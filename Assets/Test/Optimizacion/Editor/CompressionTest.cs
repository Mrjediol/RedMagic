using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace RedMagic.Test.Optimizacion
{
    /// <summary>
    /// Prueba aislada de compresión (tarea optimizacion-arte-carga, paso 1). Copia una capa de fondo y una pieza de enemigo a
    /// <c>Assets/Test/Optimizacion/Prueba/</c> en cuatro variantes — tal cual (RGBA32), rellena a múltiplo de 4 (ETC2), ASTC 6×6 y
    /// ASTC 4×4 sin cambiar el tamaño (todas sin mipmaps: con mips y tamaño no potencia de 2 Unity no comprime) — y escribe, por cada original, una imagen con un recorte ampliado de las cuatro una al lado
    /// de otra (<c>comparacion_*.png</c>) y <c>resultado.md</c> con la memoria de cada una. No toca los originales.
    /// </summary>
    public static class CompressionTest
    {
        private const string Dir = "Assets/Test/Optimizacion/Prueba";
        public static readonly string[] Sources =
        {
            "Assets/Levels/_Biomas/Hielo/Fondo/capa3.png",
            "Assets/Art/RigImports/BossCenter/Parts/Torso.png",
        };
        private static readonly (string tag, string label)[] Variants =
        {
            ("actual", "Actual"), ("x4_etc2", "Rellena a ×4 → ETC2, sin mips"), ("astc6", "ASTC 6×6, sin mips"), ("astc4", "ASTC 4×4, sin mips"),
        };

        [MenuItem("Tools/RedMagic/Optimización/Prueba de compresión · crear")]
        public static void CreateMenu() => Debug.Log(Create());

        [MenuItem("Tools/RedMagic/Optimización/Prueba de compresión · borrar")]
        public static void Delete() { AssetDatabase.DeleteAsset(Dir); Debug.Log("[CompressionTest] Prueba borrada."); }

        /// <summary>Paso 1: copia las variantes y las importa (rápido; la comparación va en <see cref="Compare"/>).</summary>
        public static string Create()
        {
            Directory.CreateDirectory(Dir);
            foreach (var src in Sources)
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(Path.GetDirectoryName(src))) + "_" + Path.GetFileNameWithoutExtension(src);
                byte[] png = File.ReadAllBytes(src);
                var srcImp = (TextureImporter)AssetImporter.GetAtPath(src);
                foreach (var (tag, _) in Variants)
                {
                    string dst = $"{Dir}/{name}__{tag}.png";
                    File.WriteAllBytes(dst, tag == "x4_etc2" ? PadToMultipleOf4(png) : png);
                }
                AssetDatabase.Refresh();
                foreach (var (tag, _) in Variants)
                {
                    string dst = $"{Dir}/{name}__{tag}.png";
                    var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
                    EditorUtility.CopySerialized(srcImp, imp);
                    var android = imp.GetPlatformTextureSettings("Android");
                    android.overridden = tag.StartsWith("astc");
                    android.maxTextureSize = srcImp.maxTextureSize;
                    android.format = tag == "astc6" ? TextureImporterFormat.ASTC_6x6 : tag == "astc4" ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.Automatic;
                    imp.SetPlatformTextureSettings(android);
                    if (tag != "actual") imp.mipmapEnabled = false;      // con mips y tamaño no potencia de 2 no se comprime
                    imp.SaveAndReimport();
                }
            }
            return "[CompressionTest] Variantes creadas en " + Dir;
        }

        /// <summary>Paso 2: imágenes comparativas + resultado.md.</summary>
        public static string Compare()
        {
            var md = new StringBuilder("# Prueba de compresión\n\nMismo recorte ampliado ×3 de cada variante, de izquierda a derecha: " +
                                       string.Join(" · ", System.Array.ConvertAll(Variants, v => v.label)) + ".\n\n");
            foreach (var src in Sources)
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(Path.GetDirectoryName(src))) + "_" + Path.GetFileNameWithoutExtension(src);
                md.AppendLine($"## {Path.GetFileName(src)} (`{src}`)\n\n| Variante | Tamaño importado | Formato | Memoria |\n|---|---:|---|---:|");
                var crops = new List<Texture2D>(); Vector2Int at = new Vector2Int(-1, -1); int w0 = 0, h0 = 0;
                foreach (var (tag, label) in Variants)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{name}__{tag}.png");
                    long bytes = 0;
                    for (int m = 0; m < Mathf.Max(1, tex.mipmapCount); m++) bytes += GraphicsFormatUtility.ComputeMipmapSize(Mathf.Max(1, tex.width >> m), Mathf.Max(1, tex.height >> m), tex.graphicsFormat);
                    md.AppendLine($"| {label} | {tex.width}×{tex.height} | {tex.format} | {bytes / 1048576f:0.00} MB |");
                    if (at.x < 0) { w0 = tex.width; h0 = tex.height; }
                    crops.Add(Crop(tex, 160, ref at, (tex.width - w0) / 2, (tex.height - h0) / 2));
                }
                string sheet = $"{Dir}/comparacion_{name}.png";
                File.WriteAllBytes(sheet, Sheet(crops, 3).EncodeToPNG());
                md.AppendLine($"\nImagen: `{sheet}`\n");
            }
            File.WriteAllText($"{Dir}/resultado.md", md.ToString());
            AssetDatabase.Refresh();
            return "[CompressionTest] " + Dir + "/resultado.md";
        }

        // Rellena con transparente hasta múltiplo de 4, repartiendo el relleno a ambos lados (el centro casi no se mueve).
        private static byte[] PadToMultipleOf4(byte[] png)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false); t.LoadImage(png);
            int w = (t.width + 3) / 4 * 4, h = (t.height + 3) / 4 * 4, ox = (w - t.width) / 2, oy = (h - t.height) / 2;
            if (w == t.width && h == t.height) return png;
            var o = new Texture2D(w, h, TextureFormat.RGBA32, false);
            o.SetPixels32(new Color32[w * h]);
            o.SetPixels32(ox, oy, t.width, t.height, t.GetPixels32());
            o.Apply();
            return o.EncodeToPNG();
        }

        // Recorte cuadrado (size px) en la zona con más detalle: la que más contraste tiene en una rejilla de muestras.
        private static Texture2D Crop(Texture2D tex, int size, ref Vector2Int at, int ox, int oy)
        {
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var full = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            full.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); full.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            int bx = 0, by = 0; float best = -1;
            if (at.x >= 0) { bx = at.x + ox; by = at.y + oy; best = 1; }
            else for (int y = 0; y + size <= tex.height; y += size / 2)
                for (int x = 0; x + size <= tex.width; x += size / 2)
                {
                    float sum = 0, a = 0;
                    for (int k = 0; k < 64; k++) { var c = full.GetPixel(x + (k * 37) % size, y + (k * 53) % size); var d = full.GetPixel(x + (k * 37 + 3) % size, y + (k * 53 + 3) % size); sum += Mathf.Abs(c.grayscale - d.grayscale) * c.a; a += c.a; }
                    float score = a > 32 ? sum : -1;
                    if (score > best) { best = score; bx = x; by = y; }
                }
            var crop = new Texture2D(size, size, TextureFormat.RGBA32, false);
            at = at.x >= 0 ? at : new Vector2Int(bx, by);
            crop.SetPixels(full.GetPixels(bx, by, Mathf.Min(size, tex.width), Mathf.Min(size, tex.height))); crop.Apply();
            return crop;
        }

        private static Texture2D Sheet(List<Texture2D> crops, int zoom)
        {
            int s = crops[0].width * zoom, gap = 6;
            var o = new Texture2D(crops.Count * (s + gap) - gap, s, TextureFormat.RGBA32, false);
            var bg = new Color32[o.width * o.height]; for (int i = 0; i < bg.Length; i++) bg[i] = new Color32(74, 74, 74, 255); o.SetPixels32(bg);
            for (int i = 0; i < crops.Count; i++)
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        var c = crops[i].GetPixel(x / zoom, y / zoom);
                        o.SetPixel(i * (s + gap) + x, y, Color.Lerp(new Color(0.29f, 0.29f, 0.29f), new Color(c.r, c.g, c.b), c.a));
                    }
            o.Apply();
            return o;
        }
    }
}
