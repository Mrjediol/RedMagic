using System;
using System.Collections.Generic;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Suerte de la tienda: multiplicadores sobre el peso de cada rareza que aportan los items
    /// equipados (Amuleto de oro), cada uno con su clave, igual que <c>PlayerStats</c> /
    /// <c>CombatModifiers</c>. <see cref="ShopConfig.RollStock"/> los aplica al sortear (al entrar y en
    /// cada reroll), así que el valor se calcula en ese momento — p. ej. con el oro que se lleva.
    ///
    /// Estático porque los items sobreviven al cambio de escena; se vacía al arrancar Play (Domain
    /// Reload desactivado).
    /// </summary>
    public static class ShopLuck
    {
        private static readonly Dictionary<object, Func<ItemRarity, float>> Sources = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Sources.Clear();

        /// <summary><paramref name="weightMultiplier"/>: por rareza, cuánto se multiplica su peso (1 = igual).</summary>
        public static void Set(object source, Func<ItemRarity, float> weightMultiplier) => Sources[source] = weightMultiplier;

        public static void Remove(object source) => Sources.Remove(source);

        /// <summary>Los pesos de <paramref name="weights"/> con todas las fuentes aplicadas (copia).</summary>
        public static ShopConfig.RarityTable Apply(ShopConfig.RarityTable weights)
        {
            if (Sources.Count == 0 || weights == null) return weights;
            return new ShopConfig.RarityTable(
                weights.common * Product(ItemRarity.Common),
                weights.blue * Product(ItemRarity.Blue),
                weights.epic * Product(ItemRarity.Epic),
                weights.legendary * Product(ItemRarity.Legendary));
        }

        private static float Product(ItemRarity rarity)
        {
            float m = 1f;
            foreach (var source in Sources.Values) m *= Mathf.Max(0f, source(rarity));
            return m;
        }
    }
}
