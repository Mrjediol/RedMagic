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
- Style: `STYLE` + `BACKDROP` of `claude-workflow.mjs` (cartoon vector painted, cel shading, thick black
  outline, pure white background, no glow outside the outline).
- Body: prefer floaters / rooted turrets made of **separate rigid parts** (GDD, `docs/idea-fracture.md`).
  Pick the template first (`rig-templates.js`: `floatingCaster` = Torso, Head, CastArm, OffArm, Lower,
  Accessory · `foldingSentry` = Base, Stem, Head) and describe exactly those parts.

## 1 · Base (stop: the user picks)
`node claude-workflow.mjs run --no-cut --size 1024x1024 --seed N --name base_<x> --prompt "A 2D game enemy
sprite, full body, side view facing right, centered… <parts> … STYLE BACKDROP"` — 5 candidates, one
contact sheet, the user chooses.

## 2 · Parts, in place
One edit per template slot, `--keep-size --ref "claude/base_<x>_raw_00001_.png [output]" --no-cut
--name <e>_<Slot>`: *"Keep only the <part> of <image1>: … drawn complete with a closed rounded end where
it joined. Remove <every other part by name>. Same position, same size, same shapes, same colors and
same black outline as in <image1>. Erase everything else to pure flat white background."*
The part stays where it was on the canvas → the parts assemble themselves. If the model keeps other
parts, name each thing to delete and say "the single … at the far left".
Check: `contact-sheet.mjs out.png --under claude/base… <parts>` (each part over the faded base).

## 3 · States (only where a state shows)
Edit the PART (`--ref` its raw, `--keep-size`): *"Change only the <eyes> of <image1>: … Keep everything
else identical… No glow, no halo outside the black outline."* Name: `<e>_<Slot>__<state>`
(`asleep | awake | alert | attacking | hurt | dead | gesture_<Name>`).
Measure: `node compare-variant.mjs <part> <variants…>` — `outlinePct` high = it moved, redo with another seed.

## 4 · Finish (stop the first time: the user checks the parts)
Per approved image: `node claude-workflow.mjs finish --ref "claude/<e>_<Slot>_raw_0000N_.png [output]"
--name <e>/<Slot>` (×2 upscale → 2048, background cut). Then `node finish-parts.mjs claude/<e> --order
Lower,CastArm,Torso,Head,OffArm` → `claude/<e>/final/*.png` + `_assembled.png`, `_parts.png`.
It guarantees: same canvas, a state has its part's silhouette and differs only where the change is
dense, one island per part, white fringe eaten, alpha snapped. Read its ✓/✗ lines.

## 5 · Brief → Unity
Write `enemies/<Name>.json` (copy `ShroomShaman.json`): template, parts folder, `height` (world units,
≈ 2.8 for a normal enemy), `slots.<id>.pivot/muzzle` when the art is not the template's pose (a wand
held diagonally: pivot at the hand, muzzle at the tip), `tuning`, `effects` (VFX Lab presets + colours
— `vfx-presets.js`), `projectile`, `attacks` (types / fields = `boss-catalog.js`; `gesture` = a template
gesture: Flick / Charge / Sweep).
`node make-enemy.mjs enemies/<Name>.json` → `enemies/out/<Name>.zip`. Then:
`unity command eval --timeout 600 --code 'return RedMagic.Rigging.EditorTools.RigBundleImporter.Import(@"<abs zip>", new RedMagic.Rigging.EditorTools.RigBundleImporter.Options { ResetTuning = true });'`
(the call may time out at 5 s while Unity keeps working: poll with a cheap `eval` until it answers).

## 6 · Sounds
New material → add sounds + a rule keyed on a word of the asset name in `modules/game-sounds.js`
(when inserting text by script use a replacer FUNCTION: `$'` in a replacement string is a pattern),
`node ../bake-sounds.mjs <ids>`, then
`unity command eval --code 'UnityEditor.AssetDatabase.Refresh(); return RedMagic.Audio.EditorTools.SoundPaletteAssigner.Run(false);'`.
Usually nothing is needed: gestures, attack types and projectile materials already have rules.

## 7 · Close
Compile check (`unity command recompile_status`), no Play Mode. Commit. Add a numbered checklist to
`docs/part-rig-pending-playtests.md`; the user grades the new sounds in Biblioteca Jere ▸ Sounds.
Never overwrite an existing enemy: new name.
