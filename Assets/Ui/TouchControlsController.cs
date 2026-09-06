using RedMagic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Controles táctiles en pantalla (izquierda / derecha / saltar) para móvil.
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

        private void OnEnable()
        {
            if (_document == null) _document = GetComponent<UIDocument>();

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null) return;

            _inner = _root.Q<VisualElement>("touchControlsInner");
            _leftButton = _root.Q<Button>("leftButton");
            _rightButton = _root.Q<Button>("rightButton");
            _jumpButton = _root.Q<Button>("jumpButton");

            WireHold(_leftButton, -1f);
            WireHold(_rightButton, 1f);

            if (_jumpButton != null)
                _jumpButton.RegisterCallback<PointerDownEvent>(OnJumpDown);

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

        // ------------------------------------------------------------------ pausa

        private void OnPausedChanged(bool paused)
        {
            if (_inner != null)
                _inner.style.display = paused ? DisplayStyle.None : DisplayStyle.Flex;

            if (paused) TouchInput.Clear();
        }
    }
}
