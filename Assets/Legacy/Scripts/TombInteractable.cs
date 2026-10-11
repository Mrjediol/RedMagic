using RedMagic.Audio;
using RedMagic.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Run
{
    /// <summary>
    /// La tumba del hub: el jugador se acerca, pulsa interactuar y empieza la run de ese mundo.
    /// Se coloca en MainHub, una por mundo, sobre un collider marcado como <c>Is Trigger</c>.
    ///
    /// La tumba referencia el <see cref="WorldDefinition"/> directamente en vez de guardar un
    /// índice o el nombre del mundo: reordenar la lista de mundos del <see cref="RunManager"/> o
    /// renombrar el asset no puede desemparejar la tumba de su mundo.
    ///
    /// Se considera bloqueada si <see cref="lockedOverride"/> está activo o si el mundo no está
    /// desbloqueado. Las tumbas de los mundos 2..5 se dejan bloqueadas de momento.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class TombInteractable : MonoBehaviour, ISoundEventSource
    {
        public event System.Action<SoundTrigger> SoundTriggered;

        public void DeclareSoundTriggers(System.Collections.Generic.List<SoundTrigger> into) { into.Add(SoundTrigger.OnInteract); }

        [Header("Mundo")]
        [Tooltip("Mundo al que lleva esta tumba.")]
        [SerializeField] private WorldDefinition world;

        [Tooltip("Fuerza el estado bloqueado aunque el mundo esté desbloqueado. Para las tumbas " +
                 "que todavía son decorado.")]
        [SerializeField] private bool lockedOverride;

        [Header("Detección")]
        [Tooltip("Etiqueta del objeto que puede usar la tumba.")]
        [SerializeField] private string playerTag = "Player";

        [Header("Input")]
        [Tooltip("Asset de acciones. Si se deja vacío se usan las teclas de reserva.")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [Tooltip("Acción de interactuar. Si no existe en el mapa, se usan las teclas de reserva.")]
        [SerializeField] private string actionName = "Interact";

        [Header("Aviso en pantalla")]
        [Tooltip("Objeto que se enciende cuando el jugador está dentro del alcance (un cartel de " +
                 "'Pulsa E'). Opcional.")]
        [SerializeField] private GameObject prompt;

        [Tooltip("Objeto que se enciende si la tumba está bloqueada. Opcional.")]
        [SerializeField] private GameObject lockedPrompt;

        private InputAction _interactAction;
        private bool _playerInRange;

        /// <summary>True si la tumba no se puede usar todavía.</summary>
        public bool IsLocked => lockedOverride || world == null || !world.Unlocked;

        /// <summary>Mundo al que lleva esta tumba (null si está sin configurar).</summary>
        public WorldDefinition World => world;

        private void Reset()
        {
            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.isTrigger = true;
        }

        private void Awake()
        {
            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                _interactAction = map?.FindAction(actionName, throwIfNotFound: false);
            }

            SetPromptsVisible(false);
        }

        private void OnEnable() => _interactAction?.Enable();

        private void OnDisable()
        {
            _interactAction?.Disable();
            _playerInRange = false;
            SetPromptsVisible(false);
        }

        private void Update()
        {
            if (!_playerInRange) return;

            // Durante una transición de escena o con un menú abierto no se interactúa.
            if (!GameStateManager.CanPlayerAct) return;

            if (InteractPressed()) Interact();
        }

        private bool InteractPressed()
        {
            if (_interactAction != null && _interactAction.WasPressedThisFrame()) return true;

            // Reserva para cuando el asset de input todavía no tiene acción de interactuar:
            // E o Enter en teclado, botón norte del mando, y la cola táctil.
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)
                return true;

            return TouchInput.ConsumeInteract();
        }

        /// <summary>
        /// Entra a la run de este mundo. Es público para poder llamarlo también desde un botón de
        /// UI táctil o desde una cinemática.
        /// </summary>
        public void Interact()
        {
            if (RunManager.Instance == null)
            {
                Debug.LogError("[TombInteractable] No hay RunManager en la escena. Añádelo a MainHub.", this);
                return;
            }

            if (world == null)
            {
                Debug.LogError("[TombInteractable] Esta tumba no tiene mundo asignado.", this);
                return;
            }

            if (IsLocked)
            {
                Debug.Log($"[TombInteractable] '{world.DisplayName}' todavía está bloqueado.", this);
                return;
            }

            SoundTriggered?.Invoke(SoundTrigger.OnInteract);

            SetPromptsVisible(false);
            RunManager.Instance.StartRun(world);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = true;
            SetPromptsVisible(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = false;
            SetPromptsVisible(false);
        }

        private void SetPromptsVisible(bool visible)
        {
            bool locked = IsLocked;

            if (prompt != null) prompt.SetActive(visible && !locked);
            if (lockedPrompt != null) lockedPrompt.SetActive(visible && locked);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsLocked ? new Color(1f, 0.3f, 0.3f, 0.5f) : new Color(0.4f, 0.9f, 1f, 0.6f);

            var collider = GetComponent<Collider2D>();
            if (collider != null) Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
        }
    }
}
