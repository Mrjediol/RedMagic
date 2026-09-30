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
    ///
    /// En un jefe <b>por partes</b> (<see cref="BossLimbs"/>) lo hace con la mano: un puño baja al suelo
    /// delante del jefe, coge el objeto, lo sube por encima del hombro y lo lanza desde el puño; con
    /// varios lanzamientos alterna de mano. El vuelo, la marca y el daño son los mismos.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Throw Projectile Attack", fileName = "BossAttack_Throw")]
    [AttackPoint("heldOffset", "Objeto en la mano")]
    [AttackHides("projectile.speed", "projectile.muzzleOffset", "projectile.homingTurnRate",
                 "projectile.homingRange", "projectile.arcGravity")]
    public class ThrowProjectileAttack : LimbAttack
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

        [Header("Con extremidades (jefe por partes)")]
        [Tooltip("Con BossLimbs en el jefe, una mano baja al suelo, coge el objeto y lo lanza desde el " +
                 "puño. Apagado = siempre en alto sobre el jefe, como uno de un solo sprite.")]
        [SerializeField] private bool useLimbs = true;

        [Tooltip("Qué mano. Alternate = con varios lanzamientos, una y luego la otra.")]
        [SerializeField] private LimbPick limbPick = LimbPick.Alternate;

        [Tooltip("Dónde recoge el objeto: X hacia el jugador desde los pies del jefe.")]
        [SerializeField] private float pickupReach = 2.5f;

        [Tooltip("Dónde lo sostiene antes de lanzar, desde los pies (X hacia el jugador; negativo = " +
                 "por detrás del hombro, cargando).")]
        [SerializeField] private Vector2 raisedOffset = new Vector2(-1f, 6.5f);

        [Tooltip("Segundos de la mano bajando al suelo.")]
        [Min(0.05f)]
        [SerializeField] private float reachSeconds = 0.4f;

        [Tooltip("Segundos agarrando en el suelo.")]
        [Min(0f)]
        [SerializeField] private float grabSeconds = 0.15f;

        [Tooltip("Segundos subiéndolo por encima del hombro.")]
        [Min(0.05f)]
        [SerializeField] private float liftSeconds = 0.35f;

        [Tooltip("Segundos del latigazo de lanzar; el objeto sale al final.")]
        [Min(0.02f)]
        [SerializeField] private float swingSeconds = 0.14f;

        [Tooltip("Cuánto avanza la mano en el latigazo.")]
        [SerializeField] private float swingReach = 2.5f;

        [Tooltip("Segundos de vuelta de la mano al cuerpo (lo anima el rig).")]
        [Min(0.05f)]
        [SerializeField] private float limbReturnSeconds = 0.45f;

        [Header("Sonido")]
        [Tooltip("Al coger el objeto que va a lanzar (la roca en alto).")]
        [SerializeField] private SoundCue pickupSound = new SoundCue { positional = true };

        [Header("Aviso de caída")]
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
            var limbs = useLimbs ? Limbs(ctx) : null;
            if (limbs != null)
            {
                yield return RunWithLimbs(ctx, limbs);
                yield break;
            }

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

        // ============================================================ con la mano

        private IEnumerator RunWithLimbs(BossContext ctx, BossLimbs limbs)
        {
            var body = ctx.Boss.BodyAnimator;
            for (int t = 0; t < throws; t++)
            {
                if (!ctx.IsValid) yield break;

                float radius = limbs.ContactRadius;
                var hand = limbs.Pick(limbPick, ctx.PlayerCenter);
                if (hand == null) yield break;
                var parts = new[] { hand };
                var down = Aim(Vector2.down);
                var up = Aim(Vector2.up);

                // --- baja al suelo delante del jefe y agarra.
                var ground = new Vector2(ctx.Origin.x + pickupReach * ctx.Facing, ctx.GroundY + radius);
                yield return Fly(ctx, limbs, parts, new[] { limbs.Center(hand) }, new[] { ground },
                                 ctx.Scaled(reachSeconds), Ease.InOut, down);
                if (!ctx.IsValid) yield break;

                float held = ctx.Scaled(reachSeconds + grabSeconds + liftSeconds + holdSeconds + swingSeconds);
                var prop = BossHeldProp.Spawn(heldPropPrefab, limbs.Grip(hand), Vector2.zero, heldSize,
                                              ctx.Accent, ctx.FxSprite, held + 2f);
                AudioManager.Instance?.Play(pickupSound, ground);
                yield return Hold(ctx, limbs, parts, new[] { ground }, ctx.Scaled(grabSeconds), down, tremble);

                // --- lo sube por encima del hombro y lo sostiene (el aviso: se ve lo que viene).
                var raised = ctx.Origin + new Vector2(raisedOffset.x * ctx.Facing, raisedOffset.y);
                yield return Fly(ctx, limbs, parts, new[] { ground }, new[] { raised }, ctx.Scaled(liftSeconds),
                                 Ease.Out, up);
                yield return Hold(ctx, limbs, parts, new[] { raised }, ctx.Scaled(holdSeconds), up, tremble);
                if (!ctx.IsValid) { if (prop != null) prop.Release(); yield break; }

                // --- latigazo hacia el jugador: el cuerpo acompaña con su gesto y el objeto sale del puño.
                float swing = ctx.Scaled(swingSeconds);
                if (body != null) body.PlayGesture(throwGesture, swing);

                Vector2 toPlayer = ctx.PlayerCenter - raised;
                var forward = Aim(toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : new Vector2(ctx.Facing, 0f));
                var swingEnd = raised + new Vector2(swingReach * ctx.Facing, -0.5f);
                yield return Fly(ctx, limbs, parts, new[] { raised }, new[] { swingEnd }, swing, Ease.In, forward);

                Vector2 origin = prop != null ? prop.Position : limbs.Center(hand);
                if (prop != null) prop.Release();
                if (!ctx.IsValid) yield break;

                Impact();
                Throw(ctx, origin);
                limbs.Release(hand, ctx.Scaled(limbReturnSeconds));

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
