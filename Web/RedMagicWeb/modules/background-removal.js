// modules/background-removal.js
// -----------------------------------------------------------------------------
// Flood-fill background removal, ported from the corner-sample + border-flood-fill
// algorithm in the old enemy-sprite-extractor_3.html (removeBgBtn click handler).
// Pure: takes an ImageData, mutates and returns it. No DOM queries, no canvas
// lookups — the caller (app.js) is responsible for getImageData/putImageData.

/**
 * Samples the background color from the image's 4 corners, then flood-fills
 * from every border pixel that matches that color within `tolerancePercent`,
 * zeroing alpha as it goes (4-connectivity, iterative stack — recursion would
 * overflow on a large sheet).
 *
 * @param {ImageData} imageData - mutated in place.
 * @param {number} tolerancePercent - 0-100 slider value; converted to a 0-255
 *   color-distance threshold the same way the original tool did (`* 2.55`).
 * @returns {ImageData} the same object, for convenience chaining.
 */
export function removeBackground(imageData, tolerancePercent) {
  const { width: w, height: h, data: d } = imageData;
  const tol = tolerancePercent * 2.55;

  const corners = [[0, 0], [w - 1, 0], [0, h - 1], [w - 1, h - 1]];
  let r = 0, g = 0, b = 0;
  for (const [cx, cy] of corners) {
    const idx = (cy * w + cx) * 4;
    r += d[idx]; g += d[idx + 1]; b += d[idx + 2];
  }
  r /= 4; g /= 4; b /= 4;

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

  return imageData;
}
