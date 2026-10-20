using System;
using System.Collections.Generic;
using System.Reflection;
using RedMagic.Economy;
using RedMagic.Items;
using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>Qué hace un nodo del árbol permanente de un arma.</summary>
    public enum MasteryKind { Damage, Cooldown, Signature, Unlock }

    /// <summary>Un nodo del árbol permanente de un arma (se deduce del kit, no se guarda en ningún asset).</summary>
    public readonly struct MasteryNode
    {
        public readonly string id;
        public readonly MasteryKind kind;
        public readonly int maxLevel;
        /// <summary>Unlock: evolución (1..3) y mejora (1..2; la 0 viene de serie).</summary>
        public readonly int evo, upgrade;

        public MasteryNode(string id, MasteryKind kind, int maxLevel, int evo = 0, int upgrade = 0)
        {
            this.id = id; this.kind = kind; this.maxLevel = maxLevel; this.evo = evo; this.upgrade = upgrade;
        }
    }

    /// <summary>
    /// Mejoras permanentes de cada arma del arsenal (fuera de la run; se pagan con fragmentos de alma y se guardan en
    /// <c>PlayerPrefs</c>). El árbol de cada arma sale solo de su kit — el mismo para todas, sin cablear nada:
    /// <list type="bullet">
    /// <item><b>Poder</b>: Daño (campos <see cref="ArsenalDamageAttribute"/> del kit) → Recarga (<see cref="ArsenalCooldownAttribute"/>).</item>
    /// <item><b>Firma</b>: la estadística propia del arma (<see cref="ArsenalSignatureAttribute"/>: esquirlas del Abanico…).</item>
    /// <item>Una fila por evolución: desbloquear su mejora 2 → su mejora 3 (la 1 viene de serie).</item>
    /// </list>
    /// Dentro de una fila, un nodo pide el anterior a nivel 1 (como el árbol del hub). Los números y precios, en
    /// <see cref="WeaponMasteryConfig"/>. Se aplica a la copia del kit de cada partida (<see cref="ArsenalRuntime"/>).
    /// </summary>
    public static class WeaponMastery
    {
        private const string PrefPrefix = "mastery.";

        /// <summary>(id del arma, id del nodo). Lo escucha el menú para repintarse.</summary>
        public static event Action<string, string> Changed;

        private static readonly Dictionary<string, int> Levels = new Dictionary<string, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Levels.Clear(); Changed = null; }

        // ------------------------------------------------------------------ árbol

        /// <summary>Las filas del árbol de <paramref name="kit"/>.</summary>
        public static List<List<MasteryNode>> Rows(ArsenalKit kit)
        {
            var cfg = WeaponMasteryConfig.Instance;
            var rows = new List<List<MasteryNode>>
            {
                new List<MasteryNode>
                {
                    new MasteryNode("damage", MasteryKind.Damage, cfg.damageLevels),
                    new MasteryNode("cooldown", MasteryKind.Cooldown, cfg.cooldownLevels),
                },
            };
            var sig = Signature(kit, out _);
            if (sig != null) rows.Add(new List<MasteryNode> { new MasteryNode("signature", MasteryKind.Signature, sig.levels) });
            for (int e = 1; e < kit.Tree.evolutions.Count; e++)
                rows.Add(new List<MasteryNode>
                {
                    new MasteryNode($"e{e}u1", MasteryKind.Unlock, 1, e, 1),
                    new MasteryNode($"e{e}u2", MasteryKind.Unlock, 1, e, 2),
                });
            return rows;
        }

        public static int Level(string weaponId, string nodeId)
        {
            string key = PrefPrefix + weaponId + "." + nodeId;
            if (!Levels.TryGetValue(key, out int lvl)) { lvl = PlayerPrefs.GetInt(key, 0); Levels[key] = lvl; }
            return lvl;
        }

        /// <summary>¿Se puede comprar ya (el anterior de su fila tiene nivel ≥ 1)?</summary>
        public static bool IsAvailable(string weaponId, List<MasteryNode> row, int column) =>
            column == 0 || Level(weaponId, row[column - 1].id) >= 1;

        public static int NextCost(MasteryNode node, int level)
        {
            var cfg = WeaponMasteryConfig.Instance;
            return node.kind switch
            {
                MasteryKind.Damage => cfg.damageCost + cfg.damageCostStep * level,
                MasteryKind.Cooldown => cfg.cooldownCost + cfg.cooldownCostStep * level,
                MasteryKind.Signature => cfg.signatureCost + cfg.signatureCostStep * level,
                _ => node.upgrade == 1 ? cfg.unlockSecondCost : cfg.unlockThirdCost,
            };
        }

        public static bool TryBuy(ArsenalKit kit, List<MasteryNode> row, int column)
        {
            var node = row[column];
            int lvl = Level(kit.WeaponId, node.id);
            if (lvl >= node.maxLevel || !IsAvailable(kit.WeaponId, row, column)) return false;
            var wallet = CurrencyManager.Instance;
            if (wallet == null || !wallet.TrySpend(UpgradeManager.Cost, NextCost(node, lvl))) return false;

            string key = PrefPrefix + kit.WeaponId + "." + node.id;
            Levels[key] = lvl + 1;
            PlayerPrefs.SetInt(key, lvl + 1);
            PlayerPrefs.Save();
            ArsenalRuntime.RefreshMastery();
            Changed?.Invoke(kit.WeaponId, node.id);
            return true;
        }

        /// <summary>¿Se puede conseguir en la run la mejora <paramref name="upgrade"/> (0..2) de la evolución <paramref name="evo"/>?</summary>
        public static bool IsUpgradeUnlocked(string weaponId, int evo, int upgrade) =>
            upgrade == 0 || Level(weaponId, $"e{evo}u{upgrade}") >= 1;

        /// <summary>Texto de un nodo para el menú: título y efecto.</summary>
        public static void Describe(ArsenalKit kit, MasteryNode node, out string title, out string effect)
        {
            var cfg = WeaponMasteryConfig.Instance;
            switch (node.kind)
            {
                case MasteryKind.Damage:
                    title = Loc.Get("mastery.damage");
                    effect = Loc.Get("mastery.damage_desc", Mathf.RoundToInt(cfg.damagePerLevel * 100f));
                    return;
                case MasteryKind.Cooldown:
                    title = Loc.Get("mastery.cooldown");
                    effect = Loc.Get("mastery.cooldown_desc", Mathf.RoundToInt(cfg.cooldownPerLevel * 100f));
                    return;
                case MasteryKind.Signature:
                    var sig = Signature(kit, out _);
                    title = Loc.GetOr($"mastery.signature.{kit.WeaponId}", "Firma");
                    effect = sig == null ? "" : sig.multiplier
                        ? Loc.Get("mastery.signature_pct", Mathf.RoundToInt(sig.perLevel * 100f))
                        : Loc.Get("mastery.signature_add", sig.perLevel);
                    return;
                default:
                    title = ArsenalTexts.UpgradeName(kit, node.evo, node.upgrade);
                    effect = Loc.Get("mastery.unlock_desc");
                    return;
            }
        }

        // ------------------------------------------------------------------ aplicar

        /// <summary>Escribe en <paramref name="kit"/> (una copia, nunca el asset) lo comprado para su arma.</summary>
        public static void Apply(ArsenalKit kit)
        {
            var cfg = WeaponMasteryConfig.Instance;
            string id = kit.WeaponId;
            float damage = 1f + cfg.damagePerLevel * Level(id, "damage");
            float cooldown = Mathf.Max(0.2f, 1f - cfg.cooldownPerLevel * Level(id, "cooldown"));
            int signature = Level(id, "signature");

            foreach (var f in kit.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(float))
                {
                    if (damage != 1f && f.IsDefined(typeof(ArsenalDamageAttribute), false)) f.SetValue(kit, (float)f.GetValue(kit) * damage);
                    if (cooldown != 1f && f.IsDefined(typeof(ArsenalCooldownAttribute), false)) f.SetValue(kit, (float)f.GetValue(kit) * cooldown);
                }
                if (signature <= 0) continue;
                var sig = f.GetCustomAttribute<ArsenalSignatureAttribute>();
                if (sig == null) continue;
                if (f.FieldType == typeof(int)) f.SetValue(kit, (int)f.GetValue(kit) + Mathf.RoundToInt(sig.perLevel * signature));
                else if (f.FieldType == typeof(float))
                    f.SetValue(kit, sig.multiplier ? (float)f.GetValue(kit) * (1f + sig.perLevel * signature)
                                                   : (float)f.GetValue(kit) + sig.perLevel * signature);
            }
        }

        private static ArsenalSignatureAttribute Signature(ArsenalKit kit, out FieldInfo field)
        {
            foreach (var f in kit.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var sig = f.GetCustomAttribute<ArsenalSignatureAttribute>();
                if (sig != null) { field = f; return sig; }
            }
            field = null;
            return null;
        }
    }

    /// <summary>
    /// Números y precios de las mejoras permanentes de armas (<see cref="WeaponMastery"/>), en
    /// <c>Resources/WeaponMasteryConfig.asset</c>. Precios en fragmentos de alma.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Armas/Mejoras permanentes", fileName = "WeaponMasteryConfig")]
    public class WeaponMasteryConfig : ScriptableObject
    {
        [Tooltip("Armas con pestaña en el menú de mejoras del hub, en este orden.")]
        public List<WeaponDefinition> weapons = new List<WeaponDefinition>();

        [Header("Daño")]
        [Tooltip("+ daño por nivel (0,08 = +8 %).")] [Min(0f)] public float damagePerLevel = 0.08f;
        [Min(1)] public int damageLevels = 5;
        [Min(0)] public int damageCost = 15;
        [Tooltip("Lo que sube el precio por nivel ya comprado.")] [Min(0)] public int damageCostStep = 10;

        [Header("Recarga")]
        [Tooltip("− recarga por nivel (0,05 = −5 %).")] [Min(0f)] public float cooldownPerLevel = 0.05f;
        [Min(1)] public int cooldownLevels = 4;
        [Min(0)] public int cooldownCost = 20;
        [Min(0)] public int cooldownCostStep = 12;

        [Header("Firma (la estadística propia de cada arma)")]
        [Min(0)] public int signatureCost = 40;
        [Min(0)] public int signatureCostStep = 30;

        [Header("Mejoras de evolución (la 1.ª de cada evolución viene de serie)")]
        [Min(0)] public int unlockSecondCost = 60;
        [Min(0)] public int unlockThirdCost = 90;

        private static WeaponMasteryConfig _instance;

        public static WeaponMasteryConfig Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<WeaponMasteryConfig>("WeaponMasteryConfig");
                if (_instance == null) _instance = CreateInstance<WeaponMasteryConfig>();   // valores por defecto
                return _instance;
            }
        }
    }

    /// <summary>Campo de daño de un kit: lo sube la mejora permanente de Daño.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ArsenalDamageAttribute : Attribute { }

    /// <summary>Campo de recarga (segundos) de un kit: lo baja la mejora permanente de Recarga.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ArsenalCooldownAttribute : Attribute { }

    /// <summary>
    /// La estadística propia del arma (una por kit) que sube la mejora permanente de Firma: entero = + <see cref="perLevel"/>
    /// por nivel (esquirlas); decimal = × (1 + <see cref="perLevel"/>·nivel) si <see cref="multiplier"/>, si no + por nivel.
    /// El nombre que ve el jugador es la clave <c>mastery.signature.&lt;id del arma&gt;</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ArsenalSignatureAttribute : Attribute
    {
        public readonly float perLevel;
        public readonly int levels;
        public readonly bool multiplier;

        public ArsenalSignatureAttribute(float perLevel, int levels, bool multiplier = false)
        {
            this.perLevel = perLevel; this.levels = levels; this.multiplier = multiplier;
        }
    }

    /// <summary>
    /// Los textos de los árboles de las armas que ve el jugador (elegir evolución o mejora, menú de mejoras): claves
    /// <c>arsenal.&lt;id&gt;.evo&lt;e&gt;.name|desc</c> y <c>arsenal.&lt;id&gt;.evo&lt;e&gt;.up&lt;u&gt;.name|desc</c>, con el texto de
    /// <see cref="ArsenalTree"/> si falta la clave. Claves compuestas a propósito (6 armas × 25 textos): están todas en
    /// es.txt y en.txt.
    /// </summary>
    public static class ArsenalTexts
    {
        public static string EvolutionName(ArsenalKit kit, int e) =>
            Loc.GetOr($"arsenal.{kit.WeaponId}.evo{e}.name", kit.Tree.evolutions[e].name);

        public static string EvolutionDesc(ArsenalKit kit, int e) =>
            Loc.GetOr($"arsenal.{kit.WeaponId}.evo{e}.desc", kit.Tree.evolutions[e].desc);

        public static string UpgradeName(ArsenalKit kit, int e, int u) =>
            Loc.GetOr($"arsenal.{kit.WeaponId}.evo{e}.up{u}.name", kit.Tree.evolutions[e].ups[u]);

        public static string UpgradeDesc(ArsenalKit kit, int e, int u) =>
            Loc.GetOr($"arsenal.{kit.WeaponId}.evo{e}.up{u}.desc", kit.Tree.evolutions[e].upDescs[u]);
    }
}
