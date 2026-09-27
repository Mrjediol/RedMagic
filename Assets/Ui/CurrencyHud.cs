using System;
using System.Collections.Generic;
using RedMagic.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// HUD de monedas: una fila arriba a la derecha con el icono y la cantidad de cada
    /// <see cref="Currency"/>, visible en todo momento.
    ///
    /// Se auto-crea antes de la primera escena y es persistente (DontDestroyOnLoad), igual que
    /// <c>FpsOverlay</c> y <see cref="CurrencyManager"/> — no hay que ponerlo en ninguna escena ni
    /// en el prefab de mapa. Construye su propio <see cref="UIDocument"/> en código y saca el
    /// <see cref="PanelSettings"/> y los iconos de <c>Resources</c>.
    ///
    /// Se refresca por evento (<see cref="CurrencyManager.Changed"/>), no en Update.
    /// </summary>
    [DisallowMultipleComponent]
    public class CurrencyHud : MonoBehaviour
    {
        public static CurrencyHud Instance { get; private set; }

        private const string PanelSettingsResourcePath = "CurrencyHudPanelSettings";

        private UIDocument _document;
        private VisualElement _column, _row;
        private readonly Dictionary<Currency, Label> _amountLabels = new();
        private bool _built;
        private bool _bound;
        private bool _visible = true;

        // Contador de rerolls: debajo de las monedas, sólo en la tienda (ShopManager.Current).
        private VisualElement _rerollEntry;
        private Image _rerollIcon;
        private Label _rerollLabel;
        private bool _rerollVisible = true;
        private int _rerollShown = -1;
        private Coroutine _rerollPunch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[CurrencyHud]");
            go.AddComponent<CurrencyHud>();
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
                Debug.LogWarning($"[CurrencyHud] Falta '{PanelSettingsResourcePath}' en Resources; " +
                                 "el HUD de monedas no se dibujará.", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (_bound && CurrencyManager.Instance != null)
                CurrencyManager.Instance.Changed -= OnCurrencyChanged;
            if (_bound) RedMagic.Run.RunRerolls.Changed -= OnRerollsChanged;
            Instance = null;
        }

        // El UIDocument llena su rootVisualElement de forma perezosa y CurrencyManager puede tardar
        // un frame en existir; se intenta montar hasta que ambos están listos.
        private void Update()
        {
            if (!_built) TryBuild();
            if (_built && !_bound) TryBind();
            if (_built) ApplyVisibility();
        }

        // No hay moneda que contar en el menú principal (todavía no hay run ni hub), así que el
        // HUD se esconde ahí mismo — igual que los controles de teclado (ControlsLegendHud) y los
        // botones táctiles (TouchOnlyUI) se esconden según el contexto en vez de estar siempre fijos.
        private void ApplyVisibility()
        {
            bool show = !MainMenuController.IsOpen;
            bool showRerolls = ShopManager.Current != null;
            if (showRerolls != _rerollVisible)
            {
                _rerollVisible = showRerolls;
                _rerollEntry.style.display = showRerolls ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (show == _visible) return;

            _visible = show;
            _column.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void TryBuild()
        {
            if (_document == null || _document.panelSettings == null) return;

            var root = _document.rootVisualElement;
            if (root == null) return;

            BuildUi(root);
            _built = true;
        }

        private void TryBind()
        {
            var manager = CurrencyManager.Instance;
            if (manager == null) return;

            manager.Changed += OnCurrencyChanged;
            RedMagic.Run.RunRerolls.Changed += OnRerollsChanged;
            _bound = true;

            foreach (var currency in _amountLabels.Keys)
                OnCurrencyChanged(currency, manager.Get(currency));
            OnRerollsChanged(RedMagic.Run.RunRerolls.Count);
        }

        // ------------------------------------------------------------------ rerolls

        private void OnRerollsChanged(int count)
        {
            if (_rerollLabel == null) return;

            var fx = ShopFxConfig.Current;
            bool changed = _rerollShown >= 0 && count != _rerollShown;
            _rerollShown = count;
            _rerollLabel.text = count.ToString();

            // Sin rerolls, apagado (color apagado en número e icono).
            bool empty = count <= 0;
            _rerollLabel.style.color = empty ? fx.rerollCounterEmptyColor : Color.white;
            _rerollIcon.tintColor = empty ? fx.rerollCounterEmptyColor : Color.white;

            if (!changed) return;
            if (_rerollPunch != null) StopCoroutine(_rerollPunch);
            _rerollPunch = StartCoroutine(PunchRoutine(_rerollLabel, fx.rerollCounterPunch, fx.rerollCounterPunchDuration));
        }

        private System.Collections.IEnumerator PunchRoutine(VisualElement target, float peak, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float s = Mathf.Lerp(peak, 1f, t / duration);
                target.style.scale = new Scale(new Vector3(s, s, 1f));
                yield return null;
            }

            target.style.scale = new Scale(Vector3.one);
            _rerollPunch = null;
        }

        private void OnCurrencyChanged(Currency currency, int amount)
        {
            // Mientras cuenta hacia atrás (PlaySpend) manda la animación; al acabar pone el valor real.
            if (_spending.Contains(currency)) return;
            if (_amountLabels.TryGetValue(currency, out var label))
                label.text = amount.ToString();
        }

        // ------------------------------------------------------------------ gasto animado

        private readonly HashSet<Currency> _spending = new();

        /// <summary>
        /// Gasto visible: el contador de <paramref name="currency"/> tiembla y baja de
        /// <paramref name="from"/> a <paramref name="to"/> en <paramref name="tickDuration"/> segundos
        /// (tiempo real). No toca la moneda: sólo lo que se ve.
        /// </summary>
        public static void PlaySpend(Currency currency, int from, int to, float tickDuration,
                                     float shakeAmplitude, float shakeDuration)
        {
            if (Instance == null || !Instance._amountLabels.TryGetValue(currency, out var label)) return;
            Instance.StartCoroutine(Instance.SpendRoutine(currency, label, from, to, tickDuration,
                                                          shakeAmplitude, shakeDuration));
        }

        private System.Collections.IEnumerator SpendRoutine(Currency currency, Label label, int from, int to,
                                                           float tickDuration, float shakeAmplitude,
                                                           float shakeDuration)
        {
            _spending.Add(currency);
            var entry = label.parent;
            var baseColor = Color.white;
            float total = Mathf.Max(tickDuration, shakeDuration);

            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                float tick = tickDuration > 0f ? Mathf.Clamp01(t / tickDuration) : 1f;
                label.text = Mathf.RoundToInt(Mathf.Lerp(from, to, tick)).ToString();
                label.style.color = Color.Lerp(new Color(1f, 0.55f, 0.4f), baseColor, tick);

                float k = shakeDuration > 0f ? Mathf.Clamp01(1f - t / shakeDuration) : 0f;
                entry.style.translate = new Translate(Mathf.Sin(t * 70f) * shakeAmplitude * k, 0f);
                yield return null;
            }

            entry.style.translate = new Translate(0f, 0f);
            label.style.color = baseColor;
            _spending.Remove(currency);
            if (CurrencyManager.Instance != null) label.text = CurrencyManager.Instance.Get(currency).ToString();
        }

        // ------------------------------------------------------------------ construcción de la UI

        private void BuildUi(VisualElement root)
        {
            root.style.position = Position.Absolute;
            root.style.top = 0;
            root.style.left = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            root.pickingMode = PickingMode.Ignore;

            _column = new VisualElement { name = "currency-column" };
            _column.pickingMode = PickingMode.Ignore;
            _column.style.position = Position.Absolute;
            _column.style.top = 14;
            _column.style.right = 16;
            _column.style.flexDirection = FlexDirection.Column;
            _column.style.alignItems = Align.FlexEnd;
            root.Add(_column);

            _row = new VisualElement { name = "currency-row" };
            _row.pickingMode = PickingMode.Ignore;
            _row.style.flexDirection = FlexDirection.Row;
            _row.style.alignItems = Align.Center;
            _column.Add(_row);

            var config = CurrencyManager.Instance != null ? CurrencyManager.Instance.Config : null;
            foreach (var currency in OrderedCurrencies(config))
            {
                var visual = config != null ? config.VisualFor(currency) : null;
                _row.Add(BuildEntry($"currency-{currency}", visual?.icon, visual != null ? visual.tint : Color.white,
                                    out var amount));
                _amountLabels[currency] = amount;
            }

            // Rerolls: la misma pieza que una moneda, en su propia fila debajo.
            _rerollEntry = BuildEntry("reroll-counter", ShopConfig.Instance.rerollIcon, Color.white, out _rerollLabel);
            _rerollEntry.style.marginTop = 6;
            _rerollIcon = _rerollEntry.Q<Image>("icon");
            _column.Add(_rerollEntry);
            _rerollVisible = true; // ApplyVisibility lo corrige en este mismo paso

            // El primer Update tras montar decide la visibilidad real (main menu o no); arrancar
            // visible y corregir ahí evita un parpadeo si el menú principal ya estaba activo.
            ApplyVisibility();
        }

        // El orden del HUD lo manda el CurrencyConfig; si falta, se usa el orden del enum.
        private static IEnumerable<Currency> OrderedCurrencies(CurrencyConfig config)
        {
            if (config != null && config.Visuals is { Length: > 0 })
            {
                foreach (var visual in config.Visuals)
                    if (visual != null) yield return visual.currency;
                yield break;
            }

            foreach (Currency currency in Enum.GetValues(typeof(Currency)))
                yield return currency;
        }

        private static VisualElement BuildEntry(string name, Sprite iconSprite, Color tint, out Label amount)
        {
            var entry = new VisualElement { name = name };
            entry.pickingMode = PickingMode.Ignore;
            entry.style.flexDirection = FlexDirection.Row;
            entry.style.alignItems = Align.Center;
            entry.style.marginLeft = 14;
            entry.style.paddingLeft = 8;
            entry.style.paddingRight = 10;
            entry.style.paddingTop = 3;
            entry.style.paddingBottom = 3;
            entry.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            entry.style.borderTopLeftRadius = 10;
            entry.style.borderTopRightRadius = 10;
            entry.style.borderBottomLeftRadius = 10;
            entry.style.borderBottomRightRadius = 10;

            var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit };
            icon.pickingMode = PickingMode.Ignore;
            icon.style.width = 26;
            icon.style.height = 26;
            icon.style.marginRight = 6;
            if (iconSprite != null)
            {
                icon.sprite = iconSprite;
                icon.tintColor = tint;
            }
            entry.Add(icon);

            amount = new Label("0") { name = "amount" };
            amount.pickingMode = PickingMode.Ignore;
            amount.style.unityFontStyleAndWeight = FontStyle.Bold;
            amount.style.fontSize = 18;
            amount.style.color = Color.white;
            amount.style.unityTextOutlineWidth = 1.2f;
            amount.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.8f);
            amount.style.minWidth = 22;
            amount.style.unityTextAlign = TextAnchor.MiddleLeft;
            entry.Add(amount);
            return entry;
        }
    }
}
