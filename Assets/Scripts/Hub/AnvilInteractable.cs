using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Hub
{
    /// <summary>
    /// El yunque del hub: un solo toque, sin abrir/cerrar y sin bloquear nada — reproduce su
    /// destello de chispas y vuelve solo a reposo. Misma mecánica de detección/input que
    /// <c>CauldronInteractable</c>; no hereda de <see cref="HubLootContainer"/> porque no tiene el
    /// flujo en dos toques que esa base existe para compartir.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class AnvilInteractable : MonoBehaviour
    {
        [Header("Detección")]
        [SerializeField] private string playerTag = "Player";

        [Header("Input")]
        [Tooltip("Asset de acciones. Vacío = se usan las teclas de reserva (E / Enter / botón norte).")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Interact";

        [Header("Animación")]
        [Tooltip("Animator del yunque. Vacío = se busca en este objeto y sus hijos. Sin Animator " +
                 "todavía (no hay arte importado), el gancho de mejora sigue funcionando igual.")]
        [SerializeField] private Animator animator;

        [Tooltip("Trigger del Animator que reproduce la chispa una vez.")]
        [SerializeField] private string sparkTrigger = "Spark";

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

        private bool InteractPressed() => InteractInput.Pressed(_interactAction);

        /// <summary>Usa el yunque. Público para poder llamarlo desde un botón táctil o un evento.</summary>
        public void Interact()
        {
            if (_playerAnimator != null) _playerAnimator.TriggerInteract();

            if (animator != null && !string.IsNullOrWhiteSpace(sparkTrigger)) animator.SetTrigger(sparkTrigger);

            if (!string.IsNullOrWhiteSpace(useSfxId) && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(useSfxId);

            OnAnvilInteract();
        }

        /// <summary>
        /// TODO: implementar la lógica real de mejora de arma aquí (qué se sube, con qué coste,
        /// si consume alguna moneda). Por ahora sólo confirma por consola que el gancho llegó.
        /// </summary>
        protected virtual void OnAnvilInteract()
        {
            Debug.Log("[AnvilInteractable] TODO: implementar la mejora de arma real.", this);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = true;
            _playerAnimator = other.GetComponentInParent<PlayerAnimator>();
            InteractionPromptUi.Show(this, "Pulsa [Interactuar] para usar el yunque");
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

            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
        }
    }
}
