// modules/enemy-defaults.js
// -----------------------------------------------------------------------------
// Every default value here mirrors docs/schemas/enemy-config.schema.json /
// projectile-config.schema.json's own "default" keys, field-for-field — this is what pre-fills
// the Enemy Creator form so an untouched form already exports a valid enemy. If a default here
// ever drifts from the schema, modules/enemy-config-schema.js's live Ajv validation against the
// REAL schema is the safety net that catches it (an untouched form would start showing an error).
//
// Two kinds of "optional, no real default" fields are modeled as empty strings in form state,
// NOT as their schema default: `projectileSprite`, and the inline projectile sub-form's `prefab`/
// `use`. Their schema default is JSON `null` or absence, but assetRef's own type never accepts a
// literal `null` — so the correct exported form is OMITTING the key, which
// modules/enemy-export.js's buildExportObject does whenever the field is left empty.

export function createDefaultProjectileSpec() {
  return {
    prefab: '', // empty = omitted on export (ProjectileSpec.prefab default: null)
    speed: 12,
    lifetime: 2.5,
    size: { x: 0.35, y: 0.35 },
    muzzleOffset: { x: 0.65, y: 0.1 },
    pierce: 0,
    homingTurnRate: 0,
    homingRange: 9,
    arcGravity: 0,
    impactRadius: 0,
    impactDamage: 0,

    // Aiming (ProjectileSpec ▸ Aiming, Gameplay.ProjectileAim in Unity). Defaults = today's behavior:
    // faceDirection false leaves rotation to the prefab, Right = art drawn pointing +X, Fixed = the
    // shooter decides the direction. MouseDirection / NearestEnemy only act for the player.
    faceDirection: false,
    facingAxis: 'Right',
    aimMode: 'Fixed',

    use: '', // empty = omitted (library reference id, importer-only convenience key)

    // Id of a 'projectile'-kind entry in THIS tool's shared library. Empty = omitted on export.
    // When set, the Unity importer builds/reuses that entry's pooled prefab and writes it into
    // ProjectileSpec.prefab, so the enemy import alone materializes the projectile — see
    // projectile-config.schema.json's 'libraryId'. Mutually exclusive with `prefab` in practice.
    libraryId: '',
  };
}

export function createDefaultEnemyTuning() {
  return {
    archetype: 'Melee',
    staticAttack: 'Ranged',

    maxHealth: 30,
    invulnerabilityDuration: 0,
    hurtSfxId: '',
    deathSfxId: '',

    knockbackHorizontal: 6,
    knockbackVertical: 3,
    knockbackDuration: 0.18,
    knockbackResistance: 0,

    detectionRange: 7,
    loseInterestGrace: 1,
    attackRange: 1.6,
    personalSpace: 0,
    retreatReleaseFactor: 1.35,
    verticalTolerance: 3,

    moveSpeed: 2.5,
    retreatSpeed: 2.5,
    stopAtLedges: true,
    ledgeProbeDepth: 0.6,
    gravityScale: 3,

    sleepsUntilDetected: false,

    hoverOffset: 0.5,

    attackDamage: 10,
    attackCooldown: 1.8,
    attackKnockbackMultiplier: 1,
    attackReleaseFallback: 0.25,
    rootedWhileAttacking: true,

    meleeHitboxSize: { x: 1.2, y: 1 },
    meleeHitboxOffset: { x: 0.8, y: 0.5 },

    selfDestruct: false,
    explosionRadius: 1.7,
    explosionShake: 0.2,

    // 'projectile' is intentionally absent here — the "usar por defecto / configurar inline"
    // toggle (modules/enemy-form.js) owns projectileMode + projectileSpec instead; export only
    // adds a 'projectile' key when the user picked "configurar inline".
    aimAtTarget: true,
    projectileSprite: '', // empty = omitted on export (default: null, not a real asset)
    projectileTint: '#ffffffff',

    contactDamage: 0,
    contactDamageCooldown: 1,
    contactKnockbackMultiplier: 1,

    idleAnimSpeed: 1,
    moveAnimSpeed: 1,
    attackAnimSpeed: 1,
    hurtAnimSpeed: 1,
    deathAnimSpeed: 1,
    wakeAnimSpeed: 1,

    targetTag: 'Player',
    hitLayers: 'everything',
    obstacleLayers: ['Ground', 'Platform'],

    // Form-only state (stripped by buildExportObject, never sent to the validator/export as-is).
    projectileMode: 'default', // 'default' | 'inline'
    projectileSpec: createDefaultProjectileSpec(),
  };
}

export function createDefaultEnemyConfig() {
  return {
    // No default in the schema on purpose (two unnamed configs would collide) — starts empty,
    // the form shows a validation error until the user fills them in.
    enemyName: '',
    art: '',

    // Form-only state, stripped/translated by buildExportObject (modules/enemy-export.js):
    // 'library' (default) picks a sheet authored in THIS tool's shared library this session or a
    // past one (modules/enemy-library.js) by id — art is then computed from enemyName, matching
    // the path EnemyImporter.cs's sprite-import step will actually create it at. 'manual' is the
    // original behavior: a plain path/assetRef into a sheet already imported into Unity.
    artMode: 'library',
    artLibraryId: null,

    // Sprite pivot: Center (default — safe for anything without feet) or BottomCenter (ground
    // walkers). Only consumed by the combined-bundle import, where it decides the pivot
    // EnemyImporter.cs bakes into the sprites/collider/SpriteSheetRecipe it cuts — see the
    // schema's $comment on 'anchor'.
    anchor: 'Center',

    prefabFolder: 'Assets/Prefabs/Enemies',

    presence: {
      spriteScale: 1,
      colliderSize: { x: 0, y: 0 },
      colliderOffset: { x: 0, y: 0 },
      sortingOrder: 5,
      tag: 'Untagged',
    },

    projectileArt: {
      propState: 'Attack',
      scale: 1,
    },

    tier: 'Basic',

    tuning: createDefaultEnemyTuning(),
  };
}

/** ProjectileSpec.facingAxis values, in schema order — the dropdown options of both creators. */
export const PROJECTILE_FACING_AXES = [
  { value: 'Right', label: 'Right — punta a la derecha (+X, la convención)' },
  { value: 'Left', label: 'Left — punta a la izquierda' },
  { value: 'Up', label: 'Up — punta arriba' },
  { value: 'Down', label: 'Down — punta abajo' },
];

/** ProjectileSpec.aimMode values, in schema order. */
export const PROJECTILE_AIM_MODES = [
  { value: 'Fixed', label: 'Fixed — la dirección que da quien dispara' },
  { value: 'MouseDirection', label: 'MouseDirection — hacia el cursor (sólo jugador)' },
  { value: 'NearestEnemy', label: 'NearestEnemy — al enemigo más cercano al salir (sólo jugador)' },
];

/**
 * The three Aiming rows, shared by enemy-form.js and fx-form.js so both creators show the same
 * widgets in the same order. Row shape = [key, kind, label, optsOrHint], like PROJECTILE_SPEC_FIELDS.
 */
export const PROJECTILE_AIMING_FIELDS = [
  ['faceDirection', 'bool', 'Girar hacia donde vuela (faceDirection)',
    'Rota el sprite para que su punta mire a la dirección de vuelo. Apagado = decide el prefab.'],
  ['facingAxis', 'enum', 'Punta del sprite (facingAxis)',
    { options: PROJECTILE_FACING_AXES, hint: 'Qué lado del dibujo apunta hacia donde vuela.' }],
  ['aimMode', 'enum', 'Apuntado (aimMode)',
    { options: PROJECTILE_AIM_MODES, hint: 'Ratón / enemigo más cercano sólo cambian algo si dispara el jugador; un enemigo sigue en Fixed.' }],
];
