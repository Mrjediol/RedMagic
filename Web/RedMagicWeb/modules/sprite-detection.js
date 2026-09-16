// modules/sprite-detection.js
// -----------------------------------------------------------------------------
// Connected-component sprite detection, ported from the old detectBtn click
// handler + its dilateMask helper. Pure functions operating on typed arrays /
// plain box objects — no DOM, no canvas element lookups. app.js calls
// detectSprites() with an ImageData it already has and gets back a boxes[]
// array in the same {x,y,w,h,assignedLane} shape the rest of the app expects.

/** Foreground mask: 1 where alpha is above the threshold, else 0. */
export function buildAlphaMask(imageData, alphaThreshold = 10) {
  const { width: w, height: h, data: d } = imageData;
  const mask = new Uint8Array(w * h);
  for (let i = 0; i < w * h; i++) mask[i] = d[i * 4 + 3] > alphaThreshold ? 1 : 0;
  return mask;
}

/** Grows the mask outward by `radius` px (4-connectivity), one ring per iteration. */
export function dilateMask(mask, w, h, radius) {
  let current = mask;
  for (let it = 0; it < radius; it++) {
    const next = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const p = y * w + x;
        if (current[p]) { next[p] = 1; continue; }
        if ((x > 0 && current[p - 1]) || (x < w - 1 && current[p + 1]) ||
            (y > 0 && current[p - w]) || (y < h - 1 && current[p + w])) {
          next[p] = 1;
        }
      }
    }
    current = next;
  }
  return current;
}

/**
 * 4-connectivity connected components over `mask`, iterative flood fill
 * (recursion would overflow on a large sheet). Components smaller than
 * `minArea` px² are dropped — this is what filters out stray noise/label text.
 * Returns tight bounding boxes in mask-pixel space: {x,y,w,h}.
 */
export function connectedComponents(mask, w, h, minArea) {
  const labels = new Int32Array(w * h).fill(-1);
  const boxes = [];
  let label = 0;
  const stack = [];

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const p = y * w + x;
      if (!mask[p] || labels[p] !== -1) continue;

      let minX = x, maxX = x, minY = y, maxY = y, area = 0;
      stack.push(p);
      labels[p] = label;

      while (stack.length) {
        const cp = stack.pop();
        const cx = cp % w, cy = (cp / w) | 0;
        area++;
        if (cx < minX) minX = cx; if (cx > maxX) maxX = cx;
        if (cy < minY) minY = cy; if (cy > maxY) maxY = cy;

        const neighbors = [cp - 1, cp + 1, cp - w, cp + w];
        for (const np of neighbors) {
          if (np < 0 || np >= w * h) continue;
          const nx = np % w;
          if (Math.abs(nx - cx) > 1) continue; // no wrap-around at row edges
          if (mask[np] && labels[np] === -1) {
            labels[np] = label;
            stack.push(np);
          }
        }
      }

      if (area >= minArea) boxes.push({ x: minX, y: minY, w: maxX - minX + 1, h: maxY - minY + 1 });
      label++;
    }
  }

  return boxes;
}

/** Expands each box by `padding` px on every side, clamped to the image bounds. */
export function applyPadding(boxes, padding, imgW, imgH) {
  return boxes.map((b) => {
    const x = Math.max(0, b.x - padding);
    const y = Math.max(0, b.y - padding);
    const x2 = Math.min(imgW, b.x + b.w + padding);
    const y2 = Math.min(imgH, b.y + b.h + padding);
    return { x, y, w: x2 - x, h: y2 - y, assignedLane: null };
  });
}

/**
 * Buckets boxes into rows by y-center proximity (`rowTol` px). Rows come out
 * top-to-bottom (boxes are pre-sorted by y-center before clustering), but each
 * row's own boxes are returned in whatever order they were merged in — NOT
 * left-to-right. This is the shared "what row is this box in" primitive: both
 * `sortReadingOrder` below (detection's reading-order sort) and app.js's
 * "Animaciones automáticas" row-to-lane assignment build on it, so the
 * clustering rule only has one implementation.
 *
 * @returns {Array<Array<object>>} one array of boxes per row, top-to-bottom.
 */
export function clusterIntoRows(boxes, rowTol = 40) {
  const byY = [...boxes].sort((a, b) => (a.y + a.h / 2) - (b.y + b.h / 2));
  const rows = [];
  for (const bx of byY) {
    const cy = bx.y + bx.h / 2;
    let row = rows.find((r) => Math.abs(r.cy - cy) < rowTol);
    if (!row) { row = { cy, items: [] }; rows.push(row); }
    row.items.push(bx);
  }
  return rows.map((r) => r.items);
}

/**
 * Reading order: cluster into rows (see `clusterIntoRows`), then sort each row
 * left-to-right by x and flatten back into one array.
 */
export function sortReadingOrder(boxes, rowTol = 40) {
  return clusterIntoRows(boxes, rowTol).flatMap((items) => [...items].sort((a, b) => a.x - b.x));
}

/**
 * Full pipeline: alpha mask → optional dilation → connected components →
 * min-area filter → padding → reading-order sort. This is the orchestration
 * that used to live inline in the detectBtn handler.
 *
 * @param {ImageData} imageData
 * @param {{minArea?:number, dilation?:number, padding?:number, alphaThreshold?:number, rowTol?:number}} opts
 * @returns {Array<{x:number,y:number,w:number,h:number,assignedLane:null}>}
 */
export function detectSprites(imageData, opts = {}) {
  const {
    minArea = 400,
    dilation = 0,
    padding = 0,
    alphaThreshold = 10,
    rowTol = 40,
  } = opts;

  const w = imageData.width, h = imageData.height;

  let mask = buildAlphaMask(imageData, alphaThreshold);
  if (dilation > 0) mask = dilateMask(mask, w, h, dilation);

  const raw = connectedComponents(mask, w, h, minArea);
  const padded = applyPadding(raw, padding, w, h);
  return sortReadingOrder(padded, rowTol);
}
