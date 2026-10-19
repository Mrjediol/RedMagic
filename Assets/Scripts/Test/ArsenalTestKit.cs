using System.Collections.Generic;
using RedMagic.Arsenal;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Test
{
    /// <summary>
    /// Escena de prueba de las armas del arsenal: al empezar equipa <see cref="slot1"/> (activa) y <see cref="slot2"/> (reserva)
    /// y ofrece todas las de <see cref="weapons"/> en el menú DEV (F10), donde se cambian armas, evoluciones y mejoras.
    /// </summary>
    public class ArsenalTestKit : MonoBehaviour
    {
        [SerializeField] private List<WeaponDefinition> weapons = new List<WeaponDefinition>();
        [SerializeField] private WeaponDefinition slot1;
        [SerializeField] private WeaponDefinition slot2;

        private void Start()
        {
            ArsenalCatalog.Register(weapons);
            var loadout = WeaponLoadout.Instance;
            if (loadout == null) { Debug.LogWarning("[ArsenalTest] No hay WeaponLoadout.", this); return; }
            loadout.Inventory.SetWeapon(slot1);
            loadout.Inventory.SetSecondWeapon(slot2);
        }
    }
}
