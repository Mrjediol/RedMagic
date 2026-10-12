// modules/parallax-editor.js
// -----------------------------------------------------------------------------
// 🏞️ Fondos: monta un fondo de parallax por capas (lo que hacía Web/ParallaxTest/parallax-test.html) y lo prueba
// detrás de un nivel de referencia con el borde del bioma. Se guarda en la Biblioteca (kind 'parallax': se puede dejar
// a medias y lo ve 🧱 Niveles para probarlo en un nivel) y se exporta a Unity en un solo archivo (*.parallax.json →
// EditorNiveles ▸ Importar fondo…). Los campos de cada capa son los de ParallaxBackgroundLayer (parallax-data.js).
import * as Terrain from './level-terrain.js';
import * as PX from './parallax-data.js';
import { saveEnemy, getEnemy, buildThumbnail } from './enemy-library.js';
import { downloadBlob } from './export-manifest.js';

const VIEW_H = 20, PLAYER = { w: 0.6, h: 1.74 };
const STORE = 'redmagic.parallaxEditor.v1';
const pct = (v) => `${Math.round(v * 100)}%`;
const FIELDS = [
  { k: 'speed', label: 'Velocidad', type: 'range', min: 0, max: 1.5, step: 0.01, fmt: (v) => v.toFixed(2), tip: '0 = quieta (lejos), 1 = como el mundo, >1 = primer plano' },
  { k: 'repeat', label: 'Repetir', type: 'select', options: PX.REPEATS },
  { k: 'positionX', label: 'Posición X', type: 'range', min: -20, max: 20, step: 0.05, fmt: (v) => `${v.toFixed(2)} u` },
  { k: 'size', label: 'Tamaño', type: 'range', min: 5, max: 600, step: 1, fmt: (v) => `${v}%`, tip: 'Alto de la imagen en % del alto de la pantalla' },
  { k: 'heightOffset', label: 'Altura', type: 'range', min: -100, max: 100, step: 0.5, fmt: (v) => v.toFixed(1), tip: '% de media pantalla, positivo = arriba' },
  { k: 'anchor', label: 'Anclaje vertical', type: 'select', options: PX.ANCHORS },
  { k: 'align', label: 'Alinear', type: 'select', options: PX.ALIGNS, when: (l) => l.anchor === 'Proportional' },
  { k: 'travel', label: 'Recorrido', type: 'range', min: 0, max: 1.5, step: 0.01, fmt: pct, when: (l) => l.anchor === 'Proportional', tip: 'Cuánto de la imagen se recorre de arriba abajo del nivel' },
  { k: 'verticalSpeed', label: 'Vel. vertical', type: 'range', min: 0, max: 1.2, step: 0.01, fmt: (v) => v.toFixed(2), when: (l) => l.anchor !== 'Proportional' },
  { k: 'opacity', label: 'Opacidad', type: 'range', min: 0, max: 1, step: 0.01, fmt: pct },
  { k: 'fade', label: 'Atenuar', type: 'range', min: 0, max: 0.9, step: 0.01, fmt: pct, tip: 'Mezcla con el color de la niebla: aleja la capa' },
  { k: 'fogColor', label: 'Color de la niebla', type: 'color' },
  { k: 'inFront', label: 'Delante del jugador', type: 'check' },
  { k: 'overTerrain', label: 'Encima del terreno (puertas)', type: 'check' },
  { k: 'sitOnGround', label: 'Apoyada en el suelo', type: 'check', when: (l) => l.repeat === 'LevelEnds' },
  { k: 'key', label: 'Quitar magenta', type: 'check', image: true },
  { k: 'crop', label: 'Recortar bordes', type: 'range', min: 0, max: 40, step: 1, fmt: (v) => `${v} px`, image: true },
];
const toHex = (c) => '#' + [c.r, c.g, c.b].map((v) => Math.round(v * 255).toString(16).padStart(2, '0')).join('');
const fromHex = (h) => ({ r: parseInt(h.slice(1, 3), 16) / 255, g: parseInt(h.slice(3, 5), 16) / 255, b: parseInt(h.slice(5, 7), 16) / 255, a: 1 });

/**
 * @param {{ toast: (msg: string, kind?: string) => void, setDirty: (dirty: boolean, what?: string) => void, onSaved?: () => void }} o
 * @returns {{ onShow: () => void, save: () => Promise<void> }}
 */
export function initParallaxEditor({ toast, setDirty, onSaved }) {
  const $ = (id) => document.getElementById(id);
  const panel = $('tab-backgrounds'), cv = $('pbCanvas'), g = cv.getContext('2d');
  const D = { id: null, createdAt: null, name: 'Fondo', layers: [], levelW: 85, levelH: 25 };
  let ready = [];          // capas con su imagen cargada y procesada
  let sel = 0, biomes = [], server = null;

  // ---------------------------------------------------------------- capas e imágenes
  async function refreshImage(i) {
    const l = D.layers[i];
    if (!l) return;
    const src = await PX.loadImage(l.image);
    ready[i] = { ...l, src, img: PX.processImage(src, l) };
    redraw();
  }
  async function refreshAll() { ready = await PX.readyLayers(D.layers); renderLayers(); redraw(); }
  const syncReady = (i) => { if (ready[i]) ready[i] = { ...D.layers[i], src: ready[i].src, img: ready[i].img }; };

  function readFile(file) {
    return new Promise((resolve) => { const r = new FileReader(); r.onload = () => resolve(r.result); r.onerror = () => resolve(null); r.readAsDataURL(file); });
  }
  async function setImage(i, file) {
    if (!D.layers[i]) return;
    D.layers[i].image = file ? await readFile(file) : null;
    edited('imagen');
    await refreshImage(i);
    renderLayers();
  }

  // ---------------------------------------------------------------- tarjetas de capas
  function renderLayers() {
    const host = $('pbLayers'), scroll = host.scrollTop;
    host.innerHTML = '';
    D.layers.forEach((l, i) => host.appendChild(card(l, i)));
    host.scrollTop = scroll;
  }
  function card(l, i) {
    const d = document.createElement('div');
    d.className = 'pbLayer' + (i === sel ? ' sel' : '');
    const head = document.createElement('div');
    head.className = 'pbLayerHead';
    head.innerHTML = `<input type="checkbox" ${l.on ? 'checked' : ''} title="Visible">
      <span class="pbThumb" style="${l.image ? `background-image:url(${l.image})` : ''}"></span>
      <input type="text" class="pbLayerName" value="${l.name}">
      <button class="small" data-act="up" title="Más lejos" ${i === 0 ? 'disabled' : ''}>▲</button>
      <button class="small" data-act="down" title="Más cerca" ${i === D.layers.length - 1 ? 'disabled' : ''}>▼</button>
      <button class="small danger" data-act="del" title="Quitar la capa">✕</button>`;
    d.appendChild(head);
    head.querySelector('input[type=checkbox]').onchange = (e) => { l.on = e.target.checked; syncReady(i); redraw(); };
    head.querySelector('.pbLayerName').oninput = (e) => { l.name = e.target.value; };
    head.querySelectorAll('[data-act]').forEach((b) => { b.onclick = (e) => { e.stopPropagation(); layerAction(i, b.dataset.act); }; });
    d.addEventListener('mousedown', () => { if (sel !== i) { sel = i; renderLayers(); } });
    if (i !== sel) return d;

    const body = document.createElement('div');
    body.className = 'pbLayerBody';
    const img = document.createElement('div');
    img.className = 'row';
    img.innerHTML = '<button class="small" data-img="up">Subir imagen…</button><button class="small" data-img="clr">Quitar imagen</button><input type="file" accept="image/*" hidden>';
    const fi = img.querySelector('input');
    img.querySelector('[data-img=up]').onclick = () => fi.click();
    img.querySelector('[data-img=clr]').onclick = () => setImage(i, null);
    fi.onchange = () => { if (fi.files[0]) setImage(i, fi.files[0]); fi.value = ''; };
    body.appendChild(img);
    for (const f of FIELDS) {
      if (f.when && !f.when(l)) continue;
      const row = document.createElement('label');
      row.className = 'pbField' + (f.type === 'check' ? ' pbCheck' : '');
      if (f.tip) row.title = f.tip;
      if (f.type === 'range') {
        row.innerHTML = `<span>${f.label}</span><input type="range" min="${f.min}" max="${f.max}" step="${f.step}" value="${l[f.k]}"><b>${f.fmt(l[f.k])}</b>`;
        const inp = row.querySelector('input'), out = row.querySelector('b');
        inp.oninput = () => { l[f.k] = +inp.value; out.textContent = f.fmt(l[f.k]); changedField(i, f); };
      } else if (f.type === 'select') {
        row.innerHTML = `<span>${f.label}</span><select>${f.options.map(([v, t]) => `<option value="${v}" ${l[f.k] === v ? 'selected' : ''}>${t}</option>`).join('')}</select>`;
        row.querySelector('select').onchange = (e) => { l[f.k] = e.target.value; changedField(i, f); renderLayers(); };
      } else if (f.type === 'color') {
        row.innerHTML = `<span>${f.label}</span><input type="color" value="${toHex(l[f.k])}">`;
        row.querySelector('input').oninput = (e) => { l[f.k] = fromHex(e.target.value); changedField(i, f); };
      } else {
        row.innerHTML = `<input type="checkbox" ${l[f.k] ? 'checked' : ''}><span>${f.label}</span>`;
        row.querySelector('input').onchange = (e) => { l[f.k] = e.target.checked; changedField(i, f); };
      }
      body.appendChild(row);
    }
    d.appendChild(body);
    return d;
  }
  function changedField(i, f) {
    if (f.image) refreshImage(i); else { syncReady(i); redraw(); }
  }
  function layerAction(i, act) {
    if (act === 'del') { D.layers.splice(i, 1); ready.splice(i, 1); sel = Math.max(0, Math.min(sel, D.layers.length - 1)); }
    else {
      const j = i + (act === 'up' ? -1 : 1);
      [D.layers[i], D.layers[j]] = [D.layers[j], D.layers[i]];
      [ready[i], ready[j]] = [ready[j], ready[i]];
      if (sel === i) sel = j;
    }
    edited('capas'); renderLayers(); redraw();
  }
  $('pbAddLayer').onclick = () => {
    D.layers.push(PX.newLayer(`Capa ${D.layers.length + 1}`));
    ready.push({ ...D.layers[D.layers.length - 1], img: null });
    sel = D.layers.length - 1; edited('capas'); renderLayers();
  };

  // ---------------------------------------------------------------- nivel de referencia (marco + suelo, con el borde del bioma)
  let terrain = null;
  const lw = () => D.levelW, lh = () => D.levelH;
  const solid = (c, r) => c <= 0 || r <= 0 || c >= lw() - 1 || r >= lh() - 1;
  const groundTop = () => 1;
  function biome() { return biomes.find((b) => b.name === $('pbBiome').value) || biomes[0] || null; }
  async function buildTerrain() {
    const b = biome();
    if (!b) { terrain = null; return redraw(); }
    const rock = server && b.rockTexture ? await PX.loadImage(PX.artUrl(b.rockTexture)) : null;
    const ppu = Math.max(8, Math.min(40, Math.floor(Math.sqrt(24e6 / (lw() * lh())))));
    terrain = { cv: Terrain.build(solid, lw(), lh(), b, rock, ppu), w: lw(), h: lh() };
    redraw();
  }

  // ---------------------------------------------------------------- dibujo (cámara del juego siguiendo al jugador)
  let CW = 0, CH = 0, dpr = 1, needDraw = true, running = false;
  const player = { x: 6, y: 1 }, keys = {};
  const redraw = () => { needDraw = true; };
  function resizeCanvas() {
    dpr = window.devicePixelRatio || 1; const r = $('pbStage').getBoundingClientRect();
    if (!r.width || !r.height) return;
    CW = r.width; CH = r.height; cv.width = Math.round(CW * dpr); cv.height = Math.round(CH * dpr);
    cv.style.width = CW + 'px'; cv.style.height = CH + 'px'; redraw();
  }
  function draw() {
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    g.fillStyle = '#05080d'; g.fillRect(0, 0, CW, CH);
    let w = CW, h = CW * 9 / 16; if (h > CH) { h = CH; w = CH * 16 / 9; }
    const vp = { x0: (CW - w) / 2, y0: (CH - h) / 2, ppu: h / VIEW_H, viewW: VIEW_H * w / h, viewH: VIEW_H };
    const cam = {
      x: lw() <= vp.viewW ? lw() / 2 : Math.max(vp.viewW / 2, Math.min(lw() - vp.viewW / 2, player.x)),
      y: lh() <= VIEW_H ? lh() / 2 : Math.max(VIEW_H / 2, Math.min(lh() - VIEW_H / 2, player.y + 3)),
    };
    const ox = vp.x0 - (cam.x - vp.viewW / 2) * vp.ppu, oy = vp.y0 + (cam.y + VIEW_H / 2 - lh()) * vp.ppu;
    g.save(); g.beginPath(); g.rect(vp.x0, vp.y0, w, h); g.clip();
    g.fillStyle = 'rgb(7,20,38)'; g.fillRect(vp.x0, vp.y0, w, h);
    const level = { w: lw(), h: lh() };
    const layers = (kind) => ready.forEach((L) => {
      if (!L || !L.on || !L.img) return;
      const k = L.inFront ? 'front' : L.overTerrain ? 'over' : 'back';
      if (k === kind) Terrain.drawLayer(g, L, L.img, cam, vp, level, groundTop);
    });
    layers('back');
    if (terrain && terrain.w === lw() && terrain.h === lh()) g.drawImage(terrain.cv, ox, oy, lw() * vp.ppu, lh() * vp.ppu);
    else { g.fillStyle = '#22364d'; g.fillRect(vp.x0, oy + (lh() - 1) * vp.ppu, w, vp.ppu); }
    layers('over');
    const px = ox + (player.x - PLAYER.w / 2) * vp.ppu, py = oy + (lh() - player.y - PLAYER.h) * vp.ppu;
    g.fillStyle = 'rgba(255,90,90,.9)'; g.fillRect(px, py, PLAYER.w * vp.ppu, PLAYER.h * vp.ppu);
    g.fillStyle = '#fff'; g.fillRect(px, py, PLAYER.w * vp.ppu, PLAYER.h * vp.ppu * 0.22);
    layers('front');
    g.restore();
    $('pbStatus').textContent = `Nivel ${lw()} × ${lh()} u · jugador x ${player.x.toFixed(1)} y ${player.y.toFixed(1)} · ${D.layers.length} capas`;
  }
  let lastT = performance.now();
  function loop() {
    if (panel.hidden) { running = false; return; }
    const now = performance.now(), dt = Math.min(0.05, (now - lastT) / 1000); lastT = now;
    const sp = 10 * dt; let mx = 0, my = 0;
    if (keys.a || keys.arrowleft) mx -= sp; if (keys.d || keys.arrowright) mx += sp;
    if (keys.w || keys.arrowup) my += sp; if (keys.s || keys.arrowdown) my -= sp;
    if (keys.r) { player.y = 1; keys.r = false; redraw(); }
    if (mx || my) {
      player.x = Math.max(1.5, Math.min(lw() - 1.5, player.x + mx));
      player.y = Math.max(1, Math.min(lh() - 1 - PLAYER.h, player.y + my)); redraw();
    }
    if (needDraw) { needDraw = false; draw(); }
    requestAnimationFrame(loop);
  }

  // ---------------------------------------------------------------- guardar / abrir / exportar
  function edited(what) { setDirty(true, what); storeLocal(); }
  let storeTimer = 0;
  function storeLocal() {
    clearTimeout(storeTimer);
    storeTimer = setTimeout(() => { try { localStorage.setItem(STORE, JSON.stringify({ id: D.id })); } catch { /* sin almacenamiento */ } }, 300);
  }
  function syncSide() { $('pbName').value = D.name; $('pbLvW').value = D.levelW; $('pbLvH').value = D.levelH; }
  /** Ctrl+S y 💾: a la Biblioteca (se puede dejar a medias; 🧱 Niveles lo ve al momento). */
  async function save() {
    if (!D.layers.length) throw new Error('el fondo no tiene capas');
    draw();
    const rec = await saveEnemy({
      id: D.id, kind: PX.KIND, enemyName: D.name, createdAt: D.createdAt,
      thumbnail: buildThumbnail(cv, 220), draft: { layers: D.layers, levelW: D.levelW, levelH: D.levelH },
    });
    D.id = rec.id; D.createdAt = rec.createdAt;
    setDirty(false); storeLocal(); refreshOpen(); onSaved?.();
    setTimeout(() => toast(`Fondo "${D.name}" guardado en la Biblioteca`), 0);
  }
  async function open(id) {
    const e = await getEnemy(id);
    if (!e || e.kind !== PX.KIND) return toast('No encuentro ese fondo', 'error');
    D.id = e.id; D.createdAt = e.createdAt; D.name = e.enemyName;
    D.layers = (e.draft?.layers || []).map(PX.normalizeLayer);
    D.levelW = e.draft?.levelW || 85; D.levelH = e.draft?.levelH || 25;
    sel = 0; syncSide(); await refreshAll(); buildTerrain(); setDirty(false); storeLocal();
  }
  function fresh() {
    D.id = null; D.createdAt = null; D.name = 'Fondo nuevo'; D.layers = [PX.newLayer('Capa 1')]; sel = 0;
    syncSide(); refreshAll(); setDirty(false); storeLocal();
  }
  /** Un fondo que ya está en Unity (el de un bioma), para retocarlo: imágenes y valores del prefab. */
  async function fromUnity(biomeName) {
    const b = biomes.find((x) => x.name === biomeName);
    if (!b || !server) return toast('Hace falta el servidor del taller (start.cmd) para traer el fondo de Unity', 'error');
    toast('Trayendo el fondo de Unity…', 'info');
    const toData = async (p) => {
      if (!p) return null;
      const r = await fetch(PX.artUrl(p)); if (!r.ok) return null;
      const blob = await r.blob();
      return new Promise((res) => { const fr = new FileReader(); fr.onload = () => res(fr.result); fr.readAsDataURL(blob); });
    };
    const layers = [];
    for (const [i, L] of (b.layers || []).entries()) {
      const { texture, name, ...fields } = L;
      layers.push({ ...PX.newLayer(name || `Capa ${i + 1}`), ...fields, image: await toData(texture) });
    }
    D.id = null; D.createdAt = null; D.name = (b.backgroundName || `Fondo_${b.name}`).replace(/^Fondo_/, '');
    D.layers = layers; sel = 0; $('pbBiome').value = b.name;
    syncSide(); await refreshAll(); buildTerrain(); setDirty(true, 'traído de Unity');
    toast(`Fondo de ${b.name} traído de Unity: guárdalo para tenerlo en la Biblioteca`);
  }
  async function refreshOpen() {
    const list = await PX.listBackgrounds();
    const uni = server ? biomes.filter((b) => b.layers?.length) : [];
    $('pbOpen').innerHTML = '<option value="">Abrir…</option><option value="new">＋ Fondo nuevo</option>'
      + (list.length ? `<optgroup label="Biblioteca">${list.map((e) => `<option value="lib:${e.id}">${e.enemyName}</option>`).join('')}</optgroup>` : '')
      + (uni.length ? `<optgroup label="Traer de Unity">${uni.map((b) => `<option value="unity:${b.name}">Fondo de ${b.name}</option>`).join('')}</optgroup>` : '');
  }
  $('pbOpen').onchange = (e) => {
    const v = e.target.value; e.target.value = '';
    if (v === 'new') fresh();
    else if (v.startsWith('lib:')) open(v.slice(4));
    else if (v.startsWith('unity:')) fromUnity(v.slice(6));
  };
  $('pbSave').onclick = () => save().catch((err) => toast(`No se pudo guardar: ${err.message}`, 'error'));
  $('pbExport').onclick = async () => {
    if (!D.layers.some((l) => l.on && l.image)) return toast('No hay ninguna capa con imagen', 'error');
    const file = await PX.toUnityFile(D.name, D.layers);
    downloadBlob(new Blob([JSON.stringify(file)], { type: 'application/json' }), `${D.name.replace(/[\\/:*?"<>|.]+/g, '_')}.parallax.json`);
    toast('Exportado: en Unity, EditorNiveles ▸ Importar fondo…');
  };
  $('pbName').oninput = (e) => { D.name = e.target.value.trim() || 'Fondo'; };
  const sizeInput = (id, key, max) => { $(id).onchange = (e) => { D[key] = Math.max(12, Math.min(max, +e.target.value | 0)); e.target.value = D[key]; buildTerrain(); redraw(); }; };
  sizeInput('pbLvW', 'levelW', 400); sizeInput('pbLvH', 'levelH', 75);
  $('pbBiome').onchange = () => buildTerrain();

  // ---------------------------------------------------------------- imágenes: pegar, arrastrar
  document.addEventListener('paste', (e) => {
    if (panel.hidden || /^(INPUT|TEXTAREA)$/.test(e.target.tagName) && e.target.type === 'text') return;
    const f = [...(e.clipboardData?.files || [])].find((x) => x.type.startsWith('image/'));
    if (!f || !D.layers[sel]) return;
    e.preventDefault(); setImage(sel, f);
  });
  panel.addEventListener('dragover', (e) => e.preventDefault());
  panel.addEventListener('drop', (e) => {
    e.preventDefault();
    [...e.dataTransfer.files].filter((f) => f.type.startsWith('image/')).forEach((f, k) => {
      while (sel + k >= D.layers.length) D.layers.push(PX.newLayer(`Capa ${D.layers.length + 1}`));
      setImage(sel + k, f);
    });
  });

  // ---------------------------------------------------------------- teclado: mover al jugador
  const typing = (t) => t && t.matches && t.matches('input[type=text],input[type=number],select,textarea');
  window.addEventListener('keydown', (e) => {
    if (panel.hidden || typing(e.target) || e.ctrlKey || e.metaKey || e.altKey) return;
    const k = e.key.toLowerCase();
    if (['w', 'a', 's', 'd', 'r', 'arrowup', 'arrowdown', 'arrowleft', 'arrowright'].includes(k)) { keys[k] = true; lastT = performance.now(); e.preventDefault(); }
  });
  window.addEventListener('keyup', (e) => { keys[e.key.toLowerCase()] = false; });
  window.addEventListener('blur', () => { for (const k in keys) keys[k] = false; });

  // ---------------------------------------------------------------- arranque
  new ResizeObserver(resizeCanvas).observe($('pbStage'));
  (async () => {
    server = await PX.tallerServer();
    biomes = (await PX.unityData())?.biomes || [];
    $('pbBiome').innerHTML = biomes.map((b) => `<option>${b.name}</option>`).join('') || '<option>(sin datos de Unity)</option>';
    let last = null;
    try { last = JSON.parse(localStorage.getItem(STORE) || 'null')?.id; } catch { /* sin almacenamiento */ }
    if (last && (await getEnemy(last))) await open(last); else fresh();
    refreshOpen(); buildTerrain();
  })();

  return {
    /** Al abrir la pestaña: arranca el dibujo y relee la Biblioteca. */
    onShow() {
      resizeCanvas(); refreshOpen(); redraw();
      if (!running) { running = true; lastT = performance.now(); requestAnimationFrame(loop); }
    },
    save,
    /** Abre un fondo de la Biblioteca (botón «Abrir en Fondos» de la Biblioteca). */
    open,
  };
}
