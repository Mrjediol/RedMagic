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

// ---------------------------------------------------------------- Látigo de luz (g-latigo.js)
// cuerda de luz por una lista de puntos (del cristal a la punta): ancha en el mango, fina en la punta, con nudos de luz
function whipRope(ctx, t, layer, pts, pal, a, o = {}) {
  if (a <= 0 || pts.length < 2) return;
  const w = (o.w || 1) * 1.45;
  if (layer === 'back') { ribbon(ctx, pts, s => 9 * w * (1 - s * 0.75), pal.dark, 0.35 * a, layer); return; }
  ribbon(ctx, pts, s => 6.5 * w * (1 - s * 0.8), pal.deep, 0.8 * a, layer);
  ribbon(ctx, pts, s => 4 * w * (1 - s * 0.8), pal.mid, a, layer);
  ribbon(ctx, pts, s => 1.8 * w * (1 - s * 0.7), pal.core, a, layer);
  for (let i = 2; i < pts.length - 1; i += 3) { const p = pts[i], k = 0.5 + 0.5 * Math.sin(t * 20 - i); disc(ctx, p.x, p.y, (2.4 - i / pts.length) * w * k, pal.core, 0.8 * a, layer); }
  const tip = pts[pts.length - 1];
  glow(ctx, tip.x, tip.y, 16 * w * (o.tipGlow || 1), pal.g2, 0.6 * a);
  if (o.hot) star4(ctx, tip.x, tip.y, 12 * w, t * 9, pal.core, a, layer);
}
// chasquido de la punta: destello en cruz, aro corto y chispas
function whipCrack(ctx, t, layer, t0, x, y, sc, pal) {
  const u = seg(t, t0, t0 + 0.25); if (u >= 1) return;
  if (layer === 'front') {
    glow(ctx, x, y, 46 * sc, pal.g1, 0.7 * (1 - u));
    star4(ctx, x, y, 30 * sc * (1 - u * 0.5), 0.4, pal.core, 1 - u, layer);
    ellArc(ctx, x, y, (8 + 30 * easeOut(u)) * sc, (8 + 30 * easeOut(u)) * sc, 0, TAU, 2 * sc, pal.light, 1 - u, layer, { taper: 0, m: 20 });
  }
  sparks(ctx, t, t0, x, y, layer, { n: 8, pal, sp: 300 * sc, seed: Math.floor(x * 7), len: 12, life: 0.3 });
}
// cadena de eslabones de luz entre dos puntos, con comba; pulse 0..1 = destello que la recorre al compartir daño
function lightChain(ctx, t, layer, x0, y0, x1, y1, a, pal, pulse = 0) {
  if (a <= 0) return;
  const L = Math.hypot(x1 - x0, y1 - y0), n = Math.max(3, Math.floor(L / 14)), ang = Math.atan2(y1 - y0, x1 - x0), sag = Math.min(30, L * 0.12);
  if (layer === 'back') { const pts = []; for (let i = 0; i <= 12; i++) { const s = i / 12; pts.push({ x: lerp(x0, x1, s), y: lerp(y0, y1, s) + Math.sin(Math.PI * s) * sag }); } ribbon(ctx, pts, () => 7, pal.deep, 0.35 * a, layer); }
  for (let i = 0; i <= n; i++) {
    const s = i / n, px = lerp(x0, x1, s), py = lerp(y0, y1, s) + Math.sin(Math.PI * s) * sag + Math.sin(t * 3 + s * 6) * 2, flat = i % 2 === 0;
    const glowK = pulse > 0 ? Math.max(0, 1 - Math.abs(s - pulse) * 5) : 0;
    const lay = flat ? 'front' : 'back'; if (lay !== layer) continue;
    const c = glowK > 0.3 ? pal.core : pal.light;
    ellArc(ctx, px, py, 8, flat ? 4.6 : 2, 0, TAU, 2.6, c, a * (0.85 + 0.15 * glowK), layer, { rot: ang, taper: 0, m: 14 });
    if (glowK > 0 && layer === 'front') glow(ctx, px, py, 14, pal.g2, glowK * a);
  }
}

// ---------------------------------------------------------------- Jabalina (g-jabalina.js)
// jabalina de cristal: asta larga y fina con punta facetada; (x,y) = punta; ang = hacia dónde apunta; sc = escala
function crystalSpear(ctx, t, layer, x, y, ang, sc, pal, a, o = {}) {
  if (a <= 0) return;
  const L = 74 * sc, c = Math.cos(ang), s = Math.sin(ang), bx = x - c * L, by = y - s * L;
  if (layer === 'back') { if (o.trail && o.trail.length > 1) { ribbon(ctx, o.trail, q => 7 * sc * (1 - q), pal.deep, 0.4 * a, layer); ribbon(ctx, o.trail, q => 3 * sc * (1 - q), pal.light, 0.7 * a, layer); } return; }
  if (o.glow !== false) glow(ctx, x - c * L * 0.3, y - s * L * 0.3, 34 * sc, pal.g1, 0.45 * a);
  needle(ctx, bx, by, x - c * 14 * sc, y - s * 14 * sc, 5 * sc, pal.deep, a, layer);              // asta
  needle(ctx, bx, by, x - c * 14 * sc, y - s * 14 * sc, 2.2 * sc, pal.light, a, layer);
  lens(ctx, x - c * 10 * sc, y - s * 10 * sc, ang, 26 * sc, 9 * sc, pal.mid, a, layer);            // punta
  lens(ctx, x - c * 8 * sc, y - s * 8 * sc, ang, 18 * sc, 4.4 * sc, pal.core, a, layer);
  lens(ctx, bx + c * 6 * sc, by + s * 6 * sc, ang + 0.7, 12 * sc, 3 * sc, pal.light, 0.8 * a, layer);   // aletas
  lens(ctx, bx + c * 6 * sc, by + s * 6 * sc, ang - 0.7, 12 * sc, 3 * sc, pal.light, 0.8 * a, layer);
  if (o.hot) star4(ctx, x, y, 10 * sc * (0.8 + 0.2 * Math.sin(t * 40)), t * 4, pal.core, a, layer);
}
// pararrayos clavado: chispitas que saltan en la punta y aro tenue del radio
function rodSparks(ctx, t, layer, x, y, r, pal, a) {
  if (a <= 0) return;
  if (layer === 'back') { groundRing(ctx, x, Math.min(GROUND, y + 30), r, 1.4, pal.mid, 0.25 * a, layer, { taper: 0, sq: 0.2 }); return; }
  groundRing(ctx, x, Math.min(GROUND, y + 30), r, 1.4, pal.mid, 0.25 * a, layer, { taper: 0, sq: 0.2 });
  const R = rand(Math.floor(t * 14));
  for (let i = 0; i < 2; i++) { const ang = -Math.PI / 2 + (R() - 0.5) * 2.4, l = 14 + R() * 18; bolt(ctx, x, y, x + Math.cos(ang) * l, y + Math.sin(ang) * l, 4, R, 1.2, pal, 0.8 * a, layer, 0.5); }
  glow(ctx, x, y, 22, pal.g2, (0.4 + 0.3 * Math.sin(t * 25)) * a);
}
// rayo del pararrayos a un enemigo: zigzag que se apaga en 0,2 s (cambia de forma a saltos)
function rodBolt(ctx, t, layer, t0, x0, y0, x1, y1, pal, w = 2.2) {
  const u = seg(t, t0, t0 + 0.22); if (u >= 1 || layer !== 'front') return;
  const R = rand(Math.floor(t * 30) + Math.floor(x1));
  bolt(ctx, x0, y0, x1, y1, 8, R, w * (1 - u * 0.6), pal, 1 - u, layer, 0.3);
  glow(ctx, x1, y1, 26, pal.g2, 0.7 * (1 - u));
}
// destello (teletransporte): estela de siluetas desde donde estabas hasta donde llegas + aro al llegar
function blinkTrail(ctx, t, layer, t0, x0, y0, x1, y1, pal) {
  const u = seg(t, t0, t0 + 0.35); if (u >= 1) return;
  if (layer === 'back') { const pts = []; for (let i = 0; i <= 10; i++) { const s = i / 10; pts.push({ x: lerp(x0, x1, s), y: lerp(y0, y1, s) - Math.sin(Math.PI * s) * 20 }); } ribbon(ctx, pts, s => 14 * s * (1 - u), pal.mid, 0.6 * (1 - u), layer); ribbon(ctx, pts, s => 5 * s * (1 - u), pal.core, 1 - u, layer); return; }
  for (let i = 0; i < 4; i++) { const s = i / 4, x = lerp(x0, x1, s), y = lerp(y0, y1, s); ctx.save(); ctx.globalAlpha = clamp((1 - u) * 0.35 * (s + 0.2)); ctx.fillStyle = col(pal.light, layer); ctx.beginPath(); ctx.ellipse(x, y - 38, 12, 34, 0, 0, TAU); ctx.fill(); ctx.restore(); }
  shockRing(ctx, t, layer, t0, x1, y1 - 30, 70, pal);
}

// ---------------------------------------------------------------- Enjambre (g-enjambre.js)
// luciérnaga: núcleo que late, halo cálido, alitas que baten y estela corta. sc = tamaño (reina = 1.6)
function firefly(ctx, t, layer, x, y, ang, sc, pal, a, trail, seed = 0) {
  if (a <= 0) return;
  if (layer === 'back') { if (trail && trail.length > 1) ribbon(ctx, trail, s => 3.2 * sc * (1 - s), pal.mid, 0.55 * a, layer); return; }
  const pu = 0.75 + 0.25 * Math.sin(t * 14 + seed * 3.1);
  glow(ctx, x, y, 16 * sc * pu, pal.g1, 0.55 * a);
  const fl = Math.sin(t * 60 + seed) * 0.6;
  for (const sg of [-1, 1]) lens(ctx, x - Math.cos(ang) * 2 * sc + Math.cos(ang + sg * (1.9 + fl)) * 4 * sc, y - Math.sin(ang) * 2 * sc + Math.sin(ang + sg * (1.9 + fl)) * 4 * sc, ang + sg * (1.9 + fl), 8 * sc, 3.2 * sc, pal.light, 0.55 * a, layer);
  disc(ctx, x, y, 3.4 * sc, pal.deep, a, layer); disc(ctx, x, y, 2.4 * sc * pu, pal.mid, a, layer); disc(ctx, x, y, 1.3 * sc, pal.core, a, layer);
}
// picadura: chispa pequeña
function stingFx(ctx, t, layer, t0, x, y, pal, sc = 1) {
  const u = seg(t, t0, t0 + 0.2); if (u >= 1 || layer !== 'front') return;
  star4(ctx, x, y, 9 * sc * (1 - u), t * 9, pal.core, 1 - u, layer); glow(ctx, x, y, 14 * sc, pal.g2, 0.6 * (1 - u));
}
// farol: círculo de luz cálida en el suelo/aire con borde suave y motas que suben
function lanternLight(ctx, t, layer, x, y, r, pal, a) {
  if (a <= 0) return;
  if (layer === 'back') {
    if (!showLayers) { const g = ctx.createRadialGradient(x, y, 0, x, y, r); g.addColorStop(0, rgba(pal.g2, 0.22 * a)); g.addColorStop(0.7, rgba(pal.g1, 0.08 * a)); g.addColorStop(1, rgba(pal.g1, 0)); ctx.save(); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(x, y, r, 0, TAU); ctx.fill(); ctx.restore(); }
    ellArc(ctx, x, y, r, r, 0, TAU, 1.4, pal.light, 0.35 * a, layer, { taper: 0, wob: 0.04, seed: 5, m: 40 });
    return;
  }
  const R = rand(Math.floor(x));
  for (let i = 0; i < 8; i++) { const ph = (t * 0.35 + R()) % 1, ox = (R() - 0.5) * r * 1.4, oy = r * 0.6 - ph * r * 1.3; if (ox * ox + oy * oy > r * r) continue; disc(ctx, x + ox + Math.sin(t * 2 + i) * 4, y + oy, 1.4, pal.light, a * Math.sin(Math.PI * ph) * 0.8, layer); }
}

// ---------------------------------------------------------------- Tótem de cristal (g-totem.js)
// tótem: losa en el suelo + cristal alto facetado con anillo flotante; (x, gy) = base; k = carga del disparo 0..1;
// o = { mode: 'base'|'reson'|'sentry'|'decoy', hp (0..1, señuelo), life (0..1 que queda), aim (ángulo del cañón) }
function crystalTotem(ctx, t, layer, x, gy, sc, pal, a, k = 0, o = {}) {
  if (a <= 0) return;
  const h = 64 * sc, cy = gy - h * 0.55, bob = Math.sin(t * 2.4) * 2;
  if (layer === 'back') {
    ctx.save(); ctx.globalAlpha = clamp(0.55 * a); ctx.fillStyle = col(pal.dark, layer); ctx.beginPath(); ctx.ellipse(x, gy + 2, 22 * sc, 6 * sc, 0, 0, TAU); ctx.fill(); ctx.restore();
    ellArc(ctx, x, cy + bob, 24 * sc, 7 * sc, Math.PI, TAU, 2, pal.light, 0.7 * a, layer, { taper: 0, rot: -0.15, m: 20 });
    if (o.life !== undefined) groundRing(ctx, x, gy + 2, 26 * sc, 2, pal.mid, 0.6 * a, layer, { taper: 0 });
    return;
  }
  glow(ctx, x, cy, 44 * sc * (1 + 0.3 * k), pal.g1, (0.4 + 0.4 * k) * a);
  lens(ctx, x, cy + bob, -Math.PI / 2, h, 22 * sc, pal.deep, a, layer);               // cristal grande
  lens(ctx, x - 2 * sc, cy + bob - 2, -Math.PI / 2, h * 0.86, 13 * sc, pal.mid, a, layer);
  lens(ctx, x - 4 * sc, cy + bob - 4, -Math.PI / 2, h * 0.6, 5 * sc, pal.core, 0.9 * a, layer);
  lens(ctx, x - 13 * sc, gy - h * 0.22, -Math.PI / 2 - 0.35, h * 0.42, 9 * sc, pal.mid, a, layer);   // cristales pequeños de la base
  lens(ctx, x + 13 * sc, gy - h * 0.2, -Math.PI / 2 + 0.4, h * 0.36, 8 * sc, pal.deep, a, layer);
  ellArc(ctx, x, cy + bob, 24 * sc, 7 * sc, 0, Math.PI, 2, pal.light, 0.8 * a, layer, { taper: 0, rot: -0.15, m: 20 });
  if (o.life !== undefined) groundRing(ctx, x, gy + 2, 26 * sc, 2, pal.mid, 0.6 * a, layer, { taper: 0 });
  const top = { x, y: gy - h * 0.95 + bob };
  if (k > 0) { glow(ctx, top.x, top.y, 18 * sc * k, pal.g2, 0.8 * k * a); star4(ctx, top.x, top.y, 10 * sc * k, t * 6, pal.core, k * a, layer); }
  if (o.mode === 'reson') for (let i = 0; i < 3; i++) { const u = (t * 0.8 + i / 3) % 1; ellArc(ctx, x, cy, (26 + u * 50) * sc, (26 + u * 50) * sc, -2.6, -0.5, 1.6, pal.light, (1 - u) * 0.5 * a, layer, { taper: 1, m: 16 }); }
  if (o.mode === 'decoy') { const u = (t * 1.2) % 1; ellArc(ctx, x, cy, (30 + u * 70) * sc, (30 + u * 70) * sc * 0.8, 0, TAU, 2, '#ff9fb0', (1 - u) * 0.6 * a, layer, { taper: 0, m: 30 }); }
  if (o.hp !== undefined && o.hp < 1) { const w = 40 * sc; ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(x - w / 2, gy + 10, w, 4); ctx.fillStyle = pal.light; ctx.fillRect(x - w / 2, gy + 10, w * clamp(o.hp), 4); }
  if (o.life !== undefined) ellArc(ctx, x, gy + 2, 26 * sc, 26 * sc * 0.24, Math.PI / 2, Math.PI / 2 + TAU * clamp(o.life), 2.4, pal.core, 0.8 * a, layer, { taper: 0, m: 30 });
  return top;
}
// onda de resonancia del tótem al enemigo (copia de tu golpe): aro que viaja por la línea
function resonWave(ctx, t, layer, t0, x0, y0, x1, y1, pal) {
  const u = seg(t, t0, t0 + 0.22); if (u >= 1 || layer !== 'front') return;
  const x = lerp(x0, x1, easeOut(u)), y = lerp(y0, y1, easeOut(u)), ang = Math.atan2(y1 - y0, x1 - x0);
  strokeTaper(ctx, [{ x: x0, y: y0 }, { x, y }], 3, pal.light, 0.6 * (1 - u), layer, true);
  ellArc(ctx, x, y, 6, 14, ang - Math.PI / 2, ang + Math.PI / 2, 2.4, pal.core, 1 - u * 0.5, layer, { rot: 0, taper: 0.5, m: 12 });
  if (u > 0.8) star4(ctx, x1, y1, 16, 0.5, pal.core, (1 - u) * 5, layer);
}

// ---------------------------------------------------------------- Espejo (g-espejo.js)
// panel de cristal entre (x0,y0) y (x1,y1): lámina con canto brillante y un destello que la recorre; o.mode = 'mirror'|'facet'|'lens'|'portal'
function mirrorPane(ctx, t, layer, x0, y0, x1, y1, pal, a, o = {}) {
  if (a <= 0) return;
  const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, L = Math.hypot(x1 - x0, y1 - y0), ang = Math.atan2(y1 - y0, x1 - x0), mode = o.mode || 'mirror';
  if (mode === 'portal') {                                    // portal: óvalo que gira (mitad de atrás detrás)
    const half = layer === 'back' ? [Math.PI, TAU] : [0, Math.PI];
    for (const [k, c, w] of [[1.08, pal.deep, 6], [1, pal.mid, 3.4], [0.9, pal.core, 1.6]])
      ellArc(ctx, cx, cy, L / 2 * k, L / 2 * k * 0.42, half[0] + t * 3, half[1] + t * 3, w, c, a, layer, { rot: ang, taper: 0, wob: 0.06, seed: k * 7, m: 24 });
    if (layer === 'front') { glow(ctx, cx, cy, L * 0.6, pal.g1, 0.35 * a); for (let i = 0; i < 4; i++) { const th = t * 5 + i * TAU / 4, r = L * 0.3 * (0.5 + 0.5 * Math.sin(t * 3 + i)); disc(ctx, cx + Math.cos(th + ang) * r, cy + Math.sin(th + ang) * r * 0.42, 1.8, pal.core, a, layer); } }
    if (o.label && layer === 'front') { ctx.font = '700 10px system-ui'; ctx.fillStyle = pal.light; ctx.textAlign = 'center'; ctx.fillText(o.label, cx, cy - L * 0.32 - 8); ctx.textAlign = 'left'; }
    return;
  }
  if (layer === 'back') { needle(ctx, x0, y0, x1, y1, (mode === 'lens' ? 26 : 12), pal.dark, 0.35 * a, layer); return; }
  glow(ctx, cx, cy, L * 0.7, pal.g1, (o.lit ? 0.6 : 0.3) * a);
  if (mode === 'lens') { lens(ctx, cx, cy, ang, L, 18, pal.mid, 0.45 * a, layer); lens(ctx, cx, cy, ang, L * 0.9, 9, pal.light, 0.6 * a, layer); }
  else { needle(ctx, x0, y0, x1, y1, 13, pal.deep, 0.8 * a, layer); needle(ctx, x0, y0, x1, y1, 8, pal.mid, 0.9 * a, layer); needle(ctx, x0, y0, x1, y1, 3.5, pal.light, a, layer); }
  strokeTaper(ctx, [{ x: x0, y: y0 }, { x: x1, y: y1 }], 1.6, pal.core, a, layer, false);
  if (mode === 'facet') for (let i = 1; i < 4; i++) { const s = i / 4, px = lerp(x0, x1, s), py = lerp(y0, y1, s); lens(ctx, px, py, ang + Math.PI / 2, 10, 2, pal.core, 0.8 * a, layer); }
  const g = (t * 0.7) % 1, gx = lerp(x0, x1, g), gy = lerp(y0, y1, g);             // destello que recorre el canto
  star4(ctx, gx, gy, 8 * Math.sin(Math.PI * g), ang, pal.core, a, layer);
  for (const [px, py] of [[x0, y0], [x1, y1]]) disc(ctx, px, py, 3, pal.core, a, layer);
}
// rayito de luz del espejo a un enemigo (destello propio / eco)
function glintRay(ctx, t, layer, t0, x0, y0, x1, y1, pal) {
  const u = seg(t, t0, t0 + 0.2); if (u >= 1 || layer !== 'front') return;
  strokeTaper(ctx, [{ x: x0, y: y0 }, { x: x1, y: y1 }], 4 * (1 - u), pal.light, 1 - u, layer, false);
  strokeTaper(ctx, [{ x: x0, y: y0 }, { x: x1, y: y1 }], 1.6 * (1 - u), pal.core, 1 - u, layer, false);
  star4(ctx, x1, y1, 14 * (1 - u), 0.7, pal.core, 1 - u, layer);
}

// ---------------------------------------------------------------- Acumulador (g-acumulador.js)
// carga alrededor del player: chispas que orbitan, más y más rápidas con la carga (k = 0..1, cells = celdas llenas)
function voltAura(ctx, t, layer, x, y, k, pal, cells = 0) {
  if (k <= 0.02 && !cells) return;
  const n = 2 + Math.floor(k * 8), R = rand(Math.floor(t * 12));
  for (let i = 0; i < n; i++) {
    const a = t * (3 + k * 6) + i * TAU / n, r = 28 + 6 * Math.sin(t * 5 + i), front = Math.sin(a) > 0;
    if (front !== (layer === 'front')) continue;
    const px = x + Math.cos(a) * r, py = y + Math.sin(a) * r * 0.45;
    disc(ctx, px, py, 1.6 + k * 1.4, pal.core, 0.9, layer);
    if (R() < 0.35 + k * 0.4) bolt(ctx, px, py, px + (R() - 0.5) * 20, py + (R() - 0.5) * 20, 3, R, 0.9, pal, 0.8, layer, 0.6);
  }
  if (layer === 'front' && k >= 0.99) glow(ctx, x, y, 46, pal.g1, 0.35 + 0.2 * Math.sin(t * 20));
  for (let c = 0; c < cells; c++) if (layer === 'front') { const a = t * 1.5 + c * TAU / 3; disc(ctx, x + Math.cos(a) * 40, y - 30 + Math.sin(a) * 8, 4, pal.mid, 1, layer); disc(ctx, x + Math.cos(a) * 40, y - 30 + Math.sin(a) * 8, 2, pal.core, 1, layer); }
}
// rayo del cielo (Acumulador · Celdas): zigzag grueso de arriba al suelo + destello y grietas
function skyBolt(ctx, t, layer, t0, x, pal, sc = 1) {
  const u = seg(t, t0, t0 + 0.35); if (u >= 1) return;
  if (layer === 'front') { const R = rand(Math.floor(t * 20) + Math.floor(x)); bolt(ctx, x + (R() - 0.5) * 30, -20, x, GROUND, 12, R, 4.2 * sc * (1 - u * 0.7), pal, 1 - u, layer, 0.18); bolt(ctx, x, GROUND - 120, x + (R() - 0.5) * 80, GROUND - 40, 5, R, 1.6 * sc, pal, (1 - u) * 0.8, layer, 0.4); glow(ctx, x, GROUND - 30, 120 * sc, pal.g1, 0.7 * (1 - u)); }
  groundImpact(ctx, t, layer, t0, x, 1.1 * sc, pal, { seed: Math.floor(x), debris: true });
}

// ---------------------------------------------------------------- Pacto de sangre (g-pacto.js)
// orbe de sangre: esfera oscura con brillo, gotas que se quedan atrás
function bloodOrb(ctx, t, layer, x, y, ang, sc, pal, a, trail) {
  if (a <= 0) return;
  if (layer === 'back') { if (trail && trail.length > 1) { ribbon(ctx, trail, s => 10 * sc * (1 - s), pal.dark, 0.6 * a, layer); ribbon(ctx, trail, s => 5 * sc * (1 - s), pal.deep, 0.8 * a, layer); } return; }
  glow(ctx, x, y, 30 * sc, pal.g1, 0.55 * a);
  drop(ctx, x, y, ang, 26 * sc, 9 * sc, pal.deep, a, layer);
  disc(ctx, x, y, 8 * sc, pal.mid, a, layer); disc(ctx, x - 2 * sc, y - 2 * sc, 3.4 * sc, pal.light, a, layer); disc(ctx, x - 2.6 * sc, y - 2.6 * sc, 1.4 * sc, pal.core, a, layer);
}
// sangrado: gotas que caen del enemigo, más con más cargas
function bleedDrops(ctx, t, layer, e, n, pal) {
  if (layer !== 'front' || n <= 0) return;
  const R = rand(Math.floor(e.sp.x));
  for (let i = 0; i < n * 2; i++) { const ph = (t * (0.9 + R() * 0.5) + R()) % 1, ox = (R() - 0.5) * e.rx * 1.4; drop(ctx, e.x + ox, e.y - e.ry * 0.3 + ph * e.ry * 1.4, Math.PI / 2, 6 + R() * 4, 2.2, i % 2 ? pal.mid : pal.deep, 1 - ph, layer); }
  ctx.font = '700 10px system-ui'; ctx.fillStyle = pal.light; ctx.textAlign = 'center'; ctx.fillText('🩸' + n, e.x - 16, e.y - e.ry - 10); ctx.textAlign = 'left';
}
// nova de sangre: aro rojo con púas hacia fuera
function bloodNova(ctx, t, layer, t0, x, y, r, pal) {
  const u = seg(t, t0, t0 + 0.4); if (u >= 1) return;
  shockRing(ctx, t, layer, t0, x, y, r, pal);
  const R = rand(Math.floor(x)), rr = r * easeOut(u);
  for (let i = 0; i < 14; i++) { const a = i / 14 * TAU + R() * 0.2, lay = Math.sin(a) < 0 ? 'back' : 'front'; if (lay !== layer) continue; const l = 18 + R() * 20; shard(ctx, x + Math.cos(a) * (rr + l), y + Math.sin(a) * (rr + l) * 0.8, x + Math.cos(a) * rr, y + Math.sin(a) * rr * 0.8, 7, i % 2 ? pal.mid : pal.deep, 1 - u, layer); }
}

// ---------------------------------------------------------------- Mazo (g-mazo.js)
// mazo de cristal: mango desde (x,y) en ángulo ang, cabeza grande al final. sc = tamaño; glowK = carga 0..1
function crystalHammer(ctx, t, layer, x, y, ang, sc, pal, a, glowK = 0) {
  if (a <= 0 || layer !== 'front') return;
  const L = 58 * sc, c = Math.cos(ang), s = Math.sin(ang), hx = x + c * L, hy = y + s * L, px = -s, py = c;
  needle(ctx, x, y, hx, hy, 6 * sc, pal.dark, a, layer); needle(ctx, x, y, hx, hy, 3 * sc, pal.light, a, layer);
  if (glowK > 0) glow(ctx, hx, hy, 40 * sc * (0.6 + glowK), pal.g1, 0.6 * glowK * a);
  const hw = 20 * sc, hh = 13 * sc;                            // cabeza: prisma con caras
  ctx.save(); ctx.globalAlpha = clamp(a); ctx.translate(hx, hy); ctx.rotate(ang + Math.PI / 2);
  ctx.fillStyle = col(pal.deep, layer); ctx.beginPath(); ctx.moveTo(-hw, -hh * 0.6); ctx.lineTo(-hw * 0.7, -hh); ctx.lineTo(hw * 0.7, -hh); ctx.lineTo(hw, -hh * 0.6); ctx.lineTo(hw, hh * 0.6); ctx.lineTo(hw * 0.7, hh); ctx.lineTo(-hw * 0.7, hh); ctx.lineTo(-hw, hh * 0.6); ctx.closePath(); ctx.fill();
  ctx.fillStyle = col(pal.mid, layer); ctx.beginPath(); ctx.moveTo(-hw * 0.8, -hh * 0.5); ctx.lineTo(hw * 0.8, -hh * 0.5); ctx.lineTo(hw * 0.6, hh * 0.2); ctx.lineTo(-hw * 0.6, hh * 0.2); ctx.closePath(); ctx.fill();
  ctx.fillStyle = col(pal.core, layer); ctx.globalAlpha = clamp(a * (0.6 + 0.4 * glowK)); ctx.fillRect(-hw * 0.6, -hh * 0.42, hw * 1.2, hh * 0.18);
  ctx.restore();
  if (glowK >= 1) star4(ctx, hx, hy, 16 * sc + 3 * Math.sin(t * 30), t * 3, pal.core, a, layer);
}
// onda que corre por el suelo (Terremoto): lomo de tierra/cristal que avanza con trozos que saltan
function groundWave(ctx, t, layer, x, dir, h, pal, a, seed = 0) {
  if (a <= 0) return;
  const R = rand(seed + Math.floor(t * 20));
  for (let i = 0; i < 5; i++) { const ox = -dir * i * 12, hh = h * (1 - i * 0.18); const lay = i % 2 ? 'back' : 'front'; if (lay !== layer) continue; shard(ctx, x + ox, GROUND - hh, x + ox - dir * 4, GROUND + 2, 16 - i * 2, i ? pal.mid : pal.light, a * (1 - i * 0.15), layer); }
  if (layer === 'front') { glow(ctx, x, GROUND - h * 0.5, h, pal.g1, 0.4 * a); for (let i = 0; i < 3; i++) chunk(ctx, x - dir * R() * 30, GROUND - R() * h * 1.2, 3 + R() * 3, R() * 6, i, pal.mid, pal.core, a, layer); }
}

// ---------------------------------------------------------------- Égida (g-egida.js)
// escudo de cristal curvo delante del player: centro (x,y), mirando a ang, radio r; k = brillo (parada, carga)
function crystalShield(ctx, t, layer, x, y, ang, r, pal, a, k = 0) {
  if (a <= 0) return;
  const a0 = ang - 0.95, a1 = ang + 0.95;
  if (layer === 'back') { ellArc(ctx, x, y, r + 4, r + 4, a0, a1, 12, pal.dark, 0.35 * a, layer, { taper: 0.4, m: 20 }); return; }
  glow(ctx, x + Math.cos(ang) * r, y + Math.sin(ang) * r, 34 + 30 * k, pal.g1, (0.35 + 0.5 * k) * a);
  ellArc(ctx, x, y, r, r, a0, a1, 10, pal.deep, 0.85 * a, layer, { taper: 0.5, m: 20 });
  ellArc(ctx, x, y, r, r, a0 + 0.08, a1 - 0.08, 6, pal.mid, a, layer, { taper: 0.5, m: 20 });
  ellArc(ctx, x, y, r + 2, r + 2, a0 + 0.2, a1 - 0.2, 2, pal.core, a, layer, { taper: 0.6, m: 20 });
  for (let i = -1; i <= 1; i++) { const th = ang + i * 0.45; lens(ctx, x + Math.cos(th) * r, y + Math.sin(th) * r, th + Math.PI / 2, 14, 3, pal.core, 0.8 * a, layer); }
  if (k > 0.5) star4(ctx, x + Math.cos(ang) * (r + 4), y + Math.sin(ang) * (r + 4), 18 * k, t * 6, pal.core, a, layer);
}
// muro de cristal plantado (Bastión): losa vertical de altura h sobre el suelo
function crystalWall(ctx, t, layer, x, h, pal, a, hurt = 0) {
  if (a <= 0) return;
  const top = GROUND - h;
  if (layer === 'back') { ctx.save(); ctx.globalAlpha = clamp(0.5 * a); ctx.fillStyle = col(pal.dark, layer); ctx.fillRect(x - 12, top, 24, h); ctx.restore(); return; }
  ctx.save(); ctx.globalAlpha = clamp(0.75 * a); ctx.fillStyle = col(pal.deep, layer); ctx.beginPath(); ctx.moveTo(x - 11, GROUND); ctx.lineTo(x - 9, top + 10); ctx.lineTo(x, top); ctx.lineTo(x + 9, top + 10); ctx.lineTo(x + 11, GROUND); ctx.closePath(); ctx.fill();
  ctx.globalAlpha = clamp(0.85 * a); ctx.fillStyle = col(pal.mid, layer); ctx.fillRect(x - 5, top + 12, 6, h - 14); ctx.restore();
  strokeTaper(ctx, [{ x: x + 3, y: GROUND - 4 }, { x: x + 3, y: top + 8 }], 2, pal.core, a, layer, false);
  if (hurt > 0) glow(ctx, x, top + h / 2, 40, pal.g1, 0.5 * hurt * a);
  const g = (t * 0.6) % 1; star4(ctx, x, GROUND - g * h, 8 * Math.sin(Math.PI * g), 0, pal.core, 0.8 * a, layer);
}

// ---------------------------------------------------------------- Ballesta de cristal (g-ballesta.js)
// mira láser: tramos punteados con puntos de rebote; k = concentración 0..1 (más fina y brillante)
function laserSight(ctx, t, layer, pts, k, pal) {
  if (layer !== 'front' || pts.length < 2) return;
  for (let i = 0; i + 1 < pts.length; i++) {
    const a = pts[i], b = pts[i + 1], L = Math.hypot(b.x - a.x, b.y - a.y), n = Math.max(2, Math.floor(L / 10));
    for (let j = 0; j < n; j += 2) { const s0 = j / n, s1 = Math.min(1, (j + 1) / n); strokeTaper(ctx, [{ x: lerp(a.x, b.x, s0), y: lerp(a.y, b.y, s0) }, { x: lerp(a.x, b.x, s1), y: lerp(a.y, b.y, s1) }], 1.8 + 1.6 * k, k >= 1 ? pal.core : pal.light, (0.55 + 0.45 * k) * (1 - i * 0.15), layer, false); }
    if (i > 0) { disc(ctx, a.x, a.y, 3, pal.core, 0.8, layer); }
  }
  const e = pts[pts.length - 1]; ellArc(ctx, e.x, e.y, 10 - 5 * k, 10 - 5 * k, 0, TAU, 1.4, pal.core, 0.8, layer, { taper: 0, m: 16 }); if (k >= 1) star4(ctx, e.x, e.y, 9, t * 4, pal.core, 1, layer);
}
// virote de cristal: aguja larga y fina con estela recta
function crystalBolt(ctx, t, layer, x, y, ang, sc, pal, a, trail) {
  if (a <= 0) return;
  if (layer === 'back') { if (trail && trail.length > 1) { ribbon(ctx, trail, s => 5 * sc * (1 - s), pal.mid, 0.6 * a, layer); ribbon(ctx, trail, s => 2 * sc * (1 - s), pal.core, a, layer); } return; }
  glow(ctx, x, y, 18 * sc, pal.g1, 0.5 * a);
  needle(ctx, x - Math.cos(ang) * 34 * sc, y - Math.sin(ang) * 34 * sc, x, y, 5 * sc, pal.mid, a, layer);
  needle(ctx, x - Math.cos(ang) * 26 * sc, y - Math.sin(ang) * 26 * sc, x, y, 2.2 * sc, pal.core, a, layer);
}
// punto débil (Marcador): diana que gira sobre el enemigo con los golpes que le quedan
function weakSpot(ctx, t, layer, e, n, pal) {
  if (layer !== 'front' || n <= 0) return;
  const x = e.x, y = e.y - e.ry * 0.45;
  ellArc(ctx, x, y, 11, 11, t * 3, t * 3 + TAU * 0.8, 1.8, pal.core, 0.9, layer, { taper: 0, m: 20 });
  for (let i = 0; i < 4; i++) { const a = t * 3 + i * Math.PI / 2; needle(ctx, x + Math.cos(a) * 8, y + Math.sin(a) * 8, x + Math.cos(a) * 16, y + Math.sin(a) * 16, 2, pal.light, 0.9, layer); }
  ctx.font = '700 10px system-ui'; ctx.fillStyle = pal.core; ctx.textAlign = 'center'; ctx.fillText('×' + n, x + 18, y - 10); ctx.textAlign = 'left';
}
