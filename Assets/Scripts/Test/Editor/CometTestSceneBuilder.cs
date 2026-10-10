using System.Collections.Generic;
using System.IO;
using RedMagic.Items.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Test.Editor
{
    /// <summary>
    /// <b>Tools ▸ RedMagic ▸ Test ▸ Escena del cometa</b>: genera <c>Assets/Scenes/Test/CometTest.unity</c> para
    /// probar el arma Cometa. Una pista larga con tres zonas separadas (marcadas en el suelo con su color):
    /// <list type="number">
    /// <item><b>Perforación</b> (azul): cuatro muñecos en fila a la altura del jugador.</item>
    /// <item><b>Cadena</b> (morado): un grupo de seis, unos en el suelo y otros flotando.</item>
    /// <item><b>Homing</b> (ámbar): cuatro muñecos que se mueven rápido (<see cref="TestMover"/>).</item>
    /// </list>
    /// Los muñecos son <c>Target.prefab</c> (<c>TrainingDummy</c>: no mueren y enseñan el daño). El jugador nace
    /// con el Cometa equipado (<see cref="CometTestKit"/>) y se cura solo (<see cref="TestAutoHeal"/>). Mismo
    /// montaje que la escena de parry: managers, UI y cámara movidos desde Ice2 (que se cierra sin guardar),
    /// todo en aditivo. Se puede regenerar: sobrescribe la escena.
    /// </summary>
    public static class CometTestSceneBuilder
    {
        private const string SourceScene = "Assets/Scenes/Ice2.unity";
        private const string OutputScene = "Assets/Scenes/Test/CometTest.unity";
        private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
        private const string TargetPrefab = "Assets/Prefabs/Eviroment/Target.prefab";
        private const string SquareSprite = "Assets/Art/Placeholder/Square.png";
        private const int GroundLayer = 6;

        private static readonly HashSet<string> Keep = new HashSet<string>
        {
            "GameStateManager", "RunManager", "CurrencyManager", "PauseMenuUI", "OptionsMenuUI",
            "TouchControlsUI", "Global Light 2D", "Main Camera", "HubEntry",
        };

        private static readonly Vector2 Spawn = new Vector2(0f, 1.2f);

        [MenuItem("Tools/RedMagic/Test/Escena del cometa")]
        public static void Build()
        {
            var weapon = CometWeaponSetup.Run();
            var previousActive = SceneManager.GetActiveScene();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            foreach (var root in source.GetRootGameObjects())
                if (Keep.Contains(root.name.Trim())) SceneManager.MoveGameObjectToScene(root, scene);
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(scene);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), scene);
            player.transform.position = Spawn;
            var entry = FindRoot(scene, "HubEntry");
            if (entry != null) entry.transform.position = Spawn;
            var cam = FindRoot(scene, "Main Camera");
            if (cam != null) cam.transform.position = new Vector3(Spawn.x, Spawn.y + 2f, -10f);

            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSprite);
            BuildTrack(square);
            BuildTargets(scene);

            var kit = new GameObject("CometTestKit").AddComponent<CometTestKit>();
            var so = new SerializedObject(kit);
            so.FindProperty("weapon").objectReferenceValue = weapon;
            so.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("TestAutoHeal").AddComponent<TestAutoHeal>();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputScene));
            EditorSceneManager.SaveScene(scene, OutputScene);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[CometTest] Escena generada en {OutputScene}.");
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.Trim() == rootName) return root;
            Debug.LogWarning($"[CometTest] {SourceScene} no tiene '{rootName}'.");
            return null;
        }

        // ------------------------------------------------------------------ pista

        private static void BuildTrack(Sprite square)
        {
            var track = new GameObject("Track").transform;
            var ground = new Color(0.22f, 0.24f, 0.3f);
            Block(track, square, "Floor", new Vector2(40f, -0.5f), new Vector2(112f, 1f), ground);
            Block(track, square, "Wall_Left", new Vector2(-16.5f, 9f), new Vector2(1f, 20f), ground);
            Block(track, square, "Wall_Right", new Vector2(96.5f, 9f), new Vector2(1f, 20f), ground);

            // marcas de zona en el suelo
            Marker(track, square, "Zone_Pierce", 12f, 22f, new Color(0.16f, 0.5f, 1f));
            Marker(track, square, "Zone_Chain", 33f, 47f, new Color(0.55f, 0.3f, 0.95f));
            Marker(track, square, "Zone_Homing", 59f, 79f, new Color(1f, 0.65f, 0.2f));

            // la cámara saca sus límites del objeto "BG"
            var bg = MakeSprite(square, "BG", null, new Vector2(40f, 9f), new Vector2(116f, 22f), new Color(0.07f, 0.06f, 0.1f));
            bg.GetComponent<SpriteRenderer>().sortingOrder = -100;
        }

        private static void Marker(Transform parent, Sprite square, string name, float x0, float x1, Color color)
        {
            var go = MakeSprite(square, name, parent, new Vector2((x0 + x1) * 0.5f, -0.06f), new Vector2(x1 - x0, 0.12f), color);
            go.GetComponent<SpriteRenderer>().sortingOrder = 2;
        }

        private static void Block(Transform parent, Sprite square, string name, Vector2 center, Vector2 size, Color color)
        {
            var go = MakeSprite(square, name, parent, center, size, color);
            go.layer = GroundLayer;
            go.AddComponent<BoxCollider2D>();
        }

        private static GameObject MakeSprite(Sprite square, string name, Transform parent, Vector2 center, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = 1;
            return go;
        }

        // ------------------------------------------------------------------ muñecos

        private static void BuildTargets(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[CometTest] No existe {TargetPrefab}: la escena queda sin blancos.");
                return;
            }

            var pierce = new GameObject("Pierce_Line").transform;
            foreach (float x in new[] { 14f, 15.8f, 17.6f, 19.4f }) Target(scene, prefab, pierce, x, 0f);

            var chain = new GameObject("Chain_Group").transform;
            Target(scene, prefab, chain, 36f, 0f);
            Target(scene, prefab, chain, 38.5f, 0f);
            Target(scene, prefab, chain, 40.5f, 2.6f);
            Target(scene, prefab, chain, 42.5f, 0f);
            Target(scene, prefab, chain, 39f, 4.8f);
            Target(scene, prefab, chain, 44.5f, 2f);

            var homing = new GameObject("Homing_Movers").transform;
            Mover(Target(scene, prefab, homing, 64f, 0f), TestMover.Path.Horizontal, 3.5f, 0.55f, 0f);
            Mover(Target(scene, prefab, homing, 70f, 4.5f), TestMover.Path.Circle, 2.5f, 0.45f, 0.3f);
            Mover(Target(scene, prefab, homing, 75f, 3f), TestMover.Path.Eight, 3f, 0.5f, 0.6f);
            Mover(Target(scene, prefab, homing, 67f, 7f), TestMover.Path.Vertical, 2.5f, 0.6f, 0.15f);
        }

        /// <summary>Muñeco con la base de su sprite a <paramref name="lift"/> unidades sobre el suelo (y = 0).</summary>
        private static GameObject Target(Scene scene, GameObject prefab, Transform parent, float x, float lift)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(parent, true);
            go.transform.position = new Vector3(x, 0f, 0f);
            var renderer = go.GetComponentInChildren<SpriteRenderer>();
            float bottom = renderer != null ? renderer.bounds.min.y : 0f;
            go.transform.position = new Vector3(x, lift - bottom, 0f);
            return go;
        }

        private static void Mover(GameObject go, TestMover.Path path, float amplitude, float frequency, float phase)
        {
            var mover = go.AddComponent<TestMover>();
            var so = new SerializedObject(mover);
            so.FindProperty("path").enumValueIndex = (int)path;
            so.FindProperty("amplitude").floatValue = amplitude;
            so.FindProperty("frequency").floatValue = frequency;
            so.FindProperty("phase").floatValue = phase;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
