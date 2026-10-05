# Base prompt for Gemini (make-enemy step 1) — v3, 2026-10-05

Paste into the Gemini app (new chat, or the notebook — see the notebook A/B). Fill `<…>`. Result goes to
`Assets/Art/Test/` or Comfy's `input` folder (root).

Why these rules (test of 2026-10-05, bee / robot / golem): parts hidden behind other parts are not in the picture
and cannot be cut out later. The bee, every part visible, separated first time; the golem (fists in front of the
legs, one arm behind the chest) and the robot (forearm behind its cannon) came out incomplete. Details of the
test: `.claude/skills/make-enemy/SKILL.md` §0.

```
Create ONE single 2D game enemy sprite for a mobile roguelite: <the creature, one line>, full body, side view facing RIGHT, centered on the canvas, square 1:1 image. One character only (NOT a sprite sheet, no animation frames, no variations).

ART STYLE: the game's own look (see the style sources in the notebook): dark fantasy, painted digital illustration, stylized semi-realistic, soft painted shading with slight texture, a thin dark outline (1-2 px) around the silhouette and a slightly darker contour line where pieces meet. NOT thick cartoon outlines, NOT flat cel shading, NOT vector. Palette of the world it belongs to: <FOREST: emerald and moss green, earthy browns, aged stone | ICE: pale blue, icy white, blue-grey stone and armor>. It carries 1-3 glowing turquoise crystals (embedded in back, shoulders, chest or head, or as its power source); cyan/turquoise appears ONLY on crystals, eyes and magic.

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

BACKGROUND: ALWAYS a perfectly flat, uniform pure white, even if an attached reference has sky or scenery: never paint a background. NO ground, NO shadow, NO glow, aura, particles, mist, smoke or speed lines outside the outline, no effects, no text, no labels, no grid, no border. The character contains no pure white fill touching the background (use pale colors instead) so the cutout stays clean.
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

## Gemini notebook version (v2, 2026-10-05)
v1 used a cartoon-vector style that did not match the game (bee / robot / Ratchet-robot tests looked like another game).
v2 = the GDD art direction + the crystal signature + the game's own enemy sprites as the notebook's SOURCES.
Fixed rules go in the notebook's **Instructions**; each enemy is then a SHORT prompt (creature + world + pieces).
Sources: 6–8 existing in-game enemy sprites of the worlds (turtle, boar, ice wolf, ice golem, ghost, bear, forest wolf).
The per-enemy reference image goes in the chat, not in the notebook. If a result ignores a rule, paste the long prompt
above in that chat and note which rule was ignored.

```
You are the concept artist of RedMagic, a 2D mobile roguelite. Every request is ONE enemy sprite. Always follow these rules, whatever the request says:

OUTPUT: exactly one image, square 1:1, ONE character, full body, side view facing RIGHT (three-quarter view at most), centered. Never a sprite sheet, never several poses or variations. Reply with the image only, no text.

GAME STYLE — the sources of this notebook are the game's own enemies: match them. Dark fantasy, painted digital illustration, stylized semi-realistic, soft painted shading with slight texture, a thin dark outline (1-2 px) around the silhouette and a slightly darker contour line where pieces meet. NOT thick cartoon outlines, NOT flat cel shading, NOT vector art. Slightly muted, cohesive palette of the enemy's world: FOREST = emerald and moss green, earthy browns, aged stone. ICE = pale blue, icy white, blue-grey stone and armor.

CRYSTAL SIGNATURE: every enemy carries 1-3 glowing turquoise crystals like the ones in the sources (embedded in back, shoulders, chest or head, or as its power source). Cyan/turquoise appears ONLY on crystals, eyes and magic; no other neon colors.

The sources are style references only: copy palette, painting style, crystals and proportions, NEVER their creatures. Attached reference images are inspiration only: never copy their design, and ignore their background.

POSE (the character is cut into separate pieces afterwards, so EVERY piece must be fully visible): calm neutral pose, no action pose, no foreshortening. Arms held away from the torso, never crossing in front of the body and never hidden behind it. Hands, fists and weapons never cover legs, the other arm or the head. Legs apart with a visible gap. Wings, tails and accessories spread out. No piece hides more than ~10% of any other piece.

PIECES: build the character from exactly the pieces I list, each a simple chunky closed shape with a clearly visible contour where it touches a neighbour. COUNT RULE: never add extra arms, hands, weapons or accessories; every piece is attached to its neighbour by a visible joint, nothing floats alone. If I say a weapon is the end of an arm, there is no hand.

BACKGROUND: ALWAYS a perfectly flat uniform pure white, even if a reference has sky or scenery: never paint a background. No ground, no shadow, no glow, aura, particles, smoke or effects outside the silhouette, no text, no labels. No pure-white fill touching the background (use pale colors) so the cutout stays clean.
```
