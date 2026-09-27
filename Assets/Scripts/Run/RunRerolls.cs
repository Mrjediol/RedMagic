using System;
using UnityEngine;

namespace RedMagic.Run
{
    /// <summary>
    /// Rerolls que le quedan a la run. Empieza en <c>ShopConfig.startingRerolls</c> (+ Páginas del Eco,
    /// en <c>LegendaryPassiveRunner</c>), vuelve a 0 al acabar la run y lo gasta el altar del reroll
    /// de la tienda (<c>ShopManager</c>). El HUD escucha <see cref="Changed"/>.
    /// </summary>
    public static class RunRerolls
    {
        public static int Count { get; private set; }

        /// <summary>Nuevo total tras cada cambio.</summary>
        public static event Action<int> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Count = 0;
            Changed = null;
        }

        public static void Set(int count)
        {
            Count = Mathf.Max(0, count);
            Changed?.Invoke(Count);
        }

        public static void Add(int amount) => Set(Count + amount);

        public static bool TrySpend()
        {
            if (Count <= 0) return false;
            Set(Count - 1);
            return true;
        }
    }
}
