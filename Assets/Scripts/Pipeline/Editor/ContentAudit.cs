using System.Text;
using RedMagic.Combat;
using RedMagic.Economy;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Revisa que todo lo que puede recibir daño lleve la parafernalia compartida.
    ///
    /// El fallo que busca es silencioso por diseño: <c>Health</c> funciona perfectamente sin
    /// <c>Knockback</c> y sin <c>HitFlash</c>. El enemigo recibe daño, muere y todo "va bien" —
    /// sólo que no parpadea al ser golpeado ni sale despedido, y eso se lee como que el juego no
    /// registra los impactos. Nada falla en consola, así que se descubre jugando.
    ///
    /// Con la fábrica de enemigos esto ya no puede pasar en contenido nuevo. La auditoría es para
    /// lo que se hizo a mano antes, y para cualquier prefab que alguien monte al margen.
    /// </summary>
    public static class ContentAudit
    {
        private static readonly string[] Folders = { "Assets/Prefab" };

        [MenuItem("Tools/RedMagic/Pipeline/4 · Auditar contenido")]
        public static void AuditMenu() => Debug.Log(Run(false));

        [MenuItem("Tools/RedMagic/Pipeline/5 · Auditar y reparar")]
        public static void RepairMenu()
        {
            if (!EditorUtility.DisplayDialog("Auditar y reparar",
                    "Se añadirán los componentes compartidos que falten a los prefabs con Health y " +
                    "se completará la máscara de terreno de los enemigos. " +
                    "No se borra ni se reconfigura nada.", "Reparar", "Cancelar"))
                return;

            Debug.Log(Run(true));
        }

        /// <summary>Recorre los prefabs con <see cref="Health"/> y reporta (o repara) lo que falte.</summary>
        public static string Run(bool repair)
        {
            var log = new StringBuilder();
            log.AppendLine(repair ? "[ContentAudit] Reparando…" : "[ContentAudit] Revisando…");

            int checkedCount = 0, issues = 0, fixedCount = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", Folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<Health>() == null) continue;

                checkedCount++;

                // La máscara de terreno se revisa siempre, tenga o no carencias de componentes:
                // es un fallo distinto y también silencioso.
                foreach (var walker in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (walker is not (Enemies.EnemyStats or Gameplay.EnemyController)) continue;
                    if (!RepairTerrainMask(walker, repair, log, path)) continue;

                    issues++;
                    if (repair) fixedCount++;
                }

                // El retroceso sólo tiene sentido en algo que se pueda mover. El muñeco de
                // entrenamiento y los anclajes de los jefes son postes clavados en el suelo: no
                // llevan Rigidbody2D y reclamarles Knockback sería ruido, no un hallazgo.
                var rb = prefab.GetComponent<Rigidbody2D>();
                bool movable = rb != null && rb.bodyType != RigidbodyType2D.Static;
                bool needsKnockback = movable && prefab.GetComponent<Knockback>() == null;
                bool needsFlash = prefab.GetComponentInChildren<HitFlash>(true) == null;
                bool needsDrops = prefab.GetComponent<CurrencyDropper>() == null &&
                                  path.Contains("/Enemies/");
                bool needsRenderer = prefab.GetComponentInChildren<SpriteRenderer>(true) == null;

                if (!needsKnockback && !needsFlash && !needsDrops && !needsRenderer) continue;

                issues++;
                var missing = new StringBuilder();
                if (needsKnockback) missing.Append("Knockback ");
                if (needsFlash) missing.Append("HitFlash ");
                if (needsDrops) missing.Append("CurrencyDropper ");
                if (needsRenderer) missing.Append("SpriteRenderer(!) ");

                log.AppendLine($"  {path}: falta {missing.ToString().TrimEnd()}");

                if (!repair || needsRenderer) continue;   // sin arte no hay nada que reparar solo

                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (needsKnockback) root.AddComponent<Knockback>();
                if (needsFlash) root.AddComponent<HitFlash>();
                if (needsDrops) root.AddComponent<CurrencyDropper>();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                fixedCount++;
            }


            // Las fichas del pipeline llevan su propia copia del bloque de valores, así que si no
            // se reparan también, regenerar un enemigo vuelve a dejarle la máscara vieja.
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyRecipe"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var recipe = AssetDatabase.LoadAssetAtPath<EnemyRecipe>(path);
                if (recipe == null || !RepairTerrainMask(recipe, repair, log, path)) continue;

                issues++;
                if (repair) fixedCount++;
            }
            AssetDatabase.SaveAssets();

            log.AppendLine($"[ContentAudit] {checkedCount} prefabs con Health, {issues} con carencias" +
                           (repair ? $", {fixedCount} reparados." : "."));

            if (issues == 0) log.AppendLine("  Todo en orden.");
            return log.ToString();
        }

        /// <summary>
        /// Repara la máscara de terreno de todo lo que camina: prefabs de enemigo (los dos
        /// sistemas) y fichas del pipeline.
        ///
        /// El fallo que arregla es exactamente igual de silencioso que el de los componentes que
        /// faltan: la máscara por defecto era sólo <c>Ground</c>, pero las plataformas y los
        /// puentes inclinados del proyecto viven en la capa <c>Platform</c>. Con esa máscara, un
        /// enemigo que llega a un puente sondea el suelo, no encuentra nada, lo lee como
        /// precipicio y se planta — en un puente perfectamente sólido por el que el jugador acaba
        /// de pasar. Y sobre la rampa no encuentra superficie que seguir, así que empuja de frente
        /// contra la cuesta en vez de subirla.
        ///
        /// Sólo <b>añade</b> las capas de terreno que falten: lo que ya estuviera configurado se
        /// respeta, porque una máscara más ancha puede ser una decisión (el sistema viejo trae
        /// alguna con todo marcado).
        /// </summary>
        private static bool RepairTerrainMask(Object target, bool repair, StringBuilder log, string path)
        {
            int terrain = Gameplay.GroundMotion.TerrainMask.value;
            var so = new SerializedObject(target);
            var property = so.FindProperty("tuning.obstacleLayers") ?? so.FindProperty("groundLayers");
            if (property == null) return false;

            int current = property.intValue;
            if ((current & terrain) == terrain) return false;

            int missing = terrain & ~current;
            log.AppendLine($"  {path}: máscara de terreno sin {LayerNames(missing)} " +
                           $"({target.GetType().Name}).");

            if (!repair) return true;

            property.intValue = current | terrain;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            return true;
        }

        private static string LayerNames(int mask)
        {
            var names = new StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                string name = LayerMask.LayerToName(i);
                names.Append(string.IsNullOrEmpty(name) ? i.ToString() : name).Append(' ');
            }

            return names.ToString().TrimEnd();
        }
    }
}
