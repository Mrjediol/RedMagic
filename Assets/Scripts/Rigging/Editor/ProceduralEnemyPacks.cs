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
    /// después manda lo afinado a mano en el Inspector. Crea el placeholder de su proyectil y pone
    /// el motor (RigDriverCode) en el propio prefab.
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
                if (arm != null)
                {
                    var muzzle = ProceduralRigTool.EnsureMuzzle(arm, new Vector2(4.0f, 1.4f));
                    if (rig.muzzle == null) rig.muzzle = muzzle;
                }

                if (rig.muzzleFlashSprite == null)
                    rig.muzzleFlashSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderDisc);

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

            ProceduralRigTool.EnsureDriver(path, typeof(RigDriverCode), log);

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ============================================================ torreta

        private const string PlaceholderSliver = "Assets/Art/Placeholder/Sliver.png";

        [MenuItem("Tools/RedMagic/Procedural/Packs/Torreta (Turret)")]
        public static void Turret() => Debug.Log(RunTurret());

        /// <summary>
        /// Torreta de la jungla: Static + Ranged que duerme plegada. Reorganiza el prefab la primera
        /// vez (sin tocar el arte): el cuello pasa a colgar de un 'NeckPivot' en su base para
        /// estirarse desde abajo, las tres cabezas se funden en una 'Head' con sus sprites por
        /// estado en el rig, y la base pasa al frente.
        /// </summary>
        public static string RunTurret()
        {
            var log = new StringBuilder();
            string path = Folder + "/Turret.prefab";
            var laser = PlaceholderProjectile("Turret", log, "Laser", PlaceholderSliver);

            // Sprites de las cabezas antes de fundirlas (sólo existen en la primera pasada).
            Sprite asleep = null, awake = null, attacking = null;

            ProceduralRigTool.Setup(path, log, prepare: root =>
            {
                var visual = root.transform.Find(ProceduralRigTool.VisualName);
                if (visual == null) return;

                // Base: el sprite 'base' venía en un objeto llamado 'Torso'.
                var baseT = visual.Find("Torso");
                if (baseT != null) baseT.name = "Base";
                baseT = visual.Find("Base");

                // Cuello: su pivote está en el centro del lienzo; el envoltorio se pone en su
                // borde inferior (medido: 486 px bajo el pivote a 400 px/u) y el cuello cuelga de él.
                var neck = visual.Find("neck");
                if (neck != null && neck.parent == visual)
                {
                    var pivot = new GameObject("NeckPivot").transform;
                    pivot.SetParent(visual, false);
                    pivot.localPosition = neck.localPosition + new Vector3(0f, -1.215f, 0f);
                    pivot.SetSiblingIndex(neck.GetSiblingIndex());
                    neck.SetParent(pivot, true);
                    log.AppendLine("  cuello envuelto en 'NeckPivot' (pivote en su base).");
                }

                // Cabezas: una sola 'Head' (la despierta) + sprites por estado en el rig.
                var head = visual.Find("awake");
                var sleepT = visual.Find("sleep");
                var attackT = visual.Find("ataque");
                if (head != null)
                {
                    awake = head.GetComponent<SpriteRenderer>().sprite;
                    if (sleepT != null) asleep = sleepT.GetComponent<SpriteRenderer>().sprite;
                    if (attackT != null) attacking = attackT.GetComponent<SpriteRenderer>().sprite;
                    head.name = "Head";
                    head.gameObject.SetActive(true);
                    if (sleepT != null) Object.DestroyImmediate(sleepT.gameObject);
                    if (attackT != null) Object.DestroyImmediate(attackT.gameObject);
                    log.AppendLine("  cabezas fundidas en 'Head' (sprites por estado en el rig).");
                }

                head = visual.Find("Head");
                if (head != null) ProceduralRigTool.EnsureMuzzle(head, new Vector2(0.56f, 1.02f));   // entre los ojos

                // Orden: cuello detrás, cabeza delante del cuello, base delante de todo (plegada
                // se lee como una caja cerrada).
                SetOrder(neck, 2);
                SetOrder(head, 3);
                SetOrder(baseT, 4);

                // Collider del cuerpo desplegado: base (−0.82) a coronilla (3.26), en unidades locales.
                if (root.GetComponent<Collider2D>() == null)
                {
                    var box = root.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(3f, 4.08f);
                    box.offset = new Vector2(0f, 1.22f);
                }
            },
            configure: (root, rig, created) =>
            {
                var visual = root.transform.Find(ProceduralRigTool.VisualName);
                var head = visual.Find("Head");
                var muzzle = head != null ? head.Find(EnemyAttack.MuzzleChildName) : null;
                if (rig.muzzle == null) rig.muzzle = muzzle;
                if (rig.muzzleFlashSprite == null)
                    rig.muzzleFlashSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderDisc);

                if (asleep != null || awake != null || attacking != null)
                {
                    rig.spriteSwaps.RemoveAll(s => s.renderer == null);
                    if (!rig.spriteSwaps.Exists(s => s.renderer != null && s.renderer.transform == head))
                    {
                        rig.spriteSwaps.Add(new RigSpriteSwap
                        {
                            renderer = head.GetComponent<SpriteRenderer>(),
                            asleep = asleep,
                            awake = awake,
                            attacking = attacking,
                        });
                    }
                }

                if (!created)
                {
                    log.AppendLine("  rig ya existía: valores afinados conservados.");
                    return;
                }

                rig.stretchPart = visual.Find("NeckPivot");
                rig.stretchRiders.Clear();
                if (head != null) rig.stretchRiders.Add(head);
                rig.foldedStretch = 0.05f;
                rig.foldedRiderScaleY = 0.6f;
                rig.foldedRiderOffset = new Vector2(0f, -0.05f);   // la cabeza se mete ~0.24 tras la base (que va delante)

                // Una torreta no flota ni se inclina; la cabeza apunta un poco y cabecea al disparar.
                rig.hoverBob = Vector2.zero;
                rig.leanDegrees = 0f;
                rig.aimPart = head;
                rig.aimArtAngle = 0f;
                rig.aimMaxAngle = 22f;
                rig.windup = 0.65f;          // el aviso (ojos de disparo) tiene que leerse
                rig.recover = 0.5f;
                rig.windupPullAngle = 4f;
                rig.windupBodyTilt = 0f;
                rig.recoilAngle = 9f;
                rig.recoilDistance = 0.3f;
                rig.bodyRecoil = 0f;
                rig.hurtTilt = 5f;
                rig.muzzleFlashSize = 0.7f;

                foreach (var part in rig.parts)
                {
                    if (part.part == null) continue;
                    switch (part.part.name)
                    {
                        case "Base":
                            part.swayMove = Vector2.zero;
                            part.swayRotate = 0f;
                            part.breaksOnDeath = false;   // la base se queda en el suelo
                            break;
                        case "neck":
                            part.swayMove = Vector2.zero;   // su movimiento es estirarse
                            part.swayRotate = 0f;
                            break;
                        case "Head":
                            part.swayMove = new Vector2(0f, 0.04f);
                            part.swayRotate = 1.5f;
                            part.swaySpeed = 1.3f;
                            break;
                    }
                }

                var stats = root.GetComponent<EnemyStats>();
                var t = stats.Tuning;
                t.archetype = EnemyArchetype.Static;
                t.staticAttack = AttackKind.Ranged;
                t.sleepsUntilDetected = true;
                t.maxHealth = 45f;
                t.detectionRange = 8f;
                t.loseInterestGrace = 1.5f;
                t.attackRange = 7.5f;
                t.verticalTolerance = 4f;
                t.knockbackResistance = 1f;   // una torreta anclada no sale despedida
                t.gravityScale = 3f;
                t.attackDamage = 12f;
                t.attackCooldown = 2f;
                t.contactDamage = 0f;
                t.aimAtTarget = true;
                t.projectile.prefab = laser;
                t.projectile.speed = 16f;
                t.projectile.lifetime = 1.2f;
                t.projectile.size = new Vector2(1.3f, 0.22f);
                t.projectile.faceDirection = true;
                t.projectileTint = new Color(0.45f, 1f, 0.95f, 1f);
                EditorUtility.SetDirty(stats);

                new BossAuthoring.Fields(root.GetComponent<Combat.Corpse>())
                    .Set("linger", 1.1f)
                    .Set("fadeDuration", 0.4f)
                    .Apply();

                log.AppendLine("  sembrado: Static+Ranged dormida, cuello plegable, láser placeholder.");
            });

            ProceduralRigTool.EnsureDriver(path, typeof(RigDriverCode), log);

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static void SetOrder(Transform t, int order)
        {
            var renderer = t != null ? t.GetComponent<SpriteRenderer>() : null;
            if (renderer != null) renderer.sortingOrder = order;
        }

        // ============================================================ proyectiles

        /// <summary>
        /// <c>Fx_&lt;enemy&gt;_Projectile</c>: un disco placeholder con <see cref="FxPlaceholderStyle"/>
        /// (lo tiñe y dimensiona el spec del enemigo). Arte real = editar el prefab, sin código.
        /// </summary>
        private static GameObject PlaceholderProjectile(string enemy, StringBuilder log,
                                                        string kind = "Projectile",
                                                        string spritePath = PlaceholderDisc)
        {
            string folder = $"{FxFolder}/{enemy}";
            string path = $"{folder}/Fx_{enemy}_{kind}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var prefab = ProjectilePrefabFactory.BuildFromSprite(folder, $"{enemy}_{kind}", sprite, 1f);

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
