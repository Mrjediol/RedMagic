using System.Collections.Generic;
using RedMagic.Core;
using RedMagic.Economy;
using RedMagic.Gameplay;
using RedMagic.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// El HUD de juego, un único panel para todo lo que está siempre en pantalla:
    /// <list type="bullet">
    /// <item><b>arriba a la izquierda</b>: vida y escudo (<see cref="HudHealthBar"/>) y debajo una
    /// casilla por acción con cooldown (<see cref="HudCooldownSlot"/>; en táctil no, el cooldown se
    /// pinta sobre los propios botones: <see cref="TouchControlsController"/>);</item>
    /// <item><b>arriba a la derecha</b>: monedas y rerolls (<see cref="HudCurrency"/>) y las chapas de
    /// items (<see cref="HudBuffRow"/>);</item>
    /// <item><b>abajo a la izquierda</b>: la chuleta de teclas (<see cref="HudControlsLegend"/>), sólo
    /// con teclado y ratón y si no se ha apagado en Opciones.</item>
    /// </list>
    ///
    /// Se auto-crea antes de la primera escena y es persistente (DontDestroyOnLoad): no hay que
    /// ponerlo en ninguna escena. Construye su <see cref="UIDocument"/> en código con
    /// <c>Resources/CurrencyHudPanelSettings</c> (orden 15: sobre los botones táctiles, bajo los menús).
    ///
    /// <b>Una acción nueva con cooldown</b> (el parry) no toca este script: implementa
    /// <see cref="ICooldownSource"/> en un componente del jugador y añade su valor a
    /// <see cref="CooldownSlot"/>; la casilla se crea sola y se esconde mientras nadie la ocupe.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameHud : MonoBehaviour
    {
        public static GameHud Instance { get; private set; }

        private const string PanelSettingsResourcePath = "CurrencyHudPanelSettings";

        private UIDocument _document;
        private bool _built;

        private VisualElement _root, _playerCluster, _slotRow, _rightColumn;
        private HudHealthBar _health;
        private readonly List<HudCooldownSlot> _slots = new();
        private HudCurrency _currency;
        private HudBuffRow _buffs;
        private HudControlsLegend _legend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[GameHud]").AddComponent<GameHud>();
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
                Debug.LogWarning($"[GameHud] Falta '{PanelSettingsResourcePath}' en Resources; el HUD no se dibujará.", this);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            _currency?.Unbind();
            Instance = null;
        }

        /// <summary>
        /// Gasto visible en la tienda: el contador de <paramref name="currency"/> tiembla y baja de
        /// <paramref name="from"/> a <paramref name="to"/> (tiempo real). No toca la moneda.
        /// </summary>
        public static void PlaySpend(Currency currency, int from, int to, float tickDuration,
                                     float shakeAmplitude, float shakeDuration)
        {
            if (Instance == null || Instance._currency == null) return;
            Instance._currency.PlaySpend(currency, from, to, tickDuration, shakeAmplitude, shakeDuration);
        }

        // El UIDocument llena su rootVisualElement de forma perezosa y CurrencyManager puede tardar
        // un frame en existir; se monta cuando ambos están listos.
        private void Update()
        {
            if (!_built) TryBuild();
            if (!_built) return;

            float dt = Time.unscaledDeltaTime;

            // En el menú principal no hay run ni hub: ni vida ni monedas.
            bool inGame = !MainMenuController.IsOpen;
            SetDisplay(_root, inGame);
            if (!inGame) return;

            bool hasPlayer = _health.Tick(dt);
            SetDisplay(_playerCluster, hasPlayer);

            // En táctil los cooldowns van sobre los botones; aquí sólo con teclado/ratón o mando.
            bool slotsHere = hasPlayer && !InputDeviceManager.TouchActive;
            SetDisplay(_slotRow, slotsHere);
            if (slotsHere)
                foreach (var slot in _slots) SetDisplay(slot, slot.Tick(dt));

            _currency.Tick(dt);
            _buffs.Tick();

            // La chuleta: sólo jugando (sin menú delante), con teclado y ratón, y si no está apagada.
            SetDisplay(_legend, HudSettings.ShowControls && InputDeviceManager.KeyboardMouseActive && GameStateManager.CanPlayerAct);
        }

        private static void SetDisplay(VisualElement e, bool show)
        {
            var value = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (e.style.display != value) e.style.display = value;
        }

        // ---------------------------------------------------------------- construcción

        private void TryBuild()
        {
            if (_document == null || _document.panelSettings == null) return;
            if (CurrencyManager.Instance == null) return;   // las monedas necesitan su config para los iconos

            var panelRoot = _document.rootVisualElement;
            if (panelRoot == null) return;

            BuildUi(panelRoot);
            _built = true;
        }

        private void BuildUi(VisualElement panelRoot)
        {
            panelRoot.pickingMode = PickingMode.Ignore;

            _root = new VisualElement { name = "game-hud", pickingMode = PickingMode.Ignore };
            HudStyle.Fill(_root);
            panelRoot.Add(_root);

            // --- arriba a la izquierda: vida + cooldowns
            _playerCluster = new VisualElement { name = "player", pickingMode = PickingMode.Ignore };
            _playerCluster.style.position = Position.Absolute;
            _playerCluster.style.left = HudStyle.ScreenMargin;
            _playerCluster.style.top = HudStyle.ScreenMargin;
            _playerCluster.style.flexDirection = FlexDirection.Column;
            _playerCluster.style.alignItems = Align.FlexStart;
            _root.Add(_playerCluster);

            _health = new HudHealthBar();
            _playerCluster.Add(_health);

            _slotRow = new VisualElement { name = "cooldowns", pickingMode = PickingMode.Ignore };
            _slotRow.style.flexDirection = FlexDirection.Row;
            _slotRow.style.marginTop = 18;
            _playerCluster.Add(_slotRow);

            foreach (CooldownSlot slot in System.Enum.GetValues(typeof(CooldownSlot)))
            {
                var view = new HudCooldownSlot(slot);
                _slots.Add(view);
                _slotRow.Add(view);
            }

            // --- arriba a la derecha: monedas + chapas de items
            _rightColumn = new VisualElement { name = "right", pickingMode = PickingMode.Ignore };
            _rightColumn.style.position = Position.Absolute;
            _rightColumn.style.right = HudStyle.ScreenMargin;
            _rightColumn.style.top = HudStyle.ScreenMargin;
            _rightColumn.style.flexDirection = FlexDirection.Column;
            _rightColumn.style.alignItems = Align.FlexEnd;
            _root.Add(_rightColumn);

            _currency = new HudCurrency();
            _rightColumn.Add(_currency);

            _buffs = new HudBuffRow();
            _rightColumn.Add(_buffs);

            // --- abajo a la izquierda: la chuleta de teclas
            _legend = new HudControlsLegend();
            _legend.style.position = Position.Absolute;
            _legend.style.left = HudStyle.ScreenMargin;
            _legend.style.bottom = HudStyle.ScreenMargin;
            _legend.style.display = DisplayStyle.None;
            _root.Add(_legend);
        }
    }
}
