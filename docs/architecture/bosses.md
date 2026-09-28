# Bosses (`Assets/Scripts/Bosses/`)
Same content-by-convention shape as the weapons/items system, one level up: a boss is a
**`BossDefinition` asset** plus a prefab, and each of its attacks is its own **`BossAttack`
ScriptableObject** in `Assets/Resources/Bosses/`. Writing C# is only needed for a new *archetype*
(a new shape of attack), never for a new boss, a new attack asset or a phase-2 variant.
Each boss has a generator under **Tools > RedMagic > Boss**, one pack per boss:
`BossStarterPack` (Árbol Ancestral), `ScarecrowBossPack` (Espantapájaros), `StoneGuardianPack`
(Guardiana de Piedra) — these three also have a *Colocar … en WorldN_Boss* item — plus
`CursedWellPack` (Pozo Maldito), `BeetleQueenPack` (Reina Escarabajo) and `UnholySealPack` (Sello
Profano), which only build the prefab because there is no fourth world yet. Each builds its attack
assets + definition + prefab.

**Gotcha when writing a pack**: a `BossPhase` created through `SerializedProperty.arraySize` is
**zero-initialised** — Unity does not run the C# field initialisers for array elements, so any
field the generator does not write is `0`, not the default you see in the class. That is why
`BossAuthoring.WritePhase` writes every field including the ones that "already default to 1";
leaving `damageTakenMultiplier` unwritten once shipped three bosses that could not be damaged at
all. `BossDefinition.OnValidate` now repairs a zero there and logs a warning. All of it is idempotent — nothing
overwrites an existing asset, so a generator can be re-run after hand-tuning numbers and only the
missing pieces appear. The shared plumbing (creating attack assets, writing private serialized
fields, registering tags, planting the prefab on the scene's ground) lives once in
**`BossAuthoring`**; a boss pack should be almost pure data.

- **`BossController`** (on the boss prefab, replaces `EnemyController` — a boss never patrols) —
  waits for the player to enter `activationRadius`, plays an intro (invulnerable + camera shake +
  health bar + optional `musicId`), then loops **telegraph → attack → recovery → pause**. That
  cycle is the whole readability of the fight: nothing damages during the telegraph, and the
  recovery is the player's DPS window, so the nastier the attack the longer its recovery. Measures
  the arena's ground with a raycast at `Start` and **snaps itself onto it** (its prefab origin is
  the sprite's base), so placing a boss is dropping it roughly in the scene.
- **Phases** — `BossPhase` entries inside the definition, entered when normalized health drops
  below `startsAtHealth` (never backwards, so lifesteal can't rewind a phase). Entering one cuts
  the attack coroutine, goes invulnerable for `transitionSeconds` (otherwise a well-timed burst
  skips phase 2 entirely), and swaps the attack deck. `speedScale` shortens every timing in the
  phase and `frenzyBelowHealth` shortens them again at low health — so phase 2 is the same assets,
  faster, not a duplicated set.
- **Armour and punish windows** — `BossPhase.damageTakenMultiplier` sets how much damage the boss
  takes during that phase, and any attack can declare `vulnerableSeconds` / `vulnerableMultiplier`
  to leave the boss **exposed during its recovery** (`BossController.EnterVulnerable`). Together
  they change what the fight asks: an armoured boss can't be beaten by dodging patiently, only by
  getting close enough to punish the attack that just tried to kill you. Both ride on
  **`Health.DamageMultiplier`** — damage enters through one place, so no weapon, ability or boss
  attack needs to know armour exists, and the floating number the player sees is already the real
  one. The window is signposted three ways: the boss's aura turns gold (`vulnerableAura`), the
  boss health bar's fill turns gold and its subtitle reads "¡EXPUESTO!".
- **`BossContext`** — the per-cast bundle (boss, player, ground Y, arena size, phase pacing) that
  carries an **`AbilityContext`** inside it, so boss attacks reuse `AbilityHit` for target
  filtering/damage and `ProjectileFactory` for **pooled** projectiles instead of a parallel system.
- **Archetypes** — each one exists because it asks the player a *different question*; that is the
  bar a new archetype has to clear, otherwise it belongs as another asset of an existing one.
  - `ShockwaveAttack` (`BossShockwave`, pooled) — **"at what height am I?"**. A screen-wide wave
    inside a **height band**: band `0→1.9` must be jumped, band `1.9→9` must be ducked by staying
    grounded, and `alternateBands` makes one attack ask for jump→land→jump. Having both bands is
    what forces reading the telegraph instead of jumping on reflex.
  - `SweepBeamAttack` (`BossSweepBeam`, pooled) — **"at what distance am I?"**. A long arm pivoting
    at the boss's shoulder that rotates from `fromAngle` to `toAngle`: because it pivots, its height
    at your position depends on how far away you stand — point-blank it drops instantly, mid-arena
    it arrives late but fast. `bothSides` removes the "just stand behind it" answer, `returnSweep`
    makes pass 2 come back through where pass 1 just cleared. Hits **once per pass**, like the wave.
  - `SafeZoneAttack` — **"where on the floor am I?"**. Marks N refuges, then damages the whole arena
    *except* inside them, so the player has to leave the boss and run. `guaranteeReachable` always
    puts one refuge near the player (without it, a bad roll makes the hit unavoidable in a wide
    arena); refuges are drawn in their own `safeColor`, never the phase accent, because what saves
    you cannot look like what hurts you. `pulses` + `moveSpotsEachPulse` turn it into a relay.
  - `BulletHellAttack` — `Radial` (ring; with `spinPerVolley` a spiral whose gap you must track),
    `Fan` (aimed cone) and `Rain` (ground markers first, then drops). Its `projectileSprite`
    overrides the boss's FX sprite per attack, so one boss can throw pumpkins and another burrs
    while its warnings stay plain readable rectangles. Projectiles do **not** rotate to face their
    direction, so only round-ish sprites work there.
  - `GroundSlamAttack` — **"am I still standing where I was?"**. Marks the spot the player is on,
    then crushes it after `windup`; the mark is fixed when it appears and never chases, so it is
    always dodgeable but always costs you your comfortable spot. It optionally leaves rubble in the
    crater, and it is the natural carrier of a punish window.
  - `HazardFieldAttack` (`BossHazard`, pooled) — **"where will I be able to stand later?"**. Seeds
    the floor with patches that *stay* and tick for damage. Deliberately low damage and long life:
    its job is to shrink the arena, which quietly tightens every other pattern in the deck without
    changing any of them. `avoidPlayerRadius` keeps a patch from spawning under your feet.
  - `GuardStanceAttack` — **"is it safe to attack right now?"**. The boss covers up: it takes
    almost no damage and **reflects a flat amount per hit** (`BossController.EnterGuard`), so
    mashing through it costs you half a health bar. Its armour is never 0 — the hit has to land to
    be reflected, and seeing a laughable number pop off the boss is half the lesson. Holding fire
    is rewarded: it can open a punish window as it drops the guard.
  - `PlatformFloodAttack` (`BossPlatform`, pooled) — **"can I get off the ground?"**. Grows ledges
    that spawn on the **`Ground` layer**, so `PlayerMovement` stands on them with no special
    casing (and shots collide with them, which is a fair trade), then floods the whole arena floor
    for a few seconds. Platforms outlive the flood and blink before vanishing. Both halves live in
    one attack on purpose: the deck is shuffled, so "ledges first, flood second" is only
    guaranteed if they are the same card.
  - `AnchorRitualAttack` (`BossAnchor`, pooled) — **"what do I shoot first?"**. The boss shuts
    down to ~5% damage taken and plants destructible anchors on a timer; break them all and it is
    left exposed, run out of time and it discharges across the whole arena. Anchors are plain
    `Health` objects tagged like the boss, so every weapon in the game breaks them and none of the
    boss's own attacks do.
  - `OrbRingAttack` (`BossOrb`, pooled) — **"where do I stand before it's loaded?"**. The boss
    doesn't shoot, it *builds* the shot: orbs appear one at a time in a crown around it, tiny, and
    grow while the ring turns. Nothing damages during that — the pattern is drawn in the air
    before it exists, so it says exactly how many projectiles are coming and how wide the gaps
    are. At full size they all launch outward at once. Unlike a plain radial volley, which you read
    while it's already reaching you, the whole attack is the wait: stand in a gap, or spend it
    hitting the boss (hence a generous `vulnerableSeconds`). Charge orbs are pure visuals — no
    collider, no damage — and each is swapped for a real `ProjectileFactory` projectile at its own
    position on release, so nothing jumps. They also self-expire, so a coroutine cut by a phase
    change can't leave a crown floating.
  - `SummonAddsAttack` — adds, capped by `maxAlive`, killed when the boss dies.
  - `QuakeSlamAttack` — **"are my feet on the floor when it lands?"**. No radius: at the gesture's
    release frame it shakes the camera (base `shakeAmplitude`/`shakeDuration`), plays its
    `impactFxPrefab` and damages the player only if `PlayerMovement.IsGrounded &&
    !IsOnPlatform` and the feet are within `floorTolerance` of the arena floor — jumping or
    standing on a Platform-layer ledge is the answer. Its timing knob is `impactDelay` (overrides
    `Telegraph`; `QuakeSlamAttackEditor` hides the base `telegraph`).
  - `PlatformDenialAttack` (`BossBolt` + `BossHazard`, pooled) — **"where can I stand for the
    rest of the fight?"**. Fires a `BossBolt` (straight line, no collider) at each Transform in
    the scene boss's **`BossArenaTargets`** (scene objects can't live on an SO), and on arrival
    leaves a `BossHazard` with `BossHazard.UntilBossDies` — permanent, ticking `damage` every
    `fireTickInterval`. Meant as a **`BossPhase.openingAttack`**: an attack the phase fires
    **once** after its transition, outside the shuffled deck. `BossController.OnDied` calls
    `BossHazard.ClearFrom(this)`, so every hazard a boss left (fire, rubble) dies with it.
- **Per-attack availability** — `BossAttack.minPhase` (1-based: below it the attack is never drawn,
  even if it sits in that phase's deck — the empty-deck fallback respects it too) and
  `cooldownSeconds` (real-time cooldown, on top of `cooldownInAttacks`), both checked in
  `BossController.IsReady`/`PickAttack`. Use them to move an attack between phases without
  duplicating assets or editing decks. `BulletHellAttack` Rain also has `rainCenterOffset`,
  `rainRandomX` and `rainDropStagger` (drops one by one; each ground marker lasts until its drop),
  and there is a **`Storm`** pattern: continuous for `stormSeconds` at `stormPerSecond` (neither
  scaled by phase pace), each drop at a random **landing** X with its own angle
  (`stormWind` ± `stormAngleRange`) and its origin shifted against the diagonal, so slanted drops
  still cover the whole span; `stormMarkLanding` marks each landing spot until it arrives.
- **Gestures — animated bosses (`BossAnimator`)** — a boss whose body is a sprite sheet gets an
  `Animator` **on the root** plus `BossAnimator`, and each `BossAttack` names its body animation
  in **`gesture`** (`Charge`, `Slam`, `Summon`…). `BossController.Telegraph` plays it and the
  attack's `Run` starts on the clip's **`OnAttackRelease` event** (the same event the pipeline
  plants for enemies — `AnimClipBuilder` now plants it on *any* row with `releaseFrame >= 0`, not
  just `Attack`). The clip's speed is set so that frame lands exactly at `telegraph / pace`, so
  `telegraph` stays the only timing knob and phase `speedScale` speeds up gestures for free;
  `gestureSpeedRange` clamps it (then the drawing wins and the attack waits). Gestures are loose
  controller states; `BossAnimator` returns them to `Idle`. `Hurt` = stagger on phase change
  (and `flinchOnHit`, idle only, with cooldown); `Death` via the `Dead` bool. No gesture / no
  `BossAnimator` = the old timed telegraph, so the six sprite bosses are unchanged.
  `BossPhase.transitionFx` (optional one-shot prefab) bursts at the boss's feet on entering a
  phase. Archetype knobs added for it: `OrbRingAttack.launchMode = AtPlayer` (+`launchStagger`,
  aimed at `BossContext.PlayerCenter`) and `GroundSlamAttack.impactFxPrefab` / `fitFxToRadius` /
  `fxLeadSeconds` (real-art crater FX via `VfxOneShot.SpawnFitWidth`; with `aimAtPlayer = false`
  the crater is marked for the whole telegraph).
- **Every boss is built on a different question on purpose** — that is the design rule for the next
  one, not just a description of these. Bosses 4-6 have no scene of their own yet: their generators
  only build the prefab in `Assets/Prefab/Enemies/`, to be dropped into whatever arena is being
  tested.
  - **Árbol Ancestral** (World 1) — *height*: shockwave bands plus spirals, with ground-bound
    sprouts as adds. Its bullets are the green orb (`Fx_ArbolAncestral_Bullet` wears `Orbe_Move`
    with a `SpriteFlipbook` and bursts into `Fx_Orbe_Impacto`), and it now also grows crowns of
    them: `BossAttack_CoronaDeOrbes` in phase 1, `BossAttack_CoronaMayor` (14 orbs, two crowns,
    twice the spin) in phase 2.
  - **Espantapájaros Marchito** (World 2) — *distance and place*: the pivoting scythe and the
    refuges, with **flying** crows (`Enemy_Cuervo.prefab`, an ordinary `EnemyController` with
    `canFly`) that cannot be out-run the way sprouts can. Cainos scarecrow at ×3.2, pumpkins and
    spike balls as projectiles.
  - **Guardiana de Piedra** (World 3) — *when do I attack?*: armoured to 30% damage taken, so the
    only real DPS comes from the windows her own `Puño Sepulcral` opens, and the arena keeps
    shrinking under the rubble of `Sepultura` / `Derrumbe`. Phase 2 cracks the armour to 60% but
    shortens the windows. Her health (2000) is deliberately lower than the other two because the
    armour already stretches the fight; raising it would make it long rather than hard. Cainos
    statue at ×3.7, rocks as projectiles.
  - **Pozo Maldito** (`CursedWellPack`) — *when NOT to attack*: it seals itself and reflects a flat
    hit back at whoever keeps swinging, then pays out a punish window for holding fire. Around that
    it splits your attention with crawlers out of the shaft and puddles that stay. Cainos well at
    ×3.2; its geyser is an ordinary bullet-hell fan with `arcGravity`, which reads as black water
    thrown up and falling back.
  - **Reina Escarabajo** (`BeetleQueenPack`) — *where is she now?*: the only **moving** boss. A
    `BossBurrowLocomotion` component (next to `BossController`, `[RequireComponent]`) patrols her
    across the arena on the floor/ceiling, then every few seconds she digs into the nearest surface,
    tunnels invisibly (dropping a low-band `BossAttack_Temblor` shockwave every ~1s that forces a
    jump — this asset is in **no phase deck**, only the locomotion fires it), and re-emerges from
    the opposite surface 1–5m to one side after a dust telegraph. Her normal deck (`Marea Ácida`
    flood + ledges, `Salivazo` fan, `Camada` brood, `Embestida` shockwave) only fires while she is
    surfaced — locomotion calls `BossController.SuspendAttacks`/`ResumeAttacks` around each burrow
    and `SetBodyVisible(false)` + `Health.Invulnerable` while underground. Her origin is the sprite
    **centre** (not the base like every other boss) so the rotate/flip/head-dive all pivot clean;
    Brackeys' 3-frame beetle at ×1.5, pre-rotated -90° (its art faces up), animated by
    `SpriteFlipbook`. `BeetleQueenPack` is **not** idempotent-only: it has a
    *RECREAR (borra y regenera)* menu item that deletes its owned assets and rebuilds them.
  - **Sello Profano** (`UnholySealPack`) — *what deserves your damage?*: the anchor ritual is its
    spine, and the rest of the deck is deliberately a greatest-hits of the earlier bosses (sweep,
    refuges, spiral, rubble) so a player who got this far recognises everything and only has to do
    it faster, with a countdown running. Brackeys' pentagram at ×2.5, hovering, with a circle
    collider so you can pass underneath.
  - **Ent Cristalino** (`TreeBossPack`, `Boss_TreeBoss`) — *where does the next one land?*: the
    first boss built from its **own sprite sheets** and the reference for animated bosses. Rooted,
    never moves; every attack is a body gesture (`TreeBoss.png` rows Idle / Charge / Slam /
    Summon / Hurt / Death) and its FX come from `BossAttack.png`, cut by two recipes on the same
    sheet (`TreeBossOrb` centred, `TreeBossRoot` bottom-anchored, split by `cropTop/cropBottom`).
    Deck: `Orbes de Savia` (OrbRing, `AtPlayer`, gesture Charge), `Estallido de Raíces`
    (**QuakeSlam**: earthquake shake + root wave, hurts only a grounded player, punish window,
    gesture Slam), `Espinas de Raíz` (GroundSlam aimed at the player, root-spike FX with
    `fxLeadSeconds`, gesture Summon; **`minPhase 2`** like `Bosque de Espinas`, so root spikes never
    fire in phase 1). Phase 2 at 50%: 2.5s invulnerable stagger + shake + root
    wave, then **`Fuego Verde`** once (`openingAttack`, PlatformDenial: 6 bolts to the
    `FireTargets` over the two side platforms, permanent green fire), then `speedScale 1.35`,
    shorter pauses and the bigger variants (`Tormenta de Savia`, `Bosque de Espinas`), plus
    **`Tormenta de Hojas`** (BulletHell Storm, gesture Charge, `minPhase 2`, `cooldownSeconds 8`: 5s
    of leaves at 7/s, random X, wind 8° ± 18°; the Rain version `Hojas Mágicas` — two waves of 10 —
    is kept as a reusable asset but is no longer in this deck; leaves pass through Platform-layer
    ledges and die on the Ground floor; `Fx_TreeBoss_Leaf` / `_LeafImpact` are placeholders — real
    art authored pointing +X since `Projectile` faces its travel direction). The fire's
    bolt/hazard are **placeholders** (`Fx_TreeBoss_FireBolt` / `_FireHazard`, tinted squares with
    `FxPlaceholderStyle`): real art = edit those prefabs (sprite + `SpriteFlipbook`, style
    `tint`/`resize` off), no code. *TreeBoss · Colocar en World1_Boss* places it; *TreeBoss ·
    Colocar blancos de fuego en World1_Boss* drops 3 targets over each outermost Platform-layer
    collider (only if the boss has none). Its sheets are painted on a flat
    green, which is why `SpriteSheetRecipe.softEdge` (glows fade instead of being scissor-cut) and
    `fillHoles` (bark shadows share the background's hue and got punched through) exist — see
    `SPRITE_PIPELINE.md` §13-14.
- **`BossHealthBar`** — screen-space uGUI built in code (no prefab/UXML/PanelSettings), sorting
  order 18 so menus still cover it. Shows the definition's name/title, an amber "ghost" trail
  behind the fill and a tick per phase threshold. A new boss needs no UI work.
- **Teams** — the boss and its adds share the **`Enemy` tag**, which is what stops the boss's own
  bullets from killing its summons (`AbilityContext.FriendlyTag` / `Projectile`'s owner tag). Tag
  any new boss-side spawn the same way.
- **`CameraFollow.ShakeAll(amplitude, duration)`** — camera shake lives inside `CameraFollow`
  (which already owns the camera position, so there's no LateUpdate ordering fight) and is
  reusable by anything, not just bosses.
