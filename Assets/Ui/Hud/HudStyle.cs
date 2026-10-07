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
            CooldownSlot.Parry => new Color(0.86f, 0.74f, 1f, 1f),
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
