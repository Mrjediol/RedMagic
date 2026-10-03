// modules/designs-panel.js
// -----------------------------------------------------------------------------
// 🎨 Designs: review what a task proposed — enemies, projectiles, effects, sounds — and the enemy library.
//
// The batches are docs/disenos/<id>.json in the repo and go through tools/taller-server (disenos.mjs), so
// this tab only works with the web served by start.cmd. Full design: docs/architecture/designs.md.
//   · Por revisar — one foldable group per batch. Click a card to select it; accept / discard one, the
//     selection, or all that are left; ↶ undoes. Each kind previews as itself: the enemy's image, the effect
//     playing live (vfx-presets.js), the sound on ▶ (vfx-sounds.js).
//   · Accepting: an enemy → the enemy library (docs/enemigos/<Name>/, "por montar"); a sound → the game's
//     palette (server); a projectile / effect → baked HERE into the 📚 Biblioteca (IndexedDB, the same record
//     the VFX Lab saves), then the server records its libraryId.
//   · Biblioteca de enemigos — the accepted enemies, folded by estado. · Lotes revisados — folded.

import { presetById, normalizeParams, bakePreset, drawFrame, frameCount, laneFps } from './vfx-presets.js';
import { saveEnemy, deleteEnemy, listEntries, buildThumbnail } from './enemy-library.js';
import { cropBox } from './export-manifest.js';
import { renderSound } from './vfx-sounds.js';
import { playSound } from './vfx-stage.js';

const CLOSED_KEY = 'redmagic.designs.closed';
const FILTER_KEY = 'redmagic.designs.filter';
const POLL_MS = 5000;

const KIND = {
  enemy: { icon: '👾', label: 'Enemigos', one: 'enemigo', accepted: '✓ En la biblioteca de enemigos' },
  projectile: { icon: '🔥', label: 'Proyectiles', one: 'proyectil', accepted: '✓ En 📚 Biblioteca' },
  fx: { icon: '✨', label: 'Efectos VFX', one: 'efecto', accepted: '✓ En 📚 Biblioteca' },
  sound: { icon: '🔊', label: 'Sonidos', one: 'sonido', accepted: '✓ En la paleta de sonidos' },
};
const ESTADOS = {
  'por-montar': { label: 'Por montar', hint: 'diseño aprobado; falta montarlo en Unity (make-enemy)' },
  montando: { label: 'Montándose', hint: 'una tarea lo está montando' },
  'por-aprobar': { label: 'Esperando tu aprobación', hint: 'montado en Unity; pruébalo' },
  'con-pendientes': { label: 'Con pendientes', hint: 'existe, pero le faltan cosas' },
  aprobado: { label: 'Aprobados', hint: 'terminados' },
  descartado: { label: 'Descartados', hint: '' },
};

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

export function initDesignsPanel({ root, statusEl, toast }) {
  let token = null;
  let data = null, dataText = '';
  let pollTimer = null, pendingRender = false, busy = false;
  let filter = sessionStorage.getItem(FILTER_KEY) || 'all';
  const selected = new Set();               // "loteId:itemId"
  const names = new Map();                  // "loteId:itemId" → name typed in the card
  const errors = new Map();                 // "loteId:itemId" → last error
  const closed = new Set(JSON.parse(sessionStorage.getItem(CLOSED_KEY) || '[]'));
  let viewer = null;                        // { list: [key…], i } | { enemy }
  let stopSound = () => {};

  const setStatus = (t) => { statusEl.textContent = t; };
  const keyOf = (lote, item) => `${lote.id}:${item.id}`;
  const nameOf = (lote, item) => names.get(keyOf(lote, item)) ?? item.name;
  const focusedHere = () => root.contains(document.activeElement) && document.activeElement.tagName === 'INPUT';
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
    if (item.kind === 'enemy') return `<img src="${imgUrl(item.image)}" alt="${esc(item.name)}" loading="lazy">`;
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
        <div class="enBtns"><button class="small enOk" data-act="accept" data-key="${esc(key)}" title="Aceptar">✓ Aceptar</button>
        <button class="small" data-act="discard" data-key="${esc(key)}" title="Descartar">✕</button></div>`;
    } else {
      foot = `<div class="enName fixed">${esc(item.status === 'accepted' ? item.name : name)}</div>
        <div class="enBtns"><span class="enBadge ${item.status === 'accepted' ? 'ok' : ''}">${item.status === 'accepted' ? KIND[item.kind].accepted : 'Descartado'}</span>
        <button class="small" data-act="undo" data-key="${esc(key)}" title="${item.status === 'accepted' ? 'Deshacer: lo vuelve a sacar' : 'Recuperar'}">↶</button></div>`;
    }
    const corner = item.kind === 'sound'
      ? `<button class="enZoom small" data-act="hear" data-key="${esc(key)}" title="Escuchar" aria-label="Escuchar">▶</button>`
      : `<button class="enZoom small" data-act="view" data-key="${esc(key)}" title="Ver en grande" aria-label="Ver en grande">⤢</button>`;
    return `<div class="enCard${sel ? ' selected' : ''}" data-status="${item.status}" data-kind="${item.kind}">
      <button class="enArt" data-act="${item.status === 'pending' ? 'select' : item.kind === 'sound' ? 'hear' : 'view'}" data-key="${esc(key)}"
        title="${item.status === 'pending' ? 'Clic: seleccionar' : ''}">${art(lote, item)}</button>
      ${corner}<span class="enKind" title="${KIND[item.kind].label}">${KIND[item.kind].icon}</span>
      ${item.note ? `<div class="enNote">${esc(item.note)}</div>` : ''}
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
    const kinds = [...new Set(items.map((i) => i.kind))].map((k) => KIND[k].icon).join(' ');
    const count = pend.length ? `${pend.length} por revisar de ${items.length}` : `${accepted} aceptado${accepted === 1 ? '' : 's'} de ${items.length}`;
    return group(`lote:${lote.id}`, `${kinds} ${lote.title || lote.id}`, count,
      `<div class="enCards">${items.map((i) => itemCard(lote, i)).join('')}</div>`,
      { actions, sub: [lote.request && lote.request !== lote.title ? lote.request : '', when(lote.createdAt)].filter(Boolean).join(' · '), defaultOpen });
  }

  function enemyCard(e) {
    const st = ESTADOS[e.estado] || { label: e.estado };
    return `<div class="enCard" data-status="${esc(e.estado)}">
      <button class="enArt" data-act="view-enemy" data-name="${esc(e.name)}" title="Ver en grande"><img src="${imgUrl(e.diseno?.image || '')}" alt="${esc(e.name)}" loading="lazy"></button>
      <div class="enName fixed">${esc(e.name)}</div>
      ${e.diseno?.request ? `<div class="enNote">${esc(e.diseno.request)}</div>` : ''}
      <div class="enBtns"><span class="enBadge">${esc(st.label)}</span></div>
    </div>`;
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
    const panel = root.closest('#designsPanel');
    const scroll = panel?.scrollTop;
    const visible = lotes().filter((l) => l.items.some(shown));
    const toReview = visible.filter((l) => l.items.some((i) => shown(i) && i.status === 'pending'));
    const reviewed = visible.filter((l) => !toReview.includes(l));
    const enemigos = data.enemigos || [];

    const reviewHtml = toReview.length
      ? toReview.map((l) => loteGroup(l, true)).join('')
      : '<p class="enEmpty">Nada por revisar. Cuando una tarea de diseño termine, sus propuestas aparecen aquí.</p>';
    const byEstado = Object.keys(ESTADOS).concat([...new Set(enemigos.map((e) => e.estado))].filter((s) => !ESTADOS[s]))
      .map((s) => [s, enemigos.filter((e) => e.estado === s)]).filter(([, list]) => list.length);
    const libraryHtml = byEstado.length
      ? byEstado.map(([s, list]) => group(`estado:${s}`, ESTADOS[s]?.label || s, list.length,
        `<div class="enCards">${list.map(enemyCard).join('')}</div>`, { sub: ESTADOS[s]?.hint || '', defaultOpen: s !== 'descartado' && s !== 'aprobado' })).join('')
      : '<p class="enEmpty">Aún no hay enemigos: acepta el diseño de uno para empezar.</p>';
    const showLibrary = filter === 'all' || filter === 'enemy';

    root.innerHTML = `${filterBar()}
      <section class="enSection"><h2>Por revisar</h2>${reviewHtml}</section>
      ${showLibrary ? `<section class="enSection"><h2>Biblioteca de enemigos <span class="enHint">docs/enemigos/ · proyectiles y efectos aceptados van a 📚 Biblioteca; sonidos, a la paleta del juego</span></h2>${libraryHtml}</section>` : ''}
      ${reviewed.length ? `<section class="enSection">${group('revisados', 'Lotes revisados', reviewed.length, reviewed.map((l) => loteGroup(l, false)).join(''), { defaultOpen: false })}</section>` : ''}`;
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

  function renderViewer() {
    if (viewer.enemy) {
      overlay.innerHTML = `<div class="enViewerBox"><div class="enViewerHead"><b>${esc(viewer.enemy.name)}</b><span>${esc(ESTADOS[viewer.enemy.estado]?.label || viewer.enemy.estado)}</span>
        <button class="small" data-v="close" aria-label="Cerrar">✕</button></div><div class="enViewerStage"><img src="${imgUrl(viewer.enemy.diseno?.image || '')}" alt=""></div></div>`;
      overlay.hidden = false;
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
  });
  document.addEventListener('keydown', (ev) => {
    if (overlay.hidden || ev.ctrlKey || ev.altKey || ev.metaKey) return;
    const k = ev.key.toLowerCase();
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

  root.addEventListener('click', (ev) => {
    const btn = ev.target.closest('[data-act]');
    if (!btn) return;
    const act = btn.dataset.act;
    const key = btn.dataset.key;
    const [loteId, itemId] = key ? key.split(':') : [btn.dataset.lote];
    const lote = lotes().find((l) => l.id === loteId);
    const pendIds = lote ? pendingOf(lote).filter(shown).map((i) => i.id) : [];
    const selIds = pendIds.filter((id) => selected.has(`${loteId}:${id}`));
    if (act === 'select') {
      if (selected.has(key)) selected.delete(key); else selected.add(key);
      const found = findItem(key);
      if (found?.item.kind === 'sound' && selected.has(key)) hear(found.lote, found.item);
      render();
    } else if (act === 'view') openViewer(key);
    else if (act === 'hear') { const f = findItem(key); if (f) hear(f.lote, f.item); }
    else if (act === 'view-enemy') { const e = (data?.enemigos || []).find((x) => x.name === btn.dataset.name); if (e) { viewer = { enemy: e }; renderViewer(); } }
    else if (act === 'filter') { filter = btn.dataset.filter; sessionStorage.setItem(FILTER_KEY, filter); render(); }
    else if (act === 'accept') review(loteId, [itemId], 'accept');
    else if (act === 'discard') review(loteId, [itemId], 'discard');
    else if (act === 'undo') review(loteId, [itemId], 'pending');
    else if (act === 'accept-sel') review(loteId, selIds, 'accept');
    else if (act === 'discard-sel') review(loteId, selIds, 'discard');
    else if (act === 'clear-sel') { pendIds.forEach((id) => selected.delete(`${loteId}:${id}`)); render(); }
    else if (act === 'accept-all') review(loteId, pendIds, 'accept');
    else if (act === 'discard-all') review(loteId, pendIds, 'discard');
  });

  root.addEventListener('input', (ev) => {
    const key = ev.target.dataset?.name;
    if (key) names.set(key, ev.target.value);
  });
  root.addEventListener('change', (ev) => {
    const key = ev.target.dataset?.name;
    const found = key && findItem(key);
    if (found) { const t = tidyName(found.item.kind, ev.target.value); names.set(key, t); ev.target.value = t; }
  });
  root.addEventListener('focusout', () => { setTimeout(() => { if (pendingRender && !focusedHere()) render(); }, 0); });
  root.addEventListener('keydown', (ev) => {
    if (ev.key === 'Enter' && ev.target.dataset?.name) {
      const [loteId, itemId] = ev.target.dataset.name.split(':');
      names.set(ev.target.dataset.name, ev.target.value);
      ev.target.blur();
      review(loteId, [itemId], 'accept');
    }
  });
  root.addEventListener('toggle', (ev) => {
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
  };
}
