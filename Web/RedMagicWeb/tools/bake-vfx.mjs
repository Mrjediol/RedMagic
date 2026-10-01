// tools/bake-vfx.mjs
// -----------------------------------------------------------------------------
// Bakes ✨ VFX Lab presets straight into the Unity project, with no browser: the game's OWN effects
// (a weapon's projectile, its impact, a charge loop, the heal burst…) come from the same presets the
// Lab offers, drawn by the same code (modules/vfx-presets.js) on a real canvas (@napi-rs/canvas).
//
//   cd Web/RedMagicWeb/tools && npm install        (once)
//   node bake-vfx.mjs                              (all the bakes of vfx-bakes.json)
//   node bake-vfx.mjs Fireball ArcaneBeam          (only those)
//
// WHAT TO BAKE is data: vfx-bakes.json — `{ name, preset, params, lanes? }` per effect (`params`
// override the preset's defaults; `lanes` keeps only those lanes). Each bake writes
//   Assets/Art/VfxLab/<name>/<Lane>/frame_000.png …   one PNG per frame
//   Assets/Art/VfxLab/<name>/bake.json                 preset, params, frame size, fps / loop per lane
// and Unity turns them into prefabs and wires them: Tools ▸ RedMagic ▸ FX ▸ 5 · VFX Lab · Arte del
// juego (Assets/Scripts/Fx/Editor/VfxLabArtPack.cs — the table of who uses which bake lives there).
// A folder is rewritten whole on each bake; frames keep their names, so Unity keeps their GUIDs
// (.meta files are left alone) and prefabs keep pointing at them.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createCanvas } from '@napi-rs/canvas';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../..');
const OUT = path.join(repo, 'Assets/Art/VfxLab');

const { presetById, bakePreset, normalizeParams } = await import(pathToFileURL(path.join(here, '../modules/vfx-presets.js')).href);
const bakes = JSON.parse(fs.readFileSync(path.join(here, 'vfx-bakes.json'), 'utf8')).bakes;
const only = process.argv.slice(2);
const make = (w, h) => createCanvas(w, h);

let failed = 0;
for (const bake of bakes) {
  if (only.length && !only.includes(bake.name)) continue;
  const preset = presetById(bake.preset);
  if (!preset) { console.error(`✗ ${bake.name}: no preset '${bake.preset}'`); failed++; continue; }

  const params = normalizeParams(preset, bake.params || {});
  const baked = bakePreset(preset, params, make);
  const lanes = baked.lanes.filter((l) => !bake.lanes || bake.lanes.includes(l.name));
  if (!lanes.length) { console.error(`✗ ${bake.name}: none of the lanes ${bake.lanes} exist in '${bake.preset}'`); failed++; continue; }

  const folder = path.join(OUT, bake.name);
  fs.mkdirSync(folder, { recursive: true });
  // Stale lanes / frames of an earlier bake go (with their .meta); what is rewritten keeps its .meta.
  const wanted = new Set(lanes.map((l) => l.name));
  for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
    if (entry.isDirectory() && !wanted.has(entry.name)) {
      fs.rmSync(path.join(folder, entry.name), { recursive: true });
      fs.rmSync(path.join(folder, `${entry.name}.meta`), { force: true });
    }
  }

  const manifest = { name: bake.name, preset: preset.id, kind: preset.kind, params, frameSize: 0, density: baked.density, lanes: [] };
  for (const lane of lanes) {
    const dir = path.join(folder, lane.name);
    fs.mkdirSync(dir, { recursive: true });
    const names = new Set();
    lane.frameBoxIndices.forEach((bi, i) => {
      const box = baked.boxes[bi];
      const frame = createCanvas(box.w, box.h);
      frame.getContext('2d').drawImage(baked.canvas, box.x, box.y, box.w, box.h, 0, 0, box.w, box.h);
      const file = `frame_${String(i).padStart(3, '0')}.png`;
      names.add(file);
      fs.writeFileSync(path.join(dir, file), frame.toBuffer('image/png'));
      manifest.frameSize = box.w;
    });
    for (const file of fs.readdirSync(dir)) {
      if (file.endsWith('.png') && !names.has(file)) {
        fs.rmSync(path.join(dir, file));
        fs.rmSync(path.join(dir, `${file}.meta`), { force: true });
      }
    }
    manifest.lanes.push({ name: lane.name, fps: lane.fps, loop: lane.loop, frames: lane.frameBoxIndices.length });
  }
  fs.writeFileSync(path.join(folder, 'bake.json'), `${JSON.stringify(manifest, null, 2)}\n`);
  console.log(`✓ ${bake.name}  (${preset.id}: ${manifest.lanes.map((l) => `${l.name} ${l.frames}f`).join(', ')}, ${manifest.frameSize}px)`);
}
process.exit(failed ? 1 : 0);
