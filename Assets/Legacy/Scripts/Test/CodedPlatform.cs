using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RedMagic.Levels
{
    /// <summary>
    /// Plataforma simple como OBJETO (no tile): un rectángulo de <see cref="width"/> × grosor unidades, de un solo
    /// sentido (se atraviesa desde abajo y se puede bajar de ella, capa Platform). El aspecto lo dibuja el código con los
    /// colores y la textura del bioma, igual que <see cref="Level"/>; <see cref="style"/> sólo cambia cómo. El origen
    /// es el centro. Estáticas por ahora: las que se caen o se mueven serán componentes extra sobre ésta.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public class CodedPlatform : MonoBehaviour
    {
        public enum Style
        {
            [Tooltip("Losa gruesa de roca con la textura y el borde de nieve del terreno.")] Roca,
            [Tooltip("Como la roca pero con juntas de ladrillo, como el suelo del boceto.")] Ladrillo,
            [Tooltip("Tabla fina de hielo, translúcida, con un reflejo en el borde.")] Hielo,
            [Tooltip("Losa fina de cristal que brilla y ilumina (Light2D).")] Cristal,
        }

        public const string FillName = "Relleno", DetailName = "Detalle", RimName = "Borde", LightName = "Luz";

        [SerializeField] private Style style = Style.Roca;

        [Tooltip("Ancho en unidades (1 unidad = 40 px del boceto).")]
        [SerializeField, Min(1f)] private float width = 6f;

        [Tooltip("Grosor en unidades. 0 = el de cada estilo (roca y ladrillo 1, hielo 0.4, cristal 0.3).")]
        [SerializeField, Min(0f)] private float thickness = 0f;

        [Tooltip("Bioma del que sacar colores y textura. Vacío = el del Level en el que está.")]
        [SerializeField] private LevelBiome biome;

        public Style PlatformStyle => style;
        public float Width => width;

        private bool _dirty = true;
        private Mesh _fillMesh, _detailMesh, _rimMesh;
        private Material _rockMaterial, _vertexMaterial;

        // ------------------------------------------------------------------ ciclo de vida

        private void OnEnable()
        {
            LevelBiome.Changed += OnBiomeChanged;
            _dirty = true;
            Rebuild();
        }

        private void OnDisable() => LevelBiome.Changed -= OnBiomeChanged;

        private void OnDestroy()
        {
            Level.DestroySafe(_fillMesh); Level.DestroySafe(_detailMesh); Level.DestroySafe(_rimMesh);
            Level.DestroySafe(_rockMaterial); Level.DestroySafe(_vertexMaterial);
        }

        private void OnValidate()
        {
            width = Mathf.Max(1f, width);
            _dirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void Update()
        {
            if (_dirty) Rebuild();
        }

        private void OnBiomeChanged(LevelBiome changed)
        {
            if (changed != ActiveBiome) return;
            _dirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        /// <summary>Desde código (menús, importadores).</summary>
        public void Configure(Style newStyle, float newWidth, float newThickness, LevelBiome newBiome)
        {
            style = newStyle; width = Mathf.Max(1f, newWidth); thickness = newThickness; biome = newBiome;
            _dirty = true;
            Rebuild();
        }

        private LevelBiome ActiveBiome
        {
            get
            {
                if (biome != null) return biome;
                var level = GetComponentInParent<Level>();
                return level != null ? level.Biome : null;
            }
        }

        public float Thickness => thickness > 0f ? thickness : style switch
        {
            Style.Roca => 1f,
            Style.Ladrillo => 1f,
            Style.Hielo => 0.4f,
            _ => 0.3f,
        };

        // ------------------------------------------------------------------ construcción

        [ContextMenu("Regenerar")]
        public void Rebuild()
        {
            _dirty = false;
            var b = ActiveBiome;
            float w = width, h = Thickness;
            BuildCollider(w, h);
            if (b == null) return;

            // Mismo grosor de sombra y mismo radio que el terreno, pero sin pasarse en las piezas finas.
            var contour = Contour(w, h, b.cornerRadius);
            float seed = Mathf.Abs(transform.position.x * 0.37f + transform.position.y * 1.13f);

            var fillV = new List<Vector3>(); var fillT = new List<int>(); var fillUv = new List<Vector2>(); var fillC = new List<Color>();
            var detV = new List<Vector3>(); var detT = new List<int>(); var detC = new List<Color>();
            var rimV = new List<Vector3>(); var rimT = new List<int>(); var rimC = new List<Color>();

            bool textured = style == Style.Roca || style == Style.Ladrillo;
            bool light = false;
            switch (style)
            {
                case Style.Roca: BuildRock(b, contour, h, seed, fillV, fillT, fillUv, detV, detT, detC, rimV, rimT, rimC); break;
                case Style.Ladrillo:
                    BuildRock(b, contour, h, seed, fillV, fillT, fillUv, detV, detT, detC, rimV, rimT, rimC);
                    AddBricks(w, h, b.outline, detV, detT, detC);
                    break;
                case Style.Hielo: BuildIce(b, contour, w, h, seed, fillV, fillT, fillC, detV, detT, detC, rimV, rimT, rimC); break;
                default: BuildCrystal(b, contour, h, fillV, fillT, fillC, detV, detT, detC, rimV, rimT, rimC); light = true; break;
            }

            var vertexShader = b.rimShader != null ? b.rimShader : Shader.Find("RedMagic/Fx Vertex Color");
            _vertexMaterial = Level.GetMaterial(ref _vertexMaterial, vertexShader, "PlatformVertex");

            _fillMesh = Level.Assign(_fillMesh, "PlatformFill", fillV, fillT, textured ? fillUv : null, textured ? null : fillC);
            if (textured)
            {
                var rockShader = b.rockShader != null ? b.rockShader : Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                _rockMaterial = Level.GetMaterial(ref _rockMaterial, rockShader, "PlatformRock");
                _rockMaterial.mainTexture = b.rockTexture;
                Level.SetupRenderer(Child(FillName), _fillMesh, _rockMaterial, LevelSorting.Rock);
            }
            else
            {
                Level.SetupRenderer(Child(FillName), _fillMesh, _vertexMaterial, LevelSorting.Rock);
            }

            _detailMesh = Level.Assign(_detailMesh, "PlatformDetail", detV, detT, null, detC);
            Level.SetupRenderer(Child(DetailName), _detailMesh, _vertexMaterial, LevelSorting.Glow);
            _rimMesh = Level.Assign(_rimMesh, "PlatformRim", rimV, rimT, null, rimC);
            Level.SetupRenderer(Child(RimName), _rimMesh, _vertexMaterial, LevelSorting.Rim);

            UpdateLight(light, w);
        }

        private void BuildCollider(float w, float h)
        {
            int layer = LayerMask.NameToLayer("Platform");
            if (layer >= 0) gameObject.layer = layer;
            if (!TryGetComponent<BoxCollider2D>(out var box)) box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(w, h);
            box.offset = Vector2.zero;
            box.usedByEffector = true;
            if (!TryGetComponent<PlatformEffector2D>(out var effector)) effector = gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
        }

        // ------------------------------------------------------------------ estilos

        /// <summary>Roca y ladrillo: textura del bioma, brillo interior y las 4 bandas del borde del terreno.</summary>
        private void BuildRock(LevelBiome b, List<RimPoint> contour, float h, float seed,
                               List<Vector3> fillV, List<int> fillT, List<Vector2> fillUv,
                               List<Vector3> detV, List<int> detT, List<Color> detC,
                               List<Vector3> rimV, List<int> rimT, List<Color> rimC)
        {
            float inv = 1f / Mathf.Max(0.5f, b.rockTileUnits);
            var poly = FanRing(contour);
            Vector2 world = transform.position;
            for (int i = 0; i < poly.Count; i++) { fillV.Add(poly[i]); fillUv.Add((poly[i] + world) * inv); }
            FanTriangles(poly.Count, fillT);

            // brillo interior: una cinta hacia dentro que se desvanece (en piezas finas, sólo hasta casi el centro)
            float glow = Mathf.Min(b.innerGlowWidth * 0.5f, h * 0.45f);
            Color gc = b.innerGlow, clear = new Color(gc.r, gc.g, gc.b, 0f);
            Level.Ribbon(contour, detV, detC, detT, i => 0f, i => glow, i => gc, i => clear);

            int n = contour.Count;
            var w = new float[n]; var t = new float[n];
            for (int i = 0; i < n; i++)
            {
                var q = contour[i];
                float floor = Mathf.Clamp01(-q.normal.y), ceiling = Mathf.Clamp01(q.normal.y);
                float baseWidth = b.wallWidth + (b.floorWidth - b.wallWidth) * floor + (b.ceilingWidth - b.wallWidth) * ceiling;
                float wobble = 1f - b.widthVariation * 0.44f + b.widthVariation * Level.Noise(q.distance / 0.6f + seed, 3.3f);
                w[i] = baseWidth * wobble + (Level.Noise(q.distance / 0.175f + seed, 9f) - 0.5f) * 0.075f;
                t[i] = (1f - q.normal.y) * 0.5f; // 0 techo, 1 suelo
            }
            for (int band = 0; band < 4; band++)
            {
                int bi = band;
                float frac = b.bandFractions != null && b.bandFractions.Length > bi ? b.bandFractions[bi] : 1f - bi * 0.25f;
                Level.Ribbon(contour, rimV, rimC, rimT, i => 0f, i => w[i] * frac,
                    i => Color.Lerp(b.ceilingBands[bi], b.floorBands[bi], t[i]),
                    i => Color.Lerp(b.ceilingBands[bi], b.floorBands[bi], t[i]));
            }
            float half = b.outlineWidth * 0.5f;
            Level.Ribbon(contour, rimV, rimC, rimT, i => -half, i => half, i => b.outline, i => b.outline);
        }

        /// <summary>Juntas de ladrillo: hileras de 0,5 de alto, desfasadas, con ladrillos de unas 1,6 unidades.</summary>
        private static void AddBricks(float w, float h, Color outline, List<Vector3> v, List<int> tris, List<Color> cols)
        {
            var line = new Color(outline.r, outline.g, outline.b, 0.55f);
            const float RowH = 0.5f, BrickW = 1.6f, Line = 0.05f;
            float x0 = -w * 0.5f, y0 = -h * 0.5f;
            int rows = Mathf.Max(1, Mathf.RoundToInt(h / RowH));
            float rowH = h / rows;
            for (int r = 0; r < rows; r++)
            {
                float yb = y0 + r * rowH;
                if (r > 0) AddQuad(v, tris, cols, new Vector2(x0 + 0.15f, yb - Line * 0.5f), new Vector2(x0 + w - 0.15f, yb + Line * 0.5f), line);
                float shift = (r % 2) * BrickW * 0.5f;
                for (float x = x0 + 0.4f + shift; x < x0 + w - 0.4f; x += BrickW)
                    AddQuad(v, tris, cols, new Vector2(x - Line * 0.5f, yb + 0.05f), new Vector2(x + Line * 0.5f, yb + rowH - 0.05f), line);
            }
        }

        /// <summary>Hielo: tabla translúcida con degradado, contorno oscuro, filo claro arriba y unos reflejos.</summary>
        private void BuildIce(LevelBiome b, List<RimPoint> contour, float w, float h, float seed,
                              List<Vector3> fillV, List<int> fillT, List<Color> fillC,
                              List<Vector3> detV, List<int> detT, List<Color> detC,
                              List<Vector3> rimV, List<int> rimT, List<Color> rimC)
        {
            var poly = FanRing(contour);
            Color top = WithAlpha(b.floorBands[2], 0.92f), bottom = WithAlpha(Color.Lerp(b.floorBands[0], b.ceilingBands[0], 0.5f), 0.82f);
            for (int i = 0; i < poly.Count; i++)
            {
                fillV.Add(poly[i]);
                fillC.Add(Color.Lerp(bottom, top, Mathf.Clamp01(poly[i].y / h + 0.5f)));
            }
            FanTriangles(poly.Count, fillT);

            // reflejos: franjas inclinadas casi blancas
            var shine = new Color(1f, 1f, 1f, 0.35f);
            int streaks = Mathf.Max(1, Mathf.RoundToInt(w / 1.8f));
            for (int s = 0; s < streaks; s++)
            {
                float cx = -w * 0.5f + (s + 0.3f + 0.4f * Level.Noise(s * 1.7f + seed, 5f)) * (w / streaks);
                float sh = h * 0.28f;
                AddSlantedQuad(detV, detT, detC, new Vector2(cx, -h * 0.5f + sh), h * 0.5f, 0.1f, 0.14f, shine);
            }

            int n = contour.Count;
            var topW = new float[n]; var botW = new float[n];
            for (int i = 0; i < n; i++)
            {
                var q = contour[i];
                topW[i] = Mathf.Min(0.12f, h * 0.3f) * Mathf.Clamp01(-q.normal.y);
                botW[i] = 0.07f * Mathf.Clamp01(q.normal.y);
            }
            Level.Ribbon(contour, rimV, rimC, rimT, i => 0f, i => topW[i], i => b.floorBands[3], i => b.floorBands[2]);
            Level.Ribbon(contour, rimV, rimC, rimT, i => 0f, i => botW[i], i => b.ceilingBands[0], i => b.ceilingBands[0]);
            float half = b.outlineWidth * 0.4f;
            Level.Ribbon(contour, rimV, rimC, rimT, i => -half, i => half, i => b.outline, i => b.outline);
        }

        /// <summary>Cristal: núcleo casi blanco que se vuelve cian hacia el borde, halo exterior y un filo brillante.</summary>
        private void BuildCrystal(LevelBiome b, List<RimPoint> contour, float h,
                                  List<Vector3> fillV, List<int> fillT, List<Color> fillC,
                                  List<Vector3> detV, List<int> detT, List<Color> detC,
                                  List<Vector3> rimV, List<int> rimT, List<Color> rimC)
        {
            Color glow = b.innerGlow; glow.a = 1f;
            Color edge = Color.Lerp(glow, b.floorBands[1], 0.4f), core = Color.Lerp(edge, Color.white, 0.75f);
            var poly = FanRing(contour);
            for (int i = 0; i < poly.Count; i++) { fillV.Add(poly[i]); fillC.Add(i == 0 ? core : edge); }
            FanTriangles(poly.Count, fillT);

            // halo hacia fuera
            Color halo = new Color(glow.r, glow.g, glow.b, 0.4f), clear = new Color(glow.r, glow.g, glow.b, 0f);
            Level.Ribbon(contour, detV, detC, detT, i => 0f, i => -0.6f, i => halo, i => clear);

            int n = contour.Count;
            var topW = new float[n];
            for (int i = 0; i < n; i++) topW[i] = Mathf.Min(0.08f, h * 0.25f) * Mathf.Clamp01(-contour[i].normal.y);
            Level.Ribbon(contour, rimV, rimC, rimT, i => 0f, i => topW[i], i => Color.white, i => core);
            float half = b.outlineWidth * 0.35f;
            Color dark = Color.Lerp(b.outline, glow, 0.35f);
            Level.Ribbon(contour, rimV, rimC, rimT, i => -half, i => half, i => dark, i => dark);
        }

        private void UpdateLight(bool on, float w)
        {
            var existing = transform.Find(LightName);
            if (!on) { if (existing != null) Level.DestroySafe(existing.gameObject); return; }
            var go = existing != null ? existing.gameObject : Child(LightName).gameObject;
            if (!go.TryGetComponent<Light2D>(out var l)) l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(0.45f, 0.85f, 1f);
            l.intensity = 0.7f;
            l.pointLightOuterRadius = w * 0.5f + 2f;
            l.pointLightInnerRadius = 0f;
        }

        // ------------------------------------------------------------------ utilidades

        /// <summary>Rectángulo redondeado (esquinas del bioma) como contorno en sentido horario, centrado en el origen.</summary>
        private static List<RimPoint> Contour(float w, float h, float radius)
        {
            const float S = 100f; // LevelShape trabaja con enteros: centésimas de unidad
            int iw = Mathf.RoundToInt(w * S), ih = Mathf.RoundToInt(h * S);
            var loop = new List<Vector2Int> { new Vector2Int(0, ih), new Vector2Int(iw, ih), new Vector2Int(iw, 0), new Vector2Int(0, 0) };
            var pts = LevelShape.Rounded(loop, Mathf.Min(radius, h * 0.5f, w * 0.5f) * S, 0.15f * S, 12);
            var half = new Vector2(w * 0.5f, h * 0.5f);
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                p.position = p.position / S - half;
                p.distance /= S;
                pts[i] = p;
            }
            return pts;
        }

        /// <summary>Centro + contorno + primer punto: abanico para rellenar un polígono convexo.</summary>
        private static List<Vector2> FanRing(List<RimPoint> contour)
        {
            var poly = new List<Vector2>(contour.Count + 2) { Vector2.zero };
            foreach (var p in contour) poly.Add(p.position);
            poly.Add(contour[0].position);
            return poly;
        }

        private static void FanTriangles(int count, List<int> tris)
        {
            for (int i = 1; i < count - 1; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
        }

        private static void AddQuad(List<Vector3> v, List<int> tris, List<Color> cols, Vector2 min, Vector2 max, Color c)
        {
            int s = v.Count;
            v.Add(new Vector3(min.x, min.y)); v.Add(new Vector3(max.x, min.y)); v.Add(new Vector3(max.x, max.y)); v.Add(new Vector3(min.x, max.y));
            for (int i = 0; i < 4; i++) cols.Add(c);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
            tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
        }

        /// <summary>Franja inclinada (paralelogramo) que arranca en <paramref name="origin"/> y sube <paramref name="height"/>.</summary>
        private static void AddSlantedQuad(List<Vector3> v, List<int> tris, List<Color> cols, Vector2 origin, float height, float width, float slant, Color c)
        {
            int s = v.Count;
            v.Add(origin); v.Add(origin + new Vector2(width, 0f));
            v.Add(origin + new Vector2(width + slant, height)); v.Add(origin + new Vector2(slant, height));
            for (int i = 0; i < 4; i++) cols.Add(c);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
            tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private Transform Child(string childName)
        {
            var t = transform.Find(childName);
            if (t != null) return t;
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }
    }
}
