using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RedMagic.Test.Editor
{
    /// <summary>
    /// <b>Tools ▸ RedMagic ▸ Test ▸ Build minijuego de parry (Windows)</b>: build suelta de Windows con
    /// <b>sólo</b> la escena del minijuego (<c>Test/ParryTest</c>), para pasarla a quien quiera probar
    /// el parry. No toca la lista de escenas de Build Settings del juego: las escenas se pasan aquí.
    /// Deja la carpeta en <c>Builds/ParryMinigame/</c> y un zip al lado listo para enviar.
    /// </summary>
    public static class ParryMinigameBuild
    {
        private const string Scene = "Assets/Scenes/Test/ParryTest.unity";
        private const string Folder = "Builds/ParryMinigame";
        private const string Exe = "RedMagicParry.exe";
        private const string Zip = "Builds/RedMagicParry.zip";

        [MenuItem("Tools/RedMagic/Test/Build minijuego de parry (Windows)")]
        public static void Build()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Path.Combine(Folder, Exe),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[ParryMinigameBuild] Build fallida: {report.summary.result}, {report.summary.totalErrors} errores.");
                return;
            }

            // Sin la carpeta de símbolos de depuración, que no hace falta para jugar y pesa mucho.
            foreach (var dir in Directory.GetDirectories(Folder, "*_BurstDebugInformation_DoNotShip"))
                Directory.Delete(dir, true);

            if (File.Exists(Zip)) File.Delete(Zip);
            ZipFile.CreateFromDirectory(Folder, Zip, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);

            Debug.Log($"[ParryMinigameBuild] OK ({report.summary.totalSize / (1024 * 1024)} MB): " +
                      $"{Path.GetFullPath(Path.Combine(Folder, Exe))} — zip para enviar: {Path.GetFullPath(Zip)}");
        }
    }
}
