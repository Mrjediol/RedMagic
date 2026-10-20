// e-morsa.js — Morsa (mundo de hielo): pesada; golpea el suelo con los colmillos y levanta una hilera de carámbanos que se
// queda un rato como obstáculo. Pregunta: la arena cambia de forma: ¿saltas la hilera a tiempo y no te quedas encerrado?
'use strict';
addFoe({
  id: 'walrus', name: 'Morsa', short: 'Morsa', world: 'hielo', rx: 48, ry: 32, home: 'idle',
  role: 'Pesada de control de suelo',
  question: 'Saltar la hilera de carámbanos que avanza hacia ti y no quedarte encerrado entre los pinchos que deja (duran un rato). Si te subes, te lanza un témpano.',
  moves: [
    { name: 'Muro de carámbanos', desc: 'Levanta la cabeza y clava los colmillos en el suelo: salen carámbanos uno tras otro hacia ti (cada uno avisa con una grieta) y se quedan clavados un rato como obstáculo que hace daño; luego se rompen.' },
    { name: 'Témpano', desc: 'Si estás lejos o en una plataforma, arranca un bloque de hielo y te lo lanza en parábola (marca dónde cae).' },
  ],
  combos: 'Con Ventisca: el viento te empuja contra los carámbanos. Con Acechadores (lobos): los pinchos te cortan la huida y saltan a por ti.',
  art: 'Morsa enorme y rechoncha, piel gris-azulada con escarcha en el lomo, bigotes blancos, colmillos de hielo larguísimos (azul pálido, translúcidos). Se mueve a saltitos sobre las aletas. Al atacar levanta la cabeza (colmillos arriba) y la estampa. Partes: cuerpo, cabeza, colmillos, 2 aletas delanteras, cola. ~2,4×1,6 u.',
  tuning: Object.assign(baseTuning(300, 90, 0.2, 5), {
    keep: [280, 0, 600, 10, 'Se queda a esta distancia'], walk: [45, 0, 200, 5, 'Velocidad'],
    spikeWindup: [0.75, 0.2, 2, 0.05, 'Muro: preparación (s)'], spikeN: [5, 1, 12, 1, 'Muro: carámbanos'], spikeGap: [70, 20, 200, 5, 'Muro: separación'],
    spikeStep: [0.12, 0.02, 0.5, 0.01, 'Muro: tiempo entre carámbanos (s)'], spikeWarn: [0.3, 0.05, 1, 0.05, 'Muro: grieta antes de salir (s)'],
    spikeH: [64, 20, 160, 2, 'Muro: altura'], spikeLife: [1.8, 0.2, 6, 0.1, 'Muro: se quedan (s)'], spikeDmg: [14, 0, 60, 1, 'Muro: daño'],
    tossRange: [380, 0, 1000, 10, 'Témpano: si estás a más de (o en una plataforma)'], tossWindup: [0.65, 0.1, 2, 0.05, 'Témpano: preparación (s)'],
    tossFlight: [1.0, 0.3, 2.5, 0.05, 'Témpano: vuelo (s)'], tossDmg: [14, 0, 60, 1, 'Témpano: daño'], tossR: [40, 10, 120, 1, 'Témpano: radio'],
    cooldown: [1.8, 0.2, 6, 0.1, 'Pausa entre ataques (s)'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    switch (e.st) {
      case 'idle':
        faceTo(e, p.x); e.cdT -= dt;
        walkDir(e, ad > t.keep + 80 ? Math.sign(dx) : ad < t.keep - 120 ? -Math.sign(dx) * 0.6 : 0, t.walk, dt, 3);
        if (e.cdT > 0 || !canStart(e)) break;
        if (ad > t.tossRange || playerUpTime() > 0.5) { begin(e, 'Témpano'); go(e, 'tossW'); }
        else { begin(e, 'Muro de carámbanos'); go(e, 'spikeW'); e.sdir = Math.sign(dx) || e.face; }
        break;
      case 'spikeW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 8)); e.face = e.sdir;
        if (windup(e, t.spikeWindup)) { go(e, 'spike'); release(e); walrusSpikes(e, t, e.sdir, t.spikeN); }
        break;
      case 'spike':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.8) { go(e, 'idle'); e.cdT = t.cooldown; }
        break;
      case 'tossW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 8)); faceTo(e, p.x);
        if (!windup(e, t.tossWindup)) break;
        go(e, 'toss'); release(e);
        { const tx = clamp(p.x + p.vx * t.tossFlight * 0.4, ARENA.x0 + 10, ARENA.x1 - 10), ty = surfaceUnder(tx, p.y);
          lobShot(e.x + e.face * e.rx, e.y - e.ry, tx, ty, t.tossFlight, { e, atk: 'Témpano', dmg: t.tossDmg, r: 12, style: 'ice', warnR: t.tossR,
            onLand: (x, y) => { foeBlast(e, x, y - 6, t.tossR, t.tossDmg, 'Témpano', { pal: PAL.ice }); sfx(k => SFXLIB.shatter(k, 0.7)); } }); }
        break;
      case 'toss':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.6) { go(e, 'idle'); e.cdT = t.cooldown; }
        break;
    }
  },
  draw(ctx, e, P) {
    const up = e.st === 'spikeW' || e.st === 'tossW' ? e.tell : 0, slam = e.st === 'spike' ? Math.max(0, 1 - e.stT * 5) : 0, hop = Math.abs(Math.sin(e.wp * 0.6)) * (Math.abs(e.mx) > 10 ? 4 : 0);
    ctx.save(); ctx.translate(0, -hop);
    E_poly(ctx, P, [[-46, 20], [-60, 10], [-58, 30]], '#5a6f80');                 // cola
    E_ell(ctx, P, -6, 4, 46, 28, '#5a6f80');
    E_ell(ctx, P, -10, 0, 40, 22, '#7a90a2');
    for (const [x, y] of [[-26, -16], [-6, -20], [12, -16]]) E_ell(ctx, P, x, y, 10, 4, ICE.pale);   // escarcha del lomo
    E_poly(ctx, P, [[10, 20], [26, e.ry], [2, e.ry]], '#4a5e6e');                  // aleta
    // cabeza: se levanta al preparar y baja de golpe
    ctx.save(); ctx.translate(34, -4); ctx.rotate(-up * 0.6 + slam * 0.3);
    E_ell(ctx, P, 0, 0, 18, 16, '#7a90a2');
    E_ell(ctx, P, 10, 8, 10, 7, '#c8d2da');
    E_poly(ctx, P, [[6, 12], [10, 12], [6 + slam * 4, 40 + slam * 6]], ICE.pale);  // colmillos
    E_poly(ctx, P, [[12, 12], [16, 12], [13 + slam * 4, 38 + slam * 6]], ICE.light);
    E_eye(ctx, P, e, 6, -6, 2.6);
    ctx.restore();
    ctx.restore();
  },
});
// hilera de carámbanos que avanza en dir desde la Morsa: grieta (aviso) y luego pincho que se queda (pieza genérica groundSpike)
function walrusSpikes(e, t, dir, n, x0) {
  const start = x0 === undefined ? e.x + dir * (e.rx + 34) : x0;
  for (let i = 0; i < n; i++) {
    const x = start + dir * i * t.spikeGap; if (x < ARENA.x0 + 8 || x > ARENA.x1 - 8) break;
    later(e, i * t.spikeStep, () => {
      warn({ kind: 'circle', x, r: 16, t: t.spikeWarn });
      later(null, t.spikeWarn, () => groundSpike(e, x, { h: t.spikeH, w: 18, life: t.spikeLife, dmg: t.spikeDmg, atk: 'Muro de carámbanos' }));
    });
  }
  G.shake += 5; const x = e.x + dir * (e.rx + 10); addFx(1.2, (ctx, layer, t0) => groundImpact(ctx, G.t, layer, t0, x, 0.6, PAL.ice, { seed: 5 }));
  sfx(k => { const t0 = nowT(); vThump(t0, 75, 30, 0.4 * k, 0.7, 3); });
}
