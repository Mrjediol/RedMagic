using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RedMagic.Bosses;
using RedMagic.Fx;
using RedMagic.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.FxTools
{
    /// <summary>
    /// Mete en el juego el arte horneado del ✨ VFX Lab de la web: los frames PNG que deja
    /// <c>Web/RedMagicWeb/tools/bake-vfx.mjs</c> en <c>Assets/Art/VfxLab/&lt;Bake&gt;/&lt;Lane&gt;/</c>
    /// se convierten en prefabs <b>pooled</b> (<c>Assets/Prefabs/Fx/VfxLab/</c>) y se enganchan en
    /// los assets que los usan. Idempotente: relanzarlo rehace los prefabs en su sitio (mismo GUID)
    /// y reasigna sólo lo que sigue siendo placeholder o ya era de este pack.
    ///
    /// <b>Añadir un efecto al juego son tres líneas de datos, sin código nuevo:</b>
    /// <list type="number">
    /// <item>una entrada en <c>vfx-bakes.json</c> (preset + colores) y <c>node bake-vfx.mjs</c>;</item>
    /// <item>una fila en <see cref="Prefabs"/> (qué prefab sale de qué bake / lane y a qué tamaño);</item>
    /// <item>una fila en <see cref="Assignments"/> (qué campo de qué asset lo usa).</item>
    /// </list>
    ///
    /// <b>Nunca pisa arte puesto a mano</b>: un campo sólo se reasigna si está vacío, si apunta a un
    /// placeholder geométrico conocido (<see cref="ReplaceablePlaceholders"/>) o a un prefab de este
    /// mismo pack. Lo que ya tiene arte propio se deja y se dice por consola.
    ///
    /// Los sprites se importan a <b>1 frame = 1 unidad</b> (PPU = lado del frame), así el tamaño en el
    /// juego de cada prefab es directamente su <see cref="PrefabRecipe.worldWidth"/>, sea cual sea la
    /// "Nitidez" con la que se horneó.
    /// </summary>
    public static class VfxLabArtPack
    {
        private const string ArtRoot = "Assets/Art/VfxLab";
        private const string PrefabFolder = "Assets/Prefabs/Fx/VfxLab";
        private const string BossFxFolder = "Assets/Prefabs/Fx/Bosses";
        private const string PlaceholderSquare = "Assets/Art/Placeholder/Square.png";
        private const string SortingLayer = "Characters";

        private enum Kind
        {
            /// <summary>Proyectil de arma: <c>ShotProjectile</c> + bucle. El origen del prefab es la CABEZA del proyectil.</summary>
            ShotProjectile,
            /// <summary>Efecto de un solo uso: <c>VfxOneShot</c> con la duración de sus frames.</summary>
            OneShot,
            /// <summary>Bucle suelto (la carga de un arma): sólo flipbook, lo enciende y apaga quien lo usa.</summary>
            Loop,
            /// <summary>Haz de arma: <c>ShotBeam</c> + bucle; se estira en X al largo del rayo y se tiñe con el arma.</summary>
            ShotBeam,
        }

        private readonly struct PrefabRecipe
        {
            public readonly string name, bake, lane;
            public readonly Kind kind;
            /// <summary>Ancho del frame en unidades del mundo (el dibujo ocupa una parte).</summary>
            public readonly float worldWidth;
            /// <summary>Proyectil: diámetro del collider (unidades) y dónde está su cabeza en el frame (−1..1; 0 = centro).</summary>
            public readonly float colliderDiameter, headX;

            public PrefabRecipe(string name, string bake, string lane, Kind kind, float worldWidth,
                                float colliderDiameter = 0f, float headX = 0f)
            {
                this.name = name; this.bake = bake; this.lane = lane; this.kind = kind;
                this.worldWidth = worldWidth; this.colliderDiameter = colliderDiameter; this.headX = headX;
            }
        }

        // ------------------------------------------------------------------ qué prefab sale de qué bake
        private static readonly PrefabRecipe[] Prefabs =
        {
            new PrefabRecipe("Fx_Shot_Fireball",    "Fireball",      "Move",   Kind.ShotProjectile, 1.7f,  0.4f,  0.42f),
            new PrefabRecipe("Fx_Hit_Fireball",     "Fireball",      "Impact", Kind.OneShot,        1.9f),
            new PrefabRecipe("Fx_Shot_ArcaneBurst", "ArcaneBurst",   "Move",   Kind.ShotProjectile, 1.15f, 0.25f, 0.42f),
            new PrefabRecipe("Fx_Hit_ArcaneBurst",  "ArcaneBurst",   "Impact", Kind.OneShot,        1.2f),
            new PrefabRecipe("Fx_Shot_AstralLance", "AstralLance",   "Move",   Kind.ShotProjectile, 1.35f, 0.3f,  0.66f),
            new PrefabRecipe("Fx_Hit_AstralLance",  "AstralLance",   "Impact", Kind.OneShot,        1.2f),
            new PrefabRecipe("Fx_Shot_RuneGrenade", "RuneGrenade",   "Move",   Kind.ShotProjectile, 0.95f, 0.35f),
            new PrefabRecipe("Fx_Explosion_Rune",   "RuneExplosion", "Spawn",  Kind.OneShot,        4.4f),
            new PrefabRecipe("Fx_Shot_ArcaneBeam",  "BeamTile",      "Loop",   Kind.ShotBeam,       1f),
            new PrefabRecipe("Fx_Charge_Arcane",    "ArcaneCharge",  "Loop",   Kind.Loop,           0.9f),
            new PrefabRecipe("Fx_HealBurst",        "HealBurst",     "Spawn",  Kind.OneShot,        1.9f),
        };

        // ------------------------------------------------------------------ quién usa cada prefab
        private const string Weapons = "Assets/Resources/Items/Weapons/";

        private static readonly (string asset, string property, string prefab)[] Assignments =
        {
            (Weapons + "Weapon_BolaDeFuego.asset",   "baseShot.projectilePrefab",   "Fx_Shot_Fireball"),
            (Weapons + "Weapon_BolaDeFuego.asset",   "baseShot.impactVfxPrefab",    "Fx_Hit_Fireball"),
            (Weapons + "Weapon_RafagaArcana.asset",  "baseShot.projectilePrefab",   "Fx_Shot_ArcaneBurst"),
            (Weapons + "Weapon_RafagaArcana.asset",  "baseShot.impactVfxPrefab",    "Fx_Hit_ArcaneBurst"),
            (Weapons + "Weapon_LanzaAstral.asset",   "baseShot.projectilePrefab",   "Fx_Shot_AstralLance"),
            (Weapons + "Weapon_LanzaAstral.asset",   "baseShot.impactVfxPrefab",    "Fx_Hit_AstralLance"),
            (Weapons + "Weapon_GranadaRunica.asset", "baseShot.projectilePrefab",   "Fx_Shot_RuneGrenade"),
            (Weapons + "Weapon_GranadaRunica.asset", "baseShot.explosionVfxPrefab", "Fx_Explosion_Rune"),
            (Weapons + "Weapon_RayoArcano.asset",    "baseShot.beamPrefab",         "Fx_Shot_ArcaneBeam"),
            (Weapons + "Weapon_RayoArcano.asset",    "baseShot.chargeVfxPrefab",    "Fx_Charge_Arcane"),
            ("Assets/Resources/SynergyConfig.asset", "tuning.healVfxPrefab",        "Fx_HealBurst"),
        };

        /// <summary>Placeholders geométricos que este pack puede sustituir. Cualquier otro prefab asignado es arte puesto a mano: no se toca.</summary>
        private static readonly string[] ReplaceablePlaceholders =
        {
            "Assets/Prefabs/Fx/Fx_Shot_Disc1.prefab",
            "Assets/Prefabs/Fx/Fx_Shot_Sliver.prefab",
            "Assets/Prefabs/Fx/Fx_Shot_Diamond.prefab",
            "Assets/Prefabs/Fx/Fx_Shot_Beam.prefab",
        };

        /// <summary>Bake con el que se visten los barridos de jefe (<c>Fx_&lt;Jefe&gt;_SweepBeam</c>) que siguen siendo un cuadrado.</summary>
        private const string BossSweepBake = "BeamTile";
        private const string BossSweepLane = "Loop";

        // ================================================================= menú

        [MenuItem("Tools/RedMagic/FX/5 · VFX Lab · Arte del juego (importar y asignar)")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var log = new List<string>();

            var bakes = new Dictionary<string, Bake>();
            if (AssetDatabase.IsValidFolder(ArtRoot))
            {
                foreach (var folder in AssetDatabase.GetSubFolders(ArtRoot))
                {
                    var bake = ReadBake(folder, log);
                    if (bake != null) bakes[bake.name] = bake;
                }
            }

            if (bakes.Count == 0)
            {
                Debug.LogWarning($"[VfxLabArtPack] No hay nada horneado en {ArtRoot}. Lanza antes: " +
                                 "cd Web/RedMagicWeb/tools && npm install && node bake-vfx.mjs");
                return;
            }

            EnsureFolder(PrefabFolder);
            var built = new Dictionary<string, GameObject>();
            foreach (var recipe in Prefabs)
            {
                var prefab = BuildPrefab(recipe, bakes, log);
                if (prefab != null) built[recipe.name] = prefab;
            }

            int assigned = 0;
            foreach (var (asset, property, prefab) in Assignments)
                if (built.TryGetValue(prefab, out var go) && Assign(asset, property, go, log)) assigned++;

            int dressed = bakes.TryGetValue(BossSweepBake, out var sweep) ? DressBossSweeps(sweep, log) : 0;

            AssetDatabase.SaveAssets();
            Debug.Log($"[VfxLabArtPack] {bakes.Count} horneado(s), {built.Count} prefab(s), {assigned} campo(s) asignado(s), " +
                      $"{dressed} barrido(s) de jefe vestido(s).\n" + string.Join("\n", log));
        }

        // ================================================================= lectura del horneado

        [Serializable]
        private class BakeLane
        {
            public string name;
            public float fps;
            public bool loop;
            public int frames;
        }

        [Serializable]
        private class BakeManifest
        {
            public string name, preset, kind;
            public int frameSize, density;
            public BakeLane[] lanes;
        }

        private sealed class Bake
        {
            public string name;
            public readonly Dictionary<string, (Sprite[] frames, float fps, bool loop)> lanes =
                new Dictionary<string, (Sprite[] frames, float fps, bool loop)>();
        }

        private static Bake ReadBake(string folder, List<string> log)
        {
            string manifestPath = $"{folder}/bake.json";
            if (!File.Exists(manifestPath)) return null;

            var manifest = JsonUtility.FromJson<BakeManifest>(File.ReadAllText(manifestPath));
            if (manifest?.lanes == null || manifest.lanes.Length == 0)
            {
                log.Add($"  ✗ {folder}: bake.json sin animaciones.");
                return null;
            }

            var bake = new Bake { name = Path.GetFileName(folder) };
            foreach (var lane in manifest.lanes)
            {
                var sprites = LoadFrames($"{folder}/{lane.name}");
                if (sprites.Length == 0)
                {
                    log.Add($"  ✗ {bake.name}/{lane.name}: sin PNG.");
                    continue;
                }
                bake.lanes[lane.name] = (sprites, lane.fps > 0f ? lane.fps : 12f, lane.loop);
            }
            return bake;
        }

        /// <summary>Los frames de una carpeta, en orden, importados como sprite único a 1 frame = 1 unidad.</summary>
        private static Sprite[] LoadFrames(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return Array.Empty<Sprite>();

            var paths = Directory.GetFiles(folder, "*.png").Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            foreach (var path in paths)
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

                importer.GetSourceTextureWidthAndHeight(out int width, out _);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                bool ok = importer.textureType == TextureImporterType.Sprite
                          && importer.spriteImportMode == SpriteImportMode.Single
                          && Mathf.Approximately(importer.spritePixelsPerUnit, width)
                          && !importer.mipmapEnabled
                          && importer.alphaIsTransparency
                          && importer.filterMode == FilterMode.Bilinear
                          && importer.wrapMode == TextureWrapMode.Clamp
                          && importer.textureCompression == TextureImporterCompression.CompressedHQ
                          && settings.spriteMeshType == SpriteMeshType.FullRect;
                if (ok) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = width;
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;   // luz suave: una malla ajustada recorta el halo
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            return paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();
        }

        // ================================================================= prefabs

        private static GameObject BuildPrefab(PrefabRecipe recipe, Dictionary<string, Bake> bakes, List<string> log)
        {
            if (!bakes.TryGetValue(recipe.bake, out var bake) || !bake.lanes.TryGetValue(recipe.lane, out var lane))
            {
                log.Add($"  ✗ {recipe.name}: falta el horneado {recipe.bake}/{recipe.lane}.");
                return null;
            }

            var root = new GameObject(recipe.name);
            try
            {
                switch (recipe.kind)
                {
                    case Kind.ShotProjectile:
                    {
                        var body = root.AddComponent<Rigidbody2D>();
                        body.gravityScale = 0f;
                        body.freezeRotation = true;
                        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                        // El arte cuelga de un hijo desplazado: el origen del prefab queda en la cabeza
                        // del proyectil (ahí están el collider y el punto donde salta el impacto).
                        var art = new GameObject("Art");
                        art.transform.SetParent(root.transform, false);
                        art.transform.localScale = Vector3.one * recipe.worldWidth;
                        art.transform.localPosition = new Vector3(-recipe.headX * 0.5f * recipe.worldWidth, 0f, 0f);
                        AddFlipbook(art, lane.frames, lane.fps, loop: true, order: 6);

                        var collider = root.AddComponent<CircleCollider2D>();
                        collider.isTrigger = true;
                        collider.radius = Mathf.Max(0.02f, recipe.colliderDiameter * 0.5f);

                        root.AddComponent<Items.ShotProjectile>();   // faceTravelDirection = true: el arte apunta a +X
                        AddStyle(root, tint: false, resize: false);
                        break;
                    }
                    case Kind.OneShot:
                    {
                        root.transform.localScale = Vector3.one * recipe.worldWidth;
                        var flipbook = AddFlipbook(root, lane.frames, lane.fps, loop: false, order: 20);
                        var oneShot = root.AddComponent<VfxOneShot>();
                        SetFloat(oneShot, "lifetime", flipbook.Duration);
                        break;
                    }
                    case Kind.Loop:
                    {
                        root.transform.localScale = Vector3.one * recipe.worldWidth;
                        AddFlipbook(root, lane.frames, lane.fps, loop: true, order: 20);
                        break;
                    }
                    case Kind.ShotBeam:
                    {
                        AddFlipbook(root, lane.frames, lane.fps, loop: true, order: 6);
                        root.AddComponent<Items.ShotBeam>();
                        // Con estilo: el arma tiñe el haz (el arte es casi blanco para eso) y lo funde al
                        // acabar. El largo lo estira ShotBeam cada frame, no el estilo.
                        AddStyle(root, tint: true, resize: false);
                        break;
                    }
                }

                string path = $"{PrefabFolder}/{recipe.name}.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                log.Add($"  ✓ {path}  ({recipe.bake}/{recipe.lane}, {lane.frames.Length} frames, {recipe.worldWidth:0.##} u)");
                return saved;
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static SpriteFlipbook AddFlipbook(GameObject go, Sprite[] frames, float fps, bool loop, int order)
        {
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = Color.white;
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = order;

            var flipbook = go.GetComponent<SpriteFlipbook>();
            if (flipbook == null) flipbook = go.AddComponent<SpriteFlipbook>();
            var so = new SerializedObject(flipbook);
            var array = so.FindProperty("frames");
            array.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            so.FindProperty("framesPerSecond").floatValue = fps;
            so.FindProperty("pingPong").boolValue = false;
            so.FindProperty("randomStart").boolValue = false;
            so.FindProperty("oneShot").boolValue = !loop;
            so.ApplyModifiedPropertiesWithoutUndo();
            return flipbook;
        }

        private static void AddStyle(GameObject go, bool tint, bool resize)
        {
            var style = go.GetComponent<FxPlaceholderStyle>();
            if (style == null) style = go.AddComponent<FxPlaceholderStyle>();
            var so = new SerializedObject(style);
            so.FindProperty("tint").boolValue = tint;
            so.FindProperty("resize").boolValue = resize;
            so.FindProperty("matchSorting").boolValue = true;
            so.FindProperty("scaleColliderToSprite").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================= asignación

        private static bool Assign(string assetPath, string propertyPath, GameObject prefab, List<string> log)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                log.Add($"  ✗ no existe {assetPath}.");
                return false;
            }

            var so = new SerializedObject(asset);
            var property = so.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                log.Add($"  ✗ {Path.GetFileNameWithoutExtension(assetPath)}: no hay campo '{propertyPath}'.");
                return false;
            }

            var current = property.objectReferenceValue;
            if (current == prefab) return true;

            string currentPath = current != null ? AssetDatabase.GetAssetPath(current) : null;
            bool replaceable = current == null
                               || currentPath.StartsWith(PrefabFolder + "/", StringComparison.Ordinal)
                               || Array.IndexOf(ReplaceablePlaceholders, currentPath) >= 0;
            if (!replaceable)
            {
                log.Add($"  · {Path.GetFileNameWithoutExtension(assetPath)} ▸ {propertyPath}: ya tiene arte propio ({currentPath}); no se toca.");
                return false;
            }

            property.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            log.Add($"  → {Path.GetFileNameWithoutExtension(assetPath)} ▸ {propertyPath} = {prefab.name}" +
                    (currentPath != null ? $"  (antes {Path.GetFileNameWithoutExtension(currentPath)})" : ""));
            return true;
        }

        // ================================================================= barridos de jefe

        /// <summary>
        /// Viste los <c>Fx_&lt;Jefe&gt;_SweepBeam</c> que siguen siendo el cuadrado placeholder (o que ya
        /// vistió este pack): el sprite pasa a ser el tramo de rayo en bucle. Conservan su
        /// <see cref="FxPlaceholderStyle"/> tal cual, así el jefe los sigue estirando a su largo y
        /// tiñendo con el color de su fase. Un barrido con arte puesto a mano no se toca.
        /// </summary>
        private static int DressBossSweeps(Bake bake, List<string> log)
        {
            if (!bake.lanes.TryGetValue(BossSweepLane, out var lane) || !AssetDatabase.IsValidFolder(BossFxFolder)) return 0;

            var square = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSquare);
            int dressed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { BossFxFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<BossSweepBeam>() == null) continue;

                var current = asset.GetComponent<SpriteRenderer>();
                if (current == null) continue;
                bool ours = Array.IndexOf(lane.frames, current.sprite) >= 0;
                if (current.sprite != null && current.sprite != square && !ours)
                {
                    log.Add($"  · {asset.name}: ya tiene arte propio; no se toca.");
                    continue;
                }

                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var renderer = contents.GetComponent<SpriteRenderer>();
                    int order = renderer.sortingOrder;
                    string layer = renderer.sortingLayerName;
                    AddFlipbook(contents, lane.frames, lane.fps, loop: true, order: order);
                    renderer.sortingLayerName = layer;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }

                dressed++;
                if (!ours) log.Add($"  → {asset.name}: cuadrado → rayo ({bake.name}).");
            }

            return dressed;
        }

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
