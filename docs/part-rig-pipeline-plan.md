# Plan — part-based enemies from the web, no code (parallel to the sprite pipeline)

Goal: build an enemy or boss made of **separate body parts** entirely from the web app plus one
Unity import, the same way sprite-sheet enemies already work. Code is only written for a **new
attack or a new behaviour**. Once it exists, the web lists it and any enemy can use it.

---

## ⏸ Where we left off (2026-10-01)

Done: Phases 0, A, B and **C** (2026-10-01), verified by the user: floating caster and limbed
colossus made from templates, imported and playing (boss with centre pivot + `Feet`, attacks and
warnings working). The folding sentry template has not been tried by the user yet.
**Phase D** (2026-10-01) is implemented on branch `task/enemy-attack-deck` and **waiting for the
user's playtest** (checklist at the end of the Phase D section); merge the branch after it.
On top of B, decided with the user:
- **Behaviour of a part-based enemy is authored in 🧩 Enemy Creator** (rigs show up in its art
  picker as "🦴 … (por partes)"); it is saved on the rig record as `enemyConfig` and travels in the
  rig .zip as `enemy-config.json`. No duplicated behaviour form in the Rig Composer.
- **One prefab per enemy, driver included** (`RigDriverCode` on the prefab). The old base + `_Code`
  variant pairs were merged and deleted; never reintroduce variants (memory:
  one-prefab-no-driver-variants).
- The classic "Importar enemigo completo" menu hands a rig .zip to the rig importer.
- Real test by the user: `RobotAgua` authored in the web, behaviour from Enemy Creator, imported,
  flies and shoots in game.

**Next session, in this order** (details in each phase below):
1. ~~Phase C — generic templates~~ done.
2. ~~Phase D — attacks on normal enemies, several per enemy~~ built; **playtest pending** (branch
   `task/enemy-attack-deck`).
3. ~~Phase E — gesture pose editor~~ built (2026-10-01, same branch); **test pending**.
4. **Phase F — VFX preset library** (future, after C; see Phase F). Only planned, nothing built.

Pending from the user side: nothing blocking. Their scratch files `Assets/Json/` and
`Assets/Prefabs/Enemies/Procedural/rigconfi_Code.prefab` (orphan variant) are untracked junk to delete.

## ▶ How to resume (read this first, next session)

**What to tell Claude:**
> Lee `docs/part-rig-pipeline-plan.md` y ejecuta la **Fase N** (sólo esa). Sigue las reglas de la
> sección "Reglas de trabajo".

(Replace N with the next phase in the table below. **Next: Fase F** — see "Where we left off".)

**Model and effort:** use Claude Opus, with **high** effort for Phases 0, A and D (schema,
importer, refactor: design matters and mistakes are expensive). **Medium** is enough for B, C and E
(web UI on top of a fixed schema), and for F.

**Structure:** **one phase per session/turn, not everything at once.** Each phase:
1. Claude reads this doc plus the docs listed for that phase (and nothing else unless needed).
2. `git status`; if there are pending changes, commit them first as `wip: pre-task snapshot`.
3. Implement → compile-check → update docs → commit.
4. **Stop** and give a short test checklist. The user tests in Unity and the browser and reports
   back. Only then continue.
5. Tick the phase in the status table below (in the same commit).

**Context Claude must read at the start of any phase:**
- `CLAUDE.md` (hard rules, compile check, git).
- `docs/architecture/procedural-rig.md` (the rig: data, driver contract, bosses, limbs).
- `docs/web-tools-guide.md` (how the classic web → Unity flow works; the new one mirrors it).
- `docs/schemas/COMPATIBILITY.md` + `enemy-config.schema.json` + `boss-config.schema.json`.
- `Web/RedMagicWeb/ARCHITECTURE.md` (only for web phases B, C, E).
- For reference implementations: `Assets/Scripts/Rigging/Editor/ProceduralEnemyPacks.cs` and
  `ProceduralBossPack.cs`. They are the hand-written version of what the importer must do; every
  line there becomes a field in the JSON.

---

## Reglas de trabajo (apply in every phase)

- **The classic frame-by-frame pipeline is untouched.** Web tabs 🍄 Sprites / 🧩 Enemy Creator /
  🔥 Proyectil-VFX / 👑 Boss Creator and the Unity menus `Tools ▸ Web ▸ Enemy Importer`,
  `Import Config…` and `CombinedBundleImporter` keep working exactly as today. The new system gets
  **its own web tab and its own importer**, next to them. Shared helpers may be reused, never
  changed in a way that alters classic behaviour.
- **Rig attacks are separate.** Attacks that move or throw body parts live in their own folder and
  show up in the web as their own category (see Phase 0). Classic attacks stay where they are.
- **No Play Mode testing by Claude.** Verification = compile check (`unity command recompile` /
  `recompile_status`), console errors, and **static** checks (read the generated prefab/asset data
  with `unity command eval`, preview-scene math checks without Play). Claude never enters Play to
  "see if the enemy works". The user does all gameplay testing and reports back.
- Keep the project's hard rules: localization (no hardcoded player-facing text), pooling, Domain
  Reload off, `SceneReference`, Inspector-first, generic-first, docs updated in the same task,
  conventional commits, never push.
- Replace nothing silently: if a pivot, sorting order or art setting looks wrong, report it.

---

## Status

| Phase | Deliverable | Web? | Effort | Done |
|---|---|---|---|---|
| 0 | Split rig attacks into their own folder and category (catalog + web picker shows "Clásico" / "De partes") | small web change | high | ☑ |
| A | `rig-config` schema + round-trip exporter + **separate** `RigBundleImporter` (rebuilds Robot / Turret / BossIce2 from JSON) | no | high | ☑ |
| B | New web tab **🦴 Rig Composer** (assemble parts, pivots, roles, state sprites, live sway preview, export) | yes | medium | ☑ |
| C | Generic body templates in the Rig Composer (floating caster / folding sentry / limbed colossus) | yes | medium | ☑ |
| D | `IAttackHost` refactor → catalog attacks on normal enemies, **several attacks per enemy** (deck + per-attack gesture) | yes (Enemy Creator) | high | ☑ (playtest pending) |
| E | Gesture pose editor in the Rig Composer | yes | medium | ☑ (test pending) |
| F | **VFX preset library** (web): reusable, tweakable effects (size, colour, intensity…) that attacks/characters reference and that ship inside the enemy export | yes | medium | ☐ (future, after C) |

Order (updated 2026-09-30): C → D → E → F. D does not depend on C; E is what gives each of D's
attacks its own gesture; F comes after the templates exist.

Side track, not blocking, decide any time: the **DOTween comparison**. Recommended: do only the
drone in DOTween (`RigDriverTween` swapped onto a copy of `Robot`, same `ProceduralRig` data), compare,
and pick one driver. The rig data is driver-agnostic, so the pipeline does not depend on this.

---

## Naming

The attacks that move body parts are called **"Ataques de partes"** in the web (short, says what
they are). In code: namespace `RedMagic.Bosses`, folder `Assets/Scripts/Bosses/Definitions/Rig/`,
base class `LimbAttack` (already the base of all of them). Catalog category key `rig`; classic
attacks `classic`.

---

## Phase details

### Phase 0 — separate the rig attacks
- `git mv` (with `.meta`, so GUIDs and asset references stay intact) `LimbAttack.cs`,
  `FistThrowAttack.cs`, `LimbSlamAttack.cs`, `LimbPincerAttack.cs`, `OrbiterDiveAttack.cs` into
  `Assets/Scripts/Bosses/Definitions/Rig/`.
- `ThrowProjectileAttack` stays **classic**. It derives from `LimbAttack` only to *optionally* use
  hands when the boss has `BossLimbs`. Decide in this phase whether to extract its hand path into a
  helper so the classic attack no longer inherits from the rig base. That is cleaner and
  recommended.
- `BossCatalogExporter`: add a `category` to every attack type (`rig` if it derives from
  `LimbAttack`, else `classic`).
- Web Boss Creator attack picker: two groups, "Clásico" and "De partes". The existing configs keep
  working, because the category is display-only.
- Rig attacks' asset files: move `BossAttack_Hielo2_*` into `Assets/Resources/Bosses/Rig/` only if
  nothing loads them by path (check `Resources.Load` usages first). Otherwise leave them and note it.
- Docs: `bosses.md` (folder + category), `web-tools-guide.md` (picker groups).
- **Done (Phase 0 outcome):** `ThrowProjectileAttack` now derives from `BossAttack` and moves the hand
  through the new static `LimbMotion` (in `Rig/`; `LimbAttack` wraps it). The `Hielo2_*` assets were
  **not** moved: `ProceduralBossPack` / `BossConfigImporter` locate attack assets by
  `Assets/Resources/Bosses/` path.

### Phase A — schema, exporter, importer (Unity only)
- New `docs/schemas/rig-config.schema.json` (+ mirror in `Web/RedMagicWeb/schemas`), 1:1 with
  `ProceduralRig` (no renamed fields, like the other schemas):
  - `parts[]`: `name`, `parent`, `sprite` (file), `position`, `rotation`, `scale`, `sortingOrder`,
    `pivot`, `center`, `pointAngle`, `sway{move,rotate,scale,speed,phase}`, `leanFollow`,
    `breaksOnDeath`.
  - Roles: `body`, `aimPart` + `aimArtAngle`, `muzzle` (part + local point), `stretchPart` +
    `stretchRiders`, `limbs[]`, `orbiters[]`, `grips[]`.
  - `spriteSwaps[]` (part + asleep / awake / attacking + `stages[]`), `gestures[]`
    (`name`, `windup[]`, `strike[]` poses, `strikeTime`, `recover`, `shake`), and all rig timings
    (hover, lean, windup/recover, recoil, hurt, stagger, death, fold/wake).
  - `kind`: `enemy` | `boss`, and `driver`: `code` | `dotween`.
- **Exporter** `Tools ▸ Web ▸ Rig ▸ Exportar rig a JSON…` (o clic derecho en el prefab): writes the config from an
  existing prefab. Run it on Robot / Turret / BossIce2 and commit the three JSONs as test fixtures.
- **Importer** `Tools ▸ Web ▸ Rig ▸ Importar rig (.zip / carpeta)…` (**separate** from the classic
  import menus):
  - Parts come **cropped** from the web with their pivot. The importer writes each PNG's import
    settings (sprite mode, ppu, pivot) from the config.
  - Builds `root/Visual/parts`, fills `ProceduralRig`, then reuses `ProceduralRigTool.Setup`
    (enemy stack) + `EnemyConfigImporter` for tuning (`enemy-config.json` in the zip), or
    `BossConfigImporter` for a boss (its schema already has `base.enemyLibraryId`) + `EnsureVariant`
    for the driver.
  - Idempotent like the rest: re-importing updates without duplicating, and tuning done in the
    Inspector is kept unless "reset" is chosen.
- Acceptance: importing each fixture rebuilds a prefab whose `ProceduralRig` data equals the
  original, checked by an eval comparing fields. No Play Mode.
- Docs: new section in `web-tools-guide.md`, `COMPATIBILITY.md` entry, `procedural-rig.md`.
- **Done (Phase A outcome) — what Phase B must produce.** Menus live under `Tools ▸ Web ▸ Rig`
  (import, export, round-trip test). Decisions the web must follow:
  - Part references are **paths relative to `Visual`** (`"Torso/RightArm/Muzzle"`), `"Visual"` = the
    body. Names can repeat under different parents (two `Grip`s), paths can't.
  - Muzzle / grips / empty pivots are ordinary `parts[]` nodes with `rigPart: false` (no special
    role block): the muzzle *is* a node named `Muzzle` under the aim part, `rig.muzzle` points at it.
  - Web-cropped PNGs go in the zip under e.g. `Parts/`, referenced relatively, with
    `sprites["Parts/x.png"] = { pixelsPerUnit, pivot }` (pivot **normalized**). The importer writes
    the texture import settings from that.
  - `rig` block = every `ProceduralRig` field by C# name (partial is fine: absent key keeps the
    value). Boss stack numbers go in `bossBody` (same keys as `BossConfig.body`), boss attacks in a
    bundled `boss-config.json` without `body`; enemy tuning in a bundled `enemy-config.json`.
  - The fixtures in `docs/schemas/fixtures/rig/` are real, importable examples of all of the above.
  - Game numbers travel in a companion `enemy-config.json` (full `tuning`, same schema as the Enemy
    Creator) next to the rig; a boss can instead reference an existing `bossBody.definition`.

### Phase B — web tab 🦴 Rig Composer
- New tab and modules (e.g. `rig-composer.js`, `rig-canvas-view.js`, `rig-export.js`), reusing the
  library, background removal and sprite detection. **Do not modify the classic tabs.**
- Drop part PNGs → auto-trim → drag, rotate and scale on a canvas to assemble the body. Parent by
  drag in a tree; sorting order = list order.
- Click to set pivot (joint), centre and muzzle; drag an arrow to set `pointAngle`.
- Assign roles from a dropdown per part (aim, stretch, rider, limb, orbiter, grip).
- Per-part sprite variants: asleep / awake / attacking, and stages (phase 2).
- Live preview of idle sway / lean / hurt, using the same formulas as `RigDriverCode` (sines,
  pivot correction).
- Library entry kind `rig`. Export `.zip` = cropped PNGs + `rig-config.json` + `enemy-config.json`
  (from the existing Enemy Creator form, embedded or linked).

- **Done (Phase B outcome).** Modules `rig-defaults/model/canvas-view/export/composer.js`, library
  kind `rig` (the document incl. part images is saved as `draft`). The joint IS the sprite pivot,
  so `RigPart.pivot` exports as 0; PNGs cut from one shared canvas assemble themselves; "✂ Dividir
  en piezas" splits a sheet of parts. Not in B: gestures (E), templates (C), fold/aim preview, undo.
  Verified: the Robot PNGs → zip valid against the schema → imported in Unity, art sizes identical.

### Phase C — templates (generic body shapes)
A template is a **body shape**, not a character: a checklist of slots (which parts, which role each
one has, who hangs from whom, draw order) plus default sway, lean, timings, gestures and a suggested
archetype for Enemy Creator. The same template must fit very different art. Name and describe them
by shape; the old names (dron, torreta, coloso) are just the first art that used them.

- **Lanzador flotante** (floating caster) — torso, head, two arms, a lower part (flame, skirt,
  tail, wisp). One arm is the *casting arm* (aim role + muzzle at the hand/weapon tip), the other
  sways; the lower part pulses (sway scale) and trails behind (leanFollow). Hovers and leans.
  Suggested behaviour: FlyingRanged. Fits the drone with a gun, a flying magical girl throwing
  spells from her hand, a floating skull with a claw, a lantern ghost…
- **Centinela plegable** (folding sentry) — base (fixed, doesn't break on death), a stretching
  neck/stem (stretch role, pivot at its bottom), a head riding its tip (rider) with asleep / awake /
  attacking sprites and the muzzle between the eyes. Folds when asleep, unfolds on detection.
  Suggested behaviour: Static + Ranged + sleepsUntilDetected. Fits the jungle turret, a plant that
  pops out of a pot, a periscope, a jack-in-the-box…
- **Coloso con extremidades** (limbed colossus, boss) — torso (body renderer, cracked stage
  sprite for phase 2), a lower part, two throwable limbs (limb role, grip at the knuckles,
  "frente al lanzar" = knuckles), optional orbiting pieces (crown crystals, floating stones), heavy
  inertia. Suggested: boss with the rig attack deck (fist throw, slams, pincer, orbiter dive).
  Fits the ice colossus, a stone golem, a floating pair of gauntlets around a core…

UX: "Nuevo desde plantilla" in the Rig Composer → the parts list shows the empty slots with their
role and hint ("brazo que lanza: suelta aquí la mano/arma"); dropping a PNG on a slot fills it with
the template's pivot guess (bottom for stems, shoulder side for arms), role and sway. Everything
stays editable. Default values come from `ProceduralEnemyPacks` / `ProceduralBossPack` (the
hand-written versions of the three shapes). Store templates as data (one JSON-like object per
template in a web module), so a fourth one is data, not code.

- **Done (Phase C outcome).** `Web/RedMagicWeb/modules/rig-templates.js`: `RIG_TEMPLATES` (data) +
  generic `instantiateTemplate` / `fillSlot` / `matchSlot` / `fitLengths`. Web only, no C# touched.
  - Slots are ordinary parts with `part.slot`; empty ones are dashed placeholders in the parts list
    (label + hint) and on the canvas. Fill by dropping a PNG on the row or the placeholder, or all
    at once (file-name keywords). Guesses applied: pivot, muzzle (+ barrel), grip, centre.
  - Template lengths are written for a reference body height and rescaled to the real art when the
    last required slot is filled (and on demand: "Reajustar amplitudes").
  - **Gestures now export** when the document has them (the template's): caster `Flick` / `Charge`
    / `Sweep` on the casting arm (three spells from one hand — each deck attack picks one, Phase D),
    sentry `Nod`, colossus `Brace` / `Dash` / `DashLoop` / `Move` / `GroundSlam` / `PickUP` /
    `Throw` (the names the existing boss attacks use). They replace the prefab's on reimport.
  - Suggested behaviour: enemy templates carry a tuning (FlyingRanged / Static+Ranged sleeper) that
    travels as `enemy-config.json` until Enemy Creator saves one. ~~Known gap: the Enemy Creator
    form does not preload it~~ — fixed in Phase D: picking the rig there loads it.
  - After the user's first test (2026-10-01): caster draw order is lower → extra → casting arm →
    torso → head → free arm (far arm behind, near arm in front), an optional "Pieza extra" slot
    (ring, belt…) was added, and an **enemy's origin is the centre of its body** (templates are laid
    out around it and re-centred when completed; "◎ Origen al centro" in the toolbar).
  - Second test (2026-10-01): `sortingOrder` exported from 1; empty nodes (`Crown`) have a canvas
    handle and position fields step 0.05; the classic "Importar jefe completo" hands a rig .zip to
    the rig importer; **bosses also pivot at the centre** — new `BossController.feet` (`root.feet`
    in the schema, `Feet` child): planted on the ground and the origin of every attack
    (`BossContext.Origin`), null = root is the base as before. Phase D's `IAttackHost` should expose
    this feet origin for enemies too.
  - Simplifications vs the hand-made packs: the caster's lower part hangs from the torso (no
    separate `Base`); the sentry's stem is the stretch part itself with its pivot at the bottom (no
    `NeckPivot` wrapper — the joint is the sprite pivot); the colossus keeps an empty `Crown` node
    for its orbiters. Boss `rootScale` is left to the user.
  - Checked: the three templates → `buildRigExport` → valid against `rig-config.schema.json`
    (ajv), required-only and fully filled, after a library round trip. Not checked by Claude: the
    UI in a browser and the Unity import of a template-made rig.

### Phase D — catalog attacks on normal enemies, several per enemy
**New core requirement (2026-09-30): a normal enemy can have MORE THAN ONE attack**, like bosses
already do. Reference case: the floating magical girl (Lanzador flotante) with three spells from
the same hand — e.g. a fast bolt, a slow homing orb, a 3-shot fan — each with a slightly different
gesture (a flick, a charged raise, a sweep) and its own cooldown / range / weight.

- Extract `IAttackHost` (origin, facing, ground Y, `SurfaceBelow`, arena bounds, warn prefabs,
  pace/speed scale, body animator/rig, `BossLimbs`, `FacingLocked`, `MoveBody`) from what
  `BossContext` / `BossController` expose. `BossController` implements it, and so does a small
  enemy adapter.
- `EnemyAttack` gets an optional **list** of catalog attack assets (a small deck). `EnemyBrain`
  keeps deciding *when* to attack; the deck decides *which* (weights, per-attack cooldown, min/max
  range, "only when the target is above/below", same availability rules as `BossAttack`: `weight`,
  `cooldownSeconds`, `cooldownInAttacks`). The existing single built-in attack (melee / ranged
  from `EnemyTuning`) stays as the default entry when the deck is empty — no migration of the
  sprite-sheet enemies is needed, and it must keep working unchanged.
- Each attack names its **gesture** (`BossAttack.gesture`, already there): the rig plays that
  `RigGesture` as the telegraph; a sprite-sheet enemy plays the animator state of that name. Three
  spells from one hand = three attacks with three gestures on the same casting arm/muzzle.
- The web Enemy Creator gets an **attacks list** (reusing the Boss Creator attack cards and the
  generated catalog: classic + "De partes"), with the enemy's built-in attack as the first entry.
  Exported in the EnemyConfig (schema change: new optional `attacks` block, mirror of the boss
  one) and imported by `EnemyConfigImporter` / `RigBundleImporter` into attack assets.
- A new attack afterwards = one C# class → compile → catalog → selectable for enemies and bosses.

- **Done (Phase D outcome, 2026-10-01).** Branch `task/enemy-attack-deck`.
  - **Runtime.** `IAttackHost` + `IAttackBody` (`Bosses/IAttackHost.cs`); `BossContext.Host`
    replaces `.Boss` for everything generic (`.Boss` stays, null on an enemy, for the four
    `[BossOnly]` attacks: AnchorRitual, GuardStance, SummonAdds, PlatformDenial). `BossController`
    and the new `EnemyAttackHost` implement the host; `BossAnimator` and `EnemyAnimation` the body.
    The feet origin the plan asked for is `IAttackHost.FeetPosition` (enemy: optional `feet` child,
    else the collider's bottom).
  - **Where the deck lives: `EnemyTuning.attacks`**, not a list on `EnemyAttack` as first written
    above — `EnemyStats` is the one component edited by hand and the tuning block is what the
    recipe/pipeline already copies, so sprite-sheet and part-based enemies both get it for free.
    `EnemyAttack` draws (`Choose`) and `EnemyAttackHost` runs the attack. Built-in attack =
    `builtInAttack` / `builtInWeight`; empty deck = unchanged behaviour.
  - **Gestures.** Rig → `RigGesture` by name; sprite sheet → Animator state by name (released by
    its `OnAttackRelease` event).
  - **Data.** EnemyConfig gains `attacks` + `artAssets` (the BossConfig blocks, by `$ref`) and
    `tuning.attacks` / `builtInAttack` / `builtInWeight`; `EnemyDeckImporter` (shared by
    `EnemyConfigImporter` and `RigBundleImporter`) reuses `BossConfigImporter`'s attack writer and
    `BossArtBuilder`. Catalog entries carry `hosts: boss|any`. The deck is rewritten on every import.
  - **Web.** Enemy Creator ▸ *Ataques*: built-in card + catalog cards (new shared
    `attack-cards.js` / `attack-export.js`; `boss-form.js` untouched, still its own copy). The
    form now **loads the saved behaviour** of the entry picked as art (closes Phase C's known gap:
    a template rig opens with its suggested tuning).
  - **Checked by Claude (static, no Play):** compile clean; web export of a 3-attack caster valid
    against the schema and identical after a save → reopen round trip; the form driven headless
    (jsdom) end to end; `EnemyDeckImporter` and the full rig import with a deck (assets, cards,
    `EnemyAttackHost`, reimport without reset replaces the deck and keeps the rest);
    `RigConfigRoundTrip` OK. **Not checked:** anything in Play, the form in a real browser.
  - **Known limits.** Enemy FX slots (`EnemyAttackHost` warn/shockwave… prefabs) start empty =
    code placeholders, no auto-filled placeholder prefab set like a boss's. A Static non-sleeper
    still needs the target within `verticalTolerance` before any card is considered. Deck attacks
    do not raise the enemy's `OnAttack` sound (each attack has its own `sound`). A sprite-sheet
    gesture plays at the clip's own speed (not retimed to `telegraph`).
  - **Playtest checklist (user).**
    1. Any existing enemy (sprite sheet and `Robot` / `Turret`), untouched: attacks exactly as
       before. Any existing boss (BossIce2 + one sprite boss): every attack, warnings, charge,
       thrown rock, hazards cleared on death — the refactor touched all of them.
    2. Web ▸ Enemy Creator: pick the 🦴 caster rig as art → its saved/suggested behaviour loads.
       *Ataques*: add 2–3 attacks (e.g. Lluvia/abanico with gesture `Sweep`, Corona de orbes with
       `Charge`, Onda de choque with `Flick`), different distances, export the .zip.
    3. Unity ▸ *Tools ▸ Web ▸ Rig ▸ Importar rig* (**Conservarlo** is enough): assets appear in
       `Assets/ScriptableObjects/EnemyAttacks/<name>/`, `EnemyStats ▸ Ataque · baraja` lists them,
       the prefab has `EnemyAttackHost`.
    4. Play: the enemy alternates its built-in shot and the spells, each with its own arm gesture
       and warning; ranges and "only when above/below" respected; `attackCooldown` between attacks;
       killing it mid-attack leaves nothing behind.
    5. A sprite-sheet enemy with one catalog attack whose gesture is one of its animations.
    Report: which attack misbehaves (name + what you saw), whether a gesture fails to play, whether
    the built-in attack fires at the wrong moment, anything left on screen after death.

### Phase F — VFX preset library (future, after C)
Goal: stop making one effect per character. A library of **effect presets** in the web (e.g.
"disparo de hechizo", "orbe cargándose", "impacto mágico", "aura de aviso", "estela"), each with
tweakable parameters — size, colours/gradient, intensity/glow, particle count, speed, duration,
shape — previewed live. Characters/attacks pick a preset + overrides (like art slots pick a
library entry today), and the enemy/boss export carries the effects so it lands in Unity with them.

To decide when it starts (not now): the Unity side of a preset — a parametrised pooled prefab
(`FxPlaceholderStyle`-like component reading the parameters) vs a particle system template vs
baked frames; how presets reference each other (a spell = muzzle flash + projectile trail + impact);
where they live in the library (a new kind `vfx-preset`?). Constraints that already apply:
pooling for anything spawned repeatedly, placeholders swappable as prefabs, no per-character code.

### Phase E — gesture pose editor
In the Rig Composer, pose the parts for `windup` and `strike` (dragging a part = its offset), name
the gesture, and preview it. Until this exists, gestures come from the template.

- **Done (Phase E outcome, 2026-10-01).** Web only, no C# touched (the importer already read
  `gestures`). Inspector ▸ **Gestos**: list, ＋ new, rename (keeps `moveGesture` in sync),
  duplicate, delete, `strikeTime` / `recover` / `shake`. **Posar carga / Posar golpe** turns the
  canvas into a pose editor: Mover / Rotar / Escalar write that part's offset in the pose (drag on
  empty space = the whole body, `Visual`), the rest pose stays as a ghost, numbers editable in the
  panel, Supr removes a part from the pose, Esc leaves. **▶ Probar** plays carga → golpe → reposo
  with `RigDriverCode`'s formulas (`rig-gestures.js`). Only rig parts can be posed (same rule as
  the driver). Gestures made here show up in Enemy Creator ▸ Ataques after saving the rig.
  - Checked by Claude: formulas against the driver's, the editor driven headless (jsdom: pose by
    drag and by number, tool guard, play, new gesture, undo), export valid against the schema.
    Not checked: a real browser (drawing, feel of the drags), the result in Unity.
  - Tests for the user: `docs/part-rig-pending-playtests.md`.

---

## Why this is feasible (short)
- The body is already **data** (`ProceduralRig`).
- New attack classes already reach the web through the auto-generated catalog
  (`BossCatalogExporter` → `boss-catalog.js`, rebuilt on every compile).
- The config importers already write fields by reflection.

What's missing is a schema for the body, a separate importer, a separate web tab, and one refactor
so normal enemies can use catalog attacks.

## Recommendation carried over
Crop the parts (no 2048 px canvases with the piece floating inside): lighter on mobile, and pivots
come from the tool instead of per-part hand fixes like the ones this session needed.
