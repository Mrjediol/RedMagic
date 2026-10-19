// g-semilla.js — 5 · Semilla estelar, versión jugable. Ficha y decisiones: docs/armas/semilla.md · progresión: docs/armas/progresion.md
// Se lanza en parábola hacia el ratón (cae en el suelo bajo él), se arma y estalla.
// Árbol: base → evolución (Campo de minas / Racimo / Madura) → 3 mejoras de esa evolución. Elemento y Duplicador vienen de items.
'use strict';
addWeapon({
  id: 'seed', name: 'Semilla estelar', short: 'Semilla', pal: 'set',
  archetypes: [
    { id: 'prox', name: 'Campo de minas', desc: 'Se arma y espera a que un enemigo se acerque (hasta 3 a la vez; la más vieja estalla al poner otra).',
      ups: [{ id: 'chain', name: 'Reacción en cadena', desc: 'Una mina que estalla hace estallar a las cercanas, y cada una de la cadena hace más daño.' },
            { id: 'root', name: 'Raíces', desc: 'Al activarse, unas raíces sujetan a los de alrededor y estalla un poco después.' },
            { id: 'sprout', name: 'Semillero', desc: 'Cuanto más tiempo lleva puesta una mina, más crece: más daño y más radio.' }] },
    { id: 'cluster', name: 'Racimo', desc: 'Estallido menor que suelta semillas pequeñas que estallan en cadena.',
      ups: [{ id: 'seek', name: 'Semillas vivas', desc: 'Las semillas pequeñas saltan hacia los enemigos cercanos.' },
            { id: 'gen', name: 'Generaciones', desc: 'Cada semilla pequeña suelta a su vez otras dos aún más pequeñas.' },
            { id: 'plant', name: 'Siembra', desc: 'Las semillas pequeñas que no dan a nadie se quedan como minas un rato.' }] },
    { id: 'ripe', name: 'Madura', desc: 'Mantén para que la semilla madure en la mano: más daño, radio y alcance; suelta para lanzarla. Verde es más débil.',
      ups: [{ id: 'super', name: 'Supernova', desc: 'Madura del todo, el estallido se repite dos veces más.' },
            { id: 'implode', name: 'Implosión', desc: 'Antes de estallar atrae a los enemigos cercanos (más cuanto más madura).' },
            { id: 'harvest', name: 'Cosecha', desc: 'Cada baja de una semilla deja la siguiente ya medio madura.' }] },
  ],
  // [valor, mín, máx, paso, etiqueta] — nombres = futuros campos en Unity
  tuning: {
    cooldown: [0.8, 0.1, 4, 0.05, 'Base: recarga (s)'], range: [420, 80, 800, 10, 'Base: alcance (px)'], flightTime: [0.5, 0.2, 1.5, 0.05, 'Base: vuelo (s)'],
    armTime: [0.62, 0, 2, 0.02, 'Base: armado hasta estallar (s)'], damage: [45, 1, 200, 1, 'Base: daño'], radius: [70, 20, 200, 2, 'Base: radio'],
    knockback: [260, 0, 800, 10, 'Base: empuje'], launch: [420, 0, 900, 10, 'Base: lanza hacia arriba'],
    novaRadius: [130, 30, 300, 5, 'Hielo: radio de la nova'], puddleTime: [1.5, 0.2, 5, 0.1, 'Fuego: duración del charco (s)'], puddleBurn: [2, 0, 6, 0.1, 'Fuego: quemadura (s)'],
    dupSpread: [45, 0, 150, 1, 'Duplicador: separación de las dos semillas (px)'],
    proxRadius: [46, 10, 150, 2, 'Campo de minas: radio de disparo'], maxMines: [3, 1, 8, 1, 'Campo de minas: minas a la vez'],
    chainRadius: [150, 30, 400, 5, 'Campo de minas · Reacción: radio'], chainBonus: [0.3, 0, 1.5, 0.05, 'Campo de minas · Reacción: + daño por mina de la cadena'],
    rootRadius: [100, 20, 250, 5, 'Campo de minas · Raíces: radio'], rootDelay: [0.6, 0.1, 2, 0.05, 'Campo de minas · Raíces: espera hasta estallar (s)'],
    sproutTime: [4, 0.5, 15, 0.5, 'Campo de minas · Semillero: s hasta crecer del todo'], sproutMul: [1, 0, 3, 0.1, 'Campo de minas · Semillero: + daño al máximo'], sproutSize: [0.5, 0, 2, 0.05, 'Campo de minas · Semillero: + radio al máximo'],
    clusterCount: [3, 1, 8, 1, 'Racimo: semillas'], clusterDamage: [22, 1, 100, 1, 'Racimo: daño de cada una'], clusterRadius: [48, 10, 150, 2, 'Racimo: radio'],
    seekRange: [240, 50, 500, 10, 'Racimo · Semillas vivas: alcance'], genCount: [2, 1, 5, 1, 'Racimo · Generaciones: semillas por semilla'], genDamage: [12, 1, 60, 1, 'Racimo · Generaciones: daño'],
    plantLife: [4, 0.5, 12, 0.5, 'Racimo · Siembra: duración de la mina (s)'], plantRadius: [40, 10, 120, 2, 'Racimo · Siembra: radio de disparo'],
    matureTime: [1.2, 0.2, 4, 0.05, 'Madura: s hasta madurar del todo'], ripeMin: [0.5, 0.1, 1, 0.05, 'Madura: × daño sin madurar'], ripeMax: [1.8, 1, 4, 0.05, 'Madura: × daño madura'],
    ripeRange: [1.5, 1, 3, 0.05, 'Madura: × alcance madura'],
    superGap: [0.3, 0.1, 1, 0.05, 'Madura · Supernova: tiempo entre estallidos (s)'], superMul: [0.6, 0.1, 1.5, 0.05, 'Madura · Supernova: × daño de cada réplica'],
    implodeRadius: [180, 40, 400, 5, 'Madura · Implosión: radio'], implodeTime: [0.45, 0.1, 1.5, 0.05, 'Madura · Implosión: + s de armado atrayendo'], implodeSpeed: [320, 50, 800, 10, 'Madura · Implosión: fuerza'],
    harvestRipe: [0.5, 0, 1, 0.05, 'Madura · Cosecha: madurez con la que empieza tras una baja'],
  },
  create(slot) {
    const id = 'seed';
    let cd = 0, hand = null, harvest = 0;
    const mines = [];
    const lob = (aim, o = {}) => {
      const t = T(id); cd = t.cooldown * cdMul(); G.player.castT = 0.2;
      if (slot.arch === 'prox') { const live = mines.filter(m => !m.gone); while (live.length >= t.maxMines) live.shift().detonate(); }
      const reach = t.range * (o.ripe !== undefined ? lerp(1, t.ripeRange, o.ripe) : 1);
      const tx = clamp(aim.tx, aim.x - reach, aim.x + reach), n = copies();
      for (let c = 0; c < n; c++) { const s = starSeedEnt(slot, aim.x, aim.y, tx + (n > 1 ? (c ? 1 : -1) * t.dupSpread : 0), Object.assign({ mines, onKill: () => { harvest = 1; } }, o)); G.ents.push(s); mines.push(s); }
      sfx(SND.seed_lob.fn);
    };
    // Madura: la semilla crece en la mano mientras mantienes; al soltar (o cambiar de arma) se lanza
    const throwHand = aim => { if (!hand) return; const r = hand.ripe(); hand.gone = true; hand = null; lob(aim || aimInfo(), { ripe: r }); };
    return {
      onPress(aim) {
        if (cd > 0 || hand) return;
        if (slot.arch === 'ripe') {
          const t = T(id), r0 = hasMod(slot, 'harvest') && harvest ? t.harvestRipe : 0; harvest = 0;
          hand = seedInHand(slot, r0); G.ents.push(hand); return;
        }
        lob(aim);
      },
      onHold(aim, dt) { if (hand) hand.grow(dt); },
      onRelease(aim) { throwHand(aim); },
      update(dt, current) { cd -= dt; if (hand && !current) throwHand(); for (let i = mines.length - 1; i >= 0; i--) if (mines[i].gone) mines.splice(i, 1); },
      ready() { return hand ? hand.ripe() : 1 - cd / (T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
  },
});

// Madura: semilla en la mano que crece (anillo de madurez alrededor del cristal)
function seedInHand(slot, r0) {
  const t = T('seed'), pal = elemPal('set'), gp = PAL.gold;
  let held = r0 * t.matureTime, full = r0 >= 1;
  return {
    kind: 'hand', gone: false,
    ripe() { return clamp(held / t.matureTime); },
    grow(dt) { held += dt; G.player.castT = 0.1; if (!full && this.ripe() >= 1) { full = true; sfx(SND.seed_armed.fn); } },
    update() { return !this.gone; },
    draw(ctx, layer) {
      if (layer !== 'front') return;
      const m = muzzle(), r = this.ripe(), P = r >= 1 ? gp : pal, pu = r >= 1 ? 0.8 + 0.2 * Math.sin(G.t * 30) : 1;
      glow(ctx, m.x, m.y, (20 + 30 * r) * pu, P.g1, 0.6);
      ellArc(ctx, m.x, m.y, 18, 18, -Math.PI / 2, -Math.PI / 2 + TAU * r, 2.4, P.light, 0.9, layer, { taper: 0, m: 32 });
      starSeed(ctx, G.t, m.x, m.y - 2, G.t * (4 + 10 * r), P, 1, layer, 0.5 + 0.7 * r);
    },
  };
}

// Semilla lanzada. o = { mines (las de esta arma, para la Reacción), onKill, ripe (Madura 0..1), chainN (orden en la cadena) }
function starSeedEnt(slot, x0, y0, tx, o = {}) {
  const t = T('seed'), el = elemOf(), ice = el === 'ice', fire = el === 'fire', arch = slot.arch;
  const cluster = arch === 'cluster', prox = arch === 'prox', ripe = o.ripe;
  const pal = elemPal(ripe >= 1 ? 'gold' : 'set'), t0 = G.t, TL = t0 + t.flightTime, d = Math.abs(tx - x0), hApex = 60 + d * 0.28;
  const rooting = prox && hasMod(slot, 'root'), armT = t.armTime + (ripe !== undefined && hasMod(slot, 'implode') ? t.implodeTime : 0);   // Implosión: atrae un rato más antes de estallar
  let state = 'fly', armedAt = -1, trigger = -1, ticks = [], chainN = 0, rooted = [];
  const pos = tt => { const u = seg(tt, t0, TL); return { x: lerp(x0, tx, u), y: lerp(y0, GROUND - 4, u) - 4 * hApex * u * (1 - u) }; };
  // multiplicadores de daño y radio: Madura (madurez), Semillero (tiempo puesta), Reacción (orden en la cadena)
  const grownK = () => prox && hasMod(slot, 'sprout') && armedAt >= 0 ? clamp((G.t - armedAt) / t.sproutTime) : 0;
  const mul = () => (ripe !== undefined ? lerp(t.ripeMin, t.ripeMax, ripe) : 1) * (1 + t.sproutMul * grownK()) * (1 + t.chainBonus * chainN);
  const rmul = () => (ripe !== undefined ? Math.sqrt(lerp(t.ripeMin, t.ripeMax, ripe)) : 1) * (1 + t.sproutSize * grownK());
  const hit = extra => Object.assign({ slot, onKill: o.onKill }, extra);
  const ent = {
    kind: 'seed', gone: false, x: x0, y: y0,
    get armed() { return state === 'armed'; },
    detonate(n = 0) { if (state === 'done' || state === 'boom') return; chainN = n; state = 'boom'; ent.boomNow = true; },
    update(dt) {
      if (state === 'fly') {
        const p = pos(G.t); this.x = p.x; this.y = p.y;
        if (G.t >= TL) { state = 'armed'; armedAt = G.t; this.x = tx; this.y = GROUND - 5; sfx(SND.seed_land.fn); addFx(0.6, (ctx, layer, tl) => { dust(ctx, G.t, tl, tx, layer, { n: 5, reach: 22, sc: 0.6, seed: 4 }); if (layer === 'front') { const u = seg(G.t, tl, tl + 0.15); if (u < 1) star4(ctx, tx, GROUND - 4, 18 * (1 - u * 0.5), 0, pal.core, 1 - u, layer); } });
          ticks = prox ? [armedAt + 0.12] : [armedAt + armT * 0.24, armedAt + armT * 0.55, armedAt + armT * 0.79];
          ticks.forEach((tt, i) => G.ents.push({ update() { if (G.t >= tt) { sfx(prox ? SND.seed_armed.fn : k => seedTick(k, i)); return false; } return state === 'armed'; }, draw() {} }));
        }
        return true;
      }
      if (state === 'armed') {
        if (prox) {
          if (trigger < 0 && G.t > armedAt + 0.12 && alive().some(e => Math.abs(e.x - tx) < t.proxRadius + e.rx * 0.5 && GROUND - (e.y + e.ry) < 40)) {
            trigger = G.t; sfx(SND.seed_trigger.fn);
            if (rooting) { rooted = alive().filter(e => Math.abs(e.x - tx) < t.rootRadius && GROUND - (e.y + e.ry) < 60).map(e => ({ e, x: e.x })); sfx(k => SFXLIB.whoosh(k, 600, 200, 0.25, 0.2)); }
          }
          if (trigger >= 0 && G.t - trigger >= (rooting ? t.rootDelay : 0.12)) state = 'boom';
          if (G.t - armedAt > 20) state = 'boom';
        } else if (G.t - armedAt >= armT) state = 'boom';
        for (const r of rooted) if (!r.e.dead) { r.e.x = r.x; r.e.vx = 0; r.e.stunT = Math.max(r.e.stunT, 0.1); }   // Raíces: no se mueven
        if (this.boomNow) state = 'boom';
        if (state !== 'boom') return true;
      }
      if (state === 'boom') {
        this.explode(1);
        if (ripe >= 1 && hasMod(slot, 'super')) for (let k = 1; k <= 2; k++) G.ents.push(delayed(t.superGap * k, () => this.explode(t.superMul, true)));
        if (prox && hasMod(slot, 'chain') && o.mines) {        // Reacción en cadena: las minas cercanas estallan detrás
          for (const m of o.mines) if (m !== ent && m.armed && Math.abs(m.x - tx) < t.chainRadius) { const n = chainN + 1; G.ents.push(delayed(0.12, () => m.detonate(n))); }
          if (chainN > 0) text(tx, GROUND - 120, 'CADENA ×' + (chainN + 1), '#ffe39a', true);
        }
        state = 'done'; this.gone = true; return false;
      }
      return false;
    },
    explode(k = 1, echo = false) {
      const x = tx, cy = GROUND - 30, M = mul() * k, RM = rmul();
      if (ice) {
        damageArea(x, cy, t.novaRadius * RM, hit({ dmg: t.damage * 0.5 * M, kx: 60, elem: 'ice', freeze: 1, shake: 6, frame: !echo, hitstop: echo ? 0 : 0.05 }));
        const nr = t.novaRadius * RM; addFx(1.6, (ctx, layer, tb) => frostNova(ctx, G.t, layer, tb, x, nr));
        sfx(SND.seed_nova.fn);
        return;
      }
      const sc = (cluster ? 0.7 : 1.1) * RM;
      const blasted = damageArea(x, cy, t.radius * RM * (cluster ? 0.75 : 1), hit({ dmg: t.damage * M * (cluster ? 0.6 : 1), kx: t.knockback, ky: -t.launch, heavy: true, elem: el, burn: fire ? t.puddleBurn : 0, shake: cluster ? 5 : 8, frame: !echo, hitstop: echo ? 0 : 0.06 }));
      if (fire && !echo) G.ents.push(fireZone(x, t.radius * 0.9 * RM, t.puddleTime, t.puddleBurn, slot));
      addFx(1.4, (ctx, layer, tb) => {
        const u = G.t - tb;
        lightColumn(ctx, x, GROUND, u, sc, pal, layer);
        groundImpact(ctx, G.t, layer, tb, x, sc, pal, { seed: Math.floor(x) });
        const R = rand(19);
        for (let i = 0; i < 10; i++) { const ang = -Math.PI / 2 + (R() - 0.5) * 1.3, nu = seg(u, 0, 0.28), l0 = 12 * sc, l1 = l0 + (50 + R() * 70) * sc * easeOut(nu), lay = R() < 0.4 ? 'back' : 'front'; if (lay === layer && nu < 1) needle(ctx, x + Math.cos(ang) * l0, GROUND - 10 + Math.sin(ang) * l0, x + Math.cos(ang) * l1, GROUND - 10 + Math.sin(ang) * l1, 3.4 * sc * (1 - nu * 0.6), i % 3 ? pal.light : pal.core, 1 - nu, layer); }
        cracks(ctx, G.t, tb, x, layer, { n: 6, len: 60 * sc, pal });
      });
      sfx(cluster ? SND.seed_split.fn : SND.seed_boom.fn);
      if (cluster && !echo) {
        const used = new Set(blasted);                     // Semillas vivas: a por los que no tocó el estallido
        for (let i = 0; i < t.clusterCount; i++) {
          const R = rand(i + Math.floor(G.t * 100)), side = i % 2 ? 1 : -1;
          let x1 = x + side * (50 + R() * 120);
          if (hasMod(slot, 'seek')) { const e = nearestEnemy(x, cy, t.seekRange, used); if (e) { used.add(e); x1 = e.x + (R() - 0.5) * 10; } }   // Semillas vivas
          G.ents.push(bomblet(slot, x, x1, 0.28 + i * 0.07, pal, { gen: hasMod(slot, 'gen') ? 1 : 0, onKill: o.onKill }));
        }
      }
    },
    draw(ctx, layer) {
      if (state === 'fly') {
        if (layer === 'back') { const pts = trailPts(pos, G.t, 0.09, 10, t0); ribbon(ctx, pts, s => 8 * Math.pow(1 - s, 1.2), pal.dark, 0.35, layer); ribbon(ctx, pts, s => 4 * (1 - s), pal.light, 0.9, layer); }
        starSeed(ctx, G.t, this.x, this.y, G.t * 14, pal, 1, layer, ripe !== undefined ? 0.6 + 0.7 * ripe : 1);
        return;
      }
      if (state !== 'armed') return;
      const blink = ticks.reduce((m, tt) => Math.max(m, G.t >= tt ? 1 - seg(G.t, tt, tt + 0.16) : 0), 0), armed = prox && G.t > armedAt + 0.12, gk = grownK();
      if (layer === 'front') {
        for (let i = 0; i < 3 + Math.round(gk * 4); i++) { const s = i - (3 + Math.round(gk * 4) - 1) / 2; needle(ctx, tx, GROUND - 2, tx + s * 7, GROUND - 16 - (s === 0 ? 8 : 0) - blink * 8 - gk * 14, 2.2, pal.light, 0.8, layer); }
        starSeed(ctx, G.t, tx, GROUND - 5 - gk * 6, 0.785, pal, 1, layer, (0.8 + blink * 0.35) * (1 + gk * 0.6));
      }
      if (armed) groundRing(ctx, tx, GROUND, t.proxRadius, 1.4, pal.light, 0.35 + 0.15 * Math.sin(G.t * 8), layer, { taper: 0, seed: 2 });
      for (const tt of ticks) { const u = seg(G.t, tt, tt + 0.4); if (u > 0 && u < 1) groundRing(ctx, tx, GROUND, 8 + 44 * easeOut(u), 2.6 * (1 - u) + 0.4, pal.mid, 1 - u, layer, { seed: 3 }); }
      if (trigger >= 0 && layer === 'front') glow(ctx, tx, GROUND - 8, 40 * seg(G.t, trigger, trigger + 0.12), pal.g2, 1);
      if (trigger >= 0 && layer === 'front') for (const [i, r] of rooted.entries()) if (!r.e.dead) {   // raíces: rayos que suben a los sujetos
        const R = rand(i * 7 + Math.floor(G.t * 20)); bolt(ctx, tx, GROUND - 2, r.e.x, r.e.y + r.e.ry * 0.6, 6, R, 1.6, pal, 0.9, layer, 0.35);
      }
    },
  };
  // Madura · Implosión: atrae mientras se arma (más fuerte cuanto más madura)
  if (ripe !== undefined && hasMod(slot, 'implode')) {
    const k = 0.4 + 0.6 * ripe;
    G.ents.push({ update() { if (ent.gone) return false; if (state === 'armed') for (const e of alive()) if (Math.hypot(e.x - tx, e.y - (GROUND - 30)) < t.implodeRadius * k) e.pull = { x: tx, y: GROUND - 30, s: t.implodeSpeed * k, lift: e.fly }; return true; },
      draw(ctx, layer) { if (state === 'armed') { const u = (G.t * 2.5) % 1; groundRing(ctx, tx, GROUND, t.implodeRadius * k * (1 - u), 2, pal.light, 0.6 * u, layer, { taper: 0, seed: 6 }); } } });
  }
  return ent;
}
// llamada diferida (réplicas de la Supernova, minas de la cadena)
function delayed(s, fn) { const at = G.t + s; return { update() { if (G.t >= at) { fn(); return false; } return true; }, draw() {} }; }
// tic de armado (sube de tono con cada pulso)
function seedTick(k, i) { const tn = nowT(); vNoise(tn, 0.02 * k, 0.25, 'bandpass', 2600 + i * 500, 2600 + i * 500, 6, { att: 0.001 }); vTone(tn, 220 * Math.pow(1.26, i), 200 * Math.pow(1.26, i), 0.12 * k, 0.12, 'sine', 0.1); }
// semilla pequeña del Racimo. o = { gen (Generaciones: suelta más), mini (de segunda generación), onKill }
function bomblet(slot, x0, x1, dur, pal, o = {}) {
  const t = T('seed'), t0 = G.t, t1 = t0 + dur, sc = o.mini ? 0.42 : 0.6, hop = o.mini ? 45 : 80;
  const pos = tt => { const u = seg(tt, t0, t1); return { x: lerp(x0, x1, u), y: lerp(GROUND - 8, GROUND - 4, u) - 4 * hop * u * (1 - u) }; };
  return {
    kind: 'bomblet',
    update() {
      if (G.t < t1) return true;
      const r = o.mini ? t.clusterRadius * 0.7 : t.clusterRadius;
      const hit = damageArea(x1, GROUND - 30, r, { dmg: o.mini ? t.genDamage : t.clusterDamage, kx: 160, ky: -200, heavy: true, slot, shake: o.mini ? 2 : 3.5, onKill: o.onKill });
      addFx(1.2, (ctx, layer, tb) => groundImpact(ctx, G.t, layer, tb, x1, sc, pal, { seed: Math.floor(x1) }));
      sfx(SND.seed_mini.fn);
      if (o.gen) for (let i = 0; i < t.genCount; i++) { const R = rand(i + Math.floor(x1)), dx = (i % 2 ? 1 : -1) * (30 + R() * 50); G.ents.push(bomblet(slot, x1, x1 + dx, 0.2 + i * 0.05, pal, { mini: true, onKill: o.onKill })); }
      if (!hit.length && hasMod(slot, 'plant')) G.ents.push(plantMine(slot, x1, pal, o));   // Siembra
      return false;
    },
    draw(ctx, layer) {
      const p = pos(G.t);
      if (layer === 'back') { const pts = trailPts(pos, G.t, 0.07, 8, t0); ribbon(ctx, pts, s => 4 * (1 - s), pal.light, 0.9, layer); }
      starSeed(ctx, G.t, p.x, p.y, G.t * 18, pal, 1, layer, sc);
    },
  };
}
// Racimo · Siembra: semilla pequeña que se queda de mina un rato
function plantMine(slot, x, pal, o) {
  const t = T('seed'), t0 = G.t;
  return {
    kind: 'plant',
    update() {
      if (G.t - t0 > t.plantLife) return false;
      if (G.t - t0 > 0.2 && alive().some(e => Math.abs(e.x - x) < t.plantRadius + e.rx * 0.5 && GROUND - (e.y + e.ry) < 40)) {
        damageArea(x, GROUND - 30, o.mini ? t.clusterRadius * 0.7 : t.clusterRadius, { dmg: o.mini ? t.genDamage : t.clusterDamage, kx: 160, ky: -200, heavy: true, slot, shake: 3, onKill: o.onKill });
        addFx(1.2, (ctx, layer, tb) => groundImpact(ctx, G.t, layer, tb, x, 0.6, pal, { seed: Math.floor(x) }));
        sfx(SND.seed_mini.fn);
        return false;
      }
      return true;
    },
    draw(ctx, layer) {
      const a = 1 - seg(G.t, t0 + t.plantLife - 0.4, t0 + t.plantLife);
      if (layer === 'front') starSeed(ctx, G.t, x, GROUND - 4, 0.785, pal, a, layer, 0.45);
      groundRing(ctx, x, GROUND, t.plantRadius, 1.2, pal.light, (0.25 + 0.1 * Math.sin(G.t * 8)) * a, layer, { taper: 0, seed: 8 });
    },
  };
}
