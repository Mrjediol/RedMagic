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

import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawn, execFile } from 'node:child_process';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

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
   * Adds a run: the whole task, one step of it, or a reply to its last session (`followUp`).
   * @returns {string|null} an error for the user, or null
   */
  enqueue({ taskId, stepId = null, followUp = null, force = false }) {
    const found = this.store.find(taskId);
    if (!found) return 'No existe esa tarea.';
    if (followUp && !found.task.sessionId) return 'Esta tarea aún no tiene una sesión a la que responder.';
    if (this.store.doc.queue.some((q) => q.taskId === taskId && q.stepId === stepId && !q.followUp) && !followUp) return 'Ya está en la cola.';
    this.store.doc.queue.push({ taskId, stepId, followUp, queuedAt: new Date().toISOString() });
    if (force) this.force.add(taskId);
    const target = stepId ? found.task.steps?.find((s) => s.id === stepId) : found.task;
    if (target && target.status !== 'running') target.status = 'queued';
    this.store.save();
    this.kick();
    return null;
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
    const resume = !!item.followUp || (!!step && !!task.sessionId);
    const sessionId = resume ? task.sessionId : crypto.randomUUID();
    const prompt = item.followUp ? this._followUpPrompt(task, item.followUp) : this._prompt(phase, task, step);
    const cfg = this.opts.config;

    const args = ['-p', '--output-format', 'stream-json', '--verbose',
      '--permission-mode', 'auto', '--permission-prompts', 'none',
      '--model', task.model || cfg.defaultModel, '--effort', task.effort || cfg.defaultEffort,
      '-n', `Tarea: ${task.text}`.slice(0, 80),
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
    this.current = { taskId: task.id, stepId: step?.id || null, runId, child, startedAt, activity: [], title: task.text, step: step?.text || null };
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
          else this._activity(e);
        } catch { /* not JSON: keep it in the log only */ }
      }
    });
    child.stderr.on('data', (chunk) => { stderr += chunk; log.write(JSON.stringify({ type: 'stderr', text: String(chunk) }) + '\n'); });

    const code = await new Promise((resolve) => child.on('close', resolve));
    this.children.delete(child.pid);
    log.end();
    const cancelled = this.current?.cancelled;
    this.current = null;

    const ok = !cancelled && final && !final.is_error && code === 0;
    const text = cancelled ? 'Cancelada.'
      : final?.result || (final?.is_error ? `Terminó con error (${final.subtype}).` : `Claude salió con código ${code}.${stderr ? `\n${stderr.slice(-800)}` : ''}`);
    const denials = (final?.permission_denials || []).map((d) => d.tool_name + (d.tool_input?.command ? `: ${String(d.tool_input.command).slice(0, 120)}` : ''));
    const result = {
      ok: !!ok, text, at: new Date().toISOString(), durationMs: Date.now() - startedAt,
      costUsd: final?.total_cost_usd ?? null,
      tokens: final?.usage ? { input: final.usage.input_tokens, cacheWrite: final.usage.cache_creation_input_tokens, cacheRead: final.usage.cache_read_input_tokens, output: final.usage.output_tokens, calls: final.num_turns } : undefined, runId, sessionId: final?.session_id || sessionId,
      ...(item.followUp ? { reply: item.followUp } : {}),
      ...(denials.length ? { denied: denials } : {}),
    };
    if (step) this.store.setRun(task.id, step.id, { status: ok ? 'done' : cancelled ? 'cancelled' : 'failed', result });
    // Earlier reports are kept (newest first, 10 at most): a reply must not wipe what came before it.
    const fresh = this.store.find(task.id)?.task;
    const history = [...(fresh?.history || [])];
    if (!step && fresh?.result) history.unshift(fresh.result);
    this.store.setRun(task.id, null, {
      history: history.length ? history.slice(0, 10) : undefined,
      status: ok ? 'done' : cancelled ? 'cancelled' : 'failed',
      sessionId: result.sessionId,
      ...(step ? {} : { result }),
    });
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
      task.steps.forEach((s, i) => lines.push(`${i + 1}. ${s.done ? '[done] ' : ''}${s.text}${s.result?.text ? `\n   Previous result: ${s.result.text.slice(0, 600)}` : ''}`));
    }
    if (step) {
      const n = task.steps.indexOf(step) + 1;
      lines.push('', `Do ONLY step ${n}: ${step.text}`);
    } else if (task.steps?.length) {
      lines.push('', 'Do the steps that are not done, in order.');
    }
    if (task.prompt) lines.push('', 'Instructions:', task.prompt);
    if (task.needs?.length) lines.push('', `Already open: ${task.needs.map((n) => (n === 'unity' ? 'Unity (the project editor)' : 'ComfyUI')).join(', ')}.`);
    lines.push('',
      'When done: compile and commit as CLAUDE.md says; if the task has a document, update it with the state and what is left.',
      'Your LAST message is the report the user reads on the web, under the task: what you did, conclusions, decisions taken, where each thing is and a short list of what to test. Write it in Spanish, concise, as in the terminal.');
    return lines.join('\n');
  }

  _followUpPrompt(task, reply) {
    return [`The user's reply from the web (📋 Tareas) about the task "${task.text}":`, '', reply, '',
      'You are still unattended. When done, commit as CLAUDE.md says; your last message is the report for the web (in Spanish).'].join('\n');
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
      running: cur ? { taskId: cur.taskId, stepId: cur.stepId, title: cur.title, step: cur.step, startedAt: cur.startedAt, runId: cur.runId, activity: cur.activity.slice(-12) } : null,
      waiting: this.waiting,
      usage: this.usage,
      queue: this.store.doc.queue.map((q) => ({ taskId: q.taskId, stepId: q.stepId, followUp: !!q.followUp })),
    };
  }
}

/** Runs a command and returns its stdout ('' on failure or timeout). */
function run(cmd, args, timeoutMs) {
  return new Promise((resolve) => {
    execFile(cmd, args, { timeout: timeoutMs, windowsHide: true, shell: process.platform === 'win32' && cmd === 'unity' }, (err, stdout) => resolve(err && !stdout ? '' : String(stdout || '')));
  });
}
