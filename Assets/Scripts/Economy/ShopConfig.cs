using System;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Catálogo de la tienda que aparece a mitad de run. Vive en
    /// <c>Assets/Resources/ShopConfig.asset</c> y lo carga <c>ShopInteractable</c> con
    /// <c>Resources.Load</c>.
    ///
    /// Cada tienda sortea <see cref="itemsPerShop"/> artículos distintos de <see cref="items"/>.
    /// Se pagan con oro (moneda de run) y cada uno se puede comprar una sola vez: al comprarlo
    /// desaparece del escaparate.
    ///
    /// De momento los artículos son placeholders sin efecto; cuando exista el sistema de objetos,
    /// cada entrada apuntará a lo que de verdad conceda.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "RedMagic/Shop Config")]
    public class ShopConfig : ScriptableObject
    {
        [Serializable]
        public class Item
        {
            [Tooltip("Clave estable del artículo. Sólo para depurar y para el guardado futuro.")]
            public string id;

            public string title = "Placeholder";

            [TextArea(2, 3)]
            public string description = "Placeholder item";

            [Tooltip("Precio en oro.")]
            [Min(0)] public int cost = 25;

            [Tooltip("Icono opcional. Si se deja vacío la carta sale sólo con texto.")]
            public Sprite icon;
        }

        [SerializeField] private Item[] items = Array.Empty<Item>();

        [Tooltip("Cuántos artículos distintos enseña cada tienda.")]
        [Min(1)] [SerializeField] private int itemsPerShop = 3;

        public Item[] Items => items;
        public int ItemsPerShop => Mathf.Max(1, itemsPerShop);

        /// <summary>
        /// Sortea el escaparate de una tienda: <see cref="ItemsPerShop"/> artículos distintos, con
        /// un <see cref="System.Random"/> propio para que la misma semilla dé siempre la misma
        /// tienda (igual que el sorteo de secciones de <c>WorldDefinition</c>).
        /// </summary>
        public Item[] RollStock(int seed)
        {
            if (items == null || items.Length == 0) return Array.Empty<Item>();

            var pool = new System.Collections.Generic.List<Item>();
            foreach (var item in items)
                if (item != null) pool.Add(item);

            if (pool.Count == 0) return Array.Empty<Item>();

            int take = Mathf.Min(ItemsPerShop, pool.Count);
            var random = new System.Random(seed);
            var result = new Item[take];

            // Fisher-Yates parcial: sólo hace falta barajar los 'take' primeros.
            for (int i = 0; i < take; i++)
            {
                int j = random.Next(i, pool.Count);
                (pool[i], pool[j]) = (pool[j], pool[i]);
                result[i] = pool[i];
            }

            return result;
        }
    }
}
