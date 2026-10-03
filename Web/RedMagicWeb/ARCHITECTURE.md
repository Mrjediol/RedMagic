# Enemy Sprite Extractor — architecture

No build step: plain HTML + vanilla JS ES modules, served as static files. Open `index.html`
through a local static server (ES module imports are blocked under `file://` by browser CORS
rules — see the bottom of this doc for a one-line server command).

This file is the primary reference for "I want to change X" requests. **Keep it accurate**: when a
module's responsibility shifts, update its row in the same change.

**Shell conveniences** (Ctrl+S, Ctrl+K finder, Space = Probar, unsaved-changes dot, toast): `modules/shell-tools.js` — it knows no tab; `app.js` hands it one function per tab (`saveActions`, `playActions`, `paletteItems`, `dirtyTabs`). Library backup: `enemy-library.js ▸ exportAllRecords / restoreRecords`, `map-library.js ▸ exportAllMapRecords / restoreMapRecords`.

**Look and layout rules** (tokens, type, tab groups, what a panel may and may not do): `DESIGN.md`.

## Module map

| To change... | Edit | Function |
|---|---|---|
| How background removal picks the key color / flood-fills | `modules/background-removal.js` | `detectKeyColor(imageData)` (dominant border color), `removeBackground(imageData, tolerancePercent, { keyColor })` — any chroma key, not only magenta |
| How the key-colored halo is cleaned off the edge | `modules/despill-edge.js` | `despillEdge(imageData, { keyColor, threshold, edgeRadius })`: spill = min(key channels) − max(other channels); above `threshold` the edge pixel is deleted, above 0 its key channels are clamped (green → G = max(R,B)) |
| Where the key color is chosen (presets / picker / eyedropper / auto) | `app.js` | `keyColor()`, `setKeyColor`, `autoDetectKey` — one key feeds flood-fill, pockets and despill |
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
| Tab switching (Sprites / Enemy Studio / Enemy Creator / Proyectil-VFX / Boss Creator / VFX Lab / Biblioteca / Map Tracer) | `app.js` | top-of-file `.tabBtn` click wiring; `creatorFor(kind)` maps a kind to its creator-tab API via `KINDS[kind].creator` |
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
| The **"Defensa" section** (defensive moves): cards with when / how / gesture at hand and the numbers folded; `state.defences` + `defencesTouched` (an edited empty list is exported to clear them) | `modules/enemy-form.js` / `modules/enemy-defaults.js` / `modules/enemy-export.js` | `renderDefences()`, `createDefence()`, `applyDefences()` → `tuning.defences` |
| The **"Ataques" section** (the enemy's deck): the built-in attack card (`builtInAttack` / `builtInWeight`), the catalog attack cards with their per-card deck rows (min/max range, target height), which gestures a card can pick (`bodyGestureNames()`: a rig's `draft.gestures`, or a sheet's animation lanes), the options of the art the attacks use | `modules/enemy-form.js` | the "ataques (la baraja)" block: `createAttackEditor({...})` call (`typeFilter` hides catalog `hosts: 'boss'` types, `hiddenCommon` hides boss-only base fields), `renderExtra`, `renderDeckArt()`, `exportContext()` |
| The attack card itself (catalog-driven fields, art pickers, type picker, add/duplicate/reorder/remove) — shared, not Enemy-Creator-specific | `modules/attack-cards.js` | `createAttackEditor(o)` → `{ render, refreshGestures }`, `newAttack()`. **Boss Creator still draws its own copy in `boss-form.js`** (left untouched on purpose); when that tab is reworked, move it onto this module |
| Attack cards → the `attacks` / `artAssets` blocks (only-what-differs rule, art registry), and the reverse (saved config → cards) | `modules/attack-export.js` | `buildAttacksExport()`, `adoptAttacksFromConfig()` — same rules as `boss-export.js` |
| What the deck adds to the exported EnemyConfig (`tuning.attacks`, `builtIn*`, `attacks`, `artAssets`) — and that an enemy WITHOUT a deck exports exactly the old document | `modules/enemy-export.js` | `buildEnemyExport(state, ctx)` (`buildExportObject` = its `.doc`) |
| The frames of the deck's art inside a bundle (`Art/<libraryId>/<State>/`), for both the combined sprite zip and the rig zip | `modules/enemy-bundle.js` | `embedAttackArt(zip, configObj)` — read by `EnemyDeckImporter.cs` / `BossArtBuilder.cs` |
| Loading a saved behaviour into the form when an entry is picked, sent from Biblioteca or linked by Enemy Studio (the form is built once; widgets follow the state through `bind()`) | `modules/enemy-form.js` | `adoptConfig()`, `loadSavedBehaviour()`, `bind()` / `syncers`, `layerMaskRow()` |
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

### VFX Lab tab (✨, parametric effect presets)

Effects drawn in code from parameters and **baked into a normal frame sheet**, saved as an ordinary
`projectile` / `fx` library entry — so every existing art picker and export uses them unchanged.

| To change... | Edit | Function |
|---|---|---|
| The first five presets (`orb`, `bolt`, `burst`, `charge`, `aura` — frozen: saved entries reopen with their params), the families | `modules/vfx-presets.js` | `PRESETS` (those five + `...EFFECTS`), `FAMILIES` — a preset is `{ id, family, label, kind, description, defaults, params, lanes: [{ name, loop, fps, frames, draw(ctx, k, p, rnd) }] }` |
| The effects library (40 presets), by family | `modules/vfx-effects.js` | `PROJECTILES`, `BURSTS`, `AURAS`, `TRAILS`, `GROUNDS`, `MUZZLES` → `EFFECTS`. A new effect is one more entry in its family's array. One-shot pieces shared by a projectile's `Impact` and the stand-alone burst: `explosion`, `shatter`, `splash`, `arcBurst`, `debris`, `hitSpark`; shared loops: `flamesRow`, `risers`, `spiralArms`, `bubbleShield`, `reticle`, `zone`, `runeCircle`, `beam`, `iceSpikes` |
| Shared drawing helpers | `modules/vfx-draw.js` | light: `glowDot`, `ring`, `core`, `sparkShape`, `burst`, `shockRing`, `flare`, `cone`; shapes: `streak` (teardrop), `sliver`, `shard`, `crescent`, `boltPath` + `lightning`, `puff`, `flame`, `droplet`, `bubble`, `blob`, `chunk`; colour: `mix`, `shade`, `ramp(p, t)` (core → glow → dark edge); motion: `loopNoise` (periodic), `emit` (wrapping lifetimes), `bell`, `pop`, eases; `solid(ctx, fn)` = normal blending for dark / opaque shapes |
| The parameters every preset shares (size, colours, glow, sparks, speed, length, pixelation, seed) | `modules/vfx-presets.js` | `COMMON_PARAMS`, `normalizeParams()` |
| How a preset becomes a sheet (one row per lane, the Sprites tab's `sprite` block shape) | `modules/vfx-presets.js` | `bakePreset()`, `drawFrame()`, `frameCount()`, `laneFps()` |
| The tab: gallery by family in its own column (`#vfxGalleryRoot`, built once, with a search box), form, live preview, backdrops, contact sheet, save / update in place / variant, the list of saved presets | `modules/vfx-lab.js` | `initVfxLab()` — `thumbOf` (gallery stills, cached), `BACKDROPS`, `contactSheet()` ("Ver todos los frames"); the record keeps `draft.vfxPreset = { id, params }` to be reopened |
| "▶ Probar": the effect played as the game plays it (a projectile flies from a caster to a dummy and bursts on it; an fx runs Spawn → Loop → End), from the **baked** sheet, with its sounds | `modules/vfx-stage.js` | `buildTimeline(preset, baked)` (the segments: lane, seconds, positions, sound), `createStage()` (canvas, play / stop, the sounds list with ▶ and ⬇ .wav), `playSound` (Web Audio; no Web Audio = silent, no error) |
| The example sounds (synthesised in code, DOM-free) and which moment of which preset plays which | `modules/vfx-sounds.js` | `SOUNDS` — `{ label, seconds, loop?, layers }`, layers = `tone` / `noise` (filtered, gliding) / `pings` / `clicks`; `PRESET_SOUNDS[preset] = { cast?, <Lane>: soundId }` (a looping lane needs a `loop` sound); `renderSound(id[, def])` → samples, `encodeWav()`; a tone can take `fm: [ratio, depth]` (metal, bells, coins) |
| The GAME's sounds: the palette that fills the Sounds tab's slots, and the rules that say which slot gets which | `modules/game-sounds.js` | `GAME_SOUNDS[id] = { family, label, seconds, loop?, layers }` (same synthesiser), `GAME_FAMILIES` (label + length budget), `GAME_ASSIGN` (ordered regex rules → clip, pitch range, volume; applied in Unity by `SoundPaletteAssigner`). `<id>_2`, `_3` = options assigned to nothing |
| Driving ComfyUI (enemy art): the "Claude" workflow as data, and one graph → editor format + API format | `tools/comfy/claude-workflow.mjs`, `tools/comfy/comfy-graph.mjs` | `claudeGraph(options)` = nodes + groups; `toUi` / `toApi` build from Comfy's own `/object_info`. `install` writes Comfy's `workflows/Claude.json`, `run` queues it (Comfy Desktop open, `http://127.0.0.1:8188`). Plan: `docs/plan-comfyui-enemy-workflow.md` |
| Listening to the game's sounds ("🔊 Sonidos del juego", in the VFX Lab tab) | `modules/game-sounds-panel.js` | a `<dialog>`: a section per family, ▶ / ⬇ per sound, "asignado" vs "opción"; plays through `playSound` of `vfx-stage.js`. Edits nothing |
| The game's own effects and sounds, baked without a browser | `tools/bake-vfx.mjs` + `tools/vfx-bakes.json`, `tools/bake-sounds.mjs` | node + `@napi-rs/canvas` (`npm install` in `tools/` once). Frames → `Assets/Art/VfxLab/<name>/`, then Unity's `VfxLabArtPack` builds and wires the prefabs (`docs/architecture/fx-placeholders.md`); Lab sounds → `Assets/Audio/Sfx/VfxLab/*.wav`, assigned by hand; game sounds → `Assets/Audio/Sfx/Game/<family>/*.wav` + `palette.json`, assigned by Unity's `Tools ▸ RedMagic ▸ Audio ▸ 4`. `tools/check-sounds.mjs` = the numeric checks the bake enforces (`npm run check-sounds` prints the table) |

Rules every preset keeps (the headless checks assert them):
- `draw` works in a centred, **y-up unit square** (1 = the frame edge), never in pixels — that is what keeps
  "Nitidez" working. The canvas arrives additive (`lighter`); anything dark or opaque goes inside `solid()`.
- `kind` + lane names are canonical — `projectile`: `Move` (loop) / `Impact`; `fx`: `Spawn` / `Loop` / `End` —
  because `rig-fx.js ▸ pickLane` and the art pickers look for those names.
- A looping lane draws the same picture at `k = 0` and `k = 1` (whole turns, `loopNoise`, `emit`). Rotating
  N-fold shapes may turn `TAU / N` per loop **only if the N copies are identical** (colour included).
- A one-shot has no empty frame; nothing touches the frame edge at default parameters (stay inside ±0.95).
- A projectile points to +X. Effects that stand on the ground keep it low in the frame (`GROUND`); flat ones
  (zones, puddles, circles) are centred.
- Pale light over a bright scene (ice) needs a darker shape of its own colour under it (`streakUnder`, the
  `solid` rims of the shield / mark): additive glow alone disappears on light backgrounds.

### Enemy Studio tab (🧬, one workflow from parts to a finished enemy)

Plan and phases: `docs/web-enemy-studio-plan.md` (all six built). **The one place where a part-based
enemy or boss is made**, body and behaviour: the old 🦴 Rig Composer tab is gone (its editor,
`rig-composer.js`, is the Studio's body editor and renders into hosts that live in `#tab-studio`);
the 🧩 Enemy Creator tab stays for **sprite-sheet** enemies only and no longer lists part-based ones.
The behaviour form is still one (`enemy-form.js`): while the Studio is visible it **borrows** the
🧩 tab's two panels for step 4 (`mount`) and gives them back on leaving (`unmount`); entering the 🧩
tab lets go of a part-based enemy the Studio left linked (`unlinkRig`).

Layout: left = steps (one open at a time), centre = the composer's canvas, right = the composer's
inspector — or, in step 4 of an enemy, the behaviour form.

| Step | Shows | Borrowed panel |
|---|---|---|
| 1 Cuerpo | document, add parts, parts list | `#rigSidebarBody` |
| 2 Plantilla | template in use, "Nuevo desde plantilla" (built-in + ★ saved), "Guardar como plantilla" | `#rigSidebarTemplate` |
| 3 Efectos | the library's projectiles / fx as a palette: ＋ hangs one at the instant the open gesture's playhead is on; dragged onto the timeline, at the instant it is dropped | — (own) |
| 4 Comportamiento | the state-sprites board; enemy: the Enemy Creator form (right) + validation / JSON (left); boss: the Boss Creator link | `#rigInspectorStates`, `#enemyFormPanel`, `#enemyPreviewPanel`, `#rigInspectorLinks` |
| 5 Exportar | save, .zip, rig-config preview | `#rigInspectorExport` |

Steps 3–5 are locked until the body is usable (a name, at least one part with art, no required
template slot empty); step 2 is always open (it is also a way to start).

| To change... | Edit | Function |
|---|---|---|
| Steps, their locks / ticks / state text, which panel the right column shows | `modules/enemy-studio.js` | `STEPS`, `locked`, `usable`, `render` |
| Where the composer's hosts go, and which panels are borrowed | `modules/enemy-studio.js` + `app.js` + `index.html` | `slots` (the five step hosts of `#rigHosts` are moved once at start-up); `mount` / `unmount` for the two Enemy Creator panels; the `nodes` passed by `app.js`. The tab-switch handler calls `enemyStudio.show()` / `hide()` **before** anything else |
| The body ↔ behaviour glue | `modules/enemy-studio.js` | `enterBehaviour` (saves the body, links the form to its record), `flushBehaviour` (form → record's `enemyConfig`, no download) — also the composer's `hooks.beforeExport` / `afterSave`, only while the Studio is on screen |
| What the composer / the form expose for it | `modules/rig-composer.js`, `modules/enemy-form.js` | composer: `hooks`, `getStatus()`, `save()`, `linkLibraryEntry()`, `useTemplateRecord()`, `addFxToOpenGesture()`; form: `linkedId()`, `persistLinked()`, `setEnemyName()`, `unlinkRig()` |
| Biblioteca ▸ "Editar en Enemy Studio" (a part-based enemy: open it; a body template: a new enemy from it) | `modules/enemy-studio.js` | `linkLibraryEntry(id)` — `KINDS.rig.creator` and `KINDS['rig-template'].creator` are both `studio` |
| The templates gallery (cards: saved ★ ones with thumbnail, then the built-in shapes) | `modules/rig-composer.js` | `renderTemplateBlock()` (`.rigTplGallery`) |
| What the Studio hides of the borrowed panels (tab titles, the form's sheet-only rows) | `styles.css` | the `#tab-studio …` rules (`.rigTabIntro`, `.efArtIdentity`, `.efSheetOnly`…) |

**User templates** (`rig-template` kind): "Guardar como plantilla" turns the composed document into
a template in the same data shape as the built-ins (`rig-templates.js` ▸ `templateFromState`): one
slot per part (placement, pivot / muzzle / grip / centre guesses read from this art, roles, sway),
gestures rewritten **by slot id**, and the behaviour (the rig record's `enemyConfig`) carried whole
as `behaviour.config`. **No art is stored.** The template object is the record's `draft`; the record
has no `config` and never goes to Unity. `templateFromRecord` gives it the id `lib:<record id>`.
On use, "Con sus gestos" / "Con su comportamiento" choose what comes along
(`instantiateTemplate(template, take)`); a user template's behaviour lives in the new document
(`state.template.behaviour`) and is what `suggestedEnemyConfig` returns until the form saves its own.
"Traer sólo sus gestos a este cuerpo" (`importGestures`) lays a template's gestures over the body on
screen, matching parts by slot id → role (the aim part, the n-th limb…) → name, lengths rescaled.

**Gestures ⇄ attacks** (2026-10-02). An attack is made with a gesture and launches something at each of its
releases. The gestures are the composer's document, the attacks the behaviour form's state: `enemy-studio.js`
is where they meet.

| To change... | Edit | Function |
|---|---|---|
| A list of objects in an attack card; **one slot per release** of the attack's gesture (`perRelease`); fields that show only for some values of another (`showIf`) | `modules/attack-cards.js` | `renderList`, `releasesOf`, `renderFields` (`o.gestureInfo(name)` → `{ releases: [label…] }`). Spanish labels: `boss-catalog-notes.js` ▸ `FIELD_NOTES` (by path, `'shots.kind'`) |
| Lists in defaults / export / reopening a config | `modules/boss-defaults.js`, `modules/attack-export.js` | `defaultsOf` / `withDefaults` (`objectList`), `fullFields` (a list is always exported whole) |
| What a gesture releases, and keeping every card's slots at that count | `modules/enemy-form.js` | `bodyGestures` (live from the Studio via `setGestureSource`, else the record's draft), `gestureInfo`, `syncReleaseSlots` (also run before every export) |
| The attacks of a gesture inside its card (Gestos ▸ a gesture ▸ "⚔ Ataques que usan este gesto") | `modules/enemy-form.js` + `modules/enemy-studio.js` | `renderGestureAttacks` (the deck's own field renderer, drawn a second time), `gestureShots`; the Studio's `rig.hooks.gestureAttacks`, `ensureLinked`, `signatureOf` (a release added → `enemy.gesturesChanged()`) |
| **The test stage**: "▶ Probar" fires the gesture's attack at each release towards a draggable target | `modules/rig-composer.js` | `stageAttack`, `armStage`, `fireReleases`, `fireShot` (= `ReleaseAttack.Fire`), `tickShots`, `stageShapes`; `lead()` = the attack's telegraph. Drawing: `rig-canvas-view.js` ▸ `_drawShapes`. Units: the body is drawn at 1 and plays at `rootScale`, so world numbers are × `1 / rootScale` |
| The old "own attack with another gesture" cards of a saved config → a `ReleaseAttack` | `modules/enemy-form.js` | `attackFromVariant` (in `adoptConfig`) |
| A defence that launches an attack; a card that is "only from a defence" | `modules/enemy-form.js`, `modules/enemy-export.js` | `renderDefences` (`d.attack`), `attack.deck.defenceOnly`; `buildEnemyExport` |

A step marked **`aim`** ("🎯 Acaba apuntando al objetivo") turns the aim part, after the gesture's poses, until its
barrel (pivot → muzzle) passes through the stage's target, by that step's weight — exact at full weight, no
`aimMaxAngle` limit (`rig-composer.js` ▸ `previewPoses`, = `RigDriverCode.ApplyGestureAim`; `rig-gestures.js` ▸ `aimWeight`).
A step that "lasts the warning" has no time of its own: its field and its edge edit the **warning** — the
telegraph of the attack that uses the gesture when there is one (`host.setLead` → `enemy.setGestureTelegraph`).

Not simulated on the stage: homing, pierce, impact explosions, and the catalog attacks that are not made of
shots (they start at the first release; their numbers are edited in step 4). A gesture's sounds come from
Unity or are set here; the stage plays them only for an enemy opened through the bridge (below).

**The second door — 🎬 "Gestos y ataques"** (tab id `gestures`, no panel of its own: `app.js` ▸ `TAB_PANELS`
shows `#tab-studio`). `enemyStudio.show('focus')`: the steps are hidden, the left column is `#studioFocus`
(enemy picker, state sprites, save / export), the composer is in **focus mode** (`rig-composer.js` ▸
`setFocusMode`: parts cannot be moved, added or deleted; only pose tools; the inspector is the Gestures
section) and the right column shows the gestures with the form's *Ataques* and *Defensa* sections under them
(CSS: `#tab-studio[data-mode="focus"] …`). Its export is **partial** — `rig-export.js` ▸ `buildGesturesExport` /
`buildGesturesBundle` / `partialEnemyConfig` (`"partial": true`; only gestures, idle gestures, state sprites,
deck, defences) — read by `RigBundleImporter.ImportPartial` over an existing prefab. Nothing is implemented
twice: same composer, same timeline, same form.

**The Unity bridge** (plan: `docs/plan-unity-web-bridge.md`; Unity side: `Assets/Scripts/Rigging/Editor/Bridge/`).
Unity opens the page with `?bridge=<port>&token=<t>&prefab=<asset path>[&gesture=][&attack=<index>]`: the 🎬 tab
loads that enemy **from the project** and gains "💾 Guardar en Unity". Opened by hand, none of it exists.

| To change... | Edit | Function |
|---|---|---|
| The calls to Unity (`/ping`, `/enemy`, `/file`, `/gestures`), the query params, the one retry | `modules/unity-bridge.js` | `bridge`, `fetchEnemy`, `fetchFile` / `fetchDataURL`, `saveGestures`, `BridgeError` (`status` 0 = no answer, 409 + `reason`) |
| Opening straight on 🎬 | `app.js` | `restoreLastTab` (`bridge.active` → `enemyStudio.openFromUnity()`) |
| What is loaded and from where; the banner, the save button, Unity's report, "Cambió en Unity" | `modules/enemy-studio.js` | `openFromUnity`, `saveToUnity`, `rig.hooks.focusExtras` |
| A body drawn from Unity's rig-config (nodes, sprites, pivots, roles, state sprites) | `modules/rig-export.js` | `readRigDocument` — the document is marked `unityBody` (a full export of it is refused) and every image keeps `unityRef`, which the partial export writes back instead of a PNG |
| Unity's gestures over a library body; opening a gesture by name; the zip that is posted | `modules/rig-composer.js` | `applyUnityGestures`, `openGestureNamed`, `buildGesturesZip`; the sound moments: `armStage` / `playGestureSounds` |
| Art that is a prefab of the project (`unity:<path>` in a card, `{ "project": … }` in JSON) and its library preview | `modules/unity-art.js` (+ `attack-export.js`, `attack-cards.js`) | `unityArtValue`, `artLibraryId`, `unityArtRef`; `adoptAttacksFromConfig` keeps each card's `assetPath` so it is saved into the same asset |
| Playing a clip file; the project's clips | `modules/vfx-stage.js`, `modules/unity-bridge.js`, `modules/enemy-studio.js` | `decodeAudio`, `playBuffer`; `fetchSounds`; `clipBuffer`, `rig.hooks.soundClips` / `playClip`, `rig.applyUnitySounds` |

Rules: Unity owns what it has — on open, gestures and the deck always come from the prefab, even when the library
has the enemy (the library record is then updated to match). The body comes from the library record of that
name when it was authored here, else from Unity. Saving never touches the body or the rest of the tuning. A
gesture's sounds are edited here (clips, volume, pitch, instant of each) and saved with the gesture. The effect of a gesture that is a Unity prefab keeps its path (`fx.prefab`) and is exported
as it came; when Unity says which library entry it was built from, that entry is only its preview.

### Rig composer (the body editor of Enemy Studio — part-based enemies and bosses)

No tab of its own. Its panels are six hosts (`#rigSidebarBody`, `#rigSidebarTemplate`, `#rigInspectorMain`,
`#rigInspectorStates`, `#rigInspectorLinks`, `#rigInspectorExport`) so Enemy Studio can borrow them; the scrolling element
is whatever column holds them (class `scrollHost`).

**State sprites** : per part, `variants = { asleep, awake, attacking, stages, named: { alert, hurt, dead },
gestures: { <gesture name>: image } }`. `rig-model.js` owns the vocabulary and the rule: `SPRITE_STATES`,
`variantOf` / `setVariant`, `stateImage` (the fallback chain, a copy of `ProceduralRig.RefreshSprites`),
`renameGestureSprites`. The **board** (`rig-composer.js` ▸ `renderStatesBoard`, host `#rigInspectorStates`,
shown in Enemy Studio's step 4): pick a state — the canvas shows the body in it — and give each part its
sprite; the per-part section "Sprites por estado / etapa" edits the same data plus the boss stages. The
canvas shows a gesture's sprites while it plays and the hurt ones during "💥 Golpe". Export:
`spriteSwaps[].named` (a sprite for a gesture that no longer exists is reported and left out).

**Idle gestures**: `state.idle = { gestures: [names], interval, chance, windup }` (`rig-defaults.js` ▸
`createDefaultIdle`). The "💤 Gesto de reposo" checkbox on a gesture card adds / removes its name; the
"💤 En reposo" fold under the list has the three numbers and "▶ Probar uno". Renaming / deleting a
gesture keeps the list in step; export = `idleGestures` (names that no longer exist drop out).

**When a gesture sprite shows**: `variants.gestureWindows[<gesture>] = { from, to }` in **steps** (0–1 the
first step, 1–2 the second… the last stretch, `steps` → `steps + 1`, is the way back; absent = the whole
gesture) — `rig-model.js` ▸ `gestureWindow` / `setGestureWindow` / `gestureMoments(gesture)`, honoured by
`stateImage(…, moment)`. UI: "Sprites de este gesto" in the gesture card (`renderGestureSprites`), two
dropdowns per part; the canvas gets the moment the playhead is on (`gestureLayer().moment` →
`opts.gestureMoment`). Export: `named[].from` / `to`. A draft saved with the old 0–3 scale is converted on
load (`adoptRigState`, same rule as `ProceduralRig.FixConvertedWindows`).

**Effect resolution**: VFX Lab's "Nitidez" (`detail`) bakes `density` times more pixels per frame for the
same effect (`bakePreset`, capped at 512 px a frame) and stores `sprite.density`; everything that sizes an
effect on a body divides by it (`rig-fx.js` ▸ `fxDensity`: `worldWidth` of the art asset, the canvas
preview). Re-save an old effect in the Lab to rebake it sharper; its size does not change.

**"⏹ Dejar quieta"** (part inspector ▸ Balanceo): `makeStill` zeroes the part's sway, scale pulse and lean
follow and takes it out of every gesture pose — for a template slot that is not a flame / tail in this enemy.

**Gestures are timelines** (the same shape as Unity's `RigGesture`):
`{ name, recover, steps: [{ name, duration, ease, stretch, shake, poses[] }], releases: [{ step, at, point, offset }], fx[] }`.

| To change... | Edit | Function |
|---|---|---|
| The data, its defaults, the old two-pose shape → timeline | `modules/rig-gestures.js` | `createGesture` / `createStep` / `createRelease`, `normalizeGesture` (= `RigGesture.OnAfterDeserialize`), `insertStep` / `removeStep` / `moveStep` (keep releases and effects on their step) |
| How a gesture runs in time | `modules/rig-gestures.js` | `stepDurations` (= `RigGesture.Durations`: stretch steps share the warning up to the first release), `timeline`, `gestureAt` (weights per step = `GestureRoutine` / `SettleRoutine`), `applySteps` (= `ApplyPose`, short turns on the way back), `instantAt` / `timeOf` |
| Effects: the hook, which lane plays, when it is alive, its `artAssets` entry | `modules/rig-fx.js` | `createFx`, `pickLane`, `activeFx` (= `FireFx` / `EndFxStep`), `fxArtAsset`; `fxDensity` |
| **The timeline editor** (dock under the canvas + the selected thing's fields): step blocks (click, drag to reorder, drag the edge = duration), ◆ releases and ✨ effects dragged along time, scrub ruler, ▶ | `modules/gesture-timeline.js` | `createGestureTimeline(host)` → `renderDock`, `renderFields`, `updatePlayhead`, `addFx`, `select`. **Canvas-agnostic**: everything about the body goes through `host` (documented at the top of the file), so it can be mounted elsewhere |
| What the canvas does with it (the host) | `modules/rig-composer.js` | the `createGestureTimeline({...})` call; `head` (playhead) / `gestureLayer` / `layerAt`; pose editing `startPosing` / `beginPoseDrag` / `renderPosePanel`; `fxOverlay` + `fxMatrix` (facing direction / anchor / world, layer behind); `releaseMarks` / `releaseMatrix`; `canvasHandle` (drag the selected release / effect on the canvas) and `placeAt` ("⌖ Colocar") |
| Drawing effects behind / in front and the release targets | `modules/rig-canvas-view.js` | `_drawFx(opts, behind)`, `_drawMarks` |
| rig-config ⇄ gestures | `modules/rig-export.js` | `buildRigExport` writes `steps / releases / recover / fx`; `readRigGestures(doc, state)` reads them back by part path (Unity's export, an old classic export) — Gestos ▸ "⬆ Traer de un rig-config.json" |

**The timeline behaves like a video editor's** (`gesture-timeline.js`): − / + or Ctrl+wheel zoom the track (it
scrolls; drags and the clock go down to the millisecond), ← / → move the playhead one frame of 1/60 s (Shift:
ten), Ctrl+← / → move what is selected, Ctrl+D duplicates it (`duplicateSelected`), Ctrl+B cuts the step under
the playhead in two (`splitAtPlayhead` → `rig-gestures.js` ▸ `splitStep`: the first half ends in the pose
the body has at that instant), Supr removes it. **The fields of what is selected are a panel of their own**
(`rig-composer.js` ▸ `renderSelection`, the element `selectionPanel`; Enemy Studio puts it in the left column
of both doors), titled by kind: step, ◆ attack output, ✨ effect output, 🔊 audio output. **The sounds of a
gesture** are a lane each with one marker (`gesture.sounds = [{ clips, volume, pitchMin, pitchMax, step, at }]`,
`createSound`; absent = unknown, [] = none; "🔊 Sonido" / "＋ Otro sonido" add one, Ctrl+D duplicates), dragged
like an effect; the stage plays each at its instant (`armStage` / `playGestureSounds`); its clips are files of the Unity
project, so picking / hearing them needs the bridge (`host.soundClips` / `host.playClip`). **A gesture with no
release written shows no release** unless an attack uses it (`host.attackUses`): idle, defence and transition
gestures release nothing. **Transition gestures** (`state.transition`, `createDefaultTransition`): the "🔁"
checkbox on a gesture card and the "🔁 Entre ataques" fold; export `transitionGestures`.

**Lane heads**: a narrow column at the left of the track names each lane (⏱ Tiempo, ▭ Pasos, ◆ Ataque, 🔊 Sonido,
✨ VFX n) — `renderDock` ▸ `head()`, CSS `.gtHeads` / `.gtHead` (each head reuses its lane's class, so the heights
match). The 🔊 lane is always there: empty, it is the "add a sound" button. **Library sounds** (kind `sound`,
`modules/sound-library.js`): an audio file uploaded from 📚 Biblioteca ▸ "⬆ Subir sonidos…" or from the sound
panel's "⬆ Subir archivo…". A gesture's clip is an asset path of the Unity project or `lib:<record id>`; the
picker lists both (`rig-composer.js` ▸ `soundClips`, `playClip`, `uploadSound`). On export a library clip is
written into the bundle as `Audio/<file>` (`rig-export.js` ▸ `clipOut`) and Unity copies it into
`Assets/Audio/Sfx/Enemies/<name>/` (`RigBundleImporter.CopyBundleAudio`) — through the bridge or a .zip alike.

Rules: a step's pose is measured from **rest**, not from the previous step. An effect whose `fx` is null carries a
Unity prefab path in `prefab` (read back from Unity): exported as it came, no preview. Templates carry gestures by
slot id (`rig-templates.js` ▸ `gestureIn` / `fxMapped`); the built-in ones are still written as two poses and are
converted when they enter a document. The dock's DOM id is `#rigTimeline` (hidden while no gesture is open).
Changing how a gesture plays in Unity = change `RigDriverCode.cs` / `RigDriver.cs` **and** `rig-gestures.js` / `rig-fx.js`.

Assembles an enemy or boss from **separate part PNGs** and exports the `.zip` that Unity's
**Tools ▸ Web ▸ Rig ▸ Importar rig** reads (`Assets/Scripts/Rigging/Editor/RigConfig/RigBundleImporter.cs`).
Separate from every classic tab: it touches none of them, it only reuses shared helpers
(`background-removal.js`, `despill-edge.js`, `sprite-detection.js`, `enemy-form-fields.js`,
`config-schema.js`, the library). Contract: `schemas/rig-config.schema.json` +
`docs/web-tools-guide.md ▸ Rig`.

| To change... | Edit | Function |
|---|---|---|
| Part / document defaults, the ProceduralRig fields shown and their C# defaults | `modules/rig-defaults.js` | `createPart`, `createDefaultRigState`, `RIG_FIELD_GROUPS` — **keep in sync with `ProceduralRig.cs` initializers** |
| Matrices, world transforms, reparent / pivot moves that keep the art in place | `modules/rig-model.js` | `worldMatrix`, `reparent`, `setPivot`, `pixelToLocal` |
| Loading a PNG into a part (auto background removal when the corners are opaque, trim), splitting a sheet of parts | `modules/rig-model.js` | `importImage`, `splitPart`, `initialPosition` (layers of one shared canvas land already assembled: canvas centre = origin) |
| State sprite alignment (asleep/awake/attacking/stages) | `modules/rig-model.js` | `variantPivot` — same file size as the part = same pivot pixel |
| Canvas: camera, drawing, pixel picking, overlays (pivot, centre, point/barrel arrows, muzzle, grips, collider) | `modules/rig-canvas-view.js` | `RigCanvasView` |
| Tools, parts list (draw order, parenting by drag), inspector, preview (sway/hover/lean/hurt = `RigDriverCode` formulas), save/export | `modules/rig-composer.js` | `initRigComposer` |
| State → `rig-config.json`, the zip layout, companion `enemy-config.json` / `boss-config.json` | `modules/rig-export.js` | `buildRigExport`, `buildRigBundle`, `companionEnemyConfig` |
| Body templates: the shapes offered by "Nuevo desde plantilla", their slots, default sway / timings / gestures, suggested behaviour | `modules/rig-templates.js` | `RIG_TEMPLATES` (**data**: a new template = a new object, no code), `instantiateTemplate`, `fillSlot`, `matchSlot`, `fitLengths`, `suggestedEnemyConfig` |

**Templates** (`rig-templates.js`): a template is a body *shape* (floating caster / folding sentry /
limbed colossus), not a character. `instantiateTemplate` makes a document whose slots are empty
parts carrying `part.slot` (`label`, `hint`, `optional`, guesses); `isOpenSlot` = slot without art.
An open slot is drawn as a dashed placeholder (canvas + parts list), filled by dropping a PNG on its
row / its placeholder, or in bulk by file-name keyword (`slot.match`). `fillSlot` applies the pivot /
muzzle / grip / centre guesses (normalized in the trimmed image, y from the top); the first image
sizes the schematic to the art, and a layer cut from the same canvas as a placed part snaps to where
that canvas puts it. When the last required slot is filled, `fitLengths` rescales every *length*
(sway moves, hover, recoil, gesture moves) from the template's reference `height` to the real body
height — once (`state.template.fitted`), or again from "Reajustar amplitudes". At that moment an
enemy's body is also moved so its **centre sits on the origin** (the prefab root is the centre of
the body, never the feet: ranges/detection are measured from it; toolbar "◎ Origen al centro" redoes it).
Same for bosses; a boss also exports `root.feet` (`state.feetY`, null = bottom of the collider — the
orange "pies" line), which Unity turns into `BossController.feet`. Empty nodes (pivots without art)
are drawn as a diamond handle and picked by it (`nodeAt`). Exported `sortingOrder` = list index + 1
(0 is behind the scene background). Export: an empty
optional slot is left out, an empty required one is a problem. With no behaviour saved from Enemy
Creator, an enemy template's `behaviour` travels in the zip as `enemy-config.json`
(`suggestedEnemyConfig`); the Enemy Creator form does not preload it.

Undo/redo (Ctrl+Z / Ctrl+Y) snapshots the document after every settled edit (images shared by reference). Parent = drag a row, or the inspector's "Cuelga de". Boca sets `aimArtAngle` = pivot → muzzle; the "Frente al lanzar" arrow (pointAngle) only shows on limbs/orbiters.

Model rules: a part's **pivot is its joint and becomes the sprite's own pivot** (exported in
`sprites`, normalized), so `RigPart.pivot` is always 0; geometry is kept in each file's original
pixels (`cropX/cropY/origW/origH`). Muzzle and grips are exported as `Muzzle` / `Grip` nodes with
`rigPart:false`. `gestures` is exported only when the document has them (`state.gestures`; poses,
release points and effect anchors reference parts by uid or `'Visual'`): a
present list replaces Unity's, an absent key (`null`) keeps them. Library kind
`rig` (sheetless, like `boss`): the whole document incl. part images is the record's `draft`,
`config` is the last rig-config and `enemyConfig` its **behaviour**: Enemy Studio links the behaviour
form to the rig (step 4) and saves the EnemyConfig onto the rig record as `enemyConfig` (not `config`);
the 🧩 tab's picker does not list rigs. The rig's zip embeds that config as
`enemy-config.json` (`tuning` + `tier`, sprite-sheet keys stripped) plus its projectile's `Library/<id>/`; the boss link
embeds a Boss Creator entry's config without `body` plus its `Art/`.

### Entry kinds (enemy / projectile / fx / boss / rig / rig-template)

Every library entry has a **`kind`** — `enemy` (default), `projectile`, `fx` or `boss` — defined
once in `modules/entry-kinds.js`. The Sprites tab's pipeline (background removal, detect/grid
slicing, box editing, cropping, zip building) is entirely generic and is **shared unchanged** by the
three sheet kinds; the kind only decides:

| | `enemy` | `projectile` | `fx` | `boss` | `rig` |
|---|---|---|---|---|---|
| Owns a sprite sheet (`sheet`) | yes | yes | yes | **no** | **no** (parts live in its `draft`) |
| Canonical lanes | Idle / Walk / Attack / Hurt / Death / Wake | Awake / Move / Impact | Spawn / Loop / End | — | — |
| Pre-created on load | all but `Wake` | `Move`, `Impact` (`Awake` optional) | all three | — | — |
| Projectile lanes (`Projectiles/` in the zip) | yes | no | no | no | no |
| Creator tab (`creator`) | Enemy Creator | Proyectil/VFX | Proyectil/VFX | Boss Creator | Enemy Studio |
| Config file in a bundle (`configFile`) | `enemy-config.json` | `fx-config.json` | `fx-config.json` | `boss-config.json` | `rig-config.json` |

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
