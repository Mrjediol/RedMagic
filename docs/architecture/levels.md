# Levels: terrain, parallax background, biomes (`Assets/Scripts/Levels/`)

Replaces the old sketch-importer/kit system (tiled edge strips, `LevelKit`, old `ParallaxLayer`), now in
`Assets/Legacy/Scripts/Levels/`. Validated first in the web prototypes `Web/ParallaxTest/` (see
`docs/biomas-receta.md`); the Unity code ports the same algorithms and the same parameters.

## Scale
**1 unit = 1 cell = 40 px of the web sketches.** 1000 px = 25 units. Camera `orthographicSize` 10 (20 units of
screen height), as forced by `RunManager`.

## `Level` (one per level scene)
- `width` × `height` in units (Inspector shows the px equivalent). Height capped at `Level.MaxHeight` = 75
  (3000 px). Changing the size re-fits everything: the implicit 1-cell rock frame, `BG` (camera bounds read by
  `CameraFollow`), the collider and the parallax range. Painted cells are kept.
- **Platforms** = a second Tilemap (`Grid/Plataformas`, tile `Plataforma` in the same palette). Same look as rock, but
  each connected platform gets its own child under `Plataformas`: layer Platform, `PolygonCollider2D` (square corners,
  usedByEffector) + one-way `PlatformEffector2D`. One collider per platform because drop-through ignores the specific
  collider being stood on (`PlayerMovement.TryDropThroughPlatform`). Rock-only cells feed `Colision` (Ground).
- Terrain = a `Tilemap` child (`Grid/Terreno`, renderer disabled). Paint with the **`PaletaTerreno`** palette
  (`Assets/Levels/_Biomas/`): any tile = rock. The frame is NOT painted — cells on the border and outside are
  always rock (`Level.IsSolid`). Origin = bottom-left corner.
- Regenerates on paint (`Tilemap.tilemapTileChanged`), on size/biome change and on load; meshes are `DontSave`.
  Children it owns: `BG`, `Roca` (rock fill, Sprite-Lit, biome texture tiled in world space), `Brillo` (rock shadow +
  inner glow baked into a texture by exact distance-to-air at 8 texels/unit), `Borde` (4 colour bands of noisy
  width, dark outline (outer glow off: `outerGlowWidth` 0, the user didn't want it) — one vertex-colour mesh with `RedMagic/Fx Vertex Color`), `Colision` (one `EdgeCollider2D` per contour, layer Ground — **square corners**: the rounding is visual only, the game has no slopes). `PlayerMovement` matches: no slope following and no automatic step-up — any higher surface, even a 1-cell platform, has to be jumped onto (removed 2026-10-13: the slope `rise` lifted the player onto platforms up to `|move.x|·tan 80°` high, ~2.4 u in a dash).
- - Rim variants tried and **rejected** (2026-10-12): snow pads sticking out of floors, faceted crystal ice with icicles, lumpy gradient band, flat two-tone band with drips. The 4-band rim stays.
- Floor depth (a pale top face so the floor reads as seen slightly from above, like Ice1) was tried and **rejected**:
  it looked wrong and made walking feel off. Keep the rim flat/side-view.
- Geometry in `LevelShape` (pure): contours of the air pockets with rock on the right (pinch vertices turn left
  so each pocket keeps its own loop), every corner rounded with a quadratic curve of `cornerRadius`, and the rock
  fill cut by the same curves (convex corners cut, concave corners get a curved fillet) so no square corner pokes
  out — the "black corner" bug of the web sketch.
- Inner glow is a distance field, NOT ribbons pushed inward from the rim: ribbons fanned out at corners and left
  light wedges (rejected by the user). Thin rock naturally gets less glow.

**Antialiasing**: the rim/outline are hard-edged meshes with a fine wobble; without AA their edges hop pixel to pixel as the camera moves by sub-pixels (seen as subtle trembling, 2026-10-12). `UniversalRP.asset` MSAA = 4x — keep it on.

## Draw order (`LevelSorting`)
Everything of the level is on sorting layer **Default with negative orders** (project convention for backgrounds):
parallax -1000+, background snow -600, rock/glow/rim -300/-299/-298, over-terrain objects (doors) -200+, bounds -2000.
Enemies, projectiles and FX live on Default 0..20, and Default draws BEFORE BackGround/Terrain — putting the level on
those layers hid every enemy (bug seen in the first test). In-front layers and gameplay snow go on Characters.

## `LevelBiome` (ScriptableObject, one per biome)
Rock texture/tile size/shadow, corner radius (visual only; editing any biome value redraws its levels live via `LevelBiome.Changed`), rim widths (floor/wall/ceiling), 4 floor + 4 ceiling band colours,
outline, glows, background prefab, particles prefab. Ice: `Assets/Levels/_Biomas/Hielo/Hielo.asset`.

## `ParallaxBackgroundLayer` (one per background layer)
Same logic as the web tab 🏞️ Fondos (`level-terrain.js ▸ drawLayer`): size and height offset relative to screen height; horizontal `speed`
(fraction of camera movement); `repeat` Repeat / Mirror / LevelEnds (once at the level's left end, mirrored at the
right — wall and door); vertical `anchor` Proportional (spread over the level height: same look for 1000 or 3000 px
levels) / Ceiling / Floor (move with the world at `verticalSpeed`); `fade` toward `fogColor`; `inFront` draws on
`Characters` (order 50+); `overTerrain` draws above the rim and behind enemies/player (doors). Doors also
use `sitOnGround` (LevelEnds: base placed on `Level.GroundTop` at each end, Altura = fine offset) and `castShadow`
(sharp silhouette offset toward the level edge (light comes from the level centre), drawn right above the wall so it lands ON the wall and the
terrain hides it below the floor line — a blurred copy around the door read as dirt and was rejected; plus a soft
ellipse on the floor, `RedMagic/Parallax Shadow`). `ConfigureAsDoor` sets all of this. **Door steps**: one child object per step under the door layer (`Escalón abajo` / `Escalón arriba`, sprites `escalon_*.png` drawn by `DoorSteps` — *Tools ▸ RedMagic ▸ Niveles ▸ Crear escalones de la puerta*, which resets them) with `LevelEndsAttachment`: a LevelEnds layer moving with the world (speed 1) that has such children keeps its transform at the bottom-left of its left copy, so children follow the door and are placed by hand with the Move tool; each one draws a mirrored copy at the other end and sorts right in front of the layer (`orderOffset`).
gradient sampling so seams don't show a mip line). Needs a `Level` in a parent.

Made in the web tab **🏞️ Fondos** and imported with **`EditorNiveles ▸ Importar fondo…`** (`ParallaxImporter`, the only
background importer): reads the `*.parallax.json` (`{format:"redmagic-parallax", version:1, name, layers:[{name, image
(data URL, magenta/crop already applied), …the fields of this component}]}`) and makes `Fondo_<name>.prefab` with
images `capa1…` (new: in `<biome folder>/Fondos/<name>/`, biome = the open level's). If that prefab exists it is updated
in place (same GUID; layers matched **by name**, so children like the door steps survive; layers not in the file are
removed). Then it offers to set it as the biome background and/or on the open level (replacing its `Fondo`).

## Ambience
`Nieve.prefab` (cheap by design: native particle systems, no per-flake script, capped at 300 + 260 particles):
two systems on a `FollowCameraView` emitter. The gameplay-plane one collides in 2D with Ground + Platform (static
colliders only, quality Medium); a flake dies on impact and a Collision sub-emitter (`Copo posado`, max 150) spawns a
still flake that stays 2-3.5 s and fades. The background one passes through. Rebuild: `Tools ▸ … ▸ Crear nieve`. Tuning in one place: **`SnowSettings`** on the prefab root (amount, size range,
opacity, fall speed, landed time, particle caps for falling/landed/background). Test scenes add a Global Light 2D
(rock is lit, ready for crystal lights) and a global Volume `PostHielo` (bloom + vignette).

## Menus (`Tools ▸ RedMagic ▸ Niveles`, `LevelSetup`)
Preparar bioma de hielo · Crear tile y paleta de terreno · Crear nieve · Crear escalones de la puerta.

## EditorNiveles — tab 🧱 Niveles of the web + Unity importer
The editor is a tab of the main web (`Web/RedMagicWeb`, `modules/level-editor.js` + `modules/level-terrain.js`); Unity
only has the importer, in its own top menu. The first standalone page is in `Web/legacy/EditorNiveles/`; the old
🧱 Boceto de nivel tab and `LevelSketchImporter` are in `Web/legacy/` / `Assets/Legacy/Scripts/Editor/`.
- **Tab**: paint Roca / Plataforma… / art platforms / doors (brush, rectangle, flood fill, erase, right click = erase, undo; one thing per cell), size in cells
  (height ≤ 75, resize anchored bottom-left like Unity, 1-cell frame always rock). **Vista previa** (P) = game camera
  (20 units tall, 16:9) with the rim, rock texture and parallax background drawn by `level-terrain.js` — a canvas port
  of `Level` / `ParallaxBackgroundLayer` with the same parameters (approximate; door steps not drawn). Uses the web's
  Ctrl+S and unsaved dot (`saveActions.levels`, `dirtyTabs`); autosaves the grid to localStorage. Keep the UI minimal:
  the user rejected extra toggles and cursor overlays.
- **Files**: with the taller server (`start.cmd`) the sketches are saved/opened in **`Assets/Levels/Bocetos/`**
  (`tools/taller-server/niveles.mjs`: `GET /api/niveles/bocetos|boceto`, `POST /api/niveles/guardar`) and the biome art
  is read from `Assets/Levels` (`GET /api/niveles/arte`, nothing outside that folder). Without it: download / open a
  file, preview without art.
- **`Web/RedMagicWeb/assets/niveles/datos-unity.json`** (generated by `EditorNivelesWebData`, committed): tiles (`Roca` +
  every TileBase named `Plataforma…` in `Assets/Levels/_Biomas`, plus every `ArtPlatformTile` with its 4 sprite paths and every
  `DoorTile` with its door's off/on textures, `startOpen` and the `LevelDoor` size limits) and, per `LevelBiome`, its values and background
  layers (texture paths `Assets/…`). Rewritten when a biome / tile / background prefab under `_Biomas` is saved
  (AssetPostprocessor) and on every import.
- **Fondo** of the level (tab ▸ *Fondo*): the biome one (from `datos-unity.json`) or any 🏞️ Fondos entry of the
  Biblioteca; saved in the sketch as `background` and, on import, Unity puts `Fondo_<background>` on the level if that
  background was already imported (else a warning).
- **Format `*.levelsketch.json`** (`LevelSketchFile`): `{format:"redmagic-level", version:3, name, biome, background?, width, height,
  legend:[{char, layer:"terrain"|"platforms"|"artPlatforms"|"doors", tile}], rows}` — `rows[0]` = top row, frame included, `.` = air.
  v1 files (no legend) read `#` as rock. v1/v2 have no object layers: reimporting one leaves the `PlataformasArte` / `Puertas`
  painted in Unity untouched; v3 repaints them too.
- **Objects in the web, same rules as Unity**: a horizontal run of one art tile = one platform (single / left / centre / right
  sprite by neighbours, `ArtPlatformTile`); a connected group of one door tile = one door of its bounding rectangle
  (`DoorTilemap`), drawn red with a warning when outside `LevelDoor` limits (1–4 × 2–10). Neither counts as rock for the rim
  or `GroundTop`. Art from Unity via the taller server (door = its image stretched to the rectangle, an approximation of the
  piece-built `LevelDoor`); without it, palette colours. Tiles are looked up **by name**: a new platform type = a new tile `Plataforma<Algo>` in
  `_Biomas` (it then shows in the web palette by itself); an unknown name falls back to the default tile with a warning.
- **Menu `EditorNiveles ▸ Importar nivel…`** (top menu bar, NOT under Tools; `EditorNivelesMenu`, its only item; opens in
  `Assets/Levels/Bocetos`): into the open scene's `Level` (selected or the only one) or into a new scene (camera with
  `CameraFollow`, `Level` + Grid/Terreno + Grid/Plataformas + Grid/PlataformasArte + Grid/Puertas (created by
  `ArtPlatformSetup.EnsureMap` / `DoorTileSetup.EnsureMap`, also on reimport into a level missing them) + biome background and
  particles, global light, `PostHielo` volume, `SectionEntry`/`SectionExit` on the floor at both ends — move them by hand, plus an
  empty `WaveManager` at the level centre (one wave, no enemies, finishes the level); the scene is added to Build Settings and a
  dialog offers to add it to the first unlocked `WorldDefinition` pool so it shows up in runs). Pause/HUD/player are persistent
  (`MenusUI`, `GameHud`, `RunManager`), never in the level scene; `GameStateManager` creates itself after the first scene load
  if none came with it, so pause also freezes when a level scene is played on its own.
- **Importing only touches tiles, size and biome** (with Undo): rooms, waves, enemies, entry/exit added in Unity survive.

## 🏞️ Fondos (web tab, `modules/parallax-editor.js` + `parallax-data.js`)
Port of the old `parallax-test.html` (now `Web/legacy/ParallaxTest/`). Layers (1 = farthest) with the SAME fields as
`ParallaxBackgroundLayer` (+ web-only `key` = remove magenta, `crop` = cut edges, baked on export). Preview = game
camera following a flying player in front of a frame-only test level with the biome rim. Saved in the **Biblioteca**
(kind `parallax`, `draft = { layers, levelW, levelH }`: can be left half-done; 🧱 Niveles lists it at once). *Abrir… ▸
Traer de Unity* loads a biome's current background (images via the taller server) to retouch it; keep its name
(e.g. `Hielo` → `Fondo_Hielo`) so the import updates that prefab in place. One export: **⬇ Exportar a Unity** (also in
the Biblioteca card).

## Editing a level in Unity
`Level` Inspector ▸ **🖌 Pintar roca** / **🖌 Pintar plataformas**: opens the Tile Palette with the right target Tilemap
(Terreno / Plataformas), the `PaletaTerreno` palette, the tile in the brush and the Paint tool active (painting a
platform tile into Terreno would make it rock). Then Tile Palette tools: B brush, U rectangle, G fill, D erase (or
Shift+click). The level redraws as you paint. **Nieve del borde** (`rimOnTerrain`, `rimOnPlatforms`): turn the light rim
bands off for rock and/or platforms (1-cell platforms get top and bottom bands meeting in the middle); the outline stays.

## Painted objects (in `Assets/Scripts/Test/` until validated)
- **Art platforms**: `ArtPlatformTile` (left / repeating centre / right / single sprite chosen from its left-right
  neighbours) painted in `Grid/PlataformasArte`; `ArtPlatformTilemap` makes one thin one-way collider per run
  (`surfaceDrop` below the cell top skips the art's dark outline, `colliderThickness`). Thin styles only (art hangs
  from the cell top). Menu *Preparar plataformas de arte*.
- **Doors**: `DoorTile` cells painted in `Grid/Puertas` (renderer off); `DoorTilemap` turns each connected group into a
  `LevelDoor` of its bounding rectangle, `DontSave` (rebuilt on paint and load: the painted cells are the truth).
  `DoorTilemap.Doors` is the list for room logic. Menu *Preparar puertas pintables*.
- Both palettes share `_Biomas/PaletaPlataformasArte`; pick the right target Tilemap. Also paintable in the web editor (format v3).
- **Spikes (crystals)**: `SpikeTile` (`_Biomas/Hielo/Objetos/Pinchos/SpikeTile_Cristal`, palette row 3) painted in `Grid/Pinchos`
  (renderer off); `SpikeTilemap` reads the `Level` rock around each cell — rock below = grows up, above = down, else from the
  side wall — groups same-orientation cells into runs (2-cell sprite per pair, 1-cell for the leftover, variant by cell hash;
  a 2×2 block — 2 along the surface, 2 outward — is one big crystal: the 1-cell sprite ×2, hurt zone twice as deep),
  base-less art (`Cristal1_*` 256 px = 1 cell, `Cristal2_*` 512 px = 2 cells, pivot bottom-centre, `sink` into the rim) and
  one trigger per run (layer Ignore Raycast, `SpikeHazard`, player team only). Generated objects are `DontSave`; rebuilt on
  painting spikes or rock. Hit = damage + `CameraFollow.ShakeAll` + `HitStop` + fade and teleport to the respawn point
  (`ParkourRoom.RespawnPointFor`), all tuned on `SpikeTilemap`. Not in the web editor yet.
- **Gold chest** `LevelChest` (`Assets/Test/Parkour/CofreOro.prefab`, sheet `Objetos/ChestOpening.png`, 6 frames, 190 ppu):
  interact (`prompt.chest_open`, `InteractInput`, touch button) → frames → `gold` (500) + `itemChance` (10 %) of
  `ItemPickup.RandomItem` → hold → fade/rise/scale → inactive; `Opened` event.
- **Respawn pad** `RespawnPad` (`PlataformaReaparicion.prefab`, `SpawnOn_cut`/`SpawnOff_cut`, trimmed copies of the user's
  art): no collider, origin = ground point, `width` / `heightScale` (squash the isometric-ish art) / `sink`, `SpawnPosition`,
  lit while it is the active checkpoint (`RespawnPad.Last`).
- **Parkour room** `ParkourRoom` (needs a `LevelRoom`; *GameObject ▸ RedMagic ▸ Sala de parkour* makes one with pad left and
  chest right): finds its pad and chest by position; lights the pad while the player is inside and the room isn't done; chest
  opened → `LevelRoom.Unlock()` (the closed end door). Spikes inside it send the player back to its pad. Menu *Tools ▸ RedMagic ▸
  Niveles ▸ Preparar parkour* (idempotent: art import, tile + palette, prefabs if missing, `Grid/Pinchos` on the open level;
  EditorNiveles imports also add `Grid/Pinchos`).

## Pending
**Door art**: the Ice1 door was drawn for a floor with depth; on the flat rim it doesn't sit well. Tried a procedural
snow mound at its base — rejected. Needs new door art made for a flat side-view floor (or Hollow-Knight-style steps +
press up to enter).
Objects (platform variants, crystals with Light2D) as prefabs, then placeable from EditorNiveles (user: "pronto"; entry/exit/enemies stay manual for now).
