using System.IO;
using System.Linq;
using RedMagic.Gameplay;
using RedMagic.Levels;
using RedMagic.LevelsEditor;
using RedMagic.Run;
using RedMagic.Run.EditorTools;
using RedMagic.RunEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace RedMagic.EditorNiveles
{
    /// <summary>
    /// Menú propio <b>EditorNiveles ▸ Importar nivel...</b> (en la barra de menús, aparte de Tools): lee un
    /// <c>*.levelsketch.json</c> del editor web y lo vuelca en el nivel de la escena abierta o en una escena nueva.
    /// Reimportar sólo toca tiles, tamaño y bioma; lo añadido a mano en la escena se queda. Al importar también se
    /// refresca <c>datos-unity.json</c> (valores del bioma para la vista previa web).
    /// </summary>
    public static class EditorNivelesMenu
    {
        private const string LastFolderKey = "EditorNiveles.LastFolder";

        private static string LastFolder
        {
            get => EditorPrefs.GetString(LastFolderKey, Path.GetFullPath(Path.Combine(Application.dataPath, "Levels", "Bocetos")));
            set => EditorPrefs.SetString(LastFolderKey, value);
        }

        [MenuItem("EditorNiveles/Importar nivel...")]
        private static void Import()
        {
            var sketch = PickSketch();
            if (sketch == null) return;
            EditorNivelesWebData.Write(log: true);

            var level = FindOpenLevel();
            if (level != null)
            {
                int choice = EditorUtility.DisplayDialogComplex("Importar nivel",
                    $"¿Dónde va '{sketch.name}'?\n\nEn el nivel abierto se cambian sus tiles y su tamaño; lo demás de la escena se queda.",
                    $"En el nivel abierto ({level.gameObject.scene.name})", "Cancelar", "En una escena nueva");
                if (choice == 1) return;
                if (choice == 0)
                {
                    LevelSketchFile.Apply(sketch, level);
                    EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
                    return;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path = EditorUtility.SaveFilePanelInProject("Escena del nivel", sketch.name, "unity",
                "Dónde guardar la escena del nivel nuevo.", "Assets/Scenes");
            if (!string.IsNullOrEmpty(path)) CreateLevelScene(sketch, path);
        }

        private static LevelSketchFile.Sketch PickSketch()
        {
            string file = EditorUtility.OpenFilePanelWithFilters("Importar nivel", LastFolder, new[] { "Boceto de nivel", "json" });
            if (string.IsNullOrEmpty(file)) return null;
            LastFolder = Path.GetDirectoryName(file);
            var sketch = LevelSketchFile.Parse(File.ReadAllText(file), out string error);
            if (sketch == null)
            {
                EditorUtility.DisplayDialog("Importar nivel", error, "Vale");
                return null;
            }
            if (string.IsNullOrEmpty(sketch.name)) sketch.name = Path.GetFileNameWithoutExtension(file).Replace(".levelsketch", "");
            return sketch;
        }

        /// <summary>El nivel seleccionado, o el único de la escena abierta (null si no hay uno claro).</summary>
        private static Level FindOpenLevel()
        {
            var level = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Level>() : null;
            if (level == null)
            {
                var all = Object.FindObjectsByType<Level>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (all.Length == 1) level = all[0];
            }
            return level != null && level.Terrain != null ? level : null;
        }

        /// <summary>
        /// Escena de nivel completa: cámara que sigue al jugador, nivel (Grid/Terreno + Plataformas + PlataformasArte +
        /// Puertas, bioma, fondo, partículas), luz global, postproceso, entrada y salida en los extremos sobre el suelo
        /// (para moverlas a mano) y un WaveManager vacío. Pausa, HUD y jugador no van en la escena: son persistentes.
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

            // Vacío pero listo: una oleada sin enemigos que termina el nivel (la salida abre). Se rellena a mano.
            // En el centro del nivel sobre el suelo, para que sus puntos de aparición no caigan dentro del marco de roca.
            var waves = WaveManagerEditor.CreateDefault();
            int midX = level.Width / 2;
            waves.transform.position = new Vector3(midX + 0.5f, level.GroundTop(midX) + 1f, 0f);

            camGo.transform.position = new Vector3(entry.transform.position.x, entry.transform.position.y, -10f);
            Selection.activeGameObject = levelGo;
            EditorSceneManager.SaveScene(scene, path);
            WorldSceneGenerator.AddToBuildSettings(path);
            var world = OfferRunPool(path);
            Debug.Log($"[EditorNiveles] Escena nueva: {path} (en Build Settings, con WaveManager)" +
                      (world != null ? $", en el pool de {world.name}." : ". Añádela al pool de su WorldDefinition para que salga en la run."));
        }

        /// <summary>
        /// Pregunta si la escena entra ya en las runs: la añade al pool de secciones del primer mundo desbloqueado
        /// (por referencia a la escena, como lo hace el WorldSceneGenerator). Devuelve el mundo o null.
        /// </summary>
        private static WorldDefinition OfferRunPool(string scenePath)
        {
            var world = AssetDatabase.FindAssets("t:WorldDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<WorldDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null && w.Unlocked)
                .OrderBy(w => w.WorldNumber)
                .FirstOrDefault();
            if (world == null) return null;
            if (world.SectionPool.Any(s => s != null && s.Path == scenePath)) return world;
            if (!EditorUtility.DisplayDialog("Importar nivel",
                    $"¿Meter el nivel en las runs?\n\nSe añade al pool de secciones de '{world.DisplayName}' ({world.name}); " +
                    "si no, se queda sólo en Build Settings y se añade a mano.", "Sí, en la run", "No")) return null;

            var so = new SerializedObject(world);
            var pool = so.FindProperty("sectionPool");
            pool.arraySize++;
            WorldSceneGenerator.AssignScene(pool.GetArrayElementAtIndex(pool.arraySize - 1), scenePath);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(world);
            AssetDatabase.SaveAssets();
            return world;
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
