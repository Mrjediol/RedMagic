# Tareas — the to-do, and Claude's task queue (`docs/tareas/`, web 📋 Tareas, `tools/taller-server/`)

The project's pending work lives in **`docs/tareas/todo.json`**, shown and edited in the web's 📋 Tareas tab.
It is also the hand-off between sessions: instead of "remember to do X", X is a task in that file, with its
own document when it needs context. Tasks can be **launched** from the web: a local server runs them one by
one as unattended Claude Code sessions and writes each report under its task.

## The file

`{ v, updated, phases: [{ id, name, tag, tasks: [Task] }], queue: [...] }`

| Task field | Who writes it | Meaning |
|---|---|---|
| `id`, `text`, `note`, `done` | the user (web) / Claude | title, description, ticked |
| `prompt` | the user | what to ask Claude; empty = title + description + steps |
| `doc` | Claude | `docs/tareas/<id>.md`: state, instructions, what is done — read FIRST when it runs |
| `steps[{ id, text, done }]` | the user / Claude | ordered steps; each can be launched alone |
| `needs: ['comfy','unity']` | the user | the queue opens them before running |
| `model`, `effort` | the user | per task; default `sonnet` / `medium` (`config.json`) |
| `status`, `waiting`, `result`, `history`, `sessionId`, `lastRunId`; on steps `status`, `result` | **the queue only** | run state; a save from the web never overwrites it |

`result = { ok, text (the report), at, durationMs, costUsd, runId, sessionId, reply?, denied? }`; `history` =
the earlier reports (newest first, 10 at most).

## Parking a task ("déjalo pendiente")

When the user says to leave something for later, the session writes **`docs/tareas/<task id>.md`** — goal, state,
what was done, what is left, files involved, decisions and open questions: everything a cold session needs —
and adds (or updates) the task in `todo.json` with `doc` pointing at it and `steps` when it splits. The web shows
it pending; launching it starts a session that reads that document first. Keep the document current when the
task moves on (a run updates it before finishing).

## The server (`Web/RedMagicWeb/tools/taller-server/`)

`start.cmd` (or `node server.mjs`) — replaces `npx serve`: same address, **http://localhost:3000**, so the Unity
bridge keeps trusting the web. Serves `Web/RedMagicWeb` and the API (see the header of `server.mjs`).
- **`store.mjs`** — the file in memory. Saves from the web are merged by ownership (table above): the user's
  words from the web, run state from here, tasks the web never saw (Claude edited the file) kept; the file is
  watched, an outside edit reloads it and bumps `rev`, which the web polls.
- **`runner.mjs`** — the queue, one session at a time:
  1. an **interactive Claude Code session open in a terminal** (`…claude-code\bin\claude.exe` without `-p`)
     makes it WAIT (two sessions on one repo step on each other's files and commits) and re-check every 15 s;
     the web's "Lanzar igualmente" overrides it once;
  2. `needs` → ComfyUI (`Comfy Desktop.exe`) / Unity (`unity open <project> --args -force-d3d11`) opened if
     closed, waited for (3 / 6 min);
  3. `claude -p` with the prompt on stdin, `--output-format stream-json`, **`--permission-mode auto
     --permission-prompts none`** (risky actions refused, nothing can stop to ask) and a deny list (push, reset,
     rebase, clean, branch -D — `config.json ▸ disallowedTools`); `--session-id` new, or `--resume` for a step
     of a task that already has a session and for a **reply** ("↩ Responder" = continue that session);
  4. the final message is the task's `result`; the whole stream → `Logs/Taller/<run>.jsonl` (not in git);
     what it is doing (last tool calls) → `/api/status` → the live box in the web.
  A run that was going when the server stopped is marked "interrumpida" at the next start.
- **Security**: 127.0.0.1 only; Host must be localhost / 127.0.0.1 (DNS rebinding); anything that changes
  something needs the per-start token from `GET /api/hello` in `X-Taller-Token` — a custom header forces a CORS
  preflight the server never answers, so no other page can send it.
- `config.json` (optional, next to `server.mjs`): `port`, `defaultModel`, `defaultEffort`, `maxBudgetUsd`,
  `disallowedTools`, `claudeExe`, `comfyExe`. `TALLER_PORT` overrides the port (tests).

## The web (`modules/todo-panel.js`, markup `#tab-todo`)

One tab, three columns: a **left rail** (jump to any phase / taller, with counts: a gold badge = something waits for
the user), the **list**, and a **right drawer** with the open task (click anywhere on a row; Esc / ✕ closes it):
title, **description** (`note`, always visible), steps (▶ each), launch / cancel / ✓ hecha, report + earlier reports,
reply, and a fold with prompt / needs / model / document. The list has two zones:

- **📝 Apuntes** — the user's phases (the to-do): each folds on its header (folded set in `localStorage`; open by
  default), progress bar, "＋ Tarea para Claude" form, add phase.
- **⚙ Talleres** — `TALLERES` in `todo-panel.js`: one `<details>` per kind of work (folded by default). A phase
  belongs to a taller by `phase.taller` or by its id (`designs` → 🎨 Diseños, `enemies` → 👾 Enemigos; the server
  makes those two). **Diseños** and **Enemigos** are static markup in `index.html` hosting `designs-panel.js`
  (`#designsRoot` / `#enemiesRoot`, marked `[data-designs]`: todo-panel ignores events there) plus their task list.
  The rest (Mapas, Ataques del jugador, Items, Mecánicas nuevas, Bugs) are **por definir**: free tasks only, kept in
  phase `taller-<id>` (made with the first task). A new taller = an entry in `TALLERES`; when it gets its own form /
  skill / prompt, give it static markup like Diseños.

Served by the server → **server mode**: list through the API, polled (2 s while something runs, else 5 s),
re-rendered only when you are not typing in the tab. Served by anything else → **file mode**: the same list, the
repo file linked once with the File System Access API (no launching).

## Not built yet

**Apunte → taller link** (task `apunte-taller`, `docs/tareas/apunte-taller.md`): an apunte like "15 enemigos" taking
you to the taller that does it and showing its progress.

Phase 3 — a gallery of generated sprites (Comfy output) to pick from and send back as a task. Phase 4 — phone
access (Tailscale + token, the server already speaks a token) and a notification when the queue empties (ntfy /
Telegram).

## Plan usage (Pro plan: no dollars)

The CLI's `rate_limit_event` carries the real 5 h / weekly utilisation. `Runner._noteUsage` keeps the latest in
`Logs/Taller/usage.json`; `/api/status` returns it as `usage`, and the toolbar shows two bars (`usageChip` in
`todo-panel.js`). The web no longer shows `$` (the CLI's `total_cost_usd` is list price, not a charge; it is still stored in
`result.costUsd`). The queue never pauses on usage (the weekly cap was removed on purpose). Data only updates when the queue runs a task.
