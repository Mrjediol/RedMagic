# RedMagic — GDD (short)

Shared design source for chat and Claude Code. Design intent only; code architecture lives in `CLAUDE.md` + `docs/architecture/`. Update when a design decision changes.

## Pitch
2D side-scrolling mobile roguelite (Unity, Android landscape, target device Red Magic 11 Pro). Dark fantasy dungeon/forest tone. Small kangaroo-mouse hero with a crystal backpack fights through randomized runs across multiple worlds.

## Core loop
Menu → **MainHub** (upgrades, build management, pick a tomb/world) → run: 5 random sections out of 15 per world → boss → back to the hub (on death or completion). Death resets the run; meta-progression persists outside the run.

## Prototype goal
Playable ~10 min run: ~5 min World 1 + ~5 min World 2. All systems experienceable: kill enemies, try builds, fight bosses, get upgrades between runs. Then balancing (no perfection sought).

## Player
- Hero: small kangaroo-mouse, big ears, backpack with green crystals, green chest gem.
- Attack: universal charge-and-release of energy through a glowing crystal (crystal lights up; no thrown orb) — feeds all projectile types.
- Moves: run, jump, double jump, Skull Slayer-style dash (air and ground dash, independent cooldowns), interact, hurt, death.
- Weapons: 2–3 projectile attacks with progression.

## Builds and items
- Items with unique and set (synergy) passives; real upgrades outside the run; an **anvil** defines shot type/shape.
- In-run shop = physical altars/pedestals in the level (not a UI menu), with a reroll mechanic.
- Meta-progression currency: extension point stubbed in `RunManager`.

## Worlds
- **World 1**: magical forest (dark fantasy, crystals).
- Other themes in exploration: ice cave, infernal volcano. Each theme ≈ 10 maps with varied geometry (walls, hills, heights).
- Maps: very wide, 2 identical open doors (bottom-left = entry, bottom-right = exit), 2–3 big open combat spaces, many platforms/bridges. Sizes mix: coliseum arena with waves, medium 21:9, long.

## Enemies (design rules)
- Generic-first: movement and attack types reusable by any enemy/boss; selectable in the web Enemy/Boss Creator.
- Every attack ships a default placeholder projectile/FX so it is testable before art.
- Roster so far: bear, wolf, forest fairy (flying, ranged), wisp (kamikaze), root boar (charge), frost wolf, frozen skeleton, ice golem.
- Prefer bodies that are easy for AI art to animate (quadrupeds, floaters) over humanoids.

## Bosses
- Boss 1 exists. Boss 2: floating ice colossus (no legs, torso ending in an ice point, two floating fists; attacks: dash, ground slam, power punch, boulder pick-up/throw, fist barrage) — logic and testing pending.
- Every new boss attack becomes a reusable general attack.

## Art direction
Dark fantasy magical forest + crystals. Saturated emerald/forest green, earthy browns, moss, aged stone; cyan/turquoise only on magical elements. Painted digital, stylized semi-realistic. Skill effects: lime-green flame/energy with white-gold core. Sprites: Gemini sheets (5 frames per action, magenta background) → web background remover → web Enemy Creator → Unity importer.

## Audio
Last priority, before the prototype. Source undecided (reuse old game sounds or a generator).

## Rules
- Spanish + English from day one: no hardcoded player text.
- Device-tested workflow; Claude Code does not test in Play Mode — user plays and reports.
