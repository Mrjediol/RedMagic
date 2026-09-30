# Plan — part-based enemies from the web, no code (like the sprite pipeline)

Goal: build an enemy/boss made of **separate body parts** entirely from the web app + one Unity
import, the way sprite-sheet enemies already work (`Enemy Creator` → `Importar enemigo completo`).
Code is only written for a **new attack or a new behaviour**; once it exists, the web lists it
and any enemy can use it.

## What already exists (reused as-is)

| Piece | Where | Role in the new flow |
|---|---|---|
| Web library, canvas view, background removal, sprite detection | `Web/RedMagicWeb/modules/*` | Import part PNGs, auto-trim full canvases |
| `EnemyConfig` schema + Enemy Creator form | `docs/schemas/enemy-config.schema.json` | Stats/tuning of the enemy — unchanged |
| `BossConfig` + Boss Creator, `BossConfigImporter` (type + `params` by reflection) | web + `Bosses/Editor` | Boss decks/phases/attacks — unchanged |
| **Attack catalog auto-export** (`BossCatalogExporter`, regenerated on every compile) | → `boss-catalog.js` | A new attack class shows up in the web automatically (the limb attacks from this session already do) |
| `ProceduralRig` data + `RigDriver` contract | `Assets/Scripts/Rigging` | Everything a part-based body needs is **data**: parts, sway, pivots, centres, pointAngle, gestures (poses), sprite states/stages, fold/stretch, aim/muzzle, limbs/orbiters/grips |
| `ProceduralRigTool.Setup` / `EnsureVariant` | `Rigging/Editor` | Enemy stack + driver variant on top of the imported body |
| `libraryId` lazy projectiles | `FxPrefabBuilder` | Projectiles/FX referenced from the rig or attacks |

The packs written this session (`ProceduralEnemyPacks`, `ProceduralBossPack`) are exactly the
hand-written version of what the importer will do from JSON: each line in them becomes a field.

## What is missing

### 1. `rig-config` schema + Unity importer (`RigBundleImporter`)
A new `docs/schemas/rig-config.schema.json`, 1:1 with `ProceduralRig`:
- `parts[]`: `name`, `parent`, `sprite`, `position`, `rotation`, `scale`, `sortingOrder`,
  `pivot`, `center`, `pointAngle`, `sway{move,rotate,scale,speed,phase}`, `leanFollow`,
  `breaksOnDeath`.
- roles: `aimPart`+`aimArtAngle`, `muzzle` (point on a part), `stretchPart`+`riders`,
  `limbs[]`, `orbiters[]`, `grips`.
- `spriteStates[]` (part + asleep/awake/attacking + `stages[]`), `gestures[]` (name + windup/strike
  poses), timings (windup/recover/hurt/death…).

The importer: import the PNGs (**cropped parts + pivot set from the config**; no more 2048
canvases, which are also heavy for mobile), build `root/Visual/parts`, fill `ProceduralRig`, then
reuse `ProceduralRigTool.Setup` (enemy stack) + `EnemyConfigImporter` (tuning) or
`BossConfigImporter` (boss, via the schema's existing `base.enemyLibraryId`) + `EnsureVariant`.
A **round-trip exporter** (`Robot`/`Turret`/`BossIce2` → JSON) validates the schema against what we
already built, before the web exists.

### 2. Web tab "🦴 Rig Composer"
- Drop part PNGs, auto-trim, **drag/rotate/scale them on a canvas** to assemble the body; parent by
  drag-in-tree; sorting order by list order.
- Click to set pivot (joint), centre, muzzle; drag an arrow for `pointAngle`.
- Per part, sprite variants per state (asleep/awake/attacking) and per stage (phase 2 torso).
- **Live preview** of idle sway / lean / hurt in the browser (same sine formulas as
  `RigDriverCode`), so tuning is visual.
- Gesture editor: pose the parts for windup and strike (drag = offset), named gestures.
- Export `.zip` = PNGs + `rig-config.json` (+ `enemy-config.json` from the existing Enemy Creator).

### 3. Templates ("behaviours")
A template = a role checklist + default sway/gestures + default archetype, e.g.:
- **Torreta plegable** — asks: base, neck (stretch), head + 3 state sprites → Static+Ranged sleeper.
- **Dron con arma** — asks: body parts, gun arm (aim) + muzzle → FlyingRanged.
- **Coloso** — asks: torso, 2 limbs, orbiters → boss base.
Templates are data in the web; picking one pre-fills roles and defaults, you only drop sprites.

### 4. Attacks usable by normal enemies (the one real code change)
Today a normal enemy has one attack (ranged/melee/explode in `EnemyTuning`); the limb attacks are
`BossAttack`s and need `BossContext`, which is tied to `BossController`. Plan: extract an
`IAttackHost` (origin, facing, ground, arena, limbs, warn prefabs, pace) implemented by both
`BossController` and a small enemy adapter, so `EnemyAttack` can run **any attack asset from the
catalog** (e.g. a regular enemy with "Lanzar Puño"). After that, a new attack = one C# class →
appears in the catalog → selectable for enemies and bosses from the web.

### 5. Adding a new attack/behaviour afterwards
Write the class (derive `LimbAttack`/`BossAttack`), compile → catalog regenerates → the web shows
it with its fields → pick it on any enemy/boss. No other step.

## Order and cost

| Phase | Deliverable | Needs web? |
|---|---|---|
| A | `rig-config` schema + round-trip exporter + `RigBundleImporter` (rebuilds the 3 current enemies from JSON) | No |
| B | Web Rig Composer (assemble, pivots, roles, state sprites, live preview, export) | Yes |
| C | Templates (turret / drone / colossus) | Yes |
| D | `IAttackHost` refactor → catalog attacks on normal enemies | No |
| E | Gesture pose editor in the web (until then gestures come from the template) | Yes |

A is the foundation and proves the schema; B is the bulk of the work; D is independent and can go
before or after B.

## Pending decision: which animation driver the pipeline targets
Rig data is driver-agnostic, so the pipeline works with either. Recommendation: **don't redo all
three enemies in DOTween**. Do the DOTween pass on the drone only (cheapest, exercises the whole
contract: sway, aim, recoil, hurt, death), compare, pick one driver, and build the pipeline for
that one. Doing all three twice would spend another session on a comparison the drone already
answers.
