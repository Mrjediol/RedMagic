using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Catálogo de habilidades. Carga <b>todos</b> los assets de
    /// <c>Assets/Resources/Abilities/</c> y los deja ordenados por familia y nombre.
    ///
    /// Es deliberadamente un barrido de carpeta y no una lista que haya que mantener: añadir una
    /// habilidad al juego es dejar el asset en esa carpeta, sin tocar código ni arrastrar nada a
    /// ningún Inspector, y sin que nada falle si la carpeta está vacía.
    /// </summary>
    public static class AbilityLibrary
    {
        public const string ResourceFolder = "Legacy/Abilities";

        private static List<AbilityDefinition> _all;

        /// <summary>Todas las habilidades encontradas. Nunca es null.</summary>
        public static IReadOnlyList<AbilityDefinition> All
        {
            get
            {
                if (_all != null) return _all;

                var loaded = Resources.LoadAll<AbilityDefinition>(ResourceFolder);
                _all = new List<AbilityDefinition>(loaded);
                _all.RemoveAll(a => a == null);
                _all.Sort((a, b) =>
                {
                    int byCategory = a.Category.CompareTo(b.Category);
                    return byCategory != 0 ? byCategory : string.CompareOrdinal(a.DisplayName, b.DisplayName);
                });

                if (_all.Count == 0)
                    Debug.LogWarning("[Abilities] No hay ninguna habilidad en Resources/" + ResourceFolder + ".");

                return _all;
            }
        }

        /// <summary>Olvida el catálogo cacheado (tras crear o borrar assets en el editor).</summary>
        public static void Invalidate() => _all = null;
    }
}
