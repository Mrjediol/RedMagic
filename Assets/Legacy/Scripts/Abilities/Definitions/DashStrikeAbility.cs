using System.Collections;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Embestida: el personaje sale disparado hacia delante y hace daño a todo lo que atraviesa.
    ///
    /// El desplazamiento reutiliza <see cref="Knockback"/> — el mismo componente que ya sabe mover
    /// tanto a un cuerpo físico como al controlador cinemático del jugador —, así que la embestida
    /// no necesita saber cómo se mueve quien la lanza.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Abilities/Dash Strike", fileName = "Ability_Dash")]
    public class DashStrikeAbility : AbilityDefinition
    {
        [Header("Embestida")]
        [Min(1f)]
        [SerializeField] private float dashSpeed = 22f;

        [Min(0.05f)]
        [SerializeField] private float dashDuration = 0.2f;

        [Tooltip("Componente vertical del impulso. Positivo levanta un poco al embestir.")]
        [SerializeField] private float dashLift;

        [Header("Daño en el recorrido")]
        [Tooltip("Tamaño de la caja que va barriendo por delante durante la embestida.")]
        [SerializeField] private Vector2 hitSize = new Vector2(1.5f, 1.2f);

        [Tooltip("Cada objetivo recibe un solo golpe por embestida, aunque se le atraviese entero.")]
        [SerializeField] private bool oneHitPerTarget = true;

        [Header("Defensa")]
        [Tooltip("Invulnerable mientras dura la embestida (esquiva ofensiva).")]
        [SerializeField] private bool invulnerableWhileDashing = true;

        public override string ShortStats() =>
            $"{Damage:0} dmg · embestida {dashSpeed:0}u/s{(invulnerableWhileDashing ? " · invulnerable" : "")}";

        public override void Execute(AbilityContext ctx)
        {
            ctx.Runner.StartCoroutine(DashRoutine(ctx));
        }

        private IEnumerator DashRoutine(AbilityContext ctx)
        {
            var knockback = ctx.Caster != null ? ctx.Caster.GetComponent<Knockback>() : null;
            var health = ctx.CasterHealth;

            // El impulso va por ApplyVelocity y no por Apply: aquí la dirección y la fuerza las
            // pone la habilidad, no la resistencia al empujón del personaje.
            knockback?.ApplyVelocity(new Vector2(ctx.Facing * dashSpeed, dashLift), dashDuration);

            bool restoreInvulnerable = false;
            if (invulnerableWhileDashing && health != null && !health.Invulnerable)
            {
                health.Invulnerable = true;
                restoreInvulnerable = true;
            }

            Vector2 hitSize = this.hitSize * ctx.SizeScale;   // el nivel ensancha el barrido

            var alreadyHit = oneHitPerTarget ? new System.Collections.Generic.HashSet<Health>() : null;
            float elapsed = 0f;

            while (elapsed < dashDuration && ctx.Caster != null)
            {
                Vector2 center = ctx.Origin + new Vector2(ctx.Facing * hitSize.x * 0.35f, 0f);
                var targets = AbilityHit.OverlapBox(ctx, center, hitSize);

                // Se copia el índice hacia atrás: dañar puede matar y desactivar colliders, y la
                // lista es un buffer compartido que no se debe conservar entre frames.
                for (int i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (alreadyHit != null && !alreadyHit.Add(target)) continue;

                    AbilityHit.Damage(target, ctx, Damage, ctx.Origin, KnockbackMultiplier);
                }

                AbilityFx.Flash(FxSprite, center, hitSize * 0.8f, Accent * new Color(1f, 1f, 1f, 0.35f),
                                0.1f, 0f, 1f, ctx.Caster);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (restoreInvulnerable && health != null) health.Invulnerable = false;
        }
    }
}
