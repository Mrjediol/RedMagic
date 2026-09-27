using System;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// El árbol de mejoras permanentes del hub (el que abre el caldero). Estilo "Skull: The Hero
    /// Slayer": una rejilla de <see cref="rows"/> filas × <see cref="columns"/> columnas donde cada
    /// fila es un carril independiente y, dentro de un carril, la columna N sólo se puede comprar
    /// cuando la columna N-1 ya tiene al menos nivel 1.
    ///
    /// Vive en <c>Assets/Resources/UpgradeTree.asset</c> y lo carga <see cref="UpgradeManager"/>
    /// con <c>Resources.Load</c>. De momento todos los nodos son un placeholder de +10 %; se
    /// rellenan con mejoras de verdad editando el asset, sin tocar código.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeTree", menuName = "RedMagic/Upgrade Tree")]
    public class UpgradeTree : ScriptableObject
    {
        /// <summary>Un nodo de la rejilla: una mejora con varios niveles.</summary>
        [Serializable]
        public class Node
        {
            [Tooltip("Clave estable para el guardado. No la cambies una vez publicada o se pierde " +
                     "el progreso de ese nodo. Formato libre; por defecto 'r{fila}c{columna}'.")]
            public string id;

            public string title = "Placeholder";

            [Tooltip("Texto de una línea. {0} se sustituye por el % por nivel.")]
            public string description = "+{0}% placeholder por nivel";

            [Min(1)] public int maxLevel = 5;

            [Tooltip("Coste en fragmentos de alma del nivel 1.")]
            [Min(0)] public int baseCost = 10;

            [Tooltip("Lo que sube el coste por cada nivel ya comprado (nivel 2 = base + este, etc.).")]
            [Min(0)] public int costPerLevel = 8;

            [Tooltip("Bono por nivel, como fracción (0.10 = +10 %). Placeholder: nada lo lee todavía.")]
            public float bonusPerLevel = 0.10f;

            [Tooltip("Qué stat modifica. Placeholder por ahora; cuando exista el sistema de stats " +
                     "cada nodo apuntará al suyo (damage, maxHealth, moveSpeed...).")]
            public string statId = "placeholder";

            /// <summary>Título en el idioma activo: <c>upgrade.&lt;id&gt;.title</c> del fichero de idioma, o <see cref="title"/>.</summary>
            public string DisplayTitle => Loc.GetOr($"upgrade.{id}.title", title);

            /// <summary>Descripción en el idioma activo (<c>upgrade.&lt;id&gt;.description</c>), con su {0}.</summary>
            public string DisplayDescription => Loc.GetOr($"upgrade.{id}.description", description);

            /// <summary>Coste de subir de <paramref name="currentLevel"/> al siguiente.</summary>
            public int CostForNextLevel(int currentLevel) => baseCost + costPerLevel * Mathf.Max(0, currentLevel);
        }

        [Min(1)] [SerializeField] private int rows = 3;
        [Min(1)] [SerializeField] private int columns = 5;

        [Tooltip("Los nodos en orden fila-mayor: fila 0 completa, luego fila 1... Debe haber " +
                 "rows × columns entradas.")]
        [SerializeField] private Node[] nodes = Array.Empty<Node>();

        public int Rows => rows;
        public int Columns => columns;
        public Node[] Nodes => nodes;

        /// <summary>Nodo en (fila, columna), o null si está fuera de rango o el array no cuadra.</summary>
        public Node NodeAt(int row, int column)
        {
            if (row < 0 || column < 0 || row >= rows || column >= columns) return null;
            int index = row * columns + column;
            return index >= 0 && index < nodes.Length ? nodes[index] : null;
        }
    }
}
