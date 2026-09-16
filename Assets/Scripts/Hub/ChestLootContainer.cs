using RedMagic.Abilities;
using RedMagic.Items;
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
    /// Sustituye a <c>AbilityChest</c> (movido a Legacy): misma lógica de entrega, ahora sobre
    /// <see cref="HubLootContainer"/> para el flujo en dos toques (abrir → recoger) y el reseteo
    /// al volver de una run.
    /// </summary>
    public class ChestLootContainer : HubLootContainer
    {
        [Header("Contenido")]
        [Tooltip("Arma que suelta este cofre. Vacío = una al azar (lo normal en el juego). El " +
                 "desplegable del Inspector lo dibuja ChestLootContainerEditor.")]
        [SerializeField] private WeaponDefinition forcedWeapon;

        protected override string OpenPromptText => "Pulsa [Interactuar] para abrir el cofre";
        protected override string PickupPromptText => "Pulsa [Interactuar] para recoger el arma";
        protected override bool BlocksHubExitUntilLooted => true;

        /// <summary>Arma que soltará: la forzada, o null si va a ser aleatoria.</summary>
        public WeaponDefinition ForcedWeapon => forcedWeapon;

        protected override void OnLoot()
        {
            var weapon = forcedWeapon != null ? forcedWeapon : WeaponLibrary.Random();
            if (weapon == null)
            {
                Debug.LogWarning("[ChestLootContainer] No hay armas en Resources/Items: el cofre " +
                                 "se abre vacío.", this);
                return;
            }

            if (WeaponLoadout.Instance == null)
            {
                Debug.LogWarning("[ChestLootContainer] No hay WeaponLoadout al que darle el arma.", this);
                return;
            }

            WeaponLoadout.Instance.Inventory.SetWeapon(weapon);

            // El sistema de armas reemplaza al de habilidades: si el jugador aún llevaba una
            // habilidad equipada, se la quitamos para que el arma dispare de inmediato (WeaponUser
            // cede ante una habilidad equipada). Null-safe por si AbilityUser ya no está.
            foreach (var user in FindObjectsByType<AbilityUser>(FindObjectsSortMode.None))
                user.Equip(null);

            // Destello del color del arma sobre el cofre: se ve que ha salido algo y de qué familia
            // es, sin necesidad de UI.
            AbilityFx.Flash(weapon.Icon, transform.position + Vector3.up * 0.6f,
                            Vector2.one * 0.9f, weapon.Accent, 0.6f, 0f, 2f, gameObject);

            RewardPopupUi.Show(weapon.Icon, "Arma obtenida", weapon.DisplayName, weapon.Accent);

            Debug.Log($"[ChestLootContainer] Arma obtenida: {weapon.DisplayName} " +
                      $"({weapon.BaseDamage:0} dmg · {weapon.BaseCooldown:0.00}s).", this);
        }
    }
}
