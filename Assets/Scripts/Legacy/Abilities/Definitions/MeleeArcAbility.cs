using System.Collections;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Golpe cuerpo a cuerpo: abre una caja de daño delante (o alrededor) del personaje durante
    /// unos frames, opcionalmente varias veces seguidas.
    ///
    /// Cubre desde una daga rápida hasta un mandoble lento o un torbellino que golpea a los dos
    /// lados: son los mismos tres números (tamaño de la caja, número de golpes y ritmo) con
    /// valores distintos.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Abilities/Melee Arc", fileName = "Ability_Melee")]
    public class MeleeArcAbility : AbilityDefinition
    {
        [Header("Caja de daño")]
        [Tooltip("Centro de la caja respecto al personaje. La X se invierte según hacia dónde mira.")]
        [SerializeField] private Vector2 offset = new Vector2(0.9f, 0.1f);
        [SerializeField] private Vector2 size = new Vector2(1.4f, 1.2f);
        [Tooltip("Inclinación de la caja en grados (positivo = hacia arriba). Un uppercut son " +
                 "unos 45°; un barrido bajo, negativos.")]
        [SerializeField] private float angle;
        [Tooltip("Golpea también a la espalda (torbellino).")]
        [SerializeField] private bool bothSides;

        [Header("Cadena de golpes")]
        [Tooltip("Cuántos golpes seguidos da un solo uso.")]
        [Min(1)]
        [SerializeField] private int hits = 1;
        [Tooltip("Segundos entre golpes de la cadena.")]
        [Min(0.01f)]
        [SerializeField] private float timeBetweenHits = 0.12f;

        [Header("Empujón vertical")]
        [Tooltip("Multiplicador de retroceso extra hacia arriba. El Knockback del objetivo ya " +
                 "levanta un poco; súbelo para un lanzamiento tipo uppercut.")]
        [Min(0f)]
        [SerializeField] private float launchMultiplier = 1f;

        public override string ShortStats() =>
            hits > 1
                ? $"{Damage:0} × {hits} golpes · {Cooldown:0.00}s"
                : $"{Damage:0} dmg · caja {size.x:0.0}×{size.y:0.0} · {Cooldown:0.00}s";

        public override void Execute(AbilityContext ctx)
        {
            if (hits <= 1)
            {
                Strike(ctx);
                return;
            }

            ctx.Runner.StartCoroutine(StrikeChain(ctx));
        }

        private IEnumerator StrikeChain(AbilityContext ctx)
        {
            for (int i = 0; i < hits; i++)
            {
                if (ctx.Caster == null) yield break;

                Strike(ctx);
                yield return new WaitForSeconds(timeBetweenHits);
            }
        }

        private void Strike(in AbilityContext ctx)
        {
            HitSide(ctx, ctx.Facing);
            if (bothSides) HitSide(ctx, -ctx.Facing);
        }

        private void HitSide(in AbilityContext ctx, int facing)
        {
            // El nivel del arma agranda la caja y la aleja un poco: golpea más lejos y más ancho.
            Vector2 size = this.size * ctx.SizeScale;
            Vector2 offset = this.offset * ctx.SizeScale;

            Vector2 center = ctx.Origin + new Vector2(offset.x * facing, offset.y);
            float rotation = angle * facing;

            // El empujón sale del personaje, no del centro de la caja: si saliera de la caja, un
            // enemigo que está entre el personaje y el centro saldría despedido hacia atrás.
            AbilityHit.DamageBox(ctx, center, size, rotation, Damage,
                                 KnockbackMultiplier * launchMultiplier, ctx.Origin);

            AbilityFx.Flash(FxSprite, center, size, Accent * new Color(1f, 1f, 1f, 0.55f),
                            0.14f, rotation, 1.15f, ctx.Caster);
        }
    }
}
