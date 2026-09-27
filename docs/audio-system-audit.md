# Audio system audit

Date: 2026-09-27. Scope: `Assets/Audio/**`, every `.cs` touching `AudioSource/AudioClip/AudioMixer/
AudioListener/AudioManager/SoundEmitter`, `GameAudioMixer.mixer`, `ProjectSettings/AudioManager.asset`,
clip import settings, and every scene/prefab that serializes an audio component.
`Assets/_Reference/` does **not exist** in this repo — there is no legacy audio implementation to rescue.

**Verdict: (b) — good playback core, weak authoring layer.** Details and plan in §4.

---

## 0. Headline findings

1. **`SoundEmitter` is wired nowhere.** Zero prefabs, zero scenes carry it (GUID
   `5e7509053f7d33041993a6e2142f35c0` appears in no `.prefab/.unity/.asset`). The code that calls it
   (`EnemyBrain`, `EnemyController`, `Projectile`) always gets `null`. It also has no "checkboxes":
   it is a list of entries, each with a `SoundTrigger` enum dropdown (`OnHit/OnDeath/OnSpawn/Custom`).
2. **The real system is string ids.** ~70 call sites do `AudioManager.Instance.PlaySFX("SFX_…")` with
   ids typed into ~25 serialized `string` fields or hardcoded in code.
3. **The id registry is copy-pasted into 14 scenes.** `AudioManager` with an identical 11-entry `sounds`
   list lives in MainMenu, MainHub, Ice1-4, Fire1, `Fire1 1`, Bosque1-4/Boss, testboss, plus 2
   `_Recovery` scenes. 13 copies are byte-identical today; any edit must be repeated 14× or it silently
   works only when Play starts from the edited scene (the CLAUDE.md gotcha, now 7× worse).
4. **Almost nothing is actually audible.** Of 11 registered ids, 4 have no clip (`SFX_ButtonHover`,
   `SFX_ButtonClick`, `SFX_Fireball`, `SFX_PlayerDash`) — so all UI, section-clear, dash, shop, hub
   props and item pickup are silent. The rest are placeholders reused across meanings (below). No
   enemy, boss, boss attack, phase or weapon has any sound id set.
5. **The new clips in `Assets/Audio/Sounds/`** (`player_dash`, `player_footstep_01-03`, `player_jump`,
   `shop_buy`, `ui_hover`) are referenced by nothing. `sfx_gen.py` sits inside `Assets/` with a `.meta`.

---

## 1. Architecture — how a sound plays today

### 1.1 Call path

```
                 ┌──────────────────────── string id path (everything real) ────────────────────────┐
 Health.TakeDamage/Die ─┐                                                                           │
 PlayerMovement (jump/dash/footstep/stomp) ─┤                                                        │
 PlayerAttack / RangedAttack ─┤                                                                      │
 BossController (roar, phase) / BossAttack.Telegraph ─┤   AudioManager.PlaySFX(id)                    │
 SectionClearTracker / ShopManager / Hub props ─┤ ───►   └─ TryGet(id) in _library (from `sounds`    │
 ItemPickup / WeaponUser (charge) ─┤                          list of THIS scene's AudioManager copy) │
 10 UI controllers (hover/click) ─┘                        └─ PlayClip(clip, vol, pitch, group)       │
                                                               └─ TakeVoice(): 8 pooled AudioSources, │
 SoundEmitter.Play(trigger)  ── (never reached: no instances) ─►  round-robin, steal if all busy      │
   ▲ EnemyBrain / EnemyController / Projectile (OnHit, OnDeath)     └─ voice.Play() → SFX mixer group  │
   ▲ OnEnable (OnSpawn)                                                                               │
                                                                                                      │
 Music: MainMenuController/BossController → PlayMusic(id) ─┐                                          │
        RunManager → PlaySceneMusic(id) → Resources.LoadAsync("Music/<id>") ─┴─► 1 music AudioSource, │
                                                                   fade out/in on unscaled time ──────┘
 Volume: OptionsMenuController → SetVolume/SetMute(group) → mixer.SetFloat("<Group>Volume", 20·log10(v))
```

**One playback entry point** (`AudioManager.PlayClip`) — good. There are **no stray
`AudioSource.Play`/`PlayOneShot`/`PlayClipAtPoint` calls anywhere** in gameplay/UI code. The only other
`AudioSource` touches are `PlayModePerfMonitor` (editor diagnostics, read-only).

### 1.2 Trigger sites outside `SoundEmitter` (i.e. the id path)

| Where | Field / literal | Current value |
|---|---|---|
| `Combat/Health.cs:254,350` | `hurtSfxId`, `deathSfxId` | Player.prefab: `SFX_PlayerHurt`/`SFX_PlayerDeath`; enemies: empty (pushed from `EnemyTuning`) |
| `Gameplay/PlayerMovement.cs:1147,1225,1348,1947` | `jumpSfxId`, `dashSfxId`, `footstepSfxId` (+`footstepInterval`), `stompSfxId` | registered ids |
| `Gameplay/PlayerAttack.cs:133` | `attackSfxId` | `SFX_PlayerAttack` |
| `Combat/RangedAttack.cs:167` | `attackSfxId` | `SFX_Fireball` (no clip) |
| `Items/WeaponUser.cs:214` | **hardcoded** `SFX_ButtonHover`/`SFX_ButtonClick` as weapon charge sound | placeholder hack in code |
| `Items/ItemPickup.cs:163` | **hardcoded** `SFX_ButtonClick` | — |
| `Bosses/BossController.cs:584,587,931` | `musicId` (default `Music_Boss`, unregistered), `roarSfxId`, `BossPhase.transitionSfxId` | all empty in assets |
| `Bosses/BossAttack.cs:159` | `sfxId` per attack asset | all empty |
| `Run/SectionClearTracker.cs:237` | `clearSfxId` | `SFX_Fireball` (no clip) ×14 scenes |
| `Economy/Shop/ShopManager.cs:231,268,300` | `denySfxId`, `rerollSfxId`, **hardcoded** buy `SFX_ButtonClick` | no clip |
| `Hub/HubLootContainer.cs`, `AnvilInteractable.cs`, `MirrorInteractable.cs` | `openSfxId`, `lootSfxId`, `useSfxId` | `SFX_ButtonClick` (no clip) |
| `Ui/PassiveDropCinematic.cs:290` (via settings asset) | `landSfxId`, `continueSfxId` | empty / `SFX_ButtonClick` |
| 10 UI controllers (`MainMenu`, `Pause`, `Options`, `ItemMenu`, `Upgrade`, `Mirror`, `WeaponForge`, `WeaponChoice`, + Legacy ×2) | **hardcoded** `SFX_ButtonHover` / `SFX_ButtonClick`, ~35 lines | no clips |
| `Ui/MainMenuController.cs:106` | `menuMusicId` | `Music_Menu` |
| `Run/RunManager.cs:664,922` | `hubMusicId`, `shopMusicId`, `World{n}-{k}`, `BossBattle{n}` via `PlaySceneMusic` | Resources/Music convention — works |

### 1.3 Registry contents (identical in all 13 readable scene copies)

| id | clip | note |
|---|---|---|
| `Music_Menu` | `Sly_2_…_00367.wav` (72 KB, <1 s) | a short SFX looping as menu "music" |
| `SFX_PlayerDeath` | **same** `…_00367.wav` | shares the menu-music clip |
| `SFX_PlayerHurt`, `SFX_EnemyStomp`, `SFX_PlayerAttack` | `enemydmg1.wav` | one clip, three meanings |
| `SFX_PlayerJump` | `jump.wav` | |
| `SFX_PlayerFootstep` | `jump2.wav` | footstep uses a jump sound |
| `SFX_ButtonHover`, `SFX_ButtonClick`, `SFX_Fireball`, `SFX_PlayerDash` | none | silent |

---

## 2. What's missing for real SFX work

| Requirement | Status | Detail |
|---|---|---|
| One-shot without allocating an AudioSource | ✅ | 8 pre-built voices, reused. No allocation per play. |
| Overlapping sounds don't cut each other off | ⚠️ partial | Separate voices, so no cut-off up to 8. At 9+ the next voice in round-robin order is `Stop()`ped abruptly (click) — **no priority**, so a footstep can kill the boss roar or a UI click. **No per-clip concurrency cap / same-frame dedupe**: a 5-pellet shotgun hitting 5 enemies = 5 identical sounds in one frame (phasing, loudness spike) that also evict everything else. `PlayOneShot` is deliberately not used (per-voice pitch) — correct choice. |
| Multiple clip variants, random, no-repeat | ❌ | `SoundData.clip` and `SoundEvent.clip` are single clips. The three new footstep variants can't be used. |
| Per-sound volume + random pitch in Inspector | ⚠️ | `SoundEmitter`: volume + min/max pitch ✅ (but unused). Id registry: volume + **fixed** pitch only, no randomisation — and it lives in scenes, not on the object. |
| Looping / interval sounds with clean start/stop | ❌ loops / ⚠️ interval | No SFX loop API at all: `PlaySFX` ignores `SoundData.loop`, voices are `loop=false`, no handle to stop. Needed for weapon charge hum, beams, burning `BossHazard`, boss flood, ambience. Footsteps: interval timer in `PlayerMovement` works (scaled time, not reset on stop — intentional, fine), but single clip and player-only. |
| Paused behaviour | ⚠️ | `AudioListener.pause` is never used, voices don't use `ignoreListenerPause` — everything keeps playing on pause, by design (comment in `GameStateManager`). Fine for one-shots and music. **Would be wrong for loops** (a charge hum would drone under the pause menu) — no mechanism to pause gameplay audio while keeping UI audio. Timers: footstep uses `Time.deltaTime` inside an Update that early-outs on `!CanPlayerAct`/`timeScale==0` → frozen ✅. Music fades use unscaled time ✅. |
| 2D vs positional + rolloff | ❌ | Every voice is 2D (`spatialBlend` 0), `PlayClip` takes no position. An enemy dying off-screen is exactly as loud as one next to the player; no stereo pan. `SoundEmitter` routes through the same 2D voices, so being "per object" gives it no position either. |
| UI sounds from UI Toolkit (hover + gamepad/keyboard focus) | ⚠️ | Hover is hand-registered `PointerEnterEvent` per button in each controller (~35 duplicated lines). **No `FocusInEvent`/`NavigationMoveEvent` sound anywhere → gamepad/keyboard navigation is silent.** No back/cancel or deny sound. All of it is moot today: both UI ids have no clip. |
| Per-group volume for a settings menu | ✅ | Mixer `Master → Music, SFX`, exposed `MasterVolume/MusicVolume/SFXVolume`, linear→dB `20·log10(max(v,1e-4))` (−80 dB floor), mute = floor, persisted in PlayerPrefs, applied in `Start`. Options menu already has 3 sliders + 3 mutes. One flaw: `PlayerPrefs.Save()` on **every** slider `ValueChanged` = a disk write per drag frame on mobile. No separate UI group (UI shares SFX) — acceptable. |
| Mobile: voices | ⚠️ | 8 SFX voices + 1 music (project: 32 real / 512 virtual). 8 is tight for a bullet-hell with UI on top; without priority/dedupe it will audibly steal. |
| Mobile: import settings | ❌ | All 13 SFX clips: **Vorbis, quality 100, Decompress On Load, stereo, `preloadAudioData: 0`**, no Android override. Preload off → first play of each sound loads the clip synchronously on the main thread (hitch + late first playback). Vorbis at 100 for sub-second SFX wastes decode CPU vs ADPCM; stereo doubles memory for no benefit in a 2D mix. Music is handled correctly by `MusicImportPostprocessor` (Streaming + Load In Background) ✅, though quality 100 is oversized. There is **no equivalent postprocessor for SFX**. |
| Mobile: latency | ⚠️ | `DSP Buffer Size = 1024` ("Best performance") — adds ~21-46 ms on top of Android's output latency. For hit/jump feedback `512` ("Good latency") is the usual choice; worth one device test. |

---

## 3. Code quality / flow

### 3.1 Cost of adding one sound today

**Id path (the one that actually runs)** — e.g. "enemy death sound":
1. Import clip.
2. Add a `SoundData` row to `AudioManager.sounds` in MainMenu.unity.
3. Repeat in MainHub + 12 other scenes (or accept: silent when pressing Play from any un-edited scene; a build only ever uses MainMenu's list).
4. Type the id string into the consumer field (`EnemyStats ▸ Tuning ▸ deathSfxId`). A typo = runtime warning only.
5. If the event has no id field (enemy attack, weapon fire, projectile impact, beam, hazard, UI back/deny, boss hit…) → **code change**.

≈ 4 steps + N scenes, and code for most events. **Not a 30-second job.**

**`SoundEmitter` path** — add component, add entry, pick trigger, drag clip: genuinely 30 s — but it
only ever fires `OnHit`/`OnDeath` from `EnemyBrain`/`EnemyController`/`Projectile` and `OnSpawn` from
`OnEnable`. Nothing for the player, bosses, weapons, player shots (`ShotProjectile`), UI.

### 3.2 Bugs / risks

| # | Severity | Issue |
|---|---|---|
| 1 | High | **Registry duplicated in 14 scenes** (+ `Fire1 1.unity`, 2 `_Recovery` scenes that shouldn't be in the repo). Guaranteed drift the moment real sounds are added. |
| 2 | High | **SFX clips `preloadAudioData: 0` + synchronous load on first `Play()`** → hitch the first time each sound plays (first jump, first hit…). |
| 3 | Medium | **`SoundEmitter.OnEnable` plays `OnSpawn` on pool creation.** `PrefabPool.createFunc` does `Instantiate(prefab)` (active → `OnEnable` → sound) then `SetActive(false)`, then `Get` activates again → **the first spawn of every new pooled instance plays OnSpawn twice** (and prewarm plays it for objects nobody spawned). |
| 4 | Medium | Voice stealing has no priority and no per-clip cap (see §2). |
| 5 | Medium | `Health.TakeDamage` plays `hurtSfx` and then, on a lethal hit, `Die()` plays `deathSfx` in the same frame — both sounds stack on every kill. |
| 6 | Medium | Placeholder aliasing: `SFX_PlayerDeath` = menu music clip; hurt/stomp/attack = same clip; footstep = jump2; `WeaponUser` charge and `ShopManager` buy use UI ids hardcoded in code. |
| 7 | Low | `PlayMusic(id)` doesn't cancel a pending `PlaySceneMusic` async load: `PlaySceneMusic(A)` then `PlayMusic(B)` before A finishes loading → A overrides B when it lands. |
| 8 | Low | `_sound?.Play(...)` on a `GetComponent` result (EnemyBrain:607/633, EnemyController:163/422, Projectile:390/427): `?.` bypasses Unity's null, and in the Editor a missing component returns a fake-null wrapper, so the call enters `SoundEmitter.Play` on a dead object. Works by accident (empty list). Use `TryGetComponent`. |
| 9 | Low | `PlayerPrefs.Save()` per slider tick. |
| 10 | Low | `BossController.musicId` default `"Music_Boss"` is not registered → warning on any boss prefab not built by a pack (packs write empty). |
| 11 | Low | Dead/leftover: `SoundData.loop` ignored for SFX; `SoundEmitter.Has()` unused; 3 `Sly_2_*.wav` rips and `enemydmg1/jump/jump2.wav` are placeholders; `sfx_gen.py` inside `Assets/`; Legacy controllers still call `PlaySFX`. |
| 12 | Info | **AudioListener (corrected during phase 1):** in every scene the one listener sits on the `AudioManager` GameObject, not the camera, so the persistent AudioManager already carried it through additive loads — there was never a zero-listener window. Only `Shop.unity` has none (only matters when opened standalone). Phase 1 makes this ownership explicit (AudioManager adds its listener if missing and disables any other on load). |

### 3.3 What's good (keep)

- Single playback funnel, pooled voices, per-voice pitch, mixer routing on every voice.
- Music: async `Resources` load (no transition hitch), streaming import enforced by postprocessor, fades on unscaled time, "missing clip = silence, no warning" convention for scene music.
- Mixer/volume/mute/persistence is complete and correct; Options menu already consumes it.
- `SoundEmitter`'s shape (per-object list, trigger enum, `Custom` escape hatch, compact drawer) is the right authoring idea — it's just unfinished and disconnected.

---

## 4. Verdict: (b) Good foundation, needs targeted additions

Not a redesign: the playback core and mixer are right. What's wrong is the authoring layer — sounds
are addressed by strings stored in scenes, and the per-object component that should replace that is
orphaned. The plan makes **`SoundEmitter` the per-object surface** and **plain `SoundCue` fields** the
surface for sounds that belong to data assets/system components, both feeding the existing
`AudioManager` voices. No ScriptableObject sound assets.

### 4.1 New shared type — `SoundCue` (plain `[Serializable]` class)

`Assets/Audio/SoundCue.cs` — what every sound slot in the project becomes:

| Field | Type | Default | Purpose |
|---|---|---|---|
| `clips` | `AudioClip[]` | — | variants; one is fine |
| `volume` | `float [0-1]` | 1 | |
| `pitchMin` / `pitchMax` | `float` | 1 / 1 | random pitch range (equal = fixed) |
| `noRepeat` | `bool` | true | never the same variant twice in a row |
| `priority` | enum `Low/Normal/High/Critical` | Normal | voice stealing order (UI = Critical, footsteps = Low) |
| `positional` | `bool` | false | attenuate + pan by distance to listener (§4.4) |
| `maxInstances` | `int` | 0 (=global default) | cap simultaneous plays of this cue |

Drawer (`Assets/Audio/Editor/SoundCueDrawer.cs`): one foldout line "`3 clips · vol 0.8 · pitch 0.9-1.1`",
min-max pitch slider, and a **▶ preview button** (Editor-only `AudioUtil` play) so tuning is done
without entering Play Mode.

### 4.2 `AudioManager` additions (modify `Assets/Audio/AudioManager.cs`)

- `Play(SoundCue cue, Vector3? position = null)` — variant pick (no-repeat), pitch, volume, priority,
  positional math → existing voices. `PlaySFX(id)` / `PlayClip` stay (backward compatible).
- **Global anti-stacking**: same clip started within `sameClipWindow` (default 0.04 s) is skipped;
  `maxPerClip` (default 3) concurrent. Fixes shotgun/beam spam with zero per-object config.
- **Priority stealing**: steal lowest priority, then oldest; never steal Critical.
- **Loops**: `SoundLoopHandle PlayLoop(SoundCue cue, Object owner, Transform follow = null)` /
  `StopLoop(handle, fade)`. Separate small loop-voice pool (`loopVoiceCount`, default 4). Auto-stops
  when `owner` is destroyed/disabled (checked in `Update`), so pooled objects can't leak a hum.
- **Pause**: subscribes to `GameStateManager.PausedChanged`; `pauseGameplayAudio` (default true)
  pauses gameplay loops + gameplay voices, **UI voices are separate and never paused**, music
  untouched (current behaviour kept).
- **UI cues** (Inspector): `uiHover`, `uiClick`, `uiBack`, `uiDeny` — `SoundCue`s, played on 2
  dedicated UI voices.
- **Listener ownership**: `AudioManager` carries the one `AudioListener`, follows `Camera.main` in
  `LateUpdate`, and disables any scene-camera listener on `sceneLoaded` → never 0, never 2.
- **Bootstrap from one prefab**: `Assets/Resources/AudioManager.prefab`, instantiated in
  `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` if none exists (same pattern as the other
  self-bootstrapping singletons) → the prefab always wins; scene copies become redundant.
- `PlayMusic` cancels a pending `_sceneMusicRoutine` (bug 7).
- Inspector fields: `sfxVoiceCount` (raise default 8 → 12), `loopVoiceCount`, `uiVoiceCount`,
  `sameClipWindow`, `maxPerClip`, `pauseGameplayAudio`, `spatialFullRange`, `spatialSilentRange`,
  `spatialPanWidth`, the 4 UI cues.

### 4.3 `SoundEmitter` rework (modify `SoundEmitter.cs`, `SoundEventDrawer.cs`)

- `SoundEvent` = `trigger` + `customEventName` + **`SoundCue cue`** + `loop` checkbox (for
  `WhileActive`-style entries). Replaces `clip/volume/randomizePitch/min/maxPitch`.
  **Serialized-data break: none in practice** — no asset uses `SoundEmitter` today.
- Trigger enum, appended (existing 0-3 kept): `OnAttack`, `OnJump`, `OnAirJump`, `OnDash`, `OnLand`,
  `Footstep`, `OnActivate` (boss intro), `OnPhaseChange`, `OnImpact` (projectile hit),
  `OnDespawn`, `WhileActive` (loop for the object's lifetime).
- **Auto-binding in `OnEnable` / unbind in `OnDisable`** — this is what removes code from the loop:
  `Health.Damaged → OnHit`, `Health.Died → OnDeath` (and suppresses the same-frame `OnHit`, bug 5),
  `PlayerMovement.Jumped/AirJumped/Dashed/Landed/Stepped`, `EnemyAnimation` release → `OnAttack`,
  `BossController` intro / phase events. Any object with those components gets the sounds by adding
  entries — no code.
- `positional` defaults to **true** for emitter entries (world objects), false for UI/system cues.
- **Pool-safe `OnSpawn`**: skipped while the object sits under `PoolRunner.Root` inactive-at-creation
  (fix bug 3) — only real `Get`/`Spawn` activations fire it.
- `TryGetComponent` in callers (bug 8).

### 4.4 Positional audio (inside `AudioManager.Play`)

Not Unity 3D audio (orthographic camera at z = −10 makes 3D rolloff awkward and z-dependent).
Instead, cheap and predictable 2D: volume × `InverseLerp(spatialSilentRange, spatialFullRange, dx/dy
distance to listener)` and `panStereo = clamp(dx / spatialFullRange) × spatialPanWidth`. Off-screen
enemies get quieter and panned; nothing beyond `spatialSilentRange` takes a voice at all.

### 4.5 UI Toolkit — `Assets/Ui/UiSounds.cs` (new)

`UiSounds.Bind(VisualElement root)` — one call per menu. Registers **TrickleDown on the root**:
`PointerEnterEvent` (Button / `.rm-sfx`) → hover, `FocusInEvent` from navigation → hover (deduped
against a pointer hover of the same element), `ClickEvent`/`NavigationSubmitEvent` → click,
`NavigationCancelEvent` → back. Opt-out class `rm-nosfx`. Clips come from `AudioManager`'s UI cues.
Then **delete the ~35 hardcoded `PlaySFX("SFX_Button…")` lines** in the 8 live controllers.

### 4.6 Import settings — `Assets/Audio/Editor/SfxImportPostprocessor.cs` (new)

Everything under `Assets/Audio/` except `Resources/Music`: Decompress On Load, **Preload Audio Data
on**, **Force To Mono**, default Vorbis 70, **Android override ADPCM**, sample rate Optimize.
Same drop-it-in-the-folder contract as `MusicImportPostprocessor`. Also: music quality 100 → 60-70.
Project setting: DSP buffer 1024 → 512, verify on a device.

### 4.7 Replace string-id fields with `SoundCue` fields (system/data sounds)

| File | Old | New |
|---|---|---|
| `Bosses/BossAttack.cs` | `sfxId` | `SoundCue sound` |
| `Bosses/BossDefinition.cs` (`BossPhase`) | `transitionSfxId` | handled by boss `SoundEmitter ▸ OnPhaseChange` |
| `Bosses/BossController.cs` | `roarSfxId` | boss `SoundEmitter ▸ OnActivate` |
| `Items/WeaponDefinition` / `BaseShot` | hardcoded charge ids in `WeaponUser` | `fireSound`, `chargeLoop`, `impactSound` cues on the weapon asset; `ShotProjectile`/`ShotBeam` play them (beam = loop) |
| `Run/SectionClearTracker.cs` | `clearSfxId` | `SoundCue clearSound` |
| `Economy/Shop/ShopManager.cs` | `denySfxId`, `rerollSfxId`, hardcoded buy | `denySound`, `rerollSound`, `buySound` |
| `Hub/HubLootContainer`, `AnvilInteractable`, `MirrorInteractable` | `openSfxId`, `lootSfxId`, `useSfxId` | `SoundCue`s |
| `Items/ItemPickup.cs` | hardcoded | `SoundCue pickupSound` |
| `Ui/PassiveDropCinematicSettings.cs` | `landSfxId`, `continueSfxId` | `SoundCue`s |
| `Combat/Health.cs`, `PlayerMovement`, `PlayerAttack`, `RangedAttack`, `EnemyTuning` | `*SfxId` | superseded by `SoundEmitter`; fields kept one release as fallback, then removed (→ `Legacy` per convention) |

Music stays on ids + `Resources/Music/<id>` — that convention already works.

### 4.8 Tooling (build the tool, not the instance)

`Assets/Audio/Editor/AudioSetupTool.cs` — `Tools ▸ RedMagic ▸ Audio ▸ …`, idempotent:
1. **Crear prefab AudioManager** from MainMenu's instance → `Resources/AudioManager.prefab`.
2. **Quitar AudioManager de las escenas** — removes the 14 scene copies (they're identical; nothing lost).
3. **Migrar ids** — for every field in 4.7 and Player.prefab's `Health/PlayerMovement/PlayerAttack`
   ids, resolves the id through the registry and writes the clip into the new cue / a `SoundEmitter`
   entry, then clears the id (so nothing plays twice).
4. **Auditar sonido** — lists every `SoundCue`/`SoundEmitter` entry with no clip, per prefab/asset.

`EnemyFactory` + `ContentAudit` stamp an empty `SoundEmitter` on every enemy/boss, so "give this
enemy a death sound" is always: open prefab → `SoundEmitter` → `+` → `OnDeath` → drag clips.
Doc: `Assets/_Pipeline/AUDIO_PIPELINE.md` + a short CLAUDE.md section.

### 4.9 Rewiring required

| What | Action | Manual? |
|---|---|---|
| 14 scenes' `AudioManager` GameObjects | removed by tool step 2 | no |
| Player.prefab ids (`SFX_PlayerJump/Footstep/Dash/Stomp/Attack/Hurt/Death`) | migrated by tool step 3 into a `SoundEmitter` | no |
| System components in 4.7 | migrated by tool step 3 (most resolve to "no clip" → empty cue) | no |
| Existing `SoundEmitter` usages | **none exist** — nothing to break | — |
| `Fire1 1.unity`, `Assets/_Recovery/*.unity` | recommend deleting (junk copies) | your call |
| `sfx_gen.py` | move out of `Assets/` (e.g. `Tools/`) | trivial |
| New clips in `Assets/Audio/Sounds/` | drag into the new slots (footstep ×3 as variants) | yes — the actual content work |

### 4.10 Order of work

1. `SoundCue` + drawer, `AudioManager.Play(cue)` with variants/no-repeat/priority/anti-stacking,
   prefab bootstrap + listener ownership, SFX import postprocessor. *(unblocks adding clips)*
2. `SoundEmitter` rework + auto-binding + pool-safe OnSpawn; `EnemyFactory`/`ContentAudit` stamping;
   migration tool; Player.prefab migrated.
3. Loops + pause handling; weapon cues (fire/charge/impact/beam).
4. `UiSounds.Bind` + remove hardcoded UI calls; system cues (shop, hub, clear, pickup, cinematic).
5. Positional attenuation/pan; DSP buffer device test; pipeline doc.

---

## 5. Outcome (phases 1–4) and known tech debt

The refactor is complete: string-id API, id table, `SoundData` and every hidden TEMPORAL field are gone;
sounds live in `SoundCue` slots (see the Audio section of `CLAUDE.md`). Pending/placeholder sounds:
`docs/audio-pending.md`. Migration history: `docs/audio-migration-log.md`.

**Known tech debt, kept on purpose — the one string-keyed audio path.** Scene music is loaded by file
name from `Resources/Music/` through `AudioManager.PlaySceneMusic(name)`, with names built by
`RunManager`: `hubMusicId` (`MainHub`), `shopMusicId` (`Shop`), `World{n}-{k}` for sections and
`BossBattle{n}` for bosses. A rename breaks it silently (the scene just has no music). Replacing it
means moving music onto `WorldDefinition` / `ShopConfig` as `AudioClip` fields.
