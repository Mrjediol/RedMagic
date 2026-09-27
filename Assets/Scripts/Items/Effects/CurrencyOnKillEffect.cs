using System;
using System.ComponentModel;
using RedMagic.Economy;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>Espada de oro: cada baja del jugador da moneda extra (con probabilidad opcional).</summary>
    [Serializable, DisplayName("Al matar · Moneda extra")]
    public sealed class CurrencyOnKillEffect : ItemEffect
    {
        public Currency currency = Currency.Gold;

        [Tooltip("Moneda extra por baja.")]
        [Min(0)] public int amount = 3;

        [Tooltip("Probabilidad de que la baja pague (1 = siempre).")]
        [Range(0f, 1f)] public float chance = 1f;

        private sealed class State
        {
            public Action<KillInfo> OnKill;
        }

        public override void OnEquip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            state.OnKill = _ =>
            {
                if (chance < 1f && UnityEngine.Random.value >= chance) return;
                if (CurrencyManager.Instance != null) CurrencyManager.Instance.Add(currency, amount);
            };
            PlayerHit.Killed += state.OnKill;
        }

        public override void OnUnequip(ItemEffectContext context) => PlayerHit.Killed -= context.GetState<State>().OnKill;

        public override string Summary() => chance >= 1f
            ? Loc.Get("effect.currency_on_kill", amount, Currencies.DisplayName(currency))
            : Loc.Get("effect.currency_on_kill_chance", amount, Currencies.DisplayName(currency), chance * 100f);
    }
}
