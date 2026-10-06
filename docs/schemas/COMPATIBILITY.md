# JSON pipeline — compatibility note

Companion to `enemy-config.schema.json`, `projectile-config.schema.json`, `boss-config.schema.json`
and `_shared.schema.json`. Scope: enemies, enemy/boss projectiles and bosses. The player-side
`ShotProjectile` / `ShotBeam` / `WeaponShot` path is **out of scope** and is referenced nowhere in
the schemas.

## Verdict per schema

| Schema | Runtime classes it populates | Changes required |
|---|---|---|
| EnemyConfig | `EnemyRecipe`, `EnemyTuning` (+ `EnemyDeckEntry`, `BossAttack` assets for its deck) | **None** — see "EnemyConfig deck" below |
| ProjectileConfig | `ProjectileSpec` | **None** |
| BossConfig | `BossDefinition`, `BossPhase`, `BossAttack` + subclasses | **None to the runtime classes**; one authoring-helper limitation, below |
| RigConfig | `ProceduralRig`, `RigPart`, `RigSpriteSwap`, `RigGesture`/`RigPose`, `BossLimbs` (+ root collider, `Corpse`, boss stack) | **None** — see "RigConfig" below |

All three are edit-time authoring formats that produce assets. The importer writes fields and
calls the existing generators — it introduces no new runtime code path.

## RigConfig (`rig-config.schema.json`) — part-based bodies

Separate from the three above: its importer (`RigBundleImporter`, *Tools ▸ Web ▸ Rig*) never goes
through `EnemyFactory` / sprite sheets. Verified 1:1 by a round trip, not by reading:
`RigConfigRoundTrip` exports Robot / Turret / BossIce2 to `docs/schemas/fixtures/rig/`, reimports
them and compares every public field of `ProceduralRig` by reflection (plus the hierarchy under
`Visual` and `BossLimbs`) — a new `ProceduralRig` field is covered automatically.

- **`rig` block → `ProceduralRig`**: every serialized field by its C# name (48 today), except
  `body` (always the `Visual` child), `parts`, `spriteSwaps`, `gestures` (own blocks). Transform
  fields travel as **paths relative to `Visual`** (`"Visual"` = the body itself), `LayerMask` as
  layer names (shared `layerMask`), sprites as `Assets/…` paths or bundle-relative files. The schema
  lists the fields with `additionalProperties: false`: adding a field to `ProceduralRig` means adding
  it to the schema too (the exporter already emits it).
- **`parts[]` → hierarchy + `RigPart`**: one entry per Transform under Visual. The `RigPart` fields
  map 1:1 except that the five `sway*` fields are grouped as `sway.{move,rotate,scale,speed,phase}`
  (a grouping, like `presence` in EnemyConfig; no rename inside it). `rigPart: false` nodes (Muzzle,
  Grip, empty pivots) exist only in the hierarchy. List order = `ProceduralRig.parts` order.
- **Importer-only keys**: `pixelsPerUnit`, `sprites` (per-file import settings), `root`, `collider`,
  `corpse`, `bossBody` (same keys as `BossConfig.body`'s stack, read by the shared
  `BossBodyBuilder.AddBossStack`). They describe the prefab around the rig, not rig data.
- **Companions**: next to `X.rig-config.json` the importer reads `X.enemy-config.json` / `X.boss-config.json`
  (in a zip: `enemy-config.json` / `boss-config.json`). The exporter writes `X.enemy-config.json` (full
  `tuning` by reflection + `tier`, no `art`: in a rig bundle `art` is not used) and `bossBody.definition`
  (existing asset, linked when no boss-config comes along).
- **Attacks at releases, partial bundles** (2026-10-02): (1) new attack type `ReleaseAttack`: `params.shots`
  is a list of objects, one per release of its gesture (written whole by the web). (2) `tuning.attacks[]`
  entries with `builtIn` (the own attack with another gesture) are gone from the schema; the importer skips
  one with a warning — reopen the enemy in the web and export again (it becomes a `ReleaseAttack`). (3)
  `tuning.defences[].attack` (optional): an id of `attacks` launched while the defence moves; such an
  attack need not be a card of `tuning.attacks`. (4) rig-config `"partial": true` + an enemy-config with
  only `attacks` / `artAssets` / `tuning.attacks` / `tuning.defences`: applied over an existing prefab,
  only gestures, state sprites, deck and defences are written.
- **Gestures as timelines from the web** (2026-10-02): the web (Enemy Studio, `make-enemy.mjs`) now writes
  `gestures[]` as `{ name, steps[], releases[], recover, fx[] }` — the shape `RigConfigExporter` already
  wrote — instead of `windup` / `strike` / `phases[]`. The importer reads both; nothing changes for a
  rig-config written before. A gesture effect's `fx` may be `{"art": id}` (web library) or a prefab path
  (read back from a Unity export and written as it came).
- **Gesture effects** (additive, 2026-10-01): `gestures[].phases[]` = `{ phase: windup | strike | recover,
  fx: [{ fx, at, anchor, part, offset, scale, followsPart, loop }] }` ↔ `RigGesture.fx` (a flat list of
  `RigFx`, each with its `phase`; the JSON groups them by phase, in phase order). `fx` is
  `{"art": "<id>"}` into the document's own `artAssets` (the BossConfig block, reused by `$ref`) or a
  prefab path (what the exporter writes). New art kind **`fxLoop`** in `boss-config`'s `artAssets`
  (flipbook that loops, no `VfxOneShot`). A config without `phases` imports exactly as before and
  keeps the effects already on the prefab; `phases: []` clears them.
- **Named state sprites** (additive, 2026-10-01): `spriteSwaps[].named[]` = `{ state: alert | hurt | dead |
  gesture, gesture?, sprite }` ↔ `RigSpriteSwap.named` (`RigNamedSprite`). Absent = the three classic
  states only, imported as before. Behaviour change that comes with it: a state without a sprite now falls
  through to the next one (down to the prefab's sprite) instead of keeping whatever was shown.
- **Gesture sprite window** (additive, 2026-10-01): `spriteSwaps[].named[]` with `state: gesture` may carry
  `from` / `to` (0–3: windup 0–1, strike 1–2, recover 2–3) ↔ `RigNamedSprite.showFrom` / `showTo`. Absent =
  the whole gesture. **Bug fixed with it**: `named[].sprite` files of a bundle were never copied (only
  asleep / awake / attacking / stages were collected), so every named sprite of a web export was dropped
  with a warning — the round trip did not see it because it uses project paths.
- **Idle gestures** (additive, 2026-10-01): top-level `idleGestures` = `{ gestures: [names], interval, chance,
  windup }` ↔ `ProceduralRig.idle` (`RigIdleGestures`); it is a structured field, so it is **not** in the
  `rig` block. The exporter always writes it (the fixtures gained an empty one).
- **Things that bite**: (1) tuning from a bundled `enemy-config.json` goes through
  `EnemyConfigImporter.ReadTuning`, so the `EnemyStats.OnValidate` clamps above apply unchanged;
  its `presence` block is ignored (the body/collider come from rig-config). (2) A bundled
  `boss-config.json` must not carry `body` (the rig *is* the body) — it is stripped with a warning.
  (3) Unassigned object fields on a loaded component are Unity "fake nulls": compare with Unity's
  `==`, never `is`/pattern matching (the round-trip comparer learned this).

## EnemyConfig deck (`attacks`, `artAssets`, `tuning.attacks`) — several attacks per enemy

Optional; a config without `tuning.attacks` imports exactly as before (`EnemyDeckImporter.Read`
returns null and nothing else runs).

- **`attacks` / `artAssets`** are the BossConfig blocks, by `$ref`
  (`boss-config.schema.json#/definitions/attackEntry`, `#/properties/artAssets`) and by code
  (`BossConfigImporter.ResolveAttackTypes` / `WriteAttacks`, `BossArtBuilder`): one definition of
  an attack for bosses and enemies. Assets:
  `Assets/ScriptableObjects/EnemyAttacks/<enemyName>/EnemyAttack_<enemyName>_<id>.asset`; art
  prefabs `Assets/Prefabs/Fx/Enemies/<enemyName>/`. Relative art folders resolve against the
  bundle's landing folder (`Assets/Art/EnemyImports/<name>/` or `Assets/Art/RigImports/<name>/`),
  frames under `Art/<libraryId>/<State>/`.
- **`tuning.attacks[]` → `EnemyTuning.attacks`** (`EnemyDeckEntry`): `minRange`, `maxRange`,
  `targetHeight` map 1:1; `attack` is an id of the `attacks` block **or** an `Assets/…` path (what
  `RigConfigExporter` writes). `tuning.builtInAttack` / `builtInWeight` map 1:1.
- **Things that bite**: (1) the deck (cards + the two `builtIn*` scalars) is written on **every**
  import, the rest of `tuning` only on first generation / reset. To clear a deck, export an empty
  `tuning.attacks: []` (the web does once the deck was touched). (2) `[BossOnly]` types are
  rejected with an error before any asset is written. (3) `minPhase`, `vulnerableSeconds` and
  `vulnerableMultiplier` are accepted (shared definition) and ignored at runtime on an enemy.
  (4) Each attack asset is saved right after it is written: creating the next asset in a
  just-created folder could otherwise reload the previous one from its still-default file.

## EnemyConfig defences (`tuning.defences`) — defensive moves

Additive (2026-10-01). `tuning.defences[]` ↔ `EnemyTuning.defences` (`EnemyDefence`), 1:1 by field name;
`trigger` and `move` are the C# enum member names. Read by `EnemyDeckImporter.ReadDefences` (both
importers), **not** by `BuildTuning`: like the deck it is applied on every import when the key is present
(`[]` removes them, an absent key keeps the prefab's). A config without the key is the same document as
before. `trigger` also takes `AfterAttack` and `direction` (`Away` | `Toward` | `ByRange`, default `Away`) is additive (2026-10-06).
The rig exporter always writes the list (the two enemy fixtures gained `"defences": []`).

## Field-level mapping confirmations

**EnemyConfig → `EnemyTuning`** — every serialized field maps 1:1 by name (`moveSoundInterval` was missing until the rig export needed a full round trip; now in schema + importer). The six derived
properties (`Flies`, `Airborne`, `IsRanged`, `Moves`, `Retreats`, `Sleeps`) are computed, have no
backing store, and are deliberately absent from the schema. Seeding goes through the existing
`EnemyStats.EditorSetTuning`, which already takes a whole `EnemyTuning` and calls `Apply()`.

**EnemyConfig → `EnemyRecipe`** — all 12 fields map 1:1. The schema groups five of them under
`presence` and two under `projectileArt` purely for readability; the importer flattens them back
out and **no field is renamed**. Everything else is flat, matching the class.

**EnemyConfig → `SpriteSheetRecipe.anchor`** — not a 1:1 field mapping like the ones above: `anchor`
is not a field on `EnemyRecipe` or `EnemyTuning` at all, it is `RedMagic.Pipeline.AnchorMode` on
whatever `SpriteSheetRecipe` the config's `art` resolves to. It is consumed **only** by the
combined-bundle import (`CombinedBundleImporter.ImportBundle` → `EnemyImporter.BuildEnemyFromFolderPath`),
the one import path that actually cuts a fresh `SpriteSheetRecipe` from raw PNGs — a plain
config-only import (`EnemyConfigImporter.Import`) references an `art` recipe that already exists,
whose `anchor` was already fixed whenever *that* was cut, and does not touch it. See the field's
`$comment` in the schema for the full reasoning, and "Things that need no code change" below for
the bug this closes.

**ProjectileConfig → `ProjectileSpec`** — all 14 fields map 1:1 (the last three — `faceDirection`, `facingAxis`, `aimMode` — are the Aiming block; the two enums travel as their C# names and `ProjectileConfigImporter` parses them case-insensitively).

**BossConfig → `BossDefinition` / `BossPhase`** — all 4 + 15 fields map 1:1.

**BossConfig → `BossAttack`** — all 17 shared base fields map 1:1. Subclass fields ride in the
open `params` block keyed by C# field name.

## The brief's projectile field list was wrong — correction applied

The brief listed `damage`, `hitLayers`, `knockbackMultiplier` and "pooled-or-destroy" as
`ProjectileSpec` fields. They are not. That list is the union of `ProjectileSpec` and the
`Projectile` MonoBehaviour. Verified against source:

- `damage` and `knockbackMultiplier` — per-shot arguments. `EnemyAttack.Shoot` passes
  `tuning.attackDamage` and `tuning.attackKnockbackMultiplier` into `ProjectileFactory.Spawn`.
- `hitLayers` — arrives via `AbilityContext.HitLayers`, i.e. `tuning.hitLayers`.
- `destroyWhenDone` — a `Projectile` field driven by the `PooledCode` / `PooledPrefab` marks the
  factory stamps. Never spec data.
- The spec's field is `pierce`; the component's is `pierceCount`. The schema uses `pierce`.

Putting any of the four in ProjectileConfig would produce configs whose values are silently
overwritten on every shot — the exact failure the "projectile tuning lives on EnemyStats"
convention exists to prevent. They are therefore absent, and `EnemyConfig.tuning` is where the
three real ones are set.

## Things that need no code change but will bite the importer

**1. `EnemyStats.OnValidate` mutates two fields, and it runs during import.**
The importer is editor code, so `OnValidate` fires. It will:
- force `detectionRange` up to at least `attackRange + 2` (`ApproachBandMargin`) when the
  archetype moves;
- clamp `personalSpace` down to at most `attackRange`.

A config violating either is silently rewritten and the asset stops matching the JSON that made
it. The importer must apply both clamps itself and report them.

**2. `BossDefinition.OnValidate` forces `phases[0].startsAtHealth = 1`.**
Reject any other value on the first phase in the config rather than writing one the asset discards.

**3. `BossAuthoring.WritePhase` hardcodes two of the fields this schema exposes.**
`transitionShake` is written as `transitionSeconds > 0f ? 0.7f : 0f` and `frenzySpeedScale` as a
literal `1.35f`, both ignoring anything the caller wanted. An importer that routes through
`WritePhase` would silently drop those two config values. Write the phase fields directly with
`SerializedProperty` (the same mechanism `WritePhase` uses internally) or give `WritePhase` two
more parameters. **This is a limitation of the authoring helper, not of `BossPhase`** — the
serialized class itself needs nothing.

**4. Private serialized fields are the norm on the boss side.**
`BossDefinition`'s four fields and `BossAttack`'s seventeen are all `[SerializeField] private` with
read-only properties. The importer must go through `SerializedObject.FindProperty`, exactly as
`BossAuthoring.Fields.Write` already does. No accessibility change is needed or wanted.

**5. `anchor` fixed a real bug in `EnemyImporter.cs`, not just a missing schema field.**
Before this field existed, `EnemyImporter.cs`'s sprite-import path (`ConfigureSpriteImport`) hardcoded
every enemy's sprite pivot to `BottomCenter` ("pies") regardless of archetype — unlike the
pipeline-pack path, which already had per-recipe `AnchorMode` and used it (e.g.
`MurcielagoPack.cs: anchor = AnchorMode.Center // vuela: sin pies`). A flying enemy imported via the
web's combined bundle got a pivot at an empty point in the air below its body, and with it a
`transform.position`-relative `detectionRange`/`attackRange`/`explosionRadius` (and their gizmos —
`EnemyStats.OnDrawGizmosSelected`) all measured from that same wrong point instead of the body.
Fixed by threading `AnchorMode` through `EnemyImporter.BuildEnemyFromFolderPath` (default `Center`)
instead of the old `bool pivotBottom`, including the generated `BoxCollider2D`'s offset
(`EnemyImporter.BuildEnemyPrefab`, mirroring `EnemyFactory.cs`'s `centeredPivot` calculation) and the
compatibility recipe's own `anchor` field (`BuildSpriteSheetRecipe`), so a later regen through
`EnemyFactory.Generate` against the same recipe would not silently fall back to `BottomCenter`.

**6. Two `Projectile.ConfigureBehaviour` guards swallow zeros.**
`homingRange` is only applied when `> 0`, so an explicit `0` leaves the component's own `9` in
place rather than disabling homing — disable it with `homingTurnRate: 0`. `lifetime` has the same
guard but its `[Min(0.05f)]` makes it unreachable.

## The one field deliberately left out, and why

`EnemyBrain.WalkableDrop` (1.5f) is the only hardcoded constant in the enemy stack that is
genuinely per-enemy gameplay rather than probe geometry — a large enemy should step down a ledge a
small one refuses. It **should** eventually become an `EnemyTuning` field.

It is **not** in this schema, because adding it costs a runtime change (one new float on
`EnemyTuning`, one line in `EnemyBrain`) and no shipped enemy currently needs it. A schema key that
maps to nothing is worse than no key: the importer would have to silently ignore it. Promote the
constant first, then add the key.

## Hardcoded constants — decision per constant

| Constant | File | Value | Verdict | Reasoning |
|---|---|---|---|---|
| `RetargetInterval` | `EnemyBrain.cs` | 0.5 | **Stays hardcoded** | How often it retries to find a target that does not exist yet. Pure housekeeping with no gameplay feel. Exposing it invites someone to set 5s and create an "enemy ignored me" bug that reads as broken AI and gets chased in the state machine. |
| `LedgeProbeInset` | `EnemyBrain.cs` | 0.05 | **Stays hardcoded** | Horizontal offset of the probe ray from the muzzle. Geometric implementation detail of the probe; meaningless to anyone not reading the probe code. |
| `LedgeProbeRise` | `EnemyBrain.cs` | 0.35 | **Stays hardcoded** | Ray start height above the feet, sized so a few centimetres of collider penetration do not blind it. It is a property of the physics setup, not of the enemy. `GroundMotion.Probe` uses the same 0.35 — exposing one copy and not the other would let them desync, which is worse than leaving both fixed. |
| `WalkableDrop` | `EnemyBrain.cs` | 1.5 | **Promote — but later** | The only one that is a real per-enemy gameplay decision (how big a drop reads as a step versus a cliff). Costs a runtime change, so it is out of schema v1. See above. |
| `ApproachBandMargin` | `EnemyStats.cs` | 2.0 | **Stays hardcoded** | It is not a tunable, it is a coherence rule: without a band above `attackRange`, the enemy spawns already in range and the approach phase is dead. Making it configurable makes the broken configuration expressible. The importer must mirror the clamp (see above). |
| `Idle`/`Walk`/`Attack`/`Hurt`/`Death`/`Wake` | `EnemyAnimation.cs` | — | **Stay hardcoded** | The project's single animation vocabulary, shared with `AnimClipBuilder` and `PlayerAnimator`. A per-enemy override would break the contract that lets one clip builder serve every character. Configs reach these indirectly through the sheet recipe's row `state` names. |
| explosion shake duration | `EnemyAttack.cs` | 0.25 | **Stays hardcoded** | An inline literal next to `explosionShake`, which *is* exposed. Amplitude is the readable knob; a second one for duration adds a field nobody will tune. |
| `GroundMask` | `Projectile.cs` | `1 << 6` | **Stays hardcoded (this pass)** | See the recommendation below. |

## Recommended shared-constant fix: defer `GroundMask`, do the data-layer fix now

**Defer the `GroundMask` unification.** The audit flagged `GroundMask = 1 << 6` duplicated as a
private `const` in `Projectile.cs`, `ShotProjectile.cs` and `ShotBeam.cs`. Doing it now is the
wrong sequencing:

- It is invisible to all three schemas. No JSON field maps to it, so it blocks nothing.
- Two of the three files (`ShotProjectile`, `ShotBeam`) are on the player-side path this pass
  explicitly excludes. Fixing it now means touching out-of-scope code while the importer is being
  written against it.
- It is a clean standalone change: one shared constant resolved by layer *name*, three call sites,
  one commit, no data migration. It loses nothing by waiting and is easier to review alone.

**Do this instead, now, at zero runtime cost:** make the importer resolve layer masks by **name**
rather than accepting raw bitmasks — which is what `_shared.schema.json#/definitions/layerMask`
already specifies, with `["Ground", "Platform"]` as `obstacleLayers`' default and a hard failure on
an unknown layer name.

That is the same class of bug as `GroundMask`, caught at the layer where this pass actually lives.
`EnemyTuning.obstacleLayers` defaults to the magic number `(1 << 6) | (1 << 8)` and has **already**
shipped a real bug — enemies halting at the foot of `Platform`-layer bridges because the mask was
`1 << 6` alone — serious enough that `ContentAudit` exists to repair it after the fact. Every enemy
authored as JSON from here on is one that cannot reintroduce it, and a renamed layer breaks the
import loudly instead of breaking the game quietly.
