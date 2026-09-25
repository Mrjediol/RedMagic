using System;
using System.Collections.Generic;
using RedMagic.Core;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>Un artículo sorteado para un altar: el item y lo que cuesta en oro en esta tienda.</summary>
    public readonly struct ShopStockEntry
    {
        public readonly ItemDefinition Item;
        public readonly int Price;

        public ShopStockEntry(ItemDefinition item, int price)
        {
            Item = item;
            Price = price;
        }
    }

    /// <summary>
    /// Todo lo ajustable de la tienda, en <c>Assets/Resources/ShopConfig.asset</c> (lo lee
    /// <see cref="Instance"/> con <c>Resources.Load</c>):
    ///  - <see cref="shopScene"/>: la escena de la tienda que <c>RunManager</c> mete tras cada
    ///    sección normal despejada (nunca tras el jefe). Es global: no está en el pool de ningún mundo.
    ///  - <see cref="rarityWeightsPerWorld"/>: probabilidad de cada rareza por mundo (entrada 0 =
    ///    mundo 1; la última vale para ese mundo y todos los siguientes). Los pesos no tienen que
    ///    sumar 100.
    ///  - <see cref="basePrice"/> × <see cref="priceMultiplierPerWorld"/> (misma regla de índices).
    ///
    /// No hay lista de items: salen de <see cref="ItemLibrary.All"/>, así que un item nuevo aparece
    /// en la tienda solo.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "RedMagic/Shop Config")]
    public class ShopConfig : ScriptableObject
    {
        public const string ResourcePath = "ShopConfig";

        [Serializable]
        public class RarityTable
        {
            [Min(0f)] public float common;
            [Min(0f)] public float blue;
            [Min(0f)] public float epic;
            [Min(0f)] public float legendary;

            public RarityTable() { }

            public RarityTable(float common, float blue, float epic, float legendary)
            {
                this.common = common;
                this.blue = blue;
                this.epic = epic;
                this.legendary = legendary;
            }

            public float For(ItemRarity rarity) => rarity switch
            {
                ItemRarity.Blue => blue,
                ItemRarity.Epic => epic,
                ItemRarity.Legendary => legendary,
                _ => common,
            };
        }

        [Header("Escena")]
        [Tooltip("La escena de la tienda. Entrada = SectionEntry (puerta izquierda), salida = " +
                 "SectionExit (puerta derecha).")]
        public SceneReference shopScene = new SceneReference();

        [Header("Rareza (peso por mundo: 0 = mundo 1, la última vale para el resto)")]
        public List<RarityTable> rarityWeightsPerWorld = new()
        {
            new RarityTable(50f, 35f, 12f, 3f),
            new RarityTable(35f, 35f, 22f, 8f),
            new RarityTable(20f, 35f, 30f, 15f),
            new RarityTable(10f, 30f, 35f, 25f),
        };

        [Header("Precio en oro = base por rareza × multiplicador del mundo")]
        public RarityTable basePrice = new(30f, 60f, 120f, 200f);

        [Tooltip("0 = mundo 1; la última vale para ese mundo y los siguientes.")]
        public List<float> priceMultiplierPerWorld = new() { 1f, 1.4f, 1.8f, 2.2f };

        private static ShopConfig _instance;

        /// <summary>El asset de Resources, o uno con los valores por defecto si falta.</summary>
        public static ShopConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<ShopConfig>(ResourcePath);
                if (_instance == null) _instance = CreateInstance<ShopConfig>();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        // ------------------------------------------------------------------ reglas

        /// <param name="world">Número de mundo, desde 1.</param>
        public RarityTable WeightsFor(int world) => ForWorld(rarityWeightsPerWorld, world) ?? new RarityTable(1f, 0f, 0f, 0f);

        /// <param name="world">Número de mundo, desde 1.</param>
        public int PriceFor(ItemRarity rarity, int world)
        {
            float multiplier = priceMultiplierPerWorld is { Count: > 0 }
                ? priceMultiplierPerWorld[Mathf.Clamp(world - 1, 0, priceMultiplierPerWorld.Count - 1)]
                : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(basePrice.For(rarity) * multiplier));
        }

        /// <summary>
        /// Sortea hasta <paramref name="count"/> artículos distintos: primero la rareza por peso,
        /// luego un item de esa rareza. Si no queda ninguno de esa rareza, la más cercana que tenga
        /// (primero hacia abajo, luego hacia arriba). Menos items que huecos = huecos vacíos.
        /// </summary>
        public List<ShopStockEntry> RollStock(int count, int world, System.Random rng)
        {
            var remaining = new List<ItemDefinition>(ItemLibrary.All);
            var stock = new List<ShopStockEntry>(count);
            var weights = WeightsFor(world);

            for (int i = 0; i < count && remaining.Count > 0; i++)
            {
                var rarity = RollRarity(weights, rng);
                var item = PickNearest(remaining, rarity, rng);
                if (item == null) break;

                remaining.Remove(item);
                stock.Add(new ShopStockEntry(item, PriceFor(item.Rarity, world)));
            }

            return stock;
        }

        private static ItemRarity RollRarity(RarityTable weights, System.Random rng)
        {
            var rarities = (ItemRarity[])Enum.GetValues(typeof(ItemRarity));
            float total = 0f;
            foreach (var r in rarities) total += Mathf.Max(0f, weights.For(r));
            if (total <= 0f) return ItemRarity.Common;

            float roll = (float)rng.NextDouble() * total;
            foreach (var r in rarities)
            {
                roll -= Mathf.Max(0f, weights.For(r));
                if (roll < 0f) return r;
            }
            return rarities[rarities.Length - 1];
        }

        private static ItemDefinition PickNearest(List<ItemDefinition> pool, ItemRarity rarity, System.Random rng)
        {
            int max = Enum.GetValues(typeof(ItemRarity)).Length;
            for (int distance = 0; distance < max; distance++)
            {
                foreach (int r in distance == 0 ? new[] { (int)rarity } : new[] { (int)rarity - distance, (int)rarity + distance })
                {
                    if (r < 0 || r >= max) continue;
                    var matches = pool.FindAll(item => (int)item.Rarity == r);
                    if (matches.Count > 0) return matches[rng.Next(matches.Count)];
                }
            }
            return null;
        }

        private static T ForWorld<T>(List<T> perWorld, int world) where T : class =>
            perWorld is { Count: > 0 } ? perWorld[Mathf.Clamp(world - 1, 0, perWorld.Count - 1)] : null;
    }
}
