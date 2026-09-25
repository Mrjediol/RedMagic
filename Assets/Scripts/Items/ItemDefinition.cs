using System.Collections.Generic;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// En qué hueco del arma entra un item. Los tres dedicados aceptan exactamente 1 item activo
    /// ("último equipado gana"); el pool libre son 6 huecos sin restricción de tipo.
    /// </summary>
    public enum ItemSlot
    {
        DedicatedElement,
        DedicatedTrajectory,
        DedicatedShape,
        Free,
    }

    /// <summary>
    /// Base de todo lo equipable que aporta tags de sinergia: los items del pool libre y los tres
    /// modificadores de slot dedicado (<see cref="WeaponModifier"/>). El arma en sí no hereda de
    /// aquí (<see cref="WeaponDefinition"/>) porque no se "equipa" en un slot, pero sí aporta tags
    /// con las mismas reglas.
    ///
    /// Cada item concreto es un <b>asset</b> (ScriptableObject) en <c>Assets/Resources/Items/</c>:
    /// crear uno nuevo es duplicar un asset y tocar datos, sin escribir código ni enganchar nada
    /// en el Inspector.
    ///
    /// Lo que el item <b>hace</b> mientras está equipado va en su lista <c>effects</c>
    /// (<see cref="ItemEffect"/>), con los valores en este mismo asset.
    /// </summary>
    public abstract class ItemDefinition : ScriptableObject
    {
        [Header("Ficha")]
        [Tooltip("Nombre que se ve en la UI. Vacío = el nombre del asset.")]
        [SerializeField] private string displayName;

        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Tooltip("Icono para la cuadrícula de items.")]
        [SerializeField] private Sprite icon;

        [Tooltip("Color de acento de la carta / tooltip.")]
        [SerializeField] private Color accent = new Color(0.8f, 0.8f, 0.85f);

        [Tooltip("Rareza: color del nombre en la UI (y, más adelante, peso en tienda y cofres).")]
        [SerializeField] private ItemRarity rarity = ItemRarity.Common;

        [Header("Tags de sinergia")]
        [Tooltip("Tags que este item aporta al conteo. Reglas por slot (se avisan en OnValidate):\n" +
                 "· Elemento y pool libre: 1 elemental + 1 universal\n" +
                 "· Trayectoria y Forma: 2 universales, 0 elementales\n" +
                 "Cada aparición en la lista suma +1 punto completo a esa tag.")]
        [SerializeField] private List<BuildTag> tags = new List<BuildTag>();

        [Header("Comportamiento")]
        [Tooltip("Lo que hace el item mientras está equipado. '+' añade una entrada; el desplegable " +
                 "elige el tipo de efecto y debajo salen sus valores. Varios efectos se combinan.")]
        [SerializeReference, SubclassPicker] private List<ItemEffect> effects = new List<ItemEffect>();

        /// <summary>Slot en el que entra este item. Lo fija el tipo, no es editable por asset.</summary>
        public abstract ItemSlot Slot { get; }

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color Accent => accent;
        public ItemRarity Rarity => rarity;
        public IReadOnlyList<BuildTag> Tags => tags;

        /// <summary>Efectos mientras está equipado (los aplica <see cref="ItemEffectRunner"/>).</summary>
        public IReadOnlyList<ItemEffect> Effects => effects;

        /// <summary>
        /// Puntos que este item aporta a <paramref name="tag"/>. Cada aparición en la lista cuenta
        /// +1 (normalmente 0 o 1; se permite duplicar por si un item de diseño lo necesita).
        /// </summary>
        public int PointsFor(BuildTag tag)
        {
            int points = 0;
            for (int i = 0; i < tags.Count; i++)
                if (tags[i] == tag) points++;
            return points;
        }

        /// <summary>
        /// Reglas de composición de tags según el slot. Sólo avisa por consola (no corrige ni
        /// bloquea), igual que <see cref="WorldDefinition"/> con las secciones sin asignar: el
        /// fallo se ve en el Inspector mientras se monta el asset, no a mitad de partida.
        /// </summary>
        protected virtual void OnValidate()
        {
            int elemental = 0, universal = 0;
            foreach (var tag in tags)
            {
                if (BuildTags.IsElemental(tag)) elemental++;
                else universal++;
            }

            bool mechanicalSlot = Slot == ItemSlot.DedicatedTrajectory || Slot == ItemSlot.DedicatedShape;
            if (mechanicalSlot)
            {
                if (elemental != 0 || universal != 2)
                    Debug.LogWarning($"[Items] '{name}' ({Slot}): se esperan 2 tags universales y 0 " +
                                     $"elementales; hay {universal} universales y {elemental} elementales.", this);
            }
            else if (elemental != 1 || universal != 1)
            {
                Debug.LogWarning($"[Items] '{name}' ({Slot}): se esperan 1 elemental + 1 universal; " +
                                 $"hay {elemental} elementales y {universal} universales.", this);
            }
        }
    }
}
