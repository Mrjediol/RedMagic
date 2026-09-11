using System.Collections.Generic;
using System.Linq;
using System.Text;
using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Fx;
using RedMagic.FxTools;
using RedMagic.Gameplay;
using RedMagic.Pipeline;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// El <b>TreeBoss</b> (Ent Cristalino): primer jefe animado de verdad — su cuerpo es una lámina
    /// con un gesto por ataque, y cada ataque sale en el frame de su gesto (<see cref="BossAnimator"/>).
    ///
    /// Es a la vez la plantilla de un jefe con lámina propia. De dos láminas sale todo:
    /// <list type="bullet">
    ///   <item><c>Assets/Sprites/TreeBoss.png</c> — el cuerpo, una fila por estado (reposo, cargar,
    ///         golpe al suelo, invocar, tambaleo, muerte). Pasa por el pipeline de enemigos
    ///         (<see cref="SpritePipeline.RunSheet"/>) y sale con clips, controller y los eventos de
    ///         suelta en cada gesto.</item>
    ///   <item><c>Assets/Sprites/BossAttack.png</c> — los efectos (orbe quieto / en vuelo / estallido,
    ///         púa que brota / se retira, onda de raíces). Se corta con dos recetas sobre la misma
    ///         lámina, recortada por arriba o por abajo, porque los orbes van centrados y lo que sale
    ///         del suelo va con el pivote abajo.</item>
    /// </list>
    ///
    /// Los ataques son assets de arquetipos (<see cref="OrbRingAttack"/>, <see cref="GroundSlamAttack"/>,
    /// <see cref="QuakeSlamAttack"/>, y <see cref="PlatformDenialAttack"/> como apertura de la fase 2)
    /// con su <c>gesture</c> puesto: un ataque nuevo para este jefe
    /// es otro asset más en su baraja, y un gesto nuevo es otra fila en la lámina.
    ///
    /// Idempotente como el resto de packs: recetas, ataques, definición y prefabs no se pisan si ya
    /// existen (mandan los números afinados a mano). Lo único que se refresca siempre es el ARTE —
    /// los frames de los efectos y el controller del cuerpo —, para que relanzar tras cambiar la
    /// lámina no deje referencias a sprites viejos.
    /// </summary>
    public static class TreeBossPack
    {
        // ================================================================= rutas

        private const string BodySheet = "Assets/Sprites/TreeBoss.png";
        private const string FxSheet = "Assets/Sprites/BossAttack.png";

        private const string ArtFolder = "Assets/Art/Characters/TreeBoss";
        private const string BodyRecipePath = ArtFolder + "/TreeBoss.sheet.asset";
        private const string ControllerPath = ArtFolder + "/TreeBoss.controller";

        private const string FxFolder = "Assets/Prefab/Fx/Bosses/TreeBoss";
        private const string FxArtFolder = FxFolder + "/Art";
        private const string OrbRecipePath = FxArtFolder + "/TreeBossOrb.sheet.asset";
        private const string RootRecipePath = FxArtFolder + "/TreeBossRoot.sheet.asset";

        // "_Bullet" es el nombre que la convención da a la bala de cada jefe: si algún día se le
        // añade un ataque bullet-hell, 'FX ▸ 3 · Asignar a jefes' ya le pone el orbe. La onda de
        // raíces NO se llama "_Shockwave": ése es el slot del BossShockwave pooled (otra cosa).
        private const string OrbChargePath = FxFolder + "/Fx_TreeBoss_OrbCharge.prefab";
        private const string BulletPath = FxFolder + "/Fx_TreeBoss_Bullet.prefab";
        private const string OrbImpactPath = FxFolder + "/Fx_TreeBoss_OrbImpact.prefab";
        private const string RootSpikePath = FxFolder + "/Fx_TreeBoss_RootSpike.prefab";
        private const string RootWavePath = FxFolder + "/Fx_TreeBoss_RootWave.prefab";

        // PLACEHOLDER del fuego verde (el arte llega aparte): un cuadrado teñido con FxPlaceholderStyle.
        // El arte final se mete editando ESTOS prefabs (sprite + SpriteFlipbook; en el estilo, tint a
        // false, y resize a false si la celda no es cuadrada); el ataque no se toca. El pack no los
        // pisa nunca si ya existen.
        private const string FireBoltPath = FxFolder + "/Fx_TreeBoss_FireBolt.prefab";
        private const string FireHazardPath = FxFolder + "/Fx_TreeBoss_FireHazard.prefab";
        private const string PlaceholderSquare = "Assets/Art/Placeholder/Square.png";

        // PLACEHOLDER de las hojas mágicas (el arte llega aparte). La hoja es una bala de jefe
        // (Projectile) con el cuadrado y FxPlaceholderStyle; el impacto, un VfxOneShot con el cuadrado
        // aplastado. Arte final = editar ESTOS prefabs; el ataque no se toca. El pack no los pisa.
        private const string LeafPath = FxFolder + "/Fx_TreeBoss_Leaf.prefab";
        private const string LeafImpactPath = FxFolder + "/Fx_TreeBoss_LeafImpact.prefab";
        private static readonly Color LeafPlaceholderColor = new Color(0.9f, 0.95f, 0.35f, 1f);

        private const string BossFolder = "Assets/Resources/Bosses";
        private const string DefinitionPath = BossFolder + "/Boss_TreeBoss.asset";
        private const string PrefabFolder = "Assets/Prefab/Enemies";
        private const string PrefabPath = PrefabFolder + "/Boss_TreeBoss.prefab";
        private const string BossScenePath = "Assets/Scenes/Worlds/World1/World1_Boss.unity";

        /// <summary>Gestos del cuerpo: filas de la lámina y valor de <c>gesture</c> en los ataques.</summary>
        public const string Charge = "Charge";
        public const string Slam = "Slam";
        public const string Summon = "Summon";

        /// <summary>
        /// Línea que separa, en la lámina de efectos, las filas de orbe (arriba) de las que salen del
        /// suelo (abajo), en píxeles desde arriba. Medida con 'TreeBoss · Diagnosticar láminas'.
        /// </summary>
        private const int FxSplitFromTop = 557;
        private const int FxSheetHeight = 1199;

        // ================================================================= tamaños en el mundo

        /// <summary>Alto del jefe. Con ortho 8.5 caben ~17 unidades: media pantalla, como el Árbol.</summary>
        private const float BodyHeight = 7.5f;

        /// <summary>Ancho de la celda del orbe de carga (la bola con su corona de lianas).</summary>
        private const float ChargeCellWidth = 1.5f;

        /// <summary>Diámetro de la bola en vuelo. La estela va aparte, detrás.</summary>
        private const float BulletBall = 0.75f;

        private const float ImpactWidth = 2.2f;

        /// <summary>Anchos de referencia: al golpear, GroundSlamAttack los ajusta a su radio.</summary>
        private const float SpikeWidth = 2.4f;
        private const float WaveWidth = 9f;

        /// <summary>Cuánto de la onda queda por debajo de la línea del suelo: el anillo va tumbado.</summary>
        private const float WaveSink = 0.3f;

        // ================================================================= números del combate

        private const float MaxHealth = 2400f;

        private static readonly Color PhaseOneAccent = new Color(0.35f, 0.95f, 0.8f, 1f);
        private static readonly Color PhaseTwoAccent = new Color(0.8f, 1f, 0.3f, 1f);
        private static readonly Color FireAccent = new Color(0.4f, 1f, 0.25f, 1f);

        /// <summary>Puntos de 'Fuego Verde' por plataforma lateral (el colocador de la escena).</summary>
        private const int FireTargetsPerPlatform = 3;

        // ================================================================= menú

        [MenuItem("Tools/RedMagic/Boss/TreeBoss · Crear jefe (arte + ataques + prefab)")]
        public static void Generate() => Debug.Log(Run());

        [MenuItem("Tools/RedMagic/Boss/TreeBoss · Colocar en World1_Boss")]
        public static void PlaceInBossScene() =>
            BossAuthoring.PlaceBossInScene(PrefabPath, BossScenePath, x: 6f, fallbackY: -3f);

        [MenuItem("Tools/RedMagic/Boss/TreeBoss · Diagnosticar láminas")]
        public static void Diagnose() => Debug.Log(DiagnoseAll());

        [MenuItem("Tools/RedMagic/Boss/TreeBoss · Colocar blancos de fuego en World1_Boss")]
        public static void PlaceFireTargetsMenu() => Debug.Log(PlaceFireTargets());

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run()
        {
            var log = new StringBuilder("[TreeBossPack]\n");

            BossAuthoring.EnsureTag("Enemy");
            EnsureFolders();

            var body = BodyRecipe();
            var orb = OrbRecipe();
            var root = RootRecipe();
            AssetDatabase.SaveAssets();

            log.Append(SpritePipeline.RunSheet(body));
            log.Append(SpritePipeline.RunSheet(orb));
            log.Append(SpritePipeline.RunSheet(root));

            var fx = BuildFx(log);
            var attacks = CreateAttacks(fx);
            var definition = CreateDefinition(attacks, fx);
            BuildBossPrefab(definition, log);

            // El aviso (y los demás slots placeholder) salen de la herramienta de FX, igual que en
            // el resto de jefes: una carpeta por jefe con sus prefabs editables. Es idempotente y
            // no toca la bala, que ya existe.
            FxPlaceholderPack.GeneratePrefabs();
            FxPlaceholderPack.AssignToBosses();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine($"Listo: {PrefabPath}. Colócalo con 'Tools ▸ RedMagic ▸ Boss ▸ TreeBoss · " +
                           "Colocar en World1_Boss' o arrastrándolo a cualquier arena.");
            return log.ToString();
        }

        private static void EnsureFolders()
        {
            BossAuthoring.EnsureFolder(ArtFolder);
            BossAuthoring.EnsureFolder(FxArtFolder);
            BossAuthoring.EnsureFolder(BossFolder);
            BossAuthoring.EnsureFolder(PrefabFolder);
        }

        public static string DiagnoseAll()
        {
            EnsureFolders();
            var log = new StringBuilder();
            foreach (var recipe in new[] { BodyRecipe(), OrbRecipe(), RootRecipe() })
                log.AppendLine($"##### {recipe.name}").Append(SheetSlicer.DiagnoseBands(recipe));
            return log.ToString();
        }

        // ================================================================= recetas

        /// <summary>El cuerpo: lámina de 6×6 sobre fondo verde liso, sin rótulos.</summary>
        private static SpriteSheetRecipe BodyRecipe()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SpriteSheetRecipe>(BodyRecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<SpriteSheetRecipe>();
            recipe.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(BodySheet);
            recipe.characterName = "TreeBoss";
            recipe.outputFolder = ArtFolder;
            recipe.sliceMode = SliceMode.AutoBounds;

            // releaseFrame = el dibujo en el que sale el golpe. BossAnimator acelera o frena el clip
            // para que ESE frame caiga al acabar el aviso del ataque.
            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",  frames = 6, fps = 7f,  loop = true },
                new SheetRow { state = Charge,  frames = 6, fps = 10f, loop = false, releaseFrame = 4 },  // brillo al máximo
                new SheetRow { state = Slam,    frames = 6, fps = 10f, loop = false, releaseFrame = 3 },  // raíces abiertas
                new SheetRow { state = Summon,  frames = 6, fps = 10f, loop = false, releaseFrame = 3 },  // brazos extendidos
                new SheetRow { state = "Hurt",  frames = 6, fps = 12f, loop = false },
                new SheetRow { state = "Death", frames = 6, fps = 8f,  loop = false },
            };

            ConfigureKey(recipe, tolerance: 0.12f, softEdge: 10);
            // Las sombras de la corteza tiran al verde del fondo: sin esto el tronco sale perforado.
            recipe.fillHoles = 200;
            recipe.anchor = AnchorMode.BottomCenter;   // plantado: el origen es la base del tronco
            recipe.runtime = AnimRuntime.Animator;     // jefe: no pasa por pool
            recipe.attackEvents = true;                // los recibe BossAnimator

            AssetDatabase.CreateAsset(recipe, BodyRecipePath);
            return recipe;
        }

        /// <summary>Las tres filas de orbe de la lámina de efectos: centradas, van por el aire.</summary>
        private static SpriteSheetRecipe OrbRecipe()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SpriteSheetRecipe>(OrbRecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<SpriteSheetRecipe>();
            recipe.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(FxSheet);
            recipe.characterName = "TreeBossOrb";
            recipe.outputFolder = FxArtFolder;
            recipe.sliceMode = SliceMode.AutoBounds;
            recipe.cropBottom = FxSheetHeight - FxSplitFromTop;

            recipe.rows = new[]
            {
                new SheetRow { state = "Idle",   frames = 6, fps = 10f, loop = true },
                new SheetRow { state = "Move",   frames = 6, fps = 14f, loop = true },
                new SheetRow { state = "Impact", frames = 6, fps = 16f, loop = false },
            };

            ConfigureKey(recipe, tolerance: 0.1f, softEdge: 16);
            recipe.anchor = AnchorMode.Center;
            recipe.runtime = AnimRuntime.Flipbook;   // pooled: sin Animator
            recipe.attackEvents = false;

            AssetDatabase.CreateAsset(recipe, OrbRecipePath);
            return recipe;
        }

        /// <summary>Las tres filas que salen del suelo: pivote abajo, se plantan en el terreno.</summary>
        private static SpriteSheetRecipe RootRecipe()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SpriteSheetRecipe>(RootRecipePath);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<SpriteSheetRecipe>();
            recipe.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(FxSheet);
            recipe.characterName = "TreeBossRoot";
            recipe.outputFolder = FxArtFolder;
            recipe.sliceMode = SliceMode.AutoBounds;
            recipe.cropTop = FxSplitFromTop;

            recipe.rows = new[]
            {
                new SheetRow { state = "Up",   frames = 6, fps = 18f, loop = false },
                new SheetRow { state = "Down", frames = 6, fps = 16f, loop = false },
                // Los anillos de la onda crecen hasta tocarse: columnas iguales en vez de manchas.
                new SheetRow { state = "Wave", frames = 6, fps = 14f, loop = false, evenSplit = true },
            };

            ConfigureKey(recipe, tolerance: 0.1f, softEdge: 16);
            recipe.fillHoles = 80;   // mismas sombras verdosas en las raíces de la púa
            recipe.anchor = AnchorMode.BottomCenter;
            recipe.runtime = AnimRuntime.Flipbook;
            recipe.attackEvents = false;

            AssetDatabase.CreateAsset(recipe, RootRecipePath);
            return recipe;
        }

        /// <summary>Las dos láminas son arte pintado sobre un verde liso, sin alfa: fondo deducido y borde suave.</summary>
        private static void ConfigureKey(SpriteSheetRecipe recipe, float tolerance, int softEdge)
        {
            recipe.keyBackground = true;
            recipe.backgroundTolerance = tolerance;
            recipe.softEdge = softEdge;
            recipe.pixelsPerUnit = 100;
            recipe.filterMode = FilterMode.Bilinear;
            recipe.margin = 8;
        }

        // ================================================================= efectos

        private sealed class Fx
        {
            public GameObject charge, bullet, impact, spike, wave;
            public GameObject fireBolt, fireHazard;   // PLACEHOLDER hasta que llegue el arte del fuego
            public GameObject leaf, leafImpact;       // PLACEHOLDER hasta que llegue el arte de las hojas
        }

        private static Fx BuildFx(StringBuilder log)
        {
            var fx = new Fx
            {
                // Los placeholders no dependen de las láminas: salen siempre.
                fireBolt = FirePlaceholder<BossBolt>(FireBoltPath, sortingOrder: 6, log),
                fireHazard = FirePlaceholder<BossHazard>(FireHazardPath, sortingOrder: 4, log),
            };
            fx.leafImpact = LeafImpactPlaceholder(log);
            fx.leaf = LeafPlaceholder(fx.leafImpact, log);

            var idle = SpritesOf("TreeBossOrb", "Idle");
            var move = SpritesOf("TreeBossOrb", "Move");
            var impact = SpritesOf("TreeBossOrb", "Impact");
            var up = SpritesOf("TreeBossRoot", "Up");
            var down = SpritesOf("TreeBossRoot", "Down");
            var wave = SpritesOf("TreeBossRoot", "Wave");

            if (idle.Length == 0 || move.Length == 0 || impact.Length == 0 ||
                up.Length == 0 || down.Length == 0 || wave.Length == 0)
            {
                // Sin arte, los ataques caen a su versión en código (formas teñidas): se juega igual.
                log.AppendLine("  ERROR: faltan hojas de efectos en " + FxArtFolder + "; los ataques " +
                               "saldrán con el placeholder de código.");
                return fx;
            }

            fx.impact = OneShot(OrbImpactPath, impact, 16f, ImpactWidth, sortingOrder: 8, sink: 0f, log);
            fx.wave = OneShot(RootWavePath, wave, 14f, WaveWidth, sortingOrder: 6, sink: WaveSink, log);

            // Brota, se sostiene arriba un instante (el último dibujo repetido) y se retira.
            var hold = Enumerable.Repeat(up[up.Length - 1], 3);
            var spike = up.Concat(hold).Concat(down).ToArray();
            fx.spike = OneShot(RootSpikePath, spike, 16f, SpikeWidth, sortingOrder: 7, sink: 0f, log);

            fx.charge = ChargeOrb(idle, log);
            fx.bullet = Bullet(move, fx.impact, log);
            return fx;
        }

        /// <summary>
        /// Efecto de un solo uso: VfxOneShot en la raíz y el flipbook en el hijo "Sprite". El hijo
        /// permite hundir el dibujo respecto al punto de spawn (la onda, tumbada, cruza el suelo).
        /// </summary>
        private static GameObject OneShot(string path, Sprite[] frames, float fps, float width,
                                          int sortingOrder, float sink, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return RefreshFrames(path, frames, log);

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            root.transform.localScale = Vector3.one * (width / CellWidth(frames[0]));

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, -CellHeight(frames[0]) * sink, 0f);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = sortingOrder;

            new BossAuthoring.Fields(visual.AddComponent<SpriteFlipbook>())
                .SetObjectArray("frames", frames)
                .Set("framesPerSecond", fps)
                .Set("pingPong", false)
                .Set("randomStart", false)
                .Set("oneShot", true)
                .Apply();

            // VfxOneShot sólo sabe medir clips de Animator: con flipbook la duración se le dice a mano.
            new BossAuthoring.Fields(root.AddComponent<VfxOneShot>())
                .Set("lifetime", frames.Length / fps)
                .Set("extraTime", 0.05f)
                .Apply();

            return SaveAndDiscard(root, path, log);
        }

        /// <summary>Orbe de carga: el que aparece y crece en la corona antes de salir (BossOrb).</summary>
        private static GameObject ChargeOrb(Sprite[] frames, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(OrbChargePath);
            if (existing != null) return RefreshFrames(OrbChargePath, frames, log);

            var go = new GameObject("Fx_TreeBoss_OrbCharge");
            go.transform.localScale = Vector3.one * (ChargeCellWidth / CellWidth(frames[0]));

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 6;

            new BossAuthoring.Fields(go.AddComponent<SpriteFlipbook>())
                .SetObjectArray("frames", frames)
                .Set("framesPerSecond", 10f)
                .Set("pingPong", true)
                .Set("randomStart", true)   // los orbes de una corona no laten al unísono
                .Set("oneShot", false)
                .Apply();

            go.AddComponent<BossOrb>();
            return SaveAndDiscard(go, OrbChargePath, log);
        }

        /// <summary>
        /// La bala: física y Projectile en la raíz, el dibujo en el hijo "Sprite". La hoja de vuelo
        /// lleva la estela, así que su centro no es la bola: el hijo se desplaza hacia atrás para
        /// que la bola quede en el origen, que es donde está el collider y el centro de giro
        /// (Projectile orienta la raíz con transform.right). Sin FxPlaceholderStyle: es arte real y
        /// el spawner sólo la coloca.
        /// </summary>
        private static GameObject Bullet(Sprite[] frames, GameObject impact, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPath);
            if (existing != null) return RefreshFrames(BulletPath, frames, log);

            var sprite = frames[0];
            float ppu = sprite.pixelsPerUnit;
            float margin = 8f / ppu;

            // La bola es lo más alto de la celda y va pegada al borde delantero (+X).
            float ballDiameter = Mathf.Max(0.01f, CellHeight(sprite) - margin * 2f);
            float scale = BulletBall / ballDiameter;
            float ballFromPivot = (CellWidth(sprite) - CellHeight(sprite)) * 0.5f;

            var root = new GameObject("Fx_TreeBoss_Bullet");

            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = BulletBall * 0.4f;   // un pelo menos que la bola: la estela no golpea

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = new Vector3(-ballFromPivot * scale, 0f, 0f);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 6;

            new BossAuthoring.Fields(visual.AddComponent<SpriteFlipbook>())
                .SetObjectArray("frames", frames)
                .Set("framesPerSecond", 14f)
                .Set("pingPong", false)
                .Set("randomStart", true)
                .Set("oneShot", false)
                .Apply();

            var projectile = root.AddComponent<Projectile>();
            if (impact != null) new BossAuthoring.Fields(projectile).SetObject("impactEffect", impact).Apply();

            return SaveAndDiscard(root, BulletPath, log);
        }

        /// <summary>
        /// Relanzar el pack vuelve a cortar las láminas: se reenganchan los frames nuevos en el
        /// flipbook del prefab que ya existe, sin tocar nada más (escala, fps, collider…).
        /// </summary>
        private static GameObject RefreshFrames(string path, Sprite[] frames, StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var flipbook = root.GetComponentInChildren<SpriteFlipbook>(true);
                if (flipbook == null) return AssetDatabase.LoadAssetAtPath<GameObject>(path);

                var so = new SerializedObject(flipbook);
                float fps = so.FindProperty("framesPerSecond").floatValue;
                new BossAuthoring.Fields(flipbook).SetObjectArray("frames", frames).Apply();

                var renderer = flipbook.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.sprite = frames[0];

                var oneShot = root.GetComponent<VfxOneShot>();
                if (oneShot != null)
                    new BossAuthoring.Fields(oneShot).Set("lifetime", frames.Length / Mathf.Max(0.1f, fps)).Apply();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            log.AppendLine($"  frames refrescados: {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ================================================================= ataques

        private sealed class Attacks
        {
            public BossAttack Orbs, OrbStorm, Slam, Spikes, SpikeForest, FireDenial, Leaves, LeafStorm;

            /// <summary>La tormenta de hojas se ha creado en esta pasada (y hay que meterla en la baraja).</summary>
            public bool LeafStormCreated;
        }

        private static Attacks CreateAttacks(Fx fx)
        {
            var a = new Attacks();

            // ---- 1 · Carga y orbes: gesto de carga, corona que crece, y los orbes al jugador de uno
            // en uno. Un leve teledirigido castiga quedarse quieto sin hacerlos imposibles.
            a.Orbs = Attack<OrbRingAttack>("BossAttack_OrbesDeSavia", "Orbes de Savia",
                "Carga el cristal del pecho y lo suelta en orbes que van a por ti de uno en uno. " +
                "Muévete a su ritmo.",
                PhaseOneAccent, f => Orbs(f, fx)
                    .Set("telegraph", 0.9f).Set("recovery", 1.2f).Set("weight", 1.2f)
                    .Set("cooldownInAttacks", 1)
                    .Set("damage", 12f).Set("knockbackMultiplier", 0.6f)
                    .Set("shakeAmplitude", 0.25f).Set("shakeDuration", 0.25f)
                    .Set("orbCount", 5).Set("ringRadius", 2.4f)
                    .Set("spinDegreesPerSecond", 25f)
                    .Set("growSeconds", 0.9f).Set("appearStagger", 0.1f).Set("holdSeconds", 0.25f)
                    .Set("volleys", 1).Set("timeBetweenVolleys", 0.8f)
                    .Set("launchStagger", 0.2f)
                    .Set("projectile.speed", 9f).Set("projectile.homingTurnRate", 25f));

            a.OrbStorm = Attack<OrbRingAttack>("BossAttack_TormentaDeSavia", "Tormenta de Savia",
                "Dos coronas llenas, más rápidas y más tercas. No hay sitio seguro: hay ritmo.",
                PhaseTwoAccent, f => Orbs(f, fx)
                    .Set("telegraph", 0.8f).Set("recovery", 1.1f).Set("weight", 1.3f)
                    .Set("cooldownInAttacks", 1)
                    .Set("damage", 12f).Set("knockbackMultiplier", 0.6f)
                    .Set("shakeAmplitude", 0.35f).Set("shakeDuration", 0.3f)
                    .Set("orbCount", 8).Set("ringRadius", 2.8f)
                    .Set("spinDegreesPerSecond", 45f)
                    .Set("growSeconds", 0.8f).Set("appearStagger", 0.06f).Set("holdSeconds", 0.2f)
                    .Set("volleys", 2).Set("timeBetweenVolleys", 0.6f)
                    .Set("launchStagger", 0.12f)
                    .Set("projectile.speed", 10.5f).Set("projectile.homingTurnRate", 35f));

            // ---- 2 · Golpe de raíces: gesto de golpe; al tocar el suelo la cámara tiembla como en un
            // terremoto y duele a quien PISE el suelo de la arena. Ni radio ni onda que esquivar:
            // salta o súbete a una plataforma en el momento justo. Deja al jefe expuesto.
            a.Slam = Attack<QuakeSlamAttack>("BossAttack_EstallidoDeRaices", "Estallido de Raíces",
                "Hunde las raíces y toda la arena tiembla. Si tienes los pies en el suelo cuando " +
                "golpea, te alcanza: salta o súbete a una plataforma — y vuelve a pegarle mientras " +
                "se recupera.",
                PhaseOneAccent, f => f
                    .Set("gesture", Slam)
                    .Set("impactDelay", 1f).Set("recovery", 1.4f).Set("weight", 1f)
                    .Set("cooldownInAttacks", 1)
                    .Set("damage", 22f).Set("knockbackMultiplier", 1.8f)
                    .Set("vulnerableSeconds", 1.2f).Set("vulnerableMultiplier", 1.8f)
                    .Set("shakeAmplitude", 0.9f).Set("shakeDuration", 0.6f)   // el terremoto
                    .Set("floorTolerance", 0.6f)
                    .Set("warnFloor", true).Set("warnHeight", 0.5f)
                    .SetObject("impactFxPrefab", fx.wave).Set("fxWidth", WaveWidth));

            // ---- 3 · Invocar raíces: gesto de invocar, y púas que brotan donde estás. La marca se
            // fija al empezar y no persigue; cada púa vuelve a buscarte.
            a.Spikes = Attack<GroundSlamAttack>("BossAttack_EspinasDeRaiz", "Espinas de Raíz",
                "Señala y las raíces brotan bajo tus pies, una detrás de otra. Cuando el suelo " +
                "brille, ya no estés ahí.",
                PhaseOneAccent, f => Spikes(f, fx)
                    .Set("telegraph", 0.8f).Set("recovery", 1.1f).Set("weight", 1.1f)
                    .Set("damage", 16f).Set("knockbackMultiplier", 1.2f)
                    .Set("windup", 0.6f).Set("slams", 2).Set("timeBetweenSlams", 0.55f));

            a.SpikeForest = Attack<GroundSlamAttack>("BossAttack_BosqueDeEspinas", "Bosque de Espinas",
                "Cuatro púas seguidas, casi sin aviso entre una y otra. No pares.",
                PhaseTwoAccent, f => Spikes(f, fx)
                    .Set("telegraph", 0.7f).Set("recovery", 1.2f).Set("weight", 1.2f)
                    .Set("damage", 16f).Set("knockbackMultiplier", 1.2f)
                    .Set("windup", 0.5f).Set("slams", 4).Set("timeBetweenSlams", 0.38f));

            // ---- 5 · Hojas mágicas (Rain): el mismo gesto de carga que los orbes y dos oleadas de
            // hojas que caen una tras otra sobre toda la arena, con marca en el suelo antes de cada
            // una. Variante de referencia: ya NO está en la baraja de este jefe (la sustituye la
            // tormenta, abajo); se conserva para reutilizarla en otro. Hoja e impacto son PLACEHOLDER.
            a.Leaves = Attack<BulletHellAttack>("BossAttack_HojasMagicas", "Hojas Mágicas",
                "Carga el cristal y todo el bosque deja caer hojas encantadas, una tras otra. Lee las " +
                "marcas del suelo y cuélate entre ellas.",
                PhaseTwoAccent, f => f
                    .Set("gesture", Charge)
                    .Set("minPhase", 2)
                    .Set("telegraph", 1f).Set("recovery", 1f).Set("weight", 1.1f)
                    .Set("cooldownInAttacks", 1).Set("cooldownSeconds", 6f)
                    .Set("damage", 10f).Set("knockbackMultiplier", 0.4f)
                    .Set("shakeAmplitude", 0.2f).Set("shakeDuration", 0.2f)
                    .Set("pattern", (int)BulletPattern.Rain)
                    .SetObject("projectile.prefab", fx.leaf)
                    .Set("projectile.speed", 7f)        // velocidad de caída
                    .Set("projectile.lifetime", 4f)     // > alto de la arena / velocidad, o se apagan en el aire
                    .Set("projectile.size", new Vector2(0.5f, 0.5f))
                    .Set("volleys", 2).Set("timeBetweenVolleys", 0.9f).Set("bulletsPerVolley", 10)
                    .Set("rainSpan", 1.05f).Set("rainCenterOffset", 0f)   // toda la arena + la plataforma que asoma
                    .Set("rainRandomX", false).Set("rainJitter", 0.6f)
                    .Set("rainDropStagger", 0.08f)
                    .Set("rainMarkerSeconds", 0.5f).Set("rainMarkerSize", new Vector2(1f, 0.35f)));

            // ---- 6 · Tormenta de hojas (Storm; sólo fase 2, carta normal de su baraja — sustituye a
            // las Hojas Mágicas en este jefe): mismo gesto de carga y 5 s de hojas cayendo sin parar,
            // cada una en una X al azar y un poco en diagonal (viento 8° ± 18°). Misma hoja PLACEHOLDER.
            a.LeafStormCreated = AssetDatabase.LoadAssetAtPath<BossAttack>($"{BossFolder}/BossAttack_TormentaDeHojas.asset") == null;
            a.LeafStorm = Attack<BulletHellAttack>("BossAttack_TormentaDeHojas", "Tormenta de Hojas",
                "Carga el cristal y el bosque entero se deshoja sobre ti durante unos segundos. No hay " +
                "filas: muévete entre lo que cae.",
                PhaseTwoAccent, f => f
                    .Set("gesture", Charge)
                    .Set("minPhase", 2)
                    .Set("telegraph", 1f).Set("recovery", 1.2f).Set("weight", 1.1f)
                    .Set("cooldownInAttacks", 1).Set("cooldownSeconds", 8f)
                    .Set("damage", 10f).Set("knockbackMultiplier", 0.4f)
                    .Set("shakeAmplitude", 0.2f).Set("shakeDuration", 0.2f)
                    .Set("pattern", (int)BulletPattern.Storm)
                    .SetObject("projectile.prefab", fx.leaf)
                    .Set("projectile.speed", 6f)         // velocidad de caída
                    .Set("projectile.lifetime", 4.5f)    // cubre la diagonal más larga: 16.5 / cos 26° / 6 ≈ 3.1 s
                    .Set("projectile.size", new Vector2(0.5f, 0.5f))
                    .Set("rainSpan", 1.05f).Set("rainCenterOffset", 0f)
                    .Set("rainMarkerSize", new Vector2(0.8f, 0.25f))
                    .Set("stormSeconds", 5f).Set("stormPerSecond", 7f)
                    .Set("stormAngleRange", 18f).Set("stormWind", 8f)
                    .Set("stormMarkLanding", true));

            // ---- 4 · Fuego verde: apertura de la fase 2 (BossPhase.openingAttack, fuera de la
            // baraja). Un proyectil a cada punto de BossArenaTargets y un fuego que se queda hasta
            // que el jefe muere: niega las plataformas laterales el resto del combate. Proyectil y
            // fuego son PLACEHOLDER (Fx_TreeBoss_FireBolt / _FireHazard).
            a.FireDenial = Attack<PlatformDenialAttack>("BossAttack_FuegoVerde", "Fuego Verde",
                "Escupe fuego verde sobre las plataformas y ya no se apaga. A partir de aquí, " +
                "las alturas son suyas.",
                FireAccent, f => f
                    .Set("gesture", Charge)
                    .Set("telegraph", 1.1f).Set("recovery", 1.2f).Set("weight", 1f)
                    .Set("cooldownInAttacks", 0)
                    .Set("damage", 8f).Set("knockbackMultiplier", 0.3f)
                    .Set("shakeAmplitude", 0.3f).Set("shakeDuration", 0.3f)
                    .SetObject("boltPrefab", fx.fireBolt)
                    .Set("boltSpeed", 12f).Set("boltSize", new Vector2(0.8f, 0.8f))
                    .Set("launchOffset", new Vector2(0f, BodyHeight * 0.62f))   // desde el cristal
                    .Set("launchStagger", 0.12f)
                    .SetObject("firePrefab", fx.fireHazard)
                    .Set("fireSize", new Vector2(2.1f, 1f)).Set("fireTickInterval", 0.5f)
                    .Set("snapToSurface", true).Set("snapDistance", 4f));

            return a;
        }

        /// <summary>Lo común a los dos ataques de orbes: gesto, forma de la corona y arte.</summary>
        private static BossAuthoring.Fields Orbs(BossAuthoring.Fields f, Fx fx) => f
            .Set("gesture", Charge)
            .Set("originOffset", new Vector2(0f, BodyHeight * 0.62f))   // a la altura del cristal
            .Set("alignToPlayer", true)
            .Set("startScale", 0.2f).Set("endScale", 1f)
            .Set("recoilFraction", 0.1f)
            .Set("launchMode", (int)OrbLaunchMode.AtPlayer).Set("launchSpread", 0f)
            .Set("tintOrbWithAccent", false).Set("codeOrbSize", 0.7f)
            .SetObject("orbPrefab", fx.charge)
            .SetObject("projectile.prefab", fx.bullet)
            .Set("projectile.lifetime", 4.5f)
            .Set("projectile.size", new Vector2(BulletBall, BulletBall))
            .Set("projectile.homingRange", 12f);

        /// <summary>Lo común a los dos ataques de púas: gesto, forma de la púa y arte.</summary>
        private static BossAuthoring.Fields Spikes(BossAuthoring.Fields f, Fx fx) => f
            .Set("gesture", Summon)
            .Set("minPhase", 2)   // las púas son de la fase 2: en la 1 nunca salen aunque estén en la baraja
            .Set("cooldownInAttacks", 1)
            .Set("shakeAmplitude", 0.2f).Set("shakeDuration", 0.2f)
            .Set("aimAtPlayer", true).Set("maxReach", 0f)
            .Set("radius", 1.1f).Set("height", 3.2f)
            .Set("rubbleSeconds", 0f)
            .SetObject("impactFxPrefab", fx.spike).Set("fitFxToRadius", true)
            // La púa tarda ~4 frames a 16 fps en llegar arriba: sale antes y el daño cae en su pico.
            .Set("fxLeadSeconds", 0.2f);

        // ================================================================= definición

        private static BossDefinition CreateDefinition(Attacks a, Fx fx)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BossDefinition>(DefinitionPath);
            if (existing != null)
            {
                EnsureOpeningAttack(existing, a.FireDenial);
                if (a.LeafStormCreated) ReplaceInDeck(existing, phaseIndex: 1, replaces: a.Leaves, a.LeafStorm);
                return existing;
            }

            var definition = ScriptableObject.CreateInstance<BossDefinition>();
            AssetDatabase.CreateAsset(definition, DefinitionPath);

            var so = new SerializedObject(definition);
            so.FindProperty("displayName").stringValue = "Ent Cristalino";
            so.FindProperty("title").stringValue = "Corazón del Bosque";
            so.FindProperty("description").stringValue =
                "No se mueve: no le hace falta. Todo el bosque es su brazo, y el cristal de su pecho " +
                "late cada vez más deprisa.";

            var phases = so.FindProperty("phases");
            phases.arraySize = 2;

            BossAuthoring.WritePhase(phases.GetArrayElementAtIndex(0),
                "Savia Tranquila", startsAtHealth: 1f, damageScale: 1f, speedScale: 1f,
                accent: PhaseOneAccent, pause: new Vector2(1.1f, 1.8f),
                transitionSeconds: 0f, frenzyBelow: 0f,
                attacks: new[] { a.Orbs, a.Slam, a.Spikes });

            // Fase 2 al 50%: invulnerable 2.5s mientras se tambalea, la cámara tiembla y la onda de
            // raíces le estalla a los pies. Luego todo un 35% más rápido (avisos, gestos, ondas,
            // recuperaciones), pausas más cortas, más daño y las variantes gordas de orbes y púas.
            var two = phases.GetArrayElementAtIndex(1);
            BossAuthoring.WritePhase(two,
                "Savia Desbocada", startsAtHealth: 0.5f, damageScale: 1.2f, speedScale: 1.35f,
                accent: PhaseTwoAccent, pause: new Vector2(0.5f, 0.9f),
                transitionSeconds: 2.5f, frenzyBelow: 0.2f,
                attacks: new[] { a.OrbStorm, a.Slam, a.SpikeForest, a.LeafStorm });

            two.FindPropertyRelative("transitionShake").floatValue = 0.9f;
            two.FindPropertyRelative("transitionFx").objectReferenceValue = fx.wave;
            // Tras la transición, una sola vez: el fuego verde que niega las plataformas.
            two.FindPropertyRelative("openingAttack").objectReferenceValue = a.FireDenial;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // ================================================================= prefab del jefe

        private static void BuildBossPrefab(BossDefinition definition, StringBuilder log)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var idle = SpritesOf("TreeBoss", "Idle", ArtFolder);

            if (controller == null || idle.Length == 0)
            {
                log.AppendLine("  ERROR: no hay controller o sprites del cuerpo en " + ArtFolder +
                               "; el prefab del jefe no se crea.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                RefreshBossArt(controller, idle[0], log);
                return;
            }

            var root = new GameObject("Boss_TreeBoss");
            BossAuthoring.TrySetTag(root, "Enemy");

            // ---- visual: el hijo "Sprite", que es la ruta que animan los clips.
            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * (BodyHeight / CellHeight(idle[0]));

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = idle[0];
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 5;

            // El pivote de la hoja está a los pies (AnchorMode.BottomCenter): el origen ya es la base.
            var bounds = renderer.bounds;

            // ---- animación: el Animator en la RAÍZ, o los eventos de suelta no llegarían a BossAnimator.
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;

            // ---- física: inamovible, con contactos para el daño por roce.
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.freezeRotation = true;

            // Caja del tronco: no llega a la copa ni a las raíces abiertas, para que estar bajo las
            // ramas no cuente como tocar al jefe.
            var collider = root.AddComponent<BoxCollider2D>();
            float width = Mathf.Max(1f, bounds.size.x * 0.36f);
            float height = Mathf.Max(1f, bounds.size.y * 0.78f);
            collider.size = new Vector2(width, height);
            collider.offset = new Vector2(0f, height * 0.5f);

            // ---- combate (lo mismo que cualquier jefe)
            new BossAuthoring.Fields(root.AddComponent<Health>())
                .Set("maxHealth", MaxHealth)
                .Set("currentHealth", MaxHealth)
                .Set("invulnerabilityDuration", 0f)   // sin i-frames: se comerían las armas multigolpe
                .Apply();

            new BossAuthoring.Fields(root.AddComponent<Knockback>()).Set("immune", true).Set("resistance", 1f).Apply();
            root.AddComponent<HitFlash>();

            // Con animación de muerte, el tinte oscuro del cadáver sobra: se deja que el clip se
            // vea (6 frames a 8 fps) y luego se desvanece.
            new BossAuthoring.Fields(root.AddComponent<Corpse>())
                .Set("linger", 1.4f).Set("fadeDuration", 1.2f).Set("tintOnDeath", false)
                .Apply();

            new BossAuthoring.Fields(root.AddComponent<CurrencyDropper>()).Set("tier", (int)EnemyTier.Boss).Apply();

            root.AddComponent<BossAnimator>();
            root.AddComponent<BossArenaTargets>();   // los puntos de 'Fuego Verde' se rellenan en la escena

            new BossAuthoring.Fields(root.AddComponent<BossController>())
                .SetObject("definition", definition)
                .Set("arenaHalfWidth", 15f)
                .Set("arenaHeight", 13f)
                .Set("activationRadius", 18f)
                .Set("faceTarget", true)
                .Set("introSeconds", 2.2f)
                // Vacío como en el resto de jefes: 'Music_Boss' no está dado de alta en el AudioManager.
                .Set("musicId", string.Empty)
                .Set("introShake", 0.6f)
                .Set("contactDamage", 12f)
                .Set("contactDamageCooldown", 0.8f)
                .Set("contactKnockbackMultiplier", 1.4f)
                .Set("swayDegrees", 0f)   // animado: el balanceo lo pone la lámina
                .Apply();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            log.AppendLine("  prefab creado: " + PrefabPath);
        }

        /// <summary>Re-engancha el controller y el primer frame en un prefab que ya existe.</summary>
        private static void RefreshBossArt(AnimatorController controller, Sprite idle, StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var animator = root.GetComponent<Animator>();
                if (animator != null) animator.runtimeAnimatorController = controller;

                var renderer = root.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null) renderer.sprite = idle;

                if (root.GetComponent<BossArenaTargets>() == null) root.AddComponent<BossArenaTargets>();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            log.AppendLine("  arte del prefab refrescado: " + PrefabPath);
        }

        // ================================================================= fuego verde (placeholder)

        /// <summary>
        /// Prefab PLACEHOLDER del fuego: cuadrado blanco + el componente de runtime +
        /// <see cref="FxPlaceholderStyle"/> (el ataque lo tiñe con su accent y lo escala). Si ya
        /// existe no se toca: ahí es donde entra el arte final.
        /// </summary>
        private static GameObject FirePlaceholder<T>(string path, int sortingOrder, StringBuilder log)
            where T : Component
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var square = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSquare);
            if (square == null)
                log.AppendLine($"  AVISO: falta {PlaceholderSquare} ('FX ▸ 1 · Generar prefabs " +
                               "placeholder'); el placeholder del fuego sale sin sprite.");

            var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = sortingOrder;

            go.AddComponent<T>();
            go.AddComponent<FxPlaceholderStyle>();
            return SaveAndDiscard(go, path, log);
        }

        /// <summary>
        /// La fase 2 abre con 'Fuego Verde'. En una definición que ya existía sólo se rellena si el
        /// hueco está vacío: lo que se haya puesto a mano manda.
        /// </summary>
        private static void EnsureOpeningAttack(BossDefinition definition, BossAttack opening)
        {
            var so = new SerializedObject(definition);
            var phases = so.FindProperty("phases");
            if (phases == null || phases.arraySize < 2) return;

            var slot = phases.GetArrayElementAtIndex(1).FindPropertyRelative("openingAttack");
            if (slot == null || slot.objectReferenceValue != null) return;

            slot.objectReferenceValue = opening;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        /// <summary>
        /// Deja en World1_Boss los puntos de 'Fuego Verde': <see cref="FireTargetsPerPlatform"/>
        /// repartidos sobre cada plataforma lateral (las de capa Platform más a la izquierda y más a
        /// la derecha) bajo un "FireTargets", y los engancha en el <see cref="BossArenaTargets"/> del
        /// jefe. Es un punto de partida para retocar a mano; si el jefe ya tiene puntos, no toca nada.
        /// </summary>
        public static string PlaceFireTargets()
        {
            var loaded = SceneManager.GetSceneByPath(BossScenePath);
            bool wasOpen = loaded.IsValid() && loaded.isLoaded;
            var scene = wasOpen ? loaded : EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Additive);
            if (!scene.IsValid()) return "[TreeBossPack] No se pudo abrir " + BossScenePath;

            try
            {
                var boss = FindTreeBoss(scene);
                if (boss == null) return $"[TreeBossPack] {BossScenePath} no tiene Boss_TreeBoss; colócalo primero.";

                var holder = boss.GetComponent<BossArenaTargets>();
                if (holder == null) holder = boss.gameObject.AddComponent<BossArenaTargets>();

                if (holder.Targets != null && holder.Targets.Any(t => t != null))
                    return "[TreeBossPack] El jefe ya tiene puntos de fuego; no se toca nada.";

                var points = SidePlatformPoints(scene, boss);
                var root = new GameObject("FireTargets");
                SceneManager.MoveGameObjectToScene(root, scene);

                var targets = new Transform[points.Count];
                for (int i = 0; i < points.Count; i++)
                {
                    var point = new GameObject($"FireTarget_{i + 1}");
                    point.transform.SetParent(root.transform, false);
                    point.transform.position = points[i];
                    targets[i] = point.transform;
                }

                new BossAuthoring.Fields(holder).SetObjectArray("targets", targets).Apply();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return $"[TreeBossPack] {targets.Length} puntos de fuego en {BossScenePath} (FireTargets). " +
                       "Muévelos a mano si hace falta.";
            }
            finally
            {
                if (!wasOpen) EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static BossController FindTreeBoss(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var boss in root.GetComponentsInChildren<BossController>(true))
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(boss.gameObject) == PrefabPath)
                    return boss;

            return null;
        }

        /// <summary>Puntos sobre las dos plataformas laterales; sin ellas, repartidos por la arena.</summary>
        private static List<Vector3> SidePlatformPoints(Scene scene, BossController boss)
        {
            Physics2D.SyncTransforms();

            int platformLayer = LayerMask.NameToLayer("Platform");
            var platforms = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Collider2D>(true))
                .Where(c => c.gameObject.layer == platformLayer && !c.isTrigger)
                .OrderBy(c => c.bounds.center.x)
                .ToList();

            var points = new List<Vector3>();

            if (platforms.Count >= 2)
            {
                foreach (var platform in new[] { platforms[0], platforms[platforms.Count - 1] })
                {
                    var bounds = platform.bounds;
                    for (int k = 0; k < FireTargetsPerPlatform; k++)
                    {
                        float x = bounds.min.x + bounds.size.x * (k + 0.5f) / FireTargetsPerPlatform;
                        points.Add(new Vector3(x, bounds.max.y + 0.6f, 0f));
                    }
                }

                return points;
            }

            int count = FireTargetsPerPlatform * 2;
            var origin = boss.transform.position;
            for (int k = 0; k < count; k++)
                points.Add(new Vector3(origin.x + Mathf.Lerp(-12f, 12f, k / (count - 1f)), origin.y + 3f, 0f));

            return points;
        }

        // ================================================================= hojas mágicas (placeholder)

        /// <summary>
        /// Hoja PLACEHOLDER: montaje de bala de jefe (Rigidbody2D + trigger + Projectile) con el
        /// cuadrado y FxPlaceholderStyle (se tiñe con el color de la fase y se escala con
        /// projectile.size). Al terminar suelta <paramref name="impact"/>. Si ya existe no se toca.
        /// Arte final: sprite dibujado mirando a +X (Projectile gira hacia donde vuela, aquí abajo).
        /// </summary>
        private static GameObject LeafPlaceholder(GameObject impact, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LeafPath);
            if (existing != null) return existing;

            var go = new GameObject("Fx_TreeBoss_Leaf");

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderSprite(log);
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 6;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5f;   // FxPlaceholderStyle lo reajusta al tamaño de cada disparo

            var projectile = go.AddComponent<Projectile>();
            if (impact != null) new BossAuthoring.Fields(projectile).SetObject("impactEffect", impact).Apply();

            go.AddComponent<FxPlaceholderStyle>();
            return SaveAndDiscard(go, LeafPath, log);
        }

        /// <summary>Impacto PLACEHOLDER de la hoja: el cuadrado aplastado contra el suelo, 0.25s.</summary>
        private static GameObject LeafImpactPlaceholder(StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LeafImpactPath);
            if (existing != null) return existing;

            var go = new GameObject("Fx_TreeBoss_LeafImpact");
            go.transform.localScale = new Vector3(0.7f, 0.25f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderSprite(log);
            renderer.color = LeafPlaceholderColor;   // VfxOneShot no tiñe: el color va en el prefab
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 6;

            new BossAuthoring.Fields(go.AddComponent<VfxOneShot>()).Set("lifetime", 0.25f).Apply();
            return SaveAndDiscard(go, LeafImpactPath, log);
        }

        private static Sprite PlaceholderSprite(StringBuilder log)
        {
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSquare);
            if (square == null)
                log.AppendLine($"  AVISO: falta {PlaceholderSquare} ('FX ▸ 1 · Generar prefabs placeholder').");
            return square;
        }

        /// <summary>
        /// Pone <paramref name="attack"/> en la baraja de una fase: en el hueco de
        /// <paramref name="replaces"/> si está, o al final si no. Si ya está, no hace nada.
        /// </summary>
        private static void ReplaceInDeck(BossDefinition definition, int phaseIndex, BossAttack replaces,
                                          BossAttack attack)
        {
            var so = new SerializedObject(definition);
            var phases = so.FindProperty("phases");
            if (attack == null || phases == null || phases.arraySize <= phaseIndex) return;

            var deck = phases.GetArrayElementAtIndex(phaseIndex).FindPropertyRelative("attacks");
            int slot = -1;
            for (int i = 0; i < deck.arraySize; i++)
            {
                var current = deck.GetArrayElementAtIndex(i).objectReferenceValue;
                if (current == attack) return;
                if (replaces != null && current == replaces) slot = i;
            }

            if (slot < 0)
            {
                deck.arraySize++;
                slot = deck.arraySize - 1;
            }

            deck.GetArrayElementAtIndex(slot).objectReferenceValue = attack;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        // ================================================================= util

        /// <summary>Los sprites de <c>&lt;folder&gt;/&lt;character&gt;_&lt;state&gt;.png</c>, en orden.</summary>
        private static Sprite[] SpritesOf(string character, string state, string folder = FxArtFolder)
        {
            string path = $"{folder}/{character}_{state}.png";
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s =>
                {
                    int at = s.name.LastIndexOf('_');
                    return at >= 0 && int.TryParse(s.name.Substring(at + 1), out int n) ? n : 0;
                })
                .ToArray();
        }

        /// <summary>Tamaño en mundo de la celda (común a todos los frames de una hoja cortada).</summary>
        private static float CellWidth(Sprite sprite) => sprite.rect.width / sprite.pixelsPerUnit;
        private static float CellHeight(Sprite sprite) => sprite.rect.height / sprite.pixelsPerUnit;

        private static T Attack<T>(string fileName, string displayName, string description, Color accent,
                                   System.Func<BossAuthoring.Fields, BossAuthoring.Fields> configure)
            where T : BossAttack =>
            BossAuthoring.Attack<T>(BossFolder, fileName, displayName, description, accent, configure);

        private static GameObject SaveAndDiscard(GameObject go, string path, StringBuilder log)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            log.AppendLine("  prefab creado: " + path);
            return prefab;
        }
    }
}
