// g-singularidad.js — 2 · Singularidad, versión jugable. Ficha y decisiones: docs/armas/singularidad.md
// Regla genérica (sinergia entre armas): los proyectiles del player marcados `capturable` que entran en su radio quedan orbitando
// y salen despedidos al colapsar, con todos sus efectos (Abanico con hielo → esquirlas heladas en todas direcciones…).
// Elemento (item): fuego = estallido ígneo que quema; hielo = el colapso congela a todos los atrapados.
// Duplicador (item): salen dos orbes que se buscan al llegar y se funden en uno mayor.
'use strict';
addWeapon({
  id: 'sing', name: 'Singularidad', short: 'Singularidad', pal: 'void',
  // mejoras provisionales (el árbol con evoluciones está pendiente de diseñar, como el del Abanico)
  ups: [
    { id: 'mass', name: 'Masa', desc: 'Más grande y dura más; arranca a los voladores y los estampa contra el suelo.' },
  ],
  tuning: {
    cooldown: [2.5, 0.3, 8, 0.1, 'Recarga (s)'], range: [360, 80, 800, 10, 'Distancia máxima (px)'], travelTime: [0.5, 0.15, 1.5, 0.05, 'Viaje (s)'],
    pullTime: [0.8, 0.2, 3, 0.05, 'Tiempo atrayendo (s)'], pullRadius: [150, 40, 400, 5, 'Radio de atracción'], pullSpeed: [150, 0, 500, 5, 'Fuerza de atracción (px/s)'],
    burstDamage: [40, 0, 200, 1, 'Daño del colapso'], burstRadius: [130, 30, 350, 5, 'Radio del colapso'], burstKnock: [380, 0, 900, 10, 'Empuje del colapso'],
    captureRadius: [95, 0, 250, 5, 'Radio de captura de proyectiles'], ejectSpeed: [900, 200, 2000, 10, 'Velocidad de salida de lo capturado'],
    burn: [3, 0, 8, 0.1, 'Fuego: quemadura (s)'], massScale: [1.4, 1, 2.5, 0.05, 'Masa: escala'], massTime: [1.35, 1, 3, 0.05, 'Masa: × tiempo atrayendo'],
    mergeTime: [0.3, 0.05, 1, 0.05, 'Duplicador: tiempo de fusión (s)'], mergeScale: [1.35, 1, 2.5, 0.05, 'Duplicador: escala de la fusionada'], mergeDamage: [1.7, 1, 4, 0.05, 'Duplicador: × daño de la fusionada'],
  },
  create(slot) {
    const id = 'sing';
    let cd = 0;
    return {
      onPress(aim) {
        if (cd > 0) return;
        const t = T(id); cd = t.cooldown * cdMul(); G.player.castT = 0.25;
        const d = Math.min(t.range, Math.hypot(aim.tx - aim.x, aim.ty - aim.y)), tx = aim.x + aim.dx * d, ty = Math.min(GROUND - 30, aim.y + aim.dy * d);
        G.ents.push(voidOrbEnt(slot, aim.x, aim.y, tx, ty, copies() > 1));
        sfx(SND.sing_launch.fn);
      },
      update(dt) { cd -= dt; },
      ready() { return 1 - cd / (T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
  },
});

function voidOrbEnt(slot, x0, y0, tx, ty, twin) {
  const t = T('sing'), mass = hasMod(slot, 'mass'), el = elemOf();
  const s = (mass ? t.massScale : 1) * (twin ? t.mergeScale : 1), s0 = mass ? t.massScale : 1, pull = t.pullTime * (mass ? t.massTime : 1);
  const pal = PAL.void, bp = el ? PAL[el] : PAL.set;
  const t0 = G.t, TA = t0 + t.travelTime, TM = TA + (twin ? t.mergeTime : 0), TC = TM + pull, TB = TC + 0.12;
  const dmgMul = twin ? t.mergeDamage : 1;
  let stopHum = null, burst = false;
  const captured = [];
  const dx = tx - x0, dy = ty - y0, L = Math.hypot(dx, dy) || 1, px = -dy / L, py = dx / L;
  // posición de cada orbe (k = -1 / +1 con Duplicador; 0 sin él) y del orbe ya fundido
  const orbPos = k => {
    const u = easeOut(seg(G.t, t0, TA)), bow = 46 * k * Math.sin(Math.PI * Math.min(1, u * 1.1));
    if (G.t < TA) return { x: lerp(x0, tx, u) + px * bow, y: lerp(y0, ty, u) + py * bow - 26 * bump(seg(G.t, t0, TA)) };
    if (G.t < TM) { const m = seg(G.t, TA, TM), a = (G.t - TA) * 16 + (k > 0 ? 0 : Math.PI), r = 30 * (1 - easeIn(m)); return { x: tx + Math.cos(a) * r, y: ty + Math.sin(a) * r * 0.6 }; }
    return { x: tx, y: ty + Math.sin(G.t * 5) * 2 };
  };
  const hist = [[], []];
  return {
    kind: 'singularity', x: x0, y: y0,
    update(dt) {
      const p = orbPos(0); this.x = p.x; this.y = p.y;
      if (G.t < TA) (twin ? [-1, 1] : [0]).forEach((k, i) => { const q = orbPos(k); hist[i].unshift({ x: q.x, y: q.y, t: G.t }); if (hist[i].length > 30) hist[i].pop(); });
      if (twin && !this.merged && G.t >= TM) {               // fusión: destello y onda corta
        this.merged = true; G.shake += 4;
        sfx(k => { const tn = nowT(); vThump(tn, 70, 35, 0.4 * k, 0.6, 2, 0.3); vNoise(tn, 0.3 * k, 0.25, 'bandpass', 300, 2500, 1, { rising: true, wet: 0.3 }); });
        addFx(0.6, (ctx, layer, tm) => { const u = seg(G.t, tm, tm + 0.4); if (layer === 'front') { glow(ctx, tx, ty, 90 * s, bp.g2, 0.8 * (1 - u)); star4(ctx, tx, ty, 50 * s * (1 - u * 0.5), 0.3, '#ffffff', 1 - u, layer); } ellArc(ctx, tx, ty, (20 + 80 * easeOut(u)) * s, (20 + 80 * easeOut(u)) * s * 0.9, layer === 'back' ? Math.PI : 0, layer === 'back' ? TAU : Math.PI, 3 * (1 - u), PAL.void.light, 1 - u, layer, { taper: 0, wob: 0.1, seed: 4 }); });
      }
      if (G.t >= TM && G.t < TC) {
        if (!stopHum) stopHum = loopSound(out => {
          const a = AC.createOscillator(), b = AC.createOscillator(), f = AC.createBiquadFilter(), g = AC.createGain(), l = AC.createOscillator(), lg = AC.createGain();
          a.type = b.type = 'sawtooth'; a.frequency.value = 52 / s; b.frequency.value = 78.5 / s; f.type = 'lowpass'; f.frequency.value = 500; g.gain.value = 0.12;
          l.frequency.value = 6; lg.gain.value = 0.05; l.connect(lg); lg.connect(g.gain); a.connect(f); b.connect(f); f.connect(g); g.connect(out);
          f.frequency.linearRampToValueAtTime(1100, AC.currentTime + pull); a.start(); b.start(); l.start(); return [a, b, l];
        });
        const R = t.pullRadius * s;
        for (const e of alive()) {
          const d = Math.hypot(e.x - p.x, e.y - p.y); if (d > R) continue;
          e.pull = { x: p.x, y: p.y, s: t.pullSpeed * (1 - 0.4 * d / R), lift: mass && e.fly };
        }
        for (const q of G.ents) {                             // captura proyectiles del player
          if (!q.capturable || q.captured || q.state !== 'fly' || q.lastSing === this) continue;
          if (Math.hypot(q.x - p.x, q.y - p.y) < t.captureRadius * s) {
            q.captured = this; q.lastSing = this; captured.push({ q, a: Math.atan2(q.y - p.y, q.x - p.x), r: (40 + Math.random() * 18) * Math.min(1.4, s), spin: 9 + Math.random() * 3 });
            if (!this._capSfx || G.t - this._capSfx > 0.3) { this._capSfx = G.t; sfx(SND.sing_capture.fn); }
          }
        }
      }
      if (G.t >= TC && stopHum) { stopHum(); stopHum = null; sfx(SND.sing_collapse.fn); }
      for (const c of captured) {
        const shrink = 1 - 0.6 * seg(G.t, TC, TB); c.a += c.spin * dt;
        c.q.x = p.x + Math.cos(c.a) * c.r * shrink; c.q.y = p.y + Math.sin(c.a) * c.r * 0.6 * shrink; c.q.ang = c.a + Math.PI / 2; c.q.depth = Math.sin(c.a);
      }
      if (G.t >= TB && !burst) {
        burst = true;
        const hit = damageArea(p.x, p.y, t.burstRadius * s, { dmg: t.burstDamage * dmgMul, kx: t.burstKnock, ky: -260, heavy: true, slot, elem: el, burn: el === 'fire' ? t.burn : 0, freeze: el === 'ice' ? 1 : 0, hitstop: 0.07, shake: 9 * s, frame: true });
        if (mass) for (const e of hit) if (e.fly) { e.slammed = true; e.vy = 700; }
        for (const c of captured) { const q = c.q, sp = t.ejectSpeed; q.captured = null; q.ejected = true; q.age = 0; q.hit = new Set(); q.vx = Math.cos(c.a) * sp; q.vy = Math.sin(c.a) * sp; q.ang = c.a; q.trail = []; }
        const bx = p.x, by = p.y;
        addFx(1.6, (ctx, layer, tb) => voidBurst(ctx, G.t, layer, tb, bx, by, s, bp, { mass, fire: el === 'fire' }));
        if (el === 'ice') addFx(1.6, (ctx, layer, tb) => frostNova(ctx, G.t, layer, tb, bx, t.burstRadius * s * 0.8));
        sfx(el === 'fire' ? SND.sing_burst_fire.fn : el === 'ice' ? SND.seed_nova.fn : SND.sing_burst.fn);
        if (captured.length) sfx(SND.sing_eject.fn);
        return false;
      }
      return true;
    },
    draw(ctx, layer) {
      const live = { pal, bp, pullT: G.t >= TM && G.t < TC ? G.t - TM : -1, liveT: G.t - t0, collapse: seg(G.t, TC, TB) };
      if (twin && G.t < TM) {                                 // dos orbes (más pequeños) que se buscan
        [-1, 1].forEach((k, i) => { const q = orbPos(k); voidOrb(ctx, G.t, layer, q.x, q.y, Object.assign({}, live, { s: s0, form: easeOut(seg(G.t, t0, TA)), trail: G.t < TA ? [0.04, 0.08, 0.12].map(dt => hist[i].find(h => G.t - h.t >= dt)).filter(Boolean) : [] })); });
        if (G.t >= TA && layer === 'front') { const m = seg(G.t, TA, TM); glow(ctx, tx, ty, 50 * s * m, bp.g2, 0.6 * m); }
      } else {
        voidOrb(ctx, G.t, layer, this.x, this.y, Object.assign({}, live, { s, form: twin ? 1 : easeOut(seg(G.t, t0, TA)), trail: !twin && G.t < TA ? [0.04, 0.08, 0.12].map(dt => hist[0].find(h => G.t - h.t >= dt)).filter(Boolean) : [] }));
      }
      if (G.settings.hitboxes && G.t >= TM) { ctx.strokeStyle = 'rgba(160,110,255,.6)'; ctx.beginPath(); ctx.arc(this.x, this.y, t.pullRadius * s, 0, TAU); ctx.stroke(); }
    },
  };
}
