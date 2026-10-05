// tools/taller-server/enemigos.mjs
// -----------------------------------------------------------------------------
// 🎨 Designs ▸ the enemy library: docs/enemigos/<Name>/enemigo.json, one per enemy that came out of an accepted
// design or was added from Unity / the web as "existing, with things to do". Full design: docs/architecture/designs.md.
//
//   { name, estado, prefab?, imagen?, diseno?: { image, source, lote, item, request, note }, pendientes: [Pendiente],
//     tarea?: <todo task id of the last build / fix run>, montaje?, createdAt,
//     cuerpo?: { fecha, plantilla, partes, brief, resumen }  (by steps: the body built in step 1),
//     paso2?: { tipo | plantilla, proyectil|sonidos|efectos: { origen: biblioteca|nuevo, texto }, notas } }
//   Pendiente = { id, tipo: comportamiento|sprite|gesto|ataque|bug|otro, texto, estado: abierto|en-curso|hecho,
//                 tarea?, resumen?, nota? }
//
// The web changes estado and pendientes; a task (make-enemy, or fixing pendientes) is queued through the same
// 📋 Tareas queue and updates the file itself when it ends (the prompts in design-prompts.mjs say how).

import fs from 'node:fs';
import path from 'node:path';
import { execFile } from 'node:child_process';
import { buildPrompt, bodyPrompt, restPrompt, pendingPrompt, PENDING_TYPES, BEHAVIORS } from './design-prompts.mjs';

const ESTADOS = ['por-montar', 'montando-cuerpo', 'cuerpo-por-aprobar', 'cuerpo-aprobado', 'montando', 'por-aprobar', 'con-pendientes', 'aprobado', 'descartado'];
const PHASE = { id: 'enemies', name: '👾 Enemigos', tag: 'Web' };
const uid = () => Math.random().toString(36).slice(2, 9);

export class Enemigos {
  constructor({ projectRoot, store, runner }) {
    this.projectRoot = projectRoot;
    this.dir = path.join(projectRoot, 'docs', 'enemigos');
    this.store = store;
    this.runner = runner;
  }

  list() {
    return readDir(this.dir).map((d) => readJson(this._file(d))).filter(Boolean)
      .map((e) => ({ ...e, pendientes: Array.isArray(e.pendientes) ? e.pendientes : [] }))
      .sort((a, b) => String(b.createdAt || '').localeCompare(String(a.createdAt || '')));
  }

  _file(name) {
    if (!/^[A-Za-z][\w-]*$/.test(name)) throw new Error('nombre inválido');
    return path.join(this.dir, name, 'enemigo.json');
  }

  _get(name) {
    const e = readJson(this._file(name));
    if (!e) throw new Error(`No encuentro el enemigo ${name}`);
    e.pendientes = Array.isArray(e.pendientes) ? e.pendientes : [];
    return e;
  }

  _put(e) { writeJson(this._file(e.name), e); return e; }

  // ------------------------------------------------------------ what the web changes

  /** op: estado | add-pendiente | edit-pendiente | remove-pendiente */
  update({ name, op, estado, tipo, texto, id }) {
    const e = this._get(name);
    if (op === 'estado') {
      if (!ESTADOS.includes(estado)) throw new Error('estado inválido');
      e.estado = estado;
    } else if (op === 'add-pendiente') {
      if (!PENDING_TYPES[tipo]) throw new Error('tipo de pendiente inválido');
      if (!String(texto || '').trim()) throw new Error('Escribe qué falta');
      e.pendientes.push({ id: uid(), tipo, texto: String(texto).trim(), estado: 'abierto', createdAt: new Date().toISOString() });
      if (e.estado === 'aprobado' || e.estado === 'por-aprobar') e.estado = 'con-pendientes';
    } else if (op === 'edit-pendiente') {
      const p = e.pendientes.find((x) => x.id === id);
      if (!p) throw new Error('No encuentro ese pendiente');
      if (tipo && PENDING_TYPES[tipo]) p.tipo = tipo;
      if (texto != null && String(texto).trim()) p.texto = String(texto).trim();
      if (estado && ['abierto', 'hecho'].includes(estado)) p.estado = estado;
    } else if (op === 'remove-pendiente') {
      e.pendientes = e.pendientes.filter((x) => x.id !== id);
    } else throw new Error('operación inválida');
    return this._put(e);
  }

  // ------------------------------------------------------------ tasks

  /** make-enemy from the approved design → estado "montando" until the task writes "por-aprobar". */
  build({ name, model, effort }) {
    const e = this._get(name);
    if (!e.diseno?.source) throw new Error('Este enemigo no tiene diseño del que partir');
    const task = this.store.addTask(PHASE, {
      text: `Montar ${name} (make-enemy)`, prompt: buildPrompt(e), needs: ['comfy', 'unity'],
      model: model || 'sonnet', effort: effort || 'medium', enemy: name,
    });
    e.estado = 'montando';
    e.tarea = task.id;
    this._put(e);
    return this._queue(task);
  }

  /**
   * By steps, step 1: only the body (parts + assembly + a body-only prefab) → "montando-cuerpo" until the task writes
   * "cuerpo-por-aprobar". Also redoes the body (sprite pendientes are the notes).
   */
  buildBody({ name, model, effort }) {
    const e = this._get(name);
    if (!e.diseno?.source) throw new Error('Este enemigo no tiene diseño del que partir');
    const task = this.store.addTask(PHASE, {
      text: `Cuerpo de ${name} (make-enemy, paso 1)`, prompt: bodyPrompt(e), needs: ['comfy', 'unity'],
      model: model || 'sonnet', effort: effort || 'medium', enemy: name,
    });
    e.estado = 'montando-cuerpo';
    e.tarea = task.id;
    this._put(e);
    return this._queue(task);
  }

  /**
   * By steps, step 3: the user approved the body and chose the kind of enemy and where its assets come from.
   * `paso2` = { tipo | plantilla, proyectil|sonidos|efectos: { origen, texto }, notas } → "montando".
   */
  buildRest({ name, paso2, model, effort }) {
    const e = this._get(name);
    if (!['cuerpo-aprobado', 'montando'].includes(e.estado)) throw new Error('Aprueba primero el cuerpo');
    const p = paso2 || {};
    const heir = String(p.plantilla || '').trim();
    if (heir) this._get(heir);   // it has to exist in the library
    else if (!BEHAVIORS[p.tipo]) throw new Error('Elige el tipo de enemigo (o una plantilla de la que heredar)');
    const asset = (a) => ({ origen: a?.origen === 'nuevo' ? 'nuevo' : 'biblioteca', texto: String(a?.texto || '').trim(),
      elegidos: (Array.isArray(a?.elegidos) ? a.elegidos : []).map((c) => ({ id: String(c.id || ''), name: String(c.name || '') })).filter((c) => c.id) });
    e.paso2 = { ...(heir ? { plantilla: heir } : { tipo: p.tipo }), proyectil: asset(p.proyectil), sonidos: asset(p.sonidos), efectos: asset(p.efectos), notas: String(p.notas || '').trim() };
    const task = this.store.addTask(PHASE, {
      text: `Terminar ${name} (make-enemy, paso 3)`, prompt: restPrompt(e), needs: ['comfy', 'unity'],
      model: model || 'sonnet', effort: effort || 'medium', enemy: name,
    });
    e.estado = 'montando';
    e.tarea = task.id;
    this._put(e);
    return this._queue(task);
  }

  /** A task for the open pendientes (all, or `ids`). */
  work({ name, ids, model, effort }) {
    const e = this._get(name);
    const items = e.pendientes.filter((p) => p.estado !== 'hecho' && (!ids?.length || ids.includes(p.id)));
    if (!items.length) throw new Error('No hay pendientes abiertos');
    const needs = ['unity', ...(items.some((p) => p.tipo === 'sprite' || p.tipo === 'gesto') ? ['comfy'] : [])];
    const task = this.store.addTask(PHASE, {
      text: `${name}: ${items.map((p) => PENDING_TYPES[p.tipo].toLowerCase()).filter((v, i, a) => a.indexOf(v) === i).join(', ')} (${items.length} pendiente${items.length === 1 ? '' : 's'})`,
      prompt: pendingPrompt(e, items), needs, model: model || 'sonnet', effort: effort || 'medium', enemy: name,
    });
    items.forEach((p) => { p.estado = 'en-curso'; p.tarea = task.id; });
    e.estado = 'con-pendientes';
    e.tarea = task.id;
    this._put(e);
    return this._queue(task);
  }

  _queue(task) {
    const error = this.runner.enqueue({ taskId: task.id });
    if (error) throw new Error(error);
    return { taskId: task.id };
  }

  /** Statuses of the tasks the enemies point at, for the cards ("en cola", "trabajando", "falló"). */
  taskStates(ids) {
    const out = {};
    for (const id of new Set(ids.filter(Boolean))) {
      const t = this.store.find(id)?.task;
      if (t) out[id] = { status: t.status || 'pending', text: t.text, ok: t.result?.ok ?? null };
    }
    return out;
  }

  // ------------------------------------------------------------ existing enemies (from the prefabs)

  /** Enemy prefabs in Assets/Prefabs/Enemies that are not in the library yet. */
  prefabs() {
    const have = new Set(this.list().flatMap((e) => [e.name.toLowerCase(), String(e.prefab || '').toLowerCase()]));
    const root = path.join(this.projectRoot, 'Assets', 'Prefabs', 'Enemies');
    const out = [];
    const walk = (d) => readDir(d).forEach((f) => {
      const p = path.join(d, f);
      let st;
      try { st = fs.statSync(p); } catch { return; }
      if (st.isDirectory()) walk(p);
      else if (f.endsWith('.prefab')) {
        const rel = path.relative(this.projectRoot, p).replace(/\\/g, '/');
        const name = nameFromPrefab(f);
        if (!have.has(name.toLowerCase()) && !have.has(rel.toLowerCase())) out.push({ name, prefab: rel });
      }
    });
    walk(root);
    return out.sort((a, b) => a.name.localeCompare(b.name));
  }

  /** Adds an existing prefab to the library (estado "con-pendientes"); its picture comes from Unity. */
  addExisting({ prefab }) {
    const found = this.prefabs().find((p) => p.prefab === prefab);
    if (!found) throw new Error('Ese prefab no existe o ya está en la biblioteca');
    const e = this._put({ name: found.name, estado: 'con-pendientes', prefab: found.prefab, pendientes: [], createdAt: new Date().toISOString(), origen: 'web' });
    this.refreshPicture(e.name);
    return e;
  }

  /**
   * Asks Unity (if it is open) to render the prefab into docs/enemigos/<Name>/preview.png
   * (EnemyPreviewExporter, the same thing the Unity menu does). Best effort, in the background.
   */
  refreshPicture(name) {
    const e = this._get(name);
    if (!e.prefab) return;
    const out = path.join(this.dir, name, 'preview.png').replace(/\\/g, '/');
    const code = `return RedMagic.Enemies.EditorTools.EnemyPreviewExporter.Export(@"${e.prefab}", @"${out}");`;
    execFile(process.platform === 'win32' ? 'unity.exe' : 'unity', ['command', 'eval', '--timeout', '60', '--code', code], { timeout: 90000, windowsHide: true, cwd: this.projectRoot }, (err) => {
      if (err) return;
      try {
        const fresh = this._get(name);
        if (fs.existsSync(out)) { fresh.imagen = `docs/enemigos/${name}/preview.png`; this._put(fresh); }
      } catch { /* deleted meanwhile */ }
    });
  }
}

export function nameFromPrefab(file) {
  return path.basename(file, '.prefab').replace(/^(Enemy|Boss)_/, '');
}

function readDir(dir) { try { return fs.readdirSync(dir); } catch { return []; } }
function readJson(file) { try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch { return null; } }
function writeJson(file, obj) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, `${JSON.stringify(obj, null, 2)}\n`);
}
