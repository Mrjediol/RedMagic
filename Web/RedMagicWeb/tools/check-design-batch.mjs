// tools/check-design-batch.mjs
// -----------------------------------------------------------------------------
// Checks a 🎨 Designs batch (docs/disenos/<id>.json) before a design task ends — that the web can show every
// proposal and that accepting it would work. Format: docs/architecture/designs.md.
//
//   node check-design-batch.mjs ../../../docs/disenos/<id>.json
//
// Per item: id / kind / name format · enemy: the image exists · projectile / fx: the VFX Lab preset exists and is of
// that kind · sound: known family, passes check-sounds (peak, clicks, loop seam, family length) and its id is free.
// Exit code 1 if anything is wrong.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { checkSound } from './check-sounds.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const load = (file) => import(pathToFileURL(path.join(here, '../modules', file)).href);
const { presetById, paramsOf } = await load('vfx-presets.js');
const { renderSound, SOUNDS } = await load('vfx-sounds.js');
const { GAME_SOUNDS, GAME_FAMILIES } = await load('game-sounds.js');

const file = process.argv[2];
if (!file) { console.error('uso: node check-design-batch.mjs <docs/disenos/lote.json>'); process.exit(2); }
const lote = JSON.parse(fs.readFileSync(file, 'utf8'));
const repo = path.resolve(here, '../../..');
const comfy = path.join(process.env.LOCALAPPDATA || '', 'Comfy-Desktop', 'ComfyUI-Shared', 'output');
const KINDS = ['enemy', 'projectile', 'fx', 'sound'];
const NAME = { enemy: /^[A-Z][A-Za-z0-9]{2,40}$/, projectile: /^[A-Za-z][A-Za-z0-9_]{1,40}$/, fx: /^[A-Za-z][A-Za-z0-9_]{1,40}$/, sound: /^[a-z][a-z0-9_]{2,40}$/ };

let bad = 0;
const ids = new Set();
const names = new Set();
for (const item of lote.items || []) {
  const kind = item.kind || lote.kind || 'enemy';
  const issues = [];
  if (!item.id || ids.has(String(item.id))) issues.push('id vacío o repetido');
  ids.add(String(item.id));
  if (!KINDS.includes(kind)) issues.push(`kind desconocido "${kind}"`);
  if (!NAME[kind]?.test(item.name || '')) issues.push(`nombre "${item.name}" no sigue el formato de ${kind}`);
  if (names.has(item.name)) issues.push('nombre repetido en el lote');
  names.add(item.name);
  if (!['pending', 'accepted', 'discarded', undefined].includes(item.status)) issues.push(`status "${item.status}"`);

  if (kind === 'enemy') {
    const src = String(item.image || '');
    const abs = src.startsWith('comfy:') ? path.join(comfy, src.slice(6)) : path.join(repo, src);
    if (!src) issues.push('falta image');
    else if (!fs.existsSync(abs)) issues.push(`no existe la imagen ${abs}`);
  } else if (kind === 'projectile' || kind === 'fx') {
    const preset = presetById(item.vfx?.preset);
    if (!preset) issues.push(`no existe el preset "${item.vfx?.preset}"`);
    else {
      if (preset.kind !== kind) issues.push(`el preset "${preset.id}" es de tipo ${preset.kind}, no ${kind}`);
      const known = new Set(paramsOf(preset).map((p) => p.key));
      const unknown = Object.keys(item.vfx.params || {}).filter((k) => !known.has(k));
      if (unknown.length) issues.push(`parámetros que el preset no tiene: ${unknown.join(', ')}`);
    }
  } else if (kind === 'sound') {
    const def = item.sound;
    if (!def || !Array.isArray(def.layers)) issues.push('falta sound.layers');
    else if (!GAME_FAMILIES[def.family]) issues.push(`familia desconocida "${def.family}" (${Object.keys(GAME_FAMILIES).join(', ')})`);
    else {
      if (item.status !== 'accepted' && (GAME_SOUNDS[item.name] || SOUNDS[item.name])) issues.push(`el id ${item.name} ya existe`);
      for (const issue of checkSound(renderSound(`check:${item.name}`, def), def, GAME_FAMILIES[def.family].max)) issues.push(issue);
    }
  }

  if (issues.length) { bad++; console.log(`✗ ${item.id} ${item.name} (${kind}): ${issues.join(' · ')}`); }
  else console.log(`✓ ${item.id} ${item.name} (${kind})`);
}
console.log(bad ? `\n${bad} propuesta(s) con problemas.` : `\nLote correcto: ${(lote.items || []).length} propuesta(s).`);
process.exit(bad ? 1 : 0);
