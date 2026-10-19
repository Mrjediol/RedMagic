using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace RedMagic.Test.Optimizacion
{
    /// <summary>Camino de dependencias más corto entre dos assets (por qué una escena carga una textura).</summary>
    public static class DependencyPath
    {
        public static string Find(string from, string to)
        {
            var prev = new Dictionary<string, string> { [from] = null };
            var q = new Queue<string>(); q.Enqueue(from);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == to) break;
                foreach (var d in AssetDatabase.GetDependencies(cur, false))
                    if (!prev.ContainsKey(d) && (!d.EndsWith(".unity") || d == to)) { prev[d] = cur; q.Enqueue(d); }
            }
            if (!prev.ContainsKey(to)) return "(sin camino)";
            var path = new List<string>();
            for (var c = to; c != null; c = prev[c]) path.Add(c);
            path.Reverse();
            return string.Join("\n  → ", path);
        }

        /// <summary>Para cada dependencia directa de la escena: cuántas texturas y MB arrastra (aprox.).</summary>
        public static string Heavy(string scene)
        {
            var lines = new List<(string, int)>();
            foreach (var d in AssetDatabase.GetDependencies(scene, false))
            {
                if (d.EndsWith(".unity")) continue;
                var n = TextureAudit.SceneDeps(d).Count(p => p.EndsWith(".png") || p.EndsWith(".jpg") || p.EndsWith(".psd"));
                lines.Add((d, n));
            }
            return string.Join("\n", lines.OrderByDescending(l => l.Item2).Take(15).Select(l => $"{l.Item2,4}  {l.Item1}"));
        }
    }
}
