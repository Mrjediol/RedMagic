using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Run.EditorTools
{
    /// <summary>
    /// Inspector de <see cref="WaveManager"/>: puntos de spawn y salas con botón para crear uno, y cada
    /// oleada plegable con su resumen ("Oleada 2 · 3 enemigos: Ogro ×2, Lobo"), cuándo empieza, una línea por
    /// enemigo (<see cref="EnemySpawnDrawer"/>) y qué pasa al terminar. También añade GameObject ▸ RedMagic ▸ Wave Manager.
    /// </summary>
    [CustomEditor(typeof(WaveManager))]
    public class WaveManagerEditor : Editor
    {
        private SerializedProperty _spawnPoints, _waves, _startOnLoad;
        private readonly HashSet<int> _folded = new();

        private void OnEnable()
        {
            _spawnPoints = serializedObject.FindProperty("spawnPoints");
            _waves = serializedObject.FindProperty("waves");
            _startOnLoad = serializedObject.FindProperty("startOnLoad");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var manager = (WaveManager)target;

            EditorGUILayout.PropertyField(_startOnLoad);
            if (Application.isPlaying)
                EditorGUILayout.HelpBox(manager.Finished ? "Nivel superado."
                    : manager.Running ? $"Última oleada empezada: {manager.CurrentWave + 1}/{_waves.arraySize} · vivos: {manager.AliveCount}"
                    : "Sin empezar.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_spawnPoints, new GUIContent("Puntos de spawn"), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Punto de spawn")) AddSpawnPoint(manager);
                if (GUILayout.Button("Recoger hijos")) CollectChildren(manager);
                if (GUILayout.Button("+ Sala")) AddRoom(manager);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Oleadas", EditorStyles.boldLabel);

            int remove = -1, moveUp = -1, duplicate = -1;
            for (int i = 0; i < _waves.arraySize; i++)
            {
                var wave = _waves.GetArrayElementAtIndex(i);
                var enemies = wave.FindPropertyRelative(nameof(WaveDefinition.enemies));

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var swatch = GUILayoutUtility.GetRect(6, EditorGUIUtility.singleLineHeight, GUILayout.Width(6));
                        EditorGUI.DrawRect(swatch, WaveManager.WaveColor(i));

                        bool open = !_folded.Contains(i);
                        bool now = EditorGUILayout.Foldout(open, Summary(i, enemies), true, EditorStyles.foldoutHeader);
                        if (now != open) { if (now) _folded.Remove(i); else _folded.Add(i); }

                        using (new EditorGUI.DisabledScope(i == 0))
                            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(22))) moveUp = i;
                        if (GUILayout.Button("⧉", EditorStyles.miniButtonMid, GUILayout.Width(22))) duplicate = i;
                        if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22))) remove = i;
                    }

                    if (_folded.Contains(i)) continue;

                    var start = wave.FindPropertyRelative(nameof(WaveDefinition.start));
                    var room = wave.FindPropertyRelative(nameof(WaveDefinition.room));
                    EditorGUILayout.PropertyField(start, new GUIContent("Empieza"));
                    EditorGUILayout.PropertyField(room, new GUIContent("Sala"));
                    if (start.enumValueIndex == (int)WaveStart.OnRoomEnter && room.objectReferenceValue == null)
                        EditorGUILayout.HelpBox("Empieza al entrar en su sala, pero no tiene sala: no saldrá nunca.", MessageType.Warning);
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative(nameof(WaveDefinition.startDelay)),
                        new GUIContent("Espera antes"));

                    for (int e = 0; e < enemies.arraySize; e++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(enemies.GetArrayElementAtIndex(e), GUIContent.none);
                            if (GUILayout.Button("−", EditorStyles.miniButton, GUILayout.Width(20)))
                            {
                                enemies.DeleteArrayElementAtIndex(e);
                                break;
                            }
                        }
                    }

                    if (GUILayout.Button("+ Enemigo", EditorStyles.miniButton)) AddEnemy(enemies, manager);
                    DrawOnFinish(wave, i);
                }
            }

            if (remove >= 0) _waves.DeleteArrayElementAtIndex(remove);
            else if (moveUp > 0) _waves.MoveArrayElement(moveUp, moveUp - 1);
            else if (duplicate >= 0) _waves.GetArrayElementAtIndex(duplicate).DuplicateCommand();

            if (GUILayout.Button("+ Oleada", GUILayout.Height(24))) AddWave();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>"Al terminar": lanzar oleada (por defecto la siguiente), terminar sala (+ item) y terminar nivel.</summary>
        private void DrawOnFinish(SerializedProperty wave, int index)
        {
            EditorGUILayout.LabelField("Al terminar", EditorStyles.miniBoldLabel);

            var launch = wave.FindPropertyRelative(nameof(WaveDefinition.launchWave));
            var target = wave.FindPropertyRelative(nameof(WaveDefinition.launchIndex));
            using (new EditorGUILayout.HorizontalScope())
            {
                launch.boolValue = EditorGUILayout.ToggleLeft("Lanzar oleada", launch.boolValue, GUILayout.Width(110));
                using (new EditorGUI.DisabledScope(!launch.boolValue))
                {
                    var names = new GUIContent[_waves.arraySize + 1];
                    names[0] = new GUIContent(index + 1 < _waves.arraySize ? $"La siguiente ({index + 2})" : "La siguiente (no hay)");
                    for (int w = 0; w < _waves.arraySize; w++) names[w + 1] = new GUIContent($"Oleada {w + 1}");
                    int sel = Mathf.Clamp(target.intValue + 1, 0, _waves.arraySize);
                    target.intValue = EditorGUILayout.Popup(sel, names) - 1;
                }
            }

            var finishRoom = wave.FindPropertyRelative(nameof(WaveDefinition.finishRoom));
            using (new EditorGUILayout.HorizontalScope())
            {
                finishRoom.boolValue = EditorGUILayout.ToggleLeft(new GUIContent("Terminar sala",
                    "Abre las puertas de su sala."), finishRoom.boolValue, GUILayout.Width(110));
                using (new EditorGUI.DisabledScope(!finishRoom.boolValue))
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative(nameof(WaveDefinition.dropItem)),
                        new GUIContent("Item", "Cae en el centro de la sala. Vacío = ninguno."));
            }
            if (finishRoom.boolValue && wave.FindPropertyRelative(nameof(WaveDefinition.room)).objectReferenceValue == null)
                EditorGUILayout.HelpBox("Terminar sala sin sala: no abre nada.", MessageType.Warning);

            var finishLevel = wave.FindPropertyRelative(nameof(WaveDefinition.finishLevel));
            finishLevel.boolValue = EditorGUILayout.ToggleLeft(new GUIContent("Terminar nivel",
                "El nivel queda superado y se puede salir, aunque queden salas opcionales."), finishLevel.boolValue);
        }

        /// <summary>
        /// Oleada nueva: la anterior pasa a lanzarla al terminar y le cede 'terminar nivel' y 'terminar sala' (la sala
        /// sigue hasta su última oleada); la nueva es de la misma sala y espera a que la lancen.
        /// </summary>
        private void AddWave()
        {
            int prev = _waves.arraySize - 1;
            _waves.arraySize++;
            var wave = _waves.GetArrayElementAtIndex(_waves.arraySize - 1);
            wave.FindPropertyRelative(nameof(WaveDefinition.startDelay)).floatValue = 0.5f;
            wave.FindPropertyRelative(nameof(WaveDefinition.enemies)).arraySize = 0;
            wave.FindPropertyRelative(nameof(WaveDefinition.launchWave)).boolValue = false;
            wave.FindPropertyRelative(nameof(WaveDefinition.launchIndex)).intValue = -1;
            wave.FindPropertyRelative(nameof(WaveDefinition.dropItem)).objectReferenceValue = null;

            if (prev < 0)
            {
                wave.FindPropertyRelative(nameof(WaveDefinition.start)).enumValueIndex = (int)WaveStart.OnLoad;
                wave.FindPropertyRelative(nameof(WaveDefinition.room)).objectReferenceValue = null;
                wave.FindPropertyRelative(nameof(WaveDefinition.finishRoom)).boolValue = false;
                wave.FindPropertyRelative(nameof(WaveDefinition.finishLevel)).boolValue = true;
                return;
            }

            var before = _waves.GetArrayElementAtIndex(prev);
            wave.FindPropertyRelative(nameof(WaveDefinition.start)).enumValueIndex = (int)WaveStart.Launched;
            wave.FindPropertyRelative(nameof(WaveDefinition.room)).objectReferenceValue =
                before.FindPropertyRelative(nameof(WaveDefinition.room)).objectReferenceValue;
            foreach (var flag in new[] { nameof(WaveDefinition.finishRoom), nameof(WaveDefinition.finishLevel) })
            {
                var from = before.FindPropertyRelative(flag);
                wave.FindPropertyRelative(flag).boolValue = from.boolValue;
                from.boolValue = false;
            }
            before.FindPropertyRelative(nameof(WaveDefinition.launchWave)).boolValue = true;
            before.FindPropertyRelative(nameof(WaveDefinition.launchIndex)).intValue = -1;
        }

        private void AddRoom(WaveManager manager)
        {
            var go = new GameObject($"Sala_{manager.GetComponentsInChildren<LevelRoom>(true).Length + 1}");
            Undo.RegisterCreatedObjectUndo(go, "Añadir sala");
            go.transform.SetParent(manager.transform, false);
            var pivot = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : manager.transform.position;
            go.transform.position = new Vector3(Mathf.Round(pivot.x - 6f), Mathf.Round(pivot.y - 3f), 0f);
            go.AddComponent<LevelRoom>();
            Selection.activeGameObject = go;
        }

        private static string Summary(int index, SerializedProperty enemies)
        {
            var counts = new Dictionary<string, int>();
            for (int e = 0; e < enemies.arraySize; e++)
            {
                var prefab = enemies.GetArrayElementAtIndex(e)
                    .FindPropertyRelative(nameof(EnemySpawn.enemyPrefab)).objectReferenceValue;
                string n = prefab != null ? prefab.name.Replace("Enemy_", "") : "¿?";
                counts[n] = counts.TryGetValue(n, out int c) ? c + 1 : 1;
            }

            string list = string.Join(", ", counts.Select(kv => kv.Value > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key));
            return $"Oleada {index + 1} · {enemies.arraySize} enemigo{(enemies.arraySize == 1 ? "" : "s")}"
                   + (list.Length > 0 ? $": {list}" : "");
        }

        /// <summary>Nuevo enemigo copiando prefab y punto del anterior (lo normal es repetir).</summary>
        private static void AddEnemy(SerializedProperty enemies, WaveManager manager)
        {
            enemies.arraySize++;
            var entry = enemies.GetArrayElementAtIndex(enemies.arraySize - 1);
            if (enemies.arraySize > 1) return; // Unity ya copia el elemento anterior

            entry.FindPropertyRelative(nameof(EnemySpawn.enemyPrefab)).objectReferenceValue = null;
            entry.FindPropertyRelative(nameof(EnemySpawn.spawnPoint)).objectReferenceValue =
                manager.SpawnPoints.FirstOrDefault(p => p != null);
            entry.FindPropertyRelative(nameof(EnemySpawn.spawnDelay)).floatValue = 0f;
        }

        private void AddSpawnPoint(WaveManager manager)
        {
            var go = new GameObject($"SpawnPoint_{_spawnPoints.arraySize + 1}");
            Undo.RegisterCreatedObjectUndo(go, "Añadir punto de spawn");
            go.transform.SetParent(manager.transform, false);
            go.transform.localPosition = new Vector3(2f * (_spawnPoints.arraySize + 1), 0f, 0f);

            _spawnPoints.arraySize++;
            _spawnPoints.GetArrayElementAtIndex(_spawnPoints.arraySize - 1).objectReferenceValue = go.transform;
            Selection.activeGameObject = go;
        }

        private void CollectChildren(WaveManager manager)
        {
            var existing = new HashSet<Transform>(manager.SpawnPoints.Where(p => p != null));
            foreach (Transform child in manager.transform)
            {
                if (existing.Contains(child)) continue;
                _spawnPoints.arraySize++;
                _spawnPoints.GetArrayElementAtIndex(_spawnPoints.arraySize - 1).objectReferenceValue = child;
            }
        }

        [MenuItem("GameObject/RedMagic/Wave Manager", false, 10)]
        private static void Create(MenuCommand command)
        {
            var go = new GameObject("WaveManager");
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            if (command.context == null && SceneView.lastActiveSceneView != null)
            {
                var pivot = SceneView.lastActiveSceneView.pivot;
                go.transform.position = new Vector3(pivot.x, pivot.y, 0f);
            }

            var manager = go.AddComponent<WaveManager>();
            Undo.RegisterCreatedObjectUndo(go, "Crear Wave Manager");

            var so = new SerializedObject(manager);
            var points = so.FindProperty("spawnPoints");
            for (int i = 0; i < 2; i++)
            {
                var p = new GameObject($"SpawnPoint_{i + 1}");
                p.transform.SetParent(go.transform, false);
                p.transform.localPosition = new Vector3(i == 0 ? -4f : 4f, 0f, 0f);
                points.arraySize++;
                points.GetArrayElementAtIndex(i).objectReferenceValue = p.transform;
            }

            var waves = so.FindProperty("waves");
            waves.arraySize = 1;
            var first = waves.GetArrayElementAtIndex(0);
            first.FindPropertyRelative(nameof(WaveDefinition.startDelay)).floatValue = 0.5f;
            first.FindPropertyRelative(nameof(WaveDefinition.start)).enumValueIndex = (int)WaveStart.OnLoad;
            first.FindPropertyRelative(nameof(WaveDefinition.finishLevel)).boolValue = true;
            first.FindPropertyRelative(nameof(WaveDefinition.launchIndex)).intValue = -1;
            so.FindProperty("roomsMigrated").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = go;
        }
    }
}
