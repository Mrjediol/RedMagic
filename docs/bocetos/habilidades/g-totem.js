// g-totem.js — 12 · Tótem de cristal, versión jugable. Ficha y decisiones: docs/armas/totem.md · progresión: docs/armas/progresion.md
// Clic = lanzas un cristal que se planta en el suelo (o plataforma) bajo donde apuntas y dispara solo al enemigo más cercano
// durante un rato: el arma trabaja mientras llevas la otra en la mano. Árbol: base → Resonador / Centinela / Señuelo → 3 mejoras.
// Elemento y Duplicador vienen de items (Duplicador: cada disparo sale doble).
'use strict';
const SND_TOTEM = {
  plant: sound('totem_plant', 'Tótem', 'Se planta', k => { const t = nowT(); vThump(t, 120, 50, 0.25 * k, 0.6, 2, 0.2); vRing(t, [659, 988, 1318], 0.7 * k, 0.05, 0.35); }),
  shot: sound('totem_shot', 'Tótem', 'Disparo', k => { const t = nowT(); vTone(t, 1760, 880, 0.08 * k, 0.05, 'triangle', 0.15); vNoise(t, 0.05 * k, 0.12, 'highpass', 3000, 6000, 1, { att: 0.001 }); }),
  reson: sound('totem_reson', 'Tótem', 'Resonador · copia tu golpe', k => { const t = nowT(); vRing(t, [523, 784, 1046], 0.3 * k, 0.045, 0.4); }),
  chord: sound('totem_chord', 'Tótem', 'Resonador · Acorde', k => { const t = nowT(); vRing(t, [262, 330, 392, 523], 1.1 * k, 0.07, 0.5); SFXLIB.boom(k, 0.4); }),
  taunt: sound('totem_taunt', 'Tótem', 'Señuelo · llama', k => { const t = nowT(); vTone(t, 440, 440, 0.25 * k, 0.05, 'square', 0.3); vTone(t + 0.12 * k, 554, 554, 0.25 * k, 0.04, 'square', 0.3); }),
  boom: sound('totem_boom', 'Tótem', 'Señuelo · estalla', k => { SFXLIB.boom(k, 1); SFXLIB.shatter(k, 0.8); }),
};
addWeapon({
  id: 'totem', name: 'Tótem de cristal', short: 'Tótem', pal: 'set',  fresh: true,   // arma nueva (2026-10-20), sin probar por el usuario
  archetypes: [
    { id: 'reson', name: 'Resonador', desc: 'El tótem COPIA tus golpes: cada golpe de tu otra arma a un enemigo cerca del tótem le devuelve una onda con parte de ese daño.',
      ups: [{ id: 'amp', name: 'Amplificador', desc: 'La copia es más fuerte y llega más lejos.' },
            { id: 'choir', name: 'Coro', desc: 'Puedes tener dos tótems a la vez; los dos copian.' },
            { id: 'chord', name: 'Acorde', desc: 'Cada pocas copias, el tótem suelta una onda de choque alrededor.' }] },
    { id: 'sentry', name: 'Centinela', desc: 'Torreta de verdad: dispara el doble de rápido, sus cristales atraviesan a uno y prefiere a los marcados y a los congelados.',
      ups: [{ id: 'triple', name: 'Triple', desc: 'Cada disparo son tres cristales en abanico.' },
            { id: 'hawk', name: 'Ojo de halcón', desc: 'Mucho más alcance, y sus disparos a marcados o congelados son golpes fuertes.' },
            { id: 'aegis', name: 'Égida', desc: 'Derriba las balas enemigas que pasan cerca.' }] },
    { id: 'decoy', name: 'Señuelo', desc: 'El tótem PROVOCA: atrae a los enemigos cercanos y sus disparos van a por él. Tiene vida; al romperse (o acabarse) estalla.',
      ups: [{ id: 'thorns', name: 'Espinas', desc: 'Los que lo tocan reciben daño continuo y van lentos.' },
            { id: 'magnet', name: 'Imán', desc: 'Atrae con mucha más fuerza y desde más lejos: los amontona.' },
            { id: 'rebirth', name: 'Renacer', desc: 'Al estallar, deja un tótem más pequeño en su sitio (una vez).' }] },
  ],
  tuning: {
    cooldown: [5, 0.5, 15, 0.1, 'Base: recarga (s)'], range: [420, 80, 800, 10, 'Base: distancia máxima para plantarlo'], totemTime: [9, 1, 25, 0.5, 'Base: duración (s)'],
    fireGap: [0.7, 0.1, 3, 0.05, 'Base: s entre disparos'], boltDamage: [9, 1, 60, 1, 'Base: daño por cristal'], boltSpeed: [650, 200, 1500, 10, 'Base: velocidad del cristal'],
    totemRange: [380, 80, 800, 10, 'Base: alcance del tótem'], iceFreeze: [0.2, 0, 1, 0.05, 'Hielo: congelación por cristal'], fireBurn: [1.2, 0, 5, 0.1, 'Fuego: quemadura por cristal (s)'],
    resonRadius: [320, 80, 700, 10, 'Resonador: radio'], resonMul: [0.4, 0, 2, 0.05, 'Resonador: × daño del golpe copiado'], ampMul: [0.7, 0, 2, 0.05, 'Resonador · Amplificador: × daño (en vez de resonMul)'],
    ampRadius: [1.5, 1, 3, 0.05, 'Resonador · Amplificador: × radio'], chordEvery: [6, 2, 20, 1, 'Resonador · Acorde: cada N copias'], chordDamage: [30, 0, 150, 1, 'Resonador · Acorde: daño'],
    chordRadius: [140, 30, 350, 5, 'Resonador · Acorde: radio'],
    sentryGap: [0.35, 0.05, 2, 0.05, 'Centinela: s entre disparos'], tripleSpread: [0.18, 0.02, 0.6, 0.01, 'Centinela · Triple: abertura (rad)'], hawkRange: [1.6, 1, 3, 0.05, 'Centinela · Ojo de halcón: × alcance'],
    aegisRadius: [160, 30, 400, 5, 'Centinela · Égida: radio'],
    decoyRadius: [300, 50, 700, 10, 'Señuelo: radio de provocación'], decoyPull: [60, 0, 300, 5, 'Señuelo: atracción'], decoyHp: [60, 5, 400, 5, 'Señuelo: vida'],
    decoyBoom: [40, 0, 200, 1, 'Señuelo: daño al estallar'], decoyBoomRadius: [120, 30, 350, 5, 'Señuelo: radio del estallido'], thornsDps: [15, 0, 80, 1, 'Señuelo · Espinas: daño/s'],
    magnetPull: [140, 0, 400, 5, 'Señuelo · Imán: atracción'], magnetRadius: [1.5, 1, 3, 0.05, 'Señuelo · Imán: × radio'], rebirthScale: [0.6, 0.2, 1, 0.05, 'Señuelo · Renacer: tamaño, vida y duración (×)'],
  },
  create(slot) {
    const id = 'totem';
    let cd = 0, totems = [];
    const inst = {
      totems: () => totems,
      onPress(aim) {
        if (cd > 0) return;
        const t = T(id); cd = t.cooldown * cdMul(); G.player.castT = 0.3;
        const p = G.player, x = clamp(aim.tx, p.x - t.range, p.x + t.range), gy = surfaceBelow(x, aim.ty);
        G.ents.push(totemThrow(slot, inst, aim.x, aim.y, x, gy, tm => {
          totems.push(tm); G.ents.push(tm);
          const max = slot.arch === 'reson' && hasMod(slot, 'choir') ? 2 : 1;
          while (totems.filter(o => o.alive).length > max) totems.find(o => o.alive).end(false);
        }));
      },
      update(dt) { cd -= dt; totems = totems.filter(o => o.alive); },
      // Resonador: copia los golpes de tu otra arma cerca del tótem
      onAnyHit(e, o, dmg) {
        if (slot.arch !== 'reson' || o.slot === slot) return;
        for (const tm of totems) if (tm.alive) tm.resonate(e, dmg);
      },
      ready() { return 1 - cd / Math.max(0.01, T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
    return inst;
  },
});

// superficie (suelo o plataforma) justo debajo de (x, y)
function surfaceBelow(x, y) {
  let best = GROUND;
  for (const pl of PLATFORMS) if (x > pl.x0 && x < pl.x1 && pl.y >= y - 10 && pl.y < best) best = pl.y;
  return best;
}
// el cristal viaja en arco hasta su sitio y se planta
function totemThrow(slot, owner, x0, y0, x1, gy, onPlant) {
  const t0 = G.t, D = 0.32, pal = elemPal('set');
  let x = x0, y = y0;
  return { kind: 'fx', update() {
      const u = seg(G.t, t0, t0 + D); x = lerp(x0, x1, u); y = lerp(y0, gy - 30, u) - Math.sin(Math.PI * u) * 70;
      if (u >= 1) { onPlant(crystalTotemEnt(slot, owner, x1, gy, 1)); return false; }
      return true; },
    draw(ctx, layer) { crystalShard(ctx, x, y, G.t * 12, 1.2, pal, 1, layer); } };
}

function crystalTotemEnt(slot, owner, x, gy, sc) {
  const t = T('totem'), arch = slot.arch, el = elemOf(), mode = arch || 'base', t0 = G.t, life = t.totemTime * (sc < 1 ? t.rebirthScale : 1);
  const pal = elemPal(mode === 'decoy' ? 'life' : mode === 'reson' ? 'gold' : 'set');
  let fireT = 0.3, chargeK = 0, resonN = 0, thornT = 0, hp = t.decoyHp * sc, aimA = -Math.PI / 2;
  const cy = gy - 40 * sc;
  sfx(SND_TOTEM.plant.fn);
  addFx(1, (ctx, layer, f0) => groundImpact(ctx, G.t, layer, f0, x, 0.55 * sc, pal, { seed: Math.floor(x), debris: false }));
  const tm = {
    kind: 'totem', alive: true, x, y: cy, r: 0,
    top() { return { x, y: gy - 64 * sc * 0.95 }; },
    end(boom = true) {
      if (!this.alive) return; this.alive = false;
      if (mode === 'decoy') {
        G.decoys = (G.decoys || []).filter(d => d !== this);
        if (boom) {
          damageArea(x, cy, t.decoyBoomRadius * sc, { dmg: t.decoyBoom * sc, slot, heavy: true, kx: 260, ky: -200, shake: 7, hitstop: 0.05, frame: true, elem: el, freeze: el === 'ice' ? 1 : 0, burn: el === 'fire' ? 2.5 : 0 });
          addFx(1.1, (ctx, layer, f0) => { impact(ctx, G.t, layer, f0, x, cy, 1.2 * sc, pal, { spread: TAU, seed: 21 }); shockRing(ctx, G.t, layer, f0, x, cy, t.decoyBoomRadius * sc, pal); });
          sfx(SND_TOTEM.boom.fn);
          if (hasMod(slot, 'rebirth') && sc >= 1) { const nt = crystalTotemEnt(slot, owner, x, gy, t.rebirthScale); owner.totems().push(nt); G.ents.push(nt); }
        }
      }
      const P = pal; addFx(0.6, (ctx, layer, f0) => { const u = seg(G.t, f0, f0 + 0.5); for (let i = 0; i < 8; i++) { const a = i / 8 * TAU, d = 50 * easeOut(u); chunk(ctx, x + Math.cos(a) * d, cy + Math.sin(a) * d * 0.6 + 80 * u * u, 4 * sc, u * 6 + i, i, P.mid, P.core, 1 - u, layer); } });
    },
    hurt(d) { if (!this.alive) return; hp -= d; text(x, cy - 50, '-' + d, '#ffb3c4'); if (hp <= 0) this.end(true); },
    // Resonador: onda al enemigo golpeado con parte del daño
    resonate(e, dmg) {
      const amp = hasMod(slot, 'amp'), R = t.resonRadius * (amp ? t.ampRadius : 1);
      if (e.dead || Math.hypot(e.x - x, e.y - cy) > R) return;
      const tp = this.top();
      damage(e, { dmg: dmg * (amp ? t.ampMul : t.resonMul), raw: true, proc: true, slot, silentFlash: true, color: '#ffe9a0' });
      addFx(0.3, (ctx, layer, f0) => resonWave(ctx, G.t, layer, f0, tp.x, tp.y, e.x, e.y, pal));
      chargeK = 1;
      if (!G._resSfx || G.t - G._resSfx > 0.1) { G._resSfx = G.t; sfx(SND_TOTEM.reson.fn); }
      if (hasMod(slot, 'chord') && ++resonN >= t.chordEvery) {
        resonN = 0;
        damageArea(x, cy, t.chordRadius, { dmg: t.chordDamage, slot, proc: true, kx: 200, shake: 4, elem: el, freeze: el === 'ice' ? 0.4 : 0, burn: el === 'fire' ? 2 : 0 });
        addFx(0.8, (ctx, layer, f0) => { shockRing(ctx, G.t, layer, f0, x, cy, t.chordRadius, pal); });
        sfx(SND_TOTEM.chord.fn);
      }
    },
    pickTarget(range) {
      let list = alive().filter(e => Math.hypot(e.x - x, e.y - cy) < range);
      if (!list.length) return null;
      if (mode === 'sentry') { const pri = list.filter(e => e.markT > 0 || e.frozenT > 0 || e.freeze > 0.3); if (pri.length) list = pri; }
      return list.sort((a, b) => Math.hypot(a.x - x, a.y - cy) - Math.hypot(b.x - x, b.y - cy))[0];
    },
    update(dt) {
      if (!this.alive) return false;
      const age = G.t - t0;
      if (age > life) { this.end(true); return false; }
      chargeK = Math.max(0, chargeK - dt * 4);
      // disparo propio
      const sentry = mode === 'sentry', range = t.totemRange * (sentry && hasMod(slot, 'hawk') ? t.hawkRange : 1);
      fireT -= dt;
      if (fireT < 0.15) chargeK = Math.max(chargeK, 1 - fireT / 0.15);
      if (fireT <= 0) {
        const tg = this.pickTarget(range);
        fireT = tg ? (sentry ? t.sentryGap : t.fireGap) : 0.2;
        if (tg) {
          const tp = this.top(), a = Math.atan2(tg.y - tp.y, tg.x - tp.x); aimA = a;
          const fan = sentry && hasMod(slot, 'triple') ? [-t.tripleSpread, 0, t.tripleSpread] : [0], n = copies();
          for (const off of fan) for (let c = 0; c < n; c++) G.ents.push(totemBolt(slot, tp.x, tp.y, a + off + (n > 1 ? (c ? 0.07 : -0.07) : 0), { pierce: sentry ? 1 : 0, hawk: sentry && hasMod(slot, 'hawk') }));
          if (!G._totSfx || G.t - G._totSfx > 0.06) { G._totSfx = G.t; sfx(SND_TOTEM.shot.fn); }
        }
      }
      // Égida: derriba balas enemigas
      if (sentry && hasMod(slot, 'aegis')) for (const s of G.ents) if (s.kind === 'enemyShot' && !s.friendly && !s.captured && s.life > 0 && Math.hypot(s.x - x, s.y - cy) < t.aegisRadius) {
        s.life = 0; const tp = this.top(), sx = s.x, sy = s.y; addFx(0.3, (ctx, layer, f0) => { rodBolt(ctx, G.t, layer, f0, tp.x, tp.y, sx, sy, pal, 1.4); });
      }
      // Señuelo: atrae y provoca (sus balas vienen aquí: G.decoys)
      if (mode === 'decoy') {
        const mag = hasMod(slot, 'magnet'), R = t.decoyRadius * (mag ? t.magnetRadius : 1) * sc;
        this.r = R; G.decoys = G.decoys || []; if (!G.decoys.includes(this)) { G.decoys.push(this); sfx(SND_TOTEM.taunt.fn); }
        for (const e of alive()) { const d = Math.hypot(e.x - x, e.y - cy); if (d < R && d > 34 && !e.stasis) e.pull = e.pull || { x, y: e.fly ? cy - 20 : e.y, s: mag ? t.magnetPull : t.decoyPull, lift: e.fly }; }
        if (hasMod(slot, 'thorns')) { thornT -= dt; if (thornT <= 0) { thornT = 0.25; for (const e of alive()) if (Math.abs(e.x - x) < e.rx + 30 && Math.abs(e.y - cy) < e.ry + 40) { damage(e, { dmg: t.thornsDps * 0.25, slot, silentFlash: true, color: '#ffb3c4', elem: el, burn: el === 'fire' ? 1 : 0, freeze: el === 'ice' ? 0.08 : 0 }); e.timeK = Math.min(e.timeK === undefined ? 1 : e.timeK, 0.5); } } }
      }
      return true;
    },
    draw(ctx, layer) {
      const age = G.t - t0, a = easeOut(seg(age, 0, 0.2)) * (1 - seg(age, life - 0.3, life));
      crystalTotem(ctx, G.t, layer, x, gy, sc, pal, a, chargeK, { mode, life: 1 - age / life, hp: mode === 'decoy' ? hp / (t.decoyHp * sc) : undefined });
      if (mode === 'decoy' && hasMod(slot, 'thorns') && layer === 'front') for (let i = 0; i < 6; i++) { const ang = -Math.PI / 2 + (i - 2.5) * 0.45; needle(ctx, x + Math.cos(ang) * 14 * sc, gy - 20 * sc + Math.sin(ang) * 10, x + Math.cos(ang) * 34 * sc, gy - 20 * sc + Math.sin(ang) * 26, 3, pal.light, 0.8 * a, layer); }
    },
  };
  return tm;
}

// cristal que dispara el tótem: proyectil capturable (Singularidad), lo aceleran el Reloj y la Corriente, lo curva la Luz guía
function totemBolt(slot, x, y, ang, o) {
  const t = T('totem'), el = elemOf(), pal = elemPal('set'), trail = [];
  const b = {
    kind: 'bolt', capturable: true, state: 'fly', slot, x, y, vx: Math.cos(ang) * t.boltSpeed, vy: Math.sin(ang) * t.boltSpeed, age: 0, life: 1.1, dmgMul: 1, hit: new Set(), pierce: o.pierce || 0,
    clone() { const c = totemBolt(slot, this.x, this.y, Math.atan2(this.vy, this.vx), o); c.dmgMul = this.dmgMul; return c; },
    update(dt) {
      this.age += dt;
      if (this.captured) { trail.length = 0; return true; }
      if (this.age > this.life + (this.ejected ? 0.6 : 0)) return false;
      const nx = this.x + this.vx * dt, ny = this.y + this.vy * dt, h = raycastEnemy(this.x, this.y, nx, ny, this.hit);
      if (h) {
        const e = h.e; this.hit.add(e);
        const heavy = o.hawk && (e.markT > 0 || e.frozenT > 0);
        damage(e, { dmg: t.boltDamage * this.dmgMul, slot, kx: Math.sign(this.vx) * 60, heavy, elem: el, freeze: el === 'ice' ? t.iceFreeze : 0, burn: el === 'fire' ? t.fireBurn : 0, hitstop: heavy ? 0.03 : 0 });
        const hx = h.x, hy = h.y, a = Math.atan2(this.vy, this.vx); addFx(0.6, (ctx, layer, f0) => impact(ctx, G.t, layer, f0, hx, hy, 0.35, pal, { dir: a + Math.PI, spread: 2, seed: Math.floor(hx), needles: 5, drops: 3 }));
        if (this.pierce-- <= 0) return false;
      }
      if (ny >= GROUND - 2) return false;
      this.x = nx; this.y = ny;
      trail.unshift({ x: this.x, y: this.y }); if (trail.length > 6) trail.pop();
      return this.x > -50 && this.x < WW + 50 && this.y > -100;
    },
    draw(ctx, layer) {
      if (layer === 'back' && trail.length > 1) ribbon(ctx, trail, s => 4 * (1 - s), pal.mid, 0.7, layer);
      crystalShard(ctx, this.x, this.y, Math.atan2(this.vy, this.vx), 0.55, pal, 1, layer);
    },
  };
  return b;
}
