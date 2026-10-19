// fx.js — piezas de dibujo de cada habilidad, compartidas por los clips (a1-…a6-*.js) y la versión jugable (jugable.html).
// Lo que se apruebe aquí es lo que se porta a Unity: no duplicar estos dibujos en otro sitio.
'use strict';

// ---------------------------------------------------------------- de a2-singularidad.js
// disco de acreción: anillos inclinados + trazos que giran; mitad de arriba detrás, mitad de abajo delante
function singularityDisk(ctx, t, layer, x, y, s, pal, a, spin = 1) {
  if (a <= 0 || s <= 0) return;
  const rot = -0.28, half = layer === 'back' ? [Math.PI, TAU] : [0, Math.PI];
  for (const [k, c, w, aa] of [[1.25, pal.dark, 7, 0.5], [1, pal.deep, 5, 0.75], [0.8, pal.mid, 3.4, 0.9], [0.62, pal.light, 2.2, 1], [0.48, pal.core, 1.4, 1]])
    ellArc(ctx, x, y, 46 * s * k, 11 * s * k, half[0], half[1], w * s, c, a * aa, layer, { rot, taper: 0, wob: 0.06, seed: k * 10, m: 28 });
  const R = rand(17);
  for (let i = 0; i < 9; i++) {                       // trazos que giran (dan la sensación de rotación)
    const k = 0.55 + R() * 0.75, a0 = R() * TAU + t * 7 * spin / k, span = 0.5 + R() * 0.6, mid = a0 + span / 2;
    if ((Math.sin(mid) < 0) !== (layer === 'back')) continue;
    ellArc(ctx, x, y, 46 * s * k, 11 * s * k, a0, a0 + span, 2.6 * s, i % 3 ? pal.light : pal.core, a * 0.95, layer, { rot, m: 10 });
  }
}
function singularityCore(ctx, t, x, y, s, pal, a) {
  if (a <= 0) return;
  glow(ctx, x, y, 60 * s, pal.g1, 0.55 * a);
  glow(ctx, x, y, 26 * s, pal.g2, 0.35 * a);
  disc(ctx, x, y, 13 * s, pal.deep, 0.6 * a, 'front');
  disc(ctx, x, y, 10.5 * s, PAL.void.dark, a, 'front');
  ellArc(ctx, x, y, 11 * s, 11 * s, 0, TAU, 1.8 * s, pal.core, a, 'front', { taper: 0, m: 30 });
  ellArc(ctx, x, y, 17 * s, 17 * s, t * 3, t * 3 + TAU, 1.2 * s, pal.light, 0.5 * a, 'front', { taper: 0, wob: 0.12, seed: 3, m: 30 });
}

// ---------------------------------------------------------------- de a3-rayo.js
// haz entre dos puntos, por capas; env = grosor 0..1; seed cambia el ruido
function energyBeam(ctx, t, layer, x0, y0, x1, y1, env, pal, o = {}) {
  if (env <= 0.01) return;
  const n = 36, dx = x1 - x0, dy = y1 - y0, L = Math.hypot(dx, dy) || 1, nx = -dy / L, ny = dx / L, sc = o.sc || 1, sd = o.seed || 0;
  const pt = (s, off = 0) => { const wv = Math.sin(s * L * 0.05 - t * 30 + sd) * 1.3 * env; return { x: x0 + dx * s + nx * (wv + off), y: y0 + dy * s + ny * (wv + off) }; };
  const pts = []; for (let i = 0; i <= n; i++) pts.push(pt(i / n));
  const breathe = s => 1 + 0.16 * Math.sin(s * L * 0.09 - t * 45 + sd) + 0.08 * noise1(s * 9 - t * 11, sd);
  const rise = s => 0.55 + 0.45 * seg(s, 0, 0.1);
  if (layer === 'front') {
    for (const [w, c, a] of [[28, pal.dark, 0.2], [17, pal.deep, 0.45], [11, pal.mid, 0.9], [6, pal.light, 1], [2.6, pal.core, 1]])
      ribbon(ctx, pts, s => w * sc * env * breathe(s) * rise(s), c, a, layer);
    // anillos de flujo que avanzan por el haz
    for (let i = 0; i < 6; i++) {
      const s = ((t * 900 / L) + i / 6) % 1, p = pt(s), w = 9 * sc * env * breathe(s);
      ctx.save(); ctx.globalAlpha = clamp(0.55 * env * Math.sin(Math.PI * s)); ctx.strokeStyle = col(pal.core, layer); ctx.lineWidth = 1.4;
      ctx.translate(p.x, p.y); ctx.rotate(Math.atan2(dy, dx)); ctx.beginPath(); ctx.ellipse(0, 0, 2.6, w, 0, 0, TAU); ctx.stroke(); ctx.restore();
    }
    // motas que corren hacia delante
    const R = rand(sd + 3);
    for (let i = 0; i < 12; i++) {
      const sp = 0.8 + R() * 0.8, s = ((t * sp * 1.6) + R()) % 1, off = (R() - 0.5) * 16 * sc * env, p = pt(s, off);
      drop(ctx, p.x, p.y, Math.atan2(dy, dx), (6 + R() * 8) * sc, 1.4 * sc, i % 2 ? pal.core : pal.light, env * Math.sin(Math.PI * s), layer);
    }
  }
  // dos hilos en hélice (por detrás y por delante del eje)
  for (let k = 0; k < 2; k++) {
    const ph = s => s * L * 0.07 - t * 50 + k * Math.PI;
    const seg2 = []; let cur = null;
    for (let i = 0; i <= n; i++) {
      const s = i / n, front = Math.cos(ph(s)) > 0;
      if ((layer === 'front') !== front) { if (cur) { seg2.push(cur); cur = null; } continue; }
      (cur = cur || []).push(pt(s, Math.sin(ph(s)) * 9 * sc * env * rise(s)));
    }
    if (cur) seg2.push(cur);
    for (const sgm of seg2) if (sgm.length > 1) { ctx.save(); ctx.globalAlpha = clamp(env); ctx.strokeStyle = col(k ? pal.light : pal.core, layer); ctx.lineWidth = 1.7 * sc; ctx.lineCap = 'round'; ctx.beginPath(); sgm.forEach((p, i) => i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y)); ctx.stroke(); ctx.restore(); }
  }
}
// punto de contacto: luz, destello que parpadea y chispas continuas que saltan hacia atrás
function beamContact(ctx, t, layer, x, y, env, pal, t0, t1, sc = 1, seed = 0) {
  if (layer === 'front' && env > 0) {
    glow(ctx, x, y, 46 * sc * env, pal.g1, 0.7 * env); glow(ctx, x, y, 20 * sc * env, pal.g2, 0.9 * env);
    star4(ctx, x, y, (18 + 6 * Math.sin(t * 70 + seed)) * sc * env, t * 4 + seed, pal.core, env, layer);
    disc(ctx, x, y, 5 * sc * env, pal.core, env, layer);
  }
  for (let e = t0; e < Math.min(t, t1); e += 0.035) if (t - e < 0.4) sparks(ctx, t, e, x, y, layer, { n: 3, a0: Math.PI - 1.1, a1: Math.PI + 1.1, sp: 260 * sc, pal, seed: Math.floor(e * 1000) + seed, len: 12, life: 0.3, w: 2.2 });
}

// ---------------------------------------------------------------- de a4-media-luna.js
// media luna centrada en (x,y), girada rot; R = radio exterior
function crescentPath(ctx, R, th) { ctx.beginPath(); ctx.arc(0, 0, R, -1.95, 1.95); ctx.arc(R * th, 0, R * (1 - th * 0.55), 1.75, -1.75, true); ctx.closePath(); }
function crescentBlade(ctx, x, y, R, rot, pal, a, layer) {
  if (a <= 0) return;
  glow(ctx, x, y, R * 2.2, pal.g1, 0.45 * a);
  for (const [k, th, c, aa] of [[1.18, 0.32, pal.dark, 0.45], [1.0, 0.36, pal.deep, 0.9], [0.92, 0.42, pal.mid, 1], [0.86, 0.5, pal.light, 1]]) {
    ctx.save(); ctx.globalAlpha = clamp(a * aa); ctx.translate(x, y); ctx.rotate(rot); ctx.fillStyle = col(c, layer); crescentPath(ctx, R * k, th); ctx.fill(); ctx.restore();
  }
  ctx.save(); ctx.globalAlpha = clamp(a); ctx.translate(x, y); ctx.rotate(rot); ctx.strokeStyle = col(pal.core, layer); ctx.lineWidth = 2; ctx.lineCap = 'round';
  ctx.beginPath(); ctx.arc(0, 0, R * 0.93, -1.6, 1.6); ctx.stroke(); ctx.restore();
}

// ---------------------------------------------------------------- de a5-semilla.js
function starSeed(ctx, t, x, y, rot, pal, a, layer, big = 1) {
  if (layer !== 'front' || a <= 0) return;
  glow(ctx, x, y, 24 * big, pal.g1, 0.6 * a); glow(ctx, x, y, 11 * big, pal.g2, 0.8 * a);
  star4(ctx, x, y, 13 * big, rot, pal.mid, a, layer, 0.28); star4(ctx, x, y, 10 * big, rot + 0.78, pal.light, a, layer, 0.22);
  star4(ctx, x, y, 7 * big, rot, pal.core, a, layer, 0.2); disc(ctx, x, y, 3 * big, pal.core, a, layer);
}
// nova de escarcha: picos que brotan del suelo desde el centro hacia fuera
function frostNova(ctx, t, layer, t0, x, reach) {
  if (t < t0) return;
  const I = PAL.ice, u = t - t0;
  for (let i = 0; i < 22; i++) {
    const R = rand(i + 600), side = i % 2 ? 1 : -1, d = 8 + (i / 22) * reach + R() * 10, tOn = t0 + d / 500, g = easeOut(seg(t, tOn, tOn + 0.1)) * (1 - seg(u, 1.0, 1.4));
    const lay = R() < 0.4 ? 'back' : 'front'; if (lay !== layer) continue;
    const h = (34 - d * 0.18) * (0.6 + R() * 0.6);
    iceSpike(ctx, x + side * d, GROUND + 3, h * g, 4 + R() * 3, side * (0.12 + R() * 0.25), g, layer);
  }
  groundRing(ctx, x, GROUND, 20 + reach * easeOut(seg(u, 0, 0.3)), 3, I.core, 1 - seg(u, 0, 0.35), layer, { seed: 5 });
  if (layer === 'front') { glow(ctx, x, GROUND - 20, 120, I.g1, 0.6 * (1 - seg(u, 0, 0.3))); star4(ctx, x, GROUND - 14, 46, 0, I.core, 1 - seg(u, 0, 0.16), layer); }
  dust(ctx, t, t0, x, layer, { n: 12, reach: reach * 0.9, seed: 61, life: 1.1, sc: 1.3 });
  sparks(ctx, t, t0, x, GROUND - 8, layer, { n: 14, a0: -Math.PI * 0.95, a1: -Math.PI * 0.05, pal: I, sp: 300, seed: 63, len: 14 });
}

// ---------------------------------------------------------------- de a6-golpe-estelar.js
// círculo rúnico en el suelo: arcos afilados que giran en sentidos contrarios, marcas radiales y columna guía tenue
function runeCircle(ctx, t, layer, x, r, a, pal, seed = 0) {
  if (a <= 0) return;
  for (let i = 0; i < 4; i++) {
    const a0 = (i / 4) * TAU + t * (i % 2 ? 1.6 : -1.2) + seed;
    const half = Math.sin(a0 + 0.6) < 0 ? 'back' : 'front';
    if (half === layer) ellArc(ctx, x, GROUND + 1, r, r * 0.24, a0, a0 + 1.2, 3, i % 2 ? pal.mid : pal.light, a, layer, { m: 12 });
  }
  groundRing(ctx, x, GROUND + 1, r * 0.7, 1.4, pal.core, a * 0.8, layer, { taper: 0, seed: 4 });
  for (let i = 0; i < 12; i++) {
    const th = i / 12 * TAU - t * 0.8 + seed, cx = x + Math.cos(th) * r * 0.85, cy = GROUND + 1 + Math.sin(th) * r * 0.85 * 0.24, lay = Math.sin(th) < 0 ? 'back' : 'front';
    if (lay === layer) needle(ctx, cx, cy - 3, cx, cy + 3, 2, pal.light, a * 0.9, layer);
  }
  if (layer === 'back' && !showLayers) {
    const g = ctx.createLinearGradient(0, 0, 0, GROUND); g.addColorStop(0, rgba(pal.g2, 0)); g.addColorStop(1, rgba(pal.g2, 0.22 * a));
    ctx.save(); ctx.fillStyle = g; ctx.fillRect(x - r * 0.5, 0, r, GROUND); ctx.restore();
  }
}
// lanza de luz que cae: columna por capas desde arriba hasta la cabeza (hy); tras el impacto se afina
function starLance(ctx, t, layer, x, hy, w, a, pal) {
  if (layer !== 'front' || a <= 0) return;
  const pts = []; for (let i = 0; i <= 14; i++) pts.push({ x: x + Math.sin(i * 1.3 + t * 40) * 1.2, y: lerp(hy, -40, i / 14) });
  for (const [k, c, aa] of [[1.7, pal.dark, 0.3], [1.15, pal.deep, 0.55], [0.8, pal.mid, 0.9], [0.5, pal.light, 1], [0.22, pal.core, 1]])
    ribbon(ctx, pts, s => w * k * (1 - 0.35 * s), c, a * aa, layer);
  glow(ctx, x, hy, w * 2.6, pal.g1, 0.7 * a);
  lens(ctx, x, hy - w * 0.6, Math.PI / 2, w * 2.6, w * 0.7, pal.core, a, layer);
}
