// WebLibraryWindow.cs
// -----------------------------------------------------------------------------
// Tools > Web > Biblioteca — a dockable, categorized browser for content produced by the
// web-driven import pipelines (Enemy Sprite Extractor / Enemy Creator / Map Tracer), for quick
// navigation and safe multi-location deletion. Left: category list. Right: a card grid for the
// selected category, each card a thumbnail + name + action buttons — same interaction model as
// the Project window's favorites, richer per-item actions.
//
// GetWindow<WebLibraryWindow>() alone (no 'utility' flag) is what makes this genuinely dockable —
// Unity persists a plain EditorWindow's dock position across sessions by its Type, same as any
// other panel, with no extra code needed.
//
// ENEMY PREFAB FOLDER — HISTORY: two audits and a folder restructure went into this (see
// docs/web-tools-guide.md, ARCHITECTURE.md's Map Tracer section, and
// docs/folder-restructure-audit.md). Up through that restructure there were TWO sibling folders,
// "Assets/Prefab/Enemies/" (singular — the REAL, gameplay-ready `Enemy_<Name>.prefab`/
// `Boss_<Name>.prefab`, EnemyStats/EnemyBrain/EnemyAttack and everything EnemyFactory stamps) and
// "Assets/Prefabs/Enemies/" (plural — a sprite/Animator-only INTERMEDIATE artifact from
// EnemyImporter.cs's old "Build Enemy From Folder" prefab-writing step, read by nothing
// downstream). The Enemies category only ever scanned the singular, real one — deliberately
// excluding the plural intermediate folder. As part of the restructure, that dead prefab-writing
// step was removed from EnemyImporter.cs entirely, the plural folder (by then just test junk) was
// deleted, and the REAL folder was renamed to reuse the now-free plural name — so
// ENEMY_PIPELINE_FOLDER below is "Assets/Prefabs/Enemies" (plural) today, and there is only one
// enemy prefab folder in the project, full stop.
//
// A single enemy's sprite/recipe data can independently live under Assets/Art/Characters/<Name>/
// (pipeline: .sheet.asset/.enemy.asset/sliced PNGs/controller) and/or Assets/Art/EnemyImports/<Name>/
// (EnemyImporter output that the real prefab's AnimationClips may still reference directly,
// enemy-config.json if it came from a combined-bundle import — this folder was itself renamed from
// "Assets/Enemies/" in the same restructure). "Eliminar enemigo" groups by <Name> off the real
// prefab and also trashes whichever of THOSE two exist alongside it.
//
// DELIBERATELY NOT deleted with an enemy: projectile assets (Assets/Prefabs/Projectiles/<ProjName>/).
// Those are keyed by PROJECTILE name, not enemy name, and nothing stops two enemies sharing one —
// auto-deleting them per-enemy risks breaking an unrelated enemy silently. Left for manual cleanup.
//
// ORDER (cards and tabs): AssetDatabase has no notion of a user-chosen order, so one is kept
// separately — a small JSON file under Library/ (not Assets/, so it's local to this machine/Editor
// install, never versioned as project data — see LibraryOrderStore). Dragging a card onto another
// card of the same tab swaps them; dragging a tab onto another tab swaps those. The drag payload is
// DragAndDrop generic data (CardReorderKey / TabReorderKey), which is what tells an internal reorder
// apart from a drop anywhere else: a card drag ALSO carries its asset in objectReferences, so the
// same gesture dropped on the Scene view / Hierarchy instantiates the prefab as before.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RedMagic.Bosses;
using RedMagic.Economy;
using RedMagic.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class WebLibraryWindow : EditorWindow
{
    // Declaration order is only the first-run fallback: the sidebar is drawn from the persisted tab
    // order (LibraryOrderStore, key TabOrderKey), reordered by dragging one tab onto another.
    private enum Category { Escenas, Enemies, Bosses, MiniBosses, Mapas, Player, Pasivas, Items, Armas }

    private static string Label(Category cat) => cat == Category.Pasivas ? "Pasivas Legendarias" : cat.ToString();

    // Bottom-bar utilities (WebLibraryUtilities does the work). Same persisted, drag-to-swap order
    // as the tabs; "Actualizar" stays pinned first.
    private enum Utility { CargarHub, AnadirPlayer, QuitarPlayers }

    private static string Label(Utility u) => u switch
    {
        Utility.CargarHub => "Cargar MainHub",
        Utility.AnadirPlayer => "Añadir Player",
        _ => "Quitar Player de escenas",
    };

    private const string TabOrderKey = "__Tabs";
    private const string UtilityOrderKey = "__Utilities";
    private const string CardReorderKey = "BibliotecaWeb.CardReorder";
    private const string TabReorderKey = "BibliotecaWeb.TabReorder";
    private const string UtilityReorderKey = "BibliotecaWeb.UtilityReorder";

    private sealed class CardDrag { public Category category; public int index; }
    private sealed class TabDrag { public Category category; }
    private sealed class UtilityDrag { public Utility utility; }

    private const string ENEMY_PIPELINE_FOLDER = "Assets/Prefabs/Enemies";   // Enemy_<Name>.prefab / Boss_<Name>.prefab — the ONLY source for the Enemies grid
    private const string ART_CHARACTERS_FOLDER = "Assets/Art/Characters";
    private const string ENEMY_IMPORTER_FOLDER = "Assets/Art/EnemyImports";
    private const string BOSS_DEFINITION_FOLDER = "Assets/Resources/Bosses";
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/Player.prefab";
    private const string SCENES_FOLDER = "Assets/Scenes";

    private class Entry
    {
        public string id;               // persisted order key; null = name (the original tabs, so saved orders survive)
        public string name;
        public string previewPath;      // asset to draw a thumbnail for / the click-to-select target
        public UnityEngine.Object previewObject; // overrides previewPath's thumbnail (a ScriptableObject's icon sprite)
        public string dragPath;         // asset handed to Scene/Hierarchy/Inspector on drag; null = reorder-only
        public List<(string label, Action action)> actions = new List<(string, Action)>();
        public string Id => id ?? name;
    }

    [MenuItem("Tools/Web/Biblioteca")]
    public static void Open()
    {
        var win = GetWindow<WebLibraryWindow>();
        win.titleContent = new GUIContent("Biblioteca Web");
        win.minSize = new Vector2(480, 300);
        win.Show();
    }

    [SerializeField] private Category _category;
    [SerializeField] private bool _hasCategory; // false only on a brand-new window: open on the first tab
    private Vector2 _sidebarScroll, _gridScroll;
    private bool _dirty = true; // scans are cached, not re-run every OnGUI — see RequestRescan()
    private readonly Dictionary<Category, List<Entry>> _cache = new Dictionary<Category, List<Entry>>();
    private List<Category> _tabs;
    private List<Utility> _utilities;
    private bool _reorderDragActive; // a drag WE started is in flight — gates hover highlights

    private void OnEnable()
    {
        _dirty = true;
        _tabs = LibraryOrderStore.Apply(TabOrderKey, ((Category[])Enum.GetValues(typeof(Category))).ToList(), c => c.ToString());
        _utilities = LibraryOrderStore.Apply(UtilityOrderKey, ((Utility[])Enum.GetValues(typeof(Utility))).ToList(), u => u.ToString());
        if (!_hasCategory) { _category = _tabs[0]; _hasCategory = true; }
    }

    private void RequestRescan() => _dirty = true;

    private void OnGUI()
    {
        if (_dirty)
        {
            RescanAll();
            _dirty = false;
        }

        Event evt = Event.current;
        if (evt.type == EventType.DragExited || evt.type == EventType.MouseMove || evt.type == EventType.MouseDown)
        {
            if (_reorderDragActive) Repaint();
            _reorderDragActive = false;
        }
        else if (evt.type == EventType.DragUpdated && _reorderDragActive)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected; // a drop target below turns it into Move
            Repaint(); // the hover highlight follows the pointer even over empty space
        }

        EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
        DrawSidebar();
        DrawContent();
        EditorGUILayout.EndHorizontal();
        DrawUtilityBar();
    }

    // ============================================================ bottom utility bar

    private void DrawUtilityBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        if (GUILayout.Button("🔄 Actualizar", GUILayout.Height(24), GUILayout.Width(132))) RequestRescan();

        // Index loop: a drop swaps two entries of _utilities mid-iteration.
        for (int i = 0; i < _utilities.Count; i++)
        {
            Utility utility = _utilities[i];
            var content = new GUIContent(Label(utility));
            Rect rect = GUILayoutUtility.GetRect(content, GUI.skin.button, GUILayout.Height(24));
            int id = GUIUtility.GetControlID(FocusType.Passive);

            DrawDraggableButton(rect, id, content, false);
            DragSource(id, rect, () => RunUtility(utility), () => StartUtilityDrag(utility), MouseCursor.Arrow);
            DropTarget<UtilityDrag>(UtilityReorderKey, rect, d => d.utility != utility, d => SwapUtilities(d.utility, utility));
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void RunUtility(Utility utility)
    {
        switch (utility)
        {
            case Utility.CargarHub: WebLibraryUtilities.OpenHub(); break;
            case Utility.AnadirPlayer: WebLibraryUtilities.AddPlayer(PLAYER_PREFAB_PATH); break;
            case Utility.QuitarPlayers: WebLibraryUtilities.RemovePlayersFromAllScenes(PLAYER_PREFAB_PATH); break;
        }
        GUIUtility.ExitGUI(); // abrir/cerrar escenas y diálogos invalidan el layout de este evento
    }

    /// <summary>A button drawn by hand so it can be both clicked and dragged (GUILayout.Button
    /// eats the MouseDown a drag needs).</summary>
    private static void DrawDraggableButton(Rect rect, int id, GUIContent content, bool selected)
    {
        if (Event.current.type != EventType.Repaint) return;
        var prevColor = GUI.backgroundColor;
        if (selected) GUI.backgroundColor = new Color(0.45f, 0.72f, 1f);
        GUI.skin.button.Draw(rect, content, rect.Contains(Event.current.mousePosition), GUIUtility.hotControl == id, false, false);
        GUI.backgroundColor = prevColor;
    }

    // ============================================================ sidebar

    private void DrawSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(140), GUILayout.ExpandHeight(true));
        _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

        // Index loop, not foreach: a tab drop swaps two entries of _tabs mid-iteration.
        for (int i = 0; i < _tabs.Count; i++)
        {
            Category cat = _tabs[i];
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.button, GUILayout.Height(30), GUILayout.ExpandWidth(true));
            int id = GUIUtility.GetControlID(FocusType.Passive);

            DrawDraggableButton(rect, id, new GUIContent(Label(cat)), _category == cat);
            DragSource(id, rect, () => _category = cat, () => StartTabDrag(cat), MouseCursor.Arrow);
            DropTarget<TabDrag>(TabReorderKey, rect, d => d.category != cat, d => SwapTabs(d.category, cat));
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ============================================================ content grid

    private const float CardWidth = 140f, PreviewHeight = 96f, CardSpacing = 8f;

    private void DrawContent()
    {
        EditorGUILayout.BeginVertical();

        EditorGUILayout.LabelField(Label(_category), EditorStyles.boldLabel);

        var entries = _cache.TryGetValue(_category, out var list) ? list : new List<Entry>();

        if (entries.Count == 0)
        {
            EditorGUILayout.HelpBox(EmptyStateMessage(_category), MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll);

        float contentWidth = position.width - 140 - 24; // sidebar + scrollbar/margins
        int columns = Mathf.Max(1, Mathf.FloorToInt((contentWidth + CardSpacing) / (CardWidth + CardSpacing)));
        float cardHeight = CardHeightFor(entries);

        for (int i = 0; i < entries.Count; i += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + columns, entries.Count); j++)
            {
                DrawCard(entries, j, cardHeight);
                GUILayout.Space(CardSpacing);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(CardSpacing);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static string EmptyStateMessage(Category cat)
    {
        switch (cat)
        {
            case Category.MiniBosses:
                return "MiniBosses: sin herramienta de importación dedicada todavía — no hay ningún " +
                       "tipo/convención de asset para minijefes en el proyecto (auditado, cero coincidencias).";
            case Category.Player:
                return "No se encontró Assets/Prefabs/Player.prefab.";
            case Category.Bosses:
                return "Sin BossDefinition en Assets/Resources/Bosses/.";
            case Category.Escenas:
                return $"No se encontraron escenas en {SCENES_FOLDER}/ (esta categoría sólo mira ahí, no el proyecto entero).";
            case Category.Mapas:
                return "No se encontraron prefabs de mapa (se detectan por tener un hijo 'Collisions', " +
                       "la huella de MapImporter — ver Tools > Web > Map Tracer).";
            case Category.Pasivas:
                return "Sin assets LegendaryPassive (Tools > RedMagic > Hub > Espejo · Generar pasivas legendarias).";
            case Category.Items:
                return "Sin assets ItemDefinition (Assets/Resources/Items/).";
            case Category.Armas:
                return "Sin assets WeaponDefinition (Assets/Resources/Items/Weapons/).";
            default:
                return "Nada encontrado.";
        }
    }

    /// <summary>Tight card height for this tab: every card in a tab shares it (so the grid stays
    /// aligned), sized to the tallest name and the most action buttons actually present — not a
    /// fixed worst case, which left one-button cards with a big empty bottom.</summary>
    private static float CardHeightFor(List<Entry> entries)
    {
        GUIStyle box = GUI.skin.box, label = EditorStyles.wordWrappedLabel, button = GUI.skin.button;
        float innerWidth = CardWidth - box.padding.horizontal;

        float nameHeight = 0f;
        int maxActions = 0;
        foreach (var e in entries)
        {
            nameHeight = Mathf.Max(nameHeight, label.CalcHeight(new GUIContent(e.name), innerWidth));
            maxActions = Mathf.Max(maxActions, e.actions.Count);
        }

        float buttonHeight = button.CalcHeight(GUIContent.none, innerWidth) + Mathf.Max(button.margin.top, button.margin.bottom);
        return box.padding.vertical + PreviewHeight + label.margin.vertical + nameHeight + maxActions * buttonHeight;
    }

    private void DrawCard(List<Entry> entries, int index, float cardHeight)
    {
        Entry entry = entries[index];
        EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(CardWidth), GUILayout.Height(cardHeight));

        Rect previewRect = GUILayoutUtility.GetRect(CardWidth - 8, PreviewHeight, GUILayout.ExpandWidth(false));
        int dragId = GUIUtility.GetControlID(FocusType.Passive); // siempre, para que los ids no bailen entre eventos
        Texture preview = GetPreview(entry);
        if (preview != null) GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
        else EditorGUI.LabelField(previewRect, "(sin vista previa)", EditorStyles.centeredGreyMiniLabel);

        GUILayout.Label(entry.name, EditorStyles.wordWrappedLabel);
        Rect nameRect = GUILayoutUtility.GetLastRect();
        // Zona de arrastre = miniatura + nombre; los botones quedan fuera, así que siguen igual.
        DragSource(dragId,
                   Rect.MinMaxRect(previewRect.xMin, previewRect.yMin, Mathf.Max(previewRect.xMax, nameRect.xMax), nameRect.yMax),
                   () => PingAndSelect(entry.previewPath), () => StartCardDrag(entry, index), MouseCursor.Pan);

        foreach (var (btnLabel, action) in entry.actions)
        {
            if (GUILayout.Button(btnLabel)) action();
        }

        EditorGUILayout.EndVertical();

        // Toda la tarjeta es destino de reordenación (GetLastRect = la caja que acaba de cerrarse).
        Category cat = _category;
        DropTarget<CardDrag>(CardReorderKey, GUILayoutUtility.GetLastRect(),
                             d => d.category == cat && d.index != index,
                             d => SwapCards(cat, d.index, index));
    }

    // ============================================================ drag & drop (reorder + drag to scene)

    private const float DragThreshold = 5f; // px antes de que un clic se convierta en arrastre
    private static readonly Color DropHighlight = new Color(0.45f, 0.72f, 1f, 0.3f);
    private Vector2 _pressPos;

    /// <summary>One pressable control that is either clicked (released without moving past
    /// <see cref="DragThreshold"/>) or dragged. <paramref name="onDrag"/> must call
    /// DragAndDrop.StartDrag — it runs while the event is still MouseDrag, as StartDrag requires.</summary>
    private void DragSource(int id, Rect rect, Action onClick, Action onDrag, MouseCursor cursor)
    {
        Event evt = Event.current;
        switch (evt.GetTypeForControl(id))
        {
            case EventType.Repaint:
                if (cursor != MouseCursor.Arrow) EditorGUIUtility.AddCursorRect(rect, cursor);
                break;

            case EventType.MouseDown:
                if (evt.button != 0 || !rect.Contains(evt.mousePosition)) break;
                _pressPos = evt.mousePosition;
                GUIUtility.hotControl = id;
                evt.Use();
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != id) break;
                if ((evt.mousePosition - _pressPos).sqrMagnitude >= DragThreshold * DragThreshold)
                {
                    GUIUtility.hotControl = 0;
                    onDrag(); // antes de Use(): Use() convierte el evento en 'Used' y StartDrag lo rechaza
                }
                evt.Use();
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl != id) break;
                GUIUtility.hotControl = 0;
                evt.Use();
                if (rect.Contains(evt.mousePosition)) onClick();
                break;
        }
    }

    /// <summary>Accepts one of our own reorder drags (generic data under <paramref name="key"/>)
    /// over <paramref name="rect"/>: Move cursor + highlight while hovering, <paramref name="onDrop"/>
    /// on release. Drags from anywhere else (Project window, another card tab) are ignored.</summary>
    private void DropTarget<T>(string key, Rect rect, Func<T, bool> accepts, Action<T> onDrop) where T : class
    {
        if (!_reorderDragActive) return;
        Event evt = Event.current;
        if (!(DragAndDrop.GetGenericData(key) is T data) || !accepts(data)) return;

        switch (evt.type)
        {
            case EventType.Repaint:
                if (rect.Contains(evt.mousePosition)) EditorGUI.DrawRect(rect, DropHighlight);
                break;

            case EventType.DragUpdated:
                if (!rect.Contains(evt.mousePosition)) break;
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                evt.Use();
                break;

            case EventType.DragPerform:
                if (!rect.Contains(evt.mousePosition)) break;
                DragAndDrop.AcceptDrag();
                _reorderDragActive = false;
                onDrop(data);
                evt.Use();
                Repaint();
                break;
        }
    }

    /// <summary>The card's asset rides along as objectReferences, so dropping it on the Scene view /
    /// Hierarchy instantiates the prefab (a linked instance, since it's the asset, not a scene
    /// object); the generic data is what makes it a reorder when dropped on another card.</summary>
    private void StartCardDrag(Entry entry, int index)
    {
        DragAndDrop.PrepareStartDrag();
        var asset = string.IsNullOrEmpty(entry.dragPath) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.dragPath);
        if (asset != null)
        {
            DragAndDrop.objectReferences = new[] { asset };
            DragAndDrop.paths = new[] { entry.dragPath };
        }
        DragAndDrop.SetGenericData(CardReorderKey, new CardDrag { category = _category, index = index });
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        _reorderDragActive = true;
        DragAndDrop.StartDrag(entry.name);
    }

    private void StartTabDrag(Category cat)
    {
        DragAndDrop.PrepareStartDrag();
        DragAndDrop.SetGenericData(TabReorderKey, new TabDrag { category = cat });
        _reorderDragActive = true;
        DragAndDrop.StartDrag(Label(cat));
    }

    /// <summary>Swaps two cards in the CURRENTLY CACHED list — no rescan needed, the in-memory
    /// order already IS the display order — and persists the result.</summary>
    private void SwapCards(Category cat, int a, int b)
    {
        if (!_cache.TryGetValue(cat, out var entries)) return;
        if (a < 0 || b < 0 || a >= entries.Count || b >= entries.Count) return;
        (entries[a], entries[b]) = (entries[b], entries[a]);
        LibraryOrderStore.SaveOrder(cat, entries.Select(e => e.Id).ToList());
    }

    private void StartUtilityDrag(Utility utility)
    {
        DragAndDrop.PrepareStartDrag();
        DragAndDrop.SetGenericData(UtilityReorderKey, new UtilityDrag { utility = utility });
        _reorderDragActive = true;
        DragAndDrop.StartDrag(Label(utility));
    }

    private void SwapUtilities(Utility a, Utility b)
    {
        int i = _utilities.IndexOf(a), j = _utilities.IndexOf(b);
        if (i < 0 || j < 0) return;
        (_utilities[i], _utilities[j]) = (_utilities[j], _utilities[i]);
        LibraryOrderStore.SaveOrder(UtilityOrderKey, _utilities.Select(u => u.ToString()).ToList());
    }

    private void SwapTabs(Category a, Category b)
    {
        int i = _tabs.IndexOf(a), j = _tabs.IndexOf(b);
        if (i < 0 || j < 0) return;
        (_tabs[i], _tabs[j]) = (_tabs[j], _tabs[i]);
        LibraryOrderStore.SaveOrder(TabOrderKey, _tabs.Select(c => c.ToString()).ToList());
    }

    /// <summary>Asset preview thumbnail, async like the Project window's — keeps repainting while
    /// Unity is still generating it so it pops in instead of staying blank forever.</summary>
    private Texture GetPreview(Entry entry)
    {
        var obj = entry.previewObject;
        if (obj == null && !string.IsNullOrEmpty(entry.previewPath))
            obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.previewPath);
        if (obj == null) return null;

        Texture t = AssetPreview.GetAssetPreview(obj);
        if (t != null) return t;
        if (AssetPreview.IsLoadingAssetPreview(obj.GetInstanceID())) Repaint();
        return AssetPreview.GetMiniThumbnail(obj);
    }

    // ============================================================ scanning (cached — see RequestRescan)

    private static readonly Func<Entry, string> EntryId = e => e.Id;

    private void RescanAll()
    {
        _cache[Category.Enemies] = LibraryOrderStore.Apply(Category.Enemies, ScanEnemies(), EntryId);
        _cache[Category.Bosses] = LibraryOrderStore.Apply(Category.Bosses, ScanBosses(), EntryId);
        _cache[Category.MiniBosses] = new List<Entry>(); // no convention exists yet — see EmptyStateMessage
        _cache[Category.Escenas] = LibraryOrderStore.Apply(Category.Escenas, ScanScenes(), EntryId);
        _cache[Category.Mapas] = LibraryOrderStore.Apply(Category.Mapas, ScanMaps(), EntryId);
        _cache[Category.Player] = LibraryOrderStore.Apply(Category.Player, ScanPlayer(), EntryId);
        _cache[Category.Pasivas] = LibraryOrderStore.Apply(Category.Pasivas,
            ScanScriptables<LegendaryPassive>(p => p.DisplayName, p => p.icon), EntryId);
        _cache[Category.Items] = LibraryOrderStore.Apply(Category.Items,
            ScanScriptables<ItemDefinition>(i => i.DisplayName, i => i.Icon), EntryId);
        _cache[Category.Armas] = LibraryOrderStore.Apply(Category.Armas,
            ScanScriptables<WeaponDefinition>(w => w.DisplayName, w => w.Icon), EntryId);
    }

    /// <summary>Every asset of type <typeparamref name="T"/> in the project (FindAssets, so a new
    /// passive / item / weapon shows up on the next rescan with no list to maintain). Keyed by GUID —
    /// display names are localized and may collide. Click / "Abrir en Inspector" select it; dragging
    /// hands the asset over too, so it can be dropped into an Inspector object field.</summary>
    private static List<Entry> ScanScriptables<T>(Func<T, string> displayName, Func<T, Sprite> icon) where T : ScriptableObject
    {
        var result = new List<Entry>();
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) continue;

            string name = null;
            try { name = displayName(asset); } catch (Exception) { /* Loc sin cargar: se usa el nombre del asset */ }
            if (string.IsNullOrWhiteSpace(name)) name = asset.name;

            var entry = new Entry { id = guid, name = name, previewPath = path, previewObject = icon(asset), dragPath = path };
            entry.actions.Add(("Abrir en Inspector", () => PingAndSelect(path)));
            result.Add(entry);
        }
        return result.OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private List<Entry> ScanEnemies()
    {
        var result = new List<Entry>();
        if (!AssetDatabase.IsValidFolder(ENEMY_PIPELINE_FOLDER)) return result;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ENEMY_PIPELINE_FOLDER }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            if (!file.StartsWith("Enemy_", StringComparison.Ordinal)) continue; // Boss_* -> Bosses category
            string name = file.Substring("Enemy_".Length);

            string artFolder = $"{ART_CHARACTERS_FOLDER}/{name}";
            string importerFolder = $"{ENEMY_IMPORTER_FOLDER}/{name}";
            var paths = new List<string> { path };
            if (AssetDatabase.IsValidFolder(artFolder)) paths.Add(artFolder);
            if (AssetDatabase.IsValidFolder(importerFolder)) paths.Add(importerFolder);

            var entry = new Entry { name = name, previewPath = path, dragPath = path };
            entry.actions.Add(("Abrir prefab", () => OpenPrefab(path)));
            entry.actions.Add(("Eliminar enemigo", () => DeleteWithConfirm("Eliminar enemigo", name, paths, refreshCategory: Category.Enemies)));
            result.Add(entry);
        }
        return result.OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<Entry> ScanBosses()
    {
        var result = new List<Entry>();
        if (!AssetDatabase.IsValidFolder(BOSS_DEFINITION_FOLDER)) return result;

        foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(BossDefinition)}", new[] { BOSS_DEFINITION_FOLDER }))
        {
            string defPath = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(defPath);
            string name = file.StartsWith("Boss_", StringComparison.Ordinal) ? file.Substring("Boss_".Length) : file;

            string prefabPath = $"{ENEMY_PIPELINE_FOLDER}/Boss_{name}.prefab";
            bool hasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;

            var entry = new Entry { name = name, previewPath = hasPrefab ? prefabPath : defPath, dragPath = hasPrefab ? prefabPath : null };
            entry.actions.Add(("Abrir definición", () => PingAndSelect(defPath)));
            if (hasPrefab) entry.actions.Add(("Abrir prefab", () => OpenPrefab(prefabPath)));
            result.Add(entry);
        }
        return result.OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<Entry> ScanScenes()
    {
        var result = new List<Entry>();
        if (!AssetDatabase.IsValidFolder(SCENES_FOLDER)) return result;

        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { SCENES_FOLDER }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            var entry = new Entry { name = name, previewPath = path };
            entry.actions.Add(("Abrir escena", () => OpenScenePrompted(path)));
            result.Add(entry);
        }
        return result.OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Map prefabs have no enforced folder (MapImporter saves wherever the user pointed it),
    /// so they're found by their shape instead of their location: MapImporter.ImportOneMap always
    /// creates a "Collisions" child — a marker no other prefab in this project has.</summary>
    private List<Entry> ScanMaps()
    {
        var result = new List<Entry>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null || go.transform.Find("Collisions") == null) continue;

            string name = Path.GetFileNameWithoutExtension(path);
            string folder = path.Substring(0, path.LastIndexOf('/'));
            // Map Tracer's batch importer names each prefab after its source JSON 1:1 in the same
            // folder ("<folder>/<jsonName>.prefab" — CollisionImporter.cs's ImportMapsBatch) — so a
            // same-named .json next to the prefab, if Unity ever imported it as an asset, is that map's.
            string jsonPath = $"{folder}/{name}.json";
            bool hasJson = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(jsonPath) != null;

            var paths = new List<string> { path };
            if (hasJson) paths.Add(jsonPath);

            var entry = new Entry { name = name, previewPath = path, dragPath = path };
            entry.actions.Add(("Abrir prefab", () => OpenPrefab(path)));
            entry.actions.Add(("Eliminar mapa", () => DeleteWithConfirm("Eliminar mapa", name, paths, refreshCategory: Category.Mapas)));
            result.Add(entry);
        }
        return result.OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>No per-name convention exists for the player (PlayerPack.cs re-skins one fixed
    /// prefab in place) — a single card, open-only. Deliberately no delete action here: there is
    /// nothing to disambiguate/dedupe the way there is for enemies, and this is the game's only
    /// player prefab.</summary>
    private static List<Entry> ScanPlayer()
    {
        var result = new List<Entry>();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH) == null) return result;

        var entry = new Entry { name = "Player", previewPath = PLAYER_PREFAB_PATH, dragPath = PLAYER_PREFAB_PATH };
        entry.actions.Add(("Abrir prefab", () => OpenPrefab(PLAYER_PREFAB_PATH)));
        result.Add(entry);
        return result;
    }

    // ============================================================ actions

    private static void PingAndSelect(string path)
    {
        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        if (obj == null) { EditorUtility.DisplayDialog("Biblioteca Web", $"No se encontró '{path}'.", "OK"); return; }
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
    }

    private static void OpenPrefab(string path)
    {
        var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (obj == null) { EditorUtility.DisplayDialog("Biblioteca Web", $"No se encontró el prefab en '{path}'.", "OK"); return; }
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
        AssetDatabase.OpenAsset(obj); // entra en Prefab Mode, listo para tocar EnemyStats/tuning en el Inspector
    }

    private static void OpenScenePrompted(string path)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return; // cancelado por el usuario
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    /// <summary>Confirms with the FULL path list up front, then trashes (never hard-deletes) each
    /// one that still exists — a path already gone (partial prior cleanup, manual delete) is
    /// skipped and reported, not treated as a failure of the whole operation.</summary>
    private void DeleteWithConfirm(string title, string entryName, List<string> paths, Category refreshCategory)
    {
        if (paths.Count == 0)
        {
            EditorUtility.DisplayDialog(title, $"No se encontró ninguna ruta para '{entryName}'.", "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            title,
            $"Esto mueve a la papelera del sistema (recuperable desde ahí, no desde Unity) TODAS estas rutas de '{entryName}':\n\n" +
            string.Join("\n", paths) +
            "\n\n¿Continuar?",
            "Eliminar", "Cancelar");
        if (!confirmed) return;

        var ok = new List<string>();
        var missing = new List<string>();
        var failed = new List<string>();

        foreach (string p in paths)
        {
            bool exists = AssetDatabase.IsValidFolder(p) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p) != null;
            if (!exists) { missing.Add(p); continue; }
            if (AssetDatabase.MoveAssetToTrash(p)) ok.Add(p);
            else failed.Add(p);
        }
        AssetDatabase.Refresh();
        RequestRescan();

        string summary = $"Eliminadas: {ok.Count}\nYa no existían (omitidas): {missing.Count}\nFallidas: {failed.Count}";
        if (missing.Count > 0) summary += "\n\nYa no existían:\n" + string.Join("\n", missing);
        if (failed.Count > 0) summary += "\n\nFallidas:\n" + string.Join("\n", failed);
        EditorUtility.DisplayDialog(title, summary, "OK");
    }
}

/// <summary>
/// Persists each category's user-chosen card order across Editor sessions. Lives in
/// <c>Library/WebLibraryWindowOrder.json</c> — the <c>Library/</c> folder, not <c>Assets/</c>, on
/// purpose: this is a per-machine/per-Editor-install UI preference (like window layouts, which
/// Unity itself keeps under <c>Library/</c>), not project data — it has no business being
/// version-controlled or shipped, and two people would only fight over it if it were.
///
/// Keyed by entry NAME, not path/GUID: names are already each category's identity (how entries are
/// grouped/deduplicated in the Scan* methods above), and stay stable across a rescan even when the
/// underlying asset moves, unlike a raw path.
/// </summary>
internal static class LibraryOrderStore
{
    [Serializable] private class CategoryOrder { public string category; public List<string> names; }
    [Serializable] private class Wrapper { public List<CategoryOrder> categories = new List<CategoryOrder>(); }

    private static Dictionary<string, List<string>> _data;

    private static string FilePath => Path.GetFullPath(
        Path.Combine(UnityEngine.Application.dataPath, "..", "Library", "WebLibraryWindowOrder.json"));

    private static void EnsureLoaded()
    {
        if (_data != null) return;
        _data = new Dictionary<string, List<string>>();
        try
        {
            if (File.Exists(FilePath))
            {
                var wrapper = JsonUtility.FromJson<Wrapper>(File.ReadAllText(FilePath));
                if (wrapper?.categories != null)
                    foreach (var c in wrapper.categories)
                        if (!string.IsNullOrEmpty(c.category)) _data[c.category] = c.names ?? new List<string>();
            }
        }
        catch (Exception e)
        {
            // A missing/corrupt order file must never block the window from opening — it only ever
            // degrades back to "whatever order the scan returned", never an error.
            Debug.LogWarning($"[WebLibraryWindow] No se pudo leer el orden guardado, se ignora: {e.Message}");
        }
    }

    /// <summary>Reorders <paramref name="scanned"/> to match the persisted order for
    /// <paramref name="category"/>, identifying each entry via <paramref name="nameOf"/>. Entries
    /// with no stored position — new since the order was last saved — are appended at the end, in
    /// the order the scan returned them.</summary>
    public static List<T> Apply<T>(object category, List<T> scanned, Func<T, string> nameOf)
    {
        EnsureLoaded();
        if (!_data.TryGetValue(category.ToString(), out var order) || order.Count == 0) return scanned;

        var byName = new Dictionary<string, T>();
        foreach (var e in scanned) byName[nameOf(e)] = e;

        var result = new List<T>();
        foreach (string n in order)
            if (byName.TryGetValue(n, out var e)) { result.Add(e); byName.Remove(n); }
        foreach (var e in scanned)
            if (byName.ContainsKey(nameOf(e))) result.Add(e);
        return result;
    }

    public static void SaveOrder(object category, List<string> names)
    {
        EnsureLoaded();
        _data[category.ToString()] = names;
        try
        {
            var wrapper = new Wrapper
            {
                categories = _data.Select(kv => new CategoryOrder { category = kv.Key, names = kv.Value }).ToList(),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(wrapper, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebLibraryWindow] No se pudo guardar el orden: {e.Message}");
        }
    }
}
