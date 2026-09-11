using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RedMagic.UI
{
    /// <summary>
    /// Marco de arte estirable para UI Toolkit, a partir de los 4 cuadrantes que genera
    /// <c>UiArtKitProcessor</c> (modo Quad).
    ///
    /// Un 9-slice normal estira el centro de cada lado, que es justo donde estos marcos llevan su
    /// adorno (nudo, cristal): lo aplasta o lo alarga. Partido en cuadrantes, cada mitad del adorno
    /// cae dentro del borde fijo de su cuadrante y las dos mitades se juntan en el centro a la misma
    /// escala; lo único que se estira es una tira de 2 px elegida donde el arte es liso.
    ///
    /// La escala de las partes fijas es <see cref="maxScale"/>, reducida sola si el elemento es
    /// demasiado pequeño para que quepan (así una fila baja no se monta sobre sí misma). Las capas
    /// van en un hijo con <see cref="PickingMode.Ignore"/> detrás del contenido: no roban clics.
    /// </summary>
    [Serializable]
    public class UiFrame
    {
        public const string LayerName = "ui-frame";

        [Tooltip("Cuadrantes TL, TR, BL, BR (UiArtKitProcessor, modo Quad).")]
        public Sprite[] quads = new Sprite[4];

        [Tooltip("El interior del marco en una sola pieza, pintado encima de los cuadrantes para que el " +
                 "panel liso no salga de estirar tiras. Vacío = sin relleno.")]
        public Sprite fill;

        [Tooltip("Radio de las esquinas del relleno interior, en px del sprite (se escala con el marco).")]
        public float fillCornerRadius = 36f;

        [Tooltip("Escala máxima de las partes fijas del marco (px de UI por px del sprite). Se reduce " +
                 "sola si el elemento es demasiado pequeño.")]
        [Range(0.02f, 1f)] public float maxScale = 0.3f;

        [Tooltip("Relleno interior (px de UI) para que el contenido quede dentro del marco.")]
        public UiInsets padding = new(24, 24, 24, 24);

        public bool IsSet =>
            quads != null && quads.Length == 4 && quads[0] != null && quads[1] != null &&
            quads[2] != null && quads[3] != null;

        /// <summary>
        /// Pone el marco detrás del contenido de <paramref name="host"/> y le da su relleno.
        /// Devuelve false (sin tocar nada) si el marco no tiene sprites: el llamador pinta el
        /// aspecto de siempre.
        /// </summary>
        public bool Dress(VisualElement host)
        {
            if (!IsSet) return false;

            var layer = host.Q<VisualElement>(LayerName);
            if (layer == null)
            {
                layer = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
                MenuStyle.FillParent(layer);
                host.Insert(0, layer);
                layer.RegisterCallback<GeometryChangedEvent>(_ => Fit(layer));
            }

            layer.Clear();
            for (int i = 0; i < 4; i++)
            {
                var q = new VisualElement { pickingMode = PickingMode.Ignore };
                q.style.position = Position.Absolute;
                bool right = (i & 1) == 1, bottom = i >= 2;
                q.style.left = right ? Length.Percent(50) : 0;
                q.style.right = right ? 0 : Length.Percent(50);
                q.style.top = bottom ? Length.Percent(50) : 0;
                q.style.bottom = bottom ? 0 : Length.Percent(50);

                var sprite = quads[i];
                var b = sprite.border;   // (izq, abajo, der, arriba)
                q.style.backgroundImage = new StyleBackground(sprite);
                q.style.unitySliceLeft = Mathf.RoundToInt(b.x);
                q.style.unitySliceBottom = Mathf.RoundToInt(b.y);
                q.style.unitySliceRight = Mathf.RoundToInt(b.z);
                q.style.unitySliceTop = Mathf.RoundToInt(b.w);
                q.style.unitySliceScale = maxScale;
                layer.Add(q);
            }

            if (fill != null)
            {
                var f = new VisualElement { name = "ui-frame-fill", pickingMode = PickingMode.Ignore };
                f.style.position = Position.Absolute;
                f.style.backgroundImage = new StyleBackground(fill);
                f.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                layer.Add(f);
            }

            layer.userData = this;

            host.style.backgroundColor = Color.clear;
            MenuStyle.SetBorder(host, 0, Color.clear, 0);
            host.style.paddingLeft = padding.left;
            host.style.paddingRight = padding.right;
            host.style.paddingTop = padding.top;
            host.style.paddingBottom = padding.bottom;

            Fit(layer);
            return true;
        }

        /// <summary>Tiñe el marco de <paramref name="host"/> (resaltado de estado). Blanco = arte tal cual.</summary>
        public static void Tint(VisualElement host, Color tint)
        {
            var layer = host.Q<VisualElement>(LayerName);
            if (layer == null) return;
            foreach (var q in layer.Children()) q.style.unityBackgroundImageTintColor = tint;
        }

        private static void Fit(VisualElement layer)
        {
            if (layer.userData is not UiFrame frame || !frame.IsSet) return;

            float w = layer.layout.width, h = layer.layout.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0f || h <= 0f) return;

            // Las partes fijas de cada cuadrante tienen que caber en su mitad.
            float scale = frame.maxScale;
            for (int i = 0; i < 4; i++)
            {
                var b = frame.quads[i].border;
                scale = Mathf.Min(scale, w * 0.5f / Mathf.Max(1f, b.x + b.z + 1f));
                scale = Mathf.Min(scale, h * 0.5f / Mathf.Max(1f, b.y + b.w + 1f));
            }

            foreach (var q in layer.Children()) q.style.unitySliceScale = scale;

            // El borde interior cae en las partes fijas, así que en pantalla está a (margen × escala).
            var f = layer.Q<VisualElement>("ui-frame-fill");
            if (f == null || frame.fill == null) return;

            float texW = frame.quads[0].rect.width + frame.quads[1].rect.width;
            float texH = frame.quads[0].rect.height + frame.quads[2].rect.height;
            var r = frame.fill.rect;
            f.style.left = r.x * scale;
            f.style.right = (texW - r.xMax) * scale;
            f.style.bottom = r.y * scale;
            f.style.top = (texH - r.yMax) * scale;
            MenuStyle.SetBorder(f, 0, Color.clear, frame.fillCornerRadius * scale);
        }
    }

    /// <summary>
    /// Relleno por lados. No es <see cref="RectOffset"/> porque ése es nativo y no se puede crear en
    /// el inicializador de un campo de ScriptableObject.
    /// </summary>
    [Serializable]
    public struct UiInsets
    {
        public int left, right, top, bottom;

        public UiInsets(int left, int right, int top, int bottom)
        {
            this.left = left;
            this.right = right;
            this.top = top;
            this.bottom = bottom;
        }
    }

    /// <summary>Par normal / hover de una pieza que no se estira (slot, botón).</summary>
    [Serializable]
    public class UiStateSprites
    {
        public Sprite normal;
        public Sprite hover;

        public bool IsSet => normal != null;

        public Sprite Get(bool hovered) => hovered && hover != null ? hover : normal;

        /// <summary>Ancho/alto del sprite normal (1 si no hay), para no deformarlo al darle tamaño.</summary>
        public float Aspect => normal != null && normal.rect.height > 0f ? normal.rect.width / normal.rect.height : 1f;
    }
}
