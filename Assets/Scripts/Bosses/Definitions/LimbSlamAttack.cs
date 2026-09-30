using System.Collections;
using RedMagic.Abilities;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Mazazos con las extremidades — <b>"¿dónde va a caer, y hacia dónde corre la onda?"</b>. Las
    /// piezas suben, se quedan temblando sobre el punto marcado y se estampan contra el suelo;
    /// cada impacto puede soltar ondas rasantes (proyectiles) hacia los lados que obligan a saltar.
    ///
    /// Dos formas con los mismos campos:
    ///  - <see cref="SlamMode.Together"/>: doble mazazo, las dos a la vez a cada lado del punto
    ///    (deja un hueco en medio: ahí es donde hay que estar).
    ///  - <see cref="SlamMode.Alternating"/>: ráfaga de mazazos alternando pieza, cada uno sobre
    ///    donde está el jugador en ese momento; la anterior vuelve al cuerpo mientras cae la
    ///    siguiente. Castiga quedarse quieto.
    ///
    /// Sin extremidades marca y golpea los mismos puntos (sin pieza visible).
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Limb Slam Attack", fileName = "BossAttack_LimbSlam")]
    public class LimbSlamAttack : LimbAttack
    {
        public enum SlamMode
        {
            Together,
            Alternating,
        }

        [Header("Forma")]
        [SerializeField] private SlamMode mode = SlamMode.Together;

        [Tooltip("Sólo Alternating: cuántos mazazos.")]
        [Min(1)]
        [SerializeField] private int slams = 4;

        [Tooltip("Together: centrado en el jugador (encendido) o en el jefe (apagado). Alternating " +
                 "siempre busca al jugador.")]
        [SerializeField] private bool aimAtPlayer = true;

        [Tooltip("Together: distancia entre los dos impactos.")]
        [Min(0f)]
        [SerializeField] private float spacing = 5f;

        [Header("Tiempos")]
        [Tooltip("Segundos en subir.")]
        [Min(0.05f)]
        [SerializeField] private float riseSeconds = 0.4f;

        [Tooltip("Segundos quieto arriba, temblando sobre la marca: el aviso.")]
        [Min(0f)]
        [SerializeField] private float hangSeconds = 0.35f;

        [Tooltip("Segundos de caída.")]
        [Min(0.02f)]
        [SerializeField] private float dropSeconds = 0.1f;

        [Tooltip("Segundos hundida en el suelo tras el golpe.")]
        [Min(0f)]
        [SerializeField] private float embedSeconds = 0.35f;

        [Tooltip("Segundos de vuelta al cuerpo (lo anima el rig).")]
        [Min(0.05f)]
        [SerializeField] private float returnSeconds = 0.45f;

        [Header("Golpe")]
        [Tooltip("Altura sobre el suelo desde la que cae.")]
        [Min(0.5f)]
        [SerializeField] private float raiseHeight = 4f;

        [Tooltip("Radio del impacto contra el suelo.")]
        [Min(0.1f)]
        [SerializeField] private float impactRadius = 1.6f;

        [Header("Onda rasante (opcional)")]
        [Tooltip("Ondas por impacto: 0 = ninguna, 1 = hacia fuera, 2 = a los dos lados.")]
        [Range(0, 2)]
        [SerializeField] private int wavesPerImpact = 1;

        [Tooltip("Daño de la onda. 0 = el mismo que el golpe.")]
        [Min(0f)]
        [SerializeField] private float waveDamage = 10f;

        [ArtSlot("Onda rasante", ArtSlotKind.Projectile, "bola aplastada del color de la fase")]
        [SerializeField] private ProjectileSpec wave = new ProjectileSpec
        {
            speed = 9f,
            lifetime = 1.4f,
            size = new Vector2(1.4f, 0.6f),
        };

        public override string ShortStats() =>
            $"{Damage:0} dmg · {(mode == SlamMode.Together ? "doble" : $"{slams} alternos")} · radio {impactRadius:0.#}";

        public override IEnumerator Run(BossContext ctx)
        {
            var limbs = Limbs(ctx);

            if (mode == SlamMode.Together)
            {
                float centerX = aimAtPlayer ? ctx.PlayerPosition.x : ctx.Origin.x;
                var points = new[] { centerX - spacing * 0.5f, centerX + spacing * 0.5f };
                yield return Slam(ctx, limbs, points, new[] { 0, 1 });
                yield break;
            }

            for (int i = 0; i < slams; i++)
            {
                if (!ctx.IsValid) yield break;
                yield return Slam(ctx, limbs, new[] { ctx.PlayerPosition.x }, new[] { i % 2 });
            }
        }

        /// <summary>Un golpe con una o varias piezas a la vez, en las X dadas.</summary>
        private IEnumerator Slam(BossContext ctx, BossLimbs limbs, float[] xs, int[] limbIndices)
        {
            float radius = limbs != null ? limbs.ContactRadius : 0.8f;
            int count = xs.Length;

            var floors = new float[count];
            var ground = new Vector2[count];
            var top = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                // Cae sobre la superficie que pisa el jugador (una cornisa, el puente), no sobre el suelo
                // del jefe, y se coloca encima de él antes de caer.
                float x = Mathf.Clamp(xs[i], ctx.ArenaMinX, ctx.ArenaMaxX);
                floors[i] = aimAtPlayer || mode == SlamMode.Alternating ? ctx.PlayerSurfaceAt(x) : ctx.SurfaceBelow(x, ctx.Origin.y + 0.3f);
                ground[i] = new Vector2(x, floors[i] + radius);
                top[i] = new Vector2(x, Mathf.Max(floors[i], ctx.PlayerPosition.y) + raiseHeight);
            }

            float rise = ctx.Scaled(riseSeconds), hang = ctx.Scaled(hangSeconds), drop = ctx.Scaled(dropSeconds);
            for (int i = 0; i < count; i++)
                WarnCircle(ctx, new Vector2(ground[i].x, floors[i]), impactRadius, rise + hang + drop);

            Transform[] parts = null;
            if (limbs != null)
            {
                parts = new Transform[count];
                var from = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    parts[i] = limbs.Get(limbIndices[i] % Mathf.Max(1, limbs.Count));
                    from[i] = limbs.Center(parts[i]);
                }

                // Sube, espera temblando sobre la marca y cae de golpe, siempre con los nudillos abajo.
                var down = new Vector2?[count];
                for (int i = 0; i < count; i++) down[i] = Vector2.down;

                yield return Fly(ctx, limbs, parts, from, top, rise, Ease.Out, down);
                yield return Hold(ctx, limbs, parts, top, hang, down, tremble);
                yield return Fly(ctx, limbs, parts, top, ground, drop, Ease.In, down);
            }
            else
            {
                yield return new WaitForSeconds(rise + hang + drop);
            }

            if (!ctx.IsValid) yield break;

            for (int i = 0; i < count; i++)
            {
                var point = new Vector2(ground[i].x, floors[i]);
                AbilityHit.DamageCircle(ctx.Ability, point, impactRadius, ScaledDamage(ctx), KnockbackMultiplier);
                BossHitboxDebug.Circle(point, impactRadius, new Color(1f, 0.3f, 0.2f, 1f));
                ImpactAt(ctx, point + Vector2.up * 0.4f, new Vector2(impactRadius * 2f, impactRadius));
                LaunchWaves(ctx, point, i, count);
            }

            if (parts == null) yield break;

            yield return Hold(ctx, limbs, parts, ground, ctx.Scaled(embedSeconds), null, 0f);
            foreach (var part in parts) limbs.Release(part, ctx.Scaled(returnSeconds));
        }

        /// <summary>Ondas que corren por el suelo desde el impacto: hacia fuera y, con 2, también hacia dentro.</summary>
        private void LaunchWaves(in BossContext ctx, Vector2 point, int index, int count)
        {
            if (wavesPerImpact <= 0) return;

            // Hacia fuera: con dos impactos, cada uno hacia su lado; con uno, lejos del jefe.
            float outward = count > 1 ? (index == 0 ? -1f : 1f)
                                      : (point.x >= ctx.Origin.x ? 1f : -1f);

            float damage = waveDamage > 0f ? waveDamage * ctx.Ability.DamageScale : ScaledDamage(ctx);
            Vector2 origin = point + Vector2.up * (wave.size.y * 0.5f);

            ProjectileFactory.Spawn(ctx.Ability, wave, origin, new Vector2(outward, 0f), damage,
                                    KnockbackMultiplier, ctx.FxSprite, ctx.Accent);
            if (wavesPerImpact > 1)
                ProjectileFactory.Spawn(ctx.Ability, wave, origin, new Vector2(-outward, 0f), damage,
                                        KnockbackMultiplier, ctx.FxSprite, ctx.Accent);
        }
    }
}
