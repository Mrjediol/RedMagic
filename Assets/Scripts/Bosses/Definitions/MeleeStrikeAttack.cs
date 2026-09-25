using System.Collections;
using RedMagic.Abilities;
using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Golpe cuerpo a cuerpo delante del jefe — <b>"¿estoy demasiado cerca?"</b>. Una caja de daño a
    /// su lado del jugador, una o varias veces seguidas, con un efecto de impacto en cada golpe y,
    /// opcionalmente, un <b>proyectil</b> que sale del golpe (la onda de un puñetazo).
    ///
    /// Con los mismos campos salen:
    ///  - <b>puñetazo potente</b>: 1 golpe, proyectil hacia delante (castiga también a distancia);
    ///  - <b>ráfaga de puños</b>: 3-4 golpes rápidos sin proyectil (castiga quedarse pegado);
    ///  - un zarpazo de enemigo normal: 1 golpe corto, sin nada más.
    ///
    /// El primer golpe sale en el frame de suelta del gesto (el aviso es el cuerpo); los siguientes,
    /// cada <see cref="timeBetweenHits"/> — ajústalo a los frames en que el dibujo golpea.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Melee Strike Attack", fileName = "BossAttack_Melee")]
    [AttackBox("hitboxOffset", "hitboxSize", "Golpe")]
    [AttackPoint("projectile.muzzleOffset", "Salida del proyectil")]
    public class MeleeStrikeAttack : BossAttack
    {
        [Header("Golpes")]
        [Min(1)]
        [SerializeField] private int hits = 1;

        [Tooltip("Segundos entre golpes (con varios). Cuadra con los frames de golpe del dibujo.")]
        [Min(0.02f)]
        [SerializeField] private float timeBetweenHits = 0.25f;

        [Header("Alcance (relativo al jefe, X hacia el jugador)")]
        [Tooltip("Centro de la caja de daño: X hacia delante, Y hacia arriba desde los pies.")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(2.4f, 1.8f);

        [SerializeField] private Vector2 hitboxSize = new Vector2(3.2f, 2.6f);

        [Tooltip("Pinta la caja de daño durante el aviso. Apágalo si el gesto ya se lee solo.")]
        [SerializeField] private bool warnHitbox = true;

        [Header("Proyectil del golpe (opcional)")]
        [Tooltip("El golpe lanza además un proyectil (la onda del puñetazo).")]
        [SerializeField] private bool launchProjectile;

        [Tooltip("Sólo en el primer golpe (apagado) o en cada golpe (encendido).")]
        [SerializeField] private bool projectileEveryHit;

        [Tooltip("Apunta al jugador. Apagado = recto hacia delante.")]
        [SerializeField] private bool aimAtPlayer;

        [Tooltip("Daño del proyectil. 0 = el mismo que el golpe.")]
        [Min(0f)]
        [SerializeField] private float projectileDamage;

        [ArtSlot("Proyectil del golpe", ArtSlotKind.Projectile, "bola de color de la fase")]
        [Tooltip("Cómo vuela y cómo se ve el proyectil. prefab vacío = bola de color. 'muzzleOffset' es " +
                 "de dónde sale (X hacia delante).")]
        [SerializeField] private ProjectileSpec projectile = new ProjectileSpec
        {
            speed = 14f,
            lifetime = 1.6f,
            size = new Vector2(1.4f, 1.4f),
            muzzleOffset = new Vector2(3f, 1.8f),
        };

        [Header("Arte")]
        [ArtSlot("Impacto de cada golpe", ArtSlotKind.Fx, "destello de color")]
        [Tooltip("Efecto de un solo uso (VfxOneShot) en el centro de la caja en cada golpe. Vacío = " +
                 "destello del color de la fase.")]
        [SerializeField] private GameObject impactFxPrefab;

        [Tooltip("Escala el efecto para que mida esto de ancho. 0 = escala del prefab.")]
        [Min(0f)]
        [SerializeField] private float impactFxWidth;

        public override string ShortStats() =>
            $"{Damage:0} dmg · {hits} golpe(s) · caja {hitboxSize.x:0.#}×{hitboxSize.y:0.#}" +
            (launchProjectile ? " · + proyectil" : "");

        public override void OnTelegraph(BossContext ctx)
        {
            if (warnHitbox) Warn(ctx, HitboxCenter(ctx), hitboxSize, ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            for (int i = 0; i < hits; i++)
            {
                if (!ctx.IsValid) yield break;

                Strike(ctx);

                if (launchProjectile && (i == 0 || projectileEveryHit)) Launch(ctx);

                if (i < hits - 1) yield return new WaitForSeconds(ctx.Scaled(timeBetweenHits));
            }
        }

        private Vector2 HitboxCenter(in BossContext ctx) =>
            ctx.Origin + new Vector2(hitboxOffset.x * ctx.Facing, hitboxOffset.y);

        private void Strike(in BossContext ctx)
        {
            Vector2 center = HitboxCenter(ctx);

            Impact();
            AbilityHit.DamageBox(ctx.Ability, center, hitboxSize, 0f, Damage, KnockbackMultiplier, ctx.Origin);
            BossHitboxDebug.Box(center, hitboxSize, new Color(1f, 0.3f, 0.2f, 1f));

            if (impactFxPrefab != null)
            {
                var at = new Vector3(center.x, center.y, 0f);
                if (impactFxWidth > 0f) VfxOneShot.SpawnFitWidth(impactFxPrefab, at, impactFxWidth, ctx.Facing);
                else VfxOneShot.Spawn(impactFxPrefab, at, ctx.Facing);
                return;
            }

            var flash = ctx.Accent;
            flash.a = 0.7f;
            AbilityFx.Flash(ctx.FxSprite, center, hitboxSize, flash, 0.22f, 0f, 1.2f, ctx.Ability.Caster);
        }

        private void Launch(in BossContext ctx)
        {
            Vector2 origin = ctx.Origin + new Vector2(projectile.muzzleOffset.x * ctx.Facing, projectile.muzzleOffset.y);

            Vector2 direction = new Vector2(ctx.Facing, 0f);
            if (aimAtPlayer)
            {
                Vector2 delta = ctx.PlayerCenter - origin;
                if (delta.sqrMagnitude > 0.0001f) direction = delta.normalized;
            }

            float damage = projectileDamage > 0f ? projectileDamage * ctx.Ability.DamageScale : ScaledDamage(ctx);
            ProjectileFactory.Spawn(ctx.Ability, projectile, origin, direction, damage, KnockbackMultiplier,
                                    ctx.FxSprite, ctx.Accent);
        }
    }
}
