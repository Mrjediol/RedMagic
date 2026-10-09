using System;
using System.ComponentModel;
using RedMagic.Gameplay;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// El dash hace daño a los enemigos que atraviesa (una vez por enemigo y dash). Lo aplica
    /// <c>PlayerDashCombat</c>; varios items se suman.
    /// </summary>
    [Serializable, DisplayName("Jugador · Dash con daño")]
    public sealed class DashDamageEffect : ItemEffect
    {
        [Tooltip("Daño a cada enemigo que atraviesa el dash.")]
        [Min(0f)] public float damage = 20f;

        public override void OnEquip(ItemEffectContext context) => PlayerStats.SetDashDamage(context, damage);

        public override void Tick(ItemEffectContext context, float deltaTime) => PlayerStats.SetDashDamage(context, damage);

        public override void OnUnequip(ItemEffectContext context) => PlayerStats.RemoveDashDamage(context);

        public override string Summary() => Loc.Get("effect.dash_damage", damage);
    }
}
