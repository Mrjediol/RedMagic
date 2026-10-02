using System.Text;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Deja en disco, ya como línea de tiempo, los gestos de los prefabs guardados antes de que un
    /// gesto fuera una lista de pasos (carga + golpe fijos, efectos por fase). Cargarlos ya los
    /// convierte (<see cref="RigGesture.OnAfterDeserialize"/>); esto sólo los vuelve a guardar para que
    /// el dato antiguo desaparezca. Idempotente: un prefab ya convertido no cambia.
    /// </summary>
    public static class RigGestureMigration
    {
        [MenuItem("Tools/Web/Rig/Convertir gestos a pasos (prefabs antiguos)")]
        private static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder();
            int converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<ProceduralRig>() == null) continue;

                // El texto del prefab dice si aún trae el formato antiguo (cargado ya está convertido).
                string text = System.IO.File.ReadAllText(path);
                if (!text.Contains(" strikeTime:")) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var rig = root.GetComponent<ProceduralRig>();
                    rig.FixConvertedWindows();
                    EditorUtility.SetDirty(rig);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    converted++;
                    log.AppendLine($"  {path}: {rig.gestures.Count} gesto(s)");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            return $"[RigGestureMigration] Prefabs convertidos: {converted}\n{log}";
        }
    }
}
