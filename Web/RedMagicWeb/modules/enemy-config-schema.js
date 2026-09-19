// modules/enemy-config-schema.js
// -----------------------------------------------------------------------------
// The Enemy Creator's view of the shared schema registry. Both functions are thin, name-preserving
// wrappers over modules/config-schema.js, which owns the single Ajv instance every creator tab
// compiles out of — see that file for WHY there is only one instance (cross-file `$ref`s only
// resolve within the instance the referenced schema was registered in).
//
// This file stayed as its own module rather than having enemy-form.js import the generic one
// directly so that the Enemy Creator's entry point keeps reading as "load MY schema", and so the
// docs' module map (ARCHITECTURE.md) keeps pointing at one file per tab.

import { loadValidator, validateConfig } from './config-schema.js';

/** Fetches + compiles enemy-config.schema.json once; safe to call repeatedly (cached). */
export function loadEnemyConfigValidator() {
  return loadValidator('enemy');
}

/** @see validateConfig — identical, kept under the old name for the Enemy Creator's call sites. */
export const validateEnemyConfig = validateConfig;
