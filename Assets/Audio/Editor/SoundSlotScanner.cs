using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Audio.EditorTools
{
    public enum SoundSlotKind
    {
        /// <summary>Entrada de un <see cref="SoundEmitter"/> para un momento que declara el objeto.</summary>
        EmitterTrigger,
        /// <summary>Campo <see cref="SoundCue"/> de un componente o asset.</summary>
        CueField,
        /// <summary>Campo AudioClip de música marcado con <see cref="MusicSlotAttribute"/>.</summary>
        MusicField,
        /// <summary>Pista por convención en <c>Resources/Music/&lt;nombre&gt;</c> (la deriva el registro).</summary>
        MusicConvention,
        /// <summary>Hueco declarado a mano: el momento aún no existe en el juego.</summary>
        Declared,
    }

    /// <summary>Un hueco de sonido que existe en el proyecto, derivado del proyecto mismo.</summary>
    public sealed class SoundSlot
    {
        /// <summary>Clave estable: GUID del asset + tipo del componente + campo o momento.</summary>
        public string Id;
        public SoundSlotKind Kind;
        public string AssetPath;
        public string AssetGuid;
        /// <summary>Escena que contiene el objeto (vacío si es un prefab o un asset).</summary>
        public string ScenePath;
        /// <summary>Ruta del objeto dentro del prefab / escena (vacío si es un asset).</summary>
        public string ObjectPath;
        public string ComponentType;
        /// <summary>Ruta del <see cref="SoundCue"/> en el SerializedObject (vacía si falta la entrada).</summary>
        public string PropertyPath;
        /// <summary>Nombre del campo, o del momento para <see cref="SoundSlotKind.EmitterTrigger"/>.</summary>
        public string Member;
        public SoundTrigger Trigger;
        /// <summary>El objeto declara el momento pero aún no hay entrada en su SoundEmitter.</summary>
        public bool EntryMissing;
        /// <summary>Hueco vacío de un enemigo que suena con el genérico de SystemSounds (InheritedPlaceholder).</summary>
        public bool UsesFallback;
        public readonly List<AudioClip> Clips = new List<AudioClip>();
    }

    /// <summary>
    /// Encuentra todos los huecos de sonido reales del proyecto, sin lista a mano:
    /// <list type="bullet">
    /// <item>en cada prefab, los momentos que declaran sus <see cref="ISoundEventSource"/> de la raíz,
    /// con o sin entrada en su <see cref="SoundEmitter"/>;</item>
    /// <item>cada campo <see cref="SoundCue"/> de componentes (prefabs y escenas) y de ScriptableObjects,
    /// respetando <see cref="SoundSlotIfAttribute"/>.</item>
    /// </list>
    /// Lo marcado con la etiqueta de asset <see cref="IgnoreLabel"/> (contenido de prueba) no cuenta.
    /// Lo que no cuadra (una entrada de emisor para un momento que nadie declara) se devuelve como
    /// problema, nunca se filtra en silencio.
    /// </summary>
    public static class SoundSlotScanner
    {
        public const string IgnoreLabel = "SoundIgnore";
        private static readonly string[] Vendored = { "Assets/Brackeys/", "Assets/Cainos/", "Assets/Dragon Warrior Files/" };

        public static List<SoundSlot> Scan(List<string> problems)
        {
            var slots = new List<SoundSlot>();
            var cueTypes = TypesWithCueFields();

            foreach (var path in Find("t:Prefab")) ScanPrefab(path, slots, problems);
            foreach (var path in Find("t:ScriptableObject")) ScanAsset(path, slots);
            foreach (var path in ScenesWith(cueTypes)) ScanScene(path, slots, problems);

            slots.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return slots;
        }

        public static bool IsIgnored(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            return asset != null && AssetDatabase.GetLabels(asset).Contains(IgnoreLabel);
        }

        // ------------------------------------------------------------------ prefabs

        private static void ScanPrefab(string path, List<SoundSlot> slots, List<string> problems)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) return;
            string guid = AssetDatabase.AssetPathToGUID(path);

            // Momentos declarados por las fuentes de la raíz (el SoundEmitter escucha su propio GameObject).
            var declared = new List<SoundTrigger>();
            var buffer = new List<SoundTrigger>();
            foreach (var source in root.GetComponents<ISoundEventSource>())
            {
                if (source is Component c && Inherited(c)) continue;
                buffer.Clear();
                source.DeclareSoundTriggers(buffer);
                foreach (var t in buffer) if (!declared.Contains(t)) declared.Add(t);
            }

            var emitter = root.GetComponent<SoundEmitter>();
            if (emitter != null && Inherited(emitter)) emitter = null;
            if (declared.Count > 0 || emitter != null)
                AddEmitterSlots(path, guid, "", "", root, declared, emitter, slots, problems);

            foreach (var child in root.GetComponentsInChildren<ISoundEventSource>(true))
                if (child is Component c && c.gameObject != root && !Inherited(c))
                    problems.Add($"{path} ▸ {PathOf(c.transform)}: {c.GetType().Name} emite sonido fuera de la raíz; su SoundEmitter no lo oiría.");

            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is SoundEmitter || Inherited(component)) continue;
                // Ruta relativa a la raíz: renombrar el prefab no cambia la clave.
                string rel = component.transform == root.transform ? "" : AnimationUtility.CalculateTransformPath(component.transform, root.transform);
                AddCueFields(new SerializedObject(component), path, guid, "", rel, component.GetType().Name, slots);
            }
        }

        private static void AddEmitterSlots(string path, string guid, string scene, string scope, GameObject owner,
                                            List<SoundTrigger> declared, SoundEmitter emitter,
                                            List<SoundSlot> slots, List<string> problems)
        {
            var events = emitter != null ? new SerializedObject(emitter).FindProperty("soundEvents") : null;
            // Enemigo con el genérico activo: sus huecos vacíos de herido/muerte/movimiento/ataque heredan.
            bool fallback = owner.GetComponent<IEnemySoundFallbackUser>() != null &&
                            (emitter == null || emitter.UsesGenericFallback);
            var seen = new Dictionary<SoundTrigger, int>();

            if (events != null)
            {
                for (int i = 0; i < events.arraySize; i++)
                {
                    var e = events.GetArrayElementAtIndex(i);
                    var trigger = (SoundTrigger)e.FindPropertyRelative("trigger").intValue;
                    if (!declared.Contains(trigger))
                    {
                        problems.Add($"{path}{(scene != "" ? " (" + scene + ")" : "")} ▸ {PathOf(owner.transform)}: " +
                                     $"el SoundEmitter tiene '{trigger}' pero ningún componente lo lanza (hueco fantasma).");
                        continue;
                    }

                    seen.TryGetValue(trigger, out int n);
                    seen[trigger] = n + 1;

                    var cue = e.FindPropertyRelative("cue");
                    var slot = NewSlot(SoundSlotKind.EmitterTrigger, path, guid, scene, scope,
                                       nameof(SoundEmitter), trigger.ToString() + (n > 0 ? "#" + (n + 1) : ""));
                    slot.Trigger = trigger;
                    slot.PropertyPath = cue.propertyPath;
                    ReadClips(cue, slot.Clips);
                    slot.UsesFallback = fallback && slot.Clips.Count == 0 && SystemSounds.HasEnemyFallback(trigger);
                    slots.Add(slot);
                }
            }

            foreach (var trigger in declared)
            {
                if (seen.ContainsKey(trigger)) continue;
                var slot = NewSlot(SoundSlotKind.EmitterTrigger, path, guid, scene, scope,
                                   nameof(SoundEmitter), trigger.ToString());
                slot.Trigger = trigger;
                slot.EntryMissing = true;
                slot.UsesFallback = fallback && SystemSounds.HasEnemyFallback(trigger);
                slots.Add(slot);
            }
        }

        // ------------------------------------------------------------------ assets

        private static void ScanAsset(string path, List<SoundSlot> slots)
        {
            if (!path.EndsWith(".asset", StringComparison.Ordinal)) return;
            var asset = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
            if (asset == null) return;
            AddCueFields(new SerializedObject(asset), path, AssetDatabase.AssetPathToGUID(path), "", "",
                         asset.GetType().Name, slots);
        }

        // ------------------------------------------------------------------ escenas

        private static void ScanScene(string path, List<SoundSlot> slots, List<string> problems)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                    {
                        // Lo que viene de un prefab lo cuenta el prefab; aquí sólo lo propio de la escena.
                        if (component == null || component is SoundEmitter ||
                            PrefabUtility.IsPartOfPrefabInstance(component)) continue;
                        AddCueFields(new SerializedObject(component), path, guid, path, PathOf(component.transform),
                                     component.GetType().Name, slots);
                    }
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Sólo se abren las escenas que llevan algún componente con campos SoundCue (se mira el YAML).
        private static IEnumerable<string> ScenesWith(HashSet<string> cueScriptGuids)
        {
            foreach (var path in Find("t:Scene"))
            {
                if (path.StartsWith("Assets/Settings/", StringComparison.Ordinal)) continue;
                string text = File.ReadAllText(path);
                if (cueScriptGuids.Any(g => text.Contains(g))) yield return path;
            }
        }

        private static HashSet<string> TypesWithCueFields()
        {
            var guids = new HashSet<string>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (type == typeof(SoundEmitter) || !HasCueField(type, 0)) continue;
                foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
                    if (script != null && script.GetClass() == type)
                        guids.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script)));
            }
            return guids;
        }

        private static bool HasCueField(Type type, int depth)
        {
            if (depth > 3 || type == null) return false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (var f in t.GetFields(flags | BindingFlags.DeclaredOnly))
                {
                    var ft = f.FieldType.IsArray ? f.FieldType.GetElementType() : f.FieldType;
                    if (ft == typeof(SoundCue) || f.IsDefined(typeof(MusicSlotAttribute), false)) return true;
                    if (ft != null && ft.IsClass && ft.IsSerializable && ft.Namespace != null &&
                        ft.Namespace.StartsWith("RedMagic", StringComparison.Ordinal) && HasCueField(ft, depth + 1))
                        return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ campos SoundCue

        private static void AddCueFields(SerializedObject so, string path, string guid, string scene,
                                         string objectPath, string componentType, List<SoundSlot> slots)
        {
            var it = so.GetIterator();
            bool enter = true;
            while (it.Next(enter))
            {
                enter = true;
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var clipField = SoundSlotRules.FieldOf(it);
                    if (clipField == null || !clipField.IsDefined(typeof(MusicSlotAttribute), false)) continue;

                    var music = NewSlot(SoundSlotKind.MusicField, path, guid, scene, objectPath, componentType, it.propertyPath);
                    music.PropertyPath = it.propertyPath;
                    if (it.objectReferenceValue is AudioClip clip) music.Clips.Add(clip);
                    slots.Add(music);
                    continue;
                }

                if (it.propertyType != SerializedPropertyType.Generic || it.type != nameof(SoundCue)) continue;
                enter = false;   // no bajar dentro de la cue

                var field = SoundSlotRules.FieldOf(it);
                if (!SoundSlotRules.IsActive(it, field)) continue;

                var slot = NewSlot(SoundSlotKind.CueField, path, guid, scene, objectPath, componentType, it.propertyPath);
                slot.PropertyPath = it.propertyPath;
                ReadClips(it, slot.Clips);
                slots.Add(slot);
            }
        }

        // ------------------------------------------------------------------ utilidades

        // objectPath: ruta relativa a la raíz del prefab ("" = la raíz) o ruta completa en la escena.
        private static SoundSlot NewSlot(SoundSlotKind kind, string path, string guid, string scene,
                                         string objectPath, string componentType, string member)
        {
            string where = string.IsNullOrEmpty(objectPath) ? "" : objectPath + "/";
            return new SoundSlot
            {
                Kind = kind,
                AssetPath = path,
                AssetGuid = guid,
                ScenePath = scene,
                ObjectPath = objectPath,
                ComponentType = componentType,
                Member = member,
                Id = $"{guid}/{where}{componentType}/{member}",
            };
        }

        private static void ReadClips(SerializedProperty cue, List<AudioClip> into)
        {
            var clips = cue.FindPropertyRelative("clips");
            if (clips == null) return;
            for (int i = 0; i < clips.arraySize; i++)
                if (clips.GetArrayElementAtIndex(i).objectReferenceValue is AudioClip clip) into.Add(clip);
        }

        private static IEnumerable<string> Find(string filter) =>
            AssetDatabase.FindAssets(filter, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !Vendored.Any(v => p.StartsWith(v, StringComparison.Ordinal)) && !IsIgnored(p))
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal);

        // Heredado de otro prefab (anidado o variante): se cuenta en su propio asset, no dos veces.
        private static bool Inherited(Component c) => PrefabUtility.GetCorrespondingObjectFromSource(c) != null;

        public static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
