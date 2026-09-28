# Combat (`Assets/Scripts/Combat/`)
- **`Teams`** — the one rule about who can hurt whom: **only the player damages enemies and only
  enemies damage the player.** A side is *derived*, never configured: carrying the `Player` tag is
  team Player, everything else with a `Health` is team Enemy (bosses, adds, anchors, the training
  dummy). `Teams.Allied(attacker, target)` is checked in the four places damage is filtered —
  `AbilityHit.IsValidTarget`, `Projectile`, `ShotProjectile`, `ShotBeam` — so weapons, boss decks
  and enemy attacks all obey it without any prefab needing to be set up. It exists because the
  older mechanism, the attacker's "friendly tag", only worked for bosses (which do tag their adds
  `Enemy`): pipeline enemies are born `Untagged`, so their friendly tag was empty, the filter
  filtered nothing, and one enemy's bullet crossing another killed it. The friendly tag still
  applies — this is an extra rule on top, not a replacement.
- **`Health`** — the only place damage is applied. Exposes instance `Damaged`/`HealthChanged`/`Died`
  events plus a **static `AnyDamaged(Health, float)`** — the global feed `DamagePopups` subscribes to
  once instead of hooking every character. `TakeDamage(amount)` **returns false when the
  hit didn't land** (dead, invulnerable, zero damage); attackers must check it before playing hit
  effects. I-frames live here (`invulnerabilityDuration`); `InvulnerabilityChanged` fires on
  entering/leaving them. **Only the player has them (0.8s); enemies are set to 0** — i-frames are
  there to stop two enemies plus a projectile counting as three hits on the player, and on an enemy
  they instead swallow every multi-hit ability (a 5-pellet shotgun landed one pellet). Enemy
  hit-stun is knockback's job, not invulnerability's.
- **`Knockback`** — sits next to `Health` on anything that should be pushed. **The victim owns the
  force** (`horizontalForce`/`verticalForce`/`duration`/`resistance` per prefab); the attacker only
  passes a multiplier, so a heavy enemy or a boss is tuned in one place instead of in every attack.
  `TakeDamage(amount, sourcePosition, multiplier)` is the overload that damages *and* pushes — it
  only pushes if the hit actually landed, so an i-framed hit doesn't punt. Applying the push takes
  two paths: `IKnockbackReceiver` for the player (its controller integrates its own velocity, so a
  force would do nothing) and `Rigidbody2D.linearVelocity` for dynamic bodies (enemies).
  `EnemyController` yields control while `Knockback.IsActive`; `PlayerMovement` ignores input for
  the same window (otherwise holding the opposite direction cancels the hit on the same frame).
  Melee (`PlayerAttack`) pushes along the attacker's facing, not away from its position, so an
  enemy standing on top of the player still gets sent the way the swing points.
- **`HitFlash`** — tints the sprite on damage and blinks it during i-frames. Without it i-frames
  read as the game not registering hits. Add it wherever `Health` is.
- **Projectile collision rule** — `ShotProjectile` (and the old `Projectile`) stop for exactly two
  things: a collider with a `Health` in its parents (an enemy / the dummy — friendly-tag and
  already-hit filtered), or a collider on the **`Ground` layer (6)** — the terrain painted with
  the Tile Painter, matching `1 << 6` used by `ShotBeam` / `PlayerMovement`. Everything else —
  decoration, trigger zones (cauldron, tomb, shop), other props — is passed straight through.
- **`GroundSnap`** (`Assets/Scripts/Gameplay/`, on `WeaponUpgrade.prefab`) — on
  `Start` (and via context-menu) raycasts down to the `Ground` layer and drops the object so the
  base of its sprite bounds rests on the terrain. Needed because `RunManager` spawns
  the boss reward at a position that isn't ground-aligned. Reusable on any
  spawned prop.
- **Every `TilemapCollider2D` MUST be merged into a `CompositeCollider2D`** (+ a `Rigidbody2D` set
  to **Static**, which the composite requires). A bare `TilemapCollider2D` emits **one box per
  tile**, and two adjacent tiles share a vertical face that the physics engine treats as a real
  wall: anything walking along the top **snags on the seam and stops dead on ground that looks
  perfectly flat**. This cost a long debugging session — the symptom reads as broken AI ("the enemy
  follows me and then randomly stops"), so it gets chased in `EnemyBrain` where there is nothing to
  find. The proof is in the contact list, not the code:
  `Rigidbody2D.GetContacts` returned `[Tilemap] normal=(1.00, 0.00) point=(9.01, -3.00)` on flat
  terrain. After merging, MainHub went from hundreds of boxes to 5 outlines and the same enemy went
  from walking 2.9 units to 11.5 without stopping. It hits the player too (stutter while running).
  **Tools > RedMagic > Pipeline > `6 · Auditar colliders de tilemap` / `7 · Auditar y reparar`**
  (`TilemapColliderAudit`) scans every scene in `Assets/Scenes` and fixes this; `RunSingle(path,
  repair)` is the per-scene entry point for the CLI, because opening all nine scenes at once blows
  the 5s `unity command eval` timeout. It also forces a Static body — five world scenes had the
  ground tilemap on a **Dynamic** `Rigidbody2D`. Run it after painting terrain in a new scene.
- **`EnvironmentDecorColliders`** (`Assets/Scripts/Gameplay/`, on MainHub's `Enviroment`) — on
  `Awake` (and via its inspector context-menu) disables the `Collider2D` of every child that is
  pure decoration, so imported props (barrels, fences…) with baked-in colliders don't block
  movement or the physics broadphase. **Skips** any child with a `Health`, any `RedMagic.*` script
  on itself or a parent up to the container, any trigger collider, or anything in its `keep` list —
  so the same container can still hold the dummy, cauldron, tomb, chest, etc.
- **`Corpse`** — on death, tints the body, holds it ~0.6s so the kill reads, fades it over ~0.35s
  and destroys the GameObject. Colliders/physics are still switched off by whoever drives the
  character (`EnemyController.OnDied`); this only owns the look and the cleanup, so an enemy with
  different AI still tidies itself up. Corpses that stay forever litter the scene and hide things —
  they were covering the reward the boss drops.
- Both are already on `Player.prefab` and every `Enemy_*.prefab`. A damageable prefab built any way
  other than through `EnemyFactory` needs `Knockback` + `HitFlash` added by hand — `Health` works
  without them, just silently unpushed and unflashing. `EnemyFactory` stamps them, and
  `ContentAudit` (`Tools ▸ RedMagic ▸ Pipeline ▸ 4/5`) finds and fixes the ones that predate it.
- **`DamagePopups`** (`Assets/Scripts/Fx/`) — self-bootstrapping `DontDestroyOnLoad` singleton.
  Subscribes once to `Health.AnyDamaged` and spawns a floating number (code-built world-space
  `Canvas` + `Text` with the built-in `LegacyRuntime.ttf`, no font asset) over the victim: warm and
  size-scaled for damage dealt to enemies / the dummy, red and `-`-prefixed for damage the player
  takes (distinguished by the `Player` tag). `DamagePopups.Show(pos, amount, kind)` for manual use.
- **`TrainingDummy`** (`Assets/Scripts/Combat/`, on `Assets/Prefab/Eviroment/Target.prefab`) —
  `[RequireComponent(Health)]`. The prefab's `Health` has `maxHealth 1000000` and
  `invulnerabilityDuration 0`; the component calls `Health.ResetHealth()` on every hit, so it never
  dies and every pellet / beam tick / burst shot registers. Draws an IMGUI readout above itself
  (`FpsOverlay`-style) — last hit, DPS, hit count, average, max — for the current burst, which
  auto-resets after `idleResetSeconds` (2.5s) idle or on **R**.
- **`EnemyController.canFly`** — per-prefab bool. Off (default) is the ground enemy as before:
  gravity, ledge probing, and it ignores targets outside `verticalTolerance`. On, the enemy's
  `gravityScale` is zeroed in `Awake`, the ledge probe and the vertical-tolerance gate are skipped,
  detection uses true distance, and chasing flies straight at the target on both axes while holding
  its altitude. Set it on the floating enemies; leave it off for anything that should walk.
