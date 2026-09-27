using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace RedMagic.Audio.EditorTools
{
    /// <summary>
    /// Montaje del sistema de audio. Idempotente: cada paso comprueba antes de tocar y registra en
    /// consola todo lo que cambia.
    ///
    /// 1 · Crear prefab AudioManager — <c>Assets/Resources/AudioManager.prefab</c>, el único
    /// AudioManager del juego (lo instancia <see cref="AudioManager"/> antes de la primera escena).
    /// Mixer y grupos salen de <c>GameAudioMixer</c>; el resto, de los valores por defecto del
    /// componente (idénticos a los de las copias en escena).
    /// </summary>
    public static class AudioSetupTool
    {
        private const string Menu = "Tools/RedMagic/Audio/";
        public const string PrefabPath = "Assets/Resources/" + AudioManager.PrefabResourcePath + ".prefab";
        private const string MixerPath = "Assets/Audio/GameAudioMixer.mixer";

        [MenuItem(Menu + "1 · Crear prefab AudioManager")]
        public static void CreatePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log($"[AudioSetup] {PrefabPath} ya existe; no se toca.");
                return;
            }

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                Debug.LogError($"[AudioSetup] No se encuentra {MixerPath}.");
                return;
            }

            var music = FirstGroup(mixer, "Music");
            var sfx = FirstGroup(mixer, "SFX");
            if (music == null || sfx == null)
            {
                Debug.LogError("[AudioSetup] El mixer no tiene los grupos Music y SFX.");
                return;
            }

            var go = new GameObject("AudioManager");
            try
            {
                var manager = go.AddComponent<AudioManager>();
                var so = new SerializedObject(manager);
                so.FindProperty("mixer").objectReferenceValue = mixer;
                so.FindProperty("musicGroup").objectReferenceValue = music;
                so.FindProperty("sfxGroup").objectReferenceValue = sfx;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Debug.Log($"[AudioSetup] Creado {PrefabPath} (mixer {mixer.name}, grupos {music.name}/{sfx.name}).");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static AudioMixerGroup FirstGroup(AudioMixer mixer, string name)
        {
            foreach (var group in mixer.FindMatchingGroups(name))
                if (group.name == name) return group;
            return null;
        }
    }
}
