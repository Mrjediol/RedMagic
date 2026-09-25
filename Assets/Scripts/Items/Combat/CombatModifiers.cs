using System;
using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Reglas de combate que aportan los items equipados, cada una con la clave de quien la pone
    /// (el <see cref="ItemEffectContext"/> del slot), igual que <c>PlayerStats</c> con los
    /// multiplicadores: el efecto la pone en <c>OnEquip</c>, la quita en <c>OnUnequip</c>, y el
    /// sistema que la usa (<see cref="PlayerHit"/>, <see cref="ShotProjectile"/>) sólo lee el
    /// agregado. Ningún arma sabe qué item la ha cambiado.
    ///
    /// Estático porque el jugador cambia entre hub y run y los items no. Se vacía al arrancar Play
    /// (Domain Reload desactivado).
    /// </summary>
    public static class CombatModifiers
    {
        private static readonly Dictionary<object, Func<Health, float>> HitBonuses = new();
        private static readonly Dictionary<object, float> SlowVulnerabilities = new();
        private static readonly Dictionary<object, float> PierceOnKillDelays = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HitBonuses.Clear();
            SlowVulnerabilities.Clear();
            PierceOnKillDelays.Clear();
        }

        /// <summary>Quita todo lo que aportaba <paramref name="source"/>. Seguro de llamar de más.</summary>
        public static void Remove(object source)
        {
            HitBonuses.Remove(source);
            SlowVulnerabilities.Remove(source);
            PierceOnKillDelays.Remove(source);
        }

        // -- Daño plano extra por golpe (Anillo) ---------------------------------------------------

        /// <summary>
        /// Daño que se suma a cada golpe del jugador, calculado por objetivo (p. ej. "8% de su vida
        /// máxima si está ralentizado").
        /// </summary>
        public static void SetHitBonus(object source, Func<Health, float> bonus) => HitBonuses[source] = bonus;

        public static float HitBonus(Health target)
        {
            if (HitBonuses.Count == 0 || target == null) return 0f;

            float total = 0f;
            foreach (var bonus in HitBonuses.Values) total += Mathf.Max(0f, bonus(target));
            return total;
        }

        // -- Ralentizado = expuesto (Grimorio) -----------------------------------------------------

        /// <summary>Daño extra que recibe un enemigo mientras está ralentizado: 0.3 = +30%.</summary>
        public static void SetSlowVulnerability(object source, float bonus) => SlowVulnerabilities[source] = bonus;

        /// <summary>Agregado de todas las fuentes, multiplicativo: dos +30% dan +69%.</summary>
        public static float SlowVulnerability()
        {
            float m = 1f;
            foreach (float v in SlowVulnerabilities.Values) m *= 1f + Mathf.Max(0f, v);
            return m - 1f;
        }

        // -- Proyectil que sigue tras matar (Capa) -------------------------------------------------

        /// <summary>
        /// Al matar, el proyectil desaparece, espera <paramref name="delay"/> segundos y reaparece
        /// en el cuerpo del muerto con la misma dirección y velocidad.
        /// </summary>
        public static void SetPierceOnKill(object source, float delay) => PierceOnKillDelays[source] = delay;

        /// <summary>true si algún item da "atravesar al matar"; <paramref name="delay"/> = la espera más corta.</summary>
        public static bool PierceOnKill(out float delay)
        {
            delay = 0f;
            if (PierceOnKillDelays.Count == 0) return false;

            delay = float.MaxValue;
            foreach (float d in PierceOnKillDelays.Values) delay = Mathf.Min(delay, Mathf.Max(0f, d));
            return true;
        }
    }
}
