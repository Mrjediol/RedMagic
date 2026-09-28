# Economy (`Assets/Scripts/Economy/`)
- **`CurrencyManager`** — `DontDestroyOnLoad` singleton **hand-placed in MainMenu and MainHub**
  (like `RunManager`); a `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` fallback
  self-creates one only if a scene didn't provide it. Its four amount fields (`gold`, `diamond`,
  `soulFragment`, `skull`) mirror the live state and are **editable in the Inspector, including
  during Play** — type a value and `OnValidate` applies it immediately (persists the meta ones,
  fires `Changed`); any code change mirrors back so the Inspector always shows the truth. This is
  the test knob for currency. Context-menu "Borrar moneda de meta guardada" wipes the saved
  `PlayerPrefs`. `CurrencyManager.IsRunCurrency` is the single source of the run-vs-meta split:
  **Gold** and **Diamond** are run currency (spent in mid-run shops, zeroed on `RunManager.RunEnded`
  whether you died or won, not persisted); **SoulFragment** and **Skull** are meta currency (spent
  in the hub, saved to `PlayerPrefs`, survive death). Spending is `TrySpend` (used by `UpgradeManager`).
- **`CurrencyConfig`** — one ScriptableObject at `Assets/Resources/CurrencyConfig.asset`, loaded by
  `Resources.Load`. Holds per-currency HUD visuals (icon/label/tint) and the drop table: a
  min–max `DropRange` per currency for each `EnemyTier` (Basic / Elite / Boss). This asset **is**
  the "currency manager" for tuning drop amounts — edit it in the Inspector.
  Currency icons: source art in `Assets/Art/Currency/` (Coin/Diamond/Skull/SoulFragmen, big canvases),
  trimmed by alpha into `Assets/Art/UI/Currency/Currency_<Currency>.png` (bilinear, mipmaps, 256 max,
  1.6 u longest side) and assigned in `CurrencyConfig` by **Tools ▸ RedMagic ▸ UI ▸ Iconos de moneda ·
  Procesar e instalar** (`CurrencyIconsPack`). Everything (HUD, shop prices) reads icons from there.
- **`CurrencyDropper`** — `[RequireComponent(Health)]`; on `Health.Died` calls
  `CurrencyManager.GrantDrops(tier)`, which rolls each currency's range for that tier. On the enemy
  prefabs (`Enemy_*`, tier set per prefab) and on boss enemy instances (tier Boss). A Basic tier
  with diamond/skull ranges left at 0 simply drops none of those.
- **`CurrencyHud`** (`Assets/Ui/CurrencyHud.cs`) — self-bootstrapping persistent `UIDocument`, built
  in code, always visible (a row of icon+amount, top-right). Uses its own
  `Assets/Resources/CurrencyHudPanelSettings.asset` (sorting order 15, under the menus at 20).
  Note: headless `capture_game_view` / `screenshot` do **not** render UI Toolkit runtime panels, so
  the HUD (and touch controls) are invisible in those captures even when working — verify by
  querying the visual tree instead.

**Permanent upgrades** (the hub cauldron):

- **`UpgradeTree`** — ScriptableObject at `Assets/Resources/UpgradeTree.asset`. A `rows × columns`
  grid (currently 3×5) of `Node`s in row-major order. Each node has `maxLevel`, a cost that scales
  with level (`baseCost + costPerLevel * currentLevel`), and a placeholder `bonusPerLevel` /
  `statId`. Fill in real upgrades by editing the asset.
- **`UpgradeManager`** — self-bootstrapping `DontDestroyOnLoad` singleton. Levels are saved per node
  id in `PlayerPrefs` (meta-progression, survives death). "Skull: The Hero Slayer" unlock rule:
  within a row, column N is buyable only once column N-1 has level ≥ 1; rows are independent.
  `TryBuy(row, col)` pays `UpgradeManager.Cost` (SoulFragment) via `CurrencyManager.TrySpend`.
  `GetBonus(statId)` sums purchased bonuses — the hook for a future player-stat system; nothing
  reads it yet.
- **`UpgradeMenuController`** (`Assets/Ui/`) — self-bootstrapping persistent code-built `UIDocument`
  (`Assets/Resources/UpgradeMenuPanelSettings.asset`, sorting order 30). Hidden until
  `Open()`; pauses the game via `GameStateManager` while open. Close with Esc / E / gamepad B.
- The trigger for this menu is now `Hub.BookLootContainer` on the hub's book lectern (see Main Hub
  interactables); `CauldronInteractable`, the old single-press placeholder, is retired to Legacy.

**Shop (scene-based, between sections):**

- **Flow** — `RunPhase.Shop`. `RunManager.AdvanceSection` from a cleared normal section loads
  `ShopConfig.shopScene` (global, never in a world's section pool) before the next section; from
  the shop it continues as usual; bosses never lead to a shop. Enter at the scene's `SectionEntry`
  (left door), leave through its `SectionExit` (right door). After loading, `RunManager.StockShop`
  calls `ShopManager.Stock(CurrentWorldNumber, seed)`; a Shop scene opened on its own stocks itself
  as world 1.
- **`ItemLibrary.All`** (`Assets/Scripts/Items/`) is the global item pool (folder scan of
  `Resources/Items`), read by the shop and `ItemPickup` — a new item asset shows up in both with no
  wiring. The pool is currently the 6 ice-set items.
- **`ShopConfig`** (`Assets/Resources/ShopConfig.asset`) — every shop number: `shopScene`,
  `rarityWeightsPerWorld` (index 0 = world 1, last entry covers every later world), `basePrice`
  per rarity × `priceMultiplierPerWorld`. `RollStock(count, world, rng)` rolls rarity by weight,
  then an item of it, falling back to the nearest rarity that still has items; no duplicates.
- **`Economy.ShopManager`** (in `Shop.unity`) — `itemSpawnPoints` (one per altar), icon size /
  price offset / bob, proximity ranges, the Interact action. Adds a **`ShopAltar`** to each point
  (icon with a bob + world-space coin+price canvas, red when unaffordable, shake/flash on a denied
  buy). Nearest altar in range → **`UI.ShopItemPanel`** (UI Toolkit, own
  `ShopItemPanelSettings`, sorting 17): name in rarity colour, rarity, description, each tag
  "now → after" (gold + the `SynergyConfig` tier text when the buy crosses 2/4/6), and the hint.
  Interact (`Core.InteractInput`) → `CurrencyManager.TrySpend(Gold)` → `WeaponInventory.TryEquip`
  → altar cleared. A free-pool item with all 6 free slots full is refused before charging.
- **`Tools ▸ RedMagic ▸ Tienda ▸ Preparar escena Shop`** (`ShopSceneSetup`) — adds whatever is
  missing to `Shop.unity` (BG, floor, entry/exit, `ShopManager` with 5 points (one per altar) laid out on
  `Shop1.png`, `CameraFollow`) and assigns `ShopConfig.shopScene`. Idempotent; it respects the
  `BG.prefab` layout already in the scene.
- **Shop FX** — every number in `Resources/ShopFxConfig.asset` (`ShopFxConfig`: per-rarity aura
  table — halo alpha/pulse, particles/s, size, sparkle interval/count, purchase burst — plus focus
  boost, pop/flash/fly/ring timings, legendary shake/extra sparkles, HUD gold shake/tick). Colours
  come only from `Resources/ItemRarityColors.asset` via `ItemRarities.ColorOf` (also used by the UI).
  `ShopAltar` builds halo + aura/burst `ParticleSystem`s + flash overlay once per altar;
  `PlayPurchase` empties the altar immediately and runs pop → burst → arc to the player → ring,
  no pause; `CurrencyHud.PlaySpend` shakes and counts the gold down. Additive look =
  `Assets/Art/Fx/Shaders/SpriteAdditive.shader` (`RedMagic/Sprite Additive`) +
  `Assets/Art/Fx/Materials/Fx_SpriteAdditive.mat`. **Unity 6 gotcha**: a SpriteRenderer's colour
  reaches the shader as `unity_SpriteColor`, NOT the vertex colour — a custom sprite shader that
  only reads `COLOR` renders every tint as white; particles do use the vertex colour
  (`_UseSpriteColor = 0` on their material). `Tools ▸ RedMagic ▸ Tienda ▸ Generar FX de tienda`
  (`ShopFxSetup`) creates the material + both assets if missing.
- **Reroll** — `ShopManager.rerollPoint` (the scene's `Reroll` sprite) gets a `ShopRerollAltar` at
  runtime, same as spawn points get `ShopAltar`. Both implement `IShopInteractable`, so the reroll
  shares the altars' range/focus/prompt (`ShopItemPanel.ShowReroll`)/input and the single deny path
  (`ShopManager.Deny` → `ShopPriceTag.Deny`, the price-row shake both altars use). Spends
  `Run.RunRerolls` (start = `ShopConfig.startingRerolls` + Páginas del Eco, set in
  `LegendaryPassiveRunner`; a standalone Shop scene seeds it too) and re-rolls every altar through
  `ShopConfig.RollStock(..., exclude: bought this visit, avoid: on display)`. Staggered exit/enter
  (`ShopAltar.PlayExit/PlayEnter`) — timings, accent, altar flash and HUD punch in `ShopFxConfig ▸
  Reroll`. `CurrencyHud` shows the counter under the currencies only while `ShopManager.Current` exists.
- **`Core.InteractInput.Pressed(action)`** is the one "Interact pressed this frame" recipe (action,
  E/Enter, gamepad north, touch). Every interactable uses it.

**`MenuStyle`** (`Assets/Ui/MenuStyle.cs`) — shared palette, sizes and element factories for both
code-built menus. **Change the size constants here to rescale that UI**; both screens follow. Sizes
are in the panels' 1600×900 reference resolution, so they render ~1.2× larger at 1080p.
