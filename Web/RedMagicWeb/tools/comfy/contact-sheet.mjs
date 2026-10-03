// tools/comfy/contact-sheet.mjs
// -----------------------------------------------------------------------------
// Several images side by side in one PNG, to look at a batch at once (Claude and the user).
//
//   node contact-sheet.mjs <out.png> [--cell 512] [--cols 3] [--bg #ffffff] [--under base.png] [--tight] a.png b.png …
//     --tight  every cell shows only the box where something is drawn (union over all images, same canvas):
//              for states / small parts, which are lost on a full 1024 canvas
//     --under  draws that image faded beneath each cell: shows whether a part sits where it sat in the base
//   Names are relative to Comfy's output folder unless absolute.

import fs from 'node:fs';
import path from 'node:path';
import { createCanvas, loadImage } from '@napi-rs/canvas';

export const COMFY_OUTPUT = process.env.COMFY_OUTPUT
  || path.join(process.env.LOCALAPPDATA || '', 'Comfy-Desktop/ComfyUI-Shared/output');
export const inOutput = (file) => (path.isAbsolute(file) ? file : path.join(COMFY_OUTPUT, file));

const args = process.argv.slice(2);
if (process.argv[1]?.endsWith('contact-sheet.mjs')) {
  const out = args.shift();
  let cell = 512, cols = 3, bg = '#ffffff', under = null, tight = false;
  const files = [];
  while (args.length) {
    const a = args.shift();
    if (a === '--cell') cell = Number(args.shift());
    else if (a === '--cols') cols = Number(args.shift());
    else if (a === '--bg') bg = args.shift();
    else if (a === '--tight') tight = true;
    else if (a === '--under') under = await loadImage(inOutput(args.shift()));
    else files.push(a);
  }
  const images = [];
  for (const file of files) images.push(await loadImage(inOutput(file)));
  const box = tight ? drawnBox(images) : null;
  const rows = Math.ceil(files.length / cols);
  const canvas = createCanvas(cols * cell, rows * cell);
  const ctx = canvas.getContext('2d');
  ctx.fillStyle = bg;
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  for (let i = 0; i < files.length; i++) {
    const img = images[i];
    const src = box || { x: 0, y: 0, w: img.width, h: img.height };
    const x = (i % cols) * cell, y = Math.floor(i / cols) * cell;
    const k = Math.min(cell / src.w, cell / src.h);
    if (under) { ctx.globalAlpha = 0.25; ctx.drawImage(under, src.x, src.y, src.w, src.h, x, y, src.w * k, src.h * k); ctx.globalAlpha = 1; ctx.globalCompositeOperation = 'multiply'; }
    ctx.drawImage(img, src.x, src.y, src.w, src.h, x, y, src.w * k, src.h * k);
    ctx.globalCompositeOperation = 'source-over';
    ctx.fillStyle = '#d02090';
    ctx.font = `bold ${Math.round(cell / 22)}px sans-serif`;
    ctx.fillText(path.basename(files[i]).replace(/_raw_0000(d+)_/, ' #$1').replace(/_raw|_cut|_0000\d_|\.png/g, ''), x + 8, y + cell / 18);
    ctx.strokeStyle = '#bbb';
    ctx.strokeRect(x + 0.5, y + 0.5, cell - 1, cell - 1);
  }
  fs.writeFileSync(inOutput(out), canvas.toBuffer('image/png'));
  console.log(`✓ ${inOutput(out)}`);
}

/** The box (with a small margin) holding every pixel that is not near-white or transparent, over all the images. */
function drawnBox(images) {
  let x0 = Infinity, y0 = Infinity, x1 = -1, y1 = -1;
  for (const img of images) {
    const c = createCanvas(img.width, img.height), g = c.getContext('2d');
    g.drawImage(img, 0, 0);
    const d = g.getImageData(0, 0, img.width, img.height).data;
    for (let y = 0; y < img.height; y++) for (let x = 0; x < img.width; x++) {
      const i = (y * img.width + x) * 4;
      if (d[i + 3] < 32 || (d[i] > 235 && d[i + 1] > 235 && d[i + 2] > 235)) continue;
      if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
    }
  }
  if (x1 < 0) return null;
  const pad = Math.round(Math.max(x1 - x0, y1 - y0) * 0.06);
  const w = images[0].width, h = images[0].height;
  x0 = Math.max(0, x0 - pad); y0 = Math.max(0, y0 - pad);
  return { x: x0, y: y0, w: Math.min(w, x1 + pad + 1) - x0, h: Math.min(h, y1 + pad + 1) - y0 };
}
