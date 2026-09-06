using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Economy
{
    /// <summary>
    /// El caldero del hub: el jugador se acerca, pulsa interactuar y se abre el menú de mejoras
    /// permanentes (<see cref="UpgradeMenuController"/>).
    ///
    /// Misma mecánica que <c>TombInteractable</c>: se pone sobre un <see cref="Collider2D"/>
    /// marcado como trigger, detecta al jugador por etiqueta y lee la pulsación de interactuar
    /// (E / Enter / botón norte del mando / cola táctil). El aviso en pantalla (<see cref="prompt"/>)
    /// es opcional.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class CauldronInteractable : MonoBehaviour
    {
        [Header("Detección")]
        [Tooltip("Etiqueta del objeto que puede usar el caldero.")]
        [SerializeField] private string playerTag = "Player";

        [Header("Aviso en pantalla")]
        [Tooltip("Objeto que se enciende cuando el jugador está a rango (un cartel de 'Pulsa E'). Opcional.")]
        [SerializeField] private GameObject prompt;

        [Header("Sonido")]
        [Tooltip("id de sonido del AudioManager al abrir el menú. Vacío = sin sonido.")]
        [SerializeField] private string openSfxId = "SFX_ButtonClick";

        private bool _playerInRange;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake() => SetPromptVisible(false);

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

            if (!string.IsNullOrWhiteSpace(openSfxId) && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(openSfxId);

            SetPromptVisible(false);
            UpgradeMenuController.Instance.Open();
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
