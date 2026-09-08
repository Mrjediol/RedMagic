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
- **Scene inspection**: `unity command list_open_scenes`, `get_scene_hierarchy`, `find_gameobjects`,
  `get_component_properties` read the actual loaded scene state — prefer these over parsing scene
  YAML by hand when an editor is connected, since they reflect what Unity actually deserialized
  (see the SceneReference gotcha below).
- **Builds**: `unity build --target <platform>` / `unity command build` trigger a Player build via
  the CLI; there's no CI config in this repo.
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
  `Despawn(go)` (via the `PooledInstance` marker it stamps on). Used by `VfxOneShot` and the
  prefab `Projectile` path.
- **`PoolRunner`** — persistent `DontDestroyOnLoad` root that every pooled instance is parented
  under (so a scene unload never destroys the reserve) and that force-releases everything still
  active on each `sceneLoaded`, so nothing bleeds between sections.
- Domain Reload is off, so every `static` pool field is nulled in a
  `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, and `PoolRunner` clears its releaser list
  once at runtime start.
- Low-churn spawns (one `Corpse` per death, boss reward, shop, `AbilityFx.SpawnSprite` for a
  seconds-long zone/turret/orb) are still plain `new GameObject`/`Instantiate` — pool them if they
  ever become hot.

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
  scene** — the run player is `DontDestroyOnLoad` so it is excluded for free — and exposes
  `IsCleared` / `RemainingEnemies` / `Cleared`. This is the "kill everything first" gate:
  `SectionExit` (`requireEnemiesDead`) and `ShopInteractable` both refuse to work while enemies
  live. When the last one dies it plays `clearSfxId` and spawns `clearEffectPrefab` (the fireball's
  `VFX_Explosion`) at every `SectionExit` in the scene, so the feedback points at the way out.
- **Shop placement** — `RunManager` picks `_shopSectionIndex` at random from the sampled section
  order (seeded off `RunSeed`, so it's reproducible; never the boss scene), which guarantees
  **exactly one shop per world, always before the boss** — including the degenerate case of a world
  with a single section. `SpawnShopIfDue` instantiates `shopPrefab` at a `ShopSpawnPoint` marker if
  the section has one, else `shopDistanceBeforeExit` units short of the `SectionExit`.

### Combat (`Assets/Scripts/Combat/`)

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
- Both are already on `Player.prefab` and every `Enemy_*.prefab`. **New damageable prefabs need
  `Knockback` + `HitFlash` added by hand** — `Health` works without them, just silently unpushed.
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
  legacy `AbilityUser` if an ability is somehow equipped.
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
- **Runtime** (`Items/Shot/`) — `ShotProjectile` and `ShotBeam`, both pooled, built in code, reusing
  `AbilityHit` for target filtering/damage and `AbilityFx` for sprite/tint/sorting.
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
  `Weapon_ProyectilRecto`), 4 modifiers, 4 free-pool items.
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
use it), `AbilityFx` (code-built sprites/flashes, no art pipeline), `ProjectileSpec` /
`ProjectileFactory` and `Projectile` (the pooled projectile the bosses fire).

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
  - `SummonAddsAttack` — adds, capped by `maxAlive`, killed when the boss dies.
- **Every boss is built on a different question on purpose** — that is the design rule for the next
  one, not just a description of these. Bosses 4-6 have no scene of their own yet: their generators
  only build the prefab in `Assets/Prefab/Enemies/`, to be dropped into whatever arena is being
  tested.
  - **Árbol Ancestral** (World 1) — *height*: shockwave bands plus spirals, with ground-bound
    sprouts as adds.
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
  - **Reina Escarabajo** (`BeetleQueenPack`) — *where is the fight happening?*: floods the floor
    with acid and grows ledges to stand on, so for a few seconds the fight leaves the ground
    entirely, with her brood (`Enemy_Beetle`) forcing the choice between coming down to clear them
    or waiting it out. The only animated boss so far — Brackeys' 3-frame beetle at ×1.5 driven by
    `SpriteFlipbook`.
  - **Sello Profano** (`UnholySealPack`) — *what deserves your damage?*: the anchor ritual is its
    spine, and the rest of the deck is deliberately a greatest-hits of the earlier bosses (sweep,
    refuges, spiral, rubble) so a player who got this far recognises everything and only has to do
    it faster, with a countdown running. Brackeys' pentagram at ×2.5, hovering, with a circle
    collider so you can pass underneath.
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

### Vendored third-party content — do not search or modify by default

`Assets/Brackeys/`, `Assets/Cainos/` (including its bundled `Third Party/Lucid Editor` inspector
attribute library), and `Assets/Dragon Warrior Files/` are imported asset packages (art, demo
scenes, a few utility scripts like `Chest.cs`/`Elevator.cs`/`SecondOrderDynamics.cs`). They are not
part of the game's own architecture. Scope searches to `Assets/Scripts`, `Assets/Ui`,
`Assets/Audio`, and `Assets/Scenes` unless a task specifically concerns one of these packages —
scanning all of `Assets/` pulls in hundreds of irrelevant demo/example files from them.
