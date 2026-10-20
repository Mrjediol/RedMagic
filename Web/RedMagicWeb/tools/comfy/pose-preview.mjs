// tools/comfy/pose-preview.mjs
// -----------------------------------------------------------------------------
// Prueba de movimiento de un enemigo por piezas ANTES de Unity: gira cada pieza en su articulación, las monta en su orden
// de profundidad y dibuja varias poses (andar, ataques) en una sola hoja. Sirve para ver lo que el recorte no enseña:
// un extremo que asoma al girar (un gancho en vez de una rótula), un hueco en una articulación, una pieza al revés.
// Regla (usuario 2026-10-20, Golem de escoria): «las piezas tienen que considerar que van a usarse para generar movimiento».
//
//   node pose-preview.mjs <config.json> <out.png> [--cell 360]
//   config = {
//     parts: [{ name, file (relativo a la carpeta output de Comfy, o absoluto), pivot: [fx, fy], parent? }],   // pivot: fracción de la
//                                                         // caja de lo dibujado (0,0 arriba-izquierda) = dónde gira;
//                                                         // parent: la pieza de la que cuelga (como en el rig: gira con ella)
//     order: ["Atrás", …, "Delante"],                    // orden de dibujo (de atrás adelante)
//     poses: [{ name, rot: { Pieza: grados } }]          // grados en pantalla: + = sentido horario
//   }
// Las piezas están sobre blanco (sin recortar): el blanco se vuelve transparente sólo para esta vista previa.
import fs from 'node:fs';
import path from 'node:path';
import { createCanvas, loadImage } from '@napi-rs/canvas';

const OUT_DIR = path.join(process.env.LOCALAPPDATA || '', 'Comfy-Desktop', 'ComfyUI-Shared', 'output');
const args = process.argv.slice(2), opt = (k, d) => { const i = args.indexOf(k); return i >= 0 ? args[i + 1] : d; };
const [cfgFile, outFile] = args.filter((a, i) => !a.startsWith('--') && !(i > 0 && args[i - 1].startsWith('--')));
if (!cfgFile || !outFile) { console.error('uso: node pose-preview.mjs <config.json> <out.png> [--cell 360]'); process.exit(1); }
const cfg = JSON.parse(fs.readFileSync(cfgFile, 'utf8')), CELL = +opt('--cell', 360);
const resolve = f => path.isAbsolute(f) ? f : path.join(OUT_DIR, f);

// cada pieza: lienzo con el blanco quitado + caja de lo dibujado + pivote en píxeles
const parts = {};
let W = 0, H = 0;
for (const p of cfg.parts) {
  const img = await loadImage(resolve(p.file)); W = img.width; H = img.height;
  const c = createCanvas(W, H), x = c.getContext('2d'); x.drawImage(img, 0, 0);
  const d = x.getImageData(0, 0, W, H), a = d.data; let x0 = W, y0 = H, x1 = 0, y1 = 0;
  let hasAlpha = false; for (let i = 3; i < a.length; i += 4 * 97) if (a[i] < 250) { hasAlpha = true; break; }
  for (let i = 0; i < a.length; i += 4) {
    if (!hasAlpha) { const m = Math.min(a[i], a[i + 1], a[i + 2]); if (m > 238) a[i + 3] = 0; else if (m > 215) a[i + 3] = Math.round(255 * (238 - m) / 23); }   // pieza sin recortar: vista aproximada
    if (a[i + 3] > 40) { const px = (i / 4) % W, py = Math.floor(i / 4 / W); if (px < x0) x0 = px; if (px > x1) x1 = px; if (py < y0) y0 = py; if (py > y1) y1 = py; }
  }
  x.putImageData(d, 0, 0);
  // pivote: [fx, fy] de la caja, { px: [x, y] } en píxeles del lienzo, o "joint-top" / "joint-bottom": el centro de la rótula
  // (la fila donde el extremo redondo alcanza su ancho, medido en la máscara: nunca a ojo)
  const span = y => { let l = -1, r = -1; for (let xx = x0; xx <= x1; xx++) if (a[(y * W + xx) * 4 + 3] > 128) { if (l < 0) l = xx; r = xx; } return [l, r]; };
  let px, py;
  if (p.pivot === 'joint-top' || p.pivot === 'joint-bottom') {
    const top = p.pivot === 'joint-top', h = y1 - y0, band = Math.round(h * 0.22), rows = [];
    for (let k = 0; k <= band; k++) { const y = top ? y0 + k : y1 - k, [l, r] = span(y); rows.push({ y, l, r, w: l < 0 ? 0 : r - l }); }
    const wmax = Math.max(...rows.map(q => q.w)), row = rows.find(q => q.w >= wmax * 0.92);
    px = (row.l + row.r) / 2; py = row.y;
  } else if (p.pivot && p.pivot.px) { [px, py] = p.pivot.px; }
  else { px = x0 + (x1 - x0) * p.pivot[0]; py = y0 + (y1 - y0) * p.pivot[1]; }
  parts[p.name] = { c, parent: p.parent, px, py };
}
const cols = Math.min(cfg.poses.length, +opt('--cols', 4)), rows = Math.ceil(cfg.poses.length / cols);
const sheet = createCanvas(cols * CELL, rows * (CELL + 24)), s = sheet.getContext('2d');
s.fillStyle = '#5a5f6a'; s.fillRect(0, 0, sheet.width, sheet.height);
cfg.poses.forEach((pose, k) => {
  const cx = (k % cols) * CELL, cy = Math.floor(k / cols) * (CELL + 24), sc = CELL / Math.max(W, H);
  s.save(); s.beginPath(); s.rect(cx, cy + 24, CELL, CELL); s.clip(); s.translate(cx, cy + 24); s.scale(sc, sc);
  for (const name of cfg.order) {
    const p = parts[name]; if (!p) continue; const r = ((pose.rot || {})[name] || 0) * Math.PI / 180;
    s.save();
    const par = p.parent && parts[p.parent]; if (par) { const pr = ((pose.rot || {})[p.parent] || 0) * Math.PI / 180; s.translate(par.px, par.py); s.rotate(pr); s.translate(-par.px, -par.py); }
    s.translate(p.px, p.py); s.rotate(r); s.translate(-p.px, -p.py); s.drawImage(p.c, 0, 0); s.restore();
    if (pose.pivots) { s.fillStyle = '#ff3b6b'; s.beginPath(); s.arc(p.px, p.py, 7 / sc * 0.6, 0, Math.PI * 2); s.fill(); }
  }
  s.restore();
  s.fillStyle = '#ffffff'; s.font = '600 15px sans-serif'; s.fillText(pose.name, cx + 8, cy + 17);
});
fs.writeFileSync(resolve(outFile), sheet.toBuffer('image/png'));
console.log('✓', resolve(outFile));
