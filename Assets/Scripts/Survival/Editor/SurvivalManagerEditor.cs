using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Survival.EditorTools
{
    /// <summary>
    /// Inspector del <see cref="SurvivalManager"/>: lo de siempre y, debajo, los puntos de aparición. Botones «+ Volador /
    /// + Suelo / + Torreta / + Jefe» crean un punto hijo (en «Puntos») en el centro de la vista de escena y lo dejan
    /// seleccionado para moverlo, como el «+ Nuevo» del WaveManager. Debajo, todos los puntos por tipo (clic = seleccionar).
    /// </summary>
    [CustomEditor(typeof(SurvivalManager))]
    public class SurvivalManagerEditor : Editor
    {
        private const string PointsParent = "Puntos";
        private static readonly SurvivalSpawnKind[] Kinds =
            { SurvivalSpawnKind.Flying, SurvivalSpawnKind.Ground, SurvivalSpawnKind.Turret, SurvivalSpawnKind.Boss };

        private readonly List<SurvivalSpawnPoint> _points = new List<SurvivalSpawnPoint>();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var manager = (SurvivalManager)target;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Puntos de aparición", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var kind in Kinds)
                {
                    var old = GUI.backgroundColor;
                    GUI.backgroundColor = SurvivalSpawnPoint.ColorOf(kind);
                    if (GUILayout.Button("+ " + SurvivalSpawnPoint.LabelOf(kind))) Create(manager, kind);
                    GUI.backgroundColor = old;
                }
            }

            manager.GetComponentsInChildren(true, _points);
            foreach (var kind in Kinds)
            {
                int count = 0;
                foreach (var p in _points) if (p.kind == kind) count++;
                EditorGUILayout.LabelField($"{SurvivalSpawnPoint.LabelOf(kind)} ({count})", EditorStyles.miniBoldLabel);
                if (count == 0)
                {
                    EditorGUILayout.HelpBox(kind == SurvivalSpawnKind.Boss || kind == SurvivalSpawnKind.Turret
                        ? "Ninguno: salen en uno de Suelo."
                        : "Ninguno: los enemigos de este tipo no salen.", MessageType.None);
                    continue;
                }
                foreach (var p in _points)
                {
                    if (p.kind != kind) continue;
                    Vector3 pos = p.transform.position;
                    string text = $"{p.name}   ({pos.x:0.#}, {pos.y:0.#}){(p.gameObject.activeInHierarchy ? "" : "   · apagado")}";
                    if (GUILayout.Button(text, EditorStyles.miniButton))
                    {
                        Selection.activeGameObject = p.gameObject;
                        EditorGUIUtility.PingObject(p.gameObject);
                    }
                }
            }
        }

        private static void Create(SurvivalManager manager, SurvivalSpawnKind kind)
        {
            var parent = manager.transform.Find(PointsParent);
            if (parent == null)
            {
                var folder = new GameObject(PointsParent);
                Undo.RegisterCreatedObjectUndo(folder, "Crear carpeta de puntos");
                Undo.SetTransformParent(folder.transform, manager.transform, "Crear carpeta de puntos");
                folder.transform.localPosition = Vector3.zero;
                parent = folder.transform;
            }

            int n = 1;
            foreach (var p in manager.GetComponentsInChildren<SurvivalSpawnPoint>(true)) if (p.kind == kind) n++;

            var go = new GameObject($"{SurvivalSpawnPoint.LabelOf(kind)}_{n}");
            Undo.RegisterCreatedObjectUndo(go, "Crear punto de aparición");
            Undo.SetTransformParent(go.transform, parent, "Crear punto de aparición");
            var view = SceneView.lastActiveSceneView;
            Vector3 at = view != null ? view.pivot : manager.transform.position;
            go.transform.position = new Vector3(Mathf.Round(at.x * 2f) / 2f, Mathf.Round(at.y * 2f) / 2f, 0f);
            go.AddComponent<SurvivalSpawnPoint>().kind = kind;

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
    }
}
