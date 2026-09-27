using System;
using System.ComponentModel;
using RedMagic.Gameplay;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Multiplica una estadística del jugador mientras el item está equipado (velocidad, dash,
    /// salto). Varios items sobre la misma estadística se multiplican entre sí. El valor se vuelve a
    /// aplicar cada frame, así que retocarlo en el Inspector en Play se nota al momento.
    /// </summary>
    [Serializable, DisplayName("Jugador · Multiplicar estadística")]
    public sealed class PlayerStatMultiplierEffect : ItemEffect
    {
        [Tooltip("Qué estadística del jugador multiplica.")]
        public PlayerStat stat = PlayerStat.MoveSpeed;

        [Tooltip("×3 = el triple, ×0.5 = la mitad.")]
        [Min(0f)] public float multiplier = 2f;

        public override void OnEquip(ItemEffectContext context) =>
            PlayerStats.SetMultiplier(context, stat, multiplier);

        public override void Tick(ItemEffectContext context, float deltaTime) =>
            PlayerStats.SetMultiplier(context, stat, multiplier);

        public override void OnUnequip(ItemEffectContext context) => PlayerStats.Remove(context);

        public override string Summary() => Loc.Get("effect.stat_multiplier", StatName(stat), multiplier);

        public static string StatName(PlayerStat stat) => stat switch
        {
            PlayerStat.MoveSpeed => Loc.Get("stat.move_speed"),
            PlayerStat.DashDistance => Loc.Get("stat.dash_distance"),
            PlayerStat.JumpHeight => Loc.Get("stat.jump_height"),
            PlayerStat.CooldownRate => Loc.Get("stat.cooldown_rate"),
            _ => stat.ToString(),
        };
    }
}
