---
name: make-enemy
description: Make a part-based enemy for RedMagic from a request, end to end — art in ComfyUI (base → parts → states → 2k cut), body, projectile, attacks, sounds, Unity import. Use when the user asks for a new enemy "así" / "make an enemy", or to redo one step of an enemy made this way.
---

# make-enemy — request → part-based enemy in Unity

Everything is a script; **do not re-derive the pipeline, run it**. Tools: `Web/RedMagicWeb/tools/comfy/`
(run from that folder). Comfy Desktop must be open (`http://127.0.0.1:8188`). Images land in
`%LOCALAPPDATA%/Comfy-Desktop/ComfyUI-Shared/output/claude/`.
First working example: `enemies/ShroomShaman.json` (2026-10-02).

Token rules: 1024 px while deciding, upscale only what is approved. Look at **one contact sheet per
batch** (`contact-sheet.mjs`), never at single full-size images. Trust the measured checks.

## 0 · Words every prompt carries
- Style: the game's look — painted digital, stylized semi-realistic, thin 2px dark outline, the ZONE's palette (`docs/zonas.json`)
  and 1–3 glowing crystals of the zone's colour. Never write it by hand: `Web/RedMagicWeb/tools/enemy-prompt.mjs --zone <id>
  --target gemini|comfy …` builds the whole base prompt (zone, crystals, pose, pieces, edge rules, white background).
- Body: prefer floaters / rooted turrets made of **separate rigid parts** (GDD, `docs/idea-fracture.md`).
  Pick the template first (`rig-templates.js`: `floatingCaster` = Torso, Head, CastArm, OffArm, Lower,
  Accessory · `foldingSentry` = Base, Stem, Head) and describe exactly those parts.
- **The base must show every part whole (never skip).** The base does NOT need floating parts: a connected
  body (the robot, the turret) is fine. What matters is that **no part is hidden behind another**: a hidden
  part does not exist in the pixels, so step 2 cannot cut it (2026-10-05 test, Gemini art: the bee, whose parts
  are all visible, separated first time; the golem — fists in front of the legs and the other arm, an arm behind the
  chest — and the robot — a forearm behind its cannon — came out with half an arm / half a leg). So the base
  prompt (built by `enemy-prompt.mjs --body … --attack …`, rules per body type in `docs/tipos-enemigo.json`) asks for a neutral pose
  where no part hides another, each part one closed shape with its own full outline. The arms / legs wording applies ONLY to
  `humanoid`; a quadruped has no arms, a floater no legs, a flyer spread wings (2026-10-05: the humanoid wording made wolves, wisps and
  spirits come out as humanoids).
  One shape per template slot, nothing more (extra chunks = extra islands with no slot). Ground enemy: the `Lower`/
  `Hips`/feet pieces rest on the floor.
- **One depth per part (never skip):** a part is drawn whole in front of or whole behind each neighbour — the rig
  has one sorting order per part. Never design a part that goes INTO another (a neck in a pot's mouth, a body in a
  cup, an arm through a ring): it would have to be in front of the back rim and behind the front one. BloomSentry
  (2026-10-03, a stem in a flower pot) failed exactly there.
- **foldingSentry:** the Base is a CLOSED solid seen from the side — a rock, a stump, a crate with its lid on, a
  mound — never a pot / vase / cup / well with a visible opening. Say *"the stem stands behind the base, its foot
  hidden behind the base's top edge"*. Draw order is fixed: Stem behind, Head, Base in front (asleep, the stem shrinks
  to its foot behind the base and the head rests on the base). If an already approved design has an open container,
  close it in the Base edit of step 2: *"…the opening of the pot is covered by a flat stone lid, a closed top"*.

## Por pasos (the web's ▶ Por pasos — for enemies the user doubts; ▶ Montar completo stays for simple ones)
The 🎨 Diseños sheet splits the build in three, with the user in between (sheet: `docs/enemigos/<Name>/enemigo.json`):
- **Paso 1 · cuerpo** (task from `bodyPrompt`): step 0–1 skipped (the design is the base), step 2 (parts in place) and 4 (finish), a
  brief with NO attacks / effects / gestures of its own, `make-enemy.mjs`, import → a body-only prefab. Not steps 5, 6;
  step 3 only for the states the sheet asks for ("Estados pedidos"). The cut: "Piezas elegidas" if the user chose it.
  (The prefab already shows EnemyBrain / attack components with placeholders: the importer adds them by default, no
  tokens spent — leave them.)
  Ends with estado `cuerpo-por-aprobar`, `prefab`, `cuerpo: { fecha, plantilla, partes, brief, resumen }` and the assembled
  picture at `docs/enemigos/<Name>/cuerpo.png`. The user looks at it and, if proportions / distances / layers are off, adjusts it
  in 🧬 Enemy Studio (open from the Unity project, 💾 Guardar en Unity) → `cuerpo-aprobado`. A sprite pendiente + ↻ Rehacer = redo it.
- **Paso 2** (the user, in the sheet): the kind of enemy (flyingRanged, flyingMelee, groundRanged, groundMelee, turret, floatingCaster) OR a
  template enemy to inherit behaviour + animation from, and per asset (projectile, sounds, effects) "de mi biblioteca" or "nuevo" + a
  description (empty = you decide). Stored as `paso2`.
- **Paso 3 · el resto** (task from `restPrompt`): behaviour, deck, gestures, effects, sounds on THAT body. **Never re-import the full zip**
  (it would undo the user's adjustments): write the brief as in step 5 and run `node make-enemy.mjs <brief> --partial` →
  `out/<Name>.partial.zip` (gestures + effects + deck only); import it with `ResetTuning = false`. Movement / EnemyStats tuning is not in
  the partial: set it on the prefab. A template to inherit = copy its brief / prefab's tuning, deck, gestures, retargeted to this body's slots.
  Ends with estado `por-aprobar` and `montaje`.

## Piezas elegidas (the sheet's 🧩 Piezas — when the prompt says "THE CUT IS DECIDED BY THE USER")
The user lists the pieces to SEPARATE, not the anatomy. "Head, Torso, Lower" on a robot with arms and legs = three
images: the arms are cut together with the torso, the legs with the lower piece. **Never drop anything**: the
assembled body must match the design pixel for pixel. Step 2's edit for a piece erases only the OTHER listed pieces.
A piece note («el torso es el cañón») says where it ends: follow it over your own reading of the picture.
- One piece only (`Body`): no step 2. Remove the background + finish at 2k (step 4) → that single image is the part and
  the assembled body.
- Template: pick the one whose slots fit the pieces; map each piece to one slot and write `only: [slot ids]` in the
  brief (the template's other slots are left out, even required ones). A piece that is the weapon / cannon goes in the
  slot that shoots, or put `slots.<id>.muzzle` on the piece that has the mouth. A piece with no fitting slot: the closest
  one (`accessory` for wings / crests) and say it in the report.

## Estados pedidos (the sheet's 🎭 Estados — any step, finished enemies too)
A state is an **extra sprite of one part** for animations (angry face, open palm). It never changes the base part
or the body. Make it with step 3 on the FINISHED part's raw (`--ref` its raw, `--keep-size`), then step 4.
- **Name**: the rig state when it fits (`uso` in the sheet: asleep | awake | alert | attacking | hurt | dead) →
  `<Slot>__<state>.png`; during one attack → `<Slot>__gesture_<Gesture>.png` if that gesture exists, else
  `<Slot>__extra_<name>.png` (kept in the folder; step 3 renames it to the gesture). make-enemy.mjs skips `extra_`.
- **Transition (frames > 1)**: N drawings from the normal part to the state; the last one IS the state
  (`<Slot>__<state>.png`), the others are `<Slot>__<state>.t1.png … .t<N-1>.png` (make-enemy.mjs skips them: Unity
  does not play transitions yet — task `rigtrans` in todo.json). How:
  1. Make the FINAL state first and check it (compare-variant: outline must not move).
  2. Split the change into features and spread them over the frames — write it down before drawing. Angry, 4:
     t1 brows start to lower · t2 brows lowered, eyes narrowing · t3 frown, eyes narrow, mouth tightening · final.
     Never "a bit angry" for every frame: each frame says exactly which features moved and how far.
  3. Each in-between is an edit of the PREVIOUS frame (t1 from the part, t2 from t1…) — *"Change only the <eyes,
     brows> of image1: <this frame's amount>. Keep everything else identical, same outline…"* — so the sequence
     stays continuous.
  4. ONE contact sheet: part → t1 … final, in order. It must read as one smooth motion: no feature going back, no
     jump bigger than the others, no colour / outline drift. Redo only the bad frame (≤ 2 retries); else keep the
     best and note it in the sheet's `nota`.

## 1 · Base (stop: the user picks)
The user's best bases come from **Gemini** (pasted by hand in the Gemini app): give them the prompt of
`base-prompt-gemini.md` (fixed block + the slot list of the template + the creature). They save the image and
copy it into Comfy's `input` folder (root, no subfolders: `LoadImage` does not list them).
In ComfyUI instead: `node claude-workflow.mjs run --no-cut --size 1024x1024 --seed N --name base_<x> --prompt "<enemy-prompt.mjs --target comfy …>"`
— ONE seed, look at it, redo only with a changed prompt (the web's 🎨 Diseños does both: Prompt mode or ComfyUI mode).
**The art faces right.** If the base comes out facing left (the 2026-10-05 robot did), mirror every final part
horizontally before the brief (same canvas, so they stay assembled) and set `pivot` / `muzzle` for the flipped art.

## 2 · Parts, in place
One edit per template slot, `--keep-size --ref "claude/base_<x>_raw_00001_.png [output]" --no-cut
--name <e>_<Slot>`: *"Erase from image1 <every other part by name, with position words>, painting pure flat white
where they were. Leave only <the part, with position words>, drawn COMPLETE: <what it is made of, from end to end>.
<If something hides it:> Parts of it are hidden behind <the neighbour> in image1: redraw the hidden parts so the
whole part is visible (do not draw <the neighbour>). Same size, same colors, same black outline, in its exact
place on the canvas. The result shows one single object on a pure flat white background."* — listing what to ERASE
works far better than "keep only X" (Knight 2026-10-03: "keep only" failed 5 of 8 parts; "erase …" was clean 15 of 15).
- **Say "COMPLETE" and name what hides the part.** The old ending ("drawn complete with a closed end where it was cut")
  only closes the cut, it does not rebuild what was hidden: the 2026-10-05 golem arm and leg stayed half. With the
  new clause + the hidden piece named, the same parts came out whole in 3 of 3 seeds (arm, forearm sleeve) and 2 of 3
  (leg). Do it from the start for every part that touches another in the base.
- **Name the part by what it IS, not left/right.** "the leg on the left" returned the same leg for both feet; use
  colour / object ("the right leg, under the dark blue fist"), and ERASE the neighbours by those words too.
- **ONE seed per part, ONE contact sheet for the whole enemy** (`contact-sheet.mjs --cell 300`, ~1 look): 5–7 runs ≈ 2 min.
  A part that passes the check (whole, only that part, outline closed, nothing left over) is DONE: never run more seeds
  of it "for comparison" — in the 2026-10-05 test the first seed of the robot arm and the golem arm was the best one and
  extra seeds only got worse or identical (seeds barely change a part; the prompt does). A part that FAILS: change the
  prompt (say what was kept / what is missing, name it by what it is) and run ONE seed; at most 2 retries, then fix the
  base image instead (Gemini, see `base-prompt-gemini.md`). Name seeds in the file (`<e>_<Slot>_s<N>`).
  **Unattended (a web task): no Gemini.** After the 3rd failed try of a part, stop on it: keep the best attempt,
  finish the rest of the enemy, and open the report with "No conseguí separar bien <part>: <what is wrong>" (and a
  `nota` in the sheet). Same for a state. Never a 4th seed.
  (Writing the shell loop: do not reuse `$s` for both the seed and the timer; the names then carry the clock.)
- **Cut for MOVEMENT, not only for "whole" (never skip, user 2026-10-20, Golem de escoria):** a part that rotates
  needs a ROUND joint end (a ball / cap as wide as the limb) centred where it pivots, reaching under its parent, so it
  stays hidden while it turns: never a hook, a thin stub or an end that stops short of the joint (a leg must reach the
  hip). A shoulder pad / armour plate that does not swing belongs to the TORSO, not to the arm (otherwise it flies off
  when the arm rises); the arm then starts at a ball joint under it. When a cut gives a bad joint, edit THAT result
  ("replace the curved hook at the top by a thick round shoulder joint… keep the fist identical"), do not re-cut from
  the base. The same trick fixes a part that keeps a neighbour: edit the result to erase just that neighbour.
- **Pose check before states (never skip):** `node pose-preview.mjs enemies/<Name>.poses.json claude/<e>_poses.png`
  — pivots, draw order, parents, and the poses of the enemy's real moves (from its ficha in `docs/enemigos-diseno/`:
  walk, each attack's wind-up and strike, dead). One sheet; anything that sticks out, leaves a gap or flies off = fix
  that part. Example: `enemies/SlagGolem.poses.json`.
The part stays where it was on the canvas → the parts assemble themselves. If the model keeps other
parts, name each thing to delete and say "the single … at the far left".
Check: `contact-sheet.mjs out.png --under claude/base… <parts>` (each part over the faded base).

## 3 · States (only where a state shows)
Edit the PART (`--ref` its raw, `--keep-size`): *"Change only the <eyes> of <image1>: … Keep everything
else identical… No glow, no halo outside the black outline."* Name: `<e>_<Slot>__<state>`
(`asleep | awake | alert | attacking | hurt | dead | gesture_<Name>`).
Measure: `node compare-variant.mjs <part> <variants…>` — `outlinePct` high = it moved, redo with another seed.
A state may be drawn SMALLER than its part (a mouth closed, leaves folded shut) but never bigger: the part's
silhouette is its outer limit (finish-parts cuts the rest and eats the state's own white backdrop). For a closed
state say *"the same outline as image1, only the opening is closed"*, not "a closed bud" (a new, rounder shape).
A folding rider's `asleep` sprite IS the folded look: make-enemy then turns off the rig's squash
(`foldedRiderScaleY` = 1) — do not set it back in the brief.

## 4 · Finish (stop the first time: the user checks the parts)
Per approved image: `node claude-workflow.mjs finish --ref "claude/<e>_<Slot>_raw_0000N_.png [output]"
--name <e>/<Slot>` (×2 upscale → 2048, background cut). Then `node finish-parts.mjs claude/<e> --order
Lower,CastArm,Torso,Head,OffArm` → `claude/<e>/final/*.png` + `_assembled.png`, `_parts.png`.
It guarantees: same canvas, a state has its part's silhouette and differs only where the change is
dense, one island per part, white fringe eaten, alpha snapped. Read its ✓/✗ lines.
Look at `_parts.png` on magenta: a white rim around any state = redo that state (it cannot pass to Unity).

## 5 · Brief → Unity
Write `enemies/<Name>.json` (copy `ShroomShaman.json`): template, parts folder, `height` (world units,
≈ 2.8 for a normal enemy), `slots.<id>.pivot/muzzle` when the art is not the template's pose (a wand
held diagonally: pivot at the hand, muzzle at the tip), `tuning`, `effects` (VFX Lab presets + colours
— `vfx-presets.js`), `projectile`, `attacks` (types / fields = `boss-catalog.js`; `gesture` = a template
gesture: Flick / Charge / Sweep).
**A finished enemy has no placeholder squares** — always fill these too:
- `effects`: besides the projectiles, a `chargeVortex` (charge), a `muzzleFlash` (release), and for a dash a
- Projectiles are clean: no outer halo / bloom around them. Keep the `clean` param at its default (1) on every projectile effect; never set it to 0.
  `smokePuff` + `dashTrail`, in the enemy's colours.
- `gestureFx`: charge looping on `windup` + flash on `strike` for every gesture an attack uses (anchor `muzzle`,
  `size` in world units ≈ 0.9–1.6).
- `builtIn: [{ "gesture": "Flick" }]` so the normal shot goes through a gesture with effects, and `rig: { "muzzleFlashSize": 0 }`.
- attacks cast from a wand / cannon: `params.fromCastPoint: true` (BulletHell) — otherwise they leave from the feet
  and paint a square warning.
- `defences` (e.g. `DashAway`) with a `gesture` of their own, added under `gestures` (poses by slot id).
- `tuning.moveSoundInterval` ≈ 0.9 for a flyer.
- **Gestures are timelines.** A template gesture is step 0 (wind-up, lasts the telegraph) + step 1 (strike). The
  attack leaves at a *release*: put it where the motion delivers — `releases: { "Charge": [{ "step": 1, "at": 1 }] }`
  = at the end of the strike, not at the end of the wind-up. Own gestures (`gestures`) take any number of
  `steps` and several releases; with `params.volleyPerRelease` a BulletHell fires one volley per release.
- Effect hooks: `step` / `at`, `untilStep` for a loop that spans steps, `facing` (default `direction`: the shot at
  the muzzle, the movement on the body; a round charge swirl = `world`), `layer: "behind"` for clouds and trails.

`node make-enemy.mjs enemies/<Name>.json` → `enemies/out/<Name>.zip`. Then:
`unity command eval --timeout 600 --code 'return RedMagic.Rigging.EditorTools.RigBundleImporter.Import(@"<abs zip>", new RedMagic.Rigging.EditorTools.RigBundleImporter.Options { ResetTuning = true });'`
`ResetTuning = true` overwrites what the user tuned by hand in the Inspector (EnemyStats): on a RE-import first
copy their changes into the brief (deck, defences, numbers), or import with `ResetTuning = false`.
(the call may time out at 5 s while Unity keeps working: poll with a cheap `eval` until it answers — "Main thread
operation timed out after 5000ms" is that, not a failure. If Unity has not answered after ~5 min of polling, or
ComfyUI's API does not answer twice: stop, the report says where it stopped and what to restart.)

## 6 · Sounds
New material → add sounds + a rule keyed on a word of the asset name in `modules/game-sounds.js`
(when inserting text by script use a replacer FUNCTION: `$'` in a replacement string is a pattern),
`node ../bake-sounds.mjs <ids>`, then
`unity command eval --code 'UnityEditor.AssetDatabase.Refresh(); return RedMagic.Audio.EditorTools.SoundPaletteAssigner.Run(false);'`.
Usually nothing is needed: gestures, attack types and projectile materials already have rules.

## 7 · Close
Compile check (`unity command recompile_status`), no Play Mode. Commit. Give the user a short numbered
checklist IN THE REPLY (never in a repo file — they don't read docs for test steps); they grade the new sounds in
Biblioteca Jere ▸ Sounds.
Never overwrite an existing enemy: new name.
