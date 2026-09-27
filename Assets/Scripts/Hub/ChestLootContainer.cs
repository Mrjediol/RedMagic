using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Economy;
using RedMagic.Items;
using RedMagic.Localization;
using RedMagic.UI;
using UnityEngine;

namespace RedMagic.Hub
{
    /// <summary>
    /// El cofre dorado del hub. Da un <b>arma</b> (<see cref="WeaponDefinition"/>) — la única
    /// forma de conseguir la primera arma de la run, así que <see cref="Run.SectionExit"/> exige
    /// <see cref="WeaponLoadout"/> tenga un arma equipada antes de dejar salir por la puerta del
    /// hub. No hace falta que este cofre sepa nada de eso: basta con que
    /// <c>WeaponLoadout.Instance.Inventory.SetWeapon</c> sea la única forma de conseguir un arma
    /// en el hub, y lo es.
    ///
    /// Pasivas legendarias que lo cambian (números en <see cref="LegendaryPassiveTuning"/>):
    ///  - <b>Grimorio del Umbral</b>: nv1 muestra 3 armas y el jugador elige
    ///    (<see cref="WeaponChoiceMenuController"/>); nv2 la elegida sale con 1 mejora.
    ///  - <b>El Tomo Roto</b>: nv1 el arma sale con 2 mejoras; nv2 además es la de mayor daño base
    ///    (con el Grimorio, va seguro entre las opciones).
    /// Las mejoras se suman y se topan en <see cref="WeaponLevelManager.MaxLevel"/>. Un
    /// <see cref="forcedWeapon"/> puesto en el Inspector manda sobre todo esto (es una prueba).
    /// </summary>
    public class ChestLootContainer : HubLootContainer
    {
        [Header("Contenido")]
        [Tooltip("Arma que suelta este cofre. Vacío = una al azar (lo normal en el juego). El " +
                 "desplegable del Inspector lo dibuja ChestLootContainerEditor.")]
        [SerializeField] private WeaponDefinition forcedWeapon;

        protected override string OpenPromptKey => "prompt.chest_open";
        protected override string PickupPromptKey => "prompt.chest_pickup";
        protected override bool BlocksHubExitUntilLooted => true;

        /// <summary>Arma que soltará: la forzada, o null si va a ser aleatoria.</summary>
        public WeaponDefinition ForcedWeapon => forcedWeapon;

        protected override void OnLoot()
        {
            if (WeaponLoadout.Instance == null)
            {
                Debug.LogWarning("[ChestLootContainer] No hay WeaponLoadout al que darle el arma.", this);
                return;
            }

            if (forcedWeapon != null)
            {
                Grant(forcedWeapon, chosen: false);
                return;
            }

            var tuning = LegendaryPassiveTuning.Current;
            bool strongest = LegendaryPassiveEffects.Level(LegendaryPassiveEffectKind.ChestWeaponUpgrades) >= 2;

            if (LegendaryPassiveEffects.Has(LegendaryPassiveEffectKind.ChestWeaponChoice))
            {
                var options = WeaponLibrary.RandomDistinct(tuning.chestWeaponChoices,
                                                           strongest ? WeaponLibrary.Strongest() : null);
                var passive = LegendaryPassiveEffects.Find(LegendaryPassiveEffectKind.ChestWeaponChoice);

                if (options.Count > 1 && WeaponChoiceMenuController.Instance != null &&
                    WeaponChoiceMenuController.Instance.Open(options, w => Grant(w, chosen: true),
                                                             passive != null ? passive.DisplayName : ""))
                {
                    AudioManager.Instance?.Play(tuning.grimorioChoiceSound);
                    return;
                }

                if (options.Count > 0)
                {
                    Grant(options[0], chosen: true);
                    return;
                }
            }

            var weapon = strongest ? WeaponLibrary.Strongest() : WeaponLibrary.Random();
            if (weapon == null)
            {
                Debug.LogWarning("[ChestLootContainer] No hay armas en Resources/Items: el cofre " +
                                 "se abre vacío.", this);
                return;
            }

            Grant(weapon, chosen: false);
        }

        /// <param name="chosen">La eligió el jugador con el Grimorio (cuenta para su nivel 2).</param>
        private void Grant(WeaponDefinition weapon, bool chosen)
        {
            if (weapon == null || WeaponLoadout.Instance == null) return;

            WeaponLoadout.Instance.Inventory.SetWeapon(weapon);

            var tuning = LegendaryPassiveTuning.Current;
            int upgrades = 0;
            if (forcedWeapon == null && LegendaryPassiveEffects.Has(LegendaryPassiveEffectKind.ChestWeaponUpgrades))
            {
                upgrades += tuning.chestWeaponUpgrades;
                AudioManager.Instance?.Play(tuning.tomoRotoUpgradeSound, transform.position);
            }
            if (chosen && LegendaryPassiveEffects.Level(LegendaryPassiveEffectKind.ChestWeaponChoice) >= 2)
                upgrades += tuning.chestChoiceUpgradesLevel2;
            if (upgrades > 0 && WeaponLevelManager.Instance != null)
                WeaponLevelManager.Instance.SetLevel(weapon, 1 + upgrades);

            // El sistema de armas reemplaza al de habilidades: si el jugador aún llevaba una
            // habilidad equipada, se la quitamos para que el arma dispare de inmediato (WeaponUser
            // cede ante una habilidad equipada). Null-safe por si AbilityUser ya no está.
            foreach (var user in FindObjectsByType<AbilityUser>(FindObjectsSortMode.None))
                user.Equip(null);

            // Destello del color del arma sobre el cofre: se ve que ha salido algo y de qué familia
            // es, sin necesidad de UI.
            AbilityFx.Flash(weapon.Icon, transform.position + Vector3.up * 0.6f,
                            Vector2.one * 0.9f, weapon.Accent, 0.6f, 0f, 2f, gameObject);

            int level = WeaponLevelManager.Instance != null ? WeaponLevelManager.Instance.GetLevel(weapon) : 1;
            RewardPopupUi.Show(weapon.Icon, level > 1 ? Loc.Get("reward.weapon_level", level) : Loc.Get("reward.weapon"),
                               weapon.DisplayName, weapon.Accent);

            Debug.Log($"[ChestLootContainer] Arma obtenida: {weapon.DisplayName} nivel {level} " +
                      $"({weapon.BaseDamage:0} dmg · {weapon.BaseCooldown:0.00}s).", this);
        }
    }
}
