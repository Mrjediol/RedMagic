# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

RedMagic is a 2D mobile roguelite built in Unity 6000.3.23f1 (URP 2D Renderer). Loop: menu → hub
(manage upgrades/build, pick a tomb) → a run through a world's sections → boss → back to the hub
(on death or completion).

## Response style

- Skip pleasantries: no introductory or concluding filler ("Sure, I can help with that", "Let me
  know if you need anything else"). Start directly with the answer.
- Be concise: 2-3 sentences max outside of code/lists.
- Limit explanations: code only, or a bulleted list, with no explanation unless explicitly asked.
- Terse mode: drop non-essential articles and narrative explanation; keep technical accuracy.

## Commands

There is no separate build/lint/test CLI — everything goes through the Unity Editor.

- **Compile check**: if a Unity Editor is open on this project, `unity status` (from the Unity CLI)
  shows its port; `unity command recompile` triggers a recompile and `unity command recompile_status`
  polls it (`errors: []` = clean). This is much faster than opening the editor from scratch and is
  the way to verify a C# change compiles.
- **Console output**: `unity command console --level error --tail 50` reads the live editor's
  console without needing a screenshot.
- **Compile check with the Editor closed** (or stuck in Safe Mode, which is exactly when you need
  it most): `dotnet build Assembly-CSharp.csproj` and `dotnet build Assembly-CSharp-Editor.csproj`
  from the project root. Unity keeps those two `.csproj` files in sync with the real file list, so
  this is a genuine check of the same sources — add `-t:Rebuild` to defeat caching. **Verify the
  file list first** (`grep -c "Compile Include" Assembly-CSharp.csproj` against
  `find Assets -name '*.cs' -not -path '*/Editor/*' | wc -l`): if Unity died before regenerating
  them, the projects are stale and a "0 errors" is meaningless because your new files aren't in it.
- **Scene inspection**: `unity command list_open_scenes`, `get_scene_hierarchy`, `find_gameobjects`,
  `get_component_properties` read the actual loaded scene state — prefer these over parsing scene
  YAML by hand when an editor is connected, since they reflect what Unity actually deserialized
  (see the SceneReference gotcha below).
- **Builds**: `unity build --target <platform>` / `unity command build` trigger a Player build via
  the CLI; there's no CI config in this repo.
- **Play Mode via the CLI ticks far slower than real time when the Editor window has no focus/OS
  input** — `Time.realtimeSinceStartup` and `Time.frameCount` still creep forward, but a component's
  own `Update()`/coroutines can sit frozen for many real seconds between two `eval` calls, only
  visibly catching up in a burst right after `unity command editor_focus`. A single `eval` reading a
  timer twice with a real-time gap in between is **not reliable evidence** that gameplay logic is
  broken — it may only prove the harness never gave Unity a reason to tick. To actually verify
  timed/AI/physics behavior: call `editor_focus` immediately before the window you're measuring,
  or invoke the private `Update`/coroutine method via reflection to force one step deterministically,
  or use `capture_game_view` (a real screenshot is unambiguous — see the ranged-enemy debugging in
  the pipeline doc for a worked example) rather than trusting polled state across an idle gap.
- **Tests**: `com.unity.test-framework` is in `Packages/manifest.json` but no test assembly or
  `Tests/` folder exists yet — there is nothing to run.


## Hard rules (always apply)

- **Localization**: never hardcode player-facing text. Key in BOTH `es.txt` and `en.txt`; finish with *Auditar claves* = 0 missing. Details: `docs/architecture/localization.md`.
- **No Play Mode testing** by default (slow, burns tokens). Compile-check only, then give user a checklist of what to try + what feedback to report.
- **Pooling**: spawn-heavy objects never Instantiate/Destroy → `Pool<T>` / `PrefabPool`.
- **Domain Reload is off**: statics survive Play sessions → reset state manually in `Awake`/`OnEnable`.
- **Singletons** (`GameStateManager`, `AudioManager`, `RunManager`): `DontDestroyOnLoad`, never place `AudioManager` in a scene.
- **Scenes**: never store a scene by name string → use `SceneReference`.
- **Generic-first**: attacks/movements reusable by any enemy or boss; build generic behaviour first, then specific enemy. Every attack ships a default placeholder projectile/FX.
- **Inspector-first**: plain fields/checkboxes over ScriptableObject indirection when it reads better in Inspector. Prefer redesign over workaround.
- **Legacy** (`Assets/Scripts/Legacy/`) and `Assets/_Reference/`: read-only reference, never build on it.
- **Search scope**: `Assets/Scripts`, `Assets/Ui`, `Assets/Audio`, `Assets/Scenes`. Skip `Assets/Brackeys/`, `Assets/Cainos/`, `Assets/Dragon Warrior Files/`.
- "Biblioteca web" = custom tabbed Unity **editor window**, NOT `Web/RedMagicWeb`.

## Architecture docs — read ONLY the ones the task touches

| Touching… | Read |
|---|---|
| Object pools, spawning | `docs/architecture/pooling.md` |
| Placeholder FX → real art (`Assets/Prefab/Fx/`) | `docs/architecture/fx-placeholders.md` |
| Singletons, pause, `CanPlayerAct` | `docs/architecture/singletons.md` |
| Scene refs, `SceneReference` | `docs/architecture/scene-references.md` |
| Runs, worlds, sections, `RunManager` | `docs/architecture/run-world.md` |
| Enemies, AI | `docs/architecture/enemies.md` |
| Sprite/enemy import pipeline | `docs/architecture/sprite-pipeline.md` |
| Combat, damage, projectiles | `docs/architecture/combat.md` |
| Camera | `docs/architecture/camera.md` |
| Weapons, items, builds, synergies | `docs/architecture/weapons-items.md` |
| Legacy code | `docs/architecture/legacy.md` |
| Bosses, Boss Creator | `docs/architecture/bosses.md` |
| Currency, shop, reroll | `docs/architecture/economy.md` |
| Main Hub, tombs, interactables | `docs/architecture/hub.md` |
| Audio, `SoundEmitter` | `docs/architecture/audio.md` |
| Input System | `docs/architecture/input.md` |
| UI Toolkit | `docs/architecture/ui.md` |
| Localization, display presets | `docs/architecture/localization.md` |
| Vendored packs | `docs/architecture/vendored.md` |

Other: `docs/GDD.md` (game design: loop, worlds, enemies, bosses, art direction — read before design decisions), `docs/schemas/` (JSON configs + `COMPATIBILITY.md`), `docs/web-tools-guide.md` (web tool), `docs/RedMagic — Roadmap al prototipo.md`.

## Git (Claude Code handles it)

- Before starting a task: `git status`; if uncommitted changes exist, commit them first as `wip: pre-task snapshot` so the task is revertible.
- After each completed task that compiles cleanly: `git add -A` + `git commit` with a short conventional message (`feat:`, `fix:`, `refactor:`, `docs:`, `art:`), one line summary + bullet body.
- Never commit if compile errors exist; never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` (already in `.gitignore`).
- **Never `git push`, force-push, rebase, reset --hard or delete branches** — user pushes from GitHub Desktop.
- Large or risky refactors: create a branch `task/<short-name>` first; tell user to merge after playtest.

## Keeping docs current

When a task changes a system's architecture, update its `docs/architecture/*.md` in the same task. Keep this file under ~10 KB: new detail goes in the topic doc, not here.
