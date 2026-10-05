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
        public static string Create(string basePrefabPath, string suffix, System.Action<Material> configure,
            string shadowPart = "Torso", string mountPart = "CastArm")
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
            mat.SetFloat("_Gradient", 0.12f);
            configure(mat);
            EditorUtility.SetDirty(mat);

            var shadowMat = ContactShadowMaterial(mat, basePrefabPath, shadowPart, mountPart);

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
                    if (current != null && current.shader != null && (current.shader.name.Contains("Sprite-Lit-Default") || current.shader.name == ShaderName))
                        sr.sharedMaterial = shadowMat != null && sr.name == shadowPart ? shadowMat : mat;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            }
            finally { Object.DestroyImmediate(instance); }

            AssetDatabase.SaveAssets();
            return variantPath;
        }

        /// <summary>
        /// Copia de <paramref name="source"/> con una sombra de contacto en <paramref name="shadowPart"/> justo
        /// donde se monta <paramref name="mountPart"/> (el cañón sobre el platillo): la pieza de abajo se oscurece
        /// alrededor del punto de apoyo y la de arriba deja de verse "pegada encima".
        /// </summary>
        public static Material ContactShadowMaterial(Material source, string basePrefabPath, string shadowPart, string mountPart)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                Transform under = null, over = null;
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == shadowPart) under = t;
                    if (t.name == mountPart) over = t;
                }
                if (under == null || over == null) return null;

                var local = under.InverseTransformPoint(over.position);
                var path = AssetDatabase.GetAssetPath(source);
                var copyPath = path.Replace(".mat", "_" + shadowPart + ".mat");
                var copy = AssetDatabase.LoadAssetAtPath<Material>(copyPath);
                if (copy == null) { copy = new Material(source); AssetDatabase.CreateAsset(copy, copyPath); }
                copy.CopyPropertiesFromMaterial(source);
                copy.SetVector("_ShadowPos", new Vector4(local.x, local.y - 0.1f, 0f, 0f));
                copy.SetFloat("_ShadowRadius", 1.5f);
                copy.SetFloat("_ShadowStrength", 0.55f);
                copy.SetFloat("_ShadowSquash", 0.7f);
                EditorUtility.SetDirty(copy);
                return copy;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        /// <summary>Mejora visual del enemigo original, sin cambiar colores: sombra de contacto del cañón y algo de volumen.</summary>
        [MenuItem("Tools/Enemies/Pulido visual · PolarTurretBot (base)")]
        private static void PolishPolarTurretBot()
        {
            const string basePath = "Assets/Prefabs/Enemies/Procedural/PolarTurretBot.prefab";
            var shader = Shader.Find(ShaderName);
            if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder("Assets/Art/Fx/Materials", "Enemies");
            var matPath = MaterialDir + "/PolarTurretBot_Polish.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            mat.SetFloat("_Gradient", 0.12f);
            var torsoMat = ContactShadowMaterial(mat, basePath, "Torso", "CastArm");

            var root = PrefabUtility.LoadPrefabContents(basePath);
            try
            {
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.sharedMaterial != null && sr.sharedMaterial.shader.name.Contains("Sprite-Lit-Default"))
                        sr.sharedMaterial = sr.name == "Torso" ? torsoMat : mat;
                PrefabUtility.SaveAsPrefabAsset(root, basePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
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
                m.SetColor("_GlowColor", new Color(0.3f, 1.2f, 2f, 1f));
                m.SetFloat("_GlowThreshold", 0.45f);
                m.SetFloat("_GlowPulse", 2.2f);
                m.SetColor("_RimColor", new Color(0.7f, 1.2f, 1.6f, 1f));
                m.SetFloat("_RimWidth", 45f);
            });

            // Infernal: tono hacia naranja, más saturado, brasas que pulsan.
            Create(basePath, "Inferno", m =>
            {
                m.SetFloat("_Hue", 0.42f);
                m.SetFloat("_Saturation", 1.3f);
                m.SetColor("_GlowColor", new Color(2.2f, 0.7f, 0.1f, 1f));
                m.SetFloat("_GlowThreshold", 0.4f);
                m.SetFloat("_GlowPulse", 4f);
                m.SetColor("_RimColor", new Color(2f, 0.5f, 0.05f, 1f));
                m.SetFloat("_RimWidth", 50f);
            });

            // Vacío: violeta, apagado, contorno interior que brilla.
            Create(basePath, "Void", m =>
            {
                m.SetFloat("_Hue", -0.27f);
                m.SetFloat("_Saturation", 0.8f);
                m.SetFloat("_Brightness", 0.7f);
                m.SetColor("_RimColor", new Color(1.6f, 0.5f, 2.4f, 1f));
                m.SetFloat("_RimWidth", 60f);
                m.SetColor("_GlowColor", new Color(1.5f, 0.4f, 2.2f, 1f));
                m.SetFloat("_GlowThreshold", 0.45f);
                m.SetFloat("_GlowPulse", 1.2f);
            });

            AssetDatabase.Refresh();
        }
    }
}
