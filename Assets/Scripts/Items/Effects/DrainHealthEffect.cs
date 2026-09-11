using System;
using System.ComponentModel;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Quita vida al jugador cada cierto tiempo mientras el item está equipado. Usa
    /// <c>Health.Drain</c>: sin i-frames ni animación de golpe (no es un golpe), pero con el número
    /// flotante.
    /// </summary>
    [Serializable, DisplayName("Jugador · Perder vida por segundo")]
    public sealed class DrainHealthEffect : ItemEffect
    {
        [Tooltip("Vida que se pierde en cada intervalo.")]
        [Min(0f)] public float amount = 1f;

        [Tooltip("Segundos entre pérdidas.")]
        [Min(0.05f)] public float interval = 1f;

        [Tooltip("Si está desactivado, nunca baja de 1 de vida.")]
        public bool canKill;

        public override void Tick(ItemEffectContext context, float deltaTime) =>
            context.Every(interval, deltaTime, () => context.PlayerHealth?.Drain(amount, canKill));

        public override string Summary() => $"Pierde {amount:0.##} de vida cada {interval:0.##} s";
    }
}
