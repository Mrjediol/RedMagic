using System.Text;
using RedMagic.Bosses.EditorTools;
using RedMagic.Enemies;
using RedMagic.Fx;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Los enemigos por partes, uno por método: <b>sólo datos</b> (qué brazo apunta, dónde está la
    /// boca, sus números). La pila común y el rig los monta <see cref="ProceduralRigTool"/>.
    ///
    /// Idempotente: lo específico se siembra la primera vez (cuando el prefab aún no tenía rig);
    /// después manda lo afinado a mano en el Inspector. Crea el placeholder de su proyectil y la
    /// variante por motor.
    /// </summary>
    public static class ProceduralEnemyPacks
    {
        public const string Folder = "Assets/Prefabs/Enemies/Procedural";
        private const string FxFolder = "Assets/Prefabs/Fx/Enemies";
        private const string PlaceholderDisc = "Assets/Art/Placeholder/Disc.png";

        // ============================================================ dron

        [MenuItem("Tools/RedMagic/Procedural/Packs/Dron (Robot)")]
        public static void Drone() => Debug.Log(RunDrone());

        /// <summary>Punto de entrada también para <c>unity command eval</c>.</summary>
        public static string RunDrone()
        {
            var log = new StringBuilder();
            string path = Folder + "/Robot.prefab";
            var projectile = PlaceholderProjectile("Robot", log);

            ProceduralRigTool.Setup(path, log, (root, rig, created) =>
            {
                // El brazo derecho es el cañón: la boca va en la punta del cristal (medida sobre el
                // dibujo: 403 px a la derecha y 140 px arriba del pivote, a 100 px/u).
                var arm = root.transform.Find("Visual/Torso/RightArm");
                if (arm != null) ProceduralRigTool.EnsureMuzzle(arm, new Vector2(4.0f, 1.4f));

                if (!created)
                {
                    log.AppendLine("  rig ya existía: valores afinados conservados.");
                    return;
                }

                rig.aimPart = arm;
                rig.aimArtAngle = 11.5f;   // el cañón del dibujo sube ~11°
                rig.aimMaxAngle = 70f;
                rig.hoverBob = new Vector2(0f, 0.12f);
                rig.leanDegrees = 14f;

                // Lienzos completos: el pivote del sprite cae en el centro del lienzo, lejos de la
                // pieza. El punto de articulación real se midió sobre el dibujo (100 px/u).
                var ring = ProceduralRigTool.Part(rig, "Ring");
                if (ring != null) ring.pivot = new Vector2(-1.07f, -1.53f);

                var flame = ProceduralRigTool.Part(rig, "Flame");
                if (flame != null)
                {
                    // Llama del propulsor: late rápido desde su boquilla, sin vaivén propio.
                    flame.pivot = new Vector2(-0.6f, -1.25f);
                    flame.swayMove = Vector2.zero;
                    flame.swayRotate = 4f;
                    flame.swayScale = new Vector2(0.1f, 0.22f);
                    flame.swaySpeed = 16f;
                }

                var stats = root.GetComponent<EnemyStats>();
                var t = stats.Tuning;
                t.archetype = EnemyArchetype.FlyingRanged;
                t.maxHealth = 30f;
                t.detectionRange = 9f;
                t.attackRange = 6f;
                t.personalSpace = 3f;
                t.moveSpeed = 2.5f;
                t.retreatSpeed = 2.2f;
                t.hoverOffset = 1.2f;
                t.attackDamage = 8f;
                t.attackCooldown = 1.6f;
                t.contactDamage = 0f;
                t.aimAtTarget = true;
                t.projectile.prefab = projectile;
                t.projectile.speed = 9f;
                t.projectile.lifetime = 2.5f;
                t.projectile.size = new Vector2(0.4f, 0.4f);
                t.projectileTint = new Color(0.45f, 1f, 1f, 1f);
                EditorUtility.SetDirty(stats);

                // Las piezas necesitan tiempo para caer antes de desvanecerse.
                new BossAuthoring.Fields(root.GetComponent<Combat.Corpse>())
                    .Set("linger", 1.1f)
                    .Set("fadeDuration", 0.4f)
                    .Apply();

                log.AppendLine("  sembrado: FlyingRanged, brazo-arma RightArm, proyectil placeholder.");
            });

            ProceduralRigTool.EnsureVariant(path, ProceduralRigTool.CodeSuffix, typeof(RigDriverCode), log);

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ============================================================ proyectiles

        /// <summary>
        /// <c>Fx_&lt;enemy&gt;_Projectile</c>: un disco placeholder con <see cref="FxPlaceholderStyle"/>
        /// (lo tiñe y dimensiona el spec del enemigo). Arte real = editar el prefab, sin código.
        /// </summary>
        private static GameObject PlaceholderProjectile(string enemy, StringBuilder log)
        {
            string folder = $"{FxFolder}/{enemy}";
            string path = $"{folder}/Fx_{enemy}_Projectile.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderDisc);
            var prefab = ProjectilePrefabFactory.BuildFromSprite(folder, $"{enemy}_Projectile", sprite, 1f);

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ProceduralRigTool.Get<FxPlaceholderStyle>(root);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            log.AppendLine($"  proyectil placeholder: {path}");
            return prefab;
        }
    }
}
