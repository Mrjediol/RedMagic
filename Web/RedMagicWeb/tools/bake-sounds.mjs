// tools/bake-sounds.mjs
// -----------------------------------------------------------------------------
// Writes the VFX Lab's example sounds (modules/vfx-sounds.js — synthesised in code) as .wav files
// into the Unity project, so they can be assigned from Biblioteca Jere ▸ Sounds like any other clip.
//
//   node bake-sounds.mjs               (all of them)
//   node bake-sounds.mjs castFire heal (only those)
//
// Output: Assets/Audio/Sfx/VfxLab/<id>.wav (16-bit mono, 44.1 kHz) + sounds.json (label, seconds,
// loop, and which presets use each one). Files keep their names, so Unity keeps their GUIDs and any
// slot that already points at one keeps working after a re-bake.
// This script only writes clips: it assigns NOTHING. A sound enters the game when it is put in a
// slot — and then it has to be graded (docs/architecture/audio.md).

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.resolve(here, '../../../Assets/Audio/Sfx/VfxLab');
const { SOUNDS, PRESET_SOUNDS, SAMPLE_RATE, renderSound, encodeWav } = await import(pathToFileURL(path.join(here, '../modules/vfx-sounds.js')).href);

const only = process.argv.slice(2);
fs.mkdirSync(OUT, { recursive: true });
const index = {};
for (const [id, def] of Object.entries(SOUNDS)) {
  const usedBy = Object.entries(PRESET_SOUNDS).filter(([, map]) => Object.values(map).includes(id)).map(([preset]) => preset);
  index[id] = { label: def.label, seconds: def.seconds, loop: !!def.loop, usedBy };
  if (only.length && !only.includes(id)) continue;
  fs.writeFileSync(path.join(OUT, `${id}.wav`), encodeWav(renderSound(id), SAMPLE_RATE));
}
fs.writeFileSync(path.join(OUT, 'sounds.json'), `${JSON.stringify(index, null, 2)}\n`);
console.log(`✓ ${only.length || Object.keys(SOUNDS).length} sound(s) → ${path.relative(path.resolve(here, '../../..'), OUT)}`);
