// e-golem.js — Golem de escoria (volcán): pesado que levanta PILARES de obsidiana entre tú y él (tapan tus disparos) y luego los
// revienta a puñetazos lanzándote la metralla. Pregunta: ¿rompes tú el pilar antes, o lo rodeas, en vez de dispararle al muro?
'use strict';
addFoe({
  id: 'slaggolem', name: 'Golem de escoria', short: 'Golem', world: 'volcan', rx: 44, ry: 46, home: 'roam',
  role: 'Pesado que construye cobertura',
  question: 'Sus pilares tapan tus disparos: rodéalos (saltando por encima) o rómpelos tú antes de que los reviente, porque si lo hace él te lanza la metralla. De cerca, golpe al suelo.',
  moves: [
    { name: 'Alzar pilar', desc: 'Golpea el suelo (marca en el suelo) y sale un pilar de obsidiana entre tú y él. Los pilares paran tus disparos y duran un rato (como mucho dos a la vez). Se pueden romper.' },
    { name: 'Reventar pilar', desc: 'Si hay un pilar entre los dos, va hacia él, carga el puño y lo revienta: la metralla sale en abanico hacia ti. Si lo rompes tú antes, no hay metralla.' },
    { name: 'Golpe al suelo', desc: 'Si estás cerca: levanta los dos puños y golpea el suelo (zona alrededor).' },
  ],
  combos: 'Con la Salamandra: pilares + rastros te dejan muy poco suelo. Con el Escorpión: dos que piden paciencia.',
  art: 'Golem grande de bloques de basalto y escoria con las juntas llenas de magma que brilla, un núcleo ardiente en el pecho, cabeza pequeña hundida entre los hombros y puños enormes. Los pilares: columnas de obsidiana negra brillante con filos violáceos. Partes: torso, cabeza, 2 brazos (puño), 2 piernas, núcleo; pilar (3 estados de grietas). ~2,2×2,3 u; pilar ~0,8×2,8 u.',
  tuning: Object.assign(baseTuning(320, 110, 0.15, 6), {
    speed: [50, 0, 200, 5, 'Velocidad'], keepMin: [200, 0, 500, 10, 'Le gusta estar a (mín.)'], keepMax: [320, 50, 700, 10, 'Le gusta estar a (máx.)'],
    pillarWindup: [0.7, 0.2, 2, 0.05, 'Alzar pilar: preparación (s)'], maxPillars: [2, 0, 4, 1, 'Pilares a la vez'], pillarHp: [60, 5, 400, 5, 'Pilar: vida'],
    pillarLife: [9, 1, 30, 0.5, 'Pilar: dura (s)'], pillarGap: [110, 40, 300, 5, 'Pilar: a esta distancia de él'],
    punchWindup: [0.55, 0.1, 2, 0.05, 'Reventar: preparación (s)'], shardN: [5, 1, 12, 1, 'Reventar: trozos'], shardSpeed: [440, 100, 900, 10, 'Reventar: velocidad'],
    shardDmg: [8, 0, 40, 1, 'Reventar: daño de cada trozo'],
    slamRange: [110, 30, 300, 5, 'Golpe al suelo: si estás a menos de'], slamWindup: [0.6, 0.1, 2, 0.05, 'Golpe al suelo: preparación (s)'],
    slamR: [110, 30, 300, 5, 'Golpe al suelo: radio'], slamDmg: [14, 0, 50, 1, 'Golpe al suelo: daño'], cooldown: [1.6, 0.2, 5, 0.1, 'Pausa entre ataques (s)'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    const mine = foes().filter(o => o.type === 'pillar' && !o.dead && o.owner === e);
    const between = mine.find(o => Math.sign(o.x - e.x) === Math.sign(dx) && Math.abs(o.x - e.x) < ad);
    switch (e.st) {
      case 'roam':
        faceTo(e, p.x); e.cdT -= dt;
        roam(e, dt, { min: t.keepMin, max: t.keepMax, speed: t.speed, burst: 2, hop: 0 });
        if (e.cdT > 0 || !canStart(e)) break;
        if (ad < t.slamRange && Math.abs(p.y - (e.y + e.foot)) < 80) { begin(e, 'Golpe al suelo'); go(e, 'slamW'); warn({ e, kind: 'circle', x: e.x, r: t.slamR, t: t.slamWindup }); }
        else if (between) { begin(e, 'Reventar pilar'); go(e, 'toPillar'); e.pillar = between; }
        else if (mine.length < t.maxPillars) {
          let x = e.x + Math.sign(dx || 1) * t.pillarGap; if (Math.abs(p.x - x) < 50) x = e.x + Math.sign(dx || 1) * Math.max(50, ad - 70);
          e.pillarX = clamp(x, ARENA.x0 + 30, ARENA.x1 - 30); begin(e, 'Alzar pilar'); go(e, 'raiseW'); warn({ e, kind: 'circle', x: e.pillarX, r: 18, t: t.pillarWindup });
        }
        break;
      case 'raiseW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10));
        if (!windup(e, t.pillarWindup)) break;
        go(e, 'raise'); release(e);
        if (Math.abs(G.player.x - e.pillarX) < 24) hitP(e, 'Alzar pilar', 8, Math.sign(G.player.x - e.pillarX || 1) * 300, -420);
        spawnFoe('pillar', e.pillarX, undefined, { aware: true, owner: e }); G.shake += 5; sfx(k => { const t0 = nowT(); vThump(t0, 70, 30, 0.35 * k, 0.6, 3); });
        break;
      case 'raise':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.6) { go(e, 'roam'); e.cdT = t.cooldown; }
        break;
      case 'toPillar': {
        const pl = e.pillar; if (!pl || pl.dead) { go(e, 'roam'); e.busy = false; e.cdT = 0.4; break; }
        const side = Math.sign(pl.x - e.x) || 1; e.face = side;
        walkTo(e, pl.x - side * (e.rx + pl.rx + 6), t.speed * 2.2, dt, 8);
        if (Math.abs(e.x - (pl.x - side * (e.rx + pl.rx + 6))) < 10) go(e, 'punchW');
        if (e.stT > 3) { go(e, 'roam'); e.busy = false; }
        break;
      }
      case 'punchW':
        e.mx = 0;
        if (!e.pillar || e.pillar.dead) { go(e, 'roam'); e.busy = false; e.cdT = 0.5; break; }   // se lo has roto antes: sin metralla
        if (!windup(e, t.punchWindup)) break;
        go(e, 'punch'); release(e);
        { const pl = e.pillar, sx = pl.x, sy = GROUND - 60, c = aimPoint(e), a0 = Math.atan2(c.y - sy, c.x - sx);
          for (let i = 0; i < t.shardN; i++) { const a = a0 + (i - (t.shardN - 1) / 2) * 0.16; foeShot({ e, atk: 'Reventar pilar', x: sx, y: sy, vx: Math.cos(a) * t.shardSpeed, vy: Math.sin(a) * t.shardSpeed, g: GRAV * 0.25, r: 6, style: 'rock', dmg: t.shardDmg, life: 2.5 }); }
          hitFx('die', sx, sy, pl); selfKill(pl); G.shake += 6; sfx(k => SFXLIB.shatter(k, 0.9)); }
        break;
      case 'punch':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.6) { go(e, 'roam'); e.cdT = t.cooldown; }
        break;
      case 'slamW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10));
        if (!windup(e, t.slamWindup)) break;
        go(e, 'slam'); release(e);
        if (G.player.y > GROUND - 30 && Math.abs(G.player.x - e.x) < t.slamR) hitP(e, 'Golpe al suelo', t.slamDmg, Math.sign(dx || 1) * 420, -380);
        { const x = e.x; addFx(1.2, (ctx, layer, t0) => groundImpact(ctx, G.t, layer, t0, x, t.slamR / 110, PAL.fire, { seed: 7 })); }
        G.shake += 7; sfx(k => { const t0 = nowT(); vThump(t0, 75, 30, 0.45 * k, 0.8, 3); });
        break;
      case 'slam':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.8) { go(e, 'roam'); e.cdT = t.cooldown; }
        break;
    }
  },
  draw(ctx, e, P) {
    const up = e.st === 'raiseW' || e.st === 'slamW' ? e.tell : 0, punch = e.st === 'punchW' ? e.tell : e.st === 'punch' ? Math.max(0, 1 - e.stT * 4) : 0;
    for (const s of [-1, 1]) E_line(ctx, P, [[s * 14, 20], [s * 16 + Math.sin(e.wp * 0.7 + s) * (Math.abs(e.mx) > 8 ? 4 : 0), e.ry]], 16, MAGMA.basalt);
    E_poly(ctx, P, [[-36, 22], [-40, -26], [-16, -40], [20, -38], [38, -20], [34, 22]], MAGMA.basalt);
    E_poly(ctx, P, [[-28, 14], [-30, -20], [-12, -30], [16, -28], [28, -14], [24, 14]], '#3e3034');
    E_line(ctx, P, [[-30, -6], [-10, -10], [8, -4], [30, -10]], 2.5, MAGMA.glow); E_line(ctx, P, [[-20, 8], [0, 4], [22, 10]], 2, MAGMA.glow);
    if (!P.mono) glow(ctx, 4, -8, 22, [255, 120, 40], 0.5 + 0.3 * Math.sin(G.t * 3));
    E_ell(ctx, P, 4, -8, 8, 8, MAGMA.hot);
    E_ell(ctx, P, 8, -44, 12, 9, MAGMA.basalt); E_eye(ctx, P, e, 14, -45, 2.6);
    // brazos: arriba al preparar golpe/pilar, adelante al reventar
    for (const s of [-1, 1]) {
      ctx.save(); ctx.translate(s * 30, -20); ctx.rotate(s < 0 ? 0.3 - up * 2.2 : -0.3 - up * 2.2 - punch * 1.2);
      E_line(ctx, P, [[0, 0], [6, 30]], 14, MAGMA.basalt); E_ell(ctx, P, 7, 38, 13, 12, '#3e3034'); E_line(ctx, P, [[2, 34], [12, 40]], 1.5, MAGMA.glow);
      ctx.restore();
    }
  },
});
// pilar de obsidiana: objeto (no cuenta para despejar el encuentro) que para tus disparos; sube del suelo y se agrieta al dañarlo
addFoe({
  id: 'pillar', name: 'Pilar de obsidiana', short: 'Pilar', world: 'volcan', rx: 16, ry: 56, home: 'stand', prop: true,
  role: '—', question: '—', moves: [], tuning: baseTuning(60, 0, 0, 0),
  init(e) { const ot = e.owner ? FT(e.owner) : null; e.hp = e.hpMax = (ot ? ot.pillarHp : 60) * (G.es.hpMul || 1); e.life = ot ? ot.pillarLife : 9; e.rise = 0; },
  think(e, dt) { e.mx = 0; e.rise = Math.min(1, e.rise + dt / 0.25); if (e.stT > e.life) { hitFx('die', e.x, e.y, e); selfKill(e); } },
  onDeath(e) { const x = e.x; addFx(1.2, (ctx, layer, t0) => debris(ctx, G.t, t0, x, layer, { n: 8, c1: MAGMA.obs, c2: MAGMA.obsL, seed: 3 })); },
  draw(ctx, e, P) {
    const h = e.ry * 2 * (e.rise === undefined ? 1 : e.rise), crack = 1 - e.hp / e.hpMax;
    ctx.save(); ctx.translate(0, e.ry);
    E_poly(ctx, P, [[-15, 0], [-13, -h], [-4, -h - 8], [13, -h + 2], [15, 0]], MAGMA.obs);
    E_poly(ctx, P, [[-8, 0], [-7, -h + 6], [-2, -h], [-3, 0]], MAGMA.obsL);
    E_line(ctx, P, [[10, -h + 10], [12, -h * 0.4]], 1.5, '#8a6aa8');
    if (crack > 0.2) E_line(ctx, P, [[-10, -h * 0.7], [0, -h * 0.55], [-6, -h * 0.35], [6, -h * 0.2]], 1.6, MAGMA.glow);
    if (crack > 0.55) E_line(ctx, P, [[8, -h * 0.85], [-2, -h * 0.7], [4, -h * 0.5]], 1.6, MAGMA.glow);
    ctx.restore();
  },
});
