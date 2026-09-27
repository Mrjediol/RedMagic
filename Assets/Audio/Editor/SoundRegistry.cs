using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Audio.EditorTools
{
    public enum SoundStatus { Pending, Perfect, Good, Bad, Horrible }

    public enum ClipSource { Own, InheritedPlaceholder, GenericTest, None }

    /// <summary>Una fila del registro: el hueco más lo que el proyecto no puede derivar (la nota).</summary>
    public sealed class SoundRegistryEntry
    {
        public string Id;
        public string Name;
        public string Category;
        public SoundSlotKind Kind;
        /// <summary>Discovered (escáner), Convention (música por nombre) o Declared (a mano).</summary>
        public string Origin;
        public string DeclaredKey;
        public ClipSource ClipSource;
        public List<string> Clips = new List<string>();
        public SoundSlot Slot;       // null en declarados y en música por convención
        public string MusicName;     // sólo MusicConvention: el nombre en Resources/Music
        public string Notes;         // del declarado
    }

    /// <summary>Nota y calificación del usuario para un hueco (SoundStatus.json).</summary>
    public sealed class SoundStatusRecord
    {
        public SoundStatus Status = SoundStatus.Pending;
        public string Notes = "";
        public string LastModified = "";
        /// <summary>Hueco retirado a propósito desde la pestaña: sin clips y oculto.</summary>
        public bool Removed;
    }

    /// <summary>
    /// El registro de sonidos, en tres ficheros de <see cref="Folder"/>:
    /// <list type="bullet">
    /// <item><c>SoundRegistry.generated.json</c> — derivado del proyecto (escáner + música por convención
    /// + declarados). Nunca se edita a mano; regenerarlo dos veces da el mismo fichero byte a byte.</item>
    /// <item><c>SoundDeclared.json</c> — los pocos huecos cuyo momento aún no existe en el juego. Cuando
    /// el momento se implementa, el escáner lo descubre y se funde con el declarado por su clave
    /// (componente + campo/momento): no se duplica.</item>
    /// <item><c>SoundStatus.json</c> — calificaciones y notas del usuario, por id estable (GUID del asset +
    /// componente + campo/momento). Regenerar el registro nunca lo toca, salvo para pasar la nota de un
    /// declarado a su id definitivo cuando se funde.</item>
    /// </list>
    /// </summary>
    public static class SoundRegistry
    {
        public const string Folder = "Assets/Audio/Registry";
        public const string GeneratedPath = Folder + "/SoundRegistry.generated.json";
        public const string DeclaredPath = Folder + "/SoundDeclared.json";
        public const string StatusPath = Folder + "/SoundStatus.json";
        public const string MusicResourceFolder = "Assets/Resources/Music";
        private const string DeclaredPrefix = "declared:";

        [MenuItem("Tools/RedMagic/Audio/3 · Regenerar registro de sonidos")]
        private static void RegenerateMenu()
        {
            var entries = Regenerate(out string report, out bool ok);
            if (ok) Debug.Log("[SoundRegistry] " + report);
            else Debug.LogError("[SoundRegistry] " + report);
        }

        /// <summary>
        /// Escanea, funde con los declarados y escribe el fichero generado. Si el escáner encuentra un
        /// hueco que no debería existir, no escribe nada y lo cuenta en <paramref name="report"/>.
        /// </summary>
        public static List<SoundRegistryEntry> Regenerate(out string report, out bool ok)
        {
            var problems = new List<string>();
            var slots = SoundSlotScanner.Scan(problems);
            if (problems.Count > 0)
            {
                ok = false;
                report = "El escáner encontró huecos que no deberían existir; no se escribe el registro:\n- " +
                         string.Join("\n- ", problems);
                return null;
            }

            var entries = Build(slots, LoadDeclared(), out var merged);
            MigrateDeclaredStatus(merged);
            WriteGenerated(entries);

            ok = true;
            report = Summary(entries, LoadStatus());
            return entries;
        }

        // ------------------------------------------------------------------ construir

        /// <summary>Recalcula clips y origen de una fila tras cambiar su hueco (sin volver a escanear).</summary>
        public static void Refresh(SoundRegistryEntry e)
        {
            var test = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundSlotEditor.GenericTestClipPath);
            if (e.Kind == SoundSlotKind.MusicConvention)
            {
                var music = FindMusic(e.MusicName);
                e.Clips = music != null ? new List<string> { music } : new List<string>();
                e.ClipSource = music != null ? ClipSource.Own : ClipSource.None;
                return;
            }
            if (e.Slot == null) return;
            e.Clips = e.Slot.Clips.Select(AssetDatabase.GetAssetPath).ToList();
            e.ClipSource = e.Slot.UsesFallback ? ClipSource.InheritedPlaceholder
                         : e.Slot.Clips.Count == 0 ? ClipSource.None
                         : e.Slot.Clips.Any(c => c == test) ? ClipSource.GenericTest
                         : ClipSource.Own;
        }

        /// <summary>El registro tal y como está en disco (sin volver a escanear).</summary>
        public static List<SoundRegistryEntry> LoadGenerated()
        {
            var list = new List<SoundRegistryEntry>();
            if (!File.Exists(GeneratedPath)) return list;

            var root = JObject.Parse(File.ReadAllText(GeneratedPath));
            foreach (var o in (root["slots"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                var e = new SoundRegistryEntry
                {
                    Id = (string)o["id"],
                    Name = (string)o["name"],
                    Category = (string)o["category"],
                    Kind = Enum.TryParse((string)o["kind"], out SoundSlotKind k) ? k : SoundSlotKind.Declared,
                    Origin = (string)o["origin"],
                    DeclaredKey = (string)o["declaredKey"],
                    ClipSource = Enum.TryParse((string)o["clipSource"], out ClipSource c) ? c : ClipSource.None,
                    Clips = (o["clips"] as JArray)?.Select(x => (string)x).ToList() ?? new List<string>(),
                    MusicName = (string)o["musicName"],
                    Notes = (string)o["notes"],
                };

                if (o["owner"] is JObject owner)
                {
                    e.Slot = new SoundSlot
                    {
                        Id = e.Id,
                        Kind = e.Kind,
                        AssetPath = (string)owner["asset"],
                        AssetGuid = (string)owner["guid"],
                        ScenePath = (string)owner["scene"] ?? "",
                        ObjectPath = (string)owner["object"] ?? "",
                        ComponentType = (string)owner["component"],
                        Member = (string)owner["member"],
                        PropertyPath = (string)owner["property"] ?? "",
                        EntryMissing = (bool?)owner["entryMissing"] ?? false,
                        UsesFallback = e.ClipSource == ClipSource.InheritedPlaceholder,
                    };
                    if (e.Kind == SoundSlotKind.EmitterTrigger &&
                        Enum.TryParse(e.Slot.Member.Split('#')[0], out SoundTrigger trigger))
                        e.Slot.Trigger = trigger;
                    foreach (var path in e.Clips)
                        if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) is AudioClip clip) e.Slot.Clips.Add(clip);
                }
                list.Add(e);
            }
            return list;
        }

        private static List<SoundRegistryEntry> Build(List<SoundSlot> slots, List<JObject> declared,
                                                      out Dictionary<string, string> merged)
        {
            merged = new Dictionary<string, string>();   // clave declarada → id descubierto
            SoundCategories.Reset();
            var entries = new List<SoundRegistryEntry>();
            var test = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundSlotEditor.GenericTestClipPath);

            foreach (var slot in slots)
            {
                var entry = new SoundRegistryEntry
                {
                    Id = slot.Id,
                    Kind = slot.Kind,
                    Origin = "Discovered",
                    Slot = slot,
                    Clips = slot.Clips.Select(AssetDatabase.GetAssetPath).ToList(),
                };
                SoundCategories.Describe(slot, out entry.Category, out entry.Name);
                entry.ClipSource = slot.UsesFallback ? ClipSource.InheritedPlaceholder
                                 : slot.Clips.Count == 0 ? ClipSource.None
                                 : slot.Clips.Any(c => c == test) ? ClipSource.GenericTest
                                 : ClipSource.Own;

                var match = declared.FirstOrDefault(d => (string)d["matchComponent"] == slot.ComponentType &&
                                                         (string)d["matchMember"] == slot.Member);
                if (match != null)
                {
                    entry.DeclaredKey = (string)match["key"];
                    merged[entry.DeclaredKey] = entry.Id;
                }
                entries.Add(entry);
            }

            entries.AddRange(MusicByConvention());

            foreach (var d in declared)
            {
                string key = (string)d["key"];
                if (string.IsNullOrEmpty(key) || merged.ContainsKey(key)) continue;
                entries.Add(new SoundRegistryEntry
                {
                    Id = DeclaredPrefix + key,
                    Name = (string)d["name"] ?? key,
                    Category = (string)d["category"] ?? "Declared",
                    Kind = SoundSlotKind.Declared,
                    Origin = "Declared",
                    DeclaredKey = key,
                    ClipSource = ClipSource.None,
                    Notes = (string)d["notes"] ?? "",
                });
            }

            entries.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Category, b.Category);
                if (c == 0) c = string.CompareOrdinal(a.Name, b.Name);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            return entries;
        }

        // Música de escena por la convención de RunManager: hub, tienda, World{n}-{k}, BossBattle{n}.
        private static IEnumerable<SoundRegistryEntry> MusicByConvention()
        {
            var names = new List<(string name, string label)>();

            foreach (var (key, label) in new[] { ("hubMusicId", "Hub"), ("shopMusicId", "Shop") })
            {
                string value = RunManagerValue(key);
                if (!string.IsNullOrEmpty(value)) names.Add((value, label));
            }

            foreach (var guid in AssetDatabase.FindAssets("t:WorldDefinition", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (SoundSlotScanner.IsIgnored(path)) continue;
                var world = AssetDatabase.LoadAssetAtPath<Run.WorldDefinition>(path);
                if (world == null) continue;

                int n = world.WorldNumber;
                for (int k = 1; k <= world.SectionsPerRun; k++) names.Add(($"World{n}-{k}", $"World {n} · Section {k}"));
                names.Add(($"BossBattle{n}", $"World {n} · Boss"));
            }

            foreach (var (name, label) in names.Distinct())
            {
                var clip = FindMusic(name);
                yield return new SoundRegistryEntry
                {
                    Id = "music/" + name,
                    Name = label,
                    Category = "Music",
                    Kind = SoundSlotKind.MusicConvention,
                    Origin = "Convention",
                    MusicName = name,
                    Clips = clip != null ? new List<string> { clip } : new List<string>(),
                    ClipSource = clip != null ? ClipSource.Own : ClipSource.None,
                };
            }
        }

        /// <summary>Ruta del clip <c>Resources/Music/&lt;name&gt;.*</c>, o null.</summary>
        public static string FindMusic(string name)
        {
            if (!AssetDatabase.IsValidFolder(MusicResourceFolder)) return null;
            return AssetDatabase.FindAssets("t:AudioClip", new[] { MusicResourceFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
        }

        // hubMusicId / shopMusicId del RunManager, leídos del YAML de las escenas (todas deben coincidir).
        private static string RunManagerValue(string field)
        {
            string found = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                foreach (var line in File.ReadLines(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    string t = line.Trim();
                    if (!t.StartsWith(field + ":", StringComparison.Ordinal)) continue;
                    found ??= t.Substring(field.Length + 1).Trim();
                }
            }
            return found;
        }

        // Al fundirse un declarado con su hueco real, su nota pasa a la clave definitiva.
        private static void MigrateDeclaredStatus(Dictionary<string, string> merged)
        {
            if (merged.Count == 0) return;
            var status = LoadStatus();
            bool changed = false;
            foreach (var pair in merged)
            {
                string oldKey = DeclaredPrefix + pair.Key;
                if (!status.TryGetValue(oldKey, out var record) || status.ContainsKey(pair.Value)) continue;
                status[pair.Value] = record;
                status.Remove(oldKey);
                changed = true;
            }
            if (changed) SaveStatus(status);
        }

        // ------------------------------------------------------------------ ficheros

        /// <summary>Reescribe el fichero generado con las filas en memoria (tras asignar un clip desde la pestaña).</summary>
        public static void WriteGenerated(List<SoundRegistryEntry> entries)
        {
            var array = new JArray();
            foreach (var e in entries)
            {
                var o = new JObject
                {
                    ["id"] = e.Id,
                    ["name"] = e.Name,
                    ["category"] = e.Category,
                    ["kind"] = e.Kind.ToString(),
                    ["origin"] = e.Origin,
                    ["clipSource"] = e.ClipSource.ToString(),
                    ["clips"] = new JArray(e.Clips.Where(c => !string.IsNullOrEmpty(c)).Cast<object>().ToArray()),
                };
                if (!string.IsNullOrEmpty(e.DeclaredKey)) o["declaredKey"] = e.DeclaredKey;
                if (e.Slot != null)
                {
                    var owner = new JObject
                    {
                        ["asset"] = e.Slot.AssetPath,
                        ["guid"] = e.Slot.AssetGuid,
                        ["component"] = e.Slot.ComponentType,
                        ["member"] = e.Slot.Member,
                    };
                    if (!string.IsNullOrEmpty(e.Slot.ScenePath)) owner["scene"] = e.Slot.ScenePath;
                    if (!string.IsNullOrEmpty(e.Slot.ObjectPath)) owner["object"] = e.Slot.ObjectPath;
                    if (!string.IsNullOrEmpty(e.Slot.PropertyPath)) owner["property"] = e.Slot.PropertyPath;
                    if (e.Slot.EntryMissing) owner["entryMissing"] = true;
                    o["owner"] = owner;
                }
                if (!string.IsNullOrEmpty(e.MusicName)) o["musicName"] = e.MusicName;
                if (!string.IsNullOrEmpty(e.Notes)) o["notes"] = e.Notes;
                array.Add(o);
            }

            var root = new JObject
            {
                ["_comment"] = "GENERADO por Tools > RedMagic > Audio > 3. No editar a mano: se sobrescribe. " +
                               "Calificaciones y notas: SoundStatus.json. Huecos sin momento aún: SoundDeclared.json.",
                ["slots"] = array,
            };
            WriteJson(GeneratedPath, root);
        }

        public static List<JObject> LoadDeclared()
        {
            if (!File.Exists(DeclaredPath)) return new List<JObject>();
            var root = JObject.Parse(File.ReadAllText(DeclaredPath));
            return (root["slots"] as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
        }

        /// <summary>Añade un hueco declarado a mano (la pestaña Sounds: "Añadir hueco").</summary>
        public static void AddDeclared(string key, string name, string category, string notes,
                                       string matchComponent = "", string matchMember = "")
        {
            var list = LoadDeclared();
            if (list.Any(d => (string)d["key"] == key)) throw new InvalidOperationException($"Ya existe '{key}'.");
            list.Add(new JObject
            {
                ["key"] = key,
                ["name"] = name,
                ["category"] = category,
                ["matchComponent"] = matchComponent,
                ["matchMember"] = matchMember,
                ["notes"] = notes,
            });
            SaveDeclared(list);
        }

        public static void RemoveDeclared(string key)
        {
            var list = LoadDeclared();
            list.RemoveAll(d => (string)d["key"] == key);
            SaveDeclared(list);
        }

        private static void SaveDeclared(List<JObject> list)
        {
            var sorted = list.OrderBy(d => (string)d["key"], StringComparer.Ordinal);
            WriteJson(DeclaredPath, new JObject
            {
                ["_comment"] = "Huecos de sonido cuyo momento aún no existe en el juego. matchComponent + matchMember " +
                               "es la clave con la que se funden con el hueco real cuando el escáner lo descubra.",
                ["slots"] = new JArray(sorted.Cast<object>().ToArray()),
            });
        }

        public static Dictionary<string, SoundStatusRecord> LoadStatus()
        {
            var map = new Dictionary<string, SoundStatusRecord>();
            if (!File.Exists(StatusPath)) return map;

            var root = JObject.Parse(File.ReadAllText(StatusPath));
            if (!(root["slots"] is JObject slots)) return map;
            foreach (var p in slots.Properties())
            {
                var o = (JObject)p.Value;
                map[p.Name] = new SoundStatusRecord
                {
                    Status = Enum.TryParse((string)o["status"], out SoundStatus s) ? s : SoundStatus.Pending,
                    Notes = (string)o["notes"] ?? "",
                    LastModified = (string)o["lastModified"] ?? "",
                    Removed = (bool?)o["removed"] ?? false,
                };
            }
            return map;
        }

        public static void SaveStatus(Dictionary<string, SoundStatusRecord> map)
        {
            var slots = new JObject();
            foreach (var pair in map.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var o = new JObject
                {
                    ["status"] = pair.Value.Status.ToString(),
                    ["notes"] = pair.Value.Notes ?? "",
                    ["lastModified"] = pair.Value.LastModified ?? "",
                };
                if (pair.Value.Removed) o["removed"] = true;
                slots[pair.Key] = o;
            }
            WriteJson(StatusPath, new JObject
            {
                ["_comment"] = "Calificaciones y notas de los sonidos, por id estable del registro. Lo escribe la pestaña Sounds.",
                ["slots"] = slots,
            });
        }

        /// <summary>Cambia la calificación / nota de un hueco y guarda.</summary>
        public static void SetStatus(string id, Action<SoundStatusRecord> change)
        {
            var map = LoadStatus();
            if (!map.TryGetValue(id, out var record)) map[id] = record = new SoundStatusRecord();
            change(record);
            record.LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            SaveStatus(map);
        }

        // JSON estable: sangría fija, saltos LF, UTF-8 sin BOM, salto final. Sin marcas de tiempo.
        private static void WriteJson(string path, JObject root)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string text = root.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
            if (File.Exists(path) && File.ReadAllText(path) == text) return;
            File.WriteAllText(path, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
        }

        // ------------------------------------------------------------------ informe

        public static string Summary(List<SoundRegistryEntry> entries, Dictionary<string, SoundStatusRecord> status)
        {
            var live = entries.Where(e => !(status.TryGetValue(e.Id, out var r) && r.Removed)).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"Huecos: {live.Count} · descubiertos {live.Count(e => e.Origin == "Discovered")} · " +
                          $"música por convención {live.Count(e => e.Origin == "Convention")} · " +
                          $"declarados {live.Count(e => e.Origin == "Declared")}");
            sb.AppendLine("Por estado: " + string.Join(" · ", Enum.GetValues(typeof(SoundStatus)).Cast<SoundStatus>()
                .Select(s => $"{s} {live.Count(e => (status.TryGetValue(e.Id, out var r) ? r.Status : SoundStatus.Pending) == s)}")));
            sb.AppendLine("Por origen del clip: " + string.Join(" · ", Enum.GetValues(typeof(ClipSource)).Cast<ClipSource>()
                .Select(c => $"{c} {live.Count(e => e.ClipSource == c)}")));
            return sb.ToString();
        }
    }
}
