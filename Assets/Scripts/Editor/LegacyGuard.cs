using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.EditorTools
{
    /// <summary>
    /// Vigila que nada vivo use lo que está en <b>Legacy</b> (<c>Assets/Legacy/</c> y cualquier carpeta llamada
    /// <c>Legacy</c>, p. ej. <c>Resources/Legacy</c>). Legacy es lo que ya no se usa y se guarda por si acaso: si algo de
    /// ahí aparece en juego, es un aviso de que no estaba tan muerto.
    ///
    /// Dos comprobaciones:
    /// <list type="bullet">
    /// <item><b>En Play</b> (sólo editor, automático): al cargar cada escena mira sus dependencias, y cada pocos segundos
    /// lo que hay vivo (scripts, sprites, audio, animators, materiales — también lo que se instancia por código o sale
    /// de un pool). Cada uso nuevo se avisa en consola con <c>[Legacy]</c> y se apunta, una vez, en
    /// <c>docs/legacy-usage.log</c>. Si ese archivo no crece en semanas, Legacy no se usa.</item>
    /// <item><b>Tools ▸ RedMagic ▸ Legacy ▸ Comprobar qué usa Legacy</b>: recorre todos los assets de fuera y el código, y
    /// escribe <c>docs/legacy-report.md</c> con quién depende de qué.</item>
    /// </list>
    /// <see cref="Exempt"/>: lo que se guarda a propósito como referencia (Ice1) no cuenta como uso.
    /// </summary>
    [InitializeOnLoad]
    public static class LegacyGuard
    {
        public const string Root = "Assets/Legacy";
        private const string UsageLog = "docs/legacy-usage.log";
        private const string ReportPath = "docs/legacy-report.md";
        private const double ScanInterval = 3.0;

        /// <summary>Assets que se conservan como referencia: usarlos no es una alarma.</summary>
        private static readonly string[] Exempt = { "Assets/Scenes/Ice1.unity" };

        private static readonly HashSet<string> Reported = new HashSet<string>();
        private static readonly HashSet<string> CheckedScenes = new HashSet<string>();
        private static double _nextScan;

        /// <summary>True si la ruta está en Legacy.</summary>
        public static bool IsLegacyPath(string path) =>
            !string.IsNullOrEmpty(path) &&
            (path.StartsWith(Root + "/", StringComparison.Ordinal) || path.Contains("/Legacy/"));

        static LegacyGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.update += OnUpdate;
        }

        // ================================================================ en Play

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            Reported.Clear();
            CheckedScenes.Clear();
            _nextScan = EditorApplication.timeSinceStartup + 1.0;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(scene.path) || !CheckedScenes.Add(scene.path)) return;
            if (IsLegacyPath(scene.path)) { Report(scene.path, "escena cargada", scene.path); return; }
            if (Exempt.Contains(scene.path)) return;

            foreach (string dep in AssetDatabase.GetDependencies(scene.path, true))
                if (IsLegacyPath(dep)) Report(dep, "dependencia de la escena", scene.path);
        }

        private static void OnUpdate()
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup < _nextScan) return;
            _nextScan = EditorApplication.timeSinceStartup + ScanInterval;

            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var script = MonoScript.FromMonoBehaviour(mb);
                Check(script, mb);
            }
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r is SpriteRenderer sr) Check(sr.sprite, r);
                Check(r.sharedMaterial, r);
            }
            foreach (var a in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                Check(a.clip, a);
            foreach (var an in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                Check(an.runtimeAnimatorController, an);
        }

        private static void Check(UnityEngine.Object asset, Component user)
        {
            if (asset == null) return;
            string path = AssetDatabase.GetAssetPath(asset);
            if (!IsLegacyPath(path)) return;
            string scene = user.gameObject.scene.path;
            if (Exempt.Contains(scene)) return;
            Report(path, $"en uso por '{PathOf(user.transform)}'", string.IsNullOrEmpty(scene) ? user.gameObject.scene.name : scene);
        }

        private static void Report(string legacyAsset, string how, string where)
        {
            if (!Reported.Add(legacyAsset + "|" + where)) return;
            Debug.LogWarning($"[Legacy] Se está usando algo de Legacy: {legacyAsset} — {how} ({where}). " +
                             "Si es correcto que se use, sácalo de Legacy; si no, quita la referencia.");
            try
            {
                string key = $"{legacyAsset} | {where}";
                var lines = File.Exists(UsageLog) ? File.ReadAllLines(UsageLog) : Array.Empty<string>();
                if (lines.Any(l => l.Contains(key))) return;   // ya apuntado: una línea por asset y sitio
                File.AppendAllText(UsageLog, $"{DateTime.Now:yyyy-MM-dd HH:mm} | {key} | {how}\n");
            }
            catch (Exception e) { Debug.LogWarning($"[Legacy] No se pudo escribir {UsageLog}: {e.Message}"); }
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        // ================================================================ informe completo

        [MenuItem("Tools/RedMagic/Legacy/Comprobar qué usa Legacy")]
        public static void WriteReport()
        {
            var assetHits = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            string[] all = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !IsLegacyPath(p) && !Exempt.Contains(p)
                            && !p.StartsWith("Assets/_Recovery/", StringComparison.Ordinal)   // copia de un crash, fuera de git
                            && !AssetDatabase.IsValidFolder(p) && !p.EndsWith(".cs", StringComparison.Ordinal))
                .ToArray();

            for (int i = 0; i < all.Length; i++)
            {
                if (i % 200 == 0 && EditorUtility.DisplayCancelableProgressBar("Legacy", all[i], i / (float)all.Length)) break;
                foreach (string dep in AssetDatabase.GetDependencies(all[i], false))
                {
                    if (!IsLegacyPath(dep)) continue;
                    if (!assetHits.TryGetValue(all[i], out var set)) assetHits[all[i]] = set = new SortedSet<string>(StringComparer.Ordinal);
                    set.Add(dep);
                }
            }
            EditorUtility.ClearProgressBar();

            // Código: tipos declarados SÓLO en Legacy (un nombre que también existe fuera, p. ej. dos SheetSlicer en
            // namespaces distintos, no se puede atribuir) que aparecen en código de fuera, sin contar comentarios.
            var scripts = AssetDatabase.FindAssets("t:MonoScript").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p.EndsWith(".cs", StringComparison.Ordinal)
                            && !p.StartsWith("Assets/_Recovery/", StringComparison.Ordinal))
                .ToList();
            var outsideCode = scripts.Where(p => !IsLegacyPath(p)).ToDictionary(p => p, p => StripComments(File.ReadAllText(p)));
            var outsideTypes = new HashSet<string>(outsideCode.Values.SelectMany(DeclaredTypes));
            // El tipo principal de cada script (el que se llama como el archivo): los anidados tienen nombres
            // genéricos (Mode, Strip…) que coinciden con cualquier palabra.
            var legacyTypes = scripts.Where(IsLegacyPath)
                .Select(p => (name: Path.GetFileNameWithoutExtension(p), declared: DeclaredTypes(File.ReadAllText(p))))
                .Where(s => s.declared.Contains(s.name)).Select(s => s.name)
                .Where(t => !outsideTypes.Contains(t)).Distinct().ToList();
            var codeHits = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            if (legacyTypes.Count > 0)
            {
                var pattern = new Regex(@"\b(" + string.Join("|", legacyTypes.Select(Regex.Escape)) + @")\b");
                foreach (var (cs, code) in outsideCode.Select(kv => (kv.Key, kv.Value)))
                {
                    foreach (Match m in pattern.Matches(code))
                    {
                        if (!codeHits.TryGetValue(cs, out var set)) codeHits[cs] = set = new SortedSet<string>(StringComparer.Ordinal);
                        set.Add(m.Value);
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Qué usa Legacy — {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine("Generado por *Tools ▸ RedMagic ▸ Legacy ▸ Comprobar qué usa Legacy* (`LegacyGuard`). Exentos: " +
                          string.Join(", ", Exempt) + ".");
            sb.AppendLine();
            sb.AppendLine($"## Assets de fuera que referencian Legacy ({assetHits.Count})");
            sb.AppendLine();
            foreach (var kv in assetHits) sb.AppendLine($"- `{kv.Key}` → {string.Join(", ", kv.Value.Select(v => $"`{v}`"))}");
            sb.AppendLine();
            sb.AppendLine($"## Código de fuera que nombra clases de Legacy ({codeHits.Count})");
            sb.AppendLine();
            foreach (var kv in codeHits) sb.AppendLine($"- `{kv.Key}` → {string.Join(", ", kv.Value)}");
            File.WriteAllText(ReportPath, sb.ToString());

            Debug.Log($"[Legacy] Informe en {ReportPath}: {assetHits.Count} assets y {codeHits.Count} scripts de fuera usan Legacy.");
        }

        private static readonly Regex Declaration = new Regex(@"\b(?:class|struct|enum|interface)\s+([A-Za-z_]\w*)");
        private static readonly Regex Comments = new Regex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline);

        private static IEnumerable<string> DeclaredTypes(string code) =>
            Declaration.Matches(StripComments(code)).Cast<Match>().Select(m => m.Groups[1].Value);

        private static string StripComments(string code) => Comments.Replace(code, "");
    }
}
