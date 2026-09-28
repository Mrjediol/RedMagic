# Legacy (`Assets/Scripts/Legacy/`, `Assets/Resources/Legacy/`) — reference only, do not build on it
The **ability system**: 22 `AbilityDefinition` assets (now in `Assets/Resources/Legacy/Abilities/`
— `AbilityLibrary.ResourceFolder` points there, so the dev-only K menu still lists them as a
working reference instead of coming up empty), the nine archetype classes, `AbilityUser`,
`AbilityLibrary`, `AbilityMenuController`, the `AbilityStarterPack` editor tool, and the runtime
pieces only they used: `AbilityTurret`, `DamageZone`, `OrbitSpinner`. Plus the **old weapon level
1–3** system that hung off it, `AbilityLevelManager` + `WeaponUpgradeAltar` +
`WeaponUpgradeMenuController` — replaced by the `WeaponLevelManager` / `WeaponForgeAltar` /
`WeaponForgeMenuController` trio documented above. Superseded by the weapons/items build system;
five of the abilities were rescued as weapons (`Assets/Resources/Items/Weapons/`).

It still compiles and the `.meta` files moved with the sources, so GUIDs are intact. `Player.prefab`
still carries the legacy `AbilityUser` component (inert unless something equips it) and
`WeaponUpgrade.prefab` (`RunManager.bossRewardPrefab`) was **repointed** from `WeaponUpgradeAltar`
to the new `WeaponForgeAltar` — that swap is why the forge used to say "no llevas arma equipada"
even with an item-system weapon out. Read the rest as reference and port what is needed into
`Assets/Scripts/Items/`; do not extend it.

**Deliberately *not* legacy**, because the live systems call into it: `AbilityContext` /
`AbilityHit` (the one place target filtering and damage application live — weapons and bosses both
use it), `AbilityFx` (code-built sprites/flashes — the fallback when no placeholder prefab is
assigned), `ProjectileSpec` / `ProjectileFactory` and `Projectile` (the pooled projectile the
bosses fire; `ProjectileFactory` now restyles a prefab instance that carries `FxPlaceholderStyle`).
