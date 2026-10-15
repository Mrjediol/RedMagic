using System;
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
    /// El HUD de juego, un único panel para todo lo que está siempre en pantalla. Cuatro piezas que
    /// el jugador puede recolocar (<see cref="HudElement"/>; de fábrica, entre paréntesis):
    /// <list type="bullet">
    /// <item><b>Vida</b> (arriba izq.): vida y escudo, <see cref="HudHealthBar"/>.</item>
    /// <item><b>Cooldowns</b> (bajo la vida): una <see cref="HudCooldownSlot"/> por acción. En táctil
    /// no: el cooldown se pinta sobre los propios botones (<see cref="TouchControlsController"/>).</item>
    /// <item><b>Monedas</b> (arriba der.): <see cref="HudCurrency"/> y debajo las chapas de items
    /// (<see cref="HudBuffRow"/>).</item>
    /// <item><b>Controles</b> (abajo izq.): <see cref="HudControlsLegend"/>, sólo con teclado y ratón.</item>
    /// </list>
    /// Más dos piezas de enemigos, según el modo de Opciones (<see cref="HudEnemyBars"/>):
    /// <b>Foco</b> (arriba al centro, bajo la del jefe) y <b>Lista</b> (derecha); el modo flotante va
    /// en una capa que sigue a la cámara y no se recoloca.
    /// Detrás de todo, <see cref="HudScreenFx"/>: bordes rojos al recibir daño y con poca vida.
    ///
    /// Las posiciones salen de <see cref="HudSettings"/> (anclas relativas a la pantalla) y se
    /// editan con <see cref="BeginLayoutEdit"/> (Opciones ▸ Interfaz), que abre
    /// <see cref="HudLayoutEditor"/>.
    ///
    /// Se auto-crea antes de la primera escena y es persistente (DontDestroyOnLoad). Construye su
    /// <see cref="UIDocument"/> en código con <c>Resources/CurrencyHudPanelSettings</c> (orden 15:
    /// sobre los botones táctiles, bajo la pausa (20) y Opciones (25)).
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

        /// <summary>Hueco entre la barra de vida y la fila de cooldowns, en su sitio de fábrica.</summary>
        private const float CooldownsGap = 18f;

        private UIDocument _document;
        private bool _built;

        private VisualElement _root, _slotRow, _rightColumn;
        private HudScreenFx _screenFx;
        private HudHealthBar _health;
        private readonly List<HudCooldownSlot> _slots = new();
        private HudCurrency _currency;
        private HudBuffRow _buffs;
        private HudControlsLegend _legend;
        private HudEnemyBars _enemyBars;
        private HudLayoutEditor _editor;
        private readonly Dictionary<HudElement, VisualElement> _movable = new();

        private Action _onLayoutDone;

        /// <summary>True mientras el jugador está colocando las piezas del HUD.</summary>
        public static bool IsEditingLayout => Instance != null && Instance._editor != null && Instance._editor.IsOpen;

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
            _screenFx?.Unbind();
            Instance = null;
        }

        // ---------------------------------------------------------------- API

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

        /// <summary>
        /// Abre el modo "colocar HUD". <paramref name="onDone"/> se llama al terminar (botón Listo,
        /// Escape o <see cref="EndLayoutEdit"/>). Devuelve false si el HUD aún no está montado.
        /// </summary>
        public static bool BeginLayoutEdit(Action onDone)
        {
            if (Instance == null || !Instance._built) return false;
            Instance._onLayoutDone = onDone;
            Instance._editor.Open();
            return true;
        }

        /// <summary>Cierra el modo "colocar HUD" (lo que ya se soltó está guardado).</summary>
        public static void EndLayoutEdit()
        {
            if (!IsEditingLayout) return;
            var hud = Instance;
            hud._editor.Close();
            var done = hud._onLayoutDone;
            hud._onLayoutDone = null;
            done?.Invoke();
        }

        // ---------------------------------------------------------------- frame

        // El UIDocument llena su rootVisualElement de forma perezosa y CurrencyManager puede tardar
        // un frame en existir; se monta cuando ambos están listos.
        private void Update()
        {
            if (!_built) TryBuild();
            if (!_built) return;

            float dt = Time.unscaledDeltaTime;
            bool editing = _editor.IsOpen;

            // En el menú principal no hay run ni hub: ni vida ni monedas (salvo colocándolas).
            bool inGame = !MainMenuController.IsOpen || editing;
            SetDisplay(_root, inGame);
            if (!inGame) return;

            _screenFx.Tick(dt);

            bool hasPlayer = _health.Tick(dt);
            if (!hasPlayer && editing) _health.ShowPreview();
            SetDisplay(_health, hasPlayer || editing);

            // En táctil los cooldowns van sobre los botones; aquí sólo con teclado/ratón o mando.
            bool slotsHere = editing || (hasPlayer && !InputDeviceManager.TouchActive);
            SetDisplay(_slotRow, slotsHere);
            if (slotsHere)
                foreach (var slot in _slots)
                    SetDisplay(slot, slot.Tick(dt) || (editing && slot.Slot != CooldownSlot.Parry));

            _currency.Tick(dt);
            _buffs.Tick();

            var (enemyFocus, enemyList) = _enemyBars.Tick(dt, editing);
            SetDisplay(_enemyBars.Focus, enemyFocus);
            SetDisplay(_enemyBars.List, enemyList);

            // La chuleta: jugando (sin menú delante), con teclado y ratón, y si no está apagada.
            SetDisplay(_legend, editing || (HudSettings.ShowControls && InputDeviceManager.KeyboardMouseActive && GameStateManager.CanPlayerAct));

            ApplyLayout();
            if (editing) _editor.Tick();
        }

        private static void SetDisplay(VisualElement e, bool show)
        {
            var value = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (e.style.display != value) e.style.display = value;
        }

        // ---------------------------------------------------------------- colocación

        private void ApplyLayout()
        {
            var area = _root.layout;
            if (float.IsNaN(area.width) || area.width <= 0f) return;

            foreach (var (element, view) in _movable)
            {
                if (view == _editor.Dragging) continue;

                var size = view.layout;
                if (float.IsNaN(size.width) || size.width <= 0f) continue;

                var anchor = HudSettings.TryGetAnchor(element, out var saved) ? saved : DefaultAnchor(element, area);
                var place = PlaceFromAnchor(anchor, area, size.size);
                float x = place.x, y = place.y;

                if (Mathf.Abs(view.resolvedStyle.left - x) > 0.5f) view.style.left = x;
                if (Mathf.Abs(view.resolvedStyle.top - y) > 0.5f) view.style.top = y;
                if (view.style.visibility != Visibility.Visible) view.style.visibility = Visibility.Visible;
            }
        }

        /// <summary>El sitio de fábrica de cada pieza, a partir de los márgenes de <see cref="HudStyle"/>.</summary>
        private static HudAnchor DefaultAnchor(HudElement element, Rect area)
        {
            float m = HudStyle.ScreenMargin;
            float healthTop = m + HudStyle.ShieldHeight + 5f;   // deja sitio al escudo, que va encima
            float w = area.width, h = area.height;

            return element switch
            {
                HudElement.Health => new HudAnchor(Vector2.zero, new Vector2(m / w, healthTop / h)),
                HudElement.Cooldowns => new HudAnchor(Vector2.zero, new Vector2(m / w, (healthTop + HudStyle.HealthHeight + CooldownsGap) / h)),
                HudElement.Currency => new HudAnchor(new Vector2(1f, 0f), new Vector2(1f - m / w, m / h)),
                HudElement.Controls => new HudAnchor(new Vector2(0f, 1f), new Vector2(m / w, 1f - m / h)),
                // Bajo la barra del jefe (arriba al centro), para que no se pisen en un combate con esbirros.
                HudElement.EnemyFocus => new HudAnchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.15f)),
                // La lista crece hacia abajo desde su borde superior (añadir enemigos no la mueve).
                _ => new HudAnchor(new Vector2(1f, 0f), new Vector2(1f - m / w, 0.3f)),
            };
        }

        /// <summary>
        /// Ancla para una pieza soltada en <paramref name="placed"/>: se ancla por el borde del tercio
        /// de pantalla en el que cae (izquierda / centro / derecha, y lo mismo en vertical), para
        /// que al crecer crezca hacia dentro de la pantalla.
        /// </summary>
        private HudAnchor AnchorFor(HudElement element, Rect placed)
        {
            var anchor = AnchorFromRect(placed, _root.layout);
            // La lista de enemigos cambia de alto con cada enemigo: en el centro se ancla por arriba,
            // o crecería hacia los dos lados y bailaría.
            if (element == HudElement.EnemyList && Mathf.Approximately(anchor.Pivot.y, 0.5f))
                anchor = AnchorFromRect(placed, _root.layout, forceTopPivot: true);
            return anchor;
        }

        /// <summary>
        /// Ancla de una pieza soltada en <paramref name="placed"/> dentro de <paramref name="area"/>: se ancla por el
        /// borde del tercio de pantalla en el que cae (lo comparten el HUD y los botones táctiles).
        /// </summary>
        public static HudAnchor AnchorFromRect(Rect placed, Rect area, bool forceTopPivot = false)
        {
            float w = Mathf.Max(1f, area.width), h = Mathf.Max(1f, area.height);

            static float PivotFor(float center, float extent) =>
                center < extent / 3f ? 0f : center > extent * 2f / 3f ? 1f : 0.5f;

            var pivot = new Vector2(PivotFor(placed.center.x, w), forceTopPivot ? 0f : PivotFor(placed.center.y, h));
            var point = new Vector2(placed.x + pivot.x * placed.width, placed.y + pivot.y * placed.height);
            return new HudAnchor(pivot, new Vector2(point.x / w, point.y / h));
        }

        /// <summary>Esquina superior izquierda de una pieza de tamaño <paramref name="size"/> con <paramref name="anchor"/>, dentro de <paramref name="area"/>.</summary>
        public static Vector2 PlaceFromAnchor(HudAnchor anchor, Rect area, Vector2 size)
        {
            float x = anchor.Position.x * area.width - anchor.Pivot.x * size.x;
            float y = anchor.Position.y * area.height - anchor.Pivot.y * size.y;
            return new Vector2(Mathf.Clamp(x, 0f, Mathf.Max(0f, area.width - size.x)),
                               Mathf.Clamp(y, 0f, Mathf.Max(0f, area.height - size.y)));
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

            // Primero (detrás de todo): los bordes rojos.
            _screenFx = new HudScreenFx();
            _root.Add(_screenFx);

            // Barras de enemigo: la capa flotante va detrás de las piezas del jugador.
            _enemyBars = new HudEnemyBars();
            _root.Add(_enemyBars.Overhead);
            AddMovable(HudElement.EnemyFocus, _enemyBars.Focus);
            AddMovable(HudElement.EnemyList, _enemyBars.List);
            _enemyBars.Focus.style.display = DisplayStyle.None;
            _enemyBars.List.style.display = DisplayStyle.None;

            _health = new HudHealthBar();
            AddMovable(HudElement.Health, _health);

            _slotRow = new VisualElement { name = "cooldowns", pickingMode = PickingMode.Ignore };
            _slotRow.style.flexDirection = FlexDirection.Row;
            foreach (CooldownSlot slot in Enum.GetValues(typeof(CooldownSlot)))
            {
                var view = new HudCooldownSlot(slot);
                _slots.Add(view);
                _slotRow.Add(view);
            }
            // La última casilla no necesita hueco a la derecha (si no, el ancla derecha quedaría desplazada).
            _slots[^1].style.marginRight = 0;
            AddMovable(HudElement.Cooldowns, _slotRow);

            _rightColumn = new VisualElement { name = "currency-column", pickingMode = PickingMode.Ignore };
            _rightColumn.style.flexDirection = FlexDirection.Column;
            _rightColumn.style.alignItems = Align.FlexEnd;
            _currency = new HudCurrency();
            _rightColumn.Add(_currency);
            _buffs = new HudBuffRow();
            _rightColumn.Add(_buffs);
            AddMovable(HudElement.Currency, _rightColumn);

            _legend = new HudControlsLegend();
            _legend.style.display = DisplayStyle.None;
            AddMovable(HudElement.Controls, _legend);

            // Encima de todo: el editor de posiciones (oculto hasta que se abre desde Opciones).
            _editor = new HudLayoutEditor(_root, "hud.layout.title", "hud.layout.hint", HudSettings.ResetAnchors);
            RegisterPiece(HudElement.Health, "hud.element.health");
            RegisterPiece(HudElement.Cooldowns, "hud.element.cooldowns");
            RegisterPiece(HudElement.Currency, "hud.element.currency");
            RegisterPiece(HudElement.Controls, "hud.element.controls");
            RegisterPiece(HudElement.EnemyFocus, "hud.element.enemy_focus");
            RegisterPiece(HudElement.EnemyList, "hud.element.enemy_list");
            _editor.Finished += EndLayoutEdit;
            _root.Add(_editor);
        }

        private void RegisterPiece(HudElement element, string labelKey) =>
            _editor.Register(_movable[element], labelKey, placed => HudSettings.SetAnchor(element, AnchorFor(element, placed)));

        private void AddMovable(HudElement element, VisualElement view)
        {
            view.style.position = Position.Absolute;
            view.style.left = 0;
            view.style.top = 0;
            view.style.visibility = Visibility.Hidden;   // hasta que ApplyLayout la ponga en su sitio (sin parpadeo en 0,0)
            _movable[element] = view;
            _root.Add(view);
        }
    }
}
