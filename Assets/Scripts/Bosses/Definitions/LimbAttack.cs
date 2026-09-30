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
        /// <paramref name="to"/> en <paramref name="seconds"/> (ya escalados), con easing, girándolos
        /// de <paramref name="spinFrom"/> a <paramref name="spinTo"/>. Con <paramref name="hit"/> daña
        /// al tocar (una vez).
        /// </summary>
        protected IEnumerator Fly(BossContext ctx, BossLimbs limbs, Transform[] parts, Vector2[] from,
                                  Vector2[] to, float seconds, Ease ease, float spinFrom, float spinTo,
                                  HitOnce hit = null)
        {
            seconds = Mathf.Max(0.01f, seconds);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!ctx.IsValid) yield break;

                float k = Apply(ease, t / seconds);
                for (int i = 0; i < parts.Length; i++)
                {
                    Vector2 at = Vector2.LerpUnclamped(from[i], to[i], k);
                    limbs.DriveCenter(parts[i], at, Mathf.LerpUnclamped(spinFrom, spinTo, k) * SpinSide(ctx, i));
                    if (hit != null) Contact(ctx, at, limbs.ContactRadius, hit);
                }

                yield return null;
            }

            for (int i = 0; i < parts.Length; i++) limbs.DriveCenter(parts[i], to[i], spinTo * SpinSide(ctx, i));
        }

        /// <summary>Mantiene las piezas en <paramref name="at"/> (temblando) <paramref name="seconds"/>.</summary>
        protected IEnumerator Hold(BossContext ctx, BossLimbs limbs, Transform[] parts, Vector2[] at,
                                   float seconds, float spin, float shake)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!ctx.IsValid) yield break;

                for (int i = 0; i < parts.Length; i++)
                {
                    Vector2 jitter = shake > 0f ? Random.insideUnitCircle * shake : Vector2.zero;
                    limbs.DriveCenter(parts[i], at[i] + jitter, spin * SpinSide(ctx, i));
                }

                yield return null;
            }
        }

        /// <summary>
        /// Vuelve volando a su sitio en el cuerpo (que puede estar moviéndose) y lo suelta al rig
        /// para el último tramo.
        /// </summary>
        protected IEnumerator FlyHome(BossContext ctx, BossLimbs limbs, Transform part, float seconds,
                                      HitOnce hit = null)
        {
            Vector2 from = limbs.Center(part);
            seconds = Mathf.Max(0.01f, seconds);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!ctx.IsValid) yield break;

                Vector2 at = Vector2.Lerp(from, limbs.HomeCenter(part), Apply(Ease.InOut, t / seconds));
                limbs.DriveCenter(part, at, 0f);
                if (hit != null) Contact(ctx, at, limbs.ContactRadius, hit);
                yield return null;
            }

            limbs.Release(part, 0.12f);
        }

        /// <summary>Las piezas 0/1 giran en sentidos opuestos: un puño a cada lado se lee simétrico.</summary>
        private static float SpinSide(in BossContext ctx, int index) => index % 2 == 0 ? 1f : -1f;

        // ============================================================ daño y efectos

        protected void Contact(in BossContext ctx, Vector2 center, float radius, HitOnce hit)
        {
            if (hit.Landed) return;
            if (AbilityHit.DamageCircle(ctx.Ability, center, radius, ScaledDamage(ctx), KnockbackMultiplier) > 0)
                hit.Landed = true;
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
