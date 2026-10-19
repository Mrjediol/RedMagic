// a6-golpe-estelar.js — 6 · Golpe estelar: el cristal marca un punto del suelo y, tras un aviso, cae una lanza de luz del cielo.
'use strict';
(function () {
  const G = '6 · Golpe estelar';
  const S = {
    up: sound('star_up', G, 'Señal hacia el cielo', k => { const t = nowT(); vNoise(t, 0.3 * k, 0.2, 'bandpass', 600, 5000, 1.2, { att: 0.01, wet: 0.3 }); vRise(t, 'triangle', 300, 1200, 0.25 * k, 0.06, 800, 4000); }),
    mark: sound('star_mark', G, 'Marca en el suelo', k => { const t = nowT(); vThump(t, 90, 60, 0.25 * k, 0.4, 1.2, 0.2); vRing(t, [440, 660, 990], 0.6 * k, 0.04, 0.5); }),
    tension: (k, dur) => { const t = nowT(); vNoise(t, dur * k, 0.12, 'bandpass', 400, 2400, 2, { rising: true, wet: 0.3 }); vSaw(t, 55, 70, dur * k, 0.06, 200, 700, 1, 0.2, { rising: true }); },
    fall: sound('star_fall', G, 'Caída (silbido)', k => { const t = nowT(); vTone(t, 2800, 520, 0.42 * k, 0.07, 'sine', 0.2, 0.05); vNoise(t, 0.42 * k, 0.16, 'highpass', 6000, 1500, 1, { rising: true, wet: 0.2 }); }),
    impact: sound('star_impact', G, 'Impacto de la lanza', k => { SFXLIB.boom(k, 1.15); const t = nowT(); vNoise(t, 0.04 * k, 0.4, 'highpass', 3000, 3000, 1, { att: 0.001 }); vRing(t, [110, 165, 247, 330], 1.0 * k, 0.05, 0.5); }),
    needle: sound('star_needle', G, 'Lluvia · cada aguja', k => { const t = nowT(); vThump(t, 160, 70, 0.12 * k, 0.32, 2); vNoise(t, 0.06 * k, 0.22, 'highpass', 4000, 9000, 1, { att: 0.001 }); vRing(t, [2400, 3600], 0.15 * k, 0.03, 0.2); }),
    markOn: sound('star_markon', G, 'Marcas · enemigo marcado', k => { const t = nowT(); vTone(t, 660, 660, 0.2 * k, 0.05, 'triangle', 0.2); vNoise(t, 0.03 * k, 0.12, 'bandpass', 3000, 3000, 6, { att: 0.001 }); }),
    burn: k => SFXLIB.burn(k, 1.3),
  };

  const TUP = 0.38;


  function build(P) {
    P = Object.assign({ pal: 'set', mode: 'base' }, P);
    const pal = PAL[P.pal];
    const E1 = { x: 400, hits: [] }, E2 = { x: 522, hits: [] }, enemies = [E1, E2];
    let E3 = null;
    if (P.mode === 'marks' || P.mode === 'rain') { E3 = { x: P.mode === 'rain' ? 575 : 462, cy: 160, hits: [] }; enemies.push(E3); }
    const sfx = [{ t: 0.04, f: k => SFXLIB.castShort(k * 1.3) }, { t: TUP, f: S.up }];
    const shakes = [], frames = [];
    // golpes: lista de { x, tFall, target? }
    let strikes = [];
    if (P.mode === 'marks') strikes = [{ E: E1, t: 0.95 }, { E: E3, t: 1.1 }, { E: E2, t: 1.25 }];
    else if (P.mode === 'rain') strikes = [];
    else strikes = [{ E: E2, t: 1.02 }];
    const FALL = 0.07;
    for (const s of strikes) {
      s.x = enemyPos(s.t, s.E).x; s.hy = s.E.cy !== undefined ? s.E.cy + 6 : GROUND; s.ti = s.t + FALL * (s.hy / GROUND);
      s.E.hits.push({ t: s.ti, k: s.E.cy !== undefined ? 6 : 4, fl: 0.3, shake: 7, sq: 0.34, d: 0.5 });
      if (s.E.cy !== undefined) s.E.lift = t => -40 * easeOut(seg(t, s.ti, s.ti + 0.12)) * (1 - seg(t, s.ti + 0.5, s.ti + 1.1));
      sfx.push({ t: s.t - 0.42, f: S.fall }, { t: s.ti, f: S.impact });
      shakes.push({ t: s.ti, a: P.mode === 'marks' ? 7 : 10, d: 0.4 }); frames.push({ t: s.ti, d: 0.06 });
    }
    if (P.mode !== 'marks') { sfx.push({ t: 0.42, f: S.mark }, { t: 0.45, f: k => S.tension(k, 0.55) }); E1.hits.push({ t: 1.02 + FALL + 0.05, k: -7, fl: 0.1, shake: 3 }); }
    // marcas: los tres enemigos ya estaban marcados por otras armas
    const markT = [0.08, 0.16, 0.24];
    if (P.mode === 'marks') markT.forEach((tt, i) => sfx.push({ t: tt, f: S.markOn }));
    // lluvia: agujas doradas en diagonal sobre la zona
    const needles = [];
    if (P.mode === 'rain') {
      for (let i = 0; i < 15; i++) {
        const R = rand(i + 900), tx = 445 + ((i * 7) % 15) * 11 + (R() - 0.5) * 10, t1 = 0.98 + i * 0.035 + R() * 0.02;
        const hitE = [E2, E3].find(E => { const q = enemyPos(t1, E); return Math.abs(q.x - tx) < 26 && (E.cy === undefined || true); });
        needles.push({ tx, t1, t0: t1 - 0.12, hitE });
        sfx.push({ t: t1, f: S.needle });
        if (hitE) hitE.hits.push({ t: t1, k: 4, fl: 0.12, shake: 4, sq: 0.1 });
      }
      shakes.push({ t: 1.0, a: 4, d: 0.6 });
    }
    const lastT = strikes.length ? Math.max(...strikes.map(s => s.ti)) : Math.max(...needles.map(n => n.t1));
    const fire = P.mode === 'fire';
    if (fire) { E2.burn = t => t < strikes[0].ti ? 0 : 1 - seg(t, strikes[0].ti + 1.2, strikes[0].ti + 1.5); sfx.push({ t: strikes[0].ti + 0.05, f: S.burn }); }
    const total = lastT + (fire ? 1.6 : 1.1);
    const markX = P.mode === 'rain' ? 520 : E2.x;

    function draw(ctx, t, layer) {
      castFx(ctx, t, layer, 0.04, TUP, pal, 1.2, { x: PX + 16, y: GROUND - 74, dir: -Math.PI / 2 });
      // señal hacia el cielo
      if (t > TUP && t < TUP + 0.3 && layer === 'front') {
        const u = seg(t, TUP, TUP + 0.3), top = lerp(GROUND - 74, -20, easeOut(seg(u, 0, 0.4)));
        strokeTaper(ctx, [{ x: PX + 16, y: top }, { x: PX + 16, y: GROUND - 74 }], 6 * (1 - u), pal.light, 1 - u, layer, false);
        strokeTaper(ctx, [{ x: PX + 16, y: top }, { x: PX + 16, y: GROUND - 74 }], 2.4 * (1 - u), pal.core, 1 - u, layer, false);
      }
      // marca y aviso
      if (P.mode !== 'marks') {
        const a = easeOut(seg(t, 0.42, 0.55)) * (1 - seg(t, 1.02, 1.2)), r = lerp(P.mode === 'rain' ? 110 : 74, P.mode === 'rain' ? 88 : 46, easeInOut(seg(t, 0.42, 1.0)));
        runeCircle(ctx, t, layer, markX, r, a, pal);
        if (layer === 'front' && t > 0.45 && t < 1.05) { const k = seg(t, 0.45, 1.0); star4(ctx, markX + (P.mode === 'rain' ? -90 : 0), 14, (8 + 16 * k) * (0.8 + 0.2 * Math.sin(t * 40)), t, pal.core, a, layer); glow(ctx, markX + (P.mode === 'rain' ? -90 : 0), 14, 40 * k, pal.g2, a * k); }
      } else {
        for (let i = 0; i < 3; i++) {                     // marca sobre cada enemigo: rombo que gira con corchetes
          const E = [E1, E3, E2][i], s = strikes[i], a = easeOut(seg(t, markT[i], markT[i] + 0.12)) * (1 - seg(t, s.ti, s.ti + 0.1));
          if (a <= 0 || layer !== 'front') continue;
          const q = enemyPos(t, E), y = q.y - (E.cy !== undefined ? 46 : 62), sp = 1 + 0.3 * (1 - seg(t, markT[i], markT[i] + 0.12));
          ctx.save(); ctx.translate(q.x, y); ctx.rotate(t * 2); ctx.globalAlpha = a;
          for (const [r, c] of [[9 * sp, pal.mid], [5.5 * sp, pal.core]]) { ctx.fillStyle = col(c, layer); ctx.beginPath(); ctx.moveTo(0, -r); ctx.lineTo(r * 0.7, 0); ctx.lineTo(0, r); ctx.lineTo(-r * 0.7, 0); ctx.closePath(); ctx.fill(); }
          ctx.restore();
          glow(ctx, q.x, y, 22, pal.g2, 0.6 * a);
          for (const sx of [-1, 1]) for (const sy of [-1, 1]) { const bx = q.x + sx * 34 * sp, by = q.y + sy * 44 * sp; strokeTaper(ctx, [{ x: bx, y: by }, { x: bx - sx * 10, y: by }], 2.4, pal.light, a, layer, false); strokeTaper(ctx, [{ x: bx, y: by }, { x: bx, y: by - sy * 10 }], 2.4, pal.light, a, layer, false); }
          if (t > s.t - 0.4 && t < s.t) { const k = seg(t, s.t - 0.4, s.t); star4(ctx, s.x, 12, 10 + 14 * k, t, pal.core, k, layer); }
        }
      }
      // lanzas
      for (const s of strikes) {
        if (t < s.t) continue;
        const fallU = seg(t, s.t, s.ti), hy = lerp(-30, s.hy, easeIn(fallU)), after = seg(t, s.ti + 0.12, s.ti + 0.3), w = 18 * (1 - after);
        if (after < 1) starLance(ctx, t, layer, s.x, hy, w, 1 - after * after, pal);
        if (t < s.ti && layer === 'front') for (let i = 0; i < 6; i++) { const R = rand(i + 70), ox = (R() - 0.5) * 60, l = 40 + R() * 50, y = lerp(-60, s.hy, easeIn(seg(t, s.t - 0.02 * i, s.ti))); needle(ctx, s.x + ox, y - l, s.x + ox, y, 2, pal.light, 0.7, layer); }
        if (t >= s.ti) {
          if (s.E.cy === undefined) { groundImpact(ctx, t, layer, s.ti, s.x, 1.35, pal, { seed: Math.floor(s.x) }); cracks(ctx, t, s.ti, s.x, layer, { n: 8, len: 110, pal }); }
          else impact(ctx, t, layer, s.ti, s.x, s.hy, 1.3, pal, { dir: -Math.PI / 2, spread: TAU, seed: 8, needles: 14 });
          if (layer === 'front') { const u = seg(t, s.ti, s.ti + 0.2); if (u < 1) { glow(ctx, s.x, s.hy - 30, 150, pal.g1, 0.7 * (1 - u)); } }
        }
      }
      // cráter ardiendo
      if (fire && t >= strikes[0].ti) {
        const s = strikes[0];
        for (let i = 0; i < 9; i++) {
          const R = rand(i + 40), d = (R() - 0.5) * 120, on = seg(t, s.ti + Math.abs(d) / 400, s.ti + Math.abs(d) / 400 + 0.1) * (1 - seg(t, s.ti + 1.1 + R() * 0.2, s.ti + 1.45)), lay = R() < 0.45 ? 'back' : 'front';
          if (lay === layer) flame(ctx, s.x + d, GROUND + 2, (24 + R() * 22) * on, (7 + R() * 5) * Math.max(0.3, on), t, i, on, layer);
        }
        if (layer === 'back') { const a = (1 - seg(t, s.ti + 1.1, s.ti + 1.5)) * 0.7; ctx.save(); ctx.globalAlpha = a; ctx.fillStyle = col('#3a1208', layer); ctx.beginPath(); ctx.ellipse(s.x, GROUND + 4, 70, 9, 0, 0, TAU); ctx.fill(); ctx.restore(); groundRing(ctx, s.x, GROUND + 3, 66, 2.4, PAL.fire.mid, a, layer, { taper: 0, wob: 0.1, seed: 9 }); }
      }
      // lluvia de agujas
      for (const n of needles) {
        const ang = Math.PI / 2 - 0.42, L = 420, x0 = n.tx - Math.cos(ang) * L, y0 = GROUND - Math.sin(ang) * L;
        const hy = n.hitE && n.hitE.cy !== undefined ? n.hitE.cy + 4 : GROUND - 2, endU = (hy - y0) / (GROUND - y0);
        if (t >= n.t0 && t < n.t1) {
          const u = seg(t, n.t0, n.t1) * endU, x = lerp(x0, n.tx, u), y = lerp(y0, GROUND, u);
          rainNeedle(ctx, x, y, ang, pal, layer);
        }
        if (t >= n.t1) {
          const ix = lerp(x0, n.tx, endU), iy = lerp(y0, GROUND, endU);
          if (n.hitE && n.hitE.cy !== undefined) impact(ctx, t, layer, n.t1, ix, iy, 0.7, pal, { dir: -ang, seed: Math.floor(n.tx) });
          else { groundImpact(ctx, t, layer, n.t1, n.tx, 0.6, pal, { seed: Math.floor(n.tx), debris: false }); if (layer === 'front' && t < n.t1 + 0.5) lens(ctx, n.tx - Math.cos(ang) * 8, GROUND - Math.sin(ang) * 8, ang, 22, 3, pal.light, 1 - seg(t, n.t1 + 0.2, n.t1 + 0.5), layer); }
        }
      }
    }
    const pose = t => {
      const prep = easeOut(seg(t, 0.04, TUP)), rec = seg(t, TUP, TUP + 0.4);
      return { lean: t < TUP ? -0.16 * prep : -0.16 * (1 - rec), squash: t < TUP ? -0.05 * prep : 0 };
    };
    return { total, draw, enemies, playerPose: pose, shakes, frames, sfx };
  }

  addAbility({
    id: 'star', num: 6, name: 'Golpe estelar', short: 'Golpe estelar',
    pitch: 'El cristal lanza una señal al cielo; un círculo rúnico marca el suelo bajo el enemigo y se va cerrando (aviso), y cae una lanza de luz que aplasta lo que haya debajo. Mucho daño, mucho retraso: hay que leer hacia dónde va el enemigo.',
    fit: 'Habilidad de recarga larga o arma «diferida». Encaja con un sistema de marcas: otras armas marcan y el golpe cae solo sobre los marcados.',
    ideas: [
      '<b>Constelación</b> (Sistema nuevo: transformación tipo Isaac): con 3 items «astrales», cada 5º disparo de cualquier arma invoca un golpe estelar pequeño gratis.',
      '<b>Seguimiento</b> (Haste): el círculo persigue al enemigo durante el aviso.',
      '<b>Hielo</b>: la lanza deja una columna de hielo que bloquea el paso (pared temporal).',
      '<b>Parry</b>: un parry perfecto marca al atacante; el siguiente golpe estelar cae sobre él sin aviso.',
      '<b>Doble impacto</b> (Reset): si mata, cae otra lanza sobre el enemigo más cercano.',
    ],
    clips: [
      { title: '6 · Base', tag: 'Base', text: 'El player levanta el cristal: señal al cielo. Círculo rúnico bajo el segundo enemigo (arcos que giran en sentidos opuestos, marcas, columna guía) que se cierra mientras un destello crece arriba. Cae la lanza (agujas de velocidad alrededor), aplasta al enemigo y revienta el suelo: anillo, grietas, polvo y escombros.', P: {} },
      { title: '6 · Marcas', tag: 'Sistema nuevo: marcas', text: 'Tres enemigos ya marcados por otras armas (rombo y corchetes encima). El golpe estelar no apunta: cae en cadena sobre cada marcado, incluido el volador, que sale despedido hacia abajo.', P: { mode: 'marks' } },
      { title: '6 · Fuego → cráter', tag: 'Fuego · Sistema nuevo: zonas', text: 'Lanza de fuego: el impacto deja un cráter oscuro con llamas durante más de un segundo y el enemigo queda ardiendo. El golpe pasa a controlar una zona.', P: { pal: 'fire', mode: 'fire' } },
      { title: '6 · Lluvia', tag: 'Oro', text: 'En vez de una lanza, una lluvia de agujas doradas en diagonal sobre una zona más grande: muchos golpes pequeños que alcanzan también al volador. Menos daño por golpe, mejor contra grupos.', P: { pal: 'gold', mode: 'rain' } },
    ],
    build,
  });
})();
