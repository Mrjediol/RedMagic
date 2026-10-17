using System.Linq;
using RedMagic.Levels;
using RedMagic.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Parkour: pinchos de cristal pintables, cofre de oro y plataforma de reaparición.
    /// <b>Tools ▸ RedMagic ▸ Niveles ▸ Preparar parkour</b> (idempotente): importa el arte de <c>Hielo/Objetos</c>, crea el
    /// <see cref="SpikeTile"/> y lo pone en la paleta <c>PaletaPlataformasArte</c>, los prefabs del cofre y de la plataforma
    /// (en <c>Assets/Test/Parkour</c>) y, si hay un nivel abierto, su Tilemap <c>Grid/Pinchos</c>.
    /// <b>GameObject ▸ RedMagic ▸ Sala de parkour</b>: una sala (<see cref="LevelRoom"/> + <see cref="ParkourRoom"/>) con su
    /// plataforma a la izquierda y su cofre a la derecha, sobre el suelo, donde mira la vista de escena.
    /// </summary>
    public static class ParkourSetup
    {
        private const string ArtFolder = "Assets/Levels/_Biomas/Hielo/Objetos";
        private const string SpikeFolder = ArtFolder + "/Pinchos";
        private const string SpikeTilePath = SpikeFolder + "/SpikeTile_Cristal.asset";
        // arte en vista lateral (2D…); los _cut son copias recortadas de 2DSpawn / 2DSpawnOff (sin el margen del lienzo)
        private const string ChestSheet = ArtFolder + "/2DOpenChest.png";
        private const string PadOn = ArtFolder + "/2DSpawn_cut.png", PadOff = ArtFolder + "/2DSpawnOff_cut.png";
        private const string PalettePath = "Assets/Levels/_Biomas/PaletaPlataformasArte.prefab";
        private const string PrefabFolder = "Assets/Test/Parkour";
        public const string ChestPrefabPath = PrefabFolder + "/CofreOro.prefab";
        public const string PadPrefabPath = PrefabFolder + "/PlataformaReaparicion.prefab";
        private const string MapName = "Pinchos";
        private const float SpikePixelsPerCell = 256f; // Cristal1_* miden 256 (1 celda), Cristal2_* 512 (2 celdas)
        private const float ChestPixelsPerUnit = 175f;  // fotograma de 265 px ≈ 1,5 celdas

        [MenuItem("Tools/RedMagic/Niveles/Preparar parkour")]
        public static void Setup()
        {
            var tile = EnsureSpikeTile();
            AddToPalette(tile);
            EnsureChestPrefab();
            EnsurePadPrefab();
            AssetDatabase.SaveAssets();

            var level = Object.FindFirstObjectByType<Level>();
            if (level == null || level.Terrain == null)
            {
                Debug.Log("Parkour listo (tile, paleta y prefabs). Abre un nivel para crear su Tilemap Pinchos.");
                return;
            }
            var map = EnsureMap(level);
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            Selection.activeGameObject = map.gameObject;
            Debug.Log("Parkour listo: pinta los cristales en Grid/" + MapName + " con la paleta PaletaPlataformasArte, y crea salas con GameObject ▸ RedMagic ▸ Sala de parkour.");
        }

        /// <summary>El Tilemap <c>Grid/Pinchos</c> del nivel con <see cref="SpikeTilemap"/> (renderer apagado); lo crea si falta.</summary>
        public static Tilemap EnsureMap(Level level)
        {
            var grid = level.Terrain.transform.parent;
            var t = grid.Find(MapName);
            if (t == null)
            {
                var created = new GameObject(MapName);
                Undo.RegisterCreatedObjectUndo(created, "Crear Pinchos");
                created.transform.SetParent(grid, false);
                t = created.transform;
            }
            var go = t.gameObject;
            if (!go.TryGetComponent<Tilemap>(out var map)) map = go.AddComponent<Tilemap>();
            if (!go.TryGetComponent<TilemapRenderer>(out _)) go.AddComponent<TilemapRenderer>();
            if (!go.TryGetComponent<SpikeTilemap>(out _)) go.AddComponent<SpikeTilemap>();
            return map;
        }

        // ------------------------------------------------------------------ sala

        [MenuItem("GameObject/RedMagic/Sala de parkour", false, 11)]
        private static void CreateRoom(MenuCommand command)
        {
            Setup();
            var level = Object.FindFirstObjectByType<Level>();
            var pivot = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            var size = new Vector2Int(16, 7);
            int x0 = Mathf.RoundToInt(pivot.x - size.x * 0.5f);
            if (level != null) x0 = Mathf.Clamp(x0, 1, Mathf.Max(1, level.Width - size.x - 1));
            int ground = level != null ? Mathf.RoundToInt(level.GroundTop(x0 + 2)) : Mathf.RoundToInt(pivot.y);

            var go = new GameObject("Sala_Parkour");
            Undo.RegisterCreatedObjectUndo(go, "Crear sala de parkour");
            var room = go.AddComponent<LevelRoom>();
            room.SetArea(new Vector2(x0, ground), size);
            go.AddComponent<ParkourRoom>();

            var pad = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PadPrefabPath));
            pad.transform.SetParent(go.transform, true);
            pad.transform.position = new Vector3(x0 + 2.5f, level != null ? level.GroundTop(x0 + 2) : ground, 0f);

            int cx = x0 + size.x - 3;
            var chest = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath));
            chest.transform.SetParent(go.transform, true);
            float chestGround = level != null ? level.GroundTop(cx) : ground;
            chest.transform.position = new Vector3(cx + 0.5f, chestGround + ChestHalfHeight(), 0f);

            Selection.activeGameObject = go;
            Debug.Log("Sala de parkour creada: ajusta el rectángulo con las asas, pinta los cristales en Grid/Pinchos y una puerta " +
                      "cerrada (DoorTile_Energia) en el borde del final; el cofre la abre.", go);
        }

        private static float ChestHalfHeight()
        {
            var s = AssetDatabase.LoadAllAssetsAtPath(ChestSheet).OfType<Sprite>().FirstOrDefault();
            return s != null ? s.bounds.extents.y : 0.8f;
        }

        // ------------------------------------------------------------------ tile de pinchos

        private static SpikeTile EnsureSpikeTile()
        {
            var one = new[] { PrepareSprite(SpikeFolder + "/Cristal1_a.png"), PrepareSprite(SpikeFolder + "/Cristal1_b.png") };
            var two = new[] { PrepareSprite(SpikeFolder + "/Cristal2_a.png"), PrepareSprite(SpikeFolder + "/Cristal2_b.png") };
            var tile = AssetDatabase.LoadAssetAtPath<SpikeTile>(SpikeTilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<SpikeTile>();
                AssetDatabase.CreateAsset(tile, SpikeTilePath);
            }
            tile.oneCell = one.Where(s => s != null).ToArray();
            tile.twoCells = two.Where(s => s != null).ToArray();
            tile.icon = tile.oneCell.FirstOrDefault();
            EditorUtility.SetDirty(tile);
            return tile;
        }

        /// <summary>Sprite con pivote abajo en el centro, 256 px por celda, sin mipmaps.</summary>
        private static Sprite PrepareSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("Falta " + path); return null; }
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, SpikePixelsPerCell) || importer.mipmapEnabled ||
                settings.spriteAlignment != (int)SpriteAlignment.BottomCenter)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = SpikePixelsPerCell;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void AddToPalette(TileBase tile)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath) == null)
            {
                Debug.LogWarning("Falta la paleta " + PalettePath + " (Preparar plataformas de arte la crea). El tile no se añadió.");
                return;
            }
            var contents = PrefabUtility.LoadPrefabContents(PalettePath);
            var map = contents.GetComponentInChildren<Tilemap>();
            if (map != null) map.SetTile(new Vector3Int(0, 2, 0), tile);
            PrefabUtility.SaveAsPrefabAsset(contents, PalettePath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        // ------------------------------------------------------------------ prefabs

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Test", "Parkour");
        }

        private static void EnsureChestPrefab()
        {
            var importer = AssetImporter.GetAtPath(ChestSheet) as TextureImporter;
            if (importer != null && (!Mathf.Approximately(importer.spritePixelsPerUnit, ChestPixelsPerUnit) || importer.mipmapEnabled))
            {
                importer.spritePixelsPerUnit = ChestPixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            // en orden de lectura: filas de arriba abajo, cada una de izquierda a derecha (vale para 1 fila o varias)
            var frames = AssetDatabase.LoadAllAssetsAtPath(ChestSheet).OfType<Sprite>()
                .OrderByDescending(s => Mathf.RoundToInt(s.rect.y / 50f)).ThenBy(s => s.rect.x).ToArray();
            if (frames.Length == 0) { Debug.LogWarning("La hoja " + ChestSheet + " no tiene sprites cortados."); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath) != null)
            {
                // ya existe: sólo se viste con el arte (fotogramas, sprite y tamaño de la zona de interactuar)
                var contents = PrefabUtility.LoadPrefabContents(ChestPrefabPath);
                DressChest(contents, frames);
                PrefabUtility.SaveAsPrefabAsset(contents, ChestPrefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            EnsureFolder();
            var root = new GameObject("CofreOro");
            root.AddComponent<BoxCollider2D>().isTrigger = true;
            var art = new GameObject("Arte");
            art.transform.SetParent(root.transform, false);
            var sr = art.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = LevelSorting.Layer;
            sr.sortingOrder = LevelSorting.OverTerrain + 3;
            root.AddComponent<LevelChest>();
            DressChest(root, frames);
            PrefabUtility.SaveAsPrefabAsset(root, ChestPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void DressChest(GameObject root, Sprite[] frames)
        {
            var sr = root.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sprite = frames[0];
            if (root.TryGetComponent<BoxCollider2D>(out var box))
                box.size = new Vector2(frames[0].bounds.size.x + 1f, frames[0].bounds.size.y + 0.5f);
            var so = new SerializedObject(root.GetComponent<LevelChest>());
            var list = so.FindProperty("frames");
            list.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsurePadPrefab()
        {
            var on = PreparePadSprite(PadOn);
            var off = PreparePadSprite(PadOff);
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PadPrefabPath) != null;
            if (!exists) EnsureFolder();
            var root = exists ? PrefabUtility.LoadPrefabContents(PadPrefabPath) : new GameObject("PlataformaReaparicion");
            if (!root.TryGetComponent<RespawnPad>(out var pad)) pad = root.AddComponent<RespawnPad>();
            var so = new SerializedObject(pad);
            so.FindProperty("onSprite").objectReferenceValue = on;
            so.FindProperty("offSprite").objectReferenceValue = off;
            so.FindProperty("heightScale").floatValue = 1f; // arte de vista lateral: sin aplastar
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PadPrefabPath);
            if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
        }

        private static Sprite PreparePadSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("Falta " + path); return null; }
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
