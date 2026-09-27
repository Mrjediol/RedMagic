using System;
using System.ComponentModel;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>Guanteletes de oro: cada N ataques, todos los proyectiles de ese disparo salen dorados (<see cref="GoldMark"/>).</summary>
    [Serializable, DisplayName("Proyectil · Cada N ataques, disparo dorado")]
    public sealed class ForceGildedEveryNEffect : ItemEffect
    {
        [Tooltip("Cada cuántos ataques sale uno dorado seguro (4 = el 4º, el 8º…).")]
        [Min(1)] public int attacksPerGilded = 4;

        private sealed class State
        {
            public int Count;
            public Action<CastArgs> OnCast;
        }

        public override void OnEquip(ItemEffectContext context)
        {
            var state = context.GetState<State>();
            state.OnCast = args =>
            {
                if (++state.Count < attacksPerGilded) return;
                state.Count = 0;
                args.ForceGilded = true;
            };
            WeaponUser.Casting += state.OnCast;
        }

        public override void OnUnequip(ItemEffectContext context) =>
            WeaponUser.Casting -= context.GetState<State>().OnCast;

        public override string Summary() => Loc.Get("effect.force_gilded", attacksPerGilded);
    }
}
