using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedMagic.Bosses;
using RedMagic.Combat;
using RedMagic.Gameplay.Movement;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Escribe el <c>rig-config.json</c> (<c>docs/schemas/rig-config.schema.json</c>) de un prefab por
    /// partes que ya existe: la jerarquía bajo <c>Visual</c>, cada <see cref="RigPart"/>, todo
    /// <see cref="ProceduralRig"/>, los sprites por estado, los gestos, <see cref="BossLimbs"/> y lo
    /// que el cuerpo necesita de la raíz (escala, collider, cadáver, pila de jefe).
    ///
    /// Es la otra mitad de <see cref="RigBundleImporter"/>: exportar e importar un prefab da el mismo
    /// <see cref="ProceduralRig"/> (lo comprueba <see cref="RigConfigRoundTrip"/>). Sirve también
    /// para llevar a la web lo afinado en el Inspector antes de reimportar desde allí.
    ///
    /// Los sprites se escriben como rutas de asset del proyecto: el JSON exportado se reimporta tal
    /// cual, sin copiar ni tocar el arte.
    /// </summary>
    public static class RigConfigExporter
    {
        [MenuItem("Assets/Exportar rig a JSON", false, 2000)]
        private static void ExportFromContext() => ExportSelected();

        [MenuItem("Assets/Exportar rig a JSON", true)]
        private static bool CanExportFromContext() => SelectedRigPrefabs().Count > 0;

        /// <summary>Los prefabs seleccionados en el Project; sin selección, pide uno con el explorador.</summary>
        [MenuItem("Tools/Web/Rig/Exportar rig a JSON…")]
        private static void ExportSelected()
        {
            var prefabs = SelectedRigPrefabs();
            if (prefabs.Count == 0)
            {
                string picked = EditorUtility.OpenFilePanel("Prefab por partes (con ProceduralRig)",
                                                            Path.Combine(Application.dataPath, "Prefabs"), "prefab");
                if (string.IsNullOrEmpty(picked)) return;

                string full = Path.GetFullPath(picked).Replace('\\', '/');
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/') + "/";
                string asset = full.StartsWith(project, StringComparison.OrdinalIgnoreCase) ? full.Substring(project.Length) : null;
                var go = asset != null ? AssetDatabase.LoadAssetAtPath<GameObject>(asset) : null;
                if (go == null || go.GetComponent<ProceduralRig>() == null)
                {
                    EditorUtility.DisplayDialog("Exportar rig", "Ese archivo no es un prefab del proyecto con ProceduralRig.", "OK");
                    return;
                }

                prefabs.Add(asset);
            }

            string folder = EditorUtility.SaveFolderPanel("Carpeta para los rig-config", Application.dataPath, "");
            if (string.IsNullOrEmpty(folder)) return;

            var log = new StringBuilder();
            foreach (var path in prefabs)
                log.AppendLine(ExportToFile(path, Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(path)}.rig-config.json")));

            Debug.Log($"[RigConfigExporter]\n{log}");
            EditorUtility.DisplayDialog("Exportar rig", log.ToString(), "OK");
        }

        private static List<string> SelectedRigPrefabs()
        {
            var prefabs = new List<string>();
            foreach (var go in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(go);
                if (path.EndsWith(".prefab", StringComparison.Ordinal) && go.GetComponent<ProceduralRig>() != null)
                    prefabs.Add(path);
            }

            return prefabs;
        }

        /// <summary>Exporta un prefab a un archivo. Punto de entrada también para <c>unity command eval</c>.</summary>
        public static string ExportToFile(string prefabPath, string outPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return $"x {prefabPath}: no existe.";

            var warnings = new List<string>();
            var json = Export(prefab, prefabPath, warnings);
            if (json == null) return $"x {prefabPath}: sin ProceduralRig o sin 'Visual'.";

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.WriteAllText(outPath, json.ToString(Formatting.Indented) + "\n");

            var sb = new StringBuilder($"+ {prefabPath} → {outPath}");

            // Los números de juego del enemigo viajan al lado, en su propio EnemyConfig: el
            // importador lo busca como '<nombre>.enemy-config.json' junto al rig.
            var enemy = EnemyConfig(prefab, json.Value<string>("name"));
            if (enemy != null)
            {
                string enemyPath = RigConfigJson.SiblingPath(outPath, RigConfigJson.EnemyConfigFile);
                File.WriteAllText(enemyPath, enemy.ToString(Formatting.Indented) + "\n");
                sb.Append($"\n+ tuning → {enemyPath}");
            }
            foreach (var w in warnings) sb.Append($"\n  · {w}");
            return sb.ToString();
        }

        /// <summary>El JSON del prefab, o null si no es un prefab por partes.</summary>
        public static JObject Export(GameObject root, string prefabPath, List<string> warnings)
        {
            var rig = root.GetComponent<ProceduralRig>();
            var visual = rig != null ? (rig.body != null ? rig.body : root.transform.Find(ProceduralRigTool.VisualName)) : null;
            if (rig == null || visual == null) return null;

            bool boss = root.GetComponent<BossController>() != null;

            var json = new JObject
            {
                ["kind"] = boss ? "boss" : "enemy",
                ["driver"] = Driver(root),
                ["name"] = root.name,
                ["root"] = Root(root),
            };

            var collider = Collider(root);
            if (collider != null) json["collider"] = collider;

            var corpse = root.GetComponent<Corpse>();
            if (corpse != null)
            {
                var so = new SerializedObject(corpse);
                json["corpse"] = new JObject
                {
                    ["linger"] = RigConfigJson.Num(so.FindProperty("linger").floatValue),
                    ["fadeDuration"] = RigConfigJson.Num(so.FindProperty("fadeDuration").floatValue),
                };
            }

            json["visual"] = new JObject
            {
                ["position"] = RigConfigJson.V2(visual.localPosition),
                ["rotation"] = RigConfigJson.Num(RigConfigJson.Angle(visual)),
                ["scale"] = RigConfigJson.Scale(visual.localScale),
            };

            json["parts"] = Parts(rig, visual, warnings);
            json["rig"] = RigBlock(rig, visual, warnings);
            json["spriteSwaps"] = Swaps(rig, visual, warnings);
            json["gestures"] = Gestures(rig, visual);
            json["idleGestures"] = IdleGestures(rig);

            var limbs = root.GetComponent<BossLimbs>();
            if (limbs != null) json["bossLimbs"] = Limbs(limbs, visual);
            if (boss) json["bossBody"] = BossBody(root, visual);

            return json;
        }

        // ============================================================ raíz

        /// <summary>El motor del prefab: hay uno solo (RigDriverCode).</summary>
        private static string Driver(GameObject root) => "code";

        private static JObject Root(GameObject root)
        {
            var o = new JObject
            {
                ["scale"] = RigConfigJson.Scale(root.transform.localScale),
                ["tag"] = root.tag,
                ["layer"] = LayerMask.LayerToName(root.layer),
            };

            var controller = root.GetComponent<BossController>();
            var feet = controller != null
                ? new SerializedObject(controller).FindProperty("feet").objectReferenceValue as Transform
                : null;
            if (feet != null) o["feet"] = RigConfigJson.V2(feet.localPosition);
            return o;
        }

        private static JObject Collider(GameObject root)
        {
            var c = root.GetComponent<Collider2D>();
            JObject o;
            switch (c)
            {
                case BoxCollider2D box:
                    o = new JObject { ["type"] = "box", ["size"] = RigConfigJson.V2(box.size) };
                    break;
                case CapsuleCollider2D capsule:
                    o = new JObject
                    {
                        ["type"] = "capsule",
                        ["size"] = RigConfigJson.V2(capsule.size),
                        ["direction"] = capsule.direction.ToString(),
                    };
                    break;
                case CircleCollider2D circle:
                    o = new JObject { ["type"] = "circle", ["radius"] = RigConfigJson.Num(circle.radius) };
                    break;
                default:
                    return null;
            }

            o["offset"] = RigConfigJson.V2(c.offset);
            if (c.isTrigger) o["isTrigger"] = true;
            return o;
        }

        // ============================================================ piezas

        /// <summary>
        /// Primero las partes del rig en el orden de <c>ProceduralRig.parts</c> (el importador rehace
        /// la lista en este orden), después el resto de nodos bajo Visual (bocas, agarres, pivotes
        /// vacíos) en orden de jerarquía, con <c>rigPart: false</c>.
        /// </summary>
        private static JArray Parts(ProceduralRig rig, Transform visual, List<string> warnings)
        {
            var parts = new JArray();
            var done = new HashSet<Transform>();

            foreach (var p in rig.parts)
            {
                if (p == null || p.part == null) continue;
                if (!p.part.IsChildOf(visual) || p.part == visual)
                {
                    warnings.Add($"parte '{p.part.name}' fuera de Visual: no se exporta.");
                    continue;
                }

                if (!done.Add(p.part)) continue;
                parts.Add(Node(visual, p.part, p));
            }

            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
            {
                if (t == visual || done.Contains(t)) continue;
                done.Add(t);
                parts.Add(Node(visual, t, null));
            }

            return parts;
        }

        private static JObject Node(Transform visual, Transform t, RigPart data)
        {
            var o = new JObject
            {
                ["name"] = t.name,
                ["parent"] = t.parent == visual ? "" : RigConfigJson.PathOf(visual, t.parent),
            };

            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                o["sprite"] = RigConfigJson.SpriteRef(sr.sprite);
                o["sortingOrder"] = sr.sortingOrder;
                if (sr.sortingLayerName != "Default") o["sortingLayer"] = sr.sortingLayerName;
                if (sr.color != Color.white) o["color"] = RigConfigJson.Hex(sr.color);
                if (sr.flipX) o["flipX"] = true;
                if (sr.flipY) o["flipY"] = true;
            }

            o["position"] = RigConfigJson.V2(t.localPosition);
            o["rotation"] = RigConfigJson.Num(RigConfigJson.Angle(t));
            o["scale"] = RigConfigJson.Scale(t.localScale);
            if (!t.gameObject.activeSelf) o["active"] = false;

            if (data == null)
            {
                o["rigPart"] = false;
                return o;
            }

            o["pivot"] = RigConfigJson.V2(data.pivot);
            o["center"] = RigConfigJson.V2(data.center);
            o["pointAngle"] = RigConfigJson.Num(data.pointAngle);
            o["leanFollow"] = RigConfigJson.Num(data.leanFollow);
            o["sway"] = new JObject
            {
                ["move"] = RigConfigJson.V2(data.swayMove),
                ["rotate"] = RigConfigJson.Num(data.swayRotate),
                ["scale"] = RigConfigJson.V2(data.swayScale),
                ["speed"] = RigConfigJson.Num(data.swaySpeed),
                ["phase"] = RigConfigJson.Num(data.swayPhase),
            };
            o["breaksOnDeath"] = data.breaksOnDeath;
            return o;
        }

        // ============================================================ rig

        /// <summary>Todos los campos de ProceduralRig (menos los estructurados), por su nombre C#.</summary>
        private static JObject RigBlock(ProceduralRig rig, Transform visual, List<string> warnings)
        {
            var o = new JObject();
            foreach (var f in RigConfigJson.RigFields())
            {
                var value = f.GetValue(rig);
                var ft = f.FieldType;

                if (ft == typeof(float)) o[f.Name] = RigConfigJson.Num((float)value);
                else if (ft == typeof(int)) o[f.Name] = (int)value;
                else if (ft == typeof(bool)) o[f.Name] = (bool)value;
                else if (ft == typeof(string)) o[f.Name] = (string)value ?? "";
                else if (ft == typeof(Vector2)) o[f.Name] = RigConfigJson.V2((Vector2)value);
                else if (ft == typeof(Color)) o[f.Name] = RigConfigJson.Hex((Color)value);
                else if (ft == typeof(LayerMask)) o[f.Name] = RigConfigJson.LayerNames((LayerMask)value);
                else if (ft == typeof(Sprite)) o[f.Name] = RigConfigJson.SpriteRef((Sprite)value);
                else if (ft == typeof(Transform)) o[f.Name] = PathOrNull(visual, (Transform)value, f.Name, warnings);
                else if (ft == typeof(List<Transform>))
                {
                    var list = new JArray();
                    foreach (var t in (List<Transform>)value) list.Add(PathOrNull(visual, t, f.Name, warnings));
                    o[f.Name] = list;
                }
                else warnings.Add($"rig.{f.Name}: tipo {ft.Name} no soportado por el exportador.");
            }

            return o;
        }

        private static JToken PathOrNull(Transform visual, Transform t, string field, List<string> warnings)
        {
            if (t == null) return JValue.CreateNull();
            string path = RigConfigJson.PathOf(visual, t);
            if (path == null) warnings.Add($"{field}: '{t.name}' no cuelga de Visual; se exporta vacío.");
            return path ?? (JToken)JValue.CreateNull();
        }

        private static JArray Swaps(ProceduralRig rig, Transform visual, List<string> warnings)
        {
            var swaps = new JArray();
            foreach (var s in rig.spriteSwaps)
            {
                if (s == null || s.renderer == null) continue;
                var stages = new JArray();
                foreach (var sprite in s.stages) stages.Add(RigConfigJson.SpriteRef(sprite));
                var swap = new JObject
                {
                    ["part"] = PathOrNull(visual, s.renderer.transform, "spriteSwaps", warnings),
                    ["asleep"] = RigConfigJson.SpriteRef(s.asleep),
                    ["awake"] = RigConfigJson.SpriteRef(s.awake),
                    ["attacking"] = RigConfigJson.SpriteRef(s.attacking),
                    ["stages"] = stages,
                };

                // Estados con nombre (alerta, herido, muerto, por gesto): sólo si hay alguno.
                var named = new JArray();
                if (s.named != null)
                    foreach (var n in s.named)
                    {
                        if (n == null || n.sprite == null) continue;
                        var item = new JObject { ["state"] = RigConfigJson.NamedStateName(n.state) };
                        if (n.state == RigNamedState.Gesture)
                        {
                            item["gesture"] = n.gesture ?? "";
                            item["from"] = RigConfigJson.Num(n.showFrom);
                            item["to"] = RigConfigJson.Num(n.showTo);
                        }

                        item["sprite"] = RigConfigJson.SpriteRef(n.sprite);
                        named.Add(item);
                    }
                if (named.Count > 0) swap["named"] = named;
                swaps.Add(swap);
            }

            return swaps;
        }

        private static JArray Gestures(ProceduralRig rig, Transform visual)
        {
            var gestures = new JArray();
            foreach (var g in rig.gestures)
            {
                if (g == null) continue;
                // La línea de tiempo: pasos, sueltas y efectos (cada uno con su paso y su momento).
                var steps = new JArray();
                foreach (var s in g.steps)
                {
                    if (s == null) continue;
                    steps.Add(new JObject
                    {
                        ["name"] = s.name ?? "",
                        ["duration"] = RigConfigJson.Num(s.duration),
                        ["ease"] = RigConfigJson.EnumName(s.ease),
                        ["stretch"] = s.stretch,
                        ["shake"] = RigConfigJson.Num(s.shake),
                        ["poses"] = Poses(s.poses, visual),
                    });
                }

                var releases = new JArray();
                foreach (var r in g.releases)
                {
                    if (r == null) continue;
                    var release = new JObject { ["step"] = r.step, ["at"] = RigConfigJson.Num(r.at) };
                    if (r.point != null) release["point"] = RigConfigJson.PathOf(visual, r.point);
                    release["offset"] = RigConfigJson.V2(r.offset);
                    releases.Add(release);
                }

                var json = new JObject
                {
                    ["name"] = g.name,
                    ["steps"] = steps,
                    ["releases"] = releases,
                    ["recover"] = RigConfigJson.Num(g.recover),
                };
                var fx = Fx(g, visual);
                if (fx.Count > 0) json["fx"] = fx;
                gestures.Add(json);
            }

            return gestures;
        }

        private static JObject IdleGestures(ProceduralRig rig)
        {
            var idle = rig.idle ?? new RigIdleGestures();
            return new JObject
            {
                ["gestures"] = new JArray(idle.gestures ?? new List<string>()),
                ["interval"] = RigConfigJson.Num(idle.interval),
                ["chance"] = RigConfigJson.Num(idle.chance),
                ["windup"] = RigConfigJson.Num(idle.windup),
            };
        }

        /// <summary>Los efectos de un gesto (<c>fx[]</c>), cada uno con su paso y su momento.</summary>
        private static JArray Fx(RigGesture gesture, Transform visual)
        {
            var list = new JArray();
            if (gesture.fx == null) return list;

            foreach (var fx in gesture.fx)
            {
                if (fx == null) continue;
                var item = new JObject
                {
                    ["fx"] = fx.prefab != null ? AssetDatabase.GetAssetPath(fx.prefab) : null,
                    ["step"] = fx.step,
                    ["at"] = RigConfigJson.Num(fx.at),
                    ["anchor"] = RigConfigJson.FxAnchorName(fx.anchor),
                };
                if (fx.anchor == RigFxAnchor.Part)
                    item["part"] = fx.part != null ? RigConfigJson.PathOf(visual, fx.part) : null;
                item["offset"] = RigConfigJson.V2(fx.offset);
                item["scale"] = RigConfigJson.Num(fx.scale);
                item["followsPart"] = fx.followsPart;
                item["loop"] = fx.loop;
                item["untilStep"] = fx.untilStep;
                item["facing"] = RigConfigJson.EnumName(fx.facing);
                item["angle"] = RigConfigJson.Num(fx.angle);
                item["layer"] = RigConfigJson.EnumName(fx.layer);
                list.Add(item);
            }

            return list;
        }

        private static JArray Poses(List<RigPose> poses, Transform visual)
        {
            var list = new JArray();
            foreach (var p in poses)
            {
                if (p == null) continue;
                list.Add(new JObject
                {
                    ["part"] = p.part != null ? RigConfigJson.PathOf(visual, p.part) : null,
                    ["move"] = RigConfigJson.V2(p.move),
                    ["rotate"] = RigConfigJson.Num(p.rotate),
                    ["scale"] = RigConfigJson.V2(p.scale),
                });
            }

            return list;
        }

        // ============================================================ jefe

        private static JObject Limbs(BossLimbs limbs, Transform visual)
        {
            var so = new SerializedObject(limbs);

            JArray Paths(string field)
            {
                var list = new JArray();
                var prop = so.FindProperty(field);
                for (int i = 0; i < prop.arraySize; i++)
                    list.Add(RigConfigJson.PathOf(visual, prop.GetArrayElementAtIndex(i).objectReferenceValue as Transform));
                return list;
            }

            return new JObject
            {
                ["limbs"] = Paths("limbs"),
                ["orbiters"] = Paths("orbiters"),
                ["orbiterRadius"] = RigConfigJson.Num(so.FindProperty("orbiterRadius").floatValue),
                ["grips"] = Paths("grips"),
                ["contactRadius"] = RigConfigJson.Num(so.FindProperty("contactRadius").floatValue),
            };
        }

        /// <summary>Lo mismo que lee <c>BossBodyBuilder.AddBossStack</c> (mismas claves que <c>BossConfig.body</c>).</summary>
        private static JObject BossBody(GameObject root, Transform visual)
        {
            var o = new JObject();

            var health = root.GetComponent<Health>();
            if (health != null) o["health"] = RigConfigJson.Num(new SerializedObject(health).FindProperty("maxHealth").floatValue);

            var controller = new SerializedObject(root.GetComponent<BossController>());
            o["arenaHalfWidth"] = RigConfigJson.Num(controller.FindProperty("arenaHalfWidth").floatValue);
            o["arenaHeight"] = RigConfigJson.Num(controller.FindProperty("arenaHeight").floatValue);
            o["activationRadius"] = RigConfigJson.Num(controller.FindProperty("activationRadius").floatValue);
            o["contactDamage"] = RigConfigJson.Num(controller.FindProperty("contactDamage").floatValue);

            // La definición (fases y barajas) es un asset que ya existe: se referencia, no se copia.
            var definition = controller.FindProperty("definition").objectReferenceValue;
            if (definition != null) o["definition"] = AssetDatabase.GetAssetPath(definition);

            var body = controller.FindProperty("bodyRenderer").objectReferenceValue as SpriteRenderer;
            if (body != null) o["bodyRenderer"] = RigConfigJson.PathOf(visual, body.transform);

            var mover = root.GetComponent<Mover>();
            if (mover != null && mover.Behaviour != null)
            {
                var prop = new SerializedObject(mover).FindProperty("behaviour");
                var parameters = new JObject();
                var it = prop.Copy();
                var end = prop.GetEndProperty();
                bool enter = true;
                while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
                {
                    enter = false;
                    var value = Simple(it);
                    if (value != null) parameters[it.name] = value;
                }

                o["movement"] = new JObject { ["type"] = mover.Behaviour.GetType().Name, ["params"] = parameters };
            }

            return o;
        }

        // ============================================================ enemigo

        /// <summary>
        /// El EnemyConfig (<c>enemy-config.schema.json</c>) del EnemyStats del prefab: <c>tuning</c>
        /// entero por reflexión (mismos nombres que la clase) + <c>tier</c>. Sin <c>art</c>: en un
        /// enemigo por partes el arte es el rig-config. Null si no es un enemigo.
        /// </summary>
        public static JObject EnemyConfig(GameObject root, string name)
        {
            var stats = root.GetComponent<Enemies.EnemyStats>();
            if (stats == null || root.GetComponent<BossController>() != null) return null;

            var o = new JObject { ["kind"] = "enemy", ["enemyName"] = name };
            var dropper = root.GetComponent<Economy.CurrencyDropper>();
            if (dropper != null)
            {
                var tier = new SerializedObject(dropper).FindProperty("tier");
                o["tier"] = tier.enumNames[tier.enumValueIndex];
            }

            o["tuning"] = Fields(stats.Tuning);
            return o;
        }

        /// <summary>Campos públicos de una clase [Serializable] como JSON (los tipos que leen los importadores).</summary>
        private static JObject Fields(object obj)
        {
            var o = new JObject();
            foreach (var f in obj.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                var value = f.GetValue(obj);
                var ft = f.FieldType;
                JToken token = null;

                if (ft == typeof(float)) token = RigConfigJson.Num((float)value);
                else if (ft == typeof(int)) token = (int)value;
                else if (ft == typeof(bool)) token = (bool)value;
                else if (ft == typeof(string)) token = (string)value ?? "";
                else if (ft == typeof(Vector2)) token = RigConfigJson.V2((Vector2)value);
                else if (ft == typeof(Color)) token = RigConfigJson.Hex((Color)value);
                else if (ft.IsEnum) token = value.ToString();
                else if (ft == typeof(LayerMask)) token = RigConfigJson.LayerNames((LayerMask)value);
                else if (value is System.Collections.Generic.List<Enemies.EnemyDeckEntry> deck)
                {
                    // La baraja: cada carta apunta a su asset de ataque (ya existe en el proyecto).
                    var array = new JArray();
                    foreach (var entry in deck)
                    {
                        if (entry != null && entry.builtIn)
                        {
                            array.Add(new JObject
                            {
                                ["builtIn"] = true,
                                ["gesture"] = entry.gesture ?? "",
                                ["weight"] = RigConfigJson.Num(entry.weight),
                                ["minRange"] = RigConfigJson.Num(entry.minRange),
                                ["maxRange"] = RigConfigJson.Num(entry.maxRange),
                                ["targetHeight"] = entry.targetHeight.ToString(),
                            });
                            continue;
                        }

                        if (entry == null || entry.attack == null || !AssetDatabase.Contains(entry.attack)) continue;
                        array.Add(new JObject
                        {
                            ["attack"] = AssetDatabase.GetAssetPath(entry.attack),
                            ["minRange"] = RigConfigJson.Num(entry.minRange),
                            ["maxRange"] = RigConfigJson.Num(entry.maxRange),
                            ["targetHeight"] = entry.targetHeight.ToString(),
                        });
                    }

                    if (array.Count > 0) token = array;
                }
                else if (value is System.Collections.Generic.List<Enemies.EnemyDefence> defences)
                {
                    // Movimientos defensivos: siempre la lista (vacía = no tiene), como la lee EnemyDeckImporter.
                    var array = new JArray();
                    foreach (var d in defences)
                        if (d != null) array.Add(Fields(d));
                    token = array;
                }
                else if (ft.IsGenericType) continue;   // otras listas: no son parte del EnemyConfig
                else if (typeof(Object).IsAssignableFrom(ft))
                {
                    var asset = value as Object;
                    if (asset != null && AssetDatabase.Contains(asset)) token = AssetDatabase.GetAssetPath(asset);
                }
                else if (ft.IsClass && ft.IsDefined(typeof(SerializableAttribute), false) && value != null &&
                         ft != typeof(Audio.SoundCue))
                    token = Fields(value);

                if (token != null) o[f.Name] = token;
            }

            return o;
        }

        private static JToken Simple(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: return RigConfigJson.Num(p.floatValue);
                case SerializedPropertyType.Integer: return p.intValue;
                case SerializedPropertyType.Boolean: return p.boolValue;
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Vector2: return RigConfigJson.V2(p.vector2Value);
                case SerializedPropertyType.Color: return RigConfigJson.Hex(p.colorValue);
                case SerializedPropertyType.Enum:
                    return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumNames.Length ? p.enumNames[p.enumValueIndex] : null;
                case SerializedPropertyType.LayerMask: return RigConfigJson.LayerNames(p.intValue);
                default: return null;
            }
        }
    }
}
