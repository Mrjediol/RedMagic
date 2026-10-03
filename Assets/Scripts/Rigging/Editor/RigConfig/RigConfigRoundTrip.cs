using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using RedMagic.Bosses;
using RedMagic.Bosses.EditorTools;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Prueba de ida y vuelta del pipeline de rigs, sin Play: exporta los prefabs por partes de
    /// referencia a <c>docs/schemas/fixtures/rig/</c> (los fixtures del repo), los reimporta con otro
    /// nombre en una carpeta temporal y compara campo a campo (por reflexión) el
    /// <see cref="ProceduralRig"/>, la jerarquía bajo Visual y <see cref="BossLimbs"/>. Después borra
    /// lo importado. Un campo nuevo en ProceduralRig entra en la comparación solo.
    ///
    /// Los prefabs de referencia no llevan efectos de gesto (<see cref="RigFx"/>), así que hay un
    /// cuarto caso sintético: una copia del primero con un gesto que usa todas las anclas y fases, y
    /// sprites con nombre (alerta, herido, muerto, por gesto) en una de sus piezas.
    /// </summary>
    public static class RigConfigRoundTrip
    {
        private const string TempFolder = ProceduralEnemyPacks.Folder + "/_RoundTrip";
        private static readonly string[] Reference = { "IceRobot", "Turret", "IceBoss2" };

        public static string FixtureFolder =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "schemas", "fixtures", "rig"));

        [MenuItem("Tools/Web/Rig/Probar ida y vuelta (IceRobot · Turret · IceBoss2)")]
        private static void RunMenu()
        {
            string result = Run();
            Debug.Log(result);
            EditorUtility.DisplayDialog("Rig · ida y vuelta", result, "OK");
        }

        /// <summary>Punto de entrada también para <c>unity command eval</c>. Devuelve el informe.</summary>
        public static string Run()
        {
            var sb = new StringBuilder();
            int failures = 0;
            try
            {
                foreach (var name in Reference)
                {
                    string source = $"{ProceduralEnemyPacks.Folder}/{name}.prefab";
                    string fixture = Path.Combine(FixtureFolder, $"{name}.rig-config.json");
                    sb.AppendLine(RigConfigExporter.ExportToFile(source, fixture));

                    string copyName = name + "_RoundTrip";
                    string summary = RigBundleImporter.Import(fixture, new RigBundleImporter.Options
                    {
                        NameOverride = copyName,
                        PrefabFolder = TempFolder,
                    });

                    var original = AssetDatabase.LoadAssetAtPath<GameObject>(source);
                    var copy = AssetDatabase.LoadAssetAtPath<GameObject>($"{TempFolder}/{copyName}.prefab");
                    if (copy == null)
                    {
                        failures++;
                        sb.AppendLine($"x {name}: el import no produjo el prefab.\n{summary}");
                        continue;
                    }

                    var diffs = Compare(original, copy);
                    failures += diffs.Count > 0 ? 1 : 0;
                    sb.AppendLine(diffs.Count == 0 ? $"✓ {name}: ProceduralRig, jerarquía, BossLimbs y números de juego idénticos."
                                                   : $"x {name}: {diffs.Count} diferencias");
                    foreach (var d in diffs) sb.AppendLine($"    {d}");
                }

                failures += GestureFxCase(sb);
                failures += DeckCase(sb);
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempFolder);

                // El import de un jefe también deja sus visuales por jefe (Fx/Bosses/<Nombre>_RoundTrip).
                foreach (var name in Reference)
                    AssetDatabase.DeleteAsset($"Assets/Prefabs/Fx/Bosses/{name}_RoundTrip");
            }

            sb.Insert(0, failures == 0 ? "IDA Y VUELTA OK\n" : $"IDA Y VUELTA CON FALLOS ({failures})\n");
            return sb.ToString();
        }

        /// <summary>
        /// La baraja de cada enemigo por partes, ida y vuelta (lo que hace el puente web): cada asset de
        /// ataque → su carta JSON (<see cref="BossAttackExporter"/>) → escrita de nuevo en el MISMO asset
        /// con el importador de siempre. El asset tiene que quedar idéntico; si no, se restaura y se avisa.
        /// </summary>
        private static int DeckCase(StringBuilder sb)
        {
            int failures = 0, cards = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ProceduralEnemyPacks.Folder }))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (root == null || root.GetComponent<ProceduralRig>() == null) continue;

                var config = RigConfigExporter.EnemyConfig(root, root.name);
                if (config == null) continue;
                BossAttackExporter.FillDeck(root, config);
                if (!(config["attacks"] is JObject attacks) || attacks.Count == 0) continue;

                var before = new Dictionary<string, string>();
                string folder = null;
                foreach (var prop in attacks.Properties())
                {
                    string path = prop.Value.Value<string>("assetPath");
                    before[path] = EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<BossAttack>(path));
                    folder = folder ?? Path.GetDirectoryName(path).Replace('\\', '/');
                }

                var report = new ConfigImportReport(root.name);
                var diffs = new List<string>();
                try
                {
                    BossArtBuilder.ResolveArtRefs(attacks, null, report.Warnings, "attacks");
                    BossConfigImporter.WriteAttacks(attacks, BossConfigImporter.ResolveAttackTypes(attacks), folder,
                                                    id => id, null, report);
                }
                catch (Exception e) { diffs.Add($"excepción: {e.Message}"); }

                diffs.AddRange(report.UnresolvedParams);
                foreach (var pair in before)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<BossAttack>(pair.Key);
                    if (EditorJsonUtility.ToJson(asset) == pair.Value) continue;
                    diffs.Add($"{pair.Key}: el asset cambió al reescribirlo (restaurado).");
                    EditorJsonUtility.FromJsonOverwrite(pair.Value, asset);
                    EditorUtility.SetDirty(asset);
                }

                cards += before.Count;
                failures += diffs.Count > 0 ? 1 : 0;
                sb.AppendLine(diffs.Count == 0 ? $"✓ baraja de {root.name}: {before.Count} ataques, asset → carta → asset idénticos."
                                               : $"x baraja de {root.name}: {diffs.Count} diferencias");
                foreach (var d in diffs) sb.AppendLine($"    {d}");
            }

            AssetDatabase.SaveAssets();
            if (cards == 0) sb.AppendLine("· barajas: ningún enemigo por partes tiene ataques del catálogo.");
            return failures;
        }

        /// <summary>
        /// Efectos de gesto: una copia temporal del primer prefab de referencia con un gesto cargado de
        /// <see cref="RigFx"/> (cada ancla, cada fase, valores no por defecto) → JSON → reimport → comparar.
        /// El JSON va a la carpeta Temp del proyecto, no a los fixtures.
        /// </summary>
        private static int GestureFxCase(StringBuilder sb)
        {
            const string caseName = "GestureFx";
            string[] fxPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Fx" });
            if (fxPrefabs.Length == 0)
            {
                sb.AppendLine($"- {caseName}: no hay ningún prefab en Assets/Prefabs/Fx con el que probar; caso omitido.");
                return 0;
            }

            var fxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(fxPrefabs[0]));
            BossAuthoring.EnsureFolder(TempFolder);
            string sourcePath = $"{TempFolder}/{caseName}.prefab";
            if (!AssetDatabase.CopyAsset($"{ProceduralEnemyPacks.Folder}/{Reference[0]}.prefab", sourcePath))
            {
                sb.AppendLine($"x {caseName}: no se pudo copiar {Reference[0]}.");
                return 1;
            }

            var contents = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                var rig = contents.GetComponent<ProceduralRig>();
                Transform part = null;
                foreach (var p in rig.parts)
                    if (p?.part != null) { part = p.part; break; }

                rig.gestures.Add(new RigGesture
                {
                    name = "FxRoundTrip",
                    // Una línea de tiempo de tres pasos con dos sueltas (una desde una pieza) y un
                    // efecto en cada tramo, vuelta al reposo incluida (paso 3).
                    steps = new List<RigGestureStep>
                    {
                        new RigGestureStep { name = "Lanza", duration = 0.4f, ease = RigEase.Out, shake = 1f,
                                             poses = new List<RigPose> { new RigPose { part = part, move = new Vector2(0f, 0.5f), rotate = 180f } } },
                        new RigGestureStep { name = "Recoge", duration = 0.3f, ease = RigEase.In, stretch = true,
                                             poses = new List<RigPose> { new RigPose { part = part, rotate = 360f } } },
                        new RigGestureStep { name = "Dispara", duration = 0.1f, ease = RigEase.Linear },
                    },
                    releases = new List<RigRelease>
                    {
                        new RigRelease { step = 1, at = 0.5f, point = part, offset = new Vector2(0.2f, 0.1f) },
                        new RigRelease { step = 2, at = 1f },
                    },
                    fx = new List<RigFx>
                    {
                        new RigFx { prefab = fxPrefab, step = 0, at = 0f, anchor = RigFxAnchor.Part, part = part,
                                    offset = new Vector2(0.25f, -0.5f), scale = 1.5f, followsPart = true, loop = true, untilStep = 1,
                                    facing = RigFxFacing.Anchor, layer = RigFxLayer.Behind },
                        new RigFx { prefab = fxPrefab, step = 0, at = 0.7f, anchor = RigFxAnchor.Body,
                                    offset = new Vector2(0f, 1f), scale = 1f, followsPart = true, loop = false, angle = 30f },
                        new RigFx { prefab = fxPrefab, step = 2, at = 0.5f, anchor = RigFxAnchor.Muzzle,
                                    offset = Vector2.zero, scale = 0.75f, followsPart = false, loop = false },
                        new RigFx { prefab = fxPrefab, step = 3, at = 1f, anchor = RigFxAnchor.Feet,
                                    offset = new Vector2(-1f, 0f), scale = 2f, followsPart = true, loop = false, facing = RigFxFacing.World },
                    },
                });
                rig.idle = new RigIdleGestures { gestures = new List<string> { "FxRoundTrip" }, interval = 2.5f, chance = 0.35f, windup = 0.8f };

                // Andar: un pie (una pieza que cuelga de Visual) y una capa (otra pieza), con números que no son los de serie.
                var foot = rig.parts.Find(p => p?.part != null && p.part.parent == rig.body)?.part;
                var layered = rig.parts.Find(p => p?.part != null && p.part != foot)?.part;
                rig.gait = new RigGait
                {
                    feet = new List<RigGaitFoot> { new RigGaitFoot { part = foot, phase = 0.25f } },
                    stride = 0.9f, fullSpeed = 3f, minStride = 0.3f, duty = 0.6f, lift = 0.22f, footTilt = 11f,
                    bodyBob = 0.04f, maxSpeed = 5f, settleTime = 0.2f,
                    layers = new List<RigGaitLayer> { new RigGaitLayer { part = layered, phase = 0.5f, swing = new Vector2(0.1f, 0.02f), bob = -0.01f, rotate = 6f } },
                };

                // Sprites por estado con nombre: cada estado, sobre el renderer de esa pieza (su propio sprite vale).
                SpriteRenderer renderer = null;
                foreach (var p in rig.parts)
                {
                    renderer = p?.part != null ? p.part.GetComponent<SpriteRenderer>() : null;
                    if (renderer != null && renderer.sprite != null) break;
                }

                if (renderer != null && renderer.sprite != null)
                {
                    var swap = rig.spriteSwaps.Find(s => s != null && s.renderer == renderer);
                    if (swap == null)
                    {
                        swap = new RigSpriteSwap { renderer = renderer };
                        rig.spriteSwaps.Add(swap);
                    }

                    swap.named = new List<RigNamedSprite>
                    {
                        new RigNamedSprite { state = RigNamedState.Alert, sprite = renderer.sprite },
                        new RigNamedSprite { state = RigNamedState.Hurt, sprite = renderer.sprite },
                        new RigNamedSprite { state = RigNamedState.Dead, sprite = renderer.sprite },
                        new RigNamedSprite { state = RigNamedState.Gesture, gesture = "FxRoundTrip", sprite = renderer.sprite, showFrom = 0.75f, showTo = 2.5f },
                    };
                }

                PrefabUtility.SaveAsPrefabAsset(contents, sourcePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            string json = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "RigRoundTrip", $"{caseName}.rig-config.json"));
            sb.AppendLine(RigConfigExporter.ExportToFile(sourcePath, json));

            string copyName = caseName + "_RoundTrip";
            string summary = RigBundleImporter.Import(json, new RigBundleImporter.Options { NameOverride = copyName, PrefabFolder = TempFolder });
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var copy = AssetDatabase.LoadAssetAtPath<GameObject>($"{TempFolder}/{copyName}.prefab");
            if (copy == null)
            {
                sb.AppendLine($"x {caseName}: el import no produjo el prefab.\n{summary}");
                return 1;
            }

            var diffs = Compare(original, copy);
            int found = copy.GetComponent<ProceduralRig>().FindGesture("FxRoundTrip")?.fx?.Count ?? 0;
            if (found != 4) diffs.Add($"el gesto reimportado tiene {found} efectos, no 4");
            int namedCount = 0;
            foreach (var s in copy.GetComponent<ProceduralRig>().spriteSwaps) namedCount += s?.named?.Count ?? 0;
            if (namedCount != 4) diffs.Add($"los sprites con nombre reimportados son {namedCount}, no 4");
            sb.AppendLine(diffs.Count == 0 ? $"✓ {caseName}: efectos de gesto (4 anclas, 3 fases) y sprites con nombre (4 estados) idénticos tras la ida y vuelta."
                                           : $"x {caseName}: {diffs.Count} diferencias");
            foreach (var d in diffs) sb.AppendLine($"    {d}");
            return diffs.Count > 0 ? 1 : 0;
        }

        // ============================================================ comparación

        public static List<string> Compare(GameObject a, GameObject b)
        {
            var diffs = new List<string>();
            var rigA = a.GetComponent<ProceduralRig>();
            var rigB = b.GetComponent<ProceduralRig>();
            var visA = rigA.body;
            var visB = rigB.body;

            foreach (var f in typeof(ProceduralRig).GetFields(BindingFlags.Instance | BindingFlags.Public))
                Value($"rig.{f.Name}", f.GetValue(rigA), f.GetValue(rigB), visA, visB, diffs);

            Transforms("visual", visA, visB, diffs);
            Transforms("root", a.transform, b.transform, diffs, positionToo: false);

            var nodesA = visA.GetComponentsInChildren<Transform>(true);
            var nodesB = visB.GetComponentsInChildren<Transform>(true);
            if (nodesA.Length != nodesB.Length) diffs.Add($"nodos bajo Visual: {nodesA.Length} ≠ {nodesB.Length}");
            foreach (var t in nodesA)
            {
                if (t == visA) continue;
                string path = RigConfigJson.PathOf(visA, t);
                var other = visB.Find(path);
                if (other == null)
                {
                    diffs.Add($"falta el nodo {path}");
                    continue;
                }

                Transforms(path, t, other, diffs);
                var ra = t.GetComponent<SpriteRenderer>();
                var rb = other.GetComponent<SpriteRenderer>();
                if ((ra == null) != (rb == null)) diffs.Add($"{path}: SpriteRenderer {(ra != null)} ≠ {(rb != null)}");
                else if (ra != null && (ra.sprite != rb.sprite || ra.sortingOrder != rb.sortingOrder ||
                                        ra.sortingLayerName != rb.sortingLayerName || ra.color != rb.color))
                    diffs.Add($"{path}: sprite/orden/capa/color distintos");
            }

            // Números de juego: el EnemyStats (vía <nombre>.enemy-config.json) y la definición del jefe.
            var statsA = a.GetComponent<Enemies.EnemyStats>();
            var statsB = b.GetComponent<Enemies.EnemyStats>();
            if ((statsA == null) != (statsB == null)) diffs.Add("EnemyStats presente en uno solo");
            else if (statsA != null)
                foreach (var f in typeof(Enemies.EnemyTuning).GetFields(BindingFlags.Instance | BindingFlags.Public))
                    if (f.FieldType != typeof(Audio.SoundCue))
                        Value($"tuning.{f.Name}", f.GetValue(statsA.Tuning), f.GetValue(statsB.Tuning), visA, visB, diffs);

            var bossA = a.GetComponent<BossController>();
            var bossB = b.GetComponent<BossController>();
            if (bossA != null && bossB != null)
                Value("BossController.definition", new SerializedObject(bossA).FindProperty("definition").objectReferenceValue,
                      new SerializedObject(bossB).FindProperty("definition").objectReferenceValue, visA, visB, diffs);

            var limbsA = a.GetComponent<BossLimbs>();
            var limbsB = b.GetComponent<BossLimbs>();
            if ((limbsA == null) != (limbsB == null)) diffs.Add("BossLimbs presente en uno solo");
            else if (limbsA != null)
            {
                var soA = new SerializedObject(limbsA);
                var soB = new SerializedObject(limbsB);
                foreach (var field in new[] { "limbs", "orbiters", "grips" })
                {
                    var pa = soA.FindProperty(field);
                    var pb = soB.FindProperty(field);
                    if (pa.arraySize != pb.arraySize) { diffs.Add($"BossLimbs.{field}: {pa.arraySize} ≠ {pb.arraySize}"); continue; }
                    for (int i = 0; i < pa.arraySize; i++)
                        Value($"BossLimbs.{field}[{i}]", pa.GetArrayElementAtIndex(i).objectReferenceValue,
                              pb.GetArrayElementAtIndex(i).objectReferenceValue, visA, visB, diffs);
                }

                foreach (var field in new[] { "orbiterRadius", "contactRadius" })
                    Value($"BossLimbs.{field}", soA.FindProperty(field).floatValue, soB.FindProperty(field).floatValue,
                          visA, visB, diffs);
            }

            // Los sonidos de un gesto no viajan en el export a archivo (sólo por el puente): la copia nace sin ellos.
            diffs.RemoveAll(d => d.StartsWith("rig.gestures[") && d.Contains("].sounds"));
            return diffs;
        }

        private static void Transforms(string path, Transform a, Transform b, List<string> diffs, bool positionToo = true)
        {
            if (positionToo && Vector2.Distance(a.localPosition, b.localPosition) > 0.001f)
                diffs.Add($"{path}: posición {a.localPosition} ≠ {b.localPosition}");
            if (Mathf.Abs(Mathf.DeltaAngle(a.localEulerAngles.z, b.localEulerAngles.z)) > 0.01f)
                diffs.Add($"{path}: giro {a.localEulerAngles.z} ≠ {b.localEulerAngles.z}");
            if (Vector2.Distance(a.localScale, b.localScale) > 0.001f)
                diffs.Add($"{path}: escala {a.localScale} ≠ {b.localScale}");
            if (a.gameObject.activeSelf != b.gameObject.activeSelf) diffs.Add($"{path}: activo distinto");
        }

        /// <summary>Compara dos valores serializados; las piezas por su ruta bajo Visual.</summary>
        private static void Value(string path, object a, object b, Transform visA, Transform visB, List<string> diffs)
        {
            if (a is Object || b is Object)
            {
                // Un campo sin asignar es un "null falso" de Unity: sólo el == de Object lo ve nulo.
                var oa = a as Object;
                var ob = b as Object;
                if (oa == null) oa = null;
                if (ob == null) ob = null;
                if (oa is Component ca && ob is Component cb)
                {
                    string pa = RigConfigJson.PathOf(visA, ca.transform), pb = RigConfigJson.PathOf(visB, cb.transform);
                    if (pa != pb || ca.GetType() != cb.GetType()) diffs.Add($"{path}: {pa} ≠ {pb}");
                }
                else if (oa is Component || ob is Component)
                {
                    if ((oa == null) != (ob == null)) diffs.Add($"{path}: {(oa == null ? "vacío" : oa.name)} ≠ {(ob == null ? "vacío" : ob.name)}");
                }
                else if (oa != ob) diffs.Add($"{path}: {(oa ? oa.name : "vacío")} ≠ {(ob ? ob.name : "vacío")}");
                return;
            }

            if (a == null || b == null)
            {
                if (a != b) diffs.Add($"{path}: {a ?? "null"} ≠ {b ?? "null"}");
                return;
            }

            switch (a)
            {
                case float fa:
                    if (Mathf.Abs(fa - (float)b) > 0.001f) diffs.Add($"{path}: {fa} ≠ {b}");
                    return;
                case Vector2 va:
                    if (Vector2.Distance(va, (Vector2)b) > 0.001f) diffs.Add($"{path}: {va} ≠ {b}");
                    return;
                case Vector3 v3:
                    if (Vector3.Distance(v3, (Vector3)b) > 0.001f) diffs.Add($"{path}: {v3} ≠ {b}");
                    return;
                case Color ca2:
                    var cb2 = (Color)b;
                    if (Mathf.Abs(ca2.r - cb2.r) + Mathf.Abs(ca2.g - cb2.g) + Mathf.Abs(ca2.b - cb2.b) + Mathf.Abs(ca2.a - cb2.a) > 0.01f)
                        diffs.Add($"{path}: {ca2} ≠ {cb2}");
                    return;
                case LayerMask la:
                    if (la.value != ((LayerMask)b).value) diffs.Add($"{path}: {la.value} ≠ {((LayerMask)b).value}");
                    return;
                case string _:
                case bool _:
                case int _:
                case Enum _:
                    if (!a.Equals(b)) diffs.Add($"{path}: {a} ≠ {b}");
                    return;
                case IList la2:
                    var lb = (IList)b;
                    if (la2.Count != lb.Count)
                    {
                        diffs.Add($"{path}: {la2.Count} elementos ≠ {lb.Count}");
                        return;
                    }

                    for (int i = 0; i < la2.Count; i++) Value($"{path}[{i}]", la2[i], lb[i], visA, visB, diffs);
                    return;
            }

            var type = a.GetType();
            if (type.IsDefined(typeof(SerializableAttribute), false))
            {
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                    Value($"{path}.{f.Name}", f.GetValue(a), f.GetValue(b), visA, visB, diffs);
                return;
            }

            if (!a.Equals(b)) diffs.Add($"{path}: {a} ≠ {b} ({type.Name})");
        }
    }
}
