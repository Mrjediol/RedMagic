// e-colmena.js — Colmena (invocadora) y Zángano (su cría, también suelto). Pregunta: ¿vas a por la fuente o limpias lo que
// suelta? Si matas la colmena, sus zánganos se enfurecen un momento y mueren.
'use strict';
const HIVE_PAL = { core: '#fffbe8', light: '#ffe2a0', mid: '#ffb347', deep: '#d0761a', dark: '#5a3006', g: [255, 180, 70], g1: [255, 160, 40], g2: [255, 225, 150] };
addFoe({
  id: 'hive', name: 'Colmena', short: 'Colm.', rx: 32, ry: 34, fly: true, home: 'float',
  role: 'Invocadora voladora (pare zánganos)',
  question: 'Prioridad: ir a por la colmena (lenta, alta, resistente) mientras los zánganos te pican, o limpiar zánganos con armas de área.',
  moves: [
    { name: 'Parir', desc: 'Cada pocos segundos se hincha (se puede interrumpir con daño) y suelta 2 zánganos, hasta un máximo vivos a la vez.' },
    { name: 'Enjambre', desc: 'Si le haces mucho daño seguido, suelta 3 de golpe y huye al otro lado.' },
    { name: 'Furia', desc: 'Al morir, sus zánganos se enfurecen (más rápidos) unos segundos y luego caen muertos.' },
  ],
  combos: 'Detrás de un Caparazón. Con Fatuos: muchos objetivos pequeños, las armas de área brillan.',
  art: 'Nido colgante y abultado con agujeros que brillan, dos alitas que zumban. Panal de ámbar, nido de avispas de corteza.',
  tuning: Object.assign(baseTuning(180, 50, 0.4, 0), {
    height: [170, 60, 280, 5, 'Altura sobre el suelo'], followDist: [260, 0, 700, 10, 'Se queda a esta distancia'], speed: [55, 0, 300, 5, 'Velocidad'],
    spawnEvery: [3.6, 0.5, 15, 0.1, 'Parir: cada (s)'], spawnN: [2, 1, 6, 1, 'Parir: zánganos'], maxDrones: [5, 1, 15, 1, 'Zánganos vivos como mucho'],
    birthWindup: [0.6, 0.1, 2, 0.05, 'Parir: preparación (s)'], panicHurt: [45, 0, 300, 5, 'Enjambre: si recibe este daño en 2 s'],
    panicN: [3, 0, 8, 1, 'Enjambre: zánganos'], panicCd: [8, 1, 30, 0.5, 'Enjambre: pausa (s)'], frenzyTime: [3, 0, 10, 0.5, 'Furia: duración (s)'],
  }),
  init(e) { e.spawnT = 1.5; e.panicCdT = 0; e.fleeT = 0; e.side = 1; },
  think(e, dt) {
    const t = FT(e), p = G.player;
    e.spawnT += dt; e.panicCdT -= dt; e.fleeT -= dt;
    switch (e.st) {
      case 'float': {
        faceTo(e, p.x);
        if (e.fleeT <= 0 && Math.abs(e.x - p.x) < t.followDist * 0.5) e.side = Math.sign(e.x - p.x) || e.side;
        let tx = p.x + e.side * t.followDist; if (tx < ARENA.x0 + 60 || tx > ARENA.x1 - 60) { e.side = -e.side; tx = p.x + e.side * t.followDist; }
        steer(e, tx, GROUND - t.height + Math.sin(G.t * 1.1 + e.id) * 14, t.speed * (e.fleeT > 0 ? 3 : 1), dt, 2);
        if (e.spawnT > t.spawnEvery && myDrones(e) < t.maxDrones) { go(e, 'birthW'); e.spawnT = 0; }
        break;
      }
      case 'birthW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 4)); e.my = lerp(e.my, 0, Math.min(1, dt * 4));
        if (!windup(e, t.birthWindup)) break;
        release(e); spawnDrones(e, Math.min(t.spawnN, t.maxDrones - myDrones(e))); go(e, 'float');
        break;
    }
  },
  onHurt(e) {
    const t = FT(e);
    if (e.panicCdT > 0 || t.panicN <= 0 || recentDmg(e, 2) < t.panicHurt) return;
    e.panicCdT = t.panicCd; e.fleeT = 1.5; e.side = -(Math.sign(e.x - G.player.x) || 1);    // huye al otro lado de ti
    say(e, '¡ENJAMBRE!', '#ffd27a', true); spawnDrones(e, t.panicN);
  },
  onDeath(e) {
    const t = FT(e);
    for (const d of foes()) if (d.mother === e && !d.dead) { d.frenzyUntil = G.t + t.frenzyTime; say(d, '¡FURIA!', '#ff9a6a'); }
  },
  draw(ctx, e, P) {
    const s = 1 + (e.st === 'birthW' ? e.tell * 0.14 : 0), fl = Math.sin(G.t * 40 + e.id);
    ctx.save(); ctx.globalAlpha *= P.mono ? 1 : 0.5;
    for (const sd of [-1, 1]) E_ell(ctx, P, sd * 16, -30, 14, 5 + fl * 2, '#efe6d0', sd * 0.4);
    ctx.restore();
    E_line(ctx, P, [[0, -34], [0, -26]], 4, '#4a3018');
    E_ell(ctx, P, 0, 0, 30 * s, 33 * s, '#5a3c1a');
    for (let i = 0; i < 4; i++) E_ell(ctx, P, 0, (-22 + i * 14) * s, (24 - Math.abs(i - 1.5) * 5) * s, 7 * s, i % 2 ? '#8a6a3a' : '#9c7a44');
    for (const [x, y] of [[-12, -10], [10, -4], [-6, 10], [14, 14]]) { if (!P.mono) glow(ctx, x * s, y * s, 9, HIVE_PAL.g, 0.35 + 0.4 * e.tell); E_ell(ctx, P, x * s, y * s, 4, 3.4, '#2a1606'); E_ell(ctx, P, x * s, y * s, 2, 1.6, HIVE_PAL.mid); }
    E_ell(ctx, P, 0, 30 * s, 9, 5, '#2a1606');
    E_eye(ctx, P, e, 16, -14, 3);
  },
});
const myDrones = e => foes().filter(d => d.mother === e && !d.dead).length;
function spawnDrones(e, n) {
  for (let i = 0; i < n; i++) {
    const d = spawnFoe('drone', e.x + (i - (n - 1) / 2) * 14, e.y + e.ry, { mother: e });
    d.vy = 140; d.vx = (i - (n - 1) / 2) * 120; d.cdT = 0.8 + Math.random();
  }
  sfx(k => { const t0 = nowT(); vNoise(t0, 0.3 * k, 0.12, 'bandpass', 600, 900, 3, { att: 0.02 }); });
}

addFoe({
  id: 'drone', name: 'Zángano', short: 'Záng.', rx: 10, ry: 9, fly: true, home: 'orbit',
  role: 'Cría de la Colmena: frágil, en grupo',
  question: 'Muchos objetivos pequeños que se lanzan en línea: aparta o barre (un golpe los mata) sin perder de vista lo grande.',
  moves: [
    { name: 'Rondar', desc: 'Vuelan en círculo alrededor de ti, separándose entre ellos.' },
    { name: 'Picado', desc: 'Se para, tiembla (línea corta del recorrido) y se lanza en línea recta. Si falla, vuelve a rondar. Como todos los ataques, cuentan para el máximo de atacantes a la vez.' },
  ],
  combos: 'Con su Colmena (la fuente). Sueltos, en grupo de 4-6, como relleno de una oleada.',
  art: 'Insecto pequeño y redondo con rayas de ámbar, aguijón y alas borrosas. Avispa de corteza, mosca de luz.',
  tuning: Object.assign(baseTuning(12, 0, 2, 0), {
    orbitR: [80, 30, 250, 5, 'Rondar: radio'], speed: [190, 40, 500, 5, 'Velocidad'],
    diveWindup: [0.4, 0.1, 1.5, 0.05, 'Picado: preparación (s)'], diveSpeed: [540, 100, 1200, 10, 'Picado: velocidad'],
    diveDist: [230, 50, 600, 10, 'Picado: recorrido'], dmg: [6, 0, 40, 1, 'Picado: daño'], cooldown: [1.4, 0.2, 6, 0.1, 'Pausa entre picados (s)'],
  }),
  init(e) { e.ang = Math.random() * TAU; e.spin = Math.random() < 0.5 ? -1 : 1; },
  think(e, dt) {
    const t = FT(e), c = pCenter(), fr = e.frenzyUntil ? 1.6 : 1;
    if (e.frenzyUntil && G.t > e.frenzyUntil) { hitFx('die', e.x, e.y, e); selfKill(e); return; }
    switch (e.st) {
      case 'orbit': {
        e.cdT -= dt * fr; e.ang += dt * (1.5 + (e.id % 3) * 0.3) * e.spin;
        let tx = c.x + Math.cos(e.ang) * t.orbitR * 1.3, ty = c.y - 30 + Math.sin(e.ang) * t.orbitR * 0.6;
        for (const o of foes()) if (o !== e && o.type === 'drone' && !o.dead) { const dx = e.x - o.x, dy = e.y - o.y, d = Math.hypot(dx, dy); if (d < 26 && d > 0.1) { tx += dx / d * 30; ty += dy / d * 30; } }
        steer(e, tx, Math.min(ty, GROUND - 30), t.speed * fr, dt, 4); faceTo(e, c.x);
        if (e.cdT <= 0 && canStart(e)) { begin(e, 'Picado'); go(e, 'diveW'); e.dv = null;
          warn({ e, kind: 'line', t: t.diveWindup, follow: w => { const a = e.dv || dirTo(e, pCenter()); w.ax = e.x; w.ay = e.y; w.bx = e.x + a.x * t.diveDist; w.by = e.y + a.y * t.diveDist; w.locked = !!e.dv; } }); }
        break;
      }
      case 'diveW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10)); e.my = lerp(e.my, 0, Math.min(1, dt * 10)); faceTo(e, c.x);
        if (e.stT > t.diveWindup * 0.7 && !e.dv) e.dv = dirTo(e, aimPoint(e));
        if (windup(e, t.diveWindup / fr)) { go(e, 'dive'); release(e); e.run = 0; e.hitP = false; if (!e.dv) e.dv = dirTo(e, aimPoint(e)); }
        break;
      case 'dive':
        e.phase = 'active'; e.mx = e.dv.x * t.diveSpeed * fr; e.my = e.dv.y * t.diveSpeed * fr; e.run += t.diveSpeed * fr * dt; e.face = Math.sign(e.dv.x) || e.face;
        if (!e.hitP && ellipseHitsPlayer(e, 3) && hitP(e, 'Picado', t.dmg, e.dv.x * 200, -100)) e.hitP = true;
        if (e.run > t.diveDist || e.y >= GROUND - e.foot - 1) { go(e, 'orbit'); recover(e); e.cdT = t.cooldown * (0.7 + Math.random() * 0.6); e.mx *= 0.3; e.my *= 0.3; }
        break;
    }
  },
  draw(ctx, e, P) {
    const fl = Math.sin(G.t * 60 + e.id * 3);
    ctx.save(); ctx.globalAlpha *= P.mono ? 1 : 0.45;
    E_ell(ctx, P, -2, -8, 8, 3 + fl * 1.5, '#f4f0e0', -0.3); E_ell(ctx, P, 3, -8, 7, 3 - fl * 1.5, '#f4f0e0', 0.3);
    ctx.restore();
    E_poly(ctx, P, [[-8, -2], [-16, 0], [-8, 3]], '#3a2208');
    E_ell(ctx, P, 0, 0, 10, 7, '#d9a43a');
    E_line(ctx, P, [[-3, -6], [-3, 6]], 2.4, '#3a2208'); E_line(ctx, P, [[2, -6.5], [2, 6.5]], 2.4, '#3a2208');
    E_eye(ctx, P, e, 7, -2, 2);
  },
});
function dirTo(e, q) { const dx = q.x - e.x, dy = q.y - e.y, d = Math.hypot(dx, dy) || 1; return { x: dx / d, y: dy / d }; }
