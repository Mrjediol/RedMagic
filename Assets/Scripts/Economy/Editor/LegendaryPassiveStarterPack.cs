using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Economy.EditorTools
{
    /// <summary>
    /// Rellena <c>Assets/Resources/LegendaryPassives/</c> con las 9 pasivas legendarias del espejo
    /// (id 0-8): nombre, descripción de cada nivel, mecánica (<see cref="LegendaryPassiveEffectKind"/>)
    /// e icono. Los efectos aún no están implementados (TODO en <see cref="LegendaryPassiveEffects"/>).
    ///
    /// <b>Iconos</b>: se buscan en <see cref="IconFolder"/> por nombre de fichero = nombre de la pasiva
    /// sin espacios ni mayúsculas (<c>Codex Aurum</c> → <c>CodexAurum.png</c>); una pasiva sin icono
    /// en la carpeta conserva el que tuviera.
    ///
    /// Idempotente en la <b>creación</b> del asset (nunca duplica ni borra), pero <b>sincroniza los
    /// campos de diseño</b> en cada pasada. Lo único que NUNCA toca al re-generar es
    /// <see cref="LegendaryPassive.isUnlocked"/> y <see cref="LegendaryPassive.currentLevel"/>,
    /// que son estado de una partida de prueba.
    /// </summary>
    public static class LegendaryPassiveStarterPack
    {
        private const string Folder = "Assets/Resources/LegendaryPassives";
        public const string IconFolder = "Assets/Art/Pasives";

        private readonly struct Entry
        {
            public readonly string name, description, upgradeDescription;
            public readonly LegendaryPassiveEffectKind kind;

            public Entry(string name, LegendaryPassiveEffectKind kind, string description, string upgradeDescription)
            {
                this.name = name;
                this.kind = kind;
                this.description = description;
                this.upgradeDescription = upgradeDescription;
            }
        }

        private static readonly Entry[] Entries =
        {
            new("Codex Aurum", LegendaryPassiveEffectKind.FreeItemAfterBoss,
                "A free item drops after defeating each world boss",
                "Two free items drop after defeating each world boss"),
            new("Tomo del Destino", LegendaryPassiveEffectKind.ItemChoiceOptions,
                "When picking an item, choose from 3 options instead of 1",
                "Also reroll those 3 options once for free"),
            new("Grimorio del Umbral", LegendaryPassiveEffectKind.ChestWeaponChoice,
                "See 3 weapons in the chest and choose which to take",
                "The chosen weapon starts with 1 upgrade already applied"),
            new("Páginas del Eco", LegendaryPassiveEffectKind.PermanentRerolls,
                "+2 permanent rerolls per run",
                "+4 permanent rerolls, rerolls show 2 options instead of 1"),
            new("El Libro Sin Nombre", LegendaryPassiveEffectKind.ReviveOnce,
                "Revive once per run at 30% HP",
                "On revive, also gain a shield that absorbs 3 hits"),
            new("Volumen Carmesí", LegendaryPassiveEffectKind.DamageBonus,
                "+10% damage permanently",
                "+20% damage permanently"),
            new("Anales del Vacío", LegendaryPassiveEffectKind.WeakenedEnemies,
                "First wave of each level starts with all enemies slowed",
                "All enemies in the run start with -20% HP"),
            new("Manuscrito Eterno", LegendaryPassiveEffectKind.PeriodicFreeItem,
                "Every 3 completed levels, receive a free item",
                "That free item is always epic or legendary"),
            new("El Tomo Roto", LegendaryPassiveEffectKind.ChestWeaponUpgrades,
                "Chest weapon starts with 2 upgrades applied",
                "Chest weapon is also always the highest base-damage weapon available"),
        };

        [MenuItem("Tools/RedMagic/Hub/Espejo · Generar pasivas legendarias", priority = 410)]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Resources", "LegendaryPassives");

            int created = 0, updated = 0, withIcon = 0;
            for (int id = 0; id < LegendaryPassiveLibrary.SlotCount && id < Entries.Length; id++)
            {
                string path = $"{Folder}/LegendaryPassive_{id}.asset";
                var entry = Entries[id];
                var passive = AssetDatabase.LoadAssetAtPath<LegendaryPassive>(path);
                bool isNew = passive == null;

                if (isNew)
                {
                    passive = ScriptableObject.CreateInstance<LegendaryPassive>();
                    passive.isUnlocked = false;
                    passive.currentLevel = 0;
                }

                passive.id = id;
                passive.displayName = entry.name;
                passive.description = entry.description;
                passive.upgradeDescription = entry.upgradeDescription;
                passive.effectKind = entry.kind;
                passive.maxLevel = 2;
                passive.upgradeCost = 50;

                var icon = FindIcon(entry.name);
                if (icon != null) passive.icon = icon;
                else Debug.LogWarning($"[LegendaryPassiveStarterPack] Sin icono para '{entry.name}' en '{IconFolder}'.");
                if (passive.icon != null) withIcon++;

                if (isNew)
                {
                    AssetDatabase.CreateAsset(passive, path);
                    created++;
                }
                else
                {
                    EditorUtility.SetDirty(passive);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            LegendaryPassiveLibrary.Invalidate();

            Debug.Log($"[LegendaryPassiveStarterPack] {created} creada(s), {updated} sincronizada(s), " +
                      $"{withIcon} con icono, en '{Folder}'.");
        }

        /// <summary>Primer sprite del PNG de <see cref="IconFolder"/> cuyo nombre coincide con la pasiva.</summary>
        private static Sprite FindIcon(string passiveName)
        {
            string key = Key(passiveName);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Key(System.IO.Path.GetFileNameWithoutExtension(path)) != key) continue;

                return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            }

            return null;
        }

        /// <summary>Sin espacios, minúsculas y en NFC, para que "Páginas del Eco" = "PáginasdelEco".</summary>
        private static string Key(string s) =>
            new string(s.Normalize(NormalizationForm.FormC).Where(c => !char.IsWhiteSpace(c)).ToArray())
                .ToLower(CultureInfo.InvariantCulture);
    }
}
