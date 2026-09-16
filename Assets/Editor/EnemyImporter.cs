// EnemyImporter.cs
// -----------------------------------------------------------------------------
// Importa el paquete exportado por "Enemy Sprite Extractor" (manifest.json +
// carpetas de frames por animación, incluyendo animaciones de proyectiles) y
// genera automáticamente:
//   - Sprites configurados (pivote inferior-centro para el enemigo, centro
//     para los proyectiles)
//   - Un AnimationClip por animación (de enemigo y de proyectil)
//   - Un AnimatorController por enemigo (con triggers de transición) y uno
//     simple por cada proyectil
//   - Un prefab del enemigo Y un prefab por cada proyectil que use, enlazados:
//     si una animación tiene un proyectil asociado, se añade un Animation
//     Event en el frame de lanzamiento que llama a SpawnProjectile(string) en
//     el componente EnemyProjectileSpawner del enemigo.
//
// REQUISITOS:
// 1. Coloca este archivo en Assets/Editor/EnemyImporter.cs
// 2. Copia también EnemyProjectileSpawner.cs y Projectile.cs (scripts NO de
//    editor) en cualquier carpeta normal, p.ej. Assets/Scripts/. Son
//    necesarios para que el importer pueda añadirlos como componentes.
// 3. (Recomendado) Instala el paquete "Newtonsoft Json" desde el Package
//    Manager (com.unity.nuget.newtonsoft-json) para un parseo robusto del
//    manifest. Si no lo tienes, se usa un parser manual de respaldo.
//
// USO:
// 1. Descomprime el .zip exportado por la web DENTRO de tu carpeta Assets,
//    por ejemplo en: Assets/Enemies/RawImport/MushroomWarrior/
// 2. En Unity: Tools > Enemy Importer > Build Enemy From Folder
// 3. Selecciona esa carpeta. Se generará:
//      Assets/Enemies/<Nombre>/Animations/*.anim
//      Assets/Enemies/<Nombre>/<Nombre>Controller.controller
//      Assets/Projectiles/<NombreProyectil>/... (clip + controller)
//      Assets/Prefabs/Enemies/<Nombre>.prefab
//      Assets/Prefabs/Projectiles/<NombreProyectil>.prefab
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class EnemyImporter : EditorWindow
{
    // Ajusta estos valores por defecto a tu proyecto
    private const float PIXELS_PER_UNIT = 100f;
    private const FilterMode SPRITE_FILTER_MODE = FilterMode.Bilinear; // usa Point para pixel art
    private const string ENEMY_PREFAB_FOLDER = "Assets/Prefabs/Enemies";
    private const string PROJECTILE_PREFAB_FOLDER = "Assets/Prefabs/Projectiles";
    private const string PROJECTILE_ASSET_FOLDER = "Assets/Projectiles";

    [System.Serializable]
    public class ProjectileRef
    {
        public string name;
        public int spawnFrame;
    }

    [System.Serializable]
    public class AnimationEntry
    {
        public string name;
        public float fps;
        public bool loop;
        public int frameCount;
        public ProjectileRef projectile; // null si esta animación no dispara nada
    }

    [System.Serializable]
    public class ProjectileEntry
    {
        public string name;
        public float fps;
        public bool loop;
        public int frameCount;
    }

    [System.Serializable]
    public class Manifest
    {
        public string enemyName;
        public List<AnimationEntry> animations;
        public List<ProjectileEntry> projectiles;
    }

    private class BuiltClip
    {
        public string name;
        public float fps;
        public bool loop;
        public AnimationClip clip;
        public Sprite firstSprite;
    }

    [MenuItem("Tools/Enemy Importer/Build Enemy From Folder")]
    public static void BuildEnemyFromFolder()
    {
        string folder = EditorUtility.OpenFolderPanel("Selecciona la carpeta del enemigo (con manifest.json)", "Assets", "");
        if (string.IsNullOrEmpty(folder)) return;

        if (!folder.Replace("\\", "/").Contains("/Assets"))
        {
            EditorUtility.DisplayDialog("Enemy Importer", "La carpeta debe estar dentro de Assets/ de tu proyecto de Unity.", "OK");
            return;
        }

        string manifestPath = Path.Combine(folder, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            EditorUtility.DisplayDialog("Enemy Importer", "No se encontró manifest.json en la carpeta seleccionada.", "OK");
            return;
        }

        Manifest manifest = ParseManifest(File.ReadAllText(manifestPath));
        if (manifest == null || manifest.animations == null || manifest.animations.Count == 0)
        {
            EditorUtility.DisplayDialog("Enemy Importer", "El manifest no contiene animaciones válidas.", "OK");
            return;
        }
        if (manifest.projectiles == null) manifest.projectiles = new List<ProjectileEntry>();

        string relativeFolder = ToAssetsRelativePath(folder);
        string enemyName = string.IsNullOrEmpty(manifest.enemyName) ? "Enemy" : manifest.enemyName;

        // ---------------- 1. Construir clips de PROYECTILES primero ----------------
        var projectilePrefabsByName = new Dictionary<string, GameObject>();
        string projRelativeRoot = $"{relativeFolder}/Projectiles";

        foreach (var proj in manifest.projectiles)
        {
            string projSourceFolder = $"{projRelativeRoot}/{proj.name}";
            var sprites = LoadAndConfigureSprites(projSourceFolder, pivotBottom:false);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[EnemyImporter] No se encontraron imágenes para el proyectil '{proj.name}' en {projSourceFolder}");
                continue;
            }

            string projRoot = $"{PROJECTILE_ASSET_FOLDER}/{proj.name}";
            Directory.CreateDirectory(projRoot);
            AssetDatabase.Refresh();

            AnimationClip clip = BuildClip(sprites, proj.fps, proj.loop);
            AssetDatabase.CreateAsset(clip, $"{projRoot}/{proj.name}.anim");

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath($"{projRoot}/{proj.name}Controller.controller");
            var state = controller.layers[0].stateMachine.AddState(proj.name);
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;

            GameObject projPrefabObj = BuildProjectilePrefab(proj.name, controller, sprites[0]);
            Directory.CreateDirectory(PROJECTILE_PREFAB_FOLDER);
            AssetDatabase.Refresh();
            string prefabPath = $"{PROJECTILE_PREFAB_FOLDER}/{proj.name}.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(projPrefabObj, prefabPath);
            Object.DestroyImmediate(projPrefabObj);

            projectilePrefabsByName[proj.name] = savedPrefab;
        }

        // ---------------- 2. Construir clips del ENEMIGO ----------------
        string enemyRoot = $"Assets/Enemies/{enemyName}";
        string animFolder = $"{enemyRoot}/Animations";
        Directory.CreateDirectory(animFolder);
        AssetDatabase.Refresh();

        var builtClips = new List<BuiltClip>();
        // guarda, por nombre de animación, qué proyectil dispara y en qué frame (tiempo normalizado 0-1 para el evento)
        var projectileTriggers = new Dictionary<string, ProjectileRef>();

        foreach (var anim in manifest.animations)
        {
            string animSourceFolder = $"{relativeFolder}/{anim.name}";
            var sprites = LoadAndConfigureSprites(animSourceFolder, pivotBottom:true);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[EnemyImporter] No se encontraron imágenes para la animación '{anim.name}' en {animSourceFolder}");
                continue;
            }

            AnimationClip clip = BuildClip(sprites, anim.fps, anim.loop);

            // Si esta animación dispara un proyectil, añade el Animation Event en el frame indicado
            if (anim.projectile != null && projectilePrefabsByName.ContainsKey(anim.projectile.name))
            {
                AddSpawnProjectileEvent(clip, anim.projectile.spawnFrame, anim.fps, anim.projectile.name);
                projectileTriggers[anim.name] = anim.projectile;
            }

            AssetDatabase.CreateAsset(clip, $"{animFolder}/{anim.name}.anim");
            builtClips.Add(new BuiltClip { name = anim.name, fps = anim.fps, loop = anim.loop, clip = clip, firstSprite = sprites[0] });
        }

        if (builtClips.Count == 0)
        {
            EditorUtility.DisplayDialog("Enemy Importer", "No se pudo generar ninguna animación de enemigo.", "OK");
            return;
        }

        AnimatorController enemyController = BuildEnemyAnimatorController(enemyRoot, enemyName, builtClips);
        GameObject enemyPrefabObj = BuildEnemyPrefab(enemyName, enemyController, builtClips[0].firstSprite, projectilePrefabsByName, projectileTriggers.Values);

        Directory.CreateDirectory(ENEMY_PREFAB_FOLDER);
        AssetDatabase.Refresh();
        string enemyPrefabPath = $"{ENEMY_PREFAB_FOLDER}/{enemyName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(enemyPrefabObj, enemyPrefabPath);
        Object.DestroyImmediate(enemyPrefabObj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Enemy Importer",
            $"Enemigo '{enemyName}' generado:\n\n" +
            $"- {builtClips.Count} animaciones\n" +
            $"- {projectilePrefabsByName.Count} prefab(s) de proyectil\n" +
            $"- Prefab enemigo: {enemyPrefabPath}",
            "OK");

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPrefabPath);
    }

    // -------------------------------------------------------------------
    private static List<Sprite> LoadAndConfigureSprites(string relativeFolder, bool pivotBottom)
    {
        var spritePaths = AssetDatabase.FindAssets("t:Texture2D", new[] { relativeFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p)
            .ToList();

        var sprites = new List<Sprite>();
        foreach (var path in spritePaths)
        {
            ConfigureSpriteImport(path, pivotBottom);
            Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (spr != null) sprites.Add(spr);
        }
        return sprites;
    }

    private static void ConfigureSpriteImport(string path, bool pivotBottom)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = SPRITE_FILTER_MODE;
        importer.spritePixelsPerUnit = PIXELS_PER_UNIT;

        Vector2 pivot = pivotBottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
        importer.spritePivot = pivot;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static AnimationClip BuildClip(List<Sprite> sprites, float fps, bool loop)
    {
        AnimationClip clip = new AnimationClip();
        clip.frameRate = fps > 0 ? fps : 8f;

        EditorCurveBinding binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
        float frameDuration = 1f / clip.frameRate;
        for (int i = 0; i < sprites.Count; i++)
            keyframes[i] = new ObjectReferenceKeyframe { time = i * frameDuration, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

        return clip;
    }

    // Añade un AnimationEvent que llama a SpawnProjectile(string projectileName) en el frame indicado
    private static void AddSpawnProjectileEvent(AnimationClip clip, int spawnFrame, float fps, string projectileName)
    {
        float time = spawnFrame / (fps > 0 ? fps : 8f);
        time = Mathf.Min(time, clip.length);

        AnimationEvent evt = new AnimationEvent
        {
            time = time,
            functionName = "SpawnProjectile",
            stringParameter = projectileName
        };

        var existing = AnimationUtility.GetAnimationEvents(clip).ToList();
        existing.Add(evt);
        AnimationUtility.SetAnimationEvents(clip, existing.ToArray());
    }

    private static AnimatorController BuildEnemyAnimatorController(string enemyRoot, string enemyName, List<BuiltClip> clips)
    {
        string controllerPath = $"{enemyRoot}/{enemyName}Controller.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var rootStateMachine = controller.layers[0].stateMachine;

        var idleEntry = clips.FirstOrDefault(c => c.name.ToLower() == "idle");
        var defaultEntry = idleEntry ?? clips[0];

        var statesByName = new Dictionary<string, AnimatorState>();
        foreach (var c in clips)
        {
            AnimatorState state = rootStateMachine.AddState(c.name);
            state.motion = c.clip;
            statesByName[c.name] = state;
        }

        rootStateMachine.defaultState = statesByName[defaultEntry.name];

        foreach (var c in clips)
        {
            if (c.name == defaultEntry.name) continue;

            controller.AddParameter(c.name, AnimatorControllerParameterType.Trigger);

            AnimatorStateTransition anyTransition = rootStateMachine.AddAnyStateTransition(statesByName[c.name]);
            anyTransition.AddCondition(AnimatorConditionMode.If, 0, c.name);
            anyTransition.duration = 0.05f;
            anyTransition.hasExitTime = false;
            anyTransition.canTransitionToSelf = false;

            bool isDeath = c.name.ToLower() == "death";
            if (!c.loop && !isDeath)
            {
                AnimatorStateTransition backToIdle = statesByName[c.name].AddTransition(statesByName[defaultEntry.name]);
                backToIdle.hasExitTime = true;
                backToIdle.exitTime = 1f;
                backToIdle.duration = 0.05f;
            }
        }

        return controller;
    }

    private static GameObject BuildEnemyPrefab(string enemyName, AnimatorController controller, Sprite defaultSprite,
        Dictionary<string, GameObject> projectilePrefabsByName, IEnumerable<ProjectileRef> usedProjectiles)
    {
        GameObject go = new GameObject(enemyName);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = defaultSprite;

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic; // ajusta según tu sistema de movimiento

        var col = go.AddComponent<BoxCollider2D>();
        if (defaultSprite != null)
        {
            col.size = defaultSprite.bounds.size;
            col.offset = new Vector2(0, defaultSprite.bounds.extents.y);
        }

        // Sólo añade el spawner si el enemigo realmente dispara algún proyectil
        var distinctProjNames = usedProjectiles.Select(p => p.name).Distinct().ToList();
        if (distinctProjNames.Count > 0)
        {
            var spawnerType = System.Type.GetType("EnemyProjectileSpawner");
            if (spawnerType == null)
            {
                Debug.LogWarning("[EnemyImporter] No se encontró el script 'EnemyProjectileSpawner' en el proyecto. " +
                    "Copia EnemyProjectileSpawner.cs (no en carpeta Editor) y vuelve a importar para enlazar los proyectiles automáticamente.");
            }
            else
            {
                var spawnerComponent = go.AddComponent(spawnerType);
                var so = new SerializedObject(spawnerComponent);
                var listProp = so.FindProperty("projectiles");
                if (listProp != null)
                {
                    listProp.ClearArray();
                    int i = 0;
                    foreach (var name in distinctProjNames)
                    {
                        if (!projectilePrefabsByName.ContainsKey(name)) continue;
                        listProp.InsertArrayElementAtIndex(i);
                        var element = listProp.GetArrayElementAtIndex(i);
                        element.FindPropertyRelative("name").stringValue = name;
                        element.FindPropertyRelative("prefab").objectReferenceValue = projectilePrefabsByName[name];
                        i++;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        return go;
    }

    private static GameObject BuildProjectilePrefab(string projectileName, AnimatorController controller, Sprite defaultSprite)
    {
        GameObject go = new GameObject(projectileName);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = defaultSprite;

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        if (defaultSprite != null)
            col.radius = Mathf.Max(defaultSprite.bounds.extents.x, defaultSprite.bounds.extents.y);

        var projType = System.Type.GetType("Projectile");
        if (projType != null)
        {
            go.AddComponent(projType);
        }
        else
        {
            Debug.LogWarning("[EnemyImporter] No se encontró el script 'Projectile' en el proyecto. " +
                "Copia Projectile.cs (no en carpeta Editor) para que el prefab tenga movimiento por defecto.");
        }

        return go;
    }

    // -------------------------------------------------------------------
    private static string ToAssetsRelativePath(string absolutePath)
    {
        absolutePath = absolutePath.Replace("\\", "/");
        int idx = absolutePath.IndexOf("/Assets");
        return absolutePath.Substring(idx + 1);
    }

    private static Manifest ParseManifest(string json)
    {
        var newtonsoftType = System.Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json");
        if (newtonsoftType != null)
        {
            var method = newtonsoftType.GetMethod("DeserializeObject", new[] { typeof(string), typeof(System.Type) });
            return (Manifest)method.Invoke(null, new object[] { json, typeof(Manifest) });
        }
        return ManualParseManifest(json);
    }

    // Parser manual de respaldo para:
    // { "enemyName": "...", "animations":[{name,fps,loop,frameCount,projectile:{name,spawnFrame}|null}],
    //   "projectiles":[{name,fps,loop,frameCount}] }
    private static Manifest ManualParseManifest(string json)
    {
        Manifest m = new Manifest { animations = new List<AnimationEntry>(), projectiles = new List<ProjectileEntry>() };
        m.enemyName = ExtractStringField(json, "enemyName");

        m.animations = ParseAnimationArray(json, "animations");
        m.projectiles = ParseProjectileArray(json, "projectiles");
        return m;
    }

    private static List<AnimationEntry> ParseAnimationArray(string json, string field)
    {
        var result = new List<AnimationEntry>();
        int arrStart = json.IndexOf("\"" + field + "\"");
        if (arrStart < 0) return result;
        arrStart = json.IndexOf('[', arrStart);
        int depth = 0, i = arrStart, objStart = -1;

        while (i < json.Length)
        {
            char c = json[i];
            if (c == '{') { if (depth == 0) objStart = i; depth++; }
            else if (c == '}')
            {
                depth--;
                if (depth == 0 && objStart >= 0)
                {
                    string obj = json.Substring(objStart, i - objStart + 1);
                    var entry = new AnimationEntry
                    {
                        name = ExtractStringField(obj, "name"),
                        fps = ExtractFloatField(obj, "fps"),
                        loop = ExtractBoolField(obj, "loop"),
                        frameCount = (int)ExtractFloatField(obj, "frameCount")
                    };

                    int projIdx = obj.IndexOf("\"projectile\"");
                    if (projIdx >= 0)
                    {
                        int nullCheck = obj.IndexOf(':', projIdx) + 1;
                        while (nullCheck < obj.Length && obj[nullCheck] == ' ') nullCheck++;
                        if (obj.Substring(nullCheck, System.Math.Min(4, obj.Length - nullCheck)) != "null")
                        {
                            int pObjStart = obj.IndexOf('{', projIdx);
                            int pObjEnd = obj.IndexOf('}', pObjStart);
                            if (pObjStart >= 0 && pObjEnd > pObjStart)
                            {
                                string pObj = obj.Substring(pObjStart, pObjEnd - pObjStart + 1);
                                entry.projectile = new ProjectileRef
                                {
                                    name = ExtractStringField(pObj, "name"),
                                    spawnFrame = (int)ExtractFloatField(pObj, "spawnFrame")
                                };
                            }
                        }
                    }
                    result.Add(entry);
                }
            }
            else if (c == ']' && depth == 0) break;
            i++;
        }
        return result;
    }

    private static List<ProjectileEntry> ParseProjectileArray(string json, string field)
    {
        var result = new List<ProjectileEntry>();
        int arrStart = json.IndexOf("\"" + field + "\"");
        if (arrStart < 0) return result;
        arrStart = json.IndexOf('[', arrStart);
        int depth = 0, i = arrStart, objStart = -1;

        while (i < json.Length)
        {
            char c = json[i];
            if (c == '{') { if (depth == 0) objStart = i; depth++; }
            else if (c == '}')
            {
                depth--;
                if (depth == 0 && objStart >= 0)
                {
                    string obj = json.Substring(objStart, i - objStart + 1);
                    result.Add(new ProjectileEntry
                    {
                        name = ExtractStringField(obj, "name"),
                        fps = ExtractFloatField(obj, "fps"),
                        loop = ExtractBoolField(obj, "loop"),
                        frameCount = (int)ExtractFloatField(obj, "frameCount")
                    });
                }
            }
            else if (c == ']' && depth == 0) break;
            i++;
        }
        return result;
    }

    private static string ExtractStringField(string json, string field)
    {
        string pattern = "\"" + field + "\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return "";
        int colon = json.IndexOf(':', idx);
        int quoteStart = json.IndexOf('"', colon + 1);
        int quoteEnd = json.IndexOf('"', quoteStart + 1);
        if (quoteStart < 0 || quoteEnd < 0) return "";
        return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
    }

    private static float ExtractFloatField(string json, string field)
    {
        string pattern = "\"" + field + "\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return 0;
        int colon = json.IndexOf(':', idx);
        int start = colon + 1;
        while (start < json.Length && json[start] == ' ') start++;
        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '.')) end++;
        float.TryParse(json.Substring(start, end - start), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float result);
        return result;
    }

    private static bool ExtractBoolField(string json, string field)
    {
        string pattern = "\"" + field + "\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return false;
        int colon = json.IndexOf(':', idx);
        int start = colon + 1;
        while (start < json.Length && json[start] == ' ') start++;
        return json.Substring(start, System.Math.Min(4, json.Length - start)) == "true";
    }
}
