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
    /// La leyenda dice qué es cada carácter: capa (<c>terrain</c> = Grid/Terreno, <c>platforms</c> = Grid/Plataformas)
    /// y nombre del tile. Un tipo nuevo de plataforma = un tile nuevo llamado <c>Plataforma…</c> en
    /// <see cref="LevelSetup.BiomesRoot"/>: la web lo recibe en <c>datos-unity.js</c> y el importador lo busca por nombre.
    /// Sin leyenda (formato v1 de la pestaña vieja): <c>#</c> = roca.
    /// </summary>
    public static class LevelSketchFile
    {
        public const string Format = "redmagic-level";
        public const int Version = 2;
        public const string TerrainLayer = "terrain", PlatformsLayer = "platforms";
        public const string RockTile = "Roca", PlatformTile = "Plataforma";
        private const string FreeChars = "=-~^+*%&@$!?abcdefghijklmnopqrstuvwxyz";

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
            public int version = Version;
            public string name;
            public string biome;
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
        /// Pone el tamaño del boceto y repinta Grid/Terreno y Grid/Plataformas. Sólo toca tiles, tamaño y (si viene)
        /// bioma: lo que se haya añadido a la escena (objetos, enemigos, entrada/salida) se queda como estaba.
        /// </summary>
        public static void Apply(Sketch sketch, Level level)
        {
            // EnsureTiles reescribe la paleta: sólo si falta alguno de los dos tiles base
            TileBase rockTile = FindTile(RockTile), platformTile = FindTile(PlatformTile);
            if (rockTile == null || platformTile == null) LevelSetup.EnsureTiles(out rockTile, out platformTile);
            var tiles = new Dictionary<char, (bool platform, TileBase tile)>();
            foreach (var e in sketch.legend)
            {
                if (string.IsNullOrEmpty(e.@char) || e.@char[0] == '.') continue;
                bool platform = e.layer == PlatformsLayer;
                var tile = FindTile(e.tile);
                if (tile == null)
                {
                    tile = platform ? platformTile : rockTile;
                    Debug.LogWarning($"[EditorNiveles] No hay tile '{e.tile}' en {LevelSetup.BiomesRoot}: '{e.@char}' se pinta con {tile.name}.");
                }
                tiles[e.@char[0]] = (platform, tile);
            }

            var biome = FindBiome(sketch.biome);
            if (!string.IsNullOrEmpty(sketch.biome) && biome == null)
                Debug.LogWarning($"[EditorNiveles] No hay bioma '{sketch.biome}': el nivel se queda con el que tenía.");

            Undo.RecordObject(level, "Importar boceto");
            Undo.RegisterCompleteObjectUndo(level.Terrain, "Importar boceto");
            if (level.Platforms != null) Undo.RegisterCompleteObjectUndo(level.Platforms, "Importar boceto");
            level.SetSize(sketch.width, sketch.height);
            if (biome != null) level.SetBiome(biome);
            level.Terrain.ClearAllTiles();
            if (level.Platforms != null) level.Platforms.ClearAllTiles();

            var rockCells = new List<Vector3Int>(); var rockTiles = new List<TileBase>();
            var platCells = new List<Vector3Int>(); var platTiles = new List<TileBase>();
            int w = sketch.width, h = sketch.height, lost = 0;
            for (int row = 1; row < h - 1; row++) // el marco no se pinta
            {
                string line = sketch.rows[row] ?? "";
                for (int x = 1; x < w - 1 && x < line.Length; x++)
                {
                    if (!tiles.TryGetValue(line[x], out var t)) continue;
                    var cell = new Vector3Int(x, h - 1 - row, 0);
                    if (!t.platform) { rockCells.Add(cell); rockTiles.Add(t.tile); }
                    else if (level.Platforms != null) { platCells.Add(cell); platTiles.Add(t.tile); }
                    else lost++;
                }
            }
            level.Terrain.SetTiles(rockCells.ToArray(), rockTiles.ToArray());
            if (level.Platforms != null) level.Platforms.SetTiles(platCells.ToArray(), platTiles.ToArray());
            if (lost > 0) Debug.LogWarning($"[EditorNiveles] El nivel no tiene Tilemap de plataformas: {lost} celdas de plataforma sin pintar.");
            level.Rebuild();
            EditorUtility.SetDirty(level);
            Debug.Log($"[EditorNiveles] '{sketch.name}' ({w}×{h}: {rockCells.Count} roca, {platCells.Count} plataforma) → {level.name}");
        }

        /// <summary>El nivel tal como está en Unity, en el mismo formato: para seguir editándolo en la web.</summary>
        public static Sketch FromLevel(Level level, string sketchName)
        {
            var legend = new List<LegendEntry> { new LegendEntry { @char = "#", layer = TerrainLayer, tile = RockTile } };
            var platformChars = new Dictionary<string, char>();
            char PlatformChar(TileBase tile)
            {
                string tileName = tile != null ? tile.name : PlatformTile;
                if (platformChars.TryGetValue(tileName, out var c)) return c;
                c = tileName == PlatformTile ? '=' : FreeChars.First(f => f != '=' && legend.All(l => l.@char[0] != f));
                platformChars[tileName] = c;
                legend.Add(new LegendEntry { @char = c.ToString(), layer = PlatformsLayer, tile = tileName });
                return c;
            }

            int w = level.Width, h = level.Height;
            var rows = new string[h];
            var line = new char[w];
            for (int row = 0; row < h; row++)
            {
                int y = h - 1 - row;
                for (int x = 0; x < w; x++)
                {
                    if (level.IsRock(x, y)) line[x] = '#';
                    else if (level.IsPlatform(x, y)) line[x] = PlatformChar(level.Platforms.GetTile(new Vector3Int(x, y, 0)));
                    else line[x] = '.';
                }
                rows[row] = new string(line);
            }
            return new Sketch
            {
                name = sketchName, biome = level.Biome != null ? level.Biome.name : null,
                width = w, height = h, legend = legend.ToArray(), rows = rows,
            };
        }
    }
}
