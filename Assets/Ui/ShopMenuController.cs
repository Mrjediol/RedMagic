using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Economy;
using RedMagic.Items;
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
    /// <b>Comprar equipa.</b> El item entra en el <see cref="WeaponLoadout"/> de la run siguiendo la
    /// lógica del <see cref="WeaponInventory"/>:
    /// <list type="bullet">
    /// <item>Elemento / Trayectoria / Forma: van a su slot dedicado; si ya había uno, lo sustituyen
    /// ("último equipado gana").</item>
    /// <item>Item de pool libre: entra en el primer slot libre vacío. Si los 6 están llenos, se
    /// abre un selector para que el jugador elija <b>cuál sustituir</b> antes de cobrar.</item>
    /// </list>
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

        private VisualElement _replaceOverlay;
        private VisualElement _replaceRow;
        private Label _replaceHeading;
        private FreePoolItemDefinition _pendingIncoming;
        private ShopStockEntry _pendingEntry;

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
            bool cancel = keyboard != null && (keyboard.escapeKey.wasPressedThisFrame ||
                                               keyboard.eKey.wasPressedThisFrame);
            var gamepad = Gamepad.current;
            cancel |= gamepad != null && (gamepad.buttonEast.wasPressedThisFrame ||
                                          gamepad.startButton.wasPressedThisFrame);

            if (!cancel) return;

            // El selector de "cuál sustituir" se cierra primero y deja la tienda abierta.
            if (_replaceOverlay != null && _replaceOverlay.style.display == DisplayStyle.Flex)
                CloseReplacePicker();
            else
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

            CloseReplacePicker();
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
            _itemRow.style.flexWrap = Wrap.Wrap;
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

            BuildReplaceOverlay(root);
        }

        private void BuildReplaceOverlay(VisualElement root)
        {
            _replaceOverlay = new VisualElement { name = "shop-replace-overlay" };
            MenuStyle.FillParent(_replaceOverlay);
            _replaceOverlay.style.backgroundColor = MenuStyle.Backdrop;
            _replaceOverlay.style.alignItems = Align.Center;
            _replaceOverlay.style.justifyContent = Justify.Center;
            _replaceOverlay.style.display = DisplayStyle.None;
            root.Add(_replaceOverlay);

            var panel = MenuStyle.Panel();
            _replaceOverlay.Add(panel);

            var header = MenuStyle.Header();
            panel.Add(header);
            header.Add(MenuStyle.Title("REPLACE"));
            header.Add(MenuStyle.CloseButton(CloseReplacePicker));

            _replaceHeading = new Label();
            _replaceHeading.style.fontSize = MenuStyle.BodyFontSize;
            _replaceHeading.style.color = MenuStyle.Cream;
            _replaceHeading.style.unityTextAlign = TextAnchor.MiddleCenter;
            _replaceHeading.style.whiteSpace = WhiteSpace.Normal;
            _replaceHeading.style.marginBottom = 14;
            panel.Add(_replaceHeading);

            _replaceRow = new VisualElement();
            _replaceRow.style.flexDirection = FlexDirection.Row;
            _replaceRow.style.flexWrap = Wrap.Wrap;
            _replaceRow.style.justifyContent = Justify.Center;
            panel.Add(_replaceRow);

            panel.Add(MenuStyle.Hint("Elige el item que se descarta · Esc para cancelar"));
        }

        // ------------------------------------------------------------------ escaparate

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
            foreach (var entry in new List<ShopStockEntry>(stock))
                _itemRow.Add(BuildCard(entry, gold));
        }

        private VisualElement BuildCard(ShopStockEntry entry, int gold)
        {
            var item = entry.Item;
            bool affordable = gold >= entry.Cost;

            var card = new Button(() => Buy(entry));
            MenuStyle.Card(card);
            card.style.backgroundColor = affordable ? MenuStyle.CellBuyable : MenuStyle.CellBg;
            card.SetEnabled(affordable);
            card.RegisterCallback<PointerEnterEvent>(_ => AudioManager.Instance?.PlaySFX("SFX_ButtonHover"));

            if (item.Icon != null)
            {
                var icon = new Image { sprite = item.Icon, scaleMode = ScaleMode.ScaleToFit };
                icon.style.width = 56;
                icon.style.height = 56;
                card.Add(icon);
            }

            var slot = new Label(SlotLabel(item));
            slot.style.fontSize = MenuStyle.HintFontSize;
            slot.style.color = new Color(item.Accent.r, item.Accent.g, item.Accent.b, 0.9f);
            slot.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(slot);

            card.Add(MenuStyle.CardTitle(item.DisplayName));
            card.Add(MenuStyle.CardDescription(TagsText(item)));

            var replaces = ReplaceHint(item);
            if (!string.IsNullOrEmpty(replaces))
            {
                var note = MenuStyle.CardDescription(replaces);
                note.style.color = new Color(MenuStyle.Cream.r, MenuStyle.Cream.g, MenuStyle.Cream.b, 0.55f);
                card.Add(note);
            }

            var price = MenuStyle.CardFooter($"{entry.Cost} G");
            price.style.color = affordable ? MenuStyle.CostAfford : MenuStyle.CostTooDear;
            card.Add(price);

            return card;
        }

        // ------------------------------------------------------------------ compra + equipar

        private void Buy(ShopStockEntry entry)
        {
            if (_shop == null || entry == null || entry.Item == null) return;

            var loadout = WeaponLoadout.Instance;
            if (loadout == null)
            {
                Debug.LogWarning("[ShopMenu] No hay WeaponLoadout: no se puede equipar lo comprado.", this);
                return;
            }

            var inventory = loadout.Inventory;

            // Pool libre con los 6 slots llenos: el jugador elige cuál descarta antes de cobrar.
            if (entry.Item is FreePoolItemDefinition free && inventory.FreeSlotsFull)
            {
                OpenReplacePicker(free, entry);
                return;
            }

            var wallet = CurrencyManager.Instance;
            if (wallet == null || !wallet.TrySpend(Currency.Gold, entry.Cost)) return;

            // TryEquip reparte por tipo: modificador → su slot dedicado (sustituye al anterior),
            // item de pool → primer slot libre vacío.
            inventory.TryEquip(entry.Item);

            _shop.MarkSold(entry);
            AudioManager.Instance?.PlaySFX("SFX_ButtonClick");
            RebuildItems();
        }

        private void OpenReplacePicker(FreePoolItemDefinition incoming, ShopStockEntry entry)
        {
            _pendingIncoming = incoming;
            _pendingEntry = entry;

            _replaceHeading.text = $"Los 6 slots libres están llenos. ¿Qué descartas por " +
                                   $"\"{incoming.DisplayName}\"?  ({entry.Cost} G)";

            _replaceRow.Clear();
            var inventory = WeaponLoadout.Instance.Inventory;

            for (int i = 0; i < WeaponInventory.FreeSlotCount; i++)
            {
                int index = i;
                var current = inventory.GetFree(index);

                var card = new Button(() => ConfirmReplace(index));
                MenuStyle.Card(card);
                card.style.backgroundColor = MenuStyle.CellBg;
                card.RegisterCallback<PointerEnterEvent>(_ => AudioManager.Instance?.PlaySFX("SFX_ButtonHover"));

                card.Add(MenuStyle.CardTitle($"Slot {index + 1}"));
                if (current != null)
                {
                    card.Add(MenuStyle.CardDescription(current.DisplayName));
                    card.Add(MenuStyle.CardDescription(TagsText(current)));
                }
                else
                {
                    card.Add(MenuStyle.CardDescription("(vacío)"));
                }

                _replaceRow.Add(card);
            }

            _replaceOverlay.style.display = DisplayStyle.Flex;
        }

        private void ConfirmReplace(int slotIndex)
        {
            var loadout = WeaponLoadout.Instance;
            var wallet = CurrencyManager.Instance;

            if (loadout == null || wallet == null || _pendingIncoming == null || _pendingEntry == null)
            {
                CloseReplacePicker();
                return;
            }

            if (!wallet.TrySpend(Currency.Gold, _pendingEntry.Cost))
            {
                CloseReplacePicker();
                return;
            }

            loadout.Inventory.EquipFree(slotIndex, _pendingIncoming);
            _shop?.MarkSold(_pendingEntry);
            AudioManager.Instance?.PlaySFX("SFX_ButtonClick");

            CloseReplacePicker();
            RebuildItems();
        }

        private void CloseReplacePicker()
        {
            _pendingIncoming = null;
            _pendingEntry = null;
            if (_replaceOverlay != null) _replaceOverlay.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ texto

        private static string SlotLabel(ItemDefinition item) => item switch
        {
            ElementModifier => "ELEMENTO",
            TrajectoryModifier => "TRAYECTORIA",
            ShapeModifier => "FORMA",
            _ => "POOL LIBRE",
        };

        private static string TagsText(ItemDefinition item)
        {
            var tags = item.Tags;
            if (tags.Count == 0) return "—";

            var parts = new List<string>(tags.Count);
            for (int i = 0; i < tags.Count; i++) parts.Add(BuildTags.DisplayName(tags[i]));
            return string.Join(" · ", parts);
        }

        /// <summary>Qué sustituye este item si su slot dedicado ya está ocupado (nota en la carta).</summary>
        private static string ReplaceHint(ItemDefinition item)
        {
            var loadout = WeaponLoadout.Instance;
            if (loadout == null) return null;
            var inv = loadout.Inventory;

            ItemDefinition occupied = item switch
            {
                ElementModifier when inv.Element != null => inv.Element,
                TrajectoryModifier when inv.Trajectory != null => inv.Trajectory,
                ShapeModifier when inv.Shape != null => inv.Shape,
                _ => null,
            };

            return occupied != null ? $"reemplaza {occupied.DisplayName}" : null;
        }
    }
}
