using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using RedMagic.Abilities;
using RedMagic.Bosses;
using RedMagic.Bosses.EditorTools;
using RedMagic.Enemies;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Pipeline.EditorTools
{
    /// <summary>
    /// La <b>baraja de ataques</b> de un enemigo normal dentro de un EnemyConfig
    /// (<c>docs/schemas/enemy-config.schema.json</c>):
    /// <list type="bullet">
    /// <item><c>attacks</c> — los ataques del catálogo, el <b>mismo bloque</b> que en un BossConfig
    /// (tipo + campos base + <c>params</c>). Sale un asset <see cref="BossAttack"/> por ataque, con el
    /// mismo código que los de un jefe (<see cref="BossConfigImporter"/>).</item>
    /// <item><c>artAssets</c> — el arte de esos ataques (<see cref="BossArtBuilder"/>), opcional.</item>
    /// <item><c>tuning.attacks</c> — las cartas: qué ataque y cuándo puede salir
    /// (<see cref="EnemyDeckEntry"/>). <c>tuning.builtInAttack</c> / <c>builtInWeight</c> dicen si
    /// el ataque propio entra en el sorteo.</item>
    /// </list>
    ///
    /// Lo usan los dos imports de enemigo: el de sprite sheet (<see cref="EnemyConfigImporter"/>) y
    /// el de partes (<c>RigBundleImporter</c>). Un config sin <c>tuning.attacks</c> devuelve null y
    /// el import es exactamente el de antes.
    /// </summary>
    public static class EnemyDeckImporter
    {
        /// <summary>Un asset por ataque, en una carpeta por enemigo.</summary>
        public const string AttackRoot = "Assets/ScriptableObjects/EnemyAttacks";

        /// <summary>Prefabs del arte de los ataques, junto al resto de FX del enemigo.</summary>
        public const string ArtPrefabRoot = "Assets/Prefabs/Fx/Enemies";

        /// <summary>La baraja leída de un config, lista para escribirla en un <see cref="EnemyTuning"/>.</summary>
        public sealed class Deck
        {
            public readonly List<EnemyDeckEntry> Entries = new List<EnemyDeckEntry>();
            public bool BuiltInAttack = true;
            public float BuiltInWeight = 1f;

            /// <summary>El config traía <c>tuning.attacks</c> (aunque vacío: eso borra la baraja).</summary>
            public bool HasDeck;

            /// <summary>
            /// El proyectil del ataque propio, ya resuelto (prefab de biblioteca incluido), o null si el
            /// config no trae <c>tuning.projectile</c>. Se escribe en cada import, como la baraja: el
            /// proyectil se elige en la web y, si sólo se aplicara al crear el prefab, cambiarlo
            /// después reimportando "conservando" lo afinado no haría nada.
            /// </summary>
            public ProjectileSpec Projectile;

            public void ApplyTo(EnemyTuning tuning)
            {
                if (Projectile != null) tuning.projectile = Projectile;
                if (!HasDeck) return;

                tuning.attacks = new List<EnemyDeckEntry>();
                foreach (var entry in Entries) tuning.attacks.Add(entry.Clone());
                tuning.builtInAttack = BuiltInAttack;
                tuning.builtInWeight = BuiltInWeight;
            }
        }

        /// <summary>
        /// Crea/actualiza los ataques y devuelve la baraja (y el proyectil del ataque propio), o null si
        /// el config no trae ni <c>tuning.attacks</c> ni <c>tuning.projectile</c>. <paramref name="artSourceFolder"/> (ruta <c>Assets/…</c>) es donde
        /// están los frames del arte: las carpetas relativas de <c>artAssets</c> se resuelven ahí.
        /// </summary>
        public static Deck Read(JObject root, string enemyName, string artSourceFolder, ConfigImportReport report,
                                Dictionary<string, JObject> projectileLibrary = null)
        {
            var tuningJson = root["tuning"] as JObject;
            var projectileToken = tuningJson?["projectile"];
            bool hasDeck = tuningJson?["attacks"] is JArray;
            if (!hasDeck && projectileToken == null) return null;
            var deckJson = tuningJson["attacks"] as JArray ?? new JArray();

            var deck = new Deck
            {
                HasDeck = hasDeck,
                Projectile = projectileToken != null
                    ? ProjectileConfigImporter.Resolve(projectileToken, projectileLibrary, $"{enemyName}.tuning.projectile")
                    : null,
                BuiltInAttack = tuningJson.Value<bool?>("builtInAttack") ?? true,
                BuiltInWeight = Mathf.Max(0.01f, tuningJson.Value<float?>("builtInWeight") ?? 1f),
            };

            // --- los ataques que define el config (los que apuntan a un asset existente no crean nada).
            var attacksByKey = new Dictionary<string, BossAttack>();
            if (root["attacks"] is JObject attacksJson && attacksJson.Count > 0)
            {
                // Tipos antes que nada: fallar antes de escribir un asset.
                var types = BossConfigImporter.ResolveAttackTypes(attacksJson);
                foreach (var pair in types)
                    if (pair.Value.IsDefined(typeof(BossOnlyAttribute), true))
                        throw new ConfigImportException(
                            $"{enemyName}.attacks.{pair.Key}: '{pair.Value.Name}' es sólo de jefes (usa fases, " +
                            "guardia, esbirros o blancos de arena); un enemigo normal no puede lanzarlo.");

                var log = new StringBuilder();
                Dictionary<string, string> artMap = null;
                if (root["artAssets"] is JObject artAssets)
                {
                    ResolveRelativeFolders(artAssets, artSourceFolder);
                    artMap = BossArtBuilder.Build(enemyName, artAssets, log, $"{ArtPrefabRoot}/{enemyName}");
                    foreach (var path in artMap.Values) report.Add(false, path);
                }

                BossArtBuilder.ResolveArtRefs(attacksJson, artMap, report.Warnings, "attacks");
                if (log.Length > 0) report.Warnings.Add(log.ToString().TrimEnd());

                attacksByKey = BossConfigImporter.WriteAttacks(attacksJson, types, $"{AttackRoot}/{enemyName}",
                                                               id => $"EnemyAttack_{enemyName}_{id}",
                                                               projectileLibrary, report);
            }

            // --- las cartas.
            for (int i = 0; i < deckJson.Count; i++)
            {
                string ctx = $"{enemyName}.tuning.attacks[{i}]";
                var entryJson = ConfigJson.AsObject(deckJson[i], ctx);

                // Variante del ataque propio: sin ataque de catálogo, con su gesto y su peso.
                if (entryJson.Value<bool?>("builtIn") == true)
                {
                    deck.Entries.Add(new EnemyDeckEntry
                    {
                        builtIn = true,
                        gesture = entryJson.Value<string>("gesture") ?? "",
                        weight = Mathf.Max(0.01f, entryJson.Value<float?>("weight") ?? 1f),
                        minRange = Mathf.Max(0f, entryJson.Value<float?>("minRange") ?? 0f),
                        maxRange = Mathf.Max(0f, entryJson.Value<float?>("maxRange") ?? 0f),
                        targetHeight = entryJson["targetHeight"] != null
                            ? ConfigJson.ParseEnum<AttackHeightRule>(entryJson["targetHeight"], $"{ctx}.targetHeight")
                            : AttackHeightRule.Any,
                    });
                    continue;
                }

                string reference = ConfigJson.RequireString(entryJson, "attack", ctx);

                BossAttack attack;
                if (reference.StartsWith("Assets/", StringComparison.Ordinal))
                    attack = ConfigJson.ReadAsset<BossAttack>(entryJson["attack"], $"{ctx}.attack");
                else if (!attacksByKey.TryGetValue(reference, out attack))
                    throw new ConfigImportException($"{ctx}.attack: '{reference}' no está en 'attacks'.");

                if (attack.GetType().IsDefined(typeof(BossOnlyAttribute), true))
                    throw new ConfigImportException($"{ctx}.attack: '{attack.GetType().Name}' es sólo de jefes.");

                var entry = new EnemyDeckEntry
                {
                    attack = attack,
                    minRange = Mathf.Max(0f, entryJson.Value<float?>("minRange") ?? 0f),
                    maxRange = Mathf.Max(0f, entryJson.Value<float?>("maxRange") ?? 0f),
                };
                if (entryJson["targetHeight"] != null)
                    entry.targetHeight = ConfigJson.ParseEnum<AttackHeightRule>(entryJson["targetHeight"], $"{ctx}.targetHeight");

                if (entry.maxRange > 0f && entry.maxRange < entry.minRange)
                {
                    report.Clamped.Add($"{ctx}.maxRange: json={entry.maxRange:0.##} -> {entry.minRange:0.##} (no puede ser menor que minRange)");
                    entry.maxRange = entry.minRange;
                }

                deck.Entries.Add(entry);
            }

            AssetDatabase.SaveAssets();
            return deck;
        }

        /// <summary>Escribe la baraja en el EnemyStats de un prefab ya generado, y le pone quien lance sus ataques.</summary>
        public static void ApplyToPrefab(string prefabPath, Deck deck)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ApplyToRoot(root, deck);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Lo mismo, sobre la raíz de un prefab que ya está abierto para editar.</summary>
        public static void ApplyToRoot(GameObject root, Deck deck)
        {
            var stats = root.GetComponent<EnemyStats>();
            if (stats == null) return;

            deck.ApplyTo(stats.Tuning);
            EditorUtility.SetDirty(stats);

            // EnemyAttack lo añadiría solo al arrancar; en el prefab se ve y deja poner arte a los avisos.
            if (deck.HasDeck && deck.Entries.Count > 0 && root.GetComponent<EnemyAttackHost>() == null)
                root.AddComponent<EnemyAttackHost>();
        }

        /// <summary>Las carpetas de arte que no empiezan por "Assets/" son relativas a la del bundle.</summary>
        private static void ResolveRelativeFolders(JObject artAssets, string baseFolder)
        {
            foreach (var prop in artAssets.Properties())
            {
                if (!(prop.Value is JObject entry) || entry["folder"] == null) continue;

                string folder = (entry.Value<string>("folder") ?? "").Replace('\\', '/').Trim();
                if (folder.Length == 0 || folder.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                entry["folder"] = $"{baseFolder}/{folder.TrimStart('/')}";
            }
        }
    }
}
