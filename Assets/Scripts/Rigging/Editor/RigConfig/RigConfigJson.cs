using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Rigging.EditorTools
{
    /// <summary>
    /// Vocabulario compartido por <see cref="RigConfigExporter"/> y <see cref="RigBundleImporter"/>
    /// (<c>docs/schemas/rig-config.schema.json</c>): cómo se escribe una referencia a una pieza, a
    /// un sprite, a un color o a una máscara de capas. Los dos lados pasan por aquí para que una ida
    /// y vuelta sea exacta.
    /// </summary>
    public static class RigConfigJson
    {
        public const string ConfigFile = "rig-config.json";
        public const string EnemyConfigFile = "enemy-config.json";
        public const string BossConfigFile = "boss-config.json";

        /// <summary>Hijo de la raíz que marca los pies de un jefe (<c>root.feet</c> ↔ BossController.feet).</summary>
        public const string FeetName = "Feet";

        /// <summary>Referencia a la raíz visual (<c>ProceduralRig.body</c>) en poses y roles.</summary>
        public const string BodyToken = "Visual";

        /// <summary>Carpeta donde el importador deja los PNG que vienen en un bundle.</summary>
        public const string ArtRoot = "Assets/Art/RigImports";

        /// <summary>
        /// Campos de <see cref="ProceduralRig"/> que NO van en el bloque <c>rig</c>: la raíz visual es
        /// siempre <c>Visual</c>, y las listas estructuradas tienen su propio bloque.
        /// </summary>
        public static readonly HashSet<string> StructuredRigFields = new HashSet<string>
        {
            nameof(ProceduralRig.body), nameof(ProceduralRig.parts), nameof(ProceduralRig.spriteSwaps),
            nameof(ProceduralRig.gestures),
        };

        /// <summary>Campos serializados de ProceduralRig que viajan en el bloque <c>rig</c>, en orden de declaración.</summary>
        public static IEnumerable<FieldInfo> RigFields() =>
            typeof(ProceduralRig).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => !f.IsInitOnly && !f.IsDefined(typeof(NonSerializedAttribute), false) &&
                            !StructuredRigFields.Contains(f.Name))
                .OrderBy(f => f.MetadataToken);

        /// <summary>
        /// El archivo que acompaña a un rig-config: junto a "Robot.rig-config.json" es
        /// "Robot.enemy-config.json"; junto a "rig-config.json" (el de un zip), "enemy-config.json".
        /// </summary>
        public static string SiblingPath(string rigConfigPath, string companionFile)
        {
            string dir = System.IO.Path.GetDirectoryName(rigConfigPath) ?? "";
            string file = System.IO.Path.GetFileName(rigConfigPath);
            const string suffix = "." + ConfigFile;
            return file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? System.IO.Path.Combine(dir, file.Substring(0, file.Length - suffix.Length) + "." + companionFile)
                : System.IO.Path.Combine(dir, companionFile);
        }

        // ============================================================ piezas

        /// <summary>Ruta de <paramref name="t"/> relativa a la raíz visual ("Torso/RightArm"), o null.</summary>
        public static string PathOf(Transform visual, Transform t)
        {
            if (t == null || visual == null) return null;
            if (t == visual) return BodyToken;
            if (!t.IsChildOf(visual)) return null;
            return AnimationUtility.CalculateTransformPath(t, visual);
        }

        /// <summary>La pieza de esa ruta (o la raíz visual con <see cref="BodyToken"/>), o null.</summary>
        public static Transform Find(Transform visual, string path)
        {
            if (visual == null || string.IsNullOrEmpty(path)) return null;
            return path == BodyToken ? visual : visual.Find(path);
        }

        // ============================================================ valores

        public static double Num(float v) => Math.Round((double)v, 5);

        // Fases y anclas de los efectos de gesto: los nombres del JSON (minúscula) ↔ los enums.
        public static string FxPhaseName(RigFxPhase phase) => phase.ToString().ToLowerInvariant();
        public static string FxAnchorName(RigFxAnchor anchor) => anchor.ToString().ToLowerInvariant();

        public static string NamedStateName(RigNamedState state) => state.ToString().ToLowerInvariant();

        public static bool TryNamedState(string name, out RigNamedState state) =>
            Enum.TryParse(name, true, out state) && Enum.IsDefined(typeof(RigNamedState), state);

        public static bool TryFxPhase(string name, out RigFxPhase phase) =>
            Enum.TryParse(name, true, out phase) && Enum.IsDefined(typeof(RigFxPhase), phase);

        public static bool TryFxAnchor(string name, out RigFxAnchor anchor) =>
            Enum.TryParse(name, true, out anchor) && Enum.IsDefined(typeof(RigFxAnchor), anchor);

        public static JObject V2(Vector2 v) => new JObject { ["x"] = Num(v.x), ["y"] = Num(v.y) };

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c).ToLowerInvariant();

        /// <summary>Escala: un número si es uniforme (x = y = z), si no {x, y} (z = 1 al importar).</summary>
        public static JToken Scale(Vector3 s) =>
            Mathf.Approximately(s.x, s.y) && Mathf.Approximately(s.x, s.z) ? (JToken)Num(s.x) : V2(s);

        public static Vector3 ReadScale(JToken token, Vector3 fallback)
        {
            if (token == null || token.Type == JTokenType.Null) return fallback;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                float k = token.Value<float>();
                return new Vector3(k, k, k);
            }

            var v = ConfigJson.ReadVector2(token, fallback);
            return new Vector3(v.x, v.y, 1f);
        }

        /// <summary>Ángulo Z local en (-180, 180].</summary>
        public static float Angle(Transform t) => Mathf.DeltaAngle(0f, t.localEulerAngles.z);

        /// <summary>Máscara como nombres de capa (el formato preferido del schema); entero si alguna capa no tiene nombre.</summary>
        public static JToken LayerNames(LayerMask mask)
        {
            if (mask.value == 0) return "nothing";
            if (mask.value == ~0) return "everything";

            var names = new JArray();
            for (int i = 0; i < 32; i++)
            {
                if ((mask.value & (1 << i)) == 0) continue;
                string name = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(name)) return mask.value;
                names.Add(name);
            }

            return names;
        }

        // ============================================================ sprites

        /// <summary>
        /// Un sprite del proyecto como ruta de asset; con <c>#nombre</c> si su textura lleva varios
        /// sprites. Null si no hay sprite.
        /// </summary>
        public static JToken SpriteRef(Sprite sprite)
        {
            if (sprite == null) return JValue.CreateNull();
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path)) return JValue.CreateNull();

            int count = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().Count();
            return count > 1 ? $"{path}#{sprite.name}" : path;
        }

        /// <summary>Un sprite ya en el proyecto ("Assets/…png" o "Assets/…png#nombre"), o null.</summary>
        public static Sprite LoadProjectSprite(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;

            int hash = reference.IndexOf('#');
            string path = hash >= 0 ? reference.Substring(0, hash) : reference;
            if (hash < 0) return AssetDatabase.LoadAssetAtPath<Sprite>(path);

            string name = reference.Substring(hash + 1);
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>()
                .FirstOrDefault(s => s.name == name);
        }

        /// <summary>True si la referencia apunta a un asset del proyecto (y no a un archivo del bundle).</summary>
        public static bool IsProjectRef(string reference) =>
            reference != null && reference.StartsWith("Assets/", StringComparison.Ordinal);
    }
}
