# Survival mode (`Assets/Scripts/Survival/`, content in `Assets/Test/Survival/`)

One map, waves of enemies that keep scaling, rewards between waves, until the player dies. A mode of its own (main menu ▸
Jugar ▸ Supervivencia ▸ Fácil / Normal / Difícil) and the balance bench for the classic run: each wave stands for a power
level of a run.

## Pieces
- **`SurvivalConfig`** (ScriptableObject, `Assets/Test/Survival/SurvivalConfig.asset`) — **every number**, so any map uses
  the same asset:
  - start (gold, two weapons, items, DEV-menu weapons);
  - **stages** in order (`SurvivalStage` = one world: normal wave count, `worldNumber` for shop rarities/prices, optional
    `zoneLook`, health/damage/count ×, three enemy lists — flying / ground / turret —, per-wave `waveSeconds` / `waveKills`
    for dynamic waves (list shorter than the waves → its last value repeats; empty → the config default) and an optional
    boss wave at the end); `repeatLastStage` (after the last stage keep going with it; off = victory screen);
  - **kind mix** for both wave modes: `flyingPercent` / `groundPercent` / `turretPercent` (80 / 15 / 5; normalised between
    them, a kind with no enemies in the stage gives its share to the others);
  - **`waveMode`**: *Simple* (a fixed list per wave: `enemiesPerWave` = at wave 1 / + per wave / cap, split by the mix with
    largest remainders — 26 → 21 / 4 / 1; spawned one every `spawnInterval` while alive < `maxAlive`; cleared when nothing
    alive or pending) or *Dynamic* (fill up to `dynamicMaxAlive` staggered by `spawnInterval`, then each death asks for
    another `respawnDelay` s later if there is room; each one's kind is rolled with the mix; ends by **time** (`waveSeconds`, `onTimeUp`: stop spawning and finish the rest,
    or they vanish without loot) or by **kills** (`waveKills`; never spawns more than the kills still needed));
  - where they appear (rules for `SurvivalSpawnFinder`, no hand-placed points): `minDistance` / `maxDistance` from the
    player, `maxAbove` / `maxBelow`, `minSeparation` from any enemy, `flyerClearance`, `groundSpawnHeight`, `solidLayers`
    (Ground) / `platformLayers` (Platform), `maxAttempts`;
  - per-wave **scaling** of health and damage (linear or compound), **difficulty** (start + per wave) and `specialChance`,
    **rarity weights** (1 / 0.7 / 0.4 / 0.15 → 2 commons + 1 rare of 30 ≈ 12.5 / 12.5 / 5);
  - **difficulty modes** `easy` / `normal` / `hard` (`SurvivalDifficultySettings`): × enemies at once (dynamic) / per wave
    (simple), × kills to reach, × health, × damage — more enemies = more to farm, more health/damage = the cost;
  - rewards between waves (flags: shop / item drop / both / nothing; a stage can override), heal between waves, auto-next
    timer (0 = wait for the player), gold drop × (≥ 1, via `CurrencyDropModifiers`), gold per cleared wave.
- **`SurvivalWaveBuilder`** — pure logic. `Build` = a simple wave's list (total × stage × difficulty split by the kind mix,
  weighted pick by rarity, special only if `variant.difficulty ≤ wave difficulty`, then `specialChance`, shuffled; boss wave
  = one random boss). `PickOne` = the next enemy of a dynamic wave (kind rolled with the mix, then the same pick).
- **`SurvivalSpawnFinder`** — where each enemy appears, by rules (replaced hand-placed spawn points on 2026-10-20: with
  ~20 points only 2–4 fell inside the radius and enemies piled up on them). Random samples in the box around the player,
  kept if inside the ring (min–max distance, above/below limits), not in rock and `minSeparation` away from every alive or
  just-spawned enemy. *Flying*: free air with `flyerClearance`. *Ground* / *Turret*: ray down (solid + platform layers) to
  a surface with an up normal, spawn `groundSpawnHeight` above it (they fall). *Boss*: level centre, on the first floor from
  the bottom. "In rock" = `Level.IsRock` on the level grid (the painted terrain only has edge colliders, which detect
  nothing inside); without a `Level`, `OverlapPoint` on `solidLayers`. No spot → halve the separation, then drop it and halve
  the min distance; still none → false (dynamic waves retry after `respawnDelay`, simple waves requeue it up to 20 times).
  Wall / ceiling turrets: separate task.
- **`SurvivalManager`** (in the scene, from `Assets/Test/Survival/Survival.prefab`) — config, player prefab (used only if the
  scene has no `Player`-tagged object; `RunManager.Player` first), player spawn (empty = `SectionEntry`), the between-waves
  **shop** (a `ShopManager` with altar points + reroll, child of the prefab; deactivated during waves, `Stock(world, seed)` on
  each visit), item drop point, main-menu scene. Testing knobs: `startAtWave`, `testDifficulty` (used when the scene is
  opened directly; the menu's choice travels in `SurvivalSession.Difficulty`), `seed`. The boss wave is always simple (ends
  when the boss dies). Each spawned enemy: zone look (unless the special recolours) → wave × stage × difficulty scaling →
  special. Logs one line per wave (time, kills, damage taken, gold). On start it resets gold/rerolls/inventory and sets the
  start weapons; on destroy it empties them (nothing travels to the classic mode). It disables the map's `WaveManager` and
  `SectionExit` at runtime (levels from EditorNiveles bring them). Enemies are `Instantiate`d like `WaveManager` does (the
  enemy stack destroys itself on death; pooling enemies needs a reset path first).
  - **Gizmos** (object selected): the spawn ring (red = min, green = max) and the above/below lines around the player (or
    where they start, outside Play); in Play, the spots used in the last 2.5 s coloured by kind.
- **`SurvivalHud`** — code-built UI Toolkit on `CurrencyHudPanelSettings` (sortingOrder 5): wave · world · difficulty at the
  top; under it "Tiempo: 0:45" / "Bajas: 12/30" / "Enemigos: N" / "¡Jefe!"; the between-waves block (button, Enter / pad
  Select, or countdown) and the game-over / victory panel (Reintentar = reload the map, same difficulty; Menú principal).

## Shared pieces it added (usable anywhere)
- **`EnemyVariant`** (`Assets/Scripts/Enemies/`, assets in `Assets/Test/Survival/Variants/`) — a version of any enemy
  without a prefab: difficulty, health / damage / size ×, recolour through `RedMagic/Sprite Lit FX` (hue — the shader
  *subtracts* it: from ice blue −0.18 purple, +0.27 green, +0.47 gold, −0.4 red, −0.43 orange —, saturation, brightness,
  rim, glow, sheen) and an optional `attachPrefab` (the `Shield` behaviour). `Apply` / `ApplyLook` / `ApplyStats`. Runtime
  materials are cached per (source material, variant, **texture**): sharing one FX material between parts of different
  textures drew them with another part's texture.
- **`IOutgoingDamageScale`** (`Combat/`) — runtime × on all damage an enemy (`EnemyStats`) or boss (`BossController`)
  deals: melee / explosion / deck attacks via `AbilityContext.DamageScale`, `EnemyAttack` projectiles, contact damage.
  Not serialized; projectile `impactDamage` (splash) is not scaled yet.

## Main menu
`MainMenu.uxml` has hidden `classicButton` / `survivalButton` and `easyButton` / `normalButton` / `hardButton`, plus
`backButton`. Jugar → mode choice → (Supervivencia) difficulty choice; Atrás / Escape go one step back.
`MainMenuController ▸ survivalScene` = `SurvivalArena` (in Build Settings).

## Moving it to another map
Drag `Survival.prefab` into the scene, place the shop / drop point (no spawn points to place), make sure the scene is in Build Settings
and point `MainMenuController ▸ survivalScene` at it. The config is shared.
