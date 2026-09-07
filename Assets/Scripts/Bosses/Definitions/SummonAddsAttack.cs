using System.Collections;
using RedMagic.Abilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Invoca esbirros. No está para hacer daño, sino para <b>cambiar el problema</b>: durante unos
    /// segundos el jugador ya no puede dedicarse sólo a leer los patrones del jefe.
    ///
    /// Los esbirros quedan apuntados en el <see cref="BossController"/>, así que mueren con él y
    /// nadie se queda peleando contra los restos mientras recoge la recompensa. El tope de vivos
    /// (<see cref="maxAlive"/>) evita la bola de nieve si el jugador ignora las invocaciones.
    ///
    /// Se usa <c>Instantiate</c> a propósito y no un pool: son unos pocos por combate (churn bajo,
    /// como el cadáver o la tienda) y un enemigo reutilizado tendría que saber resucitarse —
    /// <c>EnemyController</c> apaga sus colliders al morir y no tiene camino de vuelta.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Boss/Summon Adds Attack", fileName = "BossAttack_Summon")]
    public class SummonAddsAttack : BossAttack
    {
        [Header("Esbirros")]
        [Tooltip("Prefabs a invocar. Se elige uno al azar por esbirro.")]
        [SerializeField] private GameObject[] prefabs;

        [Min(1)]
        [SerializeField] private int count = 3;

        [Tooltip("Tope de esbirros vivos. Si ya hay tantos, la invocación no añade más.")]
        [Min(1)]
        [SerializeField] private int maxAlive = 6;

        [Tooltip("Etiqueta que se pone al esbirro. Compartirla con el jefe es lo que hace que los " +
                 "proyectiles del jefe no maten a sus propios esbirros.")]
        [SerializeField] private string addTag = "Enemy";

        [Header("Colocación")]
        [Tooltip("Distancia mínima y máxima al jefe. Van alternando izquierda y derecha.")]
        [SerializeField] private Vector2 distanceRange = new Vector2(4f, 11f);

        [Tooltip("Altura sobre el suelo a la que aparecen (se ajusta por raycast si hay suelo).")]
        [SerializeField] private float spawnHeight = 0.8f;

        [Tooltip("Capas que cuentan como suelo al colocarlos.")]
        [SerializeField] private LayerMask groundLayers = 1 << 6;

        [Header("Ritmo")]
        [Tooltip("Segundos que la marca del suelo está visible antes de que aparezca el esbirro.")]
        [Min(0.05f)]
        [SerializeField] private float markerSeconds = 0.55f;

        [Min(0f)]
        [SerializeField] private float timeBetweenSpawns = 0.12f;

        public override string ShortStats() => $"invoca {count} (máx. {maxAlive} vivos)";

        public override void OnTelegraph(BossContext ctx)
        {
            Warn(ctx, new Vector2(ctx.Origin.x, ctx.GroundY + 0.4f),
                 new Vector2(ctx.ArenaHalfWidth * 2f, 0.8f), ctx.Scaled(Telegraph));
        }

        public override IEnumerator Run(BossContext ctx)
        {
            if (!ctx.IsValid || prefabs == null || prefabs.Length == 0) yield break;

            int room = Mathf.Max(0, maxAlive - ctx.Boss.LiveAdds);
            int toSpawn = Mathf.Min(count, room);
            if (toSpawn <= 0) yield break;

            var spots = new Vector2[toSpawn];

            // Primero se marcan todos los sitios y luego aparecen: al jugador le da tiempo a
            // apartarse en vez de encontrarse un bicho encima de la cara.
            for (int i = 0; i < toSpawn; i++)
            {
                spots[i] = PickSpot(ctx, i);
                Warn(ctx, spots[i], new Vector2(1.2f, 1.6f), ctx.Scaled(markerSeconds));
            }

            yield return new WaitForSeconds(ctx.Scaled(markerSeconds));

            for (int i = 0; i < toSpawn; i++)
            {
                if (!ctx.IsValid) yield break;

                Impact();
                SpawnAdd(ctx, spots[i]);

                if (timeBetweenSpawns > 0f && i < toSpawn - 1)
                    yield return new WaitForSeconds(ctx.Scaled(timeBetweenSpawns));
            }
        }

        /// <summary>
        /// Un sitio a cada lado, alternando, dentro del rango de distancias y sin salirse de la
        /// arena. Alternar evita que los tres salgan amontonados en el mismo flanco.
        /// </summary>
        private Vector2 PickSpot(in BossContext ctx, int index)
        {
            int side = (index & 1) == 0 ? -1 : 1;
            float distance = Random.Range(distanceRange.x, distanceRange.y);

            float x = Mathf.Clamp(ctx.Origin.x + side * distance,
                                  ctx.ArenaMinX + 1f, ctx.ArenaMaxX - 1f);

            // Si hay suelo bajo ese punto se usa su altura real; así los esbirros no aparecen
            // flotando en una arena con desniveles.
            var probe = new Vector2(x, ctx.CeilingY);
            var hit = Physics2D.Raycast(probe, Vector2.down, ctx.ArenaHeight + 5f, groundLayers);
            float groundY = hit.collider != null ? hit.point.y : ctx.GroundY;

            return new Vector2(x, groundY + spawnHeight);
        }

        private void SpawnAdd(in BossContext ctx, Vector2 position)
        {
            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null) return;

            var add = Instantiate(prefab, position, Quaternion.identity);

            // A la escena del jefe, no a la escena activa: RunManager mantiene viva una escena
            // "raíz" durante toda la run, y un esbirro creado ahí sobreviviría a la descarga de la
            // sección y aparecería en la siguiente.
            var scene = ctx.Boss.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded) SceneManager.MoveGameObjectToScene(add, scene);

            if (!string.IsNullOrWhiteSpace(addTag)) TrySetTag(add, addTag);

            ctx.Boss.RegisterAdd(add);

            AbilityFx.Flash(ctx.FxSprite, position, Vector2.one * 2.2f, ctx.Accent, 0.35f, 0f, 1.8f,
                            ctx.Ability.Caster);
        }

        /// <summary>
        /// Etiquetar puede fallar si la etiqueta no está dada de alta en el proyecto. Se avisa una
        /// vez en vez de reventar la corrutina del ataque a mitad de combate.
        /// </summary>
        private static void TrySetTag(GameObject go, string tag)
        {
            try
            {
                go.tag = tag;
            }
            catch (UnityException)
            {
                Debug.LogWarning($"[Boss] La etiqueta '{tag}' no existe en el proyecto: el esbirro " +
                                 $"'{go.name}' recibirá los proyectiles del jefe.", go);
            }
        }
    }
}
