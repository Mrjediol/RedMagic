// tools/test-jugable.cjs — prueba sin navegador de jugable.html: cada arma × (base, cada evolución con cada combinación de sus
// mejoras, o su lista de mejoras) × build (nada / 4 hielo / 4 fuego) × Duplicador, y cada pareja de armas al máximo con varias
// builds completas de 6 items; dispara a los enemigos unos segundos y avisa de errores y de NaN.
// Uso: node docs/bocetos/habilidades/tools/test-jugable.cjs ["" | carpeta] [id de arma]   (VERBOSE=1 para ver cada caso)
const fs = require('fs'), path = require('path'), vm = require('vm');
const dir = process.argv[2] || path.join(__dirname, '..'), only = process.argv[3];
const html = fs.readFileSync(path.join(dir, 'jugable.html'), 'utf8');
const files = [...html.matchAll(/<script src="([^"]+)"/g)].map(m => m[1]).filter(f => f !== 'jugable-ui.js');
let nan = 0; const nanWhere = {};
const ctx = new Proxy({}, { get(_, k) {
  if (k === 'createRadialGradient' || k === 'createLinearGradient') return () => ({ addColorStop() {} });
  if (k === 'measureText') return () => ({ width: 10 });
  return (...a) => { if (a.some(x => typeof x === 'number' && !isFinite(x))) { nan++; nanWhere[k] = (nanWhere[k] || 0) + 1; } };
}, set() { return true; } });
const sandbox = { console, Math, performance: { now: () => 1000 }, window: {}, document: { getElementById: () => ({}) }, setTimeout: () => 0 };
vm.createContext(sandbox);
for (const f of files) vm.runInContext(fs.readFileSync(path.join(dir, f), 'utf8'), sandbox, { filename: f });
vm.runInContext('this.__api = { G, WEAPONS, ITEMS, step, render, makePlayer, resetEnemies, aimInfo };', sandbox);
const { G, WEAPONS, ITEMS, step, render, makePlayer, resetEnemies, aimInfo } = sandbox.__api;
const ids = Object.keys(WEAPONS);
const BUILDS = { '-': [], ice4: ['anillo', 'baston', 'botasR', 'grimorio'], fire4: ['swap', 'void', 'sharp', 'blood'] };
const FULL = { ice6: ['anillo', 'baston', 'botasR', 'capa', 'grimorio', 'yelmo'], fire6: ['dup', 'swap', 'const', 'void', 'sharp', 'blood'], mix: ['yelmo', 'grimorio', 'gear', 'capa', 'espadaO', 'botasO'], gold: ['espadaO', 'botasO', 'guantesO', 'dup', 'baston', 'gear'] };

function combos(list) { const out = [{}]; for (const m of list) for (const c of out.slice()) out.push(Object.assign({}, c, { [m.id]: true })); return out; }
function configs(d) {
  if (d.archetypes) return [{ arch: null, ups: {} }, ...d.archetypes.flatMap(a => combos(a.ups).map(u => ({ arch: a.id, ups: u })))];
  return combos(d.ups || []).map(u => ({ arch: null, ups: u }));
}
function run(slotsDef, secs, build) {
  G.t = 0; G.ents = []; G.texts = []; G.dmgTotal = 0; G.dmgLog = []; G.lastSwap = -9; G.swapBuff = -9;
  G.build = { items: Object.fromEntries(build.map(i => [i, true])) };
  G.settings.enemyAttacks = true;                            // con balas enemigas: también se prueban paradas, reflejos, señuelos…
  G.player = makePlayer(); resetEnemies(); G.cur = 0; G.slotCount = slotsDef.length;
  G.slots = slotsDef.map(s => ({ w: s.w, arch: s.arch || null, ups: s.ups || {} })); G.slots.forEach(s => s.inst = WEAPONS[s.w].create(s));
  nan = 0; for (const k in nanWhere) delete nanWhere[k];
  const dt = 1 / 60; let err = null;
  for (let i = 0; i < secs * 60 && !err; i++) {
    try { script(i * dt, i); step(dt); render(ctx); } catch (e) { err = `t=${(i * dt).toFixed(2)} ${e.stack.split('\n').slice(0, 4).join(' | ')}`; }
  }
  return { err, nan: nan ? JSON.stringify(nanWhere) : '', dmg: Math.round(G.dmgTotal) };
}
// guion: apunta al enemigo vivo más cercano por delante; pulsa y mantiene 1 s, suelta; repite; cambia de arma a mitad (y vuelve)
function script(t, i) {
  const p = G.player, tgt = G.enemies.filter(e => !e.dead && e.x > p.x).sort((a, b) => a.x - b.x)[0] || G.enemies[0];
  G.mouse.x = tgt.x; G.mouse.y = tgt.y;
  const phase = t % 1.3, s = G.slots[G.cur];
  if (phase < 1 / 60 + 1e-6) { G.mouse.down = true; s.inst.onPress && s.inst.onPress(aimInfo()); }
  if (Math.abs(phase - 1.0) < 1 / 120) { G.mouse.down = false; s.inst.onRelease && s.inst.onRelease(aimInfo()); }
  if ((i === 100 || i === 150) && G.slotCount > 1) { G.mouse.down = false; s.inst.onRelease && s.inst.onRelease(aimInfo()); sandbox.selectSlot ? 0 : 0; G.cur = (G.cur + 1) % G.slotCount; G.lastSwap = G.t; }
  if (i % 50 === 0) p.vx = 120;
}
let bad = 0, n = 0;
const ELEMS = Object.keys(BUILDS);
for (const id of ids) {
  if (only && id !== only) continue;
  for (const c of configs(WEAPONS[id])) for (const el of ELEMS) for (const dup of [false, true]) {
    n++;
    const r = run([Object.assign({ w: id }, c), { w: ids[(ids.indexOf(id) + 1) % ids.length] }], 3, BUILDS[el].concat(dup ? ['dup'] : []));
    const tag = `${id} [${c.arch || 'base'}${Object.keys(c.ups).length ? '+' + Object.keys(c.ups).join('+') : ''}] ${el}${dup ? ' dup' : ''}`;
    if (r.err || r.nan) bad++;
    if (r.err || r.nan || process.env.VERBOSE) console.log(`${r.err ? 'ERR ' : r.nan ? 'NaN ' : 'ok  '} ${tag.padEnd(46)} daño ${String(r.dmg).padStart(5)}${r.nan ? ' · NaN ' + r.nan : ''}${r.err ? '\n     ' + r.err : ''}`);
  }
}
// parejas: cada arma al máximo con cada otra, todos los items, con cada elemento
if (!only) for (const a of ids) for (const b of ids) if (a !== b) {
  const max = d => { const cs = configs(WEAPONS[d]); return cs[cs.length - 1]; };
  for (const el in FULL) {
    n++;
    const r = run([Object.assign({ w: a }, max(a)), Object.assign({ w: b }, max(b))], 3, FULL[el]);
    if (r.err || r.nan) { bad++; console.log(`${r.err ? 'ERR ' : 'NaN '} pareja ${a}+${b} ${el} ${r.err || r.nan}`); }
  }
}
console.log(bad ? `${bad} de ${n} con problemas` : `todo ok (${n} casos)`);
