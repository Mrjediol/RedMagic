// g-pacto.js — 15 · Pacto de sangre, versión jugable. Ficha y decisiones: docs/armas/pacto.md · progresión: docs/armas/progresion.md
// Paga con TU VIDA: cada orbe de sangre cuesta un poco de vida y pega mucho; las bajas te la devuelven. Riesgo / recompensa y
// pareja natural de la etiqueta Robo de vida y de Tanque. Árbol: base → Hemorragia / Vampiro / Mártir → 3 mejoras.
// Elemento y Duplicador vienen de items (Duplicador: dos orbes por el mismo precio).
'use strict';
const SND_BLOOD = {
  cast: sound('blood_cast', 'Pacto', 'Orbe (pagas vida)', k => { const t = nowT(); vThump(t, 90, 50, 0.2 * k, 0.4, 1.5, 0.2); vNoise(t, 0.25 * k, 0.16, 'bandpass', 500, 1500, 2, { att: 0.01, wet: 0.3 }); }),
  hit: sound('blood_hit', 'Pacto', 'Impacto', k => { const t = nowT(); vThump(t, 130, 50, 0.16 * k, 0.5, 2.5); vNoise(t, 0.12 * k, 0.25, 'lowpass', 1800, 300, 1, { att: 0.002 }); }),
  heal: sound('blood_heal', 'Pacto', 'Te cura', k => { const t = nowT(); vTone(t, 523, 784, 0.25 * k, 0.04, 'sine', 0.3); }),
  burst: sound('blood_burst', 'Pacto', 'Hemorragia · derrame', k => { const t = nowT(); vThump(t, 160, 40, 0.25 * k, 0.6, 3); vNoise(t, 0.3 * k, 0.3, 'bandpass', 900, 300, 1.5, { att: 0.002, wet: 0.2 }); }),
  nova: sound('blood_nova', 'Pacto', 'Mártir · nova', k => { SFXLIB.boom(k, 1); const t = nowT(); vRing(t, [110, 165, 220], 1 * k, 0.06, 0.5); }),
  drain: sound('blood_drain', 'Pacto', 'Vampiro · drenar (tic)', k => { const t = nowT(); vTone(t, 220, 330, 0.08 * k, 0.03, 'sine', 0.2); }),
};
addWeapon({
  id: 'blood', name: 'Pacto de sangre', short: 'Pacto', pal: 'blood',  fresh: true,   // arma nueva (2026-10-20), sin probar por el usuario
  archetypes: [
    { id: 'bleed', name: 'Hemorragia', desc: 'El orbe hace SANGRAR (se acumula). Golpear con tu OTRA arma a un enemigo que sangra mucho revienta la sangre: daño extra y te cura.',
      ups: [{ id: 'artery', name: 'Arterias', desc: 'Al morir un enemigo que sangra, su sangrado pasa a los de alrededor.' },
            { id: 'transfuse', name: 'Transfusión', desc: 'El derrame cura el doble y lo que sobra se queda de escudo.' },
            { id: 'rage', name: 'Rabia', desc: 'Cuanta menos vida tengas, más daño hace el derrame.' }] },
    { id: 'vamp', name: 'Vampiro', desc: 'El orbe vuelve a ti después de golpear y te devuelve parte del daño que hizo.',
      ups: [{ id: 'thirst', name: 'Sed', desc: 'Cada baja del orbe te devuelve lo que costó y lo deja listo al instante.' },
            { id: 'bats', name: 'Murciélagos', desc: 'Al golpear, el orbe suelta murciélagos que buscan a otros enemigos.' },
            { id: 'drain', name: 'Drenar', desc: 'Mantén: en vez de lanzar, enlazas al enemigo más cercano al ratón y le chupas la vida (no cuesta).' }] },
    { id: 'martyr', name: 'Mártir', desc: 'Cuanta MENOS vida tienes, más daño hacen TODAS tus armas. El clic gasta mucha vida en una nova de sangre que crece con la vida que te falta.',
      ups: [{ id: 'breath', name: 'Último aliento', desc: 'Un golpe que te mataría te deja a 1 de vida y suelta una nova enorme (cada 30 s).' },
            { id: 'sacrifice', name: 'Sacrificio', desc: 'La nova cuesta más pero aturde a todos.' },
            { id: 'penance', name: 'Penitencia', desc: 'Cada vez que te hieren, tu siguiente ataque (de cualquier arma) es mucho más fuerte.' }] },
  ],
  tuning: {
    cooldown: [0.55, 0.1, 3, 0.05, 'Base: recarga (s)'], cost: [3, 0, 20, 0.5, 'Base: vida que cuesta cada orbe'], orbDamage: [38, 1, 200, 1, 'Base: daño del orbe'],
    orbSpeed: [700, 200, 1500, 10, 'Base: velocidad'], pierce: [1, 0, 5, 1, 'Base: enemigos que atraviesa'], healOnKill: [6, 0, 40, 1, 'Base: vida que devuelve cada baja del orbe'],
    iceFreeze: [0.4, 0, 1, 0.05, 'Hielo: congelación'], fireBurn: [2, 0, 6, 0.1, 'Fuego: quemadura (s)'],
    bleedMax: [5, 1, 12, 1, 'Hemorragia: cargas máximas'], bleedDps: [3, 0, 20, 0.5, 'Hemorragia: daño/s por carga'], bleedTime: [4, 0.5, 12, 0.5, 'Hemorragia: s que dura'],
    burstMin: [3, 1, 12, 1, 'Hemorragia: cargas para que reviente'], burstPer: [8, 0, 40, 1, 'Hemorragia: daño del derrame por carga'], burstHeal: [1, 0, 10, 0.5, 'Hemorragia: vida por carga'],
    arteryRadius: [110, 20, 300, 5, 'Hemorragia · Arterias: radio'], rageMax: [1, 0, 3, 0.05, 'Hemorragia · Rabia: + daño con 1 de vida (×)'],
    lifesteal: [0.35, 0, 1, 0.01, 'Vampiro: fracción del daño que te cura al volver'], batCount: [3, 1, 8, 1, 'Vampiro · Murciélagos: cuántos'], batDamage: [14, 1, 60, 1, 'Vampiro · Murciélagos: daño'],
    drainDps: [30, 0, 120, 1, 'Vampiro · Drenar: daño/s'], drainHeal: [0.5, 0, 1, 0.05, 'Vampiro · Drenar: fracción que cura'], drainRange: [280, 50, 600, 10, 'Vampiro · Drenar: alcance'],
    martyrMax: [0.6, 0, 2, 0.05, 'Mártir: + daño de todas tus armas sin vida (×)'], novaCost: [0.12, 0, 0.5, 0.01, 'Mártir: nova, fracción de vida máxima que cuesta'],
    novaRadius: [180, 40, 400, 5, 'Mártir: radio de la nova'], novaDamage: [40, 0, 200, 1, 'Mártir: daño fijo de la nova'], novaPerMissing: [1, 0, 5, 0.05, 'Mártir: + daño por punto de vida que te falta'],
    breathCd: [30, 5, 120, 1, 'Mártir · Último aliento: cada (s)'], sacrificeStun: [1.2, 0, 4, 0.1, 'Mártir · Sacrificio: aturdido (s)'], penanceMul: [2, 1, 5, 0.1, 'Mártir · Penitencia: × siguiente ataque'],
  },
  create(slot) {
    const id = 'blood';
    let cd = 0, breathT = -999, drainE = null, drainT = 0, healAcc = 0;
    const pal = () => elemPal('blood');
    const pay = c => { const p = G.player; if (c <= 0) return 1; if (p.hp > c + 1) { p.hp -= c; text(p.x + 16, p.y - 84, '-' + (Math.round(c * 10) / 10), '#e8304a'); return 1; } return 0.5; };   // sin vida para pagar: gratis pero a medias
    const heal = (n, shieldExtra) => {
      if (n <= 0) return; healAcc += n; if (healAcc < 1) return; const h = Math.floor(healAcc); healAcc -= h;
      const got = healPlayer(h), p = G.player; if (got > 0) text(p.x, p.y - 96, '+' + got, '#ff9a9a');
      if (shieldExtra && h - got > 0) p.shield = Math.min(40, (p.shield || 0) + h - got);
      if (!G._bhSfx || G.t - G._bhSfx > 0.2) { G._bhSfx = G.t; sfx(SND_BLOOD.heal.fn); }
    };
    const martyrK = () => slot.arch === 'martyr' ? 1 + T(id).martyrMax * (1 - G.player.hp / G.player.hpMax) : 1;
    const nova = (big) => {
      const t = T(id), p = G.player, x = p.x, y = p.y - 40, P = pal(), sac = hasMod(slot, 'sacrifice');
      const k = big ? 1 : pay(t.novaCost * p.hpMax * (sac ? 1.5 : 1));
      const dmg = (t.novaDamage + (p.hpMax - p.hp) * t.novaPerMissing) * k * (big ? 2 : 1), R = t.novaRadius * (big ? 1.5 : 1);
      const hit = damageArea(x, y, R, { dmg, slot, heavy: true, kx: 260, ky: -200, shake: 8, hitstop: 0.06, frame: true, elem: elemOf(), freeze: elemOf() === 'ice' ? t.iceFreeze : 0, burn: elemOf() === 'fire' ? t.fireBurn : 0 });
      if (sac || big) for (const e of hit) e.stunT = Math.max(e.stunT, t.sacrificeStun);
      addFx(0.6, (ctx, layer, f0) => bloodNova(ctx, G.t, layer, f0, x, y, R, P));
      sfx(SND_BLOOD.nova.fn);
    };
    const inst = {
      onPress(aim) {
        if (cd > 0) return;
        const t = T(id);
        if (slot.arch === 'vamp' && hasMod(slot, 'drain')) { drainE = null; drainT = 0; return; }   // Drenar: se hace en onHold
        cd = t.cooldown * cdMul(); G.player.castT = 0.25;
        if (slot.arch === 'martyr') { cd = Math.max(cd, 1.2 * cdMul()); nova(false); return; }
        const k = pay(t.cost), n = copies();
        for (let c = 0; c < n; c++) G.ents.push(bloodOrbEnt(slot, inst, aim.x, aim.y, aim.ang + (n > 1 ? (c ? 0.08 : -0.08) : 0), k));
        sfx(SND_BLOOD.cast.fn);
      },
      onHold(aim, dt) {
        if (!(slot.arch === 'vamp' && hasMod(slot, 'drain'))) return;
        const t = T(id), m = muzzle();
        if (!drainE || drainE.dead || Math.hypot(drainE.x - m.x, drainE.y - m.y) > t.drainRange * 1.2) { const n = nearestEnemy(aim.tx, aim.ty, 200); drainE = n && Math.hypot(n.x - m.x, n.y - m.y) < t.drainRange ? n : null; }
        G.player.castT = 0.1;
        if (!drainE) return;
        drainT -= dt;
        if (drainT <= 0) { drainT = 0.15; const d = damage(drainE, { dmg: t.drainDps * 0.15, slot, silentFlash: true, color: '#ff9a9a' }); heal(d * t.drainHeal); if (!G._drSfx || G.t - G._drSfx > 0.3) { G._drSfx = G.t; sfx(SND_BLOOD.drain.fn); } }
      },
      onRelease() { drainE = null; },
      update(dt, current) {
        const t = T(id);
        cd -= dt; if (!current) drainE = null;
        if (slot.arch === 'bleed') for (const e of alive()) {   // sangrado (también con la otra arma en la mano)
          if (!(e.bleedN > 0)) continue;
          if (G.t > e.bleedT) { e.bleedN = 0; continue; }
          e.bleedTick = (e.bleedTick || 0) - dt;
          if (e.bleedTick <= 0) { e.bleedTick = 0.5; damage(e, { dmg: t.bleedDps * 0.5 * e.bleedN, slot, dot: true, silentFlash: true, color: '#e8304a' }); }
        }
      },
      // Hemorragia: tu otra arma revienta la sangre
      onAnyHit(e, o) {
        if (slot.arch !== 'bleed' || o.slot === slot || !(e.bleedN >= T(id).burstMin) || e.dead) return;
        const t = T(id), n = e.bleedN, p = G.player; e.bleedN = 0;
        const rage = hasMod(slot, 'rage') ? 1 + t.rageMax * (1 - p.hp / p.hpMax) : 1;
        damage(e, { dmg: n * t.burstPer * rage, slot, proc: true, heavy: true, kx: 0, hitstop: 0.03, shake: 3, color: '#ff4a5a' });
        heal(n * t.burstHeal * (hasMod(slot, 'transfuse') ? 2 : 1), hasMod(slot, 'transfuse'));
        const x = e.x, y = e.y, P = pal(); addFx(0.7, (ctx, layer, f0) => { impact(ctx, G.t, layer, f0, x, y, 0.8, P, { spread: TAU, seed: Math.floor(x), drops: 12 }); });
        text(x, y - 66, 'DERRAME', '#ff4a5a', true); sfx(SND_BLOOD.burst.fn);
      },
      onAnyKill(e) {
        if (slot.arch === 'bleed' && hasMod(slot, 'artery') && e.bleedN > 0) for (const o of alive()) if (Math.hypot(o.x - e.x, o.y - e.y) < T(id).arteryRadius) { o.bleedN = Math.max(o.bleedN || 0, e.bleedN); o.bleedT = G.t + T(id).bleedTime; }
      },
      outMul() { return martyrK(); },
      onLethal() {
        if (slot.arch !== 'martyr' || !hasMod(slot, 'breath') || G.t - breathT < T(id).breathCd) return false;
        breathT = G.t; G.player.hurtT = 1.5; text(G.player.x, G.player.y - 120, 'ÚLTIMO ALIENTO', '#ff4a5a', true); nova(true); return true;
      },
      onHurt() { if (slot.arch === 'martyr' && hasMod(slot, 'penance')) { G.empower = T(id).penanceMul; G.empowerHit = false; G.empowerEnd = 0; text(G.player.x, G.player.y - 110, 'PENITENCIA', '#ff4a5a', true); } },
      heal, pay, drainTarget: () => drainE,
      refreshCd() { cd = 0; },
      ready() { return 1 - cd / Math.max(0.01, T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
    G.ents.push(bloodAuraEnt(slot, inst));
    return inst;
  },
});

// gotas de los que sangran, el hilo de Drenar y el pulso del Mártir (vive mientras el arma esté equipada)
function bloodAuraEnt(slot, inst) {
  return { kind: 'fx', update() { return slot.inst === inst && G.slots.slice(0, G.slotCount).includes(slot); },
    draw(ctx, layer) {
      const P = elemPal('blood');
      if (slot.arch === 'bleed') for (const e of alive()) if (e.bleedN > 0) bleedDrops(ctx, G.t, layer, e, e.bleedN, P);
      const d = inst.drainTarget(); if (d && !d.dead) { const m = muzzle(); energyBeam(ctx, G.t, layer, d.x, d.y, m.x, m.y, 0.5, P, { sc: 0.35, seed: 3 }); lifeMotes(ctx, G.t, layer, d.x, d.y, m.x, m.y, 1); }
      if (slot.arch === 'martyr' && layer === 'front') { const p = G.player, k = 1 - p.hp / p.hpMax; if (k > 0.05) glow(ctx, p.x, p.y - 40, 40 + 40 * k, P.g1, (0.2 + 0.4 * k) * (0.7 + 0.3 * Math.sin(G.t * (4 + 8 * k)))); }
    } };
}

// orbe de sangre. k = 1 pagado, 0.5 sin vida para pagar. Vampiro: vuelve y cura.
function bloodOrbEnt(slot, owner, x, y, ang, k) {
  const t = T('blood'), el = elemOf(), pal = elemPal('blood'), vamp = slot.arch === 'vamp', trail = [];
  let stored = 0, back = false;
  const o = {
    kind: 'orb', capturable: true, state: 'fly', slot, x, y, vx: Math.cos(ang) * t.orbSpeed, vy: Math.sin(ang) * t.orbSpeed, age: 0, dmgMul: 1, hit: new Set(), pierce: vamp ? 0 : t.pierce,   // Vampiro: vuelve en cuanto golpea
    clone() { const c = bloodOrbEnt(slot, owner, this.x, this.y, Math.atan2(this.vy, this.vx), k); c.dmgMul = this.dmgMul; return c; },
    update(dt) {
      this.age += dt; if (this.captured) { trail.length = 0; return true; }
      if (back) {                                                // Vampiro: vuelve al player y cura lo guardado
        const m = muzzle(), dx = m.x - this.x, dy = m.y - this.y, d = Math.hypot(dx, dy) || 1;
        this.vx = lerp(this.vx, dx / d * 900, Math.min(1, dt * 8)); this.vy = lerp(this.vy, dy / d * 900, Math.min(1, dt * 8)); this.x += this.vx * dt; this.y += this.vy * dt;
        trail.unshift({ x: this.x, y: this.y }); if (trail.length > 8) trail.pop();
        if (d < 24) { owner.heal(stored); return false; }
        return this.age < 4;
      }
      if (this.age > 1.1 + (this.ejected ? 0.6 : 0)) { if (vamp && stored > 0) { back = true; return true; } return false; }
      const nx = this.x + this.vx * dt, ny = this.y + this.vy * dt, h = raycastEnemy(this.x, this.y, nx, ny, this.hit);
      if (h) {
        const e = h.e; this.hit.add(e);
        const d = damage(e, { dmg: t.orbDamage * k * this.dmgMul, slot, kx: Math.sign(this.vx) * 120, hitstop: 0.03, shake: 3, elem: el, freeze: el === 'ice' ? t.iceFreeze : 0, burn: el === 'fire' ? t.fireBurn : 0,
          onKill: () => { owner.heal(t.healOnKill); if (vamp && hasMod(slot, 'thirst')) { owner.heal(t.cost); owner.refreshCd(); text(G.player.x, G.player.y - 110, 'SED', '#ff9a9a', true); } } });
        if (slot.arch === 'bleed') { e.bleedN = Math.min(t.bleedMax, (e.bleedN || 0) + 1); e.bleedT = G.t + t.bleedTime; }
        if (vamp) stored += d * t.lifesteal;
        if (vamp && hasMod(slot, 'bats') && !this.batted) { this.batted = true; for (let i = 0; i < t.batCount; i++) G.ents.push(bloodBat(slot, owner, h.x, h.y, i, e)); }
        const hx = h.x, hy = h.y, a = Math.atan2(this.vy, this.vx); addFx(0.8, (ctx, layer, f0) => impact(ctx, G.t, layer, f0, hx, hy, 0.55, pal, { dir: a + Math.PI, spread: 2.4, seed: Math.floor(hx), drops: 10 }));
        sfx(SND_BLOOD.hit.fn);
        if (this.pierce-- <= 0) { if (vamp) { back = true; return true; } return false; }
      }
      if (ny >= GROUND - 2) { if (vamp && stored > 0) { back = true; return true; } return false; }
      this.x = nx; this.y = ny; trail.unshift({ x: nx, y: ny }); if (trail.length > 8) trail.pop();
      return this.x > -50 && this.x < WW + 50 && this.y > -150;
    },
    draw(ctx, layer) { bloodOrb(ctx, G.t, layer, this.x, this.y, Math.atan2(this.vy, this.vx), k < 1 ? 0.75 : 1, pal, back ? 0.8 : 1, trail); },
  };
  return o;
}
// murciélago de sangre (Vampiro · Murciélagos): busca a otro enemigo, pica y vuelve curando
function bloodBat(slot, owner, x, y, i, skip) {
  const t = T('blood'), pal = elemPal('blood');
  let vx = Math.cos(i * 2.1) * 300, vy = -250, target = nearestEnemy(x, y, 400, new Set([skip])), done = false, stored = 0;
  return { kind: 'bat', update(dt) {
      const tg = !done && target && !target.dead ? target : null, m = muzzle(), px = tg ? tg.x : m.x, py = tg ? tg.y : m.y, dx = px - x, dy = py - y, d = Math.hypot(dx, dy) || 1;
      vx = lerp(vx, dx / d * 520, Math.min(1, dt * 5)); vy = lerp(vy, dy / d * 520, Math.min(1, dt * 5)); x += vx * dt; y += vy * dt;
      if (tg && d < tg.rx) { stored = damage(tg, { dmg: t.batDamage, slot, kx: Math.sign(vx) * 40, color: '#ff9a9a' }) * t.lifesteal; done = true; }
      if (!tg && d < 24) { owner.heal(stored); return false; }
      return true; },
    draw(ctx, layer) {
      if (layer !== 'front') return; const fl = Math.sin(G.t * 40 + i) * 0.8, a = Math.atan2(vy, vx);
      for (const s of [-1, 1]) lens(ctx, x + Math.cos(a + s * 1.6) * 6, y + Math.sin(a + s * 1.6) * 6 - fl * 2, a + s * (1.2 + fl * 0.5), 14, 5, pal.deep, 0.95, layer);
      disc(ctx, x, y, 4, pal.mid, 1, layer); disc(ctx, x + Math.cos(a) * 2, y + Math.sin(a) * 2, 1.4, pal.core, 1, layer);
    } };
}
