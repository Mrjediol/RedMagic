# Main Hub interactables (`Assets/Scripts/Hub/`)
Five hand-built props in the hub, each its own MonoBehaviour rather than data-driven content —
there's no recurring "new prop" pipeline to build a tool for yet, unlike enemies/bosses/items. All
five share one sheet, `Assets/Sprites/Maibhubitems.png` (flat dark-teal background, no alpha, keyed
like `TreeBoss.png`/`BossAttack.png`), sliced via `Assets/Art/Characters/MainHubItems/`.

**Import is `Hub.EditorTools.MainHubItemsPack`** (`Tools ▸ RedMagic ▸ Hub ▸ Generar props del hub`)
— doesn't fit the usual `<Name>Pack` shape (one recipe, one character) because this is one sheet →
five *different* objects, each needing its own `AnimatorController`. Measured with `Pipeline ▸ 2b`
before writing it: the sheet is **not** a uniform 7-column grid — chest/book are 7 real frames,
wardrobe/mirror only 6, anvil 4 (the row's 7 slots are sparse; `AutoBounds` finds the 4 real blobs
and ignores the true gaps). Each "furniture" row (chest/book/wardrobe) already draws the **whole**
closed→open→closed arc in one direction — no `reverse` needed on the `DerivedClip`s that carve it
into Closed/Opening/Open/Closing (see `SPRITE_PIPELINE.md` §10). **`Hub.EditorTools.
OpenCloseControllerBuilder`** builds the controllers `AnimClipBuilder.BuildController` can't (it
only wires the enemy Idle/Walk/Attack/Hurt/Death vocabulary): `BuildBoolDriven` for the bool-gated
4-state open/close cycle (chest/book/wardrobe, param `IsOpened`), `BuildTriggerOneShot` for a
fire-and-return gesture (anvil, trigger `Spark`), `BuildIdleLoop` for a single always-playing state
(mirror's shimmer). The pack dresses `GoldChest.prefab` in place (`PrefabDresser`-style: only the
`SpriteRenderer` + `Animator`, nothing else touched) and creates `BookLectern`/`Wardrobe`/`Anvil`/
`Mirror` prefabs under `Assets/Prefab/Eviroment/` *if missing* — re-running it after one is placed
and hand-tuned in a scene won't overwrite it.

- **`InteractionPromptUi`** — the "Press [Interact] to…" cartel, shared by every interactable in
  this folder. Self-bootstrapping persistent `UIDocument`, same shape as `CurrencyHud` (own
  `Assets/Resources/InteractionPromptPanelSettings.asset`, sorting order 16, just above the
  currency HUD's 15). Exists because the old pattern — a `GameObject prompt` sign hand-dragged per
  object, just `SetActive` — can't change its wording at runtime, and the two-step flow needs
  "…to open" to become "…to pick up"/"…to consult" without a second sign. `Show(caller, text)` /
  `Hide(caller)` are static; `Hide` only clears the cartel if `caller` is who last showed it, so two
  overlapping interactables can't steal the prompt from each other.
- **`RewardPopupUi`** — "Arma obtenida: X" (icon + name), self-bootstrapping, holds a couple
  seconds then fades. Didn't exist before — the chest used to just log to console and flash a
  colour. Built generic (`Show(icon, title, name, accent)`) so the wardrobe's future 3-item loot
  reuses it instead of writing a second popup.
- **`HubLootContainer`** (abstract) — the open → wait → use/pick-up → close skeleton shared by all
  three "furniture" objects (chest, wardrobe, book): trigger+tag detection, the same interact-input
  reading as `SectionExit` (InputActionAsset + E/Enter/gamepad-north/touch fallback), an `Animator`
  bool (`openParameter`, default `IsOpened` — same convention as the Cainos chest controller and
  the old `AbilityChest`) that's optional and null-safe so the logic works before any art exists,
  and the reset-on-return-to-hub rule. That reset subscribes to `RunManager.RunEnded` with the
  exact retry-until-bound pattern `WeaponLoadout` already uses (`SceneManager.sceneLoaded` →
  `TryBindToRun` until `RunManager.Instance` exists) — a container resets on **any** `RunEnded`,
  win or lose, same as `WeaponLoadout.Inventory.Clear()`. Subclasses implement `OpenPromptText` /
  `PickupPromptText` / `OnLoot()`, and override `BlocksHubExitUntilLooted` if being empty should
  matter (only the chest does).
  - **`ChestLootContainer`** — replaces `AbilityChest` (moved to `Legacy/`) on
    `Assets/Prefab/Eviroment/GoldChest.prefab`, now dressed with the real chest art (still placed
    as an in-run reward chest in the world scenes, not yet in MainHub — dragging one into the hub
    is on the to-do list before the exit gate below does anything). Same weapon-grant logic (`WeaponLibrary.Random()` or the
    Inspector-forced `forcedWeapon`, `WeaponLoadout.Instance.Inventory.SetWeapon`, the
    `AbilityFx.Flash` colour burst, unequipping any stray `AbilityUser`), now behind two toques
    instead of one, plus `RewardPopupUi.Show(...)`. Its Inspector still gets the "pick a weapon or
    Aleatoria" dropdown (`Hub.EditorTools.ChestLootContainerEditor`, ported from
    `AbilityChestEditor`). **This is the run's only source of a starting weapon**, which is what
    makes it required — see `SectionExit` below.
  - **`WardrobeLootContainer`** — same shape, `BlocksHubExitUntilLooted = false` (optional loot),
    `OnLoot()` is a marked `// TODO` stub (3 items, pool/rarity undecided) that logs instead of
    granting anything yet.
  - **`BookLootContainer`** — replaces `CauldronInteractable` (moved to `Legacy/`, it was only ever
    the pre-art placeholder for this menu). Same two-step shape as the chest/wardrobe, but
    `OnLoot()` — the "2nd toque" — opens `UpgradeMenuController` instead of granting an item;
    doesn't block the hub exit.
- **`AnvilInteractable`** / **`MirrorInteractable`** — single-touch, no open/close state, own copy
  of the detection+input block (there's no two-step state machine here for a shared base to save).
  Anvil plays an optional `Animator` trigger then calls the `// TODO` hook `OnAnvilInteract()`.
  Mirror's row is a continuous idle shimmer loop rather than a triggered animation; interacting
  calls its own `// TODO` hook `OnMirrorInteract()` and opens **`MirrorMenuController`**
  (`Assets/Ui/MirrorMenuController.cs`) — a placeholder menu, same self-bootstrapping shape and
  `MenuStyle` look as `UpgradeMenuController` (own `Assets/Resources/MirrorMenuPanelSettings.asset`,
  sorting order 30), whose body is just a "Coming soon" label until stats/cosmetics are decided.
- **Legendary passives (mirror)** — 9 `LegendaryPassive` assets in `Resources/LegendaryPassives/`,
  synced (names, level 1/2 texts, `effectKind`, icon from `Assets/Art/Pasives/<NameWithoutSpaces>.png`)
  by `Tools ▸ RedMagic ▸ Hub ▸ Espejo · Generar pasivas legendarias` (`LegendaryPassiveStarterPack`).
  `LegendaryPassiveEffects.Level(kind)` gives the owned level (0/1/2; level 2 includes level 1
  unless the text says "instead"); every number is in `Resources/LegendaryPassiveTuning.asset`.
  Where each effect lives: **Codex Aurum / Páginas del Eco / Anales del Vacío / Manuscrito
  Eterno** → `Economy.LegendaryPassiveRunner` (self-bootstrapping; boss clear → `Items.ItemPickup.Drop`,
  run start → `Run.RunRerolls`, `WaveManager.AnyEnemySpawned` wave 0 → `SlowStatus`,
  `Health.AnyStarted` → -20% max HP on enemies during a run, every N cleared sections → item);
  **Grimorio del Umbral / El Tomo Roto** → `Hub.ChestLootContainer` (+ `UI.WeaponChoiceMenuController`,
  upgrades via `WeaponLevelManager.SetLevel`); **El Libro Sin Nombre** →
  `Gameplay.LegendaryPassivePlayerHook` on Player.prefab, through `Health.DeathGuard` /
  `Health.HitAbsorber`; **Volumen Carmesí** → `PlayerDamageMultiplier` in `Items.PlayerHit.Deal`.
  Tomo del Destino and Páginas del Eco lvl 2 are TODO (shop/reroll rework). `ItemPickup` is the
  free-item-on-the-ground drop: pooled, touch to equip (`TryEquip`), stays if free slots are full. When
  `LegendaryPassiveManager` drops one it calls **`UI.PassiveDropCinematic.Show(passive)`** (also
  `Show(name, icon)`): self-bootstrapping persistent uGUI canvas at `sortingOrder` 32767, built once
  and reused, queues repeated calls, pauses via `GameStateManager`, unscaled time, dismissed by any
  tap/click/key/gamepad south. Every timing/size/colour lives in
  `Resources/PassiveDropCinematicSettings.asset` (`Espejo · Ajustes de cinemática de pasiva`);
  `Espejo · Probar cinemática de pasiva (Play)` previews it. Headless `capture_game_view` does not
  show overlay canvases — switch the canvas to `ScreenSpaceCamera` at runtime to screenshot it.
- **`SectionExit.BlockedByMissingWeapon`** — the hub door (`SectionExit` with `world` assigned, see
  the Run/world system section) refuses to `StartRun` while
  `WeaponLoadout.Instance.Inventory.Weapon == null`, gated by `[SerializeField] bool
  requireWeaponToStartRun = true` (next to `requireEnemiesDead`, same convention). Checking the
  inventory directly — not a separate "chest looted" flag — means there's only one source of truth
  and the gate self-resets for free: `WeaponLoadout` already wipes the weapon on every `RunEnded`.
