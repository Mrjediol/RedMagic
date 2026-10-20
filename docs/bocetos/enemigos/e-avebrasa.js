// e-avebrasa.js — Ave brasa (volcán): pájaro de fuego que hace pasadas rasantes soltando brasas sobre una franja marcada. Al morir
// cae como un HUEVO de brasa que renace si no lo rompes. Pregunta: ¿sales de la franja, y te acuerdas de rematar el huevo?
'use strict';
addFoe({
  id: 'emberbird', name: 'Ave brasa', short: 'Ave', world: 'volcan', rx: 18, ry: 14, fly: true, home: 'circle',
  role: 'Voladora de pasadas; renace si no la rematas',
  question: 'Salir de la franja marcada cuando hace la pasada (las brasas caen a lo largo de ella) y, al matarla, romper el huevo de brasa antes de que eclosione: si no, vuelve.',
  moves: [
    { name: 'Pasada', desc: 'Sube a un lado, se encoge (franja marcada en el suelo por donde va a pasar) y cruza en línea recta soltando brasas que caen. Tocarla también duele.' },
    { name: 'Renacer', desc: 'Al morir cae al suelo como un huevo de brasa que late cada vez más deprisa; si no lo rompes en unos segundos, el ave renace (con menos vida).' },
  ],
  combos: 'Con el Escorpión: esperar su guardia mientras caen brasas. Con la Serpiente: dos cosas que te pasan por encima.',
  art: 'Pájaro pequeño de plumas de fuego naranja y amarillo con el cuerpo de carbón negro agrietado; cola larga de llamas, ojos blancos. El huevo: bola de carbón con grietas de magma que laten. Partes: cuerpo, 2 alas, cola (llamas), huevo. ~0,9×0,7 u.',
  tuning: Object.assign(baseTuning(50, 0, 1.2, 0), {
    height: [150, 40, 260, 5, 'Altura sobre ti'], speed: [220, 40, 500, 10, 'Velocidad'],
    runWindup: [0.55, 0.1, 2, 0.05, 'Pasada: preparación (s)'], runSpeed: [520, 150, 1000, 10, 'Pasada: velocidad'], runDist: [700, 200, 1300, 10, 'Pasada: recorrido'],
    dropEvery: [0.11, 0.03, 0.5, 0.01, 'Pasada: brasa cada (s)'], emberDmg: [7, 0, 30, 1, 'Brasa: daño'], runDmg: [10, 0, 40, 1, 'Pasada: daño al tocarla'],
    eggHp: [20, 1, 200, 1, 'Huevo: vida'], reviveTime: [3, 0.5, 10, 0.1, 'Huevo: renace a los (s)'], reviveHp: [0.6, 0.1, 1, 0.05, 'Al renacer: fracción de vida'],
    cooldown: [1.6, 0.2, 5, 0.1, 'Pausa entre pasadas (s)'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player;
    switch (e.st) {
      case 'circle':
        e.cdT -= dt; faceTo(e, p.x);
        dart(e, dt, { min: 120, max: 300, height: t.height, speed: t.speed, burst: 2.2 });
        if (e.cdT <= 0 && canStart(e)) { begin(e, 'Pasada'); go(e, 'toStart'); e.rdir = Math.sign(p.x - e.x) || 1; e.rsx = clamp(p.x - e.rdir * t.runDist * 0.5, ARENA.x0 + 30, ARENA.x1 - 30); e.rsy = clamp(p.y - 40 - t.height, CEIL + 40, GROUND - 120); }
        break;
      case 'toStart':
        steer(e, e.rsx, e.rsy, t.speed * 2.2, dt, 6); e.face = e.rdir;
        if (Math.hypot(e.rsx - e.x, e.rsy - e.y) < 14 || e.stT > 2) {
          go(e, 'runW'); e.rx1 = clamp(e.x + e.rdir * t.runDist, ARENA.x0 + 20, ARENA.x1 - 20);
          warn({ e, kind: 'band', x0: e.x, x1: e.rx1, t: t.runWindup + 0.3 });
        }
        break;
      case 'runW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 8)); e.my = lerp(e.my, 0, Math.min(1, dt * 8)); e.face = e.rdir;
        if (windup(e, t.runWindup)) { go(e, 'run'); release(e); e.dropT = 0; e.hitP = false; sfx(k => SFXLIB.whoosh(k, 1000, 2600, 0.5, 0.18)); }
        break;
      case 'run':
        e.phase = 'active'; e.mx = e.rdir * t.runSpeed; e.my = 0; e.face = e.rdir; e.dropT -= dt;
        if (e.dropT <= 0) { e.dropT = t.dropEvery; foeShot({ e, atk: 'Brasa', x: e.x, y: e.y + 8, vx: e.rdir * 60, vy: 60, g: GRAV * 0.7, r: 4, style: 'ember', dmg: t.emberDmg, life: 2, pal: PAL.fire, landY: GROUND - 2, onLand: (x, y) => { if (t.emberZone) foeZone(e, x, GROUND, { halfW: 16, life: t.emberZone, dmg: 5, atk: 'Llamas', pal: PAL.fire }); } }); }
        if (!e.hitP && ellipseHitsPlayer(e, 4) && hitP(e, 'Pasada', t.runDmg, e.rdir * 300, -200)) e.hitP = true;
        if ((e.x - e.rx1) * e.rdir >= 0 || e.wall === e.rdir) { go(e, 'circle'); recover(e); e.cdT = t.cooldown; e.roamX = undefined; }
        break;
    }
  },
  onDeath(e) {
    if (e.revived) return;                                                       // renacida: ya no pone huevo
    const egg = spawnFoe('emberEgg', e.x, undefined, { aware: true, bird: e.type, birdHp: e.hpMax }); egg.y = Math.min(e.y, GROUND - egg.foot); egg.vy = 0;
  },
  draw(ctx, e, P) {
    const fl = Math.sin(G.t * 18 + e.id), run = e.st === 'run', tuck = e.st === 'runW' ? e.tell : 0;
    if (!P.mono) glow(ctx, 0, 0, 30, [255, 120, 40], 0.45);
    for (let i = 0; i < 4; i++) E_poly(ctx, P, [[-12, -2 + i * 2], [-28 - i * 6 + Math.sin(G.t * 12 + i) * 3, -6 + i * 5], [-12, 4]], i % 2 ? MAGMA.hot : MAGMA.glow);   // cola de llamas
    for (const s of [-1, 1]) { ctx.save(); ctx.translate(-2, -4); ctx.rotate(s * (run ? 0.3 : 0.9 + fl * 0.6) * (1 - tuck * 0.7)); E_poly(ctx, P, [[0, 0], [-6, -22 * s], [10, -16 * s]], s < 0 ? MAGMA.glow : MAGMA.hot); ctx.restore(); }
    E_ell(ctx, P, 0, 0, 14, 9, '#2a1a18'); E_line(ctx, P, [[-8, 0], [0, -2], [8, 1]], 1.5, MAGMA.glow);
    E_ell(ctx, P, 12, -4, 7, 6, '#2a1a18'); E_poly(ctx, P, [[18, -4], [26, -2], [18, 0]], MAGMA.hot);
    E_eye(ctx, P, e, 14, -5, 2);
  },
});
// el huevo: objeto en el suelo que late; si no lo rompes a tiempo, renace el ave (o el fénix) con parte de la vida
addFoe({
  id: 'emberEgg', name: 'Huevo de brasa', short: 'Huevo', world: 'volcan', rx: 14, ry: 14, home: 'wait', noFicha: true,
  role: '—', question: '—', moves: [], tuning: Object.assign(baseTuning(20, 0, 0, 0), {}),
  init(e) { const bt = G.tuning['foe_' + (e.bird || 'emberbird')]; e.hp = e.hpMax = (bt.eggHp || 20) * (G.es.hpMul || 1); e.life = bt.reviveTime || 3; },
  think(e, dt) {
    if (e.stT < e.life) return;
    const b = spawnFoe(e.bird || 'emberbird', e.x, e.y - 60, { aware: true, revived: true }), bt = G.tuning['foe_' + b.type];
    b.hp = Math.max(1, (e.birdHp || b.hpMax) * (bt.reviveHp || 0.6)); b.hpMax = e.birdHp || b.hpMax; b.cdT = 1; b.vy = -200;
    say(b, '¡RENACE!', '#ffd36b', true); foeBlast(e, e.x, e.y, 50, 6, 'Renacer', { pal: PAL.fire }); selfKill(e);
  },
  onDeath(e) { const x = e.x, y = e.y; addFx(0.6, (ctx, layer, t0) => sparks(ctx, G.t, t0, x, y, layer, { n: 10, pal: PAL.fire, sp: 200, seed: 6, len: 8, life: 0.4 })); },
  draw(ctx, e, P) {
    const k = clamp(e.stT / e.life), beat = 0.5 + 0.5 * Math.sin(G.t * (6 + 30 * k));
    if (!P.mono) glow(ctx, 0, 0, 26, [255, 110, 40], 0.3 + 0.5 * beat * k);
    E_ell(ctx, P, 0, 0, 12, 14, '#2a1a18');
    E_line(ctx, P, [[-6, -8], [-2, -2], [-5, 4], [0, 10]], 1.5, beat > 0.5 ? MAGMA.hot : MAGMA.glow); E_line(ctx, P, [[5, -9], [2, -1], [6, 6]], 1.5, beat > 0.5 ? MAGMA.hot : MAGMA.glow);
  },
});
