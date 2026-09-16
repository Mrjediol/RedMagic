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
  return omitEmpty(spec, ['use', 'prefab']);
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
 * at once the combined bundle is imported: 'Assets/Enemies/<enemyName>/<enemyName>.sheet.asset'.
 * Emitted so a "solo config JSON" export stays schema-valid even before that sheet exists — the
 * combined-bundle Unity importer (CombinedBundleImporter.cs) resolves the REAL freshly-created
 * asset explicitly rather than trusting this string, so a mismatch here is inert, not a silent bug.
 */
function libraryArtPath(enemyName) {
  const name = enemyName.trim();
  return `Assets/Enemies/${name}/${name}.sheet.asset`;
}

/** @param {object} state - the Enemy Creator's live form state (see enemy-defaults.js's shape). */
export function buildExportObject(state) {
  const art = state.artMode === 'library' && state.artLibraryId
    ? libraryArtPath(state.enemyName)
    : state.art.trim();

  return {
    enemyName: state.enemyName.trim(),
    art,
    prefabFolder: state.prefabFolder.trim() || 'Assets/Prefab/Enemies',
    presence: { ...state.presence },
    projectileArt: { ...state.projectileArt },
    tier: state.tier,
    tuning: buildTuningExport(state.tuning),
  };
}
