using System.Collections;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// La <b>guadaña</b>: un brazo larguísimo que gira anclado al jefe y barre la arena de arriba
    /// abajo (o de abajo arriba). El trabajo lo hace <see cref="BossSweepBeam"/>; este asset sólo
    /// decide desde qué ángulo hasta cuál, cuántas pasadas y a qué velocidad.
    ///
    /// <b>Por qué existe pudiendo usar <see cref="ShockwaveAttack"/>:</b> la onda es una franja de
    /// altura fija que viaja en horizontal, así que se resuelve con una decisión — saltar o no. La
    /// guadaña pivota, de modo que su altura en tu posición depende de la <b>distancia</b>: pegado
    /// al jefe cae a plomo, y a media arena llega tarde pero pasa deprisa. Son dos preguntas
    /// distintas ("¿a qué altura estoy?" contra "¿a qué distancia estoy?") y por eso conviven.
    ///
    /// El aviso dibuja la recta de salida y unas marcas tenues por el recorrido: se ve de dónde
    /// sale y hacia dónde va antes de que nada haga daño.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Sweep Beam Attack", fileName = "BossAttack_Sweep")]
    public class SweepBeamAttack : BossAttack
    {
        [Header("Pivote")]
        [Tooltip("Punto del que sale el brazo, respecto al jefe. La Y es la clave del ataque: " +
                 "cuanto más alto, más tarda la guadaña en llegar al suelo lejos del jefe.")]
        [SerializeField] private Vector2 pivotOffset = new Vector2(0f, 3.2f);

        [Header("Barrido (grados: 0 = horizontal, 90 = arriba)")]
        [Tooltip("Ángulo de salida. 80 = casi vertical.")]
        [SerializeField] private float fromAngle = 80f;

        [Tooltip("Ángulo final. Un pelín negativo hace que termine clavada en el suelo.")]
        [SerializeField] private float toAngle = -6f;

        [Tooltip("Segundos que tarda una pasada. Es el mando de dificultad principal.")]
        [Min(0.1f)]
        [SerializeField] private float sweepSeconds = 0.95f;

        [Tooltip("Apunta el barrido al lado en el que está el jugador. Apagado, siempre barre a la " +
                 "derecha del jefe.")]
        [SerializeField] private bool sweepTowardPlayer = true;

        [Tooltip("Barre a los dos lados a la vez. Quita la salida fácil de 'ponerse detrás' y " +
                 "convierte el ataque en una pregunta pura de distancia.")]
        [SerializeField] private bool bothSides;

        [Header("Pasadas")]
        [Min(1)]
        [SerializeField] private int sweeps = 1;

        [Min(0.05f)]
        [SerializeField] private float timeBetweenSweeps = 0.6f;

        [Tooltip("Las pasadas pares vuelven del ángulo final al inicial. Encadenar ida y vuelta " +
                 "castiga quedarse quieto justo donde acaba de pasar el filo.")]
        [SerializeField] private bool returnSweep = true;

        [Header("Filo")]
        [Tooltip("Largo del brazo. 0 = de sobra para cruzar media arena.")]
        [Min(0f)]
        [SerializeField] private float length;

        [Tooltip("Grosor del filo: cuánto margen hay al saltarlo o pasarle por debajo.")]
        [Min(0.2f)]
        [SerializeField] private float width = 0.85f;

        public override string ShortStats() =>
            $"{Damage:0} dmg · {fromAngle:0}°→{toAngle:0}° en {sweepSeconds:0.00}s · {sweeps} pasada(s)";

        public override void OnTelegraph(BossContext ctx)
        {
            float duration = ctx.Scaled(Telegraph);

            foreach (int side in Sides(ctx))
            {
                Vector2 pivot = Pivot(ctx, side);
                float length = Length(ctx);

                // La recta de salida, bien visible: es donde va a nacer el filo.
                WarnRay(ctx, pivot, Angle(fromAngle, side), length, duration, 1f);

                // Tres marcas tenues por el recorrido: dicen hacia dónde gira sin tapar la arena.
                for (int i = 1; i <= 3; i++)
                {
                    float t = i / 4f;
                    WarnRay(ctx, pivot, Angle(Mathf.Lerp(fromAngle, toAngle, t), side), length,
                            duration * 0.8f, 0.32f);
                }
            }
        }

        public override IEnumerator Run(BossContext ctx)
        {
            for (int sweep = 0; sweep < sweeps; sweep++)
            {
                if (!ctx.IsValid) yield break;

                // La ida y la vuelta alternan, así que el filo nunca "reaparece" en el ángulo de
                // salida: sigue justo donde se quedó la pasada anterior.
                bool back = returnSweep && (sweep & 1) == 1;
                float from = back ? toAngle : fromAngle;
                float to = back ? fromAngle : toAngle;

                Impact();

                foreach (int side in Sides(ctx))
                {
                    // Daño sin escalar: lo aplica AbilityHit dentro del barrido, igual que en la
                    // onda de choque. Pasar aquí ScaledDamage lo multiplicaría dos veces por fase.
                    BossSweepBeam.Spawn(ctx, Pivot(ctx, side), Angle(from, side), Angle(to, side),
                                        ctx.Scaled(sweepSeconds), Length(ctx), width,
                                        Damage, KnockbackMultiplier);
                }

                // La espera incluye la pasada: el ataque no termina hasta que el filo se apaga.
                yield return new WaitForSeconds(ctx.Scaled(sweepSeconds) +
                                                (sweep < sweeps - 1 ? ctx.Scaled(timeBetweenSweeps) : 0f));
            }
        }

        // ------------------------------------------------------------------ geometría

        // Arrays fijos: los ataques se lanzan constantemente y devolver un 'new[]' por pasada sería
        // basura del recolector por nada.
        private static readonly int[] BothSidesLanes = { 1, -1 };
        private static readonly int[] RightLane = { 1 };
        private static readonly int[] LeftLane = { -1 };

        /// <summary>Lados que barre esta pasada: uno (hacia el jugador) o los dos.</summary>
        private int[] Sides(in BossContext ctx)
        {
            if (bothSides) return BothSidesLanes;

            int side = sweepTowardPlayer ? ctx.Facing : 1;
            return side < 0 ? LeftLane : RightLane;
        }

        private Vector2 Pivot(in BossContext ctx, int side) =>
            ctx.Origin + new Vector2(pivotOffset.x * side, pivotOffset.y);

        /// <summary>Espeja el ángulo al lado izquierdo. 30° a la derecha es 150° a la izquierda.</summary>
        private static float Angle(float angle, int side) => side < 0 ? 180f - angle : angle;

        private float Length(in BossContext ctx) =>
            length > 0f ? length : ctx.ArenaHalfWidth + 2f;

        /// <summary>Marca de aviso con forma de recta desde el pivote, ya rotada.</summary>
        private void WarnRay(in BossContext ctx, Vector2 pivot, float angle, float length,
                             float duration, float strength)
        {
            float radians = angle * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            Warn(ctx, pivot + direction * (length * 0.5f), new Vector2(length, width * strength),
                 duration, angle, strength);
        }
    }
}
