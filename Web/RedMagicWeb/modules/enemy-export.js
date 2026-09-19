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
  const { projectileMode, projectileSpec, ...rest } = tuning;
  const out = omitEmpty(rest, ['projectileSprite']);

  if (projectileMode === 'inline') {
    out.projectile = buildProjectileExport(projectileSpec);
  }

  return out;
}

/**
 * Library mode's 'art' isn't a path the user typed — it's the path EnemyImporter.cs's sprite-import
 * step (Assets/Editor/EnemyImporter.cs, BuildSpriteSheetRecipe) will create the SpriteSheetRecipe
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
export function buildExportObject(state) {
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
