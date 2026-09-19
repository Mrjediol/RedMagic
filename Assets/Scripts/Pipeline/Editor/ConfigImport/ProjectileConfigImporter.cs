using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using RedMagic.Abilities;
using RedMagic.Bosses;
using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Importa <c>docs/schemas/projectile-config.schema.json</c>. Mirrors <see cref="ProjectileSpec"/>
    /// campo a campo — ver el $comment de "SCOPE CORRECTION" del schema: 'damage', 'hitLayers' y
    /// 'knockbackMultiplier' NO están aquí porque no son datos del spec, son argumentos por disparo
    /// (<c>EnemyAttack.Shoot</c> los pasa sueltos a <c>ProjectileFactory.Spawn</c>), y
    /// 'destroyWhenDone' es un campo de <c>Projectile</c>, no de <c>ProjectileSpec</c>.
    ///
    /// <b>Dos formas de uso, igual que el propio schema:</b>
    ///  - <b>Biblioteca</b> (<c>{"projectiles": {id: spec}}</c>): no escribe ningún asset por sí
    ///    sola — un <see cref="ProjectileSpec"/> no es un <c>ScriptableObject</c>, así que no hay
    ///    ningún archivo "sólo proyectil" al que grabarla. Se carga en memoria y se referencia con
    ///    <c>use</c> desde el <c>tuning.projectile</c> de un EnemyConfig o desde el <c>params</c> de
    ///    un BossAttack (ver <see cref="Resolve"/>), pasándola como "biblioteca de proyectiles" en
    ///    <see cref="ConfigImportWindow"/>.
    ///  - <b>Bloque suelto</b>: si se importa directamente (sin ir embebido en otro config), se
    ///    aplica sobre el asset seleccionado en el Project — un <c>EnemyRecipe</c>
    ///    (<c>tuning.projectile</c>) o un <c>BossAttack</c> con campo <c>projectile</c>
    ///    (<c>BulletHellAttack</c>, <c>OrbRingAttack</c>). Esto es lo que el schema llama "asset
    ///    compartido standalone": aplicar el mismo spec ya resuelto sobre el target que se tenga
    ///    seleccionado, en vez de inventar un tipo de asset "proyectil suelto" que no existe en el
    ///    runtime.
    ///
    ///    <b>Escribir sobre "lo que esté seleccionado" es peligroso sin salvaguardas</b> — una
    ///    selección equivocada pisaría el ProjectileSpec de un asset que no tenía nada que ver.
    ///    Por eso <see cref="ResolveApplyTarget"/> valida la selección ANTES de tocar nada (exactamente
    ///    un objeto, de un tipo que este importador sabe escribir) y <see cref="ApplyToSelection"/>
    ///    pide confirmación explícita nombrando el asset exacto antes de escribir.
    /// </summary>
    public static class ProjectileConfigImporter
    {
        // ============================================================ biblioteca

        public static Dictionary<string, JObject> LoadLibraryFromFile(string path)
        {
            var json = File.ReadAllText(path);
            return LoadLibrary(JObject.Parse(json));
        }

        /// <summary>Acepta tanto <c>{"projectiles": {...}}</c> como un mapa id→spec desnudo.</summary>
        public static Dictionary<string, JObject> LoadLibrary(JObject root)
        {
            var container = root["projectiles"] as JObject ?? root;
            var map = new Dictionary<string, JObject>();

            foreach (var prop in container.Properties())
                if (prop.Value is JObject spec) map[prop.Name] = spec;

            return map;
        }

        // ============================================================ resolución

        /// <summary>
        /// Resuelve un bloque ProjectileConfig (con 'use' opcional) contra la biblioteca dada y
        /// devuelve un <see cref="ProjectileSpec"/> en memoria. No toca ningún asset — sólo lo
        /// usan <c>EnemyConfigImporter</c> (para <c>tuning.projectile</c>) y
        /// <c>BossConfigImporter</c> (para los campos <c>ProjectileSpec</c> embebidos de
        /// <c>BulletHellAttack</c>/<c>OrbRingAttack</c>) antes de escribirlo de verdad.
        /// </summary>
        public static ProjectileSpec Resolve(JToken specToken, Dictionary<string, JObject> library, string context)
        {
            if (specToken == null || specToken.Type == JTokenType.Null) return new ProjectileSpec();

            var specObj = ConfigJson.AsObject(specToken, context);

            JObject baseObj = null;
            if (specObj.TryGetValue("use", out var useToken) && useToken.Type == JTokenType.String)
            {
                string id = useToken.Value<string>();
                if (library == null || !library.TryGetValue(id, out baseObj))
                    throw new ConfigImportException(
                        $"{context}: 'use: \"{id}\"' no se pudo resolver — no se cargó ninguna " +
                        "biblioteca de proyectiles con ese id (asígnala en el importador).");
            }

            var spec = new ProjectileSpec();
            if (baseObj != null) ApplyFields(spec, baseObj, context);
            ApplyFields(spec, specObj, context); // los campos propios pisan a los de la biblioteca

            return spec;
        }

        private static void ApplyFields(ProjectileSpec spec, JObject obj, string context)
        {
            if (obj.TryGetValue("prefab", out var prefab))
                spec.prefab = ConfigJson.ReadAsset<GameObject>(prefab, $"{context}.prefab");

            // 'libraryId' es la vía perezosa: en vez de una ruta a un prefab que alguien tuvo que
            // importar antes, nombra una entrada de la biblioteca web y el prefab se construye (o
            // se recupera) aquí mismo. Es lo único que este importador hace con arte — los números
            // de abajo siguen siendo exactamente los de siempre.
            //
            // Una ruta explícita GANA sobre el id: la regla de esta tarea es que un proyectil que
            // ya apuntaba a un prefab colocado a mano siga comportándose igual, así que un JSON con
            // los dos campos (sólo posible escribiéndolo a mano — la web emite uno u otro) conserva
            // el comportamiento viejo y avisa, en vez de que el builder pise silenciosamente una
            // referencia deliberada.
            if (obj.TryGetValue("libraryId", out var libraryIdToken) && libraryIdToken.Type == JTokenType.String)
            {
                string libraryId = libraryIdToken.Value<string>();

                if (spec.prefab != null)
                {
                    Debug.LogWarning($"{context}: se han dado 'prefab' y 'libraryId' a la vez; manda " +
                                     $"el prefab explícito y se ignora libraryId '{libraryId}'.");
                }
                else if (!string.IsNullOrWhiteSpace(libraryId))
                {
                    var log = new System.Text.StringBuilder();
                    var built = FxPrefabBuilder.BuildOrGetProjectilePrefab(libraryId, log);
                    if (log.Length > 0) Debug.Log(log.ToString().TrimEnd());

                    if (built != null) spec.prefab = built;
                    else
                        throw new ConfigImportException(
                            $"{context}: 'libraryId: \"{libraryId}\"' no se pudo materializar — no hay " +
                            "ninguna fuente (manifest.json + PNG) en disco para esa entrada. Impórtala " +
                            "con Tools > Web > Import Config... > Importar proyectil/VFX..., o exporta el " +
                            "enemigo como bundle combinado para que venga incluida.");
                }
            }

            if (obj.TryGetValue("speed", out var speed))
                spec.speed = speed.Value<float>();
            if (obj.TryGetValue("lifetime", out var lifetime))
                spec.lifetime = lifetime.Value<float>();
            if (obj.TryGetValue("size", out var size))
                spec.size = ConfigJson.ReadVector2(size, spec.size);
            if (obj.TryGetValue("muzzleOffset", out var muzzle))
                spec.muzzleOffset = ConfigJson.ReadVector2(muzzle, spec.muzzleOffset);
            if (obj.TryGetValue("pierce", out var pierce))
                spec.pierce = pierce.Value<int>();
            if (obj.TryGetValue("homingTurnRate", out var homingTurnRate))
                spec.homingTurnRate = homingTurnRate.Value<float>();
            if (obj.TryGetValue("homingRange", out var homingRange))
                spec.homingRange = homingRange.Value<float>();
            if (obj.TryGetValue("arcGravity", out var arcGravity))
                spec.arcGravity = arcGravity.Value<float>();
            if (obj.TryGetValue("impactRadius", out var impactRadius))
                spec.impactRadius = impactRadius.Value<float>();
            if (obj.TryGetValue("impactDamage", out var impactDamage))
                spec.impactDamage = impactDamage.Value<float>();
        }

        /// <summary>
        /// Escribe un ProjectileSpec ya resuelto dentro de un SerializedProperty de tipo
        /// ProjectileSpec (un campo embebido, p.ej. BulletHellAttack.projectile). Va campo a campo
        /// por FindPropertyRelative porque ProjectileSpec es una clase [Serializable] normal, no
        /// un ScriptableObject — no hay ningún "objectReferenceValue" al que asignarlo entero.
        /// </summary>
        public static void WriteInto(SerializedProperty projectileProp, ProjectileSpec spec)
        {
            projectileProp.FindPropertyRelative("prefab").objectReferenceValue = spec.prefab;
            projectileProp.FindPropertyRelative("speed").floatValue = spec.speed;
            projectileProp.FindPropertyRelative("lifetime").floatValue = spec.lifetime;
            projectileProp.FindPropertyRelative("size").vector2Value = spec.size;
            projectileProp.FindPropertyRelative("muzzleOffset").vector2Value = spec.muzzleOffset;
            projectileProp.FindPropertyRelative("pierce").intValue = spec.pierce;
            projectileProp.FindPropertyRelative("homingTurnRate").floatValue = spec.homingTurnRate;
            projectileProp.FindPropertyRelative("homingRange").floatValue = spec.homingRange;
            projectileProp.FindPropertyRelative("arcGravity").floatValue = spec.arcGravity;
            projectileProp.FindPropertyRelative("impactRadius").floatValue = spec.impactRadius;
            projectileProp.FindPropertyRelative("impactDamage").floatValue = spec.impactDamage;
        }

        // ============================================================ entrada directa (menú)

        /// <summary>Punto de entrada cuando el archivo importado ES un ProjectileConfig (no está embebido).</summary>
        public static void Import(JObject root, ConfigImportReport report)
        {
            bool isLibrary = root["projectiles"] is JObject;

            if (isLibrary)
            {
                var library = LoadLibrary(root);
                report.Warnings.Add(
                    $"Biblioteca de proyectiles: {library.Count} entradas ({string.Join(", ", library.Keys)}). " +
                    "No se escribe ningún asset por sí sola — un ProjectileSpec no es un asset " +
                    "independiente. Referéncialas con 'use' desde un EnemyConfig o BossConfig, " +
                    "cargando este archivo como 'biblioteca de proyectiles' en el importador.");
                return;
            }

            ApplyToSelection(root, report);
        }

        /// <summary>
        /// Resultado de validar la selección del Project como destino de un ProjectileConfig
        /// suelto. Separado de <see cref="ApplyToSelection"/> para que <c>ConfigImportWindow</c>
        /// pueda enseñar el mismo resultado ANTES de que el usuario pulse Importar — no sólo en el
        /// diálogo de confirmación de último momento.
        /// </summary>
        public readonly struct ApplyTargetResolution
        {
            public bool IsValid { get; }
            public UnityEngine.Object Target { get; }
            public string TargetLabel { get; }
            public string Error { get; }

            private ApplyTargetResolution(bool isValid, UnityEngine.Object target, string targetLabel, string error)
            {
                IsValid = isValid;
                Target = target;
                TargetLabel = targetLabel;
                Error = error;
            }

            public static ApplyTargetResolution Ok(UnityEngine.Object target, string targetLabel) =>
                new ApplyTargetResolution(true, target, targetLabel, null);

            public static ApplyTargetResolution Fail(string error) =>
                new ApplyTargetResolution(false, null, null, error);
        }

        /// <summary>
        /// Valida la selección actual del Project SIN escribir nada: exactamente un objeto
        /// seleccionado, y de un tipo al que este importador sabe escribir (un
        /// <see cref="EnemyRecipe"/>, o un <see cref="BossAttack"/> con campo <c>projectile</c>).
        /// </summary>
        public static ApplyTargetResolution ResolveApplyTarget()
        {
            var selection = Selection.objects;

            if (selection == null || selection.Length == 0)
                return ApplyTargetResolution.Fail("No hay ningún asset seleccionado en el Project.");

            if (selection.Length > 1)
                return ApplyTargetResolution.Fail(
                    $"Hay {selection.Length} assets seleccionados; selecciona exactamente uno.");

            var target = selection[0];

            switch (target)
            {
                case EnemyRecipe recipe:
                    return ApplyTargetResolution.Ok(recipe, "EnemyRecipe (tuning.projectile)");

                case BossAttack attack:
                {
                    var prop = new SerializedObject(attack).FindProperty("projectile");
                    if (prop == null)
                        return ApplyTargetResolution.Fail(
                            $"'{attack.name}' ({attack.GetType().Name}) no tiene un campo 'projectile' " +
                            "al que escribir (sólo lo llevan BulletHellAttack y OrbRingAttack).");

                    return ApplyTargetResolution.Ok(attack, $"{attack.GetType().Name} (projectile)");
                }

                default:
                    return ApplyTargetResolution.Fail(
                        $"'{target.name}' ({target.GetType().Name}) no es un EnemyRecipe ni un " +
                        "BossAttack con campo 'projectile'.");
            }
        }

        /// <summary>
        /// Valida la selección, pide confirmación nombrando el asset exacto, y sólo entonces
        /// escribe. No escribe nada ante una selección inválida ni ante una cancelación — ver el
        /// comentario de clase.
        /// </summary>
        private static void ApplyToSelection(JObject specJson, ConfigImportReport report)
        {
            var resolution = ResolveApplyTarget();
            if (!resolution.IsValid)
            {
                report.Errors.Add($"Config de proyectil suelto: {resolution.Error}");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(resolution.Target);

            bool confirmed = EditorUtility.DisplayDialog(
                "Import Config — proyectil suelto",
                $"¿Aplicar este ProjectileConfig a '{resolution.Target.name}' ({resolution.TargetLabel})?\n\n{assetPath}",
                "Aplicar", "Cancelar");

            if (!confirmed)
            {
                report.Warnings.Add(
                    $"Import cancelado por el usuario (destino: '{resolution.Target.name}', {resolution.TargetLabel}).");
                return;
            }

            var spec = Resolve(specJson, null, "projectile");

            switch (resolution.Target)
            {
                case EnemyRecipe recipe:
                    recipe.tuning ??= new EnemyTuning();
                    recipe.tuning.projectile = spec;
                    EditorUtility.SetDirty(recipe);
                    break;

                case BossAttack attack:
                {
                    var so = new SerializedObject(attack);
                    WriteInto(so.FindProperty("projectile"), spec);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(attack);
                    break;
                }
            }

            report.AddProjectileTarget(assetPath, resolution.TargetLabel);
        }
    }
}
