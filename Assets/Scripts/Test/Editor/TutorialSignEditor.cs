using System.Collections.Generic;
using System.IO;
using System.Linq;
using RedMagic.Levels;
using RedMagic.Localization;
using RedMagic.Localization.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RedMagic.LevelsEditor
{
    /// <summary>
    /// Inspector de <see cref="TutorialSign"/>: el texto en español y en inglés se escribe aquí y se guarda en
    /// <c>Assets/Resources/Localization/es.txt</c> y <c>en.txt</c> con la clave del cartel (regla del proyecto: ningún texto
    /// de jugador escrito a mano en la escena). Un cartel duplicado comparte clave con el original: al cambiarle el texto
    /// se le da una clave nueva antes de guardar, para no pisar al otro.
    /// <b>GameObject ▸ RedMagic ▸ Texto de tutorial</b> lo crea con la fuente Cinzel y el material con contorno y sombra
    /// (los genera la primera vez, y si hace falta importa los recursos básicos de TextMesh Pro).
    /// </summary>
    [CustomEditor(typeof(TutorialSign))]
    public class TutorialSignEditor : Editor
    {
        private const string LanguageFolder = "Assets/Resources/Localization";
        private const string FontFile = "Assets/Ui/Fonts/Cinzel/Cinzel-Bold.ttf";
        private const string FontAssetPath = "Assets/Ui/Fonts/Cinzel/Cinzel-Bold SDF.asset";
        private const string MaterialPath = "Assets/Ui/Fonts/Cinzel/Cinzel-Bold SDF - Cartel.mat";
        private const string SectionHeader = "# --- carteles del tutorial (TutorialSign; se editan desde el Inspector del cartel)";
        private const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "¡¿ÁÉÍÓÚÜÑáéíóúüñ·«»–—…→←↑↓";

        private string _es, _en;
        private string _loadedKey;

        public override void OnInspectorGUI()
        {
            var sign = (TutorialSign)target;
            serializedObject.Update();
            if (_loadedKey != sign.Key) LoadTexts(sign.Key);

            EditorGUILayout.HelpBox("Escribe el texto en los dos idiomas: se guarda en es.txt / en.txt al salir del campo. " +
                                    "\\n o Intro = salto de línea. Tamaño, color y alineación: en el TextMeshPro de abajo.", MessageType.None);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Clave", sign.Key);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.LabelField("Español");
            var es = EditorGUILayout.TextArea(_es ?? "", GUILayout.MinHeight(40));
            EditorGUILayout.LabelField("English");
            var en = EditorGUILayout.TextArea(_en ?? "", GUILayout.MinHeight(40));
            if (es != _es || en != _en)
            {
                _es = es;
                _en = en;
                Save(sign);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeWidth"));
            serializedObject.ApplyModifiedProperties();
        }

        private void LoadTexts(string key)
        {
            _loadedKey = key;
            _es = string.IsNullOrEmpty(key) ? "" : ReadEntry("es", key) ?? "";
            _en = string.IsNullOrEmpty(key) ? "" : ReadEntry("en", key) ?? "";
        }

        private void Save(TutorialSign sign)
        {
            // un duplicado (otro cartel con la misma clave): se separa con una clave propia antes de escribir
            bool shared = !string.IsNullOrEmpty(sign.Key) && Object.FindObjectsByType<TutorialSign>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(s => s != sign && s.Key == sign.Key);
            if (string.IsNullOrEmpty(sign.Key) || shared)
            {
                Undo.RecordObject(sign, "Clave del cartel");
                sign.Key = NewKey(sign.gameObject.scene.name);
                EditorUtility.SetDirty(sign);
            }
            WriteEntry("es", sign.Key, _es);
            WriteEntry("en", sign.Key, _en);
            _loadedKey = sign.Key;
            Loc.Reload();
            foreach (var s in Object.FindObjectsByType<TutorialSign>(FindObjectsInactive.Include, FindObjectsSortMode.None)) s.Refresh();
            SceneView.RepaintAll();
        }

        // ------------------------------------------------------------------ ficheros de idioma

        private static string PathFor(string code) => $"{LanguageFolder}/{code}.txt";

        private static string ReadEntry(string code, string key)
        {
            var path = PathFor(code);
            if (!File.Exists(path)) return null;
            foreach (var raw in File.ReadAllLines(path))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0 || raw.TrimStart().StartsWith("#")) continue;
                if (raw.Substring(0, eq).Trim() == key) return raw.Substring(eq + 1).Trim().Replace("\\n", "\n");
            }
            return null;
        }

        private static void WriteEntry(string code, string key, string value)
        {
            var path = PathFor(code);
            if (!File.Exists(path)) return;
            string text = File.ReadAllText(path);
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            string line = $"{key} = {(value ?? "").Replace("\r", "").Replace("\n", "\\n").Trim()}";
            int at = lines.FindIndex(l => { int eq = l.IndexOf('='); return eq > 0 && !l.TrimStart().StartsWith("#") && l.Substring(0, eq).Trim() == key; });
            if (at >= 0) lines[at] = line;
            else
            {
                while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
                if (!lines.Contains(SectionHeader)) { lines.Add(""); lines.Add(SectionHeader); }
                lines.Add(line);
                lines.Add("");
            }
            File.WriteAllText(path, string.Join(nl, lines));
            AssetDatabase.ImportAsset(path);
        }

        private static string NewKey(string sceneName)
        {
            string prefix = "tutorial." + LocalizationTools.Slug(string.IsNullOrEmpty(sceneName) ? "nivel" : sceneName) + ".";
            var used = new HashSet<string>(Object.FindObjectsByType<TutorialSign>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(s => s.Key));
            for (int i = 1; ; i++)
            {
                string k = prefix + i;
                if (!used.Contains(k) && ReadEntry("es", k) == null) return k;
            }
        }

        // ------------------------------------------------------------------ crear

        [MenuItem("GameObject/RedMagic/Texto de tutorial", false, 12)]
        private static void Create(MenuCommand command)
        {
            var font = EnsureFont(out var material);
            if (font == null) return;

            var go = new GameObject("Cartel tutorial");
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            if (command.context == null && SceneView.lastActiveSceneView != null)
            {
                var p = SceneView.lastActiveSceneView.pivot;
                go.transform.position = new Vector3(p.x, p.y, 0f);
            }
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.fontSharedMaterial = material;
            tmp.fontSize = 6.5f;   // ≈0,9 u de línea: legible con la cámara del juego (20 u de alto)
            tmp.color = new Color(0.9f, 0.97f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = new Vector2(14f, 4f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sortingLayerName = LevelSorting.Layer;
            renderer.sortingOrder = LevelSorting.OverTerrain + 10; // delante del nivel, detrás de jugador y enemigos

            var sign = go.AddComponent<TutorialSign>();
            sign.Key = NewKey(go.scene.name);
            WriteEntry("es", sign.Key, "Escribe aquí el texto");
            WriteEntry("en", sign.Key, "Write the text here");
            Loc.Reload();
            sign.Refresh();

            Undo.RegisterCreatedObjectUndo(go, "Crear texto de tutorial");
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
        }

        /// <summary>La fuente Cinzel Bold como TMP_FontAsset dinámico y su material de cartel (contorno oscuro + sombra).</summary>
        public static TMP_FontAsset EnsureFont(out Material material)
        {
            material = null;
            if (Shader.Find("TextMeshPro/Distance Field") == null)
            {
                // los shaders de TMP vienen en sus recursos básicos, que este proyecto no tenía importados
                var package = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
                if (package == null) { Debug.LogError("No encuentro 'TMP Essential Resources.unitypackage' en Library/PackageCache."); return null; }
                AssetDatabase.ImportPackage(package, false);
                AssetDatabase.Refresh();
                if (Shader.Find("TextMeshPro/Distance Field") == null)
                {
                    Debug.LogWarning("Importados los recursos de TextMesh Pro: vuelve a usar el menú cuando termine de compilar/importar.");
                    return null;
                }
            }

            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fa == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(FontFile);
                if (source == null) { Debug.LogError("Falta la fuente " + FontFile); return null; }
                fa = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                fa.name = Path.GetFileNameWithoutExtension(FontAssetPath);
                AssetDatabase.CreateAsset(fa, FontAssetPath);
                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                fa.atlasTextures[0].name = fa.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.TryAddCharacters(Characters, out _);
                EditorUtility.SetDirty(fa);
                AssetDatabase.SaveAssets();
            }

            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(fa.material) { name = Path.GetFileNameWithoutExtension(MaterialPath) };
                material.SetColor("_FaceColor", Color.white);
                material.SetFloat("_OutlineWidth", 0.22f);
                material.SetColor("_OutlineColor", new Color(0.02f, 0.07f, 0.16f, 1f));
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor", new Color(0f, 0.02f, 0.06f, 0.65f));
                material.SetFloat("_UnderlayOffsetX", 0.35f);
                material.SetFloat("_UnderlayOffsetY", -0.45f);
                material.SetFloat("_UnderlaySoftness", 0.35f);
                material.SetFloat("_UnderlayDilate", 0.2f);
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SaveAssets();
            }
            return fa;
        }
    }
}
