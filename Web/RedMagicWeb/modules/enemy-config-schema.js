// modules/enemy-config-schema.js
// -----------------------------------------------------------------------------
// Loads and compiles the REAL EnemyConfig JSON Schema (docs/schemas/enemy-config.schema.json,
// _shared.schema.json, projectile-config.schema.json) via Ajv (draft-07) — not a hand-rolled
// reimplementation of the rules, so the Enemy Creator's validation can never silently drift from
// the actual contract the Unity-side importer reads.
//
// KEEP IN SYNC: schemas/*.json in this folder must stay byte-identical to docs/schemas/*.json at
// the repo root (vendored here so the tool stays self-contained when served from its own folder,
// same reasoning as ARCHITECTURE.md's "Running locally" section). There is no build step to
// enforce this — re-copy by hand whenever the source schemas change.
//
// Ajv is loaded globally via the CDN <script> in index.html (window.ajv7), same pattern as JSZip.

let _validate = null;

/** Fetches + compiles the schema once; safe to call repeatedly (cached after the first call). */
export async function loadEnemyConfigValidator() {
  if (_validate) return _validate;

  const [shared, projectile, enemy] = await Promise.all([
    fetch('./schemas/_shared.schema.json').then((r) => r.json()),
    fetch('./schemas/projectile-config.schema.json').then((r) => r.json()),
    fetch('./schemas/enemy-config.schema.json').then((r) => r.json()),
  ]);

  const AjvCtor = window.ajv7.Ajv || window.ajv7.default;
  const ajv = new AjvCtor({ allErrors: true, strict: false });
  ajv.addSchema(shared);
  ajv.addSchema(projectile);
  _validate = ajv.compile(enemy);
  return _validate;
}

/**
 * Runs the compiled validator against `doc` (the export-shaped object — see
 * modules/enemy-export.js's buildExportObject, NOT the raw editable form state, which carries
 * empty-string placeholders the real schema would reject).
 *
 * @returns {{valid:boolean, errors: Array<{path:string, message:string}>}}
 *   `path` is an Ajv instancePath ('' for a root-level error, e.g. '/tuning/archetype').
 */
export function validateEnemyConfig(validate, doc) {
  const valid = validate(doc);
  const errors = (validate.errors || []).map((e) => ({
    path: e.instancePath || '',
    message: e.message,
  }));
  return { valid, errors };
}
