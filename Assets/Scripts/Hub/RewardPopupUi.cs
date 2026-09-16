using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.Hub
{
    /// <summary>
    /// Ventanita de "has conseguido X": icono + nombre, unos segundos y se desvanece sola.
    ///
    /// No existía ningún popup de este tipo en el proyecto — el cofre sólo dejaba un
    /// <c>Debug.Log</c> y un destello de color (<c>AbilityFx.Flash</c>) al dar el arma. Se
    /// construye aquí, autocreado y persistente igual que <c>CurrencyHud</c>/
    /// <see cref="InteractionPromptUi"/>, para que el cofre lo use ahora y el armario (sus 3
    /// items, cuando exista esa lógica) lo reutilice después sin escribir una segunda ventana.
    /// </summary>
    [DisallowMultipleComponent]
    public class RewardPopupUi : MonoBehaviour
    {
        public static RewardPopupUi Instance { get; private set; }

        private const string PanelSettingsResourcePath = "InteractionPromptPanelSettings";

        [Tooltip("Segundos que se queda visible antes de desvanecerse.")]
        [Min(0.1f)]
        [SerializeField] private float holdSeconds = 2f;

        [Tooltip("Segundos que tarda en desvanecerse tras holdSeconds.")]
        [Min(0.05f)]
        [SerializeField] private float fadeSeconds = 0.4f;

        private UIDocument _document;
        private VisualElement _card;
        private Image _icon;
        private Label _title;
        private Label _name;
        private bool _built;
        private Coroutine _hideRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[RewardPopupUi]").AddComponent<RewardPopupUi>();
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

        /// <summary>Muestra "[título]: [nombre]" con el icono dado, un momento, y se desvanece sola.</summary>
        public static void Show(Sprite icon, string title, string itemName, Color accent)
        {
            if (Instance == null) return;
            Instance.ShowInternal(icon, title, itemName, accent);
        }

        private void ShowInternal(Sprite icon, string title, string itemName, Color accent)
        {
            if (!_built) return;

            if (_hideRoutine != null) StopCoroutine(_hideRoutine);

            _icon.sprite = icon;
            _icon.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
            _title.text = title;
            _name.text = itemName;
            _card.style.borderTopColor = accent;
            _card.style.borderBottomColor = accent;
            _card.style.borderLeftColor = accent;
            _card.style.borderRightColor = accent;

            _card.style.opacity = 1f;
            _card.style.display = DisplayStyle.Flex;

            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(holdSeconds);

            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                _card.style.opacity = 1f - Mathf.Clamp01(t / fadeSeconds);
                yield return null;
            }

            _card.style.display = DisplayStyle.None;
            _hideRoutine = null;
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

            _card = new VisualElement { name = "reward-popup" };
            _card.pickingMode = PickingMode.Ignore;
            _card.style.display = DisplayStyle.None;
            _card.style.position = Position.Absolute;
            _card.style.top = 90;
            _card.style.left = 0;
            _card.style.right = 0;
            _card.style.flexDirection = FlexDirection.Row;
            _card.style.alignItems = Align.Center;
            _card.style.alignSelf = Align.Center;
            _card.style.marginLeft = new StyleLength(new Length(35, LengthUnit.Percent));
            _card.style.marginRight = new StyleLength(new Length(35, LengthUnit.Percent));
            _card.style.paddingTop = 10;
            _card.style.paddingBottom = 10;
            _card.style.paddingLeft = 16;
            _card.style.paddingRight = 16;
            _card.style.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            _card.style.borderTopWidth = 2;
            _card.style.borderBottomWidth = 2;
            _card.style.borderLeftWidth = 2;
            _card.style.borderRightWidth = 2;
            _card.style.borderTopLeftRadius = 10;
            _card.style.borderTopRightRadius = 10;
            _card.style.borderBottomLeftRadius = 10;
            _card.style.borderBottomRightRadius = 10;

            _icon = new Image { name = "reward-popup-icon" };
            _icon.style.width = 48;
            _icon.style.height = 48;
            _icon.style.marginRight = 14;
            _card.Add(_icon);

            var textColumn = new VisualElement();
            textColumn.style.flexDirection = FlexDirection.Column;

            _title = new Label { name = "reward-popup-title" };
            _title.style.fontSize = 18;
            _title.style.color = new Color(0.85f, 0.85f, 0.85f);
            textColumn.Add(_title);

            _name = new Label { name = "reward-popup-name" };
            _name.style.fontSize = 26;
            _name.style.unityFontStyleAndWeight = FontStyle.Bold;
            _name.style.color = Color.white;
            textColumn.Add(_name);

            _card.Add(textColumn);
            root.Add(_card);
        }
    }
}
