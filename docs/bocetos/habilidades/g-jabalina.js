// g-jabalina.js — 10 · Jabalina, versión jugable. Ficha y decisiones: docs/armas/jabalina.md · progresión: docs/armas/progresion.md
// Mantén para cargar y suelta: jabalina de cristal en parábola que atraviesa a uno y SE QUEDA CLAVADA. Hay que recuperarla:
// pasando por encima (al instante) o pulsando otra vez (vuelve volando). Mientras está fuera, el arma no dispara → premia
// cambiar a la otra arma. Árbol: base → Pararrayos / Empalar / Destello → 3 mejoras. Elemento y Duplicador vienen de items.
'use strict';
const SND_SPEAR = {
  charge: sound('spear_charge', 'Jabalina', 'Carga', k => { const t = nowT(); vRise(t, 'triangle', 220, 660, 0.45 * k, 0.05, 600, 3000); vNoise(t, 0.45 * k, 0.06, 'bandpass', 800, 3000, 2, { rising: true }); }),
  throw: sound('spear_throw', 'Jabalina', 'Lanzamiento', k => { const t = nowT(); vNoise(t, 0.25 * k, 0.32, 'bandpass', 3200, 900, 1.2, { att: 0.004, wet: 0.15 }); vThump(t, 160, 90, 0.08 * k, 0.25, 1); }),
  hit: sound('spear_hit', 'Jabalina', 'Atraviesa', k => { const t = nowT(); vThump(t, 140, 55, 0.18 * k, 0.55, 2.5); vNoise(t, 0.08 * k, 0.3, 'highpass', 2500, 6000, 1, { att: 0.001 }); }),
  stick: sound('spear_stick', 'Jabalina', 'Se clava', k => { const t = nowT(); vThump(t, 200, 70, 0.12 * k, 0.5, 2); vRing(t, [880, 1320, 1980], 0.5 * k, 0.04, 0.25); }),
  pick: sound('spear_pick', 'Jabalina', 'Recoger', k => { const t = nowT(); vTone(t, 990, 1480, 0.12 * k, 0.06, 'triangle', 0.2); }),
  recall: sound('spear_recall', 'Jabalina', 'Vuelve', k => { const t = nowT(); vNoise(t, 0.3 * k, 0.2, 'bandpass', 700, 3500, 1.5, { rising: true, att: 0.01 }); }),
  zap: sound('spear_zap', 'Jabalina', 'Pararrayos · rayo', k => { const t = nowT(); vCrackle(t, 0.12 * k, 0.3, 26, 0.15, [3000, 8000]); vNoise(t, 0.1 * k, 0.25, 'highpass', 4000, 9000, 1, { att: 0.001 }); vThump(t, 90, 50, 0.1 * k, 0.25, 2); }),
  blink: sound('spear_blink', 'Jabalina', 'Destello', k => { const t = nowT(); vNoise(t, 0.12 * k, 0.3, 'highpass', 1500, 8000, 1, { rising: true, att: 0.002 }); vRing(t + 0.05 * k, [1760, 2640], 0.3 * k, 0.05, 0.3); vThump(t + 0.08 * k, 100, 45, 0.25 * k, 0.5, 2); }),
};
addWeapon({
  id: 'spear', name: 'Jabalina', short: 'Jabalina', pal: 'set',
  archetypes: [
    { id: 'rod', name: 'Pararrayos', desc: 'Clavada, es un pararrayos: cada golpe de CUALQUIERA de tus armas a un enemigo cerca de ella hace caer un rayo desde la jabalina, que salta a otro.',
      ups: [{ id: 'storm', name: 'Tormenta', desc: 'Además, lanza un rayo sola cada poco al enemigo más cercano.' },
            { id: 'conduct', name: 'Conductor', desc: 'Clavada en un enemigo, su radio crece y cada golpe a ese enemigo electrocuta a todos los de alrededor.' },
            { id: 'dump', name: 'Descarga', desc: 'Cada rayo carga la jabalina; al recuperarla, suelta toda la carga en una explosión.' }] },
    { id: 'impale', name: 'Empalar', desc: 'Cargada, se lleva al primer enemigo ensartado y lo deja clavado donde para: inmóvil y vulnerable un rato.',
      ups: [{ id: 'skewer', name: 'Brocheta', desc: 'Ensarta a todos los que atraviesa (hasta 4).' },
            { id: 'nail', name: 'Clavado', desc: 'Los clavados reciben aún más daño y se quedan más tiempo.' },
            { id: 'rip', name: 'Arrancar', desc: 'Al recuperarla la arrancas: daño fuerte a los clavados y los arrastra hacia ti.' }] },
    { id: 'blink', name: 'Destello', desc: 'Pulsar con la jabalina fuera te TELETRANSPORTA a ella, con onda de choque al llegar, y vuelve a tu mano.',
      ups: [{ id: 'trail', name: 'Estela', desc: 'Atraviesas y dañas a todo lo que hay entre donde estabas y la jabalina.' },
            { id: 'phase', name: 'Fase', desc: 'Al llegar eres intocable un momento y tu siguiente ataque (de cualquier arma) es golpe fuerte.' },
            { id: 'relay', name: 'Relevo', desc: 'Cada destello recarga un poco tu otra arma.' }] },
  ],
  tuning: {
    chargeTime: [0.5, 0.05, 2, 0.05, 'Base: carga máxima (s)'], speedMin: [700, 200, 2000, 10, 'Base: velocidad sin carga'], speedMax: [1250, 200, 2500, 10, 'Base: velocidad con carga'],
    gravity: [420, 0, 1500, 10, 'Base: gravedad (parábola)'], dmgMin: [25, 1, 150, 1, 'Base: daño sin carga'], dmgMax: [70, 1, 300, 1, 'Base: daño con carga (golpe fuerte)'],
    pierce: [1, 0, 5, 1, 'Base: enemigos que atraviesa antes de clavarse'], knock: [160, 0, 600, 5, 'Base: empuje'], throwCd: [0.25, 0, 3, 0.05, 'Base: recarga tras tenerla en la mano (s)'],
    stickTime: [6, 1, 20, 0.5, 'Base: s clavada antes de volver sola'], pickupRadius: [45, 10, 120, 1, 'Base: radio para recogerla'], recallTime: [0.3, 0.1, 1, 0.05, 'Base: vuelo de vuelta (s)'],
    iceFreeze: [0.6, 0, 1, 0.05, 'Hielo: congelación del impacto'], fireBurn: [2, 0, 6, 0.1, 'Fuego: quemadura (y suelo ardiendo donde se clava)'],
    rodRadius: [220, 40, 500, 5, 'Pararrayos: radio'], rodDamage: [12, 1, 60, 1, 'Pararrayos: daño del rayo'], rodChain: [0.6, 0, 1.5, 0.05, 'Pararrayos: × daño del salto'],
    rodGap: [0.2, 0.03, 1, 0.01, 'Pararrayos: s mínimo entre rayos al mismo enemigo'], stormEvery: [0.9, 0.2, 4, 0.05, 'Pararrayos · Tormenta: cada (s)'],
    conductRadius: [1.5, 1, 3, 0.05, 'Pararrayos · Conductor: × radio clavada en un enemigo'], dumpPer: [8, 0, 40, 1, 'Pararrayos · Descarga: daño por carga'], dumpRadius: [120, 30, 300, 5, 'Pararrayos · Descarga: radio'],
    impaleMin: [0.5, 0, 1, 0.05, 'Empalar: carga mínima para ensartar'], pinTime: [2.5, 0.3, 8, 0.1, 'Empalar: s clavado'], pinVuln: [1.3, 1, 3, 0.05, 'Empalar: × daño que recibe clavado'],
    skewerMax: [4, 1, 8, 1, 'Empalar · Brocheta: ensartados máx.'], nailVuln: [1.6, 1, 4, 0.05, 'Empalar · Clavado: × daño (en vez de pinVuln)'], nailTime: [1.6, 1, 3, 0.05, 'Empalar · Clavado: × tiempo'],
    ripDamage: [40, 0, 200, 1, 'Empalar · Arrancar: daño'],
    blinkDamage: [25, 0, 150, 1, 'Destello: daño de la onda'], blinkRadius: [90, 20, 250, 5, 'Destello: radio'], trailDamage: [30, 0, 150, 1, 'Destello · Estela: daño'],
    phaseMul: [1.6, 1, 4, 0.05, 'Destello · Fase: × daño del siguiente ataque'], phaseTime: [0.8, 0, 3, 0.05, 'Destello · Fase: s intocable'], relayRefund: [1.5, 0, 6, 0.1, 'Destello · Relevo: s de recarga a la otra arma'],
  },
  create(slot) {
    const id = 'spear';
    let cd = 0, charge = null, spears = [];
    const inst = {
      spears: () => spears,
      onPress(aim) {
        if (spears.length) {                                   // fuera: Destello o que vuelva
          if (slot.arch === 'blink') blinkTo(spears.find(s => s.state !== 'recall') || spears[0]);
          else spears.forEach(s => s.recall());
          return;
        }
        if (cd > 0 || charge) return;
        charge = { t0: G.t }; G.ents.push(spearChargeEnt(slot, () => charge)); sfx(SND_SPEAR.charge.fn);
      },
      onRelease(aim) {
        if (!charge) return;
        const t = T(id), k = clamp((G.t - charge.t0) / t.chargeTime); charge = null;
        const n = copies();
        for (let c = 0; c < n; c++) { const s = spearEnt(slot, inst, aim.x, aim.y, aim.ang + (n > 1 ? (c ? 0.07 : -0.07) : 0), k); spears.push(s); G.ents.push(s); }
        G.player.castT = 0.3; G.player.vx -= Math.cos(aim.ang) * 80 * k; sfx(SND_SPEAR.throw.fn);
      },
      update(dt, current) {
        cd -= dt; if (charge && !current) charge = null;
        const before = spears.length; spears = spears.filter(s => !s.gone);
        if (before && !spears.length) cd = Math.max(cd, T(id).throwCd * cdMul());
      },
      // Pararrayos: los golpes de tus armas cerca de una jabalina clavada atraen un rayo
      onAnyHit(e, o) {
        if (slot.arch !== 'rod') return;
        for (const s of spears) if (s.state === 'stuck') s.zapFor(e);
      },
      chargeK() { return charge ? clamp((G.t - charge.t0) / T(id).chargeTime) : 0; },
      ready() { return charge ? clamp((G.t - charge.t0) / T(id).chargeTime) : spears.length ? 0 : 1 - cd / Math.max(0.01, T(id).throwCd * cdMul()); },
      refund(s) { cd -= s; },
    };
    const blinkTo = s => {
      const t = T(id), p = G.player, x0 = p.x, y0 = p.y, pal = elemPal('set');
      const tx = clamp(s.stuckE ? s.stuckE.x - Math.sign(s.stuckE.x - x0 || 1) * (s.stuckE.rx + 16) : s.x, 20, WW - 20), ty = Math.min(GROUND, s.state === 'stuck' && !s.stuckE ? s.y + 4 : s.y + 40);
      p.x = tx; p.y = ty; p.vx = 0; p.vy = 0; p.onGround = p.y >= GROUND; p.dashT = 0;
      if (hasMod(slot, 'trail')) { const skip = new Set(); let h; while ((h = raycastEnemy(x0, y0 - 40, tx, ty - 40, skip))) { skip.add(h.e); damage(h.e, { dmg: t.trailDamage, slot, heavy: true, kx: Math.sign(tx - x0) * 200, hitstop: 0.03, elem: elemOf() }); } }
      damageArea(tx, ty - 30, t.blinkRadius, { dmg: t.blinkDamage, slot, kx: 220, ky: -160, shake: 5, hitstop: 0.03, elem: elemOf(), freeze: elemOf() === 'ice' ? 0.4 : 0, burn: elemOf() === 'fire' ? 2 : 0 });
      if (hasMod(slot, 'phase')) { p.hurtT = t.phaseTime; G.empower = t.phaseMul; G.empowerHit = false; G.empowerEnd = 0; text(tx, ty - 110, 'FASE', '#9ef2ff', true); }
      if (hasMod(slot, 'relay')) for (const so of otherSlots(slot)) if (so.inst && so.inst.refund) so.inst.refund(t.relayRefund);
      addFx(0.5, (ctx, layer, f0) => blinkTrail(ctx, G.t, layer, f0, x0, y0, tx, ty, pal));
      sfx(SND_SPEAR.blink.fn);
      for (const sp of spears) sp.vanish();
      spears = []; cd = 0.15;
    };
    return inst;
  },
});

// la jabalina en la mano mientras cargas (echada hacia atrás, brilla con la carga llena)
function spearChargeEnt(slot, getCharge) {
  return { kind: 'fx', update() { return !!getCharge() && G.slots.includes(slot); },
    draw(ctx, layer) {
      const c = getCharge(); if (!c) return;
      const k = clamp((G.t - c.t0) / T('spear').chargeTime), a = aimInfo(), back = 26 * easeOut(k), tip = { x: a.x + a.dx * (30 - back), y: a.y + a.dy * (30 - back) };
      crystalSpear(ctx, G.t, layer, tip.x, tip.y, a.ang, 0.8, elemPal('set'), 1, { hot: k >= 1, glow: k > 0.3 });
      if (layer === 'front' && k >= 1) glow(ctx, tip.x, tip.y, 30, elemPal('set').g2, 0.5 + 0.3 * Math.sin(G.t * 30));
      G.player.castT = 0.1;
    } };
}

// La jabalina: fly → stuck (en suelo, plataforma o enemigo) → recall (vuelve) / recogida. k = carga 0..1
function spearEnt(slot, owner, x, y, ang, k) {
  const t = T('spear'), el = elemOf(), arch = slot.arch, pal = elemPal(arch === 'rod' ? 'storm' : 'set'), sp = lerp(t.speedMin, t.speedMax, k);
  const heavy = k >= 0.95, impale = arch === 'impale' && k >= t.impaleMin, carryMax = impale ? (hasMod(slot, 'skewer') ? t.skewerMax : 1) : 0;
  const nail = hasMod(slot, 'nail'), pinT = t.pinTime * (nail ? t.nailTime : 1), pinV = nail ? t.nailVuln : t.pinVuln;
  const trail = [], carried = [], hit = new Set();
  let pierce = t.pierce, stuckAt = 0, recallT0 = 0, rx = 0, ry = 0, stormT = 0, charges = 0, pinned = [], pinUntil = 0, off = { x: 0, y: 0 };
  const s = {
    kind: 'spear', capturable: true, state: 'fly', x, y, vx: Math.cos(ang) * sp, vy: Math.sin(ang) * sp, ang, dmgMul: 1, age: 0, stuckE: null, gone: false,
    get charges() { return charges; },
    recall() { if (this.state === 'recall' || this.gone) return; this.release(); this.state = 'recall'; recallT0 = G.t; rx = this.x; ry = this.y; this.captured = null; sfx(SND_SPEAR.recall.fn);
      if (hasMod(slot, 'rip')) for (const e of pinned) if (!e.dead) { damage(e, { dmg: t.ripDamage, slot, heavy: true, kx: Math.sign(G.player.x - e.x) * 300, hitstop: 0.05, shake: 6, elem: el }); e.vpull = { x: G.player.x, y: e.y, until: G.t + 0.4 }; text(e.x, e.y - 60, 'ARRANCADA', '#9ef2ff', true); }
      pinned = []; },
    vanish() { this.release(); this.gone = true; },
    release() {                                                 // al irse: Descarga suelta la carga
      if (charges > 0 && hasMod(slot, 'dump')) {
        const x = this.x, y = this.y, d = Math.min(150, charges * t.dumpPer);
        damageArea(x, y - 20, t.dumpRadius, { dmg: d, slot, proc: true, heavy: true, kx: 220, shake: 6, hitstop: 0.05, frame: d > 60 });
        addFx(1, (ctx, layer, f0) => { impact(ctx, G.t, layer, f0, x, y - 20, 1, PAL.storm, { spread: TAU, seed: 3 }); shockRing(ctx, G.t, layer, f0, x, y - 20, t.dumpRadius, PAL.storm); });
        text(x, y - 80, 'DESCARGA ' + Math.round(d), '#e9e4ff', true); sfx(k2 => { SND_SPEAR.zap.fn(k2); SFXLIB.boom(k2, 0.7); });
        charges = 0;
      }
    },
    picked() { this.release(); this.gone = true; sfx(SND_SPEAR.pick.fn); const P = pal, px = G.player.x, py = G.player.y; addFx(0.3, (ctx, layer, f0) => { if (layer === 'front') { const u = seg(G.t, f0, f0 + 0.25); star4(ctx, px, py - 46, 18 * (1 - u), 0.5, P.core, 1 - u, layer); } }); },
    // Pararrayos: rayo desde la punta a e (y salto a otro)
    zapFor(e, own) {
      if (e.dead || G.t - (e.rodT || -9) < t.rodGap) return;
      const r = t.rodRadius * (this.stuckE && hasMod(slot, 'conduct') ? t.conductRadius : 1);
      const top = { x: this.x - Math.cos(this.ang) * 50, y: this.y - Math.sin(this.ang) * 50 };
      if (Math.hypot(e.x - top.x, e.y - top.y) > r) return;
      const targets = this.stuckE === e && hasMod(slot, 'conduct') ? alive().filter(o => o !== e && Math.hypot(o.x - e.x, o.y - e.y) < r) : [e];
      for (const tg of targets) {
        if (G.t - (tg.rodT || -9) < t.rodGap && tg !== e) continue;
        tg.rodT = G.t; charges++;
        damage(tg, { dmg: t.rodDamage, slot, proc: true, silentFlash: false, color: '#e9e4ff', kx: 0, elem: el, freeze: el === 'ice' ? 0.2 : 0, burn: el === 'fire' ? 1 : 0 });
        const x0 = top.x, y0 = top.y, x1 = tg.x, y1 = tg.y; addFx(0.3, (ctx, layer, f0) => rodBolt(ctx, G.t, layer, f0, x0, y0, x1, y1, PAL.storm));
        const n = nearestEnemy(tg.x, tg.y, 140, new Set([tg, e]));
        if (n && G.t - (n.rodT || -9) >= t.rodGap) { n.rodT = G.t; damage(n, { dmg: t.rodDamage * t.rodChain, slot, proc: true, color: '#e9e4ff', elem: el }); addFx(0.3, (ctx, layer, f0) => rodBolt(ctx, G.t, layer, f0, x1, y1, n.x, n.y, PAL.storm, 1.4)); }
      }
      if (!G._zapSfx || G.t - G._zapSfx > 0.08) { G._zapSfx = G.t; sfx(SND_SPEAR.zap.fn); }
    },
    stick(e, sx, sy) {
      this.state = 'stuck'; stuckAt = G.t; this.x = sx; this.y = sy; this.vx = 0; this.vy = 0;
      if (e) { this.stuckE = e; off = { x: sx - e.x, y: sy - e.y }; }
      if (carried.length) { pinned = carried.slice(); pinUntil = G.t + pinT; for (const c of pinned) if (!c.dead) { c.stunT = Math.max(c.stunT, pinT); makeVuln(c, pinV, pinT); text(c.x, c.y - 60, 'CLAVADO', '#9ef2ff', true); } carried.length = 0; }
      if (el === 'fire' && !e) G.ents.push(fireZone(sx, 34, 2, t.fireBurn, slot));
      sfx(SND_SPEAR.stick.fn);
      const P = pal; addFx(0.8, (ctx, layer, f0) => { if (!e) cracks(ctx, G.t, f0, sx, layer, { n: 5, len: 40, pal: P }); sparks(ctx, G.t, f0, sx, sy, layer, { n: 8, pal: P, sp: 220, seed: Math.floor(sx), len: 12, life: 0.35 }); });
    },
    hitEnemy(e, hx, hy) {
      hit.add(e);
      damage(e, { dmg: lerp(t.dmgMin, t.dmgMax, k) * this.dmgMul, slot, heavy, kx: Math.sign(this.vx) * t.knock * (impale ? 0 : 1), elem: el, freeze: el === 'ice' ? t.iceFreeze : 0, burn: el === 'fire' ? t.fireBurn : 0,
        hitstop: heavy ? 0.06 : 0.025, shake: heavy ? 6 : 3, frame: heavy });
      const P = pal, a = this.ang; addFx(0.9, (ctx, layer, f0) => impact(ctx, G.t, layer, f0, hx, hy, heavy ? 0.8 : 0.55, P, { dir: a + Math.PI, spread: 1.6, seed: Math.floor(hx) }));
      sfx(SND_SPEAR.hit.fn);
      if (e.dead) return true;
      if (carried.length < carryMax) { carried.push(e); return true; }   // Empalar: se lo lleva
      if (pierce > 0) { pierce--; return true; }
      this.stick(e, hx, hy); return false;
    },
    update(dt) {
      this.age += dt;
      if (this.gone) return false;
      if (this.captured) { trail.length = 0; return true; }
      if (this.state === 'fly') {
        for (let i = 0; i < 3 && this.state === 'fly'; i++) {
          const h = dt / 3; this.vy += t.gravity * h;
          const nx = this.x + this.vx * h, ny = this.y + this.vy * h;
          const he = raycastEnemy(this.x, this.y, nx, ny, new Set([...hit, ...carried]));
          if (he && !this.hitEnemy(he.e, he.x, he.y)) break;
          if (ny >= GROUND - 4) { this.ang = Math.atan2(this.vy, this.vx); this.stick(null, nx, GROUND - 4); break; }
          if (this.vy > 0) { const pl = PLATFORMS.find(p => nx > p.x0 && nx < p.x1 && this.y <= p.y && ny >= p.y); if (pl) { this.ang = Math.atan2(this.vy, this.vx); this.stick(null, nx, pl.y); break; } }
          this.x = nx; this.y = ny; this.ang = Math.atan2(this.vy, this.vx);
        }
        if (this.x < -40 || this.x > WW + 40 || this.y < -400) this.recall();
        carried.forEach((c, i) => { if (!c.dead) { c.x = this.x - Math.cos(this.ang) * (14 + i * 34); c.y = Math.min(GROUND - c.ry, this.y - Math.sin(this.ang) * (14 + i * 34)); c.vx = 0; c.vy = 0; } });
        trail.unshift({ x: this.x, y: this.y }); if (trail.length > 9) trail.pop();
      } else if (this.state === 'stuck') {
        if (this.stuckE) { if (this.stuckE.dead) { this.stuckE = null; this.y = GROUND - 4; } else { this.x = this.stuckE.x + off.x; this.y = this.stuckE.y + off.y; } }
        if (G.t < pinUntil) pinned.forEach((c, i) => { if (!c.dead) { c.x = this.x - Math.cos(this.ang) * (14 + i * 34); c.y = Math.min(GROUND - c.ry, this.y - Math.sin(this.ang) * (14 + i * 34)); c.vx = 0; c.vy = 0; c.stunT = Math.max(c.stunT, 0.05); } });
        else pinned = [];
        if (slot.arch === 'rod' && hasMod(slot, 'storm')) { stormT -= dt; if (stormT <= 0) { const n = nearestEnemy(this.x - Math.cos(this.ang) * 50, this.y - Math.sin(this.ang) * 50, t.rodRadius); stormT = n ? t.stormEvery : 0.2; if (n) this.zapFor(n); } }
        const p = G.player;
        if (!this.stuckE && G.t - stuckAt > 0.5 && Math.hypot(p.x - this.x, p.y - 30 - this.y) < t.pickupRadius + 30) { this.picked(); return false; }   // 0,5 s de gracia: no se recoge sola al caer a tus pies
        if (G.t - stuckAt > t.stickTime) this.recall();
        trail.length = 0;
      } else if (this.state === 'recall') {
        const u = easeIn(seg(G.t, recallT0, recallT0 + t.recallTime)), m = muzzle();
        this.x = lerp(rx, m.x, u); this.y = lerp(ry, m.y, u) - Math.sin(Math.PI * u) * 30; this.ang = Math.atan2(m.y - ry, m.x - rx) + Math.PI;
        trail.unshift({ x: this.x, y: this.y }); if (trail.length > 9) trail.pop();
        if (u >= 1) { this.gone = true; sfx(SND_SPEAR.pick.fn); return false; }
      }
      return true;
    },
    draw(ctx, layer) {
      const a = this.state === 'recall' ? 0.85 : 1, ang = this.state === 'recall' ? this.ang : this.ang;
      crystalSpear(ctx, G.t, layer, this.x, this.y, ang, 0.85, pal, a, { trail, hot: this.state === 'fly' && heavy });
      if (this.state === 'stuck' && slot.arch === 'rod') rodSparks(ctx, G.t, layer, this.x - Math.cos(ang) * 50, this.y - Math.sin(ang) * 50, t.rodRadius * (this.stuckE && hasMod(slot, 'conduct') ? t.conductRadius : 1), PAL.storm, 1);
      if (this.state === 'stuck' && layer === 'front' && !this.stuckE) {                           // indicador «recógela»
        const pu = 0.5 + 0.5 * Math.sin(G.t * 6); ellArc(ctx, this.x, GROUND - 2, 16 + 4 * pu, 4, 0, TAU, 1.5, pal.light, 0.5 * pu, layer, { taper: 0, m: 20 });
      }
      if (charges > 0 && layer === 'front') { ctx.font = '700 10px system-ui'; ctx.fillStyle = '#e9e4ff'; ctx.textAlign = 'center'; ctx.fillText('⚡' + charges, this.x, this.y - 70); ctx.textAlign = 'left'; }
    },
  };
  return s;
}
