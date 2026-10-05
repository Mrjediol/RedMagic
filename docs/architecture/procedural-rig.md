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
  sprites + `named`: `RigNamedSprite` for `Alert` / `Hurt` / `Dead` / `Gesture` + gesture name).
  The rig keeps the visual state (asleep|awake, attacking, gesture in course, alert, hurt, dead) and
  **`ProceduralRig.RefreshSprites`** resolves each renderer: dead (`Dead` → `asleep`) → hurt → the
  gesture in course → attacking → alert → asleep/awake → the stage → the prefab's sprite. **An empty
  state lets the next one through** (it no longer leaves the previous sprite stuck). Who sets what:
  `ShowSprites(Asleep|Awake|Attacking)` (sleep / wake / built-in attack, as before),
  `ShowGestureSprites` / `EndGestureSprites` (driver: `PlayGesture` counts as an attack, so
  `attacking` is the fallback; held gestures only use their own), `SetHurtSprites` (while the hurt
  lasts), `ShowDeadSprites`, `SetAlertSprites` ← `RigDriver.SetAlert` ← `EnemyAnimation.SetAlert` ←
  `EnemyBrain.Enter` (Approach / Retreat / Attacking / Cooldown / Waking). `RestorePose` resets it
  (pool-safe). "In attack 2 this arm looks different" = a `Gesture` sprite on that part for the
  gesture that attack names. A gesture sprite has a **window** inside its gesture — `showFrom` /
  `showTo` on one scale, 0–1 windup, 1–2 strike, 2–3 recover (default 0–3 = all of it): "stretch the
  hand, show the finger, hide it, pull back". The driver reports the moment each frame
  (`ProceduralRig.SetGestureMoment`; a held pose runs 0→1 and stays, any settle is 2→3) and the
  sprites are only re-resolved when one enters or leaves its window. Swaps are data, never hardcoded.
- **Fold / unfold** (sleep/wake) — `stretchPart` scales Y from its pivot (`foldedStretch` when
  asleep), `stretchRiders` ride its tip (offset = rest distance × stretch, squashed to
  `foldedRiderScaleY`, `foldedRiderOffset`). `stretchPart` may be a plain node or a RigPart (make-enemy's
  `Stem`): `RigDriverCode.ComposeParts` applies the stretch in both cases. A rider with an `asleep` sprite
  gets `foldedRiderScaleY` = 1 from make-enemy (the art is already folded). Wake = OutBack with `wakeOvershoot` + eye flicker
  (eyes open once at the start of the last `wakeFlickerPortion`; `wakeFlickers` > 0 = optional blinks) → `WakeFinished`; sleep = InBack back to 0.
  `ProceduralRig.OutBack`/`InBack` are DOTween's exact formulas so both passes match.
- **Muzzle flash** — `muzzle` + `muzzleFlash*`: pooled `AbilityFx.Flash` fired by the shared
  `RigDriver.RaiseReleased`, so every driver flashes identically. It is a **placeholder** (a white
  square without `muzzleFlashSprite`): a gesture that brings its own effects (`RigGesture.fx`) does
  not flash, and `muzzleFlashSize = 0` turns it off for the built-in attack too.
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
- **One driver.** `RigDriverCode` is the only engine. A DOTween one (`RigDriverTween`) was built and
  compared on the drone, the turret and BossIce2 (2026-10-01) and retired to
  `Assets/Scripts/Legacy/RigDriverTween.cs`: no visible gain in game, and it costs more — a dozen
  tweens and closures allocated per gesture against zero-allocation composition, a second engine to
  keep in step with every rig feature, and tuning (eases) that lived on the component instead of
  in `ProceduralRig`, so it did not travel in `rig-config.json` nor reach other enemies. **What
  scales here is data, not engines**: a new motion = a `RigGesture` (web ▸ Gestos) or a field on
  `ProceduralRig`, read by the one driver and reusable by every body. Do not add a second driver;
  if a gesture ever needs parts arriving one after another or a curve of its own, add it as a field
  of `RigGesture` (the legacy file shows the per-pose-weight approach). Per-action squash / stretch
  / elastic bounce was tried there and rejected by the user as a default: keep recoil and bounce as
  opt-in numbers per enemy (`recoilDistance`, `bodyRecoil`, `wakeOvershoot` — 0 = none).

## A gesture is a timeline (2026-10-02)
This replaces the fixed "wind-up → release → strike" everywhere below: where the older sections say
*phase* read *step* (`Windup` = step 0, `Strike` = step 1, `Recover` = the stretch after the last step).
- **`RigGesture.steps`** (`RigGestureStep`): any number of steps, each a pose (measured from rest; a part
  not listed returns to rest), a `duration`, an `ease` (InOut / Out / In / Linear) and a `shake`. The body
  holds **one weight per step** (`RigDriverCode._weights`) and each step blends from wherever it was to
  "only this pose"; `recover` blends everything to rest. A cut cross-fades the old gesture underneath, as before.
- **`aim`** on a step: it ends with the aim part (`rig.aimPart`, the weapon) pointing at the attack's target
  (`RigDriver.AimTarget`, the point the host froze). `RigDriverCode.ApplyGestureAim` runs after the gesture's
  poses and turns the part, from wherever they left it, until its barrel (pivot → muzzle, `aimArtAngle`) passes
  through the target — weighted by the sum of the weights of the `aim` steps (`GestureAimWeight`), exact at
  full weight, overriding the part's own rotation in the pose and **not limited by `aimMaxAngle`** (that limit
  is the native attack's, `AimRotation`). To keep aiming through the next step, mark that one too.
  **Which parts aim is the step's own data** (2026-10-03): `RigGestureStep.aimParts` (`RigAimPart`: part + `angle` =
  its barrel in its unrotated art, from its `RigPart.pivot`); empty = `rig.aimPart` with `aimArtAngle`, as before.
  Several = all at once (both hands), each gesture its own (the knight's sword in `Slash`, its left hand in
  `DarkOrb`). It travels inside the gesture (JSON `steps[].aimParts: [{ part: path, angle }]`), so 🎬 *Gestos y
  ataques ▸ Guardar en Unity* carries it without touching the body — the body's single aim role (built-in attack,
  muzzle tool) only lives in a full Enemy Studio save. Web: the step's 🎯 box lists the parts with a "Cañón (°)"
  each (`aimPartsBox`, gesture-timeline.js), the canvas draws each barrel, `G.aimTargets` mirrors the driver.
- **`stretch`** on a step: its duration is given by whoever launches the gesture (the attack's `telegraph`)
  so one gesture serves attacks with different warnings — the stretch steps share what is left for the
  FIRST release to land exactly at that time (`RigGesture.Durations`). No stretch step = the gesture
  keeps its own clock and the attack waits for it (`RigDriver.SecondsToRelease` is what the hosts wait on).
- **`RigGesture.releases`** (`RigRelease`: step, `at`, `point`, `offset`): the instants the attack LEAVES
  — one or several per gesture — and from where (a part or node; empty = the muzzle). At each one the
  driver sets `ProceduralRig.CastPosition` and raises `AttackReleased`. None written = once, at the end of
  the last step. Who listens: the built-in attack fires once per release (from `CastPosition`); a catalog
  attack starts at the first one (`IAttackBody.Released`) and can follow the rest through
  `ReleaseCount` / `ReleaseTotal` — `BulletHellAttack.volleyPerRelease` fires one volley per release.
  `IAttackHost.CastPoint` = `CastPosition`. **`ReleaseAttack`** is the attack built on this: one entry per
  release — what leaves there (projectile / fan / ring / strike / nothing), see `enemies.md`.
- **Effects** (`RigFx`): `step` + `at` instead of a phase; a loop lives until the end of `untilStep`
  (-1 = its own step). `facing`: **Direction** (default — at the muzzle, where the weapon points, pivot →
  muzzle; on the body / feet, where the body is moving, else where it faces), **Anchor** (turns with its
  part, the old behaviour) or **World**; `angle` adds to it. `layer`: Front / **Behind** the body (the
  parts are lifted one sorting step once, in `RigDriver.CacheFxContext`, since 0 is behind the scene).
- **A full turn does not unwind**: on the way back to rest (and in the cross-fade layer) pose rotations
  go the short way (`_settling`), so a step that leaves a part at 360° is already at rest to the eye.
- **Gesture sprite windows** (`showFrom` / `showTo`) are in steps: 0–1 the first… and the last stretch
  is the recover. For the classic two-step gesture the numbers are the old 0–3.
- **Old data**: a prefab saved before this carries `windup` / `strike` / `strikeTime` / `shake` and effects
  by phase; `RigGesture.OnAfterDeserialize` converts them on load (effects keep `facing = Anchor`) and
  *Tools ▸ Web ▸ Rig ▸ Convertir gestos a pasos* re-saves the prefabs (done for the five that existed).
  The classic gesture is `RigGesture.Classic(…)`: step 0 `stretch`, release at its end, step 1 the strike.
- **JSON**: `gestures[]` = `{ name, steps[], releases[], recover, fx[] }` — what Unity exports and what
  the web writes (Enemy Studio and `tools/comfy/make-enemy.mjs`, both through `rig-export.js`; `fx` is
  always present, so removing an effect in the web removes it in Unity). The classic shape (`windup` /
  `strike` / `phases[].fx`, old web exports) is still read and converted by the importer.
- **Web editor**: the timeline is authored in 🧬 Enemy Studio ▸ *Gestos* (`gesture-timeline.js`, a dock
  under the canvas: steps, releases, effects, a playhead). `rig-gestures.js` mirrors `Durations`,
  `EffectiveReleases`, `GestureRoutine`, `SettleRoutine` and `ApplyPose`; `rig-fx.js` mirrors
  `FireFx` / `EndFxStep` — **change both sides together**. `rig-export.js ▸ readRigGestures` reads a
  rig-config's gestures (Unity's export included) back onto a body in the web, by part path.
- `Legacy/RigDriverTween.cs` no longer compiles (guarded by `#if`): it spoke of the two fixed poses.

## Gesture effects (`RigFx`)
A `RigGesture` has three **phases** — `Windup` (lasts the attack's telegraph), `Strike`
(`strikeTime`; zero-length when the gesture has no strike pose) and `Recover` — and `RigGesture.fx`
hangs effects from them: `prefab`, `phase`, `at` (0–1 inside the phase), `anchor` (`Part` + `part`,
`Body`, `Muzzle`, `Feet` = bottom of the root collider), `offset` (anchor-local, so facing mirrors
it), `scale`, `followsPart`, `loop`. Data only, authored in the web (Enemy Studio ▸ Gestos).
- **Who spawns**: the base `RigDriver` (`FireFx` / `TickFx` / `EndFxPhase` / `StopFx`), always through
  `PrefabPool`, never parented (a pooled instance must not die with its enemy): a follower is
  re-placed every `LateUpdate` **after** the body is composed. `RigDriverCode.GestureRoutine` reports
  each phase's progress; a held pose (`HoldGesture`, the move gesture) only fires its `at = 0`
  windup effects, once. Size = prefab × `scale` × the body's world scale; drawn one order above the
  front-most part, same sorting layer.
- **Lifetime**: `loop` = lives until its phase ends (or the gesture is cut: another gesture, death,
  disable) and the driver despawns it — its prefab must have **no** `VfxOneShot` (art kind `fxLoop`).
  Otherwise it plays once: a prefab with `VfxOneShot` (art kind `fx`) returns itself to the pool and
  the driver only follows it; a prefab without one is despawned after its flipbook's duration.
- **Pipeline**: `gestures[].phases[].fx` in rig-config + its own `artAssets` block (same format as a
  boss's). `RigBundleImporter.BuildGestureArt` copies `Art/` next to the parts, builds one pooled
  prefab per entry with `BossArtBuilder` (`Assets/Prefabs/Fx/Enemies/<name>/`, bosses
  `Fx/Bosses/<name>/`) and swaps each `{"art": id}` for its prefab path; `phases` present replaces the
  gesture's effects, absent keeps them. The exporter writes prefab paths. `RigConfigRoundTrip` has a
  synthetic `GestureFx` case (every anchor and phase), since the reference prefabs carry none.
- The built-in attack (`PlayAttack`) has no phases of its own: it keeps the muzzle flash. To put
  effects on it, make it a catalog attack with a gesture (a `ReleaseAttack`, see `enemies.md`).

## Walking (`ProceduralRig.gait`, 2026-10-03)
`RigGait`: `feet` (`RigGaitFoot`: part + `phase`) and `layers` (`RigGaitLayer`: part + `phase`, `swing` world
x/y once per cycle, `bob` twice per cycle, `rotate` once per cycle around its pivot), plus `stride`, `fullSpeed`,
`minStride`, `duty`, `lift`, `footTilt`, `bodyBob`, `maxSpeed`, `settleTime` — all world units. No feet = nothing.
- **Feet are placed in root space**, not body space (`RigDriverCode.ApplyGait`): each foot's rest pivot (its sole =
  `RigPart.pivot`) is cached at `Awake` in root space, all at the lowest one's height (one ground). A planted foot
  slides back exactly what the body moves, so it does not skate; in the air it arcs (`lift`) and tilts toe-down at
  take-off, toe-up at landing. Lean, body bob, follow spring and hurt squash never lift it. **A foot must hang
  straight from `Visual`** (the importer warns otherwise). The root may be scaled: rest goes through
  `TransformPoint`, stride / lift are added in world units.
- **The cycle advances with distance** (`TickGait`: measured `vx × Facing`, smoothed): faster = more cadence,
  backwards = walks backwards. Stopped, airborne (`|vy| > 2.5`) or faster than `maxSpeed` (a dash, knockback) the
  step closes in `settleTime` (`_gaitAmp`, `_gaitWeight`). Step length scales with speed (`minStride`…1), and the
  cycle length with it, so slow steps are short and still exact.
- **Order in `Step`**: `TickGait` → body (+ `GaitBodyBob`) → parts → `CaptureFeet` → gestures → `ApplyGait` → aim.
  A gesture's pose ON a foot is kept (measured from the ground); a pose on `Visual` no longer drags the feet.
- **`RigDriverCode.Step(dt)`** is the frame composition (`LateUpdate` calls it with `Time.deltaTime`): editor
  previews call it at a fixed step. **`RigFilmstrip`** (Editor) uses it: `Walk(prefab, png, speed, cells, …)` = a
  strip of a body walking with a world ground ruler (a planted foot stays on its tick), `Poses(prefab, png, gesture)`
  = rest + each step (and the midpoint) of a gesture, still. Call them with `unity command eval`; no Play Mode.
- JSON: `gait` block (exporter / importer, present = replaces, absent = kept); web: `mapGait` (rig-model.js), the
  template's `gait` by slot id → document by uid → export by path, read back by `readRigDocument`. The web does
  not preview it yet. `RigConfigRoundTrip`'s synthetic case carries one.

## Idle gestures (`ProceduralRig.idle`)
`RigIdleGestures`: a pool of gesture **names**, `interval`, `chance`, `windup`. `RigDriverCode.TickIdle`
plays one at random (never the same twice in a row) every `interval` ±25% s with probability `chance`,
only while truly resting: alive, no target (`RigDriver.SetAlert` — the player out of its detection), not
moving (`SetMoving`), no gesture / attack / hurt in course, no part lent to an attack. An enemy that sleeps
until it detects IS resting while asleep, so it plays them asleep too — unless its body folds to sleep
(`ProceduralRig.CanFold`). Never with a target: a gesture between attacks would be a *transition* gesture,
a separate feature (not built). It runs the ordinary
`GestureRoutine` in **silent** mode — same poses, effects, sprites and sound, but **no
`AttackReleased` / `AttackFinished` and no muzzle flash**, so nothing can mistake it for an attack. Any
real gesture replaces it with the usual cross-fade; falling asleep settles it. JSON: `idleGestures`
block (present = replaces, absent = kept). Web: the "💤 Gesto de reposo" checkbox on a gesture card.

## Transition gestures (`ProceduralRig.transition`)
`RigTransitionGestures`: a pool of gesture **names**, `chance`, `windup`. When an attack ends
(`EnemyBrain.OnAttackFinished` → `EnemyAnimation.PlayTransition(cooldown)` → `RigDriverCode.PlayTransition`),
with probability `chance` it plays one of them — only among those whose whole length
(`RigGesture.TotalSeconds(windup)`) **fits in the cooldown**, never the same twice in a row. Silent, like an
idle gesture (no `AttackReleased`). Optional: an empty list does nothing. Enemies only (a boss's pause
between attacks does not call it). JSON: `transitionGestures`. Web: the "🔁 Gesto de transición" checkbox on a
gesture card + the "🔁 Entre ataques" fold.

## A gesture's sounds
`RigGesture.sounds` — any number of `RigGestureSound` (`SoundCue` + `moment`): each plays at `Start`, `Release`
(the first release) or **`At`** — its own instant, `step` + `at`, fired in `FireStep` like an effect. `At` is
what the web writes: through the bridge a gesture carries `sounds = [{ clips[], volume, pitchMin, pitchMax, step,
at }]` (`RigConfigExporter.GestureSounds` → `/enemy` ▸ `sounds`, a flat row per sound with its `gesture`;
`RigBundleImporter.ReadGestureSounds`: present = replaces, `[]`/`null` = clears, absent = kept by name; each
keeps the priority / rolloff of the cue that was in the same place). Clips are project asset paths (`GET /sounds` lists them)
or files the bundle brings (`Audio/<file>`, a sound uploaded in the web): `RigBundleImporter.CopyBundleAudio`
copies those into `Assets/Audio/Sfx/Enemies/<name>/` before the prefab is touched and reports each new one.
A file export (`Exportar rig a JSON`) does not write them. A clip new to the game still has to be graded in
the Sounds tab.

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
- **Partial import** (`"partial": true` in rig-config — the web's "🎬 Gestos y ataques" export):
  `RigBundleImporter.ImportPartial` applies it over an **existing** prefab and writes only `gestures` /
  `idleGestures` / `spriteSwaps` (`WriteSwapsAndGestures`) and, from `enemy-config.json`, the deck and the
  defences. Body, rig fields, EnemyStats and the Inspector's tuning are not touched; it never goes through
  `ResetTuning`. A part the JSON names that the prefab lacks is an error (the body changed: export it whole).
- **The bridge** (a third way in, next to import / export — `Rigging/Editor/Bridge/`, plan and decisions in
  `docs/plan-unity-web-bridge.md`): the web's 🎬 tab reads an enemy from the project and saves into it over
  HTTP, no files; Enemy Studio saves a whole enemy (new or existing) the same way. `WebBridgeServer` =
  `HttpListener` on `127.0.0.1:7810` (next free port), **started with the editor** unless turned off in
  Tools ▸ Web ▸ Puente (`EditorPrefs`, persists across sessions; any web button or "Encender" turns it back
  on), per-session token in the URL Unity opens or handed out by `GET /hello` (answered on the listener
  thread, only to a request whose `Origin` is the configured web — the browser lets no other page read it;
  never typed), CORS limited to the web URL (`EditorPrefs`, edited in Biblioteca
  Jere ▸ "Web"), requests drained on the main thread. `WebBridgeRoutes`: `GET /ping`, `GET /enemy`
  (`RigConfigExporter.Export` + `EnemyConfig` with the deck's full cards from `BossAttackExporter.FillDeck`,
  + per-sprite rect / pivot / ppu, gesture sounds, effect prefab → library id, `revision`), `GET /file`
  (png / wav / ogg / mp3 under `Assets/`), `POST /gestures` (the partial zip → `ImportWithReport`; **409** when
  the prefab changed since it was read, is dirty in Prefab Mode, or Unity is playing — `force=1` overwrites),
  `GET /enemies` (every prefab with `ProceduralRig`, found by script guid in the prefab text, skipping vendored /
  legacy folders), `POST /rig` (the WHOLE Enemy Studio zip → the normal full import: with `prefab` into that
  one, else `ProceduralEnemyPacks.Folder/<name>.prefab` — **409 `exists`** if already there, until `force`).
  After a domain reload the listener comes back **on the same port** (the watchdog in `Drain` retries it
  while the old socket is released — hopping ports would leave the open web tab talking to nobody).
  `WebBridgeMenu.Open(prefab, gesture, attack)` is the one entry point: the button on `EnemyStats` /
  `BossController` (with a "✎ web" per gesture and per deck card), `ProceduralRig ▸ ⋮`, Biblioteca Jere.
  `BossAttackExporter` = a `BossAttack` asset → its card JSON, by reflection over the fields
  `BossCatalogExporter` publishes; art comes out as `{ "project": "<prefab path>" }` and
  `BossArtBuilder.ResolveArtRefs` reads that form back as the same reference (nothing is rebuilt).
  For the same reason a rig bundle's `Art/` is **merged** into `Assets/Art/RigImports/<name>/Art`, never
  mirrored (`BossBundleImporter.Sync(prune: false)`): art the bundle does not carry is still in use.
- `RigConfigJson` — shared vocabulary: part refs are **paths relative to `Visual`** (`"Visual"` =
  body), sprites are `Assets/…` paths (used as-is) or bundle files (copied to
  `Assets/Art/RigImports/<name>/`, import settings from `sprites`).
- Gestures are authored in the web too: 🧬 Enemy Studio ▸ *Gestos* edits each one as a timeline and
  previews it with this driver's formulas (see "A gesture is a timeline ▸ Web editor").
- Web side: 🧬 Enemy Studio authors the bundle (its body editor is `rig-composer.js`; the old 🦴 Rig
  Composer tab was retired); its **body templates** (floating caster / folding
  sentry / limbed colossus / ground humanoid — `Web/RedMagicWeb/modules/rig-templates.js`, data) are the generic
  versions of the three packs above and ship default gestures in `gestures`. **`groundHumanoid`** (2026-10-03) is
  the first walker: Cape, BackHand, BackFoot, Hips, Torso, Head, FrontFoot, WeaponHand — fracture-style floating
  parts; the two feet hang from `Visual` (not the torso) so a gait can plant them. First body: `FrostKnight`.
- **Pivot = centre of the body**, for enemies and bosses: the web lays the body around the origin.
  A boss also carries `root.feet` → a `Feet` child wired to `BossController.feet` (see `bosses.md`).
  Web-exported `sortingOrder` starts at **1** (0 draws behind the scene background).
- `RigConfigRoundTrip` — static acceptance test (no Play): Robot / Turret / BossIce2 → fixtures in
  `docs/schemas/fixtures/rig/` → reimport → reflection compare → cleanup. Rerun after touching
  `ProceduralRig` or either side of the pipeline. The packs above stay as the hand-written reference.
  Its last case is the **deck**: every attack asset of every part-based enemy → card JSON → written back
  into the same asset → must be identical (what the bridge does on every save).

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

## Editing a Unity-read body in Enemy Studio (whole save)

A body read from Unity (`readRigDocument`, `state.unityBody`) CAN be saved whole with Enemy Studio ▸ Exportar ▸ 💾 Guardar en Unity (`POST /rig`, updates the prefab in place). Parts keep their project sprite by reference (`image.unityRef`, no new PNG); the joint is written as `node.pivot` = offset from the sprite's own pivot (`image.unityOrigin`, set on read). Entry point from the web: 📋 Tareas ▸ Enemigos ▸ sheet ▸ **✎ Editar cuerpo** (`designs-panel.js` `onEditBody` → `enemyStudio.openFromUnity`; the export box then shows "← Volver a la ficha", which reopens the sheet and redoes its picture). Unity needs to be open only for the bridge — nothing is done in Unity.
