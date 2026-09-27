using System;
using System.ComponentModel;
using RedMagic.Combat;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Pechera de oro: vida máxima extra mientras está equipado (y cura lo que añade). Al quitarlo se
    /// devuelve. Sigue al jugador del hub a la run (<see cref="PlayerHealthLink"/>).
    /// </summary>
    [Serializable, DisplayName("Jugador · Más vida máxima")]
    public sealed class MaxHealthBonusEffect : ItemEffect
    {
        [Tooltip("Vida máxima que suma (puntos, no %).")]
        [Min(0f)] public float amount = 25f;

        private sealed class State
        {
            public readonly PlayerHealthLink Link = new();
            public float Applied; // lo sumado de verdad, para devolver lo mismo aunque se retoque amount
        }

        public override void OnEquip(ItemEffectContext context) => Sync(context);

        public override void Tick(ItemEffectContext context, float deltaTime) => Sync(context);

        public override void OnUnequip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            state.Link.Release(health => Remove(health, state));
        }

        private void Sync(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            state.Link.Sync(context, health => Add(health, state), health => Remove(health, state));
        }

        private void Add(Health health, State state)
        {
            state.Applied = amount;
            health.SetMaxHealth(health.MaxHealth + amount, healToFull: false);
            health.Heal(amount);
        }

        private static void Remove(Health health, State state)
        {
            health.SetMaxHealth(health.MaxHealth - state.Applied, healToFull: false);
            state.Applied = 0f;
        }

        public override string Summary() => Loc.Get("effect.max_health_bonus", amount);
    }
}
