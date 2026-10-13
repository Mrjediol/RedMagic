using System.IO;
using RedMagic.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Prueba de las plataformas-objeto: crea un prefab por estilo en <c>Assets/Test/Plataformas</c> y los pone en la
    /// escena abierta, en fila sobre el suelo del <see cref="Level"/>. Idempotente. Menú:
    /// <b>Tools ▸ RedMagic ▸ Niveles ▸ Crear plataformas de prueba</b>.
    /// </summary>
    public static class CodedPlatformSetup
    {
        private const string Folder = "Assets/Test/Plataformas";
        private const string IceBiomePath = "Assets/Levels/_Biomas/Hielo/Hielo.asset";

        public static void CreateTestPlatforms()
        {
            var biome = AssetDatabase.LoadAssetAtPath<LevelBiome>(IceBiomePath);
            if (biome == null) { Debug.LogError("Falta el bioma de hielo: " + IceBiomePath); return; }
            Directory.CreateDirectory(Folder);

            var level = Object.FindFirstObjectByType<Level>();
            var root = new GameObject("Plataformas de prueba").transform;
            if (level != null) root.SetParent(level.transform, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Plataformas de prueba");

            var styles = (CodedPlatform.Style[])System.Enum.GetValues(typeof(CodedPlatform.Style));
            float x = 8f;
            foreach (var style in styles)
            {
                string path = Folder + "/Plataforma_" + style + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    var temp = new GameObject("Plataforma " + style);
                    temp.AddComponent<CodedPlatform>().Configure(style, 6f, 0f, biome);
                    prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
                    Object.DestroyImmediate(temp);
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                // 4 unidades sobre el suelo de esa columna, para saltar a ellas desde el suelo
                float y = level != null ? level.GroundTop(Mathf.RoundToInt(x)) + 3f : 3f;
                instance.transform.localPosition = new Vector3(x, y, 0f);
                x += 10f;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeTransform = root;
            Debug.Log("Plataformas de prueba creadas en " + Folder + " y puestas en la escena.");
        }
    }
}
