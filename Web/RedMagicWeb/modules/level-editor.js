// modules/level-editor.js
// -----------------------------------------------------------------------------
// 🧱 Niveles: editor de cuadrícula de niveles. Pinta roca y plataformas y guarda *.levelsketch.json, que Unity importa
// con el menú EditorNiveles ▸ Importar nivel… (formato: docs/architecture/levels.md ▸ EditorNiveles).
// - Con el servidor del taller (start.cmd): guarda/abre en Assets/Levels/Bocetos y la vista previa lee el arte del
//   bioma de Assets/Levels (tools/taller-server/niveles.mjs). Sin él: descarga/abre archivos y vista previa sin arte.
// - Valores del bioma y tiles: assets/niveles/datos-unity.json, lo escribe Unity (EditorNivelesWebData).
// Celdas con la fila 0 ARRIBA; Unity pone el origen abajo a la izquierda (lo convierte el importador).
import * as Terrain from './level-terrain.js';
import * as Bg from './parallax-data.js';

const MIN = 12, MAX_W = 400, MAX_H = 75, PX = 40, VIEW_H = 20;
const STORE = 'redmagic.levelEditor.v1', FREE_CHARS = '=-~^+*%&@$!?abcdefghijklmnopqrstuvwxyz';
const PLAT_COLORS = ['#f2b84d', '#e07a5f', '#9bd36a', '#c58df0', '#5fd0c4', '#f07fb0'];
const FALLBACK = {
  tiles: [{ name: 'Roca', layer: 'terrain' }, { name: 'Plataforma', layer: 'platforms' }],
  biomes: [{
    name: 'Hielo', rockTexture: null, rockTileUnits: 5.4, cornerRadius: 0.19, floorWidth: 0.625, wallWidth: 0.4,
    ceilingWidth: 0.35, widthVariation: 0.5, outlineWidth: 0.11, innerGlowWidth: 1.6,
    rockShadow: { r: .016, g: .055, b: .133, a: .45 }, outline: { r: .016, g: .067, b: .165, a: 1 }, innerGlow: { r: .31, g: .76, b: 1, a: .28 },
    floorBands: [[63, 149, 208], [134, 205, 242], [205, 238, 252], [255, 255, 255]].map(([r, g, b]) => ({ r: r / 255, g: g / 255, b: b / 255, a: 1 })),
    ceilingBands: [[40, 104, 170], [84, 160, 218], [150, 210, 244], [210, 240, 255]].map(([r, g, b]) => ({ r: r / 255, g: g / 255, b: b / 255, a: 1 })),
    bandFractions: [1, .68, .4, .17], layers: [],
  }],
};

/**
 * @param {{ toast: (msg: string, kind?: string) => void, setDirty: (dirty: boolean, what?: string) => void }} shell
 * @returns {{ onShow: () => void, save: () => Promise<void> }}
 */
export function initLevelEditor({ toast, setDirty }) {
  const $ = (id) => document.getElementById(id);
  const panel = $('tab-levels'), cv = $('lvCanvas'), g = cv.getContext('2d');
  let DATA = FALLBACK, BIOMES = FALLBACK.biomes, dataLoaded = false;
  let server = null; // { token } cuando la página viene de tools/taller-server

  // ---------------------------------------------------------------- paleta (de los tiles de Unity)
  const palette = [{ name: '(aire)', layer: null, char: '.', color: null }];
  let ROCK = 1;
  function addTile(name, layer, ch) {
    const plats = palette.filter((p) => p.layer === 'platforms').length;
    const used = new Set(palette.map((p) => p.char));
    const char = ch && !used.has(ch) ? ch : name === 'Roca' && !used.has('#') ? '#' : name === 'Plataforma' && !used.has('=') ? '=' : [...FREE_CHARS].find((c) => !used.has(c));
    palette.push({ name, layer, char, color: layer === 'terrain' ? '#5a9ed9' : PLAT_COLORS[plats % PLAT_COLORS.length] });
    return palette.length - 1;
  }
  function buildPalette() {
    palette.length = 1;
    DATA.tiles.forEach((t) => addTile(t.name, t.layer));
    ROCK = palette.findIndex((p) => p.layer === 'terrain');
  }
  buildPalette();

  // ---------------------------------------------------------------- estado
  const S = { name: 'nivel', biome: 'Hielo', background: '', w: 85, h: 25, cells: null, mode: 'edit', tool: 'brush', mat: ROCK, brush: 1 };
  const idx = (c, r) => r * S.w + c;
  const inside = (c, r) => c >= 0 && r >= 0 && c < S.w && r < S.h;
  const isFrame = (c, r) => c === 0 || r === 0 || c === S.w - 1 || r === S.h - 1;
  const cell = (c, r) => (inside(c, r) ? S.cells[idx(c, r)] : ROCK);
  const solid = (c, r) => cell(c, r) !== 0;
  const groundTop = (x) => { x = Math.max(1, Math.min(S.w - 2, x)); let y = 1; while (y < S.h - 1 && solid(x, S.h - 1 - y)) y++; return y; }; // y de Unity
  function frame() { for (let r = 0; r < S.h; r++) for (let c = 0; c < S.w; c++) if (isFrame(c, r)) S.cells[idx(c, r)] = ROCK; }
  function blank(w, h) { S.w = w; S.h = h; S.cells = new Uint8Array(w * h); frame(); }
  const biome = () => BIOMES.find((b) => b.name === S.biome) || BIOMES[0];

  // ---------------------------------------------------------------- deshacer
  const undo = [], redo = [];
  const snap = () => ({ w: S.w, h: S.h, cells: S.cells.slice() });
  const restore = (s) => { S.w = s.w; S.h = s.h; S.cells = s.cells; syncInputs(); changed(); };
  function pushUndo() { undo.push(snap()); if (undo.length > 200) undo.shift(); redo.length = 0; }
  function doUndo() { if (undo.length) { redo.push(snap()); restore(undo.pop()); } }
  function doRedo() { if (redo.length) { undo.push(snap()); restore(redo.pop()); } }

  // ---------------------------------------------------------------- tamaño (pegado abajo a la izquierda)
  function resize(w, h) {
    w = Math.max(MIN, Math.min(MAX_W, w | 0)); h = Math.max(MIN, Math.min(MAX_H, h | 0));
    if (w === S.w && h === S.h) return;
    pushUndo();
    const old = S.cells, ow = S.w, oh = S.h, out = new Uint8Array(w * h);
    for (let y = 1; y < Math.min(oh, h) - 1; y++) for (let x = 1; x < Math.min(ow, w) - 1; x++) // y de Unity
      out[(h - 1 - y) * w + x] = old[(oh - 1 - y) * ow + x];
    S.w = w; S.h = h; S.cells = out; frame(); syncInputs(); changed(); fit();
  }

  // ---------------------------------------------------------------- pintar
  function stamp(c, r, v) {
    const n = S.brush, o = Math.floor((n - 1) / 2);
    for (let y = r - o; y < r - o + n; y++) for (let x = c - o; x < c - o + n; x++)
      if (inside(x, y) && !isFrame(x, y)) S.cells[idx(x, y)] = v;
  }
  function line(c0, r0, c1, r1, v) { // Bresenham: sin huecos al mover rápido
    const dx = Math.abs(c1 - c0), dy = -Math.abs(r1 - r0), sx = c0 < c1 ? 1 : -1, sy = r0 < r1 ? 1 : -1; let e = dx + dy;
    for (;;) { stamp(c0, r0, v); if (c0 === c1 && r0 === r1) break; const e2 = 2 * e; if (e2 >= dy) { e += dy; c0 += sx; } if (e2 <= dx) { e += dx; r0 += sy; } }
  }
  function rect(a, b, v) {
    for (let y = Math.min(a.r, b.r); y <= Math.max(a.r, b.r); y++) for (let x = Math.min(a.c, b.c); x <= Math.max(a.c, b.c); x++)
      if (inside(x, y) && !isFrame(x, y)) S.cells[idx(x, y)] = v;
  }
  function flood(c, r, v) {
    if (!inside(c, r) || isFrame(c, r)) return;
    const from = cell(c, r); if (from === v) return;
    const st = [[c, r]];
    while (st.length) {
      const [x, y] = st.pop();
      if (!inside(x, y) || isFrame(x, y) || S.cells[idx(x, y)] !== from) continue;
      S.cells[idx(x, y)] = v; st.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
    }
  }

  // ---------------------------------------------------------------- vista: (z px por celda, ox/oy = pantalla de la celda 0,0)
  let CW = 0, CH = 0, dpr = 1;
  const V = { z: 16, ox: 20, oy: 20 };
  const cam = { x: 0, y: 0 }; // vista previa: centro de la cámara en unidades (y hacia arriba)
  const aspect = 16 / 9;
  function viewport() { // la pantalla del juego dentro del canvas
    let w = CW, h = CW / aspect; if (h > CH) { h = CH; w = CH * aspect; }
    return { x0: (CW - w) / 2, y0: (CH - h) / 2, w, h, ppu: h / VIEW_H, viewW: VIEW_H * w / h, viewH: VIEW_H };
  }
  function clampCam(vp) {
    const hw = vp.viewW / 2, hh = VIEW_H / 2;
    cam.x = S.w <= vp.viewW ? S.w / 2 : Math.max(hw, Math.min(S.w - hw, cam.x));
    cam.y = S.h <= VIEW_H ? S.h / 2 : Math.max(hh, Math.min(S.h - hh, cam.y));
  }
  function applyCam() { // la vista previa también es una transformación afín de celdas a pantalla
    const vp = viewport(); clampCam(vp);
    V.z = vp.ppu; V.ox = vp.x0 - (cam.x - vp.viewW / 2) * vp.ppu; V.oy = vp.y0 + (cam.y + VIEW_H / 2 - S.h) * vp.ppu;
    return vp;
  }
  const toCell = (sx, sy) => ({ c: Math.floor((sx - V.ox) / V.z), r: Math.floor((sy - V.oy) / V.z) });
  function fit() {
    if (S.mode === 'preview') { cam.x = viewport().viewW / 2; cam.y = groundTop(2) + VIEW_H * 0.3; return redraw(); }
    V.z = Math.max(2, Math.min(64, Math.min((CW - 40) / S.w, (CH - 40) / S.h)));
    V.ox = (CW - S.w * V.z) / 2; V.oy = (CH - S.h * V.z) / 2; redraw();
  }
  function setMode(m) {
    if (m === S.mode) return;
    if (m === 'preview') { // la cámara empieza donde miraba la cuadrícula
      cam.x = (CW / 2 - V.ox) / V.z; cam.y = S.h - (CH / 2 - V.oy) / V.z; S.mode = m; applyCam(); scheduleTerrain(0);
    } else {
      const cx = cam.x, cy = S.h - cam.y; S.mode = m; V.z = Math.max(V.z * 0.5, 6); V.ox = CW / 2 - cx * V.z; V.oy = CH / 2 - cy * V.z;
    }
    $('lvModeEdit').classList.toggle('active', m === 'edit'); $('lvModePreview').classList.toggle('active', m === 'preview'); redraw();
  }

  // ---------------------------------------------------------------- vista previa: terreno e imágenes
  const images = new Map();
  const artUrl = (p) => `/api/niveles/arte?path=${encodeURIComponent(p)}`;
  function img(path) {
    if (!path || !server) return null;
    if (!images.has(path)) {
      const i = new Image();
      i.onload = () => { redraw(); if (path === biome().rockTexture) scheduleTerrain(0); };
      i.onerror = () => { i.failed = true; showWarn(); };
      i.src = artUrl(path); images.set(path, i);
    }
    const i = images.get(path); return i.complete && i.naturalWidth ? i : null;
  }
  let terrain = null, terrainStale = true, terrainTimer = 0;
  function scheduleTerrain(ms = 250) {
    terrainStale = true; clearTimeout(terrainTimer);
    if (S.mode !== 'preview') return;
    terrainTimer = setTimeout(() => {
      if (painting) return scheduleTerrain(ms);
      const b = biome(), ppu = Math.max(8, Math.min(40, Math.floor(Math.sqrt(24e6 / (S.w * S.h)))));
      terrain = { cv: Terrain.build(solid, S.w, S.h, b, img(b.rockTexture), ppu), w: S.w, h: S.h };
      terrainStale = false; redraw();
    }, ms);
  }
  function showWarn() {
    const msg = [];
    if (!dataLoaded) msg.push('Sin datos de Unity (assets/niveles/datos-unity.json): se escribe al importar un nivel o al guardar el bioma.');
    if (!server) msg.push('Sin el servidor del taller (start.cmd) no se guarda en el proyecto ni se ve el arte del bioma.');
    const bad = [...images.values()].filter((i) => i.failed).length;
    if (bad) msg.push(`${bad} imagen(es) del bioma no cargan.`);
    $('lvWarn').textContent = msg.join(' ');
    $('lvWarn').hidden = !msg.length;
  }

  // ---------------------------------------------------------------- dibujo
  let needDraw = true, hover = null, rectStart = null, painting = null, panning = null, spaceDown = false, running = false;
  const redraw = () => { needDraw = true; };
  const accent = () => getComputedStyle(document.documentElement).getPropertyValue('--accent').trim() || '#5fe0a0';
  function resizeCanvas() {
    dpr = window.devicePixelRatio || 1; const r = $('lvStage').getBoundingClientRect();
    if (!r.width || !r.height) return;
    if (CW && S.mode === 'edit') { V.ox += (r.width - CW) / 2; V.oy += (r.height - CH) / 2; }
    const first = !CW;
    CW = r.width; CH = r.height; cv.width = Math.round(CW * dpr); cv.height = Math.round(CH * dpr);
    cv.style.width = CW + 'px'; cv.style.height = CH + 'px';
    if (first) fit(); else redraw();
  }
  function cellsRange() {
    return { c0: Math.max(0, Math.floor(-V.ox / V.z)), c1: Math.min(S.w, Math.ceil((CW - V.ox) / V.z)), r0: Math.max(0, Math.floor(-V.oy / V.z)), r1: Math.min(S.h, Math.ceil((CH - V.oy) / V.z)) };
  }
  function drawCells(alpha) {
    const { c0, c1, r0, r1 } = cellsRange(), z = V.z;
    g.globalAlpha = alpha;
    for (let r = r0; r < r1; r++) for (let c = c0; c < c1; c++) {
      const v = S.cells[idx(c, r)]; if (!v) continue;
      g.fillStyle = isFrame(c, r) ? '#22364d' : palette[v].color;
      g.fillRect(V.ox + c * z, V.oy + r * z, Math.ceil(z), Math.ceil(z));
    }
    g.globalAlpha = 1;
  }
  function drawGrid() {
    const z = V.z; if (z < 6) return;
    const { c0, c1, r0, r1 } = cellsRange();
    g.lineWidth = 1;
    for (let pass = 0; pass < 2; pass++) {
      g.strokeStyle = pass ? 'rgba(255,255,255,.16)' : 'rgba(255,255,255,.05)'; g.beginPath();
      for (let c = c0; c <= c1; c++) if ((c % 5 === 0) === !!pass) { const x = Math.round(V.ox + c * z) + .5; g.moveTo(x, V.oy + r0 * z); g.lineTo(x, V.oy + r1 * z); }
      for (let r = r0; r <= r1; r++) if (((S.h - r) % 5 === 0) === !!pass) { const y = Math.round(V.oy + r * z) + .5; g.moveTo(V.ox + c0 * z, y); g.lineTo(V.ox + c1 * z, y); }
      g.stroke();
    }
  }
  function drawLayers(vp, which) {
    const view = { viewW: vp.viewW, viewH: VIEW_H, ppu: vp.ppu, x0: vp.x0, y0: vp.y0 };
    // un fondo de la Biblioteca (🏞️ Fondos) si se eligió; si no, el del bioma en Unity
    const layers = bgLayers || biome().layers || [];
    for (const L of layers) {
      if (L.on === false) continue;
      const kind = L.inFront ? 'front' : L.overTerrain ? 'over' : 'back';
      if (kind === which) Terrain.drawLayer(g, L, bgLayers ? L.img : img(L.texture), cam, view, { w: S.w, h: S.h }, groundTop);
    }
  }

  // ---------------------------------------------------------------- fondo de la Biblioteca
  let bgLayers = null;
  async function setBackground(name) {
    S.background = name || '';
    bgLayers = null;
    if (S.background) {
      const e = (await Bg.listBackgrounds()).find((x) => x.enemyName === S.background);
      if (e) bgLayers = await Bg.readyLayers(e.draft?.layers || []);
      else toast(`El fondo "${S.background}" no está en la Biblioteca: se usa el del bioma`, 'info');
    }
    $('lvBg').value = bgLayers ? S.background : '';
    redraw();
  }
  async function refreshBackgrounds() {
    const list = await Bg.listBackgrounds();
    $('lvBg').innerHTML = '<option value="">Del bioma (Unity)</option>' + list.map((e) => `<option>${e.enemyName}</option>`).join('');
    $('lvBg').value = bgLayers ? S.background : '';
    if (S.background) setBackground(S.background); // por si se volvió a guardar en 🏞️ Fondos
  }
  function drawPlatformMarks() {
    const z = V.z, { c0, c1, r0, r1 } = cellsRange();
    g.lineWidth = Math.max(2, z * 0.08); g.setLineDash([z * 0.3, z * 0.2]);
    for (let r = r0; r < r1; r++) for (let c = c0; c < c1; c++) {
      const v = S.cells[idx(c, r)]; if (!v || palette[v].layer !== 'platforms' || cell(c, r - 1) === v) continue;
      g.strokeStyle = palette[v].color; g.beginPath(); g.moveTo(V.ox + c * z, V.oy + r * z + 1); g.lineTo(V.ox + (c + 1) * z, V.oy + r * z + 1); g.stroke();
    }
    g.setLineDash([]);
  }
  const hexA = (hex, a) => { const n = parseInt(hex.slice(1), 16); return `rgba(${n >> 16},${n >> 8 & 255},${n & 255},${a})`; };
  function drawCursor() {
    if (!hover || panning) return;
    const z = V.z, { c, r } = hover;
    if (rectStart) {
      const x0 = Math.min(rectStart.c, c), x1 = Math.max(rectStart.c, c), y0 = Math.min(rectStart.r, r), y1 = Math.max(rectStart.r, r);
      g.fillStyle = rectStart.erase ? 'rgba(224,96,90,.25)' : hexA(palette[S.mat].color, .35);
      g.fillRect(V.ox + x0 * z, V.oy + y0 * z, (x1 - x0 + 1) * z, (y1 - y0 + 1) * z);
      g.strokeStyle = '#fff'; g.lineWidth = 1; g.strokeRect(V.ox + x0 * z + .5, V.oy + y0 * z + .5, (x1 - x0 + 1) * z - 1, (y1 - y0 + 1) * z - 1);
      return;
    }
    if (!inside(c, r)) return;
    const n = S.tool === 'fill' ? 1 : S.brush, o = Math.floor((n - 1) / 2);
    g.strokeStyle = S.tool === 'erase' ? '#ff8a80' : '#fff'; g.lineWidth = 1.5;
    g.strokeRect(V.ox + (c - o) * z + .5, V.oy + (r - o) * z + .5, n * z - 1, n * z - 1);
  }
  function draw() {
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    g.fillStyle = '#05080d'; g.fillRect(0, 0, CW, CH);
    if (S.mode === 'edit') {
      g.fillStyle = '#0d1724'; g.fillRect(V.ox, V.oy, S.w * V.z, S.h * V.z);
      drawCells(1); drawGrid();
      g.strokeStyle = accent(); g.lineWidth = 1; g.strokeRect(V.ox - .5, V.oy - .5, S.w * V.z + 1, S.h * V.z + 1);
      drawCursor();
    } else {
      const vp = applyCam();
      g.save(); g.beginPath(); g.rect(vp.x0, vp.y0, vp.w, vp.h); g.clip();
      g.fillStyle = 'rgb(7,20,38)'; g.fillRect(vp.x0, vp.y0, vp.w, vp.h);
      drawLayers(vp, 'back');
      if (terrain && terrain.w === S.w && terrain.h === S.h) g.drawImage(terrain.cv, V.ox, V.oy, S.w * V.z, S.h * V.z);
      if (!terrain || terrainStale) drawCells(terrain ? 0.55 : 1);
      drawLayers(vp, 'over');
      drawPlatformMarks();
      drawLayers(vp, 'front');
      drawCursor();
      g.restore();
    }
    status();
  }
  const keys = {};
  let lastT = performance.now();
  function moveCam() {
    const now = performance.now(), dt = Math.min(0.05, (now - lastT) / 1000); lastT = now;
    const sp = 25 * dt; let mx = 0, my = 0;
    if (keys.a || keys.arrowleft) mx -= sp; if (keys.d || keys.arrowright) mx += sp;
    if (keys.w || keys.arrowup) my += sp; if (keys.s || keys.arrowdown) my -= sp;
    if (mx || my) { cam.x += mx; cam.y += my; redraw(); }
  }
  function loop() { // sólo mientras la pestaña está abierta
    if (panel.hidden) { running = false; return; }
    if (S.mode === 'preview') moveCam();
    if (needDraw) { needDraw = false; draw(); }
    requestAnimationFrame(loop);
  }

  // ---------------------------------------------------------------- estado en pantalla
  function status() {
    const h = hover && inside(hover.c, hover.r) ? hover : null;
    $('lvStPos').textContent = h ? `x ${h.c} · y ${S.h - 1 - h.r} (Unity)${isFrame(h.c, h.r) ? ' · marco' : ''} · ${palette[cell(h.c, h.r)].name}` : '—';
    $('lvStSize').textContent = `${S.w} × ${S.h} celdas = ${S.w * PX} × ${S.h * PX} px`;
    let rock = 0, plat = 0; for (let r = 1; r < S.h - 1; r++) for (let c = 1; c < S.w - 1; c++) { const v = S.cells[idx(c, r)]; if (v) palette[v].layer === 'platforms' ? plat++ : rock++; }
    $('lvStCount').textContent = `${rock} roca · ${plat} plataforma`;
    $('lvStHint').textContent = S.mode === 'preview' && terrainStale ? 'regenerando borde…' : '';
  }

  // ---------------------------------------------------------------- cambios y autoguardado (en el navegador)
  let storeTimer = 0;
  function changed(edit = true) {
    redraw(); scheduleTerrain();
    if (edit) setDirty(true, 'cuadrícula');
    clearTimeout(storeTimer); storeTimer = setTimeout(() => { try { localStorage.setItem(STORE, JSON.stringify(toJson())); } catch { /* sin almacenamiento */ } }, 400);
  }

  // ---------------------------------------------------------------- formato .levelsketch.json
  function toJson() {
    return {
      format: 'redmagic-level', version: 2, name: S.name, biome: S.biome, background: S.background || undefined, width: S.w, height: S.h,
      legend: palette.slice(1).map((p) => ({ char: p.char, layer: p.layer, tile: p.name })),
      rows: Array.from({ length: S.h }, (_, r) => Array.from(S.cells.subarray(r * S.w, (r + 1) * S.w), (v) => palette[v].char).join('')),
    };
  }
  function fromJson(text) {
    const d = JSON.parse(text);
    if (!Array.isArray(d.rows) || !d.width || d.rows.length !== d.height) throw new Error('faltan filas o no cuadran con "height"');
    const map = new Map([['.', 0]]);
    const legend = d.legend?.length ? d.legend : [{ char: '#', layer: 'terrain', tile: 'Roca' }];
    for (const e of legend) {
      let i = palette.findIndex((p, k) => k && p.name === e.tile && p.layer === e.layer);
      if (i < 0) i = e.layer === 'terrain' ? ROCK : addTile(e.tile, e.layer, e.char);
      map.set(e.char, i);
    }
    const w = Math.max(MIN, Math.min(MAX_W, d.width)), h = Math.max(MIN, Math.min(MAX_H, d.height)), skip = d.height - h;
    if (skip > 0) toast(`Alto ${d.height} > ${MAX_H}: se quitan ${skip} filas de arriba`, 'info');
    blank(w, h);
    for (let r = 0; r < h; r++) { const row = d.rows[r + skip] || ''; for (let c = 0; c < w; c++) S.cells[idx(c, r)] = map.get(row[c]) ?? 0; }
    frame();
    S.name = d.name || S.name;
    if (d.biome) { if (BIOMES.some((b) => b.name === d.biome)) S.biome = d.biome; else toast(`Bioma "${d.biome}" no está en los datos de Unity`, 'info'); }
    buildMats(); syncInputs(); changed(false); fit();
    setBackground(d.background || '');
  }

  // ---------------------------------------------------------------- archivo
  const text = () => JSON.stringify(toJson(), null, 2);
  async function call(method, url, body) {
    const r = await fetch(url, { method, headers: { 'Content-Type': 'application/json', ...(server ? { 'X-Taller-Token': server.token } : {}) }, body: body ? JSON.stringify(body) : undefined });
    if (!r.ok) throw new Error(`${r.status}`);
    return r.json();
  }
  /** Ctrl+S y 💾: en el proyecto (Assets/Levels/Bocetos) con el servidor; si no, se descarga el archivo. */
  async function save() {
    let msg;
    if (server) {
      const r = await call('POST', '/api/niveles/guardar', { name: S.name, text: text() });
      if (r.error) throw new Error(r.error);
      msg = `Guardado en ${r.path}`;
      refreshSketches();
    } else {
      const a = document.createElement('a');
      a.href = URL.createObjectURL(new Blob([text()], { type: 'application/json' }));
      a.download = `${S.name.replace(/[\\/:*?"<>|.]+/g, '_')}.levelsketch.json`; a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 1000);
      msg = `Descargado: ${a.download}`;
    }
    setDirty(false);
    setTimeout(() => toast(msg), 0); // después del "Guardado." de Ctrl+S
  }
  async function refreshSketches() {
    const sel = $('lvOpen');
    const keep = '<option value="">Abrir…</option>';
    let list = [];
    if (server) { try { list = await call('GET', '/api/niveles/bocetos'); } catch { list = []; } }
    sel.innerHTML = keep + list.map((s) => `<option value="p:${encodeURIComponent(s.name)}">${s.name}</option>`).join('') + '<option value="file">📁 Desde un archivo…</option>';
  }
  async function load(txt) {
    try { pushUndo(); fromJson(txt); setDirty(false); toast(`Abierto: ${S.name}`); }
    catch (e) { undo.pop(); toast('No es un boceto válido: ' + e.message, 'error'); }
  }
  $('lvOpen').onchange = async (e) => {
    const v = e.target.value; e.target.value = '';
    if (v === 'file') return $('lvFileInput').click();
    if (v.startsWith('p:')) {
      const r = await fetch(`/api/niveles/boceto?name=${v.slice(2)}`);
      if (r.ok) await load(await r.text()); else toast('No se pudo abrir', 'error');
    }
  };
  $('lvFileInput').onchange = async (e) => { const f = e.target.files[0]; if (f) await load(await f.text()); e.target.value = ''; };
  panel.addEventListener('dragover', (e) => e.preventDefault());
  panel.addEventListener('drop', async (e) => { e.preventDefault(); const f = [...e.dataTransfer.files].find((x) => x.name.endsWith('.json')); if (f) await load(await f.text()); });

  // ---------------------------------------------------------------- interfaz
  function buildMats() {
    const box = $('lvMats'); box.querySelectorAll('button').forEach((b) => b.remove());
    palette.forEach((p, i) => {
      if (!i) return;
      const b = document.createElement('button'); b.dataset.mat = i; b.className = 'small';
      b.innerHTML = `<span class="lvSwatch" style="background:${p.color}"></span>${p.name}`;
      b.title = `${p.layer === 'terrain' ? 'Roca maciza (Grid/Terreno)' : 'Plataforma atravesable desde abajo (Grid/Plataformas)'} — tecla ${i}`;
      b.onclick = () => { S.mat = i; if (S.tool === 'erase') S.tool = 'brush'; syncButtons(); };
      box.appendChild(b);
    });
    syncButtons();
  }
  function setTool(t) { S.tool = t; syncButtons(); redraw(); }
  function syncButtons() {
    panel.querySelectorAll('[data-lvtool]').forEach((b) => b.classList.toggle('active', b.dataset.lvtool === S.tool));
    panel.querySelectorAll('[data-mat]').forEach((b) => b.classList.toggle('active', +b.dataset.mat === S.mat && S.tool !== 'erase'));
    $('lvBrush').value = S.brush;
  }
  function syncInputs() {
    $('lvName').value = S.name; $('lvW').value = S.w; $('lvH').value = S.h; $('lvBiome').value = S.biome;
    $('lvWpx').textContent = `${S.w * PX} px`; $('lvHpx').textContent = `${S.h * PX} px`;
  }
  function fillBiomes() { $('lvBiome').innerHTML = BIOMES.map((b) => `<option>${b.name}</option>`).join(''); $('lvBiome').value = S.biome; }
  panel.querySelectorAll('[data-lvtool]').forEach((b) => { b.onclick = () => setTool(b.dataset.lvtool); });
  $('lvBrush').onchange = (e) => { S.brush = +e.target.value; redraw(); };
  $('lvModeEdit').onclick = () => setMode('edit'); $('lvModePreview').onclick = () => setMode('preview');
  $('lvFit').onclick = fit; $('lvUndo').onclick = doUndo; $('lvRedo').onclick = doRedo;
  $('lvName').oninput = (e) => { S.name = e.target.value.trim() || 'nivel'; changed(false); };
  $('lvBiome').onchange = (e) => { S.biome = e.target.value; changed(false); };
  $('lvBg').onchange = (e) => { setBackground(e.target.value); changed(false); };
  $('lvW').oninput = (e) => { $('lvWpx').textContent = `${(+e.target.value || 0) * PX} px`; };
  $('lvH').oninput = (e) => { $('lvHpx').textContent = `${(+e.target.value || 0) * PX} px`; };
  $('lvResize').onclick = () => resize(+$('lvW').value, +$('lvH').value);
  $('lvSave').onclick = () => save().catch((err) => toast(`No se pudo guardar: ${err.message}`, 'error'));
  // "Nuevo" vacía el nivel (se puede deshacer): segundo clic para confirmar, sin diálogo del navegador
  let armed = 0;
  $('lvNew').onclick = () => {
    if (Date.now() - armed > 2500) { armed = Date.now(); toast('Pulsa otra vez "Nuevo" para vaciar el nivel (Ctrl+Z lo recupera)', 'info'); return; }
    pushUndo(); blank(S.w, S.h); changed();
  };

  // ---------------------------------------------------------------- ratón
  const pos = (e) => { const r = cv.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };
  cv.addEventListener('contextmenu', (e) => e.preventDefault());
  cv.addEventListener('mousedown', (e) => {
    const p = pos(e), at = toCell(p.x, p.y);
    if (e.button === 1 || (e.button === 0 && spaceDown)) { panning = { x: p.x, y: p.y, ox: V.ox, oy: V.oy, cx: cam.x, cy: cam.y }; e.preventDefault(); return; }
    if (e.button !== 0 && e.button !== 2) return;
    const erase = e.button === 2 || S.tool === 'erase', v = erase ? 0 : S.mat;
    pushUndo();
    if (S.tool === 'rect') rectStart = { ...at, erase, v };
    else if (S.tool === 'fill') { flood(at.c, at.r, v); changed(); }
    else { painting = { last: at, v }; stamp(at.c, at.r, v); changed(); }
  });
  window.addEventListener('mousemove', (e) => {
    if (panel.hidden) return;
    const p = pos(e);
    if (panning) {
      if (S.mode === 'edit') { V.ox = panning.ox + p.x - panning.x; V.oy = panning.oy + p.y - panning.y; }
      else { cam.x = panning.cx - (p.x - panning.x) / V.z; cam.y = panning.cy + (p.y - panning.y) / V.z; }
      return redraw();
    }
    const at = toCell(p.x, p.y);
    if (!hover || at.c !== hover.c || at.r !== hover.r) { hover = at; redraw(); }
    if (painting) { line(painting.last.c, painting.last.r, at.c, at.r, painting.v); painting.last = at; changed(); }
  });
  window.addEventListener('mouseup', () => {
    if (rectStart && hover) { rect(rectStart, hover, rectStart.v); changed(); }
    rectStart = null; painting = null; panning = null; redraw();
  });
  cv.addEventListener('mouseleave', () => { hover = null; redraw(); });
  cv.addEventListener('wheel', (e) => {
    e.preventDefault();
    if (S.mode === 'preview') { const k = 1 / V.z; if (e.shiftKey) cam.x += e.deltaY * k; else { cam.x += e.deltaX * k; cam.y -= e.deltaY * k; } return redraw(); }
    const p = pos(e), f = Math.exp(-e.deltaY * 0.0015), z = Math.max(2, Math.min(64, V.z * f));
    V.ox = p.x - (p.x - V.ox) * z / V.z; V.oy = p.y - (p.y - V.oy) * z / V.z; V.z = z; redraw();
  }, { passive: false });

  // ---------------------------------------------------------------- teclado (sólo con la pestaña abierta; Ctrl+S lo lleva la web)
  const typing = (t) => t && t.matches && t.matches('input,select,textarea,[contenteditable]');
  window.addEventListener('keydown', (e) => {
    if (panel.hidden || typing(e.target) || e.altKey) return;
    const k = e.key.toLowerCase();
    if (e.ctrlKey || e.metaKey) {
      if (k === 'z' && !e.shiftKey) { doUndo(); e.preventDefault(); }
      else if (k === 'y' || (k === 'z' && e.shiftKey)) { doRedo(); e.preventDefault(); }
      return;
    }
    if (k === ' ') { spaceDown = true; cv.style.cursor = 'grab'; e.preventDefault(); e.stopPropagation(); return; }
    if (S.mode === 'preview' && ['w', 'a', 's', 'd', 'arrowup', 'arrowdown', 'arrowleft', 'arrowright'].includes(k)) { keys[k] = true; lastT = performance.now(); e.preventDefault(); return; }
    if (k === 'b') setTool('brush'); else if (k === 'r') setTool('rect'); else if (k === 'g') setTool('fill'); else if (k === 'e') setTool('erase');
    else if (k === 'p') setMode(S.mode === 'edit' ? 'preview' : 'edit');
    else if (k === 'f') fit();
    else if (k === '[' || k === ']') { const o = [1, 2, 3, 5, 8], i = o.indexOf(S.brush); S.brush = o[Math.max(0, Math.min(o.length - 1, (i < 0 ? 0 : i) + (k === ']' ? 1 : -1)))]; syncButtons(); redraw(); }
    else if (/^[1-9]$/.test(k) && +k < palette.length) { S.mat = +k; if (S.tool === 'erase') S.tool = 'brush'; syncButtons(); }
  }, true);
  window.addEventListener('keyup', (e) => { const k = e.key.toLowerCase(); keys[k] = false; if (k === ' ') { spaceDown = false; cv.style.cursor = ''; } });
  window.addEventListener('blur', () => { for (const k in keys) keys[k] = false; spaceDown = false; });

  // ---------------------------------------------------------------- arranque
  blank(S.w, S.h);
  let restored = false;
  try { const t = localStorage.getItem(STORE); if (t) { fromJson(t); restored = true; } } catch { blank(85, 25); }
  if (!restored) for (let c = 1; c < S.w - 1; c++) S.cells[idx(c, S.h - 2)] = ROCK; // un suelo para empezar
  fillBiomes(); buildMats(); syncInputs();
  new ResizeObserver(resizeCanvas).observe($('lvStage'));
  $('lvModeEdit').classList.add('active');

  (async () => {
    try { const r = await fetch('/api/hello'); if (r.ok) { const h = await r.json(); if (h.token) server = { token: h.token }; } } catch { /* sin servidor */ }
    try {
      const r = await fetch('assets/niveles/datos-unity.json', { cache: 'no-store' });
      if (r.ok) {
        DATA = await r.json(); dataLoaded = true;
        if (!DATA.biomes?.length) DATA.biomes = FALLBACK.biomes;
        if (!DATA.tiles?.length) DATA.tiles = FALLBACK.tiles;
        BIOMES = DATA.biomes;
        const saved = toJson(); buildPalette(); fromJson(JSON.stringify(saved)); // re-mapear con la paleta de Unity
        fillBiomes();
      }
    } catch { /* se queda la paleta de serie */ }
    showWarn(); refreshSketches(); redraw();
  })();

  return {
    /** Al abrir la pestaña: arranca el dibujo y relee la lista de bocetos del proyecto. */
    onShow() {
      resizeCanvas(); redraw(); refreshSketches(); refreshBackgrounds();
      if (!running) { running = true; requestAnimationFrame(loop); }
    },
    save,
  };
}
