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
const PENDING_EN = { comportamiento: 'Behaviour', sprite: 'Sprite', gesto: 'Gesture', ataque: 'Attack', bug: 'Bug', otro: 'Other' };

const PICTURE = 'After editing the enemy sheet, refresh its picture for the web: '
  + `unity command eval --timeout 60 --code 'return RedMagic.Enemies.EditorTools.EnemyPreviewExporter.Mark(@"<prefab>");'`;

/** A design task: `index` (1-based) when the batch is split into one task per proposal. */
export function designPrompt({ kind, request, count, loteFile, index = null }) {
  const lines = [
    `Use the design-proposals skill (.claude/skills/design-proposals/SKILL.md), kind: ${kind} (${KIND_EN[kind]}).`,
    `What the user asked for (their words): «${request}»`,
  ];
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
  const base = String(d.source || '').replace(/^comfy:/, '');
  const open = (enemy.pendientes || []).filter((p) => p.estado !== 'hecho');
  return [
    `Use the make-enemy skill (.claude/skills/make-enemy/SKILL.md) to build the enemy ${enemy.name} from its approved design.`,
    `Enemy sheet: docs/enemigos/${enemy.name}/enemigo.json. Original request (user's words): «${d.request || ''}»${d.note ? `. Design note: «${d.note}»` : ''}.`,
    `The design is ALREADY approved: skip step 1 (Base). The base is the Comfy image "${base}" (the --ref of steps 2 and 3); a copy is at ${d.image}.`,
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
