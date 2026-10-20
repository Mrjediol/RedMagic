// tools/test.cjs — prueba sin navegador de la jugable de enemigos: carga jugable.html en un `vm` con un canvas falso.
// Uso: node docs/bocetos/enemigos/tools/test.cjs [id de enemigo]
//  1. Duelo contra un player QUIETO (cada enemigo tiene que pasar por todos sus movimientos y llegar a hacer daño).
//  2. Duelo y encuentros contra un bot que se mueve, salta, hace dash y dispara (sin errores ni NaN; cuánto tarda y cuánto recibe).
//  3. Casos dirigidos: el Embestidor se estampa contra la pared, el Fatuo daña a otros enemigos, el escudo del Caparazón tapa.
const fs = require('fs'), path = require('path'), vm = require('vm');
const dir = path.join(__dirname, '..');
const html = fs.readFileSync(path.join(dir, 'jugable.html'), 'utf8');
const files = [...html.matchAll(/<script src="([^"]+)"/g)].map(m => m[1]).filter(f => !/(^|\/)(ui|jugable-ui)\.js$/.test(f));
let nan = 0;
const ctx = new Proxy({}, { get(_, k) {
  if (k === 'createRadialGradient' || k === 'createLinearGradient') return () => ({ addColorStop() {} });
  if (k === 'measureText') return () => ({ width: 10 });
  return (...a) => { if (a.some(x => typeof x === 'number' && !isFinite(x))) nan++; };
}, set() { return true; } });
let seed = 1; const M = Object.create(Math); M.random = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
const sb = { console, Math: M, performance: { now: () => 1000 }, window: {}, document: { getElementById: () => ({}) }, setTimeout: () => 0, setInterval: () => 0, UI: {} };
vm.createContext(sb);
for (const f of files) vm.runInContext(fs.readFileSync(path.join(dir, f), 'utf8'), sb, { filename: f });
vm.runInContext('this.__api = { G, FOES, WEAPONS, ENCOUNTERS, PLATFORMS, step, render, makePlayer, startEncounter, duelEncounter, aimInfo, alive, FT, spawnFoe, damage };', sb);
const A = sb.__api, G = A.G, dt = 1 / 60;
let bad = 0, n = 0;
const check = (name, ok, info = '') => { n++; if (!ok) bad++; console.log(`${ok ? 'ok  ' : 'MAL '} ${name}${info !== '' ? ' — ' + info : ''}`); };

function setup(enc, o = {}) {
  seed = o.seed || 12345; G.t = 0; G.ents = []; G.texts = []; G.dmgTotal = 0; G.dmgLog = []; G.timeScale = 1; G.hitStop = 0; G.decoys = []; G.empower = 0;
  G.settings.sound = false; G.build = { items: {} };
  Object.assign(G.es, { harmless: false, frozenAI: false, dashIframes: true, dmgMul: 1, hpMul: o.hpMul || 1, maxAttackers: 3, respawn: false, showStates: false, arena: o.arena || 'actual', crumble: !!o.crumble, crumbleT: 1, respawnT: 3, eliteSlots: 2 });
  G.player = A.makePlayer();
  G.cur = 0; const sl = o.slots || [{ w: 'fan' }, { w: 'star' }]; G.slotCount = sl.length;
  G.slots = sl.map(s => ({ w: s.w, arch: s.arch || null, ups: s.ups || {} })); G.slots.forEach(s => s.inst = A.WEAPONS[s.w].create(s));
  G.mouse.down = false; G._visits = {}; nan = 0;
  A.startEncounter(enc);
  if (o.px !== undefined) G.player.x = o.px;
}
function run(secs, each) {
  const steps = Math.round(secs / dt);
  for (let i = 0; i < steps; i++) {
    if (each) each(i * dt);
    try { A.step(dt); A.render(ctx); } catch (e) { return e; }
    if (G.fight.tEnd) break;
  }
  return null;
}
// bot: se mantiene a media distancia del más cercano, salta y hace dash de vez en cuando, dispara a ráfagas
function bot() {
  const p = G.player, foes = A.alive().filter(e => e.def);
  let tgt = null, bd = 1e9; for (const e of foes) { const d = Math.abs(e.x - p.x); if (d < bd) { bd = d; tgt = e; } }
  G.keys = {};
  if (tgt) {
    const dx = tgt.x - p.x;
    if (Math.abs(dx) > 320) G.keys[dx > 0 ? 'KeyD' : 'KeyA'] = true; else if (Math.abs(dx) < 140) G.keys[dx > 0 ? 'KeyA' : 'KeyD'] = true;
    G.mouse.x = tgt.x; G.mouse.y = tgt.y;
  }
  const r = Math.random();
  if (r < 0.012) sb.onKeyDown('KeyW'); else if (r < 0.018) sb.onKeyDown('ShiftLeft');
  const fire = Math.floor(G.t / 0.35) % 2 === 0, s = G.slots[G.cur];
  if (fire && !G.mouse.down) { G.mouse.down = true; s.inst.onPress && s.inst.onPress(A.aimInfo()); }
  else if (!fire && G.mouse.down) { G.mouse.down = false; s.inst.onRelease && s.inst.onRelease(A.aimInfo()); }
  if (Math.random() < 0.004) sb.onKeyDown('KeyQ');
}
const NEED = { charger: ['chargeW', 'charge'], stalker: ['crouch', 'leap', 'land'], mortar: ['aim', 'fire'], weaver: ['aim', 'shot'], shell: ['quakeW', 'quake'],
  hive: ['birthW'], drone: ['diveW', 'dive'], wisp: ['fuse'], burrower: ['under', 'emergeW', 'emerge', 'spitW', 'digW'],
  chargerAlpha: ['chargeW', 'charge'], stalkerAlpha: ['howlW', 'howl', 'crouch', 'leap', 'recrouch'],
  icicle: ['hang', 'shiver', 'fall', 'stuck', 'climb'], urchin: ['rainW', 'bald'], gale: ['gustW', 'gust', 'snowW'],
  hermit: ['grabW', 'grab', 'grabRec'], walrus: ['spikeW', 'spike'], walrusAlpha: ['slideW', 'slide', 'restUp'],
  shroom: ['hop', 'bigW', 'big', 'rest'], bramble: ['wallW', 'wall'], bear: ['swipeW', 'swipe', 'swipeGap'], owl: ['fly', 'perch', 'swoopW', 'swoop'],
  flytrap: ['vineW', 'vine'], shroomAlpha: ['bigW', 'big', 'rest'],
  salamander: ['dashW', 'dash', 'skid'], scorpion: ['stingW', 'stingRec'], emberbird: ['toStart', 'runW', 'run'], lavaserpent: ['under', 'leapW', 'leap', 'surface'],
  slaggolem: ['raiseW', 'raise'], phoenix: ['runW', 'run', 'backW'], pillar: [], emberEgg: [] };
const only = process.argv[2];
const types = Object.keys(A.FOES).filter(t => (!only || t === only) && !A.FOES[t].prop && !A.FOES[t].noFicha);

console.log('— 1. duelo contra un player quieto (30 s, sin morir: vida infinita)');
for (const t of types) {
  setup(A.duelEncounter(t, t === 'drone' ? 3 : 1), { px: A.FOES[t].trap ? 420 : undefined });   // una trampa no se mueve: el player se pone a su alcance
  const err = run(30, () => { G.player.hp = G.player.hpMax; G.keys = {}; });
  const vis = Object.keys(G._visits[t] || {}), miss = (NEED[t] || []).filter(s => !vis.includes(s));
  check(`${t}: sin errores`, !err, err ? err.stack.split('\n').slice(0, 3).join(' | ') : '');
  check(`${t}: pasa por todos sus movimientos`, !miss.length, miss.length ? 'faltan ' + miss.join(', ') : vis.join(' '));
  check(`${t}: hace daño a un player quieto`, G.fight.dmgTaken > 0, `${G.fight.dmgTaken} en 30 s · ` + Object.entries(G.fight.hits).map(([k, h]) => `${k.split(' · ')[1]} ${h.dmg}/${h.n}×`).join(', '));
  check(`${t}: sin NaN al dibujar`, nan === 0, nan ? nan + ' llamadas' : '');
}

console.log('— 2. contra un bot que se mueve y dispara (Abanico + Golpe estelar, máx. 60 s)');
const encs = [...types.map(t => A.duelEncounter(t, t === 'drone' ? 4 : 1)), ...(only ? [] : A.ENCOUNTERS)];
for (const enc of encs) {
  setup(enc);
  const err = run(60, () => { G.player.hp = Math.max(G.player.hp, 1); bot(); });
  const F = G.fight;
  check(`${enc.name}: sin errores ni NaN`, !err && nan === 0, err ? err.stack.split('\n').slice(0, 3).join(' | ') : nan ? nan + ' NaN' : '');
  console.log(`      ${F.tEnd ? 'despejado en ' + (F.tEnd - F.t0).toFixed(1) + ' s' : 'sin despejar (quedan ' + A.alive().filter(e => e.def).length + ')'} · recibe ${F.dmgTaken} · esquivas ${F.dodges} · interrupciones ${F.interrupts} · bajas ${F.kills}`);
}

if (!only) {
  console.log('— 2b. encuentros en la arena vertical con plataformas que se rompen (sólo errores)');
  for (const enc of A.ENCOUNTERS) { setup(enc, { arena: 'vertical', crumble: true }); const err = run(40, () => { G.player.hp = Math.max(G.player.hp, 1); bot(); }); check(`${enc.name} (vertical, se rompen): sin errores ni NaN`, !err && nan === 0, err ? err.stack.split(String.fromCharCode(10)).slice(0, 3).join(' | ') : nan ? nan + ' NaN' : ''); }
  console.log('— 3. casos dirigidos');
  // el Embestidor carga hacia el player pegado a la pared izquierda, le pasa y se estampa
  setup({ id: 't', name: 'pared', list: [{ type: 'charger', x: 700 }] }, { px: 90 });
  G.es.harmless = true; run(12, () => { G.keys = {}; });
  check('Embestidor: se estampa contra la pared', !!(G._visits.charger || {}).crash, Object.keys(G._visits.charger || {}).join(' '));
  // el Fatuo que revienta hace daño a los otros enemigos
  setup({ id: 't', name: 'bomba', list: [{ type: 'wisp', x: 300, y: 260 }, { type: 'shell', x: 330 }] }, { px: 250 });
  G.es.harmless = true; const sh = G.enemies[1], hp0 = sh.hp;
  run(4, () => { G.keys = {}; });
  check('Fatuo: su explosión daña a otros enemigos', sh.hp < hp0, `Caparazón ${Math.round(hp0)} → ${Math.round(sh.hp)}`);
  // escudo del Caparazón: de frente tapa, por la espalda no, golpe fuerte rompe la guardia
  setup({ id: 't', name: 'escudo', list: [{ type: 'shell', x: 600 }] }, { px: 400 });
  G.es.frozenAI = true; const s2 = G.enemies[0]; s2.face = -1;
  const front = A.damage(s2, { dmg: 20, kx: 100 }), back = A.damage(s2, { dmg: 20, kx: -100 });
  check('Caparazón: de frente ×0,15, de espalda completo', front < 5 && back >= 19, `frente ${front.toFixed(1)} · espalda ${back.toFixed(1)}`);
  A.damage(s2, { dmg: 20, kx: 100, heavy: true });
  check('Caparazón: golpe fuerte de frente rompe la guardia', s2.brokenT > G.t);
  // interrumpir: mucho daño mientras prepara cancela el ataque
  setup({ id: 't', name: 'aguante', list: [{ type: 'stalker', x: 500 }] }, { px: 300 });
  let interrupted = false; run(6, () => { G.player.hp = G.player.hpMax; G.keys = {}; const e = G.enemies[0]; if (e.phase === 'windup' && !interrupted) { A.damage(e, { dmg: 30, kx: 10 }); interrupted = G.fight.interrupts > 0; } });
  check('Aguante: daño al preparar = interrumpido', interrupted);
  // arenas y respuestas hacia arriba
  const onPl = pl => { const p = G.player; p.x = (pl.x0 + pl.x1) / 2; p.y = pl.y; p.vy = 0; p.onGround = true; };
  setup({ id: 't', name: 'rompe', list: [{ type: 'mortar', x: 1400 }] }, { crumble: true }); G.es.frozenAI = true;
  const n0 = A.PLATFORMS.length; onPl(A.PLATFORMS[0]); run(1.4, () => { G.keys = {}; });
  const n1 = A.PLATFORMS.length; run(3.5, () => { G.keys = {}; });
  check('Plataformas que se rompen: se rompe al estar encima y vuelve', n1 === n0 - 1 && A.PLATFORMS.length === n0, `${n0} → ${n1} → ${A.PLATFORMS.length}`);
  setup({ id: 't', name: 'salto', list: [{ type: 'charger', x: 1100 }] }, { arena: 'vertical' });
  const pv = A.PLATFORMS.find(pl => pl.x0 === 640); onPl(pv); run(8, () => { G.player.hp = G.player.hpMax; G.keys = {}; if (G.player.y >= 340) onPl(pv); });
  check('Embestidor: salta a tu plataforma baja con la carga', !!G.fight.hits['Embestidor · Embestida con salto'], Object.keys(G.fight.hits).join(', '));
  setup({ id: 't', name: 'pedrada', list: [{ type: 'shell', x: 1100 }] }, { arena: 'vertical' });
  onPl(pv); run(8, () => { G.player.hp = G.player.hpMax; G.keys = {}; if (G.player.y >= 340) onPl(pv); });
  check('Caparazón: si te subes, te lanza una piedra', !!G.fight.hits['Caparazón · Pedrada'], Object.keys(G.fight.hits).join(', '));
  setup({ id: 't', name: 'columna', list: [{ type: 'burrower', x: 500 }] });
  const pb = A.PLATFORMS.find(pl => pl.y === 196); onPl(pb); run(10, () => { G.player.hp = G.player.hpMax; G.keys = {}; });
  check('Excavador: su columna atraviesa la plataforma', !!G.fight.hits['Excavador · Emerger'], Object.keys(G.fight.hits).join(', '));
  setup({ id: 't', name: 'eriza', list: [{ type: 'urchin', x: 260 }] }, { px: 200 }); run(4, () => { G.player.hp = G.player.hpMax; G.keys = {}; });
  check('Erizo: si te pegas, se eriza y duele', !!G.fight.hits['Erizo de escarcha · Erizarse'], Object.keys(G.fight.hits).join(', '));
  setup({ id: 't', name: 'pinza', list: [{ type: 'hermit', x: 300 }] }, { px: 200 }); let froze = false; run(5, () => { G.player.hp = G.player.hpMax; G.keys = {}; if (G.player.lockT > G.t) froze = true; });
  check('Ermitaño: la pinza congela al player y remata', froze && !!G.fight.hits['Ermitaño de hielo · Pellizco'], Object.keys(G.fight.hits).join(', '));
  setup({ id: 't', name: 'coraza', list: [{ type: 'hermit', x: 700 }] }); G.es.frozenAI = true; const h = G.enemies[0];
  const d1 = A.damage(h, { dmg: 20, kx: 10 }); for (let i = 0; i < 3; i++) A.damage(h, { dmg: 10, heavy: true, kx: 10 });
  check('Ermitaño: la coraza reduce y los golpes fuertes la rompen', d1 < 8 && h.broken, `primer golpe ${d1.toFixed(1)} · rota ${h.broken}`);
  setup({ id: 't', name: 'viento', list: [{ type: 'gale', x: 500, y: 200 }] }, { px: 300 }); const x0 = G.player.x; let pushed = 0; run(8, () => { G.player.hp = G.player.hpMax; G.keys = {}; pushed = Math.max(pushed, Math.abs(G.player.x - x0)); });
  check('Ventisca: el soplo empuja al player', pushed > 60, Math.round(pushed) + ' px');
  setup({ id: 't', name: 'llamada', list: [{ type: 'stalkerAlpha', x: 650 }] });
  run(6, () => { G.player.hp = G.player.hpMax; G.keys = {}; });
  check('Acechador alfa: solo, aúlla y llama a su manada', A.alive().filter(e => e.type === 'stalker').length >= 2, Object.keys(G._visits.stalkerAlpha || {}).join(' ') + ' · ' + A.alive().map(e => e.type).join(','));
  // vida entre ataques
  setup({ id: 't', name: 'fuera', list: [{ type: 'mortar', x: 1400 }, { type: 'urchin', x: 1300 }] }); run(8, () => { G.player.hp = G.player.hpMax; G.keys = {}; });
  check('Nadie ataca desde fuera de la pantalla', G.fight.dmgTaken === 0 && !G.enemies.some(e => e.aware), 'daño ' + G.fight.dmgTaken);
  setup({ id: 't', name: 'huye', list: [{ type: 'urchin', x: 700 }] }, { px: 560 }); const u0 = G.enemies[0].x; run(1.2, () => { G.player.hp = G.player.hpMax; G.keys = {}; });
  check('Erizo: si te acercas, huye', G.enemies[0].x - u0 > 60, Math.round(G.enemies[0].x - u0) + ' px');
  setup({ id: 't', name: 'rueda', list: [{ type: 'urchin', x: 1420 }] }, { px: 1300 }); G.cam.x = 700; run(4, () => { G.player.hp = G.player.hpMax; G.keys = {}; });
  check('Erizo: acorralado, rueda a través de ti', !!(G._visits.urchin || {}).roll, Object.keys(G._visits.urchin || {}).join(' '));
  setup({ id: 't', name: 'cadena', list: [{ type: 'chargerAlpha', x: 1100 }] }, { px: 700 }); G.es.harmless = true; run(10, () => { G.keys = {}; });
  check('Embestidor alfa: encadena una segunda carga', !!(G._visits.chargerAlpha || {}).rechargeW, Object.keys(G._visits.chargerAlpha || {}).join(' '));
  // bosque
  setup({ id: 't', name: 'liana', list: [{ type: 'flytrap', x: 600 }] }, { px: 380 }); let grabbed = false; run(6, () => { G.player.hp = G.player.hpMax; G.keys = {}; if (G.player.dragBy) grabbed = true; });
  check('Atrapamoscas: la liana te atrapa, te arrastra y muerde', grabbed && !!G.fight.hits['Atrapamoscas · Mordisco (arrastrado)'], Object.keys(G.fight.hits).join(', '));
  setup({ id: 't', name: 'suelta', list: [{ type: 'flytrap', x: 600 }] }, { px: 380 }); let freed = false; run(6, () => { G.player.hp = G.player.hpMax; G.keys = {}; if (G.player.dragBy && !freed && G.player.dashCd <= 0) { sb.onKeyDown('ShiftLeft'); freed = true; } });
  check('Atrapamoscas: con el dash te sueltas', freed && !G.fight.hits['Atrapamoscas · Mordisco (arrastrado)'], Object.keys(G.fight.hits).join(', '));
  setup({ id: 't', name: 'trepa', list: [{ type: 'bear', x: 1000 }] }, { arena: 'vertical' }); const pb2 = A.PLATFORMS.find(pl => pl.x0 === 640); onPl(pb2); run(10, () => { G.player.hp = G.player.hpMax; G.keys = {}; if (G.player.y >= 340) onPl(pb2); });
  check('Oso: trepa a tu plataforma', !!(G._visits.bear || {}).climb, Object.keys(G._visits.bear || {}).join(' '));
  setup({ id: 't', name: 'esporada', list: [{ type: 'shroomAlpha', x: 600 }] }); G.es.frozenAI = true; const sm = G.enemies[0]; A.damage(sm, { dmg: sm.hpMax * 0.4 }); run(0.2);
  check('Seta madre: al perder vida suelta Saltasetas', A.alive().filter(e => e.type === 'shroom').length === 2);
  setup({ id: 't', name: 'latigo', list: [{ type: 'bramble', x: 280 }] }, { px: 200 }); run(3, () => { G.player.hp = G.player.hpMax; G.keys = {}; });
  check('Zarza: si te pegas (o la acorralas), latigazo que te aparta', !!G.fight.hits['Zarza · Latigazo'], Object.keys(G.fight.hits).join(', '));
  // volcán
  setup({ id: 't', name: 'guardia', list: [{ type: 'scorpion', x: 600 }] }); const sc = G.enemies[0]; sc.aware = true; sc.st = 'guard'; sc.stT = 0; const dg = A.damage(sc, { dmg: 20, kx: -50 }); run(0.5, () => { G.player.hp = G.player.hpMax; G.keys = {}; });
  check('Escorpión: pegarle en guardia no hace daño y contraataca', dg === 0 && !!(G._visits.scorpion || {}).counter, 'daño ' + dg);
  setup({ id: 't', name: 'huevo', list: [{ type: 'emberbird', x: 600, y: 200 }] }); A.damage(G.enemies[0], { dmg: 999 }); run(0.2);
  const egg = G.enemies.find(e => e.type === 'emberEgg' && !e.dead); run(4, () => { G.player.hp = G.player.hpMax; G.keys = {}; });
  check('Ave brasa: al morir deja un huevo que renace si no lo rompes', !!egg && A.alive().some(e => e.type === 'emberbird' && e.revived));
  setup({ id: 't', name: 'huevo2', list: [{ type: 'emberbird', x: 600, y: 200 }] }); A.damage(G.enemies[0], { dmg: 999 }); run(0.2); const egg2 = G.enemies.find(e => e.type === 'emberEgg' && !e.dead); A.damage(egg2, { dmg: 999 }); run(4);
  check('Ave brasa: si rompes el huevo, no vuelve (y el encuentro se despeja)', !A.alive().some(e => e.type === 'emberbird') && !!G.fight.tEnd);
  setup({ id: 't', name: 'pilar', list: [{ type: 'slaggolem', x: 700 }] }, { px: 300 }); let sawPillar = false; run(10, () => { G.player.hp = G.player.hpMax; G.keys = {}; if (A.alive().some(e => e.type === 'pillar')) sawPillar = true; });
  check('Golem: alza pilares y los revienta (metralla)', sawPillar && !!G.fight.hits['Golem de escoria · Reventar pilar'], Object.keys(G.fight.hits).join(', '));
  setup({ id: 't', name: 'rastro', list: [{ type: 'salamander', x: 650 }] }, { px: 300 }); let zones = 0; run(6, () => { G.player.hp = G.player.hpMax; G.keys = {}; zones = Math.max(zones, G.ents.filter(x => x.kind === 'foeZone').length); });
  check('Salamandra: la carrera deja rastro de lava', zones >= 5, zones + ' trozos de rastro');
}
console.log(bad ? `${bad} de ${n} mal` : `todo ok (${n})`);
if (bad) process.exitCode = 1;
