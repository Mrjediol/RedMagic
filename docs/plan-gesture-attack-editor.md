# Plan — the gesture / attack editor (web), then the Unity bridge

Agreed with the user 2026-10-02. The engine side is done and merged (`docs/architecture/procedural-rig.md ▸
A gesture is a timeline`): a gesture is any number of steps, with attack releases and effects at any instant.
What is missing is a comfortable place to author that. **Step 1 is built (2026-10-02); next: Step 2.**

## ▶ How to start (after /clear)

> Lee `docs/plan-gesture-attack-editor.md` y ejecuta el Paso 2.

Read first: `CLAUDE.md`, this file, `docs/architecture/procedural-rig.md` (the timeline section),
`Web/RedMagicWeb/ARCHITECTURE.md ▸ Rig composer / Enemy Studio`, `modules/rig-gestures.js`, `rig-fx.js`,
`gesture-timeline.js`, `rig-export.js`, `attack-cards.js`, `vfx-stage.js`, and `tools/comfy/make-enemy.mjs`.

## What the user asked for

- Edit a gesture as a timeline: add / remove / reorder steps, each with its pose, duration and ease; a
  scrubber to stop the gesture at any instant.
- Release markers on the timeline (one or several), and the point the attack leaves from draggable on the
  drawing — no more "muzzle is a number in the Inspector".
- Effects on the same timeline: instant, anchor, size, facing (direction by default), front / behind.
- An attack and its gesture are edited together: the attack's numbers (damage, speeds, pattern) next to the
  gesture, and a **test stage** that plays it — pose, release, projectile, effects, sound if it has one
  (like "▶ Probar" in the VFX Lab).
- From Unity: an "edit gesture / attack" button on an existing enemy opens the web with that enemy, that
  attack and gesture loaded, sound included; saving in the web writes it into Unity — no export / import.

## Decision: one editor, two doors

The user wants a **focused tab** for "add or change a gesture / attack of an enemy that already exists":
no template picker, no body building — only gestures, attacks, their effects and sounds, plus adding a part
PNG when an attack needs a new state sprite for a part. Not the whole Enemy Studio.
Claude agreed, with one condition: it is the SAME editor module mounted in two places, never a second
implementation —
- inside 🧬 Enemy Studio ▸ Gestos, as one step of making an enemy;
- alone, in the focused tab, opened on an existing enemy (from the library today, from Unity through the bridge).
Two copies would drift apart within weeks (the reason the audio tables and the importers were unified).

## Steps (one per session)

1. ✅ **Timeline editor** as a module (`modules/gesture-timeline.js`), mounted in Enemy Studio ▸ Gestos:
   steps, scrubber, releases, effects. The document's gesture becomes the timeline shape; `rig-export.js`
   writes `steps / releases / fx` (what `make-enemy.mjs` writes today) and reads back what Unity exports
   (`RigConfigExporter` already writes the new shape — until this step the Studio cannot reopen it).
   Templates (`rig-templates.js`) keep their two-step gestures, converted on instantiate.
2. **Attack + gesture together, and the test stage**: the attack card beside the timeline; the stage plays
   the body through the gesture and fires at each release. Faithful for position, instant and direction;
   the pattern is re-implemented in JS only for the projectile attacks (shot, fan, ring) — the other
   catalog types get a simplified view. The real feel is still judged in the game.
3. **The focused tab** ("Gestos y ataques"): the same module on an existing enemy, with "añadir PNG de
   estado" for a part. Export = only gestures + attacks (a partial rig-config the importer merges), never
   the body or the Inspector's tuning.
4. **The bridge**: a small local server inside the Unity editor (idle until called; editor-only, not in the
   build). Unity ▸ Inspector button opens the browser on the focused tab with the enemy loaded (body, parts,
   gestures, the attack, its sound clip); saving posts the gestures / attack back and they are written
   into the prefab and the attack asset. Decide then who wins when the prefab has unsaved Inspector changes.

## Step 1 — what was built, and what Step 2 starts from

- `modules/gesture-timeline.js` = the editor: a dock under the canvas (▶ / scrub ruler / step blocks /
  ◆ releases / one lane per ✨ effect / playhead) + the fields of what is selected. It knows nothing of the
  canvas: `rig-composer.js` is its **host** (the `host` interface at the top of the module) and owns pose
  editing, the playhead's effect on the body, and dragging the release target / effect cross on the canvas.
  The focused tab (step 3) mounts it by providing the same host.
- The document's gesture IS the timeline (`rig-gestures.js`); old drafts and built-in templates convert on
  load / instantiate (`normalizeGesture`). Export writes `steps / releases / fx`; `readRigGestures` reads a
  rig-config back (Gestos ▸ "⬆ Traer de un rig-config.json"). `make-enemy.mjs` authors timelines through the
  document — no conversion of its own any more (ShroomShaman's rig-config came out identical).
- The preview "fires" nothing yet: a release only flashes a ring where the attack leaves. Step 2 hangs the
  attack from there — `rig-composer.js ▸ releaseMarks` is the point where each release's instant, position
  (`releaseMatrix`) and direction are known.
- Not done here, on purpose: the per-release attack picker (needs the engine work in Notes), sound.
- Pending user test: `docs/plan-gesture-attack-editor-tests.md`.

## Step 2 — how the user wants attacks to read (feedback 2026-10-02, after step 1)

The attack list must be built around the gesture's releases; today nothing in "Ataques" mentions them.
- **A plain "one projectile" attack in the catalog** ("Disparo simple"), with its OWN projectile picker
  (placeholder by default, like every attack). "Ataque propio · variante" ("Repetir el ataque propio con
  otro gesto", `builtInVariants`) is not that: it reuses the built-in shot's projectile and numbers and
  cannot choose another. The user finds it confusing — replace it with the catalog attack, migrating the
  variants ([[replace-systems-fully-no-coexistence]]): the built-in attack stays only as the default.
- **Creating an attack = pick its gesture → the card shows one slot per release of that gesture** ("suelta
  1", "suelta 2"…) and asks what leaves at each: a single projectile, a fan, a ring… or nothing. A release
  is a spawn point of an attack, not necessarily of one bullet. Adding / removing a release in the
  timeline adds / removes its slot.
- Engine side = the `RigRelease` work in Notes below (each release launches its own attack).
- Supr acts on what was clicked last: a timeline item when the timeline was (done), else the canvas.
- Tests for the user: short, in the web only; ONE Unity import at the very end, never per step.

## Notes

- **Several attacks inside one gesture — wanted (user, 2026-10-02).** Today several releases = the same
  attack fires several times. The user wants each release to be able to launch its OWN attack, and
  gestures that are not attacks to fire too. Their examples:
  - "shoots twice fast, seems to jam on the third, then attacks again": releases 1, 2 and 4 fire; the third
    beat is a step with a pose and an effect / sound (the jam) and no release — or a release that fires nothing.
  - "a defensive dash that shoots at the end": a defence's gesture (`EnemyDefence.gesture`, held today,
    silent) with a release at its end that launches an attack.
  What it needs in the ENGINE, before or with step 2 (it is engine work, not web):
  - `RigRelease` names what it launches: empty = the attack that launched the gesture (today's behaviour),
    or a specific attack (the enemy's own shot / melee, or a catalog `BossAttack`), or nothing (a feint).
  - Whoever plays the gesture resolves it: `EnemyAttackHost` / `BossController` run that attack at that
    release, from that release's point, aimed at the target of that instant (not the one frozen at the start).
  - A gesture played by a defence or a move (not by an attack) must be able to release: today `Hold` poses
    never run the timeline past step 0 and idle gestures are silent. A defence needs "play the whole gesture
    while moving", with its releases live.
  - Attacks launched this way skip their own telegraph (the gesture IS the telegraph) and must not restart
    the gesture; cooldowns and `Running` have to count the whole gesture as one attack.
  In the editor (step 2): each release marker on the timeline gets an "attack" picker (this attack / own
  shot / another catalog attack / nothing), and the test stage fires each one.
- `ResetTuning` on re-import wipes hand tuning: the focused export must never go through it.
- Placeholder squares, effect direction and layers: see `docs/architecture/enemies.md`.
