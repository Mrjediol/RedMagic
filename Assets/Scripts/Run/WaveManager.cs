using System;
using System.Collections;
using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Items;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RedMagic.Run
{
    /// <summary>
    /// Oleadas de enemigos de un nivel, por salas. Cada oleada (<see cref="WaveDefinition"/>) dice cuándo empieza
    /// (al empezar el nivel, al entrar el jugador en su <see cref="LevelRoom"/>, o cuando la lanza otra) y qué pasa al
    /// acabar (lanzar otra oleada, terminar su sala —abre sus puertas y puede soltar un item en el centro— y/o terminar
    /// el nivel). Varias pueden estar en marcha a la vez: los enemigos que ya esperan en la sala siguiente son una oleada
    /// lanzada al terminar la anterior.
    ///
    /// Al entrar el jugador del todo en una sala con oleadas sin terminar, se cierran sus puertas.
    ///
    /// No toca los prefabs de enemigo: instancia el prefab tal cual y escucha
    /// <see cref="Health.AnyDied"/>, así que vale cualquier cosa con <see cref="Health"/> (enemigos
    /// del pipeline, los viejos de <c>EnemyController</c>, adds…).
    ///
    /// Mantiene cerrada la sección (<see cref="SectionClearTracker"/>: la salida y la tienda) hasta que termina una
    /// oleada marcada <c>finishLevel</c>. Los enemigos de las oleadas NO se registran en el tracker: una sala opcional
    /// sin limpiar no debe impedir salir.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaveManager : MonoBehaviour
    {
        [Tooltip("Objetos vacíos de la escena donde pueden aparecer enemigos. Cada entrada de " +
                 "oleada elige uno de éstos.")]
        [SerializeField] private List<Transform> spawnPoints = new();

        [SerializeField] private List<WaveDefinition> waves = new();

        [Tooltip("Arranca solo al cargar la escena (oleadas 'al empezar el nivel' y vigilar las salas). Apagado = " +
                 "esperar a StartWaves().")]
        [SerializeField] private bool startOnLoad = true;

        // Datos de antes de las salas: se convierten una vez a la secuencia de siempre (ver Migrate).
        [SerializeField, HideInInspector] private bool roomsMigrated;

        /// <summary>Empieza una oleada (índice desde 0), justo antes del primer spawn.</summary>
        public event Action<int> OnWaveStart;

        /// <summary>Muere el último enemigo de una oleada (índice desde 0).</summary>
        public event Action<int> OnWaveComplete;

        /// <summary>Nivel superado: terminó una oleada con 'terminar nivel'.</summary>
        public event Action OnAllWavesComplete;

        /// <summary>
        /// Cualquier enemigo que salga de cualquier oleada: (manager, oleada desde 0, su vida). Para
        /// reglas globales sobre oleadas (p. ej. la pasiva que ralentiza la primera).
        /// </summary>
        public static event Action<WaveManager, int, Health> AnyEnemySpawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => AnyEnemySpawned = null;

        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;
        public IReadOnlyList<WaveDefinition> Waves => waves;

        /// <summary>Última oleada que ha empezado (desde 0); -1 antes de empezar.</summary>
        public int CurrentWave { get; private set; } = -1;

        /// <summary>En marcha: arrancado y el nivel aún sin superar.</summary>
        public bool Running { get; private set; }

        /// <summary>Nivel superado.</summary>
        public bool Finished { get; private set; }

        /// <summary>Enemigos vivos de todas las oleadas en curso.</summary>
        public int AliveCount
        {
            get { int n = 0; if (_alive != null) foreach (var set in _alive) n += set.Count; return n; }
        }

        private enum WaveState { Idle, Waiting, Running, Done }

        private WaveState[] _state;
        private HashSet<Health>[] _alive;
        private int[] _pending;
        private readonly List<Health> _prune = new();
        private readonly List<LevelRoom> _rooms = new();
        private readonly HashSet<LevelRoom> _entered = new();
        private readonly HashSet<LevelRoom> _finishedRooms = new();
        private Collider2D _player;
        private bool _started, _holding;

        private void Awake()
        {
            if (!roomsMigrated) Migrate();   // escena vieja sin pasar por el editor: se convierte en memoria
        }

        private void OnEnable() => Health.AnyDied += OnAnyDied;

        private void OnDisable()
        {
            Health.AnyDied -= OnAnyDied;
            // Descarga a medias (muerte, salir al menú): sin aviso de "despejada".
            ReleaseSection(announce: false);
        }

        // En Start y no en Awake: el tracker escanea la escena en sceneLoaded (entre ambos) y,
        // si lo crea su respaldo AfterSceneLoad, tampoco existe todavía en Awake.
        private void Start()
        {
            if (startOnLoad) StartWaves();
        }

        /// <summary>Arranca: lanza las oleadas 'al empezar el nivel' y empieza a vigilar las salas. Una sola vez.</summary>
        public void StartWaves()
        {
            if (_started) return;
            if (waves.Count == 0)
            {
                Debug.LogWarning($"[WaveManager] '{name}' no tiene oleadas.", this);
                return;
            }

            _started = true;
            Running = true;
            int n = waves.Count;
            _state = new WaveState[n];
            _pending = new int[n];
            _alive = new HashSet<Health>[n];
            for (int i = 0; i < n; i++) _alive[i] = new HashSet<Health>();

            _rooms.Clear();
            foreach (var w in waves)
                if (w.room != null && !_rooms.Contains(w.room)) _rooms.Add(w.room);

            HoldSection();
            for (int i = 0; i < n; i++)
                if (waves[i].start == WaveStart.OnLoad) Launch(i);
        }

        private void Update()
        {
            if (!_started) return;
            CheckRooms();
            CheckWaves();
        }

        // ------------------------------------------------------------------ salas

        private void CheckRooms()
        {
            if (_rooms.Count == _entered.Count) return;
            if (_player == null)
            {
                var go = GameObject.FindWithTag("Player");
                if (go == null || !go.TryGetComponent(out _player)) return;
            }
            if (!_player.gameObject.activeInHierarchy) return;

            var bounds = _player.bounds;
            foreach (var room in _rooms)
            {
                if (_entered.Contains(room) || !room.ContainsFully(bounds)) continue;
                _entered.Add(room);
                OnRoomEntered(room);
            }
        }

        private void OnRoomEntered(LevelRoom room)
        {
            if (!_finishedRooms.Contains(room) && HasUnfinishedWaves(room)) room.Lock();
            for (int i = 0; i < waves.Count; i++)
                if (waves[i].start == WaveStart.OnRoomEnter && waves[i].room == room) Launch(i);
        }

        private bool HasUnfinishedWaves(LevelRoom room)
        {
            for (int i = 0; i < waves.Count; i++)
                if (waves[i].room == room && _state[i] != WaveState.Done) return true;
            return false;
        }

        private void FinishRoom(LevelRoom room, ItemDefinition drop)
        {
            if (room == null || !_finishedRooms.Add(room)) return;
            room.Unlock();
            if (drop != null) ItemPickup.Spawn(drop, room.Center);
        }

        // ------------------------------------------------------------------ oleadas

        /// <summary>Lanza la oleada <paramref name="index"/> (desde 0). Cada oleada sale una sola vez.</summary>
        public void Launch(int index)
        {
            if (!_started || index < 0 || index >= waves.Count || _state[index] != WaveState.Idle) return;
            _state[index] = WaveState.Waiting;
            StartCoroutine(RunWave(index));
        }

        private IEnumerator RunWave(int i)
        {
            var wave = waves[i];
            if (wave.startDelay > 0f) yield return new WaitForSeconds(wave.startDelay);

            _state[i] = WaveState.Running;
            CurrentWave = i;
            OnWaveStart?.Invoke(i);
            SystemSounds.Play(s => s.waveStart);

            _pending[i] = wave.enemies.Count;
            foreach (var entry in wave.enemies) StartCoroutine(SpawnAfterDelay(entry, i));
        }

        private void CheckWaves()
        {
            for (int i = 0; i < waves.Count; i++)
            {
                // Sondeo además del evento: un enemigo destruido sin morir (caída al vacío,
                // limpieza de otro sistema) no dispara Died y dejaría la oleada colgada.
                if (_state[i] == WaveState.Running && _pending[i] == 0 && PruneAlive(i) == 0) Complete(i);
            }
        }

        private void Complete(int i)
        {
            _state[i] = WaveState.Done;
            var wave = waves[i];
            OnWaveComplete?.Invoke(i);

            if (wave.finishRoom) FinishRoom(wave.room, wave.dropItem);
            if (wave.launchWave) Launch(LaunchTarget(i));

            if (wave.finishLevel || (!AnyFinishesLevel() && AllDone())) FinishLevel();
        }

        /// <summary>A qué oleada apunta 'lanzar oleada' de <paramref name="i"/>: la elegida, o la siguiente.</summary>
        public int LaunchTarget(int i) => waves[i].launchIndex >= 0 ? waves[i].launchIndex : i + 1;

        private void FinishLevel()
        {
            if (Finished) return;
            Finished = true;
            Running = false;
            ReleaseSection(announce: true);
            OnAllWavesComplete?.Invoke();
        }

        private bool AnyFinishesLevel()
        {
            foreach (var w in waves) if (w.finishLevel) return true;
            return false;
        }

        private bool AllDone()
        {
            foreach (var s in _state) if (s != WaveState.Done) return false;
            return true;
        }

        private IEnumerator SpawnAfterDelay(EnemySpawn entry, int waveIndex)
        {
            if (entry.spawnDelay > 0f) yield return new WaitForSeconds(entry.spawnDelay);

            try { Spawn(entry, waveIndex); }
            finally { _pending[waveIndex]--; }
        }

        private void Spawn(EnemySpawn entry, int waveIndex)
        {
            if (entry.enemyPrefab == null)
            {
                Debug.LogWarning($"[WaveManager] Oleada {waveIndex + 1}: entrada sin prefab.", this);
                return;
            }

            var point = entry.spawnPoint != null ? entry.spawnPoint : transform;
            if (entry.spawnPoint == null)
                Debug.LogWarning($"[WaveManager] Oleada {waveIndex + 1}: '{entry.enemyPrefab.name}' " +
                                 "sin punto de spawn, sale en el WaveManager.", this);

            var go = Instantiate(entry.enemyPrefab, point.position, Quaternion.identity);

            // Si la plantilla es un enemigo de la escena apagado, el clon nace apagado también.
            if (!go.activeSelf) go.SetActive(true);

            // Las secciones se cargan aditivas: sin esto el enemigo nacería en la escena activa
            // y no se descargaría con la sección.
            if (go.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            SystemSounds.PlayAt(s => s.enemySpawn, point.position);

            var health = go.GetComponentInChildren<Health>();
            if (health == null)
            {
                Debug.LogWarning($"[WaveManager] '{entry.enemyPrefab.name}' no tiene Health: no " +
                                 "cuenta para terminar la oleada.", this);
                return;
            }

            if (health.IsDead) return;
            _alive[waveIndex].Add(health);
            AnyEnemySpawned?.Invoke(this, waveIndex, health);
        }

        private void OnAnyDied(Health health)
        {
            if (_alive == null) return;
            foreach (var set in _alive) set.Remove(health);
        }

        private int PruneAlive(int i)
        {
            var set = _alive[i];
            _prune.Clear();
            foreach (var h in set)
                if (h == null || h.IsDead) _prune.Add(h);
            foreach (var h in _prune) set.Remove(h);
            return set.Count;
        }

        private void HoldSection()
        {
            if (_holding || SectionClearTracker.Instance == null) return;
            SectionClearTracker.Instance.AddHold(this);
            _holding = true;
        }

        private void ReleaseSection(bool announce)
        {
            if (!_holding) return;
            _holding = false;
            if (SectionClearTracker.Instance != null) SectionClearTracker.Instance.ReleaseHold(this, announce);
        }

        // ------------------------------------------------------------------ datos

        /// <summary>
        /// Convierte las oleadas de antes de las salas a lo que hacían: la 1ª al empezar el nivel, cada una lanza la
        /// siguiente al terminar y la última termina el nivel.
        /// </summary>
        private void Migrate()
        {
            roomsMigrated = true;
            for (int i = 0; i < waves.Count; i++)
            {
                var w = waves[i];
                w.start = i == 0 ? WaveStart.OnLoad : WaveStart.Launched;
                w.launchWave = i < waves.Count - 1;
                w.launchIndex = -1;
                w.finishRoom = false;
                w.finishLevel = i == waves.Count - 1;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (roomsMigrated) return;
            Migrate();
            EditorUtility.SetDirty(this);
        }
#endif

        // ------------------------------------------------------------------ gizmos

#if UNITY_EDITOR
        /// <summary>Color de la oleada <paramref name="index"/> (desde 0), para gizmos e Inspector.</summary>
        public static Color WaveColor(int index) =>
            Color.HSVToRGB(index * 0.61803f % 1f, 0.75f, 1f);

        // Siempre visible, no sólo seleccionado: es el plano de la sección.
        private void OnDrawGizmos()
        {
            var lines = new Dictionary<Transform, List<string>>();
            var firstWave = new Dictionary<Transform, int>();

            for (int w = 0; w < waves.Count; w++)
            {
                var counts = new Dictionary<(Transform, string), int>();
                foreach (var e in waves[w].enemies)
                {
                    if (e.spawnPoint == null) continue;
                    var key = (e.spawnPoint, e.enemyPrefab != null ? e.enemyPrefab.name : "¿?");
                    counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
                    firstWave.TryAdd(e.spawnPoint, w);
                }

                foreach (var ((point, enemy), count) in counts)
                {
                    if (!lines.TryGetValue(point, out var list)) lines[point] = list = new List<string>();
                    list.Add(count > 1 ? $"W{w + 1}  {enemy} ×{count}" : $"W{w + 1}  {enemy}");
                }
            }

            var style = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.white } };

            // Los puntos de la lista y cualquiera arrastrado a mano en una entrada.
            var points = new HashSet<Transform>(spawnPoints);
            points.UnionWith(lines.Keys);

            foreach (var point in points)
            {
                if (point == null) continue;
                var pos = point.position;
                bool used = firstWave.TryGetValue(point, out int wave);
                var color = used ? WaveColor(wave) : new Color(0.6f, 0.6f, 0.6f);

                Gizmos.color = new Color(color.r, color.g, color.b, 0.35f);
                Gizmos.DrawSphere(pos, 0.35f);
                Gizmos.color = color;
                Gizmos.DrawWireSphere(pos, 0.35f);

                string label = point.name;
                if (used) label += "\n" + string.Join("\n", lines[point]);
                else label += "\n(sin usar)";

                style.normal.textColor = color;
                Handles.Label(pos + Vector3.up * 0.6f, label, style);
            }
        }
#endif
    }
}
