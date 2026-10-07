using System;
using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>Qué clase de golpe se ha intentado bloquear.</summary>
    public enum ParryHitKind
    {
        /// <summary>Un <c>Projectile</c> enemigo (o del entorno, sin dueño).</summary>
        Projectile,
        /// <summary>Un golpe de área de un enemigo o jefe (<c>AbilityHit</c>: espadazo, embestida…).</summary>
        Melee,
    }

    /// <summary>Un golpe enemigo que está a punto de entrar en alguien que puede hacer parry.</summary>
    public readonly struct IncomingHit
    {
        public readonly Health Target;
        public readonly float Damage;
        /// <summary>Quien lo lanza. Null = del entorno (trampa, proyectil sin dueño): no se le devuelve daño.</summary>
        public readonly GameObject Attacker;
        /// <summary>Dónde está el golpe ahora (el proyectil, el centro de la caja de daño).</summary>
        public readonly Vector2 Point;
        public readonly ParryHitKind Kind;

        public IncomingHit(Health target, float damage, GameObject attacker, Vector2 point, ParryHitKind kind)
        {
            Target = target;
            Damage = damage;
            Attacker = attacker;
            Point = point;
            Kind = kind;
        }
    }

    /// <summary>Lo implementa quien puede bloquear golpes (el <c>PlayerParry</c> del jugador).</summary>
    public interface IParryReceiver
    {
        /// <summary>True = golpe bloqueado: quien llama no aplica daño (y un proyectil desaparece).</summary>
        bool TryParry(in IncomingHit hit);
    }

    /// <summary>
    /// <b>Único punto por el que un golpe enemigo pregunta si lo han bloqueado.</b> Los dos caminos
    /// de daño enemigo → jugador (<c>Projectile.HandleHit</c> y <c>AbilityHit.Damage</c>) llaman a
    /// <see cref="TryParry"/> justo antes de <c>Health.TakeDamage</c>; así un ataque nuevo, de
    /// enemigo o de jefe, se puede parar sin tocarlo. El daño por contacto (chocar con el cuerpo) no
    /// pasa por aquí a propósito: no es un ataque.
    ///
    /// <see cref="Succeeded"/> / <see cref="Missed"/> son los ganchos para items y sinergias de parry.
    /// </summary>
    public static class Parry
    {
        /// <summary>Un parry ha bloqueado un golpe.</summary>
        public static event Action<IncomingHit> Succeeded;

        /// <summary>Se cerró la ventana de parry sin bloquear nada (posición del que lo hizo).</summary>
        public static event Action<Health> Missed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Succeeded = null;
            Missed = null;
        }

        /// <summary>
        /// Pregunta a <paramref name="target"/> si bloquea el golpe. False si no puede hacer parry o
        /// no está en ventana: quien llama sigue con el daño normal.
        /// </summary>
        public static bool TryParry(Health target, float damage, GameObject attacker, Vector2 point, ParryHitKind kind)
        {
            if (target == null || target.IsDead) return false;
            if (!target.TryGetComponent(out IParryReceiver receiver)) return false;
            return receiver.TryParry(new IncomingHit(target, damage, attacker, point, kind));
        }

        internal static void RaiseSucceeded(in IncomingHit hit) => Succeeded?.Invoke(hit);

        internal static void RaiseMissed(Health who) => Missed?.Invoke(who);
    }
}
