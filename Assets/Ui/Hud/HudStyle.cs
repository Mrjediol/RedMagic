using RedMagic.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Medidas y colores del HUD en un solo sitio, para que todas las piezas escalen y casen juntas.
    /// Las medidas son píxeles de referencia del <c>CurrencyHudPanelSettings</c> (2688 × 1216, que
    /// en 1080p se ve a ~0,8×): por eso son grandes — se juega en móvil.
    /// La paleta es la de los menús (marrón dorado sobre casi negro).
    /// </summary>
    public static class HudStyle
    {
        // ---------------------------------------------------------------- medidas
        public const float ScreenMargin = 34f;

        public const float HealthWidth = 620f;
        public const float HealthHeight = 46f;
        public const float ShieldHeight = 14f;
        public const float HealthFont = 26f;

        public const float SlotSize = 104f;
        public const float SlotGap = 16f;
        public const float SlotFont = 30f;

        public const float CurrencyIcon = 40f;
        public const float CurrencyFont = 30f;

        // ---------------------------------------------------------------- colores
        public static readonly Color PanelBg = new(0.055f, 0.035f, 0.045f, 0.78f);
        public static readonly Color Frame = new(0.59f, 0.44f, 0.29f, 0.9f);
        public static readonly Color FrameDark = new(0.03f, 0.02f, 0.025f, 0.95f);
        public static readonly Color Text = new(0.98f, 0.95f, 0.88f, 1f);
        public static readonly Color TextOutline = new(0f, 0f, 0f, 0.85f);
        public static readonly Color Gold = new(0.96f, 0.81f, 0.52f, 1f);

        public static readonly Color HealthFill = new(0.80f, 0.15f, 0.20f, 1f);
        public static readonly Color HealthFillLow = new(1f, 0.32f, 0.30f, 1f);
        public static readonly Color HealthGhost = new(1f, 0.86f, 0.74f, 0.95f);
        public static readonly Color HealthHeal = new(0.42f, 0.92f, 0.52f, 1f);
        public static readonly Color HealthTrack = new(0.16f, 0.05f, 0.07f, 1f);
        /// <summary>Relleno de las barras de enemigo: ámbar, para no confundirlas nunca con la del jugador.</summary>
        public static readonly Color EnemyFill = new(0.93f, 0.50f, 0.16f, 1f);
        public static readonly Color Shield =new(0.45f, 0.88f, 1f, 1f);
        public static readonly Color ShieldEmpty = new(0.08f, 0.14f, 0.18f, 0.9f);

        public static readonly Color CooldownShade = new(0.02f, 0.01f, 0.02f, 0.66f);

        /// <summary>Color de acento de cada casilla (anillo de progreso, destello de "listo").</summary>
        public static Color Accent(CooldownSlot slot) => slot switch
        {
            CooldownSlot.Attack => new Color(1f, 0.70f, 0.32f, 1f),
            CooldownSlot.Dash => new Color(0.42f, 0.86f, 1f, 1f),
            CooldownSlot.Parry => new Color(0.62f, 1f, 0.35f, 1f),   // verde lima, como su icono
            CooldownSlot.Weapon => new Color(0.78f, 0.52f, 1f, 1f),
            _ => Gold,
        };

        // ---------------------------------------------------------------- utilidades de estilo

        public static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        public static void Border(VisualElement e, float width, Color color)
        {
            e.style.borderTopWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderRightWidth = width;
            BorderColor(e, color);
        }

        public static void BorderColor(VisualElement e, Color color)
        {
            e.style.borderTopColor = color;
            e.style.borderBottomColor = color;
            e.style.borderLeftColor = color;
            e.style.borderRightColor = color;
        }

        /// <summary>Hijo absoluto que llena a su padre.</summary>
        public static VisualElement Layer(string name, Color color)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Fill(e);
            e.style.backgroundColor = color;
            return e;
        }

        public static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.top = 0;
            e.style.right = 0;
            e.style.bottom = 0;
        }

        /// <summary>Texto blanco con contorno, legible sobre cualquier fondo.</summary>
        public static Label OutlinedLabel(string name, float size)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Text;
            label.style.unityTextOutlineWidth = 1.4f;
            label.style.unityTextOutlineColor = TextOutline;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginLeft = 0;
            label.style.marginRight = 0;
            label.style.paddingLeft = 0;
            label.style.paddingRight = 0;
            return label;
        }

        /// <summary>
        /// Prepara una barra para el aro hueco del <see cref="HudSkin"/> (<paramref name="art"/>): quita el
        /// borde vectorial del marco. Con máscara (lo normal) la pista ocupa el marco entero y cada capa
        /// de relleno (<see cref="BarLayer"/>) lleva la silueta del arte, así que no asoma por ninguna
        /// esquina; sin máscara, la pista se mete por dentro del aro con extremos redondos (aproximado).
        /// El aro se añade luego, encima de todo, con <see cref="UiFrame.Overlay"/>.
        /// Devuelve false si no hay arte (la barra se queda vectorial).
        /// </summary>
        public static bool PrepareSkinnedBar(VisualElement frame, VisualElement track, UiFrame art, float rim, float height)
        {
            if (art == null || !art.IsSet) return false;

            frame.style.backgroundColor = Color.clear;
            Border(frame, 0f, Color.clear);

            float inset = art.HasMask ? 0f : height * rim * 0.6f;
            frame.style.paddingLeft = inset;
            frame.style.paddingRight = inset;
            frame.style.paddingTop = inset;
            frame.style.paddingBottom = inset;

            if (art.HasMask)
            {
                track.style.backgroundColor = Color.clear;
                Radius(track, 0f);
                track.style.overflow = Overflow.Visible;   // cada capa recorta lo suyo
            }
            else Radius(track, (height - inset * 2f) * 0.5f);

            return true;
        }

        /// <summary>
        /// Una capa de relleno de barra (pista, estela, curación, vida, brillo, destello): un rectángulo
        /// de color, o —con un aro del skin que trae máscara— la silueta exacta del arte recortada a la
        /// fracción. Se maneja igual en los dos casos: <see cref="SetFraction"/> y <see cref="SetColor"/>.
        /// </summary>
        public sealed class BarLayer
        {
            public readonly VisualElement Root;
            private readonly VisualElement _mask;

            private BarLayer(VisualElement root, VisualElement mask)
            {
                Root = root;
                _mask = mask;
            }

            /// <param name="art">Aro del skin, o null para una capa plana.</param>
            /// <param name="sizeFrom">Elemento cuyo tamaño tiene la barra entera (la pista).</param>
            /// <param name="heightFraction">Sólo la parte de arriba (el brillo).</param>
            public static BarLayer Create(string name, Color color, UiFrame art, VisualElement sizeFrom, float heightFraction = 1f)
            {
                var root = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
                Fill(root);
                root.style.right = StyleKeyword.Auto;
                root.style.width = Length.Percent(100f);
                if (heightFraction < 1f) root.style.bottom = Length.Percent((1f - heightFraction) * 100f);

                if (art != null && art.HasMask)
                {
                    root.style.overflow = Overflow.Hidden;
                    var mask = art.Mask(sizeFrom, color);
                    root.Add(mask);
                    return new BarLayer(root, mask);
                }

                root.style.backgroundColor = color;
                return new BarLayer(root, null);
            }

            public void SetFraction(float fraction) => Root.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);

            public void SetColor(Color color)
            {
                if (_mask != null) UiFrame.TintLayer(_mask, color);
                else Root.style.backgroundColor = color;
            }
        }

        public static float Smooth(float current, float target, float sharpness, float dt) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));

        /// <summary>Rebote que va de <paramref name="peak"/> a 1 (t de 0 a 1), con un pequeño sobrepaso.</summary>
        public static float Punch(float t, float peak)
        {
            t = Mathf.Clamp01(t);
            float decay = 1f - t;
            return 1f + (peak - 1f) * decay * decay * Mathf.Cos(t * Mathf.PI * 1.5f);
        }
    }
}
