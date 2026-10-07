// modules/press-guard.js
// -----------------------------------------------------------------------------
// A click is a mousedown AND a mouseup on the same element. A panel that redraws itself (a poll, leaving a field)
// between the two swaps the button under the pointer and the browser drops the click: "I had to click ▶ Lanzar
// several times". Panels ask `busy()` before a redraw and, while a button is held down inside them, put it off with
// `after(fn)`: it runs right after the click has been delivered.

export function pressGuard(scopes) {
  let down = false;
  const later = new Set();
  document.addEventListener('pointerdown', (e) => { if (scopes().some((s) => s?.contains(e.target))) down = true; }, true);
  const release = () => {
    if (!down) return;
    // After the click: it is dispatched right behind pointerup (a touch's a little later).
    setTimeout(() => {
      down = false;
      const fns = [...later];
      later.clear();
      fns.forEach((fn) => fn());
    }, 40);
  };
  document.addEventListener('pointerup', release, true);
  document.addEventListener('pointercancel', release, true);
  return { busy: () => down, after: (fn) => later.add(fn) };
}
