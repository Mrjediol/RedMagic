using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Item de uno de los 6 slots libres. No transforma el disparo: su único aporte es un par de
    /// tags (1 elemental + 1 universal) al conteo de sinergias. El efecto que el jugador nota sale
    /// de los umbrales de esas tags, no de este asset (ver documento de diseño, secciones 4 y 6).
    ///
    /// Los items con tag <see cref="BuildTag.Reset"/> son items de pool normales: llevan
    /// <see cref="BuildTag.Reset"/> como su tag universal. Su contador propio y su N base vivirán en
    /// una subclase <c>ResetItemDefinition</c> en el paso de efectos, no aquí.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Items/Free Pool Item", fileName = "Item_")]
    public class FreePoolItemDefinition : ItemDefinition
    {
        public override ItemSlot Slot => ItemSlot.Free;
    }
}
