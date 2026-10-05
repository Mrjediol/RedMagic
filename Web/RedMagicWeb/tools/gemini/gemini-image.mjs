// tools/gemini/gemini-image.mjs
// -----------------------------------------------------------------------------
// One image from Google's Gemini image model (the API), for 🎨 Designs proposals: the alternative to
// claude-workflow.mjs (ComfyUI) that the request form offers — and its default (docs/architecture/designs.md).
// The PNG is saved under Comfy's output folder (output/claude/…), the same place ComfyUI saves to, so the batch item
// is still "comfy:claude/<name>.png" and make-enemy takes it as its base with no change. ComfyUI does NOT need to be open.
//
//   node gemini-image.mjs --name des_x_1 --prompt "…" [--ref a.png,b.png] [--aspect 1:1] [--model id]
//       --ref       reference images (absolute paths, or relative to Comfy's output): "like this one"
//       --aspect    1:1 (default), 3:4, 4:3, 9:16, 16:9…
//       --model     default env GEMINI_IMAGE_MODEL, else MODELS[0]; a model the key cannot use (404) falls to the next
//   prints: ✓ … → output/claude/<name>.png
//
// Key: env GEMINI_API_KEY, or the file tools/gemini/api-key.txt (git-ignored). Get one at https://aistudio.google.com/apikey.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createCanvas, loadImage } from '@napi-rs/canvas';
import { COMFY_OUTPUT, inOutput } from '../comfy/contact-sheet.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const API = 'https://generativelanguage.googleapis.com/v1beta/models';
export const MODELS = ['gemini-3-pro-image-preview', 'gemini-2.5-flash-image'];
const MIME = { '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.webp': 'image/webp', '.gif': 'image/gif' };

function apiKey() {
  if (process.env.GEMINI_API_KEY) return process.env.GEMINI_API_KEY.trim();
  const file = path.join(HERE, 'api-key.txt');
  if (fs.existsSync(file)) return fs.readFileSync(file, 'utf8').trim();
  throw new Error(`no Gemini key: set GEMINI_API_KEY or write it in ${file}`);
}

function options(argv) {
  const o = { refs: [], aspect: '1:1' };
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    const value = () => argv[++i];
    if (flag === '--prompt') o.prompt = value();
    else if (flag === '--ref') o.refs = value().split(',').map((s) => s.trim()).filter(Boolean);
    else if (flag === '--aspect') o.aspect = value();
    else if (flag === '--model') o.model = value();
    else if (flag === '--name') o.name = value();
    else throw new Error(`unknown option ${flag}`);
  }
  if (!o.prompt) throw new Error('--prompt is required');
  if (!o.name || !/^[\w-]+$/.test(o.name)) throw new Error('--name is required (letters, numbers, _ and -)');
  return o;
}

/** One call to one model → the first image's bytes. Throws { status } so a 404 can fall to the next model. */
async function generate(model, { prompt, refs, aspect }) {
  const parts = [{ text: prompt }, ...refs.map((file) => ({
    inline_data: { mime_type: MIME[path.extname(file).toLowerCase()] || 'image/png', data: fs.readFileSync(file).toString('base64') },
  }))];
  const imageConfig = { aspectRatio: aspect, ...(model.startsWith('gemini-3') ? { imageSize: '1K' } : {}) };
  const res = await fetch(`${API}/${model}:generateContent`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'x-goog-api-key': apiKey() },
    body: JSON.stringify({ contents: [{ role: 'user', parts }], generationConfig: { responseModalities: ['TEXT', 'IMAGE'], imageConfig } }),
  });
  if (!res.ok) throw Object.assign(new Error(`${model} → ${res.status} ${(await res.text()).slice(0, 400)}`), { status: res.status });
  const out = await res.json();
  const all = (out.candidates || []).flatMap((c) => c.content?.parts || []);
  const image = all.find((p) => p.inlineData?.data || p.inline_data?.data);
  if (!image) {
    const why = out.promptFeedback?.blockReason || out.candidates?.[0]?.finishReason || all.map((p) => p.text).filter(Boolean).join(' ') || 'no image';
    throw new Error(`${model} returned no image (${String(why).slice(0, 300)})`);
  }
  return Buffer.from((image.inlineData || image.inline_data).data, 'base64');
}

async function main() {
  const o = options(process.argv.slice(2));
  o.refs = o.refs.map(inOutput);
  for (const r of o.refs) if (!fs.existsSync(r)) throw new Error(`no such reference image: ${r}`);
  const models = o.model ? [o.model] : [...new Set([process.env.GEMINI_IMAGE_MODEL, ...MODELS].filter(Boolean))];
  const started = Date.now();
  let bytes = null, used = null;
  for (const model of models) {
    try { bytes = await generate(model, o); used = model; break; }
    catch (err) { if (err.status === 404 && model !== models.at(-1)) { console.log(`· ${model} not available, trying the next`); continue; } throw err; }
  }
  // Always a PNG (Gemini may answer JPEG): the rest of the pipeline reads .png from Comfy's output.
  const img = await loadImage(bytes);
  const canvas = createCanvas(img.width, img.height);
  canvas.getContext('2d').drawImage(img, 0, 0);
  const rel = `claude/${o.name}.png`;
  const file = path.join(COMFY_OUTPUT, rel);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, canvas.toBuffer('image/png'));
  console.log(`✓ ${used} ${((Date.now() - started) / 1000).toFixed(0)} s, ${img.width}×${img.height} → output/${rel}`);
}

main().catch((err) => { console.error(`✗ ${err.message}`); process.exit(1); });
