// tools/comfy/compare-variant.mjs
// -----------------------------------------------------------------------------
// "Is this variant the same piece, except what had to change?" — measured, not eyeballed.
// A state sprite replaces its part at the same pivot pixel, so it must have the same canvas and the
// same silhouette, and differ only where the state shows (the eyes, the crystal).
//
//   node compare-variant.mjs <part.png> <variant.png> [<variant.png> …]
//     canvas     same size or not
//     changed    % of the part's pixels that differ, and the box they sit in (as % of the part's box)
//     outline    how far the silhouette moved: pixels that are ink in one and background in the other
//   node compare-variant.mjs --part-of <base.png> <part.png> [<part.png> …]
//     a PART against the drawing it was cut from: did it stay in place, same size, same colours?
//     matchPct  % of the part's drawn pixels that sit on drawn base pixels of the same colour (≥ 90 = in place)
//     offPct    % of the part drawn where the base has background (it moved / grew / gained something)
//   Works on images with a flat light background (before the cut) or with alpha (after it).
//   Names are relative to Comfy's output folder unless absolute.

import { createCanvas, loadImage } from '@napi-rs/canvas';
import { inOutput } from './contact-sheet.mjs';

async function pixels(file) {
  const img = await loadImage(inOutput(file));
  const canvas = createCanvas(img.width, img.height);
  const ctx = canvas.getContext('2d');
  ctx.drawImage(img, 0, 0);
  return { w: img.width, h: img.height, d: ctx.getImageData(0, 0, img.width, img.height).data };
}

/** Part of the piece (not background): opaque enough, and not the near-white backdrop. */
const solid = (d, i) => d[i + 3] > 40 && !(d[i + 3] > 250 && d[i] > 235 && d[i + 1] > 235 && d[i + 2] > 235);

function box(mask, w, h) {
  let x0 = w, y0 = h, x1 = -1, y1 = -1, n = 0;
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (mask[y * w + x]) { n++; if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
  return { x0, y0, x1, y1, n, w: x1 - x0 + 1, h: y1 - y0 + 1 };
}

export async function compare(partFile, variantFile) {
  const a = await pixels(partFile), b = await pixels(variantFile);
  if (a.w !== b.w || a.h !== b.h) return { sameCanvas: false, note: `${a.w}x${a.h} vs ${b.w}x${b.h}` };
  const n = a.w * a.h;
  const inA = new Uint8Array(n), changed = new Uint8Array(n), edge = new Uint8Array(n);
  for (let p = 0; p < n; p++) {
    const i = p * 4;
    const sa = solid(a.d, i), sb = solid(b.d, i);
    inA[p] = sa ? 1 : 0;
    if (sa !== sb) edge[p] = 1;
    const diff = Math.abs(a.d[i] - b.d[i]) + Math.abs(a.d[i + 1] - b.d[i + 1]) + Math.abs(a.d[i + 2] - b.d[i + 2]);
    if ((sa || sb) && diff > 90) changed[p] = 1;
  }
  const part = box(inA, a.w, a.h), ch = box(changed, a.w, a.h), ed = box(edge, a.w, a.h);
  return {
    sameCanvas: true,
    partBox: `${part.w}x${part.h} at ${part.x0},${part.y0}`,
    changedPct: +(100 * ch.n / part.n).toFixed(1),
    changedBox: ch.n ? `${ch.w}x${ch.h} at ${ch.x0},${ch.y0} (${(100 * ch.w / part.w).toFixed(0)}% x ${(100 * ch.h / part.h).toFixed(0)}% of the part)` : 'nothing',
    outlinePct: +(100 * ed.n / part.n).toFixed(1),
  };
}

/** A part against the base drawing it was cut from: same place, same size, same colours. */
export async function comparePart(baseFile, partFile) {
  const a = await pixels(baseFile), b = await pixels(partFile);
  if (a.w !== b.w || a.h !== b.h) return { sameCanvas: false, note: `${a.w}x${a.h} vs ${b.w}x${b.h}` };
  const n = a.w * a.h, inB = new Uint8Array(n);
  let drawn = 0, match = 0, off = 0;
  for (let p = 0; p < n; p++) {
    const i = p * 4;
    if (!solid(b.d, i)) continue;
    inB[p] = 1; drawn++;
    if (!solid(a.d, i)) { off++; continue; }
    const diff = Math.abs(a.d[i] - b.d[i]) + Math.abs(a.d[i + 1] - b.d[i + 1]) + Math.abs(a.d[i + 2] - b.d[i + 2]);
    if (diff <= 90) match++;
  }
  const part = box(inB, b.w, b.h);
  return { partBox: drawn ? `${part.w}x${part.h} at ${part.x0},${part.y0}` : 'empty',
    matchPct: drawn ? +(100 * match / drawn).toFixed(1) : 0, offPct: drawn ? +(100 * off / drawn).toFixed(1) : 0 };
}

if (process.argv[1]?.endsWith('compare-variant.mjs')) {
  const argv = process.argv.slice(2);
  if (argv[0] === '--part-of') {
    const [, base, ...parts] = argv;
    for (const p of parts) console.log(p.replace(/^.*[\/]/, ''), JSON.stringify(await comparePart(base, p)));
    process.exit(0);
  }
  const [part, ...variants] = argv;
  for (const v of variants) console.log(v.replace(/^.*[\\/]/, ''), JSON.stringify(await compare(part, v)));
}
