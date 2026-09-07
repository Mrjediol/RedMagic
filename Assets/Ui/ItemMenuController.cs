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

        private UIDocument _document;
        private VisualElement _overlay;
        private bool _open;
        private bool _built;

        private WeaponLoadout _loadout;

        // Columna izquierda
        private readonly Dictionary<BuildTag, SynergyRow> _synergyRows = new Dictionary<BuildTag, SynergyRow>();

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

            var header = MenuStyle.Header();
            panel.Add(header);
            header.Add(MenuStyle.Title("ITEMS"));
            header.Add(MenuStyle.CloseButton(Close));

            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexGrow = 1;
            panel.Add(columns);

            columns.Add(BuildSynergyColumn());
            columns.Add(BuildSlotColumn());
            columns.Add(BuildDescriptionColumn());

            panel.Add(MenuStyle.Hint("I / Esc / B para cerrar · clic en un slot para ciclar los items disponibles"));
        }

        // -- columna izquierda -----------------------------------------------------------------

        private VisualElement BuildSynergyColumn()
        {
            var column = Column("SINERGIAS", 300);

            foreach (var tag in SynergyOrder)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 8;
                row.style.paddingBottom = 8;
                row.style.paddingLeft = 12;
                row.style.paddingRight = 12;
                row.style.marginBottom = 6;
                row.style.backgroundColor = MenuStyle.CellBg;
                MenuStyle.SetBorder(row, 2, MenuStyle.GoldBorder, 10);

                var name = new Label(BuildTags.DisplayName(tag));
                name.style.fontSize = MenuStyle.BodyFontSize;
                name.style.color = MenuStyle.Cream;
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                row.Add(name);

                var count = new Label("0 ▸ 2");
                count.style.fontSize = MenuStyle.BodyFontSize;
                count.style.color = MenuStyle.Cream;
                row.Add(count);

                MenuStyle.AddSelectionHighlight(row, 10f);

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
            var column = Column("EQUIPO", 0);
            column.style.flexGrow = 1;
            column.style.marginLeft = 14;
            column.style.marginRight = 14;

            var dedicated = new VisualElement();
            dedicated.style.flexDirection = FlexDirection.Row;
            dedicated.style.justifyContent = Justify.Center;
            column.Add(dedicated);

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

            var freeHeader = new Label("Slots libres");
            freeHeader.style.color = MenuStyle.Cream;
            freeHeader.style.fontSize = MenuStyle.HintFontSize;
            freeHeader.style.marginTop = 18;
            freeHeader.style.marginBottom = 4;
            freeHeader.style.unityTextAlign = TextAnchor.MiddleCenter;
            column.Add(freeHeader);

            var freeGrid = new VisualElement();
            freeGrid.style.flexDirection = FlexDirection.Row;
            freeGrid.style.flexWrap = Wrap.Wrap;
            freeGrid.style.justifyContent = Justify.Center;
            column.Add(freeGrid);

            for (int i = 0; i < _freeSlots.Length; i++)
            {
                int index = i;
                _freeSlots[i] = MakeSlot($"Libre {i + 1}",
                    () => CycleFree(index),
                    () => _loadout.Inventory.GetFree(index));
                freeGrid.Add(_freeSlots[i].Button);
            }

            var weaponHeader = new Label("Arma");
            weaponHeader.style.color = MenuStyle.Cream;
            weaponHeader.style.fontSize = MenuStyle.HintFontSize;
            weaponHeader.style.marginTop = 18;
            weaponHeader.style.marginBottom = 4;
            weaponHeader.style.unityTextAlign = TextAnchor.MiddleCenter;
            column.Add(weaponHeader);

            var weaponRow = new VisualElement();
            weaponRow.style.flexDirection = FlexDirection.Row;
            weaponRow.style.justifyContent = Justify.Center;
            column.Add(weaponRow);

            _weaponSlot = MakeSlot("— sin arma —", CycleWeapon, () => _loadout.Inventory.Weapon);
            _weaponSlot.Button.style.width = 320;
            weaponRow.Add(_weaponSlot.Button);

            return column;
        }

        private SlotButton MakeSlot(string placeholder, System.Action onClick, System.Func<Object> currentItem)
        {
            var button = new Button(() => { onClick(); AudioManager.Instance?.PlaySFX("SFX_ButtonClick"); });
            button.style.width = 150;
            button.style.height = 68;
            button.style.marginLeft = 6;
            button.style.marginRight = 6;
            button.style.marginTop = 6;
            button.style.marginBottom = 6;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.backgroundColor = MenuStyle.CellBg;
            MenuStyle.SetBorder(button, 2, MenuStyle.GoldBorder, 12);
            MenuStyle.AddSelectionHighlight(button, 12f);

            var caption = new Label(placeholder);
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

            var slot = new SlotButton { Button = button, Value = value, Placeholder = placeholder };

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

        // -- columna derecha -----------------------------------------------------------------

        private VisualElement BuildDescriptionColumn()
        {
            var column = Column("DESCRIPCIÓN", 340);
            _description = new VisualElement();
            _description.style.flexGrow = 1;
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
                    MenuStyle.SetBorder(row.Root, 2, MenuStyle.SelectionHighlight, 10);
                }
                else
                {
                    int next = synergy.NextThreshold(tag);
                    row.Count.text = $"{effective} ▸ {next}";
                    row.Count.style.color = effective > 0 ? MenuStyle.Cream
                        : new Color(MenuStyle.Cream.r, MenuStyle.Cream.g, MenuStyle.Cream.b, 0.4f);
                    MenuStyle.SetBorder(row.Root, 2, MenuStyle.GoldBorder, 10);
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
            MenuStyle.SetBorder(_weaponSlot.Button, weapon != null ? 3 : 2,
                weapon != null ? weapon.Accent : MenuStyle.GoldBorder, 12);
        }

        private static void SetSlot(SlotButton slot, ItemDefinition item)
        {
            slot.Value.text = item != null ? item.DisplayName : "—";
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

            _description.Add(TagLine(item.Tags));
        }

        private void ShowWeaponDescription(WeaponDefinition weapon)
        {
            _description.Clear();
            _description.Add(DescriptionTitle(weapon.DisplayName));

            if (!string.IsNullOrWhiteSpace(weapon.Description))
                _description.Add(Paragraph(weapon.Description));

            var shot = weapon.Shot;
            _description.Add(Paragraph(
                $"Daño base {weapon.BaseDamage:0} · cadencia {weapon.BaseCooldown:0.00}s\n" +
                $"Disparo base: {shot.delivery}, {shot.speed:0} u/s, vida {shot.lifetime:0.0}s\n" +
                $"Elemento innato: {BuildTags.DisplayName(weapon.InnateElement)}"));

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
            string trajectory = shot.homingTurnRate > 0f ? "auto-mira" : "recto";
            string shape = shot.splitCount > 0 ? $"división en {shot.splitCount}"
                : shot.projectileCount > 1 ? $"{shot.projectileCount} en abanico"
                : "1 proyectil";
            string element = shot.element != ElementId.None
                ? BuildTags.DisplayName(shot.element) : "físico";
            return $"{trajectory} · {shape} · {element}";
        }

        // ------------------------------------------------------------------ resaltado de sinergias

        private void HighlightSynergies(IEnumerable<BuildTag> tags)
        {
            ClearSynergyHighlights();
            foreach (var tag in tags)
            {
                if (!_synergyRows.TryGetValue(tag, out var row)) continue;
                MenuStyle.SetBorder(row.Root, 3, MenuStyle.SelectionHighlight, 10);
                row.Root.style.backgroundColor = MenuStyle.CellMaxed;
            }
        }

        private void ClearSynergyHighlights()
        {
            // Deja que RefreshSynergies vuelva a poner el borde de "tope" en las que estén a 6.
            foreach (var row in _synergyRows.Values)
            {
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

        private static VisualElement Column(string title, float width)
        {
            var column = new VisualElement();
            if (width > 0f)
            {
                column.style.width = width;
                column.style.flexShrink = 0;
            }
            column.style.backgroundColor = new Color(0f, 0f, 0f, 0.18f);
            column.style.paddingTop = 12;
            column.style.paddingBottom = 12;
            column.style.paddingLeft = 12;
            column.style.paddingRight = 12;
            MenuStyle.SetBorder(column, 2, MenuStyle.GoldBorder, 12);

            var header = new Label(title);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = MenuStyle.BodyFontSize;
            header.style.color = MenuStyle.Cream;
            header.style.letterSpacing = 2;
            header.style.marginBottom = 12;
            column.Add(header);

            return column;
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
