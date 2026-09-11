using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Kit de arte de UI crudo → sprites limpios listos para UI Toolkit (<see cref="UiArtKitRecipe"/>).
    ///
    /// Por cada imagen: deduce el fondo liso del marco (igual que <c>SheetSlicer.MaskByBackground</c>),
    /// lo quita por inundación desde el borde (y, si se pide, también el hueco grande encerrado de un
    /// marco "con espacio para icono"), des-mezcla del fondo el color de la franja junto al contorno
    /// para que los halos de brillo se desvanezcan en vez de cortarse, recorta, separa la rejilla de
    /// estados y deja un PNG por celda con sus bordes de 9-slice ya puestos.
    ///
    /// Determinista y re-ejecutable: sobrescribe los PNG generados, nunca las fuentes.
    /// </summary>
    public static class UiArtKitProcessor
    {
        /// <summary>Ancho (px) de la tira que se estira en un marco <see cref="UiSliceMode.Quad"/>.</summary>
        public const int StretchStrip = 2;

        /// <summary>Sprites generados por celda. Quad = 4 (TL, TR, BL, BR); None = 1.</summary>
        public sealed class Result
        {
            public readonly Dictionary<string, Sprite[]> Sprites = new();
            public readonly StringBuilder Log = new();

            public Sprite Single(string cell) =>
                Sprites.TryGetValue(cell, out var s) && s.Length > 0 ? s[0] : null;

            public Sprite[] Quads(string cell) =>
                Sprites.TryGetValue(cell, out var s) && s.Length >= 4 ? new[] { s[0], s[1], s[2], s[3] } : null;

            /// <summary>El interior de un marco Quad en una sola pieza (null si no se detectó).</summary>
            public Sprite Fill(string cell) =>
                Sprites.TryGetValue(cell, out var s) && s.Length == 5 ? s[4] : null;
        }

        private struct Box
        {
            public int x0, y0, x1, y1;   // inclusivo, y hacia abajo
            public bool Empty => x1 < x0;
            public static Box None => new() { x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue };

            public void Add(int x, int y)
            {
                if (x < x0) x0 = x;
                if (y < y0) y0 = y;
                if (x > x1) x1 = x;
                if (y > y1) y1 = y;
            }

            public Box Union(Box o) => Empty ? o : o.Empty ? this : new Box
            {
                x0 = Mathf.Min(x0, o.x0), y0 = Mathf.Min(y0, o.y0),
                x1 = Mathf.Max(x1, o.x1), y1 = Mathf.Max(y1, o.y1),
            };
        }

        /// <summary>Imagen ya sin fondo, en orden "como se ve" (fila 0 arriba).</summary>
        private sealed class Keyed
        {
            public UiArtPiece Piece;
            public Color32[] Px;
            public int W, H;
            public int CellW, CellH;
            public Box[] Cells;   // contenido de cada celda, en coordenadas de celda
        }

        public static Result Run(UiArtKitRecipe recipe)
        {
            var result = new Result();
            SheetSlicer.EnsureFolder(recipe.outputFolder);

            var keyed = new List<Keyed>();
            foreach (var piece in recipe.pieces)
            {
                if (piece?.source == null)
                {
                    result.Log.AppendLine($"⚠ '{piece?.name}': sin imagen fuente, se salta.");
                    continue;
                }

                var k = Key(recipe, piece, result.Log);
                if (k != null) keyed.Add(k);
            }

            ShareGroupTrims(keyed, result.Log);

            foreach (var k in keyed) Emit(recipe, k, result);

            AssetDatabase.Refresh();
            return result;
        }

        // ============================================================ fondo

        private static Keyed Key(UiArtKitRecipe recipe, UiArtPiece piece, StringBuilder log)
        {
            string path = AssetDatabase.GetAssetPath(piece.source);
            var raw = SheetSlicer.LoadRaw(path);
            if (raw == null)
            {
                log.AppendLine($"⚠ '{piece.name}': no se pudo leer '{path}'.");
                return null;
            }

            int w = raw.width, h = raw.height;
            var bottomUp = raw.GetPixels32();
            Object.DestroyImmediate(raw);

            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                System.Array.Copy(bottomUp, (h - 1 - y) * w, px, y * w, w);

            var bg = BackgroundColor(px, w, h);
            float tol = recipe.backgroundTolerance;

            // Qué es fondo: lo parecido al color de fondo conectado con el borde de la imagen…
            var raw0 = new float[px.Length];
            for (int i = 0; i < px.Length; i++) raw0[i] = ColorToAlpha(px[i], bg);

            var isBg = new bool[px.Length];
            var queue = new int[px.Length];
            int head = 0, tail = 0;

            void Seed(int i)
            {
                if (isBg[i] || raw0[i] > tol) return;
                isBg[i] = true;
                queue[tail++] = i;
            }

            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }

            while (head < tail)
            {
                int i = queue[head++];
                int x = i % w, y = i / w;
                if (x > 0) Seed(i - 1);
                if (x < w - 1) Seed(i + 1);
                if (y > 0) Seed(i - w);
                if (y < h - 1) Seed(i + w);
            }

            var outerBg = (bool[])isBg.Clone();

            // …y, si se pide, los huecos grandes encerrados (el interior de un marco hueco).
            int holes = 0;
            if (piece.keyEnclosed) holes = KeyEnclosed(raw0, isBg, w, h, tol, recipe.minEnclosedArea);

            // La franja suave ancha (halos) sólo mira al fondo de fuera; junto a un hueco encerrado
            // basta un contorno limpio, o el marco entero caería dentro de la franja.
            var depthOuter = Depth(outerBg, w, h, piece.softEdge + 1);
            int holeEdge = Mathf.Min(piece.softEdge, 4);
            var depthHoles = holes > 0 ? Depth(isBg, w, h, holeEdge + 1) : null;
            float full = Mathf.Max(tol + 0.05f, recipe.softOpaqueAt);

            var outPx = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                if (isBg[i]) continue;   // (0,0,0,0)
                var c = px[i];
                bool soft = depthOuter[i] <= piece.softEdge || (depthHoles != null && depthHoles[i] <= holeEdge);
                outPx[i] = soft ? Unmix(c, bg, raw0[i], tol, full) : new Color32(c.r, c.g, c.b, 255);
            }

            var k = new Keyed
            {
                Piece = piece, Px = outPx, W = w, H = h,
                CellW = w / Mathf.Max(1, piece.columns),
                CellH = h / Mathf.Max(1, piece.rows),
                Cells = new Box[piece.columns * piece.rows],
            };

            for (int r = 0; r < piece.rows; r++)
            for (int c = 0; c < piece.columns; c++)
                k.Cells[r * piece.columns + c] = ContentBox(k, c, r);

            log.AppendLine($"• {piece.name}: {w}×{h}, fondo ({bg.r},{bg.g},{bg.b})" +
                           (holes > 0 ? $", {holes} hueco(s) encerrado(s) quitado(s)" : "") +
                           $", {piece.columns}×{piece.rows} celda(s).");
            return k;
        }

        /// <summary>
        /// El color de fondo: el tono más repetido del marco de la imagen (cubos de 32 niveles, como
        /// <c>SheetSlicer.MaskByBackground</c>), promediado dentro de su cubo para no heredar el ruido
        /// de un JPG.
        /// </summary>
        private static Color32 BackgroundColor(Color32[] px, int w, int h)
        {
            int border = Mathf.Max(2, Mathf.Min(w, h) / 64);
            var tally = new Dictionary<int, (long r, long g, long b, int n)>();

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (x >= border && x < w - border && y >= border && y < h - border) continue;
                var c = px[y * w + x];
                int key = (c.r >> 3) << 10 | (c.g >> 3) << 5 | (c.b >> 3);
                tally.TryGetValue(key, out var e);
                tally[key] = (e.r + c.r, e.g + c.g, e.b + c.b, e.n + 1);
            }

            (long r, long g, long b, int n) best = default;
            foreach (var e in tally.Values)
                if (e.n > best.n) best = e;

            return best.n == 0 ? new Color32(255, 255, 255, 255)
                : new Color32((byte)(best.r / best.n), (byte)(best.g / best.n), (byte)(best.b / best.n), 255);
        }

        /// <summary>
        /// Cuánta opacidad hace falta, como mínimo, para obtener <paramref name="c"/> mezclando algún
        /// color sobre el fondo <paramref name="k"/> ("color a alfa"). Vale para cualquier fondo liso:
        /// sobre blanco mide lo oscuro, sobre negro lo brillante.
        /// </summary>
        private static float ColorToAlpha(Color32 c, Color32 k)
        {
            float a = Channel(c.r, k.r);
            a = Mathf.Max(a, Channel(c.g, k.g));
            return Mathf.Max(a, Channel(c.b, k.b));

            // Denominador con suelo: sobre un fondo casi blanco, "más claro que el fondo" dividiría
            // entre ~0 y el ruido del JPG (+1 nivel) saldría opaco — no se quitaría nada.
            static float Channel(int v, int bg) =>
                v > bg ? (v - bg) / Mathf.Max(64f, 255f - bg)
                : v < bg ? (bg - v) / Mathf.Max(64f, bg)
                : 0f;
        }

        /// <summary>
        /// El pixel de la franja de contorno, des-mezclado del fondo: <c>F = (c − (1−a)·K) / a</c>.
        /// La rodilla (<paramref name="knee"/> = tolerancia) empalma con el fondo quitado, así que el
        /// halo se funde a 0 sin escalón; a partir de <paramref name="full"/> es opaco, porque "color a
        /// alfa" puro da por translúcido cualquier color apagado (la piedra), no sólo el brillo.
        /// </summary>
        private static Color32 Unmix(Color32 c, Color32 k, float raw, float knee, float full)
        {
            float a = Mathf.Clamp01((raw - knee) / (full - knee));
            if (a <= 0.004f) return new Color32(0, 0, 0, 0);
            if (a >= 0.996f) return new Color32(c.r, c.g, c.b, 255);

            return new Color32(Ch(c.r, k.r), Ch(c.g, k.g), Ch(c.b, k.b), (byte)Mathf.RoundToInt(a * 255f));

            byte Ch(int v, int bg) => (byte)Mathf.Clamp(Mathf.RoundToInt((v - (1f - a) * bg) / a), 0, 255);
        }

        private static int KeyEnclosed(float[] raw0, bool[] isBg, int w, int h, float tol, float minArea)
        {
            int min = Mathf.RoundToInt(minArea * w * h);
            var seen = new bool[raw0.Length];
            var queue = new int[raw0.Length];
            int holes = 0;

            for (int start = 0; start < raw0.Length; start++)
            {
                if (isBg[start] || seen[start] || raw0[start] > tol) continue;

                int head = 0, tail = 0;
                seen[start] = true;
                queue[tail++] = start;

                while (head < tail)
                {
                    int i = queue[head++];
                    int x = i % w, y = i / w;
                    if (x > 0) Visit(i - 1);
                    if (x < w - 1) Visit(i + 1);
                    if (y > 0) Visit(i - w);
                    if (y < h - 1) Visit(i + w);
                }

                if (tail < min) continue;
                for (int j = 0; j < tail; j++) isBg[queue[j]] = true;
                holes++;

                void Visit(int j)
                {
                    if (isBg[j] || seen[j] || raw0[j] > tol) return;
                    seen[j] = true;
                    queue[tail++] = j;
                }
            }

            return holes;
        }

        /// <summary>Distancia (chaflán, 8 vecinos) de cada pixel de contenido al fondo, con tope.</summary>
        private static int[] Depth(bool[] isBg, int w, int h, int cap)
        {
            var d = new int[w * h];

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (isBg[i]) continue;
                int v = cap;
                if (x > 0) v = Mathf.Min(v, d[i - 1] + 1);
                if (y > 0)
                {
                    v = Mathf.Min(v, d[i - w] + 1);
                    if (x > 0) v = Mathf.Min(v, d[i - w - 1] + 1);
                    if (x < w - 1) v = Mathf.Min(v, d[i - w + 1] + 1);
                }
                d[i] = v;
            }

            for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x;
                if (isBg[i]) continue;
                int v = d[i];
                if (x < w - 1) v = Mathf.Min(v, d[i + 1] + 1);
                if (y < h - 1)
                {
                    v = Mathf.Min(v, d[i + w] + 1);
                    if (x < w - 1) v = Mathf.Min(v, d[i + w + 1] + 1);
                    if (x > 0) v = Mathf.Min(v, d[i + w - 1] + 1);
                }
                d[i] = v;
            }

            return d;
        }

        // ============================================================ recorte

        private static Box ContentBox(Keyed k, int col, int row)
        {
            var box = Box.None;
            int ox = col * k.CellW, oy = row * k.CellH;

            for (int y = 0; y < k.CellH; y++)
            for (int x = 0; x < k.CellW; x++)
                if (k.Px[(oy + y) * k.W + ox + x].a > 12) box.Add(x, y);

            return box;
        }

        /// <summary>
        /// Normal y hover en archivos distintos, dibujados en el mismo sitio: todos toman la unión de
        /// sus cajas, así se superponen al pixel al intercambiarse.
        /// </summary>
        private static void ShareGroupTrims(List<Keyed> keyed, StringBuilder log)
        {
            var groups = new Dictionary<int, List<Keyed>>();
            foreach (var k in keyed)
            {
                if (k.Piece.trimGroup == 0) continue;
                if (!groups.TryGetValue(k.Piece.trimGroup, out var list)) groups[k.Piece.trimGroup] = list = new();
                list.Add(k);
            }

            foreach (var (group, list) in groups)
            {
                var union = Box.None;
                bool sameSize = true;
                foreach (var k in list)
                {
                    foreach (var box in k.Cells) union = union.Union(box);
                    sameSize &= k.CellW == list[0].CellW && k.CellH == list[0].CellH;
                }

                if (!sameSize)
                {
                    log.AppendLine($"⚠ Grupo de recorte {group}: celdas de distinto tamaño, cada pieza recorta por su cuenta.");
                    continue;
                }

                foreach (var k in list)
                    for (int i = 0; i < k.Cells.Length; i++) k.Cells[i] = union;
            }
        }

        // ============================================================ salida

        private static void Emit(UiArtKitRecipe recipe, Keyed k, Result result)
        {
            var piece = k.Piece;

            // Todas las celdas salen del mismo tamaño (la mayor caja), cada una centrada en su propio
            // contenido: una rejilla dibujada a mano no está registrada al pixel, y centrar es lo que
            // hace que normal, hover, vacío y equipado caigan en el mismo sitio al intercambiarse.
            int bw = 0, bh = 0;
            foreach (var box in k.Cells)
            {
                if (box.Empty) continue;
                bw = Mathf.Max(bw, box.x1 - box.x0 + 1);
                bh = Mathf.Max(bh, box.y1 - box.y0 + 1);
            }

            if (bw == 0)
            {
                result.Log.AppendLine($"⚠ '{piece.name}': no queda nada tras quitar el fondo.");
                return;
            }

            int m = recipe.margin;
            int cw = bw + 2 * m, ch = bh + 2 * m;

            int cells = piece.columns * piece.rows;
            for (int cell = 0; cell < cells; cell++)
            {
                var own = k.Cells[cell];
                if (own.Empty)
                {
                    result.Log.AppendLine($"⚠ '{piece.name}' celda {cell}: vacía, se salta.");
                    continue;
                }

                int x0 = own.x0 - m - (bw - (own.x1 - own.x0 + 1)) / 2;
                int y0 = own.y0 - m - (bh - (own.y1 - own.y0 + 1)) / 2;

                int col = cell % piece.columns, row = cell / piece.columns;
                string cellName = piece.cellNames != null && cell < piece.cellNames.Length &&
                                  !string.IsNullOrWhiteSpace(piece.cellNames[cell])
                    ? piece.cellNames[cell]
                    : cells > 1 ? $"{piece.name}_{cell}" : piece.name;

                // Recorte (y hacia abajo) → textura (y hacia arriba). Lo que caiga fuera de la celda
                // (el centrado puede pedirlo) queda transparente.
                var crop = new Color32[cw * ch];
                for (int y = 0; y < ch; y++)
                {
                    int ly = y0 + y;
                    if (ly < 0 || ly >= k.CellH) continue;
                    for (int x = 0; x < cw; x++)
                    {
                        int lx = x0 + x;
                        if (lx < 0 || lx >= k.CellW) continue;
                        crop[(ch - 1 - y) * cw + x] = k.Px[(row * k.CellH + ly) * k.W + col * k.CellW + lx];
                    }
                }

                string file = $"{recipe.outputFolder}/{recipe.prefix}{cellName}.png";
                var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                tex.SetPixels32(crop);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), file), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceSynchronousImport);

                Sprite[] sprites;
                if (piece.slice == UiSliceMode.Quad)
                {
                    var rects = QuadRects(crop, cw, ch, piece.stretchSearch, piece.interior,
                        col * k.CellW + x0, row * k.CellH + y0, $"{recipe.prefix}{cellName}", result.Log);
                    sprites = Import(file, rects, true);
                }
                else
                {
                    sprites = Import(file, new[]
                    {
                        new SpriteRect { name = $"{recipe.prefix}{cellName}", rect = new Rect(0, 0, cw, ch), border = Vector4.zero },
                    }, false);
                }

                result.Sprites[cellName] = sprites;
                result.Log.AppendLine($"  → {file} ({cw}×{ch}){(piece.slice == UiSliceMode.Quad ? " · quad" : "")}");
            }
        }

        /// <summary>
        /// Los 4 cuadrantes de un marco Quad (textura y=0 abajo). Cada uno es un 9-slice cuyo único
        /// tramo elástico es una tira de <see cref="StretchStrip"/> px en la columna/fila más lisa de su
        /// franja de búsqueda: el resto (esquina, runas, mitad del adorno central) queda a escala fija.
        /// Las columnas/filas se comparten entre cuadrantes vecinos, así que las costuras casan.
        /// </summary>
        /// <param name="interior">Panel interior en px de la fuente (y hacia abajo); ancho 0 = sin relleno.</param>
        /// <param name="ox">Origen del recorte dentro de la fuente, para pasar <paramref name="interior"/> a él.</param>
        private static SpriteRect[] QuadRects(Color32[] bottomUp, int w, int h, Vector2 search, RectInt interior,
                                              int ox, int oy, string name, StringBuilder log)
        {
            int s = StretchStrip;
            int cx = w / 2, cy = h / 2;

            Color32 P(int x, int yTop) => bottomUp[(h - 1 - yTop) * w + x];

            int FlattestColumn(int from, int to)
            {
                int best = from; long bestCost = long.MaxValue;
                for (int x = from; x <= to; x++)
                {
                    long cost = 0;
                    for (int y = 0; y < h; y++)
                    for (int j = 0; j < s; j++)
                        cost += Diff(P(x + j, y), P(x + j + 1, y));
                    if (cost < bestCost) { bestCost = cost; best = x; }
                }
                return best;
            }

            int FlattestRow(int from, int to)
            {
                int best = from; long bestCost = long.MaxValue;
                for (int y = from; y <= to; y++)
                {
                    long cost = 0;
                    for (int x = 0; x < w; x++)
                    for (int j = 0; j < s; j++)
                        cost += Diff(P(x, y + j), P(x, y + j + 1));
                    if (cost < bestCost) { bestCost = cost; best = y; }
                }
                return best;
            }

            int lo(int size) => Mathf.Clamp(Mathf.RoundToInt(search.x * size), 1, size / 2 - s - 2);
            int hi(int size) => Mathf.Clamp(Mathf.RoundToInt(search.y * size), lo(size), size / 2 - s - 2);

            int left = FlattestColumn(lo(w), hi(w));
            int right = FlattestColumn(w - 1 - hi(w) - s, w - 1 - lo(w) - s);
            int top = FlattestRow(lo(h), hi(h));
            int bottom = FlattestRow(h - 1 - hi(h) - s, h - 1 - lo(h) - s);

            log.AppendLine($"  quad {name}: tiras x={left}/{right}, y={top}/{bottom} (de {w}×{h})");

            // border = (izq, abajo, der, arriba). Filas de arriba en y "como se ve".
            var rects = new List<SpriteRect>
            {
                Quad($"{name}_TL", 0, 0, cx, cy, left, cx - left - s, top, cy - top - s),
                Quad($"{name}_TR", cx, 0, w - cx, cy, right - cx, w - right - s, top, cy - top - s),
                Quad($"{name}_BL", 0, cy, cx, h - cy, left, cx - left - s, bottom - cy, h - bottom - s),
                Quad($"{name}_BR", cx, cy, w - cx, h - cy, right - cx, w - right - s, bottom - cy, h - bottom - s),
            };

            // El interior del marco en UNA pieza, que en runtime se pinta encima de los cuadrantes: si
            // no, el interior sale de estirar las tiras de 2 px y se ve partido en rectángulos. Viene
            // medido en la receta (detectarlo solo confundía el bisel con la talla del marco). Sólo
            // encaja al pixel si el borde interior cae en las partes fijas (antes de las tiras).
            int il = interior.x - ox, it = interior.y - oy;
            int ir = w - (interior.xMax - ox), ib = h - (interior.yMax - oy);
            if (interior.width <= 0 || interior.height <= 0)
                log.AppendLine($"  interior {name}: sin medir en la receta, sin relleno.");
            else if (il <= 0 || it <= 0 || ir <= 0 || ib <= 0 ||
                     il >= left || it >= top || ir >= w - right - s || ib >= h - bottom - s)
                log.AppendLine($"  ⚠ interior {name}: márgenes ({il},{it},{ir},{ib}) fuera de las partes fijas " +
                               $"(tiras x={left}/{right}, y={top}/{bottom}); sin relleno.");
            else
            {
                rects.Add(new SpriteRect
                {
                    name = $"{name}_Fill",
                    rect = new Rect(il, ib, w - il - ir, h - it - ib),
                    border = Vector4.zero,
                });
                log.AppendLine($"  interior {name}: márgenes izq {il}, arriba {it}, der {ir}, abajo {ib}");
            }

            return rects.ToArray();

            SpriteRect Quad(string n, int x, int yTop, int rw, int rh, int bl, int br, int bt, int bb) => new()
            {
                name = n,
                rect = new Rect(x, h - (yTop + rh), rw, rh),
                border = new Vector4(bl, bb, br, bt),
            };
        }

        private static long Diff(Color32 a, Color32 b)
        {
            // Premultiplicado: dos píxeles transparentes de distinto color no son "distintos".
            int ar = a.r * a.a, ag = a.g * a.a, ab = a.b * a.a;
            int br = b.r * b.a, bg = b.g * b.a, bb = b.b * b.a;
            return (System.Math.Abs(ar - br) + System.Math.Abs(ag - bg) + System.Math.Abs(ab - bb)) / 255 +
                   System.Math.Abs(a.a - b.a);
        }

        /// <summary>
        /// Importa como sprite(s) para UI: sin mipmaps, malla FullRect (UI Toolkit necesita el
        /// rectángulo entero para el 9-slice) y los bordes escritos en el propio sprite.
        /// </summary>
        private static Sprite[] Import(string path, SpriteRect[] rects, bool multiple)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = multiple ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            if (!multiple) importer.spriteBorder = rects[0].border;
            importer.SaveAndReimport();

            if (multiple)
            {
                var factories = new SpriteDataProviderFactories();
                factories.Init();
                var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();

                // Mismos GUID que la vez anterior si el nombre ya existía: las referencias sobreviven
                // a una regeneración.
                var previous = new Dictionary<string, GUID>();
                foreach (var r in provider.GetSpriteRects()) previous[r.name] = r.spriteID;

                foreach (var r in rects)
                {
                    r.spriteID = previous.TryGetValue(r.name, out var id) ? id : GUID.Generate();
                    r.alignment = SpriteAlignment.Center;
                    r.pivot = new Vector2(0.5f, 0.5f);
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

            var byName = new Dictionary<string, Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) byName[sprite.name] = sprite;

            var sprites = new Sprite[rects.Length];
            for (int i = 0; i < rects.Length; i++)
            {
                if (!multiple) sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                else byName.TryGetValue(rects[i].name, out sprites[i]);
            }

            return sprites;
        }
    }
}
