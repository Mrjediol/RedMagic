// tools/taller-server/runner.mjs
// -----------------------------------------------------------------------------
// The queue: one Claude Code session at a time, unattended (`claude -p`), in the project folder.
//
// Before each one it checks what the task needs (ComfyUI, Unity — opening them when they are closed) and
// that no interactive Claude Code session is open in a terminal: two sessions on the same repo would step
// on each other's files and commits, so the queue WAITS (and re-checks) until the terminal closes, unless
// the user says "lanzar igualmente". Permissions: `--permission-mode auto` (safe actions run, risky ones are
// refused by the classifier) + `--permission-prompts none` (nothing can stop to ask: it is denied) + an
// explicit deny list (never push, never rewrite history — CLAUDE.md). What it prints at the end is the
// task's result; the whole stream goes to Logs/Taller/<run>.jsonl.
// Stuck runs: every run is told the no-progress rule (STUCK), and a watchdog (_watch) stops one that goes in circles
// (same error / failing calls / same call over and over) with a "⛔ Atascada" report; an instruction resumes its session.

import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawn, execFile } from 'node:child_process';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const HISTORY_MAX = 40;   // earlier reports kept per task

// Every run, resumed ones too: tokens are the plan's budget, so no narration and a short report.
const TERSE = 'Unattended run from the project web. Save tokens: do not narrate, explain or teach while working; no text between tool calls '
  + 'unless something blocks you; do not restate the task or summarise what you read. The only prose is the final report.';
// The no-progress rule (CLAUDE.md ▸ Hard rules), said to every run; the watchdog below is the safety net when it is not kept.
const STUCK = 'No-progress rule: if the same thing fails 3 times without getting better (a part that will not separate, a fix that does not fix), '
  + 'or a service (Unity, ComfyUI, an API) does not answer after 2 tries, STOP that piece: leave it noted, do the rest of the task, and say it in the report. '
  + 'If it blocks everything, stop the task. Never retry the same thing hoping for a different result. A report after a stop starts with '
  + '"⛔ Bloqueada en: <where>" + the cause + "Para seguir: <what the user can do>"; the user then sends an instruction and the session resumes there.';
const REPORT ='Your LAST message is the report the user reads on the web: Spanish, terse, at most ~8 short lines — what was done, where it is, '
  + 'decisions you took on your own, what to test (short list). No preamble, no explanations, no recap of the task.';

export class Runner {
  /**
   * @param {import('./store.mjs').Store} store
   * @param {{ projectRoot: string, logDir: string, claudeExe: string, comfyExe: string|null, config: object }} opts
   */
  constructor(store, opts) {
    this.store = store;
    this.opts = opts;
    this.current = null;          // { taskId, stepId, runId, child, startedAt, activity: [] }
    this.waiting = null;          // { taskId, reason } — the head of the queue cannot start yet
    this.children = new Set();    // PIDs of the sessions we started (not "interactive")
    this.force = new Set();       // task ids allowed to start with a terminal session open
    this._timer = null;
    this.usage = this._loadUsage();   // { fiveHour, sevenDay: { utilization, resetsAt }, at } — the plan's real limits
    // A run that was going when the server stopped did not finish.
    for (const p of store.doc.phases) for (const t of p.tasks) {
      if (t.status === 'running') { t.status = 'failed'; t.result = { ok: false, text: 'Interrumpida: el servidor se cerró mientras trabajaba.', at: new Date().toISOString() }; }
      for (const s of t.steps || []) if (s.status === 'running') s.status = 'failed';
    }
    store.save();
    this.kick();
  }

  // ------------------------------------------------------------ the queue

  /**
   * Adds a run: the whole task, one step of it, or an instruction for it (`followUp`). An instruction runs in the
   * task's session (resumed) when it has one by then, else it starts the task with the instruction appended. While
   * the task is running or queued, whatever is added goes right behind it — "do this when you finish", like typing
   * in a Claude console.
   * @returns {string|null} an error for the user, or null
   */
  enqueue({ taskId, stepId = null, followUp = null, force = false }) {
    const found = this.store.find(taskId);
    if (!found) return 'No existe esa tarea.';
    followUp = followUp ? String(followUp).trim() || null : null;
    const queue = this.store.doc.queue;
    if (!followUp && queue.some((q) => q.taskId === taskId && q.stepId === stepId && !q.followUp)) return 'Ya está en la cola.';
    const item = { qid: crypto.randomBytes(4).toString('hex'), taskId, stepId, followUp, queuedAt: new Date().toISOString() };
    // Behind what this task already has queued (or first, if it is the one running): its session goes on before
    // other tasks get in between.
    let at = -1;
    queue.forEach((q, i) => { if (q.taskId === taskId) at = i; });
    if (at >= 0) queue.splice(at + 1, 0, item);
    else if (this.current?.taskId === taskId) queue.unshift(item);
    else queue.push(item);
    if (force) this.force.add(taskId);
    const target = stepId ? found.task.steps?.find((s) => s.id === stepId) : found.task;
    if (target && target.status !== 'running') target.status = 'queued';
    this.store.save();
    this.kick();
    return null;
  }

  /** Takes one queued run (an instruction, a step) out of the queue. */
  unqueue(qid) {
    const q = this.store.doc.queue.find((x) => x.qid === qid);
    if (!q) return;
    this.store.doc.queue = this.store.doc.queue.filter((x) => x !== q);
    const found = this.store.find(q.taskId);
    if (found) {
      const step = q.stepId ? found.task.steps?.find((s) => s.id === q.stepId) : null;
      if (step?.status === 'queued') step.status = 'cancelled';
      const left = this.store.doc.queue.some((x) => x.taskId === q.taskId);
      if (!left && this.current?.taskId !== q.taskId && found.task.status === 'queued') {
        found.task.status = found.task.result ? (found.task.result.ok ? 'done' : 'failed') : 'cancelled';
      }
    }
    this.store.save();
  }

  /** Takes a task out of the queue, or stops it if it is the one running. */
  cancel(taskId) {
    const before = this.store.doc.queue.length;
    this.store.doc.queue = this.store.doc.queue.filter((q) => q.taskId !== taskId);
    const found = this.store.find(taskId);
    if (found) {
      for (const t of [found.task, ...(found.task.steps || [])]) if (t.status === 'queued' || t.status === 'waiting') t.status = 'cancelled';
      delete found.task.waiting;
    }
    if (this.waiting?.taskId === taskId) this.waiting = null;
    if (this.current?.taskId === taskId) this._kill(this.current.child);
    else if (before !== this.store.doc.queue.length || found) this.store.save();
  }

  /** Lets the waiting head start even with a terminal session open (the user said so). */
  forceStart(taskId) {
    this.force.add(taskId);
    this.waiting = null;
    this.kick();
  }

  kick() {
    clearTimeout(this._timer);
    this._timer = setTimeout(() => this._next().catch((e) => console.error('[runner]', e)), 50);
  }

  async _next() {
    // One decision at a time: a kick while the head is still waiting for Unity to open must not start a second run.
    if (this.current || this._busy) return;
    this._busy = true;
    try { await this._step(); } finally { this._busy = false; }
  }

  async _step() {
    const head = this.store.doc.queue[0];
    if (!head) { this.waiting = null; return; }
    const found = this.store.find(head.taskId);
    if (!found) { this.store.doc.queue.shift(); this.store.save(); this.kick(); return; }

    // 1 · no interactive Claude Code session in a terminal (unless told to go ahead)
    if (!this.force.has(head.taskId)) {
      const open = await this.interactiveSessions();
      if (open.length) return this._wait(head, `Claude está abierto en una terminal (${open.length}). Ciérralo o pulsa "Lanzar igualmente".`, 15000);
    }

    // 2 · what it needs, opened if closed
    const needs = found.task.needs || [];
    for (const app of needs) {
      if (await this.isUp(app)) continue;
      this._wait(head, app === 'unity' ? 'Abriendo Unity… (tarda un par de minutos)' : 'Abriendo ComfyUI…', 0);
      this.open(app);
      const ok = await this._waitUp(app, app === 'unity' ? 6 * 60e3 : 3 * 60e3);
      if (!ok) return this._wait(head, `${app === 'unity' ? 'Unity' : 'ComfyUI'} no responde. Ábrelo a mano; la cola lo vuelve a intentar.`, 30000);
    }

    this.waiting = null;
    this.force.delete(head.taskId);
    this.store.doc.queue.shift();
    const running = this._run(head, found);
    running.finally(() => this.kick());
  }

  _wait(head, reason, retryMs) {
    this.waiting = { taskId: head.taskId, reason };
    this.store.setRun(head.taskId, null, { status: 'waiting', waiting: reason });
    if (retryMs) { clearTimeout(this._timer); this._timer = setTimeout(() => this.kick(), retryMs); }
  }

  // ------------------------------------------------------------ one run

  async _run(item, { phase, task }) {
    const step = item.stepId ? task.steps?.find((s) => s.id === item.stepId) : null;
    const runId = `${new Date().toISOString().replace(/[:.]/g, '-')}_${task.id}`;
    // An instruction or a step goes on in the task's session; without one (never launched) it starts the task.
    const resume = !!task.sessionId && (!!item.followUp || !!step);
    const sessionId = resume ? task.sessionId : crypto.randomUUID();
    const prompt = item.followUp && resume ? this._followUpPrompt(task, item.followUp)
      : this._prompt(phase, task, step) + (item.followUp ? `\n\nThe user's instruction for this run (their words; it overrides the above where they conflict):\n${item.followUp}` : '');
    const cfg = this.opts.config;

    const args = ['-p', '--output-format', 'stream-json', '--verbose',
      '--permission-mode', 'auto', '--permission-prompts', 'none',
      '--model', task.model || cfg.defaultModel, '--effort', task.effort || cfg.defaultEffort,
      '-n', `Tarea: ${task.text}`.slice(0, 80),
      '--append-system-prompt', `${TERSE}\n${STUCK}`,
      '--disallowedTools', ...cfg.disallowedTools];
    if (resume) args.push('--resume', sessionId);
    else args.push('--session-id', sessionId);
    if (cfg.maxBudgetUsd) args.push('--max-budget-usd', String(cfg.maxBudgetUsd));

    fs.mkdirSync(this.opts.logDir, { recursive: true });
    const log = fs.createWriteStream(path.join(this.opts.logDir, `${runId}.jsonl`));
    log.write(JSON.stringify({ type: 'taller', prompt, args }) + '\n');

    const startedAt = Date.now();
    this.store.setRun(task.id, null, { status: 'running', waiting: undefined, lastRunId: runId, sessionId });
    if (step) this.store.setRun(task.id, step.id, { status: 'running' });

    const child = spawn(this.opts.claudeExe, args, { cwd: this.opts.projectRoot, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    this.children.add(child.pid);
    this.current = { taskId: task.id, stepId: step?.id || null, runId, child, startedAt, activity: [], title: task.text, step: step?.text || null, followUp: item.followUp || null,
      watch: { calls: new Map(), errors: new Map(), inARow: 0 } };
    child.stdin.end(prompt);

    let buffer = '', final = null, stderr = '';
    child.stdout.on('data', (chunk) => {
      buffer += chunk;
      let nl;
      while ((nl = buffer.indexOf('\n')) >= 0) {
        const line = buffer.slice(0, nl).trim();
        buffer = buffer.slice(nl + 1);
        if (!line) continue;
        log.write(line + '\n');
        try {
          const e = JSON.parse(line);
          if (e.type === 'result') final = e;
          else if (e.type === 'rate_limit_event') this._noteUsage(e.rate_limit_info);
          else { this._activity(e); this._watch(e); }
        } catch { /* not JSON: keep it in the log only */ }
      }
    });
    child.stderr.on('data', (chunk) => { stderr += chunk; log.write(JSON.stringify({ type: 'stderr', text: String(chunk) }) + '\n'); });

    const code = await new Promise((resolve) => child.on('close', resolve));
    this.children.delete(child.pid);
    log.end();
    const cancelled = this.current?.cancelled;
    const stuck = this.current?.stuck;
    this.current = null;

    const ok = !cancelled && !stuck && final && !final.is_error && code === 0;
    const text = cancelled ? 'Cancelada.'
      : stuck ? `⛔ Atascada: la paró el vigilante de la cola — ${stuck.reason}.\nLo último que hacía:\n${stuck.last.map((l) => `· ${l}`).join('\n')}\n`
        + 'Para seguir donde lo dejó: arregla la causa (abre / reinicia lo que no responda) y mándale una instrucción a esta tarea (p. ej. «sigue»): retoma su sesión.'
      : final?.result || (final?.is_error ? `Terminó con error (${final.subtype}).` : `Claude salió con código ${code}.${stderr ? `\n${stderr.slice(-800)}` : ''}`);
    const denials = (final?.permission_denials || []).map((d) => d.tool_name + (d.tool_input?.command ? `: ${String(d.tool_input.command).slice(0, 120)}` : ''));
    const result = {
      ok: !!ok, text, at: new Date().toISOString(), durationMs: Date.now() - startedAt,
      costUsd: final?.total_cost_usd ?? null,
      tokens: final?.usage ? { input: final.usage.input_tokens, cacheWrite: final.usage.cache_creation_input_tokens, cacheRead: final.usage.cache_read_input_tokens, output: final.usage.output_tokens, calls: final.num_turns } : undefined, runId, sessionId: final?.session_id || sessionId,
      ...(item.followUp ? { reply: item.followUp } : {}),
      ...(step ? { step: step.text, stepId: step.id } : {}),
      ...(denials.length ? { denied: denials } : {}),
      ...(stuck ? { stuck } : {}),
    };
    if (step) this.store.setRun(task.id, step.id, { status: ok ? 'done' : cancelled ? 'cancelled' : 'failed', result });
    // Every run (the task, a step, an instruction) is the task's latest report; the earlier ones are kept, newest
    // first: they are the conversation the web shows and the history's token totals.
    const fresh = this.store.find(task.id)?.task;
    const history = [...(fresh?.history || [])];
    if (fresh?.result) history.unshift(fresh.result);
    const more = this.store.doc.queue.some((q) => q.taskId === task.id);
    this.store.setRun(task.id, null, {
      history: history.length ? history.slice(0, HISTORY_MAX) : undefined,
      status: more && !cancelled ? 'queued' : ok ? 'done' : cancelled ? 'cancelled' : 'failed',
      sessionId: result.sessionId,
      result,
    });
    try { this.opts.onFinish?.(fresh || task, result); } catch (e) { console.error('[onFinish]', e); }
  }

  _usageFile() { return path.join(this.opts.logDir, 'usage.json'); }

  _loadUsage() {
    try { return JSON.parse(fs.readFileSync(this._usageFile(), 'utf8')); } catch { /* none yet: look in the run logs */ }
    // First start: take the newest rate_limit_event from the logs of earlier runs so the bars are not empty.
    try {
      const files = fs.readdirSync(this.opts.logDir).filter((f) => f.endsWith('.jsonl')).sort().reverse();
      for (const f of files) {
        const lines = fs.readFileSync(path.join(this.opts.logDir, f), "utf8").split(String.fromCharCode(10)).reverse();
        for (const l of lines) {
          if (!l.includes('rate_limit_event')) continue;
          const w = JSON.parse(l).rate_limit_info?.unifiedWindows;
          if (w) return { fiveHour: w.five_hour || null, sevenDay: w.seven_day || null, at: fs.statSync(path.join(this.opts.logDir, f)).mtimeMs };
        }
      }
    } catch { /* nothing usable */ }
    return null;
  }

  /** The CLI reports the plan's limits in a `rate_limit_event`; keep the latest so the web can show it. */
  _noteUsage(info) {
    const w = info?.unifiedWindows;
    if (!w) return;
    this.usage = { fiveHour: w.five_hour || null, sevenDay: w.seven_day || null, at: Date.now() };
    try { fs.mkdirSync(this.opts.logDir, { recursive: true }); fs.writeFileSync(this._usageFile(), JSON.stringify(this.usage)); } catch { /* cosmetic */ }
  }

  _activity(e) {
    if (e.type !== 'assistant' || !Array.isArray(e.message?.content)) return;
    for (const block of e.message.content) {
      let line = null;
      if (block.type === 'tool_use') {
        const i = block.input || {};
        const what = i.command || i.file_path || i.pattern || i.description || i.query || i.url || '';
        line = `${block.name}${what ? `: ${String(what).replace(/\s+/g, ' ').slice(0, 140)}` : ''}`;
      } else if (block.type === 'text' && block.text?.trim()) {
        line = `💬 ${block.text.trim().replace(/\s+/g, ' ').slice(0, 160)}`;
      }
      if (line) {
        this.current.activity.push({ at: Date.now(), line });
        if (this.current.activity.length > 40) this.current.activity.shift();
      }
    }
  }

  /**
   * The watchdog: stops a run that is going round in circles — the same error again and again, a long row of failed
   * tool calls, or the very same call repeated — before it burns the plan's tokens. It never looks at the clock: a
   * long run that keeps getting different results goes on. Limits: config.json ▸ watchdog (0 = off).
   */
  _watch(e) {
    const cur = this.current;
    if (!cur || cur.stuck || !Array.isArray(e.message?.content)) return;
    const lim = { sameError: 4, sameBusyError: 20, errorsInARow: 8, sameCall: 20, ...(this.opts.config.watchdog || {}) };
    const w = cur.watch;
    const bump = (map, key) => { const n = (map.get(key) || 0) + 1; map.set(key, n); return n; };
    for (const block of e.message.content) {
      if (e.type === 'assistant' && block.type === 'tool_use') {
        const sig = `${block.name} ${JSON.stringify(block.input || {})}`.replace(/\s+/g, ' ').slice(0, 600);
        const n = bump(w.calls, sig);
        if (lim.sameCall && n >= lim.sameCall) return this._stuck(`la misma orden ${n} veces (${sig.slice(0, 140)})`);
      } else if (e.type === 'user' && block.type === 'tool_result') {
        if (!block.is_error) { w.inARow = 0; continue; }
        const raw = Array.isArray(block.content) ? block.content.map((c) => c.text || '').join(' ') : String(block.content || '');
        // Numbers out (times, ports, ids): the same failure with another timestamp is still the same failure.
        const key = raw.replace(/\d+/g, '#').replace(/\s+/g, ' ').trim().slice(0, 200);
        const n = bump(w.errors, key);
        // "Busy" errors (Unity's 5 s main-thread timeout while it imports, a refused connection while an app starts)
        // are what polling looks like: they get a longer rope and do not count as a row of failures.
        const busy = /timed out|timeout|ECONNREFUSED|busy|not responding/i.test(raw);
        const limit = busy ? lim.sameBusyError : lim.sameError;
        if (!busy) w.inARow += 1;
        if (limit && n >= limit) return this._stuck(`el mismo error ${n} veces: «${raw.replace(/\s+/g, ' ').trim().slice(0, 200)}»`);
        if (lim.errorsInARow && w.inARow >= lim.errorsInARow) return this._stuck(`${w.inARow} órdenes seguidas fallando (la última: «${raw.replace(/\s+/g, ' ').trim().slice(0, 160)}»)`);
      }
    }
  }

  _stuck(reason) {
    const cur = this.current;
    cur.stuck = { reason, last: cur.activity.slice(-6).map((a) => a.line), at: new Date().toISOString() };
    console.log(`[runner] ⛔ ${cur.title}: ${reason}`);
    if (process.platform === 'win32') execFile('taskkill', ['/PID', String(cur.child.pid), '/T', '/F'], () => {});
    else cur.child.kill('SIGTERM');
  }

  // In English: it is what Claude reads. The user's own words (task text, notes, replies) go in as written.
  _prompt(phase, task, step) {
    const lines = [
      'Task launched from the project web (📋 Tareas tab, taller server queue).',
      'You work unattended: you cannot ask questions. If something is unclear, make the reasonable decision and note it in the final report.',
      'Do not edit docs/tareas/todo.json: the server stores your report in the task.',
      '',
      `Phase: ${phase.name}${phase.tag ? ` (${phase.tag})` : ''}`,
      `Task: ${task.text}`,
    ];
    if (task.note) lines.push(`Description: ${task.note}`);
    if (task.doc) lines.push(`Task document (read it first: state, instructions and what is already done): ${task.doc}`);
    if (task.steps?.length) {
      lines.push('', 'Steps:');
      task.steps.forEach((s, i) => lines.push(`${i + 1}. ${s.done || s.status === 'done' ? '[done] ' : ''}${s.text}${s.result?.text ? `\n   Previous result: ${s.result.text.slice(0, 600)}` : ''}`));
    }
    if (step) {
      const n = task.steps.indexOf(step) + 1;
      lines.push('', `Do ONLY step ${n}: ${step.text}`);
    } else if (task.steps?.length) {
      lines.push('', 'Do the steps that are not done, in order.');
    }
    if (task.prompt) lines.push('', 'Instructions:', task.prompt);
    const images = this._imagePaths(task);
    if (images.length) lines.push('', 'Reference images attached by the user (look at them with the Read tool before starting):', ...images.map((f) => `- ${f}`));
    if (task.needs?.length) lines.push('', `Already open: ${task.needs.map((n) => (n === 'unity' ? 'Unity (the project editor)' : 'ComfyUI')).join(', ')}.`);
    lines.push('', 'When done: compile and commit as CLAUDE.md says; if the task has a document, update it with the state and what is left.', REPORT);
    return lines.join('\n');
  }

  /** Absolute paths of the task's attached reference images (server.mjs saves them in Logs/Taller/adjuntos/). */
  _imagePaths(task) {
    return (task.images || []).map((i) => String(i.id || '')).filter((id) => /^[0-9a-f]{32}\.(png|jpg|webp|gif)$/.test(id))
      .map((id) => path.join(this.opts.logDir, 'adjuntos', id)).filter((f) => fs.existsSync(f));
  }

  _followUpPrompt(task, reply) {
    return [`The user's next instruction from the web (📋 Tareas) for the task "${task.text}":`, '', reply, '',
      'Still unattended. When done, commit as CLAUDE.md says.', REPORT].join('\n');
  }

  _kill(child) {
    if (!child) return;
    if (this.current) this.current.cancelled = true;
    if (process.platform === 'win32') execFile('taskkill', ['/PID', String(child.pid), '/T', '/F'], () => {});
    else child.kill('SIGTERM');
  }

  // ------------------------------------------------------------ apps

  async isUp(app) {
    if (app === 'comfy') {
      try { return (await fetch('http://127.0.0.1:8188/system_stats', { signal: AbortSignal.timeout(2500) })).ok; }
      catch { return false; }
    }
    if (app === 'unity') {
      const out = await run('unity', ['status'], 15000);
      const root = path.resolve(this.opts.projectRoot).toLowerCase();
      return out.split(/\r?\n/).some((l) => l.toLowerCase().includes(root) && /\bready\b/i.test(l));
    }
    return true;
  }

  open(app) {
    if (app === 'comfy' && this.opts.comfyExe) {
      spawn(this.opts.comfyExe, [], { detached: true, stdio: 'ignore', windowsHide: false }).unref();
    } else if (app === 'unity') {
      // -force-d3d11: the editor stutters with DX12 on this PC (memory: editor-stutter-is-dx12).
      spawn('unity', ['open', `"${this.opts.projectRoot}"`, '--args', '"-force-d3d11"'], { detached: true, stdio: 'ignore', shell: true }).unref();
    }
  }

  async _waitUp(app, timeoutMs) {
    const until = Date.now() + timeoutMs;
    while (Date.now() < until) {
      if (await this.isUp(app)) return true;
      await sleep(5000);
    }
    return false;
  }

  /** Claude Code sessions open in a terminal (not the desktop app, not the ones this queue started). */
  async interactiveSessions() {
    if (process.platform !== 'win32') return [];
    const ps = "Get-CimInstance Win32_Process -Filter \"Name='claude.exe'\" | Where-Object { $_.CommandLine -match 'claude-code' } | ForEach-Object { \"$($_.ProcessId)`t$($_.CommandLine)\" }";
    const out = await run('powershell', ['-NoProfile', '-Command', ps], 15000);
    return out.split(/\r?\n/).map((l) => l.split('\t')).filter(([pid, cmd]) => pid && cmd
      && !this.children.has(Number(pid)) && !/(\s-p\b|\s--print\b)/.test(cmd)).map(([pid]) => Number(pid));
  }

  status() {
    const cur = this.current;
    return {
      running: cur ? { taskId: cur.taskId, stepId: cur.stepId, title: cur.title, step: cur.step, followUp: cur.followUp, startedAt: cur.startedAt, runId: cur.runId, activity: cur.activity.slice(-30) } : null,
      waiting: this.waiting,
      usage: this.usage,
      queue: this.store.doc.queue.map((q) => ({ qid: q.qid, taskId: q.taskId, stepId: q.stepId, followUp: q.followUp ? String(q.followUp).slice(0, 400) : null })),
    };
  }
}

/** Runs a command and returns its stdout ('' on failure or timeout). */
function run(cmd, args, timeoutMs) {
  return new Promise((resolve) => {
    execFile(cmd, args, { timeout: timeoutMs, windowsHide: true, shell: process.platform === 'win32' && cmd === 'unity' }, (err, stdout) => resolve(err && !stdout ? '' : String(stdout || '')));
  });
}
