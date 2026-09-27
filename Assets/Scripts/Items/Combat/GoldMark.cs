using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Mecánica del set de oro: <b>proyectiles dorados</b> y la <b>marca de oro</b> que dejan al golpear.
    ///
    /// <list type="bullet">
    /// <item>Cada proyectil del jugador tira al salir (<see cref="RollGilded"/>, lo llama
    /// <see cref="ShotProjectile"/>). La probabilidad sale de <see cref="GildChance"/> — <b>la única
    /// fórmula</b>: <c>base + porDaño × daño</c>, con tope. Los valores los aportan los items (Botas de
    /// oro: <see cref="SetGildSource"/>) y Oro 4 suma una base plana. Sin fuentes, 0.</item>
    /// <item>Un proyectil forzado (Guanteletes: cada N ataques) sale dorado siempre.</item>
    /// <item>Un proyectil dorado que golpea marca al enemigo (<see cref="Mark"/> →
    /// <see cref="GoldMarkStatus"/>): si muere marcado suelta su botín ×<see cref="MarkMultiplier"/>.</item>
    /// </list>
    /// Números de la marca y de los umbrales en <see cref="SynergyConfig"/> ▸ <see cref="SynergyTuning"/> (Oro).
    /// </summary>
    public static class GoldMark
    {
        private readonly struct GildSource
        {
            public readonly float BaseChance, ChancePerDamage, MaxChance;

            public GildSource(float baseChance, float chancePerDamage, float maxChance)
            {
                BaseChance = baseChance;
                ChancePerDamage = chancePerDamage;
                MaxChance = maxChance;
            }
        }

        private static readonly Dictionary<object, GildSource> Sources = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Sources.Clear();

        /// <summary>Una fuente de probabilidad de dorado (las Botas), con la clave de quien la pone.</summary>
        /// <param name="baseChance">Probabilidad con daño 0 (0.05 = 5%).</param>
        /// <param name="chancePerDamage">Lo que suma cada punto de daño del proyectil.</param>
        /// <param name="maxChance">Tope.</param>
        public static void SetGildSource(object source, float baseChance, float chancePerDamage, float maxChance) =>
            Sources[source] = new GildSource(baseChance, chancePerDamage, maxChance);

        public static void RemoveGildSource(object source) => Sources.Remove(source);

        /// <summary>
        /// Probabilidad (0..1) de que un proyectil de <paramref name="damage"/> salga dorado. Las
        /// fuentes se suman (dos Botas, el doble); el tope es el mayor de sus topes. Oro 4 suma una base
        /// plana, también sin Botas (entonces esa base es el tope).
        /// </summary>
        public static float GildChance(float damage)
        {
            var t = SynergyConfig.CurrentTuning;
            float flat = PlayerHit.SynergyActive(BuildTag.Gold, 4) ? t.gold4GildChanceBonus : 0f;
            if (Sources.Count == 0) return Mathf.Clamp01(flat);

            float baseChance = flat, perDamage = 0f, cap = 0f;
            foreach (var s in Sources.Values)
            {
                baseChance += Mathf.Max(0f, s.BaseChance);
                perDamage += Mathf.Max(0f, s.ChancePerDamage);
                cap = Mathf.Max(cap, s.MaxChance);
            }

            return Mathf.Clamp(baseChance + perDamage * Mathf.Max(0f, damage), 0f, Mathf.Clamp01(Mathf.Max(cap, flat)));
        }

        /// <summary>¿Sale dorado este proyectil? <paramref name="forced"/> = sí siempre (Guanteletes).</summary>
        public static bool RollGilded(float damage, bool forced) => forced || Random.value < GildChance(damage);

        /// <summary>Botín de un enemigo que muere marcado (Oro 6 lo sube).</summary>
        public static float MarkMultiplier
        {
            get
            {
                var t = SynergyConfig.CurrentTuning;
                return t.markDropMultiplier + (PlayerHit.SynergyActive(BuildTag.Gold, 6) ? t.gold6MarkMultiplierBonus : 0f);
            }
        }

        /// <summary>Marca a <paramref name="target"/> (o refresca su marca).</summary>
        public static GoldMarkStatus Mark(Health target)
        {
            var t = SynergyConfig.CurrentTuning;
            return GoldMarkStatus.Apply(target, t.markDuration, MarkMultiplier, t.markAuraColor, t.markAuraSize,
                                        t.markPulseSpeed, t.markPulseScale, t.goldAuraMaterial, t.markBodyTint);
        }
    }
}
