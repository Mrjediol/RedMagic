using RedMagic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// La chuleta de teclas de PC, abajo a la izquierda, siempre a la vista y translúcida para que
    /// no moleste. Es el equivalente de los botones en pantalla del móvil: cada modo de entrada
    /// enseña lo suyo y nada más — táctil los botones (<see cref="TouchOnlyUI"/>), teclado y ratón
    /// esta lista, y con mando no se enseña ninguna de las dos porque los botones no son éstos.
    ///
    /// Se auto-crea y es persistente (DontDestroyOnLoad) igual que <c>CurrencyHud</c> y
    /// <see cref="RedMagic.Hub.InteractionPromptUi"/>, y comparte con ésta su
    /// <c>PanelSettings</c> — no hay que ponerla en ninguna escena.
    ///
    /// Se esconde sola mientras hay un menú abierto (<see cref="GameStateManager.CanPlayerAct"/>):
    /// ahí no se está jugando, cada menú explica sus propias teclas, y en el menú principal
    /// quedaría fuera de sitio.
    ///
    /// <b>Las teclas de esta lista se escriben a mano</b>: salen de
    /// <c>Settings/RedMagicControls.inputactions</c> (Move WASD+flechas, Jump espacio, Attack clic
    /// izquierdo, Dash shift izq.) y de las que leen los scripts directamente (E interactuar,
    /// I items, Esc pausa). Si cambias un binding, cambia también <see cref="Rows"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ControlsLegendHud : MonoBehaviour
    {
        public static ControlsLegendHud Instance { get; private set; }

        private const string PanelSettingsResourcePath = "InteractionPromptPanelSettings";

        /// <summary>Tecla → qué hace. El orden es el de la lista en pantalla.</summary>
        private static readonly (string Key, string Action)[] Rows =
        {
            ("WASD", "Moverse"),
            ("Espacio", "Saltar / doble salto"),
            ("Shift", "Dash"),
            ("Clic izq.", "Disparar"),
            ("E", "Interactuar"),
            ("I", "Items"),
            ("Esc", "Opciones"),
        };

        private UIDocument _document;
        private VisualElement _panel;
        private bool _built;
        private bool _visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[ControlsLegendHud]").AddComponent<ControlsLegendHud>();
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
                Debug.LogWarning($"[ControlsLegendHud] Falta '{PanelSettingsResourcePath}' en " +
                                 "Resources; la lista de controles no se dibujará.", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_built) TryBuild();
            if (!_built) return;

            // Dos condiciones, las dos pueden cambiar en cualquier momento: que se esté jugando con
            // teclado y ratón, y que no haya un menú delante.
            bool show = InputDeviceManager.KeyboardMouseActive && GameStateManager.CanPlayerAct;
            if (show == _visible) return;

            _visible = show;
            _panel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void TryBuild()
        {
            if (_document == null || _document.panelSettings == null) return;

            var root = _document.rootVisualElement;
            if (root == null) return;

            BuildUi(root);
            _built = true;
        }

        // ------------------------------------------------------------------ construcción de la UI

        private void BuildUi(VisualElement root)
        {
            root.style.position = Position.Absolute;
            root.style.top = 0;
            root.style.left = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            root.pickingMode = PickingMode.Ignore;

            _panel = new VisualElement { name = "controls-legend", pickingMode = PickingMode.Ignore };
            _panel.style.display = DisplayStyle.None;
            _panel.style.position = Position.Absolute;
            _panel.style.left = 18;
            _panel.style.bottom = 16;
            _panel.style.flexDirection = FlexDirection.Column;
            _panel.style.paddingLeft = 14;
            _panel.style.paddingRight = 16;
            _panel.style.paddingTop = 10;
            _panel.style.paddingBottom = 10;
            _panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.3f);
            _panel.style.borderTopLeftRadius = 10;
            _panel.style.borderTopRightRadius = 10;
            _panel.style.borderBottomLeftRadius = 10;
            _panel.style.borderBottomRightRadius = 10;

            foreach (var (key, action) in Rows) _panel.Add(Row(key, action));

            root.Add(_panel);
        }

        private static VisualElement Row(string key, string action)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2;
            row.style.marginBottom = 2;

            var keyLabel = new Label(key) { pickingMode = PickingMode.Ignore };
            keyLabel.style.width = 76;
            keyLabel.style.flexShrink = 0;
            keyLabel.style.fontSize = 15;
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            keyLabel.style.color = new Color(0.95f, 0.9f, 0.78f, 0.92f);
            keyLabel.style.backgroundColor = new Color(1f, 1f, 1f, 0.09f);
            keyLabel.style.paddingTop = 2;
            keyLabel.style.paddingBottom = 2;
            keyLabel.style.borderTopLeftRadius = 5;
            keyLabel.style.borderTopRightRadius = 5;
            keyLabel.style.borderBottomLeftRadius = 5;
            keyLabel.style.borderBottomRightRadius = 5;
            row.Add(keyLabel);

            var actionLabel = new Label(action) { pickingMode = PickingMode.Ignore };
            actionLabel.style.marginLeft = 10;
            actionLabel.style.fontSize = 15;
            actionLabel.style.color = new Color(0.92f, 0.88f, 0.78f, 0.62f);
            row.Add(actionLabel);

            return row;
        }
    }
}
