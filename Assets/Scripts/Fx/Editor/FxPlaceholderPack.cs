using System;
using System.IO;
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
    /// </summary>
    public static class FxPlaceholderPack
    {
        private const string ShapeFolder = "Assets/Art/Placeholder";
        private const string PrefabFolder = "Assets/Prefab/Fx";
        private const string WeaponFolder = "Assets/Resources/Items/Weapons";

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

            BossBulletPrefab("Fx_Boss_Bullet", disc);

            EnsureFolder(PrefabFolder + "/Bosses");
            BossWarnPrefab("Bosses/Fx_Boss_Warn", square);
            BossFxPrefab<RedMagic.Bosses.BossShockwave>("Bosses/Fx_Boss_Shockwave", square, null, true);
            BossFxPrefab<RedMagic.Bosses.BossSweepBeam>("Bosses/Fx_Boss_SweepBeam", square, null, true);
            BossFxPrefab<RedMagic.Bosses.BossHazard>("Bosses/Fx_Boss_Hazard", square, null, true);
            BossFxPrefab<RedMagic.Bosses.BossPlatform>("Bosses/Fx_Boss_Platform", square, PlatformExtras, false);
            BossFxPrefab<RedMagic.Bosses.BossAnchor>("Bosses/Fx_Boss_Anchor", square, AnchorExtras, false);

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
        public static void AssignToBosses()
        {
            int bullets = 0, controllers = 0;

            // --- balas de los ataques bullet-hell
            var bullet = LoadPrefab("Fx_Boss_Bullet");
            foreach (var guid in AssetDatabase.FindAssets("t:BulletHellAttack", new[] { "Assets/Resources/Bosses" }))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;

                var so = new SerializedObject(asset);
                var prop = so.FindProperty("projectile.prefab");
                if (prop == null || prop.objectReferenceValue != null) continue;

                prop.objectReferenceValue = bullet;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                bullets++;
            }

            // --- prefabs placeholder en cada BossController
            var slots = new (string field, string prefab)[]
            {
                ("warnPrefab",      "Bosses/Fx_Boss_Warn"),
                ("shockwavePrefab", "Bosses/Fx_Boss_Shockwave"),
                ("sweepBeamPrefab", "Bosses/Fx_Boss_SweepBeam"),
                ("hazardPrefab",    "Bosses/Fx_Boss_Hazard"),
                ("platformPrefab",  "Bosses/Fx_Boss_Platform"),
                ("anchorPrefab",    "Bosses/Fx_Boss_Anchor"),
            };

            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Prefab/Enemies" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var controller = root != null ? root.GetComponentInChildren<RedMagic.Bosses.BossController>(true) : null;
                if (controller == null) continue;

                var so = new SerializedObject(controller);
                bool touched = false;
                foreach (var (field, prefabName) in slots)
                {
                    var prop = so.FindProperty(field);
                    if (prop == null || prop.objectReferenceValue != null) continue;
                    prop.objectReferenceValue = LoadPrefab(prefabName);
                    touched = true;
                }
                if (!touched) continue;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
                controllers++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FxPlaceholderPack] Jefes: {bullets} ataque(s) bullet-hell y {controllers} BossController enganchados.");
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
