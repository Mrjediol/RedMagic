// g-golpe-estelar.js — 6 · Golpe estelar, versión jugable. Ficha y decisiones: docs/armas/golpe-estelar.md
// Clic = marca el suelo bajo el ratón; tras el aviso cae una lanza vertical que golpea todo lo que hay en la columna
// (también voladores). Con Marcas: los golpes de tus otras armas marcan y la lanza cae sola sobre los marcados.
'use strict';
addWeapon({
  id: 'star', name: 'Golpe estelar', short: 'G. estelar', pal: 'set',
  mods: [
    { id: 'marks', name: 'Marcas', desc: 'Tus otras armas marcan al golpear; la lanza cae en cadena sobre los marcados (sin apuntar).' },
    { id: 'fire', name: 'Fuego', desc: 'Deja un cráter ardiendo que quema a quien lo pisa.' },
    { id: 'rain', name: 'Lluvia', desc: 'Lluvia de agujas en diagonal sobre una zona más ancha.' },
  ],
  tuning: {
    cooldown: [3, 0.3, 10, 0.1, 'Recarga (s)'], delay: [0.6, 0.1, 2, 0.05, 'Aviso hasta que cae (s)'], damage: [70, 1, 300, 1, 'Daño de la lanza'],
    width: [40, 10, 120, 2, 'Ancho de la columna'], shockDamage: [15, 0, 100, 1, 'Onda: daño'], shockRadius: [120, 0, 300, 5, 'Onda: radio'],
    maxTargets: [5, 1, 10, 1, 'Marcas: objetivos máx.'], chainGap: [0.15, 0.03, 0.6, 0.01, 'Marcas: tiempo entre lanzas (s)'],
    craterTime: [1.3, 0.2, 5, 0.1, 'Fuego: duración del cráter (s)'], craterBurn: [2, 0, 6, 0.1, 'Fuego: quemadura (s)'],
    rainNeedles: [15, 3, 40, 1, 'Lluvia: agujas'], rainWidth: [170, 40, 400, 5, 'Lluvia: ancho'], rainTime: [0.55, 0.1, 2, 0.05, 'Lluvia: duración (s)'], needleDamage: [12, 1, 60, 1, 'Lluvia: daño por aguja'],
  },
  create(slot) {
    const id = 'star';
    let cd = 0;
    return {
      onPress(aim) {
        if (cd > 0) return;
        const t = T(id); cd = t.cooldown; G.player.castT = 0.4;
        sfx(SND.star_up.fn);
        const pal = PAL[hasMod(slot, 'fire') ? 'fire' : hasMod(slot, 'rain') ? 'gold' : 'set'];
        const m = { x: G.player.x + 16 * G.player.face, y: G.player.y - 74 };
        addFx(0.35, (ctx, layer, tu) => {                    // señal hacia el cielo
          if (layer !== 'front') return; const u = seg(G.t, tu, tu + 0.3), top = lerp(m.y, -20, easeOut(seg(u, 0, 0.4)));
          strokeTaper(ctx, [{ x: m.x, y: top }, { x: m.x, y: m.y }], 6 * (1 - u), pal.light, 1 - u, layer, false); strokeTaper(ctx, [{ x: m.x, y: top }, { x: m.x, y: m.y }], 2.4 * (1 - u), pal.core, 1 - u, layer, false);
        });
        const marked = hasMod(slot, 'marks') ? alive().filter(e => e.markT > 0).sort((a, b) => Math.abs(a.x - G.player.x) - Math.abs(b.x - G.player.x)).slice(0, t.maxTargets) : [];
        if (marked.length) marked.forEach((e, i) => { e.markT = 0; G.ents.push(starStrike(slot, pal, null, e, t.delay * 0.6 + i * t.chainGap)); });
        else if (hasMod(slot, 'rain')) G.ents.push(starRain(slot, pal, aim.tx));
        else G.ents.push(starStrike(slot, pal, aim.tx, null, t.delay));
      },
      update(dt) { cd -= dt; },
      ready() { return 1 - cd / T(id).cooldown; },
      refund(s) { cd -= s; },
    };
  },
});

// Una lanza: sobre un punto del suelo (x) o siguiendo a un enemigo marcado (target) hasta que cae.
function starStrike(slot, pal, x0, target, delay) {
  const t = T('star'), fire = hasMod(slot, 'fire'), t0 = G.t, TS = t0 + delay, FALL = 0.07;
  let x = target ? target.x : x0, hy = GROUND, TI = TS + FALL, done = false;
  sfx(SND.star_mark.fn);
  const fallS = { at: TS - 0.42, played: false };
  return {
    kind: 'strike',
    update() {
      if (target && G.t < TS) { if (!target.dead) x = target.x; }
      if (!fallS.played && G.t >= fallS.at) { fallS.played = true; sfx(SND.star_fall.fn); }
      if (!done && G.t >= TI) {
        done = true;
        // golpe: todo lo que está en la columna (de arriba abajo) recibe la lanza; alrededor, la onda
        for (const e of alive()) {
          if (Math.abs(e.x - x) < t.width / 2 + e.rx) damage(e, { dmg: t.damage, kx: Math.sign(e.x - x) * 60, ky: e.fly ? 0 : -120, heavy: true, slot, elem: fire ? 'fire' : null, burn: fire ? t.craterBurn : 0, hitstop: 0.08, shake: 10, frame: true });
          else if (Math.abs(e.x - x) < t.shockRadius && GROUND - e.y < 90) damage(e, { dmg: t.shockDamage, kx: Math.sign(e.x - x) * 160, slot, noReact: true });
          if (e.fly && Math.abs(e.x - x) < t.width / 2 + e.rx) { e.slammed = true; e.vy = 600; }
        }
        sfx(SND.star_impact.fn);
        const ix = x, P = pal;
        addFx(1.4, (ctx, layer, ti) => { groundImpact(ctx, G.t, layer, ti, ix, 1.35, P, { seed: Math.floor(ix) }); cracks(ctx, G.t, ti, ix, layer, { n: 8, len: 110, pal: P }); if (layer === 'front') { const u = seg(G.t, ti, ti + 0.2); if (u < 1) glow(ctx, ix, GROUND - 30, 150, P.g1, 0.7 * (1 - u)); } });
        if (fire) G.ents.push(crater(slot, ix));
      }
      return G.t < TI + 0.32;
    },
    draw(ctx, layer) {
      if (G.t < TS + 0.02) {                                   // círculo rúnico y destello arriba
        const a = easeOut(seg(G.t, t0, t0 + 0.12)), r = lerp(74, 46, easeInOut(seg(G.t, t0, TS)));
        runeCircle(ctx, G.t, layer, x, r, a, pal);
        if (layer === 'front') { const k = seg(G.t, t0, TS); star4(ctx, x, 14, (8 + 16 * k) * (0.8 + 0.2 * Math.sin(G.t * 40)), G.t, pal.core, a, layer); glow(ctx, x, 14, 40 * k, pal.g2, a * k); }
      }
      if (G.t >= TS) {
        const fallU = seg(G.t, TS, TI), y = lerp(-30, hy, easeIn(fallU)), after = seg(G.t, TI + 0.12, TI + 0.3), w = 18 * (1 - after);
        if (after < 1) starLance(ctx, G.t, layer, x, y, w, 1 - after * after, pal);
        if (G.t < TI && layer === 'front') for (let i = 0; i < 6; i++) { const R = rand(i + 70), ox = (R() - 0.5) * 60, l = 40 + R() * 50, yy = lerp(-60, hy, easeIn(seg(G.t, TS - 0.02 * i, TI))); needle(ctx, x + ox, yy - l, x + ox, yy, 2, pal.light, 0.7, layer); }
      }
    },
  };
}
function crater(slot, x) {
  const t = T('star'), t0 = G.t; let tick = 0;
  return {
    kind: 'zone',
    update(dt) { tick -= dt; if (tick <= 0) { tick = 0.3; for (const e of alive()) if (Math.abs(e.x - x) < 70 && GROUND - (e.y + e.ry) < 30) damage(e, { dmg: 3, elem: 'fire', burn: t.craterBurn, slot, silentFlash: true }); } return G.t - t0 < t.craterTime + 0.35; },
    draw(ctx, layer) {
      const end = t0 + t.craterTime;
      for (let i = 0; i < 9; i++) {
        const R = rand(i + 40), d = (R() - 0.5) * 120, on = seg(G.t, t0 + Math.abs(d) / 400, t0 + Math.abs(d) / 400 + 0.1) * (1 - seg(G.t, end - 0.2 + R() * 0.2, end + 0.35)), lay = R() < 0.45 ? 'back' : 'front';
        if (lay === layer) flame(ctx, x + d, GROUND + 2, (24 + R() * 22) * on, (7 + R() * 5) * Math.max(0.3, on), G.t, i, on, layer);
      }
      if (layer === 'back') { const a = (1 - seg(G.t, end, end + 0.35)) * 0.7; ctx.save(); ctx.globalAlpha = a; ctx.fillStyle = col('#3a1208', layer); ctx.beginPath(); ctx.ellipse(x, GROUND + 4, 70, 9, 0, 0, TAU); ctx.fill(); ctx.restore(); groundRing(ctx, x, GROUND + 3, 66, 2.4, PAL.fire.mid, a, layer, { taper: 0, wob: 0.1, seed: 9 }); }
    },
  };
}
// Lluvia: agujas en diagonal repartidas en una franja alrededor de x; cada una golpea lo primero que corta
function starRain(slot, pal, cx) {
  const t = T('star'), t0 = G.t, TS = t0 + t.delay, ang = Math.PI / 2 - 0.42, L = 420;
  const needles = [];
  for (let i = 0; i < t.rainNeedles; i++) {
    const R = rand(i + 900 + Math.floor(t0 * 10)), tx = cx - t.rainWidth / 2 + ((i * 7) % t.rainNeedles) / t.rainNeedles * t.rainWidth + (R() - 0.5) * 10, t1 = TS + i / t.rainNeedles * t.rainTime + R() * 0.02;
    needles.push({ tx, t1, t0: t1 - 0.12, x0: tx - Math.cos(ang) * L, y0: GROUND - Math.sin(ang) * L, hit: null, done: false });
  }
  sfx(SND.star_mark.fn);
  return {
    kind: 'rain',
    update() {
      for (const n of needles) {
        if (n.done || G.t < n.t1) continue;
        n.done = true;
        const h = raycastEnemy(n.x0, n.y0, n.tx, GROUND);
        if (h) { n.hit = h; damage(h.e, { dmg: t.needleDamage, kx: 40, slot, heavy: false, shake: 2.5, hitstop: 0.015 }); }
        const ix = h ? h.x : n.tx, iy = h ? h.y : GROUND, P = pal, onE = !!h;
        addFx(1.2, (ctx, layer, ti) => {
          if (onE) impact(ctx, G.t, layer, ti, ix, iy, 0.7, P, { dir: -ang, seed: Math.floor(ix) });
          else { groundImpact(ctx, G.t, layer, ti, ix, 0.6, P, { seed: Math.floor(ix), debris: false }); if (layer === 'front' && G.t < ti + 0.5) lens(ctx, ix - Math.cos(ang) * 8, GROUND - Math.sin(ang) * 8, ang, 22, 3, P.light, 1 - seg(G.t, ti + 0.2, ti + 0.5), layer); }
        });
        sfx(SND.star_needle.fn);
      }
      return G.t < TS + t.rainTime + 0.3;
    },
    draw(ctx, layer) {
      if (G.t < TS + 0.02) {
        const a = easeOut(seg(G.t, t0, t0 + 0.12)), r = lerp(110, 88, easeInOut(seg(G.t, t0, TS)));
        runeCircle(ctx, G.t, layer, cx, r, a, pal);
        if (layer === 'front') { const k = seg(G.t, t0, TS); star4(ctx, cx - 90, 14, (8 + 16 * k) * (0.8 + 0.2 * Math.sin(G.t * 40)), G.t, pal.core, a, layer); glow(ctx, cx - 90, 14, 40 * k, pal.g2, a * k); }
      }
      for (const n of needles) {
        if (G.t < n.t0 || G.t >= n.t1) continue;
        const end = raycastEnemy(n.x0, n.y0, n.tx, GROUND), endU = end ? end.u : 1, u = seg(G.t, n.t0, n.t1) * endU;
        rainNeedle(ctx, lerp(n.x0, n.tx, u), lerp(n.y0, GROUND, u), ang, pal, layer);
      }
    },
  };
}
