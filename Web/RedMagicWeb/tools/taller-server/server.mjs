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

const here = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(here, '../..');
const projectRoot = path.resolve(webRoot, '../..');

const DEFAULTS = {
  port: 3000,
  defaultModel: 'sonnet',
  defaultEffort: 'medium',
  maxBudgetUsd: null,
  pauseAtWeekly: 0.85,   // the queue holds back new runs above this share of the weekly plan limit (0 = never)
  disallowedTools: ['Bash(git push:*)', 'Bash(git reset:*)', 'Bash(git rebase:*)', 'Bash(git clean:*)', 'Bash(git branch -D:*)', 'Bash(git checkout --:*)'],
  claudeExe: null,
  comfyExe: null,
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
  fs.stat(file, (err, st) => {
    if (err || !st.isFile()) return send(res, 404, 'no encontrado', 'text/plain; charset=utf-8');
    res.writeHead(200, { 'Content-Type': MIME[path.extname(file).toLowerCase()] || 'application/octet-stream', 'Cache-Control': 'no-cache' });
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
