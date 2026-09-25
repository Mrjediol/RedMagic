using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Los colores de rareza, en un solo asset: <c>Assets/Resources/ItemRarityColors.asset</c>.
    /// Lo lee <see cref="ItemRarities.ColorOf"/>, que es lo que usan la UI (nombres), las auras y
    /// efectos de la tienda y los drops. Sin asset, estos valores por defecto.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemRarityColors", menuName = "RedMagic/Items/Rarity Colors")]
    public class ItemRarityColors : ScriptableObject
    {
        public const string ResourcePath = "ItemRarityColors";

        public Color common = new(0.45f, 0.85f, 0.4f);
        public Color blue = new(0.35f, 0.65f, 1f);
        public Color epic = new(0.75f, 0.45f, 1f);
        public Color legendary = new(1f, 0.72f, 0.2f);

        public Color For(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Blue => blue,
            ItemRarity.Epic => epic,
            ItemRarity.Legendary => legendary,
            _ => common,
        };

        private static ItemRarityColors _current;

        public static ItemRarityColors Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Resources.Load<ItemRarityColors>(ResourcePath);
                if (_current == null) _current = CreateInstance<ItemRarityColors>();
                return _current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _current = null;
    }
}
