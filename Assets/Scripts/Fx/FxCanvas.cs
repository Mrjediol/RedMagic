using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Fx
{
    /// <summary>
    /// Lienzo de efectos dibujados por código: cada frame se vacía, se le añaden formas (trazos,
    /// discos, elipses, gotas, brillos) en coordenadas de mundo y <see cref="Apply"/> las vuelca en
    /// una malla con color por vértice (shader <c>RedMagic/Fx Vertex Color</c>). Es el equivalente
    /// en Unity del canvas 2D de los bocetos web, para portar un concepto casi línea a línea.
    ///
    /// Un efecto con profundidad usa dos lienzos: uno con orden de dibujo por detrás del personaje
    /// y otro por delante.
    /// </summary>
    public sealed class FxCanvas
    {
        private readonly Mesh _mesh;
        private readonly MeshRenderer _renderer;
        private readonly Transform _transform;
        private readonly List<Vector3> _verts = new List<Vector3>(1024);
        private readonly List<Color> _colors = new List<Color>(1024);
        private readonly List<int> _tris = new List<int>(2048);
        private static readonly List<Vector2> Scratch = new List<Vector2>(256);
        private bool _nanWarned;

        public FxCanvas(Transform parent, string name, Material material, string sortingLayer, int sortingOrder)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _transform = go.transform;
            _transform.SetParent(parent, false);
            _mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            _mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.sortingLayerName = sortingLayer;
            _renderer.sortingOrder = sortingOrder;
        }

        public GameObject GameObject => _transform.gameObject;

        public void Clear()
        {
            _verts.Clear();
            _colors.Clear();
            _tris.Clear();
        }

        /// <summary>Vuelca lo dibujado. Las posiciones se dieron en mundo; el lienzo vive en el origen.</summary>
        public void Apply()
        {
            _transform.position = Vector3.zero;
            _transform.rotation = Quaternion.identity;
            _transform.localScale = Vector3.one;
            if (_transform.parent != null)
            {
                // compensa la escala/rotación del padre: las formas están en mundo
                _transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var ls = _transform.parent.lossyScale;
                _transform.localScale = new Vector3(1f / NonZero(ls.x), 1f / NonZero(ls.y), 1f);
            }
            // Un solo vértice NaN deja los límites de la malla en NaN y Unity la descarta ENTERA (desaparece todo el lienzo):
            // se aplastan a un punto y se avisa una vez para poder encontrar la pieza que lo genera.
            for (int i = 0; i < _verts.Count; i++)
            {
                var v = _verts[i];
                if (!float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y)) continue;
                _verts[i] = Vector3.zero;
                if (!_nanWarned) { _nanWarned = true; Debug.LogWarning($"[FxCanvas] Vértice NaN en '{_mesh.name}' (índice {i}): una pieza recibe un valor inválido."); }
            }
            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
        }

        public void Destroy()
        {
            if (_mesh != null) Kill(_mesh);
            if (_transform != null) Kill(_transform.gameObject);
        }

        // ------------------------------------------------------------------ primitivas

        /// <summary>Trazo por una polilínea, de ancho constante, con extremos afilados opcionales.</summary>
        public void Stroke(List<Vector2> pts, float width, Color color, bool taperEnds = false)
        {
            int n = pts.Count;
            if (n < 2 || color.a <= 0f || width <= 0f) return;
            int start = _verts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[Mathf.Max(0, i - 1)], b = pts[Mathf.Min(n - 1, i + 1)];
                Vector2 d = (b - a);
                Vector2 nrm = d.sqrMagnitude > 1e-8f ? new Vector2(-d.y, d.x).normalized : Vector2.up;
                float w = width * 0.5f;
                if (taperEnds) w *= Mathf.Sin(Mathf.PI * (i / (float)(n - 1)));
                _verts.Add(pts[i] + nrm * w);
                _verts.Add(pts[i] - nrm * w);
                _colors.Add(color);
                _colors.Add(color);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int v = start + i * 2;
                _tris.Add(v); _tris.Add(v + 2); _tris.Add(v + 1);
                _tris.Add(v + 1); _tris.Add(v + 2); _tris.Add(v + 3);
            }
        }

        /// <summary>
        /// Trazo con ancho y color por punto (cometas, anillos que adelgazan). Con
        /// <paramref name="soft"/> los bordes van a alfa 0 (resplandor).
        /// </summary>
        public void StrokeVar(List<Vector2> pts, List<float> widths, List<Color> colors, bool soft = false)
        {
            int n = pts.Count;
            if (n < 2) return;
            int start = _verts.Count;
            int rows = soft ? 3 : 2;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[Mathf.Max(0, i - 1)], b = pts[Mathf.Min(n - 1, i + 1)];
                Vector2 d = b - a;
                Vector2 nrm = d.sqrMagnitude > 1e-10f ? new Vector2(-d.y, d.x).normalized : Vector2.up;
                float w = Mathf.Max(0f, widths[i]) * 0.5f;
                Color c = colors[i];
                if (soft)
                {
                    var clear = new Color(c.r, c.g, c.b, 0f);
                    _verts.Add(pts[i] + nrm * w); _colors.Add(clear);
                    _verts.Add(pts[i]); _colors.Add(c);
                    _verts.Add(pts[i] - nrm * w); _colors.Add(clear);
                }
                else
                {
                    _verts.Add(pts[i] + nrm * w); _colors.Add(c);
                    _verts.Add(pts[i] - nrm * w); _colors.Add(c);
                }
            }
            for (int i = 0; i < n - 1; i++)
            for (int k = 0; k < rows - 1; k++)
            {
                int v = start + i * rows + k;
                _tris.Add(v); _tris.Add(v + rows); _tris.Add(v + 1);
                _tris.Add(v + 1); _tris.Add(v + rows); _tris.Add(v + rows + 1);
            }
        }

        /// <summary>Trazo con bordes difuminados (núcleo de color, bordes a alfa 0): brillos y estelas.</summary>
        public void StrokeSoft(Vector2 a, Vector2 b, float width, Color color)
        {
            if (color.a <= 0f || width <= 0f) return;
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            Color clear = new Color(color.r, color.g, color.b, 0f);
            int s = _verts.Count;
            _verts.Add(a + n); _colors.Add(clear);
            _verts.Add(a); _colors.Add(color);
            _verts.Add(a - n); _colors.Add(clear);
            _verts.Add(b + n); _colors.Add(clear);
            _verts.Add(b); _colors.Add(color);
            _verts.Add(b - n); _colors.Add(clear);
            for (int k = 0; k < 2; k++)
            {
                _tris.Add(s + k); _tris.Add(s + 3 + k); _tris.Add(s + 1 + k);
                _tris.Add(s + 1 + k); _tris.Add(s + 3 + k); _tris.Add(s + 4 + k);
            }
        }

        /// <summary>Segmento recto (atajo de <see cref="Stroke"/>).</summary>
        public void Line(Vector2 a, Vector2 b, float width, Color color, bool taper = false)
        {
            Scratch.Clear();
            if (taper)
            {
                for (int i = 0; i <= 6; i++) Scratch.Add(Vector2.Lerp(a, b, i / 6f));
            }
            else { Scratch.Add(a); Scratch.Add(b); }
            Stroke(Scratch, width, color, taper);
        }

        /// <summary>Arco de elipse (centro, radios, rotación, ángulos en radianes). Con dashOn/dashOff &gt; 0 sale a trozos.</summary>
        public void EllipseArc(Vector2 c, float rx, float ry, float rot, float a0, float a1, float width, Color color,
                               float dashOn = 0f, float dashOff = 0f)
        {
            if (color.a <= 0f) return;
            int seg = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(a1 - a0) * Mathf.Max(rx, ry) * 12f), 8, 96);
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            Scratch.Clear();
            float acc = 0f; bool on = true; Vector2 prev = default;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg);
                var p = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                p = c + new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
                if (i > 0 && dashOn > 0f)
                {
                    acc += Vector2.Distance(prev, p);
                    if (acc > (on ? dashOn : dashOff))
                    {
                        acc = 0f;
                        if (on && Scratch.Count > 1) Stroke(Scratch, width, color);
                        Scratch.Clear();
                        on = !on;
                    }
                }
                if (on) Scratch.Add(p);
                prev = p;
            }
            if (Scratch.Count > 1) Stroke(Scratch, width, color);
        }

        /// <summary>Disco relleno; con <paramref name="edge"/> distinto al centro sale un degradado radial.</summary>
        public void Disc(Vector2 c, float r, Color center, Color edge, int segments = 20) =>
            Ellipse(c, r, r, 0f, center, edge, segments);

        public void Disc(Vector2 c, float r, Color color) => Ellipse(c, r, r, 0f, color, color, 18);

        /// <summary>Brillo: disco con el borde a alfa 0.</summary>
        public void Glow(Vector2 c, float r, Color color) =>
            Ellipse(c, r, r, 0f, color, new Color(color.r, color.g, color.b, 0f), 24);

        public void Ellipse(Vector2 c, float rx, float ry, float rot, Color center, Color edge, int segments = 20)
        {
            if (center.a <= 0f && edge.a <= 0f) return;
            int s = _verts.Count;
            _verts.Add(c); _colors.Add(center);
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var p = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                _verts.Add(c + new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs));
                _colors.Add(edge);
            }
            for (int i = 0; i < segments; i++)
            {
                _tris.Add(s); _tris.Add(s + 1 + i); _tris.Add(s + 1 + (i + 1) % segments);
            }
        }

        /// <summary>
        /// Disco con degradado de tres paradas: <paramref name="center"/> en el centro, <paramref name="mid"/> a la fracción
        /// <paramref name="midAt"/> del radio y <paramref name="edge"/> en el borde (los brillos de los bocetos web).
        /// </summary>
        public void Radial(Vector2 c, float r, Color center, float midAt, Color mid, Color edge, int segments = 24)
        {
            if (r <= 0f || (center.a <= 0f && mid.a <= 0f)) return;
            int s = _verts.Count;
            _verts.Add(c); _colors.Add(center);
            float rm = r * Mathf.Clamp01(midAt);
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                _verts.Add(c + d * rm); _colors.Add(mid);
                _verts.Add(c + d * r); _colors.Add(edge);
            }
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                int mi = s + 1 + i * 2, mj = s + 1 + j * 2;
                _tris.Add(s); _tris.Add(mi); _tris.Add(mj);
                _tris.Add(mi); _tris.Add(mi + 1); _tris.Add(mj + 1);
                _tris.Add(mi); _tris.Add(mj + 1); _tris.Add(mj);
            }
        }

        /// <summary>
        /// Anillo con degradado radial: <paramref name="inner"/> en r0, <paramref name="mid"/> a mitad y <paramref name="outer"/> en r1.
        /// <paramref name="squashY"/> aplasta en vertical (perspectiva de suelo); <paramref name="a0"/>..<paramref name="a1"/> = arco (radianes).
        /// </summary>
        public void Annulus(Vector2 c, float r0, float r1, float squashY, Color inner, Color mid, Color outer,
                            float a0 = 0f, float a1 = Mathf.PI * 2f, int segments = 40)
        {
            if (r1 <= 0f || (inner.a <= 0f && mid.a <= 0f && outer.a <= 0f)) return;
            int s = _verts.Count;
            float rm = (r0 + r1) * 0.5f;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)segments);
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * squashY);
                _verts.Add(c + d * r0); _colors.Add(inner);
                _verts.Add(c + d * rm); _colors.Add(mid);
                _verts.Add(c + d * r1); _colors.Add(outer);
            }
            for (int i = 0; i < segments; i++)
            {
                int v = s + i * 3, w = v + 3;
                _tris.Add(v); _tris.Add(w); _tris.Add(v + 1); _tris.Add(v + 1); _tris.Add(w); _tris.Add(w + 1);
                _tris.Add(v + 1); _tris.Add(w + 1); _tris.Add(v + 2); _tris.Add(v + 2); _tris.Add(w + 1); _tris.Add(w + 2);
            }
        }

        /// <summary>Gota: cabeza redonda en <paramref name="pos"/>, cola afilada hacia atrás según <paramref name="angle"/>.</summary>
        public void Teardrop(Vector2 pos, float angle, float length, float width, Color color)
        {
            if (color.a <= 0f || length <= 0f) return;
            Vector2 f = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), n = new Vector2(-f.y, f.x);
            int s = _verts.Count;
            _verts.Add(pos); _colors.Add(color);
            const int seg = 12;
            for (int i = 0; i <= seg; i++)
            {
                float u = i / (float)seg;                       // 0 → 1 alrededor del contorno
                float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, u);
                // media vuelta redonda delante + cola recta detrás
                var p = pos + f * (Mathf.Cos(a) * width * 0.6f) + n * (Mathf.Sin(a) * width);
                _verts.Add(p); _colors.Add(color);
            }
            _verts.Add(pos - f * length); _colors.Add(color);
            int tip = _verts.Count - 1;
            for (int i = 0; i < seg; i++) { _tris.Add(s); _tris.Add(s + 1 + i); _tris.Add(s + 2 + i); }
            _tris.Add(s + 1 + seg); _tris.Add(tip); _tris.Add(s);
            _tris.Add(tip); _tris.Add(s + 1); _tris.Add(s);
        }

        /// <summary>Triángulo (púas, esquirlas).</summary>
        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            if (color.a <= 0f) return;
            int s = _verts.Count;
            _verts.Add(a); _verts.Add(b); _verts.Add(c);
            _colors.Add(color); _colors.Add(color); _colors.Add(color);
            _tris.Add(s); _tris.Add(s + 1); _tris.Add(s + 2);
        }

        /// <summary>Triángulo con color por vértice (degradados dentro de formas: el vacío de la grieta).</summary>
        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            if (ca.a <= 0f && cb.a <= 0f && cc.a <= 0f) return;
            int s = _verts.Count;
            _verts.Add(a); _verts.Add(b); _verts.Add(c);
            _colors.Add(ca); _colors.Add(cb); _colors.Add(cc);
            _tris.Add(s); _tris.Add(s + 1); _tris.Add(s + 2);
        }

        /// <summary>Polígono convexo relleno (abanico desde el primer punto): lentes, cuñas.</summary>
        public void Polygon(List<Vector2> pts, Color color)
        {
            int n = pts.Count;
            if (n < 3 || color.a <= 0f) return;
            int s = _verts.Count;
            for (int i = 0; i < n; i++) { _verts.Add(pts[i]); _colors.Add(color); }
            for (int i = 1; i < n - 1; i++) { _tris.Add(s); _tris.Add(s + i); _tris.Add(s + i + 1); }
        }

        /// <summary>Aguja: afilada en los dos extremos, lo más ancha al 70 % de <paramref name="a"/> a <paramref name="b"/>.</summary>
        public void Needle(Vector2 a, Vector2 b, float width, Color color)
        {
            if (color.a <= 0f || width <= 0f) return;
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-10f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            Vector2 m = a + d * 0.7f;
            int s = _verts.Count;
            _verts.Add(a); _verts.Add(m + n); _verts.Add(b); _verts.Add(m - n);
            for (int i = 0; i < 4; i++) _colors.Add(color);
            _tris.Add(s); _tris.Add(s + 1); _tris.Add(s + 2);
            _tris.Add(s); _tris.Add(s + 2); _tris.Add(s + 3);
        }

        /// <summary>Lista temporal para construir polilíneas sin reservar memoria.</summary>
        public static List<Vector2> Points()
        {
            Scratch.Clear();
            return Scratch;
        }

        private static float NonZero(float v) => Mathf.Abs(v) < 1e-5f ? 1f : v;

        private static void Kill(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
