using System;
using System.ComponentModel;
using RedMagic.Economy;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>Pechera de oro: cada golpe que el jugador recibe (y sobrevive) le da un poco de moneda.</summary>
    [Serializable, DisplayName("Economía · Moneda al recibir un golpe")]
    public sealed class CurrencyOnDamageTakenEffect : ItemEffect
    {
        public Currency currency = Currency.Gold;

        [Tooltip("Moneda por golpe recibido.")]
        [Min(0)] public int amount = 2;

        private sealed class State
        {
            public readonly PlayerHealthLink Link = new();
            public Action<float> OnDamaged;
        }

        public override void OnEquip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            state.OnDamaged = _ =>
            {
                var health = state.Link.Current;
                if (health == null || health.IsDead) return; // "cada golpe que sobrevives"
                if (CurrencyManager.Instance != null) CurrencyManager.Instance.Add(currency, amount);
            };
            Sync(context, state);
        }

        public override void Tick(ItemEffectContext context, float deltaTime) => Sync(context, context.GetState<State>());

        public override void OnUnequip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            state.Link.Release(health => health.Damaged -= state.OnDamaged);
        }

        private static void Sync(ItemEffectContext context, State state) =>
            state.Link.Sync(context, health => health.Damaged += state.OnDamaged,
                            health => health.Damaged -= state.OnDamaged);

        public override string Summary() =>
            Loc.Get("effect.currency_on_damage_taken", amount, Currencies.DisplayName(currency));
    }
}
