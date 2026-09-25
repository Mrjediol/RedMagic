// modules/fx-export.js
// -----------------------------------------------------------------------------
// Turns the Projectile/FX Creator's live form state (fx-defaults.js's shape) into the actual
// FxConfig JSON the schema describes. Same role, and the same one-place rule, as
// modules/enemy-export.js: the live preview panel and the .json/.zip downloads all call this, so
// what is shown and what is written can never disagree.
//
// Three translations happen here, all of them "the form holds a placeholder the schema refuses":
//  - empty optional strings are OMITTED, never emitted as "" (the schema's minLength 1 rejects it);
//  - the whole `projectile` block disappears for kind 'fx' (a VFX has no ProjectileSpec — the
//    schema's own if/then says so, and leaving it in would export inert speed/pierce numbers);
//  - `prefab` / `libraryId` INSIDE the projectile block are always dropped, because for a
//    standalone entry the prefab is what this document builds and the art is the TOP-LEVEL
//    libraryId. Emitting either would produce a spec pointing at something other than itself.

function omitEmpty(obj, keys) {
  const out = { ...obj };
  for (const k of keys) {
    if (out[k] === '' || out[k] == null) delete out[k];
  }
  return out;
}

/** The spec block as the schema wants it: the ProjectileSpec fields (numbers + the three Aiming keys), no art keys. */
function buildProjectileExport(spec) {
  const out = { ...spec };
  delete out.prefab;
  delete out.libraryId;
  return omitEmpty(out, ['use']);
}

/** @param {object} state - the Projectile/FX Creator's live form state (see fx-defaults.js). */
export function buildFxExportObject(state) {
  const doc = {
    kind: state.kind,
    name: state.name.trim(),
    movement: state.movement,
  };

  if (state.libraryId) doc.libraryId = state.libraryId;
  if (state.kind === 'projectile') doc.projectile = buildProjectileExport(state.projectile);

  return doc;
}
