using System.Collections;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Habilidad de apoyo: cura, te vuelve invulnerable un momento y/o sube tu daño un rato.
    /// No hace daño por sí misma — está para probar que el botón de ataque puede llevar algo que
    /// no sea un golpe, que es lo que hace falta si luego hay construcciones con espacios de
    /// utilidad.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Abilities/Buff", fileName = "Ability_Buff")]
    public class BuffAbility : AbilityDefinition
    {
        [Header("Curación")]
        [Min(0f)]
        [SerializeField] private float healAmount = 15f;

        [Header("Invulnerabilidad")]
        [Tooltip("Segundos de invulnerabilidad total al lanzarla. 0 = ninguna.")]
        [Min(0f)]
        [SerializeField] private float invulnerabilitySeconds = 1.2f;

        [Header("Daño")]
        [Tooltip("Multiplicador de daño mientras dura el buff. 1 = sin cambio.")]
        [Min(0.1f)]
        [SerializeField] private float damageMultiplier = 1f;

        [Min(0f)]
        [SerializeField] private float damageBuffSeconds = 5f;

        public override string ShortStats()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (healAmount > 0f) parts.Add($"+{healAmount:0} vida");
            if (invulnerabilitySeconds > 0f) parts.Add($"{invulnerabilitySeconds:0.0}s invulnerable");
            if (damageMultiplier > 1f) parts.Add($"×{damageMultiplier:0.0} daño {damageBuffSeconds:0}s");
            return parts.Count > 0 ? string.Join(" · ", parts) : "sin efecto";
        }

        public override void Execute(AbilityContext ctx)
        {
            if (healAmount > 0f && ctx.CasterHealth != null) ctx.CasterHealth.Heal(healAmount);

            if (damageMultiplier > 1f && damageBuffSeconds > 0f && ctx.Runner is AbilityUser user)
                user.AddDamageBuff(damageMultiplier, damageBuffSeconds);

            AbilityFx.Flash(FxSprite, ctx.Origin, Vector2.one * 1.8f,
                            Accent * new Color(1f, 1f, 1f, 0.55f), 0.4f, 0f, 1.8f, ctx.Caster);

            if (invulnerabilitySeconds > 0f) ctx.Runner.StartCoroutine(InvulnerabilityRoutine(ctx));
        }

        private IEnumerator InvulnerabilityRoutine(AbilityContext ctx)
        {
            var health = ctx.CasterHealth;
            if (health == null || health.Invulnerable) yield break;

            health.Invulnerable = true;
            yield return new WaitForSeconds(invulnerabilitySeconds);

            // Puede haber muerto o cambiado de escena mientras tanto: se comprueba antes de tocarlo.
            if (health != null) health.Invulnerable = false;
        }
    }
}
