using System.IO;
using RedMagic.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Plataformas de arte pintables: prepara los sprites de <c>.../Objetos/Plataformas/Tiles</c>, crea un
    /// <see cref="ArtPlatformTile"/> por estilo y su paleta, añade el Tilemap <c>PlataformasArte</c> al Level abierto y pinta
    /// una de cada con tres largos para probar. Idempotente. Menú:
    /// <b>Tools ▸ RedMagic ▸ Niveles ▸ Preparar plataformas de arte</b>.
    /// </summary>
    public static class ArtPlatformSetup
    {
        private const string TilesFolder = "Assets/Levels/_Biomas/Hielo/Objetos/Plataformas/Tiles";
        private const string PaletteFolder = "Assets/Levels/_Biomas";
        private const string PaletteName = "PaletaPlataformasArte";
        private const string MapName = "PlataformasArte";
        private static readonly string[] Styles = { "FinaPequena", "FinaMediana", "FinaLarga" };

        [MenuItem("Tools/RedMagic/Niveles/Preparar plataformas de arte")]
        public static void Setup()
        {
            var tiles = new ArtPlatformTile[Styles.Length];
            for (int i = 0; i < Styles.Length; i++) tiles[i] = EnsureTile(Styles[i]);
            EnsurePalette(tiles);

            var level = Object.FindFirstObjectByType<Level>();
            if (level == null || level.Terrain == null)
            {
                Debug.LogWarning("Abre un nivel (con Level y Terreno) para crear el Tilemap PlataformasArte. Tiles y paleta creados.");
                return;
            }
            var grid = level.Terrain.transform.parent;
            var mapTransform = grid.Find(MapName);
            if (mapTransform == null)
            {
                var go = new GameObject(MapName);
                Undo.RegisterCreatedObjectUndo(go, "Crear PlataformasArte");
                go.transform.SetParent(grid, false);
                mapTransform = go.transform;
            }
            var go2 = mapTransform.gameObject;
            // (no usar "GetComponent(...) ?? AddComponent(...)": en el editor un componente que falta no es null para ??)
            if (!go2.TryGetComponent<Tilemap>(out var map)) map = go2.AddComponent<Tilemap>();
            if (!go2.TryGetComponent<TilemapRenderer>(out var renderer)) renderer = go2.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = LevelSorting.Layer;
            renderer.sortingOrder = LevelSorting.OverTerrain;
            if (go2.GetComponent<ArtPlatformTilemap>() == null) go2.AddComponent<ArtPlatformTilemap>();

            // una de cada estilo, de 2, 5 y 9 celdas, sobre el suelo del nivel, para ver extremos y centro
            int[] lengths = { 2, 5, 9 };
            int x = 8;
            for (int s = 0; s < tiles.Length; s++)
            {
                int y = Mathf.RoundToInt(level.GroundTop(x)) + 3 + s * 2;
                for (int k = 0; k < lengths[s]; k++) map.SetTile(new Vector3Int(x + k, y, 0), tiles[s]);
                x += lengths[s] + 4;
            }

            EditorSceneManager.MarkSceneDirty(go2.scene);
            Selection.activeGameObject = go2;
            Debug.Log("Plataformas de arte listas: pinta en " + MapName + " con la paleta " + PaletteName + " (Window ▸ 2D ▸ Tile Palette).");
        }

        private static ArtPlatformTile EnsureTile(string style)
        {
            string path = $"{TilesFolder}/ArtTile_{style}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<ArtPlatformTile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<ArtPlatformTile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.left = PrepareSprite($"{TilesFolder}/{style}_izq.png");
            tile.center = PrepareSprite($"{TilesFolder}/{style}_centro.png");
            tile.right = PrepareSprite($"{TilesFolder}/{style}_der.png");
            tile.single = PrepareSprite($"{TilesFolder}/{style}_solo.png");
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssets();
            return tile;
        }

        /// <summary>Sprite de celda entera: 1 unidad = su ancho, pivote en el centro (como un tile normal).</summary>
        private static Sprite PrepareSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float ppu = tex != null ? tex.width : 100f;
            if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu) || importer.mipmapEnabled)
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
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null && !Mathf.Approximately(importer.spritePixelsPerUnit, tex.width))
                {
                    importer.spritePixelsPerUnit = tex.width;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsurePalette(ArtPlatformTile[] tiles)
        {
            string path = $"{PaletteFolder}/{PaletteName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var created = GridPaletteUtility.CreateNewPalette(PaletteFolder, PaletteName, GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
                if (created != null) path = AssetDatabase.GetAssetPath(created);
            }
            var contents = PrefabUtility.LoadPrefabContents(path);
            var map = contents.GetComponentInChildren<Tilemap>();
            if (map != null) for (int i = 0; i < tiles.Length; i++) map.SetTile(new Vector3Int(i, 0, 0), tiles[i]);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.SaveAssets();
        }
    }
}
