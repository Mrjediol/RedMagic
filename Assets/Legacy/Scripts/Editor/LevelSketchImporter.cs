using System;
using System.Collections.Generic;
using System.IO;
using RedMagic.Levels;
using RedMagic.LevelsEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.RunEditor
{
    /// <summary>
    /// Vuelca un boceto de la web (pestaña "Boceto de nivel", <c>*.levelsketch.json</c>) en el <see cref="Level"/>
    /// seleccionado: le pone el tamaño del boceto y pinta sus celdas sólidas en el Tilemap de terreno (sin el marco
    /// exterior, que el nivel pone solo). A partir de ahí se sigue editando en Unity con la paleta.
    /// 1 celda del boceto = 1 unidad. Menú: <b>Tools ▸ RedMagic ▸ Niveles ▸ Importar boceto en el nivel...</b>.
    /// (El importador anterior, que generaba un prefab con tiras de borde, está en Legacy.)
    /// </summary>
    public static class LevelSketchImporter
    {
        [Serializable]
        private class Sketch
        {
            public string name;
            public int width;
            public int height;
            public string[] rows;
        }

        // (menú quitado: sustituido por Tools ▸ EditorNiveles, Assets/Test/EditorNiveles)
        private static void Import()
        {
            var level = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Level>() : null;
            if (level == null || level.Terrain == null)
            {
                EditorUtility.DisplayDialog("Importar boceto", "Selecciona en la escena un Nivel con su Tilemap de terreno.", "Vale");
                return;
            }
            string json = EditorUtility.OpenFilePanel("Boceto de nivel", Application.dataPath, "json");
            if (string.IsNullOrEmpty(json)) return;
            Apply(File.ReadAllText(json), level, LevelSetup.EnsureTileAndPalette());
        }

        public static void Apply(string sketchJson, Level level, TileBase tile)
        {
            var sketch = JsonUtility.FromJson<Sketch>(sketchJson);
            if (sketch?.rows == null || sketch.rows.Length != sketch.height)
            {
                Debug.LogError("[LevelSketchImporter] JSON no válido: faltan filas o no cuadran con 'height'.");
                return;
            }

            Undo.RecordObject(level, "Importar boceto");
            Undo.RecordObject(level.Terrain, "Importar boceto");
            level.SetSize(sketch.width, sketch.height);
            level.Terrain.ClearAllTiles();

            var cells = new List<Vector3Int>();
            int w = sketch.width, h = sketch.height;
            for (int row = 1; row < h - 1; row++) // fila 0 = arriba en el boceto
            for (int x = 1; x < w - 1; x++)
                if (x < sketch.rows[row].Length && sketch.rows[row][x] == '#') cells.Add(new Vector3Int(x, h - 1 - row, 0));

            var tiles = new TileBase[cells.Count];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
            level.Terrain.SetTiles(cells.ToArray(), tiles);
            level.Rebuild();
            Debug.Log($"[LevelSketchImporter] '{sketch.name}' ({w}×{h}, {cells.Count} celdas) → {level.name}");
        }
    }
}
