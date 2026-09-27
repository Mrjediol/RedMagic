using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Audio.EditorTools
{
    /// <summary>
    /// Pestaña "Sounds" de la ventana Biblioteca Web: el registro de sonidos navegable por carpetas,
    /// con filtros, calificación, asignación directa de clip (sin abrir el prefab), preescucha,
    /// huecos declarados a mano y quitar un sonido del todo. Lee y escribe los tres ficheros de
    /// <see cref="SoundRegistry"/>. Interfaz de editor: sus textos no pasan por la localización.
    /// </summary>
    [Serializable]
    public sealed class SoundsTab
    {
        private static readonly string[] TopOrder =
        {
            "Player", "Enemies", "Bosses", "Projectiles", "Interactables", "UI", "Shop",
            "Legendary Passives", "Run Flow", "Economy", "Build & Status", "Music",
        };

        [SerializeField] private string _search = "";
        [SerializeField] private int _statusFilter = -1;   // -1 = todos
        [SerializeField] private int _sourceFilter = -1;
        [SerializeField] private bool _needsClip;
        [SerializeField] private bool _needsReplace;
        [SerializeField] private bool _showRemoved;
        [SerializeField] private string _folder = "";
        [SerializeField] private Vector2 _listScroll;

        [NonSerialized] private List<SoundRegistryEntry> _entries;
        [NonSerialized] private Dictionary<string, SoundStatusRecord> _status;
        [NonSerialized] private string _message;
        [NonSerialized] private MessageType _messageType;
        [NonSerialized] private string _notesId;
        [NonSerialized] private string _notesBuffer = "";

        [NonSerialized] private bool _adding;
        [NonSerialized] private string _newName = "", _newCategory = "", _newNotes = "";

        public void Reload()
        {
            _entries = SoundRegistry.LoadGenerated();
            _status = SoundRegistry.LoadStatus();
        }

        // ================================================================== dibujo

        public void Draw(float width)
        {
            if (_entries == null) Reload();

            DrawHeader();
            DrawFilters();
            if (_adding) DrawAddForm();
            int stale = SoundRegistryWatcher.StaleCount;
            if (stale > 0)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Hay cambios en el proyecto desde la última regeneración ({stale}). " +
                                           "Lo nuevo (enemigos, armas, mundos…) no aparece hasta regenerar.", EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Regenerar ahora", GUILayout.Width(120), GUILayout.Height(30))) Regenerate();
                EditorGUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _messageType);

            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox("Aún no hay registro. Pulsa 'Regenerar registro'.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawTree();
            DrawRows(width - 230f);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            var live = _entries.Where(e => !IsRemoved(e)).ToList();
            int needClip = live.Count(NeedsRealClip);
            int needReplace = live.Count(NeedsReplacing);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"Total {live.Count}", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label($"Necesitan clip real: {needClip}", GUILayout.Width(150));
            GUILayout.Label($"A reemplazar: {needReplace}", GUILayout.Width(110));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Añadir hueco", GUILayout.Width(110))) _adding = !_adding;
            if (GUILayout.Button("Regenerar registro", GUILayout.Width(130))) Regenerate();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));

            var statusNames = new[] { "Estado: todos" }.Concat(Enum.GetNames(typeof(SoundStatus))).ToArray();
            _statusFilter = EditorGUILayout.Popup(_statusFilter + 1, statusNames, GUILayout.Width(120)) - 1;

            var sourceNames = new[] { "Clip: todos" }.Concat(Enum.GetNames(typeof(ClipSource))).ToArray();
            _sourceFilter = EditorGUILayout.Popup(_sourceFilter + 1, sourceNames, GUILayout.Width(150)) - 1;

            _needsClip = GUILayout.Toggle(_needsClip, "Necesita clip real", EditorStyles.miniButton, GUILayout.Width(115));
            _needsReplace = GUILayout.Toggle(_needsReplace, "A reemplazar", EditorStyles.miniButton, GUILayout.Width(90));
            _showRemoved = GUILayout.Toggle(_showRemoved, "Quitados", EditorStyles.miniButton, GUILayout.Width(65));
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ árbol

        [SerializeField] private TreeViewState<int> _treeState;
        [NonSerialized] private FolderTree _tree;
        [NonSerialized] private string _treeSignature;

        private void DrawTree()
        {
            var visible = _entries.Where(e => _showRemoved || !IsRemoved(e)).ToList();

            // El árbol se reconstruye sólo cuando cambian las carpetas o los recuentos.
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var e in visible)
            {
                string path = "";
                foreach (var part in e.Category.Split('/'))
                {
                    path = path.Length == 0 ? part : path + "/" + part;
                    counts.TryGetValue(path, out int n);
                    counts[path] = n + 1;
                }
            }
            string signature = visible.Count + "|" + string.Join(";", counts.Select(p => p.Key + "=" + p.Value));

            _treeState ??= new TreeViewState<int>();
            if (_tree == null)
            {
                _tree = new FolderTree(_treeState, path => _folder = path);
                _treeSignature = null;
            }
            if (_treeSignature != signature)
            {
                _tree.SetData(counts, visible.Count, _folder);
                _treeSignature = signature;
            }

            var rect = GUILayoutUtility.GetRect(220, 220, 100, 100000, GUILayout.Width(220), GUILayout.ExpandHeight(true));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            _tree.OnGUI(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4));
        }

        /// <summary>Árbol de carpetas con el TreeView nativo de Unity (el mismo aspecto que la Jerarquía).</summary>
        private sealed class FolderTree : TreeView<int>
        {
            private readonly Action<string> _onSelect;
            private readonly Dictionary<int, string> _paths = new Dictionary<int, string>();
            private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();
            private SortedDictionary<string, int> _data = new SortedDictionary<string, int>();
            private int _total;

            public FolderTree(TreeViewState<int> state, Action<string> onSelect) : base(state)
            {
                _onSelect = onSelect;
                showAlternatingRowBackgrounds = true;
                rowHeight = 20f;
            }

            public void SetData(SortedDictionary<string, int> counts, int total, string selected)
            {
                _data = counts;
                _total = total;
                Reload();
                int id = Id(selected ?? "");
                if (!GetSelection().Contains(id)) SetSelection(new[] { id });
            }

            private static int Id(string path) => path.Length == 0 ? 1 : (path.GetHashCode() & 0x7fffffff) | 2;

            protected override TreeViewItem<int> BuildRoot()
            {
                _paths.Clear();
                _counts.Clear();
                var root = new TreeViewItem<int>(0, -1, "root");
                var all = new TreeViewItem<int>(Id(""), 0, "Todo");
                _paths[all.id] = "";
                _counts[all.id] = _total;
                root.AddChild(all);

                var tops = _data.Keys.Where(k => !k.Contains("/"))
                    .OrderBy(t => Array.IndexOf(TopOrder, t) < 0 ? 999 : Array.IndexOf(TopOrder, t)).ThenBy(t => t, StringComparer.Ordinal);
                foreach (var top in tops) all.AddChild(Node(top));

                SetupDepthsFromParentsAndChildren(root);
                return root;
            }

            private TreeViewItem<int> Node(string path)
            {
                var item = new TreeViewItem<int>(Id(path), 0, path.Substring(path.LastIndexOf('/') + 1));
                _paths[item.id] = path;
                _counts[item.id] = _data[path];
                foreach (var child in _data.Keys.Where(k => k.StartsWith(path + "/", StringComparison.Ordinal) &&
                                                           k.IndexOf('/', path.Length + 1) < 0))
                    item.AddChild(Node(child));
                return item;
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                base.RowGUI(args);
                if (!_counts.TryGetValue(args.item.id, out int count)) return;
                var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(args.rowRect.x, args.rowRect.y, args.rowRect.width - 6, args.rowRect.height),
                          count.ToString(), style);
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                if (selectedIds.Count > 0 && _paths.TryGetValue(selectedIds[0], out string path)) _onSelect(path);
            }

            protected override bool CanMultiSelect(TreeViewItem<int> item) => false;
        }

        // ------------------------------------------------------------------ filas

        private void DrawRows(float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var rows = _entries.Where(Visible).ToList();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(_folder) ? $"Todo · {rows.Count}" : $"{_folder} · {rows.Count}",
                                       EditorStyles.boldLabel);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (var entry in rows) DrawRow(entry);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRow(SoundRegistryEntry e)
        {
            var record = Record(e);
            bool removed = record.Removed;

            var prevBg = GUI.backgroundColor;
            if (record.Status == SoundStatus.Horrible) GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBg;

            EditorGUILayout.BeginHorizontal();

            // ▶ preescucha
            var preview = PreviewClip(e);
            using (new EditorGUI.DisabledScope(preview == null))
                if (GUILayout.Button("▶", GUILayout.Width(24))) PlayPreview(preview);

            // Nombre: selecciona el objeto dueño y enfoca el campo.
            string sub = e.Category.Length > _folder.Length && !string.IsNullOrEmpty(_folder)
                ? e.Category.Substring(Math.Min(e.Category.Length, _folder.Length + 1)) : e.Category;
            var nameStyle = new GUIStyle(EditorStyles.label) { richText = true };
            string text = $"<b>{e.Name}</b>" + (string.IsNullOrEmpty(sub) ? "" : $"   <color=#888>{sub}</color>");
            if (GUILayout.Button(new GUIContent(text, e.Id), nameStyle, GUILayout.MinWidth(120))) Reveal(e);

            GUILayout.FlexibleSpace();

            // Clip: asignar arrastrando o eligiendo, sin abrir el prefab.
            using (new EditorGUI.DisabledScope(e.Kind == SoundSlotKind.Declared || removed))
            {
                var current = e.Clips.Count > 0 ? AssetDatabase.LoadAssetAtPath<AudioClip>(e.Clips[0]) : null;
                EditorGUI.BeginChangeCheck();
                var picked = (AudioClip)EditorGUILayout.ObjectField(current, typeof(AudioClip), false, GUILayout.Width(190));
                if (EditorGUI.EndChangeCheck()) Assign(e, picked);
            }
            // Variantes: el botón abre el panel para verlas, cambiarlas, quitarlas o añadir más.
            bool canVary = HasVariants(e) && !removed;
            using (new EditorGUI.DisabledScope(!canVary))
            {
                bool open = _variantsOpen.Contains(e.Id);
                string label = canVary ? $"{e.Clips.Count} {(open ? "▴" : "▾")}" : "";
                if (GUILayout.Button(new GUIContent(label, "Variantes: suena una al azar cada vez"),
                                     EditorStyles.miniButton, GUILayout.Width(36)) && canVary)
                {
                    if (open) _variantsOpen.Remove(e.Id); else _variantsOpen.Add(e.Id);
                }
            }

            // Estado → SoundStatus.json
            var prevColor = GUI.color;
            GUI.color = StatusColor(record.Status);
            EditorGUI.BeginChangeCheck();
            var status = (SoundStatus)EditorGUILayout.EnumPopup(record.Status, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck()) SetStatus(e, r => r.Status = status);
            GUI.color = prevColor;

            GUILayout.Label(SourceLabel(e.ClipSource), GUILayout.Width(128));

            if (GUILayout.Button("⋯", GUILayout.Width(22))) RowMenu(e, record);
            EditorGUILayout.EndHorizontal();

            if (_variantsOpen.Contains(e.Id) && HasVariants(e) && !removed) DrawVariants(e);

            // Notas (del usuario o del declarado)
            string notes = !string.IsNullOrEmpty(record.Notes) ? record.Notes : e.Notes;
            if (_notesId == e.Id)
            {
                _notesBuffer = EditorGUILayout.TextArea(_notesBuffer, GUILayout.MinHeight(34));
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Guardar nota", GUILayout.Width(100)))
                {
                    string value = _notesBuffer;
                    SetStatus(e, r => r.Notes = value);
                    _notesId = null;
                }
                if (GUILayout.Button("Cancelar", GUILayout.Width(70))) _notesId = null;
                EditorGUILayout.EndHorizontal();
            }
            else if (!string.IsNullOrEmpty(notes))
                EditorGUILayout.LabelField(notes, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void RowMenu(SoundRegistryEntry e, SoundStatusRecord record)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Nota…"), false, () => { _notesId = e.Id; _notesBuffer = record.Notes ?? ""; });
            menu.AddItem(new GUIContent("Mostrar en el Inspector"), false, () => Reveal(e));
            menu.AddSeparator("");
            if (e.Kind == SoundSlotKind.Declared)
                menu.AddItem(new GUIContent("Borrar hueco declarado"), false, () => RemoveDeclared(e));
            else if (record.Removed)
                menu.AddItem(new GUIContent("Restaurar hueco"), false, () => Restore(e));
            else
                menu.AddItem(new GUIContent("Quitar sonido (no sonará nunca)"), false, () => RemoveSound(e));
            menu.ShowAsContext();
        }

        // ------------------------------------------------------------------ añadir hueco a mano

        private void DrawAddForm()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Hueco nuevo (declarado: un sonido que el juego necesita y aún no tiene momento)",
                                       EditorStyles.miniBoldLabel);
            _newName = EditorGUILayout.TextField("Nombre", _newName);
            _newCategory = EditorGUILayout.TextField("Carpeta", string.IsNullOrEmpty(_newCategory) ? _folder : _newCategory);
            _newNotes = EditorGUILayout.TextField("Nota", _newNotes);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newName) || string.IsNullOrWhiteSpace(_newCategory)))
                if (GUILayout.Button("Añadir", GUILayout.Width(80))) AddDeclared();
            if (GUILayout.Button("Cancelar", GUILayout.Width(80))) _adding = false;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void AddDeclared()
        {
            string key = new string(_newName.Where(char.IsLetterOrDigit).ToArray());
            key = char.ToLowerInvariant(key[0]) + key.Substring(1);
            try
            {
                SoundRegistry.AddDeclared(key, _newName.Trim(), _newCategory.Trim(), _newNotes.Trim());
                _entries.Add(new SoundRegistryEntry
                {
                    Id = "declared:" + key, Name = _newName.Trim(), Category = _newCategory.Trim(),
                    Kind = SoundSlotKind.Declared, Origin = "Declared", DeclaredKey = key,
                    ClipSource = ClipSource.None, Notes = _newNotes.Trim(),
                });
                Sort();
                SoundRegistry.WriteGenerated(_entries);
                Info($"Añadido '{_newName.Trim()}' en {_newCategory.Trim()}.");
                _adding = false;
                _newName = _newNotes = "";
            }
            catch (Exception ex) { Error(ex.Message); }
        }

        // ================================================================== acciones

        private void Regenerate()
        {
            var entries = SoundRegistry.Regenerate(out string report, out bool ok);
            if (ok) { Reload(); Info(report.Trim()); }
            else Error(report);
            GUIUtility.ExitGUI();
        }

        private void Assign(SoundRegistryEntry e, AudioClip clip)
        {
            try
            {
                if (e.Kind == SoundSlotKind.MusicConvention) AssignMusic(e, clip);
                else AssignSlot(e, clip);

                SoundRegistry.Refresh(e);
                SoundRegistry.WriteGenerated(_entries);
                Info(clip != null ? $"{e.Name}: {clip.name}" : $"{e.Name}: sin clip");
            }
            catch (Exception ex) { Error($"{e.Name}: {ex.Message}"); }
            GUIUtility.ExitGUI();
        }

        // ------------------------------------------------------------------ variantes

        [NonSerialized] private readonly HashSet<string> _variantsOpen = new HashSet<string>();

        /// <summary>Huecos con SoundCue (admiten varias variantes). La música es de un solo clip.</summary>
        private static bool HasVariants(SoundRegistryEntry e) =>
            e.Slot != null && (e.Kind == SoundSlotKind.EmitterTrigger || e.Kind == SoundSlotKind.CueField);

        private void DrawVariants(SoundRegistryEntry e)
        {
            var clips = e.Clips.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToList();
            List<AudioClip> changed = null;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Variantes (suena una al azar cada vez, sin repetir la anterior)", EditorStyles.miniBoldLabel);

            for (int i = 0; i < clips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString(), GUILayout.Width(18));
                if (GUILayout.Button("▶", GUILayout.Width(24))) PlayPreview(clips[i]);
                EditorGUI.BeginChangeCheck();
                var picked = (AudioClip)EditorGUILayout.ObjectField(clips[i], typeof(AudioClip), false);
                if (EditorGUI.EndChangeCheck())
                {
                    changed = new List<AudioClip>(clips);
                    if (picked != null) changed[i] = picked; else changed.RemoveAt(i);
                }
                if (GUILayout.Button(new GUIContent("✕", "Quitar esta variante"), GUILayout.Width(24)))
                {
                    changed = new List<AudioClip>(clips);
                    changed.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("+", GUILayout.Width(18));
            GUILayout.Space(28);
            var added = (AudioClip)EditorGUILayout.ObjectField(null, typeof(AudioClip), false);
            if (added != null) changed = new List<AudioClip>(clips) { added };
            GUILayout.Space(28);
            EditorGUILayout.EndHorizontal();

            // Zona para sustituirlas todas: arrastrar 4 pasos nuevos deja exactamente esos 4.
            var drop = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
            GUI.Box(drop, "Suelta aquí uno o varios clips para SUSTITUIR todas las variantes", EditorStyles.helpBox);
            var dropped = DroppedClips(drop);
            if (dropped != null) changed = dropped;

            EditorGUILayout.EndVertical();

            if (changed != null) SetClips(e, changed);
        }

        private static List<AudioClip> DroppedClips(Rect area)
        {
            var evt = Event.current;
            if (!area.Contains(evt.mousePosition)) return null;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return null;

            var clips = DragAndDrop.objectReferences.OfType<AudioClip>().ToList();
            if (clips.Count == 0) return null;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type != EventType.DragPerform) return null;

            DragAndDrop.AcceptDrag();
            evt.Use();
            return clips.OrderBy(c => c.name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Deja en el hueco exactamente estos clips, en este orden.</summary>
        private void SetClips(SoundRegistryEntry e, List<AudioClip> clips)
        {
            try
            {
                var slot = e.Slot;
                bool ok = SoundSlotEditor.Edit(slot, prop =>
                {
                    if (!prop.FindPropertyRelative("initialized").boolValue)
                        SoundSlotEditor.InitCue(prop, prop.FindPropertyRelative("positional").boolValue, SoundPriority.Normal);
                    var list = prop.FindPropertyRelative("clips");
                    list.arraySize = clips.Count;
                    for (int i = 0; i < clips.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                    SetSilent(prop, false);
                }, out string error);
                if (!ok) throw new InvalidOperationException(error);

                slot.Clips.Clear();
                slot.Clips.AddRange(clips);
                slot.EntryMissing = false;
                slot.UsesFallback = clips.Count == 0 && slot.Kind == SoundSlotKind.EmitterTrigger &&
                                    SystemSounds.HasEnemyFallback(slot.Trigger) && OwnerUsesFallback(slot);

                SoundRegistry.Refresh(e);
                SoundRegistry.WriteGenerated(_entries);
                Info($"{e.Name}: {clips.Count} variante{(clips.Count == 1 ? "" : "s")}.");
            }
            catch (Exception ex) { Error($"{e.Name}: {ex.Message}"); }
            GUIUtility.ExitGUI();
        }

        private void AssignSlot(SoundRegistryEntry e, AudioClip clip)
        {
            var slot = e.Slot;
            var clips = new List<AudioClip>();
            bool ok = SoundSlotEditor.Edit(slot, prop =>
            {
                if (slot.Kind == SoundSlotKind.MusicField)
                {
                    prop.objectReferenceValue = clip;
                    if (clip != null) clips.Add(clip);
                    return;
                }

                if (!prop.FindPropertyRelative("initialized").boolValue)
                    SoundSlotEditor.InitCue(prop, prop.FindPropertyRelative("positional").boolValue, SoundPriority.Normal);

                // Sólo cambia la primera variante; las demás se quedan.
                var list = prop.FindPropertyRelative("clips");
                if (clip != null)
                {
                    if (list.arraySize == 0) list.arraySize = 1;
                    list.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                }
                else if (list.arraySize > 0)
                {
                    list.GetArrayElementAtIndex(0).objectReferenceValue = null;
                    list.DeleteArrayElementAtIndex(0);
                }

                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is AudioClip c) clips.Add(c);

                SetSilent(prop, false);
            }, out string error);

            if (!ok) throw new InvalidOperationException(error);

            slot.Clips.Clear();
            slot.Clips.AddRange(clips);
            slot.EntryMissing = false;
            slot.UsesFallback = clips.Count == 0 && slot.Kind == SoundSlotKind.EmitterTrigger &&
                                SystemSounds.HasEnemyFallback(slot.Trigger) && OwnerUsesFallback(slot);
        }

        // Música por convención: el clip se copia a Resources/Music/<nombre>, que es lo que carga RunManager.
        private static void AssignMusic(SoundRegistryEntry e, AudioClip clip)
        {
            string existing = SoundRegistry.FindMusic(e.MusicName);
            string source = clip != null ? AssetDatabase.GetAssetPath(clip) : null;
            if (existing != null && existing == source) return;

            SoundRegistryWatcher.IgnoreNext(existing);
            if (existing != null) AssetDatabase.MoveAssetToTrash(existing);
            if (clip == null) return;

            string target = $"{SoundRegistry.MusicResourceFolder}/{e.MusicName}{Path.GetExtension(source)}";
            SoundRegistryWatcher.IgnoreNext(target);
            if (!AssetDatabase.CopyAsset(source, target)) throw new IOException($"No se pudo copiar a {target}.");
        }

        private void RemoveSound(SoundRegistryEntry e)
        {
            if (!EditorUtility.DisplayDialog("Quitar sonido",
                    $"'{e.Name}' se queda sin clip y no sonará nunca (tampoco el sonido genérico). " +
                    "Puedes restaurarlo desde el filtro 'Quitados'.", "Quitar", "Cancelar"))
                return;

            try
            {
                if (e.Kind == SoundSlotKind.MusicConvention) AssignMusic(e, null);
                else
                {
                    var slot = e.Slot;
                    bool ok = SoundSlotEditor.Edit(slot, prop =>
                    {
                        if (slot.Kind == SoundSlotKind.MusicField) { prop.objectReferenceValue = null; return; }
                        prop.FindPropertyRelative("clips").arraySize = 0;
                        SetSilent(prop, true);
                    }, out string error);
                    if (!ok) throw new InvalidOperationException(error);
                    slot.Clips.Clear();
                    slot.EntryMissing = false;
                    slot.UsesFallback = false;
                }

                SetStatus(e, r => r.Removed = true);
                SoundRegistry.Refresh(e);
                SoundRegistry.WriteGenerated(_entries);
                Info($"Quitado: {e.Name}.");
            }
            catch (Exception ex) { Error($"{e.Name}: {ex.Message}"); }
        }

        private void Restore(SoundRegistryEntry e)
        {
            if (e.Slot != null && e.Kind == SoundSlotKind.EmitterTrigger)
            {
                SoundSlotEditor.Edit(e.Slot, prop => SetSilent(prop, false), out _);
                e.Slot.UsesFallback = SystemSounds.HasEnemyFallback(e.Slot.Trigger) && OwnerUsesFallback(e.Slot);
                SoundRegistry.Refresh(e);
                SoundRegistry.WriteGenerated(_entries);
            }
            SetStatus(e, r => r.Removed = false);
            Info($"Restaurado: {e.Name}.");
        }

        private void RemoveDeclared(SoundRegistryEntry e)
        {
            SoundRegistry.RemoveDeclared(e.DeclaredKey);
            _entries.Remove(e);
            SoundRegistry.WriteGenerated(_entries);
            var status = SoundRegistry.LoadStatus();
            if (status.Remove(e.Id)) SoundRegistry.SaveStatus(status);
            _status = status;
            Info($"Borrado el hueco declarado '{e.Name}'.");
        }

        // La entrada del SoundEmitter que contiene esta cue (si la cue es de un emisor).
        private static void SetSilent(SerializedProperty cue, bool silent)
        {
            string path = cue.propertyPath;
            if (!path.EndsWith(".cue", StringComparison.Ordinal)) return;
            var entry = cue.serializedObject.FindProperty(path.Substring(0, path.Length - 4));
            var flag = entry?.FindPropertyRelative("silent");
            if (flag != null) flag.boolValue = silent;
        }

        private static bool OwnerUsesFallback(SoundSlot slot)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(slot.AssetPath);
            if (root == null || root.GetComponent<IEnemySoundFallbackUser>() == null) return false;
            var emitter = root.GetComponent<SoundEmitter>();
            return emitter == null || emitter.UsesGenericFallback;
        }

        // Selecciona el objeto dueño, abre su componente en un Inspector flotante y despliega el campo.
        private void Reveal(SoundRegistryEntry e)
        {
            if (e.Kind == SoundSlotKind.MusicConvention)
            {
                var music = e.Clips.Count > 0 ? AssetDatabase.LoadMainAssetAtPath(e.Clips[0])
                                              : AssetDatabase.LoadMainAssetAtPath(SoundRegistry.MusicResourceFolder);
                Selection.activeObject = music;
                EditorGUIUtility.PingObject(music);
                return;
            }
            if (e.Slot == null)
            {
                Info($"'{e.Name}' es un hueco declarado: su momento aún no existe en el juego.");
                return;
            }

            var slot = e.Slot;
            UnityEngine.Object target = null;
            UnityEngine.Object select;

            if (!string.IsNullOrEmpty(slot.ScenePath))
            {
                var scene = SceneManager.GetSceneByPath(slot.ScenePath);
                if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(slot.ScenePath, OpenSceneMode.Additive);
                var host = FindInScene(scene, slot.ObjectPath);
                target = host != null ? ComponentOf(host, slot.ComponentType) : null;
                select = host != null ? host : AssetDatabase.LoadMainAssetAtPath(slot.ScenePath);
            }
            else if (slot.AssetPath.EndsWith(".prefab", StringComparison.Ordinal))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(slot.AssetPath);
                var host = string.IsNullOrEmpty(slot.ObjectPath) ? root.transform : root.transform.Find(slot.ObjectPath);
                target = host != null ? ComponentOf(host.gameObject, slot.ComponentType) : null;
                select = root;
            }
            else
            {
                target = AssetDatabase.LoadMainAssetAtPath(slot.AssetPath);
                select = target;
            }

            Selection.activeObject = select;
            EditorGUIUtility.PingObject(select);
            if (target == null) return;

            if (!string.IsNullOrEmpty(slot.PropertyPath))
            {
                var so = new SerializedObject(target);
                var prop = so.FindProperty(slot.PropertyPath);
                for (var p = prop; p != null; p = Parent(so, p.propertyPath)) p.isExpanded = true;
            }
            EditorUtility.OpenPropertyEditor(target);
        }

        private static SerializedProperty Parent(SerializedObject so, string path)
        {
            int dot = path.LastIndexOf('.');
            if (dot < 0) return null;
            string parent = path.Substring(0, dot);
            if (parent.EndsWith(".Array", StringComparison.Ordinal)) parent = parent.Substring(0, parent.Length - 6);
            return so.FindProperty(parent);
        }

        private static Component ComponentOf(GameObject go, string type) =>
            go.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == type);

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

        // ------------------------------------------------------------------ preescucha (modo edición)

        private static MethodInfo s_play, s_stop;

        private static AudioClip PreviewClip(SoundRegistryEntry e)
        {
            if (e.Clips.Count > 0)
            {
                string path = e.Clips[UnityEngine.Random.Range(0, e.Clips.Count)];
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            if (e.ClipSource == ClipSource.InheritedPlaceholder && e.Slot != null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioSetupTool.PrefabPath);
                var cue = prefab != null ? prefab.GetComponent<SystemSounds>()?.EnemyFallback(e.Slot.Trigger) : null;
                if (cue != null && cue.clips.Length > 0) return cue.clips[0];
            }
            return null;
        }

        private static void PlayPreview(AudioClip clip)
        {
            if (clip == null) return;
            if (s_play == null)
            {
                var util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
                s_play = util?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null,
                                         new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
                s_stop = util?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
            }
            s_stop?.Invoke(null, null);
            s_play?.Invoke(null, new object[] { clip, 0, false });
        }

        // ------------------------------------------------------------------ filtros y datos

        private bool Visible(SoundRegistryEntry e)
        {
            var r = Record(e);
            if (r.Removed != _showRemoved) return false;
            if (!InFolder(e.Category, _folder)) return false;
            if (_statusFilter >= 0 && (int)r.Status != _statusFilter) return false;
            if (_sourceFilter >= 0 && (int)e.ClipSource != _sourceFilter) return false;
            if (_needsClip && !NeedsRealClip(e)) return false;
            if (_needsReplace && !NeedsReplacing(e)) return false;
            if (!string.IsNullOrWhiteSpace(_search))
            {
                string q = _search.Trim();
                bool hit = Contains(e.Name, q) || Contains(e.Category, q) ||
                           e.Clips.Any(c => Contains(Path.GetFileName(c), q)) || Contains(r.Notes, q);
                if (!hit) return false;
            }
            return true;
        }

        private bool NeedsRealClip(SoundRegistryEntry e) =>
            Record(e).Status == SoundStatus.Pending || e.ClipSource == ClipSource.GenericTest;

        private bool NeedsReplacing(SoundRegistryEntry e) =>
            Record(e).Status is SoundStatus.Bad or SoundStatus.Horrible;

        private bool IsRemoved(SoundRegistryEntry e) => Record(e).Removed;

        private SoundStatusRecord Record(SoundRegistryEntry e) =>
            _status != null && _status.TryGetValue(e.Id, out var r) ? r : Default;

        private static readonly SoundStatusRecord Default = new SoundStatusRecord();

        private void SetStatus(SoundRegistryEntry e, Action<SoundStatusRecord> change)
        {
            SoundRegistry.SetStatus(e.Id, change);
            _status = SoundRegistry.LoadStatus();
        }

        private void Sort() => _entries.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.Category, b.Category);
            if (c == 0) c = string.CompareOrdinal(a.Name, b.Name);
            return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
        });

        private static bool InFolder(string category, string folder) =>
            string.IsNullOrEmpty(folder) || category == folder || category.StartsWith(folder + "/", StringComparison.Ordinal);

        private static string Top(string category) => category.Split('/')[0];

        private static bool Contains(string s, string q) =>
            !string.IsNullOrEmpty(s) && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string SourceLabel(ClipSource source) => source switch
        {
            ClipSource.Own => "Propio",
            ClipSource.InheritedPlaceholder => "Genérico heredado",
            ClipSource.GenericTest => "Clip de prueba",
            _ => "Sin clip",
        };

        private static Color StatusColor(SoundStatus s) => s switch
        {
            SoundStatus.Perfect => new Color(0.55f, 1f, 0.55f),
            SoundStatus.Good => new Color(0.8f, 1f, 0.6f),
            SoundStatus.Bad => new Color(1f, 0.75f, 0.4f),
            SoundStatus.Horrible => new Color(1f, 0.5f, 0.5f),
            _ => Color.white,
        };

        private void Info(string text) { _message = text; _messageType = MessageType.Info; }
        private void Error(string text) { _message = text; _messageType = MessageType.Error; }
    }
}
