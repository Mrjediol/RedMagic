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
  width, dark outline (outer glow off: `outerGlowWidth` 0, the user didn't want it) — one vertex-colour mesh with `RedMagic/Fx Vertex Color`), `Colision` (one `EdgeCollider2D` per contour, layer Ground — **square corners**: the rounding is visual only, the game has no slopes).
- Floor depth (a pale top face so the floor reads as seen slightly from above, like Ice1) was tried and **rejected**:
  it looked wrong and made walking feel off. Keep the rim flat/side-view.
- Geometry in `LevelShape` (pure): contours of the air pockets with rock on the right (pinch vertices turn left
  so each pocket keeps its own loop), every corner rounded with a quadratic curve of `cornerRadius`, and the rock
  fill cut by the same curves (convex corners cut, concave corners get a curved fillet) so no square corner pokes
  out — the "black corner" bug of the web sketch.
- Inner glow is a distance field, NOT ribbons pushed inward from the rim: ribbons fanned out at corners and left
  light wedges (rejected by the user). Thin rock naturally gets less glow.

## Draw order (`LevelSorting`)
Everything of the level is on sorting layer **Default with negative orders** (project convention for backgrounds):
parallax -1000+, background snow -600, rock/glow/rim -300/-299/-298, over-terrain objects (doors) -200+, bounds -2000.
Enemies, projectiles and FX live on Default 0..20, and Default draws BEFORE BackGround/Terrain — putting the level on
those layers hid every enemy (bug seen in the first test). In-front layers and gameplay snow go on Characters.

## `LevelBiome` (ScriptableObject, one per biome)
Rock texture/tile size/shadow, corner radius (visual only; editing any biome value redraws its levels live via `LevelBiome.Changed`), rim widths (floor/wall/ceiling), 4 floor + 4 ceiling band colours,
outline, glows, background prefab, particles prefab. Ice: `Assets/Levels/_Biomas/Hielo/Hielo.asset`.

## `ParallaxBackgroundLayer` (one per background layer)
Same logic as `parallax-test.html`: size and height offset relative to screen height; horizontal `speed`
(fraction of camera movement); `repeat` Repeat / Mirror / LevelEnds (once at the level's left end, mirrored at the
right — wall and door); vertical `anchor` Proportional (spread over the level height: same look for 1000 or 3000 px
levels) / Ceiling / Floor (move with the world at `verticalSpeed`); `fade` toward `fogColor`; `inFront` draws on
`Characters` (order 50+); `overTerrain` draws above the rim and behind enemies/player (doors). Doors also
use `sitOnGround` (LevelEnds: base placed on `Level.GroundTop` at each end, Altura = fine offset) and `castShadow`
(sharp silhouette offset toward the level edge (light comes from the level centre), drawn right above the wall so it lands ON the wall and the
terrain hides it below the floor line — a blurred copy around the door read as dirt and was rejected; plus a soft
ellipse on the floor, `RedMagic/Parallax Shadow`). `ConfigureAsDoor` sets all of this. Drawn with its own quad + `RedMagic/Parallax Layer` shader (repeat/mirror in the shader,
gradient sampling so seams don't show a mip line). Needs a `Level` in a parent.

Import: `Tools ▸ EditorNiveles ▸ Montaje ▸ Importar fondo de parallax (.json)` reads the page's **Exportar escena** file,
writes the images to `Assets/Levels/_Biomas/Hielo/Fondo/` and builds `Fondo_Hielo.prefab` (assigned to the biome).
Blur is not reproduced (warned).

## Ambience
`Nieve.prefab` (cheap by design: native particle systems, no per-flake script, capped at 300 + 260 particles):
two systems on a `FollowCameraView` emitter. The gameplay-plane one collides in 2D with Ground + Platform (static
colliders only, quality Medium); a flake dies on impact and a Collision sub-emitter (`Copo posado`, max 150) spawns a
still flake that stays 2-3.5 s and fades. The background one passes through. Rebuild: `Tools ▸ EditorNiveles ▸ Montaje ▸ Crear nieve`. Tuning in one place: **`SnowSettings`** on the prefab root (amount, size range,
opacity, fall speed, landed time, particle caps for falling/landed/background). Test scenes add a Global Light 2D
(rock is lit, ready for crystal lights) and a global Volume `PostHielo` (bloom + vignette).

## EditorNiveles — web level editor + Unity importer (`Web/EditorNiveles/`, `Assets/Test/EditorNiveles/Editor/`)
Own tool, NOT a tab of `Web/RedMagicWeb` nor of *Tools ▸ Web* (the old 🧱 Boceto de nivel tab and `LevelSketchImporter`
are in `Web/legacy/` / `Assets/Legacy/Scripts/Editor/`). Plain page opened with file:// (classic scripts, no modules/fetch).
- **Web** (`index.html`, `editor.js`, `terreno.js`): paint Roca / Plataforma… (brush, rectangle, flood fill, erase,
  right click = erase, undo), size in cells (height ≤ 75, resize anchored bottom-left like Unity, 1-cell frame always
  rock), camera + player (0.6 × 1.74) scale overlay. **Vista previa** = game camera (20 units tall, 16:9 / 19.5:9 / 4:3)
  with the rim, rock texture and parallax background drawn by `terreno.js` — a canvas port of `Level` /
  `ParallaxBackgroundLayer` with the same parameters (approximate, not pixel-exact). Saves with the File System Access
  API (fallback: download), autosaves to localStorage, opens by picker or drag & drop. Sketches: `Web/EditorNiveles/bocetos/`.
- **`datos-unity.js`** (generated by `EditorNivelesWebData`, committed): tiles (`Roca` + every TileBase named
  `Plataforma…` in `Assets/Levels/_Biomas`) and, per `LevelBiome`, its values and background layers (read from the
  biome's background prefab). Rewritten on *Abrir editor web* / *Actualizar biomas del editor web*: tune in Unity, the
  preview follows. A `.js` (`window.UNITY_DATA`), not `.json`, because file:// pages can't fetch.
- **Format `*.levelsketch.json`** (`LevelSketchFile`): `{format:"redmagic-level", version:2, name, biome, width, height,
  legend:[{char, layer:"terrain"|"platforms", tile}], rows}` — `rows[0]` = top row, frame included, `.` = air. v1 files
  (no legend) read `#` as rock. Tiles are looked up **by name**: a new platform type = a new tile `Plataforma<Algo>` in
  `_Biomas` (it then shows in the web palette by itself); an unknown name falls back to the default tile with a warning.
- **Menu `Tools ▸ EditorNiveles`** (`EditorNivelesMenu`): *Abrir editor web* · *Nuevo nivel desde boceto…* (new scene:
  camera with `CameraFollow`, `Level` + Grid/Terreno + Grid/Plataformas + biome background and particles, global light,
  `PostHielo` volume, `SectionEntry`/`SectionExit` on the floor at both ends — move them by hand; add the scene to the
  `WorldDefinition`) · *Importar boceto en el nivel abierto…* (selected `Level`, or the only one in the scene) ·
  *Exportar nivel abierto a boceto…* (Unity → web round trip) · *Montaje ▸* the old *Niveles* commands (`LevelSetup`).
- **Importing only touches tiles, size and biome** (with Undo): objects, enemies, entry/exit added in Unity survive a
  re-import. After importing, keep editing in Unity with the palette as usual.

## Pending
**Door art**: the Ice1 door was drawn for a floor with depth; on the flat rim it doesn't sit well. Tried a procedural
snow mound at its base — rejected. Needs new door art made for a flat side-view floor (or Hollow-Knight-style steps +
press up to enter).
Objects (platform variants, crystals with Light2D) as prefabs, then placeable from EditorNiveles (user: "pronto"; entry/exit/enemies stay manual for now).
