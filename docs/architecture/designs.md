# 🎨 Designs — proposals to approve (web tab + `docs/disenos/`) and the enemy library (`docs/enemigos/`)

A task that generates options (enemy designs, projectiles, effects, sounds) leaves them in a **batch**; the
user accepts or discards them in the web's 🎨 Designs tab, and each accepted one goes where that kind lives.
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

## An enemy (`docs/enemigos/<Name>/enemigo.json`)

`{ name, estado, diseno: { image, source, lote, item, request, note }, pendientes: [], createdAt }` — estado
`por-montar` → `montando` → `por-aprobar` (built, the user tests it) → `aprobado` · `con-pendientes` · `descartado`.

## Server (`tools/taller-server/disenos.mjs`, routes in `server.mjs`)

- `GET /api/disenos` → `{ lotes, enemigos }`, read from disk on every call (Claude's edits show up, no watcher).
- `GET /api/img?src=…` → an image, only from Comfy's output (`config.json → comfyOutput`, default
  `%LOCALAPPDATA%/Comfy-Desktop/ComfyUI-Shared/output`) or `docs/`.
- `POST /api/disenos/review { loteId, itemIds, decision: accept|discard|pending, names, libraryIds }` → `{ errors }`
  per item: one bad item never blocks the rest.

## Web (`modules/designs-panel.js`, markup `#tab-designs`, styles `/* 🎨 Designs */`)

Filter chips by kind (pending counts); **Por revisar** (a foldable group per batch), **Biblioteca de enemigos**
(foldable by estado), **Lotes revisados** (folded). Previews: the image, the effect playing live (one
`requestAnimationFrame` loop draws every `canvas[data-vfx]` with `drawFrame`), the sound's waveform + ▶.
Card click = select (a sound also plays); group bar = accept/discard the selection or all that are left;
⤢ = viewer (← → move, A accept, D discard, Space play, Esc). Polls every 5 s while visible; never re-renders while a
name is being typed.
