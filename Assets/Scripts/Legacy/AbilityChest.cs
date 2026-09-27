using System.Collections;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Gameplay;
using RedMagic.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace RedMagic.Abilities
{
    /// <summary>
    /// <b>Legacy — sustituido por <c>Hub.ChestLootContainer</c></b>. Un solo toque daba el arma;
    /// el cofre del hub ahora necesita dos (abrir → recoger) y quedarse gastado hasta la próxima
    /// run, lo que no encajaba en esta clase sin reescribirla — se lee como referencia de la parte
    /// de entrega (<see cref="Grant"/>), que sí se portó tal cual.
    ///
    /// Cofre que da un <b>arma</b> (<see cref="WeaponDefinition"/>). El jugador se acerca, pulsa
    /// interactuar, el cofre reproduce su animación de abrirse y le equipa el arma en el
    /// <see cref="WeaponLoadout"/> de la run.
    ///
    /// Por defecto el arma es <b>aleatoria</b> entre todas las de <c>Resources/Items</c> — que es
    /// como funcionará en el juego —, pero en el Inspector se puede forzar una concreta para probar:
    /// el desplegable lo dibuja <c>AbilityChestEditor</c> y su primera opción es "Aleatoria".
    ///
    /// La animación no se toca a mano: se enciende el bool del Animator (el mismo <c>IsOpened</c>
    /// que usa el controlador del cofre dorado), así que sirve cualquier controlador que tenga ese
    /// parámetro, y el cofre sigue funcionando aunque no tenga Animator.
    ///
    /// Misma forma de interacción que <c>TombInteractable</c> y <c>CauldronInteractable</c>:
    /// collider en trigger, acción de interactuar del asset de input con teclas de reserva, y
    /// nada ocurre mientras haya un menú abierto (<see cref="GameStateManager.CanPlayerAct"/>).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class AbilityChest : MonoBehaviour, ISoundEventSource
    {
        public event System.Action<SoundTrigger> SoundTriggered;

        public void DeclareSoundTriggers(System.Collections.Generic.List<SoundTrigger> into) { into.Add(SoundTrigger.OnInteract); }

        [Header("Contenido")]
        [Tooltip("Arma que suelta este cofre. Vacío = una al azar (lo normal en el juego). " +
                 "El desplegable del Inspector lo dibuja AbilityChestEditor.")]
        [FormerlySerializedAs("forcedAbility")]
        [SerializeField] private WeaponDefinition forcedWeapon;

        [Tooltip("Un solo uso. Desactívalo para poder abrirlo una y otra vez mientras pruebas.")]
        [SerializeField] private bool singleUse = true;

        [Tooltip("Segundos entre que se abre la tapa y se entrega el arma, para que se vea " +
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

        private InputAction _interactAction;
        private bool _playerInRange;
        private bool _opened;
        private bool _busy;

        /// <summary>Arma que soltará: la forzada, o null si va a ser aleatoria.</summary>
        public WeaponDefinition ForcedWeapon => forcedWeapon;

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

            SoundTriggered?.Invoke(SoundTrigger.OnInteract);

            if (grantDelay > 0f) yield return new WaitForSeconds(grantDelay);

            Grant();

            _busy = false;
        }

        private void Grant()
        {
            var weapon = PickWeapon();
            if (weapon == null)
            {
                Debug.LogWarning("[AbilityChest] No hay armas en Resources/Items: el cofre se abre " +
                                 "vacío.", this);
                return;
            }

            if (WeaponLoadout.Instance == null)
            {
                Debug.LogWarning("[AbilityChest] No hay WeaponLoadout al que darle el arma.", this);
                return;
            }

            WeaponLoadout.Instance.Inventory.SetWeapon(weapon);

            // El sistema de armas reemplaza al de habilidades: si el jugador aún llevaba una
            // habilidad equipada, se la quitamos para que el arma dispare de inmediato (WeaponUser
            // cede ante una habilidad equipada). Null-safe por si AbilityUser ya no está.
            foreach (var user in FindObjectsByType<AbilityUser>(FindObjectsSortMode.None))
                user.Equip(null);

            // Destello del color del arma sobre el cofre: se ve que ha salido algo y de qué familia
            // es, sin necesidad de UI.
            AbilityFx.Flash(weapon.Icon, transform.position + Vector3.up * 0.6f,
                            Vector2.one * 0.9f, weapon.Accent, 0.6f, 0f, 2f, gameObject);

            Debug.Log($"[AbilityChest] Arma obtenida: {weapon.DisplayName} " +
                      $"({weapon.BaseDamage:0} dmg · {weapon.BaseCooldown:0.00}s).", this);
        }

        private WeaponDefinition PickWeapon()
        {
            return forcedWeapon != null ? forcedWeapon : WeaponLibrary.Random();
        }

        private bool HasParameter(string parameterName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName)) return false;

            foreach (var parameter in animator.parameters)
                if (parameter.name == parameterName) return true;

            Debug.LogWarning($"[AbilityChest] El Animator no tiene el parámetro '{parameterName}'; " +
                             "el cofre da el arma pero no se anima.", this);
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
