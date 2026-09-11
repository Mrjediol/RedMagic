using System.Collections;
using RedMagic.Abilities;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>Geometría del patrón. Lo único que cambia es de dónde salen y hacia dónde.</summary>
    public enum BulletPattern
    {
        /// <summary>Anillo alrededor del jefe. Con giro por oleada se convierte en espiral.</summary>
        Radial,

        /// <summary>Abanico centrado en una dirección (normalmente, hacia el jugador).</summary>
        Fan,

        /// <summary>Cae desde el techo de la arena, con marcas en el suelo antes de cada oleada.</summary>
        Rain,

        /// <summary>
        /// Tormenta: durante unos segundos caen proyectiles sin parar, cada uno en una X al azar y
        /// con su propio ángulo (algunos en diagonal). Sin filas ni oleadas que leer: hay que
        /// moverse entre lo que va cayendo.
        /// </summary>
        Storm
    }

    /// <summary>
    /// El ataque "bullet hell" del jefe: nubes de proyectiles con huecos por los que colarse
    /// mientras se le sigue pegando. Es lo que llena el tiempo entre los ataques grandes y lo que
    /// obliga a moverse sin dejar de atacar.
    ///
    /// Los proyectiles salen por <see cref="ProjectileFactory"/> con un
    /// <see cref="ProjectileSpec"/> normal y corriente, así que van <b>pooled</b> como los del
    /// jugador y heredan gratis perforación, teledirigido, parábola y explosión al impactar.
    ///
    /// Los tres patrones cubren cosas distintas de verdad:
    ///  - <see cref="BulletPattern.Radial"/> con giro = espiral: hay que leer el hueco y moverse
    ///    <i>con</i> él;
    ///  - <see cref="BulletPattern.Fan"/> apuntado = castigo por quedarse quieto en línea;
    ///  - <see cref="BulletPattern.Rain"/> = castigo por quedarse quieto en el suelo, con marcas
    ///    que dicen exactamente dónde no estar.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Bullet Hell Attack", fileName = "BossAttack_BulletHell")]
    public class BulletHellAttack : BossAttack
    {
        [Header("Patrón")]
        [SerializeField] private BulletPattern pattern = BulletPattern.Radial;

        [Tooltip("Cómo vuela cada proyectil. El mismo bloque que usan las habilidades del jugador.")]
        [SerializeField] private ProjectileSpec projectile = new ProjectileSpec();

        [Header("Oleadas")]
        [Min(1)]
        [SerializeField] private int volleys = 3;

        [Min(0.02f)]
        [SerializeField] private float timeBetweenVolleys = 0.35f;

        [Min(1)]
        [SerializeField] private int bulletsPerVolley = 12;

        [Header("Forma (Radial / Fan)")]
        [Tooltip("Apertura total en grados. 360 en Radial = anillo completo.")]
        [Range(0f, 360f)]
        [SerializeField] private float arcDegrees = 360f;

        [Tooltip("Ángulo base cuando no se apunta al jugador (0 = a la derecha).")]
        [SerializeField] private float startAngle;

        [Tooltip("Grados que rota el patrón en cada oleada. Es lo que convierte un anillo en una " +
                 "espiral: con un valor que NO divida a 360 el hueco se desplaza y hay que seguirlo.")]
        [SerializeField] private float spinPerVolley = 11f;

        [Tooltip("Invierte el giro en cada oleada. El patrón se cruza consigo mismo y deja huecos " +
                 "que abren y cierran, en vez de una espiral previsible.")]
        [SerializeField] private bool alternateSpin;

        [Tooltip("Desviación aleatoria por proyectil, en grados.")]
        [Min(0f)]
        [SerializeField] private float randomSpread;

        [Tooltip("Centra el patrón en el jugador. En Fan es lo normal; en Radial gira el anillo entero.")]
        [SerializeField] private bool aimAtPlayer = true;

        [Header("Origen (Radial / Fan)")]
        [Tooltip("Desplazamiento del centro del patrón respecto al jefe. No se voltea con la mirada.")]
        [SerializeField] private Vector2 originOffset = new Vector2(0f, 2.2f);

        [Tooltip("Radio del círculo sobre el que nacen los proyectiles. Hace que el anillo se lea " +
                 "como algo que emana de la copa, y no como un punto que escupe.")]
        [Min(0f)]
        [SerializeField] private float spawnRadius = 1.2f;

        [Header("Lluvia")]
        [Tooltip("Fracción de la anchura de la arena que cubre cada oleada. 1 = de lado a lado; por " +
                 "encima de 1 se sale un poco de la arena (para cubrir una plataforma que asoma).")]
        [Range(0.1f, 2f)]
        [SerializeField] private float rainSpan = 1f;

        [Tooltip("Desplaza en X el centro de la franja de lluvia respecto al jefe, en unidades. " +
                 "0 = centrada en el jefe.")]
        [SerializeField] private float rainCenterOffset;

        [Tooltip("Cada gota en una X al azar dentro de la franja, en vez de repartidas en rejilla " +
                 "(+ 'rainJitter').")]
        [SerializeField] private bool rainRandomX;

        [Tooltip("Segundos entre una gota y la siguiente dentro de la misma oleada (en rejilla, de " +
                 "izquierda a derecha). 0 = todas a la vez. Cada marca dura hasta que cae su gota.")]
        [Min(0f)]
        [SerializeField] private float rainDropStagger;

        [Tooltip("Segundos que la marca del suelo está visible antes de que caiga el proyectil.")]
        [Min(0.05f)]
        [SerializeField] private float rainMarkerSeconds = 0.5f;

        [Tooltip("Desorden horizontal de cada gota respecto a su hueco. 0 = rejilla perfecta " +
                 "(fácil de leer), alto = caos.")]
        [Min(0f)]
        [SerializeField] private float rainJitter = 0.8f;

        [Tooltip("Tamaño de la marca de aviso en el suelo.")]
        [SerializeField] private Vector2 rainMarkerSize = new Vector2(1f, 0.35f);

        [Header("Tormenta (Storm) — usa también rainSpan / rainCenterOffset / rainMarkerSize")]
        [Tooltip("Segundos que dura la tormenta. NO lo acorta el ritmo de la fase: es lo que dura.")]
        [Min(0.1f)]
        [SerializeField] private float stormSeconds = 5f;

        [Tooltip("Proyectiles por segundo mientras dura. Tampoco lo cambia el ritmo de la fase.")]
        [Min(0.1f)]
        [SerializeField] private float stormPerSecond = 7f;

        [Tooltip("Desviación máxima respecto a la vertical, en grados (± al azar por proyectil). " +
                 "0 = todos rectos hacia abajo. La X al azar es la de LLEGADA, así que las diagonales " +
                 "siguen cubriendo toda la franja. Ojo: projectile.lifetime tiene que cubrir la caída " +
                 "más larga (alto de la arena / cos(ángulo) / speed) o se apagan en el aire.")]
        [Range(0f, 60f)]
        [SerializeField] private float stormAngleRange = 18f;

        [Tooltip("Viento: inclinación común a todos, en grados. + = caen hacia la derecha.")]
        [Range(-45f, 45f)]
        [SerializeField] private float stormWind;

        [Tooltip("Marca en el suelo dónde va a caer cada proyectil, desde que nace hasta que llega.")]
        [SerializeField] private bool stormMarkLanding = true;

        [Header("Arte")]
        [Tooltip("Sprite de los proyectiles de ESTE patrón. Vacío = el del jefe " +
                 "(BossController.fxSprite). Está separado del sprite del jefe para que cada " +
                 "ataque pueda tirar de su propio objeto —calabazas, abrojos, piedras— sin que los " +
                 "avisos y las ondas dejen de ser rectángulos legibles.\n\n" +
                 "Ojo: los proyectiles NO giran hacia su dirección de vuelo, así que aquí sólo " +
                 "funcionan sprites redondeados; uno alargado (una espada, una lanza) volaría de lado.")]
        [SerializeField] private Sprite projectileSprite;

        /// <summary>Sprite con el que sale cada proyectil: el propio del ataque, o el del jefe.</summary>
        private Sprite Art(in BossContext ctx) => projectileSprite != null ? projectileSprite : ctx.FxSprite;

        public override string ShortStats() =>
            pattern == BulletPattern.Storm
                ? $"{Damage:0} dmg · Storm · {stormSeconds:0.#}s × {stormPerSecond:0.#}/s · ±{stormAngleRange:0}°"
                : $"{Damage:0} dmg · {pattern} · {volleys}×{bulletsPerVolley} proyectiles";

        public override void OnTelegraph(BossContext ctx)
        {
            if (pattern == BulletPattern.Rain || pattern == BulletPattern.Storm)
            {
                // La lluvia avisa con marcas por oleada dentro de Run; aquí sólo se marca el suelo
                // entero para que se entienda "va a caer algo".
                var center = new Vector2(ctx.Origin.x + rainCenterOffset, ctx.GroundY + 0.2f);
                Warn(ctx, center, new Vector2(ctx.ArenaHalfWidth * 2f * rainSpan, 0.4f), ctx.Scaled(Telegraph));
                return;
            }

            Warn(ctx, ctx.Origin + originOffset, Vector2.one * (spawnRadius * 2.2f), ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            if (pattern == BulletPattern.Rain) return RunRain(ctx);
            if (pattern == BulletPattern.Storm) return RunStorm(ctx);
            return RunAngular(ctx);
        }

        // ------------------------------------------------------------------ radial / abanico

        private IEnumerator RunAngular(BossContext ctx)
        {
            for (int volley = 0; volley < volleys; volley++)
            {
                if (!ctx.IsValid) yield break;

                Impact();
                FireVolley(ctx, volley);

                if (volley < volleys - 1)
                    yield return new WaitForSeconds(ctx.Scaled(timeBetweenVolleys));
            }
        }

        private void FireVolley(in BossContext ctx, int volley)
        {
            Vector2 center = ctx.Origin + originOffset;

            float baseAngle = aimAtPlayer
                ? Mathf.Atan2(ctx.AimAtPlayer.y, ctx.AimAtPlayer.x) * Mathf.Rad2Deg
                : startAngle;

            float spinSign = alternateSpin && (volley & 1) == 1 ? -1f : 1f;
            baseAngle += spinPerVolley * volley * spinSign;

            bool fullRing = pattern == BulletPattern.Radial && arcDegrees >= 359.5f;

            // En un anillo completo el paso reparte los 360º entre todos (no hay extremos); en un
            // abanico el reparto es entre extremos, así que con un solo proyectil sale recto.
            float step = fullRing
                ? arcDegrees / bulletsPerVolley
                : (bulletsPerVolley > 1 ? arcDegrees / (bulletsPerVolley - 1) : 0f);

            float start = fullRing ? baseAngle : baseAngle - arcDegrees * 0.5f;

            for (int i = 0; i < bulletsPerVolley; i++)
            {
                float angle = start + step * i + Random.Range(-randomSpread, randomSpread);
                float radians = angle * Mathf.Deg2Rad;

                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 origin = center + direction * spawnRadius;

                ProjectileFactory.Spawn(ctx.Ability, projectile, origin, direction,
                                        ScaledDamage(ctx), KnockbackMultiplier,
                                        Art(ctx), ctx.Accent);
            }
        }

        // ------------------------------------------------------------------ lluvia

        /// <summary>
        /// Lluvia telegrafiada: primero se marcan los huecos en el suelo y sólo después cae algo.
        /// Marcar antes es lo que separa "esquiva esto" de "te ha caído encima sin más".
        /// </summary>
        private IEnumerator RunRain(BossContext ctx)
        {
            float span = ctx.ArenaHalfWidth * 2f * rainSpan;
            float left = ctx.Origin.x + rainCenterOffset - span * 0.5f;
            float step = bulletsPerVolley > 1 ? span / (bulletsPerVolley - 1) : 0f;
            float stagger = ctx.Scaled(rainDropStagger);

            var columns = new float[bulletsPerVolley];

            for (int volley = 0; volley < volleys; volley++)
            {
                if (!ctx.IsValid) yield break;

                for (int i = 0; i < bulletsPerVolley; i++)
                {
                    if (rainRandomX)
                    {
                        columns[i] = Random.Range(left, left + span);
                    }
                    else
                    {
                        float x = bulletsPerVolley > 1 ? left + step * i : left + span * 0.5f;
                        columns[i] = x + Random.Range(-rainJitter, rainJitter);
                    }

                    // Con goteo escalonado, cada marca dura hasta que cae SU gota.
                    Warn(ctx, new Vector2(columns[i], ctx.GroundY + rainMarkerSize.y * 0.5f),
                         rainMarkerSize, ctx.Scaled(rainMarkerSeconds) + stagger * i);
                }

                yield return new WaitForSeconds(ctx.Scaled(rainMarkerSeconds));

                if (!ctx.IsValid) yield break;

                Impact();

                for (int i = 0; i < bulletsPerVolley; i++)
                {
                    if (!ctx.IsValid) yield break;

                    var origin = new Vector2(columns[i], ctx.CeilingY);
                    ProjectileFactory.Spawn(ctx.Ability, projectile, origin, Vector2.down,
                                            ScaledDamage(ctx), KnockbackMultiplier,
                                            Art(ctx), ctx.Accent);

                    if (stagger > 0f && i < bulletsPerVolley - 1)
                        yield return new WaitForSeconds(stagger);
                }

                if (volley < volleys - 1)
                    yield return new WaitForSeconds(ctx.Scaled(timeBetweenVolleys));
            }
        }

        // ------------------------------------------------------------------ tormenta

        /// <summary>
        /// Tormenta: <see cref="stormSeconds"/> segundos soltando <see cref="stormPerSecond"/>
        /// proyectiles por segundo, cada uno con su X y su ángulo. La X al azar es la de LLEGADA al
        /// suelo y el origen se desplaza en contra de la diagonal, así que inclinarlos no deja un
        /// lado de la arena vacío ni tira la mitad fuera.
        /// </summary>
        private IEnumerator RunStorm(BossContext ctx)
        {
            float span = ctx.ArenaHalfWidth * 2f * rainSpan;
            float left = ctx.Origin.x + rainCenterOffset - span * 0.5f;
            float height = ctx.CeilingY - ctx.GroundY;
            float interval = 1f / stormPerSecond;

            Impact();

            // Acumulador: el ritmo se mantiene aunque los frames lleguen a saltos. Empieza lleno para
            // que el primero salga ya.
            float due = interval;
            for (float elapsed = 0f; elapsed < stormSeconds; elapsed += Time.deltaTime)
            {
                if (!ctx.IsValid) yield break;

                due += Time.deltaTime;
                while (due >= interval)
                {
                    due -= interval;
                    DropStormProjectile(ctx, left, span, height);
                }

                yield return null;
            }
        }

        private void DropStormProjectile(in BossContext ctx, float left, float span, float height)
        {
            float degrees = stormWind + Random.Range(-stormAngleRange, stormAngleRange);
            float radians = degrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));

            float landX = Random.Range(left, left + span);
            var origin = new Vector2(landX - height * Mathf.Tan(radians), ctx.CeilingY);

            if (stormMarkLanding)
            {
                float fallSeconds = height / Mathf.Max(0.05f, Mathf.Cos(radians)) / Mathf.Max(0.1f, projectile.speed);
                Warn(ctx, new Vector2(landX, ctx.GroundY + rainMarkerSize.y * 0.5f), rainMarkerSize, fallSeconds);
            }

            ProjectileFactory.Spawn(ctx.Ability, projectile, origin, direction,
                                    ScaledDamage(ctx), KnockbackMultiplier, Art(ctx), ctx.Accent);
        }
    }
}
