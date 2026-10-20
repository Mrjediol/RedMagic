using System;
using System.Collections.Generic;
using RedMagic.Bosses;
using RedMagic.Combat;
using RedMagic.Enemies;
using RedMagic.Items;
using RedMagic.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Cómo crece el arma <b>dentro</b> de una run (la de fuera es <see cref="WeaponMastery"/>):
    /// <list type="bullet">
    /// <item><b>Evolución — Prisma</b>: seguro, uno por hito (en el modo clásico, cada jefe derrotado; en Supervivencia, las
    /// rondas de <c>SurvivalConfig.evolutionAfterWaves</c>). Eliges qué arma evoluciona (si hay dos sin evolucionar) y una de
    /// <see cref="WeaponProgressionConfig.offerEvolutions"/> evoluciones al azar.</item>
    /// <item><b>Subida de nivel — Núcleo</b>: lo suelta entero un <b>portador</b> (<see cref="CoreCarrier"/>), un enemigo raro
    /// y brillante que se escapa si no lo matas a tiempo. Sube un nivel el arma que elijas, al momento: si no está
    /// evolucionada, evoluciona (como un Prisma); si lo está, una de <see cref="WeaponProgressionConfig.offerUpgrades"/>
    /// mejoras al azar entre las desbloqueadas (<see cref="WeaponMastery.IsUpgradeUnlocked"/>) que aún no tiene.</item>
    /// </list>
    /// Cuánto puede subir cada arma lo marcan las mejoras permanentes: de serie, evolución + su 1.ª mejora (2 niveles); con
    /// las dos de cada evolución desbloqueadas, 4. Cuando ninguna de las armas que llevas puede subir más
    /// (<see cref="NothingLeft"/>), el portador suelta fragmentos de alma (moneda permanente) en vez de núcleo.
    /// Núcleos enteros y no fragmentos a propósito: sumar fragmentos estrecha la curva (casi todas las runs acaban con lo
    /// mismo); sucesos raros que dan la mejora entera dan la que se busca — ~30 % sin mejoras, ~35 % una, ~22 % dos, ~13 %
    /// tres o más (Poisson de media ≈1,3 núcleos por run). Las elecciones las enseña <see cref="ArsenalProgressUi"/>.
    /// </summary>
    public static class WeaponRunProgress
    {
        /// <summary>Evoluciones ganadas y aún sin elegir.</summary>
        public static int PendingEvolutions { get; private set; }
        /// <summary>Núcleos ganados y aún sin usar (esperando el momento de elegir: sin menú abierto).</summary>
        public static int PendingCores { get; private set; }

        /// <summary>Algo ha cambiado (se ha ganado o gastado algo, o una partida nueva).</summary>
        public static event Action Changed;

        private static RunManager _run;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PendingEvolutions = PendingCores = 0;
            Changed = null;
            _run = null;
        }

        // ------------------------------------------------------------------ API

        /// <summary>Partida nueva: nada pendiente y las armas otra vez en su forma base (+ mejoras permanentes).</summary>
        public static void ResetRun()
        {
            PendingEvolutions = PendingCores = 0;
            ArsenalRuntime.Clear();
            Changed?.Invoke();
        }

        public static void GrantEvolution() { PendingEvolutions++; Changed?.Invoke(); }
        public static void GrantCore() { PendingCores++; Changed?.Invoke(); }

        internal static void ConsumeEvolution() { PendingEvolutions = Mathf.Max(0, PendingEvolutions - 1); Changed?.Invoke(); }
        internal static void ConsumeCore() { PendingCores = Mathf.Max(0, PendingCores - 1); Changed?.Invoke(); }

        /// <summary>Las armas del arsenal equipadas ahora (la activa y la de reserva).</summary>
        public static List<WeaponDefinition> Equipped()
        {
            var list = new List<WeaponDefinition>(2);
            var inv = WeaponLoadout.Instance != null ? WeaponLoadout.Instance.Inventory : null;
            if (inv == null) return list;
            if (inv.Weapon != null && inv.Weapon.IsArsenal) list.Add(inv.Weapon);
            if (inv.SecondWeapon != null && inv.SecondWeapon.IsArsenal && inv.SecondWeapon != inv.Weapon) list.Add(inv.SecondWeapon);
            return list;
        }

        public static bool CanEvolve(WeaponDefinition w) => ArsenalRuntime.For(w) is ArsenalKit k && k.evolution == 0;

        /// <summary>Las mejoras (0..2) que <paramref name="w"/> puede conseguir aún: de su evolución, desbloqueadas y sin tener.</summary>
        public static List<int> AvailableUpgrades(WeaponDefinition w)
        {
            var list = new List<int>(3);
            var k = ArsenalRuntime.For(w);
            if (k == null || k.evolution == 0) return list;
            for (int u = 0; u < 3; u++)
                if (!k.GetUpgrade(u) && WeaponMastery.IsUpgradeUnlocked(k.WeaponId, k.evolution, u)) list.Add(u);
            return list;
        }

        /// <summary>Niveles que le quedan a <paramref name="w"/>: la evolución (si no la tiene) + las mejoras que podrá conseguir.</summary>
        public static int LevelsLeft(WeaponDefinition w)
        {
            var k = ArsenalRuntime.For(w);
            if (k == null) return 0;
            if (k.evolution > 0) return AvailableUpgrades(w).Count;
            int best = 0;   // aún sin evolucionar: la evolución + las mejoras de la que más tenga desbloqueadas
            for (int e = 1; e < k.Tree.evolutions.Count; e++)
            {
                int n = 0;
                for (int u = 0; u < 3; u++) if (WeaponMastery.IsUpgradeUnlocked(k.WeaponId, e, u)) n++;
                best = Mathf.Max(best, n);
            }
            return 1 + best;
        }

        /// <summary>¿Se puede usar un núcleo ahora mismo en alguna de las armas que llevas?</summary>
        public static bool CanUseCore() => Equipped().Exists(w => LevelsLeft(w) > 0);

        /// <summary>
        /// Ninguna arma que llevas puede subir más, contando lo ya ganado y sin gastar (Prismas y núcleos pendientes): un
        /// portador suelta almas en vez de núcleo.
        /// </summary>
        public static bool NothingLeft()
        {
            int left = 0;
            foreach (var w in Equipped()) left += LevelsLeft(w);
            return left - PendingCores - PendingEvolutions <= 0;
        }

        public static void ApplyEvolution(WeaponDefinition w, int evo)
        {
            var k = ArsenalRuntime.Editable(w);
            k.evolution = evo;
            k.upgrade1 = k.upgrade2 = k.upgrade3 = false;
            ArsenalRuntime.Touch();
        }

        public static void ApplyUpgrade(WeaponDefinition w, int upgrade)
        {
            ArsenalRuntime.Editable(w).SetUpgrade(upgrade, true);
            ArsenalRuntime.Touch();
        }

        /// <summary>Probabilidad efectiva (con la facilidad de prueba) de que un enemigo normal salga de portador en el clásico.</summary>
        public static float ClassicCarrierChance =>
            Mathf.Clamp01(WeaponProgressionConfig.Instance.carrierChancePerEnemy * WeaponProgressionConfig.Instance.testEase);

        // ------------------------------------------------------------------ modo clásico

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookClassic()
        {
            Health.AnyStarted -= OnHealthStarted;
            Health.AnyStarted += OnHealthStarted;
            Health.AnyDied -= OnHealthDied;
            Health.AnyDied += OnHealthDied;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static bool InClassicRun => RunManager.Instance != null && RunManager.Instance.RunInProgress;

        private static void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            if (_run == RunManager.Instance) return;
            if (_run != null) _run.RunStarted -= OnRunStarted;
            _run = RunManager.Instance;
            if (_run != null) _run.RunStarted += OnRunStarted;
        }

        private static void OnRunStarted(WorldDefinition _) => ResetRun();

        /// <summary>Clásico: cada enemigo normal que aparece puede ser portador.</summary>
        private static void OnHealthStarted(Health h)
        {
            if (!InClassicRun || h == null || h.GetComponent<EnemyStats>() == null || h.GetComponentInParent<BossController>() != null) return;
            if (UnityEngine.Random.value < ClassicCarrierChance) CoreCarrier.Make(h.gameObject);
        }

        /// <summary>Clásico: cada jefe derrotado da un Prisma.</summary>
        private static void OnHealthDied(Health h)
        {
            if (InClassicRun && h != null && h.GetComponent<BossController>() != null) GrantEvolution();
        }
    }

}
