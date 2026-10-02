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

## Status — 2026-10-02

Steps 1–3 **done**; step 4 (grading) is the user's and is open. Nothing here says a sound is good: only
that it exists, passes the numeric checks and is in its slot.

- Registry after the cleanup: **252 slots** — 141 on `generic_test`, 70 empty, 24 on the enemy fallback,
  17 with a clip of their own.
- After assigning: **230 with a clip of their own, 0 on `generic_test`**. Left without one, on purpose:
  8 music slots (out of scope), 10 enemy slots that stay on the generic fallback (Move / Attack, and
  Hurt / Death of koros), the declared "Tomo del Destino" (its moment does not exist yet), `Player ▸ Hurt`
  and `IceRobot ▸ Gesture · Finger` (removed by the user in the Sounds tab — respected) and
  `IceBoss2 ▸ Gesture · DashLoop` (a looping gesture: a one-shot there would retrigger).
- 213 slots assigned from **118 sounds**; **46 are options** (`_2`, `_3`…, and the first takes for slots that
  already had a clip) assigned to nothing; 2 are defaults waiting for content (`enemy_wake`, `interact_use`). 166 clips.

## Inventory (what each empty slot IS → the sound it got)

| What it is | Sound(s) |
|---|---|
| Player: land, ranged shot, death, heal, low health, pick up | `player_land`, `player_shoot`, `player_death`, `player_heal`, `player_low_health`, `pickup_item` |
| Weapon fire / charge (one per weapon) | `weapon_fire_bolt` · `_burst` · `_fireball` · `_shotgun` · `_grenade` · `_lance` · `_beam`, `weapon_charge` |
| Weapon projectile: impact (by element), terrain, expire, explosion, beam loop | `weapon_hit_arcane` · `_fire` · `_spectral` · `_lance` · `_beam`, `weapon_grenade_bounce`, `weapon_hit_terrain`, `weapon_expire`, `weapon_explosion`, `weapon_beam_loop` |
| Enemy projectile: launch / hit player / hit terrain / expire, by material (rock, ice, laser, leaf, wave, energy) | `proj_launch_*` (6), `proj_hit_*` (4), `proj_terrain_*` (3), `proj_expire` |
| Enemy hurt / death by material (ice-stone, metal, spirit), wake, evade | `enemy_hurt_ice` · `_metal` · `_spirit`, `enemy_death_*`, `enemy_wake`, `enemy_wake_metal`, `enemy_evade` |
| Rig gestures (body foley under the attack's sound) | `gesture_charge` · `_flick` · `_sweep` · `_shoot` · `_hop` · `_dash` · `_slam` · `_step` · `_pickup` · `_throw` |
| Boss: appears, hurt, dies, phase change, vulnerable window | `boss_activate`, `boss_hurt`, `boss_death`, `boss_phase`, `boss_vulnerable` |
| Attacks, by attack type (bosses + enemy decks) | `attack_dash` · `_shockwave` · `_throw` · `_pickup` · `_melee_heavy` · `_slam` · `_pincer` · `_shard_dive` · `_spikes` · `_quake` · `_ignite` · `_fire_loop` · `_orb_ring` · `_bullet_burst` · `_cast` |
| Interactables: use / open / take, prop hit / broken | `anvil_use`, `book_open`, `chest_open`, `wardrobe_open`, `mirror_use`, `loot_take`, `prop_hit`, `prop_break` (`interact_use` = default for new ones) |
| Legendary passives | `passive_land` · `_drop` · `_gold_drop` · `_choice` · `_revive` · `_shield` · `_slow` · `_upgrade` |
| Build & status | `status_slow`, `status_gold_mark`, `synergy_tier` |
| Economy | `coin_gold`, `coin_diamond`, `coin_skull`, `coin_soul` |
| Run flow | `run_start`, `run_over`, `run_complete`, `section_enter`, `section_clear`, `wave_start`, `enemy_spawn`, `door_locked`, `reward_appear` |
| UI + shop (menu open / close is one pair for every menu) | `ui_click`, `ui_focus`, `ui_back`, `ui_deny`, `ui_menu_open`, `ui_menu_close`, `ui_upgrade`, `ui_level_up`, `shop_enter`, `shop_reroll` |
| Music | — out of scope |

Which slot got which: `Tools ▸ RedMagic ▸ Audio ▸ 4b` prints it, and the Sounds tab shows it.

**Options** (assigned to nothing; pick one in the Sounds tab to try it). Requested by the user for
everything heard all run long. Where the slot already had a clip (`player_jump`, `player_dash`,
footsteps, `enemydmg1` on melee, `ui_hover`, the generic enemy hurt / death) that clip stays and only
the numbered options were added:
`player_jump_2/3/4`, `player_air_jump` + `_2`, `player_dash_2/3/4`, `player_footstep_2/3/4`,
`player_land_2/3`, `player_melee` + `_2/3`, `player_shoot_2/3`, `player_hurt` + `_2`, `pickup_item_2`,
`weapon_fire_bolt_2/3`, `weapon_fire_burst_2`, `weapon_hit_arcane_2/3`, `weapon_hit_terrain_2`,
`weapon_expire_2`, `proj_launch_energy_2`, `enemy_hurt_2/3/4`, `enemy_death_2/3`, `boss_hurt_2`,
`enemy_spawn_2/3`, `coin_gold_2/3`, `ui_click_2/3`, `ui_hover_2/3`, `ui_focus_2`,
`ui_menu_open_2`, `ui_menu_close_2`.

## Step 4 — how to grade (user)

1. Hear them first without Unity: web ▸ ✨ VFX Lab ▸ **🔊 Sonidos del juego** (by family, ▶ per sound).
2. In play: Biblioteca Jere ▸ Sounds, one folder at a time, in this order (most heard first):
   Player → Player/Attack + Projectiles/Weapons → UI + Shop → Economy → Enemies → Projectiles →
   Bosses (+ Attacks) → Run Flow → Interactables → Legendary Passives → Build & Status.
3. Grade each row. Tell Claude the Bad / Horrible ones **with a word about why** ("too long", "too
   sharp", "sounds like a toy", "want it lower") — that is what a remake needs. To try an option, assign
   `Assets/Audio/Sfx/Game/<family>/<id>_N.wav` to the row.
4. A remake keeps the file name (same GUID): every slot that uses it follows on the next bake.

## Decisions

- **All families at once**, not "one family first": the slots were on a test beep, so a first take
  everywhere costs nothing, and the user asked for the whole plan plus options. The feedback loop is
  step 4, per family.
- **`game-sounds.js`, a sibling of `vfx-sounds.js`**, one synthesiser (`renderSound(id, def)`). One layer
  option added: `fm: [ratio, depth]` on a tone, for metal / bells / coins (anvil, robot hits, gold).
- **The rules travel with the sounds** (`GAME_ASSIGN` → `palette.json`), and Unity's
  `SoundPaletteAssigner` only applies them. A rule matches what the thing is (attack type, material
  word in the asset name), so the next boss or projectile needs no new rule — re-run menu 4.
- **Enemy fallback slots** get a clip only where the material is clear (Golem → ice / stone, Robot /
  Turret → metal, Spirit → ethereal). Everything else stays on the generic of `SystemSounds`.
- **Bosses** got Hurt / Death of their own (they were on the generic *enemy* fallback).
- **Gestures and attacks both sound** on rig enemies / bosses: the gesture is quiet body foley (volume
  0.6–0.8), the attack is the event. If it reads as doubled in play, silence the gesture rows.
- **Naming**: `snake_case` like the existing `player_jump.wav`; option = `_2`, `_3`.
- **Not done**: music; `Player ▸ Hurt` (removed by the user — `player_hurt` and `_2` exist as options).
