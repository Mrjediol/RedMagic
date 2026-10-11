using System.Collections;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Explosión radial centrada en el personaje: onda de choque, pisotón sísmico, aura de fuego.
    /// Golpea a todo lo que haya en un radio, y opcionalmente lo repite varias veces (un aura que
    /// pulsa).
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Abilities/Nova", fileName = "Ability_Nova")]
    public class NovaAbility : AbilityDefinition
    {
        [Header("Onda")]
        [Min(0.1f)]
        [SerializeField] private float radius = 3f;

        [Tooltip("Desplazamiento del centro respecto al personaje (a los pies, por ejemplo).")]
        [SerializeField] private Vector2 offset = new Vector2(0f, -0.2f);

        [Tooltip("Pulsos que da un solo uso. >1 convierte la onda en un aura que late.")]
        [Min(1)]
        [SerializeField] private int pulses = 1;

        [Min(0.05f)]
        [SerializeField] private float timeBetweenPulses = 0.35f;

        [Header("Condiciones")]
        [Tooltip("Sólo se puede lanzar tocando el suelo (pisotones).")]
        [SerializeField] private bool requiresGround;

        public override string ShortStats() =>
            pulses > 1
                ? $"{Damage:0} dmg · radio {radius:0.0} · {pulses} pulsos"
                : $"{Damage:0} dmg · radio {radius:0.0} · {Cooldown:0.00}s";

        public override bool CanCast(in AbilityContext ctx)
        {
            if (!requiresGround) return true;

            var movement = ctx.Caster != null
                ? ctx.Caster.GetComponent<Gameplay.PlayerMovement>()
                : null;

            // Sin controlador de jugador (un enemigo lanzándola) no hay forma de saberlo: se deja
            // pasar en vez de bloquear la habilidad para siempre.
            return movement == null || movement.IsGrounded;
        }

        public override void Execute(AbilityContext ctx)
        {
            if (pulses <= 1)
            {
                Pulse(ctx);
                return;
            }

            ctx.Runner.StartCoroutine(PulseRoutine(ctx));
        }

        private IEnumerator PulseRoutine(AbilityContext ctx)
        {
            for (int i = 0; i < pulses; i++)
            {
                if (ctx.Caster == null) yield break;

                Pulse(ctx);
                yield return new WaitForSeconds(timeBetweenPulses);
            }
        }

        private void Pulse(in AbilityContext ctx)
        {
            Vector2 center = ctx.Origin + offset;
            float radius = this.radius * ctx.SizeScale;   // el nivel del arma agranda la onda

            AbilityHit.DamageCircle(ctx, center, radius, Damage, KnockbackMultiplier);

            // El efecto sale pequeño y crece hasta el radio real: así se ve de dónde salió la onda
            // y hasta dónde llegaba, que es justo lo que hay que leer para esquivarla.
            AbilityFx.Flash(FxSprite, center, Vector2.one * radius, Accent * new Color(1f, 1f, 1f, 0.5f),
                            0.3f, 0f, 2.2f, ctx.Caster);
        }
    }
}
