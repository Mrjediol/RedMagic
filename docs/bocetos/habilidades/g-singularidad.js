// g-singularidad.js — 2 · Singularidad, versión jugable. Ficha y decisiones: docs/armas/singularidad.md
// Regla genérica (sinergia entre armas): los proyectiles del player marcados `capturable` que entran en su radio quedan orbitando
// y salen despedidos al colapsar, con todos sus efectos (Abanico con Hielo → esquirlas heladas en todas direcciones…).
'use strict';
addWeapon({
  id: 'sing', name: 'Singularidad', short: 'Singularidad', pal: 'void',
  mods: [
    { id: 'fire', name: 'Fuego', desc: 'El colapso quema a todo lo que alcanza (y rompe congelados: deshielo).' },
    { id: 'mass', name: 'Masa', desc: 'Más grande y dura más; arranca a los voladores y los estampa contra el suelo.' },
  ],
  tuning: {
    cooldown: [2.5, 0.3, 8, 0.1, 'Recarga (s)'], range: [360, 80, 800, 10, 'Distancia máxima (px)'], travelTime: [0.5, 0.15, 1.5, 0.05, 'Viaje (s)'],
    pullTime: [0.8, 0.2, 3, 0.05, 'Tiempo atrayendo (s)'], pullRadius: [150, 40, 400, 5, 'Radio de atracción'], pullSpeed: [150, 0, 500, 5, 'Fuerza de atracción (px/s)'],
    burstDamage: [40, 0, 200, 1, 'Daño del colapso'], burstRadius: [130, 30, 350, 5, 'Radio del colapso'], burstKnock: [380, 0, 900, 10, 'Empuje del colapso'],
    captureRadius: [95, 0, 250, 5, 'Radio de captura de proyectiles'], ejectSpeed: [900, 200, 2000, 10, 'Velocidad de salida de lo capturado'],
    burn: [3, 0, 8, 0.1, 'Fuego: quemadura (s)'], massScale: [1.4, 1, 2.5, 0.05, 'Masa: escala'], massTime: [1.35, 1, 3, 0.05, 'Masa: × tiempo atrayendo'],
  },
  create(slot) {
    const id = 'sing';
    let cd = 0;
    return {
      onPress(aim) {
        if (cd > 0) return;
        const t = T(id); cd = t.cooldown; G.player.castT = 0.25;
        const d = Math.min(t.range, Math.hypot(aim.tx - aim.x, aim.ty - aim.y)), tx = aim.x + aim.dx * d, ty = Math.min(GROUND - 30, aim.y + aim.dy * d);
        G.ents.push(voidOrbEnt(slot, aim.x, aim.y, tx, ty));
        sfx(SND.sing_launch.fn);
      },
      update(dt) { cd -= dt; },
      ready() { return 1 - cd / T(id).cooldown; },
      refund(s) { cd -= s; },
    };
  },
});

function voidOrbEnt(slot, x0, y0, tx, ty) {
  const t = T('sing'), mass = hasMod(slot, 'mass'), fire = hasMod(slot, 'fire');
  const s = mass ? t.massScale : 1, pull = t.pullTime * (mass ? t.massTime : 1), pal = PAL.void, bp = fire ? PAL.fire : PAL.set;
  const t0 = G.t, TA = t0 + t.travelTime, TC = TA + pull, TB = TC + 0.12;
  let stopHum = null, burst = false;
  const captured = [];
  const pos = () => { const u = easeOut(seg(G.t, t0, TA)); return { x: lerp(x0, tx, u), y: lerp(y0, ty, u) - 26 * bump(seg(G.t, t0, TA)) + (G.t > TA ? Math.sin(G.t * 5) * 2 : 0) }; };
  const hist = [];
  return {
    kind: 'singularity', x: x0, y: y0,
    update(dt) {
      const p = pos(); this.x = p.x; this.y = p.y;
      if (G.t < TA) { hist.unshift({ x: p.x, y: p.y, t: G.t }); if (hist.length > 30) hist.pop(); }
      if (G.t >= TA && G.t < TC) {
        if (!stopHum) stopHum = loopSound(out => {
          const a = AC.createOscillator(), b = AC.createOscillator(), f = AC.createBiquadFilter(), g = AC.createGain(), l = AC.createOscillator(), lg = AC.createGain();
          a.type = b.type = 'sawtooth'; a.frequency.value = 52 / s; b.frequency.value = 78.5 / s; f.type = 'lowpass'; f.frequency.value = 500; g.gain.value = 0.12;
          l.frequency.value = 6; lg.gain.value = 0.05; l.connect(lg); lg.connect(g.gain); a.connect(f); b.connect(f); f.connect(g); g.connect(out);
          f.frequency.linearRampToValueAtTime(1100, AC.currentTime + pull); a.start(); b.start(); l.start(); return [a, b, l];
        });
        // atrae enemigos
        const R = t.pullRadius * s;
        for (const e of alive()) {
          const d = Math.hypot(e.x - p.x, e.y - p.y); if (d > R) continue;
          e.pull = { x: p.x, y: p.y, s: t.pullSpeed * (1 - 0.4 * d / R), lift: mass && e.fly };
          if (mass && e.fly) e.captive = true;
        }
        // captura proyectiles del player
        for (const q of G.ents) {
          if (!q.capturable || q.captured || q.state !== 'fly' || q.lastSing === this) continue;
          if (Math.hypot(q.x - p.x, q.y - p.y) < t.captureRadius * s) {
            q.captured = this; q.lastSing = this; captured.push({ q, a: Math.atan2(q.y - p.y, q.x - p.x), r: 40 + Math.random() * 18, spin: 9 + Math.random() * 3 });
            if (!this._capSfx || G.t - this._capSfx > 0.3) { this._capSfx = G.t; sfx(SND.sing_capture.fn); }
          }
        }
      }
      if (G.t >= TC && stopHum) { stopHum(); stopHum = null; sfx(SND.sing_collapse.fn); }
      // lo capturado orbita (se cierra al colapsar)
      for (const c of captured) {
        const shrink = 1 - 0.6 * seg(G.t, TC, TB); c.a += c.spin * dt;
        c.q.x = p.x + Math.cos(c.a) * c.r * shrink; c.q.y = p.y + Math.sin(c.a) * c.r * 0.6 * shrink; c.q.ang = c.a + Math.PI / 2; c.q.depth = Math.sin(c.a);
      }
      if (G.t >= TB && !burst) {
        burst = true;
        const hit = damageArea(p.x, p.y, t.burstRadius * s, { dmg: t.burstDamage, kx: t.burstKnock, ky: -260, heavy: true, slot, elem: fire ? 'fire' : null, burn: fire ? t.burn : 0, hitstop: 0.07, shake: 9 * s, frame: true });
        if (mass) for (const e of hit) if (e.fly) { e.slammed = true; e.vy = 700; }
        for (const c of captured) { const q = c.q, sp = t.ejectSpeed; q.captured = null; q.ejected = true; q.age = 0; q.hit = new Set(); q.vx = Math.cos(c.a) * sp; q.vy = Math.sin(c.a) * sp; q.ang = c.a; q.trail = []; }
        const bx = p.x, by = p.y;
        addFx(1.6, (ctx, layer, tb) => voidBurst(ctx, G.t, layer, tb, bx, by, s, bp, { mass, fire }));
        sfx(fire ? SND.sing_burst_fire.fn : SND.sing_burst.fn);
        if (captured.length) sfx(SND.sing_eject.fn);
        return false;
      }
      return true;
    },
    draw(ctx, layer) {
      voidOrb(ctx, G.t, layer, this.x, this.y, { s, pal, bp, form: easeOut(seg(G.t, t0, TA)), collapse: seg(G.t, TC, TB), pullT: G.t >= TA && G.t < TC ? G.t - TA : -1, liveT: G.t - t0,
        trail: G.t < TA ? [0.04, 0.08, 0.12].map(dt => hist.find(h => G.t - h.t >= dt)).filter(Boolean) : [] });
      if (G.settings.hitboxes && G.t >= TA) { ctx.strokeStyle = 'rgba(160,110,255,.6)'; ctx.beginPath(); ctx.arc(this.x, this.y, T('sing').pullRadius * s, 0, TAU); ctx.stroke(); }
    },
  };
}
