using System.Collections.Generic;
using System.Text;
using RedMagic.Bosses;
using RedMagic.Pipeline.EditorTools;
using RedMagic.Rigging;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Enemies.EditorTools
{
    /// <summary>
    /// Convierte las cartas "el ataque propio con otro gesto" (<c>builtIn</c>, de antes del 2026-10-02)
    /// de los prefabs de enemigo en ataques del catálogo: un <see cref="ReleaseAttack"/> por gesto, con
    /// una entrada por suelta de ese gesto que lanza lo mismo que lanzaba el ataque propio (su
    /// proyectil, o su golpe de melé) con sus mismos números. Idempotente: un prefab ya convertido no
    /// cambia.
    /// </summary>
    public static class BuiltInVariantMigration
    {
        [MenuItem("Tools/Web/Rig/Convertir variantes del ataque propio (prefabs antiguos)")]
        private static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder();
            int converted = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<EnemyStats>() == null) continue;

                // El texto del prefab dice si aún trae cartas del formato antiguo.
                string text = System.IO.File.ReadAllText(path);
                if (!text.Contains("builtIn: 1") && !text.Contains("legacyBuiltIn: 1")) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = Convert(root, asset.name, log);
                    if (n == 0) continue;

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    converted++;
                    log.AppendLine($"  {path}: {n} carta(s)");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            return $"[BuiltInVariantMigration] Prefabs convertidos: {converted}\n{log}";
        }

        private static int Convert(GameObject root, string enemyName, StringBuilder log)
        {
            var stats = root.GetComponent<EnemyStats>();
            var tuning = stats.Tuning;
            var rig = root.GetComponent<ProceduralRig>();

            var so = new SerializedObject(stats);
            var deck = so.FindProperty("tuning.attacks");
            if (deck == null) return 0;

            var made = new Dictionary<string, ReleaseAttack>();
            int count = 0;

            for (int i = 0; i < deck.arraySize; i++)
            {
                var entry = deck.GetArrayElementAtIndex(i);
                var legacy = entry.FindPropertyRelative("legacyBuiltIn");
                if (legacy == null || !legacy.boolValue) continue;

                string gesture = entry.FindPropertyRelative("legacyGesture").stringValue ?? "";
                float weight = Mathf.Max(0.01f, entry.FindPropertyRelative("legacyWeight").floatValue);

                string key = $"{gesture}|{weight:0.###}";
                if (!made.TryGetValue(key, out var attack))
                {
                    attack = CreateAttack(enemyName, gesture, weight, made.Count, tuning, rig);
                    made[key] = attack;
                    log.AppendLine($"    + {AssetDatabase.GetAssetPath(attack)}");
                }

                entry.FindPropertyRelative("attack").objectReferenceValue = attack;
                legacy.boolValue = false;
                entry.FindPropertyRelative("legacyGesture").stringValue = "";
                count++;
            }

            if (count == 0) return 0;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stats);
            if (root.GetComponent<EnemyAttackHost>() == null) root.AddComponent<EnemyAttackHost>();
            return count;
        }

        /// <summary>
        /// El ataque propio como <see cref="ReleaseAttack"/>: el aviso que tenía (<c>rig.windup</c>), una
        /// entrada por suelta del gesto, cada una con el proyectil (o el golpe) y el daño de siempre.
        /// </summary>
        private static ReleaseAttack CreateAttack(string enemyName, string gesture, float weight, int index,
                                                  EnemyTuning tuning, ProceduralRig rig)
        {
            string folder = $"{EnemyDeckImporter.AttackRoot}/{enemyName}";
            SheetSlicer.EnsureFolder(folder);

            string id = "Propio" + (string.IsNullOrWhiteSpace(gesture) ? "" : gesture) + (index > 0 ? (index + 1).ToString() : "");
            string assetPath = $"{folder}/EnemyAttack_{enemyName}_{id}.asset";

            var attack = AssetDatabase.LoadAssetAtPath<ReleaseAttack>(assetPath);
            if (attack == null)
            {
                attack = ScriptableObject.CreateInstance<ReleaseAttack>();
                AssetDatabase.CreateAsset(attack, assetPath);
            }

            var found = rig != null ? rig.FindGesture(gesture) : null;
            int releases = found != null ? Mathf.Max(1, found.EffectiveReleases.Count) : 1;
            float speed = Mathf.Max(0.01f, tuning.attackAnimSpeed);

            var so = new SerializedObject(attack);
            so.FindProperty("displayName").stringValue = string.IsNullOrWhiteSpace(gesture) ? "Ataque propio" : $"Ataque propio ({gesture})";
            so.FindProperty("telegraph").floatValue = (rig != null ? rig.windup : 0.4f) / speed;
            so.FindProperty("recovery").floatValue = found != null ? found.recover : (rig != null ? rig.recover : 0.45f);
            so.FindProperty("weight").floatValue = weight;
            so.FindProperty("cooldownInAttacks").intValue = 0;
            so.FindProperty("damage").floatValue = tuning.attackDamage;
            so.FindProperty("knockbackMultiplier").floatValue = tuning.attackKnockbackMultiplier;
            so.FindProperty("gesture").stringValue = gesture ?? "";
            so.FindProperty("accent").colorValue = tuning.projectileTint;
            so.FindProperty("projectileSprite").objectReferenceValue = tuning.projectileSprite;

            var shots = so.FindProperty("shots");
            shots.arraySize = releases;
            for (int i = 0; i < releases; i++)
            {
                // Un elemento recién crecido nace a ceros: se escriben todos sus campos.
                var shot = shots.GetArrayElementAtIndex(i);
                shot.FindPropertyRelative("kind").enumValueIndex =
                    (int)(tuning.IsRanged ? ReleaseShotKind.Projectile : ReleaseShotKind.Strike);
                shot.FindPropertyRelative("aim").enumValueIndex =
                    (int)(tuning.aimAtTarget ? ReleaseAim.Frozen : ReleaseAim.Forward);
                shot.FindPropertyRelative("angle").floatValue = 0f;
                ProjectileConfigImporter.WriteInto(shot.FindPropertyRelative("projectile"), tuning.projectile);
                shot.FindPropertyRelative("count").intValue = 5;
                shot.FindPropertyRelative("arcDegrees").floatValue = 50f;
                shot.FindPropertyRelative("spawnRadius").floatValue = 0f;
                shot.FindPropertyRelative("strikeSize").vector2Value = tuning.meleeHitboxSize;
                shot.FindPropertyRelative("strikeOffset").vector2Value = tuning.meleeHitboxOffset;
                shot.FindPropertyRelative("damage").floatValue = 0f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
            AssetDatabase.SaveAssetIfDirty(attack);
            return attack;
        }
    }
}
