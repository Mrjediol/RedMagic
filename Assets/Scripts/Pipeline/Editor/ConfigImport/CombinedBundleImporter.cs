using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using RedMagic.Pipeline;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Tercera vía de import, junto a las dos ya existentes (un manifest de sprites suelto vía
    /// <see cref="EnemyImporter"/>, o un EnemyConfig JSON suelto vía <see cref="EnemyConfigImporter"/>):
    /// el .zip combinado que produce el botón "⬇ Exportar todo junto" de la pestaña Enemy Creator
    /// de la web (Web/RedMagicWeb/modules/enemy-bundle.js) — mismo manifest.json + carpetas de PNG
    /// por animación que ya lee <see cref="EnemyImporter"/>, más <c>enemy-config.json</c> en la raíz
    /// del zip.
    ///
    /// No reimplementa ninguno de los dos pasos: descomprime, corre
    /// <see cref="EnemyImporter.BuildEnemyFromFolderPath"/> tal cual (produce el
    /// <see cref="SpriteSheetRecipe"/> + AnimatorController + prefab de sprites), y con ESE recipe
    /// recién creado corre <see cref="EnemyConfigImporter.Import"/> tal cual — el mismo resultado
    /// que hacer los dos imports por separado a mano, en el mismo orden. El 'art' que traiga
    /// enemy-config.json (un valor "por convención" que la web no puede verificar, ver
    /// modules/enemy-export.js's <c>libraryArtPath</c>) se sobreescribe con la ruta REAL del recipe
    /// que este mismo import acaba de crear, así que un enemyName que no coincidiera entre el
    /// manifest y el config falla alto en vez de producir un enemigo con el arte equivocado.
    ///
    /// <b>Dónde acaban los PNG de entrada — y por qué NO se pueden borrar después.</b>
    /// <see cref="EnemyImporter.BuildClip"/> apunta cada keyframe del <c>AnimationClip</c> AL
    /// SPRITE DE ENTRADA TAL CUAL (el PNG de cada frame, cargado desde la carpeta que se le pasa a
    /// <c>BuildEnemyFromFolderPath</c>) — no copia los frames a ningún otro sitio; lo único que
    /// <c>BuildSpriteSheetRecipe</c> copia aparte es UN frame representativo por fila, para el
    /// sprite de reposo/collider de <c>EnemyFactory.IdleSprite</c>. Por eso la carpeta de entrada
    /// es, en la práctica, el almacén permanente de cada frame de cada clip: borrarla después de
    /// importar (como hacía una versión anterior de esta clase, tratándola como "material de
    /// staging") deja cada keyframe apuntando a un asset borrado — el clip conserva el NÚMERO de
    /// frames (va horneado en los tiempos de las keyframes) pero cada uno queda sin sprite, y el
    /// enemigo se vuelve invisible en Play. La entrada se descomprime, por tanto, DIRECTAMENTE en
    /// <c>Assets/Art/EnemyImports/&lt;enemyName&gt;/</c> — la misma carpeta que ya usan las salidas de
    /// <see cref="EnemyImporter"/> (Animations/, el .sheet.asset, el .controller) y la misma que
    /// tendría un usuario descomprimiendo el .zip a mano ahí, tal y como documenta la cabecera de
    /// <c>EnemyImporter.cs</c> — y nunca se borra.
    /// </summary>
    public static class CombinedBundleImporter
    {
        [MenuItem("Tools/Web/Import Config.../Importar enemigo completo (sprites + config)...")]
        public static void ImportBundleMenu()
        {
            string path = EditorUtility.OpenFilePanel(
                "Selecciona el .zip combinado (sprites + config)", Application.dataPath, "zip");
            if (string.IsNullOrEmpty(path)) return;

            string summary = ImportBundle(path);
            Debug.Log(summary);
            EditorUtility.DisplayDialog("Importar enemigo completo", summary, "OK");
        }

        /// <summary>
        /// Corre el import completo y devuelve el resumen combinado como texto — mismo patrón de
        /// "nunca lanza, todo va al report" que <see cref="ConfigImportRunner.ImportFile"/>.
        /// </summary>
        public static string ImportBundle(string zipPath)
        {
            var report = new ConfigImportReport(Path.GetFileName(zipPath));
            string tempExtractFolder = null;

            try
            {
                if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
                    throw new ConfigImportException($"No existe el archivo '{zipPath}'.");

                // Primero se descomprime a una carpeta temporal del SISTEMA (fuera de Assets/, sin
                // que AssetDatabase se entere todavía) sólo para poder leer enemyName de
                // enemy-config.json antes de saber dónde debe vivir permanentemente el contenido —
                // ver el comentario de clase sobre por qué el destino final no puede ser desechable.
                tempExtractFolder = Path.Combine(Path.GetTempPath(), $"RedMagicBundleImport_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempExtractFolder);
                ZipFile.ExtractToDirectory(zipPath, tempExtractFolder);

                string manifestPath = Path.Combine(tempExtractFolder, "manifest.json");
                if (!File.Exists(manifestPath))
                    throw new ConfigImportException("El .zip no contiene manifest.json — ¿es un bundle combinado válido?");

                string configPath = Path.Combine(tempExtractFolder, "enemy-config.json");
                if (!File.Exists(configPath))
                    throw new ConfigImportException("El .zip no contiene enemy-config.json en la raíz — ¿es un bundle combinado válido?");

                var configRoot = JObject.Parse(File.ReadAllText(configPath));
                string enemyName = ConfigJson.RequireString(configRoot, "enemyName", "EnemyConfig");

                // 'anchor' sólo existe para decidirlo AQUÍ: este es el único de los tres import
                // paths que realmente CORTA un SpriteSheetRecipe nuevo (vía EnemyImporter, más
                // abajo) — el import de config suelto (EnemyConfigImporter.Import) sólo referencia
                // un recipe YA existente por 'art', cuyo anchor ya quedó fijado cuando se cortó, así
                // que no tiene sentido (ni es seguro) que ese camino lo sobreescriba. Ver
                // docs/schemas/COMPATIBILITY.md.
                AnchorMode anchor = configRoot["anchor"] != null
                    ? ConfigJson.ParseEnum<AnchorMode>(configRoot["anchor"], $"{enemyName}.anchor")
                    : AnchorMode.Center;

                // Destino PERMANENTE — mismo folder que EnemyImporter.cs usa para sus propias
                // salidas y que un unzip a mano ahí produciría. Copia (no mueve) archivo a archivo
                // para que reimportar el mismo enemigo sobrescriba en vez de fallar por "ya existe".
                string enemyRoot = $"Assets/Art/EnemyImports/{enemyName}";
                Directory.CreateDirectory(enemyRoot);
                CopyDirectoryRecursive(tempExtractFolder, enemyRoot);
                AssetDatabase.Refresh();

                // ---------------- paso 1: sprites — EnemyImporter.cs tal cual ----------------
                string absoluteEnemyRoot = Path.GetFullPath(enemyRoot);
                EnemyImporter.BuildEnemyFromFolderPath(absoluteEnemyRoot, anchor: anchor, showDialog: false);

                string recipePath = $"{enemyRoot}/{enemyName}.sheet.asset";
                var recipe = AssetDatabase.LoadAssetAtPath<SpriteSheetRecipe>(recipePath);
                if (recipe == null)
                    throw new ConfigImportException(
                        $"El paso de sprites no produjo el SpriteSheetRecipe esperado en '{recipePath}' " +
                        $"(¿el 'enemyName' del manifest.json no coincide con '{enemyName}' de enemy-config.json?).");

                report.Add(true, recipePath);
                report.Warnings.Add(
                    $"Sprites: '{enemyName}' importado desde {Path.GetFileName(zipPath)} a '{enemyRoot}' " +
                    "(detalle de animaciones/proyectiles en la consola, vía EnemyImporter).");

                // Sobreescribe el 'art' provisional del JSON con el recipe real recién creado —
                // ver el comentario de clase.
                configRoot["art"] = recipePath;

                // ---------------- paso 2: config — EnemyConfigImporter.cs tal cual ----------------
                EnemyConfigImporter.Import(configRoot, report, projectileLibrary: null, resetTuning: false);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (ConfigImportException e)
            {
                report.Errors.Add(e.Message);
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                report.Errors.Add($"JSON inválido: {e.Message}");
            }
            catch (Exception e)
            {
                report.Errors.Add($"Excepción inesperada: {e}");
            }
            finally
            {
                // Sólo la carpeta temporal del sistema (nunca tocada por AssetDatabase) se limpia
                // aquí — el destino en Assets/Art/EnemyImports/<enemyName>/ es permanente, ver arriba.
                if (tempExtractFolder != null && Directory.Exists(tempExtractFolder))
                {
                    try { Directory.Delete(tempExtractFolder, recursive: true); }
                    catch (Exception e) { report.Warnings.Add($"No se pudo borrar la carpeta temporal '{tempExtractFolder}': {e.Message}"); }
                }
            }

            return report.BuildSummary();
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
