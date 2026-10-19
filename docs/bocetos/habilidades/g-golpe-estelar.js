// g-golpe-estelar.js — 6 · Golpe estelar, versión jugable. Ficha: docs/armas/golpe-estelar.md · progresión: docs/armas/progresion.md
// Árbol: base (una lanza donde apuntas) → evolución (Bombardeo / Cazador / Tormenta) → 3 mejoras. La lanza golpea toda la
// columna (voladores incluidos). Elemento: hielo congela la columna, fuego deja un cráter. Duplicador: dos lanzas.
'use strict';
addWeapon({
  id: 'star', name: 'Golpe estelar', short: 'G. estelar', pal: 'set',
  archetypes: [
    { id: 'barrage', name: 'Bombardeo', desc: 'Lanzas más pequeñas en fila desde donde apuntas hacia delante.',
      ups: [{ id: 'salvo', name: 'Salva', desc: '+2 lanzas en la fila.' },
            { id: 'finale', name: 'Final', desc: 'La última lanza es enorme (doble de ancho y de daño).' },
            { id: 'rolling', name: 'Rodante', desc: 'La fila sigue al ratón mientras va cayendo.' }] },
    { id: 'hunter', name: 'Cazador', desc: 'Tus otras armas marcan al golpear; las lanzas caen solas sobre los marcados (sin apuntar).',
      ups: [{ id: 'stack', name: 'Acumulación', desc: 'Las marcas se apilan hasta 3: cada una suma daño a la lanza.' },
            { id: 'exec', name: 'Ejecución', desc: 'Remata a los que quedan con poca vida y marca al enemigo más cercano.' },
            { id: 'watch', name: 'Vigilancia', desc: 'Cada pocos segundos cae una lanza sola sobre un marcado, aunque lleves la otra arma.' }] },
    { id: 'storm', name: 'Tormenta', desc: 'Lluvia de agujas en diagonal sobre una zona ancha.',
      ups: [{ id: 'deluge', name: 'Diluvio', desc: 'Más agujas, dura el doble y la zona sigue al ratón.' },
            { id: 'heavyN', name: 'Agujas pesadas', desc: 'Cada aguja es golpe fuerte (rompe congelados) y hace más daño.' },
            { id: 'eye', name: 'Ojo de la tormenta', desc: 'La lluvia atrae hacia el centro y acaba con una lanza en medio.' }] },
  ],
  tuning: {
    cooldown: [3, 0.3, 10, 0.1, 'Base: recarga (s)'], delay: [0.6, 0.1, 2, 0.05, 'Base: aviso hasta que cae (s)'], damage: [70, 1, 300, 1, 'Base: daño de la lanza'],
    width: [40, 10, 120, 2, 'Base: ancho de la columna'], shockDamage: [15, 0, 100, 1, 'Base: onda, daño'], shockRadius: [120, 0, 300, 5, 'Base: onda, radio'],
    iceRadius: [80, 0, 250, 5, 'Hielo: radio que congela al caer'], craterTime: [1.3, 0.2, 5, 0.1, 'Fuego: duración del cráter (s)'], craterBurn: [2, 0, 6, 0.1, 'Fuego: quemadura (s)'],
    barrageCount: [3, 1, 10, 1, 'Bombardeo: lanzas'], barrageGap: [70, 20, 200, 2, 'Bombardeo: separación (px)'], barrageStep: [0.12, 0.02, 0.5, 0.01, 'Bombardeo: tiempo entre lanzas (s)'],
    barrageDamage: [45, 1, 200, 1, 'Bombardeo: daño por lanza'], barrageWidth: [30, 10, 100, 2, 'Bombardeo: ancho'], finaleMul: [2, 1, 5, 0.1, 'Bombardeo · Final: × tamaño y daño'],
    maxTargets: [5, 1, 10, 1, 'Cazador: objetivos máx.'], chainGap: [0.15, 0.03, 0.6, 0.01, 'Cazador: tiempo entre lanzas (s)'], stackMul: [0.6, 0, 2, 0.05, 'Cazador · Acumulación: + daño por marca extra'],
    execThreshold: [0.25, 0, 0.6, 0.01, 'Cazador · Ejecución: remata por debajo de (× vida)'], watchEvery: [4, 1, 15, 0.5, 'Cazador · Vigilancia: cada (s)'],
    rainNeedles: [15, 3, 40, 1, 'Tormenta: agujas'], rainWidth: [170, 40, 400, 5, 'Tormenta: ancho'], rainTime: [0.55, 0.1, 2, 0.05, 'Tormenta: duración (s)'], needleDamage: [12, 1, 60, 1, 'Tormenta: daño por aguja'],
    delugeMul: [1.6, 1, 4, 0.1, 'Tormenta · Diluvio: × agujas'], heavyMul: [1.5, 1, 4, 0.1, 'Tormenta · Agujas pesadas: × daño'], eyePull: [140, 0, 400, 5, 'Tormenta · Ojo: fuerza de atracción'],
    miniDamage: [25, 1, 100, 1, 'Constelación (item): daño de la lanza pequeña'],
  },
  create(slot) {
    const id = 'star';
    let cd = 0, watchT = 0;
    const pal = () => elemPal(slot.arch === 'storm' ? 'gold' : 'set');
    const signal = () => {
      const P = pal(), m = { x: G.player.x + 16 * G.player.face, y: G.player.y - 74 };
      addFx(0.35, (ctx, layer, tu) => {
        if (layer !== 'front') return; const u = seg(G.t, tu, tu + 0.3), top = lerp(m.y, -20, easeOut(seg(u, 0, 0.4)));
        strokeTaper(ctx, [{ x: m.x, y: top }, { x: m.x, y: m.y }], 6 * (1 - u), P.light, 1 - u, layer, false); strokeTaper(ctx, [{ x: m.x, y: top }, { x: m.x, y: m.y }], 2.4 * (1 - u), P.core, 1 - u, layer, false);
      });
      sfx(SND.star_up.fn);
    };
    // una lanza (con Duplicador, dos a los lados)
    const lanceAt = (o) => {
      const n = copies(), w = o.width;
      for (let c = 0; c < n; c++) G.ents.push(starStrike(Object.assign({}, o, { slot, pal: pal(), offset: n > 1 ? (c ? 1 : -1) * w * 0.9 : 0 })));
    };
    const strikeMarked = (list, base) => list.forEach((e, i) => {
      const mult = hasMod(slot, 'stack') ? 1 + T(id).stackMul * ((e.markN || 1) - 1) : 1;
      e.markT = 0; e.markN = 0;
      lanceAt({ target: e, delay: base + i * T(id).chainGap, dmg: T(id).damage * mult, width: T(id).width, exec: hasMod(slot, 'exec') });
    });
    return {
      onPress(aim) {
        if (cd > 0) return;
        const t = T(id); G.player.castT = 0.4; signal();
        if (slot.arch === 'barrage') {
          cd = t.cooldown * 1.15 * cdMul();
          const n = t.barrageCount + (hasMod(slot, 'salvo') ? 2 : 0), dir = Math.sign(aim.tx - G.player.x) || G.player.face;
          for (let i = 0; i < n; i++) {
            const big = i === n - 1 && hasMod(slot, 'finale'), k = big ? t.finaleMul : 1, off = i * t.barrageGap * dir;
            lanceAt({ x: aim.tx + off, track: hasMod(slot, 'rolling') ? () => G.mouse.x + off : null, delay: t.delay + i * t.barrageStep, dmg: t.barrageDamage * k, width: t.barrageWidth * k, scale: 0.75 * k });
          }
        } else if (slot.arch === 'hunter') {
          cd = t.cooldown * cdMul();
          const marked = alive().filter(e => e.markT > 0).sort((a, b) => Math.abs(a.x - G.player.x) - Math.abs(b.x - G.player.x)).slice(0, t.maxTargets);
          if (marked.length) strikeMarked(marked, t.delay * 0.6);
          else lanceAt({ x: aim.tx, delay: t.delay, dmg: t.damage, width: t.width });
        } else if (slot.arch === 'storm') {
          cd = t.cooldown * 1.15 * cdMul();
          G.ents.push(starRain(slot, pal(), aim.tx));
        } else {
          cd = t.cooldown * cdMul();
          lanceAt({ x: aim.tx, delay: t.delay, dmg: t.damage, width: t.width });
        }
      },
      update(dt) {
        cd -= dt;
        if (slot.arch === 'hunter' && hasMod(slot, 'watch')) {             // Vigilancia: aunque no sea el arma activa
          watchT -= dt;
          if (watchT <= 0) { const m = alive().filter(e => e.markT > 0); if (m.length) { watchT = T(id).watchEvery; strikeMarked([m[Math.floor(Math.random() * m.length)]], 0.35); } else watchT = 0.5; }
        }
      },
      ready() { return 1 - cd / Math.max(0.01, T(id).cooldown * (slot.arch === 'barrage' || slot.arch === 'storm' ? 1.15 : 1) * cdMul()); },
      refund(s) { cd -= s; },
    };
  },
});

// Constelación (item general): lanza pequeña sobre un enemigo, sin arma
G.spawnMiniStrike = e => G.ents.push(starStrike({ slot: null, pal: elemPal('set'), target: e, delay: 0.25, dmg: T('star').miniDamage, width: 24, scale: 0.6, noShock: true }));

// Una lanza. o = { slot, pal, x | target | track, offset, delay, dmg, width, scale, exec, noShock }
function starStrike(o) {
  const t = T('star'), el = elemOf(), slot = o.slot, pal = o.pal, sc = o.scale || 1;
  const t0 = G.t, TS = t0 + o.delay, FALL = 0.07, TI = TS + FALL;
  const xAt = () => (o.target && !o.target.dead ? o.target.x : o.track ? o.track() : o.x) + (o.offset || 0);
  let x = xAt(), done = false, fallPlayed = false;
  if (!o.noShock) sfx(SND.star_mark.fn);
  return {
    kind: 'strike',
    update() {
      if (G.t < TS) x = xAt();
      if (!fallPlayed && G.t >= TS - 0.42) { fallPlayed = true; if (!o.noShock) sfx(SND.star_fall.fn); }
      if (!done && G.t >= TI) {
        done = true;
        for (const e of alive()) {
          if (Math.abs(e.x - x) < o.width / 2 + e.rx) {
            damage(e, { dmg: o.dmg, kx: Math.sign(e.x - x) * 60, ky: e.fly ? 0 : -120, heavy: true, slot, elem: el, freeze: el === 'ice' ? 1 : 0, burn: el === 'fire' ? t.craterBurn : 0, hitstop: 0.08 * sc, shake: 10 * sc, frame: sc >= 0.75,
              onKill: o.exec ? dead => { const n = nearestEnemy(dead.x, dead.y, 300); if (n) { n.markT = 8; n.markN = Math.max(1, n.markN || 0); } } : null });
            if (o.exec && !e.dead && e.hp < e.hpMax * t.execThreshold) { hurtEnemy(e, e.hp + 1, '#ff5a7e', true); text(e.x, e.y - e.ry - 34, 'EJECUCIÓN', '#ff9fb4', true); const n = nearestEnemy(e.x, e.y, 300); if (n) { n.markT = 8; n.markN = Math.max(1, n.markN || 0); } }
            if (e.fly && !e.dead) { e.slammed = true; e.vy = 600; }
          } else if (!o.noShock && Math.abs(e.x - x) < t.shockRadius * sc && GROUND - e.y < 90) {
            damage(e, { dmg: t.shockDamage * sc, kx: Math.sign(e.x - x) * 160, slot, noReact: true, elem: el, freeze: el === 'ice' && Math.abs(e.x - x) < t.iceRadius ? 0.5 : 0 });
          }
        }
        sfx(o.noShock ? SND.star_needle.fn : SND.star_impact.fn);
        const ix = x, P = pal;
        addFx(1.4, (ctx, layer, ti) => {
          groundImpact(ctx, G.t, layer, ti, ix, 1.35 * sc, P, { seed: Math.floor(ix) }); cracks(ctx, G.t, ti, ix, layer, { n: 8, len: 110 * sc, pal: P });
          if (layer === 'front') { const u = seg(G.t, ti, ti + 0.2); if (u < 1) glow(ctx, ix, GROUND - 30, 150 * sc, P.g1, 0.7 * (1 - u)); }
        });
        if (el === 'ice') addFx(1.6, (ctx, layer, ti) => frostNova(ctx, G.t, layer, ti, ix, t.iceRadius * sc));
        if (el === 'fire') G.ents.push(fireZone(ix, 70 * sc, t.craterTime, t.craterBurn, slot));
      }
      return G.t < TI + 0.32;
    },
    draw(ctx, layer) {
      if (G.t < TS + 0.02) {                                 // círculo rúnico y destello arriba
        const a = easeOut(seg(G.t, t0, t0 + 0.12)), r = lerp(74, 46, easeInOut(seg(G.t, t0, TS))) * Math.max(0.6, sc);
        runeCircle(ctx, G.t, layer, x, r, a, pal);
        if (layer === 'front') { const k = seg(G.t, t0, TS); star4(ctx, x, 14, (8 + 16 * k) * sc * (0.8 + 0.2 * Math.sin(G.t * 40)), G.t, pal.core, a, layer); glow(ctx, x, 14, 40 * k * sc, pal.g2, a * k); }
      }
      if (G.t >= TS) {
        const fallU = seg(G.t, TS, TI), y = lerp(-30, GROUND, easeIn(fallU)), after = seg(G.t, TI + 0.12, TI + 0.3), w = 18 * sc * (1 - after);
        if (after < 1) starLance(ctx, G.t, layer, x, y, w, 1 - after * after, pal);
        if (G.t < TI && layer === 'front') for (let i = 0; i < 6; i++) { const R = rand(i + 70), ox = (R() - 0.5) * 60 * sc, l = 40 + R() * 50, yy = lerp(-60, GROUND, easeIn(seg(G.t, TS - 0.02 * i, TI))); needle(ctx, x + ox, yy - l, x + ox, yy, 2, pal.light, 0.7, layer); }
      }
    },
  };
}

// Tormenta: agujas en diagonal repartidas en una franja; cada una golpea lo primero que corta
function starRain(slot, pal, cx0) {
  const t = T('star'), el = elemOf(), deluge = hasMod(slot, 'deluge'), heavy = hasMod(slot, 'heavyN'), eye = hasMod(slot, 'eye');
  const t0 = G.t, TS = t0 + t.delay, dur = t.rainTime * (deluge ? 2 : 1), count = Math.round(t.rainNeedles * (deluge ? t.delugeMul : 1) * copies()), ang = Math.PI / 2 - 0.42, L = 420;
  let cx = cx0, lanceDone = false;
  const needles = [];
  for (let i = 0; i < count; i++) {
    const R = rand(i + 900 + Math.floor(t0 * 10)), rel = -t.rainWidth / 2 + ((i * 7) % count) / count * t.rainWidth + (R() - 0.5) * 10, t1 = TS + i / count * dur + R() * 0.02;
    needles.push({ rel, t1, t0: t1 - 0.12, done: false, tx: null });
  }
  sfx(SND.star_mark.fn);
  return {
    kind: 'rain',
    update() {
      if (deluge && G.t < TS + dur) cx = lerp(cx, G.mouse.x, 0.06);
      if (eye && G.t >= TS && G.t < TS + dur) for (const e of alive()) if (Math.abs(e.x - cx) < t.rainWidth / 2 + 40) e.pull = { x: cx, y: e.y, s: t.eyePull };
      for (const n of needles) {
        if (n.tx === null && G.t >= n.t0) n.tx = cx + n.rel;                  // la aguja fija su sitio al aparecer
        if (n.done || G.t < n.t1) continue;
        n.done = true;
        const x0 = n.tx - Math.cos(ang) * L, y0 = GROUND - Math.sin(ang) * L, h = raycastEnemy(x0, y0, n.tx, GROUND);
        if (h) damage(h.e, { dmg: t.needleDamage * (heavy ? t.heavyMul : 1), kx: 40, slot, heavy, elem: el, freeze: el === 'ice' ? 0.2 : 0, burn: el === 'fire' ? 1 : 0, shake: 2.5, hitstop: 0.015 });
        const ix = h ? h.x : n.tx, iy = h ? h.y : GROUND, P = pal, onE = !!h;
        addFx(1.2, (ctx, layer, ti) => {
          if (onE) impact(ctx, G.t, layer, ti, ix, iy, 0.7, P, { dir: -ang, seed: Math.floor(ix) });
          else { groundImpact(ctx, G.t, layer, ti, ix, 0.6, P, { seed: Math.floor(ix), debris: false }); if (layer === 'front' && G.t < ti + 0.5) lens(ctx, ix - Math.cos(ang) * 8, GROUND - Math.sin(ang) * 8, ang, 22, 3, P.light, 1 - seg(G.t, ti + 0.2, ti + 0.5), layer); }
        });
        if (!G._ndlSfx || G.t - G._ndlSfx > 0.03) { G._ndlSfx = G.t; sfx(SND.star_needle.fn); }
      }
      if (eye && !lanceDone && G.t >= TS + dur - 0.1) { lanceDone = true; G.ents.push(starStrike({ slot, pal, x: cx, delay: 0.1, dmg: t.damage, width: t.width })); }
      return G.t < TS + dur + 0.3;
    },
    draw(ctx, layer) {
      if (G.t < TS + 0.02 || (eye && G.t < TS + dur)) {
        const a = easeOut(seg(G.t, t0, t0 + 0.12)) * (G.t > TS ? 0.6 : 1), r = lerp(110, 88, easeInOut(seg(G.t, t0, TS)));
        runeCircle(ctx, G.t, layer, cx, r, a, pal);
        if (layer === 'front' && G.t < TS + 0.02) { const k = seg(G.t, t0, TS); star4(ctx, cx - 90, 14, (8 + 16 * k) * (0.8 + 0.2 * Math.sin(G.t * 40)), G.t, pal.core, a, layer); glow(ctx, cx - 90, 14, 40 * k, pal.g2, a * k); }
      }
      for (const n of needles) {
        if (n.tx === null || G.t < n.t0 || G.t >= n.t1) continue;
        const x0 = n.tx - Math.cos(ang) * L, y0 = GROUND - Math.sin(ang) * L, end = raycastEnemy(x0, y0, n.tx, GROUND), u = seg(G.t, n.t0, n.t1) * (end ? end.u : 1);
        rainNeedle(ctx, lerp(x0, n.tx, u), lerp(y0, GROUND, u), ang, pal, layer);
      }
    },
  };
}
