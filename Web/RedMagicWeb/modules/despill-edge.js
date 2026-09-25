// modules/despill-edge.js
// -----------------------------------------------------------------------------
// Post-process step that runs AFTER removeBackground (background-removal.js) on
// the already-transparent ImageData: cleans the halo of key color (magenta, green,
// blue… whatever the background was) that anti-aliasing blended into the
// silhouette's edge during chroma keying. Pure: mutates and returns the ImageData.

/**
 * Splits the key color's channels into "key" channels (the ones that make it that hue —
 * R+B for magenta, G for green, B for blue) and "other" channels. Returns null for a key
 * with no clear hue (black, white, grey), where there is no tint to remove.
 *
 * @param {{r:number,g:number,b:number}} keyColor
 * @returns {{key:number[], other:number[]} | null} channel offsets (0=R, 1=G, 2=B)
 */
export function keyChannels(keyColor) {
  const c = [keyColor.r, keyColor.g, keyColor.b];
  const max = Math.max(...c);
  if (max < 60) return null;

  const key = [], other = [];
  c.forEach((v, i) => (v >= max * 0.6 ? key : other).push(i));
  if (other.length === 0) return null;

  const saturation = Math.min(...key.map((i) => c[i])) - Math.max(...other.map((i) => c[i]));
  return saturation >= 40 ? { key, other } : null;
}

/**
 * Erodes the alpha>10 mask by `radius` using two separable 1D passes (horizontal
 * then vertical) instead of a naive O(w*h*radius^2) double loop. On every "edge" pixel
 * (alpha>10 but not solid) measures its spill — how far its key channels rise above its
 * other channels, `min(key) - max(other)`:
 *  - spill > `threshold`: the pixel IS background color → deleted (alpha 0).
 *  - 0 < spill <= threshold: tinted fringe → the key channels are clamped down by the spill
 *    (green: G = max(R, B); magenta: R and B lowered until min(R, B) = G), so no halo remains.
 *
 * @param {ImageData} imageData - mutated in place.
 * @param {object} [opts]
 * @param {{r:number,g:number,b:number}} opts.keyColor - the chroma key that was removed.
 * @param {number} [opts.threshold=15] - spill above which an edge pixel is deleted instead of clamped.
 * @param {number} [opts.edgeRadius=2] - how many pixels in from the alpha boundary count as "edge".
 * @returns {ImageData} the same object, for convenience chaining.
 */
export function despillEdge(imageData, opts = {}) {
  const { keyColor, threshold = 15, edgeRadius = 2 } = opts;
  const channels = keyColor ? keyChannels(keyColor) : null;
  if (!channels) return imageData; // no hue to despill (grey/black/white key, or unknown)

  const { key, other } = channels;
  const { width: w, height: h, data: d } = imageData;
  const n = w * h;

  // alphaMask[i] = 1 if pixel i has alpha > 10.
  const alphaMask = new Uint8Array(n);
  for (let i = 0; i < n; i++) {
    alphaMask[i] = d[i * 4 + 3] > 10 ? 1 : 0;
  }

  const solid = erodeMask(alphaMask, w, h, edgeRadius);

  for (let i = 0; i < n; i++) {
    if (!alphaMask[i] || solid[i]) continue; // interior or already transparent
    const idx = i * 4;

    let keyMin = 255, otherMax = 0;
    for (const c of key) keyMin = Math.min(keyMin, d[idx + c]);
    for (const c of other) otherMax = Math.max(otherMax, d[idx + c]);
    const spill = keyMin - otherMax;

    if (spill > threshold) {
      d[idx + 3] = 0;
    } else if (spill > 0) {
      for (const c of key) d[idx + c] -= spill;
    }
  }

  return imageData;
}

/** Binary erosion of a 0/1 mask by `radius` (4-neighborhood square), separable. */
function erodeMask(mask, w, h, radius) {
  if (radius <= 0) return mask.slice();

  const horiz = new Uint8Array(w * h);
  for (let y = 0; y < h; y++) {
    const rowOff = y * w;
    for (let x = 0; x < w; x++) {
      let ok = mask[rowOff + x];
      for (let k = 1; ok && k <= radius; k++) {
        const xl = x - k, xr = x + k;
        if (xl < 0 || xr >= w || !mask[rowOff + xl] || !mask[rowOff + xr]) ok = 0;
      }
      horiz[rowOff + x] = ok;
    }
  }

  const eroded = new Uint8Array(w * h);
  for (let x = 0; x < w; x++) {
    for (let y = 0; y < h; y++) {
      const p = y * w + x;
      let ok = horiz[p];
      for (let k = 1; ok && k <= radius; k++) {
        const yu = y - k, yd = y + k;
        if (yu < 0 || yd >= h || !horiz[yu * w + x] || !horiz[yd * w + x]) ok = 0;
      }
      eroded[p] = ok;
    }
  }

  return eroded;
}
