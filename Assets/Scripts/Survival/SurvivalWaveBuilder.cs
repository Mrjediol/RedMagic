using System.Collections.Generic;
using RedMagic.Enemies;
using UnityEngine;

namespace RedMagic.Survival
{
    /// <summary>Un enemigo por salir: con qué reglas de sitio (tipo), cuál y, si toca, qué especial.</summary>
    public struct SurvivalSpawnRequest
    {
        public SurvivalSpawnKind kind;
        public GameObject prefab;
        public EnemyVariant special;
        /// <summary>Veces que no encontró sitio (las simples lo reintentan, hasta un límite).</summary>
        public int failures;
    }

    /// <summary>
    /// Decide qué sale en una oleada, sin tocar la escena: de qué tipo (la proporción del config: 80 % voladores, 15 % suelo,
    /// 5 % torretas…), cuál de la lista del mundo por peso de rareza, y si sale como especial — sólo las especiales con
    /// dificultad ≤ la de la oleada, con probabilidad <see cref="SurvivalConfig.specialChance"/>. El orden sale barajado.
    /// </summary>
    public static class SurvivalWaveBuilder
    {
        private static readonly SurvivalSpawnKind[] Kinds = { SurvivalSpawnKind.Flying, SurvivalSpawnKind.Ground, SurvivalSpawnKind.Turret };
        private static readonly List<EnemyVariant> Allowed = new List<EnemyVariant>();
        private static readonly int[] Split = new int[3];
        private static readonly float[] Remainders = new float[3];

        /// <summary>Oleada simple (o la del jefe): la lista entera. <paramref name="countMultiplier"/> = el de la dificultad.</summary>
        public static List<SurvivalSpawnRequest> Build(SurvivalConfig config, int wave, System.Random rng, float countMultiplier = 1f)
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
            int total = Mathf.RoundToInt(config.enemiesPerWave.At(wave, stage.countMultiplier) * countMultiplier);
            SplitByPercent(config, stage, total);
            for (int k = 0; k < Kinds.Length; k++)
            {
                var pool = stage.Pool(Kinds[k]);
                for (int i = 0; i < Split[k]; i++)
                {
                    var entry = Pick(pool, config, rng);
                    if (entry == null) break;
                    list.Add(new SurvivalSpawnRequest { kind = Kinds[k], prefab = entry.prefab, special = PickSpecial(entry, difficulty, config, rng) });
                }
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        /// <summary>
        /// Reparte <paramref name="total"/> entre los tipos según su % (en <see cref="Split"/>): la parte entera de cada uno y
        /// lo que sobra, a los de mayor resto. Así 20 enemigos al 80/15/5 son exactamente 16 / 3 / 1.
        /// </summary>
        private static void SplitByPercent(SurvivalConfig config, SurvivalStage stage, int total)
        {
            float sum = 0f;
            for (int k = 0; k < Kinds.Length; k++) sum += KindWeight(config, stage, Kinds[k]);
            int given = 0;
            for (int k = 0; k < Kinds.Length; k++)
            {
                float exact = sum > 0f ? total * KindWeight(config, stage, Kinds[k]) / sum : 0f;
                Split[k] = Mathf.FloorToInt(exact);
                Remainders[k] = exact - Split[k];
                given += Split[k];
            }
            for (; given < total && sum > 0f; given++)
            {
                int best = 0;
                for (int k = 1; k < Kinds.Length; k++) if (Remainders[k] > Remainders[best]) best = k;
                Split[best]++;
                Remainders[best] = -1f;
            }
        }

        /// <summary>
        /// Oleada dinámica: el siguiente enemigo que sale. El tipo se sortea con la proporción del config como pesos; luego,
        /// como en las simples.
        /// </summary>
        public static bool PickOne(SurvivalConfig config, int wave, SurvivalStage stage, System.Random rng, out SurvivalSpawnRequest request)
        {
            request = default;
            if (stage == null) return false;
            float total = 0f;
            foreach (var kind in Kinds) total += KindWeight(config, stage, kind);
            if (total <= 0f) return false;

            double roll = rng.NextDouble() * total;
            foreach (var kind in Kinds)
            {
                float w = KindWeight(config, stage, kind);
                if (w <= 0f) continue;
                roll -= w;
                if (roll > 0) continue;
                var entry = Pick(stage.Pool(kind), config, rng);
                if (entry == null) return false;
                request = new SurvivalSpawnRequest { kind = kind, prefab = entry.prefab, special = PickSpecial(entry, config.Difficulty(wave), config, rng) };
                return true;
            }
            return false;
        }

        /// <summary>El % del tipo, o 0 si el mundo no tiene enemigos de ese tipo (su parte se reparte entre los otros).</summary>
        private static float KindWeight(SurvivalConfig config, SurvivalStage stage, SurvivalSpawnKind kind)
        {
            var pool = stage.Pool(kind);
            if (pool == null || !pool.Exists(e => e != null && e.prefab != null)) return 0f;
            return Mathf.Max(0f, config.KindPercent(kind));
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
