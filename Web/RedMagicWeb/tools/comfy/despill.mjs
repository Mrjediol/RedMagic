// tools/comfy/despill.mjs
// -----------------------------------------------------------------------------
// Removes the flat-background colour that bleeds into the soft edges of a cut-out (the pink rim left by RMBG on
// art painted over magenta). Pure Node, no dependencies: reads/writes 8-bit RGBA/RGB PNGs itself.
//
//   node despill.mjs in.png out.png [--key magenta|white|none] [--strength 1] [--erode px]
//
// magenta: m = min(r,b) − g; where m > 0 subtract it from r and b (a blue/cyan palette has min(r,b) < g, so
//          untouched). white: pull near-white semi-transparent edge pixels toward their own hue.
// Only pixels with alpha < 255 or a clear tint are touched, so the interior of the art is unchanged.
import fs from 'node:fs';
import zlib from 'node:zlib';

const CRC = (() => { const t = new Uint32Array(256); for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } return t; })();
const crc32 = (buf) => { let c = 0xffffffff; for (const b of buf) c = CRC[(c ^ b) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };

export function readPng(file) {
  const b = fs.readFileSync(file);
  let p = 8, w, h, depth, type, idat = [];
  while (p < b.length) {
    const len = b.readUInt32BE(p), kind = b.toString('ascii', p + 4, p + 8), data = b.subarray(p + 8, p + 8 + len);
    if (kind === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); depth = data[8]; type = data[9]; if (data[12]) throw new Error('interlaced PNG not supported'); }
    else if (kind === 'IDAT') idat.push(data);
    else if (kind === 'IEND') break;
    p += 12 + len;
  }
  if (depth !== 8 || (type !== 6 && type !== 2)) throw new Error(`only 8-bit RGB/RGBA PNG (got depth ${depth}, type ${type})`);
  const bpp = type === 6 ? 4 : 3, stride = w * bpp, raw = zlib.inflateSync(Buffer.concat(idat)), px = Buffer.alloc(w * h * 4);
  let prev = Buffer.alloc(stride), row = Buffer.alloc(stride);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], src = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    for (let i = 0; i < stride; i++) {
      const a = i >= bpp ? row[i - bpp] : 0, up = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
      let v = src[i];
      if (f === 1) v += a; else if (f === 2) v += up; else if (f === 3) v += (a + up) >> 1;
      else if (f === 4) { const pp = a + up - c, pa = Math.abs(pp - a), pb = Math.abs(pp - up), pc = Math.abs(pp - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? up : c; }
      row[i] = v & 255;
    }
    for (let x = 0; x < w; x++) { const o = (y * w + x) * 4, s = x * bpp; px[o] = row[s]; px[o + 1] = row[s + 1]; px[o + 2] = row[s + 2]; px[o + 3] = bpp === 4 ? row[s + 3] : 255; }
    [prev, row] = [row, prev];
  }
  return { w, h, px };
}

export function writePng(file, { w, h, px }) {
  const raw = Buffer.alloc(h * (w * 4 + 1));
  for (let y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0; px.copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4); }
  const chunk = (kind, data) => { const len = Buffer.alloc(4); len.writeUInt32BE(data.length); const kd = Buffer.concat([Buffer.from(kind, 'ascii'), data]); const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(kd)); return Buffer.concat([len, kd, crc]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
  fs.writeFileSync(file, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw, { level: 6 })), chunk('IEND', Buffer.alloc(0))]));
}

// magenta, conservative: ONLY a thin band next to the transparent area is touched (the interior and its textures stay
// exactly as they are), and inside it only pixels with a clear magenta tint (min(r,b) − g > minM). Those take the colour
// of the nearest untinted pixel and lose alpha in proportion to their magenta, so a solid magenta stripe vanishes.
// (A first version also recoloured the interior and smeared dark-navy textures: keep it to the band.)
const nb = (j, w, h) => { const x = j % w, y = (j / w) | 0; return [x > 0 ? j - 1 : -1, x < w - 1 ? j + 1 : -1, y > 0 ? j - w : -1, y < h - 1 ? j + w : -1]; };
function decontaminateMagenta(img, { strength = 1, band = 10, minM = 6 } = {}) {
  const { w, h, px: p } = img, n = w * h, m = new Int16Array(n), dist = new Uint8Array(n).fill(255), bad = new Uint8Array(n), src = new Int32Array(n).fill(-1);
  for (let j = 0; j < n; j++) { const i = j * 4; m[j] = Math.min(p[i], p[i + 2]) - p[i + 1]; }
  // distance (in px, up to band) to the transparent area
  let front = []; for (let j = 0; j < n; j++) if (p[j * 4 + 3] < 128) { dist[j] = 0; front.push(j); }
  for (let d = 1; d <= band && front.length; d++) { const next = []; for (const j of front) for (const k of nb(j, w, h)) if (k >= 0 && dist[k] === 255) { dist[k] = d; next.push(k); } front = next; }
  for (let j = 0; j < n; j++) if (dist[j] <= band && p[j * 4 + 3] > 0 && m[j] > minM) bad[j] = 1;
  // nearest untinted visible pixel, spreading only through the tinted ones
  front = [];
  for (let j = 0; j < n; j++) if (!bad[j] && p[j * 4 + 3] >= 128 && m[j] <= 0 && nb(j, w, h).some((k) => k >= 0 && bad[k])) { src[j] = j; front.push(j); }
  for (let s = 0; s < band * 3 && front.length; s++) { const next = []; for (const j of front) for (const k of nb(j, w, h)) if (k >= 0 && bad[k] && src[k] < 0) { src[k] = src[j]; next.push(k); } front = next; }
  let touched = 0;
  for (let j = 0; j < n; j++) { if (!bad[j]) continue; const i = j * 4, t = Math.min(1, (m[j] / 255) * strength * 1.6);
    if (src[j] >= 0) { const s = src[j] * 4; p[i] = p[s]; p[i + 1] = p[s + 1]; p[i + 2] = p[s + 2]; }
    p[i + 3] = t > 0.9 ? 0 : Math.round(p[i + 3] * (1 - t)); touched++; }
  return touched;
}

export function despill(img, { key = 'magenta', strength = 1 } = {}) {
  if (key === 'magenta') return decontaminateMagenta(img, { strength });
  const p = img.px;
  let touched = 0;
  for (let i = 0; i < p.length; i += 4) {
    if (p[i + 3] === 0) continue;
    if (key === 'magenta') {
      // un-mix: the pixel is C = (1−t)·F + t·M with M = pure magenta; t ≈ how much magenta it holds
      const m = Math.min(p[i], p[i + 2]) - p[i + 1];
      if (m > 0) {
        const t = Math.min(1, (m / 255) * strength);
        if (t > 0.97) { p[i + 3] = 0; touched++; continue; }
        const k = 1 / (1 - t);
        p[i] = Math.max(0, Math.min(255, (p[i] - t * 255) * k));
        p[i + 1] = Math.max(0, Math.min(255, p[i + 1] * k));
        p[i + 2] = Math.max(0, Math.min(255, (p[i + 2] - t * 255) * k));
        p[i + 3] = Math.round(p[i + 3] * (1 - t));
        touched++;
      }
    } else if (key === 'white' && p[i + 3] < 255) {
      const lo = Math.min(p[i], p[i + 1], p[i + 2]), k = (1 - p[i + 3] / 255) * strength;
      if (lo > 0) { for (let c = 0; c < 3; c++) p[i + c] = Math.max(0, p[i + c] - lo * k); touched++; }
    }
  }
  return touched;
}

// Shrinks the alpha by N px (min filter): drops a thin rim of the old background (white halo) around art that has
// a thick dark outline. Only for outlined art; soft painted edges lose their shape.
export function erode(img, radius) {
  const { w, h, px } = img; let a = new Uint8Array(w * h);
  for (let j = 0; j < w * h; j++) a[j] = px[j * 4 + 3];
  for (let r = 0; r < radius; r++) { const b = a.slice();
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const j = y * w + x;
      b[j] = Math.min(a[j], x > 0 ? a[j - 1] : 0, x < w - 1 ? a[j + 1] : 0, y > 0 ? a[j - w] : 0, y < h - 1 ? a[j + w] : 0); }
    a = b; }
  for (let j = 0; j < w * h; j++) px[j * 4 + 3] = a[j];
}

if (process.argv[1]?.endsWith('despill.mjs')) {
  const [inp, out, ...rest] = process.argv.slice(2);
  if (!inp || !out) { console.log('usage: node despill.mjs in.png out.png [--key magenta|white] [--strength 1]'); process.exit(1); }
  const opt = {}; for (let i = 0; i < rest.length; i += 2) opt[rest[i].replace(/^--/, '')] = rest[i + 1];
  const img = readPng(inp), n = opt.key === 'none' ? 0 : despill(img, { key: opt.key || 'magenta', strength: Number(opt.strength || 1) });
  if (opt.erode) erode(img, Number(opt.erode));
  writePng(out, img);
  console.log(`✓ ${img.w}×${img.h}, ${n} pixels despilled → ${out}`);
}
