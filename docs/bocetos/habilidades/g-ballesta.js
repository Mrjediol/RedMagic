// g-ballesta.js — 18 · Ballesta de cristal, versión jugable. Ficha y decisiones: docs/armas/ballesta.md · progresión: docs/armas/progresion.md
// Precisión: mantén para apuntar (mira láser; cuanto más esperas, más daño) y suelta: virote muy rápido y recto. Darle en la
// parte de arriba del enemigo (el «ojo») es CRÍTICO. Árbol: base → Perforador / Rebote / Marcador → 3 mejoras.
// Elemento y Duplicador vienen de items (Duplicador: un segundo virote un instante después).
'use strict';
const SND_BOW = {
  draw: sound('bow_draw', 'Ballesta', 'Tensar', k => { const t = nowT(); vRise(t, 'triangle', 300, 900, 0.6 * k, 0.03, 800, 3000); vNoise(t, 0.6 * k, 0.05, 'bandpass', 2000, 4000, 4, { rising: true }); }),
  ready: sound('bow_ready', 'Ballesta', 'Concentración máxima', k => { const t = nowT(); vTone(t, 2637, 2637, 0.12 * k, 0.04, 'sine', 0.3); }),
  shoot: sound('bow_shoot', 'Ballesta', 'Disparo', k => { const t = nowT(); vThump(t, 220, 90, 0.06 * k, 0.45, 2); vNoise(t, 0.12 * k, 0.35, 'highpass', 2500, 7000, 1, { att: 0.001, wet: 0.1 }); }),
  hit: sound('bow_hit', 'Ballesta', 'Impacto', k => { const t = nowT(); vThump(t, 160, 60, 0.12 * k, 0.5, 2.5); vNoise(t, 0.05 * k, 0.3, 'highpass', 3000, 7000, 1, { att: 0.001 }); }),
  crit: sound('bow_crit', 'Ballesta', 'Crítico', k => { const t = nowT(); vRing(t, [1976, 2960, 3951], 0.4 * k, 0.06, 0.3); vThump(t, 120, 50, 0.15 * k, 0.6, 3); }),
  bounce: sound('bow_bounce', 'Ballesta', 'Rebote', k => { const t = nowT(); vTone(t, 3200, 1800, 0.08 * k, 0.05, 'triangle', 0.2); }),
};
addWeapon({
  id: 'bow', name: 'Ballesta de cristal', short: 'Ballesta', pal: 'ice',  fresh: true,   // arma nueva (2026-10-20), sin probar por el usuario
  archetypes: [
    { id: 'pierce', name: 'Perforador', desc: 'El virote atraviesa a TODOS los de la línea y gana daño con cada uno que atraviesa.',
      ups: [{ id: 'caliber', name: 'Calibre', desc: 'Con la concentración llena, el virote es enorme y lanza a los que atraviesa.' },
            { id: 'cadence', name: 'Cadencia', desc: 'Cada baja deja el siguiente disparo ya concentrado.' },
            { id: 'volley', name: 'Ráfaga', desc: 'Con la concentración llena dispara tres virotes seguidos.' }] },
    { id: 'ricochet', name: 'Rebote', desc: 'El virote rebota en el suelo y las plataformas (la mira enseña el recorrido); cada rebote suma daño y lo que golpea después de rebotar es crítico.',
      ups: [{ id: 'more', name: 'Más rebotes', desc: 'Dos rebotes más.' },
            { id: 'billiard', name: 'Carambola', desc: 'También rebota en los enemigos, hacia el siguiente más cercano.' },
            { id: 'splinter', name: 'Astillas', desc: 'Cada rebote suelta dos astillas pequeñas.' }] },
    { id: 'spotter', name: 'Marcador', desc: 'El virote deja un PUNTO DÉBIL: los siguientes golpes de tu OTRA arma a ese enemigo son críticos.',
      ups: [{ id: 'spots', name: 'Puntos débiles', desc: 'El punto débil aguanta más golpes.' },
            { id: 'expose', name: 'Exponer', desc: 'El marcado recibe además más daño de todo.' },
            { id: 'bounty', name: 'Recompensa', desc: 'Matar a un marcado recarga del todo tu otra arma y deja el siguiente virote concentrado.' }] },
  ],
  tuning: {
    cooldown: [0.35, 0.05, 3, 0.05, 'Base: recarga (s)'], focusTime: [0.9, 0.1, 3, 0.05, 'Base: s para concentración máxima'], aimMove: [0.5, 0.1, 1, 0.05, 'Base: × tu velocidad apuntando'],
    dmgMin: [18, 1, 100, 1, 'Base: daño sin concentrar'], dmgMax: [90, 1, 300, 1, 'Base: daño con concentración llena (golpe fuerte)'], speed: [1800, 400, 3000, 10, 'Base: velocidad del virote'],
    critMul: [1.8, 1, 4, 0.05, 'Base: × crítico (parte de arriba del enemigo)'], critZone: [0.35, 0, 1, 0.05, 'Base: zona de crítico (fracción de arriba del cuerpo)'], knock: [120, 0, 600, 5, 'Base: empuje'],
    iceFreeze: [0.5, 0, 1, 0.05, 'Hielo: congelación'], fireBurn: [2, 0, 6, 0.1, 'Fuego: quemadura (s)'],
    pierceUp: [0.25, 0, 1, 0.05, 'Perforador: + daño por enemigo atravesado'], caliberPush: [700, 0, 1500, 10, 'Perforador · Calibre: empuje (lanzado)'], volleyGap: [0.09, 0.03, 0.4, 0.01, 'Perforador · Ráfaga: s entre virotes'],
    bounces: [2, 0, 8, 1, 'Rebote: rebotes'], bounceUp: [0.3, 0, 1, 0.05, 'Rebote: + daño por rebote'], splinterDamage: [10, 1, 50, 1, 'Rebote · Astillas: daño'],
    weakHits: [3, 1, 10, 1, 'Marcador: golpes críticos que aguanta el punto débil'], weakTime: [6, 1, 15, 0.5, 'Marcador: s que dura'], spotsHits: [3, 0, 10, 1, 'Marcador · Puntos débiles: golpes extra'],
    exposeVuln: [1.25, 1, 3, 0.05, 'Marcador · Exponer: × daño que recibe'],
  },
  create(slot) {
    const id = 'bow';
    let cd = 0, charge = null, nextFull = false, readySaid = false;
    const pal = () => elemPal('ice');
    const fire = (aim, k) => {
      const n = copies(), t = T(id);
      for (let c = 0; c < n; c++) { const go = () => { const a = aimInfo(); G.ents.push(bowBolt(slot, inst, a.x, a.y, a.ang + (c ? 0.03 : 0), k)); }; if (c) { const t0 = G.t; G.ents.push({ kind: 'timer', update() { if (G.t - t0 < 0.08) return true; go(); return false; }, draw() {} }); } else go(); }
      G.player.castT = 0.25; G.player.vx -= Math.cos(aim.ang) * 60 * k; sfx(SND_BOW.shoot.fn);
      if (slot.arch === 'pierce' && hasMod(slot, 'volley') && k >= 1) for (let i = 1; i <= 2; i++) { const t0 = G.t; G.ents.push({ kind: 'timer', update() { if (G.t - t0 < t.volleyGap * i) return true; const a = aimInfo(); G.ents.push(bowBolt(slot, inst, a.x, a.y, a.ang, k)); sfx(SND_BOW.shoot.fn); return false; }, draw() {} }); }
    };
    const inst = {
      onPress() { if (cd > 0 || charge) return; charge = { t0: G.t }; readySaid = false; G.ents.push(bowSightEnt(slot, inst)); sfx(SND_BOW.draw.fn); },
      onHold(aim) {
        if (!charge) return; const t = T(id); G.player.speedMul = Math.min(G.player.speedMul || 1, t.aimMove); G.player.castT = 0.1;
        if (!readySaid && inst.focus() >= 1) { readySaid = true; sfx(SND_BOW.ready.fn); }
      },
      onRelease(aim) {
        if (!charge) return;
        const k = inst.focus(); charge = null; nextFull = false; cd = T(id).cooldown * cdMul(); fire(aim, k);
      },
      update(dt, current) { cd -= dt; if (charge && !current) charge = null; },
      focus() { return charge ? (nextFull ? 1 : clamp((G.t - charge.t0) / T(id).focusTime)) : 0; },
      charging: () => !!charge,
      setNextFull() { nextFull = true; },
      // Marcador: los golpes de tu otra arma a un punto débil son críticos
      outMul(e, o) {
        if (slot.arch !== 'spotter' || o.slot === slot || !(e.weakN > 0) || G.t > e.weakT) return 1;
        e.weakN--; text(e.x - 10, e.y - e.ry - 44, 'CRÍTICO', '#e4f9ff', true); if (!G._wkSfx || G.t - G._wkSfx > 0.08) { G._wkSfx = G.t; sfx(SND_BOW.crit.fn); }
        return T(id).critMul;
      },
      onAnyKill(e) {
        if (slot.arch === 'spotter' && hasMod(slot, 'bounty') && e._weakBy === slot && G.t < e.weakT) { for (const so of otherSlots(slot)) if (so.inst && so.inst.refund) so.inst.refund(99); nextFull = true; text(G.player.x, G.player.y - 110, 'RECOMPENSA', '#e4f9ff', true); }
      },
      ready() { return charge ? inst.focus() : 1 - cd / Math.max(0.01, T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
    G.ents.push(bowSpotsEnt(slot, inst));
    return inst;
  },
});

// recorrido recto con rebotes en suelo y plataformas (para la mira y para el virote); se para en el primer enemigo si stopAtEnemy
function bowPath(x, y, ang, bounces, maxLen = 1100, stopAtEnemy = true) {
  const pts = [{ x, y }]; let vx = Math.cos(ang), vy = Math.sin(ang), left = maxLen, b = bounces;
  while (left > 0) {
    let best = left, kind = 'end';
    if (vy > 0) { const d = (GROUND - y) / vy; if (d > 0.5 && d < best) { best = d; kind = 'ground'; } for (const p of PLATFORMS) { const dp = (p.y - y) / vy; if (dp > 0.5 && dp < best) { const px = x + vx * dp; if (px > p.x0 && px < p.x1) { best = dp; kind = 'plat'; } } } }
    const ex = x + vx * best, ey = y + vy * best;
    if (stopAtEnemy) { const h = raycastEnemy(x, y, ex, ey); if (h) { pts.push({ x: h.x, y: h.y }); return pts; } }
    pts.push({ x: ex, y: ey }); left -= best; x = ex; y = ey;
    if (kind === 'end' || b <= 0) return pts;
    vy = -vy; b--;
  }
  return pts;
}

function bowSightEnt(slot, inst) {
  return { kind: 'fx', update() { return inst.charging() && G.slots.includes(slot); },
    draw(ctx, layer) { const a = aimInfo(), t = T('bow'), b = slot.arch === 'ricochet' ? t.bounces + (hasMod(slot, 'more') ? 2 : 0) : 0, k = inst.focus();
      laserSight(ctx, G.t, layer, bowPath(a.x, a.y, a.ang, b, 1100, slot.arch !== 'pierce'), k, elemPal('ice'));
      if (layer === 'front') { const P = elemPal('ice'); glow(ctx, a.x, a.y, 14 + 14 * k, P.g1, 0.5 * k); } } };
}
function bowSpotsEnt(slot, inst) {
  return { kind: 'fx', update() { return slot.inst === inst && G.slots.slice(0, G.slotCount).includes(slot); },
    draw(ctx, layer) { if (slot.arch !== 'spotter') return; for (const e of alive()) if (e.weakN > 0 && G.t < e.weakT) weakSpot(ctx, G.t, layer, e, e.weakN, elemPal('ice')); } };
}

// virote: recto y muy rápido; crítico si da en la parte de arriba del cuerpo
function bowBolt(slot, owner, x, y, ang, k, o = {}) {
  const t = T('bow'), el = elemOf(), pal = elemPal('ice'), arch = slot.arch, trail = [];
  const big = arch === 'pierce' && hasMod(slot, 'caliber') && k >= 1;
  let bounces = o.splinter ? 0 : arch === 'ricochet' ? t.bounces + (hasMod(slot, 'more') ? 2 : 0) : 0, nb = 0, pierced = 0;
  const b = {
    kind: 'bolt', capturable: !o.splinter, state: 'fly', slot, x, y, vx: Math.cos(ang) * t.speed * (o.splinter ? 0.5 : 1), vy: Math.sin(ang) * t.speed * (o.splinter ? 0.5 : 1), age: 0, dmgMul: 1, hit: new Set(),
    clone() { const c = bowBolt(slot, owner, this.x, this.y, Math.atan2(this.vy, this.vx), k, o); c.dmgMul = this.dmgMul; return c; },
    onEnemy(h) {
      const e = h.e; this.hit.add(e);
      const crit = h.y < e.y - e.ry * (1 - 2 * t.critZone) || (arch === 'ricochet' && nb > 0);
      let dmg = (o.splinter ? t.splinterDamage : lerp(t.dmgMin, t.dmgMax, k)) * this.dmgMul * (1 + nb * t.bounceUp) * (1 + pierced * t.pierceUp) * (crit ? t.critMul : 1);
      damage(e, { dmg, slot, heavy: k >= 1 || crit, kx: Math.sign(this.vx) * (big ? t.caliberPush : t.knock), ky: big && !e.fly ? -200 : 0, launch: big ? 0.6 : 0, elem: el, freeze: el === 'ice' ? t.iceFreeze : 0, burn: el === 'fire' ? t.fireBurn : 0,
        hitstop: crit ? 0.05 : 0.02, shake: crit ? 5 : 2, frame: crit && k >= 1, onKill: arch === 'pierce' && hasMod(slot, 'cadence') ? () => owner.setNextFull() : null });
      if (crit) { text(e.x + 10, e.y - e.ry - 30, '¡CRÍTICO!', '#e4f9ff', true); sfx(SND_BOW.crit.fn); } else sfx(SND_BOW.hit.fn);
      if (arch === 'spotter' && !o.splinter) { e.weakN = Math.max(e.weakN || 0, t.weakHits + (hasMod(slot, 'spots') ? t.spotsHits : 0)); e.weakT = G.t + t.weakTime; e._weakBy = slot; if (hasMod(slot, 'expose')) makeVuln(e, t.exposeVuln, t.weakTime); }
      const hx = h.x, hy = h.y, a = Math.atan2(this.vy, this.vx), P = pal; addFx(0.7, (ctx, layer, f0) => impact(ctx, G.t, layer, f0, hx, hy, crit ? 0.8 : 0.45, P, { dir: a + Math.PI, spread: 1.4, seed: Math.floor(hx) }));
      if (arch === 'pierce') { pierced++; return true; }
      if (arch === 'ricochet' && hasMod(slot, 'billiard') && bounces > 0) { const n = nearestEnemy(e.x, e.y, 500, this.hit); if (n) { bounces--; nb++; const sp = Math.hypot(this.vx, this.vy), na = Math.atan2(n.y - h.y, n.x - h.x); this.vx = Math.cos(na) * sp; this.vy = Math.sin(na) * sp; this.x = h.x; this.y = h.y; sfx(SND_BOW.bounce.fn); return true; } }
      return false;
    },
    bounce(nx, ny) {
      bounces--; nb++; this.vy = -this.vy; this.x = nx; this.y = ny; this.hit.clear();
      const P = pal; addFx(0.4, (ctx, layer, f0) => sparks(ctx, G.t, f0, nx, ny, layer, { n: 6, pal: P, sp: 200, a0: -Math.PI * 0.9, a1: -Math.PI * 0.1, seed: Math.floor(nx), len: 10, life: 0.3 }));
      sfx(SND_BOW.bounce.fn);
      if (hasMod(slot, 'splinter')) for (const s of [-0.5, 0.5]) G.ents.push(bowBolt(slot, owner, nx, ny - 2, Math.atan2(this.vy, this.vx) + s, k, { splinter: true }));
    },
    update(dt) {
      this.age += dt; if (this.captured) { trail.length = 0; return true; }
      if (this.age > (o.splinter ? 0.3 : 0.9) + (this.ejected ? 0.6 : 0)) return false;
      const n = 6;
      for (let i = 0; i < n; i++) {
        const nx = this.x + this.vx * dt / n, ny = this.y + this.vy * dt / n, h = raycastEnemy(this.x, this.y, nx, ny, this.hit);
        if (h) { if (!this.onEnemy(h)) return false; continue; }
        if (ny >= GROUND - 1 && this.vy > 0) { if (bounces > 0) { this.bounce(nx, GROUND - 2); continue; } const P = pal; addFx(0.4, (ctx, layer, f0) => sparks(ctx, G.t, f0, nx, GROUND - 2, layer, { n: 5, pal: P, sp: 160, a0: -Math.PI * 0.9, a1: -Math.PI * 0.1, seed: Math.floor(nx), len: 8, life: 0.25 })); return false; }
        if (this.vy > 0 && bounces > 0) { const pl = PLATFORMS.find(p => nx > p.x0 && nx < p.x1 && this.y <= p.y && ny >= p.y); if (pl) { this.bounce(nx, pl.y - 1); continue; } }
        this.x = nx; this.y = ny;
      }
      trail.unshift({ x: this.x, y: this.y }); if (trail.length > 5) trail.pop();
      return this.x > -50 && this.x < WW + 50 && this.y > -200;
    },
    draw(ctx, layer) { crystalBolt(ctx, G.t, layer, this.x, this.y, Math.atan2(this.vy, this.vx), o.splinter ? 0.45 : big ? 1.8 : 1, pal, 1, trail); },
  };
  return b;
}
