// e-salamandra.js — Salamandra de magma (volcán): lagartija rápida que corre a través de ti dejando un RASTRO de lava que dura un
// rato, y trepa por las paredes para lanzarse desde ellas. Pregunta: ¿no te encierras entre rastros y vigilas las paredes?
'use strict';
addFoe({
  id: 'salamander', name: 'Salamandra de magma', short: 'Salam.', world: 'volcan', rx: 30, ry: 14, home: 'roam',
  role: 'Corredora que deja rastro (cambia el suelo)',
  question: 'El suelo cambia: cada carrera deja una franja de lava que dura unos segundos. No quedarte encerrado entre rastros, saltar la carrera, y mirar las paredes: trepa y se lanza desde ellas.',
  moves: [
    { name: 'Carrera', desc: 'Se agacha (franja marcada en el suelo por donde va a pasar) y cruza corriendo a través de ti; detrás deja un rastro de lava que quema al pisarlo. Al acabar derrapa (castigo).' },
    { name: 'Salto de pared', desc: 'A veces corre a la pared de la arena, trepa por ella y se lanza en arco hacia donde estás (marca dónde cae, también en tu plataforma).' },
  ],
  combos: 'Con el Golem: los pilares y los rastros te dejan poco suelo. Con Ave brasa: arriba brasas, abajo lava.',
  art: 'Salamandra o lagartija de piel negra como obsidiana con grietas de magma naranja que brillan (más al correr), cola larga que gotea lava, ojos amarillos. Muy baja y alargada; patas que se mueven rápido. Partes: cuerpo, cabeza, cola (2 segmentos), 4 patas. ~1,5×0,7 u.',
  tuning: Object.assign(baseTuning(80, 25, 1, 3), {
    speed: [150, 30, 400, 5, 'Velocidad'], keepMin: [160, 0, 500, 10, 'Le gusta estar a (mín.)'], keepMax: [300, 50, 700, 10, 'Le gusta estar a (máx.)'],
    dashWindup: [0.45, 0.1, 1.5, 0.05, 'Carrera: preparación (s)'], dashSpeed: [540, 150, 1000, 10, 'Carrera: velocidad'], dashDist: [460, 100, 1000, 10, 'Carrera: recorrido'],
    dashDmg: [10, 0, 40, 1, 'Carrera: daño'], trailLife: [2.4, 0, 8, 0.1, 'Rastro: dura (s)'], trailDmg: [6, 0, 30, 1, 'Rastro: daño al pisarlo'],
    wallChance: [0.35, 0, 1, 0.05, 'Probabilidad de salto de pared (si hay pared cerca)'], wallNear: [380, 50, 900, 10, 'Salto de pared: si la pared está a menos de'],
    climbSpeed: [260, 50, 600, 10, 'Trepar: velocidad'], wallH: [150, 40, 260, 5, 'Trepar: hasta esta altura'], pounceWindup: [0.4, 0.1, 1.5, 0.05, 'Salto de pared: preparación (s)'],
    pounceDmg: [12, 0, 40, 1, 'Salto de pared: daño'], skid: [0.5, 0, 2, 0.05, 'Derrape (s, castigo)'], cooldown: [1.4, 0.2, 5, 0.1, 'Pausa entre ataques (s)'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    switch (e.st) {
      case 'roam':
        faceTo(e, p.x); e.cdT -= dt;
        roam(e, dt, { min: t.keepMin, max: t.keepMax, speed: t.speed, burst: 2.2, hop: 0.1 });
        if (e.cdT > 0 || !canStart(e)) break;
        { const wall = e.x - ARENA.x0 < ARENA.x1 - e.x ? -1 : 1, wd = wall < 0 ? e.x - ARENA.x0 : ARENA.x1 - e.x;
          if (wd < t.wallNear && Math.random() < t.wallChance) { begin(e, 'Salto de pared'); go(e, 'toWall'); e.wall0 = wall; break; } }
        begin(e, 'Carrera'); go(e, 'dashW'); e.ddir = Math.sign(dx) || e.face;
        { const x1 = clamp(e.x + e.ddir * t.dashDist, ARENA.x0 + e.rx, ARENA.x1 - e.rx); warn({ e, kind: 'band', x0: e.x, x1, t: t.dashWindup }); }
        break;
      case 'dashW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 12)); e.face = e.ddir;
        if (windup(e, t.dashWindup)) { go(e, 'dash'); release(e); e.run = 0; e.hitP = false; e.lastTrail = e.x; sfx(k => SFXLIB.whoosh(k, 1400, 500, 0.35, 0.18)); }
        break;
      case 'dash':
        e.phase = 'active'; e.mx = e.ddir * t.dashSpeed; e.run += t.dashSpeed * dt;
        if (!e.hitP && ellipseHitsPlayer(e, 4) && hitP(e, 'Carrera', t.dashDmg, e.ddir * 360, -300)) e.hitP = true;
        if (Math.abs(e.x - e.lastTrail) >= 34 && t.trailLife > 0) { foeZone(e, e.x, GROUND, { halfW: 20, life: t.trailLife, dmg: t.trailDmg, atk: 'Rastro de lava', pal: PAL.fire }); e.lastTrail = e.x; }
        if (e.run > t.dashDist || e.wall === e.ddir) { go(e, 'skid'); recover(e); }
        break;
      case 'skid':
        e.phase = 'recover'; e.mx = lerp(e.mx, 0, Math.min(1, dt * 6));
        if (e.stT > t.skid) { go(e, 'roam'); e.cdT = t.cooldown; }
        break;
      // ---- trepar y lanzarse desde la pared
      case 'toWall':
        e.face = e.wall0; walkDir(e, e.wall0, t.speed * 2.4, dt, 10);
        if (e.wall === e.wall0) { go(e, 'climb'); e.fly = true; e.mx = 0; e.vy = 0; }
        if (e.stT > 3) { go(e, 'roam'); e.busy = false; }
        break;
      case 'climb':
        e.mx = 0; e.my = -t.climbSpeed; e.face = e.wall0;
        if (e.y <= GROUND - t.wallH) { e.my = 0; go(e, 'pounceW'); }
        break;
      case 'pounceW':
        e.mx = 0; e.my = 0;
        if (!windup(e, t.pounceWindup)) break;
        { const tx = clamp(p.x + p.vx * 0.2, ARENA.x0 + e.rx, ARENA.x1 - e.rx), feet = surfaceUnder(tx, p.y);
          e.fly = false; const T = leapTo(e, tx, feet, 30); go(e, 'pounce'); release(e); e.hitP = false; e.face = -e.wall0;
          warn({ e, kind: 'circle', x: tx, y: feet + 2, r: e.rx * 1.1, t: T }); sfx(k => SFXLIB.whoosh(k, 1800, 600, 0.3, 0.16)); }
        break;
      case 'pounce':
        e.phase = 'active';
        if (!e.hitP && ellipseHitsPlayer(e, 4) && hitP(e, 'Salto de pared', t.pounceDmg, Math.sign(e.mx || 1) * 380, -260)) e.hitP = true;
        if (e.onGround && e.stT > 0.08) { e.mx = 0; go(e, 'skid'); recover(e); dustPuff(e.x, GROUND); }
        break;
    }
  },
  platforms: true,
  onInterrupt(e) { e.fly = false; go(e, 'roam'); },
  draw(ctx, e, P) {
    const onWall = e.st === 'climb' || e.st === 'pounceW', run = e.st === 'dash' ? 1 : 0, low = e.st === 'dashW' ? e.tell : 0;
    ctx.save();
    if (onWall) { ctx.rotate(-Math.PI / 2); ctx.translate(0, 0); }                 // en la pared: de cabeza hacia arriba
    ctx.translate(0, low * 4);
    for (let i = 0; i < 4; i++) { const lx = [-14, -6, 8, 16][i], sw = Math.sin(e.wp * (1.6 + run) + i * Math.PI) * (Math.abs(e.mx) > 10 || onWall ? 6 : 0); E_line(ctx, P, [[lx, 4], [lx + sw, e.ry]], 3.5, MAGMA.obs); }
    E_line(ctx, P, [[-26, 0], [-40, -4 + Math.sin(G.t * 8) * 3], [-54, 2]], 6, MAGMA.obs);
    E_ell(ctx, P, 0, 0, 28, 9, MAGMA.obs);
    E_line(ctx, P, [[-20, -2], [-6, -5], [6, -2], [18, -4]], 2, run ? MAGMA.hot : MAGMA.glow);  // grietas de magma (más vivas al correr)
    E_line(ctx, P, [[-12, 3], [2, 2], [12, 4]], 1.5, MAGMA.glow);
    E_poly(ctx, P, [[22, -6], [40, -2], [38, 4], [22, 6]], MAGMA.obsL);
    E_eye(ctx, P, e, 30, -2, 2.2);
    if (!P.mono && run) glow(ctx, 0, 0, 40, [255, 110, 40], 0.35);
    ctx.restore();
  },
});
