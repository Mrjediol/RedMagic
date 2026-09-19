// modules/boss-defaults.js
// -----------------------------------------------------------------------------
// Default form state for the Boss Creator, mirroring docs/schemas/boss-config.schema.json's own
// "default" keys field-for-field — same contract as enemy-defaults.js / fx-defaults.js, same
// safety net (live Ajv validation against the real schema catches any drift).
//
// SCOPE: this is the WEB-SIDE data model only. Nothing imports a BossConfig into Unity yet — that
// is deliberately future work, until boss 2 actually has art (see the schema's 'base' $comment).
// So everything here is chosen to match what BossDefinition/BossPhase/BossAttack already are, not
// to match an importer that does not exist: when one is written it reads this shape unchanged.

/**
 * The starter set of generic attack "slots". Each is a real shipped BossAttack subclass, named by
 * its C# type — the same string boss-config.schema.json's 'type' takes and a future importer
 * resolves against types assignable to RedMagic.Bosses.BossAttack.
 *
 * DELIBERATELY A SMALL SUBSET of the thirteen shipped archetypes, and deliberately NOT a registry:
 * the schema's own "OPEN ARCHETYPE SET" note is explicit that enumerating archetypes is the
 * coupling the boss system was built to avoid. This list is a PICKER CONVENIENCE — the five that
 * cover the most ground with the fewest subclass-specific params, each asking the player a
 * different question (the project's stated bar for an archetype existing at all). Anything outside
 * it is still authorable: the type field accepts a typed-in name, and per-subclass fields go in
 * `params`, which the schema leaves open on purpose.
 *
 * `projectile` says whether the subclass has an embedded ProjectileSpec field, which is what makes
 * the "arte desde biblioteca" picker meaningful for that slot: BossConfigImporter.WriteField
 * already routes a ProjectileSpec-typed property through ProjectileConfigImporter.Resolve, and
 * that resolver already understands `libraryId`. So a boss attack referencing a web-authored
 * projectile needs no importer work at all — it rides the path the enemy's projectile already uses.
 */
export const ATTACK_SLOTS = [
  {
    type: 'BulletHellAttack',
    label: 'Bullet hell — anillo / abanico / lluvia',
    question: '¿Por dónde paso?',
    projectile: true,
  },
  {
    type: 'ShockwaveAttack',
    label: 'Onda de choque — banda de altura',
    question: '¿A qué altura estoy? (saltar o quedarse en el suelo)',
    projectile: false,
  },
  {
    type: 'SweepBeamAttack',
    label: 'Barrido — brazo que pivota',
    question: '¿A qué distancia estoy?',
    projectile: false,
  },
  {
    type: 'GroundSlamAttack',
    label: 'Pisotón — marca el suelo y lo aplasta',
    question: '¿Sigo donde estaba?',
    projectile: false,
  },
  {
    type: 'OrbRingAttack',
    label: 'Corona de orbes — se carga y se suelta',
    question: '¿Dónde me coloco antes de que cargue?',
    projectile: true,
  },
];

let _attackSeq = 0;

/** Resets the local-id counter. Only for tests/a full form reset — ids are per-document. */
export function resetAttackIdCounter() { _attackSeq = 0; }

/**
 * One entry of the config's `attacks` library. `id` is the LOCAL key the phases' decks reference
 * (schema: `^[A-Za-z][A-Za-z0-9_-]*$`), not a library id — it only has to be unique inside this
 * one document, which is why it can be generated rather than typed.
 */
export function createDefaultAttack(slot = ATTACK_SLOTS[0]) {
  _attackSeq += 1;
  return {
    id: `Ataque${_attackSeq}`,
    type: slot.type,
    displayName: '',
    telegraph: 0.8,
    recovery: 1.1,
    cooldownSeconds: 0,
    cooldownInAttacks: 1,
    minPhase: 1,
    damage: 18,
    // Id of a 'projectile'-kind library entry. Exported as `params.projectile.libraryId` — see
    // ATTACK_SLOTS above for why that lands in `params` and not next to the base fields.
    projectileLibraryId: '',
  };
}

/**
 * One `phases` entry. `attacks` holds the local ids of the attacks available in this phase; an
 * empty deck is legal per the schema (the phase can still act through an opening attack) and is
 * what a freshly-added phase starts as.
 */
export function createDefaultPhase(index = 0) {
  return {
    displayName: `Fase ${index + 1}`,
    // Phase 0 MUST be 1: BossDefinition.OnValidate forces it unconditionally, so the schema
    // rejects any other value there rather than letting the config claim something the asset will
    // not keep. Later phases default to a half-health cut, the shape every shipped boss uses.
    startsAtHealth: index === 0 ? 1 : 0.5,
    attacks: [],
    damageScale: 1,
    damageTakenMultiplier: 1,
    speedScale: 1,
    transitionSeconds: index === 0 ? 0 : 1.8,
  };
}

export function createDefaultBossConfig() {
  resetAttackIdCounter();
  return {
    // No schema default on purpose — two unnamed bosses would collide on the generated asset path.
    displayName: '',
    title: '',
    description: '',

    // The library enemy this boss is built on (schema: `base`). Empty = omitted on export.
    baseEnemyLibraryId: '',
    baseEnemyName: '',

    attacks: [createDefaultAttack()],
    phases: [createDefaultPhase(0)],
  };
}
