using System;
using System.Collections;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Run
{
    /// <summary>En qué punto de la run está el jugador.</summary>
    public enum RunPhase
    {
        /// <summary>No hay run: el jugador está en el hub.</summary>
        None,
        /// <summary>Jugando una de las secciones sorteadas.</summary>
        Section,
        /// <summary>Jugando el jefe del mundo.</summary>
        Boss
    }

    /// <summary>
    /// Dueño del ciclo de una run. Singleton persistente (DontDestroyOnLoad), igual que
    /// <see cref="GameStateManager"/> y <c>AudioManager</c>.
    ///
    /// Ciclo: el jugador interactúa con una tumba en MainHub → se sortean N secciones del pool del
    /// mundo → se juegan en secuencia → jefe → mundo siguiente. Si muere en cualquier punto vuelve
    /// a MainHub y la run se pierde.
    ///
    /// <b>Carga de escenas.</b> Cada sección es una escena ligera aparte, cargada en aditivo. Al
    /// empezar la run se crea en memoria una escena vacía (<see cref="SceneManager.CreateScene"/>)
    /// que hace de sostén durante toda la run. Existe por un motivo concreto: Unity no deja
    /// descargar la última escena cargada, así que sin ella habría que cargar la sección siguiente
    /// <i>antes</i> de soltar la actual y el pico de memoria sería el de dos secciones. Con el
    /// sostén se descarga primero y se carga después, y nunca hay más de una sección en memoria —
    /// que es justo lo que se busca en móvil. No es un asset, así que no hay nada que añadir a
    /// Build Settings ni que se pueda romper al renombrar.
    ///
    /// Durante la transición el juego queda congelado vía <see cref="GameStateManager"/>: el
    /// jugador es persistente y sobrevive a la descarga, así que sin congelar se caería por donde
    /// hace un instante había suelo.
    ///
    /// <b>El jugador.</b> Es un único objeto persistente (DontDestroyOnLoad) instanciado una vez
    /// desde <see cref="playerPrefab"/>, y sobrevive tanto a las secciones de una run como a las
    /// visitas al hub: MainHub no tiene su propio Player en la escena, sólo un
    /// <see cref="SectionEntry"/> marcando dónde debe aparecer. Ese mismo objeto es el que se
    /// mueve por el hub entre runs, así que morir no lo destruye — vuelve a colocarse en el
    /// <see cref="SectionEntry"/> del hub con la vida a tope. Sólo se destruye de verdad al volver
    /// al menú principal (<see cref="DiscardRun"/>), que es cuando de verdad ya no hace falta.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        /// <summary>Nombre de la escena-sostén creada en memoria. No es un asset del proyecto.</summary>
        private const string RunRootSceneName = "~RunRoot";

        [Header("Escenas")]
        [Tooltip("El hub. Es a donde se vuelve al morir, al abandonar y al terminar el último mundo.")]
        [SerializeField] private SceneReference hubScene = new SceneReference();

        [Header("Mundos")]
        [Tooltip("Mundos en orden de progresión. El índice de esta lista es el worldIndex que " +
                 "acepta StartRun(int); las tumbas del hub referencian el asset directamente, así " +
                 "que reordenar la lista no las rompe.")]
        [SerializeField] private List<WorldDefinition> worlds = new List<WorldDefinition>();

        [Tooltip("Al matar al jefe, encadenar directamente con el mundo siguiente. Si se apaga, " +
                 "cada mundo devuelve al hub al completarse.")]
        [SerializeField] private bool chainToNextWorld = true;

        [Header("Jugador")]
        [Tooltip("Prefab del jugador que se instancia durante la run. Debe llevar Health y la " +
                 "etiqueta 'Player'. Si se deja vacío, se usa el Player que traiga cada sección " +
                 "(sirve para probar una sección suelta, pero no conserva la vida entre secciones).")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Ritmo")]
        [Tooltip("Segundos (en tiempo real) entre la muerte del jugador y la vuelta al hub, para " +
                 "que dé tiempo a ver la animación de muerte.")]
        [Min(0f)]
        [SerializeField] private float deathReturnDelay = 1.5f;

        [Tooltip("Semilla fija para depurar: la misma semilla da siempre las mismas secciones en el " +
                 "mismo orden. 0 = semilla nueva en cada run.")]
        [SerializeField] private int debugSeed;

        [Header("Música")]
        [Tooltip("Id de la música del hub. Debe haber un clip con este nombre en Resources/Music " +
                 "(o una entrada en el AudioManager). Si no existe, el hub suena en silencio.")]
        [SerializeField] private string hubMusicId = "MainHub";

        [Header("Tienda")]
        [Tooltip("Prefab de la tienda. Se coloca UNA por mundo, en una de las secciones sorteadas " +
                 "elegida al azar (nunca en el jefe). Si el mundo sólo tiene una sección antes del " +
                 "jefe, la tienda cae ahí seguro.")]
        [SerializeField] private GameObject shopPrefab;

        [Tooltip("A qué distancia por delante de la salida se coloca la tienda cuando la sección " +
                 "no trae un ShopSpawnPoint propio.")]
        [SerializeField] private float shopDistanceBeforeExit = 5f;

        [Header("Recompensa del jefe")]
        [Tooltip("Prefab que aparece al caer el jefe, sobre su cadáver (el altar de mejora de " +
                 "arma). Vacío = el jefe no suelta nada.")]
        [SerializeField] private GameObject bossRewardPrefab;

        [Tooltip("Desplazamiento respecto al punto donde murió el jefe, para que no quede " +
                 "enterrado en el suelo.")]
        [SerializeField] private Vector3 bossRewardOffset = new Vector3(0f, 0.5f, 0f);

        [Header("Cámara")]
        [Tooltip("Tamaño ortográfico que se fuerza en todas las cámaras con CameraFollow (hub, " +
                 "secciones y jefe) tras cada carga, para que el zoom sea idéntico y no pegue un " +
                 "salto al empezar o avanzar una run. Ajústalo aquí para buscar el encuadre óptimo.")]
        [Min(0.1f)]
        [SerializeField] private float cameraOrthographicSize = 8.5f;

        [Header("Suavizado de transiciones")]
        [Tooltip("Cada cuántas cargas de escena se hace la limpieza de assets no usados " +
                 "(Resources.UnloadUnusedAssets), que es lo que más tira del frame en una " +
                 "transición. 1 = en cada carga (mínima memoria, lo suyo en móvil). 0 = nunca. " +
                 "En un PC con memoria de sobra, 3-4 deja las transiciones más suaves.")]
        [Min(0)]
        [SerializeField] private int assetCleanupEveryNScenes = 1;

        [Tooltip("Frames (con el juego congelado) que se esperan tras cargar la escena antes de " +
                 "devolver el control, para que el primer frame de trabajo pesado (integrar la " +
                 "escena, compilar shaders) caiga dentro de la transición y no en la primera " +
                 "décima de juego.")]
        [Min(0)]
        [SerializeField] private int postLoadSettleFrames = 2;

        // ------------------------------------------------------------------ estado

        /// <summary>True entre StartRun y la vuelta al hub.</summary>
        public bool RunInProgress { get; private set; }

        /// <summary>Fase actual. <see cref="RunPhase.None"/> mientras se está en el hub.</summary>
        public RunPhase Phase { get; private set; } = RunPhase.None;

        /// <summary>Mundo que se está jugando, o null si no hay run.</summary>
        public WorldDefinition CurrentWorld { get; private set; }

        /// <summary>Semilla con la que se sortearon las secciones de la run actual.</summary>
        public int RunSeed { get; private set; }

        /// <summary>Sección actual dentro de la run, empezando en 1. 0 si no hay run o si toca jefe.</summary>
        public int CurrentSectionNumber => Phase == RunPhase.Section ? _index + 1 : 0;

        /// <summary>Cuántas secciones tiene la run actual, sin contar el jefe.</summary>
        public int SectionCount => _order?.Count ?? 0;

        /// <summary>True mientras hay una carga o descarga de escena en curso.</summary>
        public bool IsTransitioning { get; private set; }

        /// <summary>El jugador de la run, o null si no hay run (o si se juega con el de la sección).</summary>
        public GameObject Player { get; private set; }

        /// <summary>Se dispara al cambiar de fase (sección → sección, sección → jefe, → None).</summary>
        public event Action<RunPhase> PhaseChanged;

        /// <summary>Se dispara al empezar una run (y al encadenar con el mundo siguiente).</summary>
        public event Action<WorldDefinition> RunStarted;

        /// <summary>Se dispara al terminar la run. True si se completó, false si murió o abandonó.</summary>
        public event Action<bool> RunEnded;

        private List<SceneReference> _order;
        private int _index;                 // índice dentro de _order
        private Scene _loadedRunScene;      // la sección o el jefe cargado ahora mismo
        private SectionClearTracker _boundTracker;   // al que se le escucha 'Cleared'
        private Scene _runRootScene;        // escena-sostén creada en memoria
        private Health _playerHealth;
        private GameObject _playerRoot;     // contenedor persistente del jugador de la run
        private Coroutine _flow;
        private bool _frozen;               // si hemos sumado una pausa al contador de GameStateManager
        private int _scenesSinceCleanup;    // para assetCleanupEveryNScenes
        private int _shopSectionIndex = -1; // en qué sección del mundo actual sale la tienda

        // ------------------------------------------------------------------ ciclo de vida

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // SceneManager.sceneLoaded es la única forma fiable de saber que el hub ya está
            // realmente cargado y activo. Un hubScene.Load() hecho síncronamente y seguido de
            // EnterHub() en la misma línea PARECE bastar, pero no lo es cuando ReturnToHub se
            // dispara desde dentro de un callback de físicas (SectionExit.OnTriggerEnter2D →
            // AdvanceSection → CompleteWorld → ReturnToHub): ahí Unity puede diferir la activación
            // real de la escena, así que el código de justo después seguía viendo la escena vieja
            // y "MainHub no tiene SectionEntry" saltaba aunque el HubEntry sí existiera. Con el
            // evento, EnterHub sólo corre cuando la escena está de verdad lista, sea cual sea el
            // contexto desde el que se disparó la carga.
            SceneManager.sceneLoaded += OnSceneLoaded;

            // El cargador asíncrono reparte su trabajo en trozos más pequeños por frame: cargar una
            // sección tarda un pelín más de reloj pero deja de comerse frames enteros, que es lo que
            // se nota como tirón al entrar en una escena. Las transiciones van tapadas por el freeze,
            // así que el tiempo extra de carga no molesta.
            Application.backgroundLoadingPriority = ThreadPriority.Low;

            // Con "Reload Domain" desactivado los estáticos sobreviven entre sesiones de Play.
            _frozen = false;
            Phase = RunPhase.None;
            ResetRunState();
        }

        /// <summary>
        /// Se engancha al contador de enemigos (que se auto-crea después que esto) para enterarse
        /// de cuándo cae el jefe. Se llama tras cada carga porque el tracker puede aparecer más
        /// tarde; suscribirse dos veces no duplica el aviso porque antes se quita.
        /// </summary>
        private void BindClearTracker()
        {
            var tracker = SectionClearTracker.Instance;
            if (tracker == null || tracker == _boundTracker) return;

            if (_boundTracker != null) _boundTracker.Cleared -= OnSectionCleared;

            tracker.Cleared += OnSectionCleared;
            _boundTracker = tracker;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_boundTracker != null) _boundTracker.Cleared -= OnSectionCleared;
            UnsubscribeFromPlayer();
            Unfreeze();
            Instance = null;
        }

        /// <summary>
        /// Cubre tanto arrancar Play directamente sobre MainHub (el propio arranque de la escena
        /// cuenta como una carga) como cada vuelta al hub tras una run. EnterHub es idempotente
        /// (EnsurePlayer no hace nada si el jugador ya existe), así que no importa si esto llega a
        /// dispararse más de una vez para la misma carga real.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (hubScene.IsAssigned && scene.path == hubScene.Path)
                EnterHub();
        }

        // ------------------------------------------------------------------ API pública

        /// <summary>
        /// Empieza una run en el mundo con este índice dentro de <see cref="worlds"/>.
        /// Se mantiene por comodidad; las tumbas del hub usan la sobrecarga con
        /// <see cref="WorldDefinition"/>, que no depende del orden de la lista.
        /// </summary>
        public bool StartRun(int worldIndex)
        {
            if (worldIndex < 0 || worldIndex >= worlds.Count)
            {
                Debug.LogError($"[RunManager] worldIndex {worldIndex} fuera de rango " +
                               $"(hay {worlds.Count} mundos en la lista).", this);
                return false;
            }

            return StartRun(worlds[worldIndex]);
        }

        /// <summary>Empieza una run en el mundo indicado. Devuelve false y explica el motivo si no puede.</summary>
        public bool StartRun(WorldDefinition world)
        {
            if (world == null)
            {
                Debug.LogError("[RunManager] StartRun con un mundo nulo.", this);
                return false;
            }

            if (RunInProgress || IsTransitioning)
            {
                Debug.LogWarning("[RunManager] Ya hay una run en curso; se ignora StartRun.", this);
                return false;
            }

            if (!world.Unlocked)
            {
                Debug.Log($"[RunManager] El mundo '{world.DisplayName}' está bloqueado.", this);
                return false;
            }

            if (!hubScene.IsAssigned)
            {
                Debug.LogError("[RunManager] Falta asignar 'Hub Scene': sin ella no habría forma de " +
                               "volver al hub al morir. Se cancela la run.", this);
                return false;
            }

            RunSeed = debugSeed != 0 ? debugSeed : Environment.TickCount;

            if (!world.TryBuildRunOrder(RunSeed, out _order) || _order.Count == 0)
                return false;

            if (!world.BossScene.CanLoad)
            {
                Debug.LogError($"[RunManager] El mundo '{world.DisplayName}' no tiene una escena de " +
                               "jefe cargable. Se cancela la run.", this);
                return false;
            }

            CurrentWorld = world;
            _index = 0;
            PickShopSection();
            RunInProgress = true;
            SetPhase(RunPhase.Section);

            Debug.Log($"[RunManager] Run en '{world.DisplayName}' (semilla {RunSeed}): " +
                      $"{string.Join(" → ", _order.ConvertAll(s => s.Name))} → {world.BossScene.Name}");

            RunStarted?.Invoke(world);
            _flow = StartCoroutine(EnterCurrentRoutine(firstOfRun: true));
            return true;
        }

        /// <summary>
        /// Avanza al siguiente paso: siguiente sección, jefe, o mundo siguiente si el jefe ya cayó.
        /// La llaman <see cref="SectionExit"/> y el evento <c>Died</c> del jefe.
        /// </summary>
        public void AdvanceSection()
        {
            if (!RunInProgress)
            {
                Debug.LogWarning("[RunManager] AdvanceSection sin run en curso.", this);
                return;
            }

            // Protege de la llamada doble: dos triggers de salida solapados, o el jefe muriendo
            // mientras ya se está cargando lo siguiente.
            if (IsTransitioning) return;

            if (Phase == RunPhase.Boss)
            {
                CompleteWorld();
                return;
            }

            _index++;
            SetPhase(_index < _order.Count ? RunPhase.Section : RunPhase.Boss);
            _flow = StartCoroutine(EnterCurrentRoutine(firstOfRun: false));
        }

        /// <summary>Abandona la run y vuelve al hub sin morir (botón del menú de pausa).</summary>
        public void AbandonRun()
        {
            if (!RunInProgress) return;
            ReturnToHub(completed: false);
        }

        /// <summary>
        /// Cierra la run sin cargar nada y destruye al jugador persistente. La usa el menú de
        /// pausa al salir al menú principal: esa pantalla ya se encarga de su propia carga, y a
        /// diferencia de volver al hub, aquí sí que ya no hace falta conservar al jugador — la
        /// próxima vez que se entre a MainHub se creará uno nuevo desde cero.
        /// </summary>
        public void DiscardRun()
        {
            bool wasActive = RunInProgress || Phase != RunPhase.None;

            ClearRun();
            UnsubscribeFromPlayer();
            DestroyRunPlayer();

            if (wasActive) RunEnded?.Invoke(false);
        }

        /// <summary>Mundo siguiente al indicado en la lista de progresión, o null si es el último.</summary>
        public WorldDefinition NextWorldAfter(WorldDefinition world)
        {
            int i = worlds.IndexOf(world);

            if (i < 0)
            {
                // El encadenado sale de la lista 'Worlds' del RunManager, no del asset del mundo:
                // si el mundo actual no está en ella (o está pero le falta el siguiente detrás),
                // la run termina en el hub aunque exista un WorldDefinition para el mundo siguiente.
                Debug.LogWarning($"[RunManager] '{world.DisplayName}' no está en la lista 'Worlds' del " +
                                 "RunManager, así que no hay forma de saber qué mundo va después. " +
                                 "Añádelo (y los siguientes, en orden) en el Inspector del RunManager.", this);
                return null;
            }

            return i + 1 < worlds.Count ? worlds[i + 1] : null;
        }

        // ------------------------------------------------------------------ flujo de escenas

        /// <summary>Escena que toca cargar ahora mismo, según fase e índice.</summary>
        private SceneReference CurrentTarget =>
            Phase == RunPhase.Boss ? CurrentWorld.BossScene : _order[_index];

        private IEnumerator EnterCurrentRoutine(bool firstOfRun)
        {
            IsTransitioning = true;
            Freeze();

            // 1. La escena-sostén se crea una sola vez por run. A partir de aquí siempre hay al
            //    menos una escena cargada, así que descargar la actual es legal.
            if (firstOfRun || !_runRootScene.IsValid() || !_runRootScene.isLoaded)
            {
                _runRootScene = SceneManager.CreateScene(RunRootSceneName);
                SceneManager.SetActiveScene(_runRootScene);
            }

            // 2. Fuera lo anterior: el hub si es la primera sección, la sección previa si no.
            //    Se descarga ANTES de cargar, que es lo que mantiene el pico de memoria en una
            //    sola sección.
            yield return UnloadPreviousRoutine(firstOfRun);

            // 3. El jugador persistente se crea una vez (si aún no existía, p. ej. si la run
            //    empezó sin haber pasado por el hub) y sobrevive a todas las secciones.
            EnsurePlayer();

            // 4. Dentro la siguiente, en aditivo.
            var target = CurrentTarget;
            var load = target.LoadAsync(LoadSceneMode.Additive, this);
            if (load == null)
            {
                // ResolveForLoad ya ha dejado en consola el motivo exacto.
                Debug.LogError($"[RunManager] No se pudo cargar '{target}'. Se aborta la run.", this);
                IsTransitioning = false;
                ReturnToHub(completed: false);
                yield break;
            }

            while (!load.isDone) yield return null;

            // La integración de la escena (todos los Awake/OnEnable/Start de golpe) es el frame
            // más caro de la transición; se le deja respirar un frame antes de tocar nada más para
            // no encadenar ese pico con el del reposicionamiento y la cámara.
            yield return null;

            _loadedRunScene = target.GetLoadedScene();
            if (_loadedRunScene.IsValid())
                SceneManager.SetActiveScene(_loadedRunScene);

            // 5. Colocar al jugador y apuntarle la cámara de la sección nueva.
            PlacePlayerAtEntry(_loadedRunScene);
            RetargetCameras(_loadedRunScene);
            SpawnShopIfDue(_loadedRunScene);
            BindClearTracker();

            // 6. Música de la fase (World{n}-{puesto} o BossBattle{n}). Si no hay clip con ese
            //    nombre en Resources/Music, PlaySceneMusic deja la escena en silencio sin errores.
            //    Se lanza aquí para que la carga del clip solape con los frames de asentamiento.
            PlayPhaseMusic();

            // 7. Unos frames más, aún congelados, para que el warm-up de shaders y el primer
            //    LateUpdate de la cámara caigan dentro de la transición y no nada más soltar.
            for (int i = 0; i < postLoadSettleFrames; i++)
                yield return null;

            Unfreeze();
            IsTransitioning = false;
            _flow = null;
        }

        // ------------------------------------------------------------------ tienda

        /// <summary>
        /// Elige en qué sección del mundo actual sale la tienda: una al azar de las sorteadas,
        /// nunca el jefe. Como va con la semilla de la run, la misma semilla pone la tienda en el
        /// mismo sitio. Si el mundo sólo tiene una sección antes del jefe, sale ahí seguro — que es
        /// justo lo que se busca: <b>una tienda por mundo, siempre antes del jefe</b>.
        /// </summary>
        private void PickShopSection()
        {
            // El XOR desmarca esta tirada de la del orden de secciones: con la misma semilla, la
            // tienda no queda siempre pegada a la misma posición del sorteo.
            const int ShopSeedSalt = 0x5409;

            _shopSectionIndex = _order != null && _order.Count > 0
                ? new System.Random(RunSeed ^ ShopSeedSalt).Next(0, _order.Count)
                : -1;
        }

        /// <summary>
        /// Instancia la tienda en la sección recién cargada, si es la que tocaba. La escena del
        /// jefe nunca la lleva porque <see cref="_shopSectionIndex"/> indexa sólo las secciones.
        /// </summary>
        private void SpawnShopIfDue(Scene scene)
        {
            if (shopPrefab == null || Phase != RunPhase.Section) return;
            if (_index != _shopSectionIndex) return;
            if (!scene.IsValid() || !scene.isLoaded) return;

            var shop = Instantiate(shopPrefab, ResolveShopPosition(scene), Quaternion.identity);
            shop.name = shopPrefab.name;
            SceneManager.MoveGameObjectToScene(shop, scene);

            // La tienda no trae enemigos, pero re-escanear deja el contador correcto pase lo que
            // pase si algún día el prefab spawnea algo con vida.
            SectionClearTracker.Instance?.Track(scene);
        }

        /// <summary>
        /// Suelta la recompensa del jefe cuando cae el último enemigo de la escena del jefe.
        ///
        /// Se engancha al <see cref="SectionClearTracker"/> en vez de pedir que cada jefe lleve un
        /// componente propio: el jefe es "lo último que queda vivo en su escena", así que cualquier
        /// escena de jefe suelta su recompensa sin tener que acordarse de configurar nada. Aparece
        /// sobre el cadáver, y el mundo no avanza por soltarla — el jugador la usa y sale por su
        /// pie.
        /// </summary>
        private void OnSectionCleared()
        {
            if (bossRewardPrefab == null || Phase != RunPhase.Boss) return;
            if (!_loadedRunScene.IsValid() || !_loadedRunScene.isLoaded) return;

            var tracker = SectionClearTracker.Instance;
            Vector3 position = tracker != null ? tracker.LastDeathPosition : Vector3.zero;

            var reward = Instantiate(bossRewardPrefab, position + bossRewardOffset, Quaternion.identity);
            reward.name = bossRewardPrefab.name;
            SceneManager.MoveGameObjectToScene(reward, _loadedRunScene);
        }

        /// <summary>
        /// Dónde plantar la tienda: si la sección trae un <see cref="ShopSpawnPoint"/> manda ese;
        /// si no, un poco antes de la salida (que es por donde el jugador pasa sí o sí); y como
        /// último recurso, junto a la entrada.
        /// </summary>
        private Vector3 ResolveShopPosition(Scene scene)
        {
            SectionExit exit = null;

            foreach (var root in scene.GetRootGameObjects())
            {
                var marker = root.GetComponentInChildren<ShopSpawnPoint>(true);
                if (marker != null) return marker.transform.position;

                exit ??= root.GetComponentInChildren<SectionExit>(true);
            }

            if (exit != null)
                return exit.transform.position + Vector3.left * shopDistanceBeforeExit;

            var entry = SectionEntry.FindIn(scene);
            return entry != null ? entry.SpawnPosition + Vector3.right * 6f : Vector3.zero;
        }

        private IEnumerator UnloadPreviousRoutine(bool firstOfRun)
        {
            Scene toUnload = firstOfRun ? hubScene.GetLoadedScene() : _loadedRunScene;

            if (!toUnload.IsValid() || !toUnload.isLoaded)
                yield break;

            var unload = SceneManager.UnloadSceneAsync(toUnload);
            while (unload != null && !unload.isDone) yield return null;

            _loadedRunScene = default;

            // Liberar de verdad texturas y mallas de lo que se acaba de ir: sin esto la memoria
            // sólo baja cuando al recolector le apetece, que en móvil llega tarde. Es el paso que
            // más pesa en una transición, así que su frecuencia se controla desde el Inspector
            // (assetCleanupEveryNScenes): en móvil, cada carga; en PC con memoria de sobra, cada
            // varias, para que las transiciones sean más suaves.
            if (assetCleanupEveryNScenes > 0 && ++_scenesSinceCleanup >= assetCleanupEveryNScenes)
            {
                _scenesSinceCleanup = 0;
                var cleanup = Resources.UnloadUnusedAssets();
                while (!cleanup.isDone) yield return null;
            }
        }

        /// <summary>
        /// Al caer el jefe: si hay un mundo siguiente en <see cref="worlds"/> y su casilla "Mundo
        /// desbloqueado" (<see cref="WorldDefinition.Unlocked"/>) está activa, se encadena
        /// directamente con él sin pasar por el hub. Si no hay mundo siguiente, o existe pero
        /// sigue bloqueado, la run termina y se vuelve al hub.
        /// </summary>
        private void CompleteWorld()
        {
            var next = chainToNextWorld ? NextWorldAfter(CurrentWorld) : null;

            if (next == null)
            {
                Debug.Log($"[RunManager] '{CurrentWorld.DisplayName}' completado. No hay mundo siguiente.");
                ReturnToHub(completed: true);
                return;
            }

            if (!next.Unlocked)
            {
                Debug.Log($"[RunManager] El mundo siguiente ('{next.DisplayName}') está bloqueado; " +
                          "la run termina aquí.");
                ReturnToHub(completed: true);
                return;
            }

            // Encadenar sin pasar por el hub: se resortea el orden pero el jugador sigue tal cual
            // está (vida y mejoras incluidas), que es lo que le da tensión a la run larga.
            RunSeed = debugSeed != 0 ? debugSeed : Environment.TickCount;
            if (!next.TryBuildRunOrder(RunSeed, out _order) || _order.Count == 0 || !next.BossScene.CanLoad)
            {
                Debug.LogError($"[RunManager] '{next.DisplayName}' no está listo para jugarse; la run " +
                               "termina en el hub.", this);
                ReturnToHub(completed: true);
                return;
            }

            Debug.Log($"[RunManager] '{CurrentWorld.DisplayName}' completado → '{next.DisplayName}' " +
                      $"(semilla {RunSeed}).");

            CurrentWorld = next;
            _index = 0;
            PickShopSection();
            SetPhase(RunPhase.Section);
            RunStarted?.Invoke(next);
            _flow = StartCoroutine(EnterCurrentRoutine(firstOfRun: false));
        }

        /// <summary>
        /// Cierra la run y vuelve al hub con una carga Single, que se lleva por delante de una vez
        /// la sección cargada y la escena-sostén. El jugador persistente sobrevive a esa carga
        /// (está en DontDestroyOnLoad); <see cref="OnSceneLoaded"/> es quien llama a
        /// <see cref="EnterHub"/> en cuanto la escena está de verdad activa.
        /// </summary>
        private void ReturnToHub(bool completed)
        {
            ClearRun();

            if (!hubScene.Load(this))
            {
                Debug.LogError("[RunManager] No se pudo volver al hub. Revisa 'Hub Scene' en el " +
                               "RunManager.", this);
            }

            RunEnded?.Invoke(completed);
        }

        /// <summary>
        /// Deja al jugador listo dentro del hub: lo crea si aún no existe, lo coloca en el
        /// <see cref="SectionEntry"/> del hub, le resetea la vida (se entra siempre sano) y le
        /// apunta la cámara de la escena. Se llama tanto al arrancar Play ya dentro de MainHub
        /// como cada vez que se vuelve a él tras una run.
        /// </summary>
        private void EnterHub()
        {
            EnsurePlayer();

            // La música del hub no depende del jugador: se pone aunque no haya playerPrefab.
            AudioManager.Instance?.PlaySceneMusic(hubMusicId);

            if (Player == null) return;    // sin playerPrefab asignado: no hay nada que colocar

            MoveToHubEntry(Player);
            _playerHealth?.ResetHealth();
            RetargetCameras(hubScene.GetLoadedScene());
        }

        /// <summary>Coloca a <paramref name="player"/> en el <see cref="SectionEntry"/> del hub y le corta la inercia.</summary>
        private void MoveToHubEntry(GameObject player)
        {
            var entry = SectionEntry.FindIn(hubScene.GetLoadedScene());
            if (entry == null)
            {
                Debug.LogWarning("[RunManager] MainHub no tiene ningún SectionEntry: el jugador se " +
                                 "queda donde esté en vez de aparecer en un punto conocido.", this);
                return;
            }

            player.transform.position = entry.SpawnPosition;

            var body = player.GetComponentInChildren<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            player.GetComponentInChildren<Knockback>()?.Cancel();
        }

        // ------------------------------------------------------------------ jugador

        /// <summary>Crea el jugador persistente la primera vez que hace falta. Idempotente.</summary>
        private void EnsurePlayer()
        {
            if (Player != null) return;

            if (playerPrefab == null)
            {
                // Sin prefab se juega con el Player que traiga la sección (o, en el hub, con
                // ninguno). Vale para probar una sección suelta desde el editor, pero no hay
                // jugador persistente que conserve vida ni pueda respawnear en el hub.
                Debug.LogWarning("[RunManager] No hay 'Player Prefab' asignado: no habrá un jugador " +
                                 "persistente. Las secciones usarán el Player que traigan y el hub " +
                                 "se quedará sin jugador propio.", this);
                return;
            }

            if (_playerRoot == null)
            {
                _playerRoot = new GameObject("RunPlayer");
                DontDestroyOnLoad(_playerRoot);
            }

            Player = Instantiate(playerPrefab, _playerRoot.transform);
            Player.name = playerPrefab.name;   // sin el "(Clone)", que ensucia la jerarquía
            SubscribeToPlayer(Player);
        }

        /// <summary>
        /// Engancha la muerte del jugador. Es el mismo evento <see cref="Health.Died"/> que ya usan
        /// <c>PlayerMovement</c> y <c>EnemyController</c>, así que no hace falta tocar Health.
        /// </summary>
        private void SubscribeToPlayer(GameObject player)
        {
            UnsubscribeFromPlayer();

            _playerHealth = player != null ? player.GetComponentInChildren<Health>() : null;
            if (_playerHealth == null)
            {
                Debug.LogError("[RunManager] El jugador de la run no tiene componente Health: no se " +
                               "detectará su muerte y la run no podrá terminar.", this);
                return;
            }

            _playerHealth.Died += OnPlayerDied;
        }

        private void UnsubscribeFromPlayer()
        {
            if (_playerHealth == null) return;

            _playerHealth.Died -= OnPlayerDied;
            _playerHealth = null;
        }

        /// <summary>
        /// Qué significa morir: si es durante una run, la run termina entera (vuelta al hub
        /// incluida); si es dentro del hub (sin run en curso), es sólo un respawn en el sitio.
        /// </summary>
        private void OnPlayerDied()
        {
            StartCoroutine(RunInProgress ? RunDeathRoutine() : HubDeathRoutine());
        }

        /// <summary>
        /// Morir durante una run la termina por completo: se descarga la sección/jefe actual, se
        /// resetea el progreso de la run (mundo, orden de secciones, índice) y se vuelve al hub,
        /// donde el jugador reaparece sano en su punto de entrada. Es el comportamiento estándar
        /// de roguelite: los enemigos y el estado de las secciones no sobreviven porque sus
        /// escenas se descargan sin más — no hace falta resetearlos aparte.
        /// </summary>
        private IEnumerator RunDeathRoutine()
        {
            // Sin congelar: se deja correr la animación de muerte. El tiempo va en real por si algo
            // dejó timeScale a 0.
            if (deathReturnDelay > 0f)
                yield return new WaitForSecondsRealtime(deathReturnDelay);

            // PUNTO DE EXTENSIÓN (meta-progresión): aquí, ANTES de ReturnToHub, es donde se
            // guardaría cualquier progreso persistente ganado durante la run (moneda, experiencia,
            // objetos desbloqueados...) en un sistema aparte que no dependa de la escena. Ese
            // sistema no existe todavía — por ahora morir no deja nada permanente, que es lo
            // correcto mientras no haya nada que conservar.

            ReturnToHub(completed: false);
        }

        /// <summary>
        /// Morir dentro del hub (no hay run que terminar) es sólo un respawn: no se carga ninguna
        /// escena, el jugador vuelve a aparecer en el <see cref="SectionEntry"/> del hub con la
        /// vida a tope.
        /// </summary>
        private IEnumerator HubDeathRoutine()
        {
            if (deathReturnDelay > 0f)
                yield return new WaitForSecondsRealtime(deathReturnDelay);

            if (Player != null) MoveToHubEntry(Player);
            _playerHealth?.ResetHealth();
        }

        private void PlacePlayerAtEntry(Scene scene)
        {
            var entry = SectionEntry.FindIn(scene);

            // Sin prefab de jugador se usa el que trae la sección, que ya está donde lo puso el
            // diseñador; sólo hay que engancharle la muerte.
            if (Player == null)
            {
                var scenePlayer = GameObject.FindGameObjectWithTag("Player");
                if (scenePlayer != null) SubscribeToPlayer(scenePlayer);
                return;
            }

            if (entry == null)
            {
                Debug.LogError($"[RunManager] La escena '{scene.name}' no tiene ningún SectionEntry: " +
                               "el jugador aparecería en el origen. Añade uno al montar la sección.", this);
                return;
            }

            Player.transform.position = entry.SpawnPosition;

            // Cortar la inercia que traía de la sección anterior, incluida la de un empujón a
            // medias: si no, se entra en la sección nueva despedido y sin control unos frames.
            var body = Player.GetComponentInChildren<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            Player.GetComponentInChildren<Knockback>()?.Cancel();
        }

        /// <summary>
        /// Apunta las cámaras de la escena recién cargada al jugador de la run y les fija el mismo
        /// <see cref="cameraOrthographicSize"/>, para que el zoom no cambie entre el hub y las
        /// secciones (cada escena trae su cámara con su propio tamaño y sin esto se notaría el
        /// salto al empezar la run).
        ///
        /// <c>CameraFollow</c> se busca su objetivo por etiqueta en su propio Start, pero eso sólo
        /// funciona si el jugador ya existe en ese instante; hacerlo explícito aquí quita esa
        /// dependencia de orden.
        /// </summary>
        private void RetargetCameras(Scene scene)
        {
            if (Player == null || !scene.IsValid() || !scene.isLoaded) return;

            foreach (var root in scene.GetRootGameObjects())
            {
                var cameras = root.GetComponentsInChildren<CameraFollow>(true);
                foreach (var follow in cameras)
                {
                    follow.SetTarget(Player.transform);

                    var cam = follow.GetComponent<Camera>();
                    if (cam != null && cam.orthographic)
                        cam.orthographicSize = cameraOrthographicSize;
                }
            }
        }

        private void DestroyRunPlayer()
        {
            if (Player != null) Destroy(Player);
            Player = null;

            if (_playerRoot != null) Destroy(_playerRoot);
            _playerRoot = null;
        }

        // ------------------------------------------------------------------ utilidades

        /// <summary>
        /// Id de música por convención para la fase actual: <c>World{n}-{puesto}</c> para las
        /// secciones (el puesto es el orden dentro de la run, 1..N, no el nombre de la escena) y
        /// <c>BossBattle{n}</c> para el jefe, donde <c>n</c> es <see cref="WorldDefinition.WorldNumber"/>.
        /// Dejar un clip con ese nombre en Resources/Music le pone música a esa fase; si no existe,
        /// esa fase suena en silencio, así que añadir el Mundo 2 no obliga a tocar nada aquí.
        /// </summary>
        private string CurrentMusicId()
        {
            int world = CurrentWorld != null ? CurrentWorld.WorldNumber : 0;
            return Phase == RunPhase.Boss
                ? $"BossBattle{world}"
                : $"World{world}-{CurrentSectionNumber}";
        }

        private void PlayPhaseMusic() => AudioManager.Instance?.PlaySceneMusic(CurrentMusicId());

        private void SetPhase(RunPhase phase)
        {
            if (Phase == phase) return;

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        /// <summary>
        /// Congela el juego durante una transición. Va contra el contador de
        /// <see cref="GameStateManager"/>, así que se lleva la cuenta con <see cref="_frozen"/>
        /// para no desbalancearlo si la transición se corta a medias.
        /// </summary>
        private void Freeze()
        {
            if (_frozen || GameStateManager.Instance == null) return;

            _frozen = true;
            GameStateManager.Instance.SetPaused(true);
        }

        private void Unfreeze()
        {
            if (!_frozen) return;

            _frozen = false;
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SetPaused(false);
        }

        /// <summary>
        /// Deja el RunManager como si no hubiera run: corta la corutina de transición, resetea el
        /// progreso y descongela. Deliberadamente NO toca al jugador — sigue siendo el mismo
        /// objeto persistente tanto si se sigue jugando (vuelta al hub) como si no, así que
        /// destruirlo aquí sería prematuro. Sólo <see cref="DiscardRun"/> (irse al menú principal)
        /// lo destruye de verdad. Tampoco carga ninguna escena — de eso se encarga quien llama,
        /// porque no siempre se va al mismo sitio.
        /// </summary>
        private void ClearRun()
        {
            if (_flow != null)
            {
                StopCoroutine(_flow);
                _flow = null;
            }

            // Primero descongelar: Unfreeze devuelve al contador de GameStateManager la pausa que
            // sumó la transición, y sólo lo hace si _frozen sigue en true. Si se limpiara el estado
            // antes, esa pausa se quedaría sin devolver y el hub arrancaría con timeScale a 0.
            Unfreeze();

            ResetRunState();

            // Después de limpiar, para que quien escuche PhaseChanged vea ya el estado final.
            SetPhase(RunPhase.None);
        }

        /// <summary>Limpia los campos de la run. No toca Phase: de eso se encarga <see cref="SetPhase"/>.</summary>
        private void ResetRunState()
        {
            RunInProgress = false;
            IsTransitioning = false;
            CurrentWorld = null;
            _order = null;
            _index = 0;
            _loadedRunScene = default;
            _runRootScene = default;
        }
    }
}
