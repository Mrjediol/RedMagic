using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Gameplay;
using RedMagic.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Hub
{
    /// <summary>
    /// El espejo del hub: un solo toque, reproduce su animación y abre
    /// <see cref="MirrorMenuController"/> (todavía un cascarón vacío — ver ese archivo). Misma
    /// mecánica de detección/input que <c>CauldronInteractable</c>/<see cref="AnvilInteractable"/>.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class MirrorInteractable : MonoBehaviour
    {
        [Header("Detección")]
        [SerializeField] private string playerTag = "Player";

        [Header("Input")]
        [Tooltip("Asset de acciones. Vacío = se usan las teclas de reserva (E / Enter / botón norte).")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Interact";

        [Header("Animación")]
        [Tooltip("Animator del espejo. Vacío = se busca en este objeto y sus hijos. El brillo en " +
                 "bucle es cosa del propio Animator/Flipbook del prefab; esto sólo dispara un " +
                 "pulso extra al interactuar, si existe ese trigger.")]
        [SerializeField] private Animator animator;

        [Tooltip("Trigger del Animator al interactuar. Vacío = no se dispara nada (el brillo en " +
                 "bucle sigue su curso).")]
        [SerializeField] private string interactTrigger = "";

        [Header("Sonido")]
        [Tooltip("id de sonido del AudioManager al usarlo. Vacío = sin sonido.")]
        [SerializeField] private string useSfxId = "SFX_ButtonClick";

        private InputAction _interactAction;
        private bool _playerInRange;
        private PlayerAnimator _playerAnimator;

        private void Reset()
        {
            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.isTrigger = true;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                _interactAction = map?.FindAction(actionName, throwIfNotFound: false);
            }
        }

        private void OnEnable() => _interactAction?.Enable();

        private void OnDisable()
        {
            _interactAction?.Disable();
            if (_playerInRange) InteractionPromptUi.Hide(this);
            _playerInRange = false;
        }

        private void Update()
        {
            if (!_playerInRange) return;
            if (!GameStateManager.CanPlayerAct) return;

            if (InteractPressed()) Interact();
        }

        private bool InteractPressed()
        {
            if (_interactAction != null && _interactAction.WasPressedThisFrame()) return true;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame) return true;

            return TouchInput.ConsumeInteract();
        }

        /// <summary>Usa el espejo. Público para poder llamarlo desde un botón táctil o un evento.</summary>
        public void Interact()
        {
            if (_playerAnimator != null) _playerAnimator.TriggerInteract();

            if (animator != null && !string.IsNullOrWhiteSpace(interactTrigger)) animator.SetTrigger(interactTrigger);

            if (!string.IsNullOrWhiteSpace(useSfxId) && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(useSfxId);

            InteractionPromptUi.Hide(this);

            OnMirrorInteract();

            if (MirrorMenuController.Instance == null)
            {
                Debug.LogWarning("[MirrorInteractable] No hay MirrorMenuController; ¿falta su " +
                                 "PanelSettings en Resources?", this);
                return;
            }

            MirrorMenuController.Instance.Open();
        }

        /// <summary>
        /// TODO: implementar el contenido real del espejo aquí (estadísticas, cosméticos — sin
        /// decidir todavía). Por ahora sólo confirma por consola que el gancho llegó; el menú que
        /// se abre es un cascarón vacío.
        /// </summary>
        protected virtual void OnMirrorInteract()
        {
            Debug.Log("[MirrorInteractable] TODO: implementar el contenido real del espejo.", this);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = true;
            _playerAnimator = other.GetComponentInParent<PlayerAnimator>();
            InteractionPromptUi.Show(this, "Pulsa [Interactuar] para usar el espejo");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = false;
            _playerAnimator = null;
            InteractionPromptUi.Hide(this);
        }

        private void OnDrawGizmosSelected()
        {
            var collider = GetComponent<Collider2D>();
            if (collider == null) return;

            Gizmos.color = new Color(0.5f, 0.85f, 1f, 0.6f);
            Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
        }
    }
}
