using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Paso 1 del pipeline: lámina cruda → una hoja limpia por estado, ya cortada en sprites.
    ///
    /// No corta la lámina original en su sitio a propósito. Escribe hojas nuevas, y eso resuelve
    /// de una vez tres cosas que si no hay que arreglar a mano en cada importación: el fondo
    /// (damero pintado de un JPG → alfa real), los rótulos que el artista deja en la imagen, y
    /// las celdas desiguales (cada frame acaba en una celda del mismo tamaño, con el pivote
    /// siempre en el mismo punto del personaje, que es lo que impide que la animación tiemble).
    ///
    /// El resultado es determinista: misma lámina + misma receta = mismos sprites.
    /// </summary>
    public static class SheetSlicer
    {
        /// <summary>Zona útil de la lámina, en píxeles de textura (y=0 abajo).</summary>
        public struct CropRect
        {
            public int x0, x1, y0, y1;
            public int Width => x1 - x0 + 1;
            public int Height => y1 - y0 + 1;
        }

        /// <summary>
        /// Traduce el recorte de la receta (expresado como se ve la imagen: arriba es arriba) a
        /// coordenadas de textura, donde y=0 es la fila de abajo.
        /// </summary>
        private static CropRect ResolveCrop(SpriteSheetRecipe recipe, int w, int h)
        {
            var crop = new CropRect
            {
                x0 = Mathf.Clamp(recipe.cropLeft, 0, w - 1),
                x1 = Mathf.Clamp(w - 1 - recipe.cropRight, 0, w - 1),
                y0 = Mathf.Clamp(recipe.cropBottom, 0, h - 1),
                y1 = Mathf.Clamp(h - 1 - recipe.cropTop, 0, h - 1),
            };

            if (crop.x1 < crop.x0) { crop.x0 = 0; crop.x1 = w - 1; }
            if (crop.y1 < crop.y0) { crop.y0 = 0; crop.y1 = h - 1; }
            return crop;
        }

        private static bool IsFullFrame(CropRect crop, int w, int h) =>
            crop.x0 == 0 && crop.y0 == 0 && crop.x1 == w - 1 && crop.y1 == h - 1;

        /// <summary>Borra de la máscara todo lo que quede fuera de la zona útil.</summary>
        private static void ApplyCrop(bool[] mask, int w, int h, CropRect crop)
        {
            if (IsFullFrame(crop, w, h)) return;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (x < crop.x0 || x > crop.x1 || y < crop.y0 || y > crop.y1) mask[y * w + x] = false;
        }

        /// <summary>Un frame localizado dentro de la lámina.</summary>
        private struct Frame
        {
            public int x0, x1, y0, y1;   // caja de contenido
            public int ax, ay;           // ancla: el punto que será el pivote
        }

        /// <summary>Una mancha conexa de contenido.</summary>
        private class Blob
        {
            public int x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue;
            public int area;
            public int Width => x1 - x0 + 1;
            public int Height => y1 - y0 + 1;
        }

        /// <summary>Resultado del corte, para que los pasos siguientes no relean nada.</summary>
        public class Result
        {
            public readonly Dictionary<string, List<Sprite>> ByState = new();

            /// <summary>
            /// Objetos sueltos que <see cref="Reconcile"/> absorbió para cuadrar el número de
            /// frames de una fila — por ejemplo, la piedra que un ataque de lanzamiento dibuja
            /// separada del personaje. Se exportan también como sprite propio, centrado, listo
            /// para convertirse en el proyectil de ese ataque. Vacío si la fila no tenía ninguno.
            /// </summary>
            public readonly Dictionary<string, List<Sprite>> Props = new();

            public readonly StringBuilder Log = new();
            public bool Ok = true;
        }

        // ============================================================ entrada

        public static Result Slice(SpriteSheetRecipe recipe)
        {
            var result = new Result();

            if (recipe == null || recipe.sheet == null)
            {
                result.Ok = false;
                result.Log.AppendLine("[SheetSlicer] La receta no tiene lámina asignada.");
                return result;
            }

            if (recipe.rows == null || recipe.rows.Length == 0)
            {
                result.Ok = false;
                result.Log.AppendLine("[SheetSlicer] La receta no tiene filas.");
                return result;
            }

            string sheetPath = AssetDatabase.GetAssetPath(recipe.sheet);
            var tex = LoadRaw(sheetPath);
            if (tex == null)
            {
                result.Ok = false;
                result.Log.AppendLine($"[SheetSlicer] No se pudo leer '{sheetPath}'.");
                return result;
            }

            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();

            // Zona útil de la lámina: fuera queda lo que la receta manda ignorar (la columna de
            // rótulos, una paleta pintada al margen…). Se aplica ANTES de deducir el fondo, o el
            // color del rótulo entraría en la lista de tonos de fondo y se comería arte.
            var crop = ResolveCrop(recipe, w, h);

            // Máscara de contenido. Con alfa real basta el alfa; si no, se deduce el fondo de los
            // bordes (el damero de un JPG son dos grises que ocupan todo el marco).
            bool[] mask = recipe.keyBackground
                ? MaskByBackground(px, w, h, crop, recipe.backgroundTolerance, out var bgColors)
                : MaskByAlpha(px, recipe.alphaThreshold, out bgColors);

            ApplyCrop(mask, w, h, crop);

            result.Log.AppendLine($"[SheetSlicer] {recipe.characterName}: {w}x{h}, " +
                                  $"{(recipe.keyBackground ? $"fondo detectado ({bgColors.Count} tonos)" : "alfa real")}" +
                                  $"{(IsFullFrame(crop, w, h) ? "" : $", recorte x[{crop.x0}..{crop.x1}] y[{crop.y0}..{crop.y1}]")}.");

            string folder = recipe.ResolvedFolder;
            EnsureFolder(folder);

            var bands = recipe.sliceMode == SliceMode.Grid
                ? GridBands(recipe, crop)
                : AutoBands(mask, w, h, recipe.rows.Length, result.Log);

            if (bands.Count != recipe.rows.Length)
            {
                result.Log.AppendLine($"[SheetSlicer] AVISO: la receta declara {recipe.rows.Length} " +
                                      $"filas y se han detectado {bands.Count}. Revisa la lámina o " +
                                      $"pasa a modo Grid.");
            }

            int rowCount = Mathf.Min(bands.Count, recipe.rows.Length);

            for (int r = 0; r < rowCount; r++)
            {
                var row = recipe.rows[r];
                var (by0, by1) = bands[r];

                List<Blob> absorbed = null;
                List<Frame> frames;
                if (row.frameRects != null && row.frameRects.Length > 0)
                    // Recuadros puestos a mano: mandan sobre todo lo demás.
                    frames = ExplicitFrames(mask, w, h, row.frameRects, recipe.anchor, result.Log);
                else if (recipe.sliceMode == SliceMode.Grid)
                    frames = GridFrames(mask, w, crop, by0, by1, recipe.columns, recipe.anchor);
                else if (row.evenSplit && row.frames > 0)
                    // Filas con FX que se pisan en X (polvo, estallidos): la detección por
                    // contenido las junta, así que se parte la franja de contenido en N columnas
                    // iguales y se recorta cada una a su contenido.
                    frames = EvenSplitFrames(mask, w, by0, by1, row.frames, recipe.anchor, result.Log);
                else
                    frames = AutoFrames(mask, w, by0, by1, recipe.anchor, row.frames, result.Log, out absorbed);

                if (frames.Count == 0)
                {
                    result.Log.AppendLine($"  {row.state}: sin frames, fila saltada.");
                    continue;
                }

                if (row.frames > 0 && row.frames != frames.Count)
                {
                    result.Log.AppendLine($"  AVISO {row.state}: la receta pide {row.frames} frames " +
                                          $"y se han detectado {frames.Count}. Se usan los detectados.");
                }

                var sprites = Emit(recipe, row.state, frames, px, w, h, mask, folder, result.Log);
                if (sprites.Count > 0) result.ByState[row.state] = sprites;

                if (absorbed is { Count: > 0 })
                {
                    var props = EmitProps(recipe, row.state, absorbed, px, w, h, mask, folder, result.Log);
                    if (props.Count > 0) result.Props[row.state] = props;
                }
            }

            Object.DestroyImmediate(tex);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Reenganchar las referencias de sprite DESPUÉS del Refresh final. Los sprites que se
            // metieron en el resultado durante el corte se cargaron antes de este Refresh, que en
            // la PRIMERA importación de una lámina nueva reimporta los PNG y destruye esas
            // instancias. AnimClipBuilder recibía entonces referencias muertas y escribía clips
            // vacíos (1 s, 60 fps, sin eventos) — el bug que obligaba a lanzar el pack dos veces.
            RebindSprites(recipe, result);
            return result;
        }

        /// <summary>
        /// Vuelve a cargar de disco los sprites de cada estado y prop, por nombre, tras el Refresh
        /// final: así las referencias que se pasan a los pasos siguientes son las definitivas.
        /// </summary>
        private static void RebindSprites(SpriteSheetRecipe recipe, Result result)
        {
            string folder = recipe.ResolvedFolder;

            // Hoja de cada estado: un PNG con N sub-sprites <Char>_<Estado>_00..NN.
            foreach (var state in new List<string>(result.ByState.Keys))
            {
                int count = result.ByState[state].Count;
                var names = new string[count];
                for (int i = 0; i < count; i++) names[i] = SpriteName(recipe.characterName, state, i);

                var loaded = LoadSlicedSprites($"{folder}/{recipe.characterName}_{state}.png", names);
                if (loaded.Count == count) result.ByState[state] = loaded;
            }

            // Props: cada uno es su propio PNG con un único sprite del mismo nombre.
            foreach (var state in new List<string>(result.Props.Keys))
            {
                int count = result.Props[state].Count;
                var loaded = new List<Sprite>();
                for (int i = 0; i < count; i++)
                {
                    string name = PropSpriteName(recipe.characterName, state, i, count);
                    loaded.AddRange(LoadSlicedSprites($"{folder}/{name}.png", new[] { name }));
                }

                if (loaded.Count == count) result.Props[state] = loaded;
            }
        }

        /// <summary>
        /// Lee la textura del disco en vez de usar el asset importado. Así no hace falta marcar la
        /// lámina como <c>Read/Write</c> ni deshacer el cambio después, y funciona con JPG igual
        /// que con PNG.
        /// </summary>
        public static Texture2D LoadRaw(string assetPath)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!File.Exists(full)) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(full))) return tex;

            Object.DestroyImmediate(tex);
            return null;
        }

        // ============================================================ máscara

        private static bool[] MaskByAlpha(Color32[] px, float threshold, out List<Color32> bg)
        {
            bg = new List<Color32>();
            byte cut = (byte)Mathf.RoundToInt(Mathf.Clamp01(threshold) * 255f);

            var mask = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) mask[i] = px[i].a > cut;
            return mask;
        }

        /// <summary>
        /// Deduce el fondo del marco de la imagen y lo quita.
        ///
        /// Recorre el borde, agrupa los colores que aparecen ahí y se queda con los que cubren una
        /// parte apreciable del marco. Un damero de transparencia son exactamente dos grises que
        /// ocupan todo el borde, así que caen los dos; un fondo liso cae solo. Lo que el arte
        /// comparta con esos tonos por casualidad se recupera después, porque sólo se descartan
        /// las manchas pequeñas.
        /// </summary>
        private static bool[] MaskByBackground(Color32[] px, int w, int h, CropRect crop,
                                               float tolerance, out List<Color32> bg)
        {
            var tally = new Dictionary<int, (Color32 c, int n)>();
            int border = Mathf.Max(2, Mathf.Min(crop.Width, crop.Height) / 64);
            int sampled = 0;

            void Sample(int x, int y)
            {
                var c = px[y * w + x];
                int key = (c.r >> 3) << 10 | (c.g >> 3) << 5 | (c.b >> 3);   // cubos de 32 niveles
                tally[key] = tally.TryGetValue(key, out var e) ? (e.c, e.n + 1) : (c, 1);
                sampled++;
            }

            // El marco que se muestrea es el de la zona útil, no el de la lámina entera.
            for (int y = crop.y0; y <= crop.y1; y++)
            for (int x = crop.x0; x <= crop.x1; x++)
                if (x < crop.x0 + border || x > crop.x1 - border ||
                    y < crop.y0 + border || y > crop.y1 - border) Sample(x, y);

            // Umbral bajo a propósito: un damero son dos tonos y uno de ellos puede quedar en
            // minoría si el marco lo corta de forma desigual. Se cogen los tonos frecuentes, no
            // sólo el dominante.
            bg = new List<Color32>();
            foreach (var e in tally.Values)
                if (e.n > sampled * 0.02f) bg.Add(e.c);

            if (bg.Count == 0) bg.Add(px[0]);

            float tol = Mathf.Max(0.01f, tolerance) * 255f;
            var mask = new bool[px.Length];

            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a < 8) continue;   // si la lámina sí traía alfa, se respeta

                bool isBg = false;
                for (int b = 0; b < bg.Count; b++)
                {
                    var k = bg[b];
                    if (Mathf.Abs(c.r - k.r) <= tol && Mathf.Abs(c.g - k.g) <= tol &&
                        Mathf.Abs(c.b - k.b) <= tol)
                    {
                        isBg = true;
                        break;
                    }
                }

                mask[i] = !isBg;
            }

            return mask;
        }

        // ============================================================ bandas (filas)

        private static List<(int y0, int y1)> GridBands(SpriteSheetRecipe recipe, CropRect crop)
        {
            var bands = new List<(int, int)>();
            int n = Mathf.Max(1, recipe.rows.Length);
            float cell = crop.Height / (float)n;

            // La fila 0 de la receta es la de arriba de la imagen; en coordenadas de Unity y=0 es
            // abajo, así que se recorre al revés.
            for (int i = 0; i < n; i++)
            {
                int top = crop.y1 - Mathf.RoundToInt(i * cell);
                int bottom = (i == n - 1) ? crop.y0 : crop.y1 - Mathf.RoundToInt((i + 1) * cell) + 1;
                bands.Add((bottom, top));
            }

            return bands;
        }

        /// <summary>
        /// Bandas = las <paramref name="want"/> franjas horizontales de contenido más altas.
        ///
        /// La receta ya dice cuántas filas hay, así que no hace falta adivinar dónde corta cada
        /// una: los rótulos son franjas bajas y los personajes franjas altas, y quedarse con las
        /// más altas los descarta sin más reglas.
        ///
        /// El intento anterior — absorber cada franja baja en la de al lado — se comía la lámina
        /// entera: al fusionar el rótulo, la banda crecía, y una banda más alta cumple más
        /// fácilmente el criterio de fusión con la siguiente. Cuatro filas acababan siendo una.
        /// </summary>
        private static List<(int y0, int y1)> AutoBands(bool[] mask, int w, int h, int want,
                                                        StringBuilder log)
        {
            var rowHas = new bool[h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (mask[y * w + x]) { rowHas[y] = true; break; }

            var runs = Runs(rowHas, 1);

            if (runs.Count > want)
            {
                runs.Sort((p, q) => (q.b - q.a).CompareTo(p.b - p.a));
                log.AppendLine($"  [bandas] {runs.Count} franjas para {want} filas; se usan las " +
                               $"{want} más altas (el resto son rótulos).");
                runs = runs.GetRange(0, want);
            }

            // De arriba abajo de la imagen, que es el orden en que la receta lista las filas.
            runs.Sort((p, q) => q.a.CompareTo(p.a));

            var bands = new List<(int, int)>();
            foreach (var run in runs) bands.Add((run.a, run.b));
            return bands;
        }

        // ============================================================ frames dentro de una banda

        /// <summary>
        /// Frames a partir de los recuadros que trae la receta. Cada recuadro se recorta a su
        /// propio contenido, igual que en los demás modos: lo que se pone a mano es <b>dónde
        /// empieza y acaba cada frame</b>, no la celda final — el empaquetado uniforme y el pivote
        /// los sigue calculando el corte, que es lo que impide que la animación tiemble.
        /// </summary>
        private static List<Frame> ExplicitFrames(bool[] mask, int w, int h, RectInt[] rects,
                                                  AnchorMode anchor, StringBuilder log)
        {
            var frames = new List<Frame>();

            foreach (var rect in rects)
            {
                int x0 = Mathf.Clamp(Mathf.Min(rect.xMin, rect.xMax), 0, w - 1);
                int x1 = Mathf.Clamp(Mathf.Max(rect.xMin, rect.xMax) - 1, 0, w - 1);
                int y0 = Mathf.Clamp(Mathf.Min(rect.yMin, rect.yMax), 0, h - 1);
                int y1 = Mathf.Clamp(Mathf.Max(rect.yMin, rect.yMax) - 1, 0, h - 1);

                if (x1 < x0 || y1 < y0) continue;

                var f = Bounds(mask, w, x0, x1, y0, y1);
                if (f.x1 < f.x0) f = new Frame { x0 = x0, x1 = x1, y0 = y0, y1 = y1 };
                frames.Add(Anchor(f, anchor));
            }

            log.AppendLine($"  [frames] {frames.Count} recuadros explícitos de la receta.");
            return frames;
        }

        /// <summary>
        /// Reparte la franja en <paramref name="want"/> columnas iguales, tomando como ancho total
        /// la extensión de contenido de la banda (no la lámina entera: así los rótulos de la
        /// izquierda no descuadran el reparto). Cada columna se recorta luego a su propio
        /// contenido. Para filas donde el FX de un frame invade el de al lado y la detección por
        /// manchas los fundiría en uno.
        /// </summary>
        private static List<Frame> EvenSplitFrames(bool[] mask, int w, int y0, int y1, int want,
                                                   AnchorMode anchor, StringBuilder log)
        {
            var span = Bounds(mask, w, 0, w - 1, y0, y1);
            if (span.x1 < span.x0)
            {
                log.AppendLine("  [frames] reparto uniforme: la banda está vacía.");
                return new List<Frame>();
            }

            var frames = new List<Frame>();
            float cell = (span.x1 - span.x0 + 1) / (float)want;

            for (int c = 0; c < want; c++)
            {
                int cx0 = span.x0 + Mathf.RoundToInt(c * cell);
                int cx1 = (c == want - 1) ? span.x1 : span.x0 + Mathf.RoundToInt((c + 1) * cell) - 1;

                var f = Bounds(mask, w, cx0, cx1, y0, y1);
                if (f.x1 < f.x0) f = new Frame { x0 = cx0, x1 = cx1, y0 = y0, y1 = y1 };
                frames.Add(Anchor(f, anchor));
            }

            log.AppendLine($"  [frames] reparto uniforme en {want} columnas sobre " +
                           $"x[{span.x0}..{span.x1}].");
            return frames;
        }

        /// <summary>
        /// Rejilla uniforme dentro de la zona útil. Divide el <b>recorte</b>, no la lámina entera:
        /// con una columna de rótulos a la izquierda, repartir sobre el ancho total desplaza todas
        /// las celdas y parte a los personajes por la mitad.
        /// </summary>
        private static List<Frame> GridFrames(bool[] mask, int w, CropRect crop, int y0, int y1,
                                              int columns, AnchorMode anchor)
        {
            var frames = new List<Frame>();
            int n = Mathf.Max(1, columns);
            float cell = crop.Width / (float)n;

            for (int c = 0; c < n; c++)
            {
                int cx0 = crop.x0 + Mathf.RoundToInt(c * cell);
                int cx1 = (c == n - 1) ? crop.x1 : crop.x0 + Mathf.RoundToInt((c + 1) * cell) - 1;

                var f = Bounds(mask, w, cx0, cx1, y0, y1);
                if (f.x1 < f.x0) continue;
                frames.Add(Anchor(f, anchor));
            }

            return frames;
        }

        /// <summary>
        /// Frames por manchas conexas.
        ///
        /// Se etiquetan las manchas de la banda, se tiran los rótulos (bajos y colgados por encima
        /// del personaje) y el ruido, y lo que queda se agrupa por solapamiento horizontal — así
        /// las hojas sueltas que salen despedidas en la fila HURT viajan con su frame en lugar de
        /// contar como frames de pleno derecho.
        /// </summary>
        private static List<Frame> AutoFrames(bool[] mask, int w, int y0, int y1, AnchorMode anchor,
                                              int want, StringBuilder log, out List<Blob> absorbed)
        {
            absorbed = new List<Blob>();

            var blobs = Label(mask, w, y0, y1);
            if (blobs.Count == 0) return new List<Frame>();

            // Altura de referencia: la mancha más alta de la banda es el personaje.
            int tallest = 0, baseTop = 0;
            foreach (var b in blobs)
                if (b.Height > tallest) { tallest = b.Height; baseTop = b.y1; }

            var kept = new List<Blob>();
            foreach (var b in blobs)
            {
                if (b.area < 24) continue;                        // motas de compresión JPEG
                if (b.Height < tallest * 0.4f && b.y0 > baseTop) continue;   // rótulo: bajo y por encima
                kept.Add(b);
            }

            if (kept.Count == 0) return new List<Frame>();
            kept.Sort((p, q) => p.x0.CompareTo(q.x0));

            // Agrupa por solapamiento en X, con un margen de tolerancia proporcional al personaje.
            int slack = Mathf.Max(4, tallest / 12);
            var groups = new List<Blob>();
            var current = kept[0];

            for (int i = 1; i < kept.Count; i++)
            {
                var b = kept[i];
                if (b.x0 <= current.x1 + slack)
                {
                    current.x0 = Mathf.Min(current.x0, b.x0);
                    current.x1 = Mathf.Max(current.x1, b.x1);
                    current.y0 = Mathf.Min(current.y0, b.y0);
                    current.y1 = Mathf.Max(current.y1, b.y1);
                    current.area += b.area;
                    continue;
                }

                groups.Add(current);
                current = b;
            }

            groups.Add(current);

            int grouped = groups.Count;
            Reconcile(groups, want, log, absorbed);

            var frames = new List<Frame>();
            foreach (var g in groups)
                frames.Add(Anchor(new Frame { x0 = g.x0, x1 = g.x1, y0 = g.y0, y1 = g.y1 }, anchor));

            log.AppendLine($"  [frames] {blobs.Count} manchas → {kept.Count} útiles → " +
                           $"{grouped} grupos → {frames.Count} frames.");
            return frames;
        }

        /// <summary>
        /// Reduce los grupos hasta los <paramref name="want"/> que declara la receta.
        ///
        /// Un frame puede contener algo que vuela suelto y separado del personaje — el proyectil
        /// que el TreeWalk lanza en su cuarto frame de ataque —, y eso se detecta como un frame
        /// más. El número que declara la receta es la verdad: se va retirando el grupo más pequeño
        /// hasta que cuadra, y el más pequeño siempre es el objeto suelto antes que un personaje.
        ///
        /// Retirar, no fundir: el objeto sale de la cuenta de frames y se exporta aparte, pero
        /// <b>no</b> entra en los límites de ningún frame (ver el comentario del bucle).
        ///
        /// Con <paramref name="want"/> a 0 (la receta no lo declara) no se toca nada.
        ///
        /// Lo absorbido se apila en <paramref name="absorbed"/> tal cual estaba antes de fundirse
        /// en el host, para que <see cref="Slice"/> pueda exportarlo aparte como el sprite de un
        /// proyectil — es exactamente la piedra que un ataque de lanzamiento dibuja suelta.
        /// </summary>
        private static void Reconcile(List<Blob> groups, int want, StringBuilder log, List<Blob> absorbed)
        {
            if (want <= 0) return;

            while (groups.Count > want && groups.Count > 1)
            {
                int smallest = 0;
                for (int i = 1; i < groups.Count; i++)
                    if (groups[i].area < groups[smallest].area) smallest = i;

                var eaten = groups[smallest];
                absorbed.Add(new Blob { x0 = eaten.x0, x1 = eaten.x1, y0 = eaten.y0, y1 = eaten.y1, area = eaten.area });

                // El grupo absorbido se saca de la cuenta de frames PERO NO se funde en los
                // límites del anfitrión, a propósito. Un proyectil dibujado suelto está lejos del
                // personaje, así que fundirlo estiraba la celda hasta abarcar los dos: medido en la
                // lámina del Ogro, la celda de ataque pasaba de ~140 px a 385, y como la celda es
                // uniforme para toda la fila, el resto de frames se rellenaban de aire y el
                // personaje quedaba diminuto y descentrado.
                //
                // Además el proyectil no debe ir pintado en el frame: el juego lanza ahí un
                // proyectil de verdad (ese mismo sprite, ya exportado como prop), así que si se
                // quedara en el dibujo se verían dos.
                groups.RemoveAt(smallest);
                log.AppendLine($"    objeto suelto separado (área {eaten.area}) — fuera del frame, " +
                               "exportado como prop.");
            }
        }

        /// <summary>Etiquetado de componentes conexas por inundación iterativa (sin recursión).</summary>
        private static List<Blob> Label(bool[] mask, int w, int y0, int y1)
        {
            var seen = new bool[(y1 - y0 + 1) * w];
            var blobs = new List<Blob>();
            var stack = new Stack<int>();

            for (int y = y0; y <= y1; y++)
            for (int x = 0; x < w; x++)
            {
                int local = (y - y0) * w + x;
                if (seen[local] || !mask[y * w + x]) continue;

                var blob = new Blob();
                stack.Push(local);
                seen[local] = true;

                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    int py = p / w + y0, pxl = p % w;

                    if (pxl < blob.x0) blob.x0 = pxl;
                    if (pxl > blob.x1) blob.x1 = pxl;
                    if (py < blob.y0) blob.y0 = py;
                    if (py > blob.y1) blob.y1 = py;
                    blob.area++;

                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = pxl + dx, ny = py + dy;
                        if (nx < 0 || nx >= w || ny < y0 || ny > y1) continue;

                        int nl = (ny - y0) * w + nx;
                        if (seen[nl] || !mask[ny * w + nx]) continue;

                        seen[nl] = true;
                        stack.Push(nl);
                    }
                }

                blobs.Add(blob);
            }

            return blobs;
        }

        private static Frame Bounds(bool[] mask, int w, int xa, int xb, int y0, int y1)
        {
            var f = new Frame { x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue };

            for (int y = y0; y <= y1; y++)
            for (int x = xa; x <= xb; x++)
            {
                if (x < 0 || x >= w || !mask[y * w + x]) continue;
                if (x < f.x0) f.x0 = x;
                if (x > f.x1) f.x1 = x;
                if (y < f.y0) f.y0 = y;
                if (y > f.y1) f.y1 = y;
            }

            return f;
        }

        private static Frame Anchor(Frame f, AnchorMode mode)
        {
            f.ax = (f.x0 + f.x1) / 2;
            f.ay = mode == AnchorMode.BottomCenter ? f.y0 : (f.y0 + f.y1) / 2;
            return f;
        }

        // ============================================================ salida

        /// <summary>
        /// Escribe la hoja limpia de un estado y la corta.
        ///
        /// La celda es común a todos los frames del estado y el ancla cae siempre en el mismo
        /// punto de la celda — que es también el pivote. Es lo que hace que el personaje no se
        /// mueva solo al cambiar de frame ni al cambiar de estado.
        /// </summary>
        private static List<Sprite> Emit(SpriteSheetRecipe recipe, string state, List<Frame> frames,
                                         Color32[] px, int w, int h, bool[] mask, string folder,
                                         StringBuilder log)
        {
            int l = 0, r = 0, d = 0, u = 0;
            foreach (var f in frames)
            {
                l = Mathf.Max(l, f.ax - f.x0);
                r = Mathf.Max(r, f.x1 - f.ax);
                d = Mathf.Max(d, f.ay - f.y0);
                u = Mathf.Max(u, f.y1 - f.ay);
            }

            int m = recipe.margin;
            l += m; r += m; d += m; u += m;
            int cw = Mathf.Max(1, l + r), ch = Mathf.Max(1, d + u);

            var outTex = new Texture2D(cw * frames.Count, ch, TextureFormat.RGBA32, false);
            outTex.SetPixels32(new Color32[cw * frames.Count * ch]);

            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];

                // Territorio del frame: hasta el punto medio con el vecino. Recortar más allá
                // metería en la celda un trozo del frame de al lado, que al animar parpadea.
                int t0 = i == 0 ? int.MinValue : (frames[i - 1].x1 + f.x0) / 2;
                int t1 = i == frames.Count - 1 ? int.MaxValue : (f.x1 + frames[i + 1].x0) / 2;

                for (int cy = 0; cy < ch; cy++)
                for (int cx = 0; cx < cw; cx++)
                {
                    int sx = f.ax - l + cx;
                    int sy = f.ay - d + cy;
                    if (sx < t0 || sx > t1) continue;
                    if (sx < 0 || sx >= w || sy < 0 || sy >= h) continue;
                    if (!mask[sy * w + sx]) continue;

                    // Con alfa real se conserva tal cual (bordes suaves). Al deducir el fondo no
                    // hay alfa que conservar: el recorte es duro, y por eso una lámina con alfa
                    // de verdad siempre da mejor resultado que un JPG con el damero pintado.
                    var c = px[sy * w + sx];
                    byte a = recipe.keyBackground ? (byte)255 : c.a;
                    outTex.SetPixel(i * cw + cx, cy, new Color32(c.r, c.g, c.b, a));
                }
            }

            outTex.Apply();

            string path = $"{folder}/{recipe.characterName}_{state}.png";
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);

            var pivot = new Vector2((float)l / cw, (float)d / ch);
            log.AppendLine($"  {state}: {frames.Count} frames, celda {cw}x{ch}, pivote {pivot}. → {path}");

            var names = new string[frames.Count];
            for (int i = 0; i < frames.Count; i++) names[i] = SpriteName(recipe.characterName, state, i);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureAndSlice(recipe, path, names, cw, ch, pivot);

            // Cargar los sub-sprites recién cortados, reintentando. La PRIMERA importación de un
            // PNG nuevo no siempre deja los sub-assets consultables por LoadAllAssetsAtPath en el
            // mismo tick, aunque ConfigureAndSlice acabe con SaveAndReimport; sin este reintento la
            // fila salía sin sprites, AnimClipBuilder se la saltaba y el clip quedaba vacío (1 s,
            // 60 fps, sin eventos) — el bug que obligaba a lanzar el pack dos veces.
            var sprites = LoadSlicedSprites(path, names);
            for (int attempt = 0; attempt < 3 && sprites.Count != frames.Count; attempt++)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                sprites = LoadSlicedSprites(path, names);
            }

            if (sprites.Count != frames.Count)
                log.AppendLine($"  AVISO {state}: se esperaban {frames.Count} sprites y se han " +
                               $"cargado {sprites.Count}.");

            return sprites;
        }

        /// <summary>
        /// Los objetos sueltos que <see cref="Reconcile"/> absorbió, exportados cada uno como su
        /// propio sprite: fondo quitado igual que un frame normal, pero <b>centrado</b> siempre
        /// (el ancla de la fila puede ser a los pies, que no tiene sentido para algo que vuela) y
        /// con el margen mínimo — es un proyectil, no hace falta aire alrededor.
        /// </summary>
        private static List<Sprite> EmitProps(SpriteSheetRecipe recipe, string state, List<Blob> absorbed,
                                              Color32[] px, int w, int h, bool[] mask, string folder,
                                              StringBuilder log)
        {
            var sprites = new List<Sprite>();

            for (int i = 0; i < absorbed.Count; i++)
            {
                var b = absorbed[i];
                int m = Mathf.Max(2, recipe.margin / 2);
                int cw = b.Width + m * 2, ch = b.Height + m * 2;

                var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                outTex.SetPixels32(new Color32[cw * ch]);

                for (int cy = 0; cy < ch; cy++)
                for (int cx = 0; cx < cw; cx++)
                {
                    int sx = b.x0 - m + cx;
                    int sy = b.y0 - m + cy;
                    if (sx < 0 || sx >= w || sy < 0 || sy >= h || !mask[sy * w + sx]) continue;

                    var c = px[sy * w + sx];
                    byte a = recipe.keyBackground ? (byte)255 : c.a;
                    outTex.SetPixel(cx, cy, new Color32(c.r, c.g, c.b, a));
                }

                outTex.Apply();

                string name = PropSpriteName(recipe.characterName, state, i, absorbed.Count);
                string path = $"{folder}/{name}.png";
                File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), outTex.EncodeToPNG());
                Object.DestroyImmediate(outTex);

                log.AppendLine($"  {name}: prop suelto {cw}x{ch}, centrado. → {path}");

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ConfigureAndSlice(recipe, path, new[] { name }, cw, ch, new Vector2(0.5f, 0.5f));

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Sprite s && s.name == name) { sprites.Add(s); break; }
            }

            return sprites;
        }

        /// <summary>Convención de nombre: <c>Personaje_Estado_00</c>.</summary>
        public static string SpriteName(string character, string state, int index)
            => $"{character}_{state}_{index:00}";

        /// <summary>Nombre del sprite de un objeto suelto exportado por <see cref="EmitProps"/>.</summary>
        public static string PropSpriteName(string character, string state, int index, int count)
            => count > 1 ? $"{character}_{state}_Prop{index}" : $"{character}_{state}_Prop";

        /// <summary>Los sub-sprites de <paramref name="path"/> en el orden de <paramref name="names"/>.</summary>
        private static List<Sprite> LoadSlicedSprites(string path, string[] names)
        {
            var byName = new Dictionary<string, Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite s) byName[s.name] = s;

            var sprites = new List<Sprite>();
            foreach (var name in names)
                if (byName.TryGetValue(name, out var s)) sprites.Add(s);

            return sprites;
        }

        private static void ConfigureAndSlice(SpriteSheetRecipe recipe, string path, string[] names,
                                              int cw, int ch, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = recipe.pixelsPerUnit;
            importer.filterMode = recipe.filterMode;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new System.Exception($"Sin data provider para '{path}'.");
            provider.InitSpriteEditorDataProvider();

            var edit = provider.GetDataProvider<ISpriteFrameEditCapability>();
            if (edit == null) throw new System.Exception($"El importer de '{path}' no permite editar sprites.");

            var cap = edit.GetEditCapability();
            if (!cap.HasCapability(EEditCapability.CreateAndDeleteSprite) ||
                !cap.HasCapability(EEditCapability.EditSpriteName) ||
                !cap.HasCapability(EEditCapability.EditSpriteRect) ||
                !cap.HasCapability(EEditCapability.EditPivot))
                throw new System.Exception($"El importer de '{path}' no permite cortar la rejilla.");

            var rects = new SpriteRect[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                rects[i] = new SpriteRect
                {
                    name = names[i],
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
                foreach (var rect in rects) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
                nameFileId.SetNameFileIdPairs(pairs);
            }

            provider.Apply();
            importer.SaveAndReimport();
        }

        // ============================================================ utilidades

        /// <summary>Tramos contiguos de <c>true</c>, uniendo huecos menores que <paramref name="minGap"/>.</summary>
        private static List<(int a, int b)> Runs(bool[] on, int minGap)
        {
            var runs = new List<(int, int)>();
            int start = -1, last = -1;

            for (int i = 0; i < on.Length; i++)
            {
                if (!on[i]) continue;
                if (start < 0) start = i;
                else if (i - last - 1 >= minGap) { runs.Add((start, last)); start = i; }
                last = i;
            }

            if (start >= 0) runs.Add((start, last));
            return runs;
        }

        public static void EnsureFolder(string folder)
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
    }
}
