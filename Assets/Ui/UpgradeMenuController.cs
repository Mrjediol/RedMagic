using System;
using System.Collections.Generic;
using RedMagic.Arsenal;
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
    /// Menú de mejoras permanentes del hub (el que abre el caldero). Rejilla estilo "Skull: The
    /// Hero Slayer": filas independientes, y dentro de cada fila hay que comprar de izquierda a
    /// derecha (la columna N se desbloquea al llevar la N-1 a nivel 1). Cada nodo tiene varios
    /// niveles y se paga con fragmentos de alma.
    ///
    /// Se auto-crea y es persistente (DontDestroyOnLoad) como <c>CurrencyHud</c>: construye su
    /// <see cref="UIDocument"/> en código y saca el <see cref="PanelSettings"/> de Resources. Está
    /// oculto salvo cuando <see cref="Open"/> lo llama <c>Hub.BookLootContainer</c> (el atril del
    /// libro; antes era el caldero, ya retirado a Legacy).
    ///
    /// La maqueta y los tamaños salen de <see cref="MenuStyle"/>, compartidos con la tienda; el
    /// <b>arte</b> sale de <see cref="MenuSkin"/> vía <see cref="MenuSkinDresser"/> — marco de
    /// piedra en el panel y en cada celda, placa en el título y la X redonda del kit. Sin ese
    /// asset el menú se dibuja igual que siempre, plano. Ojo al tocarlo: el estado de una celda
    /// (bloqueada / comprable / al máximo) se pinta <b>tiñendo el marco</b>, no con el color de
    /// fondo — ver <see cref="PaintCell"/>.
    ///
    /// Mientras está abierto el juego queda en pausa vía <see cref="GameStateManager"/>, así que el
    /// jugador no se mueve por el hub por detrás.
    ///
    /// Pestañas: <b>General</b> (esta rejilla) y una por arma de <see cref="WeaponMasteryConfig.weapons"/> con su árbol
    /// permanente (<see cref="WeaponMastery"/>: Poder, Firma y una fila por evolución), que se desplaza en vertical.
    /// <see cref="OpenWeapons"/> abre directamente en la primera arma (lo usa Supervivencia).
    /// </summary>
    [DisallowMultipleComponent]
    public class UpgradeMenuController : MonoBehaviour
    {
        public static UpgradeMenuController Instance { get; private set; }

        private const string PanelSettingsResourcePath = "UpgradeMenuPanelSettings";

        /// <summary>
        /// Alto de la placa del título en esta pantalla. La letra aquí es de 34 px (la mitad que en
        /// pausa/opciones), y la piedra de la placa se lleva ~22 px por arriba y otros tantos por
        /// abajo: 96 deja el texto centrado con aire sin dejar un pedrusco desproporcionado.
        /// </summary>
        private const float SkinnedTitleHeight = 96f;

        private UIDocument _document;
        private VisualElement _overlay;
        private Label _soulLabel;
        private VisualElement _grid;
        private Cell[,] _cells;
        private bool _open;
        private bool _built;

        // Pestañas: -1 = General; 0.. = armas de WeaponMasteryConfig.weapons.
        private VisualElement _tabRow;
        private ScrollView _weaponPanel;
        private int _tab = -1;
        private readonly List<Button> _tabButtons = new();
        private readonly List<(MasteryNode node, List<MasteryNode> row, int column, Cell cell)> _weaponCells = new();

        /// <summary>Arte del kit de menús. Null = aspecto plano de <see cref="MenuStyle"/>, como siempre.</summary>
        private MenuSkin _skin;

        private sealed class Cell
        {
            public Button button;
            public Label title;
            public Label level;
            public Label desc;
            public Label footer;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[UpgradeMenu]").AddComponent<UpgradeMenuController>();
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
                Debug.LogWarning($"[UpgradeMenu] Falta '{PanelSettingsResourcePath}' en Resources; el " +
                                 "menú del caldero no se podrá abrir.", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (UpgradeManager.Instance != null) UpgradeManager.Instance.Changed -= OnUpgradeChanged;
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

        public void Open()
        {
            if (_open) return;
            if (!EnsureBuilt()) return;

            _open = true;
            SystemSounds.Play(s => s.upgradeMenu.open);
            _overlay.style.display = DisplayStyle.Flex;

            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(true);

            Subscribe();
            SelectTab(_tab);
        }

        /// <summary>Abre el menú en la pestaña de la primera arma.</summary>
        public void OpenWeapons()
        {
            Open();
            if (_open && WeaponMasteryConfig.Instance.weapons.Count > 0) SelectTab(0);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            SystemSounds.Play(s => s.upgradeMenu.close);

            _overlay.style.display = DisplayStyle.None;
            Unsubscribe();

            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(false);
        }

        private void Subscribe()
        {
            if (UpgradeManager.Instance != null)
            {
                UpgradeManager.Instance.Changed -= OnUpgradeChanged;
                UpgradeManager.Instance.Changed += OnUpgradeChanged;
            }
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Changed -= OnCurrencyChanged;
                CurrencyManager.Instance.Changed += OnCurrencyChanged;
            }
            WeaponMastery.Changed -= OnMasteryChanged;
            WeaponMastery.Changed += OnMasteryChanged;
        }

        private void Unsubscribe()
        {
            if (UpgradeManager.Instance != null) UpgradeManager.Instance.Changed -= OnUpgradeChanged;
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed -= OnCurrencyChanged;
            WeaponMastery.Changed -= OnMasteryChanged;
        }

        private void OnMasteryChanged(string _, string __) => RefreshAll();

        private void OnUpgradeChanged(string _, int __) => RefreshAll();

        private void OnCurrencyChanged(Currency currency, int __)
        {
            if (currency == UpgradeManager.Cost) RefreshAll();
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

            _overlay = new VisualElement { name = "upgrade-overlay" };
            MenuStyle.FillParent(_overlay);
            _overlay.style.backgroundColor = MenuStyle.Backdrop;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var panel = MenuStyle.Panel();
            _overlay.Add(panel);

            // El marco de piedra sustituye al fondo+borde planos de MenuStyle.Panel.
            MenuSkinDresser.DressPanel(panel, _skin?.panel);

            var header = MenuStyle.Header();
            panel.Add(header);

            var title = MenuStyle.Title("");
            LocalizedUi.Bind(title, "upgrades.title");
            header.Add(title);

            _soulLabel = MenuStyle.CurrencyLabel();
            header.Add(_soulLabel);

            var close = MenuStyle.CloseButton(Close);
            header.Add(close);

            if (_skin != null)
            {
                // La placa del título va con su alto propio: aquí la letra es de 34 px, la mitad
                // que en pausa/opciones, así que la placa de 132 dejaría un hueco enorme.
                // Sin margen inferior: aquí la placa va dentro de una cabecera en fila, no encima
                // del contenido, así que ese hueco la descentraría respecto a la X.
                MenuSkinDresser.DressTitle(title, _skin.titleBar, SkinnedTitleHeight, 0f, 0f);
                MenuSkinDresser.DressCloseButton(close, _skin);
            }

            BuildTabs(panel);

            _grid = new VisualElement { name = "upgrade-grid" };
            _grid.style.flexDirection = FlexDirection.Column;
            panel.Add(_grid);

            BuildGrid();

            _weaponPanel = new ScrollView(ScrollViewMode.Vertical) { name = "weapon-mastery" };
            _weaponPanel.style.maxHeight = 600;
            _weaponPanel.style.display = DisplayStyle.None;
            panel.Add(_weaponPanel);

            var hint = MenuStyle.Hint("");
            LocalizedUi.Bind(hint, "common.close_hint");
            panel.Add(hint);
        }

        private void BuildGrid()
        {
            _grid.Clear();

            var tree = UpgradeManager.Instance != null ? UpgradeManager.Instance.Tree : null;
            if (tree == null)
            {
                var missing = new Label(Loc.Get("upgrades.missing_tree"));
                missing.style.color = MenuStyle.Cream;
                missing.style.fontSize = MenuStyle.BodyFontSize;
                _grid.Add(missing);
                _cells = new Cell[0, 0];
                return;
            }

            _cells = new Cell[tree.Rows, tree.Columns];

            for (int r = 0; r < tree.Rows; r++)
            {
                var rowBox = new VisualElement();
                rowBox.style.flexDirection = FlexDirection.Row;
                _grid.Add(rowBox);

                for (int c = 0; c < tree.Columns; c++)
                {
                    var cell = BuildCell(r, c, tree.NodeAt(r, c));
                    _cells[r, c] = cell;
                    rowBox.Add(cell.button);
                }
            }
        }

        private Cell BuildCell(int row, int column, UpgradeTree.Node node)
        {
            int r = row, c = column;
            var button = new Button(() => TryBuy(r, c));
            MenuStyle.Card(button);
            button.AddToClassList(UiSounds.NoClickClass);   // suena sólo si la compra sale bien

            // El marco de la celda va DESPUÉS de MenuStyle.Card: Dress limpia el fondo y el borde
            // planos y pone su propio relleno, más ancho, para que el texto no pise la piedra.
            MenuSkinDresser.DressPanel(button, _skin?.card);

            var title = MenuStyle.CardTitle(node != null ? node.DisplayTitle : "-");
            var desc = MenuStyle.CardDescription("");
            var level = MenuStyle.CardLevel("");
            var footer = MenuStyle.CardFooter("");

            button.Add(title);
            button.Add(desc);
            button.Add(level);
            button.Add(footer);

            return new Cell { button = button, title = title, level = level, desc = desc, footer = footer };
        }

        // ------------------------------------------------------------------ pestañas y árboles de armas

        private void BuildTabs(VisualElement panel)
        {
            _tabRow = new VisualElement { name = "upgrade-tabs" };
            _tabRow.style.flexDirection = FlexDirection.Row;
            _tabRow.style.justifyContent = Justify.Center;
            _tabRow.style.marginBottom = 8;
            panel.Add(_tabRow);
            _tabButtons.Clear();

            AddTab(-1, null, "upgrades.tab_general");
            var weapons = WeaponMasteryConfig.Instance.weapons;
            for (int i = 0; i < weapons.Count; i++)
                if (weapons[i] != null && weapons[i].IsArsenal) AddTab(i, weapons[i].Icon, null, weapons[i]);
        }

        private void AddTab(int index, Sprite icon, string key, Items.WeaponDefinition weapon = null)
        {
            var tab = new Button(() => SelectTab(index));
            tab.style.flexDirection = FlexDirection.Row;
            tab.style.alignItems = Align.Center;
            tab.style.height = 56;
            tab.style.marginLeft = tab.style.marginRight = 4;
            tab.style.paddingLeft = tab.style.paddingRight = 10;
            tab.style.backgroundColor = MenuStyle.CellBg;
            tab.style.color = MenuStyle.Cream;
            tab.style.fontSize = 22;
            MenuStyle.SetBorder(tab, 2, MenuStyle.GoldBorder, 8);
            tab.userData = index;
            if (icon != null)
            {
                var img = new Image { sprite = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                img.style.width = 40; img.style.height = 40;
                tab.Add(img);
                tab.tooltip = weapon.DisplayName;
            }
            else LocalizedUi.Bind(tab, key);
            _tabRow.Add(tab);
            _tabButtons.Add(tab);
        }

        private void SelectTab(int index)
        {
            _tab = index;
            foreach (var b in _tabButtons)
            {
                bool on = (int)b.userData == index;
                b.style.backgroundColor = on ? MenuStyle.CellBuyable : MenuStyle.CellBg;
            }
            _grid.style.display = index < 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _weaponPanel.style.display = index < 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (index >= 0) BuildWeaponTree(WeaponMasteryConfig.Instance.weapons[index]);
            RefreshAll();
        }

        private void BuildWeaponTree(Items.WeaponDefinition weapon)
        {
            _weaponPanel.Clear();
            _weaponCells.Clear();
            var kit = weapon != null ? weapon.ArsenalKitAsset : null;
            if (kit == null) return;

            var name = MenuStyle.CardTitle(weapon.DisplayName);
            name.style.fontSize = 30;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            _weaponPanel.Add(name);

            var rows = WeaponMastery.Rows(kit);
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                var line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;
                _weaponPanel.Add(line);

                // La fila se llama como lo que mejora: Poder, Firma o el nombre de la evolución.
                string rowName = row[0].kind switch
                {
                    MasteryKind.Damage => Loc.Get("mastery.row_power"),
                    MasteryKind.Signature => Loc.Get("mastery.row_signature"),
                    _ => ArsenalTexts.EvolutionName(kit, row[0].evo),
                };
                var label = MenuStyle.CardTitle(rowName);
                label.style.width = 200;
                label.style.whiteSpace = WhiteSpace.Normal;
                line.Add(label);

                for (int c = 0; c < row.Count; c++)
                {
                    int column = c;
                    var nodeRow = row;
                    var cell = BuildCell(0, 0, null);
                    cell.button.clickable = new Clickable(() => BuyMastery(kit, nodeRow, column));
                    cell.button.style.width = 300;
                    cell.button.style.height = StyleKeyword.Auto;
                    cell.button.style.minHeight = 130;
                    cell.desc.style.whiteSpace = WhiteSpace.Normal;
                    line.Add(cell.button);
                    _weaponCells.Add((row[c], row, c, cell));
                }
            }
        }

        private void BuyMastery(ArsenalKit kit, List<MasteryNode> row, int column)
        {
            if (WeaponMastery.TryBuy(kit, row, column)) SystemSounds.Play(s => s.upgradeBought);
            else UiSounds.Deny();
        }

        private void RefreshWeaponCells()
        {
            if (_tab < 0 || _weaponCells.Count == 0) return;
            var weapon = WeaponMasteryConfig.Instance.weapons[_tab];
            var kit = weapon.ArsenalKitAsset;
            int soul = CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(UpgradeManager.Cost) : 0;

            foreach (var (node, row, column, cell) in _weaponCells)
            {
                WeaponMastery.Describe(kit, node, out string title, out string effect);
                int lvl = WeaponMastery.Level(kit.WeaponId, node.id);
                cell.title.text = title;
                cell.desc.text = effect;
                cell.level.text = Loc.Get("upgrades.level", lvl, node.maxLevel);

                if (!WeaponMastery.IsAvailable(kit.WeaponId, row, column))
                {
                    cell.button.SetEnabled(false);
                    cell.button.style.opacity = 0.55f;
                    PaintCell(cell.button, MenuStyle.Locked, _skin?.cardLockedTint);
                    cell.footer.text = Loc.Get("upgrades.locked");
                    cell.footer.style.color = MenuStyle.CostTooDear;
                    continue;
                }
                cell.button.style.opacity = 1f;
                if (lvl >= node.maxLevel)
                {
                    cell.button.SetEnabled(false);
                    PaintCell(cell.button, MenuStyle.CellMaxed, _skin?.cardMaxedTint);
                    cell.footer.text = Loc.Get("upgrades.max");
                    cell.footer.style.color = MenuStyle.Cream;
                    continue;
                }
                int cost = WeaponMastery.NextCost(node, lvl);
                bool afford = soul >= cost;
                cell.button.SetEnabled(true);
                PaintCell(cell.button, afford ? MenuStyle.CellBuyable : MenuStyle.CellBg, afford ? _skin?.cardBuyableTint : Color.white);
                cell.footer.text = Loc.Get("upgrades.cost", cost);
                cell.footer.style.color = afford ? MenuStyle.CostAfford : MenuStyle.CostTooDear;
            }
        }

        // ------------------------------------------------------------------ compra / refresco

        private void TryBuy(int row, int column)
        {
            var manager = UpgradeManager.Instance;
            if (manager == null) return;

            if (manager.TryBuy(row, column)) SystemSounds.Play(s => s.upgradeBought);
            else UiSounds.Deny();
            // Un fallo (bloqueado / sin fondos / al máximo) no hace nada: RefreshAll ya deja claro
            // el estado de cada celda.
        }

        private void RefreshAll()
        {
            var upgrades = UpgradeManager.Instance;
            var wallet = CurrencyManager.Instance;
            var tree = upgrades != null ? upgrades.Tree : null;

            if (_soulLabel != null)
            {
                int soul = wallet != null ? wallet.Get(UpgradeManager.Cost) : 0;
                _soulLabel.text = Loc.Get("upgrades.soul_fragments", soul);
            }

            RefreshWeaponCells();
            if (tree == null || _cells == null) return;

            for (int r = 0; r < tree.Rows; r++)
            for (int c = 0; c < tree.Columns; c++)
                RefreshCell(r, c, tree.NodeAt(r, c), _cells[r, c], upgrades);
        }

        private void RefreshCell(int row, int column, UpgradeTree.Node node, Cell cell, UpgradeManager upgrades)
        {
            if (cell == null) return;

            if (node == null || upgrades == null)
            {
                cell.button.SetEnabled(false);
                PaintCell(cell.button, MenuStyle.Locked, _skin?.cardLockedTint);
                return;
            }

            int lvl = upgrades.GetLevel(node.id);
            bool unlocked = upgrades.IsUnlocked(row, column);
            bool maxed = upgrades.IsMaxed(node);
            int percent = Mathf.RoundToInt(node.bonusPerLevel * 100f);

            cell.title.text = node.DisplayTitle;
            cell.desc.text = SafeFormat(node.DisplayDescription, percent);
            cell.level.text = Loc.Get("upgrades.level", lvl, node.maxLevel);

            if (!unlocked)
            {
                cell.button.SetEnabled(false);
                PaintCell(cell.button, MenuStyle.Locked, _skin?.cardLockedTint);
                cell.button.style.opacity = 0.55f;
                cell.footer.text = Loc.Get("upgrades.locked");
                cell.footer.style.color = MenuStyle.CostTooDear;
                return;
            }

            cell.button.style.opacity = 1f;

            if (maxed)
            {
                cell.button.SetEnabled(false);
                PaintCell(cell.button, MenuStyle.CellMaxed, _skin?.cardMaxedTint);
                cell.footer.text = Loc.Get("upgrades.max");
                cell.footer.style.color = MenuStyle.Cream;
                return;
            }

            int cost = upgrades.NextCost(node);
            bool canAfford = upgrades.CanBuy(row, column);

            cell.button.SetEnabled(true);
            PaintCell(cell.button, canAfford ? MenuStyle.CellBuyable : MenuStyle.CellBg,
                      canAfford ? _skin?.cardBuyableTint : Color.white);
            cell.footer.text = Loc.Get("upgrades.cost", cost);
            cell.footer.style.color = canAfford ? MenuStyle.CostAfford : MenuStyle.CostTooDear;
        }

        /// <summary>
        /// Pinta el estado de una celda. Con arte, el estado va en el <b>tinte del marco</b>
        /// (<paramref name="skinTint"/>): pintarle un color de fondo taparía la piedra, porque el
        /// marco se dibuja en una capa aparte y el fondo del botón queda por detrás. Sin arte, se
        /// sigue usando el color plano de siempre.
        /// </summary>
        private void PaintCell(VisualElement button, Color plainBackground, Color? skinTint)
        {
            if (_skin != null && _skin.card.IsSet)
            {
                UiFrame.Tint(button, skinTint ?? Color.white);
                return;
            }

            button.style.backgroundColor = plainBackground;
        }

        private static string SafeFormat(string template, int value)
        {
            if (string.IsNullOrEmpty(template)) return "";
            try { return string.Format(template, value); }
            catch (FormatException) { return template; }
        }
    }
}
