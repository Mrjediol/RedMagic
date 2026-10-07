// Writes <folder>/final/<state>.png with the state's OWN silhouette (alpha of its _cut, colour of its _up):
// for a state that must be bigger than its part (an open hand). Usage: node fix-state-own-alpha.mjs <folder> <state>
import fs from 'node:fs';
import path from 'node:path';
import { createCanvas, loadImage } from '@napi-rs/canvas';
import { inOutput } from './contact-sheet.mjs';
const [folderArg, state] = process.argv.slice(2);
const folder = inOutput(folderArg);
const pick = (kind) => fs.readdirSync(folder).filter((f) => f.startsWith(`${state}_${kind}_`)).sort().pop();
const rd = async (f) => { const i = await loadImage(path.join(folder, f)); const c = createCanvas(i.width, i.height); const x = c.getContext('2d'); x.drawImage(i, 0, 0); return x.getImageData(0, 0, i.width, i.height); };
const up = await rd(pick('up')), cut = await rd(pick('cut'));
const { width: w, height: h } = up;
const lab = new Int32Array(w * h), sizes = [0], st = [];
for (let p = 0; p < w * h; p++) {
  if (cut.data[p * 4 + 3] <= 8 || lab[p]) continue;
  const id = sizes.length; let n = 0; st.push(p); lab[p] = id;
  while (st.length) { const q = st.pop(); n++; const x = q % w, y = (q / w) | 0;
    for (const [dx, dy] of [[1,0],[-1,0],[0,1],[0,-1]]) { const nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue; const r = ny * w + nx; if (!lab[r] && cut.data[r * 4 + 3] > 8) { lab[r] = id; st.push(r); } } }
  sizes.push(n);
}
const main = sizes.indexOf(Math.max(...sizes.slice(1)));
const out = createCanvas(w, h), ox = out.getContext('2d'), img = ox.createImageData(w, h);
for (let p = 0; p < w * h; p++) {
  if (lab[p] !== main) continue;
  const i = p * 4; let a = cut.data[i + 3]; a = a >= 250 ? 255 : a; const k = a / 255;
  for (let c = 0; c < 3; c++) img.data[i + c] = a === 255 ? up.data[i + c] : Math.max(0, Math.min(255, (up.data[i + c] - (1 - k) * 255) / k));
  img.data[i + 3] = a;
}
ox.putImageData(img, 0, 0);
fs.writeFileSync(path.join(folder, 'final', `${state}.png`), out.toBuffer('image/png'));
console.log('✓', state, 'own silhouette');
