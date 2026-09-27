using System.Collections;
using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Coger y lanzar — <b>"¿dónde va a caer?"</b>. El jefe recoge algo grande (su gesto de aviso,
    /// <see cref="BossAttack.Gesture"/>), lo sostiene en alto, hace el gesto de lanzar
    /// (<see cref="throwGesture"/>) y lo tira en parábola al sitio donde estaba el jugador, que
    /// queda marcado en el suelo durante todo el vuelo. El proyectil explota al caer.
    ///
    /// La marca se fija al soltar y no persigue: da exactamente el tiempo de vuelo para salir del
    /// círculo. Por eso el mando de dificultad es <see cref="flightSeconds"/>.
    ///
    /// Vale para cualquier "lanzar algo en arco": la roca del golem, un barril, una granada de un
    /// enemigo. Sin gesto de lanzar, suelta por tiempo.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Throw Projectile Attack", fileName = "BossAttack_Throw")]
    [AttackPoint("heldOffset", "Objeto en la mano")]
    [AttackHides("projectile.speed", "projectile.muzzleOffset", "projectile.homingTurnRate",
                 "projectile.homingRange", "projectile.arcGravity")]
    public class ThrowProjectileAttack : BossAttack
    {
        [Header("Gesto de lanzar")]
        [Tooltip("Estado del Animator del gesto de LANZAR. El de COGER va en 'Gesto' (arriba) y hace " +
                 "de aviso. Vacío = sin segundo gesto: se lanza al pasar 'holdSeconds'.")]
        [SerializeField] private string throwGesture = "Throw";

        [Tooltip("Segundos con el objeto en alto hasta soltarlo. Con gesto de lanzar, el clip se " +
                 "ajusta para que su frame de suelta caiga aquí.")]
        [Min(0.05f)]
        [SerializeField] private float holdSeconds = 0.6f;

        [Header("Vuelo")]
        [Tooltip("Segundos desde que lo suelta hasta que cae. Es TODO el margen para salir de la " +
                 "marca: el mando de dificultad de este ataque.")]
        [Min(0.2f)]
        [SerializeField] private float flightSeconds = 1.1f;

        [Tooltip("Gravedad de la parábola (u/s²). Más = arco más alto para el mismo tiempo.")]
        [Min(1f)]
        [SerializeField] private float gravity = 30f;

        [Tooltip("Desvío aleatorio del punto de caída, en X. 0 = justo donde estaba el jugador.")]
        [Min(0f)]
        [SerializeField] private float landingSpread;

        [Tooltip("Lanzamientos seguidos (cada uno repite el gesto de lanzar y vuelve a apuntar).")]
        [Min(1)]
        [SerializeField] private int throws = 1;

        [Min(0.05f)]
        [SerializeField] private float timeBetweenThrows = 0.5f;

        [Header("Proyectil")]
        [ArtSlot("Proyectil lanzado", ArtSlotKind.Projectile, "bola de color de la fase")]
        [Tooltip("Aspecto y explosión del objeto. prefab vacío = bola de color. 'impactRadius' / " +
                 "'impactDamage' = la explosión al caer. La velocidad y la gravedad las calcula el " +
                 "ataque para caer en la marca.")]
        [SerializeField] private ProjectileSpec projectile = new ProjectileSpec
        {
            size = new Vector2(1.6f, 1.6f),
            lifetime = 4f,
            impactRadius = 2.2f,
            impactDamage = 16f,
        };

        [Header("Objeto en la mano")]
        [ArtSlot("Objeto sostenido", ArtSlotKind.Prop, "cuadrado de color")]
        [Tooltip("Lo que se ve en alto antes de lanzarlo (la roca). Vacío = cuadrado del color de la " +
                 "fase. Tip: el mismo arte que el proyectil.")]
        [SerializeField] private GameObject heldPropPrefab;

        [Tooltip("Dónde lo sostiene, relativo a los pies del jefe (X hacia el jugador).")]
        [SerializeField] private Vector2 heldOffset = new Vector2(0f, 5.5f);

        [Tooltip("Tamaño del objeto sostenido. Con arte del importador manda el ancho (X).")]
        [SerializeField] private Vector2 heldSize = new Vector2(1.6f, 1.6f);

        [Header("Aviso de caída")]
        [Tooltip("Al coger el objeto que va a lanzar (la roca en alto).")]
        [SerializeField] private SoundCue pickupSound = new SoundCue { positional = true };

        [SerializeField] private bool markLanding = true;

        [Tooltip("Radio de la marca. 0 = el de la explosión (o 1.5 si no explota).")]
        [Min(0f)]
        [SerializeField] private float markRadius;

        public override string ShortStats() =>
            $"{Damage:0} dmg · vuelo {flightSeconds:0.00}s · explosión {projectile.impactRadius:0.#} u · {throws} lanz.";

        public override void OnTelegraph(BossContext ctx)
        {
            // Sin gesto de coger, un destello sobre la cabeza avisa de que viene algo.
            if (string.IsNullOrEmpty(Gesture))
                Warn(ctx, HeldPoint(ctx), heldSize * 1.5f, ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            for (int t = 0; t < throws; t++)
            {
                if (!ctx.IsValid) yield break;

                // --- en alto
                float hold = ctx.Scaled(holdSeconds);
                var prop = BossHeldProp.Spawn(heldPropPrefab, ctx.Boss.transform, heldOffset, heldSize,
                                              ctx.Accent, ctx.FxSprite, hold * 3f + 2f);
                AudioManager.Instance?.Play(pickupSound, HeldPoint(ctx));

                // --- gesto de lanzar
                var body = ctx.Boss.BodyAnimator;
                if (body != null && body.PlayGesture(throwGesture, hold))
                {
                    float timeout = Mathf.Max(hold, body.SecondsToRelease) + 0.5f;
                    while (!body.Released && timeout > 0f && ctx.IsValid)
                    {
                        timeout -= Time.deltaTime;
                        yield return null;
                    }
                }
                else
                {
                    yield return new WaitForSeconds(hold);
                }

                Vector2 origin = prop != null ? prop.Position : HeldPoint(ctx);
                if (prop != null) prop.Release();
                if (!ctx.IsValid) yield break;

                // --- suelta
                Impact();
                Throw(ctx, origin);

                if (t < throws - 1) yield return new WaitForSeconds(ctx.Scaled(timeBetweenThrows));
            }
        }

        private Vector2 HeldOffset(in BossContext ctx) => new Vector2(heldOffset.x * ctx.Facing, heldOffset.y);

        private Vector2 HeldPoint(in BossContext ctx) => ctx.Origin + HeldOffset(ctx);

        private void Throw(in BossContext ctx, Vector2 origin)
        {
            float x = ctx.PlayerPosition.x + (landingSpread > 0f ? Random.Range(-landingSpread, landingSpread) : 0f);
            var landing = new Vector2(Mathf.Clamp(x, ctx.ArenaMinX, ctx.ArenaMaxX), ctx.GroundY);

            // El ritmo de la fase acorta el vuelo; la gravedad sube con su cuadrado para que la
            // parábola tenga la misma forma, sólo más rápida.
            float time = ctx.Scaled(flightSeconds);
            float g = gravity * ctx.SpeedScale * ctx.SpeedScale;

            // Projectile resta la gravedad ANTES de cada paso (Euler), así que cae ~½·g·T·dt más
            // bajo que la parábola exacta: el término (T + dt) lo compensa y la roca cae en la marca.
            var velocity = new Vector2((landing.x - origin.x) / time,
                                       (landing.y - origin.y + 0.5f * g * time * (time + Time.fixedDeltaTime)) / time);

            if (markLanding)
            {
                float r = markRadius > 0f ? markRadius : (projectile.impactRadius > 0f ? projectile.impactRadius : 1.5f);
                WarnCircle(ctx, landing, r, time);
                BossHitboxDebug.Circle(landing, projectile.impactRadius, new Color(1f, 0.3f, 0.2f, 1f), time + 0.3f);
            }

            var go = ProjectileFactory.Spawn(ctx.Ability, projectile, origin, velocity.normalized,
                                             ScaledDamage(ctx), KnockbackMultiplier, ctx.FxSprite, ctx.Accent);
            var shot = go != null ? go.GetComponent<Projectile>() : null;
            if (shot == null) return;

            // La velocidad y la gravedad no son las del spec: son las justas para caer en la marca.
            shot.Configure(-1f, velocity.magnitude, ctx.Ability.HitLayers);
            shot.ConfigureBehaviour(projectile.pierce, 0f, projectile.homingRange, g,
                                    projectile.impactRadius * ctx.Ability.SizeScale, projectile.impactDamage,
                                    KnockbackMultiplier, Mathf.Max(projectile.lifetime, time + 1.5f));
            shot.Launch(velocity.normalized, ctx.Ability.Caster);
        }
    }
}
