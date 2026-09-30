# Procedural rig — bodies built from separate sprites (`Assets/Scripts/Rigging/`)
For enemies/bosses whose art is **one sprite per part** (torso, head, arms…) instead of a sprite
sheet. Animation is code, not clips.

## Pieces
- **`ProceduralRig`** — *data only*, shared by every driver: `body` (the `Visual` root: flipped for
  facing, leans, hovers), `parts` (`RigPart` list: sway move/rotate/scale/speed/phase,
  `pivot`, `breaksOnDeath`), aim (`aimPart`, `aimArtAngle`, `aimMaxAngle`), attack timings
  (`windup`, `recover`, anticipation, recoil), hurt, death break-apart. Captures the prefab pose
  once and `RestorePose()` puts back pose **and hierarchy** (death reparents pieces) → pool-safe.
- **`RigPart.pivot`** — real joint point relative to the sprite pivot. The part art is exported
  on full canvases (1024² with the piece off-centre), so the sprite pivot can sit far from the
  piece; sway, scale pulse and death spin happen around `pivot` instead. **Art is never edited.**
- **Sprite swaps** — `spriteSwaps` (`RigSpriteSwap`: renderer + `asleep`/`awake`/`attacking`
  sprites, empty = unchanged). `Attacking` is the telegraph: set at windup start, back to `Awake`
  after recover; death shows `Asleep`. Swaps are data, never hardcoded.
- **Fold / unfold** (sleep/wake) — `stretchPart` scales Y from its pivot (`foldedStretch` when
  asleep), `stretchRiders` ride its tip (offset = rest distance × stretch, squashed to
  `foldedRiderScaleY`, `foldedRiderOffset`). Wake = OutBack with `wakeOvershoot` + eye flicker
  (eyes open once at the start of the last `wakeFlickerPortion`; `wakeFlickers` > 0 = optional blinks) → `WakeFinished`; sleep = InBack back to 0.
  `ProceduralRig.OutBack`/`InBack` are DOTween's exact formulas so both passes match.
- **Muzzle flash** — `muzzle` + `muzzleFlash*`: pooled `AbilityFx.Flash` fired by the shared
  `RigDriver.RaiseReleased`, so every driver flashes identically.
- **Recoil** — windup ends with the barrel on target; after release the arm holds `recoilDelay`
  (shot visibly leaves the barrel), then kicks over `recoilKick` with easing.
- **`RigDriver`** (abstract) — the engine. Contract: `SetFacing` (applied immediately),
  `PlayAttack(aimPoint, speed)` → **`AttackReleased` exactly at the end of `windup`** →
  `AttackFinished` after `recover`; `PlayHurt`, `PlayDeath`, `PlayWake` (→ `WakeFinished`),
  `PlaySleep(instant)`. Gestures advance only with scaled time **and** `GameStateManager.CanPlayerAct`.
- **`RigDriverCode`** — pass B (plain code): `LateUpdate` recomposes every part from
  `rest + sine sway + gesture layers`; gestures are coroutines that only write a few numbers
  (aim weight, arm rotation, push, body tilt/kick), so overlapping gestures never fight. Lean
  comes from measured velocity (works for physics, boss AI or knockback alike). Death: pieces
  reparent to the root, fly with gravity, bounce on `breakGround` (Ground+Platform), then
  `Corpse` fades/destroys the whole enemy.
- **`RigDriverTween`** — pass A (DOTween). *Not built yet.*

## Enemy integration
- `EnemyAnimation` finds a `RigDriver` on the root; if present **it wins over Animator/flipbook**:
  every order is forwarded, and the driver's events play the role of the clip's
  `AnimationEvent`s. The timer fallback is only a safety net (duration + 1s).
- `EnemyAttack.AimPoint` exposes the frozen aim point so the rig aims the arm at the same point
  the projectile flies to. A **`Muzzle` nested under the aim part** is found recursively and its
  *world* position is used (a root-level `Muzzle` keeps the old mirrored-local behaviour).
- Timings scale with `EnemyTuning.attackAnimSpeed` / `wakeAnimSpeed`.

## Sleeping enemies (any archetype)
`EnemyTuning.Sleeps` is now just `sleepsUntilDetected` (a sleeping `Static` = folded turret);
`Detects` (`Moves || Sleeps`) gates awareness/disengage. A static sleeper wakes on
`detectionRange` or a hit, goes to `Idle` (not `Approach`) after `WakeFinished`, and on giving up
folds in place (`Returning` → `FallAsleep` immediately). `FallAsleep` calls
`EnemyAnimation.PlaySleep(false)`; the brain's first `Update` calls `PlaySleep(true)` so a sleeper
spawns folded (not in `OnEnable`: the rig resets its pose there). Animator enemies ignore
`PlaySleep` (their Idle clip already is the sleep).

## Two-pass comparison (code vs DOTween)
Base prefab (`Assets/Prefabs/Enemies/Procedural/<Name>.prefab`) carries **everything except the
driver**: enemy stack, `EnemyStats`, `ProceduralRig`. Variants add the driver:
`<Name>_Code` (`RigDriverCode`) and `<Name>_Dotween` (`RigDriverTween`). Tune on the base → both
passes change together; the comparison only differs in the engine.

## Tooling (Tools ▸ RedMagic ▸ Procedural)
- `1 · Preparar enemigo por partes (selección)` — `ProceduralRigTool.Setup`: adds the enemy stack
  (Health, Knockback, HitFlash, Corpse, CurrencyDropper, EnemyStats/Brain/Attack/Animation,
  SoundEmitter, Rigidbody2D, collider if missing), `ProceduralRig` with one `RigPart` per sprite
  under `Visual` (golden-angle phases), and migrates old `ProceduralSway` values (the component,
  now in `Legacy/`, is removed). Idempotent.
- `2 · Crear variante _Code (selección)` — driver variant.
- `Packs/Torreta (Turret)` — see Enemies below.
- `Packs/Dron (Robot)` — `ProceduralEnemyPacks.RunDrone()`: the above + drone data (aim arm,
  `Muzzle` at the gun tip, flame/ring pivots, FlyingRanged tuning, `Corpse` linger 1.1s) + the
  placeholder projectile `Assets/Prefabs/Fx/Enemies/Robot/Fx_Robot_Projectile` (disc +
  `FxPlaceholderStyle`). Enemy-specific values are seeded **only the first time** (no rig yet);
  afterwards the Inspector wins.

**New part-based enemy** = drop the body prefab (root + `Visual` + parts) in the folder, run
`1` + `2`, set `aimPart`/`Muzzle`/tuning in the Inspector (or add a pack method if it will be
regenerated).

## Enemies
- **Robot** (TestProcedural art) — FlyingRanged drone; `RightArm` is the gun (`aimArtAngle` 11.5°).
- **Turret** (jungle art) — Static+Ranged sleeper. `Packs/Torreta` restructures once: `neck` under a
  `NeckPivot` at its bottom edge (neck sprite pivot is centred), the three head objects merged into
  one `Head` + a `RigSpriteSwap`, sorting neck 2 / head 3 / base 4 (base in front → folded reads as
  a closed box), `Muzzle` between the eyes, box collider for the unfolded body. Laser placeholder
  `Fx_Turret_Laser` (sliver, `faceDirection`). Base `breaksOnDeath = false`.
- **BossIce2** (Boss2 art) — part-based variant of the Coloso de Hielo, see *Bosses* below.

## Bosses (named gestures, driven parts)
- **`RigGesture`** (`ProceduralRig.gestures`) — a named pose pair: `windup` (reached with easing
  over the attack's telegraph) and `strike` (snapped to in `strikeTime` after release), then back
  over `recover`; `shake` trembles the body at the end of the windup. Poses are `RigPose`
  (part + move/rotate/scale, additive on the composed pose; on the `body` X/rotation follow
  facing). `BossAttack.gesture` names it — a new gesture is data, never code.
- **`BossAnimator` rig path** — with a `RigDriver` on the boss it wins over the Animator:
  `PlayGesture` → `RigDriver.PlayGesture` (release event = `Released`), `Hold`/`ReturnToRest` →
  `HoldGesture`/`ClearHold` (charge travel pose), `PlayStagger` → `PlayHurt(staggerStrength)`
  (phase change), flinch on every hit (additive, ≥0.2s apart), death → `PlayDeath`.
- **`BossController`** — `bodyRenderer` picks the sprite the telegraph aura copies (the torso);
  with a rig, facing flips the whole `Visual` (`RigDriver.SetFacing`), its own sway is skipped and
  `SetBodyVisible` toggles every part.
- **Driven parts** — `RigDriver.DrivePart(part, worldPos, spin)` lends a part to an attack (the
  driver stops composing it); `ReleasePart(part, seconds)` blends it back onto the live body. A
  lease (`drivenLease`) auto-returns parts whose attack was cut (phase change): nothing is ever
  left floating. Trajectories live in the attack → identical in every driver.
- **`RigPart.center`** = visible centre (contact point for attacks); **`leanFollow`** = extra
  rotation × body lean (negative = trails like a pendulum: the ice tip, the fists).
- `ProceduralRigTool.Setup(..., enemyStack: false)` for bosses (they bring their own stack).
- `Packs/Coloso de Hielo por partes (BossIce2)` — `ProceduralBossPack`: definition
  `Boss_ColosodeHielo2` = copy of `Boss_ColosodeHielo` (same phases/pacing/health/contact) with
  key `boss.coloso_de_hielo_2`; root stack copied component-by-component from the ice prefab
  (BossController, Mover, Health, Corpse…, no Animator) + `BossAnimator` + `BossLimbs`; `Visual`
  at scale 0.38, 3.9 above the feet (same size as the original); parts renamed Tip / Crown /
  LeftFist / RightFist; 6 gestures (Brace, Dash, DashLoop, GroundSlam, PickUP, Throw — the last
  five match the ice attacks' gesture names, so the shared attack assets animate unchanged).
