using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RedMagic.Pipeline.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Bosses.EditorTools
{
    /// <summary>
    /// Importa <c>docs/schemas/boss-config.schema.json</c>: crea/actualiza los <see cref="BossAttack"/>
    /// que declara <c>attacks</c> y el <see cref="BossDefinition"/> con sus <see cref="BossPhase"/>.
    ///
    /// El conjunto de arquetipos es <b>abierto</b> a propósito (ver el $comment "OPEN ARCHETYPE SET"
    /// del schema): <c>type</c> se resuelve contra cualquier clase que herede de
    /// <see cref="BossAttack"/> en el proyecto vía reflexión, así que un arquetipo nuevo no toca ni
    /// este archivo ni el schema — sólo el .cs del arquetipo.
    ///
    /// Los assets viven en <c>Assets/Resources/Bosses/</c>, la misma carpeta que
    /// <see cref="BossStarterPack"/> y el resto de packs escritos a mano usan — así un jefe
    /// generado por JSON es indistinguible en el Project de uno generado por un pack.
    /// </summary>
    public static class BossConfigImporter
    {
        private const string BossFolder = "Assets/Resources/Bosses";

        public static void Import(JObject root, ConfigImportReport report,
                                  Dictionary<string, JObject> projectileLibrary = null)
        {
            string displayName = ConfigJson.RequireString(root, "displayName", "BossConfig");

            var attacksJson = root["attacks"] as JObject;
            if (attacksJson == null || attacksJson.Count == 0)
                throw new ConfigImportException($"BossConfig '{displayName}': 'attacks' está vacío o falta.");

            var phasesJson = root["phases"] as JArray;
            if (phasesJson == null || phasesJson.Count == 0)
                throw new ConfigImportException($"BossConfig '{displayName}': 'phases' está vacío o falta.");

            // --- Trampa obligatoria #2: rechazar, no reescribir en silencio. -----------------
            // BossDefinition.OnValidate fuerza phases[0].startsAtHealth a 1 sin decírselo a nadie.
            // Si se dejara pasar un valor distinto, el asset terminaría en desacuerdo con el JSON
            // que lo generó y el import mentiría sobre lo que de verdad escribió.
            var firstPhase = ConfigJson.AsObject(phasesJson[0], "phases[0]");
            if (firstPhase.TryGetValue("startsAtHealth", out var firstHealthToken))
            {
                float firstHealth = firstHealthToken.Value<float>();
                if (Mathf.Abs(firstHealth - 1f) > 0.0001f)
                    throw new ConfigImportException(
                        $"BossConfig '{displayName}': phases[0].startsAtHealth = {firstHealth}, pero " +
                        "BossDefinition.OnValidate fuerza la primera fase a 1 siempre. Pon 1 (o quita " +
                        "el campo) en vez de escribir un valor que el asset no va a conservar.");
            }

            // --- Pase 1: resolver TODOS los tipos de ataque ANTES de tocar el AssetDatabase. -
            // 'fallar alto' significa fallar antes de escribir un asset, no a medio escribirlo.
            var resolvedTypes = new Dictionary<string, Type>();
            foreach (var prop in attacksJson.Properties())
            {
                var entry = ConfigJson.AsObject(prop.Value, $"attacks.{prop.Name}");
                if (entry.ContainsKey("asset")) continue; // referencia a un asset existente: no crea nada

                string typeName = ConfigJson.RequireString(entry, "type", $"attacks.{prop.Name}");
                resolvedTypes[prop.Name] = ResolveAttackType(typeName, $"attacks.{prop.Name}");
            }

            // --- Arte de los ataques (opcional): se construye tras validar los tipos (fallar antes de escribir) y antes de los ataques, porque los ataques lo
            // referencian con {"art": "id"}, que aquí se cambia por la ruta del prefab construido.
            string slug = Slugify(displayName);
            ResolveRelativeFolders(root, slug);
            var log = new StringBuilder();
            Dictionary<string, string> artMap = null;
            if (root["artAssets"] is JObject artAssets)
            {
                artMap = BossArtBuilder.Build(slug, artAssets, log);
                foreach (var path in artMap.Values) report.Add(false, path);
            }
            BossArtBuilder.ResolveArtRefs(attacksJson, artMap, report.Warnings, "attacks");
            if (root["phases"] != null) BossArtBuilder.ResolveArtRefs(root["phases"], artMap, report.Warnings, "phases");

            // --- Pase 2: crear/actualizar cada BossAttack. -----------------------------------
            SheetSlicer.EnsureFolder(BossFolder);
            var attacksByKey = new Dictionary<string, BossAttack>();

            foreach (var prop in attacksJson.Properties())
            {
                string id = prop.Name;
                var entry = (JObject)prop.Value;
                string ctx = $"attacks.{id}";

                if (entry.TryGetValue("asset", out var assetToken))
                {
                    // Referencia a un asset ya existente: se usa tal cual, no se toca ni un campo
                    // — así una carta afinada a mano no se revierte por estar en un deck.
                    attacksByKey[id] = ConfigJson.ReadAsset<BossAttack>(assetToken, ctx);
                    continue;
                }

                var type = resolvedTypes[id];
                string assetPath = entry.TryGetValue("assetPath", out var apToken)
                    ? apToken.Value<string>()
                    : $"{BossFolder}/BossAttack_{id}.asset";

                var attack = LoadOrCreateAttack(type, assetPath, ctx, report);
                WriteAttackFields(attack, entry, projectileLibrary, ctx, report);
                attacksByKey[id] = attack;
            }

            // --- BossDefinition ---------------------------------------------------------------
            string definitionPath = $"{BossFolder}/Boss_{slug}.asset";

            var definition = AssetDatabase.LoadAssetAtPath<BossDefinition>(definitionPath);
            bool isNewDefinition = definition == null;
            if (isNewDefinition)
            {
                definition = ScriptableObject.CreateInstance<BossDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            var so = new SerializedObject(definition);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("title").stringValue = root.TryGetValue("title", out var titleT) ? titleT.Value<string>() : "";
            so.FindProperty("description").stringValue = root.TryGetValue("description", out var descT) ? descT.Value<string>() : "";

            var phasesProp = so.FindProperty("phases");
            phasesProp.arraySize = phasesJson.Count;
            for (int i = 0; i < phasesJson.Count; i++)
            {
                var phaseJson = ConfigJson.AsObject(phasesJson[i], $"phases[{i}]");
                WriteBossPhase(phasesProp.GetArrayElementAtIndex(i), phaseJson, attacksByKey, $"phases[{i}]", report);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            report.Add(isNewDefinition, definitionPath);

            // --- Cuerpo (opcional): clips + controller + prefab jugable, enlazado a la definición.
            if (root["body"] is JObject body)
            {
                AssetDatabase.SaveAssets();
                BossBodyBuilder.Build(slug, body, definition, artMap, report, log);
            }

            if (log.Length > 0) Debug.Log($"[BossConfigImporter] '{displayName}':\n{log}");
        }

        // ============================================================ resolución de tipo

        private static Type ResolveAttackType(string typeName, string context)
        {
            var candidates = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(t => t.Name == typeName && typeof(BossAttack).IsAssignableFrom(t))
                .ToList();

            if (candidates.Count == 0)
                throw new ConfigImportException(
                    $"{context}: 'type' = '{typeName}' no es ninguna clase que herede de BossAttack en el proyecto.");

            if (candidates.Count > 1)
                throw new ConfigImportException(
                    $"{context}: '{typeName}' es ambiguo — hay {candidates.Count} clases con ese " +
                    $"nombre ({string.Join(", ", candidates.Select(t => t.FullName))}).");

            var type = candidates[0];
            if (type.IsAbstract)
                throw new ConfigImportException($"{context}: '{typeName}' es abstracta, no se puede instanciar.");

            return type;
        }

        private static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        // ============================================================ BossAttack

        private static BossAttack LoadOrCreateAttack(Type type, string assetPath, string context, ConfigImportReport report)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BossAttack>(assetPath);
            if (existing != null)
            {
                if (existing.GetType() != type)
                    throw new ConfigImportException(
                        $"{context}: '{assetPath}' ya existe como {existing.GetType().Name}, pero el " +
                        $"JSON pide {type.Name}. No se puede cambiar el tipo de un asset existente — " +
                        "usa otro 'assetPath' o borra el asset a mano.");

                report.Add(false, assetPath);
                return existing;
            }

            var created = (BossAttack)ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(created, assetPath);
            report.Add(true, assetPath);
            return created;
        }

        private static readonly string[] BaseAttackFields =
        {
            "displayName", "description", "accent", "telegraph", "recovery", "weight",
            "cooldownInAttacks", "cooldownSeconds", "minPhase", "damage", "knockbackMultiplier",
            "vulnerableSeconds", "vulnerableMultiplier", "gesture", "shakeAmplitude", "shakeDuration",
        };

        private static void WriteAttackFields(BossAttack attack, JObject entry,
                                              Dictionary<string, JObject> projectileLibrary,
                                              string context, ConfigImportReport report)
        {
            var so = new SerializedObject(attack);

            foreach (var field in BaseAttackFields)
            {
                if (!entry.TryGetValue(field, out var token) || token.Type == JTokenType.Null) continue;
                // Los 17 campos base son garantizados por BossAttack: un fallo aquí es un bug
                // nuestro, no un dato de usuario mal escrito, así que se reporta como aviso y no
                // se mezcla con las claves de 'params' (que sí pueden fallar por typos del autor).
                WriteField(so, field, token, projectileLibrary, $"{context}.{field}", report.Warnings);
            }

            if (entry.TryGetValue("params", out var paramsToken) && paramsToken is JObject paramsObj)
            {
                foreach (var prop in paramsObj.Properties())
                    WriteField(so, prop.Name, prop.Value, projectileLibrary, $"{context}.params.{prop.Name}",
                              report.UnresolvedParams);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
        }

        /// <summary>
        /// Escribe un único campo serializado. Los <c>ProjectileSpec</c> embebidos
        /// (<c>BulletHellAttack.projectile</c>, <c>OrbRingAttack.projectile</c>) reciben trato
        /// especial: no son un tipo que <see cref="SerializedPropertyType"/> distinga por sí solo
        /// (son una clase [Serializable] normal) y sus valores pueden venir de una biblioteca vía
        /// 'use', igual que en <c>EnemyConfig.tuning.projectile</c>.
        /// </summary>
        private static void WriteField(SerializedObject so, string fieldName, JToken value,
                                       Dictionary<string, JObject> projectileLibrary, string context,
                                       List<string> unresolved)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                unresolved.Add($"{context}: el campo '{fieldName}' no existe en {so.targetObject.GetType().Name}.");
                return;
            }

            if (prop.propertyType == SerializedPropertyType.Generic && prop.type == "ProjectileSpec")
            {
                var spec = ProjectileConfigImporter.Resolve(value, projectileLibrary, context);
                ProjectileConfigImporter.WriteInto(prop, spec);
                return;
            }

            SerializedFieldWriter.TryWriteValue(prop, value, context, unresolved);
        }

        // ============================================================ BossPhase

        /// <summary>
        /// Escribe una BossPhase entera vía SerializedProperty, campo a campo — <b>NO</b> a través
        /// de <see cref="BossAuthoring.WritePhase"/>. WritePhase hardcodea 'transitionShake'
        /// (0.7f si hay transición, si no 0) y 'frenzySpeedScale' (1.35f fijo), ignorando lo que se
        /// le pase: es fontanería pensada para los packs escritos a mano, donde esos dos valores
        /// nunca importan. Un JSON que sí los da tiene que ganar, así que se escriben aquí
        /// directamente (trampa obligatoria #3).
        ///
        /// Un elemento de array recién crecido con 'arraySize' nace a CEROS — Unity no llama a los
        /// inicializadores de campo de C# —, así que cada campo se escribe explícitamente, incluidos
        /// los que "ya valdrían eso por defecto" (mismo motivo que documenta WritePhase).
        /// </summary>
        private static void WriteBossPhase(SerializedProperty phaseProp, JObject phaseJson,
                                           Dictionary<string, BossAttack> attacksByKey, string context,
                                           ConfigImportReport report)
        {
            string displayName = phaseJson.TryGetValue("displayName", out var dn) ? dn.Value<string>() : "Fase";
            float startsAtHealth = phaseJson.TryGetValue("startsAtHealth", out var sah) ? sah.Value<float>() : 1f;
            float damageScale = phaseJson.TryGetValue("damageScale", out var ds) ? ds.Value<float>() : 1f;
            float damageTaken = phaseJson.TryGetValue("damageTakenMultiplier", out var dtm) ? dtm.Value<float>() : 1f;
            float speedScale = phaseJson.TryGetValue("speedScale", out var sp) ? sp.Value<float>() : 1f;
            Color accent = ConfigJson.ReadColor(phaseJson["accent"], new Color(0.55f, 0.9f, 0.4f, 1f));
            Vector2 pause = ConfigJson.ReadVector2(phaseJson["pauseBetweenAttacks"], new Vector2(0.9f, 1.6f));
            float transitionSeconds = phaseJson.TryGetValue("transitionSeconds", out var ts) ? ts.Value<float>() : 1.8f;
            float transitionShake = phaseJson.TryGetValue("transitionShake", out var tsh) ? tsh.Value<float>() : 0.5f;
            float frenzyBelow = phaseJson.TryGetValue("frenzyBelowHealth", out var fb) ? fb.Value<float>() : 0f;
            float frenzySpeedScale = phaseJson.TryGetValue("frenzySpeedScale", out var fs) ? fs.Value<float>() : 1.35f;

            // damageTakenMultiplier a 0 (o negativo) deja al jefe invencible durante la fase.
            // BossDefinition.OnValidate lo repararía a 1 más tarde con un warning, pero eso deja el
            // asset en desacuerdo con lo que el JSON pedía — se aplica aquí el mismo mínimo que
            // BossAuthoring.WritePhase (Mathf.Max(0.01f, …)) ANTES de escribir.
            if (damageTaken < 0.01f)
            {
                report.Clamped.Add(
                    $"{context}.damageTakenMultiplier: json={damageTaken:0.##} -> 1 (0 deja al jefe " +
                    "invencible; ver BossDefinition.OnValidate)");
                damageTaken = 1f;
            }

            phaseProp.FindPropertyRelative("displayName").stringValue = displayName;
            phaseProp.FindPropertyRelative("startsAtHealth").floatValue = startsAtHealth;
            phaseProp.FindPropertyRelative("damageScale").floatValue = damageScale;
            phaseProp.FindPropertyRelative("damageTakenMultiplier").floatValue = damageTaken;
            phaseProp.FindPropertyRelative("speedScale").floatValue = speedScale;
            phaseProp.FindPropertyRelative("accent").colorValue = accent;
            phaseProp.FindPropertyRelative("pauseBetweenAttacks").vector2Value = pause;
            phaseProp.FindPropertyRelative("transitionSeconds").floatValue = transitionSeconds;

            // --- Los dos campos que BossAuthoring.WritePhase hardcodea (ver el doc de este método). ---
            phaseProp.FindPropertyRelative("transitionShake").floatValue = transitionShake;
            phaseProp.FindPropertyRelative("frenzySpeedScale").floatValue = frenzySpeedScale;

            phaseProp.FindPropertyRelative("frenzyBelowHealth").floatValue = frenzyBelow;

            if (phaseJson.TryGetValue("transitionFx", out var fxToken))
                phaseProp.FindPropertyRelative("transitionFx").objectReferenceValue =
                    ConfigJson.ReadAsset<GameObject>(fxToken, $"{context}.transitionFx");

            if (phaseJson.TryGetValue("openingAttack", out var openingToken) && openingToken.Type != JTokenType.Null)
            {
                string openingId = openingToken.Value<string>();
                if (!attacksByKey.TryGetValue(openingId, out var openingAttack))
                    throw new ConfigImportException($"{context}.openingAttack: '{openingId}' no está en 'attacks'.");
                phaseProp.FindPropertyRelative("openingAttack").objectReferenceValue = openingAttack;
            }

            var deckJson = phaseJson["attacks"] as JArray ?? new JArray();
            var deckProp = phaseProp.FindPropertyRelative("attacks");
            deckProp.arraySize = deckJson.Count;
            for (int i = 0; i < deckJson.Count; i++)
            {
                string id = deckJson[i].Value<string>();
                if (!attacksByKey.TryGetValue(id, out var deckAttack))
                    throw new ConfigImportException($"{context}.attacks[{i}]: '{id}' no está en 'attacks'.");
                deckProp.GetArrayElementAtIndex(i).objectReferenceValue = deckAttack;
            }
        }

        // ============================================================ carpetas relativas

        /// <summary>Donde deja <see cref="BossBundleImporter"/> los frames de un jefe exportado de la web.</summary>
        internal static string SourceFolderFor(string slug) => $"Assets/Art/Bosses/{slug}/Source";

        /// <summary>
        /// Las carpetas de arte que no empiezan por "Assets/" son relativas a
        /// <see cref="SourceFolderFor"/>: así el JSON que exporta la web ("Body", "Art/&lt;id&gt;") vale
        /// igual dentro del .zip que suelto, reimportado después para afinar números.
        /// </summary>
        private static void ResolveRelativeFolders(JObject root, string slug)
        {
            string baseFolder = SourceFolderFor(slug);

            string Fix(string folder)
            {
                if (string.IsNullOrWhiteSpace(folder)) return folder;
                folder = folder.Replace('\\', '/').Trim();
                return folder.StartsWith("Assets/", StringComparison.Ordinal) ? folder : $"{baseFolder}/{folder.TrimStart('/')}";
            }

            if (root["body"] is JObject body && body["folder"] != null)
                body["folder"] = Fix(body.Value<string>("folder"));

            if (root["artAssets"] is JObject art)
                foreach (var prop in art.Properties())
                    if (prop.Value is JObject entry && entry["folder"] != null)
                        entry["folder"] = Fix(entry.Value<string>("folder"));
        }

        // ============================================================ nombre de archivo

        /// <summary>
        /// "Árbol Ancestral" -> "ArbolAncestral", igual que los nombres de archivo que ya usan los
        /// packs escritos a mano (<c>Boss_ArbolAncestral.asset</c>). El schema no lleva un 'id' de
        /// jefe aparte — se deriva de 'displayName' para no inventar un campo que rompería el
        /// mapeo 1:1 con BossDefinition que exige docs/schemas/COMPATIBILITY.md.
        /// </summary>
        internal static string Slugify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "Boss";

            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }

            return sb.Length > 0 ? sb.ToString() : "Boss";
        }
    }
}
