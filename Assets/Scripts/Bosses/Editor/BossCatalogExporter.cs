using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedMagic.Abilities;
using RedMagic.Core;
using RedMagic.Gameplay.Movement;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Escribe el <b>catálogo</b> que lee la pestaña de jefes de la web
    /// (<c>Web/RedMagicWeb/modules/boss-catalog.js</c>): todos los tipos de ataque y de movimiento
    /// del proyecto, con sus campos, valores por defecto, tooltips y huecos de arte, sacados por
    /// reflexión. Así la web nunca mantiene una lista a mano: un ataque nuevo en C# aparece en la
    /// web al recompilar.
    ///
    /// Se regenera solo tras cada compilación (sólo reescribe si cambia) y a mano desde
    /// Tools ▸ Web ▸ Exportar catálogo de jefes.
    /// </summary>
    [InitializeOnLoad]
    public static class BossCatalogExporter
    {
        /// <summary>Los campos de BossAttack que el importador escribe fuera de 'params'.</summary>
        internal static readonly string[] CommonFields =
        {
            "displayName", "description", "accent", "telegraph", "recovery", "weight",
            "cooldownInAttacks", "cooldownSeconds", "minPhase", "damage", "knockbackMultiplier",
            "vulnerableSeconds", "vulnerableMultiplier", "gesture", "shakeAmplitude", "shakeDuration",
        };

        private static readonly HashSet<string> NoHidden = new HashSet<string>();

        static BossCatalogExporter()
        {
            EditorApplication.delayCall += () =>
            {
                try { Export(out _); }
                catch (Exception e) { Debug.LogWarning($"[BossCatalog] No se pudo regenerar el catálogo de la web: {e.Message}"); }
            };
        }

        public static string OutputPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Web", "RedMagicWeb", "modules", "boss-catalog.js"));

        [MenuItem("Tools/Web/Exportar catálogo de jefes (para la web)")]
        private static void ExportMenu()
        {
            bool ok = Export(out string message);
            EditorUtility.DisplayDialog("Catálogo de jefes", message, "OK");
            if (ok) Debug.Log($"[BossCatalog] {message}");
        }

        /// <summary>Escribe el catálogo. Devuelve false si no hay carpeta de la web.</summary>
        public static bool Export(out string message)
        {
            string path = OutputPath;
            string dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                message = $"No existe la carpeta de la web '{dir}'.";
                return false;
            }

            var catalog = Build();
            string js = "// GENERADO por Unity (BossCatalogExporter.cs) — no editar a mano.\n" +
                        "// Se regenera al compilar y desde Tools > Web > Exportar catálogo de jefes.\n" +
                        "export const BOSS_CATALOG = " + catalog.ToString(Formatting.Indented) + ";\n";

            if (File.Exists(path) && File.ReadAllText(path) == js)
            {
                message = $"Catálogo al día ({path}).";
                return true;
            }

            File.WriteAllText(path, js);
            message = $"Catálogo escrito: {catalog["attacks"].Count()} ataques, {catalog["movements"].Count()} movimientos → {path}";
            return true;
        }

        // ============================================================ construcción

        private static JObject Build()
        {
            var attacks = new JArray();
            JArray common = null;

            var attackTypes = TypeCache.GetTypesDerivedFrom<BossAttack>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .OrderBy(t => t.Name);

            foreach (var type in attackTypes)
            {
                var instance = ScriptableObject.CreateInstance(type);
                try
                {
                    var hidden = new HashSet<string>(type.GetCustomAttributes(typeof(AttackHidesAttribute), true)
                        .Cast<AttackHidesAttribute>().SelectMany(a => a.Paths));

                    var parameters = new JArray();
                    var commonHere = new JArray();
                    foreach (var field in SerializableFields(type))
                    {
                        var target = field.DeclaringType == typeof(BossAttack) ? commonHere : parameters;
                        if (target == commonHere && !CommonFields.Contains(field.Name)) continue;

                        // Los campos base no los oculta ningún tipo: son de todos los ataques.
                        var d = Describe(field, field.GetValue(instance), field.Name,
                                         target == commonHere ? NoHidden : hidden, null, 0);
                        if (d != null) target.Add(d);
                    }

                    if (common == null) common = commonHere;

                    attacks.Add(new JObject
                    {
                        ["type"] = type.Name,
                        ["label"] = ObjectNames.NicifyVariableName(type.Name.EndsWith("Attack")
                            ? type.Name.Substring(0, type.Name.Length - 6) : type.Name),
                        ["params"] = parameters,
                    });
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            var movements = new JArray();
            foreach (var type in TypeCache.GetTypesDerivedFrom<MovementBehaviour>()
                         .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                         .OrderBy(t => t.Name))
            {
                object instance = Activator.CreateInstance(type);
                var parameters = new JArray();
                foreach (var field in SerializableFields(type))
                {
                    var d = Describe(field, field.GetValue(instance), field.Name, NoHidden, null, 0);
                    if (d != null) parameters.Add(d);
                }

                var display = type.GetCustomAttribute<DisplayNameAttribute>();
                movements.Add(new JObject
                {
                    ["type"] = type.Name,
                    ["label"] = display != null ? display.DisplayName : ObjectNames.NicifyVariableName(type.Name),
                    ["params"] = parameters,
                });
            }

            return new JObject
            {
                ["version"] = 1,
                ["common"] = common ?? new JArray(),
                ["attacks"] = attacks,
                ["movements"] = movements,
            };
        }

        /// <summary>Campos que Unity serializa, de la clase base a la derivada, en orden de declaración.</summary>
        private static IEnumerable<FieldInfo> SerializableFields(Type type)
        {
            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(ScriptableObject) && t != typeof(object) &&
                               t != typeof(Object) && t != typeof(MonoBehaviour); t = t.BaseType)
                chain.Insert(0, t);

            foreach (var t in chain)
            {
                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.DeclaredOnly)
                    .OrderBy(f => f.MetadataToken);
                foreach (var f in fields)
                {
                    if (f.IsInitOnly || f.IsLiteral) continue;
                    if (f.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                    if (f.IsDefined(typeof(HideInInspector), false)) continue;
                    if (f.IsDefined(typeof(SerializeReference), false)) continue;
                    // Los sonidos (SoundCue) son datos de Unity que se asignan en la pestaña Sounds, no en la web.
                    if (f.FieldType == typeof(RedMagic.Audio.SoundCue)) continue;
                    if (!f.IsPublic && !f.IsDefined(typeof(SerializeField), false)) continue;
                    yield return f;
                }
            }
        }

        /// <summary>
        /// Un campo como lo pinta la web. <c>type</c>: float, int, bool, string, vector2, color, enum,
        /// floatList, intList, art (GameObject → hueco de arte), object (clase anidada, con
        /// <c>fields</c>) o unity (sólo se asigna en Unity; la web lo enseña pero no lo exporta).
        /// </summary>
        private static JObject Describe(FieldInfo field, object value, string path, HashSet<string> hidden,
                                        ArtSlotAttribute inheritedSlot, int depth)
        {
            if (hidden.Contains(path) || depth > 8) return null;

            var ft = field.FieldType;
            var o = new JObject
            {
                ["name"] = field.Name,
                ["label"] = ObjectNames.NicifyVariableName(field.Name),
            };

            var tooltip = field.GetCustomAttribute<TooltipAttribute>();
            if (tooltip != null) o["tooltip"] = tooltip.tooltip;
            var header = field.GetCustomAttributes<HeaderAttribute>().FirstOrDefault();
            if (header != null) o["header"] = header.header;
            var min = field.GetCustomAttribute<MinAttribute>();
            if (min != null) o["min"] = Math.Round((double)min.min, 4);
            var range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null) { o["min"] = Math.Round((double)range.min, 4); o["max"] = Math.Round((double)range.max, 4); }

            var slot = field.GetCustomAttribute<ArtSlotAttribute>();

            if (ft == typeof(float)) { o["type"] = "float"; o["default"] = Math.Round((double)(float)value, 4); }
            else if (ft == typeof(double)) { o["type"] = "float"; o["default"] = Math.Round((double)value, 4); }
            else if (ft == typeof(int)) { o["type"] = "int"; o["default"] = (int)value; }
            else if (ft == typeof(bool)) { o["type"] = "bool"; o["default"] = (bool)value; }
            else if (ft == typeof(string)) { o["type"] = "string"; o["default"] = (string)value ?? ""; }
            else if (ft == typeof(Vector2) || ft == typeof(Vector3))
            {
                Vector2 v = ft == typeof(Vector2) ? (Vector2)value : (Vector2)(Vector3)value;
                o["type"] = "vector2";
                o["default"] = new JObject { ["x"] = Round(v.x), ["y"] = Round(v.y) };
            }
            else if (ft == typeof(Color))
            {
                o["type"] = "color";
                o["default"] = "#" + ColorUtility.ToHtmlStringRGBA((Color)value).ToLowerInvariant();
            }
            else if (ft.IsEnum)
            {
                o["type"] = "enum";
                o["options"] = JArray.FromObject(Enum.GetNames(ft));
                o["default"] = value != null ? value.ToString() : Enum.GetNames(ft).FirstOrDefault();
            }
            else if (ft == typeof(GameObject))
            {
                var s = slot ?? inheritedSlot;
                o["type"] = "art";
                o["artKind"] = (s != null ? s.Kind : InferKind(field.Name, inheritedSlot)).ToString().ToLowerInvariant();
                o["artLabel"] = s != null ? s.Label : ObjectNames.NicifyVariableName(field.Name);
                o["placeholder"] = s != null ? s.Placeholder : "el de por defecto del ataque";
                o["default"] = null;
            }
            else if (typeof(Object).IsAssignableFrom(ft))
            {
                o["type"] = "unity";
                o["unityType"] = ft.Name;
            }
            else if (ft == typeof(float[]) || ft == typeof(List<float>))
            {
                o["type"] = "floatList";
                o["default"] = JArray.FromObject(((IEnumerable)value ?? Array.Empty<float>()).Cast<float>().Select(Round).ToArray());
            }
            else if (ft == typeof(int[]) || ft == typeof(List<int>))
            {
                o["type"] = "intList";
                o["default"] = JArray.FromObject(((IEnumerable)value ?? Array.Empty<int>()).Cast<int>().ToArray());
            }
            else if (ft.IsArray || (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>)))
            {
                o["type"] = "unity";
                o["unityType"] = ft.Name;
            }
            else if (ft.IsClass && ft.IsDefined(typeof(SerializableAttribute), false))
            {
                object nested = value ?? SafeCreate(ft);
                var fields = new JArray();
                // Un ProjectileSpec marcado como hueco de arte pasa la etiqueta a su 'prefab'.
                var childSlot = slot ?? (ft == typeof(ProjectileSpec)
                    ? new ArtSlotAttribute(ObjectNames.NicifyVariableName(field.Name), ArtSlotKind.Projectile,
                                           "bola de color de la fase")
                    : null);
                if (nested != null)
                    foreach (var child in SerializableFields(ft))
                    {
                        var d = Describe(child, child.GetValue(nested), $"{path}.{child.Name}", hidden, childSlot, depth + 1);
                        if (d != null) fields.Add(d);
                    }

                o["type"] = "object";
                o["fields"] = fields;
                if (ft == typeof(ProjectileSpec)) o["spec"] = "projectile";
                if (slot != null) o["artLabel"] = slot.Label;
            }
            else
            {
                o["type"] = "unity";
                o["unityType"] = ft.Name;
            }

            return o;
        }

        private static ArtSlotKind InferKind(string fieldName, ArtSlotAttribute inherited)
        {
            if (inherited != null) return inherited.Kind;
            string n = fieldName.ToLowerInvariant();
            if (n.Contains("fx") || n.Contains("impact") || n.Contains("effect")) return ArtSlotKind.Fx;
            if (n.Contains("warn")) return ArtSlotKind.Warning;
            if (n.Contains("bolt") || n.Contains("orb") || n.Contains("projectile") || n.Contains("bullet"))
                return ArtSlotKind.Projectile;
            return ArtSlotKind.Prop;
        }

        private static object SafeCreate(Type t)
        {
            try { return Activator.CreateInstance(t); }
            catch { return null; }
        }

        private static double Round(float v) => Math.Round((double)v, 4);
    }
}
