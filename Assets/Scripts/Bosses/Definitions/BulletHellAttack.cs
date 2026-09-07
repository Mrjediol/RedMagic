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
        Rain
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
        [Tooltip("Fracción de la anchura de la arena que cubre cada oleada. 1 = de lado a lado.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float rainSpan = 1f;

        [Tooltip("Segundos que la marca del suelo está visible antes de que caiga el proyectil.")]
        [Min(0.05f)]
        [SerializeField] private float rainMarkerSeconds = 0.5f;

        [Tooltip("Desorden horizontal de cada gota respecto a su hueco. 0 = rejilla perfecta " +
                 "(fácil de leer), alto = caos.")]
        [Min(0f)]
        [SerializeField] private float rainJitter = 0.8f;

        [Tooltip("Tamaño de la marca de aviso en el suelo.")]
        [SerializeField] private Vector2 rainMarkerSize = new Vector2(1f, 0.35f);

        public override string ShortStats() =>
            $"{Damage:0} dmg · {pattern} · {volleys}×{bulletsPerVolley} proyectiles";

        public override void OnTelegraph(BossContext ctx)
        {
            if (pattern == BulletPattern.Rain)
            {
                // La lluvia avisa con marcas por oleada dentro de Run; aquí sólo se marca el suelo
                // entero para que se entienda "va a caer algo".
                var center = new Vector2(ctx.Origin.x, ctx.GroundY + 0.2f);
                Warn(ctx, center, new Vector2(ctx.ArenaHalfWidth * 2f * rainSpan, 0.4f), ctx.Scaled(Telegraph));
                return;
            }

            Warn(ctx, ctx.Origin + originOffset, Vector2.one * (spawnRadius * 2.2f), ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            if (pattern == BulletPattern.Rain) return RunRain(ctx);
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
                                        ctx.FxSprite, ctx.Accent);
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
            float left = ctx.Origin.x - span * 0.5f;
            float step = bulletsPerVolley > 1 ? span / (bulletsPerVolley - 1) : 0f;

            var columns = new float[bulletsPerVolley];

            for (int volley = 0; volley < volleys; volley++)
            {
                if (!ctx.IsValid) yield break;

                for (int i = 0; i < bulletsPerVolley; i++)
                {
                    float x = bulletsPerVolley > 1 ? left + step * i : ctx.Origin.x;
                    columns[i] = x + Random.Range(-rainJitter, rainJitter);

                    Warn(ctx, new Vector2(columns[i], ctx.GroundY + rainMarkerSize.y * 0.5f),
                         rainMarkerSize, ctx.Scaled(rainMarkerSeconds));
                }

                yield return new WaitForSeconds(ctx.Scaled(rainMarkerSeconds));

                if (!ctx.IsValid) yield break;

                Impact();

                for (int i = 0; i < bulletsPerVolley; i++)
                {
                    var origin = new Vector2(columns[i], ctx.CeilingY);
                    ProjectileFactory.Spawn(ctx.Ability, projectile, origin, Vector2.down,
                                            ScaledDamage(ctx), KnockbackMultiplier,
                                            ctx.FxSprite, ctx.Accent);
                }

                if (volley < volleys - 1)
                    yield return new WaitForSeconds(ctx.Scaled(timeBetweenVolleys));
            }
        }
    }
}
