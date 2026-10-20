// g-latigo.js — 9 · Látigo de luz, versión jugable. Ficha y decisiones: docs/armas/latigo.md · progresión: docs/armas/progresion.md
// Latigazo en arco desde arriba hacia donde apuntas: el cuerpo pega poco y la PUNTA chasquea (×daño, golpe fuerte): premia
// medir la distancia. Árbol: base → Cadena / Garfio / Remolino → 3 mejoras. Elemento y Duplicador vienen de items.
'use strict';
const SND_WHIP = {
  lash: sound('whip_lash', 'Látigo', 'Latigazo (silbido)', k => { const t = nowT(); vNoise(t, 0.16 * k, 0.22, 'bandpass', 900, 4200, 1.4, { rising: true, att: 0.01, wet: 0.12 }); }),
  crack: sound('whip_crack', 'Látigo', 'Chasquido de la punta', k => { const t = nowT(); vNoise(t, 0.035 * k, 0.6, 'highpass', 2500, 7000, 0.7, { att: 0.0005, wet: 0.3 }); vThump(t, 300, 120, 0.05 * k, 0.3, 3); vRing(t, [3300, 4700], 0.15 * k, 0.03, 0.3); }),
  hit: sound('whip_hit', 'Látigo', 'Golpe del cuerpo', k => { const t = nowT(); vThump(t, 170, 80, 0.1 * k, 0.3, 1.5); vNoise(t, 0.05 * k, 0.15, 'bandpass', 2000, 1200, 1, { att: 0.001 }); }),
  chain: sound('whip_chain', 'Látigo', 'Cadena · se encadenan', k => { const t = nowT(); for (let i = 0; i < 4; i++) vRing(t + i * 0.03 * k, [1800 + i * 300, 2700 + i * 200], 0.18 * k, 0.03, 0.3); }),
  share: sound('whip_share', 'Látigo', 'Cadena · el daño se reparte', k => { const t = nowT(); vRing(t, [1500, 2250], 0.12 * k, 0.025, 0.3); vNoise(t, 0.05 * k, 0.08, 'bandpass', 3000, 3000, 3, { att: 0.001 }); }),
  hook: sound('whip_hook', 'Látigo', 'Garfio · engancha', k => { const t = nowT(); vThump(t, 220, 90, 0.12 * k, 0.45, 2); vNoise(t, 0.3 * k, 0.12, 'bandpass', 600, 1800, 2, { att: 0.01, lfo: [22, 0.6] }); }),
  spin: sound('whip_spin', 'Látigo', 'Remolino (vuelta)', k => SFXLIB.whoosh(k, 1400, 700, 0.2, 0.14)),
  snap: sound('whip_snap', 'Látigo', 'Cadena · Ruptura', k => { SFXLIB.glass(k, 1); SFXLIB.boom(k * 0.6, 0.35); }),
};
addWeapon({
  id: 'whip', name: 'Látigo de luz', short: 'Látigo', pal: 'set',
  archetypes: [
    { id: 'link', name: 'Cadena', desc: 'El latigazo ENCADENA a los que golpea (si es uno, con el más cercano): mientras dura, parte del daño que recibe uno — de CUALQUIER arma — les llega a los demás.',
      ups: [{ id: 'shackle', name: 'Grillete', desc: 'Los encadenados se atraen entre sí: no pueden separarse.' },
            { id: 'conduct', name: 'Conducción', desc: 'Por la cadena pasan también el hielo, el fuego y las marcas.' },
            { id: 'snap', name: 'Ruptura', desc: 'Al romperse la cadena (se acaba o muere un eslabón) estalla en cada enemigo.' }] },
    { id: 'hook', name: 'Garfio', desc: 'Mantén: la punta engancha al enemigo y lo arrastra hacia ti. Al llegar (o al soltar) queda aturdido y vulnerable un momento.',
      ups: [{ id: 'fling', name: 'Lanzamiento', desc: 'Soltar mientras arrastras lo lanza hacia donde apuntas: choca con los demás.' },
            { id: 'reel', name: 'Red', desc: 'Engancha a todos los que toca el latigazo (hasta 3).' },
            { id: 'finish', name: 'Remate', desc: 'Cuando llega a ti, onda de choque alrededor (golpe fuerte).' }] },
    { id: 'spin', name: 'Remolino', desc: 'Mantén: giras el látigo a tu alrededor golpeando todo lo que toca (te mueves más lento). Al soltar, latigazo final fuerte hacia donde apuntas.',
      ups: [{ id: 'gale', name: 'Vendaval', desc: 'El remolino atrae a los enemigos cercanos hacia el aro.' },
            { id: 'accel', name: 'Aceleración', desc: 'Cuanto más tiempo giras, más rápido va y más daño hace.' },
            { id: 'deflect', name: 'Desvío', desc: 'El aro devuelve las balas enemigas que toca, como tuyas.' }] },
  ],
  tuning: {
    cooldown: [0.5, 0.1, 3, 0.05, 'Base: recarga (s)'], reach: [190, 60, 400, 5, 'Base: alcance'], sweepTime: [0.16, 0.06, 0.5, 0.01, 'Base: duración del latigazo (s)'],
    damage: [12, 1, 60, 1, 'Base: daño del cuerpo'], tipMul: [2.2, 1, 5, 0.05, 'Base: × daño de la punta (golpe fuerte)'], tipZone: [0.72, 0.3, 0.95, 0.01, 'Base: dónde empieza la punta (fracción)'],
    knock: [140, 0, 500, 5, 'Base: empuje'], iceFreeze: [0.3, 0, 1, 0.05, 'Hielo: congelación por golpe (punta ×2)'], fireBurn: [1.5, 0, 6, 0.1, 'Fuego: quemadura (s)'],
    linkTime: [6, 1, 15, 0.5, 'Cadena: duración (s)'], linkShare: [0.35, 0, 1, 0.05, 'Cadena: fracción del daño que pasa a los demás'], linkRange: [220, 50, 500, 10, 'Cadena: alcance para encadenar al más cercano'],
    shackleSpeed: [80, 0, 300, 5, 'Cadena · Grillete: atracción'], snapDamage: [25, 0, 120, 1, 'Cadena · Ruptura: daño'], snapRadius: [70, 0, 200, 5, 'Cadena · Ruptura: radio'],
    hookSpeed: [560, 100, 1500, 10, 'Garfio: velocidad del arrastre'], hookStun: [0.8, 0, 3, 0.1, 'Garfio: aturdido al llegar (s)'], hookVuln: [1.4, 1, 3, 0.05, 'Garfio: × daño que recibe al llegar'],
    hookVulnTime: [2, 0, 6, 0.1, 'Garfio: s vulnerable'], flingSpeed: [950, 200, 2000, 10, 'Garfio · Lanzamiento: velocidad'], finishDamage: [30, 0, 150, 1, 'Garfio · Remate: daño'],
    finishRadius: [90, 20, 250, 5, 'Garfio · Remate: radio'], reelMax: [3, 1, 6, 1, 'Garfio · Red: enganchados máx.'],
    spinTick: [0.22, 0.05, 1, 0.01, 'Remolino: cada cuánto golpea al mismo (s)'], spinDamage: [9, 1, 50, 1, 'Remolino: daño por golpe'], spinRate: [9, 2, 25, 0.5, 'Remolino: giro (rad/s)'],
    spinMove: [0.75, 0.2, 1, 0.05, 'Remolino: × tu velocidad'], finalMul: [2.5, 1, 6, 0.1, 'Remolino: × daño del latigazo final'],
    galeRadius: [260, 50, 500, 10, 'Remolino · Vendaval: radio'], galeSpeed: [90, 0, 300, 5, 'Remolino · Vendaval: atracción'],
    accelMax: [2, 1, 4, 0.1, 'Remolino · Aceleración: × máximo'], accelTime: [2, 0.3, 6, 0.1, 'Remolino · Aceleración: s hasta el máximo'],
  },
  create(slot) {
    const id = 'whip';
    let cd = 0, spin = null, hooked = [], chains = [];
    const later = (s, fn) => { const t0 = G.t; G.ents.push({ kind: 'timer', update() { if (G.t - t0 < s) return true; fn(); return false; }, draw() {} }); };
    const inst = {
      chains: () => chains,
      onPress(aim) {
        if (cd > 0 || spin) return;
        const t = T(id); cd = t.cooldown * cdMul();
        if (slot.arch === 'spin') { spin = whipSpin(slot, inst); G.ents.push(spin); return; }
        lash(aim.ang, {});
        if (copies() > 1) later(0.07, () => lash(aimInfo().ang + 0.25 * (Math.sign(Math.cos(aim.ang)) || 1), { mirror: true }));
      },
      onRelease(aim) {
        const t = T(id);
        if (spin) { const s = spin; spin = null; s.stop(); lash(aim.ang, { final: true, mul: t.finalMul * s.accelK() }); return; }
        if (hooked.length) {                                   // Garfio: suelta (Lanzamiento: lo lanza hacia el ratón)
          for (const h of hooked) if (!h.e.dead && !h.arrived) {
            if (hasMod(slot, 'fling')) { const a = Math.atan2(aim.ty - h.e.y, aim.tx - h.e.x); damage(h.e, { dmg: 0, slot, kx: Math.cos(a) * t.flingSpeed, ky: Math.sin(a) * t.flingSpeed * (h.e.fly ? 1 : 0.4) - (h.e.fly ? 0 : 200), launch: 0.8, silentFlash: true }); text(h.e.x, h.e.y - 60, 'LANZADO', '#e8f0ff', true); }
            else arrive(h);
          }
          hooked = [];
        }
      },
      onHold(aim, dt) {
        const t = T(id);
        for (const h of hooked) {
          if (h.e.dead || h.arrived) continue;
          const p = G.player, tx = p.x + p.face * 46, ty = h.e.fly ? p.y - 60 : h.e.y;
          if (Math.hypot(h.e.x - tx, h.e.y - ty) < 30) { arrive(h); continue; }
          h.e.pull = { x: tx, y: ty, s: t.hookSpeed, lift: h.e.fly }; h.e.vx = 0; h.e.hookT = G.t;
        }
        hooked = hooked.filter(h => !h.arrived && !h.e.dead);
      },
      update(dt, current) {
        cd -= dt;
        if (spin && !current) { spin.stop(); spin = null; }
        if (hooked.length && !current) hooked = [];
        chains = chains.filter(c => c.alive);
      },
      // Cadena: el daño a un encadenado (de cualquier arma) se reparte con los demás eslabones
      onAnyHit(e, o, dmg) {
        if (slot.arch !== 'link') return;
        const ch = chains.find(c => c.alive && c.members.includes(e)); if (!ch) return;
        const t = T(id), cond = hasMod(slot, 'conduct');
        for (const m of ch.members) {
          if (m === e || m.dead) continue;
          damage(m, { dmg: dmg * t.linkShare, raw: true, proc: true, slot, silentFlash: true, color: '#cfe8ff', elem: cond ? o.elem : null, freeze: cond ? (o.freeze || 0) * 0.6 : 0, burn: cond ? o.burn : 0, noReact: !cond });
          if (cond && e.markT > 0 && m.markT <= 0) { m.markT = e.markT; m.markN = 1; }
        }
        ch.pulse(e);
      },
      onAnyKill(e) { for (const c of chains) if (c.alive && c.members.includes(e)) c.lose(e); },
      ready() { return spin ? 1 : 1 - cd / Math.max(0.01, T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
    const arrive = h => {
      const t = T(id), e = h.e; h.arrived = true; if (e.dead) return;
      e.stunT = Math.max(e.stunT, t.hookStun); makeVuln(e, t.hookVuln, t.hookVulnTime); e.pull = null; e.vx = 0;
      if (hasMod(slot, 'finish')) {
        damageArea(e.x, e.y, t.finishRadius, { dmg: t.finishDamage, slot, heavy: true, kx: 200, shake: 5, hitstop: 0.04, elem: elemOf() });
        const x = e.x, y = e.y, P = elemPal('set'); addFx(0.9, (ctx, layer, f0) => { impact(ctx, G.t, layer, f0, x, y, 0.9, P, { spread: TAU, seed: Math.floor(x) }); shockRing(ctx, G.t, layer, f0, x, y, t.finishRadius, P); });
        sfx(k => SFXLIB.boom(k, 0.6));
      }
    };
    const lash = (ang, o) => {
      G.player.castT = 0.25;
      const l = whipLash(slot, ang, o, hits => {
        if (slot.arch === 'link' && !o.mirror) {
          const t = T(id), m = hits.filter(e => !e.dead);
          if (m.length === 1) { const n = nearestEnemy(m[0].x, m[0].y, t.linkRange, new Set(m)); if (n) m.push(n); }
          if (m.length >= 2) { const c = whipChainEnt(slot, m); chains.push(c); G.ents.push(c); if (chains.length > 3) chains.shift().end(); sfx(SND_WHIP.chain.fn); }
        }
      }, e => {                                                  // Garfio: engancha con la punta
        if (slot.arch !== 'hook' || o.mirror || !G.mouse.down) return false;
        if (hooked.length >= (hasMod(slot, 'reel') ? T(id).reelMax : 1)) return false;
        hooked.push({ e, arrived: false }); e.hookT = G.t; sfx(SND_WHIP.hook.fn); return true;
      });
      G.ents.push(l);
      sfx(SND_WHIP.lash.fn);
    };
    return inst;
  },
});

// geometría del látigo: el mango gira de arriba hacia el objetivo y cada trozo va con retraso (la punta latiguea)
function whipPts(x, y, angAt, reach, ext, n = 16) {
  const pts = [];
  for (let i = 0; i <= n; i++) { const s = i / n, a = angAt(s), d = s * reach * ext; pts.push({ x: x + Math.cos(a) * d, y: Math.min(GROUND - 2, y + Math.sin(a) * d), s }); }
  return pts;
}
// un latigazo. o = { mirror (copia del Duplicador), final (Remolino: latigazo final), mul }; onEnd(golpeados); onTip(e) → true si engancha
function whipLash(slot, ang, o, onEnd, onTip) {
  const t = T('whip'), el = elemOf(), pal = elemPal(o.final ? 'gold' : 'set'), face = Math.sign(Math.cos(ang)) || 1;
  const a0 = ang - 1.15 * face, a1 = ang + 0.35 * face, T0 = G.t, D = t.sweepTime * (o.final ? 1.3 : 1), LAG = 0.06, reach = t.reach * (o.final ? 1.15 : 1);
  const hit = new Set(), hitList = [];
  let pts = [], ended = false, hooks = [];
  const handle = age => lerp(a0, a1, easeInOut(seg(age, 0, D)));
  return {
    kind: 'lash',
    update() {
      const age = G.t - T0, m = muzzle();
      const ext = age <= D ? easeOut(seg(age, 0, D * 0.55)) : 1 - easeIn(seg(age, D + 0.04, D + 0.2));
      pts = whipPts(m.x, m.y, s => handle(age - s * LAG), reach, ext);
      // enganchados: la cuerda va del cristal al enemigo mientras se arrastra
      if (age <= D + 0.03) for (let i = 1; i < pts.length; i++) {
        const p0 = pts[i - 1], p1 = pts[i];
        for (const e of alive()) {
          if (hit.has(e) || !segHitsEllipse(p0.x, p0.y, p1.x, p1.y, e.x, e.y, e.rx, e.ry)) continue;
          hit.add(e); hitList.push(e);
          const tip = p1.s >= t.tipZone, mul = (tip ? t.tipMul : 1) * (o.mul || 1), dir = Math.sign(e.x - m.x) || face;
          damage(e, { dmg: t.damage * mul, kx: dir * t.knock * (tip ? 1.5 : 1), slot, heavy: tip || o.final, elem: el, freeze: el === 'ice' ? t.iceFreeze * (tip ? 2 : 1) : 0, burn: el === 'fire' ? t.fireBurn : 0,
            hitstop: tip ? 0.04 : 0.015, shake: tip ? 4 : 1.5, frame: o.final && tip });
          const hx = p1.x, hy = p1.y, P = pal;
          if (tip) { addFx(0.4, (ctx, layer, f0) => whipCrack(ctx, G.t, layer, f0, hx, hy, o.final ? 1.3 : 1, P)); sfx(SND_WHIP.crack.fn); if (onTip && onTip(e)) hooks.push(e); }
          else { addFx(0.6, (ctx, layer, f0) => impact(ctx, G.t, layer, f0, hx, hy, 0.4, P, { dir: Math.atan2(p1.y - p0.y, p1.x - p0.x), spread: 2, seed: Math.floor(hx) })); sfx(SND_WHIP.hit.fn); }
        }
      }
      if (!ended && age > D + 0.03) {
        ended = true; if (onEnd) onEnd(hitList);
        if (!hitList.length) { const tp = pts[pts.length - 1], P = pal; addFx(0.4, (ctx, layer, f0) => whipCrack(ctx, G.t, layer, f0, tp.x, tp.y, 0.7, P)); sfx(SND_WHIP.crack.fn); }   // chasquido al aire
      }
      hooks = hooks.filter(e => !e.dead && G.t - (e.hookT === undefined ? G.t : e.hookT) < 0.08);
      return age < D + 0.22 || hooks.length > 0;
    },
    draw(ctx, layer) {
      const age = G.t - T0;
      if (hooks.length && age > D + 0.2) { const m = muzzle(); for (const e of hooks) { const mid = { x: (m.x + e.x) / 2, y: (m.y + e.y) / 2 + 14 }; whipRope(ctx, G.t, layer, [m, mid, { x: e.x, y: e.y }], pal, 1, { w: 0.8 }); } return; }
      whipRope(ctx, G.t, layer, pts, pal, 1, { hot: age < D, w: o.final ? 1.3 : 1 });
    },
  };
}

// Remolino: el látigo gira alrededor del player mientras mantienes
function whipSpin(slot, owner) {
  const t = T('whip'), el = elemOf(), pal = elemPal('set'), t0 = G.t, last = new Map(), n = copies();
  let ang = -Math.PI / 2, stopped = -1, sfxA = 0, pts = [[], []];
  const accelK = () => hasMod(slot, 'accel') ? lerp(1, t.accelMax, clamp((G.t - t0) / t.accelTime)) : 1;
  sfx(SND_WHIP.lash.fn);
  return {
    kind: 'spin',
    accelK,
    stop() { if (stopped < 0) stopped = G.t; },
    update(dt) {
      if (stopped >= 0) return G.t - stopped < 0.15;
      const k = accelK(), m = muzzle(), R = t.reach * 0.85;
      ang += t.spinRate * k * dt; G.player.speedMul = Math.min(G.player.speedMul || 1, t.spinMove); G.player.castT = 0.1;
      if ((sfxA += t.spinRate * k * dt) > TAU) { sfxA = 0; sfx(SND_WHIP.spin.fn); }
      for (let c = 0; c < n; c++) {
        const base = ang + c * Math.PI;
        pts[c] = whipPts(m.x, m.y, s => base - s * 0.9, R, 1, 14);
        for (let i = 1; i < pts[c].length; i++) {
          const p0 = pts[c][i - 1], p1 = pts[c][i];
          for (const e of alive()) {
            if (G.t - (last.get(e) || -9) < t.spinTick || !segHitsEllipse(p0.x, p0.y, p1.x, p1.y, e.x, e.y, e.rx, e.ry)) continue;
            last.set(e, G.t);
            const tip = p1.s >= t.tipZone, dir = Math.sign(e.x - m.x) || 1;
            damage(e, { dmg: t.spinDamage * k * (tip ? 1.5 : 1), kx: dir * 90, slot, elem: el, freeze: el === 'ice' ? t.iceFreeze * 0.5 : 0, burn: el === 'fire' ? t.fireBurn : 0, silentFlash: !tip, hitstop: 0.01 });
            if (tip) { const hx = p1.x, hy = p1.y; addFx(0.4, (ctx, layer, f0) => whipCrack(ctx, G.t, layer, f0, hx, hy, 0.6, pal)); }
          }
          if (hasMod(slot, 'deflect')) for (const s of G.ents) if (s.kind === 'enemyShot' && s.hostile && !s.friendly && !s.captured && Math.hypot(s.x - p1.x, s.y - p1.y) < 16) {
            const a = Math.atan2(s.y - m.y, s.x - m.x), sp = Math.hypot(s.vx, s.vy) * 1.8; s.vx = Math.cos(a) * sp; s.vy = Math.sin(a) * sp; s.hostile = false; s.friendly = true; s.slot = slot; s.dmg = 14; s.life = 3;
            const sx = s.x, sy = s.y; addFx(0.4, (ctx, layer, f0) => whipCrack(ctx, G.t, layer, f0, sx, sy, 0.5, pal)); sfx(k2 => SFXLIB.glass(k2, 0.5));
          }
        }
      }
      if (hasMod(slot, 'gale')) for (const e of alive()) { const d = Math.hypot(e.x - m.x, e.y - m.y); if (d < t.galeRadius && d > R * 0.7) e.pull = { x: m.x, y: e.fly ? m.y : e.y, s: t.galeSpeed, lift: e.fly }; }
      return true;
    },
    draw(ctx, layer) {
      const a = stopped < 0 ? 1 : 1 - seg(G.t, stopped, stopped + 0.15), k = accelK();
      for (const p of pts) whipRope(ctx, G.t, layer, p, k > 1.5 ? elemPal('gold') : pal, a, { hot: true });
      if (layer === 'back' && pts[0].length) { const m = muzzle(); ellArc(ctx, m.x, m.y, t.reach * 0.85, t.reach * 0.85, 0, TAU, 1.5, pal.light, 0.25 * a, layer, { taper: 0, m: 40 }); }
    },
  };
}

// Cadena entre enemigos: dura linkTime; pulse() la hace brillar; lose() quita un eslabón (Ruptura: estalla)
function whipChainEnt(slot, members) {
  const t = T('whip'), t0 = G.t, pal = elemPal('set');
  let pulseT = -9, pulseFrom = null, ended = false;
  const burst = e => {
    if (!hasMod(slot, 'snap')) return;
    const x = e.x, y = e.y;
    damageArea(x, y, t.snapRadius, { dmg: t.snapDamage, slot, proc: true, kx: 140, shake: 3, elem: elemOf() });
    addFx(0.8, (ctx, layer, f0) => { impact(ctx, G.t, layer, f0, x, y, 0.7, pal, { spread: TAU, seed: Math.floor(x) }); });
    sfx(SND_WHIP.snap.fn);
  };
  const c = {
    kind: 'chain', alive: true, members: members.slice(),
    pulse(from) { if (G.t - pulseT > 0.05) { pulseT = G.t; pulseFrom = from; if (!G._shareSfx || G.t - G._shareSfx > 0.12) { G._shareSfx = G.t; sfx(SND_WHIP.share.fn); } } },
    lose(e) { if (!this.members.includes(e)) return; burst(e); this.members = this.members.filter(m => m !== e); if (this.members.filter(m => !m.dead).length < 2) this.end(); },
    end() { if (ended) return; ended = true; this.alive = false; for (const m of this.members) if (!m.dead) burst(m); },
    update() {
      if (ended) return false;
      if (G.t - t0 > t.linkTime) { this.end(); return false; }
      this.members = this.members.filter(m => !m.dead);
      if (hasMod(slot, 'shackle') && this.members.length > 1) {
        const cx = this.members.reduce((a, m) => a + m.x, 0) / this.members.length, cy = this.members.reduce((a, m) => a + m.y, 0) / this.members.length;
        for (const m of this.members) if (Math.hypot(m.x - cx, m.y - cy) > 40 && !m.stasis) m.pull = m.pull || { x: cx, y: m.fly ? cy : m.y, s: t.shackleSpeed, lift: m.fly };
      }
      return true;
    },
    draw(ctx, layer) {
      const a = easeOut(seg(G.t, t0, t0 + 0.15)) * (1 - seg(G.t, t0 + t.linkTime - 0.4, t0 + t.linkTime)), pu = seg(G.t, pulseT, pulseT + 0.25);
      for (let i = 1; i < this.members.length; i++) {
        const p = this.members[i - 1], q = this.members[i], fromP = pulseFrom === p;
        lightChain(ctx, G.t, layer, p.x, p.y, q.x, q.y, a, pal, pu < 1 ? (fromP ? pu : 1 - pu) : 0);
      }
    },
  };
  return c;
}
