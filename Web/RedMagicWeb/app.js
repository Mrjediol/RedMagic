// app.js
// -----------------------------------------------------------------------------
// Tab shell + wiring. Owns every piece of shared state (image, boxes, lanes,
// selection, mode) and calls into the pure modules for the actual algorithms;
// this file's own job is DOM: reading inputs, building the lane/unsorted-grid
// UI, and reacting to canvas mouse events. See ARCHITECTURE.md for exactly
// which function here to edit for a given change.

import { removeBackground } from './modules/background-removal.js';
import { detectSprites, clusterIntoRows } from './modules/sprite-detection.js';
import { CanvasView } from './modules/canvas-view.js';
import * as Lanes from './modules/animation-lanes.js';
import { buildGridBoxes } from './modules/grid-autoslice.js';
import { cropToDataURL, buildExportZip, downloadBlob } from './modules/export-manifest.js';
import { initEnemyCreator } from './modules/enemy-form.js';

// Always pre-created on image load, in this order — "Animaciones automáticas" defaults its
// 5-row mapping to this same order (row i -> DEFAULT_LANE_NAMES[i]) when there are exactly 5 rows.
const DEFAULT_LANE_NAMES = ['Idle', 'Walk', 'Attack', 'Hurt', 'Death'];

// Set once initEnemyCreator() runs below (after `lanes` exists) — the tab-switch handler reads
// this closure variable at CLICK time, not at registration time, so declaring it after is fine.
let enemyCreator = null;

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
    if (btn.dataset.tab === 'enemy' && enemyCreator) enemyCreator.refreshLaneInfo();
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

function currentSource() { return bgRemoved ? workCanvas : img; }

function primarySelected() {
  return selectedIndices.size === 1 ? [...selectedIndices][0] : -1;
}
function clearSelection() {
  selectedIndices.clear();
  lastClickedIndex = -1;
}
function setStatus(msg) { document.getElementById('status').textContent = msg; }

// ============================================================ image loading

document.getElementById('fileInput').addEventListener('change', (e) => {
  const f = e.target.files[0];
  if (f) loadImageFile(f);
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
      Lanes.resetLaneIdCounter();
      DEFAULT_LANE_NAMES.forEach((name) => Lanes.createLane(lanes, 'animation', name));
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

document.getElementById('removeBgBtn').addEventListener('click', () => {
  if (!img) { setStatus('Carga una imagen primero.'); return; }

  const tolerance = parseInt(document.getElementById('tolerance').value, 10);
  const imageData = workCtx.getImageData(0, 0, workCanvas.width, workCanvas.height);
  removeBackground(imageData, tolerance);
  workCtx.putImageData(imageData, 0, 0);

  bgRemoved = true;
  render();
  setStatus('Fondo eliminado. Revisa el resultado; si quedaron restos, sube la tolerancia y repite.');
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
  DEFAULT_LANE_NAMES.forEach((name) => Lanes.createLane(lanes, 'animation', name));

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

document.getElementById('addLaneBtn').addEventListener('click', () => {
  const name = prompt('Nombre de la animación (ej. Idle, Attack, Hurt, Death):');
  if (!name) return;
  Lanes.createLane(lanes, 'animation', name);
  renderLanes(); updateAssignBar();
});

document.getElementById('addProjBtn').addEventListener('click', () => {
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

function renderAutoAssignRows() {
  autoAssignRowsEl.innerHTML = '';
  const animLanes = lanes.filter((l) => l.type === 'animation');
  const useDefaultMapping = pendingAutoAssignRows.length === DEFAULT_LANE_NAMES.length;

  pendingAutoAssignRows.forEach((items, i) => {
    const row = document.createElement('div');
    row.className = 'autoAssignRow';

    const options = ['<option value="">Sin asignar / omitir</option>']
      .concat(animLanes.map((l) => `<option value="${l.id}">${l.name}</option>`))
      .join('');

    row.innerHTML = `
      <span class="autoAssignLabel">Fila ${i + 1} (${items.length} sprite${items.length > 1 ? 's' : ''})</span>
      <select data-row="${i}">${options}</select>
    `;

    if (useDefaultMapping) {
      const preferred = animLanes.find((l) => l.name === DEFAULT_LANE_NAMES[i]);
      if (preferred) row.querySelector('select').value = preferred.id;
    }

    autoAssignRowsEl.appendChild(row);
  });
}

function confirmAutoAssign() {
  if (!pendingAutoAssignRows) return;

  // Reuses the exact same assignment path the manual "Asignar" button uses
  // (Lanes.assignBoxesToLane) — same pull-out-of-old-lane + push-in-order behavior.
  [...autoAssignRowsEl.querySelectorAll('select')].forEach((select) => {
    if (!select.value) return; // "Sin asignar / omitir"
    const items = pendingAutoAssignRows[parseInt(select.dataset.row, 10)];
    Lanes.assignBoxesToLane(lanes, boxes, select.value, items.map((b) => b._boxIndex));
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
    lbl.textContent = 'Animaciones del enemigo';
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
  const enemyName = document.getElementById('enemyName').value.trim() || 'Enemy';
  const hasAnim = lanes.some((l) => l.type === 'animation' && l.frameBoxIndices.length > 0);
  if (!hasAnim) { setStatus('Crea al menos una animación de enemigo y asígnale sprites.'); return; }

  setStatus('Generando .zip...');
  const { blob, manifest } = await buildExportZip({
    enemyName, lanes, boxes, source: currentSource(), JSZip: window.JSZip,
  });
  downloadBlob(blob, `${enemyName}.zip`);
  setStatus(`Exportado ${enemyName}.zip — ${manifest.animations.length} animaciones, ${manifest.projectiles.length} proyectiles. Descomprímelo dentro de Assets/ en Unity.`);
});

// ============================================================ Enemy Creator tab

enemyCreator = initEnemyCreator({
  formRoot: document.getElementById('enemyFormRoot'),
  laneInfoEl: document.getElementById('enemyLaneInfo'),
  previewEl: document.getElementById('enemyJsonPreview'),
  summaryEl: document.getElementById('enemyValidationSummary'),
  exportBtn: document.getElementById('enemyExportBtn'),
  // Display-only cross-reference (see the tab-switch handler above) — never a schema field.
  getLaneNames: () => lanes.filter((l) => l.type === 'animation').map((l) => l.name),
});
