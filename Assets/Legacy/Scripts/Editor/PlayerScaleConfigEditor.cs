using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Run.EditorTools
{
    /// <summary>
    /// Dibuja <see cref="PlayerScaleConfig"/> como una lista compacta escena → escala, con un
    /// botón que la rellena con todo lo que haya en Build Settings.
    /// </summary>
    [CustomEditor(typeof(PlayerScaleConfig))]
    public class PlayerScaleConfigEditor : Editor
    {
        private SerializedProperty _entries;

        private void OnEnable() => _entries = serializedObject.FindProperty("entries");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Escala del jugador por escena. 1.0 es el tamaño con el que está modelado el " +
                "personaje; ajusta cada fila probando esa escena en Play.", MessageType.None);

            if (GUILayout.Button("Sincronizar con Build Settings"))
                SyncFromBuildSettings();

            EditorGUILayout.Space();
            DrawColumnHeader();

            for (int i = 0; i < _entries.arraySize; i++)
            {
                if (DrawRow(i)) { i--; continue; } // se borró esta fila: no saltarse la siguiente
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("+ Fila manual"))
                AppendBlankRow();

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawColumnHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Escena", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Escala", EditorStyles.miniBoldLabel, GUILayout.Width(70));
            GUILayout.Space(24); // hueco del botón de borrar de cada fila
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Dibuja una fila. Devuelve true si el usuario la borró.</summary>
        private bool DrawRow(int index)
        {
            var entry = _entries.GetArrayElementAtIndex(index);
            var sceneAssetProp = entry.FindPropertyRelative("scene").FindPropertyRelative("sceneAsset");
            var scaleProp = entry.FindPropertyRelative("scale");

            bool missing = sceneAssetProp.objectReferenceValue == null;

            EditorGUILayout.BeginHorizontal();

            if (missing) GUI.backgroundColor = new Color(1f, 0.65f, 0.55f);
            EditorGUILayout.PropertyField(sceneAssetProp, GUIContent.none);
            GUI.backgroundColor = Color.white;

            scaleProp.floatValue = Mathf.Max(0.01f,
                EditorGUILayout.FloatField(scaleProp.floatValue, GUILayout.Width(70)));

            bool deleted = GUILayout.Button("×", GUILayout.Width(22));
            EditorGUILayout.EndHorizontal();

            if (deleted) _entries.DeleteArrayElementAtIndex(index);
            return deleted;
        }

        private void AppendBlankRow()
        {
            int newIndex = _entries.arraySize;
            _entries.InsertArrayElementAtIndex(newIndex);

            // InsertArrayElementAtIndex duplica la última fila en vez de dejarla en blanco cuando
            // la lista no está vacía: se pisan sus dos campos a mano para no heredar por sorpresa
            // la escena de la fila anterior.
            var newEntry = _entries.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("scene").FindPropertyRelative("sceneAsset").objectReferenceValue = null;
            newEntry.FindPropertyRelative("scale").floatValue = PlayerScaleConfig.DefaultScale;
        }

        /// <summary>
        /// Añade una fila por cada escena habilitada en Build Settings que todavía no esté en la
        /// lista, a escala 1.0. Idempotente: nunca toca ni borra una fila existente, así que
        /// relanzarlo tras crear una escena nueva no desajusta las que ya se probaron.
        /// </summary>
        private void SyncFromBuildSettings()
        {
            var existingPaths = new HashSet<string>();

            for (int i = 0; i < _entries.arraySize; i++)
            {
                var sceneAssetProp = _entries.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("scene").FindPropertyRelative("sceneAsset");

                if (sceneAssetProp.objectReferenceValue != null)
                    existingPaths.Add(AssetDatabase.GetAssetPath(sceneAssetProp.objectReferenceValue));
            }

            int added = 0;

            foreach (var buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled || existingPaths.Contains(buildScene.path)) continue;

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path);
                if (sceneAsset == null) continue;

                int newIndex = _entries.arraySize;
                _entries.InsertArrayElementAtIndex(newIndex);

                var newEntry = _entries.GetArrayElementAtIndex(newIndex);
                newEntry.FindPropertyRelative("scene").FindPropertyRelative("sceneAsset").objectReferenceValue = sceneAsset;
                newEntry.FindPropertyRelative("scale").floatValue = PlayerScaleConfig.DefaultScale;

                existingPaths.Add(buildScene.path);
                added++;
            }

            serializedObject.ApplyModifiedProperties();

            // Fuerza a SceneReference a hornear scenePath/sceneName ya mismo (OnBeforeSerialize
            // corre al guardar el asset), en vez de dejarlo pendiente hasta el próximo guardado.
            AssetDatabase.SaveAssets();

            Debug.Log(added == 0
                ? "[PlayerScaleConfig] Nada que añadir: todas las escenas de Build Settings ya están en la lista."
                : $"[PlayerScaleConfig] {added} escena(s) añadida(s) a escala {PlayerScaleConfig.DefaultScale}.");
        }

        [MenuItem("Tools/RedMagic/Jugador/Sincronizar escalas con Build Settings", priority = 310)]
        private static void SyncFromMenu()
        {
            var config = FindOrCreateConfig();
            var editor = (PlayerScaleConfigEditor)CreateEditor(config);
            editor.OnEnable();
            editor.SyncFromBuildSettings();
            DestroyImmediate(editor);

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        private static PlayerScaleConfig FindOrCreateConfig()
        {
            const string path = "Assets/Resources/PlayerScaleConfig.asset";

            var config = AssetDatabase.LoadAssetAtPath<PlayerScaleConfig>(path);
            if (config != null) return config;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            config = ScriptableObject.CreateInstance<PlayerScaleConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PlayerScaleConfig] Creado en '{path}'.");
            return config;
        }
    }
}
