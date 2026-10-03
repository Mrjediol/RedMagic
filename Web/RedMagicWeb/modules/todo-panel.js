// modules/todo-panel.js
// -----------------------------------------------------------------------------
// 📋 Tareas: the project's to-do / roadmap, and the door to Claude's task queue.
//
// Two zones in one tab (docs/architecture/tareas.md ▸ The web):
//   · 📝 Apuntes — the user's phases: notes / ideas / goals, each task with its description. Folded per phase.
//   · ⚙ Talleres — the "mechanical" side: one foldable section per kind of work (TALLERES). A phase belongs to a
//     taller by `phase.taller` or by its id (`designs`, `enemies` — made by the server). Diseños and Enemigos also
//     host modules/designs-panel.js (their own roots); the rest are "por definir" and take free tasks.
// A left rail jumps to every phase / taller; clicking a task opens it in the right-hand drawer.
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
const SEL_KEY = 'redmagic.todo.sel';
const FOLD_KEY = 'redmagic.todo.folded';        // apuntes phases the user folded (open by default)
const TALLER_KEY = 'redmagic.todo.talleres';    // talleres the user unfolded (folded by default)

/** The workshops. `phase` = the todo.json phase that holds its tasks (created with its first task). */
export const TALLERES = [
  { id: 'disenos', icon: '🎨', name: 'Diseños', phase: 'designs', tag: 'propuestas', built: true },
  { id: 'enemigos', icon: '👾', name: 'Enemigos', phase: 'enemies', tag: 'montaje', built: true },
  { id: 'mapas', icon: '🗺️', name: 'Mapas', phase: 'taller-mapas', hint: 'el método nuevo para obtener mapas aún no está decidido' },
  { id: 'ataques', icon: '⚔️', name: 'Ataques del jugador', phase: 'taller-ataques' },
  { id: 'items', icon: '🎒', name: 'Items', phase: 'taller-items' },
  { id: 'mecanicas', icon: '⚙️', name: 'Mecánicas nuevas', phase: 'taller-mecanicas' },
  { id: 'bugs', icon: '🐞', name: 'Bugs', phase: 'taller-bugs' },
];
const tallerById = (id) => TALLERES.find((t) => t.id === id);
/** The taller a phase belongs to, or null (= an apunte phase). */
export const tallerOf = (p) => (p.taller && tallerById(p.taller)) || TALLERES.find((t) => t.phase === p.id) || null;
const loadSet = (store, key) => { try { return new Set(JSON.parse(store.getItem(key) || '[]')); } catch { return new Set(); } };
const saveSet = (store, key, set) => { try { store.setItem(key, JSON.stringify([...set])); } catch { /* private window */ } };

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
 * @param {{ root: HTMLElement, rail: HTMLElement, detail: HTMLElement, extraTalleres: HTMLElement, statusEl: HTMLElement,
 *   toolbar: HTMLElement, quick: HTMLElement, toast?: (text: string) => void }} ctx
 *   root = the apuntes list; rail / detail = left index and right drawer; extraTalleres = where the "por definir"
 *   talleres go. Everything inside a `[data-designs]` element belongs to designs-panel.js and is left alone.
 *   mobile = movil.html (no file mode, no reset, no 📱 pairing button).
 * @returns {{ onShow(): void, flush(): Promise<void>, setBadges(b: Record<string, number>): void }}
 */
export function initTodoPanel({ root, rail, detail, extraTalleres, statusEl, toolbar, quick, toast = () => {}, mobile = false }) {
  const tab = root.closest('.tabPanel') || root.parentElement;
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
  let sel = sessionStorage.getItem(SEL_KEY) || null;   // the task open in the drawer
  let advOpen = false;                                 // the drawer's "prompt, modelo…" fold
  let badges = {};                                     // taller id → count that needs the user (designs-panel)
  const folded = loadSet(localStorage, FOLD_KEY);
  const tallerOpen = loadSet(localStorage, TALLER_KEY);

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
  const ours = (el) => tab.contains(el) && !el.closest('[data-designs]');
  const focusedHere = () => ours(document.activeElement) && /^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement.tagName)
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
      ${server && !mobile ? '<button data-act="phone" title="Usar este tab desde el móvil: enlace y QR para emparejarlo">📱 Móvil</button>' : ''}
      ${mobile ? '' : `<button data-act="reset" class="${confirmKey === 'reset' ? 'danger' : ''}">${confirmKey === 'reset' ? '¿Seguro? Se pierden tus cambios' : 'Restaurar lista original'}</button>`}
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
    const n = (v) => Number(v || 0).toLocaleString('es');
    const tok = r.tokens ? `entrada ${n(r.tokens.input)} · caché escrita ${n(r.tokens.cacheWrite)} · caché leída ${n(r.tokens.cacheRead)} · salida ${n(r.tokens.output)}` : '';
    const out = r.tokens ? `↓ ${(r.tokens.output / 1000).toFixed(1).replace('.', ',')}k tokens` : '';
    const meta = [when(r.at), r.durationMs ? minutes(r.durationMs) : '', out, tok].filter(Boolean).join(' · ');
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

  /** The drawer's body below title + description: live state, steps, launch, reports, then the advanced fold. */
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
      <label class="todoLabel">Pasos</label>
      <ul class="todoSteps">${steps.map((s, i) => stepRow(t, s, i)).join('')}</ul>
      <form class="todoAdd" data-form="addstep" data-id="${t.id}"><input id="todoStep-${t.id}" type="text" placeholder="Añadir paso" autocomplete="off"><button type="submit">Añadir</button></form>
      <div class="todoRow todoLaunch">
        ${server ? (active
          ? `<button class="danger" data-act="cancel" data-id="${t.id}">⏹ ${t.status === 'running' ? 'Parar' : 'Quitar de la cola'}</button>`
          : `<button class="primary" data-act="run" data-id="${t.id}">▶ Lanzar${steps.length ? ' (los pasos sin hacer)' : ''}</button>`)
          : '<span class="efHint">Para lanzarla, abre la web desde tools/taller-server/start.cmd.</span>'}
        <button data-act="toggle" data-id="${t.id}">${t.done ? '↶ Reabrir' : '✓ Hecha'}</button>
        <button class="${confirmKey === `dt${t.id}` ? 'danger' : ''}" data-act="del-task" data-id="${t.id}">${confirmKey === `dt${t.id}` ? '¿Borrar la tarea?' : '🗑'}</button>
      </div>
      ${resultBlock(t.result)}
      ${t.history?.length ? `<details class="todoHistory"><summary>Informes anteriores (${t.history.length})</summary>${t.history.map((r) => resultBlock(r, 'Anterior')).join('')}</details>` : ''}
      ${server && t.sessionId && !active ? `<div class="todoRow todoReplyBox">
        <textarea data-reply="${t.id}" placeholder="Responder: sigue la misma sesión (ej.: adelante con el 3 y el 7, así y así)"></textarea>
        <button data-act="reply" data-id="${t.id}">↩ Responder</button></div>` : ''}
      <details class="todoAdv" ${advOpen ? 'open' : ''}><summary>Prompt, modelo, qué necesita, documento</summary>
        <label class="todoLabel">Prompt <span class="efHint">vacío = título, descripción y pasos</span></label>
        <textarea class="todoPrompt" data-field="prompt" data-id="${t.id}" placeholder="Ej.: genera 10 bases de enemigos de hielo así y así, déjame la hoja de contacto">${esc(t.prompt || '')}</textarea>
        <div class="todoRow">
          <label><input type="checkbox" data-need="comfy" data-id="${t.id}" ${needs.includes('comfy') ? 'checked' : ''}> necesita ComfyUI</label>
          <label><input type="checkbox" data-need="unity" data-id="${t.id}" ${needs.includes('unity') ? 'checked' : ''}> necesita Unity</label>
          ${modelSelects(t.model || '', t.effort || '', t.id)}
        </div>
        <label class="todoLabel">Documento <span class="efHint">el que Claude deja al aparcar una tarea; lo lee primero</span></label>
        <input class="todoDoc" type="text" data-field="doc" data-id="${t.id}" value="${esc(t.doc || '')}" placeholder="docs/tareas/…md" autocomplete="off">
      </details>
    </div>`;
  }

  /** One task in a list: the whole row opens it in the drawer; the check and the tools act in place. */
  function taskRow(t) {
    const dbtn = confirmKey === `dt${t.id}`
      ? `<button class="small danger" data-act="del-task" data-id="${t.id}">¿Borrar?</button>`
      : `<button class="small" data-act="del-task" data-id="${t.id}" aria-label="Borrar tarea" title="Borrar tarea">×</button>`;
    const marks = [t.steps?.length ? `<span class="todoMark" title="Pasos">☰ ${t.steps.filter((s) => s.done).length}/${t.steps.length}</span>` : '',
      t.doc ? '<span class="todoMark" title="Tiene documento">📄</span>' : '',
      t.prompt ? '<span class="todoMark" title="Tiene prompt">💬</span>' : ''].join('');
    const note = t.note ? `<span class="todoNote">${esc(t.note)}</span>` : '';
    return `<li class="todoTask${t.done ? ' done' : ''}${sel === t.id ? ' sel' : ''}" data-open="${t.id}" title="Abrir">
      <button class="todoCheck" data-act="toggle" data-id="${t.id}" aria-pressed="${t.done}" aria-label="Marcar como hecha">✓</button>
      <div class="todoBody"><span class="todoText">${esc(t.text)}</span>${note}<div class="todoMarks">${statusBadge(t)}${marks}</div></div>
      <div class="todoTools"><button class="small" data-act="up-task" data-id="${t.id}" aria-label="Subir">↑</button><button class="small" data-act="down-task" data-id="${t.id}" aria-label="Bajar">↓</button>${dbtn}</div>
    </li>`;
  }

  const visibleTasks = (p) => p.tasks.filter((t) => !(hideDone && t.done));
  const apuntes = () => phases.filter((p) => !tallerOf(p));
  const tallerPhase = (tl) => phases.find((p) => tallerOf(p) === tl);
  const isRunning = (t) => ACTIVE.has(t.status);

  function renderList() {
    pendingRender = false;
    let total = 0, done = 0;
    apuntes().forEach((p) => p.tasks.forEach((t) => { total++; if (t.done) done++; }));
    const pct = total ? Math.round((done / total) * 100) : 0;
    const meter = document.getElementById('todoMeter');
    if (meter) {
      meter.querySelector('[data-m="done"]').textContent = done;
      meter.querySelector('[data-m="total"]').textContent = total;
      meter.querySelector('[data-m="pct"]').textContent = `${pct}%`;
      meter.querySelector('[data-m="fill"]').style.width = `${pct}%`;
    }

    root.innerHTML = apuntes().map((p) => {
      const pd = p.tasks.filter((t) => t.done).length;
      const isFolded = folded.has(p.id);
      const name = editing?.kind === 'pname' && editing.id === p.id
        ? `<input class="todoEdit todoPhaseEdit" data-edit data-focus aria-label="Nombre de la fase" value="${esc(p.name)}">`
        : `<span class="todoPhaseName">${esc(p.name)}</span><button class="small todoPen" data-act="edit-pname" data-id="${p.id}" title="Renombrar" aria-label="Renombrar">✎</button>`;
      const tag = editing?.kind === 'ptag' && editing.id === p.id
        ? `<input class="todoEdit todoTagEdit" data-edit data-focus aria-label="Etiqueta de la fase" value="${esc(p.tag)}">`
        : `<button class="todoTag${p.tag ? '' : ' empty'}" data-act="edit-ptag" data-id="${p.id}" title="Editar etiqueta">${p.tag ? esc(p.tag) : '+ etiqueta'}</button>`;
      const del = confirmKey === `dp${p.id}`
        ? `<button class="small danger" data-act="del-phase" data-id="${p.id}">¿Borrar fase?</button>`
        : `<button class="small" data-act="del-phase" data-id="${p.id}" aria-label="Borrar fase" title="Borrar fase">×</button>`;
      let rows = visibleTasks(p).map(taskRow).join('');
      if (!rows) rows = `<li class="todoEmpty">${p.tasks.length ? 'Todo hecho en esta fase.' : 'Sin tareas todavía. Añade la primera abajo.'}</li>`;
      return `<section class="todoPhase${isFolded ? ' folded' : ''}" id="todoPhase-${p.id}">
        <div class="todoPhaseHead" data-fold="${p.id}"><span class="todoChev" aria-hidden="true">▸</span>${name}${tag}<span class="todoCount">${pd}/${p.tasks.length}</span>
          <div class="todoTools always"><button class="small" data-act="up-phase" data-id="${p.id}" aria-label="Subir fase">↑</button><button class="small" data-act="down-phase" data-id="${p.id}" aria-label="Bajar fase">↓</button>${del}</div></div>
        ${isFolded ? '' : `<ul class="todoTasks">${rows}</ul>
        <form class="todoAdd" data-form="addtask" data-id="${p.id}"><input id="todoAdd-${p.id}" type="text" placeholder="Apuntar en ${esc(p.name)}" aria-label="Nueva tarea en ${esc(p.name)}" autocomplete="off"><button type="submit">Añadir</button></form>`}
      </section>`;
    }).join('');

    // Talleres: the built ones are static markup (index.html, with designs-panel inside) — only their task list
    // and summary meta are ours; the "por definir" ones are drawn whole here.
    TALLERES.forEach((tl) => {
      const ph = tallerPhase(tl);
      const list = ph ? visibleTasks(ph) : [];
      const meta = tallerMeta(tl, ph);
      if (tl.built) {
        const ul = tab.querySelector(`[data-taller-tasks="${tl.id}"]`);
        if (ul) ul.innerHTML = list.map(taskRow).join('') || '<li class="todoEmpty">Ninguna tarea todavía.</li>';
        const m = tab.querySelector(`[data-taller-meta="${tl.id}"]`);
        if (m) m.innerHTML = meta;
      }
    });
    extraTalleres.innerHTML = TALLERES.filter((tl) => !tl.built).map((tl) => {
      const ph = tallerPhase(tl);
      const rows = (ph ? visibleTasks(ph) : []).map(taskRow).join('');
      return `<details class="todoTaller" id="taller-${tl.id}" data-taller="${tl.id}" ${tallerOpen.has(tl.id) ? 'open' : ''}>
        <summary><span class="todoTallerName">${tl.icon} ${esc(tl.name)}</span><span class="todoTag empty">por definir</span><span class="todoTallerMeta">${tallerMeta(tl, ph)}</span></summary>
        <div class="todoTallerBody">
          <p class="efHint">Taller por definir${tl.hint ? ` (${esc(tl.hint)})` : ''}: aún sin formulario ni skill propios. Mientras tanto, apunta aquí tareas libres y lánzalas desde su ficha.</p>
          <ul class="todoTasks">${rows || '<li class="todoEmpty">Ninguna tarea todavía.</li>'}</ul>
          <form class="todoAdd" data-form="tallertask" data-id="${tl.id}"><input id="todoTaller-${tl.id}" type="text" placeholder="Nueva tarea en ${esc(tl.name)}" autocomplete="off"><button type="submit">Añadir</button></form>
        </div></details>`;
    }).join('');
  }

  function tallerMeta(tl, ph) {
    const tasks = ph?.tasks || [];
    const run = tasks.filter(isRunning).length;
    const open = tasks.filter((t) => !t.done).length;
    return [badges[tl.id] ? `<span class="todoPill you" title="Esperan tu decisión">${badges[tl.id]} te esperan</span>` : '',
      run ? `<span class="todoPill run">${run} en cola</span>` : '',
      open ? `<span class="todoCount">${open} tarea${open === 1 ? '' : 's'}</span>` : ''].join('');
  }

  function renderRail() {
    const item = (jump, label, count, cls = '') => `<button class="todoRailItem ${cls}" data-jump="${jump}">${label}${count}</button>`;
    rail.innerHTML = `<h2>📝 Apuntes</h2>${apuntes().map((p) => {
      const left = p.tasks.filter((t) => !t.done).length;
      return item(`phase:${p.id}`, esc(p.name), left ? `<b>${left}</b>` : '');
    }).join('')}
      <h2>⚙ Talleres</h2>${TALLERES.map((tl) => {
      const ph = tallerPhase(tl);
      const run = (ph?.tasks || []).some(isRunning);
      const count = badges[tl.id] ? `<b class="hot" title="Esperan tu decisión">${badges[tl.id]}</b>` : run ? '<b class="run" title="Trabajando">●</b>' : '';
      return item(`taller:${tl.id}`, `${tl.icon} ${esc(tl.name)}`, count, tl.built ? '' : 'soon');
    }).join('')}`;
  }

  /** Where a task lives, for the drawer's header. */
  function whereOf(r) {
    const tl = tallerOf(r.ph);
    return tl ? `⚙ ${tl.icon} ${tl.name}` : `📝 ${r.ph.name}`;
  }

  function renderDetail() {
    const r = sel ? find(sel) : null;
    if (!r) {   // sel is kept: on a reload the list arrives from the server after the first render
      detail.hidden = true;
      detail.innerHTML = '';
      return;
    }
    const t = r.t;
    detail.hidden = false;
    detail.innerHTML = `<div class="todoDetailHead"><span class="efHint">${esc(whereOf(r))}</span>${statusBadge(t)}
        <button class="small" data-act="close-detail" aria-label="Cerrar" title="Cerrar (Esc)">✕</button></div>
      <input class="todoDetailTitle" type="text" data-field="text" data-id="${t.id}" value="${esc(t.text)}" aria-label="Título" autocomplete="off">
      <label class="todoLabel">Descripción</label>
      <textarea class="todoDetailNote" data-field="note" data-id="${t.id}" placeholder="Apunta la idea, el contexto, lo que tenga que saber Claude…">${esc(t.note)}</textarea>
      ${details(t)}`;
  }

  function render() {
    renderList();
    renderRail();
    renderDetail();
    renderToolbar();
    renderQuick();
    const f = tab.querySelector('[data-focus]');
    if (f) { f.focus(); f.select?.(); }
    else if (focusId) document.getElementById(focusId)?.focus();
    focusId = null;
  }

  // On a narrow screen the drawer covers the list: the phone's Back button closes it (a history entry per opening).
  const narrow = () => window.matchMedia('(max-width: 760px)').matches;
  let pushed = false;
  function select(id) {
    if (id && !sel && narrow() && !pushed) { history.pushState({ todoSel: true }, ''); pushed = true; }
    if (!id && pushed) { pushed = false; history.back(); }
    sel = id;
    if (id) sessionStorage.setItem(SEL_KEY, id); else sessionStorage.removeItem(SEL_KEY);
    render();
    if (id && !narrow()) detail.querySelector('.todoDetailNote')?.focus({ preventScroll: true });
    if (id && narrow()) detail.scrollTop = 0;
  }
  window.addEventListener('popstate', () => { if (pushed) { pushed = false; select(null); } });

  /** 📱 Móvil: the pairing link (with the key) as text + QR, and how to reach the PC from the phone. */
  async function showPhone() {
    let info;
    try { info = await api('GET', 'movil'); } catch (e) { toast(e.message); return; }
    const base = info.url || info.lan[0] || '';
    const link = base ? `${base}/movil.html#k=${info.key}` : '';
    const dlg = document.createElement('dialog');
    dlg.className = 'todoPhone';
    dlg.innerHTML = `<h2>📱 Tareas en el móvil</h2>
      ${link ? `<p>Abre este enlace en el móvil una vez (o escanea el QR): queda emparejado. Luego, añádelo a la pantalla de inicio.</p>
        <div class="todoPhoneQr" data-qr></div>
        <p><input type="text" readonly value="${esc(link)}" data-link> <button data-copy>Copiar</button></p>
        ${info.url ? '<p class="efHint">Si no abre: en el PC, una vez, <code>tailscale serve --bg 3000</code> (y Tailscale conectado en el móvil).</p>' : ''}
        ${info.lan.length > 1 ? `<p class="efHint">Otras direcciones de este PC: ${info.lan.slice(1).map(esc).join(' · ')}</p>` : ''}`
      : `<p><b>El móvil aún no llega a este PC.</b> Lo más fácil, y funciona fuera de casa:</p>
        <ol><li>Instala <b>Tailscale</b> en el PC y en el móvil y entra con la misma cuenta.</li>
          <li>En el PC, una vez: <code>tailscale serve --bg 3000</code> (si pide activar HTTPS, acepta).</li>
          <li>Vuelve a pulsar 📱 Móvil: aquí saldrá el enlace con su QR.</li></ol>
        <p class="efHint">Solo en casa, sin Tailscale: <code>"listen": "0.0.0.0"</code> en tools/taller-server/config.json y reinicia start.cmd.</p>`}
      <p class="efHint">El enlace lleva la clave del móvil: no lo compartas. Borrar <code>Logs/Taller/movil-key.txt</code> (y reiniciar) desempareja todos.</p>
      <form method="dialog"><button>Cerrar</button></form>`;
    document.body.appendChild(dlg);
    dlg.addEventListener('close', () => dlg.remove());
    dlg.querySelector('[data-copy]')?.addEventListener('click', () => { navigator.clipboard?.writeText(link).then(() => toast('Enlace copiado')); });
    dlg.showModal();
    const qr = dlg.querySelector('[data-qr]');
    if (qr) {
      try {
        if (!window.QRCode) await new Promise((ok, fail) => { const sc = document.createElement('script'); sc.src = 'https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js'; sc.onload = ok; sc.onerror = fail; document.head.appendChild(sc); });
        new window.QRCode(qr, { text: link, width: 220, height: 220 });
      } catch { qr.textContent = '(sin conexión para dibujar el QR: copia el enlace)'; }
    }
  }

  /** Unfolds a phase or a taller and scrolls to it (the rail, and links into a taller). */
  function jump(target) {
    const [kind, id] = target.split(':');
    if (kind === 'phase') {
      if (folded.delete(id)) { saveSet(localStorage, FOLD_KEY, folded); renderList(); }
      document.getElementById(`todoPhase-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } else {
      const el = document.getElementById(`taller-${id}`);
      if (el) { el.open = true; el.scrollIntoView({ behavior: 'smooth', block: 'start' }); }
    }
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

  /** Moves an apunte phase past its apunte neighbour (taller phases are not in that list). */
  function movePhase(id, d) {
    const list = apuntes();
    const k = list.findIndex((p) => p.id === id);
    const other = list[k + d];
    if (k < 0 || !other) return;
    mut(() => swap(phases, phaseIdx(id), phaseIdx(other.id)));
  }

  /** The phase that holds a taller's tasks, made the first time. */
  function tallerPhaseFor(tl) {
    let ph = tallerPhase(tl);
    if (!ph) { ph = { id: tl.phase, name: `${tl.icon} ${tl.name}`, tag: 'Taller', taller: tl.id, tasks: [] }; phases.push(ph); }
    return ph;
  }

  const ACTIONS = {
    toggle: (id) => { const r = find(id); if (r) mut(() => { r.t.done = !r.t.done; }); },
    'close-detail': () => select(null),
    'edit-pname': (id) => { editing = { kind: 'pname', id }; render(); },
    'edit-ptag': (id) => { editing = { kind: 'ptag', id }; render(); },
    'edit-step': (id) => { editing = { kind: 'step', id }; render(); },
    'up-task': (id) => { const r = find(id); if (r) mut(() => swap(r.ph.tasks, r.i, r.i - 1)); },
    'down-task': (id) => { const r = find(id); if (r) mut(() => swap(r.ph.tasks, r.i, r.i + 1)); },
    'up-phase': (id) => movePhase(id, -1),
    'down-phase': (id) => movePhase(id, 1),
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
      const box = detail.querySelector(`[data-reply="${id}"]`);
      const text = box?.value.trim();
      if (!text) { toast('Escribe la respuesta primero'); return; }
      launch(id, { followUp: text });
    },
    cancel: (id) => serverAction('cancel', { taskId: id }, 'Cancelada'),
    force: (id) => serverAction('force', { taskId: id }, 'Lanzando aunque haya una terminal abierta'),
  };

  // One set of listeners for the whole tab (apuntes, talleres, rail, drawer); designs-panel's areas are skipped.
  tab.addEventListener('click', (e) => {
    if (!ours(e.target)) return;
    const b = e.target.closest('[data-act]');
    if (b && ACTIONS[b.dataset.act]) { ACTIONS[b.dataset.act](b.dataset.id); return; }
    if (b || e.target.closest('input, textarea, select, form, a')) return;
    const j = e.target.closest('[data-jump]');
    if (j) { jump(j.dataset.jump); return; }
    const row = e.target.closest('[data-open]');
    if (row) { select(sel === row.dataset.open ? null : row.dataset.open); return; }
    const head = e.target.closest('[data-fold]');
    if (head) {
      const id = head.dataset.fold;
      if (!folded.delete(id)) folded.add(id);
      saveSet(localStorage, FOLD_KEY, folded);
      renderList();
    }
  });
  tab.addEventListener('toggle', (e) => {
    const d = e.target;
    if (d.matches?.('details.todoTaller')) {
      if (d.open) tallerOpen.add(d.dataset.taller); else tallerOpen.delete(d.dataset.taller);
      saveSet(localStorage, TALLER_KEY, tallerOpen);
    } else if (d.matches?.('details.todoAdv')) advOpen = d.open;
  }, true);
  tab.addEventListener('keydown', (e) => {
    const t = e.target;
    if (!ours(t)) return;
    if (e.key === 'Escape' && sel && !t.hasAttribute?.('data-edit') && !/^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName)) { select(null); return; }
    if (e.key === 'Enter' && t.dataset?.field === 'text') { e.preventDefault(); t.blur(); return; }
    if (!t.hasAttribute?.('data-edit')) return;
    if (e.key === 'Escape') { editing = null; render(); }
    else if (e.key === 'Enter' && (t.tagName === 'INPUT' || !e.shiftKey)) { e.preventDefault(); commit(t.value); }
  });
  tab.addEventListener('focusout', (e) => {
    if (!ours(e.target)) return;
    if (e.target.hasAttribute?.('data-edit')) commit(e.target.value);
    else if (pendingRender) setTimeout(() => { if (!focusedHere() && pendingRender) render(); }, 0);
  });
  // Drawer fields (title, description, prompt, doc, model, needs): saved when they change; only the list is
  // redrawn, never the drawer under your cursor.
  tab.addEventListener('change', (e) => {
    const el = e.target;
    if (!ours(el)) return;
    const r = el.dataset.id ? find(el.dataset.id) : null;
    if (!r) return;
    if (el.dataset.need) {
      const needs = new Set(r.t.needs || []);
      if (el.checked) needs.add(el.dataset.need); else needs.delete(el.dataset.need);
      r.t.needs = [...needs];
      if (!r.t.needs.length) delete r.t.needs;
    } else if (el.dataset.field === 'text') {
      const v = el.value.trim();
      if (v) r.t.text = v; else el.value = r.t.text;
    } else if (el.dataset.field === 'note') r.t.note = el.value.trim();
    else if (el.dataset.field) {
      const v = el.value.trim();
      if (v) r.t[el.dataset.field] = v; else delete r.t[el.dataset.field];
    } else return;
    renderList();
    renderRail();
    queueSave();
  });
  tab.addEventListener('submit', (e) => {
    const f = e.target;
    if (!ours(f) || !['addtask', 'addstep', 'tallertask'].includes(f.dataset.form)) return;
    e.preventDefault();
    const v = f.querySelector('input').value.trim();
    if (!v) return;
    if (f.dataset.form === 'addtask') {
      const i = phaseIdx(f.dataset.id);
      if (i < 0) return;
      focusId = `todoAdd-${f.dataset.id}`;
      mut(() => phases[i].tasks.push({ id: uid(), text: v, note: '', done: false }));
    } else if (f.dataset.form === 'tallertask') {
      const tl = tallerById(f.dataset.id);
      if (!tl) return;
      const task = { id: uid(), text: v, note: '', done: false };
      mut(() => tallerPhaseFor(tl).tasks.push(task));
      select(task.id);   // straight to its sheet: write the description, then launch
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
    sel = task.id;
    sessionStorage.setItem(SEL_KEY, sel);
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
    phone: showPhone,
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
    tab.querySelectorAll('details.todoTaller').forEach((d) => { d.open = tallerOpen.has(d.dataset.taller); });
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
    /** What waits for the user in a taller (designs-panel: proposals to review, enemies to approve). */
    setBadges: (b) => {
      if (JSON.stringify(b) === JSON.stringify(badges)) return;
      badges = { ...b };
      renderRail();
      TALLERES.forEach((tl) => { const m = tl.built && tab.querySelector(`[data-taller-meta="${tl.id}"]`); if (m) m.innerHTML = tallerMeta(tl, tallerPhase(tl)); });
    },
  };
}
