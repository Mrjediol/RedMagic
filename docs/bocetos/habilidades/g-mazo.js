// g-mazo.js — 16 · Mazo de cristal, versión jugable. Ficha y decisiones: docs/armas/mazo.md · progresión: docs/armas/progresion.md
// Cuerpo a cuerpo pesado: mantén para levantar el mazo, suelta para estamparlo delante (golpe fuerte + onda por el suelo). En el
// aire, soltar = caída en picado que golpea donde aterrizas (más fuerte cuanto más alto). Árbol: base → Terremoto / Meteoro /
// Sismo → 3 mejoras. Elemento y Duplicador vienen de items (Duplicador: un segundo golpe un poco más adelante).
'use strict';
const SND_HAMMER = {
  lift: sound('hammer_lift', 'Mazo', 'Levantar (carga)', k => { const t = nowT(); vRise(t, 'sawtooth', 60, 140, 0.5 * k, 0.06, 200, 900); vNoise(t, 0.5 * k, 0.08, 'lowpass', 300, 900, 1, { rising: true }); }),
  slam: sound('hammer_slam', 'Mazo', 'Golpe contra el suelo', k => { SFXLIB.boom(k, 1.2); const t = nowT(); vThump(t, 70, 25, 0.5 * k, 1, 4, 0.2); vNoise(t, 0.08 * k, 0.4, 'highpass', 2000, 5000, 1, { att: 0.001 }); }),
  dive: sound('hammer_dive', 'Mazo', 'Picado', k => { const t = nowT(); vNoise(t, 0.3 * k, 0.25, 'bandpass', 400, 2500, 1.2, { rising: true, att: 0.01 }); }),
  wave: sound('hammer_wave', 'Mazo', 'Terremoto · onda', k => { const t = nowT(); vNoise(t, 0.7 * k, 0.3, 'lowpass', 900, 150, 1, { att: 0.01, wet: 0.2 }); vThump(t, 55, 30, 0.6 * k, 0.5, 2); }),
  tremor: sound('hammer_tremor', 'Mazo', 'Sismo · réplica', k => { const t = nowT(); vThump(t, 80, 40, 0.2 * k, 0.45, 2); vNoise(t, 0.2 * k, 0.15, 'lowpass', 600, 150, 1, { att: 0.005 }); }),
};
addWeapon({
  id: 'hammer', name: 'Mazo de cristal', short: 'Mazo', pal: 'set',
  archetypes: [
    { id: 'quake', name: 'Terremoto', desc: 'El golpe suelta una onda que corre por el suelo hacia delante y lanza por los aires a todos los de suelo.',
      ups: [{ id: 'after', name: 'Réplica', desc: 'Una segunda onda sale poco después.' },
            { id: 'fissure', name: 'Fisura', desc: 'La onda deja una grieta que hace daño un rato.' },
            { id: 'both', name: 'Doble frente', desc: 'La onda sale hacia los dos lados.' }] },
    { id: 'meteor', name: 'Meteoro', desc: 'Todo golpe empieza con un salto y cae en picado: cuanto más alto caes, más fuerte y más ancho.',
      ups: [{ id: 'crater', name: 'Cráter', desc: 'El impacto deja un hoyo que atrae a los enemigos un momento.' },
            { id: 'bounce', name: 'Rebote', desc: 'Tras el impacto rebotas y caes otra vez (un golpe extra).' },
            { id: 'gravity', name: 'Gravedad', desc: 'Los golpeados quedan pesados: los voladores caen al suelo y todos van lentos un rato.' }] },
    { id: 'tremor', name: 'Sismo', desc: 'Tras el golpe el suelo queda temblando: cada golpe de tu OTRA arma a un enemigo que lo pisa provoca una réplica a su alrededor.',
      ups: [{ id: 'long', name: 'Temblor largo', desc: 'El suelo tiembla el doble de tiempo.' },
            { id: 'strong', name: 'Réplica fuerte', desc: 'Las réplicas son golpes fuertes (rompen congelados).' },
            { id: 'topple', name: 'Desequilibrio', desc: 'Las réplicas aturden.' }] },
  ],
  tuning: {
    cooldown: [0.9, 0.1, 4, 0.05, 'Base: recarga (s)'], chargeTime: [0.6, 0.05, 2, 0.05, 'Base: carga máxima (s)'], dmgMin: [30, 1, 150, 1, 'Base: daño sin carga'],
    dmgMax: [85, 1, 300, 1, 'Base: daño con carga'], slamWidth: [90, 30, 250, 5, 'Base: ancho del golpe'], shockRadius: [110, 0, 300, 5, 'Base: onda alrededor (a cada lado)'],
    shockDamage: [15, 0, 80, 1, 'Base: daño de la onda'], diveSpeed: [1300, 400, 2500, 10, 'Base: velocidad del picado'], fallBonus: [0.25, 0, 1, 0.01, 'Base: + daño por cada 100 px de caída'],
    iceFreeze: [0.6, 0, 1, 0.05, 'Hielo: congelación del golpe'], fireBurn: [2, 0, 6, 0.1, 'Fuego: quemadura (y suelo ardiendo)'],
    waveSpeed: [650, 100, 1500, 10, 'Terremoto: velocidad de la onda'], waveRange: [420, 80, 900, 10, 'Terremoto: alcance'], waveDamage: [24, 0, 120, 1, 'Terremoto: daño'],
    waveLift: [520, 0, 1200, 10, 'Terremoto: los lanza hacia arriba'], waveStun: [0.4, 0, 2, 0.05, 'Terremoto: aturdido (s)'], afterMul: [0.6, 0.1, 1.5, 0.05, 'Terremoto · Réplica: × daño'],
    afterDelay: [0.5, 0.1, 2, 0.05, 'Terremoto · Réplica: retraso (s)'], fissureTime: [2, 0.3, 6, 0.1, 'Terremoto · Fisura: duración (s)'], fissureDps: [12, 0, 60, 1, 'Terremoto · Fisura: daño/s'],
    hopSpeed: [650, 200, 1200, 10, 'Meteoro: salto antes del picado'], meteorFall: [0.6, 0, 2, 0.05, 'Meteoro: + daño por cada 100 px de caída (en vez de fallBonus)'],
    craterTime: [1.5, 0.3, 5, 0.1, 'Meteoro · Cráter: duración (s)'], craterRadius: [170, 40, 400, 5, 'Meteoro · Cráter: radio'], craterPull: [180, 0, 500, 5, 'Meteoro · Cráter: atracción'],
    bounceMul: [0.6, 0.1, 1.5, 0.05, 'Meteoro · Rebote: × del golpe extra'], heavyTime: [3, 0.3, 8, 0.1, 'Meteoro · Gravedad: s pesados'], heavySlow: [0.5, 0, 1, 0.05, 'Meteoro · Gravedad: × velocidad'],
    tremorTime: [3, 0.5, 10, 0.1, 'Sismo: s que tiembla'], tremorWidth: [200, 40, 400, 5, 'Sismo: medio ancho de la zona'], tremorDamage: [14, 0, 80, 1, 'Sismo: daño de la réplica'],
    tremorRadius: [75, 20, 200, 5, 'Sismo: radio de la réplica'], tremorGap: [0.3, 0.05, 2, 0.05, 'Sismo: s entre réplicas del mismo enemigo'], toppleStun: [0.5, 0, 2, 0.05, 'Sismo · Desequilibrio: aturdido (s)'],
  },
  create(slot) {
    const id = 'hammer';
    let cd = 0, charge = null, dive = null, bounces = 0, tremors = [];
    const pal = () => elemPal('set');
    const face = () => G.player.face;
    const slam = (x, gy, k, fall, mul = 1) => {
      const t = T(id), meteor = slot.arch === 'meteor', el = elemOf(), P = pal(), f = face();
      const fallK = 1 + Math.max(0, fall) / 100 * (meteor ? t.meteorFall : t.fallBonus);
      const w = (t.slamWidth + 30 * k) * (meteor ? 1 + Math.max(0, fall) / 500 : 1), dmg = lerp(t.dmgMin, t.dmgMax, k) * fallK * mul;
      const push = slot.arch === 'tremor' ? 0.25 : 1;            // Sismo: apenas empuja, para que se queden sobre el suelo que tiembla
      const hit = [];
      for (const e of alive()) {
        if (Math.abs(e.x - x) > w / 2 + e.rx || e.y + e.ry < gy - 170) continue;
        hit.push(e);
        damage(e, { dmg, slot, heavy: true, kx: (Math.sign(e.x - x) || f) * 160 * push, ky: e.fly ? 300 : -320, elem: el, freeze: el === 'ice' ? t.iceFreeze : 0, burn: el === 'fire' ? t.fireBurn : 0, hitstop: 0.05 + 0.04 * k, shake: 6 + 6 * k, frame: k > 0.9 || fall > 150 });
        if (e.fly && !e.dead) { e.slammed = true; e.vy = 700; }
      }
      if (t.shockRadius > 0) for (const e of alive()) { const d = Math.abs(e.x - x); if (hit.includes(e) || e.fly || d > w / 2 + t.shockRadius + e.rx) continue; damage(e, { dmg: t.shockDamage * mul, slot, kx: Math.sign(e.x - x) * 260 * push, ky: -180, noReact: true, elem: el, freeze: el === 'ice' ? 0.3 : 0 }); }
      if (meteor && hasMod(slot, 'gravity')) G.ents.push(hammerHeavy(slot, hit));
      if (meteor && hasMod(slot, 'crater')) G.ents.push(hammerCrater(slot, x));
      if (el === 'fire') G.ents.push(fireZone(x, w / 2, 1.5, t.fireBurn, slot));
      if (slot.arch === 'quake') {
        const dirs = hasMod(slot, 'both') ? [-1, 1] : [f];
        for (const d of dirs) { G.ents.push(hammerWave(slot, x, d, mul)); if (hasMod(slot, 'after')) { const t0 = G.t; G.ents.push({ kind: 'timer', update() { if (G.t - t0 < t.afterDelay) return true; G.ents.push(hammerWave(slot, x, d, mul * t.afterMul)); return false; }, draw() {} }); } }
        sfx(SND_HAMMER.wave.fn);
      }
      if (slot.arch === 'tremor') { const z = { x, until: G.t + t.tremorTime * (hasMod(slot, 'long') ? 2 : 1) }; tremors.push(z); G.ents.push(hammerTremorZone(slot, z)); }
      const sc = 0.9 + 0.6 * k + Math.min(0.8, fall / 300);
      addFx(1.4, (ctx, layer, f0) => { groundImpact(ctx, G.t, layer, f0, x, sc, P, { seed: Math.floor(x) }); cracks(ctx, G.t, f0, x, layer, { n: 10, len: 70 * sc, pal: P }); if (layer === 'front') { const u = seg(G.t, f0, f0 + 0.25); if (u < 1) glow(ctx, x, GROUND - 20, 120 * sc, P.g1, 0.7 * (1 - u)); } });
      addFx(0.35, (ctx, layer, f0) => { const u = seg(G.t, f0, f0 + 0.3), p = G.player; crystalHammer(ctx, G.t, layer, p.x + f * 8, p.y - 52, f > 0 ? lerp(0.3, 0.6, u) : Math.PI - lerp(0.3, 0.6, u), 1.15, P, 1 - u * u, 0); });
      sfx(SND_HAMMER.slam.fn);
      if (copies() > 1 && mul === 1) { const t0 = G.t; G.ents.push({ kind: 'timer', update() { if (G.t - t0 < 0.15) return true; slam(x + f * 90, gy, k, fall, 0.6); return false; }, draw() {} }); }
    };
    const startDive = k => { const p = G.player; dive = { k, y0: p.y, started: true }; p.vy = T(id).diveSpeed; p.vx *= 0.3; sfx(SND_HAMMER.dive.fn); };
    const inst = {
      onPress() {
        if (cd > 0 || charge || dive) return;
        charge = { t0: G.t }; G.ents.push(hammerChargeEnt(slot, () => charge)); sfx(SND_HAMMER.lift.fn);
      },
      onRelease() {
        if (!charge) return;
        const t = T(id), p = G.player, k = clamp((G.t - charge.t0) / t.chargeTime); charge = null;
        cd = t.cooldown * cdMul(); G.player.castT = 0.3; bounces = slot.arch === 'meteor' && hasMod(slot, 'bounce') ? 1 : 0;
        const air = !p.onGround;
        if (air) startDive(k);
        else if (slot.arch === 'meteor') { p.vy = -t.hopSpeed; p.onGround = false; dive = { k, start: G.t + 0.22 }; }
        else slam(p.x + face() * 60, p.y, k, 0);
      },
      update(dt, current) {
        const p = G.player, t = T(id);
        cd -= dt;
        if (charge && !current) charge = null;
        tremors = tremors.filter(z => z.until > G.t);
        if (dive) {
          if (dive.start && G.t >= dive.start) { startDive(dive.k); }
          else if (dive.started) {
            p.vy = Math.max(p.vy, t.diveSpeed); p.castT = 0.1;
            if (p.onGround) {
              const k = dive.k, fall = p.y - dive.y0; dive = null; slam(p.x, p.y, k, fall);
              if (bounces > 0) { bounces--; p.vy = -t.hopSpeed * 0.85; p.onGround = false; dive = { k: k * t.bounceMul, start: G.t + 0.25 }; }
            }
          }
        }
      },
      // Sismo: los golpes de tu otra arma a enemigos sobre el suelo que tiembla provocan réplicas
      onAnyHit(e, o) {
        if (slot.arch !== 'tremor' || o.slot === slot || e.fly) return;
        const t = T(id), z = tremors.find(z => Math.abs(e.x - z.x) < t.tremorWidth); if (!z || G.t - (e._tremT || -9) < t.tremorGap) return;
        e._tremT = G.t;
        const hit = damageArea(e.x, GROUND - 30, t.tremorRadius, { dmg: t.tremorDamage, slot, proc: true, heavy: hasMod(slot, 'strong'), ky: -220, kx: 80, shake: 2.5, elem: elemOf() });
        if (hasMod(slot, 'topple')) for (const h of hit) h.stunT = Math.max(h.stunT, t.toppleStun);
        const x = e.x, P = pal(); addFx(1, (ctx, layer, f0) => { groundImpact(ctx, G.t, layer, f0, x, 0.5, P, { seed: Math.floor(x), debris: false }); });
        if (!G._trSfx || G.t - G._trSfx > 0.1) { G._trSfx = G.t; sfx(SND_HAMMER.tremor.fn); }
      },
      diving: () => !!(dive && dive.started),
      ready() { return charge ? clamp((G.t - charge.t0) / T(id).chargeTime) : 1 - cd / Math.max(0.01, T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
    G.ents.push(hammerDiveEnt(slot, inst));
    return inst;
  },
});

// el mazo levantado mientras cargas
function hammerChargeEnt(slot, getCharge) {
  return { kind: 'fx', update() { return !!getCharge() && G.slots.includes(slot); },
    draw(ctx, layer) {
      const c = getCharge(); if (!c) return;
      const p = G.player, f = p.face, k = clamp((G.t - c.t0) / T('hammer').chargeTime), a = -Math.PI / 2 - f * (0.3 + 0.7 * easeOut(k)), shake = k >= 1 ? Math.sin(G.t * 60) * 1.5 : 0;
      crystalHammer(ctx, G.t, layer, p.x + f * 4 + shake, p.y - 56, a, 1.1, elemPal('set'), 1, k); p.castT = 0.1;
    } };
}
// el mazo hacia abajo durante el picado (vive mientras el arma esté equipada)
function hammerDiveEnt(slot, inst) {
  return { kind: 'fx', update() { return slot.inst === inst && G.slots.slice(0, G.slotCount).includes(slot); },
    draw(ctx, layer) {
      if (!inst.diving()) return;
      const p = G.player, P = elemPal('set');
      crystalHammer(ctx, G.t, layer, p.x, p.y - 50, Math.PI / 2, 1.15, P, 1, 1);
      if (layer === 'back') for (const s of [-1, 1]) strokeTaper(ctx, [{ x: p.x + s * 14, y: p.y + 10 }, { x: p.x + s * 18, y: p.y - 90 }], 4, P.light, 0.6, layer, true);
    } };
}
// Terremoto: onda que corre por el suelo
function hammerWave(slot, x0, dir, mul) {
  const t = T('hammer'), t0 = G.t, el = elemOf(), pal = elemPal('set'), hit = new Set(), seed = Math.floor(x0);
  let x = x0, lastFis = x0;
  return { kind: 'wave', update(dt) {
      const d = (G.t - t0) * t.waveSpeed; x = x0 + dir * d;
      if (d > t.waveRange || x < 0 || x > WW) return false;
      for (const e of alive()) if (!e.fly && !hit.has(e) && Math.abs(e.x - x) < e.rx + 14) {
        hit.add(e); damage(e, { dmg: t.waveDamage * mul, slot, ky: -t.waveLift, kx: dir * 120, heavy: true, hitstop: 0.02, shake: 3, elem: el, freeze: el === 'ice' ? 0.4 : 0, burn: el === 'fire' ? 1.5 : 0 }); e.stunT = Math.max(e.stunT, t.waveStun);
      }
      if (hasMod(slot, 'fissure') && Math.abs(x - lastFis) > 50) { lastFis = x; G.ents.push(hammerFissure(slot, x)); }
      return true; },
    draw(ctx, layer) { const d = (G.t - t0) * t.waveSpeed; groundWave(ctx, G.t, layer, x, dir, 34 * (1 - 0.5 * d / t.waveRange) * Math.min(1, mul + 0.3), pal, 1 - seg(d, t.waveRange * 0.8, t.waveRange), seed); } };
}
function hammerFissure(slot, x) {
  const t = T('hammer'), t0 = G.t, pal = elemPal('set'); let tick = 0;
  return { kind: 'zone', x, y: GROUND - 10, halfW: 26,
    update(dt) { tick -= dt; if (tick <= 0) { tick = 0.25; for (const e of alive()) if (!e.fly && Math.abs(e.x - x) < 26 + e.rx * 0.5) damage(e, { dmg: t.fissureDps * 0.25, slot, silentFlash: true }); } return G.t - t0 < t.fissureTime; },
    draw(ctx, layer) { const a = 1 - seg(G.t, t0 + t.fissureTime - 0.4, t0 + t.fissureTime); if (layer === 'back') { ctx.save(); ctx.globalAlpha = clamp(0.8 * a); ctx.fillStyle = col(pal.dark, layer); ctx.beginPath(); ctx.ellipse(x, GROUND + 2, 26, 5, 0, 0, TAU); ctx.fill(); ctx.restore(); } else glow(ctx, x, GROUND, 20, pal.g1, 0.35 * a * (0.7 + 0.3 * Math.sin(G.t * 12 + x))); } };
}
// Meteoro · Cráter: hoyo que atrae un momento
function hammerCrater(slot, x) {
  const t = T('hammer'), t0 = G.t, pal = elemPal('set');
  return { kind: 'zone', x, y: GROUND - 10, halfW: 40,
    update() { for (const e of alive()) { const d = Math.abs(e.x - x); if (d < t.craterRadius && d > 12 && !e.stasis) e.pull = e.pull || { x, y: e.fly ? GROUND - 60 : e.y, s: t.craterPull, lift: e.fly }; } return G.t - t0 < t.craterTime; },
    draw(ctx, layer) { const a = 1 - seg(G.t, t0 + t.craterTime - 0.4, t0 + t.craterTime); if (layer === 'back') { ctx.save(); ctx.globalAlpha = clamp(0.85 * a); ctx.fillStyle = col('#151a28', layer); ctx.beginPath(); ctx.ellipse(x, GROUND + 3, 50, 9, 0, 0, TAU); ctx.fill(); ctx.restore(); } groundRing(ctx, x, GROUND + 2, 50 + 10 * Math.sin(G.t * 8), 2, pal.mid, 0.6 * a, layer, { taper: 0 }); } };
}
// Meteoro · Gravedad: los golpeados pesan (voladores al suelo, todos lentos)
function hammerHeavy(slot, list) {
  const t = T('hammer'), t0 = G.t;
  return { kind: 'fx', update() { for (const e of list) if (!e.dead) { e.timeK = Math.min(e.timeK === undefined ? 1 : e.timeK, t.heavySlow); if (e.fly) e.slammed = true; } return G.t - t0 < t.heavyTime; },
    draw(ctx, layer) { if (layer !== 'front') return; for (const e of list) if (!e.dead) for (let i = 0; i < 3; i++) { const y = e.y + e.ry + 4 - ((G.t * 40 + i * 9) % 26); needle(ctx, e.x - 14 + i * 14, y - 10, e.x - 14 + i * 14, y, 2, '#c8d4ff', 0.6, layer); } } };
}
// Sismo: zona de suelo que tiembla (sólo se dibuja; las réplicas las provoca onAnyHit)
function hammerTremorZone(slot, z) {
  const t = T('hammer'), pal = elemPal('set');
  return { kind: 'fx', update() { return G.t < z.until; },
    draw(ctx, layer) { const a = Math.min(1, (z.until - G.t) * 2), w = t.tremorWidth; groundRing(ctx, z.x + Math.sin(G.t * 50) * 2, GROUND + 2, w, 1.6, pal.light, 0.45 * a, layer, { taper: 0, wob: 0.05, seed: Math.floor(G.t * 20), sq: 0.12 });
      if (layer === 'front') { const R = rand(Math.floor(G.t * 18)); for (let i = 0; i < 4; i++) { const px = z.x + (R() - 0.5) * w * 2; chunk(ctx, px, GROUND - 2 - R() * 6, 2 + R() * 2, R() * 6, i, pal.mid, pal.core, 0.6 * a, layer); } } } };
}
