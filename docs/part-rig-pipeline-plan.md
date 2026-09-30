# Plan — part-based enemies from the web, no code (parallel to the sprite pipeline)

Goal: build an enemy or boss made of **separate body parts** entirely from the web app plus one
Unity import, the same way sprite-sheet enemies already work. Code is only written for a **new
attack or a new behaviour**. Once it exists, the web lists it and any enemy can use it.

---

## ▶ How to resume (read this first, next session)

**What to tell Claude:**
> Lee `docs/part-rig-pipeline-plan.md` y ejecuta la **Fase N** (sólo esa). Sigue las reglas de la
> sección "Reglas de trabajo".

(Replace N with the next phase in the table below. Start with **Fase P** (Boss2 polish), then **Fase 0**.)

**Model and effort:** use Claude Opus, with **high** effort for Phases 0, A and D (schema,
importer, refactor: design matters and mistakes are expensive). **Medium** is enough for B, C and E
(web UI on top of a fixed schema).

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
| P | **Boss2 polish** (see "Phase P" below): dash lean/inertia, fist and tip swing direction, moving pose | no | medium | ☐ |
| 0 | Split rig attacks into their own folder and category (catalog + web picker shows "Clásico" / "De partes") | small web change | high | ☐ |
| A | `rig-config` schema + round-trip exporter + **separate** `RigBundleImporter` (rebuilds Robot / Turret / BossIce2 from JSON) | no | high | ☐ |
| B | New web tab **🦴 Rig Composer** (assemble parts, pivots, roles, state sprites, live sway preview, export) | yes | medium | ☐ |
| C | Templates in the Rig Composer (folding turret / armed drone / colossus) | yes | medium | ☐ |
| D | `IAttackHost` refactor → rig (and classic) catalog attacks usable by normal enemies | no | high | ☐ |
| E | Gesture pose editor in the Rig Composer | yes | medium | ☐ |

Order: 0 → A → B → C, then D and E. D is independent of B/C and can move earlier if a normal enemy
with a rig attack is wanted sooner.

Side track, not blocking, decide any time: the **DOTween comparison**. Recommended: do only the
drone in DOTween (`RigDriverTween` + `Robot_Dotween` variant, same `ProceduralRig` data), compare,
and pick one driver. The rig data is driver-agnostic, so the pipeline does not depend on this.

---

## Naming

The attacks that move body parts are called **"Ataques de partes"** in the web (short, says what
they are). In code: namespace `RedMagic.Bosses`, folder `Assets/Scripts/Bosses/Definitions/Rig/`,
base class `LimbAttack` (already the base of all of them). Catalog category key `rig`; classic
attacks `classic`.

---

## Phase details

### Phase P — Boss2 polish (do first; small, independent of the pipeline)
The user compared it with the sprite Coloso de Hielo: the new attacks are better, but the dash and
the repositioning feel worse. Diagnosis:
1. **Wrong swing direction.** In `DashLoop` (and `Dash`) the fists have `rotate +35/+20`. On a
   hanging fist (`pointAngle -90`) that swings the knuckles *forward*; trailing needs a negative
   value. `RigPart.leanFollow` has the wrong sign on the tip (-1.2) and the fists (-0.5): negative
   cancels the body lean. For a hanging part to trail like a pendulum it must be **positive**
   (tip ≈ +0.8, fists ≈ +0.4). Also fix the `leanFollow` tooltip in `ProceduralRig`, which
   currently says the opposite. Check the `GroundSlam` / `Throw` / `PickUP` poses for the same sign
   mistake.
2. **No inertia.** The visual body is glued to the root, so starts and stops are instant. Add a
   generic **follow spring** to the rig data (`followStiffness`, `followDamping`,
   `followMaxOffset`; 0 = off) that is driver-specific in implementation: the `Visual` lags behind
   the root's acceleration and overshoots a little when the root stops. Enable it on BossIce2.
3. **Short dash lean.** The travel-pose blend is 0.2s and the dash is ~0.4s. Give the dash a
   bigger, longer lean: `leanDegrees` / `leanResponse` on BossIce2, a stronger `DashLoop` body
   rotation, and the follow spring.
4. **Instant speed.** `ChargeDashAttack` goes 0 → full speed → 0. Add `accelerationSeconds` /
   `brakeSeconds` (default 0 = unchanged, so the ice boss stays identical). Give BossIce2 its own
   copy of the dash attack (`Hielo2_Embestida`, same numbers + easing) and swap it into its decks,
   like the ×2 rocks.
5. **Moving does nothing visually.** `RigDriver.SetMoving` is a no-op, and the ice boss has a walk
   clip. Add an optional **move gesture** (a `RigGesture` named e.g. `Move`, held and cross-faded
   while `SetMoving(true)`: tip and fists trailing, a slight forward lean, crown drifting back).
   Author it for BossIce2.

All of this is data or generic rig features. Verify statically (pose math in a preview scene); the
user playtests.

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
- **Exporter** `Tools ▸ Web ▸ Rig ▸ Exportar rig a JSON (selección)`: writes the config from an
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

### Phase C — templates
A template is a role checklist plus default sway, gestures and archetype:
- **Torreta plegable**: base, neck (stretch), head with 3 state sprites → Static + Ranged sleeper.
- **Dron con arma**: body parts, gun arm (aim) + muzzle → FlyingRanged.
- **Coloso**: torso, 2 limbs, orbiters, stage sprite → boss base.

Picking a template pre-fills roles and defaults; the user only drops sprites and adjusts. The
default values are the ones in `ProceduralEnemyPacks` / `ProceduralBossPack`.

### Phase D — rig attacks on normal enemies
- Extract `IAttackHost` (origin, facing, ground Y, `SurfaceBelow`, arena bounds, warn prefabs,
  pace/speed scale, body animator/rig, `BossLimbs`, `FacingLocked`, `MoveBody`) from what
  `BossContext` / `BossController` expose. `BossController` implements it, and so does a small
  enemy adapter.
- `EnemyAttack` gets an optional attack asset from the catalog. `EnemyBrain` keeps deciding *when*
  to attack; the asset decides *what*. A regular enemy can then have "Lanzar Puño".
- The web Enemy Creator (part-based side) lets you pick a catalog attack, rig or classic.
- A new attack afterwards = one C# class → compile → catalog → selectable for enemies and bosses.

### Phase E — gesture pose editor
In the Rig Composer, pose the parts for `windup` and `strike` (dragging a part = its offset), name
the gesture, and preview it. Until this exists, gestures come from the template.

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
