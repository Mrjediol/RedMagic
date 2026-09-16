using RedMagic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Oculta los elementos con la clase USS <c>touch-only</c> mientras hay un mando activo
    /// (joystick virtual, botones de acción en pantalla, etc.) y los vuelve a mostrar en táctil.
    ///
    /// Colócalo en el mismo GameObject que un <see cref="UIDocument"/> y pon la clase
    /// <c>touch-only</c> en los elementos de control táctil de su UXML.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class TouchOnlyUI : MonoBehaviour
    {
        public const string TouchOnlyClass = "touch-only";

        private UIDocument _document;

        private void OnEnable()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            Subscribe();
            Apply();
        }

        private void Start()
        {
            // InputDeviceManager podría no existir aún en OnEnable (orden de arranque): reintentar.
            Subscribe();
            Apply();
        }

        private void OnDisable()
        {
            if (InputDeviceManager.Instance != null)
                InputDeviceManager.Instance.ModeChanged -= OnModeChanged;
        }

        private void Subscribe()
        {
            if (InputDeviceManager.Instance == null) return;
            InputDeviceManager.Instance.ModeChanged -= OnModeChanged;
            InputDeviceManager.Instance.ModeChanged += OnModeChanged;
        }

        private void OnModeChanged(InputMode mode) => Apply();

        private void Apply()
        {
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null) return;

            // Sólo con pantalla táctil de verdad: con teclado y ratón (o mando) estos botones
            // estorban. Ojo, antes esto era "|| Instance == null", que en PC los enseñaba durante
            // el primer frame.
            bool show = InputDeviceManager.TouchActive;

            root.Query(className: TouchOnlyClass).ForEach(e =>
                e.style.display = show ? DisplayStyle.Flex : DisplayStyle.None);
        }
    }
}
