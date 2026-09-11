using System.Collections;
using RedMagic.Abilities;
using RedMagic.Combat;
using RedMagic.Fx;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// El pisotón que hace temblar la arena entera: el jefe hunde las raíces, la cámara tiembla
    /// como en un terremoto y <b>quien tenga los pies en el suelo de la arena</b> se lleva el golpe.
    ///
    /// Pregunta <b>"¿estás en el suelo cuando cae?"</b>. No hay radio del que salir: la única
    /// respuesta es estar en el aire o subido a una plataforma en el instante del impacto. Lo que
    /// se lee es el gesto del cuerpo, así que el tiempo del gesto ES el ataque.
    ///
    /// El impacto es el frame de suelta del gesto (el evento OnAttackRelease de su fila), que
    /// <see cref="BossAnimator"/> ajusta para que caiga a los <see cref="impactDelay"/> segundos de
    /// empezar. Sin gesto, es un aviso por tiempo de la misma duración.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Quake Slam Attack", fileName = "BossAttack_Quake")]
    public class QuakeSlamAttack : BossAttack
    {
        [Header("Golpe")]
        [Tooltip("Segundos desde que empieza el gesto hasta que el golpe toca el suelo (sacudida + " +
                 "daño). Sustituye al 'telegraph' del ataque. Es a ritmo 1: la fase lo divide por su " +
                 "speedScale. Si obliga al clip a ir más rápido o más lento de lo que permite el " +
                 "BossAnimator (gestureSpeedRange), manda el dibujo y el golpe cae en su frame.")]
        [Min(0.1f)]
        [SerializeField] private float impactDelay = 1f;

        [Header("¿Está en el suelo?")]
        [Tooltip("Cuánto por encima del suelo de la arena pueden estar los pies del jugador y seguir " +
                 "contando como 'en el suelo'. Deja fuera los salientes sólidos elevados (capa Ground) " +
                 "aunque no sean plataforma; estar en una plataforma (capa Platform) o en el aire " +
                 "salva siempre.")]
        [Min(0f)]
        [SerializeField] private float floorTolerance = 0.6f;

        [Header("Aviso")]
        [Tooltip("Pinta una franja a ras de suelo en toda la arena mientras dura el gesto.")]
        [SerializeField] private bool warnFloor = true;

        [Min(0.05f)]
        [SerializeField] private float warnHeight = 0.5f;

        [Header("Arte")]
        [Tooltip("Efecto de un solo uso (pooled, VfxOneShot) que sale a los pies del jefe al golpear: " +
                 "la onda de raíces. Vacío = un destello del color de la fase a ras de suelo.")]
        [SerializeField] private GameObject impactFxPrefab;

        [Tooltip("Ancho del efecto en unidades. 0 = la escala del prefab tal cual.")]
        [Min(0f)]
        [SerializeField] private float fxWidth = 9f;

        public override float Telegraph => impactDelay;

        public override string ShortStats() =>
            $"{Damage:0} dmg si pisa suelo · impacto a {impactDelay:0.00}s" +
            (VulnerableSeconds > 0f ? $" · expone {VulnerableSeconds:0.0}s ×{VulnerableMultiplier:0.0}" : "");

        public override void OnTelegraph(BossContext ctx)
        {
            if (!warnFloor) return;

            var center = new Vector2(ctx.Origin.x, ctx.GroundY + warnHeight * 0.5f);
            Warn(ctx, center, new Vector2(ctx.ArenaHalfWidth * 2f, warnHeight), ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            if (!ctx.IsValid) yield break;

            // Run arranca en el frame de suelta del gesto: esto ES el impacto.
            Impact();   // sacudida (Presencia ▸ shakeAmplitude / shakeDuration) + sonido
            SpawnFx(ctx);

            var player = ctx.Player;
            if (player == null || !IsOnArenaFloor(ctx, player)) yield break;

            var health = player.GetComponentInParent<Health>();
            if (health != null) health.TakeDamage(ScaledDamage(ctx), ctx.Origin, KnockbackMultiplier);
        }

        /// <summary>
        /// Pisando el suelo de la arena: apoyado (no en el aire), no sobre una plataforma, y con los
        /// pies a ras del suelo que midió el jefe.
        /// </summary>
        private bool IsOnArenaFloor(in BossContext ctx, Transform player)
        {
            var movement = player.GetComponent<PlayerMovement>();
            if (movement != null && (!movement.IsGrounded || movement.IsOnPlatform)) return false;

            float feet = player.TryGetComponent<Collider2D>(out var body) ? body.bounds.min.y : player.position.y;
            return feet <= ctx.GroundY + floorTolerance;
        }

        private void SpawnFx(in BossContext ctx)
        {
            var at = new Vector3(ctx.Origin.x, ctx.GroundY, 0f);

            if (impactFxPrefab != null)
            {
                if (fxWidth > 0f) VfxOneShot.SpawnFitWidth(impactFxPrefab, at, fxWidth, ctx.Facing);
                else VfxOneShot.Spawn(impactFxPrefab, at, ctx.Facing);
                return;
            }

            var flash = ctx.Accent;
            flash.a = 0.6f;
            AbilityFx.Flash(ctx.FxSprite, new Vector2(ctx.Origin.x, ctx.GroundY + warnHeight * 0.5f),
                            new Vector2(ctx.ArenaHalfWidth * 2f, warnHeight), flash, 0.4f, 0f, 1.2f,
                            ctx.Ability.Caster);
        }
    }
}
