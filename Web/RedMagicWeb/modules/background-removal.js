// modules/background-removal.js
// -----------------------------------------------------------------------------
// Flood-fill background removal, ported from the corner-sample + border-flood-fill
// algorithm in the old enemy-sprite-extractor_3.html (removeBgBtn click handler).
// Pure: takes an ImageData, mutates and returns it. No DOM queries, no canvas
// lookups — the caller (app.js) is responsible for getImageData/putImageData.

/** Averages the 4 corner pixels of the image into a single sampled background color. */
function sampleCornerColor(imageData) {
  const { width: w, height: h, data: d } = imageData;
  const corners = [[0, 0], [w - 1, 0], [0, h - 1], [w - 1, h - 1]];
  let r = 0, g = 0, b = 0;
  for (const [cx, cy] of corners) {
    const idx = (cy * w + cx) * 4;
    r += d[idx]; g += d[idx + 1]; b += d[idx + 2];
  }
  return { r: r / 4, g: g / 4, b: b / 4 };
}

/**
 * Samples the background color from the image's 4 corners, then flood-fills
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

  const { r, g, b } = sampleCornerColor(imageData);
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
