using System.IO;
using System.Linq;
using RedMagic.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Hornea el aspecto de cada <see cref="Level"/> al guardar su escena: las mallas, materiales y textura de brillo que
    /// el nivel acaba de generar se guardan tal cual en <c>Assets/Levels/Horneado/&lt;escena&gt;_&lt;nivel&gt;.asset</c> (+ el
    /// PNG del brillo), y el nivel los usa al cargar en vez de recalcularlos (en LargeLevelTest eran ~240 ms por carga).
    /// Sólo se rehornea si cambió algo (<see cref="Level.Signature"/>); si no, guardar no toca nada.
    /// <b>Tools ▸ RedMagic ▸ Niveles ▸ Hornear todas las escenas de nivel</b> lo hace de golpe con las de <c>Assets/Scenes</c>.
    /// </summary>
    [InitializeOnLoad]
    public static class LevelBaker
    {
        private const string Folder = "Assets/Levels/Horneado";

        static LevelBaker()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (Application.isPlaying) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var level in root.GetComponentsInChildren<Level>(true))
                    BakeLevel(level, scene.name);
        }

        /// <summary>Hornea <paramref name="level"/> si su horneado falta o no cuadra. Devuelve true si escribió algo.</summary>
        public static bool BakeLevel(Level level, string sceneName, bool force = false)
        {
            if (level == null || level.Biome == null) return false;
            string signature = level.Signature();
            var bake = level.Bake;
            if (!force && bake != null && bake.IsComplete && bake.signature == signature) return false;

            level.Rebuild(); // lo que se hornea es exactamente lo que se genera
            if (level.RockMesh == null || level.RimMesh == null || level.GlowMesh == null || level.GlowTexture == null) return false;

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Levels", "Horneado");
            string assetPath = bake != null ? AssetDatabase.GetAssetPath(bake) : null;
            if (string.IsNullOrEmpty(assetPath)) assetPath = $"{Folder}/{Sanitize(sceneName + "_" + level.name)}.asset";
            if (bake == null) bake = AssetDatabase.LoadAssetAtPath<LevelBake>(assetPath);
            if (bake == null)
            {
                bake = ScriptableObject.CreateInstance<LevelBake>();
                AssetDatabase.CreateAsset(bake, assetPath);
            }

            var glowTexture = WriteGlowPng(level.GlowTexture, assetPath.Substring(0, assetPath.Length - ".asset".Length) + "_brillo.png");

            bake.rock = Store(bake, bake.rock, level.RockMesh, "Roca");
            bake.rim = Store(bake, bake.rim, level.RimMesh, "Borde");
            bake.glow = Store(bake, bake.glow, level.GlowMesh, "Brillo");
            bake.rockMaterial = Store(bake, bake.rockMaterial, level.RockMaterial, "Roca (material)");
            bake.rimMaterial = Store(bake, bake.rimMaterial, level.RimMaterial, "Borde (material)");
            bake.glowMaterial = Store(bake, bake.glowMaterial, level.GlowMaterial, "Brillo (material)");
            bake.glowMaterial.mainTexture = glowTexture;
            bake.glowTexture = glowTexture;
            bake.signature = signature;
            EditorUtility.SetDirty(bake);
            AssetDatabase.SaveAssets();

            level.SetBake(bake);
            EditorUtility.SetDirty(level);
            foreach (var r in level.GetComponentsInChildren<Renderer>(true)) EditorUtility.SetDirty(r);
            foreach (var f in level.GetComponentsInChildren<MeshFilter>(true)) EditorUtility.SetDirty(f);
            Debug.Log($"[Niveles] Horneado '{level.name}' ({sceneName}) → {assetPath}", bake);
            return true;
        }

        /// <summary>Copia <paramref name="source"/> en el sub-asset (lo crea la primera vez; luego lo sobrescribe en su sitio).</summary>
        private static T Store<T>(LevelBake main, T existing, T source, string name) where T : Object
        {
            T target = existing;
            if (target == null)
            {
                target = Object.Instantiate(source);
                target.hideFlags = HideFlags.None;
                target.name = name;
                AssetDatabase.AddObjectToAsset(target, main);
            }
            else
            {
                EditorUtility.CopySerialized(source, target);
            }
            target.hideFlags = HideFlags.None;   // la copia trae el DontSave de lo generado: sin esto no se guardaría
            target.name = name;
            EditorUtility.SetDirty(target);
            return target;
        }

        /// <summary>El brillo como PNG importado sin compresión, sin mipmaps ni reescalado: los mismos píxeles que se generan.</summary>
        private static Texture2D WriteGlowPng(Texture2D source, string path)
        {
            File.WriteAllBytes(path, source.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Default || importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize < 8192 ||
                importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear || importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 8192;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = false;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static string Sanitize(string s) =>
            new string(s.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());

        /// <summary>Hornea todas las escenas de <c>Assets/Scenes</c> que tengan un nivel (abre las cerradas, las guarda y las cierra).</summary>
        [MenuItem("Tools/RedMagic/Niveles/Hornear todas las escenas de nivel")]
        public static void BakeAllScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int baked = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = SceneManager.GetSceneByPath(path);
                bool wasOpen = scene.IsValid() && scene.isLoaded;
                if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                bool any = false;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var level in root.GetComponentsInChildren<Level>(true))
                        any |= BakeLevel(level, scene.name);
                if (any) { EditorSceneManager.SaveScene(scene); baked++; }
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log($"[Niveles] Escenas horneadas: {baked}.");
        }
    }
}
