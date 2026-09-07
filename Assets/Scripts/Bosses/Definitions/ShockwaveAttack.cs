using System.Collections;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Ataque que barre <b>toda la arena</b> con una o varias ondas dentro de una franja de altura.
    /// Es el ataque "de pantalla completa" del jefe: no se esquiva corriendo, se esquiva
    /// colocándose a la altura correcta.
    ///
    /// Con los mismos campos salen los dos ataques que forman la pareja interesante:
    ///  - <b>pisotón / raíces</b> — franja <c>0 → 1.9</c>: hay que <b>saltar</b>;
    ///  - <b>barrido de ramas</b> — franja <c>1.9 → 8</c>: hay que <b>quedarse en el suelo</b>.
    /// Que ambos existan es lo que obliga a leer el aviso en vez de saltar por reflejo.
    ///
    /// El aviso pinta la franja exacta que va a doler, así que siempre es justo: si te pilla, es
    /// porque estabas a la altura equivocada.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Shockwave Attack", fileName = "BossAttack_Shockwave")]
    public class ShockwaveAttack : BossAttack
    {
        [Header("Franja de altura (unidades sobre el suelo de la arena)")]
        [Tooltip("Borde inferior. 0 = pegado al suelo.")]
        [SerializeField] private float bandMin;

        [Tooltip("Borde superior. Con el borde inferior a 0, esto es la altura que hay que saltar.")]
        [SerializeField] private float bandMax = 1.9f;

        [Header("Franja alterna")]
        [Tooltip("Las oleadas impares usan la segunda franja. Con una franja baja y otra alta, el " +
                 "ataque pide saltar, aterrizar y volver a saltar: es el patrón más exigente que " +
                 "se puede montar sin escribir código nuevo.")]
        [SerializeField] private bool alternateBands;

        [SerializeField] private float bandMinB = 1.9f;

        [SerializeField] private float bandMaxB = 9f;

        [Header("Oleadas")]
        [Tooltip("Ondas seguidas que lanza el ataque.")]
        [Min(1)]
        [SerializeField] private int waves = 1;

        [Min(0.05f)]
        [SerializeField] private float timeBetweenWaves = 0.5f;

        [Tooltip("Lanza una onda a cada lado. Apagado = sólo hacia el jugador.")]
        [SerializeField] private bool bothDirections = true;

        [Header("Onda")]
        [Tooltip("Grosor horizontal de la onda: cuánto tiempo te toca si no la esquivas.")]
        [Min(0.2f)]
        [SerializeField] private float width = 1.6f;

        [Min(1f)]
        [SerializeField] private float speed = 16f;

        [Tooltip("Distancia que recorre. 0 = cruza la arena entera.")]
        [Min(0f)]
        [SerializeField] private float travelDistance;

        [Tooltip("A qué distancia del jefe nace la onda. Evita que aparezca dentro de su tronco.")]
        [Min(0f)]
        [SerializeField] private float spawnInset = 1.4f;

        [Header("Escalonado")]
        [Tooltip("Cada oleada sale un poco más rápida que la anterior. 1 = todas iguales.")]
        [Min(0.5f)]
        [SerializeField] private float speedRampPerWave = 1f;

        public override string ShortStats() =>
            $"{Damage:0} dmg · franja {bandMin:0.0}–{bandMax:0.0} · {waves} onda(s) · {speed:0} u/s";

        public override void OnTelegraph(BossContext ctx)
        {
            // Se pinta la franja exacta que va a doler, de lado a lado de la arena: el aviso ES la
            // regla del ataque. Sin esto, "salta o quédate en el suelo" sería adivinar.
            WarnBand(ctx, bandMin, bandMax);

            // Alternando, la primera oleada manda pero se enseñan las dos: la segunda franja llega
            // demasiado rápido detrás como para poder leerla desde cero.
            if (alternateBands && waves > 1) WarnBand(ctx, bandMinB, bandMaxB);
        }

        private void WarnBand(in BossContext ctx, float min, float max)
        {
            float minY = ctx.GroundY + min;
            float maxY = ctx.GroundY + max;

            var center = new Vector2(ctx.Origin.x, (minY + maxY) * 0.5f);
            var size = new Vector2(ctx.ArenaHalfWidth * 2f, Mathf.Max(0.2f, maxY - minY));

            Warn(ctx, center, size, ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            float distance = travelDistance > 0f ? travelDistance : ctx.ArenaHalfWidth + width;

            for (int wave = 0; wave < waves; wave++)
            {
                if (!ctx.IsValid) yield break;

                bool useB = alternateBands && (wave & 1) == 1;
                float minY = ctx.GroundY + (useB ? bandMinB : bandMin);
                float maxY = ctx.GroundY + (useB ? bandMaxB : bandMax);

                Impact();

                float waveSpeed = speed * Mathf.Pow(speedRampPerWave, wave);

                if (bothDirections)
                {
                    Launch(ctx, -1, minY, maxY, distance, waveSpeed);
                    Launch(ctx, 1, minY, maxY, distance, waveSpeed);
                }
                else
                {
                    Launch(ctx, ctx.Facing, minY, maxY, distance, waveSpeed);
                }

                if (wave < waves - 1)
                    yield return new WaitForSeconds(ctx.Scaled(timeBetweenWaves));
            }
        }

        private void Launch(in BossContext ctx, int direction, float minY, float maxY,
                            float distance, float waveSpeed)
        {
            float originX = ctx.Origin.x + direction * spawnInset;
            BossShockwave.Spawn(ctx, originX, direction, minY, maxY, width, waveSpeed, distance,
                                Damage, KnockbackMultiplier);
        }
    }
}
