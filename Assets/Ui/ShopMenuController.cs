using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Economy;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Escaparate de la tienda de mitad de run. Enseña los artículos que le quedan a un
    /// <see cref="ShopInteractable"/> concreto; se pagan con <b>oro</b> y cada uno se puede comprar
    /// una vez: al comprarlo desaparece de la lista.
    ///
    /// Mismo montaje que <see cref="UpgradeMenuController"/>: se auto-crea, es persistente,
    /// construye su UI en código y saca el <see cref="PanelSettings"/> de Resources. Mientras está
    /// abierto el juego queda en pausa vía <see cref="GameStateManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopMenuController : MonoBehaviour
    {
        public static ShopMenuController Instance { get; private set; }

        private const string PanelSettingsResourcePath = "ShopMenuPanelSettings";

        private UIDocument _document;
        private VisualElement _overlay;
        private Label _goldLabel;
        private VisualElement _itemRow;
        private Label _emptyLabel;
        private ShopInteractable _shop;
        private bool _open;
        private bool _built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[ShopMenu]").AddComponent<ShopMenuController>();
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
                Debug.LogWarning($"[ShopMenu] Falta '{PanelSettingsResourcePath}' en Resources; la " +
                                 "tienda no se podrá abrir.", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed -= OnCurrencyChanged;
            Instance = null;
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

        // ------------------------------------------------------------------ abrir / cerrar

        public void Open(ShopInteractable shop)
        {
            if (_open || shop == null) return;
            if (!EnsureBuilt()) return;

            _shop = shop;
            _open = true;
            _overlay.style.display = DisplayStyle.Flex;

            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(true);

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Changed -= OnCurrencyChanged;
                CurrencyManager.Instance.Changed += OnCurrencyChanged;
            }

            RebuildItems();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _shop = null;

            _overlay.style.display = DisplayStyle.None;

            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed -= OnCurrencyChanged;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ButtonClick");
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(false);
        }

        private void OnCurrencyChanged(Currency currency, int amount)
        {
            if (currency == Currency.Gold) RebuildItems();
        }

        // ------------------------------------------------------------------ construcción

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

            _overlay = new VisualElement { name = "shop-overlay" };
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

            header.Add(MenuStyle.Title("SHOP"));

            _goldLabel = MenuStyle.CurrencyLabel();
            header.Add(_goldLabel);

            header.Add(MenuStyle.CloseButton(Close));

            _itemRow = new VisualElement();
            _itemRow.style.flexDirection = FlexDirection.Row;
            _itemRow.style.justifyContent = Justify.Center;
            panel.Add(_itemRow);

            _emptyLabel = new Label("Sold out.");
            _emptyLabel.style.fontSize = MenuStyle.BodyFontSize;
            _emptyLabel.style.color = MenuStyle.Cream;
            _emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _emptyLabel.style.paddingTop = 40;
            _emptyLabel.style.paddingBottom = 40;
            _emptyLabel.style.display = DisplayStyle.None;
            panel.Add(_emptyLabel);

            panel.Add(MenuStyle.Hint("Esc / E / B to close"));
        }

        private void RebuildItems()
        {
            if (_itemRow == null) return;

            int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(Currency.Gold) : 0;
            if (_goldLabel != null) _goldLabel.text = $"Gold: {gold}";

            _itemRow.Clear();

            var stock = _shop != null ? _shop.Stock : null;
            bool empty = stock == null || stock.Count == 0;

            _emptyLabel.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            _itemRow.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
            if (empty) return;

            // Se copia la lista: comprar muta el stock de la tienda y no se puede iterar sobre él.
            foreach (var item in new List<ShopConfig.Item>(stock))
                _itemRow.Add(BuildCard(item, gold));
        }

        private VisualElement BuildCard(ShopConfig.Item item, int gold)
        {
            bool affordable = gold >= item.cost;

            var card = new Button(() => Buy(item));
            MenuStyle.Card(card);
            card.style.backgroundColor = affordable ? MenuStyle.CellBuyable : MenuStyle.CellBg;
            card.SetEnabled(affordable);
            card.RegisterCallback<PointerEnterEvent>(_ => AudioManager.Instance?.PlaySFX("SFX_ButtonHover"));

            if (item.icon != null)
            {
                var icon = new Image { sprite = item.icon, scaleMode = ScaleMode.ScaleToFit };
                icon.style.width = 64;
                icon.style.height = 64;
                card.Add(icon);
            }

            card.Add(MenuStyle.CardTitle(item.title));
            card.Add(MenuStyle.CardDescription(item.description));

            var price = MenuStyle.CardFooter($"{item.cost} G");
            price.style.color = affordable ? MenuStyle.CostAfford : MenuStyle.CostTooDear;
            card.Add(price);

            return card;
        }

        private void Buy(ShopConfig.Item item)
        {
            if (_shop == null || item == null) return;

            var wallet = CurrencyManager.Instance;
            if (wallet == null || !wallet.TrySpend(Currency.Gold, item.cost)) return;

            // Comprado = fuera del escaparate. TrySpend ya dispara Changed, que repinta, pero se
            // llama explícito por si el precio era 0.
            _shop.MarkSold(item);
            AudioManager.Instance?.PlaySFX("SFX_ButtonClick");
            RebuildItems();
        }
    }
}
