// modules/vfx-presets.js
// -----------------------------------------------------------------------------
// The ✨ VFX Lab's effect PRESETS: procedural effects drawn in code from a handful of parameters
// (size, colours, glow, particle count, speed, length, look) and BAKED into an ordinary frame sheet.
//
// Why baked frames: a baked preset is a normal library entry (kind `projectile` or `fx`, with the
// lanes those kinds already use), so everything that picks art from the library — an enemy's
// projectile, the art slots of a catalog attack, a boss's warnings — can use it today, and it
// reaches Unity through the importers that already exist (pooled prefabs, no per-character code).
// The entry keeps the preset id + parameters it was baked from, so it stays tweakable: reopen it,
// move a slider, bake again, and every enemy that uses it gets the new look on its next export.
//
// A PRESET IS DATA + ONE DRAW FUNCTION PER LANE:
//   { id, label, kind, description, params: [extra param defs], lanes: [{ name, loop, fps, frames, draw }] }
// `draw(ctx, k, p, rnd)` paints one frame on a canvas whose origin is the frame's centre and whose
// unit is the frame's half size (so 1 = the edge): `k` is the progress 0..1 through the lane, `p`
// the parameters, `rnd(i)` a stable pseudo-random 0..1 for particle i. A looping lane must draw the
// same picture at k=0 and k=1. Adding a preset = one more entry in PRESETS, nothing else.
//
// The drawing helpers every preset shares live in modules/vfx-draw.js; the effects library (the
// presets beyond the first five, grouped by FAMILY) in modules/vfx-effects.js.
//
// DOM-free apart from the canvas factory it is handed, so it can be exercised outside a browser.

import { TAU, hash, glowDot, ring, sparkShape, core, burst, easeOut, easeIn } from './vfx-draw.js';
import { EFFECTS } from './vfx-effects.js';

// ============================================================ parameters

/** Parameters every preset has. A preset's own `params` are appended to these. */
export const COMMON_PARAMS = [
  { key: 'size', label: 'Tamaño del frame (px)', type: 'int', min: 16, max: 256, step: 8, default: 96,
    hint: 'Lado de cada frame. El tamaño en el juego lo decide quien lo usa (el "size" del proyectil o del ataque).' },
  { key: 'detail', label: 'Nitidez (× resolución)', type: 'int', min: 1, max: 4, step: 1, default: 3,
    hint: 'Multiplica los píxeles de cada frame SIN cambiar su tamaño en el juego: 1 = como antes; 3–4 para que se vea tan nítido como arte en alta resolución.' },
  { key: 'core', label: 'Color del núcleo', type: 'color', default: '#ffffff' },
  { key: 'glow', label: 'Color del brillo', type: 'color', default: '#58c8ff' },
  { key: 'accent', label: 'Color de las chispas', type: 'color', default: '#b8f0ff' },
  { key: 'intensity', label: 'Intensidad del brillo', type: 'float', min: 0.2, max: 2, step: 0.05, default: 1 },
  { key: 'particles', label: 'Nº de chispas', type: 'int', min: 0, max: 40, step: 1, default: 10 },
  { key: 'speed', label: 'Velocidad (fps ×)', type: 'float', min: 0.25, max: 3, step: 0.05, default: 1,
    hint: 'Multiplica los fps de cada animación.' },
  { key: 'length', label: 'Duración (frames ×)', type: 'float', min: 0.5, max: 2.5, step: 0.25, default: 1,
    hint: 'Más frames = más suave y más largo (y una hoja más grande).' },
  { key: 'spark', label: 'Forma de las chispas', type: 'enum', default: 'round',
    options: [['round', 'Punto'], ['star', 'Estrella'], ['square', 'Cuadrado']] },
  { key: 'pixel', label: 'Pixelado (px)', type: 'int', min: 1, max: 8, step: 1, default: 1,
    hint: '1 = suave. 2–4 = aspecto de pixel art.' },
  { key: 'seed', label: 'Semilla', type: 'int', min: 1, max: 999, step: 1, default: 7,
    hint: 'Otra semilla = otra colocación de las chispas.' },
];

/** Largest baked frame side (px): keeps a 12-frame lane inside what a canvas can hold. */
const MAX_FRAME_PX = 512;

/** Every parameter of a preset, common ones first. */
export function paramsOf(preset) {
  return [...COMMON_PARAMS, ...(preset.params || [])];
}

/** The default value of every parameter of a preset (its own `defaults` override the common ones). */
export function defaultParams(preset) {
  const out = {};
  paramsOf(preset).forEach((d) => { out[d.key] = d.default; });
  return { ...out, ...(preset.defaults || {}) };
}

/** `values` with every missing/invalid parameter filled in and every number inside its range. */
export function normalizeParams(preset, values = {}) {
  const out = defaultParams(preset);
  paramsOf(preset).forEach((d) => {
    const v = values[d.key];
    if (v === undefined || v === null) return;
    if (d.type === 'color') { if (/^#[0-9a-f]{6}$/i.test(v)) out[d.key] = v.toLowerCase(); return; }
    if (d.type === 'enum') { if (d.options.some(([id]) => id === v)) out[d.key] = v; return; }
    let n = Number(v);
    if (!Number.isFinite(n)) return;
    n = Math.min(d.max, Math.max(d.min, n));
    out[d.key] = d.type === 'int' ? Math.round(n) : n;
  });
  return out;
}

// ============================================================ presets

/**
 * The families the Lab's picker groups the presets by (a preset's `family`). Display only: what an
 * entry IS for the rest of the tool is its `kind` (projectile / fx) and its lane names.
 */
export const FAMILIES = [
  { id: 'projectile', label: 'Proyectiles', hint: 'Vuelan (Move) y revientan al chocar (Impact). Apuntan a la derecha.' },
  { id: 'burst', label: 'Impactos y estallidos', hint: 'De un solo uso: golpes, explosiones, caídas.' },
  { id: 'aura', label: 'Cargas, auras y estados', hint: 'En bucle sobre un cuerpo o un arma: aparecen, se mantienen y se apagan.' },
  { id: 'trail', label: 'Estelas y movimiento', hint: 'En bucle detrás de algo que se mueve.' },
  { id: 'ground', label: 'Suelo y zonas', hint: 'Avisos, charcos, ondas y rayos: se estiran al tamaño de la zona.' },
  { id: 'muzzle', label: 'Disparo y aparición', hint: 'De un solo uso en la boca del arma o al aparecer / desaparecer.' },
];

export const PRESETS = [
  {
    id: 'orb',
    family: 'projectile',
    label: 'Orbe de hechizo',
    kind: 'projectile',
    description: 'Una bola de energía con chispas girando a su alrededor. Vuela y revienta al chocar.',
    params: [
      { key: 'orbit', label: 'Radio de la órbita', type: 'float', min: 0.2, max: 0.8, step: 0.05, default: 0.52 },
    ],
    lanes: [
      { name: 'Move', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          const pulse = 1 + 0.09 * Math.sin(k * TAU);
          core(ctx, 0, 0, 0.3 * pulse, p);
          for (let i = 0; i < p.particles; i++) {
            const turns = rnd(i, 4) < 0.5 ? 1 : -1;
            const a = rnd(i, 1) * TAU + k * TAU * turns;
            const d = p.orbit * (0.75 + rnd(i, 2) * 0.35);
            const twinkle = 0.55 + 0.45 * Math.sin(k * TAU + rnd(i, 5) * TAU);
            sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d * 0.85, 0.035 + rnd(i, 3) * 0.03, p, twinkle, a);
          }
        } },
      { name: 'Impact', loop: false, fps: 16, frames: 7, draw: (ctx, k, p, rnd) => burst(ctx, k, p, rnd) },
    ],
  },
  {
    id: 'bolt',
    family: 'projectile',
    label: 'Disparo con estela',
    kind: 'projectile',
    description: 'Un proyectil alargado que apunta a la derecha (+X) y deja una estela. Para disparos rápidos.',
    defaults: { glow: '#ffb347', accent: '#ffe9a8', particles: 8 },
    params: [
      { key: 'tail', label: 'Largo de la estela', type: 'float', min: 0.3, max: 1.6, step: 0.05, default: 1.1 },
    ],
    lanes: [
      { name: 'Move', loop: true, fps: 14, frames: 6,
        draw(ctx, k, p, rnd) {
          const head = 0.48;
          // Tail: overlapping lights that thin out towards the back, flickering in a loop.
          const steps = 14;
          for (let i = steps; i >= 1; i--) {
            const t = i / steps;
            const flick = 0.75 + 0.25 * Math.sin(k * TAU + t * 9);
            glowDot(ctx, head - t * p.tail, Math.sin(k * TAU + t * 6) * 0.03 * t, 0.24 * (1 - t * 0.75), p.glow, 0.42 * (1 - t) * flick * p.intensity);
          }
          core(ctx, head, 0, 0.2 * (1 + 0.07 * Math.sin(k * TAU)), p);
          for (let i = 0; i < p.particles; i++) {
            const t = (rnd(i, 1) + k) % 1;   // shed backwards, wraps = seamless loop
            sparkShape(ctx, head - t * p.tail, (rnd(i, 2) - 0.5) * 0.36 * (0.3 + t), 0.03 + rnd(i, 3) * 0.025, p, (1 - t) * 0.9, t * 6);
          }
        } },
      { name: 'Impact', loop: false, fps: 16, frames: 6, draw: (ctx, k, p, rnd) => burst(ctx, k, p, rnd, 0.9) },
    ],
  },
  {
    id: 'burst',
    family: 'burst',
    label: 'Impacto mágico',
    kind: 'fx',
    description: 'Un estallido de un solo uso: destello, anillo que se abre y chispas. Para impactos y golpes.',
    defaults: { particles: 14 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 16, frames: 8, draw: (ctx, k, p, rnd) => burst(ctx, k, p, rnd, 1.1) },
    ],
  },
  {
    id: 'charge',
    family: 'aura',
    label: 'Orbe cargándose',
    kind: 'fx',
    description: 'Las chispas acuden al centro y el orbe crece (Spawn), se mantiene cargado (Loop) y se deshace (End).',
    defaults: { glow: '#c38bff', accent: '#ecd9ff', particles: 14 },
    params: [],
    lanes: [
      { name: 'Spawn', loop: false, fps: 12, frames: 9,
        draw(ctx, k, p, rnd) {
          core(ctx, 0, 0, 0.08 + 0.26 * easeOut(k), p, 0.4 + 0.6 * k);
          for (let i = 0; i < p.particles; i++) {
            const t = (rnd(i, 1) + k * 1.4) % 1;
            const a = rnd(i, 2) * TAU;
            const d = 0.85 * (1 - easeIn(t));
            sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d, 0.03 + rnd(i, 3) * 0.03, p, t * (0.5 + 0.5 * k), a);
          }
        } },
      { name: 'Loop', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          core(ctx, 0, 0, 0.34 * (1 + 0.1 * Math.sin(k * TAU)), p);
          ring(ctx, 0, 0, 0.5 + 0.04 * Math.sin(k * TAU), 0.025, p.glow, 0.5 * p.intensity);
          for (let i = 0; i < p.particles; i++) {
            const t = (rnd(i, 1) + k) % 1;
            const a = rnd(i, 2) * TAU + t * 1.2;
            const d = 0.85 * (1 - easeIn(t)) + 0.1;
            sparkShape(ctx, Math.cos(a) * d, Math.sin(a) * d, 0.028 + rnd(i, 3) * 0.028, p, Math.sin(t * Math.PI), a);
          }
        } },
      { name: 'End', loop: false, fps: 16, frames: 6, draw: (ctx, k, p, rnd) => burst(ctx, k, p, rnd, 0.95) },
    ],
  },
  {
    id: 'aura',
    family: 'ground',
    label: 'Aura de aviso',
    kind: 'fx',
    description: 'Un anillo de luz en el suelo (visto de lado) que aparece, late y se apaga. Para avisos y zonas.',
    defaults: { glow: '#ff5a5a', accent: '#ffd0a8', core: '#fff1e0', particles: 8 },
    params: [
      { key: 'flat', label: 'Aplastado (vista de lado)', type: 'float', min: 0.2, max: 1, step: 0.05, default: 0.4,
        hint: '1 = círculo visto desde arriba; 0.4 = elipse en el suelo.' },
    ],
    lanes: [
      { name: 'Spawn', loop: false, fps: 14, frames: 6,
        draw(ctx, k, p) {
          const out = easeOut(k);
          glowDot(ctx, 0, 0, 0.85 * out, p.glow, 0.22 * p.intensity);
          ring(ctx, 0, 0, 0.78 * out, 0.05, p.glow, (0.4 + 0.6 * k) * p.intensity, p.flat);
        } },
      { name: 'Loop', loop: true, fps: 12, frames: 8,
        draw(ctx, k, p, rnd) {
          const pulse = 0.5 + 0.5 * Math.sin(k * TAU);
          ctx.save(); ctx.scale(1, p.flat);
          glowDot(ctx, 0, 0, 0.85, p.glow, (0.16 + 0.12 * pulse) * p.intensity);
          ctx.restore();
          ring(ctx, 0, 0, 0.78, 0.045 + 0.015 * pulse, p.glow, (0.7 + 0.3 * pulse) * p.intensity, p.flat);
          ring(ctx, 0, 0, 0.78 * ((k + 0.0001) % 1), 0.02, p.core, (1 - k) * 0.6, p.flat);   // a wave travelling outwards
          for (let i = 0; i < p.particles; i++) {
            const a = rnd(i, 1) * TAU;
            const t = (rnd(i, 2) + k) % 1;   // rising motes
            sparkShape(ctx, Math.cos(a) * 0.72, Math.sin(a) * 0.72 * p.flat + t * 0.45, 0.025 + rnd(i, 3) * 0.02, p, Math.sin(t * Math.PI) * 0.9, a);
          }
        } },
      { name: 'End', loop: false, fps: 14, frames: 5,
        draw(ctx, k, p) {
          ring(ctx, 0, 0, 0.78 + 0.12 * k, 0.05 * (1 - k) + 0.01, p.glow, (1 - k) * p.intensity, p.flat);
        } },
    ],
  },
  ...EFFECTS,
];

export function presetById(id) {
  return PRESETS.find((p) => p.id === id) || null;
}

// ============================================================ rendering + baking

/** How many frames a lane gets with these parameters. */
export function frameCount(lane, p) {
  return Math.max(2, Math.round(lane.frames * p.length));
}

/** fps of a lane with these parameters (what the baked lane carries into Unity). */
export function laneFps(lane, p) {
  return Math.max(1, Math.round(lane.fps * p.speed));
}

/**
 * Paints one frame of a lane on `ctx` (cleared first), filling a `size`×`size` square at (x, y).
 * `k` = progress 0..1. With `p.pixel` > 1 the frame is drawn small and scaled up without smoothing.
 */
export function drawFrame(ctx, preset, lane, k, p, x, y, size, createCanvas) {
  const pixel = Math.max(1, p.pixel | 0);
  const rnd = (i, channel = 0) => hash(p.seed, i, channel);

  const paint = (target, s) => {
    target.save();
    target.translate(s / 2, s / 2);
    target.scale(s / 2, -s / 2);          // unit = half a frame, y up
    target.globalCompositeOperation = 'lighter';
    lane.draw(target, k, p, rnd);
    target.restore();
  };

  ctx.clearRect(x, y, size, size);
  if (pixel === 1) {
    ctx.save();
    ctx.beginPath(); ctx.rect(x, y, size, size); ctx.clip();
    ctx.translate(x, y);
    paint(ctx, size);
    ctx.restore();
    return;
  }

  const small = Math.max(4, Math.round(size / pixel));
  const tmp = createCanvas(small, small);
  paint(tmp.getContext('2d'), small);
  ctx.save();
  ctx.imageSmoothingEnabled = false;
  ctx.drawImage(tmp, x, y, size, size);
  ctx.restore();
}

/**
 * Bakes a preset into a frame sheet: one row per lane, in the shape a library entry's `sprite`
 * block has (the Sprites tab's own format), so every existing export reads it unchanged.
 *
 * @param {(w: number, h: number) => HTMLCanvasElement} createCanvas
 * @returns {{canvas, boxes: Array<{x,y,w,h,assignedLane}>, lanes: object[], width: number, height: number}}
 */
export function bakePreset(preset, values, createCanvas) {
  const base = normalizeParams(preset, values);
  // 'detail' bakes more pixels for the same effect: the sheet is `density` times finer and everything
  // that sizes the effect divides by it (rig-fx.js). The pixel-art look keeps its block size.
  const density = Math.max(1, Math.min(base.detail || 1, Math.floor(MAX_FRAME_PX / base.size) || 1));
  const p = { ...base, pixel: base.pixel > 1 ? base.pixel * density : 1 };
  const size = base.size * density;
  const counts = preset.lanes.map((lane) => frameCount(lane, p));
  const width = size * Math.max(...counts);
  const height = size * preset.lanes.length;
  const canvas = createCanvas(width, height);
  const ctx = canvas.getContext('2d');

  const boxes = [];
  const lanes = preset.lanes.map((lane, row) => {
    const n = counts[row];
    const id = `lane${row}`;
    const frameBoxIndices = [];
    for (let i = 0; i < n; i++) {
      // A loop never reaches k=1 (it would repeat frame 0). A one-shot samples the middle of each
      // step, so neither its first frame (nothing yet) nor its last (all faded) comes out empty.
      const k = lane.loop ? i / n : (i + 0.5) / n;
      drawFrame(ctx, preset, lane, k, p, i * size, row * size, size, createCanvas);
      frameBoxIndices.push(boxes.length);
      boxes.push({ x: i * size, y: row * size, w: size, h: size, assignedLane: id });
    }
    return { id, type: 'animation', name: lane.name, fps: laneFps(lane, p), loop: lane.loop, frameBoxIndices, projectileLink: null };
  });

  return { canvas, boxes, lanes, width, height, density };
}
