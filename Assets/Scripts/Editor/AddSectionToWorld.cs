using System.IO;
using RedMagic.Run;
using UnityEditor;
using UnityEngine;

namespace RedMagic.RunEditor
{
    /// <summary>
    /// Añade UNA escena ya existente (sección o jefe) a un <see cref="WorldDefinition"/> ya
    /// existente, sin tocar el resto del pool.
    ///
    /// Existe porque <see cref="WorldSceneGenerator"/> es destructivo con el pool: cada vez que se
    /// ejecuta reescribe <c>sectionPool</c> entero a partir de la convención de nombres
    /// World{n}_Section{i:00}, así que no sirve para meter una escena creada a mano (o una escena
    /// de jefe de repuesto) sin regenerar —y por tanto arriesgar— todo lo demás que ya había en el
    /// mundo. Esta herramienta sólo añade: toma una escena y un WorldDefinition ya montados y
    /// escribe la referencia, nada más. No crea escenas ni contenido placeholder.
    ///
    /// Menú: <b>Tools > RedMagic > Add Section To World</b>.
    /// </summary>
    public class AddSectionToWorld : EditorWindow
    {
        private WorldDefinition _world;
        private SceneAsset _scene;
        private bool _isBossScene;

        private string _resultMessage = string.Empty;
        private MessageType _resultType = MessageType.None;

        [MenuItem("Tools/RedMagic/Add Section To World")]
        private static void Open()
        {
            var window = GetWindow<AddSectionToWorld>(true, "Add Section To World");
            window.minSize = new Vector2(380f, 220f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Añade una escena existente a un WorldDefinition existente, sin tocar el resto del " +
                "pool. Para generar un mundo entero de golpe usa World Scene Generator; esto es sólo " +
                "para meter una escena suelta (hecha a mano, o de jefe) en un mundo ya montado.",
                MessageType.Info);

            EditorGUILayout.Space();
            _world = (WorldDefinition)EditorGUILayout.ObjectField(
                new GUIContent("World Definition", "El asset al que se añade la escena."),
                _world, typeof(WorldDefinition), allowSceneObjects: false);

            _scene = (SceneAsset)EditorGUILayout.ObjectField(
                new GUIContent("Escena", "La escena a añadir (sección o jefe)."),
                _scene, typeof(SceneAsset), allowSceneObjects: false);

            _isBossScene = EditorGUILayout.Toggle(
                new GUIContent("Es escena de jefe",
                               "Si está marcado, se escribe en el campo bossScene del mundo en vez " +
                               "de añadirse al sectionPool."),
                _isBossScene);

            EditorGUILayout.Space();

            bool canAdd = _world != null && _scene != null;
            using (new EditorGUI.DisabledScope(!canAdd))
            {
                if (GUILayout.Button("Add", GUILayout.Height(28f)))
                    Add();
            }

            if (!string.IsNullOrEmpty(_resultMessage))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_resultMessage, _resultType);
            }
        }

        private void Add()
        {
            string scenePath = AssetDatabase.GetAssetPath(_scene);

            AddToBuildSettings(scenePath);

            var so = new SerializedObject(_world);

            if (_isBossScene)
            {
                AssignScene(so.FindProperty("bossScene"), scenePath);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_world);
                AssetDatabase.SaveAssets();

                _resultMessage = $"'{_scene.name}' asignada como escena de jefe de '{_world.name}'.";
                _resultType = MessageType.Info;
                Debug.Log($"[AddSectionToWorld] {_resultMessage}", _world);
                return;
            }

            var pool = so.FindProperty("sectionPool");

            // No añadir la misma escena dos veces: se compara por scenePath, que es el mismo
            // valor horneado que AssignScene escribe, así que detecta un duplicado aunque la
            // escena esté cerrada o el sceneAsset de un elemento antiguo se haya perdido.
            for (int i = 0; i < pool.arraySize; i++)
            {
                var existingPath = pool.GetArrayElementAtIndex(i).FindPropertyRelative("scenePath");
                if (existingPath != null && existingPath.stringValue == scenePath)
                {
                    _resultMessage = $"'{_scene.name}' ya está en el pool de '{_world.name}'; no se ha añadido de nuevo.";
                    _resultType = MessageType.Warning;
                    Debug.LogWarning($"[AddSectionToWorld] {_resultMessage}", _world);
                    return;
                }
            }

            int newIndex = pool.arraySize;
            pool.arraySize = newIndex + 1;
            AssignScene(pool.GetArrayElementAtIndex(newIndex), scenePath);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_world);
            AssetDatabase.SaveAssets();

            _resultMessage = $"'{_scene.name}' añadida al pool de '{_world.name}' (posición {newIndex}).";
            _resultType = MessageType.Info;
            Debug.Log($"[AddSectionToWorld] {_resultMessage}", _world);
        }

        // ------------------------------------------------------------------ escenas

        /// <summary>Mismo patrón que WorldSceneGenerator.AddToBuildSettings: añade si falta, sin duplicar.</summary>
        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (var entry in scenes)
                if (entry.path == scenePath) return;

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Rellena una SceneReference: el asset (que es el enlace por GUID que sobrevive a
        /// renombrados) y además los valores horneados, que son los que existen en un build. Mismo
        /// patrón que WorldSceneGenerator.AssignScene.
        /// </summary>
        private static void AssignScene(SerializedProperty sceneReference, string scenePath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (asset == null)
            {
                Debug.LogError($"[AddSectionToWorld] No se encontró la escena '{scenePath}'.");
                return;
            }

            var assetProp = sceneReference.FindPropertyRelative("sceneAsset");
            if (assetProp != null) assetProp.objectReferenceValue = asset;

            sceneReference.FindPropertyRelative("scenePath").stringValue = scenePath;
            sceneReference.FindPropertyRelative("sceneName").stringValue =
                Path.GetFileNameWithoutExtension(scenePath);
        }
    }
}
