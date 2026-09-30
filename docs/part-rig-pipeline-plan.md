# Plan — part-based enemies from the web, no code (parallel to the sprite pipeline)

Goal: build an enemy or boss made of **separate body parts** entirely from the web app plus one
Unity import, the same way sprite-sheet enemies already work. Code is only written for a **new
attack or a new behaviour**. Once it exists, the web lists it and any enemy can use it.

---

## ▶ How to resume (read this first, next session)

**What to tell Claude:**
> Lee `docs/part-rig-pipeline-plan.md` y ejecuta la **Fase N** (sólo esa). Sigue las reglas de la
> sección "Reglas de trabajo".

(Replace N with the next phase in the table below. Start with **Fase 0**.)

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
| 0 | Split rig attacks into their own folder and category (catalog + web picker shows "Clásico" / "De partes") | small web change | high | ☑ |
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
