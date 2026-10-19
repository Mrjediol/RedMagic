// g-singularidad.js — 2 · Singularidad (Grieta), versión jugable. Ficha y decisiones: docs/armas/singularidad.md · progresión: progresion.md
// Viaja un orbe y abre una grieta en el sitio que atrae enemigos y CAPTURA proyectiles del player (regla genérica: lo capturable que
// entra en su radio orbita y sale despedido al colapsar, con sus efectos). Árbol: base → Fauces / Errante / Arsenal → 3 mejoras.
// Elemento (sinergia 4): fuego = estallido ígneo que quema; hielo = el colapso congela. Duplicador: dos orbes que se funden en una grieta mayor.
'use strict';
addWeapon({
  id: 'sing', name: 'Singularidad', short: 'Singularidad', pal: 'void',
  archetypes: [
    { id: 'maw', name: 'Fauces', desc: 'Devora: lo que llega a la grieta recibe daño continuo y desaparece dentro.',
      ups: [{ id: 'hunger', name: 'Hambre', desc: 'Cada baja alarga la grieta y la hace crecer.' },
            { id: 'swallow', name: 'Digestión', desc: 'Se traga de golpe a los que tienen poca vida.' },
            { id: 'regurg', name: 'Regurgitar', desc: 'Al colapsar escupe un proyectil teledirigido por cada baja.' }] },
    { id: 'drift', name: 'Errante', desc: 'Mientras atrae, la grieta se desplaza hacia el ratón arrastrando a los enemigos.',
      ups: [{ id: 'hunt', name: 'Cazadora', desc: 'Va sola, más rápido, a por el enemigo más cercano.' },
            { id: 'trail', name: 'Rastro', desc: 'Deja grietas pequeñas por el camino que atraen y dañan un momento.' },
            { id: 'mass', name: 'Masa', desc: 'Más grande y dura más; arranca a los voladores y los estampa contra el suelo.' }] },
    { id: 'arsenal', name: 'Arsenal', desc: 'Captura más lejos; lo capturado sale reforzado y APUNTADO a los enemigos, no en todas direcciones.',
      ups: [{ id: 'reload', name: 'Recarga', desc: 'Cada proyectil capturado recarga un poco tu otra arma.' },
            { id: 'multiply', name: 'Multiplicar', desc: 'Cada proyectil capturado sale doble.' },
            { id: 'shield', name: 'Escudo de vacío', desc: 'También captura las balas enemigas y las devuelve como tuyas.' }] },
  ],
  tuning: {
    cooldown: [3.5, 0.3, 10, 0.1, 'Base: recarga (s)'], range: [360, 80, 800, 10, 'Base: distancia máxima (px)'], travelTime: [0.5, 0.15, 1.5, 0.05, 'Base: viaje (s)'],
    pullTime: [1.8, 0.2, 5, 0.05, 'Base: tiempo atrayendo (s)'], pullRadius: [150, 40, 400, 5, 'Base: radio de atracción'], pullSpeed: [150, 0, 500, 5, 'Base: fuerza de atracción (px/s)'],
    burstDamage: [40, 0, 200, 1, 'Base: daño del colapso'], burstRadius: [130, 30, 350, 5, 'Base: radio del colapso'], burstKnock: [380, 0, 900, 10, 'Base: empuje del colapso'],
    captureRadius: [55, 0, 250, 5, 'Base: radio de captura de proyectiles'], ejectSpeed: [900, 200, 2000, 10, 'Base: velocidad de salida de lo capturado'],
    burn: [3, 0, 8, 0.1, 'Fuego: quemadura (s)'],
    devourRadius: [40, 10, 120, 2, 'Fauces: radio de la boca'], devourDps: [70, 0, 200, 1, 'Fauces: daño por segundo dentro'],
    hungerTime: [0.5, 0, 2, 0.05, 'Fauces · Hambre: + s por baja'], hungerGrow: [0.1, 0, 0.5, 0.01, 'Fauces · Hambre: + tamaño por baja'], hungerMax: [1.6, 1, 3, 0.05, 'Fauces · Hambre: tamaño máximo'],
    swallowPct: [0.35, 0, 0.6, 0.01, 'Fauces · Digestión: traga por debajo de (× vida)'], regurgDamage: [45, 1, 150, 1, 'Fauces · Regurgitar: daño de cada proyectil'], regurgRadius: [55, 0, 150, 5, 'Fauces · Regurgitar: radio del estallido al impactar'],
    driftSpeed: [80, 10, 400, 5, 'Errante: velocidad (px/s)'], huntSpeed: [130, 10, 500, 5, 'Errante · Cazadora: velocidad'],
    trailEvery: [0.35, 0.1, 1.5, 0.05, 'Errante · Rastro: cada (s)'], trailLife: [1.2, 0.2, 4, 0.1, 'Errante · Rastro: duración (s)'], trailDps: [10, 0, 60, 1, 'Errante · Rastro: daño/s'],
    massScale: [1.4, 1, 2.5, 0.05, 'Errante · Masa: escala'], massTime: [1.35, 1, 3, 0.05, 'Errante · Masa: × tiempo atrayendo'],
    arsenalRadius: [1.25, 1, 3, 0.05, 'Arsenal: × radio de captura'], arsenalMul: [1.5, 1, 4, 0.05, 'Arsenal: × daño de lo capturado'],
    reloadRefund: [0.2, 0, 1, 0.01, 'Arsenal · Recarga: s por captura'], shotDamage: [12, 1, 60, 1, 'Arsenal · Escudo: daño de una bala devuelta'],
    look: [3, 0, 5, 1, 'Aspecto: 0 anillas · 1 horizonte · 2 remolino · 3 grieta · 4 abismo · 5 lente'], over: [0, 0, 1, 1, 'Vórtice por encima de los enemigos (0 = no, 1 = sí)'],
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
  const t = T('sing'), el = elemOf(), arch = slot.arch;
  const maw = arch === 'maw', drift = arch === 'drift', arsenal = arch === 'arsenal', mass = hasMod(slot, 'mass');
  const s0 = mass ? t.massScale : 1;
  let grow = 1;                                               // Fauces · Hambre: crece con cada baja
  const sNow = () => s0 * (twin ? t.mergeScale : 1) * grow;
  const pull = t.pullTime * (mass ? t.massTime : 1);
  const pal = PAL.void, bp = el ? PAL[el] : PAL.set;
  const t0 = G.t, TA = t0 + t.travelTime, TM = TA + (twin ? t.mergeTime : 0);
  let TC = TM + pull, TB = TC + 0.12, extra = 0;               // TC/TB se alargan con Hambre
  const dmgMul = twin ? t.mergeDamage : 1;
  let stopHum = null, burst = false, feeds = 0, cx = tx, cy = ty, trailT = 0, devourT = 0;
  const captured = [];
  const dx = tx - x0, dy = ty - y0, L = Math.hypot(dx, dy) || 1, px = -dy / L, py = dx / L;
  const orbPos = k => {                                       // k = -1 / +1 con Duplicador (dos orbes); 0 = la grieta
    const u = easeOut(seg(G.t, t0, TA)), bow = 46 * k * Math.sin(Math.PI * Math.min(1, u * 1.1));
    if (G.t < TA) return { x: lerp(x0, tx, u) + px * bow, y: lerp(y0, ty, u) + py * bow - 26 * bump(seg(G.t, t0, TA)) };
    if (G.t < TM) { const m = seg(G.t, TA, TM), a = (G.t - TA) * 16 + (k > 0 ? 0 : Math.PI), r = 30 * (1 - easeIn(m)); return { x: tx + Math.cos(a) * r, y: ty + Math.sin(a) * r * 0.6 }; }
    return { x: cx, y: cy + Math.sin(G.t * 5) * 2 };
  };
  const onFeed = e => {                                       // Fauces: una baja dentro alimenta a la grieta
    feeds++;
    if (hasMod(slot, 'hunger') && extra < 2) { const add = Math.min(t.hungerTime, 2 - extra); extra += add; TC += add; TB += add; grow = Math.min(t.hungerMax, grow + t.hungerGrow); }
    const fx = e.x, fy = e.y;
    addFx(0.5, (ctx, layer, tf) => { const u = seg(G.t, tf, tf + 0.4); if (layer === 'front') { glow(ctx, cx, cy, 60 * sNow() * (1 - u), [160, 110, 255], 0.8 * (1 - u)); for (let i = 0; i < 6; i++) { const a = i / 6 * TAU, r = 30 * (1 - u); disc(ctx, lerp(fx, cx, u) + Math.cos(a) * r, lerp(fy, cy, u) + Math.sin(a) * r, 2.5 * (1 - u), '#cdbcff', 1 - u, layer); } } });
    sfx(k => { const tn = nowT(); vThump(tn, 60, 30, 0.35 * k, 0.55, 2, 0.3); vNoise(tn, 0.25 * k, 0.2, 'lowpass', 1200, 200, 1, { wet: 0.3 }); });
  };
  const hist = [[], []];
  return {
    kind: 'singularity', x: x0, y: y0,
    update(dt) {
      const s = sNow();
      // Errante: se desplaza hacia el ratón (o, con Cazadora, hacia el enemigo más cercano) mientras atrae
      if (drift && G.t >= TM && G.t < TC) {
        const tgt = hasMod(slot, 'hunt') ? nearestEnemy(cx, cy, 600) : null, gx = tgt ? tgt.x : G.mouse.x, gy = tgt ? tgt.y : G.mouse.y;
        const sp = (tgt ? t.huntSpeed : t.driftSpeed) * dt, ddx = gx - cx, ddy = gy - cy, dd = Math.hypot(ddx, ddy);
        if (dd > 4) { cx += ddx / dd * Math.min(sp, dd); cy += ddy / dd * Math.min(sp, dd); }
        cy = clamp(cy, 60, GROUND - 30);
        if (hasMod(slot, 'trail')) { trailT -= dt; if (trailT <= 0) { trailT = t.trailEvery; G.ents.push(miniRift(slot, cx, cy, t.trailLife, t.trailDps)); } }
      }
      const p = orbPos(0); this.x = p.x; this.y = p.y;
      if (!this.opened && G.t >= TM) { this.opened = true; sfx(SND.sing_tear.fn); }   // la grieta se abre en el sitio
      if (G.t < TA) (twin ? [-1, 1] : [0]).forEach((k, i) => { const q = orbPos(k); hist[i].unshift({ x: q.x, y: q.y, t: G.t }); if (hist[i].length > 30) hist[i].pop(); });
      if (twin && !this.merged && G.t >= TM) {               // fusión: destello y onda corta
        this.merged = true; G.shake += 4;
        sfx(k => { const tn = nowT(); vThump(tn, 70, 35, 0.4 * k, 0.6, 2, 0.3); vNoise(tn, 0.3 * k, 0.25, 'bandpass', 300, 2500, 1, { rising: true, wet: 0.3 }); });
        addFx(0.6, (ctx, layer, tm) => { const u = seg(G.t, tm, tm + 0.4); if (layer === 'front') { glow(ctx, tx, ty, 90 * s, bp.g2, 0.8 * (1 - u)); star4(ctx, tx, ty, 50 * s * (1 - u * 0.5), 0.3, '#ffffff', 1 - u, layer); } });
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
          e.pull = { x: p.x, y: p.y, s: t.pullSpeed * (1 - 0.4 * d / R) * (drift ? 1.25 : 1), lift: (mass || drift) && e.fly };
          e.suck = { x: p.x, y: p.y, k: Math.min(1, (1 - d / R) * 1.4) * seg(G.t, TM, TM + 0.25), t: G.t };
        }
        // Fauces: la boca devora (daño continuo) y, con Digestión, traga a los débiles
        if (maw) {
          devourT -= dt;
          if (devourT <= 0) {
            devourT = 0.2;
            for (const e of alive()) {
              if (Math.hypot(e.x - p.x, e.y - p.y) > t.devourRadius * s + Math.max(e.rx, e.ry) * 0.5) continue;
              if (hasMod(slot, 'swallow') && e.hp < e.hpMax * t.swallowPct) { text(e.x, e.y - e.ry - 30, 'TRAGADO', '#cdbcff', true); hurtEnemy(e, e.hp + 1, '#cdbcff', true); onFeed(e); continue; }
              damage(e, { dmg: t.devourDps * 0.2, slot, dot: true, silentFlash: true, color: '#cdbcff', onKill: onFeed });
            }
          }
        }
        // captura: proyectiles del player (y balas enemigas con Escudo de vacío)
        const capR = t.captureRadius * s * (arsenal ? t.arsenalRadius : 1);
        for (const q of G.ents) {
          if (!q.capturable || q.captured || q.state !== 'fly' || q.lastSing === this) continue;
          if (q.hostile && !(arsenal && hasMod(slot, 'shield'))) continue;
          if (Math.hypot(q.x - p.x, q.y - p.y) < capR) {
            const d0 = Math.hypot(q.x - p.x, q.y - p.y), qx = q.x, qy = q.y;
            q.captured = this; q.lastSing = this; captured.push({ q, a: Math.atan2((q.y - p.y) / 0.6, q.x - p.x), r: (40 + Math.random() * 18) * Math.min(1.4, s), r0: d0, tc: G.t, spin: 9 + Math.random() * 3 });
            addFx(0.3, (ctx, layer, tf) => { if (layer === 'front') { const u = seg(G.t, tf, tf + 0.25); star4(ctx, qx, qy, 12 * (1 - u), 0.6, '#cdbcff', 1 - u, layer); glow(ctx, qx, qy, 18 * (1 - u), [160, 110, 255], 0.6 * (1 - u)); } });
            if (arsenal && hasMod(slot, 'reload')) for (const so of otherSlots(slot)) if (so.inst && so.inst.refund) so.inst.refund(t.reloadRefund);
            if (!this._capSfx || G.t - this._capSfx > 0.3) { this._capSfx = G.t; sfx(SND.sing_capture.fn); }
          }
        }
      }
      if (G.t >= TC && stopHum) { stopHum(); stopHum = null; sfx(SND.sing_collapse.fn); }
      for (const c of captured) {
        const shrink = 1 - 0.6 * seg(G.t, TC, TB), inU = easeOut(seg(G.t, c.tc, c.tc + 0.18)), r = lerp(c.r0, c.r, inU) * shrink; c.a += c.spin * dt * inU;
        c.q.x = p.x + Math.cos(c.a) * r; c.q.y = p.y + Math.sin(c.a) * r * lerp(1, 0.6, inU); c.q.ang = c.a + Math.PI / 2; c.q.depth = Math.sin(c.a);
      }
      if (G.t >= TB && !burst) {
        burst = true;
        const hit = damageArea(p.x, p.y, t.burstRadius * s, { dmg: t.burstDamage * dmgMul, kx: t.burstKnock, ky: -260, heavy: true, slot, elem: el, burn: el === 'fire' ? t.burn : 0, freeze: el === 'ice' ? 1 : 0, hitstop: 0.07, shake: 9 * s, frame: true });
        if (mass) for (const e of hit) if (e.fly) { e.slammed = true; e.vy = 700; }
        // lo capturado sale: en todas direcciones (base) o apuntado a los enemigos (Arsenal), reforzado y, con Multiplicar, doble
        const targets = arsenal ? alive().sort((a, b) => Math.hypot(a.x - p.x, a.y - p.y) - Math.hypot(b.x - p.x, b.y - p.y)) : [];
        const launch = (q, i, aOff = 0) => {
          const sp = t.ejectSpeed, tg = targets.length ? targets[i % targets.length] : null;
          const a = (tg ? Math.atan2(tg.y - q.y, tg.x - q.x) : captured[i % captured.length].a) + aOff;
          q.captured = null; q.ejected = true; q.age = 0; q.hit = new Set(); q.vx = Math.cos(a) * sp; q.vy = Math.sin(a) * sp; q.ang = a; q.trail = [];
          if (arsenal) q.dmgMul = (q.dmgMul || 1) * t.arsenalMul;
          if (q.hostile) { q.hostile = false; q.friendly = true; q.slot = slot; q.dmg = t.shotDamage * (arsenal ? t.arsenalMul : 1); q.life = 3; }
        };
        captured.forEach((c, i) => {
          const cl = arsenal && hasMod(slot, 'multiply') && c.q.clone ? c.q.clone() : null;     // la copia antes de reforzar
          launch(c.q, i, cl ? -0.12 : 0);
          if (cl) { G.ents.push(cl); launch(cl, i, 0.12); }
        });
        if (maw && hasMod(slot, 'regurg')) for (let i = 0; i < feeds; i++) { const tg = nearestEnemy(p.x, p.y, 700); G.ents.push(voidBolt(p.x, p.y, (i / Math.max(1, feeds)) * TAU, tg, t.regurgDamage, slot)); }
        const bx = p.x, by = p.y;
        const lk = SING_LOOK_IDS[t.look | 0] || 'horizon';
        addFx(1.6, (ctx, layer, tb) => singBurst(lk, ctx, G.t, layer, tb, bx, by, s, bp, { mass, fire: el === 'fire' }));
        if (el === 'ice') addFx(1.6, (ctx, layer, tb) => frostNova(ctx, G.t, layer, tb, bx, t.burstRadius * s * 0.8));
        sfx(el === 'fire' ? SND.sing_burst_fire.fn : el === 'ice' ? SND.seed_nova.fn : SND.sing_burst.fn);
        if (captured.length) sfx(SND.sing_eject.fn);
        return false;
      }
      return true;
    },
    draw(ctx, layer) {
      const s = sNow(), lk = SING_LOOK_IDS[t.look | 0] || 'horizon';
      const live = { pal, bp, pullT: G.t >= TM && G.t < TC ? G.t - TM : -1, liveT: G.t - t0, collapse: seg(G.t, TC, TB), phase: G.t < TM ? 'travel' : 'live', openT: G.t - TM };
      if (twin && G.t < TM) {                                 // dos orbes (más pequeños) que se buscan
        [-1, 1].forEach((k, i) => { const q = orbPos(k); singLook(lk, ctx, G.t, layer, q.x, q.y, Object.assign({}, live, { s: s0, form: easeOut(seg(G.t, t0, TA)), trail: G.t < TA ? [0.025, 0.05, 0.075, 0.1, 0.125].map(dt => hist[i].find(h => G.t - h.t >= dt)).filter(Boolean) : [] }), t.over >= 0.5); });
        if (G.t >= TA && layer === 'front') { const m = seg(G.t, TA, TM); glow(ctx, tx, ty, 50 * s * m, bp.g2, 0.6 * m); }
      } else {
        singLook(lk, ctx, G.t, layer, this.x, this.y, Object.assign({}, live, { s, form: twin ? 1 : easeOut(seg(G.t, t0, TA)), trail: !twin && G.t < TA ? [0.025, 0.05, 0.075, 0.1, 0.125].map(dt => hist[0].find(h => G.t - h.t >= dt)).filter(Boolean) : [] }), t.over >= 0.5);
      }
      if (maw && G.t >= TM && G.t < TC && layer === 'front') {   // Fauces: la boca (zona que devora) late
        const r = t.devourRadius * s * (1 + 0.06 * Math.sin(G.t * 12));
        ellArc(ctx, this.x, this.y, r, r, 0, TAU, 1.6, '#cdbcff', 0.35 + 0.15 * Math.sin(G.t * 12), layer, { taper: 0, m: 40, wob: 0.06, seed: 2 });
      }
      if (G.settings.hitboxes && G.t >= TM) { ctx.strokeStyle = 'rgba(160,110,255,.6)'; ctx.beginPath(); ctx.arc(this.x, this.y, t.pullRadius * s, 0, TAU); ctx.stroke(); }
    },
  };
}
// Errante · Rastro: grieta pequeña que atrae y daña un momento
function miniRift(slot, x, y, life, dps) {
  const t0 = G.t; let tick = 0;
  return {
    kind: 'zone',
    update(dt) {
      tick -= dt;
      for (const e of alive()) { const d = Math.hypot(e.x - x, e.y - y); if (d < 70) { e.pull = e.pull || { x, y, s: 60 }; if (tick <= 0) damage(e, { dmg: dps * 0.25, slot, dot: true, silentFlash: true, color: '#cdbcff' }); } }
      if (tick <= 0) tick = 0.25;
      return G.t - t0 < life;
    },
    draw(ctx, layer) { const u = seg(G.t, t0 + life - 0.25, t0 + life); lookRift(ctx, G.t, layer, x, y, { s: 0.38, form: 1, collapse: u, pullT: 0, liveT: G.t - t0, phase: 'live', openT: G.t - t0, pal: PAL.void }); },
  };
}
// Fauces · Regurgitar: orbe de vacío grande que sale en abanico, persigue al enemigo más cercano y estalla en área al impactar
// (el usuario: «apenas se nota, efecto demasiado débil para una mejora así», 2026-10-19)
function voidBolt(x, y, a0, target, dmg, slot) {
  const t = T('sing'), R0 = 11;
  let vx = Math.cos(a0) * 380, vy = Math.sin(a0) * 380 - 120, life = 3, age = 0; const trail = [];
  sfx(k => { const tn = nowT(); vThump(tn, 110, 45, 0.25 * k, 0.4, 2, 0.2); vNoise(tn, 0.2 * k, 0.2, 'bandpass', 800, 3000, 1.2, { att: 0.005, wet: 0.2 }); });
  const boom = () => {
    damageArea(x, y, t.regurgRadius, { dmg, slot, heavy: true, kx: 200, ky: -150, shake: 5, hitstop: 0.04 }, false);
    const bx = x, by = y;
    addFx(1.4, (ctx, layer, tb) => burstRefined(ctx, G.t, layer, tb, bx, by, 0.45, PAL.void, {}));
    sfx(SND.sing_burst.fn);
  };
  return {
    kind: 'bolt',
    update(dt) {
      life -= dt; age += dt; if (life <= 0) { boom(); return false; }
      if (!target || target.dead) target = nearestEnemy(x, y, 800);
      const turn = Math.min(1, dt * (age < 0.15 ? 1 : 6));
      if (target) { const a = Math.atan2(target.y - y, target.x - x); vx = lerp(vx, Math.cos(a) * 720, turn); vy = lerp(vy, Math.sin(a) * 720, turn); }
      x += vx * dt; y += vy * dt; trail.unshift({ x, y }); if (trail.length > 16) trail.pop();
      if ((target && Math.hypot(target.x - x, target.y - y) < target.rx + R0 * 0.5) || y >= GROUND - 4) { boom(); return false; }
      return true;
    },
    draw(ctx, layer) {
      if (layer === 'back' && trail.length > 1) { ribbon(ctx, trail, q => 22 * Math.pow(1 - q, 1.2), '#3a1c9c', 0.45, layer); ribbon(ctx, trail, q => 11 * (1 - q), '#8a5cff', 0.8, layer); ribbon(ctx, trail, q => 4 * (1 - q), '#ffffff', 0.9, layer); }
      if (layer !== 'front') return;
      glow(ctx, x, y, 40, [130, 70, 255], 0.75); glow(ctx, x, y, 18, [220, 210, 255], 0.6);
      disc(ctx, x, y, R0 + 3, '#5a2fd0', 0.9, layer); disc(ctx, x, y, R0, '#05020e', 1, layer);
      ellArc(ctx, x, y, R0 + 0.8, R0 + 0.8, 0, TAU, 2, '#ffffff', 0.95, layer, { taper: 0, m: 30 });
      const B = rand(Math.floor(G.t * 22) + Math.floor(x));
      for (let i = 0; i < 2; i++) { const a = B() * TAU; bolt(ctx, x, y, x + Math.cos(a) * 24, y + Math.sin(a) * 24, 3, B, 1.1, PAL.set, 0.8, layer); }
    },
  };
}
