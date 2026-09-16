using RedMagic.Audio;
using RedMagic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Menú del espejo del hub. <b>Cascarón vacío a propósito</b> — el contenido real (estadísticas,
    /// cosméticos, o lo que se decida) todavía no está definido; esto sólo prueba que el espejo
    /// puede abrir un menú, con el mismo aspecto y las mismas reglas que los demás menús del hub.
    ///
    /// Mismo patrón self-bootstrapping que <see cref="UpgradeMenuController"/>/
    /// <c>ShopMenuController</c>: auto-creado, persistente, <c>UIDocument</c> construido en código,
    /// <c>PanelSettings</c> propio en Resources, pausa el juego mientras está abierto, se cierra con
    /// Esc / E / botón este del mando.
    ///
    /// TODO: sustituir <see cref="BuildUi"/> por el contenido real cuando se decida qué enseña el
    /// espejo.
    /// </summary>
    [DisallowMultipleComponent]
    public class MirrorMenuController : MonoBehaviour
    {
        public static MirrorMenuController Instance { get; private set; }

        private const string PanelSettingsResourcePath = "MirrorMenuPanelSettings";

        private UIDocument _document;
        private VisualElement _overlay;
        private bool _open;
        private bool _built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[MirrorMenu]").AddComponent<MirrorMenuController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
            if (_document.panelSettings == null)
            {
                Debug.LogWarning($"[MirrorMenu] Falta '{PanelSettingsResourcePath}' en Resources; " +
                                 "el menú del espejo no se podrá abrir.", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_open) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame ||
                                     keyboard.eKey.wasPressedThisFrame))
            {
                Close();
                return;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.buttonEast.wasPressedThisFrame ||
                                    gamepad.startButton.wasPressedThisFrame))
                Close();
        }

        public void Open()
        {
            if (_open) return;
            if (!EnsureBuilt()) return;

            _open = true;
            _overlay.style.display = DisplayStyle.Flex;

            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(true);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;

            _overlay.style.display = DisplayStyle.None;

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ButtonClick");
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(false);
        }

        private bool EnsureBuilt()
        {
            if (_built) return true;
            if (_document == null || _document.panelSettings == null) return false;

            var root = _document.rootVisualElement;
            if (root == null) return false;

            BuildUi(root);
            _built = true;
            return true;
        }

        private void BuildUi(VisualElement root)
        {
            MenuStyle.FillParent(root);

            _overlay = new VisualElement { name = "mirror-overlay" };
            MenuStyle.FillParent(_overlay);
            _overlay.style.backgroundColor = MenuStyle.Backdrop;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var panel = MenuStyle.Panel();
            _overlay.Add(panel);

            var header = MenuStyle.Header();
            panel.Add(header);
            header.Add(MenuStyle.Title("MIRROR"));
            header.Add(MenuStyle.CloseButton(Close));

            // TODO: implementar el contenido real (estadísticas / cosméticos / lo que se decida).
            var placeholder = new Label("Coming soon");
            placeholder.style.fontSize = 24;
            placeholder.style.color = MenuStyle.Cream;
            placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
            placeholder.style.marginTop = 24;
            placeholder.style.marginBottom = 24;
            panel.Add(placeholder);

            panel.Add(MenuStyle.Hint("Esc / E / B to close"));
        }
    }
}
