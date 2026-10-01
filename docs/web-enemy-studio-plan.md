# Plan — Enemy Studio: one web flow from parts to a finished enemy

Next big task (proposed 2026-10-01, after the part-rig plan D–F). **Nothing here is built yet.**
Goal, in the user's words: import the parts of an enemy, build body and gestures, save it as a
**template**, create projectile / VFX effects in the VFX tab, and **attach those effects anywhere
on the enemy** — plus defensive moves, idle gestures, and per-state sprites — in one comfortable
workflow, with full freedom to make very personalised enemies.

## ▶ How to resume (read this first)

**What to tell Claude:**
> Lee `docs/web-enemy-studio-plan.md` y ejecuta la **Fase S1** (sólo esa). Sigue las reglas de trabajo del plan.

(Replace S1 with the phase to do. S0 is the merge, done by the user or on request.)

**Model and effort:** high for S1, S2, S3, S5 (schema + Unity + web at once); medium for S4, S6.

**Read at the start of a phase** (and nothing else unless needed): `CLAUDE.md`; this plan;
`docs/architecture/procedural-rig.md`, `enemies.md`, `bosses.md` (IAttackHost), `audio.md`;
`Web/RedMagicWeb/ARCHITECTURE.md`; `docs/schemas/COMPATIBILITY.md`; `docs/part-rig-pending-playtests.md`
(what is still untested); `docs/part-rig-pipeline-plan.md` only for history.

**Working rules (same as the part-rig plan, plus what the user asked since):**
- No Play Mode by Claude: compile check (`unity command recompile` + `recompile_status`; focus the Editor
  with `unity command editor_focus` or it does not tick), importer/round-trip checks via `unity command eval`,
  web checked headless (jsdom + Ajv, the pattern used in the part-rig phases). One phase per session, commit
  per phase, never push. Work on a task branch (`task/enemy-studio`), snapshot commit before starting.
- **Tests are batched**: the user tests at the end, not per phase. After each phase: say what important things
  were added, say honestly whether the goal of the step was reached, append "what was added + tests" to
  `docs/part-rig-pending-playtests.md`.
- **New rig features must reach everything that already automates enemies**: the Sounds tab (every new action /
  gesture / state needs its `SoundCue` + a category in `SoundCategories`, kept on reimport by name, see
  `audio.md`), the rig-config schema + exporter/importer + `RigConfigRoundTrip`, the inspector (fields under a
  `[Group]`; daily ones first), pooling, localization (no hardcoded player-facing text).
- Web UI: important fields at hand, the rest in foldable groups (the user dislikes visual noise).
- One rig driver only: `RigDriverCode`. Do not reintroduce a second engine (see `procedural-rig.md`).
- Generic-first: movement/defence behaviours are one C# class reusable by any enemy, with a default placeholder.
- No hand-written name strings for wiring; `SceneReference` etc. per CLAUDE.md. Untracked user test assets
  (`Assets/Art/Characters/Newworl`, `koros` changes…) are not ours: never commit them.

## State at the start of this task (2026-10-01)
Branch `task/rig-driver-tween` (stacked on `task/enemy-attack-deck`, 13 commits ahead of `main`) holds
everything built so far and is **not merged**: part-rig phases D (deck of attacks per enemy,
`IAttackHost`), E (gesture pose editor), F (VFX Lab), the retired DOTween driver (in `Legacy/`), the
own attack repeated with another gesture, sounds per gesture / per deck attack, foldout inspectors
(`[Group]`), Enemy Creator reorganised, the own attack's projectile written on every import. The user
tested part of E/F and the regressions (all fine); the rest is listed in `part-rig-pending-playtests.md`.
**Start S1 from that branch** (create `task/enemy-studio` from it) unless the user merged it to `main` first.

## Where we are (what exists and gets reused)
- Rig Composer: parts, roles, sway, **gestures** with a pose editor and preview (`rig-gestures.js`),
  body templates (`rig-templates.js`), sprites per state / stage **per part** (asleep / awake /
  attacking), export to `.zip`.
- Enemy Creator: behaviour (archetype, ranges…), **deck of attacks** (catalog attacks + the own
  attack repeated with another gesture), library art pickers, headless-tested export.
- VFX Lab: parametric presets baked into library entries (projectile / fx).
- Unity: one driver (`RigDriverCode`), `EnemyAttackHost`, `IAttackHost`, importers for all of it.
- Fixed just before this plan: the own attack's projectile is rewritten on every import; Enemy
  Creator reorganised with folds.

## Product shape: one "Enemy Studio" tab (replaces the Rig Composer + Enemy Creator split)
Keep both existing tabs working until the Studio covers them, then retire them (no coexistence).
Left: steps in the order of the workflow. Centre: the canvas of the Rig Composer. Right: inspector
of whatever is selected. Steps (each foldable, the next one unlocked as the previous is usable):

1. **Cuerpo** — import part PNGs / start from a template; parts, roles, pivots (today's Composer).
2. **Plantilla** — "Guardar como plantilla": body + gestures (+ optionally behaviour) saved as a
   library entry of kind `rig-template`. "Nuevo desde plantilla" lists built-in AND user templates;
   a template carries its **gestures by part role** (casting arm, head…), not by uid, so another
   enemy with the same body shape inherits them (slot matching already exists for built-ins).
3. **Efectos** — a panel of the library's projectiles / fx (made in ✨ VFX Lab) to drop on gestures.
4. **Comportamiento** — a timeline-less "behaviour board" (below): attacks, defensive moves, idle
   gestures, state sprites.
5. **Exportar** — one `.zip`, one Unity import.

## Core new concept: a gesture is a timeline with phases, and phases carry effects
Today a gesture = `windup` pose + `strike` pose + timings. Generalise to **phases** without
breaking existing data (`windup` / `strike` stay as the first two phases; `recover` the last):

- Each phase can hold **FX hooks**: `{ fx: <library id>, at: 'start' | 'end' | 0..1, anchor: <part uid | muzzle | body | feet>, offset, scale, followsPart: bool, loop: bool }`.
  Example (the user's): energy weapon — *carga*: charge-up particles on the weapon tip for the whole
  phase; *golpe*: the shot's burst at the muzzle on release.
- Unity: `RigGesture` gains `List<RigFx>` per phase (data only). `RigDriverCode` spawns them through
  the existing pooled fx path (`VfxOneShot` / pooled flipbook prefab, built by the importer from the
  library entry exactly like attack art) and parents them to the anchor when `followsPart`.
- Pooling and placeholders rules apply (hard rules): nothing Instantiate/Destroy per cast.

## Behaviour board (the "gestures for everything" idea)
A single list of **reactions**, each = trigger → gesture → optional effect, all data:
- **Attack** (exists): catalog attack or own attack + gesture + ranges + weight.
- **Defensive move** (new): triggers `onTargetNear(d)`, `onHit(chance)`, `onLowHealth`; actions
  `dash away`, `hop back`, `strafe` — implemented as **generic movement behaviours** in C# (like
  attacks: one class, reusable by any enemy), each naming a gesture for its anticipation.
- **Idle gesture** (new): `everyXSeconds` + `chance` + pool of gestures, only while idle/not
  attacking (one more driver layer; reuses `PlayGesture`).
- **State sprites** (generalise what exists per part): for each state — *dormido, detecta al
  objetivo, ataca (global or per attack), herido, muerto* — choose which sprite each part shows.
  Per attack #N a part can use another sprite ("en el ataque 2 uso este brazo"). Data:
  `spriteStates[state or attackId][partUid] = spriteVariantId`. Unity: `RigSpriteSwap` today is
  keyed by 3 fixed states per renderer; extend it to named states + per-attack override applied by
  `RigDriver.ShowSprites(stateName)` (the swap lists stay driver-agnostic).

## Phases (one per session, same working rules as the part-rig plan: static checks only, tests batched in `part-rig-pending-playtests.md`-style file)
| # | Deliverable | Where | Effort |
|---|---|---|---|
| S0 | **Merge** the pending branches after the user's test pass (D–F, drivers). Prerequisite. | git | — |
| S1 | Shell: new tab "Enemy Studio" = Composer canvas + steps; Enemy Creator form embedded as step 4, nothing lost. Template save/load (`rig-template` kind, gestures by role). | web | high |
| S2 | **Gesture phases + FX hooks**: schema (`rig-config` gestures gain `phases[].fx`), Unity `RigFx` + driver spawning (pooled), web: phase tabs in the gesture card with an FX picker and anchor tool on the canvas, preview of the effect in "▶ Probar". | web + Unity | high |
| S3 | **State sprites** generalisation (named states, per-attack override): schema, `RigSpriteSwap` v2 + driver, web board for choosing sprites per state/attack. | web + Unity | high |
| S4 | **Idle gestures** (data + driver layer + web list). | web + Unity | medium |
| S5 | **Defensive moves**: generic movement behaviours (dash away / hop back), triggers, brain hook in `EnemyBrain`, web board entries. | web + Unity | high |
| S6 | Polish: retire Rig Composer + Enemy Creator tabs, one export, docs, templates gallery. | web | medium |

Order rationale: S1 gives the workflow shell the user asked for; S2 is the most visible new power
(effects on phases); S3–S5 are independent and can swap order by the user's priority.

## Decisions to take at S1 (not now)
- Template granularity: body only, body + gestures, or body + gestures + behaviour (recommended:
  all three optional on use, saved together).
- Whether the Studio keeps a per-attack timeline view or just phases per gesture (recommended:
  phases per gesture first; an attack's own timeline is its gesture's).
- Effect anchors: part / muzzle / body / feet is enough; free world points later.

## Risks
- Scope: S2/S3 touch schema + Unity + web at once; each must keep the round-trip test
  (`RigConfigRoundTrip`) green and add its own fields to it.
- Effects baked as frames (VFX Lab) are not tintable at runtime: per-enemy colour variants = new
  baked entry. Revisit only if it becomes a pain.
- Everything is verified statically (compile, schema, headless web, importer tests); the user is
  the only one who can judge feel in Play.
