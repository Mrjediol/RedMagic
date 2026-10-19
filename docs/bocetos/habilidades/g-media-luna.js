// g-media-luna.js — 4 · Media luna, versión jugable. Ficha y decisiones: docs/armas/media-luna.md
// Bumerán de una sola hoja: mientras está fuera no se puede volver a lanzar. Golpea a la ida y a la vuelta.
'use strict';
addWeapon({
  id: 'moon', name: 'Media luna', short: 'Media luna', pal: 'set',
  mods: [
    { id: 'twin', name: 'Doble', desc: 'Sale otra hoja en arco alto (cubre arriba y abajo).' },
    { id: 'orbit', name: 'Órbita', desc: 'Al volver da una vuelta alrededor del player antes de atraparse.' },
    { id: 'fire', name: 'Fuego', desc: 'Deja una estela de llamas en el aire que quema.' },
  ],
  tuning: {
    damage: [18, 1, 80, 1, 'Daño por pasada'], range: [360, 100, 700, 10, 'Alcance (px)'], outTime: [0.55, 0.15, 1.5, 0.05, 'Ida (s)'],
    hoverTime: [0.14, 0, 1, 0.02, 'Pausa en el extremo (s)'], backTime: [0.48, 0.15, 1.5, 0.05, 'Vuelta (s)'], knockback: [130, 0, 500, 5, 'Empuje'],
    hitRadius: [22, 6, 60, 1, 'Radio de golpe'], cooldown: [0.15, 0, 2, 0.05, 'Recarga tras atraparla (s)'],
    twinArc: [120, 20, 250, 5, 'Doble: altura del arco'], orbitRadius: [64, 30, 140, 2, 'Órbita: radio'], orbitTime: [0.42, 0.15, 1.5, 0.02, 'Órbita: duración (s)'],
    fireLife: [0.75, 0.2, 2, 0.05, 'Fuego: duración de la estela (s)'], fireBurn: [2.0, 0, 6, 0.1, 'Fuego: quemadura (s)'],
  },
  create(slot) {
    const id = 'moon';
    let cd = 0, out = [];
    return {
      onPress(aim) {
        if (cd > 0 || out.length) return;
        G.player.castT = 0.2;
        out = [moonBlade(slot, aim, 0, () => { out = out.filter(b => !b.done); if (!out.length) cd = T(id).cooldown; })];
        if (hasMod(slot, 'twin')) out.push(moonBlade(slot, aim, 1, () => { out = out.filter(b => !b.done); if (!out.length) cd = T(id).cooldown; }));
        out.forEach(b => G.ents.push(b));
        sfx(SND.moon_throw.fn);
      },
      update(dt) { cd -= dt; },
      ready() { return out.length ? 0 : 1 - cd / Math.max(0.01, T(id).cooldown); },
      refund(s) { cd -= s; },
    };
  },
});

function moonBlade(slot, aim, bi, onDone) {
  const t = T('moon'), orbit = hasMod(slot, 'orbit'), fire = hasMod(slot, 'fire'), pal = PAL[fire ? 'fire' : hasMod(slot, 'twin') ? 'gold' : 'set'], R0 = 21;
  const t0 = G.t, TO = t0 + t.outTime, TH = TO + t.hoverTime, TB = TH + t.backTime, TOE = TB + (orbit ? t.orbitTime : 0);
  const ox = aim.x, oy = aim.y, dir = { x: aim.dx, y: aim.dy }, perp = { x: dir.y, y: -dir.x }, hi = bi === 1 ? t.twinArc : 14;
  const up = perp.y > 0 ? -1 : 1;                           // el arco siempre hacia arriba en pantalla
  let apex = null, last = { x: ox, y: oy }, stopSpin = null;
  const hitOut = new Set(), hitBack = new Set(), hitOrbit = new Set();
  const pos = tt => {
    if (tt <= TO) { const u = seg(tt, t0, TO), d = t.range * easeOut(u), a = hi * Math.sin(Math.PI * u) * up; return { x: ox + dir.x * d + perp.x * a, y: oy + dir.y * d + perp.y * a }; }
    if (!apex) apex = { x: ox + dir.x * t.range, y: oy + dir.y * t.range };
    if (tt <= TH) return { x: apex.x + Math.sin((tt - TO) * 12) * 2, y: apex.y + Math.sin((tt - TO) * 9) * 2 };
    const p = G.player, m = muzzle(), target = orbit ? { x: p.x + t.orbitRadius * (apex.x > p.x ? 1 : -1), y: p.y - 34 } : m;
    if (tt <= TB) { const u = easeIn(seg(tt, TH, TB)), a = (bi === 1 ? hi : 6) * Math.sin(Math.PI * u) * up; return { x: lerp(apex.x, target.x, u) + perp.x * a, y: lerp(apex.y, target.y, u) + perp.y * a }; }
    if (orbit && tt <= TOE) { const s = apex.x > p.x ? 1 : -1, a = seg(tt, TB, TOE) * TAU; return { x: p.x + Math.cos(a) * t.orbitRadius * s, y: p.y - 34 + Math.sin(a) * 22 * s, depth: Math.sin(a) * s }; }
    return m;
  };
  const hist = [];
  return {
    kind: 'blade', done: false,
    update(dt) {
      if (G.t >= TO && !stopSpin) stopSpin = loopSound(out => { const n = AC.createBufferSource(); n.buffer = noiseBuf; n.loop = true; const f = AC.createBiquadFilter(); f.type = 'bandpass'; f.frequency.value = 1200; f.Q.value = 2.2; const g = AC.createGain(); g.gain.value = 0.14; const l = AC.createOscillator(), lg = AC.createGain(); l.frequency.value = 15; lg.gain.value = 0.1; l.connect(lg); lg.connect(g.gain); n.connect(f); f.connect(g); g.connect(out); n.start(); l.start(); return [n, l]; });
      const p = pos(G.t); this.x = p.x; this.y = p.y; this.depth = p.depth;
      hist.unshift({ x: p.x, y: p.y, t: G.t, r: this.rot() }); if (hist.length > 40) hist.pop();
      // golpes (una vez por pasada)
      const pass = G.t <= TH ? hitOut : G.t <= TB ? hitBack : hitOrbit;
      for (const e of alive()) {
        if (pass.has(e)) continue;
        if (Math.pow((p.x - e.x) / (e.rx + t.hitRadius), 2) + Math.pow((p.y - e.y) / (e.ry + t.hitRadius * 0.6), 2) < 1) {
          pass.add(e);
          const dx = p.x - last.x, k = Math.sign(dx) || Math.sign(e.x - G.player.x);
          damage(e, { dmg: t.damage, kx: k * t.knockback, slot, elem: fire ? 'fire' : null, burn: fire ? t.fireBurn : 0, shake: 3, hitstop: 0.03 });
          const hx = e.x, hy = e.y, back = dx < 0, P = pal;
          addFx(0.5, (ctx, layer, th) => {                     // tajo diagonal que se abre y se afina + chispas
            const u = seg(G.t, th, th + 0.22), len = 70 * easeOut(seg(u, 0, 0.35)), a = 1 - seg(u, 0.4, 1), ang = (back ? 1 : -1) * 0.55 + (bi ? 0.6 : 0);
            if (layer === 'front' && u < 1) {
              needle(ctx, hx - Math.cos(ang) * len / 2, hy - Math.sin(ang) * len / 2, hx + Math.cos(ang) * len / 2, hy + Math.sin(ang) * len / 2, 9 * (1 - u * 0.7), P.mid, a, layer);
              needle(ctx, hx - Math.cos(ang) * len / 2, hy - Math.sin(ang) * len / 2, hx + Math.cos(ang) * len / 2, hy + Math.sin(ang) * len / 2, 3.5 * (1 - u * 0.6), P.core, a, layer);
              glow(ctx, hx, hy, 40, P.g1, 0.6 * (1 - u));
            }
            sparks(ctx, G.t, th, hx, hy, layer, { n: 9, a0: (back ? Math.PI : 0) - 0.7, a1: (back ? Math.PI : 0) + 0.7, sp: 300, pal: P, seed: Math.floor(th * 1000), len: 14, life: 0.35 });
          });
          sfx(SND.moon_hit.fn);
        }
      }
      // estela de fuego
      if (fire && (!this.lastFire || G.t - this.lastFire > 0.028)) { this.lastFire = G.t; G.ents.push(fireBit(p.x, p.y, t.fireLife, t.fireBurn, slot)); }
      last = p;
      if (G.t >= TOE) {
        this.done = true; if (stopSpin) stopSpin(); onDone();
        sfx(SND.moon_catch.fn); G.player.squashT = 0.15;
        addFx(0.25, (ctx, layer, tc) => { const m = muzzle(), u = seg(G.t, tc, tc + 0.22); if (layer === 'front') { glow(ctx, m.x, m.y, 34, pal.g2, 0.8 * (1 - u)); star4(ctx, m.x, m.y, 20 * (1 + u * 0.4), 0.6, pal.core, 1 - u, layer); ellArc(ctx, m.x, m.y, 10 + 24 * easeOut(u), 10 + 24 * easeOut(u), 0, TAU, 2, pal.light, 1 - u, layer, { taper: 0 }); } });
        return false;
      }
      return true;
    },
    rot() { return (G.t - t0) * 29 + bi * 1.3; },
    draw(ctx, layer) {
      if (this.x === undefined) return;
      const behind = orbit && this.depth !== undefined && this.depth < 0, lay = behind ? 'back' : 'front', rot = this.rot();
      if (layer === 'back') for (const sgn of [-1, 1]) {      // estelas en espiral de las dos puntas
        const pts = hist.filter(h => G.t - h.t <= 0.075).map(h => ({ x: h.x + Math.cos(h.r + sgn * 1.9) * R0, y: h.y + Math.sin(h.r + sgn * 1.9) * R0 }));
        if (pts.length > 1) { ribbon(ctx, pts, s => 6 * Math.pow(1 - s, 1.3), pal.dark, 0.35, layer); ribbon(ctx, pts, s => 3.2 * (1 - s), pal.mid, 0.85, layer); ribbon(ctx, pts, s => 1.3 * (1 - s), pal.core, 1, layer); }
      }
      if (layer !== lay) return;
      for (const k of [3, 2, 1]) { const h = hist[k * 2]; if (h) crescentBlade(ctx, h.x, h.y, R0 * (1 - k * 0.06), h.r, pal, 0.18 / k + 0.06, layer); }
      for (const k of [1, 2]) crescentBlade(ctx, this.x, this.y, R0, rot - k * 0.32, pal, 0.22 / k, layer);
      crescentBlade(ctx, this.x, this.y, R0, rot, pal, 1, layer);
    },
  };
}
// trozo de estela de fuego colgado en el aire: quema a lo que lo toca
function fireBit(x, y, life, burn, slot) {
  const t0 = G.t, seed = Math.floor(t0 * 1000), R = rand(seed), ox = (R() - 0.5) * 10, lay = R() < 0.5 ? 'back' : 'front', h = 16 + R() * 12, w = 5 + R() * 3, ember = R() < 0.4;
  let tick = 0;
  return {
    kind: 'zone',
    update(dt) {
      tick -= dt;
      if (tick <= 0) { tick = 0.3; for (const e of alive()) if (Math.abs(e.x - x) < e.rx + 6 && Math.abs(e.y - y) < e.ry + 10) damage(e, { dmg: 2, elem: 'fire', burn, slot, silentFlash: true }); }
      return G.t - t0 < life;
    },
    draw(ctx, layer) {
      if (layer !== lay) return;
      const age = G.t - t0, a = (1 - age / life) * seg(age, 0, 0.05);
      flame(ctx, x + ox, y + 10, h * a, w * Math.max(0.4, a), G.t, seed, a, layer);
      if (ember) disc(ctx, x + ox * 2, y - age * 40, 1.6, PAL.fire.light, a, layer);
    },
  };
}
