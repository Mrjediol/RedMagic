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
// ---------------------------------------------------------------- 11 · Enjambre
if (!only || only === 'swarm') {
  sim.setup({ slots: [{ w: 'swarm' }, { w: 'fan' }], enemies: near(600, 2), px: 200 }); sim.aim(630, 296).tap().run(5);
  check('Enjambre base: pican a los dos y vuelven', G.enemies.every(e => e.hp < e.hpMax) && G.slots[0].inst.flies().length === 0, G.enemies.map(e => Math.round(e.hp)).join('/'));
  // Colmena: los golpes de la otra arma traen luciérnagas
  const hiveRun = arch => { sim.setup({ slots: [{ w: 'swarm', arch }, { w: 'star' }], enemies: [{ x: 600, kind: 'ground' }], px: 200, hpMul: 4 }); sim.run(0.5).swap(1).aim(600, 296).tap().run(3); return sim.dmg(); };
  const dh = hiveRun('hive'), dn = hiveRun(null);
  check('Colmena: al golpear con la otra arma acuden solas', dh > dn + 8, `${dh} vs ${dn}`);
  // Colmena · Néctar: la otra arma recarga antes
  sim.setup({ slots: [{ w: 'swarm', arch: 'hive', ups: { nectar: true } }, { w: 'star' }], enemies: [{ x: 600, kind: 'ground' }], px: 200, hpMul: 4 }); sim.run(0.5).swap(1).aim(600, 296).tap().run(2.3);
  const rN = G.slots[1].inst.ready();
  sim.setup({ slots: [{ w: 'swarm', arch: 'hive' }, { w: 'star' }], enemies: [{ x: 600, kind: 'ground' }], px: 200, hpMul: 4 }); sim.run(0.5).swap(1).aim(600, 296).tap().run(2.3);
  check('Colmena · Néctar: la otra arma recarga antes', rN > G.slots[1].inst.ready(), `${rN.toFixed(2)} vs ${G.slots[1].inst.ready().toFixed(2)}`);
  // Plaga: se meten dentro y saltan al morir
  sim.setup({ slots: [{ w: 'swarm', arch: 'plague' }, { w: 'fan' }], enemies: near(600, 2), px: 200 }); G.enemies[0].hp = 25;
  let inf = 0; sim.aim(600, 296).tap().run(4, () => { inf = Math.max(inf, G.enemies[1].infest || 0); });
  check('Plaga: infesta y, al morir el huésped, salta al vecino', G.enemies[0].dead || inf > 0, `infestación del vecino ${inf}`);
  // Faroles: críticos a los iluminados
  sim.setup({ slots: [{ w: 'swarm', arch: 'lantern' }, { w: 'fan' }], enemies: [{ x: 600, kind: 'ground' }], px: 200, hpMul: 20 }); sim.aim(600, 270).tap().run(0.3);
  check('Faroles: el enemigo queda iluminado', G.enemies[0].litT > G.t);
  sim.swap(1); let crit = 0; for (let i = 0; i < 6; i++) sim.aim(600, 296).tap().run(0.55, () => { if (G.texts.some(x => x.s === 'CRÍTICO')) crit = 1; });
  check('Faroles: golpes críticos de la otra arma', crit === 1);
}
// ---------------------------------------------------------------- 12 · Tótem
if (!only || only === 'totem') {
  sim.setup({ slots: [{ w: 'totem' }, { w: 'fan' }], enemies: [{ x: 600, kind: 'ground' }], px: 200 }); sim.aim(420, 300).tap().run(0.1).swap(1).run(4);
  check('Tótem base: dispara solo con la otra arma en la mano', sim.dmg() >= T('totem').boltDamage * 3, `daño ${sim.dmg()}`);
  // plataforma: se planta encima
  sim.setup({ slots: [{ w: 'totem' }, { w: 'fan' }], enemies: [{ x: 1500, kind: 'ground' }], px: 500 }); sim.aim(590, 200).tap().run(0.5);
  const tm = G.slots[0].inst.totems()[0];
  check('Tótem: se planta sobre la plataforma de debajo', tm && Math.abs(tm.y - (252 - 40)) < 2, tm ? `y ${Math.round(tm.y)}` : 'sin tótem');
  // Resonador: copia los golpes de la otra arma
  const resRun = arch => { sim.setup({ slots: [{ w: 'totem', arch }, { w: 'star' }], enemies: [{ x: 700, kind: 'ground' }], px: 200, hpMul: 4 }); sim.aim(450, 300).tap().run(0.5).swap(1).aim(700, 296).tap().run(1.5); return G.enemies[0].hpMax - G.enemies[0].hp; };
  const rr = resRun('reson'), rn = resRun(null);
  check('Resonador: copia la lanza del Golpe estelar', rr > rn + 20, `${Math.round(rr)} vs ${Math.round(rn)}`);
  // Centinela: más daño que el base
  const sentRun = (arch, ups) => { sim.setup({ slots: [{ w: 'totem', arch, ups }, { w: 'fan' }], enemies: [{ x: 640, kind: 'ground' }], px: 200, hpMul: 10 }); sim.aim(450, 300).tap().run(0.1).swap(1).run(4); return sim.dmg(); };
  const ds = sentRun('sentry', { triple: true }), db = sentRun(null, {});
  check('Centinela · Triple: mucho más daño que el base', ds > db * 2, `${ds} vs ${db}`);
  // Señuelo: atrae y las balas van a por él
  sim.setup({ slots: [{ w: 'totem', arch: 'decoy' }, { w: 'fan' }], enemies: [{ x: 700, kind: 'walker', x0: 600, x1: 800 }, { x: 760, kind: 'flyer', y: 150 }], px: 200, enemyAttacks: true });
  const hp0 = G.player.hp; sim.aim(520, 300).tap(); const x0 = G.enemies[0].x; sim.run(5);
  check('Señuelo: los atrae', G.enemies[0].x < x0 - 40, `${Math.round(x0)} → ${Math.round(G.enemies[0].x)}`);
  check('Señuelo: las balas van a por él y no a ti', G.player.hp === hp0, `vida ${G.player.hp}`);
}
// ---------------------------------------------------------------- 13 · Espejo
if (!only || only === 'mirror') {
  // base: las esquirlas del Abanico que cruzan el espejo van a por un enemigo fuera de la línea
  const flyer = [{ x: 560, kind: 'flyer', y: 120 }];
  const fanVia = arch => { sim.setup({ slots: [{ w: 'mirror', arch }, { w: 'fan' }], enemies: flyer, px: 200, hpMul: 4 }); if (arch !== 'none') sim.aim(400, 294).tap(); sim.run(0.2).swap(1); for (let i = 0; i < 3; i++) sim.aim(400, 294).tap().run(0.6); return G.enemies[0].hpMax - G.enemies[0].hp; };
  const dm = fanVia(null), d0 = fanVia('none');
  check('Espejo: desvía las esquirlas al enemigo de fuera de la línea', dm > d0 + 20, `${Math.round(dm)} vs ${Math.round(d0)}`);
  // el Rayo rebota en el espejo
  const rayVia = arch => { sim.setup({ slots: [{ w: 'mirror', arch }, { w: 'ray' }], enemies: flyer, px: 200, hpMul: 4 }); if (arch !== 'none') sim.aim(400, 294).tap(); sim.run(0.3).swap(1); sim.aim(400, 294).press().run(1.5).release(); return G.enemies[0].hpMax - G.enemies[0].hp; };
  const rm = rayVia(null), r0 = rayVia('none');
  check('Espejo: el Rayo rebota hacia el enemigo', rm > r0 + 30, `${Math.round(rm)} vs ${Math.round(r0)}`);
  // Lupa: lo que la atraviesa pega más (enemigo en línea)
  const lensVia = arch => { sim.setup({ slots: [{ w: 'mirror', arch }, { w: 'ray' }], enemies: [{ x: 600, kind: 'ground' }], px: 200, hpMul: 6 }); if (arch !== 'none') sim.aim(400, 296).tap(); sim.run(0.3).swap(1); sim.aim(600, 296).press().run(1.5).release(); return G.enemies[0].hpMax - G.enemies[0].hp; };
  const lm = lensVia('lens'), l0 = lensVia('none');
  check('Lupa: el Rayo que la cruza pega mucho más', lm > l0 * 1.5, `${Math.round(lm)} vs ${Math.round(l0)}`);
  // Portal: un caminante que entra por A sale por B
  sim.setup({ slots: [{ w: 'mirror', arch: 'portal' }, { w: 'fan' }], enemies: [{ x: 520, kind: 'walker', x0: 420, x1: 560 }], px: 200 });
  sim.aim(470, 300).tap().run(1.3).aim(560, 140).tap(); let tp = 0; sim.run(12, () => { if (G.enemies[0].y < 200) tp = 1; });
  check('Portal: el enemigo que entra por A sale por B (en el aire)', tp === 1);
  // balas enemigas vuelven como tuyas
  sim.setup({ slots: [{ w: 'mirror' }, { w: 'fan' }], enemies: [{ x: 520, kind: 'ground' }], px: 200, enemyAttacks: true }); let turned = 0;
  sim.aim(330, 280).tap(); sim.run(4.5, () => { if (G.ents.some(s => s.kind === 'enemyShot' && s.friendly)) turned = 1; });
  check('Espejo: devuelve las balas enemigas', turned === 1 && G.player.hp === G.player.hpMax, `vida ${G.player.hp}`);
}
// ---------------------------------------------------------------- 14 · Acumulador
if (!only || only === 'volt') {
  // se carga con la otra arma y no consigo mismo
  sim.setup({ slots: [{ w: 'volt' }, { w: 'fan' }], enemies: near(420, 3), px: 200, hpMul: 6 });
  sim.tap(); const c0 = G.slots[0].inst.charge(); sim.swap(1); for (let i = 0; i < 6; i++) sim.aim(470, 296).tap().run(0.55);
  const c1 = G.slots[0].inst.charge();
  check('Acumulador: sin carga no descarga', c0 === 0 && sim.dmg() > 0);
  check('Acumulador: se carga con los golpes del Abanico', c1 > 30, `carga ${Math.round(c1)}`);
  const before = sim.dmg(); G.player.x = G.enemies[1].x - 40; sim.swap(0).tap().run(0.3);
  check('Acumulador: la descarga pega a todos alrededor según la carga', sim.dmg() - before > 3 * (10 + c1 * 0.9) * 0.9, `+${sim.dmg() - before}`);
  // Sobrecarga: descarga sola con la otra arma en la mano
  sim.setup({ slots: [{ w: 'volt', arch: 'overload' }, { w: 'fan' }], enemies: near(420, 3), px: 200, hpMul: 10 }); sim.swap(1); let auto = 0;
  for (let i = 0; i < 14; i++) sim.aim(470, 296).tap().run(0.55, () => { if (G.texts.some(x => x.s === 'SOBRECARGA')) auto = 1; });
  check('Sobrecarga: descarga sola al llenarse', auto === 1);
  // Arco: salta entre enemigos
  sim.setup({ slots: [{ w: 'volt', arch: 'arc' }, { w: 'fan' }], enemies: [{ x: 500, kind: 'ground' }, { x: 640, kind: 'ground' }, { x: 780, kind: 'ground' }, { x: 920, kind: 'ground' }], px: 200, hpMul: 6 });
  G.slots[0].inst.onAnyHit(G.enemies[0], { slot: G.slots[1] }, 600); sim.aim(500, 296).tap().run(0.3);
  check('Arco: salta a los cuatro con carga llena', G.enemies.every(e => e.hp < e.hpMax), G.enemies.map(e => Math.round(e.hp)).join('/'));
  // Celdas: guarda varias y cada clic gasta una en un rayo
  sim.setup({ slots: [{ w: 'volt', arch: 'cells' }, { w: 'fan' }], enemies: [{ x: 600, kind: 'ground' }], px: 200, hpMul: 6 });
  G.slots[0].inst.onAnyHit(G.enemies[0], { slot: G.slots[1] }, 5000); const full = G.slots[0].inst.charge();
  sim.aim(600, 296).tap().run(0.5);
  check('Celdas: 3 celdas y cada clic gasta una (rayo del cielo)', full === 300 && G.slots[0].inst.charge() === 200 && sim.dmg() >= 70, `carga ${full} → ${G.slots[0].inst.charge()}, daño ${sim.dmg()}`);
}
// ---------------------------------------------------------------- 15 · Pacto de sangre
if (!only || only === 'blood') {
  sim.setup({ slots: [{ w: 'blood' }, { w: 'fan' }], enemies: [{ x: 500, kind: 'ground' }], px: 200 });
  sim.aim(500, 296).tap().run(0.6);
  check('Pacto: el orbe cuesta vida y pega fuerte', G.player.hp === G.player.hpMax - T('blood').cost && sim.dmg() >= T('blood').orbDamage, `vida ${G.player.hp}, daño ${sim.dmg()}`);
  G.enemies[0].hp = 10; sim.run(0.7).tap().run(0.6);
  check('Pacto: la baja devuelve vida', G.player.hp > G.player.hpMax - 2 * T('blood').cost, `vida ${G.player.hp}`);
  // Hemorragia: la otra arma revienta la sangre y cura
  sim.setup({ slots: [{ w: 'blood', arch: 'bleed' }, { w: 'star' }], enemies: [{ x: 500, kind: 'ground' }], px: 200, hpMul: 6 });
  for (let i = 0; i < 3; i++) sim.aim(500, 296).tap().run(0.6);
  const n = G.enemies[0].bleedN, hp0 = G.player.hp; let burst = 0; sim.swap(1).aim(500, 296).tap().run(1, () => { if (G.texts.some(x => x.s === 'DERRAME')) burst = 1; });
  check('Hemorragia: acumula sangrado', n >= 3, `cargas ${n}`);
  check('Hemorragia: el Golpe estelar revienta la sangre y cura', burst === 1 && G.player.hp > hp0, `vida ${hp0} → ${G.player.hp}`);
  // Vampiro: el orbe vuelve y cura
  sim.setup({ slots: [{ w: 'blood', arch: 'vamp' }, { w: 'fan' }], enemies: [{ x: 500, kind: 'ground' }], px: 200, hpMul: 4 }); G.player.hp = 50;
  sim.aim(500, 296).tap().run(2.6);
  check('Vampiro: el orbe vuelve y cura más de lo que costó', G.player.hp > 50, `vida ${G.player.hp}`);
  // Vampiro · Drenar
  sim.setup({ slots: [{ w: 'blood', arch: 'vamp', ups: { drain: true } }, { w: 'fan' }], enemies: [{ x: 420, kind: 'ground' }], px: 200, hpMul: 4 }); G.player.hp = 50;
  sim.aim(420, 296).press().run(2).release();
  check('Vampiro · Drenar: chupa vida', G.player.hp > 55 && sim.dmg() > 40, `vida ${G.player.hp}, daño ${sim.dmg()}`);
  // Mártir: con poca vida, todas tus armas pegan más
  const fanAt = hp => { sim.setup({ slots: [{ w: 'blood', arch: 'martyr' }, { w: 'fan' }], enemies: [{ x: 420, kind: 'ground' }], px: 200, hpMul: 6 }); G.player.hp = hp; sim.swap(1).aim(420, 296).tap().run(0.6); return sim.dmg(); };
  const lo = fanAt(10), hi = fanAt(100);
  check('Mártir: con 10 de vida el Abanico pega mucho más', lo > hi * 1.4, `${lo} vs ${hi}`);
  // Mártir · Último aliento
  sim.setup({ slots: [{ w: 'blood', arch: 'martyr', ups: { breath: true } }, { w: 'fan' }], enemies: near(300, 2), px: 200 }); G.player.hp = 5; sim.sb.hurtPlayer(20);
  check('Mártir · Último aliento: te deja a 1 y suelta la nova', G.player.hp === 1 && sim.dmg() > 50, `vida ${G.player.hp}, daño ${sim.dmg()}`);
}
check.done();
function GROUND_Y() { return 340; }
