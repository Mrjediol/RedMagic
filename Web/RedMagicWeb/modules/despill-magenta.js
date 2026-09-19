// modules/despill-magenta.js
// -----------------------------------------------------------------------------
// Post-process step that runs AFTER removeBackground (background-removal.js) on
// the already-transparent ImageData: deletes leftover magenta/pink halo pixels
// that anti-aliasing blended into the silhouette's edge during chroma keying.
// Pure: mutates and returns the ImageData. No DOM/canvas lookups.

/**
 * Erodes the alpha>10 mask by `radius` using two separable 1D passes (horizontal
 * then vertical) instead of a naive O(w*h*radius^2) double loop, then deletes any
 * "edge" pixel (alpha>10 but not solid) whose color is magenta-ish.
 *
 * @param {ImageData} imageData - mutated in place.
 * @param {object} [opts]
 * @param {number} [opts.magentaThreshold=15] - min (R-G) and (B-G) to call a pixel magenta.
 * @param {number} [opts.edgeRadius=2] - how many pixels in from the alpha boundary count as "edge".
 * @returns {ImageData} the same object, for convenience chaining.
 */
export function despillMagentaEdge(imageData, opts = {}) {
  const { magentaThreshold = 15, edgeRadius = 2 } = opts;
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
    const r = d[idx], g = d[idx + 1], b = d[idx + 2];
    if (r - g > magentaThreshold && b - g > magentaThreshold) {
      d[idx + 3] = 0;
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
