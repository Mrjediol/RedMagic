// tools/comfy/contact-sheet.mjs
// -----------------------------------------------------------------------------
// Several images side by side in one PNG, to look at a batch at once (Claude and the user).
//
//   node contact-sheet.mjs <out.png> [--cell 512] [--cols 3] [--bg #ffffff] [--under base.png] a.png b.png …
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
  let cell = 512, cols = 3, bg = '#ffffff', under = null;
  const files = [];
  while (args.length) {
    const a = args.shift();
    if (a === '--cell') cell = Number(args.shift());
    else if (a === '--cols') cols = Number(args.shift());
    else if (a === '--bg') bg = args.shift();
    else if (a === '--under') under = await loadImage(inOutput(args.shift()));
    else files.push(a);
  }
  const rows = Math.ceil(files.length / cols);
  const canvas = createCanvas(cols * cell, rows * cell);
  const ctx = canvas.getContext('2d');
  ctx.fillStyle = bg;
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  for (let i = 0; i < files.length; i++) {
    const img = await loadImage(inOutput(files[i]));
    const x = (i % cols) * cell, y = Math.floor(i / cols) * cell;
    const k = Math.min(cell / img.width, cell / img.height);
    if (under) { ctx.globalAlpha = 0.25; ctx.drawImage(under, x, y, under.width * k, under.height * k); ctx.globalAlpha = 1; ctx.globalCompositeOperation = 'multiply'; }
    ctx.drawImage(img, x, y, img.width * k, img.height * k);
    ctx.globalCompositeOperation = 'source-over';
    ctx.fillStyle = '#d02090';
    ctx.font = `bold ${Math.round(cell / 22)}px sans-serif`;
    ctx.fillText(path.basename(files[i]).replace(/_raw|_cut|_0000\d_|\.png/g, ''), x + 8, y + cell / 18);
    ctx.strokeStyle = '#bbb';
    ctx.strokeRect(x + 0.5, y + 0.5, cell - 1, cell - 1);
  }
  fs.writeFileSync(inOutput(out), canvas.toBuffer('image/png'));
  console.log(`✓ ${inOutput(out)}`);
}
