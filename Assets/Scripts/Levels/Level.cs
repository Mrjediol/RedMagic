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
    /// bioma, borde de hielo de grosor variable con esquinas redondeadas, brillo y contorno. Se regenera al pintar y
    /// al cambiar el tamaño o el bioma. <b>Al cargar NO se genera</b>: se usa lo horneado al guardar la escena
    /// (<see cref="LevelBake"/>, escrito por <c>LevelBaker</c>); sólo si falta o ya no cuadra con los tiles/bioma
    /// (<see cref="Signature"/>) se genera como antes.
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

        public const string BackgroundName = "BG", RockName = "Roca", RimName = "Borde", GlowName = "Brillo", CollisionName = "Colision", PlatformsName = "Plataformas";

        [Tooltip("Ancho del nivel en unidades (celdas), marco incluido. 1 unidad = 40 px del boceto.")]
        [SerializeField] private int width = 85;

        [Tooltip("Alto del nivel en unidades (celdas), marco incluido. Máximo 75 (= 3000 px del boceto).")]
        [SerializeField] private int height = 25;

        [Tooltip("Bioma: textura de la roca, colores del borde, fondo y partículas.")]
        [SerializeField] private LevelBiome biome;

        [Tooltip("Tilemap donde se pinta el terreno. Cualquier tile cuenta como roca.")]
        [SerializeField] private Tilemap terrain;

        [Tooltip("Tilemap de plataformas: se ven igual que el terreno, pero se atraviesan desde abajo y se puede " +
                 "bajar de ellas (capa Platform, una por plataforma). Opcional.")]
        [SerializeField] private Tilemap platforms;

        [Header("Nieve del borde")]
        [Tooltip("Bandas claras (nieve/hielo) del borde del terreno. Apagado: sólo roca y contorno.")]
        [SerializeField] private bool rimOnTerrain = true;

        [Tooltip("Bandas claras del borde en las plataformas. En las de 1 celda de grosor las de arriba y abajo se juntan.")]
        [SerializeField] private bool rimOnPlatforms = true;

        [Tooltip("Aspecto horneado (lo pone el editor al guardar la escena). No se toca a mano.")]
        [SerializeField, HideInInspector] private LevelBake bake;

        /// <summary>Sube si cambia el código que genera el aspecto: invalida todos los horneados.</summary>
        public const int GeneratorVersion = 1;


        public int Width => width;
        public int Height => height;
        public LevelBiome Biome => biome;
        public Tilemap Terrain => terrain;
        public Tilemap Platforms => platforms;

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
            LevelBiome.Changed += OnBiomeChanged;
            _dirty = true;
            if (!TryUseBake()) Rebuild();
        }

        private void OnDisable()
        {
            Tilemap.tilemapTileChanged -= OnTilesChanged;
            LevelBiome.Changed -= OnBiomeChanged;
        }

        /// <summary>Tocar un valor del bioma en el Inspector (radio de esquina, colores…) redibuja al momento.</summary>
        private void OnBiomeChanged(LevelBiome changed)
        {
            if (changed != biome) return;
            _dirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

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
            if (_dirty && !TryUseBake()) Rebuild();
        }

        // ------------------------------------------------------------------ horneado

        public LevelBake Bake => bake;

        /// <summary>Lo que se está viendo ahora (para hornearlo). Null si aún no se ha generado en esta sesión.</summary>
        public Mesh RockMesh => _rockMesh;
        public Mesh RimMesh => _rimMesh;
        public Mesh GlowMesh => _glowMesh;
        public Material RockMaterial => _rockMaterial;
        public Material RimMaterial => _rimMaterial;
        public Material GlowMaterial => _glowMaterial;
        public Texture2D GlowTexture => _glowTexture;

        /// <summary>Lo pone el horneador del editor tras guardar el aspecto en <paramref name="baked"/>.</summary>
        public void SetBake(LevelBake baked)
        {
            bake = baked;
            if (bake != null) ApplyBake();
        }

        /// <summary>
        /// ¿Hay un horneado bueno? Si lo hay, los renderers lo usan y no se genera nada. En el editor se comprueba que
        /// cuadre con los tiles, el tamaño y el bioma de ahora; en una build se confía en él (se hornea al guardar).
        /// </summary>
        private bool TryUseBake()
        {
            if (bake == null || !bake.IsComplete || biome == null) return false;
#if UNITY_EDITOR
            if (bake.signature != Signature()) return false;
#endif
            _dirty = false;
            ApplyBake();
            return true;
        }

        private void ApplyBake()
        {
            if (terrain == null) terrain = GetComponentInChildren<Tilemap>(true);
            BuildBackgroundRect();
            SetupRenderer(Child(RockName), bake.rock, bake.rockMaterial, LevelSorting.Rock);
            SetupRenderer(Child(GlowName), bake.glow, bake.glowMaterial, LevelSorting.Glow);
            SetupRenderer(Child(RimName), bake.rim, bake.rimMaterial, LevelSorting.Rim);
            // los colliders (Colision y Plataformas) ya vienen guardados en la escena
        }

        /// <summary>
        /// Huella de todo lo que decide el aspecto: tamaño, interruptores del borde, valores del bioma, tiles de roca y de
        /// plataforma y la versión del generador. Si cambia, el horneado ya no vale.
        /// </summary>
        public string Signature()
        {
            var sb = new System.Text.StringBuilder(4096);
            sb.Append(GeneratorVersion).Append('|').Append(width).Append('x').Append(height)
              .Append('|').Append(rimOnTerrain).Append(rimOnPlatforms).Append('|');
            if (biome != null)
            {
                // los ids de instancia de las referencias cambian entre sesiones: fuera, y en su lugar los nombres
                string json = System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(biome), "\"instanceID\":-?\\d+", "");
                sb.Append(biome.name).Append(json)
                  .Append(biome.rockTexture != null ? biome.rockTexture.name : "-")
                  .Append(biome.rockShader != null ? biome.rockShader.name : "-")
                  .Append(biome.rimShader != null ? biome.rimShader.name : "-");
            }
            AppendTiles(sb, terrain);
            sb.Append('#');
            AppendTiles(sb, platforms);
            return Hash128.Compute(sb.ToString()).ToString();
        }

        private static void AppendTiles(System.Text.StringBuilder sb, Tilemap map)
        {
            if (map == null) return;
            foreach (var p in map.cellBounds.allPositionsWithin)
                if (map.HasTile(p)) sb.Append(p.x).Append(',').Append(p.y).Append(';');
        }

        private void OnTilesChanged(Tilemap map, Tilemap.SyncTile[] tiles)
        {
            if (map != terrain && map != platforms) return;
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

        public void SetPlatforms(Tilemap map) { platforms = map; _dirty = true; }

        /// <summary>Roca maciza (colisiona por todos lados): marco, terreno pintado.</summary>
        public bool IsRock(int x, int y)
        {
            if (x <= 0 || y <= 0 || x >= width - 1 || y >= height - 1) return true;
            return terrain != null && terrain.HasTile(new Vector3Int(x, y, 0));
        }

        /// <summary>Celda de plataforma (y no de roca).</summary>
        public bool IsPlatform(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height || IsRock(x, y)) return false;
            return platforms != null && platforms.HasTile(new Vector3Int(x, y, 0));
        }

        /// <summary>Roca en (x, y) de la cuadrícula del nivel; fuera del nivel y en el marco, siempre.</summary>
        public bool IsSolid(int x, int y) => IsRock(x, y) || IsPlatform(x, y);

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
            BuildCollision(LevelShape.Loops(IsRock, width, height));
            BuildPlatforms(LevelShape.Loops(IsPlatform, width, height));
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
            sr.sortingLayerName = LevelSorting.Layer;
            sr.sortingOrder = LevelSorting.Bounds;
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
            SetupRenderer(Child(RockName), _rockMesh, _rockMaterial, LevelSorting.Rock);
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
                    // la normal apunta hacia dentro del sólido: la celda de al lado dice si es roca o plataforma
                    var inside = q.position + q.normal * 0.3f;
                    if (!(IsPlatform(Mathf.FloorToInt(inside.x), Mathf.FloorToInt(inside.y)) ? rimOnPlatforms : rimOnTerrain)) w[i] = 0f;
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
                if (biome.outerGlowWidth > 0f && biome.outerGlow.a > 0f)
                    Ribbon(pts, verts, cols, tris, i => 0f, i => -biome.outerGlowWidth, i => biome.outerGlow, i => clear(biome.outerGlow));
                // contorno oscuro
                float half = biome.outlineWidth * 0.5f;
                Ribbon(pts, verts, cols, tris, i => -half, i => half, i => biome.outline, i => biome.outline);
            }

            _rimMesh = Assign(_rimMesh, "LevelRim", verts, tris, null, cols);
            var shader = biome.rimShader != null ? biome.rimShader : Shader.Find("RedMagic/Fx Vertex Color");
            _rimMaterial = GetMaterial(ref _rimMaterial, shader, "LevelRim");
            SetupRenderer(Child(RimName), _rimMesh, _rimMaterial, LevelSorting.Rim);
        }

        /// <summary>
        /// Collider de la roca: los vértices de la cuadrícula tal cual, <b>esquinas cuadradas</b>. El redondeo de
        /// <see cref="LevelBiome.cornerRadius"/> es sólo visual: el juego no tiene superficies inclinadas.
        /// </summary>
        private void BuildCollision(List<List<Vector2Int>> loops)
        {
            var col = Child(CollisionName);
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0) col.gameObject.layer = ground;
            foreach (var old in col.GetComponents<EdgeCollider2D>()) DestroySafe(old);

            foreach (var loop in loops)
            {
                var path = new List<Vector2>(loop.Count + 1);
                foreach (var p in loop) path.Add(p);
                path.Add(loop[0]);
                var edge = col.gameObject.AddComponent<EdgeCollider2D>();
                edge.SetPoints(path);
            }
        }

        /// <summary>
        /// Una plataforma = un objeto en la capa Platform con su PolygonCollider2D (esquinas cuadradas, como
        /// la roca) y un PlatformEffector2D de un solo sentido. Separadas porque el jugador, al dejarse caer, ignora la
        /// plataforma concreta que pisa (<c>PlayerMovement.TryDropThroughPlatform</c>).
        /// </summary>
        private void BuildPlatforms(List<List<Vector2Int>> loops)
        {
            var root = Child(PlatformsName);
            for (int i = root.childCount - 1; i >= 0; i--) DestroySafe(root.GetChild(i).gameObject);
            int layer = LayerMask.NameToLayer("Platform");
            for (int i = 0; i < loops.Count; i++)
            {
                var path = new Vector2[loops[i].Count];
                for (int k = 0; k < path.Length; k++) path[k] = loops[i][k];
                var go = new GameObject("Plataforma " + (i + 1));
                go.transform.SetParent(root, false);
                if (layer >= 0) go.layer = layer;
                var poly = go.AddComponent<PolygonCollider2D>();
                poly.SetPath(0, path);
                poly.usedByEffector = true;
                var effector = go.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 160f;
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
            SetupRenderer(Child(GlowName), _glowMesh, _glowMaterial, LevelSorting.Glow);
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
        internal static void Ribbon(List<RimPoint> pts, List<Vector3> verts, List<Color> cols, List<int> tris,
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

        internal static void AddFan(List<Vector2> poly, List<Vector3> verts, List<int> tris, List<Vector2> uvs, float uvScale,
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

        internal static Mesh Assign(Mesh mesh, string name, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> cols)
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

        internal static Material GetMaterial(ref Material material, Shader shader, string name)
        {
            if (material == null || material.shader != shader)
            {
                DestroySafe(material);
                material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            }
            return material;
        }

        internal static void SetupRenderer(Transform child, Mesh mesh, Material material, int order)
        {
            if (!child.TryGetComponent<MeshFilter>(out var mf)) mf = child.gameObject.AddComponent<MeshFilter>();
            if (!child.TryGetComponent<MeshRenderer>(out var mr)) mr = child.gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = material;
            mr.sortingLayerName = LevelSorting.Layer;
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

        internal static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>Ruido de valor 1D en [0, 1] (mismo papel que el del boceto web).</summary>
        internal static float Noise(float x, float seed)
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
