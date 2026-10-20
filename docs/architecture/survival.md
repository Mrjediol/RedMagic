# Survival mode (`Assets/Scripts/Survival/`, content in `Assets/Test/Survival/`)

One map, waves of enemies that keep scaling, rewards between waves, until the player dies. A mode of its own (main menu ▸
Jugar ▸ Supervivencia) and the balance bench for the classic run: each wave stands for a power level of a run.

## Pieces
- **`SurvivalConfig`** (ScriptableObject, `Assets/Test/Survival/SurvivalConfig.asset`) — **every number**, so any map uses
  the same asset: start (gold, two weapons, items, DEV-menu weapons), **stages** in order (`SurvivalStage` = one world:
  normal wave count, `worldNumber` for shop rarities/prices, optional `zoneLook`, health/damage/count ×, three enemy lists
  — flying / ground / turret — and an optional boss wave at the end), enemies per wave per kind (`SurvivalCount`: at wave 1,
  + per wave, cap), spawn pacing (first delay, interval, max alive, `preferSpawnsWithin`), per-wave **scaling** of health and
  damage (linear or compound), **difficulty** (start + per wave) and `specialChance`, **rarity weights**, rewards between
  waves (flags: shop / item drop / both / nothing; a stage can override), heal between waves, auto-next timer (0 = wait for the
  player), gold drop × (≥ 1, via `CurrencyDropModifiers`) and gold per cleared wave. `repeatLastStage`: after the last stage,
  keep going with its enemies (scaling keeps rising); off = victory screen.
- **`SurvivalEnemyEntry`** — prefab + `SurvivalRarity` (weights in the config: 1 / 0.7 / 0.4 / 0.15 → 2 commons + 1 rare of
  30 enemies ≈ 12.5 / 12.5 / 5) + its `specials` (`EnemyVariant`s).
- **`SurvivalWaveBuilder`** — pure logic: the wave's list (count per kind × stage multiplier, weighted pick, special only if
  `variant.difficulty ≤ wave difficulty`, then `specialChance`), shuffled. Boss wave = one random boss of the stage.
- **`SurvivalSpawnPoint`** — placed by hand, one kind each: Flying / Ground (the enemy drops from it) / Turret / Boss (none →
  a Ground one). Found through a static registry, never wired. Gizmo coloured by kind with a label.
- **`SurvivalManager`** (in the scene, from `Assets/Test/Survival/Survival.prefab`) — config, player prefab (used only if the
  scene has no `Player`-tagged object; `RunManager.Player` first), player spawn (empty = `SectionEntry`), the between-waves
  **shop** (a `ShopManager` with altar points + reroll, child of the prefab; deactivated during waves, `Stock(world, seed)` on
  each visit), item drop point, main-menu scene. Testing knobs: `startAtWave`, `seed`. Loop: build → spawn one every
  `spawnInterval` while alive < `maxAlive` → cleared when nothing alive or pending → rewards → wait for **Siguiente ronda**.
  Each spawned enemy: zone look (unless the special recolours) → wave × stage scaling → special. Logs one line per wave
  (time, damage taken, gold). On start it resets gold/rerolls/inventory and sets the start weapons; on destroy it empties
  them (nothing travels to the classic mode). It disables the map's `WaveManager` and `SectionExit` at runtime (levels from
  EditorNiveles bring them). Enemies are `Instantiate`d like `WaveManager` does (the enemy stack destroys itself on death;
  pooling enemies needs a reset path first).
- **`SurvivalHud`** — code-built UI Toolkit on `CurrencyHudPanelSettings` (sortingOrder 5): wave + world at the top, enemies
  left / "¡Jefe!", the between-waves block (button, Enter / pad Select, or countdown) and the game-over / victory panel
  (Reintentar = reload the map, Menú principal).

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
`MainMenu.uxml` has `classicButton` / `survivalButton` / `backButton` (hidden); Jugar swaps the buttons for the mode choice
(Escape / Atrás go back). `MainMenuController ▸ survivalScene` = `SurvivalArena` (in Build Settings).

## Moving it to another map
Drag `Survival.prefab` into the scene, move its points (and the shop / drop point), make sure the scene is in Build Settings
and point `MainMenuController ▸ survivalScene` at it. The config is shared.
