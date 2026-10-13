using System.IO;
using RedMagic.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Prueba de la puerta de energía: prepara las dos imágenes, crea el prefab y pone tres puertas de distinto tamaño
    /// en la escena abierta (sobre el suelo del <see cref="Level"/>). Idempotente. Menú:
    /// <b>Tools ▸ RedMagic ▸ Niveles ▸ Crear puertas de prueba</b>.
    /// </summary>
    public static class DoorSetup
    {
        private const string ArtFolder = "Assets/Levels/_Biomas/Hielo/Doors";
        private const string OffPath = ArtFolder + "/DoorOff_cut.png", OnPath = ArtFolder + "/DoorOn_cut.png";
        private const string ShaderPath = "Assets/Art/Fx/Shaders/FxDoorEnergy.shader";
        private const string Folder = "Assets/Test/Puertas";
        private const string PrefabPath = Folder + "/PuertaEnergia.prefab";

        [MenuItem("Tools/RedMagic/Niveles/Crear puertas de prueba")]
        public static void CreateTestDoors()
        {
            var off = Prepare(OffPath);
            var on = Prepare(OnPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (off == null || on == null || shader == null) { Debug.LogError("Faltan las imágenes de la puerta o el shader de energía."); return; }
            Directory.CreateDirectory(Folder);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var temp = new GameObject("PuertaEnergia");
                temp.AddComponent<LevelDoor>().Configure(1, 4, false, off, on, shader);
                prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
                Object.DestroyImmediate(temp);
            }

            var level = Object.FindFirstObjectByType<Level>();
            var root = new GameObject("Puertas de prueba").transform;
            if (level != null) root.SetParent(level.transform, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Puertas de prueba");

            (int w, int h, bool open)[] samples = { (1, 3, false), (1, 5, false), (2, 4, false), (1, 4, true) };
            float x = 10f;
            foreach (var s in samples)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = $"Puerta {s.w}x{s.h}" + (s.open ? " (abierta)" : "");
                var door = go.GetComponent<LevelDoor>();
                door.Configure(s.w, s.h, s.open, off, on, shader);
                float y = level != null ? level.GroundTop(Mathf.RoundToInt(x)) : 0f;
                go.transform.localPosition = new Vector3(x, y, 0f);
                x += 6f;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeTransform = root;
            Debug.Log("Puertas de prueba creadas (prefab en " + PrefabPath + ").");
        }

        /// <summary>Ajustes de importación para dibujarla en mallas: sin comprimir ni mipmaps, bordes fijos.</summary>
        private static Texture2D Prepare(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp || importer.textureCompression != TextureImporterCompression.Uncompressed
                || !importer.alphaIsTransparency)
            {
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
