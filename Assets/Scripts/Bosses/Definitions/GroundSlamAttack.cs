using System.Collections;
using RedMagic.Abilities;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// El puñetazo al suelo: el jefe elige <b>el sitio donde estás</b>, lo marca, y un instante
    /// después lo revienta.
    ///
    /// Las otras esquivas del juego preguntan por la altura, por la distancia o por a qué refugio
    /// llegas. Ésta pregunta lo más simple y lo más incómodo: <b>vete de donde estás</b>. Como la
    /// marca se fija al empezar y no persigue, siempre se puede esquivar, pero obliga a soltar el
    /// sitio cómodo desde el que se estaba pegando.
    ///
    /// Es además el ataque que abre el combate por dentro: con
    /// <see cref="BossAttack.VulnerableSeconds"/> puesto, el jefe se queda con el puño clavado y
    /// recibe daño multiplicado durante la recuperación. Contra un jefe acorazado eso convierte su
    /// golpe más peligroso en la única forma real de hacerle daño — acercarse a castigar es
    /// justo lo contrario de lo que pide el instinto, y ahí está la gracia.
    ///
    /// Opcionalmente deja <b>escombro</b> (<see cref="BossHazard"/>) en el cráter, de modo que el
    /// sitio castigado deja de ser sitio durante unos segundos.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Ground Slam Attack", fileName = "BossAttack_Slam")]
    public class GroundSlamAttack : BossAttack
    {
        [Header("Objetivo")]
        [Tooltip("Marca el sitio donde está el jugador. Apagado, golpea siempre a sus pies.")]
        [SerializeField] private bool aimAtPlayer = true;

        [Tooltip("Distancia máxima a la que el jefe puede alcanzar desde su sitio. 0 = toda la arena.")]
        [Min(0f)]
        [SerializeField] private float maxReach;

        [Header("Golpe")]
        [Tooltip("Radio del cráter.")]
        [Min(0.5f)]
        [SerializeField] private float radius = 3.4f;

        [Tooltip("Segundos entre que se fija la marca y cae el puño. Es TODO el margen que hay " +
                 "para apartarse, así que es el mando de dificultad de este ataque.")]
        [Min(0.1f)]
        [SerializeField] private float windup = 0.75f;

        [Tooltip("Altura del cráter sobre el suelo. Cubre a un jugador de pie; subirlo hace que " +
                 "saltar tampoco salve.")]
        [Min(0.5f)]
        [SerializeField] private float height = 3f;

        [Header("Repetición")]
        [Min(1)]
        [SerializeField] private int slams = 1;

        [Tooltip("Segundos entre puñetazos. Con varios, cada uno vuelve a marcar dónde estás.")]
        [Min(0.1f)]
        [SerializeField] private float timeBetweenSlams = 0.9f;

        [Header("Escombro")]
        [Tooltip("Segundos que el cráter sigue haciendo daño. 0 = no deja nada.")]
        [Min(0f)]
        [SerializeField] private float rubbleSeconds;

        [Min(0f)]
        [SerializeField] private float rubbleDamagePerTick = 6f;

        [Min(0.05f)]
        [SerializeField] private float rubbleTickInterval = 0.5f;

        [Header("Arte")]
        [Tooltip("Efecto de un solo uso (pooled, con VfxOneShot) que sale en el suelo del cráter: " +
                 "la onda de raíces, la púa que brota. Su pivote debe ir abajo (se planta en el " +
                 "suelo). Vacío = el rectángulo de siempre con el color de la fase.")]
        [SerializeField] private GameObject impactFxPrefab;

        [Tooltip("Escala el efecto para que su ancho sea el del cráter (2 × radio). Así cambiar el " +
                 "radio no deja el dibujo más grande o más pequeño que lo que duele. Apagado = la " +
                 "escala del prefab tal cual.")]
        [SerializeField] private bool fitFxToRadius = true;

        [Tooltip("Segundos que el efecto sale ANTES del golpe. Para un efecto que tarda en llegar a " +
                 "su pico — una púa que brota —: el daño cae cuando el dibujo está arriba del todo, " +
                 "no mientras aún asoma. Nunca más que 'windup'.")]
        [Min(0f)]
        [SerializeField] private float fxLeadSeconds;

        public override string ShortStats() =>
            $"{Damage:0} dmg · radio {radius:0.0} · aviso {windup:0.00}s · {slams} golpe(s)" +
            (VulnerableSeconds > 0f ? $" · expone {VulnerableSeconds:0.0}s ×{VulnerableMultiplier:0.0}" : "");

        public override void OnTelegraph(BossContext ctx)
        {
            // Aquí sólo se anuncia el gesto (el jefe levanta el puño). Dónde va a caer se marca
            // dentro de Run, ya con el reloj corriendo: marcarlo en el telegrafiado daría el doble
            // de tiempo y el ataque dejaría de pedir nada. Con gesto, el propio cuerpo del jefe ya
            // anuncia el golpe y el rectángulo sobre su cabeza sobra.
            if (string.IsNullOrEmpty(Gesture))
                Warn(ctx, ctx.Origin + new Vector2(0f, 3.6f), Vector2.one * 2.4f, ctx.Scaled(Telegraph));

            // Salvo si no apunta: entonces cae siempre en el mismo sitio (a los pies del jefe), no
            // hay nada que adivinar, y el aviso entero es margen para salir del radio.
            if (!aimAtPlayer)
                Warn(ctx, PickTarget(ctx), new Vector2(radius * 2f, height), ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            for (int slam = 0; slam < slams; slam++)
            {
                if (!ctx.IsValid) yield break;

                Vector2 target = PickTarget(ctx);
                float wait = ctx.Scaled(windup);

                // El efecto sale 'fxLeadSeconds' antes del golpe, para que su pico caiga con el daño.
                float fxAt = wait - Mathf.Min(ctx.Scaled(fxLeadSeconds), wait);
                bool fxOut = impactFxPrefab == null;

                // La marca late cada poco en vez de pintarse una vez: una marca que se apaga sola
                // se lee como "esto ya pasó", que es lo contrario de lo que está a punto de ocurrir.
                float elapsed = 0f;
                while (elapsed < wait)
                {
                    if (!ctx.IsValid) yield break;

                    if (!fxOut && elapsed >= fxAt)
                    {
                        SpawnFx(ctx, target);
                        fxOut = true;
                    }

                    float t = Mathf.Clamp01(elapsed / wait);
                    Warn(ctx, target, Vector2.one * (radius * 2f), 0.18f, 0f, Mathf.Lerp(0.5f, 1.2f, t));

                    // Pasos de 0.12s, recortados para no pasarse del momento del efecto ni del golpe.
                    float step = 0.12f;
                    if (!fxOut && fxAt > elapsed) step = Mathf.Min(step, fxAt - elapsed);
                    step = Mathf.Max(0.01f, Mathf.Min(step, wait - elapsed));

                    yield return new WaitForSeconds(step);
                    elapsed += step;
                }

                if (!ctx.IsValid) yield break;

                if (!fxOut) SpawnFx(ctx, target);

                Impact();
                Crush(ctx, target);

                if (slam < slams - 1)
                    yield return new WaitForSeconds(ctx.Scaled(timeBetweenSlams));
            }
        }

        /// <summary>Dónde cae el puño. Se fija ahora y no se mueve: es lo que lo hace esquivable.</summary>
        private Vector2 PickTarget(in BossContext ctx)
        {
            float x = aimAtPlayer ? ctx.PlayerPosition.x : ctx.Origin.x;

            if (maxReach > 0f)
                x = Mathf.Clamp(x, ctx.Origin.x - maxReach, ctx.Origin.x + maxReach);

            x = Mathf.Clamp(x, ctx.ArenaMinX, ctx.ArenaMaxX);
            return new Vector2(x, ctx.GroundY + height * 0.5f);
        }

        /// <summary>El efecto del cráter, plantado en el suelo bajo el golpe (su pivote va abajo).</summary>
        private void SpawnFx(in BossContext ctx, Vector2 target)
        {
            var at = new Vector3(target.x, ctx.GroundY, 0f);

            if (fitFxToRadius) VfxOneShot.SpawnFitWidth(impactFxPrefab, at, radius * 2f, ctx.Facing);
            else VfxOneShot.Spawn(impactFxPrefab, at, ctx.Facing);
        }

        private void Crush(in BossContext ctx, Vector2 target)
        {
            // Caja y no círculo: el cráter tiene que llegar hasta el suelo aunque el centro esté a
            // media altura, y una caja es lo que se corresponde con la marca que se ha pintado.
            AbilityHit.DamageBox(ctx.Ability, target, new Vector2(radius * 2f, height), 0f,
                                 Damage, KnockbackMultiplier, target);

            // Con arte propio el cráter lo dibuja el efecto (salió en SpawnFx); el destello de color
            // es sólo el sustituto de cuando no hay.
            if (impactFxPrefab == null)
            {
                var flash = ctx.Accent;
                flash.a = 0.7f;
                AbilityFx.Flash(ctx.FxSprite, target, new Vector2(radius * 2f, height), flash, 0.4f, 0f, 1.35f,
                                ctx.Ability.Caster);
            }

            if (rubbleSeconds <= 0f) return;

            BossHazard.Spawn(ctx, new Vector2(target.x, ctx.GroundY + 0.45f),
                             new Vector2(radius * 1.8f, 0.9f), ctx.Scaled(rubbleSeconds),
                             rubbleDamagePerTick, rubbleTickInterval, 0.4f);
        }
    }
}
