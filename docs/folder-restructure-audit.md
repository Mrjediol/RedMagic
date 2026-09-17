# Assets/ folder restructure — audit (read-only)

No files were moved, renamed, or deleted to produce this report. The move plan is a separate,
follow-up deliverable — this is reference material for that planning, not a proposal.

## 1. Full current Assets/ tree

One level deep, real contents (not folder-name guesses):

| Folder | Actual contents |
|---|---|
| `Adaptive Performance/` | 3 files — URP Adaptive Performance provider settings assets. Unity-package config, not project content. |
| `Art/` | `Art/Characters/<Name>/` per character — pipeline-cut sprite sheets (`.sheet.asset`, `.enemy.asset`, sliced PNGs, `.controller`). 237 files across ~16 character subfolders (enemies, bosses, Player, MainHubItems, plus test junk like `OgrooTest2`). |
| `Audio/` | `AudioManager.cs`, `SoundData.cs`, `SoundEmitter.cs`, an `Editor/` subfolder, and the URP `GameAudioMixer.mixer`. Code + one asset, not audio clips themselves (those are in `Sounds/`/`Resources/Music/`). |
| `Brackeys/` | Vendored "2D Mega Pack" — 305 files: characters, enemies, environment, platforms, a demo scene, example photos. Third-party. |
| `Cainos/` | Vendored "Pixel Art Platformer - Village Props" + a ground tileset + a "Lucid Editor" attribute library (`Third Party/`) — 423 files. Third-party; also the only vendor pack with live, non-editor **scripts** (`Chest.cs`, `Elevator.cs`, etc. — see §2). |
| `Data/` | 3 files — `Data/Worlds/World{1,2,3}.asset` (`WorldDefinition` assets). Small, load-bearing run config. |
| `Dragon Warrior Files/` | 99 files. **Misleadingly named** — not just "Dragon" character source art. Also hosts generic one-shot VFX source assets (`Effects/fireball_01.png`, `dashwind_01/02.png`, `explosion_01.png` + their `.controller`/`.anim`) that 4 currently-shipped `Assets/Prefab/Fx/*.prefab` still depend on (see §2). Third-party/vendored despite the folder name not signaling that. |
| `Editor/` | 4 files — the hand-written (non-generated) Unity-side importers: `CollisionImporter.cs`, `EnemyImporter.cs`, `WebLibraryWindow.cs`, plus `MapTracer.html` (oddly co-located; it's the *source* of the standalone web tool, not a Unity asset). |
| `Enemies/` | `Enemies/<Name>/` per enemy imported via `EnemyImporter.cs`/`CombinedBundleImporter` — `Animations/*.anim`, two near-duplicate `.controller` files, `.sheet.asset`, representative PNGs, `enemy-config.json`. 222 files, ~8 enemy subfolders. |
| `Icon/` | 9 loose images — item icons (Boots/Cape/Diamond/Gold/Helmet/Ring/Skull/SoulFragment/Staff). |
| `Prefab/` (singular) | `Prefab/Enemies/` — the **real, gameplay-ready** `Enemy_<Name>.prefab`/`Boss_<Name>.prefab` set (19 files). Also has sibling subfolders not sampled above (`Prefab/Fx/`, `Prefab/Eviroment/`, `Prefab/Maps/`, `Prefab/Player.prefab`) per other sections below. |
| `Prefabs/` (plural) | `Prefabs/Enemies/` (13 files, sprite-only intermediate prefabs — see §5) + `Prefabs/Projectiles/`. Sibling of `Prefab/` by a one-letter folder-name typo/drift, not a deliberate second convention. |
| `Projectiles/` | 2 files — `Projectiles/Rock/` (anim + controller). Sparse; most projectile visuals are code-built or under `Prefab/Fx/`. |
| `Resources/` | See §3 — configs, `PanelSettings` assets, `Bosses/` (`BossDefinition` + `BossAttack_*`), `Items/`, `Legacy/Abilities/`, `Music/`, an empty `Abilities/`. |
| `Scenes/` | Only **7** real `.unity` files exist (`MainMenu`, `MainHub`, `TestScene`, `World1/{TestBosque1, testboss, World1_Boss, World1_Section01}`) — see the Build Settings note below §1. |
| `Scripts/` | 204 files — the actual game/editor C# source tree (`Bosses/`, `Enemies/`, `Pipeline/`, `Items/`, `Run/`, `Economy/`, `Hub/`, `Legacy/`, etc.). |
| `Settings/` | 9 files — URP/render pipeline settings, input actions, a scene template. Unity project config, not gameplay content. |
| `Sounds/` | 6 loose `.wav` files (jump SFX, enemy damage, two "Sly 2" music rips) — separate from `Resources/Music/`'s `.mp3` set; not Resources-loaded (see §3). |
| `Sprites/` | 39 loose source images — the RAW reference sheets fed into the sprite pipeline (`abeja-distancia-statica-aire.jpg`, map backgrounds, AI-generated concept art) — pre-slice input, not runtime-consumed sprites. |
| `Ui/` | 54 files — UI Toolkit C# controllers + `Ui/Art/` (loose reference images) + `Ui/ItemsUi/` (source art for the items-screen skin). |
| `_Pipeline/` | 3 markdown docs (`SPRITE_PIPELINE.md`, `ITEMS_PIPELINE.md`, `UI_ART_PIPELINE.md`). Docs, not assets — leading underscore keeps it sorted to the top in the Project window. |
| `_Recovery/` | 3 `.unity` files (`0.unity`, `0 (1).unity`, `0 (2).unity`) — **not in Build Settings** (confirmed against `ProjectSettings/EditorBuildSettings.asset`), i.e. Unity crash-recovery autosaves, not shipped content. |

**Build Settings anomaly (found incidentally, flagged since it affects any Scenes/-folder
decision):** `ProjectSettings/EditorBuildSettings.asset` lists ~23 scene paths, including
`World1_Section02` through `World1_Section15` and all of `World2`/`World3`'s boss/section scenes —
**none of these exist on disk**. Only `World1_Boss.unity` and `World1_Section01.unity` are real
files under `Scenes/Worlds/World1/`. Build Settings is stale/dangling for the large majority of its
entries. Not something this audit was asked to fix, but relevant: a restructure that reorganizes
`Scenes/` should not assume Build Settings is an accurate map of what exists.

## 2. Third-party pack sprite/script usage (Brackeys, Cainos, Dragon Warrior Files)

Method: extracted every `guid:` this project's own (non-vendor) `.prefab`/`.asset`/`.unity`/
`.controller`/`.anim`/`.mat` files reference, intersected against every guid declared in the three
vendor packs' own `.meta` files. 40 distinct vendor guids are actually referenced somewhere outside
their own pack folder.

### (a) Sprites/assets in use — copy these out before deleting any vendor folder

| Source file | Referenced by |
|---|---|
| `Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png` | 4 early boss prefabs + 15 `BossAttack_*.asset` + 3 non-boss prop prefabs (see (b)) |
| `Cainos/Pixel Art Platformer - Village Props/Material/MT Village Props - Default.mat` | material for the texture above |
| `Cainos/.../Texture/TX Tileset Ground.png` + 27 `TX Tileset Ground_N.asset` RuleTile sub-tiles | **only** `Prefab/Maps/Base.prefab` (orphaned — see below) |
| `Cainos/.../Prefab/PF Village Props - Gravestone 01.prefab` | only `Prefab/Maps/Base.prefab` |
| `Cainos/.../Prefab/PF Village Props - Well.prefab` | only `Prefab/Maps/Base.prefab` |
| `Cainos/Pixel Art Platformer - Village Props/Script/Chest.cs` | **live, enabled** `MonoBehaviour` on `Prefab/Eviroment/GoldChest.prefab` |
| `Brackeys/2D Mega Pack/Enemies/Insects/GiantBeetle.png` | `Prefab/Enemies/Boss_ReinaEscarabajo.prefab` |
| `Brackeys/2D Mega Pack/Environment/Gothic/Pentagram_Activated.png` | `Prefab/Enemies/Boss_SelloProfano.prefab` |
| `Brackeys/2D Mega Pack/Enemies/Crow.png` | `Resources/Bosses/BossAttack_BandadaDeCuervos.asset`, `BossAttack_NocheDeCuervos.asset` (crow-shaped projectile sprite — no `Enemy_Cuervo.prefab` currently exists on disk despite being named in `ScarecrowBossPack.cs`) |
| `Brackeys/2D Mega Pack/Backgrounds/SkyBackground.png` | only `Prefab/Maps/Base.prefab` (+ non-shipped `_Recovery/0.unity`) |
| `Dragon Warrior Files/Effects/fireball_01.png` + `Animations/VFX_Fireball.controller` + `.anim` | `Prefab/Fx/Fireball.prefab` |
| `Dragon Warrior Files/Effects/dashwind_01.png` + `Animations/VFX_DashWind.controller` + `.anim` | `Prefab/Fx/VFX_DashWind.prefab` |
| `Dragon Warrior Files/Effects/dashwind_02.png` | `Prefab/Fx/VFX_DoubleJump.prefab` |
| `Dragon Warrior Files/Effects/explosion_01.png` + `Animations/VFX_Explosion.controller` + `.anim` | `Prefab/Fx/VFX_Explosion.prefab` |

**Generation-time-only dependency (no committed asset embeds this, but re-running the tool needs
it):** `Scripts/Pipeline/Editor/PlayerPack.cs:38,170` — `AssetDatabase.CopyAsset("Assets/Dragon
Warrior Files/Animations/DragonWarrior.controller", ControllerPath)`. The live
`Art/Characters/Player/Player.controller` does **not** itself reference the vendor file's guid
(confirmed — zero hits outside `Dragon Warrior Files/`), but the next time someone re-runs
`Tools ▸ RedMagic ▸ Pipeline ▸ Packs ▸ Player`, this line fails loudly if the source file moved.

### (b) Confirmed: NOT exclusive to early bosses

`Cainos/.../TX Village Props.png` is also referenced by **`Prefab/Eviroment/Shop.prefab`,
`Prefab/Eviroment/Target.prefab`, `Prefab/Eviroment/WeaponUpgrade.prefab`** — live, shipped hub/run
props, not boss-only. This is the one genuine "outside the early bosses" dependency on shared,
currently-used content.

Everything else that reaches beyond bosses (`TX Tileset Ground.png`, the 27 RuleTile sub-assets,
the two `PF Village Props` decoration prefabs, `SkyBackground.png`) traces to exactly one
consumer: **`Prefab/Maps/Base.prefab`**. That prefab is not instantiated by any scene or other
prefab (zero guid references to it found anywhere), has no `Collisions` child (so it's also invisible
to `WebLibraryWindow.cs`'s Map-detection heuristic), and its whole containing folder
(`Prefab/Maps/`, including a `maps/` subfolder with loose `.json` files) matches this session's own
earlier Map Tracer test exports — i.e. it reads as scratch/test output, not shipped content, though
this audit did not get explicit confirmation of that from you.

### (c) Non-editor vendor scripts actually used

Checked all non-`Editor/` `.cs` files under the three packs. Only one is live: **`Chest.cs`**
(`Cainos/Pixel Art Platformer - Village Props/Script/Chest.cs`), attached and enabled on
`Prefab/Eviroment/GoldChest.prefab` — see (a). `Elevator.cs`, `BoundingPlatform.cs`,
`MovingPlatform.cs` (also Cainos) and `SecondOrderDynamics.cs` (Cainos `Common/Script/Utils/`) have
zero references anywhere outside their own pack folder.

## 3. Resources/ folder audit

**Real contents**: `Abilities/` (empty — 0 files), `Bosses/` (134 files: `Boss_*.asset` +
`BossAttack_*.asset`), `Items/` (`Item_*.asset` loose + `Modifiers/` + `Weapons/` subfolders),
`Legacy/Abilities/` (`Ability_*.asset`), `Music/` (5 `.mp3`), plus loose top-level: `CurrencyConfig`,
`ShopConfig`, `SynergyConfig`, `PlayerScaleConfig`, `UpgradeTree` (configs), and ~10 `*PanelSettings`/
`*Skin` UI Toolkit assets.

**Every `Resources.Load`/`LoadAll` call found**, with its exact string:

| Loads | Call site |
|---|---|
| `"CurrencyConfig"` | `Scripts/Economy/CurrencyManager.cs:86` |
| `"ShopConfig"` | `Scripts/Economy/ShopInteractable.cs:139` |
| `"UpgradeTree"` | `Scripts/Economy/UpgradeManager.cs:58` |
| `"SynergyConfig"` | `Scripts/Items/SynergyConfig.cs:56` |
| `"PlayerScaleConfig"` | `Scripts/Run/RunManager.cs:851` |
| `"InteractionPromptPanelSettings"` | `Scripts/Hub/InteractionPromptUi.cs:62`, `Scripts/Hub/RewardPopupUi.cs:59`, `Ui/ControlsLegendHud.cs:70` |
| `"AbilityMenuPanelSettings"` | `Scripts/Legacy/Ui/AbilityMenuController.cs:62` |
| `"WeaponUpgradeMenuPanelSettings"` | `Scripts/Legacy/WeaponLevels/WeaponUpgradeMenuController.cs:60` |
| `"CurrencyHudPanelSettings"` | `Ui/CurrencyHud.cs:55` |
| `"ItemMenuPanelSettings"` | `Ui/ItemMenuController.cs:124` |
| `"MirrorMenuPanelSettings"` | `Ui/MirrorMenuController.cs:54` |
| `"ShopMenuPanelSettings"` | `Ui/ShopMenuController.cs:73` |
| `"UpgradeMenuPanelSettings"` | `Ui/UpgradeMenuController.cs:86` |
| `"WeaponForgeMenuPanelSettings"` | `Ui/WeaponForgeMenuController.cs:63` |
| `"ItemMenuSkin"` | `Ui/ItemMenuSkin.cs:83` |
| `"MenuSkin"` | `Ui/MenuSkin.cs:95` |
| `"Items"` (whole folder, `LoadAll<T>`) | `Scripts/Items/ItemLibrary.cs:32`, `Scripts/Items/WeaponLibrary.cs:28`, `Ui/ItemMenuController.cs:133-137` (5 separate `LoadAll` calls, all rooted at `"Items"`) |
| `"Legacy/Abilities"` (whole folder, `LoadAll`) | `Scripts/Legacy/Abilities/AbilityLibrary.cs:27` |
| `"Music/<id>"` (default folder, semi-hardcoded) | `Audio/AudioManager.cs:214,216` — folder name comes from `[SerializeField] musicResourceFolder = "Music"` (`AudioManager.cs:51`), a serialized default that could be overridden per-scene in the Inspector, not a `const` |

**Flagged as NOT Resources.Load'd anywhere — safe to move/rename freely without touching code**:

- `Resources/Bosses/` (all 134 files — `BossDefinition`/`BossAttack_*` are referenced by direct
  serialized object reference from elsewhere, e.g. `WorldDefinition`, never by string path; the
  `Assets/Resources/Bosses/` location is a documented *convention*, not a load-path requirement —
  confirmed via `BossDefinition.cs`'s own doc comment and zero `Resources.Load` hits mentioning Boss).
- `Resources/Abilities/` — empty, nothing to move.
- Individual filenames inside `Resources/Items/` and `Resources/Legacy/Abilities/` — the *folder*
  position is load-bearing (`LoadAll` roots), but files can be renamed/reorganized into subfolders
  within that root freely since `LoadAll<T>` filters by C# type, not filename.
- `Resources/Music/*.mp3` **filenames** are load-bearing (looked up by `id` matching each scene's
  `AudioManager.sounds` list), but this isn't a single-file grep-able constant — it's per-scene
  serialized data. Treat renaming any `Music/*.mp3` file as requiring an `AudioManager` sound-list
  audit across every scene, not a single code-line fix.

## 4. Hardcoded path conventions in this project's own tooling

| Path/pattern | file:line | What breaks if moved |
|---|---|---|
| `Assets/Prefab/Enemies` (default) | `Scripts/Pipeline/EnemyRecipe.cs:66` (`ResolvedFolder`) | Every pipeline-generated enemy/boss prefab's save location |
| `Enemy_{enemyName}.prefab` | `Scripts/Pipeline/EnemyRecipe.cs:70` (`PrefabPath`) | Prefab naming convention itself |
| `Assets/Art/Characters/{characterName}` (default) | `Scripts/Pipeline/SpriteSheetRecipe.cs:259` (`ResolvedFolder`) | Every pipeline-cut sheet's sprite/controller/recipe location |
| `Assets/Prefabs/Enemies` | `Editor/EnemyImporter.cs:65` (`ENEMY_PREFAB_FOLDER`) | "Build Enemy From Folder"'s sprite-only prefab output |
| `Assets/Prefabs/Projectiles` | `Editor/EnemyImporter.cs:66` (`PROJECTILE_PREFAB_FOLDER`) | Same importer's projectile prefabs |
| `Assets/Projectiles` | `Editor/EnemyImporter.cs:67` (`PROJECTILE_ASSET_FOLDER`) | Same importer's projectile anim/controller |
| `Assets/Enemies/{enemyName}` | `Editor/EnemyImporter.cs:217` (`enemyRoot`) | Sprite/anim/controller/config-json tree for both the standalone and combined-bundle import paths |
| `Assets/Prefab/Fx/Enemies/{recipe.enemyName}` | `Scripts/Pipeline/Editor/EnemyFactory.cs:288` | Generated per-enemy projectile FX prefab folder |
| `Assets/Prefab/Enemies` | `Editor/WebLibraryWindow.cs:65` (`ENEMY_PIPELINE_FOLDER`) | Biblioteca's Enemies grid source |
| `Assets/Art/Characters` | `Editor/WebLibraryWindow.cs:66` (`ART_CHARACTERS_FOLDER`) | Biblioteca's per-enemy delete-path check |
| `Assets/Enemies` | `Editor/WebLibraryWindow.cs:67` (`ENEMY_IMPORTER_FOLDER`) | Biblioteca's per-enemy delete-path check |
| `Assets/Resources/Bosses` | `Editor/WebLibraryWindow.cs:68` (`BOSS_DEFINITION_FOLDER`) | Biblioteca's Bosses grid source |
| `Assets/Prefab/Player.prefab` | `Editor/WebLibraryWindow.cs:69` (`PLAYER_PREFAB_PATH`) | Biblioteca's Player card |
| `Assets/Scenes` | `Editor/WebLibraryWindow.cs:70` (`SCENES_FOLDER`) | Biblioteca's Escenas grid source |
| `Assets/Resources/Bosses` | `Scripts/Bosses/Editor/BossStarterPack.cs:20`, `ScarecrowBossPack.cs:31` (`BossFolder`) | Where each boss pack writes its `BossDefinition`/`BossAttack_*` assets |
| `Assets/Prefab/Enemies` | `BossStarterPack.cs:21`, `ScarecrowBossPack.cs:32` (`PrefabFolder`) | Where each boss pack writes its prefab |
| `Assets/Scenes/Worlds/World1/World1_Boss.unity` | `BossStarterPack.cs:28` (`BossScenePath`) | "Colocar jefe en escena" menu target |
| `Assets/Scenes/Worlds/World2/World2_Boss.unity` | `ScarecrowBossPack.cs:42` (`BossScenePath`) | Same, World 2 |
| `Assets/Brackeys/2D Mega Pack/Enemies/Crow.png` | `ScarecrowBossPack.cs:40` (`CrowTexture`) | Crow minion/projectile sprite source at generation time |
| `Assets/Sprites/player_sprite.jpeg` | `Scripts/Pipeline/Editor/PlayerPack.cs:34` (`Sheet`) | Player regen source sheet |
| `Assets/Art/Characters/Player` | `PlayerPack.cs:35` (`Folder`) | Player's cut-sheet output folder |
| `Assets/Dragon Warrior Files/Animations/DragonWarrior.controller` | `PlayerPack.cs:38` (`SourceController`) | Player regen's starting controller template (see §2) |
| `Assets/Prefab/Player.prefab` | `PlayerPack.cs:39` (`PlayerPrefab`) | The one player prefab's location |
| `Assets/Sprites/New folder` | `PlayerPack.cs:42` (`HandCutFolder`) | A hand-cut fallback sprite source — note the literal un-renamed `"New folder"` name |
| *(dialog start folder only, not a real constraint)* | `Editor/CollisionImporter.cs:29,45,251` | `SaveFilePanelInProject`/`OpenFolderPanel`/`SaveFolderPanel` all start at `"Assets"` or prompt the user — Map Tracer import paths are **not** hardcoded beyond that; safe w.r.t. restructure by design |

**`Assets/...` paths mentioned in the four docs** (grep of each file):

- `Web/RedMagicWeb/ARCHITECTURE.md`: `Assets/Editor/EnemyImporter.cs`, `Assets/Editor/CollisionImporter.cs`, `Assets/Editor/WebLibraryWindow.cs`, `Assets/Enemies/<enemyName>/<enemyName>.sheet.asset` (the convention `libraryArtPath()` assumes — noted in-doc as "inert if wrong, not a silent bug").
- `docs/web-tools-guide.md`: `Assets/Enemies/RawImport/...` (example), `Assets/Enemies/<Nombre>/...` (Animations/.controller/.sheet.asset), `Assets/Prefabs/Enemies/<Nombre>.prefab`, `Assets/Projectiles/<NombreProyectil>/...`, `Assets/Prefabs/Projectiles/<NombreProyectil>.prefab`, `Assets/Prefab/Enemies` (singular, contrasted explicitly against the plural), `Assets/Art/Characters/<Nombre>/`, `Assets/Resources/Bosses/`, `Assets/Prefab/Player.prefab`, `Assets/Scenes/`. This doc is the densest concentration of literal paths in prose form — a restructure invalidates most of its "Qué produce" sections verbatim.
- `docs/schemas/enemy-config.schema.json`: `Assets/Enemies/<enemyName>/<enemyName>.sheet.asset` (in `libraryArtPath`-adjacent commentary imported from ARCHITECTURE.md's convention), plus the `art` field's `pattern: "^Assets/"` and `prefabFolder`'s `pattern: "^Assets/"` / `default: "Assets/Prefab/Enemies"` — these are **schema-enforced** patterns, not just prose: any restructure that moves content out of `Assets/` (impossible in Unity anyway) or changes the default prefab folder needs this default updated too.
- `docs/schemas/COMPATIBILITY.md`: no literal `Assets/...` paths — it's about C# field-mapping, not folder locations.

## 5. The Prefab/Prefabs duplicate — confirmed

`Assets/Prefab/Enemies/` (singular) currently holds **19 files**: 7 `Boss_<Name>.prefab` + 12
`Enemy_<Name>.prefab` (Abeja, Butterfly, Caballo, Champi, Dragon, Dragoncito, Gorila, Lobo,
Murcielago, OGREEE, TreeWalk, Turtle).

`Assets/Prefabs/Enemies/` (plural) currently holds **13 files**, all bare-name, no prefix: Butterfly,
Dragon, Dragoncito, ElTestDEOgro, OGREE, OgreeDeFinitivo, OgroRock, OgroSpriteTest, OgrooTest2,
RegressionFixEnemy, TestBundleEnemy, Turtle, torguga — several are explicitly test/junk names
(`ElTestDEOgro`, `OgroSpriteTest`, `RegressionFixEnemy`, `TestBundleEnemy`).

Write sites, confirmed fresh: the **real, gameplay-ready** prefab
(`EnemyStats`/`EnemyBrain`/`EnemyAttack` and everything `EnemyFactory` stamps) is written by
`Scripts/Pipeline/Editor/EnemyFactory.cs:64` (`PrefabUtility.SaveAsPrefabAsset(root, path)`) where
`path = recipe.PrefabPath` resolves, per `EnemyRecipe.cs:65-70`, to
`Assets/Prefab/Enemies/Enemy_<Name>.prefab` (singular folder, prefixed). The **sprite-only
intermediate artifact** (no gameplay component at all) is written by
`Editor/EnemyImporter.cs:261` (`PrefabUtility.SaveAsPrefabAsset(enemyPrefabObj, enemyPrefabPath)`)
where `enemyPrefabPath = $"{ENEMY_PREFAB_FOLDER}/{enemyName}.prefab"` and `ENEMY_PREFAB_FOLDER =
"Assets/Prefabs/Enemies"` (plural folder, no prefix) — `EnemyImporter.cs:65`.

This matches `Editor/WebLibraryWindow.cs` exactly, re-checked fresh: `ScanEnemies()` (line ~236)
scans **only** `ENEMY_PIPELINE_FOLDER = "Assets/Prefab/Enemies"` (singular); the plural folder is
never referenced anywhere in that file — the Biblioteca window's Enemies category already treats
`Assets/Prefab/Enemies/` as the sole real/canonical location and `Assets/Prefabs/Enemies/` as an
out-of-scope intermediate artifact, consistent with the fix applied last session. For a restructure:
`Assets/Prefab/Enemies/` is the one to preserve/rename cleanly; `Assets/Prefabs/Enemies/` is
disposable (subject to whatever the follow-up move plan decides — this audit takes no position on
deleting it, only confirms what it is).
