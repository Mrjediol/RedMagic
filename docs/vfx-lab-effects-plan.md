# Plan — VFX Lab: a real library of effects

Requested 2026-10-01. The user will be away: **once started, do not stop or ask** until the whole
plan is done and a large set of effects exists. Quality over quantity — an effect that does not
look good is cut, not shipped.

## ▶ How to start (after /clear)

> Lee `docs/vfx-lab-effects-plan.md` y ejecútalo entero, sin parar ni preguntar.

Read first: `CLAUDE.md`, this file, `Web/RedMagicWeb/ARCHITECTURE.md ▸ VFX Lab tab` and
`▸ Effect resolution`, `Web/RedMagicWeb/modules/vfx-presets.js` (whole file, ~400 lines) and
`vfx-lab.js`. Branch: stay on `task/enemy-studio` (not merged yet; the Lab changes of this plan sit
on top of it). Snapshot commit of tracked changes first (never the user's untracked test assets, nor
`koros`). Never push.

## Goal

Today the Lab has **5 presets** (`orb`, `bolt`, `burst`, `charge`, `aura`). The user finds that too
few, and wants many more — the ones this game actually needs. The art of the game is **painted, HD
(parts imported at 2048 px), not pixel art**: effects must read as smooth, luminous and clean at
"Nitidez" 3 (the default), and still loop seamlessly.

## Phase 1 — Survey the game (read only, ~20–30 min, write the result down)

Purpose: decide WHICH effects, from what the game really contains. Read, do not edit:
- `docs/GDD.md` (loop, worlds, enemies, bosses, art direction).
- `docs/architecture/weapons-items.md`, `combat.md`, `bosses.md`, `enemies.md`, `fx-placeholders.md`.
- Items / sets / synergies: the item and weapon definitions (`Assets/Resources/Items/`, the scripts
  under `Assets/Scripts` for items, weapons, shots, statuses — e.g. `SlowStatus`, `GoldMarkStatus`,
  burn / poison / freeze if they exist), the legendary passives.
- The catalog of boss / enemy attacks: `Web/RedMagicWeb/modules/boss-catalog-notes.js` (names and
  one-liners) and the `[ArtSlot]`s in `boss-catalog.js` — every art slot is a consumer of an effect
  (projectile / fx / warning).
- Worlds and their elements (ice, forest, …): what elemental families exist.
- Where effects are consumed today: attack art slots, enemy projectile, **gesture phases** (charge on
  a weapon tip = loop; release = burst; dash = trail; landing = dust), state / status visuals.

Deliverable: a section **"Survey"** appended to this file: the elemental families found, the list of
consumers, and the **final list of presets to build** (target ≈ 25–35 new ones, grouped), each with
one line saying what it is for. Cover at least:
- **Projectiles** (lanes `Move` loop + `Impact`, optional `Awake`): per element (fire, ice shard,
  poison glob, lightning bolt, arcane, shadow, rock, water), plus shapes (arrow / needle, spinning
  blade, big slow orb, small fast pellet, homing wisp).
- **Bursts / impacts** (one-shot `Spawn`): hit spark, explosion, ice shatter, poison splash,
  lightning strike, ground slam dust ring, heal burst, shield break.
- **Charges / auras** (loop, optional `Spawn` in and `End` out): charge-up by element, buff aura,
  shield bubble, status loops over a body (burning, frozen, poisoned, slowed, stunned stars, marked).
- **Trails / motion** (loop): dash trail, speed lines, afterimage smear, footstep dust.
- **Ground / area**: warning circle fill, hazard patch (fire, poison, ice), shockwave ring, beam.
- **Muzzle / release**: muzzle flash (several shapes), spawn / despawn puff, teleport.
Adjust the list to what the survey shows; drop what nothing in the game would use.

## Phase 2 — Engine work in `vfx-presets.js` (only what the presets need)

Presets are DATA + a `draw(ctx, k, p, rnd)` per lane, in a centred, y-up unit square, additive
blending (`lighter`). The five existing ones only use glows, rings and sparks. To make varied,
good-looking effects add **shared drawing helpers** (not one-off code per preset), for example:
- soft-edged shapes: tapered streak / teardrop, shard / crystal polygon, crescent / slash arc,
  lightning polyline (seeded jitter, loop-safe), smoke puff (non-additive `source-over` with alpha —
  additive cannot draw dark smoke; add a per-lane or per-call blend option), flame tongue, droplet,
  bubble, star / cross flare, expanding shock ring with thickness falloff, cone / fan.
- motion helpers: `loopNoise(k, seed)` (periodic so loops close), particle emitters with a lifetime
  that wrap around `k` for seamless loops, ease curves.
- a **secondary colour ramp** (core → glow → accent → dark edge) so fire, ice and poison differ by
  more than hue; keep `core` / `glow` / `accent` as the user-facing colours, with good per-preset
  `defaults`.
Rules: a looping lane must be seamless (frame n wraps to frame 0); a one-shot must not start or end
on an empty frame (the sampler already uses mid-steps); nothing may be clipped by the frame edge at
default parameters (leave margin; the art is centred on the anchor / pivot); projectiles point to
**+X**; keep frame counts modest (6–12) and fps as today; every preset gets a Spanish `label` and
`description` and sensible `params` (2–4 extra sliders at most — the user dislikes noise).
Group the preset picker in the Lab (`vfx-lab.js`) by family (optgroups or a small gallery like the
templates one) once there are 30+.

## Phase 3 — Build the presets, in batches, LOOKING at each one

For each batch of ~5 presets:
1. Write them.
2. **Render them and look.** Real pixels, not a stub: serve the project with the scratch static
   server pattern (a tiny node http server over the repo root) and open
   `http://127.0.0.1:<port>/Web/RedMagicWeb/index.html` in Chrome (claude-in-chrome tools), or bake
   with a real canvas in node if a canvas package installs cleanly in the scratchpad. In the Lab,
   select the preset and screenshot the preview; better, add a temporary/dev **contact sheet** (all
   frames of every lane of a preset in a row, on dark and on a mid-light background) and screenshot
   that — keep the contact sheet as a real Lab feature ("Ver todos los frames") if it turns out
   useful to the user.
3. Judge honestly against: reads at a glance at game size (small!), distinct silhouette from the
   others, seamless loop, no clipping, not noisy, colours right for its element, looks good on both
   dark and bright backgrounds (the game has ice-blue and forest scenes). Iterate until good; cut
   the ones that never get there.
4. Run the headless suite (below) and commit the batch (`feat: VFX Lab — <family> presets`).
Use an isolated origin/port for the browser so the user's own web library is not touched; delete the
test IndexedDB when done; close tabs and stop servers.

## Phase 4 — Close

- Headless checks (extend the existing test, see below): every preset bakes at detail 1 and 3
  without errors, every lane has ≥ 2 frames, boxes inside the sheet, looping lanes' first and last
  frames are consecutive samples (loop closure is by construction: `k = i / n`), `kind` and lane
  names are the canonical ones (`projectile`: Awake / Move / Impact; `fx`: Spawn / Loop / End) so
  `rig-fx.js ▸ pickLane` and the art pickers keep working; saving an entry from each preset produces
  a valid library record.
- One end-to-end sanity pass: hang two of the new effects on a gesture in 🧬 Enemy Studio (a loop on
  Carga, a burst on Golpe), "▶ Probar", export, and import the zip in Unity (frame-step Play as
  documented in `CLAUDE.md` if a runtime look is needed) — the pipeline itself is unchanged, this is
  only to catch a preset whose lanes confuse `pickLane`.
- Docs: `Web/RedMagicWeb/ARCHITECTURE.md ▸ VFX Lab tab` (helpers, families, contact sheet),
  `docs/web-tools-guide.md` if it lists the presets, and append to
  `docs/part-rig-pending-playtests.md`: the list of new effects by family + what to judge (which look
  wrong / which are missing) as tests `VFX1…`.
- Final report to the user: how many presets by family, a few screenshots' worth of description,
  what was cut and why, what still needs their eye.

## Working rules

- No questions, no pauses: decide and continue; note decisions in this file under "Decisions".
- Web only unless something in Unity is truly required (it should not be: effects are baked sheets
  that travel through the existing importers).
- Do not change the look or parameters of the 5 existing presets (saved entries reopen with their
  params); new behaviour = new presets or new optional params with defaults that reproduce today.
- Keep `sprite.density` / "Nitidez" working: draw in the unit square, never in pixels.
- Commit per batch on `task/enemy-studio`; conventional messages; never push.
- Headless test harness: `studio-test.mjs` lived in the session scratchpad and is gone after a
  clear. Rebuild a small one (jsdom + `fake-indexeddb` + `ajv` + `jszip` installed in the scratchpad,
  import `app.js` after putting `window` / `document` globals in place, stub
  `HTMLCanvasElement.getContext`) only as far as these checks need; pixel judgement is done in the
  real browser, not in jsdom.

## Survey

**Status: done 2026-10-02** — 40 presets built (45 with the original five), commit `00ad67a`.

Elemental families the game really has:
- **Ice** — the ice set + `SlowStatus` + `IceBurst`, the ice-cave world, frost wolf, frozen skeleton, ice golem,
  IceRobot, Boss 2 (ice colossus).
- **Fire** — `BuildTag.Fire` / `ElementId.Fire`, `Weapon_BolaDeFuego`, `PlatformDenialAttack` (fixed fires),
  the volcano world.
- **Gold** — the gold set, gilded projectiles, `GoldMarkStatus` (aura + ring).
- **Arcane** (reserved tag; `Weapon_RafagaArcana`, `RayoArcano`, `LanzaAstral`), **electric** (reserved tag),
  **spectral / shadow** (`Weapon_EscopetaEspectral`).
- **Nature** — the hero's lime-green energy with a white-gold core, the forest world (fairy, wisp, root boar,
  Árbol Ancestral). Poison is not a status yet, but it is the forest's obvious hazard.
- **Rock / dust** — boulder throw, slams, quakes, landings. **Lifesteal** (red / green motes, heals).
- Water: nothing uses it → no water preset (Salpicadura recoloured covers it).

Consumers of an effect: an enemy's projectile; the art slots of the catalog attacks (10 `fx`, 9 `projectile`,
2 `prop` — warnings, shockwaves, sweep beam, hazard patches, bullets, orbs, thrown boulder); gesture phases in
Enemy Studio (loop on Carga, burst on Golpe, trail on a dash, dust on a landing); status visuals (slow, gold mark).

Built (family → preset → what for):
- **Projectiles**: Bola de fuego (fire weapon / volcano), Esquirla de hielo (ice enemies), Glóbulo de veneno
  (forest), Chispa eléctrica, Misil arcano (Ráfaga arcana), Orbe sombrío (Escopeta espectral), Roca (boulder
  throw; recolour for ice), Aguja / lanza de luz (Lanza astral, pellets, arrows), Cuchilla giratoria (thrown
  scythes, discs), Orbe pesado (`OrbRingAttack`, boss bullets), Fuego fatuo (wisp, homing shots, souls).
- **Bursts**: Chispazo de golpe (every hit), Explosión (grenade, kill-counter explosion, deaths), Estallido de
  hielo (`IceBurst`), Salpicadura, Rayo caído, Polvareda de caída (landings, `LimbSlam`), Cura (lifesteal),
  Escudo roto (guard broken), Tajo (`MeleeStrikeAttack`).
- **Charges / auras / states**: Carga en espiral (the hero-style charge, lime by default), Aura de mejora
  (buffs, enrage), Burbuja de escudo (`GuardStanceAttack`, `AnchorRitual`), Ardiendo, Congelado (slow),
  Envenenado, Aturdido, Marca (gold mark, locked target).
- **Trails**: Estela de dash (`ChargeDashAttack`, defensive dashes), Polvo de pisada.
- **Ground**: Zona de aviso (rellena) (`GroundSlam`, `SafeZone`), Onda de choque (`ShockwaveAttack`, quake),
  Círculo de invocación (`SummonAdds`, anchors), Suelo en llamas / Charco de veneno / Púas de hielo
  (`HazardFieldAttack`, `PlatformDenial`), Rayo continuo (`SweepBeam`, Rayo arcano).
- **Muzzle / spawn**: Fogonazo de disparo, Nube de aparición, Teletransporte.

## Decisions

- **Three files instead of one**: helpers in `vfx-draw.js`, the new presets in `vfx-effects.js`;
  `vfx-presets.js` keeps parameters + baking + the five original presets (only their helper functions moved;
  their output is unchanged).
- **Looked at with a real canvas in node** (`@napi-rs/canvas`, Skia — same drawing code): one contact sheet per
  preset, every frame on dark / ice-blue / forest green, plus automatic checks (edge clipping, empty frames, loop
  closure k=0 vs k=1). Then the Lab itself in Chrome on an isolated port (gallery, contact sheet, three saves);
  its IndexedDB was deleted afterwards.
- **Cut**: *Líneas de velocidad* (almost invisible on ice, redundant with the dash trail); *perdigón* (= Aguja or
  the existing Disparo con estela, smaller); *afterimage smear* (needs the body's own sprite, not a baked sheet);
  *water* projectile (no consumer); separate per-element charge-ups (one charge + colours does it).
- **Redone until acceptable**: Bola de fuego (was a comb of flames → teardrop down the ramp), Estela de dash
  (comb → soft plume with slivers), Aura de mejora (matchsticks → slivers), Tajo (too thin), Orbe sombrío (too
  small), shield / mark / pale streaks (dark under-shape so they read on ice).
- **Bug the checks caught**: Carga en espiral turned a third of a turn per loop with arms of alternating colour
  → the loop jumped. Rule written down in ARCHITECTURE.md.
- **The five original presets clip slightly at the frame edge** (burst, charge) — left alone on purpose
  (the plan forbids changing their look).
- **Picker = gallery of stills by family** (not optgroups) + backdrop switch + "Ver todos los frames" kept as a
  real feature.
- **Ground convention**: standing effects keep the floor low in the frame (y = −0.55…−0.7); flat ones are centred.
  Asked back to the user as VFX6.
- **One commit for the four batches** instead of one per batch: the families share helpers that kept changing
  while the earlier batches were being judged.
- **End-to-end pass stopped at the web side**: three effects saved through the real Lab in Chrome, their records
  resolved with `pickLane` / `fxArtAsset` (loop → Loop / Move, one-shot → Spawn / Impact, worldWidth 0.96 at
  Nitidez 3). The gesture → export → Unity import half was **not** run: the importers are untouched and the
  standing rule is that the user verifies Unity imports — it is test VFX5.
- Headless harness lives in the session scratchpad (`vfx-test.mjs`, `sheet.mjs`): 3476 checks, 0 failures.
