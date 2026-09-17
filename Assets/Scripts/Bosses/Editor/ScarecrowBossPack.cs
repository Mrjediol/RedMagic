using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Crea el segundo jefe del juego — el <b>Espantapájaros Marchito</b> — con sus ataques, su
    /// <see cref="BossDefinition"/>, su prefab y el cuervo que invoca.
    ///
    /// Es <b>idempotente</b> como el pack del Árbol Ancestral: no pisa nada que ya exista, así que
    /// se puede relanzar después de haber afinado números a mano y sólo aparecerá lo que falte.
    ///
    /// <b>Qué lo hace distinto del primer jefe</b> (que es de lo que va tener dos):
    /// <list type="bullet">
    /// <item>El árbol pregunta por la <b>altura</b> (saltar la onda baja, agacharse bajo la alta).
    /// El espantapájaros pregunta por la <b>distancia</b> con su guadaña giratoria
    /// (<see cref="SweepBeamAttack"/>) y por el <b>sitio</b> con sus refugios
    /// (<see cref="SafeZoneAttack"/>), que obligan a soltarlo y correr.</item>
    /// <item>Sus esbirros <b>vuelan</b> (cuervos con <c>EnemyController.canFly</c>): el árbol
    /// invocaba brotes que se quedaban en el suelo y se podían ignorar corriendo.</item>
    /// <item>Cada patrón de proyectiles usa su propio prop del pack de Cainos —calabazas y
    /// abrojos— en vez del cuadrado genérico.</item>
    /// </list>
    /// </summary>
    public static class ScarecrowBossPack
    {
        private const string BossFolder = "Assets/Resources/Bosses";
        private const string PrefabFolder = "Assets/Prefabs/Enemies";
        private const string PrefabPath = PrefabFolder + "/Boss_EspantapajarosMarchito.prefab";
        private const string CrowPrefabPath = PrefabFolder + "/Enemy_Cuervo.prefab";
        private const string DefinitionPath = BossFolder + "/Boss_EspantapajarosMarchito.asset";

        // BROKEN BY DESIGN since the vendor-asset cleanup — both Cainos/ and Brackeys/ were deleted
        // from the project (see docs/folder-restructure-audit.md / Assets/Editor/
        // VendorCleanupMigration.cs). The already-committed Boss_EspantapajarosMarchito.prefab and
        // its BossAttack_*.asset were retargeted to the placeholder square as part of that same
        // pass, so this pack does NOT need to run again for the shipped boss — these two constants
        // are left pointing at paths that no longer exist rather than silently retargeted, since
        // both call sites (BossAuthoring.LoadSprite below, and the direct LoadAssetAtPath at
        // renderer.sprite = ... further down) already fail loudly with a Debug.LogWarning/null
        // instead of throwing or misbehaving quietly. Point these at real paths first if this pack
        // is ever genuinely re-run.
        private const string PropsSheet =
            "Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";

        private const string CrowTexture = "Assets/Brackeys/2D Mega Pack/Enemies/Crow.png";

        private const string BossScenePath = "Assets/Scenes/Worlds/World2/World2_Boss.unity";

        /// <summary>
        /// Escala del sprite del espantapájaros (64×90 px a 32 px/unidad = 2,0 × 2,8 unidades).
        /// A ×3,2 mide unas 9 unidades: con el tamaño ortográfico 8,5 de la cámara caben 17, así
        /// que ocupa media pantalla — imponente, pero dejando sitio para leer sus ataques.
        /// </summary>
        private const float VisualScale = 3.2f;

        /// <summary>
        /// Caja de golpeo: sólo el poste y el cuerpo, no los brazos. Estrecha a propósito — es un
        /// muro sólido en mitad de la arena y los refugios de la Espantada obligan a cruzarla.
        /// </summary>
        private const float ColliderWidthFactor = 0.24f;
        private const float ColliderHeightFactor = 0.92f;

        /// <summary>Fase 1: ámbar de rastrojo seco. Fase 2: el morado sangriento de la noche.</summary>
        private static readonly Color PhaseOneAccent = new Color(0.91f, 0.64f, 0.24f, 1f);
        private static readonly Color PhaseTwoAccent = new Color(0.72f, 0.24f, 0.42f, 1f);

        /// <summary>Color de los refugios de <see cref="SafeZoneAttack"/>: nunca el de la fase.</summary>
        private static readonly Color SafeAccent = new Color(0.96f, 0.94f, 0.74f, 0.85f);

        // ==================================================================== menú

        [MenuItem("Tools/RedMagic/Boss/Crear jefe: Espantapajaros Marchito")]
        public static void Generate()
        {
            BossAuthoring.EnsureTag("Enemy");
            BossAuthoring.EnsureFolder(BossFolder);
            BossAuthoring.EnsureFolder(PrefabFolder);

            var crow = CreateCrow();
            var attacks = CreateAttacks(crow);
            var definition = CreateDefinition(attacks);
            var prefab = CreatePrefab(definition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ScarecrowBossPack] Jefe listo: {definition.name}. " +
                      $"Colócalo con Tools > RedMagic > Boss > Colocar Espantapajaros en World2_Boss.",
                      definition);

            Selection.activeObject = prefab != null ? (Object)prefab : definition;
        }

        [MenuItem("Tools/RedMagic/Boss/Colocar Espantapajaros en World2_Boss")]
        public static void PlaceInBossScene() =>
            BossAuthoring.PlaceBossInScene(PrefabPath, BossScenePath, x: 6f, fallbackY: -3f);

        // ==================================================================== ataques

        private sealed class Attacks
        {
            public BossAttack StrawScythe;
            public BossAttack PumpkinRain;
            public BossAttack BurrVolley;
            public BossAttack Scatter;
            public BossAttack CrowFlock;

            public BossAttack DoubleReap;
            public BossAttack CrowNight;
            public BossAttack BurrWhirl;
            public BossAttack PumpkinDeluge;
            public BossAttack StakeSlam;
        }

        private static Attacks CreateAttacks(GameObject crow)
        {
            var a = new Attacks();

            var pumpkin = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Pumpkin 01");
            var burr = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Spike Ball");

            // ---------------------------------------------------------------- fase 1

            // La firma del jefe. El brazo pivota a 3,2 de altura, así que lo que decide si te pilla
            // es la distancia: pegado al poste baja a plomo, a media arena tarda pero llega rápido.
            a.StrawScythe = Attack<SweepBeamAttack>("BossAttack_GuadanaDePaja", "Guadaña de Paja",
                "Estira el brazo y siega de arriba abajo. No mires la altura: mira a qué distancia estás.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.95f).Set("recovery", 1.3f).Set("weight", 1.4f)
                    .Set("damage", 22f).Set("knockbackMultiplier", 1.7f)
                    .Set("shakeAmplitude", 0.35f).Set("shakeDuration", 0.3f)
                    .Set("pivotOffset", new Vector2(0f, 3.2f))
                    .Set("fromAngle", 82f).Set("toAngle", -6f)
                    .Set("sweepSeconds", 1f).Set("sweepTowardPlayer", true).Set("bothSides", false)
                    .Set("sweeps", 1).Set("width", 0.85f));

            a.PumpkinRain = Attack<BulletHellAttack>("BossAttack_LluviaDeCalabazas", "Lluvia de Calabazas",
                "Marca el suelo y deja caer calabazas sobre las marcas. Muévete.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.5f).Set("recovery", 1.05f).Set("weight", 1.2f)
                    .Set("damage", 13f).Set("knockbackMultiplier", 0.6f)
                    .Set("pattern", (int)BulletPattern.Rain)
                    .Set("volleys", 2).Set("timeBetweenVolleys", 0.5f)
                    .Set("bulletsPerVolley", 7)
                    .Set("rainSpan", 0.95f).Set("rainMarkerSeconds", 0.55f).Set("rainJitter", 0.8f)
                    .Set("projectile.speed", 14f).Set("projectile.lifetime", 3f)
                    .Set("projectile.size", new Vector2(0.62f, 0.62f))
                    .SetObject("projectileSprite", pumpkin));

            a.BurrVolley = Attack<BulletHellAttack>("BossAttack_SalvaDeAbrojos", "Salva de Abrojos",
                "Tres abanicos de abrojos apuntados. Hay hueco: no te quedes en línea recta.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.6f).Set("recovery", 0.9f).Set("weight", 1.25f)
                    .Set("damage", 11f).Set("knockbackMultiplier", 0.5f)
                    .Set("pattern", (int)BulletPattern.Fan)
                    .Set("volleys", 3).Set("timeBetweenVolleys", 0.3f)
                    .Set("bulletsPerVolley", 5)
                    .Set("arcDegrees", 50f).Set("spinPerVolley", 10f).Set("alternateSpin", true)
                    .Set("aimAtPlayer", true).Set("randomSpread", 1.5f)
                    .Set("originOffset", new Vector2(0f, 2.8f)).Set("spawnRadius", 1f)
                    .Set("projectile.speed", 11f).Set("projectile.lifetime", 3.5f)
                    .Set("projectile.size", new Vector2(0.45f, 0.45f))
                    .SetObject("projectileSprite", burr));

            // El ataque que rompe la rutina: hay que dejar de pegarle e ir a un sitio concreto.
            // Tres refugios y 1,7 s son de sobra desde cualquier punto de la arena: en fase 1 esto
            // enseña la regla, y la fase 2 es la que la aprieta.
            a.Scatter = Attack<SafeZoneAttack>("BossAttack_Espantada", "Espantada",
                "Los cuervos cubren el campo entero. Sólo se salvan los círculos marcados: corre a uno.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.8f).Set("recovery", 1.5f).Set("weight", 1f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 26f).Set("knockbackMultiplier", 1.2f)
                    .Set("shakeAmplitude", 0.6f).Set("shakeDuration", 0.45f)
                    .Set("safeSpots", 3).Set("spotRadius", 2.4f)
                    .Set("minSeparation", 6f).Set("edgeMargin", 2.5f)
                    .Set("markerSeconds", 1.7f).Set("pulses", 1).Set("moveSpotsEachPulse", false)
                    .Set("guaranteeReachable", true).Set("reachableRadius", 10f)
                    .Set("safeColor", SafeAccent));

            a.CrowFlock = Attack<SummonAddsAttack>("BossAttack_BandadaDeCuervos", "Bandada de Cuervos",
                "Suelta cuervos que vuelan hacia ti. No se van a ir solos.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.85f).Set("recovery", 1.4f).Set("weight", 0.8f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 0f)
                    .Set("count", 2).Set("maxAlive", 4)
                    .Set("markerSeconds", 0.6f).Set("timeBetweenSpawns", 0.15f)
                    .Set("distanceRange", new Vector2(5f, 11f))
                    // Aparecen en alto: son cuervos, no brotes. Vuelan, así que no hace falta
                    // que nazcan pisando el suelo.
                    .Set("spawnHeight", 3.2f)
                    .SetObjectArray("prefabs", crow));

            // ---------------------------------------------------------------- fase 2

            // La guadaña, pero a los dos lados y de ida y vuelta: se acabó lo de ponerse detrás.
            a.DoubleReap = Attack<SweepBeamAttack>("BossAttack_SiegaDoble", "Siega Doble",
                "Siega a los dos lados, ida y vuelta. Elige bien la distancia antes de que empiece.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 1.05f).Set("recovery", 1.6f).Set("weight", 1.3f)
                    .Set("damage", 24f).Set("knockbackMultiplier", 1.7f)
                    .Set("shakeAmplitude", 0.45f).Set("shakeDuration", 0.35f)
                    .Set("pivotOffset", new Vector2(0f, 3.2f))
                    .Set("fromAngle", 86f).Set("toAngle", -6f)
                    .Set("sweepSeconds", 0.85f).Set("sweepTowardPlayer", true).Set("bothSides", true)
                    .Set("sweeps", 2).Set("timeBetweenSweeps", 0.45f).Set("returnSweep", true)
                    .Set("width", 0.9f));

            // La Espantada apretada: dos refugios, menos tiempo y cambian de sitio entre golpes.
            // Deja de ser "ve a un sitio" y pasa a ser una carrera de refugio en refugio.
            a.CrowNight = Attack<SafeZoneAttack>("BossAttack_NocheDeCuervos", "Noche de Cuervos",
                "Dos refugios, y cambian de sitio entre golpe y golpe. No te quedes en el primero.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.75f).Set("recovery", 1.6f).Set("weight", 1.2f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 24f).Set("knockbackMultiplier", 1.2f)
                    .Set("shakeAmplitude", 0.6f).Set("shakeDuration", 0.45f)
                    .Set("safeSpots", 2).Set("spotRadius", 2.2f)
                    .Set("minSeparation", 9f).Set("edgeMargin", 2.5f)
                    .Set("markerSeconds", 1.35f).Set("pulses", 2).Set("timeBetweenPulses", 0.5f)
                    .Set("moveSpotsEachPulse", true)
                    .Set("guaranteeReachable", true).Set("reachableRadius", 8f)
                    .Set("safeColor", SafeAccent));

            a.BurrWhirl = Attack<BulletHellAttack>("BossAttack_TorbellinoDeAbrojos", "Torbellino de Abrojos",
                "Un anillo de abrojos que gira. El hueco se mueve: síguelo.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.7f).Set("recovery", 1.15f).Set("weight", 1.5f)
                    .Set("damage", 10f).Set("knockbackMultiplier", 0.35f)
                    .Set("pattern", (int)BulletPattern.Radial)
                    .Set("volleys", 6).Set("timeBetweenVolleys", 0.22f)
                    .Set("bulletsPerVolley", 13)
                    .Set("arcDegrees", 360f).Set("spinPerVolley", 13f).Set("aimAtPlayer", false)
                    .Set("originOffset", new Vector2(0f, 3.4f)).Set("spawnRadius", 1.3f)
                    .Set("projectile.speed", 7.5f).Set("projectile.lifetime", 5f)
                    .Set("projectile.size", new Vector2(0.42f, 0.42f))
                    .SetObject("projectileSprite", burr));

            a.PumpkinDeluge = Attack<BulletHellAttack>("BossAttack_DiluvioDeCalabazas", "Diluvio de Calabazas",
                "La lluvia, pero en serio. Tres tandas y casi sin huecos fijos.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.5f).Set("recovery", 1.2f).Set("weight", 1.1f)
                    .Set("damage", 13f).Set("knockbackMultiplier", 0.6f)
                    .Set("pattern", (int)BulletPattern.Rain)
                    .Set("volleys", 3).Set("timeBetweenVolleys", 0.42f)
                    .Set("bulletsPerVolley", 10)
                    .Set("rainSpan", 1f).Set("rainMarkerSeconds", 0.45f).Set("rainJitter", 1f)
                    .Set("projectile.speed", 16f).Set("projectile.lifetime", 3f)
                    .Set("projectile.size", new Vector2(0.62f, 0.62f))
                    .SetObject("projectileSprite", pumpkin));

            // Una onda rasante clásica: el jugador que vino del Árbol Ancestral ya sabe leerla, y
            // que aquí siga significando lo mismo es lo que hace que aprender tenga premio.
            a.StakeSlam = Attack<ShockwaveAttack>("BossAttack_Estacazo", "Estacazo",
                "Arranca el poste y lo estrella contra el suelo. Salta por encima de la onda.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.85f).Set("recovery", 1.35f).Set("weight", 1.1f)
                    .Set("damage", 23f).Set("knockbackMultiplier", 1.6f)
                    .Set("shakeAmplitude", 0.5f).Set("shakeDuration", 0.4f)
                    .Set("bandMin", 0f).Set("bandMax", 1.9f)
                    .Set("waves", 2).Set("timeBetweenWaves", 0.6f).Set("speedRampPerWave", 1.15f)
                    .Set("bothDirections", true)
                    .Set("width", 1.7f).Set("speed", 15f).Set("spawnInset", 1.4f));

            return a;
        }

        private static T Attack<T>(string fileName, string displayName, string description, Color accent,
                                   System.Func<BossAuthoring.Fields, BossAuthoring.Fields> configure)
            where T : BossAttack =>
            BossAuthoring.Attack<T>(BossFolder, fileName, displayName, description, accent, configure);

        // ==================================================================== definición

        private static BossDefinition CreateDefinition(Attacks a)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BossDefinition>(DefinitionPath);
            if (existing != null) return existing;

            var definition = ScriptableObject.CreateInstance<BossDefinition>();
            AssetDatabase.CreateAsset(definition, DefinitionPath);

            var so = new SerializedObject(definition);
            so.FindProperty("displayName").stringValue = "Espantapájaros Marchito";
            so.FindProperty("title").stringValue = "Señor de la Cosecha Muerta";
            so.FindProperty("description").stringValue =
                "Lo clavaron en el campo para espantar a los cuervos. Llevan tanto tiempo " +
                "obedeciéndole que ya no saben distinguir entre espantar y devorar.";

            var phases = so.FindProperty("phases");
            phases.arraySize = 2;

            BossAuthoring.WritePhase(phases.GetArrayElementAtIndex(0),
                "Cosecha Seca", startsAtHealth: 1f, damageScale: 1f, speedScale: 1f,
                accent: PhaseOneAccent, pause: new Vector2(1f, 1.7f),
                transitionSeconds: 0f, frenzyBelow: 0f,
                attacks: new[] { a.StrawScythe, a.PumpkinRain, a.BurrVolley, a.Scatter, a.CrowFlock });

            // Fase 2: la misma baraja apretada más los patrones que piden leer de verdad. Por
            // debajo del 20% entra en frenesí y deja de dar respiro.
            BossAuthoring.WritePhase(phases.GetArrayElementAtIndex(1),
                "Noche de Cuervos", startsAtHealth: 0.55f, damageScale: 1.15f, speedScale: 1.2f,
                accent: PhaseTwoAccent, pause: new Vector2(0.7f, 1.2f),
                transitionSeconds: 2f, frenzyBelow: 0.2f,
                attacks: new[]
                {
                    a.DoubleReap, a.CrowNight, a.BurrWhirl, a.PumpkinDeluge,
                    a.StakeSlam, a.BurrVolley, a.CrowFlock
                });

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            return definition;
        }

        // ==================================================================== prefab del jefe

        private static GameObject CreatePrefab(BossDefinition definition)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("Boss_EspantapajarosMarchito");
            root.transform.position = Vector3.zero;
            BossAuthoring.TrySetTag(root, "Enemy");

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * VisualScale;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Scarecrow");
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 5;

            // El origen del jefe es su base: así 'MeasureGroundY' y la colocación en la escena
            // funcionan poniéndolo simplemente a la altura del suelo.
            var bounds = renderer.bounds;
            visual.transform.localPosition += new Vector3(0f, -bounds.min.y, 0f);
            bounds = renderer.bounds;

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.freezeRotation = true;

            var collider = root.AddComponent<BoxCollider2D>();
            float width = Mathf.Max(1f, bounds.size.x * ColliderWidthFactor);
            float height = Mathf.Max(1f, bounds.size.y * ColliderHeightFactor);
            collider.size = new Vector2(width, height);
            collider.offset = new Vector2(0f, height * 0.5f);

            var health = root.AddComponent<Health>();
            new BossAuthoring.Fields(health)
                .Set("maxHealth", 3000f)
                .Set("currentHealth", 3000f)
                // Sin i-frames, como el resto de enemigos: si no, una escopeta de 5 perdigones
                // sólo le metería uno.
                .Set("invulnerabilityDuration", 0f)
                .Apply();

            var knockback = root.AddComponent<Knockback>();
            new BossAuthoring.Fields(knockback).Set("immune", true).Set("resistance", 1f).Apply();

            root.AddComponent<HitFlash>();

            var corpse = root.AddComponent<Corpse>();
            new BossAuthoring.Fields(corpse).Set("linger", 1.6f).Set("fadeDuration", 1.4f).Apply();

            var dropper = root.AddComponent<CurrencyDropper>();
            new BossAuthoring.Fields(dropper).Set("tier", (int)EnemyTier.Boss).Apply();

            var controller = root.AddComponent<BossController>();
            new BossAuthoring.Fields(controller)
                .SetObject("definition", definition)
                .Set("arenaHalfWidth", 18f)
                .Set("arenaHeight", 13f)
                .Set("activationRadius", 18f)
                .Set("introSeconds", 2.2f)
                // Vacío a propósito: 'Music_Boss' todavía no está dado de alta en las listas del
                // AudioManager, y un id inexistente sólo dejaría un warning por combate.
                .Set("musicId", string.Empty)
                .Set("contactDamage", 14f)
                .Set("contactDamageCooldown", 0.8f)
                .Set("contactKnockbackMultiplier", 1.4f)
                // Clavado en una estaca: se balancea bastante más que un árbol enraizado, y eso es
                // lo que le da vida a un jefe que no se mueve del sitio.
                .Set("swayDegrees", 3.2f)
                .Set("swaySpeed", 1.5f)
                .Apply();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }

        // ==================================================================== el cuervo

        /// <summary>
        /// El esbirro del jefe: un enemigo volador normal y corriente
        /// (<c>EnemyController.canFly</c>), no un bicho especial de jefe. Así sirve igual para
        /// poblar una sección del mundo 2, y el jefe sólo tiene que apuntar a su prefab.
        /// </summary>
        private static GameObject CreateCrow()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CrowPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("Enemy_Cuervo");
            BossAuthoring.TrySetTag(root, "Enemy");

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.7f;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CrowTexture);
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 4;

            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 1f;   // EnemyController lo pone a 0 en Awake cuando vuela
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 0.75f);
            collider.offset = new Vector2(0f, 0.05f);

            var health = root.AddComponent<Health>();
            new BossAuthoring.Fields(health)
                .Set("maxHealth", 22f).Set("currentHealth", 22f)
                .Set("invulnerabilityDuration", 0f)
                .Apply();

            var controller = root.AddComponent<EnemyController>();
            new BossAuthoring.Fields(controller)
                .Set("canFly", true)
                .Set("patrolSpeed", 2.4f).Set("patrolDistance", 3f)
                .Set("chasePlayer", true).Set("chaseSpeed", 4.4f)
                .Set("detectionRadius", 16f).Set("loseSightRadius", 24f)
                .Set("contactDamage", 8f).Set("contactDamageCooldown", 1f)
                .Set("contactKnockbackMultiplier", 1f)
                .Apply();

            var knockback = root.AddComponent<Knockback>();
            new BossAuthoring.Fields(knockback).Set("resistance", 0.6f).Apply();

            root.AddComponent<HitFlash>();
            root.AddComponent<UI.HealthBarUI>();

            var corpse = root.AddComponent<Corpse>();
            new BossAuthoring.Fields(corpse).Set("linger", 0.35f).Set("fadeDuration", 0.3f).Apply();

            var dropper = root.AddComponent<CurrencyDropper>();
            new BossAuthoring.Fields(dropper).Set("tier", (int)EnemyTier.Basic).Apply();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CrowPrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }
    }
}
