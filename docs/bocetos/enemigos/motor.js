// motor.js — jugable de ENEMIGOS (jugable.html): enemigos con IA encima del motor de la jugable de armas (../habilidades/game.js).
// Se reutiliza tal cual el player, las armas (g-*.js), el daño, los estados y las reacciones; aquí se sustituyen las dianas por
// enemigos de verdad. Cada enemigo vive en su e-*.js (diseño + IA + dibujo + TUNING), igual que las armas.
// Piezas GENÉRICAS (lo que se porta a Unity como movimientos / ataques reutilizables por cualquier enemigo o jefe):
//   avisos (warn), disparo y mortero (foeShot / lobShot), onda de suelo (groundWave), charco (foeZone), explosión (foeBlast),
//   aguante e interrupción (poise), turnos de ataque (canStart: máx. N atacando a la vez), golpe al player (hitP).
'use strict';

// ---------------------------------------------------------------- arena (más corta que la de armas: paredes para chocar)
const ARENA = { x0: 60, x1: 1460 };
PLATFORMS.length = 0;
PLATFORMS.push({ x0: 280, x1: 420, y: 250 }, { x0: 650, x1: 830, y: 196 }, { x0: 1060, x1: 1200, y: 250 });

// ---------------------------------------------------------------- ajustes generales de los enemigos (no de uno concreto)
G.es = { harmless: false, frozenAI: false, dashIframes: true, dmgMul: 1, hpMul: 1, maxAttackers: 3, respawn: true, showStates: false };
G.fight = null;
const EUI = {};                                             // ganchos del panel (ui.js)
G.enc = null;                                               // encuentro actual { id, name, list:[{ type, x, y? }] }

// ---------------------------------------------------------------- registro
const FOES = {};
// def = { id, name, role, question, moves:[{name, desc}], combos, art, fly, rx, ry, home, platforms,
//         tuning:{ clave:[valor, min, max, paso, etiqueta] }, init(e), think(e, dt), draw(ctx, e, P),
//         onDamage(e, o) (puede cambiar o; false = ignora el golpe), onHurt(e, d, o), onDeath(e, o), onInterrupt(e), drawHidden(ctx, e) }
function addFoe(def) {
  FOES[def.id] = def;
  G.tuning['foe_' + def.id] = Object.fromEntries(Object.entries(def.tuning).map(([k, v]) => [k, v[0]]));
}
const FT = e => G.tuning['foe_' + e.type];
// los cuatro ajustes que tiene todo enemigo (el resto los pone cada uno)
const baseTuning = (hp, poise, kb, contact) => ({
  hp: [hp, 5, 1500, 5, 'Vida'], poise: [poise, 0, 300, 5, 'Aguante: daño que le interrumpe mientras prepara (0 = nunca)'],
  kb: [kb, 0, 3, 0.05, 'Empuje recibido ×'], contact: [contact, 0, 40, 1, 'Daño por contacto'] });
const foes = () => G.enemies.filter(e => e.def);

// ---------------------------------------------------------------- player: golpes, esquiva, ralentizado
function pBox() { const p = G.player; return { x0: p.x - 13, x1: p.x + 13, y0: p.y - 74, y1: p.y }; }
function circleHitsPlayer(x, y, r) { const b = pBox(), nx = clamp(x, b.x0, b.x1), ny = clamp(y, b.y0, b.y1); return Math.hypot(nx - x, ny - y) < r; }
function rectHitsPlayer(x0, y0, x1, y1) { const b = pBox(); return Math.min(x0, x1) < b.x1 && Math.max(x0, x1) > b.x0 && Math.min(y0, y1) < b.y1 && Math.max(y0, y1) > b.y0; }
function ellipseHitsPlayer(e, pad = 0) {
  const b = pBox(), nx = clamp(e.x, b.x0, b.x1), ny = clamp(e.y, b.y0, b.y1);
  return Math.pow((nx - e.x) / (e.rx + pad), 2) + Math.pow((ny - e.y) / (e.ry + pad), 2) < 1;
}
const pCenter = () => ({ x: G.player.x, y: G.player.y - 40 });
// punto al que apunta un enemigo: el player, o un señuelo (Tótem · Señuelo) si tiene uno cerca
function aimPoint(e) {
  const d = (G.decoys || []).find(d => d.alive && G.ents.includes(d) && Math.hypot(d.x - e.x, d.y - e.y) < d.r);
  return d ? { x: d.x, y: d.y, decoy: d } : pCenter();
}
// golpe de un enemigo al player. atk = nombre del ataque (para las estadísticas). Devuelve si ha dado.
function hitP(e, atk, dmg, kx = 0, ky = 0) {
  const p = G.player, F = G.fight;
  if (p.hurtT > 0 || dmg <= 0) return false;
  if (G.es.dashIframes && p.dashT > 0) {
    if (G.t - (p._dodgeT || -9) > 0.3) { p._dodgeT = G.t; text(p.x, p.y - 96, 'ESQUIVA', '#bfe8ff'); if (F) F.dodges++; }
    return false;
  }
  if (G.es.harmless) { if (G.t - (p._harmT || -9) > 0.5) { p._harmT = G.t; text(p.x, p.y - 96, '(' + atk + ')', '#ffb0a0'); } return false; }
  const d = Math.max(1, Math.round(dmg * G.es.dmgMul)), before = p.hp;
  hurtPlayer(d);
  if (before - d <= 0 && p.hp === p.hpMax && F) F.deaths++;
  p.vx = kx; if (ky) { p.vy = ky; p.onGround = false; }
  if (F) { const k = (e && e.def ? e.def.name : '?') + ' · ' + atk; const h = F.hits[k] = F.hits[k] || { n: 0, dmg: 0 }; h.n++; h.dmg += d; F.dmgTaken += d; }
  return true;
}
function slowPlayer(s, k = 0.5) { const p = G.player; p.slowUntil = Math.max(p.slowUntil || 0, G.t + s); p.slowK = k; }
const _updatePlayer = updatePlayer;
updatePlayer = function (dt) {
  const p = G.player;
  if (G.t < (p.slowUntil || 0)) p.speedMul = Math.min(p.speedMul || 1, p.slowK || 0.5);
  _updatePlayer(dt);
  p.x = clamp(p.x, ARENA.x0 + 14, ARENA.x1 - 14);
};
const _step = step;
step = function (dt) {
  _step(dt);
  G.cam.x = clamp(G.cam.x, ARENA.x0 - 60, ARENA.x1 + 60 - VW);
  if (G.restartAt && G.t > G.restartAt) { G.restartAt = 0; resetEnemies(); }
};

// ---------------------------------------------------------------- máquina de estados y turnos
function go(e, st) {
  e.st = st; e.stT = 0; e.phase = null; e.tell = 0;
  if (G._visits) (G._visits[e.type] = G._visits[e.type] || {})[st] = true;
}
// turnos de ataque: como mucho G.es.maxAttackers enemigos preparando / ejecutando un ataque a la vez (multitudes justas)
function canStart(e) {
  if (G.es.frozenAI) return false;
  let n = 0; for (const o of alive()) if (o !== e && o.busy && o.def) n++;
  return n < G.es.maxAttackers;
}
function begin(e, name) { e.busy = true; e.atkId++; e.atkName = name; }
// fases de un ataque: preparación (se puede interrumpir, el cuerpo se enciende) → golpe (destello blanco) → recuperación (castigo)
function windup(e, dur) { e.phase = 'windup'; e.tell = clamp(e.stT / Math.max(0.01, dur)); return e.stT >= dur; }
function release(e) { e.phase = 'active'; e.tell = 0; e.relT = G.t; }
function recover(e) { e.phase = 'recover'; e.tell = 0; e.busy = false; }
function interrupt(e, why = '¡INTERRUMPIDO!') {
  e.atkId++; e.busy = false; e.phase = null; e.tell = 0; e.poiseDmg = 0; e.stunT = Math.max(e.stunT, 0.7);
  text(e.x, e.y - e.ry - 44, why, '#fff3b0', true);
  if (G.fight) G.fight.interrupts++;
  if (e.def.onInterrupt) e.def.onInterrupt(e); else go(e, e.def.home);
  e.cdT = Math.max(e.cdT || 0, 0.6);
}
function faceTo(e, x) { e.face = x >= e.x ? 1 : -1; }
const dxP = e => G.player.x - e.x;
const distP = e => Math.hypot(G.player.x - e.x, G.player.y - 40 - e.y);
// andar hacia x (suelo) con aceleración; los voladores usan steer
function walkTo(e, x, spd, dt, acc = 6) { const want = clamp((x - e.x) * 4, -spd, spd); e.mx = lerp(e.mx, want, Math.min(1, dt * acc)); }
function walkDir(e, dir, spd, dt, acc = 6) { e.mx = lerp(e.mx, dir * spd, Math.min(1, dt * acc)); }
function steer(e, x, y, spd, dt, acc = 3) {
  const dx = x - e.x, dy = y - e.y, d = Math.hypot(dx, dy) || 1, s = Math.min(spd, d * 3);
  e.mx = lerp(e.mx, dx / d * s, Math.min(1, dt * acc)); e.my = lerp(e.my, dy / d * s, Math.min(1, dt * acc));
}
function recentDmg(e, win) { e.recent = e.recent.filter(r => G.t - r.t < win); return e.recent.reduce((a, r) => a + r.d, 0); }
// esconder (bajo tierra, parpadeo): las armas no lo ven (se aparca fuera del mundo); hx/hy guardan dónde está de verdad
function hide(e) { if (e.hidden) return; e.hidden = true; e.hx = e.x; e.hy = e.y; e.x = -5000; e.y = -5000; e.busy = false; }
function unhide(e, x, y) { e.hidden = false; e.x = x === undefined ? e.hx : x; e.y = y === undefined ? e.hy : y; e.vx = 0; e.vy = 0; }
function say(e, s, c = '#ffe0a0', big = false) { if (G.t - (e._sayT || -9) > 0.35) { e._sayT = G.t; text(e.x, e.y - e.ry - 30, s, c, big); } }

// ---------------------------------------------------------------- daño a enemigos: escudos, empuje, aguante
const _damage = damage;
damage = function (e, o) {
  if (!e || !e.def) return _damage(e, o);
  if (e.dead || e.hidden) return 0;
  o = Object.assign({}, o);
  const t = FT(e);
  if (o.kx) o.kx *= t.kb; if (o.ky) o.ky *= t.kb;
  if (e.def.onDamage && e.def.onDamage(e, o) === false) return 0;
  const d = _damage(e, o);
  if (d > 0 && !e.dead) {
    e.poiseDmg += d * (o.heavy ? 2 : 1); e.recent.push({ t: G.t, d }); if (e.recent.length > 40) e.recent.shift();
    if (t.poise > 0 && e.poiseDmg >= t.poise && e.phase === 'windup') interrupt(e);
    if (e.def.onHurt) e.def.onHurt(e, d, o);
  }
  if (e.dead) foeDied(e, o);
  return d;
};
function foeDied(e, o = {}) {
  if (e._dd) return; e._dd = true; e.busy = false;
  if (G.fight) G.fight.kills++;
  if (e.def.onDeath) e.def.onDeath(e, o);
  checkClear();
}
// muerte propia (kamikaze): no cuenta como baja del player (no dispara robo de vida, reset, etc.)
function selfKill(e) { e.hp = 0; e.dead = true; e.deadT = 0; e._dd = true; e.busy = false; checkClear(); }

// ---------------------------------------------------------------- aparición, encuentros, resultado
let FOE_N = 0;
function spawnFoe(type, x, y, extra = {}) {
  const def = FOES[type], t = G.tuning['foe_' + type], fly = !!def.fly;
  const e = makeEnemy({ x, kind: fly ? 'flyer' : 'ground', y: y || 150 });
  const hp = t.hp * (G.es.hpMul || 1);
  Object.assign(e, {
    def, type, id: ++FOE_N, rx: def.rx, ry: def.ry, foot: def.ry, fly, hp, hpMax: hp,
    x: clamp(x, ARENA.x0 + def.rx, ARENA.x1 - def.rx), y: fly ? (y || 150) : GROUND - def.ry, homeY: y || 150, face: -1,
    st: '', stT: 0, phase: null, tell: 0, relT: -9, busy: false, atkId: 0, atkName: '', mx: 0, my: 0, poiseDmg: 0, recent: [],
    cdT: 0.8 + Math.random() * 0.8, onGround: true, wp: Math.random() * 10, hidden: false,
  }, extra);
  go(e, def.home);
  if (def.init) def.init(e);
  G.enemies.push(e);
  return e;
}
function startEncounter(enc) {
  G.enc = enc;
  G.enemies = []; G.restartAt = 0;
  G.ents = G.ents.filter(x => !x.foeEnt);
  const p = G.player; p.x = 200; p.y = GROUND; p.vx = 0; p.vy = 0; p.hp = p.hpMax; p.slowUntil = 0; p.shield = 0; p.shieldUsed = false;
  G.cam.x = 0;
  G.fight = { name: enc.name, t0: G.t, tEnd: 0, dmgTaken: 0, hits: {}, dodges: 0, deaths: 0, kills: 0, interrupts: 0 };
  for (const s of enc.list) spawnFoe(s.type, s.x, s.y);
  if (EUI.onEncounter) EUI.onEncounter();
}
resetEnemies = function () { startEncounter(G.enc || ENCOUNTERS[0]); };
function checkClear() {
  const F = G.fight; if (!F || F.tEnd) return;
  if (G.enemies.some(e => e.def && !e.dead)) return;
  F.tEnd = G.t;
  if (EUI.onClear) EUI.onClear(F);
  if (G.es.respawn) G.restartAt = G.t + 3;
}
// encuentros predefinidos: cada uno prueba una combinación (lo interesante de un enemigo sale al mezclarlo)
const ENCOUNTERS = [];
function addEncounter(enc) { ENCOUNTERS.push(enc); }

// ---------------------------------------------------------------- bucle de los enemigos (estados comunes copiados de las dianas)
updateEnemies = function (dt0) {
  for (const e of G.enemies.slice()) {
    if (!e.def) continue;
    if (e.dead) { if (!e._dd) foeDied(e); e.deadT += dt0; continue; }
    const tk = e.timeK === undefined ? 1 : e.timeK, dt = dt0 * tk; e.timeK = 1; e.timeSlow = tk < 1;
    e.flashT -= dt0; e.sqT -= dt0; e.shakeT -= dt0; e.markT -= dt; e.stunT -= dt;
    if (e.hidden) { e.mx = 0; e.my = 0; if (!G.es.frozenAI && tk > 0) { e.stT += dt; e.def.think(e, dt); } continue; }
    if (e.launchT > 0) { e.launchT -= dt; if (Math.abs(e.vx) > 220) launchCollide(e); }
    const frozen = e.frozenT > 0;
    if (frozen) { e.frozenT -= dt; e.iceT = G.t; if (e.frozenT <= 0) e.freeze = 0; }
    else e.freeze = Math.max(0, e.freeze - dt * 0.18);
    if (e.burnT > 0) { e.burnT -= dt; e.burnTick -= dt; if (e.burnTick <= 0) { e.burnTick = 0.35; damage(e, { dmg: 4, elem: 'burnTick', silentFlash: true, color: '#ffb35a' }); if (e.dead) continue; } }
    if (G.t - e.lastHitT > 1.2) e.poiseDmg = Math.max(0, e.poiseDmg - FT(e).poise * 0.6 * dt);
    // aturdido por otra cosa (vapor, golpe…) mientras prepara = interrumpido
    if ((e.stunT > 0 || frozen) && e.phase === 'windup') interrupt(e, frozen ? '¡CONGELADO!' : '¡INTERRUMPIDO!');
    // IA
    if (!frozen && e.stunT <= 0 && !G.es.frozenAI && tk > 0) { e.stT += dt; e.def.think(e, dt); }
    else { e.mx = 0; e.my = 0; }
    if (e.dead || e.hidden) continue;
    // atracción (singularidad, corazón de vacío)
    if (e.vpull && G.t < e.vpull.until && !e.pull) e.pull = { x: e.vpull.x, y: e.vpull.y, s: 240 };
    if (e.pull) {
      const dx = e.pull.x - e.x, dy = e.pull.y - e.y, d = Math.hypot(dx, dy) || 1, s = e.pull.s * (frozen ? 0.5 : 1) * Math.min(1, FT(e).kb * 1.5);
      if (d > 14) { e.x += dx / d * s * dt; if (e.pull.lift) e.y += dy / d * s * dt; }
      e.pull = null;
    }
    // movimiento propio + empujones
    const ox = e.x;
    e.x += (e.vx + e.mx) * dt; e.vx *= Math.pow(e.launchT > 0 ? 0.25 : 0.02, dt);
    e.wp += Math.abs(e.x - ox) * 0.08;
    if (e.fly) {
      if (!e.slammed) { e.y += (e.vy + e.my) * dt; e.vy *= Math.pow(0.05, dt); }
      else { e.vy += GRAV * dt; e.y += e.vy * dt; if (e.y >= GROUND - e.foot) { e.y = GROUND - e.foot; if (e.vy > 300) hitFx('slam', e.x, GROUND); e.vy = 0; e.slamT = (e.slamT || 0) + dt; if (e.slamT > 1.6) { e.slammed = false; e.slamT = 0; } } }
      e.y = clamp(e.y, 30, GROUND - e.foot);
    } else {
      const oy = e.y; e.vy += GRAV * dt; e.y += e.vy * dt; e.onGround = false;
      if (e.y >= GROUND - e.foot) { e.y = GROUND - e.foot; if (e.vy > 0) e.vy = 0; e.onGround = true; }
      else if (e.def.platforms && e.vy >= 0 && !e.dropT) for (const pl of PLATFORMS) { const py = pl.y - e.foot; if (e.x > pl.x0 && e.x < pl.x1 && oy <= py + 1 && e.y >= py) { e.y = py; e.vy = 0; e.onGround = true; } }
    }
    const lo = ARENA.x0 + e.rx, hi = ARENA.x1 - e.rx;
    e.wall = e.x <= lo ? -1 : e.x >= hi ? 1 : 0; e.x = clamp(e.x, lo, hi);
    // daño por contacto (pequeño: el daño de verdad está en los ataques, que se avisan)
    const c = FT(e).contact;
    if (c > 0 && !frozen && ellipseHitsPlayer(e)) hitP(e, 'Contacto', c, Math.sign(G.player.x - e.x || 1) * 260, -160);
  }
  // los muertos viejos se quitan (las armas comprueban .dead, así que da igual que alguna los tenga guardados)
  if (G.enemies.length > 40) G.enemies = G.enemies.filter(e => !e.dead || e.deadT < 3);
};

// ---------------------------------------------------------------- avisos (todos con el mismo lenguaje)
// Rojo-naranja = ahí va a doler; el relleno crece hasta el borde = cuánto falta; borde blanco = ya.
// Tamaño siempre sacado de lo que avisan (radio de la explosión, ancho del cuerpo), con un mínimo legible.
// kind: 'circle' (zona del suelo: x, y?, r) · 'ring' (explosión en el aire: x, y, r) · 'line' (puntería fina: ax, ay, bx, by)
//       'arrow' (embestida: x, y, dir, len, h) · 'mark' (dónde va a aparecer algo: x, y, r)
function warn(o) {
  const w = Object.assign({ t0: G.t, foeEnt: true, kind: 'circle' }, o);
  w.atk = o.e ? o.e.atkId : 0;
  w.update = function () {
    if (this.follow) this.follow(this);
    if (this.dead) return false;
    if (this.e && (this.e.dead || (!this.keep && this.e.atkId !== this.atk))) return false;
    return G.t - this.t0 < this.t;
  };
  w.draw = function (ctx, layer) { drawWarn(ctx, layer, this); };
  G.ents.push(w); return w;
}
function drawWarn(ctx, layer, w) {
  const u = clamp((G.t - w.t0) / w.t), a = Math.min(1, (G.t - w.t0) / 0.08), hot = u > 0.85;
  const edge = hot ? '#fff1c8' : '#ff7a5a';
  ctx.save();
  if (w.kind === 'circle' && layer === 'back') {
    const r = Math.max(18, w.r), y = w.y === undefined ? GROUND + 2 : w.y, ry = Math.max(5, r * 0.2);
    ctx.globalAlpha = 0.2 * a; ctx.fillStyle = '#ff4a2a'; ctx.beginPath(); ctx.ellipse(w.x, y, r, ry, 0, 0, TAU); ctx.fill();
    ctx.globalAlpha = 0.42 * a; ctx.beginPath(); ctx.ellipse(w.x, y, r * u, ry * u, 0, 0, TAU); ctx.fill();
    ctx.globalAlpha = 0.9 * a; ctx.strokeStyle = edge; ctx.lineWidth = 2; ctx.beginPath(); ctx.ellipse(w.x, y, r, ry, 0, 0, TAU); ctx.stroke();
  }
  if (w.kind === 'ring' && layer === 'front') {
    const r = Math.max(18, w.r);
    ctx.globalAlpha = 0.12 * a; ctx.fillStyle = '#ff4a2a'; ctx.beginPath(); ctx.arc(w.x, w.y, r, 0, TAU); ctx.fill();
    ctx.globalAlpha = 0.25 * a; ctx.beginPath(); ctx.arc(w.x, w.y, r * u, 0, TAU); ctx.fill();
    ctx.globalAlpha = 0.85 * a; ctx.strokeStyle = edge; ctx.lineWidth = 2; ctx.setLineDash([8, 6]); ctx.lineDashOffset = -G.t * 40;
    ctx.beginPath(); ctx.arc(w.x, w.y, r, 0, TAU); ctx.stroke(); ctx.setLineDash([]);
  }
  if (w.kind === 'line' && layer === 'front') {
    const lk = !!w.locked;
    ctx.globalAlpha = (lk ? 0.95 : 0.3 + 0.4 * u) * a; ctx.strokeStyle = lk ? '#ffffff' : '#ff7a5a'; ctx.lineWidth = lk ? 2.2 : 1.2;
    if (!lk) ctx.setLineDash([10, 7]);
    ctx.beginPath(); ctx.moveTo(w.ax, w.ay); ctx.lineTo(w.bx, w.by); ctx.stroke(); ctx.setLineDash([]);
    if (lk) { ctx.globalAlpha = 0.25 * a; ctx.lineWidth = 7; ctx.strokeStyle = '#ff5a4a'; ctx.beginPath(); ctx.moveTo(w.ax, w.ay); ctx.lineTo(w.bx, w.by); ctx.stroke(); }
  }
  if (w.kind === 'arrow' && layer === 'back') {
    // chevrones en el suelo, del alto del cuerpo que va a venir; se encienden de atrás adelante
    const n = 3, h = Math.max(14, w.h), len = Math.max(40, w.len), y = w.y === undefined ? GROUND - 2 : w.y;
    for (let i = 0; i < n; i++) {
      const k = (i + 1) / n, on = u >= i / n ? 1 : 0.35, x = w.x + w.dir * len * k;
      ctx.globalAlpha = (0.25 + 0.6 * on) * a; ctx.fillStyle = on === 1 ? (hot ? '#fff1c8' : '#ff6a4a') : '#ff6a4a';
      const th = h * 0.28, d = w.dir;
      ctx.beginPath(); ctx.moveTo(x, y - h); ctx.lineTo(x + d * h * 0.5, y - h * 0.5); ctx.lineTo(x, y);
      ctx.lineTo(x - d * th, y); ctx.lineTo(x + d * (h * 0.5 - th), y - h * 0.5); ctx.lineTo(x - d * th, y - h); ctx.closePath(); ctx.fill();
    }
  }
  if (w.kind === 'mark' && layer === 'back') {
    const r = Math.max(14, w.r), rot = G.t * 3;
    ctx.globalAlpha = (0.4 + 0.5 * u) * a; ctx.strokeStyle = edge; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.arc(w.x, w.y, r * (1.6 - 0.6 * u), 0, TAU); ctx.stroke();
    for (let i = 0; i < 4; i++) { const an = rot + i * TAU / 4; ctx.beginPath(); ctx.moveTo(w.x + Math.cos(an) * r * 0.4, w.y + Math.sin(an) * r * 0.4); ctx.lineTo(w.x + Math.cos(an) * r, w.y + Math.sin(an) * r); ctx.stroke(); }
  }
  ctx.restore();
}

// ---------------------------------------------------------------- proyectiles (compatibles con las armas: Égida, Espejo,
// Singularidad · Escudo de vacío, Reloj, Ráfaga, Látigo, Media luna y Tótem los desvían / capturan igual que la bala de prueba)
const FOE_PAL = { core: '#ffffff', light: '#ffc2b0', mid: '#ff5a4a', deep: '#b0203a', g: [255, 80, 70] };
// o = { x, y, vx, vy, g (gravedad), r, dmg, e, atk, style ('orb'|'needle'|'rock'|'seed'|'web'|'spore'), pal, life,
//       landY (para al llegar a esa y, cayendo), onLand(x, y), onHitP() (en vez del daño normal), warnEnt }
function foeShot(o) {
  const s = Object.assign({ kind: 'enemyShot', capturable: true, hostile: true, state: 'fly', foeEnt: true, life: 4, r: 5, g: 0, style: 'orb', pal: FOE_PAL, spin: Math.random() * TAU }, o);
  s.pdmg = o.dmg; s.dmg = undefined;                          // dmg lo ponen las armas al devolverla (pasa a ser del player)
  s.update = function (dt) {
    if (this.captured) return true;
    dt *= this.timeK === undefined ? 1 : this.timeK; this.timeK = 1;
    if (!this.friendly) this.vy += this.g * dt;
    this.x += this.vx * dt; this.y += this.vy * dt; this.life -= dt; this.spin += dt * 8;
    if (this.friendly) {
      if (this.warnEnt) this.warnEnt.dead = true;
      const h = insideEnemy(this.x, this.y, 4); if (h) { damage(h, { dmg: (this.dmg || this.pdmg) * (this.dmgMul || 1), slot: this.slot, kx: Math.sign(this.vx) * 80 }); return false; }
    } else {
      const d = (G.decoys || []).find(d => d.alive && G.ents.includes(d) && Math.hypot(d.x - this.x, d.y - this.y) < 26); if (d) { d.hurt(this.pdmg); return false; }
      if (circleHitsPlayer(this.x, this.y, this.r)) {
        const dodge = G.es.dashIframes && G.player.dashT > 0;  // con el dash la bala te atraviesa
        if (this.onHitP) { if (!dodge) this.onHitP(this); } else hitP(this.e, this.atk, this.pdmg, Math.sign(this.vx) * 220, -120);
        if (!dodge) { if (this.warnEnt) this.warnEnt.dead = true; return false; }
      }
      if (this.landY !== undefined && this.vy > 0 && this.y >= this.landY) { if (this.warnEnt) this.warnEnt.dead = true; if (this.onLand) this.onLand(this.x, this.landY); return false; }
    }
    if (this.y >= GROUND) { if (this.onLand && !this.friendly) this.onLand(this.x, GROUND); if (this.warnEnt) this.warnEnt.dead = true; return false; }
    return this.life > 0 && this.x > ARENA.x0 - 40 && this.x < ARENA.x1 + 40 && this.y > -200;
  };
  s.draw = function (ctx, layer) {
    if (layer !== 'front') return;
    const fr = this.friendly || this.captured, P = fr ? PAL.void : this.pal, gl = fr ? [150, 100, 255] : this.pal.g || FOE_PAL.g, a = Math.atan2(this.vy, this.vx);
    if (this.style === 'needle') { glow(ctx, this.x, this.y, 14, gl, 0.6); lens(ctx, this.x, this.y, a, 26, 4, P.mid, 1, layer); lens(ctx, this.x, this.y, a, 18, 1.8, '#fff', 1, layer); return; }
    if (this.style === 'rock') { chunk(ctx, this.x, this.y, this.r + 2, this.spin, Math.floor(this.r * 7), fr ? P.mid : '#6b5442', fr ? P.light : '#9a7c5e', 1, layer); return; }
    if (this.style === 'web') {
      glow(ctx, this.x, this.y, this.r * 2.2, [230, 220, 255], 0.35);
      ctx.save(); ctx.strokeStyle = fr ? P.light : '#efe8ff'; ctx.globalAlpha = 0.9; ctx.lineWidth = 1.2;
      for (let i = 0; i < 6; i++) { const an = this.spin * 0.3 + i * TAU / 6; ctx.beginPath(); ctx.moveTo(this.x, this.y); ctx.lineTo(this.x + Math.cos(an) * this.r, this.y + Math.sin(an) * this.r); ctx.stroke(); }
      for (const k of [0.45, 0.85]) { ctx.beginPath(); ctx.arc(this.x, this.y, this.r * k, 0, TAU); ctx.stroke(); }
      ctx.restore(); return;
    }
    const r = this.r;
    glow(ctx, this.x, this.y, r * 3.2, gl, 0.6); disc(ctx, this.x, this.y, r, P.mid, 1, layer); disc(ctx, this.x, this.y, r * 0.48, P.core || '#fff', 1, layer);
  };
  G.ents.push(s); return s;
}
// mortero: parábola que llega a (tx, ty) en T s, con su zona de aviso del tamaño de lo que explota
function lobShot(sx, sy, tx, ty, T, o) {
  const g = o.g === undefined ? GRAV * 0.75 : o.g;
  const vx = (tx - sx) / T, vy = (ty - sy - 0.5 * g * T * T) / T;
  const w = warn({ kind: 'circle', x: tx, y: ty + 2, r: o.warnR || o.r * 3, t: T });
  return foeShot(Object.assign({ x: sx, y: sy, vx, vy, g, landY: ty, warnEnt: w, life: T + 1 }, o));
}
// suelo bajo un punto x: la plataforma en la que está el player si es esa, si no el suelo
function surfaceUnder(x, y) { let best = GROUND; for (const pl of PLATFORMS) if (x > pl.x0 && x < pl.x1 && pl.y >= y - 2 && pl.y < best) best = pl.y; return best; }

// ---------------------------------------------------------------- ataques genéricos de área
// onda por el suelo (saltar): o = { speed, dist, h, dmg, atk, pal }
function groundWave(e, x, dir, o) {
  const pal = o.pal || { a: '#8a6a52', b: '#c9a98a' };
  G.ents.push({ foeEnt: true, x, x0: x, dir, hit: false, t0: G.t,
    update(dt) {
      this.x += dir * o.speed * dt; const p = G.player;
      if (!this.hit && Math.abs(p.x - this.x) < 18 && p.y > GROUND - o.h) { if (hitP(e, o.atk, o.dmg, dir * 300, -420)) this.hit = true; }
      return Math.abs(this.x - this.x0) < o.dist && this.x > ARENA.x0 && this.x < ARENA.x1;
    },
    draw(ctx, layer) {
      const fade = 1 - seg(Math.abs(this.x - this.x0), o.dist * 0.8, o.dist);
      for (let i = 0; i < 5; i++) {
        const xx = this.x - dir * i * 13, h = o.h * (1 - i * 0.19) * fade, lay = i === 0 ? 'front' : 'back'; if (lay !== layer || h <= 1) continue;
        ctx.save(); ctx.fillStyle = col(i === 0 ? pal.b : pal.a, layer);
        ctx.beginPath(); ctx.moveTo(xx - 9, GROUND + 2); ctx.lineTo(xx + dir * 3, GROUND - h); ctx.lineTo(xx + 9, GROUND + 2); ctx.closePath(); ctx.fill(); ctx.restore();
      }
      if (layer === 'front' && Math.random() < 0.15) dustPuff(this.x, GROUND);
    } });
}
// charco en el suelo que hace daño al pisarlo: o = { halfW, life, dmg, atk, pal, slow }
function foeZone(e, x, y, o) {
  const t0 = G.t, P = o.pal || PAL.life;
  G.ents.push({ foeEnt: true, kind: 'foeZone',
    update() {
      const p = G.player;
      if (p.onGround && Math.abs(p.y - y) < 3 && Math.abs(p.x - x) < o.halfW) { if (o.slow) slowPlayer(0.2, o.slow); if (o.dmg) hitP(e, o.atk, o.dmg, 0, 0); }
      return G.t - t0 < o.life;
    },
    draw(ctx, layer) {
      const u = (G.t - t0) / o.life, a = Math.min(1, (G.t - t0) * 6) * (1 - seg(u, 0.8, 1));
      if (layer === 'back') { ctx.save(); ctx.globalAlpha = 0.55 * a; ctx.fillStyle = col(P.deep, layer); ctx.beginPath(); ctx.ellipse(x, y + 2, o.halfW, 6, 0, 0, TAU); ctx.fill(); ctx.globalAlpha = 0.5 * a; ctx.fillStyle = col(P.mid, layer); ctx.beginPath(); ctx.ellipse(x, y + 1, o.halfW * 0.8, 3.5, 0, 0, TAU); ctx.fill(); ctx.restore(); }
      else for (let i = 0; i < 4; i++) { const R = rand(i + Math.floor(x)), ph = (G.t * (0.8 + R()) + R()) % 1, bx = x + (R() - 0.5) * o.halfW * 1.6; disc(ctx, bx, y - ph * 14, 2.5 * (1 - ph), P.light, a * (1 - ph), layer); }
    } });
}
// explosión: daña al player dentro del radio; o.foes = daño a los demás enemigos (kamikaze: úsalo como bomba)
function foeBlast(e, x, y, r, dmg, atk, o = {}) {
  const p = G.player;
  if (circleHitsPlayer(x, y, r)) hitP(e, atk, dmg, Math.sign(p.x - x || 1) * 400, -320);
  if (o.foes) for (const f of alive()) if (f !== e && f.def && Math.hypot(f.x - x, f.y - y) < r + Math.max(f.rx, f.ry) * 0.6) damage(f, { dmg: o.foes, kx: Math.sign(f.x - x || 1) * 320, heavy: true, color: '#ffb35a' });
  const P = o.pal || PAL.fire, sc = r / 80;
  addFx(1.1, (ctx, layer, t0) => impact(ctx, G.t, layer, t0, x, y, sc, P, { seed: Math.floor(x) }));
  addFx(0.5, (ctx, layer, t0) => { if (layer !== 'front') return; const u = seg(G.t, t0, t0 + 0.35); ctx.save(); ctx.globalAlpha = 0.5 * (1 - u); ctx.fillStyle = P.light; ctx.beginPath(); ctx.arc(x, y, r * (0.4 + 0.6 * easeOut(u)), 0, TAU); ctx.fill(); ctx.restore(); });
  G.shake += 5 * Math.min(2, sc);
  sfx(k => SFXLIB.boom(k, 0.5 * Math.min(2, sc)));
}

// ---------------------------------------------------------------- dibujo de los enemigos (sustituye al de las dianas)
// Cada e-*.js dibuja su cuerpo mirando a la derecha, centrado en (0,0), con P.c(color): así el mismo dibujo sirve para la
// silueta de los destellos (golpe = blanco, preparando ataque = naranja que late, congelación, quemadura, singularidad).
const PAINT = { c: x => x, mono: false, a: 1 };
const monoPaint = c => ({ c: () => c, mono: true, a: 1 });
function E_ell(ctx, P, x, y, rx, ry, c, rot = 0) { ctx.fillStyle = P.c(c); ctx.beginPath(); ctx.ellipse(x, y, Math.max(0.1, rx), Math.max(0.1, ry), rot, 0, TAU); ctx.fill(); }
function E_poly(ctx, P, pts, c) { ctx.fillStyle = P.c(c); ctx.beginPath(); pts.forEach((p, i) => i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1])); ctx.closePath(); ctx.fill(); }
function E_line(ctx, P, pts, w, c) { ctx.strokeStyle = P.c(c); ctx.lineWidth = w; ctx.lineCap = 'round'; ctx.lineJoin = 'round'; ctx.beginPath(); pts.forEach((p, i) => i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1])); ctx.stroke(); }
// ojo: amarillo; al preparar un ataque se pone blanco (la señal de «va a atacar» de TODOS los enemigos)
function E_eye(ctx, P, e, x, y, r) {
  const hot = e.tell > 0 || G.t - e.relT < 0.15;
  if (!P.mono && hot) glow(ctx, x, y, r * 4, [255, 220, 120], 0.5 + 0.4 * e.tell);
  ctx.fillStyle = P.c(hot ? '#ffffff' : C.eye); ctx.beginPath(); ctx.arc(x, y, r * (hot ? 1.25 : 1), 0, TAU); ctx.fill();
  if (!P.mono && !hot) { ctx.fillStyle = '#1a0f14'; ctx.beginPath(); ctx.arc(x + r * 0.3, y, r * 0.45, 0, TAU); ctx.fill(); }
}
drawDummy = function (ctx, e, solid) {
  if (!e.def || e.dead) return;
  const d = e.def;
  if (e.hidden) { if (d.drawHidden) d.drawHidden(ctx, e); if (G.es.showStates) stateLabel(ctx, e, e.hx, e.hy); return; }
  const fz = e.frozenT > 0;
  const shake = e.shakeT > 0 && !fz ? Math.sin(G.t * 140) * e.shakeT * 14 : 0, sq = e.sqT > 0 ? Math.sin(e.sqT / 0.2 * Math.PI) * 0.12 : 0;
  const suckK = e.suck && G.t - e.suck.t < 0.06 ? e.suck.k : 0;
  const body = P => {
    ctx.save(); ctx.translate(e.x + shake, e.y); ctx.scale(1 - sq, 1 + sq * 0.6);
    if (suckK > 0) { const sa = Math.atan2(e.suck.y - e.y, e.suck.x - e.x); ctx.rotate(sa); ctx.scale(1 + 0.22 * suckK, 1 - 0.14 * suckK); ctx.rotate(-sa); }
    ctx.scale(e.face, 1); d.draw(ctx, e, P); ctx.restore();
  };
  const over = (c, a) => { if (a <= 0.01) return; ctx.save(); ctx.globalAlpha = Math.min(1, a); body(monoPaint(c)); ctx.restore(); };
  if (e.tell > 0) glow(ctx, e.x, e.y, Math.max(e.rx, e.ry) * 1.9, [255, 150, 60], 0.35 * e.tell);
  body(PAINT);
  if (e.freeze > 0 && !fz) over(PAL.ice.light, 0.45 * e.freeze);
  if (e.burnT > 0) over('#ff7a2e', 0.22 + 0.1 * Math.sin(G.t * 30));
  if (suckK > 0) over(PAL.void.mid, 0.32 * suckK);
  if (e.stasis) over('#c9d6dc', 0.5);
  if (e.tell > 0) over('#ffb347', (0.12 + 0.4 * e.tell) * (0.7 + 0.3 * Math.sin(G.t * (18 + 30 * e.tell))));
  if (solid) over('#ffffff', 1);
  else if (e.flashT > 0) over('#ffffff', e.flashT / 0.12);
  else if (G.t - e.relT < 0.1) over('#ffffff', 0.85 * (1 - (G.t - e.relT) / 0.1));
  const rx = e.rx + 3, ry = e.ry + 3;
  if (fz) iceBlock(ctx, e.x, e.y, rx + 8, ry + 8, 1, e.sp.x, seg(e.frozenT, 0.6, 0));
  if (e.burnT > 0) for (let i = 0; i < 3; i++) flame(ctx, e.x - 12 + i * 12, e.y - e.ry * 0.55 + Math.abs(i - 1) * 6, 16 + i % 2 * 6, 6, G.t, e.sp.x + i * 3, 0.9, 'front');
  if (e.markT > 0) {
    const y = e.y - e.ry - 22, a = Math.min(1, e.markT);
    ctx.save(); ctx.translate(e.x, y); ctx.rotate(G.t * 2); ctx.globalAlpha = a;
    for (const [r, c] of [[8, PAL.set.mid], [5, PAL.set.core]]) { ctx.fillStyle = c; ctx.beginPath(); ctx.moveTo(0, -r); ctx.lineTo(r * 0.7, 0); ctx.lineTo(0, r); ctx.lineTo(-r * 0.7, 0); ctx.closePath(); ctx.fill(); }
    ctx.restore();
  }
  if (e.vulnT > G.t) { const a = Math.min(1, (e.vulnT - G.t) * 3) * (0.6 + 0.4 * Math.sin(G.t * 16)); ellArc(ctx, e.x, e.y, rx + 9, ry + 9, 0, TAU, 2, '#ff8fb0', a, 'front', { taper: 0, wob: 0.08, seed: e.sp.x, m: 28 }); }
  if (e.stunT > 0) for (let i = 0; i < 3; i++) { const a = G.t * 5 + i * TAU / 3; star4(ctx, e.x + Math.cos(a) * 18, e.y - e.ry - 8 + Math.sin(a) * 5, 5, G.t * 4, '#fff3b0', Math.min(1, e.stunT * 4), 'front'); }
  if (e.stasis && e.stasis.bank > 0) { ctx.font = '800 13px system-ui'; ctx.textAlign = 'center'; ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,.6)'; const s = '⏸ ' + Math.round(e.stasis.bank); ctx.strokeText(s, e.x, e.y - e.ry - 36); ctx.fillStyle = '#e6f3f6'; ctx.fillText(s, e.x, e.y - e.ry - 36); ctx.textAlign = 'left'; }
  // vida (siempre, para leer el combate) + aguante (blanco, sólo si tiene)
  const w = Math.max(30, Math.min(70, e.rx * 1.6)), by = e.y + e.ry + 9;
  if (e.hp < e.hpMax || G.t - e.lastHitT < 3) {
    ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(e.x - w / 2, by, w, 5);
    ctx.fillStyle = '#ff5a7e'; ctx.fillRect(e.x - w / 2, by, w * clamp(e.hp / e.hpMax), 5);
    const po = FT(e).poise; if (po > 0 && e.poiseDmg > 0) { ctx.fillStyle = 'rgba(255,255,255,.8)'; ctx.fillRect(e.x - w / 2, by + 6, w * clamp(e.poiseDmg / po), 2); }
    if (e.freeze > 0) { ctx.fillStyle = '#9ef2ff'; ctx.fillRect(e.x - w / 2, by + 9, w * e.freeze, 2); }
  }
  if (G.es.showStates) stateLabel(ctx, e, e.x, e.y);
  if (G.settings.hitboxes) { ctx.strokeStyle = '#0f0'; ctx.lineWidth = 1; ctx.beginPath(); ctx.ellipse(e.x, e.y, e.rx, e.ry, 0, 0, TAU); ctx.stroke(); }
};
function stateLabel(ctx, e, x, y) {
  ctx.font = '600 10px system-ui'; ctx.textAlign = 'center'; ctx.fillStyle = e.phase === 'windup' ? '#ffb347' : e.phase === 'active' ? '#fff' : e.phase === 'recover' ? '#9fe8a0' : 'rgba(255,255,255,.6)';
  ctx.fillText(e.st + (e.phase ? ' · ' + e.phase : ''), x, y - e.ry - 50); ctx.textAlign = 'left';
}
// HUD del combate: encuentro, tiempo, daño recibido, flechas a los enemigos fuera de pantalla
const _render = render;
render = function (ctx) {
  _render(ctx);
  const F = G.fight; if (!F) return;
  const live = foes().filter(e => !e.dead);
  ctx.save(); ctx.textAlign = 'center'; ctx.font = '700 13px system-ui'; ctx.fillStyle = 'rgba(255,255,255,.85)';
  const tt = (F.tEnd || G.t) - F.t0;
  ctx.fillText(`${F.name} · ${tt.toFixed(1)} s · quedan ${live.length} · daño recibido ${F.dmgTaken}`, VW / 2, 22);
  if (F.tEnd) {
    ctx.fillStyle = 'rgba(10,14,22,.7)'; ctx.fillRect(VW / 2 - 170, 120, 340, 64);
    ctx.fillStyle = '#9fe8a0'; ctx.font = '800 22px system-ui'; ctx.fillText(`DESPEJADO · ${tt.toFixed(1)} s`, VW / 2, 150);
    ctx.fillStyle = '#e8ecf2'; ctx.font = '600 12px system-ui'; ctx.fillText(`daño recibido ${F.dmgTaken} · esquivas ${F.dodges} · interrupciones ${F.interrupts}${F.deaths ? ' · muertes ' + F.deaths : ''}`, VW / 2, 172);
  }
  for (const e of live) {
    const x = e.hidden ? e.hx : e.x, y = e.hidden ? e.hy : e.y, sx = x - G.cam.x;
    if (sx > -10 && sx < VW + 10) continue;
    const ex = sx < 0 ? 14 : VW - 14, ey = clamp(y, 40, VH - 60), dir = sx < 0 ? -1 : 1;
    ctx.fillStyle = e.busy ? '#ffb347' : 'rgba(255,120,100,.8)'; ctx.beginPath(); ctx.moveTo(ex + dir * 8, ey); ctx.lineTo(ex - dir * 4, ey - 7); ctx.lineTo(ex - dir * 4, ey + 7); ctx.closePath(); ctx.fill();
  }
  ctx.restore();
};
// paredes de la arena (fondo de roca a los lados)
const _drawPlatforms = drawPlatforms;
drawPlatforms = function (ctx) {
  _drawPlatforms(ctx);
  ctx.fillStyle = '#232a36';
  ctx.fillRect(ARENA.x0 - 400, -40, 400, VH + 80); ctx.fillRect(ARENA.x1, -40, 400, VH + 80);
  ctx.fillStyle = '#3a4456'; ctx.fillRect(ARENA.x0 - 6, -40, 6, VH + 80); ctx.fillRect(ARENA.x1, -40, 6, VH + 80);
};
