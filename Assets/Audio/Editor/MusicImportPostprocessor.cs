using UnityEditor;
using UnityEngine;

namespace RedMagic.Audio.EditorTools
{
    /// <summary>
    /// Fuerza los ajustes de importación correctos para música en cualquier audio que caiga bajo
    /// <c>Assets/Resources/Music/</c>: <b>Streaming</b> + <b>Load In Background</b>.
    ///
    /// Sin esto, un clip nuevo se importa con los ajustes por defecto del proyecto (Decompress On
    /// Load), que descomprime la pista entera en el hilo principal al empezar a sonar — justo el
    /// tirón que se ve al cambiar de escena. Con este postprocesador basta con soltar el archivo
    /// bien nombrado en la carpeta: queda listo para <c>AudioManager.PlaySceneMusic</c> sin tocar
    /// el Inspector.
    /// </summary>
    public class MusicImportPostprocessor : AssetPostprocessor
    {
        private const string MusicFolder = "Assets/Resources/Music/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(MusicFolder)) return;

            var importer = (AudioImporter)assetImporter;

            var settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.Streaming)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                importer.defaultSampleSettings = settings;
            }

            importer.loadInBackground = true;
            importer.ambisonic = false;
        }
    }
}
