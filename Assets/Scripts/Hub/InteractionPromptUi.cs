using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.Hub
{
    /// <summary>
    /// El cartel de "Pulsa [tecla] para..." compartido por TODOS los interactuables del hub
    /// (cofre, armario, atril del libro, yunque, espejo — y cualquiera que se añada después).
    ///
    /// Antes de esto, cada interactuable (<c>AbilityChest</c>, <c>CauldronInteractable</c>,
    /// <c>TombInteractable</c>) sólo podía encender/apagar un <c>GameObject prompt</c> fijo
    /// arrastrado a mano en el Inspector — un cartel de arte con el texto ya dibujado, sin forma de
    /// cambiar la frase en marcha. La mecánica de dos pasos que pide el cofre y el armario ("Pulsa
    /// para abrir" → "Pulsa para recoger") necesita texto que cambie en el mismo objeto, así que
    /// este HUD sustituye a ese patrón por uno con <see cref="Show"/>/<see cref="Hide"/> con texto
    /// libre. Sigue exactamente la forma de <c>CurrencyHud</c>: auto-creado, persistente
    /// (DontDestroyOnLoad), <c>UIDocument</c> construido en código, <c>PanelSettings</c> propio en
    /// Resources.
    ///
    /// Un único cartel global en vez de uno por objeto es también lo correcto para el juego: sólo
    /// tiene sentido mostrar un aviso de interacción a la vez, así que no hay que preocuparse de que
    /// dos interactuables cercanos compitan por el mismo hueco de pantalla — el último que llama a
    /// <see cref="Show"/> gana, y cualquiera que llame a <see cref="Hide"/> lo apaga (ver el gotcha
    /// de <see cref="Hide(MonoBehaviour)"/> más abajo para el caso de dos objetos solapados).
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionPromptUi : MonoBehaviour
    {
        public static InteractionPromptUi Instance { get; private set; }

        private const string PanelSettingsResourcePath = "InteractionPromptPanelSettings";

        private UIDocument _document;
        private Label _label;
        private bool _built;

        // Quién pidió el cartel visible ahora mismo. Con dos objetos de rango solapado, el que
        // sale de rango primero no debe poder apagar el cartel del que sigue dentro — por eso
        // Hide(caller) sólo apaga si el que pide ocultar es quien lo encendió.
        private Object _owner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[InteractionPromptUi]").AddComponent<InteractionPromptUi>();
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
                Debug.LogWarning($"[InteractionPromptUi] Falta '{PanelSettingsResourcePath}' en " +
                                 "Resources; los carteles de interacción no se dibujarán.", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_built) TryBuild();
        }

        private void TryBuild()
        {
            if (_document == null || _document.panelSettings == null) return;

            var root = _document.rootVisualElement;
            if (root == null) return;

            BuildUi(root);
            _built = true;
        }

        /// <summary>Muestra el cartel con este texto. <paramref name="caller"/> es quien lo pide
        /// (normalmente <c>this</c> desde el interactuable) — hace falta para que <see cref="Hide"/>
        /// sepa si le corresponde apagarlo.</summary>
        public static void Show(Object caller, string text)
        {
            if (Instance == null || string.IsNullOrEmpty(text)) return;

            Instance._owner = caller;
            if (Instance._label != null)
            {
                Instance._label.text = text;
                Instance._label.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>Oculta el cartel, pero sólo si sigue siendo <paramref name="caller"/> quien lo
        /// pidió — así un objeto que ya no está en rango no le quita el cartel a otro que sí lo
        /// está.</summary>
        public static void Hide(Object caller)
        {
            if (Instance == null || Instance._owner != caller) return;

            Instance._owner = null;
            if (Instance._label != null) Instance._label.style.display = DisplayStyle.None;
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

            // Grande y legible por defecto (regla del proyecto para toda UI de juego), en la
            // franja inferior donde no tapa ni la barra de vida del jefe ni el HUD de monedas.
            _label = new Label
            {
                name = "interaction-prompt",
                pickingMode = PickingMode.Ignore
            };
            _label.style.display = DisplayStyle.None;
            _label.style.position = Position.Absolute;
            _label.style.left = 0;
            _label.style.right = 0;
            _label.style.bottom = 90;
            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.fontSize = 30;
            _label.style.color = Color.white;
            _label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _label.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            _label.style.paddingTop = 10;
            _label.style.paddingBottom = 10;
            _label.style.paddingLeft = 24;
            _label.style.paddingRight = 24;
            _label.style.marginLeft = new StyleLength(new Length(30, LengthUnit.Percent));
            _label.style.marginRight = new StyleLength(new Length(30, LengthUnit.Percent));
            _label.style.borderTopLeftRadius = 10;
            _label.style.borderTopRightRadius = 10;
            _label.style.borderBottomLeftRadius = 10;
            _label.style.borderBottomRightRadius = 10;

            root.Add(_label);
        }
    }
}
