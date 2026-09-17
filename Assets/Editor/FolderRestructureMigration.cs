// FolderRestructureMigration.cs
// -----------------------------------------------------------------------------
// ONE-TIME migration: executes the Assets/ folder restructure planned from
// docs/folder-restructure-audit.md, after the vendor cleanup and the dead basic-prefab step
// (EnemyImporter.cs) were already removed. Every move goes through AssetDatabase.MoveAsset (GUIDs
// preserved) — never a filesystem move. Left in the project afterward as a record, same convention
// as VendorCleanupMigration.cs.
//
// Run once via: unity command run_script --file Assets/Editor/FolderRestructureMigration.cs --entry FolderRestructureMigration.Run
//
// ORDERING MATTERS: the dead plural Assets/Prefabs/ must be deleted BEFORE the real singular
// Assets/Prefab/ is renamed to Assets/Prefabs/ — otherwise the rename target already exists and
// AssetDatabase.MoveAsset fails.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FolderRestructureMigration
{
    public static void Run()
    {
        var log = new System.Text.StringBuilder();

        // ---- 1) delete the now-fully-dead plural Prefabs/ folder ----
        if (AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.MoveAssetToTrash("Assets/Prefabs");
            log.AppendLine("TRASHED: Assets/Prefabs (dead plural folder — basic sprite-only prefabs + test junk)");
        }
        else log.AppendLine("Assets/Prefabs already gone.");
        AssetDatabase.Refresh();

        // ---- 2) split Prefab/Maps/: composed map prefabs (have a "Collisions" child — the same
        //         marker WebLibraryWindow.cs uses to find maps) stay and ride along with the
        //         Prefab -> Prefabs rename below; standalone piece prefabs (no such child) move to
        //         Art/Environments/ now, before that rename. ----
        if (!AssetDatabase.IsValidFolder("Assets/Art/Environments"))
            AssetDatabase.CreateFolder("Assets/Art", "Environments");

        if (AssetDatabase.IsValidFolder("Assets/Prefab/Maps"))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab/Maps" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool isComposedMap = go != null && go.transform.Find("Collisions") != null;
                if (isComposedMap) { log.AppendLine($"KEEP (composed map, has Collisions): {path}"); continue; }
                Move(path, $"Assets/Art/Environments/{Path.GetFileName(path)}", log);
            }
        }

        // ---- 3) simple renames ----
        Move("Assets/Enemies", "Assets/Art/EnemyImports", log);
        Move("Assets/Icon", "Assets/Art/Icons", log);
        Move("Assets/Sounds", "Assets/Audio/Sfx", log);

        // Ui/Art/ -> Art/UI/: Art/UI/ already exists (the UI-art-kit pipeline's own output folder,
        // ItemIcons/ItemsUi/Menus), so this can't be a folder-to-folder rename (destination already
        // exists) — move the two loose files in individually instead, then drop the empty source.
        MoveFilesInto("Assets/Ui/Art", "Assets/Art/UI", log);

        // ---- 4) Data/Worlds -> ScriptableObjects/Worlds ----
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        Move("Assets/Data/Worlds", "Assets/ScriptableObjects/Worlds", log);
        if (AssetDatabase.IsValidFolder("Assets/Data") && AssetDatabase.FindAssets("", new[] { "Assets/Data" }).Length == 0)
        {
            AssetDatabase.MoveAssetToTrash("Assets/Data");
            log.AppendLine("TRASHED (now empty): Assets/Data");
        }

        // NOTE deliberately NOT moved, per the audit's own Resources.Load findings (see the report):
        //  - CurrencyConfig/ShopConfig/UpgradeTree/SynergyConfig/PlayerScaleConfig and every
        //    *PanelSettings*/*Skin* asset: all confirmed Resources.Load'd by exact filename.
        //  - Resources/Music/*.mp3: Resources.Load requires a literal "Resources" ancestor folder —
        //    moving these out breaks loading regardless of updating scene sound-list strings first.
        //  - Resources/Bosses/: confirmed safe to move, but never named in the requested move list.

        // ---- 5) the big one: Prefab/ (singular) -> Prefabs/ (plural, now free) ----
        Move("Assets/Prefab", "Assets/Prefabs", log);

        // ---- 6) old top-level Projectiles/ folds into the new Prefabs/Projectiles/ ----
        if (AssetDatabase.IsValidFolder("Assets/Projectiles"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Projectiles"))
            {
                Move("Assets/Projectiles", "Assets/Prefabs/Projectiles", log);
            }
            else
            {
                foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets/Projectiles" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(path)) continue;
                    Move(path, "Assets/Prefabs/Projectiles/" + path.Substring("Assets/Projectiles/".Length), log);
                }
                AssetDatabase.MoveAssetToTrash("Assets/Projectiles");
                log.AppendLine("TRASHED (emptied): Assets/Projectiles");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FolderRestructureMigration]\n" + log);
    }

    private static void Move(string from, string to, System.Text.StringBuilder log)
    {
        bool exists = AssetDatabase.IsValidFolder(from) || AssetDatabase.LoadAssetAtPath<Object>(from) != null;
        if (!exists) { log.AppendLine($"SKIP (not found): {from}"); return; }
        string err = AssetDatabase.MoveAsset(from, to);
        if (string.IsNullOrEmpty(err)) log.AppendLine($"MOVED: {from} -> {to}");
        else { log.AppendLine($"FAILED: {from} -> {to} :: {err}"); Debug.LogError($"[FolderRestructureMigration] MoveAsset failed {from} -> {to}: {err}"); }
    }

    private static void MoveFilesInto(string fromFolder, string toFolder, System.Text.StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder(fromFolder)) { log.AppendLine($"SKIP (not found): {fromFolder}"); return; }
        foreach (string guid in AssetDatabase.FindAssets("", new[] { fromFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path)) continue;
            Move(path, toFolder + "/" + Path.GetFileName(path), log);
        }
        if (AssetDatabase.FindAssets("", new[] { fromFolder }).Length == 0)
        {
            AssetDatabase.MoveAssetToTrash(fromFolder);
            log.AppendLine($"TRASHED (now empty): {fromFolder}");
        }
    }
}
