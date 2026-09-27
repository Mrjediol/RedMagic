using System;
using System.ComponentModel;
using RedMagic.Economy;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Escudo de oro: resta un daño plano a cada golpe que recibe el jugador (<see cref="CombatModifiers"/>
    /// → <c>Health.FlatDamageReduction</c>) y convierte lo bloqueado en moneda. Con varios escudos, cada uno
    /// convierte la parte del bloqueo que aporta.
    /// </summary>
    [Serializable, DisplayName("Defensa · Reducir daño y convertirlo en moneda")]
    public sealed class DamageReductionToCurrencyEffect : ItemEffect
    {
        [Tooltip("Daño que se le quita a cada golpe recibido.")]
        [Min(0f)] public float flatReduction = 3f;

        [Tooltip("Lo mínimo que sigue entrando de un golpe (así nunca se vuelve inmune a golpes flojos).")]
        [Min(0f)] public float minimumDamage = 1f;

        public Currency currency = Currency.Gold;

        [Tooltip("Moneda por cada punto de daño bloqueado (los restos se acumulan).")]
        [Min(0f)] public float currencyPerBlockedDamage = 1f;

        private sealed class State
        {
            public readonly PlayerHealthLink Link = new();
            public Action<float> OnReduced;
            public float Pending; // fracción de moneda aún sin pagar
        }

        public override void OnEquip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            state.OnReduced = reduced =>
            {
                var (total, _) = CombatModifiers.DamageReduction();
                float share = total > 0f ? Mathf.Clamp01(flatReduction / total) : 1f;
                state.Pending += reduced * share * currencyPerBlockedDamage;

                int pay = Mathf.FloorToInt(state.Pending);
                if (pay <= 0) return;
                state.Pending -= pay;
                if (CurrencyManager.Instance != null) CurrencyManager.Instance.Add(currency, pay);
            };
            Sync(context, state);
        }

        public override void Tick(ItemEffectContext context, float deltaTime) => Sync(context, context.GetState<State>());

        public override void OnUnequip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            CombatModifiers.Remove(context);
            state.Link.Release(health => health.DamageReduced -= state.OnReduced);
        }

        private void Sync(ItemEffectContext context, State state)
        {
            CombatModifiers.SetDamageReduction(context, flatReduction, minimumDamage);
            state.Link.Sync(context, health => health.DamageReduced += state.OnReduced,
                            health => health.DamageReduced -= state.OnReduced);
        }

        public override string Summary() => Loc.Get("effect.damage_reduction_to_currency", flatReduction, minimumDamage,
                                                     currencyPerBlockedDamage, Currencies.DisplayName(currency));
    }
}
