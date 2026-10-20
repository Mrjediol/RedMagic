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
- **Part-based enemies and bosses (rig)**: sounds are per *action*, not per sprite animation.
  - **Per gesture**: `RigGesture.sounds` — a list of `RigGestureSound` (`SoundCue` + `moment`: Start of the
    gesture / Release of the strike / At its own step + instant), played by the driver (`RigDriver.PlayGestureSounds`,
    `At` ones in `FireStep`) whichever attack uses the gesture. Registry rows: `Enemies|Bosses/<name>` ▸
    "Gesture · <name>" (" · 2", " · 3"… for the next ones; `SoundCategories`, from `gestures[i].sounds[j].sound`).
    Empty = silent. Edited in the web (one 🔊 lane per sound, see `procedural-rig.md`); the importer keeps them
    **by gesture name** when the JSON has no `sounds` key.
  - **Per attack of the deck**: each enemy-deck attack is a `BossAttack` asset in
    `Assets/ScriptableObjects/EnemyAttacks/<Enemy>/`, whose `sound` is a normal cue → rows
    `Enemies/<Enemy>` ▸ "Attack · <id>" (category by that folder convention, like boss attacks by their definition).
  - **Per state**: wake / hurt / death / move / attack are the usual `SoundEmitter` moments, declared only
    when they apply (Wake only if `sleepsUntilDetected`).
  - A rig boss that shares a `BossDefinition` with another boss (BossIce2 / ColosodeHielo2) lists the
    definition's attack sounds under the definition's name, not under itself.
  - After an import, press **Regenerar registro** in the Sounds tab (a full scan opens scenes, so it is not
    automatic); the new rows appear, empty ones silent until *Preparar huecos* or you assign clips.
- **Arsenal weapons** (`Delivery = Arsenal`): sounds by id in `Resources/Arsenal/ArsenalAssets ▸ Sonidos`
  (`ArsenalSounds.Play("fan_hit")`), WAVs rendered from the web code by
  `docs/bocetos/habilidades/tools/render-sonidos-armas.cjs` into `Assets/Audio/Sounds/Player/Arsenal/` and wired by
  file name (*Arsenal ▸ Preparar*). Rows `Player/Arsenal/<id prefix>` ▸ `<id>`. The arsenal `WeaponDefinition`s carry
  `SoundIgnore` (their fire/comet cues never play).
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
- **Sounds tab** (Biblioteca Jere): folder tree, filters, assign/replace clips and variants straight into
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
- **The game's synthesised palette** (`Assets/Audio/Sfx/Game/<family>/<id>.wav` + `palette.json`): every slot
  without a recording of its own is filled from one small palette, defined as data in
  `Web/RedMagicWeb/modules/game-sounds.js` (+ `game-sounds-added.js`: sounds accepted in the web's 🎨 Designs,
  written by the taller server — `docs/architecture/designs.md`) (`GAME_SOUNDS` — same synthesiser as the Lab's — and `GAME_ASSIGN`,
  the ordered rules "which slot gets which clip") and written by `tools/bake-sounds.mjs` (which refuses to write
  if `check-sounds.mjs` finds a broken sound: silent, clicking, cut off, loop that does not close, over its
  family's length budget).
  - **`Tools ▸ RedMagic ▸ Audio ▸ 4 · Asignar paleta…`** (`SoundPaletteAssigner`; `4b` = dry run) applies the rules
    through `SoundSlotEditor`. A rule matches by regex on the row's category / name, the owner's type, the
    field / trigger, the asset name and an enemy's archetype (`archetype: '^Flying'` → a quiet breath of air on Move instead of
    the generic footsteps) — by *what the thing is* (attack type, "Rock" / "Laser" / "Ice" in a
    projectile's name, "Robot" / "Golem" / "Spirit" in an enemy's), so new content gets its sounds with no new rule.
    It only writes into a slot that is empty, on `generic_test` or on the enemy fallback; never one with a real
    clip, a silenced one or one removed in the Sounds tab. Idempotent: run it again after adding content.
  - Per-owner variation is the cue's pitch range / volume (written by the rule), never a new clip.
  - **Options**: a sound heard all run long has alternative takes `<id>_2`, `<id>_3`… that are assigned to
    nothing; try one by assigning it in the Sounds tab. Web ▸ VFX Lab ▸ "🔊 Sonidos del juego" plays them all.
  - Enemy Move / Attack (and koros' Hurt / Death) have no rule on purpose: they stay on the generic fallback.
    Music is out of the palette (a synthesiser of tones and noise does not write music).
  - Changing a sound = edit its entry, `npm run bake-sounds` (same file name → same GUID → every slot follows).
- **Example clips from the VFX Lab**: `Assets/Audio/Sfx/VfxLab/*.wav` (+ `sounds.json`: label, loop, which effect each
  was made for) are synthesised placeholders written by `Web/RedMagicWeb/tools/bake-sounds.mjs`. They are **assigned to
  nothing**: they are just clips to pick in the Sounds tab. Assigning one is adding a sound → the grading rule applies.
