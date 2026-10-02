using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Escritor genérico de <see cref="SerializedProperty"/> a partir de un <see cref="JToken"/>.
    ///
    /// Lo usa <c>BossConfigImporter</c> tanto para los 17 campos compartidos de <c>BossAttack</c>
    /// como para el bloque abierto <c>params</c> de cada arquetipo — un único camino en vez de
    /// mezclar <c>BossAuthoring.Fields</c> (sólo cubre unos pocos tipos, y sólo avisa por
    /// <c>Debug.LogWarning</c> ante un campo que no existe) con casos sueltos por tipo.
    ///
    /// <b>Nunca lanza.</b> Un campo que no existe o cuyo tipo no se sabe escribir se añade a
    /// <paramref name="unresolved"/> en vez de abortar el import entero — a diferencia de un
    /// assetRef o una layerMask rotos (ver <see cref="ConfigJson"/>), una clave de <c>params</c>
    /// mal escrita es justo lo que <c>docs/schemas/COMPATIBILITY.md</c> pide "reportar, no
    /// silenciar", no "fallar alto": el resto del ataque se sigue escribiendo con normalidad.
    /// </summary>
    public static class SerializedFieldWriter
    {
        /// <summary>Busca el campo por nombre en <paramref name="so"/> y lo escribe.</summary>
        public static void TryWrite(SerializedObject so, string fieldName, JToken value, string context,
                                    List<string> unresolved)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                unresolved.Add($"{context}: el campo '{fieldName}' no existe en {so.targetObject.GetType().Name}.");
                return;
            }

            TryWriteValue(prop, value, context, unresolved);
        }

        /// <summary>Escribe un valor ya resuelto a un SerializedProperty concreto.</summary>
        public static bool TryWriteValue(SerializedProperty prop, JToken value, string path, List<string> unresolved)
        {
            if (value == null || value.Type == JTokenType.Null) return true; // ausente = no se toca

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:
                    prop.floatValue = value.Value<float>();
                    return true;

                case SerializedPropertyType.Integer:
                    prop.intValue = value.Value<int>();
                    return true;

                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.Value<bool>();
                    return true;

                case SerializedPropertyType.String:
                    prop.stringValue = value.Value<string>();
                    return true;

                case SerializedPropertyType.Vector2:
                    prop.vector2Value = ConfigJson.ReadVector2(value, prop.vector2Value);
                    return true;

                case SerializedPropertyType.Vector3:
                {
                    var v2 = ConfigJson.ReadVector2(value, prop.vector3Value);
                    prop.vector3Value = new Vector3(v2.x, v2.y, prop.vector3Value.z);
                    return true;
                }

                case SerializedPropertyType.Color:
                {
                    // Un hex tiene 8 bits por canal: si es el mismo color que ya hay, no se reescribe
                    // (guardar desde la web sin tocarlo no debe redondear el del asset).
                    var color = ConfigJson.ReadColor(value, prop.colorValue);
                    if (ColorUtility.ToHtmlStringRGBA(color) != ColorUtility.ToHtmlStringRGBA(prop.colorValue))
                        prop.colorValue = color;
                    return true;
                }

                case SerializedPropertyType.LayerMask:
                    prop.intValue = ConfigJson.ReadLayerMask(value, prop.intValue, path);
                    return true;

                case SerializedPropertyType.Enum:
                    return TryWriteEnum(prop, value, path, unresolved);

                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = ConfigJson.ReadAsset(value, path);
                    return true;

                case SerializedPropertyType.Generic when prop.isArray:
                    return TryWriteArray(prop, value, path, unresolved);

                // Un ProjectileSpec anidado (el de cada suelta de un ReleaseAttack): entero, como el de
                // primer nivel — con su arte de biblioteca ('libraryId') resuelto a prefab.
                case SerializedPropertyType.Generic when prop.type == "ProjectileSpec":
                    ProjectileConfigImporter.WriteInto(prop, ProjectileConfigImporter.Resolve(value, null, path));
                    return true;

                case SerializedPropertyType.Generic:
                    return TryWriteObject(prop, value, path, unresolved);

                default:
                    unresolved.Add($"{path}: tipo de campo no soportado por el importador ({prop.propertyType}).");
                    return false;
            }
        }

        private static bool TryWriteEnum(SerializedProperty prop, JToken value, string path, List<string> unresolved)
        {
            string name = value.Value<string>();

            for (int i = 0; i < prop.enumNames.Length; i++)
            {
                if (prop.enumNames[i] != name) continue;
                prop.enumValueIndex = i;
                return true;
            }

            unresolved.Add($"{path}: '{name}' no es un valor válido ({string.Join(", ", prop.enumNames)}).");
            return false;
        }

        /// <summary>
        /// Una clase [Serializable] anidada: cada clave del JSON a su campo. Un elemento recién crecido
        /// de un array nace a ceros (Unity no llama a los inicializadores de C#), así que quien escribe
        /// listas de objetos manda todos sus campos.
        /// </summary>
        private static bool TryWriteObject(SerializedProperty prop, JToken value, string path, List<string> unresolved)
        {
            if (!(value is JObject obj))
            {
                unresolved.Add($"{path}: se esperaba un objeto JSON.");
                return false;
            }

            bool ok = true;
            foreach (var p in obj.Properties())
            {
                var child = prop.FindPropertyRelative(p.Name);
                if (child == null)
                {
                    unresolved.Add($"{path}.{p.Name}: el campo no existe en {prop.type}.");
                    ok = false;
                    continue;
                }

                ok &= TryWriteValue(child, p.Value, $"{path}.{p.Name}", unresolved);
            }

            return ok;
        }

        private static bool TryWriteArray(SerializedProperty prop, JToken value, string path, List<string> unresolved)
        {
            if (!(value is JArray array))
            {
                unresolved.Add($"{path}: se esperaba un array JSON.");
                return false;
            }

            prop.arraySize = array.Count;
            bool ok = true;
            for (int i = 0; i < array.Count; i++)
                ok &= TryWriteValue(prop.GetArrayElementAtIndex(i), array[i], $"{path}[{i}]", unresolved);

            return ok;
        }
    }
}
