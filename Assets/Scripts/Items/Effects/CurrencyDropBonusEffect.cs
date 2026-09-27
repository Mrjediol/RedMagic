using System;
using System.ComponentModel;
using RedMagic.Economy;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>Casco de oro: toda moneda que sueltan los enemigos sale aumentada un %.</summary>
    [Serializable, DisplayName("Economía · Más moneda de los enemigos")]
    public sealed class CurrencyDropBonusEffect : ItemEffect
    {
        [Tooltip("Aumento de toda moneda soltada: 0.25 = +25%. Varios bonos se suman.")]
        [Min(0f)] public float bonus = 0.25f;

        public override void OnEquip(ItemEffectContext context) => CurrencyDropModifiers.Set(context, bonus);

        public override void OnUnequip(ItemEffectContext context) => CurrencyDropModifiers.Remove(context);

        // Se reescribe cada frame: retocar el valor en Play se nota al momento.
        public override void Tick(ItemEffectContext context, float deltaTime) => CurrencyDropModifiers.Set(context, bonus);

        public override string Summary() => Loc.Get("effect.currency_drop_bonus", bonus * 100f);
    }
}
