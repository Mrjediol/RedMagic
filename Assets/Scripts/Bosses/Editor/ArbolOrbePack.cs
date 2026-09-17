using System.Linq;
using RedMagic.Fx;
using RedMagic.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Monta el <b>orbe del Árbol Ancestral</b>: coge las tres tiras que
    /// <c>OrbeSheetSlicer</c> sacó de la lámina original (idle / vuelo / impacto), construye los
    /// prefabs que las usan, le pone el arte a las balas del jefe y crea el ataque nuevo
    /// <see cref="OrbRingAttack"/> con sus dos variantes.
    ///
    /// Idempotente como el resto de packs: nunca pisa un prefab ni un asset que ya exista, así que
    /// se puede relanzar después de haber ajustado números a mano.
    /// </summary>
    public static class ArbolOrbePack
    {
        private const string OrbFolder = "Assets/Prefabs/Fx/Bosses/ArbolAncestral/Orbe";
        private const string BossFxFolder = "Assets/Prefabs/Fx/Bosses/ArbolAncestral";
        private const string BossFolder = "Assets/Resources/Bosses";

        private const string IdleSheet = OrbFolder + "/Orbe_Idle.png";
        private const string MoveSheet = OrbFolder + "/Orbe_Move.png";
        private const string ImpactSheet = OrbFolder + "/Orbe_Impact.png";

        private const string ChargePath = OrbFolder + "/Fx_Orbe_Carga.prefab";
        private const string ImpactPath = OrbFolder + "/Fx_Orbe_Impacto.prefab";
        private const string BulletPath = BossFxFolder + "/Fx_ArbolAncestral_Bullet.prefab";
        private const string DefinitionPath = BossFolder + "/Boss_ArbolAncestral.asset";

        // Las hojas van a 100 px/unidad, así que la celda de vuelo mide 2.18 × 1.27 unidades y la
        // bola de dentro unas 0.6. Estas escalas la dejan en ~0.5, que es el calibre que ya usaban
        // las balas de este jefe.
        private const float BulletScale = 0.85f;
        private const float ChargeScale = 0.6f;
        private const float ImpactScale = 0.35f;

        private static readonly Color PhaseOneAccent = new Color(0.45f, 0.85f, 0.38f, 1f);
        private static readonly Color PhaseTwoAccent = new Color(0.78f, 0.33f, 0.85f, 1f);

        [MenuItem("Tools/RedMagic/Boss/Arbol · Orbe (arte + Corona de Orbes)")]
        public static void Generate()
        {
            var idle = SpritesOf(IdleSheet);
            var move = SpritesOf(MoveSheet);
            var impact = SpritesOf(ImpactSheet);

            if (idle.Length == 0 || move.Length == 0 || impact.Length == 0)
            {
                Debug.LogError("[ArbolOrbePack] Faltan las hojas del orbe. Lanza antes " +
                               "Tools > RedMagic > FX > Orbe · Cortar hoja.");
                return;
            }

            var impactPrefab = BuildImpact(impact);
            var chargePrefab = BuildCharge(idle);
            var bullet = DressBullet(move, impactPrefab);

            var crown = CreateAttacks(chargePrefab, bullet);
            AddToDecks(crown.small, crown.big);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArbolOrbePack] Orbe listo: prefabs en " + OrbFolder +
                      ", balas del Árbol con arte y ataque Corona de Orbes en las dos fases.");
        }

        // ================================================================= sprites

        /// <summary>Los sprites de una hoja, en el orden del sufijo numérico de su nombre.</summary>
        private static Sprite[] SpritesOf(string sheetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                .OfType<Sprite>()
                .OrderBy(s =>
                {
                    int at = s.name.LastIndexOf('_');
                    return at >= 0 && int.TryParse(s.name.Substring(at + 1), out int n) ? n : 0;
                })
                .ToArray();
        }

        // ================================================================= prefabs

        /// <summary>Estallido del orbe: se reproduce una vez y se devuelve solo al pool.</summary>
        private static GameObject BuildImpact(Sprite[] frames)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPath);
            if (existing != null) return existing;

            var go = new GameObject("Fx_Orbe_Impacto");
            go.transform.localScale = Vector3.one * ImpactScale;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 7;   // por delante de la bala que acaba de reventar

            float fps = 15f;
            var flipbook = go.AddComponent<SpriteFlipbook>();
            new BossAuthoring.Fields(flipbook)
                .SetObjectArray("frames", frames)
                .Set("framesPerSecond", fps)
                .Set("pingPong", false)
                .Set("randomStart", false)
                .Set("oneShot", true)
                .Apply();

            // VfxOneShot sólo sabe medir clips de Animator, así que la duración se le dice a mano.
            var oneShot = go.AddComponent<VfxOneShot>();
            new BossAuthoring.Fields(oneShot)
                .Set("lifetime", frames.Length / fps)
                .Set("extraTime", 0.05f)
                .Apply();

            return SaveAndDiscard(go, ImpactPath);
        }

        /// <summary>Orbe de carga: el que da vueltas alrededor del jefe mientras crece.</summary>
        private static GameObject BuildCharge(Sprite[] frames)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ChargePath);
            if (existing != null) return existing;

            var go = new GameObject("Fx_Orbe_Carga");
            go.transform.localScale = Vector3.one * ChargeScale;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 6;

            var flipbook = go.AddComponent<SpriteFlipbook>();
            new BossAuthoring.Fields(flipbook)
                .SetObjectArray("frames", frames)
                .Set("framesPerSecond", 8f)
                .Set("pingPong", true)
                .Set("randomStart", true)   // los orbes de una misma corona no laten al unísono
                .Set("oneShot", false)
                .Apply();

            go.AddComponent<BossOrb>();

            return SaveAndDiscard(go, ChargePath);
        }

        /// <summary>
        /// Le pone el arte del orbe a la bala del Árbol. Este prefab lo comparten los cinco ataques
        /// bullet-hell del jefe, así que a partir de aquí todo lo que dispara son orbes.
        ///
        /// Al dejar de ser un placeholder se apagan <c>tint</c> y <c>resize</c>: el sprite ya trae su
        /// propio color, y la celda de vuelo no es cuadrada (lleva la estela), así que dejar que
        /// <c>FxPlaceholderStyle</c> la estirase a un tamaño cuadrado convertiría la bola en un
        /// huevo. El tamaño pasa a fijarlo la escala del prefab.
        /// </summary>
        private static GameObject DressBullet(Sprite[] frames, GameObject impactPrefab)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPath);
            if (asset == null)
            {
                Debug.LogWarning("[ArbolOrbePack] No está " + BulletPath + "; se salta el vestido de la bala.");
                return null;
            }

            var root = PrefabUtility.LoadPrefabContents(BulletPath);
            try
            {
                if (root.GetComponent<SpriteFlipbook>() != null) return asset;   // ya vestida

                root.transform.localScale = Vector3.one * BulletScale;

                var renderer = root.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    renderer.sprite = frames[0];
                    renderer.color = Color.white;
                }

                var flipbook = root.AddComponent<SpriteFlipbook>();
                new BossAuthoring.Fields(flipbook)
                    .SetObjectArray("frames", frames)
                    .Set("framesPerSecond", 16f)
                    .Set("pingPong", false)
                    .Set("randomStart", true)
                    .Set("oneShot", false)
                    .Apply();

                // El pivote de la hoja está en la bola, no en el centro de la celda, así que el
                // collider va centrado en el origen y sólo cubre la bola: la estela no golpea.
                var circle = root.GetComponent<CircleCollider2D>();
                if (circle != null)
                {
                    circle.radius = 0.30f;
                    circle.offset = Vector2.zero;
                }

                var style = root.GetComponent<FxPlaceholderStyle>();
                if (style != null)
                {
                    new BossAuthoring.Fields(style)
                        .Set("tint", false)
                        .Set("resize", false)
                        .Set("matchSorting", true)
                        .Set("scaleColliderToSprite", false)
                        .Apply();
                }

                var projectile = root.GetComponent<Projectile>();
                if (projectile != null && impactPrefab != null)
                    new BossAuthoring.Fields(projectile).SetObject("impactEffect", impactPrefab).Apply();

                PrefabUtility.SaveAsPrefabAsset(root, BulletPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(BulletPath);
        }

        // ================================================================= ataques

        private static (BossAttack small, BossAttack big) CreateAttacks(GameObject orbPrefab,
                                                                       GameObject bulletPrefab)
        {
            var small = BossAuthoring.Attack<OrbRingAttack>(BossFolder, "BossAttack_CoronaDeOrbes",
                "Corona de Orbes",
                "Se rodea de orbes diminutos y los deja crecer. Cuando están maduros, salen todos a " +
                "la vez. Colócate en un hueco mientras hay tiempo — o aprovecha para pegarle.",
                PhaseOneAccent, f => f
                    .Set("telegraph", 0.5f).Set("recovery", 1.35f).Set("weight", 1.3f)
                    .Set("cooldownInAttacks", 2)
                    .Set("damage", 12f).Set("knockbackMultiplier", 0.5f)
                    .Set("vulnerableSeconds", 1.3f).Set("vulnerableMultiplier", 2f)
                    .Set("shakeAmplitude", 0.35f).Set("shakeDuration", 0.3f)
                    .Set("orbCount", 9).Set("ringRadius", 2.8f)
                    .Set("originOffset", new Vector2(0f, 3f))
                    .Set("alignToPlayer", true).Set("spinDegreesPerSecond", 30f)
                    .Set("startScale", 0.15f).Set("endScale", 1f)
                    .Set("growSeconds", 1.5f).Set("appearStagger", 0.08f)
                    .Set("holdSeconds", 0.35f).Set("recoilFraction", 0.18f)
                    .Set("volleys", 1).Set("timeBetweenVolleys", 0.9f).Set("launchSpread", 0f)
                    .Set("tintOrbWithAccent", false).Set("codeOrbSize", 0.7f)
                    .SetObject("orbPrefab", orbPrefab)
                    .SetObject("projectile.prefab", bulletPrefab)
                    .Set("projectile.speed", 8f).Set("projectile.lifetime", 4.5f)
                    .Set("projectile.size", new Vector2(0.5f, 0.5f)));

            var big = BossAuthoring.Attack<OrbRingAttack>(BossFolder, "BossAttack_CoronaMayor",
                "Corona Mayor",
                "La corona completa, dos veces seguidas y girando el doble de rápido. Los huecos " +
                "existen, pero no se quedan quietos.",
                PhaseTwoAccent, f => f
                    .Set("telegraph", 0.45f).Set("recovery", 1.2f).Set("weight", 1.5f)
                    .Set("cooldownInAttacks", 3)
                    .Set("damage", 12f).Set("knockbackMultiplier", 0.5f)
                    .Set("vulnerableSeconds", 1f).Set("vulnerableMultiplier", 2f)
                    .Set("shakeAmplitude", 0.45f).Set("shakeDuration", 0.35f)
                    .Set("orbCount", 14).Set("ringRadius", 3.2f)
                    .Set("originOffset", new Vector2(0f, 3f))
                    .Set("alignToPlayer", true).Set("spinDegreesPerSecond", 55f)
                    .Set("startScale", 0.15f).Set("endScale", 1f)
                    .Set("growSeconds", 1.1f).Set("appearStagger", 0.05f)
                    .Set("holdSeconds", 0.25f).Set("recoilFraction", 0.2f)
                    .Set("volleys", 2).Set("timeBetweenVolleys", 1f).Set("launchSpread", 2f)
                    .Set("tintOrbWithAccent", false).Set("codeOrbSize", 0.7f)
                    .SetObject("orbPrefab", orbPrefab)
                    .SetObject("projectile.prefab", bulletPrefab)
                    .Set("projectile.speed", 9f).Set("projectile.lifetime", 5f)
                    .Set("projectile.size", new Vector2(0.5f, 0.5f)));

            return (small, big);
        }

        /// <summary>
        /// Mete cada corona en su fase. <c>BossStarterPack</c> no toca una definición que ya existe,
        /// así que un ataque nuevo hay que añadirlo aquí o no entraría nunca en la baraja.
        /// </summary>
        private static void AddToDecks(BossAttack small, BossAttack big)
        {
            var definition = AssetDatabase.LoadAssetAtPath<BossDefinition>(DefinitionPath);
            if (definition == null)
            {
                Debug.LogWarning("[ArbolOrbePack] No está " + DefinitionPath + "; no se tocan barajas.");
                return;
            }

            var so = new SerializedObject(definition);
            var phases = so.FindProperty("phases");
            bool touched = false;

            touched |= AddToPhase(phases, 0, small);
            touched |= AddToPhase(phases, 1, big);

            if (!touched) return;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static bool AddToPhase(SerializedProperty phases, int index, BossAttack attack)
        {
            if (attack == null || phases == null || index >= phases.arraySize) return false;

            var list = phases.GetArrayElementAtIndex(index).FindPropertyRelative("attacks");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == attack) return false;

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = attack;
            return true;
        }

        // ================================================================= util

        private static GameObject SaveAndDiscard(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[ArbolOrbePack] Prefab creado: " + path);
            return prefab;
        }
    }
}
