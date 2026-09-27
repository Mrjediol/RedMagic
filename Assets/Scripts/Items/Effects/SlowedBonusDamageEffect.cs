using System;
using System.ComponentModel;
using RedMagic.Combat;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Anillo: cada golpe del jugador a un enemigo ralentizado suma un daño extra igual a un % de
    /// su vida máxima. Entra por <see cref="PlayerHit.Deal"/> antes del golpe, así que la armadura
    /// y el daño extra de la ralentización también lo multiplican.
    /// </summary>
    [Serializable, DisplayName("Golpe · % de vida máxima a ralentizados")]
    public sealed class SlowedBonusDamageEffect : ItemEffect
    {
        [Tooltip("Fracción de la vida máxima del objetivo que se suma a cada golpe: 0.08 = 8%.")]
        [Range(0f, 1f)] public float percentOfMaxHealth = 0.08f;

        [Tooltip("Tope del daño extra por golpe (contra jefes de miles de vida). 0 = sin tope.")]
        [Min(0f)] public float maxBonusPerHit;

        public override void OnEquip(ItemEffectContext context) =>
            CombatModifiers.SetHitBonus(context, Bonus);

        public override void OnUnequip(ItemEffectContext context) => CombatModifiers.Remove(context);

        private float Bonus(Health target)
        {
            if (!SlowStatus.IsSlowedTarget(target)) return 0f;
            float bonus = target.MaxHealth * percentOfMaxHealth;
            return maxBonusPerHit > 0f ? Mathf.Min(bonus, maxBonusPerHit) : bonus;
        }

        public override string Summary() =>
            (maxBonusPerHit > 0f
                ? Loc.Get("effect.slowed_bonus_damage_capped", percentOfMaxHealth * 100f, maxBonusPerHit)
                : Loc.Get("effect.slowed_bonus_damage", percentOfMaxHealth * 100f));
    }
}
