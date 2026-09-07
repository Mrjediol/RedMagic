using System;
using System.Collections.Generic;
using RedMagic.Economy;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Nivel de cada arma (1..3) y su compra con calaveras.
    ///
    /// El nivel se guarda por <b>referencia al asset</b>, no por nombre, así que renombrar una
    /// habilidad no pierde su nivel. Vive en memoria y se borra al terminar la run
    /// (<c>RunManager.RunEnded</c>): las mejoras del arma son progreso <b>de la partida</b>, como
    /// el oro, no meta-progresión — lo que se conserva entre runs son las mejoras del caldero.
    /// Si algún día se quiere que persistan, este es el único sitio que hay que tocar.
    ///
    /// Se auto-crea: nadie tiene que acordarse de ponerlo en una escena.
    /// </summary>
    [DisallowMultipleComponent]
    public class AbilityLevelManager : MonoBehaviour
    {
        public static AbilityLevelManager Instance { get; private set; }

        [Tooltip("Coste en calaveras de cada subida: el primero es de nivel 1 a 2, el segundo de " +
                 "2 a 3. Añadir un valor más aquí no basta para tener nivel 4: el tope lo marca " +
                 "AbilityDefinition.MaxLevel.")]
        [SerializeField] private int[] upgradeCosts = { 1, 3 };

        private readonly Dictionary<AbilityDefinition, int> _levels = new Dictionary<AbilityDefinition, int>();

        private bool _boundToRun;

        /// <summary>(habilidad, nivel nuevo). Lo escuchan el menú del altar y la UI.</summary>
        public event Action<AbilityDefinition, int> LevelChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[AbilityLevels]").AddComponent<AbilityLevelManager>();
        }

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

            // El Domain Reload está desactivado: los estáticos sobreviven entre partidas y hay que
            // limpiar a mano, como hacen los demás singletons del proyecto.
            _levels.Clear();
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            TryBindToRun();
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_boundToRun && Run.RunManager.Instance != null)
                Run.RunManager.Instance.RunEnded -= OnRunEnded;
            _boundToRun = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // El RunManager sólo existe una vez cargado el hub, así que se reintenta enganchar tras
        // cada carga de escena hasta conseguirlo (mismo patrón que CurrencyManager).
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                   UnityEngine.SceneManagement.LoadSceneMode mode) => TryBindToRun();

        private void TryBindToRun()
        {
            if (_boundToRun || Run.RunManager.Instance == null) return;

            Run.RunManager.Instance.RunEnded += OnRunEnded;
            _boundToRun = true;
        }

        private void OnRunEnded(bool completed) => ResetLevels();

        /// <summary>Nivel actual de una habilidad. Siempre 1 o más.</summary>
        public int GetLevel(AbilityDefinition ability)
        {
            if (ability == null) return 1;
            return _levels.TryGetValue(ability, out int level) ? level : 1;
        }

        public bool IsMaxed(AbilityDefinition ability) => GetLevel(ability) >= AbilityDefinition.MaxLevel;

        /// <summary>
        /// Calaveras que cuesta subir del nivel actual al siguiente. Devuelve -1 si ya está al
        /// máximo (o no hay habilidad), que es lo que mira el menú para desactivar el botón.
        /// </summary>
        public int CostToUpgrade(AbilityDefinition ability)
        {
            if (ability == null || IsMaxed(ability)) return -1;

            int index = GetLevel(ability) - 1;
            if (upgradeCosts == null || index < 0 || index >= upgradeCosts.Length) return -1;

            return Mathf.Max(0, upgradeCosts[index]);
        }

        /// <summary>
        /// Paga y sube un nivel. Devuelve false si está al máximo o no llegan las calaveras; en ese
        /// caso no se cobra nada.
        /// </summary>
        public bool TryUpgrade(AbilityDefinition ability)
        {
            int cost = CostToUpgrade(ability);
            if (cost < 0) return false;

            var wallet = CurrencyManager.Instance;
            if (wallet == null) return false;
            if (cost > 0 && !wallet.TrySpend(Currency.Skull, cost)) return false;

            int level = GetLevel(ability) + 1;
            _levels[ability] = level;
            LevelChanged?.Invoke(ability, level);

            return true;
        }

        /// <summary>Fuerza un nivel sin cobrar (pruebas, recompensas de guion).</summary>
        public void SetLevel(AbilityDefinition ability, int level)
        {
            if (ability == null) return;

            _levels[ability] = Mathf.Clamp(level, 1, AbilityDefinition.MaxLevel);
            LevelChanged?.Invoke(ability, _levels[ability]);
        }

        /// <summary>Todas las armas vuelven a nivel 1. Se llama al terminar la run.</summary>
        public void ResetLevels()
        {
            if (_levels.Count == 0) return;

            var previous = new List<AbilityDefinition>(_levels.Keys);
            _levels.Clear();

            foreach (var ability in previous) LevelChanged?.Invoke(ability, 1);
        }
    }
}
