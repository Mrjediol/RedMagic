using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RedMagic.Levels;
using RedMagic.LevelsEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RedMagic.EditorNiveles
{
    /// <summary>
    /// Menú <b>EditorNiveles ▸ Importar fondo...</b>: el único importador de fondos de parallax. Lee el
    /// <c>*.parallax.json</c> que exporta la pestaña 🏞️ Fondos de la web y deja el prefab <c>Fondo_&lt;nombre&gt;</c> con
    /// una <see cref="ParallaxBackgroundLayer"/> por capa (imágenes <c>capa1…</c> junto a él).
    /// Si ese prefab ya existe se actualiza en su sitio (mismo GUID, las escenas que lo usan cambian solas) y cada capa
    /// se busca por nombre: lo que cuelgue de ella (los escalones de la puerta) se conserva.
    /// Después ofrece ponerlo como fondo del bioma y/o en el nivel abierto (sustituyendo el que tenga).
    /// </summary>
    public static class ParallaxImporter
    {
        private const string LastFolderKey = "EditorNiveles.LastBackgroundFolder";
        private const string Prefix = "Fondo_";

        [Serializable] private class FileJson { public string format; public int version; public string name; public LayerJson[] layers; }

        [Serializable]
        private class LayerJson
        {
            public string name, image, repeat, anchor, align;
            public float speed, positionX, size, heightOffset, travel, verticalSpeed, opacity, fade;
            public Color fogColor;
            public bool inFront, overTerrain, sitOnGround;
        }

        [MenuItem("EditorNiveles/Importar fondo...")]
        private static void Import()
        {
            string start = EditorPrefs.GetString(LastFolderKey, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            string path = EditorUtility.OpenFilePanelWithFilters("Importar fondo", start, new[] { "Fondo de parallax", "json" });
            if (string.IsNullOrEmpty(path)) return;
            EditorPrefs.SetString(LastFolderKey, Path.GetDirectoryName(path));

            FileJson file;
            try { file = JsonUtility.FromJson<FileJson>(File.ReadAllText(path)); }
            catch (Exception e) { EditorUtility.DisplayDialog("Importar fondo", "JSON no válido: " + e.Message, "Vale"); return; }
            if (file?.layers == null || file.layers.Length == 0 || file.format != "redmagic-parallax")
            {
                EditorUtility.DisplayDialog("Importar fondo", "No es un fondo exportado desde 🏞️ Fondos (*.parallax.json).", "Vale");
                return;
            }

            var level = OpenLevel();
            var biome = level != null && level.Biome != null ? level.Biome : FirstBiome();
            var prefab = Build(file, biome);
            if (prefab == null) return;
            EditorNivelesWebData.Write();
            EditorGUIUtility.PingObject(prefab);

            string biomeName = biome != null ? biome.name : "(sin bioma)";
            int choice = level != null
                ? EditorUtility.DisplayDialogComplex("Fondo importado", $"'{prefab.name}' listo. ¿Dónde lo pongo?",
                    $"Bioma {biomeName} y nivel abierto", "Sólo crearlo", "Sólo en el nivel abierto")
                : EditorUtility.DisplayDialog("Fondo importado", $"'{prefab.name}' listo. ¿Lo pongo como fondo del bioma {biomeName}?",
                    $"Sí, en el bioma {biomeName}", "Sólo crearlo") ? 0 : 1;
            if (choice == 1) return;
            if (choice == 0 && biome != null)
            {
                Undo.RecordObject(biome, "Fondo del bioma");
                biome.background = prefab;
                EditorUtility.SetDirty(biome);
                AssetDatabase.SaveAssets();
            }
            if (level != null) ReplaceBackground(level, prefab);
        }

        /// <summary>Prefab <c>Fondo_&lt;nombre&gt;</c> ya importado (en cualquier bioma), o null.</summary>
        public static GameObject FindBackground(string name)
        {
            string target = Prefix + SafeName(name);
            return AssetDatabase.FindAssets($"{target} t:Prefab", new[] { LevelSetup.BiomesRoot })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(p => p != null && p.name == target);
        }

        /// <summary>Pone <paramref name="prefab"/> como fondo del nivel, quitando el que tuviera (con Undo).</summary>
        public static void ReplaceBackground(Level level, GameObject prefab)
        {
            foreach (Transform child in level.transform.Cast<Transform>().ToArray())
            {
                if (child.GetComponentInChildren<ParallaxBackgroundLayer>(true) == null) continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == prefab) return; // ya es ese
                Undo.DestroyObjectImmediate(child.gameObject);
            }
            var bg = (GameObject)PrefabUtility.InstantiatePrefab(prefab, level.transform);
            bg.name = "Fondo";
            Undo.RegisterCreatedObjectUndo(bg, "Cambiar fondo");
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        }

        private static GameObject Build(FileJson file, LevelBiome biome)
        {
            string safe = SafeName(file.name);
            var existing = FindBackground(file.name);
            string prefabPath, folder;
            if (existing != null)
            {
                prefabPath = AssetDatabase.GetAssetPath(existing);
                folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            }
            else
            {
                string root = biome != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(biome)).Replace('\\', '/') : LevelSetup.BiomesRoot;
                folder = $"{root}/Fondos/{safe}";
                prefabPath = $"{folder}/{Prefix}{safe}.prefab";
            }
            Directory.CreateDirectory(folder);

            // imágenes: capa1, capa2… (los mismos nombres al reimportar: mismo GUID)
            var textures = new Texture2D[file.layers.Length];
            for (int i = 0; i < file.layers.Length; i++)
                textures[i] = SaveImage(file.layers[i].image, $"{folder}/capa{i + 1}");

            var root2 = existing != null ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(Prefix + safe);
            try
            {
                var shader = Shader.Find("RedMagic/Parallax Layer");
                var keep = file.layers.Select(l => l.name).ToHashSet();
                foreach (var old in root2.GetComponentsInChildren<ParallaxBackgroundLayer>(true))
                    if (old.transform.parent == root2.transform && !keep.Contains(old.name)) UnityEngine.Object.DestroyImmediate(old.gameObject);

                for (int i = 0; i < file.layers.Length; i++)
                {
                    var j = file.layers[i];
                    var t = root2.transform.Find(j.name);
                    if (t == null) { t = new GameObject(j.name).transform; t.SetParent(root2.transform, false); }
                    t.SetSiblingIndex(i);
                    var layer = t.GetComponent<ParallaxBackgroundLayer>();
                    if (layer == null) layer = t.gameObject.AddComponent<ParallaxBackgroundLayer>();
                    layer.Configure(textures[i], shader, j.speed, Parse<ParallaxRepeat>(j.repeat), j.positionX, j.size, j.heightOffset,
                        Parse<ParallaxVerticalAnchor>(j.anchor), Parse<ParallaxAlign>(j.align), j.travel, j.verticalSpeed,
                        j.opacity, j.fade, j.inFront, i);
                    var so = new SerializedObject(layer);
                    so.FindProperty("fogColor").colorValue = j.fogColor;
                    so.FindProperty("overTerrain").boolValue = j.overTerrain;
                    so.FindProperty("sitOnGround").boolValue = j.sitOnGround;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(layer);
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(root2, prefabPath);
                Debug.Log($"[EditorNiveles] Fondo '{file.name}': {file.layers.Length} capas → {prefabPath}{(existing != null ? " (actualizado)" : "")}");
                return saved;
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root2);
                else UnityEngine.Object.DestroyImmediate(root2);
            }
        }

        /// <summary>Escribe la imagen de un data URL (png/jpg) e importa la textura. Quita la de la otra extensión.</summary>
        private static Texture2D SaveImage(string dataUrl, string basePath)
        {
            if (string.IsNullOrEmpty(dataUrl)) return null;
            var m = Regex.Match(dataUrl, @"^data:image/(png|jpe?g);base64,(.+)$", RegexOptions.Singleline);
            if (!m.Success) { Debug.LogWarning($"[EditorNiveles] Imagen no válida para {basePath}"); return null; }
            string ext = m.Groups[1].Value == "png" ? ".png" : ".jpg", other = ext == ".png" ? ".jpg" : ".png";
            if (File.Exists(basePath + other)) AssetDatabase.DeleteAsset(basePath + other);
            string path = basePath + ext;
            File.WriteAllBytes(path, Convert.FromBase64String(m.Groups[2].Value));
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 8192;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static T Parse<T>(string v) where T : struct => Enum.TryParse<T>(v, out var r) ? r : default;

        private static string SafeName(string n) =>
            Regex.Replace(string.IsNullOrWhiteSpace(n) ? "Fondo" : n.Trim(), @"[\\/:*?""<>|.]+", "_");

        private static Level OpenLevel()
        {
            var sel = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Level>() : null;
            if (sel != null) return sel;
            var all = UnityEngine.Object.FindObjectsByType<Level>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.Length == 1 ? all[0] : null;
        }

        private static LevelBiome FirstBiome() =>
            AssetDatabase.FindAssets("t:LevelBiome").Select(g => AssetDatabase.LoadAssetAtPath<LevelBiome>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(b => b != null);
    }
}
