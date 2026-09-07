using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Deja una zona de daño en el escenario: un charco que va quemando mientras estén dentro, o
    /// una mina que espera a que alguien se acerque y revienta.
    ///
    /// Es la familia de habilidades que separa el "daño ahora" del "daño donde estará": el control
    /// del terreno es media construcción de roguelite, y con esto ya se puede probar.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Abilities/Zone", fileName = "Ability_Zone")]
    public class ZoneAbility : AbilityDefinition
    {
        public enum Placement
        {
            /// <summary>A los pies de quien la lanza.</summary>
            AtCaster,

            /// <summary>Un poco por delante, en la dirección hacia la que mira.</summary>
            InFront
        }

        [Header("Colocación")]
        [SerializeField] private Placement placement = Placement.AtCaster;

        [Tooltip("Distancia por delante cuando se coloca delante.")]
        [SerializeField] private float forwardDistance = 1.5f;

        [SerializeField] private Vector2 offset = new Vector2(0f, -0.35f);

        [Header("Zona")]
        [SerializeField] private DamageZone.Mode mode = DamageZone.Mode.Continuous;

        [Min(0.1f)]
        [SerializeField] private float radius = 1.6f;

        [Tooltip("Segundos que dura antes de desaparecer sola.")]
        [Min(0.1f)]
        [SerializeField] private float duration = 4f;

        [Tooltip("Segundos entre golpes (sólo en modo continuo).")]
        [Min(0.05f)]
        [SerializeField] private float tickInterval = 0.5f;

        [Tooltip("Segundos antes de armarse: evita que una mina explote en tu propia cara al ponerla.")]
        [Min(0f)]
        [SerializeField] private float armDelay = 0.2f;

        public override string ShortStats() =>
            mode == DamageZone.Mode.ProximityBomb
                ? $"{Damage:0} dmg al pisarla · radio {radius:0.0}"
                : $"{Damage:0} dmg/{tickInterval:0.0}s · {duration:0}s · radio {radius:0.0}";

        public override void Execute(AbilityContext ctx)
        {
            Vector2 position = ctx.Origin + offset;
            if (placement == Placement.InFront) position += new Vector2(ctx.Facing * forwardDistance, 0f);

            // El nivel del arma agranda la zona (charco más grande, mina con más alcance).
            float radius = this.radius * ctx.SizeScale;

            var visualColor = Accent;
            visualColor.a = mode == DamageZone.Mode.ProximityBomb ? 0.85f : 0.45f;

            var go = AbilityFx.SpawnSprite($"Zone ({DisplayName})", FxSprite, position,
                                           Vector2.one * radius * 2f, visualColor, 0f, ctx.Caster);

            var zone = go.AddComponent<DamageZone>();
            zone.Configure(ctx, mode, radius, Damage, duration, tickInterval, KnockbackMultiplier,
                           armDelay, FxSprite, Accent);
        }
    }
}
