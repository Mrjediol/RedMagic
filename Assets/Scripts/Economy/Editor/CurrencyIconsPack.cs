using System.IO;
using System.Text;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Economy.EditorTools
{
    /// <summary>
    /// <b>Tools ▸ RedMagic ▸ UI ▸ Iconos de moneda · Procesar e instalar</b>: convierte el arte de
    /// <c>Assets/Art/Currency/</c> (lienzos grandes con el icono en medio, ya con transparencia) en
    /// iconos de UI y los pone en <c>Resources/CurrencyConfig.asset</c>, que es de donde los lee
    /// todo (HUD, precios de la tienda…).
    ///
    /// Recorta cada imagen al contenido opaco (+ margen) — sin eso, el icono ocuparía un tercio de
    /// la casilla del HUD — y la guarda en <see cref="OutputFolder"/>; la fuente no se toca.
    /// Importación de arte pintado para UI: Sprite, bilinear, mipmaps, máx. 256 px, y los px/unidad
    /// que dan el mismo tamaño en mundo que los iconos viejos (1.6 u de lado mayor).
    /// Re-ejecutable: regenera los PNG y reasigna.
    /// </summary>
    public static class CurrencyIconsPack
    {
        private const string SourceFolder = "Assets/Art/Currency";
        public const string OutputFolder = "Assets/Art/UI/Currency";
        private const string ConfigPath = "Assets/Resources/CurrencyConfig.asset";
        private const int Margin = 8;
        private const int MaxSize = 256;
        private const float WorldSize = 1.6f; // lado mayor en unidades (los viejos: 16 px a 10 px/u)

        private static readonly (Currency currency, string source)[] Map =
        {
            (Currency.Gold, "Coin"),
            (Currency.Diamond, "Diamond"),
            (Currency.SoulFragment, "SoulFragmen"),
            (Currency.Skull, "Skull"),
        };

        [MenuItem("Tools/RedMagic/UI/Iconos de moneda · Procesar e instalar", priority = 430)]
        public static void Build() => Debug.Log(Run());

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run()
        {
            SheetSlicer.EnsureFolder(OutputFolder);
            var log = new StringBuilder("[CurrencyIconsPack]\n");
            var config = AssetDatabase.LoadAssetAtPath<CurrencyConfig>(ConfigPath);
            if (config == null) return log.Append("⚠ Falta ").Append(ConfigPath).ToString();

            var so = new SerializedObject(config);
            var visuals = so.FindProperty("visuals");

            foreach (var (currency, source) in Map)
            {
                string src = $"{SourceFolder}/{source}.png";
                string dst = $"{OutputFolder}/Currency_{currency}.png";
                if (!Trim(src, dst, out Vector2Int size, log)) continue;

                ConfigureImporter(dst, size);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(dst);

                for (int i = 0; i < visuals.arraySize; i++)
                {
                    var entry = visuals.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("currency").enumValueIndex != (int)currency) continue;
                    entry.FindPropertyRelative("icon").objectReferenceValue = sprite;
                }

                log.AppendLine($"{currency}: {src} → {dst} ({size.x}×{size.y})");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        /// <summary>Copia <paramref name="src"/> recortado a su contenido opaco (alfa &gt; 12) + margen.</summary>
        private static bool Trim(string src, string dst, out Vector2Int size, StringBuilder log)
        {
            size = default;
            var raw = SheetSlicer.LoadRaw(src);
            if (raw == null) { log.AppendLine($"⚠ No se pudo leer {src}"); return false; }

            int w = raw.width, h = raw.height;
            var px = raw.GetPixels32();
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a <= 12) continue;
                if (x < x0) x0 = x; if (x > x1) x1 = x;
                if (y < y0) y0 = y; if (y > y1) y1 = y;
            }

            if (x1 < 0) { Object.DestroyImmediate(raw); log.AppendLine($"⚠ {src} está vacío"); return false; }

            x0 = Mathf.Max(0, x0 - Margin); y0 = Mathf.Max(0, y0 - Margin);
            x1 = Mathf.Min(w - 1, x1 + Margin); y1 = Mathf.Min(h - 1, y1 + Margin);
            size = new Vector2Int(x1 - x0 + 1, y1 - y0 + 1);

            var outTex = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            outTex.SetPixels(raw.GetPixels(x0, y0, size.x, size.y));
            outTex.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), dst), outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
            Object.DestroyImmediate(raw);

            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            return true;
        }

        private static void ConfigureImporter(string path, Vector2Int size)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;          // se dibuja muy reducido (26 px en el HUD)
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.spritePixelsPerUnit = Mathf.Max(size.x, size.y) / WorldSize;
            importer.SaveAndReimport();
        }
    }
}
