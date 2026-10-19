using System;
using System.Collections.Generic;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>Capa de dibujo: detrás del player y los enemigos, o delante.</summary>
    public enum L { Back, Front }

    /// <summary>
    /// Paleta de una etiqueta (set, hielo, fuego, oro, vida, vacío), la misma que <c>PAL</c> en
    /// <c>docs/bocetos/habilidades/core.js</c>. <see cref="g1"/>/<see cref="g2"/> son los colores de brillo.
    /// </summary>
    public sealed class ArsenalPal
    {
        public Color core, light, mid, deep, dark, g1, g2;

        private static Color H(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        private static ArsenalPal Make(string core, string light, string mid, string deep, string dark, Color g1, Color g2) =>
            new ArsenalPal { core = H(core), light = H(light), mid = H(mid), deep = H(deep), dark = H(dark), g1 = g1, g2 = g2 };

        public static readonly ArsenalPal Set = Make("#ffffff", "#9ef2ff", "#29c2ff", "#1c5cdb", "#7538db", Rgb(160, 110, 255), Rgb(120, 220, 255));
        public static readonly ArsenalPal Ice = Make("#ffffff", "#e4f9ff", "#93e6ff", "#3f93e3", "#24499e", Rgb(110, 180, 255), Rgb(205, 246, 255));
        public static readonly ArsenalPal Fire = Make("#fff6d8", "#ffd36b", "#ff8a1f", "#e0461a", "#8a1a10", Rgb(255, 80, 20), Rgb(255, 190, 80));
        public static readonly ArsenalPal Gold = Make("#fffbe8", "#ffe9a0", "#ffc93c", "#d48a0c", "#7a4a06", Rgb(255, 160, 30), Rgb(255, 232, 150));
        public static readonly ArsenalPal Life = Make("#fff0f3", "#ffb3c4", "#ff5a7e", "#c81e4a", "#6a0f2a", Rgb(255, 50, 100), Rgb(255, 170, 195));
        public static readonly ArsenalPal Void = Make("#ffffff", "#cdbcff", "#8a5cff", "#3a1c9c", "#0d0626", Rgb(120, 60, 255), Rgb(180, 140, 255));

        /// <summary>Paleta del elemento de la build, o <paramref name="fallback"/> sin elemento.</summary>
        public static ArsenalPal ForElement(ArsenalElement e, ArsenalPal fallback) =>
            e == ArsenalElement.Ice ? Ice : e == ArsenalElement.Fire ? Fire : fallback;
    }

    /// <summary>Aleatorio determinista por semilla (el mismo patrón cada frame, como <c>rand(seed)</c> de los bocetos).</summary>
    public struct Rng
    {
        private uint _s;
        public Rng(float seed) { _s = (uint)(Mathf.Abs(Mathf.Floor(seed * 7919f + 13f)) * 2654435761u) | 1u; Next(); Next(); Next(); }
        public float Next()
        {
            _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
            return (_s & 0xFFFFFF) / 16777216f;
        }
        public float Range(float a, float b) => a + (b - a) * Next();
    }

    /// <summary>
    /// Primitivas y piezas de dibujo de las armas, portadas de <c>docs/bocetos/habilidades/core.js</c> a <see cref="FxCanvas"/>.
    /// <b>Todo en mundo y con Y hacia arriba</b>: medidas del boceto en "px" × <see cref="PX"/>; los ángulos del boceto (Y hacia
    /// abajo) cambian de signo. Cada pieza recibe la capa (<see cref="L"/>) y dibuja sólo lo que le toca, como en la web. Los
    /// brillos van a un lienzo aditivo propio de cada capa (debajo de las piezas sólidas): suman luz en vez de tapar.
    /// </summary>
    public static class ArsenalDraw
    {
        /// <summary>Unidades del mundo por "px" del boceto (README de las armas: × ~0,025).</summary>
        public const float PX = 0.025f;
        public const float TAU = Mathf.PI * 2f;

        private static FxCanvas _back, _front, _backGlow, _frontGlow;
        private static readonly List<Vector2> Pts = new List<Vector2>(128), Side = new List<Vector2>(128);
        private static readonly List<float> Ws = new List<float>(128);
        private static readonly List<Color> Cs = new List<Color>(128);

        /// <summary>Lo llama <see cref="ArsenalWorld"/> con sus cuatro lienzos antes de dibujar.</summary>
        public static void Bind(FxCanvas back, FxCanvas front, FxCanvas backGlow, FxCanvas frontGlow)
        {
            _back = back; _front = front; _backGlow = backGlow; _frontGlow = frontGlow;
        }

        public static FxCanvas Solid(L l) => l == L.Back ? _back : _front;
        public static FxCanvas GlowCanvas(L l) => l == L.Back ? _backGlow : _frontGlow;

        // ------------------------------------------------------------------ utilidades (como en core.js)

        public static float Clamp01(float v) => Mathf.Clamp01(v);
        public static float Seg(float t, float a, float b) => b <= a ? (t >= b ? 1f : 0f) : Mathf.Clamp01((t - a) / (b - a));
        public static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        public static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float Bump(float u) => Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));
        public static float Noise1(float x, float s = 0f) => Mathf.Sin(x * 1.7f + s) * 0.5f + Mathf.Sin(x * 3.1f + s * 2.3f) * 0.3f + Mathf.Sin(x * 5.3f + s * 0.7f) * 0.2f;
        public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(a));
        public static Vector2 Dir(float ang) => new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

        // ------------------------------------------------------------------ primitivas

        /// <summary>Brillo: degradado radial 0,9 → 0,35 (a mitad) → 0, aditivo. Siempre en la capa delantera salvo que se pida.</summary>
        public static void Glow(Vector2 p, float r, Color rgb, float a, L l = L.Front)
        {
            if (a <= 0f || r <= 0f) return;
            a = Mathf.Clamp01(a);
            GlowCanvas(l)?.Radial(p, r, A(rgb, 0.9f * a), 0.45f, A(rgb, 0.35f * a), A(rgb, 0f));
        }

        public static void Disc(L l, Vector2 p, float r, Color c, float a)
        {
            if (a <= 0f || r <= 0f) return;
            Solid(l)?.Ellipse(p, r, r, 0f, A(c, a), A(c, a), r > 0.3f ? 28 : 16);
        }

        /// <summary>Lente afilada por los dos extremos centrada en <paramref name="p"/> a lo largo de <paramref name="ang"/> (ancho máx = <paramref name="wid"/>).</summary>
        public static void Lens(L l, Vector2 p, float ang, float len, float wid, Color c, float a)
        {
            if (a <= 0f || len <= 0f || wid <= 0f) return;
            var cv = Solid(l); if (cv == null) return;
            Vector2 f = Dir(ang), n = new Vector2(-f.y, f.x);
            Pts.Clear();
            const int seg = 10;
            for (int i = 0; i <= seg; i++) { float u = i / (float)seg; Pts.Add(p + f * Mathf.Lerp(-len * 0.5f, len * 0.5f, u) + n * (2f * u * (1f - u) * wid)); }
            for (int i = seg - 1; i >= 1; i--) { float u = i / (float)seg; Pts.Add(p + f * Mathf.Lerp(-len * 0.5f, len * 0.5f, u) - n * (2f * u * (1f - u) * wid)); }
            cv.Polygon(Pts, A(c, a));
        }

        /// <summary>Aguja entre dos puntos (afilada en las puntas, ancho <paramref name="w"/> en el centro).</summary>
        public static void Needle(L l, Vector2 a0, Vector2 a1, float w, Color c, float a)
        {
            Vector2 d = a1 - a0; float len = d.magnitude; if (len <= 1e-5f) return;
            Lens(l, (a0 + a1) * 0.5f, Mathf.Atan2(d.y, d.x), len, w, c, a);
        }

        /// <summary>Gota: cabeza redonda en <paramref name="p"/> y cola afilada hacia atrás (<paramref name="ang"/> = avance).</summary>
        public static void Drop(L l, Vector2 p, float ang, float len, float wid, Color c, float a)
        {
            if (a <= 0f || len < 0.5f * PX) return;
            var cv = Solid(l); if (cv == null) return;
            Vector2 f = Dir(ang), n = new Vector2(-f.y, f.x);
            Pts.Clear();
            Vector2 p0 = new Vector2(-len, 0f), p2 = new Vector2(wid * 0.6f, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 c1 = new Vector2(-len * 0.2f, wid * s);
                for (int i = 0; i <= 8; i++)
                {
                    float u = s < 0 ? i / 8f : 1f - i / 8f;
                    Vector2 q = (1 - u) * (1 - u) * p0 + 2 * (1 - u) * u * c1 + u * u * p2;
                    Pts.Add(p + f * q.x + n * q.y);
                }
            }
            cv.Polygon(Pts, A(c, a));
        }

        /// <summary>Astilla: punta en <paramref name="tip"/>, base de ancho <paramref name="w"/> en <paramref name="b"/>.</summary>
        public static void Shard(L l, Vector2 tip, Vector2 b, float w, Color c, float a)
        {
            if (a <= 0f) return;
            Vector2 d = b - tip; float len = d.magnitude; if (len < 1e-5f) return;
            Vector2 n = new Vector2(-d.y, d.x) / len * (w * 0.5f);
            Solid(l)?.Triangle(tip, b + n, b - n, A(c, a));
        }

        /// <summary>Destello de 4 puntas (dos lentes cruzadas).</summary>
        public static void Star4(L l, Vector2 p, float r, float ang, Color c, float a, float thin = 0.16f)
        {
            Lens(l, p, ang, r * 2f, r * thin * 2f, c, a);
            Lens(l, p, ang + Mathf.PI * 0.5f, r * 1.3f, r * thin * 1.6f, c, a);
        }

        public struct ArcOpt
        {
            public int m; public float seed, wob, taper, rot; public bool noTaper;
            public static ArcOpt Def => new ArcOpt { m = 24, seed = 1, taper = 1 };
        }

        /// <summary>
        /// Arco de elipse de <paramref name="a0"/> a <paramref name="a1"/> (radianes, Y arriba) con grosor que se afila en los
        /// extremos y radio con ruido (<see cref="ArcOpt.wob"/>).
        /// </summary>
        public static void EllArc(L l, Vector2 c, float rx, float ry, float a0, float a1, float w, Color col, float a, ArcOpt o)
        {
            if (a <= 0f || w <= 0f || rx <= 0f) return;
            var cv = Solid(l); if (cv == null) return;
            int M = o.m > 0 ? o.m : 24;
            var R = new Rng(o.seed == 0 ? 1 : o.seed); float ph0 = R.Next() * 6f, ph1 = R.Next() * 6f;
            float taper = o.noTaper ? 0f : (o.taper == 0f ? 1f : o.taper), cr = Mathf.Cos(o.rot), sr = Mathf.Sin(o.rot);
            Pts.Clear(); Ws.Clear(); Cs.Clear();
            var cc = A(col, a);
            for (int i = 0; i <= M; i++)
            {
                float u = i / (float)M, th = Mathf.Lerp(a0, a1, u), k = 1f + o.wob * (Mathf.Sin(th * 3f + ph0) * 0.6f + Mathf.Sin(th * 5f + ph1) * 0.4f);
                float ex = Mathf.Cos(th) * rx * k, ey = Mathf.Sin(th) * ry * k;
                Pts.Add(c + new Vector2(ex * cr - ey * sr, ex * sr + ey * cr));
                Ws.Add(Mathf.Max(0.3f * PX, w * (taper > 0f ? Mathf.Pow(Bump(u), 0.6f * taper) : 1f)));
                Cs.Add(cc);
            }
            cv.StrokeVar(Pts, Ws, Cs);
        }

        public static void EllArc(L l, Vector2 c, float rx, float ry, float a0, float a1, float w, Color col, float a) =>
            EllArc(l, c, rx, ry, a0, a1, w, col, a, ArcOpt.Def);

        /// <summary>Anillo de suelo en perspectiva: la mitad lejana (arriba) detrás, la cercana delante. <paramref name="gy"/> = suelo.</summary>
        public static void GroundRing(L l, float x, float gy, float r, float w, Color c, float a, ArcOpt o, float sq = 0.24f)
        {
            if (l == L.Back) EllArc(l, new Vector2(x, gy), r, r * sq, 0f, Mathf.PI, w, c, a, o);
            else EllArc(l, new Vector2(x, gy), r, r * sq, Mathf.PI, TAU, w, c, a, o);
        }

        /// <summary>Cinta afilada a lo largo de <paramref name="pts"/> (s = 0 en el primero, 1 en el último).</summary>
        public static void Ribbon(L l, List<Vector2> pts, Func<float, float> wfn, Color c, float a, bool soft = false)
        {
            int n = pts.Count; if (n < 2 || a <= 0f) return;
            var cv = Solid(l); if (cv == null) return;
            Ws.Clear(); Cs.Clear();
            var cc = A(c, a);
            for (int i = 0; i < n; i++) { Ws.Add(Mathf.Max(0f, wfn(i / (float)(n - 1)))); Cs.Add(cc); }
            cv.StrokeVar(pts, Ws, Cs, soft);
        }

        /// <summary>Trazo que se afila (y se desvanece) hacia el final.</summary>
        public static void StrokeTaper(L l, List<Vector2> pts, float w0, Color c, float a, bool fade = true)
        {
            int n = pts.Count; if (n < 2 || a <= 0f) return;
            var cv = Solid(l); if (cv == null) return;
            Ws.Clear(); Cs.Clear();
            for (int i = 0; i < n; i++) { float u = i / (float)(n - 1); Ws.Add(Mathf.Max(0.3f * PX, w0 * (1f - u))); Cs.Add(A(c, a * (fade ? 1f - u * 0.9f : 1f))); }
            cv.StrokeVar(pts, Ws, Cs);
        }

        /// <summary>Rayo en zigzag (ancho de color + núcleo). <paramref name="R"/> cambia el trazo a saltos.</summary>
        public static void Bolt(L l, Vector2 a0, Vector2 a1, int segs, ref Rng R, float w, ArsenalPal pal, float a, float jag = 0.22f)
        {
            if (a <= 0f) return;
            var cv = Solid(l); if (cv == null) return;
            Side.Clear(); Side.Add(a0);
            Vector2 d = a1 - a0; float len = d.magnitude; if (len < 1e-5f) return;
            Vector2 n = new Vector2(-d.y, d.x) / len;
            for (int i = 1; i < segs; i++) { float u = i / (float)segs, off = (R.Next() - 0.5f) * len * jag * Mathf.Sin(Mathf.PI * u); Side.Add(a0 + d * u + n * off); }
            Side.Add(a1);
            cv.Stroke(Side, w * 3f, A(pal.dark, a * 0.35f));
            cv.Stroke(Side, w * 1.6f, A(pal.mid, a * 0.8f));
            cv.Stroke(Side, w * 0.6f, A(pal.core, a));
        }

        /// <summary>Llama: gota que parpadea en cuatro pasadas (rojo → naranja → núcleo). <paramref name="p"/> = base.</summary>
        public static void Flame(L l, Vector2 p, float h, float w, float t, float seed, float a)
        {
            if (a <= 0f || h <= 0f) return;
            var cv = Solid(l); if (cv == null) return;
            var R = new Rng(seed); float ph = R.Next() * 6f;
            float fl = 1f + 0.18f * Mathf.Sin(t * 23f + ph) + 0.1f * Mathf.Sin(t * 37f + ph * 2f), sway = Mathf.Sin(t * 9f + ph) * w * 0.35f;
            var F = ArsenalPal.Fire;
            Span4(cv, p, h, w, fl, sway, 1f, F.deep, a * 0.75f);
            Span4(cv, p, h, w, fl, sway, 0.72f, F.mid, a * 0.95f);
            Span4(cv, p, h, w, fl, sway, 0.42f, F.light, a);
            Span4(cv, p, h, w, fl, sway, 0.2f, F.core, a);
        }

        private static void Span4(FxCanvas cv, Vector2 p, float h, float w, float fl, float sway, float k, Color c, float a)
        {
            float hh = h * k * fl, ww = w * k;
            Pts.Clear();
            Vector2 tip = p + new Vector2(sway * k, hh);
            Quad(Pts, p + new Vector2(-ww, 0f), p + new Vector2(-ww * 1.05f, hh * 0.45f), tip, 6, true);
            Quad(Pts, tip, p + new Vector2(ww * 1.05f, hh * 0.45f), p + new Vector2(ww, 0f), 6, false);
            Quad(Pts, p + new Vector2(ww, 0f), p + new Vector2(0f, -ww * 0.5f), p + new Vector2(-ww, 0f), 4, false);
            cv.Polygon(Pts, A(c, a));
        }

        private static void Quad(List<Vector2> into, Vector2 p0, Vector2 c, Vector2 p2, int n, bool first)
        {
            for (int i = first ? 0 : 1; i <= n; i++) { float u = i / (float)n; into.Add((1 - u) * (1 - u) * p0 + 2 * (1 - u) * u * c + u * u * p2); }
        }

        /// <summary>Pico de hielo que sale del suelo (<paramref name="baseY"/>): caras clara/media y filo blanco.</summary>
        public static void IceSpike(L l, float x, float baseY, float h, float w, float lean, float a)
        {
            if (a <= 0f || h <= 0.5f * PX) return;
            var cv = Solid(l); if (cv == null) return;
            var I = ArsenalPal.Ice; Vector2 tip = new Vector2(x + lean * h, baseY + h);
            cv.Triangle(new Vector2(x - w, baseY), tip, new Vector2(x + w, baseY), A(I.deep, a));
            cv.Triangle(new Vector2(x - w * 0.8f, baseY), tip, new Vector2(x + w * 0.1f, baseY), A(I.mid, a));
            cv.Triangle(new Vector2(x - w * 0.55f, baseY), tip, new Vector2(x - w * 0.15f, baseY), A(I.light, a));
            cv.Line(tip, new Vector2(x - w * 0.2f, baseY + h * 0.25f), 1f * PX, A(I.core, a));
        }

        /// <summary>Fragmento de hielo/roca girando (polígono de 4-5 lados con una cara clara).</summary>
        public static void Chunk(L l, Vector2 p, float r, float rot, float seed, Color c1, Color c2, float a)
        {
            if (a <= 0f || r <= 0.3f * PX) return;
            var cv = Solid(l); if (cv == null) return;
            var R = new Rng(seed); int n = 4 + (R.Next() < 0.5f ? 0 : 1);
            Pts.Clear();
            for (int i = 0; i < n; i++) { float th = rot + i / (float)n * TAU + R.Next() * 0.6f, rr = r * (0.6f + R.Next() * 0.5f); Pts.Add(p + Dir(th) * rr); }
            cv.Polygon(Pts, A(c1, a));
            cv.Triangle(Pts[0], Pts[1], p, A(c2, a));
        }

        /// <summary>Esquirla de cristal (la del Abanico): lente larga en capas + faceta clara. Sólo delante.</summary>
        public static void CrystalShard(L l, Vector2 p, float ang, float sc, ArsenalPal pal, float a)
        {
            if (l != L.Front || a <= 0f) return;
            float s = sc * PX; Vector2 f = Dir(ang);
            Glow(p, 16f * s, pal.g1, 0.45f * a);
            Lens(l, p, ang, 30f * s, 6.5f * s, pal.deep, 0.9f * a);
            Lens(l, p + f * 2f * s, ang, 25f * s, 4.6f * s, pal.mid, a);
            Lens(l, p + f * 4f * s, ang, 18f * s, 2.4f * s, pal.core, a);
            // faceta clara (media lente por un lado)
            var cv = Solid(l); Vector2 n = new Vector2(-f.y, f.x);
            Pts.Clear();
            for (int i = 0; i <= 8; i++) { float u = i / 8f; Pts.Add(p + f * Mathf.Lerp(-12f * s, 15f * s, u) + n * (2f * u * (1 - u) * 5.5f * s)); }
            cv.Polygon(Pts, A(pal.light, 0.7f * a));
        }

        // ------------------------------------------------------------------ piezas compuestas

        public struct SparkOpt
        {
            public int n; public ArsenalPal pal; public float seed, sp, grav, len, a0, a1, life, w, backShare;
            public static SparkOpt Make(ArsenalPal pal, float seed) => new SparkOpt { n = 10, pal = pal, seed = seed, sp = 260, grav = 520, len = 14, a0 = 0, a1 = TAU, life = 0.45f, w = 2.6f, backShare = 0.45f };
        }

        /// <summary>Chispas: astillas que salen de <paramref name="p"/> entre a0 y a1 (Y arriba), con gravedad, repartidas detrás/delante. Valores en px.</summary>
        public static void Sparks(L l, float t, float t0, Vector2 p, SparkOpt o)
        {
            float life = o.life > 0 ? o.life : 0.45f, u0 = (t - t0) / life; if (u0 <= 0f || u0 >= 1.6f) return;
            var pal = o.pal ?? ArsenalPal.Set; var R = new Rng(o.seed);
            float g = o.grav * PX, sp = o.sp * PX, len = o.len * PX;
            for (int i = 0; i < o.n; i++)
            {
                float ang = Mathf.Lerp(o.a0, o.a1, R.Next()), v = sp * (0.45f + R.Next() * 0.8f), lf = life * (0.6f + R.Next() * 0.6f);
                bool back = R.Next() < o.backShare; float lr = R.Next();
                Color cc = (i % 4) switch { 1 => pal.light, 2 => pal.mid, _ => pal.core };
                float tt = t - t0, u = tt / lf; if (u <= 0f || u >= 1f || (back ? L.Back : L.Front) != l) continue;
                Vector2 v0 = Dir(ang) * v, pos = p + v0 * tt + new Vector2(0f, -0.5f * g * tt * tt), cvv = v0 + new Vector2(0f, -g * tt);
                float L2 = len * (1f - u * 0.6f) * (0.6f + lr * 0.7f);
                Shard(l, pos, pos - cvv.normalized * L2, o.w * PX * (1f - u * 0.5f), cc, 1f - u);
            }
        }

        /// <summary>Gotas de energía que saltan hacia arriba y caen.</summary>
        public static void Droplets(L l, float t, float t0, Vector2 p, ArsenalPal pal, float seed, int n = 8, float sp = 150f, float life = 0.7f, float spread = 2.6f)
        {
            if (t < t0 || t > t0 + life * 1.5f) return;
            var R = new Rng(seed);
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.PI * 0.5f + (R.Next() - 0.5f) * spread, v = sp * PX * (0.4f + R.Next()), lf = life * (0.6f + R.Next() * 0.6f);
                bool back = R.Next() < 0.5f; float r1 = R.Next(), r2 = R.Next();
                float tt = t - t0, u = tt / lf; if (u >= 1f || (back ? L.Back : L.Front) != l) continue;
                Vector2 v0 = Dir(ang) * v, vel = v0 + new Vector2(0f, -420f * PX * tt), pos = p + v0 * tt + new Vector2(0f, -210f * PX * tt * tt);
                Color cc = (i % 3) switch { 0 => pal.core, 1 => pal.light, _ => pal.mid };
                Drop(l, pos, Mathf.Atan2(vel.y, vel.x), (6f + r1 * 6f) * PX * (1f - u * 0.5f), (1.6f + r2 * 1.6f) * PX * (1f - u * 0.6f), cc, 1f - u);
            }
        }

        private static readonly Color DustShadow = new Color(0.36f, 0.42f, 0.47f), DustMid = new Color(0.79f, 0.83f, 0.86f), DustHi = new Color(0.93f, 0.96f, 0.97f);

        /// <summary>Polvo de suelo: bocanadas con sombra y brillo que se abren y se desvanecen. <paramref name="gy"/> = suelo.</summary>
        public static void Dust(L l, float t, float t0, float x, float gy, float seed, int n = 8, float reach = 60f, float sc = 1f, float life = 0.7f, int side = 0)
        {
            float u0 = (t - t0) / life; if (u0 <= 0f || u0 >= 1.3f) return;
            var R = new Rng(seed);
            for (int i = 0; i < n; i++)
            {
                int s = side != 0 ? side : (R.Next() < 0.5f ? -1 : 1);
                float d = reach * PX * (0.3f + R.Next() * 0.8f), lf = life * (0.7f + R.Next() * 0.5f), u = (t - t0) / lf;
                bool back = R.Next() < 0.5f; float r1 = R.Next(), r2 = R.Next();
                if (u >= 1f || (back ? L.Back : L.Front) != l) continue;
                float px = x + s * d * EaseOut(u), py = gy + (4f + (6f + r1 * 12f) * EaseOut(u)) * PX, r = (5f + r2 * 6f) * PX * sc * (0.6f + u * 0.9f), a = (1f - u) * 0.75f;
                Disc(l, new Vector2(px + 1.5f * PX, py - 1.5f * PX), r, DustShadow, a * 0.6f);
                Disc(l, new Vector2(px, py), r, DustMid, a * 0.8f);
                Disc(l, new Vector2(px - r * 0.3f, py + r * 0.3f), r * 0.5f, DustHi, a * 0.7f);
            }
        }

        private static readonly Color RockA = new Color(0.18f, 0.36f, 0.48f), RockB = new Color(0.35f, 0.56f, 0.7f);

        /// <summary>Escombros que saltan en arco desde el suelo y giran.</summary>
        public static void Debris(L l, float t, float t0, float x, float gy, float seed, int n = 7, float sp = 260f, float life = 0.9f, float r = 4f)
        {
            if (t < t0 || t > t0 + life * 1.4f) return;
            var R = new Rng(seed);
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.PI * 0.5f + (R.Next() - 0.5f) * 2.2f, v = sp * PX * (0.5f + R.Next() * 0.7f), lf = life * (0.7f + R.Next() * 0.5f);
                bool back = R.Next() < 0.4f; float jx = (R.Next() - 0.5f) * 14f * PX, rr = r * PX * (0.6f + R.Next() * 0.8f), spin = 8f + R.Next() * 10f;
                float tt = t - t0; if (tt > lf || (back ? L.Back : L.Front) != l) continue;
                Vector2 pos = new Vector2(x + jx + Mathf.Cos(ang) * v * tt, gy + 2f * PX + Mathf.Sin(ang) * v * tt - 380f * PX * tt * tt);
                if (pos.y < gy - 2f * PX) continue;
                Chunk(l, pos, rr, tt * spin, i * 7 + seed, RockA, RockB, 1f - Seg(tt, lf * 0.7f, lf));
            }
        }

        /// <summary>Grietas radiales en el suelo con brillo (sólo detrás).</summary>
        public static void Cracks(L l, float t, float t0, float x, float gy, ArsenalPal pal, float seed, int n = 6, float len = 70f, float life = 1.2f)
        {
            if (l != L.Back || t < t0) return;
            float u = (t - t0) / life; if (u >= 1f) return;
            var R = new Rng(seed); float grow = EaseOut(Seg(t, t0, t0 + 0.12f)), fade = 1f - Seg(u, 0.5f, 1f);
            var pts = new List<Vector2>(5);
            for (int i = 0; i < n; i++)
            {
                int side = i % 2 == 1 ? 1 : -1; float L1 = len * PX * (0.5f + R.Next() * 0.6f) * grow;
                pts.Clear(); float px = x, py = gy - 2f * PX; pts.Add(new Vector2(px, py));
                for (int k = 1; k <= 4; k++) { px += side * L1 / 4f; py -= ((R.Next() - 0.3f) * 5f + 1.5f) * PX; pts.Add(new Vector2(px, py)); }
                StrokeTaper(l, pts, 4.5f * PX, pal.mid, 0.55f * fade, false);
                StrokeTaper(l, pts, 2f * PX, pal.core, 0.9f * fade, false);
            }
        }

        /// <summary>Impacto sobre un cuerpo: destello en cruz, ondas hacia <paramref name="dir"/>, agujas radiales y gotas.</summary>
        public static void Impact(L l, float t, float t0, Vector2 p, float sc, ArsenalPal pal, float dir = 0f, float spread = 2.8f, float seed = 7f, int needles = 9, int drops = 7)
        {
            float u = t - t0; if (u < 0f || u > 1.2f) return;
            float fu = Seg(u, 0f, 0.16f), s = sc * PX;
            if (l == L.Front)
            {
                Glow(p, 60f * s * (1f - fu * 0.5f), pal.g1, 0.7f * (1f - fu));
                Glow(p, 30f * s, pal.g2, 0.8f * (1f - fu));
                Star4(l, p, 34f * s * (1f - fu * 0.4f), dir + 0.1f, pal.core, 1f - fu);
                Disc(l, p, 9f * s * (1f - fu), pal.core, 1f - fu);
                float ru = Seg(u, 0f, 0.3f), rr = (14f + 46f * EaseOut(ru)) * s;
                EllArc(l, p, rr, rr * 1.05f, dir - 1.3f, dir + 1.3f, 3.2f * s, pal.mid, 1f - ru, new ArcOpt { m = 24, wob = 0.12f, seed = 13, taper = 1 });
                EllArc(l, p, rr * 0.72f, rr * 0.72f * 1.05f, dir - 1.3f, dir + 1.3f, 1.6f * s, pal.core, 1f - ru, new ArcOpt { m = 24, wob = 0.12f, seed = 10.2f, taper = 1 });
            }
            float nu = Seg(u, 0f, 0.22f); var R = new Rng(seed);
            for (int i = 0; i < needles; i++)
            {
                float ang = dir + (R.Next() - 0.5f) * spread, l0 = (10f + R.Next() * 14f) * s, l1 = l0 + (24f + R.Next() * 30f) * s * EaseOut(nu);
                bool back = R.Next() < 0.4f;
                if ((back ? L.Back : L.Front) != l || nu >= 1f) continue;
                Needle(l, p + Dir(ang) * l0, p + Dir(ang) * l1, 3.2f * s * (1f - nu * 0.6f), i % 3 != 0 ? pal.light : pal.core, 1f - nu);
            }
            Droplets(l, t, t0 + 0.02f, p, pal, seed + 50, drops, 140f * sc);
        }

        /// <summary>Impacto contra el suelo: anillo en perspectiva, polvo, escombros, destello vertical y chispas hacia arriba.</summary>
        public static void GroundImpact(L l, float t, float t0, float x, float gy, float sc, ArsenalPal pal, float seed = 1f, bool dust = true, bool debris = true)
        {
            float u = t - t0; if (u < 0f || u > 1.4f) return;
            float ru = Seg(u, 0f, 0.35f), fu = Seg(u, 0f, 0.14f), s = sc * PX;
            GroundRing(l, x, gy, (16f + 80f * EaseOut(ru)) * s, 4f * s * (1f - ru) + 0.5f * PX, pal.mid, 1f - ru, new ArcOpt { m = 32, wob = 0.08f, seed = 11, taper = 1 });
            GroundRing(l, x, gy, (10f + 58f * EaseOut(ru)) * s, 2f * s, pal.core, 1f - ru, new ArcOpt { m = 32, wob = 0.1f, seed = 12, taper = 1 });
            if (l == L.Front)
            {
                Glow(new Vector2(x, gy + 10f * PX), 70f * s, pal.g1, 0.6f * (1f - fu));
                Lens(l, new Vector2(x, gy + 26f * s), Mathf.PI * 0.5f, 70f * s * (1f - fu * 0.3f), 9f * s * (1f - fu), pal.core, 1f - fu);
                Star4(l, new Vector2(x, gy + 8f * PX), 40f * s, 0f, pal.core, 1f - fu);
            }
            if (dust) Dust(l, t, t0, x, gy, seed + 3, 10, 70f * sc, sc);
            if (debris) Debris(l, t, t0, x, gy, seed + 9, 8, 280f * sc);
            Sparks(l, t, t0, new Vector2(x, gy + 4f * PX), new SparkOpt { n = 12, pal = pal, seed = seed + 17, sp = 330f * sc, grav = 520, len = 16, a0 = Mathf.PI * 0.05f, a1 = Mathf.PI * 0.95f, life = 0.45f, w = 2.6f, backShare = 0.45f });
        }

        /// <summary>Carga en la boca: motas que convergen y orbe que crece (t0..t1); destello al soltar (t1..t1+0,18).</summary>
        public static void CastFx(L l, float t, float t0, float t1, Vector2 p, ArsenalPal pal, float sc = 1f, float dir = 0f, float seed = 71f)
        {
            float s = sc * PX;
            if (t >= t0 && t < t1)
            {
                float u = Seg(t, t0, t1); var R = new Rng(seed);
                for (int i = 0; i < 12; i++)
                {
                    float a = R.Next() * TAU, rate = 1.6f + R.Next(), ph = R.Next(), age = ((t - t0) * rate + ph) % 1f, r = (46f - 40f * age) * s;
                    bool back = Mathf.Sin(a + age * 3f) < 0f; if ((back ? L.Back : L.Front) != l) continue;
                    Vector2 q = p + new Vector2(Mathf.Cos(a + age * 2f) * r, Mathf.Sin(a + age * 2f) * r * 0.8f);
                    Drop(l, q, Mathf.Atan2(p.y - q.y, p.x - q.x), 7f * s * age + 2f * PX, 1.6f * s, i % 2 == 1 ? pal.light : pal.core, age * u);
                }
                if (l == L.Front)
                {
                    Glow(p, 26f * s * u + 6f * PX, pal.g1, 0.6f * u);
                    Disc(l, p, 6f * s * u, pal.mid, 0.9f); Disc(l, p, 3.6f * s * u, pal.core, 1f);
                }
            }
            if (l == L.Front && t >= t1 && t < t1 + 0.18f)
            {
                float u = Seg(t, t1, t1 + 0.18f);
                Glow(p, 34f * s, pal.g2, 0.8f * (1f - u));
                Star4(l, p, 22f * s * (1f + u * 0.5f), dir, pal.core, 1f - u);
                EllArc(l, p, 8f * PX + 22f * EaseOut(u) * s, 8f * PX + 26f * EaseOut(u) * s, -1.2f, 1.2f, 2.4f * PX, pal.light, 1f - u, new ArcOpt { m = 20, seed = 1, taper = 1, rot = dir });
            }
        }

        /// <summary>Lista temporal compartida para construir polilíneas sin reservar memoria (no anidar).</summary>
        public static List<Vector2> TempPoints() { Pts.Clear(); return Pts; }
    }
}
