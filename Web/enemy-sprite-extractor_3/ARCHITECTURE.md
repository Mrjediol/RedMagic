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
| Reading-order sort (row clustering, left-to-right within a row) | `modules/sprite-detection.js` | `sortReadingOrder(boxes, rowTol)` |
| The end-to-end detect pipeline (order of the steps above) | `modules/sprite-detection.js` | `detectSprites(imageData, opts)` |
| Grid auto-slice cell math (rows×cols → boxes) | `modules/grid-autoslice.js` | `buildGridBoxes(width, height, rows, cols)` |
| Which row becomes which lane, frame order within a row | `app.js` | `gridSliceBtn` click handler (calls `buildGridBoxes` + `Lanes.createLane`/`assignBoxesToLane`) |
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
| manifest.json shape / zip folder layout | `modules/export-manifest.js` | `buildExportZip({enemyName, lanes, boxes, source, JSZip})` — **coordinate with the Unity-side importer (`Assets/Editor/EnemyImporter.cs`) before changing field names or folder structure** |
| Triggering the actual file download | `modules/export-manifest.js` | `downloadBlob(blob, filename)` |
| The "app.js failed to load" fallback banner (e.g. opened via `file://` instead of a server) | `index.html` | the inline classic `<script>` right before `<script type="module" src="app.js">`, and `#moduleFailBanner` in the CSS/HTML |
| Tab switching (Sprites / Enemy Creator) | `app.js` | top-of-file `.tabBtn` click wiring |
| Detect-vs-grid sidebar mode switch | `app.js` | `setSpriteMode(next)` |
| Canvas box click/drag/resize/create interaction | `app.js` | `canvas` `mousedown`/`mousemove`/`mouseup` listeners, `hitTestHandle`, `getPos` |
| Box multi-select rules (click / ctrl+click / shift-range) | `app.js` | `applySelectionClick(i, ctrlKey, shiftKey)` — shared by the canvas and the unsorted grid |
| Redrawing the sheet + box outlines/handles | `app.js` | `render()` |
| The unsorted-sprites side panel | `app.js` | `renderUnsorted()` |
| The animation lane cards (frame thumbnails, fps/loop, projectile link UI) | `app.js` | `renderLanes()`, `buildLaneCard(lane, projLanes)` |
| The multi-select "assign to lane" toolbar | `app.js` | `updateAssignBar()`, `assignBtn` click handler |
| Colors, sizes, tab bar look, checkerboard background | `styles.css` | (plain CSS, one block per concern, matches the section comments) |
| Page structure / which controls exist | `index.html` | containers only — no inline logic; wire new controls in `app.js` |

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
npx serve Web/enemy-sprite-extractor_3
# or
python -m http.server --directory Web/enemy-sprite-extractor_3 8000
```

then open the printed `http://localhost:...` URL.
