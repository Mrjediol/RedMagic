using System;
using RedMagic.Combat;
using RedMagic.Economy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Crea de golpe el primer jefe del juego: sus ataques como assets, su
    /// <see cref="BossDefinition"/> y su prefab ya cableado.
    ///
    /// Es <b>idempotente</b>, igual que el Ability Starter Pack: no toca nada que ya exista, así
    /// que se puede volver a ejecutar tras haber retocado números a mano y sólo aparecerá lo que
    /// falte. Para un jefe nuevo, duplica los assets y cambia valores — escribir C# sólo hace
    /// falta para un <i>arquetipo</i> de ataque nuevo.
    /// </summary>
    public static class BossStarterPack
    {
        private const string BossFolder = "Assets/Resources/Bosses";
        private const string PrefabFolder = "Assets/Prefab/Enemies";
        private const string PrefabPath = PrefabFolder + "/Boss_ArbolAncestral.prefab";
        private const string DefinitionPath = BossFolder + "/Boss_ArbolAncestral.asset";

        private const string TreePrefabPath =
            "Assets/Cainos/Pixel Art Platformer - Village Props/Prefab/PF Village Props - Tree 01.prefab";

        private const string BossScenePath = "Assets/Scenes/Worlds/World1/World1_Boss.unity";

        /// <summary>
        /// Escala del sprite del árbol. Se ha ajustado a la cámara: con un tamaño ortográfico de
        /// 8.5 caben unas 17 unidades de alto, así que un jefe de ~8 ocupa media pantalla —
        /// imponente, pero dejando sitio para ver sus ataques y esquivarlos.
        /// </summary>
        private const float VisualScale = 2.2f;

        /// <summary>Parte del ancho / alto del sprite que ocupa la caja de golpeo (el tronco).</summary>
        private const float ColliderWidthFactor = 0.42f;
        private const float ColliderHeightFactor = 0.75f;

        private static readonly Color PhaseOneAccent = new Color(0.45f, 0.85f, 0.38f, 1f);
        private static readonly Color PhaseTwoAccent = new Color(0.78f, 0.33f, 0.85f, 1f);

        // ==================================================================== menú

        [MenuItem("Tools/RedMagic/Boss/Crear jefe: Arbol Ancestral")]
        public static void Generate()
        {
            EnsureTag("Enemy");
            EnsureFolder(BossFolder);
            EnsureFolder(PrefabFolder);

            var attacks = CreateAttacks();
            var definition = CreateDefinition(attacks);
            var prefab = CreatePrefab(definition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BossStarterPack] Jefe listo: {definition.name}. " +
                      $"Colócalo con Tools > RedMagic > Boss > Colocar en World1_Boss.", definition);

            Selection.activeObject = prefab != null ? (Object)prefab : definition;
        }

        [MenuItem("Tools/RedMagic/Boss/Colocar Arbol Ancestral en World1_Boss")]
        public static void PlaceInBossScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[BossStarterPack] No existe el prefab del jefe. Ejecuta primero " +
                               "'Crear jefe: Arbol Ancestral'.");
                return;
            }

            // Se abre en aditivo y se cierra al terminar: así la escena en la que esté trabajando
            // el usuario no se descarga ni se le pide guardarla.
            var scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Additive);
            if (!scene.IsValid())
            {
                Debug.LogError($"[BossStarterPack] No se pudo abrir '{BossScenePath}'.");
                return;
            }

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<BossController>(true) == null) continue;

                    Debug.LogWarning("[BossStarterPack] La escena ya tiene un jefe; no se toca nada.", root);
                    return;
                }

                // Se busca el suelo real bajo ese punto en vez de fijar una altura: la escena
                // puede tener tilemap, plataformas o un placeholder, y el origen del prefab es la
                // base del jefe. (En juego, BossController vuelve a plantarse solo.)
                var position = new Vector3(6f, GroundYAt(6f, fallback: -3f), 0f);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.position = position;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                Debug.Log($"[BossStarterPack] Jefe colocado en '{BossScenePath}' en {position}.", prefab);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        /// <summary>
        /// Altura del suelo (capa Ground) bajo <paramref name="x"/> en la escena recién abierta.
        /// Hace falta sincronizar la física porque los transforms acaban de deserializarse y el
        /// mundo 2D todavía no los conoce.
        /// </summary>
        private static float GroundYAt(float x, float fallback)
        {
            Physics2D.SyncTransforms();

            int groundLayer = LayerMask.NameToLayer("Ground");
            int mask = groundLayer >= 0 ? 1 << groundLayer : Physics2D.AllLayers;

            var hit = Physics2D.Raycast(new Vector2(x, 40f), Vector2.down, 120f, mask);
            return hit.collider != null ? hit.point.y : fallback;
        }

        // ==================================================================== ataques

        /// <summary>
        /// La baraja del jefe. Cada entrada es un asset; los que ya existan se reutilizan tal cual,
        /// para no pisar números afinados a mano.
        /// </summary>
        private sealed class Attacks
        {
            public BossAttack RootStomp;
            public BossAttack BranchSweep;
            public BossAttack AcornRain;
            public BossAttack ThornVolley;
            public BossAttack CursedSprouts;

            public BossAttack SporeSpiral;
            public BossAttack RootQuake;
            public BossAttack ForestWhip;
            public BossAttack AcornDeluge;
            public BossAttack SporeStorm;
        }

        private static Attacks CreateAttacks()
        {
            var a = new Attacks();

            // ---------------------------------------------------------------- fase 1

            // Pisotón: la franja pegada al suelo. Se esquiva SALTANDO.
            a.RootStomp = Attack<ShockwaveAttack>("BossAttack_PisotonDeRaices", "Pisotón de Raíces",
                "Las raíces revientan el suelo a los dos lados. Salta por encima de la onda.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.85f).Set("recovery", 1.25f).Set("weight", 1.4f)
                    .Set("damage", 22f).Set("knockbackMultiplier", 1.6f)
                    .Set("shakeAmplitude", 0.45f).Set("shakeDuration", 0.35f)
                    .Set("bandMin", 0f).Set("bandMax", 1.9f)
                    .Set("waves", 1).Set("bothDirections", true)
                    .Set("width", 1.7f).Set("speed", 15f).Set("spawnInset", 1.6f));

            // Barrido: la franja alta. Se esquiva QUEDÁNDOSE EN EL SUELO. Es el par del anterior,
            // y tenerlos los dos es lo que obliga a leer el aviso en vez de saltar por reflejo.
            a.BranchSweep = Attack<ShockwaveAttack>("BossAttack_BarridoDeRamas", "Barrido de Ramas",
                "Una rama barre la arena a la altura del pecho. No saltes: quédate abajo.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.95f).Set("recovery", 1.35f).Set("weight", 1.1f)
                    .Set("damage", 20f).Set("knockbackMultiplier", 1.9f)
                    .Set("shakeAmplitude", 0.3f)
                    .Set("bandMin", 1.9f).Set("bandMax", 9f)
                    .Set("waves", 1).Set("bothDirections", true)
                    .Set("width", 1.5f).Set("speed", 13f).Set("spawnInset", 1.6f));

            a.AcornRain = Attack<BulletHellAttack>("BossAttack_LluviaDeBellotas", "Lluvia de Bellotas",
                "Marca el suelo y deja caer bellotas sobre las marcas. Muévete.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.5f).Set("recovery", 1.1f).Set("weight", 1.2f)
                    .Set("damage", 13f).Set("knockbackMultiplier", 0.6f)
                    .Set("pattern", (int)BulletPattern.Rain)
                    .Set("volleys", 2).Set("timeBetweenVolleys", 0.5f)
                    .Set("bulletsPerVolley", 7)
                    .Set("rainSpan", 0.95f).Set("rainMarkerSeconds", 0.55f).Set("rainJitter", 0.7f)
                    .Set("projectile.speed", 15f).Set("projectile.lifetime", 3f)
                    .Set("projectile.size", new Vector2(0.5f, 0.5f)));

            a.ThornVolley = Attack<BulletHellAttack>("BossAttack_SalvaDeEspinas", "Salva de Espinas",
                "Tres abanicos de espinas apuntados. Hay hueco: no te quedes en línea recta.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.6f).Set("recovery", 0.9f).Set("weight", 1.3f)
                    .Set("damage", 11f).Set("knockbackMultiplier", 0.5f)
                    .Set("pattern", (int)BulletPattern.Fan)
                    .Set("volleys", 3).Set("timeBetweenVolleys", 0.3f)
                    .Set("bulletsPerVolley", 5)
                    .Set("arcDegrees", 52f).Set("spinPerVolley", 9f).Set("alternateSpin", true)
                    .Set("aimAtPlayer", true).Set("randomSpread", 1.5f)
                    .Set("originOffset", new Vector2(0f, 2.4f)).Set("spawnRadius", 1f)
                    .Set("projectile.speed", 11f).Set("projectile.lifetime", 3.5f)
                    .Set("projectile.size", new Vector2(0.42f, 0.42f)));

            a.CursedSprouts = Attack<SummonAddsAttack>("BossAttack_BrotesMalditos", "Brotes Malditos",
                "Escupe brotes que salen del suelo. Deja de mirar sólo al jefe.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.9f).Set("recovery", 1.5f).Set("weight", 0.75f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 0f)
                    .Set("count", 2).Set("maxAlive", 4)
                    .Set("markerSeconds", 0.6f).Set("timeBetweenSpawns", 0.15f)
                    .Set("distanceRange", new Vector2(5f, 11f)).Set("spawnHeight", 0.9f));

            // ---------------------------------------------------------------- fase 2

            // El centro de la fase 2: anillo completo con giro, o sea espiral. 13 no divide a 360,
            // así que el hueco se desplaza en cada oleada y hay que moverse CON él.
            a.SporeSpiral = Attack<BulletHellAttack>("BossAttack_EspiralDeEsporas", "Espiral de Esporas",
                "Un anillo de esporas que gira. El hueco se mueve: síguelo.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.7f).Set("recovery", 1.15f).Set("weight", 1.6f)
                    .Set("damage", 10f).Set("knockbackMultiplier", 0.35f)
                    .Set("pattern", (int)BulletPattern.Radial)
                    .Set("volleys", 6).Set("timeBetweenVolleys", 0.22f)
                    .Set("bulletsPerVolley", 13)
                    .Set("arcDegrees", 360f).Set("spinPerVolley", 13f).Set("aimAtPlayer", false)
                    .Set("originOffset", new Vector2(0f, 3f)).Set("spawnRadius", 1.4f)
                    .Set("projectile.speed", 7.5f).Set("projectile.lifetime", 5f)
                    .Set("projectile.size", new Vector2(0.4f, 0.4f)));

            a.SporeStorm = Attack<BulletHellAttack>("BossAttack_TormentaDeEsporas", "Tormenta de Esporas",
                "Dos espirales cruzadas. Los huecos abren y cierran: espera el momento.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.8f).Set("recovery", 1.4f).Set("weight", 1.1f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 10f).Set("knockbackMultiplier", 0.35f)
                    .Set("pattern", (int)BulletPattern.Radial)
                    .Set("volleys", 10).Set("timeBetweenVolleys", 0.18f)
                    .Set("bulletsPerVolley", 9)
                    .Set("arcDegrees", 360f).Set("spinPerVolley", 21f).Set("alternateSpin", true)
                    .Set("aimAtPlayer", false)
                    .Set("originOffset", new Vector2(0f, 3f)).Set("spawnRadius", 1.4f)
                    .Set("projectile.speed", 6.5f).Set("projectile.lifetime", 6f)
                    .Set("projectile.size", new Vector2(0.38f, 0.38f)));

            a.RootQuake = Attack<ShockwaveAttack>("BossAttack_TerremotoDeRaices", "Terremoto de Raíces",
                "Tres pisotones seguidos, cada uno más rápido. Encadena los saltos.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.9f).Set("recovery", 1.5f).Set("weight", 1.2f)
                    .Set("damage", 24f).Set("knockbackMultiplier", 1.6f)
                    .Set("shakeAmplitude", 0.55f).Set("shakeDuration", 0.4f)
                    .Set("bandMin", 0f).Set("bandMax", 2f)
                    .Set("waves", 3).Set("timeBetweenWaves", 0.55f).Set("speedRampPerWave", 1.18f)
                    .Set("bothDirections", true)
                    .Set("width", 1.8f).Set("speed", 14f).Set("spawnInset", 1.6f));

            // El ataque que combina las dos alturas: salta, aterriza, salta. Es el "examen final".
            a.ForestWhip = Attack<ShockwaveAttack>("BossAttack_LatigoDelBosque", "Látigo del Bosque",
                "Suelo, altura, suelo. Salta, aterriza y vuelve a saltar.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 1.05f).Set("recovery", 1.7f).Set("weight", 1f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 22f).Set("knockbackMultiplier", 1.8f)
                    .Set("shakeAmplitude", 0.4f)
                    .Set("bandMin", 0f).Set("bandMax", 1.9f)
                    .Set("alternateBands", true).Set("bandMinB", 1.9f).Set("bandMaxB", 9f)
                    .Set("waves", 3).Set("timeBetweenWaves", 0.75f)
                    .Set("bothDirections", true)
                    .Set("width", 1.6f).Set("speed", 13f).Set("spawnInset", 1.6f));

            a.AcornDeluge = Attack<BulletHellAttack>("BossAttack_DiluvioDeBellotas", "Diluvio de Bellotas",
                "La lluvia, pero en serio. Tres tandas y casi sin huecos fijos.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.5f).Set("recovery", 1.2f).Set("weight", 1.1f)
                    .Set("damage", 13f).Set("knockbackMultiplier", 0.6f)
                    .Set("pattern", (int)BulletPattern.Rain)
                    .Set("volleys", 3).Set("timeBetweenVolleys", 0.42f)
                    .Set("bulletsPerVolley", 10)
                    .Set("rainSpan", 1f).Set("rainMarkerSeconds", 0.45f).Set("rainJitter", 1f)
                    .Set("projectile.speed", 16f).Set("projectile.lifetime", 3f)
                    .Set("projectile.size", new Vector2(0.5f, 0.5f)));

            return a;
        }

        // ==================================================================== definición

        private static BossDefinition CreateDefinition(Attacks a)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BossDefinition>(DefinitionPath);
            if (existing != null) return existing;

            var definition = ScriptableObject.CreateInstance<BossDefinition>();
            AssetDatabase.CreateAsset(definition, DefinitionPath);

            var so = new SerializedObject(definition);
            so.FindProperty("displayName").stringValue = "Árbol Ancestral";
            so.FindProperty("title").stringValue = "Guardián del Bosque Marchito";
            so.FindProperty("description").stringValue =
                "Lleva tanto tiempo enraizado en la tumba que ya no distingue entre defenderla y " +
                "devorar lo que entra.";

            var phases = so.FindProperty("phases");
            phases.arraySize = 2;

            WritePhase(phases.GetArrayElementAtIndex(0),
                       "Raíces Despiertas", startsAtHealth: 1f, damageScale: 1f, speedScale: 1f,
                       accent: PhaseOneAccent, pause: new Vector2(1f, 1.7f),
                       transitionSeconds: 0f, frenzyBelow: 0f,
                       attacks: new[] { a.RootStomp, a.BranchSweep, a.AcornRain, a.ThornVolley, a.CursedSprouts });

            // Fase 2: la misma idea, más rápida, más daño y con los patrones que piden leer de
            // verdad. Por debajo del 20% entra en frenesí y ya no da respiro.
            WritePhase(phases.GetArrayElementAtIndex(1),
                       "Corazón Podrido", startsAtHealth: 0.55f, damageScale: 1.15f, speedScale: 1.2f,
                       accent: PhaseTwoAccent, pause: new Vector2(0.7f, 1.2f),
                       transitionSeconds: 2f, frenzyBelow: 0.2f,
                       attacks: new[]
                       {
                           a.SporeSpiral, a.SporeStorm, a.RootQuake, a.ForestWhip,
                           a.AcornDeluge, a.ThornVolley, a.CursedSprouts
                       });

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            return definition;
        }

        private static void WritePhase(SerializedProperty phase, string displayName, float startsAtHealth,
                                       float damageScale, float speedScale, Color accent, Vector2 pause,
                                       float transitionSeconds, float frenzyBelow, BossAttack[] attacks)
        {
            phase.FindPropertyRelative("displayName").stringValue = displayName;
            phase.FindPropertyRelative("startsAtHealth").floatValue = startsAtHealth;
            phase.FindPropertyRelative("damageScale").floatValue = damageScale;
            phase.FindPropertyRelative("speedScale").floatValue = speedScale;
            phase.FindPropertyRelative("accent").colorValue = accent;
            phase.FindPropertyRelative("pauseBetweenAttacks").vector2Value = pause;
            phase.FindPropertyRelative("transitionSeconds").floatValue = transitionSeconds;
            phase.FindPropertyRelative("transitionShake").floatValue = transitionSeconds > 0f ? 0.7f : 0f;
            phase.FindPropertyRelative("frenzyBelowHealth").floatValue = frenzyBelow;
            phase.FindPropertyRelative("frenzySpeedScale").floatValue = 1.35f;

            var list = phase.FindPropertyRelative("attacks");
            list.arraySize = attacks.Length;
            for (int i = 0; i < attacks.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = attacks[i];
        }

        // ==================================================================== prefab

        private static GameObject CreatePrefab(BossDefinition definition)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("Boss_ArbolAncestral");
            root.transform.position = Vector3.zero;
            TrySetTag(root, "Enemy");

            // ---- visual: se reaprovecha el árbol del pack de props, escalado a tamaño de jefe.
            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * VisualScale;

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadTreeSprite();
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 5;

            // El origen del jefe es su base: así 'MeasureGroundY' y la colocación en la escena
            // funcionan poniéndolo simplemente a la altura del suelo.
            var bounds = renderer.bounds;
            visual.transform.localPosition += new Vector3(0f, -bounds.min.y, 0f);
            bounds = renderer.bounds;

            // ---- física: inamovible, pero con contactos para el daño por roce.
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.freezeRotation = true;

            // Caja de golpeo pegada al tronco y a la base: no llega a la copa, para que estar
            // debajo de las ramas no cuente como estar tocando al jefe.
            var collider = root.AddComponent<BoxCollider2D>();
            float width = Mathf.Max(1f, bounds.size.x * ColliderWidthFactor);
            float height = Mathf.Max(1f, bounds.size.y * ColliderHeightFactor);
            collider.size = new Vector2(width, height);
            collider.offset = new Vector2(0f, height * 0.5f);

            // ---- combate
            var health = root.AddComponent<Health>();
            new Fields(health)
                .Set("maxHealth", 2600f)
                .Set("currentHealth", 2600f)
                // Sin i-frames, como el resto de enemigos: si no, una escopeta de 5 perdigones
                // sólo le metería uno.
                .Set("invulnerabilityDuration", 0f)
                .Apply();

            var knockback = root.AddComponent<Knockback>();
            new Fields(knockback).Set("immune", true).Set("resistance", 1f).Apply();

            root.AddComponent<HitFlash>();

            var corpse = root.AddComponent<Corpse>();
            new Fields(corpse).Set("linger", 1.6f).Set("fadeDuration", 1.4f).Apply();

            var dropper = root.AddComponent<CurrencyDropper>();
            new Fields(dropper).Set("tier", (int)EnemyTier.Boss).Apply();

            var controller = root.AddComponent<BossController>();
            new Fields(controller)
                .SetObject("definition", definition)
                .Set("arenaHalfWidth", 15f)
                .Set("arenaHeight", 13f)
                .Set("activationRadius", 18f)
                .Set("introSeconds", 2.2f)
                // Vacío a propósito: 'Music_Boss' todavía no está dado de alta en las listas del
                // AudioManager, y un id inexistente sólo dejaría un warning por combate.
                .Set("musicId", string.Empty)
                .Set("contactDamage", 14f)
                .Set("contactDamageCooldown", 0.8f)
                .Set("contactKnockbackMultiplier", 1.4f)
                .Set("swayDegrees", 1.6f)
                .Apply();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }

        private static Sprite LoadTreeSprite()
        {
            var treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            var source = treePrefab != null ? treePrefab.GetComponentInChildren<SpriteRenderer>(true) : null;

            if (source != null && source.sprite != null) return source.sprite;

            Debug.LogWarning("[BossStarterPack] No se encontró el sprite del árbol; el jefe se queda " +
                             "sin sprite (asígnaselo a mano en el prefab).");
            return null;
        }

        // ==================================================================== utilidades

        private static T Attack<T>(string fileName, string displayName, string description,
                                   Color accent, Func<Fields, Fields> configure) where T : BossAttack
        {
            string path = $"{BossFolder}/{fileName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);

            var fields = new Fields(asset)
                .Set("displayName", displayName)
                .Set("description", description)
                .Set("accent", accent);

            configure(fields);
            fields.Apply();

            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// Da de alta una etiqueta si falta. El jefe y sus esbirros la comparten, y eso es lo que
        /// impide que los proyectiles del jefe maten a sus propias invocaciones.
        /// </summary>
        private static void EnsureTag(string tag)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0) return;

            var so = new SerializedObject(asset[0]);
            var tags = so.FindProperty("tags");

            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[BossStarterPack] Etiqueta '{tag}' dada de alta.");
        }

        private static void TrySetTag(GameObject go, string tag)
        {
            try
            {
                go.tag = tag;
            }
            catch (UnityException)
            {
                Debug.LogWarning($"[BossStarterPack] No se pudo poner la etiqueta '{tag}'.", go);
            }
        }

        /// <summary>
        /// Escritor de campos serializados encadenable, igual que el del Ability Starter Pack: los
        /// campos de los ataques son privados a propósito y sólo el editor debe rellenarlos.
        /// </summary>
        private class Fields
        {
            private readonly SerializedObject _so;

            public Fields(Object target) => _so = new SerializedObject(target);

            public Fields Set(string path, float value) => Write(path, p => p.floatValue = value);
            public Fields Set(string path, int value) => Write(path, p => p.intValue = value);
            public Fields Set(string path, bool value) => Write(path, p => p.boolValue = value);
            public Fields Set(string path, string value) => Write(path, p => p.stringValue = value);
            public Fields Set(string path, Vector2 value) => Write(path, p => p.vector2Value = value);
            public Fields Set(string path, Color value) => Write(path, p => p.colorValue = value);
            public Fields SetObject(string path, Object value) => Write(path, p => p.objectReferenceValue = value);

            private Fields Write(string path, Action<SerializedProperty> write)
            {
                var property = _so.FindProperty(path);
                if (property == null)
                {
                    Debug.LogWarning($"[BossStarterPack] El campo '{path}' no existe en {_so.targetObject.name}.");
                    return this;
                }

                write(property);
                return this;
            }

            public void Apply() => _so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
