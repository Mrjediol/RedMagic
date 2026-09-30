using System.Collections;
using RedMagic.Abilities;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Puño lanzado — <b>"¿dónde estaba yo cuando lo cargó?"</b>. Una pieza del cuerpo se echa
    /// atrás temblando (el aviso: además se marca el punto y la trayectoria), sale disparada hacia
    /// donde estaba el jugador al empezar a cargar, sigue un poco más allá, se queda clavada un
    /// instante y vuelve a su sitio. Esquivarlo es moverse durante la carga; quedarse quieto es
    /// comérselo. Si llega al suelo, se hunde y retumba.
    ///
    /// Con varios lanzamientos alterna o elige puño (<see cref="pick"/>). Sin extremidades (jefe de
    /// un solo sprite) lanza el proyectil <see cref="fallbackProjectile"/> con el mismo ritmo.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Fist Throw Attack", fileName = "BossAttack_FistThrow")]
    public class FistThrowAttack : LimbAttack
    {
        [Header("Lanzamientos")]
        [Min(1)]
        [SerializeField] private int throws = 1;

        [Tooltip("Segundos entre un lanzamiento y la carga del siguiente.")]
        [Min(0f)]
        [SerializeField] private float timeBetweenThrows = 0.2f;

        [SerializeField] private LimbPick pick = LimbPick.NearestToTarget;

        [Header("Carga (aviso)")]
        [Tooltip("Segundos de carga: el puño se echa atrás y tiembla, y se marca adónde irá.")]
        [Min(0.05f)]
        [SerializeField] private float windup = 0.6f;

        [Tooltip("Cuánto se echa atrás el puño, en contra de la trayectoria.")]
        [SerializeField] private float pullBack = 1.2f;

        [Tooltip("Cuánto sube mientras carga.")]
        [SerializeField] private float pullUp = 0.6f;


        [Header("Vuelo")]
        [Tooltip("Segundos hasta el punto marcado (acelera: sale lento y llega rápido).")]
        [Min(0.05f)]
        [SerializeField] private float flightSeconds = 0.32f;

        [Tooltip("Sigue esta distancia más allá del punto marcado: no se para justo encima.")]
        [Min(0f)]
        [SerializeField] private float overshoot = 1.5f;

        [Tooltip("Segundos clavado al final antes de volver.")]
        [Min(0f)]
        [SerializeField] private float embedSeconds = 0.45f;

        [Tooltip("Segundos de vuelta al cuerpo.")]
        [Min(0.05f)]
        [SerializeField] private float returnSeconds = 0.55f;

        [Tooltip("También hace daño al volver (castiga perseguirlo).")]
        [SerializeField] private bool hitOnReturn;

        [Header("Sin extremidades")]
        [ArtSlot("Proyectil sin extremidades", ArtSlotKind.Projectile, "bola de color de la fase")]
        [Tooltip("Lo que lanza un jefe sin piezas sueltas, desde su centro hacia el jugador.")]
        [SerializeField] private ProjectileSpec fallbackProjectile = new ProjectileSpec
        {
            speed = 16f,
            lifetime = 1.6f,
            size = new Vector2(1.2f, 1.2f),
            muzzleOffset = new Vector2(1.5f, 2.5f),
        };

        public override string ShortStats() =>
            $"{Damage:0} dmg · {throws} puño(s) · carga {windup:0.00}s · vuelo {flightSeconds:0.00}s";

        public override IEnumerator Run(BossContext ctx)
        {
            var limbs = Limbs(ctx);
            for (int n = 0; n < throws; n++)
            {
                if (!ctx.IsValid) yield break;

                if (limbs != null) yield return ThrowOne(ctx, limbs);
                else yield return Fallback(ctx);

                if (n < throws - 1) yield return new WaitForSeconds(ctx.Scaled(timeBetweenThrows));
            }
        }

        private IEnumerator ThrowOne(BossContext ctx, BossLimbs limbs)
        {
            float radius = limbs.ContactRadius;

            // El punto se congela al empezar la carga: es lo que se esquiva.
            Vector2 target = AboveGround(ctx, ctx.PlayerCenter, radius);
            var fist = limbs.Pick(pick, target);
            if (fist == null) yield break;

            var parts = new[] { fist };
            Vector2 start = limbs.Center(fist);
            Vector2 dir = target - start;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : new Vector2(ctx.Facing, 0f);

            Vector2 end = target + dir * overshoot;
            bool hitsGround = end.y < ctx.GroundY + radius;
            end = AboveGround(ctx, end, radius);
            Vector2 back = start - dir * pullBack + Vector2.up * pullUp;

            // --- carga: se marca el destino y el recorrido, el puño se echa atrás y tiembla.
            float charge = ctx.Scaled(windup);
            WarnPart(ctx, limbs, fist, dir, target, charge + ctx.Scaled(flightSeconds));

            // Los nudillos miran al objetivo desde que empieza a cargar: se lee hacia dónde va.
            var aim = Aim(dir);
            yield return Fly(ctx, limbs, parts, new[] { start }, new[] { back }, charge * 0.55f, Ease.Out, aim);
            yield return Hold(ctx, limbs, parts, new[] { back }, charge * 0.45f, aim, tremble);
            if (!ctx.IsValid) yield break;

            // --- vuelo: acelera hacia el punto y lo pasa; daña al tocar (una vez).
            var hit = new HitOnce();
            yield return Fly(ctx, limbs, parts, new[] { back }, new[] { end }, ctx.Scaled(flightSeconds), Ease.In, aim, hit);
            if (!ctx.IsValid) yield break;

            if (hitsGround) ImpactAt(ctx, end, Vector2.one * radius * 3f);
            else if (hit.Landed) ImpactAt(ctx, end, Vector2.one * radius * 2f);

            // --- clavado y vuelta.
            yield return Hold(ctx, limbs, parts, new[] { end }, ctx.Scaled(embedSeconds), aim, hitsGround ? 0f : tremble * 0.5f);
            yield return FlyHome(ctx, limbs, fist, ctx.Scaled(returnSeconds), hitOnReturn ? new HitOnce() : null);
        }

        private IEnumerator Fallback(BossContext ctx)
        {
            Vector2 origin = ctx.Origin + new Vector2(fallbackProjectile.muzzleOffset.x * ctx.Facing,
                                                      fallbackProjectile.muzzleOffset.y);
            Vector2 target = ctx.PlayerCenter;
            Warn(ctx, target, Vector2.one * 2f, ctx.Scaled(windup));
            yield return new WaitForSeconds(ctx.Scaled(windup));
            if (!ctx.IsValid) yield break;

            Vector2 dir = target - origin;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : new Vector2(ctx.Facing, 0f);
            Impact();
            ProjectileFactory.Spawn(ctx.Ability, fallbackProjectile, origin, dir, ScaledDamage(ctx),
                                    KnockbackMultiplier, ctx.FxSprite, ctx.Accent);
        }
    }
}
