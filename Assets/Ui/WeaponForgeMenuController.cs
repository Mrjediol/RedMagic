using RedMagic.Audio;
using RedMagic.Core;
using RedMagic.Economy;
using RedMagic.Items;
using RedMagic.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Menú del altar que suelta el jefe: sube de nivel <b>el arma que llevas equipada</b> del
    /// sistema de items, pagando con calaveras.
    ///
    /// Reemplaza al viejo <c>WeaponUpgradeMenuController</c> (ver
    /// <c>Assets/Scripts/Legacy/WeaponLevels/</c>), que leía <c>AbilityUser.Equipped</c>. Es a
    /// propósito una pantalla mínima — una sola carta, un solo botón —: la decisión no es qué
    /// comprar sino si gastar las calaveras ahora o guardarlas.
    ///
    /// <b>Placeholder de nivel</b>: hasta decidir el efecto real del nivel 2 y 3, el texto de cada
    /// nivel sólo describe el tinte visual que aplica <see cref="WeaponLevelManager"/> — no hay
    /// cambio de daño/tamaño/cooldown todavía.
    ///
    /// Mismo montaje que los demás menús construidos en código: se auto-crea, es persistente, saca
    /// su <see cref="PanelSettings"/> de Resources y pausa el juego mientras está abierto.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponForgeMenuController : MonoBehaviour
    {
        public static WeaponForgeMenuController Instance { get; private set; }

        private const string PanelSettingsResourcePath = "WeaponForgeMenuPanelSettings";

        private UIDocument _document;
        private VisualElement _overlay;
        private VisualElement _body;
        private Label _skullLabel;
        private bool _open;
        private bool _built;

        private WeaponUser _user;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[WeaponForgeMenu]").AddComponent<WeaponForgeMenuController>();
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
                Debug.LogWarning("[WeaponForgeMenu] Falta '" + PanelSettingsResourcePath +
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

        public void Open(WeaponUser user)
        {
            if (_open) return;
            if (!EnsureBuilt()) return;

            _user = user != null ? user : FindUser();
            _open = true;
            SystemSounds.Play(s => s.weaponForgeMenu.open);
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
            SystemSounds.Play(s => s.weaponForgeMenu.close);
            _overlay.style.display = DisplayStyle.None;

            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed -= OnCurrencyChanged;
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetPaused(false);
        }

        private void OnCurrencyChanged(Currency currency, int amount)
        {
            if (currency == Currency.Skull) Rebuild();
        }

        private static WeaponUser FindUser()
        {
            var users = FindObjectsByType<WeaponUser>(FindObjectsSortMode.None);
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
            UiSounds.Bind(root);
            if (root == null) return false;

            BuildUi(root);
            _built = true;
            return true;
        }

        private void BuildUi(VisualElement root)
        {
            MenuStyle.FillParent(root);

            _overlay = new VisualElement { name = "weapon-forge-overlay" };
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

            var title = MenuStyle.Title("");
            LocalizedUi.Bind(title, "forge.title");
            header.Add(title);

            _skullLabel = MenuStyle.CurrencyLabel();
            header.Add(_skullLabel);

            header.Add(MenuStyle.CloseButton(Close));

            _body = new VisualElement();
            _body.style.alignItems = Align.Center;
            panel.Add(_body);

            var hint = MenuStyle.Hint("");
            LocalizedUi.Bind(hint, "common.close_hint");
            panel.Add(hint);
        }

        private void Rebuild()
        {
            if (_body == null) return;

            _body.Clear();

            int skulls = CurrencyManager.Instance != null ? CurrencyManager.Instance.Get(Currency.Skull) : 0;
            _skullLabel.text = Loc.Get("forge.skulls", skulls);

            var weapon = _user != null ? _user.Weapon : null;
            var levels = WeaponLevelManager.Instance;

            if (weapon == null)
            {
                _body.Add(Message(Loc.Get("forge.no_weapon")));
                return;
            }

            int level = levels != null ? levels.GetLevel(weapon) : 1;
            int cost = levels != null ? levels.CostToUpgrade(weapon) : -1;

            _body.Add(WeaponHeader(weapon, level));

            if (cost < 0)
            {
                _body.Add(Message(Loc.Get("forge.max_level", weapon.DisplayName, WeaponLevelManager.MaxLevel)));
                return;
            }

            _body.Add(NextLevelCard(weapon, level + 1, cost, skulls));
        }

        private static VisualElement WeaponHeader(WeaponDefinition weapon, int level)
        {
            var box = new VisualElement();
            box.style.alignItems = Align.Center;
            box.style.marginBottom = 14;

            var title = MenuStyle.CardTitle(Loc.Get("forge.weapon_level", weapon.DisplayName, level));
            title.style.fontSize = 26;
            title.style.color = weapon.Accent;
            box.Add(title);

            var stats = MenuStyle.CardDescription(
                Loc.Get("forge.stats", weapon.BaseDamage, weapon.BaseCooldown));
            stats.style.fontSize = 16;
            box.Add(stats);

            return box;
        }

        private VisualElement NextLevelCard(WeaponDefinition weapon, int nextLevel, int cost, int skulls)
        {
            bool affordable = skulls >= cost;

            var card = new Button(() => Buy(weapon));
            MenuStyle.Card(card);
            card.style.width = 420;
            card.style.height = 170;
            card.style.backgroundColor = affordable ? MenuStyle.CellBuyable : MenuStyle.CellBg;
            card.SetEnabled(affordable);
            card.AddToClassList(UiSounds.NoClickClass);   // suena sólo si la mejora se paga

            card.Add(MenuStyle.CardTitle(Loc.Get("forge.upgrade_to", nextLevel)));
            card.Add(MenuStyle.CardDescription(LevelSummary(nextLevel)));

            var price = MenuStyle.CardFooter(Loc.Get(cost == 1 ? "forge.cost_one" : "forge.cost_many", cost));
            price.style.color = affordable ? MenuStyle.CostAfford : MenuStyle.CostTooDear;
            card.Add(price);

            return card;
        }

        /// <summary>
        /// Placeholder: hasta que se decida el efecto real de cada nivel, sólo describe el tinte
        /// que <see cref="WeaponLevelManager"/> aplica al disparo (ver <c>ShotResolver</c>).
        /// </summary>
        private static string LevelSummary(int level) => level switch
        {
            2 => Loc.Get("forge.level2_summary"),
            3 => Loc.Get("forge.level3_summary"),
            _ => "",
        };

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

        private void Buy(WeaponDefinition weapon)
        {
            var levels = WeaponLevelManager.Instance;
            if (levels == null || !levels.TryUpgrade(weapon))
            {
                UiSounds.Deny();
                return;
            }

            SystemSounds.Play(s => s.weaponLevelUp);

            // TryUpgrade ya ha cobrado (lo que dispara Changed y repinta), pero se repinta también
            // aquí por si la subida sale gratis y no hay cambio de moneda que escuchar.
            Rebuild();
        }
    }
}
