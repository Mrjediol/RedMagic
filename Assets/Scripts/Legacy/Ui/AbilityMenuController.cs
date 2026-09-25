using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Menú de pruebas de habilidades: se abre con <b>K</b> en cualquier momento, lista todas las
    /// habilidades de <c>Resources/Abilities</c> y al pulsar una la equipa en el jugador.
    ///
    /// Es una herramienta de desarrollo, no una pantalla del juego: existe para probar de un
    /// vistazo cómo se siente cada ataque sin recompilar ni tocar el prefab. Cuando haya sistema de
    /// construcciones de verdad (elegir habilidades durante la run), esto se puede quedar tal cual
    /// detrás de una comprobación de build de desarrollo, o borrarse sin tocar nada más — nada del
    /// sistema de habilidades depende de este archivo.
    ///
    /// Mismo montaje que <see cref="UpgradeMenuController"/> y el antiguo menú de tienda:
    /// se auto-crea, es persistente, construye su UI en código y saca el
    /// <see cref="PanelSettings"/> de Resources; mientras está abierto el juego queda en pausa vía
    /// <see cref="GameStateManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class AbilityMenuController : MonoBehaviour
    {
        public static AbilityMenuController Instance { get; private set; }

        private const string PanelSettingsResourcePath = "AbilityMenuPanelSettings";

        private UIDocument _document;
        private VisualElement _overlay;
        private VisualElement _grid;
        private Label _equippedLabel;
        private bool _open;
        private bool _built;

        private AbilityUser _user;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[AbilityMenu]").AddComponent<AbilityMenuController>();
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
                Debug.LogWarning("[AbilityMenu] Falta '" + PanelSettingsResourcePath +
                                 "' en Resources; el menú de habilidades no se podrá abrir.", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.kKey.wasPressedThisFrame)
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

            _user = FindUser();
            if (_user == null)
            {
                Debug.LogWarning("[AbilityMenu] No hay ningún AbilityUser en la escena: no hay a " +
                                 "quién equiparle la habilidad.", this);
            }

            _open = true;
            _overlay.style.display = DisplayStyle.Flex;

            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(true);

            RebuildList();
        }

        public void Close()
        {
            if (!_open) return;

            _open = false;
            _overlay.style.display = DisplayStyle.None;

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ButtonClick");
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(false);
        }

        /// <summary>
        /// Busca al jugador por su <see cref="AbilityUser"/>. Se busca cada vez que se abre y no se
        /// cachea porque el jugador de la run es un objeto persistente que se crea y se destruye
        /// entre secciones: una referencia guardada apuntaría a un jugador muerto.
        /// </summary>
        private static AbilityUser FindUser()
        {
            var users = FindObjectsByType<AbilityUser>(FindObjectsSortMode.None);
            if (users.Length == 0) return null;

            // Con varios (el jugador de la run más el que viva en la escena cargada), gana el
            // persistente: es el que de verdad está jugando.
            foreach (var user in users)
                if (user.gameObject.scene.name == "DontDestroyOnLoad") return user;

            return users[0];
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

            _overlay = new VisualElement { name = "ability-overlay" };
            MenuStyle.FillParent(_overlay);
            _overlay.style.backgroundColor = MenuStyle.Backdrop;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var panel = MenuStyle.Panel();
            panel.style.maxHeight = Length.Percent(92);
            _overlay.Add(panel);

            var header = MenuStyle.Header();
            panel.Add(header);

            header.Add(MenuStyle.Title("HABILIDADES"));

            _equippedLabel = MenuStyle.CurrencyLabel();
            header.Add(_equippedLabel);

            header.Add(MenuStyle.CloseButton(Close));

            // La rejilla va dentro de un scroll: con 20 habilidades no caben en una pantalla, y
            // esta lista está para crecer.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.maxHeight = 620;
            panel.Add(scroll);

            _grid = new VisualElement();
            _grid.style.flexDirection = FlexDirection.Row;
            _grid.style.flexWrap = Wrap.Wrap;
            _grid.style.justifyContent = Justify.Center;
            scroll.Add(_grid);

            panel.Add(MenuStyle.Hint("K / Esc / B para cerrar · pulsa una habilidad para equiparla"));
        }

        private void RebuildList()
        {
            if (_grid == null) return;

            _grid.Clear();

            var equipped = _user != null ? _user.Equipped : null;
            _equippedLabel.text = equipped != null ? equipped.DisplayName : "Ataque base";

            _grid.Add(BuildBasicAttackCard(equipped == null));

            AbilityCategory? lastCategory = null;
            foreach (var ability in AbilityLibrary.All)
            {
                // Separador de familia: rompe la fila para que cada grupo empiece en una línea.
                if (lastCategory == null || ability.Category != lastCategory.Value)
                {
                    lastCategory = ability.Category;
                    _grid.Add(CategoryHeader(ability.Category));
                }

                _grid.Add(BuildCard(ability, ability == equipped));
            }
        }

        private static VisualElement CategoryHeader(AbilityCategory category)
        {
            var header = new Label(CategoryName(category));
            header.style.width = Length.Percent(100);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = MenuStyle.BodyFontSize;
            header.style.color = MenuStyle.Cream;
            header.style.marginTop = 14;
            header.style.marginBottom = 4;
            header.style.paddingLeft = 8;
            return header;
        }

        private static string CategoryName(AbilityCategory category) => category switch
        {
            AbilityCategory.Melee => "CUERPO A CUERPO",
            AbilityCategory.Ranged => "A DISTANCIA",
            AbilityCategory.Area => "ÁREA / TERRENO",
            _ => "UTILIDAD"
        };

        private VisualElement BuildBasicAttackCard(bool isEquipped)
        {
            var card = new Button(() => Equip(null));
            StyleCard(card, isEquipped, MenuStyle.Cream);

            card.Add(MenuStyle.CardTitle("Ataque base"));
            card.Add(MenuStyle.CardDescription("La espada de siempre (PlayerAttack)."));

            var footer = MenuStyle.CardFooter(isEquipped ? "EQUIPADA" : "equipar");
            footer.style.color = isEquipped ? MenuStyle.CostAfford : MenuStyle.Cream;
            card.Add(footer);

            return card;
        }

        private VisualElement BuildCard(AbilityDefinition ability, bool isEquipped)
        {
            var card = new Button(() => Equip(ability));
            StyleCard(card, isEquipped, ability.Accent);

            if (ability.Icon != null)
            {
                var icon = new Image { sprite = ability.Icon, scaleMode = ScaleMode.ScaleToFit };
                icon.style.width = 48;
                icon.style.height = 48;
                card.Add(icon);
            }

            card.Add(MenuStyle.CardTitle(ability.DisplayName));
            card.Add(MenuStyle.CardDescription(ability.Description));

            var stats = MenuStyle.CardFooter(ability.ShortStats());
            stats.style.fontSize = 14;
            stats.style.whiteSpace = WhiteSpace.Normal;
            stats.style.unityTextAlign = TextAnchor.MiddleCenter;
            stats.style.color = isEquipped ? MenuStyle.CostAfford : ability.Accent;
            card.Add(stats);

            return card;
        }

        private static void StyleCard(Button card, bool isEquipped, Color accent)
        {
            MenuStyle.Card(card);
            card.style.height = 200;
            card.style.backgroundColor = isEquipped ? MenuStyle.CellBuyable : MenuStyle.CellBg;
            MenuStyle.SetBorder(card, isEquipped ? 3 : 2,
                                isEquipped ? accent : MenuStyle.GoldBorder, 12);
            card.RegisterCallback<PointerEnterEvent>(_ => AudioManager.Instance?.PlaySFX("SFX_ButtonHover"));
        }

        private void Equip(AbilityDefinition ability)
        {
            if (_user == null) _user = FindUser();
            if (_user == null) return;

            _user.Equip(ability);
            AudioManager.Instance?.PlaySFX("SFX_ButtonClick");

            // Se repinta en vez de cerrarse: probar es cambiar de habilidad varias veces seguidas,
            // y cerrar el menú en cada cambio obligaría a reabrirlo con K cada vez.
            RebuildList();
        }
    }
}
