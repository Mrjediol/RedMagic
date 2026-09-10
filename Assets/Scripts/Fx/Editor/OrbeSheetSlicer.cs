using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace RedMagic.FxTools
{
    /// <summary>
    /// Herramienta de un solo uso: parte la lámina de referencia <c>orbe.png</c> (las tres
    /// animaciones del orbe del Árbol Ancestral dibujadas en una sola imagen, con rótulos, sprite
    /// de referencia y paleta alrededor) en tres hojas limpias, con fondo transparente, celdas
    /// uniformes y los frames centrados en el orbe.
    ///
    /// Se deja en el repo porque la lámina original se queda como fuente: si mañana llega una
    /// versión retocada del PNG, se relanza y salen las hojas otra vez.
    /// </summary>
    public static class OrbeSheetSlicer
    {
        private const string Folder = "Assets/Prefab/Fx/Bosses/ArbolAncestral";
        private const string Sheet = Folder + "/orbe.png";
        private const string OutFolder = Folder + "/Orbe";

        /// <summary>Las tres tiras dentro de la lámina, en coordenadas de Unity (y=0 abajo).</summary>
        /// <summary>
        /// Las tres tiras. <c>y0..y1</c> es la extensión del contenido y <c>safe0..safe1</c> hasta
        /// dónde se puede recortar sin morder el rótulo de la tira de abajo.
        /// </summary>
        private static readonly (string name, int y0, int y1, int safe0, int safe1, bool seedSplit)[] Bands =
        {
            ("Idle",   747, 908, 738, 924, false),
            ("Move",   512, 682, 496, 698, true),
            ("Impact", 164, 447, 157, 463, false),
        };

        /// <summary>Todo lo que hay a partir de aquí es paleta y sprite de referencia, no frames.</summary>
        private const int ContentRight = 1310;

        /// <summary>Margen que se deja alrededor del recorte para no cortar el halo tenue.</summary>
        private const int Margin = 16;

        /// <summary>Color del fondo de la lámina, leído de una esquina.</summary>
        private static Color32 _bg = new Color32(11, 15, 16, 255);

        /// <summary>Residuo sobre el fondo a partir del cual un pixel es totalmente opaco.</summary>
        private const float OpaqueAt = 64f;

        /// <summary>Píxeles por unidad de las tres hojas. Deja la bola en ~0.7 unidades de mundo.</summary>
        private const int PixelsPerUnit = 100;

        // ================================================================= máscara

        /// <summary>Contenido = pixel claro y verdoso (descarta el fondo y los rótulos grises).</summary>
        private static bool IsContent(Color32 c)
        {
            float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
            return (0.299f * r + 0.587f * g + 0.114f * b) > 0.14f && (g - b) > 0.05f;
        }

        /// <summary>Núcleo = pixel muy brillante. Sólo se usa para localizar el orbe en la tira Move.</summary>
        private static bool IsCore(Color32 c)
        {
            float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
            return (0.299f * r + 0.587f * g + 0.114f * b) > 0.62f;
        }

        /// <summary>
        /// Saca el pixel del fondo negro de la lámina. El orbe está pintado como un brillo sobre
        /// negro, así que lo que sobra del fondo <b>es</b> la opacidad: el halo se desvanece solo
        /// en vez de quedar recortado con un borde duro.
        ///
        /// Además se le devuelve el color: un pixel muy transparente se sube a brillo pleno (si no,
        /// el halo verde oscuro se ve como una mancha gris sobre fondos claros), y uno opaco se
        /// deja tal cual (las lianas marrones del impacto no deben volverse naranjas). Se mezcla
        /// entre los dos según el propio alfa, que es justo la frontera entre "brillo" y "arte".
        /// </summary>
        private static Color32 Extract(Color32 c)
        {
            int rr = Mathf.Max(0, c.r - _bg.r);
            int gg = Mathf.Max(0, c.g - _bg.g);
            int bb = Mathf.Max(0, c.b - _bg.b);
            int m = Mathf.Max(rr, Mathf.Max(gg, bb));
            if (m <= 2) return new Color32(0, 0, 0, 0);

            // Gris neutro y apagado = rótulo de la lámina, no arte. Nunca es parte de un orbe.
            if (m < 90 && Mathf.Abs(rr - gg) < 8 && Mathf.Abs(gg - bb) < 8) return new Color32(0, 0, 0, 0);

            float a = Mathf.Clamp01(m / OpaqueAt);
            float k = 255f / m;   // el residuo llevado a brillo pleno

            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(rr * k, c.r, a)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(gg * k, c.g, a)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(bb * k, c.b, a)),
                (byte)Mathf.RoundToInt(a * 255f));
        }

        // ================================================================= segmentación

        private struct Frame
        {
            public int x0, x1, y0, y1;   // bbox del contenido
            public int sx0, sx1;         // territorio del frame: hasta aquí se puede recortar
            public int ax, ay;           // ancla (centro del orbe)
        }

        private static Texture2D LoadSheet()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), Sheet)));
            return tex;
        }

        /// <summary>Tramos contiguos de columnas que cumplen <paramref name="on"/>, con hueco mínimo.</summary>
        private static List<(int a, int b)> Runs(bool[] on, int minGap)
        {
            var runs = new List<(int, int)>();
            int start = -1, lastOn = -1;
            for (int x = 0; x < on.Length; x++)
            {
                if (!on[x]) continue;
                if (start < 0) { start = x; }
                else if (x - lastOn - 1 >= minGap) { runs.Add((start, lastOn)); start = x; }
                lastOn = x;
            }
            if (start >= 0) runs.Add((start, lastOn));
            return runs;
        }

        private static List<Frame> Segment(Color32[] px, int w, int y0, int y1, bool seedSplit,
                                           StringBuilder log)
        {
            // Perfil de columnas dentro de la banda.
            var content = new bool[ContentRight];
            var core = new bool[ContentRight];
            for (int x = 0; x < ContentRight; x++)
            for (int y = y0; y <= y1; y++)
            {
                var c = px[y * w + x];
                if (IsContent(c)) content[x] = true;
                if (IsCore(c)) core[x] = true;
            }

            List<(int a, int b)> spans;

            if (!seedSplit)
            {
                // Tiras cuyos frames se separan solos: basta con partir por huecos anchos.
                spans = Runs(content, minGap: 26);
            }
            else
            {
                // Tira Move: las estelas de un frame casi tocan el siguiente, así que los huecos no
                // sirven. Se localiza el cuerpo del orbe (tramo ancho de núcleo) y el corte se pone
                // en el hueco más ancho que haya entre dos cuerpos consecutivos.
                var seeds = Runs(core, minGap: 6).FindAll(r => r.b - r.a + 1 >= 40);
                var content2 = Runs(content, minGap: 26);
                int left = content2.Count > 0 ? content2[0].a : 0;
                int right = content2.Count > 0 ? content2[content2.Count - 1].b : ContentRight - 1;

                spans = new List<(int, int)>();
                for (int i = 0; i < seeds.Count; i++)
                {
                    int a = i == 0 ? left : Cut(content, seeds[i - 1].b, seeds[i].a);
                    int b = i == seeds.Count - 1 ? right : Cut(content, seeds[i].b, seeds[i + 1].a) - 1;
                    spans.Add((a, b));
                }
            }

            var frames = new List<Frame>();
            for (int s = 0; s < spans.Count; s++)
            {
                var (a, b) = spans[s];

                // Territorio: hasta el punto medio con el vecino. Recortar más allá metería en la
                // celda las hojas del frame de al lado, que en la animación se ven como parpadeos.
                int t0 = s == 0 ? a - Margin : (spans[s - 1].b + a) / 2;
                int t1 = s == spans.Count - 1 ? b + Margin : (b + spans[s + 1].a) / 2;

                var f = new Frame
                {
                    x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue,
                    sx0 = t0, sx1 = t1,
                };
                for (int x = a; x <= b; x++)
                for (int y = y0; y <= y1; y++)
                {
                    if (!IsContent(px[y * w + x])) continue;
                    if (x < f.x0) f.x0 = x;
                    if (x > f.x1) f.x1 = x;
                    if (y < f.y0) f.y0 = y;
                    if (y > f.y1) f.y1 = y;
                }
                if (f.x1 < f.x0) continue;

                if (seedSplit)
                {
                    // El orbe es el disco brillante del frame; la estela también brilla pero es
                    // fina, así que la columna con MÁS pixeles de núcleo (suavizada, para que una
                    // chispa suelta no gane) cae siempre dentro del orbe. Anclar ahí es lo que
                    // impide que la bola tiemble de frame a frame al animar.
                    int span = f.x1 - f.x0 + 1;
                    var count = new int[span];
                    for (int x = f.x0; x <= f.x1; x++)
                    for (int y = y0; y <= y1; y++)
                        if (IsCore(px[y * w + x])) count[x - f.x0]++;

                    int best = 0, bestScore = -1;
                    for (int i = 0; i < span; i++)
                    {
                        int score = 0;
                        for (int k = -3; k <= 3; k++)
                        {
                            int j = i + k;
                            if (j >= 0 && j < span) score += count[j];
                        }
                        if (score > bestScore) { bestScore = score; best = i; }
                    }
                    f.ax = f.x0 + best;

                    // Centro vertical del núcleo en esa columna, no del frame entero: la estela
                    // ondula y arrastraría el ancla fuera del orbe.
                    int lo = int.MaxValue, hi = int.MinValue;
                    for (int y = y0; y <= y1; y++)
                    {
                        if (!IsCore(px[y * w + f.ax])) continue;
                        if (y < lo) lo = y;
                        if (y > hi) hi = y;
                    }
                    f.ay = hi >= lo ? (lo + hi) / 2 : (f.y0 + f.y1) / 2;
                }
                else
                {
                    f.ax = (f.x0 + f.x1) / 2;
                    f.ay = (f.y0 + f.y1) / 2;
                }

                frames.Add(f);
                log.AppendLine($"    x {f.x0}..{f.x1}  y {f.y0}..{f.y1}  ancla=({f.ax},{f.ay})");
            }

            return frames;
        }

        /// <summary>Centro del hueco vacío más ancho entre dos cuerpos; su punto medio si no hay hueco.</summary>
        private static int Cut(bool[] content, int from, int to)
        {
            int bestA = -1, bestB = -1, a = -1;
            for (int x = from; x <= to; x++)
            {
                if (!content[x]) { if (a < 0) a = x; }
                else if (a >= 0) { if (x - a > bestB - bestA) { bestA = a; bestB = x - 1; } a = -1; }
            }
            if (a >= 0 && to - a > bestB - bestA) { bestA = a; bestB = to; }
            return bestA < 0 ? (from + to) / 2 : (bestA + bestB) / 2;
        }

        // ================================================================= salida

        public static string Slice()
        {
            var log = new StringBuilder();
            var tex = LoadSheet();
            int w = tex.width;
            var px = tex.GetPixels32();
            _bg = px[5 * w + 5];   // esquina de la lámina = fondo puro

            EnsureFolder(OutFolder);

            foreach (var (name, y0, y1, safe0, safe1, seedSplit) in Bands)
            {
                log.AppendLine($"-- {name} --");
                var frames = Segment(px, w, y0, y1, seedSplit, log);

                // Celda uniforme, no cuadrada: cada lado crece lo que necesite el frame más
                // extremo. El ancla cae SIEMPRE en el mismo punto de la celda, y ese punto es el
                // pivote del sprite — así la animación no tiembla y el proyectil gira sobre la
                // bola, no sobre el centro de una caja llena de estela.
                int l = 0, r = 0, d = 0, u = 0;
                foreach (var f in frames)
                {
                    l = Mathf.Max(l, f.ax - f.x0);
                    r = Mathf.Max(r, f.x1 - f.ax);
                    d = Mathf.Max(d, f.ay - f.y0);
                    u = Mathf.Max(u, f.y1 - f.ay);
                }
                l += Margin; r += Margin; d += Margin; u += Margin;
                int cw = l + r, ch = d + u;

                var outTex = new Texture2D(cw * frames.Count, ch, TextureFormat.RGBA32, false);
                outTex.SetPixels32(new Color32[cw * frames.Count * ch]);

                for (int i = 0; i < frames.Count; i++)
                {
                    var f = frames[i];
                    for (int cy = 0; cy < ch; cy++)
                    for (int cx = 0; cx < cw; cx++)
                    {
                        int sx = f.ax - l + cx;
                        int sy = f.ay - d + cy;
                        if (sx < f.sx0 || sx > f.sx1) continue;          // territorio del frame
                        if (sy < safe0 || sy > safe1) continue;          // sin morder los rótulos
                        if (sx < 0 || sx >= w || sy < 0 || sy >= tex.height) continue;
                        outTex.SetPixel(i * cw + cx, cy, Extract(px[sy * w + sx]));
                    }
                }
                outTex.Apply();

                string path = $"{OutFolder}/Orbe_{name}.png";
                File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), outTex.EncodeToPNG());
                Object.DestroyImmediate(outTex);

                var pivot = new Vector2((float)l / cw, (float)d / ch);
                log.AppendLine($"   -> {path}  {frames.Count} frames, celda {cw}x{ch}, pivote {pivot}");

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ConfigureAndSlice(path, name, frames.Count, cw, ch, pivot);
            }

            Object.DestroyImmediate(tex);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString();
        }

        /// <summary>Importa como Sprite/Multiple y corta la rejilla con el data provider.</summary>
        private static void ConfigureAndSlice(string path, string band, int count, int cw, int ch,
                                              Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            // Mismo PPU en las tres hojas: la lámina original está dibujada a una sola escala, así
            // que el orbe mide igual en Idle y en Move sin retocar nada, y el impacto sale
            // naturalmente ~3× más grande que la bola. Ajustar el tamaño final es cosa del ataque.
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new System.Exception($"Sin data provider para {path}.");
            provider.InitSpriteEditorDataProvider();

            var editCapability = provider.GetDataProvider<ISpriteFrameEditCapability>();
            if (editCapability == null)
                throw new System.Exception("El importer no soporta edición de sprites. Abortado.");

            var capability = editCapability.GetEditCapability();
            if (!capability.HasCapability(EEditCapability.CreateAndDeleteSprite) ||
                !capability.HasCapability(EEditCapability.EditSpriteName) ||
                !capability.HasCapability(EEditCapability.EditSpriteRect) ||
                !capability.HasCapability(EEditCapability.EditPivot))
                throw new System.Exception("El importer no soporta cortar la rejilla. Abortado.");

            var rects = new SpriteRect[count];
            for (int i = 0; i < count; i++)
            {
                rects[i] = new SpriteRect
                {
                    name = $"Orbe_{band}_{i}",
                    spriteID = GUID.Generate(),
                    rect = new Rect(i * cw, 0, cw, ch),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    border = Vector4.zero,
                };
            }

            provider.SetSpriteRects(rects);

            var nameFileId = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileId != null)
            {
                var pairs = new List<SpriteNameFileIdPair>();
                foreach (var r in rects) pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
                nameFileId.SetNameFileIdPairs(pairs);
            }

            provider.Apply();
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        [MenuItem("Tools/RedMagic/FX/Orbe · Cortar hoja")]
        public static void SliceMenu() => Debug.Log(Slice());

        /// <summary>Vuelca una línea horizontal por el centro del primer orbe idle, para calibrar el alfa.</summary>
        public static string Scanline()
        {
            var tex = LoadSheet();
            int w = tex.width;
            var px = tex.GetPixels32();
            var sb = new StringBuilder();

            // Esquina de la lámina: el fondo puro.
            var bg = px[5 * w + 5];
            sb.AppendLine($"fondo esquina = {bg.r},{bg.g},{bg.b}");

            int y = 828;   // centro del primer orbe idle
            sb.AppendLine($"-- y={y}, x 20..175 --");
            for (int x = 20; x <= 175; x += 3)
            {
                var c = px[y * w + x];
                float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                int d = Mathf.Max(Mathf.Abs(c.r - bg.r), Mathf.Max(Mathf.Abs(c.g - bg.g), Mathf.Abs(c.b - bg.b)));
                sb.AppendLine($"  x={x,4} rgb={c.r,3},{c.g,3},{c.b,3}  lum={lum:F3} d={d,3}");
            }

            Object.DestroyImmediate(tex);
            return sb.ToString();
        }
    }
}
