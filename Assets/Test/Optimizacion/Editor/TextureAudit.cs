using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace RedMagic.Test.Optimizacion
{
    /// <summary>
    /// Auditoría de texturas (tarea optimizacion-arte-carga, paso 1): recorre todas las texturas del proyecto con el destino
    /// activo (Android) y escribe <c>docs/auditoria-texturas.md</c>: formato real importado, memoria (calculada del formato y los
    /// mips), si el tamaño no es múltiplo de 4 (sin compresión en ETC2/DXT), píxeles por unidad de los sprites, y cuánto arrastra
    /// cada escena de la build. No cambia nada.
    /// </summary>
    public static class TextureAudit
    {
        private const string ReportPath = "docs/auditoria-texturas.md";
        private const int PpuBudget = 128;          // ~2× los 54 px/unidad que ve la cámara (20 u en 1080 px)

        private class Row
        {
            public string path, folder, format, type;
            public int w, h, srcW, srcH, mips, maxSize;
            public long bytes;
            public float ppu;
            public bool mult4, compressed, sprite, mip;
        }

        [MenuItem("Tools/RedMagic/Optimización/Auditar texturas")]
        public static void Run() => Debug.Log(Write());

        public static string Write()
        {
            var rows = new List<Row>();
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                imp.GetSourceTextureWidthAndHeight(out int sw, out int sh);
                var r = new Row
                {
                    path = path, folder = FolderOf(path), w = tex.width, h = tex.height, srcW = sw, srcH = sh,
                    mips = tex.mipmapCount, format = tex.format.ToString(), type = imp.textureType.ToString(),
                    sprite = imp.textureType == TextureImporterType.Sprite, mip = imp.mipmapEnabled,
                    ppu = imp.textureType == TextureImporterType.Sprite ? imp.spritePixelsPerUnit : 0,
                    maxSize = imp.GetPlatformTextureSettings("Android").overridden ? imp.GetPlatformTextureSettings("Android").maxTextureSize : imp.maxTextureSize,
                    mult4 = sw % 4 == 0 && sh % 4 == 0,
                    compressed = GraphicsFormatUtility.IsCompressedFormat(tex.graphicsFormat),
                };
                for (int m = 0; m < Mathf.Max(1, tex.mipmapCount); m++)
                    r.bytes += GraphicsFormatUtility.ComputeMipmapSize(Mathf.Max(1, tex.width >> m), Mathf.Max(1, tex.height >> m), tex.graphicsFormat);
                rows.Add(r);
                if (i % 150 == 0) { EditorUtility.UnloadUnusedAssetsImmediate(); }
            }
            EditorUtility.UnloadUnusedAssetsImmediate();

            var byPath = rows.ToDictionary(r => r.path);
            var live = rows.Where(r => !IsLegacy(r.path)).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("# Auditoría de texturas");
            sb.AppendLine();
            sb.AppendLine($"Generado por *Tools ▸ RedMagic ▸ Optimización ▸ Auditar texturas* el {System.DateTime.Now:yyyy-MM-dd HH:mm}, destino **{EditorUserBuildSettings.activeBuildTarget}**. " +
                          "Memoria = formato importado real × mips (lo que ocupa en GPU). «Legacy» = `Assets/Legacy`, `Assets/_Reference`, `Assets/Test`, packs vendorizados.");
            sb.AppendLine();
            sb.AppendLine("## Resumen");
            sb.AppendLine();
            sb.AppendLine("| | Texturas | Memoria | No múltiplo de 4 | Sin comprimir | PPU > " + PpuBudget + " |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|");
            foreach (var (name, set) in new[] { ("Todo", rows), ("Sin legacy/test/packs", live) })
                sb.AppendLine($"| {name} | {set.Count} | {Mb(set.Sum(r => r.bytes))} | {set.Count(r => !r.mult4)} | {set.Count(r => !r.compressed)} ({Mb(set.Where(r => !r.compressed).Sum(r => r.bytes))}) | {set.Count(r => r.ppu > PpuBudget)} |");
            sb.AppendLine();
            sb.AppendLine("Formatos (sin legacy): " + string.Join(", ", live.GroupBy(r => r.format).OrderByDescending(g => g.Sum(r => r.bytes))
                .Select(g => $"`{g.Key}` {g.Count()} · {Mb(g.Sum(r => r.bytes))}")));
            sb.AppendLine();

            sb.AppendLine("## Por escena de la build (texturas que arrastra al cargarse)");
            sb.AppendLine();
            sb.AppendLine("| Escena | Texturas | Memoria | Sin comprimir | Las 3 más pesadas |");
            sb.AppendLine("|---|---:|---:|---:|---|");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
            foreach (var extra in new[] { "Assets/Scenes/NivelHielo_1000.unity" }) if (File.Exists(extra) && !scenes.Contains(extra)) scenes.Add(extra);
            foreach (var sc in scenes)
            {
                var deps = SceneDeps(sc).Where(byPath.ContainsKey).Select(p => byPath[p]).ToList();
                var top = string.Join("; ", deps.OrderByDescending(r => r.bytes).Take(3).Select(r => $"{Path.GetFileName(r.path)} {Mb(r.bytes)}"));
                sb.AppendLine($"| {Path.GetFileNameWithoutExtension(sc)} | {deps.Count} | {Mb(deps.Sum(r => r.bytes))} | {deps.Count(r => !r.compressed)} | {top} |");
            }
            sb.AppendLine();

            sb.AppendLine("## Por carpeta (sin legacy, las 25 que más ocupan)");
            sb.AppendLine();
            sb.AppendLine("| Carpeta | Texturas | Memoria | No múltiplo de 4 | Sin comprimir |");
            sb.AppendLine("|---|---:|---:|---:|---:|");
            foreach (var g in live.GroupBy(r => r.folder).OrderByDescending(g => g.Sum(r => r.bytes)).Take(25))
                sb.AppendLine($"| `{g.Key}` | {g.Count()} | {Mb(g.Sum(r => r.bytes))} | {g.Count(r => !r.mult4)} | {g.Count(r => !r.compressed)} |");
            sb.AppendLine();

            sb.AppendLine("## Las 40 más pesadas (sin legacy)");
            sb.AppendLine();
            sb.AppendLine("| Textura | Origen | Importada | Formato | Memoria | ×4 | PPU | Mips |");
            sb.AppendLine("|---|---:|---:|---|---:|:-:|---:|:-:|");
            foreach (var r in live.OrderByDescending(r => r.bytes).Take(40))
                sb.AppendLine($"| `{r.path.Substring(7)}` | {r.srcW}×{r.srcH} | {r.w}×{r.h} | {r.format} | {Mb(r.bytes)} | {(r.mult4 ? "✓" : "✗")} | {(r.sprite ? r.ppu.ToString("0") : "")} | {(r.mip ? "sí" : "")} |");
            sb.AppendLine();
            sb.AppendLine("## Qué ganaría cada medida (estimación sobre lo no-legacy)");
            sb.AppendLine();
            long uncompressed = live.Where(r => !r.compressed).Sum(r => r.bytes);
            long notMult4 = live.Where(r => !r.compressed && !r.mult4).Sum(r => r.bytes);
            long mips = live.Where(r => r.mip && r.sprite).Sum(r => r.bytes) / 4;
            sb.AppendLine($"- Rellenar a múltiplo de 4 las que no se comprimen por eso: {Mb(notMult4)} → ~{Mb(notMult4 / 4)} (RGBA32 4 B/px → ETC2 RGBA 1 B/px).");
            sb.AppendLine($"- Todo lo que hoy no se comprime (por cualquier motivo): {Mb(uncompressed)} → ~{Mb(uncompressed / 4)}.");
            sb.AppendLine($"- Quitar mips de los sprites que los tienen: ~{Mb(mips)}.");
            sb.AppendLine($"- Sprites con PPU > {PpuBudget} (más resolución de la que se ve): {live.Count(r => r.ppu > PpuBudget)} texturas, {Mb(live.Where(r => r.ppu > PpuBudget).Sum(r => r.bytes))}; bajar al presupuesto reduce ~(PPU/{PpuBudget})² cada una.");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());
            return $"[TextureAudit] {rows.Count} texturas ({live.Count} sin legacy, {Mb(live.Sum(r => r.bytes))}) → {ReportPath}";
        }

        /// <summary>Dependencias de una escena sin entrar en otras escenas: un <c>SceneReference</c> guarda la escena como
        /// SceneAsset sólo en el editor; en la build no la carga.</summary>
        public static HashSet<string> SceneDeps(string scene)
        {
            var seen = new HashSet<string> { scene };
            var stack = new Stack<string>(); stack.Push(scene);
            while (stack.Count > 0)
                foreach (var d in AssetDatabase.GetDependencies(stack.Pop(), false))
                    if (!d.EndsWith(".unity") && seen.Add(d)) stack.Push(d);
            return seen;
        }

        private static bool IsLegacy(string p) =>
            p.StartsWith("Assets/Legacy/") || p.StartsWith("Assets/_Reference/") || p.StartsWith("Assets/Test/") || p.Contains("/Resources/Legacy/") ||
            p.StartsWith("Assets/Brackeys/") || p.StartsWith("Assets/Cainos/") || p.StartsWith("Assets/Dragon Warrior Files/");

        private static string FolderOf(string path)
        {
            var parts = Path.GetDirectoryName(path).Replace('\\', '/').Split('/');
            return string.Join("/", parts.Take(Mathf.Min(parts.Length, 4)));
        }

        private static string Mb(long bytes) => bytes >= 1 << 20 ? (bytes / 1048576f).ToString("0.0") + " MB" : (bytes / 1024f).ToString("0") + " KB";
    }
}
