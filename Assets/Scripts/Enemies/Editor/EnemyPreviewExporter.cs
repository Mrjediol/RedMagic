using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Enemies.EditorTools
{
    /// <summary>
    /// The Unity side of the web's 🎨 Designs ▸ enemy library (<c>docs/enemigos/&lt;Name&gt;/enemigo.json</c>,
    /// docs/architecture/designs.md): a picture of an enemy prefab for its card, and marking an existing enemy
    /// as "with things to do" from the editor. The taller server calls <see cref="Export"/> through
    /// <c>unity command eval</c> when an enemy is added from the web.
    /// </summary>
    public static class EnemyPreviewExporter
    {
        private const int Size = 512;
        public const string LibraryRoot = "docs/enemigos";

        /// <summary>Renders the prefab, framed on its sprites, over a transparent background into <paramref name="outPng"/>.</summary>
        public static string Export(string prefabPath, string outPng)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return $"no hay prefab en {prefabPath}";

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var camGo = new GameObject("EnemyPreviewCam");
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            try
            {
                inst.hideFlags = HideFlags.HideAndDontSave;
                camGo.hideFlags = HideFlags.HideAndDontSave;
                foreach (var body in inst.GetComponentsInChildren<Rigidbody2D>()) body.simulated = false;
                inst.transform.position = new Vector3(9000f, 9000f, 0f);

                var renderers = inst.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled && r.sprite != null).ToArray();
                if (renderers.Length == 0) return "el prefab no tiene SpriteRenderer con sprite";
                var bounds = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);

                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.08f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.targetTexture = rt;
                cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 10f);
                cam.Render();

                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPng)) ?? ".");
                File.WriteAllBytes(outPng, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return $"ok → {outPng}";
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(inst);
            }
        }

        /// <summary>Library name of a prefab: its file name without the "Enemy_" / "Boss_" prefix (same rule as the server).</summary>
        public static string NameOf(string prefabPath)
        {
            var name = Path.GetFileNameWithoutExtension(prefabPath);
            foreach (var prefix in new[] { "Enemy_", "Boss_" })
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return name.Substring(prefix.Length);
            return name;
        }

        /// <summary>
        /// The enemy's record (created if missing, estado "con-pendientes"), with an optional new pendiente, and its
        /// picture refreshed. Returns the record's path.
        /// </summary>
        public static string Mark(string prefabPath, string tipo = null, string texto = null)
        {
            var name = NameOf(prefabPath);
            var dir = Path.Combine(LibraryRoot, name);
            var file = Path.Combine(dir, "enemigo.json");
            Directory.CreateDirectory(dir);
            var rec = File.Exists(file) ? JObject.Parse(File.ReadAllText(file)) : new JObject
            {
                ["name"] = name,
                ["estado"] = "con-pendientes",
                ["prefab"] = prefabPath,
                ["pendientes"] = new JArray(),
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
                ["origen"] = "unity",
            };
            if (rec["prefab"] == null) rec["prefab"] = prefabPath;
            if (!string.IsNullOrWhiteSpace(texto))
            {
                if (!(rec["pendientes"] is JArray list)) rec["pendientes"] = list = new JArray();
                list.Add(new JObject
                {
                    ["id"] = Guid.NewGuid().ToString("N").Substring(0, 7),
                    ["tipo"] = string.IsNullOrEmpty(tipo) ? "otro" : tipo,
                    ["texto"] = texto.Trim(),
                    ["estado"] = "abierto",
                    ["createdAt"] = DateTime.UtcNow.ToString("o"),
                });
                var estado = (string)rec["estado"];
                if (estado == "aprobado" || estado == "por-aprobar") rec["estado"] = "con-pendientes";
            }
            var png = Path.Combine(dir, "preview.png").Replace('\\', '/');
            if (Export(prefabPath, png).StartsWith("ok")) rec["imagen"] = $"{LibraryRoot}/{name}/preview.png";
            File.WriteAllText(file, rec.ToString() + "\n");
            return file;
        }
    }

    /// <summary>Tools ▸ RedMagic ▸ Enemigos ▸ Pendiente para la web… — note what an existing enemy still needs.</summary>
    public class EnemyPendingWindow : EditorWindow
    {
        private static readonly string[] Tipos = { "comportamiento", "sprite", "gesto", "ataque", "bug", "otro" };
        private static readonly string[] Etiquetas = { "Comportamiento", "Sprite", "Gesto", "Ataque", "Bug", "Otro" };
        private GameObject prefab;
        private int tipo;
        private string texto = "";
        private string last;

        [MenuItem("Tools/RedMagic/Enemigos/Pendiente para la web…")]
        private static void Open()
        {
            var w = GetWindow<EnemyPendingWindow>(true, "Pendiente para la web");
            w.prefab = Selection.activeObject as GameObject;
            w.minSize = new Vector2(420, 220);
        }

        [MenuItem("Tools/RedMagic/Enemigos/Actualizar imágenes de la web")]
        private static void RefreshAll()
        {
            int n = 0;
            foreach (var dir in Directory.Exists(EnemyPreviewExporter.LibraryRoot) ? Directory.GetDirectories(EnemyPreviewExporter.LibraryRoot) : new string[0])
            {
                var file = Path.Combine(dir, "enemigo.json");
                if (!File.Exists(file)) continue;
                var prefabPath = (string)JObject.Parse(File.ReadAllText(file))["prefab"];
                if (string.IsNullOrEmpty(prefabPath) || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) continue;
                EnemyPreviewExporter.Mark(prefabPath);
                n++;
            }
            Debug.Log($"[Enemigos web] {n} imagen(es) actualizadas en {EnemyPreviewExporter.LibraryRoot}/");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Apunta lo que le falta a un enemigo: aparece en la web (🎨 Designs ▸ Enemigos), desde donde se lanza la tarea.", MessageType.None);
            prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
            tipo = EditorGUILayout.Popup("Tipo", tipo, Etiquetas);
            EditorGUILayout.LabelField("Qué falta");
            texto = EditorGUILayout.TextArea(texto, GUILayout.MinHeight(60));
            var path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")))
            {
                if (GUILayout.Button(string.IsNullOrWhiteSpace(texto) ? "Añadir a la web (sin pendiente)" : "Añadir pendiente"))
                {
                    last = EnemyPreviewExporter.Mark(path, Tipos[tipo], texto);
                    texto = "";
                    GUI.FocusControl(null);
                }
            }
            if (!string.IsNullOrEmpty(last)) EditorGUILayout.HelpBox($"Guardado en {last}", MessageType.Info);
        }
    }
}
