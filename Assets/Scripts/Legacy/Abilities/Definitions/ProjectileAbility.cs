using System.Collections;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Dispara uno o varios proyectiles. Con los mismos campos salen una bola de fuego, una
    /// escopeta de 5 perdigones, una lanza que atraviesa, un orbe teledirigido o una granada: lo
    /// que cambia es el <see cref="ProjectileSpec"/> y cuántos salen a la vez.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Abilities/Projectile", fileName = "Ability_Projectile")]
    public class ProjectileAbility : AbilityDefinition
    {
        [Header("Disparo")]
        [SerializeField] private ProjectileSpec projectile = new ProjectileSpec();

        [Tooltip("Proyectiles por disparo (perdigones de escopeta).")]
        [Min(1)]
        [SerializeField] private int projectilesPerShot = 1;

        [Tooltip("Abanico total en grados repartido entre esos proyectiles. 0 = todos rectos.")]
        [Min(0f)]
        [SerializeField] private float spreadAngle;

        [Tooltip("Desviación aleatoria extra por proyectil, en grados. Da sensación de arma sucia.")]
        [Min(0f)]
        [SerializeField] private float randomSpread;

        [Header("Ráfaga")]
        [Tooltip("Disparos seguidos que da un solo uso.")]
        [Min(1)]
        [SerializeField] private int burstCount = 1;
        [Min(0.01f)]
        [SerializeField] private float burstInterval = 0.1f;

        public override string ShortStats()
        {
            string shots = projectilesPerShot > 1 ? $" ×{projectilesPerShot}" : "";
            string burst = burstCount > 1 ? $" · ráfaga {burstCount}" : "";
            string extra = projectile.pierce > 0 ? " · perfora"
                         : projectile.homingTurnRate > 0f ? " · teledirigido"
                         : projectile.impactRadius > 0f ? " · explosiva"
                         : "";
            return $"{Damage:0} dmg{shots}{burst}{extra} · {Cooldown:0.00}s";
        }

        public override void Execute(AbilityContext ctx)
        {
            if (burstCount <= 1)
            {
                FireVolley(ctx);
                return;
            }

            ctx.Runner.StartCoroutine(FireBurst(ctx));
        }

        private IEnumerator FireBurst(AbilityContext ctx)
        {
            for (int i = 0; i < burstCount; i++)
            {
                if (ctx.Caster == null) yield break;

                FireVolley(ctx);
                yield return new WaitForSeconds(burstInterval);
            }
        }

        private void FireVolley(in AbilityContext ctx)
        {
            Vector2 origin = ctx.Muzzle(projectile.muzzleOffset);
            float baseAngle = Mathf.Atan2(ctx.Aim.y, ctx.Aim.x) * Mathf.Rad2Deg;

            // El abanico se reparte centrado en la dirección de apuntado: con un solo proyectil
            // sale recto, con tres sale uno al centro y dos a los lados.
            float step = projectilesPerShot > 1 ? spreadAngle / (projectilesPerShot - 1) : 0f;
            float start = baseAngle - spreadAngle * 0.5f;

            for (int i = 0; i < projectilesPerShot; i++)
            {
                float angle = (projectilesPerShot > 1 ? start + step * i : baseAngle)
                              + Random.Range(-randomSpread, randomSpread);

                var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                ProjectileFactory.Spawn(ctx, projectile, origin, direction,
                                        Damage * ctx.DamageScale, KnockbackMultiplier,
                                        FxSprite, Accent);
            }
        }
    }
}
