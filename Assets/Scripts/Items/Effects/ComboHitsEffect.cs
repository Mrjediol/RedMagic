using System;
using System.ComponentModel;
using RedMagic.Gameplay;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Alarga el combo cuerpo a cuerpo con golpes extra (el combo base es de 3; la lista de
    /// <c>PlayerAttack</c> tiene hasta 5). Varios items se suman.
    /// </summary>
    [Serializable, DisplayName("Jugador · Golpes extra de combo")]
    public sealed class ComboHitsEffect : ItemEffect
    {
        [Tooltip("Golpes que añade al combo cuerpo a cuerpo.")]
        [Min(1)] public int hits = 1;

        public override void OnEquip(ItemEffectContext context) => PlayerStats.SetComboHitBonus(context, hits);

        public override void Tick(ItemEffectContext context, float deltaTime) => PlayerStats.SetComboHitBonus(context, hits);

        public override void OnUnequip(ItemEffectContext context) => PlayerStats.RemoveComboHitBonus(context);

        public override string Summary() => Loc.Get("effect.combo_hits", hits);
    }
}
