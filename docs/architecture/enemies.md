# Enemies (`Assets/Scripts/Enemies/`)
The base every non-boss enemy is built on. **Four components on the root, and only one of them is
meant to be edited by hand.**

- **`EnemyStats`** — *the* component you touch. Holds an `EnemyTuning` block with **everything**
  tunable (type, health, knockback, ranges, speeds, attack, per-state animation speed, target tag,
  layers) and hands out what belongs to shared components: it pushes into `Health` and `Knockback`
  on `Awake` and on inspector edits, because those are used by the player and bosses too and can't
  depend on anything enemy-specific. Nothing else in the enemy stack carries numbers of its own —
  they all read from here. `EnemyStatsEditor` draws it flat and hides the fields that don't apply
  to the chosen archetype. Gizmos show the ranges on selection.
- **`EnemyTuning`** is a plain `[Serializable]` class, used by **both** `EnemyStats` and
  `EnemyRecipe`, so the list of tunables is written once and the pipeline copies it rather than
  translating field by field.
- **Five archetypes** (`EnemyArchetype`): `Static`, `Melee`, `Ranged`, `FlyingMelee`,
  `FlyingRanged`. Movement and attack kind are folded into one enum because that's how you actually
  think when creating an enemy — except `Static`, which takes a separate `staticAttack`
  (`Melee`/`Ranged`) since a turret can be either.
- **`EnemyBrain`** — the state machine: `Idle → Approach → Attacking → Cooldown → Idle`, plus
  `Retreat` and `Dead`. `Static` skips detection entirely: it waits in idle and attacks the moment
  the target is inside `attackRange`. Ground movement keeps the ledge probe; **flyers steer at
  nothing — they go straight at the target because terrain no longer exists for them** (see
  `GroundMotion` below; the old whisker-raycast avoidance was deleted along with
  `EnemyTuning.avoidProbeDistance`). Target range checks use a cached transform + distance, **not**
  a trigger collider — cheaper than bodies in the broadphase and no collision-matrix setup.
  - **The leash is `detectionRange`, and only that** — it is the boundary in both directions and
    the gizmo drawn in the scene. Engage on entering it; give up after `loseInterestGrace` seconds
    (default **1s**) *continuously* outside it, timer resetting on every re-entry (that time margin
    is the hysteresis — there is no second radius). Only distance counts: a jump or a step never
    drops the target. An earlier pass added a wider `loseInterestRange` and an `alwaysChaseOnceSeen`
    "chase forever" toggle; both were removed because they made the visible detection ring
    meaningless — **do not reintroduce a second range or an override for a value the user
    configured** (see the `configured-ranges-are-the-contract` memory).
  - **Only ranged movers retreat.** `EnemyTuning.Retreats` (`Moves && IsRanged && personalSpace > 0`)
    is the single rule, read by the brain, the gizmos and the inspector so they can't desync. A
    melee enemy chases and hits — it never backs off, even if it carries a stale `personalSpace`
    from a previous archetype (which the inspector hides and the gizmo no longer draws). `Retreat`
    has a hysteresis band (`retreatReleaseFactor`) and, if it backs into a wall, gives up retreating
    and fights instead of freezing.
  - **Sleepers and kamikazes** — two orthogonal `EnemyTuning` flags. `sleepsUntilDetected`:
    `Idle` is the sleep clip; on detection (or on being hit) the brain enters `Waking`, plays
    `Wake` (non-interruptible, timed by clip length → `EnemyAnimation.WakeFinished`), then goes
    straight to `Approach`; when it gives up it enters `Returning`, flies/walks back to its spawn
    point and sleeps again (never `Idle` mid-air). `AnimClipBuilder` wires
    `Idle →(trigger Wake)→ Wake →(exit)→ Walk`. `selfDestruct`: `EnemyAttack.Execute` → `Explode`
    (`AbilityHit.DamageCircle` at `explosionRadius` + `Health.Die`); the `Attack` clip is the fuse,
    `Death` is the explosion, and `EnemyStats.Apply` turns `Corpse.tintOnDeath` off.
    `rootedWhileAttacking = false` now actively keeps chasing during the attack clip. Reference:
    `MurcielagoPack`.
- **`Gameplay.GroundMotion`** — walking on slopes and phasing through terrain, shared by
  `EnemyBrain` and the legacy `EnemyController` so both move the same way:
  - **Slopes.** An enemy is a dynamic `Rigidbody2D` whose velocity the AI writes. Writing only
    `velocity.x` into a ramp is pushing head-on against it: the enemy shakes at the foot of the
    slope, and when it stops, gravity slides it back down. `Probe` measures the surface with three
    downward rays (tail, centre, muzzle, from `0.35` above the feet so a few centimetres of
    penetration don't blind it) and `AlongSlope` returns the surface **tangent** at the same speed,
    so up, down and flat all cost the same — the dynamic-body equivalent of the player's
    `ProjectOnSlope`. `Halt` is the other half: stopped **and resting**, velocity goes to zero on
    both axes, which is what stops the slide. `MaxSlopeAngle` is 45°, deliberately the same as the
    player's: a ramp one of them can climb and the other can't reads as a broken level.
  - **Flyers phase through terrain.** `PhaseThroughTerrain` sets the collider's `excludeLayers`, so
    no new layer and no change to the project's collision matrix. Only terrain is excluded — a
    flyer still collides with the player, so contact damage is unchanged. The trigger is
    `EnemyTuning.Airborne` (`Flies || gravityScale <= 0`), which also covers the hovering turret
    (the bee is `Static` with gravity 0, not a `Flying*` archetype, but it is just as much in the
    air).
  - **`obstacleLayers` must contain BOTH terrain layers**, `Ground` (6) *and* `Platform` (8) — the
    sloped bridges and most platforms in `Forest-1` are on `Platform`. With the old `1 << 6`
    default, an enemy reaching a bridge probed for ground, found none, read it as a cliff and stood
    still on perfectly solid footing — and on the ramp it found no surface to follow.
    `GroundMotion.TerrainMask` is the canonical mask (resolved by layer *name*), and
    `Pipeline ▸ 4/5` audits and repairs the mask on every enemy prefab and `EnemyRecipe`.
- **`EnemyAnimation`** — drives the Animator and, crucially, **announces the exact frame the hit
  leaves**. The attack clip carries two `AnimationEvent`s planted by the pipeline
  (`OnAttackRelease` at the authored frame, `OnAttackFinished` at the end); the brain waits for the
  second to start cooling down. Falls back to a timer (`attackReleaseFallback`) when a clip has no
  events. Per-state speed goes through one Animator float per state (`IdleSpeed`, `AttackSpeed`, …)
  so idle can be slow while the attack is fast.
- **The Animator lives on the ROOT, not the sprite child** — `AnimationEvent`s only reach
  components on their own GameObject, and the brain/attack live on the root. Clips animate the
  child by path (`AnimClipBuilder.RendererPath` = `"Sprite"`). Moving it back would silently break
  every attack.
- **`EnemyAttack`** — executes on the release event: `ProjectileFactory.Spawn` for ranged,
  `AbilityHit.DamageBox` for melee, so damage and pooling go through the same paths as weapons and
  bosses. It aims at a **world point** frozen when the attack starts (the target collider's
  *centre*, not its pivot), and computes direction from the **muzzle** at release. Aiming
  pivot-to-pivot is the bug that makes a shot from hand height sail over the target's head.
- `EnemyController` (`Assets/Scripts/Gameplay/`) is the **older** system, still used by
  hand-built enemies and boss adds (`SummonAddsAttack`, `RunManager`, `WorldSceneGenerator`), so it
  stays. `EnemyFactory` strips it (and `RangedAttack`) from anything it generates — both write
  `Rigidbody2D.linearVelocity` every `FixedUpdate`, so they'd fight over the body.
