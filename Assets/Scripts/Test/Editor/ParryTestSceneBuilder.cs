using System.Collections.Generic;
using System.IO;
using RedMagic.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Test.Editor
{
    /// <summary>
    /// <b>Tools ▸ RedMagic ▸ Test ▸ Escena de parry</b>: genera <c>Assets/Scenes/Test/ParryTest.unity</c>.
    ///
    /// Los singletons de escena, la UI y la cámara se <b>mueven</b> desde una sección real
    /// (<see cref="SourceScene"/>, que se cierra sin guardar), así llegan con todas sus referencias
    /// igual que en el juego; el jugador es una instancia de su prefab. El resto se construye aquí: una arena cerrada, torretas de
    /// proyectiles en los lados y arriba (<see cref="ProjectileTestEmitter"/>), un enemigo melé quieto
    /// (<see cref="MeleeEnemy"/> como Static + Melee) y autocuración del jugador. Las torretas y el
    /// enemigo llevan <see cref="TrainingDummy"/>: no mueren y enseñan el daño devuelto.
    ///
    /// Se puede volver a generar cuando cambie algo: sobrescribe la escena.
    /// </summary>
    public static class ParryTestSceneBuilder
    {
        private const string SourceScene = "Assets/Scenes/Ice2.unity";
        private const string OutputScene = "Assets/Scenes/Test/ParryTest.unity";
        private const string MeleeEnemy = "Assets/Prefabs/Enemies/Procedural/TestGolem.prefab";
        private const string SquareSprite = "Assets/Art/Placeholder/Square.png";
        private const int GroundLayer = 6;

        /// <summary>Raíces de la sección que se traen tal cual.</summary>
        private static readonly HashSet<string> Keep = new HashSet<string>
        {
            "GameStateManager", "RunManager", "CurrencyManager", "PauseMenuUI", "OptionsMenuUI",
            "TouchControlsUI", "Global Light 2D", "Main Camera", "HubEntry",
        };

        private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";

        private static readonly Vector2 Spawn = new Vector2(0f, 1.2f);

        /// <summary>
        /// Todo en aditivo: las escenas que tengas abiertas (aunque tengan cambios sin guardar) no se
        /// cierran ni se tocan. La escena nueva se guarda y se cierra; ábrela tú para probar.
        /// </summary>
        [MenuItem("Tools/RedMagic/Test/Escena de parry")]
        public static void Build()
        {
            // La escena se ha convertido a mano en el minijuego de parry: regenerarla lo borraría.
            if (File.Exists(OutputScene) &&
                !EditorUtility.DisplayDialog("Escena de parry",
                    $"{OutputScene} ya existe (ahora es el minijuego de parry, editado a mano). " +
                    "Regenerarla la sobrescribe entera. ¿Seguro?", "Sobrescribir", "Cancelar"))
                return;

            var previousActive = SceneManager.GetActiveScene();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            foreach (var root in source.GetRootGameObjects())
                if (Keep.Contains(root.name.Trim())) SceneManager.MoveGameObjectToScene(root, scene);
            EditorSceneManager.CloseScene(source, true);   // sin guardar: la sección queda intacta
            SceneManager.SetActiveScene(scene);             // lo que se cree a partir de aquí va a la nueva

            // Una sección suelta no recibe el jugador del RunManager (sólo lo crea al pasar por el hub
            // o al empezar una run): se pone una instancia del prefab, que así siempre está al día.
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), scene);
            player.transform.position = Spawn;
            var entry = FindRoot(scene, "HubEntry");
            if (entry != null) entry.transform.position = Spawn;
            var cam = FindRoot(scene, "Main Camera");
            if (cam != null) cam.transform.position = new Vector3(Spawn.x, Spawn.y + 2f, -10f);

            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSprite);
            BuildArena(square);
            BuildEmitters(square);
            BuildMeleeEnemy(scene);

            new GameObject("TestAutoHeal").AddComponent<TestAutoHeal>();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputScene));
            EditorSceneManager.SaveScene(scene, OutputScene);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[ParryTest] Escena generada en {OutputScene}.");
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.Trim() == rootName) return root;
            Debug.LogWarning($"[ParryTest] {SourceScene} no tiene '{rootName}'.");
            return null;
        }

        // ------------------------------------------------------------------ arena

        private static void BuildArena(Sprite square)
        {
            var arena = new GameObject("Arena").transform;
            var ground = new Color(0.22f, 0.24f, 0.3f);

            Block(arena, square, "Floor", new Vector2(0f, -0.5f), new Vector2(36f, 1f), ground);
            Block(arena, square, "Wall_Left", new Vector2(-18.5f, 7f), new Vector2(1f, 16f), ground);
            Block(arena, square, "Wall_Right", new Vector2(18.5f, 7f), new Vector2(1f, 16f), ground);
            Block(arena, square, "Platform_Left", new Vector2(-8f, 3.5f), new Vector2(4f, 0.4f), ground);
            Block(arena, square, "Platform_Right", new Vector2(-2f, 6.5f), new Vector2(3f, 0.4f), ground);
            // Alto a propósito: pensado para llegar sólo con salto + doble salto + parry en el aire.
            Block(arena, square, "Ledge_ParryJump", new Vector2(-12f, 11f), new Vector2(3f, 0.4f), ground);

            // Fondo: la cámara saca sus límites del objeto "BG".
            var bg = MakeSprite(square, "BG", null, new Vector2(0f, 7f), new Vector2(38f, 18f), new Color(0.07f, 0.06f, 0.1f));
            bg.GetComponent<SpriteRenderer>().sortingOrder = -100;
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

        // ------------------------------------------------------------------ torretas

        private static void BuildEmitters(Sprite square)
        {
            var parent = new GameObject("Emitters").transform;

            // Lados a la altura del jugador, diagonales altas y una vertical: llegan desde todas partes.
            Emitter(parent, square, "Emitter_Left", new Vector2(-16f, 1.3f), 3f, 0.5f);
            Emitter(parent, square, "Emitter_Right", new Vector2(16f, 1.3f), 3f, 2f);
            Emitter(parent, square, "Emitter_UpLeft", new Vector2(-14f, 13f), 3.5f, 1.2f);
            Emitter(parent, square, "Emitter_UpRight", new Vector2(14f, 13f), 3.5f, 2.7f);
            Emitter(parent, square, "Emitter_Top", new Vector2(0f, 14.5f), 4f, 3.4f);
        }

        private static void Emitter(Transform parent, Sprite square, string name, Vector2 position, float interval, float delay)
        {
            var go = MakeSprite(square, name, parent, position, new Vector2(0.8f, 0.8f), new Color(1f, 0.55f, 0.15f));
            go.AddComponent<BoxCollider2D>().isTrigger = true;   // golpeable, pero no estorba

            // Vida enorme como el muñeco del hub: un daño devuelto ×10 no lo mata antes de que se resetee.
            var health = go.AddComponent<Health>();
            SetFloat(health, "invulnerabilityDuration", 0f);
            SetFloat(health, "maxHealth", 1000000f);
            SetFloat(health, "currentHealth", 1000000f);
            go.AddComponent<TrainingDummy>();

            var emitter = go.AddComponent<ProjectileTestEmitter>();
            SetFloat(emitter, "interval", interval);
            SetFloat(emitter, "startDelay", delay);
        }

        // ------------------------------------------------------------------ enemigo melé

        private static void BuildMeleeEnemy(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeleeEnemy);
            if (prefab == null)
            {
                Debug.LogWarning($"[ParryTest] No existe {MeleeEnemy}: la escena queda sin enemigo melé.");
                return;
            }

            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            enemy.name = "MeleeEnemy_Static";
            enemy.transform.position = new Vector2(5f, 1.5f);

            // Quieto: Static que golpea a quien se le pone delante.
            var stats = enemy.GetComponent<Enemies.EnemyStats>();
            var so = new SerializedObject(stats);
            SetEnum(so.FindProperty("tuning.archetype"), "Static");
            SetEnum(so.FindProperty("tuning.staticAttack"), "Melee");
            so.FindProperty("tuning.maxHealth").floatValue = 1000000f;   // no muere con el daño devuelto
            so.ApplyModifiedPropertiesWithoutUndo();

            if (enemy.GetComponent<TrainingDummy>() == null) enemy.AddComponent<TrainingDummy>();
        }

        // ------------------------------------------------------------------ utilidades

        private static void SetFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null) { Debug.LogWarning($"[ParryTest] {target.GetType().Name} no tiene '{property}'."); return; }
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(SerializedProperty prop, string name)
        {
            if (prop == null) { Debug.LogWarning($"[ParryTest] Campo de enum no encontrado para '{name}'."); return; }
            int index = System.Array.IndexOf(prop.enumNames, name);
            if (index >= 0) prop.enumValueIndex = index;
            else Debug.LogWarning($"[ParryTest] '{prop.propertyPath}' no tiene el valor '{name}'.");
        }
    }
}
