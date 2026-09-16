using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedMagic.Bosses.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>Qué schema encaja con el JSON que se está importando.</summary>
    public enum ConfigKind
    {
        /// <summary>Detectar por la forma del JSON (campos requeridos de cada schema).</summary>
        Auto,
        Enemy,
        Projectile,
        Boss,
    }

    /// <summary>
    /// Punto de entrada único de los tres importadores (<see cref="EnemyConfigImporter"/>,
    /// <see cref="ProjectileConfigImporter"/>, <see cref="BossConfigImporter"/>): parsea el JSON,
    /// detecta o respeta el tipo explícito, corre el importador que toque dentro de un intento
    /// limpio, y construye el resumen final.
    /// </summary>
    public static class ConfigImportRunner
    {
        [MenuItem("Tools/RedMagic/Import Config.../Importar archivo...")]
        public static void ImportFileMenu()
        {
            string path = EditorUtility.OpenFilePanel("Selecciona un config JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;

            RunAndShow(path, libraryPath: null, ConfigKind.Auto, resetEnemyTuning: false);
        }

        [MenuItem("Tools/RedMagic/Import Config.../Importar archivo (RESETEANDO tuning de enemigo)...")]
        public static void ImportFileResetMenu()
        {
            string path = EditorUtility.OpenFilePanel("Selecciona un EnemyConfig JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;

            if (!EditorUtility.DisplayDialog("Resetear tuning",
                    "Si el JSON es un EnemyConfig, se sobrescribirá el EnemyStats del prefab con " +
                    "los valores del JSON. Lo que se haya afinado a mano en el prefab se pierde.",
                    "Resetear", "Cancelar"))
                return;

            RunAndShow(path, libraryPath: null, ConfigKind.Auto, resetEnemyTuning: true);
        }

        [MenuItem("Tools/RedMagic/Import Config.../Ventana de importación")]
        public static void OpenWindow() => EditorWindow.GetWindow<ConfigImportWindow>("Import Config");

        private static void RunAndShow(string path, string libraryPath, ConfigKind kind, bool resetEnemyTuning)
        {
            string summary = ImportFile(path, libraryPath, kind, resetEnemyTuning);
            Debug.Log(summary);
            EditorUtility.DisplayDialog("Import Config", summary, "OK");
        }

        /// <summary>
        /// Corre el import completo y devuelve el resumen como texto. No lanza: cualquier fallo
        /// (JSON inválido, un <see cref="ConfigImportException"/> de "fallar alto", o cualquier
        /// otra excepción) se recoge en <see cref="ConfigImportReport.Errors"/>.
        /// </summary>
        public static string ImportFile(string configPath, string libraryPath, ConfigKind kind,
                                        bool resetEnemyTuning)
        {
            var report = new ConfigImportReport(Path.GetFileName(configPath));

            try
            {
                if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                    throw new ConfigImportException($"No existe el archivo '{configPath}'.");

                var root = JObject.Parse(File.ReadAllText(configPath));

                Dictionary<string, JObject> library = null;
                if (!string.IsNullOrEmpty(libraryPath))
                {
                    if (!File.Exists(libraryPath))
                        throw new ConfigImportException($"No existe la biblioteca de proyectiles '{libraryPath}'.");
                    library = ProjectileConfigImporter.LoadLibraryFromFile(libraryPath);
                }

                var resolvedKind = kind == ConfigKind.Auto ? DetectKind(root) : kind;

                switch (resolvedKind)
                {
                    case ConfigKind.Enemy:
                        EnemyConfigImporter.Import(root, report, library, resetEnemyTuning);
                        break;
                    case ConfigKind.Projectile:
                        ProjectileConfigImporter.Import(root, report);
                        break;
                    case ConfigKind.Boss:
                        BossConfigImporter.Import(root, report, library);
                        break;
                    default:
                        throw new ConfigImportException(
                            "No se pudo determinar si el JSON es un EnemyConfig, ProjectileConfig o " +
                            "BossConfig por su forma. Indica el tipo explícitamente en la ventana de " +
                            "importación (Tools ▸ RedMagic ▸ Import Config... ▸ Ventana de importación).");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (ConfigImportException e)
            {
                report.Errors.Add(e.Message);
            }
            catch (JsonException e)
            {
                report.Errors.Add($"JSON inválido: {e.Message}");
            }
            catch (Exception e)
            {
                report.Errors.Add($"Excepción inesperada: {e}");
            }

            return report.BuildSummary();
        }

        /// <summary>
        /// EnemyConfig y BossConfig se distinguen por sus campos requeridos, mutuamente
        /// excluyentes. ProjectileConfig es lo que queda cuando ninguno de los dos encaja:
        /// biblioteca ('projectiles') o bloque suelto con alguno de los campos propios de
        /// ProjectileSpec.
        ///
        /// Pública porque <c>ConfigImportWindow</c> la usa para adelantar en la UI, antes de que
        /// se pulse Importar, si el archivo es un ProjectileConfig suelto — y por tanto si va a
        /// escribir sobre lo que esté seleccionado en el Project.
        /// </summary>
        public static ConfigKind DetectKind(JObject root)
        {
            if (root["enemyName"] != null && root["art"] != null) return ConfigKind.Enemy;
            if (root["displayName"] != null && root["attacks"] != null && root["phases"] != null) return ConfigKind.Boss;
            if (root["projectiles"] != null) return ConfigKind.Projectile;

            if (root["speed"] != null || root["lifetime"] != null || root["pierce"] != null ||
                root["homingTurnRate"] != null || root["homingRange"] != null || root["arcGravity"] != null ||
                root["muzzleOffset"] != null || root["impactRadius"] != null || root["impactDamage"] != null)
                return ConfigKind.Projectile;

            return ConfigKind.Auto; // sin detectar
        }
    }
}
