using System.IO;
using System.Linq;
using RedMagic.Gameplay;
using RedMagic.Levels;
using RedMagic.LevelsEditor;
using RedMagic.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace RedMagic.EditorNiveles
{
    /// <summary>
    /// Menú <b>Tools ▸ EditorNiveles</b>: abrir el editor web (<c>Web/EditorNiveles/index.html</c>), crear una escena de
    /// nivel desde un boceto, volcar un boceto en el nivel abierto y exportar el nivel abierto para seguir en la web.
    /// Tras importar, el nivel se sigue editando en Unity (paleta de terreno, objetos): reimportar sólo toca los tiles
    /// y el tamaño. El montaje del sistema de niveles (bioma, paleta, fondo, nieve) cuelga de <c>Montaje</c>.
    /// </summary>
    public static class EditorNivelesMenu
    {
        public const string Root = "Tools/EditorNiveles/";
        private const string LastFolderKey = "EditorNiveles.LastFolder";

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string LastFolder
        {
            get => EditorPrefs.GetString(LastFolderKey, Path.Combine(ProjectRoot, "Web", "EditorNiveles", "bocetos"));
            set => EditorPrefs.SetString(LastFolderKey, value);
        }

        [MenuItem(Root + "Abrir editor web", priority = 0)]
        private static void OpenWeb()
        {
            EditorNivelesWebData.Write();
            string page = Path.Combine(ProjectRoot, "Web", "EditorNiveles", "index.html");
            Application.OpenURL("file:///" + page.Replace('\\', '/'));
        }

        [MenuItem(Root + "Nuevo nivel desde boceto...", priority = 20)]
        private static void NewLevelFromSketch()
        {
            var sketch = PickSketch("Nuevo nivel desde boceto");
            if (sketch == null || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path = EditorUtility.SaveFilePanelInProject("Escena del nivel", sketch.name, "unity",
                "Dónde guardar la escena del nivel nuevo.", "Assets/Scenes");
            if (string.IsNullOrEmpty(path)) return;
            CreateLevelScene(sketch, path);
        }

        [MenuItem(Root + "Importar boceto en el nivel abierto...", priority = 21)]
        private static void ImportIntoOpenLevel()
        {
            var level = FindOpenLevel();
            if (level == null) return;
            var sketch = PickSketch("Importar boceto en " + level.name);
            if (sketch == null) return;
            LevelSketchFile.Apply(sketch, level);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        }

        [MenuItem(Root + "Exportar nivel abierto a boceto...", priority = 22)]
        private static void ExportOpenLevel()
        {
            var level = FindOpenLevel();
            if (level == null) return;
            string sceneName = level.gameObject.scene.name;
            string file = EditorUtility.SaveFilePanel("Exportar nivel a boceto", LastFolder, sceneName + ".levelsketch", "json");
            if (string.IsNullOrEmpty(file)) return;
            LastFolder = Path.GetDirectoryName(file);
            var sketch = LevelSketchFile.FromLevel(level, sceneName);
            File.WriteAllText(file, JsonUtility.ToJson(sketch, true));
            Debug.Log($"[EditorNiveles] {level.name} → {file}");
        }

        [MenuItem(Root + "Actualizar biomas del editor web", priority = 40)]
        private static void UpdateWebData() => EditorNivelesWebData.Write();

        // ------------------------------------------------------------------ montaje (antes Tools ▸ RedMagic ▸ Niveles)

        [MenuItem(Root + "Montaje/Preparar bioma de hielo", priority = 60)]
        private static void SetupIce() => LevelSetup.EnsureIceBiome();

        [MenuItem(Root + "Montaje/Crear tile y paleta de terreno", priority = 61)]
        private static void SetupTiles() => LevelSetup.EnsureTileAndPalette();

        [MenuItem(Root + "Montaje/Importar fondo de parallax (.json)...", priority = 62)]
        private static void SetupBackground() => LevelSetup.ImportBackgroundMenu();

        [MenuItem(Root + "Montaje/Crear nieve", priority = 63)]
        private static void SetupSnow() => LevelSetup.EnsureSnow(true);

        // ------------------------------------------------------------------ utilidades

        private static LevelSketchFile.Sketch PickSketch(string title)
        {
            string file = EditorUtility.OpenFilePanelWithFilters(title, LastFolder, new[] { "Boceto de nivel", "json" });
            if (string.IsNullOrEmpty(file)) return null;
            LastFolder = Path.GetDirectoryName(file);
            var sketch = LevelSketchFile.Parse(File.ReadAllText(file), out string error);
            if (sketch == null)
            {
                EditorUtility.DisplayDialog(title, error, "Vale");
                return null;
            }
            if (string.IsNullOrEmpty(sketch.name)) sketch.name = Path.GetFileNameWithoutExtension(file).Replace(".levelsketch", "");
            return sketch;
        }

        /// <summary>El nivel seleccionado, o el único de la escena abierta.</summary>
        private static Level FindOpenLevel()
        {
            var level = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Level>() : null;
            if (level == null)
            {
                var all = Object.FindObjectsByType<Level>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (all.Length == 1) level = all[0];
                else if (all.Length > 1)
                {
                    EditorUtility.DisplayDialog("EditorNiveles", "Hay varios niveles abiertos: selecciona uno en la jerarquía.", "Vale");
                    return null;
                }
            }
            if (level == null || level.Terrain == null)
            {
                EditorUtility.DisplayDialog("EditorNiveles", "No hay ningún Nivel (con su Tilemap de terreno) en la escena abierta. " +
                                                             "Usa 'Nuevo nivel desde boceto...'.", "Vale");
                return null;
            }
            return level;
        }

        /// <summary>
        /// Escena de nivel completa: cámara que sigue al jugador, nivel (Grid/Terreno + Grid/Plataformas, bioma, fondo,
        /// partículas), luz global, postproceso, y entrada y salida en los extremos sobre el suelo (para moverlas a mano).
        /// </summary>
        public static void CreateLevelScene(LevelSketchFile.Sketch sketch, string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var biome = LevelSketchFile.FindBiome(sketch.biome) ?? LevelSetup.EnsureIceBiome();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10f; // el que fuerza RunManager
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(7, 20, 38, 255);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraFollow>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var levelGo = new GameObject("Nivel");
            var level = levelGo.AddComponent<Level>();
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(levelGo.transform, false);
            level.SetTerrain(NewTilemap("Terreno", grid.transform));
            level.SetPlatforms(NewTilemap("Plataformas", grid.transform));
            level.SetBiome(biome);
            if (biome.background != null) PrefabUtility.InstantiatePrefab(biome.background, levelGo.transform).name = "Fondo";
            if (biome.particles != null) PrefabUtility.InstantiatePrefab(biome.particles, levelGo.transform);
            LevelSketchFile.Apply(sketch, level);

            var light = new GameObject("Luz global").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            var volume = new GameObject("Postproceso").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = LevelSetup.EnsurePostProfile();

            int entryX = 3, exitX = level.Width - 4;
            var entry = new GameObject("SectionEntry").AddComponent<SectionEntry>();
            entry.transform.position = new Vector3(entryX + 0.5f, level.GroundTop(entryX) + 1f, 0f);
            var exitGo = new GameObject("SectionExit");
            var box = exitGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2f, 4f);
            exitGo.AddComponent<SectionExit>();
            exitGo.transform.position = new Vector3(exitX + 0.5f, level.GroundTop(exitX) + 2f, 0f);

            camGo.transform.position = new Vector3(entry.transform.position.x, entry.transform.position.y, -10f);
            Selection.activeGameObject = levelGo;
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[EditorNiveles] Escena nueva: {path}. Añádela al mundo (WorldDefinition) para jugarla en la run.");
        }

        private static Tilemap NewTilemap(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().enabled = false; // lo que se ve lo dibuja el Nivel
            return go.GetComponent<Tilemap>();
        }
    }
}
