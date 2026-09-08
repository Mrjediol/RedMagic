using RedMagic.Core;
using RedMagic.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Items
{
    /// <summary>
    /// El altar que suelta el jefe al morir: al interactuar abre la forja
    /// (<see cref="WeaponForgeMenuController"/>), donde se sube de nivel el arma equipada del
    /// sistema de items a cambio de calaveras.
    ///
    /// Reemplaza al viejo <c>WeaponUpgradeAltar</c> (ver
    /// <c>Assets/Scripts/Legacy/WeaponLevels/</c>), que buscaba un <c>AbilityUser</c> — con el
    /// sistema de habilidades ya sin uso, el jugador nunca tenía nada equipado ahí y la forja
    /// siempre decía "no llevas ningún arma equipada" aunque sí llevara una del sistema nuevo.
    ///
    /// No sabe nada de niveles ni de precios — sólo abre el menú —, así que cambiar la economía de
    /// las mejoras no toca este archivo. Misma forma de interacción que la tumba, el caldero y el
    /// cofre: collider en trigger, acción de interactuar con teclas de reserva, y nada ocurre
    /// mientras haya un menú abierto.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class WeaponForgeAltar : MonoBehaviour
    {
        [Header("Uso")]
        [Tooltip("Desaparece al cerrar la forja. Apagado, se puede volver a usar (para probar).")]
        [SerializeField] private bool consumeOnUse;

        [Header("Detección")]
        [SerializeField] private string playerTag = "Player";

        [Header("Input")]
        [Tooltip("Asset de acciones. Vacío = teclas de reserva (E / Enter / botón norte).")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Interact";

        [Header("Aviso en pantalla")]
        [Tooltip("Objeto que se enciende cuando el jugador está cerca. Opcional.")]
        [SerializeField] private GameObject prompt;

        private InputAction _interactAction;
        private WeaponUser _user;
        private bool _playerInRange;
        private bool _menuOpen;

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

            SetPromptVisible(false);
        }

        private void OnEnable() => _interactAction?.Enable();

        private void OnDisable()
        {
            _interactAction?.Disable();
            _playerInRange = false;
            SetPromptVisible(false);
        }

        private void Update()
        {
            // Al cerrarse la forja (Esc, E o el aspa) el altar se entera aquí: el menú es un
            // singleton compartido y no le hace falta conocer a quien lo abrió.
            if (_menuOpen)
            {
                var menu = WeaponForgeMenuController.Instance;
                if (menu == null || GameStateManager.CanPlayerAct)
                {
                    _menuOpen = false;
                    if (consumeOnUse) Destroy(gameObject);
                    else SetPromptVisible(_playerInRange);
                }
                return;
            }

            if (!_playerInRange || !GameStateManager.CanPlayerAct) return;

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

        /// <summary>Abre la forja. Público para poder llamarlo desde un botón táctil o un evento.</summary>
        public void Interact()
        {
            var menu = WeaponForgeMenuController.Instance;
            if (menu == null)
            {
                Debug.LogWarning("[WeaponForgeAltar] No hay menú de forja disponible.", this);
                return;
            }

            SetPromptVisible(false);
            menu.Open(_user);
            _menuOpen = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = true;
            _user = other.GetComponentInParent<WeaponUser>();
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
            var collider = GetComponent<Collider2D>();
            if (collider == null) return;

            Gizmos.color = new Color(0.9f, 0.4f, 1f, 0.6f);
            Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
        }
    }
}
