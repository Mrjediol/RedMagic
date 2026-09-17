// VendorCleanupMigration.cs
// -----------------------------------------------------------------------------
// ONE-TIME migration: strips the Brackeys/Cainos/Dragon Warrior Files vendor packs out of the
// project. Executes an explicit, final decision from the user (see
// docs/folder-restructure-audit.md for the read-only audit this acts on) — every external
// reference to a vendor sprite is retargeted to the project's existing placeholder square
// (Assets/Art/Placeholder/Square.png, the same asset Tools/RedMagic/FX/1 already generates and
// uses for weapon/boss FX — reused here rather than inventing a second placeholder convention),
// then the three vendor folders are trashed (AssetDatabase.MoveAssetToTrash — OS-recoverable,
// matching this project's established "never hard-delete" convention, e.g. WebLibraryWindow.cs).
//
// Run once via: unity command run_script --file Assets/Editor/VendorCleanupMigration.cs --entry VendorCleanupMigration.Run
// Left in the project afterward as a record of what this pass did, same convention as
// FxPlaceholderPack's "4 · Migrar FX de jefe a carpeta por jefe" one-time migration tool.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VendorCleanupMigration
{
    private const string PlaceholderSpritePath = "Assets/Art/Placeholder/Square.png";
    private const string DragonWarriorFolder = "Assets/Dragon Warrior Files";

    // Every vendor SPRITE guid confirmed (fresh grep, this session) to be referenced from outside
    // its own pack folder. Anything reachable ONLY from inside Brackeys/Cainos/Dragon Warrior Files
    // itself (e.g. the Cainos ground tileset + its 27 RuleTile sub-assets, both PF Village Props
    // decoration prefabs, SkyBackground.png — all exclusively consumed by the dead
    // Prefab/Maps/Base.prefab) needs no entry here: deleting the vendor folder wholesale already
    // removes them, and Base.prefab is deleted outright below.
    private static readonly Dictionary<string, string> VendorSpriteGuids = new Dictionary<string, string>
    {
        ["7810966f0f690954c87305933cabf2b0"] = "TX Village Props.png (Cainos)",
        ["dfe2da12be5f5994286f281b2cde6656"] = "GiantBeetle.png (Brackeys)",
        ["45b96aab96433304a8dd459f35996197"] = "Pentagram_Activated.png (Brackeys)",
        ["0869b1e96aac6d34cb086976dc7af826"] = "fireball_01.png (Dragon Warrior Files)",
        ["087330196575cb349a5822e5936a769f"] = "dashwind_01.png (Dragon Warrior Files)",
        ["7ee76aa704bf5874e9c6b413729f72fe"] = "dashwind_02.png (Dragon Warrior Files)",
        ["9c56ef6f982e8914daf8714d5bcd9cea"] = "explosion_01.png (Dragon Warrior Files)",
    };
    private const string VendorMaterialGuid = "e2a7255f011495047a05eb59adabd49a"; // MT Village Props - Default.mat

    // Plain field-value swaps only (sprite/material) — no structural component changes.
    private static readonly string[] FieldSwapOnlyTargets =
    {
        "Assets/Prefab/Enemies/Boss_ArbolAncestral.prefab",
        "Assets/Prefab/Enemies/Boss_EspantapajarosMarchito.prefab",
        "Assets/Prefab/Enemies/Boss_GuardianaDePiedra.prefab",
        "Assets/Prefab/Enemies/Boss_PozoMaldito.prefab",
        "Assets/Prefab/Enemies/Boss_ReinaEscarabajo.prefab",
        "Assets/Prefab/Enemies/Boss_SelloProfano.prefab",
        "Assets/Prefab/Eviroment/Shop.prefab",
        "Assets/Prefab/Eviroment/Target.prefab",
        "Assets/Prefab/Eviroment/WeaponUpgrade.prefab",
        // Found DURING this migration, not in the original ask: GoldChest.prefab's SpriteRenderer
        // still carries the Cainos "MT Village Props - Default" MATERIAL — a leftover from before
        // the user replaced its sprite/Animator with their own art. Its sprite/Animator are NOT
        // touched (no vendor sprite guid is present on this prefab at all, so the generic walker
        // below has nothing else to change) — only this dangling material reference, which would
        // otherwise render "Missing Material" the moment Cainos/ is deleted.
        "Assets/Prefab/Eviroment/GoldChest.prefab",
        "Assets/Prefab/Fx/VFX_DoubleJump.prefab", // static sprite only, no Animator on this one
    };

    // These 3 own an Animator whose RuntimeAnimatorController lives INSIDE Dragon Warrior Files
    // (so it disappears when that folder is deleted) — the Animator component itself is removed,
    // not just its controller field cleared, since a controller-less Animator is equally broken and
    // there is no placeholder controller convention to point it at instead. Left as a plain static
    // SpriteRenderer showing the placeholder square.
    private static readonly string[] StripVendorAnimatorTargets =
    {
        "Assets/Prefab/Fx/Fireball.prefab",
        "Assets/Prefab/Fx/VFX_DashWind.prefab",
        "Assets/Prefab/Fx/VFX_Explosion.prefab",
    };

    private static readonly string[] BossAttackAssets =
    {
        "Aguacero", "Chorro", "Desprendimiento", "DiluvioDeCalabazas", "Esquirlas",
        "LluviaDeCalabazas", "Maldicion", "Metralla", "Ritual", "RitualMayor",
        "Rociada", "Runas", "Salivazo", "SalvaDeAbrojos", "TorbellinoDeAbrojos",
    };

    private const string DeadTestPrefab = "Assets/Prefab/Maps/Base.prefab";
    private static readonly string[] VendorFolders = { "Assets/Brackeys", "Assets/Cainos", "Assets/Dragon Warrior Files" };

    private const string ChestOldPath = "Assets/Cainos/Pixel Art Platformer - Village Props/Script/Chest.cs";
    private const string ChestNewPath = "Assets/Scripts/Gameplay/Chest.cs";

    // Cleaned Chest.cs content — drops `using Cainos.LucidEditor;` and every [FoldoutGroup]/
    // [ShowInInspector]/[Button]/[HorizontalGroup] attribute (Cainos' bundled "Lucid Editor"
    // Odin-style inspector sugar — purely cosmetic, not functional). NOT just a move: those
    // attributes live in Assets/Cainos/Third Party/Lucid Editor/, which is deleted along with the
    // rest of Cainos in this same pass, so Chest.cs would fail to compile in its new home
    // otherwise. Field/property/method names are unchanged (animator, IsOpened, Open, Close) so
    // GoldChest.prefab's serialized references to them resolve exactly as before.
    private const string ChestCleanedContent = @"using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Migrado desde Assets/Cainos/Pixel Art Platformer - Village Props/Script/Chest.cs
    /// (limpieza de paquetes de terceros, VendorCleanupMigration) — mismo comportamiento; se
    /// quitaron los atributos de Cainos.LucidEditor (decorativos, sólo Inspector) porque ese
    /// paquete se eliminó del proyecto junto con el resto de Cainos/.
    /// </summary>
    public class Chest : MonoBehaviour
    {
        public Animator animator;

        public bool IsOpened
        {
            get => isOpened;
            set
            {
                isOpened = value;
                animator.SetBool(""IsOpened"", isOpened);
            }
        }
        private bool isOpened;

        public void Open() => IsOpened = true;
        public void Close() => IsOpened = false;
    }
}
";

    public static void Run()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("[VendorCleanupMigration] Start.");

        var placeholder = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
        if (placeholder == null) { Debug.LogError($"[VendorCleanupMigration] ABORT: placeholder sprite not found at {PlaceholderSpritePath}."); return; }
        // Resources.GetBuiltinResource<Material>("Sprites-Default.mat") FAILS SILENTLY in this
        // project (logs an error, returns null — confirmed when this migration first ran) rather
        // than throwing, so a naive null-check-free use of it would have written null material
        // references. Resolve the exact material every other SpriteRenderer in the project already
        // uses instead (guid a97c105638bdf8b4a8650670310a4cd3, confirmed via Enemy_Gorila.prefab) —
        // proven correct for THIS project rather than guessing at a builtin resource name/path.
        var defaultMat = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath("a97c105638bdf8b4a8650670310a4cd3"))
            .OfType<Material>().FirstOrDefault();
        if (defaultMat == null) { Debug.LogError("[VendorCleanupMigration] ABORT: default sprite material not found."); return; }

        int totalRefsSwapped = 0;

        // ---- 1) plain field-value swaps (prefabs) ----
        foreach (string path in FieldSwapOnlyTargets)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { log.AppendLine($"SKIP (not found): {path}"); continue; }
            int hits = 0;
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
                if (comp != null) hits += RetargetReferences(comp, placeholder, defaultMat);
            if (hits > 0) { EditorUtility.SetDirty(go); log.AppendLine($"EDITED ({hits} ref(s)): {path}"); }
            else log.AppendLine($"no vendor refs found: {path}");
            totalRefsSwapped += hits;
        }

        // ---- 2) BossAttack_*.asset (ScriptableObjects) ----
        foreach (string name in BossAttackAssets)
        {
            string path = $"Assets/Resources/Bosses/BossAttack_{name}.asset";
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so == null) { log.AppendLine($"SKIP (not found): {path}"); continue; }
            int hits = RetargetReferences(so, placeholder, defaultMat);
            if (hits > 0) { EditorUtility.SetDirty(so); log.AppendLine($"EDITED ({hits} ref(s)): {path}"); }
            else log.AppendLine($"no vendor refs found: {path}");
            totalRefsSwapped += hits;
        }

        AssetDatabase.SaveAssets();

        // ---- 3) FX prefabs needing a structural change (remove the vendor-controlled Animator) ----
        foreach (string path in StripVendorAnimatorTargets)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) { log.AppendLine($"SKIP (not found): {path}"); continue; }
            int hits = 0;
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
                if (comp != null) hits += RetargetReferences(comp, placeholder, defaultMat);

            var animator = root.GetComponentInChildren<Animator>(true);
            bool removedAnimator = false;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                string ctrlPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                if (ctrlPath.StartsWith(DragonWarriorFolder, StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(animator, true);
                    removedAnimator = true;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            log.AppendLine($"EDITED ({hits} ref(s)){(removedAnimator ? " + Animator removed" : "")}: {path}");
            totalRefsSwapped += hits;
        }

        // ---- 4) move + clean up Chest.cs BEFORE Cainos/ is deleted ----
        string moveError = AssetDatabase.MoveAsset(ChestOldPath, ChestNewPath);
        if (string.IsNullOrEmpty(moveError))
        {
            File.WriteAllText(ChestNewPath, ChestCleanedContent);
            log.AppendLine($"MOVED + cleaned: {ChestOldPath} -> {ChestNewPath}");
        }
        else
        {
            Debug.LogError($"[VendorCleanupMigration] ABORT before deleting vendor folders: could not move Chest.cs ({moveError}). GoldChest.prefab still needs it.");
            return;
        }

        // ---- 5) delete dead test content ----
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DeadTestPrefab) != null)
        {
            AssetDatabase.MoveAssetToTrash(DeadTestPrefab);
            log.AppendLine($"TRASHED: {DeadTestPrefab}");
        }
        else log.AppendLine($"not found (already gone?): {DeadTestPrefab}");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(); // pick up the Chest.cs content rewrite before anything else touches it

        // ---- 6) delete the three vendor folders ----
        foreach (string folder in VendorFolders)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                bool ok = AssetDatabase.MoveAssetToTrash(folder);
                log.AppendLine(ok ? $"TRASHED FOLDER: {folder}" : $"FAILED TO TRASH: {folder}");
            }
            else log.AppendLine($"not found (already gone?): {folder}");
        }

        // ---- 7) prune Build Settings of scenes that no longer exist on disk ----
        var before = EditorBuildSettings.scenes;
        var after = before.Where(s => File.Exists(s.path)).ToArray();
        int removedScenes = before.Length - after.Length;
        EditorBuildSettings.scenes = after;
        log.AppendLine($"Build Settings: {before.Length} -> {after.Length} scenes ({removedScenes} dangling entr{(removedScenes == 1 ? "y" : "ies")} removed).");
        foreach (var s in before.Except(after)) log.AppendLine($"  removed from Build Settings: {s.path}");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine($"[VendorCleanupMigration] Done. {totalRefsSwapped} sprite/material reference(s) retargeted to the placeholder.");
        Debug.Log(log.ToString());
    }

    /// <summary>Walks every visible SerializedProperty on <paramref name="obj"/> (including array
    /// elements, so e.g. a SpriteFlipbook's frame array is covered the same as a single m_Sprite
    /// field) and retargets any ObjectReference currently pointing at a known vendor sprite/material
    /// guid. Returns how many it changed. Generic on purpose — it never assumes a field name, only a
    /// GUID it already confirmed (this session, via grep) is a real vendor dependency, so it cannot
    /// touch anything this migration didn't explicitly account for.</summary>
    private static int RetargetReferences(UnityEngine.Object obj, Sprite placeholder, Material defaultMat)
    {
        var so = new SerializedObject(obj);
        var prop = so.GetIterator();
        int hits = 0;
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = true;
            if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
            var current = prop.objectReferenceValue;
            if (current == null) continue;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(current, out string guid, out long _)) continue;

            if (VendorSpriteGuids.ContainsKey(guid) && current is Sprite)
            {
                prop.objectReferenceValue = placeholder;
                hits++;
            }
            else if (guid == VendorMaterialGuid && current is Material)
            {
                prop.objectReferenceValue = defaultMat;
                hits++;
            }
        }
        if (hits > 0) so.ApplyModifiedPropertiesWithoutUndo();
        return hits;
    }
}
