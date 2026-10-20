// g-acumulador.js — 14 · Acumulador, versión jugable. Ficha y decisiones: docs/armas/acumulador.md · progresión: docs/armas/progresion.md
// No se carga solo: se carga con los golpes (y las bajas) de tu OTRA arma. Clic = descarga todo lo acumulado en una nova
// eléctrica alrededor de ti. Es la pareja de cualquier arma: juegas con la otra y, cuando la batería está llena, cambias y
// sueltas. Árbol: base → Sobrecarga / Arco / Celdas → 3 mejoras. Elemento y Duplicador vienen de items.
'use strict';
const SND_VOLT = {
  charge: sound('volt_charge', 'Acumulador', 'Carga llena', k => { const t = nowT(); vTone(t, 880, 1760, 0.12 * k, 0.05, 'square', 0.2); vTone(t + 0.1 * k, 1760, 1760, 0.15 * k, 0.04, 'square', 0.25); }),
  nova: sound('volt_nova', 'Acumulador', 'Descarga (nova)', k => { const t = nowT(); vCrackle(t, 0.4 * k, 0.45, 60, 0.2, [2000, 9000]); vThump(t, 90, 35, 0.4 * k, 0.8, 3, 0.2); vNoise(t, 0.3 * k, 0.35, 'highpass', 2000, 8000, 1, { att: 0.001, wet: 0.3 }); }),
  arc: sound('volt_arc', 'Acumulador', 'Arco (salto)', k => { const t = nowT(); vCrackle(t, 0.1 * k, 0.3, 22, 0.15, [3000, 9000]); vTone(t, 1200, 600, 0.08 * k, 0.04, 'sawtooth', 0.2); }),
  sky: sound('volt_sky', 'Acumulador', 'Celdas · rayo del cielo', k => { const t = nowT(); vCrackle(t, 0.3 * k, 0.5, 50, 0.3, [2500, 9000]); SFXLIB.boom(k, 1.1); }),
  fizz: sound('volt_fizz', 'Acumulador', 'Sin carga', k => { const t = nowT(); vCrackle(t, 0.12 * k, 0.12, 8, 0.1, [2000, 5000]); }),
};
addWeapon({
  id: 'volt', name: 'Acumulador', short: 'Acumulador', pal: 'volt',
  archetypes: [
    { id: 'overload', name: 'Sobrecarga', desc: 'Al llenarse, descarga SOLA alrededor de ti (aunque lleves la otra arma en la mano).',
      ups: [{ id: 'short', name: 'Cortocircuito', desc: 'Descarga antes (con menos carga): más a menudo y más pequeña.' },
            { id: 'stun', name: 'Electrocución', desc: 'Las descargas aturden.' },
            { id: 'pulse', name: 'Pulso', desc: 'Cada descarga recarga del todo tu otra arma.' }] },
    { id: 'arc', name: 'Arco', desc: 'La descarga es un rayo en cadena: sale hacia donde apuntas y salta de enemigo en enemigo; más carga = más saltos.',
      ups: [{ id: 'long', name: 'Cadena larga', desc: 'Más saltos y más lejos.' },
            { id: 'fork', name: 'Bifurcación', desc: 'Cada salto se divide en dos.' },
            { id: 'refund', name: 'Retorno', desc: 'Cada baja del arco devuelve carga.' }] },
    { id: 'cells', name: 'Celdas', desc: 'Guarda hasta 3 celdas de carga; cada clic gasta UNA en un rayo del cielo donde apuntas (golpe fuerte).',
      ups: [{ id: 'salvo', name: 'Tormenta', desc: 'Mantén para soltar todas las celdas seguidas sobre los enemigos más cercanos al ratón.' },
            { id: 'field', name: 'Suelo cargado', desc: 'Donde cae el rayo, el suelo queda electrificado un rato.' },
            { id: 'trickle', name: 'Goteo', desc: 'Las celdas también se cargan solas, despacio.' }] },
  ],
  tuning: {
    maxCharge: [100, 20, 300, 5, 'Base: carga máxima'], perHit: [2, 0, 20, 0.5, 'Base: carga por golpe de la otra arma'], perDmg: [0.12, 0, 1, 0.01, 'Base: carga por punto de daño'],
    perKill: [8, 0, 50, 1, 'Base: carga por baja con la otra arma'], minCharge: [10, 0, 100, 1, 'Base: carga mínima para descargar'], cooldown: [0.4, 0, 3, 0.05, 'Base: recarga tras descargar (s)'],
    novaRadius: [200, 50, 500, 5, 'Base: radio de la nova'], novaBase: [10, 0, 100, 1, 'Base: daño fijo'], novaPer: [0.9, 0, 5, 0.05, 'Base: + daño por punto de carga'],
    iceFreeze: [0.5, 0, 1, 0.05, 'Hielo: congelación (carga llena)'], fireBurn: [2.5, 0, 6, 0.1, 'Fuego: quemadura (s)'],
    shortAt: [0.6, 0.2, 1, 0.05, 'Sobrecarga · Cortocircuito: descarga a esta fracción'], stunTime: [0.9, 0, 3, 0.1, 'Sobrecarga · Electrocución: aturdido (s)'],
    arcJumps: [2, 0, 10, 1, 'Arco: saltos base'], arcPerJump: [25, 5, 100, 1, 'Arco: carga por salto extra'], arcRange: [200, 50, 500, 10, 'Arco: distancia de salto'],
    arcBase: [12, 0, 100, 1, 'Arco: daño fijo por golpe'], arcPer: [0.7, 0, 5, 0.05, 'Arco: + daño por punto de carga'], arcFalloff: [0.85, 0.3, 1, 0.01, 'Arco: × daño en cada salto'],
    longJumps: [3, 0, 10, 1, 'Arco · Cadena larga: saltos extra'], longRange: [1.4, 1, 3, 0.05, 'Arco · Cadena larga: × distancia'], refundKill: [20, 0, 100, 1, 'Arco · Retorno: carga por baja'],
    cellMax: [300, 100, 600, 50, 'Celdas: carga máxima (3 celdas)'], cellDamage: [75, 1, 300, 1, 'Celdas: daño del rayo'], cellWidth: [44, 10, 150, 2, 'Celdas: ancho del rayo'],
    salvoGap: [0.15, 0.03, 0.6, 0.01, 'Celdas · Tormenta: s entre rayos'], fieldTime: [2.5, 0.3, 8, 0.1, 'Celdas · Suelo cargado: duración (s)'], fieldDps: [14, 0, 80, 1, 'Celdas · Suelo cargado: daño/s'],
    trickle: [6, 0, 40, 0.5, 'Celdas · Goteo: carga por segundo'],
  },
  create(slot) {
    const id = 'volt';
    let cd = 0, charge = 0, fullSaid = false, salvo = null;
    const max = () => slot.arch === 'cells' ? T(id).cellMax : T(id).maxCharge;
    const pal = () => elemPal('volt');
    const add = n => { if (n <= 0) return; charge = Math.min(max(), charge + n); if (charge >= max() && !fullSaid) { fullSaid = true; sfx(SND_VOLT.charge.fn); text(G.player.x, G.player.y - 110, 'CARGADO', '#fff06a', true); } };
    const el = () => elemOf();
    const hitOpts = (k, extra = {}) => Object.assign({ slot, elem: el(), freeze: el() === 'ice' ? T(id).iceFreeze * k : 0, burn: el() === 'fire' ? T(id).fireBurn : 0 }, extra);
    // nova alrededor del player con toda la carga (o la que se diga)
    const nova = (c, auto) => {
      const t = T(id), p = G.player, x = p.x, y = p.y - 40, k = clamp(c / t.maxCharge), dmg = t.novaBase + c * t.novaPer, P = pal(), stun = slot.arch === 'overload' && hasMod(slot, 'stun');
      const hit = damageArea(x, y, t.novaRadius, hitOpts(k, { dmg, kx: 220 * (0.4 + k), heavy: k >= 0.8, shake: 3 + 7 * k, hitstop: 0.02 + 0.05 * k, frame: k >= 0.8 }), false);
      if (stun) for (const e of hit) e.stunT = Math.max(e.stunT, t.stunTime);
      const tg = hit.map(e => ({ x: e.x, y: e.y }));
      addFx(0.6, (ctx, layer, f0) => { shockRing(ctx, G.t, layer, f0, x, y, t.novaRadius, P); for (const q of tg) rodBolt(ctx, G.t, layer, f0, x, y, q.x, q.y, P, 2 + 2 * k); if (layer === 'front') glow(ctx, x, y, 90 * (0.6 + k), P.g1, 0.7 * (1 - seg(G.t, f0, f0 + 0.3))); });
      sfx(SND_VOLT.nova.fn);
      if (copies() > 1) { const t0 = G.t; G.ents.push({ kind: 'timer', update() { if (G.t - t0 < 0.15) return true; damageArea(G.player.x, G.player.y - 40, t.novaRadius * 0.7, hitOpts(k, { dmg: dmg * 0.5, kx: 120, proc: true })); addFx(0.5, (ctx, layer, f0) => shockRing(ctx, G.t, layer, f0, G.player.x, G.player.y - 40, t.novaRadius * 0.7, P)); return false; }, draw() {} }); }
      if (slot.arch === 'overload' && hasMod(slot, 'pulse')) for (const so of otherSlots(slot)) if (so.inst && so.inst.refund) so.inst.refund(99);
      if (auto) text(x, y - 70, 'SOBRECARGA', '#fff06a', true);
    };
    // rayo en cadena desde el cristal
    const arc = (aim, c) => {
      const t = T(id), long = hasMod(slot, 'long'), fork = hasMod(slot, 'fork'), P = pal();
      const jumps = t.arcJumps + Math.floor(c / t.arcPerJump) + (long ? t.longJumps : 0), range = t.arcRange * (long ? t.longRange : 1);
      const first = nearestEnemy(aim.tx, aim.ty, 260) || nearestEnemy(aim.x, aim.y, range * 1.5);
      if (!first) { sfx(SND_VOLT.fizz.fn); return; }
      const seen = new Set(), segs = [];
      let front = [{ e: first, from: { x: aim.x, y: aim.y }, dmg: t.arcBase + c * t.arcPer }], n = 0;
      while (front.length && n <= jumps) {
        const next = [];
        for (const f of front) {
          if (seen.has(f.e)) continue; seen.add(f.e); n++;
          segs.push({ x0: f.from.x, y0: f.from.y, x1: f.e.x, y1: f.e.y });
          damage(f.e, hitOpts(clamp(c / t.maxCharge), { dmg: f.dmg, kx: Math.sign(f.e.x - f.from.x) * 90, heavy: n === 1 && c >= t.maxCharge * 0.8, hitstop: 0.015, onKill: hasMod(slot, 'refund') ? () => add(t.refundKill) : null }));
          const ways = fork ? 2 : 1;
          for (let w = 0; w < ways; w++) { const nx = nearestEnemy(f.e.x, f.e.y, range, new Set([...seen, ...next.map(q => q.e)])); if (nx) next.push({ e: nx, from: { x: f.e.x, y: f.e.y }, dmg: f.dmg * t.arcFalloff }); }
        }
        front = next;
      }
      addFx(0.4, (ctx, layer, f0) => { for (const s of segs) rodBolt(ctx, G.t, layer, f0, s.x0, s.y0, s.x1, s.y1, P, 2.6); });
      sfx(SND_VOLT.arc.fn);
    };
    // rayo del cielo (Celdas)
    const sky = x => {
      const t = T(id), P = pal(), w = t.cellWidth;
      for (const e of alive()) if (Math.abs(e.x - x) < w / 2 + e.rx) {
        damage(e, hitOpts(1, { dmg: t.cellDamage, heavy: true, kx: Math.sign(e.x - x) * 80, ky: e.fly ? 0 : -150, shake: 8, hitstop: 0.06, frame: true }));
        if (e.fly && !e.dead) { e.slammed = true; e.vy = 500; }
      }
      if (hasMod(slot, 'field')) G.ents.push(voltField(slot, x));
      addFx(1.2, (ctx, layer, f0) => skyBolt(ctx, G.t, layer, f0, x, P));
      sfx(SND_VOLT.sky.fn);
    };
    const inst = {
      charge: () => charge,
      onPress(aim) {
        const t = T(id);
        if (cd > 0) return;
        if (slot.arch === 'cells') {
          if (charge < 100) { sfx(SND_VOLT.fizz.fn); return; }
          if (hasMod(slot, 'salvo')) { salvo = { t0: G.t, last: -9 }; return; }   // se suelta al mantener (ver onHold)
          charge -= 100; fullSaid = false; cd = t.cooldown; sky(aim.tx); if (copies() > 1) sky(aim.tx + t.cellWidth * 1.2);
          return;
        }
        if (charge < t.minCharge) { sfx(SND_VOLT.fizz.fn); text(G.player.x, G.player.y - 100, 'sin carga', '#9aa3b2'); return; }
        const c = charge; charge = 0; fullSaid = false; cd = t.cooldown * cdMul(); G.player.castT = 0.3;
        if (slot.arch === 'arc') { arc(aim, c); if (copies() > 1) arc({ ...aim, tx: aim.tx + 60 }, c * 0.6); }
        else nova(c, false);
      },
      onHold(aim, dt) {
        if (!salvo) return;
        const t = T(id);
        if (charge >= 100 && G.t - salvo.last >= t.salvoGap) {
          salvo.last = G.t; charge -= 100; fullSaid = false;
          const tg = alive().filter(e => !e._skyT || G.t - e._skyT > 0.5).sort((a, b) => Math.abs(a.x - aim.tx) - Math.abs(b.x - aim.tx))[0];
          const x = tg ? tg.x : aim.tx + (Math.random() - 0.5) * 120; if (tg) tg._skyT = G.t;
          sky(x);
        }
      },
      onRelease(aim) {
        if (!salvo) return;
        const s = salvo; salvo = null;
        if (G.t - s.last > 1 && charge >= 100) { charge -= 100; fullSaid = false; sky(aim.tx); }   // toque corto: una celda donde apuntas
        cd = T(id).cooldown;
      },
      update(dt, current) {
        const t = T(id);
        cd -= dt; if (salvo && !current) salvo = null;
        if (slot.arch === 'cells' && hasMod(slot, 'trickle')) add(t.trickle * dt);
        if (slot.arch === 'overload' && charge >= max() * (hasMod(slot, 'short') ? t.shortAt : 1)) { const c = charge; charge = 0; fullSaid = false; nova(c, true); }
      },
      onAnyHit(e, o, dmg) { if (o.slot === slot) return; const t = T(id); add(t.perHit + dmg * t.perDmg); },
      onAnyKill(e, o) { if (o.slot && o.slot !== slot) add(T(id).perKill); },
      ready() { return clamp(charge / max()); },
      refund(s) { cd -= s; },
    };
    G.ents.push(voltAuraEnt(slot, inst));
    return inst;
  },
});

// aura de carga alrededor del player (vive mientras el arma esté equipada)
function voltAuraEnt(slot, inst) {
  return { kind: 'fx', update() { return slot.inst === inst && G.slots.slice(0, G.slotCount).includes(slot); },
    draw(ctx, layer) { const T0 = T('volt'), mx = slot.arch === 'cells' ? T0.cellMax : T0.maxCharge, c = inst.charge(); voltAura(ctx, G.t, layer, G.player.x, G.player.y - 40, slot.arch === 'cells' ? clamp((c % 100) / 100) : clamp(c / mx), elemPal('volt'), slot.arch === 'cells' ? Math.floor(c / 100) : 0); } };
}
// Celdas · Suelo cargado
function voltField(slot, x) {
  const t = T('volt'), t0 = G.t, pal = elemPal('volt');
  let tick = 0;
  return { kind: 'zone', x, y: GROUND - 10, halfW: 60,
    update(dt) { tick -= dt; if (tick <= 0) { tick = 0.25; for (const e of alive()) if (Math.abs(e.x - x) < 60 && GROUND - (e.y + e.ry) < 30) damage(e, { dmg: t.fieldDps * 0.25, slot, silentFlash: true, color: '#fff06a' }); } return G.t - t0 < t.fieldTime; },
    draw(ctx, layer) { const a = 1 - seg(G.t, t0 + t.fieldTime - 0.4, t0 + t.fieldTime); groundRing(ctx, x, GROUND + 2, 60, 2, pal.mid, 0.7 * a, layer, { taper: 0, wob: 0.15, seed: Math.floor(G.t * 10) });
      if (layer === 'front') { const R = rand(Math.floor(G.t * 16) + Math.floor(x)); for (let i = 0; i < 3; i++) { const px = x + (R() - 0.5) * 110; bolt(ctx, px, GROUND, px + (R() - 0.5) * 20, GROUND - 14 - R() * 20, 3, R, 1.2, pal, a, layer, 0.5); } } } };
}
