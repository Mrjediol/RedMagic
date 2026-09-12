using System.Collections;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Negar terreno a propósito: el jefe lanza un proyectil a cada uno de los puntos <b>fijos</b>
    /// que se han dejado en la arena (<see cref="BossArenaTargets"/>, en el jefe de la escena) y,
    /// al llegar, cada uno deja un fuego que ya no se apaga hasta que el jefe muere.
    ///
    /// Pregunta <b>"¿dónde voy a poder estar el resto del combate?"</b>, pero al revés que
    /// <see cref="HazardFieldAttack"/> (aleatorio y temporal): aquí el sitio lo elige el diseño y
    /// es para siempre — una reescritura de la arena de una sola vez, no una carta de la baraja.
    /// Por eso va como <see cref="BossPhase.openingAttack"/> de una fase.
    ///
    /// <b>PLACEHOLDER → ARTE:</b> proyectil y fuego son prefabs (<see cref="boltPrefab"/>,
    /// <see cref="firePrefab"/>). Hoy son el cuadrado placeholder con <c>FxPlaceholderStyle</c>,
    /// teñido con el <c>accent</c> del ataque; el arte final se mete editando esos prefabs, sin
    /// tocar este archivo.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Platform Denial Attack", fileName = "BossAttack_Denial")]
    public class PlatformDenialAttack : BossAttack
    {
        [Header("Proyectil")]
        [Tooltip("Prefab del proyectil (BossBolt). PLACEHOLDER: Fx_<Jefe>_FireBolt, un cuadrado " +
                 "teñido. Arte final: edita ese prefab (sprite + SpriteFlipbook, y en " +
                 "FxPlaceholderStyle tint = false). Vacío = cuadrado construido en código.")]
        [SerializeField] private GameObject boltPrefab;

        [Tooltip("Velocidad del proyectil, en unidades por segundo. El ritmo de la fase no la cambia.")]
        [Min(0.5f)]
        [SerializeField] private float boltSpeed = 12f;

        [Tooltip("Tamaño del proyectil placeholder (lo aplica FxPlaceholderStyle; el arte sin estilo " +
                 "usa la escala de su prefab).")]
        [SerializeField] private Vector2 boltSize = new Vector2(0.8f, 0.8f);

        [Tooltip("De dónde sale, relativo a la base del jefe.")]
        [SerializeField] private Vector2 launchOffset = new Vector2(0f, 4.5f);

        [Tooltip("Segundos entre un proyectil y el siguiente. 0 = todos a la vez.")]
        [Min(0f)]
        [SerializeField] private float launchStagger = 0.12f;

        [Header("Fuego (hasta que muere el jefe)")]
        [Tooltip("Ráfaga de un solo uso al llegar el proyectil, antes de que nazca el fuego " +
                 "permanente (un VfxOneShot). Opcional: vacío = sin ráfaga, el fuego nace sin más.")]
        [SerializeField] private GameObject fireImpactFxPrefab;

        [Tooltip("Prefab del fuego (BossHazard). PLACEHOLDER: Fx_<Jefe>_FireHazard, un cuadrado " +
                 "teñido. Arte final: edita ese prefab. Vacío = cuadrado construido en código.")]
        [SerializeField] private GameObject firePrefab;

        [Tooltip("Ancho × alto del fuego en unidades. Es también su caja de daño.")]
        [SerializeField] private Vector2 fireSize = new Vector2(2.1f, 1f);

        [Tooltip("Segundos entre tics de daño. El daño por tic es 'damage' (sección Daño) y el empuje " +
                 "'knockbackMultiplier'. Los i-frames del jugador ya limitan cuántos entran de verdad.")]
        [Min(0.05f)]
        [SerializeField] private float fireTickInterval = 0.5f;

        [Tooltip("Asienta el fuego sobre la superficie que haya bajo cada punto (plataforma o suelo), " +
                 "así el punto puede dejarse a ojo un poco por encima.")]
        [SerializeField] private bool snapToSurface = true;

        [Min(0.1f)]
        [SerializeField] private float snapDistance = 4f;

        public override string ShortStats() =>
            $"{Damage:0} dmg/tic cada {fireTickInterval:0.00}s · proyectil {boltSpeed:0.#} u/s · " +
            $"fuego {fireSize.x:0.0}×{fireSize.y:0.0} hasta que muere el jefe";

        public override void OnTelegraph(BossContext ctx)
        {
            var points = Targets(ctx, warn: false);
            if (points == null) return;

            // Se marca dónde va a caer cada fuego: lo que se pierde se ve antes de perderlo.
            foreach (var point in points)
                if (point != null)
                    Warn(ctx, Landing(point.position), fireSize, ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            var points = Targets(ctx, warn: true);
            if (points == null) yield break;

            Impact();

            Vector2 from = ctx.Origin + launchOffset;
            var launchCtx = ctx;   // copia para el aviso de llegada (un 'in' no se puede capturar)
            float lastLanding = 0f;

            for (int i = 0; i < points.Length; i++)
            {
                if (!ctx.IsValid) yield break;
                if (points[i] == null) continue;

                Vector2 landing = Landing(points[i].position);
                BossBolt.Launch(ctx, boltPrefab, from, landing, boltSpeed, boltSize, Accent,
                                at => Ignite(launchCtx, at));

                lastLanding = Mathf.Max(lastLanding, Time.time + Vector2.Distance(from, landing) / boltSpeed);

                if (launchStagger > 0f && i < points.Length - 1)
                    yield return new WaitForSeconds(ctx.Scaled(launchStagger));
            }

            // El ataque (y con él la recuperación) acaba cuando el último fuego está en el suelo.
            while (Time.time < lastLanding && ctx.IsValid) yield return null;
        }

        /// <summary>El proyectil ha llegado: nace el fuego, salvo que el jefe ya haya caído.</summary>
        private void Ignite(BossContext ctx, Vector2 at)
        {
            if (!ctx.IsValid || ctx.Boss.BossHealth == null || ctx.Boss.BossHealth.IsDead) return;

            VfxOneShot.Spawn(fireImpactFxPrefab, at);

            BossHazard.Spawn(ctx, firePrefab, Accent, at, fireSize, BossHazard.UntilBossDies,
                             Damage, fireTickInterval, KnockbackMultiplier);
        }

        /// <summary>Centro del fuego: sobre la superficie bajo el punto, o el punto tal cual.</summary>
        private Vector2 Landing(Vector2 point)
        {
            if (!snapToSurface) return point;

            var hit = Physics2D.Raycast(point, Vector2.down, snapDistance, LayerMask.GetMask("Ground", "Platform"));
            return hit.collider != null ? new Vector2(point.x, hit.point.y + fireSize.y * 0.5f) : point;
        }

        private Transform[] Targets(in BossContext ctx, bool warn)
        {
            var holder = ctx.Boss != null ? ctx.Boss.GetComponent<BossArenaTargets>() : null;
            if (holder != null && holder.Targets != null && holder.Targets.Length > 0) return holder.Targets;

            if (warn)
                Debug.LogWarning($"[Boss] '{name}': el jefe no tiene BossArenaTargets con puntos; " +
                                 "el ataque no lanza nada.", ctx.Boss);
            return null;
        }
    }
}
