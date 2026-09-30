using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Convierte un prefab "cuerpo de sprites sueltos" (una raíz con un hijo <c>Visual</c> y las
    /// partes colgando) en un enemigo funcional animado por <see cref="ProceduralRig"/>, y crea sus
    /// variantes por motor (<c>_Code</c>, <c>_Dotween</c>).
    ///
    /// Lo común a todos se hace solo: la pila de enemigo (vida, retroceso, destello, cadáver,
    /// cerebro, ataque, animación, sonido, monedas), el rig con una entrada por parte y fases
    /// desfasadas, y la migración de los <c>ProceduralSway</c> antiguos (sus valores pasan al rig).
    /// Idempotente: lo que ya existe y se afinó a mano no se toca.
    ///
    /// Lo propio de cada enemigo (qué brazo apunta, dónde está la boca, sus números) va en su pack
    /// (<see cref="ProceduralEnemyPacks"/>). Ver <c>docs/architecture/procedural-rig.md</c>.
    /// </summary>
    public static class ProceduralRigTool
    {
        public const string VisualName = "Visual";
        public const string CodeSuffix = "_Code";
        public const string TweenSuffix = "_Dotween";

        /// <summary>Radianes entre fases consecutivas: el ángulo áureo nunca repite patrón.</summary>
        private const float GoldenPhase = 2.39996f;

        [MenuItem("Tools/RedMagic/Procedural/1 · Preparar enemigo por partes (selección)")]
        private static void SetupSelected()
        {
            var log = new StringBuilder();
            foreach (var path in SelectedPrefabs()) Setup(path, log);
            Debug.Log(log.ToString());
        }

        [MenuItem("Tools/RedMagic/Procedural/2 · Crear variante _Code (selección)")]
        private static void CodeVariantSelected()
        {
            var log = new StringBuilder();
            foreach (var path in SelectedPrefabs()) EnsureVariant(path, CodeSuffix, typeof(RigDriverCode), log);
            Debug.Log(log.ToString());
        }

        private static IEnumerable<string> SelectedPrefabs()
        {
            foreach (var go in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(go);
                if (path.EndsWith(".prefab", StringComparison.Ordinal)) yield return path;
            }
        }

        // ============================================================ preparar

        /// <summary>
        /// Deja el prefab base listo: pila de enemigo + rig. <paramref name="configure"/> recibe el
        /// prefab abierto para lo específico del enemigo (y el bool de "recién creado").
        /// </summary>
        public static void Setup(string prefabPath, StringBuilder log,
                                 Action<GameObject, ProceduralRig, bool> configure = null,
                                 Action<GameObject> prepare = null)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                log.AppendLine($"== {Path.GetFileNameWithoutExtension(prefabPath)}");

                // Reorganizar la jerarquía (envoltorios de pivote, fusionar variantes de sprite)
                // antes de rellenar el rig, para que las partes salgan ya con su forma final.
                prepare?.Invoke(root);
                EnsureEnemyStack(root, log);

                bool created = root.GetComponent<ProceduralRig>() == null;
                var rig = Get<ProceduralRig>(root);
                FillRig(root, rig, log);

                configure?.Invoke(root, rig, created);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureEnemyStack(GameObject root, StringBuilder log)
        {
            var body = Get<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (root.GetComponent<Collider2D>() == null)
            {
                root.AddComponent<CapsuleCollider2D>();
                log.AppendLine("  AVISO: sin collider — añadido un CapsuleCollider2D por defecto, ajústalo.");
            }

            Get<Health>(root);
            Get<Knockback>(root);
            Get<HitFlash>(root);
            Get<Corpse>(root);
            Get<CurrencyDropper>(root);
            Get<EnemyStats>(root);
            Get<EnemyBrain>(root);
            Get<EnemyAttack>(root);
            Get<EnemyAnimation>(root);
            Get<Audio.SoundEmitter>(root);
        }

        /// <summary>
        /// Una entrada por parte: todo lo que cuelga de <c>Visual</c> con sprite o con un
        /// <c>ProceduralSway</c> antiguo (cuyos valores se heredan y el componente se quita).
        /// Las entradas que ya existen no se tocan.
        /// </summary>
        private static void FillRig(GameObject root, ProceduralRig rig, StringBuilder log)
        {
            var visual = root.transform.Find(VisualName);
            if (visual == null)
            {
                log.AppendLine($"  AVISO: no hay hijo '{VisualName}'. Asigna 'body' a mano.");
                return;
            }

            if (rig.body == null) rig.body = visual;

            int index = rig.parts.Count;
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
            {
                if (t == visual || t.name == EnemyAttack.MuzzleChildName) continue;

                var legacy = t.GetComponent<ProceduralSway>();
                bool isPart = legacy != null || t.GetComponent<SpriteRenderer>() != null;
                if (!isPart || rig.parts.Exists(p => p.part == t))
                {
                    if (legacy != null) Object.DestroyImmediate(legacy, true);
                    continue;
                }

                var part = new RigPart
                {
                    part = t,
                    swayMove = new Vector2(0f, 0.03f),
                    swayRotate = 2f,
                    swaySpeed = 1.6f + 0.35f * (index % 3),
                    swayPhase = index * GoldenPhase % (Mathf.PI * 2f),
                };

                if (legacy != null)
                {
                    var so = new SerializedObject(legacy);
                    part.swayMove = so.FindProperty("moveAmplitude").vector2Value;
                    part.swayRotate = so.FindProperty("rotateAmplitude").floatValue;
                    part.swaySpeed = so.FindProperty("speed").floatValue;
                    float phase = so.FindProperty("phase").floatValue;
                    if (Mathf.Abs(phase) > 0.0001f) part.swayPhase = phase;
                    Object.DestroyImmediate(legacy, true);
                }

                rig.parts.Add(part);
                index++;
                log.AppendLine($"  parte: {t.name}{(legacy != null ? " (valores de ProceduralSway)" : "")}");
            }
        }

        // ============================================================ variantes por motor

        /// <summary>
        /// Crea <c>&lt;base&gt;&lt;suffix&gt;.prefab</c> como <b>variante</b> del base con el motor
        /// dado. Todo lo demás (partes, tiempos, números del enemigo) se hereda del base, así que
        /// afinar el base afina las dos pasadas a la vez. Si ya existe, no se toca.
        /// </summary>
        public static GameObject EnsureVariant(string basePath, string suffix, Type driver, StringBuilder log)
        {
            string path = basePath.Replace(".prefab", suffix + ".prefab");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                log.AppendLine($"  variante '{Path.GetFileName(path)}' ya existe.");
                return existing;
            }

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                foreach (var other in instance.GetComponents<RigDriver>()) Object.DestroyImmediate(other);
                instance.AddComponent(driver);
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
                log.AppendLine($"  variante creada: {Path.GetFileName(path)} ({driver.Name}).");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // ============================================================ utilidades

        public static T Get<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        public static RigPart Part(ProceduralRig rig, string name) =>
            rig.parts.Find(p => p.part != null && p.part.name == name);

        /// <summary>Hijo "Muzzle" bajo <paramref name="parent"/>, creado en <paramref name="local"/> la primera vez.</summary>
        public static Transform EnsureMuzzle(Transform parent, Vector2 local)
        {
            var muzzle = parent.Find(EnemyAttack.MuzzleChildName);
            if (muzzle != null) return muzzle;

            muzzle = new GameObject(EnemyAttack.MuzzleChildName).transform;
            muzzle.SetParent(parent, false);
            muzzle.localPosition = local;
            return muzzle;
        }
    }
}
