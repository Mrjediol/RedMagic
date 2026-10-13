using RedMagic.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.EditorTools
{
    /// <summary>
    /// Migración de una sola vez: los controles táctiles, la pausa y las opciones vivían dentro de MainHub e Ice1 (y por
    /// eso faltaban en cualquier nivel nuevo). Los pasa a <c>Resources/MenusUI.prefab</c> (ver <see cref="PersistentMenus"/>)
    /// y quita las copias de las dos escenas. Idempotente: si ya no están en MainHub, no hace nada.
    /// Menú: <b>Tools ▸ RedMagic ▸ UI ▸ Migrar menús a prefab persistente</b>.
    /// </summary>
    public static class MenusUiMigration
    {
        private static readonly string[] Names = { "TouchControlsUI", "OptionsMenuUI", "PauseMenuUI" };

        [MenuItem("Tools/RedMagic/UI/Migrar menús a prefab persistente")]
        public static void Migrate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var hub = EditorSceneManager.OpenScene("Assets/Scenes/MainHub.unity", OpenSceneMode.Additive);
            var root = new GameObject("[MenusUI]");
            SceneManager.MoveGameObjectToScene(root, hub);
            root.AddComponent<PersistentMenus>();

            int moved = 0;
            foreach (var n in Names)
            {
                var found = Find(hub, n);
                if (found == null) continue;
                found.transform.SetParent(root.transform, false);
                moved++;
            }

            if (moved == Names.Length)
            {
                System.IO.Directory.CreateDirectory("Assets/Resources");
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/MenusUI.prefab");
                Object.DestroyImmediate(root);
                EditorSceneManager.SaveScene(hub);
            }
            else
            {
                Debug.LogWarning($"[MenusUiMigration] En MainHub sólo hay {moved} de {Names.Length} menús: no se migra (¿ya está hecho?).");
                Object.DestroyImmediate(root);
            }
            EditorSceneManager.CloseScene(hub, true);

            var ice = EditorSceneManager.OpenScene("Assets/Scenes/Ice1.unity", OpenSceneMode.Additive);
            int removed = 0;
            foreach (var n in Names)
            {
                var found = Find(ice, n);
                if (found == null) continue;
                Object.DestroyImmediate(found);
                removed++;
            }
            if (removed > 0) EditorSceneManager.SaveScene(ice);
            EditorSceneManager.CloseScene(ice, true);

            AssetDatabase.SaveAssets();
            Debug.Log($"[MenusUiMigration] Migrados {moved} menús de MainHub a Resources/MenusUI.prefab; quitados {removed} de Ice1.");
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (var r in scene.GetRootGameObjects()) if (r.name == name) return r;
            return null;
        }
    }
}
