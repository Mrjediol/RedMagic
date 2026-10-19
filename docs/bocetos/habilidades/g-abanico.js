// g-abanico.js — 1 · Abanico de cristal, versión jugable. Ficha y decisiones: docs/armas/abanico.md · progresión: docs/armas/progresion.md
// Árbol: base → evolución (Erizo / Lanza / Caleidoscopio) → 3 mejoras de esa evolución. Elemento y Duplicador vienen de items.
'use strict';
addWeapon({
  id: 'fan', name: 'Abanico de cristal', short: 'Abanico', pal: 'set',
  archetypes: [
    { id: 'spike', name: 'Erizo', desc: 'Se clavan en enemigos y suelo y estallan al cambiar de arma (o solas al rato). Premia alternar armas.',
      ups: [{ id: 'ret', name: 'Retorno', desc: 'Tras estallar vuelven al cristal atravesando enemigos; cada una recarga el arma de reserva.' },
            { id: 'shrap', name: 'Metralla', desc: 'Cada estallido suelta esquirlas pequeñas en todas direcciones.' },
            { id: 'bleed', name: 'Hemorragia', desc: 'Cada esquirla clavada hace daño continuo mientras está dentro.' }] },
    { id: 'lance', name: 'Lanza', desc: 'Ráfaga en línea, rápida y de largo alcance, que atraviesa al primer enemigo.',
      ups: [{ id: 'pierce', name: 'Perforación', desc: 'Atraviesan a todos; cada enemigo atravesado suma daño.' },
            { id: 'accel', name: 'Aceleración', desc: 'Manteniendo el disparo, cada ráfaga sale antes que la anterior.' },
            { id: 'big', name: 'Cristal mayor', desc: 'La última de cada ráfaga es una lanza grande: mucho daño y golpe fuerte.' }] },
    { id: 'kal', name: 'Caleidoscopio', desc: 'Rebotan en suelo, plataformas y enemigos, y cada rebote suma daño.',
      ups: [{ id: 'seek', name: 'Buscador', desc: 'Todos los rebotes van hacia el enemigo más cercano.' },
            { id: 'split', name: 'Prisma', desc: 'Al rebotar en un enemigo se divide en dos.' },
            { id: 'eternal', name: 'Eterno', desc: '+3 rebotes y no caducan mientras sigan rebotando.' }] },
  ],
  // [valor, mín, máx, paso, etiqueta] — nombres = futuros campos en Unity
  tuning: {
    cooldown: [0.45, 0.1, 2, 0.01, 'Base: recarga (s)'], count: [5, 1, 12, 1, 'Base: esquirlas'], spread: [0.2, 0, 0.8, 0.01, 'Base: apertura (rad, media)'],
    speed: [950, 300, 2000, 10, 'Base: velocidad (px/s)'], damage: [9, 1, 60, 1, 'Base: daño por esquirla'], knockback: [60, 0, 400, 5, 'Base: empuje'],
    life: [0.6, 0.1, 2, 0.05, 'Base: alcance (s de vuelo)'], freeze: [0.34, 0, 1, 0.01, 'Hielo: congelación por esquirla'], burn: [1.5, 0, 6, 0.1, 'Fuego: quemadura (s)'],
    spikeLife: [2.5, 0.3, 8, 0.1, 'Erizo: estallan solas a los (s)'], spikeBlast: [10, 0, 60, 1, 'Erizo: daño del estallido'], spikeBlastRadius: [34, 8, 120, 2, 'Erizo: radio del estallido'],
    returnDamage: [7, 0, 60, 1, 'Erizo · Retorno: daño al volver'], refund: [0.15, 0, 1, 0.01, 'Erizo · Retorno: recarga del arma de reserva por esquirla (s)'],
    shrapCount: [3, 1, 8, 1, 'Erizo · Metralla: esquirlas por estallido'], shrapDamage: [4, 1, 30, 1, 'Erizo · Metralla: daño'], bleedDps: [4, 0, 30, 0.5, 'Erizo · Hemorragia: daño/s por clavada'],
    lanceCooldown: [0.55, 0.1, 2, 0.01, 'Lanza: recarga (s)'], lanceCount: [5, 1, 12, 1, 'Lanza: esquirlas por ráfaga'], lanceGap: [0.035, 0.01, 0.15, 0.005, 'Lanza: tiempo entre esquirlas (s)'],
    lanceSpeed: [1350, 400, 2500, 10, 'Lanza: velocidad'], lanceLife: [0.75, 0.1, 2, 0.05, 'Lanza: alcance (s)'], pierceDmgUp: [0.25, 0, 1, 0.05, 'Lanza · Perforación: + daño por atravesado'],
    accelStep: [0.12, 0, 0.5, 0.01, 'Lanza · Aceleración: − recarga por ráfaga'], accelMin: [0.5, 0.1, 1, 0.05, 'Lanza · Aceleración: recarga mínima (×)'],
    bigMul: [3, 1, 8, 0.1, 'Lanza · Cristal mayor: × daño'], bigSize: [1.8, 1, 3, 0.05, 'Lanza · Cristal mayor: tamaño'],
    kalBounces: [3, 1, 10, 1, 'Caleidoscopio: rebotes'], kalBounceDmg: [0.25, 0, 1, 0.05, 'Caleidoscopio: + daño por rebote'], kalSeekRange: [500, 100, 900, 10, 'Caleidoscopio: alcance al buscar'],
    splitMul: [0.5, 0.1, 1, 0.05, 'Caleidoscopio · Prisma: daño de la copia (×)'],
  },
  create(slot) {
    const id = 'fan';
    let cd = 0, accel = 0, queue = [];
    const pal = () => elemPal(slot.arch === 'kal' ? 'gold' : 'set');
    const fire = aim => {
      const t = T(id), n = copies(), P = pal();
      G.player.castT = 0.12;
      if (slot.arch === 'lance') {
        const base = t.lanceCooldown * cdMul();
        cd = base * Math.max(t.accelMin, 1 - (hasMod(slot, 'accel') ? accel : 0));
        if (hasMod(slot, 'accel')) accel = Math.min(1 - t.accelMin, accel + t.accelStep);
        for (let c = 0; c < n; c++) {                       // Duplicador: dos líneas paralelas
          const off = n > 1 ? (c ? 7 : -7) : 0;
          for (let i = 0; i < t.lanceCount; i++) queue.push({ at: G.t + i * t.lanceGap, last: i === t.lanceCount - 1, off });
        }
      } else {
        cd = t.cooldown * cdMul();
        const cnt = t.count * n, spr = t.spread * (n > 1 ? 1.35 : 1);
        for (let i = 0; i < cnt; i++) {
          const u = cnt === 1 ? 0.5 : i / (cnt - 1), a = aim.ang + lerp(-spr, spr, u) + (Math.random() - 0.5) * 0.02, sp = t.speed * (0.95 + Math.random() * 0.1);
          G.ents.push(fanShard(slot, aim.x, aim.y, Math.cos(a) * sp, Math.sin(a) * sp, P, {}));
        }
      }
      addFx(0.25, (ctx, layer, t0) => castFx(ctx, G.t, layer, t0 - 0.01, t0, P, 0.8, { x: aim.x, y: aim.y, dir: aim.ang }));
      sfx(SND.fan_release.fn);
    };
    return {
      onPress(aim) { if (cd <= 0) fire(aim); },
      onHold(aim) { if (cd <= 0) fire(aim); },
      onRelease() { accel = 0; },
      update(dt) {
        cd -= dt;
        if (queue.length) {                                  // ráfaga de la Lanza: sale en el momento, hacia donde apuntas ahora
          const t = T(id), aim = aimInfo();
          while (queue.length && queue[0].at <= G.t) {
            const q = queue.shift(), a = aim.ang + (Math.random() - 0.5) * 0.03, nx = -Math.sin(a), ny = Math.cos(a);
            const big = q.last && hasMod(slot, 'big');
            G.ents.push(fanShard(slot, aim.x + nx * q.off, aim.y + ny * q.off, Math.cos(a) * t.lanceSpeed, Math.sin(a) * t.lanceSpeed, pal(), { lance: true, big }));
          }
        }
      },
      ready() { return 1 - cd / Math.max(0.01, (slot.arch === 'lance' ? T(id).lanceCooldown : T(id).cooldown) * cdMul()); },
      refund(s) { cd -= s; },
    };
  },
});

// Esquirla: vuela, choca (enemigo / suelo / plataforma) y según la evolución rebota, atraviesa, se clava o vuelve.
// o = { lance, big, mini (metralla), split (copia del Prisma), bounces, dmgMul }
function fanShard(slot, x, y, vx, vy, pal, o) {
  const t = T('fan'), el = elemOf(), arch = slot.arch;
  const spike = arch === 'spike' && !o.mini, kal = arch === 'kal', lance = !!o.lance;
  const size = o.big ? t.bigSize : o.mini ? 0.55 : 1;
  const s = {
    kind: 'shard', capturable: !o.mini, slot, x, y, vx, vy, pal, state: 'fly', age: 0, hit: new Set(), trail: [], ang: Math.atan2(vy, vx),
    bounces: o.bounces !== undefined ? o.bounces : kal ? t.kalBounces + (hasMod(slot, 'eternal') ? 3 : 0) : 0, nb: 0,
    pierce: lance ? (hasMod(slot, 'pierce') ? 99 : 1) : 0, pierced: 0, dmgMul: o.dmgMul || 1,
    life: o.mini ? 0.25 : lance ? t.lanceLife : t.life,
    stickE: null, ox: 0, oy: 0, stickAt: 0, bleedT: 0,
    update(dt) {
      this.age += dt;
      if (this.captured) { this.push(); return true; }
      if (this.state === 'fly') return this.fly(dt);
      if (this.state === 'stick') return this.stuck(dt);
      if (this.state === 'return') return this.back(dt);
      return false;
    },
    push() { this.trail.push({ x: this.x, y: this.y }); if (this.trail.length > 8) this.trail.shift(); },
    fly(dt) {
      if (this.age > this.life + (this.ejected ? 0.6 : 0)) return false;
      const n = 3;
      for (let k = 0; k < n; k++) {
        const nx = this.x + this.vx * dt / n, ny = this.y + this.vy * dt / n;
        const h = raycastEnemy(this.x, this.y, nx, ny, this.hit);
        if (h) { const keep = this.onEnemy(h); if (!keep) return false; if (this.state !== 'fly') return true; continue; }
        if (ny >= GROUND - 2) { const keep = this.onSurface(nx, GROUND - 2); if (!keep) return false; if (this.state !== 'fly') return true; continue; }
        if (kal && this.vy > 0) { const pl = PLATFORMS.find(p => nx > p.x0 && nx < p.x1 && this.y <= p.y && ny >= p.y); if (pl) { const keep = this.onSurface(nx, pl.y - 1, true); if (!keep) return false; continue; } }
        this.x = nx; this.y = ny;
      }
      this.ang = Math.atan2(this.vy, this.vx); this.push();
      return this.x > -50 && this.x < WW + 50 && this.y > -150;
    },
    dmg() { return (lance ? t.damage : t.damage) * this.dmgMul * (o.big ? t.bigMul : 1) * (o.mini ? t.shrapDamage / t.damage : 1) * (1 + (kal ? this.nb * t.kalBounceDmg : 0)) * (1 + (lance && hasMod(slot, 'pierce') ? this.pierced * t.pierceDmgUp : 0)); },
    onEnemy(h) {
      const e = h.e; this.hit.add(e);
      damage(e, { dmg: this.dmg(), kx: Math.sign(this.vx) * t.knockback * (o.big ? 2.5 : 1), slot, elem: el, freeze: el === 'ice' ? t.freeze : 0, burn: el === 'fire' ? t.burn : 0, heavy: !!o.big, hitstop: o.big ? 0.05 : 0, shake: o.big ? 5 : 0 });
      const P = this.pal, ang = this.ang, hx = h.x, hy = h.y, sc = o.big ? 0.8 : o.mini ? 0.3 : 0.45;
      addFx(0.9, (ctx, layer, t0) => impact(ctx, G.t, layer, t0, hx, hy, sc, P, { dir: ang + Math.PI, spread: 2.2, seed: Math.floor(hx), needles: 6, drops: 4 }));
      if (this.pierce > 0) { this.pierce--; this.pierced++; sfx(SND.fan_hit.fn); return true; }       // Lanza: atraviesa
      if (this.bounces > 0) {                                                                        // Caleidoscopio
        this.bounces--; this.nb++; this.x = h.x; this.y = h.y;
        if (hasMod(slot, 'eternal')) this.age = 0;
        const sp = Math.hypot(this.vx, this.vy), nxt = nearestEnemy(h.x, h.y, t.kalSeekRange, this.hit);
        if (nxt) { const a = Math.atan2(nxt.y - h.y, nxt.x - h.x); this.vx = Math.cos(a) * sp; this.vy = Math.sin(a) * sp; }
        else { this.vx = -this.vx * 0.7; this.vy = -Math.abs(this.vy) - sp * 0.4; }
        if (hasMod(slot, 'split') && !o.split) {                                                     // Prisma: una copia hacia el siguiente
          const nx2 = nearestEnemy(h.x, h.y, t.kalSeekRange, new Set([...this.hit, nxt].filter(Boolean)));
          const a2 = nx2 ? Math.atan2(nx2.y - h.y, nx2.x - h.x) : Math.atan2(this.vy, this.vx) + 0.6;
          const c = fanShard(slot, h.x, h.y, Math.cos(a2) * sp, Math.sin(a2) * sp, this.pal, { split: true, bounces: Math.max(0, this.bounces - 1), dmgMul: this.dmgMul * t.splitMul });
          c.hit = new Set(this.hit); c.nb = this.nb; G.ents.push(c);
        }
        sfx(SND.fan_ricochet.fn); return true;
      }
      if (spike) { this.stick(e, h.x, h.y); sfx(el === 'ice' ? SND.fan_stick.fn : SND.fan_hit.fn); return true; }
      sfx(SND.fan_hit.fn); return false;
    },
    onSurface(nx, gy, platform) {
      const P = this.pal, gx = nx;
      if (this.bounces > 0) {
        this.bounces--; this.nb++; this.x = nx; this.y = gy - 1;
        if (hasMod(slot, 'eternal')) this.age = 0;
        const sp = Math.hypot(this.vx, this.vy), nxt = hasMod(slot, 'seek') ? nearestEnemy(nx, gy, t.kalSeekRange, this.hit) : null;
        if (nxt) { const a = Math.atan2(nxt.y - gy, nxt.x - nx); this.vx = Math.cos(a) * sp; this.vy = Math.sin(a) * sp; }
        else { this.vy = -Math.abs(this.vy) * 0.8; this.vx *= 0.92; }
        sfx(SND.fan_ricochet.fn);
        addFx(0.5, (ctx, layer, t0) => sparks(ctx, G.t, t0, gx, gy, layer, { n: 8, a0: -Math.PI * 0.85, a1: -Math.PI * 0.15, sp: 230, pal: P, seed: Math.floor(gx), len: 10, life: 0.35 }));
        return true;
      }
      if (platform) { this.x = nx; this.y = gy + 2; return true; }   // sin rebotes atraviesa la plataforma
      addFx(0.7, (ctx, layer, t0) => { sparks(ctx, G.t, t0, gx, gy, layer, { n: 8, a0: -Math.PI * 0.85, a1: -Math.PI * 0.15, sp: 230, pal: P, seed: Math.floor(gx), len: 10, life: 0.35 }); dust(ctx, G.t, t0, gx, layer, { n: 4, reach: 22, sc: 0.6, seed: Math.floor(gx) }); });
      sfx(SND.fan_ground.fn);
      if (spike) { this.stick(null, nx, gy); return true; }
      return false;
    },
    stick(e, hx, hy) { this.state = 'stick'; this.stickE = e; this.x = hx; this.y = hy; if (e) { this.ox = hx - e.x; this.oy = hy - e.y; } this.stickAt = G.t; this.bleedT = 0.5; },
    stuck(dt) {
      const e = this.stickE;
      if (e) { if (e.dead) return this.detonate(); this.x = e.x + this.ox; this.y = e.y + this.oy; }
      if (e && hasMod(slot, 'bleed')) { this.bleedT -= dt; if (this.bleedT <= 0) { this.bleedT = 0.5; damage(e, { dmg: t.bleedDps * 0.5, slot, silentFlash: true, dot: true, color: '#ff6a8a' }); } }
      if (G.lastSwap > this.stickAt || G.t - this.stickAt >= t.spikeLife) return this.detonate();
      return true;
    },
    detonate() {                                            // Erizo: estallido (golpe fuerte → rompe congelados)
      const P = this.pal, hx = this.x, hy = this.y;
      damageArea(hx, hy, t.spikeBlastRadius, { dmg: t.spikeBlast, kx: 60, slot, heavy: true, elem: el, freeze: el === 'ice' ? t.freeze * 0.5 : 0, burn: el === 'fire' ? t.burn : 0 }, false);
      addFx(0.7, (ctx, layer, t0) => { impact(ctx, G.t, layer, t0, hx, hy, 0.5, P, { spread: TAU, seed: Math.floor(hx * 3), needles: 8, drops: 3 }); sparks(ctx, G.t, t0, hx, hy, layer, { n: 6, pal: P, sp: 200, seed: Math.floor(hy), len: 10, life: 0.3 }); });
      if (!G._detSfx || G.t - G._detSfx > 0.06) { G._detSfx = G.t; sfx(k => SFXLIB.hitEnergy(k, 0.6)); }
      if (hasMod(slot, 'shrap')) for (let i = 0; i < t.shrapCount; i++) { const a = Math.random() * TAU; G.ents.push(fanShard(slot, hx, hy, Math.cos(a) * 700, Math.sin(a) * 700, P, { mini: true })); }
      if (hasMod(slot, 'ret')) return this.startBack();
      return false;
    },
    startBack() { this.state = 'return'; this.bx = this.x; this.by = this.y; this.bt = 0; this.hit = new Set(); sfx(SND.fan_rip.fn); return true; },
    back(dt) {                                              // vuelve en curva al cristal, acelerando y golpeando lo que atraviesa
      this.bt += dt; const m = muzzle(), u = easeIn(clamp(this.bt / 0.38));
      const mx = (this.bx + m.x) / 2, my = Math.min(this.by, m.y) - 50;
      const x = (1 - u) * (1 - u) * this.bx + 2 * (1 - u) * u * mx + u * u * m.x, y = (1 - u) * (1 - u) * this.by + 2 * (1 - u) * u * my + u * u * m.y;
      const h = raycastEnemy(this.x, this.y, x, y, this.hit);
      if (h) { this.hit.add(h.e); damage(h.e, { dmg: t.returnDamage, kx: Math.sign(G.player.x - h.e.x) * 80, slot, elem: el, freeze: el === 'ice' ? t.freeze * 0.5 : 0 }); }
      this.ang = Math.atan2(y - this.y, x - this.x); this.x = x; this.y = y; this.push();
      if (u >= 1) {
        for (const so of otherSlots(slot)) if (so.inst && so.inst.refund) so.inst.refund(t.refund);
        const P = this.pal; addFx(0.25, (ctx, layer, t0) => { const m2 = muzzle(), k = seg(G.t, t0, t0 + 0.2); if (layer === 'front') { glow(ctx, m2.x, m2.y, 30, P.g2, 0.7 * (1 - k)); star4(ctx, m2.x, m2.y, 14, 0.8, P.core, 1 - k, layer); } });
        if (!G._absorbT || G.t - G._absorbT > 0.15) { G._absorbT = G.t; sfx(SND.fan_absorb.fn); }
        return false;
      }
      return true;
    },
    draw(ctx, layer) {
      const P = this.pal;
      if (this.captured) {                                  // orbitando una singularidad: la mitad trasera de la órbita va detrás
        if (this.depth < 0) { if (layer === 'back') { lens(ctx, this.x, this.y, this.ang, 22, 4, P.mid, 0.8, layer); lens(ctx, this.x, this.y, this.ang, 14, 2, P.core, 0.9, layer); } }
        else { crystalShard(ctx, this.x, this.y, this.ang, size, P, 1, layer); if (layer === 'front') star4(ctx, this.x, this.y, 8, G.t * 9, '#ffffff', 0.9, layer); }
        return;
      }
      if (layer === 'back' && this.trail.length > 1 && this.state !== 'stick') {
        const pts = [{ x: this.x, y: this.y }, ...this.trail.slice().reverse()], k = size;
        ribbon(ctx, pts, q => 9 * k * Math.pow(1 - q, 1.2), P.dark, 0.35, layer); ribbon(ctx, pts, q => 5 * k * Math.pow(1 - q, 1.3), P.mid, 0.8, layer); ribbon(ctx, pts, q => 2 * k * (1 - q), P.core, 1, layer);
      }
      if (this.state === 'stick') {
        const left = t.spikeLife - (G.t - this.stickAt), wig = left < 0.4 ? Math.sin(G.t * 90) * (1 - left / 0.4) * 2 : 0;
        crystalShard(ctx, this.x - Math.cos(this.ang) * 8 + wig, this.y - Math.sin(this.ang) * 8, this.ang, 0.85, P, 1, layer);
        if (layer === 'front') star4(ctx, this.x - Math.cos(this.ang) * 4, this.y - Math.sin(this.ang) * 4, 5 + 3 * Math.sin(G.t * 20 + this.x), 0.4, P.core, 0.8, layer);
      } else crystalShard(ctx, this.x, this.y, this.ang, size, P, 1, layer);
    },
  };
  return s;
}
