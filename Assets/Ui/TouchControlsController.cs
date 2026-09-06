using RedMagic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Controles táctiles en pantalla (izquierda / derecha / agacharse / atacar / saltar) para móvil.
    /// Escriben en <see cref="TouchInput"/>, que <c>PlayerMovement</c> combina con el teclado y el mando.
    ///
    /// Reparto de responsabilidades:
    ///  - El contenedor <c>touchControls</c> lleva la clase USS <c>touch-only</c>: el componente
    ///    <see cref="TouchOnlyUI"/> del mismo GameObject lo oculta cuando hay un mando activo.
    ///  - Este script oculta la capa interior mientras el juego está en pausa.
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
        private Button _crouchButton;
        private Button _dashButton;
        private Button _fireballButton;

        private void OnEnable()
        {
            if (_document == null) _document = GetComponent<UIDocument>();

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null) return;

            _inner = _root.Q<VisualElement>("touchControlsInner");
            _leftButton = _root.Q<Button>("leftButton");
            _rightButton = _root.Q<Button>("rightButton");
            _jumpButton = _root.Q<Button>("jumpButton");
            _attackButton = _root.Q<Button>("attackButton");
            _crouchButton = _root.Q<Button>("crouchButton");
            _dashButton = _root.Q<Button>("dashButton");
            _fireballButton = _root.Q<Button>("fireballButton");

            WireHold(_leftButton, -1f);
            WireHold(_rightButton, 1f);

            if (_jumpButton != null)
                _jumpButton.RegisterCallback<PointerDownEvent>(OnJumpDown);

            if (_attackButton != null)
                _attackButton.RegisterCallback<PointerDownEvent>(OnAttackDown);

            if (_dashButton != null)
                _dashButton.RegisterCallback<PointerDownEvent>(OnDashDown);

            if (_fireballButton != null)
                _fireballButton.RegisterCallback<PointerDownEvent>(OnFireballDown);

            if (_crouchButton != null)
            {
                _crouchButton.RegisterCallback<PointerDownEvent>(OnCrouchDown);
                _crouchButton.RegisterCallback<PointerUpEvent>(OnCrouchUp);
                _crouchButton.RegisterCallback<PointerLeaveEvent>(OnCrouchLeave);
                _crouchButton.RegisterCallback<PointerCaptureOutEvent>(OnCrouchCaptureOut);
            }

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.PausedChanged -= OnPausedChanged;
                GameStateManager.Instance.PausedChanged += OnPausedChanged;
                OnPausedChanged(GameStateManager.Instance.IsPaused);
            }

            TouchInput.Clear();
        }

        private void OnDisable()
        {
            UnwireHold(_leftButton);
            UnwireHold(_rightButton);

            if (_jumpButton != null)
                _jumpButton.UnregisterCallback<PointerDownEvent>(OnJumpDown);

            if (_attackButton != null)
                _attackButton.UnregisterCallback<PointerDownEvent>(OnAttackDown);

            if (_dashButton != null)
                _dashButton.UnregisterCallback<PointerDownEvent>(OnDashDown);

            if (_fireballButton != null)
                _fireballButton.UnregisterCallback<PointerDownEvent>(OnFireballDown);

            if (_crouchButton != null)
            {
                _crouchButton.UnregisterCallback<PointerDownEvent>(OnCrouchDown);
                _crouchButton.UnregisterCallback<PointerUpEvent>(OnCrouchUp);
                _crouchButton.UnregisterCallback<PointerLeaveEvent>(OnCrouchLeave);
                _crouchButton.UnregisterCallback<PointerCaptureOutEvent>(OnCrouchCaptureOut);
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

        private static void OnFireballDown(PointerDownEvent evt) => TouchInput.QueueFireball();

        // ------------------------------------------------------------------ agacharse (mantenido)

        private static void OnCrouchDown(PointerDownEvent evt) => TouchInput.Crouch = true;

        private static void OnCrouchUp(PointerUpEvent evt) => TouchInput.Crouch = false;

        private static void OnCrouchLeave(PointerLeaveEvent evt) => TouchInput.Crouch = false;

        private static void OnCrouchCaptureOut(PointerCaptureOutEvent evt) => TouchInput.Crouch = false;

        // ------------------------------------------------------------------ pausa

        private void OnPausedChanged(bool paused)
        {
            if (_inner != null)
                _inner.style.display = paused ? DisplayStyle.None : DisplayStyle.Flex;

            if (paused) TouchInput.Clear();
        }
    }
}
