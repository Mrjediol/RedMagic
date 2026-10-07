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
| `images[{ id, name }]` | the user | reference images for the prompt (also from 🎨 Diseños ▸ Pedir propuestas, via `images` in `POST /api/disenos/request`): picked (🖼 button) or pasted (Ctrl+V) in the quick form or the drawer's prompt fold; `POST /api/adjunto` saves them in `Logs/Taller/adjuntos/<id>` (not in git), `GET /api/adjunto?id=` shows them; `Runner._prompt` lists their absolute paths for Claude to Read |
| `model`, `effort` | the user | per task; default `sonnet` / `medium` (`config.json`) |
| `enemy`, `kind` (`montaje`/`cuerpo`/`resto`/`pendientes`), `design` | the server (talleres) | what made the task; the 🗂 Historial filters by it |
| `status`, `waiting`, `result`, `history`, `sessionId`, `lastRunId`; on steps `status`, `result` | **the queue only** | run state; a save from the web never overwrites it |

`result = { ok, text (the report), at, durationMs, costUsd, tokens, runId, sessionId, reply?, step?, stepId?, denied? }` — EVERY
run (the task, a step, an instruction) becomes the task's `result`; `history` = the earlier ones (newest first, 40 at most).
Together they are the conversation the web shows and the history's token totals.

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
     or an **instruction** (`followUp`, the composer) of a task that already has a session; an instruction for a task
     never launched starts it with the instruction appended. **`--append-system-prompt`** (`TERSE` + `STUCK`) + the `REPORT` line:
     no narration while working, a report of ~8 lines (token budget); `STUCK` = the no-progress rule (CLAUDE.md): a piece that
     fails 3× / a service that does not answer 2× is left noted, the rest done, and a stop is reported as "⛔ Bloqueada en: … Para seguir: …";
  **Watchdog** (`runner.mjs ▸ _watch`, the safety net): reads the stream and kills a run going in circles — the same tool error
     `sameError` times (numbers ignored; a "busy" error — Unity's 5 s timeout while importing, a refused connection — `sameBusyError` times, as polling looks like that), `errorsInARow` failing calls in a row, or the very same call `sameCall` times
     (`config.json ▸ watchdog`, 0 = off; defaults 4 / 20 / 8 / 20). Never a clock: a long run that keeps doing different things goes on.
     The result is `ok: false` + `stuck: { reason, last, at }` and a "⛔ Atascada" text with the last activity; the session is kept,
     so an instruction from the composer ("sigue") resumes it where it stopped;
  Queue items have a `qid` (`POST /api/unqueue` takes one out). Whatever is added for a task that is running / queued
  goes right behind it (before other tasks), so "send while it works" = "do this when you finish".
  4. the final message is the task's `result`; the whole stream → `Logs/Taller/<run>.jsonl` (not in git);
     what it is doing (last tool calls) → `/api/status` → the live box in the web.
  A run that was going when the server stopped is marked "interrumpida" at the next start.
- **Security**: a request is LOCAL only from loopback, with Host localhost / 127.0.0.1 and no proxy headers
  (`X-Forwarded-For`, `Tailscale-User-Login`); anything else is REMOTE and every `/api/` call needs the phone's
  pairing cookie (see Phone) — that also covers DNS rebinding. Anything that changes something needs the per-start
  token from `GET /api/hello` in `X-Taller-Token` — a custom header forces a CORS preflight the server never
  answers, so no other page can send it — and a same-origin Origin (http or https).
- `config.json` (optional, next to `server.mjs`): `port`, `defaultModel`, `defaultEffort`, `maxBudgetUsd`, `watchdog`,
  `disallowedTools`, `claudeExe`, `comfyExe`, `listen`, `ntfy`. `TALLER_PORT` overrides the port (tests).

## Phone (`movil.html` + `modules/movil.js`)

The same tab on the phone, with the PC on and `start.cmd` running: apuntes, descriptions, launching, replying,
accepting / discarding proposals, enemies. `movil.js` takes `#tab-todo` from `index.html` itself (no copy of the
markup) and runs `todo-panel.js` / `designs-panel.js` with `mobile: true` (no file mode, no reset, no 📱 button;
projectiles / effects can't be accepted there — the 📚 Biblioteca is the PC browser's IndexedDB). Under 760 px the
rail is a chip strip and the drawer covers the screen (the phone's Back closes it). `movil.webmanifest` = "add to
home screen".
- **Reaching the PC**: `tailscale serve --bg 3000` on the PC (once; Tailscale on PC and phone, same account) →
  `https://<pc>.<tailnet>.ts.net/movil.html`, works outside home, the server stays on 127.0.0.1. Home-only
  alternative: `"listen": "0.0.0.0"` in config.json (LAN).
- **Pairing**: the key is `Logs/Taller/movil-key.txt` (made on the first start; delete it + restart = un-pair all).
  PC: Tareas ▸ **📱 Móvil** (`GET /api/movil`, local only) shows the link `…/movil.html#k=<key>` + a QR (qrcodejs from
  cdnjs). The phone posts it to `/api/pair` → `HttpOnly; SameSite=Strict` cookie for a year; the hash is wiped.
- **Push** (optional): `"ntfy": "https://ntfy.sh/<long secret topic>"` → `runner.opts.onFinish` posts the task title
  and ok / failed when each run ends (nothing else leaves the PC); subscribe to that topic in the ntfy app.
- A task launched from the phone still WAITS while a Claude terminal is open on the PC (task `cola-terminal`).

## The web (`modules/todo-panel.js`, markup `#tab-todo`)

Two columns: a **left rail** (jump to 🤖 Claude, any phase / taller, 🗂 Historial; a gold badge = something waits for the
user) and the page. At the **top, 🤖 Claude** (`#todoZoneClaude`): the **open task** (`#todoDetail`; a click on any task
anywhere opens it here and scrolls up, ✕ / Esc closes it and scrolls back) — left: title, description, steps, fold with
prompt / images / needs / model / document; right: the **conversation** (every run oldest first, each under what started
it, older ones folded), the live **console** of the running run, the queued instructions (× each), and the **composer**
(Enter sends, Shift+Enter new line: an instruction for the same session — queued after the current run, or now). A new
step on a task that already ran is queued the same way. Below, the list of what runs / waits / is queued and what finished
in the last 48 h (× hides one: `localStorage`). Polls only redraw these live parts (`renderLive`), never a field you type
in; a focusout towards a button never re-renders (it used to swallow the click). Then the zones:

- **📝 Apuntes** — the user's phases (the to-do): each folds on its header (folded set in `localStorage`; open by
  default), progress bar, "＋ Tarea para Claude" form, add phase.
- **⚙ Talleres** — `TALLERES` in `todo-panel.js`: one `<details>` per kind of work (folded by default). A phase
  belongs to a taller by `phase.taller` or by its id (`designs` → 🎨 Diseños, `enemies` → 👾 Enemigos; the server
  makes those two). **Diseños** and **Enemigos** are static markup in `index.html` hosting `designs-panel.js`
  (`#designsRoot` / `#enemiesRoot`, marked `[data-designs]`: todo-panel ignores events there) plus their task list.
  The rest (Mapas, Ataques del jugador, Items, Mecánicas nuevas, Bugs) are **por definir**: free tasks only, kept in
  phase `taller-<id>` (made with the first task). A new taller = an entry in `TALLERES`; when it gets its own form /
  skill / prompt, give it static markup like Diseños.

- **🗂 Historial** (`#todoZoneHistory`) — one row per task that ran, its runs added up: type chips (`typeOf`: diseño,
  montaje completo, cuerpo, resto, pendientes, each taller, apunte), estado, order by date / total tokens / output tokens /
  time. Click = open it at the top. The talleres' own task lists are folded ("Tareas de este taller").

Served by the server → **server mode**: list through the API, polled (2 s while something runs, else 5 s),
re-rendered only when you are not typing in the tab. Served by anything else → **file mode**: the same list, the
repo file linked once with the File System Access API (no launching).

## Not built yet

**Apunte → taller link** (task `apunte-taller`, `docs/tareas/apunte-taller.md`): an apunte like "15 enemigos" taking
you to the taller that does it and showing its progress.

Phase 3 — a gallery of generated sprites (Comfy output) to pick from and send back as a task.

## Plan usage (Pro plan: no dollars)

The CLI's `rate_limit_event` carries the real 5 h / weekly utilisation. `Runner._noteUsage` keeps the latest in
`Logs/Taller/usage.json`; `/api/status` returns it as `usage`, and the toolbar shows two bars (`usageChip` in
`todo-panel.js`). The web no longer shows `$` (the CLI's `total_cost_usd` is list price, not a charge; it is still stored in
`result.costUsd`). The queue never pauses on usage (the weekly cap was removed on purpose). Data only updates when the queue runs a task.
