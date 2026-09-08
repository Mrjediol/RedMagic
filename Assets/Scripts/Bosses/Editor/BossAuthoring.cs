using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Fontanería compartida por los generadores de jefes (<see cref="BossStarterPack"/>,
    /// <see cref="ScarecrowBossPack"/>): crear assets de ataque, escribir campos serializados
    /// privados, dar de alta etiquetas y colocar un prefab en la escena de su jefe.
    ///
    /// Vive aquí y no duplicado en cada pack porque un jefe nuevo debe ser <b>datos</b>: cuanto
    /// menos código propio tenga su generador, más difícil es que dos jefes acaben comportándose
    /// distinto por un detalle de fontanería en vez de por sus números.
    ///
    /// Todo lo de este archivo es <b>idempotente</b>: nunca pisa un asset existente, así que se
    /// puede volver a lanzar un pack después de haber afinado valores a mano.
    /// </summary>
    public static class BossAuthoring
    {
        /// <summary>
        /// Crea (o reutiliza) un asset de ataque. Si ya existe se devuelve tal cual y no se toca:
        /// los números afinados a mano mandan sobre los del generador.
        /// </summary>
        public static T Attack<T>(string folder, string fileName, string displayName, string description,
                                  Color accent, Func<Fields, Fields> configure) where T : BossAttack
        {
            string path = $"{folder}/{fileName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);

            var fields = new Fields(asset)
                .Set("displayName", displayName)
                .Set("description", description)
                .Set("accent", accent);

            configure(fields);
            fields.Apply();

            return asset;
        }

        /// <summary>
        /// Rellena una <see cref="BossPhase"/> dentro del array serializado del jefe.
        ///
        /// <b>Escribe TODOS los campos, también los que "ya tienen valor por defecto".</b> Un
        /// elemento de array creado con <c>SerializedProperty.arraySize</c> se inicializa a ceros:
        /// Unity no ejecuta los inicializadores de campo de C#. Así que un campo que aquí no se
        /// toque no vale 1 — vale 0, y con <c>damageTakenMultiplier</c> eso significa un jefe al
        /// que no se le puede hacer daño en toda la pelea.
        /// </summary>
        public static void WritePhase(SerializedProperty phase, string displayName, float startsAtHealth,
                                      float damageScale, float speedScale, Color accent, Vector2 pause,
                                      float transitionSeconds, float frenzyBelow, BossAttack[] attacks,
                                      float damageTaken = 1f)
        {
            phase.FindPropertyRelative("damageTakenMultiplier").floatValue = Mathf.Max(0.01f, damageTaken);
            phase.FindPropertyRelative("displayName").stringValue = displayName;
            phase.FindPropertyRelative("startsAtHealth").floatValue = startsAtHealth;
            phase.FindPropertyRelative("damageScale").floatValue = damageScale;
            phase.FindPropertyRelative("speedScale").floatValue = speedScale;
            phase.FindPropertyRelative("accent").colorValue = accent;
            phase.FindPropertyRelative("pauseBetweenAttacks").vector2Value = pause;
            phase.FindPropertyRelative("transitionSeconds").floatValue = transitionSeconds;
            phase.FindPropertyRelative("transitionShake").floatValue = transitionSeconds > 0f ? 0.7f : 0f;
            phase.FindPropertyRelative("frenzyBelowHealth").floatValue = frenzyBelow;
            phase.FindPropertyRelative("frenzySpeedScale").floatValue = 1.35f;

            var list = phase.FindPropertyRelative("attacks");
            list.arraySize = attacks.Length;
            for (int i = 0; i < attacks.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = attacks[i];
        }

        /// <summary>
        /// Deja el prefab del jefe en su escena, plantado sobre el suelo real. No hace nada si la
        /// escena ya tiene un jefe: relanzar el generador no debe llenar la arena de copias.
        /// </summary>
        public static void PlaceBossInScene(string prefabPath, string scenePath, float x, float fallbackY)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[BossAuthoring] No existe el prefab '{prefabPath}'. Créalo primero.");
                return;
            }

            // Se abre en aditivo y se cierra al terminar: así la escena en la que esté trabajando
            // el usuario no se descarga ni se le pide guardarla.
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            if (!scene.IsValid())
            {
                Debug.LogError($"[BossAuthoring] No se pudo abrir '{scenePath}'.");
                return;
            }

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<BossController>(true) == null) continue;

                    Debug.LogWarning($"[BossAuthoring] '{scenePath}' ya tiene un jefe; no se toca nada.", root);
                    return;
                }

                // El rayo se lanza desde justo encima de la entrada del jugador, no desde el techo
                // de la escena: una arena puede tener tilemap por encima (un techo, una cornisa) y
                // buscar "lo primero que hay bajando desde arriba del todo" plantaría al jefe ahí.
                // El suelo bueno es el que pisa el jugador al entrar.
                float referenceY = EntryHeight(scene, fallbackY) + 3f;
                var position = new Vector3(x, GroundYAt(x, fallbackY, scene, referenceY), 0f);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.position = position;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                Debug.Log($"[BossAuthoring] Jefe colocado en '{scenePath}' en {position}.", prefab);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        /// <summary>
        /// Altura del suelo (capa Ground) bajo <paramref name="x"/> en la escena recién abierta.
        /// Hace falta sincronizar la física porque los transforms acaban de deserializarse y el
        /// mundo 2D todavía no los conoce.
        ///
        /// <b>El filtro por escena no es opcional en la práctica:</b> la escena del jefe se abre en
        /// aditivo sobre la que ya estuviera abierta (normalmente MainHub) y las consultas de
        /// Physics2D no distinguen escenas — sin filtrar, el rayo choca con el terreno del hub y el
        /// jefe acaba plantado a 30 unidades de altura sobre su arena.
        /// </summary>
        public static float GroundYAt(float x, float fallback, Scene? onlyScene = null, float fromY = 40f)
        {
            Physics2D.SyncTransforms();

            int groundLayer = LayerMask.NameToLayer("Ground");
            int mask = groundLayer >= 0 ? 1 << groundLayer : Physics2D.AllLayers;

            var hits = Physics2D.RaycastAll(new Vector2(x, fromY), Vector2.down, 120f, mask);

            // El rayo cae desde arriba, así que la superficie buena es el impacto más alto de los
            // que sí son de la escena pedida (RaycastAll no garantiza orden).
            bool found = false;
            float top = fallback;

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (onlyScene.HasValue && hit.collider.gameObject.scene != onlyScene.Value) continue;

                if (!found || hit.point.y > top)
                {
                    top = hit.point.y;
                    found = true;
                }
            }

            return found ? top : fallback;
        }

        /// <summary>
        /// Altura a la que entra el jugador en esa escena (su <c>SectionEntry</c>). Es la
        /// referencia de "dónde está el suelo jugable" que usa la colocación del jefe; sin entrada
        /// marcada se cae al valor por defecto que pase el pack del jefe.
        /// </summary>
        private static float EntryHeight(Scene scene, float fallback)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var entry = root.GetComponentInChildren<Run.SectionEntry>(true);
                if (entry != null) return entry.transform.position.y;
            }

            return fallback;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// Da de alta una etiqueta si falta. El jefe y sus esbirros la comparten, y eso es lo que
        /// impide que los proyectiles del jefe maten a sus propias invocaciones.
        /// </summary>
        public static void EnsureTag(string tag)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0) return;

            var so = new SerializedObject(asset[0]);
            var tags = so.FindProperty("tags");

            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[BossAuthoring] Etiqueta '{tag}' dada de alta.");
        }

        public static void TrySetTag(GameObject go, string tag)
        {
            try
            {
                go.tag = tag;
            }
            catch (UnityException)
            {
                Debug.LogWarning($"[BossAuthoring] No se pudo poner la etiqueta '{tag}'.", go);
            }
        }

        /// <summary>Un sprite concreto de una hoja multi-sprite, por nombre.</summary>
        public static Sprite LoadSprite(string sheetPath, string spriteName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName) return sprite;
            }

            Debug.LogWarning($"[BossAuthoring] No se encontró el sprite '{spriteName}' en '{sheetPath}'.");
            return null;
        }

        /// <summary>El primer sprite de un prefab del pack de props (para reusar su arte).</summary>
        public static Sprite LoadSpriteFromPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var source = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;

            if (source != null && source.sprite != null) return source.sprite;

            Debug.LogWarning($"[BossAuthoring] No se encontró un sprite en '{prefabPath}'.");
            return null;
        }

        /// <summary>
        /// Escritor de campos serializados encadenable. Los campos de los ataques y de los
        /// componentes son privados a propósito (nadie debe tocarlos en runtime) y sólo el editor
        /// tiene por qué rellenarlos.
        /// </summary>
        public class Fields
        {
            private readonly SerializedObject _so;

            public Fields(Object target) => _so = new SerializedObject(target);

            public Fields Set(string path, float value) => Write(path, p => p.floatValue = value);
            public Fields Set(string path, int value) => Write(path, p => p.intValue = value);
            public Fields Set(string path, bool value) => Write(path, p => p.boolValue = value);
            public Fields Set(string path, string value) => Write(path, p => p.stringValue = value);
            public Fields Set(string path, Vector2 value) => Write(path, p => p.vector2Value = value);
            public Fields Set(string path, Color value) => Write(path, p => p.colorValue = value);
            public Fields SetObject(string path, Object value) => Write(path, p => p.objectReferenceValue = value);

            /// <summary>Rellena un array de referencias (por ejemplo, los prefabs de una invocación).</summary>
            public Fields SetObjectArray(string path, params Object[] values)
            {
                return Write(path, p =>
                {
                    p.arraySize = values.Length;
                    for (int i = 0; i < values.Length; i++)
                        p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                });
            }

            private Fields Write(string path, Action<SerializedProperty> write)
            {
                var property = _so.FindProperty(path);
                if (property == null)
                {
                    Debug.LogWarning($"[BossAuthoring] El campo '{path}' no existe en {_so.targetObject.name}.");
                    return this;
                }

                write(property);
                return this;
            }

            public void Apply() => _so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
