# JSON pipeline — compatibility note

Companion to `enemy-config.schema.json`, `projectile-config.schema.json`, `boss-config.schema.json`
and `_shared.schema.json`. Scope: enemies, enemy/boss projectiles and bosses. The player-side
`ShotProjectile` / `ShotBeam` / `WeaponShot` path is **out of scope** and is referenced nowhere in
the schemas.

## Verdict per schema

| Schema | Runtime classes it populates | Changes required |
|---|---|---|
| EnemyConfig | `EnemyRecipe`, `EnemyTuning` | **None** |
| ProjectileConfig | `ProjectileSpec` | **None** |
| BossConfig | `BossDefinition`, `BossPhase`, `BossAttack` + subclasses | **None to the runtime classes**; one authoring-helper limitation, below |

All three are edit-time authoring formats that produce assets. The importer writes fields and
calls the existing generators — it introduces no new runtime code path.

## Field-level mapping confirmations

**EnemyConfig → `EnemyTuning`** — all 45 serialized fields map 1:1 by name. The six derived
properties (`Flies`, `Airborne`, `IsRanged`, `Moves`, `Retreats`, `Sleeps`) are computed, have no
backing store, and are deliberately absent from the schema. Seeding goes through the existing
`EnemyStats.EditorSetTuning`, which already takes a whole `EnemyTuning` and calls `Apply()`.

**EnemyConfig → `EnemyRecipe`** — all 12 fields map 1:1. The schema groups five of them under
`presence` and two under `projectileArt` purely for readability; the importer flattens them back
out and **no field is renamed**. Everything else is flat, matching the class.

**ProjectileConfig → `ProjectileSpec`** — all 11 fields map 1:1.

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

**5. Two `Projectile.ConfigureBehaviour` guards swallow zeros.**
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
