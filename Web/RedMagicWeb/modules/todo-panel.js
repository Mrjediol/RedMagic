// modules/todo-panel.js
// -----------------------------------------------------------------------------
// 📋 Tareas: the project's to-do / roadmap, and the door to Claude's task queue.
//
// Two zones in one tab (docs/architecture/tareas.md ▸ The web):
//   · 📝 Apuntes — the user's phases: notes / ideas / goals, each task with its description. Folded per phase.
//   · ⚙ Talleres — the "mechanical" side: one foldable section per kind of work (TALLERES). A phase belongs to a
//     taller by `phase.taller` or by its id (`designs`, `enemies` — made by the server). Diseños and Enemigos also
//     host modules/designs-panel.js (their own roots); the rest are "por definir" and take free tasks.
//   · 🤖 Claude — at the TOP: the open task (conversation with Claude: reports, live console, queued instructions and
//     a composer that works like a Claude console), then what is running / queued / finished lately.
//   · 🗂 Historial — every task that ran, filtered by type, sorted by tokens / time / date.
// A left rail jumps to every zone / phase / taller; clicking a task anywhere opens it at the top.
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
// model, effort, images[{ id, name }] (reference images for the prompt, saved by the server) } written by you, + { status, waiting, result, sessionId } written by the queue. Unknown
// fields are kept as they came.

import { pressGuard } from './press-guard.js';

export const DATA_VERSION = 1;
export const FILE_NAME = 'todo.json';
const LS_KEY = 'redmagic.todo.v1';
const DB_NAME = 'redmagic-todo';
const SEL_KEY = 'redmagic.todo.sel';
const FOLD_KEY = 'redmagic.todo.folded';        // apuntes phases the user folded (open by default)
const TALLER_KEY = 'redmagic.todo.talleres';    // talleres the user unfolded (folded by default)
const HIST_KEY = 'redmagic.todo.hist';          // the history's filter and order
const DISMISS_KEY = 'redmagic.todo.dismissed';  // finished tasks taken out of 🤖 Claude (id → the result's date)

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
const kTok = (n) => (n >= 1e6 ? `${(n / 1e6).toFixed(1).replace('.', ',')}M` : n >= 1000 ? `${Math.round(n / 1000)}k` : String(n || 0));
const firstLine = (s) => (String(s || '').split('\n').find((l) => l.trim()) || '').trim().slice(0, 160);
/** The tokens a run WROTE (what each report's "↓" shows): the number to compare costs by. */
const tokensOf = (r) => r?.tokens?.output || 0;
/** Everything the run processed, cache reads included (they repeat the whole context every call, so it is huge). */
const withCache = (r) => (r?.tokens ? (r.tokens.input || 0) + (r.tokens.cacheWrite || 0) + (r.tokens.cacheRead || 0) + (r.tokens.output || 0) : 0);
/** A task's runs, oldest first: the earlier reports (`history`, newest first) and the latest (`result`). */
const runsOf = (t) => [...(t.history || [])].reverse().concat(t.result ? [t.result] : []);

/** The kind of a task, for the history's filter: from what made it (server fields), else where it lives. */
const TYPE_LABELS = { diseno: '🎨 Diseño', montaje: '👾 Montaje completo', cuerpo: '🦴 Cuerpo (paso 1)', resto: '🧩 Resto (paso 3)', pendientes: '🔧 Pendientes de enemigo', apunte: '📝 Apunte' };
function typeOf(t, ph) {
  if (t.design) return 'diseno';
  if (t.enemy) return t.kind || (/^Cuerpo de/.test(t.text) ? 'cuerpo' : /^Terminar/.test(t.text) ? 'resto' : /^Montar/.test(t.text) ? 'montaje' : 'pendientes');
  const tl = tallerOf(ph);
  return tl ? `taller:${tl.id}` : 'apunte';
}
function typeLabel(k) {
  if (TYPE_LABELS[k]) return TYPE_LABELS[k];
  const tl = k.startsWith('taller:') && tallerById(k.slice(7));
  return tl ? `${tl.icon} ${tl.name}` : k;
}
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
  const press = pressGuard(() => [tab, toolbar, rail, detail, quick]);   // no redraw under a held button: it would eat the click
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
  let sel = sessionStorage.getItem(SEL_KEY) || null;   // the task open at the top
  let advOpen = false;                                 // the open task's "prompt, modelo…" fold
  const kept = new Set();                              // other folds of the open task left open (data-keep)
  const drafts = new Map();                            // task id → what is typed in its composer (survives re-renders)
  let listScroll = null;                               // where the list was when a task opened at the top
  let hist = { type: 'all', state: 'all', sort: 'recent' };
  try { Object.assign(hist, JSON.parse(sessionStorage.getItem(HIST_KEY) || '{}')); } catch { /* defaults */ }
  let dismissed = {};
  try { dismissed = JSON.parse(localStorage.getItem(DISMISS_KEY) || '{}') || {}; } catch { /* none */ }
  const panelEl = tab.querySelector('#todoPanel');
  const claudeEl = tab.querySelector('#todoClaude');
  const historyEl = tab.querySelector('#todoHistoryRoot');
  let railHtml = '', claudeHtml = '';
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

  // ------------------------------------------------------------ reference images (prompt attachments)

  let quickImages = [];                    // attached in the "＋ Tarea para Claude" form, before the task exists
  const IMG_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'image/gif'];

  const toBase64 = (file) => new Promise((resolve, reject) => {
    const r = new FileReader();
    r.onload = () => resolve(String(r.result).split(',')[1] || '');
    r.onerror = () => reject(new Error('no se pudo leer la imagen'));
    r.readAsDataURL(file);
  });

  /** Uploads picked / pasted image files to the server; returns [{ id, name }]. */
  async function uploadImages(files) {
    const out = [];
    for (const f of files) {
      if (!IMG_TYPES.includes(f.type)) { toast(`${f.name || 'Archivo'}: solo png, jpg, webp o gif`); continue; }
      if (f.size > 10e6) { toast(`${f.name || 'Imagen'}: pesa más de 10 MB`); continue; }
      try { out.push(await api('POST', 'adjunto', { name: f.name || 'captura.png', type: f.type, data: await toBase64(f) })); }
      catch (e) { toast(e.message); }
    }
    return out;
  }

  /** Thumbnails with ✕, then the file button. `owner` = a task id, or '' for the quick form. */
  function imageStrip(images, owner) {
    return `<div class="todoImages" data-images="${owner}">
      ${images.map((im) => `<span class="todoImg"><a href="/api/adjunto?id=${im.id}" target="_blank" rel="noopener" title="${esc(im.name)}"><img src="/api/adjunto?id=${im.id}" alt="${esc(im.name)}"></a>
        <button type="button" class="small" data-act="img-del" data-id="${owner}:${im.id}" aria-label="Quitar imagen">×</button></span>`).join('')}
      ${server ? `<label class="todoImgAdd small" title="Elegir imágenes de referencia (también puedes pegar con Ctrl+V)">🖼 Añadir imagen<input type="file" accept="image/*" multiple hidden data-attach="${owner}"></label>
        <span class="efHint">o pega una captura con Ctrl+V</span>` : ''}</div>`;
  }

  async function attach(owner, files) {
    if (!server) { toast('Las imágenes necesitan el servidor del taller (start.cmd)'); return; }
    const added = await uploadImages(files);
    if (!added.length) return;
    if (!owner) {
      quickImages.push(...added);
      renderQuickImages();
    } else {
      const r = find(owner);
      if (!r) return;
      (r.t.images ||= []).push(...added);
      mut(() => {});
    }
    toast(added.length === 1 ? 'Imagen adjunta' : `${added.length} imágenes adjuntas`);
  }

  function renderQuickImages() {
    const box = quick.querySelector('[data-images=""]');
    if (box) box.outerHTML = imageStrip(quickImages, '');
  }

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
      else { renderToolbar(); renderLive(); }   // only the live parts: nothing under the cursor is redrawn
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
    if (press.busy()) { pendingRender = true; press.after(renderSoon); return; }
    if (focusedHere()) { pendingRender = true; if (liveOnly) renderToolbar(); return; }
    render();
  }

  async function launch(taskId, { stepId = null, followUp = null, force = false } = {}) {
    if (!server) { toast('Arranca el servidor (tools/taller-server/start.cmd) para lanzar tareas'); return; }
    await flush();
    try {
      await api('POST', 'run', { taskId, stepId, followUp, force });
      const busy = ACTIVE.has(find(taskId)?.t.status);
      toast(followUp || stepId ? (busy ? 'En cola: lo hace al acabar lo que está haciendo' : 'Enviado') : 'En la cola');
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
    if (press.busy()) return;   // the next poll draws it
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
      renderQuickImages();   // the file button only exists once the server answered
      return;
    }
    quick.dataset.built = '1';
    quick.innerHTML = `<details class="todoQuick">
      <summary>＋ Tarea para Claude</summary>
      <form data-form="quick">
        <input name="text" type="text" placeholder="Título" required autocomplete="off">
        <textarea name="note" placeholder="Descripción (para ti y para Claude)"></textarea>
        <textarea name="prompt" placeholder="Prompt: lo que se le pide a Claude. Vacío = título y descripción." class="todoPrompt"></textarea>
        ${imageStrip(quickImages, '')}
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
      <div class="todoBody">${text}${statusBadge(s)}</div>
      <div class="todoTools always">${server ? `<button class="small" data-act="step-run" data-id="${t.id}:${s.id}" title="Lanzar sólo este paso" ${ACTIVE.has(s.status) ? 'disabled' : ''}>▶</button>` : ''}${del}</div>
    </li>`;
  }

  const queueOf = (tid) => (live?.queue || []).filter((q) => q.taskId === tid);
  const you = (html) => `<div class="todoMsg">${html}</div>`;
  /** What started a run, as the user's line in the conversation. */
  const runHead = (r) => (r.reply ? you(`↩ ${esc(r.reply)}`) : r.step ? you(`▶ Paso: ${esc(r.step)}`) : '');

  /** The conversation: every run, oldest first (the older ones folded), each with what started it. */
  function thread(t) {
    const runs = runsOf(t);
    const one = (r) => `${runHead(r)}${resultBlock(r, r.ok ? 'Claude' : 'Claude · falló')}`;
    // Every run shows what you asked; its report folds (only the latest starts open).
    return runs.map((r, i) => {
      const key = `run:${t.id}:${r.at}`;
      const open = kept.has(key) || (i === runs.length - 1 && !kept.has(`-${key}`));
      return `${runHead(r)}<details class="todoRun" data-keep="${esc(key)}" ${open ? 'open' : ''}><summary>${r.ok ? '' : '✗ '}${esc(when(r.at))} · ${r.durationMs ? minutes(r.durationMs) : ''} · ↓ ${kTok(tokensOf(r))} · ${esc(firstLine(r.text).slice(0, 90))}</summary>${resultBlock(r, r.ok ? 'Claude' : 'Claude · falló')}</details>`;
    }).join('');
  }

  /** What is happening now: the console of the run, the wait, what is queued for this task (each can be taken out). */
  function liveBox(t) {
    const running = live?.running?.taskId === t.id ? live.running : null;
    const waiting = live?.waiting?.taskId === t.id ? live.waiting : null;
    const why = waiting?.reason || (t.status === 'waiting' ? t.waiting : '');
    const stepText = (id) => t.steps?.find((s) => s.id === id)?.text || '';
    return `${running ? `${running.followUp ? you(`↩ ${esc(running.followUp)}`) : running.step ? you(`▶ Paso: ${esc(running.step)}`) : ''}
        <div class="todoLive"><b>⚙ Trabajando</b> · ${minutes(Date.now() - running.startedAt)}
        <ul class="todoConsole">${running.activity.map((a) => `<li>${esc(a.line)}</li>`).join('') || '<li>arrancando…</li>'}</ul></div>` : ''}
      ${why ? `<div class="todoLive wait">⏳ ${esc(why)}
        ${/terminal/.test(why) ? `<button class="small" data-act="force" data-id="${t.id}">Lanzar igualmente</button>` : ''}</div>` : ''}
      ${queueOf(t.id).map((q) => `<div class="todoMsg queued"><span>⏳ en cola · ${q.followUp ? esc(q.followUp) : q.stepId ? `Paso: ${esc(stepText(q.stepId))}` : 'lanzar la tarea'}</span>
        ${q.qid ? `<button class="small" data-act="unqueue" data-id="${esc(q.qid)}" title="Quitar de la cola">×</button>` : ''}</div>`).join('')}`;
  }

  /** The box at the bottom of the conversation: type and Enter, like a Claude console. */
  function composer(t) {
    if (!server) return '<p class="efHint">Para lanzarla y hablar con Claude, abre la web desde tools/taller-server/start.cmd.</p>';
    const ph = ACTIVE.has(t.status) ? 'Instrucción para cuando acabe lo que está haciendo (misma sesión)'
      : t.sessionId ? 'Responde o pide lo siguiente (sigue la misma sesión)' : 'Instrucción extra: lanza la tarea con ella';
    return `<div class="todoCompose">
      <textarea data-compose="${t.id}" rows="2" placeholder="${esc(ph)} · Enter envía, Shift+Enter nueva línea">${esc(drafts.get(t.id) || '')}</textarea>
      <button class="primary" data-act="compose" data-id="${t.id}" title="Enter">Enviar ↵</button></div>`;
  }

  /** The open task's side column below title + description: steps, then the advanced fold. */
  function details(t) {
    const needs = t.needs || [];
    const steps = t.steps || [];
    const ran = server && (t.sessionId || t.status === 'running');
    return `<div class="todoDetails">
      <label class="todoLabel">Pasos <span class="efHint">${ran ? 'uno nuevo se hace al acabar lo que esté haciendo, o ya si terminó' : '▶ Lanzar hace los que no estén hechos'}</span></label>
      <ul class="todoSteps">${steps.map((s, i) => stepRow(t, s, i)).join('')}</ul>
      <form class="todoAdd" data-form="addstep" data-id="${t.id}"><input id="todoStep-${t.id}" type="text" placeholder="${ran ? 'Nuevo paso (se lanza solo)' : 'Añadir paso'}" autocomplete="off"><button type="submit">Añadir</button></form>
      <details class="todoAdv" ${advOpen ? 'open' : ''}><summary>Prompt, modelo, qué necesita, documento</summary>
        <label class="todoLabel">Prompt <span class="efHint">vacío = título, descripción y pasos</span></label>
        <textarea class="todoPrompt" data-field="prompt" data-id="${t.id}" placeholder="Ej.: genera 10 bases de enemigos de hielo así y así, déjame la hoja de contacto">${esc(t.prompt || '')}</textarea>
        <label class="todoLabel">Imágenes de referencia <span class="efHint">Claude las mira antes de empezar</span></label>
        ${imageStrip(t.images || [], t.id)}
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
      t.prompt ? '<span class="todoMark" title="Tiene prompt">💬</span>' : '',
      t.images?.length ? `<span class="todoMark" title="Imágenes de referencia">🖼 ${t.images.length}</span>` : ''].join('');
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
    const busy = allTasks().filter(isRunning).length;
    const html = `${item('zone:claude', '🤖 Claude', busy ? `<b class="run" title="Trabajando o en cola">${busy}</b>` : '')}
      <h2>📝 Apuntes</h2>${apuntes().map((p) => {
      const left = p.tasks.filter((t) => !t.done).length;
      return item(`phase:${p.id}`, esc(p.name), left ? `<b>${left}</b>` : '');
    }).join('')}
      <h2>⚙ Talleres</h2>${TALLERES.map((tl) => {
      const ph = tallerPhase(tl);
      const run = (ph?.tasks || []).some(isRunning);
      const count = badges[tl.id] ? `<b class="hot" title="Esperan tu decisión">${badges[tl.id]}</b>` : run ? '<b class="run" title="Trabajando">●</b>' : '';
      return item(`taller:${tl.id}`, `${tl.icon} ${esc(tl.name)}`, count, tl.built ? '' : 'soon');
    }).join('')}
      <h2>🗂 Historial</h2>${item('zone:history', 'Todas las tareas', '')}`;
    if (html !== railHtml) { rail.innerHTML = html; railHtml = html; }   // redrawn every poll: only when it changed
  }

  /** Where a task lives, for the open task's header. */
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
    const active = ACTIVE.has(t.status);
    const runs = runsOf(t);
    const tok = runs.reduce((n, x) => n + tokensOf(x), 0);
    const del = confirmKey === `dt${t.id}`;
    detail.hidden = false;
    detail.innerHTML = `<div class="todoDetailHead"><span class="efHint">${esc(whereOf(r))} · ${esc(typeLabel(typeOf(t, r.ph)))}</span>${statusBadge(t)}
        ${server ? (active ? `<button class="small danger" data-act="cancel" data-id="${t.id}">⏹ ${t.status === 'running' ? 'Parar' : 'Quitar de la cola'}</button>`
          : `<button class="small primary" data-act="run" data-id="${t.id}" title="${t.sessionId ? 'Otra vez desde cero, en una sesión nueva' : 'Con su prompt'}${t.steps?.length ? ' (los pasos sin hacer)' : ''}">▶ ${t.sessionId ? 'Relanzar' : 'Lanzar'}</button>`) : ''}
        <button class="small" data-act="toggle" data-id="${t.id}">${t.done ? '↶ Reabrir' : '✓ Hecha'}</button>
        <button class="small ${del ? 'danger' : ''}" data-act="del-task" data-id="${t.id}">${del ? '¿Borrar la tarea?' : '🗑'}</button>
        <button class="small" data-act="close-detail" aria-label="Cerrar" title="Cerrar (Esc)">✕</button></div>
      <div class="todoDetailGrid">
        <div class="todoDetailSide">
          <input class="todoDetailTitle" type="text" data-field="text" data-id="${t.id}" value="${esc(t.text)}" aria-label="Título" autocomplete="off">
          <label class="todoLabel">Descripción</label>
          <textarea class="todoDetailNote" data-field="note" data-id="${t.id}" placeholder="Apunta la idea, el contexto, lo que tenga que saber Claude…">${esc(t.note)}</textarea>
          ${details(t)}
        </div>
        <div class="todoConversation">
          <label class="todoLabel">Claude ${runs.length ? `<span class="efHint">${runs.length} ejecución${runs.length === 1 ? '' : 'es'}${tok ? ` · ${kTok(tok)} tokens de salida` : ''}</span>` : ''}</label>
          <div class="todoThread">${thread(t) || (active ? '' : '<p class="todoEmpty">Aún no se ha lanzado.</p>')}<div data-live="${t.id}">${liveBox(t)}</div></div>
          ${composer(t)}
        </div>
      </div>`;
    const c = detail.querySelector('.todoConsole');
    if (c) c.scrollTop = c.scrollHeight;
  }

  /** 🤖 Claude: what runs, waits or is queued, then what finished lately (× takes one out; the history keeps it). */
  function renderClaude() {
    if (!claudeEl) return;
    if (!server) { claudeEl.innerHTML = '<p class="efHint">Con el servidor del taller (start.cmd) aquí ves lo que hace Claude.</p>'; return; }
    const order = new Map((live?.queue || []).map((q, i) => [q.taskId, i]).reverse());   // first place of each task
    const all = phases.flatMap((ph) => ph.tasks.map((t) => ({ ph, t })));
    const runningId = live?.running?.taskId;
    const rank = (t) => (t.id === runningId ? -2 : t.status === 'waiting' ? -1 : order.get(t.id) ?? 999);
    const act = all.filter(({ t }) => ACTIVE.has(t.status) || t.id === runningId).sort((a, b) => rank(a.t) - rank(b.t));
    const recent = all.filter(({ t }) => !act.some((x) => x.t === t) && t.result?.at && Date.now() - Date.parse(t.result.at) < 48 * 36e5 && dismissed[t.id] !== t.result.at)
      .sort((a, b) => Date.parse(b.t.result.at) - Date.parse(a.t.result.at)).slice(0, 8);
    const row = ({ ph, t }, isRecent) => {
      const running = t.id === runningId ? live.running : null;
      const n = queueOf(t.id).length;
      const info = running ? `${minutes(Date.now() - running.startedAt)} · ${esc(running.activity.at(-1)?.line || 'arrancando…')}${n ? ` · +${n} en cola` : ''}`
        : t.status === 'waiting' ? esc(live?.waiting?.taskId === t.id ? live.waiting.reason : t.waiting || 'esperando')
        : ACTIVE.has(t.status) ? (n > 1 ? `${n} en cola` : 'en cola')
        : `${esc(when(t.result.at))}${t.result.tokens ? ` · ↓ ${kTok(tokensOf(t.result))}` : ''} · ${esc(firstLine(t.result.text))}`;
      return `<li class="todoClaudeRow${sel === t.id ? ' sel' : ''}" data-open="${t.id}" title="Abrir">
        ${statusBadge(t.id === runningId ? { status: 'running' } : t)}<span class="todoClaudeTitle">${esc(t.text)}</span>
        <span class="todoClaudeWhere">${esc(whereOf({ ph }))}</span><span class="todoClaudeInfo">${info}</span>
        ${isRecent ? `<button class="small" data-act="dismiss" data-id="${t.id}" title="Quitar de aquí (sigue en el historial)" aria-label="Quitar">×</button>` : ''}</li>`;
    };
    const html = act.length || recent.length
      ? `<ul class="todoClaudeList">${act.map((x) => row(x, false)).join('')}${recent.length ? `<li class="todoClaudeSep">Terminadas hace poco</li>${recent.map((x) => row(x, true)).join('')}` : ''}</ul>`
      : '<p class="todoEmpty">Claude no está haciendo nada.</p>';
    if (html !== claudeHtml) { claudeEl.innerHTML = html; claudeHtml = html; }
  }

  /** The live parts only (every poll): 🤖 Claude, the open task's console / queue, the rail's counts. */
  function renderLive() {
    if (press.busy()) return;   // the next poll draws it
    renderClaude();
    renderRail();
    const r = sel ? find(sel) : null;
    const box = r && detail.querySelector(`[data-live="${sel}"]`);
    if (!box) return;
    const html = liveBox(r.t);
    if (box.innerHTML === html) return;
    box.innerHTML = html;
    const c = box.querySelector('.todoConsole');
    if (c) c.scrollTop = c.scrollHeight;
  }

  /** 🗂 Historial: one row per task that ran, its runs added up. */
  function renderHistory() {
    if (!historyEl) return;
    const rows = [];
    for (const ph of phases) for (const t of ph.tasks) {
      const runs = runsOf(t);
      if (!runs.length) continue;
      rows.push({ t, type: typeOf(t, ph), runs: runs.length, at: runs.at(-1).at, ok: runs.at(-1).ok,
        out: runs.reduce((n, x) => n + tokensOf(x), 0), tok: runs.reduce((n, x) => n + withCache(x), 0), ms: runs.reduce((n, x) => n + (x.durationMs || 0), 0) });
    }
    const types = Object.keys(TYPE_LABELS).concat([...new Set(rows.map((x) => x.type))]).filter((k, i, a) => a.indexOf(k) === i && rows.some((x) => x.type === k));
    if (hist.type !== 'all' && !types.includes(hist.type)) hist.type = 'all';
    const list = rows.filter((x) => (hist.type === 'all' || x.type === hist.type) && (hist.state === 'all' || (hist.state === 'ok') === !!x.ok));
    const by = { recent: (a, b) => String(b.at).localeCompare(String(a.at)), tokens: (a, b) => b.tok - a.tok, output: (a, b) => b.out - a.out, time: (a, b) => b.ms - a.ms };
    list.sort(by[hist.sort] || by.recent);
    const opt = (v, label, cur) => `<option value="${v}" ${v === cur ? 'selected' : ''}>${label}</option>`;
    const chip = (k, label, n) => `<button class="small${hist.type === k ? ' selected' : ''}" data-act="hist-type" data-id="${esc(k)}">${esc(label)} <span class="todoHistN">${n}</span></button>`;
    historyEl.innerHTML = `<div class="todoHistBar">${chip('all', 'Todas', rows.length)}${types.map((k) => chip(k, typeLabel(k), rows.filter((x) => x.type === k).length)).join('')}</div>
      <div class="todoRow todoHistTools">
        <label>Estado <select data-hist="state">${opt('all', 'todas', hist.state)}${opt('ok', 'bien', hist.state)}${opt('bad', 'fallidas / canceladas', hist.state)}</select></label>
        <label>Ordenar por <select data-hist="sort">${opt('recent', 'más reciente', hist.sort)}${opt('output', 'más tokens', hist.sort)}${opt('tokens', 'más tokens con caché', hist.sort)}${opt('time', 'más tiempo', hist.sort)}</select></label>
        <span class="efHint">${list.length} tarea${list.length === 1 ? '' : 's'} · ${kTok(list.reduce((n, x) => n + x.out, 0))} tokens de salida</span>
      </div>
      ${list.length ? `<div class="todoHistWrap"><table class="todoHistTable"><thead><tr><th>Última</th><th>Tipo</th><th>Tarea</th><th title="Lanzar, pasos e instrucciones">Ejec.</th><th>Tiempo</th><th title="Lo que Claude escribió, sumando todas sus ejecuciones (el ↓ de cada informe)">Tokens</th><th title="Entrada + caché leída y escrita + salida: enorme porque cada llamada relee todo el contexto">Con caché</th></tr></thead>
        <tbody>${list.slice(0, 200).map((x) => `<tr data-open="${x.t.id}" class="${x.ok ? '' : 'bad'}${sel === x.t.id ? ' sel' : ''}" title="Abrir">
          <td>${esc(when(x.at))}</td><td>${esc(typeLabel(x.type))}</td><td>${esc(x.t.text)}</td><td>${x.runs}</td><td>${x.ms ? minutes(x.ms) : ''}</td><td>${x.out ? kTok(x.out) : '—'}</td><td class="faintCell">${x.tok ? kTok(x.tok) : '—'}</td></tr>`).join('')}</tbody></table></div>`
        : '<p class="todoEmpty">Nada con ese filtro.</p>'}`;
  }

  function render() {
    renderList();
    renderRail();
    renderDetail();
    renderClaude();
    renderHistory();
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
    const wasOpen = !!sel;
    sel = id;
    if (id) sessionStorage.setItem(SEL_KEY, id); else sessionStorage.removeItem(SEL_KEY);
    render();
    if (narrow()) { if (id) detail.scrollTop = 0; return; }
    // The open task lives at the top of the page: go up to it, and back to where you were when it closes.
    if (id) {
      if (!wasOpen) listScroll = panelEl?.scrollTop ?? null;
      panelEl?.scrollTo({ top: 0, behavior: 'smooth' });
    } else if (panelEl && listScroll != null) { panelEl.scrollTop = listScroll; listScroll = null; }
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
    if (kind === 'zone') {
      document.getElementById(id === 'claude' ? 'todoZoneClaude' : 'todoZoneHistory')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } else if (kind === 'phase') {
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
    compose: (id) => {
      const box = detail.querySelector(`[data-compose="${id}"]`);
      const text = (box?.value || drafts.get(id) || '').trim();
      if (!text) { toast('Escribe la instrucción primero'); box?.focus(); return; }
      drafts.delete(id);
      if (box) { box.value = ''; box.blur(); }
      launch(id, { followUp: text });
    },
    unqueue: (id) => serverAction('unqueue', { qid: id }, 'Quitado de la cola'),
    dismiss: (id) => {
      const r = find(id);
      if (!r?.t.result) return;
      dismissed[id] = r.t.result.at;
      try { localStorage.setItem(DISMISS_KEY, JSON.stringify(dismissed)); } catch { /* private window */ }
      renderClaude();
    },
    'hist-type': (id) => { hist.type = id; saveHist(); renderHistory(); },
    cancel: (id) => serverAction('cancel', { taskId: id }, 'Cancelada'),
    'img-del': (id) => {
      const [owner, imgId] = [id.slice(0, id.lastIndexOf(':')), id.slice(id.lastIndexOf(':') + 1)];
      if (!owner) { quickImages = quickImages.filter((im) => im.id !== imgId); renderQuickImages(); return; }
      const r = find(owner);
      if (!r) return;
      r.t.images = (r.t.images || []).filter((im) => im.id !== imgId);
      if (!r.t.images.length) delete r.t.images;
      mut(() => {});
    },
    force: (id) => serverAction('force', { taskId: id }, 'Lanzando aunque haya una terminal abierta'),
  };

  function saveHist() { try { sessionStorage.setItem(HIST_KEY, JSON.stringify(hist)); } catch { /* private window */ } }

  // One set of listeners for the whole tab (apuntes, talleres, rail, the open task); designs-panel's areas are skipped.
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
    else if (d.dataset?.keep) { const k = d.dataset.keep; if (d.open) { kept.add(k); kept.delete(`-${k}`); } else { kept.delete(k); kept.add(`-${k}`); } }
  }, true);
  tab.addEventListener('input', (e) => { const id = e.target.dataset?.compose; if (id) drafts.set(id, e.target.value); });
  tab.addEventListener('keydown', (e) => {
    const t = e.target;
    if (!ours(t)) return;
    if (t.dataset?.compose && e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); ACTIONS.compose(t.dataset.compose); return; }
    if (e.key === 'Escape' && sel && !t.hasAttribute?.('data-edit') && !/^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName)) { select(null); return; }
    if (e.key === 'Enter' && t.dataset?.field === 'text') { e.preventDefault(); t.blur(); return; }
    if (!t.hasAttribute?.('data-edit')) return;
    if (e.key === 'Escape') { editing = null; render(); }
    else if (e.key === 'Enter' && (t.tagName === 'INPUT' || !e.shiftKey)) { e.preventDefault(); commit(t.value); }
  });
  tab.addEventListener('focusout', (e) => {
    if (!ours(e.target)) return;
    if (e.target.hasAttribute?.('data-edit')) commit(e.target.value);
    // Leaving a field for a button of the tab (Enviar, ▶…): a re-render now would swap the button under the
    // pointer and swallow the click — the action renders itself, or the next poll does.
    else if (e.relatedTarget && tab.contains(e.relatedTarget) && e.relatedTarget.closest('button, summary, label')) return;
    else if (pendingRender) setTimeout(() => { if (!focusedHere() && pendingRender) render(); }, 0);
  });
  // Drawer fields (title, description, prompt, doc, model, needs): saved when they change; only the list is
  // redrawn, never the drawer under your cursor.
  tab.addEventListener('change', (e) => {
    const el = e.target;
    if (!ours(el)) return;
    if (el.dataset.hist) { hist[el.dataset.hist] = el.value; saveHist(); renderHistory(); return; }
    if (el.dataset.attach !== undefined) { const files = [...el.files]; el.value = ''; attach(el.dataset.attach, files); return; }
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
      const step = { id: uid(), text: v, done: false };
      mut(() => { (r.t.steps ||= []).push(step); });
      // A task that already ran (or runs): the new step is the next instruction — after the current run, or now.
      // A full launch still queued will read it in its prompt anyway.
      const fullQueued = queueOf(r.t.id).some((q) => !q.stepId && !q.followUp);
      if (server && (r.t.status === 'running' || (r.t.sessionId && !fullQueued))) launch(r.t.id, { stepId: step.id });
    }
  });

  // Ctrl+V with an image on the clipboard: into the quick form if the cursor is there, else into the open task.
  document.addEventListener('paste', (e) => {
    if (root.closest('[hidden]') || !ours(e.target)) return;
    const files = [...(e.clipboardData?.files || [])].filter((f) => IMG_TYPES.includes(f.type));
    if (!files.length) return;               // plain text: let the field take it
    const owner = quick.contains(e.target) ? '' : detail.contains(e.target) && sel ? sel : null;
    if (owner === null) return;
    e.preventDefault();
    attach(owner, files);
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
    if (quickImages.length) task.images = quickImages;
    const needs = ['comfy', 'unity'].filter((n) => data.get(n));
    if (needs.length) task.needs = needs;
    if (data.get('model')) task.model = String(data.get('model'));
    if (data.get('effort')) task.effort = String(data.get('effort'));
    const andLaunch = e.submitter?.dataset.quick === 'launch';
    phases[i].tasks.push(task);
    form.reset();
    quickImages = [];
    renderQuickImages();
    select(task.id);
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
