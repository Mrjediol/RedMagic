using System;
using System.ComponentModel;
using RedMagic.Gameplay;
using RedMagic.Localization;

namespace RedMagic.Items
{
    /// <summary>Recibir daño en pleno dash ya no lo corta ni empuja (el daño entra igual).</summary>
    [Serializable, DisplayName("Jugador · Dash imparable")]
    public sealed class DashUnstoppableEffect : ItemEffect
    {
        public override void OnEquip(ItemEffectContext context) => PlayerStats.SetDashUnstoppable(context, true);

        public override void Tick(ItemEffectContext context, float deltaTime) => PlayerStats.SetDashUnstoppable(context, true);

        public override void OnUnequip(ItemEffectContext context) => PlayerStats.SetDashUnstoppable(context, false);

        public override string Summary() => Loc.Get("effect.dash_unstoppable");
    }
}
