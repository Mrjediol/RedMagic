using System.Collections;
using RedMagic.Abilities;
using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Base de los ataques que <b>lanzan piezas del propio cuerpo</b> (un puño que sale volando, dos
    /// que se estampan contra el suelo). Sólo tienen sentido en un jefe hecho por partes
    /// (<see cref="BossLimbs"/> + un <c>RigDriver</c>); en uno de un solo sprite caen a una versión
    /// sin pieza (proyectil / impacto marcado) para que el asset nunca quede mudo.
    ///
    /// Las trayectorias son de aquí — gameplay, iguales con cualquier motor de animación — y el
    /// rig sólo se encarga de devolver la pieza al cuerpo. Todos los tiempos pasan por
    /// <see cref="BossContext.Scaled"/>, así que la fase 2 los acelera sin más assets.
    /// </summary>
    public abstract class LimbAttack : BossAttack
    {
        [Header("Extremidades · impacto")]
        [ArtSlot("Impacto de la pieza", ArtSlotKind.Fx, "destello de color")]
        [Tooltip("Efecto de un solo uso (VfxOneShot) en cada impacto. Vacío = destello del color de la fase.")]
        [SerializeField] private GameObject impactFxPrefab;

        [Tooltip("Ancho del efecto de impacto (0 = escala del prefab).")]
        [Min(0f)]
        [SerializeField] private float impactFxWidth = 2.2f;

        [Tooltip("Temblor de la pieza mientras espera para golpear (unidades de mundo).")]
        [Min(0f)]
        [SerializeField] protected float tremble = 0.06f;

        [Header("Extremidades · aviso")]
        [Tooltip("Largo de la flecha de aviso, en veces el tamaño que se ve de la pieza.")]
        [Min(0.2f)]
        [SerializeField] private float arrowLength = 1.6f;

        [Tooltip("Marca del destino, en veces el tamaño que se ve de la pieza.")]
        [Min(0.2f)]
        [SerializeField] private float targetMarkScale = 0.9f;

        [Tooltip("Largo mínimo y máximo de la flecha (unidades de mundo). Proporcional a la pieza, pero " +
                 "nunca tan pequeña que no se vea ni tan grande que tape la pantalla.")]
        [SerializeField] private Vector2 arrowLengthRange = new Vector2(2.2f, 5f);

        [Tooltip("Tamaño mínimo y máximo de la marca del destino (unidades de mundo).")]
        [SerializeField] private Vector2 targetMarkRange = new Vector2(1.1f, 2.5f);

        /// <summary>Las extremidades del jefe, o null si no tiene (entonces toca la versión sin pieza).</summary>
        protected static BossLimbs Limbs(in BossContext ctx)
        {
            var limbs = ctx.Boss != null ? ctx.Boss.GetComponent<BossLimbs>() : null;
            return limbs != null && limbs.Usable ? limbs : null;
        }

        /// <summary>Marca de "ya ha pegado" de un vuelo: cada vuelo daña una sola vez.</summary>
        protected sealed class HitOnce
        {
            public bool Landed;
        }

        // ============================================================ movimiento

        /// <summary>
        /// Lleva los centros de <paramref name="parts"/> de <paramref name="from"/> a
        /// <paramref name="to"/> en <paramref name="seconds"/> (ya escalados), con easing. Cada pieza
        /// gira para que su parte útil (los nudillos, <c>RigPart.pointAngle</c>) mire hacia
        /// <paramref name="aim"/>[i] (dirección del mundo; null = su orientación de reposo); el giro
        /// se completa en la primera mitad del tramo, así un puño lanzado ya va de frente al salir.
        /// Con <paramref name="hit"/> daña al tocar (una vez).
        /// </summary>
        protected IEnumerator Fly(BossContext ctx, BossLimbs limbs, Transform[] parts, Vector2[] from,
                                  Vector2[] to, float seconds, Ease ease, Vector2?[] aim, HitOnce hit = null,
                                  float contactRadius = 0f)
        {
            seconds = Mathf.Max(0.01f, seconds);
            var start = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++) start[i] = limbs.LocalAngle(parts[i]);

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!ctx.IsValid) yield break;

                float k = Apply(ease, t / seconds);
                float turn = Apply(Ease.Out, t / (seconds * 0.5f));
                for (int i = 0; i < parts.Length; i++)
                {
                    Vector2 at = Vector2.LerpUnclamped(from[i], to[i], k);
                    float angle = Mathf.LerpAngle(start[i], TargetAngle(limbs, parts[i], aim, i), turn);
                    limbs.DriveCenter(parts[i], at, angle);
                    // Sólo daña lo que se mueve: en un vuelo compartido, las piezas quietas son decorado.
                    if (hit != null && from[i] != to[i]) Contact(ctx, at, contactRadius > 0f ? contactRadius : limbs.ContactRadius, hit);
                }

                yield return null;
            }

            for (int i = 0; i < parts.Length; i++)
                limbs.DriveCenter(parts[i], to[i], TargetAngle(limbs, parts[i], aim, i));
        }

        /// <summary>Mantiene las piezas en <paramref name="at"/> mirando a <paramref name="aim"/> (temblando) <paramref name="seconds"/>.</summary>
        protected IEnumerator Hold(BossContext ctx, BossLimbs limbs, Transform[] parts, Vector2[] at,
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

        /// <summary>
        /// Vuelve volando a su sitio en el cuerpo (que puede estar moviéndose), girando de vuelta a su
        /// orientación de reposo, y lo suelta al rig para el último tramo.
        /// </summary>
        protected IEnumerator FlyHome(BossContext ctx, BossLimbs limbs, Transform part, float seconds,
                                      HitOnce hit = null)
        {
            Vector2 from = limbs.Center(part);
            float startAngle = limbs.LocalAngle(part);
            seconds = Mathf.Max(0.01f, seconds);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!ctx.IsValid) yield break;

                float k = Apply(Ease.InOut, t / seconds);
                Vector2 at = Vector2.Lerp(from, limbs.HomeCenter(part), k);
                limbs.DriveCenter(part, at, Mathf.LerpAngle(startAngle, limbs.RestAngle(part), k));
                if (hit != null) Contact(ctx, at, limbs.ContactRadius, hit);
                yield return null;
            }

            limbs.Release(part, 0.12f);
        }

        private static float TargetAngle(BossLimbs limbs, Transform part, Vector2?[] aim, int index)
        {
            var direction = aim != null && index < aim.Length ? aim[index] : null;
            return direction.HasValue ? limbs.AngleToward(part, direction.Value) : limbs.RestAngle(part);
        }

        /// <summary>Atajo para escribir las direcciones: <c>Aim(Vector2.right, Vector2.left)</c>.</summary>
        protected static Vector2?[] Aim(params Vector2?[] directions) => directions;

        // ============================================================ daño y efectos

        protected void Contact(in BossContext ctx, Vector2 center, float radius, HitOnce hit)
        {
            if (hit.Landed) return;
            if (AbilityHit.DamageCircle(ctx.Ability, center, radius, ScaledDamage(ctx), KnockbackMultiplier) > 0)
                hit.Landed = true;
        }

        /// <summary>
        /// Aviso <b>proporcional a la pieza</b>: una flecha corta que sale de ella hacia donde va (su largo
        /// y su grosor salen del tamaño que se ve de la pieza, no del recorrido) y una marca del tamaño
        /// de la pieza en el destino. Un cristal avisa con una flecha de cristal; un puño, de puño.
        /// </summary>
        protected void WarnPart(in BossContext ctx, BossLimbs limbs, Transform part, Vector2 direction,
                                Vector2 target, float duration)
        {
            Vector2 size = limbs.VisualSize(part);
            float extent = Mathf.Max(size.x, size.y);
            float thickness = Mathf.Min(size.x, size.y);

            float length = Mathf.Clamp(extent * arrowLength, arrowLengthRange.x, Mathf.Max(arrowLengthRange.x, arrowLengthRange.y));
            float mark = Mathf.Clamp(extent * targetMarkScale, targetMarkRange.x, Mathf.Max(targetMarkRange.x, targetMarkRange.y));

            WarnArrow(ctx, limbs.Center(part), direction, length, thickness, duration, 0.7f);
            Warn(ctx, target, Vector2.one * mark, duration);
        }

        /// <summary>Sonido, sacudida y efecto del impacto.</summary>
        protected void ImpactAt(in BossContext ctx, Vector2 point, Vector2 flashSize)
        {
            Impact();

            if (impactFxPrefab != null)
            {
                var at = new Vector3(point.x, point.y, 0f);
                if (impactFxWidth > 0f) VfxOneShot.SpawnFitWidth(impactFxPrefab, at, impactFxWidth, ctx.Facing);
                else VfxOneShot.Spawn(impactFxPrefab, at, ctx.Facing);
                return;
            }

            var flash = ctx.Accent;
            flash.a = 0.75f;
            AbilityFx.Flash(ctx.FxSprite, point, flashSize, flash, 0.22f, 0f, 1.3f, ctx.Ability.Caster);
        }

        /// <summary>Centro de pieza que no atraviesa el suelo.</summary>
        protected static Vector2 AboveGround(in BossContext ctx, Vector2 point, float radius) =>
            new Vector2(point.x, Mathf.Max(point.y, ctx.GroundY + radius));

        // ============================================================ easing

        protected enum Ease
        {
            Linear,
            In,
            Out,
            InOut,
        }

        protected static float Apply(Ease ease, float x)
        {
            x = Mathf.Clamp01(x);
            switch (ease)
            {
                case Ease.In: return x * x * x;
                case Ease.Out: { float i = 1f - x; return 1f - i * i * i; }
                case Ease.InOut: return x * x * (3f - 2f * x);
                default: return x;
            }
        }
    }
}
