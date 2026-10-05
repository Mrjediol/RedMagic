using UnityEditor;
using UnityEngine;

namespace RedMagic.Enemies.EditorTools
{
    /// <summary>
    /// Crea variantes visuales de un enemigo: un Material del shader "RedMagic/Sprite Lit FX" y un
    /// Prefab Variant del enemigo base con ese material en sus partes. Sólo cambia el aspecto —
    /// comportamiento, rig y estadísticas se heredan del base.
    /// </summary>
    public static class EnemyShaderVariants
    {
        private const string ShaderName = "RedMagic/Sprite Lit FX";
        private const string MaterialDir = "Assets/Art/Fx/Materials/Enemies";

        /// <summary>Crea (o reescribe) el material y la variante. Devuelve la ruta de la variante.</summary>
        public static string Create(string basePrefabPath, string suffix, System.Action<Material> configure)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) { Debug.LogError($"[EnemyShaderVariants] No existe el shader '{ShaderName}'."); return null; }

            if (!AssetDatabase.IsValidFolder(MaterialDir))
            {
                AssetDatabase.CreateFolder("Assets/Art/Fx/Materials", "Enemies");
            }

            var baseName = System.IO.Path.GetFileNameWithoutExtension(basePrefabPath);
            var matPath = $"{MaterialDir}/{baseName}_{suffix}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;
            configure(mat);
            EditorUtility.SetDirty(mat);

            var variantPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(basePrefabPath), $"{baseName}_{suffix}.prefab")
                .Replace((char)92, '/');
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    // Sólo las partes con el shader normal: los efectos aditivos se quedan como están.
                    var current = sr.sharedMaterial;
                    if (current != null && current.shader != null && current.shader.name.Contains("Sprite-Lit-Default"))
                        sr.sharedMaterial = mat;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            }
            finally { Object.DestroyImmediate(instance); }

            AssetDatabase.SaveAssets();
            return variantPath;
        }

        [MenuItem("Tools/Enemies/Variantes de shader · PolarTurretBot")]
        private static void PolarTurretBot()
        {
            const string basePath = "Assets/Prefabs/Enemies/Procedural/PolarTurretBot.prefab";

            // Escarcha: destello blanco-azulado que pasa por el cuerpo + cristales que laten.
            Create(basePath, "Frost", m =>
            {
                m.SetColor("_SheenColor", new Color(0.75f, 0.95f, 1f, 0.9f));
                m.SetFloat("_SheenSpeed", 0.7f);
                m.SetFloat("_SheenWidth", 0.22f);
                m.SetFloat("_SheenPause", 1.8f);
                m.SetColor("_GlowColor", new Color(0.2f, 0.9f, 1.6f, 0.55f));
                m.SetFloat("_GlowThreshold", 0.65f);
                m.SetFloat("_GlowPulse", 2.2f);
                m.SetColor("_RimColor", new Color(0.6f, 0.95f, 1f, 0.5f));
                m.SetFloat("_RimWidth", 6f);
            });

            // Infernal: tono hacia naranja, más saturado, brasas que pulsan.
            Create(basePath, "Inferno", m =>
            {
                m.SetFloat("_Hue", -0.42f);
                m.SetFloat("_Saturation", 1.3f);
                m.SetColor("_GlowColor", new Color(1.8f, 0.6f, 0.1f, 0.8f));
                m.SetFloat("_GlowThreshold", 0.55f);
                m.SetFloat("_GlowPulse", 4f);
                m.SetColor("_RimColor", new Color(1.5f, 0.35f, 0.05f, 0.7f));
                m.SetFloat("_RimWidth", 7f);
            });

            // Vacío: violeta, apagado, contorno interior que brilla.
            Create(basePath, "Void", m =>
            {
                m.SetFloat("_Hue", 0.22f);
                m.SetFloat("_Saturation", 0.8f);
                m.SetFloat("_Brightness", 0.7f);
                m.SetColor("_RimColor", new Color(1.3f, 0.4f, 2f, 0.9f));
                m.SetFloat("_RimWidth", 10f);
                m.SetColor("_GlowColor", new Color(1.2f, 0.3f, 1.8f, 0.6f));
                m.SetFloat("_GlowThreshold", 0.7f);
                m.SetFloat("_GlowPulse", 1.2f);
            });

            AssetDatabase.Refresh();
        }
    }
}
