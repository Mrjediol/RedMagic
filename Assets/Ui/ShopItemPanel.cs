using System.Collections.Generic;
using RedMagic.Economy;
using RedMagic.Items;
using RedMagic.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Ficha del item que tiene delante el jugador: en la tienda (altar) y en el suelo (<see cref="ShowGroundItem"/>).
    /// Nombre (color de rareza), rareza, efecto, sus sinergias con "ahora → después" (en dorado, con lo que
    /// desbloquea, si cruza un umbral 2/4/6) y abajo las acciones: "comprar · 30 (moneda)" o "equipar" y
    /// "mantén reciclar · +9 (moneda)" con su barra de progreso. Las cantidades van con el icono de la moneda, nunca
    /// con la palabra.
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
        private Label _name, _rarity, _description, _warning;
        private VisualElement _actions, _holdFill;

        // ficha del suelo a la vista: si no cambia nada, sólo se mueve la barra de mantener
        private ItemDefinition _groundItem;
        private int _groundGold = -1;
        private string _groundKeys;
        private bool _built;
        private System.Action _refill; // repinta la ficha visible al cambiar de idioma

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

        private void OnEnable() => Loc.Changed += OnLanguageChanged;
        private void OnDisable() => Loc.Changed -= OnLanguageChanged;

        private void OnLanguageChanged()
        {
            if (_owner != null) _refill?.Invoke();
        }

        // ------------------------------------------------------------------ API

        public static void Show(object owner, ShopStockEntry entry)
        {
            if (_instance == null || entry.Item == null || !_instance.EnsureBuilt()) return;
            _owner = owner;
            _instance.Fill(entry);
            _instance._refill = () => _instance.Fill(entry);
            _instance._card.style.display = DisplayStyle.Flex;
        }

        /// <summary>La misma ficha para el altar del reroll: qué hace, cuántos quedan y la tecla.</summary>
        public static void ShowReroll(object owner, int rerolls, int slots)
        {
            if (_instance == null || !_instance.EnsureBuilt()) return;
            _owner = owner;
            _instance.FillReroll(rerolls, slots);
            _instance._refill = () => _instance.FillReroll(rerolls, slots);
            _instance._card.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Ficha de un item del suelo: equipar y mantener para reciclar por <paramref name="recycleGold"/>, con la barra
        /// de mantener a <paramref name="holdFraction"/> (0..1). Se puede llamar cada frame: sólo repinta lo que cambia.
        /// </summary>
        public static void ShowGroundItem(object owner, ItemDefinition item, int recycleGold, float holdFraction,
                                          string equipText, string recycleText)
        {
            if (_instance == null || item == null || !_instance.EnsureBuilt()) return;
            var panel = _instance;
            string keys = equipText + "|" + recycleText;
            if (_owner != owner || panel._groundItem != item || panel._groundGold != recycleGold || panel._groundKeys != keys)
            {
                _owner = owner;
                panel._groundItem = item;
                panel._groundGold = recycleGold;
                panel._groundKeys = keys;
                panel.FillGround(item, recycleGold, equipText, recycleText);
                panel._refill = () => panel.FillGround(item, recycleGold, equipText, recycleText);
                panel._card.style.display = DisplayStyle.Flex;
            }
            if (panel._holdFill != null) panel._holdFill.style.width = Length.Percent(Mathf.Clamp01(holdFraction) * 100f);
        }

        public static void Hide(object owner)
        {
            if (_instance == null || _owner != owner || !_instance._built) return;
            _owner = null;
            _instance._groundItem = null;
            _instance._card.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ contenido

        private void Fill(ShopStockEntry entry)
        {
            var item = entry.Item;
            var inventory = FillItem(item);
            _groundItem = null;

            int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(Currency.Gold) : 0;
            bool full = item is FreePoolItemDefinition && inventory != null && inventory.FreeSlotsFull;

            SetWarning(full ? Loc.Get("ground.full") : gold < entry.Price ? Loc.Get("shop.not_enough_gold", gold, entry.Price) : "");
            _actions.Clear();
            _holdFill = null;
            _actions.Add(Action(Loc.Get("shop.buy_hint"), entry.Price.ToString(), gold >= entry.Price ? Gold : TooDear, false));
        }

        private void FillGround(ItemDefinition item, int recycleGold, string equipText, string recycleText)
        {
            var inventory = FillItem(item);
            bool full = item is FreePoolItemDefinition && inventory != null && inventory.FreeSlotsFull;
            SetWarning(full ? Loc.Get("ground.full") : "");

            _actions.Clear();
            _actions.Add(Action(equipText, null, Gold, false));
            _actions.Add(Action(recycleText, "+" + recycleGold, Gold, true));
        }

        private void SetWarning(string text)
        {
            _warning.text = text;
            _warning.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Nombre, rareza, efecto y sinergias ahora → después. Devuelve el inventario (o null).</summary>
        private WeaponInventory FillItem(ItemDefinition item)
        {
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
            return inventory;
        }

        /// <summary>
        /// Una acción de la ficha: el texto (con su tecla) y, si hay cantidad, la cifra con el icono del oro. Con
        /// <paramref name="holdBar"/>, una barra fina debajo que se llena al mantener.
        /// </summary>
        private VisualElement Action(string text, string amount, Color amountColor, bool holdBar)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.flexGrow = 1;
            box.style.flexBasis = 0;
            box.style.marginLeft = 6;
            box.style.marginRight = 6;
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;
            box.style.backgroundColor = new Color(0f, 0f, 0f, 0.28f);
            MenuStyle.SetBorder(box, 1, new Color(MenuStyle.GoldBorder.r, MenuStyle.GoldBorder.g, MenuStyle.GoldBorder.b, 0.45f), 10);

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.Center;
            box.Add(row);

            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 22;
            label.style.color = MenuStyle.Cream;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(label);

            if (amount != null)
            {
                var value = new Label(amount) { pickingMode = PickingMode.Ignore };
                value.style.fontSize = 26;
                value.style.unityFontStyleAndWeight = FontStyle.Bold;
                value.style.color = amountColor;
                value.style.marginLeft = 12;
                row.Add(value);

                var coin = CurrencyManager.Instance != null ? CurrencyManager.Instance.Config?.VisualFor(Currency.Gold)?.icon : null;
                if (coin != null)
                {
                    var icon = new Image { sprite = coin, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    icon.style.width = 30;
                    icon.style.height = 30;
                    icon.style.marginLeft = 4;
                    row.Add(icon);
                }
            }

            if (holdBar)
            {
                var track = new VisualElement { pickingMode = PickingMode.Ignore };
                track.style.height = 6;
                track.style.marginTop = 6;
                track.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
                MenuStyle.SetBorder(track, 0, Color.clear, 3);
                box.Add(track);

                _holdFill = new VisualElement { pickingMode = PickingMode.Ignore };
                _holdFill.style.height = Length.Percent(100);
                _holdFill.style.width = Length.Percent(0);
                _holdFill.style.backgroundColor = Gold;
                MenuStyle.SetBorder(_holdFill, 0, Color.clear, 3);
                track.Add(_holdFill);
            }

            return box;
        }

        private void FillReroll(int rerolls, int slots)
        {
            var accent = ShopFxConfig.Current.rerollAccent;

            _name.text = Loc.Get("shop.reroll.name");
            _name.style.color = accent;
            _rarity.text = Loc.Get("shop.reroll.left", rerolls);
            _rarity.style.color = rerolls > 0 ? accent : TooDear;
            _description.text = Loc.Get("shop.reroll.description", slots);
            _synergies.Clear();

            _groundItem = null;
            SetWarning(rerolls > 0 ? "" : Loc.Get("shop.reroll.none"));
            _actions.Clear();
            _holdFill = null;
            _actions.Add(Action(Loc.Get("shop.reroll.hint"), null, Gold, false));
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
            // Arriba: los items están en el suelo (altares, botín) y abajo la ficha los tapaba.
            root.style.justifyContent = Justify.FlexStart;
            root.style.alignItems = Align.Center;

            _card = new VisualElement { name = "shop-item-panel", pickingMode = PickingMode.Ignore };
            _card.style.width = 640;
            _card.style.marginTop = 24;
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

            _actions = new VisualElement { pickingMode = PickingMode.Ignore };
            _actions.style.flexDirection = FlexDirection.Row;
            _actions.style.marginTop = 8;
            _actions.style.marginLeft = -6;
            _actions.style.marginRight = -6;
            _card.Add(_actions);

            _built = true;
            return true;
        }
    }
}
