// g-abanico.js — 1 · Abanico de cristal, versión jugable. Ficha y decisiones: docs/armas/abanico.md
'use strict';
addWeapon({
  id: 'fan', name: 'Abanico de cristal', short: 'Abanico', pal: 'set',
  mods: [
    { id: 'ice', name: 'Hielo', desc: 'Las esquirlas se clavan y congelan; congelado + 0,5 s = añicos.' },
    { id: 'bounce', name: 'Rebote', desc: 'Cada esquirla rebota una vez (suelo o enemigo → hacia el siguiente).' },
    { id: 'ret', name: 'Retorno', desc: 'Se clavan y vuelven al cristal golpeando otra vez; cada una recarga el arma de reserva.' },
  ],
  // [valor, mín, máx, paso, etiqueta] — nombres = futuros campos en Unity
  tuning: {
    cooldown: [0.45, 0.1, 2, 0.01, 'Recarga (s)'], count: [5, 1, 12, 1, 'Esquirlas'], spread: [0.2, 0, 0.8, 0.01, 'Apertura (rad, media)'],
    speed: [950, 300, 2000, 10, 'Velocidad (px/s)'], damage: [9, 1, 60, 1, 'Daño por esquirla'], knockback: [60, 0, 400, 5, 'Empuje'],
    life: [0.6, 0.1, 2, 0.05, 'Alcance (s de vuelo)'], freeze: [0.34, 0, 1, 0.01, 'Hielo: congelación por esquirla'],
    shatterDelay: [0.5, 0, 2, 0.05, 'Hielo: espera hasta añicos (s)'], stickTime: [0.35, 0, 1.5, 0.05, 'Retorno: tiempo clavada (s)'],
    returnDamage: [7, 0, 60, 1, 'Retorno: daño al volver'], refund: [0.15, 0, 1, 0.01, 'Retorno: recarga del arma de reserva por esquirla (s)'],
    bounceKeep: [0.85, 0.3, 1.2, 0.01, 'Rebote: velocidad que conserva'],
  },
  create(slot) {
    const id = 'fan';
    let cd = 0;
    const fire = aim => {
      const t = T(id), pal = PAL[hasMod(slot, 'ice') ? 'ice' : hasMod(slot, 'bounce') ? 'gold' : 'set'];
      cd = t.cooldown; G.player.castT = 0.12;
      for (let i = 0; i < t.count; i++) {
        const u = t.count === 1 ? 0.5 : i / (t.count - 1), a = aim.ang + lerp(-t.spread, t.spread, u) + (Math.random() - 0.5) * 0.02, sp = t.speed * (0.95 + Math.random() * 0.1);
        G.ents.push(fanShard(slot, aim.x, aim.y, Math.cos(a) * sp, Math.sin(a) * sp, pal));
      }
      addFx(0.25, (ctx, layer, t0) => castFx(ctx, G.t, layer, t0 - 0.01, t0, pal, 0.8, { x: aim.x, y: aim.y, dir: aim.ang }));
      sfx(SND.fan_release.fn);
    };
    return {
      onPress(aim) { if (cd <= 0) fire(aim); },
      onHold(aim) { if (cd <= 0) fire(aim); },
      update(dt) { cd -= dt; },
      ready() { return 1 - cd / T(id).cooldown; },
      refund(s) { cd -= s; },
    };
  },
});

// Esquirla: vuela, choca (enemigo / suelo) y según los mods rebota, se clava, congela o vuelve.
function fanShard(slot, x, y, vx, vy, pal) {
  const t = T('fan'), ice = hasMod(slot, 'ice'), ret = hasMod(slot, 'ret');
  const s = {
    kind: 'shard', capturable: true, slot, x, y, vx, vy, pal, state: 'fly', age: 0, bounces: hasMod(slot, 'bounce') ? 1 : 0, hit: new Set(), trail: [],
    stickE: null, ox: 0, oy: 0, stickT: 0, frozenSince: -1, ang: Math.atan2(vy, vx),
    update(dt) {
      this.age += dt;
      if (this.captured) { this.trail.push({ x: this.x, y: this.y }); if (this.trail.length > 8) this.trail.shift(); return true; }
      if (this.state === 'fly') return this.fly(dt);
      if (this.state === 'stick') return this.stuck(dt);
      if (this.state === 'return') return this.back(dt);
      return false;
    },
    fly(dt) {
      if (this.age > t.life && !this.ejected) return false;
      if (this.ejected && this.age > t.life + 0.6) return false;
      const n = 3;
      for (let k = 0; k < n; k++) {
        const nx = this.x + this.vx * dt / n, ny = this.y + this.vy * dt / n;
        const h = raycastEnemy(this.x, this.y, nx, ny, this.hit);
        if (h) { if (!this.onEnemy(h)) return false; if (this.state !== 'fly') return true; continue; }
        if (ny >= GROUND - 2) { if (!this.onGround(nx)) return false; if (this.state !== 'fly') return true; continue; }
        this.x = nx; this.y = ny;
      }
      this.ang = Math.atan2(this.vy, this.vx);
      this.trail.push({ x: this.x, y: this.y }); if (this.trail.length > 8) this.trail.shift();
      return this.x > -50 && this.x < WW + 50 && this.y > -100;
    },
    onEnemy(h) {
      const e = h.e; this.hit.add(e); this.x = h.x; this.y = h.y;
      damage(e, { dmg: t.damage, kx: Math.sign(this.vx) * t.knockback, slot, elem: ice ? 'ice' : null, freeze: ice ? t.freeze : 0 });
      const P = this.pal, ang = this.ang, hx = h.x, hy = h.y;
      addFx(0.9, (ctx, layer, t0) => impact(ctx, G.t, layer, t0, hx, hy, 0.45, P, { dir: ang + Math.PI, spread: 2.2, seed: Math.floor(hx), needles: 6, drops: 4 }));
      if (this.bounces > 0) {
        this.bounces--;
        const nxt = nearestEnemy(h.x, h.y, 420, this.hit), sp = Math.hypot(this.vx, this.vy) * t.bounceKeep;
        if (nxt) { const a = Math.atan2(nxt.y - h.y, nxt.x - h.x); this.vx = Math.cos(a) * sp; this.vy = Math.sin(a) * sp; }
        else { this.vx = -this.vx * 0.5; this.vy = -Math.abs(this.vy) - sp * 0.5; }
        this.age = Math.min(this.age, t.life * 0.4);
        sfx(SND.fan_ricochet.fn); return true;
      }
      if (ice || ret) { this.state = 'stick'; this.stickE = e; this.ox = h.x - e.x; this.oy = h.y - e.y; this.stickT = 0; sfx(ice ? SND.fan_stick.fn : SND.fan_hit.fn); return true; }
      sfx(SND.fan_hit.fn); return false;
    },
    onGround(nx) {
      const gx = nx, P = this.pal;
      if (this.bounces > 0) { this.bounces--; this.vy = -Math.abs(this.vy) * 0.75; this.vx *= 0.9; this.y = GROUND - 3; this.x = nx; sfx(SND.fan_ricochet.fn);
        addFx(0.5, (ctx, layer, t0) => sparks(ctx, G.t, t0, gx, GROUND - 3, layer, { n: 8, a0: -Math.PI * 0.85, a1: -Math.PI * 0.15, sp: 230, pal: P, seed: Math.floor(gx), len: 10, life: 0.35 })); return true; }
      addFx(0.7, (ctx, layer, t0) => { sparks(ctx, G.t, t0, gx, GROUND - 3, layer, { n: 8, a0: -Math.PI * 0.85, a1: -Math.PI * 0.15, sp: 230, pal: P, seed: Math.floor(gx), len: 10, life: 0.35 }); dust(ctx, G.t, t0, gx, layer, { n: 4, reach: 22, sc: 0.6, seed: Math.floor(gx) }); });
      sfx(SND.fan_ground.fn);
      if (ret) { this.state = 'stick'; this.stickE = null; this.x = nx; this.y = GROUND - 3; this.stickT = 0; return true; }
      return false;
    },
    stuck(dt) {
      this.stickT += dt;
      const e = this.stickE;
      if (e) { if (e.dead) return ret ? this.startBack() : false; this.x = e.x + this.ox; this.y = e.y + this.oy; }
      if (ice && e) {                                   // congelado con esquirlas clavadas → añicos tras la espera
        if (e.frozenT > 0 && this.frozenSince < 0) this.frozenSince = G.t;
        if (this.frozenSince >= 0 && G.t - this.frozenSince >= t.shatterDelay && e.frozenT > 0) { damage(e, { dmg: 0, heavy: true, slot }); }
        if (this.frozenSince >= 0 && e.frozenT <= 0) return ret ? this.startBack() : false;
      }
      if (ret && this.stickT >= t.stickTime && !(ice && e && e.freeze > 0 && e.frozenT <= 0 && this.stickT < 1.2)) return this.startBack();
      return this.stickT < (ice ? 2.5 : 1.2);
    },
    startBack() {
      if (!ret) return false;
      this.state = 'return'; this.bx = this.x; this.by = this.y; this.bt = 0;
      if (this.stickE && !this.stickE.dead) { damage(this.stickE, { dmg: t.returnDamage, kx: Math.sign(G.player.x - this.stickE.x) * 80, slot }); const hx = this.x, hy = this.y, P = this.pal;
        addFx(0.4, (ctx, layer, t0) => sparks(ctx, G.t, t0, hx, hy, layer, { n: 6, a0: Math.PI * 0.7, a1: Math.PI * 1.3, sp: 200, pal: P, seed: Math.floor(hx), len: 12, life: 0.3 })); }
      sfx(SND.fan_rip.fn); return true;
    },
    back(dt) {                                          // vuelve en curva hacia el cristal, acelerando
      this.bt += dt; const m = muzzle(), u = easeIn(clamp(this.bt / 0.38));
      const mx = (this.bx + m.x) / 2, my = Math.min(this.by, m.y) - 50;
      const x = (1 - u) * (1 - u) * this.bx + 2 * (1 - u) * u * mx + u * u * m.x, y = (1 - u) * (1 - u) * this.by + 2 * (1 - u) * u * my + u * u * m.y;
      this.ang = Math.atan2(y - this.y, x - this.x); this.x = x; this.y = y;
      this.trail.push({ x, y }); if (this.trail.length > 8) this.trail.shift();
      if (u >= 1) {
        for (const o of otherSlots(slot)) if (o.inst && o.inst.refund) o.inst.refund(t.refund);
        const P = this.pal; addFx(0.25, (ctx, layer, t0) => { const m2 = muzzle(), k = seg(G.t, t0, t0 + 0.2); if (layer === 'front') { glow(ctx, m2.x, m2.y, 30, P.g2, 0.7 * (1 - k)); star4(ctx, m2.x, m2.y, 14, 0.8, P.core, 1 - k, layer); } });
        if (!G._absorbT || G.t - G._absorbT > 0.15) { G._absorbT = G.t; sfx(SND.fan_absorb.fn); }
        return false;
      }
      return true;
    },
    draw(ctx, layer) {
      const P = this.pal;
      if (this.captured) {                              // orbitando una singularidad: la mitad trasera de la órbita va detrás
        if (this.depth < 0) { if (layer === 'back') { lens(ctx, this.x, this.y, this.ang, 22, 4, P.mid, 0.8, layer); lens(ctx, this.x, this.y, this.ang, 14, 2, P.core, 0.9, layer); } }
        else { crystalShard(ctx, this.x, this.y, this.ang, 1, P, 1, layer); if (layer === 'front') star4(ctx, this.x, this.y, 8, G.t * 9, '#ffffff', 0.9, layer); }
        return;
      }
      if (layer === 'back' && this.trail.length > 1 && this.state !== 'stick') {
        const pts = [{ x: this.x, y: this.y }, ...this.trail.slice().reverse()];
        ribbon(ctx, pts, q => 9 * Math.pow(1 - q, 1.2), P.dark, 0.35, layer); ribbon(ctx, pts, q => 5 * Math.pow(1 - q, 1.3), P.mid, 0.8, layer); ribbon(ctx, pts, q => 2 * (1 - q), P.core, 1, layer);
      }
      if (this.state === 'stick') {
        const wig = ret ? Math.sin(G.t * 90) * seg(this.stickT, t.stickTime - 0.15, t.stickTime) * 2 : 0;
        crystalShard(ctx, this.x - Math.cos(this.ang) * 8 + wig, this.y - Math.sin(this.ang) * 8, this.ang, 0.85, P, 1, layer);
        if (ice && layer === 'front') star4(ctx, this.x - Math.cos(this.ang) * 4, this.y - Math.sin(this.ang) * 4, 6 + 3 * Math.sin(G.t * 20 + this.x), 0.4, PAL.ice.core, 0.8, layer);
      } else crystalShard(ctx, this.x, this.y, this.ang, 1, P, 1, layer);
    },
  };
  return s;
}
