// g-rayo.js — 3 · Rayo, versión jugable. Ficha y decisiones: docs/armas/rayo.md · progresión: docs/armas/progresion.md
// Mantener = canalizar (con calor: al llegar al máximo se corta solo). Quema por ticks al primer enemigo del haz.
// Árbol: base → evolución (Sobrecarga / Prisma / Faro) → 3 mejoras de esa evolución. Elemento y Duplicador vienen de items.
'use strict';
addWeapon({
  id: 'ray', name: 'Rayo', short: 'Rayo', pal: 'set',
  archetypes: [
    { id: 'over', name: 'Sobrecarga', desc: 'El daño crece mientras canalizas y al soltar descarga a lo largo de todo el haz. Si llegas al máximo se recalienta y tarda más en recargar.',
      ups: [{ id: 'sweet', name: 'Punto justo', desc: 'Soltar en el último tramo del calor, sin llegar al máximo: descarga doble y golpe fuerte.' },
            { id: 'pierce', name: 'Perforante', desc: 'Con más de medio calor el haz atraviesa a todos los de la línea.' },
            { id: 'wave', name: 'Onda', desc: 'La descarga estalla también en el punto de contacto, en área.' }] },
    { id: 'prism', name: 'Prisma', desc: 'Atraviesa al primero y se divide en haces dorados hacia los enemigos más cercanos.',
      ups: [{ id: 'chain', name: 'Cadena', desc: 'Cada haz dorado salta a un enemigo más.' },
            { id: 'focus', name: 'Foco', desc: 'Cada haz que no encuentra a quién ir se suma al principal (bueno contra uno solo).' },
            { id: 'crown', name: 'Corona', desc: 'Haces dorados cortos giran alrededor de la gema y cortan a todo lo que rodea al primero.' }] },
    { id: 'beacon', name: 'Faro', desc: 'Al soltar, el cristal se queda flotando donde estabas y sigue disparando solo al enemigo más cercano. Premia cambiar de arma.',
      ups: [{ id: 'link', name: 'Vínculo', desc: 'Un hilo de luz une el faro contigo: daña lo que lo cruza y parte del daño del faro te cura.' },
            { id: 'nova', name: 'Estallido', desc: 'Al apagarse, el faro lo descarga todo de golpe: estallido en área donde apunta (golpe fuerte).' },
            { id: 'watch', name: 'Vigía', desc: 'Cada baja del faro alarga su vida.' }] },
  ],
  // [valor, mín, máx, paso, etiqueta] — nombres = futuros campos en Unity
  tuning: {
    maxChannel: [2.0, 0.3, 6, 0.1, 'Base: canalización máxima (s)'], cooldown: [0.8, 0, 4, 0.05, 'Base: recarga tras soltar (s)'], range: [520, 100, 900, 10, 'Base: alcance (px)'],
    turnRate: [9, 1, 40, 0.5, 'Base: giro hacia el ratón (rad/s)'], tick: [0.1, 0.03, 0.5, 0.01, 'Base: cada cuánto quema (s)'], tickDamage: [6, 1, 40, 1, 'Base: daño por tick'],
    push: [18, 0, 120, 1, 'Base: empuje por tick'], freezeTick: [0.07, 0, 0.5, 0.01, 'Hielo: congelación por tick'], burnTick: [0.6, 0, 4, 0.1, 'Fuego: quemadura por tick (s)'],
    dupAngle: [0.07, 0, 0.4, 0.01, 'Duplicador: separación de los dos haces (rad)'],
    overMax: [2.5, 1, 6, 0.1, 'Sobrecarga: × daño por tick con el calor al máximo'], overBurst: [40, 0, 200, 1, 'Sobrecarga: daño de la descarga (calor al máximo)'],
    overMin: [0.2, 0, 1, 0.05, 'Sobrecarga: calor mínimo para descargar (fracción)'], overHeavyAt: [0.5, 0, 1, 0.05, 'Sobrecarga: calor para que la descarga sea golpe fuerte'],
    overHeatCd: [2, 1, 5, 0.1, 'Sobrecarga: × recarga si se recalienta'],
    sweetFrom: [0.75, 0.3, 0.98, 0.01, 'Sobrecarga · Punto justo: empieza la franja (fracción del calor)'], sweetMul: [2, 1, 5, 0.1, 'Sobrecarga · Punto justo: × descarga'],
    pierceAt: [0.5, 0, 1, 0.05, 'Sobrecarga · Perforante: calor a partir del que atraviesa'],
    waveRadius: [90, 20, 250, 5, 'Sobrecarga · Onda: radio'], waveDamage: [30, 0, 150, 1, 'Sobrecarga · Onda: daño (calor al máximo)'],
    prismCount: [2, 1, 5, 1, 'Prisma: haces'], prismRange: [300, 50, 600, 10, 'Prisma: alcance'], prismMul: [0.6, 0.1, 1.5, 0.05, 'Prisma: daño de cada haz (×)'],
    chainJumps: [1, 1, 4, 1, 'Prisma · Cadena: saltos extra por haz'], focusMul: [0.6, 0, 2, 0.05, 'Prisma · Foco: + daño del principal por haz sin usar'],
    crownCount: [3, 1, 6, 1, 'Prisma · Corona: haces que giran'], crownLen: [170, 40, 300, 5, 'Prisma · Corona: largo (px)'], crownSpin: [5, 0.5, 15, 0.5, 'Prisma · Corona: giro (rad/s)'],
    crownMul: [0.9, 0.1, 2, 0.05, 'Prisma · Corona: daño de cada haz (×)'],
    beaconTime: [3, 0.5, 10, 0.1, 'Faro: duración (s)'], beaconMul: [0.8, 0.1, 2, 0.05, 'Faro: × daño por tick'], beaconRange: [420, 100, 900, 10, 'Faro: alcance para buscar enemigo'],
    linkDps: [20, 0, 100, 1, 'Faro · Vínculo: daño/s del hilo'], lifesteal: [0.3, 0, 1, 0.01, 'Faro · Vínculo: fracción del daño del faro que cura'],
    novaDamage: [50, 0, 200, 1, 'Faro · Estallido: daño'], novaRadius: [110, 20, 300, 5, 'Faro · Estallido: radio'],
    watchTime: [1, 0, 4, 0.1, 'Faro · Vigía: + s de vida por baja'],
  },
  create(slot) {
    const id = 'ray';
    let cd = 0, cdFull = 1, beam = null, beacons = [];
    // maxed = se cortó solo al llegar al máximo (Sobrecarga: recalentado)
    const stop = maxed => {
      if (!beam) return;
      const t = T(id);
      if (slot.arch === 'beacon') { beacons.forEach(b => b.end()); beacons = beam; }   // los faros nuevos sustituyen a los anteriores
      beam.forEach(b => b.release(maxed)); beam = null;
      cd = cdFull = t.cooldown * cdMul() * (slot.arch === 'over' && maxed ? t.overHeatCd : 1);
    };
    return {
      onPress(aim) { if (cd > 0 || beam) return; const n = copies(); beam = []; for (let c = 0; c < n; c++) { const b = rayBeam(slot, aim, n > 1 ? (c ? 1 : -1) * T(id).dupAngle : 0, c > 0, c); beam.push(b); G.ents.push(b); } },
      onHold(aim, dt) { if (beam) { beam.forEach(b => b.aim(aim, dt)); if (beam[0].heat >= T(id).maxChannel) stop(true); } },
      onRelease() { stop(false); },
      update(dt, current) { cd -= dt; if (beam && !current) stop(false); },
      ready() { return beam ? 1 - beam[0].heat / T(id).maxChannel : 1 - cd / Math.max(0.01, cdFull); },
      refund(s) { cd -= s; },
    };
  },
});

// Recorrido del haz desde (x0,y0) con ángulo a hasta el primer enemigo, el suelo o el alcance (pts = [origen, final]).
// pierce = no se para en los enemigos (los devuelve todos en hits, en orden).
function rayPath(x0, y0, a, range, pierce) {
  const pts = [{ x: x0, y: y0 }], hits = [];
  const ex = x0 + Math.cos(a) * range, ey = y0 + Math.sin(a) * range;
  const ug = ey > GROUND && y0 < GROUND ? (GROUND - y0) / (ey - y0) : 2;              // fracción del recorrido hasta el suelo
  if (pierce) {
    const skip = new Set(); let h;
    while ((h = raycastEnemy(x0, y0, ex, ey, skip)) && h.u < ug) { h.ang = a; hits.push(h); skip.add(h.e); }
  } else {
    const h = raycastEnemy(x0, y0, ex, ey);
    if (h && h.u < ug) { h.ang = a; hits.push(h); pts.push({ x: h.x, y: h.y }); return { pts, hits }; }
  }
  pts.push(ug <= 1 ? { x: lerp(x0, ex, ug), y: GROUND } : { x: ex, y: ey });
  return { pts, hits };
}

// Haz: lo sostiene el player ('held'); con Faro, al soltar se planta y dispara solo ('beacon'); al final se apaga (released).
// idx = copia del Duplicador (el faro de la copia busca al segundo más cercano).
function rayBeam(slot, aim0, angOff = 0, quiet = false, idx = 0) {
  const t = T('ray'), el = elemOf(), ice = el === 'ice', fire = el === 'fire', arch = slot.arch;
  const over = arch === 'over', prism = arch === 'prism', beacon = arch === 'beacon';
  const pal = elemPal('set'), gp = PAL.gold, lp = PAL.life;
  const t0 = G.t;
  let mode = 'held', ang = aim0.ang + angOff, tickT = 0, linkT = 0, released = -1, healAcc = 0, unused = 0, crownA = 0, crown = [];
  let path = { pts: [{ x: aim0.x, y: aim0.y }, { x: aim0.x, y: aim0.y }], hits: [] }, first = null, subs = [], touched = new Set();
  let bx = 0, by = 0, plantT = 0, endT = 0, on = 0;
  if (!quiet) sfx(SND.ray_ignite.fn);
  const stopLoop = quiet ? () => {} : loopSound(out => {
    const mk = (f, g, type = 'sawtooth') => { const o = AC.createOscillator(), gg = AC.createGain(), lpf = AC.createBiquadFilter(); o.type = type; o.frequency.value = f; lpf.type = 'lowpass'; lpf.frequency.value = 1500; gg.gain.value = g; o.connect(lpf); lpf.connect(gg); gg.connect(out); o.start(); return o; };
    const a = mk(110, 0.07), b = mk(111.7, 0.06), c = mk(220, 0.025);
    const n = AC.createBufferSource(); n.buffer = noiseBuf; n.loop = true; const bp = AC.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = 3000; bp.Q.value = 2; const ng = AC.createGain(); ng.gain.value = 0.06; n.connect(bp); bp.connect(ng); ng.connect(out); n.start();
    return [a, b, c, n];
  });
  const hf = () => clamp(ent.heat / t.maxChannel);
  const piercing = () => mode === 'held' && over && hasMod(slot, 'pierce') && hf() >= t.pierceAt;
  const hitOne = (e, mul, a) => {
    touched.add(e);
    const watch = mode === 'beacon' && hasMod(slot, 'watch');
    const d = damage(e, { dmg: t.tickDamage * mul, kx: Math.sign(Math.cos(a)) * t.push, slot, elem: el, freeze: ice ? t.freezeTick : 0, burn: fire ? t.burnTick : 0, silentFlash: Math.random() < 0.5,
      onKill: watch ? () => { endT += t.watchTime; text(bx, by - 34, '+' + t.watchTime + ' s', '#ffe39a'); } : null });
    if (mode === 'beacon' && hasMod(slot, 'link') && d > 0) { healAcc += d * t.lifesteal; if (healAcc >= 1) { const hv = Math.floor(healAcc); healAcc -= hv; if (healPlayer(hv) > 0) text(G.player.x, G.player.y - 92, '+' + hv, '#ff9fb4'); } }
  };
  // Sobrecarga: descarga por toda la línea (atraviesa) al soltar; Punto justo = en la franja final sin llegar al máximo
  const discharge = maxed => {
    const k = hf(); if (k < t.overMin) return;
    const sweet = hasMod(slot, 'sweet') && !maxed && k >= t.sweetFrom, mul = k * (sweet ? t.sweetMul : 1), heavy = sweet || k >= t.overHeavyAt;
    const line = rayPath(ent.x0, ent.y0, ang, t.range, true), P = sweet ? gp : pal, end = path.pts[path.pts.length - 1];
    const hit = { slot, heavy, elem: el, freeze: ice ? t.freezeTick * 3 : 0, burn: fire ? t.burnTick * 3 : 0, hitstop: heavy ? 0.05 : 0, shake: heavy ? 4 : 0, frame: sweet };
    for (const h of line.hits) damage(h.e, Object.assign({}, hit, { dmg: t.overBurst * mul, kx: Math.sign(Math.cos(ang)) * 140 * k }));
    if (hasMod(slot, 'wave')) { damageArea(end.x, end.y, t.waveRadius, Object.assign({}, hit, { dmg: t.waveDamage * mul, kx: 160 })); }
    const pts = line.pts, ex = end.x, ey = end.y, wave = hasMod(slot, 'wave'), wr = t.waveRadius;
    addFx(0.5, (ctx, layer, f0) => { beamDischarge(ctx, G.t, layer, f0, pts, k, P); if (wave) { shockRing(ctx, G.t, layer, f0, ex, ey, wr, P); impact(ctx, G.t, layer, f0, ex, ey, 0.7, P, { spread: TAU, seed: 5 }); } });
    if (sweet) text(G.player.x, G.player.y - 110, '¡JUSTO!', '#ffe39a', true);
    if (!quiet) sfx(k2 => { SFXLIB.hitEnergy(k2, 0.5 + k); if (sweet) SND.ray_split.fn(k2); if (wave) SFXLIB.boom(k2, 0.4); });
  };
  const ent = {
    kind: 'beam', heat: 0, x0: aim0.x, y0: aim0.y,
    aim(aim, dt) { if (mode !== 'held') return; let d = ((aim.ang + angOff - ang + Math.PI * 3) % TAU) - Math.PI; ang += clamp(d, -t.turnRate * dt, t.turnRate * dt); },
    release(maxed) {
      if (mode !== 'held' || released >= 0) return;
      stopLoop();
      if (over) { if (maxed) text(G.player.x, G.player.y - 110, 'RECALENTADO', '#ff9a5a', true); discharge(maxed); }
      if (beacon) {                                           // se planta donde estaba el cristal y sigue solo
        mode = 'beacon'; bx = this.x0; by = this.y0; plantT = G.t; endT = G.t + t.beaconTime; on = first ? 1 : 0;
        if (!quiet) sfx(SND.ray_split.fn);
        return;
      }
      this.off();
    },
    end() {                                                   // fin del faro (se acaba el tiempo o lo sustituye uno nuevo)
      if (mode !== 'beacon' || released >= 0) return;
      if (hasMod(slot, 'nova')) {                             // estalla donde apunta (si no tiene a quién, en el sitio)
        const end = path.pts[path.pts.length - 1], x = first ? end.x : bx, y = first ? end.y : by, pts = first ? path.pts : [];
        damageArea(x, y, t.novaRadius, { dmg: t.novaDamage, kx: 200, slot, heavy: true, elem: el, freeze: ice ? 0.5 : 0, burn: fire ? 2 : 0, shake: 5, hitstop: 0.04 });
        addFx(0.9, (ctx, layer, f0) => { beamDischarge(ctx, G.t, layer, f0, pts, 0.5, pal); shockRing(ctx, G.t, layer, f0, x, y, t.novaRadius, pal); impact(ctx, G.t, layer, f0, x, y, 0.9, pal, { spread: TAU, seed: 11 }); });
        if (!quiet) sfx(k => SFXLIB.boom(k, 0.6));
      }
      this.off();
    },
    off() {
      released = G.t; if (!quiet) sfx(SND.ray_off.fn);
      if (ice) for (const e of touched) if (!e.dead && e.frozenT > 0) damage(e, { dmg: 0, heavy: true, slot });
    },
    update(dt) {
      if (released >= 0) return G.t - released < 0.6;
      if (mode === 'held') { this.heat += dt; G.player.castT = 0.1; const m = muzzle(); this.x0 = m.x; this.y0 = m.y; }
      else {
        if (G.t >= endT) { this.end(); return true; }
        this.x0 = bx; this.y0 = by;
        const n1 = nearestEnemy(bx, by, t.beaconRange), n2 = idx > 0 && n1 ? nearestEnemy(bx, by, t.beaconRange, new Set([n1])) : null, tg = n2 || n1;
        on = clamp(on + (tg ? 6 : -6) * dt);
        if (tg) { const d = ((Math.atan2(tg.y - by, tg.x - bx) - ang + Math.PI * 3) % TAU) - Math.PI; ang += clamp(d, -t.turnRate * dt, t.turnRate * dt); }
      }
      path = rayPath(this.x0, this.y0, ang, t.range, piercing());
      const h0 = path.hits[0];
      first = h0 ? h0.e : null;
      // prisma: sale por detrás del primero y se divide (Cadena: cada haz salta a uno más)
      subs = []; unused = 0;
      if (prism && first) {
        const px = first.x + Math.cos(h0.ang) * first.rx, py = first.y + Math.sin(h0.ang) * first.ry, skip = new Set([first]);
        for (let i = 0; i < t.prismCount; i++) {
          const n = nearestEnemy(px, py, t.prismRange, skip); if (!n) { unused++; continue; }
          skip.add(n); subs.push({ e: n, x0: px, y0: py });
          if (hasMod(slot, 'chain')) { let prev = n; for (let j = 0; j < t.chainJumps; j++) { const c = nearestEnemy(prev.x, prev.y, t.prismRange, skip); if (!c) break; skip.add(c); subs.push({ e: c, x0: prev.x, y0: prev.y, chain: true }); prev = c; } }
        }
        this.px = px; this.py = py;
      }
      // Corona: haces cortos que giran alrededor de la gema
      crown = [];
      if (prism && first && hasMod(slot, 'crown')) {
        crownA += t.crownSpin * dt;
        for (let i = 0; i < t.crownCount; i++) { const a = crownA + i / t.crownCount * TAU; crown.push({ x0: this.px, y0: this.py, x1: this.px + Math.cos(a) * t.crownLen, y1: this.py + Math.sin(a) * t.crownLen, a }); }
      }
      tickT -= dt;
      if (tickT <= 0 && (mode === 'held' || on > 0.5)) {
        tickT = t.tick;
        let mul = mode === 'beacon' ? t.beaconMul : over ? lerp(1, t.overMax, hf()) : 1;
        if (prism && hasMod(slot, 'focus')) mul *= 1 + t.focusMul * unused;
        const targets = piercing() ? path.hits : h0 ? [h0] : [];
        for (const h of targets) hitOne(h.e, mul, h.ang);
        for (const s of subs) hitOne(s.e, t.prismMul, Math.atan2(s.e.y - s.y0, s.e.x - s.x0));
        const cut = new Set([first]);                       // Corona: cada enemigo, un golpe por tick aunque lo crucen varios haces
        for (const c of crown) { let h; while ((h = raycastEnemy(c.x0, c.y0, c.x1, c.y1, cut))) { cut.add(h.e); hitOne(h.e, t.crownMul, c.a); } }
        if (targets.length && !quiet) sfx(k => { const tt = nowT(); vThump(tt, 130, 70, 0.07 * k * (mode === 'beacon' ? 0.5 : 1), 0.16, 1.5, 0.05); });
      }
      // Faro · Vínculo: el hilo faro → player daña lo que lo cruza
      if (mode === 'beacon' && hasMod(slot, 'link')) {
        linkT -= dt;
        if (linkT <= 0) {
          linkT = t.tick; const p = G.player, skip = new Set(); let h;
          while ((h = raycastEnemy(bx, by, p.x, p.y - 40, skip))) { skip.add(h.e); damage(h.e, { dmg: t.linkDps * t.tick, slot, dot: true, silentFlash: true, color: '#ff9fb4' }); }
        }
      }
      return true;
    },
    draw(ctx, layer) {
      const fade = released < 0 ? 1 : 1 - easeIn(seg(G.t, released, released + 0.14));
      const env = easeOut(seg(G.t, t0, t0 + 0.08)) * fade * (mode === 'beacon' ? on : 1);
      const pts = path.pts, m = pts[0], end = pts[pts.length - 1], k = over ? hf() : 0;
      const sc = over ? 1 + 0.8 * k : mode === 'beacon' ? 0.75 : 1;
      if (mode === 'beacon') {
        beaconCrystal(ctx, G.t, bx, by, fade, pal, layer, (endT - G.t) / t.beaconTime);
        if (hasMod(slot, 'link')) {
          const p = G.player; energyBeam(ctx, G.t, layer, bx, by, p.x, p.y - 40, 0.6 * fade, lp, { sc: 0.3, seed: 5 });
          if (first) { lifeMotes(ctx, G.t, layer, bx, by, p.x, p.y - 40, fade); healCrosses(ctx, G.t - plantT, layer, p.x, p.y, fade); }
        }
      }
      if (env > 0.01) {
        const reach = mode === 'beacon' ? 1 : easeOut(seg(G.t, t0, t0 + 0.07));
        if (reach < 1) { const p1 = pts[1]; energyBeam(ctx, G.t, layer, m.x, m.y, lerp(m.x, p1.x, reach), lerp(m.y, p1.y, reach), env, pal, { sc }); }
        else for (let i = 0; i + 1 < pts.length; i++) energyBeam(ctx, G.t, layer, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, env, pal, { sc, seed: i * 3 });
        if (layer === 'front' && mode === 'held') {
          glow(ctx, m.x, m.y, 34 * env * sc, pal.g1, 0.7 * env); star4(ctx, m.x, m.y, 16 * env * sc, G.t * 7, pal.core, env, layer);
          if (over && hasMod(slot, 'sweet') && k >= t.sweetFrom) { const pu = 0.6 + 0.4 * Math.sin(G.t * 40); glow(ctx, m.x, m.y, 50, gp.g1, 0.8 * pu); star4(ctx, m.x, m.y, 30 * pu, -G.t * 5, gp.core, 1, layer); }
        }
        if (reach >= 1) {
          const s0 = t0 + 0.07, al = s0 + Math.max(0, Math.ceil((G.t - 0.45 - s0) / 0.035)) * 0.035;
          beamContact(ctx, G.t, layer, end.x, end.y, env, pal, al, released < 0 ? G.t : released, sc, 1);
          if (piercing()) for (const [i, h] of path.hits.entries()) beamContact(ctx, G.t, layer, h.x, h.y, env, pal, al, G.t, 0.6, 20 + i);
        }
        for (const [i, s] of subs.entries()) {
          if (s.e.dead) continue;
          const tx = s.e.x - Math.sign(s.e.x - s.x0) * s.e.rx * 0.8;
          energyBeam(ctx, G.t, layer, s.x0, s.y0, tx, s.e.y, env * 0.95, gp, { sc: s.chain ? 0.4 : 0.5, seed: i * 2.1 });
          beamContact(ctx, G.t, layer, tx, s.e.y, env, gp, Math.ceil((G.t - 0.4) / 0.035) * 0.035, G.t, 0.6, 10 + i);
        }
        for (const [i, c] of crown.entries()) energyBeam(ctx, G.t, layer, c.x0, c.y0, c.x1, c.y1, env * 0.9, gp, { sc: 0.35, seed: 30 + i });
        if (subs.length || (prism && first)) { if (layer === 'front') glow(ctx, this.px, this.py, 34 * (1 + 0.3 * unused * (hasMod(slot, 'focus') ? 1 : 0)), gp.g2, 0.5 * env); prismGem(ctx, G.t, this.px, this.py, env, gp, layer); }
      }
      if (released >= 0) {                                   // restos que flotan sobre la línea del haz
        const R = rand(61), u = seg(G.t, released, released + 0.6);
        for (let i = 0; i < 16; i++) { const kk = R(), x = lerp(m.x, end.x, kk), y = lerp(m.y, end.y, kk), lay = R() < 0.5 ? 'back' : 'front', dy = -(10 + R() * 26) * easeOut(u);
          if (lay === layer) disc(ctx, x + (R() - 0.5) * 12 * u, y + dy, (1 + R() * 1.8) * (1 - u), i % 3 ? pal.light : pal.core, 1 - u, layer); }
      }
    },
  };
  G.player.castT = 0.2;
  return ent;
}
