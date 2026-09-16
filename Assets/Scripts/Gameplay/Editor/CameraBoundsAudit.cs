using System.Collections.Generic;
using System.Text;
using RedMagic.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Gameplay.EditorTools
{
    /// <summary>
    /// Mide, escena por escena, si el fondo (<c>BG</c>) es lo bastante grande como para que la
    /// cámara nunca tenga que enseñar nada de fuera.
    ///
    /// Existe porque los fondos llegan generados por IA, sin margen sobrante y con un tamaño
    /// distinto cada vez: el recorte de <see cref="CameraFollow"/> impide asomarse al vacío, pero
    /// sólo si el arte cubre la pantalla — y cuando no la cubre no hay ningún error, simplemente
    /// se ve el borde. Esto es lo que lo convierte en un número antes de jugar.
    ///
    /// Lo que se compara es el fondo contra la <b>pantalla a pleno zoom de juego</b>: el
    /// <c>cameraOrthographicSize</c> que <c>RunManager</c> fuerza en todas las cámaras después de
    /// cada carga, no el que trae la cámara de la escena en el editor.
    /// </summary>
    public static class CameraBoundsAudit
    {
        private const string ScenesRoot = "Assets/Scenes";

        /// <summary>
        /// Relaciones de aspecto contra las que se mide. Un móvil apaisado es el caso ancho — a
        /// más ancho, más fondo hace falta a los lados y menos por arriba — así que se comprueban
        /// las dos puntas del abanico y no una sola.
        /// </summary>
        private static readonly (string Name, float Aspect)[] Aspects =
        {
            ("16:9", 16f / 9f),
            ("20:9", 20f / 9f)
        };

        [MenuItem("Tools/RedMagic/Camera/Auditar encuadre (BG vs cámara)", priority = 300)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            float orthoSize = ResolveRuntimeOrthoSize(out string orthoOrigin);

            var report = new StringBuilder();
            report.AppendLine($"Auditoría de encuadre — orthographicSize {orthoSize:0.##} ({orthoOrigin})");
            report.AppendLine();

            int checkedScenes = 0, problems = 0;

            foreach (string path in ScenePaths())
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!scene.IsValid()) continue;

                var line = AuditScene(scene, orthoSize, out bool hasCamera, out bool failed);
                if (!hasCamera) continue;

                checkedScenes++;
                if (failed) problems++;
                report.AppendLine(line);
            }

            report.AppendLine();
            report.AppendLine(problems == 0
                ? $"{checkedScenes} escenas con cámara, todas cubiertas por su fondo."
                : $"{checkedScenes} escenas con cámara, {problems} con el fondo demasiado pequeño.");

            Debug.Log(report.ToString());
        }

        private static IEnumerable<string> ScenePaths()
        {
            var guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { ScenesRoot });
            var paths = new List<string>(guids.Length);

            foreach (string guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            paths.Sort(System.StringComparer.Ordinal);
            return paths;
        }

        private static string AuditScene(Scene scene, float orthoSize, out bool hasCamera, out bool failed)
        {
            hasCamera = false;
            failed = false;

            CameraFollow follow = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                follow = root.GetComponentInChildren<CameraFollow>(true);
                if (follow != null) break;
            }

            if (follow == null) return string.Empty;
            hasCamera = true;

            follow.RefreshBounds();

            if (!follow.HasLimit)
            {
                failed = true;
                return $"  ✗ {scene.name}: sin límites — no hay ningún objeto '{CameraFollow.DefaultBackgroundName}' con Renderer (o el modo es None).";
            }

            var size = follow.Limit.size;
            var sb = new StringBuilder();
            sb.Append($"  {scene.name}: fondo {size.x:0.0} × {size.y:0.0} u");

            var shortfalls = new List<string>();

            foreach (var (name, aspect) in Aspects)
            {
                float needH = orthoSize * 2f;
                float needW = needH * aspect;

                float missingW = needW - size.x;
                float missingH = needH - size.y;

                if (missingW > 0.01f) shortfalls.Add($"{name}: faltan {missingW:0.0} u de ancho");
                if (missingH > 0.01f) shortfalls.Add($"{name}: faltan {missingH:0.0} u de alto");
            }

            if (shortfalls.Count == 0)
            {
                sb.Insert(2, "✓ ");
                return sb.ToString();
            }

            failed = true;
            sb.Insert(2, "✗ ");
            sb.Append(" — ").Append(string.Join("; ", shortfalls));
            return sb.ToString();
        }

        /// <summary>
        /// El zoom real de juego sale del <c>RunManager</c>, que lo fuerza en todas las cámaras
        /// tras cada carga. Se lee de la escena que lo tenga colocado; si no aparece ninguno se
        /// usa el valor por defecto del componente.
        /// </summary>
        private static float ResolveRuntimeOrthoSize(out string origin)
        {
            foreach (string path in ScenePaths())
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!scene.IsValid()) continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    var manager = root.GetComponentInChildren<RedMagic.Run.RunManager>(true);
                    if (manager == null) continue;

                    var property = new SerializedObject(manager).FindProperty("cameraOrthographicSize");
                    if (property == null) continue;

                    origin = $"RunManager de {scene.name}";
                    return property.floatValue;
                }
            }

            origin = "valor por defecto";
            return 10f;
        }
    }
}
