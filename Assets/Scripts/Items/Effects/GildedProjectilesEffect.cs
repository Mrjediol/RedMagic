using System;
using System.ComponentModel;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Botas de oro: los proyectiles del jugador pueden salir dorados, más probable cuanto más daño hacen;
    /// uno dorado deja la marca de oro al golpear (botín ×N al morir). La fórmula está en
    /// <see cref="GoldMark.GildChance"/>; aquí sólo sus valores.
    /// </summary>
    [Serializable, DisplayName("Proyectil · Disparos dorados (marca de oro)")]
    public sealed class GildedProjectilesEffect : ItemEffect
    {
        [Tooltip("Probabilidad de salir dorado con daño 0: 0.05 = 5%.")]
        [Range(0f, 1f)] public float baseChance = 0.05f;

        [Tooltip("Lo que suma cada punto de daño del proyectil: 0.004 = +0.4% por punto (20 de daño → +8%).")]
        [Min(0f)] public float chancePerDamagePoint = 0.004f;

        [Tooltip("Tope de la probabilidad.")]
        [Range(0f, 1f)] public float maxChance = 0.5f;

        public override void OnEquip(ItemEffectContext context) => Register(context);

        public override void Tick(ItemEffectContext context, float deltaTime) => Register(context);

        public override void OnUnequip(ItemEffectContext context) => GoldMark.RemoveGildSource(context);

        private void Register(ItemEffectContext context) =>
            GoldMark.SetGildSource(context, baseChance, chancePerDamagePoint, maxChance);

        public override string Summary() => Loc.Get("effect.gilded_projectiles", baseChance * 100f,
                                                     chancePerDamagePoint * 100f, maxChance * 100f, GoldMark.MarkMultiplier);
    }
}
