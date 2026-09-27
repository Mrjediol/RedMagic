using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Hub;
using RedMagic.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Economy
{
    /// <summary>
    /// <b>Legacy — sustituido por <c>Hub.BookLootContainer</c></b>. Era el placeholder de pruebas
    /// para el menú de mejoras: un solo toque, sin animación real, sin el flujo en dos pasos que
    /// el atril del libro mágico sí necesita (abrir → consultar). Se lee como referencia de cómo
    /// se abría <see cref="UpgradeMenuController"/> antes de que existiera el arte del libro.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class CauldronInteractable : MonoBehaviour, ISoundEventSource
    {
        public event System.Action<SoundTrigger> SoundTriggered;

        [Header("Detección")]
        [Tooltip("Etiqueta del objeto que puede usar el disparador.")]
        [SerializeField] private string playerTag = "Player";

        [Header("Aviso en pantalla")]
        [Tooltip("Texto del cartel de interacción (InteractionPromptUi).")]
        [SerializeField] private string promptText = "Pulsa [Interactuar] para abrir el libro";

        [Tooltip("Objeto que se enciende cuando el jugador está a rango (un cartel de 'Pulsa E'). " +
                 "Opcional; el cartel de texto de InteractionPromptUi funciona sin esto.")]
        [SerializeField] private GameObject prompt;

        [Header("Animación")]
        [Tooltip("Animator del objeto (p. ej. el libro abriéndose). Vacío = se busca en este " +
                 "objeto y sus hijos; puede no existir todavía si no hay arte importado.")]
        [SerializeField] private Animator animator;

        [Tooltip("Parámetro bool del Animator que se enciende al interactuar. Vacío = no se anima.")]
        [SerializeField] private string openParameter = "IsOpened";

        private bool _playerInRange;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            SetPromptVisible(false);
        }

        private void OnDisable()
        {
            _playerInRange = false;
            SetPromptVisible(false);
        }

        private void Update()
        {
            if (!_playerInRange) return;

            // El propio menú abre en pausa (CanPlayerAct = false), así que esto sólo dispara
            // cuando NO hay ningún menú abierto: no se puede reabrir encima de sí mismo.
            if (!GameStateManager.CanPlayerAct) return;

            if (InteractPressed()) Open();
        }

        private static bool InteractPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)
                return true;

            return TouchInput.ConsumeInteract();
        }

        /// <summary>Abre el menú de mejoras. Público para poder llamarlo desde un botón de UI o una cinemática.</summary>
        public void Open()
        {
            if (UpgradeMenuController.Instance == null)
            {
                Debug.LogWarning("[CauldronInteractable] No hay UpgradeMenuController; ¿falta su " +
                                 "PanelSettings en Resources?", this);
                return;
            }

            if (animator != null && !string.IsNullOrWhiteSpace(openParameter) && HasParameter(openParameter))
                animator.SetBool(openParameter, true);

            SoundTriggered?.Invoke(SoundTrigger.OnInteract);

            SetPromptVisible(false);
            UpgradeMenuController.Instance.Open();
        }

        private bool HasParameter(string parameterName)
        {
            foreach (var parameter in animator.parameters)
                if (parameter.name == parameterName) return true;

            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInRange = true;
            SetPromptVisible(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            _playerInRange = false;
            SetPromptVisible(false);
        }

        private void SetPromptVisible(bool visible)
        {
            if (prompt != null) prompt.SetActive(visible);

            if (visible) InteractionPromptUi.Show(this, promptText);
            else InteractionPromptUi.Hide(this);
        }

        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<Collider2D>();
            if (col == null) return;
            Gizmos.color = new Color(0.7f, 0.4f, 1f, 0.5f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
