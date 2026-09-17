using System;
using System.Collections.Generic;
using System.IO;
using RedMagic.Bosses;
using RedMagic.Fx;
using RedMagic.Items;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.FxTools
{
    /// <summary>
    /// Genera los <b>prefabs placeholder</b> de los visuales que el juego spawnea en masa
    /// (proyectiles de arma, balas de jefe, avisos, ondas…) y los engancha en los assets que los
    /// usan. Idempotente: nunca pisa un prefab, un sprite ni un campo ya asignado, así que se puede
    /// relanzar tras haber metido arte a mano.
    ///
    /// El objetivo es que meter arte de verdad sea: abrir el prefab, borrar la forma geométrica,
    /// soltar el sprite (o añadirle estela / partículas / Animator). Cero código. Si el prefab
    /// conserva su <see cref="FxPlaceholderStyle"/>, el spawner le sigue aplicando tinte y tamaño;
    /// si se lo quitas, se respeta tal cual.
    ///
    /// <b>Los FX de jefe van en una carpeta por jefe</b>: <c>Assets/Prefabs/Fx/Bosses/&lt;Jefe&gt;/</c>
    /// contiene TODOS los visuales de ese jefe (aviso, onda, filo, hazard, plataforma, ancla, bala),
    /// para que rehacer el aspecto de un combate entero sea abrir una sola carpeta. Cada jefe tiene
    /// su propia copia, así su arte puede divergir del de los demás.
    /// </summary>
    public static class FxPlaceholderPack
    {
        private const string ShapeFolder = "Assets/Art/Placeholder";
        private const string PrefabFolder = "Assets/Prefabs/Fx";
        private const string BossFxFolder = "Assets/Prefabs/Fx/Bosses";
        private const string WeaponFolder = "Assets/Resources/Items/Weapons";
        private const string BossPrefabFolder = "Assets/Prefabs/Enemies";

        // Prefabs placeholder compartidos de la organización anterior (uno por tipo, reusado por
        // todos los jefes). El migrador los borra tras repartir una copia por jefe.
        private static readonly string[] LegacySharedBossPrefabs =
        {
            "Assets/Prefabs/Fx/Bosses/Fx_Boss_Warn.prefab",
            "Assets/Prefabs/Fx/Bosses/Fx_Boss_Shockwave.prefab",
            "Assets/Prefabs/Fx/Bosses/Fx_Boss_SweepBeam.prefab",
            "Assets/Prefabs/Fx/Bosses/Fx_Boss_Hazard.prefab",
            "Assets/Prefabs/Fx/Bosses/Fx_Boss_Platform.prefab",
            "Assets/Prefabs/Fx/Bosses/Fx_Boss_Anchor.prefab",
            "Assets/Prefabs/Fx/Fx_Boss_Bullet.prefab",
        };

        /// <summary>Un slot de <see cref="BossController"/> y el sufijo del prefab que lo llena.</summary>
        private static readonly (string field, string suffix)[] BossSlots =
        {
            ("warnPrefab",      "Warn"),
            ("shockwavePrefab", "Shockwave"),
            ("sweepBeamPrefab", "SweepBeam"),
            ("hazardPrefab",    "Hazard"),
            ("platformPrefab",  "Platform"),
            ("anchorPrefab",    "Anchor"),
        };

        // ================================================================= menú

        [MenuItem("Tools/RedMagic/FX/1 · Generar prefabs placeholder")]
        public static void GeneratePrefabs()
        {
            EnsureFolder(ShapeFolder);
            EnsureFolder(PrefabFolder);

            var disc = ShapeSprite("Disc", (x, y) => x * x + y * y <= 1f);
            var square = LoadShape("Square") ?? ShapeSprite("Square", (x, y) => true);
            var diamond = ShapeSprite("Diamond", (x, y) => Mathf.Abs(x) + Mathf.Abs(y) <= 1f);
            var sliver = ShapeSprite("Sliver", (x, y) => Mathf.Abs(x) + Mathf.Abs(y) / 0.30f <= 1f);

            AssetDatabase.Refresh();

            ShotProjectilePrefab("Fx_Shot_Disc", disc);
            ShotProjectilePrefab("Fx_Shot_Square", square);
            ShotProjectilePrefab("Fx_Shot_Diamond", diamond);
            ShotProjectilePrefab("Fx_Shot_Sliver", sliver);
            ShotBeamPrefab("Fx_Shot_Beam", square);

            EnsureFolder(BossFxFolder);
            foreach (var boss in DiscoverBosses())
                BuildBossFxSet(boss.shortName, square, disc);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FxPlaceholderPack] Prefabs placeholder listos en " + PrefabFolder + ".");
        }

        [MenuItem("Tools/RedMagic/FX/2 · Asignar a armas")]
        public static void AssignToWeapons()
        {
            // Nombre de arma -> (campo, prefab). Contenido fijo del juego.
            var map = new (string weapon, string field, string prefab)[]
            {
                ("Weapon_ProyectilRecto",   "projectilePrefab", "Fx_Shot_Disc"),
                ("Weapon_BolaDeFuego",      "projectilePrefab", "Fx_Shot_Disc"),
                ("Weapon_RafagaArcana",     "projectilePrefab", "Fx_Shot_Disc"),
                ("Weapon_EscopetaEspectral","projectilePrefab", "Fx_Shot_Square"),
                ("Weapon_GranadaRunica",    "projectilePrefab", "Fx_Shot_Diamond"),
                ("Weapon_LanzaAstral",      "projectilePrefab", "Fx_Shot_Sliver"),
                ("Weapon_RayoArcano",       "beamPrefab",       "Fx_Shot_Beam"),
            };

            int wired = 0;
            foreach (var (weaponName, field, prefabName) in map)
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponFolder}/{weaponName}.asset");
                var prefab = LoadPrefab(prefabName);
                if (weapon == null || prefab == null)
                {
                    Debug.LogWarning($"[FxPlaceholderPack] Falta {weaponName} o {prefabName}; se salta.");
                    continue;
                }

                var so = new SerializedObject(weapon);
                var prop = so.FindProperty($"baseShot.{field}");
                if (prop == null) { Debug.LogWarning($"[FxPlaceholderPack] Sin campo baseShot.{field} en {weaponName}."); continue; }
                if (prop.objectReferenceValue != null) continue;   // ya asignado a mano

                prop.objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
                wired++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FxPlaceholderPack] Armas enganchadas: {wired}.");
        }

        [MenuItem("Tools/RedMagic/FX/3 · Asignar a jefes")]
        public static void AssignToBosses() => WireBosses(overwrite: false);

        [MenuItem("Tools/RedMagic/FX/4 · Migrar FX de jefe a carpeta por jefe")]
        public static void MigrateBossFxPerBoss()
        {
            EnsureFolder(BossFxFolder);

            var square = LoadShape("Square") ?? ShapeSprite("Square", (x, y) => true);
            var disc = LoadShape("Disc") ?? ShapeSprite("Disc", (x, y) => x * x + y * y <= 1f);

            int sets = 0;
            foreach (var boss in DiscoverBosses())
            {
                BuildBossFxSet(boss.shortName, square, disc);
                sets++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Re-apunta TODO (también los slots ya rellenos con los prefabs compartidos).
            WireBosses(overwrite: true);

            // Con cada jefe apuntando ya a su copia, los compartidos sobran.
            int deleted = 0;
            foreach (var path in LegacySharedBossPrefabs)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null && AssetDatabase.DeleteAsset(path))
                    deleted++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FxPlaceholderPack] Migración: {sets} set(s) por jefe, {deleted} prefab(s) compartido(s) borrado(s).");
        }

        // ================================================================= jefes

        private struct BossEntry
        {
            public string shortName;              // "ArbolAncestral"
            public BossController controller;
            public BossDefinition definition;
        }

        /// <summary>Cada prefab de <c>Assets/Prefabs/Enemies</c> que lleva un <see cref="BossController"/>.</summary>
        private static IEnumerable<BossEntry> DiscoverBosses()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { BossPrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var controller = root != null ? root.GetComponentInChildren<BossController>(true) : null;
                if (controller == null) continue;

                string n = root.name;
                if (n.StartsWith("Boss_")) n = n.Substring("Boss_".Length);

                yield return new BossEntry { shortName = n, controller = controller, definition = controller.Definition };
            }
        }

        private static string BossFolder(string shortName) => $"{BossFxFolder}/{shortName}";

        /// <summary>Ruta relativa a <see cref="PrefabFolder"/> del prefab &lt;suffix&gt; de un jefe.</summary>
        private static string BossPrefabRel(string shortName, string suffix) =>
            $"Bosses/{shortName}/Fx_{shortName}_{suffix}";

        /// <summary>Crea (si faltan) los 7 visuales placeholder de un jefe en su carpeta.</summary>
        private static void BuildBossFxSet(string shortName, Sprite square, Sprite disc)
        {
            EnsureFolder(BossFolder(shortName));

            BossWarnPrefab(BossPrefabRel(shortName, "Warn"), square);
            BossFxPrefab<BossShockwave>(BossPrefabRel(shortName, "Shockwave"), square, null, true);
            BossFxPrefab<BossSweepBeam>(BossPrefabRel(shortName, "SweepBeam"), square, null, true);
            BossFxPrefab<BossHazard>(BossPrefabRel(shortName, "Hazard"), square, null, true);
            BossFxPrefab<BossPlatform>(BossPrefabRel(shortName, "Platform"), square, PlatformExtras, false);
            BossFxPrefab<BossAnchor>(BossPrefabRel(shortName, "Anchor"), square, AnchorExtras, false);
            BossBulletPrefab(BossPrefabRel(shortName, "Bullet"), disc);
        }

        /// <summary>
        /// Engancha cada jefe a los visuales de <i>su</i> carpeta: los seis slots del
        /// <see cref="BossController"/> y el prefab de bala de cada ataque bullet-hell de sus barajas.
        /// <paramref name="overwrite"/> = false rellena sólo lo que esté vacío (idempotente); true
        /// re-apunta todo (migración desde los prefabs compartidos).
        /// </summary>
        private static void WireBosses(bool overwrite)
        {
            int controllers = 0, bullets = 0;

            foreach (var boss in DiscoverBosses())
            {
                var so = new SerializedObject(boss.controller);
                bool touched = false;
                foreach (var (field, suffix) in BossSlots)
                {
                    var prop = so.FindProperty(field);
                    if (prop == null) continue;
                    if (prop.objectReferenceValue != null && !overwrite) continue;

                    var prefab = LoadPrefab(BossPrefabRel(boss.shortName, suffix));
                    if (prefab == null) { Debug.LogWarning($"[FxPlaceholderPack] Falta {BossPrefabRel(boss.shortName, suffix)}."); continue; }
                    if (prop.objectReferenceValue == prefab) continue;

                    prop.objectReferenceValue = prefab;
                    touched = true;
                }
                if (touched)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(boss.controller);
                    controllers++;
                }

                var bullet = LoadPrefab(BossPrefabRel(boss.shortName, "Bullet"));
                foreach (var attack in EnumerateAttacks(boss.definition))
                {
                    if (!(attack is BulletHellAttack)) continue;

                    var aso = new SerializedObject(attack);
                    var prop = aso.FindProperty("projectile.prefab");
                    if (prop == null) continue;
                    if (prop.objectReferenceValue != null && !overwrite) continue;
                    if (prop.objectReferenceValue == bullet) continue;

                    prop.objectReferenceValue = bullet;
                    aso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(attack);
                    bullets++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FxPlaceholderPack] Jefes: {controllers} BossController y {bullets} ataque(s) bullet-hell enganchados.");
        }

        private static IEnumerable<BossAttack> EnumerateAttacks(BossDefinition def)
        {
            if (def == null) yield break;
            var phases = def.Phases;
            if (phases == null) yield break;

            var seen = new HashSet<BossAttack>();
            foreach (var phase in phases)
            {
                if (phase?.attacks == null) continue;
                foreach (var attack in phase.attacks)
                    if (attack != null && seen.Add(attack)) yield return attack;
            }
        }

        // ================================================================= sprites

        private const int TexSize = 64;

        /// <summary>
        /// Crea (si falta) un PNG blanco de <see cref="TexSize"/>² con la forma que define
        /// <paramref name="inside"/> (coords normalizadas -1..1 desde el centro), importado como
        /// Sprite a 64 px/unidad y filtro Point. Devuelve el Sprite.
        /// </summary>
        private static Sprite ShapeSprite(string name, Func<float, float, bool> inside)
        {
            string assetPath = $"{ShapeFolder}/{name}.png";
            var existing = LoadShape(name);
            if (existing != null) return existing;

            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            for (int py = 0; py < TexSize; py++)
            for (int px = 0; px < TexSize; px++)
            {
                // 2×2 supersample para un borde menos dentado.
                int hits = 0;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    float nx = ((px + 0.25f + sx * 0.5f) / TexSize) * 2f - 1f;
                    float ny = ((py + 0.25f + sy * 0.5f) / TexSize) * 2f - 1f;
                    if (inside(nx, ny)) hits++;
                }
                float a = hits / 4f;
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();

            string sysPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            Directory.CreateDirectory(Path.GetDirectoryName(sysPath));
            File.WriteAllBytes(sysPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TexSize;   // 64 px = 1 unidad
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return LoadShape(name);
        }

        private static Sprite LoadShape(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ShapeFolder}/{name}.png");

        // ================================================================= prefabs

        private static void ShotProjectilePrefab(string name, Sprite sprite)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = new GameObject(name);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 6;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5f;   // sprite 64px @ 64ppu = 1 unidad; el estilo lo reajusta al lanzar

            go.AddComponent<RedMagic.Items.ShotProjectile>();
            go.AddComponent<FxPlaceholderStyle>();

            SaveAndDiscard(go, path);
        }

        /// <summary>Bala de jefe: mismo montaje que <c>ProjectileFactory.BuildCodeProjectile</c>.</summary>
        private static void BossBulletPrefab(string name, Sprite sprite)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = new GameObject(Path.GetFileName(name));

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 6;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5f;

            go.AddComponent<RedMagic.Gameplay.Projectile>();
            go.AddComponent<FxPlaceholderStyle>();

            SaveAndDiscard(go, path);
        }

        private static void ShotBeamPrefab(string name, Sprite sprite)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = new GameObject(name);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 6;

            go.AddComponent<RedMagic.Items.ShotBeam>();

            // El haz se estira en X cada frame para casar con el raycast; su tamaño no lo fija el
            // estilo, así que resize=false.
            var style = go.AddComponent<FxPlaceholderStyle>();
            var so = new SerializedObject(style);
            so.FindProperty("resize").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAndDiscard(go, path);
        }

        // ---------------------------------------------------------------- jefes

        private static void BossWarnPrefab(string name, Sprite sprite)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = new GameObject(Path.GetFileName(name));
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 4;   // por delante del suelo, por detrás de personajes

            go.AddComponent<FxPlaceholderStyle>();
            go.AddComponent<RedMagic.Fx.FxTelegraph>();

            SaveAndDiscard(go, path);
        }

        /// <summary>
        /// Prefab placeholder de un visual pooled de jefe (onda, guadaña, hazard, plataforma,
        /// ancla): SpriteRenderer + el componente de runtime + <see cref="FxPlaceholderStyle"/>.
        /// </summary>
        private static void BossFxPrefab<T>(string name, Sprite sprite, Action<GameObject> extras,
                                            bool styleScalesCollider) where T : Component
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var go = new GameObject(Path.GetFileName(name));
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 4;

            extras?.Invoke(go);

            go.AddComponent<T>();

            var style = go.AddComponent<FxPlaceholderStyle>();
            if (!styleScalesCollider)
            {
                var so = new SerializedObject(style);
                so.FindProperty("scaleColliderToSprite").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            SaveAndDiscard(go, path);
        }

        /// <summary>Plataforma: collider sólido en la capa Ground (el runtime lo dimensiona y activa).</summary>
        private static void PlatformExtras(GameObject go)
        {
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0) go.layer = ground;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = Vector2.one;
        }

        /// <summary>Ancla: trigger + Health sin i-frames (rompible por cualquier golpe) + HitFlash.</summary>
        private static void AnchorExtras(GameObject go)
        {
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;

            var health = go.AddComponent<RedMagic.Combat.Health>();
            var so = new SerializedObject(health);
            var iframes = so.FindProperty("invulnerabilityDuration");
            if (iframes != null) iframes.floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<RedMagic.Combat.HitFlash>();
        }

        private static void SaveAndDiscard(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"[FxPlaceholderPack] Prefab creado: {path}");
        }

        private static GameObject LoadPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");

        // ================================================================= util

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
    }
}
