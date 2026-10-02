// tools/bake-sounds.mjs
// -----------------------------------------------------------------------------
// Writes the synthesised sounds as .wav files into the Unity project, so they can be assigned from
// Biblioteca Jere ▸ Sounds like any other clip. Two sets, one synthesiser (modules/vfx-sounds.js):
//
//   · the VFX Lab's examples  (modules/vfx-sounds.js)  → Assets/Audio/Sfx/VfxLab/<id>.wav + sounds.json
//   · the game's palette      (modules/game-sounds.js) → Assets/Audio/Sfx/Game/<family>/<id>.wav + palette.json
//
//   node bake-sounds.mjs               (all of them)
//   node bake-sounds.mjs castFire heal (only those; the two indexes are always rewritten)
//
// 16-bit mono, 44.1 kHz. Files keep their names, so Unity keeps their GUIDs and any slot that already
// points at one keeps working after a re-bake.
// This script only writes clips: it assigns NOTHING. The Lab's examples are for picking by hand; the
// game's palette is assigned in Unity by Tools ▸ RedMagic ▸ Audio ▸ 4, which reads palette.json (the
// sounds and the rules "which slot gets which clip"). A sound that enters a slot has to be graded
// (docs/architecture/audio.md).
// Every game sound is checked before anything is written (check-sounds.mjs): a failed check stops the bake.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { checkSound } from './check-sounds.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '../../..');
const load = (file) => import(pathToFileURL(path.join(here, '../modules', file)).href);
const { SOUNDS, PRESET_SOUNDS, SAMPLE_RATE, renderSound, encodeWav } = await load('vfx-sounds.js');
const { GAME_SOUNDS, GAME_FAMILIES, GAME_ASSIGN, renderGameSound, assignedSoundIds } = await load('game-sounds.js');

const only = process.argv.slice(2);
const wanted = (id) => !only.length || only.includes(id);
const writeJson = (file, data) => fs.writeFileSync(file, `${JSON.stringify(data, null, 2)}\n`);

// ------------------------------------------------------------ the game's palette: check first
const assigned = assignedSoundIds();
const problems = [];
for (const rule of GAME_ASSIGN) if (rule.clip && !GAME_SOUNDS[rule.clip]) problems.push(`rule → '${rule.clip}': no such sound`);
for (const [id, def] of Object.entries(GAME_SOUNDS)) {
  if (SOUNDS[id]) problems.push(`${id}: id already used by a Lab sound`);
  if (!GAME_FAMILIES[def.family]) { problems.push(`${id}: unknown family '${def.family}'`); continue; }
  for (const issue of checkSound(renderGameSound(id), def, GAME_FAMILIES[def.family].max)) problems.push(`${id}: ${issue}`);
}
if (problems.length) {
  console.error(`✗ ${problems.length} problem(s); nothing was written:\n  ${problems.join('\n  ')}`);
  process.exit(1);
}

// ------------------------------------------------------------ the Lab's examples
const LAB = path.join(project, 'Assets/Audio/Sfx/VfxLab');
fs.mkdirSync(LAB, { recursive: true });
const index = {};
let lab = 0;
for (const [id, def] of Object.entries(SOUNDS)) {
  const usedBy = Object.entries(PRESET_SOUNDS).filter(([, map]) => Object.values(map).includes(id)).map(([preset]) => preset);
  index[id] = { label: def.label, seconds: def.seconds, loop: !!def.loop, usedBy };
  if (!wanted(id)) continue;
  fs.writeFileSync(path.join(LAB, `${id}.wav`), encodeWav(renderSound(id), SAMPLE_RATE));
  lab++;
}
writeJson(path.join(LAB, 'sounds.json'), index);

// ------------------------------------------------------------ the game's palette
const GAME = path.join(project, 'Assets/Audio/Sfx/Game');
const sounds = {};
let game = 0;
for (const [id, def] of Object.entries(GAME_SOUNDS)) {
  sounds[id] = { family: def.family, label: def.label, seconds: def.seconds, loop: !!def.loop, assigned: assigned.has(id) };
  if (!wanted(id)) continue;
  fs.mkdirSync(path.join(GAME, def.family), { recursive: true });
  fs.writeFileSync(path.join(GAME, def.family, `${id}.wav`), encodeWav(renderGameSound(id), SAMPLE_RATE));
  game++;
}
writeJson(path.join(GAME, 'palette.json'), {
  _comment: 'GENERADO por Web/RedMagicWeb/tools/bake-sounds.mjs desde modules/game-sounds.js. No editar a mano. '
    + 'Clip de un sonido: <esta carpeta>/<family>/<id>.wav. Las reglas las aplica Tools > RedMagic > Audio > 4.',
  families: GAME_FAMILIES,
  sounds,
  rules: GAME_ASSIGN,
});

const rel = (dir) => path.relative(project, dir).replace(/\\/g, '/');
console.log(`✓ ${lab} Lab sound(s) → ${rel(LAB)}`);
console.log(`✓ ${game} game sound(s) → ${rel(GAME)}  (${Object.keys(GAME_SOUNDS).length} in the palette, ${assigned.size} assigned by rules, ${GAME_ASSIGN.length} rules)`);
