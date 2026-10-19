// a2-singularidad.js — 2 · Singularidad: orbe lento que se detiene, atrae a los enemigos y colapsa en una implosión.
'use strict';
(function () {
  const G = '2 · Singularidad';
  const S = {
    launch: sound('sing_launch', G, 'Lanzamiento', k => { const t = nowT(); vThump(t, 80, 42, 0.35 * k, 0.45, 1.5, 0.2); vNoise(t, 0.35 * k, 0.16, 'lowpass', 300, 1200, 2, { att: 0.02, wet: 0.2, pan: [-0.3, 0.3] }); }),
    hum: (k, dur, w = 1) => { const t = nowT(), d = dur * k;
      vSaw(t, 52, 66, d, 0.11 * w, 260, 900, 1.5, 0.2, { hold: true, att: 0.12, trem: 6 }); vSaw(t, 78.5, 99, d, 0.06 * w, 300, 1100, 1, 0.2, { hold: true, att: 0.12 });
      vNoise(t, d, 0.13 * w, 'bandpass', 250, 1800, 2.5, { rising: true, wet: 0.25, lfo: [7, 0.5] }); },
    collapse: sound('sing_collapse', G, 'Colapso (implosión)', k => { const t = nowT(); vNoise(t, 0.13 * k, 0.3, 'highpass', 400, 6000, 1, { rising: true, wet: 0.2 }); vTone(t, 300, 1400, 0.13 * k, 0.08, 'triangle', 0.1); }),
    burst: sound('sing_burst', G, 'Estallido', k => { SFXLIB.boom(k, 1); const t = nowT(); vRing(t, [98, 155, 233], 0.8 * k, 0.07, 0.4); }),
    burstFire: sound('sing_burst_fire', G, 'Estallido ígneo', k => { SFXLIB.boom(k, 1.05); SFXLIB.burn(k, 1.0); const t = nowT(); vNoise(t, 0.5 * k, 0.3, 'bandpass', 400, 2500, 0.8, { att: 0.01, wet: 0.3 }); }),
    slam: sound('sing_slam', G, 'Masa · estampa al volador', k => { const t = nowT(); vThump(t, 110, 30, 0.45 * k, 0.9, 3, 0.15); vNoise(t, 0.3 * k, 0.4, 'lowpass', 1800, 150, 0.8, { dr: 2, wet: 0.2 }); }),
    capture: sound('sing_capture', G, 'Catapulta · atrapa esquirlas', k => { const t = nowT(); vNoise(t, 0.5 * k, 0.12, 'bandpass', 1500, 3500, 3, { hold: true, att: 0.05, lfo: [14, 0.8], wet: 0.2 }); vRing(t, [2600, 3900], 0.2 * k, 0.04, 0.3); }),
    eject: sound('sing_eject', G, 'Catapulta · salen disparadas', k => { const t = nowT(); vNoise(t, 0.2 * k, 0.3, 'bandpass', 6000, 1200, 1.2, { att: 0.002, wet: 0.2 }); vRing(t, [2300, 3450, 4700], 0.25 * k, 0.05, 0.25); }),
  };
  const fanRel = SND.fan_release ? SND.fan_release.fn : SFXLIB.whoosh;
  const fanHit = SND.fan_hit ? SND.fan_hit.fn : SFXLIB.hitEnergy;

  const TR = 0.32, XS = 462, YS = HY - 10;


  function build(P) {
    P = Object.assign({ pal: 'void', burst: 'set', mode: 'base', sc: 1, dur: 0.8 }, P);
    const pal = PAL[P.pal], bp = PAL[P.burst], s = P.sc;
    const TA = TR + 0.5, TC = TA + P.dur, TB = TC + 0.12, total = TB + (P.mode === 'fire' ? 1.3 : 1.05);
    const orbX = t => t < TA ? lerp(O.x + 10, XS, easeOut(seg(t, TR, TA))) : XS;
    const orbY = t => t < TA ? YS - 26 * bump(seg(t, TR, TA)) : YS + Math.sin(t * 5) * 2;
    const pullU = t => t < TC ? easeIn(seg(t, TA, TC)) * 0.9 + 0.1 * seg(t, TA, TC) : (1 - easeOut(seg(t, TB, TB + 0.3)));
    const jit = (t, k) => Math.sin(t * 61 + k) * 1.5 * (t > TA && t < TB ? 1 : 0);

    const mkE = (ex, cy) => {
      const E = { x: ex, hits: [] }; if (cy !== undefined) E.cy = cy;
      const dx = XS - ex, dy = cy === undefined ? 0 : YS - 8 - cy;
      E.pull = t => { const u = pullU(t); return [dx * 0.42 * u + jit(t, ex), dy * 0.78 * u]; };
      E.hits.push({ t: TB, k: Math.sign(-dx) * 28 * s, fl: 0.28, shake: 6, sq: 0.16, d: 0.6 });
      return E;
    };
    const enemies = [mkE(400), mkE(532)];
    const shakes = [{ t: TB, a: 9 * s, d: 0.4 }], frames = [{ t: TB, d: 0.06 }];
    const sfx = [{ t: 0.02, f: k => SFXLIB.castShort(k) }, { t: TR, f: S.launch }, { t: TA - 0.05, f: k => S.hum(k, TC - TA + 0.05, s) },
                 { t: TC, f: S.collapse }, { t: TB, f: P.mode === 'fire' ? S.burstFire : S.burst }];

    // Masa: el volador es atraído y, al estallar, estampado contra el suelo
    let fly = null, TSL = TB + 0.16;
    if (P.mode === 'tank') {
      fly = { x: 505, cy: 165, hits: [] };
      const dx = XS - 505, dy0 = YS - 165;
      fly.pull = t => {
        if (t < TB) { const u = pullU(t); return [dx * 0.55 * u + jit(t, 3), dy0 * 0.62 * u]; }
        const u = easeIn(seg(t, TB, TSL)), py = lerp(dy0 * 0.62, GROUND - 30 - 165, u);
        return [dx * 0.55 + 30 * u, py];
      };
      fly.frozen = null;
      fly.hits.push({ t: TB, k: 0, fl: 0.25, shake: 5 }, { t: TSL, k: 0, fl: 0.2, shake: 8, sq: 0.3 });
      enemies.push(fly);
      sfx.push({ t: TSL, f: S.slam }); shakes.push({ t: TSL, a: 8, d: 0.35 });
    }
    // Fuego: los atrapados arden
    if (P.mode === 'fire') for (const E of enemies) {
      E.burn = t => t < TB ? 0 : 1 - seg(t, TB + 0.9, TB + 1.25);
      E.tint = t => ['#ff7a2e', t < TB ? 0 : (0.25 + 0.12 * Math.sin(t * 30)) * (1 - seg(t, TB + 0.9, TB + 1.25))];
    }
    // Catapulta: un abanico disparado durante la atracción queda orbitando y sale despedido al estallar
    const TF = TA + 0.12, cat = [];
    if (P.mode === 'catapult') {
      const outs = [Math.PI + 0.06, -0.05, -Math.PI / 2 - 0.5, -0.75, 0.42];
      for (let i = 0; i < 5; i++) {
        const a0 = -0.22 + i * 0.11, R = rand(i + 40), tCap = TF + 0.22 + i * 0.012, ang0 = Math.atan2(O.y + 0 - YS, O.x - XS) + (R() - 0.5) * 0.6;
        cat.push({ i, a0, tCap, ang0, orbR: 44 + (i % 3) * 9, spin: 9 + R() * 3, out: outs[i] });
      }
      sfx.push({ t: TF, f: fanRel }, { t: TF + 0.22, f: S.capture }, { t: TB, f: S.eject });
      // las que salen hacia los enemigos los golpean
      enemies[0].hits.push({ t: TB + 0.06, k: -12, fl: 0.18, shake: 4 });
      enemies[1].hits.push({ t: TB + 0.1, k: 14, fl: 0.18, shake: 4 });
      sfx.push({ t: TB + 0.06, f: fanHit }, { t: TB + 0.1, f: fanHit });
    }
    const catPos = (c, t) => {
      if (t < c.tCap) { const u = seg(t, TF, c.tCap), x0 = O.x + 10, y0 = O.y + Math.sin(c.a0) * 40; return { x: lerp(x0, XS + Math.cos(c.ang0) * c.orbR, u), y: lerp(y0, YS + Math.sin(c.ang0) * c.orbR * 0.6, u), a: c.a0 }; }
      if (t < TB) {                                              // orbita (se cierra al colapsar)
        const shrink = 1 - 0.6 * seg(t, TC, TB), th = c.ang0 + (t - c.tCap) * c.spin;
        return { x: XS + Math.cos(th) * c.orbR * shrink, y: YS + Math.sin(th) * c.orbR * 0.6 * shrink, a: th + Math.PI / 2, th };
      }
      const u = t - TB, d = 12 + 900 * u;
      return { x: XS + Math.cos(c.out) * d, y: YS + Math.sin(c.out) * d, a: c.out };
    };

    function draw(ctx, t, layer) {
      castFx(ctx, t, layer, 0.04, TR, pal, 1.1);
      if (P.mode === 'catapult') castFx(ctx, t, layer, TA - 0.1, TF, PAL.set, 0.7, { seed: 9 });
      const ox = orbX(t), oy = orbY(t), live = t > TR && t < TB;
      if (live) {
        const form = easeOut(seg(t, TR, TA)), col = 1 - easeIn(seg(t, TC, TB)), ss = s * (0.45 + 0.55 * form) * col;
        // anillos de succión que se cierran
        if (t > TA && t < TC) for (let i = 0; i < 3; i++) {
          const u = ((t - TA) * 2.4 + i / 3) % 1;
          ellArc(ctx, ox, oy, (95 - 80 * easeIn(u)) * s, (95 - 80 * easeIn(u)) * s * 0.92, layer === 'back' ? Math.PI : 0, layer === 'back' ? TAU : Math.PI, 1.6 * s, pal.light, 0.35 * Math.sin(Math.PI * u), layer, { taper: 0, wob: 0.15, seed: i + 3 });
        }
        // partículas en espiral hacia el centro (aplastadas como el disco)
        const R = rand(23), n = t > TA ? 26 : 10;
        for (let i = 0; i < n; i++) {
          const rate = 0.9 + R() * 0.9, ph = R(), a0 = R() * TAU, age = ((t - TR) * rate * (t > TC ? 3 : 1) + ph) % 1, r = 120 * s * Math.pow(1 - age, 1.4), th = a0 + age * 5.5;
          const rot = -0.28, ex = Math.cos(th) * r, ey = Math.sin(th) * r * 0.42, px = ox + ex * Math.cos(rot) - ey * Math.sin(rot), py = oy + ex * Math.sin(rot) + ey * Math.cos(rot);
          if ((Math.sin(th) < 0) !== (layer === 'back')) continue;
          const th2 = th - 0.25, r2 = r * 1.12, ex2 = Math.cos(th2) * r2, ey2 = Math.sin(th2) * r2 * 0.42, qx = ox + ex2 * Math.cos(rot) - ey2 * Math.sin(rot), qy = oy + ex2 * Math.sin(rot) + ey2 * Math.cos(rot);
          drop(ctx, px, py, Math.atan2(py - qy, px - qx), (5 + 10 * age) * s, (1.4 + 1.2 * age) * s, [pal.core, pal.light, pal.mid, PAL.set.light][i % 4], Math.sin(Math.PI * age) * form, layer);
        }
        // polvo del suelo arrastrado hacia la singularidad
        if (t > TA && layer === 'front') {
          for (let i = 0; i < 10; i++) {
            const Rr = rand(i * 9 + 1), side = i % 2 ? 1 : -1, x0 = XS + side * (50 + Rr() * 110) * s, rate = 1.2 + Rr(), age = ((t - TA) * rate + Rr()) % 1;
            const px = lerp(x0, XS + side * 12, easeIn(age)), py = GROUND - 3 - age * age * 28 * s;
            strokeTaper(ctx, [{ x: px, y: py }, { x: px + side * 12 * age + side * 4, y: py + 2 }], 3, '#c9d3dc', 0.6 * Math.sin(Math.PI * age), layer);
          }
        }
        singularityDisk(ctx, t, layer, ox, oy, ss, pal, 1);
        if (layer === 'front') singularityCore(ctx, t, ox, oy, ss, pal, 1);
        if (layer === 'back') { // estela de distorsión durante el viaje
          if (t < TA) for (let k = 1; k <= 3; k++) { const tt = t - k * 0.04; if (tt > TR) ellArc(ctx, orbX(tt), orbY(tt), 14 * s * (1 + k * 0.2), 14 * s * (1 + k * 0.2), 0, TAU, 1.4, pal.light, 0.4 - k * 0.1, layer, { taper: 0, wob: 0.2, seed: k }); }
        }
        // colapso: líneas que convergen y punto blanco que crece
        if (t > TC && layer === 'front') {
          const u = seg(t, TC, TB), Rr = rand(55);
          for (let i = 0; i < 14; i++) { const a = Rr() * TAU, r0 = (90 - 80 * u) * s, r1 = r0 + 40 * s * (1 - u); needle(ctx, ox + Math.cos(a) * r0, oy + Math.sin(a) * r0, ox + Math.cos(a) * r1, oy + Math.sin(a) * r1, 2.4 * s, i % 2 ? pal.core : pal.light, u, layer); }
          glow(ctx, ox, oy, 40 * s * u, bp.g2, u); disc(ctx, ox, oy, 7 * s * u, '#ffffff', u, layer);
        }
      }
      // estallido
      if (t >= TB) {
        const u = t - TB, ru = seg(u, 0, 0.42), fu = seg(u, 0, 0.18);
        if (layer === 'front') {
          glow(ctx, XS, YS, 140 * s * (1 - fu * 0.4), bp.g1, 0.8 * (1 - fu));
          glow(ctx, XS, YS, 60 * s, bp.g2, 0.9 * (1 - fu));
          star4(ctx, XS, YS, 70 * s * (1 - fu * 0.4), 0.15, bp.core, 1 - fu, layer, 0.12);
          disc(ctx, XS, YS, 18 * s * (1 - fu), bp.core, 1 - fu, layer);
        }
        // onda: hebras parciales con ruido (no una media luna sólida)
        for (let i = 0; i < 6; i++) {
          const Rr = rand(i + 70), a0 = Rr() * TAU, span = 0.9 + Rr() * 1.4, k = 0.85 + Rr() * 0.3, mid = a0 + span / 2;
          if ((Math.sin(mid) < 0) !== (layer === 'back')) continue;
          const r = (24 + 150 * easeOut(ru)) * s * k;
          ellArc(ctx, XS, YS, r, r * 0.92, a0, a0 + span, (5 - 4 * ru) * s, i % 2 ? bp.mid : bp.light, 1 - ru, layer, { wob: 0.1, seed: i });
        }
        groundRing(ctx, XS, GROUND, (20 + 130 * easeOut(ru)) * s, 4 * s * (1 - ru) + 0.4, bp.mid, 1 - ru, layer, { wob: 0.08, seed: 4 });
        const Rr = rand(81);
        for (let i = 0; i < 18; i++) {
          const ang = Rr() * TAU, l0 = (14 + Rr() * 20) * s, nu = seg(u, 0, 0.3), l1 = l0 + (40 + Rr() * 60) * s * easeOut(nu), lay = Rr() < 0.4 ? 'back' : 'front';
          if (lay !== layer || nu >= 1) continue;
          needle(ctx, XS + Math.cos(ang) * l0, YS + Math.sin(ang) * l0, XS + Math.cos(ang) * l1, YS + Math.sin(ang) * l1, 3.6 * s * (1 - nu * 0.6), i % 3 ? bp.light : bp.core, 1 - nu, layer);
        }
        sparks(ctx, t, TB, XS, YS, layer, { n: 16, pal: bp, sp: 380 * s, seed: 12, len: 18, life: 0.55 });
        droplets(ctx, t, TB + 0.03, XS, YS, layer, { n: 12, pal: bp, sp: 220 * s, seed: 31 });
        dust(ctx, t, TB, XS, layer, { n: 12, reach: 110 * s, sc: 1.2 * s, seed: 7 });
        if (P.mode === 'tank') { cracks(ctx, t, TB, XS, layer, { n: 8, len: 110, pal: bp }); debris(ctx, t, TB, XS, layer, { n: 12, sp: 340, seed: 14 }); }
        if (P.mode === 'fire') {                           // llamas que se abren por el suelo y brasas que suben
          for (let i = 0; i < 14; i++) {
            const Rf = rand(i * 5 + 2), side = i % 2 ? 1 : -1, d = (14 + Rf() * 120) * s, tOn = TB + d / (500 * s), lf = 0.8 + Rf() * 0.35;
            const a = seg(t, tOn, tOn + 0.08) * (1 - seg(t, tOn + lf * 0.7, tOn + lf)), lay = Rf() < 0.45 ? 'back' : 'front';
            if (lay === layer) flame(ctx, XS + side * d, GROUND + 1, (22 + Rf() * 22) * a, (6 + Rf() * 5) * Math.max(0.3, a), t, i, a, layer);
          }
          for (let i = 0; i < 16; i++) {
            const Rf = rand(i * 3 + 90), lf = 0.7 + Rf() * 0.6, tt = u - Rf() * 0.3; if (tt < 0 || tt > lf || (Rf() < 0.5 ? 'back' : 'front') !== layer) continue;
            disc(ctx, XS + (Rf() - 0.5) * 160 * s + Math.sin(tt * 8 + i) * 8, GROUND - 10 - tt * (60 + Rf() * 80), 1.6 + Rf() * 1.6, i % 2 ? PAL.fire.light : PAL.fire.mid, 1 - tt / lf, layer);
          }
        }
        if (P.mode === 'tank' && fly) { const p = enemyPos(TSL, fly); groundImpact(ctx, t, layer, TSL, p.x, 0.7, PAL.void, { seed: 8 }); }
      }
      // catapulta
      for (const c of cat) {
        if (t < TF) continue;
        const p = catPos(c, t); if (p.x < -40 || p.x > W + 40 || p.y < -40) continue;
        if (layer === 'back' || t >= TB || t < c.tCap) {
          const pts = trailPts(tt => catPos(c, tt), t, t >= TB ? 0.05 : 0.05, 8, Math.max(TF, t >= TB ? TB : t < c.tCap ? TF : c.tCap));
          if (layer === 'back') { ribbon(ctx, pts, q => 7 * Math.pow(1 - q, 1.2), PAL.set.dark, 0.35, layer); ribbon(ctx, pts, q => 3.5 * (1 - q), PAL.set.light, 0.9, layer); }
        }
        const behind = t >= c.tCap && t < TB && Math.sin(p.th) < 0;
        if (behind) { if (layer === 'back') { lens(ctx, p.x, p.y, p.a, 22, 4, PAL.set.mid, 0.8, layer); lens(ctx, p.x, p.y, p.a, 14, 2, PAL.set.core, 0.9, layer); } }
        else { crystalShard(ctx, p.x, p.y, p.a, 1, PAL.set, 1, layer); if (t >= c.tCap && t < TB && layer === 'front') star4(ctx, p.x, p.y, 8, t * 9, '#ffffff', 0.9, layer); }
      }
      if (P.mode === 'catapult') {
        for (const [i, ex] of [[0, 0], [1, 1]]) { const tH = TB + (i ? 0.1 : 0.06), p = enemyPos(tH, enemies[ex]); impact(ctx, t, layer, tH, p.x + (i ? -20 : 20), p.y, 0.55, PAL.set, { dir: i ? Math.PI : 0, seed: 40 + i }); }
      }
    }
    const pose = t => {
      const p = poseCast(t, 0.04, TR, { lean: 0.1, kick: 0.16 });
      if (P.mode === 'catapult' && t > TA - 0.1 && t < TF + 0.2) Object.assign(p, poseCast(t, TA - 0.1, TF, { lean: 0.05, kick: 0.1 }));
      return p;
    };
    return { total, draw, enemies, playerPose: pose, shakes, frames, sfx };
  }

  addAbility({
    id: 'sing', num: 2, name: 'Singularidad', short: 'Singularidad',
    pitch: 'Orbe lento de vacío que avanza, se detiene y absorbe: los enemigos cercanos son arrastrados hacia él durante casi un segundo y, al colapsar, salen despedidos con un estallido. Control de masas: junta enemigos para otros ataques.',
    fit: 'Arma de carga (soltar = lanza el orbe) o habilidad con recarga larga. Su valor real está en combinarla: es la que más se presta a sinergias entre armas.',
    ideas: [
      '<b>Eco</b> (Reset): al colapsar deja una réplica pequeña que vuelve a atraer y estallar una vez.',
      '<b>Horizonte</b>: los proyectiles enemigos que entran en ella se destruyen (escudo ofensivo).',
      '<b>Hielo</b>: al colapsar congela a todos los atrapados juntos (un bloque grande) → reacción añicos con cualquier golpe fuerte.',
      '<b>Imán de oro</b> (Oro): atrae también monedas y orbes de vida del suelo.',
      '<b>Singularidad portátil</b>: en vez de lanzarse, orbita alrededor del player y atrae suavemente (pasiva).',
    ],
    clips: [
      { title: '2 · Base', tag: 'Base', text: 'El orbe sale con una estela de distorsión y se detiene entre los dos enemigos. Disco de acreción girando (mitad detrás, mitad delante), partículas en espiral, anillos de succión y polvo del suelo arrastrado. Los enemigos se deslizan hacia él; colapsa (líneas que convergen) y estalla con onda de hebras, agujas y polvo.', P: {} },
      { title: '2 · Fuego → estallido ígneo', tag: 'Fuego · Sistema nuevo: reacciones', text: 'El colapso libera fuego: el estallido es naranja, se abren llamas por el suelo y suben brasas. Los atrapados quedan ardiendo. Reacción «agrupados + fuego = quemadura en área».', P: { burst: 'fire', mode: 'fire' } },
      { title: '2 · Masa', tag: 'Tank', text: 'Más grande, más lenta y dura más: también arranca del aire al volador y, al estallar, lo estampa contra el suelo (impacto con polvo). El suelo se agrieta. Para builds de «pocos golpes muy pesados».', P: { mode: 'tank', sc: 1.4, dur: 1.1 } },
      { title: '2 · Catapulta (con Abanico)', tag: 'Sistema nuevo: sinergia entre armas', text: 'Sinergia de dos armas: mientras atrae, disparas el Abanico dentro. Las esquirlas quedan orbitando (por detrás y por delante) y, al colapsar, salen despedidas en todas direcciones golpeando a los dos enemigos. Ejemplo del sistema «segunda arma»: un arma prepara, la otra remata.', P: { mode: 'catapult' } },
    ],
    build,
  });
})();
