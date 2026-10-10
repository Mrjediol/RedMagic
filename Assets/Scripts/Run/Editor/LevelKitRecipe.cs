using System.IO;
using UnityEditor;
using UnityEngine;

namespace RedMagic.RunEditor
{
    /// <summary>
    /// Receta para sacar un <see cref="LevelKit"/> de un mapa ya hecho (los que da Gemini): cuatro
    /// recortes del propio mapa —relleno, suelo, techo, pared— que se giran, se hacen repetibles y
    /// se guardan como arte del kit. Se rellena una vez por mundo y se puede regenerar cuando se
    /// quiera; las coordenadas son píxeles del mapa original con Y contada desde ARRIBA.
    ///
    /// Para cada tira hay que decir en qué lado del recorte está el aire: la herramienta la gira
    /// para dejar el aire arriba, que es como espera las tiras el importador de niveles.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelKitRecipe", menuName = "RedMagic/Level Kit Recipe")]
    public class LevelKitRecipe : ScriptableObject
    {
        public enum AirSide { Up, Down, Left, Right }

        [System.Serializable]
        public class Strip
        {
            [Tooltip("Recorte del mapa en píxeles (x, y desde ARRIBA, ancho, alto).")]
            public RectInt rect;
            [Tooltip("En qué lado del recorte está el aire (hacia dónde 'mira' el borde).")]
            public AirSide air = AirSide.Up;
        }

        [Tooltip("Imagen del mapa de la que se recorta todo.")]
        public Texture2D map;

        [Tooltip("Carpeta de salida (el kit y sus PNG).")]
        public string outputFolder = "Assets/Levels/_Kits/New";

        [Header("Relleno (interior de la roca)")]
        [Tooltip("Recorte del mapa; se hace repetible en horizontal y vertical.")]
        public RectInt fill;
        [Min(0.5f)] public float fillTileUnits = 4f;

        [Header("Tiras de borde")]
        public Strip floor = new Strip { air = AirSide.Up };
        public Strip ceiling = new Strip { air = AirSide.Down };
        public Strip wall = new Strip { air = AirSide.Right };

        [Header("Ajustes")]
        [Min(0), Tooltip("Píxeles que se mezclan en la junta para que la tira encaje consigo misma.")]
        public int blendPixels = 100;
        [Min(1), Tooltip("Píxeles de la tira por unidad de mundo. El grosor del borde = alto / este valor.")]
        public int stripPixelsPerUnit = 240;
        [Min(0f)] public float edgeOverhang = 0.1f;

        // ------------------------------------------------------------------ generación

        public LevelKit Generate()
        {
            if (map == null) { Debug.LogError("[LevelKitRecipe] Falta el mapa.", this); return null; }

            string mapPath = AssetDatabase.GetAssetPath(map);
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(mapPath));

            Directory.CreateDirectory(outputFolder);

            var fillTex = Pixels.Crop(src, fill);
            fillTex = Pixels.TileAxis(fillTex, blendPixels, horizontal: true);
            fillTex = Pixels.TileAxis(fillTex, blendPixels, horizontal: false);

            var floorSprite = SaveStrip("floor", floor);
            var ceilingSprite = SaveStrip("ceiling", ceiling);
            var wallSprite = SaveStrip("wall", wall);
            var fillAsset = SaveTexture($"{outputFolder}/fill.png", fillTex, sprite: false);

            Object.DestroyImmediate(src);

            string kitPath = $"{outputFolder}/{Path.GetFileName(outputFolder)}Kit.asset";
            var kit = AssetDatabase.LoadAssetAtPath<LevelKit>(kitPath);
            if (kit == null)
            {
                kit = CreateInstance<LevelKit>();
                AssetDatabase.CreateAsset(kit, kitPath);
            }
            kit.fill = (Texture2D)fillAsset;
            kit.fillTileUnits = fillTileUnits;
            kit.floorEdge = (Sprite)floorSprite;
            kit.ceilingEdge = (Sprite)ceilingSprite;
            kit.wallEdge = (Sprite)wallSprite;
            kit.edgeOverhang = edgeOverhang;
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelKitRecipe] Kit generado → {kitPath}", kit);
            return kit;
        }

        private Object SaveStrip(string name, Strip strip)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(map)));
            var tex = Pixels.Crop(src, strip.rect);
            Object.DestroyImmediate(src);

            tex = Pixels.AirToTop(tex, strip.air);
            tex = Pixels.TileAxis(tex, blendPixels, horizontal: true);
            return SaveTexture($"{outputFolder}/{name}.png", tex, sprite: true);
        }

        private Object SaveTexture(string path, Texture2D tex, bool sprite)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (sprite)
            {
                importer.spritePixelsPerUnit = stripPixelsPerUnit;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return sprite ? (Object)AssetDatabase.LoadAssetAtPath<Sprite>(path)
                          : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Operaciones de píxeles. Y en Unity crece hacia ARRIBA; las rects de la receta, hacia abajo.</summary>
        private static class Pixels
        {
            public static Texture2D Crop(Texture2D src, RectInt r)
            {
                int y = src.height - r.y - r.height;
                var t = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
                t.SetPixels(src.GetPixels(r.x, y, r.width, r.height));
                t.Apply();
                return t;
            }

            /// <summary>Gira el recorte para que el lado del aire quede ARRIBA.</summary>
            public static Texture2D AirToTop(Texture2D s, AirSide air)
            {
                int w = s.width, h = s.height;
                switch (air)
                {
                    case AirSide.Up: return s;
                    case AirSide.Down: // volteo vertical
                    {
                        var o = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++) o.SetPixel(x, h - 1 - y, s.GetPixel(x, y));
                        o.Apply(); return o;
                    }
                    case AirSide.Right: // el aire (derecha) pasa a arriba: 90° antihorario
                    {
                        var o = new Texture2D(h, w, TextureFormat.RGBA32, false);
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++) o.SetPixel(h - 1 - y, x, s.GetPixel(x, y));
                        o.Apply(); return o;
                    }
                    default: // Left: 90° horario
                    {
                        var o = new Texture2D(h, w, TextureFormat.RGBA32, false);
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++) o.SetPixel(y, w - 1 - x, s.GetPixel(x, y));
                        o.Apply(); return o;
                    }
                }
            }

            /// <summary>
            /// Hace la imagen repetible en un eje fundiendo los últimos <paramref name="n"/> píxeles
            /// con los primeros. La imagen resultante es n píxeles más corta en ese eje.
            /// </summary>
            public static Texture2D TileAxis(Texture2D s, int n, bool horizontal)
            {
                int w = s.width, h = s.height;
                int len = horizontal ? w : h;
                n = Mathf.Clamp(n, 0, len / 2);
                if (n == 0) return s;

                int nw = horizontal ? w - n : w, nh = horizontal ? h : h - n;
                var o = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
                for (int y = 0; y < nh; y++)
                    for (int x = 0; x < nw; x++)
                    {
                        Color c = s.GetPixel(x, y);
                        int pos = horizontal ? x : y;
                        if (pos < n)
                        {
                            float t = (float)pos / n;
                            Color tail = horizontal ? s.GetPixel(x + nw, y) : s.GetPixel(x, y + nh);
                            c = Color.Lerp(tail, c, t);
                        }
                        o.SetPixel(x, y, c);
                    }
                o.Apply();
                return o;
            }
        }
    }

    [CustomEditor(typeof(LevelKitRecipe))]
    public class LevelKitRecipeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Generar kit", GUILayout.Height(30)))
            {
                var kit = ((LevelKitRecipe)target).Generate();
                if (kit != null) Selection.activeObject = kit;
            }
        }
    }
}
