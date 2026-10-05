// tools/enemy-prompt.mjs
// -----------------------------------------------------------------------------
// The one place that writes the prompt of an enemy BASE image — for Gemini (pasted by hand) and for ComfyUI —
// so both carry the game's look: the zone's palette, the crystal signature, the pose / pieces rules that make the
// picture cuttable afterwards, and the edge rules. What changes per enemy (creature, design, pieces) comes from
// the caller; everything else is fixed here + docs/zonas.json. Rules and why: .claude/skills/make-enemy/SKILL.md §0
// and base-prompt-gemini.md (2026-10-05 separation test).
//
//   import { buildEnemyPrompt, loadZones } from './enemy-prompt.mjs'
//   node enemy-prompt.mjs --zone ice --body humanoid|quadruped|flying|floating|libre --attack weapon|magic|none --target gemini|comfy --creature "…" --design "…" --crystals "…"
//        --piece "Head: …" --piece "Torso: …" [--cast "mouth"] [--note "…"]
//        (--piece is optional: each body has default pieces in docs/tipos-enemigo.json)
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

export const TYPES_FILE = path.resolve(HERE, '..', '..', '..', 'docs', 'tipos-enemigo.json');

/** docs/tipos-enemigo.json: { common, body: { id: { label, anatomy, pose, pieces } }, attack: { id: { label, text, piece } } } */
export function loadTypes(file = TYPES_FILE) {
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

const cap = (s) => (s ? s.charAt(0).toUpperCase() + s.slice(1) : s);
const dot = (s) => { s = String(s || '').trim(); return s && !/[.!?]$/.test(s) ? `${s}.` : s; };

/**
 * { zone: id | zone object, target: 'gemini' | 'comfy', body, attack, creature, design?, crystals?, cast?, pieces?, note? }
 *   body      key of docs/tipos-enemigo.json ▸ body (humanoid, quadruped, flying, floating, libre): its anatomy, pose and
 *             default pieces — a rule of one body never reaches another
 *   attack    key of ▸ attack (weapon, magic, none): weapon = a separate piece that fires; magic = the plain creature, no weapon
 *   creature  one line: what it is ("an ancient floating gatling-gun sentinel robot")
 *   design    shapes, colours, personality (the zone gives the PALETTE only, never its materials)
 *   crystals  where the 1-3 crystals grow / what they power (the game's signature)
 *   cast      magic only: the body part that casts (hand, mouth, horn…); drawn idle, swapped later
 *   pieces    one line per slot ("Name: description") — optional, replaces the body's default pieces
 */
export function buildEnemyPrompt({ zone, target = 'gemini', body = 'libre', attack = 'none', creature, design = '', crystals = '', cast = '', pieces = [], note = '' }, zones = loadZones(), types = loadTypes()) {
  const z = typeof zone === 'object' ? zone : zones.find((x) => x.id === zone);
  if (!z) throw new Error(`zona desconocida "${zone}" (hay: ${zones.map((x) => x.id).join(', ')})`);
  const b = types.body[body];
  if (!b) throw new Error(`cuerpo desconocido "${body}" (hay: ${Object.keys(types.body).join(', ')})`);
  const at = types.attack[attack];
  if (!at) throw new Error(`ataque desconocido "${attack}" (hay: ${Object.keys(types.attack).join(', ')})`);
  if (!String(creature || '').trim()) throw new Error('falta la criatura');

  const lead = target === 'comfy'
    ? 'A 2D game enemy sprite for a dark fantasy mobile roguelite, full body, side view facing right, centered, one single character on a square canvas (not a sprite sheet, no variations):'
    : 'Generate ONE 2D game enemy sprite for a dark fantasy mobile roguelite:';
  const gemini = target === 'gemini'
    ? ' Side view facing right, full body, centered, SQUARE 1:1 image. One single character only: NOT a sprite sheet, no animation frames, no variations.' : '';
  const crystalLine = crystals
    ? `${dot(crystals)}`
    : 'They are embedded in its body (back, shoulders, chest or head) or power it.';
  const list = pieces.length ? pieces : [...b.pieces, ...(attack === 'weapon' && at.piece ? [at.piece] : [])];
  const castLine = attack === 'magic' && cast ? ` Its casting part is the ${String(cast).trim()}: draw it plainly, in its idle form.` : '';
  const out = [
    `${lead} ${dot(creature)}${gemini}`,
    '',
    `ART STYLE: Dark fantasy mobile game. Colour world of a ${z.theme} (colours only, not materials). ${z.palette} ${z.accent} accents only on crystals and eyes. Painted digital illustration, stylized semi-realistic, slight texture, no grain/haze outside the character.`,
    '',
    `CHARACTER: ${dot(cap(design)) || 'An original design that fits the zone.'} ${b.anatomy ? `${b.anatomy} ` : ''}Use the zone's colour palette only: its materials follow the creature's own nature (a robot stays metal, an animal stays an animal) and it is NOT covered in snow, ice, leaves or rock unless the design says so. It carries 1-3 ${z.crystal}: ${crystalLine}`,
    '',
    `ATTACK: ${at.text}${castLine}`,
    '',
    `POSE (the character will be cut into separate pieces afterwards, so EVERY piece must be fully visible): ${b.pose} ${types.common}`,
    '',
    list.length
      ? `PIECES — exactly these ${list.length}, no more (no extra limbs, hands, weapons or loose parts; each piece joins its neighbour at a visible joint and has a clearly visible contour where they touch):`
      : 'PIECES — the natural parts of the creature described, no extra limbs, weapons or loose parts invented:',
    ...list.map((p, i) => `${i + 1}. ${String(p).trim()}`),
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
export const REQUIRED = ['ART STYLE:', 'CHARACTER:', 'ATTACK:', 'POSE', 'PIECES', 'EDGE RULES', 'pure white'];

function cli(argv) {
  const o = { pieces: [] };
  for (let i = 0; i < argv.length; i++) {
    const f = argv[i];
    const v = () => argv[++i];
    if (f === '--zone') o.zone = v();
    else if (f === '--target') o.target = v();
    else if (f === '--body') o.body = v();
    else if (f === '--attack') o.attack = v();
    else if (f === '--cast') o.cast = v();
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
    if (o.list) { const t = loadTypes(); Object.entries(t.body).forEach(([k, v]) => console.log(`body	${k}	${v.label}`)); Object.entries(t.attack).forEach(([k, v]) => console.log(`attack	${k}	${v.label}`)); }
    if (o.list) loadZones().forEach((z) => console.log(`${z.id}\t${z.label}\t${z.palette}`));
    else console.log(buildEnemyPrompt(o));
  } catch (err) { console.error(`✗ ${err.message}`); process.exit(1); }
}
