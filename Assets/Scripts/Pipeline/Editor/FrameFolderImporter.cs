using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Importa una <b>carpeta de frames sueltos</b> (frame_000.png, frame_001.png…, lo que exporta la
    /// web por animación) como sprites listos para animar.
    ///
    /// La diferencia con <c>EnemyImporter.LoadAndConfigureSprites</c> es el <b>recorte</b>: los PNG
    /// que salen de la web suelen ser celdas enormes con el dibujo en una esquina (un aviso de
    /// 1024×2560 con la flecha arriba del todo). Aquí se mide la caja del contenido de TODOS los
    /// frames de la carpeta y se usa su unión como rect del sprite, con el pivote en esa caja
    /// (pies o centro). Así:
    ///  - el pivote cae en el dibujo, no en el centro de una celda vacía;
    ///  - el tamaño del sprite es el del dibujo (un collider o un "ajustar al ancho" miden bien);
    ///  - todos los frames comparten caja, así que la animación no baila.
    ///
    /// Idempotente: reimportar conserva el id de cada sprite, así que clips y prefabs que ya lo
    /// referencian no se rompen.
    /// </summary>
    public static class FrameFolderImporter
    {
        /// <summary>Alfa por debajo de la cual un píxel cuenta como fondo al medir el contenido.</summary>
        private const byte AlphaThreshold = 20;

        /// <summary>
        /// Importa los PNG de <paramref name="folder"/> (sólo esa carpeta, no subcarpetas) en orden
        /// de nombre y devuelve un sprite por frame. <paramref name="anchor"/>: BottomCenter para un
        /// cuerpo (pivote en los pies), Center para proyectiles, efectos y avisos.
        /// </summary>
        public static List<Sprite> Import(string folder, AnchorMode anchor, float pixelsPerUnit = 100f,
                                          int padding = 4, StringBuilder log = null, RectInt? sharedContent = null)
        {
            folder = (folder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            var sprites = new List<Sprite>();

            if (!AssetDatabase.IsValidFolder(folder))
            {
                log?.AppendLine($"  AVISO: no existe la carpeta de frames '{folder}'.");
                return sprites;
            }

            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetDirectoryName(p)?.Replace('\\', '/'), folder, StringComparison.Ordinal))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            if (paths.Count == 0)
            {
                log?.AppendLine($"  AVISO: '{folder}' no tiene PNG.");
                return sprites;
            }

            // --- 1) caja del contenido de cada frame, en píxeles con origen abajo-izquierda.
            var sizes = new Dictionary<string, Vector2Int>();
            bool any = false;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

            foreach (var path in paths)
            {
                if (!ReadContentBounds(path, out var size, out var bounds)) continue;
                sizes[path] = size;
                if (bounds.width <= 0 || bounds.height <= 0) continue;

                any = true;
                minX = Mathf.Min(minX, bounds.xMin);
                minY = Mathf.Min(minY, bounds.yMin);
                maxX = Mathf.Max(maxX, bounds.xMax);
                maxY = Mathf.Max(maxY, bounds.yMax);
            }

            // Caja común a varias carpetas (todas las animaciones de un cuerpo): mismo rect y mismo
            // pivote en todos los clips, así el personaje no salta de sitio al cambiar de animación.
            if (sharedContent.HasValue && sharedContent.Value.width > 0 && sharedContent.Value.height > 0)
            {
                any = true;
                minX = sharedContent.Value.xMin;
                minY = sharedContent.Value.yMin;
                maxX = sharedContent.Value.xMax;
                maxY = sharedContent.Value.yMax;
            }

            // Un frame vacío (todos transparentes) no debe hacer fallar la animación entera.
            if (!any)
            {
                minX = 0;
                minY = 0;
                maxX = sizes.Count > 0 ? sizes.Values.Max(s => s.x) : 1;
                maxY = sizes.Count > 0 ? sizes.Values.Max(s => s.y) : 1;
            }

            minX -= padding;
            minY -= padding;
            maxX += padding;
            maxY += padding;

            // El punto de anclaje, en píxeles de la celda: el mismo para todos los frames.
            float anchorX = (minX + maxX) * 0.5f;
            float anchorY = anchor == AnchorMode.BottomCenter ? minY + padding : (minY + maxY) * 0.5f;

            // --- 2) un sprite por PNG con esa caja (recortada a cada textura) y ese pivote.
            foreach (var path in paths)
            {
                if (!sizes.TryGetValue(path, out var size)) continue;

                int x0 = Mathf.Clamp(minX, 0, size.x - 1);
                int y0 = Mathf.Clamp(minY, 0, size.y - 1);
                int x1 = Mathf.Clamp(maxX, x0 + 1, size.x);
                int y1 = Mathf.Clamp(maxY, y0 + 1, size.y);
                var rect = new Rect(x0, y0, x1 - x0, y1 - y0);
                var pivot = new Vector2((anchorX - rect.x) / rect.width, (anchorY - rect.y) / rect.height);

                try
                {
                    ApplySingleRect(path, Path.GetFileNameWithoutExtension(path), rect, pivot, pixelsPerUnit);
                }
                catch (Exception e)
                {
                    log?.AppendLine($"  ERROR importando '{path}': {e.Message}");
                    continue;
                }

                var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
                if (sprite != null) sprites.Add(sprite);
                else log?.AppendLine($"  AVISO: '{path}' no ha dado sprite tras importarlo.");
            }

            return sprites;
        }

        /// <summary>
        /// Caja del contenido (sin margen) de todos los PNG de las carpetas dadas, en píxeles de celda
        /// con origen abajo-izquierda. Null si no hay nada. Para pasarla como <c>sharedContent</c>.
        /// </summary>
        public static RectInt? MeasureContent(IEnumerable<string> folders)
        {
            bool any = false;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

            foreach (var raw in folders)
            {
                string folder = (raw ?? string.Empty).Replace('\\', '/').TrimEnd('/');
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                             .Select(AssetDatabase.GUIDToAssetPath)
                             .Where(p => string.Equals(Path.GetDirectoryName(p)?.Replace('\\', '/'), folder, StringComparison.Ordinal)))
                {
                    if (!ReadContentBounds(path, out _, out var b) || b.width <= 0 || b.height <= 0) continue;
                    any = true;
                    minX = Mathf.Min(minX, b.xMin);
                    minY = Mathf.Min(minY, b.yMin);
                    maxX = Mathf.Max(maxX, b.xMax);
                    maxY = Mathf.Max(maxY, b.yMax);
                }
            }

            return any ? new RectInt(minX, minY, maxX - minX, maxY - minY) : (RectInt?)null;
        }

        /// <summary>
        /// Tamaño de la textura y caja de los píxeles con alfa. Se lee del PNG en disco, no de la
        /// textura importada: así no hace falta marcarla legible ni depende de su tamaño máximo.
        /// </summary>
        private static bool ReadContentBounds(string assetPath, out Vector2Int size, out RectInt bounds)
        {
            size = Vector2Int.zero;
            bounds = new RectInt();

            string absolute = Path.GetFullPath(assetPath);
            if (!File.Exists(absolute)) return false;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(absolute))) return false;

                int w = tex.width, h = tex.height;
                size = new Vector2Int(w, h);

                var pixels = tex.GetPixels32();
                int minX = w, minY = h, maxX = -1, maxY = -1;

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (pixels[row + x].a <= AlphaThreshold) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }

                bounds = maxX < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// Deja el PNG como sprite Multiple con un único rect. Multiple (y no Single) porque es la
        /// única forma de que el rect del sprite sea la caja del dibujo y no la textura entera.
        /// </summary>
        private static void ApplySingleRect(string path, string spriteName, Rect rect, Vector2 pivot, float ppu)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new Exception("no es una textura");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            // La web exporta celdas de hasta 2560 px: con el tope por defecto (2048) se reescalarían.
            if (importer.maxTextureSize < 4096) importer.maxTextureSize = 4096;
            importer.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new Exception("sin data provider de sprites");
            provider.InitSpriteEditorDataProvider();

            // Se conserva el id del sprite si ya existía: lo que ya apuntaba a él sigue apuntando.
            var existing = provider.GetSpriteRects();
            GUID id = existing != null && existing.Length > 0 ? existing[0].spriteID : GUID.Generate();
            if (existing != null)
                foreach (var r in existing)
                    if (r.name == spriteName) { id = r.spriteID; break; }

            var spriteRect = new SpriteRect
            {
                name = spriteName,
                spriteID = id,
                rect = rect,
                alignment = SpriteAlignment.Custom,
                pivot = pivot,
                border = Vector4.zero,
            };

            provider.SetSpriteRects(new[] { spriteRect });

            var nameFileId = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileId != null)
                nameFileId.SetNameFileIdPairs(new List<SpriteNameFileIdPair> { new SpriteNameFileIdPair(spriteName, id) });

            provider.Apply();
            importer.SaveAndReimport();
        }
    }
}
