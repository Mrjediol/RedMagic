// tools/test-armas2.cjs — pruebas dirigidas de las armas 7+ (cada evolución hace lo que dice). Sin navegador.
// Uso: node docs/bocetos/habilidades/tools/test-armas2.cjs [id de arma]
const makeSim = require('./sim.cjs'), check = makeSim.checker();
const only = process.argv[2];
const sim = makeSim(), G = sim.G;
const E1 = [{ x: 620, kind: 'ground' }];
const near = (x, n = 3) => Array.from({ length: n }, (_, i) => ({ x: x + i * 60, kind: 'ground' }));
const T = id => G.tuning[id];

// ---------------------------------------------------------------- 7 · Reloj
if (!only || only === 'time') {
  // base: ralentiza a un caminante
  const walk = arch => { sim.setup({ slots: [{ w: 'time', arch }, { w: 'fan' }], enemies: [{ x: 600, kind: 'walker', x0: 300, x1: 900 }] }); const e = G.enemies[0]; sim.aim(600, 290).tap().run(0.5); const x0 = e.x; sim.run(1); return Math.abs(e.x - x0); };
  const free = (() => { sim.setup({ slots: [{ w: 'fan' }, { w: 'star' }], enemies: [{ x: 600, kind: 'walker', x0: 300, x1: 900 }] }); const e = G.enemies[0]; sim.run(0.5); const x0 = e.x; sim.run(1); return Math.abs(e.x - x0); })();
  const slow = walk(null);
  check('Reloj base: el caminante va lento dentro', slow < free * 0.5, `${slow.toFixed(1)} px vs ${free.toFixed(1)} px libre`);
  // base: golpe de reloj al cerrarse
  sim.setup({ slots: [{ w: 'time' }, { w: 'fan' }], enemies: E1 }); sim.aim(620, 290).tap().run(3.6);
  check('Reloj base: golpe de reloj al cerrarse', sim.dmg() >= T('time').closeDamage * 0.9, `daño ${sim.dmg()}`);
  // Estasis: guarda el daño de la otra arma y lo suelta aumentado
  sim.setup({ slots: [{ w: 'time', arch: 'stasis' }, { w: 'star' }], enemies: E1 }); const e = G.enemies[0];
  sim.aim(620, 290).tap().run(0.45); const held = !!e.stasis; sim.swap(1).aim(620, 290).tap().run(1.2);
  const hp1 = e.hp, bank = e.stasis ? e.stasis.bank : 0;
  check('Estasis: atrapa al enemigo', held);
  check('Estasis: el golpe de la otra arma se guarda (vida intacta)', hp1 === e.hpMax && bank > 50, `banco ${Math.round(bank)}, vida ${Math.round(hp1)}`);
  sim.run(2.5);
  check('Estasis: al cerrarse lo recibe todo aumentado', e.hpMax - e.hp >= bank * T('time').stasisMul * 0.99 || e.dead, `vida ${Math.round(e.hp)} (debía perder ≥ ${Math.round(bank * T('time').stasisMul)})`);
  // Estasis · Paradoja: si muere al soltarse, otra burbuja
  sim.setup({ slots: [{ w: 'time', arch: 'stasis', ups: { paradox: true, quake: true } }, { w: 'star' }], enemies: near(620, 2) });
  G.enemies[0].hp = 40; sim.aim(620, 290).tap().run(0.45).swap(1).aim(620, 290).tap().run(3.0);
  check('Estasis · Paradoja: la baja abre otra burbuja', sim.ents('bubble').length >= 1 && G.enemies[0].dead, `burbujas ${sim.ents('bubble').length}`);
  check('Estasis · Onda: el vecino recibe parte', G.enemies[1].hp < G.enemies[1].hpMax);
  // Eco: repite los golpes de la otra arma
  const fanRun = arch => { sim.setup({ slots: [{ w: 'time', arch }, { w: 'fan' }], enemies: near(560, 3), hpMul: 4 }); sim.aim(620, 290).tap().run(0.4).swap(1); for (let i = 0; i < 4; i++) sim.aim(600, 296).tap().run(0.55); sim.run(1); return sim.dmg(); };
  const dNo = fanRun(null), dEcho = fanRun('echo');
  check('Eco: los golpes del Abanico dentro se repiten', dEcho > dNo * 1.2, `${dEcho} con eco vs ${dNo} sin`);
  // Eco · Ancla: la burbuja sigue al player
  sim.setup({ slots: [{ w: 'time', arch: 'echo', ups: { anchor: true } }, { w: 'fan' }], enemies: E1 }); sim.aim(900, 290).tap(); G.player.vx = 0; G.player.x = 400; sim.run(0.5);
  const bb = sim.ents('bubble')[0];
  check('Eco · Ancla: la burbuja va con el player', bb && Math.abs(bb.x - G.player.x) < 20, bb ? `${Math.round(bb.x)} vs ${Math.round(G.player.x)}` : 'sin burbuja');
  // Aceleración: dentro, la otra arma recarga más deprisa
  const recharge = arch => { sim.setup({ slots: [{ w: 'time', arch }, { w: 'star' }], enemies: E1, px: 300 }); sim.aim(330, 290).tap().run(0.4).swap(1).aim(620, 290).tap().run(1.0); return G.slots[1].inst.ready(); };
  const rq = recharge('quick'), rn = recharge(null);
  check('Aceleración: la otra arma recarga más rápido dentro', rq > rn * 1.6, `${rq.toFixed(2)} vs ${rn.toFixed(2)}`);
  // Aceleración · Retroceso: las balas enemigas se vuelven tuyas
  sim.setup({ slots: [{ w: 'time', arch: 'quick', ups: { rewind: true } }, { w: 'fan' }], enemies: E1, px: 400, enemyAttacks: true }); let turned = 0;
  sim.aim(470, 300).tap(); sim.run(4, () => { for (const s of G.ents) if (s.kind === 'enemyShot' && s.friendly) turned++; });
  check('Aceleración · Retroceso: balas devueltas', turned > 0);
  // Aceleración · Bis: al cerrarse, la otra arma recargada
  sim.setup({ slots: [{ w: 'time', arch: 'quick', ups: { encore: true } }, { w: 'star' }], enemies: E1, px: 200 }); sim.aim(900, 200).tap().run(0.1).swap(1).aim(620, 290).tap().run(3.4);
  check('Aceleración · Bis: la otra arma recargada al cerrarse', G.slots[1].inst.ready() >= 0.99, G.slots[1].inst.ready().toFixed(2));
}
// ---------------------------------------------------------------- 8 · Ráfaga
if (!only || only === 'gust') {
  // base: empuja y el lanzado choca con el de detrás
  sim.setup({ slots: [{ w: 'gust' }, { w: 'fan' }], enemies: [{ x: 330, kind: 'ground' }, { x: 530, kind: 'ground' }], px: 200 });
  const [a, b] = G.enemies, ax0 = a.x; let crash = 0;
  sim.aim(330, 296).tap().run(0.6, () => { if (G.texts.some(x => x.s === 'CHOQUE')) crash = 1; });
  check('Ráfaga base: empuja', a.x - ax0 > 40 || crash, `${Math.round(a.x - ax0)} px`);
  check('Ráfaga base: el lanzado choca con el de detrás (daña a los dos)', crash && b.hp < b.hpMax, `vida del 2.º ${Math.round(b.hp)}`);
  // Tornado: levanta y los hace vulnerables
  sim.setup({ slots: [{ w: 'gust', arch: 'tornado', ups: { slam: true } }, { w: 'fan' }], enemies: near(380, 2), px: 200 });
  let maxUp = 0, vuln = 0; sim.aim(380, 296).tap().run(2.4, () => { for (const e of G.enemies) { maxUp = Math.max(maxUp, (GROUND_Y() - 44) - e.y); if (e.vulnT > G.t) vuln = 1; } });
  check('Tornado: levanta a los enemigos', maxUp > 40, `${Math.round(maxUp)} px`);
  check('Tornado: en el aire son vulnerables', vuln === 1);
  const before = sim.dmg(); sim.run(0.8);
  check('Tornado · Estampida: los estampa al deshacerse', sim.dmg() - before >= T('gust').slamDamage, `+${sim.dmg() - before}`);
  // Tornado · Absorción: toca fuego → tornado de fuego (los quema)
  sim.setup({ slots: [{ w: 'gust', arch: 'tornado', ups: { absorb: true } }, { w: 'fan' }], enemies: near(420, 1), px: 200 });
  G.ents.push(sim.sb.fireZone ? sim.sb.fireZone(330, 40, 3, 2, null) : null); sim.run(0.02);
  let burnt = 0; sim.aim(330, 296).tap().run(2, () => { if (G.enemies[0].burnT > 0) burnt = 1; });
  check('Tornado · Absorción: con fuego en el suelo quema', burnt === 1);
  // Corriente: tus esquirlas que la cruzan ganan daño
  const fanThrough = arch => { sim.setup({ slots: [{ w: 'gust', arch }, { w: 'fan' }], enemies: [{ x: 700, kind: 'ground' }], hpMul: 4, px: 200 }); sim.aim(700, 296).tap().run(0.1).swap(1); sim.aim(700, 296).tap().run(1.2); return sim.dmg(); };
  const dc = fanThrough('current'), dn = fanThrough(null);
  check('Corriente: las esquirlas del Abanico pegan más a favor', dc > dn * 1.15, `${dc} vs ${dn}`);
  // Corriente · Remolque: los arrastra al final
  sim.setup({ slots: [{ w: 'gust', arch: 'current', ups: { tow: true } }, { w: 'fan' }], enemies: [{ x: 330, kind: 'walker', x0: 250, x1: 420 }], px: 200 });
  sim.aim(700, 296).tap(); const wx0 = G.enemies[0].x; sim.run(2);
  check('Corriente · Remolque: arrastra al enemigo hacia el final', G.enemies[0].x - wx0 > 120, `${Math.round(G.enemies[0].x - wx0)} px`);
  // Martillo: más carga = más lejos
  const ramDist = hold => { sim.setup({ slots: [{ w: 'gust', arch: 'ram' }, { w: 'fan' }], enemies: [{ x: 330, kind: 'ground' }], px: 200 }); const e = G.enemies[0], x0 = e.x; sim.aim(330, 296).press().run(hold).release(); let mx = 0; sim.run(0.6, () => { mx = Math.max(mx, e.x - x0); }); return mx; };
  const r0 = ramDist(0.02), r1 = ramDist(0.9);
  check('Martillo: cargado lanza mucho más lejos', r1 > r0 * 1.6 && r1 > 150, `${Math.round(r1)} vs ${Math.round(r0)} px`);
  // Martillo · Bolos: tres en fila, el 3.º también recibe
  sim.setup({ slots: [{ w: 'gust', arch: 'ram', ups: { pins: true } }, { w: 'fan' }], enemies: near(320, 3), px: 200 });
  sim.aim(320, 296).press().run(0.9).release().run(1);
  check('Martillo · Bolos: el golpe llega al tercero', G.enemies[2].hp < G.enemies[2].hpMax, `vida ${G.enemies.map(e => Math.round(e.hp)).join('/')}`);
  // Martillo · Estampido
  sim.setup({ slots: [{ w: 'gust', arch: 'ram', ups: { crash: true } }, { w: 'fan' }], enemies: [{ x: 320, kind: 'ground' }, { x: 600, kind: 'ground' }], px: 200 });
  sim.aim(320, 296).press().run(0.9).release().run(1.6);
  check('Martillo · Estampido: estalla donde se para', G.enemies[1].hp < G.enemies[1].hpMax || sim.dmg() > T('gust').ramMax + 10, `daño ${sim.dmg()}`);
}
// ---------------------------------------------------------------- 9 · Látigo
if (!only || only === 'whip') {
  // base: la punta pega más que el cuerpo
  const lashAt = x => { sim.setup({ slots: [{ w: 'whip' }, { w: 'fan' }], enemies: [{ x, kind: 'ground' }], px: 200 }); sim.aim(x, 296).tap().run(0.4); return sim.dmg(); };
  const body = lashAt(270), tip = lashAt(200 + 20 + T('whip').reach * 0.9);
  check('Látigo base: la punta pega más que el cuerpo', tip > body * 1.6, `punta ${tip} vs cuerpo ${body}`);
  // Cadena: el daño de la otra arma a uno llega al otro
  sim.setup({ slots: [{ w: 'whip', arch: 'link' }, { w: 'star' }], enemies: [{ x: 360, kind: 'ground' }, { x: 560, kind: 'ground' }], px: 200 });
  sim.aim(360, 296).tap().run(0.4);
  const chained = sim.ents('chain').length; const hpB = G.enemies[1].hp;
  sim.swap(1).aim(360, 296).tap().run(1);
  check('Cadena: encadena con el más cercano', chained === 1);
  check('Cadena: la lanza del Golpe estelar a uno daña al otro', G.enemies[1].hp < hpB - 15, `${Math.round(hpB)} → ${Math.round(G.enemies[1].hp)}`);
  // Cadena · Ruptura al morir un eslabón
  sim.setup({ slots: [{ w: 'whip', arch: 'link', ups: { snap: true } }, { w: 'star' }], enemies: [{ x: 360, kind: 'ground' }, { x: 420, kind: 'ground' }, { x: 470, kind: 'ground' }], px: 200 });
  sim.aim(390, 296).tap().run(0.4); G.enemies[0].hp = 5; const hp3 = G.enemies[2].hp; sim.swap(1).aim(360, 296).tap().run(1.2);
  check('Cadena · Ruptura: estalla al romperse', G.enemies[2].hp < hp3 || G.enemies[1].hp < G.enemies[1].hpMax * 0.6, G.enemies.map(e => Math.round(e.hp)).join('/'));
  // Garfio: arrastra al enemigo hacia ti y queda vulnerable
  sim.setup({ slots: [{ w: 'whip', arch: 'hook' }, { w: 'fan' }], enemies: [{ x: 200 + 20 + T('whip').reach * 0.9, kind: 'ground' }], px: 200 });
  const e = G.enemies[0], d0 = e.x - G.player.x; sim.aim(e.x, 296).press().run(1).release();
  check('Garfio: lo trae hacia ti', e.x - G.player.x < d0 * 0.55, `${Math.round(d0)} → ${Math.round(e.x - G.player.x)} px`);
  check('Garfio: llega aturdido y vulnerable', e.vulnT > G.t);
  // Garfio · Lanzamiento: soltar a medio camino lo lanza contra otro
  sim.setup({ slots: [{ w: 'whip', arch: 'hook', ups: { fling: true } }, { w: 'fan' }], enemies: [{ x: 380, kind: 'ground' }, { x: 640, kind: 'ground' }], px: 200 });
  sim.aim(380, 296).press().run(0.3); sim.aim(640, 296).release().run(1.2);
  check('Garfio · Lanzamiento: el lanzado golpea al otro', G.enemies[1].hp < G.enemies[1].hpMax, `vida ${Math.round(G.enemies[1].hp)}`);
  // Remolino: mantener golpea varias veces alrededor
  sim.setup({ slots: [{ w: 'whip', arch: 'spin', ups: { accel: true } }, { w: 'fan' }], enemies: [{ x: 330, kind: 'ground' }, { x: 80, kind: 'ground' }], px: 200 });
  sim.aim(330, 296).press().run(1.6).release().run(0.4);
  check('Remolino: golpea a los dos lados', G.enemies.every(e => e.hp < e.hpMax), G.enemies.map(e => Math.round(e.hp)).join('/'));
}
// ---------------------------------------------------------------- 10 · Jabalina
if (!only || only === 'spear') {
  // base: cargada pega más; se queda clavada; pulsar otra vez la trae
  const throwAt = hold => { sim.setup({ slots: [{ w: 'spear' }, { w: 'fan' }], enemies: [{ x: 600, kind: 'ground' }], px: 200 }); sim.aim(600, 296).press().run(hold).release().run(0.8); return sim.dmg(); };
  const d0 = throwAt(0.02), d1 = throwAt(0.6);
  check('Jabalina: cargada pega mucho más', d1 > d0 * 2, `${d1} vs ${d0}`);
  const sp = G.slots[0].inst.spears()[0];
  check('Jabalina: se queda clavada', sp && sp.state === 'stuck', sp ? sp.state : 'no hay');
  check('Jabalina: mientras está fuera no se dispara', G.slots[0].inst.ready() === 0);
  sim.tap().run(0.5);
  check('Jabalina: pulsar otra vez la trae', G.slots[0].inst.spears().length === 0);
  // base: pasar por encima la recoge
  sim.setup({ slots: [{ w: 'spear' }, { w: 'fan' }], enemies: [{ x: 1500, kind: 'ground' }], px: 200 }); sim.aim(330, 330).press().run(0.1).release().run(0.4);
  const out = G.slots[0].inst.spears().length; G.player.x = G.slots[0].inst.spears()[0] ? G.slots[0].inst.spears()[0].x : 330; sim.run(0.5);
  check('Jabalina: pasar por encima la recoge', out === 1 && G.slots[0].inst.spears().length === 0);
  // Pararrayos: golpes de la otra arma cerca → rayos
  const rodRun = arch => { sim.setup({ slots: [{ w: 'spear', arch }, { w: 'fan' }], enemies: [{ x: 520, kind: 'ground' }, { x: 580, kind: 'ground' }], px: 200, hpMul: 4 }); sim.aim(470, 330).press().run(0.1).release().run(0.5).swap(1); for (let i = 0; i < 4; i++) sim.aim(540, 296).tap().run(0.5); return sim.dmg(); };
  const dr = rodRun('rod'), dn = rodRun(null);
  check('Pararrayos: los golpes del Abanico cerca atraen rayos', dr > dn * 1.3, `${dr} vs ${dn}`);
  // Pararrayos · Tormenta: rayos solos
  sim.setup({ slots: [{ w: 'spear', arch: 'rod', ups: { storm: true } }, { w: 'fan' }], enemies: [{ x: 520, kind: 'ground' }], px: 200 }); sim.aim(470, 330).press().run(0.1).release().run(3);
  check('Pararrayos · Tormenta: rayos sin hacer nada', sim.dmg() > T('spear').rodDamage * 2, `daño ${sim.dmg()}`);
  // Empalar: se lleva al enemigo y lo clava
  sim.setup({ slots: [{ w: 'spear', arch: 'impale' }, { w: 'fan' }], enemies: [{ x: 360, kind: 'ground' }], px: 200 }); const e = G.enemies[0], x0 = e.x;
  sim.aim(700, 280).press().run(0.6).release().run(0.8);
  check('Empalar: se lleva al enemigo', e.x - x0 > 80, `${Math.round(e.x - x0)} px`);
  check('Empalar: queda clavado y vulnerable', e.vulnT > G.t && e.stunT > 0);
  // Destello: te lleva a la jabalina
  sim.setup({ slots: [{ w: 'spear', arch: 'blink', ups: { phase: true } }, { w: 'fan' }], enemies: [{ x: 700, kind: 'ground' }], px: 200 });
  sim.aim(560, 330).press().run(0.1).release().run(0.6); const sx = G.slots[0].inst.spears()[0] ? G.slots[0].inst.spears()[0].x : 0; sim.tap();
  check('Destello: te teletransporta a la jabalina', Math.abs(G.player.x - sx) < 5 && sx > 400, `player ${Math.round(G.player.x)} jabalina ${Math.round(sx)}`);
  check('Destello · Fase: siguiente ataque potenciado', G.empower > 1);
}
check.done();
function GROUND_Y() { return 340; }
