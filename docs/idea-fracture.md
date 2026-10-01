# Idea — "Fracture": the game's theme built on part-based creatures

Noted 2026-10-02. **An idea, not a decision.** It goes ahead only if part-based enemies keep proving
much easier to make and manage than sprite-sheet ones with the workflow built so far (🧬 Enemy Studio,
and the experiment in `docs/plan-comfyui-enemy-workflow.md`). Until then nothing in the game or in
`docs/GDD.md` changes.

## The idea

Two things already true about the project:
- the current theme is **bioluminescent crystals** (the forest, the hero's crystal backpack, the portal);
- the enemies that turn out best are the **part-based** ones (separate pieces, rigged in code).

Join them and make it the main theme. The game is called **Fracture**.

**Opening cinematic (short):** an ancestral crystal cracks. As it cracks we see the world being
corrupted, and living creatures with it — a couple of example enemies **fracture on screen, their bodies
splitting into parts**.

## Why it is worth it

- It gives a **reason in the world** for enemies being made of separate pieces: they are not "rigged in
  parts", they are *fractured*. What is a production shortcut becomes the identity of the game.
- It gives a lot of room for **fun enemy design**: pieces that float apart, reassemble, are thrown,
  orbit, get knocked off and come back. Attacks like the fist throw, the pincer and the orbiter dive
  already exist and fit this for free.
- **Animation gets easier**, not harder: floating parts need no joints that look anatomically right;
  a gap with crystal light in it is the style.
- It would make the game **look like nothing else**.

## What it would touch if it goes ahead

- Name and identity: title, logo, main menu, the web tool's brand (it uses the portal today).
- `docs/GDD.md`: pitch, enemy design rules (the fracture as a rule: where the glow goes, how parts
  separate), art direction (crystal light in the seams).
- A shared visual language for the seams: a glow / crystal shard between parts — one effect, reused by
  every enemy (a candidate for a VFX Lab preset and a rig option, not per-enemy art).
- Existing sprite-sheet enemies: redo as fractured, keep as "uncorrupted" fauna, or retire.
- The hero: is the hero fractured too, immune (the crystals on the backpack), or the cause?
- The intro cinematic itself: the cracking crystal, the corruption spreading, two enemies fracturing.

## Open questions

- Does the fracture have gameplay meaning (parts as weak points, enemies that lose pieces as they lose
  health, a boss that comes apart per phase) or is it only the look?
- Is it one crystal and one event, or does each world have its own shard (forest, ice, volcano)?
- How does the run / hub loop read in this story (collecting shards? mending the crystal?).

## How to decide

After making a few more part-based enemies end to end (ideally the "three in a row" test of the ComfyUI
plan): if they are clearly cheaper and better than sheet enemies, write the theme into the GDD and plan
the rename and the cinematic. If not, this stays here as an idea.
