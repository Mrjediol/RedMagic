// tools/taller-server/design-prompts.mjs
// -----------------------------------------------------------------------------
// The prompts 🎨 Designs sends to the queue. Kept SHORT on purpose: the rules live in the skills
// (.claude/skills/design-proposals, .claude/skills/make-enemy), which the task loads itself — the web only says
// what the user asked for and where the result goes. The queue's own header (runner.mjs ▸ _prompt) adds the
// "unattended, commit, last message = report" part.
// Written in English (it is what Claude reads); the user's own words are quoted as they wrote them, and the
// data values of the enemy sheet (estado "por-aprobar"…) stay as the server stores them.

export const KIND_LABEL = { enemy: 'enemigo', projectile: 'proyectil', fx: 'efecto VFX', sound: 'sonido' };
export const PENDING_TYPES = { comportamiento: 'Comportamiento', sprite: 'Sprite', gesto: 'Gesto', ataque: 'Ataque', bug: 'Bug', otro: 'Otro' };
const KIND_EN = { enemy: 'enemy', projectile: 'projectile', fx: 'VFX effect', sound: 'sound' };
const GENERATOR_EN = {
  prompt: 'PROMPT MODE — you do NOT draw: write the Gemini prompt of each proposal (the user pastes it in the Gemini app and brings the picture back)',
  comfy: 'COMFY MODE — draw each base image with ComfyUI (tools/comfy/claude-workflow.mjs)',
};
const PENDING_EN = { comportamiento: 'Behaviour', sprite: 'Sprite', gesto: 'Gesture', ataque: 'Attack', bug: 'Bug', otro: 'Other' };

const PICTURE = 'After editing the enemy sheet, refresh its picture for the web: '
  + `unity command eval --timeout 60 --code 'return RedMagic.Enemies.EditorTools.EnemyPreviewExporter.Mark(@"<prefab>");'`;

/** A design task: `index` (1-based) when the batch is split into one task per proposal. */
export function designPrompt({ kind, request, count, loteFile, index = null, generator = null, zone = null, body = null, attack = null }) {
  const lines = [
    `Use the design-proposals skill (.claude/skills/design-proposals/SKILL.md), kind: ${kind} (${KIND_EN[kind]}).`,
    `What the user asked for (their words): «${request}»`,
  ];
  if (generator) lines.push(`Mode: ${GENERATOR_EN[generator]} — the skill's "${generator}" recipe. Zone: ${zone} (docs/zonas.json): every proposal belongs to it (palette, crystals).`);
  if (generator && body) lines.push(`Body type: ${body}, attack type: ${attack} (docs/tipos-enemigo.json). Build every prompt with enemy-prompt.mjs --body ${body} --attack ${attack}${attack === 'magic' ? ' --cast <the body part that casts: hand, mouth…>' : ''}: those rules decide the anatomy, so the design must not contradict them (no arms on a quadruped, no legs on a floater, no weapon on a magic one). The zone gives the colour palette only, not materials.`);
  if (index) {
    lines.push(`This task makes ONE proposal: number ${index} of ${count}. Read the batch before starting and make one different from those already in it.`);
  } else {
    lines.push(`Count: ${count} proposal${count === 1 ? '' : 's'}, different from each other.`);
  }
  lines.push(`Batch: ${loteFile} (it already exists: ADD your proposals to "items", do not touch the ones there). Check the batch with the skill's tool before finishing.`);
  return lines.join('\n');
}

/** make-enemy from an approved design. */
export function buildPrompt(enemy) {
  const d = enemy.diseno || {};
  const fromComfy = String(d.source || '').startsWith('comfy:');
  const base = String(d.source || '').replace(/^comfy:/, '');
  const ext = (String(d.image || '').match(/.[a-z]+$/i) || ['.png'])[0].toLowerCase();
  const open = (enemy.pendientes || []).filter((p) => p.estado !== 'hecho');
  return [
    `Use the make-enemy skill (.claude/skills/make-enemy/SKILL.md) to build the enemy ${enemy.name} from its approved design.`,
    `Enemy sheet: docs/enemigos/${enemy.name}/enemigo.json. Original request (user's words): «${d.request || ''}»${d.note ? `. Design note: «${d.note}»` : ''}.`,
    ...(fromComfy
      ? [`The design is ALREADY approved: skip step 1 (Base). The base is the Comfy image "${base}" (the --ref of steps 2 and 3); a copy is at ${d.image}.`]
      : [`The design is ALREADY approved: skip step 1 (Base). The base was drawn outside ComfyUI (Gemini); the picture is ${d.image}. Copy it to the ROOT of Comfy's input folder (%LOCALAPPDATA%/Comfy-Desktop/ComfyUI-Shared/input/${enemy.name}_base${ext}; no subfolders) and use "${enemy.name}_base${ext}" as the --ref of steps 2 and 3 (no " [output]"). It faces right; if it faces left, mirror the final parts (SKILL step 1). Follow SKILL step 2's COMPLETE rule for every part that touches another; ONE seed per part.`]),
    ...(d.zone ? [`Zone: ${d.zone} (docs/zonas.json): palette and crystal colour of its effects / projectile tints follow it.`] : []),
    ...(open.length ? [
      'The user also wrote these notes for this enemy (their words) — they are part of the brief, honour them:',
      ...open.map((p) => `- [${PENDING_EN[p.tipo] || p.tipo}] ${p.texto} (id ${p.id})`),
      'In the sheet, mark each one you fully covered "estado": "hecho" with a "resumen"; one you could not, leave "abierto" with a "nota" saying why.',
    ] : []),
    'Nobody will answer: where the skill says "stop: the user picks / checks", pick the best yourself from a contact sheet and go on. The user approves at the end from the web.',
    `When done, in the sheet: "estado": "por-aprobar", "prefab": "<prefab path under Assets>", "montaje": { "fecha": "<ISO>", "resumen": "<2–3 sentences, in Spanish>" }.`,
    PICTURE,
    'If you cannot finish it: "estado": "por-montar" and explain in the report what failed and what is left.',
  ].join('\n');
}

// ------------------------------------------------------------ by steps (the 🎨 Diseños sheet: "Montar por pasos")
// Step 1 = the body (parts → assembled → a body-only prefab); the user checks / adjusts it in 🧬 Enemy Studio;
// step 2 = the user picks behaviour + assets in the sheet; step 3 = everything else on top of THAT body.

/** What kind of enemy it is (step 2). Keys are stored in the sheet; `en` is what Claude reads. */
export const BEHAVIORS = {
  flyingRanged: { label: 'Volador a distancia', en: 'a flyer that keeps its distance and shoots (hover / strafe, ranged attacks)' },
  flyingMelee: { label: 'Volador cuerpo a cuerpo', en: 'a flyer that dives / charges at the player (melee, no shooting)' },
  groundRanged: { label: 'Suelo a distancia', en: 'a ground enemy that stays at range and shoots' },
  groundMelee: { label: 'Suelo cuerpo a cuerpo', en: 'a ground enemy that walks up to the player and hits in melee' },
  turret: { label: 'Torreta fija', en: 'a rooted turret that does not move and shoots' },
  floatingCaster: { label: 'Flotante lanzador', en: 'a floater that casts (a slow hover, spell-like attacks)' },
};
const ASSET_KEYS = { proyectil: 'projectile', sonidos: 'sounds', efectos: 'effects (muzzle / charge / impact / trails, gestureFx)' };

/** Step 1: the body only. */
export function bodyPrompt(enemy) {
  const d = enemy.diseno || {};
  const redo = (enemy.pendientes || []).filter((p) => p.estado !== 'hecho' && p.tipo === 'sprite');
  return [
    `Use the make-enemy skill (.claude/skills/make-enemy/SKILL.md), section "Por pasos ▸ Paso 1 (cuerpo)", to build ONLY THE BODY of the enemy ${enemy.name} from its approved design.`,
    `Enemy sheet: docs/enemigos/${enemy.name}/enemigo.json. Original request (user's words): «${d.request || ''}»${d.note ? `. Design note: «${d.note}»` : ''}.`,
    designBase(enemy),
    ...(d.zone ? [`Zone: ${d.zone} (docs/zonas.json).`] : []),
    'Do: SKILL steps 2 and 4 (parts in place, finish) and a brief with NO attacks / effects / gestures of your own (template defaults only), `make-enemy.mjs`, import it into Unity so the body exists as a prefab (ResetTuning = true is fine: nothing is tuned yet). Do NOT do steps 3 (states), 5 (attacks), 6 (sounds): the user checks the body first and may adjust it in 🧬 Enemy Studio (proportions, distances, layers).',
    ...(redo.length ? ['The user asked to redo the body with these notes (their words):', ...redo.map((p) => `- ${p.texto} (id ${p.id})`)] : []),
    'Nobody will answer: where the skill says "stop", pick the best yourself from a contact sheet and go on.',
    `Copy the assembled picture to docs/enemigos/${enemy.name}/cuerpo.png (the sheet shows it) and write in the sheet: "estado": "cuerpo-por-aprobar", "prefab": "<prefab path under Assets>", "cuerpo": { "fecha": "<ISO>", "plantilla": "<rig template id>", "partes": "<folder of the final parts>", "brief": "<brief path>", "resumen": "<2–3 sentences, in Spanish: which parts, anything dubious about proportions / layers>" }.`,
    PICTURE,
    'If you cannot finish it: "estado": "por-montar" and explain in the report what failed.',
  ].join('\n');
}

/** Step 3: behaviour + attacks + effects + sounds on the approved body. */
export function restPrompt(enemy) {
  const p2 = enemy.paso2 || {};
  const asset = (k) => {
    const a = p2[k] || {};
    const text = String(a.texto || '').trim();
    const chosen = (a.elegidos || []).map((c) => `«${c.name}» (library id ${c.id})`);
    if (a.origen === 'biblioteca') return `- ${ASSET_KEYS[k]} (there can be SEVERAL): USE WHAT THE LIBRARY ALREADY HAS (📚 Biblioteca / sound palette) — ${chosen.length ? `the user PICKED these, use all of them: ${chosen.join(', ')}${text ? `; and also: «${text}»` : ''}` : text ? `the user asks for: «${text}»` : 'pick the best fit for this enemy yourself'}. Do not create new ones; if nothing fits, say so in the report.`;
    return `- ${ASSET_KEYS[k]}: CREATE NEW ones (several if described, one per line) — ${text ? `the user describes them (their words): «${text}»` : 'the user left it empty: design them to fit this enemy'}.`;
  };
  const open = (enemy.pendientes || []).filter((p) => p.estado !== 'hecho' && p.tipo !== 'sprite');
  return [
    `Use the make-enemy skill (.claude/skills/make-enemy/SKILL.md), section "Por pasos ▸ Paso 3 (el resto)", to finish the enemy ${enemy.name}.`,
    `Enemy sheet: docs/enemigos/${enemy.name}/enemigo.json (see "cuerpo" and "paso2"). Its BODY is already built AND APPROVED by the user: prefab ${enemy.prefab || '(see the sheet)'}. The user may have adjusted it by hand in 🧬 Enemy Studio (proportions, distances, layers): NEVER re-import the full body zip over it, never change the parts or their layout.`,
    ...(p2.plantilla
      ? [`Behaviour: INHERIT from the template enemy «${p2.plantilla}» (docs/enemigos/${p2.plantilla}/enemigo.json, its prefab and brief): copy its tuning, deck, gestures, defences and effects hooks, retargeted to this body's slot ids. Keep this enemy's own art and the assets below.`]
      : [`Behaviour type: ${BEHAVIORS[p2.tipo]?.label || p2.tipo} — ${BEHAVIORS[p2.tipo]?.en || ''}. Design tuning, attacks, gestures and defences for it (SKILL step 5; generic-first rules of CLAUDE.md).`]),
    'Assets (the user\'s choice for each):',
    asset('proyectil'), asset('sonidos'), asset('efectos'),
    ...(p2.notas ? [`Extra notes from the user (their words): «${p2.notas}»`] : []),
    ...(open.length ? ['Open notes in the sheet (their words) — part of the brief:', ...open.map((p) => `- [${PENDING_EN[p.tipo] || p.tipo}] ${p.texto} (id ${p.id})`), 'Mark each one you covered "estado": "hecho" + "resumen"; one you could not stays "abierto" + "nota".'] : []),
    `How to apply it on the existing body: write the brief for the attacks / effects / gestures, run \`node make-enemy.mjs <brief> --partial\` (→ out/<Name>.partial.zip, only gestures + attacks + effects) and import THAT zip into Unity (RigBundleImporter.Import with the partial zip; ResetTuning = false). The body part of the brief must match the prefab's parts (same template, parts folder from the sheet's cuerpo.partes). The partial zip carries gestures, effects and the deck only: movement / EnemyStats tuning for the chosen behaviour type (flying vs ground, ranges, speeds, projectile spec) is NOT in it — set it on the prefab's EnemyStats (unity eval / prefab edit, see docs/architecture/enemies.md), starting from the body's template defaults.`,
    'Nobody will answer: pick the best yourself. The user approves at the end from the web.',
    `When done, in the sheet: "estado": "por-aprobar", "montaje": { "fecha": "<ISO>", "resumen": "<2–3 sentences, in Spanish>" }.`,
    PICTURE,
    `If you cannot finish it: "estado": "cuerpo-aprobado" and explain in the report what failed and what is left.`,
  ].join('\n');
}

/** Where the approved base picture is (the same wording buildPrompt uses). */
function designBase(enemy) {
  const d = enemy.diseno || {};
  const fromComfy = String(d.source || '').startsWith('comfy:');
  const base = String(d.source || '').replace(/^comfy:/, '');
  const ext = (String(d.image || '').match(/.[a-z]+$/i) || ['.png'])[0].toLowerCase();
  return fromComfy
    ? `The design is ALREADY approved: skip step 1 (Base). The base is the Comfy image "${base}" (the --ref of step 2); a copy is at ${d.image}.`
    : `The design is ALREADY approved: skip step 1 (Base). The base was drawn outside ComfyUI (Gemini); the picture is ${d.image}. Copy it to the ROOT of Comfy's input folder (%LOCALAPPDATA%/Comfy-Desktop/ComfyUI-Shared/input/${enemy.name}_base${ext}; no subfolders) and use "${enemy.name}_base${ext}" as the --ref of step 2 (no " [output]"). It faces right; if it faces left, mirror the final parts (SKILL step 1). Follow SKILL step 2's COMPLETE rule for every part that touches another; ONE seed per part.`;
}

/** Fix the open "pendientes" of an existing enemy. */
export function pendingPrompt(enemy, items) {
  return [
    `Existing enemy ${enemy.name}${enemy.prefab ? ` (prefab: ${enemy.prefab})` : ''}. Enemy sheet: docs/enemigos/${enemy.name}/enemigo.json.`,
    'Resolve these open items the user wrote down (their words):',
    ...items.map((p) => `- [${PENDING_EN[p.tipo] || p.tipo}] ${p.texto} (id ${p.id})`),
    '',
    'Project rules (CLAUDE.md): generic first; read the architecture doc it touches (enemies.md, procedural-rig.md, combat.md…).',
    'If new art is needed (a sprite, a part\'s state for a gesture) use the make-enemy skill (steps 3–4) with ComfyUI.',
    'When done, in the sheet: each item resolved gets "estado": "hecho" and a "resumen" (in Spanish); one that could not be done stays "estado": "abierto" with a "nota" saying why.',
    'If all are done, the enemy goes to "estado": "por-aprobar"; otherwise it stays "con-pendientes".',
    PICTURE,
  ].join('\n');
}
