using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RedMagic.Economy;
using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// Importa <c>docs/schemas/enemy-config.schema.json</c>: crea/actualiza un
    /// <see cref="EnemyRecipe"/> con todos sus campos y llama a <see cref="EnemyFactory.Generate"/>
    /// para producir (o refrescar) el prefab, exactamente como lo haría alguien a mano desde
    /// <c>Tools ▸ RedMagic ▸ Pipeline ▸ 3</c>.
    ///
    /// La receta se guarda en <c>Assets/Art/Characters/&lt;enemyName&gt;/&lt;enemyName&gt;.enemy.asset</c>,
    /// la misma carpeta que ya usan las recetas escritas a mano (junto a su <c>.sheet.asset</c>) —
    /// así un enemigo generado por JSON es indistinguible en el Project de uno generado por un pack,
    /// y un re-import con el mismo <c>enemyName</c> actualiza la misma receta en vez de duplicarla.
    /// </summary>
    public static class EnemyConfigImporter
    {
        // La ficha SIEMPRE refleja el JSON completo (con los defaults de EnemyTuning donde el JSON
        // no dice nada): es EnemyFactory.Generate quien decide si esos valores se siembran de
        // verdad en el EnemyStats del prefab (sólo la primera vez, o con resetTuning) — no hace
        // falta que este importador duplique esa lógica de "no pisar lo afinado a mano".
        public static void Import(JObject root, ConfigImportReport report,
                                  Dictionary<string, JObject> projectileLibrary = null,
                                  bool resetTuning = false)
        {
            string enemyName = ConfigJson.RequireString(root, "enemyName", "EnemyConfig");

            var artToken = root["art"];
            if (artToken == null)
                throw new ConfigImportException($"EnemyConfig '{enemyName}': falta 'art' (SpriteSheetRecipe ya cortada).");
            var art = ConfigJson.ReadAsset<SpriteSheetRecipe>(artToken, $"{enemyName}.art");

            var tuning = BuildTuning(root["tuning"] as JObject, projectileLibrary, enemyName, report);
            ApplyEnemyStatsClamps(tuning, enemyName, report);

            string recipeFolder = $"Assets/Art/Characters/{enemyName}";
            string recipePath = $"{recipeFolder}/{enemyName}.enemy.asset";

            SheetSlicer.EnsureFolder(recipeFolder);
            var recipe = AssetDatabase.LoadAssetAtPath<EnemyRecipe>(recipePath);
            bool isNewRecipe = recipe == null;
            if (isNewRecipe)
            {
                recipe = ScriptableObject.CreateInstance<EnemyRecipe>();
                AssetDatabase.CreateAsset(recipe, recipePath);
            }

            recipe.enemyName = enemyName;
            recipe.art = art;
            if (root.TryGetValue("prefabFolder", out var pf) && pf.Type != JTokenType.Null)
                recipe.prefabFolder = pf.Value<string>();

            ApplyPresence(recipe, root["presence"] as JObject);
            ApplyProjectileArt(recipe, root["projectileArt"] as JObject);

            if (root.TryGetValue("tier", out var tierToken))
                recipe.tier = ConfigJson.ParseEnum<EnemyTier>(tierToken, $"{enemyName}.tier");

            recipe.tuning = tuning;

            EditorUtility.SetDirty(recipe);
            report.Add(isNewRecipe, recipePath);

            string prefabPath = recipe.PrefabPath;
            bool prefabExistedBefore = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;

            var log = new StringBuilder();
            var prefabGo = EnemyFactory.Generate(recipe, log, resetTuning);

            if (prefabGo != null) report.Add(!prefabExistedBefore, prefabPath);
            else report.Errors.Add($"{enemyName}: EnemyFactory.Generate no produjo el prefab.");

            if (log.Length > 0) report.Warnings.Add(log.ToString().TrimEnd());
        }

        // ============================================================ presencia / proyectil-art

        private static void ApplyPresence(EnemyRecipe recipe, JObject presence)
        {
            if (presence == null) return;

            if (presence.TryGetValue("spriteScale", out var t1)) recipe.spriteScale = t1.Value<float>();
            if (presence.TryGetValue("colliderSize", out var t2)) recipe.colliderSize = ConfigJson.ReadVector2(t2, recipe.colliderSize);
            if (presence.TryGetValue("colliderOffset", out var t3)) recipe.colliderOffset = ConfigJson.ReadVector2(t3, recipe.colliderOffset);
            if (presence.TryGetValue("sortingOrder", out var t4)) recipe.sortingOrder = t4.Value<int>();
            if (presence.TryGetValue("tag", out var t5)) recipe.tag = t5.Value<string>();
        }

        private static void ApplyProjectileArt(EnemyRecipe recipe, JObject projectileArt)
        {
            if (projectileArt == null) return;

            if (projectileArt.TryGetValue("propState", out var t1)) recipe.projectilePropState = t1.Value<string>();
            if (projectileArt.TryGetValue("scale", out var t2)) recipe.projectileScale = t2.Value<float>();
        }

        // ============================================================ tuning

        private static EnemyTuning BuildTuning(JObject json, Dictionary<string, JObject> library,
                                               string enemyName, ConfigImportReport report)
        {
            var t = new EnemyTuning(); // arranca en los defaults de la clase
            if (json == null) return t; // 'tuning' es opcional por completo

            string ctx = $"{enemyName}.tuning";

            if (json.TryGetValue("archetype", out var archetype))
                t.archetype = ConfigJson.ParseEnum<EnemyArchetype>(archetype, $"{ctx}.archetype");
            if (json.TryGetValue("staticAttack", out var staticAttack))
                t.staticAttack = ConfigJson.ParseEnum<AttackKind>(staticAttack, $"{ctx}.staticAttack");

            SetF(json, "maxHealth", v => t.maxHealth = v);
            SetF(json, "invulnerabilityDuration", v => t.invulnerabilityDuration = v);

            SetF(json, "knockbackHorizontal", v => t.knockbackHorizontal = v);
            SetF(json, "knockbackVertical", v => t.knockbackVertical = v);
            SetF(json, "knockbackDuration", v => t.knockbackDuration = v);
            SetF(json, "knockbackResistance", v => t.knockbackResistance = v);

            SetF(json, "detectionRange", v => t.detectionRange = v);
            SetF(json, "loseInterestGrace", v => t.loseInterestGrace = v);
            SetF(json, "attackRange", v => t.attackRange = v);
            SetF(json, "personalSpace", v => t.personalSpace = v);
            SetF(json, "retreatReleaseFactor", v => t.retreatReleaseFactor = v);
            SetF(json, "verticalTolerance", v => t.verticalTolerance = v);

            SetF(json, "moveSpeed", v => t.moveSpeed = v);
            SetF(json, "retreatSpeed", v => t.retreatSpeed = v);
            SetB(json, "stopAtLedges", v => t.stopAtLedges = v);
            SetF(json, "ledgeProbeDepth", v => t.ledgeProbeDepth = v);
            SetF(json, "gravityScale", v => t.gravityScale = v);

            SetB(json, "sleepsUntilDetected", v => t.sleepsUntilDetected = v);
            SetF(json, "hoverOffset", v => t.hoverOffset = v);

            SetF(json, "attackDamage", v => t.attackDamage = v);
            SetF(json, "attackCooldown", v => t.attackCooldown = v);
            SetF(json, "attackKnockbackMultiplier", v => t.attackKnockbackMultiplier = v);
            SetF(json, "attackReleaseFallback", v => t.attackReleaseFallback = v);
            SetB(json, "rootedWhileAttacking", v => t.rootedWhileAttacking = v);

            if (json.TryGetValue("meleeHitboxSize", out var mhs)) t.meleeHitboxSize = ConfigJson.ReadVector2(mhs, t.meleeHitboxSize);
            if (json.TryGetValue("meleeHitboxOffset", out var mho)) t.meleeHitboxOffset = ConfigJson.ReadVector2(mho, t.meleeHitboxOffset);

            SetB(json, "selfDestruct", v => t.selfDestruct = v);
            SetF(json, "explosionRadius", v => t.explosionRadius = v);
            SetF(json, "explosionShake", v => t.explosionShake = v);

            if (json.TryGetValue("projectile", out var projToken))
                t.projectile = ProjectileConfigImporter.Resolve(projToken, library, $"{ctx}.projectile");

            SetB(json, "aimAtTarget", v => t.aimAtTarget = v);
            if (json.TryGetValue("projectileSprite", out var sprite))
                t.projectileSprite = ConfigJson.ReadAsset<Sprite>(sprite, $"{ctx}.projectileSprite");
            if (json.TryGetValue("projectileTint", out var tint))
                t.projectileTint = ConfigJson.ReadColor(tint, t.projectileTint);

            SetF(json, "contactDamage", v => t.contactDamage = v);
            SetF(json, "contactDamageCooldown", v => t.contactDamageCooldown = v);
            SetF(json, "contactKnockbackMultiplier", v => t.contactKnockbackMultiplier = v);

            SetF(json, "idleAnimSpeed", v => t.idleAnimSpeed = v);
            SetF(json, "moveAnimSpeed", v => t.moveAnimSpeed = v);
            SetF(json, "attackAnimSpeed", v => t.attackAnimSpeed = v);
            SetF(json, "hurtAnimSpeed", v => t.hurtAnimSpeed = v);
            SetF(json, "deathAnimSpeed", v => t.deathAnimSpeed = v);
            SetF(json, "wakeAnimSpeed", v => t.wakeAnimSpeed = v);

            SetS(json, "targetTag", v => t.targetTag = v);

            if (json.TryGetValue("hitLayers", out var hitLayers))
                t.hitLayers = ConfigJson.ReadLayerMask(hitLayers, t.hitLayers, $"{ctx}.hitLayers");
            if (json.TryGetValue("obstacleLayers", out var obstacleLayers))
                t.obstacleLayers = ConfigJson.ReadLayerMask(obstacleLayers, t.obstacleLayers, $"{ctx}.obstacleLayers");

            return t;
        }

        private static void SetF(JObject json, string key, Action<float> apply)
        {
            if (json.TryGetValue(key, out var token) && token.Type != JTokenType.Null) apply(token.Value<float>());
        }

        private static void SetS(JObject json, string key, Action<string> apply)
        {
            if (json.TryGetValue(key, out var token) && token.Type != JTokenType.Null) apply(token.Value<string>());
        }

        private static void SetB(JObject json, string key, Action<bool> apply)
        {
            if (json.TryGetValue(key, out var token) && token.Type != JTokenType.Null) apply(token.Value<bool>());
        }

        // ============================================================ trampa de EnemyStats.OnValidate

        /// <summary>
        /// Reproduce a mano los dos ajustes de <see cref="EnemyStats.OnValidate"/> — NO se puede
        /// confiar en que Unity lo dispare solo: OnValidate corre "cuando se carga el script o se
        /// cambia un valor desde el Inspector", y aquí el tuning se escribe por código
        /// (EnemyStats.EditorSetTuning → asignación directa de campo), lo que no garantiza que
        /// OnValidate se ejecute en el mismo tick. Sin este paso, la ficha (y el resumen del
        /// import) podrían mentir sobre qué valores acaba teniendo de verdad el prefab.
        /// </summary>
        private static void ApplyEnemyStatsClamps(EnemyTuning tuning, string enemyName, ConfigImportReport report)
        {
            const float ApproachBandMargin = 2f; // igual que EnemyStats.ApproachBandMargin (privada)

            if (tuning.Moves)
            {
                float minDetection = tuning.attackRange + ApproachBandMargin;
                if (tuning.detectionRange < minDetection)
                {
                    report.Clamped.Add(
                        $"{enemyName}.tuning.detectionRange: json={tuning.detectionRange:0.##} -> " +
                        $"{minDetection:0.##} (EnemyStats.OnValidate exige attackRange + {ApproachBandMargin:0.##})");
                    tuning.detectionRange = minDetection;
                }
            }

            if (tuning.personalSpace > tuning.attackRange)
            {
                report.Clamped.Add(
                    $"{enemyName}.tuning.personalSpace: json={tuning.personalSpace:0.##} -> " +
                    $"{tuning.attackRange:0.##} (EnemyStats.OnValidate no permite más que attackRange)");
                tuning.personalSpace = tuning.attackRange;
            }
        }
    }
}
