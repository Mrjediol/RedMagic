using System.IO;
using RedMagic.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Prepara las imágenes de plataforma (Sprite de 1 unidad de ancho, pivote arriba-izquierda), crea un prefab por imagen
    /// en <c>Assets/Test/Plataformas</c> y los pone en fila en la escena abierta. Idempotente. Menú:
    /// <b>Tools ▸ RedMagic ▸ Niveles ▸ Crear plataformas de prueba</b>.
    /// </summary>
    public static class PlatformSetup
    {
        private const string ArtFolder = "Assets/Levels/_Biomas/Hielo/Objetos/Plataformas";
        private const string Folder = "Assets/Test/Plataformas";

        // imagen → ancho en celdas. Elegido para que los ladrillos midan lo mismo (~0,4 celdas) en todas.
        private static readonly (string name, int cells)[] Platforms =
        {
            ("Gruesa_pequena", 4), ("Gruesa_mediana", 8),
            ("Fina_pequena", 6), ("Fina_mediana", 8), ("Fina_larga", 10),
        };

        [MenuItem("Tools/RedMagic/Niveles/Crear plataformas de prueba")]
        public static void CreateTestPlatforms()
        {
            Directory.CreateDirectory(Folder);
            var level = Object.FindFirstObjectByType<Level>();
            var root = new GameObject("Plataformas de prueba").transform;
            if (level != null) root.SetParent(level.transform, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Plataformas de prueba");

            float x = 6f, row = 0f;
            foreach (var (name, cells) in Platforms)
            {
                var sprite = PrepareSprite(ArtFolder + "/" + name + "_cut.png");
                if (sprite == null) { Debug.LogError("Falta la imagen " + name + "_cut.png"); continue; }

                string path = Folder + "/Plataforma_" + name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    var temp = new GameObject("Plataforma " + name);
                    temp.AddComponent<LevelPlatform>().Configure(sprite, cells);
                    prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
                    Object.DestroyImmediate(temp);
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                float y = (level != null ? level.GroundTop(Mathf.RoundToInt(x)) : 0f) + 4f + row;
                go.transform.localPosition = new Vector3(x, y, 0f);
                x += cells + 3f;
                row = (row + 2f) % 6f;   // escalonadas para que se vean todas sin pisarse
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeTransform = root;
            Debug.Log("Plataformas de prueba creadas (prefabs en " + Folder + ").");
        }

        /// <summary>Sprite de 1 unidad de ancho (la plataforma lo escala a sus celdas), pivote arriba-izquierda.</summary>
        private static Sprite PrepareSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float ppu = tex != null ? tex.width : 100f;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu) || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0f, 1f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                // el ancho real sólo se conoce tras importar: segunda pasada con el PPU exacto
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null && !Mathf.Approximately(importer.spritePixelsPerUnit, tex.width))
                {
                    importer.spritePixelsPerUnit = tex.width;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
