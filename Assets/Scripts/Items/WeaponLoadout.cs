using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Items
{
    /// <summary>
    /// Dueño en escena del <see cref="WeaponInventory"/> de la run y de su
    /// <see cref="SynergyTracker"/>. Es el punto de acceso único para la UI y (más adelante) para
    /// tienda, cofres y efectos de umbral.
    ///
    /// Se auto-crea y es <c>DontDestroyOnLoad</c>, como <c>AbilityLevelManager</c>. Los items son
    /// progreso <b>de la partida</b> (como el oro y el nivel del arma), así que al terminar la run
    /// (<c>RunManager.RunEnded</c>) se vacía todo: los 9 huecos y el arma.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponLoadout : MonoBehaviour
    {
        public static WeaponLoadout Instance { get; private set; }

        public WeaponInventory Inventory { get; private set; }
        public SynergyTracker Synergy { get; private set; }

        /// <summary>Aplica los efectos de lo equipado (ver <see cref="ItemEffect"/>).</summary>
        public ItemEffectRunner Effects { get; private set; }

        private bool _boundToRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[WeaponLoadout]").AddComponent<WeaponLoadout>();
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

            // Domain Reload desactivado: se reconstruye a mano en cada Awake, como los demás
            // singletons del proyecto.
            Synergy?.Dispose();
            Effects?.Dispose();
            Inventory = new WeaponInventory();
            Synergy = new SynergyTracker(Inventory);
            Effects = new ItemEffectRunner(Inventory);
        }

        private void Update() => Effects?.Tick(Time.deltaTime);

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryBindToRun();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_boundToRun && Run.RunManager.Instance != null)
                Run.RunManager.Instance.RunEnded -= OnRunEnded;
            _boundToRun = false;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Synergy?.Dispose();
            Effects?.Dispose();
            Instance = null;
        }

        // El RunManager sólo existe una vez cargado el hub; se reintenta tras cada carga de escena
        // hasta engancharlo (mismo patrón que AbilityLevelManager).
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryBindToRun();

        private void TryBindToRun()
        {
            if (_boundToRun || Run.RunManager.Instance == null) return;

            Run.RunManager.Instance.RunEnded += OnRunEnded;
            _boundToRun = true;
        }

        private void OnRunEnded(bool completed)
        {
            Inventory.Clear();
            Inventory.SetWeapon(null);
        }
    }
}
