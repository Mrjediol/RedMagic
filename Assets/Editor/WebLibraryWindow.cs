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
// PER-CATEGORY DISPLAY ORDER: AssetDatabase has no notion of a user-chosen order, so one is kept
// separately — a small JSON file under Library/ (not Assets/, so it's local to this machine/Editor
// install, never versioned as project data — see LibraryOrderStore). Cards get ↑/↓ buttons instead
// of drag-reorder: IMGUI drag-and-drop between arbitrary grid cells is a lot of fiddly hit-testing
// for a "quick navigation" tool, while ↑/↓ is a few lines and covers the same need (move an item a
// few slots either way) just as well for a grid this size.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RedMagic.Bosses;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class WebLibraryWindow : EditorWindow
{
    private enum Category { Enemies, Bosses, MiniBosses, Escenas, Mapas, Player }

    private static readonly (Category cat, string label)[] Categories =
    {
        (Category.Enemies, "Enemies"),
        (Category.Bosses, "Bosses"),
        (Category.MiniBosses, "MiniBosses"),
        (Category.Escenas, "Escenas"),
        (Category.Mapas, "Mapas"),
        (Category.Player, "Player"),
    };

    private const string ENEMY_PIPELINE_FOLDER = "Assets/Prefabs/Enemies";   // Enemy_<Name>.prefab / Boss_<Name>.prefab — the ONLY source for the Enemies grid
    private const string ART_CHARACTERS_FOLDER = "Assets/Art/Characters";
    private const string ENEMY_IMPORTER_FOLDER = "Assets/Art/EnemyImports";
    private const string BOSS_DEFINITION_FOLDER = "Assets/Resources/Bosses";
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/Player.prefab";
    private const string SCENES_FOLDER = "Assets/Scenes";

    private class Entry
    {
        public string name;
        public string previewPath;      // asset to draw a thumbnail for / the default "open" target
        public List<(string label, Action action)> actions = new List<(string, Action)>();
    }

    [MenuItem("Tools/Web/Biblioteca")]
    public static void Open()
    {
        var win = GetWindow<WebLibraryWindow>();
        win.titleContent = new GUIContent("Biblioteca Web");
        win.minSize = new Vector2(480, 300);
        win.Show();
    }

    private Category _category = Category.Enemies;
    private Vector2 _sidebarScroll, _gridScroll;
    private bool _dirty = true; // scans are cached, not re-run every OnGUI — see RequestRescan()
    private readonly Dictionary<Category, List<Entry>> _cache = new Dictionary<Category, List<Entry>>();

    private void OnEnable() => _dirty = true;

    private void RequestRescan() => _dirty = true;

    private void OnGUI()
    {
        if (_dirty)
        {
            RescanAll();
            _dirty = false;
        }

        EditorGUILayout.BeginHorizontal();
        DrawSidebar();
        DrawContent();
        EditorGUILayout.EndHorizontal();
    }

    // ============================================================ sidebar

    private void DrawSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(140), GUILayout.ExpandHeight(true));
        _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

        foreach (var (cat, label) in Categories)
        {
            bool selected = _category == cat;
            var prevColor = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = new Color(0.45f, 0.72f, 1f);
            if (GUILayout.Button(label, GUILayout.Height(30))) _category = cat;
            GUI.backgroundColor = prevColor;
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("🔄 Actualizar", GUILayout.Height(24))) RequestRescan();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ============================================================ content grid

    private const float CardWidth = 140f, CardHeight = 216f, CardSpacing = 8f;

    private void DrawContent()
    {
        EditorGUILayout.BeginVertical();

        var (_, label) = Categories.First(c => c.cat == _category);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

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

        for (int i = 0; i < entries.Count; i += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + columns, entries.Count); j++)
            {
                DrawCard(entries, j);
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
            default:
                return "Nada encontrado.";
        }
    }

    private void DrawCard(List<Entry> entries, int index)
    {
        Entry entry = entries[index];
        EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(CardWidth), GUILayout.Height(CardHeight));

        Rect previewRect = GUILayoutUtility.GetRect(CardWidth - 8, 96, GUILayout.ExpandWidth(false));
        Texture preview = GetPreview(entry.previewPath);
        if (preview != null) GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
        else EditorGUI.LabelField(previewRect, "(sin vista previa)", EditorStyles.centeredGreyMiniLabel);

        GUILayout.Label(entry.name, EditorStyles.wordWrappedLabel);

        foreach (var (btnLabel, action) in entry.actions)
        {
            if (GUILayout.Button(btnLabel)) action();
        }

        // Orden manual persistente (Library/, no Assets/ — ver LibraryOrderStore) — ↑/↓ en vez de
        // arrastrar: cubre el mismo caso de uso ("mover un item unas cuantas posiciones") con mucho
        // menos código IMGUI que un drag-and-drop entre celdas.
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = index > 0;
        if (GUILayout.Button("↑", GUILayout.Width((CardWidth - 8) / 2))) MoveEntry(_category, index, -1);
        GUI.enabled = index < entries.Count - 1;
        if (GUILayout.Button("↓", GUILayout.Width((CardWidth - 8) / 2))) MoveEntry(_category, index, 1);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>Asset preview thumbnail, async like the Project window's — keeps repainting while
    /// Unity is still generating it so it pops in instead of staying blank forever.</summary>
    private Texture GetPreview(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        if (obj == null) return null;

        Texture t = AssetPreview.GetAssetPreview(obj);
        if (t != null) return t;
        if (AssetPreview.IsLoadingAssetPreview(obj.GetInstanceID())) Repaint();
        return AssetPreview.GetMiniThumbnail(obj);
    }

    // ============================================================ scanning (cached — see RequestRescan)

    private static readonly Func<Entry, string> EntryName = e => e.name;

    private void RescanAll()
    {
        _cache[Category.Enemies] = LibraryOrderStore.Apply(Category.Enemies, ScanEnemies(), EntryName);
        _cache[Category.Bosses] = LibraryOrderStore.Apply(Category.Bosses, ScanBosses(), EntryName);
        _cache[Category.MiniBosses] = new List<Entry>(); // no convention exists yet — see EmptyStateMessage
        _cache[Category.Escenas] = LibraryOrderStore.Apply(Category.Escenas, ScanScenes(), EntryName);
        _cache[Category.Mapas] = LibraryOrderStore.Apply(Category.Mapas, ScanMaps(), EntryName);
        _cache[Category.Player] = LibraryOrderStore.Apply(Category.Player, ScanPlayer(), EntryName);
    }

    /// <summary>Swaps entry <paramref name="index"/> with its neighbour in <paramref name="direction"/>
    /// (-1 up/left, +1 down/right in reading order) within the CURRENTLY CACHED list — no rescan
    /// needed, the in-memory order already IS the display order — and persists the result.</summary>
    private void MoveEntry(Category cat, int index, int direction)
    {
        if (!_cache.TryGetValue(cat, out var entries)) return;
        int j = index + direction;
        if (j < 0 || j >= entries.Count) return;

        (entries[index], entries[j]) = (entries[j], entries[index]);
        LibraryOrderStore.SaveOrder(cat, entries.Select(e => e.name).ToList());
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

            var entry = new Entry { name = name, previewPath = path };
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

            var entry = new Entry { name = name, previewPath = hasPrefab ? prefabPath : defPath };
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

            var entry = new Entry { name = name, previewPath = path };
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

        var entry = new Entry { name = "Player", previewPath = PLAYER_PREFAB_PATH };
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
