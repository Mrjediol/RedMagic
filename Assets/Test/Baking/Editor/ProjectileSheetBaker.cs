#pragma warning disable CS0618   // TextureImporter.spritesheet: el recorte por rectángulos más simple para una hoja generada
using System.Collections.Generic;
using System.IO;
using RedMagic.Bosses;
using RedMagic.Levels;
using UnityEditor;
using UnityEngine;

namespace RedMagic.BakingTest
{
    /// <summary>
    /// PRUEBA DE BAKING de proyectiles de enemigo (IceCannonOrb, CinderKettleBot). Todo en <c>Assets/Test/Baking/Proyectiles</c>.
    ///
    /// Qué se encontró: estos proyectiles NO se calculan en vivo — son animaciones de sprites (<c>SpriteStateMachine</c>: 8
    /// fotogramas de movimiento + 7 de impacto, 288×288), cada fotograma un PNG suelto. Ya son un "flipbook", sin empaquetar.
    /// La prueba: empaquetar todos los fotogramas en UNA hoja (idéntica pixel a pixel, así que no hay "overbaked") y
    /// medir si ayuda: una textura en vez de 15 (menos cambios de textura / lotes al haber muchos proyectiles).
    ///
    /// Crea, por enemigo: la hoja, una variante del proyectil que usa sus sprites, una copia del ataque que dispara esa
    /// variante y una variante del enemigo (teñida de NEGRO para distinguirla) que usa esa copia. Los originales no se tocan.
    /// Menú: <b>Tools ▸ RedMagic ▸ Baking (prueba)</b> (4 / 5 / 6).
    /// </summary>
    public static class ProjectileSheetBaker
    {
        public const string RootName = "[Baking proyectiles]";

        private const string Folder = "Assets/Test/Baking/Proyectiles";
        private const string EnemiesFolder = "Assets/Prefabs/Enemies";
        private static readonly string[] Enemies = { "IceCannonOrb", "CinderKettleBot" };
        private static readonly Color Black = new Color(0.1f, 0.1f, 0.12f, 1f);
        private const int Gutter = 2;

        [MenuItem("Tools/RedMagic/Baking (prueba)/4 Proyectiles: empaquetar fotogramas y crear las variantes")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);
            foreach (var enemy in Enemies) BakeEnemy(enemy);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Baking] Proyectiles empaquetados. Ahora: Tools ▸ RedMagic ▸ Baking (prueba) ▸ 5.");
        }

        private static void BakeEnemy(string enemy)
        {
            string dir = $"{Folder}/{enemy}";
            Directory.CreateDirectory(dir);

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemiesFolder}/{enemy}.prefab");
            if (enemyPrefab == null) { Debug.LogError($"[Baking] No existe {EnemiesFolder}/{enemy}.prefab"); return; }

            // 1. los ataques que el enemigo usa y los proyectiles que lanzan
            var attackMap = new Dictionary<ReleaseAttack, ReleaseAttack>();
            var projectileMap = new Dictionary<GameObject, GameObject>();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            foreach (var attack in FindReferences<ReleaseAttack>(instance))
            {
                if (attackMap.ContainsKey(attack)) continue;
                string copyPath = $"{dir}/{attack.name}_Baked.asset";
                AssetDatabase.DeleteAsset(copyPath);
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(attack), copyPath)) continue;
                var copy = AssetDatabase.LoadAssetAtPath<ReleaseAttack>(copyPath);
                attackMap[attack] = copy;

                foreach (var projectile in FindReferences<GameObject>(copy))
                {
                    if (projectileMap.ContainsKey(projectile) || !projectile.GetComponentInChildren<RedMagic.Pipeline.SpriteStateMachine>()) continue;
                    projectileMap[projectile] = BuildProjectileVariant(projectile, dir, enemy);
                }

                var so = new SerializedObject(copy);
                Replace(so, projectileMap);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(copy);
            }

            // 2. el enemigo: usa los ataques copiados y se pinta de negro
            foreach (var component in instance.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform) continue;
                var so = new SerializedObject(component);
                if (Replace(so, attackMap)) so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>(true)) sr.color = Black;
            instance.name = enemy + "_Baked";
            PrefabUtility.SaveAsPrefabAsset(instance, $"{dir}/{enemy}_Baked.prefab");
            Object.DestroyImmediate(instance);

            Debug.Log($"[Baking] {enemy}: {attackMap.Count} ataque(s) copiado(s), {projectileMap.Count} proyectil(es) con hoja única.");
        }

        /// <summary>Empaqueta los fotogramas del proyectil en una hoja y devuelve la variante que los usa.</summary>
        private static GameObject BuildProjectileVariant(GameObject original, string dir, string enemy)
        {
            // fotogramas únicos, en orden de aparición
            var frames = new List<Sprite>();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
            foreach (var component in instance.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var so = new SerializedObject(component);
                for (var p = so.GetIterator(); p.NextVisible(true);)
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is Sprite s && !frames.Contains(s))
                        frames.Add(s);
            }
            if (frames.Count == 0) { Object.DestroyImmediate(instance); return original; }

            // hoja: rejilla de celdas del mayor fotograma, con 2 px de margen
            int cw = 0, ch = 0;
            foreach (var s in frames) { cw = Mathf.Max(cw, Mathf.CeilToInt(s.rect.width)); ch = Mathf.Max(ch, Mathf.CeilToInt(s.rect.height)); }
            int columns = Mathf.CeilToInt(Mathf.Sqrt(frames.Count));
            int rows = Mathf.CeilToInt(frames.Count / (float)columns);
            int sheetW = columns * (cw + Gutter) + Gutter, sheetH = rows * (ch + Gutter) + Gutter;

            var sheet = new Texture2D(sheetW, sheetH, TextureFormat.RGBA32, false, false);
            var clear = new Color32[sheetW * sheetH];
            sheet.SetPixels32(clear);

            var metas = new List<SpriteMetaData>();
            var rt = RenderTexture.GetTemporary(1, 1);
            var previous = RenderTexture.active;
            for (int i = 0; i < frames.Count; i++)
            {
                var s = frames[i];
                int col = i % columns, row = i / columns;
                int x = Gutter + col * (cw + Gutter), y = Gutter + (rows - 1 - row) * (ch + Gutter);

                RenderTexture.ReleaseTemporary(rt);
                rt = RenderTexture.GetTemporary(s.texture.width, s.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(s.texture, rt);
                RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(s.rect.x, s.rect.y, s.rect.width, s.rect.height), x, y);

                metas.Add(new SpriteMetaData
                {
                    name = "frame_" + i,
                    rect = new Rect(x, y, s.rect.width, s.rect.height),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height),
                });
            }
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            sheet.Apply(false);

            string sheetPath = $"{dir}/{original.name}_sheet.png";
            File.WriteAllBytes(sheetPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceSynchronousImport);

            // importación igual que la de los fotogramas originales
            var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frames[0].texture));
            var importer = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = frames[0].pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = sourceImporter != null ? sourceImporter.filterMode : FilterMode.Bilinear;
            importer.textureCompression = sourceImporter != null ? sourceImporter.textureCompression : TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.spritesheet = metas.ToArray();
            importer.SaveAndReimport();

            // sprites de la hoja, por nombre
            var byName = new Dictionary<string, Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
                if (asset is Sprite sp) byName[sp.name] = sp;
            var map = new Dictionary<Sprite, Sprite>();
            for (int i = 0; i < frames.Count; i++) map[frames[i]] = byName["frame_" + i];

            // la variante del proyectil: mismos componentes, fotogramas de la hoja
            foreach (var component in instance.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var so = new SerializedObject(component);
                if (Replace(so, map)) so.ApplyModifiedPropertiesWithoutUndo();
            }
            instance.name = original.name + "_Baked";
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, $"{dir}/{original.name}_Baked.prefab");
            Object.DestroyImmediate(instance);

            Debug.Log($"[Baking] {enemy}/{original.name}: {frames.Count} fotogramas {cw}×{ch} → 1 hoja {sheetW}×{sheetH} ({frames.Count} texturas → 1).");
            return variant;
        }

        // ------------------------------------------------------------------ colocar en la escena

        [MenuItem("Tools/RedMagic/Baking (prueba)/5 Proyectiles: poner originales vs variantes en la escena")]
        public static void Place()
        {
            RemoveTestObjects();
            var level = Object.FindFirstObjectByType<Level>();
            var root = new GameObject(RootName).transform;
            if (level != null) root.SetParent(level.transform, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Baking proyectiles");

            float x = 92f;
            foreach (var enemy in Enemies)
            {
                var original = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemiesFolder}/{enemy}.prefab");
                var baked = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{enemy}/{enemy}_Baked.prefab");
                if (original == null || baked == null) { Debug.LogError("[Baking] Primero el paso 4."); return; }

                PlaceOne(root, level, original, enemy + " ORIGINAL (15 texturas sueltas)", x, new Color(1f, 0.6f, 0.3f));
                PlaceOne(root, level, baked, enemy + " NEGRO: hoja única", x + 9f, new Color(0.4f, 1f, 0.6f));
                x += 24f;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeTransform = root;
            Debug.Log("[Baking] Enemigos puestos: el original (color normal) y el negro (hoja única) de cada tipo.");
        }

        private static void PlaceOne(Transform root, Level level, GameObject prefab, string label, float x, Color color)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = label;
            float ground = level != null ? level.GroundTop(Mathf.RoundToInt(x)) : 0f;
            go.transform.localPosition = new Vector3(x, ground + 1.5f, 0f);

            var text = new GameObject("Cartel " + label);
            text.transform.SetParent(root, false);
            text.transform.localPosition = new Vector3(x, ground + 6f, 0f);
            var mesh = text.AddComponent<TextMesh>();
            mesh.text = label.Replace(": ", "\n").Replace(" (", "\n(");
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.22f;
            mesh.fontSize = 60;
            mesh.color = color;
            var renderer = text.GetComponent<MeshRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 100;
        }

        [MenuItem("Tools/RedMagic/Baking (prueba)/5b Proyectiles: poner la lluvia de 100 (teclas 1 / 2 / 0 en Play)")]
        public static void PlaceRain()
        {
            var originals = new List<GameObject>();
            var baked = new List<GameObject>();
            foreach (var enemy in Enemies)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { $"{Folder}/{enemy}" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.Contains("_Projectile_Baked")) continue;
                    var bakedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    // el original es el prefab del que el variante hereda
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(bakedPrefab);
                    if (source == null) continue;
                    baked.Add(bakedPrefab);
                    originals.Add(source);
                }
            }
            if (baked.Count == 0) { Debug.LogError("[Baking] Primero el paso 4."); return; }

            var level = Object.FindFirstObjectByType<Level>();
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                if (level != null) root.transform.SetParent(level.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "Baking proyectiles");
            }
            var old = root.transform.Find("Lluvia");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var rain = new GameObject("Lluvia");
            rain.transform.SetParent(root.transform, false);
            float top = level != null ? level.Height - 3f : 17f;
            float bottom = level != null ? level.GroundTop(80) + 0.5f : 2f;
            rain.AddComponent<RedMagic.BakingTest.ProjectileRainTest>().Configure(originals.ToArray(), baked.ToArray(), 74f, 92f, top, bottom);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = rain;
            Debug.Log($"[Baking] Lluvia lista ({originals.Count} tipos de proyectil). En Play: 1 = originales, 2 = hoja única, 0 = parar, +/- = cantidad.");
        }

        [MenuItem("Tools/RedMagic/Baking (prueba)/6 BORRAR la prueba de proyectiles (sin restos)")]
        public static void DeleteProjectiles()
        {
            RemoveTestObjects();
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.SaveAssets();
            Debug.Log("[Baking] Borrada la prueba de proyectiles (escena y " + Folder + "). Guarda la escena.");
        }

        public static void RemoveTestObjects()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.name != RootName) continue;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                Object.DestroyImmediate(t.gameObject);
            }
        }

        // ------------------------------------------------------------------ utilidades

        /// <summary>Todo asset/objeto de tipo T al que apuntan las propiedades del objeto (sin repetir).</summary>
        private static List<T> FindReferences<T>(Object target) where T : Object
        {
            var found = new List<T>();
            void Scan(Object o)
            {
                var so = new SerializedObject(o);
                for (var p = so.GetIterator(); p.NextVisible(true);)
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is T t
                        && EditorUtility.IsPersistent(t) && !found.Contains(t))
                        found.Add(t);
            }

            if (target is GameObject go)
            {
                foreach (var c in go.GetComponentsInChildren<Component>(true)) if (c != null && !(c is Transform)) Scan(c);
            }
            else Scan(target);
            return found;
        }

        /// <summary>Cambia cada referencia a una clave del mapa por su valor. True si cambió algo.</summary>
        private static bool Replace<TKey, TValue>(SerializedObject so, Dictionary<TKey, TValue> map)
            where TKey : Object where TValue : Object
        {
            bool changed = false;
            for (var p = so.GetIterator(); p.NextVisible(true);)
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || !(p.objectReferenceValue is TKey key)) continue;
                if (map.TryGetValue(key, out var value) && value != null) { p.objectReferenceValue = value; changed = true; }
            }
            return changed;
        }
    }
}
