using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RedMagic.Bosses
{
    /// <summary>Curvas de los vuelos de pieza (<see cref="LimbMotion.Fly"/>).</summary>
    public enum LimbEase
    {
        Linear,
        In,
        Out,
        InOut,
    }

    /// <summary>
    /// Movimiento de piezas del cuerpo (<see cref="BossLimbs"/>) <b>sin daño</b>: volar, sostener,
    /// girar hacia una dirección. Lo usa <see cref="LimbAttack"/> (los "ataques de partes") y
    /// cualquier ataque clásico que <i>opcionalmente</i> quiera usar las manos si el jefe las tiene
    /// (<see cref="ThrowProjectileAttack"/>), sin heredar de la base de los ataques de partes.
    /// </summary>
    public static class LimbMotion
    {
        /// <summary>Las extremidades del jefe, o null si no tiene (entonces toca la versión sin pieza).</summary>
        public static BossLimbs Limbs(in BossContext ctx)
        {
            var limbs = ctx.Boss != null ? ctx.Boss.GetComponent<BossLimbs>() : null;
            return limbs != null && limbs.Usable ? limbs : null;
        }

        /// <summary>
        /// Lleva los centros de <paramref name="parts"/> de <paramref name="from"/> a
        /// <paramref name="to"/> en <paramref name="seconds"/> (ya escalados), con easing. Cada pieza
        /// gira para que su parte útil (los nudillos, <c>RigPart.pointAngle</c>) mire hacia
        /// <paramref name="aim"/>[i] (dirección del mundo; null = su orientación de reposo); el giro
        /// se completa en la primera mitad del tramo, así un puño lanzado ya va de frente al salir.
        /// <paramref name="onMoving"/> recibe cada frame la posición de cada pieza que se mueve
        /// (las quietas de un vuelo compartido son decorado): ahí engancha el daño quien lo tenga.
        /// </summary>
        public static IEnumerator Fly(BossContext ctx, BossLimbs limbs, Transform[] parts, Vector2[] from,
                                      Vector2[] to, float seconds, LimbEase ease, Vector2?[] aim,
                                      Action<Vector2> onMoving = null)
        {
            seconds = Mathf.Max(0.01f, seconds);
            var start = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++) start[i] = limbs.LocalAngle(parts[i]);

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!ctx.IsValid) yield break;

                float k = Apply(ease, t / seconds);
                float turn = Apply(LimbEase.Out, t / (seconds * 0.5f));
                for (int i = 0; i < parts.Length; i++)
                {
                    Vector2 at = Vector2.LerpUnclamped(from[i], to[i], k);
                    float angle = Mathf.LerpAngle(start[i], TargetAngle(limbs, parts[i], aim, i), turn);
                    limbs.DriveCenter(parts[i], at, angle);
                    if (onMoving != null && from[i] != to[i]) onMoving(at);
                }

                yield return null;
            }

            for (int i = 0; i < parts.Length; i++)
                limbs.DriveCenter(parts[i], to[i], TargetAngle(limbs, parts[i], aim, i));
        }

        /// <summary>Mantiene las piezas en <paramref name="at"/> mirando a <paramref name="aim"/> (temblando) <paramref name="seconds"/>.</summary>
        public static IEnumerator Hold(BossContext ctx, BossLimbs limbs, Transform[] parts, Vector2[] at,
                                       float seconds, Vector2?[] aim, float shake)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!ctx.IsValid) yield break;

                for (int i = 0; i < parts.Length; i++)
                {
                    Vector2 jitter = shake > 0f ? Random.insideUnitCircle * shake : Vector2.zero;
                    limbs.DriveCenter(parts[i], at[i] + jitter, TargetAngle(limbs, parts[i], aim, i));
                }

                yield return null;
            }
        }

        /// <summary>Atajo para escribir las direcciones: <c>Aim(Vector2.right, Vector2.left)</c>.</summary>
        public static Vector2?[] Aim(params Vector2?[] directions) => directions;

        public static float Apply(LimbEase ease, float x)
        {
            x = Mathf.Clamp01(x);
            switch (ease)
            {
                case LimbEase.In: return x * x * x;
                case LimbEase.Out: { float i = 1f - x; return 1f - i * i * i; }
                case LimbEase.InOut: return x * x * (3f - 2f * x);
                default: return x;
            }
        }

        private static float TargetAngle(BossLimbs limbs, Transform part, Vector2?[] aim, int index)
        {
            var direction = aim != null && index < aim.Length ? aim[index] : null;
            return direction.HasValue ? limbs.AngleToward(part, direction.Value) : limbs.RestAngle(part);
        }
    }
}
