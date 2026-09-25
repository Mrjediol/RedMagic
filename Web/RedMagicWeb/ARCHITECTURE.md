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
| **What KIND an entry is** (`enemy` / `projectile` / `fx` / `boss`) and everything that differs between them — lane vocabulary, default lanes, UI wording, whether it owns a sheet at all (`sheet`), whether it can carry projectile lanes, and WHICH creator tab authors its config (`creator`) + under what file name (`configFile`) | `modules/entry-kinds.js` | `KINDS` — the single source of truth for the kind dimension. Adding a fifth kind is one more entry here and nothing else. `SHEET_KIND_IDS` is the subset the Sprites tab offers |
| **The canonical animation vocabulary Unity expects** for the ENEMY kind (Idle/Walk/Attack/Hurt/Death/Wake — `EnemyAnimation.cs`/`AnimClipBuilder`, shared project-wide) | `modules/animation-lanes.js` | `CANONICAL_ANIMATION_NAMES` constant — still the single source of truth for those names; `entry-kinds.js` imports it as the enemy kind's `lanes` rather than restating them. The other kinds' vocabularies (projectile: Awake/Move/Impact; fx: Spawn/Loop/End) live in `entry-kinds.js` |
| The default lanes created on image load, per kind (enemy: Idle/Walk/Attack/Hurt/Death; projectile: Move/Impact; fx: Spawn/Loop/End) | `modules/entry-kinds.js` | each kind's `defaultLanes`; `app.js`'s `defaultLaneNames()` reads it for the current kind, used by `loadImageFile()`, the grid re-slice and `setKind()` |
| How the sprite sheet gets loaded (two entry points, one shared path) | `app.js` | `fileInput`'s `change` listener AND the document-level `paste` listener both just resolve a `File`/`Blob` and hand it to the same `loadImageFile(f)` — add a third input source (e.g. drag-and-drop) the same way rather than duplicating the `FileReader`/`Image` logic. The paste listener only acts while `#tab-sprites` is the visible panel, and silently no-ops if the clipboard has no `image/*` item (e.g. pasted text) |
| "+ Nueva" animation-name entry — a dropdown of not-yet-used canonical names plus "Personalizado…", not free text, so a canonical name can't be mistyped; the free-text field is for genuinely game-specific extras (e.g. `Attack2`) | `app.js` | `openNewLaneModal()`, `syncNewLaneCustomVisibility()`, `confirmNewLane()`; markup in `index.html`'s `#newLaneOverlay` |
| "Animaciones automáticas" — clusters the CURRENT (already detected/manually-edited) unassigned boxes into rows via `clusterIntoRows`, not a fresh grid; every row's dropdown always offers ALL of the CURRENT KIND's canonical names (`app.js`'s `canonicalLaneNames()`) (creating the lane at confirm time if it doesn't exist yet, with the exact canonical spelling) plus any custom lane already made | `app.js` | `openAutoAssignPanel()`, `buildAutoAssignLaneOptions()`, `renderAutoAssignRows()`, `confirmAutoAssign()` — assignment itself goes through the same `Lanes.assignBoxesToLane` the manual "Asignar" button uses; a canonical name with no lane yet is encoded as an option value prefixed `NEW_CANONICAL_LANE_PREFIX`, resolved into a real `Lanes.createLane` call in `confirmAutoAssign()` |
| Default row→lane mapping when the row count matches the current kind's default-lane count | `app.js` | `defaultLaneNames()` order, applied in `renderAutoAssignRows()` (matched by each `<option>`'s `data-name`, not by an existing lane's id, since the option may not have a lane yet) |
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
| Which library entries travel INSIDE an enemy's combined bundle (`Library/<libraryId>/`), so importing the enemy alone materializes its projectile | `modules/enemy-bundle.js` | `embedReferencedLibraryEntries()` — **coordinate with `CombinedBundleImporter.LiftEmbeddedLibraryEntries` and `FxPrefabBuilder` on the Unity side** |
| The Enemy Creator's "projectile art from the library" picker (exports `tuning.projectile.libraryId`) | `modules/enemy-form.js` | `renderProjectileLibraryRow()` |
| manifest.json shape / zip folder layout | `modules/export-manifest.js` | `populateSpriteZip({enemyName, kind, lanes, boxes, source})` (`kind` is emitted into the manifest; `enemyName` keeps its name for every kind because that is the key `EnemyImporter.cs` reads) fills an existing JSZip and returns the manifest; `buildExportZip({...}, JSZip)` wraps it into a fresh zip + blob for the plain export — **coordinate with the Unity-side importer (`Assets/Editor/EnemyImporter.cs`) before changing field names or folder structure** |
| Triggering the actual file download | `modules/export-manifest.js` | `downloadBlob(blob, filename)` |
| The shared, persistent (IndexedDB) entry library — save/list/get/delete/update, for every kind | `modules/enemy-library.js` | `saveEnemy`, `listEnemies` (all kinds), `listEntries(kind)` (one kind, or all when null), `getEnemy`, `deleteEnemy`, `updateEnemy`, `entryName` — see "Shared entry library" below |
| Decoding a saved sheet snapshot back into an `Image`, building a card thumbnail | `modules/enemy-library.js` | `loadImageFromDataURL(dataURL)`, `buildThumbnail(source)` |
| Rendering a library card grid (thumbnail/name/date/badges + caller-supplied action buttons) | `modules/library-panel.js` | `renderLibraryCards(container, {getActions, fetchEntries, getTitle, getBadges, emptyMessage})` — the ONE implementation shared by the Sprites-tab panel, the Biblioteca tab, and the Map Tracer tab's maps/pieces lists; defaults reproduce the original enemy-library-only behavior, so only `getActions` (and, for non-enemy entries, `fetchEntries`/`getTitle`/`getBadges`) differ per call site (`app.js`) |
| Building the combined sprites+config zip (`manifest.json` + PNGs + `enemy-config.json` at root) | `modules/enemy-bundle.js` | `buildCombinedBundle({enemyName, sprite, configObj})` |
| The "app.js failed to load" fallback banner (e.g. opened via `file://` instead of a server) | `index.html` | the inline classic `<script>` right before `<script type="module" src="app.js">`, and `#moduleFailBanner` in the CSS/HTML |
| Tab switching (Sprites / Enemy Creator / Proyectil-VFX / Boss Creator / Biblioteca / Map Tracer) | `app.js` | top-of-file `.tabBtn` click wiring; `creatorFor(kind)` maps a kind to its creator-tab API via `KINDS[kind].creator` |
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
| Which schema files are loaded / how Ajv is set up (draft-07, cross-file `$ref`s), for **every** creator tab | `modules/config-schema.js` | `loadValidator(name)` (`'enemy'`/`'fx'`/`'boss'`/`'projectile'`), `validateConfig(validate, doc)` — **one shared Ajv instance**, because a `$ref` only resolves against schemas registered in the same instance and registering `_shared` twice in one instance throws |
| The Enemy Creator's own entry point into that registry (thin wrappers, same names as before) | `modules/enemy-config-schema.js` | `loadEnemyConfigValidator()`, `validateEnemyConfig` |
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

### Projectile/FX Creator tab

Authors an **FxConfig** (`docs/schemas/fx-config.schema.json`) — a projectile or a VFX as its own
reusable library entity, instead of its numbers only existing nested inside whichever enemy throws
it. Built as a near-mirror of the Enemy Creator (same widgets, same "Desde biblioteca" picker, same
live Ajv preview, same two export buttons); what differs is a much smaller document plus a `kind`
switch that hides the projectile-only half.

**The entry and its config are ONE record.** A projectile authored in the Sprites tab is already a
`kind:'projectile'` library record; this tab fills in that same record's `config`, exactly as Enemy
Creator fills in an enemy record's. That is what "selectable elsewhere by `libraryId`" means with no
second storage system — the id an EnemyConfig's `tuning.projectile.libraryId` already points at is
the id that now also carries the tuning.

| To change... | Edit | Function |
|---|---|---|
| Which FxConfig field maps to which widget, the kind switch, the art picker | `modules/fx-form.js` | `initFxCreator()`, `PROJECTILE_SPEC_FIELDS`, `applyKind()`, `refreshArtOptions()` |
| Reopening a saved entry's config into the form (and from Biblioteca's "Editar en…") | `modules/fx-form.js` | `adoptConfig()`, exposed `linkLibraryEntry(id, name, kind)` |
| A schema default the form pre-fills, and the movement-mode dropdown's list/wording | `modules/fx-defaults.js` | `createDefaultFxConfig()`, `MOVEMENT_MODES` — the ProjectileSpec half is `enemy-defaults.js`'s `createDefaultProjectileSpec()`, **not** a second copy |
| Form state → exportable JSON (drops `projectile` for kind `fx`, drops the spec's art keys) | `modules/fx-export.js` | `buildFxExportObject(state)` |
| Page structure / layout | `index.html` (`#tab-fx`) / `styles.css` | containers only; reuses the `.ef*` widget styles |

**`damage` / `count` are deliberately absent** from the tuning block, and the form says so on
screen. `ProjectileSpec` has neither: damage is a per-shot argument the attacker supplies
(`EnemyAttack.Shoot` passes `tuning.attackDamage` into `ProjectileFactory.Spawn`), and how many
projectiles a volley fires is the *attack's* shape (`BulletHellAttack`'s pattern, the weapon
system's `ShapeModifier`). Adding either here would produce a value silently overwritten on every
single shot — see `projectile-config.schema.json`'s own "SCOPE CORRECTION" note.

**Movement modes are declared ahead of their behaviour.** `movement` takes `Straight` (implemented)
plus `Homing`/`Boomerang`/`Bounce`/`SplitOnImpact` (not). `FxPrefabBuilder` stamps the chosen mode
onto the built prefab as a `RedMagic.Gameplay.ProjectileMovementPlaceholder`, so shipping a real
mover later is adding a component that reads that field — no schema change, no re-export of
existing entries, no importer rework. See "Placeholder movement (Unity side)" below.

### Boss Creator tab

Builds a whole boss — body animations, movement, warnings, attacks with their art, phases — and
exports a `.zip` that Unity imports in one step (**Tools ▸ Web ▸ Import Config… ▸ Importar jefe
completo (.zip)**, `Assets/Scripts/Bosses/Editor/BossBundleImporter.cs`).

**Nothing about a specific attack is hardcoded on the web.** The list of attack types, movement
types, every field with its default/tooltip/min, and every art slot come from
`modules/boss-catalog.js`, which Unity **generates** (`BossCatalogExporter.cs`, by reflection over
every `BossAttack` / `MovementBehaviour` subclass; rewritten after each compile, and on demand from
Tools ▸ Web ▸ Exportar catálogo de jefes). A new C# archetype therefore appears here with no web
edit. `modules/boss-catalog-notes.js` only adds Spanish names/one-liners for known types.

| To change... | Edit | Function |
|---|---|---|
| Which fields/types exist, their defaults, art slots | the C# attack itself (`[ArtSlot]`, `[AttackHides]`, `[Tooltip]`, `[Header]`, `[Min]`) | read by `BossCatalogExporter.Describe` |
| Spanish label / group / one-liner of an attack type | `modules/boss-catalog-notes.js` | `ATTACK_NOTES` |
| What each art kind needs (projectile / fx / warning / prop) and which library kinds it lists first | `modules/boss-catalog-notes.js` | `ART_KIND_NOTES` |
| Form state shape, body stat defaults, phase defaults | `modules/boss-defaults.js` | `createDefaultBossConfig`, `BODY_STATS`, `createAttack`, `createDefaultPhase` |
| Form UI (body lanes + release-frame picker, movement, warnings, attack cards, art used, phases) | `modules/boss-form.js` | `renderBody`, `renderAttacks`/`attackCard`, `renderFields` (generic catalog-field renderer), `artPicker`, `renderArt`, `renderPhases` |
| State → `boss-config.json` (what is exported, relative folders, art ids, problems/notes) | `modules/boss-export.js` | `buildBossExport` |
| The .zip layout | `modules/boss-bundle.js` | `buildBossBundle` |

**Export rules** (`boss-export.js`, mirrored by `BossConfigImporter.cs`):

- Attack base fields and `params` are written **only when they differ from the type default**, so
  re-importing does not undo hand-tuning done in Unity on fields the web never touched. A
  `ProjectileSpec` is written whole once anything in it changes (Unity rebuilds it from
  `new ProjectileSpec()`).
- An art slot set to a library entry becomes `{"art": "<id>"}` plus `artAssets.<id>` (frames +
  kind); empty = the attack's placeholder in Unity (`BossArtBuilder.cs` builds the prefabs).
- Art/body `folder`s are **relative** (`Body`, `Art/<libraryId>`): the importer resolves them
  against `Assets/Art/Bosses/<slug>/Source/`, where the zip's frames land. That is why the loose
  `.json` export can be re-imported later to change only numbers.
- `body` stats and movement apply only when `Boss_<slug>.prefab` is first created; later imports
  relink clips/controller/definition and keep the prefab's hand-tuned values.
- `body.libraryId`/`entryName` and `artAssets.*.libraryId`/`entryName` are web-only (Unity ignores
  them). The full form state is also saved on the library record as `draft`, which is what
  "Editar en Boss Creator" restores; records from the older form (no `draft`) are adopted from
  their config.

Zip layout: `boss-config.json`, `Body/<Animation>/frame_000.png…`, `Art/<libraryId>/<Lane>/frame_000.png…`.

### Entry kinds (enemy / projectile / fx / boss)

Every library entry has a **`kind`** — `enemy` (default), `projectile`, `fx` or `boss` — defined
once in `modules/entry-kinds.js`. The Sprites tab's pipeline (background removal, detect/grid
slicing, box editing, cropping, zip building) is entirely generic and is **shared unchanged** by the
three sheet kinds; the kind only decides:

| | `enemy` | `projectile` | `fx` | `boss` |
|---|---|---|---|---|
| Owns a sprite sheet (`sheet`) | yes | yes | yes | **no** |
| Canonical lanes | Idle / Walk / Attack / Hurt / Death / Wake | Awake / Move / Impact | Spawn / Loop / End | — |
| Pre-created on load | all but `Wake` | `Move`, `Impact` (`Awake` optional) | all three | — |
| Projectile lanes (`Projectiles/` in the zip) | yes | no | no | no |
| Creator tab (`creator`) | Enemy Creator | Proyectil/VFX | Proyectil/VFX | Boss Creator |
| Config file in a bundle (`configFile`) | `enemy-config.json` | `fx-config.json` | `fx-config.json` | `boss-config.json` |

**`boss` is the one kind with no sheet of its own.** A boss is assembled, not drawn: it names an
existing enemy entry as its body/stat baseline (`base.enemyLibraryId`) and its own content is decks
of attacks and the phases that draw from them. `sheet: false` is what keeps it out of the Sprites
tab's type selector (`SHEET_KIND_IDS`) — offering "author a boss sheet" there would produce an entry
whose lanes nothing reads — and it is what gates every sprite-shaped action in the Biblioteca tab
(Cargar en Sprites / Exportar zip / Exportar combinado), since a boss record has no `sprite` block
for them to read.

The lane sets are what each kind's Unity consumer actually needs: the enemy one is
`EnemyAnimation.cs`'s fixed vocabulary; the projectile triad mirrors the shape the project already
ships by hand for the Árbol Ancestral's orb (`Orbe_Idle`/`Orbe_Move`/`Orbe_Impact`); the fx triad
covers both a one-shot (a single `Spawn` lane, loop off — what `VfxOneShot`/`SpriteFlipbook(oneShot)`
consume) and a sustained effect (all three, as named states for `Pipeline.SpriteStateMachine`). A
lane with no frames is skipped by `populateSpriteZip`, so pre-creating all three costs nothing.

**Where it is chosen**: the "4. Qué es" `<select>` in the Sprites sidebar (`#entryKindSelect`,
`app.js`'s `setKind()`/`applyKindToUi()`). Switching kind rebuilds the default lanes — that being
the whole point of the kind — so it confirms first when frames are already assigned, and
un-assigns rather than deletes the boxes (detection + hand editing is the expensive part and is
kind-agnostic). Loading a saved entry adopts **that entry's** kind instead.

**Where it goes**: `kind` is written into the saved record, into `manifest.json`, and into every
exported config (schema: `_shared.schema.json#/definitions/entryKind`, pinned to `"enemy"` by
`enemy-config.schema.json` and to `"boss"` by `boss-config.schema.json`, narrowed to
`projectile`/`fx` by `fx-config.schema.json`). It is **additive** — `enemyName` keeps its name and
position in the manifest for every kind, because that is the key `Assets/Editor/EnemyImporter.cs`
reads, and an absent `kind` means `enemy` so every bundle exported before this field existed still
imports identically.

### Adding one animation at a time to an already-saved entry (Sprites tab)

Sprites now get generated **one animation per image** (its own upload, its own bg-removal/despill
pass, its own detect-or-grid slice into a single lane), arriving separately over time, instead of
one grid sheet holding every lane at once. "💾 Guardar como enemigo" already only required ONE
lane to have frames (`hasAnim`), so authoring a brand-new entry with just its `Idle` lane already
worked. The one real gap was **adding a second animation, from a different image, to an entry
that's already saved** — the old save always overwrote the entry's whole `sprite` (one shared
sheet + box pool), so re-saving with only this session's new lane would have wiped every
previously-saved animation that isn't in THIS session.

**"➕ Añadir a enemigo existente"** (next to "💾 Guardar como enemigo") fixes exactly that, by
**compositing** rather than overwriting: it loads the target entry's existing sheet image (if it
has one), stacks the current session's (bg-removed/despilled) image below it on a taller canvas,
offsets the current session's box coordinates to match, and merges lanes — a lane whose name
matches one already on the target REPLACES it (old frames dropped, new ones take over); every
other lane the target already had is untouched, unmoved, and still points at the same pixels. The
result is saved back through the exact same `{lanes, boxes, sourceDataURL, width, height}` shape
every other part of the app already reads — `populateSpriteZip`, the Biblioteca tab, thumbnails —
so nothing downstream needed to change or learn a second data shape.

| To change... | Edit | Function |
|---|---|---|
| The target picker + button | `index.html` / `app.js` | `#addToExistingSelect`/`#addToExistingBtn` in the Sprites sidebar; `app.js`'s "Añadir a enemigo existente" section |
| The composite-and-merge algorithm (stack images, offset boxes, replace same-named lanes) | `app.js` | `mergeSessionIntoEntry(targetSprite)` |

Because the merge always lands back in the ONE shape the rest of the app already understands,
there is no "legacy vs new" split to track and no export/import path that needs updating — a
`sourceDataURL` growing by composition is just a bigger version of the same sheet the app has
always saved.

### Placeholder movement (Unity side)

`RedMagic.Gameplay.ProjectileMovementPlaceholder` (`Assets/Scripts/Gameplay/`) is stamped onto every
projectile prefab `FxPrefabBuilder` builds. It does exactly two things:

1. **Carries the movement mode** the FxConfig asked for (`Mode`, the same enum member names as
   `fx-config.schema.json#/definitions/movementMode`, written by name and never by ordinal). It is
   the *data*, not the behaviour — when `BoomerangMovement` / `BounceMovement` / `SplitOnImpact`
   exist, each reads this field (or replaces this component outright), and shipping one is swapping
   a component, not re-exporting configs or touching the importer.
2. **Flies straight and despawns when nobody launched it** — i.e. when the prefab is dropped into a
   scene and you hit Play. That is the standalone check it exists for: that the projectile built
   from the web tab came out right, moves, and returns to the pool, without building an enemy to
   fire it.

The moment something really launches it (`ProjectileFactory.Spawn` → `Projectile.Launch`) it steps
aside and never touches anything: it detects that by waiting one frame and checking whether the
`Rigidbody2D` carries velocity (`Launch` writes it synchronously right after `PrefabPool.Spawn`).
When it *does* drive, it disables the `Projectile` component so the two lifetime timers can't race,
and re-enables it in `OnDisable` so the pooled instance goes back unchanged. **It still carries no
tuning**: what flies in game is the shooter's own `ProjectileSpec`
(`EnemyStats ▸ Tuning ▸ projectile`), because the factory rewrites the prefab on every shot.

`FxPrefabBuilder` gets the mode from an **optional `fx-config.json` sitting next to `manifest.json`**
in the entry's source folder (`FxPrefabBuilder.ReadMovementPreview`). Optional by design: a folder
without one builds exactly as it did before this existed. Routing it through the folder rather than
through the callers means the lazy path (an enemy referencing a projectile by `libraryId`) picks it
up for free — `CombinedBundleImporter.LiftEmbeddedLibraryEntries` and `FxBundleImporter` both copy
whole folders, so the config travels with the frames either way, and no call signature changed.

### Projectile art by library id (web ↔ Unity lazy build)

An enemy's `tuning.projectile` can name its art two ways, and the difference is where the prefab
comes from:

- **`prefab`** — a path to a prefab already in the Unity project. Unchanged, original behaviour.
- **`libraryId`** — the id of a `projectile`-kind entry in this library. Unity builds (or reuses)
  that entry's pooled prefab **at import time** and writes it into `ProjectileSpec.prefab`.

The picker for the second one is the "Arte desde biblioteca" row at the top of the Enemy Creator's
inline projectile sub-form (`renderProjectileLibraryRow()` in `modules/enemy-form.js`), which lists
`listEntries('projectile')`. Leaving it on "(ninguno)" is exactly the old behaviour.

**What makes it lazy rather than a two-step import**: `buildCombinedBundle` walks the config it is
about to write and, for every referenced `libraryId`, writes that entry's own `manifest.json` +
frames (+ its own `fx-config.json`, if it has one) into `Library/<libraryId>/` inside the same zip
(`embedReferencedLibraryEntries()`, which collects the ids via `referencedLibraryIds(configObj)` —
`tuning.projectile.libraryId` for an enemy, `base.enemyLibraryId` plus every
`attacks.<id>.params.projectile.libraryId` for a boss, deduplicated). So the enemy's
bundle physically carries its projectile's art, and importing the enemy alone is enough —
`ProjectileConfigImporter` → `FxPrefabBuilder.BuildOrGetProjectilePrefab(libraryId)` finds the
frames already on disk. Unity's `CombinedBundleImporter` lifts those `Library/<id>/` folders out to
the shared `Assets/Art/WebLibrary/<kind>/<id>/` before importing the enemy, because a projectile can
be shared by two enemies and must not be deleted along with either one.

`manifest.json` therefore carries **`libraryId`** too (additive, alongside `kind`): it is what lets
Unity find an entry's source folder by id instead of by folder name, so renaming a folder — or the
entry — never orphans it. Both fields are optional on the Unity side; a manifest without them reads
exactly as it did before.

### Shared entry library (Sprites ↔ the three Creator tabs ↔ Biblioteca)

**Why it exists**: the Enemy Creator's `art` field used to assume a sheet had already been
imported into Unity in a separate prior step. The real workflow is building sprites and config
together in one session — so sprite authoring state now persists in the browser (IndexedDB,
`modules/enemy-library.js`, database `redmagic-enemy-library`, one object store `enemies` keyed by
a generated `id`) and both tabs read/write the same records. Survives a page reload and a browser
restart; each browser profile has its own copy (nothing is synced anywhere).

Records of all four kinds live in the **same** store. `listEntries(kind)` filters (in JS — this
library holds tens of entries, not thousands, and an index would have needed a store migration);
`listEnemies()` still returns everything. A record written before kinds existed has no `kind` field
and is normalized to `'enemy'` **on read** (`normalizeRecord`), not by a stored migration that
would have to rewrite every record on first load — so the DB version stays at 1 and old enemies
never drop out of a filter.

**Record shape** (one per entry):

```
{
  id: string,                // uuid
  kind: 'enemy'|'projectile'|'fx'|'boss',  // absent on pre-kind records -> normalized to 'enemy' on read
  enemyName: string,         // the display name for EVERY kind — NOT renamed, see "Where it goes" above
  createdAt, updatedAt: number,   // epoch ms
  thumbnail: string|null,    // small dataURL for the library cards. A boss borrows its BASE enemy's
                             // thumbnail; null renders as an empty dashed tile, never a broken <img>.
  sprite: {                  // NULL for kind 'boss' — it owns no frames (KINDS.boss.sheet === false)
    lanes: [...],             // same shape as app.js's `lanes` — see "Data model" below
    boxes: [...],             // same shape as app.js's `boxes`
    sourceDataURL: string,    // the FULL sheet (post-bg-removal, if that was run) as a PNG dataURL
    width, height: number,
  },
  config: object|null,        // the last config export object built against this entry — an
                               // EnemyConfig / FxConfig / BossConfig depending on `kind` — or null
                               // until that kind's Creator tab has exported (or saved) once. A boss
                               // record is config-only: it is CREATED by that save, not by Sprites.
}
```

Only `sprite.lanes`/`sprite.boxes` (small JSON) plus one PNG snapshot of the whole sheet are
stored — NOT one crop per frame — because `populateSpriteZip`/`buildExportZip` already crop frames
from a source canvas on demand; reloading `sourceDataURL` into an `Image` reproduces that same
source canvas, so re-exporting a saved entry crops identically to exporting it live never having
left the Sprites tab.

**Sprites tab**: the save button (labelled per kind — "💾 Guardar como enemigo/proyectil/VFX") writes the current `lanes`/`boxes`/sheet snapshot as a
record (`app.js`'s `currentLibraryId` tracks which record the currently-loaded sheet came from, so
re-saving updates it in place instead of duplicating it — reset to `null` only on loading a brand
new image file). The saved-entries panel below it (`#spritesLibraryList`, `renderSpritesLibraryList()`)
lists the records **of the currently selected kind only** — it is a "pick up where I left off"
shortcut for this sheet's kind, and its Cargar would otherwise switch kinds out from under the
tab; the Biblioteca tab is where every kind is browsed together — with Cargar (reopens for editing — replaces
`img`/`boxes`/`lanes` and rebinds `currentLibraryId`) / Exportar zip (existing sprite-only format,
unchanged) / Eliminar.

**Enemy Creator tab**: the `art` field is a mode switch. **"Desde biblioteca"** (default) is a
dropdown of every saved record **of kind `enemy`** (`listEntries('enemy')` — a projectile/VFX sheet
is a valid entry but not a valid `art` for an enemy: `EnemyFactory` reads that sheet for the
enemy's own body clips and its thrown prop, neither of which those sheets have); picking one sets `state.artLibraryId` (the config draft links to
that record by id — it does not copy or duplicate its sprite data) and pre-fills `enemyName` if
still empty. The exported `art` string in this mode is computed
(`modules/enemy-export.js`'s `libraryArtPath()`) as
`Assets/Art/EnemyImports/<enemyName>/<enemyName>.sheet.asset` — the exact path
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

**Projectile/FX Creator tab**: same shape, one level simpler — the art picker *is* the link, so
there is no mode switch. Picking an entry sets `state.libraryId`, and every export
(`persistConfigToLibrary`) writes the built FxConfig onto **that same record**, which is what makes
it show up in Biblioteca with a `config` badge. See the tab's own section above.

**Boss Creator tab**: the only tab that **creates** a record rather than filling one in. A boss has
no sheet, so there is nothing for the Sprites tab to have saved first: "💾 Guardar jefe en
biblioteca" calls `saveEnemy({kind:'boss', sprite: null, config: exportObj})` the first time and
`updateEnemy(id, …)` after that (the tab's own `currentBossId`, same role as `app.js`'s
`currentLibraryId`).

**Biblioteca tab** (`#tab-library`, `renderLibraryTab()` in `app.js`): every saved record of every
kind in one place, behind a kind filter bar (`#libraryFilterBar`, `renderLibraryFilterBar()`, built
from `KIND_IDS` — Todo / Enemigos / Proyectiles / VFX / Jefes), reusing
`modules/library-panel.js`'s card renderer with the full action set — Cargar en Sprites, **Editar
en …** (`editActionFor(entry)`, which reads `KINDS[kind].creator` to pick both the label and the
tab to switch to, then calls that creator's `linkLibraryEntry`), Exportar zip, Exportar combinado,
Exportar config, Eliminar. Actions that don't apply are **disabled with an explanatory tooltip**
rather than hidden — a missing button reads as a bug, a disabled one explains the model — and the
three sprite-shaped ones are gated on `KINDS[kind].sheet`, so a boss card can't reach code that
would read a `sprite` block it doesn't have. Each card badges its kind first (and, for a boss, its
attack/phase counts instead of an always-zero animation count), so a mixed "Todo" listing is
readable without opening anything.

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
