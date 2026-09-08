using System.Collections;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// <b>El suelo deja de valer.</b> Primero brotan unos salientes
    /// (<see cref="BossPlatform"/>), y acto seguido la arena entera se inunda
    /// (<see cref="BossHazard"/>) durante unos segundos. Hay que subirse, quedarse arriba mientras
    /// dura, y bajar antes de que los salientes se apaguen.
    ///
    /// Es la única pregunta del juego que no va de esquivar sino de <b>dónde se juega</b>: durante
    /// esos segundos el combate deja de pasar en el suelo. Y como los salientes también caducan, no
    /// se puede vivir arriba: es un préstamo, no un refugio.
    ///
    /// Los dos trozos van en el mismo ataque a propósito. La baraja del jefe se sortea, así que
    /// "primero salen plataformas y luego se inunda" sólo se puede garantizar si son la misma
    /// carta; separarlos dejaría el suelo mortal sin nada a lo que subirse.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Platform Flood Attack", fileName = "BossAttack_Flood")]
    public class PlatformFloodAttack : BossAttack
    {
        [Header("Salientes")]
        [Min(1)]
        [SerializeField] private int platformCount = 3;

        [SerializeField] private Vector2 platformSize = new Vector2(4.2f, 0.7f);

        [Tooltip("Alturas sobre el suelo a las que aparecen, repartidas por turnos. La primera " +
                 "tiene que llegarse de un salto; la segunda, saltando desde la primera.")]
        [SerializeField] private float[] platformHeights = { 3.2f, 5.6f };

        [Tooltip("Segundos que aguantan. Debe ser MÁS que la inundación: bajarse a un suelo que " +
                 "todavía quema sería una muerte que el jugador no ha elegido.")]
        [Min(1f)]
        [SerializeField] private float platformSeconds = 7f;

        [Tooltip("Separación mínima entre salientes para que no salgan pegados.")]
        [Min(0f)]
        [SerializeField] private float minSeparation = 6f;

        [Header("Inundación")]
        [Tooltip("Segundos que el suelo hace daño.")]
        [Min(0.5f)]
        [SerializeField] private float floodSeconds = 4.5f;

        [Tooltip("Trozos en los que se parte el suelo. Más trozos = borde más fino, pero da igual: " +
                 "juntos cubren la arena entera.")]
        [Min(1)]
        [SerializeField] private int floodSegments = 6;

        [Tooltip("Alto de la lámina que cubre el suelo. Bastante bajo: subirse a un saliente tiene " +
                 "que salvarte, y saltar sin más no.")]
        [Min(0.3f)]
        [SerializeField] private float floodHeight = 1.1f;

        [Min(0f)]
        [SerializeField] private float floodDamagePerTick = 11f;

        [Min(0.05f)]
        [SerializeField] private float floodTickInterval = 0.45f;

        [Header("Ritmo")]
        [Tooltip("Segundos entre que aparecen los salientes y sube la marea. Es el tiempo para " +
                 "llegar a uno y subirse.")]
        [Min(0.2f)]
        [SerializeField] private float climbSeconds = 1.4f;

        [Min(0.05f)]
        [SerializeField] private float markerSeconds = 0.5f;

        public override string ShortStats() =>
            $"{platformCount} salientes {platformSeconds:0.0}s · inunda {floodSeconds:0.0}s " +
            $"({floodDamagePerTick:0}/tic)";

        public override void OnTelegraph(BossContext ctx)
        {
            // El aviso marca el suelo entero: lo que va a cambiar es él.
            var center = new Vector2(ctx.Origin.x, ctx.GroundY + floodHeight * 0.5f);
            Warn(ctx, center, new Vector2(ctx.ArenaHalfWidth * 2f, floodHeight), ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            if (!ctx.IsValid) yield break;

            var spots = new float[Mathf.Max(1, platformCount)];
            PickSpots(ctx, spots);

            // 1. Se marca dónde van a salir los salientes…
            for (int i = 0; i < spots.Length; i++)
                Warn(ctx, PlatformCenter(ctx, spots[i], i), platformSize, ctx.Scaled(markerSeconds));

            yield return new WaitForSeconds(ctx.Scaled(markerSeconds));
            if (!ctx.IsValid) yield break;

            // 2. …salen, y empieza la cuenta atrás para subirse.
            Impact();
            for (int i = 0; i < spots.Length; i++)
                BossPlatform.Spawn(ctx, PlatformCenter(ctx, spots[i], i), platformSize,
                                   ctx.Scaled(platformSeconds));

            yield return new WaitForSeconds(ctx.Scaled(climbSeconds));
            if (!ctx.IsValid) yield break;

            // 3. Sube la marea, en trozos que juntos cubren la arena.
            Impact();
            Flood(ctx);
        }

        private void Flood(in BossContext ctx)
        {
            float width = ctx.ArenaHalfWidth * 2f;
            int segments = Mathf.Max(1, floodSegments);
            float segmentWidth = width / segments;
            float left = ctx.Origin.x - ctx.ArenaHalfWidth;

            for (int i = 0; i < segments; i++)
            {
                float x = left + segmentWidth * (i + 0.5f);
                BossHazard.Spawn(ctx, new Vector2(x, ctx.GroundY + floodHeight * 0.5f),
                                 new Vector2(segmentWidth, floodHeight), ctx.Scaled(floodSeconds),
                                 floodDamagePerTick, floodTickInterval, 0.5f);
            }
        }

        private Vector2 PlatformCenter(in BossContext ctx, float x, int index)
        {
            float height = platformHeights != null && platformHeights.Length > 0
                ? platformHeights[index % platformHeights.Length]
                : 3.2f;

            return new Vector2(x, ctx.GroundY + height);
        }

        /// <summary>
        /// Reparte los salientes por la arena. Si el sorteo no encuentra hueco se cae a un reparto
        /// regular: quedarse sin sitio al que subir convertiría el ataque en daño inevitable.
        /// </summary>
        private void PickSpots(in BossContext ctx, float[] spots)
        {
            float margin = platformSize.x * 0.5f + 1f;
            float min = ctx.ArenaMinX + margin;
            float max = ctx.ArenaMaxX - margin;
            if (max <= min) { min = ctx.ArenaMinX; max = ctx.ArenaMaxX; }

            for (int i = 0; i < spots.Length; i++)
            {
                bool placed = false;

                for (int attempt = 0; attempt < 24 && !placed; attempt++)
                {
                    float candidate = Random.Range(min, max);
                    if (!IsFarEnough(candidate, spots, i)) continue;

                    spots[i] = candidate;
                    placed = true;
                }

                if (!placed)
                {
                    float step = spots.Length > 1 ? (max - min) / (spots.Length - 1) : 0f;
                    spots[i] = spots.Length > 1 ? min + step * i : (min + max) * 0.5f;
                }
            }
        }

        private bool IsFarEnough(float candidate, float[] spots, int count)
        {
            for (int i = 0; i < count; i++)
                if (Mathf.Abs(spots[i] - candidate) < minSeparation) return false;

            return true;
        }
    }
}
