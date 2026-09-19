using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Import manual de una entrada <b>proyectil</b> o <b>VFX</b> de la biblioteca web: el .zip que
    /// produce "⬇ Descargar .zip" con el tipo puesto a Proyectil/VFX (manifest.json + una carpeta
    /// de PNG por animación), o esa misma carpeta ya descomprimida.
    ///
    /// <b>Para qué, si el import del enemigo ya lo hace solo.</b>
    /// <see cref="ProjectileConfigImporter"/> materializa el proyectil de un enemigo al vuelo desde
    /// su <c>libraryId</c>, así que este importador NO es un paso obligatorio de esa cadena. Existe
    /// para el caso contrario: traer un proyectil o un VFX al proyecto por su cuenta — para
    /// asignarlo a mano a un <c>BossAttack</c>, a un arma, o simplemente para tenerlo antes de que
    /// exista el enemigo que lo tira. Construye exactamente el mismo prefab que construiría la vía
    /// perezosa (mismo <see cref="FxPrefabBuilder"/>), así que importar primero y dejar que lo haga
    /// el enemigo después son caminos intercambiables, no dos resultados distintos.
    ///
    /// <b>La fuente no se borra nunca</b>, por el mismo motivo documentado en
    /// <see cref="CombinedBundleImporter"/>: los sprites que el prefab referencia SON los PNG de esa
    /// carpeta. Se descomprime directamente en su sitio definitivo
    /// (<c>Assets/Art/WebLibrary/&lt;kind&gt;/&lt;libraryId&gt;/</c>) en vez de en una carpeta
    /// temporal que luego habría que conservar igualmente.
    /// </summary>
    public static class FxBundleImporter
    {
        [MenuItem("Tools/Web/Import Config.../Importar proyectil/VFX (.zip)...")]
        public static void ImportZipMenu()
        {
            string path = EditorUtility.OpenFilePanel("Selecciona el .zip del proyectil o VFX", Application.dataPath, "zip");
            if (string.IsNullOrEmpty(path)) return;

            string summary = ImportZip(path);
            Debug.Log(summary);
            EditorUtility.DisplayDialog("Importar proyectil/VFX", summary, "OK");
        }

        [MenuItem("Tools/Web/Import Config.../Importar proyectil/VFX (carpeta)...")]
        public static void ImportFolderMenu()
        {
            string path = EditorUtility.OpenFolderPanel("Selecciona la carpeta (con manifest.json)", Application.dataPath, "");
            if (string.IsNullOrEmpty(path)) return;

            string summary = ImportFolder(path);
            Debug.Log(summary);
            EditorUtility.DisplayDialog("Importar proyectil/VFX", summary, "OK");
        }

        // ============================================================ zip

        /// <summary>Descomprime el .zip en su carpeta canónica y construye el prefab. Nunca lanza.</summary>
        public static string ImportZip(string zipPath)
        {
            var log = new StringBuilder();
            string temp = null;

            try
            {
                if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
                    return $"[FxBundleImporter] no existe '{zipPath}'.";

                // Se descomprime primero fuera de Assets/ sólo para poder leer kind+libraryId del
                // manifest y saber dónde debe vivir de verdad — mismo patrón que CombinedBundleImporter.
                temp = Path.Combine(Path.GetTempPath(), $"RedMagicFxImport_{Guid.NewGuid():N}");
                Directory.CreateDirectory(temp);
                ZipFile.ExtractToDirectory(zipPath, temp);

                string manifestPath = Path.Combine(temp, "manifest.json");
                if (!File.Exists(manifestPath))
                    return "[FxBundleImporter] el .zip no contiene manifest.json en su raíz.";

                var manifest = EnemyImporter.ParseManifest(File.ReadAllText(manifestPath));
                string kind = ResolveKind(manifest, log);
                if (kind == null) return log.ToString();

                string dest = DestinationFor(manifest, kind);
                CopyDirectoryRecursive(temp, dest);
                AssetDatabase.Refresh();

                log.AppendLine($"[FxBundleImporter] fuente copiada a '{dest}'.");
                return Build(dest, kind, log);
            }
            catch (Exception e)
            {
                log.AppendLine($"[FxBundleImporter] error: {e.Message}");
                return log.ToString();
            }
            finally
            {
                if (temp != null && Directory.Exists(temp))
                {
                    try { Directory.Delete(temp, recursive: true); } catch { /* temporal: da igual */ }
                }
            }
        }

        // ============================================================ carpeta ya descomprimida

        /// <summary>
        /// Construye desde una carpeta que ya está dentro de Assets/. No la mueve: si alguien la
        /// descomprimió a mano en otro sitio, ese sitio es igual de válido — <see cref="FxPrefabBuilder"/>
        /// la encuentra igual por su libraryId.
        /// </summary>
        public static string ImportFolder(string absoluteFolder)
        {
            var log = new StringBuilder();

            try
            {
                if (string.IsNullOrEmpty(absoluteFolder) || !Directory.Exists(absoluteFolder))
                    return $"[FxBundleImporter] no existe la carpeta '{absoluteFolder}'.";

                string normalized = absoluteFolder.Replace('\\', '/');
                if (!normalized.Contains("/Assets"))
                    return "[FxBundleImporter] la carpeta tiene que estar dentro de Assets/ del proyecto.";

                string manifestPath = Path.Combine(absoluteFolder, "manifest.json");
                if (!File.Exists(manifestPath))
                    return $"[FxBundleImporter] no hay manifest.json en '{absoluteFolder}'.";

                var manifest = EnemyImporter.ParseManifest(File.ReadAllText(manifestPath));
                string kind = ResolveKind(manifest, log);
                if (kind == null) return log.ToString();

                int idx = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
                string relative = idx >= 0 ? normalized.Substring(idx + 1) : normalized;

                AssetDatabase.Refresh();
                return Build(relative, kind, log);
            }
            catch (Exception e)
            {
                log.AppendLine($"[FxBundleImporter] error: {e.Message}");
                return log.ToString();
            }
        }

        // ============================================================ compartido

        /// <summary>
        /// El kind del manifest, validado. Un manifest sin 'kind' (exportado antes de que el campo
        /// existiera) no se adivina: este importador construye proyectiles y VFX, y un enemigo
        /// entra por otra puerta, así que decirlo es mejor que producir un prefab equivocado.
        /// </summary>
        private static string ResolveKind(EnemyImporter.Manifest manifest, StringBuilder log)
        {
            if (manifest == null)
            {
                log.AppendLine("[FxBundleImporter] no se pudo leer el manifest.");
                return null;
            }

            string kind = manifest.kind;
            if (kind == FxPrefabBuilder.KindProjectile || kind == FxPrefabBuilder.KindFx) return kind;

            log.AppendLine(string.IsNullOrWhiteSpace(kind)
                ? "[FxBundleImporter] el manifest no trae 'kind'. Vuelve a exportarlo desde la web con " +
                  "el tipo puesto a Proyectil o VFX; si es un enemigo, usa " +
                  "Tools > Web > Enemy Importer > Build Enemy From Folder."
                : $"[FxBundleImporter] el manifest es de tipo '{kind}'; este importador sólo construye " +
                  "'projectile' y 'fx'.");
            return null;
        }

        private static string DestinationFor(EnemyImporter.Manifest manifest, string kind)
        {
            // Por id cuando lo hay (estable frente a renombrados); por nombre como respaldo para un
            // zip exportado antes de que el manifest llevara libraryId.
            string key = !string.IsNullOrWhiteSpace(manifest.libraryId)
                ? manifest.libraryId
                : (string.IsNullOrWhiteSpace(manifest.enemyName) ? "Unnamed" : manifest.enemyName);

            return FxPrefabBuilder.SourceFolderFor(kind, key);
        }

        private static string Build(string assetsRelativeFolder, string kind, StringBuilder log)
        {
            var prefab = FxPrefabBuilder.BuildFromFolder(assetsRelativeFolder, kind, log);

            if (prefab != null)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = prefab;
                log.AppendLine($"[FxBundleImporter] listo: {AssetDatabase.GetAssetPath(prefab)}");
            }
            else log.AppendLine("[FxBundleImporter] no se pudo construir el prefab (ver arriba).");

            return log.ToString();
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectoryRecursive(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }
}
