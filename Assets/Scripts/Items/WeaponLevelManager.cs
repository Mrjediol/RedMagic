using System;
using System.Collections.Generic;
using RedMagic.Economy;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Nivel de cada arma (1..3) y su compra con calaveras, para el sistema de armas/items.
    /// Reemplaza al viejo <c>AbilityLevelManager</c> (ver <c>Assets/Scripts/Legacy/WeaponLevels/</c>),
    /// que subía de nivel una <c>AbilityDefinition</c> — con el sistema de items equipado, el altar
    /// nunca encontraba nada que subir.
    ///
    /// El nivel se guarda por <b>referencia al asset</b> <see cref="WeaponDefinition"/>, no por
    /// nombre, así que renombrar un arma no pierde su nivel. Vive en memoria y se borra al terminar
    /// la run (<c>RunManager.RunEnded</c>): las mejoras del arma son progreso <b>de la partida</b>,
    /// como el oro, no meta-progresión.
    ///
    /// <b>Placeholder de nivel</b>: hasta decidir cómo cambian de verdad el daño/tamaño/cooldown en
    /// nivel 2 y 3, lo único que hace subir de nivel es teñir el disparo — negro en nivel 2, dorado
    /// en nivel 3 (<see cref="TryGetLevelTint"/>) — sólo para comprobar que el sistema funciona.
    /// <see cref="ShotResolver"/> lo aplica al final del pipeline, después del Elemento.
    ///
    /// Se auto-crea: nadie tiene que acordarse de ponerlo en una escena.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponLevelManager : MonoBehaviour
    {
        public static WeaponLevelManager Instance { get; private set; }

        public const int MaxLevel = 3;

        public static readonly Color Level2Tint = Color.black;
        public static readonly Color Level3Tint = new Color(1f, 0.84f, 0f);

        [Tooltip("Coste en calaveras de cada subida: el primero es de nivel 1 a 2, el segundo de " +
                 "2 a 3. Añadir un valor más aquí no basta para tener nivel 4: el tope lo marca " +
                 "MaxLevel.")]
        [SerializeField] private int[] upgradeCosts = { 1, 3 };

        private readonly Dictionary<WeaponDefinition, int> _levels = new Dictionary<WeaponDefinition, int>();

        private bool _boundToRun;

        /// <summary>(arma, nivel nuevo). Lo escuchan el altar y la UI.</summary>
        public event Action<WeaponDefinition, int> LevelChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[WeaponLevels]").AddComponent<WeaponLevelManager>();
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

        /// <summary>Nivel actual de un arma. Siempre 1 o más.</summary>
        public int GetLevel(WeaponDefinition weapon)
        {
            if (weapon == null) return 1;
            return _levels.TryGetValue(weapon, out int level) ? level : 1;
        }

        public bool IsMaxed(WeaponDefinition weapon) => GetLevel(weapon) >= MaxLevel;

        /// <summary>
        /// Calaveras que cuesta subir del nivel actual al siguiente. Devuelve -1 si ya está al
        /// máximo (o no hay arma), que es lo que mira el menú para desactivar el botón.
        /// </summary>
        public int CostToUpgrade(WeaponDefinition weapon)
        {
            if (weapon == null || IsMaxed(weapon)) return -1;

            int index = GetLevel(weapon) - 1;
            if (upgradeCosts == null || index < 0 || index >= upgradeCosts.Length) return -1;

            return Mathf.Max(0, upgradeCosts[index]);
        }

        /// <summary>
        /// Paga y sube un nivel. Devuelve false si está al máximo o no llegan las calaveras; en ese
        /// caso no se cobra nada.
        /// </summary>
        public bool TryUpgrade(WeaponDefinition weapon)
        {
            int cost = CostToUpgrade(weapon);
            if (cost < 0) return false;

            var wallet = CurrencyManager.Instance;
            if (wallet == null) return false;
            if (cost > 0 && !wallet.TrySpend(Currency.Skull, cost)) return false;

            int level = GetLevel(weapon) + 1;
            _levels[weapon] = level;
            LevelChanged?.Invoke(weapon, level);

            return true;
        }

        /// <summary>Fuerza un nivel sin cobrar (pruebas, recompensas de guion).</summary>
        public void SetLevel(WeaponDefinition weapon, int level)
        {
            if (weapon == null) return;

            _levels[weapon] = Mathf.Clamp(level, 1, MaxLevel);
            LevelChanged?.Invoke(weapon, _levels[weapon]);
        }

        /// <summary>Todas las armas vuelven a nivel 1. Se llama al terminar la run.</summary>
        public void ResetLevels()
        {
            if (_levels.Count == 0) return;

            var previous = new List<WeaponDefinition>(_levels.Keys);
            _levels.Clear();

            foreach (var weapon in previous) LevelChanged?.Invoke(weapon, 1);
        }

        /// <summary>
        /// Tinte placeholder del nivel: sin override en nivel 1 (el disparo se queda con su tinte
        /// resuelto — elemento o acento del arma), negro en 2, dorado en 3.
        /// </summary>
        public static bool TryGetLevelTint(int level, out Color tint)
        {
            if (level >= 3) { tint = Level3Tint; return true; }
            if (level == 2) { tint = Level2Tint; return true; }

            tint = default;
            return false;
        }
    }
}
