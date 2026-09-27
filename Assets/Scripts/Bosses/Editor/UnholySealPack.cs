using RedMagic.Combat;
using RedMagic.Economy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Sexto jefe: el <b>Sello Profano</b>. Idempotente como los demás packs.
    ///
    /// <b>La idea del combate.</b> Es el jefe de la <i>decisión de a qué disparar</i>. Cada pocos
    /// ataques se cierra y planta anclas (<see cref="AnchorRitualAttack"/>): mientras están en pie
    /// no le entra casi nada, así que seguir dándole a él es tirar el daño. Romperlas a tiempo lo
    /// deja expuesto; no llegar significa comerse un estallido de arena entera.
    ///
    /// Alrededor de eso va la baraja más "de examen" del juego, a propósito: recoge lo que los
    /// otros cinco jefes han ido enseñando —el barrido giratorio, los refugios, la espiral, el
    /// escombro que se queda— para que quien llegue hasta aquí lo reconozca todo y sólo tenga que
    /// resolverlo más rápido y con la cuenta atrás encima.
    ///
    /// No usa armadura de fase: aquí el daño se pierde por disparar a lo que no toca.
    /// </summary>
    public static class UnholySealPack
    {
        private const string BossFolder = "Assets/Resources/Bosses";
        private const string PrefabFolder = "Assets/Prefabs/Enemies";
        private const string PrefabPath = PrefabFolder + "/Boss_SelloProfano.prefab";
        private const string DefinitionPath = BossFolder + "/Boss_SelloProfano.asset";

        // BOTH BROKEN BY DESIGN since the vendor-asset cleanup (Brackeys/ and Cainos/ deleted —
        // see docs/folder-restructure-audit.md / Assets/Editor/VendorCleanupMigration.cs). The
        // shipped Boss_SelloProfano.prefab was already retargeted to the placeholder square in
        // that same pass and needs no regen; whatever reads these already fails loudly (warn/null),
        // not by throwing — point them at real paths first if this pack is ever re-run.
        private const string SealTexture =
            "Assets/Brackeys/2D Mega Pack/Environment/Gothic/Pentagram_Activated.png";

        private const string PropsSheet =
            "Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";

        /// <summary>
        /// El pentagrama mide 36×36 px a 10 px/unidad = 3,6 × 3,6. A ×2,5 son 9 × 9: ocupa lo
        /// mismo que los demás jefes, pero en redondo.
        /// </summary>
        private const float VisualScale = 2.5f;

        /// <summary>
        /// Flota: no es una estatua ni un bicho, es un sello colgado en el aire. Esto es cuánto se
        /// levanta su centro sobre el suelo.
        /// </summary>
        private const float HoverHeight = 1.6f;

        private const float ColliderFactor = 0.62f;

        private static readonly Color PhaseOneAccent = new Color(0.42f, 0.83f, 0.95f, 1f);
        private static readonly Color PhaseTwoAccent = new Color(0.95f, 0.3f, 0.5f, 1f);

        // ==================================================================== menú

        [MenuItem("Tools/RedMagic/Boss/Crear jefe: Sello Profano")]
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

            Debug.Log($"[UnholySealPack] Jefe listo: {definition.name}. Prefab en {PrefabPath}.", definition);
            Selection.activeObject = prefab != null ? (Object)prefab : definition;
        }

        // ==================================================================== ataques

        private sealed class Attacks
        {
            public BossAttack Ritual;
            public BossAttack Runes;
            public BossAttack Wards;
            public BossAttack Curse;

            public BossAttack GreaterRitual;
            public BossAttack Beam;
            public BossAttack Embers;
            public BossAttack Judgement;
        }

        private static Attacks CreateAttacks()
        {
            var a = new Attacks();

            var rune = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Stone of Recall");
            var shard = BossAuthoring.LoadSprite(PropsSheet, "TX Village Props - Rock 03");

            // ---------------------------------------------------------------- fase 1

            // El corazón del jefe. Tres anclas y nueve segundos: llega de sobra si se dejan de
            // hacer cosas al jefe, y no llega si se intenta compaginar.
            a.Ritual = Attack<AnchorRitualAttack>("BossAttack_Ritual", "Ritual",
                "Se cierra y clava tres anclas. Rómpelas: mientras estén en pie, pegarle a él no " +
                "sirve de nada.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.8f).Set("recovery", 2.2f).Set("weight", 1.6f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 0f)
                    .Set("vulnerableSeconds", 3f).Set("vulnerableMultiplier", 3f)
                    .Set("anchorCount", 3).Set("anchorHealth", 110f)
                    .Set("anchorSize", new Vector2(1.3f, 1.6f)).Set("anchorHeight", 0.9f)
                    .Set("minSeparation", 6f).Set("edgeMargin", 2.5f)
                    .Set("ritualSeconds", 9f).Set("damageTakenDuringRitual", 0.05f)
                    .Set("dischargeDamage", 34f).Set("dischargeKnockback", 2f)
                    .Set("dischargeWarning", 0.7f)
                    .SetObject("anchorSprite", rune));

            a.Runes = Attack<BulletHellAttack>("BossAttack_Runas", "Runas",
                "Un anillo de runas que gira. El hueco se mueve: síguelo.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.65f).Set("recovery", 1.1f).Set("weight", 1.3f)
                    .Set("damage", 11f).Set("knockbackMultiplier", 0.35f)
                    .Set("pattern", (int)BulletPattern.Radial)
                    .Set("volleys", 5).Set("timeBetweenVolleys", 0.24f)
                    .Set("bulletsPerVolley", 12)
                    .Set("arcDegrees", 360f).Set("spinPerVolley", 15f).Set("aimAtPlayer", false)
                    .Set("originOffset", new Vector2(0f, 3f)).Set("spawnRadius", 1.4f)
                    .Set("projectile.speed", 7.5f).Set("projectile.lifetime", 5f)
                    .Set("projectile.size", new Vector2(0.4f, 0.4f))
                    .SetObject("projectileSprite", shard));

            // Los refugios del espantapájaros, aquí con la excusa perfecta: un sello que sólo
            // perdona a quien esté dentro de sus círculos.
            a.Wards = Attack<SafeZoneAttack>("BossAttack_CirculosDeGuarda", "Círculos de Guarda",
                "Sólo se salva quien esté dentro de un círculo. Corre a uno.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.75f).Set("recovery", 1.5f).Set("weight", 1.1f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 26f).Set("knockbackMultiplier", 1.2f)
                    .Set("shakeAmplitude", 0.55f).Set("shakeDuration", 0.45f)
                    .Set("safeSpots", 3).Set("spotRadius", 2.3f)
                    .Set("minSeparation", 6f).Set("edgeMargin", 2.5f)
                    .Set("markerSeconds", 1.6f).Set("pulses", 1).Set("moveSpotsEachPulse", false)
                    .Set("guaranteeReachable", true).Set("reachableRadius", 10f)
                    .Set("safeColor", new Color(0.96f, 0.94f, 0.74f, 0.85f)));

            a.Curse = Attack<BulletHellAttack>("BossAttack_Maldicion", "Maldición",
                "Marca el suelo y deja caer esquirlas sobre las marcas.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.5f).Set("recovery", 1.05f).Set("weight", 1.1f)
                    .Set("damage", 13f).Set("knockbackMultiplier", 0.6f)
                    .Set("pattern", (int)BulletPattern.Rain)
                    .Set("volleys", 2).Set("timeBetweenVolleys", 0.48f)
                    .Set("bulletsPerVolley", 8)
                    .Set("rainSpan", 0.95f).Set("rainMarkerSeconds", 0.5f).Set("rainJitter", 0.8f)
                    .Set("projectile.speed", 15f).Set("projectile.lifetime", 3f)
                    .Set("projectile.size", new Vector2(0.5f, 0.5f))
                    .SetObject("projectileSprite", shard));

            // ---------------------------------------------------------------- fase 2

            // El ritual grande: una ancla más, menos tiempo y más castigo si se completa.
            a.GreaterRitual = Attack<AnchorRitualAttack>("BossAttack_RitualMayor", "Ritual Mayor",
                "Cuatro anclas y menos tiempo. Si lo completa, se lleva la arena por delante.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.7f).Set("recovery", 2f).Set("weight", 1.6f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 0f)
                    .Set("vulnerableSeconds", 2.6f).Set("vulnerableMultiplier", 3.2f)
                    .Set("anchorCount", 4).Set("anchorHealth", 130f)
                    .Set("anchorSize", new Vector2(1.3f, 1.6f)).Set("anchorHeight", 0.9f)
                    .Set("minSeparation", 5.5f).Set("edgeMargin", 2.5f)
                    .Set("ritualSeconds", 8.5f).Set("damageTakenDuringRitual", 0.05f)
                    .Set("dischargeDamage", 40f).Set("dischargeKnockback", 2.2f)
                    .Set("dischargeWarning", 0.65f)
                    .SetObject("anchorSprite", rune));

            // El barrido del espantapájaros, ahora como aguja de luz del sello.
            a.Beam = Attack<SweepBeamAttack>("BossAttack_AgujaDeLuz", "Aguja de Luz",
                "Una aguja de luz que barre de arriba abajo. Mira a qué distancia estás.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.9f).Set("recovery", 1.4f).Set("weight", 1.3f)
                    .Set("damage", 23f).Set("knockbackMultiplier", 1.6f)
                    .Set("shakeAmplitude", 0.35f).Set("shakeDuration", 0.3f)
                    .Set("pivotOffset", new Vector2(0f, 3f))
                    .Set("fromAngle", 84f).Set("toAngle", -6f)
                    .Set("sweepSeconds", 0.9f).Set("sweepTowardPlayer", true).Set("bothSides", true)
                    .Set("sweeps", 1).Set("width", 0.8f));

            a.Embers = Attack<HazardFieldAttack>("BossAttack_BrasasImpias", "Brasas Impías",
                "Prende el suelo por trozos. No se apagan solos.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.7f).Set("recovery", 1.2f).Set("weight", 1f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 0f)
                    .Set("zonesPerWave", 3).Set("zoneSize", new Vector2(3.2f, 1f))
                    .Set("zoneSeconds", 9f)
                    .Set("damagePerTick", 8f).Set("tickInterval", 0.5f)
                    .Set("spread", 1f).Set("minSeparation", 4.5f).Set("avoidPlayerRadius", 3f)
                    .Set("waves", 2).Set("timeBetweenWaves", 0.9f).Set("markerSeconds", 0.5f));

            a.Judgement = Attack<SafeZoneAttack>("BossAttack_Juicio", "Juicio",
                "Dos círculos, y cambian de sitio entre golpe y golpe.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.7f).Set("recovery", 1.6f).Set("weight", 1.2f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 24f).Set("knockbackMultiplier", 1.2f)
                    .Set("shakeAmplitude", 0.6f).Set("shakeDuration", 0.45f)
                    .Set("safeSpots", 2).Set("spotRadius", 2.2f)
                    .Set("minSeparation", 9f).Set("edgeMargin", 2.5f)
                    .Set("markerSeconds", 1.3f).Set("pulses", 2).Set("timeBetweenPulses", 0.5f)
                    .Set("moveSpotsEachPulse", true)
                    .Set("guaranteeReachable", true).Set("reachableRadius", 8f)
                    .Set("safeColor", new Color(0.96f, 0.94f, 0.74f, 0.85f)));

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
            so.FindProperty("displayName").stringValue = "Sello Profano";
            so.FindProperty("title").stringValue = "Lo Que la Tumba Retiene";
            so.FindProperty("description").stringValue =
                "No es el guardián de la tumba: es la cerradura. Lleva siglos aguantando, y ya no " +
                "distingue entre lo que quiere entrar y lo que quiere salir.";

            var phases = so.FindProperty("phases");
            phases.arraySize = 2;

            BossAuthoring.WritePhase(phases.GetArrayElementAtIndex(0),
                "Sello Intacto", startsAtHealth: 1f, damageScale: 1f, speedScale: 1f,
                accent: PhaseOneAccent, pause: new Vector2(0.9f, 1.5f),
                transitionSeconds: 0f, frenzyBelow: 0f,
                attacks: new[] { a.Ritual, a.Runes, a.Wards, a.Curse });

            BossAuthoring.WritePhase(phases.GetArrayElementAtIndex(1),
                "Sello Roto", startsAtHealth: 0.5f, damageScale: 1.15f, speedScale: 1.2f,
                accent: PhaseTwoAccent, pause: new Vector2(0.65f, 1.1f),
                transitionSeconds: 2f, frenzyBelow: 0.2f,
                attacks: new[] { a.GreaterRitual, a.Beam, a.Judgement, a.Embers, a.Runes, a.Curse });

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            return definition;
        }

        // ==================================================================== prefab

        private static GameObject CreatePrefab(BossDefinition definition)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("Boss_SelloProfano");
            BossAuthoring.TrySetTag(root, "Enemy");

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * VisualScale;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SealTexture);
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 5;

            // Flota: la base del sprite se sube HoverHeight sobre el origen (que sigue siendo el
            // suelo, como en todos los jefes, para que plantarlo sea igualar la Y).
            var bounds = renderer.bounds;
            visual.transform.localPosition += new Vector3(0f, -bounds.min.y + HoverHeight, 0f);
            bounds = renderer.bounds;

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.freezeRotation = true;

            // Redondo: un círculo pide collider de círculo, y así se puede pasar por debajo.
            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = Mathf.Max(0.5f, bounds.size.x * 0.5f * ColliderFactor);
            collider.offset = new Vector2(0f, HoverHeight + bounds.size.y * 0.5f);

            var health = root.AddComponent<Health>();
            new BossAuthoring.Fields(health)
                .Set("maxHealth", 2500f).Set("currentHealth", 2500f)
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
                .Set("arenaHalfWidth", 17f)
                .Set("arenaHeight", 13f)
                .Set("activationRadius", 18f)
                .Set("introSeconds", 2.4f)
                .Set("contactDamage", 14f)
                .Set("contactDamageCooldown", 0.8f)
                .Set("contactKnockbackMultiplier", 1.4f)
                // Un sello colgado en el aire oscila; es lo único que lo separa de un dibujo.
                .Set("swayDegrees", 4f)
                .Set("swaySpeed", 0.7f)
                .Apply();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }
    }
}
