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

            // mismas 4 puertas a cada lado: la izquierda VIVA (shader original), la derecha HORNEADA (flipbook)
            (int w, int h)[] sizes = { (1, 3), (1, 4), (1, 5), (2, 4) };
            PlaceRow(root, level, live, sizes, 14f, "VIVA");
            PlaceRow(root, level, baked, sizes, 54f, "HORNEADA");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeTransform = root;
            Debug.Log("[Baking] 4 puertas VIVAS (izquierda, x=14) y 4 HORNEADAS (derecha, x=54), con carteles.");
        }

        private static void PlaceRow(Transform root, Level level, GameObject prefab, (int w, int h)[] sizes, float startX, string label)
        {
            float x = startX;
            float firstX = x, lastX = x;
            foreach (var (w, h) in sizes)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = $"{label} {w}x{h}";
                var door = go.GetComponent<LevelDoor>();
                door.SetCells(w, h);
                door.SetStartOpen(false);
                if (go.TryGetComponent<DoorFlipbookBinder>(out var binder)) binder.Bind();   // tras cambiar el tamaño
                float y = level != null ? level.GroundTop(Mathf.RoundToInt(x)) : 0f;
                go.transform.localPosition = new Vector3(x, y, 0f);
                lastX = x + w;
                x += w + 4f;
            }

            float groundY = level != null ? level.GroundTop(Mathf.RoundToInt(startX)) : 0f;
            var text = new GameObject("Cartel " + label);
            text.transform.SetParent(root, false);
            text.transform.localPosition = new Vector3((firstX + lastX) * 0.5f, groundY + 8f, 0f);
            var mesh = text.AddComponent<TextMesh>();
            mesh.text = label == "VIVA" ? "SIN HORNEAR (viva)" : "HORNEADA (flipbook)";
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.35f;
            mesh.fontSize = 60;
            mesh.color = label == "VIVA" ? new Color(1f, 0.6f, 0.3f) : new Color(0.4f, 1f, 0.6f);
            var renderer = text.GetComponent<MeshRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 100;
        }

        [MenuItem("Tools/RedMagic/Baking (prueba)/3 BORRAR todo lo de baking (sin restos)")]
        public static void DeleteAll()
        {
            RemoveTestObjects();
            ProjectileSheetBaker.RemoveTestObjects();
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
