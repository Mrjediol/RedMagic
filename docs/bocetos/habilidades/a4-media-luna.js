// a4-media-luna.js — 4 · Media luna: hoja de energía giratoria que sale, se frena, vuelve (golpea dos veces) y se atrapa.
'use strict';
(function () {
  const G = '4 · Media luna';
  const S = {
    throw: sound('moon_throw', G, 'Lanzamiento', k => { const t = nowT(); vNoise(t, 0.18 * k, 0.28, 'bandpass', 2600, 900, 1.3, { att: 0.005, wet: 0.12, pan: [-0.4, 0.4] }); vRing(t, [1760, 2640, 3960], 0.25 * k, 0.04, 0.2); vThump(t, 150, 70, 0.1 * k, 0.3, 1.5); }),
    spin: (k, dur, f0, f1, pan) => { const t = nowT(); vNoise(t, dur * k, 0.16, 'bandpass', f0, f1, 2.2, { hold: true, att: 0.04, lfo: [15, 0.9], wet: 0.12, pan }); },
    hit: sound('moon_hit', G, 'Tajo (golpe)', k => { const t = nowT(); vNoise(t, 0.12 * k, 0.35, 'highpass', 3500, 8000, 1, { att: 0.001, wet: 0.15 }); vRing(t, [2093, 3136, 4699], 0.3 * k, 0.05, 0.25); vThump(t, 140, 55, 0.16 * k, 0.5, 2); }),
    catch: sound('moon_catch', G, 'Atrapar', k => { const t = nowT(); vThump(t, 120, 60, 0.12 * k, 0.4, 1.5); vNoise(t, 0.03 * k, 0.2, 'bandpass', 2400, 2400, 5, { att: 0.001 }); vTone(t + 0.02 * k, 1568, 1568, 0.3 * k, 0.04, 'sine', 0.3); }),
    orbit: sound('moon_orbit', G, 'Órbita alrededor del player', k => { const t = nowT(); vNoise(t, 0.45 * k, 0.22, 'bandpass', 1200, 2200, 2, { hold: true, att: 0.03, lfo: [16, 0.9], wet: 0.12, pan: [0.6, -0.6] }); }),
    ignite: sound('moon_fire', G, 'Fuego · estela ardiendo', k => SFXLIB.burn(k, 1.1)),
  };
  sound('moon_spin', G, 'Giro (ida)', k => S.spin(k, 0.55, 2600, 900, [-0.2, 0.5]));

  const TR = 0.28, XA = 575;

  // media luna centrada en (x,y), girada rot; R = radio exterior
  function crescentPath(ctx, R, th) { ctx.beginPath(); ctx.arc(0, 0, R, -1.95, 1.95); ctx.arc(R * th, 0, R * (1 - th * 0.55), 1.75, -1.75, true); ctx.closePath(); }
  function crescent(ctx, x, y, R, rot, pal, a, layer) {
    if (a <= 0) return;
    glow(ctx, x, y, R * 2.2, pal.g1, 0.45 * a);
    for (const [k, th, c, aa] of [[1.18, 0.32, pal.dark, 0.45], [1.0, 0.36, pal.deep, 0.9], [0.92, 0.42, pal.mid, 1], [0.86, 0.5, pal.light, 1]]) {
      ctx.save(); ctx.globalAlpha = clamp(a * aa); ctx.translate(x, y); ctx.rotate(rot); ctx.fillStyle = col(c, layer); crescentPath(ctx, R * k, th); ctx.fill(); ctx.restore();
    }
    ctx.save(); ctx.globalAlpha = clamp(a); ctx.translate(x, y); ctx.rotate(rot); ctx.strokeStyle = col(pal.core, layer); ctx.lineWidth = 2; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.arc(0, 0, R * 0.93, -1.6, 1.6); ctx.stroke(); ctx.restore();
  }

  function build(P) {
    P = Object.assign({ pal: 'set', mode: 'base' }, P);
    const pal = PAL[P.pal], R0 = 21;
    const TO = TR + 0.55, TH = TO + 0.14, TBK = TH + 0.48;          // fin ida, fin de la pausa, vuelta
    const ORB = P.mode === 'orbit', TOE = TBK + 0.42;                // órbita alrededor del player
    const TCATCH = ORB ? TOE : TBK;
    // trayectorias (k = índice de hoja para la doble)
    const pathOf = k => {
      const hi = k === 1 ? 125 : 14;
      return t => {
        if (t <= TO) { const u = seg(t, TR, TO); return { x: lerp(O.x + 8, XA, easeOut(u)), y: HY - hi * Math.sin(Math.PI * u) - (k === 1 ? 0 : 0) }; }
        if (t <= TH) return { x: XA + Math.sin((t - TO) * 12) * 2, y: HY + Math.sin((t - TO) * 9) * 2 };
        if (t <= TBK) {
          const u = easeIn(seg(t, TH, TBK)), ex = ORB ? PX + 64 : O.x + 4;
          return { x: lerp(XA, ex, u), y: HY - (k === 1 ? hi * Math.sin(Math.PI * u) : 6 * Math.sin(Math.PI * u)) + (ORB ? (GROUND - 34 - HY) * u : 0) };
        }
        if (ORB && t <= TOE) { const a = seg(t, TBK, TOE) * TAU; return { x: PX + Math.cos(a) * 64, y: GROUND - 34 + Math.sin(-a) * -22, depth: Math.sin(a) }; }
        return { x: O.x + 4, y: HY };
      };
    };
    const blades = P.mode === 'twin' ? [0, 1] : [0];
    const paths = blades.map(pathOf);
    const spin = t => t * (t < TH ? 27 : 31);

    const E1 = { x: 400, hits: [] }, E2 = { x: 548, hits: [] }, enemies = [E1, E2];
    let E3 = null, E0 = null;
    if (P.mode === 'twin') { E3 = { x: 470, cy: 168, hits: [] }; enemies.push(E3); }
    if (ORB) { E0 = { x: 0, hits: [], walk: t => -24 + 52 * easeOut(seg(t, 0, 1.4)) }; enemies.push(E0); }
    // golpes: cruces de cada hoja con cada enemigo (ida y vuelta)
    const hitList = [];
    paths.forEach((pf, bi) => {
      for (const E of enemies) {
        let inside = false;
        for (let t = TR; t < TCATCH; t += 0.003) {
          const p = pf(t), q = enemyPos(t, E), rx = (E.cy !== undefined ? 24 : 30) + R0 * 0.6, ry = (E.cy !== undefined ? 30 : 42) + R0 * 0.5;
          const now = Math.pow((p.x - q.x) / rx, 2) + Math.pow((p.y - q.y) / ry, 2) < 1;
          if (now && !inside) { const back = t > TH; hitList.push({ t, E, bi, back }); E.hits.push({ t, k: (back || q.x < PX + 40) ? -11 : 11, fl: 0.16, shake: 4, sq: 0.1 }); }
          inside = now;
        }
      }
    });
    const sfx = [{ t: 0.02, f: k => SFXLIB.castShort(k * 0.8) }, { t: TR, f: S.throw }, { t: TR + 0.02, f: k => S.spin(k, TO - TR, 2600, 900, [-0.2, 0.5]) },
                 { t: TH, f: k => S.spin(k, TBK - TH, 900, 2600, [0.5, -0.2]) }, { t: TCATCH, f: S.catch }];
    for (const h of hitList) sfx.push({ t: h.t, f: S.hit });
    if (ORB) sfx.push({ t: TBK, f: S.orbit });
    if (P.mode === 'fire') sfx.push({ t: TR + 0.15, f: S.ignite });
    const shakes = hitList.map(h => ({ t: h.t, a: 3, d: 0.22 })), frames = hitList.filter(h => !h.back).slice(0, 1).map(h => ({ t: h.t, d: 0.04 }));
    if (P.mode === 'fire') { const h = hitList.find(q => q.E === E1); if (h) { E1.burn = t => t < h.t ? 0 : 1 - seg(t, h.t + 1.0, h.t + 1.3); E1.tint = t => ['#ff7a2e', t < h.t ? 0 : (0.22 + 0.1 * Math.sin(t * 30)) * (1 - seg(t, h.t + 1.0, h.t + 1.3))]; } }
    const total = TCATCH + 0.7;

    function drawBlade(ctx, t, layer, pf, bi) {
      if (t < TR || t > TCATCH) return;
      const p = pf(t), rot = spin(t) + bi * 1.3;
      const behind = ORB && t > TBK && p.depth !== undefined && p.depth < 0;   // mitad trasera de la órbita: detrás del player
      const lay = behind ? 'back' : 'front';
      // estelas de las dos puntas (espirales afiladas)
      for (const sgn of [-1, 1]) {
        const pts = [];
        for (let i = 0; i <= 12; i++) {
          const tt = Math.max(TR, t - 0.075 * i / 12), q = pf(tt), r = spin(tt) + bi * 1.3 + sgn * 1.9;
          pts.push({ x: q.x + Math.cos(r) * R0, y: q.y + Math.sin(r) * R0 });
        }
        if (layer === 'back') { ribbon(ctx, pts, s => 6 * Math.pow(1 - s, 1.3), pal.dark, 0.35, layer); ribbon(ctx, pts, s => 3.2 * (1 - s), pal.mid, 0.85, layer); ribbon(ctx, pts, s => 1.3 * (1 - s), pal.core, 1, layer); }
      }
      // estela de posición (afterimages)
      if (layer === lay) {
        for (let k = 3; k >= 1; k--) { const tt = t - k * 0.022; if (tt > TR) { const q = pf(tt); crescent(ctx, q.x, q.y, R0 * (1 - k * 0.06), spin(tt) + bi * 1.3, pal, 0.18 / k + 0.06, layer); } }
        for (let k = 1; k <= 2; k++) crescent(ctx, p.x, p.y, R0, rot - k * 0.32, pal, 0.22 / k, layer);      // desenfoque de giro
        crescent(ctx, p.x, p.y, R0, rot, pal, 1, layer);
      }
    }

    function draw(ctx, t, layer) {
      castFx(ctx, t, layer, 0.04, TR, pal, 0.9);
      // estela de fuego que queda colgada en el aire
      if (P.mode === 'fire') {
        for (let tt = TR + 0.03; tt < Math.min(t, TCATCH); tt += 0.028) {
          const age = t - tt, lf = 0.75; if (age > lf) continue;
          const q = paths[0](tt), Rr = rand(Math.floor(tt * 1000)), a = (1 - age / lf) * seg(age, 0, 0.05);
          const lay = Rr() < 0.5 ? 'back' : 'front'; if (lay !== layer) continue;
          flame(ctx, q.x + (Rr() - 0.5) * 10, q.y + 10, (16 + Rr() * 12) * a, (5 + Rr() * 3) * Math.max(0.4, a), t, tt * 100, a, layer);
          if (Rr() < 0.4) disc(ctx, q.x + (Rr() - 0.5) * 20, q.y - age * 40, 1.6, PAL.fire.light, a, layer);
        }
      }
      paths.forEach((pf, bi) => drawBlade(ctx, t, layer, pf, bi));
      // tajos al golpear: línea diagonal que se abre y se afina + chispas en la dirección del corte
      for (const h of hitList) {
        const u = seg(t, h.t, h.t + 0.22); if (u <= 0 || u >= 1) continue;
        const q = enemyPos(t, h.E), dir = h.back ? -1 : 1, len = 70 * easeOut(seg(u, 0, 0.35)), a = 1 - seg(u, 0.4, 1), ang = dir * -0.55 + (h.bi ? 0.6 : 0);
        if (layer === 'front') {
          needle(ctx, q.x - Math.cos(ang) * len / 2, q.y - Math.sin(ang) * len / 2, q.x + Math.cos(ang) * len / 2, q.y + Math.sin(ang) * len / 2, 9 * (1 - u * 0.7), pal.mid, a, layer);
          needle(ctx, q.x - Math.cos(ang) * len / 2, q.y - Math.sin(ang) * len / 2, q.x + Math.cos(ang) * len / 2, q.y + Math.sin(ang) * len / 2, 3.5 * (1 - u * 0.6), pal.core, a, layer);
          glow(ctx, q.x, q.y, 40, pal.g1, 0.6 * (1 - u));
        }
        sparks(ctx, t, h.t, q.x, q.y, layer, { n: 9, a0: (h.back ? Math.PI : 0) - 0.7, a1: (h.back ? Math.PI : 0) + 0.7, sp: 300, pal, seed: Math.floor(h.t * 1000), len: 14, life: 0.35 });
        droplets(ctx, t, h.t, q.x, q.y, layer, { n: 5, pal, seed: Math.floor(h.t * 997), sp: 120 });
      }
      // atrapar
      if (layer === 'front') { const u = seg(t, TCATCH, TCATCH + 0.22); if (u > 0 && u < 1) { glow(ctx, O.x, O.y, 34, pal.g2, 0.8 * (1 - u)); star4(ctx, O.x, O.y, 20 * (1 + u * 0.4), 0.6, pal.core, 1 - u, layer); ellArc(ctx, O.x, O.y, 10 + 24 * easeOut(u), 10 + 24 * easeOut(u), 0, TAU, 2, pal.light, 1 - u, layer, { taper: 0 }); } }
    }
    const pose = t => {
      const p = poseCast(t, 0.04, TR, { lean: 0.12, kick: 0.2 });
      const cu = seg(t, TCATCH, TCATCH + 0.2); if (cu > 0 && cu < 1) { p.squash = 0.06 * Math.sin(Math.PI * cu); p.lean = -0.08 * Math.sin(Math.PI * cu); }
      return p;
    };
    return { total, draw, enemies, playerPose: pose, shakes, frames, sfx };
  }

  addAbility({
    id: 'moon', num: 4, name: 'Media luna', short: 'Media luna',
    pitch: 'Bumerán de energía: la hoja sale girando, se frena al final del recorrido, vuelve y se atrapa. Golpea a la ida y a la vuelta, así que premia colocarse para que el enemigo quede en medio. Mientras está fuera no puedes volver a lanzarla.',
    fit: 'Arma con «munición» de una sola hoja: el ritmo lo marca la vuelta, no una recarga. Abre la familia de items de «retorno» (efectos al atrapar).',
    ideas: [
      '<b>Atrapada perfecta</b> (Sistema nuevo: timing): atraparla moviéndote hacia ella da un tajo-onda gratis.',
      '<b>Pausa larga</b>: se queda girando en el extremo más tiempo, golpeando en área (sierra).',
      '<b>Hielo</b>: congela a lo que atraviesa a la ida; a la vuelta lo rompe (reacción añicos con un solo lanzamiento).',
      '<b>Imán</b>: a la vuelta trae monedas y orbes de vida.',
      '<b>Sinergia con Singularidad</b>: si pasa por una singularidad, queda orbitándola como una sierra hasta el colapso.',
    ],
    clips: [
      { title: '4 · Base', tag: 'Base', text: 'Hoja en media luna con capas de color, desenfoque de giro, copias que se quedan atrás y dos estelas en espiral desde las puntas. Corta al enemigo a la ida (tajo diagonal, chispas), se frena junto al segundo, vuelve, corta otra vez en sentido contrario y se atrapa con un destello.', P: {} },
      { title: '4 · Doble', tag: 'Oro · Sistema nuevo: copias', text: 'Salen dos hojas doradas: una baja y otra en arco alto que corta al volador a la ida y a la vuelta. Cubre arriba y abajo a la vez.', P: { pal: 'gold', mode: 'twin' } },
      { title: '4 · Órbita', tag: 'Haste · Sistema nuevo: retorno', text: 'Al volver no se atrapa en seguida: da una vuelta completa alrededor del player (por detrás y por delante de él) y corta al enemigo que se acercaba por la espalda. La misma hoja ataca y defiende.', P: { mode: 'orbit' } },
      { title: '4 · Fuego', tag: 'Fuego · Sistema nuevo: estelas', text: 'La hoja deja una estela de llamas colgada en el aire durante casi un segundo; lo que atraviesa queda ardiendo. Convierte el recorrido en una zona que el enemigo evita.', P: { pal: 'fire', mode: 'fire' } },
    ],
    build,
  });
})();
