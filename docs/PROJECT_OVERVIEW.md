# RedMagic — project overview

Bootstrap doc for a brand-new chat with zero prior context. RedMagic is a 2D mobile roguelite
sidescroller built in Unity 6000.3.23f1 (URP 2D Renderer), targeting Android in landscape. Core
loop: menu → hub (manage upgrades/build, pick a world) → a run through a world's sections → boss →
back to the hub (on death or completion). Most content (enemies, maps, bosses) is authored through
a companion web app (`Web/RedMagicWeb/`) and imported into Unity as JSON/zip bundles rather than
hand-built in the Editor — see the pointer table below.

For full architectural detail (pooling, combat, weapons/items, boss archetypes, the sprite
pipeline, etc.) read `CLAUDE.md` at the repo root — this doc is only the folder map and naming
rules needed to get oriented before diving into that.

## Folder tree (post-restructure, verified against the live working tree)

```
Assets/
  Art/                One folder per art category, not per pipeline step
    Characters/          Sliced enemy/boss sprite output (Pipeline)
    EnemyImports/         Raw web-exported enemy bundles, one folder per <Name>
    Environments/         Standalone map/background art pieces (e.g. Bg.prefab)
    Icons/                Item/UI icons
    Map/                  Raw source reference art for the Map Tracer pipeline
    Placeholder/          Generator-built placeholder FX shape sprites
    UI/                   UI-art-kit output (ItemIcons, ItemsUi, Menus)
  Audio/                Sfx/, Music references, AudioManager mixer assets
  Editor/               Top-level editor tools (EnemyImporter, WebLibraryWindow, …)
  Prefabs/              All runtime prefabs (was "Prefab", singular — see naming below)
    Enemies/               One prefab per enemy, name = <Name>
    Eviroment/              Hub/world props (chest, altar, shop, GroundSnap targets) — sic, kept as-is
    Fx/                     Pooled visual prefabs (Bosses/<Boss>/, Enemies/<Name>/, weapon shots)
    Maps/                   Composed map prefabs (have a Collisions child) + nested maps/
    Player.prefab
    Projectiles/            Non-enemy-specific projectile prefabs (e.g. Rock)
  Resources/             Resources.Load-dependent assets only (configs, PanelSettings, Music, Bosses/, Items/, Abilities/, Legacy/)
  Scenes/                MainHub, MainMenu, TestScene, Worlds/ (per-world section/boss scenes)
  Scripts/               All C# source, one folder per system (Bosses, Combat, Economy, Enemies,
                          Fx, Gameplay, Hub, Input, Items, Legacy, Pipeline, Run, ...)
  ScriptableObjects/     Data assets not forced into Resources/ (currently Worlds/: World1-3.asset)
  Settings/              URP/Input System project settings assets
  Sprites/               Misc loose sprites not yet folded into Art/
  Ui/                    UI Toolkit UXML/USS/PanelSettings + UI C# controllers
  _Pipeline/             Pipeline reference docs (SPRITE_PIPELINE.md, ITEMS_PIPELINE.md, UI_ART_PIPELINE.md)
  _Recovery/             Scratch/recovery scenes, not shipped content

Web/
  RedMagicWeb/           The companion web app (Sprites + Enemy Creator + Map Tracer tabs, etc.)
  legacy/                Superseded web app version, reference only

docs/                    This file, PIPELINE_STATUS.md, web-tools-guide.md, schemas/
```

Note: as of this writing the folder-restructure migration (`Assets/Prefab` → `Assets/Prefabs`,
`Assets/Enemies` → `Assets/Art/EnemyImports`, `Assets/Data/Worlds` → `Assets/ScriptableObjects/Worlds`,
etc.) is committed to the working tree but not yet committed to git — `git status` will show a large
uncommitted diff. Trust the working tree over any older doc or memory that references the pre-move
paths (`Assets/Prefab/`, `Assets/Enemies/`, `Assets/Data/`).

## Naming conventions

- **`<Name>` consistency**: an enemy or boss's identifier is the same string across every folder it
  touches — `Art/EnemyImports/<Name>/`, `Art/Characters/<Name>/`, `Prefabs/Enemies/<Name>.prefab`,
  `Prefabs/Fx/Enemies/<Name>/`. Tooling and packs assume this; don't rename in only one place.
- **`Prefab` → `Prefabs`**: the folder is `Assets/Prefabs/` (plural). Any code, doc, or memory still
  referencing singular `Assets/Prefab/` is describing the pre-restructure layout.
- **Individual map pieces vs. composed maps both live under `Prefabs/Maps/`** — there is no
  separate "pieces" folder. They're distinguished by usage, not location: a prefab with a
  `Collisions` child is a composed, placeable map; one without (currently only `Art/Environments/Bg.prefab`,
  which was reclassified out of `Prefabs/Maps/` for exactly this reason) is standalone background
  art meant to be a piece inside a composed map, not dropped into a scene on its own. When adding a
  new map prefab, check for a `Collisions` child to decide which folder it belongs in — don't guess
  from the filename.
- **`Eviroment` (not `Environment`)** is an intentional, kept misspelling from earlier content — new
  hub/world prop prefabs go there for consistency with what's already there.

## Read this next

| Doc | Read it when... |
|---|---|
| `Web/RedMagicWeb/ARCHITECTURE.md` | Working on the web app itself (Sprites/Enemy Creator/Map Tracer tabs, background removal, export format) |
| `docs/web-tools-guide.md` | Using or troubleshooting any **Tools ▸ Web** menu in the Unity Editor (the import side) |
| `docs/schemas/COMPATIBILITY.md` | Before changing any JSON schema field, or writing/modifying a config importer |
| `docs/PIPELINE_STATUS.md` | Checking which game system (enemies/maps/bosses/player attacks/world generation) is finished vs. in progress, before starting new pipeline work |
