// modules/fx-defaults.js
// -----------------------------------------------------------------------------
// Default form state for the Projectile/FX Creator, mirroring docs/schemas/fx-config.schema.json's
// own "default" keys — the same contract enemy-defaults.js keeps for EnemyConfig, and with the
// same safety net: modules/config-schema.js validates the built object against the REAL schema on
// every keystroke, so a default that drifts shows up as an error on an untouched form.
//
// The projectile block is `createDefaultProjectileSpec()` from enemy-defaults.js, NOT a second
// copy: fx-config.schema.json $refs projectile-config.schema.json's projectileSpec, so there is
// exactly one list of ProjectileSpec defaults in the app, exactly as there is exactly one schema
// definition of it.

import { createDefaultProjectileSpec } from './enemy-defaults.js';

/** The movement modes the schema declares, in the order the dropdown shows them. */
export const MOVEMENT_MODES = [
  {
    value: 'Straight',
    label: 'Straight — recto (implementado)',
    hint: 'Vuela en línea recta a la velocidad configurada hasta impactar o agotar su vida.',
  },
  {
    value: 'Homing',
    label: 'Homing — autoguiado (pendiente)',
    hint: 'Marcado en el config; el giro real lo hace ProjectileSpec.homingTurnRate, que sí funciona hoy.',
  },
  { value: 'Boomerang', label: 'Boomerang — va y vuelve (pendiente)', hint: 'Sin script propio todavía: vuela recto.' },
  { value: 'Bounce', label: 'Bounce — rebota (pendiente)', hint: 'Sin script propio todavía: vuela recto.' },
  { value: 'SplitOnImpact', label: 'Split-on-impact — se divide al chocar (pendiente)', hint: 'Sin script propio todavía: vuela recto.' },
];

export function createDefaultFxConfig() {
  return {
    // 'projectile' | 'fx'. Not a form-only field: it is the schema's own discriminator and decides
    // which library entries the art picker offers and whether the tuning block exists at all.
    kind: 'projectile',

    // No schema default on purpose (two unnamed configs would build the same prefab path) — starts
    // empty and the form shows a validation error until it is filled in.
    name: '',

    // Id of the 'projectile'/'fx'-kind library entry whose frames are this entity's art. Empty =
    // omitted on export, which is a valid-but-artless config; the form warns rather than blocks,
    // since picking the art after tuning the numbers is a perfectly normal order to work in.
    libraryId: '',

    movement: 'Straight',

    // Authoring defaults, not the live tuning — see the schema's $comment on 'projectile'. `prefab`
    // and `libraryId` inside the spec are meaningless for a standalone entry (the prefab is what
    // this document BUILDS, and the art is the top-level libraryId above), so they are stripped on
    // export by modules/fx-export.js rather than shown as fields nobody should fill in.
    projectile: createDefaultProjectileSpec(),
  };
}
