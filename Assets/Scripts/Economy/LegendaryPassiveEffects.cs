using System;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Suma el efecto real de todas las pasivas legendarias desbloqueadas (nivel × <see
    /// cref="LegendaryPassive.baseValue"/>) y lo deja en un sitio estático que cualquier sistema
    /// puede leer — mismo espíritu que <see cref="PlayerStats"/>: el jugador cambia de instancia
    /// entre hub y run, así que el bono no puede vivir en un componente suyo, tiene que sobrevivirlo.
    ///
    /// <see cref="Recompute"/> lo llama <see cref="LegendaryPassiveManager"/> cada vez que algo
    /// cambia (desbloqueo, mejora, al arrancar). Velocidad de movimiento y de dash se escriben
    /// directamente en <see cref="PlayerStats"/> aquí mismo — son multiplicadores globales y ya
    /// existe el sitio para ellos. El resto de bonos (vida máx., armadura, daño, cooldown, oro por
    /// baja, regeneración) se deja en propiedades estáticas que el sistema correspondiente lee o
    /// aplica en su propio punto de enganche:
    ///  - Vida máxima / armadura: <c>LegendaryPassiveStatHook</c> en <c>Player.prefab</c>.
    ///  - Daño de ataque: <c>AbilityHit.Damage</c> (el único sitio por el que pasa todo el daño).
    ///  - Cooldown de armas: <c>WeaponUser.Fire</c>.
    ///  - Oro por baja: <see cref="LegendaryPassiveManager"/>, al registrar la baja.
    ///  - Ganancia de XP: <b>TODO</b> — no existe sistema de experiencia/nivel de jugador todavía;
    ///    <see cref="XpGainFraction"/> ya calcula el bono agregado para cuando exista.
    /// </summary>
    public static class LegendaryPassiveEffects
    {
        private static readonly object MoveSpeedKey = new();
        private static readonly object DashSpeedKey = new();

        public static float MaxHealthBonus { get; private set; }
        public static int GoldPerKill { get; private set; }
        public static float AttackDamageBonus { get; private set; }

        /// <summary>Multiplicador de daño recibido ya aplicado (1 = sin armadura, más bajo = más armadura).</summary>
        public static float ArmorDamageMultiplier { get; private set; } = 1f;

        /// <summary>Multiplicador del cooldown de armas ya aplicado (1 = normal, más bajo = más rápido).</summary>
        public static float CooldownMultiplier { get; private set; } = 1f;

        public static float HpRegenPerTick { get; private set; }
        public const float HpRegenTickSeconds = 10f;

        /// <summary>TODO: sin sistema de XP todavía. Fracción agregada (0.05 = +5%) lista para cuando exista.</summary>
        public static float XpGainFraction { get; private set; }

        /// <summary>Se dispara al terminar cada <see cref="Recompute"/>, con los valores ya actualizados.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MaxHealthBonus = 0f;
            GoldPerKill = 0;
            AttackDamageBonus = 0f;
            ArmorDamageMultiplier = 1f;
            CooldownMultiplier = 1f;
            HpRegenPerTick = 0f;
            XpGainFraction = 0f;
            PlayerStats.Remove(MoveSpeedKey);
            PlayerStats.Remove(DashSpeedKey);
            Changed = null;
        }

        /// <summary>
        /// Recalcula todos los bonos desde cero a partir de lo desbloqueado/mejorado en
        /// <see cref="LegendaryPassiveManager"/>. Barato: 9 pasivas como mucho, se puede llamar
        /// cada vez que algo cambia sin preocuparse por el coste.
        /// </summary>
        public static void Recompute()
        {
            var manager = LegendaryPassiveManager.Instance;

            float maxHealth = 0f, moveFrac = 0f, attackDamage = 0f, dashFrac = 0f,
                  cooldownFrac = 0f, armor = 0f, xpFrac = 0f, hpRegen = 0f;
            int goldPerKill = 0;

            if (manager != null)
            {
                foreach (var passive in LegendaryPassiveLibrary.BySlot)
                {
                    if (passive == null || !manager.IsUnlocked(passive)) continue;

                    int level = Mathf.Max(1, manager.GetLevel(passive));
                    float value = passive.baseValue * level;

                    switch (passive.effectKind)
                    {
                        case LegendaryPassiveEffectKind.MaxHealth: maxHealth += value; break;
                        case LegendaryPassiveEffectKind.GoldPerKill: goldPerKill += Mathf.RoundToInt(value); break;
                        case LegendaryPassiveEffectKind.MoveSpeed: moveFrac += value; break;
                        case LegendaryPassiveEffectKind.AttackDamage: attackDamage += value; break;
                        case LegendaryPassiveEffectKind.DashSpeed: dashFrac += value; break;
                        case LegendaryPassiveEffectKind.CooldownReduction: cooldownFrac += value; break;
                        case LegendaryPassiveEffectKind.Armor: armor += value; break;
                        case LegendaryPassiveEffectKind.XpGain: xpFrac += value; break;
                        case LegendaryPassiveEffectKind.HpRegen: hpRegen += value; break;
                    }
                }
            }

            MaxHealthBonus = maxHealth;
            GoldPerKill = goldPerKill;
            AttackDamageBonus = attackDamage;
            HpRegenPerTick = hpRegen;
            XpGainFraction = xpFrac; // TODO: sumar a la ganancia de XP cuando exista ese sistema.

            // 5 % de reducción de daño recibido por punto de armadura, nunca por debajo de 0.
            ArmorDamageMultiplier = Mathf.Clamp01(1f - armor * 0.05f);

            // Nunca por debajo del 10 % del cooldown original: evita un arma a cadencia 0.
            CooldownMultiplier = Mathf.Clamp(1f - cooldownFrac, 0.1f, 1f);

            PlayerStats.SetMultiplier(MoveSpeedKey, PlayerStat.MoveSpeed, 1f + moveFrac);
            PlayerStats.SetMultiplier(DashSpeedKey, PlayerStat.DashDistance, 1f + dashFrac);

            Changed?.Invoke();
        }
    }
}
