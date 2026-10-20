# Base prompt for Gemini (make-enemy step 1) — v4, 2026-10-05

Self-contained: the game's look is IN the prompt (no notebook sources — Gemini copied the sources' creatures).
Paste it in a new chat of the Gemini notebook whose Instructions are only the minimal block at the end.
Fill `<…>`. Save the image into Comfy's `input` folder (root).

The ART STYLE / EDGE RULES wording is the one that made the game's existing enemies (ice wolf, forest bears…):
painted semi-realistic, crystals, cyan only on crystals and eyes, thin outline, no glow outside. v1–v3 used a
"cartoon vector, thick outline" style borrowed from the ComfyUI tests and the bee / robot looked like another game.
POSE / PIECES come from the 2026-10-05 separation test: hidden parts cannot be cut out later (SKILL §0).

```
Generate ONE 2D game enemy sprite for a dark fantasy mobile roguelite: <creature, one line>. Side view facing right, full body, centered, SQUARE 1:1 image. One single character only: NOT a sprite sheet, no animation frames, no variations.

ART STYLE: Dark fantasy <WORLD THEME>. <WORLD PALETTE>. Cyan/turquoise accents only on crystals and eyes. Painted digital illustration, stylized semi-realistic, slight texture, no grain/haze outside the character.

CHARACTER: <design: materials, colors, shapes, personality>. <1-3 glowing turquoise crystals: where they grow or what they power>.

POSE (the character will be cut into separate pieces afterwards, so EVERY piece must be fully visible): calm neutral pose, not an action pose, no foreshortening. Arms held away from the torso, never crossing in front of the body and never hidden behind it. Hands, weapons and fists never cover the legs, the other arm or the head. Legs apart with a visible gap. Wings, tails and accessories spread out. No piece hides more than about 10% of another.

PIECES — exactly these, no more (no extra arms, hands, weapons or loose parts; each piece joins its neighbour at a visible joint and has a clearly visible contour where they touch):
1. <slot>: …
2. <slot>: …

EDGE RULES (critical):
- Clean, crisp, hard silhouette with a 2px near-black outline all around, and a slightly darker contour line where pieces meet.
- NO glow, aura, halo, bloom, particles, snow, frost mist, dust, smoke, blur or soft edges outside the outline. Crystal glow stays inside the silhouette.
- ABSOLUTELY NO effects of any kind: no impact bursts, no energy beams, no motion lines. NO cast shadows, NO ground.
- Background: perfectly flat, uniform pure white, zero gradient or noise, even if an attached reference image has sky or scenery. The character contains no pure white fill touching the background (use pale colors).
```

## Side view and joints (2026-10-20, Golem de escoria)
- Gemini returns a FRONT view unless the view comes first and is repeated: open with a `VIEW (most important): strict
  SIDE PROFILE… faces and walks to the RIGHT…` block and give a side pose (front arm forward, back arm swung back and
  visible beyond the torso's back outline, legs in a stride). Gemini edits badly: if the view is wrong, send a whole new
  prompt in a NEW chat, not a correction.
- An arm that starts behind the chest is the hardest piece to cut (the model glues it to the torso). Ask for a visible
  gap or a round ball joint at the shoulders, like the floating head.
- The user downloads the image with Gemini's download button (a screenshot is not square and carries UI). JPEG is fine.

## World blocks (copy into ART STYLE)
- **ICE**: `Dark fantasy frozen cave and ice crystal theme. Cold saturated palette of deep blues, icy whites, pale blue-grey stone.`
- **FOREST**: `Dark fantasy magical forest and crystal theme. Saturated emerald and moss green, earthy browns, aged stone.`
- **VOLCANO**: built by `enemy-prompt.mjs --zone volcano` (palette in `docs/zonas.json`); first base: the Golem de escoria, 2026-10-20.
- New world: take its palette from `docs/GDD.md` ▸ Art direction and the existing enemies of that world.

## Template slots (rig-templates.js) to describe in PIECES
- `floatingCaster`: Head, Torso, CastArm (aims and fires — holds the weapon, forearm fully visible), OffArm, Lower
  (flame / skirt / tail / thruster), Accessory (optional).
- `groundHumanoid`: Head, Torso, Hips (waist), WeaponHand (arm + fist, whole), BackHand (other arm + fist, whole),
  FrontFoot and BackFoot (each leg whole: thigh, knee, boot), Cape (optional).
- `foldingSentry`: Base (closed solid), Stem, Head.
- Insects / creatures with wings: put each wing in a slot (OffArm, Accessory), the abdomen in CastArm or Lower.
- Name arms by the side of the IMAGE ("the arm on the LEFT of the image"): Gemini reads "front / back arm" as left / right.
- A flame / glow that belongs to the body: one flat color, say so.

## Check the image before spending ComfyUI time (≈ 30 s)
For every slot: can I trace its whole outline? Any arm / leg / weapon cut by another piece, an extra limb, or a
background → ask Gemini to fix THAT ("the <piece> is hidden behind the <other>: move it so it is fully visible",
"remove the extra …", "pure white background") in the same chat. Cheaper than 20 cut attempts.

## Notebook Instructions (minimal, no sources)
```
Every request is ONE enemy sprite for the RedMagic game. Follow each request's rules exactly. Reply with the image only, no text. Never reuse the design of an earlier chat in this notebook: each request is a new, original creature.
```
