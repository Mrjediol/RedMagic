// tools/taller-server/session-usage.mjs
// -----------------------------------------------------------------------------
// Exact token use of Claude Code sessions, read from their transcripts
// (~/.claude/projects/<project>/<session id>.jsonl) — the same file for a terminal session and for one the
// task queue launched, so both are measured the same way.
//
//   node session-usage.mjs <session id | path.jsonl> [more…] [--since <ISO time>] [--json]
//   node session-usage.mjs --last [n]        the n most recent sessions of this project (default 5)
//
// Per session: API calls (one per assistant message, deduplicated by message id), input tokens, cache
// writes, cache reads, output tokens, per model; the first / last timestamps; and "equivalent input
// units" = input + 1.25 × cache writes + 0.1 × cache reads + 5 × output — the API price ratios, only a
// PROXY for comparing two runs (the plan's own limit is not published per token).
// --since ignores calls before that time (to measure a part of a long session, e.g. after a /clear).

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(here, '../../../..');
const dirName = projectRoot.replace(/[:\\/]/g, '-').replace(/^-+/, '');
export const SESSIONS_DIR = path.join(os.homedir(), '.claude', 'projects', dirName);

export function resolveSession(arg) {
  if (fs.existsSync(arg)) return arg;
  const file = path.join(SESSIONS_DIR, `${arg.replace(/\.jsonl$/, '')}.jsonl`);
  if (fs.existsSync(file)) return file;
  throw new Error(`No encuentro la sesión "${arg}" en ${SESSIONS_DIR}`);
}

export function measure(file, { since = null } = {}) {
  const byMessage = new Map();   // message id → last usage seen (a streamed message repeats it per block)
  let first = null, last = null, title = null, sidechain = 0;
  for (const line of fs.readFileSync(file, 'utf8').split('\n')) {
    if (!line.trim()) continue;
    let e;
    try { e = JSON.parse(line); } catch { continue; }
    if (e.type === 'custom-title' && e.customTitle) title = e.customTitle;
    if (e.type !== 'assistant' || !e.message?.usage) continue;
    if (since && e.timestamp < since) continue;
    const id = e.message.id || e.uuid;
    if (!byMessage.has(id) && e.isSidechain) sidechain++;
    byMessage.set(id, { usage: e.message.usage, model: e.message.model, at: e.timestamp });
    first = first && first < e.timestamp ? first : e.timestamp;
    last = last && last > e.timestamp ? last : e.timestamp;
  }

  const models = {};
  const total = { calls: 0, input: 0, cacheWrite: 0, cacheRead: 0, output: 0 };
  for (const { usage, model } of byMessage.values()) {
    const m = (models[model || '?'] ||= { calls: 0, input: 0, cacheWrite: 0, cacheRead: 0, output: 0 });
    for (const t of [m, total]) {
      t.calls++;
      t.input += usage.input_tokens || 0;
      t.cacheWrite += usage.cache_creation_input_tokens || 0;
      t.cacheRead += usage.cache_read_input_tokens || 0;
      t.output += usage.output_tokens || 0;
    }
  }
  const units = (t) => Math.round(t.input + 1.25 * t.cacheWrite + 0.1 * t.cacheRead + 5 * t.output);
  return { file: path.basename(file), title, first, last, sidechainCalls: sidechain, total: { ...total, units: units(total) },
    models: Object.fromEntries(Object.entries(models).map(([k, v]) => [k, { ...v, units: units(v) }])) };
}

const fmt = (n) => n.toLocaleString('es-ES');
export function report(r) {
  const t = r.total;
  const mins = r.first && r.last ? ((Date.parse(r.last) - Date.parse(r.first)) / 60000).toFixed(1) : '?';
  const lines = [
    `Sesión ${r.file}${r.title ? ` · «${r.title}»` : ''}`,
    `  ${r.first || '—'} → ${r.last || '—'} (${mins} min)${r.sidechainCalls ? ` · ${r.sidechainCalls} llamadas de subagentes` : ''}`,
    `  llamadas a la API:       ${fmt(t.calls)}`,
    `  entrada nueva:           ${fmt(t.input)}`,
    `  escrito en caché:        ${fmt(t.cacheWrite)}`,
    `  leído de caché:          ${fmt(t.cacheRead)}`,
    `  salida:                  ${fmt(t.output)}`,
    `  unidades equivalentes:   ${fmt(t.units)}   (proxy: entrada + 1,25×escritura + 0,1×lectura + 5×salida)`,
  ];
  for (const [model, m] of Object.entries(r.models)) lines.push(`    · ${model}: ${fmt(m.calls)} llamadas, ${fmt(m.units)} unidades`);
  return lines.join('\n');
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  const args = process.argv.slice(2);
  const take = (flag) => { const i = args.indexOf(flag); if (i < 0) return null; const [, v] = args.splice(i, 2); return v ?? true; };
  const asJson = args.includes('--json') && args.splice(args.indexOf('--json'), 1);
  const since = take('--since');
  const lastN = args.includes('--last') ? (() => { const i = args.indexOf('--last'); const n = Number(args[i + 1]) || 5; args.splice(i, Number(args[i + 1]) ? 2 : 1); return n; })() : 0;
  let files = args;
  if (lastN) {
    files = fs.readdirSync(SESSIONS_DIR).filter((f) => f.endsWith('.jsonl'))
      .map((f) => path.join(SESSIONS_DIR, f)).sort((a, b) => fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs).slice(0, lastN);
  }
  if (!files.length) { console.error('uso: node session-usage.mjs <id de sesión | ruta> [...] [--since ISO] [--json] | --last [n]'); process.exit(1); }
  const results = files.map((f) => measure(resolveSession(f), { since }));
  console.log(asJson ? JSON.stringify(results, null, 2) : results.map(report).join('\n\n'));
  if (results.length === 2 && !asJson) {
    const [a, b] = results;
    const pct = (x, y) => (y ? `${x >= y ? '+' : ''}${(((x - y) / y) * 100).toFixed(0)} %` : '—');
    console.log(`\nComparación (1.ª frente a 2.ª): unidades ${fmt(a.total.units)} vs ${fmt(b.total.units)} (${pct(a.total.units, b.total.units)}) · llamadas ${a.total.calls} vs ${b.total.calls} · salida ${fmt(a.total.output)} vs ${fmt(b.total.output)}`);
  }
}
