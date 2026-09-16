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

/** @param {object} state - the Enemy Creator's live form state (see enemy-defaults.js's shape). */
export function buildExportObject(state) {
  return {
    enemyName: state.enemyName.trim(),
    art: state.art.trim(),
    prefabFolder: state.prefabFolder.trim() || 'Assets/Prefab/Enemies',
    presence: { ...state.presence },
    projectileArt: { ...state.projectileArt },
    tier: state.tier,
    tuning: buildTuningExport(state.tuning),
  };
}
