using System.IO;
using RedMagic.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.BakingTest
{
    /// <summary>
    /// PRUEBA DE BAKING de la energía de la puerta. Todo vive en <c>Assets/Test/Baking</c>; nada del juego apunta aquí:
    ///  1. <b>Hornear</b>: dibuja N fotogramas del ruido (bucle perfecto, loseta periódica) en un Texture2DArray y crea la
    ///     variante de la puerta (un Prefab Variant del original con el shader flipbook).
    ///  2. <b>Poner puertas de prueba</b>: en la escena abierta, 2 puertas vivas y 2 horneadas, para compararlas.
    ///  3. <b>BORRAR</b>: quita las puertas de prueba de la escena y borra la carpeta entera. Sin restos.
    /// Menú: <b>Tools ▸ RedMagic ▸ Baking (prueba)</b>.
    /// </summary>
    public static class DoorFlipbookBaker
    {
        private const string Root = "Assets/Test/Baking";
        private const string Generated = Root + "/Generado";
        private const string ArrayPath = Generated + "/DoorEnergy_flipbook.asset";
        private const string VariantPath = Generated + "/PuertaEnergia_Flipbook.prefab";
        private const string OriginalPath = "Assets/Test/Puertas/PuertaEnergia.prefab";
        private const string TestRootName = "[Baking test]";

        private const int Frames = 16, FrameWidth = 128, FrameHeight = 384;

        [MenuItem("Tools/RedMagic/Baking (prueba)/1 Hornear energía de la puerta y crear la variante")]
        public static void Bake()
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPath);
            var bakeShader = Shader.Find("Hidden/RedMagic/Baking/DoorEnergyBake");
            var flipShader = Shader.Find("RedMagic/Fx Door Energy Flipbook");
            if (original == null || bakeShader == null || flipShader == null)
            {
                Debug.LogError("[Baking] Falta el prefab de la puerta (Tools ▸ RedMagic ▸ Niveles ▸ Crear puertas de prueba) o los shaders.");
                return;
            }

            Directory.CreateDirectory(Generated);

            var material = new Material(bakeShader);
            var rt = RenderTexture.GetTemporary(FrameWidth, FrameHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false, true);
            var array = new Texture2DArray(FrameWidth, FrameHeight, Frames, TextureFormat.R8, false, true)
            {
                name = "DoorEnergy_flipbook",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            var previous = RenderTexture.active;
            try
            {
                for (int f = 0; f < Frames; f++)
                {
                    material.SetFloat("_Phase", f / (float)Frames);
                    Graphics.Blit(null, rt, material);
                    RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0, 0, FrameWidth, FrameHeight), 0, 0);

                    // sólo se guarda el canal R (la densidad): 1 byte por píxel
                    var single = new Texture2D(FrameWidth, FrameHeight, TextureFormat.R8, false, true);
                    single.SetPixels32(readback.GetPixels32());
                    single.Apply(false);
                    Graphics.CopyTexture(single, 0, 0, array, f, 0);
                    Object.DestroyImmediate(single);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(material);
            }

            AssetDatabase.DeleteAsset(ArrayPath);
            AssetDatabase.CreateAsset(array, ArrayPath);

            // la variante: Prefab Variant del original con el shader flipbook y el binder
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
            var door = instance.GetComponent<LevelDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("energyShader").objectReferenceValue = flipShader;
            so.ApplyModifiedPropertiesWithoutUndo();
            var binder = instance.AddComponent<DoorFlipbookBinder>();
            binder.SetFlipbook(array);
            PrefabUtility.SaveAsPrefabAsset(instance, VariantPath);
            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Baking] Energía horneada: {Frames} fotogramas de {FrameWidth}×{FrameHeight} (R8, {Frames * FrameWidth * FrameHeight / 1024} KB). " +
                      $"Variante en {VariantPath}. Ahora: Tools ▸ RedMagic ▸ Baking (prueba) ▸ 2.");
        }

        [MenuItem("Tools/RedMagic/Baking (prueba)/2 Poner puertas de prueba en la escena (vivas vs horneadas)")]
        public static void PlaceTestDoors()
        {
            var live = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPath);
            var baked = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            if (live == null || baked == null) { Debug.LogError("[Baking] Primero el paso 1."); return; }

            RemoveTestObjects();
            var level = Object.FindFirstObjectByType<Level>();
            var root = new GameObject(TestRootName).transform;
            if (level != null) root.SetParent(level.transform, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Baking test");

            // de izquierda a derecha: viva 1×4, horneada 1×4, viva 2×4, horneada 2×4
            (GameObject prefab, int w, string label)[] doors =
            {
                (live, 1, "VIVA 1x4"), (baked, 1, "HORNEADA 1x4"), (live, 2, "VIVA 2x4"), (baked, 2, "HORNEADA 2x4"),
            };
            float x = 8f;
            foreach (var (prefab, w, label) in doors)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = label;
                var door = go.GetComponent<LevelDoor>();
                door.SetCells(w, 4);
                door.SetStartOpen(false);
                float y = level != null ? level.GroundTop(Mathf.RoundToInt(x)) : 0f;
                go.transform.localPosition = new Vector3(x, y, 0f);
                x += w + 3f;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeTransform = root;
            Debug.Log("[Baking] Puertas de prueba puestas (viva, horneada, viva, horneada).");
        }

        [MenuItem("Tools/RedMagic/Baking (prueba)/3 BORRAR todo lo de baking (sin restos)")]
        public static void DeleteAll()
        {
            RemoveTestObjects();
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Baking] Borrado: puertas de prueba de la escena y la carpeta " + Root + ". Guarda la escena.");
        }

        private static void RemoveTestObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (go != null && go.name == TestRootName && go.gameObject.scene == scene)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                        Object.DestroyImmediate(go.gameObject);
                    }
                }
            }
        }
    }
}
