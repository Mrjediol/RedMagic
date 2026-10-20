// fx-armas2.js — dibujo de las armas 7+ (Reloj, Ráfaga, Látigo, Jabalina, Enjambre…), con las primitivas de core.js.
// Mismo papel que fx.js: es lo que se porta a Unity (FxCanvas), no duplicarlo en los g-*.js. Capas: 'back' detrás de los
// enemigos y 'front' delante; cada pieza dibuja su mitad en la capa que le toca.
'use strict';

// ---------------------------------------------------------------- Reloj de arena (g-reloj.js)
// Burbuja de tiempo: esfera translúcida con esfera de reloj (12 marcas), dos agujas y un anillo ecuatorial inclinado
// (mitad de atrás detrás, mitad de delante delante). o = { mode: 'slow'|'stasis'|'echo'|'quick', left (0..1 de vida que
// queda), open (0..1 al abrirse), hand (ángulo de la aguja larga) }
function chronoBubble(ctx, t, layer, x, y, r, a, pal, o = {}) {
  if (a <= 0 || r <= 0) return;
  const mode = o.mode || 'slow', open = o.open === undefined ? 1 : o.open, R = r * (0.35 + 0.65 * easeOut(open)), hand = o.hand || 0;
  const tint = mode === 'stasis' ? '#d9e4e8' : pal.mid, rimC = mode === 'stasis' ? '#f2f7f8' : pal.light;
  const half = layer === 'back' ? [Math.PI, TAU] : [0, Math.PI];
  if (layer === 'back') {
    // relleno: casi transparente en el centro, más denso en el borde (se lee como cristal)
    if (!showLayers) {
      const g = ctx.createRadialGradient(x, y, R * 0.2, x, y, R);
      g.addColorStop(0, rgba(pal.g2, 0.03)); g.addColorStop(0.75, rgba(pal.g1, 0.08)); g.addColorStop(1, rgba(pal.g1, 0.22));
      ctx.save(); ctx.globalAlpha = clamp(a); ctx.fillStyle = mode === 'stasis' ? 'rgba(220,232,236,.16)' : g; ctx.beginPath(); ctx.arc(x, y, R, 0, TAU); ctx.fill(); ctx.restore();
    }
    // marcas de la esfera (12; las de 3-6-9-12 más largas)
    for (let i = 0; i < 12; i++) {
      const th = i / 12 * TAU - Math.PI / 2, big = i % 3 === 0, l = R * (big ? 0.16 : 0.09);
      needle(ctx, x + Math.cos(th) * (R - l - 3), y + Math.sin(th) * (R - l - 3), x + Math.cos(th) * (R - 3), y + Math.sin(th) * (R - 3), big ? 3.4 : 2, i % 3 ? pal.light : pal.core, a * (big ? 0.9 : 0.6), layer);
    }
    // vida que queda: arco fino que se vacía en el sentido del reloj desde las 12
    if (o.left !== undefined) ellArc(ctx, x, y, R + 7, R + 7, -Math.PI / 2, -Math.PI / 2 + TAU * clamp(o.left), 2.2, pal.core, 0.7 * a, layer, { taper: 0, m: 48 });
  }
  // anillo ecuatorial inclinado que gira (giroscopio)
  const spin = mode === 'stasis' ? 0 : t * (mode === 'quick' ? 3.2 : mode === 'echo' ? 1.2 : 0.7);
  for (const [k, c, w, aa] of [[1.08, pal.deep, 4, 0.55], [1.02, rimC, 2, 0.95]])
    ellArc(ctx, x, y, R * k, R * k * 0.26, half[0], half[1], w, c, a * aa, layer, { rot: -0.22, taper: 0, wob: 0.03, seed: 4 + k, m: 30 });
  for (let i = 0; i < 3; i++) {                                // cuentas que recorren el anillo (se ve girar)
    const th = spin + i * TAU / 3, front = Math.sin(th) > 0;
    if (front !== (layer === 'front')) continue;
    const ex = Math.cos(th) * R * 1.04, ey = Math.sin(th) * R * 1.04 * 0.26, cr = Math.cos(-0.22), sr = Math.sin(-0.22);
    disc(ctx, x + ex * cr - ey * sr, y + ex * sr + ey * cr, 3.2, pal.core, a, layer);
  }
  if (layer !== 'front') return;
  // borde
  ellArc(ctx, x, y, R, R, 0, TAU, mode === 'stasis' ? 2.6 : 2, rimC, 0.85 * a, layer, { taper: 0, wob: mode === 'stasis' ? 0.03 : 0.012, seed: 9, m: 56 });
  ellArc(ctx, x, y, R * 0.97, R * 0.97, -2.2, -0.9, 3, pal.core, 0.5 * a, layer, { taper: 1, m: 20 });   // brillo de cristal arriba
  // agujas (en Eco, dos fantasmas detrás; en Aceleración, estela)
  const handPair = (h, aa) => {
    const m = h, hr = h / 12;
    needle(ctx, x, y, x + Math.cos(m) * R * 0.78, y + Math.sin(m) * R * 0.78, 4, pal.core, aa, layer);
    needle(ctx, x, y, x + Math.cos(hr) * R * 0.48, y + Math.sin(hr) * R * 0.48, 5.5, pal.light, aa, layer);
  };
  if (mode === 'echo') { handPair(hand - 0.5, 0.25 * a); handPair(hand - 0.25, 0.45 * a); }
  if (mode === 'quick') for (let k = 1; k <= 4; k++) handPair(hand - k * 0.18, 0.18 * a * (1 - k / 5));
  handPair(hand, a);
  disc(ctx, x, y, 5, pal.deep, a, layer); disc(ctx, x, y, 2.6, pal.core, a, layer);
  // arena que flota (sube despacio en lento, cae rápido en aceleración, quieta en estasis)
  const Rn = rand(31), vy = mode === 'quick' ? 90 : mode === 'stasis' ? 0 : -12;
  for (let i = 0; i < 14; i++) {
    const ang = Rn() * TAU, rr = Math.sqrt(Rn()) * R * 0.85, ph = Rn() * 10, ox = Math.cos(ang) * rr;
    let oy = Math.sin(ang) * rr + ((t + ph) * vy % (R * 1.6));
    if (oy > R) oy -= R * 1.6; if (oy < -R) oy += R * 1.6;
    if (ox * ox + oy * oy > R * R * 0.85) continue;
    disc(ctx, x + ox, y + oy, 1.2 + Rn() * 1.4, i % 3 ? pal.light : pal.core, 0.55 * a, layer);
  }
  if (mode === 'stasis') {                                     // grietas finas de cristal: el tiempo está roto
    const Rs = rand(77);
    for (let i = 0; i < 5; i++) { const th = Rs() * TAU, l0 = R * (0.55 + Rs() * 0.3); needle(ctx, x + Math.cos(th) * l0, y + Math.sin(th) * l0, x + Math.cos(th + 0.12) * R, y + Math.sin(th + 0.12) * R, 1.4, '#ffffff', 0.6 * a, layer); }
  }
}
// orbe que viaja hasta donde se abre la burbuja: esfera de reloj pequeña con estela
function chronoOrb(ctx, t, layer, x, y, a, pal, trail) {
  if (a <= 0) return;
  if (layer === 'back' && trail && trail.length > 1) { ribbon(ctx, trail, s => 9 * (1 - s), pal.deep, 0.45 * a, layer); ribbon(ctx, trail, s => 4 * (1 - s), pal.light, 0.8 * a, layer); }
  if (layer !== 'front') return;
  glow(ctx, x, y, 34, pal.g1, 0.6 * a);
  disc(ctx, x, y, 9, pal.deep, a, layer); disc(ctx, x, y, 7, pal.mid, a, layer);
  ellArc(ctx, x, y, 11, 11, 0, TAU, 1.6, pal.core, a, layer, { taper: 0, m: 24 });
  needle(ctx, x, y, x + Math.cos(t * 9) * 8, y + Math.sin(t * 9) * 8, 2.2, pal.core, a, layer);
}
// eco: el golpe se repite → dos anillos que se cierran sobre el enemigo y un destello «fantasma»
function echoRing(ctx, t, layer, t0, x, y, r, pal) {
  const u = seg(t, t0, t0 + 0.35); if (u >= 1 || layer !== 'front') return;
  for (const [k, w] of [[1, 2.4], [0.7, 1.4]]) ellArc(ctx, x, y, r * k * (1.6 - 0.6 * easeOut(u)), r * k * (1.6 - 0.6 * easeOut(u)), 0, TAU, w, k === 1 ? pal.light : pal.core, 0.8 * (1 - u), layer, { taper: 0, wob: 0.05, seed: 3 + k, m: 32 });
  star4(ctx, x, y, 22 * (1 - u * 0.5), 0.8, pal.core, 0.8 * (1 - u), layer);
}
// estasis que se suelta: estallido de cristal gris-blanco + el impacto normal en la paleta del arma
function stasisBreak(ctx, t, layer, t0, x, y, sc, pal) {
  const u = t - t0; if (u < 0 || u > 1) return;
  impact(ctx, t, layer, t0, x, y, 0.6 + 0.5 * sc, pal, { spread: TAU, seed: Math.floor(x), needles: 12, drops: 8 });
  const Rn = rand(Math.floor(x) + 5);
  for (let i = 0; i < 10; i++) {
    const ang = Rn() * TAU, v = 120 + Rn() * 200, lay = Rn() < 0.5 ? 'back' : 'front'; if (lay !== layer) continue;
    const px = x + Math.cos(ang) * v * u, py = y + Math.sin(ang) * v * u + 260 * u * u;
    chunk(ctx, px, py, 3 + Rn() * 5, u * 9 + i, i + 3, '#dfe9ec', '#ffffff', 1 - u, layer);
  }
}
// rastro corto de un proyectil acelerado (Aceleración): rayitas detrás
function speedStreaks(ctx, t, layer, t0, x, y, ang, pal) {
  const u = seg(t, t0, t0 + 0.3); if (u >= 1 || layer !== 'front') return;
  for (let i = 0; i < 4; i++) { const off = (i - 1.5) * 5, bx = x - Math.cos(ang) * (10 + i * 6) + Math.cos(ang + Math.PI / 2) * off, by = y - Math.sin(ang) * (10 + i * 6) + Math.sin(ang + Math.PI / 2) * off;
    needle(ctx, bx, by, bx - Math.cos(ang) * 26 * (1 - u), by - Math.sin(ang) * 26 * (1 - u), 2, i % 2 ? pal.core : pal.light, 1 - u, layer); }
}

// ---------------------------------------------------------------- Ráfaga (g-rafaga.js)
// cono de viento: trazos curvos que salen del cristal y se abren; u = 0..1 de la vida del soplo
function gustCone(ctx, t, layer, t0, x, y, ang, range, half, pal, o = {}) {
  const life = o.life || 0.35, u = (t - t0) / life; if (u < 0 || u >= 1) return;
  const R = rand(o.seed || 21), n = o.n || 11;
  for (let i = 0; i < n; i++) {
    const off = (R() * 2 - 1) * half, lay = R() < 0.4 ? 'back' : 'front'; if (lay !== layer) continue;
    const d0 = range * (0.05 + 0.8 * easeOut(u)) * (0.7 + R() * 0.4), len = range * (0.18 + R() * 0.2) * (1 - u * 0.5), curl = (R() - 0.5) * 0.6;
    const pts = []; for (let k = 0; k <= 8; k++) { const s = k / 8, d = d0 - len * (1 - s), a = ang + off + curl * s * s; if (d < 4) continue; pts.push({ x: x + Math.cos(a) * d, y: y + Math.sin(a) * d }); }
    if (pts.length > 1) { ribbon(ctx, pts, s => (o.w || 3.4) * Math.sin(Math.PI * s), i % 3 ? pal.light : pal.core, (1 - u) * 0.85, layer); }
  }
  if (layer === 'front' && u < 0.4) { glow(ctx, x, y, 30, pal.g1, 0.5 * (1 - u / 0.4)); star4(ctx, x, y, 14 * (1 - u), ang, pal.core, 1 - u / 0.4, layer); }
}
// remolino que queda colgado tras el soplo (espiral pequeña que se deshace)
function windSwirl(ctx, t, layer, t0, x, y, r, pal) {
  const u = seg(t, t0, t0 + 0.6); if (u >= 1) return;
  ellArc(ctx, x, y, r * (0.5 + u), r * (0.5 + u) * 0.6, t * 8, t * 8 + 4.2, 2.2, pal.light, 0.7 * (1 - u), layer, { taper: 1, m: 20 });
}
// tornado: anillos inclinados apilados que se ensanchan hacia arriba y giran; base con polvo. h = alto, w = ancho de la boca
function tornadoFx(ctx, t, layer, x, h, w, pal, a, seed = 0) {
  if (a <= 0) return;
  const n = 9;
  for (let i = 0; i < n; i++) {
    const s = i / (n - 1), y = GROUND - 6 - s * h, rr = lerp(w * 0.18, w, Math.pow(s, 0.8)), sway = Math.sin(t * 3 + s * 4 + seed) * 10 * s, sp = t * (9 - s * 3) + i * 0.7;
    const half = layer === 'back' ? [Math.PI, TAU] : [0, Math.PI];
    ellArc(ctx, x + sway, y, rr, rr * 0.22, half[0] + sp % 0.5, half[1] + sp % 0.5, 2.6 + s * 1.5, i % 2 ? pal.light : pal.mid, a * (0.55 + 0.4 * s), layer, { taper: 1, wob: 0.15, seed: i + seed, m: 16 });
  }
  // trazos que suben girando
  const R = rand(seed + 5);
  for (let i = 0; i < 10; i++) {
    const ph = (t * (0.7 + R() * 0.5) + R()) % 1, s = ph, th = t * 7 + R() * TAU, rr = lerp(w * 0.18, w, Math.pow(s, 0.8)) * 0.95, front = Math.sin(th) > 0;
    if (front !== (layer === 'front')) continue;
    const px = x + Math.cos(th) * rr + Math.sin(t * 3 + s * 4 + seed) * 10 * s, py = GROUND - 6 - s * h + Math.sin(th) * rr * 0.22;
    drop(ctx, px, py, th + Math.PI / 2 * Math.sign(Math.cos(th) || 1), 10 + s * 8, 1.6, pal.core, a * Math.sin(Math.PI * ph), layer);
  }
  if (layer === 'front') { glow(ctx, x, GROUND - h * 0.4, w * 1.1, pal.g1, 0.25 * a); }
  dust(ctx, t, t - ((t * 2) % 0.6), x, layer, { n: 4, reach: w * 0.8, sc: 0.5, seed: Math.floor(t * 2) + seed, y: GROUND });
}
// corriente de viento: banda con trazos que corren en su dirección (x0,y0 = inicio; ang; len; wid = medio ancho)
function windCurrent(ctx, t, layer, x0, y0, ang, len, wid, a, pal, seed = 0) {
  if (a <= 0) return;
  const c = Math.cos(ang), s = Math.sin(ang), nx = -s, ny = c, R = rand(seed + 3);
  const P = (d, o) => ({ x: x0 + c * d + nx * o, y: y0 + s * d + ny * o });
  if (layer === 'back') for (const sg of [-1, 1]) { const p0 = P(0, sg * wid), p1 = P(len, sg * wid); strokeTaper(ctx, [p0, p1], 1.6, pal.mid, 0.35 * a, layer, false); }
  for (let i = 0; i < 16; i++) {
    const lane = (R() * 2 - 1) * wid * 0.85, sp = 0.8 + R() * 0.7, ph = (t * sp * 1.4 + R()) % 1, d = ph * len, l = len * (0.08 + R() * 0.08), lay = R() < 0.45 ? 'back' : 'front';
    if (lay !== layer) continue;
    const wv = k => Math.sin((d - k) * 0.03 + t * 4 + i) * 4;
    const pts = []; for (let k = 0; k <= 6; k++) { const dd = d - l * (1 - k / 6); if (dd < 0) continue; pts.push(P(dd, lane + wv(l * (1 - k / 6)))); }
    if (pts.length > 1) ribbon(ctx, pts, q => 2.6 * Math.sin(Math.PI * q), i % 3 ? pal.light : pal.core, a * Math.sin(Math.PI * ph), layer);
  }
}
// martillo de aire: aro de aire comprimido que viaja + trazos; carga = espiral que se cierra sobre el cristal
function ramCharge(ctx, t, layer, x, y, k, pal) {
  if (k <= 0) return;
  const R = rand(13);
  for (let i = 0; i < 8; i++) {
    const a0 = R() * TAU + t * (6 + k * 6), r = (40 - 28 * ((t * 2 + R()) % 1)) * (0.6 + 0.4 * k), front = Math.sin(a0) > 0;
    if (front !== (layer === 'front')) continue;
    ellArc(ctx, x, y, r, r * 0.7, a0, a0 + 1.1, 2, pal.light, 0.8 * k, layer, { taper: 1, m: 10 });
  }
  if (layer === 'front') { glow(ctx, x, y, 22 + 20 * k, pal.g1, 0.6 * k); if (k >= 1) star4(ctx, x, y, 18 + 4 * Math.sin(t * 40), t * 3, pal.core, 1, layer); }
}
function ramRing(ctx, t, layer, t0, x, y, ang, dist, sc, pal) {
  const u = seg(t, t0, t0 + 0.28); if (u >= 1) return;
  const d = dist * easeOut(u), px = x + Math.cos(ang) * d, py = y + Math.sin(ang) * d, r = (18 + 30 * u) * sc;
  ellArc(ctx, px, py, r * 0.35, r, ang + (layer === 'back' ? Math.PI / 2 : -Math.PI / 2), ang + (layer === 'back' ? Math.PI * 1.5 : Math.PI / 2), 4 * sc * (1 - u * 0.5), pal.light, 1 - u, layer, { rot: 0, taper: 0.5, m: 16 });
  if (layer === 'front') for (let i = 0; i < 5; i++) { const o = (i - 2) * 9 * sc; needle(ctx, px - Math.cos(ang) * 40 * sc + Math.cos(ang + 1.57) * o, py - Math.sin(ang) * 40 * sc + Math.sin(ang + 1.57) * o, px + Math.cos(ang + 1.57) * o * 0.6, py + Math.sin(ang + 1.57) * o * 0.6, 2.4 * sc, pal.core, 0.8 * (1 - u), layer); }
}
