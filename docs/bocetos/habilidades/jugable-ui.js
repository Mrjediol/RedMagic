// jugable-ui.js — panel (huecos, mods, opciones, ajustes con exportar/importar), entrada y bucle de la versión jugable.
'use strict';
const UI = {};
(function () {
  const KEY = 'hab.jugable.v1';
  const store = { load() { try { return JSON.parse(localStorage.getItem(KEY) || 'null'); } catch (e) { return null; } },
                  save() { try { localStorage.setItem(KEY, JSON.stringify(snapshot())); } catch (e) { /* sin almacenamiento */ } } };
  const DEFAULT_SLOTS = ['fan', 'sing', 'ray'];
  function snapshot() { return { v: 1, slotCount: G.slotCount, slots: G.slots.map(s => ({ w: s.w, mods: s.mods })), settings: G.settings, tuning: G.tuning }; }
  function apply(snap) {
    if (!snap) return;
    if (snap.slotCount) G.slotCount = clamp(snap.slotCount, 2, 3);
    if (snap.slots) snap.slots.forEach((s, i) => { if (G.slots[i] && WEAPONS[s.w]) { G.slots[i].w = s.w; G.slots[i].mods = Object.assign({}, s.mods); } });
    if (snap.settings) Object.assign(G.settings, snap.settings);
    if (snap.tuning) for (const id in snap.tuning) if (G.tuning[id]) Object.assign(G.tuning[id], snap.tuning[id]);
  }
  function makeInst(s) { s.inst = WEAPONS[s.w].create(s); }

  // ------------------------------------------------ estado inicial
  const ids = Object.keys(WEAPONS);
  G.slots = DEFAULT_SLOTS.map((w, i) => ({ w: WEAPONS[w] ? w : ids[i % ids.length], mods: {} }));
  G.player = makePlayer(); resetEnemies();
  apply(store.load());
  G.slots.forEach(makeInst);

  // ------------------------------------------------ panel
  const panel = document.getElementById('panel');
  function el(tag, attrs = {}, ...kids) { const e = document.createElement(tag); for (const k in attrs) { if (k === 'on') for (const ev in attrs.on) e.addEventListener(ev, attrs.on[ev]); else if (k in e) e[k] = attrs[k]; else e.setAttribute(k, attrs[k]); } kids.flat().forEach(c => e.append(c)); return e; }
  function build() {
    panel.innerHTML = '';
    // huecos
    const slotsCard = el('div', { className: 'card' }, el('h3', {}, 'Armas equipadas', el('label', {}, el('input', { type: 'checkbox', checked: G.slotCount === 3, on: { change: e => { G.slotCount = e.target.checked ? 3 : 2; if (G.cur >= G.slotCount) G.cur = 0; changed(); } } }), '3er hueco')));
    for (let i = 0; i < G.slotCount; i++) {
      const s = G.slots[i], d = WEAPONS[s.w];
      const box = el('div', { className: 'slot' + (i === G.cur ? ' on' : '') });
      const sel = el('select', { on: { change: e => { s.w = e.target.value; s.mods = {}; makeInst(s); changed(); } } }, ids.map(id => el('option', { value: id, selected: id === s.w }, `${i + 1} · ${WEAPONS[id].name}`)));
      box.append(sel);
      for (const m of d.mods) box.append(el('label', { className: 'mod', title: m.desc }, el('input', { type: 'checkbox', checked: !!s.mods[m.id], on: { change: e => { s.mods[m.id] = e.target.checked; changed(false); } } }), m.name, el('span', { className: 'd' }, '— ' + m.desc)));
      slotsCard.append(box);
    }
    panel.append(slotsCard);
    // opciones
    const opts = el('div', { className: 'card' }, el('h3', {}, 'Opciones'));
    const tog = (k, txt) => opts.append(el('label', {}, el('input', { type: 'checkbox', checked: !!G.settings[k], on: { change: e => { G.settings[k] = e.target.checked; if (k === 'sound' && e.target.checked) audioInit(); store.save(); } } }), txt));
    tog('reactions', 'Reacciones (añicos, deshielo, vapor)'); tog('frames', 'Impact frames'); tog('hitstop', 'Hit-stop (micro-pausa al golpear fuerte)');
    tog('sound', '🔊 Sonido'); tog('enemyAttacks', 'Los enemigos disparan (para probar robo de vida)'); tog('hitboxes', 'Ver hitboxes');
    const sp = el('input', { type: 'range', min: 0.1, max: 1, step: 0.05, value: G.timeScale, on: { input: e => { G.timeScale = +e.target.value; spv.textContent = G.timeScale.toFixed(2) + '×'; } } }), spv = el('span', {}, G.timeScale.toFixed(2) + '×');
    opts.append(el('label', {}, 'Velocidad del juego', sp, spv));
    opts.append(el('div', { className: 'row', style: 'margin-top:8px' },
      el('button', { on: { click: resetEnemies } }, '↺ Enemigos (R)'),
      el('button', { on: { click: exportJson } }, '⬇ Exportar ajustes'),
      el('button', { on: { click: () => fileIn.click() } }, '⬆ Importar'),
      el('button', { on: { click: resetTuning } }, 'Valores por defecto')));
    panel.append(opts);
    // ajustes de lo equipado + reacciones
    const tun = el('div', { className: 'card' }, el('h3', {}, 'Ajustes (se guardan en este navegador; ⬇ para pasarlos al repo)'));
    const used = [...new Set(G.slots.slice(0, G.slotCount).map(s => s.w))];
    for (const id of used) tun.append(tuningBlock(WEAPONS[id].name, id, WEAPONS[id].tuning, used.length === 1));
    tun.append(tuningBlock('Reacciones (sistema común)', 'react', REACT_TUNING, false));
    panel.append(tun);
  }
  function tuningBlock(title, id, spec, open) {
    const det = el('details', { open }); det.append(el('summary', {}, title));
    const grid = el('div', { className: 'tun' });
    for (const k in spec) {
      const [, mn, mx, st, lab] = spec[k];
      const num = el('input', { type: 'number', min: mn, max: mx, step: st, value: G.tuning[id][k] });
      const rng = el('input', { type: 'range', min: mn, max: mx, step: st, value: G.tuning[id][k] });
      const set = v => { G.tuning[id][k] = +v; num.value = v; rng.value = v; store.save(); };
      num.oninput = e => set(e.target.value); rng.oninput = e => set(e.target.value);
      grid.append(el('span', {}, lab), num, rng);
    }
    det.append(grid); return det;
  }
  function changed(rebuild = true) { store.save(); if (rebuild) build(); }
  UI.refreshSlots = () => { panel.querySelectorAll('.slot').forEach((b, i) => b.classList.toggle('on', i === G.cur)); };
  function resetTuning() {
    for (const id in WEAPONS) G.tuning[id] = Object.fromEntries(Object.entries(WEAPONS[id].tuning).map(([k, v]) => [k, v[0]]));
    G.tuning.react = Object.fromEntries(Object.entries(REACT_TUNING).map(([k, v]) => [k, v[0]]));
    changed();
  }
  function exportJson() {
    const blob = new Blob([JSON.stringify(Object.assign({ exportado: new Date().toISOString() }, snapshot()), null, 2)], { type: 'application/json' });
    const a = el('a', { href: URL.createObjectURL(blob), download: 'ajustes-armas.json' }); a.click();
  }
  const fileIn = el('input', { type: 'file', accept: '.json', style: 'display:none', on: { change: e => { const f = e.target.files[0]; if (!f) return; f.text().then(txt => { try { apply(JSON.parse(txt)); G.slots.forEach(makeInst); changed(); } catch (err) { console.warn(err); } }); } } });
  document.body.append(fileIn);
  build();

  // ------------------------------------------------ entrada
  const cv = document.getElementById('cv'), ctx = cv.getContext('2d');
  cv.width = VW * 2; cv.height = VH * 2;
  const toWorld = ev => { const r = cv.getBoundingClientRect(); G.mouse.sx = (ev.clientX - r.left) / r.width * VW; G.mouse.sy = (ev.clientY - r.top) / r.height * VH; };
  cv.addEventListener('mousemove', toWorld);
  cv.addEventListener('mousedown', ev => {
    cv.focus(); toWorld(ev); if (ev.button !== 0) return;
    if (G.settings.sound) audioInit();
    G.mouse.down = true; const s = G.slots[G.cur]; s.inst.onPress && s.inst.onPress(aimInfo());
  });
  window.addEventListener('mouseup', ev => { if (ev.button !== 0 || !G.mouse.down) return; G.mouse.down = false; const s = G.slots[G.cur]; s.inst.onRelease && s.inst.onRelease(aimInfo()); });
  cv.addEventListener('contextmenu', e => e.preventDefault());
  cv.addEventListener('wheel', e => { e.preventDefault(); swapWeapon(e.deltaY > 0 ? 1 : -1); }, { passive: false });
  cv.addEventListener('keydown', e => { if (['Space', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.code)) e.preventDefault(); if (!G.keys[e.code]) onKeyDown(e.code); G.keys[e.code] = true; });
  cv.addEventListener('keyup', e => { G.keys[e.code] = false; });
  cv.addEventListener('blur', () => { G.keys = {}; });

  // ------------------------------------------------ bucle
  let last = performance.now();
  function frame(now) {
    const dt = Math.min(0.05, (now - last) / 1000); last = now;
    G.mouse.x = G.cam.x + G.mouse.sx; G.mouse.y = G.mouse.sy;
    step(dt);
    ctx.setTransform(2, 0, 0, 2, 0, 0);
    render(ctx);
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
  cv.focus();
  window.__G = G;
})();
