using System.Collections;
using RedMagic.Abilities;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// <b>"¿Por dónde salgo antes de que estén listos?"</b> — el jefe no dispara: primero
    /// <i>fabrica</i> la munición. Van apareciendo orbes uno a uno en una corona a su alrededor,
    /// diminutos, y crecen girando despacio. Mientras crecen no hacen nada: son un reloj de cuenta
    /// atrás que se ve, y que dice exactamente cuántos proyectiles van a salir y con qué hueco entre
    /// ellos. Cuando llegan a su tamaño máximo, todos salen disparados a la vez en todas
    /// direcciones.
    ///
    /// Esa espera es la mitad del ataque. Al contrario que una descarga radial normal, que hay que
    /// leer mientras ya te está llegando, aquí el patrón está dibujado en el aire antes de existir:
    /// la pregunta no es "¿esquivo esto?" sino "¿me coloco en un hueco, o gasto la ventana pegándole
    /// al jefe?". Por eso el ataque suele valer un <c>vulnerableSeconds</c> generoso: la carga es
    /// también la mejor ocasión de hacer daño.
    ///
    /// Los orbes de la carga son <see cref="BossOrb"/> (pooled, sin collider ni daño). Al lanzarse,
    /// cada uno se suelta y en su posición exacta nace un proyectil de verdad por
    /// <see cref="ProjectileFactory"/>, así que lo que golpea es el mismo sistema de siempre.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Orb Ring Attack", fileName = "BossAttack_CoronaDeOrbes")]
    public class OrbRingAttack : BossAttack
    {
        [Header("Corona")]
        [Tooltip("Orbes de la corona. También son los proyectiles que saldrán: más orbes = menos hueco.")]
        [Min(1)]
        [SerializeField] private int orbCount = 10;

        [Tooltip("Radio de la corona alrededor del jefe, en unidades de mundo.")]
        [Min(0.2f)]
        [SerializeField] private float ringRadius = 2.8f;

        [Tooltip("Centro de la corona respecto al origen del jefe (su base).")]
        [SerializeField] private Vector2 originOffset = new Vector2(0f, 3f);

        [Tooltip("Ángulo del primer orbe. Con 'apuntar al jugador' encendido, se ignora.")]
        [SerializeField] private float startAngle;

        [Tooltip("Alinea la corona con el jugador, para que siempre haya un orbe apuntándole (y por " +
                 "tanto un hueco en un sitio predecible).")]
        [SerializeField] private bool alignToPlayer = true;

        [Tooltip("Grados por segundo que gira la corona mientras carga. El giro es lo que impide " +
                 "memorizar el hueco y quedarse quieto en él desde el principio.")]
        [SerializeField] private float spinDegreesPerSecond = 35f;

        [Header("Crecimiento")]
        [Tooltip("Tamaño del orbe recién creado, como fracción del tamaño autorizado en su prefab. " +
                 "0.15 = una chispa.")]
        [Min(0.02f)]
        [SerializeField] private float startScale = 0.15f;

        [Tooltip("Tamaño al que se dispara, en las mismas unidades. 1 = el prefab tal cual está.")]
        [Min(0.05f)]
        [SerializeField] private float endScale = 1f;

        [Tooltip("Tamaño en unidades de mundo del orbe cuando NO hay prefab y se construye en " +
                 "código. Con prefab se ignora.")]
        [Min(0.05f)]
        [SerializeField] private float codeOrbSize = 0.7f;

        [Tooltip("Segundos que tarda un orbe en crecer del todo.")]
        [Min(0.1f)]
        [SerializeField] private float growSeconds = 1.6f;

        [Tooltip("Retraso entre la aparición de un orbe y la del siguiente. Es lo que hace que se " +
                 "lean como creados uno a uno en vez de aparecer de golpe.")]
        [Min(0f)]
        [SerializeField] private float appearStagger = 0.07f;

        [Tooltip("Pausa con la corona ya completa y a tamaño máximo, justo antes de soltarla.")]
        [Min(0f)]
        [SerializeField] private float holdSeconds = 0.35f;

        [Tooltip("Cuánto se encoge la corona en el último instante antes de disparar. 0 = nada. " +
                 "Un pellizco hacia dentro hace que la salida se sienta como un latigazo.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float recoilFraction = 0.18f;

        [Header("Disparo")]
        [Tooltip("Coronas seguidas.")]
        [Min(1)]
        [SerializeField] private int volleys = 1;

        [Tooltip("Espera entre una corona y la siguiente.")]
        [Min(0.05f)]
        [SerializeField] private float timeBetweenVolleys = 0.9f;

        [Tooltip("Dispersión aleatoria en grados al soltar. 0 = radial perfecto.")]
        [Min(0f)]
        [SerializeField] private float launchSpread;

        [Tooltip("Cómo vuela cada orbe una vez suelto. Ojo con 'size': si el prefab del proyectil " +
                 "lleva arte de verdad (sin FxPlaceholderStyle) el tamaño lo fija el prefab, así " +
                 "que hay que cuadrarlo a mano con 'endScale' o la bola cambiará de tamaño al salir.")]
        [SerializeField] private ProjectileSpec projectile = new ProjectileSpec();

        [Header("Arte")]
        [Tooltip("Prefab del orbe de carga (con BossOrb). Vacío = se construye en código.")]
        [SerializeField] private GameObject orbPrefab;

        [Tooltip("Sprite del orbe de carga cuando NO hay prefab. Vacío = el sprite FX del jefe.")]
        [SerializeField] private Sprite orbSprite;

        [Tooltip("Tiñe el orbe con el color de la fase. Déjalo apagado si el prefab ya lleva arte " +
                 "de verdad: teñirlo le apagaría su propio brillo.")]
        [SerializeField] private bool tintOrbWithAccent;

        [Tooltip("Sprite del proyectil ya lanzado cuando se construye en código. Vacío = el del jefe.")]
        [SerializeField] private Sprite projectileSprite;

        private Sprite OrbArt(in BossContext ctx) => orbSprite != null ? orbSprite : ctx.FxSprite;
        private Sprite ShotArt(in BossContext ctx) => projectileSprite != null ? projectileSprite : ctx.FxSprite;

        public override string ShortStats() =>
            $"{Damage:0} dmg · corona de {orbCount} · carga {growSeconds:0.0}s";

        public override void OnTelegraph(BossContext ctx)
        {
            // Aviso mínimo: la corona en sí ya es el telegrafiado más claro que puede haber, así
            // que aquí sólo se señala de dónde va a salir.
            Warn(ctx, ctx.Origin + originOffset, Vector2.one * (ringRadius * 0.5f), ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            for (int volley = 0; volley < volleys; volley++)
            {
                if (!ctx.IsValid) yield break;

                yield return RunCrown(ctx);

                if (volley < volleys - 1)
                    yield return new WaitForSeconds(ctx.Scaled(timeBetweenVolleys));
            }
        }

        /// <summary>Una corona entera: creación escalonada, crecimiento girando, y suelta.</summary>
        private IEnumerator RunCrown(BossContext ctx)
        {
            int count = Mathf.Max(1, orbCount);
            var orbs = new BossOrb[count];

            float stagger = ctx.Scaled(appearStagger);
            float grow = ctx.Scaled(growSeconds);
            float hold = ctx.Scaled(holdSeconds);

            // Seguro: si la corrutina muere a medias (cambio de fase, jefe abatido), los orbes se
            // recogen solos en vez de quedarse flotando en la arena.
            float lifetime = stagger * count + grow + hold + 1f;

            float baseAngle = alignToPlayer
                ? Mathf.Atan2(ctx.AimAtPlayer.y, ctx.AimAtPlayer.x) * Mathf.Rad2Deg
                : startAngle;

            float step = 360f / count;
            float elapsed = 0f;
            float total = stagger * (count - 1) + grow + hold;

            try
            {
                while (elapsed < total)
                {
                    if (!ctx.IsValid) yield break;

                    Vector2 center = ctx.Origin + originOffset;
                    float spin = baseAngle + spinDegreesPerSecond * elapsed;

                    for (int i = 0; i < count; i++)
                    {
                        float bornAt = stagger * i;
                        if (elapsed < bornAt) continue;

                        if (orbs[i] == null)
                        {
                            orbs[i] = BossOrb.Spawn(ctx, orbPrefab, center, OrbArt(ctx),
                                                    ctx.Accent, tintOrbWithAccent, lifetime,
                                                    codeOrbSize);
                        }

                        float t = grow <= 0f ? 1f : Mathf.Clamp01((elapsed - bornAt) / grow);
                        // Arranque suave y llegada seca: el orbe "cuaja" al final en vez de
                        // deslizarse hasta el tamaño, que se lee mucho peor.
                        float eased = t * t * (3f - 2f * t);

                        orbs[i].Place(RingPoint(center, spin + step * i, elapsed, total),
                                      Mathf.Lerp(startScale, endScale, eased));
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!ctx.IsValid) yield break;

                Impact();
                Launch(ctx, orbs, ctx.Origin + originOffset,
                       baseAngle + spinDegreesPerSecond * elapsed, step);
            }
            finally
            {
                for (int i = 0; i < count; i++) orbs[i]?.Release();
            }
        }

        /// <summary>Posición de un orbe en la corona, con el pellizco hacia dentro del final.</summary>
        private Vector2 RingPoint(Vector2 center, float angleDegrees, float elapsed, float total)
        {
            float radius = ringRadius;

            if (recoilFraction > 0f && total > 0f)
            {
                // Sólo en el último 15% del tiempo, y de forma acelerada: es un tirón, no un viaje.
                float k = Mathf.InverseLerp(total * 0.85f, total, elapsed);
                radius *= 1f - recoilFraction * k * k;
            }

            float radians = angleDegrees * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }

        /// <summary>
        /// Suelta la corona: cada orbe se apaga y en su sitio exacto nace un proyectil que sale
        /// hacia fuera. Se dispara desde la posición del orbe, no desde el jefe, para que no haya
        /// ningún salto visual entre lo que se estaba viendo y lo que empieza a volar.
        /// </summary>
        private void Launch(in BossContext ctx, BossOrb[] orbs, Vector2 center, float spin, float step)
        {
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null) continue;

                Vector2 origin = orbs[i].Position;
                orbs[i].Release();
                orbs[i] = null;

                float angle = spin + step * i + Random.Range(-launchSpread, launchSpread);
                float radians = angle * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                // Si por lo que sea el orbe no llegó a existir, al menos que salga de su hueco.
                if (origin == Vector2.zero) origin = center + direction * ringRadius;

                ProjectileFactory.Spawn(ctx.Ability, projectile, origin, direction,
                                        ScaledDamage(ctx), KnockbackMultiplier,
                                        ShotArt(ctx), ctx.Accent);
            }
        }
    }
}
