using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Importa el .zip de "⬇ Exportar jefe completo" de la pestaña de jefes de la web:
    /// <c>boss-config.json</c> en la raíz + <c>Body/&lt;Animación&gt;/frame_000.png…</c> (el cuerpo) +
    /// <c>Art/&lt;libraryId&gt;/&lt;Estado&gt;/frame_000.png…</c> (proyectiles, efectos, avisos).
    ///
    /// Los frames van a <c>Assets/Art/Bosses/&lt;slug&gt;/Source/</c> y se quedan ahí: los clips y los
    /// prefabs apuntan a esos sprites. Reimportar sincroniza la carpeta (sobrescribe, y borra los
    /// frames que ya no vienen en el zip — si no, una animación con menos frames arrastraría los
    /// viejos). Después corre <see cref="BossConfigImporter"/> tal cual: las carpetas del JSON son
    /// relativas a esa misma carpeta.
    /// </summary>
    public static class BossBundleImporter
    {
        private const string ConfigFile = "boss-config.json";

        [MenuItem("Tools/Web/Import Config.../Importar jefe completo (.zip)...")]
        public static void ImportMenu()
        {
            string path = EditorUtility.OpenFilePanel("Selecciona el .zip del jefe (web ▸ Boss Creator)",
                                                      Application.dataPath, "zip");
            if (string.IsNullOrEmpty(path)) return;

            string summary = Import(path);
            Debug.Log(summary);
            EditorUtility.DisplayDialog("Importar jefe", summary, "OK");
        }

        public static string Import(string zipPath)
        {
            var report = new ConfigImportReport(Path.GetFileName(zipPath));
            string temp = null;

            try
            {
                if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
                    throw new ConfigImportException($"No existe el archivo '{zipPath}'.");

                temp = Path.Combine(Path.GetTempPath(), $"RedMagicBossImport_{Guid.NewGuid():N}");
                Directory.CreateDirectory(temp);
                ZipFile.ExtractToDirectory(zipPath, temp);

                string configPath = Path.Combine(temp, ConfigFile);
                if (!File.Exists(configPath))
                    throw new ConfigImportException($"El .zip no trae {ConfigFile} en la raíz — ¿es un jefe exportado de la web?");

                var root = JObject.Parse(File.ReadAllText(configPath));
                string displayName = ConfigJson.RequireString(root, "displayName", "BossConfig");
                string slug = BossConfigImporter.Slugify(displayName);
                string dest = BossConfigImporter.SourceFolderFor(slug);

                int copied = 0, removed = 0;
                foreach (var sub in new[] { "Body", "Art" })
                {
                    string from = Path.Combine(temp, sub);
                    string to = Path.Combine(Path.GetFullPath(dest), sub);
                    if (!Directory.Exists(from)) continue;
                    Sync(from, to, ref copied, ref removed);
                }

                // El config viaja con sus frames: reimportarlo suelto después (para afinar
                // números) encuentra las mismas carpetas relativas.
                Directory.CreateDirectory(Path.GetFullPath(dest));
                File.Copy(configPath, Path.Combine(Path.GetFullPath(dest), ConfigFile), true);

                AssetDatabase.Refresh();
                report.Warnings.Add($"Frames en '{dest}': {copied} copiados, {removed} viejos borrados.");

                BossConfigImporter.Import(root, report);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (ConfigImportException e) { report.Errors.Add(e.Message); }
            catch (JsonException e) { report.Errors.Add($"JSON inválido: {e.Message}"); }
            catch (Exception e) { report.Errors.Add($"Excepción inesperada: {e}"); }
            finally
            {
                if (temp != null && Directory.Exists(temp))
                {
                    try { Directory.Delete(temp, true); }
                    catch (Exception e) { report.Warnings.Add($"No se pudo borrar '{temp}': {e.Message}"); }
                }
            }

            return report.BuildSummary();
        }

        /// <summary>
        /// Deja <paramref name="to"/> igual que <paramref name="from"/> en PNGs: copia encima (el
        /// .meta del archivo se conserva, así que el sprite mantiene su GUID) y borra lo que sobra.
        /// </summary>
        private static void Sync(string from, string to, ref int copied, ref int removed)
        {
            Directory.CreateDirectory(to);
            var incoming = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(from.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(to, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
                incoming.Add(Path.GetFullPath(target));
                copied++;
            }

            foreach (var file in Directory.GetFiles(to, "*.png", SearchOption.AllDirectories))
            {
                if (incoming.Contains(Path.GetFullPath(file))) continue;
                File.Delete(file);
                if (File.Exists(file + ".meta")) File.Delete(file + ".meta");
                removed++;
            }

            // Carpetas que se quedaron vacías (una animación que ya no existe).
            foreach (var dir in Directory.GetDirectories(to, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                if (Directory.EnumerateFileSystemEntries(dir).Any(e => !e.EndsWith(".meta"))) continue;
                foreach (var meta in Directory.GetFiles(dir, "*.meta")) File.Delete(meta);
                Directory.Delete(dir);
                if (File.Exists(dir + ".meta")) File.Delete(dir + ".meta");
            }
        }
    }
}
