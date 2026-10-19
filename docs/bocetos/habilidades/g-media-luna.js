// g-media-luna.js — 4 · Media luna, versión jugable. Ficha y decisiones: docs/armas/media-luna.md · progresión: docs/armas/progresion.md
// Bumerán de una sola hoja: mientras está fuera no se puede volver a lanzar. Golpea a la ida y a la vuelta.
// Árbol: base → evolución (Sierra / Satélite / Malabar) → 3 mejoras de esa evolución. Elemento y Duplicador vienen de items.
'use strict';
addWeapon({
  id: 'moon', name: 'Media luna', short: 'Media luna', pal: 'set',
  archetypes: [
    { id: 'saw', name: 'Sierra', desc: 'Se queda girando donde toca al primer enemigo (o en el extremo) y lo tritura; vuelve al pulsar otra vez o sola al rato.',
      ups: [{ id: 'yank', name: 'Tirón', desc: 'A la vuelta arrastra hacia ti a los que corta.' },
            { id: 'rend', name: 'Desgarro', desc: 'Cada corte seguido al mismo enemigo hace más daño.' },
            { id: 'magnet', name: 'Imán', desc: 'Mientras tritura, atrae hacia la sierra a los enemigos cercanos.' }] },
    { id: 'orbit', name: 'Satélite', desc: 'La hoja vive girando a tu alrededor y corta lo que se acerca; al lanzarla vuelve a la órbita, sin recarga.',
      ups: [{ id: 'twin', name: 'Doble', desc: 'Dos hojas en órbita; cada clic lanza una.' },
            { id: 'wax', name: 'Luna creciente', desc: 'Cada vuelta en órbita sin lanzarla la hace crecer y suma daño al próximo lanzamiento.' },
            { id: 'guard', name: 'Escudo', desc: 'Las hojas en órbita destruyen las balas enemigas que tocan.' }] },
    { id: 'juggle', name: 'Malabar', desc: 'Atrapada perfecta: pulsa justo cuando vuelve y sale otra vez al instante, más rápida y fuerte (racha de hasta 3). Si la dejas llegar, la racha se pierde.',
      ups: [{ id: 'edge', name: 'Filo creciente', desc: 'Con cada punto de racha la hoja es más grande y corta más lejos.' },
            { id: 'echo', name: 'Eco', desc: 'Cada atrapada perfecta deja una hoja fantasma que repite el lanzamiento anterior.' },
            { id: 'finale', name: 'Gran final', desc: 'Con la racha llena, la siguiente atrapada perfecta lanza una hoja enorme (golpe fuerte) y la racha vuelve a empezar.' }] },
  ],
  // [valor, mín, máx, paso, etiqueta] — nombres = futuros campos en Unity
  tuning: {
    damage: [18, 1, 80, 1, 'Base: daño por pasada'], range: [360, 100, 700, 10, 'Base: alcance (px)'], outTime: [0.55, 0.15, 1.5, 0.05, 'Base: ida (s)'],
    hoverTime: [0.14, 0, 1, 0.02, 'Base: pausa en el extremo (s)'], backTime: [0.48, 0.15, 1.5, 0.05, 'Base: vuelta (s)'], knockback: [130, 0, 500, 5, 'Base: empuje'],
    hitRadius: [22, 6, 60, 1, 'Base: radio de golpe'], cooldown: [0.15, 0, 2, 0.05, 'Base: recarga tras atraparla (s)'],
    iceFreeze: [0.5, 0, 1, 0.05, 'Hielo: congelación en la ida'], fireLife: [0.75, 0.2, 2, 0.05, 'Fuego: duración de la estela (s)'], fireBurn: [2.0, 0, 6, 0.1, 'Fuego: quemadura (s)'],
    sawTime: [2.5, 0.3, 8, 0.1, 'Sierra: tiempo triturando si no la llamas (s)'], sawTick: [0.15, 0.05, 0.5, 0.01, 'Sierra: cada cuánto corta (s)'], sawDamage: [5, 1, 40, 1, 'Sierra: daño por corte'],
    yankSpeed: [420, 50, 1000, 10, 'Sierra · Tirón: velocidad del arrastre'], rendStep: [0.15, 0, 1, 0.05, 'Sierra · Desgarro: + daño por corte seguido'], rendMax: [1.5, 0, 5, 0.1, 'Sierra · Desgarro: + daño máximo'],
    magnetRadius: [170, 30, 300, 5, 'Sierra · Imán: radio'], magnetSpeed: [110, 20, 400, 5, 'Sierra · Imán: fuerza'],
    satRadius: [70, 30, 160, 2, 'Satélite: radio de la órbita'], satLap: [0.9, 0.3, 3, 0.05, 'Satélite: tiempo por vuelta (s)'], satMul: [0.6, 0.1, 2, 0.05, 'Satélite: daño en órbita (×)'],
    waxStep: [0.25, 0, 1, 0.05, 'Satélite · Luna creciente: + daño por vuelta'], waxMax: [1.5, 0, 5, 0.1, 'Satélite · Luna creciente: + daño máximo'],
    perfectWindow: [0.2, 0.05, 0.6, 0.01, 'Malabar: ventana antes de llegar (s)'], perfectLate: [0.1, 0, 0.4, 0.01, 'Malabar: margen después de atraparla (s)'],
    rachaMax: [3, 1, 6, 1, 'Malabar: racha máxima'], jugDmg: [0.35, 0, 1, 0.05, 'Malabar: + daño por punto de racha'], jugSpeed: [0.12, 0, 0.25, 0.01, 'Malabar: − tiempo de vuelo por punto de racha'],
    edgeGrow: [0.25, 0, 1, 0.05, 'Malabar · Filo creciente: + tamaño por punto de racha'], echoMul: [0.6, 0.1, 2, 0.05, 'Malabar · Eco: daño de la hoja fantasma (×)'],
    finaleMul: [3, 1, 8, 0.1, 'Malabar · Gran final: × daño'], finaleSize: [2.2, 1, 4, 0.1, 'Malabar · Gran final: tamaño'],
  },
  create(slot) {
    const id = 'moon', inst = {};
    let cd = 0, out = [], sats = [], racha = 0, caughtAt = -9, lastAim = null;
    const throwAim = (aim, o = {}) => {
      G.player.castT = 0.2;
      const n = copies(); out = [];
      for (let c = 0; c < n; c++) out.push(moonBlade(slot, inst, aim, Object.assign({ mirror: c > 0, onDone }, o)));
      out.forEach(b => G.ents.push(b)); sfx(SND.moon_throw.fn);
      lastAim = { x: aim.x, y: aim.y, dx: aim.dx, dy: aim.dy };
    };
    const onDone = () => { out = out.filter(b => !b.done); if (!out.length) { cd = T(id).cooldown * cdMul(); caughtAt = G.t; } };
    const jugStats = () => { const t = T(id); return { dmgMul: 1 + t.jugDmg * racha, speed: 1 - t.jugSpeed * racha, size: 1 + (hasMod(slot, 'edge') ? t.edgeGrow * racha : 0) }; };
    return Object.assign(inst, {
      onPress(aim) {
        const t = T(id), a = slot.arch;
        if (a === 'saw' && out.length) { out.forEach(b => b.recall()); return; }
        if (a === 'orbit') { const s = sats.find(b => b.phase === 'orbit'); if (s) { s.launch(aim); G.player.castT = 0.2; sfx(SND.moon_throw.fn); } return; }
        if (a === 'juggle') {
          const inWin = out.length && out.every(b => b.phase === 'back' && b.remain() < t.perfectWindow);
          const late = !out.length && G.t - caughtAt < t.perfectLate;
          if (inWin || late) {                               // atrapada perfecta: sale otra vez al instante
            out.forEach(b => b.catchNow()); out = []; caughtAt = -9;
            const finale = hasMod(slot, 'finale') && racha >= t.rachaMax;
            racha = finale ? 0 : Math.min(t.rachaMax, racha + 1);
            text(G.player.x, G.player.y - 110, finale ? 'GRAN FINAL' : 'RACHA ' + racha, '#ffe39a', true);
            sfx(k => { SND.moon_catch.fn(k); SFXLIB.glass(k, 0.6 + racha * 0.2); });
            if (hasMod(slot, 'echo') && lastAim) G.ents.push(moonBlade(slot, inst, lastAim, Object.assign(jugStats(), { ghost: true, dmgMul: jugStats().dmgMul * t.echoMul })));
            throwAim(aim, finale ? { dmgMul: t.finaleMul, size: t.finaleSize, giant: true } : jugStats());
            return;
          }
          if (cd > 0 || out.length) return;
          racha = 0; throwAim(aim, jugStats()); return;
        }
        if (cd > 0 || out.length) return;
        throwAim(aim);
      },
      update(dt) {
        const t = T(id);
        cd -= dt;
        if (slot.arch === 'orbit') {                           // Satélite: las hojas viven en órbita (también con la otra arma en la mano)
          const want = (hasMod(slot, 'twin') ? 2 : 1) * copies();
          sats = sats.filter(b => !b.dead && G.ents.includes(b));
          if (sats.length !== want) { sats.forEach(b => b.kill()); sats = []; for (let i = 0; i < want; i++) { const b = moonBlade(slot, inst, aimInfo(), { sat: true, satIdx: i, satN: want }); sats.push(b); G.ents.push(b); } }
        } else if (sats.length) { sats.forEach(b => b.kill()); sats = []; }
        if (slot.arch === 'juggle' && racha > 0 && !out.length && G.t - caughtAt > t.perfectLate) { racha = 0; text(G.player.x, G.player.y - 110, 'racha perdida', '#9aa3b5'); }
      },
      ready() {
        if (slot.arch === 'orbit') return sats.length ? sats.filter(b => b.phase === 'orbit').length / sats.length : 1;
        return out.length ? 0 : 1 - cd / Math.max(0.01, T(id).cooldown);
      },
      refund(s) { cd -= s; },
    });
  },
});

// Hoja. Fases: out (ida) → hover (pausa en el extremo) → back (vuelve) → atrapada; Sierra: saw (tritura) antes de volver;
// Satélite: orbit (gira alrededor del player) y vuelve a la órbita en vez de atraparse.
// o = { mirror (copia del Duplicador), onDone, sat, satIdx, satN, ghost (Eco), dmgMul, speed (× tiempos), size, giant }
function moonBlade(slot, owner, aim, o) {
  const t = T('moon'), el = elemOf(), fire = el === 'fire', ice = el === 'ice', arch = slot.arch, R0 = 21;
  const saw = arch === 'saw' && !o.ghost, sat = !!o.sat;
  const basePal = elemPal(arch === 'juggle' || arch === 'orbit' && hasMod(slot, 'twin') ? 'gold' : 'set');
  const sp = o.speed || 1, D = { out: t.outTime * sp, hover: t.hoverTime * sp, back: t.backTime * sp };
  let phase = sat ? 'orbit' : 'out', pt = G.t, ox = aim.x, oy = aim.y, dir = { x: aim.dx, y: aim.dy }, perp = { x: dir.y, y: -dir.x };
  let up = (perp.y > 0 ? -1 : 1) * (o.mirror ? -3 : 1), apex = null, from = null, last = { x: ox, y: oy }, stopSpin = null;
  let hits = new Set(), lap = -1, wax = 0, dmgMul = o.dmgMul || 1, recalled = false, sawE = null, sawOff = { x: 0, y: 0 }, sawPos = null, sawT = 0;
  const yanked = new Set(), rend = new Map(), hist = [];
  const spin = () => { if (stopSpin || o.mirror || o.ghost) return; stopSpin = loopSound(out => { const n = AC.createBufferSource(); n.buffer = noiseBuf; n.loop = true; const f = AC.createBiquadFilter(); f.type = 'bandpass'; f.frequency.value = 1200; f.Q.value = 2.2; const g = AC.createGain(); g.gain.value = 0.14; const l = AC.createOscillator(), lg = AC.createGain(); l.frequency.value = 15; lg.gain.value = 0.1; l.connect(lg); lg.connect(g.gain); n.connect(f); f.connect(g); g.connect(out); n.start(); l.start(); return [n, l]; }); };
  const unspin = () => { if (stopSpin) { stopSpin(); stopSpin = null; } };
  const orbitPos = () => {
    const p = G.player, a = G.t * TAU / t.satLap + (o.satIdx || 0) * TAU / (o.satN || 1);
    return { x: p.x + Math.cos(a) * t.satRadius, y: p.y - 34 + Math.sin(a) * t.satRadius * 0.32, depth: Math.sin(a), a };
  };
  const go = ph => { phase = ph; pt = G.t; hits = new Set(); };
  const size = () => (o.size || 1) * (1 + wax * 0.25);
  const pal = () => wax > 0 || o.giant ? elemPal('gold') : basePal;
  const b = {
    kind: 'blade', done: false, dead: false,
    get phase() { return phase; },
    remain() { return phase === 'back' ? pt + D.back - G.t : 9; },
    recall() { if (phase === 'saw' || phase === 'out' || phase === 'hover') recalled = true; },
    launch(aim2) {                                          // Satélite: sale desde donde está hacia donde apuntas
      if (phase !== 'orbit') return;
      ox = this.x; oy = this.y; dir = { x: aim2.dx, y: aim2.dy }; perp = { x: dir.y, y: -dir.x }; up = perp.y > 0 ? -1 : 1;
      dmgMul = 1 + wax; apex = null; go('out'); spin();
    },
    catchNow() { this.done = true; this.dead = true; unspin(); catchFx(pal()); },
    kill() { this.dead = true; unspin(); },
    hit(e, mul, heavy) {
      const dx = this.x - last.x, k = Math.sign(dx) || Math.sign(e.x - G.player.x), back = phase === 'back';
      damage(e, { dmg: t.damage * mul, kx: k * t.knockback * (o.giant ? 2 : 1), slot, elem: el, burn: fire ? t.fireBurn : 0, freeze: ice && !back ? t.iceFreeze : 0,
        heavy: heavy || o.giant || (ice && back), shake: o.giant ? 6 : 3, hitstop: o.giant ? 0.06 : 0.03, frame: o.giant });
      slashFx(e.x, e.y, dx < 0, pal(), size());
      if (!o.mirror && !o.ghost) sfx(SND.moon_hit.fn);
    },
    update(dt) {
      if (this.dead) return false;
      if (sat && (slot.inst !== owner || G.slots.indexOf(slot) >= G.slotCount)) { this.kill(); return false; }
      const p = this.pos(dt); if (!p) return false;
      p.y = Math.min(p.y, GROUND - 12); this.x = p.x; this.y = p.y; this.depth = p.depth;
      hist.unshift({ x: p.x, y: p.y, t: G.t, r: this.rot() }); if (hist.length > 40) hist.pop();
      const r = t.hitRadius * size();
      if (phase === 'saw') this.grind(dt, r);
      else for (const e of alive()) {                        // golpes: una vez por pasada (en órbita, una por vuelta)
        if (hits.has(e)) continue;
        if (Math.pow((p.x - e.x) / (e.rx + r), 2) + Math.pow((p.y - e.y) / (e.ry + r * 0.6), 2) >= 1) continue;
        hits.add(e);
        this.hit(e, phase === 'orbit' ? t.satMul : dmgMul, false);
        if (phase === 'back' && arch === 'saw' && hasMod(slot, 'yank')) yanked.add(e);
        if (saw && phase === 'out') { sawE = e; sawOff = { x: (p.x - e.x) * 0.5, y: (p.y - e.y) * 0.5 }; go('saw'); break; }   // Sierra: se queda en el primero
      }
      if (phase === 'back' && yanked.size) {                 // Sierra · Tirón: los cortados a la vuelta vienen detrás de la hoja
        for (const e of yanked) if (!e.dead && Math.abs(e.x - G.player.x) > 70) {
          e.pull = { x: this.x, y: this.y, s: t.yankSpeed, lift: e.fly };
          e.vpull = { x: G.player.x + Math.sign(e.x - G.player.x) * 70, y: e.y, until: G.t + 0.45 };   // sigue un poco tras atraparla
        }
      }
      if (phase === 'orbit' && hasMod(slot, 'guard')) {      // Satélite · Escudo: rompe las balas enemigas
        for (const s of G.ents) if (s.kind === 'enemyShot' && !s.captured && !s.friendly && s.life > 0 && Math.hypot(s.x - p.x, s.y - p.y) < r + 6) {
          s.life = 0; const sx = s.x, sy = s.y, P = pal(); addFx(0.4, (ctx, layer, f0) => sparks(ctx, G.t, f0, sx, sy, layer, { n: 8, pal: P, sp: 220, seed: Math.floor(sx), len: 10, life: 0.3 }));
          sfx(k => SFXLIB.glass(k, 0.5));
        }
      }
      // estela de fuego (en órbita y triturando, más espaciada)
      const fireGap = phase === 'orbit' ? 0.07 : phase === 'saw' ? 0.05 : 0.028;
      if (fire && (!this.lastFire || G.t - this.lastFire > fireGap)) { this.lastFire = G.t; G.ents.push(fireBit(p.x, p.y, t.fireLife, t.fireBurn, slot)); }
      last = p;
      return !this.dead;
    },
    // posición según la fase (y cambios de fase); null = se acabó
    pos(dt) {
      if (phase === 'out') {
        const u = seg(G.t, pt, pt + D.out), d = t.range * easeOut(u), a = 14 * Math.sin(Math.PI * u) * up;
        const q = { x: ox + dir.x * d + perp.x * a, y: oy + dir.y * d + perp.y * a };
        if (u >= 1 || recalled) { apex = q; if (saw && !recalled) { sawPos = q; go('saw'); } else if (recalled) { from = q; go('back'); } else go('hover'); }
        return q;
      }
      if (phase === 'hover') {
        if (G.t - pt >= D.hover || recalled) { from = apex; go('back'); }
        return { x: apex.x + Math.sin((G.t - pt) * 12) * 2, y: apex.y + Math.sin((G.t - pt) * 9) * 2 };
      }
      if (phase === 'saw') {
        const q = sawE && !sawE.dead ? { x: sawE.x + sawOff.x, y: sawE.y + sawOff.y } : sawPos || { x: this.x, y: this.y };
        if (sawE && sawE.dead) { sawPos = q; sawE = null; }
        if (G.t - pt >= t.sawTime || recalled) { from = q; go('back'); if (!o.mirror) sfx(SND.moon_throw.fn); }
        return q;
      }
      if (phase === 'back') {
        const u = easeIn(seg(G.t, pt, pt + D.back)), tg = sat ? orbitPos() : o.ghost ? { x: ox, y: oy } : muzzle(), a = 6 * Math.sin(Math.PI * u) * up;
        if (u >= 1) return this.arrive(tg);
        return { x: lerp(from.x, tg.x, u) + perp.x * a, y: lerp(from.y, tg.y, u) + perp.y * a };
      }
      // órbita
      const q = orbitPos(), l = Math.floor(G.t / t.satLap);
      if (l !== lap) { if (lap >= 0) { hits = new Set(); if (hasMod(slot, 'wax') && wax < t.waxMax) wax = Math.min(t.waxMax, wax + t.waxStep); } lap = l; }
      return q;
    },
    arrive(tg) {
      if (sat) { go('orbit'); lap = Math.floor(G.t / t.satLap); wax = 0; dmgMul = 1; recalled = false; unspin(); if (!o.mirror) sfx(SND.moon_catch.fn); return orbitPos(); }
      this.done = true; this.dead = true; unspin();
      if (o.ghost) return null;
      if (o.onDone) o.onDone();
      if (!o.mirror) { sfx(SND.moon_catch.fn); catchFx(pal()); }
      return null;
    },
    grind(dt, r) {                                          // Sierra: corta cada sawTick a todo lo que toca
      if (hasMod(slot, 'magnet')) for (const e of alive()) if (e !== sawE && Math.hypot(e.x - this.x, e.y - this.y) < t.magnetRadius) e.pull = { x: this.x, y: this.y, s: t.magnetSpeed };
      sawT -= dt; if (sawT > 0) return;
      sawT = t.sawTick;
      const now = new Set();
      for (const e of alive()) {
        if (Math.pow((this.x - e.x) / (e.rx + r + 6), 2) + Math.pow((this.y - e.y) / (e.ry + r * 0.6 + 6), 2) >= 1) continue;
        now.add(e);
        const n = hasMod(slot, 'rend') ? (rend.get(e) || 0) + 1 : 0; rend.set(e, n);
        const mul = 1 + Math.min(t.rendMax, Math.max(0, n - 1) * t.rendStep);
        damage(e, { dmg: t.sawDamage * mul, kx: 0, slot, elem: el, freeze: ice ? t.iceFreeze * 0.25 : 0, burn: fire ? t.fireBurn : 0, silentFlash: true });
        const sx = this.x, sy = this.y, P = pal(), sd = Math.floor(G.t * 1000);
        addFx(0.4, (ctx, layer, f0) => sparks(ctx, G.t, f0, sx, sy, layer, { n: 4, pal: P, sp: 260, seed: sd, len: 12, life: 0.3 }));
      }
      for (const e of [...rend.keys()]) if (!now.has(e)) rend.delete(e);   // Desgarro: sólo cortes seguidos
      if (now.size && !o.mirror && (!this._sfx || G.t - this._sfx > 0.3)) { this._sfx = G.t; sfx(k => SFXLIB.whoosh(k, 3200, 1600, 0.08, 0.1)); }
    },
    rot() { return (G.t - pt) * (phase === 'saw' ? 46 : 29) + (o.satIdx || 0) * 1.3 + (o.mirror ? 2 : 0); },
    draw(ctx, layer) {
      if (this.x === undefined) return;
      const behind = phase === 'orbit' || (sat && this.depth !== undefined) ? this.depth < 0 : false, lay = behind ? 'back' : 'front', rot = this.rot();
      const R = R0 * size(), P = pal(), ga = o.ghost ? 0.45 : 1;
      if (layer === 'back') for (const sgn of [-1, 1]) {      // estelas en espiral de las dos puntas
        const pts = hist.filter(h => G.t - h.t <= 0.075).map(h => ({ x: h.x + Math.cos(h.r + sgn * 1.9) * R, y: h.y + Math.sin(h.r + sgn * 1.9) * R }));
        if (pts.length > 1) { ribbon(ctx, pts, s => 6 * Math.pow(1 - s, 1.3), P.dark, 0.35 * ga, layer); ribbon(ctx, pts, s => 3.2 * (1 - s), P.mid, 0.85 * ga, layer); ribbon(ctx, pts, s => 1.3 * (1 - s), P.core, ga, layer); }
      }
      if (layer !== lay) return;
      for (const k of [3, 2, 1]) { const h = hist[k * 2]; if (h) crescentBlade(ctx, h.x, h.y, R * (1 - k * 0.06), h.r, P, (0.18 / k + 0.06) * ga, layer); }
      for (const k of [1, 2]) crescentBlade(ctx, this.x, this.y, R, rot - k * 0.32, P, 0.22 / k * ga, layer);
      crescentBlade(ctx, this.x, this.y, R, rot, P, ga, layer);
      if (layer === 'front' && phase === 'saw') glow(ctx, this.x, this.y, R * 2.6, P.g2, 0.35 + 0.15 * Math.sin(G.t * 50));
      if (layer === 'front' && arch === 'juggle' && !o.ghost && !o.mirror && this.remain() < t.perfectWindow) {   // Malabar: ¡ahora!
        const m = muzzle(), pu = 0.6 + 0.4 * Math.sin(G.t * 45), gp = PAL.gold;
        glow(ctx, this.x, this.y, R * 2.4, gp.g2, 0.7 * pu); glow(ctx, m.x, m.y, 36, gp.g1, 0.6 * pu); star4(ctx, m.x, m.y, 22 * pu, G.t * 6, gp.core, 1, layer);
      }
    },
  };
  if (!sat) spin();
  return b;
}
// destello en el cristal al atrapar la hoja
function catchFx(pal) {
  addFx(0.25, (ctx, layer, tc) => { const m = muzzle(), u = seg(G.t, tc, tc + 0.22); if (layer === 'front') { glow(ctx, m.x, m.y, 34, pal.g2, 0.8 * (1 - u)); star4(ctx, m.x, m.y, 20 * (1 + u * 0.4), 0.6, pal.core, 1 - u, layer); ellArc(ctx, m.x, m.y, 10 + 24 * easeOut(u), 10 + 24 * easeOut(u), 0, TAU, 2, pal.light, 1 - u, layer, { taper: 0 }); } });
}
// tajo diagonal que se abre y se afina + chispas
function slashFx(hx, hy, back, P, sc = 1) {
  addFx(0.5, (ctx, layer, th) => {
    const u = seg(G.t, th, th + 0.22), len = 70 * sc * easeOut(seg(u, 0, 0.35)), a = 1 - seg(u, 0.4, 1), ang = (back ? 1 : -1) * 0.55;
    if (layer === 'front' && u < 1) {
      needle(ctx, hx - Math.cos(ang) * len / 2, hy - Math.sin(ang) * len / 2, hx + Math.cos(ang) * len / 2, hy + Math.sin(ang) * len / 2, 9 * sc * (1 - u * 0.7), P.mid, a, layer);
      needle(ctx, hx - Math.cos(ang) * len / 2, hy - Math.sin(ang) * len / 2, hx + Math.cos(ang) * len / 2, hy + Math.sin(ang) * len / 2, 3.5 * sc * (1 - u * 0.6), P.core, a, layer);
      glow(ctx, hx, hy, 40 * sc, P.g1, 0.6 * (1 - u));
    }
    sparks(ctx, G.t, th, hx, hy, layer, { n: 9, a0: (back ? Math.PI : 0) - 0.7, a1: (back ? Math.PI : 0) + 0.7, sp: 300, pal: P, seed: Math.floor(th * 1000), len: 14, life: 0.35 });
  });
}
// trozo de estela de fuego colgado en el aire: quema a lo que lo toca
function fireBit(x, y, life, burn, slot) {
  const t0 = G.t, seed = Math.floor(t0 * 1000), R = rand(seed), ox = (R() - 0.5) * 10, lay = R() < 0.5 ? 'back' : 'front', h = 16 + R() * 12, w = 5 + R() * 3, ember = R() < 0.4;
  let tick = 0;
  return {
    kind: 'zone',
    update(dt) {
      tick -= dt;
      if (tick <= 0) { tick = 0.3; for (const e of alive()) if (Math.abs(e.x - x) < e.rx + 6 && Math.abs(e.y - y) < e.ry + 10) damage(e, { dmg: 2, elem: 'fire', burn, slot, silentFlash: true }); }
      return G.t - t0 < life;
    },
    draw(ctx, layer) {
      if (layer !== lay) return;
      const age = G.t - t0, a = (1 - age / life) * seg(age, 0, 0.05);
      flame(ctx, x + ox, y + 10, h * a, w * Math.max(0.4, a), G.t, seed, a, layer);
      if (ember) disc(ctx, x + ox * 2, y - age * 40, 1.6, PAL.fire.light, a, layer);
    },
  };
}
