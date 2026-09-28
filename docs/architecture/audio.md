# Audio (`Assets/Audio/`)
No string ids anywhere. Every sound slot is a **`SoundCue`** (plain `[Serializable]` class, no SO): clip
variants, volume, pitch range, no-repeat, priority, positional + rolloff. Three places hold them:
- **`SoundEmitter`** on an object's root, one entry per `SoundTrigger`. It subscribes itself to every
  **`ISoundEventSource`** on its GameObject (`Health`, `PlayerMovement`, `PlayerAttack`, `RangedAttack`,
  `EnemyAttack`, `EnemyBrain`, `BossController`, `Projectile`, `WeaponUser`, hub props). Each source also
  **declares** the triggers it can fire (`DeclareSoundTriggers`, conditional on its config) — that is how
  the registry knows a prefab's slots before it has entries. A new sound moment = raise `SoundTriggered`,
  add it to `DeclareSoundTriggers`, append a trigger value (explicit numbers, never renumber; 8 retired).
- **`SoundCue` fields** on data owners: `BossAttack.sound` (+ `pickupSound`, `igniteSound`, `fireLoopSound`
  on their attack types), `BossPhase.transitionSound`, `WeaponDefinition` (charge/fire/impact/terrain/
  expire/explosion/beam loop), `LegendaryPassiveTuning` (one per passive
  moment), `PassiveDropCinematicSettings.landSound`. A field that only applies sometimes carries
  **`[SoundSlotIf(nameof(boolMember))]`**: hidden in the Inspector and absent from the registry when false.
- **`SystemSounds`** (on `Resources/AudioManager.prefab`): everything that is the game's, not an object's —
  UI (hover/focus/click/back/deny), open/close per menu, menu actions, shop (buy/deny/reroll), menu
  music (`[MusicSlot]`), run flow, economy, synergy, status, generic enemy sounds. No scene object holds a
  sound: the scanner then never has to open scenes (opening them additively floods the console with URP
  "more than one global light" errors).
  `SystemSounds.Play(s => s.runStart)`. Never put a sound field on a scene-placed object that exists in
  several scenes (RunManager, SectionClearTracker…): it would be one slot per scene.
- **`AudioManager`** (prefab in Resources, bootstrapped `BeforeSceneLoad`, owns the only `AudioListener`):
  `Play(cue[, worldPos])`, `StartLoop/StopLoop` (loops pause with the game), growable voice pool
  (`sfxVoicesMax`), priority stealing, same-clip anti-stacking, mixer volumes (debounced PlayerPrefs).
  Music: `PlayMusic(AudioClip)`; scene music via `PlaySceneMusic(name)` = `Resources/Music/<name>` —
  the one string-keyed path, kept on purpose (tech debt, see `docs/audio-system-audit.md`).
- **UI Toolkit**: `UiSounds.Bind(root)` once per document (hover + navigation focus, silent initial focus,
  `BackClass` / `NoClickClass`); menus play their own open/close; `UiSounds.Deny()` on refused actions.
- **Registry tooling** (`Assets/Audio/Editor/`): `SoundSlotScanner` derives every slot from the project;
  assets labelled **`SoundIgnore`** (prototype content) are skipped. `SoundSlotEditor` writes a slot's cue
  wherever it lives. `Tools ▸ RedMagic ▸ Audio ▸ 2 · Preparar huecos…` adds missing emitter entries and
  fills empty slots with `Assets/Audio/Test/generic_test.wav` (never touches a slot with a clip).
- Import: anything under `Assets/Audio/` gets preload + mobile defaults (`SfxImportPostprocessor`).
- **Generic enemy sounds**: `SystemSounds.enemyHurt/Death/Move/Attack`. An enemy (`EnemyStats` or
  `BossController`, marker `IEnemySoundFallbackUser`) whose `SoundEmitter` slot is empty plays them
  (`SoundEmitter.useGenericFallback`, default on). `EnemyFactory` stamps a `SoundEmitter` on every enemy,
  so a new enemy shows up in the Sounds tab with hurt/death/move/attack slots and already sounds.
  A `SoundEvent.silent` entry plays nothing at all (that is what "remove sound" writes).
- **Sound registry** (`Assets/Audio/Registry/`): `SoundRegistry.generated.json` (derived — scanner +
  music by convention + declared; never hand-edit; `Tools ▸ RedMagic ▸ Audio ▸ 3` or the tab regenerates
  it, byte-identical on reruns), `SoundDeclared.json` (the few sounds with no hook yet; they merge with
  the real slot by `matchComponent` + `matchMember` once it exists), `SoundStatus.json` (user grades
  Pending/Perfect/Good/Bad/Horrible + notes, keyed by asset GUID + component + field/trigger). Prototype
  content is excluded with the asset label `SoundIgnore`, never a list in code.
- **Sounds tab** (Biblioteca Web): folder tree, filters, assign/replace clips and variants straight into
  the prefab/asset, preview, grade, notes, add a declared slot, remove a sound. It is the only place sound
  status lives — no markdown status docs. `SoundRegistryWatcher` counts changes to prefabs/assets/
  scenes/scripts/music since the last regeneration and the tab shows a "regenerate" notice (a full scan opens
  scenes, so it is not run on every change).
- **Rule — grading new sounds**: whenever a new sound is added to the game, ask the user to grade it
  Perfect / Good / Bad / Horrible before considering the task done, and write the answer into
  `SoundStatus.json` (`SoundRegistry.SetStatus`, or the Sounds tab).
- **Rule — sound state questions**: when the user asks about the state of the game's sounds, read
  `SoundRegistry.generated.json` and `SoundStatus.json` directly and answer from them. Never build a
  report file.
