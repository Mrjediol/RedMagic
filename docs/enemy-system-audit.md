# RedMagic Enemy / Attack / Boss System — Documentation Audit

## 1. Enemies

**Scripts by responsibility (new pipeline — `Assets/Scripts/Enemies/`):**

| Concern | Script | Path |
|---|---|---|
| Tunables (single source of truth) | `EnemyTuning` (plain `[Serializable]` class) | `Assets/Scripts/Enemies/EnemyTuning.cs` |
| Stats holder / pushes tuning into shared components | `EnemyStats` | `Assets/Scripts/Enemies/EnemyStats.cs` |
| Movement + detection + state machine | `EnemyBrain` | `Assets/Scripts/Enemies/EnemyBrain.cs` |
| Animator driving + attack-release/finish events | `EnemyAnimation` | `Assets/Scripts/Enemies/EnemyAnimation.cs` |
| Attack execution (melee box / ranged spawn / self-destruct) | `EnemyAttack` | `Assets/Scripts/Enemies/EnemyAttack.cs` |
| Ground movement/slope/terrain-phase (shared) | `Gameplay.GroundMotion` | `Assets/Scripts/Gameplay/GroundMotion.cs` |

**Legacy path** (`Assets/Scripts/Gameplay/EnemyController.cs` + `Assets/Scripts/Combat/RangedAttack.cs`) — monolithic: `EnemyController` handles patrol/chase/ledge/contact-damage/death in one class; `RangedAttack` is a separate monolithic firing component. Used by hand-built/legacy enemies and boss-side adds (`SummonAddsAttack`). `EnemyFactory` explicitly strips both when regenerating a prefab (both write `Rigidbody2D.linearVelocity` per `FixedUpdate` and would fight the new quartet).

**Split vs. monolithic:** New pipeline cleanly splits into 4 single-purpose components + `EnemyTuning` data, `EnemyBrain` as an explicit state machine (`Idle/Approach/Retreat/Attacking/Cooldown/Waking/Returning/Dead`). Legacy is monolithic. No enemy-specific interfaces exist; components wire via `GetComponent` + events (`Health.Died`, `EnemyAnimation.AttackReleased/Finished/WakeFinished`). Relevant existing interfaces: `Core.IPooled`, `Combat.IKnockbackReceiver` (neither implemented by enemies).

**Creating a new enemy today — hybrid, recipe-driven:** author an `EnemyRecipe` ScriptableObject asset (sprite sheet ref, presence, `EnemyTuning`, tier) → run `EnemyFactory.Generate` via a per-character `<Name>Pack.cs` script → factory builds sprite/collider, stamps `Health/Knockback/HitFlash/Corpse/CurrencyDropper` + the quartet, wires animation, builds a pooled projectile if ranged, retires legacy components. Hand-tuned values on the live prefab win on re-runs (recipe only seeds on first generation or if the component is missing).

**Inspector-exposed vs. hardcoded:** almost everything gameplay-relevant lives on `EnemyTuning`. Hardcoded constants found in `EnemyBrain.cs` (`RetargetInterval=0.5f`, `LedgeProbeInset=0.05f`, `LedgeProbeRise=0.35f`, `WalkableDrop=1.5f`, `HomeArrival=0.15f`, `Reach()=extents.x+0.4f`), `EnemyStats.cs` (`ApproachBandMargin=2f`), legacy `EnemyController.cs` (its own `RetargetInterval`/ledge constants). `Projectile.cs`, `ShotProjectile.cs`, `ShotBeam.cs` each independently hardcode `GroundMask = 1 << 6` as a `const int` rather than a shared/configurable value.

**Type representation:** `EnemyArchetype` enum (`Static/Melee/Ranged/FlyingMelee/FlyingRanged`) + `AttackKind` (`Melee/Ranged`, only for `Static.staticAttack`). Derived bools on `EnemyTuning` computed from archetype (`Flies`, `Airborne`, `IsRanged`, `Moves`, `Retreats`, `Sleeps`) — never duplicated state. Two independent opt-in flags: `sleepsUntilDetected`, `selfDestruct`. Consumers of the raw enum: `EnemyBrain.cs`, `EnemyTuning.cs`, `EnemyStatsEditor.cs`, and each pipeline pack. `EnemyFactory` itself is archetype-agnostic (only reads derived bools).

## 2. Attacks & Projectiles

**Two parallel systems**, unified only at the target-filtering/damage-application layer:

1. **`Gameplay.Projectile` + `Abilities.ProjectileFactory` + `Abilities.ProjectileSpec`** — used by `EnemyAttack.Shoot`, bosses (`BulletHellAttack` etc.), legacy `RangedAttack`. `Projectile` (`RequireComponent(Rigidbody2D)`) exposes speed, lifetime, damage, hitLayers, knockbackMultiplier, pierceCount, homingTurnRate/Range, arcGravity, impactRadius/Damage, pooled-or-destroy. `ProjectileSpec` is the reusable serialized data block embedded in `EnemyTuning.projectile`/boss attack assets. `ProjectileFactory.Spawn` follows the FX-placeholder convention (prefab+marker → `PrefabPool`; else pooled code sprite).

2. **`Items.ShotProjectile` + `Items.ShotBeam`** — player weapons/items only. Self-contained, `IPooled`, built in code, driven by `WeaponShot` (parallel field set: speed/lifetime/size/pierce/homing/arcGravity/impact/split/element/tint) — conceptually near-identical to `ProjectileSpec` but independently implemented.

**No shared base class/interface** across `Projectile`/`ShotProjectile`/`ShotBeam` — three separate classes reimplementing homing, piercing, arc gravity, explode-on-impact. The only real cross-path sharing is `Combat.Teams.Allied` (checked independently inside each) and `AbilityHit`/`AbilityContext` for melee/AoE (`AbilityContext` = per-cast struct; `AbilityHit` = static damage/overlap utility). Used by `EnemyAttack.Strike/Explode`, boss AoE attacks, player melee — **not** by `ShotProjectile`/`ShotBeam`, which reimplement their own target filter inline instead of calling `AbilityHit.IsValidTarget`.

**Conclusion:** player weapons and enemy/boss attacks are two parallel projectile runtimes, unified only at the policy level, not the code level.

## 3. Bosses

`BossController.cs` (~1100 lines) replaces the entire `EnemyBrain` role via **coroutines** (`FightLoop/RunAttack/Telegraph/EnterPhase/VulnerableWindow/GuardWindow`) rather than a per-frame state enum. No boss equivalent of `EnemyStats` (tuning split between `BossController` fields and `BossDefinition/BossPhase/BossAttack` assets); `BossAnimator` is optional/thin, not a forced-parallel to `EnemyAnimation`. Attack content is one `BossAttack` abstract-ScriptableObject subclass per archetype (`Run(BossContext)` coroutine) — closer to the weapon-modifier pattern than to `EnemyAttack`'s single fixed script. `BossContext` wraps an `AbilityContext` plus arena geometry/pace/FX — richer than anything on the enemy side.

**Shared with enemies:** `Health`, `Knockback` (bosses set `Immune=true`), `Combat.Teams`, pooling (`Core.Pool`/`PrefabPool` for all boss runtime FX), `AbilityHit`/`ProjectileFactory` via `BossContext.Ability`. **Fully custom:** `BossController`, the `BossAttack` hierarchy, `BossPhase`/`BossDefinition`, `BossContext`, `BossHealthBar`, `BossAnimator`, arena measurement/snap, the vulnerable/guard armor-window system (`Health.DamageMultiplier`).

**Phase/pattern system:** `BossPhase` (array in `BossDefinition`) — `startsAtHealth`, `attacks[]` deck, `pauseBetweenAttacks`, `damageScale`, `damageTakenMultiplier` (armor, repaired if zero-initialized), `speedScale`, transition FX/shake, `openingAttack`, `frenzyBelowHealth/SpeedScale`. Phase never moves backward. `PickAttack` does weighted random draw filtered by `AvailableInPhase` (`minPhase`) and `IsReady` (attack-count cooldown + real-time cooldown), with a `minPhase`-respecting fallback if the pool empties. Loop: `Telegraph()` (fixed wait, or animation-event-driven if the attack has a `Gesture`) → `attack.Run(ctx)` (stored so a phase change can cut it) → optional `EnterVulnerable` → `Recovery` wait, all scaled by `PaceOf` (`speedScale` × frenzy).

## 4. Scalability assessment

**Adding a new enemy type with a novel mechanic (e.g. shield) — moderate, not drop-in.**

- A mechanic expressible as pure numeric knobs (flat damage reduction, regenerating shield HP) fits cleanly: add a field to `EnemyTuning`, read it from a small new/extended component — mirrors the existing `Health.DamageMultiplier`/`Invulnerable` pattern.
- A mechanic that changes *decisions* (e.g. "only vulnerable from behind") does **not** fit without core edits: `EnemyBrain` is a hand-written `switch` over a closed `State` enum with no extension hooks; `EnemyTuning.archetype` is a closed enum whose derived bools are consumed directly by `EnemyBrain`, `EnemyStats.Apply`, `EnemyStatsEditor`, and every pack. There is no `IEnemyBehaviour`/strategy interface anywhere — everything is concrete `GetComponent` wiring in `EnemyBrain.Awake`. `EnemyAttack.Execute` is a hardcoded 3-way branch (`selfDestruct → Explode`, `IsRanged → Shoot`, else `Strike`), not pluggable.
- Contrast with bosses: a new archetype there is just a new `BossAttack` subclass dropped into the deck, zero edits to `BossController`. Enemies have no equivalent per-attack extensibility point.
- **Files touched for a structurally new mechanic:** `EnemyTuning.cs` (new flags/derived props), `EnemyBrain.cs` (new state/branch), `EnemyAttack.cs` (new `Execute` branch), `Enemies/Editor/EnemyStatsEditor.cs` (Inspector visibility), possibly `Pipeline/Editor/EnemyFactory.cs`. Same shape as the precedent set by `sleepsUntilDetected`/`selfDestruct` — known, bounded, multi-file, not single-asset.

**Coupling / duplication / hardcoding blocking a JSON-driven pipeline:**
- `EnemyBrain` and legacy `EnemyController` independently reimplement near-identical ledge/slope-follow logic (intentional per CLAUDE.md, but any terrain fix currently needs applying twice while both paths live).
- `Gameplay.Projectile` and `Items.ShotProjectile` independently reimplement homing/pierce/arc-gravity/explode/target-filtering with different field names for the same concepts (`ProjectileSpec.pierce` vs `WeaponShot.pierce`).
- `GroundMask = 1 << 6` hardcoded as a separate literal in three files (`Projectile.cs`, `ShotProjectile.cs`, `ShotBeam.cs`) instead of one shared constant; `EnemyController`/`BossController` expose it as a configurable `LayerMask` instead.
- `EnemyTuning.obstacleLayers` defaults to a magic-number mask `(1<<6)|(1<<8)` rather than resolving by layer name like `GroundMotion.TerrainMask` — two paths that can drift, audited/repaired by `ContentAudit`.
- `EnemyRecipe`'s only runtime bridge is an editor-only `EnemyStats.EditorSetTuning` — clean separation, but means no live/runtime JSON-reload path exists today; `EnemyTuning` relies entirely on Unity's built-in Inspector serialization with no independent (de)serializer.

**Naming/namespace/folder conventions (verified):** namespaces map 1:1 to top-level folders — `RedMagic.Enemies`, `RedMagic.Gameplay`, `RedMagic.Combat`, `RedMagic.Abilities` (+`Abilities/Runtime/`), `RedMagic.Items` (+`Items/Shot/`), `RedMagic.Bosses` (+`Runtime/`, `Definitions/`, `Editor/`), `RedMagic.Pipeline` (+`Editor/`), `RedMagic.Core`. Editor-only code isolated into per-module `Editor/` subfolders (not inline `#if UNITY_EDITOR`, except small guarded methods like `EnemyStats.EditorSetTuning`/`OnValidate`). Per-character packs strictly named `<Name>Pack.cs` (`OgroPack`, `LoboPack`, `DragonPack`, `CaballoPack`, `GorilaPack`, `ChampiPack`, `AbejaPack`, `TreeWalkPack`, `MurcielagoPack`, all under `Pipeline/Editor/`). `EnemyRecipe.ResolvedFolder` defaults to `Assets/Prefab/Enemies`.
