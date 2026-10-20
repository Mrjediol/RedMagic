// game.js — motor de la versión jugable (jugable.html): player, enemigos de prueba, entidades, daño, estados y reacciones.
// Las armas viven en g-*.js (una por arma, con su TUNING) y dibujan con fx.js / core.js, igual que los clips.
// Coordenadas = las de los clips (suelo en GROUND = 340, el player mide ~80 px); el mundo es más ancho y la cámara lo sigue.
'use strict';

const VW = 800, VH = 400, WW = 2000;
const GRAV = 1500;
const PLATFORMS = [{ x0: 520, x1: 660, y: 252 }, { x0: 760, x1: 900, y: 190 }, { x0: 1180, x1: 1340, y: 236 }, { x0: 1560, x1: 1700, y: 200 }];

const G = {
  t: 0, timeScale: 1, hitStop: 0, shake: 0, dark: 0,
  cam: { x: 0 }, mouse: { x: 0, y: 0, sx: 0, sy: 0, down: false }, keys: {},
  player: null, enemies: [], ents: [], texts: [], dmgLog: [], dmgTotal: 0,
  slots: [], cur: 0, slotCount: 2,
  settings: { reactions: true, frames: true, hitstop: true, sound: false, enemyAttacks: false, hitboxes: false, hpMul: 1 },
  build: { items: {} }, lastSwap: -9, swapBuff: -9,
  tuning: {},
};

// ---------------------------------------------------------------- armas: registro y ajustes
const WEAPONS = {};
// def = { id, name, pal, mods:[{id,name,desc}], tuning:{ clave:[valor, min, max, paso, etiqueta] }, create(slot) → instancia }
// instancia = { onPress(aim), onHold(aim, dt), onRelease(aim), update(dt), ready() 0..1, label? }
function addWeapon(def) { WEAPONS[def.id] = def; G.tuning[def.id] = Object.fromEntries(Object.entries(def.tuning).map(([k, v]) => [k, v[0]])); }
const T = id => G.tuning[id];
// hueco = { w, arch (evolución elegida o null), ups { id: true } } — hasMod mira las dos cosas
const hasMod = (slot, m) => !!(slot && (slot.arch === m || (slot.ups && slot.ups[m])));
function slotLevel(s) { const d = WEAPONS[s.w]; return 1 + (d.archetypes && s.arch ? 1 : 0) + Object.keys(s.ups || {}).filter(k => s.ups[k]).length; }
function slotOf(id) { return G.slots.slice(0, G.slotCount).find(s => s.w === id); }
function otherSlots(slot) { return G.slots.slice(0, G.slotCount).filter(s => s !== slot); }

// ---------------------------------------------------------------- player
function makePlayer() {
  return { x: 200, y: GROUND, vx: 0, vy: 0, onGround: true, jumps: 0, face: 1, dashT: 0, dashCd: 0, hp: 100, hpMax: 100,
           aura: null, auraT: 0, castT: 0, lean: 0, squash: 0, dropT: 0, hurtT: 0 };
}
const muzzle = () => { const p = G.player; return { x: p.x + 20 * p.face, y: p.y - 46 }; };
function aimInfo() {
  const m = muzzle(), dx = G.mouse.x - m.x, dy = G.mouse.y - m.y, a = Math.atan2(dy, dx);
  return { x: m.x, y: m.y, ang: a, dx: Math.cos(a), dy: Math.sin(a), tx: G.mouse.x, ty: G.mouse.y };
}
function updatePlayer(dt) {
  const p = G.player, k = G.keys;
  const left = k.KeyA || k.ArrowLeft, right = k.KeyD || k.ArrowRight;
  const want = (right ? 1 : 0) - (left ? 1 : 0);
  const spd = p.speedMul || 1; p.speedMul = 1;               // lo pone cada frame quien acelera al player (Reloj, Corriente)
  p.face = G.mouse.x >= p.x ? 1 : -1;
  if (p.blinkT > 0) { p.blinkT -= dt; }
  if (p.dashT > 0) { p.dashT -= dt; p.vx = p.dashDir * 620; p.vy = 0; }
  else {
    const acc = (p.onGround ? 2400 : 1500) * spd, max = 240 * spd;
    if (want) p.vx = clamp(p.vx + want * acc * dt, -max, max);
    else { const f = (p.onGround ? 2600 : 600) * dt; p.vx = Math.abs(p.vx) <= f ? 0 : p.vx - Math.sign(p.vx) * f; }
    p.vy += GRAV * dt;
  }
  p.dashCd -= dt; p.dropT -= dt; p.hurtT -= dt;
  const oy = p.y;
  p.x = clamp(p.x + p.vx * dt, 20, WW - 20); p.y += p.vy * dt;
  p.onGround = false;
  if (p.y >= GROUND) { p.y = GROUND; p.vy = 0; p.onGround = true; }
  else if (p.vy >= 0 && p.dropT <= 0) for (const pl of PLATFORMS) if (p.x > pl.x0 - 8 && p.x < pl.x1 + 8 && oy <= pl.y + 1 && p.y >= pl.y) { p.y = pl.y; p.vy = 0; p.onGround = true; }
  if (p.onGround) p.jumps = 0;
  p.castT -= dt; p.auraT -= dt;
  p.lean = lerp(p.lean, p.castT > 0 ? -0.1 * p.face : p.vx / 2400, Math.min(1, dt * 14));
}
function onKeyDown(code) {
  const p = G.player;
  if (code === 'KeyW' || code === 'ArrowUp' || code === 'Space') {
    if (p.onGround || p.jumps < 1) { if (!p.onGround) p.jumps++; p.vy = -560 * (G.t < (p.jumpUntil || 0) ? p.jumpMul : 1); p.onGround = false; dustPuff(p.x, p.y); }
  }
  if ((code === 'KeyS' || code === 'ArrowDown') && p.onGround && p.y < GROUND) { p.dropT = 0.25; p.y += 2; }
  if ((code === 'ShiftLeft' || code === 'ShiftRight') && p.dashCd <= 0) { p.dashT = 0.16; p.dashCd = 0.55; p.dashDir = (G.keys.KeyA || G.keys.ArrowLeft) ? -1 : (G.keys.KeyD || G.keys.ArrowRight) ? 1 : p.face; dustPuff(p.x, p.y); }
  if (code === 'KeyQ') swapWeapon(1);
  if (code === 'Digit1') selectSlot(0); if (code === 'Digit2') selectSlot(1); if (code === 'Digit3' && G.slotCount > 2) selectSlot(2);
  if (code === 'KeyR') resetEnemies();
}
function selectSlot(i) {
  if (i === G.cur || i >= G.slotCount) return;
  const s = G.slots[G.cur]; if (G.mouse.down && s.inst && s.inst.onRelease) s.inst.onRelease(aimInfo());
  G.cur = i; if (G.mouse.down) { const n = G.slots[G.cur]; n.inst && n.inst.onPress && n.inst.onPress(aimInfo()); }
  G.swapFx = G.t; G.lastSwap = G.t; if (hasItem('swap')) { G.swapBuff = G.t + T('items').swapTime; G.swapBuffHit = false; }
  sfx(k => SFXLIB.whoosh(k, 2400, 900, 0.12, 0.12)); UI.refreshSlots && UI.refreshSlots();
}
function swapWeapon(d) { selectSlot((G.cur + d + G.slotCount) % G.slotCount); }

// ---------------------------------------------------------------- enemigos de prueba (dianas)
const SPAWNS = [
  { x: 470, kind: 'ground' }, { x: 640, kind: 'ground' }, { x: 900, kind: 'walker', x0: 820, x1: 1040 },
  { x: 700, kind: 'flyer', y: 150 }, { x: 1100, kind: 'flyer', y: 120 }, { x: 1260, kind: 'ground' }, { x: 1330, kind: 'ground' },
  { x: 1420, kind: 'ground' }, { x: 1640, kind: 'flyer', y: 110 }, { x: 1780, kind: 'walker', x0: 1700, x1: 1900 },
];
function makeEnemy(sp) {
  const fly = sp.kind === 'flyer';
  return { sp, kind: sp.kind, fly, x: sp.x, y: fly ? sp.y : GROUND - 44, homeY: fly ? sp.y : GROUND - 44, vx: 0, vy: 0, dir: 1,
           rx: fly ? 22 : 27, ry: fly ? 28 : 39, hp: 120 * (G.settings.hpMul || 1), hpMax: 120 * (G.settings.hpMul || 1), markN: 0, dead: false, deadT: 0, flashT: 0, sqT: 0, shakeT: 0,
           freeze: 0, frozenT: 0, burnT: 0, burnTick: 0, markT: 0, stunT: 0, pull: null, slammed: false, lastHitT: -9, atkT: 2 + Math.random() * 2,
           timeK: 1, stasis: null, vulnT: 0, vulnK: 1, launchT: 0, launchHit: null, vpull: null, slamT: 0, litT: 0, infest: 0 };
}
function resetEnemies() { G.enemies = SPAWNS.map(makeEnemy); }
const alive = () => G.enemies.filter(e => !e.dead);
function enemiesNear(x, y, r) { return alive().filter(e => Math.hypot((e.x - x) / (1 + e.rx / r * 0), e.y - y) < r + Math.max(e.rx, e.ry) * 0.6); }
function insideEnemy(x, y, pad = 0) { return alive().find(e => Math.pow((x - e.x) / (e.rx + pad), 2) + Math.pow((y - e.y) / (e.ry + pad), 2) < 1); }
// primer enemigo que corta el segmento a→b (u = fracción 0..1)
function raycastEnemy(ax, ay, bx, by, skip) {
  let best = null;
  for (const e of alive()) {
    if (skip && skip.has(e)) continue;
    const h = segHitsEllipse(ax, ay, bx, by, e.x, e.y, e.rx, e.ry);
    if (h && (!best || h.u0 < best.u)) best = { e, u: h.u0, x: lerp(ax, bx, h.u0), y: lerp(ay, by, h.u0) };
  }
  return best;
}
// Espejos (g-espejo.js, regla común): segmentos que desvían proyectiles y haces. G.mirrors = [{ x0, y0, x1, y1, alive,
// beam(x, y, ang) → { x, y, ang, mul } }]. Devuelve el primer espejo que corta a→b (u = fracción 0..1).
function segSeg(ax, ay, bx, by, cx, cy, dx, dy) {
  const rX = bx - ax, rY = by - ay, sX = dx - cx, sY = dy - cy, den = rX * sY - rY * sX;
  if (Math.abs(den) < 1e-9) return null;
  const u = ((cx - ax) * sY - (cy - ay) * sX) / den, v = ((cx - ax) * rY - (cy - ay) * rX) / den;
  return u >= 0 && u <= 1 && v >= 0 && v <= 1 ? u : null;
}
function mirrorCross(ax, ay, bx, by, skip) {
  let best = null;
  for (const m of G.mirrors || []) {
    if (!m.alive || m === skip || !G.ents.includes(m)) continue;
    const u = segSeg(ax, ay, bx, by, m.x0, m.y0, m.x1, m.y1);
    if (u !== null && u > 1e-4 && (!best || u < best.u)) best = { u, m, x: lerp(ax, bx, u), y: lerp(ay, by, u) };
  }
  return best;
}
function nearestEnemy(x, y, maxD = 9999, skip) {
  let best = null, bd = maxD;
  for (const e of alive()) { if (skip && skip.has(e)) continue; const d = Math.hypot(e.x - x, e.y - y); if (d < bd) { bd = d; best = e; } }
  return best;
}
function updateEnemies(dt0) {
  for (const e of G.enemies) {
    if (e.dead) { e.deadT += dt0; if (e.deadT > 2.5) Object.assign(e, makeEnemy(e.sp)); continue; }
    // tiempo propio del enemigo (Reloj: lo pone cada frame quien lo ralentiza; 0 = parado del todo)
    const tk = e.timeK === undefined ? 1 : e.timeK, dt = dt0 * tk; e.timeK = 1; e.timeSlow = tk < 1;
    if (tk === 0) { e.vx = 0; e.vy = 0; }
    e.flashT -= dt0; e.sqT -= dt0; e.shakeT -= dt0; e.markT -= dt; e.stunT -= dt;
    if (e.launchT > 0) { e.launchT -= dt; if (Math.abs(e.vx) > 220) launchCollide(e); }
    const frozen = e.frozenT > 0;
    if (frozen) { e.frozenT -= dt; if (e.frozenT <= 0) e.freeze = 0; }
    else e.freeze = Math.max(0, e.freeze - dt * 0.18);
    if (e.burnT > 0) { e.burnT -= dt; e.burnTick -= dt; if (e.burnTick <= 0) { e.burnTick = 0.35; damage(e, { dmg: 4, elem: 'burnTick', silentFlash: true, color: '#ffb35a' }); if (e.dead) continue; } }
    // movimiento propio
    if (!frozen && e.stunT <= 0 && e.kind === 'walker' && Math.abs(e.vx) < 30 && !e.pull) {
      e.x += e.dir * 40 * dt; if (e.x > e.sp.x1) e.dir = -1; if (e.x < e.sp.x0) e.dir = 1;
    }
    if (e.vpull && G.t < e.vpull.until && !e.pull) e.pull = { x: e.vpull.x, y: e.vpull.y, s: 240 };
    // atracción (singularidad): la pone cada frame quien atrae
    if (e.pull) {
      const dx = e.pull.x - e.x, dy = e.pull.y - e.y, d = Math.hypot(dx, dy) || 1, s = e.pull.s * (frozen ? 0.5 : 1);
      if (d > 14) { e.x += dx / d * s * dt; if (e.pull.lift) e.y += dy / d * s * dt; }
      e.pull = null;
    }
    // empujones
    e.x += e.vx * dt; e.vx *= Math.pow(e.launchT > 0 ? 0.25 : 0.02, dt);   // lanzado: resbala mucho más
    if (e.fly) {
      if (!e.slammed) { e.y += e.vy * dt; e.vy *= Math.pow(0.05, dt); e.y = lerp(e.y, e.homeY + Math.sin(G.t * 2 + e.sp.x) * 6, Math.min(1, dt * 1.2)); e.x = lerp(e.x, e.sp.x, Math.min(1, dt * 0.25)); }
      else { e.vy += GRAV * dt; e.y += e.vy * dt; if (e.y >= GROUND - 32) { e.y = GROUND - 32; if (e.vy > 300) { hitFx('slam', e.x, GROUND); } e.vy = 0; e.slamT = (e.slamT || 0) + dt; if (e.slamT > 1.6) { e.slammed = false; e.slamT = 0; } } }
    } else {
      e.vy += GRAV * dt; e.y += e.vy * dt;
      if (e.y >= GROUND - 44) { e.y = GROUND - 44; e.vy = 0; }
      if (!e.pull && e.kind !== 'walker') e.x = lerp(e.x, e.sp.x, Math.min(1, dt * 0.4));
    }
    e.x = clamp(e.x, 20, WW - 20);
    // ataque opcional: bala lenta hacia el player
    if (G.settings.enemyAttacks && !frozen && e.stunT <= 0) { e.atkT -= dt; if (e.atkT <= 0) { e.atkT = 2.5 + Math.random() * 1.5; enemyShot(e); } }
  }
}
// Regla común «lanzado» (Ráfaga, Látigo · Garfio): un enemigo lanzado (golpe con `launch`) que choca a velocidad con otro
// hace daño a los dos según la velocidad; si va rápido es golpe fuerte (estampar a uno contra un congelado = añicos). Con
// `pins` el golpeado sale lanzado también (bolos).
function launchCollide(e) {
  const tr = T('react'), sp = Math.abs(e.vx);
  for (const o of alive()) {
    if (o === e || (e.launchHit && e.launchHit.has(o))) continue;
    if (Math.pow((o.x - e.x) / (o.rx + e.rx), 2) + Math.pow((o.y - e.y) / (o.ry + e.ry), 2) >= 1) continue;
    (e.launchHit = e.launchHit || new Set()).add(o);
    const d = tr.impactDmg + sp * tr.impactPerSpeed, heavy = sp > 480, dir = Math.sign(e.vx);
    const base = { dmg: d, heavy, slot: e.launchSlot, proc: true, shake: heavy ? 5 : 2, hitstop: heavy ? 0.04 : 0.015, color: '#e8f0ff' };
    damage(o, Object.assign({}, base, { kx: e.vx * 0.6, launch: e.launchPins ? 0.5 : 0, pins: e.launchPins }));
    damage(e, Object.assign({}, base, { kx: 0 }));
    e.vx *= 0.45;
    const ix = (o.x + e.x) / 2, iy = (o.y + e.y) / 2;
    addFx(0.6, (ctx, layer, t0) => impact(ctx, G.t, layer, t0, ix, iy, heavy ? 0.7 : 0.45, PAL.wind || PAL.set, { dir: dir > 0 ? Math.PI : 0, spread: 2.6, seed: Math.floor(ix) }));
    sfx(k => { const t = nowT(); vThump(t, 120, 50, 0.18 * k, heavy ? 0.6 : 0.35, 2); vNoise(t, 0.08 * k, 0.25, 'bandpass', 1800, 600, 1, { att: 0.002 }); });
    text(ix, iy - 50, 'CHOQUE', '#e8f0ff', heavy);
  }
}
function enemyShot(e) {
  // señuelos (Tótem · Señuelo): si hay uno cerca del que dispara, la bala va a por él
  const dec = (G.decoys || []).find(d => d.alive && G.ents.includes(d) && Math.hypot(d.x - e.x, d.y - e.y) < d.r);
  const p = G.player, a = dec ? Math.atan2(dec.y - e.y, dec.x - e.x) : Math.atan2(p.y - 40 - e.y, p.x - e.x), sp = 220;
  // bala enemiga: capturable por la Singularidad con Escudo de vacío (hostile); al soltarla pasa a ser del player (friendly)
  G.ents.push({ kind: 'enemyShot', capturable: true, hostile: true, state: 'fly', x: e.x, y: e.y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 4,
    update(dt) {
      if (this.captured) return true;
      dt *= this.timeK === undefined ? 1 : this.timeK; this.timeK = 1;          // Reloj: las balas también se ralentizan
      this.x += this.vx * dt; this.y += this.vy * dt; this.life -= dt;
      if (this.friendly) { const h = insideEnemy(this.x, this.y, 4); if (h) { damage(h, { dmg: this.dmg * (this.dmgMul || 1), slot: this.slot, kx: Math.sign(this.vx) * 80 }); return false; } }
      else {
        const d = (G.decoys || []).find(d => d.alive && G.ents.includes(d) && Math.hypot(d.x - this.x, d.y - this.y) < 26); if (d) { d.hurt(8); return false; }
        const p = G.player; if (Math.abs(this.x - p.x) < 14 && this.y > p.y - 70 && this.y < p.y) { hurtPlayer(8); return false; }
      }
      return this.life > 0 && this.y < GROUND && this.x > -50 && this.x < WW + 50; },
    draw(ctx, layer) {
      if (layer !== 'front') return;
      if (this.friendly || this.captured) { glow(ctx, this.x, this.y, 16, [150, 100, 255], 0.7); disc(ctx, this.x, this.y, 5, '#8a5cff', 1, layer); disc(ctx, this.x, this.y, 2.4, '#fff', 1, layer); }
      else { glow(ctx, this.x, this.y, 16, [255, 80, 80], 0.6); disc(ctx, this.x, this.y, 5, '#ff6a6a', 1, layer); disc(ctx, this.x, this.y, 2.4, '#fff', 1, layer); } } });
}
function hurtPlayer(d) { const p = G.player; if (p.hurtT > 0) return; if (p.shield > 0) { const b = Math.min(p.shield, d); p.shield -= b; d -= b; } p.hp = Math.max(0, p.hp - d); p.hurtT = 0.5; G.shake += 4; text(p.x, p.y - 90, '-' + d, '#ff6a6a'); if (p.hp <= 0) p.hp = p.hpMax; }
function healPlayer(d) { const p = G.player; const before = p.hp; if (tier('life') >= 3 && p.hp + d > p.hpMax) p.shield = Math.min(30, (p.shield || 0) + p.hp + d - p.hpMax); p.hp = Math.min(p.hpMax, p.hp + d); if (p.hp > before + 0.5) { p.aura = PAL.life.g1; p.auraT = 0.4; } return p.hp - before; }

// ---------------------------------------------------------------- daño, estados y reacciones
// o = { dmg, kx, ky (empujón), slot, heavy (golpe fuerte: rompe congelados), elem: 'ice'|'fire', freeze (0..1 que suma),
//       burn (s), mark, hitstop, shake, color, silentFlash, noReact,
//       proc (golpe nacido de otro golpe: no dispara los efectos «al golpear» de las armas → sin bucles),
//       raw (daño ya multiplicado por la build: no se vuelve a multiplicar), launch (s lanzado: choca con otros), pins (bolos) }
function damage(e, o) {
  if (!e || e.dead) return 0;
  let dmg = o.dmg || 0, label = null;
  const it = T('items');
  let gilded = false;
  if (o.slot && !o.dot && !o.raw) {                           // build sobre golpes de arma (no sobre daño continuo)
    if (tier('fire') >= 1) dmg *= it.fire2Damage;
    if (hasItem('sharp')) dmg *= it.sharpMul;
    if (hasItem('espadaO')) dmg *= 1.2;
    const iced = e.freeze > 0 || e.frozenT > 0;
    if (iced && hasItem('grimorio')) dmg *= it.grimorioMul;
    if (iced && hasItem('anillo')) dmg += e.hpMax * it.anilloPct;
    if (G.empower && G.empower > 1) { dmg *= G.empower; o.heavy = true; if (!G.empowerHit) { G.empowerHit = true; G.empowerEnd = G.t + 0.12; } }
    if (hasItem('swap') && G.t < G.swapBuff) { dmg *= it.swapMul; o.heavy = true; if (!G.swapBuffHit) { G.swapBuffHit = true; G.swapBuff = G.t + 0.12; } }
    let gc = tier('gold') >= 2 ? it.gildChance : 0;
    if (hasItem('botasO')) gc += Math.min(it.botasOMax, it.botasOBase + it.botasOPerDmg * dmg);
    G.goldHits = (G.goldHits || 0) + 1;
    if (Math.random() < gc || (hasItem('guantesO') && G.goldHits % 4 === 0)) { dmg *= 2; gilded = true; }
    if (e.litT > G.t && Math.random() < (e.litChance || 0)) { dmg *= 2; text(e.x + 14, e.y - e.ry - 30, 'CRÍTICO', '#ffe27a', true); }   // iluminado (Enjambre · Faroles)
  }
  const frozen = e.frozenT > 0, R = G.settings.reactions && !o.noReact;
  // reacciones
  if (R && frozen && o.elem === 'fire') { dmg *= 2; e.frozenT = 0; e.freeze = 0; label = 'DESHIELO'; hitFx('steam', e.x, e.y); }
  else if (R && frozen && o.heavy) { shatter(e); label = 'AÑICOS'; }
  else if (R && e.burnT > 0 && o.elem === 'ice') { e.burnT = 0; label = 'VAPOR'; vapor(e); }
  if (o.elem === 'ice' && o.freeze && !(label === 'AÑICOS')) { e.freeze = Math.min(1, e.freeze + o.freeze); if (e.freeze >= 1 && e.frozenT <= 0) { e.frozenT = T('react').frozenTime * (tier('ice') >= 3 ? 1.5 : 1); sfx(SFXLIB.freeze.bind(null, 1, 0.3)); } }
  if (o.burn && o.elem === 'fire' && label !== 'DESHIELO') { e.burnT = Math.max(e.burnT, o.burn); e.burnTick = Math.min(e.burnTick, 0.1); }
  if (e.vulnT > G.t && dmg > 0) dmg *= e.vulnK || 1;          // vulnerable (Ráfaga · Tornado, Jabalina · Clavado…): recibe más de todo
  if (o.launch && e.frozenT <= 0) { e.launchT = o.launch; e.launchHit = new Set(); e.launchSlot = o.slot; e.launchPins = o.pins; }
  const banked = !!(e.stasis && !o.release);                  // Estasis (Reloj): el daño se guarda y llega todo junto al soltar
  if (banked) { e.stasis.bank += dmg; e.lastHitT = G.t; } else { e.hp -= dmg; logDmg(dmg); e.lastHitT = G.t; }
  if (gilded) text(e.x - 14, e.y - e.ry - 30, 'ORO', '#ffc93c', true);
  if (o.slot && !o.dot && tier('life') >= 2 && dmg > 0) { G.lifeAcc = (G.lifeAcc || 0) + dmg * 0.1; if (G.lifeAcc >= 1) { const hv = Math.floor(G.lifeAcc); G.lifeAcc -= hv; healPlayer(hv); } }
  if (!o.silentFlash) { e.flashT = 0.12; e.sqT = 0.2; e.shakeT = 0.22; }
  if (e.frozenT <= 0) { e.vx += o.kx || 0; if (o.ky) { e.vy += o.ky; } }
  if (dmg > 0) {                                             // golpes pequeños seguidos se suman en un solo número
    const colr = banked ? '#9fb4bc' : o.color || (o.elem === 'ice' ? '#bdf3ff' : o.elem === 'fire' ? '#ffb35a' : '#ffffff');
    if (dmg < 30 && e._txt && G.t - e._txt.t0 < 0.35 && G.texts.includes(e._txt)) { e._txt.v += dmg; e._txt.s = Math.round(e._txt.v) + ''; e._txt.t0 = G.t - 0.05; e._txt.big = e._txt.v >= 30; }
    else { text(e.x + (Math.random() - 0.5) * 20, e.y - e.ry - 12, Math.round(dmg) + '', colr, dmg >= 30); if (dmg < 30) { e._txt = G.texts[G.texts.length - 1]; e._txt.v = dmg; } }
  }
  if (label) text(e.x, e.y - e.ry - 34, label, label === 'AÑICOS' ? '#cdf6ff' : label === 'VAPOR' ? '#e0e8ff' : '#ffcf7a', true);
  if (G.empowerEnd && G.t > G.empowerEnd) { G.empower = 0; G.empowerHit = false; G.empowerEnd = 0; }
  if (o.hitstop && G.settings.hitstop) G.hitStop = Math.max(G.hitStop, o.hitstop);
  if (o.shake) G.shake += o.shake;
  if (o.frame && G.settings.frames) G.dark = Math.max(G.dark, 0.05);
  // sistema de marcas: con el Golpe estelar + mod Marcas equipado, los golpes de las otras armas marcan
  // marcas: las da el Cazador (evolución del Golpe estelar); con Acumulación se apilan hasta 3 (un golpe por ataque)
  const hunter = slotOf('star');
  if (o.slot && o.slot.w !== 'star' && !o.dot && hunter && hasMod(hunter, 'hunter')) {
    const fresh = G.t - (e.markHitT || -9) > 0.25;
    e.markN = hasMod(hunter, 'stack') ? Math.min(3, (e.markT > 0 ? e.markN : 0) + (fresh ? 1 : 0)) || 1 : 1;
    e.markHitT = G.t; e.markT = 8;
  }
  if (o.slot && !o.dot && dmg > 0) {
    if (hasItem('const')) { G.constHits = (G.constHits || 0) + 1; if (G.constHits >= it.constEvery) { G.constHits = 0; if (G.spawnMiniStrike) G.spawnMiniStrike(e); } }
    if (hasItem('void') && o.heavy) voidPulse(e.x, e.y);
  }
  // armas con efectos «al golpear» (también la que no llevas en la mano): Enjambre · Colmena, Látigo · Cadena, Reloj · Eco…
  if (o.slot && !o.proc && !o.dot && dmg > 0) for (const s of G.slots.slice(0, G.slotCount)) if (s.inst && s.inst.onAnyHit) s.inst.onAnyHit(e, o, dmg);
  if (e.hp <= 0 && !banked) {
    e.dead = true; e.deadT = 0; hitFx('die', e.x, e.y, e);
    onKillBuild(e, o);
    if (o.onKill) o.onKill(e);
    killHooks(e, o);
  }
  return dmg;
}
function killHooks(e, o) { for (const s of G.slots.slice(0, G.slotCount)) if (s.inst && s.inst.onAnyKill) s.inst.onAnyKill(e, o); }
// vulnerable: recibe ×k de todo durante s segundos (se queda con el mayor)
function makeVuln(e, k, s) { if (e.vulnT > G.t && e.vulnK > k) return; e.vulnK = k; e.vulnT = G.t + s; }
function damageArea(x, y, r, o, falloff = true) {
  const hit = [];
  for (const e of alive()) {
    const d = Math.hypot(e.x - x, e.y - y) - Math.max(e.rx, e.ry) * 0.6;
    if (d > r) continue;
    const k = falloff ? 1 - 0.5 * clamp(d / r) : 1, dir = Math.sign(e.x - x) || 1;
    damage(e, Object.assign({}, o, { dmg: (o.dmg || 0) * k, kx: (o.kx || 0) * dir * k, ky: o.ky ? o.ky * k : 0 }));
    hit.push(e);
  }
  return hit;
}
// daño directo (reacciones): resta, número y muerte
function logDmg(d) { if (d > 0) { G.dmgLog.push({ t: G.t, d }); G.dmgTotal += d; } }
function hurtEnemy(e, d, color, big) {
  if (e.dead) return;
  if (e.stasis) { e.stasis.bank += d; e.lastHitT = G.t; text(e.x + 10, e.y - e.ry, Math.round(d) + '', '#9fb4bc', big); return; }
  e.hp -= d; logDmg(d); e.lastHitT = G.t; text(e.x + 10, e.y - e.ry, Math.round(d) + '', color, big);
  if (e.hp <= 0) { e.dead = true; e.deadT = 0; hitFx('die', e.x, e.y, e); onKillBuild(e, {}); killHooks(e, {}); }
}
// Reacción añicos: daño extra + fragmentos que dañan alrededor
function shatter(e) {
  const tr = T('react');
  e.frozenT = 0; e.freeze = 0; hurtEnemy(e, tr.shatterDmg * (tier('ice') >= 3 ? 2 : 1), '#cdf6ff', true);
  for (const o of alive()) if (o !== e && Math.hypot(o.x - e.x, o.y - e.y) < tr.shatterRadius) damage(o, { dmg: tr.shatterSplash, kx: Math.sign(o.x - e.x) * 120, elem: 'ice', freeze: 0.25, noReact: true });
  hitFx('shatter', e.x, e.y); G.shake += 7; if (G.settings.frames) G.dark = 0.05; if (G.settings.hitstop) G.hitStop = Math.max(G.hitStop, 0.06);
  sfx(SFXLIB.shatter.bind(null, 1, 1.1));
}
function vapor(e) {
  const tr = T('react');
  for (const o of alive()) if (Math.hypot(o.x - e.x, o.y - e.y) < tr.vaporRadius) { hurtEnemy(o, tr.vaporDmg, '#e0e8ff'); o.stunT = 0.6; o.flashT = 0.15; }
  hitFx('steam', e.x, e.y); sfx(k => { const t = nowT(); vNoise(t, 0.5 * k, 0.35, 'bandpass', 2500, 600, 0.6, { att: 0.005, wet: 0.3 }); vThump(t, 90, 40, 0.3 * k, 0.5, 2); });
}
// ajustes de las reacciones (sistema común, no de un arma)
G.tuning.react = { frozenTime: 2.5, shatterDmg: 45, shatterRadius: 80, shatterSplash: 15, vaporDmg: 25, vaporRadius: 70, impactDmg: 10, impactPerSpeed: 0.035 };
const REACT_TUNING = { frozenTime: [2.5, 0.5, 6, 0.1, 'Congelado: duración (s)'], shatterDmg: [45, 0, 150, 1, 'Añicos: daño extra'], shatterRadius: [80, 0, 200, 1, 'Añicos: radio'],
                       shatterSplash: [15, 0, 80, 1, 'Añicos: daño alrededor'], vaporDmg: [25, 0, 100, 1, 'Vapor: daño'], vaporRadius: [70, 0, 200, 1, 'Vapor: radio'],
                       impactDmg: [10, 0, 80, 1, 'Lanzado: daño base del choque'], impactPerSpeed: [0.035, 0, 0.2, 0.005, 'Lanzado: + daño por px/s de velocidad'] };

// ---------------------------------------------------------------- build: items con 2 etiquetas y sinergias 2/4/6 (como el juego)
// Igual que en Unity (BuildTag, SynergyConfig): cada item lleva UNA etiqueta elemental (Hielo, Fuego, Oro) y UNA universal
// (Tanque, Prisa, Robo de vida, Reset); cada etiqueta suma puntos y activa tramos a 2 / 4 / 6. Propuesta del usuario (2026-10-19):
// el elemento se APLICA a los enemigos con 4 de ese elemento (con 6 huecos sólo cabe uno → nunca dos estados a la vez); con 2 es
// un bonus compatible (2 hielo = menos recarga, 2 fuego = más daño), así se puede ir 4 hielo + 2 fuego o al revés.
// Un item además puede tener efecto propio (los de hielo y oro copian los del juego; los generales son los de las pruebas).
const TAGS = {
  ice:  { name: 'Hielo', kind: 'elem', color: '#9ef2ff', tiers: ['−15 % de recarga en todas las armas', 'Tus golpes aplican HIELO (cada arma a su manera; congelado + golpe fuerte = añicos)', 'Congelado dura +50 % y los añicos hacen el doble'] },
  fire: { name: 'Fuego', kind: 'elem', color: '#ffb35a', tiers: ['+15 % de daño en todas las armas', 'Tus golpes aplican FUEGO (quemadura; congelado + fuego = deshielo)', 'Al morir un enemigo quemado, la quemadura salta a los cercanos'] },
  gold: { name: 'Oro', kind: 'elem', color: '#ffc93c', tiers: ['+15 % de monedas (sin efecto en el prototipo)', '+5 % de golpes dorados (×2 de daño)', 'Botín ×3 con la marca de oro (sin efecto en el prototipo)'] },
  tank: { name: 'Tanque', kind: 'univ', color: '#c8ccd4', tiers: ['+20 % de vida máxima', 'Regeneras 1 de vida por segundo', 'Escudo de 30 al bajar del 30 % de vida (una vez)'] },
  haste: { name: 'Prisa', kind: 'univ', color: '#c8ccd4', tiers: ['Con un enemigo con hielo en pantalla, la recarga corre ×1,5', '−20 % de recarga', '−35 % de recarga'] },
  life: { name: 'Robo de vida', kind: 'univ', color: '#c8ccd4', tiers: ['Cada baja cura 3', '10 % de robo de vida en los golpes de arma', 'La curación sobrante se convierte en escudo (hasta 30)'] },
  reset: { name: 'Reset', kind: 'univ', color: '#c8ccd4', tiers: ['Cada baja quita 2 s a la recarga del arma activa', 'Cada baja quita también 2 s a la de reserva', 'Cada baja recarga del todo el arma activa'] },
};
const MAX_ITEMS = 6;
// rareza como en el juego: común · raro (azul) · épico · legendario
const RARITY = { 'común': { w: 50, color: '#c8ccd4' }, 'raro': { w: 30, color: '#5fb8ff' }, 'épico': { w: 15, color: '#c77dff' }, 'legendario': { w: 5, color: '#ffc93c' } };
const ITEMS = [
  // --- hielo (los 6 del juego, efecto adaptado: «ralentizado» = con hielo acumulado)
  { id: 'anillo', name: 'Anillo Glacial', rarity: 'raro', tags: ['ice', 'life'], desc: 'Los golpes a enemigos con hielo hacen un extra según su vida máxima.' },
  { id: 'baston', name: 'Bastón Helado', rarity: 'legendario', tags: ['ice', 'haste'], desc: 'Al matar, el siguiente ataque hace ×3 y el arma activa se recarga.' },
  { id: 'botasR', name: 'Botas Rúnicas', rarity: 'épico', tags: ['ice', 'haste'], desc: 'Con un enemigo con hielo en pantalla, la recarga corre ×1,5.' },
  { id: 'capa', name: 'Capa de Escarcha', rarity: 'épico', tags: ['ice', 'reset'], desc: 'Al matar, un fragmento de hielo sale del cuerpo hacia el enemigo más cercano.' },
  { id: 'grimorio', name: 'Grimorio Glacial', rarity: 'raro', tags: ['ice', 'reset'], desc: 'Los enemigos con hielo reciben +30 % de daño.' },
  { id: 'yelmo', name: 'Yelmo Rúnico', rarity: 'legendario', tags: ['ice', 'life'], desc: 'Cada 5 bajas, estallido de hielo a tu alrededor que congela.' },
  // --- fuego (generales de las pruebas)
  { id: 'dup', name: 'Duplicador', rarity: 'legendario', tags: ['fire', 'reset'], desc: 'Dobla los proyectiles de todas tus armas (dos singularidades se funden en una mayor).' },
  { id: 'swap', name: 'Gatillo de cambio', rarity: 'épico', tags: ['fire', 'haste'], desc: 'Tras cambiar de arma, el siguiente ataque hace más daño y es golpe fuerte.' },
  { id: 'const', name: 'Constelación', rarity: 'épico', tags: ['fire', 'reset'], desc: 'Cada pocos golpes cae una lanza pequeña sobre el último enemigo golpeado.' },
  { id: 'void', name: 'Corazón de vacío', rarity: 'raro', tags: ['fire', 'tank'], desc: 'Los golpes fuertes atraen un instante a los enemigos cercanos.' },
  { id: 'sharp', name: 'Cristal afilado', rarity: 'común', tags: ['fire', 'life'], desc: '+20 % de daño en todos los golpes de arma.' },
  { id: 'blood', name: 'Sangre de cristal', rarity: 'común', tags: ['fire', 'life'], desc: 'Cada baja cura un poco.' },
  { id: 'gear', name: 'Engranaje', rarity: 'común', tags: ['ice', 'haste'], desc: '−15 % de recarga en todas las armas.' },
  // --- oro (del juego, sólo lo que tiene sentido sin monedas)
  { id: 'espadaO', name: 'Espada de Oro', rarity: 'épico', tags: ['gold', 'life'], desc: '+20 % de daño (y monedas al matar, sin efecto aquí).' },
  { id: 'botasO', name: 'Botas de Oro', rarity: 'legendario', tags: ['gold', 'haste'], desc: 'Golpes dorados (×2) con probabilidad que sube con el daño del golpe.' },
  { id: 'guantesO', name: 'Guanteletes de Oro', rarity: 'épico', tags: ['gold', 'haste'], desc: 'Cada 4 golpes, uno dorado (×2).' },
];
const hasItem = id => !!G.build.items[id];
const ownedItems = () => ITEMS.filter(i => G.build.items[i.id]);
function tagCount(tag) { return Math.min(6, ownedItems().reduce((n, i) => n + (i.tags.includes(tag) ? 1 : 0), 0)); }
function tier(tag) { const c = tagCount(tag); return c >= 6 ? 3 : c >= 4 ? 2 : c >= 2 ? 1 : 0; }
// elemento aplicado = el elemental con 4+ (si por pruebas hubiera dos, el que más tenga)
function elemOf() { const c = ['ice', 'fire'].map(t => [t, tagCount(t)]).filter(x => x[1] >= 4).sort((a, b) => b[1] - a[1]); return c.length ? c[0][0] : null; }
const copies = () => hasItem('dup') ? 2 : 1;
const elemPal = base => PAL[elemOf() || base];
function cdMul() { let m = 1; if (tier('ice') >= 1) m *= T('items').ice2Cooldown; if (hasItem('gear')) m *= T('items').gearMul; const h = tier('haste'); if (h === 2) m *= 0.8; if (h >= 3) m *= 0.65; return m; }
const iceOnScreen = () => alive().some(e => (e.freeze > 0 || e.frozenT > 0) && Math.abs(e.x - G.player.x) < VW);
function cdRate() { return ((tier('haste') >= 1 || hasItem('botasR')) && iceOnScreen()) ? 1.5 : 1; }
G.tuning.items = { ice2Cooldown: 0.85, fire2Damage: 1.15, sharpMul: 1.2, gearMul: 0.85, swapMul: 1.6, swapTime: 1.5, constEvery: 8, voidRadius: 130, voidTime: 0.4, bloodHeal: 3,
                   anilloPct: 0.04, grimorioMul: 1.3, yelmoKills: 5, yelmoDamage: 40, yelmoRadius: 160, gildChance: 0.05, botasOBase: 0.05, botasOPerDmg: 0.004, botasOMax: 0.5 };
const ITEM_TUNING = {
  ice2Cooldown: [0.85, 0.5, 1, 0.01, 'Hielo 2: × recarga'], fire2Damage: [1.15, 1, 2, 0.01, 'Fuego 2: × daño'],
  sharpMul: [1.2, 1, 3, 0.05, 'Cristal afilado: × daño'], gearMul: [0.85, 0.3, 1, 0.01, 'Engranaje: × recarga'], swapMul: [1.6, 1, 4, 0.05, 'Gatillo: × daño'],
  swapTime: [1.5, 0.2, 5, 0.1, 'Gatillo: ventana (s)'], constEvery: [8, 2, 30, 1, 'Constelación: cada N golpes'], voidRadius: [130, 30, 400, 5, 'Corazón de vacío: radio'],
  voidTime: [0.4, 0.1, 2, 0.05, 'Corazón de vacío: duración (s)'], bloodHeal: [3, 0, 30, 1, 'Sangre de cristal: cura por baja'],
  anilloPct: [0.04, 0, 0.2, 0.005, 'Anillo Glacial: extra (× vida máx. del enemigo)'], grimorioMul: [1.3, 1, 3, 0.05, 'Grimorio: × daño a enemigos con hielo'],
  yelmoKills: [5, 1, 20, 1, 'Yelmo: bajas por estallido'], yelmoDamage: [40, 0, 200, 1, 'Yelmo: daño'], yelmoRadius: [160, 30, 400, 5, 'Yelmo: radio'],
  gildChance: [0.05, 0, 0.5, 0.01, 'Oro 4: probabilidad de golpe dorado'], botasOBase: [0.05, 0, 0.5, 0.01, 'Botas de Oro: probabilidad base'],
  botasOPerDmg: [0.004, 0, 0.05, 0.001, 'Botas de Oro: + por punto de daño'], botasOMax: [0.5, 0, 1, 0.05, 'Botas de Oro: máximo'],
};
// efectos que dependen del tiempo (regeneración, escudo) y de las bajas
function updateBuild(dt) {
  const p = G.player, hm = 100 * (tier('tank') >= 1 ? 1.2 : 1);
  if (p.hpMax !== hm) { p.hp = Math.min(hm, p.hp * hm / p.hpMax); p.hpMax = hm; }
  if (tier('tank') >= 2) { p.hp = Math.min(p.hpMax, p.hp + dt); }
  if (tier('tank') >= 3 && !p.shieldUsed && p.hp < p.hpMax * 0.3) { p.shieldUsed = true; p.shield = Math.max(p.shield || 0, 30); text(p.x, p.y - 100, 'ESCUDO', '#c8ccd4', true); }
}
function onKillBuild(e, o) {
  const it = T('items'), p = G.player;
  if (hasItem('blood')) { const h = healPlayer(it.bloodHeal); if (h > 0) text(p.x, p.y - 92, '+' + Math.round(h), '#ff9fb4'); }
  if (tier('life') >= 1) { const h = healPlayer(3); if (h > 0) text(p.x, p.y - 100, '+' + Math.round(h), '#ff9fb4'); }
  const r = tier('reset'), act = G.slots[G.cur], other = otherSlots(act);
  if (r >= 1 && act.inst && act.inst.refund) act.inst.refund(r >= 3 ? 99 : 2);
  if (r >= 2) for (const so of other) if (so.inst && so.inst.refund) so.inst.refund(2);
  if (hasItem('baston')) { G.empower = 3; if (act.inst && act.inst.refund) act.inst.refund(99); text(p.x, p.y - 108, '×3', '#9ef2ff', true); }
  if (hasItem('capa')) { const n = nearestEnemy(e.x, e.y, 400); if (n) G.ents.push(iceBolt(e.x, e.y, n, Math.max(8, (o.dmg || 10) * 0.5))); }
  if (hasItem('yelmo')) { G.yelmoKills = (G.yelmoKills || 0) + 1; if (G.yelmoKills >= it.yelmoKills) { G.yelmoKills = 0; yelmoBurst(); } }
  if (tier('fire') >= 3 && e.burnT > 0) for (const o2 of alive()) if (Math.hypot(o2.x - e.x, o2.y - e.y) < 110) { o2.burnT = Math.max(o2.burnT, 2.5); o2.burnTick = 0.1; }
}
function iceBolt(x, y, target, dmg) {                         // Capa de Escarcha: fragmento que persigue al más cercano
  let vx = 0, vy = -200; const P = PAL.ice; const trail = [];
  return { kind: 'bolt', update(dt) {
      if (target.dead) return false;
      const a = Math.atan2(target.y - y, target.x - x); vx = lerp(vx, Math.cos(a) * 700, Math.min(1, dt * 8)); vy = lerp(vy, Math.sin(a) * 700, Math.min(1, dt * 8));
      x += vx * dt; y += vy * dt; trail.unshift({ x, y }); if (trail.length > 8) trail.pop();
      if (Math.hypot(target.x - x, target.y - y) < target.rx) { damage(target, { dmg, elem: 'ice', freeze: 0.4, kx: Math.sign(vx) * 80 }); return false; }
      return true; },
    draw(ctx, layer) { if (layer === 'back' && trail.length > 1) ribbon(ctx, trail, q => 6 * (1 - q), P.mid, 0.8, layer); crystalShard(ctx, x, y, Math.atan2(vy, vx), 0.7, P, 1, layer); } };
}
function yelmoBurst() {
  const it = T('items'), p = G.player, cx = p.x, cy = p.y - 40;
  damageArea(cx, cy, it.yelmoRadius, { dmg: it.yelmoDamage, kx: 200, elem: 'ice', freeze: 1, shake: 6 });
  addFx(1.6, (ctx, layer, t0) => frostNova(ctx, G.t, layer, t0, cx, it.yelmoRadius));
  sfx(SND.seed_nova.fn);
}
function voidPulse(x, y) {
  const it = T('items'); if (G.t - (G._voidT || -9) < 0.2) return; G._voidT = G.t;
  for (const e of alive()) if (Math.hypot(e.x - x, e.y - y) < it.voidRadius) e.vpull = { x, y, until: G.t + it.voidTime };
  addFx(0.45, (ctx, layer, t0) => {
    const u = seg(G.t, t0, t0 + 0.4);
    for (let i = 0; i < 2; i++) { const r = it.voidRadius * (1 - easeIn(u)) * (1 - i * 0.3); ellArc(ctx, x, y, r, r, layer === 'back' ? Math.PI : 0, layer === 'back' ? TAU : Math.PI, 2, PAL.void.light, 0.6 * (1 - u), layer, { taper: 0, wob: 0.15, seed: i + 7 }); }
  });
}
// zona de fuego en el suelo (cráter, charco): quema a quien la pisa
function fireZone(x, halfW, life, burn, slot) {
  const t0 = G.t; let tick = 0;
  return {
    kind: 'zone', fire: true, x, y: GROUND - 10, halfW,
    update(dt) {
      tick -= dt;
      if (tick <= 0) { tick = 0.3; for (const e of alive()) if (Math.abs(e.x - x) < halfW && GROUND - (e.y + e.ry) < 30) damage(e, { dmg: 3, elem: 'fire', burn, slot, silentFlash: true, dot: true }); }
      return G.t - t0 < life + 0.35;
    },
    draw(ctx, layer) {
      const end = t0 + life, n = Math.max(4, Math.round(halfW / 14));
      for (let i = 0; i < n; i++) {
        const R = rand(i + Math.floor(x)), d = (R() - 0.5) * halfW * 1.7, on = seg(G.t, t0 + Math.abs(d) / 400, t0 + Math.abs(d) / 400 + 0.1) * (1 - seg(G.t, end - 0.2 + R() * 0.2, end + 0.35)), lay = R() < 0.45 ? 'back' : 'front';
        if (lay === layer) flame(ctx, x + d, GROUND + 2, (24 + R() * 22) * on, (7 + R() * 5) * Math.max(0.3, on), G.t, i, on, layer);
      }
      if (layer === 'back') {
        const a = (1 - seg(G.t, end, end + 0.35)) * 0.7;
        ctx.save(); ctx.globalAlpha = a; ctx.fillStyle = col('#3a1208', layer); ctx.beginPath(); ctx.ellipse(x, GROUND + 4, halfW, 9, 0, 0, TAU); ctx.fill(); ctx.restore();
        groundRing(ctx, x, GROUND + 3, halfW * 0.94, 2.4, PAL.fire.mid, a, layer, { taper: 0, wob: 0.1, seed: 9 });
      }
    },
  };
}

// ---------------------------------------------------------------- efectos de un solo uso (dibujo de core.js / fx.js con el tiempo del juego)
function addFx(life, draw) { const t0 = G.t; G.ents.push({ fx: true, update() { return G.t - t0 < life; }, draw(ctx, layer) { draw(ctx, layer, t0); } }); }
function hitFx(kind, x, y, e) {
  const I = PAL.ice;
  if (kind === 'shatter') addFx(1.1, (ctx, layer, t0) => {
    const u = G.t - t0;
    if (layer === 'front') { glow(ctx, x, y, 90 * (1 - seg(u, 0, 0.25)), I.g1, 0.7 * (1 - seg(u, 0, 0.25))); star4(ctx, x, y, 50 * (1 - seg(u, 0, 0.18) * 0.5), 0.2, I.core, 1 - seg(u, 0, 0.18), layer); }
    const ru = seg(u, 0, 0.35);
    for (const [k, c, w] of [[1, I.mid, 3.4], [0.75, I.core, 1.6]]) ellArc(ctx, x, y, (20 + 70 * easeOut(ru)) * k, (20 + 76 * easeOut(ru)) * k, layer === 'back' ? Math.PI * 0.5 : -Math.PI * 0.5, layer === 'back' ? Math.PI * 1.5 : Math.PI * 0.5, w, c, 1 - ru, layer, { wob: 0.15, seed: 50 + k * 9 });
    const R = rand(Math.floor(x));
    for (let i = 0; i < 16; i++) {
      const ang = R() * TAU, v = 160 + R() * 240, lay = R() < 0.45 ? 'back' : 'front', lf = 0.6 + R() * 0.4; if (lay !== layer || u > lf) continue;
      const px = x + Math.cos(ang) * v * u, py = y + Math.sin(ang) * v * u + 360 * u * u; if (py > GROUND) continue;
      chunk(ctx, px, py, 4 + R() * 6, u * (6 + R() * 8), i, I.mid, I.core, 1 - seg(u, lf * 0.7, lf), lay);
    }
    sparks(ctx, G.t, t0, x, y, layer, { n: 14, pal: I, sp: 320, seed: 91, len: 16, life: 0.5 });
  });
  if (kind === 'steam') addFx(0.9, (ctx, layer, t0) => {
    const u = G.t - t0;
    for (let i = 0; i < 10; i++) { const R = rand(i + Math.floor(x)), a = R() * TAU, d = 10 + u * (50 + R() * 40), lay = R() < 0.5 ? 'back' : 'front'; if (lay === layer) { disc(ctx, x + Math.cos(a) * d, y + Math.sin(a) * d * 0.6 - u * 40, (8 + R() * 10) * (0.6 + u), '#e8eef8', 0.55 * (1 - u / 0.9), layer); } }
  });
  if (kind === 'slam') addFx(1.2, (ctx, layer, t0) => groundImpact(ctx, G.t, layer, t0, x, 0.7, PAL.void, { seed: 8 }));
  if (kind === 'die') addFx(0.6, (ctx, layer, t0) => {
    const u = seg(G.t, t0, t0 + 0.5);
    if (layer === 'front') for (let i = 0; i < 14; i++) { const R = rand(i + Math.floor(x)), a = R() * TAU, d = u * (20 + R() * 40); disc(ctx, x + Math.cos(a) * d, y + Math.sin(a) * d - u * 20, (2 + R() * 3) * (1 - u), i % 2 ? '#e8d0f0' : '#ffffff', 1 - u, layer); }
  });
}
function dustPuff(x, y) { addFx(0.6, (ctx, layer, t0) => dust(ctx, G.t, t0, x, layer, { n: 5, reach: 24, sc: 0.6, seed: Math.floor(x), y })); }
function text(x, y, s, color, big) { G.texts.push({ x, y, s, color, big, t0: G.t }); }

// ---------------------------------------------------------------- sonido (con interruptor)
function sfx(fn) { if (G.settings.sound && AC) try { fn(1 / Math.max(0.25, G.timeScale)); } catch (e) { console.warn(e); } }
// bucle continuo controlable (rayo, zumbido): devuelve stop()
function loopSound(build) {
  if (!G.settings.sound || !AC) return () => {};
  const out = AC.createGain(); out.gain.setValueAtTime(X, AC.currentTime); out.gain.exponentialRampToValueAtTime(1, AC.currentTime + 0.04); out.connect(master);
  const stops = build(out) || [];
  return () => { const t = AC.currentTime; out.gain.cancelScheduledValues(t); out.gain.setValueAtTime(out.gain.value || 1, t); out.gain.exponentialRampToValueAtTime(X, t + 0.08); stops.forEach(n => { try { n.stop(t + 0.12); } catch (e) { } }); };
}

// ---------------------------------------------------------------- bucle
function step(dt) {
  if (G.hitStop > 0) { G.hitStop -= dt; dt *= 0.05; }
  dt *= G.timeScale;
  G.t += dt;
  updatePlayer(dt);
  const s = G.slots[G.cur];
  const rate = cdRate() * (G.t < (G.quickUntil || 0) ? G.quickK : 1);   // Reloj · Aceleración: tus recargas corren más
  for (const sl of G.slots.slice(0, G.slotCount)) if (sl.inst && sl.inst.update) sl.inst.update(dt * rate, sl === s);
  updateBuild(dt);
  if (G.mouse.down && s.inst && s.inst.onHold) s.inst.onHold(aimInfo(), dt);
  updateEnemies(dt);
  // se recorre una copia: lo que se crea durante la actualización entra en G.ents y se actualiza el frame siguiente
  const dead = new Set();
  for (const e of G.ents.slice()) if (e.update(dt) !== false) continue; else dead.add(e);
  if (dead.size) G.ents = G.ents.filter(e => !dead.has(e));
  G.texts = G.texts.filter(x => G.t - x.t0 < 0.9);
  while (G.dmgLog.length && G.t - G.dmgLog[0].t > 3) G.dmgLog.shift();
  G.shake = Math.min(14, G.shake) * Math.pow(0.0005, dt);
  G.dark -= dt;
  const p = G.player; G.cam.x = lerp(G.cam.x, clamp(p.x - VW * 0.42, 0, WW - VW), Math.min(1, dt * 8));
}

function drawWorld(ctx) {
  // cielo y fondo lejano (sólo para dar profundidad; sin arte)
  const g = ctx.createLinearGradient(0, 0, 0, VH); g.addColorStop(0, '#2a3346'); g.addColorStop(1, '#4a4a52');
  ctx.fillStyle = g; ctx.fillRect(0, 0, VW, VH);
  ctx.save(); ctx.translate(-G.cam.x * 0.3, 0); ctx.fillStyle = '#3a4458';
  for (let i = 0; i < 12; i++) { const x = i * 220; ctx.beginPath(); ctx.moveTo(x - 140, GROUND); ctx.lineTo(x, GROUND - 120 - (i % 3) * 40); ctx.lineTo(x + 140, GROUND); ctx.fill(); }
  ctx.restore();
}
function drawPlatforms(ctx) {
  ctx.fillStyle = '#2f5d7a'; ctx.fillRect(0, GROUND, WW, VH - GROUND + 40);
  ctx.fillStyle = '#3d7598'; ctx.fillRect(0, GROUND, WW, 6);
  for (const pl of PLATFORMS) { ctx.fillStyle = '#2f5d7a'; ctx.beginPath(); ctx.roundRect(pl.x0, pl.y, pl.x1 - pl.x0, 12, 5); ctx.fill(); ctx.fillStyle = '#5a9cc2'; ctx.fillRect(pl.x0 + 3, pl.y, pl.x1 - pl.x0 - 6, 3); }
}
function drawDummy(ctx, e, solid) {
  if (e.dead) return;
  const fz = e.frozenT > 0 ? 1 : 0;
  const shake = e.shakeT > 0 && !fz ? Math.sin(G.t * 140) * e.shakeT * 14 : 0, sq = e.sqT > 0 ? Math.sin(e.sqT / 0.2 * Math.PI) * 0.12 : 0;
  ctx.save(); ctx.translate(e.x + shake, e.y); ctx.scale(1 - sq, 1 + sq * 0.6);
  // atrapado por una singularidad: se estira hacia el centro (se lo está tragando) y se tiñe de violeta
  const suckK = e.suck && G.t - e.suck.t < 0.06 ? e.suck.k : 0;
  if (suckK > 0) { const sa = Math.atan2(e.suck.y - e.y, e.suck.x - e.x); ctx.rotate(sa); ctx.scale(1 + 0.22 * suckK, 1 - 0.14 * suckK); ctx.rotate(-sa); }
  if (e.fly) { const fl = Math.sin(G.t * 22 + e.sp.x) * (1 - fz); for (const s of [-1, 1]) { ctx.save(); ctx.fillStyle = C.enemyDark; ctx.translate(s * 20, -18); ctx.rotate(s * (0.5 + fl * 0.5)); ctx.beginPath(); ctx.ellipse(s * 14, 0, 16, 7, 0, 0, TAU); ctx.fill(); ctx.restore(); } }
  const rx = e.rx + 3, ry = e.ry + 3;
  ctx.fillStyle = C.enemyDark; ctx.beginPath(); ctx.ellipse(0, 2, rx, ry, 0, 0, TAU); ctx.fill();
  ctx.fillStyle = C.enemy; ctx.beginPath(); ctx.ellipse(-3, -2, rx - 4, ry - 4, 0, 0, TAU); ctx.fill();
  ctx.fillStyle = C.eye; ctx.beginPath(); ctx.arc(-14 * (G.player.x < e.x ? 1 : -1), -12, 4, 0, TAU); ctx.fill();
  if (e.freeze > 0 && !fz) { ctx.globalAlpha = 0.45 * e.freeze; ctx.fillStyle = PAL.ice.light; ctx.beginPath(); ctx.ellipse(0, 2, rx, ry, 0, 0, TAU); ctx.fill(); ctx.globalAlpha = 1; }
  if (e.burnT > 0) { ctx.globalAlpha = 0.22 + 0.1 * Math.sin(G.t * 30); ctx.fillStyle = '#ff7a2e'; ctx.beginPath(); ctx.ellipse(0, 2, rx, ry, 0, 0, TAU); ctx.fill(); ctx.globalAlpha = 1; }
  if (suckK > 0) { ctx.globalAlpha = 0.32 * suckK; ctx.fillStyle = PAL.void.mid; ctx.beginPath(); ctx.ellipse(0, 2, rx, ry, 0, 0, TAU); ctx.fill(); ctx.globalAlpha = 1; }
  if (e.stasis) { ctx.globalAlpha = 0.5; ctx.fillStyle = '#c9d6dc'; ctx.beginPath(); ctx.ellipse(0, 2, rx, ry, 0, 0, TAU); ctx.fill(); ctx.globalAlpha = 1; }
  if (solid) { ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.ellipse(0, 2, rx, ry, 0, 0, TAU); ctx.fill(); }
  else if (e.flashT > 0) { ctx.globalAlpha = e.flashT / 0.12; ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.ellipse(-3, -2, rx - 4, ry - 4, 0, 0, TAU); ctx.fill(); ctx.globalAlpha = 1; }
  ctx.restore();
  if (fz) iceBlock(ctx, e.x, e.y, rx + 8, ry + 8, 1, e.sp.x, seg(e.frozenT, 0.6, 0) );
  if (e.burnT > 0) for (let i = 0; i < 3; i++) flame(ctx, e.x - 12 + i * 12, e.y - e.ry * 0.55 + Math.abs(i - 1) * 6, 16 + i % 2 * 6, 6, G.t, e.sp.x + i * 3, 0.9, 'front');
  if (e.markT > 0) {                                          // marca: rombo que gira encima
    const y = e.y - e.ry - 22, a = Math.min(1, e.markT);
    ctx.save(); ctx.translate(e.x, y); ctx.rotate(G.t * 2); ctx.globalAlpha = a;
    for (const [r, c] of [[8, PAL.set.mid], [5, PAL.set.core]]) { ctx.fillStyle = c; ctx.beginPath(); ctx.moveTo(0, -r); ctx.lineTo(r * 0.7, 0); ctx.lineTo(0, r); ctx.lineTo(-r * 0.7, 0); ctx.closePath(); ctx.fill(); }
    ctx.restore();
    if (e.markN > 1) { ctx.font = '700 10px system-ui'; ctx.fillStyle = '#fff'; ctx.textAlign = 'center'; ctx.fillText('×' + e.markN, e.x + 13, y + 4); ctx.textAlign = 'left'; }
  }
  if (e.stasis && e.stasis.bank > 0) {                       // Estasis: daño guardado
    ctx.font = '800 13px system-ui'; ctx.textAlign = 'center'; ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,.6)';
    const s = '⏸ ' + Math.round(e.stasis.bank); ctx.strokeText(s, e.x, e.y - e.ry - 36); ctx.fillStyle = '#e6f3f6'; ctx.fillText(s, e.x, e.y - e.ry - 36); ctx.textAlign = 'left';
  }
  if (e.vulnT > G.t) {                                        // vulnerable: anillo roto que late
    const a = Math.min(1, (e.vulnT - G.t) * 3) * (0.6 + 0.4 * Math.sin(G.t * 16));
    ellArc(ctx, e.x, e.y, e.rx + 9, e.ry + 9, 0, TAU, 2, '#ff8fb0', a, 'front', { taper: 0, wob: 0.08, seed: e.sp.x, m: 28 });
  }
  if (e.stunT > 0) for (let i = 0; i < 3; i++) { const a = G.t * 5 + i * TAU / 3; star4(ctx, e.x + Math.cos(a) * 18, e.y - e.ry - 8 + Math.sin(a) * 5, 5, G.t * 4, '#fff3b0', Math.min(1, e.stunT * 4), 'front'); }
  // vida
  if (e.hp < e.hpMax && G.t - e.lastHitT < 3) {
    const w = 46, y = e.y + e.ry + 10; ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(e.x - w / 2, y, w, 5);
    ctx.fillStyle = '#ff5a7e'; ctx.fillRect(e.x - w / 2, y, w * clamp(e.hp / e.hpMax), 5);
    if (e.freeze > 0) { ctx.fillStyle = '#9ef2ff'; ctx.fillRect(e.x - w / 2, y + 6, w * e.freeze, 2); }
  }
  if (G.settings.hitboxes) { ctx.strokeStyle = '#0f0'; ctx.lineWidth = 1; ctx.beginPath(); ctx.ellipse(e.x, e.y, e.rx, e.ry, 0, 0, TAU); ctx.stroke(); }
}
function render(ctx) {
  const shx = (Math.random() - 0.5) * G.shake, shy = (Math.random() - 0.5) * G.shake * 0.6;
  drawWorld(ctx);
  ctx.save(); ctx.translate(-G.cam.x + shx, shy);
  drawPlatforms(ctx);
  for (const e of G.ents) e.draw(ctx, 'back');
  const p = G.player;
  player(ctx, p.x, p.y, p.face, { lean: p.lean * p.face, squash: p.dashT > 0 ? -0.12 : 0, aura: p.auraT > 0 ? p.aura : null, auraA: clamp(p.auraT * 2) });
  if (p.hurtT > 0 && Math.sin(G.t * 40) > 0) { ctx.globalAlpha = 0.5; ctx.fillStyle = '#ff6a6a'; ctx.fillRect(p.x - 14, p.y - 75, 28, 75); ctx.globalAlpha = 1; }
  const dark = G.dark > 0;
  if (dark) { ctx.fillStyle = 'rgba(10,6,36,0.62)'; ctx.fillRect(G.cam.x - 40, -40, VW + 80, VH + 80); }
  for (const e of G.enemies) drawDummy(ctx, e, dark && G.t - e.lastHitT < 0.08);
  for (const e of G.ents) e.draw(ctx, 'front');
  // retícula
  const m = G.mouse; ctx.strokeStyle = 'rgba(255,255,255,.8)'; ctx.lineWidth = 1.4;
  ctx.beginPath(); ctx.arc(m.x, m.y, 7, 0, TAU); ctx.moveTo(m.x - 11, m.y); ctx.lineTo(m.x - 4, m.y); ctx.moveTo(m.x + 4, m.y); ctx.lineTo(m.x + 11, m.y); ctx.stroke();
  // números de daño
  for (const x of G.texts) {
    const u = (G.t - x.t0) / 0.9;
    ctx.globalAlpha = 1 - u * u; ctx.font = `${x.big ? 800 : 700} ${x.big ? 17 : 13}px system-ui, sans-serif`; ctx.textAlign = 'center';
    ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,.6)'; ctx.strokeText(x.s, x.x, x.y - u * 26); ctx.fillStyle = x.color; ctx.fillText(x.s, x.x, x.y - u * 26);
  }
  ctx.globalAlpha = 1; ctx.textAlign = 'left';
  ctx.restore();
  drawHud(ctx);
}
function drawHud(ctx) {
  const p = G.player;
  ctx.fillStyle = 'rgba(0,0,0,.45)'; ctx.fillRect(12, 12, 160, 12); ctx.fillStyle = '#ff5a7e'; ctx.fillRect(14, 14, 156 * p.hp / p.hpMax, 8);
  // build activa: puntos por etiqueta (los tramos activos en su color) y escudo
  let bx = 14; ctx.font = '600 10px system-ui';
  for (const k in TAGS) { const c = tagCount(k); if (!c) continue; const tr = tier(k), txt = TAGS[k].name + ' ' + c + (tr ? ' ★' + tr : ''); ctx.fillStyle = tr ? TAGS[k].color : 'rgba(255,255,255,.45)'; ctx.fillText(txt, bx, 38); bx += ctx.measureText(txt).width + 10; }
  if (elemOf()) { ctx.fillStyle = TAGS[elemOf()].color; ctx.fillText('aplica ' + TAGS[elemOf()].name.toUpperCase(), bx, 38); }
  if (G.player.shield > 0) { ctx.fillStyle = 'rgba(200,204,212,.9)'; ctx.fillRect(14, 26, 156 * Math.min(1, G.player.shield / 30), 3); }
  // huecos de arma
  for (let i = 0; i < G.slotCount; i++) {
    const s = G.slots[i], d = WEAPONS[s.w], x = 12 + i * 112, y = VH - 44, on = i === G.cur, rd = s.inst && s.inst.ready ? clamp(s.inst.ready()) : 1;
    const pop = on && G.swapFx !== undefined ? 1 + 0.15 * (1 - seg(G.t, G.swapFx, G.swapFx + 0.2)) : 1;
    ctx.save(); ctx.translate(x + 52, y + 16); ctx.scale(pop, pop); ctx.translate(-x - 52, -y - 16);
    ctx.fillStyle = on ? 'rgba(31,58,77,.92)' : 'rgba(20,22,28,.75)'; ctx.strokeStyle = on ? '#29c2ff' : '#3a4150'; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.roundRect(x, y, 104, 32, 6); ctx.fill(); ctx.stroke();
    ctx.fillStyle = 'rgba(41,194,255,.35)'; ctx.fillRect(x + 2, y + 26, 100 * rd, 4);
    ctx.fillStyle = on ? '#fff' : '#9aa3b2'; ctx.font = '600 12px system-ui'; ctx.fillText(`${i + 1} · ${d.short}`, x + 8, y + 15);
    const arch = d.archetypes && s.arch ? d.archetypes.find(a => a.id === s.arch) : null, mods = ['Nv ' + slotLevel(s), arch ? arch.name : ''].filter(Boolean);
    ctx.font = '10px system-ui'; ctx.fillStyle = '#ffe39a'; ctx.fillText(mods.join(' · ').slice(0, 20), x + 8, y + 25);
    ctx.restore();
  }
  ctx.fillStyle = 'rgba(255,255,255,.55)'; ctx.font = '11px system-ui'; ctx.fillText('Q cambia de arma', 12 + G.slotCount * 112, VH - 22);
  const dps = G.dmgLog.reduce((a, x) => a + x.d, 0) / 3;
  ctx.textAlign = 'right'; ctx.fillStyle = 'rgba(255,255,255,.75)'; ctx.font = '600 12px system-ui'; ctx.fillText(`DPS (3 s) ${dps.toFixed(0)} · total ${Math.round(G.dmgTotal)}`, VW - 12, 22); ctx.textAlign = 'left';
}
