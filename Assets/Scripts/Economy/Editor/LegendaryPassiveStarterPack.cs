using UnityEditor;
using UnityEngine;

namespace RedMagic.Economy.EditorTools
{
    /// <summary>
    /// Rellena <c>Assets/Resources/LegendaryPassives/</c> con las 9 pasivas legendarias placeholder
    /// del espejo (id 0-8) — un efecto real por pasiva (<see cref="LegendaryPassiveEffectKind"/>),
    /// 2 niveles (base / un upgrade que dobla el valor), 50 Calaveras el upgrade. Son "placeholder"
    /// en el sentido de que los números son de diseño provisional, no en que no hagan nada: cada
    /// una está enganchada a su sistema real (o marcada con TODO si el sistema no existe, ver
    /// <see cref="LegendaryPassiveEffects"/>).
    ///
    /// Idempotente en la <b>creación</b> del asset (nunca duplica ni borra), pero <b>sincroniza los
    /// campos de diseño</b> (nombre, descripciones, tipo de efecto, valor base, coste) en cada
    /// pasada — son constantes de balance, no algo que se ajuste a mano en el asset. Lo único que
    /// NUNCA toca en una pasada de re-generación es <see cref="LegendaryPassive.isUnlocked"/> y
    /// <see cref="LegendaryPassive.currentLevel"/>, que son estado de una partida de prueba.
    /// </summary>
    public static class LegendaryPassiveStarterPack
    {
        private const string Folder = "Assets/Resources/LegendaryPassives";

        private struct Entry
        {
            public string name, description, upgradeDescription;
            public LegendaryPassiveEffectKind kind;
            public float baseValue;

            public Entry(string name, string description, string upgradeDescription,
                         LegendaryPassiveEffectKind kind, float baseValue)
            {
                this.name = name;
                this.description = description;
                this.upgradeDescription = upgradeDescription;
                this.kind = kind;
                this.baseValue = baseValue;
            }
        }

        private static readonly Entry[] Entries =
        {
            new("Corazón de Hierro", "+5 Max HP", "+10 Max HP",
                LegendaryPassiveEffectKind.MaxHealth, 5f),
            new("Bolsa sin Fondo", "+1 Gold per kill", "+2 Gold per kill",
                LegendaryPassiveEffectKind.GoldPerKill, 1f),
            new("Botas Veloces", "+5% Movement Speed", "+10% Movement Speed",
                LegendaryPassiveEffectKind.MoveSpeed, 0.05f),
            new("Filo Brutal", "+3 Attack Damage", "+6 Attack Damage",
                LegendaryPassiveEffectKind.AttackDamage, 3f),
            new("Impulso Fantasma", "+5% Dash Speed", "+10% Dash Speed",
                LegendaryPassiveEffectKind.DashSpeed, 0.05f),
            new("Reloj Roto", "-10% Cooldowns", "-20% Cooldowns",
                LegendaryPassiveEffectKind.CooldownReduction, 0.10f),
            new("Piel de Piedra", "+2 Armor", "+4 Armor",
                LegendaryPassiveEffectKind.Armor, 2f),
            new("Sabiduría Antigua", "+5% XP Gain", "+10% XP Gain",
                LegendaryPassiveEffectKind.XpGain, 0.05f),
            new("Aliento Vital", "+1 HP Regen / 10s", "+2 HP Regen / 10s",
                LegendaryPassiveEffectKind.HpRegen, 1f),
        };

        [MenuItem("Tools/RedMagic/Hub/Espejo · Generar pasivas legendarias (placeholder)", priority = 410)]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Resources", "LegendaryPassives");

            int created = 0, updated = 0;
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
                passive.baseValue = entry.baseValue;
                passive.maxLevel = 2;
                passive.upgradeCost = 50;

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

            Debug.Log($"[LegendaryPassiveStarterPack] {created} creada(s), {updated} sincronizada(s) en " +
                      $"'{Folder}' ({LegendaryPassiveLibrary.SlotCount} casillas en total).");
        }
    }
}
