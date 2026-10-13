using RedMagic.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Puertas pintables: crea los <see cref="DoorTile"/> de la puerta de energía (cerrada y abierta), los añade a la paleta y
    /// pone en el Level abierto el Tilemap <c>Puertas</c> (con <see cref="DoorTilemap"/>) con una puerta de 1×4 pintada
    /// de ejemplo. Idempotente. Menú: <b>Tools ▸ RedMagic ▸ Niveles ▸ Preparar puertas pintables</b>.
    /// </summary>
    public static class DoorTileSetup
    {
        private const string ArtFolder = "Assets/Levels/_Biomas/Hielo/Objetos/Doors";
        private const string IconPath = ArtFolder + "/DoorOff_cut.png";
        private const string TilePath = ArtFolder + "/DoorTile_Energia.asset";
        private const string OpenTilePath = ArtFolder + "/DoorTile_Energia_Abierta.asset";
        private const string PalettePath = "Assets/Levels/_Biomas/PaletaPlataformasArte.prefab";
        private const string MapName = "Puertas";

        [MenuItem("Tools/RedMagic/Niveles/Preparar puertas pintables")]
        public static void Setup()
        {
            var prefab = DoorSetup.EnsurePrefab();
            if (prefab == null) return;

            var icon = PrepareIcon();
            var tile = EnsureTile(TilePath, prefab, icon, false);
            var openTile = EnsureTile(OpenTilePath, prefab, PrepareOpenIcon(), true);
            AssetDatabase.SaveAssets();
            AddToPalette(tile, openTile);

            var level = Object.FindFirstObjectByType<Level>();
            if (level == null || level.Terrain == null)
            {
                Debug.LogWarning("Abre un nivel (con Level y Terreno) para crear el Tilemap Puertas. Tile creado.");
                return;
            }
            var grid = level.Terrain.transform.parent;
            var t = grid.Find(MapName);
            if (t == null)
            {
                var created = new GameObject(MapName);
                Undo.RegisterCreatedObjectUndo(created, "Crear Puertas");
                created.transform.SetParent(grid, false);
                t = created.transform;
            }
            var go = t.gameObject;
            if (!go.TryGetComponent<Tilemap>(out var map)) map = go.AddComponent<Tilemap>();
            if (!go.TryGetComponent<TilemapRenderer>(out _)) go.AddComponent<TilemapRenderer>();
            if (!go.TryGetComponent<DoorTilemap>(out _)) go.AddComponent<DoorTilemap>();

            // ejemplo: una puerta de 1×4 sobre el suelo, cerca del inicio
            int x = 5, y = Mathf.RoundToInt(level.GroundTop(5));
            for (int k = 0; k < 4; k++) map.SetTile(new Vector3Int(x, y + k, 0), tile);

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            Debug.Log("Puertas pintables listas: pinta en el Tilemap " + MapName + " con la paleta PaletaPlataformasArte.");
        }

        private static DoorTile EnsureTile(string path, GameObject prefab, Sprite icon, bool startOpen)
        {
            var tile = AssetDatabase.LoadAssetAtPath<DoorTile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<DoorTile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.doorPrefab = prefab.GetComponent<LevelDoor>();
            tile.icon = icon;
            tile.startOpen = startOpen;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        /// <summary>Icono de la puerta abierta en la paleta: la imagen encendida (para distinguirlas a simple vista).</summary>
        private static Sprite PrepareOpenIcon()
        {
            const string path = ArtFolder + "/DoorOn_cut.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float ppu = tex != null ? tex.height : 100f;
            if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>El icono de la paleta: la imagen apagada como Sprite de 1 celda de alto (sigue siendo la textura de la puerta).</summary>
        private static Sprite PrepareIcon()
        {
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer == null) return null;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            float ppu = tex != null ? tex.height : 100f;
            if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        }

        private static void AddToPalette(TileBase tile, TileBase openTile)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath) == null)
            {
                Debug.LogWarning("Falta la paleta " + PalettePath + " (Preparar plataformas de arte la crea). El tile no se añadió.");
                return;
            }
            var contents = PrefabUtility.LoadPrefabContents(PalettePath);
            var map = contents.GetComponentInChildren<Tilemap>();
            if (map != null)
            {
                map.SetTile(new Vector3Int(0, 1, 0), tile);
                map.SetTile(new Vector3Int(1, 1, 0), openTile);
            }
            PrefabUtility.SaveAsPrefabAsset(contents, PalettePath);
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
