---
name: design-proposals
description: Generate N proposals for RedMagic (enemy designs, projectiles, VFX effects or sounds) for the user to approve in the web's 📋 Tareas ▸ 🎨 Diseños taller, written as a batch in docs/disenos/. Use when a task from the web asks for "propuestas" / "diseños" of a kind, or the user asks for options to choose from.
---

# design-proposals — options for the user to pick from in 📋 Tareas ▸ 🎨 Diseños

The user only wrote what they want ("tortuga voladora"); the rules of the game's art and data are here.
Output = proposals appended to the batch file the task names (`docs/disenos/<id>.json`, it already exists —
add to `items`, never touch existing ones). Format and what "accept" does: `docs/architecture/designs.md`.
Each item: `{ "id": "<next free>", "kind": "<kind>", "name": "<see below>", "note": "<one line: what makes it
different>", "status": "pending", …proposal }`. Proposals in a batch must be clearly DIFFERENT from each other
(shape, colours, idea) — not seeds of the same picture. Nothing goes to Unity: the user approves first.

**Before ending:** `cd Web/RedMagicWeb/tools && node check-design-batch.mjs ../../../docs/disenos/<id>.json`
must print "Lote correcto". Commit the batch (and any new preset code). Report: one line per proposal.

## enemy — a base design (make-enemy step 1 only)
Read `.claude/skills/make-enemy/SKILL.md` §0 and §1 and follow them: the Fracture look (separate floating rigid
parts with gaps, Rayman-style, one shape per template slot), the template first (`rig-templates.js`), the
prompt words (`STYLE` + `BACKDROP` of `claude-workflow.mjs`: cartoon vector, cel shading, thick black outline,
pure white background). Fit the zone if the request implies one (GDD: `docs/GDD.md`).
- One image per proposal, 1024×1024: `cd Web/RedMagicWeb/tools/comfy && node claude-workflow.mjs run --no-cut
  --size 1024x1024 --seed <N> --name des_<slug>_<n> --prompt "…"` (ComfyUI is open).
- Look at ONE contact sheet of the batch (`node contact-sheet.mjs …`), never single full-size images. Redo (new
  seed / clearer prompt) a proposal whose parts touch, that has arms/legs/neck joining parts, or that is not on white.
- Item: `"image": "comfy:claude/des_<slug>_<n>_raw_00001_.png"`, `name` PascalCase English (`FlyingTurtle`), not an
  existing prefab name (`Assets/Prefabs/Enemies/**`).

## projectile / fx — a ✨ VFX Lab preset with its parameters
The game's effects are procedural presets (`Web/RedMagicWeb/modules/vfx-presets.js` + `vfx-effects.js`; families:
projectile, burst, aura, trail, ground, muzzle). Prefer an EXISTING preset of the right kind (`preset.kind` =
`projectile` or `fx`) with new `params` (colours `core` / `glow` / `accent`, `intensity`, `particles`, `spark`,
`size`, the preset's own params — `paramsOf(preset)`); colours that read on the game's dark backgrounds.
Only when nothing fits, add a new preset to `vfx-effects.js` in its family (same shape as its neighbours; a looping
lane draws the same picture at k=0 and k=1) and use it.
- Item: `"vfx": { "preset": "<id>", "params": { … } }`, `name` letters/numbers/_ (`FrostBolt`).

## sound — a synthesised sound in the game's palette shape
Same shape as `Web/RedMagicWeb/modules/game-sounds.js` (`{ family, label, seconds, loop?, layers }`, layers made of
`T` tone / `N` noise / `P` pings / `C` clicks — written out as objects: `{ "type": "tone", "wave": "sine", "f": [a, b],
"dur", "a", "curve", "gain" }`…). Read 3–4 entries of the same family there first and stay within its length
budget (`GAME_FAMILIES[family].max`); short, readable, not harsh. A loop must close (check-sounds says so).
- Item: `"sound": { … }`, `name` = the sound's id, snake_case with the family's prefix (`enemy_shell_hit`), unused.
