// tools/taller-server/server.mjs
// -----------------------------------------------------------------------------
// The workshop's local server: serves the web (Web/RedMagicWeb, as `npx serve` did — same address,
// http://localhost:3000, so the Unity bridge keeps trusting it) and the API of 📋 Tareas:
//
//   GET  /api/hello                 → { token, project }   (readable only by this origin: no CORS headers)
//   GET  /api/todo                  → { rev, doc }          docs/tareas/todo.json
//   PUT  /api/todo   { phases, knownTaskIds, knownPhaseIds }  → { rev, doc }   (merged, see store.mjs)
//   GET  /api/status                → { rev, running, waiting, queue, apps }
//   POST /api/run    { taskId, stepId?, followUp?, force? }   → queue a run (runner.mjs)
//   POST /api/cancel { taskId }     POST /api/force { taskId }   POST /api/open { app: 'comfy'|'unity' }
//   GET  /api/disenos               → { lotes, enemigos, tasks }   docs/disenos/ (disenos.mjs) + docs/enemigos/ (enemigos.mjs)
//   GET  /api/img?src=comfy:…|docs/…  → an image (only Comfy's output and docs/)
//   POST /api/disenos/review { loteId, itemIds, decision: accept|discard|pending, names, libraryIds }  → { errors }
//   POST /api/disenos/request { kind, request, count, mode: one|each, model, effort }  → { loteId, taskIds }   (queues the task(s))
//   GET  /api/enemigos/prefabs      → [{ name, prefab }]   enemy prefabs not in the library yet
//   POST /api/enemigos/update { name, op: estado|add-pendiente|edit-pendiente|remove-pendiente, … }
//   POST /api/enemigos/build { name, model, effort }   POST /api/enemigos/work { name, ids?, model, effort }   (queue a task)
//   POST /api/enemigos/add { prefab }   POST /api/enemigos/picture { name }   (the picture comes from Unity)
//
// Only 127.0.0.1. Anything that changes something needs the token header (a custom header forces a CORS
// preflight, which this server never answers, so no other page can send it) and a same-origin Host.
//
//   node server.mjs            (or start.cmd)        config: config.json next to this file (optional)

import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { Store } from './store.mjs';
import { Runner } from './runner.mjs';
import { Disenos } from './disenos.mjs';
import { Enemigos } from './enemigos.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(here, '../..');
const projectRoot = path.resolve(webRoot, '../..');

const DEFAULTS = {
  port: 3000,
  defaultModel: 'sonnet',
  defaultEffort: 'medium',
  maxBudgetUsd: null,
  disallowedTools: ['Bash(git push:*)', 'Bash(git reset:*)', 'Bash(git rebase:*)', 'Bash(git clean:*)', 'Bash(git branch -D:*)', 'Bash(git checkout --:*)'],
  claudeExe: null,
  comfyExe: null,
  comfyOutput: path.join(process.env.LOCALAPPDATA || '', 'Comfy-Desktop', 'ComfyUI-Shared', 'output'),   // where Comfy saves images
};
const configFile = path.join(here, 'config.json');
const config = { ...DEFAULTS, ...(fs.existsSync(configFile) ? JSON.parse(fs.readFileSync(configFile, 'utf8')) : {}) };
if (process.env.TALLER_PORT) config.port = Number(process.env.TALLER_PORT);   // tests, without stopping the real one

function findClaude() {
  if (config.claudeExe && fs.existsSync(config.claudeExe)) return config.claudeExe;
  try {
    const where = execFileSync('where', ['claude'], { encoding: 'utf8' }).split(/\r?\n/).filter(Boolean);
    for (const w of where) {
      const exe = path.join(path.dirname(w), 'node_modules', '@anthropic-ai', 'claude-code', 'bin', 'claude.exe');
      if (fs.existsSync(exe)) return exe;
      if (w.toLowerCase().endsWith('.exe')) return w;
    }
  } catch { /* not on PATH */ }
  throw new Error('No encuentro Claude Code (claude.exe). Pon su ruta en tools/taller-server/config.json → "claudeExe".');
}

function findComfy() {
  if (config.comfyExe && fs.existsSync(config.comfyExe)) return config.comfyExe;
  const guess = path.join(process.env.LOCALAPPDATA || '', 'Programs', 'Comfy Desktop', 'Comfy Desktop.exe');
  return fs.existsSync(guess) ? guess : null;
}

const store = new Store(path.join(projectRoot, 'docs', 'tareas', 'todo.json'));
const runner = new Runner(store, {
  projectRoot, logDir: path.join(projectRoot, 'Logs', 'Taller'), claudeExe: findClaude(), comfyExe: findComfy(), config,
});
const disenos = new Disenos({ projectRoot, comfyOutput: path.resolve(config.comfyOutput), store, runner });
const enemigos = new Enemigos({ projectRoot, store, runner });
const token = crypto.randomBytes(24).toString('hex');

// ============================================================ apps (cached: checking Unity runs a CLI)

const apps = { comfy: false, unity: false, at: 0 };
async function refreshApps() {
  if (Date.now() - apps.at < 8000) return apps;
  apps.at = Date.now();
  [apps.comfy, apps.unity] = await Promise.all([runner.isUp('comfy'), runner.isUp('unity')]);
  return apps;
}

// ============================================================ http

const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8', '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.svg': 'image/svg+xml', '.webp': 'image/webp',
  '.gif': 'image/gif', '.ico': 'image/x-icon', '.wav': 'audio/wav', '.ogg': 'audio/ogg', '.mp3': 'audio/mpeg', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.md': 'text/markdown; charset=utf-8' };

function send(res, status, body, type = 'application/json; charset=utf-8') {
  const data = typeof body === 'string' || Buffer.isBuffer(body) ? body : JSON.stringify(body);
  res.writeHead(status, { 'Content-Type': type, 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
  res.end(data);
}

async function readJson(req) {
  let body = '';
  for await (const chunk of req) {
    body += chunk;
    if (body.length > 5e6) throw new Error('demasiado grande');
  }
  return body ? JSON.parse(body) : {};
}

const allowedHost = (host) => host === `localhost:${config.port}` || host === `127.0.0.1:${config.port}`;

async function api(req, res, url) {
  const route = `${req.method} ${url.pathname}`;
  if (route === 'GET /api/hello') return send(res, 200, { token, project: path.basename(projectRoot) });
  if (route === 'GET /api/todo') return send(res, 200, { rev: store.rev, doc: store.doc });
  if (route === 'GET /api/disenos') {
    const lotes = disenos.list();
    const list = enemigos.list();
    const ids = [...lotes.flatMap((l) => l.taskIds || []), ...list.flatMap((e) => [e.tarea, ...e.pendientes.map((p) => p.tarea)])];
    return send(res, 200, { lotes, enemigos: list, tasks: enemigos.taskStates(ids) });
  }
  if (route === 'GET /api/enemigos/prefabs') return send(res, 200, enemigos.prefabs());
  if (route === 'GET /api/img') return serveFile(res, disenos.resolveImage(url.searchParams.get('src')), 'no-cache');
  if (route === 'GET /api/status') return send(res, 200, { rev: store.rev, ...runner.status(), apps: await refreshApps() });

  // Everything below changes something.
  if (req.headers['x-taller-token'] !== token) return send(res, 403, { error: 'token' });
  const origin = req.headers.origin;
  if (origin && origin !== `http://${req.headers.host}`) return send(res, 403, { error: 'origin' });
  const body = await readJson(req);

  if (route === 'PUT /api/todo') {
    if (!Array.isArray(body.phases)) return send(res, 400, { error: 'phases' });
    store.mergeFromWeb(body);
    return send(res, 200, { rev: store.rev, doc: store.doc });
  }
  if (route === 'POST /api/run') {
    const error = runner.enqueue(body);
    return send(res, error ? 409 : 200, error ? { error } : { ok: true, rev: store.rev });
  }
  if (route === 'POST /api/disenos/review') {
    if (!body.loteId || !Array.isArray(body.itemIds)) return send(res, 400, { error: 'loteId / itemIds' });
    return send(res, 200, await disenos.review(body));
  }
  if (route === 'POST /api/disenos/request') return send(res, 200, disenos.request(body));
  if (route === 'POST /api/enemigos/update') return send(res, 200, enemigos.update(body));
  if (route === 'POST /api/enemigos/build') return send(res, 200, enemigos.build(body));
  if (route === 'POST /api/enemigos/work') return send(res, 200, enemigos.work(body));
  if (route === 'POST /api/enemigos/add') return send(res, 200, enemigos.addExisting(body));
  if (route === 'POST /api/enemigos/picture') { enemigos.refreshPicture(body.name); return send(res, 200, { ok: true }); }
  if (route === 'POST /api/cancel') { runner.cancel(body.taskId); return send(res, 200, { ok: true }); }
  if (route === 'POST /api/force') { runner.forceStart(body.taskId); return send(res, 200, { ok: true }); }
  if (route === 'POST /api/open') {
    if (!['comfy', 'unity'].includes(body.app)) return send(res, 400, { error: 'app' });
    runner.open(body.app);
    apps.at = 0;
    return send(res, 200, { ok: true });
  }
  return send(res, 404, { error: 'ruta' });
}

function serveStatic(req, res, url) {
  let rel = decodeURIComponent(url.pathname);
  if (rel.endsWith('/')) rel += 'index.html';
  const file = path.resolve(webRoot, `.${rel}`);
  if (!file.startsWith(webRoot + path.sep) && file !== webRoot) return send(res, 403, 'no', 'text/plain');
  // The server's own folder is not part of the web.
  if (file.startsWith(here + path.sep)) return send(res, 404, 'no', 'text/plain');
  serveFile(res, file, 'no-cache');
}

function serveFile(res, file, cache) {
  if (!file) return send(res, 403, 'no', 'text/plain');
  fs.stat(file, (err, st) => {
    if (err || !st.isFile()) return send(res, 404, 'no encontrado', 'text/plain; charset=utf-8');
    res.writeHead(200, { 'Content-Type': MIME[path.extname(file).toLowerCase()] || 'application/octet-stream', 'Cache-Control': cache });
    fs.createReadStream(file).pipe(res);
  });
}

const server = http.createServer(async (req, res) => {
  try {
    // DNS rebinding: only this machine's own names reach the API.
    if (!allowedHost(req.headers.host || '')) return send(res, 403, 'host', 'text/plain');
    const url = new URL(req.url, `http://${req.headers.host}`);
    if (url.pathname.startsWith('/api/')) return await api(req, res, url);
    if (req.method !== 'GET' && req.method !== 'HEAD') return send(res, 405, 'no', 'text/plain');
    serveStatic(req, res, url);
  } catch (e) {
    console.error('[server]', e);
    if (!res.headersSent) send(res, 500, { error: e.message });
  }
});

server.on('error', (e) => {
  if (e.code === 'EADDRINUSE') console.error(`\nEl puerto ${config.port} está ocupado (¿sigue abierto "npx serve"?). Ciérralo y vuelve a arrancar.\n`);
  else console.error(e);
  process.exit(1);
});

server.listen(config.port, '127.0.0.1', () => {
  console.log(`\nTaller de RedMagic → http://localhost:${config.port}/`);
  console.log(`  tareas: docs/tareas/todo.json · registros: Logs/Taller/ · modelo por defecto: ${config.defaultModel} (${config.defaultEffort})`);
  console.log(`  ComfyUI: ${runner.opts.comfyExe || 'no encontrado (config.json → comfyExe)'}`);
  console.log('  Ctrl+C para parar. Si se para con una tarea a medias, queda marcada como interrumpida.\n');
});
