// g-enjambre.js — 11 · Enjambre, versión jugable. Ficha y decisiones: docs/armas/enjambre.md · progresión: docs/armas/progresion.md
// Suelta luciérnagas que vuelan hasta donde apuntas, se reparten entre los enemigos cercanos y los pican varias veces antes de
// volver. Daño pequeño y repartido que no hay que apuntar. Árbol: base → Colmena / Plaga / Faroles → 3 mejoras.
// Elemento y Duplicador vienen de items (el Duplicador dobla las luciérnagas).
'use strict';
const SND_SWARM = {
  release: sound('swarm_release', 'Enjambre', 'Suelta el enjambre', k => { const t = nowT(); for (let i = 0; i < 5; i++) vTone(t + i * 0.025 * k, 2200 + i * 260, 2600 + i * 200, 0.06 * k, 0.025, 'triangle', 0.25); vNoise(t, 0.3 * k, 0.08, 'bandpass', 3000, 5000, 3, { att: 0.02, lfo: [38, 0.7] }); }),
  sting: sound('swarm_sting', 'Enjambre', 'Picadura', k => { const t = nowT(); vTone(t, 3400, 2600, 0.03 * k, 0.03, 'square', 0.15, 0.001); }),
  infest: sound('swarm_infest', 'Enjambre', 'Plaga · se meten dentro', k => { const t = nowT(); vNoise(t, 0.25 * k, 0.1, 'bandpass', 900, 500, 3, { att: 0.01, lfo: [30, 0.8] }); }),
  jump: sound('swarm_jump', 'Enjambre', 'Plaga · saltan a otro', k => { const t = nowT(); for (let i = 0; i < 3; i++) vTone(t + i * 0.04 * k, 1800 + i * 400, 2600 + i * 400, 0.05 * k, 0.03, 'triangle', 0.3); }),
  lantern: sound('swarm_lantern', 'Enjambre', 'Faroles · se enciende', k => { const t = nowT(); vRing(t, [1046, 1568, 2093], 0.6 * k, 0.04, 0.5); vNoise(t, 0.2 * k, 0.06, 'highpass', 3000, 6000, 1, { rising: true }); }),
  flare: sound('swarm_flare', 'Enjambre', 'Faroles · bengala', k => { SFXLIB.boom(k, 0.55); const t = nowT(); vRing(t, [1318, 1976], 0.5 * k, 0.04, 0.4); }),
};
addWeapon({
  id: 'swarm', name: 'Enjambre', short: 'Enjambre', pal: 'ember',
  archetypes: [
    { id: 'hive', name: 'Colmena', desc: 'Las luciérnagas viven a tu alrededor y atacan SOLAS a cada enemigo que golpeas con CUALQUIER arma; el clic las manda todas a la vez.',
      ups: [{ id: 'queen', name: 'Reina', desc: 'Una luciérnaga grande más, cuyas picaduras son golpes fuertes.' },
            { id: 'brood', name: 'Cría', desc: 'Cada baja suma una luciérnaga más durante un rato.' },
            { id: 'nectar', name: 'Néctar', desc: 'Cada picadura recarga un poco tu otra arma.' }] },
    { id: 'plague', name: 'Plaga', desc: 'Las luciérnagas se METEN dentro del enemigo y lo queman por dentro; si muere, saltan al siguiente (y una más).',
      ups: [{ id: 'fever', name: 'Fiebre', desc: 'Los infestados reciben más daño de todo.' },
            { id: 'hatch', name: 'Eclosión', desc: 'Al morir un infestado, estalla.' },
            { id: 'spread', name: 'Contagio', desc: 'Cada poco, un infestado contagia a un vecino.' }] },
    { id: 'lantern', name: 'Faroles', desc: 'El clic planta un farol de luciérnagas: ILUMINA a los enemigos cercanos (tus golpes de cualquier arma pueden ser críticos ×2) y les pica despacio.',
      ups: [{ id: 'moth', name: 'Polillas', desc: 'Los faroles atraen a los enemigos hacia su luz.' },
            { id: 'flare', name: 'Bengala', desc: 'Al apagarse, el farol estalla y aturde.' },
            { id: 'guide', name: 'Luz guía', desc: 'Tus proyectiles que pasan cerca de un farol se curvan hacia los iluminados.' }] },
  ],
  tuning: {
    cooldown: [2.2, 0.2, 8, 0.1, 'Base: recarga (s)'], count: [4, 1, 12, 1, 'Base: luciérnagas por clic'], speed: [380, 100, 900, 10, 'Base: velocidad'],
    stingDamage: [5, 1, 40, 1, 'Base: daño por picadura'], stingGap: [0.3, 0.05, 1, 0.01, 'Base: s entre picaduras'], stings: [4, 1, 12, 1, 'Base: picaduras antes de volver'],
    seekRadius: [160, 30, 400, 5, 'Base: radio en el que buscan enemigos desde donde apuntas'],
    iceFreeze: [0.12, 0, 0.6, 0.01, 'Hielo: congelación por picadura'], fireBurn: [1, 0, 5, 0.1, 'Fuego: quemadura por picadura (s)'],
    hiveCount: [5, 1, 12, 1, 'Colmena: luciérnagas'], hiveStings: [2, 1, 8, 1, 'Colmena: picaduras al acudir solas'], queenMul: [3, 1, 8, 0.1, 'Colmena · Reina: × daño'],
    broodTime: [10, 1, 30, 0.5, 'Colmena · Cría: s que dura la extra'], broodMax: [6, 1, 12, 1, 'Colmena · Cría: extras máx.'], nectarRefund: [0.08, 0, 0.5, 0.01, 'Colmena · Néctar: s por picadura'],
    infestDps: [10, 1, 50, 1, 'Plaga: daño/s por luciérnaga dentro'], infestTime: [5, 1, 15, 0.5, 'Plaga: s dentro'], feverVuln: [1.25, 1, 3, 0.05, 'Plaga · Fiebre: × daño que reciben'],
    hatchDamage: [30, 0, 150, 1, 'Plaga · Eclosión: daño'], hatchRadius: [80, 20, 250, 5, 'Plaga · Eclosión: radio'], spreadEvery: [1.5, 0.3, 5, 0.1, 'Plaga · Contagio: cada (s)'],
    spreadMax: [12, 2, 30, 1, 'Plaga · Contagio: luciérnagas máx. en total'],
    lanternTime: [8, 1, 20, 0.5, 'Faroles: duración (s)'], lanternRadius: [140, 40, 300, 5, 'Faroles: radio de luz'], lanternMax: [3, 1, 6, 1, 'Faroles: a la vez'],
    litCrit: [0.2, 0, 1, 0.01, 'Faroles: probabilidad de crítico ×2 a los iluminados'], lanternDps: [6, 0, 40, 1, 'Faroles: daño/s a los de dentro'],
    mothSpeed: [70, 0, 300, 5, 'Faroles · Polillas: atracción'], flareDamage: [35, 0, 150, 1, 'Faroles · Bengala: daño'], flareRadius: [110, 20, 300, 5, 'Faroles · Bengala: radio'],
    guideTurn: [6, 0, 20, 0.5, 'Faroles · Luz guía: giro de tus proyectiles (rad/s)'],
  },
  create(slot) {
    const id = 'swarm';
    let cd = 0, flies = [], lanterns = [], brood = [], spreadT = 0;
    const pal = () => elemPal('ember');
    const want = () => {                                       // Colmena: cuántas viven alrededor (y cuáles son reina)
      const t = T(id), n = (t.hiveCount + (hasMod(slot, 'queen') ? 1 : 0)) * copies();
      return n + brood.filter(b => b > G.t).length;
    };
    const inst = {
      flies: () => flies, lanterns: () => lanterns,
      onPress(aim) {
        if (cd > 0) return;
        const t = T(id); cd = t.cooldown * cdMul(); G.player.castT = 0.25; sfx(SND_SWARM.release.fn);
        const tx = aim.tx, ty = Math.min(GROUND - 20, aim.ty);
        if (slot.arch === 'lantern') {
          const l = swarmLantern(slot, inst, tx, ty); lanterns.push(l); G.ents.push(l);
          if (lanterns.filter(x => !x.done).length > t.lanternMax) { const old = lanterns.find(x => !x.done); old.expire(); }
          return;
        }
        if (slot.arch === 'hive') { for (const f of flies) if (f.state === 'home') f.sendTo(tx, ty, t.stings); return; }
        const n = t.count * copies();
        for (let i = 0; i < n; i++) { const f = swarmFly(slot, inst, aim.x, aim.y, i, false); f.sendTo(tx, ty, t.stings); flies.push(f); G.ents.push(f); }
      },
      update(dt) {
        const t = T(id);
        cd -= dt;
        flies = flies.filter(f => !f.gone);
        if (slot.arch === 'hive') {                              // Colmena: mantener el número (también con la otra arma en la mano)
          brood = brood.filter(b => b > G.t);
          const n = want(), queens = hasMod(slot, 'queen') ? copies() : 0;
          while (flies.length < n) { const q = flies.filter(f => f.queen).length < queens; const f = swarmFly(slot, inst, G.player.x, G.player.y - 50, flies.length, true, q); flies.push(f); G.ents.push(f); }
          if (flies.length > n) { const extra = flies.filter(f => f.state === 'home' && !f.queen).slice(0, flies.length - n); extra.forEach(f => f.vanish()); }
        } else if (flies.some(f => f.hive)) flies.forEach(f => f.hive && f.vanish());
        lanterns = lanterns.filter(l => !l.done);
        // Plaga · Contagio: un infestado pasa una luciérnaga a un vecino
        if (slot.arch === 'plague' && hasMod(slot, 'spread')) {
          spreadT -= dt;
          if (spreadT <= 0) {
            spreadT = t.spreadEvery;
            const hosts = alive().filter(e => e.infest > 0);
            if (hosts.length && flies.length < t.spreadMax) { const h = hosts[Math.floor(Math.random() * hosts.length)], n = nearestEnemy(h.x, h.y, 140, new Set(hosts)); if (n) { const f = swarmFly(slot, inst, h.x, h.y, flies.length, false); flies.push(f); G.ents.push(f); f.attack(n, 99); sfx(SND_SWARM.jump.fn); } }
          }
        }
      },
      // Colmena: cada golpe de tus armas manda una luciérnaga libre a ese enemigo
      onAnyHit(e, o, dmg) {
        if (slot.arch !== 'hive' || o.slot === slot) return;
        const n = clamp(Math.round(dmg / 15), 1, 3);                // golpes grandes mandan más
        for (const f of flies.filter(f => f.state === 'home').slice(0, n)) f.attack(e, T(id).hiveStings);
      },
      onAnyKill(e) {
        const t = T(id);
        if (slot.arch === 'hive' && hasMod(slot, 'brood') && brood.filter(b => b > G.t).length < t.broodMax) brood.push(G.t + t.broodTime);
      },
      ready() { return 1 - cd / Math.max(0.01, T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
    return inst;
  },
});

// Una luciérnaga. Estados: home (Colmena: gira a tu alrededor) · go (vuela a un punto) · sting (pica a su objetivo) ·
// infest (Plaga: dentro del enemigo) · back (vuelve) · lantern (gira en un farol)
function swarmFly(slot, owner, x, y, idx, hive, queen = false) {
  const t = T('swarm'), el = elemOf(), trail = [], seed = idx * 1.7 + Math.random() * 10;
  let tx = x, ty = y, target = null, left = 0, stingT = 0, infT = 0, lantern = null, vx = (Math.random() - 0.5) * 200, vy = -150 - Math.random() * 100;
  const sc = queen ? 1.6 : 1;
  const f = {
    kind: 'fly', x, y, state: hive ? 'home' : 'go', hive, queen, gone: false,
    vanish() { this.gone = true; if (target && this.state === 'infest') target.infest = Math.max(0, (target.infest || 0) - 1); },
    sendTo(px, py, n) { tx = px; ty = py; left = n; target = null; this.state = 'go'; this.goT = G.t; },
    attack(e, n) { target = e; left = n; this.state = 'sting'; stingT = 0.12; },
    toLantern(l) { lantern = l; this.state = 'lantern'; },
    seek(px, py, sp, dt, k = 7) { const dx = px - this.x, dy = py - this.y, d = Math.hypot(dx, dy) || 1; vx = lerp(vx, dx / d * sp, Math.min(1, dt * k)); vy = lerp(vy, dy / d * sp, Math.min(1, dt * k)); this.x += vx * dt; this.y += vy * dt; return d; },
    sting(e) {
      const queenK = this.queen ? t.queenMul : 1;
      damage(e, { dmg: t.stingDamage * queenK, slot, heavy: this.queen, silentFlash: !this.queen, kx: Math.sign(e.x - this.x) * 20, elem: el, freeze: el === 'ice' ? t.iceFreeze : 0, burn: el === 'fire' ? t.fireBurn : 0 });
      const sx = this.x, sy = this.y, P = elemPal('ember'); addFx(0.25, (ctx, layer, f0) => stingFx(ctx, G.t, layer, f0, sx, sy, P, sc));
      if (slot.arch === 'hive' && hasMod(slot, 'nectar')) for (const so of otherSlots(slot)) if (so.inst && so.inst.refund) so.inst.refund(t.nectarRefund);
      if (!G._stingSfx || G.t - G._stingSfx > 0.05) { G._stingSfx = G.t; sfx(SND_SWARM.sting.fn); }
    },
    update(dt) {
      if (this.gone) return false;
      const p = G.player, sp = t.speed * (this.queen ? 0.85 : 1);
      if (this.state === 'home') {                              // gira alrededor del player
        const a = G.t * 2.2 + idx * 2.4, r = 34 + (idx % 3) * 9;
        this.seek(p.x + Math.cos(a) * r, p.y - 52 + Math.sin(a * 1.3) * r * 0.5, sp * 0.8, dt, 5);
      } else if (this.state === 'go') {
        const d = this.seek(tx, ty, sp, dt);
        if (d < 30 || G.t - this.goT > 1.2) {                      // llega: busca a quién picar (repartidas entre los cercanos)
          const near = alive().filter(e => Math.hypot(e.x - tx, e.y - ty) < t.seekRadius).sort((a, b) => Math.hypot(a.x - tx, a.y - ty) - Math.hypot(b.x - tx, b.y - ty));
          if (near.length) this.attack(near[idx % near.length], left); else this.state = 'back';
        }
      } else if (this.state === 'sting') {
        if (!target || target.dead) { const n = target ? nearestEnemy(this.x, this.y, t.seekRadius) : null; if (n && left > 0) target = n; else { this.state = 'back'; return true; } }
        if (slot.arch === 'plague') {                             // Plaga: entra
          if (this.seek(target.x, target.y, sp * 1.2, dt) < target.rx) { this.state = 'infest'; infT = G.t + t.infestTime; target.infest = (target.infest || 0) + 1; this.off = { x: (Math.random() - 0.5) * target.rx, y: (Math.random() - 0.5) * target.ry }; sfx(SND_SWARM.infest.fn); }
          return true;
        }
        const a = G.t * 6 + idx * 1.9, ox = Math.cos(a) * (target.rx + 12), oy = Math.sin(a) * (target.ry * 0.7);
        this.seek(target.x + ox, target.y + oy, sp, dt, 10);
        stingT -= dt;
        if (stingT <= 0 && Math.hypot(this.x - target.x, this.y - target.y) < target.rx + 30) { stingT = t.stingGap; this.sting(target); left--; if (left <= 0) this.state = this.hive ? 'home' : 'back'; }
      } else if (this.state === 'infest') {
        if (target.dead) { this.jump(); return !this.gone; }
        this.x = target.x + this.off.x; this.y = target.y + this.off.y;
        stingT -= dt;
        if (stingT <= 0) { stingT = 0.25; damage(target, { dmg: t.infestDps * 0.25, slot, silentFlash: true, color: '#ffd84a', elem: el, freeze: el === 'ice' ? t.iceFreeze * 0.5 : 0, burn: el === 'fire' ? t.fireBurn : 0 }); if (hasMod(slot, 'fever')) makeVuln(target, t.feverVuln, 0.4); }
        if (G.t > infT) { target.infest = Math.max(0, target.infest - 1); this.state = 'back'; }
      } else if (this.state === 'lantern') {
        if (!lantern || lantern.done) { this.gone = true; return false; }
        const a = G.t * 2.5 + idx * 1.3, r = 16 + (idx % 3) * 7;
        this.seek(lantern.x + Math.cos(a) * r, lantern.y + Math.sin(a * 1.4) * r * 0.7, sp * 0.6, dt, 6);
      } else {                                                    // back
        if (this.hive) { this.state = 'home'; return true; }
        if (this.seek(p.x, p.y - 50, sp * 1.1, dt) < 24) { this.gone = true; return false; }
      }
      trail.unshift({ x: this.x, y: this.y }); if (trail.length > 6) trail.pop();
      return true;
    },
    // Plaga: el huésped murió → salta al más cercano (y nace una más)
    jump() {
      const host = target; host.infest = 0;
      if (hasMod(slot, 'hatch') && !host._hatched) {
        host._hatched = true; const hx = host.x, hy = host.y, P = elemPal('ember');
        damageArea(hx, hy, t.hatchRadius, { dmg: t.hatchDamage, slot, proc: true, kx: 160, shake: 4, elem: el });
        addFx(0.9, (ctx, layer, f0) => impact(ctx, G.t, layer, f0, hx, hy, 0.8, P, { spread: TAU, seed: Math.floor(hx) }));
        sfx(k => SFXLIB.boom(k, 0.5));
      }
      const n = nearestEnemy(this.x, this.y, 260);
      if (!n) { this.state = 'back'; return; }
      this.attack(n, 99); sfx(SND_SWARM.jump.fn);
      if (!this.spawnedChild && owner.flies().length < t.spreadMax) { this.spawnedChild = true; const c = swarmFly(slot, owner, this.x, this.y, idx + 7, false); c.spawnedChild = true; c.attack(n, 99); owner.flies().push(c); G.ents.push(c); }
    },
    draw(ctx, layer) {
      if (this.state === 'infest') { if (layer === 'front') { const pu = 0.5 + 0.5 * Math.sin(G.t * 12 + seed); glow(ctx, this.x, this.y, 12, elemPal('ember').g1, 0.5 * pu); disc(ctx, this.x, this.y, 2, elemPal('ember').core, pu, layer); } return; }
      firefly(ctx, G.t, layer, this.x, this.y, Math.atan2(vy, vx), sc, elemPal('ember'), 1, trail, seed);
    },
  };
  return f;
}

// Farol: luz que ilumina (críticos), pica despacio, y según mejoras atrae, guía proyectiles y estalla al apagarse
function swarmLantern(slot, owner, x, y) {
  const t = T('swarm'), t0 = G.t, el = elemOf(), pal = elemPal('ember'), n = Math.max(3, t.count * copies());
  let tick = 0;
  const l = {
    kind: 'lantern', x, y, done: false,
    expire() {
      if (this.done) return; this.done = true;
      if (hasMod(slot, 'flare')) {
        for (const e of alive()) if (Math.hypot(e.x - x, e.y - y) < t.flareRadius) { damage(e, { dmg: t.flareDamage, slot, kx: Math.sign(e.x - x) * 180, shake: 4, elem: el, burn: el === 'fire' ? 2 : 0, freeze: el === 'ice' ? 0.5 : 0 }); e.stunT = Math.max(e.stunT, 0.6); }
        addFx(0.9, (ctx, layer, f0) => { impact(ctx, G.t, layer, f0, x, y, 1, pal, { spread: TAU, seed: 9 }); shockRing(ctx, G.t, layer, f0, x, y, t.flareRadius, pal); });
        sfx(SND_SWARM.flare.fn);
      }
    },
    update(dt) {
      if (this.done) return false;
      if (G.t - t0 > t.lanternTime) { this.expire(); return false; }
      const R = t.lanternRadius;
      for (const e of alive()) {
        const d = Math.hypot(e.x - x, e.y - y);
        if (d < R + e.rx * 0.5) { e.litT = G.t + 0.15; e.litChance = Math.max(e.litChance || 0, t.litCrit); }
        if (hasMod(slot, 'moth') && d < R * 1.8 && d > 30) e.pull = e.pull || { x, y: e.fly ? y : e.y, s: t.mothSpeed, lift: e.fly };
      }
      tick -= dt;
      if (tick <= 0 && t.lanternDps > 0) { tick = 0.5; for (const e of alive()) if (Math.hypot(e.x - x, e.y - y) < R) damage(e, { dmg: t.lanternDps * 0.5, slot, silentFlash: true, color: '#ffd84a', elem: el, freeze: el === 'ice' ? 0.08 : 0, burn: el === 'fire' ? 0.8 : 0 }); }
      // Luz guía: tus proyectiles cerca del farol giran hacia el iluminado más cercano
      if (hasMod(slot, 'guide')) for (const q of G.ents) {
        if (typeof q.vx !== 'number' || q.state !== 'fly' || q.hostile || q.captured || Math.hypot(q.x - x, q.y - y) > R * 1.6) continue;
        let best = null, bd = 9999; for (const e of alive()) if (e.litT > G.t) { const d = Math.hypot(e.x - q.x, e.y - q.y); if (d < bd) { bd = d; best = e; } }
        if (!best) continue;
        const sp = Math.hypot(q.vx, q.vy), a = Math.atan2(q.vy, q.vx), want = Math.atan2(best.y - q.y, best.x - q.x), da = ((want - a + Math.PI * 3) % TAU) - Math.PI, na = a + clamp(da, -t.guideTurn * dt, t.guideTurn * dt);
        q.vx = Math.cos(na) * sp; q.vy = Math.sin(na) * sp;
      }
      return true;
    },
    draw(ctx, layer) {
      const age = G.t - t0, a = easeOut(seg(age, 0, 0.25)) * (1 - seg(age, t.lanternTime - 0.5, t.lanternTime));
      lanternLight(ctx, G.t, layer, x, y, t.lanternRadius * (0.5 + 0.5 * easeOut(seg(age, 0, 0.3))), pal, a);
      for (let i = 0; i < n; i++) { const ang = G.t * 2.5 + i * TAU / n, r = 14 + (i % 3) * 6; firefly(ctx, G.t, layer, x + Math.cos(ang) * r, y + Math.sin(ang * 1.3) * r * 0.7, ang + Math.PI / 2, 0.9, pal, a, null, i); }
      if (layer === 'front') { glow(ctx, x, y, 34, pal.g2, 0.5 * a); star4(ctx, x, y, 10, G.t, pal.core, a, layer); }
    },
  };
  sfx(SND_SWARM.lantern.fn);
  return l;
}
