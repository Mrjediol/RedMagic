// e-colgante.js — Colgante (mundo de hielo): murciélago que cuelga del techo como un carámbano y se deja caer. Pregunta: ¿miras
// también hacia arriba, y le haces caer cuando te conviene (para castigarle clavado en el suelo)?
'use strict';
addFoe({
  id: 'icicle', name: 'Colgante', short: 'Colg.', world: 'hielo', rx: 16, ry: 26, fly: true, home: 'hang',
  role: 'Emboscador del techo (carámbano vivo)',
  question: 'Mirar también arriba: pasar por debajo lo hace caer. Provocar la caída a propósito y apartarse, para pegarle mientras está clavado en el suelo y mientras vuelve a subir.',
  moves: [
    { name: 'Caída', desc: 'Cuelga del techo de hielo y se arrastra por él hacia ti. Si pasas por debajo, tiembla (polvo de escarcha, zona en el suelo del tamaño del impacto) y cae en picado. Al chocar salta metralla de hielo a ras de suelo hacia los dos lados (saltarla) y se queda clavado un rato (recibe más daño).' },
    { name: 'Goteo', desc: 'Si estás lejos, de vez en cuando suelta un carámbano pequeño que cae recto donde estás (marca pequeña en el suelo).' },
    { name: 'Volver', desc: 'Tras clavarse, despliega las alas y sube despacio al techo: se le puede seguir pegando.' },
  ],
  combos: 'Encima de un Erizo o una Morsa: arriba y abajo a la vez. Con Ventisca: el viento te empuja debajo de él.',
  art: 'Murciélago colgado boca abajo cuyas alas plegadas SON carámbanos (de lejos parece una estalactita más). Cuerpo de pelo azul marino oscuro, cara pequeña con dos ojos claros arriba, garras agarradas al techo. Al caer es un carámbano con cara; al subir las alas se abren (membrana de hielo translúcida). Partes: cuerpo, 2 alas (plegada / abierta), cabeza. ~0,8×1,3 u.',
  tuning: Object.assign(baseTuning(70, 25, 0.3, 0), {
    crawl: [60, 0, 200, 5, 'Se arrastra por el techo a'], trigger: [70, 10, 250, 5, 'Cae si pasas a menos de'],
    shiver: [0.5, 0.1, 2, 0.05, 'Caída: temblor antes de caer (s)'], fallSpeed: [900, 200, 2000, 10, 'Caída: velocidad'],
    impactR: [46, 15, 150, 1, 'Caída: radio del impacto'], impactDmg: [16, 0, 60, 1, 'Caída: daño'],
    shards: [6, 0, 12, 1, 'Metralla: trozos'], shardSpeed: [260, 50, 600, 10, 'Metralla: velocidad'], shardDmg: [6, 0, 40, 1, 'Metralla: daño'],
    stuck: [1.3, 0, 4, 0.1, 'Clavado en el suelo (s)'], stuckVuln: [1.3, 1, 3, 0.05, 'Clavado: × daño recibido'],
    climb: [110, 20, 400, 5, 'Vuelve al techo a'], rehang: [1.5, 0, 6, 0.1, 'Tras volver: tarda en poder caer otra vez (s)'],
    dripEvery: [2.4, 0.5, 10, 0.1, 'Goteo: cada (s)'], dripDmg: [6, 0, 40, 1, 'Goteo: daño'],
  }),
  init(e) { e.hangY = CEIL + e.ry + 2; e.y = e.hangY; e.dripT = 1; },
  think(e, dt) {
    const t = FT(e), p = G.player, ad = Math.abs(dxP(e));
    switch (e.st) {
      case 'hang':
        e.cdT -= dt; e.dripT += dt;
        e.mx = lerp(e.mx, clamp(dxP(e), -1, 1) * (ad > 20 ? t.crawl : 0), Math.min(1, dt * 3)); e.my = (e.hangY - e.y) * 6;
        if (e.cdT <= 0 && ad < t.trigger + Math.abs(p.vx) * 0.3 && canStart(e)) {
          begin(e, 'Caída'); go(e, 'shiver');
          warn({ e, kind: 'circle', x: e.x, r: t.impactR, t: t.shiver + (GROUND - e.y) / t.fallSpeed });
        } else if (ad > 160 && e.dripT > t.dripEvery) {
          e.dripT = 0; const ty = surfaceUnder(p.x, p.y), x = p.x + p.vx * 0.3;
          warn({ kind: 'circle', x, y: ty + 2, r: 14, t: 0.75 });
          later(e, 0.35, () => foeShot({ e, atk: 'Goteo', x, y: CEIL + 4, vx: 0, vy: (ty - CEIL) / 0.4, r: 4, style: 'icicle', dmg: t.dripDmg, landY: ty, pal: ICE.pal,
            onLand: (lx, ly) => addFx(0.5, (ctx, layer, t0) => sparks(ctx, G.t, t0, lx, ly - 4, layer, { n: 5, pal: PAL.ice, sp: 120, seed: 3, len: 6, life: 0.3 })) }));
        }
        break;
      case 'shiver':
        e.mx = 0; e.my = 0;
        if (Math.random() < dt * 12) dustPuff(e.x, CEIL + 4);
        if (windup(e, t.shiver)) { go(e, 'fall'); release(e); e.hitP = false; sfx(k => SFXLIB.whoosh(k, 2600, 900, 0.25, 0.16)); }
        break;
      case 'fall':
        e.phase = 'active'; e.mx = 0; e.my = t.fallSpeed;
        if (!e.hitP && ellipseHitsPlayer(e, 2) && hitP(e, 'Caída', t.impactDmg, Math.sign(G.player.x - e.x || 1) * 300, -260)) e.hitP = true;
        if (e.y >= GROUND - e.foot - 2) {
          e.my = 0; go(e, 'stuck'); recover(e); makeVuln(e, t.stuckVuln, t.stuck);
          if (!e.hitP && circleHitsPlayer(e.x, GROUND - 10, t.impactR)) hitP(e, 'Caída', t.impactDmg, Math.sign(G.player.x - e.x || 1) * 340, -300);
          for (let i = 0; i < t.shards; i++) { const s = i % 2 ? 1 : -1, a = s > 0 ? -0.18 - (i >> 1) * 0.12 : Math.PI + 0.18 + (i >> 1) * 0.12;
            foeShot({ e, atk: 'Metralla', x: e.x + s * 10, y: GROUND - 8, vx: Math.cos(a) * t.shardSpeed, vy: Math.sin(a) * t.shardSpeed, g: GRAV * 0.5, r: 4, style: 'icicle', dmg: t.shardDmg, pal: ICE.pal, life: 2 }); }
          const x = e.x; addFx(0.9, (ctx, layer, t0) => impact(ctx, G.t, layer, t0, x, GROUND - 6, t.impactR / 70, PAL.ice, { seed: 9 }));
          G.shake += 5; sfx(k => SFXLIB.shatter(k, 0.8));
        }
        break;
      case 'stuck':
        e.phase = 'recover'; e.mx = 0; e.my = 0; e.y = GROUND - e.foot;
        if (e.stT > t.stuck) go(e, 'climb');
        break;
      case 'climb':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 4)); e.my = -t.climb;
        if (e.y <= e.hangY + 1) { e.y = e.hangY; go(e, 'hang'); e.cdT = t.rehang; }
        break;
    }
  },
  draw(ctx, e, P) {
    const open = e.st === 'climb' ? 1 : e.st === 'stuck' ? 0.3 : 0, sh = e.st === 'shiver' ? Math.sin(G.t * 80) * 2 * e.tell : 0;
    ctx.save(); ctx.translate(sh, 0);
    // alas: plegadas = dos carámbanos a los lados; abiertas = membranas que baten
    for (const s of [-1, 1]) {
      ctx.save(); ctx.translate(s * 6, -16);
      if (open > 0.5) { ctx.rotate(s * (1.2 + Math.sin(G.t * 16) * 0.5)); E_poly(ctx, P, [[0, 0], [s * 4, 30], [s * 14, 26], [s * 8, 0]], ICE.pale); }
      else E_poly(ctx, P, [[-3, 0], [3, 0], [s * 1.5, 34]], ICE.light);
      ctx.restore();
    }
    E_ell(ctx, P, 0, -16, 9, 10, ICE.dark);                                       // cuerpo de pelo, arriba
    E_poly(ctx, P, [[-7, -10], [7, -10], [0, e.ry]], ICE.pale);                    // el carámbano (cuerpo)
    E_poly(ctx, P, [[-2, -8], [3, -8], [0, e.ry - 6]], ICE.snow);
    E_line(ctx, P, [[-4, -25], [-6, -e.ry]], 2, ICE.dark); E_line(ctx, P, [[4, -25], [6, -e.ry]], 2, ICE.dark);   // garras al techo
    E_eye(ctx, P, e, -3, -14, 2); E_eye(ctx, P, e, 3, -14, 2);
    ctx.restore();
  },
});
