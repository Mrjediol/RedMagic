using System.Collections;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Siembra el suelo de trozos que <b>se quedan</b> haciendo daño (<see cref="BossHazard"/>).
    ///
    /// Es el único ataque del juego que no se esquiva y ya está: lo que hace es <b>quitar sitio</b>.
    /// Cada oleada deja la arena un poco más pequeña, así que las esquivas de los siguientes
    /// ataques —la onda que había que saltar, el refugio al que había que correr— se vuelven más
    /// estrechas sin que esos ataques hayan cambiado en nada. Ésa es toda la idea: subir la
    /// dificultad de la baraja entera desde un solo patrón.
    ///
    /// Por eso hace <b>poco daño por tic y dura mucho</b>. Si matara, el jugador lo trataría como
    /// un ataque más y lo esquivaría; haciendo cosquillas, lo que hace es empujarlo hacia donde el
    /// jefe quiere.
    ///
    /// <see cref="avoidPlayerRadius"/> impide que un trozo nazca justo bajo los pies: quitar sitio
    /// es justo, aparecer encima no.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Hazard Field Attack", fileName = "BossAttack_Hazard")]
    public class HazardFieldAttack : BossAttack
    {
        [Header("Trozos")]
        [Min(1)]
        [SerializeField] private int zonesPerWave = 3;

        [Tooltip("Tamaño de cada trozo de suelo envenenado.")]
        [SerializeField] private Vector2 zoneSize = new Vector2(3.4f, 1f);

        [Tooltip("Segundos que se queda. Cuanto más dure, más se nota que la arena encoge.")]
        [Min(0.5f)]
        [SerializeField] private float zoneSeconds = 9f;

        [Header("Daño")]
        [Tooltip("Daño por tic. Bajo a propósito: esto está para estorbar, no para matar.")]
        [Min(0f)]
        [SerializeField] private float damagePerTick = 7f;

        [Min(0.05f)]
        [SerializeField] private float tickInterval = 0.5f;

        [Min(0f)]
        [SerializeField] private float tickKnockbackMultiplier = 0.4f;

        [Header("Reparto")]
        [Tooltip("Fracción de la arena que se puede sembrar. 1 = de lado a lado.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float spread = 1f;

        [Tooltip("Separación mínima entre trozos, para que no se solapen en un muro continuo.")]
        [Min(0f)]
        [SerializeField] private float minSeparation = 4f;

        [Tooltip("No siembra a menos de esta distancia del jugador. Quitar sitio es justo; " +
                 "aparecer bajo sus pies, no.")]
        [Min(0f)]
        [SerializeField] private float avoidPlayerRadius = 2.5f;

        [Header("Ritmo")]
        [Min(1)]
        [SerializeField] private int waves = 1;

        [Min(0.1f)]
        [SerializeField] private float timeBetweenWaves = 0.8f;

        [Tooltip("Segundos que se marca el sitio antes de que aparezca el trozo.")]
        [Min(0.05f)]
        [SerializeField] private float markerSeconds = 0.6f;

        public override string ShortStats() =>
            $"{zonesPerWave}×{waves} trozos · {zoneSeconds:0.0}s · {damagePerTick:0}/tic";

        public override void OnTelegraph(BossContext ctx)
        {
            var center = new Vector2(ctx.Origin.x, ctx.GroundY + 0.5f);
            Warn(ctx, center, new Vector2(ctx.ArenaHalfWidth * 2f * spread, 1f), ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            var spots = new float[Mathf.Max(1, zonesPerWave)];

            for (int wave = 0; wave < waves; wave++)
            {
                if (!ctx.IsValid) yield break;

                PickSpots(ctx, spots);

                for (int i = 0; i < spots.Length; i++)
                    Warn(ctx, new Vector2(spots[i], ctx.GroundY + zoneSize.y * 0.5f), zoneSize,
                         ctx.Scaled(markerSeconds));

                yield return new WaitForSeconds(ctx.Scaled(markerSeconds));

                if (!ctx.IsValid) yield break;

                Impact();

                for (int i = 0; i < spots.Length; i++)
                    BossHazard.Spawn(ctx, new Vector2(spots[i], ctx.GroundY + zoneSize.y * 0.5f),
                                     zoneSize, ctx.Scaled(zoneSeconds), damagePerTick, tickInterval,
                                     tickKnockbackMultiplier);

                if (wave < waves - 1)
                    yield return new WaitForSeconds(ctx.Scaled(timeBetweenWaves));
            }
        }

        /// <summary>
        /// Reparte los trozos por la arena respetando la separación mínima y el hueco alrededor del
        /// jugador. Si el sorteo no encuentra sitio se cae a un reparto regular: es preferible un
        /// patrón previsible a dejar la oleada a medias.
        /// </summary>
        private void PickSpots(in BossContext ctx, float[] spots)
        {
            float half = ctx.ArenaHalfWidth * spread;
            float min = ctx.Origin.x - half;
            float max = ctx.Origin.x + half;
            float playerX = ctx.PlayerPosition.x;

            for (int i = 0; i < spots.Length; i++)
            {
                bool placed = false;

                for (int attempt = 0; attempt < 24 && !placed; attempt++)
                {
                    float candidate = Random.Range(min, max);

                    if (Mathf.Abs(candidate - playerX) < avoidPlayerRadius) continue;
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
