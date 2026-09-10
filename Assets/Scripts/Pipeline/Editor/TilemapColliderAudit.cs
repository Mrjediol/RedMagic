using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Revisa que todo <see cref="TilemapCollider2D"/> del juego esté fusionado en un
    /// <see cref="CompositeCollider2D"/>.
    ///
    /// <b>Por qué existe.</b> Un <c>TilemapCollider2D</c> suelto genera <b>una caja por tile</b>.
    /// Dos tiles contiguos comparten una cara vertical, y esa cara es una pared real para el motor
    /// de físicas: un cuerpo dinámico que camina por encima se engancha en la costura y se para en
    /// seco, en suelo que a ojo es perfectamente plano. Medido en MainHub, el enemigo recibía este
    /// contacto caminando sobre terreno llano:
    ///
    /// <code>[Tilemap] normal=(1.00, 0.00) point=(9.01, -3.00)</code>
    ///
    /// El cerebro escribía su velocidad cada frame y la física se la comía contra la costura. Se
    /// diagnostica fatal porque no falla nada en consola, no es el arte y no es la IA: parece que
    /// el enemigo "deja de seguirte a veces". Al fusionar el tilemap, MainHub pasó de cientos de
    /// cajas a 5 contornos y el mismo enemigo pasó de recorrer 2,9 unidades a 11,5 sin pararse.
    ///
    /// Afecta igual al jugador (tirones al correr) y a cualquier cosa que ande por el suelo, así
    /// que la regla es: <b>ningún tilemap con collider se queda sin composite</b>.
    /// </summary>
    public static class TilemapColliderAudit
    {
        /// <summary>Sólo escenas del juego: los paquetes de terceros no se tocan.</summary>
        private static readonly string[] SceneFolders = { "Assets/Scenes" };

        [MenuItem("Tools/RedMagic/Pipeline/6 · Auditar colliders de tilemap")]
        public static void AuditMenu() => Debug.Log(Run(false));

        [MenuItem("Tools/RedMagic/Pipeline/7 · Auditar y reparar colliders de tilemap")]
        public static void RepairMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!EditorUtility.DisplayDialog("Reparar colliders de tilemap",
                    "Se abrirá cada escena de Assets/Scenes, se fusionará todo TilemapCollider2D " +
                    "en un CompositeCollider2D y se guardará.\n\nNo se borra ni se repinta nada: " +
                    "sólo se añaden los componentes que faltan.", "Reparar", "Cancelar"))
                return;

            Debug.Log(Run(true));
        }

        /// <summary>
        /// Una sola escena. Existe para poder lanzarlo desde la CLI: <see cref="Run"/> abre las
        /// nueve escenas de una tacada y se pasa del límite de tiempo de <c>unity command eval</c>,
        /// así que headless se llama a esto una vez por escena.
        /// </summary>
        public static string RunSingle(string scenePath, bool repair)
        {
            var log = new StringBuilder();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int found = 0, repaired = 0;

            foreach (var tilemap in CollidersIn(scene))
            {
                found++;
                if (!NeedsMerge(tilemap)) continue;

                log.AppendLine($"  · '{PathOf(tilemap.transform)}' sin composite (una caja por tile).");
                if (!repair) continue;

                Merge(tilemap, log);
                repaired++;
            }

            if (repaired > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine("  ✔ guardada.");
            }

            return $"{scenePath}: {found} tilemap(s) con collider, {repaired} reparado(s).\n{log}";
        }

        /// <summary>
        /// Recorre las escenas del juego y reporta (o repara) los tilemaps sin fusionar. Deja
        /// abierta al final la escena que estuviera abierta al empezar.
        /// </summary>
        public static string Run(bool repair)
        {
            var log = new StringBuilder();
            log.AppendLine(repair ? "[TilemapColliderAudit] Reparando…" : "[TilemapColliderAudit] Revisando…");

            string reopen = SceneManager_CurrentPath();
            var scenes = FindScenes();
            int checkedCount = 0, issues = 0, repaired = 0;

            foreach (string path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool dirty = false;

                foreach (var tilemap in CollidersIn(scene))
                {
                    checkedCount++;
                    if (!NeedsMerge(tilemap)) continue;

                    issues++;
                    log.AppendLine($"  · {path} → '{PathOf(tilemap.transform)}' sin composite (una caja por tile).");

                    if (!repair) continue;

                    Merge(tilemap, log);
                    dirty = true;
                    repaired++;
                }

                if (dirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    log.AppendLine($"  ✔ {path} guardada.");
                }
            }

            if (!string.IsNullOrEmpty(reopen)) EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);

            log.AppendLine($"[TilemapColliderAudit] {scenes.Count} escenas, {checkedCount} tilemaps con collider, " +
                           $"{issues} sin fusionar, {repaired} reparados.");
            if (issues == 0) log.AppendLine("Todo correcto: ningún tilemap genera costuras entre tiles.");
            else if (!repair) log.AppendLine("Ejecuta '7 · Auditar y reparar colliders de tilemap' para arreglarlos.");

            return log.ToString();
        }

        /// <summary>
        /// Un tilemap está bien sólo si fusiona (<c>Merge</c>) <b>y</b> el composite existe: sin
        /// una de las dos cosas se siguen generando las cajas por tile.
        /// </summary>
        private static bool NeedsMerge(TilemapCollider2D tilemap)
        {
            return tilemap.compositeOperation != Collider2D.CompositeOperation.Merge
                   || tilemap.GetComponent<CompositeCollider2D>() == null;
        }

        /// <summary>
        /// Fusiona el tilemap. El <see cref="Rigidbody2D"/> lo exige el composite; va
        /// <b>Static</b> siempre — un suelo con cuerpo dinámico se desploma en cuanto le da la
        /// gravedad, así que si ya había uno mal puesto se corrige y se avisa.
        /// </summary>
        private static void Merge(TilemapCollider2D tilemap, StringBuilder log)
        {
            var go = tilemap.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(go, "Fusionar collider de tilemap");

            var body = go.GetComponent<Rigidbody2D>();
            if (body == null) body = Undo.AddComponent<Rigidbody2D>(go);
            if (body.bodyType != RigidbodyType2D.Static)
            {
                log.AppendLine($"    (Rigidbody2D era {body.bodyType}; forzado a Static: un suelo no cae.)");
                body.bodyType = RigidbodyType2D.Static;
            }

            var composite = go.GetComponent<CompositeCollider2D>();
            if (composite == null) composite = Undo.AddComponent<CompositeCollider2D>(go);
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;

            tilemap.compositeOperation = Collider2D.CompositeOperation.Merge;

            EditorUtility.SetDirty(go);
            log.AppendLine($"    → fusionado en {composite.shapeCount} contorno(s).");
        }

        private static List<TilemapCollider2D> CollidersIn(UnityEngine.SceneManagement.Scene scene)
        {
            var found = new List<TilemapCollider2D>();
            foreach (var root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<TilemapCollider2D>(true));

            return found;
        }

        private static List<string> FindScenes()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", SceneFolders))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            paths.Sort();
            return paths;
        }

        private static string SceneManager_CurrentPath()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return active.IsValid() ? active.path : null;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}
