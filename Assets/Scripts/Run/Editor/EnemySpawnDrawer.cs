using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Run.EditorTools
{
    /// <summary>
    /// Una línea por enemigo: <c>#n [prefab] [punto de spawn ▾] [retardo s]</c>. El punto se elige
    /// de la lista del <see cref="WaveManager"/> por nombre, pero se guarda la referencia
    /// (renombrar el objeto no rompe nada). La última opción crea un punto nuevo en el centro de la sala de la oleada
    /// (hijo de la sala, añadido a la lista).
    /// </summary>
    [CustomPropertyDrawer(typeof(EnemySpawn))]
    public class EnemySpawnDrawer : PropertyDrawer
    {
        private static readonly Color Missing = new(1f, 0.45f, 0.4f);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            var prefab = property.FindPropertyRelative(nameof(EnemySpawn.enemyPrefab));
            var point = property.FindPropertyRelative(nameof(EnemySpawn.spawnPoint));
            var delay = property.FindPropertyRelative(nameof(EnemySpawn.spawnDelay));

            EditorGUI.BeginProperty(rect, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            const float numW = 24f, delayW = 58f, gap = 4f;
            float rest = rect.width - numW - delayW - gap * 3;
            var numR = new Rect(rect.x, rect.y, numW, rect.height);
            var prefabR = new Rect(numR.xMax + gap, rect.y, rest * 0.55f, rect.height);
            var pointR = new Rect(prefabR.xMax + gap, rect.y, rest * 0.45f, rect.height);
            var delayR = new Rect(pointR.xMax + gap, rect.y, delayW, rect.height);

            EditorGUI.LabelField(numR, "#" + (IndexOf(property) + 1), EditorStyles.miniLabel);

            // Un enemigo arrastrado desde la jerarquía se cambia por su prefab: la instancia de la
            // escena suele estar apagada o desaparecer, el asset no.
            var asset = ToPrefabAsset(prefab.objectReferenceValue as GameObject);
            if (asset != null && asset != prefab.objectReferenceValue) prefab.objectReferenceValue = asset;

            var old = GUI.color;
            if (prefab.objectReferenceValue == null) GUI.color = Missing;
            EditorGUI.PropertyField(prefabR, prefab, GUIContent.none);
            GUI.color = point.objectReferenceValue == null ? Missing : old;
            DrawPointPicker(pointR, point, property);
            GUI.color = old;

            float labelW = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 14f;
            delay.floatValue = Mathf.Max(0f, EditorGUI.FloatField(delayR,
                new GUIContent("⏱", "Segundos desde el inicio de la oleada"), delay.floatValue));
            EditorGUIUtility.labelWidth = labelW;

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private static void DrawPointPicker(Rect rect, SerializedProperty point, SerializedProperty entry)
        {
            var manager = entry.serializedObject.targetObject as WaveManager;
            var points = new List<Transform>();
            if (manager != null)
                foreach (var p in manager.SpawnPoints)
                    if (p != null && !points.Contains(p)) points.Add(p);

            // Con una referencia de fuera de la lista: campo de objeto normal.
            var current = point.objectReferenceValue as Transform;
            if (manager == null || (current != null && !points.Contains(current)))
            {
                EditorGUI.PropertyField(rect, point, GUIContent.none);
                return;
            }

            var room = RoomOf(entry);
            var names = new GUIContent[points.Count + 2];
            names[0] = new GUIContent("— punto de spawn —");
            for (int i = 0; i < points.Count; i++) names[i + 1] = new GUIContent(points[i].name);
            names[points.Count + 1] = new GUIContent(room != null ? $"+ Nuevo en el centro de {room.name}" : "+ Nuevo (centro de la vista)");

            int sel = current == null ? 0 : points.IndexOf(current) + 1;
            int next = EditorGUI.Popup(rect, sel, names);
            if (next == sel) return;
            if (next == points.Count + 1) point.objectReferenceValue = CreatePoint(entry.serializedObject, manager, room);
            else point.objectReferenceValue = next == 0 ? null : points[next - 1];
        }

        /// <summary>La sala de la oleada a la que pertenece esta entrada (waves.Array.data[i].enemies.Array.data[j]).</summary>
        private static LevelRoom RoomOf(SerializedProperty entry)
        {
            string path = entry.propertyPath;
            int cut = path.IndexOf(".enemies.Array", System.StringComparison.Ordinal);
            if (cut < 0) return null;
            var wave = entry.serializedObject.FindProperty(path.Substring(0, cut));
            return wave?.FindPropertyRelative(nameof(WaveDefinition.room))?.objectReferenceValue as LevelRoom;
        }

        /// <summary>Punto nuevo en el centro de la sala (hijo de ella) o de la vista, añadido a la lista del manager.</summary>
        private static Transform CreatePoint(SerializedObject so, WaveManager manager, LevelRoom room)
        {
            var list = so.FindProperty("spawnPoints");
            var go = new GameObject($"SpawnPoint_{list.arraySize + 1}");
            Undo.RegisterCreatedObjectUndo(go, "Nuevo punto de spawn");
            go.transform.SetParent(room != null ? room.transform : manager.transform, false);
            Vector3 at = room != null ? (Vector3)room.Center
                       : SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : manager.transform.position;
            go.transform.position = new Vector3(at.x, at.y, 0f);

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = go.transform;
            return go.transform;
        }

        /// <summary>Prefab asset de una instancia de escena; null si ya es asset o no viene de prefab.</summary>
        private static GameObject ToPrefabAsset(GameObject go)
        {
            if (go == null || EditorUtility.IsPersistent(go)) return null;
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
            return root != null ? PrefabUtility.GetCorrespondingObjectFromSource(root) : null;
        }

        private static int IndexOf(SerializedProperty property)
        {
            string path = property.propertyPath;
            int open = path.LastIndexOf('[');
            int close = path.LastIndexOf(']');
            return open >= 0 && close > open && int.TryParse(path.Substring(open + 1, close - open - 1), out int i)
                ? i
                : 0;
        }
    }
}
