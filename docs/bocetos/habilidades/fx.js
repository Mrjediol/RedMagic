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

// Orbe de la singularidad vivo. o = { s (tamaño), pal, bp (paleta del estallido), form 0..1 (formándose), collapse 0..1,
// pullT (s desde que atrae, -1 = no atrae), liveT (s desde que salió), trail [{x,y}] (estela de distorsión en el viaje), gy (suelo) }
function voidOrb(ctx, t, layer, ox, oy, o) {
  const s = o.s, pal = o.pal, form = o.form, cu = o.collapse || 0, ss = s * (0.45 + 0.55 * form) * (1 - easeIn(cu)), gy = o.gy === undefined ? GROUND : o.gy;
  if (o.pullT >= 0) for (let i = 0; i < 3; i++) {                 // anillos de succión que se cierran
    const u = (o.pullT * 2.4 + i / 3) % 1;
    ellArc(ctx, ox, oy, (95 - 80 * easeIn(u)) * s, (95 - 80 * easeIn(u)) * s * 0.92, layer === 'back' ? Math.PI : 0, layer === 'back' ? TAU : Math.PI, 1.6 * s, pal.light, 0.35 * Math.sin(Math.PI * u), layer, { taper: 0, wob: 0.15, seed: i + 3 });
  }
  const R = rand(23), n = o.pullT >= 0 || cu > 0 ? 26 : 10;        // partículas en espiral hacia el centro (aplastadas como el disco)
  for (let i = 0; i < n; i++) {
    const rate = 0.9 + R() * 0.9, ph = R(), a0 = R() * TAU, age = (o.liveT * rate * (cu > 0 ? 3 : 1) + ph) % 1, r = 120 * s * Math.pow(1 - age, 1.4), th = a0 + age * 5.5;
    const rot = -0.28, ex = Math.cos(th) * r, ey = Math.sin(th) * r * 0.42, px = ox + ex * Math.cos(rot) - ey * Math.sin(rot), py = oy + ex * Math.sin(rot) + ey * Math.cos(rot);
    if ((Math.sin(th) < 0) !== (layer === 'back')) continue;
    const th2 = th - 0.25, r2 = r * 1.12, ex2 = Math.cos(th2) * r2, ey2 = Math.sin(th2) * r2 * 0.42, qx = ox + ex2 * Math.cos(rot) - ey2 * Math.sin(rot), qy = oy + ex2 * Math.sin(rot) + ey2 * Math.cos(rot);
    drop(ctx, px, py, Math.atan2(py - qy, px - qx), (5 + 10 * age) * s, (1.4 + 1.2 * age) * s, [pal.core, pal.light, pal.mid, PAL.set.light][i % 4], Math.sin(Math.PI * age) * form, layer);
  }
  if (o.pullT >= 0 && layer === 'front' && gy - oy < 160 * s) {   // polvo del suelo arrastrado (si el orbe está cerca del suelo)
    for (let i = 0; i < 10; i++) {
      const Rr = rand(i * 9 + 1), side = i % 2 ? 1 : -1, x0 = ox + side * (50 + Rr() * 110) * s, rate = 1.2 + Rr(), age = (o.pullT * rate + Rr()) % 1;
      const px = lerp(x0, ox + side * 12, easeIn(age)), py = gy - 3 - age * age * 28 * s;
      strokeTaper(ctx, [{ x: px, y: py }, { x: px + side * 12 * age + side * 4, y: py + 2 }], 3, '#c9d3dc', 0.6 * Math.sin(Math.PI * age), layer);
    }
  }
  singularityDisk(ctx, t, layer, ox, oy, ss, pal, 1);
  if (layer === 'front') singularityCore(ctx, t, ox, oy, ss, pal, 1);
  if (layer === 'back' && o.trail) o.trail.forEach((q, k) => ellArc(ctx, q.x, q.y, 14 * s * (1 + (k + 1) * 0.2), 14 * s * (1 + (k + 1) * 0.2), 0, TAU, 1.4, pal.light, 0.3 - k * 0.1, layer, { taper: 0, wob: 0.2, seed: k + 1 }));
  if (cu > 0 && layer === 'front') {                                // colapso: líneas que convergen y punto blanco que crece
    const Rr = rand(55);
    for (let i = 0; i < 14; i++) { const a = Rr() * TAU, r0 = (90 - 80 * cu) * s, r1 = r0 + 40 * s * (1 - cu); needle(ctx, ox + Math.cos(a) * r0, oy + Math.sin(a) * r0, ox + Math.cos(a) * r1, oy + Math.sin(a) * r1, 2.4 * s, i % 2 ? pal.core : pal.light, cu, layer); }
    glow(ctx, ox, oy, 40 * s * cu, o.bp.g2, cu); disc(ctx, ox, oy, 7 * s * cu, '#ffffff', cu, layer);
  }
}
// Estallido de la singularidad (empieza en t0). o = { mass (grietas y escombros), fire (llamas por el suelo y brasas), gy }
function voidBurst(ctx, t, layer, t0, x, y, s, bp, o = {}) {
  const u = t - t0; if (u < 0 || u > 1.6) return;
  const ru = seg(u, 0, 0.42), fu = seg(u, 0, 0.18), gy = o.gy === undefined ? GROUND : o.gy, nearGround = gy - y < 170 * s;
  if (layer === 'front') {
    glow(ctx, x, y, 140 * s * (1 - fu * 0.4), bp.g1, 0.8 * (1 - fu)); glow(ctx, x, y, 60 * s, bp.g2, 0.9 * (1 - fu));
    star4(ctx, x, y, 70 * s * (1 - fu * 0.4), 0.15, bp.core, 1 - fu, layer, 0.12); disc(ctx, x, y, 18 * s * (1 - fu), bp.core, 1 - fu, layer);
  }
  for (let i = 0; i < 6; i++) {                                     // onda: hebras parciales con ruido (no una media luna sólida)
    const Rr = rand(i + 70), a0 = Rr() * TAU, span = 0.9 + Rr() * 1.4, k = 0.85 + Rr() * 0.3, mid = a0 + span / 2;
    if ((Math.sin(mid) < 0) !== (layer === 'back')) continue;
    const r = (24 + 150 * easeOut(ru)) * s * k;
    ellArc(ctx, x, y, r, r * 0.92, a0, a0 + span, (5 - 4 * ru) * s, i % 2 ? bp.mid : bp.light, 1 - ru, layer, { wob: 0.1, seed: i });
  }
  if (nearGround) groundRing(ctx, x, gy, (20 + 130 * easeOut(ru)) * s, 4 * s * (1 - ru) + 0.4, bp.mid, 1 - ru, layer, { wob: 0.08, seed: 4 });
  const Rr = rand(81);
  for (let i = 0; i < 18; i++) {
    const ang = Rr() * TAU, l0 = (14 + Rr() * 20) * s, nu = seg(u, 0, 0.3), l1 = l0 + (40 + Rr() * 60) * s * easeOut(nu), lay = Rr() < 0.4 ? 'back' : 'front';
    if (lay !== layer || nu >= 1) continue;
    needle(ctx, x + Math.cos(ang) * l0, y + Math.sin(ang) * l0, x + Math.cos(ang) * l1, y + Math.sin(ang) * l1, 3.6 * s * (1 - nu * 0.6), i % 3 ? bp.light : bp.core, 1 - nu, layer);
  }
  sparks(ctx, t, t0, x, y, layer, { n: 16, pal: bp, sp: 380 * s, seed: 12, len: 18, life: 0.55 });
  droplets(ctx, t, t0 + 0.03, x, y, layer, { n: 12, pal: bp, sp: 220 * s, seed: 31 });
  if (nearGround) dust(ctx, t, t0, x, layer, { n: 12, reach: 110 * s, sc: 1.2 * s, seed: 7, y: gy });
  if (o.mass && nearGround) { cracks(ctx, t, t0, x, layer, { n: 8, len: 110, pal: bp }); debris(ctx, t, t0, x, layer, { n: 12, sp: 340, seed: 14 }); }
  if (o.fire) {                                                      // llamas que se abren por el suelo y brasas que suben
    if (nearGround) for (let i = 0; i < 14; i++) {
      const Rf = rand(i * 5 + 2), side = i % 2 ? 1 : -1, d = (14 + Rf() * 120) * s, tOn = t0 + d / (500 * s), lf = 0.8 + Rf() * 0.35;
      const a = seg(t, tOn, tOn + 0.08) * (1 - seg(t, tOn + lf * 0.7, tOn + lf)), lay = Rf() < 0.45 ? 'back' : 'front';
      if (lay === layer) flame(ctx, x + side * d, gy + 1, (22 + Rf() * 22) * a, (6 + Rf() * 5) * Math.max(0.3, a), t, i, a, layer);
    }
    for (let i = 0; i < 16; i++) {
      const Rf = rand(i * 3 + 90), lf = 0.7 + Rf() * 0.6, tt = u - Rf() * 0.3; if (tt < 0 || tt > lf || (Rf() < 0.5 ? 'back' : 'front') !== layer) continue;
      disc(ctx, x + (Rf() - 0.5) * 160 * s + Math.sin(tt * 8 + i) * 8, (nearGround ? gy - 10 : y) - tt * (60 + Rf() * 80), 1.6 + Rf() * 1.6, i % 2 ? PAL.fire.light : PAL.fire.mid, 1 - tt / lf, layer);
    }
  }
}

// ---------------------------------------------------------------- piezas sueltas compartidas
// gema del prisma (rombo en capas que gira)
function prismGem(ctx, t, x, y, a, pal, layer) {
  if (layer !== 'front' || a <= 0) return;
  ctx.save(); ctx.translate(x, y); ctx.rotate(t * 2.5); ctx.globalAlpha = clamp(a);
  for (const [r, c] of [[11, pal.deep], [8, pal.mid], [4.5, pal.core]]) { ctx.fillStyle = col(c, layer); ctx.beginPath(); ctx.moveTo(0, -r * 1.3); ctx.lineTo(r, 0); ctx.lineTo(0, r * 1.3); ctx.lineTo(-r, 0); ctx.closePath(); ctx.fill(); }
  ctx.restore();
}
// robo de vida: gotas carmesí que viajan de (x0,y0) a (x1,y1) ondulando (por detrás y por delante del haz)
function lifeMotes(ctx, t, layer, x0, y0, x1, y1, on) {
  if (on <= 0) return;
  const L2 = PAL.life, R = rand(301), ang = Math.atan2(y1 - y0, x1 - x0), nx = -Math.sin(ang), ny = Math.cos(ang);
  for (let i = 0; i < 14; i++) {
    const sp = 0.9 + R() * 0.7, s = ((t * sp * 1.3) + R()) % 1, wob = Math.sin(s * 14 + i) * 10 * (1 - s), lay = Math.sin(s * 14 + i) > 0 ? 'front' : 'back';
    if (lay !== layer) continue;
    const x = lerp(x0, x1, s) + nx * wob, y = lerp(y0, y1, s) + ny * wob;
    drop(ctx, x, y, ang, 9, 2.6, i % 3 ? L2.mid : L2.light, on * Math.sin(Math.PI * s), layer);
    if (i % 3 === 0) glow(ctx, x, y, 9, L2.g1, 0.5 * on * Math.sin(Math.PI * s));
  }
}
// cruces de curación que suben del player (px, pie en gy); tt = tiempo desde que empezó
function healCrosses(ctx, tt, layer, px, gy, on) {
  if (layer !== 'front' || on <= 0) return;
  const L2 = PAL.life;
  for (let i = 0; i < 6; i++) {
    const R2 = rand(i + 500), age = (tt * 1.2 + R2()) % 1, x = px + (R2() - 0.5) * 36, y = gy - 30 - age * 50, a = Math.sin(Math.PI * age) * on, r = 4 + R2() * 2;
    ctx.save(); ctx.globalAlpha = clamp(a); ctx.fillStyle = col(L2.light, layer); ctx.fillRect(x - r, y - 1.2, r * 2, 2.4); ctx.fillRect(x - 1.2, y - r, 2.4, r * 2); ctx.restore();
  }
}
// columna vertical de luz que sale del suelo (u = s desde el estallido) y se afina
function lightColumn(ctx, x, gy, u, sc, pal, layer) {
  const cu = seg(u, 0, 0.3); if (layer !== 'front' || cu >= 1 || u < 0) return;
  const h = 190 * sc * easeOut(seg(u, 0, 0.06)), w = 30 * sc * (1 - cu);
  glow(ctx, x, gy - h * 0.4, 70 * sc, pal.g1, 0.7 * (1 - cu));
  lens(ctx, x, gy - h / 2, Math.PI / 2, h, w, pal.mid, 0.9 * (1 - cu), layer);
  lens(ctx, x, gy - h / 2, Math.PI / 2, h * 0.92, w * 0.55, pal.light, 1 - cu, layer);
  lens(ctx, x, gy - h / 2, Math.PI / 2, h * 0.85, w * 0.22, pal.core, 1 - cu, layer);
}
// aguja de la lluvia estelar en vuelo (punta en x,y; ang = dirección)
function rainNeedle(ctx, x, y, ang, pal, layer) {
  if (layer !== 'front') return;
  strokeTaper(ctx, [{ x, y }, { x: x - Math.cos(ang) * 120, y: y - Math.sin(ang) * 120 }], 9, pal.deep, 0.5, layer);
  strokeTaper(ctx, [{ x, y }, { x: x - Math.cos(ang) * 100, y: y - Math.sin(ang) * 100 }], 5, pal.mid, 0.9, layer);
  lens(ctx, x - Math.cos(ang) * 14, y - Math.sin(ang) * 14, ang, 38, 6, pal.light, 1, layer);
  lens(ctx, x - Math.cos(ang) * 12, y - Math.sin(ang) * 12, ang, 30, 3, pal.core, 1, layer);
  glow(ctx, x, y, 26, pal.g1, 0.7);
}
// Singularidad por encima de los enemigos: el disco se ordena sólo respecto a su núcleo (mitad trasera detrás del agujero,
// delantera delante) y todo el vórtice va delante. Si la mitad trasera fuera detrás de los enemigos, un enemigo dentro del
// disco "cortaba" el anillo por donde se superpone (lo vio el usuario, 2026-10-19). El atrapado se estira hacia el centro (juego).
function voidOrbOver(ctx, t, layer, ox, oy, o) {
  if (layer !== 'front') return;
  voidOrb(ctx, t, 'back', ox, oy, o);
  voidOrb(ctx, t, 'front', ox, oy, o);
}
