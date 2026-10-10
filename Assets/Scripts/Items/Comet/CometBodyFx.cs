using System.Collections.Generic;
using RedMagic.Fx;
using UnityEngine;
using static RedMagic.Items.CometDraw;

namespace RedMagic.Items
{
    /// <summary>
    /// El cuerpo del cometa en vuelo, dibujado por código en dos <see cref="FxCanvas"/> (detrás / delante de
    /// personajes): fogonazo de salida, cola que sigue el camino real (capas de color + hilos + llamitas +
    /// chispas), cabeza (orbe clásico o punta de lanza del perforador) y, con upgrade, onda de choque
    /// frontal, capas oscuras y 14 hilos intensos. En homing añade las esquinas de fijación sobre el
    /// objetivo. Traducción casi línea a línea de <c>comet()</c>, <c>pierceHead()</c>, <c>powShock()</c>,
    /// <c>homingMark()</c> y <c>muzzle()</c> del boceto; las medidas están en "px" del boceto × <see cref="Px"/>.
    /// La lleva <see cref="CometProjectile"/>; no es un componente.
    /// </summary>
    public sealed class CometBodyFx
    {
        private const int N = 32;

        private readonly FxCanvas _back, _front;
        private readonly List<(float t, Vector2 p)> _path = new List<(float, Vector2)>(256);
        private readonly Vector2[] _pts = new Vector2[N + 1];
        private readonly Vector2[] _nrm = new Vector2[N + 1];
        private readonly List<Vector2> _p = new List<Vector2>(48);
        private readonly List<float> _w = new List<float>(48);
        private readonly List<Color> _c = new List<Color>(48);

        // ajustes del disparo
        public float Px = 0.016f;
        public float Scale = 1f;
        public float Span = 0.23f;
        public bool Pow, Lance;
        public float ShockScale = 1.2f;
        public CometPalette Pal = CometPalette.Normal;
        public float Dens = 1f;
        public int Satellites = 3;

        /// <summary>La cabeza está dentro del cuerpo de un enemigo: sólo se ve su luz a través de él.</summary>
        public bool Inside;

        /// <summary>Objetivo fijado por el homing (esquinas de fijación), o null.</summary>
        public Collider2D Mark;

        private Vector2 _origin, _markCenter, _markExt;
        private float _t, _end = -1f, _ang;
        private bool _markSeen;

        public CometBodyFx(Transform parent, Material material)
        {
            _back = new FxCanvas(parent, "__CometBack", material, "Characters", 2);
            _front = new FxCanvas(parent, "__CometFront", material, "Characters", 40);
        }

        private bool Ended => _end >= 0f;

        /// <summary>Todo se ha apagado tras <see cref="End"/>: la cola se recogió y las esquinas se cerraron.</summary>
        public bool Finished => Ended && _t - _end > Span + 0.2f && _t > 0.25f;

        public float Time => _t;

        public void Begin(Vector2 origin, Vector2 dir)
        {
            _path.Clear();
            _t = 0f;
            _end = -1f;
            _origin = origin;
            _ang = Mathf.Atan2(dir.y, dir.x);
            _path.Add((0f, origin));
            Inside = false;
            Mark = null;
            _markSeen = false;
        }

        /// <summary>La cabeza deja de existir (impacto, terreno o fin de vida): la cola se recoge y se apaga.</summary>
        public void End()
        {
            if (!Ended) _end = _t;
        }

        public void Tick(float dt, Vector2 head, Vector2 dir)
        {
            _t += dt;
            if (!Ended)
            {
                _path.Add((_t, head));
                if (dir.sqrMagnitude > 1e-6f) _ang = Mathf.Atan2(dir.y, dir.x);
                // la cola sólo necesita lo que cabe en Span
                while (_path.Count > 4 && _path[1].t < _t - Span - 0.1f) _path.RemoveAt(0);
            }
            if (Mark != null && Mark.enabled)
            {
                _markCenter = Mark.bounds.center;
                _markExt = Mark.bounds.extents;
                _markSeen = true;
            }
            Draw();
        }

        public void Clear()
        {
            _back.Clear(); _back.Apply();
            _front.Clear(); _front.Apply();
        }

        public void Destroy()
        {
            _back.Destroy();
            _front.Destroy();
        }

        // ------------------------------------------------------------------ dibujo

        private void Draw()
        {
            _back.Clear();
            _front.Clear();

            DrawMuzzle();

            float headTime = Ended ? _end : _t;
            float tailTime = Mathf.Clamp(_t - Span, 0f, headTime);
            float tailFade = Ended ? 1f - Seg(_t, _end, _end + Span) * 0.6f : 1f;
            bool alive = headTime - tailTime > 0.0005f;

            if (alive)
            {
                for (int i = 0; i <= N; i++) _pts[i] = PathAt(Mathf.Lerp(headTime, tailTime, i / (float)N));
                for (int i = 0; i <= N; i++)
                {
                    Vector2 d = _pts[Mathf.Min(N, i + 1)] - _pts[Mathf.Max(0, i - 1)];
                    _nrm[i] = d.sqrMagnitude < 1e-10f ? Perp(Dir(_ang)) : Perp(d.normalized);
                }
                DrawTail(tailFade);
            }

            if (!Ended)
            {
                if (Pow && !Inside) DrawShock();
                DrawHead();
            }

            DrawMark();

            _back.Apply();
            _front.Apply();
        }

        // fogonazo de salida: anillo discontinuo perpendicular (mitad de detrás detrás) + destello
        private void DrawMuzzle()
        {
            float m = Seg(_t, 0f, 0.22f);
            if (m <= 0f || m >= 1f) return;
            float pw = Pow ? 1.6f : 1f, a = 1f - EaseIn(m), r = Mathf.Lerp(10f, 44f, EaseOut(m)) * pw * Px;
            var col = Fade(Pow ? Pal.purple : Pal.white, a);
            float width = Mathf.Lerp(5f, 1f, m) * pw * Px;
            float on = Mathf.Max(2f, 14f * (1f - m)) * Px, off = (4f + 20f * m) * Px;
            Vector2 c = _origin + Dir(_ang) * (4f * Px);
            _back.EllipseArc(c, r * 0.3f, r, _ang, Mathf.PI * 0.5f, Mathf.PI * 1.5f, width, col, on, off);
            _front.EllipseArc(c, r * 0.3f, r, _ang, -Mathf.PI * 0.5f, Mathf.PI * 0.5f, width, col, on, off);
            if (m < 0.3f) _front.Disc(_origin, Mathf.Lerp(18f, 4f, m / 0.3f) * Px, Fade(Pal.white, 1f - m / 0.3f));
        }

        private static float TailEnv(float s) => Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(s), 0.55f));
        private static float TailRise(float s) => Mathf.Pow(Seg(s, 0f, 0.08f), 0.5f);

        private void DrawTail(float tailFade)
        {
            float t = _t, sc = Scale, px = Px;
            float wm = Pow ? 0.72f : 1f, wob = Pow ? 0.65f : 1f, tp = 0.9f;
            if (Lance) { wm = 0.82f; wob = 0.6f; tp = 1.3f; }
            float br = 1f + 0.12f * wob * Mathf.Sin(t * 30f);

            // capas de color (lienzo trasero: detrás de personajes)
            if (Pow)
            {
                Ribbon(34f, Pal.navy, 0.55f);
                Ribbon(30f, Pal.purple, 0.55f);
                Ribbon(26f, Pal.blue, 0.65f);
            }
            else
            {
                Ribbon(60f, Pal.purple, 0.16f);
                Ribbon(42f, Pal.purple, 0.32f);
                Ribbon(29f, Pal.blue, 0.55f);
            }
            Ribbon(20f, Pal.cyan, 0.9f);
            Ribbon(12f, Pal.light, 1f);
            Ribbon(5f, Pal.white, 1f);

            void Ribbon(float w0, Color color, float alpha)
            {
                _p.Clear(); _w.Clear(); _c.Clear();
                var col = Fade(color, alpha * tailFade);
                for (int i = 0; i <= N; i++)
                {
                    float s = i / (float)N;
                    float w = w0 * wm * sc * Mathf.Pow(1f - s, tp) * TailRise(s) * (br + 0.08f * wob * Mathf.Sin(t * 40f - s * 14f));
                    _p.Add(_pts[i]); _w.Add(Mathf.Max(0f, w) * px); _c.Add(col);
                }
                _back.StrokeVar(_p, _w, _c);
            }

            // hilos
            if (Pow)
            {
                var v = Pal.vivid;
                for (int k = 0; k < v.Length; k++)
                {
                    float A = (5 + (k * 3) % 10) * sc * 0.65f * 1.5f, F = 16 + (k * 7) % 23, K = 5 + (k * 3) % 9, L = 0.5f + (k % 5) * 0.11f;
                    float W0 = 1.2f + (k % 4) * 0.75f;
                    StrandRuns(k % 2 == 1 ? 1f : -1f, A, F, K, k * 1.7f, W0 * sc * 0.8f, L, 2, v[k], 0.92f * tailFade);
                }
            }
            else
            {
                Color[] cs = { Pal.cyan, Pal.light, Pal.blue, Pal.cyan, Pal.light };
                for (int k = 0; k < 5; k++)
                    StrandRuns(k % 2 == 1 ? 1f : -1f, sc * wob * (20f + k * 2.5f), 25f, 9f, k * 2f, sc * (3.2f - k * 0.3f), 1f, 0, cs[k], 0.85f * tailFade);
                for (int k = 0; k < 4; k++)
                    StrandRuns(k % 2 == 1 ? 1f : -1f, sc * wob * (5f + k * 2.2f), 38f, 13f, k * 1.9f, sc * 1.3f, 1f, 1, k % 2 == 1 ? Pal.white : Pal.light, 0.9f * tailFade);
            }

            // llamitas que se desprenden y quedan atrás
            Vector2 u = Dir(_ang);
            int drops = Mathf.RoundToInt(16 * Dens);
            for (int i = 0; i < drops; i++)
            {
                var R = new FxRng(77 + i * 19);
                float s0 = 0.04f + R.Next() * 0.6f, side = R.Next() < 0.5f ? -1f : 1f, rate = 1.3f + R.Next() * 1.5f;
                float age = Mathf.Repeat(t * rate + R.Next(), 1f);
                var canvas = R.Next() < 0.55f ? _back : _front;
                int idx = Mathf.Clamp(Mathf.FloorToInt(s0 * N), 0, N);
                float d = (5f + age * 22f * (0.6f + R.Next())) * sc;
                Vector2 pos = _pts[idx] + _nrm[idx] * (side * d * px) - u * (age * 26f * px);
                float len = (9f + R.Next() * 10f) * (1f - age * 0.6f) * sc, wid = (2.2f + R.Next() * 2.4f) * (1f - age * 0.7f) * sc;
                Color c = (i % 4) switch { 0 => Pal.white, 1 => Pal.light, 2 => Pal.cyan, _ => Pal.blue };
                canvas.Teardrop(pos, _ang + side * 0.25f * age, len * px, wid * px, Fade(c, (1f - age) * tailFade));
            }

            int sparks = Mathf.RoundToInt(14 * Dens);
            for (int i = 0; i < sparks; i++)
            {
                var R = new FxRng(300 + i * 17);
                float s0 = 0.05f + R.Next() * 0.8f, side = R.Next() < 0.5f ? -1f : 1f, rate = 1.4f + R.Next() * 1.6f;
                float age = Mathf.Repeat(t * rate + R.Next(), 1f);
                var canvas = R.Next() < 0.5f ? _back : _front;
                int idx = Mathf.Clamp(Mathf.FloorToInt(s0 * N), 0, N);
                float d = age * 26f * (0.5f + R.Next()) * sc;
                Vector2 pos = _pts[idx] + _nrm[idx] * (side * d * px) - u * (age * 14f * px) + Vector2.down * (age * age * 10f * px);
                Color c = (i % 3) switch { 0 => Pal.white, 1 => Pal.light, _ => Pal.cyan };
                canvas.Disc(pos, ((1f + R.Next() * 2f) * (1f - age) + 0.4f) * sc * px, Fade(c, (1f - age) * tailFade));
            }
        }

        /// <summary>
        /// Un hilo de la cola desplazado por la normal. kind 0 = lengua (se abre y vuelve a cerrarse), 1 =
        /// hilo fino central, 2 = hilo intenso del upgrade (longitud <paramref name="L"/> propia y pasa por
        /// detrás y por delante del eje). Nace en el centro de la cabeza y acaba en punta.
        /// </summary>
        private void StrandRuns(float sign, float A, float F, float K, float PH, float W, float L, int kind, Color color, float alpha)
        {
            float t = _t, px = Px;
            bool? runFront = null;
            _p.Clear(); _w.Clear(); _c.Clear();
            for (int i = 0; i <= N; i++)
            {
                float s = i / (float)N, uu = kind == 2 ? Mathf.Min(1f, s / L) : s;
                float ph = t * F - s * K + PH;
                float off = kind switch
                {
                    0 => sign * A * TailEnv(s) * (0.55f + 0.45f * Mathf.Sin(ph)),
                    1 => sign * A * TailEnv(s) * Mathf.Sin(ph),
                    _ => sign * A * TailEnv(uu) * Mathf.Sin(ph),
                };
                float w = Mathf.Max(0.3f, W * (1f - uu) * TailRise(s));
                float a = alpha * (1f - 0.92f * Seg(s, 0.65f, 1f));
                bool front = kind == 2 ? Mathf.Cos(ph) > 0f : false;
                var p = _pts[i] + _nrm[i] * (off * px);

                if (runFront.HasValue && front != runFront.Value)
                {
                    _p.Add(p); _w.Add(w * px); _c.Add(Fade(color, a));
                    if (_p.Count > 1) (runFront.Value ? _front : _back).StrokeVar(_p, _w, _c);
                    _p.Clear(); _w.Clear(); _c.Clear();
                }
                _p.Add(p); _w.Add(w * px); _c.Add(Fade(color, a));
                runFront = front;
                if (kind == 2 && uu >= 1f) break;
            }
            if (runFront.HasValue && _p.Count > 1) (runFront.Value ? _front : _back).StrokeVar(_p, _w, _c);
        }

        private void DrawHead()
        {
            Vector2 h = _pts[0];
            if (_path.Count > 0) h = _path[_path.Count - 1].p;
            float t = _t, sc = Scale, px = Px;
            Vector2 u = Dir(_ang), n = Perp(u);

            if (Inside)
            {
                Glow(_front, h, 30f * sc * px, Pal.white, 0.55f);
                Glow(_front, h, 52f * sc * px, Pal.glowCyan, 0.35f);
                return;
            }

            if (Pow)
            {
                Glow(_front, h, 52f * sc * px, Pal.glowDeep, 0.5f);
                Glow(_front, h, 34f * sc * px, Pal.glowBlue, 0.45f);
            }

            if (Lance)
            {
                float pulse = 1f + 0.06f * Mathf.Sin(t * 50f);
                Glow(_front, h, 34f * sc * px, Pal.glowPurple, 0.45f);
                Vector2 L(float x, float y) => h + (u * x + n * y) * px;
                for (int s = -1; s <= 1; s += 2)
                {
                    _front.Needle(L(2f, s * 3f), L(-30f * sc, s * 15f * sc), 3.4f * px, Fade(Pal.cyan, 0.9f));
                    _front.Needle(L(2f, s * 2f), L(-20f * sc, s * 10f * sc), 1.6f * px, Pal.white);
                }
                (float k, Color c, float a)[] layers = { (1.5f, Pal.purple, 0.5f), (1.15f, Pal.cyan, 0.95f), (0.8f, Pal.light, 1f), (0.45f, Pal.white, 1f) };
                foreach (var (k, c, a) in layers)
                {
                    float len = 24f * sc * k * pulse, wd = 8f * sc * k;
                    _p.Clear();
                    _p.Add(L(len, 0f)); _p.Add(L(-2f, wd)); _p.Add(L(-len * 0.5f, 0f)); _p.Add(L(-2f, -wd));
                    _front.Polygon(_p, Fade(c, a));
                }
                return;
            }

            // satélites: los de arriba detrás, los de abajo delante
            for (int k = 0; k < Satellites; k++)
            {
                float a = t * 14f + k * (Mathf.PI * 2f / Satellites), sy = Mathf.Sin(a);
                float ox = Mathf.Cos(a) * 14f * sc, oy = sy * 6f * sc;   // oy > 0 = abajo en el boceto
                var canvas = sy < 0f ? _back : _front;
                canvas.Disc(h + (u * ox - n * oy) * px, (1.8f + sy * 0.4f) * sc * px, Fade(k % 2 == 1 ? Pal.light : Pal.white, 0.95f));
            }

            // destello alargado en el sentido del vuelo + anillo que gira
            _p.Clear(); _w.Clear(); _c.Clear();
            _p.Add(h - u * (6f * px)); _w.Add(3f * sc * px); _c.Add(Fade(Pal.white, 0.8f));
            _p.Add(h + u * (38f * sc * px)); _w.Add(3f * sc * px); _c.Add(Fade(Pal.white, 0f));
            _front.StrokeVar(_p, _w, _c);
            _front.EllipseArc(h, 15f * sc * px, 5f * sc * px, _ang + t * 6f, 0f, Mathf.PI * 2f, 1.5f * px, Fade(Pal.white, 0.7f));

            float kR = 8.5f * sc * (1f + 0.08f * Mathf.Sin(t * 50f)) * px;
            if (Pow)
            {
                _front.Disc(h, 8.5f * sc * 1.6f * px, Fade(Pal.blue, 0.8f));
                _front.Disc(h, 8.5f * sc * 1.42f * px, Fade(Pal.purple, 0.85f));
            }
            Glow(_front, h, kR * 3.6f, Pal.glowPurple, 0.55f);
            Glow(_front, h, kR * 2f, Pal.glowCyan, 0.5f);
            _front.Disc(h, kR * 1.25f, Fade(Pal.cyan, 0.9f));
            _front.Disc(h, kR * 0.95f, Pal.light);
            _front.Disc(h, kR * 0.6f, Pal.white);
        }

        // onda de choque frontal del upgrade (como la del dash, más grande y con azul/morado); entera por delante
        private void DrawShock()
        {
            Vector2 h = _path.Count > 0 ? _path[_path.Count - 1].p : _pts[0];
            float T = _t, al = EaseOut(Seg(T, 0f, 0.05f));
            if (al <= 0f) return;
            float s = ShockScale * Px;
            Vector2 u = Dir(_ang), n = Perp(u);
            Vector2 P(float x, float y) => h + (u * x + n * y) * s;
            float Env(float a, float b, float y) => a - y * y / b;
            Color[] pal = { Pal.white, Pal.light, Pal.cyan, Pal.blue, Pal.purple, Pal.light, Pal.blue, Pal.purple, Pal.white, Pal.cyan, Pal.purple };

            _front.Glow(P(34f, 0f), 30f * s, Fade(Pal.white, 0.85f * al));

            var R = new FxRng(11);
            for (int k = 0; k < 11; k++)
            {
                float a = 24f + k * 3.2f + R.Next() * 3f, b = 24f + k * 2.4f, w = 28f + k * 5f, ph = R.Next() * 6.28f, sp = 18f + R.Next() * 22f;
                float y0 = -w * (0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(T * sp + ph)));
                float y1 = w * (0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(T * sp * 1.3f + ph + 2f)));
                float lw = (k < 3 ? 3.6f - k * 0.6f : 1f + R.Next() * 2.4f) * s;
                _p.Clear(); _w.Clear(); _c.Clear();
                for (int i = 0; i <= 18; i++)
                {
                    float uu = i / 18f, y = Mathf.Lerp(y0, y1, uu);
                    _p.Add(P(Env(a, b, y), y));
                    _w.Add(lw * Mathf.Max(0.15f, Mathf.Sin(Mathf.PI * uu)));
                    _c.Add(Fade(pal[k % pal.Length], al));
                }
                _front.StrokeVar(_p, _w, _c);
            }

            for (int i = 0; i < 30; i++)
            {
                var Ri = new FxRng(11 * 31 + i * 7);
                float side = i % 2 == 1 ? 1f : -1f, rate = 2.2f + Ri.Next() * 2.2f, ph = Ri.Next(), st = Mathf.Repeat(T * rate + ph, 1f);
                float a = 26f + Ri.Next() * 16f, b = 24f + Ri.Next() * 10f, wMax = 36f + Ri.Next() * 20f;
                float y = side * st * wMax, back = st > 0.7f ? (st - 0.7f) * 160f : 0f, x = Env(a, b, y) - back;
                float len = (9f + Ri.Next() * 11f) * (1f - st * 0.6f), wid = (2.6f + Ri.Next() * 2.6f) * (1f - st * 0.7f);
                float fa = al * (1f - st) * (st < 0.08f ? st / 0.08f : 1f);
                Vector2 local = new Vector2(-2f * Mathf.Abs(y) / b, side);
                Vector2 wdir = (u * local.x + n * local.y).normalized;
                Color c = (i % 4) switch { 0 => Pal.white, 1 => Pal.light, 2 => Pal.blue, _ => Pal.purple };
                _front.Teardrop(P(x, y), Mathf.Atan2(wdir.y, wdir.x), len * s, wid * s, Fade(c, fa));
            }

            for (int i = 0; i < 14; i++)
            {
                var Ri = new FxRng(11 * 53 + i * 13);
                float side = i % 2 == 1 ? 1f : -1f, rate = 3f + Ri.Next() * 3f, st = Mathf.Repeat(T * rate + Ri.Next(), 1f);
                float y = side * (10f + Ri.Next() * 44f), x = Env(48f, 32f, y), d = st * (20f + Ri.Next() * 34f);
                _front.Disc(P(x - d * 0.8f, y + side * d * 0.6f), (2.6f * (1f - st) + 0.7f) * s, Fade(i % 3 != 0 ? Pal.purple : Pal.white, al * (1f - st)));
            }
        }

        // esquinas de fijación del homing: se cierran desde lejos sobre el objetivo y se contraen al impactar
        private void DrawMark()
        {
            if (!_markSeen) return;
            float after = Ended ? Seg(_t, _end, _end + 0.14f) : 0f;
            if (Mark == null && !Ended) after = 1f;
            if (after >= 1f) return;
            float px = Px, u = EaseOut(Seg(_t, 0f, 0.16f)), a = 1f - after, tm = _t;
            float hwF = _markExt.x / px + 10f, hhF = _markExt.y / px + 12f;
            float hw = Mathf.Lerp(hwF * 3f, hwF, u) * (1f - after * 0.4f), hh = Mathf.Lerp(hhF * 2.7f, hhF, u) * (1f - after * 0.4f);
            float rot = (1f - u) * 0.9f + 0.05f * Mathf.Sin(tm * 8f), wd = 3.4f * (1f + 0.2f * Mathf.Sin(tm * 14f));
            Vector2 ux = Dir(rot), uy = Perp(ux);
            Vector2 C(float x, float y) => _markCenter + (ux * x + uy * y) * px;
            float al = a * (0.4f + 0.6f * u);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                float x = sx * hw, y = sy * hh;
                _front.Needle(C(x, y), C(x - sx * 22f, y), wd * px, Fade(Pal.white, al));
                _front.Needle(C(x, y), C(x, y - sy * 26f), wd * px, Fade(Pal.white, al));
                _front.Needle(C(x, y), C(x - sx * 14f, y), wd * 2.1f * px, Fade(Pal.cyan, al * 0.45f));
                _front.Needle(C(x, y), C(x, y - sy * 17f), wd * 2.1f * px, Fade(Pal.cyan, al * 0.45f));
            }
            if (u < 1f) Glow(_front, _markCenter, 70f * px, Pal.white, (1f - u) * 0.5f);
        }

        private Vector2 PathAt(float time)
        {
            int n = _path.Count;
            if (n == 0) return _origin;
            if (time <= _path[0].t) return _path[0].p;
            for (int i = 1; i < n; i++)
            {
                if (_path[i].t < time) continue;
                var a = _path[i - 1]; var b = _path[i];
                return Vector2.Lerp(a.p, b.p, Mathf.InverseLerp(a.t, b.t, time));
            }
            return _path[n - 1].p;
        }
    }
}
