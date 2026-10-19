// a5-semilla.js — 5 · Semilla estelar: se lanza en parábola, se clava en el suelo, se arma y estalla en columna.
'use strict';
(function () {
  const G = '5 · Semilla estelar';
  const S = {
    lob: sound('seed_lob', G, 'Lanzamiento (parábola)', k => { const t = nowT(); vThump(t, 240, 120, 0.09 * k, 0.4, 1.2); vNoise(t, 0.03 * k, 0.2, 'bandpass', 2000, 2000, 4, { att: 0.001 }); vTone(t + 0.03 * k, 900, 1500, 0.4 * k, 0.025, 'sine', 0.2); }),
    land: sound('seed_land', G, 'Se clava', k => { const t = nowT(); vThump(t, 160, 60, 0.15 * k, 0.5, 2); vRing(t, [1480, 2220], 0.25 * k, 0.04, 0.2); vNoise(t, 0.08 * k, 0.15, 'lowpass', 1500, 300, 1); }),
    tick: (k, i) => { const t = nowT(); vNoise(t, 0.02 * k, 0.25, 'bandpass', 2600 + i * 500, 2600 + i * 500, 6, { att: 0.001 }); vTone(t, 220 * Math.pow(1.26, i), 200 * Math.pow(1.26, i), 0.12 * k, 0.12, 'sine', 0.1); },
    armed: sound('seed_armed', G, 'Proximidad · armada', k => { const t = nowT(); vTone(t, 330, 330, 0.18 * k, 0.1, 'triangle', 0.15); vTone(t + 0.12 * k, 440, 440, 0.25 * k, 0.08, 'triangle', 0.2); }),
    trigger: sound('seed_trigger', G, 'Proximidad · detecta', k => { for (let i = 0; i < 3; i++) setTimeout(() => AC && S.tick(k * 0.6, 2 + i), i * 26 * k); }),
    boom: sound('seed_boom', G, 'Estallido (columna)', k => { SFXLIB.boom(k, 1); const t = nowT(); for (let i = 0; i < 6; i++) vNoise(t + (0.15 + i * 0.06 + Math.random() * 0.04) * k, 0.03 * k, 0.12, 'lowpass', 1800, 900, 1, { att: 0.001 }); }),
    mini: sound('seed_mini', G, 'Racimo · mini estallido', k => SFXLIB.boom(k * 0.7, 0.5)),
    split: sound('seed_split', G, 'Racimo · se divide', k => { const t = nowT(); for (let i = 0; i < 3; i++) vThump(t + i * 0.03 * k, 300 - i * 40, 150, 0.07 * k, 0.25, 1); }),
    nova: sound('seed_nova', G, 'Hielo · nova de escarcha', k => { SFXLIB.shatter(k, 1); const t = nowT(); vNoise(t, 0.6 * k, 0.25, 'highpass', 1500, 6000, 0.7, { att: 0.01, wet: 0.4 }); vThump(t, 120, 40, 0.4 * k, 0.6, 2); vRing(t + 0.1 * k, [880, 1320, 1760], 0.8 * k, 0.04, 0.5); }),
  };
  sound('seed_tick', G, 'Armado (3 tics)', k => { for (let i = 0; i < 3; i++) setTimeout(() => AC && S.tick(k, i), i * 160); });

  const TR = 0.28, TFL = 0.5;


  function build(P) {
    P = Object.assign({ pal: 'set', mode: 'base' }, P);
    const pal = PAL[P.pal], prox = P.mode === 'prox';
    const LX = prox ? 318 : 372, TL = TR + TFL;
    const pos = t => { const u = seg(t, TR, TL); return { x: lerp(O.x + 6, LX, u), y: lerp(O.y - 4, GROUND - 4, u) - 4 * 70 * u * (1 - u) }; };
    const E1 = { x: 405, hits: [] }, E2 = { x: 545, hits: [] }, enemies = [E1, E2];
    let TD, ticks;
    if (prox) {                                           // el enemigo camina hacia la semilla y la dispara al acercarse
      const spd = 70, x0 = 486; E1.walk = t => Math.max(LX + 40, x0 - spd * Math.max(0, t - 0.1));
      TD = 0.1 + (x0 - (LX + 46)) / spd + 0.1; ticks = [TL + 0.12];
    } else { TD = TL + 0.62; ticks = [TL + 0.15, TL + 0.34, TL + 0.49]; }
    const sfx = [{ t: 0.02, f: k => SFXLIB.castShort(k * 0.7) }, { t: TR, f: S.lob }, { t: TL, f: S.land }];
    ticks.forEach((tt, i) => sfx.push({ t: tt, f: prox ? S.armed : (k => S.tick(k, i)) }));
    if (prox) sfx.push({ t: TD - 0.1, f: S.trigger });
    const shakes = [{ t: TD, a: P.mode === 'cluster' ? 5 : 8, d: 0.35 }], frames = [{ t: TD, d: 0.05 }];
    sfx.push({ t: TD, f: P.mode === 'ice' ? S.nova : P.mode === 'cluster' ? S.split : S.boom });
    // golpe al enemigo cercano
    if (P.mode === 'ice') {
      E1.hits.push({ t: TD + 0.03, k: 6, fl: 0.2, shake: 4 });
      E1.frozen = t => t < TD + 0.04 ? 0 : 1 - seg(t, TD + 1.2, TD + 1.45);
      E1.tint = t => [PAL.ice.light, t < TD ? 0 : 0.45 * (1 - seg(t, TD + 1.2, TD + 1.45))];
    } else {
      E1.hits.push({ t: TD, k: P.mode === 'cluster' ? 12 : 20, fl: 0.25, shake: 6, sq: 0.2, d: 0.55 });
      if (P.mode !== 'cluster') E1.lift = t => 46 * bump(seg(t, TD, TD + 0.5));
    }
    // racimo: tres semillas pequeñas que saltan y estallan en cadena
    const kids = P.mode === 'cluster' ? [{ x: LX + 92, d: 0.3 }, { x: 540, d: 0.38 }, { x: LX - 74, d: 0.46 }] : [];
    kids.forEach((c, i) => {
      c.t1 = TD + c.d; sfx.push({ t: c.t1, f: S.mini }); shakes.push({ t: c.t1, a: 3.5, d: 0.25 });
      if (Math.abs(c.x - E2.x) < 40) E2.hits.push({ t: c.t1, k: 14, fl: 0.22, shake: 5, sq: 0.15 });
      if (Math.abs(c.x - enemyPos(c.t1, E1).x) < 40) E1.hits.push({ t: c.t1, k: 10, fl: 0.2, shake: 4 });
    });
    const kidPos = (c, t) => { const u = seg(t, TD, c.t1); return { x: lerp(LX, c.x, u), y: lerp(GROUND - 8, GROUND - 4, u) - 4 * 80 * u * (1 - u) }; };
    const total = (kids.length ? Math.max(...kids.map(c => c.t1)) : TD) + (P.mode === 'ice' ? 1.6 : 1.1);

    function draw(ctx, t, layer) {
      castFx(ctx, t, layer, 0.04, TR, pal, 0.8);
      // vuelo
      if (t > TR && t < TL) {
        const p = pos(t);
        if (layer === 'back') { const pts = trailPts(pos, t, 0.09, 10, TR); ribbon(ctx, pts, s => 8 * Math.pow(1 - s, 1.2), pal.dark, 0.35, layer); ribbon(ctx, pts, s => 4 * (1 - s), pal.light, 0.9, layer); }
        starSeed(ctx, t, p.x, p.y, t * 14, pal, 1, layer);
      }
      // clavada, armándose
      if (t >= TL && t < TD) {
        const blink = ticks.reduce((m, tt) => Math.max(m, 1 - seg(t, tt, tt + 0.16)), 0), armed = prox && t > ticks[0];
        if (layer === 'front') {
          for (let i = 0; i < 3; i++) needle(ctx, LX, GROUND - 2, LX + (i - 1) * 7, GROUND - 16 - (i === 1 ? 8 : 0) - blink * 8, 2.2, pal.light, 0.8, layer);     // rayitos hacia arriba
          starSeed(ctx, t, LX, GROUND - 5, 0.785, pal, 1, layer, 0.8 + blink * 0.35);
        }
        // zona de efecto (proximidad: siempre visible y respirando)
        if (armed) groundRing(ctx, LX, GROUND, 46, 1.4, pal.light, 0.35 + 0.15 * Math.sin(t * 8), layer, { taper: 0, seed: 2 });
        for (const tt of ticks) { const u = seg(t, tt, tt + 0.4); if (u > 0 && u < 1) groundRing(ctx, LX, GROUND, 8 + 44 * easeOut(u), 2.6 * (1 - u) + 0.4, pal.mid, 1 - u, layer, { seed: 3 }); }
        if (prox && t > TD - 0.1) { const u = seg(t, TD - 0.1, TD); if (layer === 'front') glow(ctx, LX, GROUND - 8, 40 * u, pal.g2, u); }
      }
      // aterrizaje
      dust(ctx, t, TL, LX, layer, { n: 5, reach: 22, sc: 0.6, seed: 4 });
      if (layer === 'front') { const u = seg(t, TL, TL + 0.15); if (u > 0 && u < 1) star4(ctx, LX, GROUND - 4, 18 * (1 - u * 0.5), 0, pal.core, 1 - u, layer); }
      // estallido
      if (t >= TD) {
        if (P.mode === 'ice') frostNova(ctx, t, layer, TD, LX, 130);
        else {
          const sc = P.mode === 'cluster' ? 0.7 : 1.1, u = t - TD, cu = seg(u, 0, 0.3);
          if (layer === 'front' && cu < 1) {                // columna vertical de luz que se afina
            lightColumn(ctx, LX, GROUND, u, sc, pal, layer);
          }
          groundImpact(ctx, t, layer, TD, LX, sc, pal, { seed: 3 });
          const R = rand(19);                                // agujas hacia arriba
          for (let i = 0; i < 10; i++) { const ang = -Math.PI / 2 + (R() - 0.5) * 1.3, nu = seg(u, 0, 0.28), l0 = 12 * sc, l1 = l0 + (50 + R() * 70) * sc * easeOut(nu), lay = R() < 0.4 ? 'back' : 'front'; if (lay === layer && nu < 1) needle(ctx, LX + Math.cos(ang) * l0, GROUND - 10 + Math.sin(ang) * l0, LX + Math.cos(ang) * l1, GROUND - 10 + Math.sin(ang) * l1, 3.4 * sc * (1 - nu * 0.6), i % 3 ? pal.light : pal.core, 1 - nu, layer); }
          cracks(ctx, t, TD, LX, layer, { n: 6, len: 60 * sc, pal });
        }
        for (const c of kids) {
          if (t < c.t1) { const p = kidPos(c, t); if (layer === 'back') { const pts = trailPts(q => kidPos(c, q), t, 0.07, 8, TD); ribbon(ctx, pts, s => 4 * (1 - s), pal.light, 0.9, layer); } starSeed(ctx, t, p.x, p.y, t * 18, pal, 1, layer, 0.6); }
          else groundImpact(ctx, t, layer, c.t1, c.x, 0.6, pal, { seed: Math.floor(c.x) });
        }
      }
    }
    return { total, draw, enemies, playerPose: t => poseCast(t, 0.04, TR, { lean: 0.14, kick: 0.12 }), shakes, frames, sfx };
  }

  addAbility({
    id: 'seed', num: 5, name: 'Semilla estelar', short: 'Semilla',
    pitch: 'Se lanza en parábola, se clava en el suelo, avisa con tres pulsos y estalla en una columna de luz que lanza por los aires al enemigo cercano. Golpe diferido: se coloca antes de que llegue el enemigo o se lanza por encima de un obstáculo.',
    fit: 'Arma «lanzada» (trayectoria con gravedad: no existe aún en el pipeline — sería un modificador de Trayectoria nuevo). Cualquier proyectil con esa trayectoria podría acabar como mina.',
    ideas: [
      '<b>Semillas que se acumulan</b>: hasta 3 en el suelo; un segundo botón las detona todas a la vez (Sistema nuevo: detonador).',
      '<b>Pegajosa</b>: si toca a un enemigo en el aire se le pega y estalla con él.',
      '<b>Fuego</b>: deja un charco ardiendo donde estalló.',
      '<b>Gravedad</b> (con Singularidad): al estallar atrae un instante antes de lanzar.',
      '<b>Oro</b>: cada enemigo lanzado por los aires suelta monedas al caer.',
    ],
    clips: [
      { title: '5 · Base', tag: 'Base', text: 'Estrella pequeña que gira en parábola con estela, se clava (polvo y destello), tres pulsos de aviso cada vez más seguidos y estallido: columna vertical de luz, anillo en el suelo, agujas hacia arriba, grietas, polvo y escombros. El enemigo sale volando un poco.', P: {} },
      { title: '5 · Racimo', tag: 'Sistema nuevo: forma «racimo»', text: 'El estallido es menor pero suelta tres semillas pequeñas que saltan y estallan en cadena (una cae junto al segundo enemigo). Pasa de un golpe a una zona.', P: { mode: 'cluster' } },
      { title: '5 · Hielo → nova', tag: 'Hielo · Sistema nuevo: reacciones', text: 'En vez de columna, nova de escarcha: picos de hielo brotan del suelo desde la semilla hacia fuera y el enemigo queda congelado entero más de un segundo (listo para la reacción añicos de otra arma).', P: { pal: 'ice', mode: 'ice' } },
      { title: '5 · Proximidad', tag: 'Haste · Sistema nuevo: trampas', text: 'Se arma al instante y espera: la zona de efecto queda marcada en el suelo. El enemigo camina hacia ella; al entrar, tres tics rápidos y estalla. Se usa para preparar el terreno antes del combate.', P: { mode: 'prox' } },
    ],
    build,
  });
})();
