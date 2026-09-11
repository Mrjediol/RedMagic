using System.Collections.Generic;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Pantalla de items y builds (sección 9 del documento de diseño). Se abre con <b>I</b>.
    ///
    /// Layout de 3 columnas:
    /// <list type="bullet">
    /// <item><b>Izquierda</b>: las 6 sinergias, con su conteo actual y el próximo umbral ("2 ▸ 4"),
    /// y marcadas en dorado al llegar al tope de 6.</item>
    /// <item><b>Centro</b>: arriba los 3 slots dedicados, debajo los 6 libres, abajo el arma.</item>
    /// <item><b>Derecha</b>: panel de descripción que se actualiza al pasar el ratón por un item /
    /// modificador (efecto + tags) o por el arma (stats base + comportamiento resultante de los 3
    /// slots dedicados equipados, "en vivo").</item>
    /// </list>
    /// Al hacer hover sobre un item se resaltan en la columna izquierda las sinergias a las que
    /// aporta.
    ///
    /// <b>La UI no cuenta nada</b>: la columna de sinergias lee del <see cref="SynergyTracker"/> y
    /// se repinta con sus eventos. Clic en un slot lo <i>cicla</i> entre los assets disponibles de
    /// ese tipo (afordancia de pruebas, como el menú de habilidades) para poder ejercitar todas las
    /// combinaciones sin tocar código.
    ///
    /// <b>Arte</b>: marcos, cabeceras, slots y botón de cerrar salen de <see cref="ItemMenuSkin"/>
    /// (<c>Resources/ItemMenuSkin.asset</c>, generado por <c>ItemsUiPack</c>). Cada pieza que falte
    /// cae al aspecto plano de <see cref="MenuStyle"/>, así que el menú funciona igual sin skin.
    ///
    /// Mismo montaje que <see cref="AbilityMenuController"/>: se auto-crea, es persistente,
    /// construye su UI en código y saca el <see cref="PanelSettings"/> de Resources; pausa el juego
    /// mientras está abierto.
    /// </summary>
    [DisallowMultipleComponent]
    public class ItemMenuController : MonoBehaviour
    {
        public static ItemMenuController Instance { get; private set; }

        private const string PanelSettingsResourcePath = "ItemMenuPanelSettings";

        private static readonly BuildTag[] SynergyOrder =
        {
            BuildTag.Ice, BuildTag.Fire, BuildTag.Tank, BuildTag.Haste, BuildTag.Lifesteal, BuildTag.Reset,
        };

        private static readonly Vector3 HoverScale = new(1.035f, 1.035f, 1f);

        private UIDocument _document;
        private ItemMenuSkin _skin;
        private VisualElement _overlay;
        private bool _open;
        private bool _built;

        private WeaponLoadout _loadout;

        // Columna izquierda
        private readonly Dictionary<BuildTag, SynergyRow> _synergyRows = new Dictionary<BuildTag, SynergyRow>();
        private bool _skinnedRows;

        // Columna central
        private SlotButton _elementSlot;
        private SlotButton _trajectorySlot;
        private SlotButton _shapeSlot;
        private readonly SlotButton[] _freeSlots = new SlotButton[WeaponInventory.FreeSlotCount];
        private SlotButton _weaponSlot;

        // Columna derecha
        private VisualElement _description;

        // Candidatos para ciclar
        private ElementModifier[] _elementCandidates;
        private TrajectoryModifier[] _trajectoryCandidates;
        private ShapeModifier[] _shapeCandidates;
        private FreePoolItemDefinition[] _freeCandidates;
        private WeaponDefinition[] _weaponCandidates;

        private sealed class SynergyRow
        {
            public VisualElement Root;
            public Label Count;
        }

        private sealed class SlotButton
        {
            public Button Button;
            public Label Value;
            public string Placeholder;
            public string Caption;

            // Sólo con skin: el marco de arte y lo que se pinta dentro.
            public bool IsWeapon;
            public VisualElement Art;
            public VisualElement Icon;
            public Label Monogram;
            public bool Hovered;
            public Object Current;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[ItemMenu]").AddComponent<ItemMenuController>();
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
                Debug.LogWarning($"[ItemMenu] Falta '{PanelSettingsResourcePath}' en Resources; la " +
                                 "pantalla de items no se podrá abrir.", this);
            }

            _skin = ItemMenuSkin.Load();

            _elementCandidates = Resources.LoadAll<ElementModifier>("Items");
            _trajectoryCandidates = Resources.LoadAll<TrajectoryModifier>("Items");
            _shapeCandidates = Resources.LoadAll<ShapeModifier>("Items");
            _freeCandidates = Resources.LoadAll<FreePoolItemDefinition>("Items");
            _weaponCandidates = Resources.LoadAll<WeaponDefinition>("Items");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.iKey.wasPressedThisFrame)
            {
                if (_open) Close();
                else Open();
                return;
            }

            if (!_open) return;

            if (keyboard.escapeKey.wasPressedThisFrame) Close();

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) Close();
        }

        // ------------------------------------------------------------------ abrir / cerrar

        public void Open()
        {
            if (_open) return;
            if (!EnsureBuilt()) return;

            _loadout = WeaponLoadout.Instance;
            if (_loadout == null)
            {
                Debug.LogWarning("[ItemMenu] No hay WeaponLoadout: no hay inventario que mostrar.", this);
                return;
            }

            _open = true;
            _overlay.style.display = DisplayStyle.Flex;

            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(true);

            var inventory = _loadout.Inventory;
            var synergy = _loadout.Synergy;
            inventory.ItemEquipped += OnInventoryChanged;
            inventory.ItemUnequipped += OnInventoryChanged;
            inventory.WeaponChanged += OnWeaponChanged;
            synergy.PointsChanged += OnPointsChanged;

            ShowDefaultDescription();
            RefreshAll();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;

            _overlay.style.display = DisplayStyle.None;

            if (_loadout != null)
            {
                var inventory = _loadout.Inventory;
                var synergy = _loadout.Synergy;
                inventory.ItemEquipped -= OnInventoryChanged;
                inventory.ItemUnequipped -= OnInventoryChanged;
                inventory.WeaponChanged -= OnWeaponChanged;
                synergy.PointsChanged -= OnPointsChanged;
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ButtonClick");
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(false);
        }

        private void OnInventoryChanged(InventorySlot slot, ItemDefinition item) => RefreshAll();
        private void OnWeaponChanged(WeaponDefinition previous, WeaponDefinition next) => RefreshAll();
        private void OnPointsChanged(BuildTag tag, int oldPoints, int newPoints) => RefreshSynergies();

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

            _overlay = new VisualElement { name = "item-overlay" };
            MenuStyle.FillParent(_overlay);
            _overlay.style.backgroundColor = MenuStyle.Backdrop;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var panel = MenuStyle.Panel();
            panel.style.maxHeight = Length.Percent(94);
            panel.style.width = Length.Percent(94);
            _overlay.Add(panel);

            bool framed = _skin != null && _skin.window.Dress(panel);
            if (framed)
            {
                // Sin cabecera: la X se sienta en la esquina del marco y no le quita altura a las columnas.
                panel.style.maxHeight = Length.Percent(92);

                var close = CloseButton();
                close.style.position = Position.Absolute;
                close.style.top = -close.style.height.value.value * 0.3f;
                close.style.right = -close.style.width.value.value * 0.3f;
                panel.Add(close);
            }
            else
            {
                var header = MenuStyle.Header();
                header.style.justifyContent = Justify.FlexEnd;
                panel.Add(header);
                header.Add(CloseButton());
            }

            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexGrow = 1;
            panel.Add(columns);

            columns.Add(BuildSynergyColumn());
            columns.Add(BuildSlotColumn());
            columns.Add(BuildDescriptionColumn());

            var hint = MenuStyle.Hint("I / Esc / B para cerrar · clic en un slot para ciclar los items disponibles");
            if (framed)
            {
                hint.style.marginTop = 4;
                hint.style.marginBottom = _skin.hintBottomMargin;
            }
            panel.Add(hint);
        }

        private Button CloseButton()
        {
            if (_skin == null || !_skin.closeButton.IsSet) return MenuStyle.CloseButton(Close);

            var sprites = _skin.closeButton;
            var button = new Button(Close) { name = "menu-close" };
            float size = _skin.closeButtonSize;
            button.style.width = size * sprites.Aspect;
            button.style.height = size;
            ClearButtonChrome(button);
            button.style.backgroundImage = new StyleBackground(sprites.normal);

            bool hovered = false, focused = false;
            void Refresh()
            {
                bool on = hovered || focused;
                button.style.backgroundImage = new StyleBackground(sprites.Get(on));
                button.style.scale = new Scale(on ? HoverScale : Vector3.one);
            }

            button.RegisterCallback<PointerEnterEvent>(_ => { hovered = true; Refresh(); });
            button.RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; Refresh(); });
            button.RegisterCallback<FocusEvent>(_ => { focused = true; Refresh(); });
            button.RegisterCallback<BlurEvent>(_ => { focused = false; Refresh(); });
            return button;
        }

        // -- columna izquierda -----------------------------------------------------------------

        private VisualElement BuildSynergyColumn()
        {
            var column = Column("SINERGIAS", SideColumnWidth, _skin != null ? _skin.sidePanel : null);
            _skinnedRows = _skin != null && _skin.synergyRow.IsSet;

            foreach (var tag in SynergyOrder)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6;

                if (_skinnedRows)
                {
                    row.style.height = _skin.synergyRowHeight;
                    _skin.synergyRow.Dress(row);
                    MenuStyle.AddSelectionHighlight(row, 10f, _skin.hoverRing);
                }
                else
                {
                    row.style.paddingTop = 8;
                    row.style.paddingBottom = 8;
                    row.style.paddingLeft = 12;
                    row.style.paddingRight = 12;
                    row.style.backgroundColor = MenuStyle.CellBg;
                    MenuStyle.SetBorder(row, 2, MenuStyle.GoldBorder, 10);
                    MenuStyle.AddSelectionHighlight(row, 10f);
                }

                var name = new Label(BuildTags.DisplayName(tag));
                name.style.fontSize = MenuStyle.BodyFontSize;
                name.style.color = MenuStyle.Cream;
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                row.Add(name);

                var count = new Label("0 ▸ 2");
                count.style.fontSize = MenuStyle.BodyFontSize;
                count.style.color = MenuStyle.Cream;
                row.Add(count);

                var hovered = tag;
                row.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    AudioManager.Instance?.PlaySFX("SFX_ButtonHover");
                    ShowSynergyDescription(hovered);
                });
                row.RegisterCallback<PointerLeaveEvent>(_ => ShowDefaultDescription());

                column.Add(row);
                _synergyRows[tag] = new SynergyRow { Root = row, Count = count };
            }

            return column;
        }

        // -- columna central -----------------------------------------------------------------

        private VisualElement BuildSlotColumn()
        {
            var column = Column("EQUIPO", 0, _skin != null ? _skin.middlePanel : null);
            column.style.flexGrow = 1;
            column.style.marginLeft = 14;
            column.style.marginRight = 14;

            // Con arte, el arma va a la izquierda de la rejilla, a la altura de los dedicados, y un hueco
            // igual a la derecha. Los dos laterales miden y encogen lo mismo y la rejilla no encoge: así
            // la rejilla queda en el centro exacto de la columna en cualquier ancho. Si falta sitio (16:9)
            // el arma desborda un poco su hueco hacia el relleno, sin llegar a los slots.
            VisualElement grid = column, weaponHolder = null;
            if (IsSlotSkinned)
            {
                var body = new VisualElement();
                body.style.flexDirection = FlexDirection.Row;
                body.style.alignItems = Align.FlexStart;
                column.Add(body);

                weaponHolder = new VisualElement();
                weaponHolder.style.width = SlotOuterWidth;
                weaponHolder.style.flexShrink = 1;
                weaponHolder.style.minWidth = 0;
                weaponHolder.style.alignItems = Align.Center;
                body.Add(weaponHolder);

                grid = new VisualElement();
                grid.style.flexGrow = 1;
                grid.style.flexShrink = 0;
                grid.style.alignItems = Align.Center;
                body.Add(grid);

                var spacer = new VisualElement();
                spacer.style.width = SlotOuterWidth;
                spacer.style.flexShrink = 1;
                spacer.style.minWidth = 0;
                body.Add(spacer);
            }

            var dedicated = new VisualElement();
            dedicated.style.flexDirection = FlexDirection.Row;
            dedicated.style.justifyContent = Justify.Center;
            grid.Add(dedicated);

            _elementSlot = MakeSlot("Elemento",
                () => CycleDedicated(ItemSlot.DedicatedElement),
                () => _loadout.Inventory.Element);
            _trajectorySlot = MakeSlot("Trayectoria",
                () => CycleDedicated(ItemSlot.DedicatedTrajectory),
                () => _loadout.Inventory.Trajectory);
            _shapeSlot = MakeSlot("Forma",
                () => CycleDedicated(ItemSlot.DedicatedShape),
                () => _loadout.Inventory.Shape);
            dedicated.Add(_elementSlot.Button);
            dedicated.Add(_trajectorySlot.Button);
            dedicated.Add(_shapeSlot.Button);

            grid.Add(SubHeader("Slots libres"));

            var freeGrid = new VisualElement();
            freeGrid.style.flexDirection = FlexDirection.Row;
            freeGrid.style.flexWrap = Wrap.Wrap;
            freeGrid.style.justifyContent = Justify.Center;
            grid.Add(freeGrid);

            for (int i = 0; i < _freeSlots.Length; i++)
            {
                int index = i;
                _freeSlots[i] = MakeSlot($"Libre {i + 1}",
                    () => CycleFree(index),
                    () => _loadout.Inventory.GetFree(index));
                freeGrid.Add(_freeSlots[i].Button);
            }

            // Con arte, los libres van en dos filas de tres: la misma anchura que los dedicados.
            if (IsSlotSkinned)
            {
                freeGrid.style.maxWidth = 3 * SlotOuterWidth + 1;
                freeGrid.style.alignSelf = Align.Center;
            }

            if (weaponHolder != null)
            {
                _weaponSlot = MakeSlot("— sin arma —", CycleWeapon, () => _loadout.Inventory.Weapon,
                    isWeapon: true, caption: "Arma");
                weaponHolder.Add(_weaponSlot.Button);
                return column;
            }

            column.Add(SubHeader("Arma"));

            var weaponRow = new VisualElement();
            weaponRow.style.flexDirection = FlexDirection.Row;
            weaponRow.style.justifyContent = Justify.Center;
            column.Add(weaponRow);

            _weaponSlot = MakeSlot("— sin arma —", CycleWeapon, () => _loadout.Inventory.Weapon, isWeapon: true);
            _weaponSlot.Button.style.width = 320;
            weaponRow.Add(_weaponSlot.Button);

            return column;
        }

        private Label SubHeader(string text)
        {
            var label = new Label(text);
            label.style.color = MenuStyle.Cream;
            label.style.fontSize = MenuStyle.HintFontSize;
            label.style.marginTop = IsSlotSkinned ? 10 : 18;
            label.style.marginBottom = IsSlotSkinned ? 2 : 4;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }

        private bool IsSlotSkinned => _skin != null && _skin.emptySlot.IsSet;

        /// <summary>Las dos columnas laterales miden lo mismo: si no, la central no queda centrada.</summary>
        private float SideColumnWidth => _skin != null ? _skin.sideColumnWidth : 320f;
        private float SlotOuterWidth => _skin.slotWidth + 8f;

        /// <param name="caption">Rótulo bajo el marco con arte (por defecto, <paramref name="placeholder"/>).</param>
        private SlotButton MakeSlot(string placeholder, System.Action onClick, System.Func<Object> currentItem,
                                    bool isWeapon = false, string caption = null)
        {
            var button = new Button(() => { onClick(); AudioManager.Instance?.PlaySFX("SFX_ButtonClick"); });
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.whiteSpace = WhiteSpace.Normal;

            var slot = new SlotButton
            {
                Button = button, Placeholder = placeholder, Caption = caption ?? placeholder, IsWeapon = isWeapon,
            };

            if (IsSlotSkinned) BuildSkinnedSlot(slot);
            else BuildPlainSlot(slot);

            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                AudioManager.Instance?.PlaySFX("SFX_ButtonHover");
                DescribeHover(currentItem(), placeholder);
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                ClearSynergyHighlights();
                ShowDefaultDescription();
            });

            return slot;
        }

        private static void BuildPlainSlot(SlotButton slot)
        {
            var button = slot.Button;
            button.style.width = 150;
            button.style.height = 68;
            button.style.marginLeft = 6;
            button.style.marginRight = 6;
            button.style.marginTop = 6;
            button.style.marginBottom = 6;
            button.style.backgroundColor = MenuStyle.CellBg;
            MenuStyle.SetBorder(button, 2, MenuStyle.GoldBorder, 12);
            MenuStyle.AddSelectionHighlight(button, 12f);

            var caption = new Label(slot.Placeholder);
            caption.style.fontSize = 13;
            caption.style.color = new Color(MenuStyle.Cream.r, MenuStyle.Cream.g, MenuStyle.Cream.b, 0.55f);
            button.Add(caption);

            var value = new Label("—");
            value.style.fontSize = MenuStyle.CardTitleFontSize;
            value.style.unityFontStyleAndWeight = FontStyle.Bold;
            value.style.color = MenuStyle.Cream;
            value.style.whiteSpace = WhiteSpace.Normal;
            value.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.Add(value);

            slot.Value = value;
        }

        /// <summary>
        /// Slot con arte: marco cuadrado (normal / hover) con el icono del item en su hueco — o su
        /// inicial si no tiene icono — y el tipo de slot + nombre del item debajo.
        /// </summary>
        private void BuildSkinnedSlot(SlotButton slot)
        {
            var button = slot.Button;
            button.style.width = _skin.slotWidth;
            button.style.marginLeft = 4;
            button.style.marginRight = 4;
            button.style.marginTop = 4;
            button.style.marginBottom = 6;
            ClearButtonChrome(button);

            button.style.flexShrink = 0;

            // flexShrink 0: si la columna se queda corta, el marco no se aplasta — se nota antes.
            float size = _skin.slotFrameSize;
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.style.width = size * _skin.emptySlot.Aspect;
            frame.style.height = size;
            frame.style.flexShrink = 0;
            button.Add(frame);

            // Fondo del hueco (sólo se ve por los marcos huecos) y el icono dentro.
            var well = new VisualElement { pickingMode = PickingMode.Ignore };
            well.style.position = Position.Absolute;
            well.style.left = Length.Percent(22);
            well.style.right = Length.Percent(22);
            well.style.top = Length.Percent(22);
            well.style.bottom = Length.Percent(22);
            well.style.backgroundColor = _skin.iconWellColor;
            MenuStyle.SetBorder(well, 0, Color.clear, 6);
            frame.Add(well);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            MenuStyle.FillParent(icon);
            icon.style.marginLeft = icon.style.marginRight = icon.style.marginTop = icon.style.marginBottom = 4;
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            well.Add(icon);

            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            MenuStyle.FillParent(art);
            frame.Add(art);

            var monogram = new Label { pickingMode = PickingMode.Ignore };
            MenuStyle.FillParent(monogram);
            monogram.style.unityTextAlign = TextAnchor.MiddleCenter;
            monogram.style.unityFontStyleAndWeight = FontStyle.Bold;
            monogram.style.fontSize = Mathf.RoundToInt(size * 0.36f);
            frame.Add(monogram);

            var caption = new Label(slot.Caption) { pickingMode = PickingMode.Ignore };
            caption.style.fontSize = 12;
            caption.style.marginTop = 1;
            caption.style.color = new Color(MenuStyle.Cream.r, MenuStyle.Cream.g, MenuStyle.Cream.b, 0.6f);
            button.Add(caption);

            var value = new Label("—") { pickingMode = PickingMode.Ignore };
            value.style.fontSize = 14;
            value.style.unityFontStyleAndWeight = FontStyle.Bold;
            value.style.color = MenuStyle.Cream;
            value.style.unityTextAlign = TextAnchor.MiddleCenter;
            value.style.whiteSpace = WhiteSpace.NoWrap;
            value.style.overflow = Overflow.Hidden;
            value.style.textOverflow = TextOverflow.Ellipsis;
            value.style.maxWidth = _skin.slotWidth;
            button.Add(value);

            slot.Value = value;
            slot.Art = art;
            slot.Icon = icon;
            slot.Monogram = monogram;

            bool hovered = false, focused = false;
            void Refresh()
            {
                slot.Hovered = hovered || focused;
                button.style.scale = new Scale(slot.Hovered ? HoverScale : Vector3.one);
                PaintSlotArt(slot);
            }

            button.RegisterCallback<PointerEnterEvent>(_ => { hovered = true; Refresh(); });
            button.RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; Refresh(); });
            button.RegisterCallback<FocusEvent>(_ => { focused = true; Refresh(); });
            button.RegisterCallback<BlurEvent>(_ => { focused = false; Refresh(); });

            PaintSlotArt(slot);
        }

        private void PaintSlotArt(SlotButton slot)
        {
            if (slot.Art == null) return;

            Sprite icon = null;
            Color accent = MenuStyle.Cream;
            string displayName = null;
            switch (slot.Current)
            {
                case WeaponDefinition weapon:
                    icon = weapon.Icon; accent = weapon.Accent; displayName = weapon.DisplayName;
                    break;
                case ItemDefinition item:
                    icon = item.Icon; accent = item.Accent; displayName = item.DisplayName;
                    break;
            }

            var set = icon != null && _skin.iconSlot.IsSet ? _skin.iconSlot
                : slot.IsWeapon && _skin.weaponSlot.IsSet ? _skin.weaponSlot
                : slot.Current != null && _skin.filledSlot.IsSet ? _skin.filledSlot
                : _skin.emptySlot;

            slot.Art.style.backgroundImage = new StyleBackground(set.Get(slot.Hovered));
            slot.Icon.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);

            slot.Monogram.text = icon == null && !string.IsNullOrEmpty(displayName)
                ? displayName.Substring(0, 1).ToUpperInvariant() : "";
            slot.Monogram.style.color = accent;
        }

        private static void ClearButtonChrome(Button button)
        {
            button.style.backgroundColor = Color.clear;
            MenuStyle.SetBorder(button, 0, Color.clear, 0);
            button.style.paddingLeft = button.style.paddingRight = 0;
            button.style.paddingTop = button.style.paddingBottom = 0;
        }

        // -- columna derecha -----------------------------------------------------------------

        private VisualElement BuildDescriptionColumn()
        {
            var column = Column("DESCRIPCIÓN", SideColumnWidth, _skin != null ? _skin.sidePanel : null);
            _description = new VisualElement();
            _description.style.flexGrow = 1;
            if (_skin != null)
            {
                _description.style.paddingLeft = _skin.descriptionPadding.left;
                _description.style.paddingRight = _skin.descriptionPadding.right;
                _description.style.paddingTop = _skin.descriptionPadding.top;
                _description.style.paddingBottom = _skin.descriptionPadding.bottom;
            }
            column.Add(_description);
            return column;
        }

        // ------------------------------------------------------------------ refresco

        private void RefreshAll()
        {
            RefreshSynergies();
            RefreshSlots();
        }

        private void RefreshSynergies()
        {
            if (_loadout == null) return;
            var synergy = _loadout.Synergy;

            foreach (var tag in SynergyOrder)
            {
                var row = _synergyRows[tag];
                int effective = synergy.EffectivePointsFor(tag);
                int raw = synergy.PointsFor(tag);

                if (synergy.IsCapped(tag))
                {
                    row.Count.text = raw > BuildTags.SynergyCap ? $"6 ✦ (+{raw - BuildTags.SynergyCap})" : "6 ✦";
                    row.Count.style.color = MenuStyle.SelectionHighlight;
                    if (_skinnedRows) UiFrame.Tint(row.Root, _skin.synergyCappedTint);
                    else MenuStyle.SetBorder(row.Root, 2, MenuStyle.SelectionHighlight, 10);
                }
                else
                {
                    int next = synergy.NextThreshold(tag);
                    row.Count.text = $"{effective} ▸ {next}";
                    row.Count.style.color = effective > 0 ? MenuStyle.Cream
                        : new Color(MenuStyle.Cream.r, MenuStyle.Cream.g, MenuStyle.Cream.b, 0.4f);
                    if (_skinnedRows) UiFrame.Tint(row.Root, Color.white);
                    else MenuStyle.SetBorder(row.Root, 2, MenuStyle.GoldBorder, 10);
                }
            }
        }

        private void RefreshSlots()
        {
            if (_loadout == null) return;
            var inventory = _loadout.Inventory;

            SetSlot(_elementSlot, inventory.Element);
            SetSlot(_trajectorySlot, inventory.Trajectory);
            SetSlot(_shapeSlot, inventory.Shape);
            for (int i = 0; i < _freeSlots.Length; i++)
                SetSlot(_freeSlots[i], inventory.GetFree(i));

            var weapon = inventory.Weapon;
            _weaponSlot.Value.text = weapon != null ? weapon.DisplayName : "—";
            if (_weaponSlot.Art != null)
            {
                _weaponSlot.Current = weapon;
                _weaponSlot.Value.style.color = weapon != null ? weapon.Accent : MenuStyle.Cream;
                PaintSlotArt(_weaponSlot);
            }
            else
            {
                MenuStyle.SetBorder(_weaponSlot.Button, weapon != null ? 3 : 2,
                    weapon != null ? weapon.Accent : MenuStyle.GoldBorder, 12);
            }
        }

        private void SetSlot(SlotButton slot, ItemDefinition item)
        {
            slot.Value.text = item != null ? item.DisplayName : "—";
            if (slot.Art != null)
            {
                slot.Current = item;
                slot.Value.style.color = item != null ? item.Accent : MenuStyle.Cream;
                PaintSlotArt(slot);
                return;
            }

            MenuStyle.SetBorder(slot.Button, item != null ? 3 : 2,
                item != null ? item.Accent : MenuStyle.GoldBorder, 12);
        }

        // ------------------------------------------------------------------ descripción (hover)

        private void DescribeHover(Object current, string slotPlaceholder)
        {
            switch (current)
            {
                case WeaponDefinition weapon:
                    ShowWeaponDescription(weapon);
                    break;
                case ItemDefinition item:
                    ShowItemDescription(item);
                    HighlightSynergies(item.Tags);
                    break;
                default:
                    ShowEmptySlotDescription(slotPlaceholder);
                    break;
            }
        }

        private void ShowDefaultDescription()
        {
            _description.Clear();
            _description.Add(Paragraph(
                "Pasa el ratón sobre un item, un modificador o el arma para ver su efecto.\n\n" +
                "Clic en un slot para ciclar entre los assets disponibles de ese tipo."));
        }

        private void ShowEmptySlotDescription(string placeholder)
        {
            _description.Clear();
            _description.Add(DescriptionTitle(placeholder));
            _description.Add(Paragraph("Slot vacío. Clic para equipar."));
        }

        private void ShowSynergyDescription(BuildTag tag)
        {
            _description.Clear();
            _description.Add(DescriptionTitle("Sinergia · " + BuildTags.DisplayName(tag)));

            var synergy = _loadout.Synergy;
            int raw = synergy.PointsFor(tag);
            int effective = synergy.EffectivePointsFor(tag);
            _description.Add(Paragraph($"Puntos: {raw}" +
                (synergy.IsCapped(tag) ? "   (tope 6 alcanzado)" : "")));

            var config = SynergyConfig.Instance;
            int next = synergy.NextThreshold(tag);
            var thresholds = BuildTags.Thresholds;

            for (int i = 0; i < thresholds.Length; i++)
            {
                int threshold = thresholds[i];
                bool active = effective >= threshold;

                string text = config != null ? config.Tier(tag, i + 1) : null;
                if (string.IsNullOrWhiteSpace(text)) text = "(efecto por definir)";

                var line = Paragraph($"{(active ? "✓" : "•")} Umbral {threshold}: {text}");
                line.style.color = active ? MenuStyle.CostAfford
                    : threshold == next ? MenuStyle.SelectionHighlight
                    : new Color(MenuStyle.Cream.r, MenuStyle.Cream.g, MenuStyle.Cream.b, 0.5f);
                _description.Add(line);
            }

            if (tag == BuildTag.Reset)
                _description.Add(Paragraph("Reset no escala por puntos: su sinergia depende de " +
                                           "cuántos items Reset llevas equipados a la vez."));
        }

        private void ShowItemDescription(ItemDefinition item)
        {
            _description.Clear();
            _description.Add(DescriptionTitle(item.DisplayName));

            if (!string.IsNullOrWhiteSpace(item.Description))
                _description.Add(Paragraph(item.Description));

            if (item is WeaponModifier modifier)
                _description.Add(Paragraph(modifier.EffectSummary()));

            foreach (var effect in item.Effects)
                if (effect != null) _description.Add(Paragraph("• " + effect.Summary()));

            _description.Add(TagLine(item.Tags));
        }

        private void ShowWeaponDescription(WeaponDefinition weapon)
        {
            _description.Clear();
            _description.Add(DescriptionTitle(weapon.DisplayName));

            if (!string.IsNullOrWhiteSpace(weapon.Description))
                _description.Add(Paragraph(weapon.Description));

            var shot = weapon.Shot;
            string charge = shot.chargeTime > 0f
                ? $"\nCarga: mantener {shot.chargeTime:0.00}s (mín. {shot.minChargeToFire * 100f:0}% para disparar)"
                : "";
            _description.Add(Paragraph(
                $"Daño base {weapon.BaseDamage:0} · cadencia {weapon.BaseCooldown:0.00}s\n" +
                $"Disparo base: {shot.delivery}, {shot.speed:0} u/s, vida {shot.lifetime:0.0}s\n" +
                $"Elemento innato: {BuildTags.DisplayName(weapon.InnateElement)}" + charge));

            var resolved = _loadout != null ? ShotResolver.Resolve(_loadout.Inventory) : null;
            if (resolved != null)
            {
                _description.Add(DescriptionTitle("Comportamiento actual"));
                _description.Add(Paragraph(DescribeShot(resolved)));
            }

            HighlightSynergies(weapon.ContributedTags());
        }

        private static string DescribeShot(WeaponShot shot)
        {
            string trajectory = shot.homingTurnRate > 0f ? "auto-mira"
                : shot.arcGravity > 0f ? "parábola" : "recto";
            bool beam = shot.delivery == ShotDelivery.Hitscan;
            string shape = beam
                ? (shot.projectileCount > 1 ? $"{shot.projectileCount} haces" : "haz")
                : shot.splitCount > 0 ? $"división en {shot.splitCount}"
                : shot.projectileCount > 1 ? $"{shot.projectileCount} en abanico"
                : "1 proyectil";
            string element = shot.element != ElementId.None
                ? BuildTags.DisplayName(shot.element) : "físico";

            string extra = "";
            if (beam) extra += $" · barre {shot.beamDuration:0.00}s, alcance {shot.beamLength:0}";
            if (shot.burstCount > 1) extra += $" · ráfaga ×{shot.burstCount}";
            if (shot.pierce > 0) extra += $" · perfora {shot.pierce}";
            if (shot.impactRadius > 0f && shot.impactDamage > 0f)
                extra += $" · explota ({shot.impactDamage:0} en {shot.impactRadius:0.0})";

            return $"{trajectory} · {shape} · {element}{extra}";
        }

        // ------------------------------------------------------------------ resaltado de sinergias

        private void HighlightSynergies(IEnumerable<BuildTag> tags)
        {
            ClearSynergyHighlights();
            foreach (var tag in tags)
            {
                if (!_synergyRows.TryGetValue(tag, out var row)) continue;
                if (_skinnedRows)
                {
                    UiFrame.Tint(row.Root, _skin.synergyHighlightTint);
                    continue;
                }
                MenuStyle.SetBorder(row.Root, 3, MenuStyle.SelectionHighlight, 10);
                row.Root.style.backgroundColor = MenuStyle.CellMaxed;
            }
        }

        private void ClearSynergyHighlights()
        {
            // Deja que RefreshSynergies vuelva a poner el borde (o tinte) de "tope" en las que estén a 6.
            foreach (var row in _synergyRows.Values)
            {
                if (_skinnedRows)
                {
                    UiFrame.Tint(row.Root, Color.white);
                    continue;
                }
                MenuStyle.SetBorder(row.Root, 2, MenuStyle.GoldBorder, 10);
                row.Root.style.backgroundColor = MenuStyle.CellBg;
            }
            RefreshSynergies();
        }

        // ------------------------------------------------------------------ ciclar (afordancia de pruebas)

        private void CycleDedicated(ItemSlot slot)
        {
            var inventory = _loadout.Inventory;
            switch (slot)
            {
                case ItemSlot.DedicatedElement:
                {
                    var next = NextCandidate(_elementCandidates, inventory.Element);
                    if (next == null) inventory.UnequipDedicated(slot); else inventory.EquipElement(next);
                    break;
                }
                case ItemSlot.DedicatedTrajectory:
                {
                    var next = NextCandidate(_trajectoryCandidates, inventory.Trajectory);
                    if (next == null) inventory.UnequipDedicated(slot); else inventory.EquipTrajectory(next);
                    break;
                }
                case ItemSlot.DedicatedShape:
                {
                    var next = NextCandidate(_shapeCandidates, inventory.Shape);
                    if (next == null) inventory.UnequipDedicated(slot); else inventory.EquipShape(next);
                    break;
                }
            }
        }

        private void CycleFree(int index)
        {
            var inventory = _loadout.Inventory;
            var next = NextCandidate(_freeCandidates, inventory.GetFree(index));
            inventory.EquipFree(index, next);
        }

        private void CycleWeapon()
        {
            var inventory = _loadout.Inventory;
            inventory.SetWeapon(NextCandidate(_weaponCandidates, inventory.Weapon));
        }

        /// <summary>
        /// Siguiente asset del ciclo [nada, cand[0], cand[1], …, nada, …]. Devolver null significa
        /// "vaciar el slot".
        /// </summary>
        private static T NextCandidate<T>(T[] candidates, T current) where T : Object
        {
            if (candidates == null || candidates.Length == 0) return null;

            int currentIndex = System.Array.IndexOf(candidates, current); // -1 si es null / no está
            int nextIndex = currentIndex + 1;
            return nextIndex >= candidates.Length ? null : candidates[nextIndex];
        }

        // ------------------------------------------------------------------ piezas comunes

        /// <summary>Una columna con su título; con <paramref name="frame"/> lleva marco de arte.</summary>
        private VisualElement Column(string title, float width, UiFrame frame)
        {
            var column = new VisualElement();
            if (width > 0f)
            {
                column.style.width = width;
                column.style.flexShrink = 0;
            }

            if (frame == null || !frame.Dress(column))
            {
                column.style.backgroundColor = new Color(0f, 0f, 0f, 0.18f);
                column.style.paddingTop = 12;
                column.style.paddingBottom = 12;
                column.style.paddingLeft = 12;
                column.style.paddingRight = 12;
                MenuStyle.SetBorder(column, 2, MenuStyle.GoldBorder, 12);
            }

            var header = new Label(title);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = MenuStyle.BodyFontSize;
            header.style.color = MenuStyle.Cream;
            header.style.letterSpacing = 2;

            if (_skin != null && _skin.titleBar.IsSet)
            {
                var bar = TitleBar(header, 0f, _skin.columnTitleHeight);
                bar.style.alignSelf = Align.Stretch;
                bar.style.maxWidth = _skin.columnTitleMaxWidth;
                bar.style.marginLeft = bar.style.marginRight = StyleKeyword.Auto;
                bar.style.marginBottom = 8;
                column.Add(bar);
            }
            else
            {
                header.style.marginBottom = 12;
                column.Add(header);
            }

            return column;
        }

        /// <summary>La barra de título de arte con <paramref name="label"/> centrado dentro.</summary>
        private VisualElement TitleBar(Label label, float width, float height)
        {
            var bar = new VisualElement();
            if (width > 0f) bar.style.width = width;
            bar.style.height = height;
            bar.style.flexShrink = 0;
            bar.style.alignItems = Align.Center;
            bar.style.justifyContent = Justify.Center;
            _skin.titleBar.Dress(bar);

            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginBottom = 0;
            bar.Add(label);
            return bar;
        }

        private static Label DescriptionTitle(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = MenuStyle.CardTitleFontSize;
            label.style.color = MenuStyle.Cream;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 10;
            label.style.marginBottom = 4;
            return label;
        }

        private static Label Paragraph(string text)
        {
            var label = new Label(text);
            label.style.fontSize = MenuStyle.CardDescriptionFontSize;
            label.style.color = new Color(MenuStyle.Cream.r, MenuStyle.Cream.g, MenuStyle.Cream.b, 0.82f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 6;
            return label;
        }

        private static Label TagLine(IReadOnlyList<BuildTag> tags)
        {
            var parts = new List<string>(tags.Count);
            for (int i = 0; i < tags.Count; i++) parts.Add(BuildTags.DisplayName(tags[i]));

            var label = Paragraph("Tags: " + (parts.Count > 0 ? string.Join(" · ", parts) : "—"));
            label.style.color = MenuStyle.SelectionHighlight;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8;
            return label;
        }
    }
}
