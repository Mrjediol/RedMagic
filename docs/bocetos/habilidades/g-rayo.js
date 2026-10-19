// g-rayo.js — 3 · Rayo, versión jugable. Ficha y decisiones: docs/armas/rayo.md
// Mantener = canalizar (con calor: al llegar al máximo se corta solo). Quema por ticks al primer enemigo del haz.
'use strict';
addWeapon({
  id: 'ray', name: 'Rayo', short: 'Rayo', pal: 'set',
  mods: [
    { id: 'prism', name: 'Prisma', desc: 'Atraviesa al primero y se divide en haces dorados hacia los enemigos más cercanos.' },
    { id: 'ice', name: 'Hielo', desc: 'Congela poco a poco; al soltar, lo congelado estalla (añicos).' },
    { id: 'life', name: 'Robo de vida', desc: 'Una parte del daño cura al player.' },
  ],
  tuning: {
    maxChannel: [2.0, 0.3, 6, 0.1, 'Canalización máxima (s)'], cooldown: [0.8, 0, 4, 0.05, 'Recarga tras soltar (s)'], range: [520, 100, 900, 10, 'Alcance (px)'],
    turnRate: [9, 1, 40, 0.5, 'Giro hacia el ratón (rad/s)'], tick: [0.1, 0.03, 0.5, 0.01, 'Cada cuánto quema (s)'], tickDamage: [6, 1, 40, 1, 'Daño por tick'],
    push: [18, 0, 120, 1, 'Empuje por tick'], prismCount: [2, 1, 5, 1, 'Prisma: haces'], prismRange: [300, 50, 600, 10, 'Prisma: alcance'],
    prismMul: [0.6, 0.1, 1.5, 0.05, 'Prisma: daño de cada haz (×)'], freezeTick: [0.07, 0, 0.5, 0.01, 'Hielo: congelación por tick'],
    lifesteal: [0.3, 0, 1, 0.01, 'Robo de vida: fracción del daño'],
  },
  create(slot) {
    const id = 'ray';
    let cd = 0, beam = null;
    const stop = aim => {
      if (!beam) return;
      beam.release(); beam = null; cd = T(id).cooldown;
    };
    return {
      onPress(aim) { if (cd > 0 || beam) return; beam = rayBeam(slot, aim); G.ents.push(beam); },
      onHold(aim, dt) { if (beam) { beam.aim(aim, dt); if (beam.heat >= T(id).maxChannel) stop(aim); } },
      onRelease(aim) { stop(aim); },
      update(dt, current) { cd -= dt; if (beam && !current) stop(); },
      ready() { return beam ? 1 - beam.heat / T(id).maxChannel : 1 - cd / Math.max(0.01, T(id).cooldown); },
      refund(s) { cd -= s; },
    };
  },
});

function rayBeam(slot, aim0) {
  const t = T('ray'), ice = hasMod(slot, 'ice'), life = hasMod(slot, 'life'), prism = hasMod(slot, 'prism');
  const pal = ice ? PAL.ice : PAL.set, gp = PAL.gold;
  const t0 = G.t;
  let ang = aim0.ang, tickT = 0, released = -1, x1 = aim0.x, y1 = aim0.y, first = null, subs = [], touched = new Set(), healAcc = 0;
  sfx(SND.ray_ignite.fn);
  const stopLoop = loopSound(out => {
    const mk = (f, g, type = 'sawtooth') => { const o = AC.createOscillator(), gg = AC.createGain(), lp = AC.createBiquadFilter(); o.type = type; o.frequency.value = f; lp.type = 'lowpass'; lp.frequency.value = 1500; gg.gain.value = g; o.connect(lp); lp.connect(gg); gg.connect(out); o.start(); return o; };
    const a = mk(110, 0.07), b = mk(111.7, 0.06), c = mk(220, 0.025);
    const n = AC.createBufferSource(); n.buffer = noiseBuf; n.loop = true; const bp = AC.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = 3000; bp.Q.value = 2; const ng = AC.createGain(); ng.gain.value = 0.06; n.connect(bp); bp.connect(ng); ng.connect(out); n.start();
    return [a, b, c, n];
  });
  const ent = {
    kind: 'beam', heat: 0,
    aim(aim, dt) { let d = ((aim.ang - ang + Math.PI * 3) % TAU) - Math.PI; ang += clamp(d, -t.turnRate * dt, t.turnRate * dt); },
    release() {
      if (released >= 0) return; released = G.t; stopLoop(); sfx(SND.ray_off.fn);
      if (ice) for (const e of touched) if (!e.dead && e.frozenT > 0) damage(e, { dmg: 0, heavy: true, slot });
    },
    update(dt) {
      if (released < 0) { this.heat += dt; G.player.castT = 0.1; }
      const m = muzzle(), ex = m.x + Math.cos(ang) * t.range, ey = m.y + Math.sin(ang) * t.range;
      // hasta dónde llega: primer enemigo, el suelo o el alcance
      const h = raycastEnemy(m.x, m.y, ex, ey);
      let gx = ex, gy = ey;
      if (ey > GROUND) { const u = (GROUND - m.y) / (ey - m.y); gx = lerp(m.x, ex, u); gy = GROUND; }
      if (h && (!(ey > GROUND) || h.u < (GROUND - m.y) / (ey - m.y))) { x1 = h.x; y1 = h.y; first = h.e; } else { x1 = gx; y1 = gy; first = null; }
      this.x0 = m.x; this.y0 = m.y;
      // prisma: sale por detrás del primero y se divide
      subs = [];
      if (prism && first && released < 0) {
        const px = first.x + Math.cos(ang) * first.rx, py = first.y + Math.sin(ang) * first.ry, skip = new Set([first]);
        for (let i = 0; i < t.prismCount; i++) { const n = nearestEnemy(px, py, t.prismRange, skip); if (!n) break; skip.add(n); subs.push({ e: n, x0: px, y0: py }); }
        this.px = px; this.py = py;
      }
      if (released < 0) {
        tickT -= dt;
        if (tickT <= 0) {
          tickT = t.tick;
          const hitOne = (e, mul) => {
            touched.add(e);
            const d = damage(e, { dmg: t.tickDamage * mul, kx: Math.sign(Math.cos(ang)) * t.push, slot, elem: ice ? 'ice' : null, freeze: ice ? t.freezeTick : 0, silentFlash: Math.random() < 0.5 });
            if (life && d > 0) { healAcc += d * t.lifesteal; if (healAcc >= 1) { const hv = Math.floor(healAcc); healAcc -= hv; if (healPlayer(hv) > 0) text(G.player.x, G.player.y - 92, '+' + hv, '#ff9fb4'); } }
          };
          if (first) hitOne(first, 1);
          for (const s of subs) hitOne(s.e, t.prismMul);
          if (first) sfx(k => { const tt = nowT(); vThump(tt, 130, 70, 0.07 * k, 0.16, 1.5, 0.05); });
        }
        return true;
      }
      return G.t - released < 0.6;
    },
    draw(ctx, layer) {
      const env = easeOut(seg(G.t, t0, t0 + 0.08)) * (released < 0 ? 1 : 1 - easeIn(seg(G.t, released, released + 0.14)));
      const m = { x: this.x0 || aim0.x, y: this.y0 || aim0.y };
      if (env > 0.01) {
        const reach = easeOut(seg(G.t, t0, t0 + 0.07)), tx = lerp(m.x, x1, reach), ty = lerp(m.y, y1, reach);
        energyBeam(ctx, G.t, layer, m.x, m.y, tx, ty, env, pal);
        if (layer === 'front') { glow(ctx, m.x, m.y, 34 * env, pal.g1, 0.7 * env); star4(ctx, m.x, m.y, 16 * env, G.t * 7, pal.core, env, layer); }
        if (reach >= 1) { const s0 = t0 + 0.07, al = s0 + Math.max(0, Math.ceil((G.t - 0.45 - s0) / 0.035)) * 0.035; beamContact(ctx, G.t, layer, x1, y1, env, pal, al, released < 0 ? G.t : released, 1, 1); }
        for (const [i, s] of subs.entries()) {
          if (s.e.dead) continue;
          energyBeam(ctx, G.t, layer, s.x0, s.y0, s.e.x - Math.sign(s.e.x - s.x0) * s.e.rx * 0.8, s.e.y, env * 0.95, gp, { sc: 0.5, seed: i * 2.1 });
          beamContact(ctx, G.t, layer, s.e.x - Math.sign(s.e.x - s.x0) * s.e.rx * 0.8, s.e.y, env, gp, Math.ceil((G.t - 0.4) / 0.035) * 0.035, G.t, 0.6, 10 + i);
        }
        if (subs.length) { if (layer === 'front') glow(ctx, first ? first.x : this.px, this.py, 34, gp.g2, 0.5 * env); prismGem(ctx, G.t, this.px, this.py, env, gp, layer); }
        if (life && first) { lifeMotes(ctx, G.t, layer, x1, y1, m.x, m.y, env); healCrosses(ctx, G.t - t0, layer, G.player.x, G.player.y, env); }
      }
      if (released >= 0) {                                   // restos que flotan sobre la línea del haz
        const R = rand(61), u = seg(G.t, released, released + 0.6);
        for (let i = 0; i < 16; i++) { const k = R(), x = lerp(m.x, x1, k), y = lerp(m.y, y1, k), lay = R() < 0.5 ? 'back' : 'front', dy = -(10 + R() * 26) * easeOut(u);
          if (lay === layer) disc(ctx, x + (R() - 0.5) * 12 * u, y + dy, (1 + R() * 1.8) * (1 - u), i % 3 ? pal.light : pal.core, 1 - u, layer); }
      }
    },
  };
  G.player.castT = 0.2;
  return ent;
}
