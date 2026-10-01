using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedMagic.Bosses;
using RedMagic.Bosses.EditorTools;
using RedMagic.Combat;
using RedMagic.Economy;
using RedMagic.Enemies;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Importa un enemigo o jefe <b>por partes</b> hecho en la web (<c>rig-config.json</c>,
    /// <c>docs/schemas/rig-config.schema.json</c>): un .zip con el config, los PNG de las piezas y,
    /// opcionalmente, <c>enemy-config.json</c> (tuning del enemigo) o <c>boss-config.json</c>
    /// (ataques + definición del jefe); o el <c>rig-config.json</c> suelto de una carpeta.
    ///
    /// Es la vía <b>separada</b> de la de sprite sheets (<c>Import Config…</c>,
    /// <c>CombinedBundleImporter</c>): no toca ninguna de las dos. Hace a mano lo que hacen
    /// <see cref="ProceduralEnemyPacks"/> / <see cref="ProceduralBossPack"/>, pero con datos:
    /// jerarquía <c>raíz/Visual/piezas</c>, <see cref="ProceduralRig"/> entero, pila de enemigo
    /// (<see cref="ProceduralRigTool.Setup"/>) o de jefe (<c>BossBodyBuilder.AddBossStack</c> +
    /// <see cref="BossLimbs"/>), y el motor (<c>driver</c>) en el propio prefab: un solo prefab jugable.
    ///
    /// <b>Qué manda al reimportar.</b> El cuerpo (jerarquía bajo Visual, collider, cadáver y
    /// ProceduralRig) sale <b>siempre</b> del JSON: es lo que se edita en la web. Una clave ausente
    /// deja el valor que había; una lista presente sustituye a la entera. Lo afinado en el Inspector
    /// del resto (EnemyStats, vida/arena del jefe) sólo se pisa la primera vez o con
    /// <see cref="Options.ResetTuning"/> — salvo la <b>baraja de ataques</b> del enemigo
    /// (<c>enemy-config.json</c> ▸ <c>attacks</c> + <c>tuning.attacks</c>), que también viene siempre
    /// del JSON. Para llevar a la web lo afinado en el rig, antes
    /// <c>Exportar rig a JSON</c>.
    /// </summary>
    public static class RigBundleImporter
    {
        public sealed class Options
        {
            /// <summary>Pisa también EnemyStats / la pila del jefe con lo que diga el JSON.</summary>
            public bool ResetTuning;

            /// <summary>Otro nombre de prefab (el de <c>name</c> si no). Lo usa la prueba de ida y vuelta.</summary>
            public string NameOverride;

            /// <summary>Carpeta del prefab (por defecto la de los enemigos por partes).</summary>
            public string PrefabFolder;
        }

        [MenuItem("Tools/Web/Rig/Importar rig (.zip · carpeta)…")]
        private static void ImportMenu()
        {
            string path = EditorUtility.OpenFilePanelWithFilters(
                "Rig de la web: el .zip, o el rig-config.json de una carpeta", Application.dataPath,
                new[] { "Rig (.zip / rig-config.json)", "zip,json" });
            if (string.IsNullOrEmpty(path)) return;

            int choice = EditorUtility.DisplayDialogComplex("Importar rig",
                "El cuerpo (piezas, collider y ProceduralRig) se reescribe siempre desde el JSON.\n\n" +
                "¿Y lo afinado en el Inspector del resto (EnemyStats, vida y arena del jefe)?",
                "Conservarlo", "Cancelar", "Resetear desde el JSON");
            if (choice == 1) return;

            string summary = Import(path, new Options { ResetTuning = choice == 2 });
            Debug.Log(summary);
            EditorUtility.DisplayDialog("Importar rig", summary, "OK");
        }

        /// <summary>Import completo. Nunca lanza: todo va al resumen (mismo patrón que los otros importadores).</summary>
        public static string Import(string path, Options options = null)
        {
            options = options ?? new Options();
            var report = new ConfigImportReport(Path.GetFileName(path));
            string temp = null;

            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    throw new ConfigImportException($"No existe el archivo '{path}'.");

                string source, configPath;
                if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    temp = Path.Combine(Path.GetTempPath(), $"RedMagicRigImport_{Guid.NewGuid():N}");
                    Directory.CreateDirectory(temp);
                    ZipFile.ExtractToDirectory(path, temp);
                    source = temp;
                    configPath = Path.Combine(temp, RigConfigJson.ConfigFile);
                    if (!File.Exists(configPath))
                        throw new ConfigImportException($"El .zip no trae {RigConfigJson.ConfigFile} en la raíz — ¿es un rig exportado de la web?");

                    // El proyectil de biblioteca del enemigo viaja en Library/<id>/, como en un bundle
                    // combinado: se lleva a su carpeta compartida antes de leer enemy-config.json.
                    if (Directory.Exists(Path.Combine(temp, "Library")) &&
                        CombinedBundleImporter.LiftEmbeddedLibraryEntries(temp, report) > 0)
                        AssetDatabase.Refresh();
                }
                else
                {
                    configPath = Path.GetFullPath(path);
                    source = Path.GetDirectoryName(configPath);
                }

                var config = JObject.Parse(File.ReadAllText(configPath));
                ImportConfig(config, source, configPath, options, report);
            }
            catch (ConfigImportException e) { report.Errors.Add(e.Message); }
            catch (JsonException e) { report.Errors.Add($"JSON inválido: {e.Message}"); }
            catch (Exception e) { report.Errors.Add($"Excepción inesperada: {e}"); }
            finally
            {
                if (temp != null && Directory.Exists(temp))
                {
                    try { Directory.Delete(temp, true); }
                    catch (Exception e) { report.Warnings.Add($"No se pudo borrar '{temp}': {e.Message}"); }
                }
            }

            return report.BuildSummary();
        }

        // ============================================================ import

        private sealed class Node
        {
            public string Name, Parent, Path;
            public JObject Json;
            public bool RigPart => Json.Value<bool?>("rigPart") ?? true;
        }

        private static void ImportConfig(JObject config, string source, string configPath, Options options,
                                         ConfigImportReport report)
        {
            // --- 1) validar TODO antes de escribir un solo asset.
            string name = options.NameOverride ?? ConfigJson.RequireString(config, "name", "RigConfig");
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("/"))
                throw new ConfigImportException($"name: '{name}' no vale como nombre de archivo.");

            string kind = config.Value<string>("kind") ?? "enemy";
            if (kind != "enemy" && kind != "boss")
                throw new ConfigImportException($"kind: '{kind}' no es 'enemy' ni 'boss'.");
            bool boss = kind == "boss";

            var driverType = DriverType(config.Value<string>("driver") ?? "code");
            if (config.Value<string>("driver") == "dotween")
                report.Warnings.Add("driver 'dotween': ese motor se retiró; el prefab lleva RigDriverCode (mismos datos, mismo resultado).");
            var nodes = ReadNodes(config);
            ValidateReferences(config, nodes);

            // --- 2) sprites: los del proyecto se usan tal cual; los del bundle se copian y se configuran.
            var sprites = ResolveSprites(config, nodes, source, name, report);

            // --- 2b) arte de los efectos de gesto ('artAssets' del propio rig-config): prefabs del pool.
            BuildGestureArt(config, source, name, boss, report);

            // --- 3) configs que acompañan al cuerpo.
            EnemyTuning tuning = null;
            EnemyDeckImporter.Deck deck = null;
            EnemyTier? tier = null;
            string enemyConfigPath = RigConfigJson.SiblingPath(configPath, RigConfigJson.EnemyConfigFile);
            string bossConfigPath = RigConfigJson.SiblingPath(configPath, RigConfigJson.BossConfigFile);
            if (!boss && File.Exists(enemyConfigPath))
            {
                var enemyJson = JObject.Parse(File.ReadAllText(enemyConfigPath));
                tuning = EnemyConfigImporter.ReadTuning(enemyJson, report);

                // Baraja de ataques del catálogo: su arte viaja en Art/ del bundle y se deja junto a
                // los PNG de las piezas, donde lo busca EnemyDeckImporter.
                string artSource = Path.Combine(source, "Art");
                string artRoot = $"{RigConfigJson.ArtRoot}/{name}";
                if (Directory.Exists(artSource) &&
                    !string.Equals(Path.GetFullPath(artSource), Path.GetFullPath($"{artRoot}/Art"), StringComparison.OrdinalIgnoreCase))
                {
                    int copied = 0, removed = 0;
                    BossBundleImporter.Sync(artSource, Path.GetFullPath($"{artRoot}/Art"), ref copied, ref removed);
                    AssetDatabase.Refresh();
                }

                deck = EnemyDeckImporter.Read(enemyJson, name, artRoot, report);
                if (deck != null) deck.ApplyTo(tuning);
                if (enemyJson["tier"] != null) tier = ConfigJson.ParseEnum<EnemyTier>(enemyJson["tier"], "enemy-config.tier");
                if (enemyJson["presence"] != null)
                    report.Warnings.Add("enemy-config.presence se ignora en un enemigo por partes: el cuerpo y el collider los define rig-config.");
            }

            BossDefinition definition = null;
            if (boss && File.Exists(bossConfigPath)) definition = ImportBossConfig(bossConfigPath, source, report);
            else if (boss && config["bossBody"]?["definition"] is JToken defToken && defToken.Type != JTokenType.Null)
                definition = ConfigJson.ReadAsset<BossDefinition>(defToken, "bossBody.definition");   // una ya existente
            if (boss && File.Exists(enemyConfigPath)) report.Warnings.Add($"{RigConfigJson.EnemyConfigFile} se ignora en un jefe (su vida y arena van en 'bossBody').");
            if (!boss && File.Exists(bossConfigPath)) report.Warnings.Add($"{RigConfigJson.BossConfigFile} se ignora: kind = enemy.");

            // --- 4) prefab.
            string folder = options.PrefabFolder ?? ProceduralEnemyPacks.Folder;
            BossAuthoring.EnsureFolder(folder);
            string prefabPath = $"{folder}/{name}.prefab";
            bool isNew = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null;
            if (isNew) CreateEmptyPrefab(name, prefabPath);
            bool reset = options.ResetTuning || isNew;

            var bossBody = config["bossBody"] as JObject ?? new JObject();
            var log = new StringBuilder();

            ProceduralRigTool.Setup(prefabPath, log,
                prepare: root =>
                {
                    ApplyRoot(root, config["root"] as JObject, report);
                    ApplyCollider(root, config["collider"] as JObject);
                    var visual = root.transform.Find(ProceduralRigTool.VisualName);
                    ApplyTransform(visual, config["visual"] as JObject);
                    BuildHierarchy(visual, nodes, sprites, report);

                    if (boss && (reset || root.GetComponent<BossController>() == null))
                    {
                        var rb = ProceduralRigTool.Get<Rigidbody2D>(root);
                        rb.bodyType = RigidbodyType2D.Kinematic;
                        rb.useFullKinematicContacts = true;
                        rb.freezeRotation = true;
                        BossAuthoring.TrySetTag(root, "Enemy");
                        BossBodyBuilder.AddBossStack(root, bossBody, report);
                        ProceduralRigTool.Get<Audio.SoundEmitter>(root);   // como el Coloso: sonidos posicionales del jefe
                    }
                },
                configure: (root, rig, createdRig) =>
                {
                    var visual = root.transform.Find(ProceduralRigTool.VisualName);
                    WriteRig(rig, visual, nodes, config, sprites, report);
                    ApplyCorpse(root, config["corpse"] as JObject);
                    // La etiqueta del JSON manda también sobre la que pone la pila de jefe.
                    if (config["root"] is JObject r && r["tag"] != null) BossAuthoring.TrySetTag(root, r.Value<string>("tag"));

                    if (config["bossLimbs"] is JObject limbs) WriteLimbs(ProceduralRigTool.Get<BossLimbs>(root), visual, limbs);

                    if (boss) LinkBoss(root, visual, definition, bossBody, isNew, report);

                    if (!boss && reset)
                    {
                        var stats = root.GetComponent<EnemyStats>();
                        if (stats != null && tuning != null)
                        {
                            stats.EditorSetTuning(tuning);
                            EditorUtility.SetDirty(stats);
                        }

                        var dropper = root.GetComponent<CurrencyDropper>();
                        if (dropper != null && tier.HasValue) new BossAuthoring.Fields(dropper).Set("tier", (int)tier.Value).Apply();
                    }

                    // La baraja viene siempre del JSON (como el cuerpo): sus ataques se editan en la web.
                    if (!boss && deck != null) EnemyDeckImporter.ApplyToRoot(root, deck);
                },
                enemyStack: !boss);

            report.Add(isNew, prefabPath);
            if (!reset) report.Warnings.Add("Lo afinado en el Inspector (EnemyStats / vida y arena del jefe) se ha conservado; el cuerpo y el rig vienen del JSON.");

            ProceduralRigTool.EnsureDriver(prefabPath, driverType, log);

            AssetDatabase.SaveAssets();

            // Un jefe sin avisos ni ondas no se puede leer: los hereda de otro con su misma pelea, o placeholders.
            if (boss)
            {
                string fx = FxTools.FxPlaceholderPack.EnsureForBoss(prefabPath);
                if (!string.IsNullOrEmpty(fx)) report.Warnings.Add(fx);
            }

            Debug.Log($"[RigBundleImporter] {name}\n{log}");
        }

        /// <summary>
        /// Los efectos de gesto (<c>gestures[].phases[].fx[].fx = {"art": id}</c>) apuntan al bloque
        /// <c>artAssets</c> del rig-config, el mismo formato que el de jefes y enemigos. Sus frames
        /// viajan en <c>Art/&lt;libraryId&gt;/</c> del bundle; aquí se copian junto a las piezas, se
        /// construye un prefab por entrada (<see cref="BossArtBuilder"/>) y cada referencia se cambia
        /// por la ruta de su prefab, que es lo que lee <see cref="WriteRig"/>.
        /// </summary>
        private static void BuildGestureArt(JObject config, string source, string name, bool boss, ConfigImportReport report)
        {
            if (!(config["gestures"] is JArray gestures)) return;
            var artAssets = config["artAssets"] as JObject;

            Dictionary<string, string> artMap = null;
            if (artAssets != null && artAssets.Count > 0)
            {
                string artRoot = $"{RigConfigJson.ArtRoot}/{name}";
                string artSource = Path.Combine(source, "Art");
                if (Directory.Exists(artSource) &&
                    !string.Equals(Path.GetFullPath(artSource), Path.GetFullPath($"{artRoot}/Art"), StringComparison.OrdinalIgnoreCase))
                {
                    int copied = 0, removed = 0;
                    // La misma carpeta Art/ del bundle que copia la baraja del enemigo: lleva el arte de ambos.
                    BossBundleImporter.Sync(artSource, Path.GetFullPath($"{artRoot}/Art"), ref copied, ref removed);
                    AssetDatabase.Refresh();
                }

                foreach (var prop in artAssets.Properties())
                    if (prop.Value is JObject entry && entry.Value<string>("folder") is string folder &&
                        !folder.StartsWith("Assets/", StringComparison.Ordinal))
                        entry["folder"] = $"{artRoot}/{folder.TrimStart('/')}";

                var log = new StringBuilder();
                string prefabFolder = boss ? null : $"{EnemyDeckImporter.ArtPrefabRoot}/{name}";
                artMap = BossArtBuilder.Build(name, artAssets, log, prefabFolder);
                foreach (var path in artMap.Values) report.Add(false, path);
                if (log.Length > 0) report.Warnings.Add(log.ToString().TrimEnd());
            }

            BossArtBuilder.ResolveArtRefs(gestures, artMap, report.Warnings, "gestures");
        }

        private static Type DriverType(string driver)
        {
            // Un solo motor: RigDriverCode. 'dotween' se sigue aceptando (configs antiguos) pero el
            // motor de DOTween se retiró tras compararlos (Legacy/RigDriverTween.cs): mismo resultado.
            if (driver == "code" || driver == "dotween") return typeof(RigDriverCode);
            throw new ConfigImportException($"driver: '{driver}' no es 'code'.");
        }

        private static void CreateEmptyPrefab(string name, string prefabPath)
        {
            var root = new GameObject(name);
            try
            {
                new GameObject(ProceduralRigTool.VisualName).transform.SetParent(root.transform, false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ============================================================ validación

        private static List<Node> ReadNodes(JObject config)
        {
            if (!(config["parts"] is JArray array) || array.Count == 0)
                throw new ConfigImportException("parts: vacío o falta.");

            var nodes = new List<Node>();
            var paths = new HashSet<string>();
            for (int i = 0; i < array.Count; i++)
            {
                var json = ConfigJson.AsObject(array[i], $"parts[{i}]");
                string name = ConfigJson.RequireString(json, "name", $"parts[{i}]");
                string parent = json.Value<string>("parent") ?? "";
                if (name.Contains("/")) throw new ConfigImportException($"parts[{i}].name: '{name}' no puede llevar '/'.");
                if (name == RigConfigJson.BodyToken && parent == "")
                    throw new ConfigImportException($"parts[{i}].name: '{RigConfigJson.BodyToken}' está reservado para la raíz visual.");

                string path = parent == "" ? name : $"{parent}/{name}";
                if (!paths.Add(path)) throw new ConfigImportException($"parts[{i}]: '{path}' está repetido.");
                nodes.Add(new Node { Name = name, Parent = parent, Path = path, Json = json });
            }

            foreach (var n in nodes)
                if (n.Parent != "" && !paths.Contains(n.Parent))
                    throw new ConfigImportException($"parts '{n.Path}': su padre '{n.Parent}' no está en 'parts'.");

            return nodes;
        }

        /// <summary>Cada referencia a una pieza (roles, poses, sprites por estado, extremidades) tiene que existir.</summary>
        private static void ValidateReferences(JObject config, List<Node> nodes)
        {
            var valid = new HashSet<string>(nodes.Select(n => n.Path)) { RigConfigJson.BodyToken };

            void Check(JToken token, string context)
            {
                if (token == null || token.Type == JTokenType.Null) return;
                string path = token.Value<string>();
                if (!valid.Contains(path)) throw new ConfigImportException($"{context}: la pieza '{path}' no está en 'parts'.");
            }

            if (config["rig"] is JObject rig)
            {
                foreach (var f in RigConfigJson.RigFields())
                {
                    if (!rig.TryGetValue(f.Name, out var token)) continue;
                    if (f.FieldType == typeof(Transform)) Check(token, $"rig.{f.Name}");
                    else if (f.FieldType == typeof(List<Transform>) && token is JArray list)
                        for (int i = 0; i < list.Count; i++) Check(list[i], $"rig.{f.Name}[{i}]");
                }
            }

            if (config["spriteSwaps"] is JArray swaps)
                for (int i = 0; i < swaps.Count; i++) Check(swaps[i]["part"], $"spriteSwaps[{i}].part");

            if (config["gestures"] is JArray gestures)
                for (int i = 0; i < gestures.Count; i++)
                    foreach (var key in new[] { "windup", "strike" })
                        if (gestures[i][key] is JArray poses)
                            for (int j = 0; j < poses.Count; j++) Check(poses[j]["part"], $"gestures[{i}].{key}[{j}].part");

            if (config["bossLimbs"] is JObject limbs)
                foreach (var key in new[] { "limbs", "orbiters", "grips" })
                    if (limbs[key] is JArray list)
                        for (int i = 0; i < list.Count; i++) Check(list[i], $"bossLimbs.{key}[{i}]");

            if (config["bossBody"] is JObject body) Check(body["bodyRenderer"], "bossBody.bodyRenderer");
        }

        // ============================================================ sprites

        /// <summary>Todas las referencias a sprite del config, resueltas a Sprite.</summary>
        private static Dictionary<string, Sprite> ResolveSprites(JObject config, List<Node> nodes, string source,
                                                                 string name, ConfigImportReport report)
        {
            var refs = new HashSet<string>();
            void Add(JToken token)
            {
                if (token != null && token.Type == JTokenType.String && !string.IsNullOrEmpty(token.Value<string>()))
                    refs.Add(token.Value<string>().Replace('\\', '/'));
            }

            foreach (var n in nodes) Add(n.Json["sprite"]);
            if (config["rig"] is JObject rig)
                foreach (var f in RigConfigJson.RigFields().Where(f => f.FieldType == typeof(Sprite)))
                    Add(rig[f.Name]);
            if (config["spriteSwaps"] is JArray swaps)
                foreach (var s in swaps)
                {
                    Add(s["asleep"]);
                    Add(s["awake"]);
                    Add(s["attacking"]);
                    if (s["stages"] is JArray stages) foreach (var st in stages) Add(st);
                }

            var result = new Dictionary<string, Sprite>();
            var copied = new List<(string reference, string assetPath)>();
            string artFolder = $"{RigConfigJson.ArtRoot}/{name}";

            foreach (var reference in refs)
            {
                if (RigConfigJson.IsProjectRef(reference))
                {
                    var sprite = RigConfigJson.LoadProjectSprite(reference);
                    if (sprite == null) throw new ConfigImportException($"sprite '{reference}': no hay ningún sprite ahí.");
                    result[reference] = sprite;
                    continue;
                }

                if (reference.Contains("..") || Path.IsPathRooted(reference))
                    throw new ConfigImportException($"sprite '{reference}': ruta no válida (relativa al bundle, sin '..').");

                string file = Path.Combine(source, reference);
                if (!File.Exists(file)) throw new ConfigImportException($"sprite '{reference}': el bundle no trae ese archivo.");

                string assetPath = $"{artFolder}/{reference}";
                string target = Path.GetFullPath(assetPath);
                if (!string.Equals(Path.GetFullPath(file), target, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target, true);
                }

                copied.Add((reference, assetPath));
            }

            if (copied.Count == 0) return result;

            // El config viaja con sus PNG: reimportar la carpeta después encuentra las mismas rutas.
            string configTarget = Path.GetFullPath($"{artFolder}/{RigConfigJson.ConfigFile}");
            string configSource = Path.Combine(source, RigConfigJson.ConfigFile);
            if (File.Exists(configSource) && !string.Equals(Path.GetFullPath(configSource), configTarget, StringComparison.OrdinalIgnoreCase))
                File.Copy(configSource, configTarget, true);

            AssetDatabase.Refresh();

            float defaultPpu = config.Value<float?>("pixelsPerUnit") ?? 100f;
            var settings = config["sprites"] as JObject;
            foreach (var (reference, assetPath) in copied)
            {
                var entry = settings?[reference] as JObject;
                float ppu = entry?.Value<float?>("pixelsPerUnit") ?? defaultPpu;
                var pivot = ConfigJson.ReadVector2(entry?["pivot"], new Vector2(0.5f, 0.5f));
                ConfigureSprite(assetPath, ppu, pivot);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null) throw new ConfigImportException($"sprite '{reference}': '{assetPath}' no se importó como sprite.");
                result[reference] = sprite;
            }

            report.Add(true, $"{artFolder}/ ({copied.Count} PNG de las piezas, importados del zip)");
            return result;
        }

        /// <summary>Sprite único con el ppu y el pivote (normalizado) del config. Sólo reimporta si cambia algo.</summary>
        private static void ConfigureSprite(string assetPath, float ppu, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new ConfigImportException($"'{assetPath}' no es una imagen.");

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            bool same = importer.textureType == TextureImporterType.Sprite &&
                        importer.spriteImportMode == SpriteImportMode.Single &&
                        Mathf.Approximately(s.spritePixelsPerUnit, ppu) &&
                        s.spriteAlignment == (int)SpriteAlignment.Custom &&
                        Vector2.Distance(s.spritePivot, pivot) < 0.0001f;
            if (same) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.ReadTextureSettings(s);
            s.spritePixelsPerUnit = ppu;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            importer.SetTextureSettings(s);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static Sprite SpriteFor(JToken token, Dictionary<string, Sprite> sprites)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            string reference = token.Value<string>();
            if (string.IsNullOrEmpty(reference)) return null;
            return sprites.TryGetValue(reference.Replace('\\', '/'), out var sprite) ? sprite : null;
        }

        // ============================================================ jefe

        /// <summary>Ataques + definición con el importador de jefes de siempre (sin 'body': el cuerpo es el rig).</summary>
        private static BossDefinition ImportBossConfig(string path, string source, ConfigImportReport report)
        {
            var bossJson = JObject.Parse(File.ReadAllText(path));
            if (bossJson.Remove("body"))
                report.Warnings.Add("boss-config.body se ignora en un jefe por partes: su cuerpo es el rig.");

            string displayName = ConfigJson.RequireString(bossJson, "displayName", "BossConfig");
            string slug = BossConfigImporter.Slugify(displayName);

            // El arte de los ataques ('artAssets', carpetas relativas) va a la misma carpeta que en un
            // jefe de sprites, para que BossConfigImporter lo encuentre igual.
            string art = Path.Combine(source, "Art");
            if (Directory.Exists(art))
            {
                int copied = 0, removed = 0;
                BossBundleImporter.Sync(art, Path.Combine(Path.GetFullPath(BossConfigImporter.SourceFolderFor(slug)), "Art"),
                                        ref copied, ref removed);
                AssetDatabase.Refresh();
            }

            BossConfigImporter.Import(bossJson, report);
            return AssetDatabase.LoadAssetAtPath<BossDefinition>($"Assets/Resources/Bosses/Boss_{slug}.asset");
        }

        private static void LinkBoss(GameObject root, Transform visual, BossDefinition definition, JObject bossBody,
                                     bool isNew, ConfigImportReport report)
        {
            var controller = root.GetComponent<BossController>();
            if (controller == null) return;

            var fields = new BossAuthoring.Fields(controller);
            if (definition != null) fields.SetObject("definition", definition);
            else if (isNew) report.Warnings.Add($"Sin {RigConfigJson.BossConfigFile}: asigna la BossDefinition del BossController a mano.");

            if (bossBody["bodyRenderer"] != null)
            {
                var t = RigConfigJson.Find(visual, bossBody.Value<string>("bodyRenderer"));
                fields.SetObject("bodyRenderer", t != null ? t.GetComponent<SpriteRenderer>() : null);
            }

            var feet = root.transform.Find(RigConfigJson.FeetName);
            if (feet != null) fields.SetObject("feet", feet);

            fields.Apply();
        }

        private static void WriteLimbs(BossLimbs limbs, Transform visual, JObject json)
        {
            Object[] Paths(string key) =>
                (json[key] as JArray ?? new JArray()).Select(t => (Object)RigConfigJson.Find(visual, t.Value<string>())).ToArray();

            var fields = new BossAuthoring.Fields(limbs);
            if (json["limbs"] != null) fields.SetObjectArray("limbs", Paths("limbs"));
            if (json["orbiters"] != null) fields.SetObjectArray("orbiters", Paths("orbiters"));
            if (json["grips"] != null) fields.SetObjectArray("grips", Paths("grips"));
            if (json["orbiterRadius"] != null) fields.Set("orbiterRadius", json.Value<float>("orbiterRadius"));
            if (json["contactRadius"] != null) fields.Set("contactRadius", json.Value<float>("contactRadius"));
            fields.Apply();
        }

        // ============================================================ raíz y jerarquía

        private static void ApplyRoot(GameObject root, JObject json, ConfigImportReport report)
        {
            if (json == null) return;
            root.transform.localScale = RigConfigJson.ReadScale(json["scale"], root.transform.localScale);
            if (json["tag"] != null) BossAuthoring.TrySetTag(root, json.Value<string>("tag"));
            if (json["layer"] != null)
            {
                int layer = LayerMask.NameToLayer(json.Value<string>("layer"));
                if (layer >= 0) root.layer = layer;
                else report.Warnings.Add($"root.layer: la capa '{json.Value<string>("layer")}' no existe; se queda la que había.");
            }

            // Los pies de un jefe: un vacío hijo de la raíz (el pivote va en el centro del cuerpo).
            if (json["feet"] != null)
            {
                var feet = root.transform.Find(RigConfigJson.FeetName);
                if (feet == null)
                {
                    feet = new GameObject(RigConfigJson.FeetName).transform;
                    feet.SetParent(root.transform, false);
                }

                feet.localPosition = (Vector3)ConfigJson.ReadVector2(json["feet"], Vector2.zero);
            }
        }

        private static void ApplyCollider(GameObject root, JObject json)
        {
            if (json == null) return;
            string type = json.Value<string>("type") ?? "box";
            Type wanted = type == "capsule" ? typeof(CapsuleCollider2D) : type == "circle" ? typeof(CircleCollider2D) : typeof(BoxCollider2D);

            // Un solo collider del cuerpo, del tipo pedido.
            foreach (var other in root.GetComponents<Collider2D>())
                if (other.GetType() != wanted) Object.DestroyImmediate(other, true);

            var c = (Collider2D)root.GetComponent(wanted);
            if (c == null) c = (Collider2D)root.AddComponent(wanted);
            c.offset = ConfigJson.ReadVector2(json["offset"], c.offset);
            c.isTrigger = json.Value<bool?>("isTrigger") ?? false;
            switch (c)
            {
                case BoxCollider2D box:
                    box.size = ConfigJson.ReadVector2(json["size"], box.size);
                    break;
                case CapsuleCollider2D capsule:
                    capsule.size = ConfigJson.ReadVector2(json["size"], capsule.size);
                    capsule.direction = json.Value<string>("direction") == "Horizontal"
                        ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
                    break;
                case CircleCollider2D circle:
                    circle.radius = json.Value<float?>("radius") ?? circle.radius;
                    break;
            }
        }

        private static void ApplyCorpse(GameObject root, JObject json)
        {
            var corpse = root.GetComponent<Corpse>();
            if (json == null || corpse == null) return;
            var fields = new BossAuthoring.Fields(corpse);
            if (json["linger"] != null) fields.Set("linger", json.Value<float>("linger"));
            if (json["fadeDuration"] != null) fields.Set("fadeDuration", json.Value<float>("fadeDuration"));
            fields.Apply();
        }

        private static void ApplyTransform(Transform t, JObject json)
        {
            if (t == null || json == null) return;
            var p = ConfigJson.ReadVector2(json["position"], t.localPosition);
            t.localPosition = new Vector3(p.x, p.y, 0f);
            if (json["rotation"] != null) t.localRotation = Quaternion.Euler(0f, 0f, json.Value<float>("rotation"));
            t.localScale = RigConfigJson.ReadScale(json["scale"], t.localScale);
        }

        /// <summary>
        /// Deja bajo Visual exactamente los nodos del JSON: reutiliza los que ya estaban (por ruta),
        /// crea los que faltan, los cuelga de su padre en el orden de la lista y borra el resto
        /// (avisando: el JSON es el cuerpo).
        /// </summary>
        private static void BuildHierarchy(Transform visual, List<Node> nodes, Dictionary<string, Sprite> sprites,
                                           ConfigImportReport report)
        {
            var existing = new Dictionary<string, Transform>();
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
            {
                if (t == visual) continue;
                string path = RigConfigJson.PathOf(visual, t);
                if (!existing.ContainsKey(path)) existing[path] = t;
            }

            var byPath = new Dictionary<string, Transform>();
            foreach (var n in nodes)
            {
                if (!existing.TryGetValue(n.Path, out var t))
                {
                    t = new GameObject(n.Name).transform;
                    t.SetParent(visual, false);
                }

                byPath[n.Path] = t;
            }

            foreach (var n in nodes)
            {
                var t = byPath[n.Path];
                var parent = n.Parent == "" ? visual : byPath[n.Parent];
                if (t.parent != parent) t.SetParent(parent, false);
                t.SetAsLastSibling();
                ApplyTransform(t, n.Json);
                t.gameObject.SetActive(n.Json.Value<bool?>("active") ?? true);
                ApplyRenderer(t, n, sprites, report);
            }

            var keep = new HashSet<Transform>(byPath.Values);
            var extra = visual.GetComponentsInChildren<Transform>(true)
                .Where(t => t != visual && !keep.Contains(t) && (t.parent == visual || keep.Contains(t.parent)))
                .ToList();
            foreach (var t in extra)
            {
                report.Warnings.Add($"Visual/{RigConfigJson.PathOf(visual, t)} no está en el JSON: borrado.");
                Object.DestroyImmediate(t.gameObject, true);
            }
        }

        private static void ApplyRenderer(Transform t, Node n, Dictionary<string, Sprite> sprites, ConfigImportReport report)
        {
            var renderer = t.GetComponent<SpriteRenderer>();
            if (!n.Json.ContainsKey("sprite"))
            {
                if (renderer != null)
                {
                    Object.DestroyImmediate(renderer, true);
                    report.Warnings.Add($"{n.Path}: sin 'sprite' en el JSON; SpriteRenderer quitado.");
                }

                return;
            }

            if (renderer == null) renderer = t.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteFor(n.Json["sprite"], sprites);
            renderer.sortingOrder = n.Json.Value<int?>("sortingOrder") ?? 0;
            renderer.sortingLayerName = n.Json.Value<string>("sortingLayer") ?? "Default";
            renderer.color = ConfigJson.ReadColor(n.Json["color"], Color.white);
            renderer.flipX = n.Json.Value<bool?>("flipX") ?? false;
            renderer.flipY = n.Json.Value<bool?>("flipY") ?? false;
        }

        // ============================================================ rig

        private static void WriteRig(ProceduralRig rig, Transform visual, List<Node> nodes, JObject config,
                                     Dictionary<string, Sprite> sprites, ConfigImportReport report)
        {
            rig.body = visual;

            // Partes: la lista se rehace en el orden del JSON; los datos de una parte que ya existía
            // son la base (una clave ausente conserva su valor).
            var old = new Dictionary<Transform, RigPart>();
            foreach (var p in rig.parts)
                if (p != null && p.part != null && !old.ContainsKey(p.part)) old[p.part] = p;

            var parts = new List<RigPart>();
            foreach (var n in nodes.Where(n => n.RigPart))
            {
                var t = RigConfigJson.Find(visual, n.Path);
                if (!old.TryGetValue(t, out var part)) part = new RigPart();
                part.part = t;
                ReadPart(part, n.Json);
                parts.Add(part);
            }

            rig.parts = parts;

            if (config["rig"] is JObject block) WriteRigFields(rig, visual, block, sprites, report);

            if (config["spriteSwaps"] is JArray swaps)
            {
                rig.spriteSwaps = new List<RigSpriteSwap>();
                for (int i = 0; i < swaps.Count; i++)
                {
                    var s = ConfigJson.AsObject(swaps[i], $"spriteSwaps[{i}]");
                    var owner = RigConfigJson.Find(visual, s.Value<string>("part"));
                    var renderer = owner != null ? owner.GetComponent<SpriteRenderer>() : null;
                    if (renderer == null)
                    {
                        report.Warnings.Add($"spriteSwaps[{i}]: '{s.Value<string>("part")}' no tiene sprite; se omite.");
                        continue;
                    }

                    rig.spriteSwaps.Add(new RigSpriteSwap
                    {
                        renderer = renderer,
                        asleep = SpriteFor(s["asleep"], sprites),
                        awake = SpriteFor(s["awake"], sprites),
                        attacking = SpriteFor(s["attacking"], sprites),
                        stages = (s["stages"] as JArray ?? new JArray()).Select(st => SpriteFor(st, sprites)).ToList(),
                    });
                }
            }

            if (config["gestures"] is JArray gestures)
            {
                // El sonido de un gesto se asigna en Unity (pestaña Sounds), no viaja en el JSON: se
                // conserva por nombre al rehacer la lista, o cada reimport lo borraría.
                var oldGestures = rig.gestures ?? new List<RigGesture>();
                rig.gestures = new List<RigGesture>();
                for (int i = 0; i < gestures.Count; i++)
                {
                    var g = ConfigJson.AsObject(gestures[i], $"gestures[{i}]");
                    var gesture = new RigGesture
                    {
                        name = ConfigJson.RequireString(g, "name", $"gestures[{i}]"),
                        windup = Poses(g["windup"] as JArray, visual),
                        strike = Poses(g["strike"] as JArray, visual),
                    };
                    gesture.strikeTime = g.Value<float?>("strikeTime") ?? gesture.strikeTime;
                    gesture.recover = g.Value<float?>("recover") ?? gesture.recover;
                    gesture.shake = g.Value<float?>("shake") ?? gesture.shake;

                    // Efectos: como las poses, vienen del JSON; sin la clave se conservan los que hubiera.
                    var previous = oldGestures.Find(o => o != null && o.name == gesture.name);
                    if (g["phases"] is JArray phases) gesture.fx = GestureFx(phases, visual, $"gestures[{i}]", report);
                    else if (previous != null) gesture.fx = previous.fx ?? new List<RigFx>();

                    if (previous != null)
                    {
                        gesture.sound = previous.sound;
                        gesture.soundMoment = previous.soundMoment;
                    }

                    rig.gestures.Add(gesture);
                }
            }

            EditorUtility.SetDirty(rig);
        }

        private static void ReadPart(RigPart part, JObject json)
        {
            part.pivot = ConfigJson.ReadVector2(json["pivot"], part.pivot);
            part.center = ConfigJson.ReadVector2(json["center"], part.center);
            part.pointAngle = json.Value<float?>("pointAngle") ?? part.pointAngle;
            part.leanFollow = json.Value<float?>("leanFollow") ?? part.leanFollow;
            part.breaksOnDeath = json.Value<bool?>("breaksOnDeath") ?? part.breaksOnDeath;

            if (!(json["sway"] is JObject sway)) return;
            part.swayMove = ConfigJson.ReadVector2(sway["move"], part.swayMove);
            part.swayRotate = sway.Value<float?>("rotate") ?? part.swayRotate;
            part.swayScale = ConfigJson.ReadVector2(sway["scale"], part.swayScale);
            part.swaySpeed = sway.Value<float?>("speed") ?? part.swaySpeed;
            part.swayPhase = sway.Value<float?>("phase") ?? part.swayPhase;
        }

        /// <summary><c>phases[].fx</c> → la lista plana de <see cref="RigFx"/> del gesto (cada uno con su fase).</summary>
        private static List<RigFx> GestureFx(JArray phases, Transform visual, string ctx, ConfigImportReport report)
        {
            var list = new List<RigFx>();
            for (int p = 0; p < phases.Count; p++)
            {
                if (!(phases[p] is JObject phaseJson)) continue;
                string phaseName = phaseJson.Value<string>("phase");
                if (!RigConfigJson.TryFxPhase(phaseName, out var phase))
                {
                    report.Warnings.Add($"{ctx}.phases[{p}]: fase '{phaseName}' desconocida (windup / strike / recover); se omite.");
                    continue;
                }

                if (!(phaseJson["fx"] is JArray items)) continue;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!(items[i] is JObject item)) continue;
                    string where = $"{ctx}.phases[{p}].fx[{i}]";

                    // Tras BuildGestureArt, 'fx' es la ruta de un prefab (o null si su arte no se pudo construir).
                    string prefabPath = item["fx"]?.Type == JTokenType.String ? item.Value<string>("fx") : null;
                    var prefab = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                    {
                        report.Warnings.Add($"{where}: sin prefab de efecto ('{prefabPath}'); se omite.");
                        continue;
                    }

                    var fx = new RigFx { prefab = prefab, phase = phase };
                    fx.at = Mathf.Clamp01(item.Value<float?>("at") ?? 0f);
                    string anchorName = item.Value<string>("anchor") ?? "part";
                    if (!RigConfigJson.TryFxAnchor(anchorName, out fx.anchor))
                    {
                        report.Warnings.Add($"{where}: ancla '{anchorName}' desconocida; va al cuerpo.");
                        fx.anchor = RigFxAnchor.Body;
                    }

                    string partPath = item.Value<string>("part");
                    if (fx.anchor == RigFxAnchor.Part)
                    {
                        fx.part = string.IsNullOrEmpty(partPath) ? null : RigConfigJson.Find(visual, partPath);
                        if (fx.part == null)
                        {
                            report.Warnings.Add($"{where}: la pieza '{partPath}' no existe; el efecto va al cuerpo.");
                            fx.anchor = RigFxAnchor.Body;
                        }
                    }

                    fx.offset = ConfigJson.ReadVector2(item["offset"], Vector2.zero);
                    fx.scale = Mathf.Max(0.01f, item.Value<float?>("scale") ?? 1f);
                    fx.followsPart = item.Value<bool?>("followsPart") ?? true;
                    fx.loop = item.Value<bool?>("loop") ?? false;
                    list.Add(fx);
                }
            }

            return list;
        }

        private static List<RigPose> Poses(JArray array, Transform visual)
        {
            var poses = new List<RigPose>();
            if (array == null) return poses;
            foreach (var token in array)
            {
                var p = token as JObject;
                if (p == null) continue;
                string path = p.Value<string>("part");
                poses.Add(new RigPose
                {
                    part = string.IsNullOrEmpty(path) ? null : RigConfigJson.Find(visual, path),
                    move = ConfigJson.ReadVector2(p["move"], Vector2.zero),
                    rotate = p.Value<float?>("rotate") ?? 0f,
                    scale = ConfigJson.ReadVector2(p["scale"], Vector2.zero),
                });
            }

            return poses;
        }

        /// <summary>El bloque <c>rig</c>: cada clave es un campo de ProceduralRig con su nombre C#.</summary>
        private static void WriteRigFields(ProceduralRig rig, Transform visual, JObject block,
                                           Dictionary<string, Sprite> sprites, ConfigImportReport report)
        {
            foreach (var prop in block.Properties())
            {
                var f = typeof(ProceduralRig).GetField(prop.Name, BindingFlags.Instance | BindingFlags.Public);
                if (f == null || RigConfigJson.StructuredRigFields.Contains(prop.Name))
                {
                    report.UnresolvedParams.Add($"rig.{prop.Name}: no es un campo de ProceduralRig.");
                    continue;
                }

                var token = prop.Value;
                var ft = f.FieldType;
                string ctx = $"rig.{prop.Name}";

                if (ft == typeof(Transform))
                    f.SetValue(rig, token.Type == JTokenType.Null ? null : RigConfigJson.Find(visual, token.Value<string>()));
                else if (ft == typeof(List<Transform>))
                    f.SetValue(rig, (token as JArray ?? new JArray())
                        .Select(t => t.Type == JTokenType.Null ? null : RigConfigJson.Find(visual, t.Value<string>())).ToList());
                else if (ft == typeof(Sprite)) f.SetValue(rig, SpriteFor(token, sprites));
                else if (token.Type == JTokenType.Null) continue;
                else if (ft == typeof(float)) f.SetValue(rig, token.Value<float>());
                else if (ft == typeof(int)) f.SetValue(rig, token.Value<int>());
                else if (ft == typeof(bool)) f.SetValue(rig, token.Value<bool>());
                else if (ft == typeof(string)) f.SetValue(rig, token.Value<string>());
                else if (ft == typeof(Vector2)) f.SetValue(rig, ConfigJson.ReadVector2(token, (Vector2)f.GetValue(rig)));
                else if (ft == typeof(Color)) f.SetValue(rig, ConfigJson.ReadColor(token, (Color)f.GetValue(rig)));
                else if (ft == typeof(LayerMask)) f.SetValue(rig, ConfigJson.ReadLayerMask(token, (LayerMask)f.GetValue(rig), ctx));
                else report.UnresolvedParams.Add($"{ctx}: tipo {ft.Name} no soportado por el importador.");
            }
        }
    }
}
