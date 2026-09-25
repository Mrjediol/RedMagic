using System.Collections.Generic;
using RedMagic.Economy;
using RedMagic.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Ficha del item del altar que tiene delante el jugador en la tienda: nombre (color de rareza),
    /// rareza, efecto, sus sinergias con "ahora → tras comprar" (en dorado, con lo que desbloquea, si
    /// la compra cruza un umbral 2/4/6), y "Pulsa [Interactuar] para comprar".
    ///
    /// No pausa: es un cartel, no un menú. <see cref="Show"/>/<see cref="Hide"/> estáticos con dueño,
    /// como <c>InteractionPromptUi</c>. Se auto-crea; PanelSettings propio en Resources.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopItemPanel : MonoBehaviour
    {
        private const string PanelSettingsResourcePath = "ShopItemPanelSettings";
        private static readonly Color Gold = new(1f, 0.82f, 0.32f);
        private static readonly Color TooDear = new(1f, 0.45f, 0.4f);

        private static ShopItemPanel _instance;
        private static object _owner;

        private UIDocument _document;
        private VisualElement _card, _synergies;
        private Label _name, _rarity, _description, _hint, _warning;
        private bool _built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _owner = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ShopItemPanel]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ShopItemPanel>();
        }

        private void Awake()
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
            if (_document.panelSettings == null)
                Debug.LogWarning("[ShopItemPanel] Falta '" + PanelSettingsResourcePath + "' en Resources.", this);
        }

        // ------------------------------------------------------------------ API

        public static void Show(object owner, ShopStockEntry entry)
        {
            if (_instance == null || entry.Item == null || !_instance.EnsureBuilt()) return;
            _owner = owner;
            _instance.Fill(entry);
            _instance._card.style.display = DisplayStyle.Flex;
        }

        public static void Hide(object owner)
        {
            if (_instance == null || _owner != owner || !_instance._built) return;
            _owner = null;
            _instance._card.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ contenido

        private void Fill(ShopStockEntry entry)
        {
            var item = entry.Item;
            var rarityColor = ItemRarities.ColorOf(item.Rarity);

            _name.text = item.DisplayName;
            _name.style.color = rarityColor;
            _rarity.text = ItemRarities.DisplayName(item.Rarity).ToUpperInvariant();
            _rarity.style.color = rarityColor;
            _description.text = item.Description;

            _synergies.Clear();
            var inventory = WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory : null;
            var tracker = WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Synergy : null;
            var replaced = inventory == null ? null : item switch
            {
                ElementModifier => inventory.Element,
                TrajectoryModifier => inventory.Trajectory,
                ShapeModifier => inventory.Shape,
                _ => (ItemDefinition)null,
            };

            var seen = new HashSet<BuildTag>();
            foreach (var tag in item.Tags)
            {
                if (!seen.Add(tag)) continue;

                int now = tracker != null ? tracker.PointsFor(tag) : 0;
                int after = now + Count(item.Tags, tag) - (replaced != null ? Count(replaced.Tags, tag) : 0);
                _synergies.Add(SynergyLine(tag, now, after));
            }

            int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(Currency.Gold) : 0;
            bool full = item is FreePoolItemDefinition && inventory != null && inventory.FreeSlotsFull;

            _warning.text = full ? "Huecos libres llenos"
                          : gold < entry.Price ? $"Oro insuficiente ({gold} / {entry.Price})"
                          : "";
            _warning.style.display = _warning.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _hint.text = $"Pulsa [Interactuar] para comprar · {entry.Price} oro";
        }

        private static VisualElement SynergyLine(BuildTag tag, int now, int after)
        {
            int cap = BuildTags.SynergyCap;
            int shownNow = Mathf.Min(now, cap), shownAfter = Mathf.Clamp(after, 0, cap);

            // Umbral nuevo = el más alto que la compra cruza (ahora < t <= después).
            int crossed = 0;
            foreach (int t in BuildTags.Thresholds)
                if (shownNow < t && shownAfter >= t) crossed = t;

            var box = new VisualElement();
            box.style.marginTop = 6;

            var line = new Label($"{BuildTags.DisplayName(tag)}  {shownNow} → {shownAfter}");
            line.style.fontSize = 24;
            line.style.unityFontStyleAndWeight = crossed > 0 ? FontStyle.Bold : FontStyle.Normal;
            line.style.color = crossed > 0 ? Gold : MenuStyle.Cream;
            box.Add(line);

            if (crossed > 0)
            {
                int tier = System.Array.IndexOf(BuildTags.Thresholds, crossed) + 1;
                string bonus = SynergyConfig.Instance != null ? SynergyConfig.Instance.Tier(tag, tier) : null;

                var unlock = new Label($"★ {BuildTags.DisplayName(tag)} {crossed}" +
                                       (string.IsNullOrWhiteSpace(bonus) ? "" : $": {bonus}"));
                unlock.style.fontSize = 20;
                unlock.style.color = Gold;
                unlock.style.whiteSpace = WhiteSpace.Normal;
                unlock.style.marginLeft = 14;
                box.Add(unlock);
            }

            return box;
        }

        private static int Count(IReadOnlyList<BuildTag> tags, BuildTag tag)
        {
            int n = 0;
            foreach (var t in tags) if (t == tag) n++;
            return n;
        }

        // ------------------------------------------------------------------ construcción

        private bool EnsureBuilt()
        {
            if (_built) return true;
            if (_document == null || _document.panelSettings == null) return false;

            var root = _document.rootVisualElement;
            if (root == null) return false;

            MenuStyle.FillParent(root);
            root.pickingMode = PickingMode.Ignore;
            root.style.justifyContent = Justify.FlexEnd;
            root.style.alignItems = Align.Center;

            _card = new VisualElement { name = "shop-item-panel", pickingMode = PickingMode.Ignore };
            _card.style.width = 640;
            _card.style.marginBottom = 36;
            _card.style.paddingTop = 18;
            _card.style.paddingBottom = 18;
            _card.style.paddingLeft = 24;
            _card.style.paddingRight = 24;
            _card.style.backgroundColor = MenuStyle.PanelBg;
            MenuStyle.SetBorder(_card, 2, MenuStyle.GoldBorder, 12);
            _card.style.display = DisplayStyle.None;
            root.Add(_card);

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            _card.Add(header);

            _name = new Label { pickingMode = PickingMode.Ignore };
            _name.style.fontSize = 34;
            _name.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(_name);

            _rarity = new Label { pickingMode = PickingMode.Ignore };
            _rarity.style.fontSize = 20;
            _rarity.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(_rarity);

            _description = new Label { pickingMode = PickingMode.Ignore };
            _description.style.fontSize = 22;
            _description.style.color = MenuStyle.Cream;
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginTop = 8;
            _description.style.marginBottom = 8;
            _card.Add(_description);

            _synergies = new VisualElement { pickingMode = PickingMode.Ignore };
            _synergies.style.marginBottom = 10;
            _card.Add(_synergies);

            _warning = new Label { pickingMode = PickingMode.Ignore };
            _warning.style.fontSize = 20;
            _warning.style.color = TooDear;
            _card.Add(_warning);

            _hint = new Label { pickingMode = PickingMode.Ignore };
            _hint.style.fontSize = 22;
            _hint.style.color = MenuStyle.Cream;
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hint.style.marginTop = 6;
            _card.Add(_hint);

            _built = true;
            return true;
        }
    }
}
