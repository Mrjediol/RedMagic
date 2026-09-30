using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using RedMagic.Bosses;
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
    /// </summary>
    public static class RigConfigRoundTrip
    {
        private const string TempFolder = ProceduralEnemyPacks.Folder + "/_RoundTrip";
        private static readonly string[] Reference = { "Robot", "Turret", "BossIce2" };

        public static string FixtureFolder =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "schemas", "fixtures", "rig"));

        [MenuItem("Tools/Web/Rig/Probar ida y vuelta (Robot · Turret · BossIce2)")]
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
                    sb.AppendLine(diffs.Count == 0 ? $"✓ {name}: ProceduralRig, jerarquía y BossLimbs idénticos."
                                                   : $"x {name}: {diffs.Count} diferencias");
                    foreach (var d in diffs) sb.AppendLine($"    {d}");
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }

            sb.Insert(0, failures == 0 ? "IDA Y VUELTA OK\n" : $"IDA Y VUELTA CON FALLOS ({failures})\n");
            return sb.ToString();
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
