using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.Localization
{
    /// <summary>
    /// El <see cref="LocalizedText"/> de UI Toolkit: ata un elemento a una clave (o a un repintado a
    /// medida) y lo vuelve a pintar en cada <see cref="Loc.Changed"/> mientras esté en un panel.
    ///
    /// <list type="bullet">
    /// <item><b>UXML</b>: escribe la clave con <c>#</c> delante — <c>text="#options.title"</c>,
    /// <c>label="#options.mute"</c> en un Toggle, <c>tooltip="#pause.open"</c> — y el controlador
    /// llama a <see cref="BindTree"/> en su <c>OnEnable</c> (antes de vestir con la skin).</item>
    /// <item><b>Menús hechos en código</b>: <see cref="Bind(VisualElement,string)"/> para un rótulo
    /// fijo, <see cref="Bind(VisualElement,Action)"/> para uno que se compone (con números…).</item>
    /// </list>
    /// Un botón vestido por <c>MenuSkinDresser</c> lleva el rótulo en un hijo
    /// (<see cref="ButtonCaptionName"/>): <see cref="SetText"/> lo sabe y escribe ahí.
    /// </summary>
    public static class LocalizedUi
    {
        public const char KeyPrefix = '#';

        /// <summary>Nombre del Label hijo que hace de rótulo en un botón vestido con la skin.</summary>
        public const string ButtonCaptionName = "button-caption";

        private sealed class Binding
        {
            public Action Refresh;
            public bool Active;
        }

        // Débil: un elemento descartado (fuera de todo panel) no se queda vivo por estar atado.
        private static ConditionalWeakTable<VisualElement, Binding> _bindings = new();
        private static readonly List<Binding> Active = new();
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _bindings = new ConditionalWeakTable<VisualElement, Binding>();
            Active.Clear();
            _subscribed = false;
        }

        // ------------------------------------------------------------------ API

        /// <summary>El texto de <paramref name="element"/> sigue a <paramref name="key"/>.</summary>
        public static void Bind(VisualElement element, string key)
        {
            if (element == null || string.IsNullOrEmpty(key)) return;
            Bind(element, () => SetText(element, Loc.Get(key)));
        }

        /// <summary>
        /// <paramref name="refresh"/> se ejecuta ahora y en cada cambio de idioma mientras
        /// <paramref name="owner"/> esté en un panel. Volver a atar el mismo elemento sustituye el repintado.
        /// </summary>
        public static void Bind(VisualElement owner, Action refresh)
        {
            if (owner == null || refresh == null) return;
            EnsureSubscribed();

            if (!_bindings.TryGetValue(owner, out var binding))
            {
                binding = new Binding();
                _bindings.Add(owner, binding);
                owner.RegisterCallback<AttachToPanelEvent>(_ => Activate(binding));
                owner.RegisterCallback<DetachFromPanelEvent>(_ => Deactivate(binding));
                if (owner.panel != null) Activate(binding);
            }

            binding.Refresh = refresh;
            refresh();
        }

        /// <summary>
        /// Recorre <paramref name="root"/> y ata todo texto, etiqueta de Toggle o tooltip que empiece
        /// por <see cref="KeyPrefix"/>. Idempotente: lo ya atado ya no lleva el prefijo.
        /// </summary>
        public static void BindTree(VisualElement root)
        {
            if (root == null) return;

            root.Query<VisualElement>().ForEach(element =>
            {
                string textKey = null, labelKey = null, tooltipKey = null;

                if (element is TextElement text && IsKey(text.text)) textKey = text.text.Substring(1);
                if (element is Toggle toggle && IsKey(toggle.label)) labelKey = toggle.label.Substring(1);
                if (IsKey(element.tooltip)) tooltipKey = element.tooltip.Substring(1);
                if (textKey == null && labelKey == null && tooltipKey == null) return;

                Bind(element, () =>
                {
                    if (textKey != null) SetText(element, Loc.Get(textKey));
                    if (labelKey != null) ((Toggle)element).label = Loc.Get(labelKey);
                    if (tooltipKey != null) element.tooltip = Loc.Get(tooltipKey);
                });
            });
        }

        /// <summary>Escribe el rótulo visible de un elemento (Label, Button —vestido o no—, Toggle).</summary>
        public static void SetText(VisualElement element, string value)
        {
            switch (element)
            {
                case Button button when button.Q<Label>(ButtonCaptionName) is { } caption:
                    caption.text = value;
                    break;
                case TextElement text:
                    text.text = value;
                    break;
                case Toggle toggle:
                    toggle.label = value;
                    break;
            }
        }

        // ------------------------------------------------------------------ interno

        private static bool IsKey(string value) => !string.IsNullOrEmpty(value) && value.Length > 1 && value[0] == KeyPrefix;

        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            Loc.Changed += RefreshAll;
        }

        private static void Activate(Binding binding)
        {
            if (binding.Active) return;
            binding.Active = true;
            Active.Add(binding);
            binding.Refresh?.Invoke(); // pudo cambiar el idioma mientras estaba fuera del panel
        }

        private static void Deactivate(Binding binding)
        {
            if (!binding.Active) return;
            binding.Active = false;
            Active.Remove(binding);
        }

        private static void RefreshAll()
        {
            // Copia: un repintado puede reconstruir elementos y atar/desatar otros.
            foreach (var binding in Active.ToArray()) binding.Refresh?.Invoke();
        }
    }
}
