using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>Rareza de un item, de menos a más.</summary>
    public enum ItemRarity
    {
        Common = 0,
        /// <summary>Azul (raro).</summary>
        Blue = 1,
        Epic = 2,
        Legendary = 3,
    }

    public static class ItemRarities
    {
        public static string DisplayName(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Blue => Loc.Get("rarity.blue"),
            ItemRarity.Epic => Loc.Get("rarity.epic"),
            ItemRarity.Legendary => Loc.Get("rarity.legendary"),
            _ => Loc.Get("rarity.common"),
        };

        /// <summary>
        /// Color de la rareza: nombres en la UI, auras y efectos de la tienda, drops. Único origen:
        /// <see cref="ItemRarityColors"/> (<c>Resources/ItemRarityColors.asset</c>, editable en el Inspector).
        /// </summary>
        public static Color ColorOf(ItemRarity rarity) => ItemRarityColors.Current.For(rarity);
    }
}
