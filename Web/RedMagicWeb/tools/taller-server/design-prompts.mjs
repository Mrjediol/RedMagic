// tools/taller-server/design-prompts.mjs
// -----------------------------------------------------------------------------
// The prompts 🎨 Designs sends to the queue. Kept SHORT on purpose: the rules live in the skills
// (.claude/skills/design-proposals, .claude/skills/make-enemy), which the task loads itself — the web only says
// what the user asked for and where the result goes. The queue's own header (runner.mjs ▸ _prompt) adds the
// "unattended, commit, last message = report" part.

export const KIND_LABEL = { enemy: 'enemigo', projectile: 'proyectil', fx: 'efecto VFX', sound: 'sonido' };
export const PENDING_TYPES = { comportamiento: 'Comportamiento', sprite: 'Sprite', gesto: 'Gesto', ataque: 'Ataque', bug: 'Bug', otro: 'Otro' };

const PICTURE = 'Después de editar la ficha, refresca su imagen para la web: '
  + `unity command eval --timeout 60 --code 'return RedMagic.Enemies.EditorTools.EnemyPreviewExporter.Mark(@"<prefab>");'`;

/** A design task: `index` (1-based) when the batch is split into one task per proposal. */
export function designPrompt({ kind, request, count, loteFile, index = null }) {
  const lines = [
    `Usa la skill design-proposals (.claude/skills/design-proposals/SKILL.md), tipo: ${kind} (${KIND_LABEL[kind]}).`,
    `Lo que pide el usuario: «${request}»`,
  ];
  if (index) {
    lines.push(`Esta tarea hace UNA propuesta: la ${index} de ${count}. Lee el lote antes de empezar y haz una distinta de las que ya tenga.`);
  } else {
    lines.push(`Cantidad: ${count} propuesta${count === 1 ? '' : 's'}, distintas entre sí.`);
  }
  lines.push(`Lote: ${loteFile} (ya existe: AÑADE tus propuestas a "items", no toques las que haya). Comprueba el lote con la herramienta de la skill antes de acabar.`);
  return lines.join('\n');
}

/** make-enemy from an approved design. */
export function buildPrompt(enemy) {
  const d = enemy.diseno || {};
  const base = String(d.source || '').replace(/^comfy:/, '');
  return [
    `Usa la skill make-enemy (.claude/skills/make-enemy/SKILL.md) para montar el enemigo ${enemy.name} a partir de su diseño aprobado.`,
    `Ficha: docs/enemigos/${enemy.name}/enemigo.json. Petición original: «${d.request || ''}»${d.note ? `. Nota del diseño: «${d.note}»` : ''}.`,
    `El diseño YA está aprobado: sáltate el paso 1 (Base). La base es la imagen de Comfy "${base}" (es el --ref de los pasos 2 y 3); hay una copia en ${d.image}.`,
    'Nadie va a contestar: donde la skill dice "stop: the user picks / checks", elige tú lo mejor con una hoja de contacto y sigue. El usuario aprueba al final desde la web.',
    `Al acabar, en la ficha: "estado": "por-aprobar", "prefab": "<ruta del prefab en Assets>", "montaje": { "fecha": "<ISO>", "resumen": "<2–3 frases>" }.`,
    PICTURE,
    'Si no puedes terminarlo: "estado": "por-montar" y explica en el informe qué falló y qué falta.',
  ].join('\n');
}

/** Fix the open "pendientes" of an existing enemy. */
export function pendingPrompt(enemy, items) {
  return [
    `Enemigo existente ${enemy.name}${enemy.prefab ? ` (prefab: ${enemy.prefab})` : ''}. Ficha: docs/enemigos/${enemy.name}/enemigo.json.`,
    'Resuelve estos pendientes que ha apuntado el usuario:',
    ...items.map((p) => `- [${PENDING_TYPES[p.tipo] || p.tipo}] ${p.texto} (id ${p.id})`),
    '',
    'Reglas del proyecto (CLAUDE.md): lo genérico primero; lee la doc de arquitectura que toque (enemies.md, procedural-rig.md, combat.md…).',
    'Si hace falta arte nuevo (un sprite, el estado de una pieza para un gesto) usa la skill make-enemy (pasos 3–4) con ComfyUI.',
    'Al acabar, en la ficha: cada pendiente resuelto con "estado": "hecho" y "resumen"; el que no se pudo, "estado": "abierto" y "nota" con el porqué.',
    'Si todos quedan hechos, el enemigo pasa a "estado": "por-aprobar"; si no, se queda en "con-pendientes".',
    PICTURE,
  ].join('\n');
}
