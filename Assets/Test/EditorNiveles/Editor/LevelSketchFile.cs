using System;
using System.Collections.Generic;
using System.Linq;
using RedMagic.Levels;
using RedMagic.LevelsEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.EditorNiveles
{
    /// <summary>
    /// Formato <c>*.levelsketch.json</c> del editor web EditorNiveles (<c>Web/EditorNiveles/</c>) y su volcado a un
    /// <see cref="Level"/> (y al revés). 1 celda = 1 unidad. <c>rows[0]</c> es la fila de ARRIBA; las filas incluyen el
    /// marco de 1 celda, que el nivel pone solo (no se pinta).
    ///
    /// La leyenda dice qué es cada carácter: capa (<c>terrain</c> = Grid/Terreno, <c>platforms</c> = Grid/Plataformas,
    /// <c>artPlatforms</c> = Grid/PlataformasArte con <see cref="ArtPlatformTile"/>, <c>doors</c> = Grid/Puertas con
    /// <see cref="DoorTile"/>) y nombre del tile. Una celda tiene una sola cosa. Un tipo nuevo = un tile nuevo en
    /// <see cref="LevelSetup.BiomesRoot"/> (<c>Plataforma…</c>, o cualquier ArtPlatformTile / DoorTile): la web lo recibe en
    /// <c>datos-unity.json</c> y el importador lo busca por nombre. Sin leyenda (formato v1): <c>#</c> = roca.
    /// v1/v2 no tienen capas de objetos: reimportarlos deja como estaban las plataformas de arte y puertas pintadas en Unity.
    /// </summary>
    public static class LevelSketchFile
    {
        public const string Format = "redmagic-level";
        public const int Version = 3;
        public const string TerrainLayer = "terrain", PlatformsLayer = "platforms", ArtPlatformsLayer = "artPlatforms", DoorsLayer = "doors";
        public const string RockTile = "Roca", PlatformTile = "Plataforma";

        [Serializable]
        public class LegendEntry
        {
            public string @char;
            public string layer;
            public string tile;
        }

        [Serializable]
        public class Sketch
        {
            public string format = Format;
            /// <summary>Sin el campo en el archivo = 1 (el formato más viejo).</summary>
            public int version = 1;
            public string name;
            public string biome;
            /// <summary>Fondo elegido en la web (🏞️ Fondos): prefab <c>Fondo_&lt;background&gt;</c> si ya se importó.</summary>
            public string background;
            public int width;
            public int height;
            public LegendEntry[] legend;
            public string[] rows;
        }

        public static Sketch Parse(string json, out string error)
        {
            error = null;
            Sketch sketch;
            try { sketch = JsonUtility.FromJson<Sketch>(json); }
            catch (Exception e) { error = "JSON no válido: " + e.Message; return null; }
            if (sketch?.rows == null || sketch.rows.Length != sketch.height || sketch.width < 1)
            {
                error = "Faltan filas o no cuadran con 'height'.";
                return null;
            }
            if (sketch.legend == null || sketch.legend.Length == 0)
                sketch.legend = new[] { new LegendEntry { @char = "#", layer = TerrainLayer, tile = RockTile } };
            return sketch;
        }

        /// <summary>Tiles por nombre (búsqueda en <see cref="LevelSetup.BiomesRoot"/>): así un tile nuevo no necesita cableado.</summary>
        public static TileBase FindTile(string tileName)
        {
            if (string.IsNullOrEmpty(tileName)) return null;
            foreach (var guid in AssetDatabase.FindAssets($"{tileName} t:TileBase", new[] { LevelSetup.BiomesRoot }))
            {
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guid));
                if (tile != null && tile.name == tileName) return tile;
            }
            return null;
        }

        public static LevelBiome FindBiome(string biomeName)
        {
            if (string.IsNullOrEmpty(biomeName)) return null;
            return AssetDatabase.FindAssets("t:LevelBiome")
                .Select(g => AssetDatabase.LoadAssetAtPath<LevelBiome>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(b => b != null && b.name == biomeName);
        }

        /// <summary>
        /// Pone el tamaño del boceto y repinta Grid/Terreno, Grid/Plataformas y (v3) Grid/PlataformasArte y Grid/Puertas,
        /// creando esos dos Tilemaps si faltan. Sólo toca tiles, tamaño y (si viene) bioma: lo que se haya añadido a la
        /// escena (salas, oleadas, enemigos, entrada/salida) se queda como estaba.
        /// </summary>
        public static void Apply(Sketch sketch, Level level)
        {
            // EnsureTiles reescribe la paleta: sólo si falta alguno de los dos tiles base
            TileBase rockTile = FindTile(RockTile), platformTile = FindTile(PlatformTile);
            if (rockTile == null || platformTile == null) LevelSetup.EnsureTiles(out rockTile, out platformTile);

            var artMap = ArtPlatformSetup.EnsureMap(level);
            var doorMap = DoorTileSetup.EnsureMap(level);
            ParkourSetup.EnsureMap(level); // pinchos: aún sólo se pintan en Unity
            bool hasObjects = sketch.version >= 3; // v1/v2 no saben de objetos: no se borran los pintados en Unity

            var maps = new Dictionary<string, Tilemap>
            {
                [TerrainLayer] = level.Terrain, [PlatformsLayer] = level.Platforms,
                [ArtPlatformsLayer] = artMap, [DoorsLayer] = doorMap,
            };
            var tiles = new Dictionary<char, (string layer, TileBase tile)>();
            foreach (var e in sketch.legend)
            {
                if (string.IsNullOrEmpty(e.@char) || e.@char[0] == '.') continue;
                string layer = e.layer != null && maps.ContainsKey(e.layer) ? e.layer : TerrainLayer;
                bool isObject = layer == ArtPlatformsLayer || layer == DoorsLayer;
                var tile = FindTile(e.tile);
                bool fits = tile != null && (layer == ArtPlatformsLayer ? tile is ArtPlatformTile
                    : layer == DoorsLayer ? tile is DoorTile
                    : !(tile is ArtPlatformTile) && !(tile is DoorTile));
                if (!fits)
                {
                    if (isObject)
                    {
                        Debug.LogWarning($"[EditorNiveles] No hay {(layer == DoorsLayer ? "DoorTile" : "ArtPlatformTile")} '{e.tile}' en " +
                                         $"{LevelSetup.BiomesRoot}: las celdas '{e.@char}' no se pintan.");
                        continue;
                    }
                    tile = layer == PlatformsLayer ? platformTile : rockTile;
                    Debug.LogWarning($"[EditorNiveles] No hay tile '{e.tile}' en {LevelSetup.BiomesRoot}: '{e.@char}' se pinta con {tile.name}.");
                }
                if (isObject && !hasObjects) continue;
                tiles[e.@char[0]] = (layer, tile);
            }

            var biome = FindBiome(sketch.biome);
            if (!string.IsNullOrEmpty(sketch.biome) && biome == null)
                Debug.LogWarning($"[EditorNiveles] No hay bioma '{sketch.biome}': el nivel se queda con el que tenía.");

            Undo.RecordObject(level, "Importar boceto");
            var touched = new List<Tilemap> { level.Terrain };
            if (level.Platforms != null) touched.Add(level.Platforms);
            if (hasObjects) { touched.Add(artMap); touched.Add(doorMap); }
            foreach (var m in touched) Undo.RegisterCompleteObjectUndo(m, "Importar boceto");
            level.SetSize(sketch.width, sketch.height);
            if (biome != null) level.SetBiome(biome);
            foreach (var m in touched) m.ClearAllTiles();

            var cells = new Dictionary<string, (List<Vector3Int> at, List<TileBase> tile)>();
            int w = sketch.width, h = sketch.height, lost = 0;
            for (int row = 1; row < h - 1; row++) // el marco no se pinta
            {
                string line = sketch.rows[row] ?? "";
                for (int x = 1; x < w - 1 && x < line.Length; x++)
                {
                    if (!tiles.TryGetValue(line[x], out var t)) continue;
                    if (maps[t.layer] == null) { lost++; continue; }
                    if (!cells.TryGetValue(t.layer, out var list)) cells[t.layer] = list = (new List<Vector3Int>(), new List<TileBase>());
                    list.at.Add(new Vector3Int(x, h - 1 - row, 0));
                    list.tile.Add(t.tile);
                }
            }
            foreach (var kv in cells) maps[kv.Key].SetTiles(kv.Value.at.ToArray(), kv.Value.tile.ToArray());
            if (lost > 0) Debug.LogWarning($"[EditorNiveles] El nivel no tiene Tilemap de plataformas: {lost} celdas de plataforma sin pintar.");
            level.Rebuild();
            // las plataformas de arte y las puertas se regeneran solas al pintar, pero en el siguiente Update: ya, para el recuento
            artMap.GetComponent<ArtPlatformTilemap>().Rebuild();
            var doors = doorMap.GetComponent<DoorTilemap>();
            doors.Rebuild();
            EditorUtility.SetDirty(level);
            if (!string.IsNullOrEmpty(sketch.background))
            {
                var bg = ParallaxImporter.FindBackground(sketch.background);
                if (bg != null) ParallaxImporter.ReplaceBackground(level, bg);
                else Debug.LogWarning($"[EditorNiveles] El nivel usa el fondo '{sketch.background}', que aún no está en Unity: " +
                                      "expórtalo en 🏞️ Fondos e impórtalo con EditorNiveles ▸ Importar fondo…");
            }
            int Count(string layer) => cells.TryGetValue(layer, out var l) ? l.at.Count : 0;
            Debug.Log($"[EditorNiveles] '{sketch.name}' ({w}×{h}: {Count(TerrainLayer)} roca, {Count(PlatformsLayer)} plataforma, " +
                      $"{Count(ArtPlatformsLayer)} plataforma de arte, {doors.Doors.Count} puerta(s)) → {level.name}" +
                      (hasObjects ? "" : $" (boceto v{sketch.version}: plataformas de arte y puertas sin tocar)"));
        }
    }
}
