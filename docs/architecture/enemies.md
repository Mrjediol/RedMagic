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
- **Inspector**: `EnemyStatsEditor` (and the boss `BossControllerEditor`) draw one **foldout per
  `[Group]`** (`GroupAttribute` replaces `[Header]` on `EnemyTuning` and `BossController`: same look in any other inspector, silent inside the foldouts) (`FoldoutInspector`): archetype, ranges, attack, deck and projectile open by default, the rest
  (health, knockback, movement, sleep, contact, sound, animation, target) folded; open state is remembered per
  group. Nothing is hidden beyond the archetype rules. New tunables: put them under a `[Group("…")]`.
- **`EnemyTuning`** is a plain `[Serializable]` class, used by **both** `EnemyStats` and
  `EnemyRecipe`, so the list of tunables is written once and the pipeline copies it rather than
  translating field by field.
- **Five archetypes** (`EnemyArchetype`): `Static`, `Melee`, `Ranged`, `FlyingMelee`,
  `FlyingRanged`. Movement and attack kind are folded into one enum because that's how you actually
  think when creating an enemy — except `Static`, which takes a separate `staticAttack`
  (`Melee`/`Ranged`) since a turret can be either.
- **`EnemyBrain`** — the state machine: `Idle → Approach → Attacking → Cooldown → Idle`, plus
  `Retreat`, `Evading` (defensive moves, below) and `Dead`. `Static` skips detection entirely: it waits in idle and attacks the moment
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
  - **Sleepers and kamikazes** — two orthogonal `EnemyTuning` flags. `sleepsUntilDetected` (any
    archetype — a sleeping `Static` wakes on `detectionRange` and folds back in place; see
    `procedural-rig.md`):
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
- **Part-based enemies** (one sprite per body part) use a `ProceduralRig` + `RigDriver` instead of
  clips; `EnemyAnimation` forwards to the driver when one exists and its events replace the
  `AnimationEvent`s. A `Muzzle` nested under an animated arm is used by world position. See
  `procedural-rig.md`.
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
- **Several attacks per enemy — the deck** (`EnemyTuning.attacks`, a list of `EnemyDeckEntry`). A
  normal enemy can launch **catalog attacks**: the same `BossAttack` assets bosses use (see
  `bosses.md` ▸ *Who launches an attack*). Empty deck = only the built-in attack, exactly as before;
  no sprite-sheet enemy needed migrating.
  - **Who decides what.** `EnemyBrain` still decides *when* (`attackCooldown` is the pause between
    two attacks, whichever they are); `EnemyAttack.Choose(distance, heightDelta, reachable)` draws
    *which*, by weight, among: the built-in attack (if `builtInAttack` and the target is within
    `attackRange` and vertical reach — weight `builtInWeight`) and every card that is in reach
    (`minRange` … `maxRange`, 0 = `attackRange`; `targetHeight` Any / TargetAbove / TargetBelow with
    a 0.5 u margin) and off cooldown (the asset's own `weight`, `cooldownInAttacks`,
    `cooldownSeconds` — same rules as a boss). Nothing available = no attack this frame; the brain
    keeps approaching. A card with `maxRange` > `attackRange` fires while still approaching.
    `selfDestruct` ignores the deck. `minPhase` and punish windows do nothing on an enemy.
  - **The own attack again, with another gesture.** A card with `builtIn = true` (no catalog attack)
    repeats the enemy's OWN attack — same projectile / strike, same numbers — launched with its
    `gesture` (and its own `weight`, ranges, height rule). It does not go through
    `EnemyAttackHost`: `EnemyAttack.BuiltInGesture` → `EnemyAnimation.PlayAttack(gesture)` plays that
    rig gesture (or Animator state) and the release/finish events flow to the normal `Execute`
    (shot from the Muzzle at the frozen aim point). Web: Enemy Creator ▸ Ataques ▸ *Ataque propio* ▸
    "＋ Repetir el ataque propio con otro gesto". Schema: `tuning.attacks[]` with `builtIn`,
    `gesture`, `weight` (no `attack`).
  - **Who runs it.** `EnemyAttackHost` (the enemy's `IAttackHost`): telegraph (the card's
    `gesture`, else `telegraph` seconds) → `BossAttack.Run` → `recovery` → `DeckAttackFinished`,
    which the brain treats like the end of the built-in attack. It holds the shared FX slots
    (warn / circle / arrow / shockwave / sweep / hazard / platform prefabs — empty = code
    placeholders) and the attack "arena" (`arenaHalfWidth` / `arenaHeight`, centred on the enemy,
    ground measured per cast with `obstacleLayers`). Feet = optional `feet` child, else the bottom
    of the collider (root pivot is the body centre). `EnemyAttack` adds the component at runtime if
    a prefab with a deck lacks it; the importers add it to the prefab.
  - **Gestures.** `EnemyAnimation` is the enemy's `IAttackBody`: `PlayGesture(name, seconds)` plays
    a `RigGesture` of that name (part-based body, timed to release at the end of the telegraph) or
    the Animator state of that name (sprite sheet, released by its `OnAttackRelease` event at its own
    speed). While a deck attack runs, releases belong to it and never fire the built-in attack
    (`_inGesture` / `_rigGesture`). No such gesture = plain timed telegraph. Three spells from one
    hand = three cards with three gestures.
  - **Where it comes from.** Web 🧩 Enemy Creator ▸ *Ataques* → EnemyConfig `attacks` (+ `artAssets`)
    and `tuning.attacks` → `EnemyDeckImporter` (used by both `EnemyConfigImporter` and
    `RigBundleImporter`): one asset per attack in
    `Assets/ScriptableObjects/EnemyAttacks/<Enemy>/EnemyAttack_<Enemy>_<id>.asset`, art prefabs in
    `Assets/Prefabs/Fx/Enemies/<Enemy>/`. Unlike the rest of the tuning, **the deck is rewritten on
    every import** (it is authored in the web, like a boss's attacks).
- **Defensive moves** (`EnemyTuning.defences`, a list of `EnemyDefence` — `EnemyDefence.cs`). Data: a
  **trigger** (`TargetNear` + `distance`, `OnHit`, `LowHealth` + `healthFraction`, once per life), `chance`,
  `cooldown`, a **move** (`DashAway`, `HopBack` + `hopHeight`, `Strafe`), `moveDistance` / `moveDuration`, a
  `gesture` and `anticipation`.
  - **The motion is generic**: `DefenceMotion` (static, no brain / animation knowledge) gives the duration
    and the velocity for an instant — a burst that decelerates and integrates to exactly `moveDistance`; a
    ground `HopBack` is a real jump (impulse from `hopHeight`, lasts until it lands), a flyer draws the arc;
    `Strafe` is perpendicular for flyers and falls back to dashing away on the ground.
  - **The brain runs it**: `EnemyBrain.TryDefend` (near: in `Update`, before retreat / attack; hit and low
    health: in `OnDamaged`) → state **`Evading`**: `anticipation` seconds still, then `EvadeStep` writes the
    velocity each physics step, then back to `Idle` for the next decision. It never interrupts an attack, a
    wake or another evade; a failed roll waits 1 s before rolling again; a ground enemy does not start (or
    continue) over a ledge (`CanAdvance`); knockback pauses it; `Static` enemies ignore the list.
  - **Body**: `EnemyAnimation.Hold(gesture)` while it lasts (the gesture's windup pose, its `at = 0` effects
    and its sprites — never an attack event), `ReturnToRest` at the end. Sound: `SoundTrigger.OnEvade`.
  - **Pipeline**: `tuning.defences` in enemy-config, read by `EnemyDeckImporter.ReadDefences` and, like the
    deck, **rewritten on every import** when present. Web: Enemy Creator / Studio step 4 ▸ *Defensa*.
- `EnemyController` (`Assets/Scripts/Gameplay/`) is the **older** system, still used by
  hand-built enemies and boss adds (`SummonAddsAttack`, `RunManager`, `WorldSceneGenerator`), so it
  stays. `EnemyFactory` strips it (and `RangedAttack`) from anything it generates — both write
  `Rigidbody2D.linearVelocity` every `FixedUpdate`, so they'd fight over the body.

## Flyers and solid ground · gesture pickers · cast point (2026-10-02)
- **A flyer goes through platforms only.** `EnemyTuning.flyThroughLayers` (default `Platform`) is what an airborne
  enemy's colliders exclude (`EnemyStats.Apply` → `GroundMotion.PhaseThroughTerrain`); `Ground` stops it, flying or
  evading (`DashAway` used to carry it out of the map). `obstacleLayers` is still "what counts as terrain".
- **`[GestureName]`** on a string field (`EnemyDefence.gesture`, `EnemyDeckEntry.gesture`): the Inspector shows a
  dropdown with the gestures of the `ProceduralRig` on the same object (`GestureNameDrawer`); no rig = text field.
- **Cast point**: `IAttackHost.CastPoint` (default = feet; `EnemyAttackHost` = the rig's muzzle, which moves with
  the gesture), `BossContext.CastPoint`. `BulletHellAttack.fromCastPoint` makes Radial / Fan leave from it and
  skips the warning mark — the charge gesture and its effects are the telegraph. A catalog attack measures from
  the feet unless it says otherwise.
- The coloured square at an attack is always a **placeholder**: the warning mark of an attack with no
  `WarnPrefab`, or the rig's muzzle flash when the gesture that fires has no effect of its own (`RigGesture.fx`).
  A finished enemy has effects on its gestures (and `muzzleFlashSize` 0).
