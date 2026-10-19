using System.Collections.Generic;
using System.IO;
using RedMagic.Arsenal.Editor;
using RedMagic.Combat;
using RedMagic.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Test.Editor
{
    /// <summary>
    /// <b>Tools ▸ RedMagic ▸ Test ▸ Escena de armas (arsenal)</b>: genera <c>Assets/Test/Scenes/ArsenalTest.unity</c>, el
    /// escenario de <c>docs/bocetos/habilidades/jugable.html</c> a escala (1 px de la web = 0,025 u): suelo de 50 u, cuatro
    /// plataformas de un solo sentido y los mismos diez muñecos (<see cref="ArsenalTestTarget"/>: suelo, andarín y volador,
    /// 120 de vida, reaparecen). El jugador nace con Abanico (activa) y Singularidad (reserva); el menú DEV (F10) cambia
    /// todo. Mismo montaje que la escena del cometa: managers, UI y cámara movidos desde Ice2, en aditivo. Sobrescribe.
    /// </summary>
    public static class ArsenalTestSceneBuilder
    {
        private const string SourceScene = "Assets/Legacy/Scenes/Ice2.unity";
        private const string OutputScene = "Assets/Test/Scenes/ArsenalTest.unity";
        private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
        private const string SquareSprite = "Assets/Art/Placeholder/Square.png";
        private const string DummySprite = "Assets/Test/Arsenal/Dummy.png";
        private const int GroundLayer = 6;
        private const float P = 0.025f;                       // u por px de la web

        private static readonly HashSet<string> Keep = new HashSet<string>
        {
            "GameStateManager", "RunManager", "CurrencyManager", "PauseMenuUI", "OptionsMenuUI",
            "TouchControlsUI", "Global Light 2D", "Main Camera", "HubEntry",
        };

        // los de jugable.html (game.js ▸ SPAWNS / PLATFORMS), x en px; y de los voladores en px desde arriba (suelo = 340)
        private static readonly (float x, ArsenalTestTarget.Kind kind, float y)[] Spawns =
        {
            (470, ArsenalTestTarget.Kind.Ground, 0), (640, ArsenalTestTarget.Kind.Ground, 0), (900, ArsenalTestTarget.Kind.Walker, 0),
            (700, ArsenalTestTarget.Kind.Flyer, 150), (1100, ArsenalTestTarget.Kind.Flyer, 120), (1260, ArsenalTestTarget.Kind.Ground, 0),
            (1330, ArsenalTestTarget.Kind.Ground, 0), (1420, ArsenalTestTarget.Kind.Ground, 0), (1640, ArsenalTestTarget.Kind.Flyer, 110),
            (1780, ArsenalTestTarget.Kind.Walker, 0),
        };
        private static readonly (float x0, float x1, float y)[] Platforms = { (520, 660, 252), (760, 900, 190), (1180, 1340, 236), (1560, 1700, 200) };
        private const float GroundPx = 340f;

        private static Vector2 Spawn => new Vector2(200 * P, 1.2f);

        [MenuItem("Tools/RedMagic/Test/Escena de armas (arsenal)")]
        public static void Build()
        {
            var weapons = ArsenalSetup.Run();
            if (weapons == null || weapons.Count == 0) { Debug.LogError("[ArsenalTest] No hay armas del arsenal."); return; }
            var dummy = EnsureDummySprite();
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
            BuildArena(square);
            BuildTargets(dummy);

            var kit = new GameObject("ArsenalTestKit").AddComponent<ArsenalTestKit>();
            var so = new SerializedObject(kit);
            var list = so.FindProperty("weapons");
            list.arraySize = weapons.Count;
            for (int i = 0; i < weapons.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            so.FindProperty("slot1").objectReferenceValue = weapons[0];
            so.FindProperty("slot2").objectReferenceValue = weapons.Count > 1 ? weapons[1] : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("TestAutoHeal").AddComponent<TestAutoHeal>();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputScene));
            EditorSceneManager.SaveScene(scene, OutputScene);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[ArsenalTest] Escena generada en {OutputScene}.");
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.Trim() == rootName) return root;
            Debug.LogWarning($"[ArsenalTest] {SourceScene} no tiene '{rootName}'.");
            return null;
        }

        // ------------------------------------------------------------------ escenario

        private static void BuildArena(Sprite square)
        {
            var arena = new GameObject("Arena").transform;
            var ground = new Color(0.17f, 0.24f, 0.32f);
            float w = 2000 * P;
            Block(arena, square, "Floor", new Vector2(w * 0.5f, -0.5f), new Vector2(w + 4f, 1f), ground, GroundLayer);
            Block(arena, square, "Wall_Left", new Vector2(-1.5f, 8f), new Vector2(1f, 18f), ground, GroundLayer);
            Block(arena, square, "Wall_Right", new Vector2(w + 1.5f, 8f), new Vector2(1f, 18f), ground, GroundLayer);

            int platformLayer = LayerMask.NameToLayer("Platform");
            foreach (var (x0, x1, y) in Platforms)
            {
                var go = MakeSprite(square, $"Platform_{x0}", arena, new Vector2((x0 + x1) * 0.5f * P, (GroundPx - y) * P - 0.1f), new Vector2((x1 - x0) * P, 0.2f), new Color(0.3f, 0.47f, 0.6f));
                if (platformLayer >= 0) go.layer = platformLayer;
                var box = go.AddComponent<BoxCollider2D>();
                box.usedByEffector = true;
                var eff = go.AddComponent<PlatformEffector2D>();
                eff.useOneWay = true;
                eff.surfaceArc = 160f;
            }

            // la cámara saca sus límites del objeto "BG"
            var bg = MakeSprite(square, "BG", null, new Vector2(w * 0.5f, 7f), new Vector2(w + 12f, 20f), new Color(0.09f, 0.1f, 0.16f));
            bg.GetComponent<SpriteRenderer>().sortingOrder = -100;
        }

        private static void Block(Transform parent, Sprite square, string name, Vector2 center, Vector2 size, Color color, int layer)
        {
            var go = MakeSprite(square, name, parent, center, size, color);
            go.layer = layer;
            go.AddComponent<BoxCollider2D>();
        }

        private static GameObject MakeSprite(Sprite sprite, string name, Transform parent, Vector2 center, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 1;
            return go;
        }

        // ------------------------------------------------------------------ muñecos

        private static void BuildTargets(Sprite dummy)
        {
            var parent = new GameObject("Targets").transform;
            int i = 0;
            foreach (var (x, kind, y) in Spawns)
            {
                bool fly = kind == ArsenalTestTarget.Kind.Flyer;
                float rx = (fly ? 22 : 27) * P, ry = (fly ? 28 : 39) * P;
                float cy = fly ? (GroundPx - y) * P : ry;
                var go = new GameObject($"Target_{i++}_{kind}");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector2(x * P, cy);

                var body = new GameObject("Body");
                body.transform.SetParent(go.transform, false);
                body.transform.localScale = new Vector3(rx * 2f, ry * 2f / 1.5f, 1f);   // el sprite mide 1 × 1,5 u
                var sr = body.AddComponent<SpriteRenderer>();
                sr.sprite = dummy;
                sr.color = new Color(0.42f, 0.23f, 0.36f);
                sr.sortingLayerName = "Characters";
                sr.sortingOrder = 8;
                var eye = new GameObject("Eye");
                eye.transform.SetParent(go.transform, false);
                eye.transform.localPosition = new Vector3(rx * 0.35f, ry * 0.3f, 0f);
                eye.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
                var er = eye.AddComponent<SpriteRenderer>();
                er.sprite = dummy;
                er.color = new Color(1f, 0.83f, 0.3f);
                er.sortingLayerName = "Characters";
                er.sortingOrder = 9;

                var col = go.AddComponent<CapsuleCollider2D>();
                col.size = new Vector2(rx * 2f, ry * 2f);
                var rb = go.AddComponent<Rigidbody2D>();
                rb.freezeRotation = true;
                rb.gravityScale = fly ? 0f : 3f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                var health = go.AddComponent<Health>();
                var hso = new SerializedObject(health);
                hso.FindProperty("maxHealth").floatValue = 120f;
                hso.FindProperty("currentHealth").floatValue = 120f;
                hso.FindProperty("invulnerabilityDuration").floatValue = 0f;
                hso.ApplyModifiedPropertiesWithoutUndo();
                var kb = go.AddComponent<Knockback>();
                var kso = new SerializedObject(kb);
                kso.FindProperty("horizontalForce").floatValue = 6f;
                kso.FindProperty("verticalForce").floatValue = 2f;
                kso.FindProperty("duration").floatValue = 0.16f;
                kso.ApplyModifiedPropertiesWithoutUndo();
                go.AddComponent<HitFlash>();

                var t = go.AddComponent<ArsenalTestTarget>();
                var tso = new SerializedObject(t);
                tso.FindProperty("kind").enumValueIndex = (int)kind;
                tso.FindProperty("walkRange").floatValue = 100 * P;
                tso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Elipse blanca de 64×96 (tamaños múltiplos de 4) para los muñecos; se tiñe con el color del SpriteRenderer.</summary>
        private static Sprite EnsureDummySprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DummySprite);
            if (sprite != null) return sprite;
            const int w = 64, h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f), dy = (y + 0.5f - h * 0.5f) / (h * 0.5f), d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * 24f);
                    float shade = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01((dx + 0.2f) * 0.8f));
                    tex.SetPixel(x, y, new Color(shade, shade, shade, a));
                }
            Directory.CreateDirectory(Path.GetDirectoryName(DummySprite));
            File.WriteAllBytes(DummySprite, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(DummySprite);
            var imp = (TextureImporter)AssetImporter.GetAtPath(DummySprite);
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = 64;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(DummySprite);
        }
    }
}
