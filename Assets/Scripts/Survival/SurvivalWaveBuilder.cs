using System.Collections.Generic;
using RedMagic.Enemies;
using UnityEngine;

namespace RedMagic.Survival
{
    /// <summary>Un enemigo por salir: dónde (tipo de punto), cuál y, si toca, qué especial.</summary>
    public struct SurvivalSpawnRequest
    {
        public SurvivalSpawnKind kind;
        public GameObject prefab;
        public EnemyVariant special;
    }

    /// <summary>
    /// Decide qué sale en una oleada, sin tocar la escena: cuántos de cada tipo (<see cref="SurvivalConfig.CountFor"/>),
    /// cuál de la lista del mundo por peso de rareza, y si sale como especial — sólo las especiales con dificultad ≤ la
    /// de la oleada, con probabilidad <see cref="SurvivalConfig.specialChance"/>. El orden sale barajado.
    /// </summary>
    public static class SurvivalWaveBuilder
    {
        private static readonly SurvivalSpawnKind[] Kinds = { SurvivalSpawnKind.Flying, SurvivalSpawnKind.Ground, SurvivalSpawnKind.Turret };
        private static readonly List<EnemyVariant> Allowed = new List<EnemyVariant>();

        public static List<SurvivalSpawnRequest> Build(SurvivalConfig config, int wave, System.Random rng)
        {
            var list = new List<SurvivalSpawnRequest>();
            var stage = config.StageFor(wave, out _, out bool bossWave);
            if (stage == null) return list;

            if (bossWave)
            {
                var boss = stage.bosses[rng.Next(stage.bosses.Count)];
                if (boss != null) list.Add(new SurvivalSpawnRequest { kind = SurvivalSpawnKind.Boss, prefab = boss });
                return list;
            }

            float difficulty = config.Difficulty(wave);
            foreach (var kind in Kinds)
            {
                var pool = stage.Pool(kind);
                if (pool == null || pool.Count == 0) continue;
                int count = config.CountFor(kind, wave, stage);
                for (int i = 0; i < count; i++)
                {
                    var entry = Pick(pool, config, rng);
                    if (entry == null) break;
                    list.Add(new SurvivalSpawnRequest { kind = kind, prefab = entry.prefab, special = PickSpecial(entry, difficulty, config, rng) });
                }
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        private static SurvivalEnemyEntry Pick(List<SurvivalEnemyEntry> pool, SurvivalConfig config, System.Random rng)
        {
            float total = 0f;
            foreach (var e in pool) if (e != null && e.prefab != null) total += config.RarityWeight(e.rarity);
            if (total <= 0f) return null;
            double roll = rng.NextDouble() * total;
            foreach (var e in pool)
            {
                if (e == null || e.prefab == null) continue;
                roll -= config.RarityWeight(e.rarity);
                if (roll <= 0) return e;
            }
            return pool.FindLast(e => e != null && e.prefab != null);
        }

        private static EnemyVariant PickSpecial(SurvivalEnemyEntry entry, float difficulty, SurvivalConfig config, System.Random rng)
        {
            Allowed.Clear();
            foreach (var v in entry.specials)
                if (v != null && v.difficulty <= difficulty + 0.0001f) Allowed.Add(v);
            if (Allowed.Count == 0 || rng.NextDouble() >= config.specialChance) return null;
            return Allowed[rng.Next(Allowed.Count)];
        }
    }
}
