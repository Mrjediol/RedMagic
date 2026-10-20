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
    /// <item><b>Mejoras — Núcleo</b>: lo suelta entero un <b>portador</b> (<see cref="CoreCarrier"/>), un enemigo raro y
    /// brillante que se escapa si no lo matas a tiempo. Eliges arma evolucionada y una de
    /// <see cref="WeaponProgressionConfig.offerUpgrades"/> mejoras al azar entre las desbloqueadas
    /// (<see cref="WeaponMastery.IsUpgradeUnlocked"/>) que aún no tiene. Un núcleo sin dónde ponerlo espera a la siguiente
    /// evolución; si ya no queda nada que mejorar, se cambia por oro.</item>
    /// </list>
    /// Núcleos enteros y no fragmentos a propósito: sumar fragmentos estrecha la curva (casi todas las runs acaban con lo
    /// mismo); sucesos raros que dan la mejora entera dan la que se busca — ~30 % sin mejoras, ~35 % una, ~22 % dos, ~13 %
    /// tres o más (Poisson de media ≈1,3 núcleos por run). Las elecciones las enseña <see cref="ArsenalProgressUi"/>.
    /// </summary>
    public static class WeaponRunProgress
    {
        /// <summary>Evoluciones ganadas y aún sin elegir.</summary>
        public static int PendingEvolutions { get; private set; }
        /// <summary>Núcleos ganados y aún sin usar (esperando una evolución o el momento de elegir).</summary>
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

    /// <summary>
    /// Números de la progresión del arma en la run (<see cref="WeaponRunProgress"/>), en
    /// <c>Resources/WeaponProgressionConfig.asset</c>. La parte propia de Supervivencia (rondas con evolución, portadores
    /// por ronda) está en su <c>SurvivalConfig</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Armas/Progresión en la run", fileName = "WeaponProgressionConfig")]
    public class WeaponProgressionConfig : ScriptableObject
    {
        [Tooltip("Las armas empiezan cada partida en su forma base (la evolución y las mejoras se ganan jugando). Apagado = la " +
                 "evolución y las mejoras puestas en el asset del arma (como antes, para probar una forma concreta).")]
        public bool startAtBase = true;

        [Header("Elegir")]
        [Tooltip("Cuántas de las evoluciones del arma se ofrecen (al azar) en cada Prisma.")]
        [Min(1)] public int offerEvolutions = 2;
        [Tooltip("Cuántas mejoras se ofrecen (al azar, de las que se pueden conseguir) con cada Núcleo.")]
        [Min(1)] public int offerUpgrades = 2;
        [Tooltip("Oro a cambio de un Núcleo cuando ya no queda nada que mejorar en ningún arma.")]
        [Min(0)] public int goldIfNothingToGive = 120;

        [Header("Portadores de núcleo")]
        [Tooltip("Clásico: probabilidad de que un enemigo normal salga de portador (sin contar la facilidad de prueba).")]
        [Range(0f, 1f)] public float carrierChancePerEnemy = 0.012f;
        [Tooltip("FACILIDAD DE PRUEBA: multiplica la probabilidad de portadores (clásico y Supervivencia). 1 = la de verdad.")]
        [Min(0f)] public float testEase = 8f;
        [Tooltip("Segundos que vive un portador antes de escaparse.")]
        [Min(1f)] public float carrierLifetime = 20f;
        [Tooltip("Los últimos segundos parpadea (se va a escapar).")]
        [Min(0f)] public float carrierBlink = 5f;
        [Tooltip("Aspecto y fuerza del portador (una variante de enemigo).")]
        public Enemies.EnemyVariant carrierLook;
        [Tooltip("Shader de las variantes (RedMagic/Sprite Lit FX).")]
        public Shader variantShader;

        [Header("Núcleo (lo que suelta)")]
        public Sprite coreSprite;
        [Min(0.1f)] public float coreSize = 1f;
        [Tooltip("Se va hacia el jugador cuando está a esta distancia.")]
        [Min(0f)] public float coreMagnetRadius = 4f;

        private static WeaponProgressionConfig _instance;

        public static WeaponProgressionConfig Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<WeaponProgressionConfig>("WeaponProgressionConfig");
                if (_instance == null) _instance = CreateInstance<WeaponProgressionConfig>();
                return _instance;
            }
        }
    }
}
