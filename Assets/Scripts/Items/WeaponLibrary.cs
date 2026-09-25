using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Catálogo de armas. Carga <b>todos</b> los <see cref="WeaponDefinition"/> de
    /// <c>Assets/Resources/Items/</c> (incluidas subcarpetas como <c>Weapons/</c>) y los deja
    /// ordenados por elemento innato y nombre.
    ///
    /// Igual que <c>AbilityLibrary</c>: es un barrido de carpeta, no una lista que mantener. Añadir
    /// un arma es dejar el asset en esa carpeta — sin tocar código ni arrastrar nada a un Inspector,
    /// y sin que nada falle si no hay ninguna.
    /// </summary>
    public static class WeaponLibrary
    {
        public const string ResourceFolder = "Items";

        private static List<WeaponDefinition> _all;

        /// <summary>Todas las armas encontradas. Nunca es null.</summary>
        public static IReadOnlyList<WeaponDefinition> All
        {
            get
            {
                if (_all != null) return _all;

                var loaded = Resources.LoadAll<WeaponDefinition>(ResourceFolder);
                _all = new List<WeaponDefinition>(loaded);
                _all.RemoveAll(w => w == null);
                _all.Sort((a, b) =>
                {
                    int byElement = a.InnateElement.CompareTo(b.InnateElement);
                    return byElement != 0 ? byElement : string.CompareOrdinal(a.DisplayName, b.DisplayName);
                });

                if (_all.Count == 0)
                    Debug.LogWarning("[Items] No hay ninguna arma en Resources/" + ResourceFolder + ".");

                return _all;
            }
        }

        /// <summary>Una al azar, o null si no hay ninguna.</summary>
        public static WeaponDefinition Random()
        {
            var all = All;
            return all.Count == 0 ? null : all[UnityEngine.Random.Range(0, all.Count)];
        }

        /// <summary>La de mayor daño base (al azar entre empatadas), o null si no hay ninguna.</summary>
        public static WeaponDefinition Strongest()
        {
            WeaponDefinition best = null;
            int ties = 0;
            foreach (var weapon in All)
            {
                if (best == null || weapon.BaseDamage > best.BaseDamage) { best = weapon; ties = 1; }
                else if (Mathf.Approximately(weapon.BaseDamage, best.BaseDamage) &&
                         UnityEngine.Random.Range(0, ++ties) == 0) best = weapon;
            }
            return best;
        }

        /// <summary>
        /// Hasta <paramref name="count"/> armas distintas al azar. Con <paramref name="include"/>, esa
        /// va seguro entre ellas (en una posición al azar).
        /// </summary>
        public static List<WeaponDefinition> RandomDistinct(int count, WeaponDefinition include = null)
        {
            var pool = new List<WeaponDefinition>(All);
            if (include != null) pool.Remove(include);

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            int take = Mathf.Min(count - (include != null ? 1 : 0), pool.Count);
            var result = pool.GetRange(0, Mathf.Max(0, take));
            if (include != null) result.Insert(UnityEngine.Random.Range(0, result.Count + 1), include);
            return result;
        }

        /// <summary>Olvida el catálogo cacheado (tras crear o borrar assets en el editor).</summary>
        public static void Invalidate() => _all = null;
    }
}
