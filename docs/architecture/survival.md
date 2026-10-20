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
  - **`waveMode`**: *Simple* (a fixed list per wave: `flying/ground/turretCount` = at wave 1 / + per wave / cap; spawned one
    every `spawnInterval` while alive < `maxAlive`; cleared when nothing alive or pending) or *Dynamic* (fill up to
    `dynamicMaxAlive` staggered by `spawnInterval`, then each death asks for another `respawnDelay` s later if there is room;
    the three counts become the kind mix; ends by **time** (`waveSeconds`, `onTimeUp`: stop spawning and finish the rest,
    or they vanish without loot) or by **kills** (`waveKills`; never spawns more than the kills still needed));
  - where they appear: only points within `spawnRadius` of the player and not closer than `minSpawnDistance` (if none is in
    the ring but some are in the radius, those; if none in the radius, the nearest; bosses ignore it);
  - per-wave **scaling** of health and damage (linear or compound), **difficulty** (start + per wave) and `specialChance`,
    **rarity weights** (1 / 0.7 / 0.4 / 0.15 → 2 commons + 1 rare of 30 ≈ 12.5 / 12.5 / 5);
  - **difficulty modes** `easy` / `normal` / `hard` (`SurvivalDifficultySettings`): × enemies at once (dynamic) / per wave
    (simple), × kills to reach, × health, × damage — more enemies = more to farm, more health/damage = the cost;
  - rewards between waves (flags: shop / item drop / both / nothing; a stage can override), heal between waves, auto-next
    timer (0 = wait for the player), gold drop × (≥ 1, via `CurrencyDropModifiers`), gold per cleared wave.
- **`SurvivalWaveBuilder`** — pure logic. `Build` = a simple wave's list (count per kind × stage × difficulty, weighted pick,
  special only if `variant.difficulty ≤ wave difficulty`, then `specialChance`, shuffled; boss wave = one random boss).
  `PickOne` = the next enemy of a dynamic wave (kind weighted by this wave's three counts, then the same pick).
- **`SurvivalSpawnPoint`** — one kind each: Flying / Ground (the enemy drops from it) / Turret / Boss (none → a Ground one).
  Children of the manager; the manager collects them in `Start` (no static registry, nothing to keep in sync: duplicate one
  and it counts). Gizmo coloured by kind with a label.
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
  - **Inspector** (`SurvivalManagerEditor`): buttons **+ Volador / + Suelo / + Torreta / + Jefe** create a point under
    `Puntos` at the scene view centre and select it; below, every point by kind (click = select), with a note when a kind
    has none.
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
Drag `Survival.prefab` into the scene, move its points (and the shop / drop point), make sure the scene is in Build Settings
and point `MainMenuController ▸ survivalScene` at it. The config is shared.
