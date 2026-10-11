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
The task names a **mode** and a **zone** (`docs/zonas.json`: ice, volcano, forest… — palette, crystal colour, materials).
Read `.Codex/skills/make-enemy/SKILL.md` §0–§1 and `base-prompt-gemini.md` (why the rules exist). The prompt of EVERY
proposal is built by `Web/RedMagicWeb/tools/enemy-prompt.mjs` (zone look, crystal signature, pose / pieces / edge rules);
you only write what changes per proposal:
- The task names a **body type** and an **attack type** (`docs/tipos-enemigo.json`; the user picked them in the web): pass them as
  `--body humanoid|quadruped|flying|floating|libre --attack weapon|magic|none`. They carry the anatomy, pose and default pieces —
  never write pieces by hand unless the design needs different ones, and never contradict them (no arms on a quadruped, no legs
  on a floater). `floating` = a robot held up by a thruster. `weapon` = a separate piece that fires the projectile; `magic` = the plain
  creature, NO weapon, and `--cast "<part that casts: hand, mouth…>"` (drawn idle, a casting version is made later); `none` = melee.
  `libre` = the prompt dictates nothing about the body.
- `--creature` one line (what it is), `--design` shapes / colours / personality, `--crystals` where its 1–3 crystals grow or what
  they power (the game's signature). The zone gives the COLOUR PALETTE only: the creature is not covered in the zone's material
  (a robot stays metal, a forest creature need not be covered in leaves). Optional `--piece "Name: …"` overrides the default pieces.
- Every piece must be fully visible (no fist in front of a leg, no arm behind the chest, no weapon hiding a forearm); name
  the sides by the image ("the arm on the LEFT of the image"); a weapon that is the end of an arm has no hand; ONE flat
  colour for a flame. A user's reference image is inspiration only: invent something original, simpler, in the zone's look.
- Proposals in a batch must differ clearly (shape, idea, template), all inside the zone's palette.
- `name`: PascalCase English (`FlyingTurtle`), not an existing prefab (`Assets/Prefabs/Enemies/**`).

**PROMPT mode** (default; the user has no paid Gemini plan, they paste by hand): you do NOT draw and ComfyUI is not open.
Item: `{ id, kind: "enemy", name, note, status: "pending", prompt: "<stdout of enemy-prompt.mjs --target gemini …>" }` — no
`image`: the user pastes the picture Gemini gives them on the card (🖼 / Ctrl+V). Run `node tools/enemy-prompt.mjs …` from
`Web/RedMagicWeb/` and put its output in `prompt`.

**COMFY mode** (ComfyUI is open): prompt = `enemy-prompt.mjs --target comfy …`; draw it with
`cd Web/RedMagicWeb/tools/comfy && node Codex-workflow.mjs run --no-cut --size 1024x1024 --seed <N> --name des_<slug>_<n> --prompt "<prompt>"`.
ONE seed per proposal; look at ONE contact sheet (`node contact-sheet.mjs …`, never single full-size images); check each
piece can be traced whole, no extra limbs, pure white background; redo only a failing proposal with a CHANGED prompt
(max 2 retries). Item: `"image": "comfy:Codex/des_<slug>_<n>_raw_00001_.png"` (+ the `prompt` it was drawn from).

## projectile / fx — a ✨ VFX Lab preset with its parameters
The game's effects are procedural presets (`Web/RedMagicWeb/modules/vfx-presets.js` + `vfx-effects.js`; families:
projectile, burst, aura, trail, ground, muzzle). Prefer an EXISTING preset of the right kind (`preset.kind` =
`projectile` or `fx`) with new `params` (colours `core` / `glow` / `accent`, `intensity`, `particles`, `spark`,
`size`, the preset's own params — `paramsOf(preset)`); colours that read on the game's dark backgrounds.
Only when nothing fits, add a new preset to `vfx-effects.js` in its family (same shape as its neighbours; a looping
lane draws the same picture at k=0 and k=1) and use it.
- Item: `"vfx": { "preset": "<id>", "params": { … } }`, `name` letters/numbers/_ (`FrostBolt`).
- **Projectiles are clean: no outer halo / bloom.** Every `kind: projectile` preset bakes with `clean: 1` (VFX Lab "Limpiar halo"); never lower it or pass `clean: 0` for a projectile. Sharp silhouette, glow only inside it.

## sound — a synthesised sound in the game's palette shape
Same shape as `Web/RedMagicWeb/modules/game-sounds.js` (`{ family, label, seconds, loop?, layers }`, layers made of
`T` tone / `N` noise / `P` pings / `C` clicks — written out as objects: `{ "type": "tone", "wave": "sine", "f": [a, b],
"dur", "a", "curve", "gain" }`…). Read 3–4 entries of the same family there first and stay within its length
budget (`GAME_FAMILIES[family].max`); short, readable, not harsh. A loop must close (check-sounds says so).
- Item: `"sound": { … }`, `name` = the sound's id, snake_case with the family's prefix (`enemy_shell_hit`), unused.
