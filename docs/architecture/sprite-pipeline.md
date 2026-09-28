# Sprite / enemy pipeline (`Assets/Scripts/Pipeline/`)
**Read `Assets/_Pipeline/SPRITE_PIPELINE.md` before importing any sprite sheet or adding an
enemy** — it documents the whole thing and is written so this section doesn't have to be re-derived.

A sprite sheet (one row per animation state, one column per frame) becomes a playable enemy through
two ScriptableObject "recipes" and one menu command. Both recipes are assets, so the configuration
survives the session and every step is re-runnable and idempotent.

- **`SpriteSheetRecipe`** (`<Name>.sheet.asset`) — the sheet, the row→state mapping, fps/loop per
  state, background keying, anchor, ppu, and the `AnimRuntime`. **`SheetSlicer`** does not slice the
  original in place: it emits one clean PNG per state into `Assets/Art/Characters/<Name>/` with the
  background removed, labels dropped, uniform cells and the pivot on the same point of the character
  in every frame. `AutoBounds` mode finds bands as the N tallest content runs (N from the recipe, so
  painted-in row labels fall out) and frames as connected components grouped by x-overlap; a row's
  declared `frames` count is authoritative and reconciles stray blobs (a thrown projectile drawn
  loose inside a frame) — it **removes** that blob from the frame count (never merges it into a
  neighbour: a thrown projectile sits far from the body and merging stretched the row's uniform
  cell 2-3× wide, shrinking the character) and **exports it as its own centred sprite**,
  `<Name>_<Row>_Prop.png`, which becomes the projectile below. `Grid` mode is the plain
  rows×columns fallback. **When writing a pack, `SheetRow.frames` counts the character's poses,
  not the drawings in the row** — a 5-drawing attack row where the 5th is the loose projectile is
  `frames = 4`. **`SheetRow.evenSplit`** splits a row into N equal columns instead of
  connected-components, for rows whose FX bleed sideways (a slam's dust, an energy burst) and would
  otherwise merge two poses — `GorilaPack` needs it. Two further per-row knobs, both read straight
  off the sheet with **`Pipeline ▸ 2b · Diagnosticar bandas y manchas`** (prints every blob's box
  and area per band — measure, don't guess): **`SheetRow.groupSlack`** scales the horizontal margin
  that decides whether two blobs belong to the same frame (1 = default, ~1/12 of the character's
  height); lower it when neighbouring poses nearly touch (a galloping horse's tail reaches the next
  frame's muzzle and the 13px default swallows a real 3px gap, merging five frames into one) —
  prefer it over `evenSplit` whenever a real gap exists, because equal columns steal a sliver of the
  neighbour when the drawings aren't perfectly gridded. **`SheetRow.propBlobs`** declares how many
  drawings in the row are the *already-thrown projectile* rather than poses: they are pulled out
  from the right (a sheet always draws them after the last pose) **before** grouping, which is the
  case `frames` alone cannot fix — when the orb is drawn touching the muzzle it has already merged
  into the attack cell by the time the count is reconciled (measured on `Lobo`: cell 192 → 272px,
  last pose off-centre). With several, they export as `_Prop0` / `_Prop1` and the first (rightmost)
  becomes the projectile.
- **Row labels never reach the PNG**: everything outside the chosen bands is cleared from the mask
  before emitting (`Emit` copies source pixels inside a margin around the frame, so the bottom of
  the row above used to end up painted over the character), and inside a band a short blob **flush
  with the band's ceiling** is dropped as a label — the old "blob hanging above the tallest
  character" rule only fired when the word was drawn higher than the mane.
- **`AnimClipBuilder`** — sprites → one `AnimationClip` per state → `AnimatorController`. **Updates
  in place**: existing states keep their hand-tuned transitions, only clips are rewritten and missing
  states added. Parameters are the ones `PlayerAnimator` already uses (`Speed`, `Attack`, `Hurt`,
  `Dead`) — one animation vocabulary for the whole project. **`SpritePipeline.RunSheet` builds the
  clips twice with a `Refresh` between** — on the first import of a brand-new sheet the freshly
  sliced sub-sprites aren't queryable in the same tick and the first pass leaves empty clips
  (1s / 60fps / no events); the second pass fills them, and it's idempotent on every re-run.
- **Terminal animations never loop** (`Pipeline.AnimStates`: Death/Impact/Explo/Muerte/Despawn, exact
  Die/Dead/End/Destroy). Enforced by every clip/state importer, at runtime (`SpriteStateMachine`
  won't loop or fall back after a terminal state, `PlayOnce`; `VfxOneShot` forces a single pass;
  `EnemyAnimation` freezes a looping Death clip on its last frame) and by `Pipeline ▸ 8/9 · …
  animaciones terminales` (`TerminalAnimAudit`). The web exporter defaults `loop: true`, which is
  what made the first frame flash after deaths/impacts. `SPRITE_PIPELINE.md` §5-bis.
- **`AnimRuntime` is not a preference, it follows pooling**: `Animator` for enemies/bosses;
  **`Flipbook` for anything in `PrefabPool`**, because `PrefabPool` has no per-instance reset hook,
  so a reused Animator would resume mid-death. **`Pipeline.SpriteStateMachine`** is the multi-state
  equivalent of `Gameplay.SpriteFlipbook` and rewinds in `OnEnable`.
- **`EnemyRecipe`** (`<Name>.enemy.asset`) + **`EnemyFactory`** — the enemy counterpart of
  `BossAuthoring`. The recipe carries an `EnemyTuning` block (the *same* class `EnemyStats` uses)
  plus presence (sprite scale, collider, sorting, tag); the factory stamps `Rigidbody2D`,
  `BoxCollider2D` (deduced from the sprite when left at zero; origin **at the feet for
  `AnchorMode.BottomCenter`, centred for `AnchorMode.Center`** — a flyer has no feet, and assuming
  bottom-anchored left the collider half a body high), `Health`, `Knockback`, `HitFlash`, `Corpse`,
  `CurrencyDropper` and the enemy quartet (`EnemyStats`, `EnemyBrain`, `EnemyAnimation`,
  `EnemyAttack`) — and **retires** `EnemyController`/`RangedAttack` if the prefab came from the old
  system.
- **Archetype from the sheet's filename** — content is named `<name>-<attack>-<mobility>-<plane>`
  (e.g. `Ogro-distancia-movimiento-suelo`, `abeja-distancia-statica-aire`). `Melee`/`Ranged`/
  `FlyingMelee`/`FlyingRanged` map directly; a **static turret that hovers** is `Static` +
  `staticAttack = Ranged` with **`tuning.gravityScale = 0`** (not a `Flying*` archetype — those
  *fly while chasing*), and its sheet recipe needs `anchor = AnchorMode.Center`.
- **Hand-tuned values win.** The recipe *seeds* `EnemyStats` the first time (or when the component
  is missing, which is how an old-system prefab gets upgraded); after that, regenerating the art
  leaves the numbers alone. `Pipeline ▸ 3b · Generar enemigo RESETEANDO valores` is the explicit
  way back to the recipe's values.
- **Ranged enemies** — an archetype that shoots (or `Static` + `staticAttack = Ranged`) plus a row
  that exported a prop sprite (see above). `EnemyFactory` builds a pooled projectile prefab from it
  via **`ProjectilePrefabFactory`** (no `FxPlaceholderStyle`: the sprite is already real art, lifted
  off the character's own sheet) at `Assets/Prefab/Fx/Enemies/<Name>/`, and drops it into
  `tuning.projectile.prefab`. Firing goes through `ProjectileFactory`, so it is pooled like every
  other projectile in the game. **The projectile's speed/lifetime/damage/homing knobs are on
  `EnemyStats ▸ Tuning ▸ projectile`, NOT on the `Fx_<Name>_Projectile` prefab** —
  `ProjectileFactory.Spawn` calls `Projectile.Configure` with the spec on every shot, so the
  prefab's serialized fields are overwritten each time (only its `SpriteRenderer` scale, i.e.
  visual size, survives). `EnemyFactory.MirrorSpecOntoPrefab` copies the spec onto the prefab at
  generation so it reads true, but it's still not the edit point.
- **`SheetRow.releaseFrame`** is what ties the throw to the drawing: the frame the generator plants
  the `OnAttackRelease` event on. `-1` = no event, fall back to `EnemyStats.attackReleaseFallback`.
- **`PrefabDresser`** — replaces a placeholder's visuals on an existing prefab (sprite + controller
  or flipbook, colour back to white) and **touches nothing else** — colliders, scripts, rigidbody
  and references stay as they were.
- **`ContentAudit`** (`Pipeline ▸ 4/5`) — scans every prefab with a `Health` for missing
  `Knockback`/`HitFlash`/`CurrencyDropper` and optionally adds them. That failure is silent by
  design (`Health` works fine without them; the enemy just never flinches), which is why it needs a
  scanner rather than a convention. It also completes the **terrain mask** (`obstacleLayers` on
  `EnemyStats`/`EnemyRecipe`, `groundLayers` on the legacy `EnemyController`) with any missing
  `Ground`/`Platform` layer — same class of silent failure: the enemy just stops at the foot of a
  bridge. It only adds layers, never removes what was configured.
- **Per-character packs** (`Assets/Scripts/Pipeline/Editor/<Name>Pack.cs`) — same shape as the boss
  packs: pure data that writes the two recipes and calls `SpritePipeline.RunEnemy`. **This is how a
  new character ships** — copy the file, change the data. Shipped: `TreeWalkPack` (static ranged),
  `OgroPack` (ranged mover), `AbejaPack` (hovering static ranged), `ChampiPack` (static ranged),
  `GorilaPack` (melee mover, `evenSplit` attack), `CaballoPack` (melee mover, fast charger),
  `LoboPack` (ranged mover, kites), `DragonPack` (flying ranged) and `MurcielagoPack` (sleeping
  flying kamikaze). Reproducible from git,
  runnable headless via
  `unity command run_script --file <pack> --entry <Namespace.Type.Run>`.
- One folder per character (`Assets/Art/Characters/<Name>/`), same rule as the per-boss FX folders.
