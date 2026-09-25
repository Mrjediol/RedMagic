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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Multipliers.Clear();

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
    }
}
