// g-rafaga.js — 8 · Ráfaga, versión jugable. Ficha y decisiones: docs/armas/rafaga.md · progresión: docs/armas/progresion.md
// Soplo en cono que hace poco daño y EMPUJA MUCHO: los enemigos salen LANZADOS (regla común: el que choca con otro a
// velocidad les hace daño a los dos). Recoloca a los enemigos para las otras armas (a la Singularidad, a la columna del
// Golpe estelar, a las minas…). Árbol: base → Tornado / Corriente / Martillo → 3 mejoras. Elemento y Duplicador vienen de items.
'use strict';
const SND_GUST = {
  blow: sound('gust_blow', 'Ráfaga', 'Soplo', k => { const t = nowT(); vNoise(t, 0.3 * k, 0.32, 'bandpass', 900, 2600, 0.9, { att: 0.01, wet: 0.2, pan: [-0.2, 0.4] }); vNoise(t, 0.2 * k, 0.15, 'lowpass', 600, 200, 1, { att: 0.005 }); }),
  tornado: sound('gust_tornado', 'Ráfaga', 'Tornado (aparece)', k => { const t = nowT(); vNoise(t, 1.0 * k, 0.22, 'bandpass', 400, 1400, 2, { rising: true, wet: 0.3, lfo: [7, 0.5] }); vThump(t, 70, 40, 0.4 * k, 0.3, 1); }),
  current: sound('gust_current', 'Ráfaga', 'Corriente (aparece)', k => { const t = nowT(); vNoise(t, 0.8 * k, 0.18, 'bandpass', 1800, 900, 1.2, { att: 0.05, wet: 0.35, pan: [-0.5, 0.5] }); }),
  charge: sound('gust_charge', 'Ráfaga', 'Martillo · carga', k => { const t = nowT(); vNoise(t, 0.7 * k, 0.14, 'bandpass', 300, 2400, 2, { rising: true, wet: 0.2 }); }),
  ram: sound('gust_ram', 'Ráfaga', 'Martillo · cañonazo de aire', k => { const t = nowT(); vThump(t, 140, 40, 0.35 * k, 0.8, 2.5, 0.15); vNoise(t, 0.35 * k, 0.4, 'bandpass', 2000, 300, 0.8, { att: 0.002, wet: 0.25 }); }),
  slam: sound('gust_slam', 'Ráfaga', 'Tornado · los estampa', k => SND.sing_slam ? SND.sing_slam.fn(k) : SFXLIB.boom(k, 0.6)),
};
addWeapon({
  id: 'gust', name: 'Ráfaga', short: 'Ráfaga', pal: 'wind',  fresh: true,   // arma nueva (2026-10-20), sin probar por el usuario
  archetypes: [
    { id: 'tornado', name: 'Tornado', desc: 'Cada soplo deja un tornado que avanza, levanta a los enemigos y los mantiene girando en el aire: ahí arriba reciben más daño de todo.',
      ups: [{ id: 'chase', name: 'Persecución', desc: 'El tornado va hacia donde apuntas.' },
            { id: 'slam', name: 'Estampida', desc: 'Al deshacerse, estampa contra el suelo a los que llevaba (golpe fuerte).' },
            { id: 'absorb', name: 'Absorción', desc: 'Si toca fuego (suelo ardiendo, un quemado) se vuelve tornado de fuego; si toca un congelado, de hielo.' }] },
    { id: 'current', name: 'Corriente', desc: 'El soplo deja una corriente de viento: arrastra a los enemigos en su dirección y lleva a TUS PROYECTILES (de cualquier arma) a favor, más rápidos y fuertes.',
      ups: [{ id: 'tail', name: 'Viento de cola', desc: 'Dentro de la corriente corres más y saltas más alto.' },
            { id: 'blade', name: 'Filo', desc: 'La corriente corta: daño continuo a los que están dentro.' },
            { id: 'tow', name: 'Remolque', desc: 'Arrastra a los enemigos hasta el final de la corriente y, al acabarse, los lanza por los aires.' }] },
    { id: 'ram', name: 'Martillo', desc: 'Mantén para comprimir el aire y suelta: un cañonazo estrecho que dispara al enemigo como un proyectil (más carga = más lejos y más fuerte).',
      ups: [{ id: 'pins', name: 'Bolos', desc: 'A quien golpea un lanzado sale lanzado también.' },
            { id: 'crash', name: 'Estampido', desc: 'Cuando el lanzado se para, estalla en área.' },
            { id: 'counter', name: 'Contragolpe', desc: 'El cañonazo devuelve las balas enemigas y redirige tus proyectiles en su dirección, reforzados.' }] },
  ],
  tuning: {
    cooldown: [0.85, 0.1, 4, 0.05, 'Base: recarga (s)'], range: [230, 60, 500, 5, 'Base: alcance del soplo'], half: [0.5, 0.1, 1.4, 0.02, 'Base: medio ángulo del cono (rad)'],
    damage: [10, 0, 60, 1, 'Base: daño'], push: [560, 0, 1500, 10, 'Base: empuje'], lift: [200, 0, 800, 10, 'Base: salto que da a los de suelo'], launchTime: [0.6, 0, 2, 0.05, 'Base: s que va lanzado (choca)'],
    iceFreeze: [0.35, 0, 1, 0.05, 'Hielo: congelación por soplo'], fireBurn: [1.5, 0, 6, 0.1, 'Fuego: quemadura (s)'], fireSpread: [90, 0, 250, 5, 'Fuego: aviva — radio al que salta la quemadura'],
    tornadoCooldown: [2, 0.3, 6, 0.1, 'Tornado: recarga (s)'], tornadoSpeed: [90, 0, 300, 5, 'Tornado: velocidad'], tornadoTime: [2.6, 0.5, 8, 0.1, 'Tornado: duración (s)'],
    tornadoW: [55, 20, 150, 1, 'Tornado: ancho'], tornadoH: [170, 60, 300, 5, 'Tornado: alto'], tornadoMax: [6, 1, 12, 1, 'Tornado: enemigos que lleva'],
    tornadoDps: [16, 0, 80, 1, 'Tornado: daño/s'], tornadoVuln: [1.3, 1, 3, 0.05, 'Tornado: × daño que reciben en el aire'], chaseSpeed: [170, 20, 400, 5, 'Tornado · Persecución: velocidad'],
    slamDamage: [35, 0, 150, 1, 'Tornado · Estampida: daño'],
    currentCooldown: [2.6, 0.3, 8, 0.1, 'Corriente: recarga (s)'], currentLen: [380, 100, 800, 10, 'Corriente: largo'], currentWid: [45, 15, 120, 1, 'Corriente: medio ancho'],
    currentTime: [3.5, 0.5, 10, 0.1, 'Corriente: duración (s)'], currentPush: [110, 0, 400, 5, 'Corriente: arrastre (px/s)'], currentProj: [1.35, 1, 3, 0.05, 'Corriente: × velocidad de tus proyectiles'],
    currentDmg: [1.35, 1, 3, 0.05, 'Corriente: × daño de tus proyectiles'], tailMove: [1.4, 1, 2.5, 0.05, 'Corriente · Viento de cola: × velocidad'], tailJump: [1.3, 1, 2, 0.05, 'Corriente · Viento de cola: × salto'],
    bladeDps: [14, 0, 80, 1, 'Corriente · Filo: daño/s'], towSpeed: [170, 20, 500, 5, 'Corriente · Remolque: arrastre'], towDamage: [25, 0, 120, 1, 'Corriente · Remolque: daño al lanzarlos'],
    ramCharge: [0.8, 0.1, 2, 0.05, 'Martillo: carga máxima (s)'], ramCooldown: [1.1, 0.1, 4, 0.05, 'Martillo: recarga (s)'], ramRange: [320, 80, 600, 10, 'Martillo: alcance'], ramHalf: [0.25, 0.05, 0.8, 0.01, 'Martillo: medio ángulo'],
    ramMin: [15, 0, 100, 1, 'Martillo: daño sin carga'], ramMax: [50, 0, 200, 1, 'Martillo: daño con carga'], ramPushMin: [500, 0, 2000, 10, 'Martillo: empuje sin carga'], ramPushMax: [1300, 0, 3000, 10, 'Martillo: empuje con carga'],
    crashRadius: [85, 20, 250, 5, 'Martillo · Estampido: radio'], crashDamage: [30, 0, 150, 1, 'Martillo · Estampido: daño'], counterMul: [1.5, 1, 4, 0.05, 'Martillo · Contragolpe: × daño de lo redirigido'],
  },
  create(slot) {
    const id = 'gust';
    let cd = 0, cdFull = 1, charge = null;
    const later = (s, fn) => { const t0 = G.t; G.ents.push({ kind: 'timer', update() { if (G.t - t0 < s) return true; fn(); return false; }, draw() {} }); };
    const fire = (aim, k) => {
      const t = T(id), arch = slot.arch, n = copies();
      const one = (aOff) => {
        const a = { ...aim, ang: aim.ang + aOff, dx: Math.cos(aim.ang + aOff), dy: Math.sin(aim.ang + aOff) };
        if (arch === 'ram') gustBlast(slot, a, { range: t.ramRange, half: t.ramHalf, dmg: lerp(t.ramMin, t.ramMax, k), push: lerp(t.ramPushMin, t.ramPushMax, k), launch: 0.4 + 0.6 * k, heavy: k >= 0.95, ram: k });
        else {
          gustBlast(slot, a, { range: t.range, half: t.half, dmg: t.damage, push: t.push, launch: t.launchTime });
          if (arch === 'tornado') G.ents.push(gustTornado(slot, a));
          if (arch === 'current') G.ents.push(gustCurrent(slot, a));
        }
      };
      one(0); if (n > 1) later(0.12, () => one(arch === 'ram' ? 0.12 : 0.1));
      G.player.castT = 0.25; G.player.vx -= Math.cos(aim.ang) * (arch === 'ram' ? 160 * k : 60);   // retroceso
    };
    return {
      onPress(aim) {
        if (cd > 0 || charge) return;
        if (slot.arch === 'ram') { charge = { t0: G.t, ent: null }; charge.ent = gustChargeEnt(slot, () => charge); G.ents.push(charge.ent); sfx(SND_GUST.charge.fn); return; }
        const t = T(id); cdFull = (slot.arch === 'tornado' ? t.tornadoCooldown : slot.arch === 'current' ? t.currentCooldown : t.cooldown) * cdMul(); cd = cdFull;
        fire(aim, 1);
      },
      onRelease(aim) {
        if (!charge) return;
        const t = T(id), k = clamp((G.t - charge.t0) / t.ramCharge); charge = null;
        cdFull = t.ramCooldown * cdMul(); cd = cdFull; fire(aim, k);
      },
      update(dt, current) { cd -= dt; if (charge && !current) charge = null; },
      chargeK() { return charge ? clamp((G.t - charge.t0) / T(id).ramCharge) : 0; },
      ready() { return charge ? clamp((G.t - charge.t0) / T(id).ramCharge) : 1 - cd / Math.max(0.01, cdFull); },
      refund(s) { cd -= s; },
    };
  },
});

// soplo / cañonazo en cono desde el cristal. o = { range, half, dmg, push, launch, heavy, ram (carga 0..1 si es Martillo) }
function gustBlast(slot, aim, o) {
  const t = T('gust'), el = elemOf(), pal = elemPal('wind'), ang = aim.ang, x = aim.x, y = aim.y;
  const inCone = (px, py, pad = 0) => { const d = Math.hypot(px - x, py - y); if (d > o.range + pad) return false; const da = Math.abs(((Math.atan2(py - y, px - x) - ang + Math.PI * 3) % TAU) - Math.PI); return da < o.half + Math.atan2(pad, Math.max(1, d)); };
  const ram = o.ram !== undefined;
  let hit = alive().filter(e => inCone(e.x, e.y, Math.max(e.rx, e.ry)));
  if (ram) hit = hit.sort((a, b) => Math.hypot(a.x - x, a.y - y) - Math.hypot(b.x - x, b.y - y)).slice(0, 1);   // el Martillo dispara sólo al más cercano
  const c = Math.cos(ang), s = Math.sin(ang);
  for (const e of hit) {
    const wasBurning = e.burnT > 0, k = 1 - 0.4 * clamp(Math.hypot(e.x - x, e.y - y) / o.range);
    damage(e, { dmg: o.dmg * k, kx: c * o.push * k, ky: e.fly ? s * o.push * k * 0.8 : -t.lift * k, slot, launch: o.launch, pins: hasMod(slot, 'pins'), heavy: o.heavy,
      elem: el, freeze: el === 'ice' ? t.iceFreeze : 0, burn: el === 'fire' ? t.fireBurn : 0, shake: ram ? 3 + 6 * o.ram : 2, hitstop: ram ? 0.03 + 0.05 * o.ram : 0.01, frame: ram && o.ram >= 0.95 });
    if (wasBurning || (el === 'fire' && e.burnT > 0)) for (const o2 of alive()) if (o2 !== e && Math.hypot(o2.x - e.x, o2.y - e.y) < t.fireSpread) { o2.burnT = Math.max(o2.burnT, 2); o2.burnTick = 0.1; }   // el viento aviva el fuego
    if (ram && hasMod(slot, 'crash')) G.ents.push(gustCrashWatch(slot, e, 0.4 + 0.6 * o.ram));
  }
  // balas: las enemigas se deshacen (Contragolpe: vuelven y son tuyas); tus proyectiles, con Contragolpe, salen en la dirección del cañonazo
  const counter = ram && hasMod(slot, 'counter');
  for (const q of G.ents) {
    if (typeof q.vx !== 'number' || q.captured || !inCone(q.x, q.y, 10)) continue;
    if (q.kind === 'enemyShot' && q.hostile && !q.friendly) {
      if (counter) { const sp = Math.hypot(q.vx, q.vy) * 1.6; q.vx = c * sp; q.vy = s * sp; q.hostile = false; q.friendly = true; q.slot = slot; q.dmg = 14 * t.counterMul; q.life = 3; }
      else q.life = 0;
      const qx = q.x, qy = q.y; addFx(0.6, (ctx, layer, f0) => windSwirl(ctx, G.t, layer, f0, qx, qy, 14, pal));
    } else if (counter && q.state === 'fly' && !q.hostile && q.countered !== true) {
      const sp = Math.max(500, Math.hypot(q.vx, q.vy)); q.vx = c * sp; q.vy = s * sp; q.dmgMul = (q.dmgMul || 1) * t.counterMul; q.countered = true; if (q.age !== undefined) q.age = 0;
    }
  }
  const range = o.range, half = o.half, sd = Math.floor(G.t * 100);
  if (ram) addFx(0.4, (ctx, layer, f0) => { ramRing(ctx, G.t, layer, f0, x, y, ang, range, 0.7 + 0.6 * o.ram, pal); gustCone(ctx, G.t, layer, f0, x, y, ang, range, half, pal, { seed: sd, n: 7, w: 4, life: 0.25 }); });
  else addFx(0.45, (ctx, layer, f0) => gustCone(ctx, G.t, layer, f0, x, y, ang, range, half, pal, { seed: sd }));
  sfx(ram ? SND_GUST.ram.fn : SND_GUST.blow.fn);
  return hit;
}

// Martillo: dibujo de la carga en el cristal mientras se mantiene
function gustChargeEnt(slot, getCharge) {
  const pal = elemPal('wind');
  return { kind: 'fx', update() { return !!getCharge() && G.slots.includes(slot); },
    draw(ctx, layer) { const c = getCharge(); if (!c) return; const m = muzzle(), k = clamp((G.t - c.t0) / T('gust').ramCharge); ramCharge(ctx, G.t, layer, m.x, m.y, k, pal); G.player.castT = 0.1; } };
}
// Martillo · Estampido: espera a que el lanzado se pare y estalla
function gustCrashWatch(slot, e, k) {
  const t0 = G.t, t = T('gust'), pal = elemPal('wind');
  return { kind: 'timer', update() {
      if (e.dead) return false;
      if (G.t - t0 < 0.15 || (Math.abs(e.vx) > 90 && G.t - t0 < 1.5)) return true;
      const x = e.x, y = e.y;
      damageArea(x, y, t.crashRadius, { dmg: t.crashDamage * (0.5 + 0.5 * k), kx: 220, ky: -200, slot, proc: true, heavy: true, shake: 5, hitstop: 0.03, elem: elemOf() });
      addFx(1, (ctx, layer, f0) => { impact(ctx, G.t, layer, f0, x, y, 0.9, pal, { spread: TAU, seed: Math.floor(x) }); windSwirl(ctx, G.t, layer, f0, x, y, t.crashRadius * 0.5, pal); });
      sfx(k2 => SFXLIB.boom(k2, 0.5));
      return false; }, draw() {} };
}

// Tornado: avanza por el suelo, levanta y hace girar a los que toca; en el aire son vulnerables
function gustTornado(slot, aim) {
  const t = T('gust'), t0 = G.t, caught = [];
  let x = aim.x + aim.dx * t.range * 0.55, dir = Math.sign(aim.dx) || G.player.face, tick = 0, elem = elemOf(), seed = Math.floor(G.t * 37) % 100;
  sfx(SND_GUST.tornado.fn);
  const palNow = () => PAL[elem === 'fire' ? 'fire' : elem === 'ice' ? 'ice' : 'wind'];
  return {
    kind: 'tornado',
    get x() { return x; },
    update(dt) {
      const age = G.t - t0;
      if (hasMod(slot, 'chase')) { const dx = G.mouse.x - x; x += clamp(dx, -t.chaseSpeed * dt, t.chaseSpeed * dt); }
      else x += dir * t.tornadoSpeed * dt;
      x = clamp(x, 30, WW - 30);
      // Absorción: fuego del suelo / un quemado → fuego; un congelado → hielo
      if (hasMod(slot, 'absorb') && !elem) {
        if (G.ents.some(z => z.kind === 'zone' && z.fire && Math.abs(z.x - x) < t.tornadoW + (z.halfW || 0))) elem = 'fire';
        for (const e of caught) { if (e.burnT > 0) elem = 'fire'; else if (e.frozenT > 0 || e.freeze > 0.5) elem = 'ice'; }
        if (elem) { text(x, GROUND - t.tornadoH - 10, elem === 'fire' ? 'TORNADO DE FUEGO' : 'TORNADO DE HIELO', elem === 'fire' ? '#ffb35a' : '#bdf3ff', true); sfx(elem === 'fire' ? k => SFXLIB.burn(k, 0.8) : k => SFXLIB.freeze(k, 0.4)); }
      }
      if (age < t.tornadoTime) {
        for (const e of alive()) if (!caught.includes(e) && caught.length < t.tornadoMax && !e.stasis && Math.abs(e.x - x) < t.tornadoW * 0.75 + e.rx * 0.5 && e.y > GROUND - t.tornadoH - 20) caught.push(e);
        caught.forEach((e, i) => {
          if (e.dead || e.stasis) return;
          const ty = GROUND - 80 - (i % 3) * 32 + Math.sin(G.t * 5 + i) * 8, tx = x + Math.cos(G.t * 7 + i * 2.1) * (t.tornadoW * 0.45);
          e.x = lerp(e.x, tx, Math.min(1, dt * 8)); e.y = lerp(e.y, ty, Math.min(1, dt * 6)); e.vx = 0; e.vy = 0; e.slammed = false;
          makeVuln(e, t.tornadoVuln, 0.3);
        });
        tick -= dt;
        if (tick <= 0) {
          tick = 0.25; const ee = elem;
          for (const e of caught) if (!e.dead) damage(e, { dmg: t.tornadoDps * 0.25, slot, silentFlash: true, elem: ee, freeze: ee === 'ice' ? 0.12 : 0, burn: ee === 'fire' ? 1.2 : 0 });
        }
      } else if (caught.length) {                                // se deshace: los suelta (Estampida: contra el suelo)
        const slam = hasMod(slot, 'slam');
        for (const e of caught) {
          if (e.dead) continue;
          e.vy = slam ? 900 : 0; e.vx = 0;
          if (slam) {
            damage(e, { dmg: t.slamDamage, heavy: true, slot, ky: 0, shake: 4, hitstop: 0.03, elem: elem, freeze: elem === 'ice' ? 0.4 : 0, burn: elem === 'fire' ? 2 : 0 });
            if (e.fly && !e.dead) { e.slammed = true; }
            const ex = e.x, P = palNow(); addFx(1.2, (ctx, layer, f0) => groundImpact(ctx, G.t, layer, f0, ex, 0.6, P, { seed: Math.floor(ex) }));
          }
        }
        if (slam) sfx(SND_GUST.slam.fn);
        caught.length = 0;
      }
      return age < t.tornadoTime + 0.4;
    },
    draw(ctx, layer) {
      const age = G.t - t0, a = easeOut(seg(age, 0, 0.25)) * (1 - seg(age, t.tornadoTime, t.tornadoTime + 0.4));
      tornadoFx(ctx, G.t, layer, x, t.tornadoH * (0.4 + 0.6 * easeOut(seg(age, 0, 0.3))), t.tornadoW, palNow(), a, seed);
    },
  };
}

// Corriente: franja de viento en la dirección del soplo; arrastra enemigos y lleva tus proyectiles a favor
function gustCurrent(slot, aim) {
  const t = T('gust'), t0 = G.t, el = elemOf(), pal = elemPal('wind');
  const x0 = aim.x, y0 = aim.y, ang = aim.ang, c = Math.cos(ang), s = Math.sin(ang), len = t.currentLen, wid = t.currentWid, seed = Math.floor(G.t * 13) % 50;
  let tick = 0, towDone = false;
  const local = (px, py) => { const dx = px - x0, dy = py - y0; return { d: dx * c + dy * s, o: -dx * s + dy * c }; };
  const inside = (px, py, pad = 0) => { const l = local(px, py); return l.d > -pad && l.d < len + pad && Math.abs(l.o) < wid + pad; };
  const end = { x: x0 + c * len, y: y0 + s * len };
  sfx(SND_GUST.current.fn);
  return {
    kind: 'current',
    update(dt) {
      const age = G.t - t0;
      if (age < t.currentTime) {
        const tow = hasMod(slot, 'tow');
        for (const e of alive()) {
          if (!inside(e.x, e.y, Math.max(e.rx, e.ry) * 0.6) || e.stasis) continue;
          e.pull = tow ? { x: end.x, y: e.fly ? end.y : e.y, s: t.towSpeed, lift: e.fly } : { x: e.x + c * 80, y: e.y + s * 80, s: t.currentPush, lift: e.fly };
        }
        for (const q of G.ents) {
          if (typeof q.vx !== 'number' || q.captured || !inside(q.x, q.y)) continue;
          if (q.kind === 'enemyShot' && !q.friendly) { q.vx += c * 420 * dt; q.vy += s * 420 * dt; continue; }
          if (q.state !== 'fly' || q.hostile || q.windBy === this) continue;
          const sp = Math.max(300, Math.hypot(q.vx, q.vy)) * t.currentProj; q.vx = c * sp; q.vy = s * sp; q.dmgMul = (q.dmgMul || 1) * t.currentDmg; q.windBy = this; if (q.age !== undefined) q.age = Math.max(0, q.age - 0.2);
          const qx = q.x, qy = q.y; addFx(0.35, (ctx, layer, f0) => speedStreaks(ctx, G.t, layer, f0, qx, qy, ang, pal));
        }
        const p = G.player;
        if (hasMod(slot, 'tail') && inside(p.x, p.y - 40, 10)) { p.speedMul = Math.max(p.speedMul || 1, t.tailMove); p.jumpMul = t.tailJump; p.jumpUntil = G.t + 0.1; }
        if (hasMod(slot, 'blade')) { tick -= dt; if (tick <= 0) { tick = 0.25; for (const e of alive()) if (inside(e.x, e.y, Math.max(e.rx, e.ry) * 0.6)) damage(e, { dmg: t.bladeDps * 0.25, slot, silentFlash: true, elem: el, freeze: el === 'ice' ? 0.06 : 0, burn: el === 'fire' ? 1 : 0 }); } }
      } else if (!towDone && hasMod(slot, 'tow')) {             // Remolque: al acabarse, los del final salen por los aires
        towDone = true;
        for (const e of alive()) if (Math.hypot(e.x - end.x, e.y - end.y) < 110) damage(e, { dmg: t.towDamage, slot, kx: c * 300, ky: -520, launch: 0.6, heavy: true, shake: 4, elem: el, freeze: el === 'ice' ? 0.4 : 0, burn: el === 'fire' ? 2 : 0 });
        addFx(0.8, (ctx, layer, f0) => { impact(ctx, G.t, layer, f0, end.x, end.y, 0.9, pal, { dir: -Math.PI / 2, spread: 2, seed: 4 }); windSwirl(ctx, G.t, layer, f0, end.x, end.y, 50, pal); });
        sfx(SND_GUST.ram.fn);
      }
      return age < t.currentTime + 0.4;
    },
    draw(ctx, layer) {
      const age = G.t - t0, a = easeOut(seg(age, 0, 0.2)) * (1 - seg(age, t.currentTime, t.currentTime + 0.4));
      windCurrent(ctx, G.t, layer, x0, y0, ang, len * easeOut(seg(age, 0, 0.25)), wid, a, pal, seed);
    },
  };
}
