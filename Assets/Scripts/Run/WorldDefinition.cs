using System.Collections.Generic;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Run
{
    /// <summary>
    /// Datos de un mundo: su pool de secciones, su jefe y cuántas secciones entran en una run.
    ///
    /// Es un ScriptableObject para que añadir el Mundo 3 sea "crear un asset y rellenarlo", sin
    /// tocar una línea de <see cref="RunManager"/>. El pool no está limitado a 15 ni la run a 5:
    /// son sólo los valores por defecto, así que se puede desbalancear un mundo concreto sin
    /// romper los demás.
    ///
    /// Las secciones se guardan como <see cref="SceneReference"/> y no como nombres de escena.
    /// Con 5 mundos × 16 escenas, una convención de nombres tecleada a mano
    /// ("World1_Section01"…) es un campo de minas: una errata o un renombrado no fallan al
    /// compilar y sólo se notan cuando la run se queda colgada a mitad. La referencia por asset
    /// se rompe de forma visible en el Inspector, que es donde se quiere ver el fallo.
    /// </summary>
    [CreateAssetMenu(fileName = "World", menuName = "RedMagic/World Definition")]
    public class WorldDefinition : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Número de mundo (1..5). Sólo se usa para ordenar y para los logs.")]
        [SerializeField] private int worldNumber = 1;

        [Tooltip("Nombre que ve el jugador en el hub.")]
        [SerializeField] private string displayName = "Mundo 1";

        [Tooltip("Si está desactivado, la tumba de este mundo aparece bloqueada en el hub.")]
        [SerializeField] private bool unlocked = true;

        [Header("Secciones")]
        [Tooltip("Pool completo de secciones del mundo (previsto: 15). De aquí se sortean las que " +
                 "se juegan en cada run.")]
        [SerializeField] private List<SceneReference> sectionPool = new List<SceneReference>();

        [Tooltip("Cuántas secciones del pool se juegan en una run antes del jefe.")]
        [Min(1)]
        [SerializeField] private int sectionsPerRun = 5;

        [Header("Jefe")]
        [Tooltip("Escena del jefe. Se juega siempre al terminar las secciones sorteadas.")]
        [SerializeField] private SceneReference bossScene = new SceneReference();

        public int WorldNumber => worldNumber;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public bool Unlocked => unlocked;
        public int SectionsPerRun => Mathf.Max(1, sectionsPerRun);
        public SceneReference BossScene => bossScene;
        public IReadOnlyList<SceneReference> SectionPool => sectionPool;

        /// <summary>Secciones del pool que están asignadas y presentes en Build Settings.</summary>
        public int PlayableSectionCount
        {
            get
            {
                int count = 0;
                foreach (var section in sectionPool)
                    if (section != null && section.CanLoad) count++;
                return count;
            }
        }

        /// <summary>
        /// Sortea el orden de secciones de una run: <see cref="SectionsPerRun"/> secciones
        /// distintas del pool, en orden aleatorio.
        ///
        /// El sorteo va con un <see cref="System.Random"/> propio en vez de con
        /// <see cref="UnityEngine.Random"/> para que la semilla de la run sea reproducible y no
        /// la altere ningún otro sistema que también tire dados ese frame.
        /// </summary>
        /// <param name="seed">Semilla de la run. La misma semilla da siempre la misma secuencia.</param>
        /// <param name="order">Secciones en el orden en que hay que jugarlas.</param>
        /// <returns>False si el mundo no tiene secciones jugables suficientes; deja el motivo en consola.</returns>
        public bool TryBuildRunOrder(int seed, out List<SceneReference> order)
        {
            order = new List<SceneReference>();

            // Sólo entran al sorteo las secciones que de verdad se pueden cargar: así un hueco sin
            // asignar en el pool degrada la variedad de la run en vez de colgarla a mitad.
            var candidates = new List<SceneReference>();
            for (int i = 0; i < sectionPool.Count; i++)
            {
                var section = sectionPool[i];
                if (section == null || !section.IsAssigned)
                {
                    Debug.LogWarning($"[WorldDefinition] '{name}': la sección #{i + 1} del pool está " +
                                     "sin asignar. Se ignora en el sorteo.", this);
                    continue;
                }

                if (!section.CanLoad)
                {
                    Debug.LogError($"[WorldDefinition] '{name}': la sección '{section.Name}' no está en " +
                                   "Build Settings. Se ignora en el sorteo.", this);
                    continue;
                }

                candidates.Add(section);
            }

            if (candidates.Count == 0)
            {
                Debug.LogError($"[WorldDefinition] '{name}' no tiene ninguna sección jugable.", this);
                return false;
            }

            int take = Mathf.Min(SectionsPerRun, candidates.Count);
            if (take < SectionsPerRun)
            {
                Debug.LogWarning($"[WorldDefinition] '{name}' pide {SectionsPerRun} secciones por run " +
                                 $"pero sólo hay {candidates.Count} jugables. La run será más corta.", this);
            }

            // Fisher-Yates parcial: baraja sólo los 'take' primeros, que es todo lo que hace falta.
            var random = new System.Random(seed);
            for (int i = 0; i < take; i++)
            {
                int j = random.Next(i, candidates.Count);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                order.Add(candidates[i]);
            }

            return true;
        }

        private void OnValidate()
        {
            if (sectionsPerRun > sectionPool.Count && sectionPool.Count > 0)
            {
                // Aviso temprano en el Inspector, mucho antes de que se note al jugar.
                Debug.LogWarning($"[WorldDefinition] '{name}': secciones por run ({sectionsPerRun}) es mayor " +
                                 $"que el pool ({sectionPool.Count}).", this);
            }
        }
    }
}
