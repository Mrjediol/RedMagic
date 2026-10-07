// modules/designs-panel.js
// -----------------------------------------------------------------------------
// 🎨 Diseños + 👾 Enemigos: review what a task proposed — enemies, projectiles, effects, sounds — and the enemy
// library. Lives inside 📋 Tareas ▸ Talleres with two roots (`root` = the Diseños taller, `enemiesRoot` = Enemigos);
// todo-panel.js leaves anything under [data-designs] to this module.
//
// The batches are docs/disenos/<id>.json in the repo and go through tools/taller-server (disenos.mjs), so
// this tab only works with the web served by start.cmd. Full design: docs/architecture/designs.md.
//   · Por revisar — one foldable group per batch. Click a card to select it (only it; Ctrl+click adds / removes,
//     Shift+click a range, Esc clears — as in the Windows explorer); accept / discard one, the selection, or all that
//     are left; ↶ undoes. Ctrl+V puts a picture on an enemy card: the one the cursor is in, else the selected one,
//     else the one under the mouse; then the next card without picture is selected. Each kind previews as itself: the enemy's image, the effect
//     playing live (vfx-presets.js), the sound on ▶ (vfx-sounds.js).
//   · Accepting: an enemy → the enemy library (docs/enemigos/<Name>/, "por montar"); a sound → the game's
//     palette (server); a projectile / effect → baked HERE into the 📚 Biblioteca (IndexedDB, the same record
//     the VFX Lab saves), then the server records its libraryId.
//   · Pedir propuestas — kind + what you want + how many + one task or one per proposal → the server makes the
//     (empty) batch and queues the task(s) with a short prompt; the rules are in the design-proposals skill.
//     A batch whose tasks are still running shows as "generándose".
//   · Enemigos — the library (docs/enemigos/), folded by estado. A card opens its sheet: build it (make-enemy) —
//     every launch (all at once, step 1, step 3, redo / retry) takes optional extra instructions for Claude —,
//     approve / discard it, note its pendientes (also from Unity: Tools ▸ RedMagic ▸ Enemigos) and launch a task
//     for them. "＋ Enemigo existente" adds a prefab that did not come from a design.
//   · Lotes revisados — folded.

import { presetById, normalizeParams, bakePreset, drawFrame, frameCount, laneFps } from './vfx-presets.js';
import { saveEnemy, deleteEnemy, listEntries, buildThumbnail } from './enemy-library.js';
import { cropBox } from './export-manifest.js';
import { renderSound } from './vfx-sounds.js';
import { playSound } from './vfx-stage.js';
import { pressGuard } from './press-guard.js';

const CLOSED_KEY = 'redmagic.designs.closed';
const FILTER_KEY = 'redmagic.designs.filter';
const POLL_MS = 5000;

const KIND = {
  enemy: { icon: '👾', label: 'Enemigos', one: 'enemigo', accepted: '✓ En la biblioteca de enemigos' },
  projectile: { icon: '🔥', label: 'Proyectiles', one: 'proyectil', accepted: '✓ En 📚 Biblioteca' },
  fx: { icon: '✨', label: 'Efectos VFX', one: 'efecto', accepted: '✓ En 📚 Biblioteca' },
  sound: { icon: '🔊', label: 'Sonidos', one: 'sonido', accepted: '✓ En la paleta de sonidos' },
};
// In the order the library shows them: what needs the user first.
const PASO_LABEL = { montaje: 'Montaje completo', cuerpo: 'Paso 1 · cuerpo', resto: 'Paso 3 · el resto', estados: 'Estados' };
const ESTADOS = {
  'cuerpo-por-aprobar': { label: 'Cuerpo por aprobar', one: 'Cuerpo por aprobar', hint: 'paso 1: mira el cuerpo montado; ajústalo en 🧬 Enemy Studio si hace falta' },
  'cuerpo-aprobado': { label: 'Cuerpo aprobado: elige el resto', one: 'Cuerpo aprobado', hint: 'paso 2: di qué tipo de enemigo es y de dónde salen sus proyectiles, sonidos y efectos' },
  'por-aprobar': { label: 'Esperando tu aprobación', one: 'Por aprobar', hint: 'montado en Unity: pruébalo y apruébalo, o apunta lo que falta' },
  'con-pendientes': { label: 'Con pendientes', one: 'Con pendientes', hint: 'existe, pero le faltan cosas' },
  'por-montar': { label: 'Por montar', one: 'Por montar', hint: 'diseño aprobado; falta montarlo en Unity (make-enemy)' },
  'montando-cuerpo': { label: 'Montando el cuerpo', one: 'Montando cuerpo', hint: 'paso 1: una tarea separa las piezas y monta el cuerpo' },
  montando: { label: 'Montándose', one: 'Montándose', hint: 'una tarea lo está montando' },
  aprobado: { label: 'Aprobados', one: 'Aprobado', hint: 'terminados' },
  descartado: { label: 'Descartados', one: 'Descartado', hint: 'el prefab sigue en Unity si llegó a montarse' },
};
const PENDING_TYPES = { comportamiento: 'Comportamiento', sprite: 'Sprite', gesto: 'Gesto', ataque: 'Ataque', bug: 'Bug', otro: 'Otro' };
// When a part state shows (the rig's sprite states; same keys as design-prompts.mjs ▸ STATE_USES).
const STATE_USES = { '': 'Claude decide', asleep: 'Dormido', awake: 'Despierto', alert: 'Alerta (ve al jugador)', attacking: 'Atacando', hurt: 'Herido', dead: 'Muerto', gesto: 'En un ataque concreto' };
// Where the sheet offers a launch that cuts the body (step 1, its redo / retry, the full build): the 🧩 Piezas box shows.
const CUT_ESTADOS = ['por-montar', 'montando-cuerpo', 'montando', 'cuerpo-por-aprobar', 'cuerpo-aprobado'];
const TASK_STATE = { queued: '⏳ en la cola', waiting: '⏳ esperando', running: '⚙️ trabajando', failed: '✗ falló', cancelled: '✗ cancelada', interrupted: '✗ interrumpida' };
const ASK_HINT = {
  enemy: 'Ej.: tortuga voladora que lanza su caparazón. Genera bases 1024 en ComfyUI (la cola lo abre) con el estilo Fracture del juego.',
  projectile: 'Ej.: bola de hielo con estela de copos. Variantes de los presets del VFX Lab.',
  fx: 'Ej.: explosión verde de veneno para el impacto. Variantes de los presets del VFX Lab.',
  sound: 'Ej.: golpe seco de caparazón al chocar. Sonidos sintetizados como los de la paleta del juego.',
};
const MODELS = [['sonnet', 'Sonnet'], ['opus', 'Opus'], ['haiku', 'Haiku']];
const EFFORTS = [['low', 'bajo'], ['medium', 'medio'], ['high', 'alto']];
const GENERATORS = [['prompt', '📋 Prompt para Gemini'], ['comfy', '🎨 Enemigo con ComfyUI']];   // enemy: a prompt to paste in Gemini, or ComfyUI draws it (the server's default: the first)
const GEMINI_URL = 'https://gemini.google.com/app';

const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const imgUrl = (src) => `/api/img?src=${encodeURIComponent(src)}`;
const createCanvas = (w, h) => { const c = document.createElement('canvas'); c.width = w; c.height = h; return c; };
const strip = (s) => String(s).normalize('NFD').replace(/[̀-ͯ]/g, '');
/** The shape each kind's name takes: PascalCase for things, snake_case for sounds (their ids). */
function tidyName(kind, s) {
  if (kind === 'sound') return strip(s).toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '');
  if (kind === 'enemy') return strip(s).replace(/[^A-Za-z0-9]+(.)?/g, (_, c) => (c ? c.toUpperCase() : '')).replace(/^./, (c) => c.toUpperCase());
  return strip(s).replace(/[^A-Za-z0-9_]+/g, '_');
}

export function initDesignsPanel({ root, enemiesRoot, statusEl, toast, onBadges = () => {}, onEditBody = null, mobile = false }) {
  const roots = [root, enemiesRoot];
  let token = null;
  let data = null, dataText = '';
  let pollTimer = null, pendingRender = false, busy = false;
  let filter = sessionStorage.getItem(FILTER_KEY) || 'all';
  const selected = new Set();               // "loteId:itemId"
  let anchor = null;                        // the last card clicked: Shift+click selects from it, Ctrl+V prefers it
  let hoverKey = null;                      // the pending enemy card under the mouse
  const instr = {};                         // enemy name → extra instructions typed in its sheet (sent with the next launch)
  const names = new Map();                  // "loteId:itemId" → name typed in the card
  const errors = new Map();                 // "loteId:itemId" → last error
  const closed = new Set(JSON.parse(sessionStorage.getItem(CLOSED_KEY) || '[]'));
  let viewer = null;                        // { list: [key…], i } | { enemy: name }
  let stopSound = () => {};
  const ask = { kind: 'enemy', request: '', count: 5, mode: 'one', model: 'sonnet', effort: 'medium', generator: 'prompt', zone: '', body: '', attack: '', images: [] };   // the request form
  const run = { model: 'sonnet', effort: 'medium' };                 // model of the tasks launched from an enemy
  const p2 = {};                            // enemy name → the step-2 form (kind of enemy + where the assets come from)
  const p2Of = (name) => (p2[name] ||= { via: 'flyingRanged', proyectil: { origen: 'biblioteca', texto: '', elegidos: [] }, sonidos: { origen: 'biblioteca', texto: '', elegidos: [] }, efectos: { origen: 'biblioteca', texto: '', elegidos: [] }, notas: '' });
  /** The library's entries a step-2 asset can be picked from (loaded once). */
  let lib = null, libLoading = false;
  const openGroups = new Set();
  const LIB_KINDS = { proyectil: 'projectile', sonidos: 'sound', efectos: 'fx' };
  async function loadLib() {
    libLoading = true;
    const out = {};
    for (const [k, kind] of Object.entries(LIB_KINDS)) out[k] = (await listEntries(kind)).map((x) => ({ id: x.id, name: x.enemyName || x.id }));
    lib = out;
    libLoading = false;
    if (viewer?.enemy && !typing(overlay)) renderViewer();
  }
  const draft = { tipo: 'comportamiento', texto: '' };               // the pendiente being written in a sheet
  const pz = {};                            // enemy name → the cut chosen in its sheet { modo: auto|manual, lista: [{ nombre, nota }] }
  const pzOf = (e) => (pz[e.name] ||= { modo: e.piezas?.modo === 'manual' ? 'manual' : 'auto', lista: (e.piezas?.lista || []).map((p) => ({ nombre: p.nombre, nota: p.nota || '' })) });
  const stDraft = { pieza: '', nombre: '', texto: '', uso: '', frames: 1 };   // the part state being written in a sheet
  let launching = false;                    // a launch is on its way: a second click must not queue it twice
  let focusPiece = null;                    // the 🧩 row to focus after the redraw ("Otra…")
  const press = pressGuard(() => [...roots, overlay]);   // no redraw under a held button: it would eat the click
  let adding = null;                        // null | [{ name, prefab }] while "＋ Enemigo existente" is open

  const setStatus = (t) => { statusEl.textContent = t; };
  const keyOf = (lote, item) => `${lote.id}:${item.id}`;
  const nameOf = (lote, item) => names.get(keyOf(lote, item)) ?? item.name;
  const typing = (scope) => scope.contains(document.activeElement) && /^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement.tagName);
  const focusedHere = () => roots.some(typing);
  const enemyByName = (name) => (data?.enemigos || []).find((e) => e.name === name);
  const taskOf = (id) => (id ? data?.tasks?.[id] : null);
  const active = (t) => t && ['queued', 'waiting', 'running'].includes(t.status);
  const lotes = () => data?.lotes || [];
  const shown = (item) => filter === 'all' || item.kind === filter;
  const pendingOf = (lote) => lote.items.filter((i) => i.status === 'pending');
  const findItem = (key) => {
    const [loteId, itemId] = key.split(':');
    const lote = lotes().find((l) => l.id === loteId);
    const item = lote?.items.find((i) => i.id === itemId);
    return item ? { lote, item } : null;
  };

  // ------------------------------------------------------------ server

  async function api(method, route, body) {
    if (!token) {
      const hello = await fetch('/api/hello', { cache: 'no-store' }).then((r) => (r.ok ? r.json() : null)).catch(() => null);
      if (!hello?.token) throw new Error('sin servidor');
      token = hello.token;
    }
    const res = await fetch(`/api/${route}`, {
      method, cache: 'no-store', headers: { 'Content-Type': 'application/json', 'X-Taller-Token': token }, body: body ? JSON.stringify(body) : undefined,
    });
    const out = await res.json().catch(() => ({}));
    if (res.status === 403 && out.error === 'token') token = null;   // the server restarted
    if (!res.ok) throw new Error(out.error || `HTTP ${res.status}`);
    return out;
  }

  /** A change through the server, then a fresh read; the error (if any) goes to a toast. */
  async function post(route, body, done) {
    try {
      const out = await api('POST', route, body);
      if (done) toast(typeof done === 'function' ? done(out) : done);
      dataText = '';
      await poll();
      return out;
    } catch (e) { toast(e.message); return null; }
  }

  async function poll() {
    clearTimeout(pollTimer);
    try {
      const next = await api('GET', 'disenos');
      const text = JSON.stringify(next);
      if (text !== dataText) { data = next; dataText = text; renderSoon(); }
      setStatus('docs/disenos/ · servidor del taller');
    } catch {
      if (!data) root.innerHTML = '<p class="efHint">Esta pestaña necesita el servidor del taller: abre la web desde <code>Web/RedMagicWeb/tools/taller-server/start.cmd</code> (http://localhost:3000).</p>';
      setStatus('El servidor del taller no responde.');
    }
    if (!root.closest('[hidden]')) pollTimer = setTimeout(poll, POLL_MS);
  }

  function renderSoon() {
    if (press.busy()) { pendingRender = true; press.after(renderSoon); return; }
    if (focusedHere() || busy) { pendingRender = true; return; }
    render();
  }

  // ------------------------------------------------------------ deciding

  /** Projectiles / effects are baked into the browser's library before the server hears of them. */
  async function saveToLibrary(item, name) {
    if (!/^[A-Za-z][A-Za-z0-9_]{1,40}$/.test(name)) throw new Error('Nombre: letras, números y _, empezando por letra');
    const preset = presetById(item.vfx?.preset);
    if (!preset) throw new Error(`No existe el preset "${item.vfx?.preset}" en el VFX Lab`);
    if ((await listEntries(null)).some((e) => e.enemyName === name)) throw new Error(`Ya hay "${name}" en la 📚 Biblioteca`);
    const params = normalizeParams(preset, item.vfx.params || {});
    const baked = bakePreset(preset, params, createCanvas);
    const first = baked.boxes[baked.lanes[0].frameBoxIndices[Math.floor(baked.lanes[0].frameBoxIndices.length / 2)]];
    const record = await saveEnemy({
      kind: preset.kind,
      enemyName: name,
      sprite: { lanes: baked.lanes, boxes: baked.boxes, sourceDataURL: baked.canvas.toDataURL(), width: baked.width, height: baked.height, density: baked.density },
      thumbnail: buildThumbnail(cropBox(baked.canvas, first)),
      draft: { vfxPreset: { id: preset.id, params: { ...params } } },
    });
    return record.id;
  }

  async function review(loteId, itemIds, decision) {
    const lote = lotes().find((l) => l.id === loteId);
    if (!lote || !itemIds.length) return;
    busy = true;
    const failed = {};
    const libraryIds = {};
    const sent = [];
    for (const id of itemIds) {
      const item = lote.items.find((i) => i.id === id);
      const key = `${loteId}:${id}`;
      if (!item) continue;
      const name = tidyName(item.kind, nameOf(lote, item));
      names.set(key, name);
      try {
        if (item.kind === 'projectile' || item.kind === 'fx') {
          // movil.html: the 📚 Biblioteca is the PC browser's IndexedDB — baking here would save it on the phone.
          if (mobile && (decision === 'accept' || item.status === 'accepted')) throw new Error('Proyectiles y efectos: acéptalos desde el PC (van a la 📚 Biblioteca de su navegador)');
          if (decision === 'accept' && item.status !== 'accepted') libraryIds[id] = await saveToLibrary(item, name);
          if (decision !== 'accept' && item.status === 'accepted' && item.libraryId) await deleteEnemy(item.libraryId);
        }
        sent.push(id);
      } catch (e) { failed[id] = e.message; }
    }
    try {
      const names_ = Object.fromEntries(sent.map((id) => [id, names.get(`${loteId}:${id}`)]));
      const { errors: errs } = sent.length ? await api('POST', 'disenos/review', { loteId, itemIds: sent, decision, names: names_, libraryIds }) : { errors: {} };
      // A library entry the server then refused would be left orphaned: take it back out.
      for (const id of Object.keys(errs || {})) if (libraryIds[id]) await deleteEnemy(libraryIds[id]).catch(() => {});
      Object.assign(failed, errs);
    } catch (e) {
      for (const id of Object.keys(libraryIds)) await deleteEnemy(libraryIds[id]).catch(() => {});
      sent.forEach((id) => { failed[id] = e.message; });
    }
    itemIds.forEach((id) => { errors.delete(`${loteId}:${id}`); if (!failed[id]) selected.delete(`${loteId}:${id}`); });
    Object.entries(failed).forEach(([id, msg]) => errors.set(`${loteId}:${id}`, msg));
    const bad = Object.keys(failed).length;
    const ok = itemIds.length - bad;
    const verb = { accept: 'aceptado', discard: 'descartado', pending: 'devuelto a revisar' }[decision];
    toast(bad ? `${ok} ${verb}${ok === 1 ? '' : 's'}; ${bad} con error (míralo en la tarjeta)` : `${ok} ${verb}${ok === 1 ? '' : 's'}`);
    busy = false;
    dataText = '';
    await poll();
    return failed;
  }

  // ------------------------------------------------------------ previews

  /** What a preview draws: the preset, its params and when each lane plays (seconds, cycling). */
  const vfxCache = new Map();
  function vfxPlan(item) {
    const k = JSON.stringify(item.vfx);
    if (vfxCache.has(k)) return vfxCache.get(k);
    const preset = presetById(item.vfx?.preset);
    let plan = null;
    if (preset) {
      const p = normalizeParams(preset, item.vfx.params || {});
      let t = 0;
      const lanes = preset.lanes.map((lane) => {
        const one = frameCount(lane, p) / laneFps(lane, p);
        const dur = lane.loop ? Math.max(one * 2, 1.2) : one;
        const entry = { lane, start: t, one, dur };
        t += dur;
        return entry;
      });
      plan = { preset, p, lanes, total: t + 0.25 };
    }
    vfxCache.set(k, plan);
    return plan;
  }

  function drawVfx(canvas, item, now) {
    const plan = vfxPlan(item);
    const ctx = canvas.getContext('2d');
    if (!plan) { ctx.clearRect(0, 0, canvas.width, canvas.height); return; }
    const t = (now / 1000) % plan.total;
    const at = plan.lanes.find((l) => t >= l.start && t < l.start + l.dur);
    if (!at) { ctx.clearRect(0, 0, canvas.width, canvas.height); return; }
    const local = t - at.start;
    const k = at.lane.loop ? (local % at.one) / at.one : Math.min(0.999, local / at.one);
    drawFrame(ctx, plan.preset, at.lane, k, plan.p, 0, 0, canvas.width, createCanvas);
  }

  // Cache key of a proposal's samples (renderSound / playSound cache by it). Not the final id: a preview may
  // differ from the baked clip in its noise grain only.
  const soundKey = (lote, item) => `design:${lote.id}:${item.id}:${nameOf(lote, item)}`;
  const soundSamples = (lote, item) => renderSound(soundKey(lote, item), item.sound) || null;

  function drawWave(canvas, samples) {
    const ctx = canvas.getContext('2d');
    const { width: w, height: h } = canvas;
    ctx.clearRect(0, 0, w, h);
    if (!samples) return;
    ctx.fillStyle = '#56e0b4';
    const step = samples.length / w;
    for (let x = 0; x < w; x++) {
      let peak = 0;
      for (let i = Math.floor(x * step); i < Math.floor((x + 1) * step); i++) peak = Math.max(peak, Math.abs(samples[i] || 0));
      const bar = Math.max(1, peak * h * 0.9);
      ctx.fillRect(x, (h - bar) / 2, 1, bar);
    }
  }

  function hear(lote, item) {
    stopSound();
    const id = soundKey(lote, item);
    const stop = playSound(id, { loop: !!item.sound?.loop, gain: item.sound?.loop ? 0.45 : 0.8, samples: soundSamples(lote, item) });
    const timer = setTimeout(stop, ((item.sound?.loop ? 2 : 1) * (item.sound?.seconds || 1)) * 1000 + 200);
    stopSound = () => { clearTimeout(timer); stop(); };
  }

  // One animation loop for every effect preview on screen (cards and the viewer); started at the end.
  function animate(now) {
    if (!root.closest('[hidden]') || !overlay.hidden) {
      document.querySelectorAll('canvas[data-vfx]').forEach((c) => {
        const found = findItem(c.dataset.vfx);
        if (found) drawVfx(c, found.item, now);
      });
    }
    requestAnimationFrame(animate);
  }

  function art(lote, item, big = false) {
    const key = keyOf(lote, item);
    if (item.kind === 'enemy') return item.image ? `<img src="${imgUrl(item.image)}" alt="${esc(item.name)}" loading="lazy">`
      : '<span class="enNoArt">🖼<br>Falta la imagen<br><small>clic aquí y Ctrl+V</small></span>';
    if (item.kind === 'sound') return `<canvas data-wave="${esc(key)}" width="${big ? 800 : 256}" height="${big ? 240 : 256}"></canvas>`;
    return `<canvas data-vfx="${esc(key)}" width="${big ? 512 : 256}" height="${big ? 512 : 256}"></canvas>`;
  }

  function paintWaves(scope) {
    scope.querySelectorAll('canvas[data-wave]').forEach((c) => {
      const found = findItem(c.dataset.wave);
      if (found) drawWave(c, soundSamples(found.lote, found.item));
    });
  }

  // ------------------------------------------------------------ render

  function group(key, title, count, body, { actions = '', sub = '', defaultOpen = true } = {}) {
    const isOpen = closed.has(key) ? false : closed.has(`+${key}`) ? true : defaultOpen;
    return `<details class="enGroup" data-key="${esc(key)}" ${isOpen ? 'open' : ''}>
      <summary><span class="enGroupTitle">${esc(title)}</span><span class="enCount">${count}</span>${sub ? `<span class="enGroupSub">${esc(sub)}</span>` : ''}</summary>
      ${actions ? `<div class="enGroupBar">${actions}</div>` : ''}
      ${body}
    </details>`;
  }

  function itemCard(lote, item) {
    const key = keyOf(lote, item);
    const name = nameOf(lote, item);
    const err = errors.get(key);
    const sel = selected.has(key);
    let foot;
    if (item.status === 'pending') {
      foot = `<input class="enName" data-name="${esc(key)}" value="${esc(name)}" aria-label="Nombre" spellcheck="false" autocomplete="off">
        <div class="enBtns">${item.kind === 'enemy' ? `<label class="small enImgBtn" title="${item.image ? 'Cambiar la imagen' : 'Elegir la imagen'} (o pégala con Ctrl+V sobre la tarjeta)">🖼<input type="file" accept="image/*" hidden data-card-file="${esc(key)}"></label>` : ''}<button class="small enOk" data-act="accept" data-key="${esc(key)}" title="${item.kind === 'enemy' && !item.image ? 'Falta la imagen' : 'Aceptar'}" ${item.kind === 'enemy' && !item.image ? 'disabled' : ''}>✓ Aceptar</button>
        <button class="small" data-act="discard" data-key="${esc(key)}" title="Descartar">✕</button></div>`;
    } else {
      foot = `<div class="enName fixed">${esc(item.status === 'accepted' ? item.name : name)}</div>
        <div class="enBtns"><span class="enBadge ${item.status === 'accepted' ? 'ok' : ''}">${item.status === 'accepted' ? KIND[item.kind].accepted : 'Descartado'}</span>
        <button class="small" data-act="undo" data-key="${esc(key)}" title="${item.status === 'accepted' ? 'Deshacer: lo vuelve a sacar' : 'Recuperar'}">↶</button></div>`;
    }
    const corner = item.kind === 'sound'
      ? `<button class="enZoom small" data-act="hear" data-key="${esc(key)}" title="Escuchar" aria-label="Escuchar">▶</button>`
      : `<button class="enZoom small" data-act="view" data-key="${esc(key)}" title="Ver en grande" aria-label="Ver en grande">⤢</button>`;
    return `<div class="enCard${sel ? ' selected' : ''}" data-status="${item.status}" data-kind="${item.kind}" data-key="${esc(key)}">
      <button class="enArt" data-act="${item.status === 'pending' ? 'select' : item.kind === 'sound' ? 'hear' : 'view'}" data-key="${esc(key)}"
        title="${item.status === 'pending' ? 'Clic: seleccionar' : ''}">${art(lote, item)}</button>
      ${corner}<span class="enKind" title="${KIND[item.kind].label}">${KIND[item.kind].icon}</span>
      ${item.note ? `<div class="enNote">${esc(item.note)}</div>` : ''}
      ${item.kind === 'enemy' && item.prompt ? `<details class="enPrompt" ${item.image ? '' : 'open'}><summary>Prompt para Gemini</summary>
        <textarea readonly rows="6" aria-label="Prompt">${esc(item.prompt)}</textarea>
        <div class="enBtns"><button class="small enOk" data-act="copy-prompt" data-key="${esc(key)}" title="Copia el prompt">📋 Copiar prompt</button>
        <a class="small" href="${GEMINI_URL}" target="_blank" rel="noopener" title="Abre la app web de Gemini">↗ Abrir Gemini</a></div></details>` : ''}
      ${foot}
      ${err ? `<div class="enErr">${esc(err)}</div>` : ''}
    </div>`;
  }

  function loteGroup(lote, defaultOpen) {
    const items = lote.items.filter(shown);
    const pend = items.filter((i) => i.status === 'pending');
    const selHere = pend.filter((i) => selected.has(keyOf(lote, i))).length;
    const accepted = items.filter((i) => i.status === 'accepted').length;
    const actions = pend.length ? `
      ${selHere ? `<span class="enSel">${selHere} seleccionado${selHere === 1 ? '' : 's'}</span>
        <button class="small primary" data-act="accept-sel" data-lote="${esc(lote.id)}">✓ Aceptar seleccionados</button>
        <button class="small" data-act="discard-sel" data-lote="${esc(lote.id)}">✕ Descartar seleccionados</button>
        <button class="small" data-act="clear-sel" data-lote="${esc(lote.id)}">Quitar selección</button><span class="enSep"></span>` : ''}
      <button class="small" data-act="accept-all" data-lote="${esc(lote.id)}">✓ Aceptar todos (${pend.length})</button>
      <button class="small danger" data-act="discard-all" data-lote="${esc(lote.id)}">✕ Descartar todos (${pend.length})</button>` : '';
    const kinds = [...new Set((items.length ? items : [{ kind: lote.kind || 'enemy' }]).map((i) => i.kind))].map((k) => KIND[k]?.icon || '').join(' ');
    const tasks = (lote.taskIds || []).map(taskOf).filter(Boolean);
    const working = tasks.filter(active).length;
    const failed = tasks.filter((t) => /failed|cancelled|interrupted/.test(t.status)).length;
    const progress = tasks.length > 1 || working
      ? `${working ? '⏳ generándose · ' : ''}${tasks.filter((t) => t.status === 'done').length}/${tasks.length} tarea${tasks.length === 1 ? '' : 's'} hecha${tasks.length === 1 ? '' : 's'}` : '';
    const count = working && !items.length ? 'generándose…'
      : pend.length ? `${pend.length} por revisar de ${items.length}` : `${accepted} aceptado${accepted === 1 ? '' : 's'} de ${items.length}`;
    const body = items.length ? `<div class="enCards">${items.map((i) => itemCard(lote, i)).join('')}</div>`
      : `<p class="enEmpty">${working ? 'La tarea está trabajando: sus propuestas aparecerán aquí al terminar (el progreso, arriba en 🤖 Claude).'
        : failed ? 'La tarea no dejó propuestas: mira su informe en 🤖 Claude o en el 🗂 Historial.' : 'Sin propuestas.'}</p>`;
    return group(`lote:${lote.id}`, `${kinds} ${lote.title || lote.id}`, count, body, {
      actions,
      sub: [progress, failed ? `✗ ${failed} fallida${failed === 1 ? '' : 's'}` : '', lote.request && lote.request !== lote.title ? lote.request : '', when(lote.createdAt)].filter(Boolean).join(' · '),
      defaultOpen,
    });
  }

  // ------------------------------------------------------------ the request form

  function askForm() {
    const opt = (list, value) => list.map(([v, l]) => `<option value="${v}" ${v === value ? 'selected' : ''}>${l}</option>`).join('');
    const zonas = data?.zonas || [];
    if (!zonas.some((z) => z.id === ask.zone)) ask.zone = zonas[0]?.id || '';
    const tipos = data?.tipos || { body: {}, attack: {} };
    if (!tipos.body[ask.body]) ask.body = Object.keys(tipos.body)[0] || '';
    if (!tipos.attack[ask.attack]) ask.attack = Object.keys(tipos.attack)[0] || '';
    return group('ask', '＋ Pedir propuestas', KIND[ask.kind].label.toLowerCase(), `
      <form class="enAsk" data-form="ask">
        <div class="enFilter">${Object.entries(KIND).map(([k, v]) => `<button type="button" class="small${ask.kind === k ? ' selected' : ''}" data-act="ask-kind" data-kind="${k}">${v.icon} ${v.label}</button>`).join('')}</div>
        <label class="enLabel">Qué quieres <span class="efHint">solo las condiciones: las reglas de arte del juego las pone la skill design-proposals</span></label>
        <textarea data-ask="request" rows="3" placeholder="${esc(ASK_HINT[ask.kind])}">${esc(ask.request)}</textarea>
        <div class="todoImages">
          ${ask.images.map((im) => `<span class="todoImg"><a href="/api/adjunto?id=${im.id}" target="_blank" rel="noopener" title="${esc(im.name)}"><img src="/api/adjunto?id=${im.id}" alt="${esc(im.name)}"></a>
            <button type="button" class="small" data-act="ask-img-del" data-id="${im.id}" aria-label="Quitar imagen">×</button></span>`).join('')}
          <label class="todoImgAdd small" title="Elegir imágenes de referencia (también puedes pegar con Ctrl+V)">🖼 Añadir imagen<input type="file" accept="image/*" multiple hidden data-ask-file></label>
          <span class="efHint">o pega una captura con Ctrl+V; Claude las mira antes de empezar</span>
        </div>
        <div class="enAskRow">
          <label>Cantidad <input type="number" data-ask="count" min="1" max="10" value="${ask.count}"></label>
          <label><input type="radio" name="askMode" data-ask="mode" value="one" ${ask.mode === 'one' ? 'checked' : ''}> Una tarea con todas</label>
          <label><input type="radio" name="askMode" data-ask="mode" value="each" ${ask.mode === 'each' ? 'checked' : ''}> Una tarea por propuesta</label>
          ${ask.kind === 'enemy' ? `<label title="Define la paleta, los cristales y el estilo del enemigo">Zona <select data-ask="zone">${opt(zonas.map((z) => [z.id, z.label]), ask.zone)}</select></label>
          <label title="Anatomía y piezas del enemigo: cada tipo trae SUS reglas (un cuadrúpedo no lleva brazos, un flotador no lleva piernas). Libre = el prompt no dicta nada del cuerpo">Cuerpo <select data-ask="body">${opt(Object.entries(tipos.body).map(([k, v]) => [k, v.label]), ask.body)}</select></label>
          <label title="Arma: es una pieza aparte que dispara el proyectil. Magia: el sprite es la criatura normal, sin arma (la parte que lanza se dibuja después). Cuerpo a cuerpo: sin arma">Ataque <select data-ask="attack">${opt(Object.entries(tipos.attack).map(([k, v]) => [k, v.label]), ask.attack)}</select></label>
          <label title="Prompt: te devuelve el texto para pegar en la app de Gemini y luego pegas la imagen en la tarjeta. ComfyUI: la tarea dibuja la imagen como hasta ahora">Cómo <select data-ask="generator">${opt(GENERATORS, ask.generator)}</select></label>` : ''}
          <label>Modelo <select data-ask="model">${opt(MODELS, ask.model)}</select></label>
          <label>Esfuerzo <select data-ask="effort">${opt(EFFORTS, ask.effort)}</select></label>
          <button type="submit" class="primary">▶ Pedir ${ask.count} ${ask.count === 1 ? KIND[ask.kind].one : `${KIND[ask.kind].label.toLowerCase()}`}</button>
        </div>
        <p class="efHint">Va a la cola: la tarea aparece arriba, en 🤖 Claude. "Una tarea por propuesta" = conversaciones más cortas; compara el coste
          en el 🗂 Historial (ordena por tokens). <b>Prompt</b> (enemigos): la tarea solo escribe el texto —no necesita ComfyUI—;
          cópialo en Gemini y pega la imagen que te dé en su tarjeta (clic en la tarjeta y Ctrl+V). Las zonas (solo paleta) se editan en <code>docs/zonas.json</code> y los tipos de cuerpo / ataque en <code>docs/tipos-enemigo.json</code>.</p>
      </form>`, { defaultOpen: true });
  }

  // ------------------------------------------------------------ the enemy library

  const enemyImage = (e) => e.imagen || (e.cuerpo ? `docs/enemigos/${e.name}/cuerpo.png` : '') || e.diseno?.image || '';
  const openPendientes = (e) => e.pendientes.filter((p) => p.estado !== 'hecho');

  function taskLine(e) {
    const t = taskOf(e.tarea);
    if (!t || t.status === 'done') return '';
    return `<div class="enTask ${active(t) ? '' : 'bad'}">${TASK_STATE[t.status] || t.status}${active(t) ? '' : ' · mira el informe en 🤖 Claude'}</div>`;
  }

  function enemyButtons(e) {
    const t = taskOf(e.tarea);
    const n = openPendientes(e).filter((p) => p.estado === 'abierto').length;
    const name = esc(e.name);
    switch (e.estado) {
      case 'por-montar': return `<button class="small enOk" data-act="sheet" data-name="${name}" title="Abre su ficha: instrucciones extra (opcional) y elige montarlo entero o por pasos">▶ Montar…</button>`;
      case 'montando-cuerpo': return active(t) ? '' : `<button class="small" data-act="sheet" data-name="${name}" title="Abre su ficha para reintentarlo (con instrucciones si quieres)">▶ Reintentar…</button><button class="small" data-act="estado" data-estado="por-montar" data-name="${name}">↶</button>`;
      case 'cuerpo-por-aprobar': return `<button class="small enOk" data-act="sheet" data-name="${name}" title="Ver el cuerpo y aprobarlo">👁 Revisar cuerpo</button>`;
      case 'cuerpo-aprobado': return `<button class="small enOk" data-act="sheet" data-name="${name}" title="Elegir tipo, proyectiles, sonidos y efectos">▶ Paso 2</button>`;
      case 'montando': return active(t) ? '' : `<button class="small" data-act="sheet" data-name="${name}" title="Abre su ficha para reintentarlo (con instrucciones si quieres)">▶ Reintentar…</button><button class="small" data-act="estado" data-estado="por-montar" data-name="${name}">↶</button>`;
      case 'por-aprobar': return `<button class="small enOk" data-act="estado" data-estado="aprobado" data-name="${name}">✓ Aprobar</button>
        <button class="small" data-act="sheet" data-name="${name}" title="Apuntar lo que le falta">⚠ Pendientes</button>`;
      case 'con-pendientes': return n ? `<button class="small enOk" data-act="work" data-name="${name}" title="Una tarea con los pendientes abiertos">▶ Arreglar (${n})</button>`
        : `<button class="small" data-act="sheet" data-name="${name}">＋ Pendiente</button>`;
      case 'descartado': return `<button class="small" data-act="estado" data-estado="${e.prefab ? 'con-pendientes' : 'por-montar'}" data-name="${name}">↶ Recuperar</button>`;
      default: return `<button class="small" data-act="sheet" data-name="${name}">＋ Pendiente</button>`;
    }
  }

  function enemyCard(e) {
    const st = ESTADOS[e.estado] || { one: e.estado };
    const open = openPendientes(e);
    const tipos = Object.entries(open.reduce((acc, p) => ({ ...acc, [p.tipo]: (acc[p.tipo] || 0) + 1 }), {}));
    const img = enemyImage(e);
    return `<div class="enCard enEnemy" data-status="${esc(e.estado)}">
      <button class="enArt" data-act="sheet" data-name="${esc(e.name)}" title="Abrir su ficha">${img ? `<img src="${imgUrl(img)}" alt="${esc(e.name)}" loading="lazy">` : '<span class="enNoArt">👾<br>sin imagen</span>'}</button>
      <div class="enName fixed">${esc(e.name)}</div>
      <div class="enChips"><span class="enChip estado" data-estado="${esc(e.estado)}">${esc(st.one)}</span>${tipos.map(([t, k]) => `<span class="enChip">${esc(PENDING_TYPES[t] || t)}${k > 1 ? ` ×${k}` : ''}</span>`).join('')}</div>
      ${taskLine(e)}
      <div class="enBtns">${enemyButtons(e)}</div>
    </div>`;
  }

  function addExistingRow() {
    if (!adding) return '<div class="enGroupBar"><button class="small" data-act="add-open" title="Un enemigo que ya está en Unity y no salió de un diseño">＋ Enemigo existente</button><span class="efHint">o desde Unity: Tools ▸ RedMagic ▸ Enemigos ▸ Pendiente para la web…</span></div>';
    return `<div class="enGroupBar"><select data-add="prefab">${adding.length ? adding.map((p) => `<option value="${esc(p.prefab)}">${esc(p.name)} — ${esc(p.prefab.replace('Assets/Prefabs/Enemies/', ''))}</option>`).join('') : '<option value="">(todos están ya en la biblioteca)</option>'}</select>
      <button class="small primary" data-act="add-confirm" ${adding.length ? '' : 'disabled'}>Añadir</button><button class="small" data-act="add-cancel">Cancelar</button>
      <span class="efHint">su imagen la hace Unity (si está abierto)</span></div>`;
  }

  function filterBar() {
    const all = lotes().flatMap((l) => l.items);
    const pend = (k) => all.filter((i) => i.status === 'pending' && (k === 'all' || i.kind === k)).length;
    const chip = (k, label) => `<button class="small${filter === k ? ' selected' : ''}" data-act="filter" data-filter="${k}">${label}${pend(k) ? ` · ${pend(k)}` : ''}</button>`;
    return `<div class="enFilter">${chip('all', 'Todo')}${Object.entries(KIND).map(([k, v]) => chip(k, `${v.icon} ${v.label}`)).join('')}</div>`;
  }

  function render() {
    pendingRender = false;
    if (!data) return;
    const panel = root.closest('#todoPanel');
    const scroll = panel?.scrollTop;
    const loteKind = (l) => filter === 'all' || (l.kind || 'enemy') === filter;
    const visible = lotes().filter((l) => l.items.some(shown) || (!l.items.length && loteKind(l)));
    const running = (l) => (l.taskIds || []).some((id) => active(taskOf(id)));
    const toReview = visible.filter((l) => l.items.some((i) => shown(i) && i.status === 'pending') || running(l) || !l.items.length);
    const reviewed = visible.filter((l) => !toReview.includes(l));
    const enemigos = data.enemigos || [];

    const reviewHtml = toReview.length
      ? toReview.map((l) => loteGroup(l, true)).join('')
      : '<p class="enEmpty">Nada por revisar. Pide propuestas arriba: cuando la tarea termine, aparecen aquí.</p>';
    const byEstado = Object.keys(ESTADOS).concat([...new Set(enemigos.map((e) => e.estado))].filter((s) => !ESTADOS[s]))
      .map((s) => [s, enemigos.filter((e) => e.estado === s)]).filter(([, list]) => list.length);
    const libraryHtml = byEstado.length
      ? byEstado.map(([s, list]) => group(`estado:${s}`, ESTADOS[s]?.label || s, list.length,
        `<div class="enCards">${list.map(enemyCard).join('')}</div>`, { sub: ESTADOS[s]?.hint || '', defaultOpen: s !== 'descartado' && s !== 'aprobado' })).join('')
      : '<p class="enEmpty">Aún no hay enemigos: acepta el diseño de uno, o añade uno que ya exista.</p>';
    root.innerHTML = `<section class="enSection">${askForm()}</section>
      ${filterBar()}
      <section class="enSection"><h2>Por revisar <span class="enHint">al aceptar: un enemigo va a 👾 Enemigos (por montar); un proyectil o efecto, a 📚 Biblioteca; un sonido, a la paleta del juego</span></h2>${reviewHtml}</section>
      ${reviewed.length ? `<section class="enSection">${group('revisados', 'Lotes revisados', reviewed.length, reviewed.map((l) => loteGroup(l, false)).join(''), { defaultOpen: false })}</section>` : ''}`;
    enemiesRoot.innerHTML = `<section class="enSection"><h2>Biblioteca <span class="enHint">docs/enemigos/</span></h2>${addExistingRow()}${libraryHtml}</section>`;
    onBadges({
      disenos: lotes().reduce((n, l) => n + pendingOf(l).length, 0),
      enemigos: enemigos.filter((e) => ['por-aprobar', 'cuerpo-por-aprobar', 'cuerpo-aprobado'].includes(e.estado)).length,
    });
    paintWaves(root);
    if (panel && scroll != null) panel.scrollTop = scroll;
    if (viewer) renderViewer();
  }

  function when(iso) {
    if (!iso) return '';
    const d = new Date(iso);
    return Number.isNaN(+d) ? '' : d.toLocaleString('es', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  }

  // ------------------------------------------------------------ viewer (big preview, ← → move, A / D decide)

  const overlay = document.createElement('div');
  overlay.className = 'enViewer';
  overlay.hidden = true;
  document.body.appendChild(overlay);

  function openViewer(key) {
    const found = findItem(key);
    if (!found) return;
    const list = found.lote.items.filter(shown);
    viewer = { list: list.map((i) => keyOf(found.lote, i)), i: list.indexOf(found.item) };
    renderViewer();
  }

  /** Step 2 of "por pasos": the kind of enemy (or a template to inherit) and where each asset comes from. */
  function step2Form(e) {
    const opt = (list, value) => list.map(([v, l]) => `<option value="${v}" ${v === value ? 'selected' : ''}>${l}</option>`).join('');
    const f = p2Of(e.name);
    const heirs = (data?.enemigos || []).filter((x) => x.name !== e.name && x.prefab && ['por-aprobar', 'aprobado', 'con-pendientes'].includes(x.estado));
    const kinds = (data?.comportamientos || []).map((b) => `<option value="${esc(b.id)}" ${f.via === b.id ? 'selected' : ''}>${esc(b.label)}</option>`).join('');
    const heirOpts = heirs.map((x) => `<option value="plantilla:${esc(x.name)}" ${f.via === `plantilla:${x.name}` ? 'selected' : ''}>${esc(x.name)}</option>`).join('');
    if (!lib && !libLoading) loadLib();
    // Library groups: everything is in "General" for now; "Placeholder" is the first group to split off later.
    const GROUPS = [['general', 'General'], ['placeholder', 'Placeholder']];
    const picks = (key) => {
      if (f[key].origen !== 'biblioteca') return '';
      const list = lib?.[key];
      if (!list) return '<div class="efHint">Leyendo tu biblioteca…</div>';
      const groupOf = () => 'general';
      return GROUPS.map(([g, label]) => {
        const items = list.filter((x) => groupOf(x) === g);
        const marked = items.filter((x) => f[key].elegidos.some((c) => c.id === x.id)).length;
        const rows = items.map((x) => `<label><input type="checkbox" data-p2pick="${key}" data-id="${esc(x.id)}" data-name="${esc(x.name)}" ${f[key].elegidos.some((c) => c.id === x.id) ? 'checked' : ''}> ${esc(x.name)}</label>`).join('');
        return `<details class="enP2Group" data-group="${key}.${g}" ${openGroups.has(`${key}.${g}`) ? 'open' : ''}><summary>${label} <span class="efHint">${marked ? `${marked} marcados de ` : ''}${items.length}</span></summary>${rows ? `<div class="enP2Picks">${rows}</div>` : '<div class="efHint">Vacío.</div>'}</details>`;
      }).join('');
    };
    const asset = (key, label, ph) => `<div class="enP2Asset"><b>${label}</b>
        <div class="enP2Body">
          <select data-p2="${key}.origen"><option value="biblioteca" ${f[key].origen === 'biblioteca' ? 'selected' : ''}>De mi biblioteca</option><option value="nuevo" ${f[key].origen === 'nuevo' ? 'selected' : ''}>Que Claude lo cree nuevo</option></select>
          ${picks(key)}
          <textarea data-p2="${key}.texto" rows="2" placeholder="${esc(ph)}">${esc(f[key].texto)}</textarea>
        </div></div>`;
    return `<h3>Paso 2 · qué tipo de enemigo es</h3>
      <div class="enP2">
        <label class="efHint">Tipo <select data-p2="via"><optgroup label="Tipo de enemigo">${kinds}</optgroup>${heirs.length ? `<optgroup label="Heredar comportamiento y animación de…">${heirOpts}</optgroup>` : ''}</select></label>
        ${asset('proyectil', '🎯 Proyectil', 'Marca los de tu biblioteca (puedes varios) o descríbelos. Vacío: Claude decide. Con "nuevo": uno por línea.')}
        ${asset('sonidos', '🔊 Sonidos', 'Marca los de tu biblioteca (varios) o descríbelos, uno por línea. Vacío: Claude decide.')}
        ${asset('efectos', '✨ Efectos', 'Marca los de tu biblioteca (varios) o descríbelos, uno por línea (carga, disparo, impacto, estela…). Vacío: Claude decide.')}
        ${instrBox(e)}
        <div class="enBtns wrap">
          <label class="efHint">Modelo <select data-run="model">${opt(MODELS, run.model)}</select></label>
          <label class="efHint">Esfuerzo <select data-run="effort">${opt(EFFORTS, run.effort)}</select></label>
          <button class="small primary" data-v="build-rest">▶ Montar el resto</button>
        </div>
      </div>`;
  }

  const LAUNCH_HINT = { 'por-montar': 'van con el montaje completo o con el paso 1 (cuerpo)', 'montando-cuerpo': 'van con el reintento del cuerpo',
    'cuerpo-por-aprobar': 'van con ↻ Rehacer el cuerpo', 'cuerpo-aprobado': 'van con el paso 3 (o con ↻ Rehacer el cuerpo)', montando: 'van con el reintento' };
  /** Extra instructions for the next launch from this sheet, and the ones sent before (the later steps read them too). */
  function instrBox(e) {
    if (!LAUNCH_HINT[e.estado]) return '';
    const before = e.instrucciones || [];
    return `<div class="enInstr"><label class="enLabel">✍ Instrucciones extra para Claude <span class="efHint">opcional; ${LAUNCH_HINT[e.estado]}</span></label>
      <textarea data-instr rows="3" placeholder="Ej.: cambia el color del cañón a azul · al separar la cabeza, hazle 3 estados: dormida, despierta y modo ataque con los ojos rojos">${esc(instr[e.name] || '')}</textarea>
      ${before.length ? `<details class="enInstrOld"><summary>Enviadas antes (${before.length})</summary><ul>${before.map((i) => `<li><span class="enChip">${esc(PASO_LABEL[i.paso] || i.paso)}</span> ${esc(i.texto)}</li>`).join('')}</ul></details>` : ''}</div>`;
  }

  /** 🧩 How step 1 cuts the body: Claude decides, or exactly the pieces listed here (each with an optional note). */
  function piecesBox(e) {
    if (!CUT_ESTADOS.includes(e.estado) || active(taskOf(e.tarea))) return '';
    const f = pzOf(e);
    const presets = data?.piezas || [];
    const labelOf = (n) => presets.find((p) => p.id === n)?.label || '';
    const rows = f.lista.map((p, i) => `<li class="enPz">
        <div class="enPzHead"><input type="text" data-pz-name="${i}" value="${esc(p.nombre)}" list="enPzPresets" placeholder="Nombre (Head, Cannon, Wings…)" autocomplete="off">
          <span class="efHint">${esc(labelOf(p.nombre))}</span><button class="small" data-v="pz-del" data-i="${i}" title="Quitar" aria-label="Quitar">✕</button></div>
        <textarea data-pz-nota="${i}" rows="1" placeholder="Qué es y dónde acaba, si no es evidente (opcional). Ej.: el torso es el cañón entero, de la boca al engranaje">${esc(p.nota)}</textarea>
      </li>`).join('');
    const free = presets.filter((p) => !f.lista.some((x) => x.nombre === p.id));
    return `<div class="enPieces"><label class="enLabel">🧩 Piezas a separar <span class="efHint">van con el paso 1 o el montaje</span></label>
      <div class="enBtns wrap">
        <label><input type="radio" name="enPzMode" data-pz-mode="auto" ${f.modo !== 'manual' ? 'checked' : ''}> Que Claude decida</label>
        <label><input type="radio" name="enPzMode" data-pz-mode="manual" ${f.modo === 'manual' ? 'checked' : ''}> Las elijo yo</label>
      </div>
      ${f.modo === 'manual' ? `<p class="efHint">Solo se separan estas. Lo que no pongas va dentro de la pieza que lo contiene (brazos sin pieza propia = parte del torso): el cuerpo montado queda siempre igual que el diseño. Una sola pieza («Body») = no se corta.</p>
        ${rows ? `<ul class="enPzList">${rows}</ul>` : '<p class="enEmpty">Ninguna todavía.</p>'}
        <select data-pz-add><option value="">＋ Añadir pieza…</option>${free.map((p) => `<option value="${esc(p.id)}">${esc(p.label)} (${esc(p.id)})</option>`).join('')}<option value="__other">Otra (escribe su nombre)…</option></select>
        <datalist id="enPzPresets">${presets.map((p) => `<option value="${esc(p.id)}">${esc(p.label)}</option>`).join('')}</datalist>` : ''}
    </div>`;
  }

  /** 🎭 Part states (an angry face, an open palm…): extra sprites of one part, asked for at any step, finished enemies too. */
  function statesBox(e) {
    if (e.estado === 'descartado') return '';
    const v = (action, label, cls = '', extra = '') => `<button class="small ${cls}" data-v="${action}" ${extra}>${label}</button>`;
    const list = e.estados || [];
    const open = list.filter((s) => s.estado !== 'hecho');
    const busyTask = active(taskOf(e.tarea));
    const f = pzOf(e);
    const pieceNames = [...new Set([...(f.modo === 'manual' ? f.lista.map((p) => p.nombre) : []), ...(data?.piezas || []).map((p) => p.id).filter((id) => id !== 'Body')])].filter(Boolean);
    const rows = list.map((s) => `<li class="enPend" data-estado="${esc(s.estado)}">
        <span class="enChip">${esc(s.pieza)}</span>
        <div class="enPendText"><b>${esc(s.nombre)}</b>${s.texto ? ` — ${esc(s.texto)}` : ''}
          <span class="efHint">· ${s.frames > 1 ? `transición de ${s.frames} frames` : 'cambio directo'}${s.uso ? ` · ${esc(STATE_USES[s.uso] || s.uso)}` : ''}</span>
          ${s.resumen ? `<div class="efHint">✓ ${esc(s.resumen)}</div>` : ''}${s.nota ? `<div class="efHint">⚠ ${esc(s.nota)}</div>` : ''}</div>
        <span class="enPendState">${s.estado === 'en-curso' ? (TASK_STATE[taskOf(s.tarea)?.status] || 'en curso') : s.estado === 'hecho' ? '✓ hecho' : 'pedido'}</span>
        ${v('st-done', s.estado === 'hecho' ? '↶' : '✓', '', `data-id="${esc(s.id)}" title="${s.estado === 'hecho' ? 'Pedirlo otra vez' : 'Marcar hecho'}"`)}
        ${v('st-del', '🗑', '', `data-id="${esc(s.id)}" title="Borrar"`)}
      </li>`).join('');
    const uses = Object.entries(STATE_USES).map(([k, l]) => `<option value="${k}" ${stDraft.uso === k ? 'selected' : ''}>${esc(l)}</option>`).join('');
    return `<h3>🎭 Estados de piezas <span class="efHint">${e.prefab ? 'lánzalos aquí o van con el próximo paso' : 'van con el paso 1 o el montaje'}</span></h3>
      <p class="efHint">Un estado es un sprite extra de UNA pieza para las animaciones (cara enfadada, mano con la palma abierta…): el cuerpo original no cambia.</p>
      ${rows ? `<ul class="enPends">${rows}</ul>` : ''}
      <div class="enStAdd">
        <label class="efHint">Pieza <input type="text" data-st="pieza" value="${esc(stDraft.pieza)}" list="enStPieces" placeholder="Head" autocomplete="off"></label>
        <datalist id="enStPieces">${pieceNames.map((n) => `<option value="${esc(n)}"></option>`).join('')}</datalist>
        <label class="efHint">Estado <input type="text" data-st="nombre" value="${esc(stDraft.nombre)}" placeholder="enfadada" autocomplete="off"></label>
        <label class="efHint">Cuándo se ve <select data-st="uso">${uses}</select></label>
        <label class="efHint" title="1 = cambia de golpe. 2 o más = frames de transición desde la pieza normal hasta este estado (el último es el estado)">Frames <input type="number" data-st="frames" min="1" max="12" value="${esc(stDraft.frames)}"></label>
        <textarea data-st="texto" rows="2" placeholder="Cómo es (opcional). Ej.: ceño fruncido, ojos rojos entrecerrados, boca apretada">${esc(stDraft.texto)}</textarea>
        ${v('st-add', '＋ Pedir estado')}
      </div>
      ${e.prefab ? `<div class="enBtns wrap">${v('states', `▶ Hacer estados (${open.length})`, 'primary', open.length && !busyTask ? '' : 'disabled')}</div>` : ''}`;
  }

  /** An enemy's sheet: its picture, what it came from, its estado buttons and its pendientes. */
  function renderSheet(e) {
    const st = ESTADOS[e.estado] || { one: e.estado };
    const opt = (list, value) => list.map(([v, l]) => `<option value="${v}" ${v === value ? 'selected' : ''}>${l}</option>`).join('');
    const img = enemyImage(e);
    const open = e.pendientes.filter((p) => p.estado === 'abierto');
    const t = taskOf(e.tarea);
    const v = (action, label, cls = '', extra = '') => `<button class="small ${cls}" data-v="${action}" ${extra}>${label}</button>`;
    const estadoButtons = [
      e.estado === 'por-montar' ? v('build-body', '▶ Montar por pasos (cuerpo primero)', 'primary') + v('build', '▶ Montar completo (make-enemy)') : '',
      e.estado === 'montando-cuerpo' && !active(t) ? v('build-body', '▶ Reintentar el cuerpo') : '',
      e.estado === 'cuerpo-por-aprobar' ? v('estado', '✓ Aprobar cuerpo', 'primary', 'data-estado="cuerpo-aprobado"') + v('edit-body', '✎ Editar cuerpo', '', 'title="Lo abre en Enemy Studio, leído de Unity: ajusta tamaños, capas y distancias, guarda en Unity y vuelve aquí"') + v('build-body', '↻ Rehacer el cuerpo') : '',
      e.estado === 'cuerpo-aprobado' ? v('estado', '↶ Volver al paso 1', '', 'data-estado="cuerpo-por-aprobar" title="Vuelve a revisar el cuerpo"') + v('edit-body', '✎ Editar cuerpo') + v('build-body', '↻ Rehacer el cuerpo') : '',
      e.estado === 'montando' && !active(t) ? v('build', '▶ Reintentar el montaje') : '',
      e.estado === 'por-aprobar' ? v('estado', '✓ Aprobar', 'primary', 'data-estado="aprobado"') : '',
      ['por-aprobar', 'aprobado'].includes(e.estado) ? v('estado', '⚠ Tiene pendientes', '', 'data-estado="con-pendientes"') : '',
      e.estado === 'descartado' ? v('estado', '↶ Recuperar', '', `data-estado="${e.prefab ? 'con-pendientes' : 'por-montar'}"`) : v('estado', '✕ Descartar', 'danger', 'data-estado="descartado"'),
      e.prefab ? v('picture', '🖼 Rehacer imagen', '', 'title="Unity la vuelve a hacer desde el prefab"') : '',
    ].join('');
    const rows = e.pendientes.map((p) => `<li class="enPend" data-estado="${esc(p.estado)}">
        <span class="enChip">${esc(PENDING_TYPES[p.tipo] || p.tipo)}</span>
        <div class="enPendText">${esc(p.texto)}${p.resumen ? `<div class="efHint">✓ ${esc(p.resumen)}</div>` : ''}${p.nota ? `<div class="efHint">⚠ ${esc(p.nota)}</div>` : ''}</div>
        <span class="enPendState">${p.estado === 'en-curso' ? (TASK_STATE[taskOf(p.tarea)?.status] || 'en curso') : p.estado === 'hecho' ? '✓ hecho' : 'abierto'}</span>
        ${v('pend-done', p.estado === 'hecho' ? '↶' : '✓', '', `data-id="${esc(p.id)}" title="${p.estado === 'hecho' ? 'Reabrir' : 'Marcar hecho'}"`)}
        ${v('pend-del', '🗑', '', `data-id="${esc(p.id)}" title="Borrar"`)}
      </li>`).join('');
    overlay.innerHTML = `<div class="enViewerBox enSheet">
      <div class="enViewerHead"><b>${esc(e.name)}</b><span><span class="enChip estado" data-estado="${esc(e.estado)}">${esc(st.one)}</span> ${esc(ESTADOS[e.estado]?.hint || '')}</span>
        <button class="small" data-v="close" aria-label="Cerrar">✕</button></div>
      <div class="enSheetBody">
        <div class="enSheetArt">${img ? `<img src="${imgUrl(img)}" alt="">` : '<span class="enNoArt">👾<br>sin imagen</span>'}</div>
        <div class="enSheetInfo">
          ${e.prefab ? `<div><span class="efHint">Prefab</span> <code>${esc(e.prefab)}</code></div>` : ''}
          ${e.diseno?.request ? `<div><span class="efHint">Pedido</span> ${esc(e.diseno.request)}${e.diseno.note ? ` — ${esc(e.diseno.note)}` : ''}</div>` : ''}
          ${e.montaje?.resumen ? `<div><span class="efHint">Montaje</span> ${esc(e.montaje.resumen)}</div>` : ''}
          ${taskLine(e)}
          ${e.cuerpo?.resumen ? `<div><span class="efHint">Cuerpo</span> ${esc(e.cuerpo.resumen)}</div>` : ''}
          ${e.estado === 'cuerpo-por-aprobar' ? '<p class="efHint">Mira el cuerpo montado. Si las proporciones, distancias o capas no cuadran: ✎ Editar cuerpo (se abre en Enemy Studio; ajusta, 💾 Guardar en Unity y vuelves aquí; hace falta Unity abierto). Si hay que rehacerlo, apunta un Pendiente de tipo Sprite y pulsa Rehacer.</p>' : ''}
          ${e.estado === 'cuerpo-aprobado' ? step2Form(e) : instrBox(e)}
          ${piecesBox(e)}
          ${e.estado === 'por-montar' || ((e.estado === 'montando' || e.estado === 'montando-cuerpo') && !active(t)) ? `<div class="enBtns wrap">
            <label class="efHint">Modelo <select data-run="model">${opt(MODELS, run.model)}</select></label>
            <label class="efHint">Esfuerzo <select data-run="effort">${opt(EFFORTS, run.effort)}</select></label></div>` : ''}
          <div class="enBtns wrap">${estadoButtons}</div>
          ${statesBox(e)}
          <h3>Pendientes</h3>
          ${rows ? `<ul class="enPends">${rows}</ul>` : '<p class="enEmpty">Nada apuntado.</p>'}
          <div class="enPendAdd">
            <select data-draft="tipo">${opt(Object.entries(PENDING_TYPES), draft.tipo)}</select>
            <textarea data-draft="texto" rows="2" placeholder="Qué le falta o qué falla (ej.: el ataque de caparazón no rebota en las paredes)">${esc(draft.texto)}</textarea>
            ${v('pend-add', '＋ Apuntar')}
          </div>
          <div class="enBtns wrap">
            <label class="efHint">Modelo <select data-run="model">${opt(MODELS, run.model)}</select></label>
            <label class="efHint">Esfuerzo <select data-run="effort">${opt(EFFORTS, run.effort)}</select></label>
            ${v('work', `▶ Lanzar tarea con los abiertos (${open.length})`, 'primary', open.length ? '' : 'disabled')}
          </div>
        </div>
      </div>
    </div>`;
    overlay.hidden = false;
  }

  function renderViewer() {
    if (viewer.enemy) {
      const e = enemyByName(viewer.enemy);
      if (!e) { closeViewer(); return; }
      if (press.busy()) { press.after(renderViewer); return; }
      if (!typing(overlay)) renderSheet(e);
      if (focusPiece != null) { overlay.querySelector(`[data-pz-name="${focusPiece}"]`)?.focus(); focusPiece = null; }
      return;
    }
    const found = findItem(viewer.list[viewer.i]);
    if (!found) { closeViewer(); return; }
    const { lote, item } = found;
    const key = keyOf(lote, item);
    overlay.innerHTML = `<div class="enViewerBox">
      <div class="enViewerHead"><b>${KIND[item.kind].icon} ${esc(nameOf(lote, item))}</b><span>${viewer.i + 1} / ${viewer.list.length} · ${esc(lote.title || '')}</span>
        <button class="small" data-v="close" aria-label="Cerrar">✕</button></div>
      <div class="enViewerStage"><button class="enViewerNav" data-v="prev" aria-label="Anterior">‹</button>
        <div class="enViewerArt" data-kind="${item.kind}">${art(lote, item, true)}</div><button class="enViewerNav" data-v="next" aria-label="Siguiente">›</button></div>
      ${item.note ? `<p class="enNote">${esc(item.note)}</p>` : ''}
      <div class="enViewerBar">${item.kind === 'sound' ? '<button data-v="hear">▶ Escuchar <kbd>Espacio</kbd></button>' : ''}${item.status === 'pending'
        ? '<button class="primary" data-v="accept">✓ Aceptar <kbd>A</kbd></button><button data-v="discard">✕ Descartar <kbd>D</kbd></button>'
        : `<span class="enBadge ${item.status === 'accepted' ? 'ok' : ''}">${item.status === 'accepted' ? KIND[item.kind].accepted : 'Descartado'}</span>`}
        <span class="efHint">← → para moverte · Esc para cerrar</span></div>
      ${errors.get(key) ? `<div class="enErr">${esc(errors.get(key))}</div>` : ''}
    </div>`;
    overlay.hidden = false;
    paintWaves(overlay);
  }

  function closeViewer() { viewer = null; overlay.hidden = true; overlay.innerHTML = ''; stopSound(); }
  function step(d) {
    if (!viewer?.list) return;
    viewer.i = (viewer.i + d + viewer.list.length) % viewer.list.length;
    renderViewer();
    const found = findItem(viewer.list[viewer.i]);
    if (found?.item.kind === 'sound') hear(found.lote, found.item);
  }

  async function decideInViewer(decision) {
    if (!viewer?.list) return;
    const [loteId, itemId] = viewer.list[viewer.i].split(':');
    const failed = await review(loteId, [itemId], decision);
    if (viewer?.list && !failed?.[itemId]) step(1);
  }

  overlay.addEventListener('click', (ev) => {
    if (ev.target === overlay) { closeViewer(); return; }
    const v = ev.target.closest('[data-v]')?.dataset.v;
    if (v === 'close') closeViewer();
    else if (v === 'prev') step(-1);
    else if (v === 'next') step(1);
    else if (v === 'accept') decideInViewer('accept');
    else if (v === 'discard') decideInViewer('discard');
    else if (v === 'hear') { const f = findItem(viewer.list[viewer.i]); if (f) hear(f.lote, f.item); }
    else if (viewer?.enemy) sheetAction(v, ev.target.closest('[data-v]'));
  });

  /** The buttons of an enemy's sheet. */
  function sheetAction(v, btn) {
    const name = viewer.enemy;
    const upd = (body, msg) => post('enemigos/update', { name, ...body }, msg);
    // A launch carries the instructions typed in the sheet; they are cleared once the task is queued. One at a time:
    // a second click while the first is on its way would queue the task twice.
    const launch = (route, body, msg) => {
      if (launching) return;
      launching = true;
      btn?.setAttribute('disabled', '');
      post(route, { name, ...body, ...run, instrucciones: instr[name] || '' }, msg)
        .then((ok) => { if (ok) { delete instr[name]; delete pz[name]; renderViewer(); } })
        .finally(() => { launching = false; btn?.removeAttribute('disabled'); });
    };
    const piezas = () => { const e = enemyByName(name); return e ? pzOf(e) : undefined; };
    if (v === 'build') launch('enemigos/build', { piezas: piezas() }, `${name}: a la cola (make-enemy)`);
    else if (v === 'edit-body') {
      const e = enemyByName(name);
      if (e?.prefab && onEditBody) { closeViewer(); onEditBody(e); }
    }
    else if (v === 'build-body') launch('enemigos/build-body', { piezas: piezas() }, `${name}: a la cola (paso 1, cuerpo)`);
    else if (v === 'states') launch('enemigos/states', {}, `${name}: a la cola (estados de piezas)`);
    else if (v === 'pz-del') { pzOf(enemyByName(name)).lista.splice(Number(btn.dataset.i), 1); renderViewer(); }
    else if (v === 'st-add') {
      if (!stDraft.pieza.trim() || !stDraft.nombre.trim()) { toast('Pon la pieza y el nombre del estado'); return; }
      upd({ op: 'add-estado', ...stDraft }, 'Estado pedido').then((ok) => { if (ok) { Object.assign(stDraft, { nombre: '', texto: '', frames: 1 }); renderViewer(); } });
    } else if (v === 'st-done') {
      const s = (enemyByName(name)?.estados || []).find((x) => x.id === btn.dataset.id);
      if (s) upd({ op: 'edit-estado', id: s.id, estado: s.estado === 'hecho' ? 'abierto' : 'hecho' });
    } else if (v === 'st-del') upd({ op: 'remove-estado', id: btn.dataset.id }, 'Estado borrado');
    else if (v === 'build-rest') {
      const f = p2Of(name);
      const paso2 = { proyectil: f.proyectil, sonidos: f.sonidos, efectos: f.efectos, notas: f.notas, ...(f.via.startsWith('plantilla:') ? { plantilla: f.via.slice(10) } : { tipo: f.via }) };
      launch('enemigos/build-rest', { paso2 }, `${name}: a la cola (paso 3, el resto)`);
    }
    else if (v === 'estado') {
      if (btn.dataset.estado === 'cuerpo-aprobado' && enemyByName(name)?.prefab) post('enemigos/picture', { name });   // the picture of what is in Unity now
      upd({ op: 'estado', estado: btn.dataset.estado }, `${name}: ${ESTADOS[btn.dataset.estado]?.one || btn.dataset.estado}`);
    }
    else if (v === 'picture') post('enemigos/picture', { name }, 'Unity está haciendo la imagen (unos segundos)');
    else if (v === 'pend-add') {
      if (!draft.texto.trim()) { toast('Escribe qué le falta'); return; }
      upd({ op: 'add-pendiente', tipo: draft.tipo, texto: draft.texto }, 'Pendiente apuntado').then((ok) => { if (ok) { draft.texto = ''; renderViewer(); } });
    } else if (v === 'pend-done') {
      const p = enemyByName(name)?.pendientes.find((x) => x.id === btn.dataset.id);
      if (p) upd({ op: 'edit-pendiente', id: p.id, estado: p.estado === 'hecho' ? 'abierto' : 'hecho' });
    } else if (v === 'pend-del') upd({ op: 'remove-pendiente', id: btn.dataset.id }, 'Pendiente borrado');
    else if (v === 'work') post('enemigos/work', { name, ...run }, `${name}: tarea a la cola con sus pendientes`);
  }

  overlay.addEventListener('input', (ev) => {
    const d = ev.target.dataset || {};
    if (d.draft) draft[d.draft] = ev.target.value;
    if (d.instr !== undefined && viewer?.enemy) instr[viewer.enemy] = ev.target.value;
    if (d.run) run[d.run] = ev.target.value;
    if (d.st) stDraft[d.st] = ev.target.value;
    if ((d.pzName || d.pzNota) && viewer?.enemy) {
      const row = pzOf(enemyByName(viewer.enemy)).lista[Number(d.pzName ?? d.pzNota)];
      if (row) row[d.pzName != null ? 'nombre' : 'nota'] = d.pzName != null ? ev.target.value.trim() : ev.target.value;
    }
    if (d.p2 && viewer?.enemy) {
      const f = p2Of(viewer.enemy);
      const [k, sub] = d.p2.split('.');
      if (sub) f[k][sub] = ev.target.value; else f[k] = ev.target.value;
    }
  });
  overlay.addEventListener('change', (ev) => {
    const d = ev.target.dataset || {};
    if ((d.pzMode || d.pzAdd !== undefined) && viewer?.enemy) {
      const f = pzOf(enemyByName(viewer.enemy));
      if (d.pzMode) f.modo = d.pzMode;
      else if (ev.target.value) {
        f.lista.push({ nombre: ev.target.value === '__other' ? '' : ev.target.value, nota: '' });
        if (ev.target.value === '__other') focusPiece = f.lista.length - 1;
      }
      ev.target.blur();   // a focused field holds the redraw back
      renderViewer();
      return;
    }
    if (!d.p2pick || !viewer?.enemy) return;
    const list = p2Of(viewer.enemy)[d.p2pick].elegidos;
    const i = list.findIndex((c) => c.id === d.id);
    if (ev.target.checked && i < 0) list.push({ id: d.id, name: d.name });
    else if (!ev.target.checked && i >= 0) list.splice(i, 1);
  });
  overlay.addEventListener('toggle', (ev) => {
    const g = ev.target.dataset?.group;
    if (g) { if (ev.target.open) openGroups.add(g); else openGroups.delete(g); }
  }, true);
  overlay.addEventListener('focusout', () => { setTimeout(() => { if (viewer?.enemy && !typing(overlay)) renderViewer(); }, 0); });
  document.addEventListener('keydown', (ev) => {
    if (overlay.hidden || ev.ctrlKey || ev.altKey || ev.metaKey) return;
    const k = ev.key.toLowerCase();
    if (viewer?.enemy) {   // a sheet: only Esc (out of a field first)
      if (k !== 'escape') return;
      if (typing(overlay)) document.activeElement.blur(); else closeViewer();
      ev.preventDefault();
      return;
    }
    if (k === 'escape') closeViewer();
    else if (k === 'arrowleft') step(-1);
    else if (k === 'arrowright') step(1);
    else if (k === 'a') decideInViewer('accept');
    else if (k === 'd') decideInViewer('discard');
    else if (k === ' ' && viewer?.list) { const f = findItem(viewer.list[viewer.i]); if (f?.item.kind === 'sound') hear(f.lote, f.item); }
    else return;
    ev.preventDefault();
    ev.stopPropagation();
  }, true);

  // ------------------------------------------------------------ events

  const on = (type, fn, capture) => roots.forEach((r) => r.addEventListener(type, fn, capture));

  on('click', (ev) => {
    const btn = ev.target.closest('[data-act]');
    if (!btn) return;
    const act = btn.dataset.act;
    const key = btn.dataset.key;
    const [loteId, itemId] = key ? key.split(':') : [btn.dataset.lote];
    const lote = lotes().find((l) => l.id === loteId);
    const pendIds = lote ? pendingOf(lote).filter(shown).map((i) => i.id) : [];
    const selIds = pendIds.filter((id) => selected.has(`${loteId}:${id}`));
    if (act === 'select') {
      // As in the Windows explorer: a click selects only that card, Ctrl+click adds / removes, Shift+click a range.
      const range = ev.shiftKey && anchor && anchor.split(':')[0] === loteId
        ? pendingOf(lote).filter(shown).map((i) => keyOf(lote, i)) : null;
      if (ev.ctrlKey || ev.metaKey) { if (selected.has(key)) selected.delete(key); else selected.add(key); anchor = key; }
      else if (range && range.includes(anchor) && range.includes(key)) {
        const [a, b] = [range.indexOf(anchor), range.indexOf(key)].sort((x, y) => x - y);
        selected.clear();
        range.slice(a, b + 1).forEach((k) => selected.add(k));
      } else { selected.clear(); selected.add(key); anchor = key; }
      const found = findItem(key);
      if (found?.item.kind === 'sound' && selected.has(key)) hear(found.lote, found.item);
      render();
    } else if (act === 'copy-prompt') {
      const f = findItem(key);
      if (f?.item.prompt) navigator.clipboard.writeText(f.item.prompt).then(() => toast('Prompt copiado: pégalo en Gemini'), () => toast('No pude copiar: selecciona el texto a mano'));
    } else if (act === 'view') openViewer(key);
    else if (act === 'hear') { const f = findItem(key); if (f) hear(f.lote, f.item); }
    else if (act === 'sheet') {
      viewer = { enemy: btn.dataset.name };
      renderViewer();
      overlay.querySelector('[data-instr]')?.focus({ preventScroll: true });
    }
    else if (act === 'work') post('enemigos/work', { name: btn.dataset.name, ...run }, `${btn.dataset.name}: tarea a la cola con sus pendientes`);
    else if (act === 'estado') post('enemigos/update', { name: btn.dataset.name, op: 'estado', estado: btn.dataset.estado }, `${btn.dataset.name}: ${ESTADOS[btn.dataset.estado]?.one || btn.dataset.estado}`);
    else if (act === 'add-open') api('GET', 'enemigos/prefabs').then((list) => { adding = list; render(); }).catch((e) => toast(e.message));
    else if (act === 'add-cancel') { adding = null; render(); }
    else if (act === 'add-confirm') {
      const prefab = enemiesRoot.querySelector('[data-add="prefab"]')?.value;
      if (prefab) post('enemigos/add', { prefab }, (e) => `${e.name} añadido: ábrelo para apuntar sus pendientes`).then((e) => { adding = null; if (e) { viewer = { enemy: e.name }; renderViewer(); } });
    }
    else if (act === 'ask-img-del') { ask.images = ask.images.filter((im) => im.id !== btn.dataset.id); render(); }
    else if (act === 'ask-kind') { ask.kind = btn.dataset.kind; render(); }
    else if (act === 'filter') { filter = btn.dataset.filter; sessionStorage.setItem(FILTER_KEY, filter); render(); }
    else if (act === 'accept') review(loteId, [itemId], 'accept');
    else if (act === 'discard') review(loteId, [itemId], 'discard');
    else if (act === 'undo') review(loteId, [itemId], 'pending');
    else if (act === 'accept-sel') review(loteId, selIds, 'accept');
    else if (act === 'discard-sel') review(loteId, selIds, 'discard');
    else if (act === 'clear-sel') { pendIds.forEach((id) => selected.delete(`${loteId}:${id}`)); anchor = null; render(); }
    else if (act === 'accept-all') review(loteId, pendIds, 'accept');
    else if (act === 'discard-all') review(loteId, pendIds, 'discard');
  });

  on('input', (ev) => {
    const key = ev.target.dataset?.name;
    if (key) names.set(key, ev.target.value);
    const field = ev.target.dataset?.ask;
    if (field) ask[field] = field === 'count' ? Math.max(1, Math.min(10, Number(ev.target.value) || 1)) : ev.target.value;
  });
  const IMG_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'image/gif'];
  const toBase64 = (file) => new Promise((resolve, reject) => {
    const r = new FileReader();
    r.onload = () => resolve(String(r.result).split(',')[1] || '');
    r.onerror = () => reject(new Error('no se pudo leer la imagen'));
    r.readAsDataURL(file);
  });
  /** Picked / pasted images go to the server now; the request carries their ids (tareas.md ▸ images). */
  async function attachImages(files) {
    let n = 0;
    for (const f of files) {
      if (!IMG_TYPES.includes(f.type)) { toast(`${f.name || 'Archivo'}: solo png, jpg, webp o gif`); continue; }
      if (f.size > 10e6) { toast(`${f.name || 'Imagen'}: pesa más de 10 MB`); continue; }
      try { ask.images.push(await api('POST', 'adjunto', { name: f.name || 'captura.png', type: f.type, data: await toBase64(f) })); n++; }
      catch (e) { toast(e.message); }
    }
    if (n) { render(); toast(n === 1 ? 'Imagen adjunta' : `${n} imágenes adjuntas`); }
  }
  /** A picture for a proposal's card (the one Gemini drew, or a replacement): the server keeps it in docs/disenos/img/. */
  async function attachCardImage(key, file) {
    if (!IMG_TYPES.includes(file.type)) { toast('Solo png, jpg, webp o gif'); return null; }
    if (file.size > 10e6) { toast('La imagen pesa más de 10 MB'); return null; }
    const [loteId, itemId] = key.split(':');
    try {
      return await post('disenos/imagen', { loteId, itemId, type: file.type, data: await toBase64(file) }, 'Imagen puesta en la tarjeta');
    } catch (e) { toast(e.message); return null; }
  }

  /**
   * The image a paste carries, read at once (the clipboard is gone after the event): a file (Copy image, a
   * screenshot — in `files` or only in `items`), else a link when the page copied its <img> (Ctrl+C on a selection).
   */
  function clipboardImage(dt) {
    const files = [...(dt?.files || [])].filter((f) => IMG_TYPES.includes(f.type));
    if (files.length) return { files };
    for (const it of dt?.items || []) {
      if (it.kind === 'file' && IMG_TYPES.includes(it.type)) { const f = it.getAsFile(); if (f) return { files: [f] }; }
    }
    const src = (dt?.getData?.('text/html') || '').match(/<img[^>]+src=["']([^"']+)["']/i)?.[1]?.replace(/&amp;/g, '&');
    if (src && /^(https:|data:image\/)/.test(src)) return { src };
    return null;
  }

  const pendingEnemy = (k) => { const f = findItem(k); return f?.item.kind === 'enemy' && f.item.status === 'pending'; };
  /** Which card a Ctrl+V goes to: the one the cursor is in, the one in the viewer, the selected one, the one under the mouse. */
  function pasteTarget(ev) {
    const inCard = ev.target.closest?.('.enCard[data-kind="enemy"][data-status="pending"]')?.dataset.key;
    if (inCard) return inCard;
    if (viewer?.list && !overlay.hidden && pendingEnemy(viewer.list[viewer.i])) return viewer.list[viewer.i];
    const sel = [...selected].filter(pendingEnemy);
    if (sel.length === 1) return sel[0];
    if (anchor && sel.includes(anchor)) return anchor;
    return hoverKey && pendingEnemy(hoverKey) ? hoverKey : null;
  }

  /** Puts the pasted picture on the card, then selects the next card of the batch still without one. */
  async function pasteInto(key, got) {
    const [loteId, itemId] = key.split(':');
    let file = got.files?.[0] || null;
    if (!file && got.src.startsWith('data:')) file = await fetch(got.src).then((r) => r.blob()).then((b) => new File([b], 'pegada', { type: b.type })).catch(() => null);
    const ok = file ? await attachCardImage(key, file)
      : await post('disenos/imagen', { loteId, itemId, url: got.src }, 'Imagen puesta en la tarjeta');
    if (!ok) return;
    const lote = lotes().find((l) => l.id === loteId);
    const list = lote ? pendingOf(lote).filter((i) => i.kind === 'enemy' && shown(i)) : [];
    const next = list.slice(list.findIndex((i) => i.id === itemId) + 1).find((i) => !i.image);
    selected.clear();
    anchor = next ? keyOf(lote, next) : null;
    if (anchor) selected.add(anchor);
    if (viewer?.list) renderViewer();
    render();
  }

  document.addEventListener('paste', (ev) => {
    if (root.closest('[hidden]') && overlay.hidden) return;
    const got = clipboardImage(ev.clipboardData);
    if (!got) return;                                         // plain text: the field takes it
    if (ev.target.closest?.('[data-form="ask"]')) { if (got.files) { ev.preventDefault(); attachImages(got.files); } return; }
    if (ev.target.closest?.('#todoDetail, #todoQuick')) return;   // todo-panel's own fields take their images
    if (!got.files && /^(INPUT|TEXTAREA)$/.test(ev.target.tagName)) return;   // rich text into a field
    const key = pasteTarget(ev);
    if (!key) {
      if (!root.closest('[hidden]') && [...selected].length > 1) toast('Hay varias seleccionadas: clic en la tarjeta donde va la imagen y Ctrl+V');
      return;
    }
    ev.preventDefault();
    pasteInto(key, got);
  });
  on('mouseover', (ev) => { hoverKey = ev.target.closest?.('.enCard[data-kind="enemy"][data-status="pending"]')?.dataset.key || null; });
  on('mouseleave', () => { hoverKey = null; });
  on('mousedown', (ev) => { if (ev.shiftKey && ev.target.closest('[data-act="select"]')) ev.preventDefault(); });   // no text selection on Shift+click
  on('submit', async (ev) => {
    if (ev.target.dataset.form !== 'ask') return;
    ev.preventDefault();
    if (!ask.request.trim()) { toast('Escribe qué quieres'); return; }
    document.activeElement?.blur?.();
    const out = await post('disenos/request', { ...ask }, (o) => (o.errors?.length ? o.errors.join(' · ')
      : `${o.taskIds.length} tarea${o.taskIds.length === 1 ? '' : 's'} en la cola: las propuestas aparecerán en "Por revisar"`));
    if (out) { ask.request = ''; ask.images = []; render(); }
  });
  on('change', (ev) => {
    if (ev.target.dataset?.cardFile) { const f = ev.target.files[0]; const k = ev.target.dataset.cardFile; ev.target.value = ''; if (f) attachCardImage(k, f); return; }
    if (ev.target.dataset?.askFile !== undefined) { const files = [...ev.target.files]; ev.target.value = ''; attachImages(files); return; }
    const key = ev.target.dataset?.name;
    const found = key && findItem(key);
    if (found) { const t = tidyName(found.item.kind, ev.target.value); names.set(key, t); ev.target.value = t; }
  });
  on('focusout', () => { setTimeout(() => { if (pendingRender && !focusedHere()) render(); }, 0); });
  on('keydown', (ev) => {
    if (ev.key === 'Escape' && selected.size && !/^(INPUT|TEXTAREA|SELECT)$/.test(ev.target.tagName)) { selected.clear(); anchor = null; render(); return; }
    if (ev.key === 'Enter' && ev.target.dataset?.name) {
      const [loteId, itemId] = ev.target.dataset.name.split(':');
      names.set(ev.target.dataset.name, ev.target.value);
      ev.target.blur();
      review(loteId, [itemId], 'accept');
    }
  });
  on('toggle', (ev) => {
    const d = ev.target;
    if (!d.matches?.('details.enGroup')) return;
    const k = d.dataset.key;
    closed.delete(k); closed.delete(`+${k}`);
    closed.add(d.open ? `+${k}` : k);
    sessionStorage.setItem(CLOSED_KEY, JSON.stringify([...closed]));
  }, true);

  requestAnimationFrame(animate);

  return {
    onShow: () => { dataText = ''; poll(); },
    /** Back from Enemy Studio: the enemy's sheet again, with its picture redone from the saved prefab. */
    reopenSheet: (name) => { viewer = { enemy: name }; renderViewer(); },
    refreshPicture: (name) => post('enemigos/picture', { name }),
  };
}
