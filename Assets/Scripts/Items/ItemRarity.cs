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
            ItemRarity.Blue => "Azul",
            ItemRarity.Epic => "Épico",
            ItemRarity.Legendary => "Legendario",
            _ => "Común",
        };

        public static Color ColorOf(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Blue => new Color(0.35f, 0.65f, 1f),
            ItemRarity.Epic => new Color(0.75f, 0.45f, 1f),
            ItemRarity.Legendary => new Color(1f, 0.65f, 0.2f),
            _ => new Color(0.85f, 0.85f, 0.85f),
        };
    }
}
