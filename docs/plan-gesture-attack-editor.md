# Plan — the gesture / attack editor (web), then the Unity bridge

Agreed with the user 2026-10-02. The engine side is done and merged (`docs/architecture/procedural-rig.md ▸
A gesture is a timeline`): a gesture is any number of steps, with attack releases and effects at any instant.
What is missing is a comfortable place to author that. Nothing here is built yet.

## ▶ How to start (after /clear)

> Lee `docs/plan-gesture-attack-editor.md` y ejecuta el Paso 1.

Read first: `CLAUDE.md`, this file, `docs/architecture/procedural-rig.md` (the timeline section),
`Web/RedMagicWeb/ARCHITECTURE.md ▸ Rig composer / Enemy Studio`, `modules/rig-gestures.js`, `rig-fx.js`,
`rig-export.js`, `attack-cards.js`, `vfx-stage.js`, and `tools/comfy/make-enemy.mjs` (it already writes
the timeline JSON: the shape to match).

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

1. **Timeline editor** as a module (`modules/gesture-timeline.js`), mounted in Enemy Studio ▸ Gestos:
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

## Notes

- Several DIFFERENT attacks inside one gesture is not supported by the engine yet (several releases = the
  same attack fires several times). Ask the user if it is wanted before step 2.
- `ResetTuning` on re-import wipes hand tuning: the focused export must never go through it.
- Placeholder squares, effect direction and layers: see `docs/architecture/enemies.md`.
