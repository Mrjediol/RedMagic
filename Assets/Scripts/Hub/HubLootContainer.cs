using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Gameplay;
using RedMagic.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RedMagic.Hub
{
    /// <summary>
    /// Base compartida del cofre y el armario del hub: abrir → esperar recogida → recoger → cerrar,
    /// y quedarse gastado hasta que se complete una run y se vuelva al hub. Se factoriza aquí
    /// porque, sin esto, escribir el armario habría sido copiar entero <c>AbilityChest</c> (rango +
    /// input + prompt + Animator) para cambiar sólo qué pasa al recoger — exactamente el caso que
    /// pide una herramienta compartida en vez de una tercera copia.
    ///
    /// Detección e input son la misma receta que <c>AbilityChest</c>/<c>CauldronInteractable</c>/
    /// <c>SectionExit</c>: collider en trigger + etiqueta, acción de interactuar del asset de
    /// input con teclas de reserva (E/Enter/botón norte/táctil), nada mientras
    /// <see cref="GameStateManager.CanPlayerAct"/> esté en false.
    ///
    /// El texto del cartel usa <see cref="InteractionPromptUi"/> en vez del viejo
    /// <c>GameObject prompt</c> fijo, porque aquí el texto cambia a mitad de partida ("Pulsa para
    /// abrir" → "Pulsa para recoger").
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public abstract class HubLootContainer : MonoBehaviour, ISoundEventSource
    {
        /// <summary>OnInteract al abrir/usar, OnLoot al recoger. Ver <see cref="SoundEmitter"/>.</summary>
        public event System.Action<SoundTrigger> SoundTriggered;

        public void DeclareSoundTriggers(System.Collections.Generic.List<SoundTrigger> into) { into.Add(SoundTrigger.OnInteract); into.Add(SoundTrigger.OnLoot); }

        private enum State
        {
            Closed,            // nunca interactuado, o ya reseteado por una run nueva
            WaitingForPickup,  // abierto, con el contenido a la vista, esperando el 2º toque
            Depleted           // ya se recogió; cerrado y sin uso hasta el próximo reset
        }

        [Header("Detección")]
        [SerializeField] private string playerTag = "Player";

        [Header("Input")]
        [Tooltip("Asset de acciones. Vacío = se usan las teclas de reserva (E / Enter / botón norte).")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Interact";

        [Header("Animación")]
        [Tooltip("Animator del objeto. Vacío = se busca en este objeto y sus hijos. Puede faltar " +
                 "mientras no haya arte todavía: sin Animator, todo lo demás (prompts, entrega, " +
                 "reset con la run) funciona igual, sólo no se ve abrirse/cerrarse.")]
        [SerializeField] private Animator animator;

        [Tooltip("Parámetro bool del Animator: true = abierto (misma convención que el cofre " +
                 "dorado de Cainos y AbilityChest).")]
        [SerializeField] private string openParameter = "IsOpened";

        private InputAction _interactAction;
        private bool _playerInRange;
        private PlayerAnimator _playerAnimator;
        private State _state = State.Closed;
        private bool _boundToRun;

        /// <summary>Clave de idioma del cartel mientras está cerrado ("Pulsa E para abrir el cofre").</summary>
        protected abstract string OpenPromptKey { get; }

        /// <summary>Clave de idioma del cartel una vez abierto, esperando el 2º toque ("Pulsa E para recoger").</summary>
        protected abstract string PickupPromptKey { get; }

        /// <summary>Se llama en el 2º toque, con el objeto ya abierto. Aquí va la recompensa real.</summary>
        protected abstract void OnLoot();

        /// <summary>
        /// True si, mientras no se haya recogido, esto debe impedir salir del hub (el cofre). Los
        /// contenedores opcionales (el armario) lo dejan en false.
        /// </summary>
        protected virtual bool BlocksHubExitUntilLooted => false;

        /// <summary>True si está esperando el toque de "recoger" (útil para <see cref="BlocksHubExitUntilLooted"/> en subclases).</summary>
        protected bool IsWaitingForPickup => _state == State.WaitingForPickup;

        /// <summary>True mientras el contenido de este toque no se haya recogido todavía.</summary>
        public bool IsPending => _state != State.Depleted;

        private void Reset()
        {
            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.isTrigger = true;
        }

        protected virtual void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                _interactAction = map?.FindAction(actionName, throwIfNotFound: false);
            }
        }

        protected virtual void OnEnable()
        {
            _interactAction?.Enable();
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryBindToRun();
        }

        protected virtual void OnDisable()
        {
            _interactAction?.Disable();
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_boundToRun && RunManager.Instance != null)
                RunManager.Instance.RunEnded -= OnRunEnded;
            _boundToRun = false;

            if (_playerInRange) InteractionPromptUi.Hide(this);
            _playerInRange = false;
        }

        // El RunManager sólo existe una vez cargado el hub; se reintenta tras cada carga de
        // escena hasta engancharlo (mismo patrón que WeaponLoadout/AbilityLevelManager).
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryBindToRun();

        private void TryBindToRun()
        {
            if (_boundToRun || RunManager.Instance == null) return;

            RunManager.Instance.RunEnded += OnRunEnded;
            _boundToRun = true;
        }

        /// <summary>
        /// Completar una run y volver al hub repone el contenedor — gane o pierda la run: es la
        /// misma regla que ya usa <c>WeaponLoadout</c> para vaciar el inventario al terminar, y
        /// aquí hace falta por la misma razón: sin un arma nueva del cofre, la siguiente run no
        /// se puede jugar.
        /// </summary>
        private void OnRunEnded(bool completed)
        {
            if (_state == State.Closed) return;

            _state = State.Closed;
            if (animator != null && HasParameter(openParameter)) animator.SetBool(openParameter, false);

            if (_playerInRange) RefreshPrompt();
        }

        protected virtual void Update()
        {
            if (!_playerInRange) return;
            if (!GameStateManager.CanPlayerAct) return;
            if (_state == State.Depleted) return;

            if (InteractPressed()) Interact();
        }

        private bool InteractPressed() => InteractInput.Pressed(_interactAction);

        /// <summary>Toque de interacción. Público para poder llamarlo desde un botón táctil o un evento.</summary>
        public void Interact()
        {
            if (_playerAnimator != null) _playerAnimator.TriggerInteract();

            switch (_state)
            {
                case State.Closed:
                    Open();
                    break;
                case State.WaitingForPickup:
                    Pickup();
                    break;
            }
        }

        private void Open()
        {
            _state = State.WaitingForPickup;

            if (animator != null && HasParameter(openParameter)) animator.SetBool(openParameter, true);

            SoundTriggered?.Invoke(SoundTrigger.OnInteract);

            RefreshPrompt();
        }

        private void Pickup()
        {
            OnLoot();

            _state = State.Depleted;

            if (animator != null && HasParameter(openParameter)) animator.SetBool(openParameter, false);

            SoundTriggered?.Invoke(SoundTrigger.OnLoot);

            InteractionPromptUi.Hide(this);
        }

        private bool HasParameter(string parameterName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName)) return false;

            foreach (var parameter in animator.parameters)
                if (parameter.name == parameterName) return true;

            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = true;
            _playerAnimator = other.GetComponentInParent<PlayerAnimator>();
            RefreshPrompt();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = false;
            _playerAnimator = null;
            InteractionPromptUi.Hide(this);
        }

        private void RefreshPrompt()
        {
            if (!_playerInRange || _state == State.Depleted)
            {
                InteractionPromptUi.Hide(this);
                return;
            }

            InteractionPromptUi.ShowKey(this, _state == State.WaitingForPickup ? PickupPromptKey : OpenPromptKey);
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
