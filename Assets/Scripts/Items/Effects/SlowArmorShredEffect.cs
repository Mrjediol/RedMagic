using System;
using System.ComponentModel;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Grimorio: ralentizar a un enemigo le rompe la armadura mientras dure la ralentización.
    ///
    /// En este proyecto la armadura es daño recibido multiplicado (<c>Health.DamageMultiplier</c>,
    /// la de los jefes y las pasivas), así que "−30% de armadura" se aplica como +30% de daño
    /// recibido: vale igual contra un enemigo normal (que no tiene armadura propia) que contra un
    /// jefe acorazado. Se guarda en la ralentización al aplicarla (<see cref="PlayerHit.ApplySlow"/>
    /// → <c>SlowStatus</c> → <c>Health.StatusDamageMultiplier</c>) y se va con ella.
    /// </summary>
    [Serializable, DisplayName("Ralentizar · Rompe armadura mientras dure")]
    public sealed class SlowArmorShredEffect : ItemEffect
    {
        [Tooltip("Armadura que pierde el ralentizado: 0.3 = recibe un 30% más de daño.")]
        [Range(0f, 2f)] public float armorReduction = 0.3f;

        public override void OnEquip(ItemEffectContext context) =>
            CombatModifiers.SetSlowVulnerability(context, armorReduction);

        public override void Tick(ItemEffectContext context, float deltaTime) =>
            CombatModifiers.SetSlowVulnerability(context, armorReduction);

        public override void OnUnequip(ItemEffectContext context) => CombatModifiers.Remove(context);

        public override string Summary() =>
            Loc.Get("effect.slow_armor_shred", armorReduction * 100f);
    }
}
