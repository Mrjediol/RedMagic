using System.Collections.Generic;
using System.IO;
using RedMagic.Gameplay;
using RedMagic.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RedMagic.RunEditor
{
    /// <summary>
    /// Genera de golpe las escenas de un mundo (secciones + jefe), las mete en Build Settings y
    /// crea el <see cref="WorldDefinition"/> con todas las referencias ya puestas.
    ///
    /// Existe por dos motivos. Uno práctico: 5 mundos × 16 escenas son 80 escenas que nadie quiere
    /// crear a mano. Y uno de fondo: cablear el mundo a mano invita a teclear los nombres
    /// ("World1_Section01"…) en una lista de strings, que es exactamente el acoplamiento por nombre
    /// que rompió el botón de Jugar al renombrar SampleScene. Aquí las referencias se rellenan
    /// como assets, así que a partir de ese momento renombrar o mover una sección es seguro.
    ///
    /// Es idempotente: vuelve a pasarlo y sólo crea lo que falte. Nunca sobrescribe una escena que
    /// ya exista, así que se puede ejecutar de nuevo tras haber montado niveles de verdad.
    ///
    /// El <c>Base Section Prefab</c> (bajo "Opciones") es opcional y es un campo por mundo: su
    /// valor en el momento de generar es el que se usa para ESE mundo, así que el Mundo 2 puede
    /// usar un prefab base distinto simplemente cambiándolo antes de generarlo. Si se asigna, se
    /// instancia tal cual en cada escena nueva del mundo —secciones y jefe— y se da por hecho que
    /// ya trae el terreno, el <see cref="SectionEntry"/> y el <see cref="SectionExit"/>; el
    /// generador sólo añade cámara y luz global. En el jefe se le quita cualquier SectionExit tras
    /// instanciarlo, porque el jefe avanza al morir, no al cruzar una salida. Si se deja vacío,
    /// cada escena se monta con un placeholder de marcador propio (terreno + entrada + salida).
    ///
    /// Menú: <b>Tools > RedMagic > World Scene Generator</b>.
    /// </summary>
    public class WorldSceneGenerator : EditorWindow
    {
        private int _worldNumber = 1;
        private int _sectionCount = 15;
        private int _sectionsPerRun = 5;
        private bool _createBossScene = true;
        private string _sceneRoot = "Assets/Scenes/Worlds";
        private string _dataRoot = "Assets/Data/Worlds";
        private bool _addToBuildSettings = true;
        private bool _createWorldAsset = true;
        private bool _unlocked = true;
        private bool _buildPlaceholderContent = true;
        private GameObject _baseSectionPrefab;

        private Vector2 _scroll;

        [MenuItem("Tools/RedMagic/World Scene Generator")]
        private static void Open()
        {
            var window = GetWindow<WorldSceneGenerator>(true, "World Scene Generator");
            window.minSize = new Vector2(420f, 460f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "Crea las escenas de un mundo, las añade a Build Settings y genera el " +
                "WorldDefinition con las referencias puestas.\n\n" +
                "Es seguro ejecutarlo varias veces: nunca sobrescribe una escena existente.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mundo", EditorStyles.boldLabel);
            _worldNumber = Mathf.Max(1, EditorGUILayout.IntField("Número de mundo", _worldNumber));
            _sectionCount = Mathf.Max(1, EditorGUILayout.IntField("Secciones en el pool", _sectionCount));
            _sectionsPerRun = Mathf.Clamp(
                EditorGUILayout.IntField("Secciones por run", _sectionsPerRun), 1, _sectionCount);
            _createBossScene = EditorGUILayout.Toggle("Crear escena de jefe", _createBossScene);
            _unlocked = EditorGUILayout.Toggle("Mundo desbloqueado", _unlocked);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Salida", EditorStyles.boldLabel);
            _sceneRoot = EditorGUILayout.TextField("Carpeta de escenas", _sceneRoot);
            _dataRoot = EditorGUILayout.TextField("Carpeta del asset", _dataRoot);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Opciones", EditorStyles.boldLabel);
            _addToBuildSettings = EditorGUILayout.Toggle("Añadir a Build Settings", _addToBuildSettings);
            _createWorldAsset = EditorGUILayout.Toggle("Crear WorldDefinition", _createWorldAsset);
            _buildPlaceholderContent = EditorGUILayout.Toggle(
                new GUIContent("Contenido placeholder",
                               "Añade cámara y luz global a cada escena nueva (más terreno, entrada " +
                               "y salida de marcador si no hay Base Section Prefab) para que sea " +
                               "jugable desde el primer momento."),
                _buildPlaceholderContent);

            _baseSectionPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Base Section Prefab",
                               "Opcional. Si se asigna, se instancia en cada escena nueva de ESTE " +
                               "mundo (secciones y jefe) y se asume que ya trae terreno, SectionEntry " +
                               "y SectionExit; el generador sólo añade cámara y luz. En el jefe se le " +
                               "quita el SectionExit. Es un campo por mundo: cámbialo antes de generar " +
                               "cada mundo si cada uno usa un prefab base distinto."),
                _baseSectionPrefab, typeof(GameObject), allowSceneObjects: false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"Se generará: World{_worldNumber}_Section01 … World{_worldNumber}_Section{_sectionCount:00}" +
                (_createBossScene ? $" + World{_worldNumber}_Boss" : "") +
                (_baseSectionPrefab != null
                    ? $"\nEscenas a partir de '{_baseSectionPrefab.name}'."
                    : ""),
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();
            if (GUILayout.Button($"Generar mundo {_worldNumber}", GUILayout.Height(32f)))
                Generate();

            EditorGUILayout.EndScrollView();
        }

        private void Generate()
        {
            string worldFolder = $"{_sceneRoot}/World{_worldNumber}";
            EnsureFolder(worldFolder);

            var activeScenePath = EditorSceneManager.GetActiveScene().path;

            // Guardar lo abierto antes de ponerse a crear escenas: si el usuario dice que no, se
            // aborta en vez de arriesgar su trabajo sin guardar.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[WorldSceneGenerator] Cancelado por el usuario.");
                return;
            }

            var sectionPaths = new List<string>();
            int created = 0;

            try
            {
                for (int i = 1; i <= _sectionCount; i++)
                {
                    string path = $"{worldFolder}/World{_worldNumber}_Section{i:00}.unity";
                    sectionPaths.Add(path);

                    EditorUtility.DisplayProgressBar("Generando mundo",
                                                     $"Sección {i} de {_sectionCount}",
                                                     (float)i / _sectionCount);

                    if (CreateSceneIfMissing(path, isBoss: false)) created++;
                }

                if (_createBossScene)
                {
                    string bossPath = $"{worldFolder}/World{_worldNumber}_Boss.unity";
                    if (CreateSceneIfMissing(bossPath, isBoss: true)) created++;

                    if (_addToBuildSettings) AddToBuildSettings(bossPath);
                }

                if (_addToBuildSettings)
                    foreach (var path in sectionPaths)
                        AddToBuildSettings(path);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (_createWorldAsset)
                CreateOrUpdateWorldAsset(worldFolder, sectionPaths);

            // Devolver al usuario a donde estaba.
            if (!string.IsNullOrEmpty(activeScenePath))
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

            Debug.Log($"[WorldSceneGenerator] Mundo {_worldNumber} listo: {created} escenas nuevas, " +
                      $"{sectionPaths.Count} secciones en total.");
        }

        // ------------------------------------------------------------------ escenas

        /// <summary>Crea la escena si no existe. Devuelve true si la ha creado.</summary>
        private bool CreateSceneIfMissing(string path, bool isBoss)
        {
            if (File.Exists(path)) return false;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            if (_buildPlaceholderContent)
                BuildPlaceholderContent(scene, isBoss, _baseSectionPrefab);

            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, removeScene: true);
            return true;
        }

        /// <summary>
        /// Monta el mínimo para que la escena se pueda jugar nada más generarla: cámara que sigue
        /// al jugador y luz global (sin ella los sprites Lit de URP 2D salen en negro).
        ///
        /// Si hay <paramref name="baseSectionPrefab"/>, se instancia y se da por hecho que ya trae
        /// el terreno, el <see cref="SectionEntry"/> y el <see cref="SectionExit"/>; en el jefe se
        /// le quita el SectionExit después, porque el jefe avanza al morir. Sin prefab, se genera
        /// un placeholder de marcador: una caja de terreno, un SectionEntry y —salvo en el jefe—
        /// un SectionExit, en coordenadas fijas.
        /// </summary>
        private static void BuildPlaceholderContent(Scene scene, bool isBoss, GameObject baseSectionPrefab)
        {
            var camera = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(camera, scene);
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camera.AddComponent<Camera>();
            cam.orthographic = true;
            // En una run, RunManager fuerza su propio 'cameraOrthographicSize' en todas las
            // cámaras con CameraFollow; esto es sólo para que la escena se vea bien suelta.
            cam.orthographicSize = 6.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.08f, 0.12f, 1f);
            camera.AddComponent<CameraFollow>();

            var light = new GameObject("Global Light 2D");
            SceneManager.MoveGameObjectToScene(light, scene);
            var light2D = light.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.intensity = 1f;

            if (baseSectionPrefab != null)
            {
                var instance = InstantiateBaseSectionPrefab(baseSectionPrefab, scene);

                // El jefe no lleva salida: avanza al morir, no al cruzar un trigger. Si el prefab
                // base trae un SectionExit, se quita de la instancia del jefe para que no se pueda
                // saltar el combate cruzándola.
                if (isBoss && instance != null)
                    foreach (var prefabExit in instance.GetComponentsInChildren<SectionExit>(true))
                        Object.DestroyImmediate(prefabExit.gameObject);

                return;
            }

            // Sin prefab base: placeholder de marcador (terreno + entrada + salida).
            var ground = new GameObject("Ground (placeholder)");
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(10f, -4f, 0f);
            // En la capa "Ground" para que el sondeo de bordes de EnemyController lo reconozca
            // como suelo (y no lo confunda con jugador/enemigos, que van en Default).
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) ground.layer = groundLayer;
            var groundCollider = ground.AddComponent<BoxCollider2D>();
            groundCollider.size = new Vector2(60f, 2f);

            var placeholderEntry = new GameObject("SectionEntry");
            SceneManager.MoveGameObjectToScene(placeholderEntry, scene);
            placeholderEntry.transform.position = new Vector3(-8f, -2f, 0f);
            placeholderEntry.AddComponent<SectionEntry>();

            if (isBoss) return;

            var exit = new GameObject("SectionExit");
            SceneManager.MoveGameObjectToScene(exit, scene);
            exit.transform.position = new Vector3(26f, -2f, 0f);
            var exitCollider = exit.AddComponent<BoxCollider2D>();
            exitCollider.isTrigger = true;
            exitCollider.size = new Vector2(2f, 6f);
            exit.AddComponent<SectionExit>();
        }

        /// <summary>
        /// Instancia el Base Section Prefab en la escena. Usa
        /// <see cref="PrefabUtility.InstantiatePrefab(Object, Scene)"/> y no
        /// <see cref="Object.Instantiate(Object)"/> para que la instancia se quede conectada al
        /// prefab: un cambio en el prefab base se puede volver a aplicar después a todas las
        /// escenas ya generadas con "Apply" en vez de rehacerlas una a una.
        /// </summary>
        private static GameObject InstantiateBaseSectionPrefab(GameObject prefab, Scene scene)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                Debug.LogError($"[WorldSceneGenerator] No se pudo instanciar el Base Section Prefab " +
                               $"'{prefab.name}' en '{scene.path}'.", prefab);
            return instance;
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (var entry in scenes)
                if (entry.path == scenePath) return;

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ asset del mundo

        private void CreateOrUpdateWorldAsset(string worldFolder, List<string> sectionPaths)
        {
            EnsureFolder(_dataRoot);

            string assetPath = $"{_dataRoot}/World{_worldNumber}.asset";
            var world = AssetDatabase.LoadAssetAtPath<WorldDefinition>(assetPath);

            if (world == null)
            {
                world = CreateInstance<WorldDefinition>();
                AssetDatabase.CreateAsset(world, assetPath);
            }

            var so = new SerializedObject(world);
            so.FindProperty("worldNumber").intValue = _worldNumber;
            so.FindProperty("displayName").stringValue = $"Mundo {_worldNumber}";
            so.FindProperty("unlocked").boolValue = _unlocked;
            so.FindProperty("sectionsPerRun").intValue = _sectionsPerRun;

            var pool = so.FindProperty("sectionPool");
            pool.arraySize = sectionPaths.Count;
            for (int i = 0; i < sectionPaths.Count; i++)
                AssignScene(pool.GetArrayElementAtIndex(i), sectionPaths[i]);

            if (_createBossScene)
                AssignScene(so.FindProperty("bossScene"), $"{worldFolder}/World{_worldNumber}_Boss.unity");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);
            AssetDatabase.SaveAssets();

            Debug.Log($"[WorldSceneGenerator] WorldDefinition en {assetPath}", world);
            Selection.activeObject = world;
        }

        /// <summary>
        /// Rellena una SceneReference: el asset (que es el enlace por GUID que sobrevive a
        /// renombrados) y además los valores horneados, que son los que existen en un build.
        /// </summary>
        private static void AssignScene(SerializedProperty sceneReference, string scenePath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (asset == null)
            {
                Debug.LogError($"[WorldSceneGenerator] No se encontró la escena '{scenePath}'.");
                return;
            }

            var assetProp = sceneReference.FindPropertyRelative("sceneAsset");
            if (assetProp != null) assetProp.objectReferenceValue = asset;

            sceneReference.FindPropertyRelative("scenePath").stringValue = scenePath;
            sceneReference.FindPropertyRelative("sceneName").stringValue =
                Path.GetFileNameWithoutExtension(scenePath);
        }

        // ------------------------------------------------------------------ utilidades

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            string current = parts[0];              // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
