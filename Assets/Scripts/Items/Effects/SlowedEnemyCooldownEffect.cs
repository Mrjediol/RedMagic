using System;
using System.ComponentModel;
using RedMagic.Combat;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Botas: mientras haya al menos un enemigo ralentizado en pantalla, el enfriamiento del arma se
    /// vacía más rápido (multiplica <see cref="PlayerStat.CooldownRate"/>). Se comprueba cada frame,
    /// así que se enciende y se apaga solo según haya o no ralentizados a la vista.
    /// </summary>
    [Serializable, DisplayName("Enfriamiento · Más rápido con ralentizados en pantalla")]
    public sealed class SlowedEnemyCooldownEffect : ItemEffect
    {
        [Tooltip("Ritmo de enfriamiento con un ralentizado a la vista. ×1.5 = un 50% más rápido.")]
        [Min(1f)] public float cooldownRate = 1.5f;

        public override void OnEquip(ItemEffectContext context) => Tick(context, 0f);

        public override void Tick(ItemEffectContext context, float deltaTime) =>
            PlayerStats.SetMultiplier(context, PlayerStat.CooldownRate, SlowStatus.AnyOnScreen() ? cooldownRate : 1f);

        public override void OnUnequip(ItemEffectContext context) => PlayerStats.Remove(context);

        public override string Summary() =>
            $"Con un enemigo ralentizado en pantalla, el enfriamiento corre ×{cooldownRate:0.##}";
    }
}
