// modules/task-notify.js
// -----------------------------------------------------------------------------
// 🔔 Avisos: a notification of the system (and, if chosen, a short chime) when a run of the taller queue
// ends. The server lists the last finished runs in /api/status (`finished`, newest first); the 📋 Tareas
// poll hands them here and each runId is announced once. The first list seen is only remembered: opening
// the web never announces what ended before it.
//
// Modes (localStorage, per browser): 'off' · 'on' (silent) · 'sound'. The chime is synthesised (WebAudio),
// no file. No DOM of its own: todo-panel draws the toolbar button.

const MODE_KEY = 'redmagic.todo.notify';
export const NOTIFY_MODES = ['off', 'on', 'sound'];
export const NOTIFY_LABEL = { off: '🔕 Avisos: no', on: '🔔 Avisos', sound: '🔔🔊 Avisos con sonido' };

let mode = 'off';
try { mode = NOTIFY_MODES.includes(localStorage.getItem(MODE_KEY)) ? localStorage.getItem(MODE_KEY) : 'off'; } catch { /* private window */ }

const seen = new Set();
let primed = false;
let audio = null;

export const notifyMode = () => mode;
const canNotify = () => typeof Notification !== 'undefined';

/** The browser lets audio start only after a gesture: the toggle's click, or any click later. */
function wakeAudio() {
  try {
    audio = audio || new (window.AudioContext || window.webkitAudioContext)();
    if (audio.state === 'suspended') audio.resume();
  } catch { audio = null; }
}
document.addEventListener('pointerdown', () => { if (mode === 'sound' && audio?.state !== 'running') wakeAudio(); }, { capture: true });

/** Two rising notes = done; two falling = failed / stopped. */
function chime(ok) {
  wakeAudio();
  if (!audio) return;
  const notes = ok ? [660, 990] : [520, 330];
  const t0 = audio.currentTime + 0.02;
  notes.forEach((f, i) => {
    const o = audio.createOscillator();
    const g = audio.createGain();
    o.type = 'sine';
    o.frequency.value = f;
    const t = t0 + i * 0.16;
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(0.25, t + 0.02);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.35);
    o.connect(g).connect(audio.destination);
    o.start(t);
    o.stop(t + 0.4);
  });
}

/**
 * Next mode (off → on → sound → off). Asks for the browser's permission when turning on.
 * @returns {Promise<string>} a message for the toast
 */
export async function cycleNotifyMode() {
  const next = NOTIFY_MODES[(NOTIFY_MODES.indexOf(mode) + 1) % NOTIFY_MODES.length];
  if (next !== 'off' && canNotify() && Notification.permission === 'default') {
    try { await Notification.requestPermission(); } catch { /* old browsers: callback form, ignored */ }
  }
  mode = next;
  try { localStorage.setItem(MODE_KEY, mode); } catch { /* private window */ }
  if (mode === 'off') return 'Avisos desactivados.';
  if (mode === 'sound') chime(true);   // also unlocks audio with this click
  if (!canNotify() || Notification.permission !== 'granted') {
    return 'El navegador no deja mostrar notificaciones (permiso bloqueado): solo verás el aviso dentro de la web'
      + (mode === 'sound' ? ' y oirás el sonido.' : '.');
  }
  return mode === 'sound' ? 'Avisos con sonido activados.' : 'Avisos sin sonido activados.';
}

/**
 * Announce the runs of `finished` not seen yet.
 * @param {Array<{runId: string, taskId: string, title: string, step?: string, reply?: string, ok: boolean, cancelled?: boolean, stuck?: boolean}>} finished
 * @param {{ toast: (t: string) => void, open: (taskId: string) => void }} o
 */
export function announceFinished(finished, { toast, open }) {
  if (!Array.isArray(finished)) return;
  const fresh = finished.filter((f) => f.runId && !seen.has(f.runId));
  fresh.forEach((f) => seen.add(f.runId));
  if (!primed) { primed = true; return; }
  if (mode === 'off' || !fresh.length) return;
  fresh.reverse().forEach((f) => {
    const state = f.ok ? '✅ Terminada' : f.cancelled ? '⏹ Cancelada' : f.stuck ? '⛔ Atascada' : '❌ Falló';
    const what = f.step ? `Paso: ${f.step}` : f.reply ? 'Respuesta a tu instrucción' : 'Tarea';
    toast(`${state}: ${f.title}`);
    if (canNotify() && Notification.permission === 'granted') {
      try {
        const n = new Notification(`${state} · ${f.title}`.slice(0, 120), { body: `${what}. Mira el informe en 📋 Tareas.`, tag: f.runId, silent: true });
        n.onclick = () => { window.focus(); open(f.taskId); n.close(); };
      } catch { /* Android Chrome: only through a service worker — the toast already said it */ }
    }
  });
  if (mode === 'sound') chime(fresh.every((f) => f.ok));
}
