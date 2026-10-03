// modules/enemies-panel.js
// -----------------------------------------------------------------------------
// 👾 Enemigos: review the designs a task generated, and the enemy library they feed.
//
// Everything lives in docs/enemigos/ in the repo and goes through tools/taller-server (enemigos.mjs), so it
// only works with the web served by start.cmd. Full design: docs/architecture/enemigos-web.md.
//   · Diseños por revisar — one foldable group per batch (docs/enemigos/lotes/<id>.json). Click a card to
//     select it; accept / discard one, the selection, or all that are left. Accepting copies the design into
//     docs/enemigos/<Name>/ (the library) with estado "por-montar". Both are undoable here.
//   · Biblioteca — the accepted enemies, folded by estado.
//   · Lotes revisados — batches with nothing left to decide (folded).

const OPEN_KEY = 'redmagic.enemies.closed';
const POLL_MS = 5000;

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
const pascal = (s) => String(s).normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^A-Za-z0-9]+(.)?/g, (_, c) => (c ? c.toUpperCase() : ''))
  .replace(/^./, (c) => c.toUpperCase());

export function initEnemiesPanel({ root, statusEl, toast }) {
  let token = null;
  let data = null, dataText = '';
  let pollTimer = null, pendingRender = false, busy = false;
  const selected = new Set();               // "loteId:itemId"
  const names = new Map();                  // "loteId:itemId" → name typed in the card
  const errors = new Map();                 // "loteId:itemId" → last error from the server
  const closed = new Set(JSON.parse(sessionStorage.getItem(OPEN_KEY) || '[]'));   // folded groups (by key)
  let viewer = null;                        // { list: [key…], i }

  const setStatus = (t) => { statusEl.textContent = t; };
  const keyOf = (lote, item) => `${lote.id}:${item.id}`;
  const focusedHere = () => root.contains(document.activeElement) && document.activeElement.tagName === 'INPUT';

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
      const next = await api('GET', 'enemigos');
      const text = JSON.stringify(next);
      if (text !== dataText) { data = next; dataText = text; renderSoon(); }
      setStatus('docs/enemigos/ · servidor del taller');
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

  async function review(loteId, itemIds, decision) {
    if (!itemIds.length) return;
    busy = true;
    const named = Object.fromEntries(itemIds.map((id) => [id, names.get(`${loteId}:${id}`)]).filter(([, n]) => n != null));
    try {
      const { errors: errs } = await api('POST', 'enemigos/review', { loteId, itemIds, decision, names: named });
      itemIds.forEach((id) => { errors.delete(`${loteId}:${id}`); selected.delete(`${loteId}:${id}`); });
      Object.entries(errs || {}).forEach(([id, msg]) => errors.set(`${loteId}:${id}`, msg));
      const ok = itemIds.length - Object.keys(errs || {}).length;
      const verb = { accept: 'aceptado', discard: 'descartado', pending: 'devuelto a revisar' }[decision];
      toast(Object.keys(errs || {}).length ? `${ok} ${verb}${ok === 1 ? '' : 's'}; ${Object.keys(errs).length} con error (míralo en la tarjeta)` : `${ok} ${verb}${ok === 1 ? '' : 's'}`);
    } catch (e) { toast(e.message); }
    busy = false;
    dataText = '';
    await poll();
  }

  // ------------------------------------------------------------ render

  const lotes = () => data?.lotes || [];
  const pendingOf = (lote) => lote.items.filter((i) => i.status === 'pending');

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
    const name = names.get(key) ?? item.name;
    const err = errors.get(key);
    const sel = selected.has(key);
    let foot;
    if (item.status === 'pending') {
      foot = `<input class="enName" data-name="${esc(key)}" value="${esc(name)}" aria-label="Nombre del enemigo" spellcheck="false" autocomplete="off">
        <div class="enBtns"><button class="small enOk" data-act="accept" data-key="${esc(key)}" title="Aceptar: pasa a la biblioteca, por montar">✓ Aceptar</button>
        <button class="small" data-act="discard" data-key="${esc(key)}" title="Descartar">✕</button></div>`;
    } else if (item.status === 'accepted') {
      foot = `<div class="enName fixed">${esc(item.enemy || item.name)}</div>
        <div class="enBtns"><span class="enBadge ok">✓ En la biblioteca</span><button class="small" data-act="undo" data-key="${esc(key)}" title="Deshacer: lo quita de la biblioteca">↶</button></div>`;
    } else {
      foot = `<div class="enName fixed">${esc(name)}</div>
        <div class="enBtns"><span class="enBadge">Descartado</span><button class="small" data-act="undo" data-key="${esc(key)}" title="Recuperar">↶</button></div>`;
    }
    return `<div class="enCard${sel ? ' selected' : ''}" data-status="${item.status}">
      <button class="enArt" data-act="${item.status === 'pending' ? 'select' : 'view'}" data-key="${esc(key)}" title="${item.status === 'pending' ? 'Clic: seleccionar (⤢ para verlo en grande)' : 'Ver en grande'}">
        <img src="${imgUrl(item.image)}" alt="${esc(name)}" loading="lazy"></button>
      <button class="enZoom small" data-act="view" data-key="${esc(key)}" title="Ver en grande" aria-label="Ver en grande">⤢</button>
      ${item.note ? `<div class="enNote">${esc(item.note)}</div>` : ''}
      ${foot}
      ${err ? `<div class="enErr">${esc(err)}</div>` : ''}
    </div>`;
  }

  function loteGroup(lote, defaultOpen) {
    const pend = pendingOf(lote);
    const selHere = pend.filter((i) => selected.has(keyOf(lote, i))).length;
    const accepted = lote.items.filter((i) => i.status === 'accepted').length;
    const actions = pend.length ? `
      ${selHere ? `<span class="enSel">${selHere} seleccionado${selHere === 1 ? '' : 's'}</span>
        <button class="small primary" data-act="accept-sel" data-lote="${esc(lote.id)}">✓ Aceptar seleccionados</button>
        <button class="small" data-act="discard-sel" data-lote="${esc(lote.id)}">✕ Descartar seleccionados</button>
        <button class="small" data-act="clear-sel" data-lote="${esc(lote.id)}">Quitar selección</button><span class="enSep"></span>` : ''}
      <button class="small" data-act="accept-all" data-lote="${esc(lote.id)}">✓ Aceptar todos (${pend.length})</button>
      <button class="small danger" data-act="discard-all" data-lote="${esc(lote.id)}">✕ Descartar todos (${pend.length})</button>` : '';
    const count = pend.length ? `${pend.length} por revisar de ${lote.items.length}` : `${accepted} aceptado${accepted === 1 ? '' : 's'} de ${lote.items.length}`;
    return group(`lote:${lote.id}`, lote.title || lote.id, count,
      `<div class="enCards">${lote.items.map((i) => itemCard(lote, i)).join('')}</div>`,
      { actions, sub: [lote.request && lote.request !== lote.title ? lote.request : '', when(lote.createdAt)].filter(Boolean).join(' · '), defaultOpen });
  }

  function enemyCard(e) {
    const st = ESTADOS[e.estado] || { label: e.estado };
    return `<div class="enCard" data-status="${esc(e.estado)}">
      <button class="enArt" data-act="view-enemy" data-name="${esc(e.name)}" title="Ver en grande"><img src="${imgUrl(e.diseno?.image || '')}" alt="${esc(e.name)}" loading="lazy"></button>
      <div class="enName fixed">${esc(e.name)}</div>
      ${e.diseno?.request ? `<div class="enNote">${esc(e.diseno.request)}</div>` : ''}
      <div class="enBtns"><span class="enBadge" data-estado="${esc(e.estado)}">${esc(st.label)}</span></div>
    </div>`;
  }

  function render() {
    pendingRender = false;
    if (!data) return;
    const scroll = root.closest('#enemiesPanel')?.scrollTop;
    const toReview = lotes().filter((l) => pendingOf(l).length);
    const reviewed = lotes().filter((l) => !pendingOf(l).length);
    const enemigos = data.enemigos || [];

    const reviewHtml = toReview.length
      ? toReview.map((l) => loteGroup(l, true)).join('')
      : '<p class="enEmpty">Nada por revisar. Cuando una tarea de diseño termine, sus propuestas aparecen aquí.</p>';
    const byEstado = Object.keys(ESTADOS).concat([...new Set(enemigos.map((e) => e.estado))].filter((s) => !ESTADOS[s]))
      .map((s) => [s, enemigos.filter((e) => e.estado === s)]).filter(([, list]) => list.length);
    const libraryHtml = byEstado.length
      ? byEstado.map(([s, list]) => group(`estado:${s}`, ESTADOS[s]?.label || s, list.length,
        `<div class="enCards">${list.map(enemyCard).join('')}</div>`, { sub: ESTADOS[s]?.hint || '', defaultOpen: s !== 'descartado' && s !== 'aprobado' })).join('')
      : '<p class="enEmpty">Aún no hay enemigos en la biblioteca: acepta un diseño para empezar.</p>';

    root.innerHTML = `
      <section class="enSection"><h2>Diseños por revisar</h2>${reviewHtml}</section>
      <section class="enSection"><h2>Biblioteca de enemigos <span class="enHint">docs/enemigos/</span></h2>${libraryHtml}</section>
      ${reviewed.length ? `<section class="enSection">${group('revisados', 'Lotes revisados', reviewed.length, reviewed.map((l) => loteGroup(l, false)).join(''), { defaultOpen: false })}</section>` : ''}`;
    if (scroll != null) root.closest('#enemiesPanel').scrollTop = scroll;
    if (viewer) renderViewer();
  }

  function when(iso) {
    if (!iso) return '';
    const d = new Date(iso);
    return Number.isNaN(+d) ? '' : d.toLocaleString('es', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  }

  // ------------------------------------------------------------ viewer (big image, ← → to move, A / D to decide)

  const findItem = (key) => {
    const [loteId, itemId] = key.split(':');
    const lote = lotes().find((l) => l.id === loteId);
    return lote ? { lote, item: lote.items.find((i) => i.id === itemId) } : null;
  };

  const overlay = document.createElement('div');
  overlay.className = 'enViewer';
  overlay.hidden = true;
  document.body.appendChild(overlay);

  function openViewer(key) {
    const found = findItem(key);
    if (!found) return;
    viewer = { list: found.lote.items.map((i) => keyOf(found.lote, i)), i: found.lote.items.indexOf(found.item) };
    renderViewer();
  }

  function openEnemyViewer(name) {
    const e = (data?.enemigos || []).find((x) => x.name === name);
    if (!e) return;
    viewer = { enemy: e };
    renderViewer();
  }

  function renderViewer() {
    if (viewer.enemy) {
      overlay.innerHTML = `<div class="enViewerBox"><div class="enViewerHead"><b>${esc(viewer.enemy.name)}</b><span>${esc(ESTADOS[viewer.enemy.estado]?.label || viewer.enemy.estado)}</span>
        <button class="small" data-v="close" aria-label="Cerrar">✕</button></div><img src="${imgUrl(viewer.enemy.diseno?.image || '')}" alt=""></div>`;
      overlay.hidden = false;
      return;
    }
    const found = findItem(viewer.list[viewer.i]);
    if (!found?.item) { closeViewer(); return; }
    const { lote, item } = found;
    const pending = item.status === 'pending';
    overlay.innerHTML = `<div class="enViewerBox">
      <div class="enViewerHead"><b>${esc(names.get(keyOf(lote, item)) ?? item.name)}</b><span>${viewer.i + 1} / ${viewer.list.length} · ${esc(lote.title || '')}</span>
        <button class="small" data-v="close" aria-label="Cerrar">✕</button></div>
      <div class="enViewerStage"><button class="enViewerNav" data-v="prev" aria-label="Anterior">‹</button>
        <img src="${imgUrl(item.image)}" alt=""><button class="enViewerNav" data-v="next" aria-label="Siguiente">›</button></div>
      ${item.note ? `<p class="enNote">${esc(item.note)}</p>` : ''}
      <div class="enViewerBar">${pending
        ? '<button class="primary" data-v="accept">✓ Aceptar <kbd>A</kbd></button><button data-v="discard">✕ Descartar <kbd>D</kbd></button>'
        : `<span class="enBadge ${item.status === 'accepted' ? 'ok' : ''}">${item.status === 'accepted' ? '✓ En la biblioteca' : 'Descartado'}</span>`}
        <span class="efHint">← → para moverte · Esc para cerrar</span></div>
      ${errors.get(keyOf(lote, item)) ? `<div class="enErr">${esc(errors.get(keyOf(lote, item)))}</div>` : ''}
    </div>`;
    overlay.hidden = false;
  }

  function closeViewer() { viewer = null; overlay.hidden = true; overlay.innerHTML = ''; }
  const step = (d) => { if (!viewer?.list) return; viewer.i = (viewer.i + d + viewer.list.length) % viewer.list.length; renderViewer(); };

  async function decideInViewer(decision) {
    if (!viewer?.list) return;
    const [loteId, itemId] = viewer.list[viewer.i].split(':');
    await review(loteId, [itemId], decision);
    if (!errors.has(viewer?.list?.[viewer.i])) step(1);
  }

  overlay.addEventListener('click', (ev) => {
    if (ev.target === overlay) return closeViewer();
    const v = ev.target.closest('[data-v]')?.dataset.v;
    if (v === 'close') closeViewer();
    else if (v === 'prev') step(-1);
    else if (v === 'next') step(1);
    else if (v === 'accept') decideInViewer('accept');
    else if (v === 'discard') decideInViewer('discard');
  });
  document.addEventListener('keydown', (ev) => {
    if (overlay.hidden || ev.ctrlKey || ev.altKey || ev.metaKey) return;
    const k = ev.key.toLowerCase();
    if (k === 'escape') closeViewer();
    else if (k === 'arrowleft') step(-1);
    else if (k === 'arrowright') step(1);
    else if (k === 'a') decideInViewer('accept');
    else if (k === 'd') decideInViewer('discard');
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
    const pendIds = lote ? pendingOf(lote).map((i) => i.id) : [];
    const selIds = pendIds.filter((id) => selected.has(`${loteId}:${id}`));
    if (act === 'select') {
      if (selected.has(key)) selected.delete(key); else selected.add(key);
      render();
    } else if (act === 'view') openViewer(key);
    else if (act === 'view-enemy') openEnemyViewer(btn.dataset.name);
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
    if (key) { const p = pascal(ev.target.value); names.set(key, p); ev.target.value = p; }
  });
  root.addEventListener('focusout', () => { setTimeout(() => { if (pendingRender && !focusedHere()) render(); }, 0); });
  root.addEventListener('keydown', (ev) => {
    if (ev.key === 'Enter' && ev.target.dataset?.name) {
      const [loteId, itemId] = ev.target.dataset.name.split(':');
      names.set(ev.target.dataset.name, pascal(ev.target.value));
      review(loteId, [itemId], 'accept');
    }
  });
  root.addEventListener('toggle', (ev) => {
    const d = ev.target;
    if (!d.matches?.('details.enGroup')) return;
    const k = d.dataset.key;
    closed.delete(k); closed.delete(`+${k}`);
    closed.add(d.open ? `+${k}` : k);
    sessionStorage.setItem(OPEN_KEY, JSON.stringify([...closed]));
  }, true);

  return {
    onShow: () => { dataText = ''; poll(); },
  };
}
