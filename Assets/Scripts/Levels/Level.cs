using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace RedMagic.Levels
{
    /// <summary>
    /// Un nivel: su tamaño y su terreno. El tamaño se escribe aquí (<see cref="width"/> × <see cref="height"/> en
    /// unidades; 1 unidad = 1 celda = 40 px del boceto web) y de él sale todo lo demás, que se reajusta solo al
    /// cambiarlo: el marco de roca de 1 celda alrededor, el rectángulo de la cámara (<c>BG</c>), el collider y el
    /// rango del parallax. Lo que se pinta en el Tilemap de terreno (con la paleta normal de Unity) se conserva al
    /// cambiar el tamaño; el marco NO se pinta, es implícito.
    ///
    /// El aspecto lo genera el código a partir de la cuadrícula, igual que el boceto web: roca con la textura del
    /// bioma, borde de hielo de grosor variable con esquinas redondeadas, brillo y contorno. Se regenera al pintar,
    /// al cambiar el tamaño o el bioma, y al cargar (las mallas no se guardan en la escena).
    /// El origen del nivel (su posición) es la esquina inferior izquierda.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class Level : MonoBehaviour
    {
        /// <summary>Píxeles del boceto web por unidad de Unity.</summary>
        public const float SketchPixelsPerUnit = 40f;
        /// <summary>Alto máximo: 75 unidades = 3000 px del boceto, el tope que se probó con el fondo.</summary>
        public const int MaxHeight = 75;
        public const int MaxWidth = 400;
        public const int MinSize = 12;

        public const string BackgroundName = "BG", RockName = "Roca", RimName = "Borde", GlowName = "Brillo", CollisionName = "Colision";

        [Tooltip("Ancho del nivel en unidades (celdas), marco incluido. 1 unidad = 40 px del boceto.")]
        [SerializeField] private int width = 85;

        [Tooltip("Alto del nivel en unidades (celdas), marco incluido. Máximo 75 (= 3000 px del boceto).")]
        [SerializeField] private int height = 25;

        [Tooltip("Bioma: textura de la roca, colores del borde, fondo y partículas.")]
        [SerializeField] private LevelBiome biome;

        [Tooltip("Tilemap donde se pinta el terreno. Cualquier tile cuenta como roca.")]
        [SerializeField] private Tilemap terrain;

        [Tooltip("Orden de dibujo (capa Terrain) de la roca; el borde va justo encima.")]
        [SerializeField] private int sortingOrder = 2;

        public int Width => width;
        public int Height => height;
        public LevelBiome Biome => biome;
        public Tilemap Terrain => terrain;

        /// <summary>Rectángulo del nivel en mundo (marco incluido).</summary>
        public Rect WorldRect => new Rect((Vector2)transform.position, new Vector2(width, height));

        private bool _dirty = true;
        private Mesh _rockMesh, _rimMesh, _glowMesh;
        private Material _rockMaterial, _rimMaterial, _glowMaterial;
        private Texture2D _glowTexture;
        private Sprite _bgSprite;

        // ------------------------------------------------------------------ ciclo de vida

        private void OnEnable()
        {
            Tilemap.tilemapTileChanged += OnTilesChanged;
            _dirty = true;
            Rebuild();
        }

        private void OnDisable() => Tilemap.tilemapTileChanged -= OnTilesChanged;

        private void OnDestroy()
        {
            DestroySafe(_rockMesh); DestroySafe(_rimMesh); DestroySafe(_glowMesh);
            DestroySafe(_glowMaterial); DestroySafe(_glowTexture);
            DestroySafe(_rockMaterial); DestroySafe(_rimMaterial);
            if (_bgSprite != null) DestroySafe(_bgSprite);
        }

        private void OnValidate()
        {
            width = Mathf.Clamp(width, MinSize, MaxWidth);
            height = Mathf.Clamp(height, MinSize, MaxHeight);
            _dirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void Update()
        {
            if (_dirty) Rebuild();
        }

        private void OnTilesChanged(Tilemap map, Tilemap.SyncTile[] tiles)
        {
            if (map != terrain) return;
            _dirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        /// <summary>Cambia el tamaño (desde código: importador, generador de escenas de prueba).</summary>
        public void SetSize(int newWidth, int newHeight)
        {
            width = Mathf.Clamp(newWidth, MinSize, MaxWidth);
            height = Mathf.Clamp(newHeight, MinSize, MaxHeight);
            _dirty = true;
        }

        public void SetBiome(LevelBiome newBiome) { biome = newBiome; _dirty = true; }

        public void SetTerrain(Tilemap map) { terrain = map; _dirty = true; }

        /// <summary>Roca en (x, y) de la cuadrícula del nivel; fuera del nivel y en el marco, siempre.</summary>
        public bool IsSolid(int x, int y)
        {
            if (x <= 0 || y <= 0 || x >= width - 1 || y >= height - 1) return true;
            return terrain != null && terrain.HasTile(new Vector3Int(x, y, 0));
        }

        /// <summary>Altura (y local) del primer suelo libre en la columna x, buscando desde abajo.</summary>
        public float GroundTop(int x)
        {
            x = Mathf.Clamp(x, 1, width - 2);
            int y = 1;
            while (y < height - 1 && IsSolid(x, y)) y++;
            return y;
        }

        // ------------------------------------------------------------------ construcción

        [ContextMenu("Regenerar")]
        public void Rebuild()
        {
            _dirty = false;
            if (terrain == null) terrain = GetComponentInChildren<Tilemap>(true);

            BuildBackgroundRect();
            if (biome == null) return;

            float radius = biome.cornerRadius;
            var polygons = new List<List<Vector2>>();
            LevelShape.FillPolygons(IsSolid, width, height, radius, 6, polygons);
            var loops = LevelShape.Loops(IsSolid, width, height);

            BuildRock(polygons);
            BuildGlow(polygons);
            BuildRim(polygons, loops, radius);
            BuildCollision(loops, radius);
        }

        private void BuildBackgroundRect()
        {
            // "BG" es lo que mide CameraFollow para no enseñar nada fuera del nivel: invisible, del tamaño del nivel.
            var bg = Child(BackgroundName);
            if (_bgSprite == null)
            {
                _bgSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero, 4f);
                _bgSprite.name = "LevelBounds";
                _bgSprite.hideFlags = HideFlags.DontSave;
            }
            if (!bg.TryGetComponent<SpriteRenderer>(out var sr)) sr = bg.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = _bgSprite;
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingLayerName = "BackGround";
            sr.sortingOrder = -1000;
            bg.localPosition = Vector3.zero;
            bg.localScale = new Vector3(width, height, 1f);
        }

        private void BuildRock(List<List<Vector2>> polygons)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float inv = 1f / Mathf.Max(0.5f, biome.rockTileUnits);
            foreach (var poly in polygons) AddFan(poly, verts, tris, uvs, inv, null, default);

            _rockMesh = Assign(_rockMesh, "LevelRock", verts, tris, uvs, null);
            var shader = biome.rockShader != null ? biome.rockShader : Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            _rockMaterial = GetMaterial(ref _rockMaterial, shader, "LevelRock");
            _rockMaterial.mainTexture = biome.rockTexture;
            SetupRenderer(Child(RockName), _rockMesh, _rockMaterial, sortingOrder);
        }

        private void BuildRim(List<List<Vector2>> polygons, List<List<Vector2Int>> loops, float radius)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();


            var contours = new List<List<RimPoint>>();
            foreach (var loop in loops) contours.Add(LevelShape.Rounded(loop, radius, 0.15f, 24));

            Color clear(Color c) => new Color(c.r, c.g, c.b, 0f);
            foreach (var pts in contours)
            {
                int n = pts.Count;
                var w = new float[n];
                var t = new float[n];
                for (int i = 0; i < n; i++)
                {
                    var q = pts[i];
                    float floor = Mathf.Clamp01(-q.normal.y), ceiling = Mathf.Clamp01(q.normal.y);
                    float baseWidth = biome.wallWidth + (biome.floorWidth - biome.wallWidth) * floor
                                      + (biome.ceilingWidth - biome.wallWidth) * ceiling;
                    float wobble = 1f - biome.widthVariation * 0.44f + biome.widthVariation * Noise(q.distance / 0.6f, 3.3f);
                    w[i] = baseWidth * wobble + (Noise(q.distance / 0.175f, 9f) - 0.5f) * 0.075f;
                    t[i] = (1f - q.normal.y) * 0.5f; // 0 techo, 1 suelo
                }

                // (sombra y brillo interior: textura por distancia, ver BuildGlow)

                // 3. las 4 bandas del borde, de la más ancha a la más estrecha
                for (int b = 0; b < 4; b++)
                {
                    int band = b;
                    float frac = biome.bandFractions != null && biome.bandFractions.Length > band ? biome.bandFractions[band] : 1f - band * 0.25f;
                    Ribbon(pts, verts, cols, tris, i => 0f, i => w[i] * frac,
                        i => Color.Lerp(biome.ceilingBands[band], biome.floorBands[band], t[i]),
                        i => Color.Lerp(biome.ceilingBands[band], biome.floorBands[band], t[i]));
                }

                // 4. brillo hacia el aire y 5. contorno oscuro
                Ribbon(pts, verts, cols, tris, i => 0f, i => -biome.outerGlowWidth, i => biome.outerGlow, i => clear(biome.outerGlow));
                float half = biome.outlineWidth * 0.5f;
                Ribbon(pts, verts, cols, tris, i => -half, i => half, i => biome.outline, i => biome.outline);
            }

            _rimMesh = Assign(_rimMesh, "LevelRim", verts, tris, null, cols);
            var shader = biome.rimShader != null ? biome.rimShader : Shader.Find("RedMagic/Fx Vertex Color");
            _rimMaterial = GetMaterial(ref _rimMaterial, shader, "LevelRim");
            SetupRenderer(Child(RimName), _rimMesh, _rimMaterial, sortingOrder + 2);
        }

        private void BuildCollision(List<List<Vector2Int>> loops, float radius)
        {
            var col = Child(CollisionName);
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0) col.gameObject.layer = ground;
            foreach (var old in col.GetComponents<EdgeCollider2D>()) DestroySafe(old);

            foreach (var loop in loops)
            {
                var pts = LevelShape.Rounded(loop, radius, 1000f, 4);
                var path = new List<Vector2>(pts.Count + 1);
                foreach (var p in pts) path.Add(p.position);
                path.Add(pts[0].position);
                var edge = col.gameObject.AddComponent<EdgeCollider2D>();
                edge.SetPoints(path);
            }
        }

        /// <summary>
        /// Sombra de la roca + brillo hacia dentro, por DISTANCIA al aire (como el desenfoque del boceto web): se
        /// rasteriza la roca ya redondeada a <see cref="GlowTexelsPerUnit"/> texels por unidad, se calcula la distancia
        /// euclídea exacta de cada texel al aire y el brillo cae con ella. Uniforme en cualquier esquina; las cintas
        /// que salían del borde se abrían en abanico en las esquinas y dejaban cuñas de luz.
        /// </summary>
        private const int GlowTexelsPerUnit = 8;

        private void BuildGlow(List<List<Vector2>> polygons)
        {
            int R = GlowTexelsPerUnit, tw = width * R, th = height * R;
            var inside = new bool[tw * th];
            var pt = new Vector2();
            foreach (var poly in polygons)
            {
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var p in poly) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                int x0 = Mathf.Max(0, Mathf.FloorToInt(minX * R)), x1 = Mathf.Min(tw - 1, Mathf.CeilToInt(maxX * R));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(minY * R)), y1 = Mathf.Min(th - 1, Mathf.CeilToInt(maxY * R));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int k = y * tw + x;
                    if (inside[k]) continue;
                    pt.x = (x + 0.5f) / R; pt.y = (y + 0.5f) / R;
                    if (Contains(poly, pt)) inside[k] = true;
                }
            }

            var dist = DistanceToAir(inside, tw, th); // en texels
            float wide = Mathf.Max(0.01f, biome.innerGlowWidth) * R, narrow = wide * 0.45f;
            var pixels = new Color32[tw * th];
            Color sc = biome.rockShadow, gc = biome.innerGlow;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (!inside[i]) { pixels[i] = new Color32(0, 0, 0, 0); continue; }
                float d = dist[i];
                float fa = d < wide ? (1f - d / wide) * (1f - d / wide) : 0f, fb = d < narrow ? (1f - d / narrow) * (1f - d / narrow) : 0f;
                float ga = Mathf.Clamp01(gc.a * (fa + fb)), sa = sc.a;
                float a = ga + sa * (1f - ga);
                Color c = a > 0f ? (gc * ga + sc * (sa * (1f - ga))) / a : sc;
                c.a = a;
                pixels[i] = c;
            }

            if (_glowTexture == null || _glowTexture.width != tw || _glowTexture.height != th)
            {
                DestroySafe(_glowTexture);
                _glowTexture = new Texture2D(tw, th, TextureFormat.RGBA32, false) { name = "LevelGlow", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            }
            _glowTexture.SetPixels32(pixels);
            _glowTexture.Apply(false);

            var verts = new List<Vector3> { new Vector3(0, 0), new Vector3(width, 0), new Vector3(width, height), new Vector3(0, height) };
            var uvs = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var cols = new List<Color> { Color.white, Color.white, Color.white, Color.white };
            _glowMesh = Assign(_glowMesh, "LevelGlow", verts, new List<int> { 0, 2, 1, 0, 3, 2 }, uvs, cols);
            _glowMaterial = GetMaterial(ref _glowMaterial, Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"), "LevelGlow");
            _glowMaterial.mainTexture = _glowTexture;
            SetupRenderer(Child(GlowName), _glowMesh, _glowMaterial, sortingOrder + 1);
        }

        private static bool Contains(List<Vector2> poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) c = !c;
            }
            return c;
        }

        /// <summary>Distancia euclídea exacta (en texels) de cada texel de roca al texel de aire más cercano (Felzenszwalb).</summary>
        private static float[] DistanceToAir(bool[] inside, int w, int h)
        {
            const double Inf = 1e20;
            var f = new double[w * h];
            for (int i = 0; i < f.Length; i++) f[i] = inside[i] ? Inf : 0.0;
            int n = Mathf.Max(w, h);
            var line = new double[n]; var outLine = new double[n]; var v = new int[n]; var z = new double[n + 1];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++) line[y] = f[y * w + x];
                Edt1D(line, h, outLine, v, z);
                for (int y = 0; y < h; y++) f[y * w + x] = outLine[y];
            }
            var result = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) line[x] = f[y * w + x];
                Edt1D(line, w, outLine, v, z);
                for (int x = 0; x < w; x++) result[y * w + x] = (float)System.Math.Sqrt(outLine[x]);
            }
            return result;
        }

        private static void Edt1D(double[] f, int n, double[] d, int[] v, double[] z)
        {
            int k = 0;
            v[0] = 0; z[0] = double.NegativeInfinity; z[1] = double.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                double s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                }
                k++; v[k] = q; z[k] = s; z[k + 1] = double.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                d[q] = (double)(q - v[k]) * (q - v[k]) + f[v[k]];
            }
        }

        // ------------------------------------------------------------------ utilidades de malla

        /// <summary>Cinta a lo largo del contorno: de <c>pos + normal·from</c> a <c>pos + normal·to</c>, cerrada.</summary>
        private static void Ribbon(List<RimPoint> pts, List<Vector3> verts, List<Color> cols, List<int> tris,
                                   System.Func<int, float> from, System.Func<int, float> to,
                                   System.Func<int, Color> colorFrom, System.Func<int, Color> colorTo)
        {
            int n = pts.Count, start = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var q = pts[i];
                verts.Add(q.position + q.normal * from(i));
                verts.Add(q.position + q.normal * to(i));
                cols.Add(colorFrom(i));
                cols.Add(colorTo(i));
            }
            for (int i = 0; i < n; i++)
            {
                int a = start + i * 2, b = start + ((i + 1) % n) * 2;
                tris.Add(a); tris.Add(b); tris.Add(b + 1);
                tris.Add(a); tris.Add(b + 1); tris.Add(a + 1);
            }
        }

        private static void AddFan(List<Vector2> poly, List<Vector3> verts, List<int> tris, List<Vector2> uvs, float uvScale,
                                   List<Color> cols, Color color)
        {
            int start = verts.Count;
            foreach (var p in poly)
            {
                verts.Add(p);
                uvs?.Add(p * uvScale);
                cols?.Add(color);
            }
            for (int i = 1; i < poly.Count - 1; i++)
            {
                tris.Add(start); tris.Add(start + i); tris.Add(start + i + 1);
            }
        }

        private static Mesh Assign(Mesh mesh, string name, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> cols)
        {
            if (mesh == null) mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            if (uvs != null) mesh.SetUVs(0, uvs);
            if (cols != null) mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material GetMaterial(ref Material material, Shader shader, string name)
        {
            if (material == null || material.shader != shader)
            {
                DestroySafe(material);
                material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            }
            return material;
        }

        private static void SetupRenderer(Transform child, Mesh mesh, Material material, int order)
        {
            if (!child.TryGetComponent<MeshFilter>(out var mf)) mf = child.gameObject.AddComponent<MeshFilter>();
            if (!child.TryGetComponent<MeshRenderer>(out var mr)) mr = child.gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = material;
            mr.sortingLayerName = "Terrain";
            mr.sortingOrder = order;
        }

        private Transform Child(string childName)
        {
            var t = transform.Find(childName);
            if (t != null) return t;
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>Ruido de valor 1D en [0, 1] (mismo papel que el del boceto web).</summary>
        private static float Noise(float x, float seed)
        {
            float i = Mathf.Floor(x), f = x - i;
            float u = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i, seed), Hash(i + 1f, seed), u);
        }

        private static float Hash(float i, float seed)
        {
            float h = Mathf.Sin(i * 127.1f + seed * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Rectángulo del nivel y, en rojo, el tope de 3000 px para no pasarse sin querer.
            Vector3 o = transform.position;
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireCube(o + new Vector3(width * 0.5f, height * 0.5f), new Vector3(width, height));
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
            Gizmos.DrawLine(o + new Vector3(0, MaxHeight), o + new Vector3(width, MaxHeight));
        }
#endif
    }
}
