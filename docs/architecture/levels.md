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
- **Platforms** = a second Tilemap (`Grid/Plataformas`, tile `Plataforma` in the same palette). Same look as rock, but
  each connected platform gets its own child under `Plataformas`: layer Platform, `PolygonCollider2D` (rounded contour,
  usedByEffector) + one-way `PlatformEffector2D`. One collider per platform because drop-through ignores the specific
  collider being stood on (`PlayerMovement.TryDropThroughPlatform`). Rock-only cells feed `Colision` (Ground).
- Terrain = a `Tilemap` child (`Grid/Terreno`, renderer disabled). Paint with the **`PaletaTerreno`** palette
  (`Assets/Levels/_Biomas/`): any tile = rock. The frame is NOT painted — cells on the border and outside are
  always rock (`Level.IsSolid`). Origin = bottom-left corner.
- Regenerates on paint (`Tilemap.tilemapTileChanged`), on size/biome change and on load; meshes are `DontSave`.
  Children it owns: `BG`, `Roca` (rock fill, Sprite-Lit, biome texture tiled in world space), `Brillo` (rock shadow +
  inner glow baked into a texture by exact distance-to-air at 8 texels/unit), `Borde` (4 colour bands of noisy
  width, dark outline (outer glow off: `outerGlowWidth` 0, the user didn't want it) — one vertex-colour mesh with `RedMagic/Fx Vertex Color`), `Colision` (one `EdgeCollider2D` per contour, rounded like the art, layer Ground).
- Floor depth (a pale top face so the floor reads as seen slightly from above, like Ice1) was tried and **rejected**:
  it looked wrong and made walking feel off. Keep the rim flat/side-view.
- Geometry in `LevelShape` (pure): contours of the air pockets with rock on the right (pinch vertices turn left
  so each pocket keeps its own loop), every corner rounded with a quadratic curve of `cornerRadius`, and the rock
  fill cut by the same curves (convex corners cut, concave corners get a curved fillet) so no square corner pokes
  out — the "black corner" bug of the web sketch.
- Inner glow is a distance field, NOT ribbons pushed inward from the rim: ribbons fanned out at corners and left
  light wedges (rejected by the user). Thin rock naturally gets less glow.

## `LevelBiome` (ScriptableObject, one per biome)
Rock texture/tile size/shadow, corner radius, rim widths (floor/wall/ceiling), 4 floor + 4 ceiling band colours,
outline, glows, background prefab, particles prefab. Ice: `Assets/Levels/_Biomas/Hielo/Hielo.asset`.

## `ParallaxBackgroundLayer` (one per background layer)
Same logic as `parallax-test.html`: size and height offset relative to screen height; horizontal `speed`
(fraction of camera movement); `repeat` Repeat / Mirror / LevelEnds (once at the level's left end, mirrored at the
right — wall and door); vertical `anchor` Proportional (spread over the level height: same look for 1000 or 3000 px
levels) / Ceiling / Floor (move with the world at `verticalSpeed`); `fade` toward `fogColor`; `inFront` draws on
`Characters` (order 50+); `overTerrain` draws on `Terrain` order 20+ (above the rim, behind the player — doors). Doors also
use `sitOnGround` (LevelEnds: base placed on `Level.GroundTop` at each end, Altura = fine offset) and `castShadow`
(blurred silhouette offset onto the wall + soft ellipse on the floor, `RedMagic/Parallax Shadow`). Drawn with its own quad + `RedMagic/Parallax Layer` shader (repeat/mirror in the shader,
gradient sampling so seams don't show a mip line). Needs a `Level` in a parent.

Import: `Tools ▸ RedMagic ▸ Niveles ▸ Importar fondo de parallax (.json)` reads the page's **Exportar escena** file,
writes the images to `Assets/Levels/_Biomas/Hielo/Fondo/` and builds `Fondo_Hielo.prefab` (assigned to the biome).
Blur is not reproduced (warned).

## Ambience
`Nieve.prefab` (cheap by design: native particle systems, no per-flake script, capped at 300 + 260 particles):
two systems on a `FollowCameraView` emitter. The gameplay-plane one collides in 2D with Ground + Platform (static
colliders only, quality Medium); a flake dies on impact and a Collision sub-emitter (`Copo posado`, max 150) spawns a
still flake that stays 2-3.5 s and fades. The background one passes through. Rebuild: `Tools ▸ … ▸ Crear nieve`. Tuning in one place: **`SnowSettings`** on the prefab root (amount, size range,
opacity, fall speed, landed time, particle caps for falling/landed/background). Test scenes add a Global Light 2D
(rock is lit, ready for crystal lights) and a global Volume `PostHielo` (bloom + vignette).

## Menus (`Tools ▸ RedMagic ▸ Niveles`, `LevelSetup`)
Preparar bioma de hielo · Crear tile y paleta de terreno · Importar fondo de parallax (.json) · Crear nieve ·
Crear escenas de prueba de hielo (`Assets/Scenes/Test/NivelHielo_{1000,2000,3000}.unity`, copied from `TestMap`
with the web sketch layout) · Importar boceto en el nivel (web `*.levelsketch.json` → size + painted cells).

## Pending
Slopes (45° cells);
objects (platform variants, crystals with Light2D) as prefabs; level entry/exit for runs (`SectionEntry/Exit`).
