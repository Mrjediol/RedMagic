# Plan — complete the game's missing sounds (synthesised)

Requested 2026-10-02, for the next session. **Do not start until the user says the cleanup is done**
(step 0): they will first delete the temporary enemies / bosses / content so no sound is made for
something that is going away.

## ▶ How to start (after /clear)

> Lee `docs/plan-sounds-completion.md` y ejecútalo. Ya he limpiado lo temporal.

Read first: `CLAUDE.md`, this file, `docs/architecture/audio.md`, `Web/RedMagicWeb/modules/vfx-sounds.js`
(the synthesiser and its 53 sounds) and `Web/RedMagicWeb/tools/bake-sounds.mjs`.

## Where things stand (measured 2026-10-02, before the user's cleanup)

`Assets/Audio/Registry/SoundRegistry.generated.json` — 295 slots. **134 have no clip**, and **143 hold the
test clip** (`Assets/Audio/Test/generic_test.wav`), which is also "missing" for this plan.

| Category | Slots | Empty |
|---|---|---|
| Enemies | 56 | 51 |
| Bosses | 71 | 40 |
| Projectiles | 67 | 28 |
| Music | 15 | 10 |
| Interactables | 13 | 4 |
| Legendary Passives | 9 | 1 |
| Player, UI, Shop, Economy, Run Flow, Build & Status | 60 | 0 (check how many are `generic_test`) |

These numbers will drop after the cleanup: **re-measure first** (Sounds tab ▸ "Regenerar registro", then
read the JSON — never a report file; the rule is in `audio.md`).

## Step 0 — the user's cleanup (theirs, not Claude's)

Delete the temporary enemies, bosses and prototype content (or label them `SoundIgnore`), then press
**Regenerar registro** in Biblioteca Jere ▸ Sounds. Tell Claude when done.

## Step 1 — inventory (read only)

From the regenerated registry, list every slot that is empty or holds `generic_test.wav`, grouped by what
the sound IS, not by owner: e.g. "enemy hurt (small / big / stone / insect)", "boss attack — slam",
"projectile impact — fire", "UI click", "shop buy". Many slots share one sound: the goal is a **small
palette reused widely** (the generic enemy fallbacks in `SystemSounds` already work this way), not 150
unique clips. Write the grouping in this file under "Inventory" and the proposed palette (≈ 40–70 sounds).
Music slots are out of scope (a synthesiser of tones and noise does not write music) — say so and leave them.

## Step 2 — make the sounds

- Extend the synthesiser, do not fork it: new entries in `vfx-sounds.js ▸ SOUNDS` (or a sibling
  `game-sounds.js` with the same shape if the file gets long — one synthesiser, `renderSound` shared).
  Add layer types only if a family needs one (e.g. a formant / vowel layer for creature voices, a
  simple reverb tail for big hits).
- `bake-sounds.mjs` writes them to `Assets/Audio/Sfx/…`; give game sounds their own folder
  (`Assets/Audio/Sfx/Game/<family>/`), keep `VfxLab/` for the Lab's examples.
- Checks that can be automated (extend the scratch `snd-test.mjs` idea): finite samples, peak, not
  silent, no click at the end, loops close, length within the family's budget (UI ≤ 0.25 s, hits ≤ 0.6 s…).
- A way for the user to audition them before they go in: a "Sonidos del juego" list in the web
  (▶ per sound, grouped by family) — the Lab's sound rows already do this for presets.

## Step 3 — assign

A reusable editor tool, not hand-wiring (`build-the-tool-not-the-instance`): a table "slot pattern →
clip" applied through `SoundSlotEditor` (the same API the Sounds tab uses), idempotent, **never
replacing a slot that has a real clip** (only empty or `generic_test`). Per-enemy variation = pitch
range on the cue, not a new clip.

## Step 4 — grading (the user's)

Every assigned sound is a new sound: the user grades each one Perfect / Good / Bad / Horrible in the
Sounds tab (`SoundStatus.json`). Deliver them in batches by family so grading is quick; "Horrible" ones
are removed immediately, "Bad" ones go back to step 2.

## Honest limits

Claude cannot hear. What was verified for the 53 Lab sounds was numbers (length, level, loop seams),
not how they sound. Expect a real share of misses on the first batch: do **one family first** (enemy
hurt / death is the biggest gap), get it graded, and use that feedback before making the rest.

## Inventory

(to be written in step 1)

## Decisions

(to be filled while working)
