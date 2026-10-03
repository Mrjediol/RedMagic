// tools/taller-server/store.mjs
// -----------------------------------------------------------------------------
// docs/tareas/todo.json in memory: the one copy the web, the queue and Claude (editing the file by hand)
// all go through.
//
// Who owns what. The USER owns a task's words (text, note, prompt, steps' text and ticks, needs, model…);
// the QUEUE owns its run state (status, waiting, result, sessionId, and the same on each step). A save from
// the web therefore never overwrites run state — it is merged over the current copy (`mergeFromWeb`) — and
// tasks the web did not know about (Claude added them to the file meanwhile) are kept instead of deleted.
// The file is watched: an edit from outside reloads it and bumps `rev`, which the web polls.

import fs from 'node:fs';
import path from 'node:path';

export const RUN_FIELDS = ['status', 'waiting', 'result', 'history', 'sessionId', 'lastRunId'];
const STEP_RUN_FIELDS = ['status', 'result'];
const uid = () => Math.random().toString(36).slice(2, 9);

const pick = (obj, keys) => Object.fromEntries(keys.filter((k) => obj?.[k] !== undefined).map((k) => [k, obj[k]]));
const omit = (obj, keys) => Object.fromEntries(Object.entries(obj || {}).filter(([k]) => !keys.includes(k)));

/** Same cleaning as the web's readDocument: every phase / task / step has an id and its basic fields. */
export function normalize(doc) {
  const phases = (Array.isArray(doc?.phases) ? doc.phases : []).filter((p) => p && typeof p === 'object').map((p) => ({
    ...p, id: p.id || uid(), name: String(p.name ?? ''), tag: String(p.tag ?? ''),
    tasks: (Array.isArray(p.tasks) ? p.tasks : []).filter((t) => t && typeof t === 'object').map((t) => ({
      ...t, id: t.id || uid(), text: String(t.text ?? ''), note: String(t.note ?? ''), done: !!t.done,
      ...(Array.isArray(t.steps) ? { steps: t.steps.filter((s) => s && typeof s === 'object').map((s) => ({ ...s, id: s.id || uid(), text: String(s.text ?? ''), done: !!s.done })) } : {}),
    })),
  }));
  const queue = (Array.isArray(doc?.queue) ? doc.queue : []).filter((q) => q && q.taskId);
  return { v: doc?.v || 1, updated: doc?.updated || new Date().toISOString(), phases, queue };
}

export class Store {
  constructor(file) {
    this.file = file;
    this.rev = 0;
    this.doc = normalize({});
    this._writtenMtime = 0;
    this._listeners = new Set();
    this.load();
    fs.watchFile(file, { interval: 800 }, (cur) => {
      if (!cur.mtimeMs || Math.abs(cur.mtimeMs - this._writtenMtime) < 1) return;   // our own write
      try {
        this.load();
        this._emit('external');
      } catch (e) {
        console.warn(`[store] ${path.basename(file)} cambió pero no se pudo leer: ${e.message}`);
      }
    });
  }

  load() {
    const text = fs.existsSync(this.file) ? fs.readFileSync(this.file, 'utf8') : '';
    this.doc = normalize(text.trim() ? JSON.parse(text) : {});
    this._writtenMtime = fs.existsSync(this.file) ? fs.statSync(this.file).mtimeMs : 0;
    this.rev++;
  }

  save() {
    this.doc.updated = new Date().toISOString();
    const text = JSON.stringify(this.doc, null, 2) + '\n';
    const tmp = `${this.file}.tmp`;
    fs.mkdirSync(path.dirname(this.file), { recursive: true });
    fs.writeFileSync(tmp, text);
    fs.renameSync(tmp, this.file);
    this._writtenMtime = fs.statSync(this.file).mtimeMs;
    this.rev++;
    this._emit('save');
  }

  onChange(fn) { this._listeners.add(fn); return () => this._listeners.delete(fn); }
  _emit(why) { for (const fn of this._listeners) { try { fn(why); } catch (e) { console.error(e); } } }

  find(taskId) {
    for (const phase of this.doc.phases) {
      const task = phase.tasks.find((t) => t.id === taskId);
      if (task) return { phase, task };
    }
    return null;
  }

  /** Changes the run state of a task (or of one of its steps) and saves. */
  setRun(taskId, stepId, fields) {
    const found = this.find(taskId);
    if (!found) return;
    const target = stepId ? found.task.steps?.find((s) => s.id === stepId) : found.task;
    if (!target) return;
    for (const [k, v] of Object.entries(fields)) {
      if (v === undefined) delete target[k];
      else target[k] = v;
    }
    this.save();
  }

  /**
   * A save from the web: its phases in its order, its words, and the run state of the copy here. Tasks and
   * phases it never saw (`known*` = what it had loaded) are kept where they are; known ones it no longer
   * has were deleted there.
   */
  mergeFromWeb({ phases, knownTaskIds = [], knownPhaseIds = [] }) {
    const incoming = normalize({ phases }).phases;
    const knownTasks = new Set(knownTaskIds);
    const knownPhases = new Set(knownPhaseIds);
    const current = new Map();
    for (const p of this.doc.phases) for (const t of p.tasks) current.set(t.id, t);
    const inWeb = new Set(incoming.flatMap((p) => p.tasks.map((t) => t.id)));

    const mergeTask = (t) => {
      const here = current.get(t.id);
      if (!here) return omit(t, RUN_FIELDS);
      const merged = { ...omit(t, RUN_FIELDS), ...pick(here, RUN_FIELDS) };
      if (Array.isArray(t.steps)) {
        const hereSteps = new Map((here.steps || []).map((s) => [s.id, s]));
        merged.steps = t.steps.map((s) => ({ ...omit(s, STEP_RUN_FIELDS), ...pick(hereSteps.get(s.id), STEP_RUN_FIELDS) }));
      }
      return merged;
    };

    const result = incoming.map((p) => ({ ...p, tasks: p.tasks.map(mergeTask) }));
    // What appeared here (Claude edited the file) while the web had the old list: keep it.
    for (const p of this.doc.phases) {
      const fresh = p.tasks.filter((t) => !knownTasks.has(t.id) && !inWeb.has(t.id));
      const target = result.find((r) => r.id === p.id);
      if (target) target.tasks.push(...fresh);
      else if (!knownPhases.has(p.id)) result.push({ ...p, tasks: p.tasks.filter((t) => !knownTasks.has(t.id) || inWeb.has(t.id)) });
      else if (fresh.length) result.push({ ...p, tasks: fresh });
    }

    this.doc.phases = result;
    const alive = new Set(result.flatMap((p) => p.tasks.map((t) => t.id)));
    this.doc.queue = this.doc.queue.filter((q) => alive.has(q.taskId));
    this.save();
  }
}
