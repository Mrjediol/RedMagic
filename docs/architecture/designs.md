# 🎨 Diseños — proposals to approve (📋 Tareas ▸ Talleres + `docs/disenos/`) and the enemy library (`docs/enemigos/`)

A task that generates options (enemy designs, projectiles, effects, sounds) leaves them in a **batch**; the
user accepts or discards them in the web's 📋 Tareas ▸ ⚙ Talleres ▸ 🎨 Diseños, and each accepted one goes where that kind lives.
Plan and phases: `docs/tareas/enemigos-web.md`. Server-only: it goes through `tools/taller-server`
(`start.cmd`, http://localhost:3000); served any other way the tab says so.

## A batch (`docs/disenos/<date>-<slug>.json`) — what a design task must write at the end

```json
{ "title": "Tortugas voladoras", "request": "tortuga voladora, 5", "createdAt": "<ISO>", "kind": "enemy", "taskId": "<todo id>",
  "items": [ { "id": "1", "kind": "enemy", "name": "FlyingTurtle", "note": "one line", "status": "pending", …proposal } ] }
```

`kind` per item (or once for the batch): the proposal is

| kind | proposal | name | accept does |
|---|---|---|---|
| `enemy` | `image`: `comfy:<path under Comfy's output>` (e.g. `comfy:claude/base_x_raw_00001_.png`) or a `docs/…` path; in Prompt mode `prompt` (no image yet; the user pastes it) | PascalCase, not an existing prefab | `docs/enemigos/<Name>/` = `enemigo.json` + `diseno.png` (library, estado `por-montar`) |
| `projectile` / `fx` | `vfx: { preset, params }` — a ✨ VFX Lab preset (`vfx-presets.js` / `vfx-effects.js`) + its parameters; prefer an existing preset with new params | letters, numbers, `_` | the **browser** bakes it into 📚 Biblioteca (same record as VFX Lab ▸ Guardar), then the server stores its `libraryId` |
| `sound` | `sound: { family, label, seconds, loop?, layers }` — the shape of `game-sounds.js` (family = a `GAME_FAMILIES` key) | snake_case id, not used yet | `modules/game-sounds-added.js` (merged into `GAME_SOUNDS`) + `bake-sounds.mjs <id>` → `Assets/Audio/Sfx/Game/<family>/<id>.wav`, an unassigned option; the user grades it in Biblioteca Jere ▸ Sounds |

`status`: `pending` / `accepted` / `discarded`; the server adds `enemy` / `libraryId` and `reviewedAt`.
**Undo** (`pending`) reverses an accept: the enemy folder is removed (refused once its estado is past
`por-montar`), the sound is taken out of the palette with its `.wav`, the library entry is deleted.
Batches with `demo: true` are test data made from older Comfy outputs.

## Asking for proposals (the form at the top of the tab)

Kind + what the user wants + how many (1–10) + **one task with all** or **one task per proposal** (to compare costs)
+ (enemy) **Zona** (`docs/zonas.json`: ice, volcano, forest… una zona nueva = una entrada; da paleta y color de los cristales; solo la paleta, nunca materiales) + **Cuerpo** (`docs/tipos-enemigo.json` ▸ body: humanoid, quadruped, flying, floating, libre — cada uno trae SU anatomía, pose y piezas) + **Ataque** (▸ attack: weapon = pieza aparte que dispara, magic = criatura normal sin arma, none = cuerpo a cuerpo) + **Cómo**:
**📋 Prompt para Gemini** (por defecto) o **🎨 Enemigo con ComfyUI** + model / effort → `POST /api/disenos/request` creates the empty batch
(`taskIds`, `count`, `mode`, `zone`, `body`, `attack`, `generator: prompt|comfy`) and the task(s) in phase **`designs`** — the 🎨 Diseños taller's task list
(`design: { lote, kind }`, `needs: comfy` only in ComfyUI mode) and queues them.

**Enemy modes** (both build the base prompt with `tools/enemy-prompt.mjs`: zone palette + crystal signature + body-type anatomy / pose / pieces + attack type + edge rules (a body type's rules never reach another: `--body`, `--attack`, `--cast`);
the task only writes creature, design, crystals and pieces):
- **Prompt**: the item carries `prompt` and no `image`. The card shows the text (📋 Copiar prompt, ↗ Abrir Gemini); the user pastes it in the Gemini app
and brings the picture back with 🖼 / Ctrl+V on the card (`POST /api/disenos/imagen` → `docs/disenos/img/<lote>__<item>.<ext>`, sets `item.image`;
also replaces a ComfyUI image while the item is pending). Ctrl+V reads the file from `files` or `items`, else the `<img src>` of copied HTML
(`data:` decoded in the browser, `https:` downloaded by the server: `{ url }`); it goes to the card the cursor is in, else the one in the
viewer, the selected one (or the last clicked), else the one under the mouse — then the next card without picture is selected.
Accept needs the image. No paid Gemini plan or API: it is by hand on purpose.
- **ComfyUI**: as before (`claude-workflow.mjs`, `image: comfy:…`), with the same prompt (`--target comfy`).
An accepted enemy keeps its `zone` in `diseno`; ▶ Montar copies a non-Comfy picture to Comfy's `input` folder for make-enemy.
The prompt is a few lines (`tools/taller-server/design-prompts.mjs`): the rules are the
**`design-proposals` skill** (`.claude/skills/design-proposals/SKILL.md`, which reuses make-enemy §0–1 for enemies),
and the task ends by running **`tools/check-design-batch.mjs <batch>`** (formats, image exists, preset exists and is
of that kind, sound passes check-sounds and its id is free). While its tasks run the batch shows "generándose".

## An enemy (`docs/enemigos/<Name>/enemigo.json`)

`{ name, estado, prefab?, imagen?, diseno?: { image, source, lote, item, request, note }, pendientes: [Pendiente],
tarea?, montaje?: { fecha, resumen }, cuerpo?: { fecha, plantilla, partes, brief, resumen }, paso2?: { tipo | plantilla, proyectil|sonidos|efectos: { origen: biblioteca|nuevo, texto }, notas }, instrucciones?: [{ paso: montaje|cuerpo|resto|estados, texto, fecha }],
piezas?: { modo: auto|manual, lista: [{ nombre, nota }] }, estados?: [Estado], origen?, createdAt }` — `Pendiente = { id, tipo: comportamiento|sprite|gesto|ataque|bug|otro,
texto, estado: abierto|en-curso|hecho, tarea?, resumen?, nota? }` · `Estado = { id, pieza, nombre, texto, uso: ''|asleep|awake|alert|attacking|hurt|dead|gesto, frames, estado: abierto|en-curso|hecho, tarea?, resumen?, nota? }`.

**🧩 Piezas** (sheet, while a launch cuts the body: por-montar … cuerpo-aprobado): *Que Claude decida* or *Las elijo yo* = a list
(presets `PIECES` in `design-prompts.mjs`, sent by `GET /api/disenos` as `piezas`, or any typed name) + a note per piece. Sent with
▶ Por pasos / ▶ Montar / ↻ Rehacer (`piezas` in the body) and stored. Meaning: the pieces to SEPARATE — whatever is not listed stays inside
the piece that contains it, the assembled body always equals the design; one piece (`Body`) = not cut. make-enemy ▸ "Piezas elegidas"; the
brief's `only` drops the template's other slots.
**🎭 Estados de piezas** (sheet, every estado but descartado): an extra sprite of one part (angry face, open palm), never a change to the
body; `frames` > 1 = a transition (make-enemy ▸ "Estados pedidos"; Unity does not play transitions yet: task `rigtrans`). Added with
`update` op `add-estado` (also `edit-estado`, `remove-estado`); the open ones go with the next build / step task (`en-curso`), or on their own
with ▶ Hacer estados once there is a prefab (`/states`, prompt `statesPrompt`). The extra-instructions wording tells Claude that a part's
look is a state unless the user says to change the body.

| estado | means | the card / sheet offers |
|---|---|---|
| `por-montar` | design accepted | ▶ Montar → a **make-enemy** task (`needs: comfy, unity`; skips step 1, the design's Comfy image is the base; decides the "stop" points itself) → `montando` |
| `por-montar` ▶ Por pasos | same estado: **▶ Por pasos** (body first) instead of ▶ Montar | a **body** task (`needs: comfy, unity`, `bodyPrompt`) → `montando-cuerpo` |
| `montando-cuerpo` | step 1 task running (parts + a body-only prefab) | its task state; ▶ Reintentar |
| `cuerpo-por-aprobar` | body built: the user looks at it (`cuerpo.png`, the picture) and may adjust it in 🧬 Enemy Studio | ✓ Aprobar cuerpo · ↻ Rehacer (sprite pendientes = notes) |
| `cuerpo-aprobado` | body approved | **Paso 2** form: kind of enemy (`BEHAVIORS` in `design-prompts.mjs`) or a template enemy to inherit; per asset (proyectil / sonidos / efectos) *de mi biblioteca* or *nuevo* + a description (empty = Claude decides) + notes → ▶ Montar el resto (`restPrompt`, stored as `paso2`) → `montando` |
| `montando` | a task is building it | its task state; ▶ Reintentar if it failed |
| `por-aprobar` | built: the user tests it in Unity | ✓ Aprobar · ⚠ Tiene pendientes |
| `con-pendientes` | exists, things to do | ▶ Arreglar (n) → ONE task with the open pendientes (`needs: unity` + comfy for sprite/gesto); they go `en-curso` |
| `aprobado` · `descartado` | done · dropped (the prefab stays) | ＋ Pendiente · ↶ Recuperar |

**Extra instructions**: every launch from the sheet (▶ Montar completo, ▶ Por pasos, ↻ Rehacer el cuerpo, ▶ Montar el resto, the retries)
sends the sheet's "✍ Instrucciones extra" box as `instrucciones`; the prompt puts it first as overriding the design and the step's
limits (e.g. extra part states in step 1), and lists the earlier ones (kept in the record) as context. The cards' ▶ Montar… /
▶ Reintentar… open the sheet with that box focused.
The tasks edit the record themselves when they end (prompts in `design-prompts.mjs`): estado `por-aprobar` (or back),
`prefab`, `montaje`, each pendiente `hecho` + `resumen` or `abierto` + `nota`, and refresh the picture.
**By steps** (▶ Por pasos): step 3 never re-imports the full body (the user may have adjusted it in Enemy Studio): `make-enemy.mjs <brief> --partial` writes `out/<Name>.partial.zip` (gestures + effects + deck only) which the importer applies on the existing prefab. ▶ Montar (everything at once) stays for simple enemies; make-enemy's "Por pasos" section has the rules.
Their tasks live in phase **`enemies`** (`enemy: <Name>`) — the 👾 Enemigos taller's task list.

**Existing enemies** (not from a design): web "＋ Enemigo existente" (a prefab of `Assets/Prefabs/Enemies/**` not in
the library; name = file name without `Enemy_` / `Boss_`) or Unity **Tools ▸ RedMagic ▸ Enemigos ▸ Pendiente para
la web…** (pick the prefab, type, text). **Pictures** come from Unity: `EnemyPreviewExporter.Export(prefab, png)`
(`Assets/Scripts/Enemies/Editor/EnemyPreviewExporter.cs`, renders the prefab's sprites on transparent) →
`docs/enemigos/<Name>/preview.png` (`imagen`); the server calls it through `unity command eval` when Unity is open;
`Mark(prefab)` does record + picture; **Tools ▸ RedMagic ▸ Enemigos ▸ Actualizar imágenes de la web** redoes them all.

## Server (`tools/taller-server/disenos.mjs`, routes in `server.mjs`)

- `GET /api/disenos` → `{ lotes, enemigos }`, read from disk on every call (Claude's edits show up, no watcher).
- `GET /api/img?src=…` → an image, only from Comfy's output (`config.json → comfyOutput`, default
  `%LOCALAPPDATA%/Comfy-Desktop/ComfyUI-Shared/output`) or `docs/`.
- `POST /api/disenos/review { loteId, itemIds, decision: accept|discard|pending, names, libraryIds }` → `{ errors }`
  per item: one bad item never blocks the rest.
- `POST /api/disenos/request` (above). `GET /api/disenos` also returns `tasks`: the state of every task a batch or an
  enemy points at.
- `tools/taller-server/enemigos.mjs`: `GET /api/enemigos/prefabs`, `POST /api/enemigos/update` (estado, add / edit /
  remove pendiente, add / edit / remove estado), `/build`, `/build-body` (step 1; both take `piezas`), `/build-rest` (step 3, `paso2`), `/states`, `/work`, `/add`, `/picture`. Tasks are added with `store.addTask(phase, task)` and
  queued with `runner.enqueue`.

## Web (`modules/designs-panel.js`, inside 📋 Tareas — see `tareas.md` ▸ The web)

Two roots: `#designsRoot` (🎨 Diseños taller: form, filter, Por revisar, Lotes revisados) and `#enemiesRoot` (👾 Enemigos
taller: the library); `onBadges` reports what waits for the user (pending proposals, enemies por aprobar) to the rail.

**＋ Pedir propuestas** (the form, state kept while the list re-renders); filter chips by kind (pending counts); **Por revisar** (a foldable group per batch), **Enemigos** (foldable by estado, needs-the-user first; a card opens its **sheet**: picture,
estado buttons, pendientes list + add + launch with model / effort), **Lotes revisados** (folded). Previews: the image, the effect playing live (one
`requestAnimationFrame` loop draws every `canvas[data-vfx]` with `drawFrame`), the sound's waveform + ▶.
Card click = select only it (Ctrl+click adds / removes, Shift+click a range, Esc clears; a sound also plays); group bar = accept/discard the selection or all that are left;
⤢ = viewer (← → move, A accept, D discard, Space play, Esc). Polls every 5 s while visible; never re-renders while a
name is being typed, nor while a mouse button is held inside it (`modules/press-guard.js`: a redraw between mousedown and
mouseup swaps the button and the click is lost — it waits for the click; 📋 Tareas uses it too). A launch from a sheet is one
at a time (a second click while it is on its way does nothing).
