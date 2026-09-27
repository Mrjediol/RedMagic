// WebLibraryUtilities.cs
// -----------------------------------------------------------------------------
// The scene utilities behind the Biblioteca Web bottom bar (WebLibraryWindow draws the buttons and
// their drag-to-reorder; this file only does the work):
//
//   · Cargar MainHub      — opens the hub scene. Resolved from RunManager.hubScene (the same
//                           SceneReference the game uses), by GUID — never by scene name.
//   · Añadir Player       — drops a Player.prefab instance into the active scene for solo testing,
//                           at its SectionEntry and with the PlayerScaleConfig scale RunManager
//                           would give it. Selects the existing one instead of duplicating.
//   · Quitar Player …     — deletes every placed Player from every scene under Assets/Scenes. In a
//                           real run the player is ALWAYS spawned by RunManager (playerPrefab), so a
//                           placed one left over from testing only shows up as a duplicate.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RedMagic.Gameplay;
using RedMagic.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class WebLibraryUtilities
{
    private const string Title = "Biblioteca Web";
    private const string ScenesFolder = "Assets/Scenes";

    // ============================================================ Cargar MainHub

    public static void OpenHub()
    {
        if (RefuseInPlayMode()) return;

        string path = FindHubScenePath();
        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog(Title, "No se encontró la escena del hub: ningún RunManager de " +
                                               $"{ScenesFolder}/ tiene 'Hub Scene' asignada.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    /// <summary>RunManager.hubScene of an open scene if there is one; otherwise the same field read
    /// out of any scene file under Assets/Scenes (RunManager is placed in every playable scene).
    /// Either way it's the asset GUID that's followed, so renaming the hub doesn't break it.</summary>
    private static string FindHubScenePath()
    {
        var runManager = Object.FindAnyObjectByType<RunManager>(FindObjectsInactive.Include);
        if (runManager != null)
        {
            var asset = new SerializedObject(runManager).FindProperty("hubScene.sceneAsset")?.objectReferenceValue;
            if (asset != null) return AssetDatabase.GetAssetPath(asset);
        }

        var pattern = new Regex(@"hubScene:\s*\n\s*sceneAsset: \{fileID: \d+, guid: ([0-9a-f]{32})");
        foreach (string scenePath in ScenePaths())
        {
            var match = pattern.Match(File.ReadAllText(scenePath));
            if (!match.Success) continue;
            string path = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
            if (!string.IsNullOrEmpty(path)) return path;
        }
        return null;
    }

    // ============================================================ Añadir Player

    public static void AddPlayer(string prefabPath)
    {
        if (RefuseInPlayMode()) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { EditorUtility.DisplayDialog(Title, $"No se encontró '{prefabPath}'.", "OK"); return; }

        Scene scene = SceneManager.GetActiveScene();
        var existing = FindPlayers(scene, prefabPath, out _);
        if (existing.Count > 0)
        {
            Selection.activeGameObject = existing[0];
            EditorGUIUtility.PingObject(existing[0]);
            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("Ya hay un Player en la escena"));
            return;
        }

        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(player, "Añadir Player");

        var entry = SectionEntry.FindIn(scene);
        if (entry != null) player.transform.position = entry.SpawnPosition;
        else if (SceneView.lastActiveSceneView != null)
        {
            Vector3 pivot = SceneView.lastActiveSceneView.pivot;
            player.transform.position = new Vector3(pivot.x, pivot.y, 0f);
        }

        // Misma escala que le pondría RunManager.ApplyPlayerScale al entrar en esta escena.
        var scaleConfig = Resources.Load<PlayerScaleConfig>("PlayerScaleConfig");
        if (scaleConfig != null)
        {
            float scale = scaleConfig.ScaleFor(scene);
            player.transform.localScale = new Vector3(scale, scale, scale);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = player;
    }

    // ============================================================ Quitar Player de todas las escenas

    public static void RemovePlayersFromAllScenes(string prefabPath)
    {
        if (RefuseInPlayMode()) return;

        if (!EditorUtility.DisplayDialog(Title,
                $"Borra todo Player colocado a mano en las escenas de {ScenesFolder}/ y guarda las que cambien.\n\n" +
                "En una run el jugador siempre lo crea el RunManager, así que no se pierde nada.",
                "Quitar", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var removed = new List<string>();
        var skipped = new List<string>();
        string[] paths = ScenePaths().ToArray();

        // Una escena cada vez (Single), nunca varias a la vez en aditivo: cada escena trae su Global
        // Light 2D, y dos cargadas juntas hacen saltar "More than one global light on layer …".
        // Al terminar se vuelve a dejar abierto lo que el usuario tenía.
        var setup = EditorSceneManager.GetSceneManagerSetup().Where(s => !string.IsNullOrEmpty(s.path)).ToArray();
        if (setup.Length > 0 && !setup.Any(s => s.isActive && s.isLoaded))
        {
            setup[0].isActive = true; // la activa era una escena sin guardar: se activa otra
            setup[0].isLoaded = true;
        }

        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                EditorUtility.DisplayProgressBar(Title, $"Revisando {Path.GetFileName(path)}", (float)i / paths.Length);

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                var players = FindPlayers(scene, prefabPath, out var nested);
                skipped.AddRange(nested.Select(n => $"{path}: {n}"));
                if (players.Count > 0)
                {
                    foreach (var p in players) Object.DestroyImmediate(p);
                    EditorSceneManager.SaveScene(scene);
                    removed.Add($"{path} ({players.Count})");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        string summary = removed.Count == 0 ? "Ninguna escena tenía un Player." : "Quitado de:\n" + string.Join("\n", removed);
        if (skipped.Count > 0)
            summary += "\n\nNo se pudo quitar (está dentro de otro prefab, hay que quitarlo de ese prefab):\n" + string.Join("\n", skipped);
        EditorUtility.DisplayDialog(Title, summary, "OK");
    }

    /// <summary>Players placed in <paramref name="scene"/>: instances of the Player prefab plus any
    /// loose object carrying PlayerMovement. A player nested inside ANOTHER prefab's instance can't
    /// be deleted from the scene (and deleting that outer instance would take the level with it), so
    /// it's reported in <paramref name="nested"/> instead.</summary>
    private static List<GameObject> FindPlayers(Scene scene, string prefabPath, out List<string> nested)
    {
        var found = new HashSet<GameObject>();
        nested = new List<string>();
        if (!scene.IsValid() || !scene.isLoaded) return found.ToList();

        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject go = t.gameObject;
            bool isPlayerInstance = PrefabUtility.IsOutermostPrefabInstanceRoot(go) &&
                                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == prefabPath;
            if (!isPlayerInstance && go.GetComponent<PlayerMovement>() == null) continue;

            if (!PrefabUtility.IsPartOfPrefabInstance(go)) { found.Add(go); continue; }

            GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(outer) == prefabPath) found.Add(outer);
            else nested.Add(go.name);
        }

        // Un Player dentro de otro Player encontrado ya se va con su padre.
        return found.Where(go => !found.Any(other => other != go && go.transform.IsChildOf(other.transform))).ToList();
    }

    // ============================================================ helpers

    private static IEnumerable<string> ScenePaths() =>
        AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder }).Select(AssetDatabase.GUIDToAssetPath);

    private static bool RefuseInPlayMode()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;
        EditorUtility.DisplayDialog(Title, "Sal de Play Mode antes de usar esta utilidad.", "OK");
        return true;
    }
}
