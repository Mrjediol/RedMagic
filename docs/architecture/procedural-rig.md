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
- **`RigDriverTween`** — pass A (DOTween), built 2026-10-01. **Derives from `RigDriverCode`**: the
  composition (`LateUpdate`: rest + sway + layers, lean, inertia, driven parts, death pieces) is
  inherited as is; what changes is who moves the layer numbers — one DOTween `Sequence` per gesture
  instead of a coroutine. Same contract (`AttackReleased` via `InsertCallback` exactly at the end
  of the windup) and same `ProceduralRig` data. What it adds, all Inspector fields on the driver:
  - an `Ease` per stretch (aim, recoil, settle, gesture windup / strike / recover, wake, sleep);
  - **overlapping action**: one weight **per pose entry** instead of one per gesture, each starting
    `poseStagger` seconds after the previous and arriving together at release — order the poses of
    a gesture from the body outwards (torso, arm, hand) and the hand lags the arm;
  - `DOTween.Punch` / `Shake` for the hurt wobble (`hurtVibrato`, `hurtElasticity`), the charge
    tremble, an anticipation squash (`chargeSquash`) and a stretch on release (`releasePunch`);
  - eases with overshoot/elastic on the strike and the return to rest (weights may pass 1 or dip
    below 0 — `ApplyPose` allows it).
  Rules that bite: every chained value uses `DOVirtual.Float` with an explicit *from* (two tweens
  on the same number inside one Sequence must not depend on when DOTween reads the start value);
  sequences are `SetTarget(this)` and paused/resumed from `Update` with
  `GameStateManager.CanPlayerAct`; everything is killed in `OnDisable` (pool-safe, Domain Reload
  off). The driver's ease fields are **not** in `rig-config.json`: they are tuned in Unity and
  survive a reimport (`EnsureDriver` keeps an existing driver of the right type).
  - Comparison copies: `Tools ▸ RedMagic ▸ Procedural ▸ Packs ▸ Copias con DOTween`
    (`ProceduralTweenPack`) → `RobotTween`, `TurretTween`, `BossIce2Tween`: full copies of the three
    reference prefabs with the driver swapped and its curves tuned per character. They exist to
    compare side by side; once a driver is chosen, swap it on the real prefab and delete the copies.

## Enemy integration
- `EnemyAnimation` finds a `RigDriver` on the root; if present **it wins over Animator/flipbook**:
  every order is forwarded, and the driver's events play the role of the clip's
  `AnimationEvent`s. The timer fallback is only a safety net (duration + 1s).
- `EnemyAttack.AimPoint` exposes the frozen aim point so the rig aims the arm at the same point
  the projectile flies to. A **`Muzzle` nested under the aim part** is found recursively and its
  *world* position is used (a root-level `Muzzle` keeps the old mirrored-local behaviour).
- Timings scale with `EnemyTuning.attackAnimSpeed` / `wakeAnimSpeed`.
- **Named gestures on enemies too.** A catalog attack in the enemy's deck (`EnemyTuning.attacks`,
  see `enemies.md`) names its `gesture`; `EnemyAnimation.PlayGesture` forwards it to
  `RigDriver.PlayGesture`, so the body telegraphs each attack with its own `RigGesture` (the caster
  template's `Flick` / `Charge` / `Sweep` on the same casting arm). The driver raises the same two
  events for a gesture and for `PlayAttack`; `EnemyAnimation` keeps a gesture's to itself so they
  never fire the built-in attack, and starting the built-in attack settles a gesture still recovering.

## Sleeping enemies (any archetype)
`EnemyTuning.Sleeps` is now just `sleepsUntilDetected` (a sleeping `Static` = folded turret);
`Detects` (`Moves || Sleeps`) gates awareness/disengage. A static sleeper wakes on
`detectionRange` or a hit, goes to `Idle` (not `Approach`) after `WakeFinished`, and on giving up
folds in place (`Returning` → `FallAsleep` immediately). `FallAsleep` calls
`EnemyAnimation.PlaySleep(false)`; the brain's first `Update` calls `PlaySleep(true)` so a sleeper
spawns folded (not in `OnEnable`: the rig resets its pose there). Animator enemies ignore
`PlaySleep` (their Idle clip already is the sleep).

## One prefab, driver included
Each part-based enemy/boss is **one prefab** (`Assets/Prefabs/Enemies/Procedural/<Name>.prefab`)
carrying everything: enemy/boss stack, `ProceduralRig` **and its driver** (`RigDriverCode`). There
are no `_Code` / `_Dotween` variants any more (the old pairs were merged on 2026-09-30, the
variant's values winning). Trying another engine = swapping the `RigDriver` component
(`ProceduralRigTool.EnsureDriver`); the rig data is driver-agnostic, so nothing else changes.

## Tooling (Tools ▸ RedMagic ▸ Procedural)
- `1 · Preparar enemigo por partes (selección)` — `ProceduralRigTool.Setup`: adds the enemy stack
  (Health, Knockback, HitFlash, Corpse, CurrencyDropper, EnemyStats/Brain/Attack/Animation,
  SoundEmitter, Rigidbody2D, collider if missing), `ProceduralRig` with one `RigPart` per sprite
  under `Visual` (golden-angle phases), and migrates old `ProceduralSway` values (the component,
  now in `Legacy/`, is removed). Idempotent.
- `2 · Poner motor (RigDriverCode) (selección)` — `EnsureDriver`: the driver on the prefab itself.
- `Packs/Torreta (Turret)` — see Enemies below.
- `Packs/Dron (Robot)` — `ProceduralEnemyPacks.RunDrone()`: the above + drone data (aim arm,
  `Muzzle` at the gun tip, flame/ring pivots, FlyingRanged tuning, `Corpse` linger 1.1s) + the
  placeholder projectile `Assets/Prefabs/Fx/Enemies/Robot/Fx_Robot_Projectile` (disc +
  `FxPlaceholderStyle`). Enemy-specific values are seeded **only the first time** (no rig yet);
  afterwards the Inspector wins.

**New part-based enemy** = drop the body prefab (root + `Visual` + parts) in the folder, run
`1` + `2`, set `aimPart`/`Muzzle`/tuning in the Inspector (or add a pack method if it will be
regenerated) — **or import it as data** (below).

## As data: `rig-config.json` (Tools ▸ Web ▸ Rig)
The whole body is a JSON (`docs/schemas/rig-config.schema.json`), separate from the sprite-sheet
pipeline. Code in `Rigging/Editor/RigConfig/`:
- `RigConfigExporter` — prefab → JSON (hierarchy under `Visual`, `RigPart`s, every
  `ProceduralRig` field by reflection, swaps, gestures, `BossLimbs`, root collider/corpse, boss stack).
- `RigBundleImporter` — `.zip` / folder → one prefab with its driver. Builds the hierarchy, then
  `ProceduralRigTool.Setup` (enemy stack) or `BossBodyBuilder.AddBossStack` (boss), then writes the
  rig. Optional `enemy-config.json` (tuning via `EnemyConfigImporter.ReadTuning`) or
  `boss-config.json` (`BossConfigImporter`, no `body`). Body = always from JSON; EnemyStats / boss
  stack only first time or with *reset*.
- `RigConfigJson` — shared vocabulary: part refs are **paths relative to `Visual`** (`"Visual"` =
  body), sprites are `Assets/…` paths (used as-is) or bundle files (copied to
  `Assets/Art/RigImports/<name>/`, import settings from `sprites`).
- Gestures are authored in the web too: Rig Composer ▸ *Gestos* poses `windup` / `strike` on the
  canvas and previews them with this driver's formulas (`rig-gestures.js` mirrors `ApplyPoses`,
  `GestureRoutine` and `SettleRoutine` — keep both in step).
- Web side: the 🦴 Rig Composer authors the bundle; its **body templates** (floating caster / folding
  sentry / limbed colossus — `Web/RedMagicWeb/modules/rig-templates.js`, data) are the generic
  versions of the three packs above and ship default gestures in `gestures`.
- **Pivot = centre of the body**, for enemies and bosses: the web lays the body around the origin.
  A boss also carries `root.feet` → a `Feet` child wired to `BossController.feet` (see `bosses.md`).
  Web-exported `sortingOrder` starts at **1** (0 draws behind the scene background).
- `RigConfigRoundTrip` — static acceptance test (no Play): Robot / Turret / BossIce2 → fixtures in
  `docs/schemas/fixtures/rig/` → reimport → reflection compare → cleanup. Rerun after touching
  `ProceduralRig` or either side of the pipeline. The packs above stay as the hand-written reference.

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
- **Inertia** — `followStiffness` / `followDamping` / `followMaxOffset` (0 = off): the `Visual` hangs from the
  root on a spring (lags when the root starts, overshoots a little when it stops). BossIce2 55 / 8 / 0.7.
- **Move gesture** — `moveGesture` names a gesture held (cross-faded) while `SetMoving(true)` and
  settled on stop; never overrides another gesture. BossIce2 `Move`: lean forward, fists/tip trail,
  crown drifts back.
- **Gesture cross-fade** — starting a different gesture moves the old one to a fade layer (0.2 s)
  instead of dropping its weights, so chained gestures blend (charge → travel pose). A gesture with
  an **empty `strike`** is a held charge: no whip at release, the windup settles over `recover`
  (the boss `Dash`: crouch back, hold while the direction locks, blend into `DashLoop`).
- **Stages** — `RigSpriteSwap.stages` = one sprite per stage; `RigDriver.SetStage(n)` applies them
  (`BossAnimator` calls it on `BossController.PhaseChanged`: BossIce2's cracked `torso2` in phase 2).
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
- **`RigPart.pointAngle`** = where the part's business end points in its unrotated art (fists:
  -90 = knuckles down). `BossLimbs.AngleToward(limb, worldDir)` turns it into a local angle through
  the parent's matrix (mirroring included) and `DriveCenter(limb, centre, localAngle)` places it;
  `LimbMotion.Fly/Hold` (wrapped by `LimbAttack`) take one world direction per part (`Aim(...)`, null = rest orientation)
  and finish the turn in the first half of the move, so a thrown fist leads with its knuckles.
  **Upright mirroring**: if turning a limb would leave it upside down (the art axis perpendicular to
  `pointAngle` facing down), `BossLimbs` mirrors it across its pointing axis instead — a fist
  pointing left is the mirror of one pointing right, like a character flipping. The mirror is kept
  until halfway through the rig's return blend.
  **Warnings are part-sized**: `ProceduralRig.PartSize(t)` = world size of the sprite's tight-mesh
  bounds (what is drawn, not the canvas); `LimbAttack.WarnPart` draws a short arrow from the part
  (`arrowLength` × part size) and a target mark (`targetMarkScale` × part size) — never sized by the
  flight distance (the arrow prefab fits uniformly to its length, so a path-long arrow gets fat).
  Both are clamped (`arrowLengthRange` 2.2–5 u, `targetMarkRange` 1.1–2.5 u): proportional, but never
  too small to see nor screen-covering.
  **Grips**: a `Grip` child at each fist's centre (`BossLimbs.grips`) is where held props attach.
- **`RigPart.center`** = visible centre (contact point for attacks); **`leanFollow`** = extra
  rotation × body lean (**positive** = exaggerates it: a hanging part trails behind like a pendulum — the ice
  tip 0.8, the fists 0.4; negative = counteracts it, stays upright). Same sign rule in gesture poses:
  a hanging part rotated **negative** swings back (facing is mirrored for free).
- `ProceduralRigTool.Setup(..., enemyStack: false)` for bosses (they bring their own stack).
- `Packs/Coloso de Hielo por partes (BossIce2)` — `ProceduralBossPack`: definition
  `Boss_ColosodeHielo2` = copy of `Boss_ColosodeHielo` (same phases/pacing/health/contact) with
  key `boss.coloso_de_hielo_2`; root stack copied component-by-component from the ice prefab
  (BossController, Mover, Health, Corpse…, no Animator) + `BossAnimator` + `BossLimbs`; `Visual`
  at scale 0.38, 3.9 above the feet (same size as the original); parts renamed Tip / Crown /
  LeftFist / RightFist, crown split into an empty `Crown` + `left`/`center`/`right` crystals
  (each bobs on its own; they are `BossLimbs` orbiters), fists drawn in front of the torso; 6 gestures (Brace, Dash, DashLoop, GroundSlam, PickUP, Throw — the last
  five match the ice attacks' gesture names, so the shared attack assets animate unchanged).
