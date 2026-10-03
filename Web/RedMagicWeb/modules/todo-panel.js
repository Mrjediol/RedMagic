// modules/todo-panel.js
// -----------------------------------------------------------------------------
// 📋 Tareas: the project's to-do / roadmap. Phases → tasks (text, note, done).
//
// Where it lives: the browser (localStorage) always, AND — once linked — a real file of the repo,
// `docs/tareas/todo.json`, through the File System Access API (Chromium). The file is the source of
// truth: Claude reads it to know what is pending and can edit it; the tab reloads it when the window
// regains focus (unless there are unsaved edits). Without a linked file it still works, saved in the
// browser only.
//
// Data (`DATA_VERSION`): { v, updated, phases: [{ id, name, tag, tasks: [{ id, text, note, done }] }] }.
// Later fields (steps, doc, prompt, status, result) are additive: unknown keys are kept as they came.

export const DATA_VERSION = 1;
export const FILE_NAME = 'todo.json';
const LS_KEY = 'redmagic.todo.v1';
const DB_NAME = 'redmagic-todo';

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

const clone = (x) => JSON.parse(JSON.stringify(x));
const uid = () => Math.random().toString(36).slice(2, 9);
const esc = (s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const swap = (arr, i, j) => { if (j >= 0 && j < arr.length) [arr[i], arr[j]] = [arr[j], arr[i]]; };

/** A document from anything that might be one (the file, an import, localStorage): phases or null. */
export function readDocument(raw) {
  try {
    const doc = typeof raw === 'string' ? JSON.parse(raw) : raw;
    const phases = Array.isArray(doc) ? doc : doc?.phases;
    if (!Array.isArray(phases)) return null;
    return phases.filter((p) => p && typeof p === 'object').map((p) => ({
      ...p, id: p.id || uid(), name: String(p.name ?? ''), tag: String(p.tag ?? ''),
      tasks: (Array.isArray(p.tasks) ? p.tasks : []).filter((t) => t && typeof t === 'object')
        .map((t) => ({ ...t, id: t.id || uid(), text: String(t.text ?? ''), note: String(t.note ?? ''), done: !!t.done })),
    }));
  } catch { return null; }
}

export function writeDocument(phases) {
  return JSON.stringify({ v: DATA_VERSION, updated: new Date().toISOString(), phases }, null, 2) + '\n';
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
    value === undefined ? tx.objectStore('kv').delete(key) : tx.objectStore('kv').put(value, key);
    tx.oncomplete = () => resolve();
    tx.onerror = () => resolve();
  });
}

// ============================================================ the panel

/**
 * @param {{ root: HTMLElement, statusEl: HTMLElement, toolbar: HTMLElement, toast?: (text: string) => void }} ctx
 * @returns {{ onShow(): void, flush(): Promise<void> }}
 */
export function initTodoPanel({ root, statusEl, toolbar, toast = () => {} }) {
  let phases = clone(DEFAULT_PHASES);
  let handle = null;          // linked FileSystemFileHandle
  let fileStamp = 0;          // lastModified of the file as we last read / wrote it
  let needsPermission = false;
  let editing = null, confirmKey = null, confirmTimer = null, focusId = null, hideDone = false;
  let dirty = false, saving = false, saveTimer = null;

  const setStatus = (text) => { statusEl.textContent = text; };
  const where = () => (handle ? `docs/tareas/${handle.name}` : 'solo este navegador');

  function find(tid) {
    for (const ph of phases) {
      const i = ph.tasks.findIndex((t) => t.id === tid);
      if (i >= 0) return { ph, i, t: ph.tasks[i] };
    }
    return null;
  }
  const phaseIdx = (pid) => phases.findIndex((p) => p.id === pid);

  // ------------------------------------------------------------ saving / loading

  function queueSave() {
    dirty = true;
    setStatus('Guardando…');
    clearTimeout(saveTimer);
    saveTimer = setTimeout(flush, 450);
  }

  async function flush() {
    clearTimeout(saveTimer);
    if (saving) { saveTimer = setTimeout(flush, 300); return; }
    dirty = false;
    saving = true;
    const text = writeDocument(phases);
    try { localStorage.setItem(LS_KEY, text); } catch { /* storage full or private window */ }
    let where_ = 'solo este navegador';
    if (handle && !needsPermission) {
      try {
        const writable = await handle.createWritable();
        await writable.write(text);
        await writable.close();
        fileStamp = (await handle.getFile()).lastModified;
        where_ = `docs/tareas/${handle.name}`;
      } catch (e) {
        setStatus(`No se pudo escribir ${handle.name}: ${e.message}. Se guardó en el navegador.`);
        saving = false;
        return;
      }
    }
    saving = false;
    if (dirty) queueSave();
    else setStatus(`Guardado · ${where_}`);
  }

  /** Loads the linked file if it changed since we last saw it (Claude or you edited it outside) and nothing is pending here. */
  async function pullFile(force = false) {
    if (!handle || dirty || saving || editing) return;
    try {
      if ((await handle.queryPermission({ mode: 'readwrite' })) !== 'granted') { needsPermission = true; renderToolbar(); return; }
      needsPermission = false;
      const file = await handle.getFile();
      if (!force && file.lastModified === fileStamp) return;
      const loaded = readDocument(await file.text());
      fileStamp = file.lastModified;
      if (!loaded) { setStatus(`${handle.name} no tiene el formato de la lista: no se ha cargado.`); return; }
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
      else await flush();   // an empty file: start it with what is on screen
      toast(`Enlazado: ${handle.name}`);
    } catch (e) {
      if (e?.name !== 'AbortError') toast(`No se pudo enlazar: ${e.message}`);
    }
    renderToolbar();
  }

  async function reconnect() {
    if (!handle) return;
    try {
      if ((await handle.requestPermission({ mode: 'readwrite' })) === 'granted') {
        needsPermission = false;
        await pullFile(true);
      }
    } catch (e) { toast(`No se pudo reconectar: ${e.message}`); }
    renderToolbar();
  }

  async function unlink() {
    handle = null;
    needsPermission = false;
    await kvSet('handle', undefined);
    setStatus('Guardado · solo este navegador');
    renderToolbar();
  }

  function exportJson() {
    const url = URL.createObjectURL(new Blob([writeDocument(phases)], { type: 'application/json' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = FILE_NAME;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  function importJson(file) {
    file.text().then((text) => {
      const loaded = readDocument(text);
      if (!loaded) { toast('Ese archivo no tiene el formato de la lista'); return; }
      mut(() => { phases = loaded; });
      toast('Lista importada');
    });
  }

  // ------------------------------------------------------------ render

  function mut(fn) { fn(); render(); queueSave(); }

  function renderToolbar() {
    const linked = handle
      ? (needsPermission
        ? '<button data-act="reconnect" class="primary" title="El navegador pide permiso otra vez en cada sesión">🔓 Reconectar con el archivo</button>'
        : `<button data-act="unlink" title="Dejar de escribir en el archivo del repo">🔗 ${esc(handle.name)} · desenlazar</button>`)
      : `<button data-act="link" class="${hasFileApi() ? 'primary' : ''}" ${hasFileApi() ? '' : 'disabled'} title="Elige docs/tareas/todo.json del repo: así Claude lee y escribe la misma lista">🔗 Enlazar docs/tareas/todo.json…</button>`;
    toolbar.innerHTML = `${linked}
      <button data-act="hide-done" aria-pressed="${hideDone}" class="${hideDone ? 'active' : ''}">Ocultar hechas</button>
      <span class="tabSpacer"></span>
      <button data-act="export" title="Descarga la lista como todo.json">⬇ Exportar</button>
      <button data-act="import" title="Carga una lista (todo.json) sustituyendo la actual">⬆ Importar…</button>
      <button data-act="reset" class="${confirmKey === 'reset' ? 'danger' : ''}">${confirmKey === 'reset' ? '¿Seguro? Se pierden tus cambios' : 'Restaurar lista original'}</button>`;
  }

  function render() {
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
        return `<li class="todoTask${t.done ? ' done' : ''}">
          <button class="todoCheck" data-act="toggle" data-id="${t.id}" aria-pressed="${t.done}" aria-label="Marcar como hecha">✓</button>
          <div class="todoBody">${text}${note}</div>
          <div class="todoTools"><button class="small" data-act="up-task" data-id="${t.id}" aria-label="Subir">↑</button><button class="small" data-act="down-task" data-id="${t.id}" aria-label="Bajar">↓</button>${dbtn}</div>
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
  }

  function commit(val) {
    if (!editing) return;
    const ed = editing;
    editing = null;
    val = (val || '').trim();
    const r = ed.kind === 'text' || ed.kind === 'note' ? find(ed.id) : null;
    const pi = ed.kind === 'pname' || ed.kind === 'ptag' ? phaseIdx(ed.id) : -1;
    if (ed.kind === 'text' && r && val) r.t.text = val;
    else if (ed.kind === 'note' && r) r.t.note = val;
    else if (ed.kind === 'pname' && pi > -1 && val) phases[pi].name = val;
    else if (ed.kind === 'ptag' && pi > -1) phases[pi].tag = val;
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

  const ACTIONS = {
    toggle: (id) => { const r = find(id); if (r) mut(() => { r.t.done = !r.t.done; }); },
    'edit-text': (id) => { editing = { kind: 'text', id }; render(); },
    'edit-note': (id) => { editing = { kind: 'note', id }; render(); },
    'edit-pname': (id) => { editing = { kind: 'pname', id }; render(); },
    'edit-ptag': (id) => { editing = { kind: 'ptag', id }; render(); },
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
  root.addEventListener('focusout', (e) => { if (e.target.hasAttribute?.('data-edit')) commit(e.target.value); });
  root.addEventListener('submit', (e) => {
    e.preventDefault();
    const f = e.target;
    if (f.dataset.form !== 'addtask') return;
    const v = f.querySelector('input').value.trim();
    const i = phaseIdx(f.dataset.id);
    if (!v || i < 0) return;
    focusId = `todoAdd-${f.dataset.id}`;
    mut(() => phases[i].tasks.push({ id: uid(), text: v, note: '', done: false }));
  });

  document.getElementById('todoPhaseForm')?.addEventListener('submit', (e) => {
    e.preventDefault();
    const input = document.getElementById('todoNewPhase');
    const v = input.value.trim();
    if (!v) return;
    input.value = '';
    mut(() => phases.push({ id: uid(), name: v, tag: '', tasks: [] }));
  });

  const importInput = document.createElement('input');
  importInput.type = 'file';
  importInput.accept = '.json,application/json';
  importInput.hidden = true;
  importInput.onchange = () => { if (importInput.files[0]) importJson(importInput.files[0]); importInput.value = ''; };
  toolbar.after(importInput);

  const TOOLBAR = {
    link: linkFile,
    reconnect,
    unlink,
    export: exportJson,
    import: () => importInput.click(),
    'hide-done': () => { hideDone = !hideDone; render(); },
    reset: () => {
      if (confirmKey !== 'reset') { arm('reset'); return; }
      confirmKey = null;
      mut(() => { phases = clone(DEFAULT_PHASES); });
    },
  };
  toolbar.addEventListener('click', (e) => {
    const b = e.target.closest('[data-act]');
    if (b && TOOLBAR[b.dataset.act]) TOOLBAR[b.dataset.act]();
  });

  window.addEventListener('focus', () => { if (!root.closest('[hidden]')) pullFile(); });

  // ------------------------------------------------------------ start

  (async function start() {
    try {
      const raw = localStorage.getItem(LS_KEY);
      const stored = raw ? readDocument(raw) : null;
      if (stored) phases = stored;
    } catch { /* no storage */ }
    render();
    setStatus('Guardado · solo este navegador');
    try {
      handle = (await kvGet('handle')) || null;
      if (handle) await pullFile(true);
    } catch { handle = null; }
    renderToolbar();
  }());

  return {
    onShow: () => pullFile(),
    flush,
  };
}
