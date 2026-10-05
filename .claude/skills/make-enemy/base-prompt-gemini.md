# Base prompt for Gemini (make-enemy step 1) — v3, 2026-10-05

Paste into the Gemini app (new chat, or the notebook — see the notebook A/B). Fill `<…>`. Result goes to
`Assets/Art/Test/` or Comfy's `input` folder (root).

Why these rules (test of 2026-10-05, bee / robot / golem): parts hidden behind other parts are not in the picture
and cannot be cut out later. The bee, every part visible, separated first time; the golem (fists in front of the
legs, one arm behind the chest) and the robot (forearm behind its cannon) came out incomplete. Details of the
test: `.claude/skills/make-enemy/SKILL.md` §0.

```
Create ONE single 2D game enemy sprite for a mobile roguelite: <the creature, one line>, full body, side view facing RIGHT, centered on the canvas, square 1:1 image. One character only (NOT a sprite sheet, no animation frames, no variations).

ART STYLE: <theme and palette: e.g. "frozen cave, deep blues, icy whites and pale blue-grey stone; cyan accents only on crystals and eyes">. Clean stylized digital illustration, flat-ish fills with simple two-tone shading per material, a thick, bold, uniform near-black outline around the whole silhouette AND around every individual body part, hard crisp edges. High-saturation vivid colors, strong contrast, nothing dull or washed out. Glow only as a lighter fill inside the outline.

POSE (very important — the character will be cut into separate pieces afterwards, so EVERY piece must be fully visible):
- A calm neutral pose: standing or hovering, NOT an action or attack pose, no foreshortening, no dramatic perspective.
- Arms held slightly away from the torso, never crossing in front of the body and never hidden behind it. Hands/fists/weapons never cover the legs, the other arm or the head.
- Legs apart with a visible gap between them; neither leg hides the other.
- Wings, tails, antennae and accessories spread out so none is covered by another piece.
- No piece hides more than about 10% of any other piece.

PIECES — exactly these, each one a simple chunky closed shape with its own complete outline and a clearly visible contour where it touches a neighbour:
<one line per template slot, with its position, e.g.
1. Head: …
2. Torso: …
3. Weapon arm (nearest to the viewer, extended forward): …
4. Other arm (away from the viewer, hanging beside the body, fist fully visible): …
5. Lower body / legs / thruster: …>
Nothing else: no extra decorations that would become loose pieces.

BACKGROUND: perfectly flat, uniform pure white. NO ground, NO shadow, NO glow, aura, particles, mist, smoke or speed lines outside the outline, no effects, no text, no labels, no grid, no border. The character contains no pure white fill touching the background (use pale colors instead) so the cutout stays clean.
```

## Template slots (rig-templates.js) to describe in PIECES
- `floatingCaster`: Head, Torso, CastArm (aims and fires — holds the weapon, forearm fully visible), OffArm, Lower
  (flame / skirt / tail / thruster), Accessory (optional).
- `groundHumanoid`: Head, Torso, Hips (waist), WeaponHand (arm + fist, whole), BackHand (other arm + fist, whole),
  FrontFoot and BackFoot (each leg whole: thigh, knee, boot), Cape (optional).
- `foldingSentry`: Base (closed solid), Stem, Head.
- Insects / creatures with wings: put each wing in a slot (OffArm, Accessory), the abdomen in CastArm or Lower.

## Check the image before spending ComfyUI time (≈ 30 s)
For every slot: can I trace its whole outline? Any arm / leg / weapon cut by another piece → ask Gemini again with
"the <piece> is hidden behind the <other>: move it so it is fully visible". Cheaper than 20 cut attempts.

## Gemini notebook version (v1, 2026-10-05)
Fixed rules go in the notebook's **Instructions** (they apply to every chat in it); each enemy is then a SHORT prompt
(creature + pieces). Sources: only 2 style images of looks the user approved (never a design to copy); the
per-enemy reference image is attached to the chat, not to the notebook. If a result ignores the rules, paste the long
prompt above in that chat and note which rule was ignored.

```
You are the concept artist of RedMagic, a 2D mobile roguelite. Every request is ONE enemy sprite. Always follow these rules, whatever the request says:

OUTPUT: exactly one image, square 1:1, ONE character, full body, side view facing RIGHT, centered. Never a sprite sheet, never several poses or variations. Reply with the image only, no text.

STYLE: clean stylized cartoon digital illustration, flat-ish fills with simple two-tone shading per material, a thick bold uniform near-black outline around the whole silhouette AND around every individual body part, hard crisp edges. High-saturation vivid colors, strong contrast, nothing dull or washed out. Glow only as a lighter fill inside the outline. The sources of this notebook are STYLE references only: copy their rendering (outline, shading, saturation), NEVER their characters, shapes or colors. Reference images attached to a request are only inspiration: invent an original design that is simpler and cartoonier.

POSE (the character is cut into separate pieces afterwards, so EVERY piece must be fully visible): calm neutral pose, no action pose, no foreshortening or dramatic perspective. Arms held away from the torso, never crossing in front of the body and never hidden behind it. Hands, fists and weapons never cover legs, the other arm or the head. Legs apart with a visible gap. Wings, tails, antennae and accessories spread out. No piece hides more than ~10% of any other piece.

PIECES: build the character from exactly the pieces I list, each a simple chunky closed shape with its own complete outline and a clearly visible contour where it touches a neighbour. No extra decorations that would become loose pieces.

BACKGROUND: perfectly flat uniform pure white. No ground, no shadow, no glow, aura, particles, smoke, speed lines or effects outside the outline, no text, no labels, no border. No pure-white fill touching the background (use pale colors) so the cutout stays clean.
```
