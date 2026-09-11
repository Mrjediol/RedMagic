# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

RedMagic is a 2D mobile roguelite built in Unity 6000.3.23f1 (URP 2D Renderer). Loop: menu → hub
(manage upgrades/build, pick a tomb) → a run through a world's sections → boss → back to the hub
(on death or completion).

## Response style

- Skip pleasantries: no introductory or concluding filler ("Sure, I can help with that", "Let me
  know if you need anything else"). Start directly with the answer.
- Be concise: 2-3 sentences max outside of code/lists.
- Limit explanations: code only, or a bulleted list, with no explanation unless explicitly asked.
- Terse mode: drop non-essential articles and narrative explanation; keep technical accuracy.

## Commands

There is no separate build/lint/test CLI — everything goes through the Unity Editor.

- **Compile check**: if a Unity Editor is open on this project, `unity status` (from the Unity CLI)
  shows its port; `unity command recompile` triggers a recompile and `unity command recompile_status`
  polls it (`errors: []` = clean). This is much faster than opening the editor from scratch and is
  the way to verify a C# change compiles.
- **Console output**: `unity command console --level error --tail 50` reads the live editor's
  console without needing a screenshot.
- **Compile check with the Editor closed** (or stuck in Safe Mode, which is exactly when you need
  it most): `dotnet build Assembly-CSharp.csproj` and `dotnet build Assembly-CSharp-Editor.csproj`
  from the project root. Unity keeps those two `.csproj` files in sync with the real file list, so
  this is a genuine check of the same sources — add `-t:Rebuild` to defeat caching. **Verify the
  file list first** (`grep -c "Compile Include" Assembly-CSharp.csproj` against
  `find Assets -name '*.cs' -not -path '*/Editor/*' | wc -l`): if Unity died before regenerating
  them, the projects are stale and a "0 errors" is meaningless because your new files aren't in it.
- **Scene inspection**: `unity command list_open_scenes`, `get_scene_hierarchy`, `find_gameobjects`,
  `get_component_properties` read the actual loaded scene state — prefer these over parsing scene
  YAML by hand when an editor is connected, since they reflect what Unity actually deserialized
  (see the SceneReference gotcha below).
- **Builds**: `unity build --target <platform>` / `unity command build` trigger a Player build via
  the CLI; there's no CI config in this repo.
- **Play Mode via the CLI ticks far slower than real time when the Editor window has no focus/OS
  input** — `Time.realtimeSinceStartup` and `Time.frameCount` still creep forward, but a component's
  own `Update()`/coroutines can sit frozen for many real seconds between two `eval` calls, only
  visibly catching up in a burst right after `unity command editor_focus`. A single `eval` reading a
  timer twice with a real-time gap in between is **not reliable evidence** that gameplay logic is
  broken — it may only prove the harness never gave Unity a reason to tick. To actually verify
  timed/AI/physics behavior: call `editor_focus` immediately before the window you're measuring,
  or invoke the private `Update`/coroutine method via reflection to force one step deterministically,
  or use `capture_game_view` (a real screenshot is unambiguous — see the ranged-enemy debugging in
  the pipeline doc for a worked example) rather than trusting polled state across an idle gap.
- **Tests**: `com.unity.test-framework` is in `Packages/manifest.json` but no test assembly or
  `Tests/` folder exists yet — there is nothing to run.

## Architecture

### Object pooling — spawn-heavy objects are never Instantiate/Destroy'd

Anything that spawns repeatedly goes through a pool (`Assets/Scripts/Core/Pool.cs`,
`PrefabPool.cs`), built on `UnityEngine.Pool.ObjectPool<T>`:

- **`Pool<T>`** — for MonoBehaviours that build their own GameObject in code. `new Pool<T>(factory,
  prewarm)`, then `Get()` (creates one more only when empty) / `Release()` (deactivates, keeps for
  reuse). Optional `IPooled.OnReturnedToPool()` for reset. Used by `ShotProjectile`, `ShotBeam`,
  `DamagePopup`, `AbilityVfx` (the `AbilityFx.Flash` impact/muzzle flash), and the code-built
  `Projectile` path in `ProjectileFactory`.
- **`PrefabPool`** — one pool per prefab, keyed by the prefab asset. `Spawn(prefab, pos, rot)` /
  `Despawn(go)` (via the `PooledInstance` marker it stamps on). Used by `VfxOneShot`, `FxTelegraph`,
  and the prefab path of `Projectile` / `ShotProjectile` / `ShotBeam` / the boss runtime visuals
  (`BossShockwave`, `BossSweepBeam`, `BossHazard`, `BossPlatform`, `BossAnchor`) — those all fall
  back to `Pool<T>` when no placeholder prefab is assigned. `PrefabPool` has no per-instance reset
  hook, so those types reset transient state in `OnEnable`.
- **`PoolRunner`** — persistent `DontDestroyOnLoad` root that every pooled instance is parented
  under (so a scene unload never destroys the reserve) and that force-releases everything still
  active on each `sceneLoaded`, so nothing bleeds between sections.
- Domain Reload is off, so every `static` pool field is nulled in a
  `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, and `PoolRunner` clears its releaser list
  once at runtime start.
- Low-churn spawns (one `Corpse` per death, boss reward, shop, `AbilityFx.SpawnSprite` for a
  seconds-long zone/turret/orb) are still plain `new GameObject`/`Instantiate` — pool them if they
  ever become hot.

### Placeholder visuals → real art (`Assets/Prefab/Fx/`)

Every repeatedly-spawned visual (weapon projectiles, boss bullets, boss telegraph rectangles,
waves, sweep beams, hazard patches, platforms, anchors) is **built in code** as a tinted
geometric shape by default — but each now also has an **optional prefab slot** on its data
asset. Assign a prefab and the visual comes from that prefab through `PrefabPool` instead;
swapping in real art is then: open the prefab, delete the shape sprite, drop the real sprite
(or add a trail / particles / an Animator). No code, no per-consumer wiring beyond the one field.

- **`Fx.FxPlaceholderStyle`** (`Assets/Scripts/Fx/FxPlaceholderStyle.cs`) — the marker a
  *placeholder* prefab carries. `Apply(tint, worldSize, sortingRef)` does exactly the per-instance
  styling the code path does (`SpriteRenderer.color`, `AbilityFx.Resize`, `AbilityFx.CopySorting`,
  collider radius/size from `sprite.bounds`), each step gated by a bool (`tint` / `resize` /
  `matchSorting` / `scaleColliderToSprite`). The spawner rule everywhere: **null prefab** → build
  in code, restyle inline (unchanged); **prefab + `FxPlaceholderStyle`** → `PrefabPool.Spawn`,
  `style.Apply(...)`; **prefab, no marker** → real art, spawner only sets position/rotation.
- The null-prefab **code fallback stays on purpose** — safety net + `ShotPipelineHarness`.
- **`Fx.FxTelegraph`** — `PrefabPool`-backed telegraph (grow + fade + despawn), the prefab
  equivalent of `AbilityFx.Flash`. Used by `BossAttack.Warn`/`Mark` when the boss has a `warnPrefab`.
- **`Bosses.BossFxSpawn`** — the shared "prefab or code pool" branch + release routing for the
  five pooled boss runtime types.
- Prefab slots: `BaseShot.projectilePrefab` / `beamPrefab` (weapons), `ProjectileSpec.prefab`
  (boss bullets / ability projectiles — restyle-aware now), `BossController.{warnPrefab,
  shockwavePrefab, sweepBeamPrefab, hazardPrefab, platformPrefab, anchorPrefab}`.
- **Boss FX are one folder per boss**: `Assets/Prefab/Fx/Bosses/<BossName>/` holds every visual
  for that fight — `Fx_<Boss>_{Warn,Shockwave,SweepBeam,Hazard,Platform,Anchor,Bullet}` — so an
  art pass on a whole boss is one folder, and no boss reads another's prefabs. Each boss's six
  `BossController` slots point at its own copies, and its `BulletHellAttack` assets'
  `projectile.prefab` at its own `_Bullet`. Any new boss visual ships the same way, in that
  boss's folder.
- Generators under **Tools > RedMagic > FX**: `1 · Generar prefabs placeholder` (builds the shape
  sprites in `Assets/Art/Placeholder/` + the weapon prefabs + a per-boss FX set), `2 · Asignar a
  armas`, `3 · Asignar a jefes` (fills null slots per boss) — idempotent. `4 · Migrar FX de jefe
  a carpeta por jefe` was the one-time move off the old shared-by-type `Fx_Boss_*` prefabs.
- `ShotProjectile` / `ShotBeam` **and** `Gameplay.Projectile` (the boss/ability bullet) all rotate
  to face travel direction — `Projectile` sets `transform.right` in `Launch` and every
  `FixedUpdate` — so elongated art with a trail is fine everywhere, authored pointing **+X**.
  (An earlier note here claimed `BulletHellAttack` bullets don't rotate; they do.) A real-art
  prefab makes that attack's `projectileSprite` + phase accent inert (the prefab owns the look).
- The `Platform` / `Anchor` placeholder prefabs must carry their colliders (solid `Ground`-layer
  box; trigger box + `Health` + `HitFlash`) — the generator builds them precisely.
- Any **new** repeatedly-spawned visual follows this: ship it as a placeholder prefab under
  `Assets/Prefab/Fx/` with `FxPlaceholderStyle`, wired through an asset field, code fallback kept.
- **Frame animation is `Gameplay.SpriteFlipbook`, not an Animator** — a sprite array + fps, with
  `pingPong`, `randomStart` and `oneShot` (play once, hold the last frame, expose `Finished` /
  `Duration`). It rewinds in `OnEnable`, which is mandatory for pooling: `PrefabPool` has **no**
  per-instance hook, so `OnEnable` is the only reset a prefab FX gets. `VfxOneShot` only measures
  Animator clips, so a flipbook-driven one-shot must set `lifetime` = `frames / fps` by hand.
  For a *pooled* visual with **several** states, use `Pipeline.SpriteStateMachine` instead — same
  `OnEnable` rewind, plus named states (see the sprite pipeline section).
- **Real art replaces a placeholder by turning the style flags off, not by deleting art.** When a
  sprite carries its own colour, set `tint: false`; when its cell is non-square (a trail), set
  `resize: false` too and let the prefab's own transform scale fix the size — `AbilityFx.Resize`
  scales per-axis from sprite bounds and would squash a wide cell into an egg.
- **Worked example — the Árbol Ancestral's orb** (`Assets/Prefab/Fx/Bosses/ArbolAncestral/Orbe/`):
  `orbe.png` is a hand-supplied reference sheet (three strips, labels and a palette baked in).
  `Tools > RedMagic > FX > Orbe · Cortar hoja` (`Fx/Editor/OrbeSheetSlicer.cs`) segments it into
  `Orbe_Idle` (6) / `Orbe_Move` (8) / `Orbe_Impact` (6), lifts the art off the black background by
  treating the residual over the background colour as alpha, packs uniform cells anchored on the
  orb, and slices them through `ISpriteEditorDataProvider` at 100 px/unit with the pivot on the
  ball (so `Orbe_Move`'s trail hangs behind a correct rotation centre). Band Y ranges are constants
  in that file — re-run its `Diagnose`/`DiagnoseBands` entry points if the source sheet changes.
  `Tools > RedMagic > Boss > Arbol · Orbe` (`Bosses/Editor/ArbolOrbePack.cs`) then builds the
  prefabs, dresses `Fx_ArbolAncestral_Bullet` and authors the attack assets.
- The generic one-shot VFX prefabs (`Fireball`, `VFX_Explosion`, `VFX_DashWind`, `VFX_DoubleJump`)
  now live in `Assets/Prefab/Fx/` too (moved out of `Assets/Dragon Warrior Files/`; their
  material/anim dependencies stayed there).

### Persistent singletons

Three `DontDestroyOnLoad` singletons carry state across scene loads, all following the same
pattern (`Instance` static property, `Destroy(gameObject)` in `Awake` if a duplicate already
exists, manual state reset in `Awake` because Domain Reload is disabled so statics survive between
Play sessions):

- **`GameStateManager`** (`Assets/Scripts/GameStateManager.cs`) — the pause system. Menus call
  `SetPaused(true/false)`, which increments/decrements an open-menu counter so stacking a second
  menu (e.g. Options over Pause) doesn't unpause on close. `CanPlayerAct` is a **static** bool that
  every gameplay script (`PlayerMovement`, `PlayerAttack`, `RangedAttack`, `EnemyController`) polls
  directly, without needing a reference to the singleton. `ForceResume()` zeroes the counter
  unconditionally (used when leaving to gameplay/hub, where no menu should stay "open").
- **`AudioManager`** (`Assets/Audio/AudioManager.cs`) — looks up `SoundData` entries by string id
  (`PlaySFX("SFX_ButtonClick")`, `PlayMusic("Music_Menu")`) and routes them through an
  `AudioMixer` with Master/Music/SFX groups, persisting volume/mute to `PlayerPrefs`. **Gotcha**:
  an `AudioManager` GameObject is hand-placed in every scene that might be the first one loaded
  (MainMenu, MainHub), each with its own `sounds` list serialized in the Inspector. Only the first
  one loaded survives; the others self-destruct. Adding a sound to one scene's list and not the
  other's works fine in the Editor (whichever scene you hit Play from) but silently drops the sound
  in a real build that always starts at MainMenu. Keep these lists in sync until they're unified
  into one prefab.
- **`RunManager`** (`Assets/Scripts/Run/RunManager.cs`) — owns the run loop. See below.

### Scene references — never store a scene by name string

`Assets/Scripts/SceneReference.cs` wraps a `SceneAsset` (editor-only) and bakes its path/name into
serialized fields for the build. Every cross-scene link in this project
(`MainMenuController.hubScene`, `PauseMenuController.mainMenuScene`, `RunManager.hubScene`, every
`WorldDefinition` section/boss slot) uses this instead of a plain
`[SerializeField] string sceneName`. **Follow this convention for any new scene link** — a raw
string breaks silently on rename (nothing fails to compile, nothing turns pink in the Inspector,
the only symptom is `SceneManager.LoadScene` failing at runtime). Call `.ResolveForLoad(this)`
before mutating any state (pausing, stopping music) so a bad reference doesn't leave things
half-changed.

Gotcha: if you hand-edit a `SerializeField` name in a script while a scene referencing it is open
in the Editor, Unity's live scene doesn't get the renamed field until the scene is reloaded from
disk after the recompile — `open_scene` on that path (or closing/reopening it) is often necessary
to pick up the change.

### Run / world system (`Assets/Scripts/Run/`)

- **`WorldDefinition`** — a ScriptableObject per world: a pool of section `SceneReference`s, how
  many to sample per run (`sectionsPerRun`), and a boss `SceneReference`. `TryBuildRunOrder(seed,
  out order)` does a seeded Fisher-Yates sample so a run seed is reproducible, and treats
  unassigned or not-in-Build-Settings pool entries as warnings rather than hard failures. Add a new
  world by creating a new asset, not by editing code. **Tools > RedMagic > World Scene Generator**
  batch-creates a world's section/boss scenes (with a minimal playable placeholder: camera, global
  2D light, ground, `SectionEntry`/`SectionExit`), adds them to Build Settings, and wires a
  `WorldDefinition` asset with all references filled — idempotent, never overwrites an existing
  scene.
- **`RunManager`** — phase machine (`RunPhase.None/Section/Boss`) driven by `StartRun(world)` →
  `AdvanceSection()` (called by `SectionExit` triggers, or by a boss's death) → `CompleteWorld()`
  (chains into the next `WorldDefinition` in its `worlds` list, or returns to the hub). Sections are
  loaded additively (`LoadSceneMode.Additive`) and unloaded before the next one loads — never both
  in memory at once — by keeping one throwaway `SceneManager.CreateScene` "root" scene alive for
  the whole run, since Unity refuses to unload the last remaining loaded scene. The run's player is
  a separate persistent instance of `playerPrefab` (not the Player living inside whatever scene is
  loaded), so health/upgrades survive section transitions; `RunManager` repositions it at each
  scene's `SectionEntry` and retargets `CameraFollow` components after each load. Player death is
  detected via the existing `Health.Died` event — `RunManager` subscribes to it directly rather
  than `Health` knowing anything about runs.
- **`TombInteractable`** — placed in the hub, references a `WorldDefinition` asset directly (not an
  index/name) so reordering `RunManager.worlds` can't desync a tomb from its world.
- **`SectionClearTracker`** — `DontDestroyOnLoad` singleton placed in MainHub (with an
  `AfterSceneLoad` fallback). On every scene load it subscribes to every `Health` **inside that
  scene** — the run player is `DontDestroyOnLoad` so it is excluded for free — **except** anything
  that can never fire `Died`: Player-team (`Teams.Of`), inactive in hierarchy, or a
  `TrainingDummy` (`CountsAsEnemy`). Section scenes carry a *disabled* `Player` instance for solo
  testing; counting it (the old `GetComponentsInChildren<Health>(true)` scan) locked every exit
  and the boss reward forever — and exposes
  `IsCleared` / `RemainingEnemies` / `Cleared`. This is the "kill everything first" gate:
  `SectionExit` (`requireEnemiesDead`) and `ShopInteractable` both refuse to work while enemies
  live. When the last one dies it plays `clearSfxId` and spawns `clearEffectPrefab` (the fireball's
  `VFX_Explosion`) at every `SectionExit` in the scene, so the feedback points at the way out.
- **Shop placement** — `RunManager` picks `_shopSectionIndex` at random from the sampled section
  order (seeded off `RunSeed`, so it's reproducible; never the boss scene), which guarantees
  **exactly one shop per world, always before the boss** — including the degenerate case of a world
  with a single section. `SpawnShopIfDue` instantiates `shopPrefab` at a `ShopSpawnPoint` marker if
  the section has one, else `shopDistanceBeforeExit` units short of the `SectionExit`.

### Enemies (`Assets/Scripts/Enemies/`)

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

### Sprite / enemy pipeline (`Assets/Scripts/Pipeline/`)

**Read `Assets/_Pipeline/SPRITE_PIPELINE.md` before importing any sprite sheet or adding an
enemy** — it documents the whole thing and is written so this section doesn't have to be re-derived.

A sprite sheet (one row per animation state, one column per frame) becomes a playable enemy through
two ScriptableObject "recipes" and one menu command. Both recipes are assets, so the configuration
survives the session and every step is re-runnable and idempotent.

- **`SpriteSheetRecipe`** (`<Name>.sheet.asset`) — the sheet, the row→state mapping, fps/loop per
  state, background keying, anchor, ppu, and the `AnimRuntime`. **`SheetSlicer`** does not slice the
  original in place: it emits one clean PNG per state into `Assets/Art/Characters/<Name>/` with the
  background removed, labels dropped, uniform cells and the pivot on the same point of the character
  in every frame. `AutoBounds` mode finds bands as the N tallest content runs (N from the recipe, so
  painted-in row labels fall out) and frames as connected components grouped by x-overlap; a row's
  declared `frames` count is authoritative and reconciles stray blobs (a thrown projectile drawn
  loose inside a frame) — it **removes** that blob from the frame count (never merges it into a
  neighbour: a thrown projectile sits far from the body and merging stretched the row's uniform
  cell 2-3× wide, shrinking the character) and **exports it as its own centred sprite**,
  `<Name>_<Row>_Prop.png`, which becomes the projectile below. `Grid` mode is the plain
  rows×columns fallback. **When writing a pack, `SheetRow.frames` counts the character's poses,
  not the drawings in the row** — a 5-drawing attack row where the 5th is the loose projectile is
  `frames = 4`. **`SheetRow.evenSplit`** splits a row into N equal columns instead of
  connected-components, for rows whose FX bleed sideways (a slam's dust, an energy burst) and would
  otherwise merge two poses — `GorilaPack` needs it. Two further per-row knobs, both read straight
  off the sheet with **`Pipeline ▸ 2b · Diagnosticar bandas y manchas`** (prints every blob's box
  and area per band — measure, don't guess): **`SheetRow.groupSlack`** scales the horizontal margin
  that decides whether two blobs belong to the same frame (1 = default, ~1/12 of the character's
  height); lower it when neighbouring poses nearly touch (a galloping horse's tail reaches the next
  frame's muzzle and the 13px default swallows a real 3px gap, merging five frames into one) —
  prefer it over `evenSplit` whenever a real gap exists, because equal columns steal a sliver of the
  neighbour when the drawings aren't perfectly gridded. **`SheetRow.propBlobs`** declares how many
  drawings in the row are the *already-thrown projectile* rather than poses: they are pulled out
  from the right (a sheet always draws them after the last pose) **before** grouping, which is the
  case `frames` alone cannot fix — when the orb is drawn touching the muzzle it has already merged
  into the attack cell by the time the count is reconciled (measured on `Lobo`: cell 192 → 272px,
  last pose off-centre). With several, they export as `_Prop0` / `_Prop1` and the first (rightmost)
  becomes the projectile.
- **Row labels never reach the PNG**: everything outside the chosen bands is cleared from the mask
  before emitting (`Emit` copies source pixels inside a margin around the frame, so the bottom of
  the row above used to end up painted over the character), and inside a band a short blob **flush
  with the band's ceiling** is dropped as a label — the old "blob hanging above the tallest
  character" rule only fired when the word was drawn higher than the mane.
- **`AnimClipBuilder`** — sprites → one `AnimationClip` per state → `AnimatorController`. **Updates
  in place**: existing states keep their hand-tuned transitions, only clips are rewritten and missing
  states added. Parameters are the ones `PlayerAnimator` already uses (`Speed`, `Attack`, `Hurt`,
  `Dead`) — one animation vocabulary for the whole project. **`SpritePipeline.RunSheet` builds the
  clips twice with a `Refresh` between** — on the first import of a brand-new sheet the freshly
  sliced sub-sprites aren't queryable in the same tick and the first pass leaves empty clips
  (1s / 60fps / no events); the second pass fills them, and it's idempotent on every re-run.
- **`AnimRuntime` is not a preference, it follows pooling**: `Animator` for enemies/bosses;
  **`Flipbook` for anything in `PrefabPool`**, because `PrefabPool` has no per-instance reset hook,
  so a reused Animator would resume mid-death. **`Pipeline.SpriteStateMachine`** is the multi-state
  equivalent of `Gameplay.SpriteFlipbook` and rewinds in `OnEnable`.
- **`EnemyRecipe`** (`<Name>.enemy.asset`) + **`EnemyFactory`** — the enemy counterpart of
  `BossAuthoring`. The recipe carries an `EnemyTuning` block (the *same* class `EnemyStats` uses)
  plus presence (sprite scale, collider, sorting, tag); the factory stamps `Rigidbody2D`,
  `BoxCollider2D` (deduced from the sprite when left at zero; origin **at the feet for
  `AnchorMode.BottomCenter`, centred for `AnchorMode.Center`** — a flyer has no feet, and assuming
  bottom-anchored left the collider half a body high), `Health`, `Knockback`, `HitFlash`, `Corpse`,
  `CurrencyDropper` and the enemy quartet (`EnemyStats`, `EnemyBrain`, `EnemyAnimation`,
  `EnemyAttack`) — and **retires** `EnemyController`/`RangedAttack` if the prefab came from the old
  system.
- **Archetype from the sheet's filename** — content is named `<name>-<attack>-<mobility>-<plane>`
  (e.g. `Ogro-distancia-movimiento-suelo`, `abeja-distancia-statica-aire`). `Melee`/`Ranged`/
  `FlyingMelee`/`FlyingRanged` map directly; a **static turret that hovers** is `Static` +
  `staticAttack = Ranged` with **`tuning.gravityScale = 0`** (not a `Flying*` archetype — those
  *fly while chasing*), and its sheet recipe needs `anchor = AnchorMode.Center`.
- **Hand-tuned values win.** The recipe *seeds* `EnemyStats` the first time (or when the component
  is missing, which is how an old-system prefab gets upgraded); after that, regenerating the art
  leaves the numbers alone. `Pipeline ▸ 3b · Generar enemigo RESETEANDO valores` is the explicit
  way back to the recipe's values.
- **Ranged enemies** — an archetype that shoots (or `Static` + `staticAttack = Ranged`) plus a row
  that exported a prop sprite (see above). `EnemyFactory` builds a pooled projectile prefab from it
  via **`ProjectilePrefabFactory`** (no `FxPlaceholderStyle`: the sprite is already real art, lifted
  off the character's own sheet) at `Assets/Prefab/Fx/Enemies/<Name>/`, and drops it into
  `tuning.projectile.prefab`. Firing goes through `ProjectileFactory`, so it is pooled like every
  other projectile in the game. **The projectile's speed/lifetime/damage/homing knobs are on
  `EnemyStats ▸ Tuning ▸ projectile`, NOT on the `Fx_<Name>_Projectile` prefab** —
  `ProjectileFactory.Spawn` calls `Projectile.Configure` with the spec on every shot, so the
  prefab's serialized fields are overwritten each time (only its `SpriteRenderer` scale, i.e.
  visual size, survives). `EnemyFactory.MirrorSpecOntoPrefab` copies the spec onto the prefab at
  generation so it reads true, but it's still not the edit point.
- **`SheetRow.releaseFrame`** is what ties the throw to the drawing: the frame the generator plants
  the `OnAttackRelease` event on. `-1` = no event, fall back to `EnemyStats.attackReleaseFallback`.
- **`PrefabDresser`** — replaces a placeholder's visuals on an existing prefab (sprite + controller
  or flipbook, colour back to white) and **touches nothing else** — colliders, scripts, rigidbody
  and references stay as they were.
- **`ContentAudit`** (`Pipeline ▸ 4/5`) — scans every prefab with a `Health` for missing
  `Knockback`/`HitFlash`/`CurrencyDropper` and optionally adds them. That failure is silent by
  design (`Health` works fine without them; the enemy just never flinches), which is why it needs a
  scanner rather than a convention. It also completes the **terrain mask** (`obstacleLayers` on
  `EnemyStats`/`EnemyRecipe`, `groundLayers` on the legacy `EnemyController`) with any missing
  `Ground`/`Platform` layer — same class of silent failure: the enemy just stops at the foot of a
  bridge. It only adds layers, never removes what was configured.
- **Per-character packs** (`Assets/Scripts/Pipeline/Editor/<Name>Pack.cs`) — same shape as the boss
  packs: pure data that writes the two recipes and calls `SpritePipeline.RunEnemy`. **This is how a
  new character ships** — copy the file, change the data. Shipped: `TreeWalkPack` (static ranged),
  `OgroPack` (ranged mover), `AbejaPack` (hovering static ranged), `ChampiPack` (static ranged),
  `GorilaPack` (melee mover, `evenSplit` attack), `CaballoPack` (melee mover, fast charger),
  `LoboPack` (ranged mover, kites), `DragonPack` (flying ranged) and `MurcielagoPack` (sleeping
  flying kamikaze). Reproducible from git,
  runnable headless via
  `unity command run_script --file <pack> --entry <Namespace.Type.Run>`.
- One folder per character (`Assets/Art/Characters/<Name>/`), same rule as the per-boss FX folders.

### Combat (`Assets/Scripts/Combat/`)

- **`Teams`** — the one rule about who can hurt whom: **only the player damages enemies and only
  enemies damage the player.** A side is *derived*, never configured: carrying the `Player` tag is
  team Player, everything else with a `Health` is team Enemy (bosses, adds, anchors, the training
  dummy). `Teams.Allied(attacker, target)` is checked in the four places damage is filtered —
  `AbilityHit.IsValidTarget`, `Projectile`, `ShotProjectile`, `ShotBeam` — so weapons, boss decks
  and enemy attacks all obey it without any prefab needing to be set up. It exists because the
  older mechanism, the attacker's "friendly tag", only worked for bosses (which do tag their adds
  `Enemy`): pipeline enemies are born `Untagged`, so their friendly tag was empty, the filter
  filtered nothing, and one enemy's bullet crossing another killed it. The friendly tag still
  applies — this is an extra rule on top, not a replacement.
- **`Health`** — the only place damage is applied. Exposes instance `Damaged`/`HealthChanged`/`Died`
  events plus a **static `AnyDamaged(Health, float)`** — the global feed `DamagePopups` subscribes to
  once instead of hooking every character. `TakeDamage(amount)` **returns false when the
  hit didn't land** (dead, invulnerable, zero damage); attackers must check it before playing hit
  effects. I-frames live here (`invulnerabilityDuration`); `InvulnerabilityChanged` fires on
  entering/leaving them. **Only the player has them (0.8s); enemies are set to 0** — i-frames are
  there to stop two enemies plus a projectile counting as three hits on the player, and on an enemy
  they instead swallow every multi-hit ability (a 5-pellet shotgun landed one pellet). Enemy
  hit-stun is knockback's job, not invulnerability's.
- **`Knockback`** — sits next to `Health` on anything that should be pushed. **The victim owns the
  force** (`horizontalForce`/`verticalForce`/`duration`/`resistance` per prefab); the attacker only
  passes a multiplier, so a heavy enemy or a boss is tuned in one place instead of in every attack.
  `TakeDamage(amount, sourcePosition, multiplier)` is the overload that damages *and* pushes — it
  only pushes if the hit actually landed, so an i-framed hit doesn't punt. Applying the push takes
  two paths: `IKnockbackReceiver` for the player (its controller integrates its own velocity, so a
  force would do nothing) and `Rigidbody2D.linearVelocity` for dynamic bodies (enemies).
  `EnemyController` yields control while `Knockback.IsActive`; `PlayerMovement` ignores input for
  the same window (otherwise holding the opposite direction cancels the hit on the same frame).
  Melee (`PlayerAttack`) pushes along the attacker's facing, not away from its position, so an
  enemy standing on top of the player still gets sent the way the swing points.
- **`HitFlash`** — tints the sprite on damage and blinks it during i-frames. Without it i-frames
  read as the game not registering hits. Add it wherever `Health` is.
- **Projectile collision rule** — `ShotProjectile` (and the old `Projectile`) stop for exactly two
  things: a collider with a `Health` in its parents (an enemy / the dummy — friendly-tag and
  already-hit filtered), or a collider on the **`Ground` layer (6)** — the terrain painted with
  the Tile Painter, matching `1 << 6` used by `ShotBeam` / `PlayerMovement`. Everything else —
  decoration, trigger zones (cauldron, tomb, shop), other props — is passed straight through.
- **`GroundSnap`** (`Assets/Scripts/Gameplay/`, on `Shop.prefab` and `WeaponUpgrade.prefab`) — on
  `Start` (and via context-menu) raycasts down to the `Ground` layer and drops the object so the
  base of its sprite bounds rests on the terrain. Needed because `RunManager` spawns the shop and
  the boss reward at marker/exit positions that aren't ground-aligned (and the shop's wheelbarrow
  no longer has the dynamic Rigidbody2D that used to let it fall into place). Reusable on any
  spawned prop.
- **Every `TilemapCollider2D` MUST be merged into a `CompositeCollider2D`** (+ a `Rigidbody2D` set
  to **Static**, which the composite requires). A bare `TilemapCollider2D` emits **one box per
  tile**, and two adjacent tiles share a vertical face that the physics engine treats as a real
  wall: anything walking along the top **snags on the seam and stops dead on ground that looks
  perfectly flat**. This cost a long debugging session — the symptom reads as broken AI ("the enemy
  follows me and then randomly stops"), so it gets chased in `EnemyBrain` where there is nothing to
  find. The proof is in the contact list, not the code:
  `Rigidbody2D.GetContacts` returned `[Tilemap] normal=(1.00, 0.00) point=(9.01, -3.00)` on flat
  terrain. After merging, MainHub went from hundreds of boxes to 5 outlines and the same enemy went
  from walking 2.9 units to 11.5 without stopping. It hits the player too (stutter while running).
  **Tools > RedMagic > Pipeline > `6 · Auditar colliders de tilemap` / `7 · Auditar y reparar`**
  (`TilemapColliderAudit`) scans every scene in `Assets/Scenes` and fixes this; `RunSingle(path,
  repair)` is the per-scene entry point for the CLI, because opening all nine scenes at once blows
  the 5s `unity command eval` timeout. It also forces a Static body — five world scenes had the
  ground tilemap on a **Dynamic** `Rigidbody2D`. Run it after painting terrain in a new scene.
- **`EnvironmentDecorColliders`** (`Assets/Scripts/Gameplay/`, on MainHub's `Enviroment`) — on
  `Awake` (and via its inspector context-menu) disables the `Collider2D` of every child that is
  pure decoration, so imported props (barrels, fences…) with baked-in colliders don't block
  movement or the physics broadphase. **Skips** any child with a `Health`, any `RedMagic.*` script
  on itself or a parent up to the container, any trigger collider, or anything in its `keep` list —
  so the same container can still hold the dummy, cauldron, tomb, chest, etc.
- **`Corpse`** — on death, tints the body, holds it ~0.6s so the kill reads, fades it over ~0.35s
  and destroys the GameObject. Colliders/physics are still switched off by whoever drives the
  character (`EnemyController.OnDied`); this only owns the look and the cleanup, so an enemy with
  different AI still tidies itself up. Corpses that stay forever litter the scene and hide things —
  they were covering the reward the boss drops.
- Both are already on `Player.prefab` and every `Enemy_*.prefab`. A damageable prefab built any way
  other than through `EnemyFactory` needs `Knockback` + `HitFlash` added by hand — `Health` works
  without them, just silently unpushed and unflashing. `EnemyFactory` stamps them, and
  `ContentAudit` (`Tools ▸ RedMagic ▸ Pipeline ▸ 4/5`) finds and fixes the ones that predate it.
- **`DamagePopups`** (`Assets/Scripts/Fx/`) — self-bootstrapping `DontDestroyOnLoad` singleton.
  Subscribes once to `Health.AnyDamaged` and spawns a floating number (code-built world-space
  `Canvas` + `Text` with the built-in `LegacyRuntime.ttf`, no font asset) over the victim: warm and
  size-scaled for damage dealt to enemies / the dummy, red and `-`-prefixed for damage the player
  takes (distinguished by the `Player` tag). `DamagePopups.Show(pos, amount, kind)` for manual use.
- **`TrainingDummy`** (`Assets/Scripts/Combat/`, on `Assets/Prefab/Eviroment/Target.prefab`) —
  `[RequireComponent(Health)]`. The prefab's `Health` has `maxHealth 1000000` and
  `invulnerabilityDuration 0`; the component calls `Health.ResetHealth()` on every hit, so it never
  dies and every pellet / beam tick / burst shot registers. Draws an IMGUI readout above itself
  (`FpsOverlay`-style) — last hit, DPS, hit count, average, max — for the current burst, which
  auto-resets after `idleResetSeconds` (2.5s) idle or on **R**.
- **`EnemyController.canFly`** — per-prefab bool. Off (default) is the ground enemy as before:
  gravity, ledge probing, and it ignores targets outside `verticalTolerance`. On, the enemy's
  `gravityScale` is zeroed in `Awake`, the ledge probe and the vertical-tolerance gate are skipped,
  detection uses true distance, and chasing flies straight at the target on both axes while holding
  its altitude. Set it on the floating enemies; leave it off for anything that should walk.

### Camera (`Assets/Scripts/Gameplay/CameraFollow.cs`)

The camera follows both axes (`followVertical` on everywhere), but **the two axes are not treated
the same**: horizontal is smoothed with `smoothTime` (0.18), vertical is **hard-locked to the
player with no smoothing at all** (`verticalSmoothTime` defaults to 0 = snap). That split is the
whole point — an earlier pass that smoothed the vertical axis and added a fall look-ahead was
rejected as unplayable, because any vertical lag means you don't see where you're landing until
after you've landed. If vertical follow ever feels wrong again, fix the framing (`offset.y`, zoom),
**not** by adding vertical smoothing back.

Zoom is a separate knob: `RunManager.cameraOrthographicSize` (8.5) is forced onto every
`CameraFollow` camera after each load, so the hub and the sections match.
`ShakeAll(amplitude, duration)` is the project-wide camera shake (see Bosses).

### Weapons & items — the build system (`Assets/Scripts/Items/`)

The current attack system (it **replaced** the ability system; see Legacy below). A weapon is a
`WeaponDefinition` asset in `Assets/Resources/Items/Weapons/` describing a **base shot**; items
transform that shot. Content by convention: a new weapon or item is a new asset, no code, no
Inspector wiring.

- **Shot pipeline** (`ShotResolver`, `WeaponShot`): 1. the weapon emits its base shot
  (`WeaponShot.FromWeapon`) → 2. the equipped **Trajectory** modifier (how it moves) → 3. the
  **Shape** modifier (how many projectiles / splits) → 4. the **Element** modifier (paints damage
  type), which every projectile produced by step 3 inherits automatically, split children included.
  `Resolve` returns the resolved plan with no physics (testable); `Fire` also launches it. A weapon
  with no Element item still paints with its `InnateElement`.
- **Slots** (`WeaponInventory`) — 3 dedicated (Element / Trajectory / Shape, exactly one item each,
  last equipped wins) + 6 free slots with no type restriction, plus the equipped weapon. It only
  tracks what sits in each slot and raises `ItemEquipped`/`ItemUnequipped`; nothing else.
- **Items** — `ItemDefinition` (base: name/description/icon/accent + synergy tags) → `WeaponModifier`
  (`ElementModifier`, `TrajectoryModifier`, `ShapeModifier`, each with a fixed `PipelineOrder`) and
  `FreePoolItemDefinition`. Free-pool items **do not transform the shot at all** — their whole
  contribution is a pair of tags.
- **Synergies** (`SynergyTracker` + `BuildTag`/`BuildTags` + `Assets/Resources/SynergyConfig.asset`)
  — the single source of truth for tag points, fed only by `WeaponInventory` events. Every equipped
  copy counts (+1 per tag, the same asset in two free slots counts twice), thresholds are 2/4/6 and
  points cap at 6. It counts and notifies; it implements no threshold effect.
- **`WeaponUser`** (on `Player.prefab`) — the only holder of firing state (cooldown, hold-to-charge
  via `BaseShot.chargeTime`). Silences `PlayerAttack` while a weapon is equipped, and yields to a
  legacy `AbilityUser` if an ability is somehow equipped. **The shot leaves on the gesture, not on
  the press**: `releaseDelay` (0.28s, the 4th-5th drawing of the 0.42s attack clip) holds the shot
  while the animation winds up, and the shot context — muzzle and facing — is built at release, so
  turning mid-swing fires where you now look. The cooldown still counts from the press, so the
  delay costs no rate of fire. It is a timer and not an `AnimationEvent` like the enemies use
  because the player’s `Animator` lives on the `Sprite` child and events only reach components on
  their own GameObject. `PlayerAttack.windup` and `RangedAttack.windup` (0.25s) are the same idea
  for the other two attack paths.
- **`WeaponLoadout`** — self-bootstrapping `DontDestroyOnLoad` singleton owning the run's
  `WeaponInventory`.
- **Weapon levels (1–3)** — `WeaponLevelManager` (self-bootstrapping singleton, keyed by
  `WeaponDefinition` asset reference) charges Skulls (1 for →2, 3 for →3, tunable in
  `upgradeCosts`) and **wipes on `RunEnded`**, same shape as the old ability-level system it
  replaces. **Placeholder effect only**: until real per-level stats are designed, levelling up just
  tints the shot — black at level 2, gold at level 3 (`WeaponLevelManager.TryGetLevelTint`, applied
  by `ShotResolver.Resolve` after the Element step) — purely to prove the plumbing works.
  **`WeaponForgeAltar`** (on `Assets/Prefab/Eviroment/WeaponUpgrade.prefab`, `RunManager`'s
  `bossRewardPrefab`) + **`WeaponForgeMenuController`** (`Assets/Ui/`, own
  `WeaponForgeMenuPanelSettings`) are the forge: same one-card-one-button shape as before, reading
  `WeaponUser.Weapon` instead of the legacy `AbilityUser.Equipped`.
- **Runtime** (`Items/Shot/`) — `ShotProjectile` and `ShotBeam`, both pooled, reusing `AbilityHit`
  for target filtering/damage and `AbilityFx` for sprite/tint/sorting. Built in code unless the
  weapon's `BaseShot.projectilePrefab` / `beamPrefab` is set, in which case the visual comes from
  that prefab via `PrefabPool` (see "Placeholder visuals → real art").
- **`WeaponLibrary`** / **`ItemLibrary`** — folder scans of `Resources/Items/(Weapons|Modifiers)` and
  the item assets, same pattern as the old `AbilityLibrary`.
- **`ItemMenuController`** (`Assets/Ui/`, key **I**, `ItemMenuPanelSettings`) — the build screen;
  same self-bootstrapping code-built pattern as the other menus.
- **`AbilityChest`** (name kept; on `Assets/Prefab/Eviroment/GoldChest.prefab`) — proximity
  interactable that grants a **`WeaponDefinition`** through `WeaponLoadout.Instance.Inventory`.
  Empty `forcedWeapon` = random from `WeaponLibrary`; `AbilityChestEditor` draws it as a dropdown.
- **`ShopMenuController`** buys items and equips them straight into the inventory (`TryEquip`).
- **`ShotPipelineHarness`** — drop it in a scene and hit Play to fire the four configurations
  (bare / trajectory only / shape only / all three layers) at a dummy it spawns.
- Current content: 7 weapons (the ones rescued from the old abilities plus a plain
  `Weapon_ProyectilRecto`), 4 modifiers, 9 free-pool items (5 of them the ice test items below).
- **Item behaviour lives on the item** — read `Assets/_Pipeline/ITEMS_PIPELINE.md`.
  `ItemDefinition.effects` is a `[SerializeReference, SubclassPicker]` list of `ItemEffect`
  subclasses (`Assets/Scripts/Items/Effects/`): pick the type from a dropdown in the item's
  Inspector and tune its values there. `ItemEffectRunner` (inside `WeaponLoadout`) applies them
  only while equipped (`OnEquip` / `OnUnequip` / `Tick`); per-equip state goes in the
  `ItemEffectContext`, never in effect fields (the instance is shared asset data). Player stat
  changes go through `Gameplay.PlayerStats` multipliers, read by `PlayerMovement` (move speed,
  dash distance = duration, jump height = √ on velocity). `Health.Drain` is damage that isn't a
  hit (no i-frames, no hurt anim, still a popup). Icons: `ItemIconsPack` (keys `Assets/Icon/*`
  via `UiArtKitProcessor`) and `Tools ▸ RedMagic ▸ Items ▸ Catálogo de items (iconos)`, which
  edits the icon on each item asset. Test items (Botas/Capa/Yelmo/Bastón/Anillo) have absurd
  effects on purpose — speed/dash/jump ×3, −1 HP/s, +1 diamond/s.
- Projectiles never collide with other projectiles — pellets from one blast spawn on top of each
  other and would annihilate on frame one.

### Legacy (`Assets/Scripts/Legacy/`, `Assets/Resources/Legacy/`) — reference only, do not build on it

The **ability system**: 22 `AbilityDefinition` assets (now in `Assets/Resources/Legacy/Abilities/`
— `AbilityLibrary.ResourceFolder` points there, so the dev-only K menu still lists them as a
working reference instead of coming up empty), the nine archetype classes, `AbilityUser`,
`AbilityLibrary`, `AbilityMenuController`, the `AbilityStarterPack` editor tool, and the runtime
pieces only they used: `AbilityTurret`, `DamageZone`, `OrbitSpinner`. Plus the **old weapon level
1–3** system that hung off it, `AbilityLevelManager` + `WeaponUpgradeAltar` +
`WeaponUpgradeMenuController` — replaced by the `WeaponLevelManager` / `WeaponForgeAltar` /
`WeaponForgeMenuController` trio documented above. Superseded by the weapons/items build system;
five of the abilities were rescued as weapons (`Assets/Resources/Items/Weapons/`).

It still compiles and the `.meta` files moved with the sources, so GUIDs are intact. `Player.prefab`
still carries the legacy `AbilityUser` component (inert unless something equips it) and
`WeaponUpgrade.prefab` (`RunManager.bossRewardPrefab`) was **repointed** from `WeaponUpgradeAltar`
to the new `WeaponForgeAltar` — that swap is why the forge used to say "no llevas arma equipada"
even with an item-system weapon out. Read the rest as reference and port what is needed into
`Assets/Scripts/Items/`; do not extend it.

**Deliberately *not* legacy**, because the live systems call into it: `AbilityContext` /
`AbilityHit` (the one place target filtering and damage application live — weapons and bosses both
use it), `AbilityFx` (code-built sprites/flashes — the fallback when no placeholder prefab is
assigned), `ProjectileSpec` / `ProjectileFactory` and `Projectile` (the pooled projectile the
bosses fire; `ProjectileFactory` now restyles a prefab instance that carries `FxPlaceholderStyle`).

### Bosses (`Assets/Scripts/Bosses/`)

Same content-by-convention shape as the weapons/items system, one level up: a boss is a
**`BossDefinition` asset** plus a prefab, and each of its attacks is its own **`BossAttack`
ScriptableObject** in `Assets/Resources/Bosses/`. Writing C# is only needed for a new *archetype*
(a new shape of attack), never for a new boss, a new attack asset or a phase-2 variant.
Each boss has a generator under **Tools > RedMagic > Boss**, one pack per boss:
`BossStarterPack` (Árbol Ancestral), `ScarecrowBossPack` (Espantapájaros), `StoneGuardianPack`
(Guardiana de Piedra) — these three also have a *Colocar … en WorldN_Boss* item — plus
`CursedWellPack` (Pozo Maldito), `BeetleQueenPack` (Reina Escarabajo) and `UnholySealPack` (Sello
Profano), which only build the prefab because there is no fourth world yet. Each builds its attack
assets + definition + prefab.

**Gotcha when writing a pack**: a `BossPhase` created through `SerializedProperty.arraySize` is
**zero-initialised** — Unity does not run the C# field initialisers for array elements, so any
field the generator does not write is `0`, not the default you see in the class. That is why
`BossAuthoring.WritePhase` writes every field including the ones that "already default to 1";
leaving `damageTakenMultiplier` unwritten once shipped three bosses that could not be damaged at
all. `BossDefinition.OnValidate` now repairs a zero there and logs a warning. All of it is idempotent — nothing
overwrites an existing asset, so a generator can be re-run after hand-tuning numbers and only the
missing pieces appear. The shared plumbing (creating attack assets, writing private serialized
fields, registering tags, planting the prefab on the scene's ground) lives once in
**`BossAuthoring`**; a boss pack should be almost pure data.

- **`BossController`** (on the boss prefab, replaces `EnemyController` — a boss never patrols) —
  waits for the player to enter `activationRadius`, plays an intro (invulnerable + camera shake +
  health bar + optional `musicId`), then loops **telegraph → attack → recovery → pause**. That
  cycle is the whole readability of the fight: nothing damages during the telegraph, and the
  recovery is the player's DPS window, so the nastier the attack the longer its recovery. Measures
  the arena's ground with a raycast at `Start` and **snaps itself onto it** (its prefab origin is
  the sprite's base), so placing a boss is dropping it roughly in the scene.
- **Phases** — `BossPhase` entries inside the definition, entered when normalized health drops
  below `startsAtHealth` (never backwards, so lifesteal can't rewind a phase). Entering one cuts
  the attack coroutine, goes invulnerable for `transitionSeconds` (otherwise a well-timed burst
  skips phase 2 entirely), and swaps the attack deck. `speedScale` shortens every timing in the
  phase and `frenzyBelowHealth` shortens them again at low health — so phase 2 is the same assets,
  faster, not a duplicated set.
- **Armour and punish windows** — `BossPhase.damageTakenMultiplier` sets how much damage the boss
  takes during that phase, and any attack can declare `vulnerableSeconds` / `vulnerableMultiplier`
  to leave the boss **exposed during its recovery** (`BossController.EnterVulnerable`). Together
  they change what the fight asks: an armoured boss can't be beaten by dodging patiently, only by
  getting close enough to punish the attack that just tried to kill you. Both ride on
  **`Health.DamageMultiplier`** — damage enters through one place, so no weapon, ability or boss
  attack needs to know armour exists, and the floating number the player sees is already the real
  one. The window is signposted three ways: the boss's aura turns gold (`vulnerableAura`), the
  boss health bar's fill turns gold and its subtitle reads "¡EXPUESTO!".
- **`BossContext`** — the per-cast bundle (boss, player, ground Y, arena size, phase pacing) that
  carries an **`AbilityContext`** inside it, so boss attacks reuse `AbilityHit` for target
  filtering/damage and `ProjectileFactory` for **pooled** projectiles instead of a parallel system.
- **Archetypes** — each one exists because it asks the player a *different question*; that is the
  bar a new archetype has to clear, otherwise it belongs as another asset of an existing one.
  - `ShockwaveAttack` (`BossShockwave`, pooled) — **"at what height am I?"**. A screen-wide wave
    inside a **height band**: band `0→1.9` must be jumped, band `1.9→9` must be ducked by staying
    grounded, and `alternateBands` makes one attack ask for jump→land→jump. Having both bands is
    what forces reading the telegraph instead of jumping on reflex.
  - `SweepBeamAttack` (`BossSweepBeam`, pooled) — **"at what distance am I?"**. A long arm pivoting
    at the boss's shoulder that rotates from `fromAngle` to `toAngle`: because it pivots, its height
    at your position depends on how far away you stand — point-blank it drops instantly, mid-arena
    it arrives late but fast. `bothSides` removes the "just stand behind it" answer, `returnSweep`
    makes pass 2 come back through where pass 1 just cleared. Hits **once per pass**, like the wave.
  - `SafeZoneAttack` — **"where on the floor am I?"**. Marks N refuges, then damages the whole arena
    *except* inside them, so the player has to leave the boss and run. `guaranteeReachable` always
    puts one refuge near the player (without it, a bad roll makes the hit unavoidable in a wide
    arena); refuges are drawn in their own `safeColor`, never the phase accent, because what saves
    you cannot look like what hurts you. `pulses` + `moveSpotsEachPulse` turn it into a relay.
  - `BulletHellAttack` — `Radial` (ring; with `spinPerVolley` a spiral whose gap you must track),
    `Fan` (aimed cone) and `Rain` (ground markers first, then drops). Its `projectileSprite`
    overrides the boss's FX sprite per attack, so one boss can throw pumpkins and another burrs
    while its warnings stay plain readable rectangles. Projectiles do **not** rotate to face their
    direction, so only round-ish sprites work there.
  - `GroundSlamAttack` — **"am I still standing where I was?"**. Marks the spot the player is on,
    then crushes it after `windup`; the mark is fixed when it appears and never chases, so it is
    always dodgeable but always costs you your comfortable spot. It optionally leaves rubble in the
    crater, and it is the natural carrier of a punish window.
  - `HazardFieldAttack` (`BossHazard`, pooled) — **"where will I be able to stand later?"**. Seeds
    the floor with patches that *stay* and tick for damage. Deliberately low damage and long life:
    its job is to shrink the arena, which quietly tightens every other pattern in the deck without
    changing any of them. `avoidPlayerRadius` keeps a patch from spawning under your feet.
  - `GuardStanceAttack` — **"is it safe to attack right now?"**. The boss covers up: it takes
    almost no damage and **reflects a flat amount per hit** (`BossController.EnterGuard`), so
    mashing through it costs you half a health bar. Its armour is never 0 — the hit has to land to
    be reflected, and seeing a laughable number pop off the boss is half the lesson. Holding fire
    is rewarded: it can open a punish window as it drops the guard.
  - `PlatformFloodAttack` (`BossPlatform`, pooled) — **"can I get off the ground?"**. Grows ledges
    that spawn on the **`Ground` layer**, so `PlayerMovement` stands on them with no special
    casing (and shots collide with them, which is a fair trade), then floods the whole arena floor
    for a few seconds. Platforms outlive the flood and blink before vanishing. Both halves live in
    one attack on purpose: the deck is shuffled, so "ledges first, flood second" is only
    guaranteed if they are the same card.
  - `AnchorRitualAttack` (`BossAnchor`, pooled) — **"what do I shoot first?"**. The boss shuts
    down to ~5% damage taken and plants destructible anchors on a timer; break them all and it is
    left exposed, run out of time and it discharges across the whole arena. Anchors are plain
    `Health` objects tagged like the boss, so every weapon in the game breaks them and none of the
    boss's own attacks do.
  - `OrbRingAttack` (`BossOrb`, pooled) — **"where do I stand before it's loaded?"**. The boss
    doesn't shoot, it *builds* the shot: orbs appear one at a time in a crown around it, tiny, and
    grow while the ring turns. Nothing damages during that — the pattern is drawn in the air
    before it exists, so it says exactly how many projectiles are coming and how wide the gaps
    are. At full size they all launch outward at once. Unlike a plain radial volley, which you read
    while it's already reaching you, the whole attack is the wait: stand in a gap, or spend it
    hitting the boss (hence a generous `vulnerableSeconds`). Charge orbs are pure visuals — no
    collider, no damage — and each is swapped for a real `ProjectileFactory` projectile at its own
    position on release, so nothing jumps. They also self-expire, so a coroutine cut by a phase
    change can't leave a crown floating.
  - `SummonAddsAttack` — adds, capped by `maxAlive`, killed when the boss dies.
  - `QuakeSlamAttack` — **"are my feet on the floor when it lands?"**. No radius: at the gesture's
    release frame it shakes the camera (base `shakeAmplitude`/`shakeDuration`), plays its
    `impactFxPrefab` and damages the player only if `PlayerMovement.IsGrounded &&
    !IsOnPlatform` and the feet are within `floorTolerance` of the arena floor — jumping or
    standing on a Platform-layer ledge is the answer. Its timing knob is `impactDelay` (overrides
    `Telegraph`; `QuakeSlamAttackEditor` hides the base `telegraph`).
  - `PlatformDenialAttack` (`BossBolt` + `BossHazard`, pooled) — **"where can I stand for the
    rest of the fight?"**. Fires a `BossBolt` (straight line, no collider) at each Transform in
    the scene boss's **`BossArenaTargets`** (scene objects can't live on an SO), and on arrival
    leaves a `BossHazard` with `BossHazard.UntilBossDies` — permanent, ticking `damage` every
    `fireTickInterval`. Meant as a **`BossPhase.openingAttack`**: an attack the phase fires
    **once** after its transition, outside the shuffled deck. `BossController.OnDied` calls
    `BossHazard.ClearFrom(this)`, so every hazard a boss left (fire, rubble) dies with it.
- **Per-attack availability** — `BossAttack.minPhase` (1-based: below it the attack is never drawn,
  even if it sits in that phase's deck — the empty-deck fallback respects it too) and
  `cooldownSeconds` (real-time cooldown, on top of `cooldownInAttacks`), both checked in
  `BossController.IsReady`/`PickAttack`. Use them to move an attack between phases without
  duplicating assets or editing decks. `BulletHellAttack` Rain also has `rainCenterOffset`,
  `rainRandomX` and `rainDropStagger` (drops one by one; each ground marker lasts until its drop),
  and there is a **`Storm`** pattern: continuous for `stormSeconds` at `stormPerSecond` (neither
  scaled by phase pace), each drop at a random **landing** X with its own angle
  (`stormWind` ± `stormAngleRange`) and its origin shifted against the diagonal, so slanted drops
  still cover the whole span; `stormMarkLanding` marks each landing spot until it arrives.
- **Gestures — animated bosses (`BossAnimator`)** — a boss whose body is a sprite sheet gets an
  `Animator` **on the root** plus `BossAnimator`, and each `BossAttack` names its body animation
  in **`gesture`** (`Charge`, `Slam`, `Summon`…). `BossController.Telegraph` plays it and the
  attack's `Run` starts on the clip's **`OnAttackRelease` event** (the same event the pipeline
  plants for enemies — `AnimClipBuilder` now plants it on *any* row with `releaseFrame >= 0`, not
  just `Attack`). The clip's speed is set so that frame lands exactly at `telegraph / pace`, so
  `telegraph` stays the only timing knob and phase `speedScale` speeds up gestures for free;
  `gestureSpeedRange` clamps it (then the drawing wins and the attack waits). Gestures are loose
  controller states; `BossAnimator` returns them to `Idle`. `Hurt` = stagger on phase change
  (and `flinchOnHit`, idle only, with cooldown); `Death` via the `Dead` bool. No gesture / no
  `BossAnimator` = the old timed telegraph, so the six sprite bosses are unchanged.
  `BossPhase.transitionFx` (optional one-shot prefab) bursts at the boss's feet on entering a
  phase. Archetype knobs added for it: `OrbRingAttack.launchMode = AtPlayer` (+`launchStagger`,
  aimed at `BossContext.PlayerCenter`) and `GroundSlamAttack.impactFxPrefab` / `fitFxToRadius` /
  `fxLeadSeconds` (real-art crater FX via `VfxOneShot.SpawnFitWidth`; with `aimAtPlayer = false`
  the crater is marked for the whole telegraph).
- **Every boss is built on a different question on purpose** — that is the design rule for the next
  one, not just a description of these. Bosses 4-6 have no scene of their own yet: their generators
  only build the prefab in `Assets/Prefab/Enemies/`, to be dropped into whatever arena is being
  tested.
  - **Árbol Ancestral** (World 1) — *height*: shockwave bands plus spirals, with ground-bound
    sprouts as adds. Its bullets are the green orb (`Fx_ArbolAncestral_Bullet` wears `Orbe_Move`
    with a `SpriteFlipbook` and bursts into `Fx_Orbe_Impacto`), and it now also grows crowns of
    them: `BossAttack_CoronaDeOrbes` in phase 1, `BossAttack_CoronaMayor` (14 orbs, two crowns,
    twice the spin) in phase 2.
  - **Espantapájaros Marchito** (World 2) — *distance and place*: the pivoting scythe and the
    refuges, with **flying** crows (`Enemy_Cuervo.prefab`, an ordinary `EnemyController` with
    `canFly`) that cannot be out-run the way sprouts can. Cainos scarecrow at ×3.2, pumpkins and
    spike balls as projectiles.
  - **Guardiana de Piedra** (World 3) — *when do I attack?*: armoured to 30% damage taken, so the
    only real DPS comes from the windows her own `Puño Sepulcral` opens, and the arena keeps
    shrinking under the rubble of `Sepultura` / `Derrumbe`. Phase 2 cracks the armour to 60% but
    shortens the windows. Her health (2000) is deliberately lower than the other two because the
    armour already stretches the fight; raising it would make it long rather than hard. Cainos
    statue at ×3.7, rocks as projectiles.
  - **Pozo Maldito** (`CursedWellPack`) — *when NOT to attack*: it seals itself and reflects a flat
    hit back at whoever keeps swinging, then pays out a punish window for holding fire. Around that
    it splits your attention with crawlers out of the shaft and puddles that stay. Cainos well at
    ×3.2; its geyser is an ordinary bullet-hell fan with `arcGravity`, which reads as black water
    thrown up and falling back.
  - **Reina Escarabajo** (`BeetleQueenPack`) — *where is she now?*: the only **moving** boss. A
    `BossBurrowLocomotion` component (next to `BossController`, `[RequireComponent]`) patrols her
    across the arena on the floor/ceiling, then every few seconds she digs into the nearest surface,
    tunnels invisibly (dropping a low-band `BossAttack_Temblor` shockwave every ~1s that forces a
    jump — this asset is in **no phase deck**, only the locomotion fires it), and re-emerges from
    the opposite surface 1–5m to one side after a dust telegraph. Her normal deck (`Marea Ácida`
    flood + ledges, `Salivazo` fan, `Camada` brood, `Embestida` shockwave) only fires while she is
    surfaced — locomotion calls `BossController.SuspendAttacks`/`ResumeAttacks` around each burrow
    and `SetBodyVisible(false)` + `Health.Invulnerable` while underground. Her origin is the sprite
    **centre** (not the base like every other boss) so the rotate/flip/head-dive all pivot clean;
    Brackeys' 3-frame beetle at ×1.5, pre-rotated -90° (its art faces up), animated by
    `SpriteFlipbook`. `BeetleQueenPack` is **not** idempotent-only: it has a
    *RECREAR (borra y regenera)* menu item that deletes its owned assets and rebuilds them.
  - **Sello Profano** (`UnholySealPack`) — *what deserves your damage?*: the anchor ritual is its
    spine, and the rest of the deck is deliberately a greatest-hits of the earlier bosses (sweep,
    refuges, spiral, rubble) so a player who got this far recognises everything and only has to do
    it faster, with a countdown running. Brackeys' pentagram at ×2.5, hovering, with a circle
    collider so you can pass underneath.
  - **Ent Cristalino** (`TreeBossPack`, `Boss_TreeBoss`) — *where does the next one land?*: the
    first boss built from its **own sprite sheets** and the reference for animated bosses. Rooted,
    never moves; every attack is a body gesture (`TreeBoss.png` rows Idle / Charge / Slam /
    Summon / Hurt / Death) and its FX come from `BossAttack.png`, cut by two recipes on the same
    sheet (`TreeBossOrb` centred, `TreeBossRoot` bottom-anchored, split by `cropTop/cropBottom`).
    Deck: `Orbes de Savia` (OrbRing, `AtPlayer`, gesture Charge), `Estallido de Raíces`
    (**QuakeSlam**: earthquake shake + root wave, hurts only a grounded player, punish window,
    gesture Slam), `Espinas de Raíz` (GroundSlam aimed at the player, root-spike FX with
    `fxLeadSeconds`, gesture Summon; **`minPhase 2`** like `Bosque de Espinas`, so root spikes never
    fire in phase 1). Phase 2 at 50%: 2.5s invulnerable stagger + shake + root
    wave, then **`Fuego Verde`** once (`openingAttack`, PlatformDenial: 6 bolts to the
    `FireTargets` over the two side platforms, permanent green fire), then `speedScale 1.35`,
    shorter pauses and the bigger variants (`Tormenta de Savia`, `Bosque de Espinas`), plus
    **`Tormenta de Hojas`** (BulletHell Storm, gesture Charge, `minPhase 2`, `cooldownSeconds 8`: 5s
    of leaves at 7/s, random X, wind 8° ± 18°; the Rain version `Hojas Mágicas` — two waves of 10 —
    is kept as a reusable asset but is no longer in this deck; leaves pass through Platform-layer
    ledges and die on the Ground floor; `Fx_TreeBoss_Leaf` / `_LeafImpact` are placeholders — real
    art authored pointing +X since `Projectile` faces its travel direction). The fire's
    bolt/hazard are **placeholders** (`Fx_TreeBoss_FireBolt` / `_FireHazard`, tinted squares with
    `FxPlaceholderStyle`): real art = edit those prefabs (sprite + `SpriteFlipbook`, style
    `tint`/`resize` off), no code. *TreeBoss · Colocar en World1_Boss* places it; *TreeBoss ·
    Colocar blancos de fuego en World1_Boss* drops 3 targets over each outermost Platform-layer
    collider (only if the boss has none). Its sheets are painted on a flat
    green, which is why `SpriteSheetRecipe.softEdge` (glows fade instead of being scissor-cut) and
    `fillHoles` (bark shadows share the background's hue and got punched through) exist — see
    `SPRITE_PIPELINE.md` §13-14.
- **`BossHealthBar`** — screen-space uGUI built in code (no prefab/UXML/PanelSettings), sorting
  order 18 so menus still cover it. Shows the definition's name/title, an amber "ghost" trail
  behind the fill and a tick per phase threshold. A new boss needs no UI work.
- **Teams** — the boss and its adds share the **`Enemy` tag**, which is what stops the boss's own
  bullets from killing its summons (`AbilityContext.FriendlyTag` / `Projectile`'s owner tag). Tag
  any new boss-side spawn the same way.
- **`CameraFollow.ShakeAll(amplitude, duration)`** — camera shake lives inside `CameraFollow`
  (which already owns the camera position, so there's no LateUpdate ordering fight) and is
  reusable by anything, not just bosses.

### Economy (`Assets/Scripts/Economy/`)

- **`CurrencyManager`** — `DontDestroyOnLoad` singleton **hand-placed in MainMenu and MainHub**
  (like `AudioManager` / `RunManager`); a `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` fallback
  self-creates one only if a scene didn't provide it. Its four amount fields (`gold`, `diamond`,
  `soulFragment`, `skull`) mirror the live state and are **editable in the Inspector, including
  during Play** — type a value and `OnValidate` applies it immediately (persists the meta ones,
  fires `Changed`); any code change mirrors back so the Inspector always shows the truth. This is
  the test knob for currency. Context-menu "Borrar moneda de meta guardada" wipes the saved
  `PlayerPrefs`. `CurrencyManager.IsRunCurrency` is the single source of the run-vs-meta split:
  **Gold** and **Diamond** are run currency (spent in mid-run shops, zeroed on `RunManager.RunEnded`
  whether you died or won, not persisted); **SoulFragment** and **Skull** are meta currency (spent
  in the hub, saved to `PlayerPrefs`, survive death). Spending is `TrySpend` (used by `UpgradeManager`).
- **`CurrencyConfig`** — one ScriptableObject at `Assets/Resources/CurrencyConfig.asset`, loaded by
  `Resources.Load`. Holds per-currency HUD visuals (icon/label/tint) and the drop table: a
  min–max `DropRange` per currency for each `EnemyTier` (Basic / Elite / Boss). This asset **is**
  the "currency manager" for tuning drop amounts — edit it in the Inspector.
- **`CurrencyDropper`** — `[RequireComponent(Health)]`; on `Health.Died` calls
  `CurrencyManager.GrantDrops(tier)`, which rolls each currency's range for that tier. On the enemy
  prefabs (`Enemy_*`, tier set per prefab) and on boss enemy instances (tier Boss). A Basic tier
  with diamond/skull ranges left at 0 simply drops none of those.
- **`CurrencyHud`** (`Assets/Ui/CurrencyHud.cs`) — self-bootstrapping persistent `UIDocument`, built
  in code, always visible (a row of icon+amount, top-right). Uses its own
  `Assets/Resources/CurrencyHudPanelSettings.asset` (sorting order 15, under the menus at 20).
  Note: headless `capture_game_view` / `screenshot` do **not** render UI Toolkit runtime panels, so
  the HUD (and touch controls) are invisible in those captures even when working — verify by
  querying the visual tree instead.

**Permanent upgrades** (the hub cauldron):

- **`UpgradeTree`** — ScriptableObject at `Assets/Resources/UpgradeTree.asset`. A `rows × columns`
  grid (currently 3×5) of `Node`s in row-major order. Each node has `maxLevel`, a cost that scales
  with level (`baseCost + costPerLevel * currentLevel`), and a placeholder `bonusPerLevel` /
  `statId`. Fill in real upgrades by editing the asset.
- **`UpgradeManager`** — self-bootstrapping `DontDestroyOnLoad` singleton. Levels are saved per node
  id in `PlayerPrefs` (meta-progression, survives death). "Skull: The Hero Slayer" unlock rule:
  within a row, column N is buyable only once column N-1 has level ≥ 1; rows are independent.
  `TryBuy(row, col)` pays `UpgradeManager.Cost` (SoulFragment) via `CurrencyManager.TrySpend`.
  `GetBonus(statId)` sums purchased bonuses — the hook for a future player-stat system; nothing
  reads it yet.
- **`UpgradeMenuController`** (`Assets/Ui/`) — self-bootstrapping persistent code-built `UIDocument`
  (`Assets/Resources/UpgradeMenuPanelSettings.asset`, sorting order 30). Hidden until
  `Open()`; pauses the game via `GameStateManager` while open. Close with Esc / E / gamepad B.
- **`CauldronInteractable`** — trigger-collider proximity interactable (same shape as
  `TombInteractable`) added to a "Cauldron Interact Zone" child near the cauldron in MainHub;
  interact key calls `UpgradeMenuController.Instance.Open()`.

**Mid-run shop:**

- **`ItemLibrary`** (`Assets/Scripts/Items/`) — folder scan of `Resources/Items` bucketed by slot
  (`Elements` / `Trajectories` / `Shapes` / `FreePool`), same pattern as `AbilityLibrary` /
  `WeaponLibrary`. The shop and any future consumer read real `ItemDefinition` assets from here.
- **`ShopConfig`** — ScriptableObject at `Assets/Resources/ShopConfig.asset`. **No item list** — it
  only tunes `freePoolCount` (default 3) and a min–max `CostRange` per type. `RollStock(seed)`
  composes one shop: 1 random Element + 1 Trajectory + 1 Shape modifier + `freePoolCount` distinct
  free-pool items (6 total), seeded partial Fisher-Yates, returns `ShopStockEntry { Item, Cost }`.
- **`ShopInteractable`** — on the `Shop.prefab` ("Shop Interact Zone" child). Rolls its stock once
  (seeded from `RunSeed` + its x position), refuses to open while `SectionClearTracker` reports
  enemies alive, and `MarkSold` removes an entry permanently — each item is buy-once.
- **`ShopMenuController`** (`Assets/Ui/`) — same self-bootstrapping code-built pattern as the
  upgrade menu, own `Assets/Resources/ShopMenuPanelSettings.asset`. Spends **Gold** and **equips on
  purchase** into `WeaponLoadout.Instance.Inventory` via `TryEquip`: a modifier replaces its
  dedicated slot silently; a free-pool item takes the first empty slot, or — if all 6 are full —
  opens a second overlay to pick which slot to discard, and only charges once the player picks.

**`MenuStyle`** (`Assets/Ui/MenuStyle.cs`) — shared palette, sizes and element factories for both
code-built menus. **Change the size constants here to rescale that UI**; both screens follow. Sizes
are in the panels' 1600×900 reference resolution, so they render ~1.2× larger at 1080p.

### Input

Three input sources are meant to coexist, not be exclusive: the Input System asset
(`RedMagicControls.inputactions`, keyboard/gamepad), and `Assets/Scripts/Input/TouchInput.cs`, a
static class that on-screen touch buttons (`TouchControlsController`) push queued presses into.
Gameplay scripts (`PlayerMovement`, `PlayerAttack`, `RangedAttack`) check both every frame. All
input is gated behind `GameStateManager.CanPlayerAct` — check that first in any new input-driven
script rather than re-deriving a pause check.

### UI

All menus (`MainMenuController`, `PauseMenuController`, `OptionsMenuController`,
`TouchControlsController`) are UI Toolkit (`UIDocument` + UXML/USS), not uGUI, and implement
`IMenuScreen` (`SetVisible(bool)`) so one menu can hide itself and hand focus to another
(Pause → Options → back) without losing UI Toolkit layout state — visibility toggles via
`style.visibility`/`display`, not by disabling the GameObject.

**UI art kits** — read `Assets/_Pipeline/UI_ART_PIPELINE.md` before dressing a screen with art.
A kit of flat-background images becomes clean sprites through a `UiArtKitRecipe`
(`<Kit>.uikit.asset`) + `UiArtKitProcessor` (background keying, soft halos, state grids,
**Quad** frames: 4 quadrant 9-slices that stretch only a 2px strip, so centre ornaments never
smear, plus the frame's interior panel as one `_Fill` sprite drawn on top — stretching the
interior from the strips made it look split into 4 rectangles), and a per-kit pack wires typed refs into a `<Screen>Skin` asset in `Resources`.
Runtime side is `Assets/Ui/UiFrame.cs` (`UiFrame.Dress(element)`, `UiStateSprites`). First
user: the items screen (`ItemsUiPack` → `Resources/ItemMenuSkin.asset`, read by
`ItemMenuController`; any missing piece falls back to the plain `MenuStyle` look). Runtime UI
Toolkit panels don't appear in `screenshot`/`capture_game_view` — to see one, render a cloned
`PanelSettings` into a `RenderTexture`.

### Vendored third-party content — do not search or modify by default

`Assets/Brackeys/`, `Assets/Cainos/` (including its bundled `Third Party/Lucid Editor` inspector
attribute library), and `Assets/Dragon Warrior Files/` are imported asset packages (art, demo
scenes, a few utility scripts like `Chest.cs`/`Elevator.cs`/`SecondOrderDynamics.cs`). They are not
part of the game's own architecture. Scope searches to `Assets/Scripts`, `Assets/Ui`,
`Assets/Audio`, and `Assets/Scenes` unless a task specifically concerns one of these packages —
scanning all of `Assets/` pulls in hundreds of irrelevant demo/example files from them.
