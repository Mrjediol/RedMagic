using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Orbes que giran alrededor del personaje durante unos segundos y dañan a lo que tocan.
    /// Es daño pasivo mientras te mueves: la pieza que en un roguelite combina con todo lo demás,
    /// porque no gasta el botón de ataque.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Abilities/Orbit", fileName = "Ability_Orbit")]
    public class OrbitAbility : AbilityDefinition
    {
        [Header("Orbes")]
        [Min(1)]
        [SerializeField] private int orbCount = 3;

        [Min(0.2f)]
        [SerializeField] private float orbitRadius = 1.6f;

        [SerializeField] private Vector2 orbSize = new Vector2(0.4f, 0.4f);

        [Tooltip("Radio de daño de cada orbe.")]
        [Min(0.1f)]
        [SerializeField] private float orbHitRadius = 0.5f;

        [Header("Movimiento")]
        [Tooltip("Velocidad de giro en grados por segundo. Negativo = al revés.")]
        [SerializeField] private float degreesPerSecond = 220f;

        [Min(0.2f)]
        [SerializeField] private float duration = 6f;

        [Tooltip("Segundos entre golpes de un mismo orbe.")]
        [Min(0.05f)]
        [SerializeField] private float tickInterval = 0.4f;

        [SerializeField] private Vector2 pivotOffset = new Vector2(0f, 0.1f);

        public override string ShortStats() =>
            $"{orbCount} orbes · {Damage:0} dmg/{tickInterval:0.0}s · {duration:0}s";

        public override void Execute(AbilityContext ctx)
        {
            var pivot = new GameObject($"Orbit ({DisplayName})");
            pivot.transform.position = ctx.Origin + pivotOffset;

            var spinner = pivot.AddComponent<OrbitSpinner>();
            spinner.Configure(ctx.Caster != null ? ctx.Caster.transform : null,
                              degreesPerSecond, duration, pivotOffset);

            // El nivel del arma sube el radio de la órbita y el alcance de cada orbe.
            float orbitRadius = this.orbitRadius * ctx.SizeScale;
            float orbHitRadius = this.orbHitRadius * ctx.SizeScale;
            Vector2 orbSize = this.orbSize * ctx.SizeScale;

            for (int i = 0; i < orbCount; i++)
            {
                float angle = 360f / orbCount * i;
                var local = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad),
                                        Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * orbitRadius;

                var orb = AbilityFx.SpawnSprite("Orb", FxSprite, pivot.transform.position + local,
                                                orbSize, Accent, 0f, ctx.Caster);

                var zone = orb.AddComponent<DamageZone>();
                zone.Configure(ctx, DamageZone.Mode.Continuous, orbHitRadius, Damage, duration,
                               tickInterval, KnockbackMultiplier, 0f, FxSprite, Accent);

                // Colgar del pivote es lo que los hace girar: el pivote rota y ellos van montados.
                zone.Attach(pivot.transform, local);
            }
        }
    }
}
