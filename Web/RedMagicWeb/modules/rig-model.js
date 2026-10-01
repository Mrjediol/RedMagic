// modules/rig-model.js
// -----------------------------------------------------------------------------
// The 🦴 Rig Composer's geometry and part operations, independent of any DOM:
//   - 2D affine matrices (Unity space: units, y up) and each part's local/world transform,
//   - reparenting and pivot moves that keep the art where it is on screen,
//   - loading a PNG into a part image (auto background removal + trim), splitting a sheet of parts.
//
// A part image keeps where its crop came from in the ORIGINAL file (`cropX/cropY/origW/origH`):
// that is what lets layers exported on one shared canvas assemble themselves, and what keeps a
// state sprite (asleep/awake/attacking) aligned with the base when both come from same-size files.

import { removeBackground, detectKeyColor } from './background-removal.js';
import { despillEdge } from './despill-edge.js';
import { detectSprites } from './sprite-detection.js';
import { createPart } from './rig-defaults.js';

// ============================================================ matrices  [a, b, c, d, e, f]
// maps (x, y) -> (a*x + c*y + e, b*x + d*y + f), same convention as CanvasRenderingContext2D.

export const IDENTITY = [1, 0, 0, 1, 0, 0];

export function multiply(m, n) {
  return [
    m[0] * n[0] + m[2] * n[1],
    m[1] * n[0] + m[3] * n[1],
    m[0] * n[2] + m[2] * n[3],
    m[1] * n[2] + m[3] * n[3],
    m[0] * n[4] + m[2] * n[5] + m[4],
    m[1] * n[4] + m[3] * n[5] + m[5],
  ];
}

export function invert(m) {
  const det = m[0] * m[3] - m[1] * m[2] || 1e-9;
  return [
    m[3] / det, -m[1] / det, -m[2] / det, m[0] / det,
    (m[2] * m[5] - m[3] * m[4]) / det, (m[1] * m[4] - m[0] * m[5]) / det,
  ];
}

export function apply(m, p) {
  return { x: m[0] * p.x + m[2] * p.y + m[4], y: m[1] * p.x + m[3] * p.y + m[5] };
}

export function applyVector(m, v) {
  return { x: m[0] * v.x + m[2] * v.y, y: m[1] * v.x + m[3] * v.y };
}

/** T(position) · R(rotation°) · S(scale) — a Unity localPosition/Rotation/Scale in 2D. */
export function trs(position, rotation, scale) {
  const r = (rotation * Math.PI) / 180;
  const cos = Math.cos(r), sin = Math.sin(r);
  return [cos * scale.x, sin * scale.x, -sin * scale.y, cos * scale.y, position.x, position.y];
}

/** Position / rotation / scale back out of a matrix (no skew assumed). */
export function decompose(m) {
  const sx = Math.hypot(m[0], m[1]);
  const rotation = (Math.atan2(m[1], m[0]) * 180) / Math.PI;
  const det = m[0] * m[3] - m[1] * m[2];
  return { position: { x: m[4], y: m[5] }, rotation, scale: { x: sx, y: sx > 0 ? det / sx : 1 } };
}

// ============================================================ hierarchy

export function byUid(state, uid) {
  return uid ? state.parts.find((p) => p.uid === uid) || null : null;
}

export function childrenOf(state, uid) {
  return state.parts.filter((p) => p.parent === uid);
}

/** A template slot nobody has dropped art on yet (rig-templates.js): drawn as a placeholder, left out of the export. */
export function isOpenSlot(part) {
  return !!part.slot && !part.image;
}

/** A plain empty node (a pivot other parts hang from): no art, not a slot. Drawn as a handle. */
export function isEmptyNode(part) {
  return !part.image && !part.slot;
}

/** Y of a boss's feet (root units): where it stands. `state.feetY` null = the bottom of the collider. */
export function feetY(state) {
  if (typeof state.feetY === 'number') return state.feetY;
  const c = state.collider;
  return c.offset.y - (c.type === 'circle' ? (c.radius || 0.5) : c.size.y / 2);
}

/** True if `uid` is `ancestorUid` or hangs from it. */
export function isDescendant(state, uid, ancestorUid) {
  for (let p = byUid(state, uid); p; p = byUid(state, p.parent)) if (p.uid === ancestorUid) return true;
  return false;
}

/** Path under Visual, the reference rig-config uses ("Torso/RightArm"). */
export function pathOf(state, part) {
  const names = [];
  for (let p = part; p; p = byUid(state, p.parent)) names.unshift(p.name);
  return names.join('/');
}

/**
 * Local matrix of a part. `pose` (optional) overrides position/rotation/scale — the preview
 * animation feeds its animated values through here.
 */
export function localMatrix(part, pose = null) {
  const p = pose || part;
  return trs(p.position, p.rotation, p.scale);
}

/** Part → Visual space. `poses` (optional Map uid → pose) animates every level of the chain. */
export function worldMatrix(state, part, poses = null) {
  let m = localMatrix(part, poses?.get(part.uid));
  for (let p = byUid(state, part.parent); p; p = byUid(state, p.parent))
    m = multiply(localMatrix(p, poses?.get(p.uid)), m);
  return m;
}

/** Original-canvas pixel (y down) → the part's local units (y up), with its pivot at 0. */
export function pixelToLocal(state, part, px) {
  const ppu = state.pixelsPerUnit;
  return { x: (px.x - part.pivot.x) / ppu, y: -(px.y - part.pivot.y) / ppu };
}

export function localToPixel(state, part, local) {
  const ppu = state.pixelsPerUnit;
  return { x: part.pivot.x + local.x * ppu, y: part.pivot.y - local.y * ppu };
}

/** Moves a part under a new parent (null = Visual) keeping it exactly where it is. */
export function reparent(state, part, parentUid) {
  if (parentUid && isDescendant(state, parentUid, part.uid)) return false;
  const world = worldMatrix(state, part);
  const parent = byUid(state, parentUid);
  const local = parent ? multiply(invert(worldMatrix(state, parent)), world) : world;
  const d = decompose(local);
  part.parent = parentUid || null;
  part.position = d.position;
  part.rotation = d.rotation;
  part.scale = d.scale;
  return true;
}

/**
 * New joint at `pixel` (original-canvas pixels). The art does not move: the part's position
 * absorbs the shift, and so do its children (they hang from the pivot).
 */
export function setPivot(state, part, pixel) {
  const shift = pixelToLocal(state, part, pixel);   // new pivot in the old local frame
  const inParent = applyVector(localMatrix({ position: { x: 0, y: 0 }, rotation: part.rotation, scale: part.scale }), shift);
  part.position = { x: part.position.x + inParent.x, y: part.position.y + inParent.y };
  childrenOf(state, part.uid).forEach((c) => { c.position = { x: c.position.x - shift.x, y: c.position.y - shift.y }; });
  part.pivot = { x: pixel.x, y: pixel.y };
}

/** A name nobody else in the document uses (paths stay unique). */
export function uniqueName(state, wanted, except = null) {
  const base = (wanted || 'Pieza').replace(/[\\/]/g, '_').trim() || 'Pieza';
  let name = base, n = 2;
  while (state.parts.some((p) => p !== except && p.name === name) || name === 'Visual') name = `${base}${n++}`;
  return name;
}

// ============================================================ images

export function loadImage(src) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = reject;
    img.src = src;
  });
}

function fileToDataURL(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

/** Opaque corners = a flat background still there (a web-generated part usually has one). */
function hasOpaqueCorners(data) {
  const { width: w, height: h, data: d } = data;
  const corners = [0, w - 1, (h - 1) * w, h * w - 1];
  return corners.every((i) => d[i * 4 + 3] > 250);
}

/** Bounding box of the visible pixels, or null if the image is empty. */
function alphaBounds(data, threshold = 10) {
  const { width: w, height: h, data: d } = data;
  let minX = w, minY = h, maxX = -1, maxY = -1;
  for (let y = 0; y < h; y++)
    for (let x = 0; x < w; x++)
      if (d[(y * w + x) * 4 + 3] > threshold) {
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
      }
  return maxX < 0 ? null : { x: minX, y: minY, w: maxX - minX + 1, h: maxY - minY + 1 };
}

function cropCanvas(source, box) {
  const c = document.createElement('canvas');
  c.width = box.w;
  c.height = box.h;
  c.getContext('2d').drawImage(source, box.x, box.y, box.w, box.h, 0, 0, box.w, box.h);
  return c;
}

/**
 * A file (or dataURL) → a trimmed part image. With `removeBg` 'auto', a flat background is keyed
 * out only when the four corners are opaque (the same removal + despill the Sprites tab uses).
 * Returns { image, removedBg } or null if nothing visible is left.
 */
export async function importImage(fileOrDataURL, { removeBg = 'auto', tolerance = 12 } = {}) {
  const src = typeof fileOrDataURL === 'string' ? fileOrDataURL : await fileToDataURL(fileOrDataURL);
  const img = await loadImage(src);
  const canvas = document.createElement('canvas');
  canvas.width = img.naturalWidth;
  canvas.height = img.naturalHeight;
  const ctx = canvas.getContext('2d');
  ctx.drawImage(img, 0, 0);
  let data = ctx.getImageData(0, 0, canvas.width, canvas.height);

  let removedBg = false;
  if (removeBg === true || (removeBg === 'auto' && hasOpaqueCorners(data))) {
    const keyColor = detectKeyColor(data);
    if (keyColor) {
      removeBackground(data, tolerance, { keyColor });
      despillEdge(data, { keyColor });
      ctx.putImageData(data, 0, 0);
      removedBg = true;
    }
  }

  const box = alphaBounds(data);
  if (!box) return null;
  return { image: imageRecord(cropCanvas(canvas, box), canvas.width, canvas.height, box.x, box.y), removedBg };
}

function imageRecord(canvas, origW, origH, cropX, cropY) {
  return { dataURL: canvas.toDataURL('image/png'), w: canvas.width, h: canvas.height, origW, origH, cropX, cropY };
}

/**
 * A part whose image holds several separate pieces → one part per piece (the Sprites tab's blob
 * detection), each with its pivot at its own centre, placed where it was. Returns the new parts
 * (the original is not touched; the caller swaps them in), or [] if there is only one piece.
 */
export async function splitPart(state, part, { minArea = 400 } = {}) {
  if (!part.image) return [];
  const img = await loadImage(part.image.dataURL);
  const canvas = document.createElement('canvas');
  canvas.width = img.naturalWidth;
  canvas.height = img.naturalHeight;
  canvas.getContext('2d').drawImage(img, 0, 0);
  const data = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height);
  const boxes = detectSprites(data, { minArea, dilation: 2 });
  if (boxes.length < 2) return [];

  const world = worldMatrix(state, part);
  return boxes.map((box, i) => {
    const piece = cropCanvas(canvas, box);
    const image = imageRecord(piece, part.image.origW, part.image.origH, part.image.cropX + box.x, part.image.cropY + box.y);
    const p = createPart({ name: uniqueName(state, `${part.name}_${i + 1}`), image, index: state.parts.length + i });
    p.parent = part.parent;
    // Where its pivot (the crop centre) is in the parent: through the original part's matrices.
    const local = pixelToLocal(state, part, p.pivot);
    const inVisual = apply(world, local);
    const parentWorld = part.parent ? worldMatrix(state, byUid(state, part.parent)) : null;
    const d = decompose(multiply(parentWorld ? invert(parentWorld) : [1, 0, 0, 1, 0, 0],
      [world[0], world[1], world[2], world[3], inVisual.x, inVisual.y]));
    p.position = d.position;
    p.rotation = d.rotation;
    p.scale = d.scale;
    return p;
  });
}

/**
 * Where a newly added part goes: layers cut from one shared canvas keep their layout (the canvas
 * centre is the body's origin), so several full-canvas PNGs drop in already assembled.
 */
export function initialPosition(state, image, pivot) {
  const ppu = state.pixelsPerUnit;
  return { x: (pivot.x - image.origW / 2) / ppu, y: -(pivot.y - image.origH / 2) / ppu };
}

/** Normalized pivot (0..1, y up — Unity's convention) of an image for a pivot in original pixels. */
export function normalizedPivot(image, pivot) {
  return { x: (pivot.x - image.cropX) / image.w, y: 1 - (pivot.y - image.cropY) / image.h };
}

/**
 * The pivot a state sprite (asleep/awake/attacking/stage) must use to line up with its part: the
 * same original pixel when both files have the same size (cut from the same canvas), else the
 * variant's own centre (then it can only be aligned by hand).
 */
export function variantPivot(part, variant) {
  const aligned = variant.origW === part.image?.origW && variant.origH === part.image?.origH;
  return aligned ? part.pivot : { x: variant.cropX + variant.w / 2, y: variant.cropY + variant.h / 2 };
}
