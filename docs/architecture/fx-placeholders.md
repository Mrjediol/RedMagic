# Placeholder visuals → real art (`Assets/Prefab/Fx/`)
Every repeatedly-spawned visual (weapon projectiles, boss bullets, boss telegraph rectangles,
waves, sweep beams, hazard patches, platforms, anchors) is **built in code** as a tinted
geometric shape by default — but each now also has an **optional prefab slot** on its data
asset. Assign a prefab and the visual comes from that prefab through `PrefabPool` instead;
swapping in real art is then: open the prefab, delete the shape sprite, drop the real sprite
(or add a trail / particles / an Animator). No code, no per-consumer wiring beyond the one field.

- **`Fx.FxPlaceholderStyle`** (`Assets/Scripts/Fx/FxPlaceholderStyle.cs`) — the marker a
  *placeholder* prefab carries. `Apply(tint, worldSize, sortingRef)` does exactly the per-instance
  styling the code path does (`SpriteRenderer.color`, `AbilityFx.Resize`, `AbilityFx.CopySorting`,
  collider radius/size from `sprite.bounds`), each step gated by a bool (`tint` / `resize` /
  `matchSorting` / `scaleColliderToSprite`). The spawner rule everywhere: **null prefab** → build
  in code, restyle inline (unchanged); **prefab + `FxPlaceholderStyle`** → `PrefabPool.Spawn`,
  `style.Apply(...)`; **prefab, no marker** → real art, spawner only sets position/rotation.
- The null-prefab **code fallback stays on purpose** — safety net + `ShotPipelineHarness`.
- **`Fx.FxTelegraph`** — `PrefabPool`-backed telegraph (grow + fade + despawn), the prefab
  equivalent of `AbilityFx.Flash`. Used by `BossAttack.Warn`/`Mark` when the boss has a `warnPrefab`.
- **`Bosses.BossFxSpawn`** — the shared "prefab or code pool" branch + release routing for the
  five pooled boss runtime types.
- Prefab slots: `BaseShot.projectilePrefab` / `beamPrefab` (weapons), `ProjectileSpec.prefab`
- **Projectile rule: clean, no outer bloom.** VFX Lab common param `clean` (0–1) cuts the soft halo at bake; `defaultParams` sets it to 1 for every `kind: projectile` preset. Re-bake an old projectile to apply it.
  (boss bullets / ability projectiles — restyle-aware now), `BossController.{warnPrefab,
  shockwavePrefab, sweepBeamPrefab, hazardPrefab, platformPrefab, anchorPrefab}`.
- **Boss FX are one folder per boss**: `Assets/Prefab/Fx/Bosses/<BossName>/` holds every visual
  for that fight — `Fx_<Boss>_{Warn,Shockwave,SweepBeam,Hazard,Platform,Anchor,Bullet}` — so an
  art pass on a whole boss is one folder, and no boss reads another's prefabs. Each boss's six
  `BossController` slots point at its own copies, and its `BulletHellAttack` assets'
  `projectile.prefab` at its own `_Bullet`. Any new boss visual ships the same way, in that
  boss's folder.
- Generators under **Tools > RedMagic > FX**: `1 · Generar prefabs placeholder` (builds the shape
  sprites in `Assets/Art/Placeholder/` + the weapon prefabs + a per-boss FX set), `2 · Asignar a
  armas`, `3 · Asignar a jefes` (fills null slots per boss) — idempotent. `4 · Migrar FX de jefe
  a carpeta por jefe` was the one-time move off the old shared-by-type `Fx_Boss_*` prefabs.
- **Projectile aiming + orientation is one system, `Gameplay.ProjectileAim`**, used by BOTH paths.
  `ProjectileSpec` (enemy/boss/ability) and `BaseShot` (player weapon) each carry an **Aiming**
  block: `faceDirection` (force rotating toward travel; false = the prefab's own
  `faceTravelDirection` decides, which is how every pre-existing projectile kept its look),
  `facingAxis` (Right/Left/Up/Down = which side of the art is the tip; Right = +X convention) and
  `aimMode` (Fixed / MouseDirection / NearestEnemy — the last two player-only, resolved once at
  spawn: `ProjectileFactory.Spawn` for specs, `ShotResolver.Fire` for weapons so the whole
  volley/burst/beam inherits it). `Projectile` and `ShotProjectile` both re-face every physics step
  through `ProjectileAim.Face`. The web schema (`projectile-config.schema.json`, mirrored in
  `docs/schemas`) and both creators (Enemy + Projectile/FX) carry the three fields;
  `ProjectileConfigImporter` writes them. Don't add rotation or aim code anywhere else.
  A real-art
  prefab makes that attack's `projectileSprite` + phase accent inert (the prefab owns the look).
- The `Platform` / `Anchor` placeholder prefabs must carry their colliders (solid `Ground`-layer
  box; trigger box + `Health` + `HitFlash`) — the generator builds them precisely.
- Any **new** repeatedly-spawned visual follows this: ship it as a placeholder prefab under
  `Assets/Prefab/Fx/` with `FxPlaceholderStyle`, wired through an asset field, code fallback kept.
- **Frame animation is `Gameplay.SpriteFlipbook`, not an Animator** — a sprite array + fps, with
  `pingPong`, `randomStart` and `oneShot` (play once, hold the last frame, expose `Finished` /
  `Duration`). It rewinds in `OnEnable`, which is mandatory for pooling: `PrefabPool` has **no**
  per-instance hook, so `OnEnable` is the only reset a prefab FX gets. `VfxOneShot` only measures
  Animator clips, so a flipbook-driven one-shot must set `lifetime` = `frames / fps` by hand.
  For a *pooled* visual with **several** states, use `Pipeline.SpriteStateMachine` instead — same
  `OnEnable` rewind, plus named states (see the sprite pipeline section).
- **Real art replaces a placeholder by turning the style flags off, not by deleting art.** When a
  sprite carries its own colour, set `tint: false`; when its cell is non-square (a trail), set
  `resize: false` too and let the prefab's own transform scale fix the size — `AbilityFx.Resize`
  scales per-axis from sprite bounds and would squash a wide cell into an egg.
- **Worked example — the Árbol Ancestral's orb** (`Assets/Prefab/Fx/Bosses/ArbolAncestral/Orbe/`):
  `orbe.png` is a hand-supplied reference sheet (three strips, labels and a palette baked in).
  `Tools > RedMagic > FX > Orbe · Cortar hoja` (`Fx/Editor/OrbeSheetSlicer.cs`) segments it into
  `Orbe_Idle` (6) / `Orbe_Move` (8) / `Orbe_Impact` (6), lifts the art off the black background by
  treating the residual over the background colour as alpha, packs uniform cells anchored on the
  orb, and slices them through `ISpriteEditorDataProvider` at 100 px/unit with the pivot on the
  ball (so `Orbe_Move`'s trail hangs behind a correct rotation centre). Band Y ranges are constants
  in that file — re-run its `Diagnose`/`DiagnoseBands` entry points if the source sheet changes.
  `Tools > RedMagic > Boss > Arbol · Orbe` (`Bosses/Editor/ArbolOrbePack.cs`) then builds the
  prefabs, dresses `Fx_ArbolAncestral_Bullet` and authors the attack assets.
- The generic one-shot VFX prefabs (`Fireball`, `VFX_Explosion`) now live in `Assets/Prefabs/Fx/` too
  (moved out of `Assets/Dragon Warrior Files/`; their material/anim dependencies stayed there).
- **Player movement VFX are ParticleSystem prefabs** in `Assets/Prefabs/Fx/Player/` (`VFX_Dash`,
  `VFX_Jump`, `VFX_DoubleJump`), built by **Tools ▸ RedMagic ▸ FX ▸ VFX del jugador · Generar**
  (`PlayerVfxPack`; *Regenerar* overwrites tuned values) and fired by `PlayerVfx` from
  `PlayerMovement.Dashed/Jumped/AirJumped`. `VfxOneShot` supports particles: Play On Awake/Looping off,
  restarted on every spawn *after* the mirror scale is set (Scaling Mode = Hierarchy flips shape and
  velocity), root Stop Action = Callback → `OnParticleSystemStopped` → `PrefabPool.Despawn`;
  `SpawnFollowing` keeps the dash emitter on the player for the dash (streak emits by distance, world
  space); `scaleMultiplier` follows the player's per-scene scale. Materials `Fx_ParticleAdditive/Alpha`
  (URP Particles/Unlit + built-in Default-Particle texture), layer `Characters`. **Gotcha when
  verifying:** a paused Game view doesn't redraw particles — capture in slow motion
  (`Time.timeScale`), not paused.
- **Art from the web's ✨ VFX Lab (`VfxLabArtPack`)** — the route by which the game's own visuals stop being
  geometric shapes. Three pieces of data, no new code per effect:
  1. `Web/RedMagicWeb/tools/vfx-bakes.json` — `{ name, preset, params, lanes? }`; `node bake-vfx.mjs` (once:
     `npm install` in that folder) bakes each one to `Assets/Art/VfxLab/<name>/<Lane>/frame_000.png…` + `bake.json`.
  2. `VfxLabArtPack.Prefabs` — which prefab comes out of which bake / lane, its kind (`ShotProjectile`, `OneShot`,
     `Loop`, `ShotBeam`) and its width in world units. Prefabs land in `Assets/Prefabs/Fx/VfxLab/`. Sprites are
     imported at **1 frame = 1 unit** (PPU = frame side), so the size in game is that width whatever "Nitidez" was
     baked. A weapon projectile's origin is its **head** (the art hangs from an offset child): collider and impact
     point are there.
  3. `VfxLabArtPack.Assignments` — which field of which asset uses it.
  Run **Tools ▸ RedMagic ▸ FX ▸ 5 · VFX Lab · Arte del juego**: idempotent, rebuilds prefabs in place (same GUID).
  **It never replaces hand-made art**: a field is reassigned only if empty, pointing at a known geometric
  placeholder (`ReplaceablePlaceholders`) or at a prefab of this pack — that is why `Weapon_ProyectilRecto` and
  `Weapon_EscopetaEspectral` (their `Fx_Shot_Square` already carries real art) were left alone.
  Wired today: Bola de Fuego, Ráfaga Arcana, Lanza Astral (projectile + impact), Granada Rúnica (projectile +
  explosion), Rayo Arcano (beam + charge loop), the lifesteal heal burst (`SynergyTuning.healVfxPrefab`), and every
  boss `Fx_<Boss>_SweepBeam` that was still a square (dressed in place with a looping beam segment; it keeps its
  `FxPlaceholderStyle`, so the boss still stretches and tints it).
- **New optional art slots on `BaseShot`** (null = exactly the old behaviour): `impactVfxPrefab` (a `VfxOneShot`
  where the projectile hits a target or the terrain, mirrored by travel direction; not used by grenades),
  `explosionVfxPrefab` (replaces the code flash of an exploding shot, `SpawnFitWidth` to the blast diameter) and
  `chargeVfxPrefab` (a pooled loop at the muzzle while charging, scaled 0.35 → 1 of the prefab's size by charge).
  A beam prefab keeps `FxPlaceholderStyle` on purpose: `ShotBeam` then tints it with the weapon / element colour and
  fades it out — its art is baked near-white for that.

## Melee slash (`Fx.MeleeSlashFx`)
Procedural layered slash, no texture: each layer is a hidden quad child (`HideAndDontSave`, rebuilt from
the `layers` list, so Inspector edits show instantly — also in edit mode with `previewLoop`) drawn by
`RedMagic/Melee Slash Layer` (`Assets/Art/Fx/Shaders/MeleeSlashLayer.shader`): a crescent along an arc whose
`_Head`/`_Tail` the component drives; per-layer delay/radius/thickness + tail fray, inner lobes, breakup and
shards give the fanned-tongue look. Independent of the attacker: place at the hit origin, rotate to the
direction, `Play(power)` (power scales size/thickness — build upgrades). Pooled via `PrefabPool`, restarts in
`OnEnable`, despawns itself. Variants = prefabs in `Assets/Prefabs/Fx/Player/Slash/`, built (overwritten) by
**Tools ▸ RedMagic ▸ FX ▸ Tajo melee · Generar variantes + escena de prueba** (`Fx/Editor/MeleeSlashPack.cs`),
which also writes `Assets/Scenes/Test/SlashPreview.unity` (all variants looping). Outer particles per layer: `shardStyle` Rombo / Rayas (speed lines) / Chispas (sparks off the head) / Trozos (chunks off the tail), compared in `SlashPreview_Exterior` (*Tajo melee · Variantes de parte exterior*). The quad is ±1.5 radii so they are not clipped. Planned on top: combo list
(3→5 hits), build-upgraded variants, air/dash hits — keep visual params separate from gameplay params.
- **Movement FX in the slash set**: jump / double jump / dash variants are `MeleeSlashFx` prefabs too
  (`Assets/Prefabs/Fx/Player/Movement/Fx_{Jump,DoubleJump,Dash}_1..4`, *Movimiento · Generar variantes +
  escena de prueba* → `Assets/Scenes/Test/MovementFxPreview.unity`). `MeleeSlashFx.symmetric` opens the arc from
  its centre (`startAngle` = centre) both ways; `shapeStretch` is the prefab's own X/Y squash. `PlayerVfx`
  accepts either a `MeleeSlashFx` prefab (pooled, mirrored by facing, dash one follows the player) or the old
  `VfxOneShot` particles, with a per-effect `Size` + `Offset`. **Depth split** (`depthSplit`, `splitY`,
  `backSortingOrder`): each layer is drawn twice — below `splitY` in front (`baseSortingOrder`), above it behind
  the player (`backSortingOrder` 2 < player 10) — so a ring at the feet wraps the body. Preview scenes are built **additively** and closed after saving — the generators
  never save or close the user's open scenes.
- **Code-drawn FX (`Fx.FxCanvas`)**: effects built from many small shapes each frame (strokes, soft strokes,
  ellipse arcs with dashes, discs/glows, teardrops, triangles) into a vertex-colour mesh
  (`RedMagic/Fx Vertex Color`, material `Assets/Art/Fx/Materials/Fx_VertexColor.mat`). Two canvases per effect
  (back order 2 / front order 40) give the behind/in-front split. Ported 1:1 from web sketches (sizes in sketch
  "px" × `pixel`). `StrokeVar` = per-point width/colour (+soft edges) for brush-like, tapering strokes. Users:
  `DashTrailFx` (departure sphere + brush rings, white thread, comet along the real path — tail stays at the origin
  while travelling and retracts after `End` — with flame-noise width, 5 palette layers, shedding drops, speed lines,
  bow shock of partial strands/teardrops/sparks, wisps; `replaceTarget` hides the target's SpriteRenderers
  during travel) and `ImpactBurstFx` (`ground`: flash, dust spikes + puffs, two ground rings, debris, sparks,
  camera shake; enemy mode: flash, ring, directional shards). Prefabs in `Assets/Prefabs/Fx/Player/Dash/`,
  built + wired to the player by **Tools ▸ RedMagic ▸ FX ▸ Dash y picado · Generar prefabs y asignar al jugador**
  (`Fx/Editor/DashFxPack`, overwrites).
