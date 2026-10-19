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
  settings: { reactions: true, frames: true, hitstop: true, sound: false, enemyAttacks: false, hitboxes: false },
  tuning: {},
};

// ---------------------------------------------------------------- armas: registro y ajustes
const WEAPONS = {};
// def = { id, name, pal, mods:[{id,name,desc}], tuning:{ clave:[valor, min, max, paso, etiqueta] }, create(slot) → instancia }
// instancia = { onPress(aim), onHold(aim, dt), onRelease(aim), update(dt), ready() 0..1, label? }
function addWeapon(def) { WEAPONS[def.id] = def; G.tuning[def.id] = Object.fromEntries(Object.entries(def.tuning).map(([k, v]) => [k, v[0]])); }
const T = id => G.tuning[id];
const hasMod = (slot, m) => !!(slot && slot.mods && slot.mods[m]);
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
  p.face = G.mouse.x >= p.x ? 1 : -1;
  if (p.dashT > 0) { p.dashT -= dt; p.vx = p.dashDir * 620; p.vy = 0; }
  else {
    const acc = p.onGround ? 2400 : 1500, max = 240;
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
    if (p.onGround || p.jumps < 1) { if (!p.onGround) p.jumps++; p.vy = -560; p.onGround = false; dustPuff(p.x, p.y); }
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
  G.swapFx = G.t; sfx(k => SFXLIB.whoosh(k, 2400, 900, 0.12, 0.12)); UI.refreshSlots && UI.refreshSlots();
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
           rx: fly ? 22 : 27, ry: fly ? 28 : 39, hp: 120, hpMax: 120, dead: false, deadT: 0, flashT: 0, sqT: 0, shakeT: 0,
           freeze: 0, frozenT: 0, burnT: 0, burnTick: 0, markT: 0, stunT: 0, pull: null, slammed: false, lastHitT: -9, atkT: 2 + Math.random() * 2 };
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
function nearestEnemy(x, y, maxD = 9999, skip) {
  let best = null, bd = maxD;
  for (const e of alive()) { if (skip && skip.has(e)) continue; const d = Math.hypot(e.x - x, e.y - y); if (d < bd) { bd = d; best = e; } }
  return best;
}
function updateEnemies(dt) {
  for (const e of G.enemies) {
    if (e.dead) { e.deadT += dt; if (e.deadT > 2.5) Object.assign(e, makeEnemy(e.sp)); continue; }
    e.flashT -= dt; e.sqT -= dt; e.shakeT -= dt; e.markT -= dt; e.stunT -= dt;
    const frozen = e.frozenT > 0;
    if (frozen) { e.frozenT -= dt; if (e.frozenT <= 0) e.freeze = 0; }
    else e.freeze = Math.max(0, e.freeze - dt * 0.18);
    if (e.burnT > 0) { e.burnT -= dt; e.burnTick -= dt; if (e.burnTick <= 0) { e.burnTick = 0.35; damage(e, { dmg: 4, elem: 'burnTick', silentFlash: true, color: '#ffb35a' }); if (e.dead) continue; } }
    // movimiento propio
    if (!frozen && e.stunT <= 0 && e.kind === 'walker' && Math.abs(e.vx) < 30 && !e.pull) {
      e.x += e.dir * 40 * dt; if (e.x > e.sp.x1) e.dir = -1; if (e.x < e.sp.x0) e.dir = 1;
    }
    // atracción (singularidad): la pone cada frame quien atrae
    if (e.pull) {
      const dx = e.pull.x - e.x, dy = e.pull.y - e.y, d = Math.hypot(dx, dy) || 1, s = e.pull.s * (frozen ? 0.5 : 1);
      if (d > 14) { e.x += dx / d * s * dt; if (e.pull.lift) e.y += dy / d * s * dt; }
      e.pull = null;
    }
    // empujones
    e.x += e.vx * dt; e.vx *= Math.pow(0.02, dt);
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
function enemyShot(e) {
  const p = G.player, a = Math.atan2(p.y - 40 - e.y, p.x - e.x), sp = 220;
  G.ents.push({ x: e.x, y: e.y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 4,
    update(dt) { this.x += this.vx * dt; this.y += this.vy * dt; this.life -= dt;
      const p = G.player; if (Math.abs(this.x - p.x) < 14 && this.y > p.y - 70 && this.y < p.y) { hurtPlayer(8); return false; }
      return this.life > 0 && this.y < GROUND; },
    draw(ctx, layer) { if (layer !== 'front') return; glow(ctx, this.x, this.y, 16, [255, 80, 80], 0.6); disc(ctx, this.x, this.y, 5, '#ff6a6a', 1, layer); disc(ctx, this.x, this.y, 2.4, '#fff', 1, layer); } });
}
function hurtPlayer(d) { const p = G.player; if (p.hurtT > 0) return; p.hp = Math.max(0, p.hp - d); p.hurtT = 0.5; G.shake += 4; text(p.x, p.y - 90, '-' + d, '#ff6a6a'); if (p.hp <= 0) p.hp = p.hpMax; }
function healPlayer(d) { const p = G.player; const before = p.hp; p.hp = Math.min(p.hpMax, p.hp + d); if (p.hp > before + 0.5) { p.aura = PAL.life.g1; p.auraT = 0.4; } return p.hp - before; }

// ---------------------------------------------------------------- daño, estados y reacciones
// o = { dmg, kx, ky (empujón), slot, heavy (golpe fuerte: rompe congelados), elem: 'ice'|'fire', freeze (0..1 que suma),
//       burn (s), mark, hitstop, shake, color, silentFlash, noReact }
function damage(e, o) {
  if (!e || e.dead) return 0;
  let dmg = o.dmg || 0, label = null;
  const frozen = e.frozenT > 0, R = G.settings.reactions && !o.noReact;
  // reacciones
  if (R && frozen && o.elem === 'fire') { dmg *= 2; e.frozenT = 0; e.freeze = 0; label = 'DESHIELO'; hitFx('steam', e.x, e.y); }
  else if (R && frozen && o.heavy) { shatter(e); label = 'AÑICOS'; }
  else if (R && e.burnT > 0 && o.elem === 'ice') { e.burnT = 0; label = 'VAPOR'; vapor(e); }
  if (o.elem === 'ice' && o.freeze && !(label === 'AÑICOS')) { e.freeze = Math.min(1, e.freeze + o.freeze); if (e.freeze >= 1 && e.frozenT <= 0) { e.frozenT = T('react').frozenTime; sfx(SFXLIB.freeze.bind(null, 1, 0.3)); } }
  if (o.burn && o.elem === 'fire' && label !== 'DESHIELO') { e.burnT = Math.max(e.burnT, o.burn); e.burnTick = Math.min(e.burnTick, 0.1); }
  e.hp -= dmg; logDmg(dmg); e.lastHitT = G.t;
  if (!o.silentFlash) { e.flashT = 0.12; e.sqT = 0.2; e.shakeT = 0.22; }
  if (e.frozenT <= 0) { e.vx += o.kx || 0; if (o.ky) { e.vy += o.ky; } }
  if (dmg > 0) {                                             // golpes pequeños seguidos se suman en un solo número
    const colr = o.color || (o.elem === 'ice' ? '#bdf3ff' : o.elem === 'fire' ? '#ffb35a' : '#ffffff');
    if (dmg < 30 && e._txt && G.t - e._txt.t0 < 0.35 && G.texts.includes(e._txt)) { e._txt.v += dmg; e._txt.s = Math.round(e._txt.v) + ''; e._txt.t0 = G.t - 0.05; e._txt.big = e._txt.v >= 30; }
    else { text(e.x + (Math.random() - 0.5) * 20, e.y - e.ry - 12, Math.round(dmg) + '', colr, dmg >= 30); if (dmg < 30) { e._txt = G.texts[G.texts.length - 1]; e._txt.v = dmg; } }
  }
  if (label) text(e.x, e.y - e.ry - 34, label, label === 'AÑICOS' ? '#cdf6ff' : label === 'VAPOR' ? '#e0e8ff' : '#ffcf7a', true);
  if (o.hitstop && G.settings.hitstop) G.hitStop = Math.max(G.hitStop, o.hitstop);
  if (o.shake) G.shake += o.shake;
  if (o.frame && G.settings.frames) G.dark = Math.max(G.dark, 0.05);
  // sistema de marcas: con el Golpe estelar + mod Marcas equipado, los golpes de las otras armas marcan
  if (o.slot && o.slot.w !== 'star' && slotOf('star') && hasMod(slotOf('star'), 'marks')) e.markT = 8;
  if (e.hp <= 0) { e.dead = true; e.deadT = 0; hitFx('die', e.x, e.y, e); }
  return dmg;
}
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
function hurtEnemy(e, d, color, big) { if (e.dead) return; e.hp -= d; logDmg(d); e.lastHitT = G.t; text(e.x + 10, e.y - e.ry, Math.round(d) + '', color, big); if (e.hp <= 0) { e.dead = true; e.deadT = 0; hitFx('die', e.x, e.y, e); } }
// Reacción añicos: daño extra + fragmentos que dañan alrededor
function shatter(e) {
  const tr = T('react');
  e.frozenT = 0; e.freeze = 0; hurtEnemy(e, tr.shatterDmg, '#cdf6ff', true);
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
G.tuning.react = { frozenTime: 2.5, shatterDmg: 45, shatterRadius: 80, shatterSplash: 15, vaporDmg: 25, vaporRadius: 70 };
const REACT_TUNING = { frozenTime: [2.5, 0.5, 6, 0.1, 'Congelado: duración (s)'], shatterDmg: [45, 0, 150, 1, 'Añicos: daño extra'], shatterRadius: [80, 0, 200, 1, 'Añicos: radio'],
                       shatterSplash: [15, 0, 80, 1, 'Añicos: daño alrededor'], vaporDmg: [25, 0, 100, 1, 'Vapor: daño'], vaporRadius: [70, 0, 200, 1, 'Vapor: radio'] };

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
  for (const sl of G.slots.slice(0, G.slotCount)) if (sl.inst && sl.inst.update) sl.inst.update(dt, sl === s);
  if (G.mouse.down && s.inst && s.inst.onHold) s.inst.onHold(aimInfo(), dt);
  updateEnemies(dt);
  G.ents = G.ents.filter(e => e.update(dt) !== false);
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
  if (e.fly) { const fl = Math.sin(G.t * 22 + e.sp.x) * (1 - fz); for (const s of [-1, 1]) { ctx.save(); ctx.fillStyle = C.enemyDark; ctx.translate(s * 20, -18); ctx.rotate(s * (0.5 + fl * 0.5)); ctx.beginPath(); ctx.ellipse(s * 14, 0, 16, 7, 0, 0, TAU); ctx.fill(); ctx.restore(); } }
  const rx = e.rx + 3, ry = e.ry + 3;
  ctx.fillStyle = C.enemyDark; ctx.beginPath(); ctx.ellipse(0, 2, rx, ry, 0, 0, TAU); ctx.fill();
  ctx.fillStyle = C.enemy; ctx.beginPath(); ctx.ellipse(-3, -2, rx - 4, ry - 4, 0, 0, TAU); ctx.fill();
  ctx.fillStyle = C.eye; ctx.beginPath(); ctx.arc(-14 * (G.player.x < e.x ? 1 : -1), -12, 4, 0, TAU); ctx.fill();
  if (e.freeze > 0 && !fz) { ctx.globalAlpha = 0.45 * e.freeze; ctx.fillStyle = PAL.ice.light; ctx.beginPath(); ctx.ellipse(0, 2, rx, ry, 0, 0, TAU); ctx.fill(); ctx.globalAlpha = 1; }
  if (e.burnT > 0) { ctx.globalAlpha = 0.22 + 0.1 * Math.sin(G.t * 30); ctx.fillStyle = '#ff7a2e'; ctx.beginPath(); ctx.ellipse(0, 2, rx, ry, 0, 0, TAU); ctx.fill(); ctx.globalAlpha = 1; }
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
  }
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
  // huecos de arma
  for (let i = 0; i < G.slotCount; i++) {
    const s = G.slots[i], d = WEAPONS[s.w], x = 12 + i * 112, y = VH - 44, on = i === G.cur, rd = s.inst && s.inst.ready ? clamp(s.inst.ready()) : 1;
    const pop = on && G.swapFx !== undefined ? 1 + 0.15 * (1 - seg(G.t, G.swapFx, G.swapFx + 0.2)) : 1;
    ctx.save(); ctx.translate(x + 52, y + 16); ctx.scale(pop, pop); ctx.translate(-x - 52, -y - 16);
    ctx.fillStyle = on ? 'rgba(31,58,77,.92)' : 'rgba(20,22,28,.75)'; ctx.strokeStyle = on ? '#29c2ff' : '#3a4150'; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.roundRect(x, y, 104, 32, 6); ctx.fill(); ctx.stroke();
    ctx.fillStyle = 'rgba(41,194,255,.35)'; ctx.fillRect(x + 2, y + 26, 100 * rd, 4);
    ctx.fillStyle = on ? '#fff' : '#9aa3b2'; ctx.font = '600 12px system-ui'; ctx.fillText(`${i + 1} · ${d.short}`, x + 8, y + 15);
    const mods = Object.keys(s.mods || {}).filter(k => s.mods[k]).map(k => (d.mods.find(m => m.id === k) || {}).name).filter(Boolean);
    ctx.font = '10px system-ui'; ctx.fillStyle = '#ffe39a'; ctx.fillText(mods.join(' · ').slice(0, 20), x + 8, y + 25);
    ctx.restore();
  }
  ctx.fillStyle = 'rgba(255,255,255,.55)'; ctx.font = '11px system-ui'; ctx.fillText('Q cambia de arma', 12 + G.slotCount * 112, VH - 22);
  const dps = G.dmgLog.reduce((a, x) => a + x.d, 0) / 3;
  ctx.textAlign = 'right'; ctx.fillStyle = 'rgba(255,255,255,.75)'; ctx.font = '600 12px system-ui'; ctx.fillText(`DPS (3 s) ${dps.toFixed(0)} · total ${Math.round(G.dmgTotal)}`, VW - 12, 22); ctx.textAlign = 'left';
}
