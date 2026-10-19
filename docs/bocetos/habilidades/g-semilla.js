// g-semilla.js — 5 · Semilla estelar, versión jugable. Ficha y decisiones: docs/armas/semilla.md
// Se lanza en parábola hacia el ratón (cae en el suelo bajo él), se arma y estalla. Con Proximidad espera a que llegue alguien.
'use strict';
addWeapon({
  id: 'seed', name: 'Semilla estelar', short: 'Semilla', pal: 'set',
  // mejoras provisionales (árbol con evoluciones pendiente). Elemento (item): hielo = nova de escarcha; fuego = charco ardiendo.
  ups: [
    { id: 'cluster', name: 'Racimo', desc: 'Estallido menor que suelta semillas pequeñas que estallan en cadena.' },
    { id: 'prox', name: 'Proximidad', desc: 'Se arma y espera a que un enemigo se acerque (hasta 3 a la vez).' },
  ],
  tuning: {
    cooldown: [0.8, 0.1, 4, 0.05, 'Recarga (s)'], range: [420, 80, 800, 10, 'Alcance (px)'], flightTime: [0.5, 0.2, 1.5, 0.05, 'Vuelo (s)'],
    armTime: [0.62, 0, 2, 0.02, 'Armado hasta estallar (s)'], damage: [45, 1, 200, 1, 'Daño'], radius: [70, 20, 200, 2, 'Radio'],
    knockback: [260, 0, 800, 10, 'Empuje'], launch: [420, 0, 900, 10, 'Lanza hacia arriba'], clusterCount: [3, 1, 8, 1, 'Racimo: semillas'],
    clusterDamage: [22, 1, 100, 1, 'Racimo: daño de cada una'], clusterRadius: [48, 10, 150, 2, 'Racimo: radio'], novaRadius: [130, 30, 300, 5, 'Hielo: radio de la nova'],
    proxRadius: [46, 10, 150, 2, 'Proximidad: radio de disparo'], maxMines: [3, 1, 8, 1, 'Proximidad: minas a la vez'], puddleTime: [1.5, 0.2, 5, 0.1, 'Fuego: duración del charco (s)'], puddleBurn: [2, 0, 6, 0.1, 'Fuego: quemadura (s)'],
    dupSpread: [45, 0, 150, 1, 'Duplicador: separación de las dos semillas (px)'],
  },
  create(slot) {
    const id = 'seed';
    let cd = 0;
    const mines = [];
    return {
      onPress(aim) {
        if (cd > 0) return;
        const t = T(id); cd = t.cooldown * cdMul(); G.player.castT = 0.2;
        if (hasMod(slot, 'prox')) { const live = mines.filter(m => !m.gone); while (live.length >= t.maxMines) live.shift().detonate(); }
        const tx = clamp(aim.tx, aim.x - t.range, aim.x + t.range), n = copies();
        for (let c = 0; c < n; c++) { const s = starSeedEnt(slot, aim.x, aim.y, tx + (n > 1 ? (c ? 1 : -1) * t.dupSpread : 0)); G.ents.push(s); mines.push(s); }
        sfx(SND.seed_lob.fn);
      },
      update(dt) { cd -= dt; for (let i = mines.length - 1; i >= 0; i--) if (mines[i].gone) mines.splice(i, 1); },
      ready() { return 1 - cd / (T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
  },
});

function starSeedEnt(slot, x0, y0, tx) {
  const t = T('seed'), el = elemOf(), ice = el === 'ice', fire = el === 'fire', cluster = hasMod(slot, 'cluster'), prox = hasMod(slot, 'prox');
  const pal = elemPal('set'), t0 = G.t, TL = t0 + t.flightTime, d = Math.abs(tx - x0), hApex = 60 + d * 0.28;
  let state = 'fly', armedAt = -1, trigger = -1, ticks = [];
  const pos = tt => { const u = seg(tt, t0, TL); return { x: lerp(x0, tx, u), y: lerp(y0, GROUND - 4, u) - 4 * hApex * u * (1 - u) }; };
  const ent = {
    kind: 'seed', gone: false, x: x0, y: y0,
    detonate() { if (state === 'done') return; state = 'boom'; ent.boomNow = true; },
    update(dt) {
      if (state === 'fly') {
        const p = pos(G.t); this.x = p.x; this.y = p.y;
        if (G.t >= TL) { state = 'armed'; armedAt = G.t; this.x = tx; this.y = GROUND - 5; sfx(SND.seed_land.fn); addFx(0.6, (ctx, layer, tl) => { dust(ctx, G.t, tl, tx, layer, { n: 5, reach: 22, sc: 0.6, seed: 4 }); if (layer === 'front') { const u = seg(G.t, tl, tl + 0.15); if (u < 1) star4(ctx, tx, GROUND - 4, 18 * (1 - u * 0.5), 0, pal.core, 1 - u, layer); } });
          ticks = prox ? [armedAt + 0.12] : [armedAt + t.armTime * 0.24, armedAt + t.armTime * 0.55, armedAt + t.armTime * 0.79];
          ticks.forEach((tt, i) => G.ents.push({ update() { if (G.t >= tt) { sfx(prox ? SND.seed_armed.fn : k => seedTick(k, i)); return false; } return state === 'armed'; }, draw() {} }));
        }
        return true;
      }
      if (state === 'armed') {
        if (prox) {
          if (trigger < 0 && G.t > armedAt + 0.12 && alive().some(e => Math.abs(e.x - tx) < t.proxRadius + e.rx * 0.5 && GROUND - (e.y + e.ry) < 40)) { trigger = G.t; sfx(SND.seed_trigger.fn); }
          if (trigger >= 0 && G.t - trigger >= 0.12) state = 'boom';
          if (G.t - armedAt > 20) state = 'boom';
        } else if (G.t - armedAt >= t.armTime) state = 'boom';
        if (this.boomNow) state = 'boom';
        if (state !== 'boom') return true;
      }
      if (state === 'boom') { this.explode(); state = 'done'; this.gone = true; return false; }
      return false;
    },
    explode() {
      const x = tx, cy = GROUND - 30;
      if (ice) {
        damageArea(x, cy, t.novaRadius, { dmg: t.damage * 0.5, kx: 60, slot, elem: 'ice', freeze: 1, shake: 6, frame: true, hitstop: 0.05 });
        addFx(1.6, (ctx, layer, tb) => frostNova(ctx, G.t, layer, tb, x, t.novaRadius));
        sfx(SND.seed_nova.fn);
      } else {
        const sc = cluster ? 0.7 : 1.1;
        damageArea(x, cy, t.radius * (cluster ? 0.75 : 1), { dmg: t.damage * (cluster ? 0.6 : 1), kx: t.knockback, ky: -t.launch, heavy: true, slot, elem: el, burn: fire ? t.puddleBurn : 0, shake: cluster ? 5 : 8, frame: true, hitstop: 0.06 });
        if (fire) G.ents.push(fireZone(x, t.radius * 0.9, t.puddleTime, t.puddleBurn, slot));
        addFx(1.4, (ctx, layer, tb) => {
          const u = G.t - tb;
          lightColumn(ctx, x, GROUND, u, sc, pal, layer);
          groundImpact(ctx, G.t, layer, tb, x, sc, pal, { seed: Math.floor(x) });
          const R = rand(19);
          for (let i = 0; i < 10; i++) { const ang = -Math.PI / 2 + (R() - 0.5) * 1.3, nu = seg(u, 0, 0.28), l0 = 12 * sc, l1 = l0 + (50 + R() * 70) * sc * easeOut(nu), lay = R() < 0.4 ? 'back' : 'front'; if (lay === layer && nu < 1) needle(ctx, x + Math.cos(ang) * l0, GROUND - 10 + Math.sin(ang) * l0, x + Math.cos(ang) * l1, GROUND - 10 + Math.sin(ang) * l1, 3.4 * sc * (1 - nu * 0.6), i % 3 ? pal.light : pal.core, 1 - nu, layer); }
          cracks(ctx, G.t, tb, x, layer, { n: 6, len: 60 * sc, pal });
        });
        sfx(cluster ? SND.seed_split.fn : SND.seed_boom.fn);
        if (cluster) for (let i = 0; i < t.clusterCount; i++) {
          const R = rand(i + Math.floor(G.t * 100)), side = i % 2 ? 1 : -1, dx = side * (50 + R() * 120), kid = { x, d: 0.28 + i * 0.07 };
          G.ents.push(bomblet(slot, x, x + dx, kid.d, pal));
        }
      }
    },
    draw(ctx, layer) {
      if (state === 'fly') {
        if (layer === 'back') { const pts = trailPts(pos, G.t, 0.09, 10, t0); ribbon(ctx, pts, s => 8 * Math.pow(1 - s, 1.2), pal.dark, 0.35, layer); ribbon(ctx, pts, s => 4 * (1 - s), pal.light, 0.9, layer); }
        starSeed(ctx, G.t, this.x, this.y, G.t * 14, pal, 1, layer);
        return;
      }
      if (state !== 'armed') return;
      const blink = ticks.reduce((m, tt) => Math.max(m, G.t >= tt ? 1 - seg(G.t, tt, tt + 0.16) : 0), 0), armed = prox && G.t > armedAt + 0.12;
      if (layer === 'front') {
        for (let i = 0; i < 3; i++) needle(ctx, tx, GROUND - 2, tx + (i - 1) * 7, GROUND - 16 - (i === 1 ? 8 : 0) - blink * 8, 2.2, pal.light, 0.8, layer);
        starSeed(ctx, G.t, tx, GROUND - 5, 0.785, pal, 1, layer, 0.8 + blink * 0.35);
      }
      if (armed) groundRing(ctx, tx, GROUND, t.proxRadius, 1.4, pal.light, 0.35 + 0.15 * Math.sin(G.t * 8), layer, { taper: 0, seed: 2 });
      for (const tt of ticks) { const u = seg(G.t, tt, tt + 0.4); if (u > 0 && u < 1) groundRing(ctx, tx, GROUND, 8 + 44 * easeOut(u), 2.6 * (1 - u) + 0.4, pal.mid, 1 - u, layer, { seed: 3 }); }
      if (trigger >= 0 && layer === 'front') glow(ctx, tx, GROUND - 8, 40 * seg(G.t, trigger, trigger + 0.12), pal.g2, 1);
    },
  };
  return ent;
}
// tic de armado (sube de tono con cada pulso)
function seedTick(k, i) { const tn = nowT(); vNoise(tn, 0.02 * k, 0.25, 'bandpass', 2600 + i * 500, 2600 + i * 500, 6, { att: 0.001 }); vTone(tn, 220 * Math.pow(1.26, i), 200 * Math.pow(1.26, i), 0.12 * k, 0.12, 'sine', 0.1); }
function bomblet(slot, x0, x1, dur, pal) {
  const t = T('seed'), t0 = G.t, t1 = t0 + dur;
  const pos = tt => { const u = seg(tt, t0, t1); return { x: lerp(x0, x1, u), y: lerp(GROUND - 8, GROUND - 4, u) - 4 * 80 * u * (1 - u) }; };
  return {
    kind: 'bomblet',
    update() {
      if (G.t < t1) return true;
      damageArea(x1, GROUND - 30, t.clusterRadius, { dmg: t.clusterDamage, kx: 160, ky: -200, heavy: true, slot, shake: 3.5 });
      addFx(1.2, (ctx, layer, tb) => groundImpact(ctx, G.t, layer, tb, x1, 0.6, pal, { seed: Math.floor(x1) }));
      sfx(SND.seed_mini.fn);
      return false;
    },
    draw(ctx, layer) {
      const p = pos(G.t);
      if (layer === 'back') { const pts = trailPts(pos, G.t, 0.07, 8, t0); ribbon(ctx, pts, s => 4 * (1 - s), pal.light, 0.9, layer); }
      starSeed(ctx, G.t, p.x, p.y, G.t * 18, pal, 1, layer, 0.6);
    },
  };
}
