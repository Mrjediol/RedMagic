using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Economy;
using RedMagic.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Menú del espejo del hub: la rejilla 3×3 de pasivas legendarias. Cada casilla es una
    /// <see cref="LegendaryPassive"/> (<see cref="LegendaryPassiveLibrary"/>, barrido de
    /// <c>Resources/LegendaryPassives</c>); bloqueada muestra un "?", desbloqueada su icono (o la
    /// inicial del nombre si no tiene). Tocar una casilla desbloqueada abre el panel de detalle
    /// (nombre, descripción, nivel actual, botón de mejora con coste) sobre la misma rejilla.
    ///
    /// Mismo patrón self-bootstrapping que <see cref="UpgradeMenuController"/>: auto-creado,
    /// persistente, <c>UIDocument</c> construido en código, <c>PanelSettings</c> propio en
    /// Resources, pausa el juego mientras está abierto, se cierra con Esc / E / botón este del
    /// mando. El arte sale de <see cref="MenuSkin"/> vía <see cref="MenuSkinDresser"/> — el mismo
    /// marco de piedra que usa la rejilla de mejoras permanentes, así que "hereda" el estilo sin
    /// necesitar un kit propio. Sin ese asset se dibuja igual, plano.
    ///
    /// Persistencia de qué está desbloqueado y a qué nivel: <see cref="LegendaryPassiveManager"/>
    /// (PlayerPrefs). El coste de mejora se paga en <see cref="Currency.Skull"/>, la misma moneda
    /// de meta-progresión más rara del juego.
    /// </summary>
    [DisallowMultipleComponent]
    public class MirrorMenuController : MonoBehaviour
    {
        public static MirrorMenuController Instance { get; private set; }

        private const string PanelSettingsResourcePath = "MirrorMenuPanelSettings";
        private const int Columns = 3;
        private const int Rows = 3;
        private const float SlotSize = 148f;

        private sealed class Slot
        {
            public Button button;
            public VisualElement iconWell;
            public VisualElement icon;
            public Label question;
            public Label monogram;
        }

        private UIDocument _document;
        private VisualElement _overlay;
        private Label _skullLabel;
        private VisualElement _gridView;
        private VisualElement _detailView;
        private Slot[] _slots;

        // panel de detalle
        private Label _detailTitle;
        private Label _detailDescription;
        private Label _detailDescription2;
        private Label _detailLevel;
        private Label _detailUpgradeDescription;
        private Button _upgradeButton;
        private Label _upgradeFooter;

        private int _selectedId = -1;
        private bool _open;
        private bool _built;

        /// <summary>Arte del kit de menús. Null = aspecto plano de <see cref="MenuStyle"/>, como siempre.</summary>
        private MenuSkin _skin;

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
            if (Instance != this) return;
            Unsubscribe();
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

        public void Open()
        {
            if (_open) return;
            if (!EnsureBuilt()) return;

            _open = true;
            SystemSounds.Play(s => s.mirrorMenu.open);
            _overlay.style.display = DisplayStyle.Flex;
            ShowGrid();

            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(true);

            // Ya se ha visto el aviso de pasiva nueva: apaga el "!" del espejo en el hub.
            LegendaryPassiveManager.Instance?.ClearNewPassiveNotification();

            Subscribe();
            RefreshGrid();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            SystemSounds.Play(s => s.mirrorMenu.close);

            _overlay.style.display = DisplayStyle.None;
            Unsubscribe();

            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(false);
        }

        private void Subscribe()
        {
            if (LegendaryPassiveManager.Instance != null)
            {
                LegendaryPassiveManager.Instance.Changed -= OnPassiveChanged;
                LegendaryPassiveManager.Instance.Changed += OnPassiveChanged;
            }
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Changed -= OnCurrencyChanged;
                CurrencyManager.Instance.Changed += OnCurrencyChanged;
            }
        }

        private void Unsubscribe()
        {
            if (LegendaryPassiveManager.Instance != null) LegendaryPassiveManager.Instance.Changed -= OnPassiveChanged;
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed -= OnCurrencyChanged;
        }

        private void OnPassiveChanged(int id, int newLevel)
        {
            RefreshGrid();
            if (_selectedId == id) RefreshDetail();
        }

        private void OnCurrencyChanged(Currency currency, int _)
        {
            if (currency != LegendaryPassiveManager.Cost) return;
            RefreshSkullLabel();
            if (_selectedId >= 0) RefreshDetail();
        }

        // ------------------------------------------------------------------ construcción

        private bool EnsureBuilt()
        {
            if (_built) return true;
            if (_document == null || _document.panelSettings == null) return false;

            var root = _document.rootVisualElement;
            UiSounds.Bind(root);
            if (root == null) return false;

            BuildUi(root);
            _built = true;
            return true;
        }

        private void BuildUi(VisualElement root)
        {
            MenuStyle.FillParent(root);
            _skin = MenuSkinDresser.Skin;

            _overlay = new VisualElement { name = "mirror-overlay" };
            MenuStyle.FillParent(_overlay);
            _overlay.style.backgroundColor = MenuStyle.Backdrop;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var panel = MenuStyle.Panel();
            _overlay.Add(panel);

            MenuSkinDresser.DressPanel(panel, _skin?.panel);

            var header = MenuStyle.Header();
            panel.Add(header);

            var title = MenuStyle.Title("");
            LocalizedUi.Bind(title, "mirror.title");
            header.Add(title);

            _skullLabel = MenuStyle.CurrencyLabel();
            header.Add(_skullLabel);

            var close = MenuStyle.CloseButton(Close);
            header.Add(close);

            if (_skin != null)
            {
                MenuSkinDresser.DressTitle(title, _skin.titleBar, 96f, 0f, 0f);
                MenuSkinDresser.DressCloseButton(close, _skin);
            }

            _gridView = new VisualElement { name = "mirror-grid-view" };
            panel.Add(_gridView);
            BuildGrid();

            _detailView = new VisualElement { name = "mirror-detail-view" };
            _detailView.style.display = DisplayStyle.None;
            panel.Add(_detailView);
            BuildDetail();

            var hint = MenuStyle.Hint("");
            LocalizedUi.Bind(hint, "common.close_hint");
            panel.Add(hint);
        }

        private void BuildGrid()
        {
            _gridView.style.flexDirection = FlexDirection.Column;
            _gridView.style.alignItems = Align.Center;

            _slots = new Slot[LegendaryPassiveLibrary.SlotCount];

            for (int r = 0; r < Rows; r++)
            {
                var rowBox = new VisualElement();
                rowBox.style.flexDirection = FlexDirection.Row;
                _gridView.Add(rowBox);

                for (int c = 0; c < Columns; c++)
                {
                    int id = r * Columns + c;
                    var slot = BuildSlot(id);
                    _slots[id] = slot;
                    rowBox.Add(slot.button);
                }
            }
        }

        private Slot BuildSlot(int id)
        {
            var button = new Button(() => OnSlotClicked(id));
            MenuStyle.Card(button);
            button.style.width = SlotSize;
            button.style.height = SlotSize;
            button.AddToClassList(UiSounds.NoClickClass);   // suena sólo si la pasiva está desbloqueada

            // El marco de la celda va DESPUÉS de MenuStyle.Card, igual que en la rejilla de mejoras:
            // Dress limpia el fondo/borde planos y pone el suyo, con más relleno.
            MenuSkinDresser.DressPanel(button, _skin?.card);

            var iconWell = new VisualElement { pickingMode = PickingMode.Ignore };
            MenuStyle.FillParent(iconWell);
            iconWell.style.marginLeft = iconWell.style.marginRight = 6;
            iconWell.style.marginTop = iconWell.style.marginBottom = 6;
            button.Add(iconWell);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            MenuStyle.FillParent(icon);
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            iconWell.Add(icon);

            var monogram = new Label { pickingMode = PickingMode.Ignore };
            MenuStyle.FillParent(monogram);
            monogram.style.unityTextAlign = TextAnchor.MiddleCenter;
            monogram.style.unityFontStyleAndWeight = FontStyle.Bold;
            monogram.style.fontSize = 40;
            monogram.style.color = MenuStyle.Cream;
            iconWell.Add(monogram);

            var question = new Label("?") { pickingMode = PickingMode.Ignore };
            MenuStyle.FillParent(question);
            question.style.unityTextAlign = TextAnchor.MiddleCenter;
            question.style.unityFontStyleAndWeight = FontStyle.Bold;
            question.style.fontSize = 52;
            question.style.color = new Color(MenuStyle.Cream.r, MenuStyle.Cream.g, MenuStyle.Cream.b, 0.55f);
            iconWell.Add(question);

            return new Slot { button = button, iconWell = iconWell, icon = icon, question = question, monogram = monogram };
        }

        private void BuildDetail()
        {
            _detailView.style.flexDirection = FlexDirection.Column;
            _detailView.style.alignItems = Align.Center;
            _detailView.style.minWidth = 480;

            _detailTitle = MenuStyle.Title("");
            _detailTitle.style.fontSize = 26;
            _detailTitle.style.marginBottom = 10;
            _detailView.Add(_detailTitle);

            _detailDescription = MenuStyle.CardDescription("");
            _detailDescription.style.fontSize = MenuStyle.BodyFontSize;
            _detailDescription.style.maxWidth = 460;
            _detailDescription.style.marginBottom = 14;
            _detailView.Add(_detailDescription);

            // Nivel 2: línea aparte bajo la del nivel 1, las dos visibles a la vez.
            _detailDescription2 = MenuStyle.CardDescription("");
            _detailDescription2.style.fontSize = MenuStyle.BodyFontSize;
            _detailDescription2.style.maxWidth = 460;
            _detailDescription2.style.marginTop = -6;
            _detailDescription2.style.marginBottom = 14;
            _detailView.Add(_detailDescription2);

            _detailLevel = MenuStyle.CardLevel("");
            _detailLevel.style.fontSize = 22;
            _detailLevel.style.marginBottom = 18;
            _detailView.Add(_detailLevel);

            var upgradeBox = new VisualElement { name = "mirror-upgrade-box" };
            upgradeBox.style.flexDirection = FlexDirection.Column;
            upgradeBox.style.alignItems = Align.Center;
            upgradeBox.style.marginBottom = 18;
            _detailView.Add(upgradeBox);

            _detailUpgradeDescription = MenuStyle.CardDescription("");
            _detailUpgradeDescription.style.fontSize = MenuStyle.BodyFontSize;
            _detailUpgradeDescription.style.maxWidth = 460;
            _detailUpgradeDescription.style.marginBottom = 10;
            upgradeBox.Add(_detailUpgradeDescription);

            _upgradeButton = new Button(OnUpgradeClicked);
            _upgradeButton.AddToClassList(UiSounds.NoClickClass);   // suena sólo si la mejora sale bien
            LocalizedUi.Bind(_upgradeButton, "mirror.upgrade");
            _upgradeButton.style.width = 220;
            _upgradeButton.style.height = 52;
            _upgradeButton.style.color = MenuStyle.Cream;
            _upgradeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _upgradeButton.style.fontSize = 18;
            _upgradeButton.style.backgroundColor = MenuStyle.CellBuyable;
            MenuStyle.SetBorder(_upgradeButton, 2, MenuStyle.GoldBorder, 10);
            MenuStyle.AddSelectionHighlight(_upgradeButton, 10f);
            upgradeBox.Add(_upgradeButton);

            _upgradeFooter = new Label("");
            _upgradeFooter.style.marginTop = 8;
            _upgradeFooter.style.fontSize = MenuStyle.CardFooterFontSize;
            _upgradeFooter.style.unityFontStyleAndWeight = FontStyle.Bold;
            upgradeBox.Add(_upgradeFooter);

            var back = new Button(ShowGrid);
            back.AddToClassList(UiSounds.BackClass);
            LocalizedUi.Bind(back, "mirror.back");
            back.style.width = 160;
            back.style.height = 44;
            back.style.color = MenuStyle.Cream;
            back.style.backgroundColor = MenuStyle.CellBg;
            MenuStyle.SetBorder(back, 2, MenuStyle.GoldBorder, 10);
            MenuStyle.AddSelectionHighlight(back, 10f);
            _detailView.Add(back);
        }

        // ------------------------------------------------------------------ navegación

        private void ShowGrid()
        {
            _selectedId = -1;
            if (_gridView != null) _gridView.style.display = DisplayStyle.Flex;
            if (_detailView != null) _detailView.style.display = DisplayStyle.None;
            RefreshGrid();
        }

        private void ShowDetail(int id)
        {
            _selectedId = id;
            if (_gridView != null) _gridView.style.display = DisplayStyle.None;
            if (_detailView != null) _detailView.style.display = DisplayStyle.Flex;
            RefreshDetail();
        }

        private void OnSlotClicked(int id)
        {
            var passive = PassiveAt(id);
            if (passive == null) return;

            var manager = LegendaryPassiveManager.Instance;
            if (manager == null || !manager.IsUnlocked(passive))
            {
                UiSounds.Deny();   // bloqueada
                return;
            }

            UiSounds.Click();
            ShowDetail(id);
        }

        private void OnUpgradeClicked()
        {
            var passive = PassiveAt(_selectedId);
            var manager = LegendaryPassiveManager.Instance;
            if (passive == null || manager == null) return;

            if (manager.TryUpgrade(passive)) SystemSounds.Play(s => s.mirrorPassiveUpgraded);
            else UiSounds.Deny();
            // Un fallo (sin fondos / al máximo) suena a rechazo; RefreshDetail ya deja claro el estado.
        }

        private static LegendaryPassive PassiveAt(int id) =>
            id >= 0 && id < LegendaryPassiveLibrary.BySlot.Count ? LegendaryPassiveLibrary.BySlot[id] : null;

        // ------------------------------------------------------------------ refresco

        private void RefreshSkullLabel()
        {
            if (_skullLabel == null) return;
            int skulls = CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(Currency.Skull) : 0;
            _skullLabel.text = Loc.Get("mirror.skulls", skulls);
        }

        private void RefreshGrid()
        {
            RefreshSkullLabel();
            if (_slots == null) return;

            var manager = LegendaryPassiveManager.Instance;
            for (int id = 0; id < _slots.Length; id++)
                RefreshSlot(id, _slots[id], PassiveAt(id), manager);
        }

        private void RefreshSlot(int id, Slot slot, LegendaryPassive passive, LegendaryPassiveManager manager)
        {
            if (slot == null) return;

            if (passive == null)
            {
                slot.button.SetEnabled(false);
                slot.button.style.opacity = 0.35f;
                slot.question.style.display = DisplayStyle.Flex;
                slot.icon.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                slot.monogram.text = "";
                PaintSlot(slot.button, MenuStyle.Locked, _skin?.cardLockedTint);
                return;
            }

            bool unlocked = manager != null && manager.IsUnlocked(passive);
            slot.button.SetEnabled(unlocked);
            slot.button.style.opacity = 1f;

            if (!unlocked)
            {
                slot.question.style.display = DisplayStyle.Flex;
                slot.icon.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                slot.monogram.text = "";
                PaintSlot(slot.button, MenuStyle.Locked, _skin?.cardLockedTint);
                return;
            }

            slot.question.style.display = DisplayStyle.None;

            if (passive.icon != null)
            {
                slot.icon.style.backgroundImage = new StyleBackground(passive.icon);
                slot.monogram.text = "";
            }
            else
            {
                slot.icon.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                slot.monogram.text = !string.IsNullOrEmpty(passive.DisplayName)
                    ? passive.DisplayName.Substring(0, 1).ToUpperInvariant() : "?";
            }

            bool maxed = manager.IsMaxed(passive);
            PaintSlot(slot.button, maxed ? MenuStyle.CellMaxed : MenuStyle.CellBg,
                      maxed ? _skin?.cardMaxedTint : Color.white);
        }

        private void RefreshDetail()
        {
            var passive = PassiveAt(_selectedId);
            var manager = LegendaryPassiveManager.Instance;
            RefreshSkullLabel();

            if (passive == null || manager == null)
            {
                _detailTitle.text = "";
                _detailDescription.text = "";
                _detailDescription2.style.display = DisplayStyle.None;
                _detailLevel.text = "";
                _detailUpgradeDescription.text = "";
                _upgradeButton.style.display = DisplayStyle.None;
                _upgradeFooter.text = "";
                return;
            }

            int level = manager.GetLevel(passive);
            bool maxed = manager.IsMaxed(passive);

            _detailTitle.text = passive.DisplayName;
            _detailDescription.text = Loc.Get("mirror.level1", passive.Description);
            _detailDescription2.text = Loc.Get("mirror.level2", passive.UpgradeDescription);
            _detailDescription2.style.display = level >= 2 ? DisplayStyle.Flex : DisplayStyle.None;
            _detailLevel.text = Loc.Get("mirror.level", level, passive.maxLevel);

            if (maxed)
            {
                _detailUpgradeDescription.text = Loc.Get("mirror.max_level");
                _upgradeButton.style.display = DisplayStyle.None;
                _upgradeFooter.text = "";
                return;
            }

            _detailUpgradeDescription.text = Loc.Get("mirror.upgrade_to_2", passive.UpgradeDescription);
            _upgradeButton.style.display = DisplayStyle.Flex;

            bool canAfford = manager.CanUpgrade(passive);
            _upgradeButton.SetEnabled(canAfford);
            _upgradeFooter.text = Loc.Get("mirror.cost", passive.upgradeCost);
            _upgradeFooter.style.color = canAfford ? MenuStyle.CostAfford : MenuStyle.CostTooDear;
        }

        /// <summary>
        /// Pinta el estado de una celda. Con arte, el estado va en el tinte del marco (igual que
        /// <see cref="UpgradeMenuController.PaintCell"/>); sin arte, el color plano de siempre.
        /// </summary>
        private void PaintSlot(VisualElement button, Color plainBackground, Color? skinTint)
        {
            if (_skin != null && _skin.card.IsSet)
            {
                UiFrame.Tint(button, skinTint ?? Color.white);
                return;
            }

            button.style.backgroundColor = plainBackground;
        }
    }
}
