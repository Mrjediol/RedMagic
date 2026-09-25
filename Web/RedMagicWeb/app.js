// app.js
// -----------------------------------------------------------------------------
// Tab shell + wiring. Owns every piece of shared state (image, boxes, lanes,
// selection, mode) and calls into the pure modules for the actual algorithms;
// this file's own job is DOM: reading inputs, building the lane/unsorted-grid
// UI, and reacting to canvas mouse events. See ARCHITECTURE.md for exactly
// which function here to edit for a given change.

import { removeBackground } from './modules/background-removal.js';
import { despillMagentaEdge } from './modules/despill-magenta.js';
import { detectSprites, clusterIntoRows } from './modules/sprite-detection.js';
import { CanvasView } from './modules/canvas-view.js';
import * as Lanes from './modules/animation-lanes.js';
import { buildGridBoxes } from './modules/grid-autoslice.js';
import { cropToDataURL, buildExportZip, downloadBlob } from './modules/export-manifest.js';
import { initEnemyCreator } from './modules/enemy-form.js';
import { initFxCreator } from './modules/fx-form.js';
import { initBossCreator } from './modules/boss-form.js';
import { saveEnemy, getEnemy, updateEnemy, deleteEnemy, listEntries, loadImageFromDataURL, buildThumbnail } from './modules/enemy-library.js';
import { renderLibraryCards } from './modules/library-panel.js';
import { buildCombinedBundle } from './modules/enemy-bundle.js';
import { KINDS, KIND_IDS, SHEET_KIND_IDS, DEFAULT_KIND, kindOf, canonicalLanesFor, defaultLanesFor } from './modules/entry-kinds.js';
import { CanvasView as MapCanvasView } from './modules/map-canvas-view.js';
import * as MapAssets from './modules/map-assets.js';
import * as MapInstances from './modules/map-instances.js';
import * as MapTracing from './modules/map-collision-tracing.js';
import * as MapExport from './modules/map-export.js';
import * as MapLibrary from './modules/map-library.js';

// What KIND of entry the Sprites tab is currently authoring — 'enemy' (default) | 'projectile' |
// 'fx'. Everything downstream that used to be enemy-specific reads this: which lanes get
// pre-created, which names the "+ Nueva"/auto-assign dropdowns offer, the wording of the name
// field and save button, which saved entries the panel lists, and the `kind` written into the
// saved record and the exported manifest. The pipeline itself (bg removal, detect/grid slicing,
// box editing, cropping, zip building) is untouched and shared by all three.
let currentKind = DEFAULT_KIND;

/** The lanes pre-created on image load / grid re-slice, for whatever kind is selected. */
function defaultLaneNames() { return defaultLanesFor(currentKind); }

/** The canonical vocabulary offered in the lane-name dropdowns, for whatever kind is selected. */
function canonicalLaneNames() { return canonicalLanesFor(currentKind); }

// Set once each init*Creator() runs below (after `lanes` exists) — the tab-switch handler reads
// these closure variables at CLICK time, not at registration time, so declaring them after is fine.
let enemyCreator = null;
let fxCreator = null;
let bossCreator = null;

/** The creator-tab API for a kind, or null for a kind nobody configures. See KINDS[…].creator. */
function creatorFor(kind) {
  const id = kindOf(kind).creator;
  if (id === 'enemy') return enemyCreator;
  if (id === 'fx') return fxCreator;
  if (id === 'boss') return bossCreator;
  return null;
}

// ============================================================ tab shell

for (const btn of document.querySelectorAll('.tabBtn')) {
  btn.addEventListener('click', () => {
    if (btn.disabled) return;
    for (const b of document.querySelectorAll('.tabBtn')) b.classList.toggle('active', b === btn);
    for (const panel of document.querySelectorAll('.tabPanel')) {
      panel.hidden = panel.id !== `tab-${btn.dataset.tab}`;
    }
    // Enemy Creator's lane cross-reference is read-only, display-only context from the Sprites
    // tab — refreshed on switching TO the tab rather than reactively, so the two tabs stay
    // decoupled (enemy-form.js never reaches into app.js's `lanes` directly).
    if (btn.dataset.tab === 'enemy' && enemyCreator) {
      enemyCreator.refreshLaneInfo();
      enemyCreator.refreshArtLibrary();
    }
    // Same reason as above for the other two creators: their library pickers are snapshots of a
    // store any tab can write to, so they are re-listed on entry rather than kept live.
    if (btn.dataset.tab === 'fx' && fxCreator) fxCreator.refreshArtLibrary();
    if (btn.dataset.tab === 'boss' && bossCreator) bossCreator.refreshLibraries();
    if (btn.dataset.tab === 'library') renderLibraryTab();
    if (btn.dataset.tab === 'map') { renderMapLibraryLists(); mapView.frameToFit(); }
  });
}

// ============================================================ shared state

const canvas = document.getElementById('canvas');
const ctx = canvas.getContext('2d');
const canvasWrap = document.getElementById('canvasWrap');
const zoomReadout = document.getElementById('zoomReadout');

const view = new CanvasView(canvas, canvasWrap, {
  onChange: (v) => { zoomReadout.textContent = `${Math.round(v.scale * 100)}%`; },
});

let img = null;
const workCanvas = document.createElement('canvas'); // holds bg-removed RGBA data
const workCtx = workCanvas.getContext('2d');
let bgRemoved = false;

let boxes = []; // {x,y,w,h, assignedLane: null|laneId}
const selectedIndices = new Set(); // multi-selection (box indices)
let lastClickedIndex = -1; // anchor for shift-range selection
let lanes = []; // {id, type, name, fps, loop, frameBoxIndices, projectileLink}

let spriteMode = 'detect'; // 'detect' | 'grid' — which sidebar panel + action is active
let interactionMode = 'select'; // 'select' | 'addbox'
let dragState = null; // {type:'move'|'resize'|'create', ...}

// Library entry this sheet was loaded from / last saved as, or null for a sheet that has never
// been saved. "💾 Guardar como enemigo" updates this record in place instead of duplicating it
// whenever it's set — see saveToLibraryBtn's handler below.
let currentLibraryId = null;

function currentSource() { return bgRemoved ? workCanvas : img; }

function primarySelected() {
  return selectedIndices.size === 1 ? [...selectedIndices][0] : -1;
}
function clearSelection() {
  selectedIndices.clear();
  lastClickedIndex = -1;
}
function setStatus(msg) { document.getElementById('status').textContent = msg; }

// ============================================================ entry kind (enemy / projectile / fx)
//
// The kind is a property of the SHEET being authored, so it lives next to the name field rather
// than as a fourth tab: everything else about the Sprites tab — detection, slicing, boxes, lanes,
// export — is identical for all three, and a separate tab would have meant three copies of it.

const entryKindSelect = document.getElementById('entryKindSelect');
const entryNameInput = document.getElementById('enemyName');
const entryNameLabel = document.getElementById('entryNameLabel');
const saveToLibraryBtn = document.getElementById('saveToLibraryBtn');
const addToExistingSelect = document.getElementById('addToExistingSelect');
const addToExistingBtn = document.getElementById('addToExistingBtn');
const addProjBtn = document.getElementById('addProjBtn');
const savedEntriesHeading = document.getElementById('savedEntriesHeading');

// SHEET_KIND_IDS, not KIND_IDS: this selector decides what the sheet on the canvas IS, so a kind
// that owns no frames (boss) has nothing to author here and offering it would produce an entry
// whose lanes nothing reads. See modules/entry-kinds.js's `sheet` flag.
entryKindSelect.innerHTML = SHEET_KIND_IDS
  .map((id) => `<option value="${id}">${KINDS[id].icon} ${KINDS[id].label}</option>`)
  .join('');

/** Re-labels every piece of the Sprites tab that names the kind, and re-lists the saved entries. */
function applyKindToUi() {
  const meta = kindOf(currentKind);

  entryKindSelect.value = currentKind;
  entryNameLabel.textContent = meta.nameLabel;
  entryNameInput.placeholder = meta.namePlaceholder;
  saveToLibraryBtn.textContent = meta.saveLabel;
  savedEntriesHeading.textContent = `6. ${meta.plural} guardados`;

  // A projectile lane is a separate thrown object nested under `Projectiles/` in the export —
  // only an enemy has one. For a projectile/VFX entry the sheet IS that object, so the button
  // would author a nonsensical nested projectile-of-a-projectile.
  addProjBtn.hidden = !meta.projectileLanes;

  renderSpritesLibraryList();
}

/**
 * Switching kind rebuilds the default lanes, because the whole point of the kind is which lanes
 * exist. Anything already assigned would be orphaned by that, so it asks first — the same
 * courtesy `confirmReplaceIfNeeded` extends before a re-detect — and a decline reverts the
 * <select> rather than leaving it showing a kind that isn't active.
 */
function setKind(next, { rebuildLanes = true } = {}) {
  if (next === currentKind) return true;

  const hasAssignedFrames = lanes.some((l) => l.frameBoxIndices.length > 0);
  if (rebuildLanes && hasAssignedFrames) {
    const ok = confirm(
      `Cambiar a "${kindOf(next).label}" recrea las animaciones por defecto de ese tipo y ` +
      'desasigna los frames que ya hubieras repartido. Los sprites detectados se conservan. ¿Continuar?'
    );
    if (!ok) { entryKindSelect.value = currentKind; return false; }
  }

  currentKind = next;

  if (rebuildLanes) {
    // Frames go back to "sin asignar" rather than being deleted: the boxes are the expensive part
    // (detection + hand editing) and are kind-agnostic, only the lane names they were sorted into
    // are not.
    lanes.forEach((l) => l.frameBoxIndices.forEach((i) => { if (boxes[i]) boxes[i].assignedLane = null; }));
    lanes = [];
    Lanes.resetLaneIdCounter();
    defaultLaneNames().forEach((name) => Lanes.createLane(lanes, 'animation', name));
  }

  applyKindToUi();
  if (rebuildLanes) { render(); renderLanes(); renderUnsorted(); updateAssignBar(); }
  return true;
}

entryKindSelect.addEventListener('change', () => { setKind(entryKindSelect.value); });

// ============================================================ image loading

document.getElementById('fileInput').addEventListener('change', (e) => {
  const f = e.target.files[0];
  if (f) loadImageFile(f);
});

// Ctrl+V alternative to the file input — same loadImageFile() entry point, so everything
// downstream (detection, bg removal, lanes) behaves identically either way. Scoped to the
// Sprites tab being the active panel; silently ignored otherwise or when the clipboard has no
// image (e.g. pasted text) — a paste of unrelated content is not something to warn about here.
document.addEventListener('paste', (e) => {
  const spritesPanel = document.getElementById('tab-sprites');
  if (!spritesPanel || spritesPanel.hidden) return;

  const items = e.clipboardData && e.clipboardData.items;
  if (!items) return;

  for (const item of items) {
    if (item.type && item.type.startsWith('image/')) {
      const f = item.getAsFile();
      if (f) loadImageFile(f);
      break;
    }
  }
});

function loadImageFile(f) {
  const reader = new FileReader();
  reader.onload = (ev) => {
    const image = new Image();
    image.onload = () => {
      img = image;
      canvas.width = img.width;
      canvas.height = img.height;
      workCanvas.width = img.width;
      workCanvas.height = img.height;
      workCtx.clearRect(0, 0, img.width, img.height);
      workCtx.drawImage(img, 0, 0);
      bgRemoved = false;
      boxes = [];
      lanes = [];
      currentLibraryId = null;
      Lanes.resetLaneIdCounter();
      defaultLaneNames().forEach((name) => Lanes.createLane(lanes, 'animation', name));
      clearSelection();
      view.frameToFit();
      render();
      renderLanes();
      renderUnsorted();
      setStatus(`Imagen cargada: ${img.width}×${img.height}px`);
    };
    image.src = ev.target.result;
  };
  reader.readAsDataURL(f);
}

window.addEventListener('resize', () => { if (img) view.frameToFit(); });

// ============================================================ background removal

// Snapshot of the canvas right after removeBackground, before despill — the baseline despill
// re-applies onto (so dragging the sliders never compounds erosion on an already-eroded image).
let postBgRemovalImageData = null;
let despillShowingBefore = false;

document.getElementById('removeBgBtn').addEventListener('click', () => {
  if (!img) { setStatus('Carga una imagen primero.'); return; }

  const tolerance = parseInt(document.getElementById('tolerance').value, 10);
  const detectPockets = document.getElementById('pocketsEnabled').checked;
  const imageData = workCtx.getImageData(0, 0, workCanvas.width, workCanvas.height);
  removeBackground(imageData, tolerance, { detectPockets });

  postBgRemovalImageData = new ImageData(
    new Uint8ClampedArray(imageData.data), imageData.width, imageData.height
  );
  despillShowingBefore = false;

  if (document.getElementById('despillEnabled').checked) {
    applyDespill();
  } else {
    workCtx.putImageData(imageData, 0, 0);
  }

  bgRemoved = true;
  render();
  setStatus('Fondo eliminado. Revisa el resultado; si quedaron restos, sube la tolerancia y repite.');
});

/** Re-runs despill from the post-bg-removal baseline using the current slider values. */
function applyDespill() {
  if (!postBgRemovalImageData) return;
  const imageData = new ImageData(
    new Uint8ClampedArray(postBgRemovalImageData.data),
    postBgRemovalImageData.width, postBgRemovalImageData.height
  );
  const magentaThreshold = parseInt(document.getElementById('despillThreshold').value, 10);
  const edgeRadius = parseInt(document.getElementById('despillRadius').value, 10);
  despillMagentaEdge(imageData, { magentaThreshold, edgeRadius });
  workCtx.putImageData(imageData, 0, 0);
  despillShowingBefore = false;
  render();
}

const despillThresholdInput = document.getElementById('despillThreshold');
const despillRadiusInput = document.getElementById('despillRadius');
const despillThresholdVal = document.getElementById('despillThresholdVal');
const despillRadiusVal = document.getElementById('despillRadiusVal');

despillThresholdInput.addEventListener('input', () => {
  despillThresholdVal.textContent = despillThresholdInput.value;
  if (postBgRemovalImageData) applyDespill();
});
despillRadiusInput.addEventListener('input', () => {
  despillRadiusVal.textContent = despillRadiusInput.value;
  if (postBgRemovalImageData) applyDespill();
});

document.getElementById('despillReapplyBtn').addEventListener('click', () => {
  if (!postBgRemovalImageData) { setStatus('Quita el fondo primero.'); return; }
  applyDespill();
  setStatus('Halo magenta reaplicado.');
});

document.getElementById('despillToggleBtn').addEventListener('click', () => {
  if (!postBgRemovalImageData) { setStatus('Quita el fondo primero.'); return; }
  despillShowingBefore = !despillShowingBefore;
  if (despillShowingBefore) {
    workCtx.putImageData(postBgRemovalImageData, 0, 0);
    setStatus('Mostrando: antes de quitar el halo magenta.');
  } else {
    applyDespill();
    setStatus('Mostrando: después de quitar el halo magenta.');
  }
  render();
});

// ============================================================ sprite mode switch (detect vs grid)

const modeDetectBtn = document.getElementById('modeDetectBtn');
const modeGridBtn = document.getElementById('modeGridBtn');
const detectModePanel = document.getElementById('detectModePanel');
const gridModePanel = document.getElementById('gridModePanel');

function setSpriteMode(next) {
  spriteMode = next;
  modeDetectBtn.classList.toggle('active', next === 'detect');
  modeGridBtn.classList.toggle('active', next === 'grid');
  detectModePanel.hidden = next !== 'detect';
  gridModePanel.hidden = next !== 'grid';
}
modeDetectBtn.addEventListener('click', () => setSpriteMode('detect'));
modeGridBtn.addEventListener('click', () => setSpriteMode('grid'));

/** True if replacing boxes right now would actually discard assigned work. */
function confirmReplaceIfNeeded(actionLabel) {
  const hasAssignments = lanes.some((l) => l.frameBoxIndices.length > 0);
  if (!hasAssignments) return true;
  return confirm(
    `${actionLabel} reemplaza todos los sprites detectados y vacía los frames ya asignados en las animaciones (las animaciones en sí no se borran). ¿Continuar?`
  );
}

// ---- detection mode: connected-component algorithm, ported to sprite-detection.js ----
document.getElementById('detectBtn').addEventListener('click', () => {
  if (!img) { setStatus('Carga una imagen primero.'); return; }
  if (!bgRemoved) setStatus('Recomendado: quita el fondo antes de detectar (paso 2).');
  if (!confirmReplaceIfNeeded('Detectar sprites')) return;

  const minArea = parseInt(document.getElementById('minArea').value, 10) || 400;
  const dilation = parseInt(document.getElementById('dilation').value, 10) || 0;
  const padding = parseInt(document.getElementById('padding').value, 10) || 0;

  const imageData = workCtx.getImageData(0, 0, workCanvas.width, workCanvas.height);
  boxes = detectSprites(imageData, { minArea, dilation, padding });
  // The boxes array is entirely new, so every old frameBoxIndices entry is now stale — but the
  // LANES themselves (names, fps/loop, the 5 defaults in particular) must survive so "Animaciones
  // automáticas" still has lanes to map detected rows onto afterwards. See
  // Lanes.clearAllFrameAssignments's doc comment.
  Lanes.clearAllFrameAssignments(lanes);

  clearSelection();
  render(); renderLanes(); renderUnsorted();
  setStatus(`Detectados ${boxes.length} sprites. Revisa, elimina texto/ruido si se coló, y asígnalos a animaciones abajo.`);
});

// ---- grid mode: uniform rows×cols slice, one lane per row ----
document.getElementById('gridSliceBtn').addEventListener('click', () => {
  if (!img) { setStatus('Carga una imagen primero.'); return; }
  if (!confirmReplaceIfNeeded('Auto-slice por grid')) return;

  const rows = parseInt(document.getElementById('gridRows').value, 10) || 1;
  const cols = parseInt(document.getElementById('gridCols').value, 10) || 1;

  const { boxes: gridBoxes, rowIndices } = buildGridBoxes(canvas.width, canvas.height, rows, cols);
  boxes = gridBoxes;
  // Grid mode fully rebuilds the lane list (Row_0, Row_1... below) rather than just clearing
  // frames — unlike Detect, it immediately owns and fills every lane it creates. Still recreates
  // the 5 defaults first so they're available (even if empty) for a later "Animaciones automáticas"
  // pass, same as a fresh image load.
  lanes = [];
  Lanes.resetLaneIdCounter();
  defaultLaneNames().forEach((name) => Lanes.createLane(lanes, 'animation', name));

  rowIndices.forEach((indices, r) => {
    const lane = Lanes.createLane(lanes, 'animation', `Row_${r}`);
    Lanes.assignBoxesToLane(lanes, boxes, lane.id, indices);
  });

  clearSelection();
  render(); renderLanes(); renderUnsorted();
  setStatus(`Grid ${rows}×${cols}: ${boxes.length} celdas en ${rows} animaciones (Row_0…Row_${rows - 1}). Renómbralas abajo.`);
});

// ============================================================ canvas rendering

function render() {
  if (!img) return;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.drawImage(currentSource(), 0, 0);

  boxes.forEach((b, i) => {
    const selected = selectedIndices.has(i);
    ctx.strokeStyle = b.assignedLane ? '#5ec8a8' : (selected ? '#e8b04b' : '#e85b5b');
    ctx.lineWidth = (selected ? 3 : 1.5) / view.scale; // keep stroke width constant on screen at any zoom
    ctx.strokeRect(b.x, b.y, b.w, b.h);

    ctx.fillStyle = 'rgba(0,0,0,0.6)';
    ctx.fillRect(b.x, b.y - 14, 22, 14);
    ctx.fillStyle = '#fff';
    ctx.font = '11px sans-serif';
    ctx.fillText(i, b.x + 3, b.y - 3);

    if (selected && primarySelected() === i) {
      const hs = 6 / view.scale;
      ctx.fillStyle = '#e8b04b';
      [[b.x, b.y], [b.x + b.w, b.y], [b.x, b.y + b.h], [b.x + b.w, b.y + b.h]].forEach(([hx, hy]) => {
        ctx.fillRect(hx - hs / 2, hy - hs / 2, hs, hs);
      });
    }
  });
}

// ============================================================ canvas interaction

document.getElementById('addBoxModeBtn').addEventListener('click', (e) => {
  interactionMode = interactionMode === 'addbox' ? 'select' : 'addbox';
  e.target.classList.toggle('active', interactionMode === 'addbox');
});

function getPos(e) {
  const p = view.screenToCanvas(e.clientX, e.clientY);
  return { x: Math.round(p.x), y: Math.round(p.y) };
}

function hitTestHandle(pos, b) {
  const hs = 8 / view.scale;
  const corners = { tl: [b.x, b.y], tr: [b.x + b.w, b.y], bl: [b.x, b.y + b.h], br: [b.x + b.w, b.y + b.h] };
  for (const key in corners) {
    const [cx, cy] = corners[key];
    if (Math.abs(pos.x - cx) < hs && Math.abs(pos.y - cy) < hs) return key;
  }
  return null;
}

// shared selection logic used by both the canvas and the sidebar thumbnails
function applySelectionClick(i, ctrlKey, shiftKey) {
  if (shiftKey && lastClickedIndex >= 0) {
    const [a, b] = [lastClickedIndex, i].sort((x, y) => x - y);
    for (let k = a; k <= b; k++) selectedIndices.add(k);
  } else if (ctrlKey) {
    if (selectedIndices.has(i)) selectedIndices.delete(i); else selectedIndices.add(i);
    lastClickedIndex = i;
  } else {
    selectedIndices.clear();
    selectedIndices.add(i);
    lastClickedIndex = i;
  }
}

canvas.addEventListener('mousedown', (e) => {
  if (!img || e.button !== 0) return; // left button only — middle is reserved for panning
  const pos = getPos(e);

  if (interactionMode === 'addbox') {
    dragState = { type: 'create', startX: pos.x, startY: pos.y };
    return;
  }

  const primary = primarySelected();
  if (primary >= 0) {
    const b = boxes[primary];
    const handle = hitTestHandle(pos, b);
    if (handle) {
      dragState = { type: 'resize', handle, box: b };
      return;
    }
  }

  for (let i = boxes.length - 1; i >= 0; i--) {
    const b = boxes[i];
    if (pos.x >= b.x && pos.x <= b.x + b.w && pos.y >= b.y && pos.y <= b.y + b.h) {
      applySelectionClick(i, e.ctrlKey || e.metaKey, e.shiftKey);
      if (selectedIndices.size === 1 && selectedIndices.has(i)) {
        dragState = { type: 'move', box: b, offX: pos.x - b.x, offY: pos.y - b.y };
      }
      render(); updateAssignBar(); renderUnsorted();
      return;
    }
  }
  clearSelection();
  render(); updateAssignBar(); renderUnsorted();
});

canvas.addEventListener('mousemove', (e) => {
  if (!dragState || !img) return;
  const pos = getPos(e);

  if (dragState.type === 'create') {
    render();
    const x = Math.min(dragState.startX, pos.x), y = Math.min(dragState.startY, pos.y);
    const w = Math.abs(pos.x - dragState.startX), h = Math.abs(pos.y - dragState.startY);
    ctx.strokeStyle = '#e8b04b'; ctx.lineWidth = 2 / view.scale; ctx.setLineDash([4 / view.scale, 3 / view.scale]);
    ctx.strokeRect(x, y, w, h); ctx.setLineDash([]);
  } else if (dragState.type === 'move') {
    dragState.box.x = pos.x - dragState.offX;
    dragState.box.y = pos.y - dragState.offY;
    render();
  } else if (dragState.type === 'resize') {
    const b = dragState.box;
    if (dragState.handle === 'br') { b.w = pos.x - b.x; b.h = pos.y - b.y; }
    if (dragState.handle === 'tl') { const ex = b.x + b.w, ey = b.y + b.h; b.x = pos.x; b.y = pos.y; b.w = ex - b.x; b.h = ey - b.y; }
    if (dragState.handle === 'tr') { const ey = b.y + b.h; b.y = pos.y; b.w = pos.x - b.x; b.h = ey - b.y; }
    if (dragState.handle === 'bl') { const ex = b.x + b.w; b.x = pos.x; b.w = ex - b.x; b.h = pos.y - b.y; }
    render();
  }
});

canvas.addEventListener('mouseup', (e) => {
  if (!dragState) return;
  const pos = getPos(e);
  if (dragState.type === 'create') {
    const x = Math.min(dragState.startX, pos.x), y = Math.min(dragState.startY, pos.y);
    const w = Math.abs(pos.x - dragState.startX), h = Math.abs(pos.y - dragState.startY);
    if (w > 4 && h > 4) {
      boxes.push({ x, y, w, h, assignedLane: null });
      selectedIndices.clear();
      selectedIndices.add(boxes.length - 1);
      lastClickedIndex = boxes.length - 1;
    }
    interactionMode = 'select';
    document.getElementById('addBoxModeBtn').classList.remove('active');
  }
  dragState = null;
  render(); updateAssignBar(); renderUnsorted();
});

document.addEventListener('keydown', (e) => {
  if ((e.key === 'Delete' || e.key === 'Backspace') && selectedIndices.size > 0 && document.activeElement.tagName !== 'INPUT') {
    removeSelectedBoxes([...selectedIndices]);
  }
});

function removeSelectedBoxes(indices) {
  Lanes.removeBoxes(boxes, lanes, indices);
  clearSelection();
  render(); updateAssignBar(); renderUnsorted(); renderLanes();
}

document.getElementById('deleteBoxBtn').addEventListener('click', () => {
  if (selectedIndices.size > 0) removeSelectedBoxes([...selectedIndices]);
});
document.getElementById('clearSelBtn').addEventListener('click', () => {
  clearSelection();
  render(); updateAssignBar(); renderUnsorted();
});

// ============================================================ lanes (animations + projectiles)

// "+ Nueva" no longer takes free text for the common case — the canonical names of the CURRENT
// KIND not yet used as a lane are offered as a dropdown (so they can't be mistyped against the
// vocabulary the Unity side expects; see modules/entry-kinds.js, and Lanes.CANONICAL_ANIMATION_NAMES
// for the enemy one it reuses), with "Personalizado…" revealing a text field for genuinely
// content-specific extras (Attack2, etc.) that aren't part of that vocabulary.
const newLaneOverlay = document.getElementById('newLaneOverlay');
const newLaneNameSelect = document.getElementById('newLaneNameSelect');
const newLaneCustomName = document.getElementById('newLaneCustomName');
const CUSTOM_LANE_NAME_VALUE = '__custom__';

document.getElementById('addLaneBtn').addEventListener('click', openNewLaneModal);
document.getElementById('newLaneCancelBtn').addEventListener('click', closeNewLaneModal);
document.getElementById('newLaneConfirmBtn').addEventListener('click', confirmNewLane);
newLaneNameSelect.addEventListener('change', syncNewLaneCustomVisibility);

function openNewLaneModal() {
  const existingNames = new Set(lanes.filter((l) => l.type === 'animation').map((l) => l.name));
  const available = canonicalLaneNames().filter((n) => !existingNames.has(n));
  newLaneNameSelect.innerHTML = available.map((n) => `<option value="${n}">${n}</option>`).join('')
    + `<option value="${CUSTOM_LANE_NAME_VALUE}">Personalizado…</option>`;
  newLaneCustomName.value = '';
  syncNewLaneCustomVisibility();
  newLaneOverlay.hidden = false;
}
function closeNewLaneModal() { newLaneOverlay.hidden = true; }
function syncNewLaneCustomVisibility() {
  const isCustom = newLaneNameSelect.value === CUSTOM_LANE_NAME_VALUE;
  newLaneCustomName.hidden = !isCustom;
  if (isCustom) newLaneCustomName.focus();
}
function confirmNewLane() {
  const name = newLaneNameSelect.value === CUSTOM_LANE_NAME_VALUE
    ? newLaneCustomName.value.trim()
    : newLaneNameSelect.value;
  if (!name) { setStatus('Escribe un nombre para la animación.'); return; }
  Lanes.createLane(lanes, 'animation', name);
  closeNewLaneModal();
  renderLanes(); updateAssignBar();
}

// Only reachable for a kind that carries projectile lanes — applyKindToUi() hides the button
// otherwise. Guarded here too so a stale click (or a call from elsewhere) can't create a nested
// projectile lane on a projectile/VFX sheet, which the exporter would nest under `Projectiles/`
// and Unity would read as a thrown prop that doesn't exist.
addProjBtn.addEventListener('click', () => {
  if (!kindOf(currentKind).projectileLanes) return;
  const name = prompt('Nombre del proyectil (ej. MushroomSpore):');
  if (!name) return;
  Lanes.createLane(lanes, 'projectile', name);
  renderLanes(); updateAssignBar();
});

// ============================================================ auto-assign (row clustering → lanes)
//
// Separate feature from grid auto-slice (modules/grid-autoslice.js): grid assumes a perfect,
// uniform rows×cols sheet and builds its own boxes from scratch. This instead works on whatever
// boxes the user already has — detected, manually added, manually deleted/resized — clustering
// them into rows with the exact same rule sortReadingOrder() already uses for frame order
// (modules/sprite-detection.js's clusterIntoRows), then lets the user map each row to a lane.

const autoAssignOverlay = document.getElementById('autoAssignOverlay');
const autoAssignRowsEl = document.getElementById('autoAssignRows');
let pendingAutoAssignRows = null; // Array<Array<box & {_boxIndex}>>, left-to-right within each row

document.getElementById('autoAssignBtn').addEventListener('click', openAutoAssignPanel);
document.getElementById('autoAssignCancelBtn').addEventListener('click', closeAutoAssignPanel);
document.getElementById('autoAssignConfirmBtn').addEventListener('click', confirmAutoAssign);

function openAutoAssignPanel() {
  if (!img) { setStatus('Carga una imagen primero.'); return; }

  // Only the boxes still unassigned — respects whatever the user already assigned by hand,
  // and whatever manual edits/deletions they made to the detected boxes.
  const unassigned = boxes
    .map((b, i) => ({ ...b, _boxIndex: i }))
    .filter((b) => !b.assignedLane);

  if (unassigned.length === 0) {
    setStatus('No hay sprites sin asignar para agrupar en animaciones.');
    return;
  }

  pendingAutoAssignRows = clusterIntoRows(unassigned, 40)
    .map((items) => [...items].sort((a, b) => a.x - b.x)); // left-to-right = frame order convention

  renderAutoAssignRows();
  autoAssignOverlay.hidden = false;
}

function closeAutoAssignPanel() {
  autoAssignOverlay.hidden = true;
  pendingAutoAssignRows = null;
}

// Sentinel prefix for a canonical name that has no lane yet — confirmAutoAssign creates it (with
// the exact canonical spelling) at confirm time instead of requiring it to be pre-created via
// "+ Nueva". Every canonical entry of the current kind is always offered, existing or not; any
// custom (non-canonical) lane the user already made is appended after them.
const NEW_CANONICAL_LANE_PREFIX = 'canonical:';

function buildAutoAssignLaneOptions(animLanes) {
  const byName = new Map(animLanes.map((l) => [l.name, l]));
  const canonical = canonicalLaneNames().map((name) => {
    const lane = byName.get(name);
    const value = lane ? lane.id : `${NEW_CANONICAL_LANE_PREFIX}${name}`;
    return `<option value="${value}" data-name="${name}">${name}</option>`;
  });
  const custom = animLanes
    .filter((l) => !canonicalLaneNames().includes(l.name))
    .map((l) => `<option value="${l.id}" data-name="${l.name}">${l.name}</option>`);
  return canonical.concat(custom).join('');
}

function renderAutoAssignRows() {
  autoAssignRowsEl.innerHTML = '';
  const animLanes = lanes.filter((l) => l.type === 'animation');
  const useDefaultMapping = pendingAutoAssignRows.length === defaultLaneNames().length;

  pendingAutoAssignRows.forEach((items, i) => {
    const row = document.createElement('div');
    row.className = 'autoAssignRow';

    const options = '<option value="">Sin asignar / omitir</option>' + buildAutoAssignLaneOptions(animLanes);

    row.innerHTML = `
      <span class="autoAssignLabel">Fila ${i + 1} (${items.length} sprite${items.length > 1 ? 's' : ''})</span>
      <select data-row="${i}">${options}</select>
    `;

    if (useDefaultMapping) {
      const select = row.querySelector('select');
      const preferred = [...select.options].find((o) => o.dataset.name === defaultLaneNames()[i]);
      if (preferred) select.value = preferred.value;
    }

    autoAssignRowsEl.appendChild(row);
  });
}

function confirmAutoAssign() {
  if (!pendingAutoAssignRows) return;

  // Reuses the exact same assignment path the manual "Asignar" button uses
  // (Lanes.assignBoxesToLane) — same pull-out-of-old-lane + push-in-order behavior. A value
  // prefixed with NEW_CANONICAL_LANE_PREFIX has no lane yet — create it first, with the exact
  // canonical spelling, so it exists to assign into.
  [...autoAssignRowsEl.querySelectorAll('select')].forEach((select) => {
    if (!select.value) return; // "Sin asignar / omitir"
    let laneId = select.value;
    if (laneId.startsWith(NEW_CANONICAL_LANE_PREFIX)) {
      const name = laneId.slice(NEW_CANONICAL_LANE_PREFIX.length);
      laneId = Lanes.createLane(lanes, 'animation', name).id;
    }
    const items = pendingAutoAssignRows[parseInt(select.dataset.row, 10)];
    Lanes.assignBoxesToLane(lanes, boxes, laneId, items.map((b) => b._boxIndex));
  });

  closeAutoAssignPanel();
  render(); renderLanes(); renderUnsorted(); updateAssignBar();
  setStatus('Animaciones automáticas asignadas.');
}

function renderLanes() {
  const list = document.getElementById('lanesList');
  list.innerHTML = '';

  const animLanes = lanes.filter((l) => l.type === 'animation');
  const projLanes = lanes.filter((l) => l.type === 'projectile');

  if (animLanes.length) {
    const lbl = document.createElement('div');
    lbl.className = 'sectionLabel';
    lbl.textContent = `Animaciones · ${kindOf(currentKind).label}`;
    list.appendChild(lbl);
    animLanes.forEach((lane) => list.appendChild(buildLaneCard(lane, projLanes)));
  }
  if (projLanes.length) {
    const lbl = document.createElement('div');
    lbl.className = 'sectionLabel';
    lbl.textContent = 'Animaciones de proyectiles';
    list.appendChild(lbl);
    projLanes.forEach((lane) => list.appendChild(buildLaneCard(lane, projLanes)));
  }
}

function buildLaneCard(lane, projLanes) {
  const card = document.createElement('div');
  card.className = 'laneCard' + (lane.type === 'projectile' ? ' projectileCard' : '');

  let projectileControlsHtml = '';
  if (lane.type === 'animation') {
    const hasProj = lane.projectileLink != null;
    const projOptions = projLanes.map((p) =>
      `<option value="${p.id}" ${hasProj && lane.projectileLink.projId === p.id ? 'selected' : ''}>${p.name}</option>`
    ).join('');
    projectileControlsHtml = `
      <div class="laneMeta" style="margin-top:4px;">
        <label style="display:flex;align-items:center;gap:3px;font-size:11px;width:auto;">
          <input type="checkbox" ${hasProj ? 'checked' : ''} data-act="projToggle" style="width:auto;"> 🎯 Dispara proyectil
        </label>
      </div>
      <div class="laneMeta" data-role="projFields" style="${hasProj ? '' : 'display:none;'}">
        <select data-act="projSelect" style="flex:2;" ${projLanes.length === 0 ? 'disabled' : ''}>
          ${projLanes.length === 0 ? '<option>Crea un proyectil primero →</option>' : projOptions}
        </select>
        <input type="number" data-act="spawnFrame" min="0" max="${Math.max(0, lane.frameBoxIndices.length - 1)}"
               value="${hasProj ? lane.projectileLink.spawnFrame : 0}" title="Frame de lanzamiento" style="flex:1;">
      </div>
    `;
  }

  card.innerHTML = `
    <div class="laneHeader">
      <span class="laneName">${lane.type === 'projectile' ? '🎯 ' : ''}${lane.name}</span>
      <button class="small danger" data-act="delLane">✕</button>
    </div>
    <div class="laneMeta">
      <input type="number" value="${lane.fps}" title="FPS" data-act="fps" style="width:50px;">
      <label style="display:flex;align-items:center;gap:3px;font-size:11px;">
        <input type="checkbox" ${lane.loop ? 'checked' : ''} data-act="loop" style="width:auto;"> loop
      </label>
    </div>
    ${projectileControlsHtml}
    <div class="laneFrames"></div>
  `;

  card.querySelector('[data-act=delLane]').onclick = () => {
    Lanes.deleteLane(lanes, boxes, lane.id);
    renderLanes(); renderUnsorted(); render();
  };
  card.querySelector('[data-act=fps]').onchange = (e) => { lane.fps = parseFloat(e.target.value) || 8; };
  card.querySelector('[data-act=loop]').onchange = (e) => { lane.loop = e.target.checked; };

  if (lane.type === 'animation') {
    const toggle = card.querySelector('[data-act=projToggle]');
    toggle.onchange = (e) => {
      Lanes.setProjectileLink(lane, projLanes, e.target.checked);
      renderLanes();
    };
    const projSelect = card.querySelector('[data-act=projSelect]');
    if (projSelect) {
      projSelect.onchange = (e) => { if (lane.projectileLink) lane.projectileLink.projId = e.target.value; };
    }
    const spawnInput = card.querySelector('[data-act=spawnFrame]');
    if (spawnInput) {
      spawnInput.onchange = (e) => { if (lane.projectileLink) lane.projectileLink.spawnFrame = parseInt(e.target.value, 10) || 0; };
    }
  }

  const framesDiv = card.querySelector('.laneFrames');
  lane.frameBoxIndices.forEach((boxIdx, orderIdx) => {
    const b = boxes[boxIdx];
    const thumb = document.createElement('div');
    thumb.className = 'frameThumb';
    thumb.style.backgroundImage = `url(${cropToDataURL(currentSource(), b)})`;
    thumb.title = `Frame ${orderIdx + 1}`;

    const isFirst = orderIdx === 0, isLast = orderIdx === lane.frameBoxIndices.length - 1;
    thumb.innerHTML = `
      <span class="fIdx">${orderIdx + 1}</span>
      <div class="fCtrls">
        <span class="fBtn" data-act="left" style="${isFirst ? 'visibility:hidden;' : ''}">‹</span>
        <span class="fBtn del" data-act="remove">✕</span>
        <span class="fBtn" data-act="right" style="${isLast ? 'visibility:hidden;' : ''}">›</span>
      </div>
    `;
    thumb.querySelector('[data-act=left]').onclick = (ev) => {
      ev.stopPropagation();
      Lanes.moveFrame(lane, orderIdx, -1);
      renderLanes();
    };
    thumb.querySelector('[data-act=right]').onclick = (ev) => {
      ev.stopPropagation();
      Lanes.moveFrame(lane, orderIdx, 1);
      renderLanes();
    };
    thumb.querySelector('[data-act=remove]').onclick = (ev) => {
      ev.stopPropagation();
      Lanes.removeFrameFromLane(lanes, boxes, lane.id, orderIdx);
      renderLanes(); renderUnsorted(); render();
    };
    framesDiv.appendChild(thumb);
  });

  return card;
}

// ============================================================ unsorted sprite grid + multi-select assign

function renderUnsorted() {
  const grid = document.getElementById('unsortedGrid');
  grid.innerHTML = '';
  let count = 0;
  boxes.forEach((b, i) => {
    if (b.assignedLane) return;
    count++;
    const div = document.createElement('div');
    div.className = 'detBox' + (selectedIndices.has(i) ? ' selected' : '');
    div.style.backgroundImage = `url(${cropToDataURL(currentSource(), b)})`;
    div.innerHTML = `<span class="idx">${i}</span>`;
    div.onclick = (e) => {
      applySelectionClick(i, e.ctrlKey || e.metaKey, e.shiftKey);
      render(); updateAssignBar(); renderUnsorted();
    };
    grid.appendChild(div);
  });
  document.getElementById('unsortedCount').textContent = count;
}

function updateAssignBar() {
  const bar = document.getElementById('assignBar');
  const select = document.getElementById('assignSelect');
  const selCount = document.getElementById('selCount');

  if (selectedIndices.size === 0) {
    bar.style.display = 'none';
    return;
  }
  bar.style.display = 'flex';
  selCount.textContent = `${selectedIndices.size} seleccionado${selectedIndices.size > 1 ? 's' : ''}`;

  if (lanes.length === 0) {
    select.innerHTML = '<option>Crea una animación primero →</option>';
  } else {
    const animOpts = lanes.filter((l) => l.type === 'animation').map((l) => `<option value="${l.id}">${l.name}</option>`).join('');
    const projOpts = lanes.filter((l) => l.type === 'projectile').map((l) => `<option value="${l.id}">${l.name}</option>`).join('');
    select.innerHTML =
      (animOpts ? `<optgroup label="Animaciones">${animOpts}</optgroup>` : '') +
      (projOpts ? `<optgroup label="Proyectiles">${projOpts}</optgroup>` : '');
  }
}

document.getElementById('assignBtn').addEventListener('click', () => {
  if (selectedIndices.size === 0 || lanes.length === 0) return;
  const laneId = document.getElementById('assignSelect').value;
  const lane = Lanes.assignBoxesToLane(lanes, boxes, laneId, [...selectedIndices]);
  if (!lane) return;

  clearSelection();
  render(); renderLanes(); renderUnsorted(); updateAssignBar();
  setStatus(`${lane.frameBoxIndices.length} frames en "${lane.name}".`);
});

// ============================================================ export

document.getElementById('exportBtn').addEventListener('click', async () => {
  if (!img) { setStatus('Carga una imagen primero.'); return; }
  const meta = kindOf(currentKind);
  const enemyName = entryNameInput.value.trim() || meta.fallbackName;
  const hasAnim = lanes.some((l) => l.type === 'animation' && l.frameBoxIndices.length > 0);
  if (!hasAnim) { setStatus(`Crea al menos una animación de ${meta.label.toLowerCase()} y asígnale sprites.`); return; }

  setStatus('Generando .zip...');
  const { blob, manifest } = await buildExportZip({
    enemyName, kind: currentKind, lanes, boxes, source: currentSource(), JSZip: window.JSZip,
  });
  downloadBlob(blob, `${enemyName}.zip`);
  setStatus(`Exportado ${enemyName}.zip (${meta.label}) — ${manifest.animations.length} animaciones, ${manifest.projectiles.length} proyectiles. Descomprímelo dentro de Assets/ en Unity.`);
});

// ============================================================ Enemy Creator tab

enemyCreator = initEnemyCreator({
  formRoot: document.getElementById('enemyFormRoot'),
  laneInfoEl: document.getElementById('enemyLaneInfo'),
  previewEl: document.getElementById('enemyJsonPreview'),
  summaryEl: document.getElementById('enemyValidationSummary'),
  exportBtn: document.getElementById('enemyExportBtn'),
  exportCombinedBtn: document.getElementById('enemyExportCombinedBtn'),
  // Display-only cross-reference (see the tab-switch handler above) — never a schema field.
  getLaneNames: () => lanes.filter((l) => l.type === 'animation').map((l) => l.name),
});

// ============================================================ Projectile/FX Creator tab

fxCreator = initFxCreator({
  formRoot: document.getElementById('fxFormRoot'),
  previewEl: document.getElementById('fxJsonPreview'),
  summaryEl: document.getElementById('fxValidationSummary'),
  exportBtn: document.getElementById('fxExportBtn'),
  exportCombinedBtn: document.getElementById('fxExportCombinedBtn'),
});

// ============================================================ Boss Creator tab

bossCreator = initBossCreator({
  formRoot: document.getElementById('bossFormRoot'),
  previewEl: document.getElementById('bossJsonPreview'),
  summaryEl: document.getElementById('bossValidationSummary'),
  exportBtn: document.getElementById('bossExportBtn'),
  zipBtn: document.getElementById('bossZipBtn'),
  saveBtn: document.getElementById('bossSaveBtn'),
  // A boss is the one kind with no Sprites-tab presence, so saving it is the only way it can ever
  // appear in Biblioteca — re-render that list so a save is visible without a tab round-trip.
  onSaved: () => renderLibraryTab(),
});

// ============================================================ shared enemy library (persistent, cross-tab)

saveToLibraryBtn.addEventListener('click', async () => {
  if (!img) { setStatus('Carga una imagen primero.'); return; }
  const meta = kindOf(currentKind);
  const enemyName = entryNameInput.value.trim() || meta.fallbackName;
  const hasAnim = lanes.some((l) => l.type === 'animation' && l.frameBoxIndices.length > 0);
  if (!hasAnim) { setStatus(`Crea al menos una animación de ${meta.label.toLowerCase()} y asígnale sprites antes de guardar.`); return; }

  // currentSource() is `img` (a plain <img>, no toDataURL) whenever bg removal hasn't run yet —
  // draw it into a throwaway canvas first so this works regardless of bgRemoved.
  const snap = document.createElement('canvas');
  snap.width = img.width;
  snap.height = img.height;
  snap.getContext('2d').drawImage(currentSource(), 0, 0);
  const sourceDataURL = snap.toDataURL();
  const thumbnail = buildThumbnail(snap);

  const record = await saveEnemy({
    id: currentLibraryId,
    kind: currentKind,
    enemyName,
    sprite: { lanes: structuredClone(lanes), boxes: structuredClone(boxes), sourceDataURL, width: img.width, height: img.height },
    thumbnail,
  });
  currentLibraryId = record.id;

  renderSpritesLibraryList();
  setStatus(`${meta.label} "${enemyName}" guardado en la biblioteca (${new Date(record.updatedAt).toLocaleTimeString()}).`);
});

/** Loads a saved entry's sheet + boxes + lanes back onto the canvas for further editing. */
async function loadLibraryEntryIntoSprites(entry) {
  const image = await loadImageFromDataURL(entry.sprite.sourceDataURL);
  img = image;
  canvas.width = entry.sprite.width;
  canvas.height = entry.sprite.height;
  workCanvas.width = entry.sprite.width;
  workCanvas.height = entry.sprite.height;
  workCtx.clearRect(0, 0, workCanvas.width, workCanvas.height);
  workCtx.drawImage(image, 0, 0);
  bgRemoved = false; // `img` already holds whatever pixels were saved (bg-removed or not) — see currentSource()

  boxes = structuredClone(entry.sprite.boxes);
  lanes = structuredClone(entry.sprite.lanes);
  currentLibraryId = entry.id;
  entryNameInput.value = entry.enemyName;

  // The entry's own kind wins over whatever was selected — the saved lanes belong to it. Lanes
  // come from the record, so the default-lane rebuild is explicitly suppressed.
  currentKind = entry.kind;
  applyKindToUi();

  clearSelection();
  view.frameToFit();
  render(); renderLanes(); renderUnsorted(); updateAssignBar();
  setStatus(`${kindOf(entry.kind).label} "${entry.enemyName}" cargado desde la biblioteca.`);
}

async function exportLibraryEntryZip(entry) {
  const image = await loadImageFromDataURL(entry.sprite.sourceDataURL);
  const c = document.createElement('canvas');
  c.width = entry.sprite.width; c.height = entry.sprite.height;
  c.getContext('2d').drawImage(image, 0, 0);

  const { blob } = await buildExportZip({
    enemyName: entry.enemyName, kind: entry.kind, lanes: entry.sprite.lanes, boxes: entry.sprite.boxes, source: c, JSZip: window.JSZip,
  });
  downloadBlob(blob, `${entry.enemyName}.zip`);
}

async function deleteLibraryEntry(entry) {
  if (!confirm(`¿Eliminar "${entry.enemyName}" de la biblioteca? Esto no se puede deshacer.`)) return;
  await deleteEnemy(entry.id);
  if (currentLibraryId === entry.id) currentLibraryId = null;
  renderSpritesLibraryList();
}

/**
 * The Sprites tab's own saved-entries panel lists only the kind currently being authored — it is
 * a "pick up where I left off" shortcut for this sheet's kind, and its "Cargar" would silently
 * switch kinds out from under the tab otherwise. The Biblioteca tab is where every kind is
 * browsed together.
 */
function renderSpritesLibraryList() {
  renderLibraryCards(document.getElementById('spritesLibraryList'), {
    fetchEntries: () => listEntries(currentKind),
    emptyMessage: kindOf(currentKind).emptyMessage,
    getActions: (entry) => [
      { label: 'Cargar', className: 'small', onClick: loadLibraryEntryIntoSprites },
      { label: 'Exportar zip', className: 'small', onClick: exportLibraryEntryZip },
      { label: 'Eliminar', className: 'small danger', onClick: deleteLibraryEntry },
    ],
  });
  refreshAddToExistingSelect();
}
applyKindToUi();

// ============================================================ "Añadir a enemigo existente" (Sprites tab)
//
// Sprites now arrive one animation per image (its own upload, its own bg-removal/despill pass,
// sliced into a single lane) instead of one grid sheet with every lane at once. "💾 Guardar como
// enemigo" already only requires ONE lane to have frames, so authoring a brand-new entry with just
// its first animation already works — but it always OVERWRITES the whole saved sheet, so re-saving
// with only this session's lane would wipe out any animation already saved from a DIFFERENT image.
// This button fixes that one gap: it composites the current session's (bg-removed/despilled) image
// onto the target entry's existing sheet instead of replacing it, offsets this session's boxes to
// match, and replaces only the lane(s) that share a name with what's in this session — every other
// lane the target already had is untouched. The saved shape stays the exact same
// `{lanes, boxes, sourceDataURL, width, height}` the rest of the app already reads, so nothing else
// (zip export, thumbnails, Biblioteca) needs to know this happened.

async function refreshAddToExistingSelect() {
  const entries = await listEntries(currentKind);
  const keepId = entries.some((e) => e.id === addToExistingSelect.value) ? addToExistingSelect.value : '';

  addToExistingSelect.innerHTML = '';
  if (entries.length === 0) {
    const opt = document.createElement('option');
    opt.value = '';
    opt.textContent = `(sin ${kindOf(currentKind).plural.toLowerCase()} guardados)`;
    addToExistingSelect.appendChild(opt);
    addToExistingSelect.disabled = true;
    addToExistingBtn.disabled = true;
    return;
  }

  addToExistingSelect.disabled = false;
  addToExistingBtn.disabled = false;
  entries.forEach((e) => {
    const opt = document.createElement('option');
    opt.value = e.id;
    opt.textContent = e.enemyName;
    addToExistingSelect.appendChild(opt);
  });
  addToExistingSelect.value = keepId || entries[0].id;
}

addToExistingBtn.addEventListener('click', async () => {
  if (!img) { setStatus('Carga una imagen primero.'); return; }
  const meta = kindOf(currentKind);
  const sessionLanes = lanes.filter((l) => l.frameBoxIndices.length > 0);
  if (sessionLanes.length === 0) { setStatus(`Crea al menos una animación de ${meta.label.toLowerCase()} y asígnale sprites antes de añadir.`); return; }

  const targetId = addToExistingSelect.value;
  if (!targetId) { setStatus('Elige a qué enemigo guardado añadir esta animación.'); return; }
  const target = await getEnemy(targetId);
  if (!target) { setStatus('El enemigo elegido ya no existe — recarga la lista.'); return; }

  // Session's own pixels, exactly like saveToLibraryBtn's snapshot (currentSource() is `img`
  // itself, no toDataURL, whenever bg removal hasn't run — always go through a canvas).
  const sessionCanvas = document.createElement('canvas');
  sessionCanvas.width = img.width;
  sessionCanvas.height = img.height;
  sessionCanvas.getContext('2d').drawImage(currentSource(), 0, 0);

  const hasExistingSheet = !!(target.sprite && target.sprite.sourceDataURL);
  const oldW = hasExistingSheet ? target.sprite.width : 0;
  const oldH = hasExistingSheet ? target.sprite.height : 0;
  const composite = document.createElement('canvas');
  composite.width = Math.max(oldW, sessionCanvas.width);
  composite.height = oldH + sessionCanvas.height;
  const cctx = composite.getContext('2d');

  if (hasExistingSheet) {
    const oldImage = await loadImageFromDataURL(target.sprite.sourceDataURL);
    cctx.drawImage(oldImage, 0, 0); // old pixels, unmoved — every OLD box/lane still points at the same spot
  }
  cctx.drawImage(sessionCanvas, 0, oldH); // this session's pixels, stacked below

  const mergedBoxes = hasExistingSheet ? structuredClone(target.sprite.boxes) : [];
  const mergedLanes = hasExistingSheet ? structuredClone(target.sprite.lanes) : [];

  sessionLanes.forEach((sessionLane) => {
    // A lane sharing this session's (type, name) REPLACES the target's existing one — its old
    // frames are simply left unreferenced in mergedBoxes (dead weight, harmless) rather than
    // spliced out, so nothing else in mergedBoxes needs re-indexing.
    const existingIdx = mergedLanes.findIndex((l) => l.type === sessionLane.type && l.name === sessionLane.name);
    if (existingIdx >= 0) mergedLanes.splice(existingIdx, 1);

    const newFrameIndices = sessionLane.frameBoxIndices.map((boxIdx) => {
      const b = boxes[boxIdx];
      mergedBoxes.push({ x: b.x, y: b.y + oldH, w: b.w, h: b.h, assignedLane: null });
      return mergedBoxes.length - 1;
    });

    mergedLanes.push({
      id: `lane${mergedLanes.length}_${sessionLane.name}`,
      type: sessionLane.type,
      name: sessionLane.name,
      fps: sessionLane.fps,
      loop: sessionLane.loop,
      frameBoxIndices: newFrameIndices,
      projectileLink: sessionLane.type === 'animation' ? null : undefined,
    });
  });

  const record = await saveEnemy({
    id: target.id,
    kind: target.kind,
    enemyName: target.enemyName,
    sprite: { lanes: mergedLanes, boxes: mergedBoxes, sourceDataURL: composite.toDataURL(), width: composite.width, height: composite.height },
    thumbnail: buildThumbnail(composite),
  });

  renderSpritesLibraryList();
  const addedNames = sessionLanes.map((l) => l.name).join(', ');
  setStatus(`"${addedNames}" añadida(s) a "${record.enemyName}" (${new Date(record.updatedAt).toLocaleTimeString()}).`);
});

// ============================================================ Biblioteca tab (shared, full actions)

// null = every kind. Persisted only for the session; the Biblioteca tab re-renders on each switch
// to it, so the filter survives tab hopping without any storage.
let libraryKindFilter = null;

const libraryFilterBar = document.getElementById('libraryFilterBar');

function renderLibraryFilterBar() {
  const options = [{ id: null, label: 'Todo', icon: '📚' }]
    .concat(KIND_IDS.map((id) => ({ id, label: KINDS[id].plural, icon: KINDS[id].icon })));

  libraryFilterBar.innerHTML = '';
  options.forEach(({ id, label, icon }) => {
    const btn = document.createElement('button');
    btn.className = `small libFilterBtn${libraryKindFilter === id ? ' active' : ''}`;
    btn.textContent = `${icon} ${label}`;
    btn.onclick = () => { libraryKindFilter = id; renderLibraryTab(); };
    libraryFilterBar.appendChild(btn);
  });
}

/**
 * Which Creator tab an entry's "Editar en …" action opens, labelled for that kind. Driven by
 * `KINDS[kind].creator` rather than an if-chain on the kind id, so a sixth kind with its own
 * creator needs nothing here — the same rule the Sprites tab already follows for its own labels.
 */
const CREATOR_LABELS = { enemy: 'Enemy Creator', fx: 'Proyectil/VFX', boss: 'Boss Creator' };

function editActionFor(entry) {
  const creatorId = kindOf(entry.kind).creator;
  const label = creatorId ? `Editar en ${CREATOR_LABELS[creatorId]}` : 'Editar config';

  // Disabled with the reason rather than hidden: a missing button reads as a bug, a disabled one
  // with a tooltip explains the model.
  return {
    label,
    className: 'small',
    disabled: !creatorId,
    title: creatorId ? '' : 'Este tipo de entrada no tiene pantalla de configuración.',
    onClick: async (e) => {
      const creator = creatorFor(e.kind);
      if (!creator) return;
      document.querySelector(`.tabBtn[data-tab="${kindOf(e.kind).creator}"]`).click();
      // Each creator takes what it needs: the enemy one links a sheet by id+name, the fx one also
      // needs the kind (it authors two), the boss one reopens a saved config by its own record id.
      if (kindOf(e.kind).creator === 'fx') await creator.linkLibraryEntry(e.id, e.enemyName, e.kind);
      else if (kindOf(e.kind).creator === 'boss') await creator.linkLibraryEntry(e.id);
      else await creator.linkLibraryEntry(e.id, e.enemyName);
    },
  };
}

function renderLibraryTab() {
  renderLibraryFilterBar();
  renderLibraryCards(document.getElementById('libraryTabList'), {
    fetchEntries: () => listEntries(libraryKindFilter),
    emptyMessage: libraryKindFilter
      ? kindOf(libraryKindFilter).emptyMessage
      : 'Todavía no hay nada guardado. Autoriza una hoja en la pestaña Sprites y guárdala, o monta un jefe en Boss Creator.',
    getActions: (entry) => {
      // Everything sprite-shaped is gated on the kind owning a sheet at all: a boss entry has no
      // `sprite` block, so Cargar/Exportar zip/Exportar combinado would throw rather than misbehave.
      const hasSheet = kindOf(entry.kind).sheet;
      const sheetOnlyTitle = 'Un jefe no tiene hoja propia: su arte es la del enemigo base.';

      return [
        {
          label: 'Cargar en Sprites',
          className: 'small',
          disabled: !hasSheet,
          title: hasSheet ? '' : sheetOnlyTitle,
          onClick: async (e) => {
            if (!kindOf(e.kind).sheet) return;
            await loadLibraryEntryIntoSprites(e);
            document.querySelector('.tabBtn[data-tab="sprites"]').click();
          },
        },
        editActionFor(entry),
        {
          label: 'Exportar zip',
          className: 'small',
          disabled: !hasSheet,
          title: hasSheet ? '' : sheetOnlyTitle,
          onClick: (e) => { if (kindOf(e.kind).sheet) exportLibraryEntryZip(e); },
        },
        {
          label: 'Exportar combinado',
          className: 'small',
          disabled: !hasSheet || !entry.config,
          title: !hasSheet
            ? sheetOnlyTitle
            : (entry.config ? '' : `Ábrelo en ${CREATOR_LABELS[kindOf(entry.kind).creator]} y expórtalo una vez primero.`),
          onClick: async (e) => {
            if (!e.config || !kindOf(e.kind).sheet) return;
            const blob = await buildCombinedBundle({ enemyName: e.enemyName, kind: e.kind, libraryId: e.id, sprite: e.sprite, configObj: e.config });
            downloadBlob(blob, `${e.enemyName.replace(/[^A-Za-z0-9_]/g, '_')}.bundle.zip`);
          },
        },
        {
          label: 'Exportar config',
          className: 'small',
          disabled: !entry.config,
          title: entry.config ? '' : 'Esta entrada todavía no tiene configuración guardada.',
          onClick: (e) => {
            if (!e.config) return;
            const blob = new Blob([JSON.stringify(e.config, null, 2)], { type: 'application/json' });
            downloadBlob(blob, `${e.enemyName.replace(/[^A-Za-z0-9_]/g, '_')}.${e.kind}.json`);
          },
        },
        { label: 'Eliminar', className: 'small danger', onClick: async (e) => { await deleteLibraryEntry(e); renderLibraryTab(); } },
      ];
    },
  });
}

// ============================================================ Map Tracer tab
//
// Ported from the standalone Assets/Editor/MapTracer.html. Same shape as the Sprites tab's own
// canvas wiring (hitTestHandle/dragState for resize, applySelectionClick-like shift semantics for
// multi-select) but driven by the map-*.js modules instead of inline logic. See ARCHITECTURE.md's
// "Map Tracer" section for the module map and the two-kind (maps/pieces) library schema.

const mapTabPanel = document.getElementById('tab-map');
const mapCanvas = document.getElementById('mapCanvas');
const mapCtx = mapCanvas.getContext('2d');
const mapCanvasWrap = document.getElementById('mapCanvasWrap');
const mapZoomReadout = document.getElementById('mapZoomReadout');

const mapView = new MapCanvasView(mapCanvas, mapCanvasWrap, {
  onChange: (v) => { mapZoomReadout.textContent = `${Math.round(v.scale * 100)}%`; },
});

const mapFileInput = document.getElementById('mapFileInput');
const mapAssetType = document.getElementById('mapAssetType');
const mapApplyAssetType = document.getElementById('mapApplyAssetType');
const mapAssetsListEl = document.getElementById('mapAssetsList');
const mapSelectedAssetInfo = document.getElementById('mapSelectedAssetInfo');
const mapTraceBtn = document.getElementById('mapTraceBtn');
const mapBackToSceneBtn = document.getElementById('mapBackToSceneBtn');
const mapSavePieceBtn = document.getElementById('mapSavePieceBtn');
const mapDeleteAssetBtn = document.getElementById('mapDeleteAssetBtn');
const mapSceneTools = document.getElementById('mapSceneTools');
const mapTraceTools = document.getElementById('mapTraceTools');
const mapModeTitle = document.getElementById('mapModeTitle');
const mapTraceHint = document.getElementById('mapTraceHint');
const mapNewLineBtn = document.getElementById('mapNewLineBtn');
const mapFinishLineBtn = document.getElementById('mapFinishLineBtn');
const mapLinesList = document.getElementById('mapLinesList');
const mapNewBtn = document.getElementById('mapNewBtn');
const mapFitBtn = document.getElementById('mapFitBtn');
const mapWidthInput = document.getElementById('mapWidthInput');
const mapHeightInput = document.getElementById('mapHeightInput');
const mapResizeBtn = document.getElementById('mapResizeBtn');
const mapFitBorderBtn = document.getElementById('mapFitBorderBtn');
const mapSelectionInfo = document.getElementById('mapSelectionInfo');
const mapSelectAllBtn = document.getElementById('mapSelectAllBtn');
const mapDeleteInstanceBtn = document.getElementById('mapDeleteInstanceBtn');
const mapInstancesListEl = document.getElementById('mapInstancesList');
const mapExportPngBtn = document.getElementById('mapExportPngBtn');
const mapExportJsonBtn = document.getElementById('mapExportJsonBtn');
const mapNameInput = document.getElementById('mapNameInput');
const mapSaveMapBtn = document.getElementById('mapSaveMapBtn');
const mapExportSelectedPiecesBtn = document.getElementById('mapExportSelectedPiecesBtn');

const MAP_TYPE_COLOR = { border: '#f6b84a', platform: '#70b8ff' };

let mapAssets = []; // MapAssets pieces — {id,name,fileName,width,height,type,image,lines}
let mapInstances = []; // MapInstances placements — {id,assetId,x,y,scaleX,scaleY}
const selectedAssetIds = new Set();
const selectedInstanceIds = new Set();
let mapMode = 'scene'; // 'scene' | 'trace'
let traceAssetId = null;
let traceLine = null; // in-progress line while tracing
let mapDragState = null; // {type:'move'|'resize', ...}
let currentMapLibraryId = null;

// Biblioteca panel's "piezas guardadas" multi-select (export batch) — same ctrl-toggle/
// shift-range semantics as the Sprites tab's applySelectionClick, kept separate because it
// selects library RECORD ids (persisted, string uuids), not in-session box array indices.
const selectedPieceLibraryIds = new Set();
let lastClickedPieceLibraryIndex = -1;

function findMapAsset(id) { return MapAssets.findAsset(mapAssets, id); }
function setMapStatus(msg) { document.getElementById('mapStatus').textContent = msg; }

function applyMapCanvasSize() {
  mapCanvas.width = parseInt(mapWidthInput.value, 10) || 2048;
  mapCanvas.height = parseInt(mapHeightInput.value, 10) || 672;
}
applyMapCanvasSize();

// ---- rendering ----

function drawMapLine(points, type) {
  if (!points.length) return;
  mapCtx.strokeStyle = MAP_TYPE_COLOR[type];
  mapCtx.lineWidth = 4 / mapView.scale;
  mapCtx.lineJoin = 'round'; mapCtx.lineCap = 'round';
  mapCtx.beginPath();
  mapCtx.moveTo(points[0].x, points[0].y);
  points.slice(1).forEach((p) => mapCtx.lineTo(p.x, p.y));
  mapCtx.stroke();
  points.forEach((p, n) => {
    mapCtx.fillStyle = n ? '#fff' : MAP_TYPE_COLOR[type];
    mapCtx.beginPath();
    mapCtx.arc(p.x, p.y, 4 / mapView.scale, 0, Math.PI * 2);
    mapCtx.fill();
  });
}

function renderMapCanvas() {
  if (mapMode === 'trace') {
    const asset = findMapAsset(traceAssetId);
    if (!asset) return;
    mapCtx.clearRect(0, 0, mapCanvas.width, mapCanvas.height);
    mapCtx.drawImage(asset.image, 0, 0);
    asset.lines.forEach((l) => drawMapLine(l.points, l.type));
    if (traceLine) drawMapLine(traceLine.points, traceLine.type);
    return;
  }

  mapCtx.clearRect(0, 0, mapCanvas.width, mapCanvas.height);
  ['background', 'platform', 'border'].forEach((type) => {
    mapInstances.forEach((inst) => {
      const asset = findMapAsset(inst.assetId);
      if (!asset || asset.type !== type) return;
      const b = MapInstances.getInstanceBounds(inst, asset);
      mapCtx.drawImage(asset.image, b.x, b.y, b.w, b.h);
      if (selectedInstanceIds.has(inst.id)) {
        mapCtx.strokeStyle = '#5ec8a8';
        mapCtx.lineWidth = 2 / mapView.scale;
        mapCtx.strokeRect(b.x, b.y, b.w, b.h);
      }
    });
  });

  if (selectedInstanceIds.size > 0) {
    const gb = MapInstances.getGroupBounds([...selectedInstanceIds], mapInstances, findMapAsset);
    const hs = 7 / mapView.scale;
    mapCtx.strokeStyle = '#e8b04b';
    mapCtx.lineWidth = 1.5 / mapView.scale;
    mapCtx.strokeRect(gb.x, gb.y, gb.w, gb.h);
    mapCtx.fillStyle = '#e8b04b';
    [[gb.x, gb.y], [gb.x + gb.w, gb.y], [gb.x, gb.y + gb.h], [gb.x + gb.w, gb.y + gb.h]].forEach(([hx, hy]) => {
      mapCtx.fillRect(hx - hs / 2, hy - hs / 2, hs, hs);
    });
  }
}

// ---- piece library (left panel) ----

function renderMapAssetsList() {
  mapAssetsListEl.innerHTML = '';
  mapAssets.forEach((a) => {
    const div = document.createElement('div');
    div.className = 'mapAsset' + (selectedAssetIds.has(a.id) ? ' active' : '');
    div.innerHTML = `
      <img src="${a.image.src}">
      <div>
        <div class="mapAssetName">${a.name}</div>
        <span class="mapTypeBadge ${a.type}">${MapAssets.TYPE_LABEL[a.type]}</span>
      </div>
    `;
    div.onclick = (e) => {
      if (!e.shiftKey) selectedAssetIds.clear();
      if (e.shiftKey && selectedAssetIds.has(a.id)) selectedAssetIds.delete(a.id);
      else selectedAssetIds.add(a.id);
      renderMapAssetsList();
    };
    mapAssetsListEl.appendChild(div);
  });
  updateSelectedAssetInfo();
}

function updateSelectedAssetInfo() {
  const n = selectedAssetIds.size;
  if (n === 1) {
    const a = findMapAsset([...selectedAssetIds][0]);
    mapSelectedAssetInfo.textContent = `${a.name} · ${a.width}×${a.height}px · ${MapAssets.TYPE_LABEL[a.type]}`;
  } else {
    mapSelectedAssetInfo.textContent = n
      ? `${n} piezas seleccionadas. Elige un tipo y pulsa "Asignar tipo a selección".`
      : 'Selecciona una pieza de la biblioteca.';
  }
}

mapFileInput.addEventListener('change', (e) => {
  MapAssets.addFiles(e.target.files, mapAssets, (asset) => {
    selectedAssetIds.add(asset.id);
    renderMapAssetsList();
    setMapStatus(`${mapAssets.length} piezas en biblioteca. Selecciónalas y asigna su tipo.`);
  });
});
mapCanvasWrap.ondragover = (e) => e.preventDefault();
mapCanvasWrap.ondrop = (e) => {
  e.preventDefault();
  MapAssets.addFiles(e.dataTransfer.files, mapAssets, (asset) => {
    selectedAssetIds.add(asset.id);
    renderMapAssetsList();
  });
};

mapApplyAssetType.onclick = () => {
  if (!selectedAssetIds.size) { setMapStatus('Selecciona una o más piezas en la biblioteca.'); return; }
  MapAssets.assignType(mapAssets, selectedAssetIds, mapAssetType.value);
  renderMapAssetsList(); renderMapCanvas();
  setMapStatus(`Tipo asignado a ${selectedAssetIds.size} pieza(s).`);
};

mapDeleteAssetBtn.onclick = () => {
  if (!selectedAssetIds.size) return;
  MapAssets.deleteAssets(mapAssets, mapInstances, selectedAssetIds);
  selectedAssetIds.clear(); selectedInstanceIds.clear();
  renderMapAssetsList(); renderMapInstancesList(); renderMapCanvas();
};

mapSavePieceBtn.onclick = async () => {
  if (selectedAssetIds.size !== 1) { setMapStatus('Selecciona exactamente una pieza para guardarla.'); return; }
  const asset = findMapAsset([...selectedAssetIds][0]);
  if (asset.type === 'unassigned') { setMapStatus('Asigna un tipo (Borde/Fondo/Plataforma) antes de guardar la pieza.'); return; }
  const thumbnail = MapLibrary.buildThumbnail(asset.image);
  await MapLibrary.savePiece({
    name: asset.name, type: asset.type, image: asset.image,
    width: asset.width, height: asset.height, lines: asset.lines, thumbnail,
  });
  renderMapLibraryLists();
  setMapStatus(`Pieza "${asset.name}" guardada en la biblioteca.`);
};

// ---- scene ↔ trace mode ----

function enterTraceMode() {
  if (selectedAssetIds.size !== 1) { setMapStatus('Selecciona exactamente una pieza primero.'); return; }
  const asset = findMapAsset([...selectedAssetIds][0]);
  if (asset.type === 'unassigned') { setMapStatus('Asigna Borde o Plataforma antes de trazar colisiones.'); return; }
  if (asset.type === 'background') { setMapStatus('Los fondos no llevan colisiones.'); return; }

  mapMode = 'trace';
  traceAssetId = asset.id;
  traceLine = null;
  mapModeTitle.textContent = 'Colisiones: ' + asset.name;
  mapSceneTools.hidden = true;
  mapTraceTools.hidden = false;
  mapBackToSceneBtn.hidden = false;
  mapTraceHint.textContent = `Estas líneas se exportarán como ${asset.type === 'border' ? 'Ground' : 'Platform'} en Unity.`;
  mapCanvas.width = asset.width;
  mapCanvas.height = asset.height;
  renderMapLinesList();
  renderMapCanvas();
  mapView.frameToFit();
}

function exitTraceMode() {
  mapMode = 'scene';
  traceAssetId = null;
  traceLine = null;
  mapModeTitle.textContent = 'Escenario';
  mapSceneTools.hidden = false;
  mapTraceTools.hidden = true;
  mapBackToSceneBtn.hidden = true;
  applyMapCanvasSize();
  renderMapCanvas();
  mapView.frameToFit();
}

mapTraceBtn.onclick = enterTraceMode;
mapBackToSceneBtn.onclick = exitTraceMode;

function commitTraceLine() {
  const asset = findMapAsset(traceAssetId);
  const result = MapTracing.commitLine(asset, traceLine);
  traceLine = null;
  if (result.message) setMapStatus(result.message);
  renderMapLinesList(); renderMapAssetsList(); renderMapCanvas();
}

mapNewLineBtn.onclick = () => {
  const asset = findMapAsset(traceAssetId);
  traceLine = MapTracing.startLine(asset.type);
  setMapStatus('Haz clic para añadir el primer punto.');
  renderMapCanvas();
};
mapFinishLineBtn.onclick = commitTraceLine;

function renderMapLinesList() {
  mapLinesList.innerHTML = '';
  const asset = findMapAsset(traceAssetId);
  if (!asset) return;
  asset.lines.forEach((l, n) => {
    const div = document.createElement('div');
    div.className = 'mapListItem';
    div.innerHTML = `<span style="color:${MAP_TYPE_COLOR[l.type]}">●</span> ${l.type === 'border' ? 'Ground' : 'Platform'} · ${l.points.length} puntos <span class="fBtn" data-n="${n}">✕</span>`;
    div.querySelector('.fBtn').onclick = (ev) => {
      ev.stopPropagation();
      MapTracing.deleteLine(asset, n);
      renderMapLinesList(); renderMapAssetsList(); renderMapCanvas();
    };
    mapLinesList.appendChild(div);
  });
}

// ---- canvas interaction (scene mode: place/select/move/resize; trace mode: add points) ----

function mapGetPos(e) {
  const p = mapView.screenToCanvas(e.clientX, e.clientY);
  return { x: Math.round(p.x), y: Math.round(p.y) };
}

mapCanvas.oncontextmenu = (e) => e.preventDefault();

mapCanvas.addEventListener('mousedown', (e) => {
  const pos = mapGetPos(e);

  if (mapMode === 'trace') {
    if (e.button === 2) { commitTraceLine(); return; }
    if (e.button !== 0) return;
    if (!traceLine) traceLine = MapTracing.startLine(findMapAsset(traceAssetId).type);
    MapTracing.addPoint(traceLine, pos);
    renderMapCanvas();
    return;
  }

  if (e.button !== 0) return; // left only — middle is reserved for panning (MapCanvasView)

  if (selectedInstanceIds.size > 0) {
    const gb = MapInstances.getGroupBounds([...selectedInstanceIds], mapInstances, findMapAsset);
    const handle = MapInstances.hitTestResizeHandle(pos, gb, 8 / mapView.scale);
    if (handle) {
      const resizeState = MapInstances.beginResize([...selectedInstanceIds], mapInstances, findMapAsset, handle);
      mapDragState = { type: 'resize', resizeState };
      return;
    }
  }

  const hit = MapInstances.hitTestInstance(pos, mapInstances, findMapAsset);
  if (hit) {
    if (!e.shiftKey) selectedInstanceIds.clear();
    selectedInstanceIds.add(hit.id);
    const startPositions = new Map([...selectedInstanceIds].map((id) => {
      const inst = MapInstances.findInstance(mapInstances, id);
      return [id, { x: inst.x, y: inst.y }];
    }));
    mapDragState = { type: 'move', start: pos, startPositions };
    renderMapInstancesList(); renderMapCanvas(); updateMapSelectionInfo();
    return;
  }

  if (selectedAssetIds.size === 1) {
    const asset = findMapAsset([...selectedAssetIds][0]);
    const inst = MapInstances.createInstance(asset.id, pos.x, pos.y);
    mapInstances.push(inst);
    selectedInstanceIds.clear();
    selectedInstanceIds.add(inst.id);
    renderMapInstancesList(); renderMapCanvas(); updateMapSelectionInfo();
  } else {
    selectedInstanceIds.clear();
    renderMapInstancesList(); renderMapCanvas(); updateMapSelectionInfo();
    setMapStatus('Selecciona una pieza para colocarla.');
  }
});

mapCanvas.addEventListener('mousemove', (e) => {
  if (!mapDragState) return;
  const pos = mapGetPos(e);
  if (mapDragState.type === 'move') {
    const dx = pos.x - mapDragState.start.x, dy = pos.y - mapDragState.start.y;
    MapInstances.moveInstances([...selectedInstanceIds], mapInstances, dx, dy, mapDragState.startPositions);
    renderMapCanvas();
  } else if (mapDragState.type === 'resize') {
    MapInstances.applyResize(mapDragState.resizeState, pos, e.shiftKey);
    renderMapCanvas();
  }
});

mapCanvas.addEventListener('mouseup', () => {
  if (!mapDragState) return;
  mapDragState = null;
  renderMapInstancesList();
});

document.addEventListener('keydown', (e) => {
  if (mapTabPanel.hidden) return;
  if (document.activeElement && document.activeElement.tagName === 'INPUT') return;

  if (mapMode === 'trace') {
    if (e.key === 'Enter') commitTraceLine();
    if (e.key === 'Escape') { traceLine = null; renderMapCanvas(); }
    if (e.key.toLowerCase() === 'z' && traceLine) {
      if (!MapTracing.undoPoint(traceLine)) traceLine = null;
      renderMapCanvas();
    }
    return;
  }

  if ((e.key === 'Delete' || e.key === 'Backspace') && selectedInstanceIds.size > 0) {
    e.preventDefault();
    deleteSelectedMapInstances();
  }
});

function deleteSelectedMapInstances() {
  MapInstances.deleteInstances(mapInstances, selectedInstanceIds);
  selectedInstanceIds.clear();
  renderMapInstancesList(); renderMapCanvas(); updateMapSelectionInfo();
}

// ---- instances panel / scene tools (right panel) ----

function renderMapInstancesList() {
  mapInstancesListEl.innerHTML = '';
  mapInstances.forEach((inst, n) => {
    const asset = findMapAsset(inst.assetId);
    const div = document.createElement('div');
    div.className = 'mapListItem' + (selectedInstanceIds.has(inst.id) ? ' active' : '');
    div.textContent = `${n + 1}. ${asset?.name || 'pieza eliminada'} (${Math.round(inst.x)}, ${Math.round(inst.y)}) · ${Math.round((inst.scaleX ?? 1) * 100)}%`;
    div.onclick = (e) => {
      if (!e.shiftKey) selectedInstanceIds.clear();
      selectedInstanceIds.add(inst.id);
      renderMapInstancesList(); renderMapCanvas(); updateMapSelectionInfo();
    };
    mapInstancesListEl.appendChild(div);
  });
}

function updateMapSelectionInfo() {
  const n = selectedInstanceIds.size;
  mapSelectionInfo.textContent = n
    ? `${n} pieza${n === 1 ? '' : 's'} seleccionada${n === 1 ? '' : 's'}. Arrastra sus esquinas para redimensionar (Shift = proporcional).`
    : 'Sin selección.';
}

mapSelectAllBtn.onclick = () => {
  selectedInstanceIds.clear();
  mapInstances.forEach((i) => selectedInstanceIds.add(i.id));
  renderMapInstancesList(); renderMapCanvas(); updateMapSelectionInfo();
};
mapDeleteInstanceBtn.onclick = () => { if (selectedInstanceIds.size) deleteSelectedMapInstances(); };

mapNewBtn.onclick = () => {
  if (!confirm('¿Vaciar escenario y conservar biblioteca de piezas?')) return;
  mapInstances = [];
  selectedInstanceIds.clear();
  currentMapLibraryId = null;
  renderMapInstancesList(); renderMapCanvas();
};

mapFitBtn.onclick = () => mapView.frameToFit();

mapResizeBtn.onclick = () => {
  if (mapMode !== 'scene') return;
  if ((+mapWidthInput.value) > 0 && (+mapHeightInput.value) > 0) {
    applyMapCanvasSize();
    renderMapCanvas();
    mapView.frameToFit();
  }
};

mapFitBorderBtn.onclick = () => {
  const borders = mapInstances.filter((i) => findMapAsset(i.assetId)?.type === 'border');
  if (!borders.length) { setMapStatus('Añade al menos una pieza de tipo Borde.'); return; }
  let left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
  borders.forEach((i) => {
    const b = MapInstances.getInstanceBounds(i, findMapAsset(i.assetId));
    left = Math.min(left, b.x); top = Math.min(top, b.y);
    right = Math.max(right, b.x + b.w); bottom = Math.max(bottom, b.y + b.h);
  });
  mapInstances.forEach((i) => { i.x -= left; i.y -= top; });
  mapWidthInput.value = Math.ceil(right - left);
  mapHeightInput.value = Math.ceil(bottom - top);
  applyMapCanvasSize();
  renderMapCanvas();
  mapView.frameToFit();
  setMapStatus('Lienzo ajustado al límite exterior del borde.');
};

window.addEventListener('resize', () => { if (!mapTabPanel.hidden) mapView.frameToFit(); });

// ---- export ----

mapExportPngBtn.onclick = async () => {
  if (!mapInstances.length) { setMapStatus('Coloca al menos una pieza.'); return; }
  const blob = await MapExport.composeMapToBlob(mapCanvas.width, mapCanvas.height, mapAssets, mapInstances);
  downloadBlob(blob, 'map.png');
};

mapExportJsonBtn.onclick = () => {
  if (!mapInstances.length) { setMapStatus('Coloca al menos una pieza.'); return; }
  const json = MapExport.buildMapJson(mapCanvas.width, mapCanvas.height, mapAssets, mapInstances);
  downloadBlob(new Blob([JSON.stringify(json, null, 2)], { type: 'application/json' }), 'map.json');
  setMapStatus('Map JSON exportado.');
};

// ---- Map Tracer library (maps + pieces) ----

mapSaveMapBtn.onclick = async () => {
  if (!mapInstances.length) { setMapStatus('Coloca al menos una pieza antes de guardar el mapa.'); return; }
  const name = mapNameInput.value.trim() || 'Map';
  const composed = MapExport.composeMap(mapCanvas.width, mapCanvas.height, mapAssets, mapInstances);
  const thumbnail = MapLibrary.buildThumbnail(composed);
  const record = await MapLibrary.saveMap({
    id: currentMapLibraryId,
    name, canvasWidth: mapCanvas.width, canvasHeight: mapCanvas.height,
    assets: mapAssets, instances: mapInstances, thumbnail,
  });
  currentMapLibraryId = record.id;
  renderMapLibraryLists();
  setMapStatus(`Mapa "${name}" guardado en la biblioteca.`);
};

async function loadMapLibraryEntry(entry) {
  const { assets, instances } = await MapLibrary.loadMapAsSession(entry);
  mapAssets = assets;
  mapInstances = instances;
  mapWidthInput.value = entry.canvasWidth;
  mapHeightInput.value = entry.canvasHeight;
  currentMapLibraryId = entry.id;
  mapNameInput.value = entry.name;
  selectedAssetIds.clear(); selectedInstanceIds.clear();
  if (mapMode === 'trace') exitTraceMode(); else applyMapCanvasSize();
  renderMapAssetsList(); renderMapInstancesList(); renderMapCanvas();
  mapView.frameToFit();
  setMapStatus(`Mapa "${entry.name}" cargado.`);
}

async function exportMapLibraryEntryPng(entry) {
  const { assets, instances } = await MapLibrary.loadMapAsSession(entry);
  const blob = await MapExport.composeMapToBlob(entry.canvasWidth, entry.canvasHeight, assets, instances);
  downloadBlob(blob, `${entry.name}.png`);
}

async function exportMapLibraryEntryJson(entry) {
  const { assets, instances } = await MapLibrary.loadMapAsSession(entry);
  const json = MapExport.buildMapJson(entry.canvasWidth, entry.canvasHeight, assets, instances);
  downloadBlob(new Blob([JSON.stringify(json, null, 2)], { type: 'application/json' }), `${entry.name}.json`);
}

async function deleteMapLibraryEntry(entry) {
  if (!confirm(`¿Eliminar el mapa "${entry.name}"? Esto no se puede deshacer.`)) return;
  await MapLibrary.deleteMap(entry.id);
  if (currentMapLibraryId === entry.id) currentMapLibraryId = null;
  renderMapLibraryLists();
}

/** Adds a saved piece into the CURRENT session's in-memory piece library — does NOT open a map. */
async function loadPieceLibraryEntry(entry) {
  const asset = await MapLibrary.loadPieceAsAsset(entry);
  mapAssets.push(asset);
  selectedAssetIds.clear();
  selectedAssetIds.add(asset.id);
  renderMapAssetsList();
  setMapStatus(`Pieza "${entry.name}" añadida a la biblioteca de piezas de la sesión.`);
}

async function deletePieceLibraryEntry(entry) {
  if (!confirm(`¿Eliminar la pieza "${entry.name}"? Esto no se puede deshacer.`)) return;
  await MapLibrary.deletePiece(entry.id);
  selectedPieceLibraryIds.delete(entry.id);
  renderMapLibraryLists();
}

function exportPieceLibraryEntry(entry) {
  const json = MapExport.buildPiecesJson([entry]);
  downloadBlob(new Blob([JSON.stringify(json, null, 2)], { type: 'application/json' }), `${entry.name}.pieces.json`);
  setMapStatus(`Pieza "${entry.name}" exportada.`);
}

function updatePieceExportButton() {
  mapExportSelectedPiecesBtn.disabled = selectedPieceLibraryIds.size === 0;
}

/** Same ctrl-toggle/shift-range/plain-replace rule as app.js's applySelectionClick (Sprites tab),
 * applied to persisted piece-library record ids instead of in-session box indices. */
function applyPieceLibrarySelectionClick(entry, index, ctrlKey, shiftKey, orderedEntries) {
  if (shiftKey && lastClickedPieceLibraryIndex >= 0) {
    const [a, b] = [lastClickedPieceLibraryIndex, index].sort((x, y) => x - y);
    for (let k = a; k <= b; k++) selectedPieceLibraryIds.add(orderedEntries[k].id);
  } else if (ctrlKey) {
    if (selectedPieceLibraryIds.has(entry.id)) selectedPieceLibraryIds.delete(entry.id);
    else selectedPieceLibraryIds.add(entry.id);
    lastClickedPieceLibraryIndex = index;
  } else {
    selectedPieceLibraryIds.clear();
    selectedPieceLibraryIds.add(entry.id);
    lastClickedPieceLibraryIndex = index;
  }
  renderMapLibraryLists();
}

mapExportSelectedPiecesBtn.onclick = async () => {
  if (selectedPieceLibraryIds.size === 0) return;
  const all = await MapLibrary.listPieces();
  const selected = all.filter((p) => selectedPieceLibraryIds.has(p.id));
  const json = MapExport.buildPiecesJson(selected);
  downloadBlob(new Blob([JSON.stringify(json, null, 2)], { type: 'application/json' }), 'map-pieces.json');
  setMapStatus(`${selected.length} pieza(s) exportadas.`);
};

function renderMapLibraryLists() {
  renderLibraryCards(document.getElementById('mapsLibraryList'), {
    fetchEntries: MapLibrary.listMaps,
    getTitle: (e) => e.name,
    getBadges: (e) => [`${e.instances.length} instancias`, `${e.canvasWidth}×${e.canvasHeight}`],
    emptyMessage: 'Todavía no hay mapas guardados.',
    getActions: () => [
      { label: 'Cargar', className: 'small', onClick: loadMapLibraryEntry },
      { label: 'Exportar PNG', className: 'small', onClick: exportMapLibraryEntryPng },
      { label: 'Exportar JSON', className: 'small', onClick: exportMapLibraryEntryJson },
      { label: 'Eliminar', className: 'small danger', onClick: deleteMapLibraryEntry },
    ],
  });
  renderLibraryCards(document.getElementById('piecesLibraryList'), {
    fetchEntries: MapLibrary.listPieces,
    getTitle: (e) => e.name,
    getBadges: (e) => [MapAssets.TYPE_LABEL[e.type] || e.type],
    emptyMessage: 'Todavía no hay piezas guardadas.',
    isSelected: (e) => selectedPieceLibraryIds.has(e.id),
    onCardClick: applyPieceLibrarySelectionClick,
    getActions: () => [
      { label: 'Cargar', className: 'small', onClick: loadPieceLibraryEntry },
      { label: '⬇ Exportar', className: 'small', onClick: exportPieceLibraryEntry },
      { label: 'Eliminar', className: 'small danger', onClick: deletePieceLibraryEntry },
    ],
  });
  updatePieceExportButton();
}

renderMapAssetsList();
renderMapInstancesList();
