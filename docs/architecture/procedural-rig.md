# Procedural rig — bodies built from separate sprites (`Assets/Scripts/Rigging/`)
For enemies/bosses whose art is **one sprite per part** (torso, head, arms…) instead of a sprite
sheet. Animation is code, not clips.

## Pieces
- **`ProceduralRig`** — *data only*, shared by every driver: `body` (the `Visual` root: flipped for
  facing, leans, hovers), `parts` (`RigPart` list: sway move/rotate/scale/speed/phase,
  `pivot`, `breaksOnDeath`), aim (`aimPart`, `aimArtAngle`, `aimMaxAngle`), attack timings
  (`windup`, `recover`, anticipation, recoil), hurt, death break-apart. Captures the prefab pose
  once and `RestorePose()` puts back pose **and hierarchy** (death reparents pieces) → pool-safe.
- **`RigPart.pivot`** — real joint point relative to the sprite pivot. The part art is exported
  on full canvases (1024² with the piece off-centre), so the sprite pivot can sit far from the
  piece; sway, scale pulse and death spin happen around `pivot` instead. **Art is never edited.**
- **`RigDriver`** (abstract) — the engine. Contract: `SetFacing` (applied immediately),
  `PlayAttack(aimPoint, speed)` → **`AttackReleased` exactly at the end of `windup`** →
  `AttackFinished` after `recover`; `PlayHurt`, `PlayDeath`, `PlayWake` (→ `WakeFinished`),
  `PlaySleep`. Gestures advance only with scaled time **and** `GameStateManager.CanPlayerAct`.
- **`RigDriverCode`** — pass B (plain code): `LateUpdate` recomposes every part from
  `rest + sine sway + gesture layers`; gestures are coroutines that only write a few numbers
  (aim weight, arm rotation, push, body tilt/kick), so overlapping gestures never fight. Lean
  comes from measured velocity (works for physics, boss AI or knockback alike). Death: pieces
  reparent to the root, fly with gravity, bounce on `breakGround` (Ground+Platform), then
  `Corpse` fades/destroys the whole enemy.
- **`RigDriverTween`** — pass A (DOTween). *Not built yet.*

## Enemy integration
- `EnemyAnimation` finds a `RigDriver` on the root; if present **it wins over Animator/flipbook**:
  every order is forwarded, and the driver's events play the role of the clip's
  `AnimationEvent`s. The timer fallback is only a safety net (duration + 1s).
- `EnemyAttack.AimPoint` exposes the frozen aim point so the rig aims the arm at the same point
  the projectile flies to. A **`Muzzle` nested under the aim part** is found recursively and its
  *world* position is used (a root-level `Muzzle` keeps the old mirrored-local behaviour).
- Timings scale with `EnemyTuning.attackAnimSpeed` / `wakeAnimSpeed`.

## Two-pass comparison (code vs DOTween)
Base prefab (`Assets/Prefabs/Enemies/Procedural/<Name>.prefab`) carries **everything except the
driver**: enemy stack, `EnemyStats`, `ProceduralRig`. Variants add the driver:
`<Name>_Code` (`RigDriverCode`) and `<Name>_Dotween` (`RigDriverTween`). Tune on the base → both
passes change together; the comparison only differs in the engine.

## Tooling (Tools ▸ RedMagic ▸ Procedural)
- `1 · Preparar enemigo por partes (selección)` — `ProceduralRigTool.Setup`: adds the enemy stack
  (Health, Knockback, HitFlash, Corpse, CurrencyDropper, EnemyStats/Brain/Attack/Animation,
  SoundEmitter, Rigidbody2D, collider if missing), `ProceduralRig` with one `RigPart` per sprite
  under `Visual` (golden-angle phases), and migrates old `ProceduralSway` values (the component,
  now in `Legacy/`, is removed). Idempotent.
- `2 · Crear variante _Code (selección)` — driver variant.
- `Packs/Dron (Robot)` — `ProceduralEnemyPacks.RunDrone()`: the above + drone data (aim arm,
  `Muzzle` at the gun tip, flame/ring pivots, FlyingRanged tuning, `Corpse` linger 1.1s) + the
  placeholder projectile `Assets/Prefabs/Fx/Enemies/Robot/Fx_Robot_Projectile` (disc +
  `FxPlaceholderStyle`). Enemy-specific values are seeded **only the first time** (no rig yet);
  afterwards the Inspector wins.

**New part-based enemy** = drop the body prefab (root + `Visual` + parts) in the folder, run
`1` + `2`, set `aimPart`/`Muzzle`/tuning in the Inspector (or add a pack method if it will be
regenerated).

## Enemies
- **Robot** (TestProcedural art) — FlyingRanged drone; `RightArm` is the gun (`aimArtAngle` 11.5°).
