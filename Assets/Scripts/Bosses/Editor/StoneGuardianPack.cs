using RedMagic.Combat;
using RedMagic.Economy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Crea el tercer jefe: la <b>Guardiana de Piedra</b>, con sus ataques, su
    /// <see cref="BossDefinition"/> y su prefab. Idempotente como los otros dos packs.
    ///
    /// <b>La idea del combate.</b> Los dos primeros jefes se resuelven esquivando: mientras no te
    /// den, puedes pegar. Ésta va <b>acorazada</b> (la fase 1 le baja el daño recibido al 30%), así
    /// que machacarla sin más no funciona; lo que la mata son las <b>ventanas de castigo</b> que
    /// abre ella misma al clavar el puño en el suelo. Eso invierte la pregunta del jugador: ya no
    /// es "¿cómo evito su ataque más peligroso?" sino "¿cómo me coloco para poder castigarlo?" —
    /// hay que acercarse justo a lo que da miedo, que es lo contrario de lo que pide el instinto.
    ///
    /// La segunda mitad añade la otra vuelta de tuerca: el <b>escombro</b>
    /// (<see cref="HazardFieldAttack"/>) se queda en el suelo, así que la arena se encoge y las
    /// esquivas de todos los demás patrones se estrechan sin que esos patrones cambien en nada.
    ///
    /// Fase 2 le agrieta la coraza (sube al 60% de daño recibido) pero acorta las ventanas: el
    /// final del combate es más rápido y menos perdonavidas por los dos lados.
    /// </summary>
    public static class StoneGuardianPack
    {
        private const string BossFolder = "Assets/Resources/Bosses";
        private const string PrefabFolder = "Assets/Prefab/Enemies";
        private const string PrefabPath = PrefabFolder + "/Boss_GuardianaDePiedra.prefab";
        private const string DefinitionPath = BossFolder + "/Boss_GuardianaDePiedra.asset";

        private const string PropsSheet =
            "Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";

        private const string BossScenePath = "Assets/Scenes/Worlds/World3/World3_Boss.unity";

        /// <summary>
        /// La estatua mide 35×77 px a 32 px/unidad = 1,1 × 2,4 unidades. A ×3,7 se queda en unas
        /// 8,9 — la misma altura que el espantapájaros, para que los tres jefes se lean como
        /// bichos de la misma liga.
        /// </summary>
        private const float VisualScale = 3.7f;

        /// <summary>Caja de golpeo: el bloque de piedra, sin el pedestal que sobresale.</summary>
        private const float ColliderWidthFactor = 0.5f;
        private const float ColliderHeightFactor = 0.94f;

        /// <summary>Fase 1: piedra fría. Fase 2: la grieta al rojo.</summary>
        private static readonly Color PhaseOneAccent = new Color(0.55f, 0.68f, 0.82f, 1f);
        private static readonly Color PhaseTwoAccent = new Color(0.93f, 0.42f, 0.22f, 1f);

        // ==================================================================== menú

        [MenuItem("Tools/RedMagic/Boss/Crear jefe: Guardiana de Piedra")]
        public static void Generate()
        {
            BossAuthoring.EnsureTag("Enemy");
            BossAuthoring.EnsureFolder(BossFolder);
            BossAuthoring.EnsureFolder(PrefabFolder);

            var attacks = CreateAttacks();
            var definition = CreateDefinition(attacks);
            var prefab = CreatePrefab(definition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StoneGuardianPack] Jefe listo: {definition.name}. " +
                      $"Colócalo con Tools > RedMagic > Boss > Colocar Guardiana en World3_Boss.",
                      definition);

            Selection.activeObject = prefab != null ? (Object)prefab : definition;
        }

        [MenuItem("Tools/RedMagic/Boss/Colocar Guardiana en World3_Boss")]
        public static void PlaceInBossScene() =>
            BossAuthoring.PlaceBossInScene(PrefabPath, BossScenePath, x: 6f, fallbackY: -3f);

        // ==================================================================== ataques

        private sealed class Attacks
        {
            public BossAttack Fist;
            public BossAttack Rockfall;
            public BossAttack Fracture;
            public BossAttack Burial;

            public BossAttack DoubleFist;
            public BossAttack Collapse;
            public BossAttack Shards;
            public BossAttack Earthquake;
            public BossAttack Shrapnel;
        }

        private static Attacks CreateAttacks()
        {
            var a = new Attacks();

            var rock = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Rock 03");
            var boulder = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Rock 01");

            // ---------------------------------------------------------------- fase 1

            // El corazón del combate: pega durísimo, marca dónde va a caer, y al clavar el puño se
            // queda expuesta 2,6 s. Peso alto porque es la única forma decente de hacerle daño;
            // si saliera poco, el combate se haría eterno por diseño y no por dificultad.
            a.Fist = Attack<GroundSlamAttack>("BossAttack_PunoSepulcral", "Puño Sepulcral",
                "Marca el suelo bajo tus pies y lo revienta. Apártate… y vuelve: el puño se queda " +
                "clavado y ahí sí duele.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.7f).Set("recovery", 2.6f).Set("weight", 1.8f)
                    .Set("damage", 30f).Set("knockbackMultiplier", 2f)
                    .Set("shakeAmplitude", 0.7f).Set("shakeDuration", 0.45f)
                    .Set("vulnerableSeconds", 2.4f).Set("vulnerableMultiplier", 3f)
                    .Set("aimAtPlayer", true).Set("radius", 3.4f).Set("height", 3.2f)
                    .Set("windup", 0.8f).Set("slams", 1)
                    .Set("rubbleSeconds", 0f));

            a.Rockfall = Attack<BulletHellAttack>("BossAttack_Desprendimiento", "Desprendimiento",
                "Se desprende media bóveda. Las marcas del suelo dicen dónde no estar.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.5f).Set("recovery", 1.1f).Set("weight", 1.1f)
                    .Set("damage", 14f).Set("knockbackMultiplier", 0.7f)
                    .Set("pattern", (int)BulletPattern.Rain)
                    .Set("volleys", 2).Set("timeBetweenVolleys", 0.5f)
                    .Set("bulletsPerVolley", 8)
                    .Set("rainSpan", 0.95f).Set("rainMarkerSeconds", 0.5f).Set("rainJitter", 0.8f)
                    .Set("projectile.speed", 15f).Set("projectile.lifetime", 3f)
                    .Set("projectile.size", new Vector2(0.6f, 0.6f))
                    .SetObject("projectileSprite", boulder));

            // Una onda rasante de las de toda la vida: el jugador que viene de los dos primeros
            // jefes ya sabe leerla, y eso está bien — no todo tiene que ser nuevo.
            a.Fracture = Attack<ShockwaveAttack>("BossAttack_Fractura", "Fractura",
                "Parte el suelo a los dos lados. Salta por encima de la grieta.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.8f).Set("recovery", 1.2f).Set("weight", 1.2f)
                    .Set("damage", 22f).Set("knockbackMultiplier", 1.6f)
                    .Set("shakeAmplitude", 0.4f)
                    .Set("bandMin", 0f).Set("bandMax", 2f)
                    .Set("waves", 1).Set("bothDirections", true)
                    .Set("width", 1.7f).Set("speed", 15f).Set("spawnInset", 1.5f));

            // Aquí empieza a encogerse la arena. Poco daño y mucha duración a propósito.
            a.Burial = Attack<HazardFieldAttack>("BossAttack_Sepultura", "Sepultura",
                "Siembra el suelo de escombro ardiendo. No mata: te quita sitio.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.7f).Set("recovery", 1.1f).Set("weight", 0.9f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 0f)
                    .Set("zonesPerWave", 3).Set("zoneSize", new Vector2(3.4f, 1f))
                    .Set("zoneSeconds", 9f)
                    .Set("damagePerTick", 7f).Set("tickInterval", 0.5f)
                    .Set("spread", 0.9f).Set("minSeparation", 5f).Set("avoidPlayerRadius", 3f)
                    .Set("waves", 1).Set("markerSeconds", 0.6f));

            // ---------------------------------------------------------------- fase 2

            // Dos puñetazos seguidos: el primero es la finta. La ventana es más corta que en fase 1,
            // así que castigar deja de ser gratis y hay que estar ya colocado.
            a.DoubleFist = Attack<GroundSlamAttack>("BossAttack_PunoDoble", "Puño Doble",
                "Dos puñetazos, y el segundo va a donde te has apartado del primero.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.65f).Set("recovery", 2.1f).Set("weight", 1.7f)
                    .Set("damage", 30f).Set("knockbackMultiplier", 2f)
                    .Set("shakeAmplitude", 0.7f).Set("shakeDuration", 0.45f)
                    .Set("vulnerableSeconds", 1.9f).Set("vulnerableMultiplier", 3f)
                    .Set("aimAtPlayer", true).Set("radius", 3.2f).Set("height", 3.2f)
                    .Set("windup", 0.65f).Set("slams", 2).Set("timeBetweenSlams", 0.85f)
                    // El cráter del segundo puño se queda ardiendo: el sitio que acabas de usar
                    // para esquivar deja de servir para el siguiente ataque.
                    .Set("rubbleSeconds", 6f).Set("rubbleDamagePerTick", 6f));

            a.Collapse = Attack<HazardFieldAttack>("BossAttack_Derrumbe", "Derrumbe",
                "El techo entero se viene abajo por tandas. La arena se acaba.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.7f).Set("recovery", 1.3f).Set("weight", 1.1f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 0f)
                    .Set("zonesPerWave", 3).Set("zoneSize", new Vector2(3.2f, 1f))
                    .Set("zoneSeconds", 10f)
                    .Set("damagePerTick", 8f).Set("tickInterval", 0.5f)
                    .Set("spread", 1f).Set("minSeparation", 4.5f).Set("avoidPlayerRadius", 3f)
                    .Set("waves", 2).Set("timeBetweenWaves", 0.9f).Set("markerSeconds", 0.5f));

            a.Shards = Attack<BulletHellAttack>("BossAttack_Esquirlas", "Esquirlas",
                "Un anillo de esquirlas que gira. El hueco se mueve: síguelo.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.65f).Set("recovery", 1.1f).Set("weight", 1.3f)
                    .Set("damage", 11f).Set("knockbackMultiplier", 0.35f)
                    .Set("pattern", (int)BulletPattern.Radial)
                    .Set("volleys", 5).Set("timeBetweenVolleys", 0.24f)
                    .Set("bulletsPerVolley", 13)
                    .Set("arcDegrees", 360f).Set("spinPerVolley", 13f).Set("aimAtPlayer", false)
                    .Set("originOffset", new Vector2(0f, 3.2f)).Set("spawnRadius", 1.3f)
                    .Set("projectile.speed", 7.5f).Set("projectile.lifetime", 5f)
                    .Set("projectile.size", new Vector2(0.4f, 0.4f))
                    .SetObject("projectileSprite", rock));

            a.Earthquake = Attack<ShockwaveAttack>("BossAttack_Terremoto", "Terremoto",
                "Suelo, altura, suelo. Salta, aterriza y vuelve a saltar.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 1f).Set("recovery", 1.5f).Set("weight", 1.1f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 24f).Set("knockbackMultiplier", 1.7f)
                    .Set("shakeAmplitude", 0.55f).Set("shakeDuration", 0.4f)
                    .Set("bandMin", 0f).Set("bandMax", 2f)
                    .Set("alternateBands", true).Set("bandMinB", 2f).Set("bandMaxB", 9f)
                    .Set("waves", 3).Set("timeBetweenWaves", 0.7f)
                    .Set("bothDirections", true)
                    .Set("width", 1.6f).Set("speed", 14f).Set("spawnInset", 1.5f));

            a.Shrapnel = Attack<BulletHellAttack>("BossAttack_Metralla", "Metralla",
                "Tres abanicos de piedra apuntados. Hay hueco: no te quedes en línea recta.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.55f).Set("recovery", 0.9f).Set("weight", 1.1f)
                    .Set("damage", 12f).Set("knockbackMultiplier", 0.5f)
                    .Set("pattern", (int)BulletPattern.Fan)
                    .Set("volleys", 3).Set("timeBetweenVolleys", 0.28f)
                    .Set("bulletsPerVolley", 5)
                    .Set("arcDegrees", 48f).Set("spinPerVolley", 9f).Set("alternateSpin", true)
                    .Set("aimAtPlayer", true).Set("randomSpread", 1.5f)
                    .Set("originOffset", new Vector2(0f, 2.9f)).Set("spawnRadius", 1f)
                    .Set("projectile.speed", 12f).Set("projectile.lifetime", 3.5f)
                    .Set("projectile.size", new Vector2(0.42f, 0.42f))
                    .SetObject("projectileSprite", rock));

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
            so.FindProperty("displayName").stringValue = "Guardiana de Piedra";
            so.FindProperty("title").stringValue = "Centinela del Sepulcro";
            so.FindProperty("description").stringValue =
                "La tallaron para velar una tumba y le dieron una sola orden. Nadie se acuerda ya " +
                "de quién está enterrado ahí, pero ella sigue de guardia.";

            var phases = so.FindProperty("phases");
            phases.arraySize = 2;

            // Vida baja para un jefe final a propósito: con la coraza al 30%, el daño efectivo del
            // jugador fuera de las ventanas es una fracción, así que 2000 de vida aquí se pelean
            // como 3000 en un jefe normal. Subirla haría el combate largo, no difícil.
            WriteArmoredPhase(phases.GetArrayElementAtIndex(0),
                "Piedra Fría", startsAtHealth: 1f, damageScale: 1f, speedScale: 1f,
                damageTaken: 0.3f, accent: PhaseOneAccent, pause: new Vector2(0.9f, 1.5f),
                transitionSeconds: 0f, frenzyBelow: 0f,
                attacks: new[] { a.Fist, a.Rockfall, a.Fracture, a.Burial });

            // La coraza se agrieta (0,3 → 0,6) pero las ventanas se acortan: el final del combate
            // va más rápido por los dos lados en vez de ser simplemente "lo mismo con más vida".
            WriteArmoredPhase(phases.GetArrayElementAtIndex(1),
                "La Grieta", startsAtHealth: 0.5f, damageScale: 1.15f, speedScale: 1.2f,
                damageTaken: 0.6f, accent: PhaseTwoAccent, pause: new Vector2(0.65f, 1.1f),
                transitionSeconds: 2f, frenzyBelow: 0.2f,
                attacks: new[]
                {
                    a.DoubleFist, a.Collapse, a.Shards, a.Earthquake, a.Shrapnel, a.Rockfall
                });

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            return definition;
        }

        private static void WriteArmoredPhase(SerializedProperty phase, string displayName,
                                              float startsAtHealth, float damageScale, float speedScale,
                                              float damageTaken, Color accent, Vector2 pause,
                                              float transitionSeconds, float frenzyBelow,
                                              BossAttack[] attacks)
        {
            BossAuthoring.WritePhase(phase, displayName, startsAtHealth, damageScale, speedScale,
                                     accent, pause, transitionSeconds, frenzyBelow, attacks, damageTaken);
        }

        // ==================================================================== prefab

        private static GameObject CreatePrefab(BossDefinition definition)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("Boss_GuardianaDePiedra");
            root.transform.position = Vector3.zero;
            BossAuthoring.TrySetTag(root, "Enemy");

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * VisualScale;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Statue");
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 5;

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
                .Set("maxHealth", 2000f)
                .Set("currentHealth", 2000f)
                .Set("invulnerabilityDuration", 0f)
                // La armadura real la pone la fase al empezar; esto es sólo el valor con el que
                // nace el prefab si alguien lo suelta en una escena sin definición.
                .Set("damageMultiplier", 0.3f)
                .Apply();

            var knockback = root.AddComponent<Knockback>();
            new BossAuthoring.Fields(knockback).Set("immune", true).Set("resistance", 1f).Apply();

            root.AddComponent<HitFlash>();

            var corpse = root.AddComponent<Corpse>();
            new BossAuthoring.Fields(corpse).Set("linger", 1.8f).Set("fadeDuration", 1.6f).Apply();

            var dropper = root.AddComponent<CurrencyDropper>();
            new BossAuthoring.Fields(dropper).Set("tier", (int)EnemyTier.Boss).Apply();

            var controller = root.AddComponent<BossController>();
            new BossAuthoring.Fields(controller)
                .SetObject("definition", definition)
                .Set("arenaHalfWidth", 18f)
                .Set("arenaHeight", 13f)
                .Set("activationRadius", 18f)
                .Set("introSeconds", 2.4f)
                .Set("musicId", string.Empty)
                .Set("contactDamage", 16f)
                .Set("contactDamageCooldown", 0.8f)
                .Set("contactKnockbackMultiplier", 1.5f)
                // Es una estatua: se mueve lo justo para no parecer decorado.
                .Set("swayDegrees", 0.9f)
                .Set("swaySpeed", 0.8f)
                .Apply();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }
    }
}
