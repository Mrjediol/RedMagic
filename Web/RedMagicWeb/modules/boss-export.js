// modules/boss-export.js
// -----------------------------------------------------------------------------
// Turns the Boss Creator's live form state (boss-defaults.js's shape) into the BossConfig JSON
// docs/schemas/boss-config.schema.json describes. Same one-place rule as the other two exporters:
// the preview panel and every download call this, so they can never disagree.
//
// TWO SHAPE TRANSLATIONS, both of them the form being flatter than the document:
//
//  - `attacks` is an ARRAY in the form (so it can be reordered and rendered as a list) and an
//    OBJECT keyed by local id in the document. The schema is explicit about why it is a keyed
//    library rather than inline-per-phase: BossPhase.attacks is an array of REFERENCES to shared
//    BossAttack assets, the same asset legitimately sits in several decks (or in none), and
//    inlining per deck would create duplicate assets and break that sharing.
//
//  - `projectileLibraryId` is a flat field on the form and lands in `params.projectile.libraryId`.
//    `params` is the subclass-specific half the schema leaves open, and `projectile` is a real
//    serialized field on BulletHellAttack/OrbRingAttack — BossConfigImporter.WriteField already
//    detects a ProjectileSpec-typed property there and hands it to
//    ProjectileConfigImporter.Resolve, which already resolves `libraryId` through
//    FxPrefabBuilder. So this rides the exact path an enemy's projectile already rides, with no
//    importer change; putting it anywhere else would have needed one.

/** A local attack id the schema accepts (`^[A-Za-z][A-Za-z0-9_-]*$`), derived from what was typed. */
function safeAttackId(raw, fallback) {
  const cleaned = (raw || '').trim().replace(/[^A-Za-z0-9_-]/g, '_');
  return /^[A-Za-z]/.test(cleaned) ? cleaned : fallback;
}

function buildAttackExport(attack) {
  const out = {
    type: attack.type,
    telegraph: attack.telegraph,
    recovery: attack.recovery,
    cooldownSeconds: attack.cooldownSeconds,
    cooldownInAttacks: attack.cooldownInAttacks,
    minPhase: attack.minPhase,
    damage: attack.damage,
  };

  // Blank means "fall back to the asset name", which is exactly what BossAttack.displayName does
  // in C# — so omitting it is more faithful than exporting an empty string.
  if (attack.displayName && attack.displayName.trim()) out.displayName = attack.displayName.trim();

  if (attack.projectileLibraryId) {
    out.params = { projectile: { libraryId: attack.projectileLibraryId } };
  }

  return out;
}

function buildPhaseExport(phase, validAttackIds) {
  return {
    displayName: phase.displayName,
    startsAtHealth: phase.startsAtHealth,
    // Filtered rather than exported raw: an attack deleted after being added to a deck would
    // otherwise leave a dangling id, which the importer rejects outright ("no está en 'attacks'").
    attacks: phase.attacks.filter((id) => validAttackIds.has(id)),
    damageScale: phase.damageScale,
    damageTakenMultiplier: phase.damageTakenMultiplier,
    speedScale: phase.speedScale,
    transitionSeconds: phase.transitionSeconds,
  };
}

/** @param {object} state - the Boss Creator's live form state (see boss-defaults.js's shape). */
export function buildBossExportObject(state) {
  const attacks = {};
  const usedIds = new Set();

  state.attacks.forEach((attack, i) => {
    let id = safeAttackId(attack.id, `Ataque${i + 1}`);
    // Two attacks sharing a key would silently collapse into one entry in the object — and the
    // deck referencing "the other one" would point at the survivor. Suffixing keeps both.
    while (usedIds.has(id)) id = `${id}_${i + 1}`;
    usedIds.add(id);
    attacks[id] = buildAttackExport(attack);
  });

  const doc = {
    // Constant, not form state: this form only ever authors a BossConfig, and the schema pins
    // 'kind' to "boss" with a const. Emitted so the Unity side can dispatch on one field across
    // every web-authored config kind — see _shared.schema.json's entryKind.
    kind: 'boss',
    displayName: state.displayName.trim(),
    attacks,
    phases: state.phases.map((p) => buildPhaseExport(p, usedIds)),
  };

  if (state.title.trim()) doc.title = state.title.trim();
  if (state.description.trim()) doc.description = state.description.trim();

  if (state.baseEnemyLibraryId) {
    doc.base = { enemyLibraryId: state.baseEnemyLibraryId };
    if (state.baseEnemyName) doc.base.enemyName = state.baseEnemyName;
  }

  return doc;
}
