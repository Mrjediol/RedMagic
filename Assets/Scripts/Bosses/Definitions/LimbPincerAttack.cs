using System.Collections;
using RedMagic.Abilities;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Tenaza — <b>"¿salgo por arriba o por un lado antes de que se cierre?"</b>. Dos piezas
    /// vuelan a los dos lados de donde está el jugador, se quedan apuntándose (la franja entre ellas
    /// se marca: es lo que se va a aplastar) y se cierran de golpe en el centro. La franja es baja
    /// (<see cref="clapHeight"/>), así que un salto a tiempo la esquiva igual que salir corriendo;
    /// lo que no vale es quedarse.
    ///
    /// Sólo tiene sentido con dos piezas sueltas: es el ataque que un jefe de un solo sprite no puede
    /// hacer. Sin ellas marca la franja y la golpea igual (sin pieza visible).
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Limb Pincer Attack", fileName = "BossAttack_LimbPincer")]
    public class LimbPincerAttack : LimbAttack
    {
        [Header("Tenaza")]
        [Tooltip("Distancia de cada pieza al centro antes de cerrarse.")]
        [Min(0.5f)]
        [SerializeField] private float spread = 3.2f;

        [Tooltip("Alto de la franja que se aplasta. Bajo = se puede saltar.")]
        [Min(0.2f)]
        [SerializeField] private float clapHeight = 1.6f;

        [Header("Tiempos")]
        [Tooltip("Segundos en llegar a los lados.")]
        [Min(0.05f)]
        [SerializeField] private float spreadSeconds = 0.5f;

        [Tooltip("Segundos apuntándose antes de cerrar: el aviso.")]
        [Min(0f)]
        [SerializeField] private float holdSeconds = 0.5f;

        [Tooltip("Segundos en cerrarse.")]
        [Min(0.02f)]
        [SerializeField] private float clapSeconds = 0.13f;

        [Tooltip("Segundos del rebote tras chocar.")]
        [Min(0f)]
        [SerializeField] private float reboundSeconds = 0.18f;

        [Tooltip("Segundos de vuelta al cuerpo (lo anima el rig).")]
        [Min(0.05f)]
        [SerializeField] private float returnSeconds = 0.5f;

        public override string ShortStats() =>
            $"{Damage:0} dmg · franja {spread * 2f:0.#}×{clapHeight:0.#} · aviso {holdSeconds:0.00}s";

        public override IEnumerator Run(BossContext ctx)
        {
            var limbs = Limbs(ctx);
            float radius = limbs != null ? limbs.ContactRadius : 0.8f;

            Vector2 target = AboveGround(ctx, ctx.PlayerCenter, radius);
            var zone = new Vector2(spread * 2f, clapHeight);
            float gather = ctx.Scaled(spreadSeconds), hold = ctx.Scaled(holdSeconds), clap = ctx.Scaled(clapSeconds);

            Warn(ctx, target, zone, gather + hold + clap);

            Transform[] parts = null;
            Vector2[] sides = { target + Vector2.left * spread, target + Vector2.right * spread };
            Vector2[] closed = { target + Vector2.left * radius * 0.6f, target + Vector2.right * radius * 0.6f };

            if (limbs != null && limbs.Count >= 2)
            {
                // La pieza que está más a la izquierda va a la izquierda: nunca se cruzan.
                var a = limbs.Get(0);
                var b = limbs.Get(1);
                if (limbs.Center(a).x > limbs.Center(b).x) (a, b) = (b, a);
                parts = new[] { a, b };

                // Los nudillos se miran entre sí, hacia lo que van a aplastar.
                var inward = Aim(Vector2.right, Vector2.left);

                yield return Fly(ctx, limbs, parts, new[] { limbs.Center(a), limbs.Center(b) }, sides, gather,
                                 Ease.Out, inward);
                yield return Hold(ctx, limbs, parts, sides, hold, inward, tremble);
                yield return Fly(ctx, limbs, parts, sides, closed, clap, Ease.In, inward);
            }
            else
            {
                yield return new WaitForSeconds(gather + hold + clap);
            }

            if (!ctx.IsValid) yield break;

            // --- choque: todo lo que siga en la franja queda aplastado.
            AbilityHit.DamageBox(ctx.Ability, target, zone, 0f, ScaledDamage(ctx), KnockbackMultiplier, target);
            BossHitboxDebug.Box(target, zone, new Color(1f, 0.3f, 0.2f, 1f));
            ImpactAt(ctx, target, new Vector2(radius * 3f, clapHeight * 1.4f));

            if (parts == null) yield break;

            // Rebote: se separan un poco por el choque y vuelven al cuerpo.
            Vector2[] rebound = { target + Vector2.left * radius * 1.6f, target + Vector2.right * radius * 1.6f };
            yield return Fly(ctx, limbs, parts, closed, rebound, ctx.Scaled(reboundSeconds), Ease.Out, Aim(Vector2.right, Vector2.left));
            foreach (var part in parts) limbs.Release(part, ctx.Scaled(returnSeconds));
        }
    }
}
