using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Audio.EditorTools
{
    /// <summary>
    /// Escribe en el <see cref="SoundCue"/> real de un <see cref="SoundSlot"/> (prefab, asset o escena)
    /// sin abrir nada a mano. Si el hueco es un momento declarado sin entrada todavía, crea el
    /// <see cref="SoundEmitter"/> y/o la entrada. Lo usan el cableado de la fase 5 y la pestaña Sounds.
    /// </summary>
    public static class SoundSlotEditor
    {
        public const string GenericTestClipPath = "Assets/Audio/Test/generic_test.wav";
        private const string GenericTestSource = "Assets/Audio/Sounds/ui_hover.wav";

        /// <summary>El clip de prueba genérico (se crea copiando ui_hover.wav si no existe).</summary>
        public static AudioClip GenericTestClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(GenericTestClipPath);
            if (clip != null) return clip;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(GenericTestClipPath));
            AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset(GenericTestSource, GenericTestClipPath)) return null;
            return AssetDatabase.LoadAssetAtPath<AudioClip>(GenericTestClipPath);
        }

        /// <summary>
        /// Aplica <paramref name="edit"/> a la cue del hueco y guarda. Devuelve false (con motivo) si no
        /// encuentra el objeto.
        /// </summary>
        public static bool Edit(SoundSlot slot, Action<SerializedProperty> edit, out string error)
        {
            error = null;
            // Lo que se escribe aquí ya deja el registro al día: que el aviso de "desactualizado" no lo cuente.
            SoundRegistryWatcher.IgnoreNext(string.IsNullOrEmpty(slot.ScenePath) ? slot.AssetPath : slot.ScenePath);
            if (!string.IsNullOrEmpty(slot.ScenePath)) return EditInScene(slot, edit, out error);
            if (slot.AssetPath.EndsWith(".prefab", StringComparison.Ordinal)) return EditInPrefab(slot, edit, out error);
            return EditInAsset(slot, edit, out error);
        }

        // ------------------------------------------------------------------ destinos

        private static bool EditInAsset(SoundSlot slot, Action<SerializedProperty> edit, out string error)
        {
            error = null;
            var asset = AssetDatabase.LoadMainAssetAtPath(slot.AssetPath);
            if (asset == null) { error = "no existe el asset"; return false; }

            var so = new SerializedObject(asset);
            var cue = so.FindProperty(slot.PropertyPath);
            if (cue == null) { error = $"no hay '{slot.PropertyPath}'"; return false; }

            edit(cue);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return true;
        }

        private static bool EditInPrefab(SoundSlot slot, Action<SerializedProperty> edit, out string error)
        {
            error = null;
            var root = PrefabUtility.LoadPrefabContents(slot.AssetPath);
            try
            {
                var host = string.IsNullOrEmpty(slot.ObjectPath) ? root.transform : root.transform.Find(slot.ObjectPath);
                if (host == null) { error = $"no hay '{slot.ObjectPath}' en el prefab"; return false; }

                if (slot.Kind == SoundSlotKind.EmitterTrigger)
                {
                    if (!EditEmitterEntry(host.gameObject, slot, edit, root.CompareTag("Player"), out error)) return false;
                }
                else
                {
                    var component = host.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == slot.ComponentType);
                    if (component == null) { error = $"no hay {slot.ComponentType}"; return false; }

                    var so = new SerializedObject(component);
                    var cue = so.FindProperty(slot.PropertyPath);
                    if (cue == null) { error = $"no hay '{slot.PropertyPath}'"; return false; }
                    edit(cue);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, slot.AssetPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool EditInScene(SoundSlot slot, Action<SerializedProperty> edit, out string error)
        {
            error = null;
            var scene = SceneManager.GetSceneByPath(slot.ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (!openedHere && scene.isDirty) { error = "la escena tiene cambios sin guardar"; return false; }
            if (openedHere) scene = EditorSceneManager.OpenScene(slot.ScenePath, OpenSceneMode.Additive);

            try
            {
                var host = FindInScene(scene, slot.ObjectPath);
                if (host == null) { error = $"no hay '{slot.ObjectPath}' en la escena"; return false; }

                var component = host.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == slot.ComponentType);
                if (component == null) { error = $"no hay {slot.ComponentType}"; return false; }

                var so = new SerializedObject(component);
                var cue = so.FindProperty(slot.PropertyPath);
                if (cue == null) { error = $"no hay '{slot.PropertyPath}'"; return false; }
                edit(cue);
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return true;
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Entrada del SoundEmitter para el momento del hueco; la crea (y el emisor) si falta.
        private static bool EditEmitterEntry(GameObject host, SoundSlot slot, Action<SerializedProperty> edit,
                                             bool isPlayer, out string error)
        {
            error = null;
            var emitter = host.GetComponent<SoundEmitter>();
            if (emitter == null) emitter = host.AddComponent<SoundEmitter>();

            var so = new SerializedObject(emitter);
            var events = so.FindProperty("soundEvents");

            // "OnHit#2" = la segunda entrada de OnHit.
            int wanted = 1;
            int hash = slot.Member.IndexOf('#');
            if (hash >= 0) int.TryParse(slot.Member.Substring(hash + 1), out wanted);

            SerializedProperty entry = null;
            int found = 0;
            for (int i = 0; i < events.arraySize && entry == null; i++)
            {
                var e = events.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("trigger").intValue == (int)slot.Trigger && ++found == wanted) entry = e;
            }

            if (entry == null)
            {
                if (found + 1 != wanted) { error = $"no existe la entrada {slot.Member}"; return false; }
                events.arraySize++;
                entry = events.GetArrayElementAtIndex(events.arraySize - 1);
                entry.FindPropertyRelative("trigger").intValue = (int)slot.Trigger;
                InitCue(entry.FindPropertyRelative("cue"), positional: !isPlayer, DefaultPriority(slot.Trigger));
            }

            edit(entry.FindPropertyRelative("cue"));
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        public static void InitCue(SerializedProperty cue, bool positional, SoundPriority priority)
        {
            cue.FindPropertyRelative("clips").arraySize = 0;
            cue.FindPropertyRelative("volume").floatValue = 1f;
            cue.FindPropertyRelative("pitchMin").floatValue = 1f;
            cue.FindPropertyRelative("pitchMax").floatValue = 1f;
            cue.FindPropertyRelative("noRepeatLast").boolValue = true;
            cue.FindPropertyRelative("priority").enumValueIndex = (int)priority;
            cue.FindPropertyRelative("positional").boolValue = positional;
            cue.FindPropertyRelative("rolloffStart").floatValue = SoundCue.DefaultRolloffStart;
            cue.FindPropertyRelative("rolloffEnd").floatValue = SoundCue.DefaultRolloffEnd;
            cue.FindPropertyRelative("initialized").boolValue = true;
        }

        private static SoundPriority DefaultPriority(SoundTrigger trigger) => trigger switch
        {
            SoundTrigger.Footstep or SoundTrigger.OnMove or SoundTrigger.OnLand or SoundTrigger.OnSpawn => SoundPriority.Low,
            SoundTrigger.OnDeath or SoundTrigger.OnActivate or SoundTrigger.OnVulnerable or SoundTrigger.OnLowHealth => SoundPriority.High,
            _ => SoundPriority.Normal,
        };

        /// <summary>Deja en la cue un único clip.</summary>
        public static void SetSingleClip(SerializedProperty cue, AudioClip clip)
        {
            if (!cue.FindPropertyRelative("initialized").boolValue)
                InitCue(cue, cue.FindPropertyRelative("positional").boolValue, SoundPriority.Normal);
            var clips = cue.FindPropertyRelative("clips");
            clips.arraySize = clip != null ? 1 : 0;
            if (clip != null) clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
        }

        private static GameObject FindInScene(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                if (parts.Length == 1) return root;
                var t = root.transform.Find(string.Join("/", parts.Skip(1)));
                if (t != null) return t.gameObject;
            }
            return null;
        }
    }
}
