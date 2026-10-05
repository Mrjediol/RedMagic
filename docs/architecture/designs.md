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
| `enemy` | `image`: `comfy:<path under Comfy's output>` (e.g. `comfy:claude/base_x_raw_00001_.png`) or a `docs/…` path | PascalCase, not an existing prefab | `docs/enemigos/<Name>/` = `enemigo.json` + `diseno.png` (library, estado `por-montar`) |
| `projectile` / `fx` | `vfx: { preset, params }` — a ✨ VFX Lab preset (`vfx-presets.js` / `vfx-effects.js`) + its parameters; prefer an existing preset with new params | letters, numbers, `_` | the **browser** bakes it into 📚 Biblioteca (same record as VFX Lab ▸ Guardar), then the server stores its `libraryId` |
| `sound` | `sound: { family, label, seconds, loop?, layers }` — the shape of `game-sounds.js` (family = a `GAME_FAMILIES` key) | snake_case id, not used yet | `modules/game-sounds-added.js` (merged into `GAME_SOUNDS`) + `bake-sounds.mjs <id>` → `Assets/Audio/Sfx/Game/<family>/<id>.wav`, an unassigned option; the user grades it in Biblioteca Jere ▸ Sounds |

`status`: `pending` / `accepted` / `discarded`; the server adds `enemy` / `libraryId` and `reviewedAt`.
**Undo** (`pending`) reverses an accept: the enemy folder is removed (refused once its estado is past
`por-montar`), the sound is taken out of the palette with its `.wav`, the library entry is deleted.
Batches with `demo: true` are test data made from older Comfy outputs.

## Asking for proposals (the form at the top of the tab)

Kind + what the user wants + how many (1–10) + **one task with all** or **one task per proposal** (to compare costs)
+ (enemy) **Imágenes: Gemini | ComfyUI** + model / effort → `POST /api/disenos/request` creates the empty batch (`taskIds`, `count`, `mode`,
`generator`) and the task(s) in phase **`designs`** — the 🎨 Diseños taller's task list (`design: { lote, kind }`, `needs: comfy` only
for a ComfyUI enemy batch) and queues them.

**Generators** (enemy only; `GENERATORS` in `disenos.mjs`, the first is the default):
- `gemini` (default) — `tools/gemini/gemini-image.mjs`: Google's Gemini image API (`gemini-3-pro-image-preview`, falls to
  `gemini-2.5-flash-image` on 404; env `GEMINI_IMAGE_MODEL` overrides). Key: env `GEMINI_API_KEY` or
  `tools/gemini/api-key.txt` (git-ignored). Takes the user's attached images as `--ref`. Saves the PNG **in Comfy's
  output folder** (`claude/<name>.png`), so the item is still `comfy:…` and make-enemy (▶ Montar) uses it unchanged —
  the later steps (parts, states, cut) still run in ComfyUI.
- `comfy` — `claude-workflow.mjs run` (Qwen Image), needs ComfyUI open.
The prompt is a few lines (`tools/taller-server/design-prompts.mjs`): the rules are the
**`design-proposals` skill** (`.claude/skills/design-proposals/SKILL.md`, which reuses make-enemy §0–1 for enemies),
and the task ends by running **`tools/check-design-batch.mjs <batch>`** (formats, image exists, preset exists and is
of that kind, sound passes check-sounds and its id is free). While its tasks run the batch shows "generándose".

## An enemy (`docs/enemigos/<Name>/enemigo.json`)

`{ name, estado, prefab?, imagen?, diseno?: { image, source, lote, item, request, note }, pendientes: [Pendiente],
tarea?, montaje?: { fecha, resumen }, origen?, createdAt }` — `Pendiente = { id, tipo: comportamiento|sprite|gesto|ataque|bug|otro,
texto, estado: abierto|en-curso|hecho, tarea?, resumen?, nota? }`.

| estado | means | the card / sheet offers |
|---|---|---|
| `por-montar` | design accepted | ▶ Montar → a **make-enemy** task (`needs: comfy, unity`; skips step 1, the design's Comfy image is the base; decides the "stop" points itself) → `montando` |
| `montando` | a task is building it | its task state; ▶ Reintentar if it failed |
| `por-aprobar` | built: the user tests it in Unity | ✓ Aprobar · ⚠ Tiene pendientes |
| `con-pendientes` | exists, things to do | ▶ Arreglar (n) → ONE task with the open pendientes (`needs: unity` + comfy for sprite/gesto); they go `en-curso` |
| `aprobado` · `descartado` | done · dropped (the prefab stays) | ＋ Pendiente · ↶ Recuperar |

The tasks edit the record themselves when they end (prompts in `design-prompts.mjs`): estado `por-aprobar` (or back),
`prefab`, `montaje`, each pendiente `hecho` + `resumen` or `abierto` + `nota`, and refresh the picture.
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
  remove pendiente), `/build`, `/work`, `/add`, `/picture`. Tasks are added with `store.addTask(phase, task)` and
  queued with `runner.enqueue`.

## Web (`modules/designs-panel.js`, inside 📋 Tareas — see `tareas.md` ▸ The web)

Two roots: `#designsRoot` (🎨 Diseños taller: form, filter, Por revisar, Lotes revisados) and `#enemiesRoot` (👾 Enemigos
taller: the library); `onBadges` reports what waits for the user (pending proposals, enemies por aprobar) to the rail.

**＋ Pedir propuestas** (the form, state kept while the list re-renders); filter chips by kind (pending counts); **Por revisar** (a foldable group per batch), **Enemigos** (foldable by estado, needs-the-user first; a card opens its **sheet**: picture,
estado buttons, pendientes list + add + launch with model / effort), **Lotes revisados** (folded). Previews: the image, the effect playing live (one
`requestAnimationFrame` loop draws every `canvas[data-vfx]` with `drawFrame`), the sound's waveform + ▶.
Card click = select (a sound also plays); group bar = accept/discard the selection or all that are left;
⤢ = viewer (← → move, A accept, D discard, Space play, Esc). Polls every 5 s while visible; never re-renders while a
name is being typed.
