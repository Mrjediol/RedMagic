using System.Collections;
using RedMagic.Abilities;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Esquirlas en picado — <b>"¿cuándo viene la siguiente?"</b>. Las piezas pequeñas del jefe
    /// (<see cref="BossLimbs"/> ▸ orbiters: los cristales de la corona) se desprenden, suben y giran
    /// en corro sobre su cabeza (el aviso: se ve cuántas vienen); luego, de una en una, cada una se
    /// orienta hacia el jugador, tiembla con su destino marcado y se lanza en picado a donde estaba,
    /// clavándose. El ritmo — carga, picado, pausa — es lo que se esquiva: cada una obliga a moverse
    /// de nuevo. Al final vuelven todas a su sitio.
    ///
    /// Sin piezas orbitales lanza <see cref="fallbackProjectile"/> con el mismo ritmo.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Orbiter Dive Attack", fileName = "BossAttack_OrbiterDive")]
    public class OrbiterDiveAttack : LimbAttack
    {
        [Header("Corro")]
        [Tooltip("Altura del centro del corro sobre las piezas en reposo.")]
        [SerializeField] private float liftHeight = 2.5f;

        [Tooltip("Radio del corro.")]
        [Min(0.2f)]
        [SerializeField] private float orbitRadius = 1.8f;

        [Tooltip("Grados por segundo que gira el corro.")]
        [SerializeField] private float orbitSpeed = 320f;

        [Tooltip("Segundos en formar el corro.")]
        [Min(0.05f)]
        [SerializeField] private float gatherSeconds = 0.35f;

        [Tooltip("Segundos girando antes del primer picado: el aviso.")]
        [Min(0f)]
        [SerializeField] private float orbitSeconds = 0.8f;

        [Header("Picados")]
        [Tooltip("Segundos que cada pieza apunta y tiembla (con su destino marcado) antes de lanzarse.")]
        [Min(0.05f)]
        [SerializeField] private float diveWindup = 0.35f;

        [Tooltip("Segundos del picado hasta el punto marcado.")]
        [Min(0.05f)]
        [SerializeField] private float diveSeconds = 0.22f;

        [Tooltip("Sigue esta distancia más allá del punto marcado.")]
        [Min(0f)]
        [SerializeField] private float overshoot = 1f;

        [Tooltip("Pausa entre un picado y la carga del siguiente.")]
        [Min(0f)]
        [SerializeField] private float betweenDives = 0.2f;

        [Tooltip("Segundos de vuelta de todas a su sitio (lo anima el rig).")]
        [Min(0.05f)]
        [SerializeField] private float returnSeconds = 0.6f;

        [Header("Sin piezas orbitales")]
        [ArtSlot("Esquirla sin piezas", ArtSlotKind.Projectile, "bola de color de la fase")]
        [SerializeField] private ProjectileSpec fallbackProjectile = new ProjectileSpec
        {
            speed = 14f,
            lifetime = 1.6f,
            size = new Vector2(0.6f, 0.6f),
            muzzleOffset = new Vector2(0f, 6f),
        };

        [Tooltip("Proyectiles sin piezas orbitales.")]
        [Min(1)]
        [SerializeField] private int fallbackShots = 3;

        public override string ShortStats() =>
            $"{Damage:0} dmg · corro {orbitSeconds:0.00}s · picado {diveWindup:0.00}+{diveSeconds:0.00}s";

        public override IEnumerator Run(BossContext ctx)
        {
            var limbs = Limbs(ctx);
            if (limbs == null || limbs.OrbiterCount == 0)
            {
                yield return Fallback(ctx);
                yield break;
            }

            int count = limbs.OrbiterCount;
            var parts = new Transform[count];
            var from = new Vector2[count];
            Vector2 hub = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                parts[i] = limbs.GetOrbiter(i);
                from[i] = limbs.Center(parts[i]);
                hub += from[i];
            }

            hub = hub / count + Vector2.up * liftHeight;
            float radius = limbs.OrbiterRadius;

            // --- corro: suben a su puesto en el círculo, con la punta hacia fuera, y giran.
            float angle = 90f;
            var ring = Ring(hub, angle, count);
            yield return Fly(ctx, limbs, parts, from, ring, ctx.Scaled(gatherSeconds), LimbEase.Out, Outward(hub, ring));

            float orbit = ctx.Scaled(orbitSeconds);
            for (float t = 0f; t < orbit; t += Time.deltaTime)
            {
                if (!ctx.IsValid) yield break;

                angle += orbitSpeed * ctx.SpeedScale * Time.deltaTime;
                ring = Ring(hub, angle, count);
                var outward = Outward(hub, ring);
                for (int i = 0; i < count; i++) limbs.DriveCenter(parts[i], ring[i], AngleFor(limbs, parts[i], outward[i]));
                yield return null;
            }

            // --- picados de uno en uno; las demás esperan en su sitio del corro.
            var at = (Vector2[])ring.Clone();
            var aim = Outward(hub, ring);
            for (int i = 0; i < count; i++)
            {
                if (!ctx.IsValid) yield break;

                Vector2 target = AboveGround(ctx, ctx.PlayerCenter, radius);
                Vector2 dir = target - at[i];
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;
                Vector2 end = target + dir * overshoot;
                bool hitsGround = end.y < ctx.GroundY + radius;
                end = AboveGround(ctx, end, radius);

                float windup = ctx.Scaled(diveWindup);
                WarnPart(ctx, limbs, parts[i], dir, target, windup + ctx.Scaled(diveSeconds));

                aim[i] = dir;
                yield return HoldOne(ctx, limbs, parts, at, aim, i, windup);

                var to = (Vector2[])at.Clone();
                to[i] = end;
                yield return Fly(ctx, limbs, parts, at, to, ctx.Scaled(diveSeconds), LimbEase.In, aim, new HitOnce(), radius);
                if (!ctx.IsValid) yield break;

                at[i] = end;
                if (hitsGround) ImpactAt(ctx, end, Vector2.one * radius * 3f);
                if (betweenDives > 0f) yield return Hold(ctx, limbs, parts, at, ctx.Scaled(betweenDives), aim, 0f);
            }

            foreach (var part in parts) limbs.Release(part, ctx.Scaled(returnSeconds));
        }

        /// <summary>Todas quietas en su sitio; sólo la que va a lanzarse tiembla.</summary>
        private IEnumerator HoldOne(BossContext ctx, BossLimbs limbs, Transform[] parts, Vector2[] at,
                                    Vector2?[] aim, int shaking, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!ctx.IsValid) yield break;

                for (int i = 0; i < parts.Length; i++)
                {
                    Vector2 jitter = i == shaking && tremble > 0f ? Random.insideUnitCircle * tremble : Vector2.zero;
                    limbs.DriveCenter(parts[i], at[i] + jitter, AngleFor(limbs, parts[i], aim[i]));
                }

                yield return null;
            }
        }

        private static float AngleFor(BossLimbs limbs, Transform part, Vector2? direction) =>
            direction.HasValue ? limbs.AngleToward(part, direction.Value) : limbs.RestAngle(part);

        private Vector2[] Ring(Vector2 hub, float startAngle, int count)
        {
            var ring = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float a = (startAngle + 360f * i / count) * Mathf.Deg2Rad;
                ring[i] = hub + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * orbitRadius;
            }

            return ring;
        }

        private static Vector2?[] Outward(Vector2 hub, Vector2[] ring)
        {
            var directions = new Vector2?[ring.Length];
            for (int i = 0; i < ring.Length; i++) directions[i] = (ring[i] - hub).normalized;
            return directions;
        }

        private IEnumerator Fallback(BossContext ctx)
        {
            for (int i = 0; i < fallbackShots; i++)
            {
                if (!ctx.IsValid) yield break;

                Vector2 origin = ctx.Origin + new Vector2(fallbackProjectile.muzzleOffset.x * ctx.Facing,
                                                          fallbackProjectile.muzzleOffset.y);
                Vector2 target = ctx.PlayerCenter;
                Warn(ctx, target, Vector2.one * 1.6f, ctx.Scaled(diveWindup));
                yield return new WaitForSeconds(ctx.Scaled(diveWindup));

                Vector2 dir = target - origin;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;
                Impact();
                ProjectileFactory.Spawn(ctx.Ability, fallbackProjectile, origin, dir, ScaledDamage(ctx),
                                        KnockbackMultiplier, ctx.FxSprite, ctx.Accent);
                yield return new WaitForSeconds(ctx.Scaled(betweenDives));
            }
        }
    }
}
