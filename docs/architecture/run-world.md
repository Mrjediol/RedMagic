# Run / world system (`Assets/Scripts/Run/`)
- **`WorldDefinition`** — a ScriptableObject per world: a pool of section `SceneReference`s, how
  many to sample per run (`sectionsPerRun`), and a boss `SceneReference` — **optional**: empty (`HasBoss` false) = finishing the sections completes the world. Today only World1 is unlocked (sections `NivelHielo_1000` and `3 CombatRoomsTest` — the room/door test level — both per run, no boss); World2/3 are locked and empty until their levels exist. `TryBuildRunOrder(seed,
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
  loaded), so health/upgrades survive section transitions — any Player (`PlayerMovement`) inside the loaded scene is deactivated right after the load, before its `Start`, so a level can keep an active Player for solo testing; `RunManager` repositions it at each
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
  `Assets/Legacy/Scripts/`, along with `CauldronInteractable` and `AbilityChest` (see Main Hub
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
  entries reference scene objects) — optional, one per combat scene
  (**GameObject ▸ RedMagic ▸ Wave Manager** creates it with two child spawn points). Waves are a small graph, not a
  fixed sequence, and several can run at once:
  - **Start** (`WaveStart`): *al empezar el nivel* (`Start`), *al entrar en su sala*, or *la lanza otra oleada*; then
    `startDelay`, then each `EnemySpawn` is `Instantiate`d after its `spawnDelay` at its spawn point and moved into the
    manager's scene. A wave is done when every spawned `Health` is dead (`Health.AnyDied` + a null/dead poll).
  - **On finish**: `launchWave` (`launchIndex`, -1 = the next one), `finishRoom` (opens its room's doors; optional
    `dropItem` → `ItemPickup.Spawn` at the room centre), `finishLevel` (releases the `SectionClearTracker` hold →
    exit/shop open, `OnAllWavesComplete`). Spawned enemies are **not** `Register`ed in the tracker, so an optional room
    left uncleared never blocks the exit. No wave marked `finishLevel` → the level finishes when every wave is done.
  - **Rooms** (`LevelRoom`, a cell rectangle with scene handles, *+ Sala* in the Inspector; it may include its doors):
    when the player's box is fully inside the room's **interior** — the rectangle shrunk `entryMargin` (2) cells left
    and right, dotted in the gizmo, so the door never shuts in the player's face nor lets a dash carry them back out —
    and the room still has unfinished waves, its doors close (`Lock`) and its *al entrar* waves launch. The player is
    found by tag `Player` (re-found if the cached one is deactivated, as `RunManager` does to a scene's test player).
    Its doors are found by geometry: every `LevelDoor` overlapping the room rectangle grown by half a cell (painted
    doors aren't saved, so they can't be dragged in); gizmo lines show them (green = starts open). No trigger collider on
    purpose: *Queries Hit Triggers* is on, and a room-sized trigger would be seen by projectile/sensor rays.
  - Door start state comes from the tile: `DoorTile_Energia` (closed) / `DoorTile_Energia_Abierta` (open).
  - Old data (before rooms) is migrated once (`roomsMigrated`): wave 1 at load, each launches the next, last finishes
    the level — the old behaviour. *+ Oleada* hands `finishLevel`/`finishRoom` from the previous wave to the new one
    and makes the previous launch it.
  - Each enemy row's spawn-point dropdown ends with *+ Nuevo en el centro de <sala>*: a new point, child of the wave's
    room, added to the manager's list.
  Events `OnWaveStart(int)` / `OnWaveComplete(int)` / `OnAllWavesComplete` (0-based). Gizmos: a sphere per spawn point
  coloured by the first wave that uses it, labelled `W<n> <enemy> ×<count>`.
- The player keeps its prefab scale in every scene (the old per-scene `PlayerScaleConfig` is in `Assets/Legacy/`).

## Level sketch → level scene

Superseded: the old web tab 🧱 Boceto de nivel and `LevelSketchImporter` (prefab with tiled edge strips, `LevelKit`)
are in Legacy. Levels are now a `Level` + Tilemaps scene made with **EditorNiveles** (web editor + menu *EditorNiveles ▸ Importar nivel…* → new scene, which also places `SectionEntry`/`SectionExit` on the floor at both ends):
see `docs/architecture/levels.md` ▸ EditorNiveles.
