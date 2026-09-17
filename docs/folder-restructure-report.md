# Assets/ folder restructure — execution report

Executed against a clean working tree with a safety-checkpoint commit already in place
(`c6d2aab Safe Before Structure change`). Every move used `AssetDatabase.MoveAsset` (GUIDs
preserved) via two one-time migration scripts left in the project as a record:
`Assets/Editor/VendorCleanupMigration.cs` (prior session) and `Assets/Editor/
FolderRestructureMigration.cs` (this pass). No filesystem-level moves.

## Step 1 — dead basic-prefab step removed, not just relocated

`Assets/Editor/EnemyImporter.cs`'s `BuildEnemyFromFolderPath` no longer builds or saves a
sprite-only enemy prefab (`BuildEnemyPrefab` method deleted entirely, along with its call site,
the now-dead `ENEMY_PREFAB_FOLDER` constant, and the now-pointless `projectileTriggers` dict that
only fed it). It still builds animation clips, the `AnimatorController`, the `SpriteSheetRecipe`
("de compatibilidad"), and projectile prefabs (those ARE wired into `EnemyProjectileSpawner`,
confirmed still live — kept). Verified live: re-running the sprite-import step against the
existing `Turtle` fixture produced `Turtle.sheet.asset` correctly and wrote **no** prefab to
`Assets/Prefabs/Enemies/Turtle.prefab` (`noBasicPrefabWritten=True`).

`CombinedBundleImporter.cs` verified unaffected — it never read the basic prefab back (confirmed
before this pass), and a live re-run of its sprite-import call produced the same correct result.

## Before → after path table

| Before | After | Notes |
|---|---|---|
| `Assets/Prefabs/` (whole folder — basic sprite-only prefabs + test junk: `ElTestDEOgro`, `OgroSpriteTest`, `RegressionFixEnemy`, `TestBundleEnemy`, etc., plus `Prefabs/Projectiles/Rock.prefab`) | **deleted** (`MoveAssetToTrash`, OS-recoverable) | Confirmed fully dead before deletion — nothing downstream read it |
| `Assets/Prefab/` (singular) | `Assets/Prefabs/` (plural, now free) | Whole-tree rename — carries `Enemies/`, `Fx/`, `Eviroment/`, `Maps/` (see split below), `Player.prefab` |
| `Assets/Enemies/<name>/` | `Assets/Art/EnemyImports/<name>/` | Rename only, internals untouched |
| `Assets/Projectiles/` (top-level, `Rock/` anim+controller) | `Assets/Prefabs/Projectiles/` | Folded into the new `Prefabs/` root — same folder `EnemyImporter.cs`'s projectile-prefab step already writes to |
| `Assets/Ui/Art/*` (2 loose files: `MainMenu.jpeg`, `necrotic_bone_shrine_03.png`) | `Assets/Art/UI/*` | **File-level merge, not a folder rename** — `Assets/Art/UI/` already existed (the UI-art-kit pipeline's own output: `ItemIcons/`, `ItemsUi/`, `Menus/`); moved the 2 loose files in individually, then trashed the now-empty `Ui/Art/` |
| `Assets/Icon/` | `Assets/Art/Icons/` | Rename |
| `Assets/Sounds/*.wav` (6 files) | `Assets/Audio/Sfx/` | Rename — confirmed NOT `Resources.Load`'d |
| `Assets/Data/Worlds/` | `Assets/ScriptableObjects/Worlds/` | Rename; `Assets/Data/` was empty afterward, trashed |
| `Assets/Prefab/Maps/Bg.prefab` | `Assets/Art/Environments/Bg.prefab` | The one prefab in `Maps/` with **no** `Collisions` child — a standalone piece, not a composed map |
| `Assets/Prefab/Maps/{Border, frame_005, frame_007, "frame_007 1", "map (1)", map, map2}.prefab` + the nested `maps/{map, "map (1)"}.prefab` (+ their `.json`) | `Assets/Prefabs/Maps/` (same names) | **All 8 of these DO have a `Collisions` child** (checked live, not by filename — my filename-based guess going in was wrong) — classified as composed maps and rode along with the `Prefab → Prefabs` rename unchanged |

### Deliberately NOT moved (and why)

| Item | Why not |
|---|---|
| `CurrencyConfig`, `ShopConfig`, `UpgradeTree`, `SynergyConfig`, `PlayerScaleConfig` (loose in `Resources/`) | Your own instruction's qualifier — "only the ones §3 confirmed are NOT Resources.Load-dependent" — and the audit shows **all five** are loaded by exact string (`Resources.Load("CurrencyConfig")` etc., 5 separate call sites). Moving them breaks those calls; that would require editing runtime game code, which wasn't authorized in this pass. |
| Every `*PanelSettings`/`*Skin` asset in `Resources/` | Same reason — all 11 are `Resources.Load`'d by filename. |
| `Resources/Music/*.mp3` | Not just "load-bearing by string" like the configs above — **technically cannot move at all** under the stated plan. `Resources.Load` only works for assets under a folder literally named `Resources` anywhere in the path; `Audio/Music/` (the requested destination) isn't one, so no amount of updating each scene's `AudioManager.sounds` id strings first would make it loadable there. Moving these would require changing `AudioManager.cs` to stop using `Resources.Load` — a runtime-code change, out of scope here. Left in place. |
| `Resources/Bosses/` (134 files, confirmed safe to move) | Never named in your move list — left alone; scope-matched to what was actually requested. |
| `Assets/Art/Map/` (loose raw PNG reference sheets: `Bg.png`, `Border.png`, `Bosque-Boss.png`, `frame_000-007.png`) | Never named in your move list, and not part of any "Map Tracer piece output" — it's the pipeline's raw source art, separate from the new `Art/Environments/` (piece prefabs). Left alone; flagged here since the two folders now sit side by side and look related. |
| `Assets/_Recovery/`, `Assets/Scripts/`, `Assets/Settings/`, `Assets/Editor/`, `Assets/_Pipeline/`, `Assets/Scenes/` | Explicitly out of scope per your instructions. |

## Code path constants updated (audit §4 + a few the audit's table didn't list)

| Constant | File | New value |
|---|---|---|
| `EnemyRecipe.ResolvedFolder` default | `Scripts/Pipeline/EnemyRecipe.cs` | `Assets/Prefabs/Enemies` |
| `ENEMY_PREFAB_FOLDER` | `Editor/EnemyImporter.cs` | **removed** (Step 1) |
| `PROJECTILE_PREFAB_FOLDER` / `PROJECTILE_ASSET_FOLDER` | `Editor/EnemyImporter.cs` | both `Assets/Prefabs/Projectiles` (consolidated — previously two separate top-level folders) |
| `enemyRoot` | `Editor/EnemyImporter.cs`, `Scripts/Pipeline/Editor/ConfigImport/CombinedBundleImporter.cs` | `Assets/Art/EnemyImports/{enemyName}` |
| `folder` (per-enemy projectile FX) | `Scripts/Pipeline/Editor/EnemyFactory.cs` | `Assets/Prefabs/Fx/Enemies/{recipe.enemyName}` |
| `ENEMY_PIPELINE_FOLDER`, `ENEMY_IMPORTER_FOLDER`, `PLAYER_PREFAB_PATH` | `Editor/WebLibraryWindow.cs` | `Assets/Prefabs/Enemies`, `Assets/Art/EnemyImports`, `Assets/Prefabs/Player.prefab` (`ART_CHARACTERS_FOLDER`/`BOSS_DEFINITION_FOLDER`/`SCENES_FOLDER` unchanged — not moved) |
| `PrefabFolder` (8 boss packs: `BeetleQueenPack`, `BossStarterPack`, `CursedWellPack`, `ScarecrowBossPack`, `StoneGuardianPack`, `TreeBossPack`, `UnholySealPack`, + `FxPlaceholderPack.BossPrefabFolder`) | `Scripts/Bosses/Editor/*.cs`, `Scripts/Fx/Editor/FxPlaceholderPack.cs` | `Assets/Prefabs/Enemies` — found via a fresh grep beyond the audit's named examples |
| `PrefabFolder`, `BossFxFolder`-adjacent Fx folders | `FxPlaceholderPack.cs`, `ArbolOrbePack.cs`, `TreeBossPack.cs`, `OrbeSheetSlicer.cs` | `Assets/Prefabs/Fx...` — same "found beyond the audit" category |
| `PrefabFolder` (Hub props) | `Scripts/Hub/Editor/MainHubItemsPack.cs` | `Assets/Prefabs/Eviroment/` |
| `PlayerPrefab` | `Scripts/Pipeline/Editor/PlayerPack.cs` | `Assets/Prefabs/Player.prefab` |
| `_dataRoot` | `Scripts/Run/Editor/WorldSceneGenerator.cs` | `Assets/ScriptableObjects/Worlds` |
| `libraryArtPath()` | `Web/RedMagicWeb/modules/enemy-export.js` | **Not in the audit's table at all** (it's JS, not C#) — the web app computes this same convention string client-side; left stale it would still be inert (Unity's `CombinedBundleImporter` overwrites it with the real path) but was fixed to `Assets/Art/EnemyImports/...` for accuracy |
| `CollisionImporter.cs` dialog-start folders | unchanged | Confirmed not a real constraint (audit already established this) |

## Docs updated

- `Web/RedMagicWeb/ARCHITECTURE.md` — the `libraryArtPath()` convention-path mention.
- `docs/web-tools-guide.md` — rewrote the "Build Enemy From Folder" section (no longer produces a
  basic prefab; explains why), every literal path in the Import Config/Biblioteca sections, and
  replaced the stale "two sibling folders" explanation (which no longer describes reality — both
  names now resolve to the same path) with a historical note plus the current, single-folder fact.
- `docs/schemas/enemy-config.schema.json` — `prefabFolder`'s `default` and `$comment`; synced the
  byte-identical vendored copy at `Web/RedMagicWeb/schemas/enemy-config.schema.json`.
- `docs/schemas/COMPATIBILITY.md` — no literal `Assets/...` paths in it; nothing to change.

## Verification performed

- Clean recompile, zero console errors, at every stage (before Step 1 edits, after Step 1, after
  the folder-move script, after every path-constant edit, at the end).
- `Tools ▸ Web ▸ Biblioteca`'s scanners invoked directly (reflection over the live Editor):
  `Enemies=12`, `Scenes=7`, `Maps=9` — matches expected counts from the new locations.
- Real end-to-end pipeline test: `EnemyFactory.Generate` re-run on the existing `Abeja` recipe —
  resolved `Assets/Prefabs/Enemies` correctly, updated `Enemy_Abeja.prefab` in place (idempotent,
  hand-tuned `EnemyStats` preserved), regenerated its mirrored projectile prefab under the new
  `Assets/Prefabs/Fx/Enemies/Abeja/` path, zero errors.
- Real combined-bundle-path test: re-ran `EnemyImporter.BuildEnemyFromFolderPath` against the
  existing `Turtle` fixture (already has `manifest.json` + `enemy-config.json` from a prior
  session) at its new `Assets/Art/EnemyImports/Turtle` location — produced the `SpriteSheetRecipe`
  correctly and confirmed no basic prefab was written, proving Step 1's removal doesn't break this
  path. No duplicate/orphaned files were created by the re-run (checked the folder listing).
- `GoldChest.prefab` opened at its new path (`Assets/Prefabs/Eviroment/GoldChest.prefab`) and
  inspected live: `Chest` component resolves (moved+cleaned script, GUID preserved), sprite is
  still `MainHubItems_ChestOpening_00` (your own art, untouched), Animator controller is still
  `Chest` (untouched) — confirmed unaffected by this pass.

## One thing to flag

`Assets/Prefab/Maps/` turned out to contain far more "composed map" prefabs (8, all carrying a
`Collisions` child) than expected going in — I'd assumed most of the loose-looking ones
(`Border.prefab`, `frame_005.prefab`, `frame_007.prefab`, `"frame_007 1.prefab"`) were standalone
art pieces by their filenames. The migration script checked live instead of trusting that guess, so
only `Bg.prefab` (genuinely no `Collisions` child) went to `Art/Environments/` — the rest rode along
into `Prefabs/Maps/` unchanged. Worth a look if any of those were actually meant to be piece art
rather than full maps; nothing was lost, they're just not where I originally expected to put them.
