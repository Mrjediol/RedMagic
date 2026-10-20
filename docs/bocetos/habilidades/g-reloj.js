// g-reloj.js — 7 · Reloj de arena, versión jugable. Ficha y decisiones: docs/armas/reloj.md · progresión: docs/armas/progresion.md
// Lanza un orbe que abre una BURBUJA DE TIEMPO: dentro, los enemigos y sus balas van lentos; al cerrarse, golpe de reloj.
// Arma de apoyo que se luce con la otra arma: Estasis guarda el daño de todo lo que les pegues, Eco repite tus golpes y
// Aceleración recarga tus dos armas. Árbol: base → Estasis / Eco / Aceleración → 3 mejoras. Elemento y Duplicador vienen de items.
'use strict';
const SND_TIME = {
  cast: sound('time_cast', 'Reloj', 'Lanzamiento (tic-tac)', k => { const t = nowT(); vTone(t, 1900, 1900, 0.03 * k, 0.09, 'square', 0.15, 0.001); vTone(t + 0.09 * k, 1500, 1500, 0.03 * k, 0.08, 'square', 0.15, 0.001); vNoise(t, 0.22 * k, 0.16, 'bandpass', 1200, 3200, 1.2, { att: 0.01, wet: 0.2 }); }),
  open: sound('time_open', 'Reloj', 'Se abre la burbuja', k => { const t = nowT(); vNoise(t, 0.4 * k, 0.18, 'bandpass', 300, 2400, 1.5, { rising: true, wet: 0.35 }); vRing(t + 0.3 * k, [784, 1175, 1568], 0.9 * k, 0.06, 0.5); vThump(t + 0.3 * k, 70, 40, 0.3 * k, 0.35, 1, 0.3); }),
  tick: sound('time_tick', 'Reloj', 'Tic (dentro)', k => { const t = nowT(); vTone(t, 2400, 2400, 0.018 * k, 0.05, 'square', 0.3, 0.001); }),
  close: sound('time_close', 'Reloj', 'Se cierra (golpe de reloj)', k => { const t = nowT(); vNoise(t, 0.25 * k, 0.2, 'bandpass', 3000, 400, 1.2, { att: 0.005, wet: 0.3 }); vThump(t + 0.2 * k, 110, 45, 0.3 * k, 0.55, 2, 0.2); vRing(t + 0.2 * k, [523, 1046], 0.6 * k, 0.05, 0.4); }),
  release: sound('time_release', 'Reloj', 'Estasis · se suelta el daño', k => { SFXLIB.boom(k, 0.9); SFXLIB.shatter(k, 0.8); const t = nowT(); vRing(t, [392, 587, 784, 1175], 1.2 * k, 0.07, 0.5); }),
  echo: sound('time_echo', 'Reloj', 'Eco (el golpe se repite)', k => { const t = nowT(); vThump(t, 160, 70, 0.12 * k, 0.28, 1.5, 0.5); vNoise(t, 0.1 * k, 0.16, 'bandpass', 2600, 1300, 2, { att: 0.002, wet: 0.6 }); vRing(t, [1318, 1976], 0.4 * k, 0.03, 0.6); }),
  quick: sound('time_quick', 'Reloj', 'Aceleración · proyectil acelerado', k => { const t = nowT(); vNoise(t, 0.12 * k, 0.16, 'bandpass', 1500, 6000, 1.5, { rising: true, att: 0.003 }); }),
};
addWeapon({
  id: 'time', name: 'Reloj de arena', short: 'Reloj', pal: 'chrono',
  archetypes: [
    { id: 'stasis', name: 'Estasis', desc: 'La burbuja DETIENE el tiempo: los de dentro se quedan quietos y GUARDAN todo el daño que reciben (también el de tu otra arma); al cerrarse lo reciben de golpe, aumentado.',
      ups: [{ id: 'interest', name: 'Interés', desc: 'El daño guardado crece por cada segundo que pasa dentro.' },
            { id: 'quake', name: 'Onda', desc: 'Al soltarse, parte del daño guardado salta en área a los que están cerca.' },
            { id: 'paradox', name: 'Paradoja', desc: 'Si alguno muere al soltarse, se abre otra burbuja de estasis más pequeña en su sitio.' }] },
    { id: 'echo', name: 'Eco', desc: 'Cada golpe de tus armas a un enemigo dentro de la burbuja se repite un instante después (eco a la mitad, con elemento y empuje).',
      ups: [{ id: 'reverb', name: 'Reverberación', desc: 'Cada eco deja otro eco más débil.' },
            { id: 'heavyEcho', name: 'Eco fuerte', desc: 'Los ecos son golpes fuertes: rompen a los congelados (añicos).' },
            { id: 'anchor', name: 'Ancla', desc: 'La burbuja se abre a tu alrededor al instante y te sigue.' }] },
    { id: 'quick', name: 'Aceleración', desc: 'Dentro de la burbuja TU tiempo corre: si estás dentro, tus dos armas recargan mucho más deprisa y te mueves más rápido; tus proyectiles que la cruzan salen acelerados y con más daño.',
      ups: [{ id: 'inertia', name: 'Inercia', desc: 'Al salir de la burbuja (o al cerrarse) la aceleración te dura unos segundos más.' },
            { id: 'encore', name: 'Bis', desc: 'Al cerrarse, tu otra arma queda recargada del todo.' },
            { id: 'rewind', name: 'Retroceso', desc: 'Las balas enemigas que entran se dan la vuelta y pasan a ser tuyas.' }] },
  ],
  // [valor, mín, máx, paso, etiqueta] — nombres = futuros campos en Unity
  tuning: {
    cooldown: [5.5, 0.5, 15, 0.1, 'Base: recarga (s)'], range: [400, 80, 800, 10, 'Base: distancia máxima (px)'], travelTime: [0.3, 0.05, 1, 0.05, 'Base: viaje del orbe (s)'],
    radius: [110, 40, 260, 5, 'Base: radio de la burbuja'], duration: [3, 0.5, 8, 0.1, 'Base: duración (s)'], slowK: [0.3, 0, 1, 0.05, 'Base: velocidad de los enemigos dentro (×)'],
    shotSlow: [0.2, 0, 1, 0.05, 'Base: velocidad de las balas enemigas dentro (×)'], closeDamage: [25, 0, 150, 1, 'Base: golpe de reloj al cerrarse'],
    iceRate: [0.35, 0, 2, 0.05, 'Hielo: congelación por segundo dentro'], fireBurn: [2.5, 0, 6, 0.1, 'Fuego: quemadura del golpe de reloj (s)'],
    stasisMul: [1.3, 1, 3, 0.05, 'Estasis: × daño guardado al soltarse'], interestRate: [0.15, 0, 0.6, 0.01, 'Estasis · Interés: + por segundo dentro'],
    quakeFrac: [0.4, 0, 1.5, 0.05, 'Estasis · Onda: fracción del daño que salta'], quakeRadius: [130, 30, 300, 5, 'Estasis · Onda: radio'],
    paradoxScale: [0.65, 0.3, 1, 0.05, 'Estasis · Paradoja: tamaño y duración de la nueva (×)'],
    echoDelay: [0.7, 0.1, 2, 0.05, 'Eco: retraso (s)'], echoMul: [0.5, 0.1, 1.5, 0.05, 'Eco: daño del eco (×)'], reverbMul: [0.5, 0.1, 1, 0.05, 'Eco · Reverberación: daño del 2.º eco (× del 1.º)'],
    anchorScale: [1.15, 0.6, 2, 0.05, 'Eco · Ancla: × radio'],
    quickCd: [2.5, 1, 6, 0.1, 'Aceleración: × velocidad de recarga dentro'], quickMove: [1.35, 1, 2.5, 0.05, 'Aceleración: × tu velocidad dentro'],
    quickProj: [1.5, 1, 3, 0.05, 'Aceleración: × velocidad de tus proyectiles'], quickDmg: [1.4, 1, 3, 0.05, 'Aceleración: × daño de tus proyectiles'],
    inertiaTime: [2.5, 0.2, 8, 0.1, 'Aceleración · Inercia: s que dura al salir'], rewindDamage: [15, 1, 60, 1, 'Aceleración · Retroceso: daño de una bala devuelta'],
  },
  create(slot) {
    const id = 'time';
    let cd = 0, bubbles = [];
    const inst = {
      onPress(aim) {
        if (cd > 0) return;
        const t = T(id); cd = t.cooldown * cdMul(); G.player.castT = 0.3;
        const n = copies(), anchor = slot.arch === 'echo' && hasMod(slot, 'anchor');
        if (anchor) {                                          // Ancla: alrededor del player, al instante (Duplicador = más grande)
          const b = chronoBubble2(slot, inst, G.player.x, G.player.y - 40, { follow: true, scale: t.anchorScale * (n > 1 ? 1.3 : 1) });
          bubbles.push(b); G.ents.push(b);
        } else {
          const d = Math.min(t.range, Math.hypot(aim.tx - aim.x, aim.ty - aim.y)), tx = aim.x + aim.dx * d, ty = Math.min(GROUND - 40, aim.y + aim.dy * d);
          for (let c = 0; c < n; c++) { const off = n > 1 ? (c ? 1 : -1) * t.radius * 0.8 : 0; G.ents.push(chronoOrbEnt(slot, inst, aim.x, aim.y, tx + off, ty, b => bubbles.push(b))); }
        }
        sfx(SND_TIME.cast.fn);
      },
      update(dt) {
        cd -= dt;
        bubbles = bubbles.filter(b => b.alive);
        if (slot.arch === 'quick' && G.t < (G.quickUntil || 0) && G.quickBy === slot) G.player.speedMul = Math.max(G.player.speedMul || 1, T(id).quickMove);
      },
      // Eco: cualquier golpe de tus armas a un enemigo dentro de una burbuja se repite
      onAnyHit(e, o) {
        if (slot.arch !== 'echo' || o.echo) return;
        const b = bubbles.find(b => b.contains(e)); if (!b) return;
        const t = T(id);
        G.ents.push(echoHit(slot, e, o, t.echoDelay, t.echoMul, b.pal));
        if (hasMod(slot, 'reverb')) G.ents.push(echoHit(slot, e, o, t.echoDelay * 2, t.echoMul * t.reverbMul, b.pal));
      },
      bubbles: () => bubbles,
      ready() { return 1 - cd / Math.max(0.01, T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
    return inst;
  },
});

// orbe que viaja y abre la burbuja al llegar
function chronoOrbEnt(slot, owner, x0, y0, tx, ty, onOpen) {
  const t = T('time'), t0 = G.t, pal = elemPal('chrono'), trail = [];
  let x = x0, y = y0;
  return {
    kind: 'orb',
    update() {
      const u = easeOut(seg(G.t, t0, t0 + t.travelTime));
      x = lerp(x0, tx, u); y = lerp(y0, ty, u) - Math.sin(Math.PI * u) * 30;
      trail.unshift({ x, y }); if (trail.length > 10) trail.pop();
      if (u >= 1) { const b = chronoBubble2(slot, owner, tx, ty, {}); G.ents.push(b); onOpen(b); return false; }
      return true;
    },
    draw(ctx, layer) { chronoOrb(ctx, G.t, layer, x, y, 1, pal, trail); },
  };
}

// La burbuja. o = { follow (Ancla: sigue al player), scale (× radio y duración), paradox (nacida de una Paradoja) }
function chronoBubble2(slot, owner, x, y, o) {
  const t = T('time'), arch = slot.arch, el = elemOf(), sc = o.scale || 1;
  const mode = arch === 'stasis' ? 'stasis' : arch === 'echo' ? 'echo' : arch === 'quick' ? 'quick' : 'slow';
  const pal = elemPal(mode === 'quick' ? 'gold' : 'chrono'), R = t.radius * sc, life = t.duration * (o.paradox ? sc : 1), t0 = G.t;
  let iceT = 0, tickT = 0, closed = false;
  const held = new Set();                                      // Estasis: los que tiene parados
  sfx(SND_TIME.open.fn);
  const inside = (px, py, pad = 0) => Math.hypot(px - b.x, py - b.y) < R + pad;
  const b = {
    kind: 'bubble', alive: true, x, y, pal,
    contains(e) { return inside(e.x, e.y, Math.max(e.rx, e.ry) * 0.6); },
    update(dt) {
      if (o.follow) { const p = G.player; this.x = lerp(this.x, p.x, Math.min(1, dt * 12)); this.y = lerp(this.y, p.y - 40, Math.min(1, dt * 12)); }
      const age = G.t - t0;
      // tiempo de los enemigos y de sus balas
      for (const e of alive()) {
        if (mode === 'stasis') {
          if (!e.stasis && this.contains(e)) { e.stasis = { bank: 0, by: this, t0: G.t }; held.add(e); e.vx = 0; e.vy = 0; }
          if (e.stasis && e.stasis.by === this) e.timeK = 0;
        } else if (this.contains(e)) e.timeK = Math.min(e.timeK === undefined ? 1 : e.timeK, t.slowK);
      }
      for (const s of G.ents) if (s.kind === 'enemyShot' && !s.captured && inside(s.x, s.y)) {
        if (mode === 'quick' && hasMod(slot, 'rewind') && s.hostile && !s.friendly) {   // Retroceso: la bala se da la vuelta y es tuya
          s.vx = -s.vx; s.vy = -s.vy; s.hostile = false; s.friendly = true; s.slot = slot; s.dmg = t.rewindDamage; s.life = 3;
          const sx = s.x, sy = s.y; addFx(0.35, (ctx, layer, f0) => echoRing(ctx, G.t, layer, f0, sx, sy, 14, pal));
        } else if (!s.friendly) s.timeK = t.shotSlow;
      }
      // Hielo (con 4): los de dentro se van congelando
      if (el === 'ice') { iceT -= dt; if (iceT <= 0) { iceT = 0.25; for (const e of alive()) if (this.contains(e)) damage(e, { dmg: 0, elem: 'ice', freeze: t.iceRate * 0.25, slot, dot: true, silentFlash: true, noReact: true }); } }
      // Aceleración: tú dentro = recargas y movimiento rápidos; tus proyectiles que la cruzan, acelerados
      if (mode === 'quick') {
        const p = G.player;
        if (inside(p.x, p.y - 40, 10)) { G.quickK = t.quickCd; G.quickBy = slot; G.quickUntil = Math.max(G.quickUntil || 0, G.t + (hasMod(slot, 'inertia') ? t.inertiaTime : 0.05)); p.speedMul = Math.max(p.speedMul || 1, t.quickMove); }
        for (const q of G.ents) {
          if (typeof q.vx !== 'number' || q.state !== 'fly' || q.hostile || q.captured || q.quicked === this || !inside(q.x, q.y)) continue;
          q.quicked = this; q.vx *= t.quickProj; q.vy *= t.quickProj; q.dmgMul = (q.dmgMul || 1) * t.quickDmg; if (q.life) q.life *= 1.2;
          const qx = q.x, qy = q.y, qa = Math.atan2(q.vy, q.vx);
          addFx(0.35, (ctx, layer, f0) => speedStreaks(ctx, G.t, layer, f0, qx, qy, qa, pal));
          if (!this._qs || G.t - this._qs > 0.12) { this._qs = G.t; sfx(SND_TIME.quick.fn); }
        }
      }
      tickT -= dt; if (tickT <= 0) { tickT = mode === 'quick' ? 0.25 : 0.5; sfx(SND_TIME.tick.fn); }
      if (age >= life && !closed) { closed = true; this.close(); }
      return age < life + 0.35;
    },
    close() {
      this.alive = false;
      const bx = this.x, by = this.y;
      if (mode === 'stasis') {
        for (const e of held) {
          if (!e.stasis || e.stasis.by !== this) continue;
          const s = e.stasis; e.stasis = null;
          if (e.dead) continue;
          const mul = t.stasisMul * (hasMod(slot, 'interest') ? 1 + t.interestRate * (G.t - s.t0) : 1), total = s.bank * mul;
          const ex = e.x, ey = e.y, big = clamp(total / 150);
          if (total > 0) {
            damage(e, { dmg: total, raw: true, release: true, proc: true, heavy: true, slot, kx: Math.sign(e.x - bx || 1) * 160, hitstop: 0.05 + 0.05 * big, shake: 4 + 8 * big, frame: total > 60,
              onKill: hasMod(slot, 'paradox') && sc > 0.35 ? dead => G.ents.push(chronoBubble2(slot, owner, dead.x, Math.min(GROUND - 40, dead.y), { scale: sc * t.paradoxScale, paradox: true })) : null });
            if (hasMod(slot, 'quake')) for (const o2 of alive()) if (o2 !== e && Math.hypot(o2.x - ex, o2.y - ey) < t.quakeRadius) damage(o2, { dmg: total * t.quakeFrac, raw: true, proc: true, slot, kx: Math.sign(o2.x - ex) * 200, color: '#e6f3f6' });
            text(ex, ey - 70, 'ESTASIS ' + Math.round(total), '#e6f3f6', true);
          }
          addFx(1, (ctx, layer, f0) => stasisBreak(ctx, G.t, layer, f0, ex, ey, big, pal));
        }
        sfx(SND_TIME.release.fn);
      } else if (t.closeDamage > 0) {                          // golpe de reloj a los de dentro
        for (const e of alive()) if (this.contains(e)) damage(e, { dmg: t.closeDamage, slot, kx: Math.sign(e.x - bx || 1) * 120, elem: el, burn: el === 'fire' ? t.fireBurn : 0, freeze: el === 'ice' ? 0.3 : 0, shake: 3, hitstop: 0.02 });
        sfx(SND_TIME.close.fn);
      }
      if (mode === 'quick' && hasMod(slot, 'encore')) { for (const so of otherSlots(slot)) if (so.inst && so.inst.refund) so.inst.refund(99); text(G.player.x, G.player.y - 110, 'BIS', '#ffe39a', true); }
      if (el === 'fire') G.ents.push(fireZone(bx, R * 0.8, 1.2, t.fireBurn, slot));
      const P = pal, rr = R;
      addFx(0.45, (ctx, layer, f0) => { const u = seg(G.t, f0, f0 + 0.4); ellArc(ctx, bx, by, rr * (1 - 0.8 * easeIn(u)), rr * (1 - 0.8 * easeIn(u)), layer === 'back' ? Math.PI : 0, layer === 'back' ? TAU : Math.PI, 3, P.light, 1 - u, layer, { taper: 0, m: 40 });
        if (layer === 'front') { glow(ctx, bx, by, rr * (1 - u), P.g1, 0.5 * (1 - u)); star4(ctx, bx, by, 40 * (1 - u), 0.3, P.core, 1 - u, layer); } });
    },
    draw(ctx, layer) {
      const age = G.t - t0, open = seg(age, 0, 0.18), fade = 1 - seg(age, life, life + 0.3);
      const speed = mode === 'stasis' ? 0 : mode === 'quick' ? 9 : mode === 'echo' ? 2 : 0.6;
      chronoBubble(ctx, G.t, layer, this.x, this.y, R, fade, pal, { mode, open, left: 1 - age / life, hand: -Math.PI / 2 + (mode === 'stasis' ? 0.4 : age * speed) });
      if (mode === 'stasis' && layer === 'front') for (const e of held) if (e.stasis && e.stasis.by === this) {   // cadenitas de la burbuja a cada parado
        ellArc(ctx, e.x, e.y, e.rx + 6, e.ry + 6, 0, TAU, 1.6, '#f2f7f8', 0.5 * fade, layer, { taper: 0, wob: 0.06, seed: e.sp.x, m: 28 });
      }
    },
  };
  return b;
}

// eco de un golpe: espera y lo repite (proc: no vuelve a provocar ecos ni efectos «al golpear»)
function echoHit(slot, e, o, delay, mul, pal) {
  const t0 = G.t, heavy = hasMod(slot, 'heavyEcho');
  return {
    kind: 'echo',
    update() {
      if (G.t - t0 < delay) return true;
      if (!e.dead) {
        damage(e, Object.assign({}, o, { dmg: (o.dmg || 0) * mul, proc: true, echo: true, heavy: heavy || o.heavy, kx: (o.kx || 0) * 0.5, ky: 0, hitstop: heavy ? 0.03 : 0, frame: false, onKill: null, launch: 0, shake: heavy ? 3 : 0 }));
        const x = e.x, y = e.y, r = Math.max(e.rx, e.ry);
        addFx(0.4, (ctx, layer, f0) => echoRing(ctx, G.t, layer, f0, x, y, r, pal));
        if (!G._echoSfx || G.t - G._echoSfx > 0.06) { G._echoSfx = G.t; sfx(SND_TIME.echo.fn); }
      }
      return false;
    },
    draw() {},
  };
}
