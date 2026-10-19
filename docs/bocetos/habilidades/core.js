// core.js — base común de los prototipos de habilidades (mismo método que ataque-cometa-estados.html).
// Medidas en "px" del boceto; tiempos en segundos del clip. Cada habilidad (a1-…js) registra sus clips con addAbility().
'use strict';

// ---------------------------------------------------------------- escena
const W = 640, H = 400, GROUND = 340;
const HY = GROUND - 46;                 // altura del cristal / centro de los enemigos de suelo
const PX = 90;                          // x del player
const O = { x: 112, y: HY };            // boca: donde sale todo lo que dispara el cristal
const ECY = GROUND - 44;                // centro del cuerpo de un enemigo de suelo
const ERX = 27, ERY = 39;               // semiejes del cuerpo (para impactos)

// Paletas: núcleo → claro → medio → profundo → oscuro, y dos resplandores (rgb). `set` = la del dash/picado/cometa.
// Las demás firman una etiqueta de build: hielo, fuego, oro, robo de vida, vacío (gravedad).
const PAL = {
  set:  { core: '#ffffff', light: '#9ef2ff', mid: '#29c2ff', deep: '#1c5cdb', dark: '#7538db', g1: [160, 110, 255], g2: [120, 220, 255] },
  ice:  { core: '#ffffff', light: '#e4f9ff', mid: '#93e6ff', deep: '#3f93e3', dark: '#24499e', g1: [110, 180, 255], g2: [205, 246, 255] },
  fire: { core: '#fff6d8', light: '#ffd36b', mid: '#ff8a1f', deep: '#e0461a', dark: '#8a1a10', g1: [255, 80, 20], g2: [255, 190, 80] },
  gold: { core: '#fffbe8', light: '#ffe9a0', mid: '#ffc93c', deep: '#d48a0c', dark: '#7a4a06', g1: [255, 160, 30], g2: [255, 232, 150] },
  life: { core: '#fff0f3', light: '#ffb3c4', mid: '#ff5a7e', deep: '#c81e4a', dark: '#6a0f2a', g1: [255, 50, 100], g2: [255, 170, 195] },
  void: { core: '#ffffff', light: '#cdbcff', mid: '#8a5cff', deep: '#3a1c9c', dark: '#0d0626', g1: [120, 60, 255], g2: [180, 140, 255] },
};
const C = { player: '#e07a3c', playerDark: '#8a4520', enemy: '#6a3a5c', enemyDark: '#42243a', eye: '#ffd34d' };

// ---------------------------------------------------------------- estado global (cabecera)
let speed = 0.5, showLayers = false, framesOn = true, soundOn = false;

// ---------------------------------------------------------------- utilidades
const clamp = (v, a = 0, b = 1) => Math.max(a, Math.min(b, v));
const lerp = (a, b, t) => a + (b - a) * t;
const easeOut = t => 1 - Math.pow(1 - t, 3);
const easeIn = t => t * t * t;
const easeInOut = t => t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
const seg = (t, a, b) => clamp((t - a) / (b - a));
const bump = u => Math.sin(Math.PI * clamp(u));                 // 0 → 1 → 0
const TAU = Math.PI * 2;
// aleatorio determinista: la semilla se mezcla antes (con semillas pequeñas la primera tirada salía ~0 y alineaba partículas)
function rand(seed) {
  let s = (Math.abs(Math.floor(seed * 7919 + 13)) * 2654435761 % 2147483647) || 1;
  for (let i = 0; i < 4; i++) s = (s * 16807) % 2147483647;
  return () => (s = (s * 16807) % 2147483647) / 2147483647;
}
const col = (c, layer) => showLayers ? (layer === 'back' ? '#ff9a3c' : '#5dff8a') : c;
const rgba = (rgb, a) => `rgba(${rgb[0]},${rgb[1]},${rgb[2]},${a})`;
const noise1 = (x, s = 0) => Math.sin(x * 1.7 + s) * 0.5 + Math.sin(x * 3.1 + s * 2.3) * 0.3 + Math.sin(x * 5.3 + s * 0.7) * 0.2;

// ---------------------------------------------------------------- primitivas de dibujo
function glow(ctx, x, y, r, rgb, a) {
  if (showLayers || a <= 0 || r <= 0) return;
  const g = ctx.createRadialGradient(x, y, 0, x, y, r);
  g.addColorStop(0, rgba(rgb, 0.9)); g.addColorStop(0.45, rgba(rgb, 0.35)); g.addColorStop(1, rgba(rgb, 0));
  ctx.save(); ctx.globalAlpha = clamp(a); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(x, y, r, 0, TAU); ctx.fill(); ctx.restore();
}
function disc(ctx, x, y, r, c, a, layer) {
  if (a <= 0 || r <= 0) return;
  ctx.save(); ctx.globalAlpha = clamp(a); ctx.fillStyle = col(c, layer); ctx.beginPath(); ctx.arc(x, y, r, 0, TAU); ctx.fill(); ctx.restore();
}
// lente: forma afilada por los dos extremos, centrada en (x,y), a lo largo de ang
function lens(ctx, x, y, ang, len, wid, c, a, layer) {
  if (a <= 0 || len <= 0) return;
  ctx.save(); ctx.globalAlpha = clamp(a); ctx.translate(x, y); ctx.rotate(ang); ctx.fillStyle = col(c, layer);
  ctx.beginPath(); ctx.moveTo(-len / 2, 0); ctx.quadraticCurveTo(0, -wid, len / 2, 0); ctx.quadraticCurveTo(0, wid, -len / 2, 0); ctx.fill();
  ctx.restore();
}
// aguja entre dos puntos (afilada en las puntas, ancho w en el centro)
function needle(ctx, x0, y0, x1, y1, w, c, a, layer) {
  lens(ctx, (x0 + x1) / 2, (y0 + y1) / 2, Math.atan2(y1 - y0, x1 - x0), Math.hypot(x1 - x0, y1 - y0), w, c, a, layer);
}
// gota: cabeza redonda en (x,y) y cola afilada hacia atrás (ang = dirección de avance)
function drop(ctx, x, y, ang, len, wid, c, a, layer) {
  if (a <= 0 || len < 0.5) return;
  ctx.save(); ctx.globalAlpha = clamp(a); ctx.translate(x, y); ctx.rotate(ang); ctx.fillStyle = col(c, layer);
  ctx.beginPath(); ctx.moveTo(-len, 0); ctx.quadraticCurveTo(-len * 0.2, -wid, wid * 0.6, 0); ctx.quadraticCurveTo(-len * 0.2, wid, -len, 0); ctx.fill();
  ctx.restore();
}
// astilla: punta en a, base ancha w en b
function shard(ctx, ax, ay, bx, by, w, c, a, layer) {
  if (a <= 0) return;
  const dx = bx - ax, dy = by - ay, l = Math.hypot(dx, dy) || 1, nx = -dy / l * w / 2, ny = dx / l * w / 2;
  ctx.save(); ctx.globalAlpha = clamp(a); ctx.fillStyle = col(c, layer);
  ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx + nx, by + ny); ctx.lineTo(bx - nx, by - ny); ctx.closePath(); ctx.fill(); ctx.restore();
}
// destello de 4 puntas (dos lentes cruzadas)
function star4(ctx, x, y, r, ang, c, a, layer, thin = 0.16) {
  lens(ctx, x, y, ang, r * 2, r * thin * 2, c, a, layer);
  lens(ctx, x, y, ang + Math.PI / 2, r * 1.3, r * thin * 1.6, c, a, layer);
}
// arco de elipse de a0 a a1, con grosor que se afila en los extremos (taper) y radio con ruido (wob)
function ellArc(ctx, x, y, rx, ry, a0, a1, w, c, a, layer, o = {}) {
  if (a <= 0 || w <= 0 || rx <= 0) return;
  const M = o.m || 24, R = rand(o.seed || 1), ph = [R() * 6, R() * 6], wob = o.wob || 0, taper = o.taper === undefined ? 1 : o.taper, rot = o.rot || 0;
  const cr = Math.cos(rot), sr = Math.sin(rot);
  ctx.save(); ctx.globalAlpha = clamp(a); ctx.strokeStyle = col(c, layer); ctx.lineCap = 'round';
  let px = 0, py = 0;
  for (let i = 0; i <= M; i++) {
    const u = i / M, th = lerp(a0, a1, u), k = 1 + wob * (Math.sin(th * 3 + ph[0]) * 0.6 + Math.sin(th * 5 + ph[1]) * 0.4);
    const ex = Math.cos(th) * rx * k, ey = Math.sin(th) * ry * k, qx = x + ex * cr - ey * sr, qy = y + ex * sr + ey * cr;
    if (i) { ctx.lineWidth = Math.max(0.3, w * (taper ? Math.pow(bump(u - 1 / M / 2), 0.6 * taper) : 1)); ctx.beginPath(); ctx.moveTo(px, py); ctx.lineTo(qx, qy); ctx.stroke(); }
    px = qx; py = qy;
  }
  ctx.restore();
}
// anillo de suelo en perspectiva: mitad lejana (arriba) detrás, cercana (abajo) delante
function groundRing(ctx, x, y, r, w, c, a, layer, o = {}) {
  const sq = o.sq || 0.24;
  if (layer === 'back') ellArc(ctx, x, y, r, r * sq, Math.PI, TAU, w, c, a, layer, o);
  else ellArc(ctx, x, y, r, r * sq, 0, Math.PI, w, c, a, layer, o);
}
// cinta afilada a lo largo de una lista de puntos (s = 0 en el primero, 1 en el último)
function ribbon(ctx, pts, wfn, c, a, layer) {
  const n = pts.length; if (n < 2 || a <= 0) return;
  const L = [], Rr = [];
  for (let i = 0; i < n; i++) {
    const p = pts[i], q0 = pts[Math.max(0, i - 1)], q1 = pts[Math.min(n - 1, i + 1)];
    const dx = q1.x - q0.x, dy = q1.y - q0.y, l = Math.hypot(dx, dy) || 1, nx = -dy / l, ny = dx / l, w = Math.max(0, wfn(i / (n - 1))) / 2;
    L.push([p.x + nx * w, p.y + ny * w]); Rr.push([p.x - nx * w, p.y - ny * w]);
  }
  ctx.save(); ctx.globalAlpha = clamp(a); ctx.fillStyle = col(c, layer); ctx.beginPath(); ctx.moveTo(L[0][0], L[0][1]);
  for (const q of L) ctx.lineTo(q[0], q[1]);
  for (let i = n - 1; i >= 0; i--) ctx.lineTo(Rr[i][0], Rr[i][1]);
  ctx.closePath(); ctx.fill(); ctx.restore();
}
// trazo que se afila y se desvanece hacia el final
function strokeTaper(ctx, pts, w0, c, a, layer, fade = true) {
  const n = pts.length; if (n < 2 || a <= 0) return;
  ctx.save(); ctx.lineCap = 'round'; ctx.strokeStyle = col(c, layer);
  for (let i = 1; i < n; i++) {
    const u = i / (n - 1); ctx.globalAlpha = clamp(a * (fade ? 1 - u * 0.9 : 1)); ctx.lineWidth = Math.max(0.3, w0 * (1 - u));
    ctx.beginPath(); ctx.moveTo(pts[i - 1].x, pts[i - 1].y); ctx.lineTo(pts[i].x, pts[i].y); ctx.stroke();
  }
  ctx.restore();
}
// rayo en zigzag (ancho de color + núcleo blanco); R = rand para que el trazo cambie a saltos
function bolt(ctx, x0, y0, x1, y1, segs, R, w, pal, a, layer, jag = 0.22) {
  if (a <= 0) return;
  const pts = [{ x: x0, y: y0 }], dx = x1 - x0, dy = y1 - y0, l = Math.hypot(dx, dy) || 1, nx = -dy / l, ny = dx / l;
  for (let i = 1; i < segs; i++) { const u = i / segs, o = (R() - 0.5) * l * jag * Math.sin(Math.PI * u); pts.push({ x: x0 + dx * u + nx * o, y: y0 + dy * u + ny * o }); }
  pts.push({ x: x1, y: y1 });
  for (const [ww, c, aa] of [[w * 3, pal.dark, 0.35], [w * 1.6, pal.mid, 0.8], [w * 0.6, pal.core, 1]]) {
    ctx.save(); ctx.globalAlpha = clamp(a * aa); ctx.strokeStyle = col(c, layer); ctx.lineWidth = ww; ctx.lineJoin = 'round'; ctx.lineCap = 'round';
    ctx.beginPath(); pts.forEach((p, i) => i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y)); ctx.stroke(); ctx.restore();
  }
}
// llama: gota que parpadea, tres pasadas (rojo → naranja → núcleo); h = alto, w = ancho
function flame(ctx, x, y, h, w, t, seed, a, layer) {
  if (a <= 0 || h <= 0) return;
  const R = rand(seed), ph = R() * 6, fl = 1 + 0.18 * Math.sin(t * 23 + ph) + 0.1 * Math.sin(t * 37 + ph * 2), sway = Math.sin(t * 9 + ph) * w * 0.35;
  const F = PAL.fire;
  for (const [k, c, aa] of [[1, F.deep, 0.75], [0.72, F.mid, 0.95], [0.42, F.light, 1], [0.2, F.core, 1]]) {
    const hh = h * k * fl, ww = w * k;
    ctx.save(); ctx.globalAlpha = clamp(a * aa); ctx.fillStyle = col(c, layer); ctx.beginPath();
    ctx.moveTo(x - ww, y); ctx.quadraticCurveTo(x - ww * 1.05, y - hh * 0.45, x + sway * k, y - hh);
    ctx.quadraticCurveTo(x + ww * 1.05, y - hh * 0.45, x + ww, y); ctx.quadraticCurveTo(x, y + ww * 0.5, x - ww, y); ctx.fill(); ctx.restore();
  }
}
// pico de hielo que sale del suelo (caras clara/media, filo blanco)
function iceSpike(ctx, x, yb, h, w, lean, a, layer) {
  if (a <= 0 || h <= 0.5) return;
  const tx = x + lean * h, ty = yb - h, I = PAL.ice;
  ctx.save(); ctx.globalAlpha = clamp(a);
  ctx.fillStyle = col(I.deep, layer); ctx.beginPath(); ctx.moveTo(x - w, yb); ctx.lineTo(tx, ty); ctx.lineTo(x + w, yb); ctx.closePath(); ctx.fill();
  ctx.fillStyle = col(I.mid, layer); ctx.beginPath(); ctx.moveTo(x - w * 0.8, yb); ctx.lineTo(tx, ty); ctx.lineTo(x + w * 0.1, yb); ctx.closePath(); ctx.fill();
  ctx.fillStyle = col(I.light, layer); ctx.beginPath(); ctx.moveTo(x - w * 0.55, yb); ctx.lineTo(tx, ty); ctx.lineTo(x - w * 0.15, yb); ctx.closePath(); ctx.fill();
  ctx.strokeStyle = col(I.core, layer); ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(tx, ty); ctx.lineTo(x - w * 0.2, yb - h * 0.25); ctx.stroke();
  ctx.restore();
}
// fragmento de hielo/roca girando (polígono de 4-5 lados)
function chunk(ctx, x, y, r, rot, seed, c1, c2, a, layer) {
  if (a <= 0 || r <= 0.3) return;
  const R = rand(seed), n = 4 + Math.floor(R() * 2), pts = [];
  for (let i = 0; i < n; i++) { const th = rot + i / n * TAU + R() * 0.6, rr = r * (0.6 + R() * 0.5); pts.push([x + Math.cos(th) * rr, y + Math.sin(th) * rr]); }
  ctx.save(); ctx.globalAlpha = clamp(a); ctx.fillStyle = col(c1, layer); ctx.beginPath(); pts.forEach((p, i) => i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1])); ctx.closePath(); ctx.fill();
  ctx.fillStyle = col(c2, layer); ctx.beginPath(); ctx.moveTo(pts[0][0], pts[0][1]); ctx.lineTo(pts[1][0], pts[1][1]); ctx.lineTo(x, y); ctx.closePath(); ctx.fill(); ctx.restore();
}

// esquirla de cristal (la del Abanico): lente larga en capas + faceta clara. Sólo capa delantera.
function crystalShard(ctx, x, y, ang, sc, pal, a, layer) {
  if (layer !== 'front' || a <= 0) return;
  glow(ctx, x, y, 16 * sc, pal.g1, 0.45 * a);
  lens(ctx, x, y, ang, 30 * sc, 6.5 * sc, pal.deep, 0.9 * a, layer);
  lens(ctx, x + Math.cos(ang) * 2, y + Math.sin(ang) * 2, ang, 25 * sc, 4.6 * sc, pal.mid, a, layer);
  lens(ctx, x + Math.cos(ang) * 4, y + Math.sin(ang) * 4, ang, 18 * sc, 2.4 * sc, pal.core, a, layer);
  ctx.save(); ctx.globalAlpha = clamp(0.7 * a); ctx.translate(x, y); ctx.rotate(ang); ctx.fillStyle = col(pal.light, layer);
  ctx.beginPath(); ctx.moveTo(-12 * sc, 0); ctx.quadraticCurveTo(0, -5.5 * sc, 15 * sc, 0); ctx.closePath(); ctx.fill(); ctx.restore();
}

// ---------------------------------------------------------------- piezas compuestas
// chispas: trazos afilados que salen de (x,y) entre a0 y a1, con gravedad; la mitad detrás y la mitad delante
function sparks(ctx, t, t0, x, y, layer, o = {}) {
  const life = o.life || 0.45, u0 = (t - t0) / life; if (u0 <= 0 || u0 >= 1.6) return;
  const n = o.n || 10, pal = o.pal || PAL.set, R = rand(o.seed || 5), sp = o.sp || 260, g = o.grav === undefined ? 520 : o.grav, len = o.len || 14, a0 = o.a0 === undefined ? 0 : o.a0, a1 = o.a1 === undefined ? TAU : o.a1;
  for (let i = 0; i < n; i++) {
    const ang = lerp(a0, a1, R()), v = sp * (0.45 + R() * 0.8), lf = life * (0.6 + R() * 0.6), lay = R() < (o.backShare === undefined ? 0.45 : o.backShare) ? 'back' : 'front', cc = [pal.core, pal.light, pal.mid, pal.core][i % 4];
    const tt = (t - t0), u = tt / lf; if (u <= 0 || u >= 1 || lay !== layer) continue;
    const vx = Math.cos(ang) * v, vy = Math.sin(ang) * v, px = x + vx * tt, py = y + vy * tt + 0.5 * g * tt * tt;
    const cvx = vx, cvy = vy + g * tt, cl = Math.hypot(cvx, cvy) || 1, L = len * (1 - u * 0.6) * (0.6 + R() * 0.7);
    shard(ctx, px - cvx / cl * L, py - cvy / cl * L, px, py, (o.w || 2.6) * (1 - u * 0.5), cc, 1 - u, layer);
  }
}
// gotas que caen (restos de energía)
function droplets(ctx, t, t0, x, y, layer, o = {}) {
  const life = o.life || 0.7; if (t < t0 || t > t0 + life * 1.5) return;
  const n = o.n || 8, R = rand(o.seed || 9), pal = o.pal || PAL.set;
  for (let i = 0; i < n; i++) {
    const ang = -Math.PI / 2 + (R() - 0.5) * (o.spread || 2.6), v = (o.sp || 150) * (0.4 + R()), lf = life * (0.6 + R() * 0.6), lay = R() < 0.5 ? 'back' : 'front';
    const tt = t - t0, u = tt / lf; if (u >= 1 || lay !== layer) continue;
    const vx = Math.cos(ang) * v, vy = Math.sin(ang) * v + 420 * tt, px = x + Math.cos(ang) * v * tt, py = y + Math.sin(ang) * v * tt + 210 * tt * tt;
    drop(ctx, px, py, Math.atan2(vy, vx), (6 + R() * 6) * (1 - u * 0.5), (1.6 + R() * 1.6) * (1 - u * 0.6), [pal.core, pal.light, pal.mid][i % 3], 1 - u, layer);
  }
}
// polvo de suelo: bocanadas con sombra y brillo que se abren y se desvanecen
function dust(ctx, t, t0, x, layer, o = {}) {
  const life = o.life || 0.7, u0 = (t - t0) / life; if (u0 <= 0 || u0 >= 1.3) return;
  const n = o.n || 8, R = rand(o.seed || 21), reach = o.reach || 60, sc = o.sc || 1, y = o.y === undefined ? GROUND : o.y;
  for (let i = 0; i < n; i++) {
    const side = o.side || (R() < 0.5 ? -1 : 1), d = reach * (0.3 + R() * 0.8), lf = life * (0.7 + R() * 0.5), u = (t - t0) / lf;
    const lay = R() < 0.5 ? 'back' : 'front'; if (u >= 1 || lay !== layer) continue;
    const px = x + side * d * easeOut(u), py = y - 4 - (6 + R() * 12) * easeOut(u), r = (5 + R() * 6) * sc * (0.6 + u * 0.9), a = (1 - u) * 0.75;
    disc(ctx, px + 1.5, py + 1.5, r, '#5b6a78', a * 0.6, lay);
    disc(ctx, px, py, r, '#c9d3dc', a * 0.8, lay);
    disc(ctx, px - r * 0.3, py - r * 0.3, r * 0.5, '#eef4f8', a * 0.7, lay);
  }
}
// escombros (rocas del suelo) que saltan en arco y giran
function debris(ctx, t, t0, x, layer, o = {}) {
  const life = o.life || 0.9; if (t < t0 || t > t0 + life * 1.4) return;
  const n = o.n || 7, R = rand(o.seed || 33), c1 = o.c1 || '#2f5d7a', c2 = o.c2 || '#5a8fb2';
  for (let i = 0; i < n; i++) {
    const ang = -Math.PI / 2 + (R() - 0.5) * (o.spread || 2.2), v = (o.sp || 260) * (0.5 + R() * 0.7), lf = life * (0.7 + R() * 0.5), lay = R() < 0.4 ? 'back' : 'front';
    const tt = t - t0; if (tt > lf || lay !== layer) continue;
    const px = x + (R() - 0.5) * 14 + Math.cos(ang) * v * tt, py = GROUND - 2 + Math.sin(ang) * v * tt + 380 * tt * tt;
    if (py > GROUND + 2) continue;
    chunk(ctx, px, py, (o.r || 4) * (0.6 + R() * 0.8), tt * (8 + R() * 10), i * 7 + (o.seed || 0), c1, c2, 1 - seg(tt, lf * 0.7, lf), lay);
  }
}
// grietas radiales en el suelo con brillo
function cracks(ctx, t, t0, x, layer, o = {}) {
  if (layer !== 'back' || t < t0) return;
  const life = o.life || 1.2, u = (t - t0) / life; if (u >= 1) return;
  const n = o.n || 6, R = rand(o.seed || 41), pal = o.pal || PAL.set, grow = easeOut(seg(t, t0, t0 + 0.12)), fade = 1 - seg(u, 0.5, 1);
  for (let i = 0; i < n; i++) {
    const side = i % 2 ? 1 : -1, len = (o.len || 70) * (0.5 + R() * 0.6) * grow, pts = [{ x, y: GROUND + 2 }];
    let px = x, py = GROUND + 2;
    for (let k = 1; k <= 4; k++) { px += side * len / 4; py += (R() - 0.3) * 5 + 1.5; pts.push({ x: px, y: Math.min(H - 4, py) }); }
    strokeTaper(ctx, pts, 4.5, pal.mid, 0.55 * fade, layer, false);
    strokeTaper(ctx, pts, 2, pal.core, 0.9 * fade, layer, false);
  }
}
// impacto estándar sobre un cuerpo: destello en cruz, ondas por delante, agujas radiales y gotas
function impact(ctx, t, layer, t0, x, y, sc, pal, o = {}) {
  const u = (t - t0); if (u < 0 || u > 1.2) return;
  const fu = seg(u, 0, 0.16), dir = o.dir || 0;
  if (layer === 'front') {
    glow(ctx, x, y, 60 * sc * (1 - fu * 0.5), pal.g1, 0.7 * (1 - fu));
    glow(ctx, x, y, 30 * sc, pal.g2, 0.8 * (1 - fu));
    star4(ctx, x, y, 34 * sc * (1 - fu * 0.4), dir + 0.1, pal.core, 1 - fu, layer);
    disc(ctx, x, y, 9 * sc * (1 - fu), pal.core, 1 - fu, layer);
    const ru = seg(u, 0, 0.3);
    for (const [k, c, w] of [[1, pal.mid, 3.2], [0.72, pal.core, 1.6]])
      ellArc(ctx, x, y, (14 + 46 * easeOut(ru)) * sc * k, (14 + 46 * easeOut(ru)) * sc * k * 1.05, dir - 1.3, dir + 1.3, w * sc, c, 1 - ru, layer, { wob: 0.12, seed: 3 + k * 10 });
  }
  const nu = seg(u, 0, 0.22), R = rand(o.seed || 7), n = o.needles || 9;
  for (let i = 0; i < n; i++) {
    const ang = dir + (R() - 0.5) * (o.spread || 2.8), l0 = (10 + R() * 14) * sc, l1 = l0 + (24 + R() * 30) * sc * easeOut(nu), lay = R() < 0.4 ? 'back' : 'front';
    if (lay !== layer || nu >= 1) continue;
    needle(ctx, x + Math.cos(ang) * l0, y + Math.sin(ang) * l0, x + Math.cos(ang) * l1, y + Math.sin(ang) * l1, 3.2 * sc * (1 - nu * 0.6), i % 3 ? pal.light : pal.core, 1 - nu, layer);
  }
  droplets(ctx, t, t0 + 0.02, x, y, layer, { n: o.drops || 7, pal, seed: (o.seed || 7) + 50, sp: 140 * sc });
}
// impacto contra el suelo: anillo en perspectiva, polvo, escombros, columna corta de luz
function groundImpact(ctx, t, layer, t0, x, sc, pal, o = {}) {
  const u = t - t0; if (u < 0 || u > 1.4) return;
  const ru = seg(u, 0, 0.35), fu = seg(u, 0, 0.14);
  groundRing(ctx, x, GROUND, (16 + 80 * easeOut(ru)) * sc, 4 * sc * (1 - ru) + 0.5, pal.mid, 1 - ru, layer, { wob: 0.08, seed: 11 });
  groundRing(ctx, x, GROUND, (10 + 58 * easeOut(ru)) * sc, 2 * sc, pal.core, 1 - ru, layer, { wob: 0.1, seed: 12 });
  if (layer === 'front') {
    glow(ctx, x, GROUND - 10, 70 * sc, pal.g1, 0.6 * (1 - fu));
    lens(ctx, x, GROUND - 26 * sc, Math.PI / 2, 70 * sc * (1 - fu * 0.3), 9 * sc * (1 - fu), pal.core, 1 - fu, layer);
    star4(ctx, x, GROUND - 8, 40 * sc, 0, pal.core, 1 - fu, layer);
  }
  if (o.dust !== false) dust(ctx, t, t0, x, layer, { n: 10, reach: 70 * sc, sc, seed: (o.seed || 1) + 3 });
  if (o.debris !== false) debris(ctx, t, t0, x, layer, { n: 8, sp: 280 * sc, seed: (o.seed || 1) + 9 });
  sparks(ctx, t, t0, x, GROUND - 4, layer, { n: 12, a0: -Math.PI * 0.95, a1: -Math.PI * 0.05, sp: 330 * sc, pal, seed: (o.seed || 1) + 17, len: 16 });
}
// carga en el cristal: motas que convergen, orbe que crece y destello al soltar
function castFx(ctx, t, layer, t0, t1, pal, sc = 1, o = {}) {
  const x = o.x || O.x, y = o.y || O.y;
  if (t >= t0 && t < t1) {
    const u = seg(t, t0, t1), R = rand(o.seed || 71);
    for (let i = 0; i < 12; i++) {
      const a = R() * TAU, rate = 1.6 + R(), ph = R(), age = ((t - t0) * rate + ph) % 1, r = (46 - 40 * age) * sc, lay = Math.sin(a + age * 3) < 0 ? 'back' : 'front';
      if (lay !== layer) continue;
      const px = x + Math.cos(a + age * 2) * r, py = y + Math.sin(a + age * 2) * r * 0.8;
      drop(ctx, px, py, Math.atan2(y - py, x - px), 7 * sc * age + 2, 1.6 * sc, i % 2 ? pal.light : pal.core, age * u, layer);
    }
    if (layer === 'front') {
      glow(ctx, x, y, 26 * sc * u + 6, pal.g1, 0.6 * u);
      disc(ctx, x, y, 6 * sc * u, pal.mid, 0.9, layer); disc(ctx, x, y, 3.6 * sc * u, pal.core, 1, layer);
    }
  }
  if (layer === 'front' && t >= t1 && t < t1 + 0.18) {
    const u = seg(t, t1, t1 + 0.18);
    glow(ctx, x, y, 34 * sc, pal.g2, 0.8 * (1 - u));
    star4(ctx, x, y, 22 * sc * (1 + u * 0.5), (o.dir || 0), pal.core, 1 - u, layer);
    ellArc(ctx, x, y, 8 + 22 * easeOut(u) * sc, 8 + 26 * easeOut(u) * sc, -1.2, 1.2, 2.4, pal.light, 1 - u, layer, { rot: o.dir || 0 });
  }
}

// ---------------------------------------------------------------- personajes
function player(ctx, x, y, dir, o = {}) {
  const { squash = 0, lean = 0, aura = null, auraA = 0 } = o;
  if (aura && auraA > 0) { glow(ctx, x, y - 34, 46, aura, auraA); }
  ctx.save(); ctx.translate(x, y); ctx.rotate(lean * dir); ctx.scale(1 + squash, 1 - squash);
  ctx.fillStyle = C.playerDark; ctx.beginPath(); ctx.roundRect(-13, -48, 26, 48, 9); ctx.fill();
  ctx.fillStyle = C.player;
  ctx.beginPath(); ctx.ellipse(4 * dir, -60, 15, 13, 0, 0, TAU); ctx.fill();
  ctx.beginPath(); ctx.ellipse(-3 * dir, -78, 4, 11, -0.3 * dir, 0, TAU); ctx.fill();
  ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.arc(11 * dir, -62, 2.5, 0, TAU); ctx.fill();
  ctx.fillStyle = '#5dff9a'; ctx.beginPath(); ctx.arc(8 * dir, -34, 3.2, 0, TAU); ctx.fill();          // gema del pecho
  ctx.restore();
}
// Enemigo. E = { x, cy? (volador), hits:[{t,k,fl,sq}], pull(t)→[dx,dy], walk(t)→x, tint(t)→[color,a], frozen(t)→0..1,
//               burn(t)→0..1, lift(t)→dy, dieAt }
function enemyPos(t, E) {
  let x = E.walk ? E.walk(t) : E.x, y = E.cy === undefined ? ECY : E.cy;
  for (const h of E.hits || []) {
    const ht = seg(t, h.t, h.t + (h.d || 0.5));
    if (ht > 0) x += Math.sin(Math.min(ht * 2.2, 1) * Math.PI) * (h.k || 0) * (h.hold && ht >= 0.45 ? 0 : 1);
  }
  if (E.pull) { const p = E.pull(t); x += p[0]; y += p[1]; }
  if (E.lift) y -= E.lift(t);
  return { x, y };
}
function drawEnemy(ctx, t, E, solid) {
  if (E.dieAt !== undefined && t > E.dieAt + 0.45) return;
  const p = enemyPos(t, E), fly = E.cy !== undefined;
  let shake = 0, sq = 0, flash = 0;
  for (const h of E.hits || []) {
    const ht = t - h.t;
    if (ht >= 0 && ht < 0.25) shake += Math.sin(ht * 140) * (1 - ht / 0.25) * (h.shake || 3);
    if (ht >= 0 && ht < 0.2) sq += Math.sin(ht / 0.2 * Math.PI) * (h.sq || 0.12);
    if (ht >= 0 && ht < (h.fl || 0.2)) flash = Math.max(flash, 1 - ht / (h.fl || 0.2));
  }
  const fz = E.frozen ? clamp(E.frozen(t)) : 0;
  if (fz > 0.6) shake *= 0.3;
  const bob = fly ? Math.sin(t * 4 + E.x) * 3 * (1 - fz) : 0;
  const dying = E.dieAt !== undefined && t > E.dieAt ? seg(t, E.dieAt, E.dieAt + 0.45) : 0;
  ctx.save(); ctx.translate(p.x + shake, p.y + bob + (fly ? 0 : 44));
  const by = fly ? 0 : -44;                 // centro del cuerpo respecto al origen dibujado
  ctx.scale(1 - sq + dying * 0.2, 1 + sq * 0.6 - dying * 0.4);
  const sk = E.suck ? E.suck(t) : null, skK = sk ? clamp(sk.k) : 0;       // atrapado por una singularidad: se estira hacia ella
  if (skK > 0) { const sa = Math.atan2(sk.y - p.y, sk.x - p.x); ctx.translate(0, fly ? 0 : -44); ctx.rotate(sa); ctx.scale(1 + 0.22 * skK, 1 - 0.14 * skK); ctx.rotate(-sa); ctx.translate(0, fly ? 0 : 44); }
  ctx.globalAlpha = 1 - dying;
  if (fly) {                                // alas que baten (lectura de "volador")
    const fl = Math.sin(t * 22 + E.x) * (1 - fz);
    for (const s of [-1, 1]) {
      ctx.save(); ctx.fillStyle = C.enemyDark; ctx.translate(s * 20, by - 18); ctx.rotate(s * (0.5 + fl * 0.5));
      ctx.beginPath(); ctx.ellipse(s * 14, 0, 16, 7, 0, 0, TAU); ctx.fill(); ctx.restore();
    }
  }
  const rx = fly ? 24 : 30, ry = fly ? 30 : 42;
  ctx.fillStyle = C.enemyDark; ctx.beginPath(); ctx.ellipse(0, by + 2, rx, ry, 0, 0, TAU); ctx.fill();
  ctx.fillStyle = C.enemy; ctx.beginPath(); ctx.ellipse(-3, by - 2, rx - 4, ry - 4, 0, 0, TAU); ctx.fill();
  ctx.fillStyle = C.eye; ctx.beginPath(); ctx.arc(-14, by - 12, 4, 0, TAU); ctx.fill();
  if (E.tint) { const [c, a] = E.tint(t); if (a > 0) { ctx.save(); ctx.globalAlpha = a; ctx.fillStyle = c; ctx.beginPath(); ctx.ellipse(0, by + 2, rx, ry, 0, 0, TAU); ctx.fill(); ctx.restore(); } }
  if (skK > 0) { ctx.save(); ctx.globalAlpha = 0.32 * skK; ctx.fillStyle = PAL.void.mid; ctx.beginPath(); ctx.ellipse(0, by + 2, rx, ry, 0, 0, TAU); ctx.fill(); ctx.restore(); }
  if (solid) { ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.ellipse(0, by + 2, rx, ry, 0, 0, TAU); ctx.fill(); }
  else if (flash > 0) { ctx.save(); ctx.globalAlpha = flash; ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.ellipse(-3, by - 2, rx - 4, ry - 4, 0, 0, TAU); ctx.fill(); ctx.restore(); }
  ctx.restore();
  if (fz > 0) iceBlock(ctx, p.x, p.y + bob, rx + 8, ry + 8, fz, E.x, E.crack ? E.crack(t) : 0);
  if (E.burn) { const b = clamp(E.burn(t)); if (b > 0) for (let i = 0; i < 3; i++) flame(ctx, p.x - 12 + i * 12, p.y - ry * 0.55 + Math.abs(i - 1) * 6, (16 + i % 2 * 6) * b, 6 * b, t, E.x + i * 3, 0.9 * b, 'front'); }
  if (dying > 0) for (let i = 0; i < 10; i++) { const R = rand(E.x + i * 13), a = R() * TAU, d = dying * (20 + R() * 30); disc(ctx, p.x + Math.cos(a) * d, p.y + Math.sin(a) * d - dying * 20, (2 + R() * 2) * (1 - dying), i % 2 ? '#e8d0f0' : '#ffffff', 1 - dying, 'front'); }
}
// bloque de hielo alrededor de un cuerpo: caras semitransparentes, filos y brillos; crack = 0..1 grietas antes de romper
function iceBlock(ctx, x, y, rx, ry, a, seed, crack = 0) {
  const R = rand(seed + 5), n = 7, pts = [];
  for (let i = 0; i < n; i++) { const th = i / n * TAU + R() * 0.4 - Math.PI / 2, k = 0.92 + R() * 0.2; pts.push([x + Math.cos(th) * rx * k, y + Math.sin(th) * ry * k]); }
  const I = PAL.ice;
  ctx.save(); ctx.globalAlpha = 0.5 * a; ctx.fillStyle = col(I.mid, 'front'); ctx.beginPath(); pts.forEach((p, i) => i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1])); ctx.closePath(); ctx.fill();
  ctx.globalAlpha = 0.35 * a; ctx.fillStyle = col(I.light, 'front'); ctx.beginPath(); ctx.moveTo(pts[0][0], pts[0][1]); ctx.lineTo(pts[1][0], pts[1][1]); ctx.lineTo(x, y); ctx.lineTo(pts[n - 1][0], pts[n - 1][1]); ctx.closePath(); ctx.fill();
  ctx.globalAlpha = 0.9 * a; ctx.strokeStyle = col(I.core, 'front'); ctx.lineWidth = 1.6; ctx.lineJoin = 'round'; ctx.beginPath(); pts.forEach((p, i) => i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1])); ctx.closePath(); ctx.stroke();
  ctx.lineWidth = 1; ctx.globalAlpha = 0.6 * a; for (let i = 0; i < n; i += 2) { ctx.beginPath(); ctx.moveTo(pts[i][0], pts[i][1]); ctx.lineTo(x + (R() - 0.5) * 10, y + (R() - 0.5) * 10); ctx.stroke(); }
  if (crack > 0) {
    ctx.strokeStyle = col('#ffffff', 'front'); ctx.lineWidth = 1.8; ctx.globalAlpha = a;
    for (let i = 0; i < 5; i++) { const th = R() * TAU, l = (rx + ry) * 0.5 * crack; ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(x + Math.cos(th) * l * 0.5 + (R() - 0.5) * 6, y + Math.sin(th) * l * 0.5); ctx.lineTo(x + Math.cos(th) * l, y + Math.sin(th) * l); ctx.stroke(); }
  }
  ctx.restore();
  star4(ctx, x - rx * 0.4, y - ry * 0.5, 7 * a, 0.3, '#ffffff', a * (0.6 + 0.4 * Math.sin(performance.now() / 180 + seed)), 'front');
}
function ground(ctx) {
  ctx.fillStyle = '#2f5d7a'; ctx.fillRect(0, GROUND, W, H - GROUND);
  ctx.fillStyle = '#3d7598'; ctx.fillRect(0, GROUND, W, 6);
}

// ---------------------------------------------------------------- trayectorias
// Rayo contra la elipse de un enemigo: primer t (0..1 del segmento a→b) en que entra, o null
function segHitsEllipse(ax, ay, bx, by, cx, cy, rx, ry) {
  const dx = (bx - ax) / rx, dy = (by - ay) / ry, fx = (ax - cx) / rx, fy = (ay - cy) / ry;
  const A = dx * dx + dy * dy, B = 2 * (fx * dx + fy * dy), Cc = fx * fx + fy * fy - 1, D = B * B - 4 * A * Cc;
  if (D < 0 || A === 0) return null;
  const s = Math.sqrt(D), u0 = (-B - s) / (2 * A), u1 = (-B + s) / (2 * A);
  if (u0 >= 0 && u0 <= 1) return { u0, u1 };
  return null;
}
// muestrea una función de posición hacia atrás en el tiempo (para estelas)
function trailPts(fn, t, span, n = 12, tMin = -1e9) {
  const pts = [];
  for (let i = 0; i <= n; i++) { const tt = Math.max(tMin, t - span * i / n); pts.push(fn(tt)); }
  return pts;
}

// ---------------------------------------------------------------- registro de habilidades y clips
// addAbility({ id, num, name, pitch, fit, ideas:[…], clips:[{ title, tag, text, P, tab? }], build(P) → R })
// R = { total, draw(ctx,t,layer), enemies:[E], playerPose(t)→{lean,squash,aura,auraA}, shakes:[{t,a,d}], frames:[{t,d}], sfx:[{t,f}] }
const ABIL = [];
function addAbility(a) { ABIL.push(a); }

function shakeAt(t, R) {
  let s = 0;
  for (const k of R.shakes || []) { const u = seg(t, k.t, k.t + (k.d || 0.3)); if (u > 0 && u < 1) s += Math.sin(u * 120) * (1 - u) * k.a; }
  return s;
}
function scene(ctx, t, R) {
  const sk = shakeAt(t, R);
  const dark = framesOn && (R.frames || []).some(f => t >= f.t && t < f.t + (f.d || 0.05));
  ctx.clearRect(0, 0, W, H);
  ctx.save(); ctx.translate(sk, sk * 0.5); ground(ctx);
  R.draw(ctx, t, 'back');
  const pose = R.playerPose ? R.playerPose(t) : {};
  player(ctx, PX, GROUND, 1, pose);
  if (dark) { ctx.fillStyle = 'rgba(10,6,36,0.62)'; ctx.fillRect(-20, -20, W + 40, H + 40); }
  for (const E of R.enemies) drawEnemy(ctx, t, E, dark && (E.hits || []).some(h => t >= h.t - 0.01 && t < h.t + 0.08));
  R.draw(ctx, t, 'front');
  ctx.restore();
}
// pose típica: se prepara (inclina atrás) y retrocede al soltar
function poseCast(t, t0, t1, o = {}) {
  const prep = easeOut(seg(t, t0, t1)), rec = seg(t, t1, t1 + 0.18);
  return { lean: -prep * (o.lean || 0.12) * (t < t1 ? 1 : 0) + (t >= t1 ? (o.kick || 0.14) * (1 - rec) : 0), squash: (t < t1 ? prep * 0.05 : -0.08 * (1 - rec)) };
}
