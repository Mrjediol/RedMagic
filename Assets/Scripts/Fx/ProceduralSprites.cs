using System;
using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Sprites blancos generados en memoria (una vez, cacheados) para efectos teñidos en código:
    /// un brillo radial suave y un anillo fino. Sin assets de arte, igual que
    /// <c>AbilityFx.DefaultSprite</c> pero redondos.
    /// </summary>
    public static class ProceduralSprites
    {
        private static Sprite _glow, _ring;

        /// <summary>Disco con el alfa cayendo suave hacia el borde.</summary>
        public static Sprite Glow => _glow != null ? _glow
            : _glow = Radial("ProceduralGlow", r => { float a = Mathf.Clamp01(1f - r); return a * a; });

        /// <summary>Anillo fino cerca del borde.</summary>
        public static Sprite Ring => _ring != null ? _ring
            : _ring = Radial("ProceduralRing", r => Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.07f, 2f)));

        private static Sprite _crescent;

        /// <summary>
        /// Media luna abierta hacia +X (un tajo / escudo de parry): banda gruesa cerca del borde,
        /// llena en el centro del arco y afilándose hacia las puntas (±80°).
        /// </summary>
        public static Sprite Crescent => _crescent != null ? _crescent
            : _crescent = Radial("ProceduralCrescent", (r, angle) =>
            {
                float tips = Mathf.Clamp01(1f - Mathf.Abs(angle) / 80f);
                float width = 0.05f + 0.13f * tips;                     // más gruesa en el centro
                float band = Mathf.Exp(-Mathf.Pow((r - 0.8f) / width, 2f));
                return band * Mathf.Sqrt(tips);
            });

        /// <summary>Sprite de 128 px (1.28 u a 100 ppu) con el alfa en función del radio normalizado.</summary>
        private static Sprite Radial(string name, Func<float, float> alphaAt) =>
            Radial(name, (r, _) => alphaAt(r));

        /// <summary>Como el anterior, con también el ángulo en grados (0 = +X, −180…180).</summary>
        private static Sprite Radial(string name, Func<float, float, float> alphaAt)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float angle = Mathf.Atan2(y - c, x - c) * Mathf.Rad2Deg;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alphaAt(r, angle)) * 255f));
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
