using System;
using System.Collections.Generic;
using System.IO;
using RedMagic.Levels;
using UnityEditor;
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
        private const string PlatformTilePath = BiomesRoot + "/Plataforma.asset";
        private const string PlatformSpritePath = BiomesRoot + "/plataforma_tile.png";
        private const string PaletteName = "PaletaTerreno";
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
            biome.particles = EnsureSnow(false);
            biome.outerGlowWidth = 0f; // el usuario lo quitó: sólo bandas, contorno y brillo interior
            EditorUtility.SetDirty(biome);
            AssetDatabase.SaveAssets();
            return biome;
        }

        [MenuItem("Tools/RedMagic/Niveles/Crear tile y paleta de terreno")]
        public static TileBase EnsureTileAndPalette() { EnsureTiles(out var rock, out _); return rock; }

        /// <summary>Tile de roca o de plataforma y la paleta (los crea si faltan).</summary>
        public static TileBase LoadTile(bool platform, out GameObject palette)
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(platform ? PlatformTilePath : TilePath);
            palette = AssetDatabase.LoadAssetAtPath<GameObject>($"{BiomesRoot}/{PaletteName}.prefab");
            if (tile != null && palette != null) return tile;
            EnsureTiles(out var rock, out var plat);
            palette = AssetDatabase.LoadAssetAtPath<GameObject>($"{BiomesRoot}/{PaletteName}.prefab");
            return platform ? plat : rock;
        }

        /// <summary>Tiles "Roca" (terreno macizo) y "Plataforma" (se atraviesa desde abajo) y la paleta con los dos.</summary>
        public static void EnsureTiles(out TileBase rock, out TileBase platform)
        {
            Directory.CreateDirectory(BiomesRoot);
            rock = EnsureTile(TilePath, TileSpritePath, new Color(0.35f, 0.62f, 0.85f), new Color(0.1f, 0.25f, 0.45f));
            platform = EnsureTile(PlatformTilePath, PlatformSpritePath, new Color(0.95f, 0.72f, 0.3f), new Color(0.5f, 0.3f, 0.08f));

            string palettePath = $"{BiomesRoot}/{PaletteName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(palettePath) == null)
            {
                var created = GridPaletteUtility.CreateNewPalette(BiomesRoot, PaletteName, GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
                if (created != null) palettePath = AssetDatabase.GetAssetPath(created);
            }
            var contents = PrefabUtility.LoadPrefabContents(palettePath);
            var map = contents.GetComponentInChildren<Tilemap>();
            if (map != null)
            {
                map.SetTile(new Vector3Int(0, 0, 0), rock);
                map.SetTile(new Vector3Int(1, 0, 0), platform);
            }
            PrefabUtility.SaveAsPrefabAsset(contents, palettePath);
            PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.SaveAssets();
        }

        private static TileBase EnsureTile(string tilePath, string spritePath, Color fill, Color border)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile != null) return tile;
            var sprite = SaveSprite(spritePath, 32, 32, (x, y) => x == 0 || y == 0 || x == 31 || y == 31 ? border : fill, 32);
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, tilePath);
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
        private static void RebuildSnowMenu() => EnsureSnow(true);

        /// <summary>
        /// Nieve barata: sistemas de partículas nativos (simulados en C++ por Unity, sin scripts por copo), con tope de
        /// partículas, colisión 2D sólo contra estáticos (Ground) y calidad media. El plano del juego colisiona: el copo
        /// muere al tocar y suelta (sub-emisor) un copo quieto que se queda 2-3,5 s y se desvanece. El de fondo no
        /// colisiona. <paramref name="rebuild"/> reescribe el prefab manteniendo su GUID (las escenas lo siguen usando).
        /// </summary>
        public static GameObject EnsureSnow(bool rebuild)
        {
            string prefabPath = IceFolder + "/Nieve.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null && !rebuild) return existing;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(IceFolder + "/Copo.mat");
            if (mat == null)
            {
                var flake = SaveTexture(IceFolder + "/copo.png", 32, 32, (x, y) =>
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16)) / 16f;
                    float a = Mathf.Clamp01(1f - d);
                    return new Color(0.95f, 0.98f, 1f, a * a);
                });
                mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")) { mainTexture = flake };
                AssetDatabase.CreateAsset(mat, IceFolder + "/Copo.mat");
            }

            var root = new GameObject("Nieve");
            try
            {
                root.AddComponent<FollowCameraView>();
                var game = Snow(root.transform, "Nieve plano", mat, sizes: new Vector2(0.07f, 0.24f), alpha: 0.9f,
                                rate: 14f, maxParticles: 300, layer: LevelSorting.FrontLayer, order: LevelSorting.GameplaySnow);
                AddLanding(game, mat);
                Snow(root.transform, "Nieve fondo", mat, sizes: new Vector2(0.04f, 0.12f), alpha: 0.45f,
                     rate: 14f, maxParticles: 260, layer: LevelSorting.Layer, order: LevelSorting.BackgroundSnow);
                root.AddComponent<SnowSettings>().Apply(); // valores de partida y sitio único para tocarlos
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static ParticleSystem Snow(Transform parent, string name, Material mat, Vector2 sizes, float alpha,
                                           float rate, int maxParticles, string layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(13f, 18f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizes.x, sizes.y);
            main.startColor = new Color(1f, 1f, 1f, alpha);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 0.5f, 0.1f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.1f);
            vel.y = new ParticleSystem.MinMaxCurve(-2f, -0.9f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(0.04f, 0.85f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingLayerName = layer;
            r.sortingOrder = order;
            return ps;
        }

        private static void AddLanding(ParticleSystem ps, Material mat)
        {
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision2D;
            col.collidesWith = LayerMask.GetMask("Ground", "Platform");
            col.enableDynamicColliders = false;     // sólo el terreno, que no se mueve: más barato
            col.quality = ParticleSystemCollisionQuality.Medium;
            col.maxCollisionShapes = 64;
            col.lifetimeLoss = 1f;                   // el que cae muere al tocar...
            col.radiusScale = 0.4f;

            // ...y en su sitio aparece uno quieto que se queda un momento y se desvanece.
            var go = new GameObject("Copo posado");
            go.transform.SetParent(ps.transform, false);
            var landed = go.AddComponent<ParticleSystem>();
            var main = landed.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;
            var emission = landed.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = landed.shape;
            shape.enabled = false;
            var fade = landed.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(0f, 0.35f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            var parentRenderer = ps.GetComponent<ParticleSystemRenderer>();
            r.sortingLayerName = parentRenderer.sortingLayerName;
            r.sortingOrder = parentRenderer.sortingOrder;

            var sub = ps.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(landed, ParticleSystemSubEmitterType.Collision,
                ParticleSystemSubEmitterProperties.InheritColor | ParticleSystemSubEmitterProperties.InheritSize);
        }

        /// <summary>Alfa 0 → 1 hasta <paramref name="fadeIn"/>, 1 hasta <paramref name="fadeOutFrom"/>, → 0 al final.</summary>
        private static Gradient Fade(float fadeIn, float fadeOutFrom)
        {
            var g = new Gradient();
            var alpha = fadeIn > 0f
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, fadeOutFrom), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeOutFrom), new GradientAlphaKey(0f, 1f) };
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
            return g;
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
