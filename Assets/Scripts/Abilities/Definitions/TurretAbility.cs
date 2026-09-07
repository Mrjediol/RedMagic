using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Planta un tótem que dispara solo a los enemigos cercanos durante unos segundos. Es la
    /// habilidad de "poner daño en el mapa" y la que mejor enseña si el sistema de bandos funciona:
    /// hereda el bando de quien la lanza.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Abilities/Turret", fileName = "Ability_Turret")]
    public class TurretAbility : AbilityDefinition
    {
        [Header("Tótem")]
        [SerializeField] private Vector2 turretSize = new Vector2(0.6f, 0.9f);
        [SerializeField] private Vector2 offset = new Vector2(1.2f, 0f);

        [Min(0.2f)]
        [SerializeField] private float duration = 8f;

        [Min(1f)]
        [SerializeField] private float range = 7f;

        [Min(0.05f)]
        [SerializeField] private float fireInterval = 0.8f;

        [Header("Disparo")]
        [SerializeField] private ProjectileSpec projectile = new ProjectileSpec();

        public override string ShortStats() =>
            $"{Damage:0} dmg/disparo · {duration:0}s · alcance {range:0}";

        public override void Execute(AbilityContext ctx)
        {
            Vector2 position = ctx.Origin + new Vector2(offset.x * ctx.Facing, offset.y);

            var go = AbilityFx.SpawnSprite($"Turret ({DisplayName})", FxSprite, position,
                                           turretSize, Accent, 0f, ctx.Caster);

            var turret = go.AddComponent<AbilityTurret>();
            turret.Configure(ctx, projectile, Damage * ctx.DamageScale, KnockbackMultiplier,
                             range * ctx.SizeScale, fireInterval, duration, FxSprite, Accent);
        }
    }
}
