using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>Estadísticas del jugador que los items pueden multiplicar.</summary>
    public enum PlayerStat
    {
        /// <summary>Velocidad máxima de carrera (y su aceleración, para llegar a ella igual de rápido).</summary>
        MoveSpeed,

        /// <summary>Longitud del dash (alarga su duración; la velocidad del dash no cambia).</summary>
        DashDistance,

        /// <summary>Altura del salto (la velocidad inicial se escala con la raíz: altura ∝ velocidad²).</summary>
        JumpHeight,

        /// <summary>
        /// Ritmo al que se descuenta el enfriamiento del arma (<c>WeaponUser</c>): ×1.5 = el cooldown se
        /// vacía un 50% más rápido. No cambia el cooldown base, sólo lo rápido que corre.
        /// </summary>
        CooldownRate,

        /// <summary>Daño de los golpes cuerpo a cuerpo (<c>PlayerAttack</c>).</summary>
        MeleeDamage,

        /// <summary>Tamaño de los tajos cuerpo a cuerpo: agranda el tajo y su zona de daño a la vez.</summary>
        MeleeRange,
    }

    /// <summary>
    /// Multiplicadores activos sobre las estadísticas del jugador. Los pone y los quita quien los
    /// aporta (hoy los efectos de item, <c>PlayerStatMultiplierEffect</c>), cada uno con su propia
    /// clave; <see cref="PlayerMovement"/> sólo lee el producto. Así ningún item toca los campos del
    /// jugador, y quitar un item no tiene que acordarse de "devolver" el valor que había.
    ///
    /// Estático (no un componente del jugador) porque el jugador cambia entre hub y run y los items
    /// no: el que exista lo lee. Domain Reload está desactivado, así que se vacía al arrancar Play.
    /// </summary>
    public static class PlayerStats
    {
        private static readonly Dictionary<object, (PlayerStat stat, float value)> Multipliers = new();
        private static readonly Dictionary<object, int> ComboBonuses = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Multipliers.Clear();
            ComboBonuses.Clear();
        }

        /// <summary>Pone (o actualiza) el multiplicador que aporta <paramref name="source"/>.</summary>
        public static void SetMultiplier(object source, PlayerStat stat, float value) =>
            Multipliers[source] = (stat, value);

        /// <summary>Quita lo que aportaba <paramref name="source"/>. Es seguro llamarlo de más.</summary>
        public static void Remove(object source) => Multipliers.Remove(source);

        /// <summary>Producto de todos los multiplicadores activos de <paramref name="stat"/> (1 = sin cambios).</summary>
        public static float Multiplier(PlayerStat stat)
        {
            float m = 1f;
            foreach (var entry in Multipliers.Values)
                if (entry.stat == stat) m *= entry.value;
            return m;
        }

        /// <summary>Golpes extra del combo cuerpo a cuerpo que aporta <paramref name="source"/> (se suman).</summary>
        public static void SetComboHitBonus(object source, int hits) => ComboBonuses[source] = hits;

        /// <summary>Quita los golpes extra que aportaba <paramref name="source"/>. Seguro llamarlo de más.</summary>
        public static void RemoveComboHitBonus(object source) => ComboBonuses.Remove(source);

        /// <summary>Total de golpes extra del combo (<c>PlayerAttack</c> no pasa del tamaño de su lista).</summary>
        public static int ComboHitBonus
        {
            get { int n = 0; foreach (var v in ComboBonuses.Values) n += v; return n; }
        }
    }
}
