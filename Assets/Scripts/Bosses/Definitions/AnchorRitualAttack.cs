using System.Collections;
using System.Collections.Generic;
using RedMagic.Abilities;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// El <b>ritual</b>: el jefe se vuelve intocable y planta unas anclas
    /// (<see cref="BossAnchor"/>) por la arena. Hay que romperlas todas antes de que termine la
    /// cuenta atrás.
    ///
    /// Es la única pregunta del juego que no va de esquivar ni de cuándo pegar, sino de <b>a qué
    /// pegar</b>. Mientras haya anclas en pie, cada bala que le entre al jefe es daño tirado; y
    /// como hay reloj, tampoco vale ir repartiendo. Obliga a mirar el arma que se lleva y decidir:
    /// una escopeta limpia dos anclas juntas, un rifle va una a una.
    ///
    /// Y tiene las dos salidas que hacen que importe:
    /// <list type="bullet">
    /// <item><b>A tiempo</b> — el ritual se rompe y el jefe queda expuesto (los campos "Castigo"
    /// del asset). Ése es el pago por haber dejado de dispararle a él.</item>
    /// <item><b>Tarde</b> — el ritual se completa y descarga un golpe que cubre la arena entera.
    /// Sin ese castigo, ignorar las anclas sería gratis y el ataque, decorado.</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Anchor Ritual Attack", fileName = "BossAttack_Ritual")]
    public class AnchorRitualAttack : BossAttack
    {
        [Header("Anclas")]
        [Min(1)]
        [SerializeField] private int anchorCount = 3;

        [Tooltip("Vida de cada ancla. Es el mando principal: mide cuánto daño hay que apartar del " +
                 "jefe para salvar el ritual.")]
        [Min(1f)]
        [SerializeField] private float anchorHealth = 120f;

        [SerializeField] private Vector2 anchorSize = new Vector2(1.3f, 1.6f);

        [Tooltip("Sprite del ancla. Vacío = el del jefe.")]
        [SerializeField] private Sprite anchorSprite;

        [Tooltip("Altura sobre el suelo a la que se plantan.")]
        [Min(0f)]
        [SerializeField] private float anchorHeight = 0.9f;

        [Tooltip("Separación mínima entre anclas. Cuanto más grande, más hay que correr para " +
                 "llegar a todas y menos vale un arma de área.")]
        [Min(0f)]
        [SerializeField] private float minSeparation = 6f;

        [Min(0f)]
        [SerializeField] private float edgeMargin = 2.5f;

        [Header("Ritual")]
        [Tooltip("Segundos para romperlas todas.")]
        [Min(1f)]
        [SerializeField] private float ritualSeconds = 9f;

        [Tooltip("Daño que recibe el jefe mientras el ritual está en pie. Prácticamente nada: es " +
                 "lo que empuja a ir a por las anclas.")]
        [Range(0f, 1f)]
        [SerializeField] private float damageTakenDuringRitual = 0.05f;

        [Header("Si se completa")]
        [Tooltip("Daño del estallido que cubre la arena si no se rompen a tiempo.")]
        [Min(0f)]
        [SerializeField] private float dischargeDamage = 34f;

        [Min(0f)]
        [SerializeField] private float dischargeKnockback = 2f;

        [Tooltip("Segundos de aviso entre que el ritual se completa y estalla. Corto: es un " +
                 "castigo, no otro patrón que esquivar.")]
        [Min(0.1f)]
        [SerializeField] private float dischargeWarning = 0.7f;

        private const float MarkerSeconds = 0.55f;

        public override string ShortStats() =>
            $"{anchorCount} anclas de {anchorHealth:0} · {ritualSeconds:0}s · si falla, {dischargeDamage:0} dmg";

        public override void OnTelegraph(BossContext ctx)
        {
            Warn(ctx, ctx.Origin + new Vector2(0f, 2.6f), Vector2.one * 4.5f, ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            if (!ctx.IsValid) yield break;

            var spots = new float[Mathf.Max(1, anchorCount)];
            PickSpots(ctx, spots);

            for (int i = 0; i < spots.Length; i++)
                Warn(ctx, new Vector2(spots[i], ctx.GroundY + anchorHeight), anchorSize * 1.4f,
                     ctx.Scaled(MarkerSeconds));

            yield return new WaitForSeconds(ctx.Scaled(MarkerSeconds));
            if (!ctx.IsValid) yield break;

            // El jefe se cierra. Ojo: no se usa Invulnerable sino un multiplicador bajísimo, para
            // que los golpes sigan entrando y el jugador VEA el número ridículo que hace — que es
            // como se aprende que hay que ir a otra cosa.
            // Al terminar se devuelve la armadura DE LA FASE, no la que hubiera puesta al empezar:
            // leer el valor vivo heredaría cualquier estado temporal (una guardia, una ventana de
            // castigo) y lo dejaría clavado para el resto del combate.
            var health = ctx.Boss.BossHealth;
            var phase = ctx.Boss.CurrentPhase;
            float restore = phase != null ? Mathf.Max(0.01f, phase.damageTakenMultiplier) : 1f;
            if (health != null) health.DamageMultiplier = damageTakenDuringRitual;

            var anchors = new List<BossAnchor>(spots.Length);
            for (int i = 0; i < spots.Length; i++)
            {
                var anchor = BossAnchor.Spawn(ctx, new Vector2(spots[i], ctx.GroundY + anchorHeight),
                                              anchorSize, anchorHealth, anchorSprite);
                if (anchor != null) anchors.Add(anchor);
            }

            Impact();

            float elapsed = 0f;
            bool broken = false;
            float window = ctx.Scaled(ritualSeconds);

            while (elapsed < window)
            {
                if (!ctx.IsValid) break;

                if (AllBroken(anchors)) { broken = true; break; }

                // La cuerda del ritual: una línea entre el jefe y cada ancla que sigue en pie.
                // Es lo que hace obvio de un vistazo qué queda por romper y dónde está.
                Tether(ctx, anchors);

                yield return new WaitForSeconds(0.2f);
                elapsed += 0.2f;
            }

            if (health != null) health.DamageMultiplier = restore;

            if (!ctx.IsValid)
            {
                for (int i = 0; i < anchors.Count; i++) anchors[i].Release(false);
                yield break;
            }

            if (broken)
            {
                // Premio: el jefe se queda expuesto. La ventana la abre el controlador al entrar en
                // la recuperación con los campos de la clase base.
                for (int i = 0; i < anchors.Count; i++) anchors[i].Release(false);
                yield break;
            }

            // Castigo: se completó. Aviso corto y estallido de arena entera.
            yield return Discharge(ctx, anchors);
        }

        private IEnumerator Discharge(BossContext ctx, List<BossAnchor> anchors)
        {
            var center = new Vector2(ctx.Origin.x, ctx.GroundY + ctx.ArenaHeight * 0.5f);
            var size = new Vector2(ctx.ArenaHalfWidth * 2f, ctx.ArenaHeight);

            Warn(ctx, center, size, ctx.Scaled(dischargeWarning));

            yield return new WaitForSeconds(ctx.Scaled(dischargeWarning));

            for (int i = 0; i < anchors.Count; i++) anchors[i].Release(false);

            if (!ctx.IsValid) yield break;

            Impact();

            AbilityHit.DamageBox(ctx.Ability, center, size, 0f, dischargeDamage, dischargeKnockback, center);

            var flash = ctx.Accent;
            flash.a = 0.75f;
            AbilityFx.Flash(ctx.FxSprite, center, size, flash, 0.5f, 0f, 1.1f, ctx.Ability.Caster);
        }

        private static bool AllBroken(List<BossAnchor> anchors)
        {
            for (int i = 0; i < anchors.Count; i++)
                if (anchors[i] != null && anchors[i].IsStanding) return false;

            return true;
        }

        /// <summary>Une el jefe con cada ancla viva por una línea, para que se vean desde lejos.</summary>
        private void Tether(in BossContext ctx, List<BossAnchor> anchors)
        {
            Vector2 from = ctx.Origin + new Vector2(0f, 2.2f);

            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null || !anchor.IsStanding) continue;

                Vector2 to = anchor.transform.position;
                Vector2 delta = to - from;
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

                Warn(ctx, from + delta * 0.5f, new Vector2(delta.magnitude, 0.18f), 0.3f, angle, 0.6f);
            }
        }

        private void PickSpots(in BossContext ctx, float[] spots)
        {
            float min = ctx.ArenaMinX + edgeMargin;
            float max = ctx.ArenaMaxX - edgeMargin;
            if (max <= min) { min = ctx.ArenaMinX; max = ctx.ArenaMaxX; }

            for (int i = 0; i < spots.Length; i++)
            {
                bool placed = false;

                for (int attempt = 0; attempt < 24 && !placed; attempt++)
                {
                    float candidate = Random.Range(min, max);
                    if (!IsFarEnough(candidate, spots, i)) continue;

                    spots[i] = candidate;
                    placed = true;
                }

                if (!placed)
                {
                    float step = spots.Length > 1 ? (max - min) / (spots.Length - 1) : 0f;
                    spots[i] = spots.Length > 1 ? min + step * i : (min + max) * 0.5f;
                }
            }
        }

        private bool IsFarEnough(float candidate, float[] spots, int count)
        {
            for (int i = 0; i < count; i++)
                if (Mathf.Abs(spots[i] - candidate) < minSeparation) return false;

            return true;
        }
    }
}
