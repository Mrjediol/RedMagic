using UnityEditor;
using UnityEngine;

namespace RedMagic.Audio.EditorTools
{
    /// <summary>
    /// Ajustes de importación para todo audio bajo <c>Assets/Audio/</c> (efectos). La música vive en
    /// <c>Assets/Resources/Music/</c> y la trata <see cref="MusicImportPostprocessor"/>.
    ///
    /// Siempre: <b>Preload Audio Data</b> — sin él el clip se carga en el hilo principal la primera
    /// vez que suena (tirón + sonido tarde).
    ///
    /// Sólo al importar un clip por primera vez (o con el menú de reaplicar), los valores de móvil:
    /// Decompress On Load + ADPCM (carga rápida, cero coste de CPU al sonar), Force To Mono (la mezcla
    /// es 2D; el estéreo duplica memoria) y Optimize Sample Rate. Así un ajuste hecho a mano en un clip
    /// concreto (un ambiente largo en Vorbis, un estéreo a propósito) sobrevive a los reimports.
    /// </summary>
    public class SfxImportPostprocessor : AssetPostprocessor
    {
        private const string SfxFolder = "Assets/Audio/";
        private const string MenuPath = "Tools/RedMagic/Audio/Reaplicar ajustes de importación de SFX";

        private static bool s_forceDefaults;

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(SfxFolder)) return;

            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;

            if (importer.importSettingsMissing || s_forceDefaults)
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
                settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                importer.forceToMono = true;
                importer.loadInBackground = false;
            }

            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }

        [MenuItem(MenuPath)]
        private static void ReapplyAll()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { SfxFolder.TrimEnd('/') });
            s_forceDefaults = true;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                s_forceDefaults = false;
            }

            Debug.Log($"[SfxImport] Ajustes de móvil reaplicados a {guids.Length} clips de {SfxFolder}.");
        }
    }
}
