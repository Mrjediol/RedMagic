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
