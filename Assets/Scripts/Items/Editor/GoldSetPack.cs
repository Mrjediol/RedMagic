using System;
using System.Linq;
using System.Text;
using RedMagic.Economy;
using RedMagic.Gameplay;
using RedMagic.Localization.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Items.EditorTools
{
    /// <summary>
    /// Set de oro: los 8 items (Casco, Pechera, Espada, Escudo, Guanteletes, Botas, Amuleto, Anillo) con su
    /// mecánica real, la sinergia Oro y la marca de oro. <b>Tools ▸ RedMagic ▸ Items ▸ Set de oro · Generar</b>.
    ///
    /// Qué hace (re-ejecutable, nunca pisa lo afinado a mano):
    /// <list type="number">
    /// <item>Crea en <see cref="ItemsFolder"/> los items que falten con ficha (texto en español; la
    /// traducción va en los ficheros de idioma por su <c>textKey</c>), rareza, precio, tags (Oro + una
    /// universal), icono de <see cref="IconFolder"/> y efectos. Un item que ya existe sólo recibe lo que
    /// le falte: icono vacío, <c>textKey</c> vacío o sus efectos si aún no los lleva.</item>
    /// <item>Rellena el material de las auras de oro en <c>SynergyConfig ▸ Tuning</c> si está vacío y escribe
    /// el texto (reserva en español) de los umbrales de Oro con los números actuales.</item>
    /// </list>
    /// Un item nuevo del set: una llamada más a <see cref="Item"/>.
    /// </summary>
    public static class GoldSetPack
    {
        /// <summary>
        /// Carpeta del set. Va bajo <c>Resources/Items</c> porque <c>ItemLibrary</c> (tienda, pantalla de
        /// items, drops) encuentra los items escaneando esa carpeta: fuera de ella no aparecerían.
        /// </summary>
        public const string ItemsFolder = "Assets/Resources/Items/GoldSet";

        /// <summary>Arte del set: un PNG por item.</summary>
        public const string IconFolder = "Assets/Art/Icons/GoldSet";

        private const string AuraMaterialPath = "Assets/Art/Fx/Materials/Fx_SpriteAdditive.mat";

        private static readonly Color GoldAccent = new(1f, 0.8f, 0.3f);

        [MenuItem("Tools/RedMagic/Items/Set de oro · Generar")]
        public static void Build() => Debug.Log(Run());

        /// <summary>Punto de entrada también para <c>unity command run_script</c>.</summary>
        public static string Run()
        {
            var log = new StringBuilder("[GoldSetPack]\n");
            EnsureFolder(ItemsFolder);

            Item(log, "Item_CascoDeOro", "Casco de Oro", "Casco", ItemRarity.Blue, 60, BuildTag.Tank,
                 "Aumenta la moneda que sueltan los enemigos.",
                 new CurrencyDropBonusEffect { bonus = 0.25f });

            Item(log, "Item_PecheraDeOro", "Pechera de Oro", "Pechera", ItemRarity.Blue, 70, BuildTag.Tank,
                 "Otorga vida máxima extra y un pequeño bonus de oro por cada golpe que sobrevives.",
                 new MaxHealthBonusEffect { amount = 25f },
                 new CurrencyOnDamageTakenEffect { currency = Currency.Gold, amount = 2 });

            Item(log, "Item_EspadaDeOro", "Espada de Oro", "Espada", ItemRarity.Epic, 120, BuildTag.Lifesteal,
                 "Tus ataques hacen más daño y los enemigos que matas sueltan oro extra.",
                 new ShotDamageBonusEffect { bonus = 0.2f },
                 new CurrencyOnKillEffect { currency = Currency.Gold, amount = 3, chance = 1f });

            Item(log, "Item_EscudoDeOro", "Escudo de Oro", "Escudo", ItemRarity.Blue, 65, BuildTag.Tank,
                 "Bloquea daño y convierte parte de lo que bloquea en oro.",
                 new DamageReductionToCurrencyEffect
                 {
                     flatReduction = 3f, minimumDamage = 1f, currency = Currency.Gold, currencyPerBlockedDamage = 1f,
                 });

            Item(log, "Item_GuanteletesDeOro", "Guanteletes de Oro", "Guante", ItemRarity.Epic, 130, BuildTag.Haste,
                 "Aumenta la velocidad de ataque; cada pocos ataques dispara un proyectil dorado seguro.",
                 new PlayerStatMultiplierEffect { stat = PlayerStat.CooldownRate, multiplier = 1.2f },
                 new ForceGildedEveryNEffect { attacksPerGilded = 4 });

            Item(log, "Item_BotasDeOro", "Botas de Oro", "Botas", ItemRarity.Legendary, 200, BuildTag.Haste,
                 "Tus proyectiles pueden forjarse en oro. Los proyectiles dorados marcan a los enemigos y " +
                 "multiplican el oro que sueltan. Cuanto más daño hace un proyectil, más probable es que " +
                 "salga dorado.",
                 new GildedProjectilesEffect { baseChance = 0.05f, chancePerDamagePoint = 0.004f, maxChance = 0.5f });

            Item(log, "Item_AmuletoDeOro", "Amuleto de Oro", "Collar", ItemRarity.Epic, 110, BuildTag.Lifesteal,
                 "La suerte crece con tu riqueza: cuanto más oro llevas, mejores objetos ofrece la tienda.",
                 new ShopLuckFromGoldEffect());

            Item(log, "Item_AnilloDeOro", "Anillo de Oro", "Ring", ItemRarity.Common, 40, BuildTag.Reset,
                 "Otorga un reroll extra en cada tienda.",
                 new RerollsOnShopEntryEffect { rerolls = 1 });

            Synergy(log);

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ─── Items ────────────────────────────────────────────────────────────────────────────────

        private static void Item(StringBuilder log, string file, string displayName, string iconName, ItemRarity rarity,
                                 int price, BuildTag universal, string description, params ItemEffect[] effects)
        {
            string path = $"{ItemsFolder}/{file}.asset";
            var item = AssetDatabase.LoadAssetAtPath<FreePoolItemDefinition>(path);
            bool created = item == null;

            if (created)
            {
                item = ScriptableObject.CreateInstance<FreePoolItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            var signature = effects.Select(e => e.GetType()).ToArray();
            bool install = created || signature.Any(t => !item.Effects.Any(e => e != null && e.GetType() == t));

            var so = new SerializedObject(item);
            if (install)
            {
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("description").stringValue = description;
                so.FindProperty("rarity").intValue = (int)rarity;
                so.FindProperty("price").intValue = price;
                so.FindProperty("accent").colorValue = GoldAccent;

                var tags = so.FindProperty("tags");
                tags.arraySize = 2;
                tags.GetArrayElementAtIndex(0).intValue = (int)BuildTag.Gold;
                tags.GetArrayElementAtIndex(1).intValue = (int)universal;

                var list = so.FindProperty("effects");
                list.arraySize = effects.Length;
                for (int i = 0; i < effects.Length; i++)
                    list.GetArrayElementAtIndex(i).managedReferenceValue = effects[i];
            }

            var key = so.FindProperty("textKey");
            if (string.IsNullOrEmpty(key.stringValue))
                key.stringValue = "item." + LocalizationTools.Slug(so.FindProperty("displayName").stringValue);

            var iconProp = so.FindProperty("icon");
            bool iconSet = false;
            if (iconProp.objectReferenceValue == null)
            {
                var icon = LoadIcon(iconName);
                if (icon != null)
                {
                    iconProp.objectReferenceValue = icon;
                    iconSet = true;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);

            string what = created ? "Creado" : install ? "Efectos instalados" : "Ya al día";
            log.AppendLine($"  {what} {path} · {key.stringValue}{(iconSet ? " · icono asignado" : "")}");
            if (iconProp.objectReferenceValue == null)
                log.AppendLine($"    AVISO: {file} sin icono ({IconFolder}/{iconName}.png no encontrado).");
        }

        /// <summary>El sprite de <c>&lt;IconFolder&gt;/&lt;name&gt;.png</c> (el mayor si la textura está troceada).</summary>
        private static Sprite LoadIcon(string name) =>
            AssetDatabase.LoadAllAssetsAtPath($"{IconFolder}/{name}.png")
                         .OfType<Sprite>()
                         .OrderByDescending(s => s.rect.width * s.rect.height)
                         .FirstOrDefault();

        // ─── Sinergia ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Texto de reserva (español) de los umbrales de Oro con los números actuales del Tuning, y el
        /// material de las auras si falta. La traducción de cada umbral va en los ficheros de idioma
        /// (<c>synergy.gold.tier1..3</c>, con los mismos números: relanzar tras retocarlos y actualizar
        /// las claves).
        /// </summary>
        private static void Synergy(StringBuilder log)
        {
            var config = SynergyConfig.Instance;
            if (config == null)
            {
                log.AppendLine("  AVISO: falta Assets/Resources/SynergyConfig.asset; no se escribe la sinergia Oro.");
                return;
            }

            var t = config.Tuning;
            if (t.goldAuraMaterial == null)
            {
                t.goldAuraMaterial = AssetDatabase.LoadAssetAtPath<Material>(AuraMaterialPath);
                if (t.goldAuraMaterial != null) log.AppendLine("  SynergyConfig: material de las auras de oro asignado.");
            }

            config.SetTierText(BuildTag.Gold, 1,
                $"Toda moneda que sueltan los enemigos, +{t.gold2DropBonus * 100f:0}%.");
            config.SetTierText(BuildTag.Gold, 2,
                $"+{t.gold4GildChanceBonus * 100f:0}% de probabilidad de que cualquier proyectil salga dorado.");
            config.SetTierText(BuildTag.Gold, 3,
                $"La marca de oro multiplica el botín ×{t.markDropMultiplier + t.gold6MarkMultiplierBonus:0.#} " +
                $"(en vez de ×{t.markDropMultiplier:0.#}).");

            EditorUtility.SetDirty(config);
            log.AppendLine("  SynergyConfig: umbrales Oro 2/4/6 escritos.");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
