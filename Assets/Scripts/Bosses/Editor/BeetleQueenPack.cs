using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Quinto jefe: la <b>Reina Escarabajo</b>. Idempotente como los demás packs.
    ///
    /// <b>La idea del combate.</b> Todos los jefes anteriores se pelean en el suelo. Ésta lo
    /// quita: inunda la arena de ácido y hace brotar unos salientes a los que hay que subirse
    /// (<see cref="PlatformFloodAttack"/>). Durante esos segundos el combate ocurre en el aire, con
    /// la ventaja de que arriba estás quieto y a tiro — y con la trampa de que los salientes
    /// también caducan.
    ///
    /// Su segunda mitad es la cría: escupe escarabajos pequeños (los <c>Enemy_Beetle</c> normales),
    /// que en el suelo inundado obligan a elegir entre bajar a limpiarlos o aguantar arriba
    /// mientras se acumulan.
    ///
    /// Es además el primer jefe <b>animado</b>: el sprite del pack de Brackeys trae tres
    /// fotogramas y los pasa <see cref="SpriteFlipbook"/>.
    ///
    /// <b>Es también el primer jefe que se mueve.</b> Un escarabajo plantado en el sitio no se lee
    /// como vivo: lleva un <see cref="BossBurrowLocomotion"/> que la hace patrullar en superficie y,
    /// cada pocos segundos, excavar — se hunde en el suelo o el techo, túnela invisible soltando
    /// temblores (el <c>BossAttack_Temblor</c>) y reaparece por la superficie opuesta un poco más
    /// allá. Su baraja normal sólo dispara mientras se la ve.
    /// </summary>
    public static class BeetleQueenPack
    {
        private const string BossFolder = "Assets/Resources/Bosses";
        private const string PrefabFolder = "Assets/Prefabs/Enemies";
        private const string PrefabPath = PrefabFolder + "/Boss_ReinaEscarabajo.prefab";
        private const string DefinitionPath = BossFolder + "/Boss_ReinaEscarabajo.asset";

        // BOTH BROKEN BY DESIGN since the vendor-asset cleanup (Brackeys/ and Cainos/ deleted —
        // see docs/folder-restructure-audit.md / Assets/Editor/VendorCleanupMigration.cs). The
        // shipped Boss_ReinaEscarabajo.prefab was already retargeted to the placeholder square in
        // that same pass and needs no regen; whatever reads these already fails loudly (warn/null),
        // not by throwing — point them at real paths first if this pack is ever re-run.
        private const string BeetleSheet = "Assets/Brackeys/2D Mega Pack/Enemies/Insects/GiantBeetle.png";
        private const string BroodPrefabPath = PrefabFolder + "/Enemy_Beetle.prefab";

        private const string PropsSheet =
            "Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";

        /// <summary>
        /// El escarabajo mide 80×120 px a 20 px/unidad = 4 × 6 unidades. A ×1,5 se queda en 6 × 9:
        /// la misma altura que los otros jefes, y es el único que no hay que ampliar mucho — su
        /// arte ya venía a buena resolución.
        /// </summary>
        private const float VisualScale = 1.5f;

        private const float ColliderWidthFactor = 0.72f;
        private const float ColliderHeightFactor = 0.72f;

        private static readonly Color PhaseOneAccent = new Color(0.45f, 0.85f, 0.62f, 1f);
        private static readonly Color PhaseTwoAccent = new Color(0.85f, 0.78f, 0.28f, 1f);

        // ==================================================================== menú

        [MenuItem("Tools/RedMagic/Boss/Reina Escarabajo/RECREAR (borra y regenera)")]
        public static void Recreate()
        {
            if (!EditorUtility.DisplayDialog("Recrear Reina Escarabajo",
                    "Borra el prefab, la definición y los assets de ataque de la Reina Escarabajo y " +
                    "los vuelve a generar con el diseño actual. Se pierde cualquier ajuste hecho a " +
                    "mano en esos assets.\n\n¿Continuar?", "Borrar y regenerar", "Cancelar"))
                return;

            foreach (var path in OwnedAssetPaths())
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    AssetDatabase.DeleteAsset(path);

            AssetDatabase.Refresh();
            Generate();
        }

        private static string[] OwnedAssetPaths() => new[]
        {
            PrefabPath, DefinitionPath,
            BossFolder + "/BossAttack_MareaAcida.asset",
            BossFolder + "/BossAttack_Salivazo.asset",
            BossFolder + "/BossAttack_Camada.asset",
            BossFolder + "/BossAttack_Embestida.asset",
            BossFolder + "/BossAttack_Temblor.asset",
            BossFolder + "/BossAttack_MareaAlta.asset",
            BossFolder + "/BossAttack_Rociada.asset",
            BossFolder + "/BossAttack_Eclosion.asset",
            BossFolder + "/BossAttack_Estampida.asset",
        };

        [MenuItem("Tools/RedMagic/Boss/Reina Escarabajo/Crear (si no existe)")]
        public static void Generate()
        {
            BossAuthoring.EnsureTag("Enemy");
            BossAuthoring.EnsureFolder(BossFolder);
            BossAuthoring.EnsureFolder(PrefabFolder);

            var attacks = CreateAttacks();
            var definition = CreateDefinition(attacks);
            var prefab = CreatePrefab(definition, attacks);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BeetleQueenPack] Jefe listo: {definition.name}. Prefab en {PrefabPath}.", definition);
            Selection.activeObject = prefab != null ? (Object)prefab : definition;
        }

        // ==================================================================== ataques

        private sealed class Attacks
        {
            public BossAttack Flood;
            public BossAttack Spit;
            public BossAttack Brood;
            public BossAttack Charge;

            /// <summary>No va en ninguna baraja: lo lanza <see cref="BossBurrowLocomotion"/> mientras túnela.</summary>
            public BossAttack Tremor;

            public BossAttack HighTide;
            public BossAttack Spray;
            public BossAttack Hatch;
            public BossAttack Rampage;
        }

        private static Attacks CreateAttacks()
        {
            var a = new Attacks();

            var egg = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Pumpkin 02");
            var brood = AssetDatabase.LoadAssetAtPath<GameObject>(BroodPrefabPath);

            // ---------------------------------------------------------------- fase 1

            // La firma: el suelo deja de valer y hay que subirse. Sale pronto y a menudo, porque
            // es la regla que hay que aprender para entender el resto del combate.
            a.Flood = Attack<PlatformFloodAttack>("BossAttack_MareaAcida", "Marea Ácida",
                "Inunda el suelo. Brotan salientes: súbete, aguanta, y baja antes de que se apaguen.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.9f).Set("recovery", 1.4f).Set("weight", 1.5f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 0f)
                    .Set("platformCount", 3).Set("platformSize", new Vector2(4.4f, 0.7f))
                    .Set("platformSeconds", 7f).Set("minSeparation", 6.5f)
                    .Set("floodSeconds", 4.5f).Set("floodSegments", 6).Set("floodHeight", 1.1f)
                    .Set("floodDamagePerTick", 11f).Set("floodTickInterval", 0.45f)
                    .Set("climbSeconds", 1.4f).Set("markerSeconds", 0.5f));

            a.Spit = Attack<BulletHellAttack>("BossAttack_Salivazo", "Salivazo",
                "Escupe ácido en abanico. No te quedes en su línea.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.55f).Set("recovery", 0.95f).Set("weight", 1.3f)
                    .Set("damage", 12f).Set("knockbackMultiplier", 0.5f)
                    .Set("pattern", (int)BulletPattern.Fan)
                    .Set("volleys", 3).Set("timeBetweenVolleys", 0.3f)
                    .Set("bulletsPerVolley", 5)
                    .Set("arcDegrees", 46f).Set("spinPerVolley", 8f).Set("alternateSpin", true)
                    .Set("aimAtPlayer", true).Set("randomSpread", 2f)
                    .Set("originOffset", new Vector2(0f, 2.4f)).Set("spawnRadius", 1f)
                    .Set("projectile.speed", 12f).Set("projectile.lifetime", 3.5f)
                    .Set("projectile.size", new Vector2(0.42f, 0.42f))
                    .SetObject("projectileSprite", egg));

            a.Brood = Attack<SummonAddsAttack>("BossAttack_Camada", "Camada",
                "Suelta cría. Pequeños, pero en el suelo inundado no hay dónde ignorarlos.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.8f).Set("recovery", 1.3f).Set("weight", 1f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 0f)
                    .Set("count", 2).Set("maxAlive", 4)
                    .Set("markerSeconds", 0.55f).Set("timeBetweenSpawns", 0.15f)
                    .Set("distanceRange", new Vector2(4f, 10f)).Set("spawnHeight", 0.8f)
                    .SetObjectArray("prefabs", brood));

            // Embestida a ras de suelo: castiga justo el sitio donde uno se refugia cuando el
            // suelo NO está inundado, para que tampoco haya una esquina cómoda permanente.
            a.Charge = Attack<ShockwaveAttack>("BossAttack_Embestida", "Embestida",
                "Baja la cabeza y arrasa a ras de suelo. Salta.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.75f).Set("recovery", 1.25f).Set("weight", 1.2f)
                    .Set("damage", 22f).Set("knockbackMultiplier", 1.8f)
                    .Set("shakeAmplitude", 0.45f).Set("shakeDuration", 0.35f)
                    .Set("bandMin", 0f).Set("bandMax", 1.8f)
                    .Set("waves", 1).Set("bothDirections", true)
                    .Set("width", 1.8f).Set("speed", 17f).Set("spawnInset", 1.6f));

            // Temblor de tránsito: no está en ninguna baraja. Lo dispara la locomoción mientras la
            // Reina túnela bajo el suelo — no se la ve, pero el suelo avisa. Franja baja: se salta.
            a.Tremor = Attack<ShockwaveAttack>("BossAttack_Temblor", "Temblor",
                "El suelo tiembla: está cavando debajo. Salta.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.5f).Set("recovery", 0f).Set("weight", 1f)
                    .Set("cooldownInAttacks", 0)
                    .Set("damage", 14f).Set("knockbackMultiplier", 1.1f)
                    .Set("shakeAmplitude", 0.35f).Set("shakeDuration", 0.3f)
                    .Set("bandMin", 0f).Set("bandMax", 1.7f)
                    .Set("waves", 2).Set("timeBetweenWaves", 0.4f).Set("bothDirections", true)
                    .Set("width", 1.6f).Set("speed", 15f).Set("spawnInset", 1.2f));

            // ---------------------------------------------------------------- fase 2

            // La marea, en serio: más alta, más larga y con un saliente menos.
            a.HighTide = Attack<PlatformFloodAttack>("BossAttack_MareaAlta", "Marea Alta",
                "Más ácido, más rato y un saliente menos. Elige bien a cuál subes.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.85f).Set("recovery", 1.5f).Set("weight", 1.5f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 0f)
                    .Set("platformCount", 2).Set("platformSize", new Vector2(3.8f, 0.7f))
                    .Set("platformSeconds", 7.5f).Set("minSeparation", 8f)
                    .Set("floodSeconds", 5.5f).Set("floodSegments", 6).Set("floodHeight", 1.4f)
                    .Set("floodDamagePerTick", 13f).Set("floodTickInterval", 0.45f)
                    .Set("climbSeconds", 1.2f).Set("markerSeconds", 0.45f));

            a.Spray = Attack<BulletHellAttack>("BossAttack_Rociada", "Rociada",
                "Un anillo de ácido que gira. El hueco se mueve: síguelo.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.65f).Set("recovery", 1.1f).Set("weight", 1.3f)
                    .Set("damage", 10f).Set("knockbackMultiplier", 0.35f)
                    .Set("pattern", (int)BulletPattern.Radial)
                    .Set("volleys", 6).Set("timeBetweenVolleys", 0.22f)
                    .Set("bulletsPerVolley", 12)
                    .Set("arcDegrees", 360f).Set("spinPerVolley", 14f).Set("aimAtPlayer", false)
                    .Set("originOffset", new Vector2(0f, 2.8f)).Set("spawnRadius", 1.3f)
                    .Set("projectile.speed", 7.5f).Set("projectile.lifetime", 5f)
                    .Set("projectile.size", new Vector2(0.4f, 0.4f))
                    .SetObject("projectileSprite", egg));

            a.Hatch = Attack<SummonAddsAttack>("BossAttack_Eclosion", "Eclosión",
                "Toda la puesta a la vez.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.75f).Set("recovery", 1.4f).Set("weight", 1.1f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 0f)
                    .Set("count", 3).Set("maxAlive", 6)
                    .Set("markerSeconds", 0.5f).Set("timeBetweenSpawns", 0.12f)
                    .Set("distanceRange", new Vector2(4f, 12f)).Set("spawnHeight", 0.8f)
                    .SetObjectArray("prefabs", brood));

            a.Rampage = Attack<ShockwaveAttack>("BossAttack_Estampida", "Estampida",
                "Tres embestidas encadenadas, cada una más rápida.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.85f).Set("recovery", 1.5f).Set("weight", 1.2f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 24f).Set("knockbackMultiplier", 1.8f)
                    .Set("shakeAmplitude", 0.55f).Set("shakeDuration", 0.4f)
                    .Set("bandMin", 0f).Set("bandMax", 1.9f)
                    .Set("waves", 3).Set("timeBetweenWaves", 0.5f).Set("speedRampPerWave", 1.2f)
                    .Set("bothDirections", true)
                    .Set("width", 1.8f).Set("speed", 16f).Set("spawnInset", 1.6f));

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
            so.FindProperty("displayName").stringValue = "Reina Escarabajo";
            so.FindProperty("title").stringValue = "Madre del Nido Anegado";
            so.FindProperty("description").stringValue =
                "Lleva tanto tiempo poniendo bajo la tumba que el suelo ya no es suelo, sino su " +
                "nido. Todo lo que pisas es suyo.";

            var phases = so.FindProperty("phases");
            phases.arraySize = 2;

            BossAuthoring.WritePhase(phases.GetArrayElementAtIndex(0),
                "Nido Anegado", startsAtHealth: 1f, damageScale: 1f, speedScale: 1f,
                accent: PhaseOneAccent, pause: new Vector2(0.9f, 1.5f),
                transitionSeconds: 0f, frenzyBelow: 0f,
                attacks: new[] { a.Flood, a.Spit, a.Brood, a.Charge });

            BossAuthoring.WritePhase(phases.GetArrayElementAtIndex(1),
                "Puesta", startsAtHealth: 0.5f, damageScale: 1.15f, speedScale: 1.2f,
                accent: PhaseTwoAccent, pause: new Vector2(0.7f, 1.1f),
                transitionSeconds: 2f, frenzyBelow: 0.2f,
                attacks: new[] { a.HighTide, a.Spray, a.Hatch, a.Rampage, a.Spit });

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            return definition;
        }

        // ==================================================================== prefab

        private static GameObject CreatePrefab(BossDefinition definition, Attacks a)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            var frames = new[]
            {
                BossAuthoring.LoadSprite(BeetleSheet, "GiantBeetle_0"),
                BossAuthoring.LoadSprite(BeetleSheet, "GiantBeetle_1"),
                BossAuthoring.LoadSprite(BeetleSheet, "GiantBeetle_2"),
            };

            var root = new GameObject("Boss_ReinaEscarabajo");
            BossAuthoring.TrySetTag(root, "Enemy");

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * VisualScale;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 5;

            // El único jefe con arte animado: tres fotogramas de ida y vuelta bastan para que se
            // lea como un bicho vivo y no como una calcomanía.
            var flipbook = visual.AddComponent<SpriteFlipbook>();
            new BossAuthoring.Fields(flipbook)
                .SetObjectArray("frames", frames)
                .Set("framesPerSecond", 5f)
                .Set("pingPong", true)
                .Set("randomStart", false)
                .Apply();

            // A diferencia del resto de jefes, el origen de éste es el CENTRO del sprite, no su
            // base: la locomoción la gira y la voltea (marcha, techo, picado de cabeza), y todo eso
            // pivota limpio sólo alrededor del centro. El componente ya la coloca a la altura justa
            // sobre cada superficie.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            var bounds = renderer.bounds;

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.freezeRotation = true;

            // El sprite ya está tumbado -90°, así que bounds.size viene con ancho y alto en su
            // orientación de marcha: la caja se ajusta a eso y va centrada en el origen.
            var collider = root.AddComponent<BoxCollider2D>();
            float width = Mathf.Max(1f, bounds.size.x * ColliderWidthFactor);
            float height = Mathf.Max(1f, bounds.size.y * ColliderHeightFactor);
            collider.size = new Vector2(width, height);
            collider.offset = Vector2.zero;

            var health = root.AddComponent<Health>();
            new BossAuthoring.Fields(health)
                .Set("maxHealth", 2600f).Set("currentHealth", 2600f)
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
                .Set("activationRadius", 18f)
                .Set("introSeconds", 2.2f)
                .Set("musicId", string.Empty)
                .Set("contactDamage", 15f)
                .Set("contactDamageCooldown", 0.8f)
                .Set("contactKnockbackMultiplier", 1.5f)
                // La locomoción de excavadora la coloca en cada superficie y la orienta hacia su
                // marcha, así que el jefe no debe ni plantarla en el suelo, ni girarla hacia el
                // jugador, ni balancearla.
                .Set("snapToGround", false)
                .Set("faceTarget", false)
                .Set("swayDegrees", 0f)
                .Apply();

            var locomotion = root.AddComponent<BossBurrowLocomotion>();
            new BossAuthoring.Fields(locomotion)
                .SetObject("tremorAttack", a.Tremor)
                .Set("surfaceSpeed", 4.5f)
                .Set("patrolMargin", 2f)
                .Set("surfacedSeconds", new Vector2(6f, 9f))
                .Set("digSeconds", 0.5f)
                .Set("emergeSeconds", 0.5f)
                .Set("emergeWarnSeconds", 0.9f)
                .Set("emergeOffsetRange", new Vector2(1f, 5f))
                .Set("transitSeconds", new Vector2(2.5f, 3.5f))
                .Set("tremorInterval", 1.1f)
                .Set("ceilingProbeHeight", 20f)
                .Set("spriteUprightDegrees", -90f)
                .Apply();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }
    }
}
