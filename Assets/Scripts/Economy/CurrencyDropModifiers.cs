using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Bonos a la moneda que sueltan los enemigos (Casco de oro, Oro 2), cada uno con su clave como
    /// <c>PlayerStats</c>: quien lo aporta lo pone y lo quita, <see cref="CurrencyManager.GrantDrops"/>
    /// sólo lee el total. Se suman: dos +25% dan +50%.
    /// </summary>
    public static class CurrencyDropModifiers
    {
        private static readonly Dictionary<object, float> Bonuses = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Bonuses.Clear();

        /// <summary><paramref name="bonus"/>: 0.25 = +25% de toda moneda soltada.</summary>
        public static void Set(object source, float bonus) => Bonuses[source] = bonus;

        public static void Remove(object source) => Bonuses.Remove(source);

        /// <summary>Multiplicador total de los bonos (1 = sin cambios).</summary>
        public static float Multiplier
        {
            get
            {
                float total = 1f;
                foreach (float bonus in Bonuses.Values) total += Mathf.Max(0f, bonus);
                return total;
            }
        }
    }
}
