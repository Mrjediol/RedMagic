using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Gameplay;
using RedMagic.Hub;
using RedMagic.Run;
using RedMagic.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Audio.EditorTools
{
    /// <summary>
    /// TEMPORAL (audio fase 4) — se borra junto con los campos que migra.
    ///
    /// Pasa los ids de la tabla antigua (campos ocultos TEMPORAL) a sus huecos nuevos
    /// (entrada de SoundEmitter, SoundCue o AudioClip), vacía el campo y borra las copias en escena
    /// del AudioManager con su tabla. Idempotente: un campo vacío y una escena sin copia no hacen nada.
    ///
    /// Nunca adivina: antes de escribir nada hace una pasada en seco sobre el mismo alcance y, si
    /// encuentra una tabla que no coincide con la de MainMenu (la que manda en un build), un id que
    /// no está en la tabla, un hueco ya ocupado por otro clip o una copia del AudioManager con más
    /// cosas encima, para y lo informa sin tocar nada.
    ///
    /// Un id cuya entrada existe pero no tiene clip no pierde nada al vaciarse: se registra y se vacía.
    /// Todo lo que hace queda en consola y en <c>docs/audio-migration-log.md</c>.
    /// </summary>
    public static class AudioMigrationTool
    {
        private const string Menu = "Tools/RedMagic/Audio/Migración (temporal)/";
        private const string CanonicalScene = "Assets/Scenes/MainMenu.unity";
        private const string LogPath = "docs/audio-migration-log.md";
        private static readonly string[] VendoredFolders = { "Assets/Brackeys", "Assets/Cainos", "Assets/Dragon Warrior Files" };

        private enum Target { Emitter, Cue, MusicClip, UiClick }

        private sealed class Rule
        {
            public Type Type; public string Field; public Target Target;
            public SoundTrigger Trigger; public string Slot;
        }

        private static Rule E(Type t, string f, SoundTrigger trg) => new Rule { Type = t, Field = f, Target = Target.Emitter, Trigger = trg };
        private static Rule C(Type t, string f, string slot) => new Rule { Type = t, Field = f, Target = Target.Cue, Slot = slot };

        private static readonly Rule[] Rules =
        {
            E(typeof(Health), "hurtSfxId", SoundTrigger.OnHit),
            E(typeof(Health), "deathSfxId", SoundTrigger.OnDeath),
            E(typeof(PlayerMovement), "jumpSfxId", SoundTrigger.OnJump),
            E(typeof(PlayerMovement), "footstepSfxId", SoundTrigger.Footstep),
            E(typeof(PlayerMovement), "dashSfxId", SoundTrigger.OnDash),
            E(typeof(PlayerAttack), "attackSfxId", SoundTrigger.OnAttack),
            E(typeof(RangedAttack), "attackSfxId", SoundTrigger.OnAttack),
            E(typeof(HubLootContainer), "openSfxId", SoundTrigger.OnInteract),
            E(typeof(HubLootContainer), "lootSfxId", SoundTrigger.OnLoot),
            E(typeof(AnvilInteractable), "useSfxId", SoundTrigger.OnInteract),
            E(typeof(MirrorInteractable), "useSfxId", SoundTrigger.OnInteract),
            C(typeof(SectionClearTracker), "clearSfxId", "clearSound"),
            C(typeof(ShopManager), "denySfxId", "denySound"),
            C(typeof(ShopManager), "rerollSfxId", "rerollSound"),
            new Rule { Type = typeof(MainMenuController), Field = "menuMusicId", Target = Target.MusicClip, Slot = "menuMusic" },
            new Rule { Type = typeof(PassiveDropCinematicSettings), Field = "continueSfxId", Target = Target.UiClick },
        };

        private sealed class Entry
        {
            public string Id;
            public AudioClip Clip;
            public float Volume, Pitch;
            public bool Loop;
            public UnityEngine.Object Group;

            public bool SameAs(Entry o) =>
                Id == o.Id && Clip == o.Clip && Mathf.Approximately(Volume, o.Volume) &&
                Mathf.Approximately(Pitch, o.Pitch) && Loop == o.Loop && Group == o.Group;

            public override string ToString() =>
                $"{Id} (clip {(Clip != null ? Clip.name : "—")}, vol {Volume:0.##}, pitch {Pitch:0.##}, loop {Loop}, grupo {(Group != null ? Group.name : "—")})";
        }

        private sealed class MigrationRun
        {
            public bool Dry;
            public List<Entry> Canonical;
            public readonly List<string> Log = new List<string>();
            public readonly List<string> Problems = new List<string>();
            public readonly List<string> Pending = new List<string>();
            public int Changes;
            public void Info(string s) => Log.Add(s);
            public void Problem(string s) => Problems.Add(s);
            public void MissingSound(string s) => Pending.Add(s);
        }

        // ------------------------------------------------------------------ menús / entradas

        [MenuItem(Menu + "Previsualizar todo (sin cambios)")]
        public static void PreviewAll() => Execute("Previsualización (todo)", dryOnly: true, assets: true, AllScenes());

        [MenuItem(Menu + "Migrar assets (prefabs y ScriptableObjects)")]
        public static void MigrateAssets() => Execute("Assets", dryOnly: false, assets: true, new string[0]);

        [MenuItem(Menu + "Migrar todas las escenas (MainMenu la última)")]
        public static void MigrateAllScenes() => Execute("Todas las escenas", dryOnly: false, assets: false, AllScenes());

        /// <summary>Para la CLI: assets + una escena concreta.</summary>
        public static void MigrateAssetsAndScene(string scenePath) =>
            Execute($"Assets + {scenePath}", dryOnly: false, assets: true, new[] { scenePath });

        /// <summary>Todas las escenas con algo que migrar, MainMenu (la tabla de referencia) la última.</summary>
        public static string[] AllScenes() =>
            AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !IsVendored(p) && !p.StartsWith("Assets/Settings/"))
                .OrderBy(p => p == CanonicalScene ? 1 : 0).ThenBy(p => p, StringComparer.Ordinal)
                .ToArray();

        private static void Execute(string title, bool dryOnly, bool assets, string[] scenes)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[AudioMigration] Sal del modo Play antes de migrar.");
                return;
            }

            // Pasada en seco: si hay algo dudoso, no se toca nada.
            var preview = new MigrationRun { Dry = true, Canonical = ReadCanonical() };
            Process(preview, assets, scenes);

            if (dryOnly || preview.Problems.Count > 0)
            {
                Report(title + (dryOnly ? "" : " — ABORTADO, sin cambios"), preview);
                return;
            }

            var real = new MigrationRun { Dry = false, Canonical = preview.Canonical };
            Process(real, assets, scenes);
            AssetDatabase.SaveAssets();
            Report(title, real);
        }

        private static void Process(MigrationRun run, bool assets, string[] scenes)
        {
            if (assets) ProcessAssets(run);
            foreach (var scene in scenes) ProcessScene(run, scene);
        }

        // ------------------------------------------------------------------ tabla de referencia

        private static List<Entry> ReadCanonical()
        {
            var scene = OpenScene(CanonicalScene, out bool openedHere);
            try
            {
                var managers = Find<AudioManager>(scene);
                return managers.Count == 0 ? null : ReadTable(managers[0]);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static List<Entry> ReadTable(AudioManager manager)
        {
            var list = new List<Entry>();
            var sounds = new SerializedObject(manager).FindProperty("sounds");
            for (int i = 0; i < sounds.arraySize; i++)
            {
                var e = sounds.GetArrayElementAtIndex(i);
                list.Add(new Entry
                {
                    Id = e.FindPropertyRelative("id").stringValue,
                    Clip = e.FindPropertyRelative("clip").objectReferenceValue as AudioClip,
                    Volume = e.FindPropertyRelative("volume").floatValue,
                    Pitch = e.FindPropertyRelative("pitch").floatValue,
                    Loop = e.FindPropertyRelative("loop").boolValue,
                    Group = e.FindPropertyRelative("mixerGroup").objectReferenceValue,
                });
            }
            return list;
        }

        // ------------------------------------------------------------------ assets

        private static void ProcessAssets(MigrationRun run)
        {
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                if (IsVendored(path)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !HasPending(asset.GetComponentsInChildren<Component>(true))) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int before = run.Changes;
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                        MigrateComponent(run, component, $"{path} ▸ {PathOf(component)}");
                    if (!run.Dry && run.Changes > before) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            foreach (var path in AssetDatabase.FindAssets("t:PassiveDropCinematicSettings").Select(AssetDatabase.GUIDToAssetPath))
            {
                var settings = AssetDatabase.LoadAssetAtPath<PassiveDropCinematicSettings>(path);
                int before = run.Changes;
                MigrateObject(run, settings, null, path);
                if (!run.Dry && run.Changes > before) EditorUtility.SetDirty(settings);
            }
        }

        // ------------------------------------------------------------------ escenas

        private static void ProcessScene(MigrationRun run, string path)
        {
            var scene = OpenScene(path, out bool openedHere);
            try
            {
                if (!openedHere && scene.isDirty)
                {
                    run.Problem($"{path}: está abierta con cambios sin guardar; guárdala o descártala antes de migrar.");
                    return;
                }

                int before = run.Changes;

                foreach (var manager in Find<AudioManager>(scene))
                    RemoveSceneCopy(run, path, manager);

                foreach (var root in scene.GetRootGameObjects())
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                        MigrateComponent(run, component, $"{path} ▸ {PathOf(component)}");

                if (!run.Dry && run.Changes > before)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void RemoveSceneCopy(MigrationRun run, string scenePath, AudioManager manager)
        {
            string where = $"{scenePath} ▸ {PathOf(manager)}";

            if (run.Canonical == null)
            {
                run.Problem($"{where}: hay una copia del AudioManager pero MainMenu ya no tiene la tabla de referencia para compararla.");
                return;
            }

            var table = ReadTable(manager);
            var drift = Diff(run.Canonical, table);
            if (drift != null)
            {
                run.Problem($"{where}: la tabla NO coincide con la de MainMenu — {drift}");
                return;
            }

            var go = manager.gameObject;
            var extras = go.GetComponents<Component>()
                .Where(c => !(c is Transform) && !(c is AudioManager) && !(c is AudioListener))
                .Select(c => c.GetType().Name).ToList();
            if (extras.Count > 0 || go.transform.childCount > 0)
            {
                run.Problem($"{where}: el GameObject lleva más cosas (componentes: {string.Join(", ", extras)}; hijos: {go.transform.childCount}); no se borra a ciegas.");
                return;
            }

            run.Info($"{where}: copia del AudioManager borrada — tabla de {table.Count} entradas idéntica a MainMenu" +
                     (go.GetComponent<AudioListener>() != null ? "; su AudioListener se va con ella (lo pone el prefab)." : "."));
            run.Changes++;
            if (!run.Dry) UnityEngine.Object.DestroyImmediate(go);
        }

        private static string Diff(List<Entry> canonical, List<Entry> table)
        {
            if (canonical.Count != table.Count) return $"{table.Count} entradas frente a {canonical.Count}";
            for (int i = 0; i < table.Count; i++)
                if (!canonical[i].SameAs(table[i]))
                    return $"entrada {i}: {table[i]} frente a {canonical[i]}";
            return null;
        }

        // ------------------------------------------------------------------ campos

        private static bool HasPending(Component[] components)
        {
            foreach (var component in components)
            {
                if (component == null) continue;
                foreach (var rule in Rules)
                {
                    if (!rule.Type.IsInstanceOfType(component)) continue;
                    var prop = new SerializedObject(component).FindProperty(rule.Field);
                    if (prop != null && !string.IsNullOrEmpty(prop.stringValue)) return true;
                }
            }
            return false;
        }

        private static void MigrateComponent(MigrationRun run, Component component, string where)
        {
            if (component == null) return;
            MigrateObject(run, component, component.gameObject, where);
        }

        private static void MigrateObject(MigrationRun run, UnityEngine.Object owner, GameObject host, string where)
        {
            foreach (var rule in Rules)
            {
                if (!rule.Type.IsInstanceOfType(owner)) continue;

                var so = new SerializedObject(owner);
                var prop = so.FindProperty(rule.Field);
                if (prop == null)
                {
                    run.Problem($"{where}: {rule.Type.Name} no tiene el campo '{rule.Field}'.");
                    continue;
                }

                string id = prop.stringValue;
                if (string.IsNullOrEmpty(id)) continue;

                string label = $"{where} · {rule.Type.Name}.{rule.Field} = '{id}'";

                // Un valor heredado de un prefab (instancia en escena, variante, prefab anidado) se
                // migra en ese prefab, no en la instancia: aquí no se toca.
                if (PrefabUtility.IsPartOfPrefabInstance(owner) && !prop.prefabOverride)
                    continue;

                var entry = run.Canonical?.FirstOrDefault(e => e.Id == id);
                if (entry == null)
                {
                    run.Problem($"{label}: ese id no está en la tabla de MainMenu (o ya no hay tabla); no se sabe a qué clip corresponde.");
                    continue;
                }

                if (entry.Clip == null)
                {
                    run.MissingSound($"{label}: la entrada no tiene clip; se vacía el campo. Sonido que falta por hacer.");
                }
                else if (!WriteTarget(run, rule, entry, so, host, label))
                {
                    continue;   // conflicto ya registrado: el campo se queda como está
                }

                run.Changes++;
                if (!run.Dry)
                {
                    prop.stringValue = string.Empty;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static bool WriteTarget(MigrationRun run, Rule rule, Entry entry, SerializedObject so, GameObject host, string label)
        {
            switch (rule.Target)
            {
                case Target.Emitter:
                {
                    var emitter = host.GetComponent<SoundEmitter>();
                    if (emitter != null)
                    {
                        var events = new SerializedObject(emitter).FindProperty("soundEvents");
                        for (int i = 0; i < events.arraySize; i++)
                        {
                            var e = events.GetArrayElementAtIndex(i);
                            if (e.FindPropertyRelative("trigger").enumValueIndex != (int)rule.Trigger) continue;
                            var cue = e.FindPropertyRelative("cue");
                            if (!CueHasClips(cue)) continue;
                            if (CueHasClip(cue, entry.Clip))
                            {
                                run.Info($"{label}: SoundEmitter ▸ {rule.Trigger} ya tiene '{entry.Clip.name}'; sólo se vacía el campo.");
                                return true;
                            }
                            run.Problem($"{label}: SoundEmitter ▸ {rule.Trigger} ya tiene otros clips; no se mezclan.");
                            return false;
                        }
                    }

                    run.Info($"{label}: → SoundEmitter ▸ {rule.Trigger} = {entry}" + (emitter == null ? " (se añade SoundEmitter)" : ""));
                    if (run.Dry) return true;

                    if (emitter == null) emitter = host.AddComponent<SoundEmitter>();
                    var eso = new SerializedObject(emitter);
                    var list = eso.FindProperty("soundEvents");
                    list.arraySize++;
                    var added = list.GetArrayElementAtIndex(list.arraySize - 1);
                    added.FindPropertyRelative("trigger").enumValueIndex = (int)rule.Trigger;
                    WriteCue(added.FindPropertyRelative("cue"), entry);
                    eso.ApplyModifiedPropertiesWithoutUndo();
                    return true;
                }

                case Target.Cue:
                {
                    var cue = so.FindProperty(rule.Slot);
                    if (CueHasClips(cue))
                    {
                        if (CueHasClip(cue, entry.Clip))
                        {
                            run.Info($"{label}: {rule.Slot} ya tiene '{entry.Clip.name}'; sólo se vacía el campo.");
                            return true;
                        }
                        run.Problem($"{label}: {rule.Slot} ya tiene otros clips; no se mezclan.");
                        return false;
                    }

                    run.Info($"{label}: → {rule.Slot} = {entry}");
                    if (!run.Dry) WriteCue(cue, entry);
                    return true;
                }

                case Target.MusicClip:
                {
                    var clip = so.FindProperty(rule.Slot);
                    if (clip.objectReferenceValue != null && clip.objectReferenceValue != entry.Clip)
                    {
                        run.Problem($"{label}: {rule.Slot} ya tiene otro clip ('{clip.objectReferenceValue.name}').");
                        return false;
                    }
                    if (!Mathf.Approximately(entry.Volume, 1f) || !Mathf.Approximately(entry.Pitch, 1f))
                    {
                        run.Problem($"{label}: la entrada tiene volumen/pitch distintos de 1 y {rule.Slot} es un AudioClip sin esos ajustes.");
                        return false;
                    }

                    run.Info($"{label}: → {rule.Slot} = '{entry.Clip.name}'");
                    if (!run.Dry) clip.objectReferenceValue = entry.Clip;
                    return true;
                }

                default:
                    run.Problem($"{label}: el id tiene clip ('{entry.Clip.name}') y su destino es el clic de UI del prefab AudioManager; " +
                                "asígnalo a mano en AudioManager ▸ UI Click y vuelve a lanzar la migración.");
                    return false;
            }
        }

        private static void WriteCue(SerializedProperty cue, Entry entry)
        {
            var clips = cue.FindPropertyRelative("clips");
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = entry.Clip;
            cue.FindPropertyRelative("volume").floatValue = entry.Volume;
            cue.FindPropertyRelative("pitchMin").floatValue = entry.Pitch;
            cue.FindPropertyRelative("pitchMax").floatValue = entry.Pitch;
            cue.FindPropertyRelative("noRepeatLast").boolValue = true;
            cue.FindPropertyRelative("priority").enumValueIndex = (int)SoundPriority.Normal;
            cue.FindPropertyRelative("positional").boolValue = false;
            cue.FindPropertyRelative("rolloffStart").floatValue = SoundCue.DefaultRolloffStart;
            cue.FindPropertyRelative("rolloffEnd").floatValue = SoundCue.DefaultRolloffEnd;
            cue.FindPropertyRelative("initialized").boolValue = true;
        }

        private static bool CueHasClips(SerializedProperty cue)
        {
            var clips = cue.FindPropertyRelative("clips");
            for (int i = 0; i < clips.arraySize; i++)
                if (clips.GetArrayElementAtIndex(i).objectReferenceValue != null) return true;
            return false;
        }

        private static bool CueHasClip(SerializedProperty cue, AudioClip clip)
        {
            var clips = cue.FindPropertyRelative("clips");
            for (int i = 0; i < clips.arraySize; i++)
                if (clips.GetArrayElementAtIndex(i).objectReferenceValue == clip) return true;
            return false;
        }

        // ------------------------------------------------------------------ utilidades

        private static Scene OpenScene(string path, out bool openedHere)
        {
            var scene = SceneManager.GetSceneByPath(path);
            openedHere = !scene.IsValid() || !scene.isLoaded;
            return openedHere ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
        }

        private static List<T> Find<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (var root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }

        private static bool IsVendored(string path) => VendoredFolders.Any(f => path.StartsWith(f + "/"));

        private static string PathOf(Component c)
        {
            var t = c.transform;
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        private static void Report(string title, MigrationRun run)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {title} ({(run.Dry ? "en seco" : "aplicado")})");
            sb.AppendLine();
            sb.AppendLine($"Cambios: {run.Changes} (sin clip: {run.Pending.Count}) · Problemas: {run.Problems.Count}");
            sb.AppendLine();
            if (run.Problems.Count > 0)
            {
                sb.AppendLine("### Problemas (nada se ha tocado)");
                foreach (var p in run.Problems) sb.AppendLine("- " + p);
                sb.AppendLine();
            }
            if (run.Log.Count > 0)
            {
                sb.AppendLine("### Cambios");
                foreach (var l in run.Log) sb.AppendLine("- " + l);
                sb.AppendLine();
            }
            if (run.Pending.Count > 0)
            {
                sb.AppendLine("### Vaciados sin clip — sonidos pendientes");
                foreach (var l in run.Pending) sb.AppendLine("- " + l);
                sb.AppendLine();
            }

            string text = sb.ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(LogPath)));
            File.AppendAllText(LogPath, text + Environment.NewLine);

            if (run.Problems.Count > 0) Debug.LogWarning("[AudioMigration] " + text);
            else Debug.Log("[AudioMigration] " + text);
        }
    }
}
