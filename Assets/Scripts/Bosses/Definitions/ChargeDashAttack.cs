using System.Collections;
using RedMagic.Abilities;
using RedMagic.Core;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Embestida — <b>"¿estoy en su línea?"</b>. El jefe fija una dirección hacia el jugador, la
    /// marca con una flecha y cruza la arena a toda velocidad, llevándose por delante lo que haya en
    /// su camino. Se esquiva saltándolo o saliendo de la línea antes de que arranque.
    ///
    /// Es el único ataque que <b>mueve al jefe</b>: acaba en otro sitio, así que cambia desde dónde
    /// sale el siguiente. La dirección se fija al empezar el ataque y no persigue — por eso siempre
    /// es esquivable.
    ///
    /// Sirve tal cual para un enemigo o jefe que embista: todo el recorrido se recorta a la arena.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Charge Dash Attack", fileName = "BossAttack_Dash")]
    [AttackBox("hitboxOffset", "hitboxSize", "Golpe en carrera")]
    public class ChargeDashAttack : BossAttack
    {
        [Header("Recorrido")]
        [Tooltip("Velocidad de la embestida, unidades/s. Se multiplica por el ritmo de la fase.")]
        [Min(1f)]
        [SerializeField] private float speed = 22f;

        [Tooltip("Cuánto sigue después de pasar por donde estaba el jugador. 0 = frena en su sitio.")]
        [Min(0f)]
        [SerializeField] private float overshoot = 4f;

        [Tooltip("Recorrido máximo. 0 = hasta donde haga falta (siempre dentro de la arena).")]
        [Min(0f)]
        [SerializeField] private float maxDistance;

        [Tooltip("Distancia a los bordes de la arena donde frena, para no meterse en la pared.")]
        [Min(0f)]
        [SerializeField] private float arenaMargin = 1.5f;

        [Header("Aviso")]
        [Tooltip("Segundos que la flecha marca la dirección YA FIJADA antes de salir. Es el margen para " +
                 "apartarse: súbelo si es injusto, bájalo para agobiar.")]
        [Min(0f)]
        [SerializeField] private float lockSeconds = 0.4f;

        [Tooltip("Grosor de la flecha / barra de aviso.")]
        [Min(0.1f)]
        [SerializeField] private float warnWidth = 1.6f;

        [Header("Golpe")]
        [Tooltip("Caja de daño relativa al jefe mientras viaja (X hacia delante, Y desde los pies).")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0.6f, 1.8f);

        [SerializeField] private Vector2 hitboxSize = new Vector2(3.2f, 3.2f);

        [Header("Animación")]
        [Tooltip("Estado del Animator que se MANTIENE mientras viaja (una pose de embestida en bucle, " +
                 "p.ej. 'DashLoop'). Vacío = el gesto sigue su curso.")]
        [SerializeField] private string travelState;

        [Header("Arte")]
        [ArtSlot("Efecto al arrancar", ArtSlotKind.Fx, "nada")]
        [Tooltip("Efecto de un solo uso a los pies al salir (polvo, estela). Vacío = nada.")]
        [SerializeField] private GameObject launchFxPrefab;

        [ArtSlot("Impacto al arrollar", ArtSlotKind.Fx, "destello de color")]
        [Tooltip("Efecto de un solo uso donde golpea. Vacío = destello del color de la fase.")]
        [SerializeField] private GameObject hitFxPrefab;

        public override string ShortStats() =>
            $"{Damage:0} dmg · {speed:0} u/s · +{overshoot:0.#} u tras el jugador · fija {lockSeconds:0.00}s";

        public override void OnTelegraph(BossContext ctx)
        {
            // Aviso tenue durante el gesto: hacia dónde MIRA ahora. La dirección buena se fija y se
            // pinta fuerte al empezar Run.
            float targetX = PlanTargetX(ctx, ctx.Facing);
            float length = Mathf.Abs(targetX - ctx.Origin.x);
            if (length > 0.5f)
                WarnArrow(ctx, ctx.Origin + Vector2.up * hitboxOffset.y, new Vector2(ctx.Facing, 0f), length,
                          warnWidth, ctx.Scaled(Telegraph), 0.45f);
        }

        public override IEnumerator Run(BossContext ctx)
        {
            if (!ctx.IsValid) yield break;

            int dir = ctx.Facing;
            Vector2 start = ctx.Origin;
            float targetX = PlanTargetX(ctx, dir);
            float length = Mathf.Abs(targetX - start.x);

            // Sin sitio (pegado a la pared hacia el jugador): no hay embestida que hacer.
            if (length < 0.5f) yield break;

            // --- dirección fijada: flecha fuerte, margen para apartarse.
            float lockTime = ctx.Scaled(lockSeconds);
            if (lockTime > 0f)
            {
                WarnArrow(ctx, start + Vector2.up * hitboxOffset.y, new Vector2(dir, 0f), length, warnWidth,
                          lockTime, 1f);
                yield return new WaitForSeconds(lockTime);
                if (!ctx.IsValid) yield break;
            }

            var body = ctx.Boss.BodyAnimator;
            bool holding = body != null && body.Hold(travelState);

            // Mira hacia donde embiste todo el recorrido, aunque pase por encima del jugador.
            ctx.Boss.FacingLocked = true;

            Impact();
            VfxOneShot.Spawn(launchFxPrefab, new Vector3(start.x, ctx.GroundY, 0f), dir);

            // --- viaje: paso a paso de física, golpeando una sola vez.
            float x = start.x;
            float y = start.y;
            float step = speed * ctx.SpeedScale;
            bool hit = false;

            while (dir * (targetX - x) > 0.01f)
            {
                yield return new WaitForFixedUpdate();
                if (!ctx.IsValid) break;

                float advance = step * Time.fixedDeltaTime;
                x = dir > 0 ? Mathf.Min(x + advance, targetX) : Mathf.Max(x - advance, targetX);
                ctx.Boss.MoveBody(new Vector2(x, y));

                if (hit) continue;

                var center = new Vector2(x + hitboxOffset.x * dir, y + hitboxOffset.y);
                BossHitboxDebug.Box(center, hitboxSize, new Color(1f, 0.6f, 0.2f, 1f), 0.1f);
                if (AbilityHit.DamageBox(ctx.Ability, center, hitboxSize, 0f, Damage, KnockbackMultiplier,
                                         new Vector2(x, y)) > 0)
                {
                    hit = true;
                    SpawnHitFx(ctx, center, dir);
                }
            }

            if (ctx.Boss != null) ctx.Boss.FacingLocked = false;
            if (holding && body != null) body.ReturnToRest();
        }

        /// <summary>Dónde frena: pasado el jugador, recortado al recorrido máximo y a la arena.</summary>
        private float PlanTargetX(in BossContext ctx, int dir)
        {
            float startX = ctx.Origin.x;
            float targetX = ctx.PlayerPosition.x + dir * overshoot;

            // Nunca hacia atrás: si el jugador está pegado, al menos avanza el sobrepaso.
            if (dir * (targetX - startX) < overshoot) targetX = startX + dir * Mathf.Max(overshoot, 1f);

            if (maxDistance > 0f && Mathf.Abs(targetX - startX) > maxDistance)
                targetX = startX + dir * maxDistance;

            float min = ctx.ArenaMinX + arenaMargin;
            float max = ctx.ArenaMaxX - arenaMargin;
            return min < max ? Mathf.Clamp(targetX, min, max) : startX;
        }

        private void SpawnHitFx(in BossContext ctx, Vector2 at, int dir)
        {
            if (hitFxPrefab != null)
            {
                VfxOneShot.Spawn(hitFxPrefab, at, dir);
                return;
            }

            var flash = ctx.Accent;
            flash.a = 0.75f;
            AbilityFx.Flash(ctx.FxSprite, at, hitboxSize, flash, 0.25f, 0f, 1.3f, ctx.Ability.Caster);
        }
    }
}
