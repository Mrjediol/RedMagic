using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Paleta y piezas comunes de los menús construidos en código (mejoras del hub y tienda).
    /// Centralizarlo aquí es lo que mantiene los dos con el mismo aspecto y, sobre todo, con el
    /// mismo <b>tamaño legible</b>: para agrandar o encoger toda esa UI se tocan las constantes de
    /// este archivo y las dos pantallas siguen a juego.
    ///
    /// Los tamaños están en píxeles de la resolución de referencia de sus PanelSettings
    /// (1600 × 900, ScaleWithScreenSize), así que en una pantalla 1080p se ven algo más grandes
    /// que estos números.
    /// </summary>
    public static class MenuStyle
    {
        // ------------------------------------------------------------------ paleta
        // A juego con RedMagicTheme / PauseMenu.uss.

        public static readonly Color Backdrop = new(0.02f, 0.015f, 0.02f, 0.84f);
        public static readonly Color PanelBg = new(0.047f, 0.031f, 0.039f, 0.97f);
        public static readonly Color GoldBorder = new(0.588f, 0.439f, 0.29f, 0.65f);
        public static readonly Color Cream = new(0.925f, 0.874f, 0.749f);
        public static readonly Color CellBg = new(0.086f, 0.063f, 0.078f, 0.95f);
        public static readonly Color CellBuyable = new(0.478f, 0.102f, 0.133f, 0.85f);
        public static readonly Color CellMaxed = new(0.34f, 0.27f, 0.13f, 0.9f);
        public static readonly Color Locked = new(0.5f, 0.5f, 0.5f, 0.35f);
        public static readonly Color CostAfford = new(0.6f, 0.9f, 0.55f);
        public static readonly Color CostTooDear = new(0.95f, 0.45f, 0.45f);

        /// <summary>
        /// Borde luminoso para el elemento seleccionado (ratón/dedo encima, o foco de
        /// teclado/mando). Más saturado y brillante que <see cref="GoldBorder"/> a propósito: tiene
        /// que distinguirse de un vistazo, sobre todo navegando con mando desde el sofá o el móvil.
        /// </summary>
        public static readonly Color SelectionHighlight = new(1f, 0.82f, 0.32f, 0.95f);

        // ------------------------------------------------------------------ tamaños

        public const int TitleFontSize = 34;
        public const int CurrencyFontSize = 26;
        public const int BodyFontSize = 20;
        public const int HintFontSize = 15;

        public const float CardWidth = 210f;
        public const float CardHeight = 180f;
        public const int CardTitleFontSize = 20;
        public const int CardDescriptionFontSize = 15;
        public const int CardLevelFontSize = 18;
        public const int CardFooterFontSize = 19;

        // ------------------------------------------------------------------ piezas

        /// <summary>Estira el elemento para que ocupe todo su padre.</summary>
        public static void FillParent(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.top = 0;
            element.style.left = 0;
            element.style.right = 0;
            element.style.bottom = 0;
        }

        /// <summary>El recuadro central del menú.</summary>
        public static VisualElement Panel()
        {
            var panel = new VisualElement { name = "menu-panel" };
            panel.style.paddingTop = 26;
            panel.style.paddingBottom = 26;
            panel.style.paddingLeft = 32;
            panel.style.paddingRight = 32;
            panel.style.backgroundColor = PanelBg;
            panel.style.maxWidth = Length.Percent(96);
            SetBorder(panel, 3, GoldBorder, 22);
            return panel;
        }

        public static VisualElement Header()
        {
            var header = new VisualElement { name = "menu-header" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 22;
            return header;
        }

        public static Label Title(string text)
        {
            var label = new Label(text) { name = "menu-title" };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = TitleFontSize;
            label.style.color = Cream;
            label.style.letterSpacing = 3;
            return label;
        }

        /// <summary>El contador de moneda de la cabecera (fragmentos de alma / oro).</summary>
        public static Label CurrencyLabel()
        {
            var label = new Label("0") { name = "menu-currency" };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = CurrencyFontSize;
            label.style.color = Cream;
            label.style.marginLeft = 36;
            label.style.marginRight = 36;
            return label;
        }

        public static Button CloseButton(Action onClick)
        {
            var button = new Button(onClick) { text = "✕", name = "menu-close" };
            button.AddToClassList(UiSounds.BackClass);
            button.style.fontSize = 26;
            button.style.width = 48;
            button.style.height = 48;
            button.style.color = Cream;
            button.style.backgroundColor = CellBuyable;
            SetBorder(button, 2, GoldBorder, 10);
            AddSelectionHighlight(button, 10f);
            return button;
        }

        public static Label Hint(string text)
        {
            var label = new Label(text) { name = "menu-hint" };
            label.style.marginTop = 18;
            label.style.fontSize = HintFontSize;
            label.style.color = new Color(Cream.r, Cream.g, Cream.b, 0.5f);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }

        /// <summary>Da forma de carta a un botón (celda de mejora o artículo de tienda).</summary>
        public static void Card(Button button)
        {
            button.style.width = CardWidth;
            button.style.height = CardHeight;
            button.style.marginLeft = 7;
            button.style.marginRight = 7;
            button.style.marginTop = 7;
            button.style.marginBottom = 7;
            button.style.paddingTop = 12;
            button.style.paddingBottom = 12;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.SpaceBetween;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.backgroundColor = CellBg;
            SetBorder(button, 2, GoldBorder, 12);
            AddSelectionHighlight(button, 12f);
        }

        /// <summary>
        /// Da a un elemento una señal visible de "esto es lo que está seleccionado ahora mismo":
        /// al pasarle el ratón/dedo por encima, o al recibir el foco navegando con teclado o mando.
        ///
        /// Estos menús se construyen en código y no tienen hoja de estilos, así que no hay
        /// <c>:hover</c>/<c>:focus</c> de USS como en los menús UXML (Main/Pause/Options). Esto es
        /// el equivalente hecho a mano: dibuja un anillo brillante como un hijo superpuesto en
        /// <c>Position.Absolute</c>, en vez de tocar el fondo o el borde propios del elemento.
        /// Así nunca pelea con el color de fondo que cada menú ya cambia según su propio estado
        /// (bloqueado, asequible, al máximo, equipado…) — el anillo es puramente aditivo y se pinta
        /// encima, nunca sustituye nada.
        ///
        /// <see cref="PickingMode.Ignore"/> en el anillo evita que se coma los clics que van
        /// dirigidos al botón. El leve <c>scale</c> extra es el mismo empujón visual que ya usan
        /// los menús UXML en <c>:hover</c> (ver <c>MainMenu.uss</c>).
        /// </summary>
        public static void AddSelectionHighlight(VisualElement element, float radius, Color? color = null)
        {
            var ring = new VisualElement { name = "selection-ring", pickingMode = PickingMode.Ignore };
            FillParent(ring);
            SetBorder(ring, 3, color ?? SelectionHighlight, radius);
            ring.style.display = DisplayStyle.None;
            element.Add(ring);

            bool hovered = false;
            bool focused = false;

            void Refresh()
            {
                bool selected = hovered || focused;
                ring.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
                element.style.scale = new Scale(selected ? new Vector3(1.035f, 1.035f, 1f) : Vector3.one);
            }

            element.RegisterCallback<PointerEnterEvent>(_ => { hovered = true; Refresh(); });
            element.RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; Refresh(); });
            element.RegisterCallback<FocusEvent>(_ => { focused = true; Refresh(); });
            element.RegisterCallback<BlurEvent>(_ => { focused = false; Refresh(); });
        }

        public static Label CardTitle(string text)
        {
            var label = new Label(text) { name = "title" };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = CardTitleFontSize;
            label.style.color = Cream;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        public static Label CardDescription(string text)
        {
            var label = new Label(text) { name = "desc" };
            label.style.fontSize = CardDescriptionFontSize;
            label.style.color = new Color(Cream.r, Cream.g, Cream.b, 0.78f);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        public static Label CardLevel(string text)
        {
            var label = new Label(text) { name = "level" };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = CardLevelFontSize;
            label.style.color = Cream;
            return label;
        }

        public static Label CardFooter(string text)
        {
            var label = new Label(text) { name = "footer" };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = CardFooterFontSize;
            return label;
        }

        public static void SetBorder(VisualElement e, float width, Color color, float radius)
        {
            e.style.borderTopWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderTopColor = color;
            e.style.borderBottomColor = color;
            e.style.borderLeftColor = color;
            e.style.borderRightColor = color;
            e.style.borderTopLeftRadius = radius;
            e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius;
            e.style.borderBottomRightRadius = radius;
        }
    }
}
