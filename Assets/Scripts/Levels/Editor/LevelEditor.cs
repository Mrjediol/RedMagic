using RedMagic.Levels;
using UnityEditor;
using UnityEngine;

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
                if (GUILayout.Button("Regenerar")) level.Rebuild();
                if (GUILayout.Button("Abrir paleta de terreno"))
                {
                    EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
                    if (level.Terrain != null) Selection.activeGameObject = level.Terrain.gameObject;
                }
            }
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
