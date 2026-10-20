// tools/sim.cjs — carga jugable.html en un `vm` con un canvas falso para pruebas dirigidas de un arma (sin navegador).
// Uso:  const sim = require('./sim.cjs')();  sim.setup({ slots: [{ w: 'time', arch: 'stasis', ups: { quake: true } }, { w: 'fan' }], items: ['dup'] });
//       sim.aim(900, 290); sim.press(); sim.run(0.5); sim.release(); sim.run(3);  → sim.G (estado), sim.dmg() (daño total)
// Con { enemies: [{ x, kind, y }] } se sustituyen las dianas por las que digas (kind: ground | walker | flyer).
const fs = require('fs'), path = require('path'), vm = require('vm');
module.exports = function makeSim(dir = path.join(__dirname, '..')) {
  const html = fs.readFileSync(path.join(dir, 'jugable.html'), 'utf8');
  const files = [...html.matchAll(/<script src="([^"]+)"/g)].map(m => m[1]).filter(f => f !== 'jugable-ui.js');
  let nan = 0;
  const ctx = new Proxy({}, { get(_, k) {
    if (k === 'createRadialGradient' || k === 'createLinearGradient') return () => ({ addColorStop() {} });
    if (k === 'measureText') return () => ({ width: 10 });
    return (...a) => { if (a.some(x => typeof x === 'number' && !isFinite(x))) nan++; };
  }, set() { return true; } });
  // Math.random con semilla: cada setup() vuelve a la misma secuencia → comparaciones justas entre dos configuraciones
  let seed = 1; const M = Object.create(Math); M.random = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
  const sb = { console, Math: M, performance: { now: () => 1000 }, window: {}, document: { getElementById: () => ({}) }, setTimeout: () => 0, UI: {} };
  vm.createContext(sb);
  for (const f of files) vm.runInContext(fs.readFileSync(path.join(dir, f), 'utf8'), sb, { filename: f });
  vm.runInContext('this.__api = { G, WEAPONS, ITEMS, step, render, makePlayer, resetEnemies, makeEnemy, aimInfo, selectSlot, alive, T };', sb);
  const A = sb.__api, G = A.G, dt = 1 / 60;
  const sim = {
    G, api: A, sb,
    setup(o = {}) {
      seed = o.seed || 12345; G.t = 0; G.ents = []; G.texts = []; G.dmgTotal = 0; G.dmgLog = []; G.lastSwap = -9; G.swapBuff = -9; G.quickUntil = 0; G.timeScale = 1; G.hitStop = 0; G.decoys = []; G.empower = 0;
      G.settings.sound = false; G.settings.enemyAttacks = !!o.enemyAttacks; G.settings.hpMul = o.hpMul || 1;
      G.build = { items: Object.fromEntries((o.items || []).map(i => [i, true])) };
      G.player = A.makePlayer(); if (o.px) G.player.x = o.px;
      if (o.enemies) G.enemies = o.enemies.map(sp => A.makeEnemy(Object.assign({ x0: sp.x - 100, x1: sp.x + 100 }, sp))); else A.resetEnemies();
      G.cur = 0; const sl = o.slots || [{ w: 'fan' }, { w: 'star' }]; G.slotCount = sl.length;
      G.slots = sl.map(s => ({ w: s.w, arch: s.arch || null, ups: s.ups || {} })); G.slots.forEach(s => s.inst = A.WEAPONS[s.w].create(s));
      G.mouse.down = false; nan = 0; return sim;
    },
    aim(x, y) { G.mouse.x = x; G.mouse.y = y; return sim; },
    aimAt(e) { return sim.aim(e.x, e.y); },
    press() { G.mouse.down = true; const s = G.slots[G.cur]; s.inst.onPress && s.inst.onPress(A.aimInfo()); return sim; },
    release() { G.mouse.down = false; const s = G.slots[G.cur]; s.inst.onRelease && s.inst.onRelease(A.aimInfo()); return sim; },
    tap() { sim.press(); sim.run(dt); return sim.release(); },
    swap(i) { A.selectSlot(i); return sim; },
    run(secs, each) { const n = Math.round(secs / dt); for (let i = 0; i < n; i++) { if (each) each(i * dt); A.step(dt); A.render(ctx); } return sim; },
    dmg() { return Math.round(G.dmgTotal); },
    nan() { return nan; },
    enemies() { return G.enemies; },
    ents(kind) { return G.ents.filter(e => !kind || e.kind === kind); },
  };
  return sim;
};
// pequeño corredor de comprobaciones: check(nombre, condición, detalle)
module.exports.checker = function () {
  let bad = 0, n = 0;
  const check = (name, ok, info = '') => { n++; if (!ok) bad++; console.log(`${ok ? 'ok  ' : 'MAL '} ${name}${info !== '' ? ' — ' + info : ''}`); };
  check.done = () => { console.log(bad ? `${bad} de ${n} mal` : `todo ok (${n})`); if (bad) process.exitCode = 1; };
  return check;
};
