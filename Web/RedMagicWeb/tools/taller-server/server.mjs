// tools/taller-server/server.mjs
// -----------------------------------------------------------------------------
// The workshop's local server: serves the web (Web/RedMagicWeb, as `npx serve` did — same address,
// http://localhost:3000, so the Unity bridge keeps trusting it) and the API of 📋 Tareas:
//
//   GET  /api/hello                 → { token, project }   (readable only by this origin: no CORS headers)
//   GET  /api/todo                  → { rev, doc }          docs/tareas/todo.json
//   PUT  /api/todo   { phases, knownTaskIds, knownPhaseIds }  → { rev, doc }   (merged, see store.mjs)
//   GET  /api/status                → { rev, running, waiting, queue, usage, finished, apps }
//   POST /api/run    { taskId, stepId?, followUp?, force? }   → queue a run (runner.mjs); followUp = an instruction (resumes its session)
//   POST /api/cancel { taskId }     POST /api/unqueue { qid }   POST /api/force { taskId }   POST /api/open { app: 'comfy'|'unity' }
//   GET  /api/disenos               → { lotes, enemigos, tasks }   docs/disenos/ (disenos.mjs) + docs/enemigos/ (enemigos.mjs)
//   GET  /api/img?src=comfy:…|docs/…  → an image (only Comfy's output and docs/)
//   POST /api/disenos/review { loteId, itemIds, decision: accept|discard|pending, names, libraryIds }  → { errors }
//   POST /api/disenos/request { kind, request, count, mode: one|each, model, effort, zone, body, attack, generator: prompt|comfy (enemy) }  → { loteId, taskIds }   (queues the task(s))
//   POST /api/disenos/imagen { loteId, itemId, type, data: <base64> } | { loteId, itemId, url } → { image }   a picture pasted on a
//        proposal's card (docs/disenos/img/); `url` = an https image the clipboard only had as a link (copied from a web page)
//   GET  /api/enemigos/prefabs      → [{ name, prefab }]   enemy prefabs not in the library yet
//   POST /api/enemigos/update { name, op: estado|add-pendiente|edit-pendiente|remove-pendiente|add-estado|edit-estado|remove-estado, … }
//   POST /api/enemigos/build { name, model, effort, instrucciones?, piezas? }   POST /api/enemigos/build-body { name, …, instrucciones?, piezas? } (step 1)
//   POST /api/enemigos/states { name, model, effort, instrucciones? }   (a task for the open part states of a built enemy)
//   POST /api/enemigos/build-rest { name, paso2, …, instrucciones? } (step 3)   POST /api/enemigos/work { name, ids?, model, effort }   (queue a task)
//   POST /api/enemigos/add { prefab }   POST /api/enemigos/picture { name }   (the picture comes from Unity)
//
//   POST /api/adjunto { name, type, data: <base64> } → { id, name }   a reference image for a task's prompt (Logs/Taller/adjuntos/)
//   GET  /api/adjunto?id=<id>       → that image
//   GET  /api/niveles/bocetos       → [{ name, mtime }]   Assets/Levels/Bocetos (niveles.mjs)
//   GET  /api/niveles/boceto?name=   GET /api/niveles/arte?path=Assets/Levels/…   POST /api/niveles/guardar { name, text } → { path }
//   GET  /api/movil                → { key, url }   (this PC only) the phone's pairing link
//   POST /api/pair   { key }        → sets the phone's cookie
//
// LOCAL = a request from this PC to localhost / 127.0.0.1 (no proxy headers). Anything else is REMOTE — the phone
// through `tailscale serve` (or the LAN with config.listen) — and every /api/ call needs the pairing cookie
// (key in Logs/Taller/movil-key.txt), which also stops DNS rebinding. Anything that changes something needs the
// token header (a custom header forces a CORS preflight, which this server never answers, so no other page can
// send it) and a same-origin Origin. docs/architecture/tareas.md ▸ Phone.
//
//   node server.mjs            (or start.cmd)        config: config.json next to this file (optional)

import http from 'node:http';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { Store } from './store.mjs';
import { Runner } from './runner.mjs';
import { Disenos } from './disenos.mjs';
import { Enemigos } from './enemigos.mjs';
import { Niveles } from './niveles.mjs';
import { BEHAVIORS, PIECES } from './design-prompts.mjs';
import { loadZones, loadTypes } from '../enemy-prompt.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(here, '../..');
const projectRoot = path.resolve(webRoot, '../..');

const DEFAULTS = {
  port: 3000,
  defaultModel: 'sonnet',
  defaultEffort: 'medium',
  maxBudgetUsd: null,
  watchdog: { sameError: 4, sameBusyError: 20, errorsInARow: 8, sameCall: 20 },   // runner.mjs ▸ _watch: stops a run going in circles (0 = off)
  disallowedTools: ['Bash(git push:*)', 'Bash(git reset:*)', 'Bash(git rebase:*)', 'Bash(git clean:*)', 'Bash(git branch -D:*)', 'Bash(git checkout --:*)'],
  claudeExe: null,
  comfyExe: null,
  comfyOutput: path.join(process.env.LOCALAPPDATA || '', 'Comfy-Desktop', 'ComfyUI-Shared', 'output'),   // where Comfy saves images
  listen: '127.0.0.1',   // '0.0.0.0' = also the LAN (phone on the same Wi-Fi); with `tailscale serve` keep 127.0.0.1
  ntfy: null,            // e.g. "https://ntfy.sh/<a long secret topic>": a push to the phone when a task ends
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
const logDir = path.join(projectRoot, 'Logs', 'Taller');
const runner = new Runner(store, {
  projectRoot, logDir, claudeExe: findClaude(), comfyExe: findComfy(), config, onFinish: notify,
});
const disenos = new Disenos({ projectRoot, comfyOutput: path.resolve(config.comfyOutput), store, runner });
const enemigos = new Enemigos({ projectRoot, store, runner });
const niveles = new Niveles({ projectRoot });
const token = crypto.randomBytes(24).toString('hex');

// ============================================================ the phone

/** The pairing key: made once, kept out of git (Logs/). Deleting the file un-pairs every phone. */
const keyFile = path.join(logDir, 'movil-key.txt');
const phoneKey = (() => {
  try { const k = fs.readFileSync(keyFile, 'utf8').trim(); if (k.length >= 20) return k; } catch { /* first start */ }
  const k = crypto.randomBytes(18).toString('base64url');
  fs.mkdirSync(logDir, { recursive: true });
  fs.writeFileSync(keyFile, k);
  return k;
})();
const COOKIE = 'taller_movil';

/** Reference images attached to tasks: kept out of git (Logs/), named by a random id so the id is the only handle. */
const attachDir = path.join(logDir, 'adjuntos');
const IMAGE_EXT = { 'image/png': '.png', 'image/jpeg': '.jpg', 'image/webp': '.webp', 'image/gif': '.gif' };
const ATTACH_ID = /^[0-9a-f]{32}\.(png|jpg|webp|gif)$/;
const LOOPBACK = new Set(['127.0.0.1', '::1', '::ffff:127.0.0.1']);
const localHost = (host) => host === `localhost:${config.port}` || host === `127.0.0.1:${config.port}`;
/** This PC's own browser: loopback, a localhost Host, no proxy in between. */
const isLocal = (req) => LOOPBACK.has(req.socket.remoteAddress) && localHost(req.headers.host || '')
  && !req.headers['x-forwarded-for'] && !req.headers['tailscale-user-login'];
function cookieOf(req, name) {
  for (const part of String(req.headers.cookie || '').split(';')) {
    const [k, ...v] = part.trim().split('=');
    if (k === name) return decodeURIComponent(v.join('='));
  }
  return '';
}
const sameKey = (k) => { const a = Buffer.from(String(k)), b = Buffer.from(phoneKey); return a.length === b.length && crypto.timingSafeEqual(a, b); };

/** The address the phone opens: the Tailscale name of this PC if `tailscale serve` is set up, else null. */
let tailscaleUrl = null;   // cached once found; looked up again while missing (Tailscale installed later)
function phoneUrl() {
  if (tailscaleUrl) return tailscaleUrl;
  try {
    const st = JSON.parse(execFileSync('tailscale', ['status', '--json'], { encoding: 'utf8', timeout: 4000, windowsHide: true }));
    const name = st?.Self?.DNSName?.replace(/\.$/, '');
    // only once `tailscale serve` is set up: without it the https name does not answer (the 100.x link is shown instead)
    const serve = JSON.parse(execFileSync('tailscale', ['serve', 'status', '--json'], { encoding: 'utf8', timeout: 4000, windowsHide: true }) || '{}');
    if (name && (serve.Web || serve.TCP)) tailscaleUrl = `https://${name}`;
  } catch { /* no Tailscale */ }
  return tailscaleUrl;
}

/** ntfy push when a task ends (config.ntfy): title + ok / failed, nothing else leaves the PC. */
function notify(task, result) {
  if (!config.ntfy || !task) return;
  try {
    const u = new URL(config.ntfy);   // JSON publishing (UTF-8 titles): POST to the server root with the topic inside
    fetch(u.origin, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({
      topic: u.pathname.replace(/^\/+/, ''), title: task.text.slice(0, 120), tags: [result.ok ? 'white_check_mark' : 'x'],
      message: result.ok ? 'Terminada: mira el informe en Tareas' : 'Falló o se canceló: mira el informe',
    }) }).catch((e) => console.error('[ntfy]', e.message));
  } catch (e) { console.error('[ntfy] config.ntfy no es una URL:', e.message); }
}

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
  '.gif': 'image/gif', '.ico': 'image/x-icon', '.wav': 'audio/wav', '.ogg': 'audio/ogg', '.mp3': 'audio/mpeg', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.md': 'text/markdown; charset=utf-8',
  '.webmanifest': 'application/manifest+json; charset=utf-8' };

function send(res, status, body, type = 'application/json; charset=utf-8') {
  const data = typeof body === 'string' || Buffer.isBuffer(body) ? body : JSON.stringify(body);
  res.writeHead(status, { 'Content-Type': type, 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
  res.end(data);
}

async function readJson(req, limit = 5e6) {
  let body = '';
  for await (const chunk of req) {
    body += chunk;
    if (body.length > limit) throw new Error('demasiado grande');
  }
  return body ? JSON.parse(body) : {};
}

async function api(req, res, url) {
  const route = `${req.method} ${url.pathname}`;
  const local = isLocal(req);
  if (route === 'POST /api/pair') {   // the phone, once: the key from the link becomes a cookie for every later call
    const body = await readJson(req);
    if (!sameKey(body.key)) return send(res, 403, { error: 'clave' });
    res.setHeader('Set-Cookie', `${COOKIE}=${encodeURIComponent(phoneKey)}; Path=/; Max-Age=31536000; HttpOnly; SameSite=Strict`);
    return send(res, 200, { ok: true });
  }
  if (!local && !sameKey(cookieOf(req, COOKIE))) return send(res, 401, { error: 'movil' });
  if (route === 'GET /api/movil') {
    if (!local) return send(res, 403, { error: 'solo desde el PC' });
    const lan = config.listen === '127.0.0.1' ? [] : Object.values(os.networkInterfaces()).flat().filter((i) => i && i.family === 'IPv4' && !i.internal && !i.address.startsWith('169.254.')).map((i) => i.address).sort((a, b) => Number(b.startsWith('100.')) - Number(a.startsWith('100.'))).map((a) => `http://${a}:${config.port}`);
    return send(res, 200, { key: phoneKey, url: phoneUrl(), lan });
  }
  if (route === 'GET /api/adjunto') {
    const id = url.searchParams.get('id') || '';
    return serveFile(res, ATTACH_ID.test(id) ? path.join(attachDir, id) : null, 'private, max-age=86400');
  }
  if (route === 'GET /api/hello') return send(res, 200, { token, project: path.basename(projectRoot) });
  if (route === 'GET /api/todo') return send(res, 200, { rev: store.rev, doc: store.doc });
  if (route === 'GET /api/disenos') {
    const lotes = disenos.list();
    const list = enemigos.list();
    const ids = [...lotes.flatMap((l) => l.taskIds || []), ...list.flatMap((e) => [e.tarea, ...e.pendientes.map((p) => p.tarea)])];
    return send(res, 200, { lotes, enemigos: list, tasks: enemigos.taskStates(ids), zonas: loadZones(), tipos: loadTypes(), comportamientos: Object.entries(BEHAVIORS).map(([id, b]) => ({ id, label: b.label })), piezas: PIECES });
  }
  if (route === 'GET /api/enemigos/prefabs') return send(res, 200, enemigos.prefabs());
  if (route === 'GET /api/img') return serveFile(res, disenos.resolveImage(url.searchParams.get('src')), 'no-cache');
  if (route === 'GET /api/niveles/bocetos') return send(res, 200, niveles.list());
  if (route === 'GET /api/niveles/boceto') return serveFile(res, niveles.resolveSketch(url.searchParams.get('name')), 'no-cache');
  if (route === 'GET /api/niveles/arte') return serveFile(res, niveles.resolveArt(url.searchParams.get('path')), 'no-cache');
  if (route === 'GET /api/status') return send(res, 200, { rev: store.rev, ...runner.status(), apps: await refreshApps() });

  // Everything below changes something.
  if (req.headers['x-taller-token'] !== token) return send(res, 403, { error: 'token' });
  const origin = req.headers.origin;
  if (origin && origin !== `http://${req.headers.host}` && origin !== `https://${req.headers.host}`) return send(res, 403, { error: 'origin' });
  if (route === 'POST /api/disenos/imagen') {   // a picture pasted on a proposal's card
    const b = await readJson(req, 16e6);
    try {
      if (b.url) Object.assign(b, await downloadImage(b.url));
      return send(res, 200, disenos.attachImage(b));
    } catch (err) { return send(res, 400, { error: err.message }); }
  }
  if (route === 'POST /api/adjunto') {
    const b = await readJson(req, 16e6);   // base64 of up to ~12 MB
    const ext = IMAGE_EXT[b.type];
    const bytes = Buffer.from(String(b.data || ''), 'base64');
    if (!ext || !bytes.length || bytes.length > 12e6) return send(res, 400, { error: 'imagen no válida (png, jpg, webp o gif, hasta 12 MB)' });
    const id = crypto.randomBytes(16).toString('hex') + ext;
    fs.mkdirSync(attachDir, { recursive: true });
    fs.writeFileSync(path.join(attachDir, id), bytes);
    return send(res, 200, { id, name: String(b.name || 'imagen').slice(0, 80) });
  }
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
  if (route === 'POST /api/enemigos/build-body') return send(res, 200, enemigos.buildBody(body));
  if (route === 'POST /api/enemigos/build-rest') return send(res, 200, enemigos.buildRest(body));
  if (route === 'POST /api/enemigos/work') return send(res, 200, enemigos.work(body));
  if (route === 'POST /api/enemigos/states') return send(res, 200, enemigos.states(body));
  if (route === 'POST /api/enemigos/add') return send(res, 200, enemigos.addExisting(body));
  if (route === 'POST /api/enemigos/picture') { enemigos.refreshPicture(body.name); return send(res, 200, { ok: true }); }
  if (route === 'POST /api/cancel') { runner.cancel(body.taskId); return send(res, 200, { ok: true }); }
  if (route === 'POST /api/unqueue') { runner.unqueue(String(body.qid || '')); return send(res, 200, { ok: true }); }
  if (route === 'POST /api/force') { runner.forceStart(body.taskId); return send(res, 200, { ok: true }); }
  if (route === 'POST /api/niveles/guardar') return send(res, 200, niveles.save(body));
  if (route === 'POST /api/open') {
    if (!['comfy', 'unity'].includes(body.app)) return send(res, 400, { error: 'app' });
    runner.open(body.app);
    apps.at = 0;
    return send(res, 200, { ok: true });
  }
  return send(res, 404, { error: 'ruta' });
}

/**
 * An image the browser only got as a link (Ctrl+C on a picture in a web page copies its <img>, not the file):
 * https only, an image type, 12 MB at most. → { type, data: <base64> } for attachImage.
 */
async function downloadImage(link) {
  let u;
  try { u = new URL(String(link)); } catch { throw new Error('enlace de imagen no válido'); }
  if (u.protocol !== 'https:') throw new Error('solo imágenes https');
  const r = await fetch(u, { signal: AbortSignal.timeout(20000), redirect: 'follow' }).catch(() => null);
  if (!r?.ok) throw new Error('No pude bajar la imagen del enlace: en Gemini usa clic derecho ▸ Copiar imagen, o descárgala y súbela con 🖼');
  const type = String(r.headers.get('content-type') || '').split(';')[0].trim();
  if (!IMAGE_EXT[type]) throw new Error(`El enlace no es una imagen png / jpg / webp / gif (${type || 'sin tipo'})`);
  const bytes = Buffer.from(await r.arrayBuffer());
  if (bytes.length > 12e6) throw new Error('La imagen pesa más de 12 MB');
  return { type, data: bytes.toString('base64') };
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
    // DNS rebinding: a page that renamed itself to this PC is REMOTE (isLocal) and has no pairing cookie for the API.
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

server.listen(config.port, config.listen, () => {
  console.log(`\nTaller de RedMagic → http://localhost:${config.port}/`);
  console.log(`  tareas: docs/tareas/todo.json · registros: Logs/Taller/ · modelo por defecto: ${config.defaultModel} (${config.defaultEffort})`);
  console.log(`  ComfyUI: ${runner.opts.comfyExe || 'no encontrado (config.json → comfyExe)'}`);
  const phone = phoneUrl();
  console.log(`  móvil: ${phone ? `${phone}/movil.html (Tailscale)` : "sin Tailscale"}${config.listen !== '127.0.0.1' ? ` · red local en el puerto ${config.port}` : ''} — el enlace para emparejarlo, en Tareas ▸ 📱`);
  console.log('  Ctrl+C para parar. Si se para con una tarea a medias, queda marcada como interrumpida.\n');
});
