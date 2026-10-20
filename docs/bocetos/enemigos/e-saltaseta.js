// e-saltaseta.js — Saltaseta (bosque): seta pequeña que avanza a saltitos con un ritmo fijo y, al tercero, da un gran salto
// que cae donde estés. Pregunta: ¿cuentas el ritmo («1, 2, 3… ¡PLAF!») y te apartas del gran salto, aunque vengan varias?
'use strict';
addFoe({
  id: 'shroom', name: 'Saltaseta', short: 'Seta', world: 'bosque', rx: 18, ry: 20, home: 'hop', platforms: true,
  role: 'Saltarina de grupo (ritmo)',
  question: 'Leer el ritmo: tres saltitos y un gran salto que cae donde estás (también en tu plataforma). En grupo, cada una lleva su cuenta: apartarte a tiempo de la que va a caer.',
  moves: [
    { name: 'Saltitos', desc: 'Avanza hacia ti a saltitos, siempre con el mismo compás; cerca de ti salta de lado a lado. No hacen daño (salvo el contacto).' },
    { name: 'Gran salto', desc: 'Tras unos saltitos se aplasta (se enciende) y salta alto hacia donde estás: marca en el suelo (o en tu plataforma) dónde cae; al caer, ¡PLAF!, nube de esporas que hace daño alrededor. Después se queda un momento aturdida.' },
  ],
  combos: 'En grupos de 2-3 (ritmos desfasados). Con Zarza: saltar los muros de espinas sin caer en un ¡PLAF!. Con el Oso: te obliga a moverte mientras te persigue.',
  art: 'Seta de sombrero rojo pardo con motas crema, pie blanco hueso con dos ojillos oscuros bajo el sombrero y musgo en la base; algunas motas son cristales turquesa que brillan. Muy elástica: se estira al saltar y se aplasta al caer (fácil de animar por escala). Partes: sombrero, pie, ojos. ~0,9×1 u.',
  tuning: Object.assign(baseTuning(45, 15, 1.3, 4), {
    hopV: [300, 100, 600, 10, 'Saltito: impulso'], hopX: [120, 0, 300, 5, 'Saltito: avance'], hopRest: [0.28, 0.05, 1, 0.01, 'Saltito: pausa en el suelo (s)'],
    keep: [110, 0, 400, 5, 'Deja de acercarse a'], bigEvery: [3, 1, 8, 1, 'Gran salto: cada N saltitos'],
    bigWindup: [0.45, 0.1, 1.5, 0.05, 'Gran salto: preparación (s)'], bigH: [150, 40, 300, 5, 'Gran salto: altura'], lead: [0.25, 0, 1, 0.05, 'Gran salto: adelanta tu movimiento (s)'],
    slamR: [58, 15, 160, 1, 'Gran salto: radio'], slamDmg: [14, 0, 60, 1, 'Gran salto: daño'], rest: [0.6, 0, 2, 0.05, 'Tras el gran salto: aturdida (s)'],
  }),
  init(e) { e.hops = Math.floor(Math.random() * 2); e.restT = Math.random() * 0.3; e.wasAir = false; },
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    if (e.onGround && e.wasAir) { e.landT = G.t; e.mx = 0; if (e.st === 'hop') sfx(k => { const t0 = nowT(); vThump(t0, 220, 120, 0.08 * k, 0.15, 1); }); }
    e.wasAir = !e.onGround;
    switch (e.st) {
      case 'hop':
        faceTo(e, p.x);
        if (!e.onGround) break;
        e.mx = 0; e.restT -= dt;
        if (e.restT > 0) break;
        if (e.hops >= t.bigEvery && canStart(e)) { begin(e, 'Gran salto'); go(e, 'bigW'); break; }
        { const dir = ad > t.keep ? Math.sign(dx) : (Math.random() < 0.5 ? -1 : 1), T = 2 * t.hopV / GRAV;
          groundHop(e, dir * t.hopX / T, t.hopV); e.hops++; e.restT = t.hopRest * (0.85 + Math.random() * 0.3); }
        break;
      case 'bigW':
        e.mx = 0; faceTo(e, p.x);
        if (!windup(e, t.bigWindup)) break;
        { const tx = clamp(p.x + p.vx * t.lead, ARENA.x0 + e.rx, ARENA.x1 - e.rx), feet = surfaceUnder(tx, p.y);
          const T = leapTo(e, tx, feet, t.bigH); go(e, 'big'); release(e);
          warn({ e, kind: 'circle', x: tx, y: feet + 2, r: t.slamR, t: T }); sfx(k => SFXLIB.whoosh(k, 900, 2400, 0.3, 0.14)); }
        break;
      case 'big':
        e.phase = 'active';
        if (e.onGround && e.stT > 0.08) {
          e.mx = 0; go(e, 'rest'); recover(e); e.hops = 0;
          foeBlast(e, e.x, e.y + e.foot - 8, t.slamR, t.slamDmg, 'Gran salto', { pal: WOOD.pal });
          const x = e.x, y = e.y + e.foot; addFx(1, (ctx, layer, t0) => { const u = G.t - t0; for (let i = 0; i < 9; i++) { const R = rand(i + 4), a = -Math.PI * R(), d = u * (40 + R() * 50); if ((R() < 0.5 ? 'back' : 'front') === layer) disc(ctx, x + Math.cos(a) * d * 1.4, y - 8 + Math.sin(a) * d * 0.6, (6 + R() * 6) * (1 - u), WOOD.pal.light, 0.6 * (1 - u), layer); } });
        }
        break;
      case 'rest':
        e.phase = 'recover'; e.mx = 0;
        if (e.stT > t.rest) { go(e, 'hop'); e.restT = 0.2; }
        break;
    }
  },
  draw(ctx, e, P) {
    const land = Math.max(0, 1 - (G.t - (e.landT || -9)) / 0.18), air = !e.onGround ? clamp(-e.vy / 600, -0.5, 1) : 0, sq = e.st === 'bigW' ? e.tell * 0.35 : land * 0.25;
    ctx.save(); ctx.translate(0, e.ry); ctx.scale(1 + sq - air * 0.15, 1 - sq + air * 0.22); ctx.translate(0, -e.ry);
    E_ell(ctx, P, 0, e.ry - 2, 10, 4, WOOD.moss);
    E_poly(ctx, P, [[-8, e.ry - 2], [-6, -4], [6, -4], [8, e.ry - 2]], WOOD.stem);
    E_eye(ctx, P, e, 1, 4, 2); E_eye(ctx, P, e, 6, 4, 2);
    E_ell(ctx, P, 0, -8, 20, 12, WOOD.capD);
    E_ell(ctx, P, -1, -10, 18, 10, WOOD.cap);
    for (const [x, y, r, c] of [[-9, -12, 3.2, WOOD.spot], [3, -15, 2.6, WOOD.crystal], [10, -10, 2.4, WOOD.spot], [-2, -6, 2, WOOD.spot]]) E_ell(ctx, P, x, y, r, r * 0.8, c);
    ctx.restore();
  },
});
