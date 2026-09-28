# Weapons & items — the build system (`Assets/Scripts/Items/`)
The current attack system (it **replaced** the ability system; see Legacy below). A weapon is a
`WeaponDefinition` asset in `Assets/Resources/Items/Weapons/` describing a **base shot**; items
transform that shot. Content by convention: a new weapon or item is a new asset, no code, no
Inspector wiring.

- **Shot pipeline** (`ShotResolver`, `WeaponShot`): 1. the weapon emits its base shot
  (`WeaponShot.FromWeapon`) → 2. the equipped **Trajectory** modifier (how it moves) → 3. the
  **Shape** modifier (how many projectiles / splits) → 4. the **Element** modifier (paints damage
  type), which every projectile produced by step 3 inherits automatically, split children included.
  `Resolve` returns the resolved plan with no physics (testable); `Fire` also launches it. A weapon
  with no Element item still paints with its `InnateElement`.
- **Slots** (`WeaponInventory`) — 3 dedicated (Element / Trajectory / Shape, exactly one item each,
  last equipped wins) + 6 free slots with no type restriction, plus the equipped weapon. It only
  tracks what sits in each slot and raises `ItemEquipped`/`ItemUnequipped`; nothing else.
- **Items** — `ItemDefinition` (base: name/description/icon/accent + synergy tags) → `WeaponModifier`
  (`ElementModifier`, `TrajectoryModifier`, `ShapeModifier`, each with a fixed `PipelineOrder`) and
  `FreePoolItemDefinition`. Free-pool items **do not transform the shot at all** — their whole
  contribution is a pair of tags.
- **Synergies** (`SynergyTracker` + `BuildTag`/`BuildTags` + `Assets/Resources/SynergyConfig.asset`)
  — the single source of truth for tag points, fed only by `WeaponInventory` events. Every equipped
  copy counts (+1 per tag, the same asset in two free slots counts twice), thresholds are 2/4/6 and
  points cap at 6. It counts and notifies; it implements no threshold effect.
- **`WeaponUser`** (on `Player.prefab`) — the only holder of firing state (cooldown, hold-to-charge
  via `BaseShot.chargeTime`). Silences `PlayerAttack` while a weapon is equipped, and yields to a
  legacy `AbilityUser` if an ability is somehow equipped. **The shot leaves on the gesture, not on
  the press**: `releaseDelay` (0.28s, the 4th-5th drawing of the 0.42s attack clip) holds the shot
  while the animation winds up, and the shot context — muzzle and facing — is built at release, so
  turning mid-swing fires where you now look. The cooldown still counts from the press, so the
  delay costs no rate of fire. It is a timer and not an `AnimationEvent` like the enemies use
  because the player’s `Animator` lives on the `Sprite` child and events only reach components on
  their own GameObject. `PlayerAttack.windup` and `RangedAttack.windup` (0.25s) are the same idea
  for the other two attack paths.
- **Projectile size / muzzle / collider authoring** — `BaseShot ▸ Projectile Collider Override`
  (`overrideCollider`, `colliderType` Circle/Box/Capsule, `colliderSize` world units — Circle uses X
  as radius — `colliderOffset`): `ShotProjectile` swaps in its own collider of that shape (created once
  per pooled instance, never per shot) and disables the prefab's, so the shared prefab is never
  edited; off = prefab collider untouched. World→local math is `Gameplay.ProjectileColliderShape`.
  **Scene preview**: `WeaponUser ▸ previewWeapon` (Player.prefab = `Weapon_ProyectilRecto`) draws,
  with no Play mode, the muzzle cross (yellow), the projectile at its real size (cyan) and its
  collider (green) whenever the player or that weapon asset is selected; editing the weapon repaints
  the Scene view (`WeaponDefinition.OnValidate`). Drawing lives in `Items.WeaponShotPreview`.
- **`WeaponLoadout`** — self-bootstrapping `DontDestroyOnLoad` singleton owning the run's
  `WeaponInventory`.
- **Weapon levels (1–3)** — `WeaponLevelManager` (self-bootstrapping singleton, keyed by
  `WeaponDefinition` asset reference) charges Skulls (1 for →2, 3 for →3, tunable in
  `upgradeCosts`) and **wipes on `RunEnded`**, same shape as the old ability-level system it
  replaces. **Placeholder effect only**: until real per-level stats are designed, levelling up just
  tints the shot — black at level 2, gold at level 3 (`WeaponLevelManager.TryGetLevelTint`, applied
  by `ShotResolver.Resolve` after the Element step) — purely to prove the plumbing works.
  **`WeaponForgeAltar`** (on `Assets/Prefab/Eviroment/WeaponUpgrade.prefab`, `RunManager`'s
  `bossRewardPrefab`) + **`WeaponForgeMenuController`** (`Assets/Ui/`, own
  `WeaponForgeMenuPanelSettings`) are the forge: same one-card-one-button shape as before, reading
  `WeaponUser.Weapon` instead of the legacy `AbilityUser.Equipped`.
- **Runtime** (`Items/Shot/`) — `ShotProjectile` and `ShotBeam`, both pooled, reusing `AbilityHit`
  for target filtering/damage and `AbilityFx` for sprite/tint/sorting. Built in code unless the
  weapon's `BaseShot.projectilePrefab` / `beamPrefab` is set, in which case the visual comes from
  that prefab via `PrefabPool` (see "Placeholder visuals → real art").
- **`WeaponLibrary`** / **`ItemLibrary`** — folder scans of `Resources/Items/(Weapons|Modifiers)` and
  the item assets, same pattern as the old `AbilityLibrary`.
- **`ItemMenuController`** (`Assets/Ui/`, key **I**, `ItemMenuPanelSettings`) — the build screen;
  same self-bootstrapping code-built pattern as the other menus.
- **`AbilityChest`** (name kept; on `Assets/Prefab/Eviroment/GoldChest.prefab`) — proximity
  interactable that grants a **`WeaponDefinition`** through `WeaponLoadout.Instance.Inventory`.
  Empty `forcedWeapon` = random from `WeaponLibrary`; `AbilityChestEditor` draws it as a dropdown.
- **The shop** (`Economy.ShopManager`, see Economy) sells items and equips them straight into the inventory (`TryEquip`).
- **`ShotPipelineHarness`** — drop it in a scene and hit Play to fire the four configurations
  (bare / trajectory only / shape only / all three layers) at a dummy it spawns.
- Current content: 7 weapons (the ones rescued from the old abilities plus a plain
  `Weapon_ProyectilRecto`) and the 6 ice-set free-pool items below — no modifier assets right now.
- **Item behaviour lives on the item** — read `Assets/_Pipeline/ITEMS_PIPELINE.md`.
  `ItemDefinition.effects` is a `[SerializeReference, SubclassPicker]` list of `ItemEffect`
  subclasses (`Assets/Scripts/Items/Effects/`): pick the type from a dropdown in the item's
  Inspector and tune its values there. `ItemEffectRunner` (inside `WeaponLoadout`) applies them
  only while equipped (`OnEquip` / `OnUnequip` / `Tick`); per-equip state goes in the
  `ItemEffectContext`, never in effect fields (the instance is shared asset data). Player stat
  changes go through `Gameplay.PlayerStats` multipliers, read by `PlayerMovement` (move speed,
  dash distance = duration, jump height = √ on velocity). `Health.Drain` is damage that isn't a
  hit (no i-frames, no hurt anim, still a popup). Icons: `ItemIconsPack` (keys `Assets/Icon/*`
  via `UiArtKitProcessor`) and `Tools ▸ RedMagic ▸ Items ▸ Catálogo de items (iconos)`, which
  edits the icon on each item asset.
- **Ice set + slow + synergy effects** (`Tools ▸ RedMagic ▸ Items ▸ Set de hielo · Generar`,
  `IceSetPack`): 6 items with real mechanics, `Fx_IceExplosion` (pooled flipbook). **All player
  damage goes through `Items.PlayerHit.Deal`** (shots, beam, sword, legacy abilities) — it applies
  item hit bonuses, Ice-2 slow (`Combat.SlowStatus`: speed via `SlowStatus.SpeedScale` in the enemy
  movers, tint via `HitFlash.SetStatusTint`, extra damage via `Health.StatusDamageMultiplier`, kept
  separate from boss armour) and raises `Landed`/`Killed`. Hooks for effects: `PlayerHit.Killed`,
  `WeaponUser.Casting` (next-shot buffs; `CastArgs.OnFirstImpact` for "where this shot lands" — the Helmet
  explosion), `CombatModifiers`, `BuffIndicators` → `UI.ItemBuffHud` (only for effects the player must
  time; the Staff has none). Lifesteal 2: the heal travels with pooled `Fx.LifeMotes` — red motes
  fly from the kill to the player every kill and each applies its share of the heal ON ARRIVAL
  (`DrainPacket`); green motes only when HP was actually gained.
  Threshold numbers live in `SynergyConfig ▸ Tuning`, applied by `SynergyEffectRunner` (in
  `WeaponLoadout`). Items have a `rarity`. Details: `ITEMS_PIPELINE.md` §4-5.
- **Gold set + Gold Mark** (`Tools ▸ RedMagic ▸ Items ▸ Set de oro · Generar`, `GoldSetPack`; assets in
  `Assets/Resources/Items/GoldSet/`): `BuildTag.Gold` (elemental family, id 4). Player projectiles roll
  "gilded" at spawn through the single formula `Items.GoldMark.GildChance` (sources: Boots effect, Gold
  4); a gilded hit applies `Combat.GoldMarkStatus` (SlowStatus-style: refresh, no stack, aura + ring +
  HitFlash tint that yields to the slow tint) and `CurrencyDropper` multiplies that enemy's loot.
  Items carry an optional per-item `price` (0 = rarity table). New reusable hooks:
  `Economy.CurrencyDropModifiers`, `Economy.ShopLuck`, `ShopManager.Entered`,
  `CombatModifiers.SetDamageReduction` → `Health.FlatDamageReduction`/`DamageReduced`. Details:
  `ITEMS_PIPELINE.md` §6.
- Projectiles never collide with other projectiles — pellets from one blast spawn on top of each
  other and would annihilate on frame one.
