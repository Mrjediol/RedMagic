using System;
using System.Collections.Generic;
using System.IO;
using RedMagic.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Montaje del sistema de niveles: bioma, tile y paleta de terreno, importación del fondo de parallax desde la
    /// página de prueba web, nieve, postproceso y escenas de prueba. Todo idempotente: se puede relanzar.
    /// Menú: <b>Tools ▸ RedMagic ▸ Niveles</b>.
    /// </summary>
    public static class LevelSetup
    {
        public const string BiomesRoot = "Assets/Levels/_Biomas";
        private const string IceFolder = BiomesRoot + "/Hielo";
        private const string IceBiomePath = IceFolder + "/Hielo.asset";
        private const string TilePath = BiomesRoot + "/Roca.asset";
        private const string TileSpritePath = BiomesRoot + "/roca_tile.png";
        private const string PaletteName = "PaletaTerreno";
        private const string TemplateScene = "Assets/Scenes/Test/TestMap.unity";
        private const string TestScenesFolder = "Assets/Scenes/Test";
        /// <summary>La página de parallax mide en px de una pantalla de 720 px; aquí la pantalla mide 20 unidades.</summary>
        private const float ScreenPxToUnits = 20f / 720f;

        // ------------------------------------------------------------------ bioma, tile, paleta

        [MenuItem("Tools/RedMagic/Niveles/Preparar bioma de hielo")]
        public static LevelBiome EnsureIceBiome()
        {
            Directory.CreateDirectory(IceFolder);
            var rock = ImportTexture(IceFolder + "/roca.jpg", repeat: true, maxSize: 2048);
            var biome = AssetDatabase.LoadAssetAtPath<LevelBiome>(IceBiomePath);
            if (biome == null)
            {
                biome = ScriptableObject.CreateInstance<LevelBiome>();
                AssetDatabase.CreateAsset(biome, IceBiomePath);
            }
            biome.rockTexture = rock;
            biome.rockShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            biome.rimShader = Shader.Find("RedMagic/Fx Vertex Color");
            if (biome.particles == null) biome.particles = EnsureSnow();
            EditorUtility.SetDirty(biome);
            AssetDatabase.SaveAssets();
            return biome;
        }

        [MenuItem("Tools/RedMagic/Niveles/Crear tile y paleta de terreno")]
        public static TileBase EnsureTileAndPalette()
        {
            Directory.CreateDirectory(BiomesRoot);
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
            if (tile == null)
            {
                var sprite = SaveSprite(TileSpritePath, 32, 32, (x, y) =>
                    x == 0 || y == 0 || x == 31 || y == 31 ? new Color(0.1f, 0.25f, 0.45f, 1f) : new Color(0.35f, 0.62f, 0.85f, 1f), 32);
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, TilePath);
            }

            string palettePath = $"{BiomesRoot}/{PaletteName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(palettePath) == null)
            {
                var created = GridPaletteUtility.CreateNewPalette(BiomesRoot, PaletteName, GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
                string path = created != null ? AssetDatabase.GetAssetPath(created) : palettePath;
                var contents = PrefabUtility.LoadPrefabContents(path);
                var map = contents.GetComponentInChildren<Tilemap>();
                if (map != null) map.SetTile(Vector3Int.zero, tile);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
            return tile;
        }

        // ------------------------------------------------------------------ fondo de parallax

        [Serializable] private class SceneJson { public LayerJson[] layers; }

        [Serializable]
        private class LayerJson
        {
            public float f, s, y, op = 100, bl, fd, cr, fy = 1, fp = 1, x;
            public string al = "c", mode = "r", vmode = "p", image;
            public bool on = true, key, front;
        }

        [MenuItem("Tools/RedMagic/Niveles/Importar fondo de parallax (.json)...")]
        private static void ImportBackgroundMenu()
        {
            string json = EditorUtility.OpenFilePanel("Escena exportada de la página de parallax", "", "json");
            if (string.IsNullOrEmpty(json)) return;
            var biome = EnsureIceBiome();
            var prefab = ImportBackground(json, IceFolder + "/Fondo", "Fondo_Hielo");
            biome.background = prefab;
            EditorUtility.SetDirty(biome);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(prefab);
        }

        /// <summary>
        /// Convierte la escena exportada (<c>Exportar escena</c> de <c>parallax-test.html</c>) en un prefab con una
        /// <see cref="ParallaxBackgroundLayer"/> por capa, con los mismos valores. Las imágenes se guardan en
        /// <paramref name="folder"/> tal cual (ya limpias en ComfyUI); si una capa traía "quitar magenta" o recorte
        /// de bordes, se aplica aquí. El difuminado no se reproduce (se avisa).
        /// </summary>
        public static GameObject ImportBackground(string jsonPath, string folder, string prefabName)
        {
            var data = JsonUtility.FromJson<SceneJson>(File.ReadAllText(jsonPath));
            if (data?.layers == null) throw new Exception("El JSON no tiene 'layers'.");
            Directory.CreateDirectory(folder);
            var shader = Shader.Find("RedMagic/Parallax Layer");

            var root = new GameObject(prefabName);
            try
            {
                for (int i = 0; i < data.layers.Length; i++)
                {
                    var l = data.layers[i];
                    if (string.IsNullOrEmpty(l.image)) continue;
                    var tex = SaveLayerImage(l, $"{folder}/capa{i + 1}", i);
                    var go = new GameObject($"Capa {i + 1}");
                    go.transform.SetParent(root.transform, false);
                    go.SetActive(l.on);
                    var layer = go.AddComponent<ParallaxBackgroundLayer>();
                    layer.Configure(tex, shader, l.f,
                        l.mode == "e" ? ParallaxRepeat.LevelEnds : l.mode == "m" ? ParallaxRepeat.Mirror : ParallaxRepeat.Repeat,
                        l.x * ScreenPxToUnits, l.s, l.y,
                        l.vmode == "t" ? ParallaxVerticalAnchor.Ceiling : l.vmode == "b" ? ParallaxVerticalAnchor.Floor : ParallaxVerticalAnchor.Proportional,
                        l.al == "t" ? ParallaxAlign.Top : l.al == "b" ? ParallaxAlign.Bottom : ParallaxAlign.Center,
                        l.fp, l.fy, l.op / 100f, l.fd / 100f, l.front, i);
                    if (l.bl > 0) Debug.LogWarning($"[LevelSetup] Capa {i + 1}: difuminar {l.bl}px no se reproduce en Unity todavía.");
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{prefabName}.prefab");
                Debug.Log($"[LevelSetup] Fondo importado: {data.layers.Length} capas → {folder}/{prefabName}.prefab");
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Texture2D SaveLayerImage(LayerJson l, string pathNoExt, int index)
        {
            int comma = l.image.IndexOf(',');
            bool png = l.image.StartsWith("data:image/png");
            byte[] bytes = Convert.FromBase64String(l.image.Substring(comma + 1));
            string path = pathNoExt + (png ? ".png" : ".jpg");

            if (l.key || l.cr > 0)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(bytes);
                int crop = Mathf.Clamp(Mathf.RoundToInt(l.cr), 0, tex.width / 4), w = tex.width - crop * 2, h = tex.height;
                var src = tex.GetPixels32();
                var dst = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = src[y * tex.width + x + crop];
                    if (l.key)
                    {
                        float m = (c.r + c.b) * 0.5f - c.g;
                        if (m > 35)
                        {
                            c.a = (byte)(c.a * Mathf.Clamp01(1f - (m - 35f) / 90f));
                            float k = 0.85f * Mathf.Min(1f, m / 120f);
                            c.r = (byte)Mathf.Max(0, c.r - m * k);
                            c.b = (byte)Mathf.Max(0, c.b - m * k);
                        }
                    }
                    dst[y * w + x] = c;
                }
                Object.DestroyImmediate(tex);
                var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                outTex.SetPixels32(dst);
                bytes = outTex.EncodeToPNG();
                Object.DestroyImmediate(outTex);
                path = pathNoExt + ".png";
            }

            File.WriteAllBytes(path, bytes);
            return ImportTexture(path, repeat: false, maxSize: 8192);
        }

        // ------------------------------------------------------------------ nieve y postproceso

        [MenuItem("Tools/RedMagic/Niveles/Crear nieve")]
        public static GameObject EnsureSnow()
        {
            string prefabPath = IceFolder + "/Nieve.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            var flake = SaveTexture(IceFolder + "/copo.png", 32, 32, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16)) / 16f;
                float a = Mathf.Clamp01(1f - d);
                return new Color(0.95f, 0.98f, 1f, a * a);
            });
            var mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")) { mainTexture = flake };
            AssetDatabase.CreateAsset(mat, IceFolder + "/Copo.mat");

            var root = new GameObject("Nieve");
            try
            {
                root.AddComponent<FollowCameraView>();
                // Plano del juego: los copos se posan en el suelo y las plataformas (y se desvanecen).
                Snow(root.transform, "Nieve plano", mat, collide: true, sizes: new Vector2(0.07f, 0.24f), alpha: 0.9f,
                     rate: 22f, fall: new Vector2(-1.8f, -0.6f), layer: "Characters", order: 20);
                // Detrás del terreno: más pequeños y tenues, atraviesan todo.
                Snow(root.transform, "Nieve fondo", mat, collide: false, sizes: new Vector2(0.04f, 0.12f), alpha: 0.45f,
                     rate: 30f, fall: new Vector2(-1.1f, -0.4f), layer: "BackGround", order: 30);
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Snow(Transform parent, string name, Material mat, bool collide, Vector2 sizes, float alpha,
                                 float rate, Vector2 fall, string layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 26f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizes.x, sizes.y);
            main.startColor = new Color(1f, 1f, 1f, alpha);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 800;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(48f, 0.5f, 0.1f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.1f);
            vel.y = new ParticleSystem.MinMaxCurve(fall.x, fall.y);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.3f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.04f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            if (collide)
            {
                var col = ps.collision;
                col.enabled = true;
                col.type = ParticleSystemCollisionType.World;
                col.mode = ParticleSystemCollisionMode.Collision2D;
                col.collidesWith = LayerMask.GetMask("Ground");
                col.dampen = 1f;
                col.bounce = 0f;
                col.lifetimeLoss = 0.8f; // al posarse se queda poco y se desvanece
                col.radiusScale = 0.5f;
                col.quality = ParticleSystemCollisionQuality.High;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingLayerName = layer;
            r.sortingOrder = order;
        }

        public static VolumeProfile EnsurePostProfile()
        {
            string path = IceFolder + "/PostHielo.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.65f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);
            foreach (var c in profile.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ------------------------------------------------------------------ escenas de prueba

        [MenuItem("Tools/RedMagic/Niveles/Crear escenas de prueba de hielo (1000, 2000, 3000 px)")]
        public static void CreateIceTestScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var biome = EnsureIceBiome();
            var tile = EnsureTileAndPalette();
            var post = EnsurePostProfile();
            foreach (int rows in new[] { 25, 50, 75 })
                CreateTestScene(rows, biome, tile, post);
        }

        private static void CreateTestScene(int rows, LevelBiome biome, TileBase tile, VolumeProfile post)
        {
            string path = $"{TestScenesFolder}/NivelHielo_{rows * 40}.unity";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(TemplateScene, path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            // Fuera el nivel viejo de la plantilla: se queda la cámara y el jugador.
            foreach (var go in scene.GetRootGameObjects())
                if (go.name != "Main Camera" && go.name != "Player") Object.DestroyImmediate(go);

            var levelGo = new GameObject($"Nivel {rows * 40}px");
            var level = levelGo.AddComponent<Level>();
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(levelGo.transform, false);
            var terrainGo = new GameObject("Terreno", typeof(Tilemap), typeof(TilemapRenderer));
            terrainGo.transform.SetParent(grid.transform, false);
            terrainGo.GetComponent<TilemapRenderer>().enabled = false; // lo que se ve lo dibuja el Nivel
            var map = terrainGo.GetComponent<Tilemap>();
            level.SetSize(85, rows);
            level.SetBiome(biome);
            level.SetTerrain(map);
            PaintTestLayout(map, tile, 85, rows);

            if (biome.background != null)
            {
                var bg = (GameObject)PrefabUtility.InstantiatePrefab(biome.background, levelGo.transform);
                bg.name = "Fondo";
            }
            if (biome.particles != null) PrefabUtility.InstantiatePrefab(biome.particles, levelGo.transform);
            level.Rebuild();

            var light = new GameObject("Luz global").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            var volume = new GameObject("Postproceso").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = post;

            var cam = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 10f;
                cam.backgroundColor = new Color32(7, 20, 38, 255);
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }

            var player = GameObject.Find("Player");
            if (player != null) player.transform.position = new Vector3(4.5f, level.GroundTop(4) + 1f, 0f);

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[LevelSetup] Escena de prueba: {path}");
        }

        /// <summary>El nivel del boceto web: suelo y techo escalonados, 4 plataformas y, si es alto, una escalera de plataformas.</summary>
        private static void PaintTestLayout(Tilemap map, TileBase tile, int cols, int rows)
        {
            map.ClearAllTiles();
            int[] floorH = Profile(cols, new[] { (0, 1), (10, 2), (13, 3), (18, 3), (20, 2), (24, 4), (29, 5), (34, 3), (40, 3), (42, 2), (47, 6), (55, 7), (60, 4), (66, 2), (73, 3), (75, 1) });
            int[] ceilH = Profile(cols, new[] { (0, 1), (11, 2), (12, 4), (14, 6), (16, 4), (18, 2), (20, 1), (31, 3), (35, 1), (50, 2), (53, 1), (61, 3), (66, 1), (70, 1) });
            var solid = new bool[cols, rows];
            for (int x = 1; x < cols - 1; x++)
            {
                for (int y = 1; y < floorH[x]; y++) solid[x, y] = true;
                for (int y = rows - ceilH[x]; y < rows - 1; y++) solid[x, y] = true;
            }
            // Plataformas: [columna, filas desde el suelo, ancho, alto]
            foreach (var (c, fromBottom, w, h) in new[] { (22, 12, 5, 1), (38, 16, 5, 2), (56, 13, 4, 1), (64, 18, 6, 1) })
                Block(solid, c, fromBottom - h, w, h);

            if (rows >= 40)
            {
                var rng = new System.Random(rows * 7);
                for (int fromBottom = 21; fromBottom < rows - 5; fromBottom += 4)
                {
                    int placed = 0;
                    for (int tries = 0; tries < 40 && placed < 4; tries++)
                    {
                        int w = 3 + rng.Next(4), c = 11 + rng.Next(cols - 26), y = fromBottom + rng.Next(2);
                        if (!Free(solid, c, y, w, 1, cols, rows)) continue;
                        Block(solid, c, y, w, 1);
                        placed++;
                    }
                }
            }

            var positions = new List<Vector3Int>();
            for (int x = 1; x < cols - 1; x++)
            for (int y = 1; y < rows - 1; y++)
                if (solid[x, y]) positions.Add(new Vector3Int(x, y, 0));
            var tiles = new TileBase[positions.Count];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
            map.SetTiles(positions.ToArray(), tiles);
        }

        private static int[] Profile(int n, (int from, int value)[] list)
        {
            var a = new int[n];
            for (int c = 0; c < n; c++) foreach (var (from, value) in list) if (c >= from) a[c] = value;
            return a;
        }

        private static void Block(bool[,] solid, int x0, int y0, int w, int h)
        {
            for (int x = x0; x < x0 + w; x++) for (int y = y0; y < y0 + h; y++) solid[x, y] = true;
        }

        private static bool Free(bool[,] solid, int x0, int y0, int w, int h, int cols, int rows)
        {
            for (int x = x0 - 2; x <= x0 + w + 1; x++)
            for (int y = y0 - 2; y <= y0 + h + 1; y++)
            {
                if (x <= 0 || y <= 0 || x >= cols - 1 || y >= rows - 1) return false;
                if (solid[x, y]) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ utilidades de textura

        private static Texture2D ImportTexture(string path, bool repeat, int maxSize)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxSize;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D SaveTexture(string path, int w, int h, Func<int, int, Color> pixel)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) tex.SetPixel(x, y, pixel(x, y));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return ImportTexture(path, repeat: false, maxSize: 64);
        }

        private static Sprite SaveSprite(string path, int w, int h, Func<int, int, Color> pixel, int ppu)
        {
            SaveTexture(path, w, h, pixel);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
