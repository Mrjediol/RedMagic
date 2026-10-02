// tools/comfy/finish-parts.mjs
// -----------------------------------------------------------------------------
// The last step of the parts: from what `claude-workflow.mjs finish` saved (per image `<Name>_up` =
// upscaled on its white background, `<Name>_cut` = same with the background removed) to the PNGs a rig
// imports — one per part and per state, all on the SAME canvas as the base drawing (a part sits where
// it sat in the creature, so stacking them rebuilds it and a state sprite lands on its part's pivot).
//
//   node finish-parts.mjs <folder in Comfy's output> [--order Lower,CastArm,Torso,Head,OffArm]
//
//   · colour  = the image's own `_up`, with the white backdrop taken out of the soft edge pixels
//   · alpha   = the PART's cut, also for its states (`Head__asleep` uses Head's): a state can never
//               have a different silhouette than its part. Snapped: ≥ 250 → 255, ≤ 8 → 0
//               (the cutter leaves the inside of a piece at 254).
//   · checks  = canvas sizes equal, one island per part (loose crumbs are deleted and counted),
//               nothing touching the canvas edge, state vs part: how much changed.
//   Writes <folder>/final/<Name>.png, plus _assembled.png (the parts stacked, on a dark backdrop) and
//   _parts.png (each one alone on magenta) to look at.

import fs from 'node:fs';
import path from 'node:path';
import { createCanvas, loadImage } from '@napi-rs/canvas';
import { inOutput } from './contact-sheet.mjs';

const [folderArg, ...rest] = process.argv.slice(2);
const folder = inOutput(folderArg);
const orderAt = rest.indexOf('--order');
const order = orderAt >= 0 ? rest[orderAt + 1].split(',') : null;

async function read(file) {
  const img = await loadImage(file);
  const canvas = createCanvas(img.width, img.height);
  const ctx = canvas.getContext('2d');
  ctx.drawImage(img, 0, 0);
  return { w: img.width, h: img.height, d: ctx.getImageData(0, 0, img.width, img.height).data };
}

// The newest take of each name: "Head_cut_00002_.png" wins over "_00001_".
const takes = {};
for (const f of fs.readdirSync(folder).sort()) {
  const m = /^(.+)_(up|cut)_\d+_\.png$/.exec(f);
  if (m) (takes[m[1]] ||= {})[m[2]] = path.join(folder, f);
}
const names = Object.keys(takes).filter((n) => takes[n].up && takes[n].cut);
const parts = names.filter((n) => !n.includes('__'));
if (!parts.length) { console.error(`✗ nothing to finish in ${folder}`); process.exit(1); }

/** Keeps the biggest island of the alpha; returns how many other pixels were dropped. */
function keepMainIsland(alpha, w, h) {
  const label = new Int32Array(w * h);
  const sizes = [0];
  const stack = [];
  for (let p = 0; p < w * h; p++) {
    if (!alpha[p] || label[p]) continue;
    const id = sizes.length;
    let size = 0;
    stack.push(p);
    label[p] = id;
    while (stack.length) {
      const q = stack.pop();
      size++;
      const x = q % w;
      for (const r of [q - w, q + w, x > 0 ? q - 1 : -1, x < w - 1 ? q + 1 : -1]) {
        if (r >= 0 && r < w * h && alpha[r] && !label[r]) { label[r] = id; stack.push(r); }
      }
    }
    sizes.push(size);
  }
  const main = sizes.indexOf(Math.max(...sizes));
  let dropped = 0;
  for (let p = 0; p < w * h; p++) if (alpha[p] && label[p] !== main) { alpha[p] = 0; dropped++; }
  return { islands: sizes.length - 1, dropped };
}

const out = path.join(folder, 'final');
fs.mkdirSync(out, { recursive: true });
const finals = {};
let canvasSize = null;
const report = [];

for (const part of parts) {
  const cut = await read(takes[part].cut);
  const { w, h } = cut;
  canvasSize ||= `${w}x${h}`;
  const alpha = new Uint8ClampedArray(w * h);
  for (let p = 0; p < w * h; p++) { const a = cut.d[p * 4 + 3]; alpha[p] = a >= 250 ? 255 : a <= 8 ? 0 : a; }
  const { islands, dropped } = keepMainIsland(alpha, w, h);
  let x0 = w, y0 = h, x1 = -1, y1 = -1;
  for (let p = 0; p < w * h; p++) if (alpha[p]) { const x = p % w, y = (p / w) | 0; if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
  const touches = x0 === 0 || y0 === 0 || x1 === w - 1 || y1 === h - 1;

  // The part first: its states are measured against it.
  for (const name of [part, ...names.filter((n) => n.startsWith(`${part}__`))]) {
    const up = await read(takes[name].up);
    const line = { name, canvas: `${up.w}x${up.h}` };
    if (up.w !== w || up.h !== h) { report.push({ ...line, problem: 'canvas differs from its part' }); continue; }
    const canvas = createCanvas(w, h);
    const ctx = canvas.getContext('2d');
    const image = ctx.createImageData(w, h);
    for (let p = 0; p < w * h; p++) {
      const a = alpha[p], i = p * 4;
      if (!a) continue;
      const k = a / 255;
      // Soft edge pixels were blended with the white backdrop: take the white back out.
      for (let c = 0; c < 3; c++) image.data[i + c] = a === 255 ? up.d[i + c] : Math.max(0, Math.min(255, (up.d[i + c] - (1 - k) * 255) / k));
      image.data[i + 3] = a;
    }
    ctx.putImageData(image, 0, 0);
    fs.writeFileSync(path.join(out, `${name}.png`), canvas.toBuffer('image/png'));
    finals[name] = canvas;

    if (name === part) {
      Object.assign(line, { box: `${x1 - x0 + 1}x${y1 - y0 + 1} at ${x0},${y0}`, islands, crumbsDeleted: dropped, ...(touches ? { problem: 'touches the canvas edge' } : {}) });
    } else {
      // A state against its part. The model redraws the whole piece and lets lines drift a pixel or
      // two everywhere; only where the change is DENSE is it the state (the eyes, the crystal). Those
      // blocks are kept from the state; every other pixel is copied from the part, so the two are
      // identical outside what changed.
      const base = finals[part].getContext('2d').getImageData(0, 0, w, h).data;
      const differs = (i) => Math.abs(base[i] - image.data[i]) + Math.abs(base[i + 1] - image.data[i + 1]) + Math.abs(base[i + 2] - image.data[i + 2]) > 90;
      const B = Math.max(8, Math.round(w / 64));
      const bw = Math.ceil(w / B), bh = Math.ceil(h / B);
      const count = new Uint32Array(bw * bh), total = new Uint32Array(bw * bh);
      let inside = 0, raw = 0;
      for (let p = 0; p < w * h; p++) {
        if (!alpha[p]) continue;
        inside++;
        const b = ((p / w / B) | 0) * bw + (((p % w) / B) | 0);
        total[b]++;
        if (differs(p * 4)) { count[b]++; raw++; }
      }
      const dense = new Uint8Array(bw * bh);
      for (let b = 0; b < bw * bh; b++) dense[b] = total[b] && count[b] / total[b] > 0.3 ? 1 : 0;
      const keep = new Uint8Array(bw * bh);       // dense blocks grown by one, so the change is whole
      for (let by = 0; by < bh; by++) for (let bx = 0; bx < bw; bx++) {
        if (!dense[by * bw + bx]) continue;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          const x = bx + dx, y = by + dy;
          if (x >= 0 && y >= 0 && x < bw && y < bh) keep[y * bw + x] = 1;
        }
      }
      let changed = 0, cx0 = w, cy0 = h, cx1 = -1, cy1 = -1;
      for (let p = 0; p < w * h; p++) {
        if (!alpha[p]) continue;
        const x = p % w, y = (p / w) | 0, i = p * 4;
        if (!keep[((y / B) | 0) * bw + ((x / B) | 0)]) {
          image.data[i] = base[i]; image.data[i + 1] = base[i + 1]; image.data[i + 2] = base[i + 2];
        } else if (differs(i)) {
          changed++;
          if (x < cx0) cx0 = x; if (x > cx1) cx1 = x; if (y < cy0) cy0 = y; if (y > cy1) cy1 = y;
        }
      }
      ctx.putImageData(image, 0, 0);
      fs.writeFileSync(path.join(out, `${name}.png`), canvas.toBuffer('image/png'));
      Object.assign(line, { sameSilhouetteAs: part, drawnDifferent: `${(100 * raw / inside).toFixed(1)}%`, keptAsTheState: `${(100 * changed / inside).toFixed(1)}%`,
        where: changed ? `${cx1 - cx0 + 1}x${cy1 - cy0 + 1} at ${cx0},${cy0}` : 'nothing', ...(changed ? {} : { problem: 'no change survived: the state is the same as its part' }) });
    }
    report.push(line);
  }
}

// ---- what to look at
const [W, H] = canvasSize.split('x').map(Number);
const stack = createCanvas(W, H);
const sctx = stack.getContext('2d');
sctx.fillStyle = '#1f3336';
sctx.fillRect(0, 0, W, H);
for (const part of (order || parts)) if (finals[part]) sctx.drawImage(finals[part], 0, 0);
fs.writeFileSync(path.join(out, '_assembled.png'), stack.toBuffer('image/png'));

const all = Object.keys(finals);
const cell = 512, cols = Math.min(5, all.length);
const sheet = createCanvas(cols * cell, Math.ceil(all.length / cols) * cell);
const c = sheet.getContext('2d');
all.forEach((name, i) => {
  const x = (i % cols) * cell, y = Math.floor(i / cols) * cell;
  c.fillStyle = (i + Math.floor(i / cols)) % 2 ? '#c2188c' : '#d02090';
  c.fillRect(x, y, cell, cell);
  c.drawImage(finals[name], x, y, cell, cell);
  c.fillStyle = '#fff';
  c.font = 'bold 24px sans-serif';
  c.fillText(name, x + 10, y + 30);
});
fs.writeFileSync(path.join(out, '_parts.png'), sheet.toBuffer('image/png'));

for (const line of report) console.log(`${line.problem ? '✗' : '✓'} ${JSON.stringify(line)}`);
console.log(`→ ${out}  (${all.length} PNGs, canvas ${canvasSize})`);
