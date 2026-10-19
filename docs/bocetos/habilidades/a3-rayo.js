// a3-rayo.js — 3 · Rayo: mantener para canalizar un haz continuo que quema al primer enemigo que toca.
'use strict';
(function () {
  const G = '3 · Rayo';
  const S = {
    charge: sound('ray_charge', G, 'Carga', k => { const t = nowT(), d = 0.33 * k; vRise(t, 'sawtooth', 90, 340, d, 0.1, 300, 3200, 1, 0.12, 4); vNoise(t, d, 0.1, 'highpass', 1500, 7000, 1, { rising: true }); }),
    ignite: sound('ray_ignite', G, 'Encendido', k => { const t = nowT(); vNoise(t, 0.08 * k, 0.4, 'highpass', 3000, 8000, 1, { att: 0.001, wet: 0.15 }); vSaw(t, 1800, 180, 0.16 * k, 0.22, 5000, 400, 2, 0.15); vThump(t, 140, 55, 0.2 * k, 0.55, 2); }),
    sustain: (k, dur, w = 1) => { const t = nowT(), d = dur * k;
      vSaw(t, 110, 112, d, 0.09 * w, 1500, 1200, 1.5, 0.15, { hold: true, att: 0.02, trem: 24 }); vSaw(t, 111.7, 113, d, 0.08 * w, 1500, 1200, 1.5, 0.15, { hold: true, att: 0.02 });
      vSaw(t, 220, 224, d, 0.03 * w, 2500, 2000, 0, 0.15, { hold: true, att: 0.02 });
      vNoise(t, d, 0.08 * w, 'bandpass', 3200, 2600, 2, { hold: true, att: 0.02, lfo: [17, 0.6], wet: 0.15 }); vCrackle(t, d, 0.12 * w, Math.round(50 * dur), 0.15); },
    tick: k => { const t = nowT(); vThump(t, 130, 70, 0.07 * k, 0.18, 1.5, 0.05); },
    off: sound('ray_off', G, 'Apagado', k => { const t = nowT(); vSaw(t, 112, 34, 0.32 * k, 0.12, 1400, 140, 1.5, 0.2); vNoise(t, 0.3 * k, 0.12, 'bandpass', 3000, 400, 1.5, { wet: 0.25 }); }),
    prism: (k, dur) => { const t = nowT(), d = dur * k; vRing(t, [1318, 1976, 2637], 0.3 * k, 0.05, 0.4); for (let i = 1; i < 6; i++) vRing(t + d * i / 6, [1318 * (1 + i * 0.02), 1976], 0.25 * k, 0.025, 0.4); vSaw(t, 660, 664, d, 0.025, 3000, 2500, 0, 0.3, { hold: true, att: 0.05, trem: 9 }); },
    split: sound('ray_split', G, 'Prisma · se divide', k => { const t = nowT(); vRing(t, [1318, 1976, 2637, 3520], 0.5 * k, 0.07, 0.4); vNoise(t, 0.1 * k, 0.2, 'highpass', 5000, 9000, 1, { att: 0.002 }); }),
    freeze: k => SFXLIB.freeze(k, 0.95),
    shatter: sound('ray_shatter', G, 'Hielo · añicos al soltar', k => SFXLIB.shatter(k, 1.2)),
    life: (k, dur) => { const t = nowT(), d = dur * k; [523, 659, 784, 1047].forEach((f, i) => { for (let j = 0; j < 2; j++) vTone(t + (i + j * 4) * d / 8, f, f * 1.01, 0.4 * k, 0.035, 'sine', 0.45); }); vNoise(t, d, 0.05, 'bandpass', 600, 1800, 1.5, { hold: true, att: 0.1, wet: 0.3 }); },
  };
  sound('ray_sustain', G, 'Mantener (1 s)', k => S.sustain(k, 1));
  sound('ray_prism', G, 'Prisma · zumbido', k => S.prism(k, 0.8));
  sound('ray_life', G, 'Robo de vida · flujo', k => S.life(k, 0.9));

  const TI = 0.35, TE = 1.35;


  function build(P) {
    P = Object.assign({ pal: 'set', mode: 'base' }, P);
    const pal = PAL[P.pal];
    const env = t => easeOut(seg(t, TI, TI + 0.1)) * (1 - easeIn(seg(t, TE, TE + 0.14)));
    const reach = t => easeOut(seg(t, TI, TI + 0.07));
    const E1 = { x: 400, hits: [] }, E2 = { x: 545, hits: [] }, enemies = [E1, E2];
    E1.pull = t => [10 * easeOut(seg(t, TI, TE)) * (1 - easeOut(seg(t, TE, TE + 0.4))) + (t > TI && t < TE ? Math.sin(t * 80) * 1.2 : 0), 0];
    const sfx = [{ t: 0.02, f: S.charge }, { t: TI, f: S.ignite }, { t: TI + 0.02, f: k => S.sustain(k, TE - TI) }, { t: TE, f: S.off }];
    const shakes = [{ t: TI, a: 3, d: 0.2 }], frames = [];
    const tickTimes = []; for (let tt = TI + 0.08; tt < TE; tt += 0.12) tickTimes.push(tt);
    for (const tt of tickTimes) { E1.hits.push({ t: tt, k: 0, fl: 0.06, shake: 1.6, sq: 0.03 }); sfx.push({ t: tt, f: S.tick }); }
    const contactX = t => enemyPos(t, E1).x - 25;

    // prisma: atraviesa al primero y se divide en tres haces dorados al salir por detrás
    let E3 = null; const TP = TI + 0.14;
    if (P.mode === 'prism') {
      E3 = { x: 520, cy: 165, hits: [] }; enemies.push(E3);
      for (const tt of tickTimes) if (tt > TP + 0.05) { E2.hits.push({ t: tt + 0.03, k: 0, fl: 0.06, shake: 1.6 }); E3.hits.push({ t: tt + 0.05, k: 0, fl: 0.06, shake: 1.6 }); }
      sfx.push({ t: TP, f: S.split }, { t: TP + 0.02, f: k => S.prism(k, TE - TP) });
    }
    // hielo: congela al enemigo y el suelo bajo el haz; al soltar, añicos
    const TSH = TE + 0.08;
    if (P.mode === 'ice') {
      E1.frozen = t => t < TSH ? 0.9 * easeInOut(seg(t, TI + 0.15, TE - 0.1)) : 0;
      E1.crack = t => seg(t, TE - 0.25, TSH);
      E1.tint = t => [PAL.ice.light, t < TSH ? 0.5 * seg(t, TI + 0.1, TE) : 0];
      E1.hits.push({ t: TSH, k: 22, fl: 0.25, shake: 6, sq: 0.18 });
      sfx.push({ t: TI + 0.15, f: S.freeze }, { t: TSH, f: S.shatter }); shakes.push({ t: TSH, a: 7, d: 0.35 }); frames.push({ t: TSH, d: 0.05 });
    }
    const total = P.mode === 'ice' ? TSH + 1.0 : TE + 0.85;
    if (P.mode === 'life') sfx.push({ t: TI + 0.2, f: k => S.life(k, TE - TI - 0.2) });

    function draw(ctx, t, layer) {
      castFx(ctx, t, layer, 0.04, TI, pal, 1.1);
      const e = env(t), cx = contactX(t), tipX = lerp(O.x + 6, cx, reach(t));
      if (t > TI && t < TE + 0.16) {
        energyBeam(ctx, t, layer, O.x + 6, O.y, tipX, O.y, e, pal);
        if (layer === 'front') {                            // boca: luz y destello que gira
          glow(ctx, O.x + 6, O.y, 34 * e, pal.g1, 0.7 * e); star4(ctx, O.x + 6, O.y, 16 * e, t * 7, pal.core, e, layer);
          ellArc(ctx, O.x + 6, O.y, 6 + 6 * ((t * 6) % 1), 10 + 12 * ((t * 6) % 1), -1.3, 1.3, 1.6, pal.light, e * (1 - (t * 6) % 1), layer);
        }
        if (reach(t) >= 1) beamContact(ctx, t, layer, cx, O.y, e, pal, TI + 0.07, TE, 1, 1);
        else if (layer === 'front') { glow(ctx, tipX, O.y, 26, pal.g2, 0.9); star4(ctx, tipX, O.y, 14, 0, pal.core, 1, layer); }
      }
      // al apagarse: restos que flotan y se deshacen sobre la línea del haz
      if (t > TE && t < TE + 0.6) {
        const R = rand(61), u = seg(t, TE, TE + 0.6);
        for (let i = 0; i < 18; i++) {
          const x = lerp(O.x + 10, contactX(TE), R()), lay = R() < 0.5 ? 'back' : 'front', dy = -(10 + R() * 26) * easeOut(u), dx = (R() - 0.5) * 12 * u;
          if (lay === layer) disc(ctx, x + dx, O.y + dy, (1 + R() * 1.8) * (1 - u), i % 3 ? pal.light : pal.core, 1 - u, layer);
        }
      }
      // prisma
      if (P.mode === 'prism' && t > TP && t < TE + 0.16) {
        const p1 = enemyPos(t, E1), ex = p1.x + 25, gp = PAL.gold, e2 = e * easeOut(seg(t, TP, TP + 0.08));
        if (layer === 'front') {                           // luz que atraviesa el cuerpo + cristal-prisma
          glow(ctx, p1.x, O.y, 34, gp.g2, 0.5 * e2);
          ctx.save(); ctx.translate(ex + 4, O.y); ctx.rotate(t * 2.5); ctx.globalAlpha = e2;
          for (const [r, c] of [[11, gp.deep], [8, gp.mid], [4.5, gp.core]]) { ctx.fillStyle = col(c, layer); ctx.beginPath(); ctx.moveTo(0, -r * 1.3); ctx.lineTo(r, 0); ctx.lineTo(0, r * 1.3); ctx.lineTo(-r, 0); ctx.closePath(); ctx.fill(); }
          ctx.restore();
        }
        const targets = [{ E: E2, dx: -24, dy: 0 }, { E: E3, dx: -16, dy: 6 }, { ground: 492 }];
        targets.forEach((tg, i) => {
          let x1, y1; if (tg.E) { const q = enemyPos(t, tg.E); x1 = q.x + tg.dx; y1 = q.y + tg.dy; } else { x1 = tg.ground; y1 = GROUND - 2; }
          const rr = easeOut(seg(t, TP + i * 0.02, TP + 0.08 + i * 0.02)), tx = lerp(ex + 4, x1, rr), ty = lerp(O.y, y1, rr);
          energyBeam(ctx, t, layer, ex + 4, O.y, tx, ty, e2 * 0.95, gp, { sc: 0.5, seed: i * 2.1 });
          if (rr >= 1) beamContact(ctx, t, layer, x1, y1, e2, gp, TP + 0.1, TE, 0.6, 10 + i);
        });
        if (layer === 'back') sparks(ctx, t, TP, ex + 4, O.y, layer, { n: 10, pal: gp, sp: 260, seed: 5, len: 12, life: 0.4 });
      }
      // hielo: picos que crecen en el suelo bajo el haz y añicos al soltar
      if (P.mode === 'ice') {
        const I = PAL.ice, x0 = O.x + 40, x1 = contactX(TE) - 4, n = 11;
        for (let i = 0; i < n; i++) {
          const R = rand(i + 200), x = lerp(x0, x1, i / (n - 1)) + (R() - 0.5) * 8, tOn = TI + 0.1 + i * 0.05, g = easeOut(seg(t, tOn, tOn + 0.14)) * (1 - seg(t, TE + 0.4, TE + 0.8));
          const lay = R() < 0.5 ? 'back' : 'front';
          if (lay === layer) { iceSpike(ctx, x, GROUND + 2, (7 + R() * 13) * g, 3.5 + R() * 2, (R() - 0.5) * 0.4, g, layer); if (i % 2) iceSpike(ctx, x + 6, GROUND + 2, (4 + R() * 6) * g, 2.4, 0.3, g, layer); }
        }
        if (layer === 'back' && t > TI + 0.1) { const g = seg(t, TI + 0.1, TI + 0.7) * (1 - seg(t, TE + 0.4, TE + 0.8)); ctx.save(); ctx.globalAlpha = 0.7 * g; ctx.fillStyle = col(I.light, layer); ctx.fillRect(x0 - 10, GROUND, (x1 - x0 + 20) * seg(t, TI + 0.1, TI + 0.7), 3); ctx.restore(); }
        if (t >= TSH) {
          const p = enemyPos(TSH, E1), u = t - TSH;
          if (layer === 'front') { glow(ctx, p.x, p.y, 90 * (1 - seg(u, 0, 0.25)), I.g1, 0.7 * (1 - seg(u, 0, 0.25))); star4(ctx, p.x, p.y, 50 * (1 - seg(u, 0, 0.18) * 0.5), 0.2, I.core, 1 - seg(u, 0, 0.18), layer); }
          const R = rand(78);
          for (let i = 0; i < 18; i++) {
            const ang = R() * TAU, v = 160 + R() * 260, lay = R() < 0.45 ? 'back' : 'front', lf = 0.6 + R() * 0.4; if (lay !== layer || u > lf) continue;
            const px = p.x + Math.cos(ang) * v * u, py = p.y + Math.sin(ang) * v * u + 360 * u * u; if (py > GROUND) continue;
            chunk(ctx, px, py, 4 + R() * 7, u * (6 + R() * 8), i + 3, I.mid, I.core, 1 - seg(u, lf * 0.7, lf), lay);
          }
          sparks(ctx, t, TSH, p.x, p.y, layer, { n: 14, pal: I, sp: 320, seed: 92, len: 16, life: 0.5 });
          dust(ctx, t, TSH + 0.02, p.x, layer, { n: 8, reach: 50, y: p.y + 30, seed: 67, life: 0.9, sc: 1.2 });
        }
      }
      // robo de vida: gotas carmesí que vuelven por el haz hasta el player y cruces que suben de él
      if (P.mode === 'life' && t > TI + 0.15 && t < TE + 0.4) {
        const L2 = PAL.life, cxx = contactX(t), R = rand(301), on = 1 - seg(t, TE, TE + 0.3);
        for (let i = 0; i < 14; i++) {
          const sp = 0.9 + R() * 0.7, s = ((t * sp * 1.3) + R()) % 1, x = lerp(cxx - 4, O.x + 4, s), wob = Math.sin(s * 14 + i) * 10 * (1 - s), lay = Math.sin(s * 14 + i) > 0 ? 'front' : 'back';
          if (lay !== layer) continue;
          drop(ctx, x, O.y + wob, Math.PI, 9, 2.6, i % 3 ? L2.mid : L2.light, on * Math.sin(Math.PI * s), layer);
          if (i % 3 === 0) glow(ctx, x, O.y + wob, 9, L2.g1, 0.5 * on * Math.sin(Math.PI * s));
        }
        if (layer === 'front') for (let i = 0; i < 6; i++) {
          const R2 = rand(i + 500), age = ((t - TI) * 1.2 + R2()) % 1, x = PX + (R2() - 0.5) * 36, y = GROUND - 30 - age * 50, a = Math.sin(Math.PI * age) * on, r = 4 + R2() * 2;
          ctx.save(); ctx.globalAlpha = clamp(a); ctx.fillStyle = col(L2.light, layer); ctx.fillRect(x - r, y - 1.2, r * 2, 2.4); ctx.fillRect(x - 1.2, y - r, 2.4, r * 2); ctx.restore();
        }
      }
    }
    const pose = t => {
      const p = poseCast(t, 0.04, TI, { lean: 0.08, kick: 0.08 });
      if (t > TI && t < TE) { p.lean = -0.07 + Math.sin(t * 70) * 0.008; p.squash = 0.02 * Math.sin(t * 50); }
      if (P.mode === 'life' && t > TI + 0.2) { p.aura = PAL.life.g1; p.auraA = (0.35 + 0.2 * Math.sin(t * 9)) * (1 - seg(t, TE, TE + 0.5)); }
      return p;
    };
    return { total, draw, enemies, playerPose: pose, shakes, frames, sfx };
  }

  addAbility({
    id: 'ray', num: 3, name: 'Rayo', short: 'Rayo',
    pitch: 'Mantener para canalizar un haz continuo: se enciende de golpe, quema por ticks al primer enemigo que toca (parpadeos y chispas que saltan hacia atrás) y se apaga dejando restos que flotan. Daño sostenido; obliga a quedarse quieto y apuntar.',
    fit: 'Arma de canalización (otra forma de disparo base: «mantener» en vez de «cargar y soltar»). Las variantes cambian qué pasa en el punto de contacto o a lo largo del haz.',
    ideas: [
      '<b>Sobrecarga</b> (Sistema nuevo): cuanto más tiempo mantienes, más grueso; pasado un umbral, al soltar estalla en el punto de contacto.',
      '<b>Barrido</b>: el haz sigue al cursor con retraso; con Haste el giro es más rápido.',
      '<b>Fuego</b>: el suelo bajo el haz queda ardiendo un momento (zona).',
      '<b>Reflejo</b>: rebota en paredes/suelo en ángulo (combina con Rebote).',
      '<b>Parry</b>: un parry perfecto enciende un rayo gratis de 0,4 s sin coste.',
    ],
    clips: [
      { title: '3 · Base', tag: 'Base', text: 'Carga corta y el haz se extiende de golpe hasta el enemigo. Haz por capas que respira, dos hilos en hélice (por detrás y por delante), anillos y motas que avanzan. Punto de contacto con destello y chispas continuas; el enemigo parpadea con cada tick y retrocede poco a poco.', P: {} },
      { title: '3 · Prisma', tag: 'Oro · Sistema nuevo: división', text: 'El haz atraviesa al primero y, al salir por detrás, un cristal-prisma lo divide en tres haces dorados: al de detrás, al volador y al suelo. Pasa de un objetivo a tres.', P: { mode: 'prism' } },
      { title: '3 · Hielo', tag: 'Hielo · Sistema nuevo: reacciones', text: 'Haz helado: el enemigo se va cubriendo de hielo y bajo el haz crecen picos de hielo en el suelo. Al soltar, el congelado estalla en añicos (misma reacción que el Abanico de hielo).', P: { pal: 'ice', mode: 'ice' } },
      { title: '3 · Robo de vida', tag: 'Lifesteal', text: 'Mismo haz; por él vuelven gotas carmesí desde el enemigo hasta el player, que brilla y suelta cruces de curación. Lectura clara de «me estoy curando mientras canalizo».', P: { mode: 'life' } },
    ],
    build,
  });
})();
