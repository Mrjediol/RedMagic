using System;
using System.Collections.Generic;
using RedMagic.Economy;
using RedMagic.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Monedas del HUD (arriba a la derecha): una chapa por <see cref="Currency"/> con su icono y la
    /// cantidad, y debajo el contador de rerolls mientras se está en la tienda.
    ///
    /// Al ganar, el número sube contando y la chapa rebota; al gastar en la tienda
    /// (<see cref="GameHud.PlaySpend"/>) baja contando, en rojo, temblando. Se refresca por evento
    /// (<see cref="CurrencyManager.Changed"/>) y anima en <see cref="Tick"/>.
    /// </summary>
    public class HudCurrency : VisualElement
    {
        private const float GainCountTime = 0.45f;

        private sealed class Entry
        {
            public VisualElement Root;
            public Image Icon;
            public Label Amount;
            public int Shown;
            public int Target;
            public float From;
            public float CountT = 1f;
            public float PopT = 1f;
            public float PopPeak = 1.18f;
            public float PopDuration = 0.3f;

            // Gasto animado (tienda).
            public bool Spending;
            public float SpendT, SpendDuration, ShakeAmplitude, ShakeDuration;
            public int SpendFrom, SpendTo;
        }

        private readonly Dictionary<Currency, Entry> _entries = new();
        private readonly VisualElement _row;

        private Entry _reroll;
        private bool _rerollVisible = true;
        private bool _bound;

        public HudCurrency()
        {
            name = "currency";
            pickingMode = PickingMode.Ignore;
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.FlexEnd;

            _row = new VisualElement { name = "currency-row", pickingMode = PickingMode.Ignore };
            _row.style.flexDirection = FlexDirection.Row;
            _row.style.alignItems = Align.Center;
            Add(_row);

            var config = CurrencyManager.Instance != null ? CurrencyManager.Instance.Config : null;
            foreach (var currency in OrderedCurrencies(config))
            {
                var visual = config != null ? config.VisualFor(currency) : null;
                var entry = BuildEntry($"currency-{currency}", visual?.icon, visual != null ? visual.tint : Color.white);
                _row.Add(entry.Root);
                _entries[currency] = entry;
            }

            // Rerolls: la misma pieza que una moneda, en su propia fila debajo, sólo en la tienda.
            _reroll = BuildEntry("reroll-counter", ShopConfig.Instance.rerollIcon, Color.white);
            _reroll.Root.style.marginTop = 10;
            Add(_reroll.Root);
        }

        public void Unbind()
        {
            if (!_bound) return;
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.Changed -= OnCurrencyChanged;
            RunRerolls.Changed -= OnRerollsChanged;
            _bound = false;
        }

        private void TryBind()
        {
            var manager = CurrencyManager.Instance;
            if (manager == null) return;

            manager.Changed += OnCurrencyChanged;
            RunRerolls.Changed += OnRerollsChanged;
            _bound = true;

            // Valor de partida sin animar.
            foreach (var (currency, entry) in _entries) SetInstant(entry, manager.Get(currency));
            SetInstant(_reroll, RunRerolls.Count);
            PaintReroll();
        }

        public void Tick(float dt)
        {
            if (!_bound) TryBind();

            bool showRerolls = ShopManager.Current != null;
            if (showRerolls != _rerollVisible)
            {
                _rerollVisible = showRerolls;
                _reroll.Root.style.display = showRerolls ? DisplayStyle.Flex : DisplayStyle.None;
            }

            foreach (var entry in _entries.Values) Animate(entry, dt);
            Animate(_reroll, dt);
        }

        // ---------------------------------------------------------------- eventos

        private void OnCurrencyChanged(Currency currency, int amount)
        {
            if (!_entries.TryGetValue(currency, out var entry)) return;
            SetTarget(entry, amount);
        }

        private void OnRerollsChanged(int count)
        {
            var fx = ShopFxConfig.Current;
            _reroll.PopPeak = fx.rerollCounterPunch;
            _reroll.PopDuration = fx.rerollCounterPunchDuration;
            SetTarget(_reroll, count);
            PaintReroll();
        }

        private void PaintReroll()
        {
            var fx = ShopFxConfig.Current;
            bool empty = _reroll.Target <= 0;
            _reroll.Amount.style.color = empty ? fx.rerollCounterEmptyColor : HudStyle.Text;
            _reroll.Icon.tintColor = empty ? fx.rerollCounterEmptyColor : Color.white;
        }

        /// <summary>Gasto visible: tiembla y baja de <paramref name="from"/> a <paramref name="to"/>. Sólo lo que se ve.</summary>
        public void PlaySpend(Currency currency, int from, int to, float tickDuration, float shakeAmplitude, float shakeDuration)
        {
            if (!_entries.TryGetValue(currency, out var entry)) return;
            entry.Spending = true;
            entry.SpendT = 0f;
            entry.SpendFrom = from;
            entry.SpendTo = to;
            entry.SpendDuration = tickDuration;
            entry.ShakeAmplitude = shakeAmplitude;
            entry.ShakeDuration = shakeDuration;
        }

        // ---------------------------------------------------------------- animación

        private static void SetInstant(Entry entry, int value)
        {
            entry.Target = entry.Shown = value;
            entry.CountT = 1f;
            entry.Amount.text = value.ToString();
        }

        private static void SetTarget(Entry entry, int value)
        {
            if (value == entry.Target) return;
            bool gained = value > entry.Target;
            entry.From = entry.Shown;
            entry.Target = value;
            entry.CountT = gained ? 0f : 1f;
            entry.PopT = 0f;
            if (!gained && !entry.Spending)
            {
                entry.Shown = value;
                entry.Amount.text = value.ToString();
            }
        }

        private static void Animate(Entry entry, float dt)
        {
            if (entry.Spending)
            {
                entry.SpendT += dt;
                float total = Mathf.Max(entry.SpendDuration, entry.ShakeDuration);
                float tick = entry.SpendDuration > 0f ? Mathf.Clamp01(entry.SpendT / entry.SpendDuration) : 1f;
                entry.Shown = Mathf.RoundToInt(Mathf.Lerp(entry.SpendFrom, entry.SpendTo, tick));
                entry.Amount.text = entry.Shown.ToString();
                entry.Amount.style.color = Color.Lerp(new Color(1f, 0.55f, 0.4f), HudStyle.Text, tick);

                float k = entry.ShakeDuration > 0f ? Mathf.Clamp01(1f - entry.SpendT / entry.ShakeDuration) : 0f;
                entry.Root.style.translate = new Translate(Mathf.Sin(entry.SpendT * 70f) * entry.ShakeAmplitude * k, 0f);

                if (entry.SpendT >= total)
                {
                    entry.Spending = false;
                    entry.Root.style.translate = new Translate(0f, 0f);
                    entry.Amount.style.color = HudStyle.Text;
                    entry.Shown = entry.Target;
                    entry.Amount.text = entry.Target.ToString();
                }
                return;
            }

            if (entry.CountT < 1f)
            {
                entry.CountT = Mathf.Min(1f, entry.CountT + dt / GainCountTime);
                float eased = 1f - Mathf.Pow(1f - entry.CountT, 3f);
                int shown = Mathf.RoundToInt(Mathf.Lerp(entry.From, entry.Target, eased));
                if (shown != entry.Shown)
                {
                    entry.Shown = shown;
                    entry.Amount.text = shown.ToString();
                }
                entry.Amount.style.color = Color.Lerp(HudStyle.Gold, HudStyle.Text, eased);
            }

            if (entry.PopT < 1f)
            {
                entry.PopT = Mathf.Min(1f, entry.PopT + dt / Mathf.Max(0.01f, entry.PopDuration));
                float s = HudStyle.Punch(entry.PopT, entry.PopPeak);
                entry.Root.style.scale = new Scale(new Vector3(s, s, 1f));
            }
        }

        // ---------------------------------------------------------------- construcción

        // El orden lo manda el CurrencyConfig; si falta, el del enum.
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

        private static Entry BuildEntry(string name, Sprite iconSprite, Color tint)
        {
            var root = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginLeft = 16;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 16;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;
            root.style.backgroundColor = HudStyle.PanelBg;
            HudStyle.Radius(root, 14f);
            HudStyle.Border(root, 2f, new Color(HudStyle.Frame.r, HudStyle.Frame.g, HudStyle.Frame.b, 0.55f));

            var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            icon.style.width = HudStyle.CurrencyIcon;
            icon.style.height = HudStyle.CurrencyIcon;
            icon.style.marginRight = 8;
            if (iconSprite != null)
            {
                icon.sprite = iconSprite;
                icon.tintColor = tint;
            }
            root.Add(icon);

            var amount = HudStyle.OutlinedLabel("amount", HudStyle.CurrencyFont);
            amount.text = "0";
            amount.style.minWidth = 36;
            amount.style.unityTextAlign = TextAnchor.MiddleLeft;
            root.Add(amount);

            // Arte (HudSkin): chapa de piedra detrás del icono y el número.
            var skin = HudSkin.Current;
            if (skin != null) skin.currencyPlaque.Dress(root);

            return new Entry { Root = root, Icon = icon, Amount = amount };
        }
    }
}
