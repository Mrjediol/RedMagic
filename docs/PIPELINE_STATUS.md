# Pipeline status

Living doc — per game system, what's finished vs. in progress. Update this when a system's status
changes; don't let it drift.

## Extensibility tiers (applies to every "complete" system below)

Before writing code for a requested feature on a **complete** system, judge which tier it falls
into — established on the enemy pipeline, same idea applies wherever a system is content-by-convention:

- **Tier 1 — pure data combination.** The feature is achievable by combining fields that already
  exist (e.g. `EnemyTuning`), with no code change at all. Just author the JSON/asset differently.
- **Tier 2 — a new field, no new decision logic.** Needs one new field added to a config/schema and
  plumbed through (e.g. `explosionRadius`), but no new branching — existing code paths just read
  one more number.
- **Tier 3 — genuinely new branching logic.** Needs a new decision path in the runtime brain (e.g.
  `EnemyBrain`/`EnemyAttack`), not just new data. This is the only tier that justifies non-trivial
  code changes; check the other two aren't sufficient first.

## Enemies — complete

Web app (Sprites tab + Enemy Creator tab) → combined-bundle importer (`CombinedBundleImporter.cs`,
`Tools ▸ Web`) → gameplay-ready prefab under `Assets/Prefabs/Enemies/`, in one step. Verified
end-to-end multiple times on real content: Ogro, a moth enemy, and a full Abeja regeneration pass
(re-running the importer against an existing recipe preserved hand-tuned `EnemyStats`).

## Maps — complete

Map Tracer tab (composed maps with baked `CompositeCollider2D` colliders, plus individual pieces) →
Unity importer. Both the composed-map path and the individual-piece path are verified working.

## Bosses — partial

`BossDefinition`/`BossPhase`/`BossAttack` JSON schema + `BossConfigImporter.cs` exist and work
(confirmed by `docs/schemas/COMPATIBILITY.md`: no runtime-class changes required). What's missing:
there is no web tab for authoring bosses yet — every boss pack (`BossStarterPack`,
`ScarecrowBossPack`, `StoneGuardianPack`, `CursedWellPack`, `BeetleQueenPack`, `UnholySealPack`,
`TreeBossPack`) is still hand-written C# under `Assets/Scripts/Bosses/Editor/`. A new boss today
means writing a new pack file, not using the web app.

## Player attacks / projectiles — not started

`ProjectileSpec` schema (`docs/schemas/projectile-config.schema.json`) exists and is shared with
enemy projectiles via `ProjectileConfigImporter.cs` — enemy-side projectile authoring already goes
through it. There is no dedicated **player**-attack web tooling yet; the weapons/items build system
(`WeaponDefinition`, trajectory/shape/element modifiers) is still authored by hand as Unity assets in
`Assets/Resources/Items/`.

## World Scene Generator — needs rework

`Assets/Scripts/Run/Editor/WorldSceneGenerator.cs` currently generates a **fixed** scene sequence
per world (N sections + a boss, built from an optional placeholder "base section" map prefab that
has since been deleted from the project) and wires a `WorldDefinition` ScriptableObject
(`Assets/ScriptableObjects/Worlds/World{1,2,3}.asset`) with all the references filled in.

Planned redesign: **pool-based random selection per world** — author a larger pool of normal-map
and boss-fight scenes, then draw a fixed count of each randomly per run (`WorldDefinition` already
supports a seeded sample via `TryBuildRunOrder`; the generator itself is what needs to change to
stop assuming one fixed ordered sequence). Not yet implemented. This depends on extending the
existing `Data/Worlds` → `ScriptableObjects/Worlds/World{1,2,3}.asset` `WorldDefinition` assets to
describe a pool rather than a fixed list.
