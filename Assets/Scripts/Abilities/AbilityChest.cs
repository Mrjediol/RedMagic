using System.Collections;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Cofre que da una habilidad. El jugador se acerca, pulsa interactuar, el cofre reproduce su
    /// animación de abrirse y le equipa una habilidad.
    ///
    /// Por defecto la habilidad es <b>aleatoria</b> entre todas las de
    /// <c>Resources/Abilities</c> — que es como funcionará en el juego —, pero en el Inspector se
    /// puede forzar una concreta para probar: el desplegable lo dibuja
    /// <c>AbilityChestEditor</c> y su primera opción es "Aleatoria".
    ///
    /// La animación no se toca a mano: se enciende el bool del Animator (el mismo
    /// <c>IsOpened</c> que usa el controlador del cofre dorado), así que sirve cualquier
    /// controlador que tenga ese parámetro, y el cofre sigue funcionando aunque no tenga Animator.
    ///
    /// Misma forma de interacción que <c>TombInteractable</c> y <c>CauldronInteractable</c>:
    /// collider en trigger, acción de interactuar del asset de input con teclas de reserva, y
    /// nada ocurre mientras haya un menú abierto (<see cref="GameStateManager.CanPlayerAct"/>).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class AbilityChest : MonoBehaviour
    {
        [Header("Contenido")]
        [Tooltip("Habilidad que suelta este cofre. Vacío = una al azar (lo normal en el juego). " +
                 "El desplegable del Inspector lo dibuja AbilityChestEditor.")]
        [SerializeField] private AbilityDefinition forcedAbility;

        [Tooltip("Un solo uso. Desactívalo para poder abrirlo una y otra vez mientras pruebas.")]
        [SerializeField] private bool singleUse = true;

        [Tooltip("Segundos entre que se abre la tapa y se entrega la habilidad, para que se vea " +
                 "la animación antes del cambio.")]
        [Min(0f)]
        [SerializeField] private float grantDelay = 0.35f;

        [Header("Animación")]
        [Tooltip("Animator del cofre. Vacío = se busca en este objeto y sus hijos.")]
        [SerializeField] private Animator animator;

        [Tooltip("Parámetro bool del Animator que abre la tapa.")]
        [SerializeField] private string openParameter = "IsOpened";

        [Header("Detección")]
        [SerializeField] private string playerTag = "Player";

        [Header("Input")]
        [Tooltip("Asset de acciones. Vacío = se usan las teclas de reserva (E / Enter / botón norte).")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Interact";

        [Header("Aviso en pantalla")]
        [Tooltip("Objeto que se enciende cuando el jugador está cerca (un cartel de 'Pulsa E'). Opcional.")]
        [SerializeField] private GameObject prompt;

        [Header("Sonido")]
        [Tooltip("id de sonido del AudioManager al abrirlo. Vacío = sin sonido.")]
        [SerializeField] private string openSfxId = "SFX_ButtonClick";

        private InputAction _interactAction;
        private AbilityUser _user;
        private bool _playerInRange;
        private bool _opened;
        private bool _busy;

        /// <summary>Habilidad que soltará: la forzada, o null si va a ser aleatoria.</summary>
        public AbilityDefinition ForcedAbility => forcedAbility;

        /// <summary>True si ya se ha abierto y es de un solo uso.</summary>
        public bool IsSpent => _opened && singleUse;

        private void Reset()
        {
            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.isTrigger = true;

            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

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
            if (!_playerInRange || _busy || IsSpent) return;
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

        /// <summary>Abre el cofre. Público para poder llamarlo desde un botón táctil o un evento.</summary>
        public void Interact()
        {
            if (_busy || IsSpent) return;
            StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            _busy = true;

            // Al reabrirlo en modo pruebas hay que cerrar la tapa un frame: la animación de abrir
            // vive en la transición cerrado → abierto, y sin ese paso no se volvería a ver.
            if (_opened && animator != null && HasParameter(openParameter))
            {
                animator.SetBool(openParameter, false);
                yield return null;
            }

            _opened = true;
            SetPromptVisible(false);

            if (animator != null && HasParameter(openParameter)) animator.SetBool(openParameter, true);

            if (!string.IsNullOrWhiteSpace(openSfxId) && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(openSfxId);

            if (grantDelay > 0f) yield return new WaitForSeconds(grantDelay);

            Grant();

            _busy = false;
        }

        private void Grant()
        {
            var ability = PickAbility();
            if (ability == null)
            {
                Debug.LogWarning("[AbilityChest] No hay habilidades en Resources/Abilities: el " +
                                 "cofre se abre vacío.", this);
                return;
            }

            var user = ResolveUser();
            if (user == null)
            {
                Debug.LogWarning("[AbilityChest] No hay ningún AbilityUser al que darle la habilidad.", this);
                return;
            }

            user.Equip(ability);

            // Destello del color de la habilidad sobre el cofre: se ve que ha salido algo y de qué
            // familia es, sin necesidad de UI.
            AbilityFx.Flash(ability.FxSprite, transform.position + Vector3.up * 0.6f,
                            Vector2.one * 0.9f, ability.Accent, 0.6f, 0f, 2f, gameObject);

            Debug.Log($"[AbilityChest] Habilidad obtenida: {ability.DisplayName} ({ability.ShortStats()}).", this);
        }

        private AbilityDefinition PickAbility()
        {
            if (forcedAbility != null) return forcedAbility;

            var all = AbilityLibrary.All;
            return all.Count == 0 ? null : all[Random.Range(0, all.Count)];
        }

        /// <summary>
        /// El jugador que ha abierto el cofre. Se prefiere el que ha entrado en el trigger; si el
        /// cofre se abre por script (sin nadie dentro) se busca el que haya en la partida.
        /// </summary>
        private AbilityUser ResolveUser()
        {
            if (_user != null) return _user;

            var users = FindObjectsByType<AbilityUser>(FindObjectsSortMode.None);
            if (users.Length == 0) return null;

            foreach (var user in users)
                if (user.gameObject.scene.name == "DontDestroyOnLoad") return user;

            return users[0];
        }

        private bool HasParameter(string parameterName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName)) return false;

            foreach (var parameter in animator.parameters)
                if (parameter.name == parameterName) return true;

            Debug.LogWarning($"[AbilityChest] El Animator no tiene el parámetro '{parameterName}'; " +
                             "el cofre da la habilidad pero no se anima.", this);
            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = true;
            _user = other.GetComponentInParent<AbilityUser>();
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
            if (prompt != null) prompt.SetActive(visible && !IsSpent);
        }

        private void OnDrawGizmosSelected()
        {
            var collider = GetComponent<Collider2D>();
            if (collider == null) return;

            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
        }
    }
}
