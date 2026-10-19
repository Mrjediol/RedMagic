// fx-sing.js — aspectos refinados de la Singularidad (ronda de variantes, 2026-10-19). Compartido por los clips (a2b) y la jugable.
// El usuario: la primera versión eran «anillas con huecos incluso entre ellas». Aquí todo son superficies continuas (anillos rellenos
// con degradado, cintas en espiral, rejilla curvada, refracción) en vez de trazos sueltos.
// Firma común: SING_LOOKS[id](ctx, t, layer, x, y, o)  con o = { s, form 0..1, collapse 0..1, pullT (s o -1), liveT, pal }
// y SING_BURSTS[id](ctx, t, layer, t0, x, y, s, pal, o)  (o = { gy })
'use strict';

const SING_LOOK_IDS = ['rings', 'horizon', 'whirl', 'rift', 'grid', 'lens'];
const SING_LOOK_NAMES = { rings: 'Anillas (primera versión)', horizon: 'Horizonte de sucesos', whirl: 'Remolino', rift: 'Grieta', grid: 'Abismo (rejilla)', lens: 'Lente' };

// anillo elíptico relleno con degradado radial (r0 → r1); half = 'back' (mitad de arriba) | 'front' | null; stops = [[0..1, color]]
function fillAnnulus(ctx, x, y, r0, r1, sq, tilt, stops, half, a = 1, comp) {
  if (a <= 0 || r1 <= r0 || r1 <= 0) return;
  ctx.save(); ctx.globalAlpha = clamp(a); if (comp && !showLayers) ctx.globalCompositeOperation = comp;
  ctx.translate(x, y); ctx.rotate(tilt); ctx.scale(1, Math.max(0.01, sq));
  if (half) { ctx.beginPath(); ctx.rect(-r1 - 2, half === 'back' ? -r1 - 2 : 0, r1 * 2 + 4, r1 + 2); ctx.clip(); }
  if (showLayers) ctx.fillStyle = col('#fff', half || 'front');
  else { const g = ctx.createRadialGradient(0, 0, Math.max(0.01, r0), 0, 0, r1); for (const [p, c] of stops) g.addColorStop(p, c); ctx.fillStyle = g; }
  ctx.beginPath(); ctx.arc(0, 0, r1, 0, TAU); ctx.arc(0, 0, Math.max(0.01, r0), 0, TAU, true); ctx.fill();
  ctx.restore();
}
// trazos finos y largos que giran dentro de un anillo (textura de seda: da movimiento sin huecos)
function silk(ctx, t, x, y, r0, r1, sq, tilt, n, seed, half, color, a, speed = 1) {
  if (a <= 0) return;
  ctx.save(); ctx.translate(x, y); ctx.rotate(tilt); ctx.scale(1, Math.max(0.01, sq));
  if (half) { ctx.beginPath(); ctx.rect(-r1 - 2, half === 'back' ? -r1 - 2 : 0, r1 * 2 + 4, r1 + 2); ctx.clip(); }
  const R = rand(seed); ctx.lineCap = 'round';
  for (let i = 0; i < n; i++) {
    const r = lerp(r0, r1, Math.pow(R(), 0.8)), w = 2.2 * Math.pow(1 - (r - r0) / (r1 - r0 + 0.01), 1.4) + 0.5, om = speed * 9 / Math.pow(r / 20, 1.3);
    const a0 = R() * TAU + t * om, len = 0.5 + R() * 1.3;
    ctx.globalAlpha = clamp(a * (0.25 + R() * 0.45)); ctx.strokeStyle = col(i % 3 ? color : '#ffffff', half || 'front'); ctx.lineWidth = w;
    ctx.beginPath(); ctx.arc(0, 0, r, a0, a0 + len); ctx.stroke();
  }
  ctx.restore();
}
// filamentos que caen en espiral desde fuera (aplastados como el disco)
function inflow(ctx, t, x, y, s, sq, tilt, n, seed, layer, color, a) {
  if (a <= 0) return;
  const R = rand(seed), cr = Math.cos(tilt), sr = Math.sin(tilt);
  for (let i = 0; i < n; i++) {
    const th0 = R() * TAU, rate = 0.7 + R() * 0.5, ph = R(), age = (t * rate + ph) % 1, pts = [];
    for (let k = 0; k <= 10; k++) {
      const u = clamp(age - k * 0.035), r = 150 * s * Math.pow(1 - u, 1.25) + 12 * s, th = th0 + u * 3.2;
      if ((Math.sin(th) < 0) !== (layer === 'back') && k === 0) break;
      const ex = Math.cos(th) * r, ey = Math.sin(th) * r * sq; pts.push({ x: x + ex * cr - ey * sr, y: y + ex * sr + ey * cr });
    }
    if (pts.length > 2) strokeTaper(ctx, pts, 2.6 * s, color, a * Math.sin(Math.PI * age), layer);
  }
}
function coreShadow(ctx, x, y, r, a, layer) {        // sombra negra con borde de fotones y penumbra
  if (a <= 0 || r <= 0) return;
  if (!showLayers) { const g = ctx.createRadialGradient(x, y, r * 0.9, x, y, r * 1.7); g.addColorStop(0, 'rgba(4,2,14,0.85)'); g.addColorStop(1, 'rgba(4,2,14,0)'); ctx.save(); ctx.globalAlpha = a; ctx.fillStyle = g; ctx.beginPath(); ctx.arc(x, y, r * 1.7, 0, TAU); ctx.fill(); ctx.restore(); }
  disc(ctx, x, y, r, '#05020e', a, layer);
  ctx.save(); ctx.globalAlpha = clamp(0.95 * a); ctx.strokeStyle = col('#ffffff', layer); ctx.lineWidth = 1.3; ctx.beginPath(); ctx.arc(x, y, r + 0.6, 0, TAU); ctx.stroke(); ctx.restore();
}

// ---------------------------------------------------------------- 1 · HORIZONTE DE SUCESOS
function lookHorizon(ctx, t, layer, x, y, o) {
  const s = o.s * (0.4 + 0.6 * o.form) * (1 - easeIn(o.collapse || 0)), pal = o.pal, tilt = -0.14, sq = 0.3;
  if (s <= 0.02) return;
  const pull = o.pullT >= 0 ? 1 : 0.55;
  if (layer === 'back') { glow(ctx, x, y, 120 * s, pal.g1, 0.4 * pull); glow(ctx, x, y, 45 * s, [200, 230, 255], 0.35 * pull); }
  const disk = h => {                                      // disco: blanco caliente dentro → violeta → nada
    fillAnnulus(ctx, x, y, 14 * s, 86 * s, sq, tilt, [[0, 'rgba(255,255,255,1)'], [0.14, 'rgba(210,245,255,0.95)'], [0.38, 'rgba(160,120,255,0.8)'], [0.7, 'rgba(100,45,215,0.4)'], [1, 'rgba(60,20,160,0)']], h, 1);
    fillAnnulus(ctx, x, y, 14 * s, 86 * s, sq, tilt, [[0, 'rgba(255,255,255,0.7)'], [0.4, 'rgba(255,255,255,0.2)'], [1, 'rgba(255,255,255,0)']], h, 0.7, 'lighter');
    silk(ctx, t, x, y, 15 * s, 80 * s, sq, tilt, 30, 7, h, pal.light, 0.95, pull);
    // efecto Doppler: el lado que se acerca (izquierda) más brillante
    if (!showLayers) { ctx.save(); ctx.translate(x, y); ctx.rotate(tilt); ctx.scale(1, sq); ctx.beginPath(); ctx.rect(-88 * s, h === 'back' ? -88 * s : 0, 176 * s, 88 * s); ctx.clip();
      const g = ctx.createLinearGradient(-86 * s, 0, 86 * s, 0); g.addColorStop(0, 'rgba(255,255,255,0.4)'); g.addColorStop(0.5, 'rgba(255,255,255,0)'); g.addColorStop(1, 'rgba(40,10,90,0.3)');
      ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, 0, 86 * s, 0, TAU); ctx.arc(0, 0, 14 * s, 0, TAU, true); ctx.fill(); ctx.restore(); }
  };
  if (layer === 'back') { disk('back'); inflow(ctx, t, x, y, s, sq, tilt, 7, 3, layer, pal.light, 0.55 * (o.pullT >= 0 ? 1 : 0)); }
  if (layer === 'front') {
    // anillo de luz: la parte de atrás del disco curvada sobre la sombra (más brillante arriba)
    fillAnnulus(ctx, x, y, 13 * s, 30 * s, 1.05, 0, [[0, 'rgba(255,255,255,1)'], [0.18, 'rgba(215,245,255,0.9)'], [0.5, 'rgba(160,120,255,0.45)'], [1, 'rgba(120,60,255,0)']], 'back', 1);
    fillAnnulus(ctx, x, y, 13 * s, 30 * s, 1.05, 0, [[0, 'rgba(255,255,255,0.6)'], [1, 'rgba(255,255,255,0)']], 'back', 0.6, 'lighter');
    fillAnnulus(ctx, x, y, 13 * s, 20 * s, 1.05, 0, [[0, 'rgba(255,255,255,0.75)'], [1, 'rgba(160,120,255,0)']], 'front', 0.8);
    coreShadow(ctx, x, y, 13 * s, 1, layer);
    disk('front');
    inflow(ctx, t, x, y, s, sq, tilt, 7, 3, layer, pal.light, 0.55 * (o.pullT >= 0 ? 1 : 0));
  }
}

// ---------------------------------------------------------------- 2 · REMOLINO
function lookWhirl(ctx, t, layer, x, y, o) {
  const s = o.s * (0.4 + 0.6 * o.form) * (1 - easeIn(o.collapse || 0)), tilt = -0.1, sq = 0.5, rot = t * (o.pullT >= 0 ? 3.2 : 2) + (o.collapse || 0) * 6;
  if (s <= 0.02) return;
  if (layer === 'back') { glow(ctx, x, y, 100 * s, [70, 20, 160], 0.5); fillAnnulus(ctx, x, y, 0, 70 * s, sq, tilt, [[0, 'rgba(20,6,50,0.75)'], [0.6, 'rgba(40,12,100,0.35)'], [1, 'rgba(40,12,100,0)']], 'back', 1); }
  if (layer === 'front') fillAnnulus(ctx, x, y, 0, 70 * s, sq, tilt, [[0, 'rgba(20,6,50,0.75)'], [0.6, 'rgba(40,12,100,0.35)'], [1, 'rgba(40,12,100,0)']], 'front', 1);
  const cr = Math.cos(tilt), sr = Math.sin(tilt), arms = 4;
  for (let k = 0; k < arms; k++) {
    const pts = [], lay = [];
    for (let i = 0; i <= 28; i++) {
      const u = i / 28, th = rot + k * TAU / arms - u * 3.4, r = (9 + 112 * Math.pow(u, 1.1)) * s;
      const ex = Math.cos(th) * r, ey = Math.sin(th) * r * sq; pts.push({ x: x + ex * cr - ey * sr, y: y + ex * sr + ey * cr }); lay.push(Math.sin(th) < 0 ? 'back' : 'front');
    }
    // trozos continuos de la cinta en esta capa (para que el brazo pase por detrás y por delante del ojo)
    let segS = null;
    const flush = (a, b) => {
      const sub = pts.slice(a, b + 1); if (sub.length < 2) return;
      const w0 = a / 28, w1 = b / 28, wf = q => { const u = lerp(w0, w1, q); return Math.sin(Math.PI * Math.pow(u, 0.7)) * 18 * s * (1 - u * 0.3); };
      ribbon(ctx, sub, wf, '#3a1c9c', 0.55, layer); ribbon(ctx, sub, q => wf(q) * 0.62, '#8a5cff', 0.8, layer); ribbon(ctx, sub, q => wf(q) * 0.28, '#e9dcff', 0.9, layer);
    };
    for (let i = 0; i <= 28; i++) { if (lay[i] === layer) { if (segS === null) segS = i; } else if (segS !== null) { flush(segS, i); segS = null; } }
    if (segS !== null) flush(segS, 28);
  }
  // gotas arrastradas por los brazos
  const R = rand(41);
  for (let i = 0; i < 16; i++) {
    const k = i % arms, ph = R(), age = (t * (0.8 + R() * 0.5) + ph) % 1, u = 1 - age, th = rot + k * TAU / arms - u * 3.4 + 0.15, r = (9 + 112 * u) * s;
    if ((Math.sin(th) < 0) !== (layer === 'back')) continue;
    const ex = Math.cos(th) * r, ey = Math.sin(th) * r * sq; disc(ctx, x + ex * cr - ey * sr, y + ex * sr + ey * cr, (1.2 + 1.8 * u) * s, i % 2 ? '#ffffff' : '#cdbcff', Math.sin(Math.PI * age), layer);
  }
  if (layer === 'front') { glow(ctx, x, y, 22 * s, [180, 140, 255], 0.6); coreShadow(ctx, x, y, 10 * s, 1, layer); }
}

// ---------------------------------------------------------------- 3 · GRIETA
// orbe que viaja hasta el sitio y abre allí la grieta (el usuario: ver la grieta volando «queda raro», 2026-10-19)
function riftSeed(ctx, t, layer, x, y, s, trail) {
  if (layer === 'back') {
    if (trail && trail.length > 1) { strokeTaper(ctx, [{ x, y }, ...trail], 7 * s, '#7538db', 0.45, layer); strokeTaper(ctx, [{ x, y }, ...trail], 2.6 * s, '#cdbcff', 0.9, layer); }
    glow(ctx, x, y, 34 * s, [110, 50, 240], 0.6);
    return;
  }
  glow(ctx, x, y, 16 * s, [210, 230, 255], 0.8);
  disc(ctx, x, y, 6.5 * s, '#5a2fd0', 1, layer); disc(ctx, x, y, 4.5 * s, '#c9b6ff', 1, layer); disc(ctx, x, y, 2.4 * s, '#ffffff', 1, layer);
  lens(ctx, x, y, Math.PI / 2, 22 * s * (1 + 0.25 * Math.sin(t * 30)), 2.2 * s, '#ffffff', 0.85, layer);    // anuncia la grieta
  const B = rand(Math.floor(t * 20) + 3);
  for (let i = 0; i < 2; i++) { const a = B() * TAU; bolt(ctx, x, y, x + Math.cos(a) * 15 * s, y + Math.sin(a) * 15 * s, 3, B, 0.9, PAL.set, 0.75, layer); }
}
// o.phase = 'travel' → orbe; si no, la grieta, que se abre en el sitio: línea de luz que crece y luego se rasga (o.openT = s desde que llegó)
function lookRift(ctx, t, layer, x, y, o) {
  const s = o.s;
  if (o.phase === 'travel') return riftSeed(ctx, t, layer, x, y, s, o.trail);
  const ot = o.openT === undefined ? 1 : o.openT, lineU = easeOut(seg(ot, 0, 0.14)), openU = easeOut(seg(ot, 0.1, 0.45)), cU = easeIn(o.collapse || 0);
  const op = (0.08 + 0.92 * openU) * (1 - cU), H = 84 * s * (0.2 + 0.8 * lineU) * (1 - 0.6 * cU), Wd = 15 * s * openU * (1 - cU) * (1 + 0.08 * Math.sin(t * 9));
  if (op <= 0.01 && lineU >= 1) return;
  // apertura: línea vertical de luz y destello al rasgarse
  if (layer === 'front' && ot < 0.5) {
    const lf = 1 - seg(ot, 0.3, 0.5);
    glow(ctx, x, y, 50 * s * lineU, [200, 220, 255], 0.7 * lf);
    lens(ctx, x, y, Math.PI / 2, H * 1.15, 4 * s * (1 - openU * 0.5), '#ffffff', lf, layer);
    const fu = seg(ot, 0.1, 0.3); if (fu > 0 && fu < 1) { star4(ctx, x, y, 46 * s * (1 - fu * 0.4), 0, '#ffffff', 1 - fu, layer, 0.12); lens(ctx, x, y, 0, 120 * s * (1 - fu * 0.3), 4 * s * (1 - fu), '#9ef2ff', 1 - fu, layer); }
  }
  if (ot >= 0.1) sparks(ctx, t, t - ot + 0.1, x, y, layer, { n: 14, a0: -Math.PI, a1: Math.PI, sp: 260 * s, pal: PAL.set, seed: 31, len: 14, life: 0.45, w: 2 });
  if (op <= 0.01) return;
  if (layer === 'back') { glow(ctx, x, y, 95 * s, [90, 40, 220], 0.45 * op); return; }
  const seed = Math.floor(t * 16), N = 18, L = [], Rr = [];
  const R = rand(seed);
  for (let i = 0; i <= N; i++) {
    const v = i / N, w = Wd * Math.pow(Math.sin(Math.PI * v), 0.85), j = i === 0 || i === N ? 0 : (R() - 0.5) * 3.2 * s * op, yy = y - H / 2 + H * v;
    L.push({ x: x - w + j, y: yy }); Rr.push({ x: x + w + (R() - 0.5) * 3.2 * s * op, y: yy });
  }
  const path = () => { ctx.beginPath(); L.forEach((p, i) => i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y)); for (let i = N; i >= 0; i--) ctx.lineTo(Rr[i].x, Rr[i].y); ctx.closePath(); };
  // dentro: vacío con estrellas y una nebulosa
  ctx.save(); path(); ctx.fillStyle = col('#04010c', layer); ctx.fill(); ctx.clip();
  if (!showLayers) { const g = ctx.createRadialGradient(x, y, 0, x, y, H / 2); g.addColorStop(0, 'rgba(120,60,255,0.45)'); g.addColorStop(1, 'rgba(20,5,60,0)'); ctx.fillStyle = g; ctx.fillRect(x - Wd - 4, y - H / 2, Wd * 2 + 8, H); }
  const S = rand(9);
  for (let i = 0; i < 26; i++) { const sx = x + (S() - 0.5) * Wd * 1.8, sy = y + (S() - 0.5) * H, tw = 0.5 + 0.5 * Math.sin(t * (3 + S() * 5) + i); disc(ctx, sx, sy, 0.5 + S() * 1.1, i % 4 ? '#ffffff' : '#9ef2ff', tw, layer); }
  ctx.restore();
  // bordes que arden (tres pasadas)
  for (const side of [L, Rr]) for (const [w, c, a] of [[7 * s, '#7538db', 0.45], [3 * s, '#29c2ff', 0.9], [1.3, '#ffffff', 1]]) {
    ctx.save(); ctx.globalAlpha = a * op; ctx.strokeStyle = col(c, layer); ctx.lineWidth = w; ctx.lineJoin = 'round'; ctx.lineCap = 'round'; ctx.beginPath(); side.forEach((p, i) => i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y)); ctx.stroke(); ctx.restore();
  }
  // chispas eléctricas que saltan del borde
  const B = rand(seed * 3 + 1);
  for (let i = 0; i < 3; i++) { const side = B() < 0.5 ? L : Rr, p = side[1 + Math.floor(B() * (N - 1))], d = side === L ? -1 : 1; bolt(ctx, p.x, p.y, p.x + d * (14 + B() * 22) * s, p.y + (B() - 0.5) * 20 * s, 4, B, 1.4, PAL.set, 0.8 * op, layer); }
  // escombros que orbitan y se cuelan por la grieta
  const D = rand(77);
  for (let i = 0; i < 12; i++) {
    const ph = D(), rate = 0.35 + D() * 0.3, age = (t * rate + ph) % 1, r = (130 * Math.pow(1 - age, 1.3) + 4) * s, th = D() * TAU + age * 5;
    const px = x + Math.cos(th) * r, py = y + Math.sin(th) * r * 0.6, sz = (2.5 + D() * 3.5) * s * (0.4 + 0.6 * (1 - age));
    chunk(ctx, px, py, sz, t * 4 + i, i, '#2a2f45', '#8a93b8', clamp((1 - age) * 3) * op, layer);
  }
  ellArc(ctx, x, y, 22 * s, H / 2 + 6 * s, 0, TAU, 1.2, '#cdbcff', 0.25 * op, layer, { taper: 0, m: 40 });
}

// ---------------------------------------------------------------- 4 · ABISMO (rejilla del espacio que se curva)
function lookGrid(ctx, t, layer, x, y, o) {
  const s = o.s * (0.35 + 0.65 * o.form), c = o.collapse || 0, Rg = 150 * s, str = (o.pullT >= 0 ? 1 : 0.6) * (1 + 1.5 * c) * s;
  if (layer === 'back') {
    const step = 17 * s, warp = (px, py) => {
      const dx = px - x, dy = py - y, d = Math.hypot(dx, dy) || 0.001, f = Math.min(d * 0.92, 900 * str / (d / s + 22));
      return { x: px - dx / d * f, y: py - dy / d * f, a: Math.pow(clamp(1 - d / Rg), 1.1) };
    };
    for (const dir of [0, 1]) for (let k = -9; k <= 9; k++) {
      const pts = [];
      for (let i = 0; i <= 30; i++) { const v = -Rg + 2 * Rg * i / 30, px = dir ? x + k * step : x + v, py = dir ? y + v : y + k * step; pts.push(warp(px, py)); }
      ctx.save(); ctx.lineWidth = 1.2; ctx.strokeStyle = col('#b9a4ff', layer);
      for (let i = 1; i < pts.length; i++) { const a = Math.min(pts[i].a, pts[i - 1].a); if (a <= 0.01) continue; ctx.globalAlpha = 0.5 * a * (1 - c * 0.5); ctx.beginPath(); ctx.moveTo(pts[i - 1].x, pts[i - 1].y); ctx.lineTo(pts[i].x, pts[i].y); ctx.stroke(); }
      ctx.restore();
    }
    glow(ctx, x, y, 70 * s, [120, 60, 255], 0.5);
  }
  if (layer === 'front') {
    inflow(ctx, t, x, y, s, 1, 0, 8, 5, layer, '#cdbcff', 0.5 * (o.pullT >= 0 ? 1 : 0));
    const r = 13 * s * (1 - easeIn(c) * 0.7);
    fillAnnulus(ctx, x, y, r, r * 2.1, 1, 0, [[0, 'rgba(200,170,255,0.8)'], [0.4, 'rgba(120,60,255,0.35)'], [1, 'rgba(80,30,200,0)']], null, 1);
    coreShadow(ctx, x, y, r, 1, layer);
  }
}

// ---------------------------------------------------------------- 5 · LENTE (refracta y retuerce lo que hay detrás)
let _lensCanvas = null;
function lookLens(ctx, t, layer, x, y, o) {
  const s = o.s * (0.35 + 0.65 * o.form) * (1 - easeIn(o.collapse || 0) * 0.8), Rl = 48 * s, c = o.collapse || 0;
  if (Rl <= 1) return;
  if (layer === 'back') { glow(ctx, x, y, 80 * s, [120, 60, 255], 0.3); return; }
  const m = ctx.getTransform ? ctx.getTransform() : null;
  if (m && m.a && typeof document !== 'undefined' && document.createElement && !showLayers) {
    // copia de lo que ya hay pintado detrás (en píxeles del lienzo), una vez por frame
    const dx0 = m.a * (x - Rl) + m.e, dy0 = m.d * (y - Rl) + m.f, dW = Math.max(2, Math.round(m.a * 2 * Rl)), dH = Math.max(2, Math.round(m.d * 2 * Rl));
    if (!_lensCanvas) _lensCanvas = document.createElement('canvas');
    if (_lensCanvas.width !== dW || _lensCanvas.height !== dH) { _lensCanvas.width = dW; _lensCanvas.height = dH; }
    const lc = _lensCanvas.getContext('2d'); lc.clearRect(0, 0, dW, dH);
    try { lc.drawImage(ctx.canvas, dx0, dy0, dW, dH, 0, 0, dW, dH); } catch (e) { /* fuera del lienzo */ }
    // anillos concéntricos: cada vez más aumentado y más girado hacia el centro → remolino
    const rings = 7, spin = t * 1.4 + (o.pullT >= 0 ? o.pullT * 2 : 0);
    for (let i = 0; i < rings; i++) {
      const u = i / rings, r = Rl * (1 - u), zoom = 1.15 + 1.1 * u + c * 1.5, tw = (0.25 + 1.6 * u * u) * Math.sin(spin * 0.7) + u * spin * 0.6;
      ctx.save(); ctx.beginPath(); ctx.arc(x, y, r, 0, TAU); ctx.clip(); ctx.translate(x, y); ctx.rotate(tw); ctx.scale(zoom, zoom);
      ctx.drawImage(_lensCanvas, -Rl, -Rl, 2 * Rl, 2 * Rl); ctx.restore();
    }
    // oscurece hacia el centro (la luz no sale)
    const g = ctx.createRadialGradient(x, y, 0, x, y, Rl); g.addColorStop(0, 'rgba(3,1,12,0.95)'); g.addColorStop(0.22, 'rgba(10,4,30,0.6)'); g.addColorStop(0.5, 'rgba(30,12,70,0.18)'); g.addColorStop(1, 'rgba(60,30,140,0.08)');
    ctx.save(); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(x, y, Rl, 0, TAU); ctx.fill(); ctx.restore();
  } else disc(ctx, x, y, Rl, '#1a0c3a', 0.8, layer);
  // borde de Fresnel, aberración cromática y brillo especular
  fillAnnulus(ctx, x, y, Rl * 0.82, Rl, 1, 0, [[0, 'rgba(255,255,255,0)'], [0.85, 'rgba(220,210,255,0.55)'], [1, 'rgba(255,255,255,0.9)']], null, 1);
  ctx.save(); ctx.lineWidth = 1.4;
  for (const [dx, cc] of [[-1.2, 'rgba(41,194,255,0.8)'], [1.2, 'rgba(255,80,200,0.7)']]) { ctx.strokeStyle = cc; ctx.beginPath(); ctx.arc(x + dx, y, Rl + 1.5, 0, TAU); ctx.stroke(); }
  ctx.restore();
  lens(ctx, x - Rl * 0.42, y - Rl * 0.5, -0.7, Rl * 0.5, Rl * 0.09, '#ffffff', 0.55, layer);
  coreShadow(ctx, x, y, (6 + 6 * (o.pullT >= 0 ? 1 : 0) + 8 * c) * s, 1, layer);
}

// ---------------------------------------------------------------- estallidos refinados
// onda continua (anillo relleno que se abre), destello alargado y pocas chispas finas
function burstRefined(ctx, t, layer, t0, x, y, s, pal, o = {}) {
  const u = t - t0; if (u < 0 || u > 1.5) return;
  const ru = seg(u, 0, 0.45), fu = seg(u, 0, 0.2), gy = o.gy === undefined ? GROUND : o.gy, near = gy - y < 170 * s;
  if (layer === 'front') {
    glow(ctx, x, y, 150 * s * (1 - fu * 0.4), pal.g1, 0.75 * (1 - fu));
    glow(ctx, x, y, 55 * s, pal.g2, 0.9 * (1 - fu));
    lens(ctx, x, y, 0, 260 * s * (1 - fu * 0.3), 6 * s * (1 - fu), '#ffffff', 1 - fu, layer);          // destello anamórfico
    lens(ctx, x, y, Math.PI / 2, 90 * s * (1 - fu * 0.4), 4 * s * (1 - fu), '#ffffff', 0.8 * (1 - fu), layer);
    disc(ctx, x, y, 16 * s * (1 - fu), '#ffffff', 1 - fu, layer);
    for (const [d, k, al] of [[0, 1, 1], [0.08, 0.75, 0.55]]) {
      const v = seg(u, d, d + 0.45); if (v <= 0 || v >= 1) continue;
      const r = (16 + 175 * easeOut(v)) * s * k, th = (24 * (1 - v) + 3) * s;
      fillAnnulus(ctx, x, y, Math.max(0.1, r - th), r, 0.95, 0, [[0, 'rgba(255,255,255,0)'], [0.55, rgba(pal.g1, 0.35)], [0.9, rgba(pal.g2, 0.85)], [1, 'rgba(255,255,255,0)']], null, al * (1 - v));
    }
  }
  if (near) {
    const r = (20 + 150 * easeOut(ru)) * s, th = 18 * s * (1 - ru) + 2;
    const stops = [[0, 'rgba(255,255,255,0)'], [0.6, rgba(pal.g1, 0.35)], [0.92, rgba(pal.g2, 0.8)], [1, 'rgba(255,255,255,0)']];
    fillAnnulus(ctx, x, gy, Math.max(0.1, r - th), r, 0.2, 0, stops, layer === 'back' ? 'back' : 'front', 1 - ru);
    dust(ctx, t, t0, x, layer, { n: 10, reach: 100 * s, sc: 1.1 * s, seed: 7, y: gy });
  }
  sparks(ctx, t, t0, x, y, layer, { n: 10, pal, sp: 420 * s, seed: 12, len: 22, life: 0.5, w: 1.8 });
  if (o.mass && near) { cracks(ctx, t, t0, x, layer, { n: 8, len: 110, pal }); debris(ctx, t, t0, x, layer, { n: 12, sp: 340, seed: 14 }); }
  if (o.fire && near) for (let i = 0; i < 12; i++) {
    const Rf = rand(i * 5 + 2), side = i % 2 ? 1 : -1, d = (14 + Rf() * 120) * s, tOn = t0 + d / (500 * s), lf = 0.8 + Rf() * 0.35;
    const a = seg(t, tOn, tOn + 0.08) * (1 - seg(t, tOn + lf * 0.7, tOn + lf)), lay = Rf() < 0.45 ? 'back' : 'front';
    if (lay === layer) flame(ctx, x + side * d, gy + 1, (22 + Rf() * 22) * a, (6 + Rf() * 5) * Math.max(0.3, a), t, i, a, layer);
  }
}
// grieta: se cierra de golpe en una línea horizontal y escupe los escombros
function burstRift(ctx, t, layer, t0, x, y, s, pal, o = {}) {
  const u = t - t0; if (u < 0 || u > 1.4) return;
  const fu = seg(u, 0, 0.25);
  if (layer === 'front') { lens(ctx, x, y, 0, 300 * s * easeOut(seg(u, 0, 0.1)) * (1 - fu * 0.5), 9 * s * (1 - fu), '#ffffff', 1 - fu, layer); lens(ctx, x, y, 0, 200 * s, 3 * s * (1 - fu), '#9ef2ff', 1 - fu, layer); glow(ctx, x, y, 120 * s, pal.g1, 0.6 * (1 - fu)); }
  const D = rand(78);
  for (let i = 0; i < 14; i++) { const a = D() * TAU, v = 200 + D() * 260, lay = D() < 0.4 ? 'back' : 'front', lf = 0.7 + D() * 0.4; if (lay !== layer || u > lf) continue; const px = x + Math.cos(a) * v * u, py = y + Math.sin(a) * v * u * 0.7 + 300 * u * u; chunk(ctx, px, py, (3 + D() * 4) * s, u * 9 + i, i, '#2a2f45', '#8a93b8', 1 - seg(u, lf * 0.7, lf), lay); }
  burstRefined(ctx, t, layer, t0 + 0.03, x, y, s * 0.8, pal, o);
}
// rejilla: la onda recorre la rejilla hacia fuera
function burstGrid(ctx, t, layer, t0, x, y, s, pal, o = {}) {
  const u = t - t0; if (u < 0 || u > 1.4) return;
  if (layer === 'back') {
    const Rg = 160 * s, step = 17 * s, front = (20 + 260 * easeOut(seg(u, 0, 0.6))) * s, fade = 1 - seg(u, 0.3, 1);
    for (const dir of [0, 1]) for (let k = -9; k <= 9; k++) {
      ctx.save(); ctx.lineWidth = 1.2; ctx.strokeStyle = col('#d6c8ff', layer);
      let prev = null;
      for (let i = 0; i <= 30; i++) {
        const v = -Rg + 2 * Rg * i / 30, px = dir ? x + k * step : x + v, py = dir ? y + v : y + k * step, dx = px - x, dy = py - y, d = Math.hypot(dx, dy) || 0.001;
        const wave = Math.exp(-Math.pow((d - front) / (18 * s), 2)) * 14 * s, q = { x: px + dx / d * wave, y: py + dy / d * wave, a: Math.pow(clamp(1 - d / Rg), 1.1) * (0.35 + wave / (14 * s)) };
        if (prev && q.a > 0.01) { ctx.globalAlpha = 0.55 * Math.min(1, q.a) * fade; ctx.beginPath(); ctx.moveTo(prev.x, prev.y); ctx.lineTo(q.x, q.y); ctx.stroke(); }
        prev = q;
      }
      ctx.restore();
    }
  }
  burstRefined(ctx, t, layer, t0, x, y, s, pal, o);
}

const SING_LOOKS = { rings: null, horizon: lookHorizon, whirl: lookWhirl, rift: lookRift, grid: lookGrid, lens: lookLens };
const SING_BURSTS = { rings: null, horizon: burstRefined, whirl: burstRefined, rift: burstRift, grid: burstGrid, lens: burstRefined };
// dibuja el aspecto elegido; over = todo por encima de los enemigos (si no, la mitad trasera va detrás de ellos)
function singLook(id, ctx, t, layer, x, y, o, over) {
  const f = SING_LOOKS[id];
  if (!f) return over ? voidOrbOver(ctx, t, layer, x, y, o) : voidOrb(ctx, t, layer, x, y, o);
  if (over) { if (layer === 'front') { f(ctx, t, 'back', x, y, o); f(ctx, t, 'front', x, y, o); } }
  else f(ctx, t, layer, x, y, o);
}
function singBurst(id, ctx, t, layer, t0, x, y, s, pal, o) {
  const f = SING_BURSTS[id];
  if (!f) return voidBurst(ctx, t, layer, t0, x, y, s, pal, o);
  f(ctx, t, layer, t0, x, y, s, pal, o);
}
