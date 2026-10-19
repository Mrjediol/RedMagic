// a1-abanico.js — 1 · Abanico de cristal: disparo rápido sin carga, varias esquirlas en abanico (escopeta corta).
'use strict';
(function () {
  const G = '1 · Abanico de cristal';
  const S = {
    release: sound('fan_release', G, 'Disparo (abanico)', k => { const t = nowT(); vNoise(t, 0.16 * k, 0.3, 'bandpass', 5200, 1400, 1.2, { att: 0.002, wet: 0.15, pan: [-0.2, 0.5] }); vRing(t, [2300, 3450, 4700], 0.22 * k, 0.05, 0.2); vThump(t, 160, 70, 0.12 * k, 0.35, 1.5); }),
    hit: sound('fan_hit', G, 'Esquirla · impacto', k => { const t = nowT(); vThump(t, 150, 60, 0.14 * k, 0.4, 2); vNoise(t, 0.06 * k, 0.28, 'highpass', 3500, 7000, 1, { att: 0.001 }); vRing(t, [3100, 4700], 0.12 * k, 0.04, 0.15); }),
    ground: sound('fan_ground', G, 'Esquirla · suelo', k => { const t = nowT(); vNoise(t, 0.08 * k, 0.18, 'bandpass', 2500, 1200, 2, { att: 0.001 }); vTone(t, 2900, 2500, 0.1 * k, 0.03); }),
    stick: sound('fan_stick', G, 'Hielo · se clava', k => { const t = nowT(); vThump(t, 190, 80, 0.1 * k, 0.4, 1.5); vNoise(t, 0.1 * k, 0.2, 'highpass', 4000, 9000, 1, { att: 0.001, wet: 0.2 }); vCrackle(t, 0.15 * k, 0.12, 10, 0.2, [4000, 9000]); }),
    freeze: sound('fan_freeze', G, 'Hielo · se congela', k => SFXLIB.freeze(k, 0.45)),
    shatter: sound('fan_shatter', G, 'Hielo · añicos', k => SFXLIB.shatter(k, 1.1)),
    ricochet: sound('fan_ricochet', G, 'Rebote · ricochet', k => { const t = nowT(); vTone(t, 3400, 1900, 0.22 * k, 0.07, 'triangle', 0.2); vRing(t, [1900, 2870, 4100], 0.25 * k, 0.05, 0.25); vNoise(t, 0.04 * k, 0.2, 'highpass', 5000, 5000, 1, { att: 0.001 }); }),
    rip: sound('fan_rip', G, 'Retorno · se arrancan', k => { const t = nowT(); vNoise(t, 0.2 * k, 0.25, 'bandpass', 700, 5000, 1.5, { rising: true, wet: 0.15, pan: [0.5, -0.5] }); vThump(t + 0.18 * k, 120, 50, 0.15 * k, 0.45, 2); }),
    absorb: sound('fan_absorb', G, 'Retorno · vuelven al cristal', k => { const t = nowT(); vTone(t, 880, 880, 0.5 * k, 0.05, 'sine', 0.4); vTone(t + 0.04 * k, 1320, 1320, 0.45 * k, 0.04, 'sine', 0.4); vTone(t + 0.08 * k, 1760, 1760, 0.4 * k, 0.03, 'sine', 0.4); vNoise(t, 0.2 * k, 0.08, 'highpass', 6000, 9000, 1, { wet: 0.3 }); }),
  };

  const TR = 0.3;
  const ENE = [{ x: 400 }, { x: 548 }];

  // Simula cada esquirla: muestras {t,x,y,a} cada DT; eventos de golpe/suelo/rebote según el modo.
  const DT = 0.002;
  function simulate(P) {
    const shards = [], events = [];
    for (let v = 0; v < (P.volleys || 1); v++) for (let i = 0; i < P.n; i++) {
      const u = P.n === 1 ? 0.5 : i / (P.n - 1), R = rand(i * 31 + v * 7 + 3);
      const a0 = lerp(-P.spread, P.spread, u) + (R() - 0.5) * 0.03, sp = P.sp * (0.94 + R() * 0.12), t0 = TR + v * (P.gap || 0) + Math.abs(u - 0.5) * 0.02;
      let x = O.x + 8, y = O.y, vx = Math.cos(a0) * sp, vy = Math.sin(a0) * sp, t = t0, bounces = 0, state = 'fly', hitE = new Set();
      const sm = [];
      while (t < t0 + 1.2) {
        sm.push({ t, x, y, a: Math.atan2(vy, vx) });
        if (state !== 'fly') break;
        const nx = x + vx * DT, ny = y + vy * DT;
        let hit = null;
        ENE.forEach((E, ei) => { if (hit || (P.mode !== 'pierce' && hitE.has(ei))) return; const h = segHitsEllipse(x, y, nx, ny, E.x, ECY, ERX, ERY); if (h && !hitE.has(ei)) hit = { ei, x: lerp(x, nx, h.u0), y: lerp(y, ny, h.u0) }; });
        if (hit) {
          hitE.add(hit.ei);
          const ev = { t, x: hit.x, y: hit.y, ei: hit.ei, ang: Math.atan2(vy, vx), shard: shards.length };
          if (P.mode === 'bounce' && bounces === 0) {
            bounces++; ev.type = 'ricochet';
            const tg = ENE[(hit.ei + 1) % ENE.length], ty = hit.ei === 1 ? ECY - 120 : ECY;      // del 2º rebota hacia arriba (fuera)
            const dx = (hit.ei === 1 ? hit.x - 160 : tg.x) - hit.x, dy = ty - hit.y, l = Math.hypot(dx, dy); const s2 = sp * 0.85;
            vx = dx / l * s2; vy = dy / l * s2; x = hit.x; y = hit.y; events.push(ev); t += DT; continue;
          }
          if (P.mode === 'pierce') { ev.type = 'pierce'; events.push(ev); x = nx; y = ny; t += DT; continue; }
          ev.type = (P.mode === 'ice' || P.mode === 'return') ? 'stick' : 'hit';
          events.push(ev); x = hit.x; y = hit.y; state = ev.type; sm.push({ t, x, y, a: ev.ang }); break;
        }
        if (ny >= GROUND - 2) {
          const ev = { t, x: nx, y: GROUND - 2, ang: Math.atan2(vy, vx), shard: shards.length, ei: -1 };
          if (P.mode === 'bounce' && bounces === 0) { bounces++; ev.type = 'ricochet'; vy = -vy * 0.75; vx *= 0.9; events.push(ev); x = nx; y = GROUND - 3; t += DT; continue; }
          ev.type = P.mode === 'return' ? 'stickGround' : 'ground'; events.push(ev); x = nx; y = GROUND - 2; state = ev.type; sm.push({ t, x, y, a: ev.ang }); break;
        }
        if (nx > W + 40 || ny < -40 || nx < -40) { state = 'off'; break; }
        x = nx; y = ny; t += DT;
      }
      shards.push({ sm, t0, end: state, tEnd: sm[sm.length - 1].t, v, i });
    }
    return { shards, events };
  }
  const sample = (sh, t) => { const k = clamp(Math.floor((t - sh.t0) / DT), 0, sh.sm.length - 1); return sh.sm[k]; };

  const drawShard = crystalShard;

  function build(P) {
    P = Object.assign({ pal: 'set', n: 5, spread: 0.2, sp: 950, mode: 'base', volleys: 1, gap: 0.1, sc: 1 }, P);
    const pal = PAL[P.pal], sim = simulate(P), ev = sim.events;
    const enemies = ENE.map(e => ({ x: e.x, hits: [] }));
    const sfx = [{ t: 0.02, f: k => SFXLIB.castShort(k * 0.7) }];
    for (let v = 0; v < (P.volleys || 1); v++) sfx.push({ t: TR + v * (P.gap || 0), f: S.release });
    let lastHitSfx = -1;
    const shakes = [], frames = [];
    for (const e of ev) {
      if (e.ei >= 0) {
        enemies[e.ei].hits.push({ t: e.t, k: P.mode === 'return' ? 3 : 6, fl: 0.14, shake: 2.5, sq: 0.08 });
        if (e.t - lastHitSfx > 0.025) { sfx.push({ t: e.t, f: e.type === 'stick' && P.mode === 'ice' ? S.stick : e.type === 'ricochet' ? S.ricochet : S.hit }); lastHitSfx = e.t; }
      } else sfx.push({ t: e.t, f: e.type === 'ricochet' ? S.ricochet : S.ground });
    }
    if (ev.some(e => e.ei >= 0)) shakes.push({ t: ev.find(e => e.ei >= 0).t, a: P.volleys > 1 ? 2 : 3.5, d: 0.25 });

    // ---- hielo: congela con las esquirlas clavadas y estalla en añicos
    const sticks = ev.filter(e => e.type === 'stick');
    const lastStick = sticks.length ? Math.max(...sticks.map(e => e.t)) : TR + 0.4;
    const TSH = lastStick + 0.55;
    if (P.mode === 'ice') {
      const E = enemies[0];
      E.tint = t => [PAL.ice.light, 0.45 * clamp(sticks.filter(s => s.ei === 0 && s.t <= t).length / 3) * (t < TSH ? 1 : 1 - seg(t, TSH, TSH + 0.3))];
      E.frozen = t => t < TSH ? 0.8 * easeOut(seg(t, lastStick, TSH - 0.05)) : 0;
      E.crack = t => seg(t, TSH - 0.2, TSH);
      E.hits.push({ t: TSH, k: 20, fl: 0.25, shake: 6, sq: 0.18 });
      sfx.push({ t: lastStick + 0.05, f: S.freeze }, { t: TSH, f: S.shatter });
      shakes.push({ t: TSH, a: 7, d: 0.35 }); frames.push({ t: TSH, d: 0.05 });
    }
    // ---- retorno: las clavadas (en enemigo o suelo) vuelven al cristal atravesando otra vez
    const TRET = lastStick + 0.32, ret = [];
    if (P.mode === 'return') {
      sim.shards.forEach((sh, si) => {
        if (sh.end !== 'stick' && sh.end !== 'stickGround') return;
        const R = rand(si + 90), tGo = TRET + R() * 0.06, dur = 0.34 + R() * 0.06;
        ret.push({ si, tGo, dur, e: ev.find(e => e.shard === si) });
      });
      for (const r of ret) if (r.e.ei >= 0) enemies[r.e.ei].hits.push({ t: r.tGo + 0.02, k: -9, fl: 0.18, shake: 3, sq: 0.1 });
      sfx.push({ t: TRET, f: S.rip });
      const tAbs = Math.min(...ret.map(r => r.tGo + r.dur));
      sfx.push({ t: tAbs, f: S.absorb });
      shakes.push({ t: TRET + 0.02, a: 3, d: 0.25 });
    }
    const tAbsorb = ret.length ? Math.min(...ret.map(r => r.tGo + r.dur)) : 99;

    const stickPos = (t, e) => {
      if (e.ei < 0) return { x: e.x, y: e.y };
      const p0 = enemyPos(e.t, enemies[e.ei]), p = enemyPos(t, enemies[e.ei]);
      return { x: e.x + (p.x - p0.x), y: e.y + (p.y - p0.y) };
    };
    const retPos = (r, t) => {                    // vuelve en curva hacia el cristal, acelerando
      const s = stickPos(r.tGo, r.e), u = easeIn(seg(t, r.tGo, r.tGo + r.dur)), mx = (s.x + O.x) / 2, my = Math.min(s.y, O.y) - 40 - (r.si % 3) * 12;
      const x = (1 - u) * (1 - u) * s.x + 2 * (1 - u) * u * mx + u * u * O.x, y = (1 - u) * (1 - u) * s.y + 2 * (1 - u) * u * my + u * u * O.y;
      return { x, y };
    };

    const total = P.mode === 'ice' ? TSH + 1.0 : P.mode === 'return' ? tAbsorb + 0.9 : TR + 1.5 + (P.volleys - 1) * P.gap;

    function draw(ctx, t, layer) {
      castFx(ctx, t, layer, 0.04, TR, pal, 0.8);
      // estelas y esquirlas en vuelo
      sim.shards.forEach((sh, si) => {
        if (t < sh.t0) return;
        const r = ret.find(q => q.si === si);
        const flying = t <= sh.tEnd || (sh.end === 'off' && t < sh.t0 + 1.2);
        if (flying) {
          const p = sample(sh, t);
          if (layer === 'back') {
            const pts = []; for (let k = 0; k <= 10; k++) { const q = sample(sh, Math.max(sh.t0, t - 0.055 * k / 10)); pts.push(q); }
            ribbon(ctx, pts, s => 9 * P.sc * Math.pow(1 - s, 1.2), pal.dark, 0.35, layer);
            ribbon(ctx, pts, s => 5 * P.sc * Math.pow(1 - s, 1.3), pal.mid, 0.8, layer);
            ribbon(ctx, pts, s => 2 * P.sc * (1 - s), pal.core, 1, layer);
          }
          drawShard(ctx, p.x, p.y, p.a, P.sc, pal, 1, layer);
        } else if ((sh.end === 'stick' || sh.end === 'stickGround') && (!r || t < r.tGo) && !(P.mode === 'ice' && t >= TSH)) {
          const e = ev.find(q => q.shard === si), s = stickPos(t, e), wig = r ? Math.sin(t * 90) * seg(t, r.tGo - 0.15, r.tGo) * 2 : 0;
          // clavada: sólo asoma la mitad trasera (la punta está dentro)
          drawShard(ctx, s.x - Math.cos(e.ang) * 8 + wig, s.y - Math.sin(e.ang) * 8, e.ang, P.sc * 0.85, pal, 1, layer);
          if (layer === 'front' && P.mode === 'ice') star4(ctx, s.x - Math.cos(e.ang) * 4, s.y - Math.sin(e.ang) * 4, 6 + 3 * Math.sin(t * 20 + si), 0.4, PAL.ice.core, 0.8, layer);
        }
        if (r && t >= r.tGo && t < r.tGo + r.dur) {
          const p = retPos(r, t), q = retPos(r, Math.max(r.tGo, t - 0.01)), ang = Math.atan2(p.y - q.y, p.x - q.x);
          if (layer === 'back') {
            const pts = trailPts(tt => retPos(r, tt), t, 0.09, 10, r.tGo);
            ribbon(ctx, pts, s => 8 * Math.pow(1 - s, 1.2), pal.dark, 0.35, layer); ribbon(ctx, pts, s => 4.5 * (1 - s), pal.light, 0.9, layer);
          }
          drawShard(ctx, p.x, p.y, ang, P.sc * 0.9, pal, 1, layer);
        }
      });
      // golpes
      for (const e of ev) {
        const R = e.shard * 13 + 5;
        if (e.ei >= 0 && (e.type === 'hit' || e.type === 'pierce' || e.type === 'ricochet' || e.type === 'stick')) {
          const p = stickPos(t, e);
          impact(ctx, t, layer, e.t, p.x, p.y, e.type === 'stick' ? 0.42 : 0.5, e.type === 'stick' && P.mode === 'ice' ? PAL.ice : pal, { dir: e.ang + Math.PI, spread: 2.2, seed: R, needles: 6, drops: 4 });
        } else if (e.ei < 0) {
          sparks(ctx, t, e.t, e.x, GROUND - 3, layer, { n: 8, a0: -Math.PI * 0.85, a1: -Math.PI * 0.15, sp: 230, pal, seed: R, len: 10, life: 0.35 });
          dust(ctx, t, e.t, e.x, layer, { n: 4, reach: 22, sc: 0.6, seed: R });
          if (layer === 'front') star4(ctx, e.x, GROUND - 4, 14 * (1 - seg(t, e.t, e.t + 0.15)), 0, pal.core, 1 - seg(t, e.t, e.t + 0.15), layer);
        }
      }
      // retorno: arrancar (destello en cada salida) y absorción en el cristal
      for (const r of ret) {
        const s = stickPos(r.tGo, r.e);
        if (r.e.ei >= 0) sparks(ctx, t, r.tGo, s.x, s.y, layer, { n: 6, a0: Math.PI * 0.7, a1: Math.PI * 1.3, sp: 200, pal, seed: r.si + 400, len: 12, life: 0.3 });
        if (layer === 'front') { const u = seg(t, r.tGo + r.dur, r.tGo + r.dur + 0.2); if (u > 0 && u < 1) { glow(ctx, O.x, O.y, 30, pal.g2, 0.7 * (1 - u)); star4(ctx, O.x, O.y, 16 * (1 - u * 0.5), 0.8, pal.core, 1 - u, layer); } }
      }
      // añicos
      if (P.mode === 'ice' && t >= TSH) {
        const E = enemies[0], p = enemyPos(TSH, E), u = t - TSH, I = PAL.ice;
        if (layer === 'front') { glow(ctx, p.x, p.y, 90 * (1 - seg(u, 0, 0.25)), I.g1, 0.7 * (1 - seg(u, 0, 0.25))); star4(ctx, p.x, p.y, 50 * (1 - seg(u, 0, 0.18) * 0.5), 0.2, I.core, 1 - seg(u, 0, 0.18), layer); }
        const ru = seg(u, 0, 0.35);
        for (const [k, c, w] of [[1, I.mid, 3.4], [0.75, I.core, 1.6]]) ellArc(ctx, p.x, p.y, (20 + 70 * easeOut(ru)) * k, (20 + 76 * easeOut(ru)) * k, layer === 'back' ? Math.PI * 0.5 : -Math.PI * 0.5, layer === 'back' ? Math.PI * 1.5 : Math.PI * 0.5, w, c, 1 - ru, layer, { wob: 0.15, seed: 50 + k * 9 });
        const R = rand(77);
        for (let i = 0; i < 16; i++) {
          const ang = R() * TAU, v = 160 + R() * 240, lay = R() < 0.45 ? 'back' : 'front', lf = 0.6 + R() * 0.4; if (lay !== layer || u > lf) continue;
          const px = p.x + Math.cos(ang) * v * u, py = p.y + Math.sin(ang) * v * u + 360 * u * u;
          if (py > GROUND) continue;
          chunk(ctx, px, py, 4 + R() * 6, u * (6 + R() * 8), i, I.mid, I.core, 1 - seg(u, lf * 0.7, lf), lay);
        }
        sparks(ctx, t, TSH, p.x, p.y, layer, { n: 14, pal: I, sp: 320, seed: 91, len: 16, life: 0.5 });
        dust(ctx, t, TSH + 0.02, p.x, layer, { n: 8, reach: 50, y: p.y + 30, seed: 66, life: 0.9, sc: 1.2 });
      }
    }
    const poses = t => {
      const p = poseCast(t, 0.04, TR, { lean: 0.06, kick: 0.1 });
      if (P.mode === 'return' && t > tAbsorb) { p.aura = pal.g2; p.auraA = 0.6 * (1 - seg(t, tAbsorb, tAbsorb + 0.6)); }
      return p;
    };
    return { total, draw, enemies, playerPose: poses, shakes, frames, sfx };
  }

  addAbility({
    id: 'fan', num: 1, name: 'Abanico de cristal', short: 'Abanico',
    pitch: 'Disparo inmediato sin carga: cinco esquirlas de cristal en abanico. Corto alcance, mucho daño de cerca (escopeta). Las que fallan chocan contra el suelo o se pierden. Es el arma de «pegarse al enemigo» frente al cometa, que premia la distancia.',
    fit: 'Arma base nueva (forma «abanico» del disparo base). Las variantes son items que cambian qué hace cada esquirla al tocar algo: se clava, rebota o vuelve.',
    ideas: [
      '<b>Cristal mayor</b> (Tank): 3 esquirlas enormes y lentas que empujan mucho.',
      '<b>Abanico cerrado</b> (Haste): las esquirlas salen en fila (ráfaga) en vez de en abanico: alcance largo, menos daño de cerca.',
      '<b>Corte de vuelta</b> (con melee): un tajo cuerpo a cuerpo dispara un abanico pequeño en su dirección.',
      '<b>Esquirlas de oro</b> (Oro): cada esquirla que mata suelta una moneda pequeña.',
      '<b>Sinergia con Singularidad</b>: las esquirlas que pasan junto a una singularidad quedan orbitando y salen disparadas al colapsar (ver clip de Singularidad).',
    ],
    clips: [
      { title: '1 · Base', tag: 'Base', text: 'Cinco esquirlas en abanico: tres aciertan al cuerpo (impactos pequeños con agujas y gotas), la de abajo choca contra el suelo con chispas y polvo, la de arriba se pierde. Sin carga: el cristal sólo destella.', P: {} },
      { title: '1 · Hielo → Añicos', tag: 'Hielo · Sistema nuevo: reacciones', text: 'Con hielo, las esquirlas se quedan clavadas y el enemigo se va congelando. Medio segundo después, el bloque de hielo estalla en añicos: golpe fuerte, fragmentos que caen, onda y vaho. Reacción «congelado + golpe = añicos».', P: { pal: 'ice', mode: 'ice' } },
      { title: '1 · Rebote', tag: 'Oro · Sistema nuevo: rebote', text: 'Cada esquirla rebota una vez: en el suelo sale despedida hacia arriba y en un enemigo se desvía hacia el siguiente. Firma dorada. El abanico deja de ser sólo de cerca: limpia grupos.', P: { pal: 'gold', mode: 'bounce' } },
      { title: '1 · Retorno', tag: 'Sistema nuevo: retorno', text: 'Las esquirlas se clavan (en enemigos y en el suelo) y, un instante después, vuelven al cristal en curva: al arrancarse golpean otra vez. Al llegar, el cristal brilla (cada esquirla que vuelve podría recargar o dar escudo).', P: { mode: 'return' } },
    ],
    build,
  });
})();
