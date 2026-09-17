# Enemy Sprite Extractor — architecture

No build step: plain HTML + vanilla JS ES modules, served as static files. Open `index.html`
through a local static server (ES module imports are blocked under `file://` by browser CORS
rules — see the bottom of this doc for a one-line server command).

This file is the primary reference for "I want to change X" requests. **Keep it accurate**: when a
module's responsibility shifts, update its row in the same change.

## Module map

| To change... | Edit | Function |
|---|---|---|
| How background removal picks the bg color / flood-fills | `modules/background-removal.js` | `removeBackground(imageData, tolerancePercent)` |
| The foreground alpha mask (threshold for "is this pixel part of a sprite") | `modules/sprite-detection.js` | `buildAlphaMask(imageData, alphaThreshold)` |
| How much detected blobs get grown before merging (dilation) | `modules/sprite-detection.js` | `dilateMask(mask, w, h, radius)` |
| Connected-component blob finding / min-area noise filter | `modules/sprite-detection.js` | `connectedComponents(mask, w, h, minArea)` |
| Padding added around a detected box | `modules/sprite-detection.js` | `applyPadding(boxes, padding, imgW, imgH)` |
| Row clustering by y-center proximity (shared primitive — see below) | `modules/sprite-detection.js` | `clusterIntoRows(boxes, rowTol)` |
| Reading-order sort (uses `clusterIntoRows`, then sorts each row left-to-right) | `modules/sprite-detection.js` | `sortReadingOrder(boxes, rowTol)` |
| The end-to-end detect pipeline (order of the steps above) | `modules/sprite-detection.js` | `detectSprites(imageData, opts)` |
| Grid auto-slice cell math (rows×cols → boxes, ASSUMES a perfect uniform grid) | `modules/grid-autoslice.js` | `buildGridBoxes(width, height, rows, cols)` |
| Which row becomes which lane, frame order within a row (grid mode) | `app.js` | `gridSliceBtn` click handler (calls `buildGridBoxes` + `Lanes.createLane`/`assignBoxesToLane`) |
| **The canonical animation vocabulary Unity expects** (Idle/Walk/Attack/Hurt/Death/Wake — `EnemyAnimation.cs`/`AnimClipBuilder`, shared project-wide) | `modules/animation-lanes.js` | `CANONICAL_ANIMATION_NAMES` constant — the single source of truth; every other place a canonical name is offered reads this instead of listing the names again |
| The 5 default lanes always created on image load (Idle/Walk/Attack/Hurt/Death — the common case; `CANONICAL_ANIMATION_NAMES` minus Wake) | `app.js` | `DEFAULT_LANE_NAMES` constant (derived from `Lanes.CANONICAL_ANIMATION_NAMES`), created in `loadImageFile()` |
| "+ Nueva" animation-name entry — a dropdown of not-yet-used canonical names plus "Personalizado…", not free text, so a canonical name can't be mistyped; the free-text field is for genuinely game-specific extras (e.g. `Attack2`) | `app.js` | `openNewLaneModal()`, `syncNewLaneCustomVisibility()`, `confirmNewLane()`; markup in `index.html`'s `#newLaneOverlay` |
| "Animaciones automáticas" — clusters the CURRENT (already detected/manually-edited) unassigned boxes into rows via `clusterIntoRows`, not a fresh grid; every row's dropdown always offers ALL of `CANONICAL_ANIMATION_NAMES` (creating the lane at confirm time if it doesn't exist yet, with the exact canonical spelling) plus any custom lane already made | `app.js` | `openAutoAssignPanel()`, `buildAutoAssignLaneOptions()`, `renderAutoAssignRows()`, `confirmAutoAssign()` — assignment itself goes through the same `Lanes.assignBoxesToLane` the manual "Asignar" button uses; a canonical name with no lane yet is encoded as an option value prefixed `NEW_CANONICAL_LANE_PREFIX`, resolved into a real `Lanes.createLane` call in `confirmAutoAssign()` |
| Default row→lane mapping when there are exactly 5 rows | `app.js` | `DEFAULT_LANE_NAMES` order, applied in `renderAutoAssignRows()` (matched by each `<option>`'s `data-name`, not by an existing lane's id, since the option may not have a lane yet) |
| Zoom-to-cursor math, min/max zoom | `modules/canvas-view.js` | `CanvasView._onWheel`, `minScale`/`maxScale` fields |
| Middle-mouse pan | `modules/canvas-view.js` | `CanvasView._onMouseDown` / `_onMouseMove` |
| Initial "fit sheet to view" framing on image load | `modules/canvas-view.js` | `CanvasView.frameToFit()` |
| Screen↔canvas coordinate conversion (used by every box interaction) | `modules/canvas-view.js` | `CanvasView.screenToCanvas(clientX, clientY)` |
| Creating/deleting an animation or projectile lane | `modules/animation-lanes.js` | `createLane`, `deleteLane` |
| Assigning boxes to a lane (multi-select "Asignar", grid auto-slice) | `modules/animation-lanes.js` | `assignBoxesToLane(lanes, boxes, laneId, indices)` |
| Reordering/removing a single frame within a lane | `modules/animation-lanes.js` | `moveFrame`, `removeFrameFromLane` |
| An animation's "fires a projectile" link | `modules/animation-lanes.js` | `setProjectileLink(lane, projLanes, enabled)` |
| Deleting multiple boxes at once (keeps every lane's indices consistent) | `modules/animation-lanes.js` | `removeBoxes(boxes, lanes, indices)` |
| Cropping a box to pixels / a data URL (used by thumbnails AND export) | `modules/export-manifest.js` | `cropBox`, `cropToDataURL` |
| manifest.json shape / zip folder layout | `modules/export-manifest.js` | `populateSpriteZip({enemyName, lanes, boxes, source})` fills an existing JSZip and returns the manifest; `buildExportZip({...}, JSZip)` wraps it into a fresh zip + blob for the plain export — **coordinate with the Unity-side importer (`Assets/Editor/EnemyImporter.cs`) before changing field names or folder structure** |
| Triggering the actual file download | `modules/export-manifest.js` | `downloadBlob(blob, filename)` |
| The shared, persistent (IndexedDB) enemy library — save/list/get/delete/update | `modules/enemy-library.js` | `saveEnemy`, `listEnemies`, `getEnemy`, `deleteEnemy`, `updateEnemy` — see "Shared enemy library" below |
| Decoding a saved sheet snapshot back into an `Image`, building a card thumbnail | `modules/enemy-library.js` | `loadImageFromDataURL(dataURL)`, `buildThumbnail(source)` |
| Rendering a library card grid (thumbnail/name/date/badges + caller-supplied action buttons) | `modules/library-panel.js` | `renderLibraryCards(container, {getActions, fetchEntries, getTitle, getBadges, emptyMessage})` — the ONE implementation shared by the Sprites-tab panel, the Biblioteca tab, and the Map Tracer tab's maps/pieces lists; defaults reproduce the original enemy-library-only behavior, so only `getActions` (and, for non-enemy entries, `fetchEntries`/`getTitle`/`getBadges`) differ per call site (`app.js`) |
| Building the combined sprites+config zip (`manifest.json` + PNGs + `enemy-config.json` at root) | `modules/enemy-bundle.js` | `buildCombinedBundle({enemyName, sprite, configObj})` |
| The "app.js failed to load" fallback banner (e.g. opened via `file://` instead of a server) | `index.html` | the inline classic `<script>` right before `<script type="module" src="app.js">`, and `#moduleFailBanner` in the CSS/HTML |
| Tab switching (Sprites / Enemy Creator / Biblioteca / Map Tracer) | `app.js` | top-of-file `.tabBtn` click wiring |
| Detect-vs-grid sidebar mode switch | `app.js` | `setSpriteMode(next)` |
| Canvas box click/drag/resize/create interaction | `app.js` | `canvas` `mousedown`/`mousemove`/`mouseup` listeners, `hitTestHandle`, `getPos` |
| Box multi-select rules (click / ctrl+click / shift-range) | `app.js` | `applySelectionClick(i, ctrlKey, shiftKey)` — shared by the canvas and the unsorted grid |
| Redrawing the sheet + box outlines/handles | `app.js` | `render()` |
| The unsorted-sprites side panel | `app.js` | `renderUnsorted()` |
| The animation lane cards (frame thumbnails, fps/loop, projectile link UI) | `app.js` | `renderLanes()`, `buildLaneCard(lane, projLanes)` |
| The multi-select "assign to lane" toolbar | `app.js` | `updateAssignBar()`, `assignBtn` click handler |
| Colors, sizes, tab bar look, checkerboard background | `styles.css` | (plain CSS, one block per concern, matches the section comments) |
| Page structure / which controls exist | `index.html` | containers only — no inline logic; wire new controls in `app.js` |

### Enemy Creator tab

One EnemyConfig at a time (`docs/schemas/enemy-config.schema.json` + `_shared.schema.json` +
`projectile-config.schema.json` — vendored as read-only copies in `schemas/`, **keep byte-identical
to `docs/schemas/*.json`; there is no build step to enforce this**). No project/library management
beyond the current session; boss/map/player config are out of scope.

| To change... | Edit | Function |
|---|---|---|
| Which schema files are loaded / how Ajv is set up (draft-07, cross-file `$ref`s) | `modules/enemy-config-schema.js` | `loadEnemyConfigValidator()` |
| Running the live validator against a built config, turning Ajv errors into `{path, message}` | `modules/enemy-config-schema.js` | `validateEnemyConfig(validate, doc)` |
| A schema default the form pre-fills (must match `docs/schemas/*.json`'s own `"default"` keys) | `modules/enemy-defaults.js` | `createDefaultEnemyConfig()`, `createDefaultEnemyTuning()`, `createDefaultProjectileSpec()` |
| Turning live form state into the actual exportable/validatable JSON (omitting empty optional refs, folding in the projectile toggle) | `modules/enemy-export.js` | `buildExportObject(state)` |
| A generic input widget (text/number/bool/enum/vector2/color/layerMask/assetRef) | `modules/enemy-form-fields.js` | one `xField({...})` builder per JSON-Schema type — schema-agnostic, reused by every field |
| The layerMask widget's mode switch (everything/nothing/specific checkboxes/raw fallback) or the known-layer checkbox list | `modules/enemy-form-fields.js` | `layerMaskField({...})`; known layers passed in from `modules/enemy-form.js`'s `KNOWN_LAYERS` |
| The `art` field's library-picker / manual-path toggle, and linking a draft to a library entry from outside (Biblioteca tab's "Editar en Enemy Creator") | `modules/enemy-form.js` | `setArtMode()`, `refreshArtLibraryOptions()`, exposed `linkLibraryEntry(id, enemyName)` — computed `art` string itself lives in `modules/enemy-export.js`'s `libraryArtPath()` |
| Enabling/wiring "⬇ Exportar todo junto (.zip)", and persisting the exported config back onto the linked library entry | `modules/enemy-form.js` | `updateCombinedExportAvailability()`, `exportCombinedBtn` handler, `persistConfigToLibraryIfLinked()` |
| Which EnemyConfig field maps to which widget, and which UI group/order it's in | `modules/enemy-form.js` | `TUNING_FIELDS` array (group, key, widget kind, label, opts) — add a row here for a new schema field, no new function needed |
| The presence / projectileArt / top-level field rows (not table-driven — small, fixed sets) | `modules/enemy-form.js` | the "top-level fields" and "presence" sections at the top of `initEnemyCreator()` |
| Sprite pivot (`anchor`: Center/BottomCenter — `RedMagic.Pipeline.AnchorMode`) | `modules/enemy-form.js` | the `anchorField` row in the "top-level fields" section, next to `art`. Only meaningful on a combined-bundle import (`Assets/Editor/EnemyImporter.cs`'s `BuildEnemyFromFolderPath`/`ConfigureSpriteImport`, which now takes `AnchorMode` instead of a hardcoded bottom-pivot bool) — see `docs/schemas/enemy-config.schema.json`'s `anchor` field and `docs/schemas/COMPATIBILITY.md` for why a manual/already-imported `art` path ignores it |
| The projectile "usar por defecto ↔ configurar inline" toggle and its sub-form fields | `modules/enemy-form.js` | `setProjectileMode()`, `renderProjectileInline()`, `PROJECTILE_SPEC_FIELDS` |
| Which fields get visually de-emphasized when the enemy isn't ranged, and the ranged check itself (`archetype` OR `Static`+`staticAttack==Ranged`) | `modules/enemy-form.js` | `isRangedNow()`, `updateRangedEmphasis()`, the `rangedOnly` flag in `TUNING_FIELDS` |
| Live JSON preview + inline per-field validation errors | `modules/enemy-form.js` | `refresh()` — calls `buildExportObject` then `validateEnemyConfig` on every field change |
| The Sprites-tab lane name cross-reference line (display-only, never a schema field) | `modules/enemy-form.js` / `app.js` | `refreshLaneInfo()` in `enemy-form.js`; `app.js`'s tab-switch handler calls it, `getLaneNames` is injected from `app.js` so `enemy-form.js` never reads `lanes` directly |
| The actual `.json` download | `modules/enemy-form.js` | `exportBtn` click handler — reuses `export-manifest.js`'s `downloadBlob` |
| Enemy Creator layout / colors | `styles.css` | the `Enemy Creator tab` block (`.ef*` classes) |
| Enemy Creator page structure (form/preview panels) | `index.html` | `#tab-enemy` — containers only, `enemy-form.js` populates `#enemyFormRoot` |

### Shared enemy library (Sprites ↔ Enemy Creator ↔ Biblioteca)

**Why it exists**: the Enemy Creator's `art` field used to assume a sheet had already been
imported into Unity in a separate prior step. The real workflow is building sprites and config
together in one session — so sprite authoring state now persists in the browser (IndexedDB,
`modules/enemy-library.js`, database `redmagic-enemy-library`, one object store `enemies` keyed by
a generated `id`) and both tabs read/write the same records. Survives a page reload and a browser
restart; each browser profile has its own copy (nothing is synced anywhere).

**Record shape** (one per enemy):

```
{
  id: string,                // uuid
  enemyName: string,
  createdAt, updatedAt: number,   // epoch ms
  thumbnail: string|null,    // small dataURL for the library cards
  sprite: {
    lanes: [...],             // same shape as app.js's `lanes` — see "Data model" below
    boxes: [...],             // same shape as app.js's `boxes`
    sourceDataURL: string,    // the FULL sheet (post-bg-removal, if that was run) as a PNG dataURL
    width, height: number,
  },
  config: object|null,        // the last EnemyConfig export object built against this entry, or
                               // null until Enemy Creator has exported at least once while linked
}
```

Only `sprite.lanes`/`sprite.boxes` (small JSON) plus one PNG snapshot of the whole sheet are
stored — NOT one crop per frame — because `populateSpriteZip`/`buildExportZip` already crop frames
from a source canvas on demand; reloading `sourceDataURL` into an `Image` reproduces that same
source canvas, so re-exporting a saved entry crops identically to exporting it live never having
left the Sprites tab.

**Sprites tab**: "💾 Guardar como enemigo" writes the current `lanes`/`boxes`/sheet snapshot as a
record (`app.js`'s `currentLibraryId` tracks which record the currently-loaded sheet came from, so
re-saving updates it in place instead of duplicating it — reset to `null` only on loading a brand
new image file). The "Enemigos guardados" panel below it (`#spritesLibraryList`,
`renderSpritesLibraryList()`) lists every record with Cargar (reopens for editing — replaces
`img`/`boxes`/`lanes` and rebinds `currentLibraryId`) / Exportar zip (existing sprite-only format,
unchanged) / Eliminar.

**Enemy Creator tab**: the `art` field is a mode switch. **"Desde biblioteca"** (default) is a
dropdown of every saved record; picking one sets `state.artLibraryId` (the config draft links to
that record by id — it does not copy or duplicate its sprite data) and pre-fills `enemyName` if
still empty. The exported `art` string in this mode is computed
(`modules/enemy-export.js`'s `libraryArtPath()`) as
`Assets/Enemies/<enemyName>/<enemyName>.sheet.asset` — the exact path
`Assets/Editor/EnemyImporter.cs`'s sprite-import step creates a `SpriteSheetRecipe` at, by
convention; the Unity-side combined-bundle importer (see below) resolves the real freshly-created
asset explicitly rather than trusting this string, so a convention drift here is inert, never a
silent bug. **"Escribir ruta manualmente"** is the original behavior: a plain path/assetRef into a
sheet already imported into Unity in a past session, independent of this session's library.

With library mode active and a record selected, **"⬇ Exportar todo junto (.zip)"**
(`modules/enemy-bundle.js`'s `buildCombinedBundle`) produces one zip containing the linked
record's sprite manifest + PNGs (same structure `populateSpriteZip` always produces) **plus
`enemy-config.json` at the zip root**. With manual-path art this button is disabled (no sprite data
to bundle) — the existing **"⬇ Exportar solo config (.json)"** button always stays available.
Either export also persists the built config object onto the library record
(`persistConfigToLibraryIfLinked`) — non-fatal if it fails, the download already happened — which
is what lets the Biblioteca tab offer "Exportar combinado" for that entry afterward without the
form needing to be refilled.

**Biblioteca tab** (`#tab-library`, `renderLibraryTab()` in `app.js`): every saved record in one
place, reusing `modules/library-panel.js`'s card renderer with the full action set — Cargar en
Sprites, Editar en Enemy Creator (switches tab and calls `enemyCreator.linkLibraryEntry(id,
enemyName)`), Exportar zip, Exportar combinado (disabled until the record has a linked `config`),
Eliminar.

### Map Tracer tab

Ported from the standalone `Assets/Editor/MapTracer.html` (a piece library of uploaded images,
placed as instances on a scene canvas, each piece carrying its own traced collision lines) into
the same module-per-concern shape as the rest of this app. The exported `RedMagicMap/1` JSON is
parsed as-is by `Assets/Editor/CollisionImporter.cs`'s `MapImporter` — **coordinate with that file
before changing field names or shapes**.

| To change... | Edit | Function |
|---|---|---|
| The piece-library data model (asset shape, multi-file upload, type assignment) | `modules/map-assets.js` | `createAsset` (internal), `addFiles`, `assignType`, `deleteAssets`, `findAsset` |
| Instance placement, hit-testing, multi-select move | `modules/map-instances.js` | `createInstance`, `hitTestInstance`, `moveInstances`, `deleteInstances` |
| Unity-style corner-resize (free + Shift-proportional, single or group) | `modules/map-instances.js` | `beginResize` (captures anchor + per-instance offsets), `applyResize` (mutates on drag), `hitTestResizeHandle` |
| Per-piece collision line tracing (add/undo/commit/delete a point or line) | `modules/map-collision-tracing.js` | `startLine`, `addPoint`, `undoPoint`, `commitLine`, `deleteLine` |
| Map canvas zoom/pan | `modules/map-canvas-view.js` | re-exports `CanvasView` from `modules/canvas-view.js` unchanged — see that file's module-map row |
| PNG compositing export (draw order: background → platform → border) | `modules/map-export.js` | `composeMap`, `composeMapToBlob` |
| Baking a piece's local-space traced lines into absolute map-space, and the exported `RedMagicMap/1` JSON shape | `modules/map-export.js` | `sceneLines`, `buildMapJson` — **coordinate with `Assets/Editor/CollisionImporter.cs`** |
| The exported `RedMagicMapPieces/1` JSON shape (standalone pieces, no canvas/instances) | `modules/map-export.js` | `buildPiecesJson(pieceRecords)` — **coordinate with `Assets/Editor/CollisionImporter.cs`'s `MapPiecesImporter`** |
| The persistent (IndexedDB) Map Tracer library — save/list/get/delete/update for BOTH maps and standalone pieces | `modules/map-library.js` | `saveMap`/`listMaps`/`getMap`/`deleteMap`/`updateMap`, `savePiece`/`listPieces`/`getPiece`/`deletePiece`/`updatePiece` — see "Map Tracer library" below |
| Rehydrating a saved map/piece record back into live session state | `modules/map-library.js` | `loadMapAsSession`, `loadPieceAsAsset` |
| Biblioteca panel's multi-select over saved pieces (ctrl-toggle/shift-range, same rule as the Sprites tab's box grid) and the "⬇ Exportar piezas seleccionadas"/per-card "⬇ Exportar" actions | `app.js` | `applyPieceLibrarySelectionClick`, `exportPieceLibraryEntry`, `mapExportSelectedPiecesBtn` handler, `selectedPieceLibraryIds` |
| Selectable-card support in the shared card grid (opt-in `isSelected`/`onCardClick`, used by the pieces list above; every other call site is unaffected) | `modules/library-panel.js` | `renderLibraryCards(container, {isSelected, onCardClick, ...})` |
| Map tab canvas interaction (place/select/move/resize/trace), all mode switching, piece/instance panels, library wiring | `app.js` | the "Map Tracer tab" section at the bottom of the file |
| Map tab layout / colors | `styles.css` | the "Map Tracer tab" block (`.map*`/`#map*` selectors) |
| Map tab page structure | `index.html` | `#tab-map` — containers only, `app.js` populates everything |
| Single-map import as a Unity prefab | `Assets/Editor/CollisionImporter.cs` | `MapImporter.ImportMap()` / shared `ImportOneMap()` |
| Batch-importing every Map JSON in a folder in one pass | `Assets/Editor/CollisionImporter.cs` | `MapImporter.ImportMapsBatch()` |
| Importing a `RedMagicMapPieces/1` JSON as one prefab per piece | `Assets/Editor/CollisionImporter.cs` | `MapPiecesImporter.ImportPieces()` / `BuildPiecePrefab()` |
| Browsing/deleting everything these importers (and the enemy/boss pipeline) have produced, from inside Unity | `Assets/Editor/WebLibraryWindow.cs` | `Tools ▸ Web ▸ Biblioteca` — see `docs/web-tools-guide.md` for the full category breakdown |

### Map Tracer library (maps vs. pieces)

A separate IndexedDB database from the enemy library (`redmagic-map-library`, `modules/
map-library.js`) — different domain, no reason to couple schemas. Two object stores, because this
tab saves two different kinds of thing:

**Maps** (`maps` store) — a full scene:

```
{
  id, name, createdAt, updatedAt: ...,
  thumbnail: string|null,
  canvasWidth, canvasHeight: number,
  assets: [{ name, fileName, width, height, type, imageDataURL, lines }, ...],  // piece library used, in order
  instances: [{ id, assetIndex, x, y, scaleX, scaleY }, ...],                    // assetIndex = index into `assets` above
}
```

Live runtime asset ids don't survive a save (regenerated on every load), so a saved instance
points at its piece by `assetIndex` (its position in the `assets` array) rather than by id;
`loadMapAsSession()` decodes every `imageDataURL` back into an `Image`, assigns each a fresh id,
and re-points every instance's `assetIndex` to that new id. Triggered by "💾 Guardar mapa en
biblioteca" — saves the CURRENT session's full `assets` + `instances` + canvas size. "Cargar"
**replaces** the current session's `assets`/`instances`/canvas size outright (it opens the map).

**Pieces** (`pieces` store) — one asset, standalone:

```
{
  id, name, createdAt, updatedAt: ...,
  thumbnail: string|null,
  type: 'background'|'border'|'platform',   // never 'unassigned' — assign a type before saving
  imageDataURL, width, height: ...,
  lines: [...],                              // that piece's own traced lines, in ITS local space
}
```

Triggered per-asset by "💾 Guardar pieza en biblioteca" in the piece-library panel (available the
moment a piece is selected, independent of any map). "Cargar" on a piece does **not** open a map —
it decodes the image and **appends** the piece into the current session's in-memory piece library
(`mapAssets`) via `loadPieceAsAsset()`, so it becomes available to place instances of immediately,
same as any freshly-uploaded piece. This is what lets border/background/platform pieces be
authored once and mixed into different maps later.

Both kinds render through the same `modules/library-panel.js`'s `renderLibraryCards` the rest of
the app uses (see below — it's generic, not enemy-specific), each with its own action set: maps
get Cargar/Exportar PNG/Exportar JSON/Eliminar; pieces get Cargar/⬇ Exportar/Eliminar, plus
multi-select (see below).

**Exporting pieces as standalone prefabs (`RedMagicMapPieces/1`)** — separate from the composed
map export above. There is no "export the current map's used pieces" mode; the only source for
this export is the **Biblioteca panel's saved pieces list**. Two ways to trigger it:

- A single piece's own "⬇ Exportar" card action — one piece, one JSON.
- Multi-select in the pieces list (ctrl-click toggles one, shift-click selects the range since the
  last click, a plain click replaces the selection — the exact same rule `app.js`'s
  `applySelectionClick` already applies to the Sprites tab's box grid, reimplemented for library
  record ids as `applyPieceLibrarySelectionClick`) plus "⬇ Exportar piezas seleccionadas" — one
  JSON with every selected piece.

Both funnel into `modules/map-export.js`'s `buildPiecesJson(pieceRecords)`, producing:

```
{
  "format": "RedMagicMapPieces/1",
  "pieces": [
    { "assetName", "fileName", "type": "background"|"border"|"platform",
      "width", "height", "lines": [{ "points": [{x,y}, ...] }, ...] },
    ...
  ]
}
```

`fileName` is always `== assetName` for a library piece (a saved piece has no original uploaded
filename to remember — same convention `loadPieceAsAsset` already uses when reopening one into a
session). No pixel data travels in this JSON; `Assets/Editor/CollisionImporter.cs`'s
`MapPiecesImporter.ImportPieces()` resolves each piece's `Sprite` by name from assets already
imported into the Unity project (via `MapImporter.FindSprite`, shared rather than duplicated) and
builds one prefab per piece — a `SpriteRenderer` plus, for `border`/`platform` pieces with traced
`lines`, an `EdgeCollider2D` per line on the matching layer. The multi-select card styling itself
(`.libCardSelectable`/`.selected` in `styles.css`) is opt-in on `renderLibraryCards` and does
nothing for any other call site (enemy library, maps list) that doesn't pass `isSelected`/
`onCardClick`.

## Data model (owned by `app.js`, passed into the modules above)

- `img` — the original loaded `Image`.
- `workCanvas`/`workCtx` — a same-size canvas holding the (possibly background-removed) RGBA
  pixels; `bgRemoved` says which of `img`/`workCanvas` is the current source (`currentSource()`).
- `boxes: Array<{x,y,w,h,assignedLane}>` — every detected/manual/grid box, in image-pixel space
  (never in screen/zoomed space — see `canvas-view.js`'s coordinate note below).
- `lanes: Array<{id,type,name,fps,loop,frameBoxIndices,projectileLink}>` — animation and
  projectile lanes; `frameBoxIndices` are indices into `boxes`, in frame order.
- `selectedIndices: Set<number>` / `lastClickedIndex` — multi-selection state shared by the canvas
  and the unsorted-sprite grid.
- `spriteMode: 'detect'|'grid'` — which sidebar panel is active.
- `interactionMode: 'select'|'addbox'` — canvas click behavior.
- `view: CanvasView` — the zoom/pan transform. **Box coordinates are always in canvas-native image
  pixels; `view` only affects how that space is displayed.** Any new code reading a mouse event
  must go through `view.screenToCanvas()`, never `event.offsetX/Y` directly.

## Behavior changes vs. the old single-file tool (intentional, not bugs)

- **Detecting or grid-slicing now clears `lanes`** (with a `confirm()` if any lane already has
  frames assigned). The old tool replaced `boxes` on every "Detectar sprites" click without
  touching `lanes`, which left `frameBoxIndices` pointing at the wrong sprites after a second
  detection pass — a latent bug, not a feature. If a future request wants detection to *merge*
  with existing lanes instead, that policy lives in `app.js`'s `detectBtn`/`gridSliceBtn` handlers
  and `confirmReplaceIfNeeded()`, not in `sprite-detection.js` or `grid-autoslice.js`.
- **Canvas navigation (zoom/pan) is new** — see `modules/canvas-view.js` above. Everything else
  (detection knobs, manual boxing, multi-select assign, export format) is a straight port.

## Running locally

Browsers block ES module `import` under `file://`. Serve the folder instead, e.g.:

```
npx serve Web/RedMagicWeb
# or
python -m http.server --directory Web/RedMagicWeb 8000
```

then open the printed `http://localhost:...` URL.
