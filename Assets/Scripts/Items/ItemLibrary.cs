using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Catálogo de items del sistema de builds. Barre <c>Assets/Resources/Items/</c> (y subcarpetas)
    /// y deja los assets separados por el hueco al que van: los tres modificadores de slot dedicado
    /// (<see cref="ElementModifier"/>, <see cref="TrajectoryModifier"/>, <see cref="ShapeModifier"/>)
    /// y el pool libre (<see cref="FreePoolItemDefinition"/>).
    ///
    /// Igual que <c>AbilityLibrary</c> y <c>WeaponLibrary</c>: es un barrido de carpeta, no una
    /// lista que mantener. Añadir un item es dejar el asset ahí; la tienda y cualquier otro
    /// consumidor lo ven solos.
    /// </summary>
    public static class ItemLibrary
    {
        public const string ResourceFolder = "Items";

        private static List<ElementModifier> _elements;
        private static List<TrajectoryModifier> _trajectories;
        private static List<ShapeModifier> _shapes;
        private static List<FreePoolItemDefinition> _freePool;

        public static IReadOnlyList<ElementModifier> Elements => _elements ??= Load<ElementModifier>();
        public static IReadOnlyList<TrajectoryModifier> Trajectories => _trajectories ??= Load<TrajectoryModifier>();
        public static IReadOnlyList<ShapeModifier> Shapes => _shapes ??= Load<ShapeModifier>();
        public static IReadOnlyList<FreePoolItemDefinition> FreePool => _freePool ??= Load<FreePoolItemDefinition>();

        /// <summary>
        /// El pool global: todos los items de todas las clases. Lo que lee la tienda y los drops
        /// gratis, así que un item nuevo en <c>Resources/Items</c> aparece en ambos sin más.
        /// </summary>
        public static IReadOnlyList<ItemDefinition> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<ItemDefinition>();
                _all.AddRange(Elements);
                _all.AddRange(Trajectories);
                _all.AddRange(Shapes);
                _all.AddRange(FreePool);
                return _all;
            }
        }

        private static List<ItemDefinition> _all;

        private static List<T> Load<T>() where T : ItemDefinition
        {
            var list = new List<T>(Resources.LoadAll<T>(ResourceFolder));
            list.RemoveAll(x => x == null);
            list.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return list;
        }

        /// <summary>Olvida los catálogos cacheados (tras crear o borrar assets en el editor).</summary>
        public static void Invalidate()
        {
            _elements = null;
            _trajectories = null;
            _shapes = null;
            _freePool = null;
            _all = null;
        }
    }
}
