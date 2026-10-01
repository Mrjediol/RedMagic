// modules/vfx-draw.js
// -----------------------------------------------------------------------------
// The shared DRAWING HELPERS of the ✨ VFX Lab presets (modules/vfx-presets.js, vfx-effects.js).
//
// Everything here paints on a canvas whose origin is the frame's centre and whose unit is the
// frame's half size, y UP (so 1 = the edge of the frame) — never in pixels, which is what keeps the
// "Nitidez" parameter (a finer bake of the same picture) working. The canvas arrives in ADDITIVE
// blending (`lighter`): light adds up. Additive cannot draw anything dark (smoke, rock, the body of
// an ice crystal), so the helpers that need it paint inside `solid()` (`source-over`).
//
// Colours: a preset has three user colours — `core` (the hottest part), `glow` (its body) and
// `accent` (sparks, secondary shapes). `ramp(p, t)` extends them into core → glow → dark edge, which
// is what makes fire, ice and poison differ by more than hue.
//
// Loops: `k` runs 0..1 through a lane and a looping lane must draw the same picture at 0 and 1. Use
// `loopNoise` (periodic), `wrap(rnd + k)` lifetimes (`emit`) and whole turns (`k * TAU`).
//
// DOM-free.

export const TAU = Math.PI * 2;

// ============================================================ colour

export function rgba(hex, alpha) {
  const n = parseInt(hex.slice(1), 16);
  return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${Math.max(0, Math.min(1, alpha))})`;
}

/** `a` → `b` by `t` (0..1), as #rrggbb. */
export function mix(a, b, t) {
  const x = parseInt(a.slice(1), 16), y = parseInt(b.slice(1), 16);
  const u = Math.max(0, Math.min(1, t));
  const ch = (s) => Math.round(((x >> s) & 255) + (((y >> s) & 255) - ((x >> s) & 255)) * u);
  return `#${((1 << 24) | (ch(16) << 16) | (ch(8) << 8) | ch(0)).toString(16).slice(1)}`;
}

/** `hex` darkened: f = 1 keeps it, 0 = black. */
export const shade = (hex, f) => mix('#000000', hex, f);

/** The colour ramp of a preset: 0 = core, 0.5 = glow, 1 = the dark edge (a deep shade of the glow). */
export function ramp(p, t) {
  return t < 0.5 ? mix(p.core, p.glow, t * 2) : mix(p.glow, shade(p.glow, 0.32), (t - 0.5) * 2);
}

// ============================================================ numbers

/** Stable pseudo-random 0..1 for (seed, index, channel). */
export function hash(seed, i, channel = 0) {
  let t = (seed * 374761393 + i * 668265263 + channel * 2147483647) | 0;
  t = Math.imul(t ^ (t >>> 13), 1274126177);
  t ^= t >>> 16;
  return ((t >>> 0) % 100000) / 100000;
}

export const easeOut = (k) => 1 - (1 - k) * (1 - k) * (1 - k);
export const easeIn = (k) => k * k;
export const easeInOut = (k) => (k < 0.5 ? 2 * k * k : 1 - 2 * (1 - k) * (1 - k));
export const clamp01 = (k) => Math.max(0, Math.min(1, k));
/** 0 at both ends, 1 in the middle. */
export const bell = (k) => Math.sin(clamp01(k) * Math.PI);
/** 0 → 1 quickly, then back to 0 slowly: the envelope of something that pops in and fades. */
export const pop = (k, attack = 0.15) => (k < attack ? k / attack : 1 - (k - attack) / (1 - attack));
export const wrap = (t) => ((t % 1) + 1) % 1;

/** Smooth noise −1..1 that is periodic in `k` (period 1): what makes a loop wobble and still close. */
export function loopNoise(k, seed = 0) {
  return Math.sin(TAU * (k + hash(seed, 1, 9))) * 0.6
    + Math.sin(TAU * (2 * k + hash(seed, 2, 9))) * 0.3
    + Math.sin(TAU * (3 * k + hash(seed, 3, 9))) * 0.1;
}

/**
 * A looping particle emitter: calls `fn(i, t)` for `n` particles, `t` = each one's age 0..1. Ages
 * are spread out and advance with `k` (`cycles` lifetimes per loop), wrapping — so the loop closes.
 */
export function emit(n, k, rnd, fn, cycles = 1) {
  for (let i = 0; i < n; i++) fn(i, wrap(rnd(i, 1) + k * cycles));
}

// ============================================================ blending

/** Runs `fn` with normal blending (`source-over`): for dark or opaque shapes additive cannot draw. */
export function solid(ctx, fn) {
  ctx.save();
  ctx.globalCompositeOperation = 'source-over';
  fn();
  ctx.restore();
}

// ============================================================ light

/** Soft radial light. */
export function glowDot(ctx, x, y, r, color, alpha) {
  if (r <= 0.001 || alpha <= 0.003) return;
  const g = ctx.createRadialGradient(x, y, 0, x, y, r);
  g.addColorStop(0, rgba(color, alpha));
  g.addColorStop(0.45, rgba(color, alpha * 0.45));
  g.addColorStop(1, rgba(color, 0));
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.arc(x, y, r, 0, TAU);
  ctx.fill();
}

/** A ring of light (stroke with a soft halo). */
export function ring(ctx, x, y, r, width, color, alpha, squash = 1) {
  if (r <= 0.001 || alpha <= 0.003) return;
  ctx.save();
  ctx.translate(x, y);
  ctx.scale(1, squash);
  ctx.lineWidth = width * 2.6;
  ctx.strokeStyle = rgba(color, alpha * 0.25);
  ctx.beginPath(); ctx.arc(0, 0, r, 0, TAU); ctx.stroke();
  ctx.lineWidth = width;
  ctx.strokeStyle = rgba(color, alpha);
  ctx.beginPath(); ctx.arc(0, 0, r, 0, TAU); ctx.stroke();
  ctx.restore();
}

export function sparkShape(ctx, x, y, r, p, alpha, angle = 0) {
  if (r <= 0.001 || alpha <= 0.003) return;
  glowDot(ctx, x, y, r * 2.4, p.accent, alpha * 0.5 * p.intensity);
  ctx.fillStyle = rgba(p.core, alpha);
  if (p.spark === 'square') {
    ctx.save(); ctx.translate(x, y); ctx.rotate(angle);
    ctx.fillRect(-r * 0.7, -r * 0.7, r * 1.4, r * 1.4);
    ctx.restore();
  } else if (p.spark === 'star') {
    ctx.save(); ctx.translate(x, y); ctx.rotate(angle);
    ctx.beginPath();
    for (let i = 0; i < 8; i++) {
      const rr = i % 2 ? r * 0.32 : r * 1.25;
      const a = (i / 8) * TAU;
      if (i) ctx.lineTo(Math.cos(a) * rr, Math.sin(a) * rr); else ctx.moveTo(Math.cos(a) * rr, Math.sin(a) * rr);
    }
    ctx.closePath(); ctx.fill();
    ctx.restore();
  } else {
    ctx.beginPath(); ctx.arc(x, y, r * 0.75, 0, TAU); ctx.fill();
  }
}

/** The glowing ball every orb-like preset shares. */
export function core(ctx, x, y, r, p, alpha = 1) {
  glowDot(ctx, x, y, r * 2.6, p.glow, 0.55 * alpha * p.intensity);
  glowDot(ctx, x, y, r * 1.5, p.glow, 0.9 * alpha * p.intensity);
  glowDot(ctx, x, y, r, p.core, alpha);
  ctx.fillStyle = rgba(p.core, alpha);
  ctx.beginPath(); ctx.arc(x, y, r * 0.45, 0, TAU); ctx.fill();
}

/** One-shot burst: flash, expanding ring, sparks thrown outwards. Shared by every "impact". */
export function burst(ctx, k, p, rnd, scale = 1) {
  const out = easeOut(k);
  const fade = 1 - easeIn(k);
  core(ctx, 0, 0, 0.34 * scale * (1 - k * 0.85), p, fade);
  ring(ctx, 0, 0, (0.18 + 0.66 * out) * scale, 0.07 * fade + 0.012, p.glow, fade * p.intensity);
  for (let i = 0; i < p.particles; i++) {
    const a = rnd(i, 1) * TAU;
    const d = (0.15 + (0.45 + rnd(i, 2) * 0.4) * out) * scale;
    sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d, (0.03 + rnd(i, 3) * 0.045) * (1 - k * 0.6), p, fade, a + k * 3);
  }
}

/** An expanding shock ring: bright at radius `r`, soft falloff `thick` inwards (a wave front). */
export function shockRing(ctx, x, y, r, thick, color, alpha, squash = 1) {
  if (r <= 0.001 || alpha <= 0.003) return;
  const r0 = Math.max(0, r - thick), r1 = r + thick * 0.3;
  ctx.save();
  ctx.translate(x, y);
  ctx.scale(1, squash);
  const g = ctx.createRadialGradient(0, 0, r0, 0, 0, r1);
  g.addColorStop(0, rgba(color, 0));
  g.addColorStop(0.55, rgba(color, alpha * 0.35));
  g.addColorStop((r - r0) / (r1 - r0), rgba(color, alpha));
  g.addColorStop(1, rgba(color, 0));
  ctx.fillStyle = g;
  ctx.beginPath(); ctx.arc(0, 0, r1, 0, TAU); ctx.fill();
  ctx.restore();
}

/** A star / cross flare: `arms` thin rays out of a bright point. */
export function flare(ctx, x, y, r, color, alpha, arms = 4, angle = 0, thin = 0.1) {
  if (r <= 0.001 || alpha <= 0.003) return;
  glowDot(ctx, x, y, r * 0.55, color, alpha * 0.7);
  ctx.save();
  ctx.translate(x, y);
  ctx.rotate(angle);
  const g = ctx.createRadialGradient(0, 0, 0, 0, 0, r);
  g.addColorStop(0, rgba(color, alpha));
  g.addColorStop(1, rgba(color, 0));
  ctx.fillStyle = g;
  for (let i = 0; i < arms; i++) {
    ctx.rotate(TAU / arms);
    ctx.beginPath();
    ctx.moveTo(r, 0); ctx.lineTo(0, r * thin); ctx.lineTo(0, -r * thin);
    ctx.closePath(); ctx.fill();
  }
  ctx.restore();
}

/** A fan of light from (x, y) towards `angle`, `spread` radians wide, fading with distance. */
export function cone(ctx, x, y, angle, spread, len, color, alpha) {
  if (len <= 0.001 || alpha <= 0.003) return;
  const g = ctx.createRadialGradient(x, y, 0, x, y, len);
  g.addColorStop(0, rgba(color, alpha));
  g.addColorStop(0.5, rgba(color, alpha * 0.4));
  g.addColorStop(1, rgba(color, 0));
  ctx.fillStyle = g;
  [1, 0.66, 0.36].forEach((s) => {            // three nested fans = a soft edge
    ctx.beginPath();
    ctx.moveTo(x, y);
    ctx.arc(x, y, len, angle - (spread * s) / 2, angle + (spread * s) / 2);
    ctx.closePath(); ctx.fill();
  });
}

// ============================================================ shapes

/** A tapered streak / teardrop: round and bright at the head, a point at the tail. */
export function streak(ctx, hx, hy, tx, ty, width, color, alpha) {
  const len = Math.hypot(tx - hx, ty - hy);
  if (len < 0.001 || width <= 0.001 || alpha <= 0.003) return;
  ctx.save();
  ctx.translate(hx, hy);
  ctx.rotate(Math.atan2(ty - hy, tx - hx));
  const g = ctx.createLinearGradient(-width / 2, 0, len, 0);
  g.addColorStop(0, rgba(color, alpha));
  g.addColorStop(0.35, rgba(color, alpha * 0.6));
  g.addColorStop(1, rgba(color, 0));
  ctx.fillStyle = g;
  const w = width / 2;
  ctx.beginPath();
  ctx.arc(0, 0, w, Math.PI / 2, Math.PI * 1.5);
  ctx.quadraticCurveTo(len * 0.4, -w * 0.55, len, 0);
  ctx.quadraticCurveTo(len * 0.4, w * 0.55, 0, w);
  ctx.closePath(); ctx.fill();
  ctx.restore();
}

/** A sliver of light from (x0, y0) to (x1, y1): pointed at both ends, `width` in the middle. */
export function sliver(ctx, x0, y0, x1, y1, width, color, alpha) {
  const len = Math.hypot(x1 - x0, y1 - y0);
  if (len < 0.001 || width <= 0.001 || alpha <= 0.003) return;
  const nx = (-(y1 - y0) / len) * (width / 2), ny = ((x1 - x0) / len) * (width / 2);
  const mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
  const g = ctx.createLinearGradient(x0, y0, x1, y1);
  g.addColorStop(0, rgba(color, 0));
  g.addColorStop(0.5, rgba(color, alpha));
  g.addColorStop(1, rgba(color, 0));
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.moveTo(x0, y0);
  ctx.quadraticCurveTo(mx + nx * 2, my + ny * 2, x1, y1);
  ctx.quadraticCurveTo(mx - nx * 2, my - ny * 2, x0, y0);
  ctx.closePath(); ctx.fill();
}

/**
 * A crystal shard pointing along `angle` (sharp tip forwards, blunt back), OPAQUE: body in the glow
 * colour, a lit upper facet and a dark lower one. `tone` 0..1 shifts it darker (shards further back).
 */
export function shard(ctx, x, y, len, wid, angle, p, alpha = 1, tone = 0) {
  if (len <= 0.001 || alpha <= 0.003) return;
  const L = len / 2, W = wid / 2;
  solid(ctx, () => {
    ctx.translate(x, y);
    ctx.rotate(angle);
    const body = () => {
      ctx.beginPath();
      ctx.moveTo(L, 0); ctx.lineTo(L * 0.2, W); ctx.lineTo(-L * 0.75, W * 0.7); ctx.lineTo(-L, 0);
      ctx.lineTo(-L * 0.75, -W * 0.7); ctx.lineTo(L * 0.2, -W);
      ctx.closePath();
    };
    body();
    ctx.fillStyle = rgba(mix(p.glow, shade(p.glow, 0.5), tone), alpha);
    ctx.fill();
    ctx.beginPath();                                   // lit upper facet
    ctx.moveTo(L, 0); ctx.lineTo(L * 0.2, W); ctx.lineTo(-L * 0.75, W * 0.7); ctx.lineTo(-L, 0);
    ctx.closePath();
    ctx.fillStyle = rgba(mix(p.core, p.glow, 0.25 + tone * 0.5), alpha * 0.9);
    ctx.fill();
    ctx.beginPath();                                   // dark lower back facet
    ctx.moveTo(-L, 0); ctx.lineTo(-L * 0.75, -W * 0.7); ctx.lineTo(L * 0.2, -W); ctx.lineTo(-L * 0.1, 0);
    ctx.closePath();
    ctx.fillStyle = rgba(shade(p.glow, 0.55), alpha * 0.75);
    ctx.fill();
    ctx.lineWidth = Math.min(0.02, wid * 0.12);
    ctx.lineJoin = 'round';
    ctx.strokeStyle = rgba(p.core, alpha * 0.9);
    body(); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(-L, 0); ctx.lineTo(L, 0); ctx.stroke();
  });
}

/** A crescent / slash arc of radius `r` from angle a0 to a1: thick in the middle, pointed at both ends, brighter towards a1. */
export function crescent(ctx, x, y, r, a0, a1, thick, color, alpha, lean = 0.35) {
  if (r <= 0.001 || alpha <= 0.003) return;
  const steps = 22;
  const g = ctx.createLinearGradient(x + Math.cos(a0) * r, y + Math.sin(a0) * r, x + Math.cos(a1) * r, y + Math.sin(a1) * r);
  g.addColorStop(0, rgba(color, alpha * lean));
  g.addColorStop(1, rgba(color, alpha));
  ctx.fillStyle = g;
  ctx.beginPath();
  for (let i = 0; i <= steps; i++) {
    const a = a0 + (a1 - a0) * (i / steps);
    if (i) ctx.lineTo(x + Math.cos(a) * r, y + Math.sin(a) * r); else ctx.moveTo(x + Math.cos(a) * r, y + Math.sin(a) * r);
  }
  for (let i = steps; i >= 0; i--) {
    const t = i / steps;
    const a = a0 + (a1 - a0) * t;
    const rr = r - thick * Math.sin(t * Math.PI) ** 0.8;
    ctx.lineTo(x + Math.cos(a) * rr, y + Math.sin(a) * rr);
  }
  ctx.closePath(); ctx.fill();
}

/** The points of a jagged bolt from (x0, y0) to (x1, y1): seeded, so the same `seed` = the same bolt. */
export function boltPath(x0, y0, x1, y1, jitter, segs, seed) {
  const nx = -(y1 - y0), ny = x1 - x0;
  const len = Math.hypot(nx, ny) || 1;
  const pts = [];
  for (let i = 0; i <= segs; i++) {
    const t = i / segs;
    const off = i === 0 || i === segs ? 0 : (hash(seed, i, 11) - 0.5) * 2 * jitter;
    pts.push([x0 + (x1 - x0) * t + (nx / len) * off, y0 + (y1 - y0) * t + (ny / len) * off]);
  }
  return pts;
}

/** Strokes a bolt (boltPath): a wide halo, a glow and a hot thin core. */
export function lightning(ctx, pts, width, p, alpha = 1) {
  if (alpha <= 0.003 || pts.length < 2) return;
  ctx.save();
  ctx.lineJoin = 'round';
  ctx.lineCap = 'round';
  [[5.5, p.glow, 0.16 * p.intensity], [2.4, p.glow, 0.55 * p.intensity], [1, p.core, 1]].forEach(([w, color, a]) => {
    ctx.lineWidth = width * w;
    ctx.strokeStyle = rgba(color, a * alpha);
    ctx.beginPath();
    pts.forEach(([x, y], i) => (i ? ctx.lineTo(x, y) : ctx.moveTo(x, y)));
    ctx.stroke();
  });
  ctx.restore();
}

/** A puff of smoke / dust: soft, OPAQUE-ish (normal blending), so it can be dark. */
export function puff(ctx, x, y, r, color, alpha) {
  if (r <= 0.001 || alpha <= 0.003) return;
  solid(ctx, () => {
    const g = ctx.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, rgba(color, alpha));
    g.addColorStop(0.55, rgba(color, alpha * 0.7));
    g.addColorStop(1, rgba(color, 0));
    ctx.fillStyle = g;
    ctx.beginPath(); ctx.arc(x, y, r, 0, TAU); ctx.fill();
  });
}

/**
 * A flame tongue: base centred on (x, y), `w` wide, tip at (x + lean, y + h). Three nested layers
 * down the colour ramp (dark edge → glow → core).
 */
export function flame(ctx, x, y, w, h, lean, p, alpha = 1) {
  if (h <= 0.001 || w <= 0.001 || alpha <= 0.003) return;
  [[1, ramp(p, 0.8), 0.55 * p.intensity], [0.74, p.glow, 0.85 * p.intensity], [0.44, p.core, 1]].forEach(([s, color, a]) => {
    const hw = (w / 2) * s, hh = h * s, ln = lean * s;
    const g = ctx.createLinearGradient(0, y, 0, y + hh);
    g.addColorStop(0, rgba(color, a * alpha));
    g.addColorStop(0.75, rgba(color, a * alpha * 0.85));
    g.addColorStop(1, rgba(color, a * alpha * 0.25));
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.moveTo(x - hw, y);
    ctx.bezierCurveTo(x - hw * 1.05, y + hh * 0.45, x + ln * 0.5 - hw * 0.3, y + hh * 0.7, x + ln, y + hh);
    ctx.bezierCurveTo(x + ln * 0.5 + hw * 0.3, y + hh * 0.7, x + hw * 1.05, y + hh * 0.45, x + hw, y);
    ctx.quadraticCurveTo(x, y - hw * 0.8, x - hw, y);
    ctx.closePath(); ctx.fill();
  });
}

/** A droplet: round body with a pointed tail towards `angle` + π (so it "flies" towards `angle`), OPAQUE, with a highlight. */
export function droplet(ctx, x, y, r, angle, p, alpha = 1, stretch = 1.6) {
  if (r <= 0.001 || alpha <= 0.003) return;
  solid(ctx, () => {
    ctx.translate(x, y);
    ctx.rotate(angle);
    const g = ctx.createRadialGradient(r * 0.2, r * 0.25, 0, 0, 0, r * 1.2);
    g.addColorStop(0, rgba(mix(p.core, p.glow, 0.45), alpha));
    g.addColorStop(0.6, rgba(p.glow, alpha));
    g.addColorStop(1, rgba(shade(p.glow, 0.5), alpha));
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(0, 0, r, -Math.PI / 2, Math.PI / 2);
    ctx.quadraticCurveTo(-r * 0.6, r * 0.75, -r * (1 + stretch), 0);
    ctx.quadraticCurveTo(-r * 0.6, -r * 0.75, 0, -r);
    ctx.closePath(); ctx.fill();
    ctx.fillStyle = rgba(p.core, alpha * 0.85);
    ctx.beginPath(); ctx.arc(r * 0.25, r * 0.3, r * 0.26, 0, TAU); ctx.fill();
  });
}

/** A bubble: thin rim, faint body and a highlight. */
export function bubble(ctx, x, y, r, p, alpha = 1) {
  if (r <= 0.001 || alpha <= 0.003) return;
  solid(ctx, () => {
    ctx.fillStyle = rgba(p.glow, alpha * 0.28);
    ctx.beginPath(); ctx.arc(x, y, r, 0, TAU); ctx.fill();
    ctx.lineWidth = Math.max(0.012, r * 0.2);
    ctx.strokeStyle = rgba(mix(p.glow, p.core, 0.35), alpha * 0.95);
    ctx.beginPath(); ctx.arc(x, y, r, 0, TAU); ctx.stroke();
    ctx.fillStyle = rgba(p.core, alpha);
    ctx.beginPath(); ctx.arc(x - r * 0.35, y + r * 0.35, r * 0.24, 0, TAU); ctx.fill();
  });
}

/** An irregular chunk of rock (seeded outline), OPAQUE: lit from above, dark below, with an outline. */
export function chunk(ctx, x, y, r, angle, p, alpha = 1, seed = 1) {
  if (r <= 0.001 || alpha <= 0.003) return;
  const n = 7;
  const pts = [];
  for (let i = 0; i < n; i++) {
    const a = (i / n) * TAU;
    const rr = r * (0.72 + hash(seed, i, 21) * 0.36);
    pts.push([Math.cos(a) * rr, Math.sin(a) * rr]);
  }
  const outline = () => { ctx.beginPath(); pts.forEach(([px, py], i) => (i ? ctx.lineTo(px, py) : ctx.moveTo(px, py))); ctx.closePath(); };
  solid(ctx, () => {
    ctx.translate(x, y);
    ctx.save();
    ctx.rotate(angle);
    outline();
    ctx.restore();
    // The light stays above while the rock tumbles: the gradient is NOT rotated with the outline.
    const g = ctx.createLinearGradient(-r * 0.4, r, r * 0.4, -r);
    g.addColorStop(0, rgba(p.core, alpha));
    g.addColorStop(0.45, rgba(p.glow, alpha));
    g.addColorStop(1, rgba(shade(p.glow, 0.42), alpha));
    ctx.fillStyle = g;
    ctx.fill();
    ctx.lineWidth = Math.min(0.03, r * 0.14);
    ctx.lineJoin = 'round';
    ctx.strokeStyle = rgba(shade(p.glow, 0.3), alpha);
    ctx.stroke();
    ctx.rotate(angle);                                  // one crack that turns with it
    ctx.lineWidth = Math.min(0.02, r * 0.09);
    ctx.strokeStyle = rgba(shade(p.glow, 0.45), alpha * 0.8);
    ctx.beginPath(); ctx.moveTo(pts[1][0] * 0.7, pts[1][1] * 0.7); ctx.lineTo(0, r * 0.05); ctx.lineTo(pts[4][0] * 0.6, pts[4][1] * 0.6); ctx.stroke();
  });
}

/**
 * A wobbling blob of liquid, OPAQUE: radius `r` with two travelling lobes (periodic in `k`), lit from
 * the upper left, dark rim. `sx` stretches it along X.
 */
export function blob(ctx, x, y, r, k, p, alpha = 1, sx = 1, wobble = 1) {
  if (r <= 0.001 || alpha <= 0.003) return;
  solid(ctx, () => {
    ctx.translate(x, y);
    ctx.scale(sx, 1);
    const g = ctx.createRadialGradient(-r * 0.3, r * 0.35, 0, 0, 0, r * 1.15);
    g.addColorStop(0, rgba(mix(p.core, p.glow, 0.35), alpha));
    g.addColorStop(0.5, rgba(p.glow, alpha));
    g.addColorStop(1, rgba(shade(p.glow, 0.45), alpha));
    ctx.fillStyle = g;
    ctx.beginPath();
    const steps = 28;
    for (let i = 0; i <= steps; i++) {
      const a = (i / steps) * TAU;
      const rr = r * (1 + wobble * (0.08 * Math.sin(2 * a + k * TAU) + 0.05 * Math.sin(3 * a - k * TAU * 2 + 1)));
      if (i) ctx.lineTo(Math.cos(a) * rr, Math.sin(a) * rr); else ctx.moveTo(Math.cos(a) * rr, Math.sin(a) * rr);
    }
    ctx.closePath(); ctx.fill();
    ctx.lineWidth = Math.min(0.025, r * 0.12);
    ctx.strokeStyle = rgba(shade(p.glow, 0.35), alpha * 0.9);
    ctx.stroke();
    ctx.fillStyle = rgba(p.core, alpha * 0.9);
    ctx.beginPath(); ctx.ellipse(-r * 0.32, r * 0.38, r * 0.2, r * 0.12, -0.5, 0, TAU); ctx.fill();
  });
}
