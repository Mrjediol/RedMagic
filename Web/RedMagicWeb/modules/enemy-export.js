// modules/enemy-export.js
// -----------------------------------------------------------------------------
// Turns the Enemy Creator's live editable state (which carries empty-string placeholders and the
// form-only projectileMode/projectileSpec split — see enemy-defaults.js) into the actual
// EnemyConfig JSON object the schema describes: optional reference fields with nothing typed in
// are OMITTED (never emitted as "" or null, since assetRef's type never accepts either), and the
// projectile sub-form only becomes a real 'tuning.projectile' key when "configurar inline" is on.
//
// This is the ONE place that state → JSON shape translation happens. Both the live preview panel
// and the actual .json download call this same function, so they can never show different JSON.

import { buildAttacksExport } from './attack-export.js';

function omitEmpty(obj, keys) {
  const out = { ...obj };
  for (const k of keys) {
    if (out[k] === '' || out[k] == null) delete out[k];
  }
  return out;
}

function buildProjectileExport(spec) {
  return omitEmpty(spec, ['use', 'prefab', 'libraryId']);
}

function buildTuningExport(tuning) {
  // builtInAttack / builtInWeight belong to the deck: buildEnemyExport adds them back with it.
  const { projectileMode, projectileSpec, builtInAttack, builtInWeight, ...rest } = tuning;
  const out = omitEmpty(rest, ['projectileSprite']);

  if (projectileMode === 'inline') {
    out.projectile = buildProjectileExport(projectileSpec);
  }

  return out;
}

/**
 * Library mode's 'art' isn't a path the user typed — it's the path EnemyImporter.cs's sprite-import
 * step (Assets/Scripts/Editor/EnemyImporter.cs, BuildSpriteSheetRecipe) will create the SpriteSheetRecipe
 * at once the combined bundle is imported: 'Assets/Art/EnemyImports/<enemyName>/<enemyName>.sheet.asset'
 * (renamed from 'Assets/Enemies/<enemyName>/...' in the Assets/ folder restructure — see
 * docs/folder-restructure-audit.md). Emitted so a "solo config JSON" export stays schema-valid even
 * before that sheet exists — the combined-bundle Unity importer (CombinedBundleImporter.cs)
 * resolves the REAL freshly-created asset explicitly rather than trusting this string, so a
 * mismatch here is inert, not a silent bug.
 */
function libraryArtPath(enemyName) {
  const name = enemyName.trim();
  return `Assets/Art/EnemyImports/${name}/${name}.sheet.asset`;
}

/** @param {object} state - the Enemy Creator's live form state (see enemy-defaults.js's shape). */
export function buildExportObject(state, ctx) {
  return buildEnemyExport(state, ctx).doc;
}

/**
 * The export plus what is worth telling the user about it.
 *
 * @param {object} state
 * @param {object} [ctx] - what the deck needs and the rest of the form does not:
 *   { catalog: Unity's attack catalog, entriesById: Map of library records (art slots),
 *     gestureNames: the body's gestures, or null when unknown }.
 * @returns {{doc: object, art: Array<{id, libraryId, kind, key}>, problems: string[], notes: string[]}}
 */
export function buildEnemyExport(state, ctx = {}) {
  const doc = baseExport(state);
  const deck = state.deck || [];
  const ex = buildAttacksExport(deck, ctx.catalog || null, ctx.entriesById || new Map(),
    state.attackArt || {}, ctx.gestureNames ?? null);
  const defenceNotes = applyDefences(doc, state, ctx, ex.idMap);

  // No deck = exactly the document this form always exported: the classic import is untouched.
  if (deck.length === 0 && !state.deckTouched) return { doc, art: [], problems: [], notes: defenceNotes };

  // A card marked "only from a defence" is an attack of the enemy but not a card of the draw.
  doc.tuning.attacks = deck.filter((a) => !a.deck?.defenceOnly).map((a) => ({
    attack: ex.idMap.get(a.id),
    minRange: a.deck?.minRange ?? 0,
    maxRange: a.deck?.maxRange ?? 0,
    targetHeight: a.deck?.targetHeight || 'Any',
  }));
  doc.tuning.builtInAttack = state.tuning.builtInAttack !== false;
  doc.tuning.builtInWeight = state.tuning.builtInWeight > 0 ? state.tuning.builtInWeight : 1;

  if (Object.keys(ex.attacks).length) doc.attacks = ex.attacks;
  if (Object.keys(ex.artAssets).length) doc.artAssets = ex.artAssets;

  const notes = [...ex.notes, ...defenceNotes];
  const launched = new Set((state.defences || []).map((d) => d.attack).filter(Boolean));
  deck.forEach((a) => {
    if (a.deck?.defenceOnly && !launched.has(a.id)) notes.push(`Ataque ${a.id}: está marcado "sólo desde una defensa" pero ninguna defensa lo lanza → no sale nunca.`);
  });
  if (state.tuning.selfDestruct && deck.length) notes.push('Kamikaze: su ataque es explotar, la baraja se ignora.');
  if (!doc.tuning.builtInAttack && doc.tuning.attacks.length === 0) notes.push('Sin baraja, el ataque propio sale siempre (aunque esté desmarcado).');

  return { doc, art: ex.art, problems: ex.problems, notes };
}

/**
 * `tuning.defences`: written when there are any, or when the list was edited (an empty one removes
 * them in Unity). An enemy that never had any exports exactly the old document.
 */
function applyDefences(doc, state, ctx, idMap) {
  const list = state.defences || [];
  const notes = [];
  if (!list.length && !state.defencesTouched) return notes;
  const num = (v, fallback, min = 0) => (typeof v === 'number' && !Number.isNaN(v) ? Math.max(min, v) : fallback);
  doc.tuning.defences = list.map((d) => ({
    // The attack it launches while it moves away: the exported id of one of this enemy's attacks.
    ...(d.attack && idMap?.has(d.attack) ? { attack: idMap.get(d.attack) } : {}),
    trigger: d.trigger || 'TargetNear',
    distance: num(d.distance, 2.5),
    healthFraction: Math.min(1, num(d.healthFraction, 0.35)),
    chance: Math.min(1, num(d.chance, 1)),
    cooldown: num(d.cooldown, 3),
    move: d.move || 'DashAway',
    direction: d.direction || 'Away',
    ...(d.diagonalByTerrain ? {
      diagonalByTerrain: true,
      floorY: typeof d.floorY === 'number' ? d.floorY : 0,
      ceilingY: typeof d.ceilingY === 'number' ? d.ceilingY : 17,
      angle: Math.min(90, num(d.angle, 45)),
      angleVariance: Math.min(45, num(d.angleVariance, 10)),
    } : {}),
    moveDistance: num(d.moveDistance, 3),
    moveDuration: num(d.moveDuration, 0.3, 0.05),
    hopHeight: num(d.hopHeight, 1.2),
    gesture: (d.gesture || '').trim(),
    anticipation: num(d.anticipation, 0.12),
  }));
  if (list.length && state.tuning.archetype === 'Static') notes.push('Defensa: un enemigo Static no se desplaza, sus movimientos defensivos se ignoran.');
  list.forEach((d, i) => {
    const g = (d.gesture || '').trim();
    if (g && ctx.gestureNames && !ctx.gestureNames.includes(g)) notes.push(`Defensa ${i + 1}: el gesto "${g}" no existe en el cuerpo → se aparta sin gesto.`);
    if (d.attack && !idMap?.has(d.attack)) notes.push(`Defensa ${i + 1}: el ataque "${d.attack}" ya no existe → sólo se aparta.`);
  });
  return notes;
}

function baseExport(state) {
  const art = state.artMode === 'library' && state.artLibraryId
    ? libraryArtPath(state.enemyName)
    : state.art.trim();

  return {
    // Constant, not form state: this form only ever authors an EnemyConfig, and the schema pins
    // 'kind' to "enemy" with a const. It is emitted so the Unity side can dispatch on one field
    // across every web-authored config kind — see _shared.schema.json's entryKind.
    kind: 'enemy',
    enemyName: state.enemyName.trim(),
    art,
    anchor: state.anchor,
    prefabFolder: state.prefabFolder.trim() || 'Assets/Prefabs/Enemies',
    presence: { ...state.presence },
    projectileArt: { ...state.projectileArt },
    tier: state.tier,
    tuning: buildTuningExport(state.tuning),
  };
}
