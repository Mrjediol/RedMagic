using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using RedMagic.Enemies;
using RedMagic.Fx;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Un asset <see cref="BossAttack"/> → la <b>carta JSON</b> que lee la web (el bloque
    /// <c>attacks.&lt;id&gt;</c> de un EnemyConfig / BossConfig): la otra mitad de
    /// <see cref="BossConfigImporter.WriteAttacks"/>. Los campos salen por reflexión con las mismas
    /// reglas que <see cref="BossCatalogExporter"/> publica en el catálogo (mismos campos, mismos
    /// ocultos, mismos tipos), así que no puede desincronizarse de él.
    ///
    /// Lo usa el puente Unity ⇄ web (<c>WebBridgeRoutes</c>): la web abre un enemigo con los números
    /// que tiene de verdad en Unity, y al guardar los escribe en <b>los mismos assets</b>
    /// (<c>assetPath</c>).
    ///
    /// <b>Arte.</b> Un hueco de arte (GameObject) sale como <c>{ "project": "&lt;ruta del prefab&gt;" }</c>
    /// (con <c>libraryId</c> si el prefab se construyó de una entrada de la biblioteca web: sirve de
    /// vista previa). La web lo devuelve tal cual y <see cref="BossArtBuilder.ResolveArtRefs"/> lo
    /// vuelve a convertir en la misma referencia: lo que no se cambió en la web no se regenera.
    /// </summary>
    public static class BossAttackExporter
    {
        private static readonly HashSet<string> NoHidden = new HashSet<string>();

        /// <summary>La carta de un ataque: <c>type</c>, <c>assetPath</c>, campos base y <c>params</c>.</summary>
        public static JObject Card(BossAttack attack)
        {
            var type = attack.GetType();
            var hidden = new HashSet<string>(type.GetCustomAttributes(typeof(AttackHidesAttribute), true)
                .Cast<AttackHidesAttribute>().SelectMany(a => a.Paths));

            var card = new JObject { ["type"] = type.Name, ["assetPath"] = AssetDatabase.GetAssetPath(attack) };
            var parameters = new JObject();

            foreach (var field in BossCatalogExporter.SerializableFields(type))
            {
                bool common = field.DeclaringType == typeof(BossAttack);
                if (common && !BossCatalogExporter.CommonFields.Contains(field.Name)) continue;

                var value = Value(field.FieldType, field.GetValue(attack), field.Name, common ? NoHidden : hidden, 0);
                if (value == null) continue;
                if (common) card[field.Name] = value;
                else parameters[field.Name] = value;
            }

            if (parameters.Count > 0) card["params"] = parameters;
            return card;
        }

        /// <summary>
        /// Completa el EnemyConfig que escribe <c>RigConfigExporter.EnemyConfig</c> — que sólo trae la
        /// ruta de cada ataque — con las cartas enteras: añade el bloque <c>attacks</c> y cambia las
        /// rutas de <c>tuning.attacks[].attack</c> y <c>tuning.defences[].attack</c> por sus ids.
        /// </summary>
        public static void FillDeck(GameObject root, JObject enemyConfig)
        {
            var stats = root.GetComponent<EnemyStats>();
            if (stats == null || !(enemyConfig?["tuning"] is JObject tuning)) return;

            string enemyName = enemyConfig.Value<string>("enemyName") ?? root.name;
            var ids = new Dictionary<string, string>();   // ruta del asset → id
            var attacks = new JObject();

            string IdOf(BossAttack attack)
            {
                string path = AssetDatabase.GetAssetPath(attack);
                if (ids.TryGetValue(path, out var known)) return known;

                // "EnemyAttack_<enemigo>_<id>" es como los nombra el importador: el id vuelve a ser el de la web.
                string name = Path.GetFileNameWithoutExtension(path);
                string prefix = $"EnemyAttack_{enemyName}_";
                string id = name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : name;
                id = Regex.Replace(id, "[^A-Za-z0-9_-]", "_");
                if (id.Length == 0 || !char.IsLetter(id[0])) id = "A" + id;
                string unique = id;
                for (int n = 2; attacks[unique] != null; n++) unique = $"{id}{n}";

                ids[path] = unique;
                attacks[unique] = Card(attack);
                return unique;
            }

            var deck = new JArray();
            foreach (var entry in stats.Tuning.attacks ?? new List<EnemyDeckEntry>())
            {
                if (entry == null || entry.attack == null || !AssetDatabase.Contains(entry.attack)) continue;
                deck.Add(new JObject
                {
                    ["attack"] = IdOf(entry.attack),
                    ["minRange"] = Round(entry.minRange),
                    ["maxRange"] = Round(entry.maxRange),
                    ["targetHeight"] = entry.targetHeight.ToString(),
                });
            }

            tuning["attacks"] = deck;

            // Las defensas: el mismo objeto que ya escribió el exportador, con el ataque por id.
            var defences = stats.Tuning.defences ?? new List<EnemyDefence>();
            if (tuning["defences"] is JArray defencesJson)
                for (int i = 0, written = 0; i < defences.Count && written < defencesJson.Count; i++)
                {
                    if (defences[i] == null) continue;
                    var json = defencesJson[written++] as JObject;
                    if (json == null) continue;
                    var attack = defences[i].attack;
                    if (attack != null && AssetDatabase.Contains(attack)) json["attack"] = IdOf(attack);
                    else json.Remove("attack");
                }

            if (attacks.Count > 0) enemyConfig["attacks"] = attacks;
        }

        /// <summary>
        /// El id de la biblioteca web del que salió un prefab de arte, o null: el marcador
        /// <see cref="WebLibrarySource"/>, o la carpeta de sus frames (<c>…/Art/&lt;libraryId&gt;/…</c>,
        /// donde los deja el import de un bundle).
        /// </summary>
        public static string ArtLibraryId(GameObject prefab)
        {
            if (prefab == null) return null;
            var marker = prefab.GetComponent<WebLibrarySource>();
            if (marker != null && !string.IsNullOrEmpty(marker.LibraryId)) return marker.LibraryId;

            var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            string path = renderer != null && renderer.sprite != null ? AssetDatabase.GetAssetPath(renderer.sprite) : null;
            if (string.IsNullOrEmpty(path)) return null;
            // La ÚLTIMA carpeta Art/ de la ruta (la del bundle), no la "Assets/Art" de arriba del todo.
            var match = Regex.Match(path, "^Assets/.+/Art/([^/]+)/");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>Un prefab de arte del proyecto como lo entiende la web (<c>project</c> + <c>libraryId</c> si se sabe).</summary>
        public static JObject ArtRef(GameObject prefab)
        {
            var o = new JObject { ["project"] = AssetDatabase.GetAssetPath(prefab) };
            string libraryId = ArtLibraryId(prefab);
            if (!string.IsNullOrEmpty(libraryId)) o["libraryId"] = libraryId;
            return o;
        }

        // ============================================================ valores

        /// <summary>Un valor como lo lee <c>SerializedFieldWriter</c>. Null = no viaja (sólo de Unity, oculto, vacío).</summary>
        private static JToken Value(Type ft, object value, string path, HashSet<string> hidden, int depth)
        {
            if (hidden.Contains(path) || depth > 8) return null;

            if (ft == typeof(float)) return Round((float)value);
            if (ft == typeof(double)) return Math.Round((double)value, 5);
            if (ft == typeof(int)) return (int)value;
            if (ft == typeof(bool)) return (bool)value;
            if (ft == typeof(string)) return (string)value ?? "";
            if (ft == typeof(Vector2) || ft == typeof(Vector3))
            {
                Vector2 v = ft == typeof(Vector2) ? (Vector2)value : (Vector2)(Vector3)value;
                return new JObject { ["x"] = Round(v.x), ["y"] = Round(v.y) };
            }

            if (ft == typeof(Color)) return "#" + ColorUtility.ToHtmlStringRGBA((Color)value).ToLowerInvariant();
            if (ft.IsEnum) return value != null ? value.ToString() : null;

            if (ft == typeof(GameObject))
            {
                var prefab = value as GameObject;
                return prefab != null && AssetDatabase.Contains(prefab) ? ArtRef(prefab) : null;
            }

            if (typeof(Object).IsAssignableFrom(ft)) return null;   // sólo se asigna en Unity

            if (ft == typeof(float[]) || ft == typeof(List<float>))
                return new JArray(((IEnumerable)value ?? Array.Empty<float>()).Cast<float>().Select(Round).Cast<object>().ToArray());
            if (ft == typeof(int[]) || ft == typeof(List<int>))
                return new JArray(((IEnumerable)value ?? Array.Empty<int>()).Cast<int>().Cast<object>().ToArray());

            if (BossCatalogExporter.ElementClass(ft) is Type element)
            {
                var array = new JArray();
                if (value is IEnumerable items)
                    foreach (var item in items)
                        array.Add(item != null ? Fields(element, item, $"{path}[]", hidden, depth + 1) : new JObject());
                return array;
            }

            if (ft.IsArray || (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))) return null;

            if (ft.IsClass && ft.IsDefined(typeof(SerializableAttribute), false))
                return value != null ? Fields(ft, value, path, hidden, depth + 1) : null;

            return null;
        }

        private static JObject Fields(Type type, object instance, string path, HashSet<string> hidden, int depth)
        {
            var o = new JObject();
            foreach (FieldInfo child in BossCatalogExporter.SerializableFields(type))
            {
                var v = Value(child.FieldType, child.GetValue(instance), $"{path}.{child.Name}", hidden, depth);
                if (v != null) o[child.Name] = v;
            }

            return o;
        }

        private static double Round(float v) => Math.Round((double)v, 5);
    }
}
