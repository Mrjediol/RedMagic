// modules/unity-bridge.js
// -----------------------------------------------------------------------------
// The client of the Unity ⇄ web bridge (docs/plan-unity-web-bridge.md): a small HTTP API the Unity
// editor serves on 127.0.0.1 (Assets/Scripts/Rigging/Editor/Bridge/WebBridgeServer.cs). No DOM here.
//
// Two ways in, no key ever typed:
//   - Unity opens this page with `?bridge=<port>&token=<t>&prefab=<asset path>[&gesture=<name>][&attack=<index>]`
//     (a deep link: `bridge.linked`, that enemy is opened at once);
//   - the page opened by hand finds Unity by itself (`connect`): the bridge starts with the editor, and
//     `GET /hello` gives its token to this web only (Unity checks the Origin; the browser lets no other
//     page read the answer).
// Not found = `bridge.active === false` and the app behaves as it always did (export / import by .zip).
//
// Routes (WebBridgeRoutes.cs):
//   GET  /hello                        → { token, project, bridge }   (no token: only for this web's origin)
//   GET  /ping                         → { project, unity, bridge, playing }
//   GET  /enemies                      → { enemies: [{ prefab, name, kind }] } — the part-based prefabs
//   GET  /enemy?prefab=                → { name, kind, prefab, revision, rigConfig, enemyConfig, sprites, sounds, artLibrary, warnings }
//   GET  /file?path=Assets/…           → raw bytes (png / wav / ogg / mp3 of the project)
//   GET  /sounds                       → { clips: [asset paths] } — every audio clip of the project
//   POST /gestures?prefab=&revision=[&force=1]   body = the partial .zip (rig-export.js ▸ buildGesturesBundle)
//                                      → { ok, summary, errors, warnings, revision } · 409 { reason, error, revision }
//   POST /rig[?prefab=&revision=][&force=1]      body = the WHOLE .zip (rig-export.js ▸ buildRigBundle)
//                                      → { ok, summary, errors, warnings, prefab, created, revision }
//                                        · 409 { reason: 'exists' | 'changed' | 'dirty' | 'playmode', prefab, revision }
//
// A call that fails to connect is retried after finding Unity again: a script recompile reloads its
// domain (the listener is down for a moment, and may come back on another port).

const params = new URLSearchParams(typeof location !== 'undefined' ? location.search : '');

/** Unity's side. `active` = connected; `linked` = Unity opened this page on `prefab` (a deep link). */
export const bridge = {
  port: Number(params.get('bridge')) || 0,
  token: params.get('token') || '',
  active: false,
  linked: false,
  project: '',
  prefab: params.get('prefab') || '',
  gesture: params.get('gesture') || '',
  attack: params.has('attack') ? Number(params.get('attack')) : -1,
};
bridge.active = bridge.linked = bridge.port > 0 && !!bridge.token;

/** Ports the bridge may be on (WebBridgeServer.DefaultPort and the next ones it tries when taken). */
const PORTS = Array.from({ length: 10 }, (_, i) => 7810 + i);

/** Thrown by every call: `status` 0 = Unity did not answer; 409 = it refused to write (`reason`). */
export class BridgeError extends Error {
  constructor(message, { status = 0, reason = '', data = null } = {}) {
    super(message);
    this.status = status;
    this.reason = reason;
    this.data = data;
  }
}

const url = (route, query = {}) => {
  const q = new URLSearchParams({ token: bridge.token, ...query });
  return `http://127.0.0.1:${bridge.port}${route}?${q}`;
};

/** A fetch that gives up: a port left half-open by a domain reload accepts and never answers. */
const fetchWithin = (href, ms, init = {}) => fetch(href, { cache: 'no-store', ...init, signal: AbortSignal.timeout(ms) });

const listeners = new Set();
/** `fn(bridge)` each time the connection comes or goes. */
export function onBridgeChange(fn) { listeners.add(fn); return () => listeners.delete(fn); }
const notify = () => listeners.forEach((fn) => { try { fn(bridge); } catch (e) { console.warn('[unity-bridge]', e); } });

let connecting = null;

/**
 * Finds Unity: every port at once, the first that answers `/hello` wins. True if connected. Never throws.
 * Already connected → asks again only with `force` (Unity restarted: new token, maybe another port).
 */
export function connect({ force = false } = {}) {
  if (bridge.active && !force) return Promise.resolve(true);
  if (connecting) return connecting;
  connecting = (async () => {
    const hello = async (port) => {
      const response = await fetchWithin(`http://127.0.0.1:${port}/hello`, 1500);
      if (!response.ok) throw new Error(String(response.status));
      const data = await response.json();
      if (!data?.token) throw new Error('no token');
      return { port, ...data };
    };
    try {
      const found = await Promise.any(PORTS.map(hello));
      Object.assign(bridge, { port: found.port, token: found.token, project: found.project || '', active: true });
    } catch {
      bridge.active = false;
    }
    connecting = null;
    notify();
    return bridge.active;
  })();
  return connecting;
}

async function call(route, query, init) {
  if (!bridge.active && !(await connect())) throw new BridgeError('Unity no está conectado: abre el editor de Unity con el proyecto (el puente arranca solo).');
  let response = null;
  for (let attempt = 0; attempt < 2 && !response; attempt++) {
    try { response = await fetchWithin(url(route, query), init?.method === 'POST' ? 600000 : 60000, init); }
    catch {
      // Down (a recompile) or moved: wait a moment and find it again.
      if (attempt === 0) { await new Promise((r) => setTimeout(r, 1000)); await connect({ force: true }); }
    }
  }
  if (!response) {
    bridge.active = false;
    notify();
    throw new BridgeError('Unity no responde. ¿Está abierto el editor? (Si estaba recompilando, vuelve a intentarlo.)');
  }
  return response;
}

async function json(route, query, init) {
  let response = await call(route, query, init);
  // Unity restarted (a new token): find it again and repeat once.
  if (response.status === 401 && (await connect({ force: true }))) response = await call(route, query, init);
  let data = null;
  try { data = await response.json(); } catch { /* not JSON: reported below by its status */ }
  if (response.status === 401) throw new BridgeError('Unity se reinició y no se pudo volver a conectar.', { status: 401 });
  if (!response.ok) throw new BridgeError(data?.error || `Unity respondió ${response.status}.`, { status: response.status, reason: data?.reason || '', data });
  return data;
}

export const ping = () => json('/ping');

/** The part-based prefabs of the project: what can be opened from here. */
export const listEnemies = async () => (await json('/enemies')).enemies || [];

/** The enemy as it is SAVED in Unity. */
export const fetchEnemy = (prefab = bridge.prefab) => json('/enemy', { prefab });

/** The project's audio clips (asset paths): what a gesture's sound can be. */
export const fetchSounds = async () => (await json('/sounds')).clips || [];

/** A project file (png / wav / ogg / mp3) as a Blob. */
export async function fetchFile(path) {
  const response = await call('/file', { path });
  if (!response.ok) throw new BridgeError(`Unity no pudo servir '${path}' (${response.status}).`, { status: response.status });
  return response.blob();
}

/** The same, as a data URL (what a part image is in a composer document). */
export async function fetchDataURL(path) {
  const blob = await fetchFile(path);
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = reject;
    reader.readAsDataURL(blob);
  });
}

const zipPost = (blob) => ({ method: 'POST', headers: { 'Content-Type': 'application/zip' }, body: blob });

/**
 * Writes the partial bundle into the prefab. `revision` = the one `/enemy` gave: Unity refuses (409)
 * if the prefab changed since, unless `force`.
 * @returns {{ ok: boolean, summary: string, errors: string[], warnings: string[], revision: string }}
 */
export const saveGestures = (zipBlob, { prefab = bridge.prefab, revision = '', force = false } = {}) => json(
  '/gestures', { prefab, revision, ...(force ? { force: '1' } : {}) }, zipPost(zipBlob));

/**
 * Writes the WHOLE enemy (the Enemy Studio bundle): creates the prefab, or updates it — body included,
 * what was tuned in the Inspector kept. Without `prefab` it goes by name to the usual folder, and a
 * prefab already there is a 409 'exists' until `force`.
 * @returns {{ ok: boolean, summary: string, errors: string[], warnings: string[], prefab: string, created: boolean, revision: string }}
 */
export const saveRig = (zipBlob, { prefab = '', revision = '', force = false } = {}) => json(
  '/rig', { ...(prefab ? { prefab } : {}), ...(revision ? { revision } : {}), ...(force ? { force: '1' } : {}) }, zipPost(zipBlob));
