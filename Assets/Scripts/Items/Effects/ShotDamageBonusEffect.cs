using System;
using System.ComponentModel;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>Espada de oro: todo disparo del arma hace un % más de daño (también cuenta para salir dorado).</summary>
    [Serializable, DisplayName("Daño · Más daño en los disparos")]
    public sealed class ShotDamageBonusEffect : ItemEffect
    {
        [Tooltip("Daño extra de cada disparo: 0.2 = +20%. Varios se multiplican.")]
        [Min(0f)] public float bonus = 0.2f;

        private sealed class State
        {
            public Action<CastArgs> OnCast;
        }

        public override void OnEquip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            state.OnCast = args => args.DamageScale *= 1f + bonus;
            WeaponUser.Casting += state.OnCast;
        }

        public override void OnUnequip(ItemEffectContext context) =>
            WeaponUser.Casting -= context.GetState<State>().OnCast;

        public override string Summary() => Loc.Get("effect.shot_damage_bonus", bonus * 100f);
    }
}
