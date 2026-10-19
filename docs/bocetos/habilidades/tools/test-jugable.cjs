// tools/test-jugable.cjs — prueba sin navegador de jugable.html: cada arma × cada combinación de mods y cada pareja de armas,
// disparando a los enemigos unos segundos; avisa de errores y de NaN. Uso: node docs/bocetos/habilidades/tools/test-jugable.cjs ["" | carpeta] [id de arma]
const fs = require('fs'), path = require('path'), vm = require('vm');
const dir = process.argv[2] || path.join(__dirname, '..'), only = process.argv[3];
const html = fs.readFileSync(path.join(dir, 'jugable.html'), 'utf8');
const files = [...html.matchAll(/<script src="([^"]+)"/g)].map(m => m[1]).filter(f => f !== 'jugable-ui.js');
let nan = 0, calls = 0; const nanWhere = {};
const fn = () => (...a) => { calls++; if (a.some(x => typeof x === 'number' && !isFinite(x))) { nan++; } };
const ctx = new Proxy({}, { get(_, k) {
  if (k === 'createRadialGradient' || k === 'createLinearGradient') return () => ({ addColorStop() {} });
  if (k === 'measureText') return () => ({ width: 10 });
  return (...a) => { calls++; if (a.some(x => typeof x === 'number' && !isFinite(x))) { nan++; nanWhere[k] = (nanWhere[k] || 0) + 1; } };
}, set() { return true; } });
const sandbox = { console, Math, performance: { now: () => 1000 }, window: {}, document: { getElementById: () => ({}) }, setTimeout: () => 0 };
vm.createContext(sandbox);
for (const f of files) vm.runInContext(fs.readFileSync(path.join(dir, f), 'utf8'), sandbox, { filename: f });
vm.runInContext(`this.__api = { G, WEAPONS, step, render, makePlayer, resetEnemies, aimInfo };`, sandbox);
const { G, WEAPONS, step, render, makePlayer, resetEnemies } = sandbox.__api;
const ids = Object.keys(WEAPONS);
function combos(mods) { const out = [{}]; for (const m of mods) for (const c of out.slice()) out.push(Object.assign({}, c, { [m.id]: true })); return out; }
function run(slotsDef, script, secs = 4) {
  G.t = 0; G.ents = []; G.texts = []; G.dmgTotal = 0; G.dmgLog = []; G.player = makePlayer(); resetEnemies(); G.cur = 0; G.slotCount = slotsDef.length;
  G.slots = slotsDef.map(s => ({ w: s.w, mods: s.mods })); G.slots.forEach(s => s.inst = WEAPONS[s.w].create(s));
  nan = 0; calls = 0; for (const k in nanWhere) delete nanWhere[k];
  const dt = 1 / 60; let err = null;
  for (let i = 0; i < secs * 60 && !err; i++) {
    try { script(i * dt, i); step(dt); render(ctx); } catch (e) { err = `t=${(i * dt).toFixed(2)} ${e.stack.split('\n').slice(0, 4).join(' | ')}`; }
  }
  const dmg = G.dmgTotal;
  return { err, nan: nan ? JSON.stringify(nanWhere) : '', dmg: Math.round(dmg), ents: G.ents.length };
}
// guion: apunta al enemigo vivo más cercano por delante; pulsa y mantiene 1 s, suelta; repite; cambia de arma a mitad
const aimAt = (x, y) => { G.mouse.x = x; G.mouse.y = y; };
function script(t, i) {
  const p = G.player, tgt = G.enemies.filter(e => !e.dead && e.x > p.x).sort((a, b) => a.x - b.x)[0] || G.enemies[0];
  aimAt(tgt.x, tgt.y);
  const phase = t % 1.3, s = G.slots[G.cur];
  if (phase < 1 / 60 + 1e-6) { G.mouse.down = true; s.inst.onPress && s.inst.onPress(sandbox.__api.aimInfo()); }
  if (Math.abs(phase - 1.0) < 1 / 120) { G.mouse.down = false; s.inst.onRelease && s.inst.onRelease(sandbox.__api.aimInfo()); }
  if (i === 140 && G.slotCount > 1) { G.mouse.down = false; s.inst.onRelease && s.inst.onRelease(sandbox.__api.aimInfo()); G.cur = 1; }
  if (i % 50 === 0) p.vx = 120;                       // se mueve un poco
}
let bad = 0;
for (const id of ids) {
  if (only && id !== only) continue;
  for (const c of combos(WEAPONS[id].mods)) {
    const r = run([{ w: id, mods: c }, { w: ids[(ids.indexOf(id) + 1) % ids.length], mods: {} }], script, 4);
    const tag = `${id} [${Object.keys(c).join('+') || 'base'}]`;
    if (r.err || r.nan) bad++;
    console.log(`${r.err ? 'ERR ' : r.nan ? 'NaN ' : 'ok  '} ${tag.padEnd(28)} daño ${String(r.dmg).padStart(5)} · ents ${r.ents}${r.nan ? ' · NaN ' + r.nan : ''}${r.err ? '\n     ' + r.err : ''}`);
  }
}
// parejas: cada arma con cada otra (todo activado)
if (!only) for (const a of ids) for (const b of ids) if (a !== b) {
  const all = d => Object.fromEntries(WEAPONS[d].mods.map(m => [m.id, true]));
  const r = run([{ w: a, mods: all(a) }, { w: b, mods: all(b) }], script, 3);
  if (r.err || r.nan) { bad++; console.log(`${r.err ? 'ERR ' : 'NaN '} pareja ${a}+${b} ${r.err || r.nan}`); }
}
console.log(bad ? `${bad} con problemas` : 'todo ok');
