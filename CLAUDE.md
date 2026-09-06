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
