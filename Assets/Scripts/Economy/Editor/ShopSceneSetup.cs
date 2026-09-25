using System.Collections.Generic;
using System.IO;
using System.Linq;
using RedMagic.Gameplay;
using RedMagic.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RedMagic.Economy.EditorTools
{
    /// <summary>
    /// <b>Tools ▸ RedMagic ▸ Tienda ▸ Preparar escena Shop</b>: deja <c>Assets/Scenes/Shop.unity</c>
    /// jugable sobre su fondo (<c>Shop1.png</c>: puerta izquierda, 5 altares, puerta derecha) y la
    /// apunta en <c>ShopConfig.shopScene</c>. Coloca, sólo lo que falte:
    ///  - <c>BG</c> con el fondo (la cámara se limita a él por nombre).
    ///  - Suelo en la capa Ground y dos paredes en los bordes.
    ///  - <c>SectionEntry</c> en la puerta izquierda, <c>SectionExit</c> en la derecha.
    ///  - <c>ShopManager</c> con 5 puntos de spawn sobre los altares (luego se afinan a mano).
    ///  - <c>CameraFollow</c> en la cámara.
    /// Las posiciones salen de coordenadas normalizadas sobre el fondo, así que siguen valiendo si
    /// se cambia su escala. Idempotente: nunca mueve algo que ya exista.
    /// </summary>
    public static class ShopSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Shop.unity";
        private const string BackgroundPath = "Assets/Prefabs/Maps/maps/Shop1.png";

        // Medido sobre Shop1.png (0..1 desde abajo-izquierda): los 5 altares.
        private static readonly float[] AltarU = { 0.2765f, 0.3825f, 0.5f, 0.62f, 0.7225f };
        private const float ItemV = 0.34f;
        private const float FloorV = 0.12f;
        private const float LeftDoorU = 0.10f;
        private const float RightDoorU = 0.905f;

        [MenuItem("Tools/RedMagic/Tienda/Preparar escena Shop", priority = 420)]
        public static void Run()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var bg = EnsureBackground(scene);
            var b = bg.bounds;
            Vector3 At(float u, float v) => new(b.min.x + u * b.size.x, b.min.y + v * b.size.y, 0f);
            float floorY = At(0f, FloorV).y;

            EnsureGround(scene, b, floorY);
            EnsureEntry(scene, At(LeftDoorU, FloorV) + Vector3.up * 0.1f);
            EnsureExit(scene, At(RightDoorU, FloorV) + Vector3.up * 2.5f);
            EnsureShopManager(scene, AltarU.Select(u => At(u, ItemV)).ToList());
            EnsureCamera(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);

            AssignShopScene();
            AddToBuildSettings();

            Debug.Log($"[ShopSceneSetup] '{ScenePath}' lista y asignada en ShopConfig.");
        }

        private static SpriteRenderer EnsureBackground(Scene scene)
        {
            var go = Find(scene, "BG");
            if (go != null && go.GetComponentInChildren<SpriteRenderer>() is { } child && !go.TryGetComponent(out SpriteRenderer _)) return child;
            if (go != null && go.TryGetComponent(out SpriteRenderer existing)) return existing;

            go ??= New(scene, "BG");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAllAssetsAtPath(BackgroundPath).OfType<Sprite>().FirstOrDefault();
            renderer.sortingLayerName = "BackGround";
            renderer.sortingOrder = -10;
            return renderer;
        }

        private static void EnsureGround(Scene scene, Bounds b, float floorY)
        {
            // Ya hay suelo (p. ej. el de BG.prefab): no se añade otro.
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (Roots(scene).SelectMany(r => r.GetComponentsInChildren<Collider2D>(true)).Any(c => c.gameObject.layer == groundLayer)) return;

            var ground = New(scene, "Ground");
            ground.layer = groundLayer;

            // Suelo: de lado a lado, con la cara de arriba en floorY.
            var floor = ground.AddComponent<BoxCollider2D>();
            floor.size = new Vector2(b.size.x, 2f);
            floor.offset = new Vector2(b.center.x, floorY - 1f);

            // Paredes en los bordes del fondo para no salirse de la sala.
            foreach (float x in new[] { b.min.x - 0.5f, b.max.x + 0.5f })
            {
                var wall = ground.AddComponent<BoxCollider2D>();
                wall.size = new Vector2(1f, b.size.y);
                wall.offset = new Vector2(x, b.center.y);
            }
        }

        private static void EnsureEntry(Scene scene, Vector3 position)
        {
            if (Roots(scene).Any(r => r.GetComponentInChildren<SectionEntry>(true) != null)) return;
            New(scene, "SectionEntry (puerta izquierda)", position).AddComponent<SectionEntry>();
        }

        private static void EnsureExit(Scene scene, Vector3 position)
        {
            if (Roots(scene).Any(r => r.GetComponentInChildren<SectionExit>(true) != null)) return;

            var exit = New(scene, "SectionExit (puerta derecha)", position);
            var trigger = exit.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(2f, 5f);
            exit.AddComponent<SectionExit>();
        }

        private static void EnsureShopManager(Scene scene, List<Vector3> points)
        {
            if (Roots(scene).Any(r => r.GetComponentInChildren<ShopManager>(true) != null)) return;

            var go = New(scene, "ShopManager");
            var shop = go.AddComponent<ShopManager>();

            var so = new SerializedObject(shop);
            var list = so.FindProperty("itemSpawnPoints");
            for (int i = 0; i < points.Count; i++)
            {
                var point = new GameObject($"Altar_{i + 1}");
                point.transform.SetParent(go.transform, false);
                point.transform.position = points[i];
                list.arraySize++;
                list.GetArrayElementAtIndex(i).objectReferenceValue = point.transform;
            }

            var actions = AssetDatabase.FindAssets("t:InputActionAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(a => a != null && a.name == "RedMagicControls");
            so.FindProperty("inputActions").objectReferenceValue = actions;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureCamera(Scene scene)
        {
            var cam = Roots(scene).Select(r => r.GetComponentInChildren<Camera>(true)).FirstOrDefault(c => c != null);
            if (cam == null) return;
            cam.orthographic = true;
            if (cam.GetComponent<CameraFollow>() == null) cam.gameObject.AddComponent<CameraFollow>();
        }

        private static void AssignShopScene()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShopConfig>("Assets/Resources/ShopConfig.asset");
            if (config == null) return;

            var so = new SerializedObject(config);
            var reference = so.FindProperty("shopScene");
            reference.FindPropertyRelative("sceneAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            reference.FindPropertyRelative("scenePath").stringValue = ScenePath;
            reference.FindPropertyRelative("sceneName").stringValue = Path.GetFileNameWithoutExtension(ScenePath);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ utilidades

        private static IEnumerable<GameObject> Roots(Scene scene) => scene.GetRootGameObjects();

        private static GameObject Find(Scene scene, string name) => Roots(scene).FirstOrDefault(r => r.name == name);

        private static GameObject New(Scene scene, string name, Vector3 position = default)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = position;
            return go;
        }
    }
}
