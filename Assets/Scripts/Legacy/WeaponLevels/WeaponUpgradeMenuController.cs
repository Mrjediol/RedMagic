using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Economy;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Menú del altar que suelta el jefe: sube de nivel <b>el arma que llevas equipada</b>, pagando
    /// con calaveras.
    ///
    /// Es a propósito una pantalla mínima — una sola carta, un solo botón —: la decisión no es qué
    /// comprar sino si gastar las calaveras ahora o guardarlas, y una rejilla de opciones sólo
    /// taparía eso. La lista de mejoras por nivel sale del propio asset del arma
    /// (<see cref="AbilityDefinition.TierSummary"/>), así que cada arma enseña lo suyo sin que este
    /// archivo sepa nada de ninguna.
    ///
    /// Mismo montaje que los demás menús construidos en código: se auto-crea, es persistente, saca
    /// su <see cref="PanelSettings"/> de Resources y pausa el juego mientras está abierto.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponUpgradeMenuController : MonoBehaviour
    {
        public static WeaponUpgradeMenuController Instance { get; private set; }

        private const string PanelSettingsResourcePath = "WeaponUpgradeMenuPanelSettings";

        private UIDocument _document;
        private VisualElement _overlay;
        private VisualElement _body;
        private Label _skullLabel;
        private bool _open;
        private bool _built;

        private AbilityUser _user;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[WeaponUpgradeMenu]").AddComponent<WeaponUpgradeMenuController>();
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
                Debug.LogWarning("[WeaponUpgradeMenu] Falta '" + PanelSettingsResourcePath +
                                 "' en Resources; el altar no se podrá abrir.", this);
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
            if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) Close();
        }

        // ------------------------------------------------------------------ abrir / cerrar

        public void Open(AbilityUser user)
        {
            if (_open) return;
            if (!EnsureBuilt()) return;

            _user = user != null ? user : FindUser();
            _open = true;
            _overlay.style.display = DisplayStyle.Flex;

            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(true);

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Changed -= OnCurrencyChanged;
                CurrencyManager.Instance.Changed += OnCurrencyChanged;
            }

            Rebuild();
        }

        public void Close()
        {
            if (!_open) return;

            _open = false;
            _overlay.style.display = DisplayStyle.None;

            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed -= OnCurrencyChanged;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("SFX_ButtonClick");
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(false);
        }

        private void OnCurrencyChanged(Currency currency, int amount)
        {
            if (currency == Currency.Skull) Rebuild();
        }

        private static AbilityUser FindUser()
        {
            var users = FindObjectsByType<AbilityUser>(FindObjectsSortMode.None);
            if (users.Length == 0) return null;

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

            _overlay = new VisualElement { name = "weapon-upgrade-overlay" };
            MenuStyle.FillParent(_overlay);
            _overlay.style.backgroundColor = MenuStyle.Backdrop;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var panel = MenuStyle.Panel();
            panel.style.minWidth = 560;
            _overlay.Add(panel);

            var header = MenuStyle.Header();
            panel.Add(header);

            header.Add(MenuStyle.Title("FORJA"));

            _skullLabel = MenuStyle.CurrencyLabel();
            header.Add(_skullLabel);

            header.Add(MenuStyle.CloseButton(Close));

            _body = new VisualElement();
            _body.style.alignItems = Align.Center;
            panel.Add(_body);

            panel.Add(MenuStyle.Hint("Esc / E / B para cerrar"));
        }

        private void Rebuild()
        {
            if (_body == null) return;

            _body.Clear();

            int skulls = CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(Currency.Skull) : 0;
            _skullLabel.text = $"Calaveras: {skulls}";

            var ability = _user != null ? _user.Equipped : null;
            var levels = AbilityLevelManager.Instance;

            if (ability == null)
            {
                _body.Add(Message("No llevas ningún arma equipada.\nLa forja mejora el arma que " +
                                  "tengas puesta: equipa una y vuelve."));
                return;
            }

            int level = levels != null ? levels.GetLevel(ability) : 1;
            int cost = levels != null ? levels.CostToUpgrade(ability) : -1;

            _body.Add(WeaponHeader(ability, level));

            if (cost < 0)
            {
                _body.Add(Message($"{ability.DisplayName} ya está al nivel máximo ({AbilityDefinition.MaxLevel})."));
                return;
            }

            _body.Add(NextLevelCard(ability, level + 1, cost, skulls));
        }

        private static VisualElement WeaponHeader(AbilityDefinition ability, int level)
        {
            var box = new VisualElement();
            box.style.alignItems = Align.Center;
            box.style.marginBottom = 14;

            var title = MenuStyle.CardTitle($"{ability.DisplayName}  ·  Nivel {level}");
            title.style.fontSize = 26;
            title.style.color = ability.Accent;
            box.Add(title);

            var stats = MenuStyle.CardDescription(ability.ShortStats());
            stats.style.fontSize = 16;
            box.Add(stats);

            return box;
        }

        private VisualElement NextLevelCard(AbilityDefinition ability, int nextLevel, int cost, int skulls)
        {
            bool affordable = skulls >= cost;

            var card = new Button(() => Buy(ability));
            MenuStyle.Card(card);
            card.style.width = 420;
            card.style.height = 170;
            card.style.backgroundColor = affordable ? MenuStyle.CellBuyable : MenuStyle.CellBg;
            card.SetEnabled(affordable);
            card.RegisterCallback<PointerEnterEvent>(_ => AudioManager.Instance?.PlaySFX("SFX_ButtonHover"));

            card.Add(MenuStyle.CardTitle($"Subir a nivel {nextLevel}"));
            card.Add(MenuStyle.CardDescription(ability.TierSummary(nextLevel)));

            var price = MenuStyle.CardFooter($"{cost} calavera{(cost == 1 ? "" : "s")}");
            price.style.color = affordable ? MenuStyle.CostAfford : MenuStyle.CostTooDear;
            card.Add(price);

            return card;
        }

        private static Label Message(string text)
        {
            var label = new Label(text);
            label.style.fontSize = MenuStyle.BodyFontSize;
            label.style.color = MenuStyle.Cream;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingTop = 30;
            label.style.paddingBottom = 30;
            return label;
        }

        private void Buy(AbilityDefinition ability)
        {
            var levels = AbilityLevelManager.Instance;
            if (levels == null || !levels.TryUpgrade(ability)) return;

            AudioManager.Instance?.PlaySFX("SFX_ButtonClick");

            // TryUpgrade ya ha cobrado (lo que dispara Changed y repinta), pero se repinta también
            // aquí por si la subida sale gratis y no hay cambio de moneda que escuchar.
            Rebuild();
        }
    }
}
