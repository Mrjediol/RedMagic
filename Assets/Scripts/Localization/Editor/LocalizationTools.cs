using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RedMagic.Bosses;
using RedMagic.Economy;
using RedMagic.Items;
using RedMagic.Settings;
using RedMagic.UI;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Localization.EditorTools
{
    /// <summary>
    /// Herramientas de la localización (<see cref="Loc"/>). Guía: <c>Assets/_Pipeline/LOCALIZATION_PIPELINE.md</c>.
    ///  - <b>Sincronizar textos de assets</b>: a cada item / arma / pasiva / jefe sin <c>textKey</c> le da
    ///    uno (del nombre visible) y añade al fichero del idioma por defecto las claves que falten con el
    ///    texto del asset; lo mismo para los nodos de <c>UpgradeTree</c> y los tramos de <c>SynergyConfig</c>.
    ///    Nunca sobrescribe una clave existente. Luego avisa de lo que falta en los demás idiomas.
    ///  - <b>Auditar claves</b>: busca todas las claves que usa el juego (código, UXML <c>#clave</c>,
    ///    assets) y lista las que falten en cada idioma.
    /// </summary>
    public static class LocalizationTools
    {
        private const string Menu = "Tools/RedMagic/Localización/";
        private const string LanguageFolder = "Assets/Resources/" + Loc.ResourceFolder;
        private static readonly string[] CodeFolders = { "Assets/Scripts", "Assets/Ui" };

        // Una línea de código "habla de claves" si contiene alguno de estos marcadores; de ella se
        // sacan los literales con forma de clave (a.b, minúsculas).
        private static readonly string[] CodeMarkers =
            { "Loc.", "LocalizedUi.Bind", "ShowKey", "PromptKey", "\"legend.", "Key = \"", "labelKey" };

        private static readonly Regex KeyLiteral = new("\"([a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)+)\"", RegexOptions.Compiled);
        private static readonly Regex UxmlKey = new("(?:text|label|tooltip)=\"#([^\"]+)\"", RegexOptions.Compiled);

        // ------------------------------------------------------------------ menú

        [MenuItem(Menu + "Sincronizar textos de assets")]
        public static void SyncAssetTexts()
        {
            var texts = new List<(string key, string value)>();
            int keysAssigned = 0;

            keysAssigned += Collect<ItemDefinition>("item", texts, ("displayName", "name"), ("description", "description"));
            keysAssigned += Collect<WeaponDefinition>("weapon", texts, ("displayName", "name"), ("description", "description"));
            keysAssigned += Collect<LegendaryPassive>("passive", texts, ("displayName", "name"), ("description", "description"),
                                                      ("upgradeDescription", "upgrade"));
            keysAssigned += Collect<BossDefinition>("boss", texts, ("displayName", "name"), ("title", "title"),
                                                    ("description", "description"));
            CollectUpgradeTree(texts);
            CollectSynergies(texts);

            var defaultCode = GameSettingsConfig.Instance.defaultLanguage;
            int added = AppendMissing(defaultCode, texts);
            AssetDatabase.SaveAssets();
            Loc.Reload();

            Debug.Log($"[Localización] Sincronizado: {keysAssigned} textKey nuevos, {added} claves añadidas a " +
                      $"{defaultCode}.txt ({texts.Count} textos de assets en total).");
            Audit();
        }

        [MenuItem(Menu + "Auditar claves")]
        public static void Audit()
        {
            var used = new SortedDictionary<string, string>(StringComparer.Ordinal); // clave → dónde
            ScanCode(used);
            ScanUxml(used);
            foreach (var (key, _) in AssetTexts()) used.TryAdd(key, "asset");
            foreach (var preset in GameSettingsConfig.Instance.resolutionPresets)
                if (!string.IsNullOrEmpty(preset.labelKey)) used.TryAdd(preset.labelKey, "GameSettings");
            var cinematic = Resources.Load<PassiveDropCinematicSettings>("PassiveDropCinematicSettings");
            if (cinematic != null)
            {
                used.TryAdd(cinematic.headerKey, "PassiveDropCinematicSettings");
                used.TryAdd(cinematic.continueKey, "PassiveDropCinematicSettings");
            }

            var languages = ReadAll();
            var report = new StringBuilder();
            int problems = 0;

            foreach (var (code, table) in languages)
            {
                var missing = used.Keys.Where(k => !table.ContainsKey(k)).ToList();
                problems += missing.Count;
                if (missing.Count == 0) continue;

                report.AppendLine($"\n{code}.txt — faltan {missing.Count}:");
                foreach (var key in missing) report.AppendLine($"  {key}   ({used[key]})");
            }

            var defaultCode = GameSettingsConfig.Instance.defaultLanguage;
            if (languages.TryGetValue(defaultCode, out var defaults))
            {
                var unused = defaults.Keys.Where(k => !k.StartsWith("@") && !used.ContainsKey(k)).ToList();
                if (unused.Count > 0)
                    report.AppendLine($"\nEn {defaultCode}.txt sin uso detectado ({unused.Count}, puede ser una clave " +
                                      "compuesta en tiempo de ejecución): " + string.Join(", ", unused));
            }

            string summary = $"[Localización] Auditoría: {used.Count} claves en uso, {languages.Count} idioma(s) " +
                             $"({string.Join(", ", languages.Keys)}), {problems} ausencia(s).";
            if (problems > 0) Debug.LogWarning(summary + report);
            else Debug.Log(summary + report);
        }

        [MenuItem("Tools/RedMagic/Ajustes/Crear asset GameSettings")]
        public static void CreateGameSettingsAsset()
        {
            const string path = "Assets/Resources/" + GameSettingsConfig.ResourcePath + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<GameSettingsConfig>(path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<GameSettingsConfig>();
                AssetDatabase.CreateAsset(existing, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Ajustes] Creado {path}.");
            }
            Selection.activeObject = existing;
        }

        // ------------------------------------------------------------------ assets → textos

        /// <summary>Todos los textos de assets que el juego traduce (con su clave), sin tocar nada.</summary>
        private static IEnumerable<(string key, string value)> AssetTexts()
        {
            var texts = new List<(string key, string value)>();
            Collect<ItemDefinition>("item", texts, false, ("displayName", "name"), ("description", "description"));
            Collect<WeaponDefinition>("weapon", texts, false, ("displayName", "name"), ("description", "description"));
            Collect<LegendaryPassive>("passive", texts, false, ("displayName", "name"), ("description", "description"),
                                      ("upgradeDescription", "upgrade"));
            Collect<BossDefinition>("boss", texts, false, ("displayName", "name"), ("title", "title"),
                                    ("description", "description"));
            CollectUpgradeTree(texts);
            CollectSynergies(texts);
            return texts;
        }

        private static int Collect<T>(string prefix, List<(string key, string value)> texts,
                                      params (string field, string suffix)[] fields) where T : ScriptableObject =>
            Collect<T>(prefix, texts, true, fields);

        /// <returns>Cuántos assets recibieron un textKey nuevo.</returns>
        private static int Collect<T>(string prefix, List<(string key, string value)> texts, bool assignKeys,
                                      params (string field, string suffix)[] fields) where T : ScriptableObject
        {
            int assigned = 0;
            var taken = new HashSet<string>();
            var assets = AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                                      .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                                      .Where(a => a != null && !AssetDatabase.GetAssetPath(a).Contains("/Legacy/"))
                                      .ToList();

            // Primero las claves ya asignadas (no se mueven), luego las nuevas sin chocar con ellas.
            foreach (var asset in assets)
            {
                var key = new SerializedObject(asset).FindProperty("textKey")?.stringValue;
                if (!string.IsNullOrEmpty(key)) taken.Add(key);
            }

            foreach (var asset in assets)
            {
                var so = new SerializedObject(asset);
                var keyProp = so.FindProperty("textKey");
                if (keyProp == null) continue;

                if (string.IsNullOrEmpty(keyProp.stringValue))
                {
                    if (!assignKeys) continue;
                    var nameProp = so.FindProperty(fields[0].field);
                    var baseKey = $"{prefix}.{Slug(nameProp != null && !string.IsNullOrWhiteSpace(nameProp.stringValue) ? nameProp.stringValue : asset.name)}";
                    var key = baseKey;
                    for (int n = 2; taken.Contains(key); n++) key = $"{baseKey}_{n}";

                    keyProp.stringValue = key;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    taken.Add(key);
                    assigned++;
                }

                foreach (var (field, suffix) in fields)
                {
                    var value = so.FindProperty(field)?.stringValue;
                    if (!string.IsNullOrWhiteSpace(value)) texts.Add(($"{keyProp.stringValue}.{suffix}", value));
                }
            }

            return assigned;
        }

        private static void CollectUpgradeTree(List<(string key, string value)> texts)
        {
            var tree = Resources.Load<UpgradeTree>("UpgradeTree");
            if (tree == null) return;
            for (int r = 0; r < tree.Rows; r++)
            for (int c = 0; c < tree.Columns; c++)
            {
                var node = tree.NodeAt(r, c);
                if (node == null || string.IsNullOrEmpty(node.id)) continue;
                if (!string.IsNullOrWhiteSpace(node.title)) texts.Add(($"upgrade.{node.id}.title", node.title));
                if (!string.IsNullOrWhiteSpace(node.description)) texts.Add(($"upgrade.{node.id}.description", node.description));
            }
        }

        private static void CollectSynergies(List<(string key, string value)> texts)
        {
            var config = SynergyConfig.Instance;
            if (config == null) return;
            foreach (BuildTag tag in Enum.GetValues(typeof(BuildTag)))
            for (int tier = 1; tier <= 3; tier++)
            {
                var raw = config.RawTier(tag, tier);
                if (!string.IsNullOrWhiteSpace(raw)) texts.Add((SynergyConfig.TierKey(tag, tier), raw));
            }
        }

        /// <summary>"Árbol Ancestral" → "arbol_ancestral".</summary>
        public static string Slug(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
            }
            return Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
        }

        // ------------------------------------------------------------------ ficheros de idioma

        private static string PathFor(string code) => $"{LanguageFolder}/{code}.txt";

        private static Dictionary<string, Dictionary<string, string>> ReadAll()
        {
            var all = new SortedDictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            if (!Directory.Exists(LanguageFolder)) return new Dictionary<string, Dictionary<string, string>>();
            foreach (var file in Directory.GetFiles(LanguageFolder, "*.txt"))
                all[Path.GetFileNameWithoutExtension(file)] = Read(file);
            return new Dictionary<string, Dictionary<string, string>>(all);
        }

        private static Dictionary<string, string> Read(string path)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(path)) return table;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.TrimStart();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                table[raw.Substring(0, eq).Trim()] = raw.Substring(eq + 1).Trim();
            }
            return table;
        }

        /// <returns>Cuántas claves se añadieron.</returns>
        private static int AppendMissing(string code, List<(string key, string value)> texts)
        {
            var path = PathFor(code);
            var existing = Read(path);
            var lines = new List<string>();
            var seen = new HashSet<string>(existing.Keys);

            foreach (var (key, value) in texts)
            {
                if (!seen.Add(key)) continue;
                lines.Add($"{key} = {Escape(value)}");
            }
            if (lines.Count == 0) return 0;

            Directory.CreateDirectory(LanguageFolder);
            var header = $"\n# --- sincronizado desde assets ({DateTime.Now:yyyy-MM-dd})\n";
            File.AppendAllText(path, header + string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            return lines.Count;
        }

        private static string Escape(string value) => value.Replace("\r", "").Replace("\n", "\\n").Trim();

        // ------------------------------------------------------------------ escaneo

        private static void ScanCode(IDictionary<string, string> used)
        {
            foreach (var folder in CodeFolders)
            foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                var norm = file.Replace('\\', '/');
                if (norm.Contains("/Editor/") || norm.Contains("/Legacy/")) continue;

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//")) continue;
                    if (!CodeMarkers.Any(line.Contains)) continue;

                    foreach (Match m in KeyLiteral.Matches(line))
                        used.TryAdd(m.Groups[1].Value, $"{Path.GetFileName(file)}:{i + 1}");
                }
            }
        }

        private static void ScanUxml(IDictionary<string, string> used)
        {
            foreach (var file in Directory.GetFiles("Assets/Ui", "*.uxml", SearchOption.AllDirectories))
            foreach (Match m in UxmlKey.Matches(File.ReadAllText(file)))
                used.TryAdd(m.Groups[1].Value, Path.GetFileName(file));
        }
    }
}
