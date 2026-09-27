using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Economy;
using RedMagic.Gameplay;
using RedMagic.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RedMagic.Hub
{
    /// <summary>
    /// El espejo del hub: un solo toque, reproduce su animación y abre
    /// <see cref="MirrorMenuController"/> — la rejilla 3×3 de pasivas legendarias. Misma mecánica
    /// de detección/input que <c>CauldronInteractable</c>/<see cref="AnvilInteractable"/>.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class MirrorInteractable : MonoBehaviour, ISoundEventSource
    {
        /// <summary>OnInteract al abrir/usar, OnLoot al recoger. Ver <see cref="SoundEmitter"/>.</summary>
        public event System.Action<SoundTrigger> SoundTriggered;

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

        [Header("Aviso de pasiva legendaria nueva")]
        [Tooltip("Desplazamiento, en unidades de mundo, del \"!\" que aparece sobre el espejo " +
                 "mientras LegendaryPassiveManager.HasNewPassiveNotification esté activo.")]
        [SerializeField] private Vector3 notificationOffset = new(0f, 1.6f, 0f);

        private InputAction _interactAction;
        private bool _playerInRange;
        private PlayerAnimator _playerAnimator;

        private static Font _notificationFont;
        private GameObject _notificationBadge;

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

            BuildNotificationBadge();
        }

        private void OnEnable()
        {
            _interactAction?.Enable();

            if (LegendaryPassiveManager.Instance != null)
                LegendaryPassiveManager.Instance.NotificationChanged += OnNotificationChanged;
            RefreshNotificationBadge();
        }

        private void OnDisable()
        {
            _interactAction?.Disable();

            if (LegendaryPassiveManager.Instance != null)
                LegendaryPassiveManager.Instance.NotificationChanged -= OnNotificationChanged;

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

        /// <summary>Usa el espejo. Público para poder llamarlo desde un botón táctil o un evento.</summary>
        public void Interact()
        {
            if (_playerAnimator != null) _playerAnimator.TriggerInteract();

            if (animator != null && !string.IsNullOrWhiteSpace(interactTrigger)) animator.SetTrigger(interactTrigger);

            SoundTriggered?.Invoke(SoundTrigger.OnInteract);

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
        /// Gancho para lo que un espejo concreto quiera hacer además de abrir el menú (VFX, SFX
        /// extra...). No hace nada por defecto.
        /// </summary>
        protected virtual void OnMirrorInteract() { }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = true;
            _playerAnimator = other.GetComponentInParent<PlayerAnimator>();
            InteractionPromptUi.ShowKey(this, "prompt.mirror");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerInRange = false;
            _playerAnimator = null;
            InteractionPromptUi.Hide(this);
        }

        // ------------------------------------------------------------------ aviso de pasiva nueva

        /// <summary>
        /// "!" en world space (Canvas + Text con la fuente incorporada, sin prefab ni asset de
        /// fuente) flotando sobre el espejo — mismo trato de construcción en código que
        /// <see cref="RedMagic.Fx.DamagePopups"/>. Placeholder hasta que haya un icono de verdad.
        /// </summary>
        private void BuildNotificationBadge()
        {
            _notificationFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("MirrorNotificationBadge", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = notificationOffset;
            go.transform.localScale = Vector3.one * 0.02f;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(60f, 60f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.font = _notificationFont;
            text.text = "!";
            text.fontSize = 44;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.84f, 0.25f);

            _notificationBadge = go;
            _notificationBadge.SetActive(false);
        }

        private void OnNotificationChanged(bool hasNotification) => RefreshNotificationBadge();

        private void RefreshNotificationBadge()
        {
            if (_notificationBadge == null) return;
            bool show = LegendaryPassiveManager.Instance != null &&
                        LegendaryPassiveManager.Instance.HasNewPassiveNotification;
            _notificationBadge.SetActive(show);
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
