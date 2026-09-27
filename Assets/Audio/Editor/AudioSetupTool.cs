using System.Collections.Generic;
using System.Text;
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
    /// AudioManager del juego (lo instancia <see cref="AudioManager"/> antes de la primera escena), con
    /// su <see cref="SystemSounds"/>. Mixer y grupos salen de <c>GameAudioMixer</c>; el resto, de los
    /// valores por defecto del componente.
    ///
    /// 2 · Preparar huecos y rellenar vacíos — ver <see cref="PrepareAndFillWithTestClip"/>.
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
                go.AddComponent<SystemSounds>();
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

        /// <summary>
        /// 2 · Deja todo hueco de sonido del juego existiendo y sonando: añade SoundEmitter y entradas a
        /// los prefabs que declaran momentos sin entrada, y pone el clip de prueba genérico en todo hueco
        /// vacío. Nunca toca un hueco que ya tiene clip. Si el escáner encuentra algo que no cuadra, para.
        /// </summary>
        [MenuItem(Menu + "2 · Preparar huecos y rellenar vacíos con el clip de prueba")]
        public static void PrepareAndFillWithTestClip()
        {
            EnsureSystemSounds();

            var problems = new List<string>();
            var slots = SoundSlotScanner.Scan(problems);
            if (problems.Count > 0)
            {
                Debug.LogError("[AudioSetup] El escáner encontró problemas; no se toca nada:\n- " +
                               string.Join("\n- ", problems));
                return;
            }

            var test = SoundSlotEditor.GenericTestClip();
            if (test == null)
            {
                Debug.LogError($"[AudioSetup] No se pudo crear {SoundSlotEditor.GenericTestClipPath}.");
                return;
            }

            var log = new StringBuilder();
            int created = 0, filled = 0, failed = 0;
            foreach (var slot in slots)
            {
                if (!slot.EntryMissing && slot.Clips.Count > 0) continue;

                bool ok = SoundSlotEditor.Edit(slot, cue =>
                {
                    var clips = cue.FindPropertyRelative("clips");
                    bool empty = true;
                    for (int i = 0; i < clips.arraySize; i++)
                        if (clips.GetArrayElementAtIndex(i).objectReferenceValue != null) empty = false;
                    if (empty) SoundSlotEditor.SetSingleClip(cue, test);
                }, out string error);

                if (!ok)
                {
                    failed++;
                    log.AppendLine($"  FALLO {slot.Id}: {error}");
                    continue;
                }

                if (slot.EntryMissing) created++;
                filled++;
                log.AppendLine($"  {(slot.EntryMissing ? "entrada nueva + " : "")}clip de prueba → " +
                               $"{slot.AssetPath} ▸ {slot.ComponentType}.{slot.Member}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioSetup] Huecos: {slots.Count} · entradas creadas: {created} · " +
                      $"rellenados con prueba: {filled} · fallos: {failed}\n{log}");
        }

        private static void EnsureSystemSounds()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root.GetComponent<SystemSounds>() != null) return;
                root.AddComponent<SystemSounds>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[AudioSetup] Añadido SystemSounds a {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
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
