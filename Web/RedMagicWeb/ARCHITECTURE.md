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
| The 5 default lanes always created on image load (Idle/Walk/Attack/Hurt/Death) | `app.js` | `DEFAULT_LANE_NAMES` constant, created in `loadImageFile()` |
| "Animaciones automáticas" — clusters the CURRENT (already detected/manually-edited) unassigned boxes into rows via `clusterIntoRows`, not a fresh grid | `app.js` | `openAutoAssignPanel()`, `renderAutoAssignRows()`, `confirmAutoAssign()` — assignment itself goes through the same `Lanes.assignBoxesToLane` the manual "Asignar" button uses |
| Default row→lane mapping when there are exactly 5 rows | `app.js` | `DEFAULT_LANE_NAMES` order, applied in `renderAutoAssignRows()` |
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
| Rendering a library card grid (thumbnail/name/date/badges + caller-supplied action buttons) | `modules/library-panel.js` | `renderLibraryCards(container, {getActions})` — the ONE implementation shared by the Sprites-tab panel and the Biblioteca tab; only which actions each passes in differs (`app.js`) |
| Building the combined sprites+config zip (`manifest.json` + PNGs + `enemy-config.json` at root) | `modules/enemy-bundle.js` | `buildCombinedBundle({enemyName, sprite, configObj})` |
| The "app.js failed to load" fallback banner (e.g. opened via `file://` instead of a server) | `index.html` | the inline classic `<script>` right before `<script type="module" src="app.js">`, and `#moduleFailBanner` in the CSS/HTML |
| Tab switching (Sprites / Enemy Creator / Biblioteca) | `app.js` | top-of-file `.tabBtn` click wiring |
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
