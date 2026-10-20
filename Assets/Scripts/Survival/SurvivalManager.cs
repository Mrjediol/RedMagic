using System;
using System.Collections;
using System.Collections.Generic;
using RedMagic.Arsenal;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Economy;
using RedMagic.Enemies;
using RedMagic.Gameplay;
using RedMagic.Items;
using RedMagic.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Survival
{
    /// <summary>
    /// El modo Supervivencia en un mapa: rondas de enemigos que van a más, y entre rondas tienda y/o item que cae, hasta
    /// que el jugador muere. Todos los números salen de <see cref="config"/> (un <see cref="SurvivalConfig"/>, el mismo
    /// para cualquier mapa); el mapa sólo aporta los <see cref="SurvivalSpawnPoint"/> (hijos de este objeto, los recoge al
    /// empezar) y la tienda.
    ///
    /// Para llevarlo a otro mapa: arrastrar <c>Assets/Test/Survival/Survival.prefab</c> a la escena y mover sus puntos.
    ///
    /// Dos tipos de oleada (<see cref="SurvivalConfig.waveMode"/>):
    /// <list type="bullet">
    /// <item><b>Simples</b>: <see cref="SurvivalWaveBuilder.Build"/> decide la lista entera, salen de uno en uno (ritmo y
    /// tope de vivos) y la ronda acaba cuando no queda ninguno.</item>
    /// <item><b>Dinámicas</b>: se llena hasta el máximo a la vez y, cada vez que muere uno, sale otro a los
    /// <see cref="SurvivalConfig.respawnDelay"/> s. Acaban por tiempo o por bajas (<see cref="SurvivalConfig.dynamicEnd"/>),
    /// con el valor de cada oleada del mundo.</item>
    /// </list>
    /// La oleada del jefe es siempre simple: acaba al matarlo. Cada enemigo sale en un punto de su tipo dentro del radio del
    /// jugador, escalado por oleada × mundo × dificultad × especial y con el color de zona. Entre rondas: recompensas,
    /// curación, oro extra, y espera a "Siguiente ronda" (<see cref="RequestNextWave"/>) o a la cuenta atrás.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivalManager : MonoBehaviour
    {
        public enum Phase { Starting, Fighting, BetweenWaves, GameOver, Victory }

        public static SurvivalManager Current { get; private set; }

        [SerializeField] private SurvivalConfig config;

        [Header("Escena")]
        [Tooltip("Jugador que se crea si la escena no trae uno (etiqueta Player).")]
        [SerializeField] private GameObject playerPrefab;
        [Tooltip("Dónde empieza el jugador. Vacío = el SectionEntry de la escena, o este objeto.")]
        [SerializeField] private Transform playerSpawn;
        [Tooltip("La tienda entre rondas (un ShopManager con sus altares). Se esconde durante las rondas. Colócala donde quieras.")]
        [SerializeField] private ShopManager shop;
        [Tooltip("Dónde caen los items al acabar una ronda. Vacío = encima del jugador.")]
        [SerializeField] private Transform itemDropPoint;
        [Tooltip("A dónde lleva 'Menú principal' al morir.")]
        [SerializeField] private SceneReference mainMenuScene = new SceneReference();

        [Header("Pruebas")]
        [Tooltip("Empezar directamente en esta ronda (el escalado y la dificultad son los de esa ronda).")]
        [Min(1)] [SerializeField] private int startAtWave = 1;
        [Tooltip("Dificultad si la escena se abre suelta, sin pasar por el menú.")]
        [SerializeField] private SurvivalDifficulty testDifficulty = SurvivalDifficulty.Normal;
        [Tooltip("Semilla de las oleadas (0 = una distinta cada partida).")]
        [SerializeField] private int seed;

        public SurvivalConfig Config => config;
        public Phase CurrentPhase { get; private set; } = Phase.Starting;
        public SurvivalDifficulty Difficulty { get; private set; }
        public int Wave { get; private set; }
        public SurvivalStage Stage { get; private set; }
        public bool IsBossWave { get; private set; }
        /// <summary>La oleada en curso es dinámica (y no la del jefe).</summary>
        public bool IsDynamicWave { get; private set; }
        /// <summary>Simples: enemigos vivos + los que faltan por salir. Dinámicas: vivos.</summary>
        public int EnemiesLeft => _alive.Count + _pending.Count;
        /// <summary>Dinámica por tiempo: segundos que le quedan (0 = se acabó; hay que rematar a los que queden).</summary>
        public float TimeLeft { get; private set; }
        public int Kills { get; private set; }
        /// <summary>Dinámica por bajas: las que hay que conseguir.</summary>
        public int KillTarget { get; private set; }
        /// <summary>Segundos que faltan para que la siguiente ronda empiece sola (0 = espera al jugador).</summary>
        public float AutoNextRemaining { get; private set; }
        public GameObject Player { get; private set; }

        public event Action<int> WaveStarted;
        public event Action<int> WaveCleared;
        public event Action<int> GameOver;

        private SurvivalDifficultySettings _difficulty;
        private readonly List<SurvivalSpawnPoint> _points = new List<SurvivalSpawnPoint>();
        private readonly List<Health> _alive = new List<Health>();
        private readonly List<SurvivalSpawnRequest> _pending = new List<SurvivalSpawnRequest>();
        private readonly List<float> _scheduled = new List<float>();   // dinámicas: cuándo sale cada uno de los que vienen
        private readonly List<SurvivalSpawnPoint> _candidates = new List<SurvivalSpawnPoint>();
        private System.Random _rng;
        private Health _playerHealth;
        private bool _nextRequested;
        private int _waveInStage;
        private float _waveStart, _damageTakenThisWave;
        private readonly HashSet<SurvivalSpawnKind> _warnedKinds = new HashSet<SurvivalSpawnKind>();

        // ------------------------------------------------------------------ arranque

        private void Awake()
        {
            Current = this;
            // La tienda no se ve hasta acabar la primera ronda (y así tampoco se surte sola al cargar).
            if (shop != null) shop.gameObject.SetActive(false);
            DisableLevelFlow();
        }

        private void Start()
        {
            if (config == null) { Debug.LogError("[Survival] Falta el SurvivalConfig.", this); enabled = false; return; }

            Difficulty = SurvivalSession.Difficulty ?? testDifficulty;
            _difficulty = config.Settings(Difficulty);
            GetComponentsInChildren(false, _points);
            _rng = new System.Random(seed != 0 ? seed : Environment.TickCount);
            SetupPlayer();
            ResetRun();
            SurvivalHud.Ensure(this);
            StartCoroutine(Loop());
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_playerHealth != null) { _playerHealth.Died -= OnPlayerDied; _playerHealth.Damaged -= OnPlayerDamaged; }
            CurrencyDropModifiers.Remove(this);

            // Lo ganado aquí no viaja al modo clásico.
            var loadout = WeaponLoadout.Instance;
            if (loadout != null)
            {
                loadout.Inventory.Clear();
                loadout.Inventory.SetWeapon(null);
                loadout.Inventory.SetSecondWeapon(null);
            }
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Set(Currency.Gold, 0);
                CurrencyManager.Instance.Set(Currency.Diamond, 0);
            }
        }

        /// <summary>El mapa viene del editor de niveles con su flujo de sección (oleadas propias, salida): aquí no pinta.</summary>
        private void DisableLevelFlow()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var w in root.GetComponentsInChildren<WaveManager>(true)) w.enabled = false;
                foreach (var e in root.GetComponentsInChildren<SectionExit>(true)) e.gameObject.SetActive(false);
            }
        }

        private void SetupPlayer()
        {
            Player = RunManager.Instance != null ? RunManager.Instance.Player : null;
            if (Player == null) Player = GameObject.FindWithTag(Teams.PlayerTag);
            if (Player == null && playerPrefab != null) Player = Instantiate(playerPrefab);
            if (Player == null) { Debug.LogError("[Survival] No hay jugador ni 'Player Prefab'.", this); return; }

            Vector3 spawn = playerSpawn != null ? playerSpawn.position
                          : SectionEntry.FindIn(gameObject.scene) is SectionEntry entry ? entry.SpawnPosition
                          : transform.position;
            Player.transform.position = spawn;

            foreach (var follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))
            {
                follow.SetTarget(Player.transform);
                follow.RefreshBounds();
            }

            _playerHealth = Player.GetComponentInChildren<Health>();
            if (_playerHealth != null)
            {
                _playerHealth.ResetHealth();
                _playerHealth.Died += OnPlayerDied;
                _playerHealth.Damaged += OnPlayerDamaged;
            }
        }

        /// <summary>Partida limpia: oro, rerolls, inventario y armas de salida.</summary>
        private void ResetRun()
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Set(Currency.Gold, config.startingGold);
                CurrencyManager.Instance.Set(Currency.Diamond, 0);
            }
            RunRerolls.Set(ShopConfig.Instance.startingRerolls);
            CurrencyDropModifiers.Set(this, config.goldDropMultiplier - 1f);

            var loadout = WeaponLoadout.Instance;
            if (loadout == null) return;
            loadout.Inventory.Clear();
            loadout.Inventory.SetWeapon(config.startingWeapon);
            loadout.Inventory.SetSecondWeapon(config.startingSecondWeapon);
            foreach (var item in config.startingItems)
                if (item != null) loadout.Inventory.TryEquip(item);

            var dev = new List<WeaponDefinition>(config.devWeapons);
            if (config.startingWeapon != null && !dev.Contains(config.startingWeapon)) dev.Add(config.startingWeapon);
            if (config.startingSecondWeapon != null && !dev.Contains(config.startingSecondWeapon)) dev.Add(config.startingSecondWeapon);
            ArsenalCatalog.Register(dev);
        }

        // ------------------------------------------------------------------ bucle de rondas

        private IEnumerator Loop()
        {
            Wave = startAtWave - 1;
            while (true)
            {
                Wave++;
                Stage = config.StageFor(Wave, out _waveInStage, out bool boss);
                if (Stage == null) { CurrentPhase = Phase.Victory; GameOver?.Invoke(Wave - 1); yield break; }
                IsBossWave = boss;
                IsDynamicWave = !boss && config.waveMode == SurvivalWaveMode.Dynamic;

                CurrentPhase = Phase.Fighting;
                _alive.Clear();
                _pending.Clear();
                _scheduled.Clear();
                Kills = 0;
                KillTarget = 0;
                TimeLeft = 0f;
                _waveStart = Time.time;
                _damageTakenThisWave = 0f;
                WaveStarted?.Invoke(Wave);

                yield return IsDynamicWave ? RunDynamicWave() : RunSimpleWave();
                if (CurrentPhase == Phase.GameOver) yield break;

                Debug.Log($"[Survival] Ronda {Wave} ({Stage.fallbackName}{(IsBossWave ? ", jefe" : IsDynamicWave ? ", dinámica" : "")}, " +
                          $"{Difficulty}) superada en {Time.time - _waveStart:0.0} s · bajas {Kills} · daño recibido " +
                          $"{_damageTakenThisWave:0} · oro {(CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(Currency.Gold) : 0)}", this);

                yield return BetweenWaves();
                if (CurrentPhase == Phase.GameOver) yield break;
            }
        }

        /// <summary>Lista fija: salen de uno en uno con su ritmo y tope; acaba sin nadie vivo ni por salir.</summary>
        private IEnumerator RunSimpleWave()
        {
            _pending.AddRange(SurvivalWaveBuilder.Build(config, Wave, _rng, _difficulty.enemyMultiplier));
            int maxAlive = Mathf.Max(1, Mathf.RoundToInt(config.maxAlive * _difficulty.enemyMultiplier));

            yield return new WaitForSeconds(config.firstSpawnDelay);

            float nextSpawn = 0f;
            while (_pending.Count > 0 || _alive.Count > 0)
            {
                if (CurrentPhase == Phase.GameOver) yield break;
                Kills += _alive.RemoveAll(h => h == null || h.IsDead);

                if (_pending.Count > 0 && Time.time >= nextSpawn && _alive.Count < maxAlive)
                {
                    var request = _pending[0];
                    _pending.RemoveAt(0);
                    Spawn(request);
                    nextSpawn = Time.time + config.spawnInterval;
                }
                yield return null;
            }
        }

        /// <summary>
        /// Se llena hasta el máximo (escalonado) y cada baja pide otro a los <see cref="SurvivalConfig.respawnDelay"/> s,
        /// si hay hueco. Por tiempo: al acabarse deja de salir (o se van, según <see cref="SurvivalConfig.onTimeUp"/>). Por
        /// bajas: nunca salen más de los que faltan, y acaba al llegar a la cifra.
        /// </summary>
        private IEnumerator RunDynamicWave()
        {
            int max = Mathf.Max(1, Mathf.RoundToInt(config.dynamicMaxAlive.At(Wave, Stage.countMultiplier) * _difficulty.enemyMultiplier));
            bool byTime = config.dynamicEnd == SurvivalWaveEnd.Time;
            float duration = config.WaveSeconds(Stage, _waveInStage);
            KillTarget = byTime ? 0 : Mathf.Max(1, Mathf.RoundToInt(config.WaveKills(Stage, _waveInStage) * _difficulty.killsMultiplier));
            float endsAt = Time.time + config.firstSpawnDelay + duration;
            int spawned = 0;

            for (int i = 0; i < max; i++) _scheduled.Add(Time.time + config.firstSpawnDelay + i * config.spawnInterval);

            while (true)
            {
                if (CurrentPhase == Phase.GameOver) yield break;
                float now = Time.time;
                Kills += _alive.RemoveAll(h => h == null || h.IsDead);

                bool timeUp = byTime && now >= endsAt;
                TimeLeft = byTime ? Mathf.Max(0f, endsAt - now) : 0f;
                if (timeUp && config.onTimeUp == SurvivalTimeUp.RemoveRemaining) { RemoveAlive(); yield break; }
                if (!byTime && Kills >= KillTarget) yield break;

                bool canSpawn = !timeUp;
                if (canSpawn)
                {
                    // Lo que puede haber fuera a la vez: el máximo, y por bajas nunca más de las que faltan.
                    int outstanding = byTime ? max : Mathf.Min(max, KillTarget - spawned + _alive.Count);
                    while (_alive.Count + _scheduled.Count < outstanding) _scheduled.Add(now + config.respawnDelay);
                    while (_alive.Count + _scheduled.Count > outstanding && _scheduled.Count > 0) _scheduled.RemoveAt(_scheduled.Count - 1);

                    _scheduled.Sort();
                    if (_scheduled.Count > 0 && _scheduled[0] <= now && _alive.Count < max)
                    {
                        _scheduled.RemoveAt(0);
                        if (SurvivalWaveBuilder.PickOne(config, Wave, Stage, _rng, out var request) && Spawn(request)) spawned++;
                    }
                }
                else
                {
                    _scheduled.Clear();
                    if (_alive.Count == 0) yield break;
                }
                yield return null;
            }
        }

        /// <summary>Se acabó el tiempo y se van: sin morir, así que tampoco sueltan botín.</summary>
        private void RemoveAlive()
        {
            foreach (var h in _alive) if (h != null) Destroy(h.gameObject);
            _alive.Clear();
        }

        private IEnumerator BetweenWaves()
        {
            CurrentPhase = Phase.BetweenWaves;
            _nextRequested = false;

            if (_playerHealth != null && config.healBetweenWaves > 0f)
                _playerHealth.Heal(_playerHealth.MaxHealth * config.healBetweenWaves);
            if (config.goldPerWaveCleared > 0 && CurrencyManager.Instance != null)
                CurrencyManager.Instance.Add(Currency.Gold, config.goldPerWaveCleared);

            var rewards = config.RewardsFor(Stage);
            if ((rewards & SurvivalReward.Shop) != 0 && shop != null)
            {
                shop.gameObject.SetActive(true);
                shop.Stock(Stage.worldNumber, _rng.Next());
            }
            if ((rewards & SurvivalReward.ItemDrop) != 0) DropItems();

            WaveCleared?.Invoke(Wave);

            AutoNextRemaining = config.autoNextWaveSeconds;
            while (!_nextRequested && CurrentPhase != Phase.GameOver)
            {
                if (config.autoNextWaveSeconds > 0f)
                {
                    AutoNextRemaining -= Time.deltaTime;
                    if (AutoNextRemaining <= 0f) break;
                }
                yield return null;
            }
            AutoNextRemaining = 0f;

            if (shop != null) shop.gameObject.SetActive(false);
        }

        /// <summary>"Siguiente ronda": sólo cuenta entre rondas.</summary>
        public void RequestNextWave()
        {
            if (CurrentPhase == Phase.BetweenWaves) _nextRequested = true;
        }

        // ------------------------------------------------------------------ aparición

        /// <returns>False si no había dónde (o qué) sacarlo.</returns>
        private bool Spawn(SurvivalSpawnRequest request)
        {
            var point = PickPoint(request.kind);
            if (point == null || request.prefab == null) return false;

            var enemy = Instantiate(request.prefab, point.position, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(enemy, gameObject.scene);

            // Color: el de la especial manda sobre el de zona (una especial tiene que reconocerse en cualquier mundo).
            var shader = config.variantShader;
            bool specialLook = request.special != null && request.special.recolor;
            if (Stage.zoneLook != null && !specialLook) Stage.zoneLook.ApplyLook(enemy, shader);
            EnemyVariant.ApplyStats(enemy, config.HealthScale(Wave) * Stage.healthMultiplier * _difficulty.healthMultiplier,
                                    config.DamageScale(Wave) * Stage.damageMultiplier * _difficulty.damageMultiplier);
            if (request.special != null) request.special.Apply(enemy, shader);

            var health = enemy.GetComponent<Health>();
            if (health != null) _alive.Add(health);
            return true;
        }

        /// <summary>
        /// Un punto del tipo pedido (jefe / torreta sin punto propio: uno de suelo) entre <see cref="SurvivalConfig.minSpawnDistance"/>
        /// y <see cref="SurvivalConfig.spawnRadius"/> del jugador; si dentro del radio sólo hay alguno demasiado cerca, ese;
        /// si no hay ninguno dentro, el más cercano. El jefe no mira el radio.
        /// </summary>
        private Transform PickPoint(SurvivalSpawnKind kind)
        {
            Collect(kind);
            if (_candidates.Count == 0 && (kind == SurvivalSpawnKind.Boss || kind == SurvivalSpawnKind.Turret))
                Collect(SurvivalSpawnKind.Ground);
            if (_candidates.Count == 0)
            {
                if (_warnedKinds.Add(kind))
                    Debug.LogWarning($"[Survival] No hay ningún SurvivalSpawnPoint de tipo {kind}: esos enemigos no salen.", this);
                return null;
            }

            if (kind != SurvivalSpawnKind.Boss && config.spawnRadius > 0f && Player != null)
            {
                Vector2 p = Player.transform.position;
                float r2 = config.spawnRadius * config.spawnRadius, min2 = config.minSpawnDistance * config.minSpawnDistance;
                SurvivalSpawnPoint nearest = null;
                float best = float.MaxValue;
                int inRadius = 0, inRing = 0;
                foreach (var s in _candidates)
                {
                    float d = ((Vector2)s.transform.position - p).sqrMagnitude;
                    if (d < best) { best = d; nearest = s; }
                    if (d <= r2) { inRadius++; if (d >= min2) inRing++; }
                }
                if (inRadius == 0) return nearest.transform;
                bool ring = inRing > 0;
                _candidates.RemoveAll(s =>
                {
                    float d = ((Vector2)s.transform.position - p).sqrMagnitude;
                    return d > r2 || (ring && d < min2);
                });
            }
            return _candidates[_rng.Next(_candidates.Count)].transform;
        }

        private void Collect(SurvivalSpawnKind kind)
        {
            _candidates.Clear();
            foreach (var s in _points)
                if (s != null && s.isActiveAndEnabled && s.kind == kind) _candidates.Add(s);
        }

        private void DropItems()
        {
            var stock = ShopConfig.Instance.RollStock(config.itemDrops, Stage.worldNumber, _rng);
            Vector3 origin = itemDropPoint != null ? itemDropPoint.position
                           : Player != null ? Player.transform.position + Vector3.up * 2f : transform.position;
            for (int i = 0; i < stock.Count; i++)
            {
                float x = (i - (stock.Count - 1) * 0.5f) * 1.6f;
                ItemPickup.Spawn(stock[i].Item, origin + new Vector3(x, 0f, 0f));
            }
        }

        // ------------------------------------------------------------------ jugador

        private void OnPlayerDamaged(float amount) => _damageTakenThisWave += amount;

        private void OnPlayerDied()
        {
            if (CurrentPhase == Phase.GameOver) return;
            CurrentPhase = Phase.GameOver;
            if (shop != null) shop.gameObject.SetActive(false);
            Debug.Log($"[Survival] Muerto en la ronda {Wave}.", this);
            GameOver?.Invoke(Wave);
        }

        /// <summary>Vuelve a empezar la partida (recarga el mapa, misma dificultad).</summary>
        public void Retry()
        {
            GameStateManager.Instance?.ForceResume();
            SceneManager.LoadScene(gameObject.scene.path);
        }

        /// <summary>Al menú principal.</summary>
        public void ToMainMenu()
        {
            string path = mainMenuScene.ResolveForLoad(this);
            if (path == null) return;
            GameStateManager.Instance?.ForceResume();
            SceneManager.LoadScene(path);
        }
    }
}
