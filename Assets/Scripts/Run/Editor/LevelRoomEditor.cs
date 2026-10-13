using RedMagic.Levels;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace RedMagic.Run.EditorTools
{
    /// <summary>
    /// Asas de la <see cref="LevelRoom"/> en la escena: se arrastran los lados y la sala se ajusta a celdas enteras de la
    /// cuadrícula del <see cref="Level"/>.
    /// </summary>
    [CustomEditor(typeof(LevelRoom))]
    public class LevelRoomEditor : Editor
    {
        private readonly BoxBoundsHandle _handle = new BoxBoundsHandle { axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var room = (LevelRoom)target;
            EditorGUILayout.HelpBox($"Puertas de la sala (tocan su borde): {room.Doors.Count}. " +
                                    "Arrastra los lados en la escena para ajustarla a la sala.", MessageType.None);
        }

        private void OnSceneGUI()
        {
            var room = (LevelRoom)target;
            var area = room.Area;
            _handle.center = area.center;
            _handle.size = new Vector3(area.width, area.height, 0f);
            _handle.SetColor(new Color(1f, 0.6f, 0.15f));

            EditorGUI.BeginChangeCheck();
            _handle.DrawHandle();
            if (!EditorGUI.EndChangeCheck()) return;

            // celdas enteras, contadas desde el origen del nivel
            var level = room.GetComponentInParent<Level>();
            if (level == null) level = Object.FindFirstObjectByType<Level>();
            Vector2 origin = level != null ? (Vector2)level.transform.position : Vector2.zero;
            Vector2 min = (Vector2)(_handle.center - _handle.size * 0.5f) - origin;
            Vector2 max = (Vector2)(_handle.center + _handle.size * 0.5f) - origin;
            var cellMin = new Vector2Int(Mathf.RoundToInt(min.x), Mathf.RoundToInt(min.y));
            var cellMax = new Vector2Int(Mathf.RoundToInt(max.x), Mathf.RoundToInt(max.y));

            Undo.RecordObjects(new Object[] { room, room.transform }, "Ajustar sala");
            room.SetArea(origin + cellMin, cellMax - cellMin);
        }
    }
}
