using System;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>Qué tipo de golpe del jugador es. Decide qué reglas de sinergia le tocan.</summary>
    public enum HitKind
    {
        /// <summary>Proyectil de arma (<see cref="ShotProjectile"/>, incluida su explosión de impacto).</summary>
        Projectile,
        /// <summary>Haz de arma (<see cref="ShotBeam"/>).</summary>
        Beam,
        /// <summary>Espada básica (<c>PlayerAttack</c>).</summary>
        Melee,
        /// <summary>Habilidad del sistema antiguo (<c>AbilityHit</c>).</summary>
        Ability,
        /// <summary>Estallido de un item o sinergia (<see cref="IceBurst"/>).</summary>
        Burst,
    }

    /// <summary>Un golpe del jugador que ha entrado.</summary>
    public readonly struct HitInfo
    {
        public readonly Health Target;
        public readonly float Damage;
        public readonly HitKind Kind;

        public HitInfo(Health target, float damage, HitKind kind)
        {
            Target = target;
            Damage = damage;
            Kind = kind;
        }
    }

    /// <summary>Una baja hecha por el jugador.</summary>
    public readonly struct KillInfo
    {
        public readonly Health Victim;
        /// <summary>Centro del cuerpo (el de su collider), no el pivote de los pies.</summary>
        public readonly Vector2 Position;
        /// <summary>Estaba ralentizado cuando recibió el golpe mortal.</summary>
        public readonly bool WasSlowed;
        public readonly HitKind Kind;

        public KillInfo(Health victim, Vector2 position, bool wasSlowed, HitKind kind)
        {
            Victim = victim;
            Position = position;
            WasSlowed = wasSlowed;
            Kind = kind;
        }
    }

    /// <summary>
    /// <b>Único punto por el que el jugador hace daño a un enemigo.</b> Los proyectiles y haces de
    /// arma, la espada y las habilidades antiguas llaman a <see cref="Deal"/> en vez de a
    /// <c>Health.TakeDamage</c>, y aquí se resuelve todo lo que depende de la build:
    /// <list type="number">
    /// <item>Bono plano por golpe de los items (<see cref="CombatModifiers.HitBonus"/>: el Anillo).</item>
    /// <item>El golpe en sí (<c>Health.TakeDamage</c>, que aplica armadura y el
    /// <see cref="Health.StatusDamageMultiplier"/> de la ralentización).</item>
    /// <item>Hielo 2: un proyectil o haz ralentiza al que sobrevive; Hielo 4 y el Grimorio lo dejan
    /// además expuesto mientras dure.</item>
    /// <item>Los avisos <see cref="Landed"/> y <see cref="Killed"/>, de los que cuelgan todos los
    /// efectos "al matar" (Bastón, Yelmo, Hielo 6, Reset 2, Vampirismo 2…).</item>
    /// </list>
    /// Así un efecto nuevo "al golpear / al matar" se suscribe aquí y no necesita tocar ningún arma.
    /// </summary>
    public static class PlayerHit
    {
        /// <summary>Un golpe del jugador ha entrado (el objetivo puede haber muerto con él).</summary>
        public static event Action<HitInfo> Landed;

        /// <summary>El jugador ha matado a un enemigo. Se dispara después de <see cref="Landed"/>.</summary>
        public static event Action<KillInfo> Killed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Landed = null;
            Killed = null;
        }

        /// <summary>
        /// Golpea a <paramref name="target"/>. Devuelve true si el golpe entró; <paramref name="killed"/>
        /// dice si lo ha matado. El filtrado de bandos es cosa de quien llama (cada arma ya lo hace).
        /// </summary>
        public static bool Deal(Health target, float amount, Vector2 sourcePosition, float knockbackMultiplier,
                                HitKind kind, out bool killed)
        {
            killed = false;
            if (target == null || target.IsDead || amount <= 0f) return false;

            amount += CombatModifiers.HitBonus(target);

            bool wasSlowed = SlowStatus.IsSlowedTarget(target);
            if (!target.TakeDamage(amount, sourcePosition, knockbackMultiplier)) return false;

            if (!target.IsDead && SlowsOnHit(kind) && SynergyActive(BuildTag.Ice, 2))
                ApplySlow(target);

            Landed?.Invoke(new HitInfo(target, amount, kind));

            if (target.IsDead)
            {
                killed = true;
                Killed?.Invoke(new KillInfo(target, BodyCenter(target), wasSlowed, kind));
            }

            return true;
        }

        /// <inheritdoc cref="Deal(Health, float, Vector2, float, HitKind, out bool)"/>
        public static bool Deal(Health target, float amount, Vector2 sourcePosition, float knockbackMultiplier,
                                HitKind kind) =>
            Deal(target, amount, sourcePosition, knockbackMultiplier, kind, out _);

        /// <summary>
        /// Ralentiza con los valores de <see cref="SynergyTuning"/>: fuerza, duración y tinte, más el
        /// daño extra de Hielo 4 y el de los items que exponen al ralentizado (Grimorio).
        /// </summary>
        public static SlowStatus ApplySlow(Health target)
        {
            var t = SynergyConfig.CurrentTuning;

            float vulnerability = (1f + (SynergyActive(BuildTag.Ice, 4) ? t.ice4DamageTakenBonus : 0f))
                                  * (1f + CombatModifiers.SlowVulnerability()) - 1f;

            return SlowStatus.Apply(target, t.slowStrength, t.slowDuration, vulnerability,
                                    t.slowTint, t.fullTintAtSlow);
        }

        /// <summary>true si el umbral de sinergia está activo en la build de la run.</summary>
        public static bool SynergyActive(BuildTag tag, int threshold)
        {
            var loadout = WeaponLoadout.Instance;
            return loadout != null && loadout.Synergy != null && loadout.Synergy.IsThresholdActive(tag, threshold);
        }

        /// <summary>Hielo 2 sólo ralentiza con lo que sale del arma (proyectiles y haces).</summary>
        private static bool SlowsOnHit(HitKind kind) => kind == HitKind.Projectile || kind == HitKind.Beam;

        /// <summary>Centro del collider del cuerpo; el pivote de un enemigo suele estar en los pies.</summary>
        public static Vector2 BodyCenter(Component body)
        {
            if (body == null) return Vector2.zero;
            var collider = body.GetComponentInChildren<Collider2D>();
            return collider != null ? (Vector2)collider.bounds.center : (Vector2)body.transform.position;
        }
    }
}
