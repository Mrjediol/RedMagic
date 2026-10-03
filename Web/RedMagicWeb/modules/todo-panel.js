// modules/todo-panel.js
// -----------------------------------------------------------------------------
// 📋 Tareas: the project's to-do / roadmap, and the door to Claude's task queue.
//
// The list is `docs/tareas/todo.json` in the repo — Claude reads it to know what is pending and may edit it.
// Two ways to reach it:
//   · SERVER  — the page is served by tools/taller-server (start.cmd, http://localhost:3000): the list goes
//               through its API (it merges your edits with the queue's and Claude's, see store.mjs), and
//               tasks can be LAUNCHED: the server runs them one by one with `claude -p` (runner.mjs).
//   · FILE    — served by anything else (`npx serve`): edit only, the file linked once through the File
//               System Access API (handle in IndexedDB), localStorage always as a fallback.
//
// A task: { id, text, note, done } + optional { prompt, doc, steps[{ id, text, done }], needs['comfy'|'unity'],
// model, effort } written by you, + { status, waiting, result, sessionId } written by the queue. Unknown
// fields are kept as they came.

export const DATA_VERSION = 1;
export const FILE_NAME = 'todo.json';
const LS_KEY = 'redmagic.todo.v1';
const DB_NAME = 'redmagic-todo';
const OPEN_KEY = 'redmagic.todo.open';

export const DEFAULT_PHASES = [
  { id: 'p1', name: 'Contenido del prototipo', tag: 'Corto plazo', tasks: [
    { id: 't1', text: '15 enemigos: 5 por zona en las 3 primeras zonas', note: '', done: false },
    { id: 't2', text: '15 mapas: 5 por zona (4 normales y 1 de boss)', note: '', done: false },
    { id: 't3', text: 'Probar un nuevo método para obtener mapas', note: '', done: false },
    { id: 't4', text: '4 full sets: hielo, oro y 2 más', note: 'Faltan por decidir los 2 últimos', done: false },
    { id: 't5', text: '5 armas base para el jugador', note: '', done: false },
    { id: 't6', text: 'Probar nuevos sistemas, integración y optimización de Claude', note: '', done: false },
  ] },
  { id: 'p2', name: 'Interfaz del player en partida', tag: 'Pronto', tasks: [
    { id: 't7', text: 'Diseñar e implementar la interfaz del player mientras juega (HUD)', note: 'Los menús ya están hechos', done: false },
  ] },
  { id: 'p3', name: 'Sonidos y efectos', tag: 'Después', tasks: [
    { id: 't8', text: 'Añadir sonidos y efectos a todo lo anterior', note: 'Enemigos, mapas, sets y armas', done: false },
  ] },
  { id: 'p4', name: 'Balanceo básico', tag: 'Después', tasks: [
    { id: 't9', text: 'Primer balanceo para el prototipo', note: 'Números, daños, pasivas, upgrades, vidas y coste en tiendas', done: false },
  ] },
  { id: 'p5', name: 'Bugs y rendimiento', tag: 'Después', tasks: [
    { id: 't10', text: 'Arreglar bugs, como los atascamientos de movimiento', note: '', done: false },
    { id: 't11', text: 'Comprobar la optimización de FPS', note: 'Ya conseguido', done: true },
  ] },
  { id: 'p6', name: 'Rework del player', tag: 'Al final · la más importante', tasks: [
    { id: 't12', text: 'Buen diseño y buenas animaciones, que se sienta fluido y divertido', note: 'Solo cuando el prototipo esté balanceado y listo', done: false },
    { id: 't13', text: 'Que pueda hacer parry', note: '', done: false },
    { id: 't14', text: 'Idea: las armas pasan a ser poderes', note: 'Mete la mano en un pozo, siente una energía fluyendo y la mano cambia de color. Cada poder tiene una mano distinta, con efectos y animaciones personalizadas.', done: false },
    { id: 't15', text: 'Idea: la otra mano lleva el cristal del parry', note: 'Con mejoras que también cambien su aspecto visual', done: false },
  ] },
  { id: 'p7', name: 'Cinemática inicial', tag: 'Tras el prototipo', tasks: [
    { id: 't16', text: 'Hacer pruebas de la cinemática inicial', note: 'Cuando todo el prototipo esté acabado', done: false },
  ] },
  { id: 'p8', name: 'Ideas sueltas', tag: 'Pendiente', tasks: [
    { id: 't17', text: 'Buscar excusas temáticas para la mecánica de waves', note: '', done: false },
  ] },
];

const STATUS = {
  queued: { label: 'en cola', cls: 'queued' },
  waiting: { label: 'esperando', cls: 'waiting' },
  running: { label: 'trabajando…', cls: 'running' },
  done: { label: 'terminada', cls: 'done' },
  failed: { label: 'falló', cls: 'failed' },
  cancelled: { label: 'cancelada', cls: 'cancelled' },
};
const ACTIVE = new Set(['queued', 'waiting', 'running']);

const clone = (x) => JSON.parse(JSON.stringify(x));
const uid = () => Math.random().toString(36).slice(2, 9);
const esc = (s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const swap = (arr, i, j) => { if (j >= 0 && j < arr.length) [arr[i], arr[j]] = [arr[j], arr[i]]; };
const minutes = (ms) => { const s = Math.round(ms / 1000); return s < 60 ? `${s} s` : `${Math.floor(s / 60)} min ${s % 60} s`; };
const when = (iso) => { try { return new Date(iso).toLocaleString('es-ES', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }); } catch { return ''; } };

/** A document from anything that might be one (the file, the server, localStorage): phases or null. */
export function readDocument(raw) {
  try {
    const doc = typeof raw === 'string' ? JSON.parse(raw) : raw;
    const phases = Array.isArray(doc) ? doc : doc?.phases;
    if (!Array.isArray(phases)) return null;
    return phases.filter((p) => p && typeof p === 'object').map((p) => ({
      ...p, id: p.id || uid(), name: String(p.name ?? ''), tag: String(p.tag ?? ''),
      tasks: (Array.isArray(p.tasks) ? p.tasks : []).filter((t) => t && typeof t === 'object').map((t) => ({
        ...t, id: t.id || uid(), text: String(t.text ?? ''), note: String(t.note ?? ''), done: !!t.done,
        ...(Array.isArray(t.steps) ? { steps: t.steps.filter((s) => s && typeof s === 'object').map((s) => ({ ...s, id: s.id || uid(), text: String(s.text ?? ''), done: !!s.done })) } : {}),
      })),
    }));
  } catch { return null; }
}

export function writeDocument(phases, extra = {}) {
  return JSON.stringify({ ...extra, v: DATA_VERSION, updated: new Date().toISOString(), phases }, null, 2) + '\n';
}

// ============================================================ the linked file (File System Access API)

const hasFileApi = () => typeof window.showOpenFilePicker === 'function';

function idb() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => req.result.createObjectStore('kv');
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}
async function kvGet(key) {
  const db = await idb();
  return new Promise((resolve) => {
    const req = db.transaction('kv').objectStore('kv').get(key);
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => resolve(undefined);
  });
}
async function kvSet(key, value) {
  const db = await idb();
  return new Promise((resolve) => {
    const tx = db.transaction('kv', 'readwrite');
    if (value === undefined) tx.objectStore('kv').delete(key); else tx.objectStore('kv').put(value, key);
    tx.oncomplete = () => resolve();
    tx.onerror = () => resolve();
  });
}

// ============================================================ the panel

/**
 * @param {{ root: HTMLElement, statusEl: HTMLElement, toolbar: HTMLElement, quick: HTMLElement, toast?: (text: string) => void }} ctx
 * @returns {{ onShow(): void, flush(): Promise<void> }}
 */
export function initTodoPanel({ root, statusEl, toolbar, quick, toast = () => {} }) {
  let phases = clone(DEFAULT_PHASES);
  let extra = {};                          // top-level fields of the file other than phases (the queue…), kept

  // server mode
  let server = null;                       // { token } when the page comes from tools/taller-server
  let rev = -1;
  let known = { tasks: [], phases: [] };   // what we loaded last: tasks we do not have any more were deleted HERE
  let live = null;                         // last /api/status
  let pendingRender = false;
  let pollTimer = null;

  // file mode
  let handle = null, fileStamp = 0, needsPermission = false;

  let editing = null, confirmKey = null, confirmTimer = null, focusId = null, hideDone = false;
  let dirty = false, saving = false, saveTimer = null;
  const open = new Set(JSON.parse(sessionStorage.getItem(OPEN_KEY) || '[]'));

  const setStatus = (text) => { statusEl.textContent = text; };
  const allTasks = () => phases.flatMap((p) => p.tasks);
  const find = (tid) => {
    for (const ph of phases) {
      const i = ph.tasks.findIndex((t) => t.id === tid);
      if (i >= 0) return { ph, i, t: ph.tasks[i] };
    }
    return null;
  };
  const phaseIdx = (pid) => phases.findIndex((p) => p.id === pid);
  const remember = () => { known = { tasks: allTasks().map((t) => t.id), phases: phases.map((p) => p.id) }; };
  const focusedHere = () => root.contains(document.activeElement) && /^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement.tagName)
    || quick.contains(document.activeElement);

  // ------------------------------------------------------------ server

  async function api(method, route, body) {
    const res = await fetch(`/api/${route}`, {
      method, headers: { 'Content-Type': 'application/json', ...(server ? { 'X-Taller-Token': server.token } : {}) },
      body: body ? JSON.stringify(body) : undefined,
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || `HTTP ${res.status}`);
    return data;
  }

  async function connectServer() {
    try {
      const res = await fetch('/api/hello', { cache: 'no-store' });
      if (!res.ok) return false;
      const hello = await res.json();
      if (!hello.token) return false;
      server = { token: hello.token };
      return true;
    } catch { return false; }
  }

  function adopt(data) {
    const loaded = readDocument(data.doc);
    if (!loaded) return;
    rev = data.rev;
    const { phases: _ignored, ...rest } = data.doc;
    extra = rest;
    phases = loaded;
    remember();
  }

  async function pull() {
    adopt(await api('GET', 'todo'));
    renderSoon();
  }

  async function poll() {
    clearTimeout(pollTimer);
    if (!server) return;
    try {
      live = await api('GET', 'status');
      if (live.rev !== rev && !dirty && !saving && !editing) await pull();
      else renderSoon(true);
      setStatus(`Guardado · docs/tareas/todo.json (servidor del taller)`);
    } catch {
      live = null;
      setStatus('El servidor del taller no responde: ¿se cerró la ventana de start.cmd?');
      renderToolbar();
    }
    const busy = live && (live.running || live.queue?.length || live.waiting);
    pollTimer = setTimeout(poll, root.closest('[hidden]') ? 10000 : busy ? 2000 : 5000);
  }

  /** Re-render unless you are typing in the panel (then right after you leave the field). */
  function renderSoon(liveOnly = false) {
    if (focusedHere()) { pendingRender = true; if (liveOnly) renderToolbar(); return; }
    render();
  }

  async function launch(taskId, { stepId = null, followUp = null, force = false } = {}) {
    if (!server) { toast('Arranca el servidor (tools/taller-server/start.cmd) para lanzar tareas'); return; }
    await flush();
    try {
      await api('POST', 'run', { taskId, stepId, followUp, force });
      toast(followUp ? 'Respuesta enviada a la cola' : 'En la cola');
    } catch (e) { toast(e.message); }
    poll();
  }

  async function serverAction(route, body, message) {
    try { await api('POST', route, body); if (message) toast(message); } catch (e) { toast(e.message); }
    poll();
  }

  // ------------------------------------------------------------ saving / loading

  function queueSave() {
    dirty = true;
    setStatus('Guardando…');
    clearTimeout(saveTimer);
    saveTimer = setTimeout(flush, 450);
  }

  async function flush() {
    clearTimeout(saveTimer);
    if (!dirty) return;
    if (saving) { saveTimer = setTimeout(flush, 300); return; }
    dirty = false;
    saving = true;
    try {
      if (server) {
        adopt(await api('PUT', 'todo', { phases, knownTaskIds: known.tasks, knownPhaseIds: known.phases }));
        setStatus('Guardado · docs/tareas/todo.json (servidor del taller)');
      } else {
        const text = writeDocument(phases, extra);
        try { localStorage.setItem(LS_KEY, text); } catch { /* storage full or private window */ }
        if (handle && !needsPermission) {
          const writable = await handle.createWritable();
          await writable.write(text);
          await writable.close();
          fileStamp = (await handle.getFile()).lastModified;
          setStatus(`Guardado · docs/tareas/${handle.name}`);
        } else setStatus('Guardado · solo este navegador');
      }
    } catch (e) {
      setStatus(`No se pudo guardar: ${e.message}`);
    }
    saving = false;
    if (dirty) queueSave();
    else renderSoon();
  }

  /** FILE mode: loads the linked file if it changed since we last saw it and nothing is pending here. */
  async function pullFile(force = false) {
    if (server || !handle || dirty || saving || editing) return;
    try {
      if ((await handle.queryPermission({ mode: 'readwrite' })) !== 'granted') { needsPermission = true; renderToolbar(); return; }
      needsPermission = false;
      const file = await handle.getFile();
      if (!force && file.lastModified === fileStamp) return;
      const text = await file.text();
      const loaded = readDocument(text);
      fileStamp = file.lastModified;
      if (!loaded) { setStatus(`${handle.name} no tiene el formato de la lista: no se ha cargado.`); return; }
      try { const { phases: _p, ...rest } = JSON.parse(text); extra = rest; } catch { /* readDocument already accepted it */ }
      if (JSON.stringify(loaded) !== JSON.stringify(phases)) { phases = loaded; render(); toast('Lista recargada desde el archivo'); }
      setStatus(`Guardado · docs/tareas/${handle.name}`);
    } catch (e) {
      setStatus(`No se pudo leer ${handle.name}: ${e.message}`);
    }
    renderToolbar();
  }

  async function linkFile() {
    if (!hasFileApi()) { toast('Este navegador no deja enlazar archivos (usa Chrome o Edge)'); return; }
    try {
      const [picked] = await window.showOpenFilePicker({
        types: [{ description: 'Lista de tareas (JSON)', accept: { 'application/json': ['.json'] } }],
        multiple: false,
      });
      if ((await picked.requestPermission({ mode: 'readwrite' })) !== 'granted') { toast('Hace falta permiso de escritura'); return; }
      handle = picked;
      needsPermission = false;
      await kvSet('handle', handle);
      const file = await handle.getFile();
      const loaded = file.size ? readDocument(await file.text()) : null;
      if (loaded) { phases = loaded; fileStamp = file.lastModified; render(); setStatus(`Guardado · docs/tareas/${handle.name}`); }
      else { dirty = true; await flush(); }
      toast(`Enlazado: ${handle.name}`);
    } catch (e) {
      if (e?.name !== 'AbortError') toast(`No se pudo enlazar: ${e.message}`);
    }
    renderToolbar();
  }

  // ------------------------------------------------------------ render: toolbar + quick form

  function appChip(app, label) {
    const up = live?.apps?.[app];
    return `<span class="todoApp ${up ? 'up' : ''}" title="${up ? 'Abierto' : 'Cerrado'}">${up ? '●' : '○'} ${label}${up ? '' : ` <button class="small" data-act="open-app" data-id="${app}">Abrir</button>`}</span>`;
  }

  /** Real plan usage (5 h and weekly windows) as the CLI reports it after each run — not dollars. */
  function usageChip() {
    const u = live?.usage;
    if (!u) return '<span class="todoUsage" title="Se rellena tras la primera tarea de la cola">Plan: sin datos aún</span>';
    const part = (label, w) => {
      if (!w) return '';
      const pct = Math.round(w.utilization * 100);
      const reset = w.resetsAt ? new Date(w.resetsAt * 1000).toLocaleString('es', { weekday: 'short', hour: '2-digit', minute: '2-digit' }) : '';
      const cls = pct >= 85 ? 'hot' : pct >= 70 ? 'warm' : '';
      return `<span class="todoUsageBar ${cls}" title="${label}: reinicia ${esc(reset)}"><i style="width:${Math.min(100, pct)}%"></i><b>${label} ${pct} %</b></span>`;
    };
    const age = Math.round((Date.now() - u.at) / 60000);
    return `<span class="todoUsage" title="Último dato hace ${age} min (se actualiza al trabajar la cola)">${part('5 h', u.fiveHour)}${part('Semana', u.sevenDay)}</span>`;
  }

  function renderToolbar() {
    let left;
    if (server) {
      const q = live?.queue?.length || 0;
      const state = !live ? '<span class="todoServer off">⚠ servidor sin respuesta</span>'
        : `<span class="todoServer">🟢 Servidor del taller · ${live.running ? 'trabajando' : q ? 'en cola' : 'libre'}${q ? ` · ${q} en cola` : ''}</span>`;
      left = `${state}${live ? appChip('comfy', 'ComfyUI') + appChip('unity', 'Unity') + usageChip() : ''}`;
    } else if (handle) {
      left = needsPermission
        ? '<button data-act="reconnect" class="primary" title="El navegador pide permiso otra vez en cada sesión">🔓 Reconectar con el archivo</button>'
        : `<button data-act="unlink" title="Dejar de escribir en el archivo del repo">🔗 ${esc(handle.name)} · desenlazar</button>`;
    } else {
      left = `<button data-act="link" class="${hasFileApi() ? 'primary' : ''}" ${hasFileApi() ? '' : 'disabled'} title="Elige docs/tareas/todo.json del repo">🔗 Enlazar docs/tareas/todo.json…</button>`;
    }
    toolbar.innerHTML = `${left}
      <button data-act="hide-done" class="${hideDone ? 'active' : ''}">Ocultar hechas</button>
      <span class="tabSpacer"></span>
      <button data-act="reset" class="${confirmKey === 'reset' ? 'danger' : ''}">${confirmKey === 'reset' ? '¿Seguro? Se pierden tus cambios' : 'Restaurar lista original'}</button>
      ${server ? '' : '<p class="efHint todoNoServer">Para lanzar tareas a Claude, abre la web con <code>Web/RedMagicWeb/tools/taller-server/start.cmd</code> en vez de <code>npx serve</code>.</p>'}`;
  }

  function renderQuick() {
    if (quick.dataset.built) {
      const sel = quick.querySelector('[name="phase"]');
      const value = sel.value;
      sel.innerHTML = phases.map((p) => `<option value="${p.id}">${esc(p.name)}</option>`).join('');
      if (phases.some((p) => p.id === value)) sel.value = value;
      quick.querySelector('[data-quick="launch"]').disabled = !server;
      return;
    }
    quick.dataset.built = '1';
    quick.innerHTML = `<details class="todoQuick">
      <summary>＋ Tarea para Claude</summary>
      <form data-form="quick">
        <input name="text" type="text" placeholder="Título" required autocomplete="off">
        <textarea name="note" placeholder="Descripción (para ti y para Claude)"></textarea>
        <textarea name="prompt" placeholder="Prompt: lo que se le pide a Claude. Vacío = título y descripción." class="todoPrompt"></textarea>
        <div class="todoRow">
          <label>Fase <select name="phase">${phases.map((p) => `<option value="${p.id}">${esc(p.name)}</option>`).join('')}</select></label>
          <label><input type="checkbox" name="comfy"> necesita ComfyUI</label>
          <label><input type="checkbox" name="unity"> necesita Unity</label>
          ${modelSelects('', '')}
        </div>
        <div class="todoRow">
          <button type="submit" data-quick="add">Añadir</button>
          <button type="submit" data-quick="launch" class="primary" ${server ? '' : 'disabled'}>Añadir y lanzar</button>
        </div>
      </form></details>`;
  }

  function modelSelects(model, effort, taskId = '') {
    const id = taskId ? ` data-id="${taskId}"` : '';
    const opt = (v, label, cur) => `<option value="${v}" ${v === cur ? 'selected' : ''}>${label}</option>`;
    return `<label>Modelo <select name="model" data-field="model"${id}>${opt('', 'por defecto (Sonnet)', model)}${opt('sonnet', 'Sonnet', model)}${opt('opus', 'Opus', model)}</select></label>
      <label>Esfuerzo <select name="effort" data-field="effort"${id}>${opt('', 'por defecto (medio)', effort)}${opt('low', 'bajo', effort)}${opt('medium', 'medio', effort)}${opt('high', 'alto', effort)}</select></label>`;
  }

  // ------------------------------------------------------------ render: the list

  function statusBadge(item) {
    const s = STATUS[item.status];
    return s ? `<span class="todoBadge ${s.cls}">${s.label}</span>` : '';
  }

  function resultBlock(r, label = 'Informe') {
    if (!r) return '';
    const meta = [when(r.at), r.durationMs ? minutes(r.durationMs) : ''].filter(Boolean).join(' · ');
    return `<div class="todoResult ${r.ok ? '' : 'bad'}">
      <div class="todoResultHead"><b>${label}</b><span>${esc(meta)}</span></div>
      ${r.reply ? `<div class="todoReply">↩ ${esc(r.reply)}</div>` : ''}
      <div class="todoResultText">${esc(r.text)}</div>
      ${r.denied?.length ? `<div class="efHint">Acciones que no se le permitieron: ${r.denied.map(esc).join(' · ')}</div>` : ''}
    </div>`;
  }

  function stepRow(t, s, i) {
    const editingThis = editing?.kind === 'step' && editing.id === `${t.id}:${s.id}`;
    const text = editingThis
      ? `<input class="todoEdit" data-edit data-focus aria-label="Texto del paso" value="${esc(s.text)}">`
      : `<button class="todoText" data-act="edit-step" data-id="${t.id}:${s.id}" title="Editar">${i + 1}. ${esc(s.text)}</button>`;
    const del = confirmKey === `ds${s.id}`
      ? `<button class="small danger" data-act="del-step" data-id="${t.id}:${s.id}">¿Borrar?</button>`
      : `<button class="small" data-act="del-step" data-id="${t.id}:${s.id}" aria-label="Borrar paso">×</button>`;
    return `<li class="todoStep${s.done ? ' done' : ''}">
      <button class="todoCheck small" data-act="step-toggle" data-id="${t.id}:${s.id}" aria-pressed="${s.done}" aria-label="Marcar paso">✓</button>
      <div class="todoBody">${text}${statusBadge(s)}${s.result ? `<details class="todoStepResult"><summary>Informe del paso</summary>${resultBlock(s.result, `Paso ${i + 1}`)}</details>` : ''}</div>
      <div class="todoTools always">${server ? `<button class="small" data-act="step-run" data-id="${t.id}:${s.id}" title="Lanzar sólo este paso" ${ACTIVE.has(s.status) ? 'disabled' : ''}>▶</button>` : ''}${del}</div>
    </li>`;
  }

  function details(t) {
    const running = live?.running?.taskId === t.id ? live.running : null;
    const waiting = live?.waiting?.taskId === t.id ? live.waiting : null;
    const needs = t.needs || [];
    const steps = t.steps || [];
    const active = ACTIVE.has(t.status);
    return `<div class="todoDetails">
      ${running ? `<div class="todoLive"><b>Trabajando${running.step ? ` · ${esc(running.step)}` : ''}</b> · ${minutes(Date.now() - running.startedAt)}
        <ul>${running.activity.map((a) => `<li>${esc(a.line)}</li>`).join('') || '<li>arrancando…</li>'}</ul></div>` : ''}
      ${waiting || t.waiting ? `<div class="todoLive wait">⏳ ${esc(waiting?.reason || t.waiting)}
        ${/terminal/.test(waiting?.reason || t.waiting || '') ? `<button class="small" data-act="force" data-id="${t.id}">Lanzar igualmente</button>` : ''}</div>` : ''}
      <label class="todoLabel">Prompt <span class="efHint">lo que se le pide a Claude; vacío = título, descripción y pasos</span></label>
      <textarea class="todoPrompt" data-field="prompt" data-id="${t.id}" placeholder="Ej.: genera 10 bases de enemigos de hielo así y así, déjame la hoja de contacto">${esc(t.prompt || '')}</textarea>
      <div class="todoRow">
        <label><input type="checkbox" data-need="comfy" data-id="${t.id}" ${needs.includes('comfy') ? 'checked' : ''}> necesita ComfyUI</label>
        <label><input type="checkbox" data-need="unity" data-id="${t.id}" ${needs.includes('unity') ? 'checked' : ''}> necesita Unity</label>
        ${modelSelects(t.model || '', t.effort || '', t.id)}
      </div>
      <label class="todoLabel">Documento <span class="efHint">el que Claude deja con el estado al aparcar una tarea; lo lee primero</span></label>
      <input class="todoDoc" type="text" data-field="doc" data-id="${t.id}" value="${esc(t.doc || '')}" placeholder="docs/tareas/…md" autocomplete="off">
      <label class="todoLabel">Pasos</label>
      <ul class="todoSteps">${steps.map((s, i) => stepRow(t, s, i)).join('')}</ul>
      <form class="todoAdd" data-form="addstep" data-id="${t.id}"><input id="todoStep-${t.id}" type="text" placeholder="Añadir paso" autocomplete="off"><button type="submit">Añadir</button></form>
      ${resultBlock(t.result)}
      ${t.history?.length ? `<details class="todoHistory"><summary>Informes anteriores (${t.history.length})</summary>${t.history.map((r) => resultBlock(r, "Anterior")).join("")}</details>` : ""}
      ${server && t.sessionId && !active ? `<div class="todoRow todoReplyBox">
        <textarea data-reply="${t.id}" placeholder="Responder: sigue la misma sesión (ej.: adelante con el 3 y el 7, así y así)"></textarea>
        <button data-act="reply" data-id="${t.id}">↩ Responder</button></div>` : ''}
      <div class="todoRow">
        ${server ? (active
          ? `<button class="danger" data-act="cancel" data-id="${t.id}">⏹ ${t.status === 'running' ? 'Parar' : 'Quitar de la cola'}</button>`
          : `<button class="primary" data-act="run" data-id="${t.id}">▶ Lanzar${steps.length ? ' (los pasos sin hacer)' : ''}</button>`)
          : '<span class="efHint">Para lanzarla, abre la web desde tools/taller-server/start.cmd.</span>'}
      </div>
    </div>`;
  }

  function render() {
    pendingRender = false;
    let total = 0, done = 0;
    phases.forEach((p) => p.tasks.forEach((t) => { total++; if (t.done) done++; }));
    const pct = total ? Math.round((done / total) * 100) : 0;
    const meter = document.getElementById('todoMeter');
    if (meter) {
      meter.querySelector('[data-m="done"]').textContent = done;
      meter.querySelector('[data-m="total"]').textContent = total;
      meter.querySelector('[data-m="pct"]').textContent = `${pct}%`;
      meter.querySelector('[data-m="fill"]').style.width = `${pct}%`;
    }

    root.innerHTML = phases.map((p) => {
      const pd = p.tasks.filter((t) => t.done).length;
      const name = editing?.kind === 'pname' && editing.id === p.id
        ? `<input class="todoEdit todoPhaseEdit" data-edit data-focus aria-label="Nombre de la fase" value="${esc(p.name)}">`
        : `<button class="todoPhaseName" data-act="edit-pname" data-id="${p.id}" title="Editar nombre">${esc(p.name)}</button>`;
      const tag = editing?.kind === 'ptag' && editing.id === p.id
        ? `<input class="todoEdit todoTagEdit" data-edit data-focus aria-label="Etiqueta de la fase" value="${esc(p.tag)}">`
        : `<button class="todoTag${p.tag ? '' : ' empty'}" data-act="edit-ptag" data-id="${p.id}" title="Editar etiqueta">${p.tag ? esc(p.tag) : '+ etiqueta'}</button>`;
      const del = confirmKey === `dp${p.id}`
        ? `<button class="small danger" data-act="del-phase" data-id="${p.id}">¿Borrar fase?</button>`
        : `<button class="small" data-act="del-phase" data-id="${p.id}" aria-label="Borrar fase" title="Borrar fase">×</button>`;

      let rows = p.tasks.filter((t) => !(hideDone && t.done)).map((t) => {
        const isOpen = open.has(t.id);
        const text = editing?.kind === 'text' && editing.id === t.id
          ? `<input class="todoEdit" data-edit data-focus aria-label="Texto de la tarea" value="${esc(t.text)}">`
          : `<button class="todoText" data-act="edit-text" data-id="${t.id}" title="Editar">${esc(t.text)}</button>`;
        let note;
        if (editing?.kind === 'note' && editing.id === t.id) note = `<textarea class="todoEdit" data-edit data-focus aria-label="Nota de la tarea">${esc(t.note)}</textarea>`;
        else if (t.note) note = `<button class="todoNote" data-act="edit-note" data-id="${t.id}">${esc(t.note)}</button>`;
        else note = `<button class="todoNote add" data-act="edit-note" data-id="${t.id}">+ nota</button>`;
        const dbtn = confirmKey === `dt${t.id}`
          ? `<button class="small danger" data-act="del-task" data-id="${t.id}">¿Borrar?</button>`
          : `<button class="small" data-act="del-task" data-id="${t.id}" aria-label="Borrar tarea" title="Borrar tarea">×</button>`;
        const marks = [t.steps?.length ? `<span class="todoMark" title="Pasos">☰ ${t.steps.filter((s) => s.done).length}/${t.steps.length}</span>` : '',
          t.doc ? '<span class="todoMark" title="Tiene documento">📄</span>' : '',
          t.prompt ? '<span class="todoMark" title="Tiene prompt">💬</span>' : ''].join('');
        return `<li class="todoTask${t.done ? ' done' : ''}${isOpen ? ' open' : ''}">
          <button class="todoCheck" data-act="toggle" data-id="${t.id}" aria-pressed="${t.done}" aria-label="Marcar como hecha">✓</button>
          <div class="todoBody">${text}${note}<div class="todoMarks">${statusBadge(t)}${marks}</div></div>
          <div class="todoTools"><button class="small" data-act="expand" data-id="${t.id}" aria-expanded="${isOpen}" title="Prompt, pasos, lanzar e informe">${isOpen ? '▾' : '▸'}</button><button class="small" data-act="up-task" data-id="${t.id}" aria-label="Subir">↑</button><button class="small" data-act="down-task" data-id="${t.id}" aria-label="Bajar">↓</button>${dbtn}</div>
          ${isOpen ? details(t) : ''}
        </li>`;
      }).join('');
      if (!rows) rows = `<li class="todoEmpty">${p.tasks.length ? 'Todo hecho en esta fase.' : 'Sin tareas todavía. Añade la primera abajo.'}</li>`;

      return `<section class="todoPhase">
        <div class="todoPhaseHead">${name}${tag}<span class="todoCount">${pd}/${p.tasks.length}</span>
          <div class="todoTools always"><button class="small" data-act="up-phase" data-id="${p.id}" aria-label="Subir fase">↑</button><button class="small" data-act="down-phase" data-id="${p.id}" aria-label="Bajar fase">↓</button>${del}</div></div>
        <ul class="todoTasks">${rows}</ul>
        <form class="todoAdd" data-form="addtask" data-id="${p.id}"><input id="todoAdd-${p.id}" type="text" placeholder="Añadir tarea" aria-label="Nueva tarea en ${esc(p.name)}" autocomplete="off"><button type="submit">Añadir</button></form>
      </section>`;
    }).join('');

    const f = root.querySelector('[data-focus]');
    if (f) { f.focus(); f.select?.(); }
    else if (focusId) document.getElementById(focusId)?.focus();
    focusId = null;
    renderToolbar();
    renderQuick();
  }

  function mut(fn) { fn(); render(); queueSave(); }

  function commit(val) {
    if (!editing) return;
    const ed = editing;
    editing = null;
    val = (val || '').trim();
    if (ed.kind === 'step') {
      const [tid, sid] = ed.id.split(':');
      const step = find(tid)?.t.steps?.find((s) => s.id === sid);
      if (step && val) step.text = val;
    } else {
      const r = ed.kind === 'text' || ed.kind === 'note' ? find(ed.id) : null;
      const pi = ed.kind === 'pname' || ed.kind === 'ptag' ? phaseIdx(ed.id) : -1;
      if (ed.kind === 'text' && r && val) r.t.text = val;
      else if (ed.kind === 'note' && r) r.t.note = val;
      else if (ed.kind === 'pname' && pi > -1 && val) phases[pi].name = val;
      else if (ed.kind === 'ptag' && pi > -1) phases[pi].tag = val;
    }
    render();
    queueSave();
  }

  function arm(key) {
    confirmKey = key;
    clearTimeout(confirmTimer);
    confirmTimer = setTimeout(() => { confirmKey = null; render(); }, 3000);
    render();
  }

  // ------------------------------------------------------------ events

  const stepOf = (id) => {
    const [tid, sid] = String(id).split(':');
    const r = find(tid);
    const i = r?.t.steps?.findIndex((s) => s.id === sid) ?? -1;
    return r && i >= 0 ? { ...r, si: i, s: r.t.steps[i] } : null;
  };

  const ACTIONS = {
    toggle: (id) => { const r = find(id); if (r) mut(() => { r.t.done = !r.t.done; }); },
    expand: (id) => {
      if (open.has(id)) open.delete(id); else open.add(id);
      sessionStorage.setItem(OPEN_KEY, JSON.stringify([...open]));
      render();
    },
    'edit-text': (id) => { editing = { kind: 'text', id }; render(); },
    'edit-note': (id) => { editing = { kind: 'note', id }; render(); },
    'edit-pname': (id) => { editing = { kind: 'pname', id }; render(); },
    'edit-ptag': (id) => { editing = { kind: 'ptag', id }; render(); },
    'edit-step': (id) => { editing = { kind: 'step', id }; render(); },
    'up-task': (id) => { const r = find(id); if (r) mut(() => swap(r.ph.tasks, r.i, r.i - 1)); },
    'down-task': (id) => { const r = find(id); if (r) mut(() => swap(r.ph.tasks, r.i, r.i + 1)); },
    'up-phase': (id) => { const i = phaseIdx(id); mut(() => swap(phases, i, i - 1)); },
    'down-phase': (id) => { const i = phaseIdx(id); mut(() => swap(phases, i, i + 1)); },
    'del-task': (id) => {
      if (confirmKey !== `dt${id}`) { arm(`dt${id}`); return; }
      const r = find(id);
      confirmKey = null;
      if (r) mut(() => r.ph.tasks.splice(r.i, 1));
    },
    'del-phase': (id) => {
      if (confirmKey !== `dp${id}`) { arm(`dp${id}`); return; }
      const i = phaseIdx(id);
      confirmKey = null;
      mut(() => phases.splice(i, 1));
    },
    'step-toggle': (id) => { const r = stepOf(id); if (r) mut(() => { r.s.done = !r.s.done; }); },
    'del-step': (id) => {
      const r = stepOf(id);
      if (!r) return;
      if (confirmKey !== `ds${r.s.id}`) { arm(`ds${r.s.id}`); return; }
      confirmKey = null;
      mut(() => r.t.steps.splice(r.si, 1));
    },
    run: (id) => launch(id),
    'step-run': (id) => { const r = stepOf(id); if (r) launch(r.t.id, { stepId: r.s.id }); },
    reply: (id) => {
      const box = root.querySelector(`[data-reply="${id}"]`);
      const text = box?.value.trim();
      if (!text) { toast('Escribe la respuesta primero'); return; }
      launch(id, { followUp: text });
    },
    cancel: (id) => serverAction('cancel', { taskId: id }, 'Cancelada'),
    force: (id) => serverAction('force', { taskId: id }, 'Lanzando aunque haya una terminal abierta'),
  };

  root.addEventListener('click', (e) => {
    const b = e.target.closest('[data-act]');
    if (b && ACTIONS[b.dataset.act]) ACTIONS[b.dataset.act](b.dataset.id);
  });
  root.addEventListener('keydown', (e) => {
    const t = e.target;
    if (!t.hasAttribute?.('data-edit')) return;
    if (e.key === 'Escape') { editing = null; render(); }
    else if (e.key === 'Enter' && (t.tagName === 'INPUT' || !e.shiftKey)) { e.preventDefault(); commit(t.value); }
  });
  root.addEventListener('focusout', (e) => {
    if (e.target.hasAttribute?.('data-edit')) commit(e.target.value);
    else if (pendingRender) setTimeout(() => { if (!focusedHere() && pendingRender) render(); }, 0);
  });
  // Detail fields (prompt, doc, model, needs): saved when they change, without re-rendering under your cursor.
  root.addEventListener('change', (e) => {
    const el = e.target;
    const r = el.dataset.id ? find(el.dataset.id) : null;
    if (!r) return;
    if (el.dataset.need) {
      const needs = new Set(r.t.needs || []);
      if (el.checked) needs.add(el.dataset.need); else needs.delete(el.dataset.need);
      r.t.needs = [...needs];
      if (!r.t.needs.length) delete r.t.needs;
    } else if (el.dataset.field) {
      const v = el.value.trim();
      if (v) r.t[el.dataset.field] = v; else delete r.t[el.dataset.field];
    } else return;
    queueSave();
  });
  root.addEventListener('submit', (e) => {
    e.preventDefault();
    const f = e.target;
    const v = f.querySelector('input').value.trim();
    if (!v) return;
    if (f.dataset.form === 'addtask') {
      const i = phaseIdx(f.dataset.id);
      if (i < 0) return;
      focusId = `todoAdd-${f.dataset.id}`;
      mut(() => phases[i].tasks.push({ id: uid(), text: v, note: '', done: false }));
    } else if (f.dataset.form === 'addstep') {
      const r = find(f.dataset.id);
      if (!r) return;
      focusId = `todoStep-${f.dataset.id}`;
      mut(() => { (r.t.steps ||= []).push({ id: uid(), text: v, done: false }); });
    }
  });

  quick.addEventListener('submit', async (e) => {
    e.preventDefault();
    const form = e.target;
    const data = new FormData(form);
    const text = String(data.get('text') || '').trim();
    const i = phaseIdx(String(data.get('phase')));
    if (!text || i < 0) return;
    const task = { id: uid(), text, note: String(data.get('note') || '').trim(), done: false };
    const prompt = String(data.get('prompt') || '').trim();
    if (prompt) task.prompt = prompt;
    const needs = ['comfy', 'unity'].filter((n) => data.get(n));
    if (needs.length) task.needs = needs;
    if (data.get('model')) task.model = String(data.get('model'));
    if (data.get('effort')) task.effort = String(data.get('effort'));
    const andLaunch = e.submitter?.dataset.quick === 'launch';
    phases[i].tasks.push(task);
    open.add(task.id);
    sessionStorage.setItem(OPEN_KEY, JSON.stringify([...open]));
    form.reset();
    render();
    dirty = true;
    await flush();
    if (andLaunch) launch(task.id);
    else toast('Tarea añadida');
  });

  document.getElementById('todoPhaseForm')?.addEventListener('submit', (e) => {
    e.preventDefault();
    const input = document.getElementById('todoNewPhase');
    const v = input.value.trim();
    if (!v) return;
    input.value = '';
    mut(() => phases.push({ id: uid(), name: v, tag: '', tasks: [] }));
  });

  const TOOLBAR = {
    link: linkFile,
    reconnect: async () => {
      try { if ((await handle.requestPermission({ mode: 'readwrite' })) === 'granted') { needsPermission = false; await pullFile(true); } }
      catch (e) { toast(`No se pudo reconectar: ${e.message}`); }
      renderToolbar();
    },
    unlink: async () => { handle = null; needsPermission = false; await kvSet('handle', undefined); setStatus('Guardado · solo este navegador'); renderToolbar(); },
    'hide-done': () => { hideDone = !hideDone; render(); },
    'open-app': (app) => serverAction('open', { app }, app === 'unity' ? 'Abriendo Unity…' : 'Abriendo ComfyUI…'),
    reset: () => {
      if (confirmKey !== 'reset') { arm('reset'); return; }
      confirmKey = null;
      mut(() => { phases = clone(DEFAULT_PHASES); });
    },
  };
  toolbar.addEventListener('click', (e) => {
    const b = e.target.closest('[data-act]');
    if (b && TOOLBAR[b.dataset.act]) TOOLBAR[b.dataset.act](b.dataset.id);
  });

  window.addEventListener('focus', () => { if (!root.closest('[hidden]')) { if (server) poll(); else pullFile(); } });

  // ------------------------------------------------------------ start

  (async function start() {
    try {
      const raw = localStorage.getItem(LS_KEY);
      const stored = raw ? readDocument(raw) : null;
      if (stored) phases = stored;
    } catch { /* no storage */ }
    render();
    if (await connectServer()) {
      try { await pull(); } catch (e) { setStatus(`El servidor no dio la lista: ${e.message}`); }
      poll();
      return;
    }
    setStatus('Guardado · solo este navegador');
    try {
      handle = (await kvGet('handle')) || null;
      if (handle) await pullFile(true);
    } catch { handle = null; }
    renderToolbar();
  }());

  return {
    onShow: () => { if (server) poll(); else pullFile(); },
    flush: async () => { dirty = true; await flush(); },
  };
}
