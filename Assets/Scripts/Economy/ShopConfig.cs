using System;
using System.Collections.Generic;
using RedMagic.Items;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Un artículo del escaparate ya sorteado: el item real del sistema de builds y lo que cuesta
    /// en oro esta tienda concreta. Lo produce <see cref="ShopConfig.RollStock"/> y lo consumen
    /// <see cref="ShopInteractable"/> y el menú de tienda.
    /// </summary>
    public sealed class ShopStockEntry
    {
        public readonly ItemDefinition Item;
        public readonly int Cost;

        public ShopStockEntry(ItemDefinition item, int cost)
        {
            Item = item;
            Cost = cost;
        }
    }

    /// <summary>
    /// Ajuste de la tienda de mitad de run. Vive en <c>Assets/Resources/ShopConfig.asset</c> y lo
    /// carga <c>ShopInteractable</c> con <c>Resources.Load</c>.
    ///
    /// Ya <b>no</b> lleva una lista de artículos: los items reales salen de <see cref="ItemLibrary"/>
    /// (barrido de <c>Resources/Items</c>), así que crear un item nuevo lo mete en la tienda solo.
    /// Este asset sólo decide <b>cuántos</b> de cada tipo enseña una tienda y <b>a qué precio</b>.
    ///
    /// Cada tienda enseña 1 modificador de Elemento + 1 de Trayectoria + 1 de Forma +
    /// <see cref="freePoolCount"/> items de pool libre (6 en total con el valor por defecto).
    /// </summary>
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "RedMagic/Shop Config")]
    public class ShopConfig : ScriptableObject
    {
        [Serializable]
        public class CostRange
        {
            [Min(0)] public int min = 20;
            [Min(0)] public int max = 40;

            public int Roll(System.Random random)
            {
                int lo = Mathf.Min(min, max);
                int hi = Mathf.Max(min, max);
                return random.Next(lo, hi + 1);
            }
        }

        [Header("Cuántos items de pool libre enseña cada tienda")]
        [Tooltip("Los slots dedicados (Elemento / Trayectoria / Forma) siempre son 1 cada uno.")]
        [Min(0)]
        [SerializeField] private int freePoolCount = 3;

        [Header("Precio en oro por tipo")]
        [SerializeField] private CostRange elementCost = new CostRange { min = 30, max = 50 };
        [SerializeField] private CostRange trajectoryCost = new CostRange { min = 30, max = 50 };
        [SerializeField] private CostRange shapeCost = new CostRange { min = 30, max = 50 };
        [SerializeField] private CostRange freePoolCost = new CostRange { min = 18, max = 38 };

        public int FreePoolCount => Mathf.Max(0, freePoolCount);

        /// <summary>
        /// Sortea el escaparate de una tienda con un <see cref="System.Random"/> propio, para que la
        /// misma semilla dé siempre la misma tienda (igual que el sorteo de secciones de
        /// <c>WorldDefinition</c>).
        ///
        /// Orden fijo: Elemento, Trayectoria, Forma, luego los de pool libre. Si a
        /// <see cref="ItemLibrary"/> le falta algún tipo, esa entrada simplemente no aparece.
        /// </summary>
        public ShopStockEntry[] RollStock(int seed)
        {
            var random = new System.Random(seed);
            var stock = new List<ShopStockEntry>();

            AddOne(stock, ItemLibrary.Elements, elementCost, random);
            AddOne(stock, ItemLibrary.Trajectories, trajectoryCost, random);
            AddOne(stock, ItemLibrary.Shapes, shapeCost, random);
            AddMany(stock, ItemLibrary.FreePool, FreePoolCount, freePoolCost, random);

            return stock.ToArray();
        }

        private static void AddOne<T>(List<ShopStockEntry> stock, IReadOnlyList<T> pool,
                                      CostRange cost, System.Random random) where T : ItemDefinition
        {
            if (pool == null || pool.Count == 0) return;
            var pick = pool[random.Next(pool.Count)];
            stock.Add(new ShopStockEntry(pick, cost.Roll(random)));
        }

        private static void AddMany<T>(List<ShopStockEntry> stock, IReadOnlyList<T> pool, int count,
                                       CostRange cost, System.Random random) where T : ItemDefinition
        {
            if (pool == null || pool.Count == 0 || count <= 0) return;

            // Fisher-Yates parcial sobre una copia: 'take' items distintos.
            var bag = new List<T>(pool);
            int take = Mathf.Min(count, bag.Count);
            for (int i = 0; i < take; i++)
            {
                int j = random.Next(i, bag.Count);
                (bag[i], bag[j]) = (bag[j], bag[i]);
                stock.Add(new ShopStockEntry(bag[i], cost.Roll(random)));
            }
        }
    }
}
