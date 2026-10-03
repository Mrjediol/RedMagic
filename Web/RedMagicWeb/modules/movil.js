// modules/movil.js
// -----------------------------------------------------------------------------
// movil.html: 📋 Tareas on the phone — apuntes, talleres, launching, reviewing proposals — through the taller
// server on the PC (docs/architecture/tareas.md ▸ Phone).
//   1 · pairing: the link from the PC (Tareas ▸ 📱 Móvil) carries `#k=<key>`; it is sent once to /api/pair, which
//       answers with a cookie, and wiped from the address bar. Without the cookie every /api/ call is 401.
//   2 · the tab's markup is taken from index.html itself (#tab-todo), so the phone never drifts from the PC.
//   3 · the same two modules as on the PC, in `mobile` mode.

import { initTodoPanel } from './todo-panel.js';
import { initDesignsPanel } from './designs-panel.js';

const gate = document.getElementById('movilGate');
const toastEl = document.getElementById('movilToast');
let toastTimer = null;
function toast(text) {
  toastEl.textContent = text;
  toastEl.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { toastEl.hidden = true; }, 3200);
}
const say = (html) => { gate.innerHTML = html; gate.hidden = false; };

async function pair() {
  const key = new URLSearchParams(location.hash.slice(1)).get('k');
  if (!key) return;
  history.replaceState(null, '', location.pathname);   // the key never stays in the address bar / history
  const res = await fetch('/api/pair', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ key }) });
  toast(res.ok ? 'Móvil emparejado' : 'El enlace no vale: pide uno nuevo en el PC');
}

async function start() {
  try { await pair(); } catch { /* the hello below says what is wrong */ }
  let hello;
  try { hello = await fetch('/api/hello', { cache: 'no-store' }); } catch { hello = null; }
  if (!hello) { say('<p><b>No llego al PC.</b></p><p>¿Está encendido, con <code>start.cmd</code> abierto y Tailscale conectado en los dos?</p><p><button onclick="location.reload()">Reintentar</button></p>'); return; }
  if (hello.status === 401) { say('<p><b>Este móvil no está emparejado.</b></p><p>En el PC: 📋 Tareas ▸ <b>📱 Móvil</b>, y abre aquí ese enlace (o escanea el QR).</p>'); return; }
  if (!hello.ok) { say(`<p>El servidor respondió ${hello.status}.</p>`); return; }

  const html = await fetch('index.html', { cache: 'no-store' }).then((r) => r.text());
  const tab = new DOMParser().parseFromString(html, 'text/html').getElementById('tab-todo');
  if (!tab) { say('<p>No encuentro el tab de Tareas en index.html.</p>'); return; }
  tab.hidden = false;
  gate.hidden = true;
  document.body.prepend(document.adoptNode(tab));

  const todo = initTodoPanel({
    root: document.getElementById('todoRoot'),
    rail: document.getElementById('todoRail'),
    detail: document.getElementById('todoDetail'),
    extraTalleres: document.getElementById('todoTalleresExtra'),
    statusEl: document.getElementById('todoStatus'),
    toolbar: document.getElementById('todoToolbar'),
    quick: document.getElementById('todoQuick'),
    toast,
    mobile: true,
  });
  const designs = initDesignsPanel({
    root: document.getElementById('designsRoot'),
    enemiesRoot: document.getElementById('enemiesRoot'),
    statusEl: document.getElementById('designsStatus'),
    toast,
    onBadges: (b) => todo.setBadges(b),
    mobile: true,
  });
  designs.onShow();
  // Back from the phone's app switcher / lock screen: fresh data at once.
  document.addEventListener('visibilitychange', () => { if (!document.hidden) { todo.onShow(); designs.onShow(); } });
}

// A pairing link opened in a tab that already shows this page only changes the hash: start again.
window.addEventListener('hashchange', () => { if (location.hash.includes('k=')) location.reload(); });
start();
