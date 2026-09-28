# Run / world system (`Assets/Scripts/Run/`)
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
  than `Health` knowing anything about runs. Every transition is covered by **`ScreenFader`**
  (`Assets/Scripts/Run/`, self-bootstrapping persistent uGUI overlay, sorting 32000, unscaled time):
  `EnterCurrentRoutine` fades out after freezing and fades in after unfreezing; `ReturnToHub` fades
  out, then loads. Any `Single` load that arrives with the screen covered fades back in by itself,
  so a cut transition can't leave it black. Timings/colour: `RunManager ▸ transitionFadeOut/In/Color`.
- **`SectionExit` is also how you leave the hub.** Assign its `world` (a `WorldDefinition` asset
  reference, not an index/name, so reordering `RunManager.worlds` can't desync it) and, with no run
  in progress, crossing it calls `StartRun(world)` instead of `AdvanceSection()`. Leaving the hub
  and moving between sections are the same gesture for the player — walking through a door — so
  they are the same component, and `RunManager` is what knows whether that means start or advance.
  `_used` is only latched if the run actually starts, so a locked or misconfigured world leaves the
  door working. Its gizmo is blue for a hub door, amber for a section exit. MainHub's is at the far
  right of the ground (x≈35) with `requireEnemiesDead` off — there is nothing to kill in the hub.
  The predecessor, `TombInteractable` (proximity + press E, its own input handling), is retired to
  `Assets/Scripts/Legacy/`, along with `CauldronInteractable` and `AbilityChest` (see Main Hub
  interactables below for what replaced them).
- **`SectionClearTracker`** — `DontDestroyOnLoad` singleton placed in MainHub (with an
  `AfterSceneLoad` fallback). On every scene load it subscribes to every `Health` **inside that
  scene** — the run player is `DontDestroyOnLoad` so it is excluded for free — **except** anything
  that can never fire `Died`: Player-team (`Teams.Of`), inactive in hierarchy, or a
  `TrainingDummy` (`CountsAsEnemy`). Section scenes carry a *disabled* `Player` instance for solo
  testing; counting it (the old `GetComponentsInChildren<Health>(true)` scan) locked every exit
  and the boss reward forever — and exposes
  `IsCleared` / `RemainingEnemies` / `Cleared`. This is the "kill everything first" gate:
  `SectionExit` (`requireEnemiesDead`) refuses to work while enemies
  live. When the last one dies it plays `clearSfxId` and spawns `clearEffectPrefab` (the fireball's
  `VFX_Explosion`) at every `SectionExit` in the scene, so the feedback points at the way out.
- **`WaveManager`** (+ `WaveDefinition` / `EnemySpawn`, serialized classes — not SOs, because
  entries reference scene `Transform`s) — optional, one per combat scene
  (**GameObject ▸ RedMagic ▸ Wave Manager** creates it with two child spawn points). `Start` →
  wave 1 (after its `startDelay`); each `EnemySpawn` is `Instantiate`d after its `spawnDelay` at its
  spawn point and moved into the manager's scene (sections load additively); next wave when every
  spawned `Health` is dead (`Health.AnyDied` + a null/dead poll for enemies destroyed without
  dying). Events `OnWaveStart(int)` / `OnWaveComplete(int)` / `OnAllWavesComplete` (0-based).
  Enemy prefabs are untouched. It **holds `SectionClearTracker`** (`AddHold`/`ReleaseHold`) so the
  exit/shop stay locked between waves, and `Register`s each spawned enemy (the tracker only scans at
  load); the "cleared" feedback fires once, after the last wave — an unload mid-wave releases
  silently. Gizmos are always drawn: a sphere per spawn point coloured by the first wave that uses
  it, labelled `W<n> <enemy> ×<count>`; unused points grey. Inspector (`WaveManagerEditor` +
  `EnemySpawnDrawer`): collapsible waves with a summary line, one row per enemy (prefab / spawn
  point dropdown from the manager's list — stored as a reference, not a name / delay).
- **`PlayerScaleConfig`** — per-scene override for the player's `transform.localScale`, needed
  because each level's background is generated separately by AI and the art scale isn't consistent
  from one image to the next. Lives at `Assets/Resources/PlayerScaleConfig.asset` (loaded with
  `Resources.Load`, same pattern as `CurrencyConfig`/`ShopConfig` — nothing to drag onto
  `RunManager`), a list of `(SceneReference, float)` rows edited through
  `PlayerScaleConfigEditor`'s **Tools ▸ RedMagic ▸ Jugador ▸ Sincronizar escalas con Build
  Settings** (also a button in the asset's own Inspector), which appends one row per enabled
  Build Settings scene at scale 1.0 — idempotent, never touches a row already tuned. `RunManager`
  calls `ApplyPlayerScale(scene)` right after `Player.transform.position = entry.SpawnPosition`
  (`PlacePlayerAtEntry`, `MoveToHubEntry`/`EnterHub`) — same synchronous span, before any `yield`,
  so the resize lands in the same frame as the teleport and is never seen popping. No entry for the
  loaded scene → scale 1.0 and a console warning (a new scene nobody's tuned yet); no asset at all
  → the prefab's scale is left alone entirely.
