using System;
using UnityEngine;

namespace RedMagic.Core
{
    /// <summary>
    /// Estado global de juego. Singleton persistente (DontDestroyOnLoad).
    /// El juego queda en pausa mientras haya al menos un menú bloqueante abierto:
    /// congela el tiempo (Time.timeScale = 0) y bloquea la acción del jugador.
    /// El audio NO se toca (AudioListener.pause se deja en false), así música y SFX de UI
    /// siguen sonando durante la pausa.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        /// <summary>True mientras haya algún menú bloqueante abierto.</summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Los scripts de gameplay consultan esto antes de leer input o actuar.
        /// Es estático para que un controlador de jugador pueda comprobarlo sin referencia al singleton.
        /// </summary>
        public static bool CanPlayerAct { get; private set; } = true;

        /// <summary>Se dispara al cambiar el estado de pausa. El argumento es el nuevo valor de IsPaused.</summary>
        public event Action<bool> PausedChanged;

        [Tooltip("Componentes de control del jugador (PlayerInput, controladores por Update...) que se " +
                 "desactivan mientras el juego está en pausa. Opcional: CanPlayerAct es el mecanismo principal.")]
        [SerializeField] private Behaviour[] playerInputBehaviours;

        // Nº de menús bloqueantes abiertos. Permite abrir Opciones encima de otro menú
        // sin que al cerrarlo se despause el juego.
        private int _openMenuCount;

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

            // Con "Reload Domain" desactivado los estáticos sobreviven entre sesiones de Play:
            // reseteamos a un estado conocido.
            _openMenuCount = 0;
            Apply(false, force: true);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            Instance = null;
            // No dejar el juego congelado si el manager desaparece.
            Time.timeScale = 1f;
            CanPlayerAct = true;
        }

        /// <summary>
        /// Registra la apertura (paused = true) o el cierre (paused = false) de un menú bloqueante.
        /// El juego permanece en pausa mientras el contador sea mayor que cero.
        /// </summary>
        public void SetPaused(bool paused)
        {
            _openMenuCount = Mathf.Max(0, _openMenuCount + (paused ? 1 : -1));
            Apply(_openMenuCount > 0);
        }

        /// <summary>Despausa de forma incondicional y pone el contador a cero (p. ej. al entrar a gameplay).</summary>
        public void ForceResume()
        {
            _openMenuCount = 0;
            Apply(false);
        }

        private void Apply(bool paused, bool force = false)
        {
            if (!force && paused == IsPaused)
                return;

            IsPaused = paused;
            CanPlayerAct = !paused;
            Time.timeScale = paused ? 0f : 1f;

            SetInputEnabled(!paused);
            PausedChanged?.Invoke(paused);
        }

        private void SetInputEnabled(bool value)
        {
            if (playerInputBehaviours == null) return;

            foreach (var behaviour in playerInputBehaviours)
                if (behaviour != null) behaviour.enabled = value;
        }
    }
}
