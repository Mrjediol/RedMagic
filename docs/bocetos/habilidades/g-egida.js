// g-egida.js — 17 · Égida, versión jugable. Ficha y decisiones: docs/armas/egida.md · progresión: docs/armas/progresion.md
// Escudo: MANTÉN para levantarlo hacia el ratón (para balas y aparta a los que se acercan; vas más lento). Si una bala llega
// justo al levantarlo = PARADA: vuelve como tuya y tu siguiente ataque (de cualquier arma) es golpe fuerte. SOLTAR = golpe de
// escudo con un pequeño avance. Para probarla bien: «Los enemigos disparan». Árbol: base → Reflejo / Bastión / Embestida.
'use strict';
const SND_AEGIS = {
  raise: sound('aegis_raise', 'Égida', 'Levantar', k => { const t = nowT(); vRing(t, [880, 1320], 0.25 * k, 0.04, 0.2); vNoise(t, 0.08 * k, 0.1, 'bandpass', 2000, 4000, 1, { att: 0.005 }); }),
  block: sound('aegis_block', 'Égida', 'Bloquea una bala', k => { const t = nowT(); vThump(t, 300, 150, 0.06 * k, 0.35, 1.5); vRing(t, [1760, 2640], 0.2 * k, 0.04, 0.2); }),
  parry: sound('aegis_parry', 'Égida', '¡Parada!', k => { const t = nowT(); vRing(t, [1318, 1976, 2637, 3951], 0.6 * k, 0.07, 0.4); vNoise(t, 0.05 * k, 0.4, 'highpass', 4000, 9000, 1, { att: 0.0005 }); vThump(t, 200, 80, 0.1 * k, 0.5, 2); }),
  bash: sound('aegis_bash', 'Égida', 'Golpe de escudo', k => { const t = nowT(); vThump(t, 150, 50, 0.2 * k, 0.7, 2.5); vNoise(t, 0.1 * k, 0.3, 'bandpass', 1500, 600, 1, { att: 0.002 }); }),
  wall: sound('aegis_wall', 'Égida', 'Bastión · se planta', k => { const t = nowT(); vThump(t, 90, 40, 0.3 * k, 0.6, 2, 0.2); vRing(t, [440, 660, 880], 0.6 * k, 0.05, 0.4); }),
  blast: sound('aegis_blast', 'Égida', 'Bastión · estalla', k => { SFXLIB.shatter(k, 1); SFXLIB.boom(k, 0.7); }),
};
addWeapon({
  id: 'aegis', name: 'Égida', short: 'Égida', pal: 'set',  fresh: true,   // arma nueva (2026-10-20), sin probar por el usuario
  archetypes: [
    { id: 'reflect', name: 'Reflejo', desc: 'TODAS las balas que para salen devueltas hacia el enemigo más cercano (no sólo las de la parada) y la parada es más fácil.',
      ups: [{ id: 'split', name: 'Rebote triple', desc: 'Cada bala devuelta sale triple.' },
            { id: 'riposte', name: 'Contraataque', desc: 'Cada parada recarga del todo tu otra arma.' },
            { id: 'absorb', name: 'Absorción', desc: 'Cada bala parada carga el escudo; el golpe de escudo suelta esa carga en una onda.' }] },
    { id: 'bastion', name: 'Bastión', desc: 'Al soltar, el escudo se queda PLANTADO como un muro: para balas y enemigos mientras usas la otra arma. Pulsar con el muro puesto lo hace estallar.',
      ups: [{ id: 'spikes', name: 'Púas', desc: 'El muro hace mucho daño a los que lo tocan.' },
            { id: 'tall', name: 'Muralla', desc: 'Muro el doble de alto (para voladores) y dura el doble.' },
            { id: 'blast', name: 'Estallido', desc: 'La explosión del muro es mayor y lanza a los enemigos.' }] },
    { id: 'charge', name: 'Embestida', desc: 'Mantén para cargar y suelta: embestida larga con el escudo por delante que arrastra a los enemigos y los estampa al final.',
      ups: [{ id: 'trample', name: 'Arrollar', desc: 'Atraviesas a los enemigos haciéndoles daño en vez de empujarlos.' },
            { id: 'unstoppable', name: 'Imparable', desc: 'Durante la embestida eres intocable y rompes las balas.' },
            { id: 'finisher', name: 'Remate', desc: 'Al terminar, onda de choque que aturde.' }] },
  ],
  tuning: {
    cooldown: [0.8, 0.1, 4, 0.05, 'Base: recarga tras soltar (s)'], shieldR: [46, 20, 120, 1, 'Base: radio del escudo'], arc: [0.95, 0.3, 1.6, 0.05, 'Base: medio ángulo que cubre (rad)'],
    holdMove: [0.6, 0.1, 1, 0.05, 'Base: × tu velocidad con el escudo arriba'], parryWindow: [0.22, 0.05, 0.6, 0.01, 'Base: ventana de parada tras levantarlo (s)'],
    parryDamage: [30, 1, 120, 1, 'Base: daño de la bala devuelta en una parada'], parryMul: [1.6, 1, 4, 0.05, 'Base: × siguiente ataque tras una parada'],
    contactDamage: [6, 0, 40, 1, 'Base: daño al apartar a los que tocan el escudo'], contactPush: [300, 0, 900, 10, 'Base: empuje al apartarlos'],
    bashDist: [140, 40, 400, 5, 'Base: avance del golpe de escudo'], bashDamage: [22, 1, 120, 1, 'Base: daño del golpe de escudo'], bashPush: [420, 0, 1200, 10, 'Base: empuje del golpe (lanzado)'],
    iceFreeze: [0.5, 0, 1, 0.05, 'Hielo: congelación del golpe'], fireBurn: [2, 0, 6, 0.1, 'Fuego: quemadura del golpe (s)'],
    reflectDamage: [18, 1, 80, 1, 'Reflejo: daño de las balas devueltas'], reflectWindow: [2, 1, 4, 0.1, 'Reflejo: × ventana de parada'], absorbPer: [12, 0, 60, 1, 'Reflejo · Absorción: daño de la onda por bala'],
    wallTime: [6, 1, 20, 0.5, 'Bastión: duración del muro (s)'], wallCooldown: [2.5, 0.2, 10, 0.1, 'Bastión: recarga tras plantar el muro (s)'], wallH: [110, 40, 300, 5, 'Bastión: alto del muro'], wallDps: [8, 0, 60, 1, 'Bastión: daño/s a los que lo tocan'],
    spikeMul: [3.5, 1, 8, 0.1, 'Bastión · Púas: × daño'], wallBlast: [50, 0, 200, 1, 'Bastión: daño al estallar'], blastRadius: [120, 30, 300, 5, 'Bastión: radio del estallido'],
    blastMul: [1.6, 1, 3, 0.05, 'Bastión · Estallido: × daño y radio'],
    chargeTime: [0.8, 0.1, 2, 0.05, 'Embestida: carga máxima (s)'], chargeMin: [160, 40, 500, 5, 'Embestida: distancia sin carga'], chargeMax: [420, 40, 900, 10, 'Embestida: distancia con carga'],
    slamMin: [20, 0, 150, 1, 'Embestida: daño final sin carga'], slamMax: [60, 0, 300, 1, 'Embestida: daño final con carga'], trampleDamage: [25, 0, 150, 1, 'Embestida · Arrollar: daño al atravesar'],
    finisherRadius: [130, 30, 300, 5, 'Embestida · Remate: radio'], finisherStun: [1, 0, 3, 0.1, 'Embestida · Remate: aturdido (s)'],
  },
  create(slot) {
    const id = 'aegis';
    let cd = 0, held = null, bash = null, walls = [], stored = 0, contactT = 0, shieldA = 0, glowK = 0;
    const pal = () => elemPal('set');
    const center = () => ({ x: G.player.x + G.player.face * 6, y: G.player.y - 40 });
    const el = () => elemOf();
    const reflectShot = (s, dmg) => {
      const c = center(), n = nearestEnemy(s.x, s.y, 700), a = n ? Math.atan2(n.y - s.y, n.x - s.x) : shieldA, sp = Math.max(400, Math.hypot(s.vx, s.vy) * 2);
      s.vx = Math.cos(a) * sp; s.vy = Math.sin(a) * sp; s.hostile = false; s.friendly = true; s.slot = slot; s.dmg = dmg; s.life = 3; s.x = c.x + Math.cos(shieldA) * (T(id).shieldR + 6); s.y = c.y + Math.sin(shieldA) * (T(id).shieldR + 6);
      if (slot.arch === 'reflect' && hasMod(slot, 'split')) for (const off of [-0.25, 0.25]) { const cp = Object.assign({}, s, { vx: Math.cos(a + off) * sp, vy: Math.sin(a + off) * sp }); G.ents.push(cp); }
    };
    const block = s => {
      const t = T(id), win = t.parryWindow * (slot.arch === 'reflect' ? t.reflectWindow : 1), c = center();
      if (G.t - held.t0 < win) {                                // ¡parada!
        reflectShot(s, t.parryDamage); G.empower = t.parryMul; G.empowerHit = false; G.empowerEnd = 0; glowK = 1;
        if (slot.arch === 'reflect' && hasMod(slot, 'riposte')) for (const so of otherSlots(slot)) if (so.inst && so.inst.refund) so.inst.refund(99);
        text(c.x, c.y - 60, '¡PARADA!', '#9ef2ff', true); if (G.settings.hitstop) G.hitStop = Math.max(G.hitStop, 0.08); G.shake += 4; sfx(SND_AEGIS.parry.fn);
      } else if (slot.arch === 'reflect') { reflectShot(s, t.reflectDamage); sfx(SND_AEGIS.block.fn); }
      else { s.life = 0; sfx(SND_AEGIS.block.fn); }
      if (slot.arch === 'reflect' && hasMod(slot, 'absorb')) stored++;
      const P = pal(), bx = s.x, by = s.y; addFx(0.3, (ctx, layer, f0) => { if (layer === 'front') { const u = seg(G.t, f0, f0 + 0.25); star4(ctx, bx, by, 16 * (1 - u), 0.4, P.core, 1 - u, layer); } sparks(ctx, G.t, f0, bx, by, layer, { n: 6, pal: P, sp: 200, seed: Math.floor(bx), len: 10, life: 0.25 }); });
    };
    const bashHit = (e, dir, k) => {
      const t = T(id);
      if (bash.kind === 'charge') {
        if (hasMod(slot, 'trample')) { damage(e, { dmg: t.trampleDamage, slot, kx: dir * 120, heavy: true, hitstop: 0.02, elem: el(), freeze: el() === 'ice' ? t.iceFreeze : 0, burn: el() === 'fire' ? t.fireBurn : 0 }); return; }
        bash.carried.push(e); return;
      }
      damage(e, { dmg: t.bashDamage, slot, kx: dir * t.bashPush, ky: e.fly ? 0 : -140, launch: 0.5, hitstop: 0.04, shake: 4, elem: el(), freeze: el() === 'ice' ? t.iceFreeze : 0, burn: el() === 'fire' ? t.fireBurn : 0 });
      const P = pal(), x = e.x - dir * e.rx, y = e.y; addFx(0.7, (ctx, layer, f0) => impact(ctx, G.t, layer, f0, x, y, 0.7, P, { dir: dir > 0 ? Math.PI : 0, spread: 2, seed: Math.floor(x) }));
    };
    const endBash = () => {
      const t = T(id), b = bash; bash = null; const p = G.player, dir = b.dir, P = pal();
      if (b.kind === 'charge') {
        const dmg = lerp(t.slamMin, t.slamMax, b.k);
        for (const e of b.carried) if (!e.dead) damage(e, { dmg, slot, heavy: true, kx: dir * 700, ky: -260, launch: 0.6, hitstop: 0.06, shake: 7, frame: b.k > 0.9, elem: el(), freeze: el() === 'ice' ? t.iceFreeze : 0, burn: el() === 'fire' ? t.fireBurn : 0 });
        if (hasMod(slot, 'finisher')) { const x = p.x + dir * 40, y = p.y - 40, hit = damageArea(x, y, t.finisherRadius, { dmg: dmg * 0.5, slot, kx: 200, shake: 5, elem: el() }); for (const e of hit) e.stunT = Math.max(e.stunT, t.finisherStun); addFx(0.6, (ctx, layer, f0) => shockRing(ctx, G.t, layer, f0, x, y, t.finisherRadius, P)); }
        sfx(SND_AEGIS.bash.fn);
      }
      if (stored > 0) {                                         // Absorción: suelta lo parado
        const x = p.x + dir * 70, y = p.y - 40, d = stored * t.absorbPer; stored = 0;
        for (const e of alive()) if (Math.sign(e.x - p.x) === dir && Math.abs(e.x - p.x) < 260 && Math.abs(e.y - y) < 110) damage(e, { dmg: d, slot, heavy: true, kx: dir * 500, launch: 0.5, hitstop: 0.05 });
        addFx(0.6, (ctx, layer, f0) => { ramRing(ctx, G.t, layer, f0, x - dir * 40, y, dir > 0 ? 0 : Math.PI, 240, 1.2, P); });
        text(x, y - 60, 'ABSORCIÓN ' + Math.round(d), '#9ef2ff', true);
      }
    };
    const inst = {
      walls: () => walls,
      onPress(aim) {
        if (slot.arch === 'bastion') { const live = walls.filter(w => w.alive); if (live.length) { live.forEach(w => w.blast()); return; } }
        if (cd > 0 || held || bash) return;
        held = { t0: G.t }; shieldA = aim.ang; sfx(SND_AEGIS.raise.fn);
      },
      onHold(aim, dt) {
        if (!held) return;
        const t = T(id), p = G.player, c = center();
        shieldA = aim.ang; p.speedMul = Math.min(p.speedMul || 1, slot.arch === 'charge' ? 0.4 : t.holdMove); p.castT = 0.1;
        const R = t.shieldR;
        for (const s of G.ents) {
          if (s.kind !== 'enemyShot' || !s.hostile || s.friendly || s.captured || s.life <= 0) continue;
          const d = Math.hypot(s.x - c.x, s.y - c.y), da = Math.abs(((Math.atan2(s.y - c.y, s.x - c.x) - shieldA + Math.PI * 3) % TAU) - Math.PI);
          if (d < R + 14 && da < t.arc + 0.2) block(s);
        }
        contactT -= dt;
        if (contactT <= 0) {
          contactT = 0.3;
          for (const e of alive()) { const d = Math.hypot(e.x - c.x, e.y - c.y) - Math.max(e.rx, e.ry) * 0.7, da = Math.abs(((Math.atan2(e.y - c.y, e.x - c.x) - shieldA + Math.PI * 3) % TAU) - Math.PI);
            if (d < R + 10 && da < t.arc + 0.3) damage(e, { dmg: t.contactDamage, slot, kx: Math.cos(shieldA) * t.contactPush, ky: e.fly ? Math.sin(shieldA) * t.contactPush : -80, silentFlash: true }); }
        }
      },
      onRelease(aim) {
        if (!held) return;
        const t = T(id), p = G.player, k = clamp((G.t - held.t0) / t.chargeTime); held = null;
        cd = t.cooldown * cdMul();
        const dir = Math.sign(Math.cos(aim.ang)) || p.face;
        if (slot.arch === 'bastion') {
          const n = copies(); for (let c = 0; c < n; c++) { const w = aegisWall(slot, clamp(p.x + dir * (50 + c * 40), 20, WW - 20), dir); walls.push(w); G.ents.push(w); }
          cd = t.wallCooldown * cdMul();                      // el muro tiene su propia recarga (si no, planta-estalla sin parar)
          return;
        }
        const dist = slot.arch === 'charge' ? lerp(t.chargeMin, t.chargeMax, k) : t.bashDist;
        p.dashT = dist / 620; p.dashDir = dir; p.dashCd = Math.max(p.dashCd, p.dashT + 0.2);
        bash = { kind: slot.arch === 'charge' ? 'charge' : 'bash', dir, k, hit: new Set(), carried: [] };
        if (bash.kind === 'bash') sfx(SND_AEGIS.bash.fn);
        if (copies() > 1) bash.extra = true;                    // Duplicador: el golpe alcanza más lejos (ver update)
      },
      update(dt, current) {
        const p = G.player, t = T(id);
        cd -= dt; glowK = Math.max(0, glowK - dt * 3);
        if (held && !current) held = null;
        walls = walls.filter(w => w.alive);
        if (bash) {
          const dir = bash.dir, reach = bash.extra ? 1.6 : 1;
          if (bash.kind === 'charge' && hasMod(slot, 'unstoppable')) { p.hurtT = Math.max(p.hurtT, 0.05); for (const s of G.ents) if (s.kind === 'enemyShot' && s.hostile && !s.friendly && Math.hypot(s.x - p.x, s.y - p.y + 40) < 60) s.life = 0; }
          for (const e of alive()) {
            if (bash.hit.has(e)) continue;
            if (Math.abs(e.x - (p.x + dir * 30 * reach)) < e.rx + 26 * reach && Math.abs(e.y - (p.y - 40)) < e.ry + 40) { bash.hit.add(e); bashHit(e, dir, bash.k); }
          }
          for (const e of bash.carried) if (!e.dead) { e.x = p.x + dir * (40 + e.rx); e.vx = 0; if (!e.fly) e.y = Math.min(e.y, GROUND - 44); }
          if (p.dashT <= 0) endBash();
        }
      },
      shield: () => held ? { a: shieldA, k: Math.max(glowK, slot.arch === 'charge' ? clamp((G.t - held.t0) / T(id).chargeTime) : G.t - held.t0 < T(id).parryWindow * (slot.arch === 'reflect' ? T(id).reflectWindow : 1) ? 0.6 : 0) } : bash ? { a: bash.dir > 0 ? 0 : Math.PI, k: 0.8 } : null,
      ready() { return held && slot.arch === 'charge' ? clamp((G.t - held.t0) / T(id).chargeTime) : 1 - cd / Math.max(0.01, T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
    G.ents.push(aegisDrawEnt(slot, inst));
    return inst;
  },
});

// dibujo del escudo (arriba o en el golpe); vive mientras el arma esté equipada
function aegisDrawEnt(slot, inst) {
  return { kind: 'fx', update() { return slot.inst === inst && G.slots.slice(0, G.slotCount).includes(slot); },
    draw(ctx, layer) { const s = inst.shield(); if (!s) return; const p = G.player; crystalShield(ctx, G.t, layer, p.x + p.face * 6, p.y - 40, s.a, T('aegis').shieldR, elemPal('set'), 1, s.k); } };
}
// Bastión: muro plantado
function aegisWall(slot, x, dir) {
  const t = T('aegis'), t0 = G.t, tall = hasMod(slot, 'tall'), h = t.wallH * (tall ? 2 : 1), life = t.wallTime * (tall ? 2 : 1), pal = elemPal('set'), el = elemOf();
  let tick = 0, hurt = 0, done = false;
  sfx(SND_AEGIS.wall.fn);
  addFx(0.8, (ctx, layer, f0) => groundImpact(ctx, G.t, layer, f0, x, 0.6, pal, { seed: Math.floor(x), debris: false }));
  const w = {
    kind: 'wall', alive: true, x,
    blast() {
      if (done) return; done = true; this.alive = false;
      const big = hasMod(slot, 'blast') ? t.blastMul : 1, cx = x + dir * 40, cy = GROUND - h / 2, R = t.blastRadius * big;
      for (const e of alive()) if (Math.hypot(e.x - cx, e.y - cy) < R + e.rx) damage(e, { dmg: t.wallBlast * big, slot, heavy: true, kx: Math.sign(e.x - x || dir) * 380, ky: -300, launch: hasMod(slot, 'blast') ? 0.6 : 0, shake: 7, hitstop: 0.05, frame: true, elem: el, freeze: el === 'ice' ? 0.8 : 0, burn: el === 'fire' ? 2.5 : 0 });
      addFx(1, (ctx, layer, f0) => { impact(ctx, G.t, layer, f0, cx, cy, 1.1 * big, pal, { spread: TAU, seed: 7 }); shockRing(ctx, G.t, layer, f0, cx, cy, R, pal); });
      sfx(SND_AEGIS.blast.fn);
    },
    update(dt) {
      if (done) return false;
      if (G.t - t0 > life) { this.blast(); return false; }
      hurt = Math.max(0, hurt - dt * 3);
      for (const s of G.ents) if (s.kind === 'enemyShot' && !s.friendly && !s.captured && s.life > 0 && Math.abs(s.x - x) < 16 && s.y > GROUND - h) { s.life = 0; hurt = 1; sfx(SND_AEGIS.block.fn); }
      tick -= dt;
      for (const e of alive()) {
        if (Math.abs(e.x - x) > e.rx + 14 || e.y + e.ry < GROUND - h) continue;
        e.x = x + Math.sign(e.x - x || -dir) * (e.rx + 14); e.vx = 0; if (e.kind === 'walker') e.dir = Math.sign(e.x - x) || 1;
        if (tick <= 0) { damage(e, { dmg: t.wallDps * 0.25 * (hasMod(slot, 'spikes') ? t.spikeMul : 1), slot, silentFlash: !hasMod(slot, 'spikes'), kx: Math.sign(e.x - x) * 60 }); hurt = 1; }
      }
      if (tick <= 0) tick = 0.25;
      return true;
    },
    draw(ctx, layer) {
      const age = G.t - t0, a = easeOut(seg(age, 0, 0.15)) * (1 - seg(age, life - 0.3, life));
      crystalWall(ctx, G.t, layer, x, h * easeOut(seg(age, 0, 0.15)), pal, a, hurt);
      if (hasMod(slot, 'spikes') && layer === 'front') for (let i = 0; i < 5; i++) { const y = GROUND - 14 - i * (h - 20) / 4; for (const s of [-1, 1]) needle(ctx, x + s * 8, y, x + s * 22, y - 4, 3, pal.light, a, layer); }
      if (layer === 'front' && age > 0.3) { ctx.font = '600 9px system-ui'; ctx.fillStyle = 'rgba(255,255,255,.6)'; ctx.textAlign = 'center'; ctx.fillText('clic = estalla', x, GROUND - h - 8); ctx.textAlign = 'left'; }
    },
  };
  return w;
}
