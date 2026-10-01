// modules/vfx-effects.js
// -----------------------------------------------------------------------------
// The ✨ VFX Lab's EFFECTS LIBRARY: the presets beyond the first five, grouped by `family`
// (vfx-presets.js ▸ FAMILIES). Same shape as every preset — data + one `draw(ctx, k, p, rnd)` per
// lane, in the centred y-up unit square — built from the shared helpers of vfx-draw.js.
//
// Conventions kept by every entry here:
//   - `kind` + lane names are the canonical ones: `projectile` = Move (loop) / Impact;
//     `fx` = Spawn / Loop / End. That is what rig-fx.js ▸ pickLane and the art pickers look for.
//   - A projectile points to +X: head near x = HEAD, tail towards −X.
//   - Nothing reaches the frame edge at default parameters (stay inside ±0.95).
//   - A looping lane closes (k = 1 draws what k = 0 draws); a one-shot is never empty mid-way.
//   - The one-shot pieces (explosion, shatter, splash…) are functions shared by a projectile's
//     Impact and by the stand-alone burst of the same element.
//
// Adding an effect = one more entry in its family's array.

import {
  TAU, rgba, mix, shade, ramp, easeOut, easeIn, bell, pop, clamp01, loopNoise, emit, solid,
  glowDot, ring, sparkShape, core, burst, shockRing, flare, cone, streak, shard, crescent,
  boltPath, lightning, puff, flame, droplet, bubble, chunk, blob, sliver,
} from './vfx-draw.js';

/** Where a projectile's head sits (its tail runs towards −X). */
const HEAD = 0.42;

const TAIL = (def = 1) => ({ key: 'tail', label: 'Largo de la estela', type: 'float', min: 0.3, max: 1.3, step: 0.05, default: def });

/**
 * A faint dark shadow of the effect's own colour under a pale streak of light, so it still reads over
 * a bright scene (ice, sky): nothing on a dark one.
 */
function streakUnder(ctx, headX, tailX, width, p, alpha = 0.3) {
  solid(ctx, () => streak(ctx, headX, 0, tailX, 0, width, shade(p.glow, 0.4), alpha));
}

// ============================================================ shared one-shots

/** Fire: a white flash, lobes of flame cooling down the ramp, dark smoke left behind, embers. */
function explosion(ctx, k, p, rnd, scale = 1) {
  const out = easeOut(k), fade = 1 - easeIn(k);
  const smoke = shade(p.glow, 0.2);
  for (let i = 0; i < 7; i++) {
    const a = rnd(i, 31) * TAU;
    const d = (0.1 + 0.4 * out * (0.5 + rnd(i, 32) * 0.5)) * scale;
    puff(ctx, Math.cos(a) * d, Math.sin(a) * d * 0.85 + 0.14 * k * scale, (0.2 + 0.14 * out) * scale * (0.7 + rnd(i, 33) * 0.5),
      smoke, 0.6 * clamp01(k * 2.4) * (1 - k * k));
  }
  for (let i = 0; i < 8; i++) {
    const a = rnd(i, 34) * TAU;
    const d = (0.04 + 0.4 * out * (0.4 + rnd(i, 35) * 0.6)) * scale;
    const life = clamp01(1 - k * (1.1 + rnd(i, 36) * 0.5));
    glowDot(ctx, Math.cos(a) * d, Math.sin(a) * d, (0.3 - 0.1 * k) * scale * (0.7 + rnd(i, 37) * 0.5), ramp(p, 0.3 + k * 0.55), life * 0.9 * p.intensity);
  }
  glowDot(ctx, 0, 0, (0.5 - 0.2 * k) * scale, p.core, clamp01(1 - k * 2.2));
  shockRing(ctx, 0, 0, (0.2 + 0.62 * out) * scale, 0.16 * scale, p.glow, fade * 0.55 * p.intensity);
  for (let i = 0; i < p.particles; i++) {
    const a = rnd(i, 1) * TAU;
    const d = (0.1 + (0.45 + rnd(i, 2) * 0.35) * out) * scale;
    sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d - 0.2 * k * k * scale, (0.025 + rnd(i, 3) * 0.03) * (1 - k * 0.5), p, fade, a + k * 3);
  }
}

/** Ice: a cold flash and shards thrown outwards, tumbling and falling a little. */
function shatter(ctx, k, p, rnd, scale = 1) {
  const out = easeOut(k), fade = 1 - easeIn(k);
  glowDot(ctx, 0, 0, 0.6 * scale * (1 - k * 0.4), p.glow, 0.5 * fade * p.intensity);
  shockRing(ctx, 0, 0, (0.15 + 0.6 * out) * scale, 0.12 * scale, p.accent, fade * 0.55 * p.intensity);
  flare(ctx, 0, 0, 0.62 * scale * (1 - k * 0.5), p.core, clamp01(1 - k * 1.7), 6, 0.3);
  const n = 9;
  for (let i = 0; i < n; i++) {
    const a = (i / n) * TAU + rnd(i, 41) * 0.5;
    const d = (0.1 + (0.36 + rnd(i, 42) * 0.26) * out) * scale;
    const len = (0.22 + rnd(i, 43) * 0.16) * scale * (1 - k * 0.3);
    shard(ctx, Math.cos(a) * d, Math.sin(a) * d - 0.16 * k * k * scale, len, len * 0.4, a + (rnd(i, 44) - 0.5) * k * 2.5, p, clamp01(fade * 1.3), rnd(i, 45) * 0.6);
  }
  for (let i = 0; i < p.particles; i++) {
    const a = rnd(i, 1) * TAU;
    const d = (0.15 + (0.4 + rnd(i, 2) * 0.35) * out) * scale;
    sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d, (0.02 + rnd(i, 3) * 0.025) * (1 - k * 0.5), p, fade, a);
  }
}

/** Liquid: the glob flattens and droplets arc out and fall. */
function splash(ctx, k, p, rnd, scale = 1) {
  const out = easeOut(k), fade = 1 - easeIn(k);
  glowDot(ctx, 0, 0, 0.55 * scale, p.glow, 0.35 * fade * p.intensity);
  shockRing(ctx, 0, 0, (0.15 + 0.5 * out) * scale, 0.1 * scale, p.accent, fade * 0.45 * p.intensity);
  blob(ctx, 0, 0, 0.26 * scale * (1 - k), k, p, clamp01(fade * 1.5), 1 + k * 0.8, 2);
  const n = 10;
  for (let i = 0; i < n; i++) {
    const a = (i / n) * TAU + rnd(i, 51) * 0.6;
    const sp = (0.5 + rnd(i, 52) * 0.4) * scale;
    const vx = Math.cos(a) * sp, vy = Math.sin(a) * sp + 0.25 * scale;
    const g = 1.3 * scale;
    droplet(ctx, vx * out, vy * out - 0.5 * g * k * k, (0.05 + rnd(i, 53) * 0.04) * scale * (1 - k * 0.45),
      Math.atan2(vy * (1 - k) - g * k, vx * (1 - k) + 0.0001), p, clamp01(fade * 1.4), 1.2);
  }
  for (let i = 0; i < Math.min(6, p.particles); i++) {
    const a = rnd(i, 1) * TAU;
    const d = (0.1 + 0.35 * rnd(i, 2) * out) * scale;
    bubble(ctx, Math.cos(a) * d, Math.sin(a) * d + 0.1 * k, (0.03 + rnd(i, 3) * 0.03) * scale, p, bell(k));
  }
}

/** Electricity: jagged arcs out of a white point, a different set every frame. */
function arcBurst(ctx, k, p, rnd, scale = 1) {
  const out = easeOut(k), fade = 1 - easeIn(k);
  const f = Math.round(k * 600);
  glowDot(ctx, 0, 0, 0.7 * scale * (1 - k * 0.3), p.glow, 0.4 * fade * p.intensity);
  const n = 6;
  for (let i = 0; i < n; i++) {
    const a = (i / n) * TAU + rnd(i + f, 61) * 0.9;
    const r1 = (0.3 + 0.5 * out * (0.6 + rnd(i, 62) * 0.4)) * scale;
    const r0 = 0.05 * scale + r1 * 0.25 * k;
    lightning(ctx, boltPath(Math.cos(a) * r0, Math.sin(a) * r0, Math.cos(a) * r1, Math.sin(a) * r1, 0.09 * scale, 5, p.seed * 131 + i * 17 + f),
      0.024 * scale, p, fade);
  }
  flare(ctx, 0, 0, 0.5 * scale * (1 - k * 0.6), p.core, clamp01(1 - k * 1.5), 4, 0.6, 0.14);
  core(ctx, 0, 0, 0.16 * scale * (1 - k * 0.7), p, fade);
  for (let i = 0; i < p.particles; i++) {
    const a = rnd(i, 1) * TAU;
    const d = (0.2 + (0.4 + rnd(i, 2) * 0.3) * out) * scale;
    sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d, (0.018 + rnd(i, 3) * 0.02) * (1 - k * 0.4), p, fade, a);
  }
}

/** Rock: chunks fly and fall, dust swells. No light apart from a small spark of the hit. */
function debris(ctx, k, p, rnd, scale = 1) {
  const out = easeOut(k), fade = 1 - easeIn(k);
  const dust = mix(p.accent, p.glow, 0.45);
  for (let i = 0; i < 7; i++) {
    const a = rnd(i, 71) * TAU;
    const d = (0.08 + 0.4 * out * (0.4 + rnd(i, 72) * 0.6)) * scale;
    puff(ctx, Math.cos(a) * d, Math.sin(a) * d * 0.7 + 0.08 * k, (0.16 + 0.16 * out) * scale * (0.7 + rnd(i, 73) * 0.5), dust, 0.55 * fade);
  }
  glowDot(ctx, 0, 0, 0.4 * scale, p.core, 0.5 * clamp01(1 - k * 3));
  const n = 7;
  for (let i = 0; i < n; i++) {
    const a = (i / n) * TAU + rnd(i, 74) * 0.7;
    const sp = (0.45 + rnd(i, 75) * 0.35) * scale;
    chunk(ctx, Math.cos(a) * sp * out, (Math.sin(a) * sp + 0.2 * scale) * out - 0.45 * k * k * scale,
      (0.07 + rnd(i, 76) * 0.07) * scale * (1 - k * 0.25), (rnd(i, 77) - 0.5) * 9 * k, p, clamp01(fade * 1.6), p.seed + i * 13);
  }
}

/** A hit: a cross flare and thin streaks flung outwards. Small and sharp. */
function hitSpark(ctx, k, p, rnd, scale = 1) {
  const out = easeOut(k), fade = 1 - easeIn(k);
  glowDot(ctx, 0, 0, 0.5 * scale * (1 - k * 0.5), p.glow, 0.55 * fade * p.intensity);
  const n = Math.max(4, p.particles);
  for (let i = 0; i < n; i++) {
    const a = (i / n) * TAU + rnd(i, 81) * 0.5;
    const far = (0.3 + (0.35 + rnd(i, 82) * 0.25) * out) * scale;
    const near = far - (0.26 + rnd(i, 83) * 0.14) * scale * (1 - k * 0.6);
    streak(ctx, Math.cos(a) * far, Math.sin(a) * far, Math.cos(a) * near, Math.sin(a) * near, 0.05 * scale, i % 2 ? p.accent : p.core, fade);
  }
  flare(ctx, 0, 0, 0.75 * scale * (1 - k * 0.55), p.core, clamp01(1 - k * 1.3), 4, 0.25 + k * 0.4, 0.13);
  core(ctx, 0, 0, 0.14 * scale * (1 - k), p, fade);
}

// ============================================================ projectiles

const PROJECTILES = [
  {
    id: 'fireball',
    label: 'Bola de fuego',
    description: 'Una bola ardiendo con lenguas de llama hacia atrás y ascuas. Al chocar: explosión con humo.',
    defaults: { core: '#fff4c2', glow: '#ff7a1a', accent: '#ffd04a', particles: 8 },
    params: [TAIL(0.95)],
    lanes: [
      { name: 'Move', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          for (let i = 10; i >= 1; i--) {
            const t = i / 10;
            glowDot(ctx, HEAD - t * p.tail, loopNoise(k + t * 0.5, 3) * 0.04 * t, 0.34 * (1 - t * 0.6), ramp(p, 0.5 + t * 0.5), 0.4 * (1 - t) * p.intensity);
          }
          // A teardrop of fire down the ramp (dark edge → glow → core), with tongues licking off its sides.
          const wob = loopNoise(k, 3) * 0.05;
          streak(ctx, HEAD, 0, HEAD - p.tail * 1.25, wob, 0.62, ramp(p, 0.85), 0.6 * p.intensity);
          for (let i = 0; i < 5; i++) {
            const side = i % 2 ? 1 : -1;
            const len = p.tail * (0.6 + 0.4 * rnd(i, 2)) * (0.8 + 0.2 * loopNoise(k, 10 + i));
            streak(ctx, HEAD - 0.06, side * 0.1, HEAD - len * 1.15, side * (0.16 + 0.2 * rnd(i, 4)) + loopNoise(k, 20 + i) * 0.06, 0.3, p.glow, 0.7 * p.intensity);
          }
          streak(ctx, HEAD, 0, HEAD - p.tail, wob, 0.46, p.glow, 0.9 * p.intensity);
          streak(ctx, HEAD + 0.02, 0, HEAD - p.tail * 0.55, wob * 0.5, 0.3, p.core, 1);
          core(ctx, HEAD, 0, 0.2 * (1 + 0.06 * Math.sin(k * TAU)), p);
          emit(p.particles, k, rnd, (i, t) => {
            sparkShape(ctx, HEAD - 0.1 - t * p.tail * 1.2, (rnd(i, 2) - 0.5) * 0.6 * (0.3 + t), 0.025 + rnd(i, 3) * 0.02, p, (1 - t) * 0.9, t * 6);
          });
        } },
      { name: 'Impact', loop: false, fps: 14, frames: 8, draw: (ctx, k, p, rnd) => explosion(ctx, k, p, rnd, 0.95) },
    ],
  },
  {
    id: 'iceShard',
    label: 'Esquirla de hielo',
    description: 'Un cristal afilado con un destello que lo recorre y escarcha detrás. Al chocar se hace añicos.',
    defaults: { core: '#ffffff', glow: '#58bfff', accent: '#d6f6ff', particles: 8 },
    params: [],
    lanes: [
      { name: 'Move', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          glowDot(ctx, 0.12, 0, 0.62, p.glow, 0.32 * p.intensity);
          streak(ctx, 0, 0, -0.92, 0, 0.34, p.glow, 0.4 * p.intensity);
          emit(p.particles, k, rnd, (i, t) => {
            sparkShape(ctx, -0.15 - t * 0.72, (rnd(i, 2) - 0.5) * 0.4 * (0.4 + t * 0.6), 0.02 + rnd(i, 3) * 0.022, p, (1 - t) * 0.95, t * 4);
          });
          for (let j = 0; j < 2; j++) {
            const s = j ? -1 : 1;
            shard(ctx, -0.34 - j * 0.14, s * (0.2 + 0.02 * Math.sin(k * TAU + j * 2)), 0.3, 0.1, s * 0.12, p, 0.9, 0.35);
          }
          shard(ctx, 0.16, 0, 1.3, 0.36, 0, p);
          flare(ctx, -0.35 + 1.05 * k, 0.03, 0.26, p.core, bell(k), 4, 0, 0.12);
        } },
      { name: 'Impact', loop: false, fps: 14, frames: 7, draw: (ctx, k, p, rnd) => shatter(ctx, k, p, rnd, 0.95) },
    ],
  },
  {
    id: 'poisonGlob',
    label: 'Glóbulo de veneno',
    description: 'Una masa viscosa que tiembla y gotea al volar. Al chocar salpica.',
    defaults: { core: '#f4ffc8', glow: '#6fcf2a', accent: '#c9ff66', particles: 6 },
    params: [],
    lanes: [
      { name: 'Move', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          glowDot(ctx, HEAD - 0.1, 0, 0.56, p.glow, 0.3 * p.intensity);
          emit(5, k, rnd, (i, t) => {
            droplet(ctx, HEAD - 0.28 - t * 0.78, (rnd(i, 2) - 0.5) * 0.3 - t * t * 0.14, 0.085 * (1 - t * 0.65), 0, p, clamp01((1 - t) * 1.6), 1.3);
          });
          blob(ctx, HEAD - 0.08, 0, 0.3, k, p, 1, 1.18);
          emit(Math.min(4, p.particles), k, rnd, (i, t) => {
            bubble(ctx, HEAD - 0.2 + (rnd(i, 4) - 0.5) * 0.45, -0.1 + t * 0.42, 0.035 + rnd(i, 5) * 0.025, p, bell(t));
          });
        } },
      { name: 'Impact', loop: false, fps: 14, frames: 7, draw: (ctx, k, p, rnd) => splash(ctx, k, p, rnd, 0.95) },
    ],
  },
  {
    id: 'sparkBolt',
    label: 'Chispa eléctrica',
    description: 'Una chispa con un rayo quebrado detrás que cambia en cada frame. Al chocar suelta arcos.',
    defaults: { core: '#ffffff', glow: '#ffd93a', accent: '#fff6b8', particles: 5 },
    params: [TAIL(1.15)],
    lanes: [
      { name: 'Move', loop: true, fps: 16, frames: 6,
        draw(ctx, k, p, rnd) {
          const f = Math.round(k * 600);
          glowDot(ctx, HEAD - p.tail * 0.4, 0, 0.4, p.glow, 0.22 * p.intensity);
          lightning(ctx, boltPath(HEAD, 0, HEAD - p.tail, (rnd(f, 3) - 0.5) * 0.2, 0.15, 7, p.seed * 31 + f), 0.03, p, 0.95);
          lightning(ctx, boltPath(HEAD, 0, HEAD - p.tail * 0.7, (rnd(f, 4) - 0.5) * 0.5, 0.12, 5, p.seed * 57 + f), 0.016, p, 0.6);
          for (let i = 0; i < 3; i++) {
            const a = rnd(i + f, 5) * TAU;
            lightning(ctx, boltPath(HEAD, 0, HEAD + Math.cos(a) * 0.3, Math.sin(a) * 0.3, 0.06, 3, p.seed * 91 + i + f), 0.014, p, 0.8);
          }
          flare(ctx, HEAD, 0, 0.42, p.core, 0.85, 4, k * (TAU / 4), 0.12);
          core(ctx, HEAD, 0, 0.15, p);
          emit(p.particles, k, rnd, (i, t) => {
            sparkShape(ctx, HEAD - t * p.tail, (rnd(i, 2) - 0.5) * 0.5, 0.018 + rnd(i, 3) * 0.018, p, bell(t), t * 5);
          });
        } },
      { name: 'Impact', loop: false, fps: 18, frames: 6, draw: (ctx, k, p, rnd) => arcBurst(ctx, k, p, rnd, 0.95) },
    ],
  },
  {
    id: 'arcaneMissile',
    label: 'Misil arcano',
    description: 'Una estrella de cuatro puntas con dos cintas de luz trenzadas detrás. Al chocar: destello mágico.',
    defaults: { core: '#ffffff', glow: '#a86bff', accent: '#ffb8f4', particles: 8 },
    params: [TAIL(1.1)],
    lanes: [
      { name: 'Move', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          streakUnder(ctx, HEAD, -0.85, 0.3, p);
          for (let j = 0; j < 2; j++) {
            for (let s = 22; s >= 0; s--) {
              const t = s / 22;
              const ph = t * TAU * 1.4 - k * TAU + j * Math.PI;
              const depth = 0.5 + 0.5 * Math.cos(ph);
              glowDot(ctx, HEAD - 0.08 - t * p.tail, Math.sin(ph) * 0.21 * Math.min(1, t * 3), 0.14 * (1 - t * 0.6) * (0.7 + 0.3 * depth),
                j ? p.accent : p.glow, (1 - t * t) * (0.55 + 0.4 * depth) * p.intensity);
            }
          }
          ctx.save();
          ctx.translate(HEAD, 0);
          ctx.rotate(k * Math.PI);                      // an ellipse: half a turn closes the loop
          ring(ctx, 0, 0, 0.26, 0.02, p.accent, 0.75 * p.intensity, 0.36);
          ctx.restore();
          flare(ctx, HEAD, 0, 0.5, p.core, 0.95, 4, 0, 0.17);
          core(ctx, HEAD, 0, 0.14 * (1 + 0.08 * Math.sin(k * TAU)), p);
          emit(p.particles, k, rnd, (i, t) => {
            sparkShape(ctx, HEAD - 0.1 - t * p.tail * 0.9, (rnd(i, 2) - 0.5) * 0.55, 0.018 + rnd(i, 3) * 0.02, p, bell(t) * 0.9, t * 5);
          });
        } },
      { name: 'Impact', loop: false, fps: 16, frames: 7,
        draw(ctx, k, p, rnd) {
          burst(ctx, k, p, rnd, 0.9);
          flare(ctx, 0, 0, 0.9 * (1 - k * 0.4), p.core, clamp01(1 - k * 1.4), 4, k * 0.5, 0.12);
        } },
    ],
  },
  {
    id: 'shadowOrb',
    label: 'Orbe sombrío',
    description: 'Un núcleo oscuro con el borde encendido y jirones de humo detrás. Al chocar se deshace en sombra.',
    defaults: { core: '#f1dcff', glow: '#8a3cff', accent: '#d49bff', particles: 5 },
    params: [],
    lanes: [
      { name: 'Move', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          const dark = shade(p.glow, 0.16), SH = 0.3;
          emit(8, k, rnd, (i, t) => {
            puff(ctx, SH - 0.15 - t * 0.85, (rnd(i, 2) - 0.5) * 0.5 * t + loopNoise(k, 30 + i) * 0.03, 0.32 * (1 - t * 0.55), shade(p.glow, 0.3), 0.6 * (1 - t));
          });
          glowDot(ctx, SH, 0, 0.64, p.glow, 0.75 * p.intensity);
          puff(ctx, SH, 0, 0.44, dark, 1);
          solid(ctx, () => { ctx.fillStyle = rgba(dark, 1); ctx.beginPath(); ctx.arc(SH, 0, 0.29, 0, TAU); ctx.fill(); });
          ring(ctx, SH, 0, 0.32 + 0.014 * Math.sin(k * TAU), 0.036, p.glow, 0.95 * p.intensity);
          crescent(ctx, SH, 0, 0.34, 0.4 + k * TAU, 1.9 + k * TAU, 0.07, p.core, 0.9);
          emit(p.particles, k, rnd, (i, t) => {
            sparkShape(ctx, SH - 0.2 - t * 0.7, (rnd(i, 2) - 0.5) * 0.5, 0.018 + rnd(i, 3) * 0.02, p, bell(t) * 0.9, t * 5);
          });
        } },
      { name: 'Impact', loop: false, fps: 14, frames: 7,
        draw(ctx, k, p, rnd) {
          const out = easeOut(k), fade = 1 - easeIn(k);
          for (let i = 0; i < 9; i++) {
            const a = (i / 9) * TAU + rnd(i, 2) * 0.5;
            const d = (0.08 + 0.5 * out * (0.5 + rnd(i, 3) * 0.5));
            puff(ctx, Math.cos(a) * d, Math.sin(a) * d, 0.26 * (1 - k * 0.35), shade(p.glow, 0.22), 0.7 * fade);
          }
          shockRing(ctx, 0, 0, 0.2 + 0.6 * out, 0.14, p.glow, fade * 0.8 * p.intensity);
          glowDot(ctx, 0, 0, 0.45 * (1 - k * 0.5), p.core, clamp01(1 - k * 2));
          for (let i = 0; i < p.particles + 4; i++) {
            const a = rnd(i, 1) * TAU;
            const d = 0.15 + (0.4 + rnd(i, 4) * 0.3) * out;
            sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d, 0.022 + rnd(i, 5) * 0.02, p, fade, a);
          }
        } },
    ],
  },
  {
    id: 'rock',
    label: 'Roca',
    description: 'Un pedrusco que gira en el aire soltando polvo. Sin brillo: opaco. Al chocar: cascotes y polvareda.',
    defaults: { core: '#efe3cc', glow: '#8c7862', accent: '#c9b596', particles: 0 },
    params: [],
    lanes: [
      { name: 'Move', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          const dust = mix(p.accent, p.glow, 0.45);
          emit(6, k, rnd, (i, t) => {
            puff(ctx, 0.05 - t * 0.78, (rnd(i, 2) - 0.5) * 0.4, 0.1 + 0.12 * t, dust, 0.42 * (1 - t));
          });
          for (let j = 0; j < 2; j++) {
            chunk(ctx, -0.42 - j * 0.2, (j ? -0.26 : 0.24) + 0.03 * Math.sin(k * TAU + j), 0.085 - j * 0.02, k * TAU * (j ? 1 : -1), p, 1, p.seed + 40 + j);
          }
          chunk(ctx, 0.22, 0, 0.44, -k * TAU, p, 1, p.seed);
        } },
      { name: 'Impact', loop: false, fps: 14, frames: 7, draw: (ctx, k, p, rnd) => debris(ctx, k, p, rnd, 0.95) },
    ],
  },
  {
    id: 'needle',
    label: 'Aguja / lanza de luz',
    description: 'Un trazo fino y muy rápido con la punta encendida. Para lanzas, flechas y perdigones.',
    defaults: { core: '#ffffff', glow: '#9dff5a', accent: '#f4ffb0', particles: 4 },
    params: [],
    lanes: [
      { name: 'Move', loop: true, fps: 14, frames: 6,
        draw(ctx, k, p, rnd) {
          const pulse = 1 + 0.12 * Math.sin(k * TAU);
          streak(ctx, 0.68, 0, -0.92, 0, 0.34 * pulse, p.glow, 0.5 * p.intensity);
          streak(ctx, 0.7, 0, -0.8, 0, 0.13, p.core, 1);
          flare(ctx, 0.66, 0, 0.26 * pulse, p.core, 0.9, 4, 0, 0.16);
          emit(p.particles, k, rnd, (i, t) => {
            sparkShape(ctx, 0.5 - t * 1.3, (rnd(i, 2) - 0.5) * 0.22, 0.016 + rnd(i, 3) * 0.016, p, (1 - t) * 0.9, 0);
          });
        } },
      { name: 'Impact', loop: false, fps: 18, frames: 5, draw: (ctx, k, p, rnd) => hitSpark(ctx, k, { ...p, particles: Math.max(6, p.particles) }, rnd, 0.9) },
    ],
  },
  {
    id: 'blade',
    label: 'Cuchilla giratoria',
    description: 'Dos filos curvos girando sobre sí mismos. Para discos, guadañas lanzadas y sierras.',
    defaults: { core: '#ffffff', glow: '#ff5a7a', accent: '#ffd0d8', particles: 0 },
    params: [],
    lanes: [
      { name: 'Move', loop: true, fps: 16, frames: 8,
        draw(ctx, k, p) {
          const a = -k * Math.PI;                       // two blades: half a turn closes the loop
          glowDot(ctx, 0, 0, 0.8, p.glow, 0.28 * p.intensity);
          for (let j = 0; j < 2; j++) {
            const lead = a + j * Math.PI;
            crescent(ctx, 0, 0, 0.8, lead + 2.3, lead, 0.34, p.glow, 0.8 * p.intensity, 0.05);
            crescent(ctx, 0, 0, 0.78, lead + 1.2, lead, 0.2, p.core, 1, 0.3);
          }
          ring(ctx, 0, 0, 0.16, 0.03, p.accent, 0.8);
          core(ctx, 0, 0, 0.09, p);
        } },
      { name: 'Impact', loop: false, fps: 18, frames: 6,
        draw(ctx, k, p, rnd) {
          const fade = 1 - easeIn(k);
          crescent(ctx, 0, 0, 0.3 + 0.5 * easeOut(k), -1.2, 1.2, 0.2 * (1 - k * 0.6), p.core, fade, 0.1);
          crescent(ctx, 0, 0, 0.3 + 0.5 * easeOut(k), Math.PI - 1.2, Math.PI + 1.2, 0.2 * (1 - k * 0.6), p.core, fade, 0.1);
          hitSpark(ctx, k, { ...p, particles: 6 }, rnd, 0.8);
        } },
    ],
  },
  {
    id: 'heavyOrb',
    label: 'Orbe pesado',
    description: 'Una esfera grande y lenta con remolino dentro y dos anillos orbitando. Para orbes de jefe.',
    defaults: { core: '#fff0f0', glow: '#ff3d6e', accent: '#ffc0a0', particles: 6 },
    params: [],
    lanes: [
      { name: 'Move', loop: true, fps: 10, frames: 10,
        draw(ctx, k, p, rnd) {
          const r = 0.44 * (1 + 0.03 * Math.sin(k * TAU));
          glowDot(ctx, 0, 0, 0.93, p.glow, 0.4 * p.intensity);
          solid(ctx, () => {
            const g = ctx.createRadialGradient(-0.12, 0.14, 0, 0, 0, r);
            g.addColorStop(0, rgba(mix(p.core, p.glow, 0.3), 1));
            g.addColorStop(0.55, rgba(p.glow, 1));
            g.addColorStop(1, rgba(shade(p.glow, 0.4), 1));
            ctx.fillStyle = g;
            ctx.beginPath(); ctx.arc(0, 0, r, 0, TAU); ctx.fill();
          });
          for (let j = 0; j < 3; j++) {
            const a = k * TAU + (j * TAU) / 3;
            crescent(ctx, 0, 0, r * (0.82 - j * 0.14), a, a + 2.2, r * 0.22, p.core, 0.4, 0);
          }
          ring(ctx, 0, 0, r, 0.022, p.accent, 0.7);
          [[0.55, 1], [-0.55, -1]].forEach(([tilt, dir], j) => {
            ctx.save();
            ctx.rotate(tilt);
            ring(ctx, 0, 0, 0.7, 0.018, p.accent, 0.55 * p.intensity, 0.3);
            const a = dir * k * TAU + j * 2;
            sparkShape(ctx, Math.cos(a) * 0.7, Math.sin(a) * 0.7 * 0.3, 0.05, p, 1, a);
            ctx.restore();
          });
          emit(p.particles, k, rnd, (i, t) => {
            const a = rnd(i, 2) * TAU;
            sparkShape(ctx, Math.cos(a) * (0.5 + t * 0.3), Math.sin(a) * (0.5 + t * 0.3), 0.02 + rnd(i, 3) * 0.015, p, bell(t) * 0.8, a);
          });
        } },
      { name: 'Impact', loop: false, fps: 14, frames: 8,
        draw(ctx, k, p, rnd) {
          burst(ctx, k, { ...p, particles: p.particles + 8 }, rnd, 0.92);
          shockRing(ctx, 0, 0, 0.2 + 0.56 * easeOut(k), 0.22, p.glow, (1 - easeIn(k)) * 0.6 * p.intensity);
        } },
    ],
  },
  {
    id: 'wisp',
    label: 'Fuego fatuo',
    description: 'Una lucecita con cola ondulante, como un espíritu. Para proyectiles que persiguen y almas.',
    defaults: { core: '#ffffff', glow: '#3fe8c0', accent: '#d0fff0', particles: 6 },
    params: [],
    lanes: [
      { name: 'Move', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          const hx = 0.36;
          streakUnder(ctx, hx, -0.7, 0.34, p);
          for (let s = 20; s >= 1; s--) {
            const t = s / 20;
            glowDot(ctx, hx - t * 1.15, Math.sin(t * TAU * 1.15 - k * TAU) * 0.17 * t, 0.2 * (1 - t * 0.8),
              mix(p.core, p.glow, Math.min(1, t * 2.5)), 0.7 * (1 - t) * p.intensity);
          }
          core(ctx, hx, 0, 0.19 * (1 + 0.07 * Math.sin(k * TAU)), p);
          emit(p.particles, k, rnd, (i, t) => {
            sparkShape(ctx, hx - 0.2 - rnd(i, 2) * 0.8, (rnd(i, 4) - 0.5) * 0.3 + t * 0.3, 0.018 + rnd(i, 3) * 0.018, p, bell(t) * 0.9, 0);
          });
        } },
      { name: 'Impact', loop: false, fps: 14, frames: 7,
        draw(ctx, k, p, rnd) {
          const out = easeOut(k), fade = 1 - easeIn(k);
          core(ctx, 0, 0, 0.3 * (1 - k * 0.8), p, fade);
          shockRing(ctx, 0, 0, 0.15 + 0.6 * out, 0.2, p.glow, fade * 0.7 * p.intensity);
          for (let i = 0; i < p.particles + 6; i++) {
            const a = rnd(i, 1) * TAU;
            const d = 0.1 + (0.3 + rnd(i, 2) * 0.3) * out;
            sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d + 0.25 * k, 0.03 + rnd(i, 3) * 0.03, p, fade, a);
          }
        } },
    ],
  },
].map((e) => ({ family: 'projectile', kind: 'projectile', ...e }));

// ============================================================ bursts (one-shot)

/** Where the ground is for the effects that STAND on it (flames, spikes, a bolt from the sky). */
const GROUND = -0.55;

/** A "+" of light (healing). */
function plus(ctx, x, y, r, p, alpha) {
  if (r <= 0.001 || alpha <= 0.003) return;
  glowDot(ctx, x, y, r * 2.2, p.glow, alpha * 0.6 * p.intensity);
  ctx.fillStyle = rgba(p.core, alpha);
  ctx.fillRect(x - r, y - r * 0.3, r * 2, r * 0.6);
  ctx.fillRect(x - r * 0.3, y - r, r * 0.6, r * 2);
}

const BURSTS = [
  {
    id: 'hitSpark',
    label: 'Chispazo de golpe',
    description: 'Un destello en cruz con trazos que salen despedidos. Pequeño y seco: para cada golpe que acierta.',
    defaults: { core: '#ffffff', glow: '#ffc65a', accent: '#fff1c9', particles: 8 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 18, frames: 5, draw: (ctx, k, p, rnd) => hitSpark(ctx, k, p, rnd, 1) },
    ],
  },
  {
    id: 'explosion',
    label: 'Explosión',
    description: 'Fogonazo blanco, bolas de fuego que se enfrían, humo oscuro y ascuas. Para bombas y muertes.',
    defaults: { core: '#fff4c2', glow: '#ff7a1a', accent: '#ffd04a', particles: 12 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 9, draw: (ctx, k, p, rnd) => explosion(ctx, k, p, rnd, 1.05) },
    ],
  },
  {
    id: 'iceShatter',
    label: 'Estallido de hielo',
    description: 'Un destello frío y esquirlas que salen girando. Para el estallido helado y romper algo congelado.',
    defaults: { core: '#ffffff', glow: '#58bfff', accent: '#d6f6ff', particles: 10 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 8, draw: (ctx, k, p, rnd) => shatter(ctx, k, p, rnd, 1.02) },
    ],
  },
  {
    id: 'poisonSplash',
    label: 'Salpicadura',
    description: 'Una masa líquida que revienta en gotas que caen. Veneno por defecto; en azul es agua.',
    defaults: { core: '#f4ffc8', glow: '#6fcf2a', accent: '#c9ff66', particles: 6 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 8, draw: (ctx, k, p, rnd) => splash(ctx, k, p, rnd, 1) },
    ],
  },
  {
    id: 'lightningStrike',
    label: 'Rayo caído',
    description: 'Un rayo que cae de arriba al suelo (parte baja del frame), con ramas y fogonazo en el punto de impacto.',
    defaults: { core: '#ffffff', glow: '#ffd93a', accent: '#fff6b8', particles: 8 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 16, frames: 7,
        draw(ctx, k, p, rnd) {
          const out = easeOut(k), fade = 1 - easeIn(k);
          const f = Math.round(k * 600);
          const strike = clamp01(1.15 - k * 1.3);
          ctx.save();
          ctx.scale(0.4, 1);
          glowDot(ctx, 0, 0.15, 0.9, p.glow, 0.4 * strike * p.intensity);
          ctx.restore();
          const pts = boltPath((rnd(f, 3) - 0.5) * 0.4, 0.84, 0, GROUND, 0.17, 8, p.seed * 37 + f);
          [[3, 1], [5, -1]].forEach(([at, side], j) => {
            const [bx, by] = pts[at];
            lightning(ctx, boltPath(bx, by, bx + side * (0.3 + rnd(f + j, 4) * 0.2), by - 0.32, 0.08, 4, p.seed * 53 + f + j), 0.016 * (1 - k * 0.4), p, strike * 0.8);
          });
          lightning(ctx, pts, 0.036 * (1 - k * 0.45), p, strike);
          shockRing(ctx, 0, GROUND, 0.15 + 0.62 * out, 0.16, p.glow, fade * 0.8 * p.intensity, 0.3);
          glowDot(ctx, 0, GROUND, 0.42 * (1 - k * 0.4), p.core, clamp01(1 - k * 1.5));
          for (let i = 0; i < p.particles; i++) {
            const a = (0.12 + rnd(i, 1) * 0.76) * Math.PI;
            const sp = 0.45 + rnd(i, 2) * 0.4;
            sparkShape(ctx, Math.cos(a) * sp * out, GROUND + Math.sin(a) * sp * out - 0.3 * k * k, (0.022 + rnd(i, 3) * 0.022) * (1 - k * 0.4), p, fade, a);
          }
        } },
    ],
  },
  {
    id: 'dustBurst',
    label: 'Polvareda de caída',
    description: 'Polvo que sale a los dos lados a ras de suelo y piedrecitas que saltan. Para aterrizajes y mazazos.',
    defaults: { core: '#fff6e0', glow: '#a8957a', accent: '#e2d5bd', particles: 5 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 8,
        draw(ctx, k, p, rnd) {
          const out = easeOut(k), fade = 1 - easeIn(k);
          const dust = mix(p.accent, p.glow, 0.4);
          shockRing(ctx, 0, -0.14, 0.2 + 0.62 * out, 0.12, p.core, 0.4 * fade, 0.22);
          for (let i = 0; i < 14; i++) {
            const side = i % 2 ? 1 : -1;
            const x = side * (0.12 + (0.14 + 0.5 * rnd(i, 2)) * out);
            const r = (0.1 + 0.11 * out) * (0.7 + rnd(i, 3) * 0.6);
            puff(ctx, x, -0.12 + rnd(i, 4) * 0.1 + 0.22 * k * rnd(i, 5), r, i % 3 ? dust : mix(dust, p.core, 0.4), 0.62 * fade);
          }
          for (let i = 0; i < p.particles; i++) {
            const a = (0.15 + rnd(i, 1) * 0.7) * Math.PI;
            const sp = 0.4 + rnd(i, 6) * 0.35;
            chunk(ctx, Math.cos(a) * sp * out, -0.1 + Math.sin(a) * sp * out - 0.5 * k * k, 0.03 + rnd(i, 7) * 0.025, k * 6 * (rnd(i, 8) - 0.5),
              { ...p, glow: shade(p.glow, 0.75) }, clamp01(fade * 1.5), p.seed + i * 7);
          }
        } },
    ],
  },
  {
    id: 'healBurst',
    label: 'Cura',
    description: 'Una columna suave de luz, un anillo que sube y cruces que flotan hacia arriba. Para curas y robo de vida.',
    defaults: { core: '#ffffff', glow: '#3dea7c', accent: '#d2ffe0', particles: 7 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 12, frames: 9,
        draw(ctx, k, p, rnd) {
          const out = easeOut(k), fade = 1 - easeIn(k);
          ctx.save();
          ctx.scale(0.55, 1);
          glowDot(ctx, 0, 0, 0.92, p.glow, 0.42 * bell(k * 0.9 + 0.1) * p.intensity);
          ctx.restore();
          ring(ctx, 0, -0.55 + 1.05 * out, 0.5 * (1 - 0.35 * k), 0.035, p.glow, fade * p.intensity, 0.3);
          ring(ctx, 0, -0.55 + 0.75 * out, 0.62 * (1 - 0.2 * k), 0.02, p.accent, fade * 0.7 * p.intensity, 0.3);
          glowDot(ctx, 0, -0.3, 0.45, p.core, 0.55 * clamp01(1 - k * 2));
          const n = Math.max(4, p.particles);
          for (let i = 0; i < n; i++) {
            const delay = rnd(i, 1) * 0.35;
            const t = clamp01((k - delay) / (1 - delay));
            plus(ctx, (i / (n - 1) - 0.5) * 1.1 + (rnd(i, 2) - 0.5) * 0.12, -0.5 + (0.6 + rnd(i, 3) * 0.6) * easeOut(t), 0.055 + rnd(i, 4) * 0.045, p, pop(t, 0.2));
          }
        } },
    ],
  },
  {
    id: 'shieldBreak',
    label: 'Escudo roto',
    description: 'El borde de una burbuja se parte en trozos curvos que salen despedidos. Para romper una guardia.',
    defaults: { core: '#ffffff', glow: '#5b8cff', accent: '#dbe6ff', particles: 8 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 7,
        draw(ctx, k, p, rnd) {
          const out = easeOut(k), fade = 1 - easeIn(k);
          const r = 0.5;
          glowDot(ctx, 0, 0, 0.75, p.glow, 0.4 * fade * p.intensity);
          ring(ctx, 0, 0, r, 0.05, p.core, clamp01(1 - k * 4));
          const n = 7;
          ctx.save();
          ctx.lineCap = 'round';
          for (let i = 0; i < n; i++) {
            const am = (i / n) * TAU + 0.3;
            const span = (TAU / n) * (0.55 + rnd(i, 2) * 0.3);
            const d = 0.3 * out * (0.55 + rnd(i, 3) * 0.6);
            ctx.save();
            ctx.translate(Math.cos(am) * d, Math.sin(am) * d - 0.12 * k * k);
            ctx.translate(Math.cos(am) * r, Math.sin(am) * r);
            ctx.rotate((rnd(i, 4) - 0.5) * 2.2 * k);
            ctx.translate(-Math.cos(am) * r, -Math.sin(am) * r);
            solid(ctx, () => {                              // a darker rim under the light: reads on ice too
              ctx.lineCap = 'round';
              ctx.lineWidth = 0.075 * (1 - k * 0.4);
              ctx.strokeStyle = rgba(shade(p.glow, 0.7), 0.85 * fade);
              ctx.beginPath(); ctx.arc(0, 0, r, am - span / 2, am + span / 2); ctx.stroke();
            });
            [[0.1, p.glow, 0.45 * p.intensity], [0.035, p.core, 1]].forEach(([w, color, a]) => {
              ctx.lineWidth = w * (1 - k * 0.4);
              ctx.strokeStyle = rgba(color, a * fade);
              ctx.beginPath(); ctx.arc(0, 0, r, am - span / 2, am + span / 2); ctx.stroke();
            });
            ctx.restore();
          }
          ctx.restore();
          for (let i = 0; i < p.particles; i++) {
            const a = rnd(i, 1) * TAU;
            const d = 0.45 + 0.4 * out * rnd(i, 5);
            sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d, 0.02 + rnd(i, 6) * 0.02, p, fade, a);
          }
        } },
    ],
  },
  {
    id: 'slash',
    label: 'Tajo',
    description: 'Un arco afilado que barre de arriba abajo por delante (hacia la derecha). Para golpes cuerpo a cuerpo.',
    defaults: { core: '#ffffff', glow: '#ffcf6a', accent: '#fff3d0', particles: 6 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 18, frames: 6,
        draw(ctx, k, p, rnd) {
          const fade = 1 - easeIn(k);
          const cx = -0.3, r = 0.95, top = 1.05;
          const lead = top - 2.1 * easeOut(Math.min(1, k * 1.7));
          const tail = top - 2.1 * easeIn(clamp01(k * 1.3 - 0.3));
          const thick = 0.36 * (1 - k * 0.35);
          solid(ctx, () => crescent(ctx, cx, 0, r + 0.02, tail, lead, thick * 1.25, shade(p.glow, 0.6), 0.5 * fade, 0.1));
          crescent(ctx, cx, 0, r + 0.03, tail, lead, thick * 1.5, p.glow, 0.7 * fade * p.intensity, 0.1);
          crescent(ctx, cx, 0, r, tail, lead, thick, p.core, fade, 0.25);
          glowDot(ctx, cx + Math.cos(lead) * r, Math.sin(lead) * r, 0.22, p.core, 0.8 * clamp01(1 - k * 1.5));
          for (let i = 0; i < p.particles; i++) {
            const a = top - 2.1 * rnd(i, 1);
            if (a < lead) continue;                       // only where the edge has already passed
            const d = r + (rnd(i, 2) - 0.2) * 0.12 + k * 0.12;
            sparkShape(ctx, cx + Math.cos(a) * d, Math.sin(a) * d, 0.02 + rnd(i, 3) * 0.02, p, fade, a);
          }
        } },
    ],
  },
].map((e) => ({ family: 'burst', kind: 'fx', ...e }));

// ============================================================ charges, auras, states (loops)

/** Arms of light spiralling into the centre. `spin` turns them; a turn of TAU / arms closes a loop. */
function spiralArms(ctx, p, arms, spin, reach, alpha) {
  for (let j = 0; j < arms; j++) {
    for (let s = 16; s >= 0; s--) {
      const t = s / 16;
      const a = spin + (j * TAU) / arms + t * 2.4;
      const r = 0.1 + reach * t;
      glowDot(ctx, Math.cos(a) * r, Math.sin(a) * r, 0.14 * (1 - t * 0.6), t < 0.3 ? p.accent : p.glow, alpha * (1 - t * 0.85) * 0.6 * p.intensity);
    }
  }
}

/** Streaks of light rising around a body. `age(i, t)` lets a one-shot drive them instead of the loop. */
function risers(ctx, k, p, rnd, alpha, oneShot = false) {
  const n = Math.max(4, p.particles);
  for (let i = 0; i < n; i++) {
    const t = oneShot ? clamp01(k * (0.8 + rnd(i, 1) * 0.5)) : (rnd(i, 1) + k) % 1;
    const x = (i / (n - 1) - 0.5) * 1.3 + (rnd(i, 2) - 0.5) * 0.1;
    const y = -0.62 + t * 1.05;
    const len = 0.4 + rnd(i, 3) * 0.3;
    const a = (oneShot ? 1 - t * 0.5 : bell(t)) * alpha;
    sliver(ctx, x, y - len * 0.45, x, y + len * 0.45, 0.11, p.glow, a * 0.6 * p.intensity);
    sliver(ctx, x, y - len * 0.4, x, y + len * 0.4, 0.04, i % 2 ? p.accent : p.core, a);
  }
}

/** A protective bubble: light gathers at the rim, a highlight up-left and a sheen that travels round. */
function bubbleShield(ctx, p, r, alpha, sheen) {
  if (r <= 0.01 || alpha <= 0.003) return;
  const g = ctx.createRadialGradient(0, 0, r * 0.3, 0, 0, r);
  g.addColorStop(0, rgba(p.glow, 0));
  g.addColorStop(0.75, rgba(p.glow, 0.2 * alpha * p.intensity));
  g.addColorStop(1, rgba(p.glow, 0.55 * alpha * p.intensity));
  ctx.fillStyle = g;
  ctx.beginPath(); ctx.arc(0, 0, r, 0, TAU); ctx.fill();
  solid(ctx, () => {
    ctx.lineWidth = 0.045;
    ctx.strokeStyle = rgba(shade(p.glow, 0.7), 0.55 * alpha);
    ctx.beginPath(); ctx.arc(0, 0, r, 0, TAU); ctx.stroke();
  });
  ring(ctx, 0, 0, r, 0.03, p.glow, alpha * p.intensity);
  ring(ctx, 0, 0, r - 0.012, 0.012, p.core, 0.8 * alpha);
  crescent(ctx, 0, 0, r * 0.86, 1.7, 2.8, 0.08, p.core, 0.75 * alpha, 0.2);
  crescent(ctx, 0, 0, r * 0.94, sheen, sheen + 1, 0.05, p.accent, 0.7 * alpha, 0);
}

/** A row of flame tongues standing on `baseY`, `width` wide and up to `height` tall, licking in a loop. */
function flamesRow(ctx, k, p, rnd, n, baseY, width, height, alpha = 1) {
  ctx.save();
  ctx.scale(1, 0.6);
  glowDot(ctx, 0, (baseY + height * 0.3) / 0.6, width * 0.75, p.glow, 0.32 * alpha * p.intensity);
  ctx.restore();
  for (let pass = 0; pass < 2; pass++) {               // the odd tongues behind, the even ones in front
    for (let i = pass ? 0 : 1; i < n; i += 2) {
      const u = n > 1 ? i / (n - 1) - 0.5 : 0;
      const edge = 1 - Math.abs(u) * 0.9;
      const h = height * (0.55 + 0.45 * rnd(i, 3)) * (0.75 + 0.25 * loopNoise(k, 40 + i)) * edge;
      flame(ctx, u * width + (rnd(i, 2) - 0.5) * 0.06, baseY, (width / n) * 1.9, h, loopNoise(k + 0.2, 60 + i) * 0.12, p, 0.9 * alpha);
    }
  }
  emit(p.particles, k, rnd, (i, t) => {
    sparkShape(ctx, (rnd(i, 2) - 0.5) * width + loopNoise(t, i) * 0.04, baseY + 0.15 + t * Math.min(height * 0.8, 0.72 - baseY), 0.02 + rnd(i, 3) * 0.02, p, bell(t) * alpha, t * 4);
  });
}

/** A five-point star, OPAQUE with an outline (it has to read over anything), over its own glow. */
function star5(ctx, x, y, r, angle, p, alpha = 1) {
  if (r <= 0.001 || alpha <= 0.003) return;
  glowDot(ctx, x, y, r * 1.9, p.glow, 0.55 * alpha * p.intensity);
  solid(ctx, () => {
    ctx.translate(x, y);
    ctx.rotate(angle);
    ctx.beginPath();
    for (let i = 0; i < 10; i++) {
      const rr = i % 2 ? r * 0.45 : r;
      const a = (i / 10) * TAU + Math.PI / 2;
      if (i) ctx.lineTo(Math.cos(a) * rr, Math.sin(a) * rr); else ctx.moveTo(Math.cos(a) * rr, Math.sin(a) * rr);
    }
    ctx.closePath();
    const g = ctx.createRadialGradient(0, 0, 0, 0, 0, r);
    g.addColorStop(0, rgba(p.core, alpha));
    g.addColorStop(1, rgba(p.glow, alpha));
    ctx.fillStyle = g;
    ctx.fill();
    ctx.lineJoin = 'round';
    ctx.lineWidth = r * 0.14;
    ctx.strokeStyle = rgba(shade(p.glow, 0.55), alpha);
    ctx.stroke();
  });
}

/** A target mark: ring, four ticks pointing in, arcs between them and a pulsing centre. */
function reticle(ctx, p, r, alpha, rot, pulse) {
  if (alpha <= 0.003) return;
  solid(ctx, () => {
    ctx.lineWidth = 0.07;
    ctx.strokeStyle = rgba(shade(p.glow, 0.55), 0.4 * alpha);
    ctx.beginPath(); ctx.arc(0, 0, r, 0, TAU); ctx.stroke();
  });
  glowDot(ctx, 0, 0, r * 1.3, p.glow, 0.22 * alpha * p.intensity);
  ring(ctx, 0, 0, r, 0.035, p.glow, alpha * p.intensity);
  ctx.save();
  ctx.rotate(rot);
  ctx.lineCap = 'round';
  for (let j = 0; j < 4; j++) {
    ctx.rotate(TAU / 4);
    ctx.fillStyle = rgba(p.core, alpha);
    ctx.beginPath();
    ctx.moveTo(r * 0.6, 0); ctx.lineTo(r * 1.14, 0.075); ctx.lineTo(r * 1.14, -0.075);
    ctx.closePath(); ctx.fill();
    ctx.lineWidth = 0.022;
    ctx.strokeStyle = rgba(p.accent, 0.85 * alpha);
    ctx.beginPath(); ctx.arc(0, 0, r * 0.74, 0.35, TAU / 4 - 0.35); ctx.stroke();
  }
  ctx.restore();
  flare(ctx, 0, 0, 0.3 * (1 + 0.18 * pulse), p.core, alpha, 4, -rot, 0.2);
}

const FROZEN_CRYSTALS = [[-0.52, 0.5, 0.42], [-0.26, 0.82, 0.17], [0.0, 1.05, -0.03], [0.27, 0.78, -0.2], [0.5, 0.46, -0.4]];

const AURAS = [
  {
    id: 'chargeVortex',
    label: 'Carga en espiral',
    description: 'Brazos de luz que giran hacia el centro mientras el núcleo crece (Spawn), se mantiene (Loop) y revienta (End).',
    defaults: { core: '#ffffff', glow: '#7dff5a', accent: '#f4ffb0', particles: 10 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 12, frames: 9,
        draw(ctx, k, p, rnd) {
          spiralArms(ctx, p, 3, -k * 2.6, 0.72, 0.25 + 0.75 * k);
          core(ctx, 0, 0, 0.07 + 0.19 * easeOut(k), p, 0.45 + 0.55 * k);
          for (let i = 0; i < p.particles; i++) {
            const t = (rnd(i, 1) + k * 1.4) % 1;
            const a = rnd(i, 2) * TAU + t * 1.5;
            const d = 0.85 * (1 - easeIn(t)) + 0.08;
            streak(ctx, Math.cos(a) * d, Math.sin(a) * d, Math.cos(a) * (d + 0.13), Math.sin(a) * (d + 0.13), 0.04, p.core, bell(t) * (0.4 + 0.6 * k));
          }
        } },
      { name: 'Loop', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          spiralArms(ctx, p, 3, (-k * TAU) / 3, 0.72, 1);
          core(ctx, 0, 0, 0.26 * (1 + 0.1 * Math.sin(k * TAU)), p);
          emit(p.particles, k, rnd, (i, t) => {
            const a = rnd(i, 2) * TAU + t * 1.5;
            const d = 0.85 * (1 - easeIn(t)) + 0.08;
            streak(ctx, Math.cos(a) * d, Math.sin(a) * d, Math.cos(a) * (d + 0.13), Math.sin(a) * (d + 0.13), 0.04, p.core, bell(t));
          });
        } },
      { name: 'End', loop: false, fps: 16, frames: 6,
        draw(ctx, k, p, rnd) {
          burst(ctx, k, p, rnd, 0.92);
          flare(ctx, 0, 0, 0.9 * (1 - k * 0.4), p.core, clamp01(1 - k * 1.5), 6, k * 0.6, 0.1);
        } },
    ],
  },
  {
    id: 'buffAura',
    label: 'Aura de mejora',
    description: 'Un anillo a los pies y trazos de luz que suben alrededor del cuerpo. Para mejoras, furia y curas sostenidas.',
    defaults: { core: '#ffffff', glow: '#ffb92e', accent: '#fff0b8', particles: 10 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 6,
        draw(ctx, k, p, rnd) {
          const out = easeOut(k);
          ring(ctx, 0, -0.62, 0.66 * out, 0.04, p.glow, (0.5 + 0.5 * k) * p.intensity, 0.28);
          glowDot(ctx, 0, -0.5, 0.5, p.core, 0.5 * clamp01(1 - k * 1.6));
          risers(ctx, k, p, rnd, 1, true);
        } },
      { name: 'Loop', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          const pulse = 0.5 + 0.5 * Math.sin(k * TAU);
          ctx.save();
          ctx.scale(0.8, 1);
          glowDot(ctx, 0, -0.1, 0.85, p.glow, (0.2 + 0.06 * pulse) * p.intensity);
          ctx.restore();
          ring(ctx, 0, -0.62, 0.66, 0.035 + 0.01 * pulse, p.glow, (0.65 + 0.3 * pulse) * p.intensity, 0.28);
          ring(ctx, 0, -0.62, 0.66 * k, 0.02, p.core, (1 - k) * 0.7, 0.28);
          risers(ctx, k, p, rnd, 1);
        } },
      { name: 'End', loop: false, fps: 14, frames: 5,
        draw(ctx, k, p, rnd) {
          ring(ctx, 0, -0.62, 0.66 + 0.1 * k, 0.035 * (1 - k) + 0.008, p.glow, (1 - k * k) * p.intensity, 0.28);
          risers(ctx, 0.3 + k * 0.7, p, rnd, 1 - k * k, true);
        } },
    ],
  },
  {
    id: 'shield',
    label: 'Burbuja de escudo',
    description: 'Una esfera protectora con el borde encendido y un reflejo que la recorre. Para la guardia y la invulnerabilidad.',
    defaults: { core: '#ffffff', glow: '#5b8cff', accent: '#dbe6ff', particles: 5 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 6,
        draw(ctx, k, p) {
          const out = easeOut(k);
          bubbleShield(ctx, p, 0.76 * (0.35 + 0.65 * out), 0.4 + 0.6 * k, 2.5 - k * 2);
          ring(ctx, 0, 0, 0.76 * out, 0.06 * (1 - k), p.core, 1 - k);
        } },
      { name: 'Loop', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          bubbleShield(ctx, p, 0.76 * (1 + 0.015 * Math.sin(k * TAU)), 1, -k * TAU);
          for (let i = 0; i < p.particles; i++) {
            const a = rnd(i, 1) * TAU, d = 0.3 + rnd(i, 2) * 0.38;
            const tw = Math.max(0, Math.sin(k * TAU + rnd(i, 3) * TAU));
            flare(ctx, Math.cos(a) * d, Math.sin(a) * d, 0.09, p.core, tw * 0.9, 4, 0, 0.18);
          }
        } },
      { name: 'End', loop: false, fps: 14, frames: 5,
        draw(ctx, k, p) {
          bubbleShield(ctx, p, 0.76 + 0.12 * easeOut(k), 1 - k, 1);
        } },
    ],
  },
  {
    id: 'burning',
    label: 'Ardiendo',
    description: 'Llamas que suben desde la base del frame con ascuas. Estado de quemado sobre un cuerpo, o una hoguera.',
    defaults: { core: '#fff4c2', glow: '#ff7a1a', accent: '#ffd04a', particles: 8 },
    params: [
      { key: 'spread', label: 'Ancho de las llamas', type: 'float', min: 0.5, max: 1.5, step: 0.05, default: 1.15 },
    ],
    lanes: [
      { name: 'Loop', loop: true, fps: 12, frames: 8,
        draw: (ctx, k, p, rnd) => flamesRow(ctx, k, p, rnd, 6, -0.66, p.spread, 1.55) },
    ],
  },
  {
    id: 'frozen',
    label: 'Congelado',
    description: 'Cristales de hielo que brotan de la base, destellos en las puntas y copos cayendo. Estado de frío / ralentizado.',
    defaults: { core: '#ffffff', glow: '#58bfff', accent: '#d6f6ff', particles: 8 },
    params: [],
    lanes: [
      { name: 'Loop', loop: true, fps: 10, frames: 8,
        draw(ctx, k, p, rnd) {
          const base = -0.72;
          glowDot(ctx, 0, -0.25, 0.85, p.glow, (0.26 + 0.05 * Math.sin(k * TAU)) * p.intensity);
          puff(ctx, -0.35, base + 0.05, 0.3, p.accent, 0.22 + 0.06 * Math.sin(k * TAU));
          puff(ctx, 0.35, base + 0.05, 0.3, p.accent, 0.22 + 0.06 * Math.sin(k * TAU + 2));
          const tips = [];
          [1, 3, 0, 4, 2].forEach((j) => {               // the tall middle one last = in front
            const [x, len, lean] = FROZEN_CRYSTALS[j];
            const a = Math.PI / 2 + lean;
            shard(ctx, x + Math.cos(a) * len * 0.5, base + Math.sin(a) * len * 0.5, len, len * 0.3, a, p, 1, j % 2 ? 0.3 : 0);
            tips[j] = [x + Math.cos(a) * len, base + Math.sin(a) * len];
          });
          const which = Math.floor(k * 5) % 5;            // the tips twinkle in turn
          flare(ctx, tips[which][0], tips[which][1], 0.24, p.core, bell(k * 5 - Math.floor(k * 5)), 4, 0, 0.14);
          emit(p.particles, k, rnd, (i, t) => {
            flare(ctx, (rnd(i, 2) - 0.5) * 1.5 + Math.sin(t * TAU + rnd(i, 4) * TAU) * 0.05, 0.82 - t * 1.25, 0.075, p.core, bell(t), 6, t * 2, 0.2);
          });
        } },
    ],
  },
  {
    id: 'poisoned',
    label: 'Envenenado',
    description: 'Burbujas que suben entre una neblina enfermiza. Estado de veneno sobre un cuerpo.',
    defaults: { core: '#f4ffc8', glow: '#6fcf2a', accent: '#c9ff66', particles: 12 },
    params: [],
    lanes: [
      { name: 'Loop', loop: true, fps: 10, frames: 8,
        draw(ctx, k, p, rnd) {
          const mist = shade(p.glow, 0.6);
          for (let i = 0; i < 4; i++) {
            puff(ctx, (i / 3 - 0.5) * 0.9 + loopNoise(k, 70 + i) * 0.05, -0.5 + loopNoise(k + 0.4, 80 + i) * 0.05, 0.3, mist, 0.2);
          }
          glowDot(ctx, 0, -0.2, 0.85, p.glow, 0.22 * p.intensity);
          emit(Math.max(4, p.particles), k, rnd, (i, t) => {
            const x = (rnd(i, 2) - 0.5) * 1.3 + Math.sin(t * TAU + rnd(i, 4) * TAU) * 0.06;
            bubble(ctx, x, -0.72 + t * 1.45, (0.07 + rnd(i, 3) * 0.07) * (0.6 + 0.4 * t), p, clamp01(Math.min(t * 5, (1 - t) * 6)));
          });
        } },
    ],
  },
  {
    id: 'stunned',
    label: 'Aturdido',
    description: 'Tres estrellas dando vueltas en un óvalo. Colócalo sobre la cabeza.',
    defaults: { core: '#fffbe0', glow: '#ffc928', accent: '#fff1a8', particles: 0 },
    params: [
      { key: 'flat', label: 'Aplastado del óvalo', type: 'float', min: 0.15, max: 0.6, step: 0.05, default: 0.32 },
    ],
    lanes: [
      { name: 'Loop', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p) {
          const R = 0.62;
          ring(ctx, 0, 0, R, 0.02, p.glow, 0.45 * p.intensity, p.flat);
          const stars = [0, 1, 2].map((j) => (k * TAU) / 3 + (j * TAU) / 3);
          stars.sort((a, b) => Math.sin(b) - Math.sin(a));       // the far ones first
          stars.forEach((a) => {
            const depth = Math.sin(a);
            for (let m = 3; m >= 1; m--) {
              const b = a - m * 0.22;
              glowDot(ctx, Math.cos(b) * R, Math.sin(b) * R * p.flat, 0.07 - m * 0.012, p.accent, (0.7 - m * 0.18) * p.intensity);
            }
            star5(ctx, Math.cos(a) * R, Math.sin(a) * R * p.flat + 0.03, 0.2 * (1 - 0.22 * depth), a * 0.6, p, 1);
          });
        } },
    ],
  },
  {
    id: 'mark',
    label: 'Marca',
    description: 'Una diana que se cierra sobre el objetivo (Spawn), gira despacio (Loop) y se suelta (End). Para la marca de oro y objetivos fijados.',
    defaults: { core: '#fffbe0', glow: '#ffb300', accent: '#ffe9a0', particles: 0 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 6,
        draw(ctx, k, p) {
          const out = easeOut(k);
          reticle(ctx, p, 0.58 + 0.2 * (1 - out), 0.35 + 0.65 * k, -1.3 * (1 - out), 1 - k);
        } },
      { name: 'Loop', loop: true, fps: 10, frames: 8,
        draw(ctx, k, p) {
          const pulse = Math.sin(k * TAU);
          reticle(ctx, p, 0.58 * (1 + 0.03 * pulse), 1, (k * TAU) / 4, pulse);
        } },
      { name: 'End', loop: false, fps: 14, frames: 5,
        draw(ctx, k, p) {
          reticle(ctx, p, 0.58 + 0.14 * easeOut(k), 1 - k, 0.5 * k, 0);
        } },
    ],
  },
].map((e) => ({ family: 'aura', kind: 'fx', ...e }));

// ============================================================ trails and motion

const TRAILS = [
  {
    id: 'dashTrail',
    label: 'Estela de dash',
    description: 'Un penacho de trazos que se estira hacia atrás (izquierda). Engánchalo al cuerpo durante una embestida.',
    defaults: { core: '#ffffff', glow: '#58c8ff', accent: '#b8f0ff', particles: 6 },
    params: [],
    lanes: [
      { name: 'Loop', loop: true, fps: 14, frames: 6,
        draw(ctx, k, p, rnd) {
          // One soft plume, widest at the body (right), with thin lines of light running inside it.
          const breathe = 1 + 0.06 * Math.sin(k * TAU);
          solid(ctx, () => sliver(ctx, 0.75, 0, -0.9, 0, 0.5, shade(p.glow, 0.45), 0.18));
          for (let s = 12; s >= 0; s--) {
            const t = s / 12;
            glowDot(ctx, 0.42 - t * 1.25, 0, 0.46 * breathe * (1 - t * 0.72), t < 0.3 ? p.accent : p.glow, 0.34 * (1 - t) * p.intensity);
          }
          emit(9, k, rnd, (i, t) => {
            const y = (rnd(i, 2) - 0.5) * 0.62;
            const len = 0.45 + rnd(i, 3) * 0.45;
            const x = 0.62 - t * 1.05;
            sliver(ctx, x, y * (1 - t * 0.5), x - len * (1 - t * 0.4), y * (1 - t * 0.6), 0.05, i % 3 ? p.core : p.accent, bell(t));
          });
          emit(p.particles, k, rnd, (i, t) => {
            sparkShape(ctx, 0.5 - t * 1.3, (rnd(i, 2) - 0.5) * 0.8 * (1 - t * 0.4), 0.02 + rnd(i, 3) * 0.02, p, bell(t) * 0.9, 0);
          });
        } },
    ],
  },
  {
    id: 'footDust',
    label: 'Polvo de pisada',
    description: 'Una nubecita que se queda atrás y se deshace. Para pasos, arranques y frenazos.',
    defaults: { core: '#fff6e0', glow: '#a8957a', accent: '#e2d5bd', particles: 0 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 6,
        draw(ctx, k, p, rnd) {
          const out = easeOut(k), fade = 1 - easeIn(k);
          const dust = mix(p.accent, p.glow, 0.4);
          for (let i = 0; i < 6; i++) {
            puff(ctx, 0.4 - (0.15 + rnd(i, 1) * 0.95) * out, -0.25 + rnd(i, 2) * 0.2 + (0.1 + rnd(i, 3) * 0.45) * k,
              (0.11 + 0.11 * out) * (0.7 + rnd(i, 4) * 0.6), i % 2 ? dust : mix(dust, p.core, 0.45), 0.7 * fade);
          }
        } },
    ],
  },
].map((e) => ({ family: 'trail', kind: 'fx', ...e }));

// ============================================================ ground and areas

/** A filled danger zone seen from above: translucent body, lit rim, ticks and a ring closing in. */
function zone(ctx, p, r, alpha, k, flat) {
  if (r <= 0.01 || alpha <= 0.003) return;
  ctx.save();
  ctx.scale(1, flat);
  const g = ctx.createRadialGradient(0, 0, 0, 0, 0, r);
  g.addColorStop(0, rgba(p.glow, 0.14 * alpha * p.intensity));
  g.addColorStop(0.8, rgba(p.glow, 0.3 * alpha * p.intensity));
  g.addColorStop(1, rgba(p.glow, 0.5 * alpha * p.intensity));
  ctx.fillStyle = g;
  ctx.beginPath(); ctx.arc(0, 0, r, 0, TAU); ctx.fill();
  ring(ctx, 0, 0, r, 0.04, p.glow, alpha * p.intensity);
  ring(ctx, 0, 0, r - 0.03, 0.012, p.core, 0.85 * alpha);
  ring(ctx, 0, 0, r * (1 - k), 0.03, p.core, k * 0.75 * alpha);
  ctx.rotate((k * TAU) / 12);
  ctx.lineCap = 'round';
  ctx.lineWidth = 0.03;
  ctx.strokeStyle = rgba(p.accent, 0.8 * alpha);
  for (let i = 0; i < 12; i++) {
    ctx.rotate(TAU / 12);
    ctx.beginPath(); ctx.moveTo(r * 0.8, 0); ctx.lineTo(r * 0.9, 0); ctx.stroke();
  }
  ctx.restore();
}

/** A summoning circle on the floor: two rings, ticks and a six-point star, turning. */
function runeCircle(ctx, p, r, alpha, rot, flat) {
  if (r <= 0.01 || alpha <= 0.003) return;
  ctx.save();
  ctx.scale(1, flat);
  glowDot(ctx, 0, 0, r * 1.05, p.glow, 0.3 * alpha * p.intensity);
  ctx.lineJoin = 'round';
  const strokes = (path) => {
    [[0.07, p.glow, 0.3 * p.intensity], [0.025, p.core, 1]].forEach(([w, color, a]) => {
      ctx.lineWidth = w;
      ctx.strokeStyle = rgba(color, a * alpha);
      ctx.beginPath(); path(); ctx.stroke();
    });
  };
  strokes(() => ctx.arc(0, 0, r, 0, TAU));
  strokes(() => ctx.arc(0, 0, r * 0.78, 0, TAU));
  ctx.rotate(rot);
  for (let t = 0; t < 2; t++) {
    strokes(() => {
      for (let i = 0; i < 3; i++) {
        const a = (i / 3) * TAU + t * (TAU / 6);
        if (i) ctx.lineTo(Math.cos(a) * r * 0.78, Math.sin(a) * r * 0.78); else ctx.moveTo(Math.cos(a) * r * 0.78, Math.sin(a) * r * 0.78);
      }
      ctx.closePath();
    });
  }
  ctx.rotate(-rot * 2);
  ctx.lineWidth = 0.03;
  ctx.strokeStyle = rgba(p.accent, 0.9 * alpha);
  for (let i = 0; i < 12; i++) {
    ctx.rotate(TAU / 12);
    ctx.beginPath(); ctx.moveTo(r * 0.83, 0); ctx.lineTo(r * 0.95, 0); ctx.stroke();
  }
  ctx.restore();
}

/** A beam along X from x0 to x1 with soft ends: `half` tall each side, alpha falling off from the axis. */
function beamBand(ctx, x0, x1, half, color, alpha, soft = 0.12) {
  if (half <= 0.001 || alpha <= 0.003) return;
  const rows = 26;
  for (let i = 0; i < rows; i++) {
    const v = (i + 0.5) / rows * 2 - 1;                  // −1..1 across the beam
    const a = alpha * (1 - v * v) ** 2;
    const row = ctx.createLinearGradient(x0, 0, x1, 0);
    if (soft > 0) {
      row.addColorStop(0, rgba(color, 0));
      row.addColorStop(soft, rgba(color, a));
      row.addColorStop(1 - soft, rgba(color, a));
      row.addColorStop(1, rgba(color, 0));
    }
    ctx.fillStyle = soft > 0 ? row : rgba(color, a);
    ctx.fillRect(x0, v * half - half / rows, x1 - x0, (half / rows) * 2);
  }
}

function beam(ctx, k, p, rnd, width, alpha) {
  const pulse = 1 + 0.12 * Math.sin(k * TAU * 2);
  if (p.ends === 'tile') {
    // A segment made to be STRETCHED along X by whoever draws the beam (a weapon's ray, a boss's
    // sweep): it fills the frame edge to edge and has no origin — only what survives stretching.
    beamBand(ctx, -1.05, 1.05, 0.42 * width * pulse, p.glow, 0.45 * alpha * p.intensity, 0);
    beamBand(ctx, -1.05, 1.05, 0.2 * width * pulse, p.glow, 0.9 * alpha * p.intensity, 0);
    beamBand(ctx, -1.05, 1.05, 0.1 * width, p.core, alpha, 0);
    emit(3, k, rnd, (i, t) => {
      ctx.save();
      ctx.translate(-1.3 + t * 2.6, 0);
      ctx.scale(1.2, 1);
      glowDot(ctx, 0, 0, 0.2 * width, p.core, 0.6 * alpha);
      ctx.restore();
    });
    return;
  }
  const x0 = -0.62, x1 = 0.92, ox = -0.55;             // the origin (a muzzle) on the left
  beamBand(ctx, x0, x1, 0.42 * width * pulse, p.glow, 0.45 * alpha * p.intensity);
  beamBand(ctx, x0, x1, 0.2 * width * pulse, p.glow, 0.9 * alpha * p.intensity);
  beamBand(ctx, x0 + 0.03, x1 - 0.03, 0.1 * width, p.core, alpha, 0.1);
  emit(4, k, rnd, (i, t) => {                           // pulses running down the beam
    ctx.save();
    ctx.translate(ox + t * 1.25, 0);
    ctx.scale(1.8, 1);
    glowDot(ctx, 0, 0, 0.2 * width, p.core, 0.7 * bell(t) * alpha);
    ctx.restore();
  });
  flare(ctx, ox, 0, 0.34 * Math.min(1.1, width) * pulse, p.core, 0.9 * alpha, 4, 0, 0.2);
  glowDot(ctx, ox, 0, 0.26 * Math.min(1.2, width), p.core, alpha);
  emit(p.particles, k, rnd, (i, t) => {
    sparkShape(ctx, ox + t * 1.3, (rnd(i, 2) - 0.5) * 0.5 * width + (rnd(i, 4) - 0.5) * 0.3 * t, 0.018 + rnd(i, 3) * 0.018, p, bell(t) * alpha, 0);
  });
}

const HAZARD_SPIKES = [[-0.74, 0.34, 0.5], [-0.5, 0.56, 0.3], [-0.24, 0.5, 0.12], [0.02, 0.74, -0.04], [0.28, 0.52, -0.15], [0.52, 0.6, -0.3], [0.76, 0.32, -0.5]];

/** The row of ice spikes of the frozen-ground hazard, `grow` 0..1 out of the floor. */
function iceSpikes(ctx, p, base, grow, alpha) {
  const tips = [];
  [0, 6, 1, 5, 2, 4, 3].forEach((j) => {
    const [x, len0, lean] = HAZARD_SPIKES[j];
    const len = len0 * grow;
    const a = Math.PI / 2 + lean;
    shard(ctx, x + Math.cos(a) * len * 0.5, base + Math.sin(a) * len * 0.5, len, len0 * 0.32, a, p, alpha, j % 2 ? 0.3 : 0);
    tips[j] = [x + Math.cos(a) * len, base + Math.sin(a) * len];
  });
  return tips;
}

const GROUNDS = [
  {
    id: 'warnZone',
    label: 'Zona de aviso (rellena)',
    description: 'Un círculo relleno con el borde encendido y un anillo que se cierra hacia el centro. Se estira al tamaño del aviso.',
    defaults: { core: '#fff1e0', glow: '#ff4a3d', accent: '#ffd0a8', particles: 0 },
    params: [
      { key: 'flat', label: 'Aplastado (vista de lado)', type: 'float', min: 0.2, max: 1, step: 0.05, default: 1,
        hint: '1 = círculo visto desde arriba; 0.4 = elipse en el suelo.' },
    ],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 5,
        draw(ctx, k, p) { zone(ctx, p, 0.88 * easeOut(k), 0.4 + 0.6 * k, 0, p.flat); } },
      { name: 'Loop', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p) { zone(ctx, p, 0.88, 0.85 + 0.15 * Math.sin(k * TAU), k, p.flat); } },
      { name: 'End', loop: false, fps: 16, frames: 4,
        draw(ctx, k, p) {
          zone(ctx, p, 0.88, 1 - k, 0, p.flat);
          ctx.save(); ctx.scale(1, p.flat);
          glowDot(ctx, 0, 0, 0.88, p.core, 0.8 * (1 - k));
          ctx.restore();
        } },
    ],
  },
  {
    id: 'shockwave',
    label: 'Onda de choque',
    description: 'Un anillo grueso que se abre a ras de suelo levantando polvo. Para ondas, terremotos y aterrizajes de jefe.',
    defaults: { core: '#ffffff', glow: '#ffb347', accent: '#ffe9a8', particles: 10 },
    params: [
      { key: 'flat', label: 'Aplastado (vista de lado)', type: 'float', min: 0.2, max: 1, step: 0.05, default: 0.32 },
    ],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 7,
        draw(ctx, k, p, rnd) {
          const out = easeOut(k), fade = 1 - easeIn(k);
          const r = 0.12 + 0.72 * out;
          ctx.save(); ctx.scale(1, p.flat);
          glowDot(ctx, 0, 0, 0.6, p.core, 0.7 * clamp01(1 - k * 2.2));
          ctx.restore();
          shockRing(ctx, 0, 0, r, 0.3, p.glow, fade * p.intensity, p.flat);
          shockRing(ctx, 0, 0, r, 0.1, p.core, fade, p.flat);
          for (let i = 0; i < p.particles; i++) {
            const a = rnd(i, 1) * TAU;
            const lift = (0.1 + rnd(i, 2) * 0.3) * Math.sin(Math.min(1, k * 1.3) * Math.PI);
            sparkShape(ctx, Math.cos(a) * r * 0.98, Math.sin(a) * r * 0.98 * p.flat + lift, 0.022 + rnd(i, 3) * 0.022, p, fade, a);
          }
        } },
    ],
  },
  {
    id: 'summonCircle',
    label: 'Círculo de invocación',
    description: 'Un círculo de runas en el suelo que gira, con luz subiendo. Para invocar esbirros, anclas y rituales.',
    defaults: { core: '#ffffff', glow: '#a86bff', accent: '#ffb8f4', particles: 8 },
    params: [
      { key: 'flat', label: 'Aplastado (vista de lado)', type: 'float', min: 0.2, max: 1, step: 0.05, default: 0.36 },
    ],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 6,
        draw(ctx, k, p) { runeCircle(ctx, p, 0.86 * easeOut(k), 0.3 + 0.7 * k, -1.5 * (1 - easeOut(k)), p.flat); } },
      { name: 'Loop', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          runeCircle(ctx, p, 0.86, 0.9 + 0.1 * Math.sin(k * TAU), (k * TAU) / 6, p.flat);
          emit(p.particles, k, rnd, (i, t) => {
            const a = rnd(i, 2) * TAU, d = 0.25 + rnd(i, 3) * 0.5;
            sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d * p.flat + t * 0.6, 0.02 + rnd(i, 4) * 0.02, p, bell(t) * 0.9, 0);
          });
        } },
      { name: 'End', loop: false, fps: 14, frames: 5,
        draw(ctx, k, p) {
          runeCircle(ctx, p, 0.86 + 0.06 * k, 1 - k, 0.4 * k, p.flat);
          ctx.save(); ctx.scale(0.5, 1);
          glowDot(ctx, 0, 0.2, 0.75, p.core, 0.6 * bell(k));
          ctx.restore();
        } },
    ],
  },
  {
    id: 'hazardFire',
    label: 'Suelo en llamas',
    description: 'Un lecho ancho de llamas bajas sobre una mancha de brasas. Para el suelo que quema y los fuegos fijos.',
    defaults: { core: '#fff4c2', glow: '#ff7a1a', accent: '#ffd04a', particles: 8 },
    params: [],
    lanes: [
      { name: 'Loop', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          const base = -0.5;
          solid(ctx, () => {                              // scorched floor
            ctx.translate(0, base); ctx.scale(1, 0.2);
            const g = ctx.createRadialGradient(0, 0, 0, 0, 0, 0.9);
            g.addColorStop(0, rgba(shade(p.glow, 0.3), 0.7));
            g.addColorStop(0.6, rgba(shade(p.glow, 0.3), 0.5));
            g.addColorStop(1, rgba(shade(p.glow, 0.3), 0));
            ctx.fillStyle = g;
            ctx.beginPath(); ctx.arc(0, 0, 0.9, 0, TAU); ctx.fill();
          });
          ctx.save(); ctx.translate(0, base); ctx.scale(1, 0.2);
          glowDot(ctx, 0, 0, 0.88, p.glow, (0.6 + 0.15 * Math.sin(k * TAU)) * p.intensity);
          ctx.restore();
          flamesRow(ctx, k, p, rnd, 9, base - 0.02, 1.3, 1.25);
        } },
    ],
  },
  {
    id: 'hazardPoison',
    label: 'Charco de veneno',
    description: 'Un charco viscoso que burbujea y suelta vapores. Para el suelo que envenena.',
    defaults: { core: '#f4ffc8', glow: '#6fcf2a', accent: '#c9ff66', particles: 8 },
    params: [],
    lanes: [
      { name: 'Loop', loop: true, fps: 10, frames: 8,
        draw(ctx, k, p, rnd) {
          const base = -0.4;
          ctx.save(); ctx.translate(0, base); ctx.scale(1, 0.3);
          glowDot(ctx, 0, 0, 0.95, p.glow, 0.4 * p.intensity);
          ctx.restore();
          ctx.save(); ctx.translate(0, base); ctx.scale(1, 0.24);
          blob(ctx, 0, 0, 0.78, k, p, 0.95, 1, 0.6);
          ctx.restore();
          emit(3, k, rnd, (i, t) => {
            puff(ctx, (i - 1) * 0.45 + loopNoise(t, i) * 0.05, base + 0.1 + t * 0.75, 0.16 + 0.12 * t, shade(p.glow, 0.7), 0.3 * bell(t));
          });
          emit(Math.max(3, p.particles), k, rnd, (i, t) => {
            const x = (rnd(i, 2) - 0.5) * 1.2;
            const y = base + (rnd(i, 4) - 0.5) * 0.2;
            if (t < 0.7) bubble(ctx, x, y + 0.03, 0.03 + 0.08 * (t / 0.7) * (0.6 + rnd(i, 3) * 0.6), p, 1);
            else ring(ctx, x, y + 0.05, 0.08 + (t - 0.7) * 0.4, 0.015, p.accent, (1 - t) / 0.3, 0.6);     // the pop
          });
        } },
    ],
  },
  {
    id: 'hazardIce',
    label: 'Púas de hielo',
    description: 'Una hilera de púas que brota del suelo (Spawn), se queda (Loop) y se hunde (End). Para el suelo helado y trampas.',
    defaults: { core: '#ffffff', glow: '#58bfff', accent: '#d6f6ff', particles: 6 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 16, frames: 5,
        draw(ctx, k, p, rnd) {
          const base = -0.5;
          ctx.save(); ctx.translate(0, base); ctx.scale(1, 0.2);
          glowDot(ctx, 0, 0, 0.95, p.glow, 0.5 * p.intensity);
          ctx.restore();
          iceSpikes(ctx, p, base, easeOut(k), 1);
          for (let i = 0; i < p.particles + 4; i++) {
            const a = (0.15 + rnd(i, 1) * 0.7) * Math.PI;
            const sp = 0.4 + rnd(i, 2) * 0.4;
            sparkShape(ctx, (rnd(i, 4) - 0.5) * 1.3 + Math.cos(a) * sp * k * 0.4, base + Math.sin(a) * sp * easeOut(k), 0.02 + rnd(i, 3) * 0.02, p, 1 - k * 0.7, a);
          }
        } },
      { name: 'Loop', loop: true, fps: 10, frames: 8,
        draw(ctx, k, p, rnd) {
          const base = -0.5;
          ctx.save(); ctx.translate(0, base); ctx.scale(1, 0.2);
          glowDot(ctx, 0, 0, 0.95, p.glow, (0.45 + 0.08 * Math.sin(k * TAU)) * p.intensity);
          ctx.restore();
          const tips = iceSpikes(ctx, p, base, 1, 1);
          const which = Math.floor(k * 4) % 4;
          const tip = tips[[3, 1, 5, 2][which]];
          flare(ctx, tip[0], tip[1], 0.22, p.core, bell(k * 4 - Math.floor(k * 4)), 4, 0, 0.14);
          emit(p.particles, k, rnd, (i, t) => {
            sparkShape(ctx, (rnd(i, 2) - 0.5) * 1.6, base + 0.1 + t * 0.9, 0.016 + rnd(i, 3) * 0.016, p, bell(t) * 0.85, 0);
          });
        } },
      { name: 'End', loop: false, fps: 14, frames: 5,
        draw(ctx, k, p) {
          const base = -0.5;
          ctx.save(); ctx.translate(0, base); ctx.scale(1, 0.2);
          glowDot(ctx, 0, 0, 0.95, p.glow, 0.45 * (1 - k) * p.intensity);
          ctx.restore();
          iceSpikes(ctx, p, base, 1 - easeIn(k) * 0.85, 1 - k * 0.6);
        } },
    ],
  },
  {
    id: 'beam',
    label: 'Rayo continuo',
    description: 'Un haz horizontal con el origen a la izquierda y pulsos que lo recorren. Se estira a lo largo: para rayos y barridos.',
    defaults: { core: '#ffffff', glow: '#a86bff', accent: '#ffb8f4', particles: 6 },
    params: [
      { key: 'thick', label: 'Grosor del haz', type: 'float', min: 0.4, max: 1.6, step: 0.05, default: 1 },
      { key: 'ends', label: 'Extremos', type: 'enum', default: 'muzzle',
        options: [['muzzle', 'Con origen (para colgar de un arma)'], ['tile', 'Tramo para estirar (llena el frame)']],
        hint: '"Tramo para estirar" es lo que usan en el juego el rayo de un arma y el barrido de un jefe: lo estiran a su largo.' },
    ],
    lanes: [
      { name: 'Spawn', loop: false, fps: 16, frames: 4,
        draw(ctx, k, p, rnd) {
          beam(ctx, 0, p, rnd, p.thick * (0.15 + 0.85 * easeOut(k)), 0.5 + 0.5 * k);
          if (p.ends !== 'tile') glowDot(ctx, -0.55, 0, 0.4 * (1 - k * 0.5), p.core, 1 - k * 0.6);
        } },
      { name: 'Loop', loop: true, fps: 14, frames: 6, draw: (ctx, k, p, rnd) => beam(ctx, k, p, rnd, p.thick, 1) },
      { name: 'End', loop: false, fps: 16, frames: 4,
        draw: (ctx, k, p, rnd) => beam(ctx, 0, p, rnd, p.thick * (1 - easeIn(k) * 0.9), 1 - k * 0.7) },
    ],
  },
].map((e) => ({ family: 'ground', kind: 'fx', ...e }));

// ============================================================ muzzle, spawn and despawn

const MUZZLES = [
  {
    id: 'muzzleFlash',
    label: 'Fogonazo de disparo',
    description: 'Un abanico de luz hacia la derecha con trazos y un destello en el origen. Para la boca de un arma al disparar.',
    defaults: { core: '#ffffff', glow: '#ffb347', accent: '#ffe9a8', particles: 6 },
    params: [
      { key: 'spread', label: 'Apertura del abanico', type: 'float', min: 0.3, max: 2.4, step: 0.05, default: 1.1 },
    ],
    lanes: [
      { name: 'Spawn', loop: false, fps: 18, frames: 4,
        draw(ctx, k, p, rnd) {
          const ox = -0.55;
          const fade = 1 - easeIn(k), out = easeOut(k);
          cone(ctx, ox, 0, 0, p.spread, 1.15 + 0.25 * out, p.glow, 0.85 * fade * p.intensity);
          cone(ctx, ox, 0, 0, p.spread * 0.45, 1.3 + 0.15 * out, p.core, fade);
          const n = Math.max(3, p.particles);
          for (let i = 0; i < n; i++) {
            const a = (i / (n - 1) - 0.5) * p.spread * 0.9;
            const far = 0.5 + (0.5 + rnd(i, 2) * 0.35) * out;
            streak(ctx, ox + Math.cos(a) * far, Math.sin(a) * far, ox + Math.cos(a) * far * 0.45, Math.sin(a) * far * 0.45, 0.06, i % 2 ? p.accent : p.core, fade);
          }
          flare(ctx, ox, 0, 0.44 * (1 - k * 0.5), p.core, fade, 4, 0, 0.2);
          core(ctx, ox, 0, 0.16 * (1 - k * 0.6), p, fade);
        } },
    ],
  },
  {
    id: 'smokePuff',
    label: 'Nube de aparición',
    description: 'Una nube que se hincha y se deshace con un destello dentro. Para aparecer, desaparecer y morir.',
    defaults: { core: '#ffffff', glow: '#8d97ab', accent: '#d5dbe8', particles: 6 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 7,
        draw(ctx, k, p, rnd) {
          const out = easeOut(k), fade = 1 - easeIn(k);
          for (let i = 0; i < 11; i++) {
            const a = (i / 11) * TAU + rnd(i, 1) * 0.6;
            const d = (0.1 + 0.42 * rnd(i, 2)) * out;
            puff(ctx, Math.cos(a) * d, Math.sin(a) * d * 0.85 + 0.12 * k, (0.2 + 0.14 * out) * (0.7 + rnd(i, 3) * 0.5),
              i % 3 ? p.glow : p.accent, 0.75 * fade);
          }
          puff(ctx, 0, 0.02, 0.3 * (1 - k * 0.3), p.accent, 0.8 * fade);
          glowDot(ctx, 0, 0, 0.45, p.core, 0.8 * clamp01(1 - k * 2.5));
          for (let i = 0; i < p.particles; i++) {
            const a = rnd(i, 4) * TAU;
            const d = 0.2 + (0.4 + rnd(i, 5) * 0.3) * out;
            sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d, 0.02 + rnd(i, 6) * 0.02, p, fade, a);
          }
        } },
    ],
  },
  {
    id: 'teleport',
    label: 'Teletransporte',
    description: 'Una columna de luz que se estrecha hasta desaparecer, con anillos que suben. Para irse o llegar de golpe.',
    defaults: { core: '#ffffff', glow: '#3fe8c0', accent: '#d0fff0', particles: 8 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 8,
        draw(ctx, k, p, rnd) {
          const fade = 1 - easeIn(k);
          const w = 0.5 * (1 - easeOut(k) * 0.85);
          ctx.save();
          ctx.rotate(Math.PI / 2);                      // the beam helper runs along X: stand it up
          beamBand(ctx, -0.9, 0.9, w * 1.5, p.glow, 0.5 * fade * p.intensity, 0.25);
          beamBand(ctx, -0.86, 0.86, w * 0.7, p.glow, 0.9 * fade * p.intensity, 0.25);
          beamBand(ctx, -0.82, 0.82, w * 0.3, p.core, fade, 0.25);
          ctx.restore();
          for (let j = 0; j < 3; j++) {
            const t = clamp01(k * 1.3 - j * 0.15);
            ring(ctx, 0, -0.6 + 1.2 * easeOut(t), 0.5 * (1 - t * 0.6), 0.03, j % 2 ? p.accent : p.glow, bell(t) * p.intensity, 0.3);
          }
          glowDot(ctx, 0, -0.6, 0.4, p.core, 0.7 * clamp01(1 - k * 2));
          for (let i = 0; i < p.particles; i++) {
            const t = clamp01(k * (0.8 + rnd(i, 1) * 0.5));
            sparkShape(ctx, (rnd(i, 2) - 0.5) * 0.9 * (1 - t * 0.7), -0.6 + t * 1.3, 0.022 + rnd(i, 3) * 0.02, p, 1 - t * t, 0);
          }
        } },
    ],
  },
].map((e) => ({ family: 'muzzle', kind: 'fx', ...e }));

// ============================================================ library

export const EFFECTS = [
  ...PROJECTILES,
  ...BURSTS,
  ...AURAS,
  ...TRAILS,
  ...GROUNDS,
  ...MUZZLES,
];
