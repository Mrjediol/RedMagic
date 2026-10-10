using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Test
{
    /// <summary>
    /// Escena de prueba del cometa: al empezar equipa <see cref="weapon"/> en el inventario de la run, para
    /// disparar nada más entrar (mantener ataque para cargar, soltar para disparar). El comportamiento se
    /// cambia en Play con el menú DEV (F9).
    /// </summary>
    public class CometTestKit : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition weapon;

        private void Start()
        {
            var loadout = WeaponLoadout.Instance;
            if (loadout != null && weapon != null) loadout.Inventory.SetWeapon(weapon);
            else Debug.LogWarning("[CometTest] No hay WeaponLoadout o falta el arma.", this);
        }
    }
}
