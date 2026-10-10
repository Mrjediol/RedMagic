using System.Collections.Generic;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Colores del cometa. <see cref="Normal"/> es la paleta del set (cian / azul / morado); <see cref="Amber"/>
    /// es la firma del homing (todo lo homing se pinta en ámbar, también la carga y el impacto).
    /// </summary>
    public sealed class CometPalette
    {
        public Color white = Color.white, core, light, cyan, blue, purple, navy;
        public Color glowPurple, glowCyan, glowDeep, glowBlue, glowPuff;
        public Color[] vivid;

        private static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        public static readonly CometPalette Normal = new CometPalette
        {
            core = Hex("#ffe6ea"), light = Hex("#9ef2ff"), cyan = Hex("#29c2ff"), blue = Hex("#1c5cdb"),
            purple = Hex("#7538db"), navy = Hex("#16277a"),
            glowPurple = Rgb(160, 110, 255), glowCyan = Rgb(120, 220, 255), glowDeep = Rgb(90, 60, 230),
            glowBlue = Rgb(30, 70, 220), glowPuff = Rgb(110, 70, 255),
            vivid = new[] { Hex("#2f6bff"), Hex("#8a3cff"), Hex("#c24bff"), Hex("#00c8ff"), Color.white, Hex("#5cf0ff"), Hex("#3a2bff"),
                            Hex("#d68bff"), Hex("#1a9bff"), Color.white, Hex("#a05cff"), Hex("#00e1ff"), Hex("#4d4dff"), Hex("#ff7ae6") },
        };

        public static readonly CometPalette Amber = new CometPalette
        {
            core = Hex("#fff3d6"), light = Hex("#ffe39a"), cyan = Hex("#ffb23c"), blue = Hex("#e8801f"),
            purple = Hex("#b5500e"), navy = Hex("#5a2606"),
            glowPurple = Rgb(255, 150, 40), glowCyan = Rgb(255, 220, 120), glowDeep = Rgb(255, 120, 20),
            glowBlue = Rgb(255, 170, 40), glowPuff = Rgb(255, 140, 40),
            vivid = new[] { Hex("#ff8a00"), Hex("#ffb23c"), Hex("#ff5a1f"), Hex("#ffd24a"), Color.white, Hex("#ffe39a"), Hex("#ff3b1f"),
                            Hex("#ff9a4d"), Hex("#ffc400"), Color.white, Hex("#ff7a2e"), Hex("#ffe066"), Hex("#ff6a00"), Hex("#ffb347") },
        };
    }

    /// <summary>Aleatorio determinista por semilla: el mismo patrón cada frame, como en el boceto.</summary>
    public struct FxRng
    {
        private uint _s;
        public FxRng(int seed) { _s = (uint)(seed * 2654435761u) | 1u; Next(); Next(); }
        public float Next()
        {
            _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
            return (_s & 0xFFFFFF) / 16777216f;
        }
    }

    /// <summary>
    /// Piezas de dibujo del cometa, portadas del boceto (<c>docs/bocetos/ataque-cometa-estados.html</c>) a
    /// <see cref="FxCanvas"/>. Todo en mundo; los tamaños llegan ya multiplicados por el "px" del boceto.
    /// Ángulos en radianes con Y hacia arriba (el boceto tenía Y hacia abajo: los lados se invierten,
    /// pero las piezas son simétricas). "Detrás" = lienzo trasero (detrás del player y los enemigos).
    /// </summary>
    public static class CometDraw
    {
        private static readonly List<Vector2> Pts = new List<Vector2>(64);
        private static readonly List<float> Ws = new List<float>(64);
        private static readonly List<Color> Cs = new List<Color>(64);

        public static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(a));
        public static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        public static float EaseIn(float t) => t * t * t;
        public static Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        public static Vector2 Perp(Vector2 v) => new Vector2(-v.y, v.x);

        /// <summary>glow() del boceto: degradado radial del color (alfa 0,9) a transparente, × <paramref name="a"/>.</summary>
        public static void Glow(FxCanvas c, Vector2 p, float r, Color color, float a)
        {
            if (a <= 0f || r <= 0f) return;
            c.Glow(p, r, Fade(color, 0.9f * a));
        }

        /// <summary>
        /// Media elipse con ruido (anillo en perspectiva) orientada según <paramref name="ang"/>: el eje
        /// estrecho (<paramref name="squash"/>) va a lo largo de la dirección. <paramref name="front"/> = la
        /// mitad de delante (en el sentido de la dirección), si no la de detrás.
        /// </summary>
        public static void RingHalf(FxCanvas c, Vector2 center, float ang, float r, float squash, float wob, int seed,
                                    float width, Color color, float a, bool front)
        {
            if (a <= 0f || r <= 0f || width <= 0f) return;
            var R = new FxRng(seed);
            float p0 = R.Next() * 6f, p1 = R.Next() * 6f;
            Vector2 u = Dir(ang), n = Perp(u);
            float a0 = front ? -Mathf.PI * 0.5f : Mathf.PI * 0.5f;
            Pts.Clear();
            for (int i = 0; i <= 28; i++)
            {
                float th = a0 + Mathf.PI * i / 28f;
                float rr = r * (1f + wob * (Mathf.Sin(th * 3f + p0) * 0.5f + Mathf.Sin(th * 5f + p1) * 0.3f));
                Pts.Add(center + u * (Mathf.Cos(th) * rr * squash) + n * (Mathf.Sin(th) * rr));
            }
            c.Stroke(Pts, width, Fade(color, a));
        }

        /// <summary>Arco con las dos puntas afiladas (centro, radio, inicio y tramo, aplastado en el eje normal).</summary>
        public static void ArcTaper(FxCanvas c, Vector2 center, float r, float a0, float span, float width, Color color, float a, float sq = 1f)
        {
            if (a <= 0f || width <= 0f) return;
            Pts.Clear(); Ws.Clear(); Cs.Clear();
            const int M = 14;
            var col = Fade(color, a);
            for (int i = 0; i <= M; i++)
            {
                float th = a0 + span * i / M;
                Pts.Add(center + new Vector2(Mathf.Cos(th) * r, Mathf.Sin(th) * r * sq));
                Ws.Add(Mathf.Max(0.0005f, width * Mathf.Sin(Mathf.PI * i / M)));
                Cs.Add(col);
            }
            c.StrokeVar(Pts, Ws, Cs);
        }

        /// <summary>Semiarco: ancho 0 en la punta (<paramref name="aTip"/>) y máximo en el centro (<paramref name="aMid"/>). En el marco local (u, n).</summary>
        public static void ArcHalf(FxCanvas c, Vector2 center, Vector2 u, float r, float aTip, float aMid, float width, Color color, float a)
        {
            if (a <= 0f || width <= 0f) return;
            Vector2 n = Perp(u);
            Pts.Clear(); Ws.Clear(); Cs.Clear();
            const int M = 12;
            var col = Fade(color, a);
            for (int i = 0; i <= M; i++)
            {
                float th = Mathf.Lerp(aTip, aMid, i / (float)M);
                Pts.Add(center + u * (Mathf.Cos(th) * r) + n * (Mathf.Sin(th) * r));
                Ws.Add(Mathf.Max(0.0005f, width * Mathf.Sin(Mathf.PI * 0.5f * i / M)));
                Cs.Add(col);
            }
            c.StrokeVar(Pts, Ws, Cs);
        }

        /// <summary>
        /// Lente (vesica) con las puntas en el eje normal a <paramref name="ang"/> (h = media altura) y la
        /// panza a lo largo de la dirección. <paramref name="lineWidth"/> &lt;= 0 = rellena.
        /// </summary>
        public static void Lens(FxCanvas c, Vector2 p, float ang, float h, float bulge, Color color, float a, float lineWidth)
        {
            if (a <= 0f || h <= 0f) return;
            Vector2 u = Dir(ang), n = Perp(u);
            Pts.Clear();
            const int M = 10;
            for (int i = 0; i <= M; i++) Pts.Add(Quad(p + n * h, p + u * (2f * bulge), p - n * h, i / (float)M));
            for (int i = 1; i < M; i++) Pts.Add(Quad(p - n * h, p - u * (2f * bulge), p + n * h, i / (float)M));
            if (lineWidth <= 0f) c.Polygon(Pts, Fade(color, a));
            else { Pts.Add(Pts[0]); c.Stroke(Pts, lineWidth, Fade(color, a)); }
        }

        private static Vector2 Quad(Vector2 a, Vector2 b, Vector2 cc, float t) =>
            (1f - t) * (1f - t) * a + 2f * (1f - t) * t * b + t * t * cc;

        /// <summary>
        /// Abanico de agujas que salen de <paramref name="p"/>; <paramref name="u"/> = vida 0..1. Ángulos de
        /// <paramref name="a0"/> a <paramref name="a1"/> relativos a <paramref name="baseAng"/>. Medio detrás y medio delante.
        /// </summary>
        public static void Fan(FxCanvas back, FxCanvas front, Vector2 p, float baseAng, int n, int seed, float a0, float a1,
                               float l0, float l1, float u, float w, Color[] pal, float px)
        {
            if (u <= 0f || u >= 1f) return;
            var R = new FxRng(seed);
            float e = EaseOut(Seg(u, 0f, 0.3f)), fade = 1f - Seg(u, 0.3f, 1f);
            for (int i = 0; i < n; i++)
            {
                float ang = baseAng + Mathf.Lerp(a0, a1, R.Next()), len = l0 + R.Next() * (l1 - l0);
                var canvas = R.Next() < 0.5f ? back : front;
                float d1 = 6f + len * e + len * 0.25f * u, d0 = Mathf.Min(d1 - 2f, 6f + len * 0.25f * e + len * 0.55f * u);
                Vector2 dir = Dir(ang);
                canvas.Needle(p + dir * (d0 * px), p + dir * (d1 * px), w * (1f - u * 0.5f) * px, Fade(pal[i % pal.Length], fade));
            }
        }

        /// <summary>Rayo en zigzag (morado ancho + núcleo blanco).</summary>
        public static void Bolt(FxCanvas c, Vector2 p0, float ang, float len, int segs, ref FxRng R, float wide, CometPalette pal)
        {
            Pts.Clear();
            Pts.Add(p0);
            Vector2 p = p0;
            for (int k = 1; k <= segs; k++)
            {
                float aa = ang + (R.Next() - 0.5f) * 1.2f;
                p += Dir(aa) * (len / segs);
                Pts.Add(p);
            }
            c.Stroke(Pts, wide, Fade(pal.purple, 0.45f));
            c.Stroke(Pts, wide * 0.4f, pal.white);
        }

        /// <summary>Destello del boceto (<c>flash()</c>): brillo blanco + halo morado + disco que encogen en 0,3 de <paramref name="ht"/>.</summary>
        public static void Flash(FxCanvas front, Vector2 p, float ht, float size, float px, CometPalette pal)
        {
            if (ht > 0.3f || ht < 0f) return;
            float f = ht / 0.3f;
            Glow(front, p, Mathf.Lerp(size * 2.2f, size * 0.5f, f), pal.white, 1f - f);
            Glow(front, p, Mathf.Lerp(size * 3.4f, size, f), pal.glowPurple, 0.7f * (1f - f));
            front.Disc(p, Mathf.Lerp(size, 3f * px, f), Fade(pal.white, 1f - f));
        }

        /// <summary>Esquirla: triángulo con el vértice en <paramref name="tip"/> y la base de ancho <paramref name="w"/> en <paramref name="baseP"/>.</summary>
        public static void Shard(FxCanvas c, Vector2 tip, Vector2 baseP, float w, Color color, float a)
        {
            Vector2 d = baseP - tip;
            if (d.sqrMagnitude < 1e-10f || a <= 0f) return;
            Vector2 n = Perp(d.normalized) * (w * 0.5f);
            c.Triangle(tip, baseP + n, baseP - n, Fade(color, a));
        }

        /// <summary>Polilínea con grosor y color por punto (los "hilos" del boceto).</summary>
        public static void Strand(FxCanvas c, List<Vector2> pts, List<float> ws, List<Color> cs) => c.StrokeVar(pts, ws, cs);
    }
}
