// modules/background-removal.js
// -----------------------------------------------------------------------------
// Flood-fill background removal, ported from the corner-sample + border-flood-fill
// algorithm in the old enemy-sprite-extractor_3.html (removeBgBtn click handler).
// Pure: takes an ImageData, mutates and returns it. No DOM queries, no canvas
// lookups — the caller (app.js) is responsible for getImageData/putImageData.

/**
 * Auto-detects the chroma-key color: samples the whole border (up to ~4000 pixels, evenly
 * strided), buckets them by color (4 bits per channel) and averages the most common bucket.
 * A character touching one corner no longer skews the result the way a 4-corner average did.
 * Transparent pixels are ignored (an already-cleaned image). Returns null if nothing opaque.
 *
 * @param {ImageData} imageData
 * @returns {{r:number, g:number, b:number} | null}
 */
export function detectKeyColor(imageData) {
  const { width: w, height: h, data: d } = imageData;
  const perimeter = 2 * (w + h);
  const stride = Math.max(1, Math.floor(perimeter / 4000));
  const buckets = new Map();

  const sample = (x, y) => {
    const idx = (y * w + x) * 4;
    if (d[idx + 3] < 128) return;
    const key = ((d[idx] >> 4) << 8) | ((d[idx + 1] >> 4) << 4) | (d[idx + 2] >> 4);
    let bucket = buckets.get(key);
    if (!bucket) { bucket = { n: 0, r: 0, g: 0, b: 0 }; buckets.set(key, bucket); }
    bucket.n++; bucket.r += d[idx]; bucket.g += d[idx + 1]; bucket.b += d[idx + 2];
  };

  for (let x = 0; x < w; x += stride) { sample(x, 0); sample(x, h - 1); }
  for (let y = 0; y < h; y += stride) { sample(0, y); sample(w - 1, y); }

  let best = null;
  for (const bucket of buckets.values()) if (!best || bucket.n > best.n) best = bucket;
  return best ? { r: best.r / best.n, g: best.g / best.n, b: best.b / best.n } : null;
}

/**
 * Takes the key color (the one passed in, or auto-detected from the border), then flood-fills
 * from every border pixel that matches that color within `tolerancePercent`,
 * zeroing alpha as it goes (4-connectivity, iterative stack — recursion would
 * overflow on a large sheet). As a second pass, also finds "enclosed pockets":
 * regions of background color trapped between body parts (e.g. the gap between
 * an arm and torso) that never touch the canvas border, so the edge flood-fill
 * can't reach them — see `removeEnclosedPockets` below.
 *
 * @param {ImageData} imageData - mutated in place.
 * @param {number} tolerancePercent - 0-100 slider value; converted to a 0-255
 *   color-distance threshold the same way the original tool did (`* 2.55`).
 * @param {object} [options]
 * @param {{r:number,g:number,b:number}} [options.keyColor] - the chroma key to remove (any
 *   color: magenta, green, blue…). Omitted = auto-detected with `detectKeyColor`.
 * @param {boolean} [options.detectPockets=true] - also remove enclosed background
 *   pockets that don't touch the border. Escape hatch for the rare case where a
 *   legitimately background-colored area is fully enclosed by the character's own
 *   design and would otherwise be wrongly deleted.
 * @param {number} [options.maxPocketAreaPercent=15] - safety guard: a pocket larger
 *   than this percent of the image's total pixel area is left alone (deleting a
 *   pocket that big is more likely a mistaken read of the character than a real gap).
 * @returns {ImageData} the same object, for convenience chaining.
 */
export function removeBackground(imageData, tolerancePercent, options = {}) {
  const { detectPockets = true, maxPocketAreaPercent = 15 } = options;
  const { width: w, height: h, data: d } = imageData;
  const tol = tolerancePercent * 2.55;

  const key = options.keyColor || detectKeyColor(imageData);
  if (!key) return imageData; // nothing opaque on the border: nothing to key out
  const { r, g, b } = key;
  const matches = (idx) => {
    const dr = d[idx] - r, dg = d[idx + 1] - g, db = d[idx + 2] - b;
    return Math.sqrt(dr * dr + dg * dg + db * db) <= tol;
  };

  const visited = new Uint8Array(w * h);
  const stack = [];
  for (let x = 0; x < w; x++) { stack.push(x, 0, x, h - 1); }
  for (let y = 0; y < h; y++) { stack.push(0, y, w - 1, y); }
  // stack holds flat [x0,y0,x1,y1,...] pairs — pushed/popped two at a time below.

  while (stack.length) {
    const y = stack.pop();
    const x = stack.pop();
    if (x < 0 || y < 0 || x >= w || y >= h) continue;
    const p = y * w + x;
    if (visited[p]) continue;
    visited[p] = 1;
    const idx = p * 4;
    if (!matches(idx)) continue;
    d[idx + 3] = 0;
    stack.push(x + 1, y, x - 1, y, x, y + 1, x, y - 1);
  }

  if (detectPockets) {
    removeEnclosedPockets(imageData, matches, maxPocketAreaPercent);
  }

  return imageData;
}

/**
 * Finds connected components of pixels that still match the background color
 * (via the same `matches` tolerance test the border flood-fill used) but were
 * never reached by it — i.e. they don't touch the canvas border through a path
 * of matching pixels, so they're background trapped inside the silhouette.
 * Components at or under `maxPocketAreaPercent` of the image area are deleted
 * (alpha set to 0); larger ones are left alone, since a hole that big is more
 * likely a real read of the character's own color than a background pocket.
 *
 * Iterative queue-based flood fill (not recursion) — safe on large sprite sheets.
 */
function removeEnclosedPockets(imageData, matches, maxPocketAreaPercent) {
  const { width: w, height: h, data: d } = imageData;
  const maxPocketArea = (maxPocketAreaPercent / 100) * (w * h);

  const visited = new Uint8Array(w * h);
  const stack = [];
  const component = [];

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const p = y * w + x;
      if (visited[p]) continue;
      visited[p] = 1;
      const idx = p * 4;
      if (d[idx + 3] === 0 || !matches(idx)) continue; // already transparent, or not background-colored

      component.length = 0;
      stack.length = 0;
      stack.push(x, y);
      component.push(p);

      while (stack.length) {
        const cy = stack.pop();
        const cx = stack.pop();
        const neighbors = [[cx + 1, cy], [cx - 1, cy], [cx, cy + 1], [cx, cy - 1]];
        for (const [nx, ny] of neighbors) {
          if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
          const np = ny * w + nx;
          if (visited[np]) continue;
          visited[np] = 1;
          const nidx = np * 4;
          if (d[nidx + 3] === 0 || !matches(nidx)) continue;
          component.push(np);
          stack.push(nx, ny);
        }
      }

      if (component.length <= maxPocketArea) {
        for (const cp of component) { d[cp * 4 + 3] = 0; }
      }
    }
  }
}
