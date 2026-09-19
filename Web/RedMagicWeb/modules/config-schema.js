// modules/config-schema.js
// -----------------------------------------------------------------------------
// Loads and compiles the REAL config JSON Schemas (docs/schemas/*.json) with Ajv (draft-07) — one
// shared Ajv instance for every creator tab, so the Enemy / Projectile-FX / Boss forms can never
// validate against a hand-rolled reimplementation that has drifted from the contract the Unity
// importers actually read.
//
// ONE Ajv instance, not one per tab, because the schemas cross-reference each other by $id
// (`fx-config` $refs `projectile-config`, all three $ref `_shared`) and a `$ref` only resolves
// against schemas registered in the SAME instance. Registering `_shared` twice in two instances
// would work; registering it twice in one throws. So the registry is built once here and every
// document type is compiled out of it.
//
// KEEP IN SYNC: schemas/*.json in this folder must stay byte-identical to docs/schemas/*.json at
// the repo root (vendored here so the tool stays self-contained when served from its own folder,
// same reasoning as ARCHITECTURE.md's "Running locally" section). There is no build step to
// enforce this — re-copy by hand whenever the source schemas change.
//
// Ajv is loaded globally via the CDN <script> in index.html (window.ajv7), same pattern as JSZip.

/** Every schema file, loaded and registered together. The keys are what `loadValidator` takes. */
const SCHEMA_FILES = {
  _shared: './schemas/_shared.schema.json',
  projectile: './schemas/projectile-config.schema.json',
  enemy: './schemas/enemy-config.schema.json',
  fx: './schemas/fx-config.schema.json',
  boss: './schemas/boss-config.schema.json',
};

/** Document types a form can validate against (i.e. everything but the shared value types). */
const ROOT_SCHEMAS = ['enemy', 'fx', 'boss', 'projectile'];

let _ajvPromise = null;
const _validators = {}; // name -> compiled validate fn

async function getAjv() {
  if (_ajvPromise) return _ajvPromise;

  _ajvPromise = (async () => {
    const names = Object.keys(SCHEMA_FILES);
    const docs = await Promise.all(names.map((n) => fetch(SCHEMA_FILES[n]).then((r) => r.json())));

    const AjvCtor = window.ajv7.Ajv || window.ajv7.default;
    const ajv = new AjvCtor({ allErrors: true, strict: false });
    docs.forEach((doc) => ajv.addSchema(doc));
    return ajv;
  })();

  return _ajvPromise;
}

/**
 * The compiled validator for one document type ('enemy' | 'fx' | 'boss' | 'projectile').
 * Cached — safe to call on every form init.
 *
 * @param {string} name
 * @returns {Promise<Function>} an Ajv validate function (`validate(doc) -> boolean`, `.errors`).
 */
export async function loadValidator(name) {
  if (!ROOT_SCHEMAS.includes(name)) throw new Error(`No hay ningún esquema de documento '${name}'.`);
  if (_validators[name]) return _validators[name];

  const ajv = await getAjv();
  // getSchema() rather than compile(): the document was already registered by $id above, and
  // compiling the same $id twice is the error Ajv raises for a duplicate schema id.
  const validate = ajv.getSchema(`https://redmagic.local/schemas/${name === 'enemy' ? 'enemy-config'
    : name === 'fx' ? 'fx-config'
    : name === 'boss' ? 'boss-config'
    : 'projectile-config'}.schema.json`);

  if (!validate) throw new Error(`El esquema '${name}' no se registró correctamente.`);
  _validators[name] = validate;
  return validate;
}

/**
 * Runs a compiled validator against `doc` (the EXPORT-shaped object built by the matching
 * *-export.js, not the raw editable form state, which carries empty-string placeholders the real
 * schema would reject).
 *
 * @returns {{valid:boolean, errors: Array<{path:string, message:string}>}}
 *   `path` is an Ajv instancePath ('' for a root-level error, e.g. '/tuning/archetype').
 */
export function validateConfig(validate, doc) {
  const valid = validate(doc);
  const errors = (validate.errors || []).map((e) => ({
    path: e.instancePath || '',
    message: e.message,
  }));
  return { valid, errors };
}
