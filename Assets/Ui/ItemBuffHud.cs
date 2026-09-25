using System.Collections.Generic;
using System.Linq;
using RedMagic.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Chapas de efectos de item en el HUD (arriba a la derecha, bajo las monedas): el contador del
    /// Yelmo (0/5 … ¡LISTO!), el "×3" del Bastón cargado, y lo que cualquier efecto futuro publique
    /// en <see cref="BuffIndicators"/>. Una chapa encendida (<c>Highlight</c>) late con el color de
    /// acento del efecto.
    ///
    /// Se auto-crea y es persistente, como <see cref="CurrencyHud"/>, y comparte su
    /// <c>CurrencyHudPanelSettings</c> (mismo orden de dibujo, bajo los menús). Reconstruye las
    /// chapas sólo cuando cambia cuántas hay (<see cref="BuffIndicators.Version"/>); el texto y el
    /// brillo se refrescan cada frame, que con dos o tres chapas no cuesta nada.
    /// </summary>
    [DisallowMultipleComponent]
    public class ItemBuffHud : MonoBehaviour
    {
        public static ItemBuffHud Instance { get; private set; }

        private const string PanelSettingsResourcePath = "CurrencyHudPanelSettings";

        // Grande y legible: se juega en móvil.
        private const float IconSize = 44f;
        private const float FontSize = 22f;

        private UIDocument _document;
        private VisualElement _row;
        private bool _built;
        private int _version = -1;

        private readonly List<(BuffIndicators.Indicator data, VisualElement chip, Image icon, Label text)> _chips = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[ItemBuffHud]").AddComponent<ItemBuffHud>();
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
                Debug.LogWarning($"[ItemBuffHud] Falta '{PanelSettingsResourcePath}' en Resources; " +
                                 "las chapas de items no se dibujarán.", this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_built) TryBuild();
            if (!_built) return;

            bool show = !MainMenuController.IsOpen && BuffIndicators.All.Count > 0;
            _row.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            if (_version != BuffIndicators.Version) Rebuild();
            Refresh();
        }

        private void TryBuild()
        {
            if (_document == null || _document.panelSettings == null) return;

            var root = _document.rootVisualElement;
            if (root == null) return;

            root.pickingMode = PickingMode.Ignore;

            _row = new VisualElement { name = "item-buff-row", pickingMode = PickingMode.Ignore };
            _row.style.position = Position.Absolute;
            _row.style.top = 58;
            _row.style.right = 16;
            _row.style.flexDirection = FlexDirection.Row;
            _row.style.alignItems = Align.Center;
            root.Add(_row);

            _built = true;
        }

        private void Rebuild()
        {
            _version = BuffIndicators.Version;
            _row.Clear();
            _chips.Clear();

            foreach (var data in BuffIndicators.All.Values.OrderBy(d => d.Order))
            {
                var chip = new VisualElement { pickingMode = PickingMode.Ignore };
                chip.style.flexDirection = FlexDirection.Row;
                chip.style.alignItems = Align.Center;
                chip.style.marginLeft = 12;
                chip.style.paddingLeft = 6;
                chip.style.paddingRight = 12;
                chip.style.paddingTop = 4;
                chip.style.paddingBottom = 4;
                SetRadius(chip, 12);
                SetBorderWidth(chip, 2);

                var icon = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                icon.style.width = IconSize;
                icon.style.height = IconSize;
                icon.style.marginRight = 6;
                chip.Add(icon);

                var text = new Label { pickingMode = PickingMode.Ignore };
                text.style.unityFontStyleAndWeight = FontStyle.Bold;
                text.style.fontSize = FontSize;
                text.style.color = Color.white;
                text.style.unityTextOutlineWidth = 1.2f;
                text.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.85f);
                text.style.unityTextAlign = TextAnchor.MiddleLeft;
                chip.Add(text);

                _row.Add(chip);
                _chips.Add((data, chip, icon, text));
            }
        }

        private void Refresh()
        {
            // Latido de las chapas encendidas: ~1.6 pulsos por segundo, en tiempo real (se ve también en pausa).
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);

            foreach (var (data, chip, icon, text) in _chips)
            {
                icon.sprite = data.Icon;
                icon.style.display = data.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;
                text.text = data.Text;

                var accent = data.Accent;
                if (data.Highlight)
                {
                    chip.style.backgroundColor = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.85f);
                    SetBorderColor(chip, new Color(accent.r, accent.g, accent.b, Mathf.Lerp(0.55f, 1f, pulse)));
                    text.style.color = Color.Lerp(Color.white, accent, pulse * 0.6f);
                    icon.tintColor = Color.white;
                }
                else
                {
                    chip.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
                    SetBorderColor(chip, new Color(accent.r, accent.g, accent.b, 0.25f));
                    text.style.color = new Color(0.9f, 0.9f, 0.9f);
                    icon.tintColor = new Color(0.75f, 0.75f, 0.75f);
                }
            }
        }

        private static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        private static void SetBorderWidth(VisualElement e, float w)
        {
            e.style.borderTopWidth = w;
            e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w;
            e.style.borderRightWidth = w;
        }

        private static void SetBorderColor(VisualElement e, Color c)
        {
            e.style.borderTopColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
            e.style.borderRightColor = c;
        }
    }
}
