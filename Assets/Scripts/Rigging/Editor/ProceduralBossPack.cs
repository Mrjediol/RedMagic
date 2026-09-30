using System.Collections.Generic;
using System.Text;
using RedMagic.Audio;
using RedMagic.Bosses;
using RedMagic.Bosses.EditorTools;
using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Gameplay.Movement;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// <b>Coloso de Hielo por partes</b> (Boss2): la misma pelea que el Coloso de Hielo — mismo
    /// esqueleto de definición (2 fases, 50%, ritmos, vida, contacto, movimiento) y los mismos
    /// assets de embestida, golpe al suelo y rocas —, pero con el cuerpo hecho de piezas sueltas
    /// animadas por <see cref="ProceduralRig"/> y, en lugar de sus dos puñetazos de sprite, tres
    /// ataques que sólo existen porque los puños están sueltos: <see cref="FistThrowAttack"/>,
    /// <see cref="LimbSlamAttack"/> (doble y en ráfaga) y <see cref="LimbPincerAttack"/>.
    ///
    /// Idempotente: los assets que ya existen no se tocan; el prefab se reorganiza y se siembra
    /// sólo la primera vez (sin rig todavía).
    /// </summary>
    public static class ProceduralBossPack
    {
        private const string BossFolder = "Assets/Resources/Bosses";
        private const string IceDefinition = BossFolder + "/Boss_ColosodeHielo.asset";
        private const string IcePrefab = "Assets/Prefabs/Enemies/Boss_ColosodeHielo.prefab";
        private const string DefinitionPath = BossFolder + "/Boss_ColosodeHielo2.asset";
        private const string PrefabPath = ProceduralEnemyPacks.Folder + "/BossIce2.prefab";

        /// <summary>Escala de la raíz visual: deja al coloso del tamaño del original (~7 u de alto).</summary>
        private const float VisualScale = 0.38f;

        /// <summary>Altura de la raíz visual sobre los pies: la punta de hielo flota ~0.6 u sobre el suelo.</summary>
        private const float VisualHeight = 3.9f;

        [MenuItem("Tools/RedMagic/Procedural/Packs/Coloso de Hielo por partes (BossIce2)")]
        public static void Build() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder();
            var accent = new Color(0.55f, 0.88f, 1f, 1f);

            var fistThrow = BossAuthoring.Attack<FistThrowAttack>(BossFolder, "BossAttack_Hielo2_LanzarPuno",
                "Lanzar Puño", "Echa atrás un puño temblando y lo dispara hacia donde estabas; vuelve solo.",
                accent, f => f
                    .Set("telegraph", 0.35f).Set("recovery", 0.8f).Set("damage", 20f)
                    .Set("gesture", "Brace")
                    .Set("windup", 0.6f).Set("flightSeconds", 0.32f).Set("overshoot", 1.5f)
                    .Set("embedSeconds", 0.45f).Set("returnSeconds", 0.55f));

            var doubleSlam = BossAuthoring.Attack<LimbSlamAttack>(BossFolder, "BossAttack_Hielo2_DobleMazazo",
                "Doble Mazazo", "Los dos puños suben y se estampan a cada lado de ti; las ondas corren hacia fuera.",
                accent, f => f
                    .Set("telegraph", 0.4f).Set("recovery", 1.1f).Set("damage", 18f)
                    .Set("gesture", "Brace")
                    .Set("mode", (int)LimbSlamAttack.SlamMode.Together).Set("spacing", 5.5f)
                    .Set("wavesPerImpact", 1).Set("waveDamage", 10f)
                    .Set("vulnerableSeconds", 1.2f).Set("vulnerableMultiplier", 1.8f)
                    .Set("shakeAmplitude", 0.35f).Set("shakeDuration", 0.3f));

            var slamBarrage = BossAuthoring.Attack<LimbSlamAttack>(BossFolder, "BossAttack_Hielo2_RafagaMazazos",
                "Ráfaga de Mazazos", "Cuatro mazazos alternando puño, cada uno sobre donde estás.",
                accent, f => f
                    .Set("telegraph", 0.3f).Set("recovery", 1f).Set("damage", 14f).Set("minPhase", 2)
                    .Set("gesture", "Brace")
                    .Set("mode", (int)LimbSlamAttack.SlamMode.Alternating).Set("slams", 4)
                    .Set("riseSeconds", 0.3f).Set("hangSeconds", 0.22f).Set("embedSeconds", 0.15f)
                    .Set("impactRadius", 1.3f).Set("wavesPerImpact", 0)
                    .Set("shakeAmplitude", 0.25f).Set("shakeDuration", 0.2f));

            var pincer = BossAuthoring.Attack<LimbPincerAttack>(BossFolder, "BossAttack_Hielo2_Tenaza",
                "Tenaza", "Un puño a cada lado de ti y palmada: sal por arriba o por un lado antes de que se cierren.",
                accent, f => f
                    .Set("telegraph", 0.3f).Set("recovery", 1.2f).Set("damage", 22f)
                    .Set("gesture", "Brace")
                    .Set("spread", 3.2f).Set("clapHeight", 1.6f).Set("holdSeconds", 0.5f)
                    .Set("vulnerableSeconds", 1f).Set("vulnerableMultiplier", 1.8f)
                    .Set("shakeAmplitude", 0.4f).Set("shakeDuration", 0.25f));

            var definition = Definition(log, fistThrow, doubleSlam, slamBarrage, pincer);

            ProceduralRigTool.Setup(PrefabPath, log,
                prepare: root => PrepareBody(root, definition, log),
                configure: (root, rig, created) => ConfigureRig(root, rig, created, log),
                enemyStack: false);

            ProceduralRigTool.EnsureVariant(PrefabPath, ProceduralRigTool.CodeSuffix, typeof(RigDriverCode), log);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ============================================================ definición

        /// <summary>
        /// Copia exacta de la del Coloso de Hielo (mismas fases, ritmos, escalas, sonido) con su
        /// propia clave de texto; en las barajas, los dos puñetazos de sprite pasan a ser los ataques
        /// de puños sueltos. Lo demás son los mismos assets: se afinan una vez para los dos.
        /// </summary>
        private static BossDefinition Definition(StringBuilder log, BossAttack fistThrow, BossAttack doubleSlam,
                                                 BossAttack slamBarrage, BossAttack pincer)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BossDefinition>(DefinitionPath);
            if (existing != null) return existing;

            var ice = AssetDatabase.LoadAssetAtPath<BossDefinition>(IceDefinition);
            var copy = Object.Instantiate(ice);
            AssetDatabase.CreateAsset(copy, DefinitionPath);

            var charge = Load("BossAttack_HieloEmbestida");
            var groundSlam = Load("BossAttack_HieloGolpeSuelo");
            var rock = Load("BossAttack_HieloLanzarRoca");
            var rocks = Load("BossAttack_HieloLanzarRocas");

            var so = new SerializedObject(copy);
            so.FindProperty("textKey").stringValue = "boss.coloso_de_hielo_2";
            so.FindProperty("displayName").stringValue = "Coloso de Hielo Desatado";
            SetDeck(so, 0, charge, groundSlam, rock, fistThrow, doubleSlam, pincer);
            SetDeck(so, 1, charge, groundSlam, rocks, fistThrow, slamBarrage, pincer);
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"  definición: {DefinitionPath} (copia del Coloso de Hielo, barajas con puños sueltos).");
            return copy;
        }

        private static BossAttack Load(string name) =>
            AssetDatabase.LoadAssetAtPath<BossAttack>($"{BossFolder}/{name}.asset");

        private static void SetDeck(SerializedObject so, int phase, params BossAttack[] attacks)
        {
            var list = so.FindProperty("phases").GetArrayElementAtIndex(phase).FindPropertyRelative("attacks");
            list.arraySize = attacks.Length;
            for (int i = 0; i < attacks.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = attacks[i];
        }

        // ============================================================ cuerpo

        /// <summary>
        /// Pila de jefe copiada del Coloso de Hielo (controlador, movimiento, vida, cadáver…) y
        /// jerarquía con nombres legibles. Sin Animator: el cuerpo lo anima el rig.
        /// </summary>
        private static void PrepareBody(GameObject root, BossDefinition definition, StringBuilder log)
        {
            var visual = root.transform.Find(ProceduralRigTool.VisualName);
            if (visual == null) return;

            Rename(visual, "Base/Flame", "Tip");
            Rename(visual, "Torso/Head", "Crown");
            Rename(visual, "Torso/LeftArm", "LeftFist");
            Rename(visual, "Torso/RightArm", "RightFist");

            if (root.GetComponent<BossController>() != null) return;   // ya preparado

            var ice = AssetDatabase.LoadAssetAtPath<GameObject>(IcePrefab);
            root.tag = ice.tag;
            root.layer = ice.layer;

            // La raíz a escala 1 (como el original) y el cuerpo escalado y alzado dentro.
            root.transform.localScale = Vector3.one;
            visual.localScale = Vector3.one * VisualScale;
            visual.localPosition = new Vector3(0f, VisualHeight, 0f);

            Copy<Rigidbody2D>(ice, root);
            var box = Copy<BoxCollider2D>(ice, root);
            box.size = new Vector2(3.4f, 4.4f);   // el torso: de 2.3 a 6.9 u sobre los pies
            box.offset = new Vector2(0f, 4.6f);

            Copy<Health>(ice, root);
            Copy<Knockback>(ice, root);
            Copy<HitFlash>(ice, root);
            Copy<Corpse>(ice, root);
            Copy<CurrencyDropper>(ice, root);
            Copy<Mover>(ice, root);
            Copy<SoundEmitter>(ice, root);
            var controller = Copy<BossController>(ice, root);
            ProceduralRigTool.Get<BossAnimator>(root);

            var torso = visual.Find("Torso");
            new BossAuthoring.Fields(controller)
                .SetObject("definition", definition)
                .SetObject("bodyRenderer", torso != null ? torso.GetComponent<SpriteRenderer>() : null)
                .Apply();

            // Las piezas tienen que caer antes de desvanecerse.
            new BossAuthoring.Fields(root.GetComponent<Corpse>()).Set("linger", 1.4f).Set("fadeDuration", 0.5f).Apply();

            var limbs = ProceduralRigTool.Get<BossLimbs>(root);
            new BossAuthoring.Fields(limbs)
                .SetObjectArray("limbs", visual.Find("Torso/LeftFist"), visual.Find("Torso/RightFist"))
                .Set("contactRadius", 0.9f)
                .Apply();

            log.AppendLine("  pila de jefe copiada del Coloso de Hielo; puños en BossLimbs.");
        }

        private static void Rename(Transform visual, string path, string name)
        {
            var t = visual.Find(path);
            if (t != null) t.name = name;
        }

        private static T Copy<T>(GameObject from, GameObject to) where T : Component
        {
            var target = ProceduralRigTool.Get<T>(to);
            var source = from.GetComponent<T>();
            if (source != null) EditorUtility.CopySerialized(source, target);
            return target;
        }

        // ============================================================ rig

        private static void ConfigureRig(GameObject root, ProceduralRig rig, bool created, StringBuilder log)
        {
            EnsureGrips(root, rig, log);

            if (!created)
            {
                log.AppendLine("  rig ya existía: valores afinados conservados.");
                return;
            }

            var visual = rig.body;
            var torso = visual.Find("Torso");
            var crown = visual.Find("Torso/Crown");
            var left = visual.Find("Torso/LeftFist");
            var right = visual.Find("Torso/RightFist");
            var tip = visual.Find("Base/Tip");

            // Flota despacio; se inclina mucho al deslizarse (es pesado).
            rig.hoverBob = new Vector2(0f, 0.18f);
            rig.hoverSpeed = 1.3f;
            rig.leanDegrees = 10f;
            rig.leanFullSpeed = 4f;
            rig.leanResponse = 4f;
            rig.hurtTilt = 6f;
            rig.hurtSquash = 0.05f;
            rig.staggerStrength = 3f;
            rig.breakImpulse = 4f;
            rig.breakUpward = 5f;
            rig.breakSpin = 360f;

            // Medidas sobre el dibujo (lienzos de 2048 px a 100 px/u): punto de giro y centro visible.
            foreach (var part in rig.parts)
            {
                if (part.part == null) continue;
                switch (part.part.name)
                {
                    case "Torso":
                        Sway(part, new Vector2(0f, 0.08f), 1.2f, 1.1f, 0f);
                        break;
                    case "Crown":
                        // La corona flota sola, a su ritmo, sobre la cabeza.
                        part.pivot = part.center = new Vector2(-0.6f, 1.9f);
                        Sway(part, new Vector2(0f, 0.45f), 4f, 1.9f, 1.3f);
                        break;
                    case "LeftFist":
                        part.pivot = new Vector2(0f, 1f);
                        part.pointAngle = -90f;   // los nudillos, hacia abajo en el dibujo
                        part.center = new Vector2(0.5f, -2.7f);
                        part.leanFollow = -0.5f;
                        Sway(part, new Vector2(0.1f, 0.35f), 5f, 1.5f, 0f);
                        break;
                    case "RightFist":
                        part.pivot = new Vector2(0f, 1f);
                        part.pointAngle = -90f;
                        part.center = new Vector2(-0.3f, -2.1f);
                        part.leanFollow = -0.5f;
                        Sway(part, new Vector2(-0.1f, 0.35f), 5f, 1.5f, Mathf.PI);   // a contratiempo
                        break;
                    case "Base":
                        Sway(part, Vector2.zero, 0f, 1f, 0f);
                        break;
                    case "Tip":
                        // La punta cuelga: gira desde arriba y se queda atrás como un péndulo.
                        part.pivot = new Vector2(-0.2f, 1f);
                        part.center = new Vector2(-0.2f, -1.6f);
                        part.leanFollow = -1.2f;
                        Sway(part, new Vector2(0f, 0.1f), 5f, 1.2f, 2.1f);
                        break;
                }
            }

            rig.gestures.Clear();
            rig.gestures.Add(Gesture("Brace", 0.08f, 0.4f, 2f,
                new[] { P(visual, new Vector2(0f, -0.25f), 3f), P(crown, new Vector2(0f, 1.2f), 0f) },
                new[] { P(visual, new Vector2(0f, 0.1f), -2f) }));
            rig.gestures.Add(Gesture("Dash", 0.1f, 0.4f, 2f,
                new[] { P(visual, new Vector2(-0.5f, 0.2f), 8f), P(left, new Vector2(-2f, 1f), 20f),
                        P(right, new Vector2(-2f, 1f), 20f), P(tip, Vector2.zero, 15f) },
                new[] { P(visual, new Vector2(0.5f, -0.1f), -14f), P(left, new Vector2(3f, 0f), -30f),
                        P(right, new Vector2(3f, 0f), -30f) }));
            rig.gestures.Add(Gesture("DashLoop", 0.1f, 0.35f, 0f,
                new[] { P(visual, new Vector2(0.3f, -0.2f), -16f), P(left, new Vector2(-3f, 1f), 35f),
                        P(right, new Vector2(-3f, 1f), 35f), P(tip, Vector2.zero, 25f),
                        P(crown, new Vector2(-1f, 0f), 10f) },
                new RigPose[0]));
            rig.gestures.Add(Gesture("GroundSlam", 0.07f, 0.5f, 2.5f,
                new[] { P(visual, new Vector2(0f, 0.8f), 0f, new Vector2(-0.04f, 0.05f)),
                        P(crown, new Vector2(0f, 2f), 0f), P(left, new Vector2(1f, 5f), -25f),
                        P(right, new Vector2(-1f, 5f), 25f) },
                new[] { P(visual, new Vector2(0f, -0.6f), 0f, new Vector2(0.08f, -0.08f)),
                        P(crown, new Vector2(0f, -1f), 0f), P(left, new Vector2(1f, -3f), 10f),
                        P(right, new Vector2(-1f, -3f), -10f) }));
            rig.gestures.Add(Gesture("PickUP", 0.12f, 0.35f, 1f,
                new[] { P(visual, new Vector2(0f, -0.3f), -6f), P(right, new Vector2(2f, -3f), -20f) },
                new[] { P(visual, Vector2.zero, 4f), P(right, new Vector2(0f, 5f), 30f) }));
            rig.gestures.Add(Gesture("Throw", 0.08f, 0.4f, 1.5f,
                new[] { P(visual, Vector2.zero, 8f), P(right, new Vector2(-2.5f, 4f), 40f) },
                new[] { P(visual, new Vector2(0.2f, 0f), -10f), P(right, new Vector2(4f, 1f), -40f) }));

            log.AppendLine("  rig sembrado: corona flotante, puños a contratiempo, punta en péndulo, 6 gestos.");
        }

        /// <summary>
        /// Un hijo "Grip" en el centro visible de cada puño (donde se agarra la roca) y la lista de
        /// agarres de <see cref="BossLimbs"/>. Idempotente: corre en cada pasada.
        /// </summary>
        private static void EnsureGrips(GameObject root, ProceduralRig rig, StringBuilder log)
        {
            var limbs = root.GetComponent<BossLimbs>();
            if (limbs == null) return;

            var grips = new List<Object>();
            for (int i = 0; i < limbs.Count; i++)
            {
                var fist = limbs.Get(i);
                if (fist == null) continue;

                var grip = fist.Find("Grip");
                if (grip == null)
                {
                    var data = rig.FindPart(fist);
                    grip = new GameObject("Grip").transform;
                    grip.SetParent(fist, false);
                    grip.localPosition = data != null ? (Vector3)data.center : Vector3.zero;
                    log.AppendLine($"  agarre creado en {fist.name}.");
                }

                grips.Add(grip);
            }

            new BossAuthoring.Fields(limbs).SetObjectArray("grips", grips.ToArray()).Apply();
        }

        private static void Sway(RigPart part, Vector2 move, float rotate, float speed, float phase)
        {
            part.swayMove = move;
            part.swayRotate = rotate;
            part.swaySpeed = speed;
            part.swayPhase = phase;
        }

        private static RigPose P(Transform part, Vector2 move, float rotate, Vector2 scale = default) =>
            new RigPose { part = part, move = move, rotate = rotate, scale = scale };

        private static RigGesture Gesture(string name, float strikeTime, float recover, float shake,
                                          RigPose[] windup, RigPose[] strike) =>
            new RigGesture
            {
                name = name,
                strikeTime = strikeTime,
                recover = recover,
                shake = shake,
                windup = new List<RigPose>(windup),
                strike = new List<RigPose>(strike),
            };
    }
}
