using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Levels
{
    /// <summary>Punto de un contorno ya redondeado. <see cref="normal"/> apunta hacia DENTRO de la roca.</summary>
    public struct RimPoint
    {
        public Vector2 position;
        public Vector2 tangent;
        public Vector2 normal;
        public float distance; // longitud recorrida desde el inicio del contorno
    }

    /// <summary>
    /// Geometría del terreno a partir de una cuadrícula sólido/vacío (1 celda = 1 unidad, y hacia arriba).
    /// Es el mismo algoritmo que el boceto web (<c>Web/ParallaxTest/boceto-nivel.html</c>): contornos de los
    /// huecos con la roca a la derecha, esquinas redondeadas con una curva cuadrática de radio fijo, y el
    /// relleno recortado con esas mismas curvas para que la roca no asome por fuera del borde.
    /// Fuera de la cuadrícula cuenta como roca (el marco del nivel).
    /// </summary>
    public static class LevelShape
    {
        // ------------------------------------------------------------------ contornos

        /// <summary>
        /// Contornos de los huecos: cada uno es una lista de vértices de la cuadrícula, recorrida con la roca a la
        /// derecha. En un vértice "pinzado" (dos celdas sólidas en diagonal) se gira primero a la izquierda: así
        /// cada hueco queda en su propio contorno en vez de cruzarse en ocho.
        /// </summary>
        public static List<List<Vector2Int>> Loops(Func<int, int, bool> solid, int width, int height)
        {
            var outgoing = new Dictionary<Vector2Int, List<Vector2Int>>();
            void Add(Vector2Int a, Vector2Int b)
            {
                if (!outgoing.TryGetValue(a, out var list)) outgoing[a] = list = new List<Vector2Int>(1);
                list.Add(b);
            }

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!solid(x, y)) continue;
                if (!solid(x, y + 1)) Add(new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1)); // aire encima
                if (!solid(x + 1, y)) Add(new Vector2Int(x + 1, y + 1), new Vector2Int(x + 1, y)); // aire a la derecha
                if (!solid(x, y - 1)) Add(new Vector2Int(x + 1, y), new Vector2Int(x, y));         // aire debajo
                if (!solid(x - 1, y)) Add(new Vector2Int(x, y), new Vector2Int(x, y + 1));         // aire a la izquierda
            }

            var loops = new List<List<Vector2Int>>();
            while (outgoing.Count > 0)
            {
                Vector2Int start = default;
                foreach (var key in outgoing.Keys) { start = key; break; }

                var loop = new List<Vector2Int>();
                Vector2Int current = start, dir = Vector2Int.zero;
                for (int guard = 0; guard < 1_000_000; guard++)
                {
                    if (!outgoing.TryGetValue(current, out var options) || options.Count == 0) break;
                    int pick = 0;
                    if (options.Count > 1 && dir != Vector2Int.zero)
                    {
                        int best = int.MinValue;
                        for (int i = 0; i < options.Count; i++)
                        {
                            Vector2Int d = options[i] - current;
                            int cross = dir.x * d.y - dir.y * d.x; // >0 = giro a la izquierda
                            int score = cross > 0 ? 2 : cross == 0 ? 1 : 0;
                            if (score > best) { best = score; pick = i; }
                        }
                    }

                    Vector2Int next = options[pick];
                    options.RemoveAt(pick);
                    if (options.Count == 0) outgoing.Remove(current);
                    loop.Add(current);
                    dir = next - current;
                    current = next;
                    if (current == start) break;
                }

                RemoveCollinear(loop);
                if (loop.Count >= 3) loops.Add(loop);
            }
            return loops;
        }

        private static void RemoveCollinear(List<Vector2Int> pts)
        {
            for (int i = pts.Count - 1; i >= 0 && pts.Count > 2; i--)
            {
                int n = pts.Count;
                Vector2Int a = pts[(i - 1 + n) % n], b = pts[i], c = pts[(i + 1) % n];
                Vector2Int d1 = b - a, d2 = c - b;
                if (d1.x * d2.y - d1.y * d2.x == 0) pts.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ esquinas redondeadas

        /// <summary>
        /// Contorno redondeado: en cada esquina, curva cuadrática de p1 a p2 (a <paramref name="radius"/> de la
        /// esquina sobre cada lado) con la esquina como control. Los tramos rectos se trocean cada
        /// <paramref name="step"/> unidades (usa un valor grande para no trocearlos, p. ej. para el collider).
        /// </summary>
        public static List<RimPoint> Rounded(List<Vector2Int> loop, float radius, float step, int arcSegments)
        {
            int n = loop.Count;
            var p1 = new Vector2[n];
            var p2 = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = loop[(i - 1 + n) % n], b = loop[i], c = loop[(i + 1) % n];
                float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
                Vector2 d1 = (b - a) / l1, d2 = (c - b) / l2;
                float r = Mathf.Min(radius, l1 * 0.5f, l2 * 0.5f);
                p1[i] = b - d1 * r;
                p2[i] = b + d2 * r;
            }

            var pts = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                Vector2 from = p2[(i - 1 + n) % n], to = p1[i];
                int k = Mathf.Max(1, Mathf.RoundToInt((to - from).magnitude / step));
                for (int j = 0; j < k; j++) pts.Add(Vector2.Lerp(from, to, j / (float)k));
                Vector2 b = loop[i];
                for (int j = 0; j <= arcSegments; j++)
                {
                    float t = j / (float)arcSegments, u = 1f - t;
                    pts.Add(u * u * p1[i] + 2f * u * t * b + t * t * p2[i]);
                }
            }

            var result = new List<RimPoint>(pts.Count);
            int m = pts.Count;
            float s = 0f;
            for (int i = 0; i < m; i++)
            {
                Vector2 prev = pts[(i - 1 + m) % m];
                Vector2 t = (pts[(i + 2) % m] - pts[(i - 2 + m) % m]);
                t = t.sqrMagnitude > 1e-8f ? t.normalized : Vector2.right;
                s += (pts[i] - prev).magnitude;
                result.Add(new RimPoint
                {
                    position = pts[i],
                    tangent = t,
                    normal = new Vector2(t.y, -t.x), // derecha del avance = roca
                    distance = s,
                });
            }
            return result;
        }

        // ------------------------------------------------------------------ relleno

        /// <summary>
        /// Polígonos convexos (abanico desde el primer punto) que cubren la roca con las esquinas ya redondeadas:
        /// cada celda sólida es su cuadrado con las esquinas convexas recortadas por la curva, y en cada esquina
        /// cóncava se añade el "chaflán" curvo que el contorno mete en la celda vacía.
        /// </summary>
        public static void FillPolygons(Func<int, int, bool> solid, int width, int height, float radius,
                                        int arcSegments, List<List<Vector2>> polygons)
        {
            var corners = new Vector2Int[4];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!solid(x, y)) continue;
                corners[0] = new Vector2Int(x, y);
                corners[1] = new Vector2Int(x + 1, y);
                corners[2] = new Vector2Int(x + 1, y + 1);
                corners[3] = new Vector2Int(x, y + 1);

                var poly = new List<Vector2> { new Vector2(x + 0.5f, y + 0.5f) }; // centro: abanico
                var ring = new List<Vector2>();
                for (int c = 0; c < 4; c++)
                {
                    Vector2Int v = corners[c];
                    if (!IsConvexFor(solid, v, x, y))
                    {
                        ring.Add(v);
                        continue;
                    }
                    Vector2 prev = corners[(c + 3) % 4], next = corners[(c + 1) % 4];
                    Vector2 a = (Vector2)v + (prev - v) * radius, b = (Vector2)v + (next - v) * radius;
                    for (int j = 0; j <= arcSegments; j++)
                    {
                        float t = j / (float)arcSegments, u = 1f - t;
                        ring.Add(u * u * a + 2f * u * t * (Vector2)v + t * t * b);
                    }
                }
                poly.AddRange(ring);
                poly.Add(ring[0]);
                polygons.Add(poly);
            }

            // Chaflanes de las esquinas cóncavas (tres celdas sólidas alrededor del vértice).
            for (int vy = 1; vy < height; vy++)
            for (int vx = 1; vx < width; vx++)
            {
                bool sw = solid(vx - 1, vy - 1), se = solid(vx, vy - 1), nw = solid(vx - 1, vy), ne = solid(vx, vy);
                int count = (sw ? 1 : 0) + (se ? 1 : 0) + (nw ? 1 : 0) + (ne ? 1 : 0);
                if (count != 3) continue;
                Vector2 v = new Vector2(vx, vy);
                // Lados de la celda vacía que salen del vértice.
                Vector2 dx = !ne || !se ? Vector2.right : Vector2.left;
                Vector2 dy = !ne || !nw ? Vector2.up : Vector2.down;
                Vector2 a = v + dx * radius, b = v + dy * radius;
                var poly = new List<Vector2> { v };
                for (int j = 0; j <= arcSegments; j++)
                {
                    float t = j / (float)arcSegments, u = 1f - t;
                    poly.Add(u * u * a + 2f * u * t * v + t * t * b);
                }
                polygons.Add(poly);
            }
        }

        /// <summary>Si el vértice <paramref name="v"/> es una esquina convexa de la celda (x, y).</summary>
        private static bool IsConvexFor(Func<int, int, bool> solid, Vector2Int v, int x, int y)
        {
            bool sw = solid(v.x - 1, v.y - 1), se = solid(v.x, v.y - 1), nw = solid(v.x - 1, v.y), ne = solid(v.x, v.y);
            int count = (sw ? 1 : 0) + (se ? 1 : 0) + (nw ? 1 : 0) + (ne ? 1 : 0);
            if (count == 1) return true;
            // Pinzada: dos sólidas en diagonal → cada una tiene ahí su esquina convexa.
            return count == 2 && sw == ne;
        }
    }
}
