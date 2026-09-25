using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Catálogo de pasivas legendarias del espejo. Barre <c>Assets/Resources/LegendaryPassives/</c>
    /// y las deja indexadas por <see cref="LegendaryPassive.id"/> — mismo patrón de "carpeta, no
    /// lista" que <c>ItemLibrary</c>/<c>WeaponLibrary</c>.
    /// </summary>
    public static class LegendaryPassiveLibrary
    {
        public const string ResourceFolder = "LegendaryPassives";

        /// <summary>Casillas de la rejilla 3×3, siempre longitud 9. Un id sin asset queda a null.</summary>
        public const int SlotCount = 9;

        private static LegendaryPassive[] _bySlot;

        public static IReadOnlyList<LegendaryPassive> BySlot => _bySlot ??= Load();

        private static LegendaryPassive[] Load()
        {
            var slots = new LegendaryPassive[SlotCount];
            foreach (var passive in Resources.LoadAll<LegendaryPassive>(ResourceFolder))
            {
                if (passive == null || passive.id < 0 || passive.id >= SlotCount) continue;

                if (slots[passive.id] != null)
                {
                    Debug.LogWarning($"[LegendaryPassiveLibrary] Dos assets comparten id {passive.id}: " +
                                      $"'{slots[passive.id].name}' y '{passive.name}'. Se queda el primero.");
                    continue;
                }

                slots[passive.id] = passive;
            }

            return slots;
        }

        /// <summary>Olvida el catálogo cacheado (tras crear o borrar assets en el editor).</summary>
        public static void Invalidate() => _bySlot = null;
    }
}
