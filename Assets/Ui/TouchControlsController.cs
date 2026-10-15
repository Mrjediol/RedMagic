using System.Collections.Generic;
using RedMagic.Core;
using RedMagic.Gameplay;
using RedMagic.Localization;
using RedMagic.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Controles táctiles en pantalla (izquierda / derecha / abajo / atacar / saltar) para móvil.
    /// Escriben en <see cref="TouchInput"/>, que <c>PlayerMovement</c> combina con el teclado y el mando.
    ///
    /// Reparto de responsabilidades:
    ///  - El contenedor <c>touchControls</c> lleva la clase USS <c>touch-only</c>: el componente
    ///    <see cref="TouchOnlyUI"/> del mismo GameObject lo oculta cuando hay un mando activo.
    ///  - Este script oculta la capa interior mientras el juego está en pausa.
    ///  - Pinta el cooldown de cada acción (<see cref="CooldownDial"/>) sobre su botón: en táctil
    ///    el HUD no enseña casillas, el pulgar ya está aquí. Un botón nuevo con cooldown (parry) =
    ///    una línea <c>AttachDial</c>.
    ///  - Dos esquemas, según <see cref="HudSettings.GestureControls"/> (Opciones ▸ Interfaz): los botones
    ///    clásicos (<c>movePad</c> + <c>actionPad</c>) o los gestos (<c>gestureLayout</c>, lógica en
    ///    <see cref="TouchGestureInput"/>, parámetros en el Inspector ▸ Gestures).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class TouchControlsController : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _inner;
        private Button _leftButton;
        private Button _rightButton;
        private Button _jumpButton;
        private Button _attackButton;
        private Button _downButton;
        private Button _dashButton;
        private Button _weaponButton;
        private Button _parryButton;
        private Button _interactButton;

        [Tooltip("Controles por gestos: zona de movimiento, joystick y dash por doble toque. Se ajusta en vivo en Play.")]
        [SerializeField] private TouchGestureInput gestures = new TouchGestureInput();

        private VisualElement _movePad, _actionPad, _gestureLayout;
        private Button _gestureJumpButton, _gestureAttackButton, _gestureDashButton;
        private bool? _gestureMode;

        // Cooldown pintado sobre el botón que lanza cada acción (el mismo dial que el HUD de PC).
        private readonly List<(CooldownSlot slot, CooldownDial dial)> _dials = new();

        private void OnEnable()
        {
            if (_document == null) _document = GetComponent<UIDocument>();

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null) return;

            LocalizedUi.BindTree(_root);
            _inner = _root.Q<VisualElement>("touchControlsInner");
            _leftButton = _root.Q<Button>("leftButton");
            _rightButton = _root.Q<Button>("rightButton");
            _jumpButton = _root.Q<Button>("jumpButton");
            _attackButton = _root.Q<Button>("attackButton");
            _downButton = _root.Q<Button>("downButton");
            _dashButton = _root.Q<Button>("dashButton");
            _weaponButton = _root.Q<Button>("weaponButton");
            _parryButton = _root.Q<Button>("parryButton");
            _interactButton = _root.Q<Button>("interactButton");
            _movePad = _root.Q<VisualElement>("movePad");
            _actionPad = _root.Q<VisualElement>("actionPad");
            _gestureLayout = _root.Q<VisualElement>("gestureLayout");
            _gestureJumpButton = _root.Q<Button>("gestureJumpButton");
            _gestureAttackButton = _root.Q<Button>("gestureAttackButton");
            _gestureDashButton = _root.Q<Button>("gestureDashButton");
            gestures.Bind(_root.Q<VisualElement>("gestureSurface"), _root.Q<VisualElement>("gestureZone"),
                          _root.Q<VisualElement>("gestureStick"), _root.Q<VisualElement>("gestureKnob"),
                          _root.Q<VisualElement>("gestureAim"));
            if (_interactButton != null)
            {
                _interactButton.style.display = DisplayStyle.None;
                _interactButton.RegisterCallback<PointerDownEvent>(OnInteractDown);
                _interactButton.RegisterCallback<PointerUpEvent>(OnInteractUp);
                _interactButton.RegisterCallback<PointerLeaveEvent>(OnInteractLeave);
                _interactButton.RegisterCallback<PointerCaptureOutEvent>(OnInteractCaptureOut);
            }

            WireHold(_leftButton, -1f);
            WireHold(_rightButton, 1f);

            _jumpButton?.RegisterCallback<PointerDownEvent>(OnJumpDown);
            _gestureJumpButton?.RegisterCallback<PointerDownEvent>(OnJumpDown);
            _attackButton?.RegisterCallback<PointerDownEvent>(OnAttackDown);
            _gestureAttackButton?.RegisterCallback<PointerDownEvent>(OnAttackDown);
            _dashButton?.RegisterCallback<PointerDownEvent>(OnDashDown);
            _gestureDashButton?.RegisterCallback<PointerDownEvent>(OnDashDown);

            if (_weaponButton != null)
                _weaponButton.RegisterCallback<PointerDownEvent>(OnWeaponDown);

            if (_parryButton != null)
                _parryButton.RegisterCallback<PointerDownEvent>(OnParryDown);

            if (_downButton != null)
            {
                _downButton.RegisterCallback<PointerDownEvent>(OnDownDown);
                _downButton.RegisterCallback<PointerUpEvent>(OnDownUp);
                _downButton.RegisterCallback<PointerLeaveEvent>(OnDownLeave);
                _downButton.RegisterCallback<PointerCaptureOutEvent>(OnDownCaptureOut);
            }

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.PausedChanged -= OnPausedChanged;
                GameStateManager.Instance.PausedChanged += OnPausedChanged;
                OnPausedChanged(GameStateManager.Instance.IsPaused);
            }

            AttachDial(_attackButton, CooldownSlot.Attack);
            AttachDial(_dashButton, CooldownSlot.Dash);
            AttachDial(_parryButton, CooldownSlot.Parry);
            AttachDial(_weaponButton, CooldownSlot.Weapon);
            AttachDial(_gestureAttackButton, CooldownSlot.Attack);
            AttachDial(_gestureDashButton, CooldownSlot.Dash);

            HudSettings.Changed -= ApplyScheme;
            HudSettings.Changed += ApplyScheme;
            _gestureMode = null;
            ApplyScheme();

            TouchInput.Clear();
        }

        // ------------------------------------------------------------------ esquema (botones / gestos)

        private void ApplyScheme()
        {
            bool gesture = HudSettings.GestureControls;
            if (_gestureMode == gesture) return;
            _gestureMode = gesture;

            var classic = gesture ? DisplayStyle.None : DisplayStyle.Flex;
            if (_movePad != null) _movePad.style.display = classic;
            if (_actionPad != null) _actionPad.style.display = classic;
            if (_gestureLayout != null) _gestureLayout.style.display = gesture ? DisplayStyle.Flex : DisplayStyle.None;

            gestures.Reset();
            TouchInput.Clear();
        }

        // ------------------------------------------------------------------ cooldowns

        private void AttachDial(Button button, CooldownSlot slot)
        {
            if (button == null || button.Q<CooldownDial>() != null) return;   // OnEnable puede repetirse
            var dial = new CooldownDial { Accent = HudStyle.Accent(slot), RingWidth = 7f };
            button.Add(dial);
            _dials.Add((slot, dial));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_gestureMode == true) gestures.Tick();

            // Interactuar sólo con algo a mano: el cartel de interacción a la vista o un interactuable que lo pide.
            if (_interactButton != null)
            {
                bool wanted = TouchInput.InteractButtonWanted || Hub.InteractionPromptUi.IsVisible;
                var display = wanted ? DisplayStyle.Flex : DisplayStyle.None;
                if (_interactButton.style.display != display) _interactButton.style.display = display;
                if (!wanted) TouchInput.InteractHeld = false;
            }

            // El botón del arma sólo con un arma equipada (sin arma no hace nada).
            if (_weaponButton != null)
            {
                bool armed = HudPlayer.Cooldown(CooldownSlot.Weapon) != null;
                var display = armed ? DisplayStyle.Flex : DisplayStyle.None;
                if (_weaponButton.style.display != display) _weaponButton.style.display = display;
            }
            foreach (var (slot, dial) in _dials)
            {
                var source = HudPlayer.Cooldown(slot);
                if (source == null)
                {
                    if (!dial.Ready) dial.ResetState();
                    continue;
                }
                dial.Tick(source.Cooldown, dt);
            }
        }

        private void OnDisable()
        {
            UnwireHold(_leftButton);
            UnwireHold(_rightButton);

            if (_interactButton != null)
            {
                _interactButton.UnregisterCallback<PointerDownEvent>(OnInteractDown);
                _interactButton.UnregisterCallback<PointerUpEvent>(OnInteractUp);
                _interactButton.UnregisterCallback<PointerLeaveEvent>(OnInteractLeave);
                _interactButton.UnregisterCallback<PointerCaptureOutEvent>(OnInteractCaptureOut);
                TouchInput.InteractHeld = false;
            }

            _jumpButton?.UnregisterCallback<PointerDownEvent>(OnJumpDown);
            _gestureJumpButton?.UnregisterCallback<PointerDownEvent>(OnJumpDown);
            _attackButton?.UnregisterCallback<PointerDownEvent>(OnAttackDown);
            _gestureAttackButton?.UnregisterCallback<PointerDownEvent>(OnAttackDown);
            _dashButton?.UnregisterCallback<PointerDownEvent>(OnDashDown);
            _gestureDashButton?.UnregisterCallback<PointerDownEvent>(OnDashDown);

            gestures.Unbind();
            HudSettings.Changed -= ApplyScheme;

            if (_weaponButton != null)
                _weaponButton.UnregisterCallback<PointerDownEvent>(OnWeaponDown);

            if (_parryButton != null)
                _parryButton.UnregisterCallback<PointerDownEvent>(OnParryDown);

            if (_downButton != null)
            {
                _downButton.UnregisterCallback<PointerDownEvent>(OnDownDown);
                _downButton.UnregisterCallback<PointerUpEvent>(OnDownUp);
                _downButton.UnregisterCallback<PointerLeaveEvent>(OnDownLeave);
                _downButton.UnregisterCallback<PointerCaptureOutEvent>(OnDownCaptureOut);
            }

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.PausedChanged -= OnPausedChanged;

            TouchInput.Clear();
        }

        // ------------------------------------------------------------------ pulsación mantenida

        private void WireHold(Button button, float value)
        {
            if (button == null) return;

            button.userData = value;
            button.RegisterCallback<PointerDownEvent>(OnMoveDown);
            button.RegisterCallback<PointerUpEvent>(OnMoveUp);
            button.RegisterCallback<PointerLeaveEvent>(OnMoveLeave);
            button.RegisterCallback<PointerCaptureOutEvent>(OnMoveCaptureOut);
        }

        private void UnwireHold(Button button)
        {
            if (button == null) return;

            button.UnregisterCallback<PointerDownEvent>(OnMoveDown);
            button.UnregisterCallback<PointerUpEvent>(OnMoveUp);
            button.UnregisterCallback<PointerLeaveEvent>(OnMoveLeave);
            button.UnregisterCallback<PointerCaptureOutEvent>(OnMoveCaptureOut);
        }

        private static void OnMoveDown(PointerDownEvent evt)
        {
            if (evt.currentTarget is VisualElement ve && ve.userData is float value)
                TouchInput.Horizontal = value;
        }

        private static void OnMoveUp(PointerUpEvent evt) => Release(evt.currentTarget);

        private static void OnMoveLeave(PointerLeaveEvent evt) => Release(evt.currentTarget);

        private static void OnMoveCaptureOut(PointerCaptureOutEvent evt) => Release(evt.currentTarget);

        private static void Release(IEventHandler target)
        {
            // Sólo se suelta si el botón liberado es el que marcaba la dirección actual, para no
            // cancelar la otra dirección cuando se pulsan las dos a la vez.
            if (target is VisualElement ve && ve.userData is float value &&
                Mathf.Approximately(TouchInput.Horizontal, value))
            {
                TouchInput.Horizontal = 0f;
            }
        }

        private static void OnJumpDown(PointerDownEvent evt) => TouchInput.QueueJump();

        private static void OnAttackDown(PointerDownEvent evt) => TouchInput.QueueAttack();

        private static void OnDashDown(PointerDownEvent evt) => TouchInput.QueueDash();

        private static void OnWeaponDown(PointerDownEvent evt) => TouchInput.QueueWeapon();

        private static void OnParryDown(PointerDownEvent evt) => TouchInput.QueueParry();

        // ------------------------------------------------------------------ abajo (atravesar plataformas)

        // Interactuar: la pulsación (comprar, abrir…) y el estado mantenido (reciclar un item del suelo).
        private static void OnInteractDown(PointerDownEvent evt) { TouchInput.QueueInteract(); TouchInput.InteractHeld = true; }

        private static void OnInteractUp(PointerUpEvent evt) => TouchInput.InteractHeld = false;

        private static void OnInteractLeave(PointerLeaveEvent evt) => TouchInput.InteractHeld = false;

        private static void OnInteractCaptureOut(PointerCaptureOutEvent evt) => TouchInput.InteractHeld = false;

        private static void OnDownDown(PointerDownEvent evt) => TouchInput.Down = true;

        private static void OnDownUp(PointerUpEvent evt) => TouchInput.Down = false;

        private static void OnDownLeave(PointerLeaveEvent evt) => TouchInput.Down = false;

        private static void OnDownCaptureOut(PointerCaptureOutEvent evt) => TouchInput.Down = false;

        // ------------------------------------------------------------------ pausa

        private void OnPausedChanged(bool paused)
        {
            if (_inner != null)
                _inner.style.display = paused ? DisplayStyle.None : DisplayStyle.Flex;

            if (!paused) return;
            gestures.Reset();
            TouchInput.Clear();
        }
    }
}
