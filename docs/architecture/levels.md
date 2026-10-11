# Levels: terrain, parallax background, biomes (`Assets/Scripts/Levels/`)

Replaces the old sketch-importer/kit system (tiled edge strips, `LevelKit`, old `ParallaxLayer`), now in
`Assets/Scripts/Legacy/Levels/`. Validated first in the web prototypes `Web/ParallaxTest/` (see
`docs/biomas-receta.md`); the Unity code ports the same algorithms and the same parameters.

## Scale
**1 unit = 1 cell = 40 px of the web sketches.** 1000 px = 25 units. Camera `orthographicSize` 10 (20 units of
screen height), as forced by `RunManager`.

## `Level` (one per level scene)
- `width` × `height` in units (Inspector shows the px equivalent). Height capped at `Level.MaxHeight` = 75
  (3000 px). Changing the size re-fits everything: the implicit 1-cell rock frame, `BG` (camera bounds read by
  `CameraFollow`), the collider and the parallax range. Painted cells are kept.
- Terrain = a `Tilemap` child (`Grid/Terreno`, renderer disabled). Paint with the **`PaletaTerreno`** palette
  (`Assets/Levels/_Biomas/`): any tile = rock. The frame is NOT painted — cells on the border and outside are
  always rock (`Level.IsSolid`). Origin = bottom-left corner.
- Regenerates on paint (`Tilemap.tilemapTileChanged`), on size/biome change and on load; meshes are `DontSave`.
  Children it owns: `BG`, `Roca` (rock fill, Sprite-Lit, biome texture tiled in world space), `Borde` (rim: shadow
  over the rock, inner glow, 4 colour bands of noisy width, outer glow, dark outline — one vertex-colour mesh with
  `RedMagic/Fx Vertex Color`), `Colision` (one `EdgeCollider2D` per contour, rounded like the art, layer Ground).
- Geometry in `LevelShape` (pure): contours of the air pockets with rock on the right (pinch vertices turn left
  so each pocket keeps its own loop), every corner rounded with a quadratic curve of `cornerRadius`, and the rock
  fill cut by the same curves (convex corners cut, concave corners get a curved fillet) so no square corner pokes
  out — the "black corner" bug of the web sketch.
- Inner glow is clamped to half the local rock thickness (else it shines through 1-cell platforms).

## `LevelBiome` (ScriptableObject, one per biome)
Rock texture/tile size/shadow, corner radius, rim widths (floor/wall/ceiling), 4 floor + 4 ceiling band colours,
outline, glows, background prefab, particles prefab. Ice: `Assets/Levels/_Biomas/Hielo/Hielo.asset`.

## `ParallaxBackgroundLayer` (one per background layer)
Same logic as `parallax-test.html`: size and height offset relative to screen height; horizontal `speed`
(fraction of camera movement); `repeat` Repeat / Mirror / LevelEnds (once at the level's left end, mirrored at the
right — wall and door); vertical `anchor` Proportional (spread over the level height: same look for 1000 or 3000 px
levels) / Ceiling / Floor (move with the world at `verticalSpeed`); `fade` toward `fogColor`; `inFront` draws on
`Characters` (order 50+). Drawn with its own quad + `RedMagic/Parallax Layer` shader (repeat/mirror in the shader,
gradient sampling so seams don't show a mip line). Needs a `Level` in a parent.

Import: `Tools ▸ RedMagic ▸ Niveles ▸ Importar fondo de parallax (.json)` reads the page's **Exportar escena** file,
writes the images to `Assets/Levels/_Biomas/Hielo/Fondo/` and builds `Fondo_Hielo.prefab` (assigned to the biome).
Blur is not reproduced (warned).

## Ambience
`Nieve.prefab`: two particle systems on a `FollowCameraView` emitter — the gameplay-plane one collides with Ground
(2D) and lands (dampen 1, lifetime loss 0.8), the background one passes through. Test scenes add a Global Light 2D
(rock is lit, ready for crystal lights) and a global Volume `PostHielo` (bloom + vignette).

## Menus (`Tools ▸ RedMagic ▸ Niveles`, `LevelSetup`)
Preparar bioma de hielo · Crear tile y paleta de terreno · Importar fondo de parallax (.json) · Crear nieve ·
Crear escenas de prueba de hielo (`Assets/Scenes/Test/NivelHielo_{1000,2000,3000}.unity`, copied from `TestMap`
with the web sketch layout) · Importar boceto en el nivel (web `*.levelsketch.json` → size + painted cells).

## Pending
Light wedge where inner glows overlap at concave ceiling corners (fix = distance-based glow); slopes (45° cells);
objects (platform variants, crystals with Light2D) as prefabs; level entry/exit for runs (`SectionEntry/Exit`).
