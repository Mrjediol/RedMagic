using System;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>Qué dispara un movimiento defensivo.</summary>
    public enum DefenceTrigger
    {
        /// <summary>El objetivo se acerca a menos de <see cref="EnemyDefence.distance"/>.</summary>
        TargetNear,

        /// <summary>Al recibir un golpe.</summary>
        OnHit,

        /// <summary>Al bajar de <see cref="EnemyDefence.healthFraction"/> de vida (una vez por vida).</summary>
        LowHealth,
    }

    /// <summary>Cómo se aparta.</summary>
    public enum DefenceMove
    {
        /// <summary>Sale disparado en línea recta, alejándose, y frena.</summary>
        DashAway,

        /// <summary>Salto hacia atrás: un arco (el de suelo salta de verdad, el volador lo dibuja).</summary>
        HopBack,

        /// <summary>De lado respecto al objetivo, sin alejarse (voladores). En el suelo no hay "lado": se aparta como DashAway.</summary>
        Strafe,
    }

    /// <summary>
    /// Un movimiento defensivo de un enemigo: <b>cuándo</b> (el disparador y su probabilidad) y
    /// <b>cómo</b> se aparta. Son datos; quien decide es <see cref="EnemyBrain"/> y quien calcula el
    /// movimiento es <see cref="DefenceMotion"/>, el mismo para cualquier enemigo.
    /// </summary>
    [Serializable]
    public class EnemyDefence
    {
        public DefenceTrigger trigger = DefenceTrigger.TargetNear;

        [Tooltip("TargetNear: salta cuando el objetivo está a menos de esta distancia.")]
        [Min(0f)] public float distance = 2.5f;

        [Tooltip("LowHealth: salta al bajar de esta fracción de vida (0.35 = 35%).")]
        [Range(0f, 1f)] public float healthFraction = 0.35f;

        [Tooltip("Probabilidad de hacerlo cuando se cumple el disparador.")]
        [Range(0f, 1f)] public float chance = 1f;

        [Tooltip("Segundos hasta que puede volver a hacerlo.")]
        [Min(0f)] public float cooldown = 3f;

        public DefenceMove move = DefenceMove.DashAway;

        [Tooltip("Unidades que recorre.")]
        [Min(0f)] public float moveDistance = 3f;

        [Tooltip("Segundos que tarda (HopBack en el suelo: lo que dure el salto).")]
        [Min(0.05f)] public float moveDuration = 0.3f;

        [Tooltip("HopBack: altura del salto.")]
        [Min(0f)] public float hopHeight = 1.2f;

        [Tooltip("Gesto del cuerpo (RigGesture o estado del Animator) que mantiene mientras se aparta: " +
                 "su pose de carga, sus efectos y sus sprites. Vacío = sin gesto.")]
        [GestureName] public string gesture = "";

        [Tooltip("Segundos quieto antes de moverse, con el gesto ya puesto: es el aviso para el jugador.")]
        [Min(0f)] public float anticipation = 0.12f;

        public EnemyDefence Clone() => (EnemyDefence)MemberwiseClone();
    }

    /// <summary>
    /// El movimiento de un <see cref="EnemyDefence"/>, genérico: sirve a cualquier enemigo (de suelo o
    /// volador) y no sabe nada de cerebros ni de animación. Sólo números: cuánto dura y qué velocidad
    /// toca en cada instante. El perfil del impulso es lineal decreciente (sale rápido y frena), cuya
    /// integral es exactamente <see cref="EnemyDefence.moveDistance"/>.
    /// </summary>
    public static class DefenceMotion
    {
        /// <summary>Velocidad vertical inicial de un salto de altura <paramref name="height"/> con esa gravedad.</summary>
        public static float HopImpulse(float height, float gravity) =>
            Mathf.Sqrt(2f * Mathf.Max(0.01f, gravity) * Mathf.Max(0f, height));

        /// <summary>
        /// Segundos que dura el desplazamiento. Un salto de un enemigo de suelo dura lo que tarde en
        /// subir y bajar (<paramref name="gravity"/> = gravedad efectiva, positiva); lo demás, <c>moveDuration</c>.
        /// </summary>
        public static float Duration(EnemyDefence d, bool flies, float gravity)
        {
            if (d.move == DefenceMove.HopBack && !flies && gravity > 0.01f && d.hopHeight > 0.01f)
                return Mathf.Max(0.05f, 2f * HopImpulse(d.hopHeight, gravity) / gravity);
            return Mathf.Max(0.05f, d.moveDuration);
        }

        /// <summary>
        /// Velocidad deseada en el instante <paramref name="k"/> (0–1 del desplazamiento).
        /// <paramref name="away"/> = dirección que se aleja del objetivo (normalizada).
        /// <paramref name="side"/> = ±1, hacia qué lado va un <see cref="DefenceMove.Strafe"/>.
        /// Para un enemigo de suelo sólo cuenta la X (la Y la pone la física, o el impulso del salto).
        /// </summary>
        public static Vector2 Velocity(EnemyDefence d, float k, float duration, Vector2 away, bool flies, int side)
        {
            k = Mathf.Clamp01(k);
            float burst = 2f * d.moveDistance / duration * (1f - k);   // sale rápido y frena
            float steady = d.moveDistance / duration;

            if (!flies)
            {
                float x = away.x >= 0f ? 1f : -1f;
                return new Vector2(x * (d.move == DefenceMove.HopBack ? steady : burst), 0f);
            }

            switch (d.move)
            {
                case DefenceMove.HopBack:
                    // El volador no salta: dibuja el arco (sube en la primera mitad, baja en la segunda).
                    float arc = Mathf.PI * d.hopHeight / duration * Mathf.Cos(Mathf.PI * k);
                    return away * steady + Vector2.up * arc;

                case DefenceMove.Strafe:
                    return new Vector2(-away.y, away.x) * (side >= 0 ? 1f : -1f) * burst;

                default:
                    return away * burst;
            }
        }
    }
}
