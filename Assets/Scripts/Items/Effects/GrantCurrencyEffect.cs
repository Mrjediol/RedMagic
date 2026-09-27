using System;
using System.ComponentModel;
using RedMagic.Economy;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>Da moneda cada cierto tiempo mientras el item está equipado.</summary>
    [Serializable, DisplayName("Economía · Dar moneda por segundo")]
    public sealed class GrantCurrencyEffect : ItemEffect
    {
        public Currency currency = Currency.Diamond;

        [Tooltip("Cantidad por intervalo.")]
        [Min(0)] public int amount = 1;

        [Tooltip("Segundos entre pagos.")]
        [Min(0.05f)] public float interval = 1f;

        public override void Tick(ItemEffectContext context, float deltaTime) =>
            context.Every(interval, deltaTime, () =>
            {
                if (CurrencyManager.Instance != null) CurrencyManager.Instance.Add(currency, amount);
            });

        public override string Summary() => Loc.Get("effect.grant_currency", amount, Currencies.DisplayName(currency), interval);
    }
}
