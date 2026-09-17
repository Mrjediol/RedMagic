using RedMagic.Combat;
using RedMagic.Economy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Cuarto jefe: el <b>Pozo Maldito</b>. Idempotente como los demás packs.
    ///
    /// <b>La idea del combate.</b> Los tres anteriores enseñan a esquivar y, el tercero, a elegir
    /// cuándo entra a castigar. Éste enseña lo contrario: <b>cuándo NO pegar</b>. Se tapa cada
    /// pocos ataques y devuelve un pellizco por cada golpe que reciba mientras está cubierto
    /// (<see cref="GuardStanceAttack"/>); quien machaca el botón se hace la mitad de la barra él
    /// solo. Y aguantar sin pegar tiene premio: al destaparse queda expuesto.
    ///
    /// Lo segundo que lo distingue es que <b>reparte la atención</b>: escupe bichos que salen del
    /// brocal y, entre medias, rebosa charcos que se quedan. Un pozo no persigue a nadie — lo que
    /// hace es que el sitio donde estás deje de ser tuyo.
    ///
    /// No lleva armadura de fase: aquí el daño se pierde por pegar cuando no toca, no porque el
    /// jefe sea de piedra.
    /// </summary>
    public static class CursedWellPack
    {
        private const string BossFolder = "Assets/Resources/Bosses";
        private const string PrefabFolder = "Assets/Prefabs/Enemies";
        private const string PrefabPath = PrefabFolder + "/Boss_PozoMaldito.prefab";
        private const string DefinitionPath = BossFolder + "/Boss_PozoMaldito.asset";

        // BROKEN BY DESIGN since the vendor-asset cleanup (Cainos/ deleted — see
        // docs/folder-restructure-audit.md / Assets/Editor/VendorCleanupMigration.cs). The shipped
        // Boss_PozoMaldito.prefab was already retargeted to the placeholder square in that same
        // pass and needs no regen; BossAuthoring.LoadSprite already fails loudly (warn/null), not
        // by throwing — point this at a real path first if this pack is ever re-run.
        private const string PropsSheet =
            "Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";

        /// <summary>Los bichos que suben del pozo: los que ya repta el mundo gótico.</summary>
        private const string CrawlerPrefabPath = PrefabFolder + "/Enemy_GothicCrawler.prefab";
        private const string StalkerPrefabPath = PrefabFolder + "/Enemy_GothicStalker.prefab";

        /// <summary>El brocal mide 87×91 px a 32 px/unidad = 2,7 × 2,8. A ×3,2 se queda en ~9.</summary>
        private const float VisualScale = 3.2f;

        private const float ColliderWidthFactor = 0.72f;
        private const float ColliderHeightFactor = 0.8f;

        private static readonly Color PhaseOneAccent = new Color(0.35f, 0.72f, 0.6f, 1f);
        private static readonly Color PhaseTwoAccent = new Color(0.6f, 0.35f, 0.75f, 1f);

        // ==================================================================== menú

        [MenuItem("Tools/RedMagic/Boss/Crear jefe: Pozo Maldito")]
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

            Debug.Log($"[CursedWellPack] Jefe listo: {definition.name}. Prefab en {PrefabPath}.", definition);
            Selection.activeObject = prefab != null ? (Object)prefab : definition;
        }

        // ==================================================================== ataques

        private sealed class Attacks
        {
            public BossAttack Cover;
            public BossAttack Hands;
            public BossAttack Geyser;
            public BossAttack Overflow;

            public BossAttack Seal;
            public BossAttack Downpour;
            public BossAttack Swarm;
            public BossAttack Tremor;
        }

        private static Attacks CreateAttacks()
        {
            var a = new Attacks();

            var drop = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Rock 03");
            var crawler = AssetDatabase.LoadAssetAtPath<GameObject>(CrawlerPrefabPath);
            var stalker = AssetDatabase.LoadAssetAtPath<GameObject>(StalkerPrefabPath);

            // ---------------------------------------------------------------- fase 1

            // La lección del jefe. Peso alto para que salga pronto: hay que aprenderla en el primer
            // minuto, no en el último.
            a.Cover = Attack<GuardStanceAttack>("BossAttack_SeCubre", "Se Cubre",
                "Cierra el brocal. Pegarle ahora te devuelve el golpe; aguanta y se destapará solo.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.6f).Set("recovery", 1.6f).Set("weight", 1.5f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 0f)
                    .Set("vulnerableSeconds", 1.6f).Set("vulnerableMultiplier", 2.5f)
                    .Set("guardSeconds", 2.6f).Set("damageTakenWhileGuarding", 0.12f)
                    .Set("reflectDamage", 9f).Set("reflectKnockback", 1.2f)
                    .Set("counterAttack", true));

            a.Hands = Attack<SummonAddsAttack>("BossAttack_ManosQueSuben", "Manos que Suben",
                "Algo trepa por dentro del pozo. Deja de mirar el agujero.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.85f).Set("recovery", 1.3f).Set("weight", 1.1f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 0f)
                    .Set("count", 2).Set("maxAlive", 4)
                    .Set("markerSeconds", 0.6f).Set("timeBetweenSpawns", 0.18f)
                    .Set("distanceRange", new Vector2(3.5f, 9f)).Set("spawnHeight", 0.9f)
                    .SetObjectArray("prefabs", crawler));

            // Chorro: los proyectiles llevan gravedad, así que salen hacia arriba en abanico y
            // vuelven a caer. La misma clase de siempre, pero se lee como agua negra saliendo a
            // presión en vez de como una ráfaga.
            a.Geyser = Attack<BulletHellAttack>("BossAttack_Chorro", "Chorro",
                "Escupe agua negra hacia arriba. Lo que sube, cae.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.55f).Set("recovery", 1.1f).Set("weight", 1.3f)
                    .Set("damage", 12f).Set("knockbackMultiplier", 0.6f)
                    .Set("pattern", (int)BulletPattern.Radial)
                    .Set("volleys", 4).Set("timeBetweenVolleys", 0.3f)
                    .Set("bulletsPerVolley", 7)
                    .Set("arcDegrees", 120f).Set("startAngle", 90f).Set("aimAtPlayer", false)
                    .Set("spinPerVolley", 17f).Set("alternateSpin", true).Set("randomSpread", 4f)
                    .Set("originOffset", new Vector2(0f, 2.6f)).Set("spawnRadius", 0.6f)
                    .Set("projectile.speed", 13f).Set("projectile.lifetime", 5f)
                    .Set("projectile.arcGravity", 14f)
                    .Set("projectile.size", new Vector2(0.45f, 0.45f))
                    .SetObject("projectileSprite", drop));

            a.Overflow = Attack<HazardFieldAttack>("BossAttack_Rebosa", "Rebosa",
                "El pozo se desborda. Los charcos no se van solos.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.7f).Set("recovery", 1.1f).Set("weight", 0.9f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 0f)
                    .Set("zonesPerWave", 3).Set("zoneSize", new Vector2(3.6f, 0.9f))
                    .Set("zoneSeconds", 8f)
                    .Set("damagePerTick", 6f).Set("tickInterval", 0.5f)
                    .Set("spread", 0.9f).Set("minSeparation", 5f).Set("avoidPlayerRadius", 3f)
                    .Set("waves", 1).Set("markerSeconds", 0.6f));

            // ---------------------------------------------------------------- fase 2

            // La misma guardia, más larga y más cara: en fase 2 ya no vale la excusa de no saberlo.
            a.Seal = Attack<GuardStanceAttack>("BossAttack_SeSella", "Se Sella",
                "Se cierra a cal y canto, y más rato. Cada golpe te cuesta más que a él.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.5f).Set("recovery", 1.5f).Set("weight", 1.4f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 0f)
                    .Set("vulnerableSeconds", 1.5f).Set("vulnerableMultiplier", 2.8f)
                    .Set("guardSeconds", 3.2f).Set("damageTakenWhileGuarding", 0.08f)
                    .Set("reflectDamage", 13f).Set("reflectKnockback", 1.4f)
                    .Set("counterAttack", true));

            a.Downpour = Attack<BulletHellAttack>("BossAttack_Aguacero", "Aguacero",
                "Lo que escupió antes vuelve a caer, y en más sitios.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.5f).Set("recovery", 1.1f).Set("weight", 1.2f)
                    .Set("damage", 12f).Set("knockbackMultiplier", 0.6f)
                    .Set("pattern", (int)BulletPattern.Rain)
                    .Set("volleys", 3).Set("timeBetweenVolleys", 0.45f)
                    .Set("bulletsPerVolley", 9)
                    .Set("rainSpan", 1f).Set("rainMarkerSeconds", 0.45f).Set("rainJitter", 1f)
                    .Set("projectile.speed", 15f).Set("projectile.lifetime", 3f)
                    .Set("projectile.size", new Vector2(0.5f, 0.5f))
                    .SetObject("projectileSprite", drop));

            a.Swarm = Attack<SummonAddsAttack>("BossAttack_Enjambre", "Enjambre",
                "Ahora suben de tres en tres, y no todos reptan.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.8f).Set("recovery", 1.4f).Set("weight", 1f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 0f)
                    .Set("count", 3).Set("maxAlive", 5)
                    .Set("markerSeconds", 0.55f).Set("timeBetweenSpawns", 0.15f)
                    .Set("distanceRange", new Vector2(3.5f, 11f)).Set("spawnHeight", 0.9f)
                    .SetObjectArray("prefabs", crawler, stalker));

            a.Tremor = Attack<ShockwaveAttack>("BossAttack_Sacudida", "Sacudida",
                "El brocal entero golpea el suelo. Salta.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.8f).Set("recovery", 1.2f).Set("weight", 1.1f)
                    .Set("damage", 21f).Set("knockbackMultiplier", 1.6f)
                    .Set("shakeAmplitude", 0.5f).Set("shakeDuration", 0.35f)
                    .Set("bandMin", 0f).Set("bandMax", 1.9f)
                    .Set("waves", 2).Set("timeBetweenWaves", 0.55f).Set("speedRampPerWave", 1.15f)
                    .Set("bothDirections", true)
                    .Set("width", 1.7f).Set("speed", 15f).Set("spawnInset", 1.5f));

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
            so.FindProperty("displayName").stringValue = "Pozo Maldito";
            so.FindProperty("title").stringValue = "Boca del Mundo de Abajo";
            so.FindProperty("description").stringValue =
                "El pueblo lo tapió hace generaciones y siguió sacando agua de él. Nadie preguntó " +
                "nunca de dónde venía.";

            var phases = so.FindProperty("phases");
            phases.arraySize = 2;

            BossAuthoring.WritePhase(phases.GetArrayElementAtIndex(0),
                "Agua Negra", startsAtHealth: 1f, damageScale: 1f, speedScale: 1f,
                accent: PhaseOneAccent, pause: new Vector2(0.9f, 1.5f),
                transitionSeconds: 0f, frenzyBelow: 0f,
                attacks: new[] { a.Cover, a.Hands, a.Geyser, a.Overflow });

            BossAuthoring.WritePhase(phases.GetArrayElementAtIndex(1),
                "Desborde", startsAtHealth: 0.5f, damageScale: 1.15f, speedScale: 1.2f,
                accent: PhaseTwoAccent, pause: new Vector2(0.7f, 1.1f),
                transitionSeconds: 2f, frenzyBelow: 0.2f,
                attacks: new[] { a.Seal, a.Swarm, a.Downpour, a.Tremor, a.Geyser, a.Overflow });

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            return definition;
        }

        // ==================================================================== prefab

        private static GameObject CreatePrefab(BossDefinition definition)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("Boss_PozoMaldito");
            BossAuthoring.TrySetTag(root, "Enemy");

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * VisualScale;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Well");
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
                .Set("maxHealth", 2400f).Set("currentHealth", 2400f)
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
                .Set("arenaHalfWidth", 16f)
                .Set("arenaHeight", 13f)
                .Set("activationRadius", 17f)
                .Set("introSeconds", 2.2f)
                .Set("musicId", string.Empty)
                .Set("contactDamage", 12f)
                .Set("contactDamageCooldown", 0.8f)
                .Set("contactKnockbackMultiplier", 1.3f)
                // Un pozo de piedra no se balancea: lo que se mueve es lo que sale de él.
                .Set("swayDegrees", 0f)
                .Apply();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }
    }
}
