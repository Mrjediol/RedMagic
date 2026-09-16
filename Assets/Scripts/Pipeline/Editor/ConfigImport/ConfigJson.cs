using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Lee los cuatro tipos compartidos de <c>docs/schemas/_shared.schema.json</c> —
    /// <c>assetRef</c>, <c>layerMask</c>, <c>vector2</c>, <c>color</c> — desde JSON ya parseado
    /// (Newtonsoft). Una sola implementación para los tres importadores, así las definiciones
    /// compartidas del schema tienen una sola lectura en C#.
    ///
    /// <b>assetRef</b> y <b>layerMask</b> fallan alto (<see cref="ConfigImportException"/>) cuando
    /// no se pueden resolver, tal y como exige <c>docs/schemas/COMPATIBILITY.md</c>: una máscara
    /// que se queda en blanco o una referencia que carga null no rompen nada visiblemente — sólo
    /// producen un enemigo o un ataque que "parece" configurado pero no lo está, y eso es
    /// exactamente la clase de fallo silencioso que <c>ContentAudit</c> existe para reparar
    /// después del hecho. Mejor pararse aquí que fabricar ese bug de nuevo.
    /// </summary>
    public static class ConfigJson
    {
        public static Vector2 ReadVector2(JToken token, Vector2 fallback)
        {
            if (token == null || token.Type == JTokenType.Null) return fallback;

            if (token is JArray array && array.Count >= 2)
                return new Vector2(array[0].Value<float>(), array[1].Value<float>());

            if (token is JObject obj)
            {
                float x = obj.TryGetValue("x", out var xt) ? xt.Value<float>() : fallback.x;
                float y = obj.TryGetValue("y", out var yt) ? yt.Value<float>() : fallback.y;
                return new Vector2(x, y);
            }

            throw new ConfigImportException($"vector2: forma no reconocida ({token.Type}).");
        }

        public static Color ReadColor(JToken token, Color fallback)
        {
            if (token == null || token.Type == JTokenType.Null) return fallback;

            if (token.Type == JTokenType.String)
            {
                string hex = token.Value<string>();
                string withHash = hex.StartsWith("#") ? hex : "#" + hex;
                if (ColorUtility.TryParseHtmlString(withHash, out var parsed)) return parsed;
                throw new ConfigImportException($"color: '{hex}' no es un hex válido.");
            }

            if (token is JObject obj)
            {
                float r = obj.TryGetValue("r", out var rt) ? rt.Value<float>() : fallback.r;
                float g = obj.TryGetValue("g", out var gt) ? gt.Value<float>() : fallback.g;
                float b = obj.TryGetValue("b", out var bt) ? bt.Value<float>() : fallback.b;
                float a = obj.TryGetValue("a", out var at) ? at.Value<float>() : 1f;
                return new Color(r, g, b, a);
            }

            throw new ConfigImportException($"color: forma no reconocida ({token.Type}).");
        }

        /// <summary>
        /// Resuelve una máscara de capas por NOMBRE. Falla alto ante una capa que no existe en el
        /// proyecto en vez de dejarla fuera calladamente — ver el comentario de clase.
        /// </summary>
        public static LayerMask ReadLayerMask(JToken token, LayerMask fallback, string context)
        {
            if (token == null || token.Type == JTokenType.Null) return fallback;

            if (token.Type == JTokenType.Integer) return (int)token;

            if (token.Type == JTokenType.String)
            {
                string s = token.Value<string>();
                if (s == "everything") return ~0;
                if (s == "nothing") return 0;
                return ResolveLayerNames(new[] { s }, context);
            }

            if (token is JArray array)
                return ResolveLayerNames(array.Select(t => t.Value<string>()).ToArray(), context);

            throw new ConfigImportException($"{context}: forma de layerMask no reconocida ({token.Type}).");
        }

        private static LayerMask ResolveLayerNames(string[] names, string context)
        {
            int mask = 0;
            foreach (var name in names)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer < 0)
                    throw new ConfigImportException(
                        $"{context}: la capa '{name}' no existe en el proyecto " +
                        "(Edit > Project Settings > Tags and Layers).");
                mask |= 1 << layer;
            }
            return mask;
        }

        /// <summary>
        /// Resuelve un assetRef (guid primero, path de respaldo) vía AssetDatabase. Falla alto si
        /// el token está presente pero no se puede resolver — ver el comentario de clase.
        /// </summary>
        public static T ReadAsset<T>(JToken token, string context) where T : UnityEngine.Object
        {
            if (token == null || token.Type == JTokenType.Null) return null;

            string guid = null, path;

            if (token.Type == JTokenType.String)
            {
                path = token.Value<string>();
            }
            else if (token is JObject obj)
            {
                guid = obj.TryGetValue("guid", out var g) ? g.Value<string>() : null;
                path = obj.TryGetValue("path", out var p) ? p.Value<string>() : null;
            }
            else
            {
                throw new ConfigImportException($"{context}: forma de assetRef no reconocida ({token.Type}).");
            }

            if (!string.IsNullOrEmpty(guid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(guidPath))
                    throw new ConfigImportException($"{context}: el guid '{guid}' no corresponde a ningún asset.");
                path = guidPath;
            }

            if (string.IsNullOrEmpty(path))
                throw new ConfigImportException($"{context}: assetRef sin 'guid' ni 'path'.");

            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new ConfigImportException(
                    $"{context}: no se encontró un asset de tipo {typeof(T).Name} en '{path}'.");

            return asset;
        }

        public static UnityEngine.Object ReadAsset(JToken token, string context) => ReadAsset<UnityEngine.Object>(token, context);

        /// <summary>Un campo string obligatorio. Falla alto si falta o está vacío.</summary>
        public static string RequireString(JObject obj, string key, string context)
        {
            if (obj != null && obj.TryGetValue(key, out var token) && token.Type != JTokenType.Null)
            {
                string value = token.Value<string>();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }

            throw new ConfigImportException($"{context}: falta '{key}'.");
        }

        /// <summary>Castea a JObject con un mensaje claro en vez de un InvalidCastException pelado.</summary>
        public static JObject AsObject(JToken token, string context)
        {
            if (token is JObject obj) return obj;
            throw new ConfigImportException($"{context}: se esperaba un objeto JSON.");
        }

        /// <summary>
        /// Un valor de enum por NOMBRE exacto (sensible a mayúsculas, igual que el schema). Falla
        /// alto ante un nombre que no exista — un archetype desconocido no debe caer callado al
        /// valor por defecto de la clase.
        /// </summary>
        public static TEnum ParseEnum<TEnum>(JToken token, string context) where TEnum : struct, Enum
        {
            string name = token?.Type == JTokenType.String ? token.Value<string>() : null;

            if (string.IsNullOrEmpty(name))
                throw new ConfigImportException($"{context}: falta el valor.");

            if (Enum.TryParse<TEnum>(name, ignoreCase: false, out var value) && Enum.IsDefined(typeof(TEnum), value))
                return value;

            throw new ConfigImportException(
                $"{context}: '{name}' no es un valor válido ({string.Join(", ", Enum.GetNames(typeof(TEnum)))}).");
        }
    }
}
