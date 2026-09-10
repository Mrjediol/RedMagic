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
                    "Se añadirán los componentes compartidos que falten a los prefabs con Health. " +
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

            AssetDatabase.SaveAssets();

            log.AppendLine($"[ContentAudit] {checkedCount} prefabs con Health, {issues} con carencias" +
                           (repair ? $", {fixedCount} reparados." : "."));

            if (issues == 0) log.AppendLine("  Todo en orden.");
            return log.ToString();
        }
    }
}
