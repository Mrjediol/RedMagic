using System;
using RedMagic.Audio;
using RedMagic.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Economy
{
    /// <summary>
    /// Cartera del jugador. Singleton persistente (DontDestroyOnLoad). Se coloca a mano en las
    /// escenas que pueden ser la primera (MainMenu, MainHub) — igual que <c>AudioManager</c> y
    /// <c>RunManager</c> — para poder ver y tocar las cantidades en el Inspector. Si no hay ninguno
    /// en la escena de arranque (p. ej. al probar una sección suelta) se crea uno de respaldo.
    ///
    /// Los cuatro campos de cantidad (<see cref="gold"/>…<see cref="skull"/>) reflejan el estado
    /// real y son <b>editables en el Inspector también durante Play</b>: escribe un número y se
    /// aplica al momento (el HUD se actualiza, y la moneda de meta se guarda). Sirve para probar
    /// cosas como el menú de mejoras sin farmear.
    ///
    /// Reglas de las monedas:
    ///  - <b>Oro y diamante</b> (moneda de run): a cero al terminar una run
    ///    (<see cref="RunManager.RunEnded"/>), se muera o se complete. No se persisten.
    ///  - <b>Fragmento de alma y calavera</b> (moneda de meta): se guardan en PlayerPrefs y
    ///    sobreviven a la muerte y al cierre del juego.
    /// </summary>
    [DisallowMultipleComponent]
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        private const string ConfigResourcePath = "CurrencyConfig";
        private const string PrefPrefix = "currency.";

        [Header("Cantidades — editables en el Inspector, también en Play para probar")]
        [Tooltip("En edición: cantidad de arranque (el oro es moneda de run, normalmente 0). " +
                 "En Play: escribe aquí y se aplica al instante.")]
        [SerializeField, Min(0)] private int gold;

        [SerializeField, Min(0)] private int diamond;

        [Tooltip("Fragmentos de alma. En edición: valor de primera partida (después manda lo " +
                 "guardado en PlayerPrefs). En Play: se aplica y se guarda al escribirlo.")]
        [SerializeField, Min(0)] private int soulFragment;

        [SerializeField, Min(0)] private int skull;

        /// <summary>(moneda, cantidad nueva). Se dispara con cualquier cambio, incluido el reset de run.</summary>
        public event Action<Currency, int> Changed;

        private readonly int[] _amounts = new int[4];
        private CurrencyConfig _config;
        private bool _boundToRun;
        private bool _ready;

        /// <summary>
        /// True si la moneda se pierde al volver al hub. Oro y diamante sí; fragmento de alma y
        /// calavera no. Es la única definición de esa clasificación en todo el código.
        /// </summary>
        public static bool IsRunCurrency(Currency currency) =>
            currency == Currency.Gold || currency == Currency.Diamond;

        /// <summary>Config cargada de Resources. Puede ser null si falta el asset (se avisa una vez).</summary>
        public CurrencyConfig Config => _config;

        // Si nadie lo puso en la escena de arranque, se crea un respaldo sin campos de Inspector
        // (AfterSceneLoad corre tras el Awake de los objetos de escena, así que uno colocado a mano
        // ya habrá reclamado Instance).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            new GameObject("[CurrencyManager]").AddComponent<CurrencyManager>();
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

            _config = Resources.Load<CurrencyConfig>(ConfigResourcePath);
            if (_config == null)
            {
                Debug.LogWarning($"[CurrencyManager] No hay '{ConfigResourcePath}' en Resources: los " +
                                 "enemigos no soltarán nada y el HUD saldrá sin iconos. Crea el asset " +
                                 "con Assets > Create > RedMagic > Currency Config.", this);
            }

            // Oro/diamante: valor del Inspector (para pruebas; normalmente 0). Alma/calavera: lo
            // guardado, usando el valor del Inspector como defecto de primera partida.
            foreach (Currency currency in Enum.GetValues(typeof(Currency)))
            {
                int inspectorValue = Mathf.Max(0, Field(currency));
                _amounts[(int)currency] = IsRunCurrency(currency)
                    ? inspectorValue
                    : PlayerPrefs.GetInt(PrefPrefix + currency, inspectorValue);
            }

            MirrorToFields();
            _ready = true;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryBindToRun();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_boundToRun && RunManager.Instance != null)
                RunManager.Instance.RunEnded -= OnRunEnded;
            _boundToRun = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Editar un campo en el Inspector durante Play lo aplica al estado real. Fuera de Play sólo
        // recorta negativos.
        private void OnValidate()
        {
            gold = Mathf.Max(0, gold);
            diamond = Mathf.Max(0, diamond);
            soulFragment = Mathf.Max(0, soulFragment);
            skull = Mathf.Max(0, skull);

            if (!Application.isPlaying || !_ready || Instance != this) return;

            // Se capturan los cuatro valores antes de aplicar ninguno: SetAmount llama a
            // MirrorToFields, que reescribe los otros campos y borraría las ediciones pendientes
            // a medio bucle.
            int wantGold = gold, wantDiamond = diamond, wantSoul = soulFragment, wantSkull = skull;
            ApplyFieldEdit(Currency.Gold, wantGold);
            ApplyFieldEdit(Currency.Diamond, wantDiamond);
            ApplyFieldEdit(Currency.SoulFragment, wantSoul);
            ApplyFieldEdit(Currency.Skull, wantSkull);
        }

        private void ApplyFieldEdit(Currency currency, int wanted)
        {
            if (wanted != _amounts[(int)currency]) SetAmount(currency, wanted);
        }

        // RunManager sólo existe una vez cargado el hub; se intenta enganchar tras cada carga de
        // escena hasta conseguirlo.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryBindToRun();

        private void TryBindToRun()
        {
            if (_boundToRun || RunManager.Instance == null) return;
            RunManager.Instance.RunEnded += OnRunEnded;
            _boundToRun = true;
        }

        private void OnRunEnded(bool completed)
        {
            // La moneda de run no llega al hub: ni ganada de más al morir, ni "banqueada" al
            // completar. Cuando exista un uso fuera de la run bastará con mirar 'completed' aquí.
            foreach (Currency currency in Enum.GetValues(typeof(Currency)))
                if (IsRunCurrency(currency)) SetAmount(currency, 0);
        }

        // ------------------------------------------------------------------ consulta / cambio

        public int Get(Currency currency) => _amounts[(int)currency];

        /// <summary>Suma <paramref name="amount"/> (ignora &lt;= 0). Persiste si es moneda de meta.</summary>
        public void Add(Currency currency, int amount)
        {
            if (amount <= 0) return;
            SetAmount(currency, _amounts[(int)currency] + amount);
            PlayGained(currency);
        }

        /// <summary>
        /// Gasta <paramref name="amount"/> si hay suficiente. Devuelve false y no toca nada si no.
        /// </summary>
        public bool TrySpend(Currency currency, int amount)
        {
            if (amount <= 0 || _amounts[(int)currency] < amount) return false;
            SetAmount(currency, _amounts[(int)currency] - amount);
            return true;
        }

        /// <summary>Fija directamente la cantidad (debug / consola / el Inspector).</summary>
        public void Set(Currency currency, int value) => SetAmount(currency, value);

        /// <summary>
        /// Da el botín de un enemigo de <paramref name="tier"/>: tira el rango de cada moneda de
        /// la fila correspondiente de <see cref="CurrencyConfig"/> y lo suma. Lo llama
        /// <see cref="CurrencyDropper"/> desde el evento <c>Died</c> del enemigo.
        /// </summary>
        /// <param name="multiplier">Multiplicador propio de esta muerte (la marca de oro); se combina con
        /// los bonos globales de <see cref="CurrencyDropModifiers"/>.</param>
        public void GrantDrops(EnemyTier tier, float multiplier = 1f)
        {
            if (_config == null) return;

            float scale = Mathf.Max(0f, multiplier) * CurrencyDropModifiers.Multiplier;
            var drops = _config.DropsFor(tier);
            Add(Currency.Gold, Scaled(drops.gold.Roll(), scale));
            Add(Currency.Diamond, Scaled(drops.diamond.Roll(), scale));
            Add(Currency.SoulFragment, Scaled(drops.soulFragment.Roll(), scale));
            Add(Currency.Skull, Scaled(drops.skull.Roll(), scale));
        }

        private static void PlayGained(Currency currency)
        {
            switch (currency)
            {
                case Currency.Gold: SystemSounds.Play(s => s.goldGained); break;
                case Currency.Diamond: SystemSounds.Play(s => s.diamondGained); break;
                case Currency.SoulFragment: SystemSounds.Play(s => s.soulFragmentGained); break;
                case Currency.Skull: SystemSounds.Play(s => s.skullGained); break;
            }
        }

        private static int Scaled(int amount, float scale) => scale == 1f ? amount : Mathf.RoundToInt(amount * scale);

        [ContextMenu("Borrar moneda de meta guardada (PlayerPrefs)")]
        private void ClearSavedMetaCurrency()
        {
            foreach (Currency currency in Enum.GetValues(typeof(Currency)))
            {
                if (IsRunCurrency(currency)) continue;
                PlayerPrefs.DeleteKey(PrefPrefix + currency);
                if (Application.isPlaying) SetAmount(currency, 0);
            }
            PlayerPrefs.Save();
            Debug.Log("[CurrencyManager] Moneda de meta guardada borrada.", this);
        }

        // ------------------------------------------------------------------ estado / persistencia

        // Único punto que muta _amounts: recorta a >= 0, refleja en los campos del Inspector,
        // guarda si es moneda de meta y avisa. No hace nada si el valor no cambia.
        private void SetAmount(Currency currency, int value)
        {
            value = Mathf.Max(0, value);
            if (_amounts[(int)currency] == value)
            {
                MirrorToFields();
                return;
            }

            _amounts[(int)currency] = value;
            MirrorToFields();

            if (!IsRunCurrency(currency)) SavePersistent(currency);
            Changed?.Invoke(currency, value);
        }

        private void MirrorToFields()
        {
            gold = _amounts[(int)Currency.Gold];
            diamond = _amounts[(int)Currency.Diamond];
            soulFragment = _amounts[(int)Currency.SoulFragment];
            skull = _amounts[(int)Currency.Skull];
        }

        private int Field(Currency currency) => currency switch
        {
            Currency.Gold => gold,
            Currency.Diamond => diamond,
            Currency.SoulFragment => soulFragment,
            Currency.Skull => skull,
            _ => 0
        };

        private void SavePersistent(Currency currency)
        {
            PlayerPrefs.SetInt(PrefPrefix + currency, _amounts[(int)currency]);
            PlayerPrefs.Save();
        }
    }
}
