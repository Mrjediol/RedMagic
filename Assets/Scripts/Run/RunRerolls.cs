using System;
using UnityEngine;

namespace RedMagic.Run
{
    /// <summary>
    /// Rerolls que le quedan a la run. Por ahora sólo se rellena (Páginas del Eco lo sube al
    /// empezar la run) — ninguna pantalla lo gasta todavía; el sistema de rerolls se rehace aparte.
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
