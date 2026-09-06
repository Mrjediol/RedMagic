using System;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Economy;
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
    /// oculto salvo cuando <see cref="Open"/> lo llama <see cref="CauldronInteractable"/>.
    ///
    /// Mientras está abierto el juego queda en pausa vía <see cref="GameStateManager"/>, así que el
    /// jugador no se mueve por el hub por detrás.
    /// </summary>
    [DisallowMultipleComponent]
    public class UpgradeMenuController : MonoBehaviour
    {
        public static UpgradeMenuController Instance { get; private set; }

        private const string PanelSettingsResourcePath = "UpgradeMenuPanelSettings";

        private UIDocument _document;
        private VisualElement _overlay;
        private Label _soulLabel;
        private VisualElement _grid;
        private Cell[,] _cells;
        private bool _open;
        private bool _built;

        // ---- paleta, a juego con RedMagicTheme / PauseMenu.uss ----
        private static readonly Color Backdrop = new(0.02f, 0.015f, 0.02f, 0.82f);
        private static readonly Color PanelBg = new(0.047f, 0.031f, 0.039f, 0.96f);
        private static readonly Color GoldBorder = new(0.588f, 0.439f, 0.29f, 0.6f);
        private static readonly Color Cream = new(0.925f, 0.874f, 0.749f);
        private static readonly Color CellBg = new(0.086f, 0.063f, 0.078f, 0.95f);
        private static readonly Color CellBuyable = new(0.478f, 0.102f, 0.133f, 0.85f);
        private static readonly Color CellMaxed = new(0.34f, 0.27f, 0.13f, 0.9f);
        private static readonly Color Locked = new(0.5f, 0.5f, 0.5f, 0.35f);
        private static readonly Color CostAfford = new(0.6f, 0.9f, 0.55f);
        private static readonly Color CostTooDear = new(0.95f, 0.45f, 0.45f);

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
            _overlay.style.display = DisplayStyle.Flex;

            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(true);

            Subscribe();
            RefreshAll();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;

            _overlay.style.display = DisplayStyle.None;
            Unsubscribe();

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ButtonClick");
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
        }

        private void Unsubscribe()
        {
            if (UpgradeManager.Instance != null) UpgradeManager.Instance.Changed -= OnUpgradeChanged;
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed -= OnCurrencyChanged;
        }

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
            if (root == null) return false;

            BuildUi(root);
            _built = true;
            return true;
        }

        private void BuildUi(VisualElement root)
        {
            root.style.position = Position.Absolute;
            root.style.top = 0;
            root.style.left = 0;
            root.style.right = 0;
            root.style.bottom = 0;

            _overlay = new VisualElement { name = "upgrade-overlay" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.top = 0;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = Backdrop;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var panel = new VisualElement { name = "upgrade-panel" };
            panel.style.paddingTop = 20;
            panel.style.paddingBottom = 20;
            panel.style.paddingLeft = 24;
            panel.style.paddingRight = 24;
            panel.style.backgroundColor = PanelBg;
            SetBorder(panel, 2, GoldBorder, 18);
            panel.style.maxWidth = Length.Percent(94);
            _overlay.Add(panel);

            // ---- cabecera ----
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 16;
            panel.Add(header);

            var title = new Label("PERMANENT UPGRADES");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 24;
            title.style.color = Cream;
            title.style.letterSpacing = 2;
            header.Add(title);

            _soulLabel = new Label("0");
            _soulLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _soulLabel.style.fontSize = 20;
            _soulLabel.style.color = Cream;
            _soulLabel.style.marginLeft = 24;
            _soulLabel.style.marginRight = 24;
            header.Add(_soulLabel);

            var close = new Button(Close) { text = "✕" };
            close.style.fontSize = 18;
            close.style.width = 34;
            close.style.height = 34;
            close.style.color = Cream;
            close.style.backgroundColor = CellBuyable;
            SetBorder(close, 1, GoldBorder, 8);
            header.Add(close);

            // ---- rejilla ----
            _grid = new VisualElement();
            _grid.style.flexDirection = FlexDirection.Column;
            panel.Add(_grid);

            BuildGrid();

            var hint = new Label("Esc / E / B to close");
            hint.style.marginTop = 12;
            hint.style.fontSize = 11;
            hint.style.color = new Color(Cream.r, Cream.g, Cream.b, 0.45f);
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(hint);
        }

        private void BuildGrid()
        {
            _grid.Clear();

            var tree = UpgradeManager.Instance != null ? UpgradeManager.Instance.Tree : null;
            if (tree == null)
            {
                _grid.Add(new Label("(no UpgradeTree asset in Resources)") { style = { color = Cream } });
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
            button.style.width = 138;
            button.style.height = 116;
            button.style.marginLeft = 5;
            button.style.marginRight = 5;
            button.style.marginTop = 5;
            button.style.marginBottom = 5;
            button.style.paddingTop = 8;
            button.style.paddingBottom = 8;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.SpaceBetween;
            button.style.whiteSpace = WhiteSpace.Normal;
            SetBorder(button, 2, GoldBorder, 10);
            button.RegisterCallback<PointerEnterEvent>(_ => AudioManager.Instance?.PlaySFX("SFX_ButtonHover"));

            var title = new Label(node != null ? node.title : "-");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 13;
            title.style.color = Cream;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.Add(title);

            var desc = new Label();
            desc.style.fontSize = 10;
            desc.style.color = new Color(Cream.r, Cream.g, Cream.b, 0.75f);
            desc.style.unityTextAlign = TextAnchor.MiddleCenter;
            desc.style.whiteSpace = WhiteSpace.Normal;
            button.Add(desc);

            var level = new Label();
            level.style.fontSize = 12;
            level.style.unityFontStyleAndWeight = FontStyle.Bold;
            level.style.color = Cream;
            button.Add(level);

            var footer = new Label();
            footer.style.fontSize = 12;
            footer.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.Add(footer);

            return new Cell { button = button, title = title, level = level, desc = desc, footer = footer };
        }

        // ------------------------------------------------------------------ compra / refresco

        private void TryBuy(int row, int column)
        {
            var manager = UpgradeManager.Instance;
            if (manager == null) return;

            if (manager.TryBuy(row, column))
                AudioManager.Instance?.PlaySFX("SFX_ButtonClick");
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
                _soulLabel.text = $"Soul Fragments: {soul}";
            }

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
                cell.button.style.backgroundColor = Locked;
                return;
            }

            int lvl = upgrades.GetLevel(node.id);
            bool unlocked = upgrades.IsUnlocked(row, column);
            bool maxed = upgrades.IsMaxed(node);
            int percent = Mathf.RoundToInt(node.bonusPerLevel * 100f);

            cell.title.text = node.title;
            cell.desc.text = SafeFormat(node.description, percent);
            cell.level.text = $"Lv {lvl}/{node.maxLevel}";

            if (!unlocked)
            {
                cell.button.SetEnabled(false);
                cell.button.style.backgroundColor = Locked;
                cell.button.style.opacity = 0.55f;
                cell.footer.text = "LOCKED";
                cell.footer.style.color = CostTooDear;
                return;
            }

            cell.button.style.opacity = 1f;

            if (maxed)
            {
                cell.button.SetEnabled(false);
                cell.button.style.backgroundColor = CellMaxed;
                cell.footer.text = "MAX";
                cell.footer.style.color = Cream;
                return;
            }

            int cost = upgrades.NextCost(node);
            bool canAfford = upgrades.CanBuy(row, column);

            cell.button.SetEnabled(true);
            cell.button.style.backgroundColor = canAfford ? CellBuyable : CellBg;
            cell.footer.text = $"{cost} SF";
            cell.footer.style.color = canAfford ? CostAfford : CostTooDear;
        }

        private static string SafeFormat(string template, int value)
        {
            if (string.IsNullOrEmpty(template)) return "";
            try { return string.Format(template, value); }
            catch (FormatException) { return template; }
        }

        private static void SetBorder(VisualElement e, float width, Color color, float radius)
        {
            e.style.borderTopWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderTopColor = color;
            e.style.borderBottomColor = color;
            e.style.borderLeftColor = color;
            e.style.borderRightColor = color;
            e.style.borderTopLeftRadius = radius;
            e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius;
            e.style.borderBottomRightRadius = radius;
        }
    }
}
