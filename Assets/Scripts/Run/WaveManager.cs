using System;
using System.Collections;
using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RedMagic.Run
{
    /// <summary>
    /// Oleadas de enemigos de una escena de combate. Al cargar la escena lanza la oleada 1; cuando
    /// muere el último enemigo de una oleada lanza la siguiente, hasta acabarlas.
    ///
    /// No toca los prefabs de enemigo: instancia el prefab tal cual y escucha
    /// <see cref="Health.AnyDied"/>, así que vale cualquier cosa con <see cref="Health"/> (enemigos
    /// del pipeline, los viejos de <c>EnemyController</c>, adds…).
    ///
    /// Mantiene cerrada la sección mientras queden oleadas: retiene
    /// <see cref="SectionClearTracker"/> (la salida y la tienda no se abren entre oleadas) y le
    /// registra cada enemigo que spawnea, porque el tracker sólo escanea la escena al cargarla.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaveManager : MonoBehaviour
    {
        [Tooltip("Objetos vacíos de la escena donde pueden aparecer enemigos. Cada entrada de " +
                 "oleada elige uno de éstos.")]
        [SerializeField] private List<Transform> spawnPoints = new();

        [SerializeField] private List<WaveDefinition> waves = new();

        [Tooltip("Lanza la oleada 1 sola al cargar la escena. Apagado = esperar a StartWaves().")]
        [SerializeField] private bool startOnLoad = true;

        /// <summary>Empieza una oleada (índice desde 0), justo antes del primer spawn.</summary>
        public event Action<int> OnWaveStart;

        /// <summary>Muere el último enemigo de una oleada (índice desde 0).</summary>
        public event Action<int> OnWaveComplete;

        /// <summary>Terminada la última oleada.</summary>
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

        /// <summary>Oleada en curso (desde 0); -1 antes de empezar.</summary>
        public int CurrentWave { get; private set; } = -1;

        public bool Running { get; private set; }
        public bool Finished { get; private set; }

        /// <summary>Enemigos vivos de la oleada en curso.</summary>
        public int AliveCount => _alive.Count;

        private readonly HashSet<Health> _alive = new();
        private readonly List<Health> _prune = new();
        private int _pendingSpawns;
        private bool _holding;

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

        /// <summary>Lanza la secuencia desde la oleada 1. No hace nada si ya está en marcha.</summary>
        public void StartWaves()
        {
            if (Running || Finished) return;

            if (waves.Count == 0)
            {
                Debug.LogWarning($"[WaveManager] '{name}' no tiene oleadas.", this);
                return;
            }

            StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            Running = true;
            HoldSection();

            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                if (wave.startDelay > 0f) yield return new WaitForSeconds(wave.startDelay);

                CurrentWave = i;
                OnWaveStart?.Invoke(i);

                _pendingSpawns = wave.enemies.Count;
                foreach (var entry in wave.enemies) StartCoroutine(SpawnAfterDelay(entry, i));

                // Sondeo además del evento: un enemigo destruido sin morir (caída al vacío,
                // limpieza de otro sistema) no dispara Died y dejaría la oleada colgada.
                while (_pendingSpawns > 0 || PruneAlive() > 0) yield return null;

                OnWaveComplete?.Invoke(i);
            }

            Running = false;
            Finished = true;
            ReleaseSection(announce: true);
            OnAllWavesComplete?.Invoke();
        }

        private IEnumerator SpawnAfterDelay(EnemySpawn entry, int waveIndex)
        {
            if (entry.spawnDelay > 0f) yield return new WaitForSeconds(entry.spawnDelay);

            try { Spawn(entry, waveIndex); }
            finally { _pendingSpawns--; }
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

            var health = go.GetComponentInChildren<Health>();
            if (health == null)
            {
                Debug.LogWarning($"[WaveManager] '{entry.enemyPrefab.name}' no tiene Health: no " +
                                 "cuenta para terminar la oleada.", this);
                return;
            }

            if (health.IsDead) return;
            _alive.Add(health);
            if (SectionClearTracker.Instance != null) SectionClearTracker.Instance.Register(health);
            AnyEnemySpawned?.Invoke(this, waveIndex, health);
        }

        private void OnAnyDied(Health health) => _alive.Remove(health);

        private int PruneAlive()
        {
            _prune.Clear();
            foreach (var h in _alive)
                if (h == null || h.IsDead) _prune.Add(h);
            foreach (var h in _prune) _alive.Remove(h);
            return _alive.Count;
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
