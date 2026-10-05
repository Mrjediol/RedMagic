// tools/enemy-prompt.mjs
// -----------------------------------------------------------------------------
// The one place that writes the prompt of an enemy BASE image — for Gemini (pasted by hand) and for ComfyUI —
// so both carry the game's look: the zone's palette, the crystal signature, the pose / pieces rules that make the
// picture cuttable afterwards, and the edge rules. What changes per enemy (creature, design, pieces) comes from
// the caller; everything else is fixed here + docs/zonas.json. Rules and why: .claude/skills/make-enemy/SKILL.md §0
// and base-prompt-gemini.md (2026-10-05 separation test).
//
//   import { buildEnemyPrompt, loadZones } from './enemy-prompt.mjs'
//   node enemy-prompt.mjs --zone ice --target gemini|comfy --creature "…" --design "…" --crystals "…"
//        --piece "Head: …" --piece "Torso: …" [--note "…"]
//   prints the prompt (stdout). `--list` prints the zones.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const ZONES_FILE = path.resolve(HERE, '..', '..', '..', 'docs', 'zonas.json');

export function loadZones(file = ZONES_FILE) {
  const doc = JSON.parse(fs.readFileSync(file, 'utf8'));
  return Array.isArray(doc.zones) ? doc.zones : [];
}

const cap = (s) => (s ? s.charAt(0).toUpperCase() + s.slice(1) : s);
const dot = (s) => { s = String(s || '').trim(); return s && !/[.!?]$/.test(s) ? `${s}.` : s; };

/**
 * { zone: id | zone object, target: 'gemini' | 'comfy', creature, design?, crystals?, pieces: ['Head: …', …], note? }
 *   creature  one line: what it is ("an ancient floating gatling-gun sentinel robot")
 *   design    materials, colours, shapes, personality
 *   crystals  where the 1-3 crystals grow / what they power (the game's signature)
 *   pieces    one line per template slot: "Name: description"
 */
export function buildEnemyPrompt({ zone, target = 'gemini', creature, design = '', crystals = '', pieces = [], note = '' }, zones = loadZones()) {
  const z = typeof zone === 'object' ? zone : zones.find((x) => x.id === zone);
  if (!z) throw new Error(`zona desconocida "${zone}" (hay: ${zones.map((x) => x.id).join(', ')})`);
  if (!String(creature || '').trim()) throw new Error('falta la criatura');
  if (!pieces.length) throw new Error('faltan las piezas');

  const lead = target === 'comfy'
    ? 'A 2D game enemy sprite for a dark fantasy mobile roguelite, full body, side view facing right, centered, one single character on a square canvas (not a sprite sheet, no variations):'
    : 'Generate ONE 2D game enemy sprite for a dark fantasy mobile roguelite:';
  const gemini = target === 'gemini'
    ? ' Side view facing right, full body, centered, SQUARE 1:1 image. One single character only: NOT a sprite sheet, no animation frames, no variations.' : '';
  const crystalLine = crystals
    ? `${dot(crystals)}`
    : 'They are embedded in its body (back, shoulders, chest or head) or power it.';
  const out = [
    `${lead} ${dot(creature)}${gemini}`,
    '',
    `ART STYLE: Dark fantasy ${z.theme}. ${z.palette} ${z.accent} accents only on crystals and eyes. Painted digital illustration, stylized semi-realistic, slight texture, no grain/haze outside the character.`,
    '',
    `CHARACTER: ${dot(cap(design)) || 'An original design that fits the zone.'} Built from ${z.materials}. It carries 1-3 ${z.crystal}: ${crystalLine} It looks like it belongs to the same world as the zone's cave.`,
    '',
    'POSE (the character will be cut into separate pieces afterwards, so EVERY piece must be fully visible): calm neutral pose, not an action pose, no foreshortening. Arms held away from the torso, never crossing in front of the body and never hidden behind it. Hands, weapons and fists never cover the legs, the other arm or the head. Legs apart with a visible gap. Wings, tails and accessories spread out. No piece hides more than about 10% of another. Name sides by the image (the arm on the LEFT / RIGHT of the image).',
    '',
    `PIECES — exactly these ${pieces.length}, no more (no extra arms, hands, weapons or loose parts; each piece joins its neighbour at a visible joint and has a clearly visible contour where they touch):`,
    ...pieces.map((p, i) => `${i + 1}. ${String(p).trim()}`),
    ...(note ? ['', String(note).trim()] : []),
    '',
    'EDGE RULES (critical):',
    '- Clean, crisp, hard silhouette with a 2px near-black outline all around, and a slightly darker contour line where pieces meet.',
    '- NO glow, aura, halo, bloom, particles, snow, frost mist, dust, smoke, embers, blur or soft edges outside the outline. Crystal glow stays inside the silhouette.',
    '- ABSOLUTELY NO effects of any kind: no energy beams, no muzzle flashes, no impact bursts, no motion lines. NO cast shadows, NO ground. A flame or glow that belongs to the body is one flat color inside its own outline.',
    '- Background: perfectly flat, uniform pure white, zero gradient or noise, even if an attached reference image has sky or scenery. The character contains no pure white fill touching the background (use pale colors).',
  ];
  return out.join('\n');
}

/** What a batch's prompt must carry (check-design-batch). */
export const REQUIRED = ['ART STYLE:', 'CHARACTER:', 'POSE', 'PIECES', 'EDGE RULES', 'pure white'];

function cli(argv) {
  const o = { pieces: [] };
  for (let i = 0; i < argv.length; i++) {
    const f = argv[i];
    const v = () => argv[++i];
    if (f === '--zone') o.zone = v();
    else if (f === '--target') o.target = v();
    else if (f === '--creature') o.creature = v();
    else if (f === '--design') o.design = v();
    else if (f === '--crystals') o.crystals = v();
    else if (f === '--note') o.note = v();
    else if (f === '--piece') o.pieces.push(v());
    else if (f === '--list') o.list = true;
    else throw new Error(`opción desconocida ${f}`);
  }
  return o;
}

if (process.argv[1]?.endsWith('enemy-prompt.mjs')) {
  try {
    const o = cli(process.argv.slice(2));
    if (o.list) loadZones().forEach((z) => console.log(`${z.id}\t${z.label}\t${z.palette}`));
    else console.log(buildEnemyPrompt(o));
  } catch (err) { console.error(`✗ ${err.message}`); process.exit(1); }
}
