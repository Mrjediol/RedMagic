using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RedMagic.RunEditor
{
    /// <summary>
    /// Corta una hoja de piezas sobre fondo magenta plano (las que genera Gemini: plataformas,
    /// decoración, puertas…) en un PNG por pieza con transparencia. Selecciona una o varias
    /// hojas en el Project y usa <b>Tools ▸ RedMagic ▸ Niveles ▸ Cortar hoja de piezas</b>:
    /// las piezas salen en una carpeta <c>&lt;hoja&gt;_Piezas</c> junto a la hoja, numeradas de
    /// arriba abajo y de izquierda a derecha.
    ///
    /// Cada pieza es un grupo de píxeles conectados; las que quedan muy juntas (el cristal que
    /// se separó de su plataforma por un hueco) se funden en una. Los bordes se limpian del tinte
    /// magenta que deja el JPEG.
    /// </summary>
    public static class SheetSlicer
    {
        private const float KeyLow = 0.45f, KeyHigh = 0.75f;   // distancia al magenta: 0 = transparente, 1 = opaco
        private const int MergeGap = 12;   // px: una mota a menos de esto de una pieza grande se le une
        private const int SpeckSide = 40;     // px: caja más pequeña que esto en ambos lados = mota                        // px: cajas más cerca que esto son una pieza
        private const int MinSide = 24;                        // px: más pequeño que esto se descarta como mota
        private const int Padding = 4;
        private const int PixelsPerUnit = 64;

        [MenuItem("Tools/RedMagic/Niveles/Cortar hoja de piezas")]
        private static void SliceSelection()
        {
            int total = 0;
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (obj is Texture2D && !string.IsNullOrEmpty(path)) total += Slice(path);
            }
            if (total == 0) Debug.LogWarning("[SheetSlicer] Selecciona una o varias hojas (imágenes) en el Project.");
        }

        [MenuItem("Tools/RedMagic/Niveles/Cortar hoja de piezas", true)]
        private static bool Validate() => Selection.activeObject is Texture2D;

        public static int Slice(string sheetPath)
        {
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheet.LoadImage(File.ReadAllBytes(sheetPath));
            int w = sheet.width, h = sheet.height;
            var px = sheet.GetPixels32();
            Object.DestroyImmediate(sheet);

            var alpha = new float[w * h];
            var solid = new bool[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                float r = px[i].r / 255f, g = px[i].g / 255f, b = px[i].b / 255f;
                float d = Mathf.Sqrt((r - 1f) * (r - 1f) + g * g + (b - 1f) * (b - 1f));
                alpha[i] = Mathf.Clamp01((d - KeyLow) / (KeyHigh - KeyLow));
                solid[i] = alpha[i] > 0.5f;
            }

            var boxes = FindBoxes(solid, w, h);
            boxes = MergeBoxes(boxes);
            boxes.RemoveAll(b => b.width < MinSide || b.height < MinSide);
            // Orden de lectura: por filas aproximadas, luego de izquierda a derecha.
            boxes.Sort((a, b) =>
            {
                int rowA = a.y / 120, rowB = b.y / 120;
                return rowA != rowB ? rowA.CompareTo(rowB) : a.x.CompareTo(b.x);
            });

            string folder = Path.Combine(Path.GetDirectoryName(sheetPath),
                Path.GetFileNameWithoutExtension(sheetPath) + "_Piezas").Replace('\\', '/');
            if (Directory.Exists(folder)) AssetDatabase.DeleteAsset(folder);   // un corte nuevo reemplaza al anterior
            Directory.CreateDirectory(folder);

            for (int n = 0; n < boxes.Count; n++)
            {
                var r = Expand(boxes[n], w, h);
                var tex = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
                var outPx = new Color32[r.width * r.height];
                for (int y = 0; y < r.height; y++)
                    for (int x = 0; x < r.width; x++)
                    {
                        int src = (r.y + y) * w + r.x + x;
                        var c = px[src];
                        // Quita el tinte magenta del borde: lo que sobra de rojo y azul por encima del verde.
                        int spill = Mathf.Max(0, Mathf.Min(c.r, c.b) - c.g);
                        float k = 1f - alpha[src];
                        int cut = Mathf.RoundToInt(spill * Mathf.Clamp01(k * 2f));
                        outPx[y * r.width + x] = new Color32(
                            (byte)Mathf.Max(0, c.r - cut), c.g, (byte)Mathf.Max(0, c.b - cut),
                            (byte)Mathf.RoundToInt(alpha[src] * 255f));
                    }
                tex.SetPixels32(outPx);
                tex.Apply();
                // La imagen se leyó con Y desde abajo: la caja ya está en ese sistema.
                string file = $"{folder}/{Path.GetFileNameWithoutExtension(sheetPath)}_{n + 1:00}.png";
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.Refresh();
            foreach (var f in Directory.GetFiles(folder, "*.png"))
            {
                string assetPath = f.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Debug.Log($"[SheetSlicer] {Path.GetFileName(sheetPath)} → {boxes.Count} piezas en {folder}");
            return boxes.Count;
        }

        private static RectInt Expand(RectInt r, int w, int h)
        {
            int x0 = Mathf.Max(0, r.x - Padding), y0 = Mathf.Max(0, r.y - Padding);
            int x1 = Mathf.Min(w, r.xMax + Padding), y1 = Mathf.Min(h, r.yMax + Padding);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>Cajas de los grupos de píxeles sólidos conectados (8 vecinos). Y crece hacia arriba, como en Unity.</summary>
        private static List<RectInt> FindBoxes(bool[] solid, int w, int h)
        {
            var seen = new bool[solid.Length];
            var boxes = new List<RectInt>();
            var stack = new Stack<int>();
            for (int start = 0; start < solid.Length; start++)
            {
                if (!solid[start] || seen[start]) continue;
                int minX = w, minY = h, maxX = 0, maxY = 0;
                stack.Push(start);
                seen[start] = true;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    int x = i % w, y = i / w;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int j = ny * w + nx;
                            if (!solid[j] || seen[j]) continue;
                            seen[j] = true;
                            stack.Push(j);
                        }
                }
                boxes.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
            }
            return boxes;
        }

        /// <summary>
        /// Absorbe las motas (cajas pequeñas) en la pieza grande que tengan al lado: el cristal que
        /// se separó de su plataforma por un hueco. Las piezas grandes NUNCA se funden entre sí,
        /// aunque sus cajas se toquen: dos plataformas en columna pueden solaparse en vertical.
        /// </summary>
        private static List<RectInt> MergeBoxes(List<RectInt> boxes)
        {
            for (int i = boxes.Count - 1; i >= 0; i--)
            {
                var small = boxes[i];
                if (small.width >= SpeckSide || small.height >= SpeckSide) continue;
                var grown = new RectInt(small.x - MergeGap, small.y - MergeGap, small.width + MergeGap * 2, small.height + MergeGap * 2);
                int host = -1;
                for (int j = 0; j < boxes.Count; j++)
                    if (j != i && (boxes[j].width >= SpeckSide || boxes[j].height >= SpeckSide) && grown.Overlaps(boxes[j])) { host = j; break; }
                if (host < 0) continue;
                int x0 = Mathf.Min(small.x, boxes[host].x), y0 = Mathf.Min(small.y, boxes[host].y);
                int x1 = Mathf.Max(small.xMax, boxes[host].xMax), y1 = Mathf.Max(small.yMax, boxes[host].yMax);
                boxes[host] = new RectInt(x0, y0, x1 - x0, y1 - y0);
                boxes.RemoveAt(i);
            }
            return boxes;
        }
    }
}
