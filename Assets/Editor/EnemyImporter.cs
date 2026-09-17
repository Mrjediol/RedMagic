// EnemyImporter.cs
// -----------------------------------------------------------------------------
// Importa el paquete exportado por "Enemy Sprite Extractor" (manifest.json +
// carpetas de frames por animación, incluyendo animaciones de proyectiles) y
// genera automáticamente:
//   - Sprites configurados (pivote elegido por 'anchor' — RedMagic.Pipeline.AnchorMode, el mismo
//     enum que ya usa SpriteSheetRecipe.anchor en el resto del pipeline — para el enemigo; siempre
//     centro para los proyectiles, sin cambios)
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
//      Assets/Enemies/<Nombre>/<Nombre>.sheet.asset   (SpriteSheetRecipe — ver más abajo)
//      Assets/Projectiles/<NombreProyectil>/... (clip + controller)
//      Assets/Prefabs/Enemies/<Nombre>.prefab
//      Assets/Prefabs/Projectiles/<NombreProyectil>.prefab
//
// SpriteSheetRecipe: EnemyConfigImporter.cs (Tools > RedMagic > Import Config) exige que el campo
// 'art' de un EnemyConfig apunte a un SpriteSheetRecipe real — es el único tipo que
// EnemyFactory.Generate sabe leer para vestir el prefab de un enemigo del pipeline nuevo (ver
// docs/enemy-config-art-field-audit.md). Este importador no corta una lámina única con
// SheetSlicer — viene de un manifest + un PNG por frame — así que el recipe que construye es "de
// compatibilidad": mismos datos que el manifest, más una copia mínima del controller y de un
// sprite de reposo por fila con el nombre exacto que EnemyFactory busca en disco. Limitación
// conocida: un enemigo a distancia generado por esta vía no tiene el "prop" suelto que
// EnemyFactory.ApplyProjectile busca (esa convención es propia de SheetSlicer) — cae en el
// fallback ya soportado por el proyecto, un proyectil construido en código sin prefab, no en un
// error.
// -----------------------------------------------------------------------------

using RedMagic.Pipeline;
using RedMagic.Pipeline.EditorTools;
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

    [MenuItem("Tools/Web/Enemy Importer/Build Enemy From Folder")]
    public static void BuildEnemyFromFolder()
    {
        string folder = EditorUtility.OpenFolderPanel("Selecciona la carpeta del enemigo (con manifest.json)", "Assets", "");
        if (string.IsNullOrEmpty(folder)) return;

        BuildEnemyFromFolderPath(folder);
    }

    /// <summary>Dónde cae el pivote de un sprite dado su <see cref="AnchorMode"/> — mismo mapeo que
    /// SheetSlicer usa para el resto del pipeline (BottomCenter = pies, Center = centro de la caja).</summary>
    private static Vector2 PivotFor(AnchorMode anchor) =>
        anchor == AnchorMode.Center ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);

    /// <summary>
    /// El cuerpo real de <see cref="BuildEnemyFromFolder"/>, separado del selector de carpeta para
    /// poder invocarlo también sin diálogo (scripts, <c>unity command eval</c>, tests) — mismo
    /// patrón que <c>SpritePipeline.RunSheet</c>/<c>RunEnemy</c> ya usan en el resto del pipeline.
    ///
    /// <paramref name="showDialog"/> = false cambia cada <c>EditorUtility.DisplayDialog</c> por un
    /// <c>Debug.Log</c>: un diálogo modal deja el hilo principal del Editor bloqueado hasta que
    /// alguien hace click, y eso incluye <c>unity command eval</c> — cualquier llamada headless se
    /// queda colgada "Main thread operation timed out" hasta que un humano entra a Unity a pulsar
    /// OK. El menú (<see cref="BuildEnemyFromFolder"/>) sigue mostrando el diálogo de verdad.
    ///
    /// <paramref name="anchor"/> decide el pivote de los sprites DEL ENEMIGO (no de sus
    /// proyectiles, que siempre van centrados) y, con ello, el offset del collider generado y el
    /// <see cref="SpriteSheetRecipe.anchor"/> del recipe de compatibilidad que este método produce
    /// — antes esto estaba fijo a "pies" (BottomCenter) sin importar el archetype, lo que dejaba a
    /// cualquier enemigo volador con el pivote en un punto vacío del aire bajo su cuerpo, y con él
    /// el detectionRange/attackRange/explosionRadius y sus gizmos (todos medidos desde
    /// transform.position) descentrados del arte. Default <see cref="AnchorMode.Center"/> porque el
    /// caso más común de esta vía de import (Enemy Creator ya no distingue "de suelo" al construir
    /// el zip) es más seguro sin asumir que el bicho tiene pies.
    /// </summary>
    public static void BuildEnemyFromFolderPath(string folder, AnchorMode anchor = AnchorMode.Center, bool showDialog = true)
    {
        void Report(string message)
        {
            if (showDialog) EditorUtility.DisplayDialog("Enemy Importer", message, "OK");
            else Debug.Log($"[EnemyImporter] {message.Replace("\n\n", " — ").Replace("\n", " ")}");
        }

        if (!folder.Replace("\\", "/").Contains("/Assets"))
        {
            Report("La carpeta debe estar dentro de Assets/ de tu proyecto de Unity.");
            return;
        }

        string manifestPath = Path.Combine(folder, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Report("No se encontró manifest.json en la carpeta seleccionada.");
            return;
        }

        Manifest manifest = ParseManifest(File.ReadAllText(manifestPath));
        if (manifest == null || manifest.animations == null || manifest.animations.Count == 0)
        {
            Report("El manifest no contiene animaciones válidas.");
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
            // Proyectiles: siempre centrados, sin cambios — sólo el enemigo respeta 'anchor'.
            var sprites = LoadAndConfigureSprites(projSourceFolder, AnchorMode.Center);
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
            var sprites = LoadAndConfigureSprites(animSourceFolder, anchor);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[EnemyImporter] No se encontraron imágenes para la animación '{anim.name}' en {animSourceFolder}");
                continue;
            }

            AnimationClip clip = BuildClip(sprites, anim.fps, anim.loop, RedMagic.Pipeline.EditorTools.AnimClipBuilder.RendererPath);

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
            Report("No se pudo generar ninguna animación de enemigo.");
            return;
        }

        AnimatorController enemyController = BuildEnemyAnimatorController(enemyRoot, enemyName, builtClips);
        GameObject enemyPrefabObj = BuildEnemyPrefab(enemyName, enemyController, builtClips[0].firstSprite, projectilePrefabsByName, projectileTriggers.Values, anchor);

        Directory.CreateDirectory(ENEMY_PREFAB_FOLDER);
        AssetDatabase.Refresh();
        string enemyPrefabPath = $"{ENEMY_PREFAB_FOLDER}/{enemyName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(enemyPrefabObj, enemyPrefabPath);
        Object.DestroyImmediate(enemyPrefabObj);

        SpriteSheetRecipe sheetRecipe = BuildSpriteSheetRecipe(enemyRoot, enemyName, manifest, enemyController, builtClips, anchor);
        string sheetRecipePath = AssetDatabase.GetAssetPath(sheetRecipe);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Report(
            $"Enemigo '{enemyName}' generado:\n\n" +
            $"- {builtClips.Count} animaciones\n" +
            $"- {projectilePrefabsByName.Count} prefab(s) de proyectil\n" +
            $"- Prefab enemigo: {enemyPrefabPath}\n" +
            $"- SpriteSheetRecipe (para EnemyConfig.art): {sheetRecipePath}");

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPrefabPath);
    }

    // ---------------------------------------------------------------------------
    // SpriteSheetRecipe de compatibilidad — ver el comentario de cabecera del archivo.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Crea (o actualiza, si ya existe — idempotente, igual que el resto del pipeline) un
    /// <see cref="SpriteSheetRecipe"/> a partir de los mismos datos del manifest que el resto de
    /// este importador ya usa, más una copia mínima del controller y de un sprite de reposo por
    /// fila con el nombre/ruta exactos que <c>EnemyFactory</c> busca en disco.
    /// </summary>
    private static SpriteSheetRecipe BuildSpriteSheetRecipe(string enemyRoot, string enemyName,
        Manifest manifest, AnimatorController controller, List<BuiltClip> builtClips, AnchorMode anchor)
    {
        string recipePath = $"{enemyRoot}/{enemyName}.sheet.asset";
        SpriteSheetRecipe recipe = AssetDatabase.LoadAssetAtPath<SpriteSheetRecipe>(recipePath);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<SpriteSheetRecipe>();
            AssetDatabase.CreateAsset(recipe, recipePath);
        }

        recipe.characterName = enemyName;
        // outputFolder NO se deja en blanco: el default de SpriteSheetRecipe.ResolvedFolder es
        // Assets/Art/Characters/<nombre>, que es donde vive el corte real de SheetSlicer — este
        // importador guarda el controller y los PNG en enemyRoot (Assets/Enemies/<nombre>), así
        // que EnemyFactory tiene que buscar ahí.
        recipe.outputFolder = enemyRoot;
        recipe.runtime = AnimRuntime.Animator;
        // Debe coincidir con el pivote que ConfigureSpriteImport aplicó a los sprites de este mismo
        // enemigo: si EnemyFactory.Generate llegara a re-generar sobre este recipe más adelante
        // (p.ej. Pipeline > 3b), lee este campo para decidir el offset del collider — dejarlo en su
        // default (BottomCenter) desincronizaría ese futuro regen del pivote real ya horneado aquí.
        recipe.anchor = anchor;
        recipe.rows = manifest.animations.Select(a => new SheetRow
        {
            state = a.name,
            frames = a.frameCount,
            fps = a.fps > 0 ? a.fps : 8f,
            loop = a.loop,
            releaseFrame = -1, // sin AnimationEvent propio; EnemyAttack usa su respaldo por tiempo
        }).ToArray();
        EditorUtility.SetDirty(recipe);

        // EnemyFactory.ApplyAnimation busca el controller como '{ResolvedFolder}/{characterName}.controller'
        // (sin el sufijo 'Controller' que usa este importador). Se copia con ese segundo nombre en
        // vez de renombrar el original, para no romper nada que ya dependa de '<Nombre>Controller.controller'.
        string expectedControllerPath = $"{enemyRoot}/{enemyName}.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(expectedControllerPath) == null)
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(controller), expectedControllerPath);
        }

        // EnemyFactory.IdleSprite busca, dentro de '{ResolvedFolder}/{characterName}_{fila}.png',
        // un sub-sprite llamado exactamente SheetSlicer.SpriteName(characterName, fila, 0). Esto
        // NO reproduce el corte multi-frame real de SheetSlicer — sólo copia el primer frame de
        // cada fila con ese nombre, lo mínimo para que EnemyFactory tenga un sprite de reposo (y
        // un collider deducido de él) de verdad en vez de salir sin arte.
        foreach (var clip in builtClips) EnsureRepresentativeSprite(enemyRoot, enemyName, clip, anchor);

        return recipe;
    }

    /// <summary>Copia el primer frame de <paramref name="clip"/> con el nombre que EnemyFactory.IdleSprite espera.</summary>
    private static void EnsureRepresentativeSprite(string enemyRoot, string enemyName, BuiltClip clip, AnchorMode anchor)
    {
        if (clip.firstSprite == null) return;

        string destPath = $"{enemyRoot}/{enemyName}_{clip.name}.png";
        string desiredName = SheetSlicer.SpriteName(enemyName, clip.name, 0);

        // Ya existe con el nombre correcto: nada que hacer (idempotente).
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(destPath))
            if (asset is Sprite sprite && sprite.name == desiredName) return;

        if (!File.Exists(destPath))
        {
            string sourcePath = AssetDatabase.GetAssetPath(clip.firstSprite);
            if (string.IsNullOrEmpty(sourcePath)) return;
            AssetDatabase.CopyAsset(sourcePath, destPath);
            AssetDatabase.Refresh();
        }

        var importer = AssetImporter.GetAtPath(destPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable CS0618 // TextureImporter.spritesheet: API vieja pero sigue siendo la forma
                               // más simple de dar nombre a un único sub-sprite sin depender de
                               // ISpriteEditorDataProvider para este caso mínimo.
        importer.spritesheet = new[]
        {
            new SpriteMetaData
            {
                name = desiredName,
                rect = new Rect(0, 0, clip.firstSprite.texture.width, clip.firstSprite.texture.height),
                alignment = (int)SpriteAlignment.Custom,
                pivot = PivotFor(anchor), // igual que ConfigureSpriteImport(anchor) para este mismo enemigo
            },
        };
#pragma warning restore CS0618
        importer.SaveAndReimport();
    }

    // -------------------------------------------------------------------
    private static List<Sprite> LoadAndConfigureSprites(string relativeFolder, AnchorMode anchor)
    {
        var spritePaths = AssetDatabase.FindAssets("t:Texture2D", new[] { relativeFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p)
            .ToList();

        var sprites = new List<Sprite>();
        foreach (var path in spritePaths)
        {
            ConfigureSpriteImport(path, anchor);
            Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (spr != null) sprites.Add(spr);
        }
        return sprites;
    }

    private static void ConfigureSpriteImport(string path, AnchorMode anchor)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = SPRITE_FILTER_MODE;
        importer.spritePixelsPerUnit = PIXELS_PER_UNIT;

        Vector2 pivot = PivotFor(anchor);
        importer.spritePivot = pivot;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    /// <summary>
    /// <paramref name="rendererPath"/> debe coincidir con dónde vive el <c>SpriteRenderer</c> visto
    /// DESDE el GameObject que lleva el Animator, o la curva no resuelve y el clip reproduce "sin
    /// arte" (Unity lo enseña como "Sprite Missing" en la ventana Animation).
    ///  - Vacío ("") para el prefab que este mismo importador construye
    ///    (<see cref="BuildEnemyPrefab"/>/<see cref="BuildProjectilePrefab"/>): ahí el
    ///    SpriteRenderer va en la MISMA raíz que el Animator.
    ///  - <c>AnimClipBuilder.RendererPath</c> ("Sprite") para que el MISMO clip también funcione en
    ///    el prefab que construye <c>EnemyFactory.Generate</c> — vía el SpriteSheetRecipe de
    ///    compatibilidad de <see cref="BuildSpriteSheetRecipe"/> —, donde el Animator va en la raíz
    ///    pero el SpriteRenderer en el hijo "Sprite" (ver el comentario de cabecera de este archivo).
    /// </summary>
    private static AnimationClip BuildClip(List<Sprite> sprites, float fps, bool loop, string rendererPath = "")
    {
        AnimationClip clip = new AnimationClip();
        clip.frameRate = fps > 0 ? fps : 8f;

        EditorCurveBinding binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = rendererPath, propertyName = "m_Sprite" };
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

    /// <summary>
    /// Construye el controller con el vocabulario de parámetros REAL que
    /// <c>RedMagic.Enemies.EnemyAnimation</c> espera — el mismo que
    /// <c>RedMagic.Pipeline.EditorTools.AnimClipBuilder.BuildController</c> ya usa para el pipeline
    /// de verdad: <c>Moving</c> (bool, Idle↔Walk), <c>Attack</c>/<c>Hurt</c> (triggers, desde
    /// AnyState), <c>Dead</c> (bool, desde AnyState, sin salida), más un <c>&lt;Estado&gt;Speed</c>
    /// (float) por estado.
    ///
    /// Una versión anterior de este método inventaba su propio vocabulario (un trigger llamado
    /// literalmente "Walk", "Death" como trigger en vez de "Dead" como bool) — coincidía por
    /// casualidad con "Attack"/"Hurt", pero <c>Animator.SetBool("Moving", ...)</c> y
    /// <c>SetBool("Dead", ...)</c> sobre un parámetro que no existe no lanza excepción, simplemente
    /// no hace nada: el enemigo se quedaba congelado en Idle al andar y nunca reproducía el clip de
    /// muerte. <c>EnemyAnimation</c> sólo aplica los parámetros de velocidad que de verdad existen
    /// en el controller (los cachea una vez en <c>Awake</c>), así que da igual si faltan algunos
    /// estados — lo que no puede faltar es <c>Moving</c>/<c>Attack</c>/<c>Hurt</c>/<c>Dead</c>.
    /// </summary>
    private static AnimatorController BuildEnemyAnimatorController(string enemyRoot, string enemyName, List<BuiltClip> clips)
    {
        string controllerPath = $"{enemyRoot}/{enemyName}Controller.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var rootStateMachine = controller.layers[0].stateMachine;

        controller.AddParameter(new AnimatorControllerParameter { name = "Moving", type = AnimatorControllerParameterType.Bool });
        controller.AddParameter(new AnimatorControllerParameter { name = "Attack", type = AnimatorControllerParameterType.Trigger });
        controller.AddParameter(new AnimatorControllerParameter { name = "Hurt", type = AnimatorControllerParameterType.Trigger });
        controller.AddParameter(new AnimatorControllerParameter { name = "Dead", type = AnimatorControllerParameterType.Bool });

        var statesByName = new Dictionary<string, AnimatorState>();
        foreach (var c in clips)
        {
            AnimatorState state = rootStateMachine.AddState(c.name);
            state.motion = c.clip;

            string speedParam = $"{c.name}Speed";
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = speedParam, type = AnimatorControllerParameterType.Float, defaultFloat = 1f,
            });
            state.speedParameterActive = true;
            state.speedParameter = speedParam;

            statesByName[c.name] = state;
        }

        AnimatorState Find(string name) =>
            statesByName.FirstOrDefault(kv => string.Equals(kv.Key, name, System.StringComparison.OrdinalIgnoreCase)).Value;

        var idle = Find("Idle");
        var walk = Find("Walk");
        var attack = Find("Attack");
        var hurt = Find("Hurt");
        var death = Find("Death");

        rootStateMachine.defaultState = idle ?? statesByName.Values.First();

        // Idle <-> Walk por el bool "Moving" que escribe EnemyBrain — no por trigger.
        if (idle != null && walk != null)
        {
            var toWalk = idle.AddTransition(walk);
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            toWalk.hasExitTime = false;
            toWalk.duration = 0.05f;

            var toIdle = walk.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.05f;
        }

        // Attack/Hurt desde AnyState (para que interrumpan lo que sea), y de vuelta a Idle al
        // acabar el clip — igual que AnimClipBuilder.WireTransitions.
        foreach (var (name, state) in new[] { ("Attack", attack), ("Hurt", hurt) })
        {
            if (state == null) continue;

            var enter = rootStateMachine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, name);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            enter.hasExitTime = false;
            enter.duration = 0.02f;
            enter.canTransitionToSelf = false;

            if (idle == null) continue;

            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 1f;
            exit.duration = 0.05f;
        }

        // Muerte: bool "Dead" desde AnyState, sin transición de salida (destino terminal).
        if (death != null)
        {
            var die = rootStateMachine.AddAnyStateTransition(death);
            die.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            die.hasExitTime = false;
            die.duration = 0.05f;
            die.canTransitionToSelf = false;
        }

        return controller;
    }

    private static GameObject BuildEnemyPrefab(string enemyName, AnimatorController controller, Sprite defaultSprite,
        Dictionary<string, GameObject> projectilePrefabsByName, IEnumerable<ProjectileRef> usedProjectiles, AnchorMode anchor)
    {
        GameObject go = new GameObject(enemyName);

        // El SpriteRenderer va en un hijo llamado "Sprite" — NO en la raíz — para que este prefab
        // reproduzca los mismos AnimationClip que construye EnemyFactory.Generate (Animator en la
        // raíz, SpriteRenderer en RedMagic.Pipeline.EditorTools.AnimClipBuilder.RendererPath). Los
        // clips llevan esa ruta grabada en su curva de sprites (ver BuildClip): con el
        // SpriteRenderer en la raíz, la curva no resuelve y Unity lo enseña como "Sprite Missing".
        var spriteChild = new GameObject(RedMagic.Pipeline.EditorTools.AnimClipBuilder.RendererPath);
        spriteChild.transform.SetParent(go.transform, false);
        var sr = spriteChild.AddComponent<SpriteRenderer>();
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
            // Mismo cálculo que EnemyFactory.cs para 'centeredPivot': con el pivote en el centro de
            // la caja (Center) el collider ya queda centrado en el origen; sólo un pivote a los pies
            // (BottomCenter) necesita empujarlo media altura hacia arriba. Antes esto asumía
            // siempre pies, así que un enemigo Center quedaba con el collider medio cuerpo por
            // encima del dibujo — el mismo bug que motivó 'centeredPivot' en EnemyFactory.
            col.offset = anchor == AnchorMode.Center ? Vector2.zero : new Vector2(0, defaultSprite.bounds.extents.y);
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
