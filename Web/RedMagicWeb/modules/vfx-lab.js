// modules/vfx-lab.js
// -----------------------------------------------------------------------------
// The ✨ VFX Lab tab: pick an effect preset (modules/vfx-presets.js), tweak its parameters with a
// live preview, and save it to the shared library as an ordinary `projectile` / `fx` entry — a baked
// frame sheet with the lanes those kinds already use. From there it is picked like any other art
// (an enemy's projectile, a catalog attack's art slots, a boss's warnings) and travels to Unity
// inside that enemy's or boss's export; nothing on the Unity side is specific to presets.
//
// The record keeps what it was baked from in `draft.vfxPreset = { id, params }`, so it can be
// reopened here, retuned and baked again over the same entry (same libraryId: whoever uses it gets
// the new look on its next export). "Guardar como nuevo" makes a variant instead.

import * as F from './enemy-form-fields.js';
//
// With dozens of presets the picker is a GALLERY grouped by family (vfx-presets.js ▸ FAMILIES), each
// card a still of the effect. The preview can be judged over three backgrounds (dark, ice, forest —
// the scenes the game has) and "Ver todos los frames" lays out every frame of every lane on all
// three at once: the fastest way to spot a loop that jumps or a frame that clips.

import { PRESETS, FAMILIES, presetById, paramsOf, defaultParams, normalizeParams, bakePreset, drawFrame, frameCount, laneFps } from './vfx-presets.js';
import { saveEnemy, updateEnemy, getEnemy, listEntries, buildThumbnail } from './enemy-library.js';
import { cropBox } from './export-manifest.js';
import { kindOf } from './entry-kinds.js';

const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};

const createCanvas = (w, h) => {
  const c = document.createElement('canvas');
  c.width = w; c.height = h;
  return c;
};

/** The scenes an effect has to read on: the preview's backgrounds. */
const BACKDROPS = [
  { id: 'dark', label: 'Oscuro', color: '#0d0f12' },
  { id: 'ice', label: 'Hielo', color: '#a9d3e6' },
  { id: 'forest', label: 'Bosque', color: '#4d7a45' },
];

const THUMB = 56;
const thumbs = new Map();        // preset id → data URL of its gallery still

/** A still of a preset at its defaults: the middle of its sustained lane (or of its only one-shot). */
function thumbOf(preset) {
  if (!thumbs.has(preset.id)) {
    const lane = preset.lanes.find((l) => l.loop) || preset.lanes[0];
    const canvas = createCanvas(THUMB, THUMB);
    drawFrame(canvas.getContext('2d'), preset, lane, lane.loop ? 0.3 : 0.32, { ...defaultParams(preset), pixel: 1 }, 0, 0, THUMB, createCanvas);
    thumbs.set(preset.id, canvas.toDataURL());
  }
  return thumbs.get(preset.id);
}

export function initVfxLab({ formRoot, previewRoot, listRoot, statusEl, onSaved }) {
  let backdrop = BACKDROPS[0];
  let showAllFrames = false;
  let preset = PRESETS[0];
  let params = defaultParams(preset);
  let name = '';
  let currentId = null;          // library record this preset was saved as / opened from
  let saved = [];                // library entries made here

  const setStatus = (text) => { statusEl.textContent = text; };

  // ============================================================ form

  function renderForm() {
    formRoot.innerHTML = '';

    const pick = el('div', 'efSection');
    pick.appendChild(el('h2', null, 'Efecto'));
    const choose = (next) => {
      // Another preset keeps the look (colours, glow…) and takes its own extras and defaults.
      const keep = currentId ? {} : params;
      preset = next;
      params = normalizeParams(next, { ...defaultParams(next), ...pickCommon(keep, next) });
      if (currentId) { currentId = null; setStatus('Otro tipo de efecto: se guardará como una entrada nueva.'); }
      renderForm(); renderPreview(); renderList();
    };
    const gallery = el('div', 'vfxGallery');
    FAMILIES.forEach((family) => {
      const members = PRESETS.filter((p) => p.family === family.id);
      if (!members.length) return;
      const head = el('div', 'vfxFamily', `${family.label} (${members.length})`);
      head.title = family.hint;
      gallery.appendChild(head);
      const grid = el('div', 'vfxCards');
      members.forEach((p) => {
        const card = el('button', `vfxCard${p.id === preset.id ? ' selected' : ''}`);
        card.type = 'button';
        card.dataset.preset = p.id;
        card.title = p.description;
        const img = el('img');
        img.alt = '';
        img.src = thumbOf(p);
        card.append(img, el('span', null, p.label));
        card.onclick = () => choose(p);
        grid.appendChild(card);
      });
      gallery.appendChild(grid);
    });
    pick.appendChild(gallery);
    pick.appendChild(el('p', 'vfxChosen', preset.label));
    pick.appendChild(el('p', 'efHint', preset.description));
    pick.appendChild(el('p', 'efHint', `Se guarda como ${kindOf(preset.kind).label} con las animaciones ${preset.lanes.map((l) => l.name).join(' / ')}: `
      + (preset.kind === 'projectile'
        ? 'elígelo como proyectil de un enemigo o en el hueco de proyectil de un ataque.'
        : 'elígelo en los huecos de efecto / aviso de un ataque.')));
    const nameField = F.textField({
      label: 'Nombre en la biblioteca', value: name, placeholder: `ej. ${preset.label.replace(/[^A-Za-z]/g, '')}Azul`,
      hint: 'Sólo letras, números y _. Es el nombre con el que aparece en los selectores de arte.',
      onChange: (v) => { name = v; updateButtons(); },
    });
    pick.appendChild(nameField.row);
    formRoot.appendChild(pick);

    const tune = el('div', 'efSection');
    tune.appendChild(el('h2', null, 'Parámetros'));
    const grid = el('div', 'bcGrid');
    paramsOf(preset).forEach((d) => {
      const set = (v) => { params = normalizeParams(preset, { ...params, [d.key]: v }); renderPreview(); };
      if (d.type === 'color') {
        // The shared colour widget speaks #rrggbbaa; a preset colour has no alpha of its own.
        grid.appendChild(F.colorField({ label: d.label, value: `${params[d.key]}ff`, hint: d.hint, onChange: (v) => set(v.slice(0, 7)) }).row);
      } else if (d.type === 'enum') {
        grid.appendChild(F.enumField({ label: d.label, value: params[d.key], hint: d.hint,
          options: d.options.map(([value, label]) => ({ value, label })), onChange: set }).row);
      } else {
        grid.appendChild(F.numberField({ label: d.label, value: params[d.key], min: d.min, max: d.max, step: String(d.step), hint: d.hint, onChange: set }).row);
      }
    });
    tune.appendChild(grid);
    const reset = el('button', 'small', 'Valores por defecto');
    reset.onclick = () => { params = defaultParams(preset); renderForm(); renderPreview(); };
    tune.appendChild(reset);
    formRoot.appendChild(tune);

    const actions = el('div', 'efSection');
    saveBtn = el('button', 'primary', '');
    saveBtn.style.width = '100%';
    saveBtn.onclick = () => save(false);
    variantBtn = el('button', null, '⧉ Guardar como nuevo (variante)');
    variantBtn.style.width = '100%';
    variantBtn.title = 'Otra entrada de la biblioteca con estos parámetros: la original no cambia.';
    variantBtn.onclick = () => save(true);
    const fresh = el('button', 'small', '＋ Nuevo efecto');
    fresh.onclick = () => { currentId = null; name = ''; params = defaultParams(preset); renderForm(); renderPreview(); renderList(); setStatus('Efecto nuevo (sin guardar).'); };
    actions.append(saveBtn, variantBtn, fresh);
    formRoot.appendChild(actions);
    updateButtons();
  }

  /** The common parameters of `values` (look, not preset-specific extras), minus what `next` overrides. */
  function pickCommon(values, next) {
    const out = {};
    const own = new Set(Object.keys(next.defaults || {}));
    ['size', 'intensity', 'speed', 'length', 'spark', 'pixel', 'seed', 'core', 'glow', 'accent', 'particles'].forEach((k) => {
      if (k in values && !own.has(k)) out[k] = values[k];
    });
    return out;
  }

  let saveBtn = null, variantBtn = null;
  const validName = () => /^[A-Za-z][A-Za-z0-9_]*$/.test(name.trim());
  function updateButtons() {
    if (!saveBtn) return;
    saveBtn.textContent = currentId ? '💾 Guardar cambios (misma entrada)' : '💾 Guardar en biblioteca';
    saveBtn.disabled = !validName();
    variantBtn.disabled = !validName() || !currentId;
    variantBtn.hidden = !currentId;
  }

  // ============================================================ preview

  let players = [];              // { canvas, lane, frame, acc }
  let last = 0;
  let running = false;

  function renderPreview() {
    previewRoot.innerHTML = '';
    players = [];
    const size = params.size;

    // The scene behind the effect + the contact sheet switch.
    const bar = el('div', 'vfxBar');
    bar.appendChild(el('span', 'efHint', 'Fondo:'));
    BACKDROPS.forEach((b) => {
      const btn = el('button', `small${b.id === backdrop.id ? ' selected' : ''}`, b.label);
      btn.dataset.backdrop = b.id;
      btn.onclick = () => { backdrop = b; renderPreview(); };
      bar.appendChild(btn);
    });
    const all = el('button', `small${showAllFrames ? ' selected' : ''}`, showAllFrames ? '▣ Ver todos los frames' : '▢ Ver todos los frames');
    all.dataset.action = 'allFrames';
    all.title = 'Todos los frames de cada animación, sobre los tres fondos: para ver si un bucle salta o algo se corta.';
    all.onclick = () => { showAllFrames = !showAllFrames; renderPreview(); };
    bar.appendChild(all);
    previewRoot.appendChild(bar);

    const row = el('div', 'vfxLanes');
    preset.lanes.forEach((lane) => {
      const box = el('div', 'vfxLane');
      const canvas = createCanvas(size, size);
      canvas.className = 'vfxCanvas';
      canvas.style.background = backdrop.color;
      box.appendChild(canvas);
      box.appendChild(el('span', 'efHint', `${lane.name} · ${frameCount(lane, params)} frames · ${laneFps(lane, params)} fps${lane.loop ? ' · bucle' : ' · una vez'}`));
      row.appendChild(box);
      players.push({ canvas, lane, frame: 0, acc: 0 });
    });
    previewRoot.appendChild(row);

    const baked = bakePreset(preset, params, createCanvas);
    if (showAllFrames) previewRoot.appendChild(contactSheet(baked));
    baked.canvas.className = 'vfxSheet';
    baked.canvas.style.background = backdrop.color;
    previewRoot.appendChild(el('p', 'efHint', `Hoja que se guarda: ${baked.width}×${baked.height} px, ${baked.boxes.length} frames.`));
    previewRoot.appendChild(baked.canvas);

    players.forEach(paint);
    if (!running) { running = true; last = 0; requestAnimationFrame(tick); }
  }

  /**
   * "Ver todos los frames": every frame of every lane of the baked sheet, in order, once per backdrop.
   * A looping lane repeats its first frame at the end (dimmed) so the seam of the loop is on screen.
   */
  function contactSheet(baked) {
    const CELL = 64;
    const wrap = el('div', 'vfxContact');
    baked.lanes.forEach((lane) => {
      const frames = lane.frameBoxIndices.map((i) => baked.boxes[i]);
      const shown = lane.loop ? [...frames, frames[0]] : frames;
      wrap.appendChild(el('span', 'efHint', `${lane.name} · ${frames.length} frames${lane.loop ? ' · el último (atenuado) repite el primero: el bucle debe cerrar' : ''}`));
      BACKDROPS.forEach((b) => {
        const strip = createCanvas(shown.length * CELL, CELL);
        strip.className = 'vfxStrip';
        strip.style.background = b.color;
        const ctx = strip.getContext('2d');
        shown.forEach((box, i) => {
          ctx.globalAlpha = lane.loop && i === shown.length - 1 ? 0.45 : 1;
          ctx.drawImage(baked.canvas, box.x, box.y, box.w, box.h, i * CELL, 0, CELL, CELL);
        });
        wrap.appendChild(strip);
      });
    });
    return wrap;
  }

  function paint(player) {
    const n = frameCount(player.lane, params);
    const i = player.frame % n;
    const k = player.lane.loop ? i / n : (i + 0.5) / n;
    drawFrame(player.canvas.getContext('2d'), preset, player.lane, k, params, 0, 0, params.size, createCanvas);
  }

  function tick(now) {
    // Stops by itself when the tab is hidden; onShow() starts it again.
    if (!previewRoot.isConnected || previewRoot.closest('.tabPanel')?.hidden) { running = false; return; }
    const dt = last ? Math.min(0.25, (now - last) / 1000) : 0;
    last = now;
    players.forEach((player) => {
      player.acc += dt * laneFps(player.lane, params);
      if (player.acc < 1) return;
      const steps = Math.floor(player.acc);
      player.acc -= steps;
      // A one-shot rests a few beats on nothing before replaying, so its end reads as an end.
      const n = frameCount(player.lane, params) + (player.lane.loop ? 0 : 4);
      player.frame = (player.frame + steps) % n;
      if (player.frame < frameCount(player.lane, params)) paint(player);
      else player.canvas.getContext('2d').clearRect(0, 0, params.size, params.size);
    });
    requestAnimationFrame(tick);
  }

  // ============================================================ library

  async function save(asNew) {
    if (!validName()) { setStatus('Ponle un nombre: letras, números y _, empezando por letra.'); return; }
    const baked = bakePreset(preset, params, createCanvas);
    const first = baked.boxes[baked.lanes[0].frameBoxIndices[Math.floor(baked.lanes[0].frameBoxIndices.length / 2)]];
    const patch = {
      kind: preset.kind,
      enemyName: name.trim(),
      // density: how many sheet pixels make one "size" pixel (the Nitidez parameter) — see rig-fx.js.
      sprite: { lanes: baked.lanes, boxes: baked.boxes, sourceDataURL: baked.canvas.toDataURL(), width: baked.width, height: baked.height, density: baked.density },
      thumbnail: buildThumbnail(cropBox(baked.canvas, first)),
      draft: { vfxPreset: { id: preset.id, params: { ...params } } },
    };
    try {
      const existing = !asNew && currentId ? await getEnemy(currentId) : null;
      const record = existing ? await updateEnemy(currentId, patch) : await saveEnemy(patch);
      currentId = record.id;
      setStatus(`"${record.enemyName}" guardado como ${kindOf(record.kind).label} (${new Date(record.updatedAt).toLocaleTimeString()}). `
        + 'Ya se puede elegir en Enemy Creator, Boss Creator y Proyectil/VFX.');
      await refresh();
      if (onSaved) onSaved(record);
    } catch (e) {
      setStatus(`No se pudo guardar: ${e.message}`);
    }
  }

  function open(entry) {
    const stored = entry.draft?.vfxPreset;
    const next = presetById(stored?.id);
    if (!next) { setStatus('Esa entrada no se hizo con un preset de esta pestaña.'); return; }
    preset = next;
    params = normalizeParams(next, stored.params);
    name = entry.enemyName;
    currentId = entry.id;
    renderForm(); renderPreview(); renderList();
    setStatus(`Abierto "${entry.enemyName}": al guardar se rehace la misma entrada (y quien la use la recibe en su próxima exportación).`);
  }

  function renderList() {
    listRoot.innerHTML = '';
    listRoot.appendChild(el('h2', null, `Efectos guardados (${saved.length})`));
    if (saved.length === 0) {
      listRoot.appendChild(el('p', 'efHint', 'Ninguno todavía. Los que guardes aquí aparecen también en la Biblioteca y en todos los selectores de arte.'));
      return;
    }
    saved.forEach((entry) => {
      const row = el('div', `vfxSaved${entry.id === currentId ? ' selected' : ''}`);
      const thumb = el('div', 'bcArtThumb');
      if (entry.thumbnail) thumb.style.backgroundImage = `url(${entry.thumbnail})`;
      row.appendChild(thumb);
      const p = presetById(entry.draft.vfxPreset.id);
      row.appendChild(el('span', 'vfxSavedName', `${entry.enemyName} — ${p ? p.label : entry.draft.vfxPreset.id} · ${kindOf(entry.kind).label}`));
      const openBtn = el('button', 'small', 'Abrir');
      openBtn.onclick = () => open(entry);
      row.appendChild(openBtn);
      listRoot.appendChild(row);
    });
  }

  async function refresh() {
    saved = (await listEntries(null)).filter((e) => e.draft?.vfxPreset && e.sprite);
    if (currentId && !saved.some((e) => e.id === currentId)) currentId = null;
    renderList();
    updateButtons();
  }

  renderForm();
  renderPreview();
  refresh();
  setStatus('Elige un efecto, ajústalo y guárdalo: queda en la biblioteca como un arte más.');

  return {
    /** Called when the tab is shown: the library may have changed, and the preview loop restarts. */
    onShow: async () => { await refresh(); renderPreview(); },
    /** Opens a library entry made here (Biblioteca ▸ "Editar en VFX Lab"). */
    linkLibraryEntry: async (id) => { const entry = await getEnemy(id); if (entry) open(entry); },
  };
}
