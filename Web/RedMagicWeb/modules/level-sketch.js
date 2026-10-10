// modules/level-sketch.js
// -----------------------------------------------------------------------------
// "Boceto de nivel": grid canvas where the user paints which cells are terrain. Self-contained
// (own canvas, own localStorage slot, own JSON export) — it does not touch the other tabs.
// 1 cell = 1 Unity world unit, so every level shares ONE scale; the player silhouette (height in
// cells) shows the proportion while sketching. Export format:
//   { version, name, width, height, cellSize, playerCells, rows: ["..##", ...] }  (row 0 = top)
const STORE_KEY = 'redmagic.levelSketch.v1';

export function initLevelSketch() {
  const $ = (id) => document.getElementById(id);
  const canvas = $('lsCanvas'), ctx = canvas.getContext('2d');
  const state = { name: 'nivel', w: 60, h: 30, player: 2, px: 20, cells: null };
  let hover = null, painting = null; // painting: 1 paint | 0 erase | null idle

  const alloc = (w, h, old) => {
    const c = Array.from({ length: h }, () => new Uint8Array(w));
    if (old) for (let y = 0; y < Math.min(h, old.length); y++) c[y].set(old[y].subarray(0, Math.min(w, old[y].length)));
    return c;
  };
  state.cells = alloc(state.w, state.h);

  const toJson = () => ({
    version: 1, name: state.name, width: state.w, height: state.h, cellSize: 1, playerCells: state.player,
    rows: state.cells.map(r => Array.from(r, v => (v ? '#' : '.')).join('')),
  });
  const save = () => { try { localStorage.setItem(STORE_KEY, JSON.stringify(toJson())); } catch {} };
  const syncInputs = () => {
    $('lsName').value = state.name; $('lsW').value = state.w; $('lsH').value = state.h;
    $('lsPlayer').value = state.player; $('lsZoom').value = state.px;
  };
  const fromJson = (j) => {
    state.name = j.name || 'nivel'; state.w = j.width; state.h = j.height; state.player = j.playerCells || 2;
    state.cells = alloc(state.w, state.h);
    j.rows.forEach((row, y) => { for (let x = 0; x < state.w; x++) state.cells[y][x] = row[x] === '#' ? 1 : 0; });
    syncInputs(); draw();
  };

  function draw() {
    const { w, h, px } = state;
    canvas.width = w * px; canvas.height = h * px;
    ctx.fillStyle = '#10191b'; ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = '#b08a5a';
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (state.cells[y][x]) ctx.fillRect(x * px, y * px, px, px);
    ctx.strokeStyle = 'rgba(255,255,255,.07)'; ctx.lineWidth = 1; ctx.beginPath();
    for (let x = 0; x <= w; x++) { ctx.moveTo(x * px + .5, 0); ctx.lineTo(x * px + .5, h * px); }
    for (let y = 0; y <= h; y++) { ctx.moveTo(0, y * px + .5); ctx.lineTo(w * px, y * px + .5); }
    ctx.stroke();
    if (hover) { // player silhouette standing on the hovered cell's bottom edge
      const hp = state.player * px, wp = hp * 0.5;
      ctx.fillStyle = 'rgba(227,168,80,.55)';
      ctx.fillRect(hover.x * px + px / 2 - wp / 2, (hover.y + 1) * px - hp, wp, hp);
      ctx.strokeStyle = '#56e0b4'; ctx.strokeRect(hover.x * px + 1, hover.y * px + 1, px - 2, px - 2);
    }
    $('lsStatus').textContent = `${w}×${h} celdas · jugador = ${state.player} celdas de alto` +
      (hover ? ` · celda ${hover.x},${hover.y}` : '');
  }

  const cellAt = (e) => {
    const r = canvas.getBoundingClientRect();
    const x = Math.floor((e.clientX - r.left) / r.width * state.w), y = Math.floor((e.clientY - r.top) / r.height * state.h);
    return x >= 0 && y >= 0 && x < state.w && y < state.h ? { x, y } : null;
  };
  const paintAt = (c) => { if (c && painting !== null) state.cells[c.y][c.x] = painting; };

  canvas.addEventListener('contextmenu', e => e.preventDefault());
  canvas.addEventListener('mousedown', e => {
    painting = e.button === 2 || e.shiftKey ? 0 : 1; paintAt(cellAt(e)); draw();
  });
  canvas.addEventListener('mousemove', e => { hover = cellAt(e); paintAt(hover); draw(); });
  canvas.addEventListener('mouseleave', () => { hover = null; draw(); });
  window.addEventListener('mouseup', () => { if (painting !== null) { painting = null; save(); } });

  const readInt = (id, min, max, fallback) => Math.min(max, Math.max(min, parseInt($(id).value, 10) || fallback));
  $('lsResize').addEventListener('click', () => {
    state.w = readInt('lsW', 8, 400, state.w); state.h = readInt('lsH', 8, 300, state.h);
    state.cells = alloc(state.w, state.h, state.cells); syncInputs(); draw(); save();
  });
  $('lsName').addEventListener('input', () => { state.name = $('lsName').value.trim() || 'nivel'; save(); });
  $('lsPlayer').addEventListener('input', () => { state.player = readInt('lsPlayer', 1, 10, 2); draw(); save(); });
  $('lsZoom').addEventListener('input', () => { state.px = readInt('lsZoom', 6, 48, 20); draw(); });
  $('lsClear').addEventListener('click', () => { if (confirm('¿Borrar todo el boceto?')) { state.cells = alloc(state.w, state.h); draw(); save(); } });
  $('lsFloor').addEventListener('click', () => { state.cells[state.h - 1].fill(1); draw(); save(); });
  $('lsExport').addEventListener('click', () => {
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([JSON.stringify(toJson(), null, 1)], { type: 'application/json' }));
    a.download = `${state.name}.levelsketch.json`; a.click(); URL.revokeObjectURL(a.href);
  });
  $('lsImport').addEventListener('change', async (e) => {
    const f = e.target.files[0]; if (!f) return;
    try { fromJson(JSON.parse(await f.text())); save(); } catch { alert('Archivo no válido'); }
    e.target.value = '';
  });

  try { const s = localStorage.getItem(STORE_KEY); if (s) fromJson(JSON.parse(s)); } catch {}
  syncInputs(); draw();
  return { onShow: draw };
}
