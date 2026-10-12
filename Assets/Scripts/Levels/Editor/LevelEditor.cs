using RedMagic.Levels;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.LevelsEditor
{
    /// <summary>Inspector del nivel: el tamaño en unidades con su equivalente en px del boceto, y atajos.</summary>
    [CustomEditor(typeof(Level))]
    public class LevelEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var level = (Level)target;
            serializedObject.Update();

            var width = serializedObject.FindProperty("width");
            var height = serializedObject.FindProperty("height");

            EditorGUILayout.LabelField("Tamaño (1 unidad = 1 celda = 40 px del boceto)", EditorStyles.boldLabel);
            SizeField(width, "Ancho", Level.MaxWidth);
            SizeField(height, "Alto", Level.MaxHeight);
            if (height.intValue >= Level.MaxHeight)
                EditorGUILayout.HelpBox($"Alto máximo: {Level.MaxHeight} unidades = {Level.MaxHeight * Level.SketchPixelsPerUnit:0} px del boceto.", MessageType.Info);
            EditorGUILayout.HelpBox("El marco de roca de 1 celda, los límites de cámara, el collider y el fondo se ajustan " +
                                    "solos al tamaño. Lo pintado dentro se conserva.", MessageType.None);

            EditorGUILayout.Space();
            DrawPropertiesExcluding(serializedObject, "m_Script", "width", "height");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(level.Terrain == null))
                    if (GUILayout.Button("🖌 Pintar roca", GUILayout.Height(26))) Paint(level.Terrain, false);
                using (new EditorGUI.DisabledScope(level.Platforms == null))
                    if (GUILayout.Button("🖌 Pintar plataformas", GUILayout.Height(26))) Paint(level.Platforms, true);
            }
            if (GUILayout.Button("Regenerar")) level.Rebuild();
        }

        /// <summary>
        /// Abre la Tile Palette lista para pintar: Tilemap destino (Terreno o Plataformas), paleta del terreno, el tile
        /// en el pincel y la herramienta Pincel activa. Así no se pinta una plataforma en el Tilemap de roca por error.
        /// </summary>
        private static void Paint(Tilemap map, bool platform)
        {
            var tile = LevelSetup.LoadTile(platform, out var palette);
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
            Selection.activeGameObject = map.gameObject;
            GridPaintingState.scenePaintTarget = map.gameObject;
            if (palette != null) GridPaintingState.palette = palette;
            if (GridPaintingState.gridBrush is GridBrush brush)
            {
                brush.Init(Vector3Int.one);
                brush.SetTile(Vector3Int.zero, tile);
            }
            ToolManager.SetActiveTool<PaintTool>();
            SceneView.lastActiveSceneView?.Focus();
        }

        private static void SizeField(SerializedProperty prop, string label, int max)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                prop.intValue = Mathf.Clamp(EditorGUILayout.IntField(label, prop.intValue), Level.MinSize, max);
                GUILayout.Label($"= {prop.intValue * Level.SketchPixelsPerUnit:0} px", GUILayout.Width(80));
            }
        }
    }
}
