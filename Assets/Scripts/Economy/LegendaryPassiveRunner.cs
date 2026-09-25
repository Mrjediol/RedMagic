using System;
using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.Items;
using RedMagic.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedMagic.Economy
{
    /// <summary>
    /// Las pasivas legendarias que reaccionan a eventos de la run. Singleton persistente que se crea
    /// solo; escucha <see cref="RunManager"/>, <see cref="SectionClearTracker"/>,
    /// <see cref="WaveManager.AnyEnemySpawned"/> y <see cref="Health.AnyStarted"/>:
    ///  - <b>Codex Aurum</b>: al caer el jefe, 1/2 items gratis en el suelo (<see cref="ItemPickup"/>).
    ///  - <b>Páginas del Eco</b>: +2/+4 a <see cref="RunRerolls"/> al empezar la run.
    ///  - <b>Anales del Vacío</b>: nv1 ralentiza la oleada 1 de cada nivel al salir; nv2 además quita
    ///    un 20 % de vida máxima a todo enemigo que aparezca durante la run.
    ///  - <b>Manuscrito Eterno</b>: cada N niveles despejados, un item gratis (nv2: épico o legendario).
    /// También lleva el estado por run de <b>El Libro Sin Nombre</b> (<see cref="ReviveAvailable"/>).
    /// Números en <see cref="LegendaryPassiveTuning"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class LegendaryPassiveRunner : MonoBehaviour
    {
        private static LegendaryPassiveRunner _instance;

        /// <summary>Queda el revivir de esta run (El Libro Sin Nombre).</summary>
        public static bool ReviveAvailable { get; private set; }

        /// <summary>Niveles despejados en la run en curso.</summary>
        public static int LevelsCleared { get; private set; }

        /// <summary>Empieza (true) o termina (false) una run. Lo escucha lo que tenga estado por run.</summary>
        public static event Action<bool> RunStateChanged;

        private RunManager _run;
        private SectionClearTracker _tracker;
        private bool _inRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            ReviveAvailable = false;
            LevelsCleared = 0;
            RunStateChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[LegendaryPassiveRunner]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LegendaryPassiveRunner>();
        }

        public static void ConsumeRevive() => ReviveAvailable = false;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            WaveManager.AnyEnemySpawned += OnWaveEnemySpawned;
            Health.AnyStarted += OnHealthStarted;
            Bind();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            WaveManager.AnyEnemySpawned -= OnWaveEnemySpawned;
            Health.AnyStarted -= OnHealthStarted;

            if (_run != null) { _run.RunStarted -= OnRunStarted; _run.RunEnded -= OnRunEnded; }
            if (_tracker != null) _tracker.Cleared -= OnSectionCleared;
            _run = null;
            _tracker = null;
        }

        // RunManager y el tracker aparecen al cargar el hub: se reintenta en cada carga, igual que
        // WeaponLevelManager.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Bind();

        private void Bind()
        {
            if (_run == null && RunManager.Instance != null)
            {
                _run = RunManager.Instance;
                _run.RunStarted += OnRunStarted;
                _run.RunEnded += OnRunEnded;
            }

            if (_tracker == null && SectionClearTracker.Instance != null)
            {
                _tracker = SectionClearTracker.Instance;
                _tracker.Cleared += OnSectionCleared;
            }
        }

        // ------------------------------------------------------------------ inicio / fin de run

        // RunStarted también salta al encadenar mundos; sólo el primero es "empezar la run".
        private void OnRunStarted(WorldDefinition world)
        {
            if (_inRun) return;
            _inRun = true;

            var tuning = LegendaryPassiveTuning.Current;
            ReviveAvailable = LegendaryPassiveEffects.Has(LegendaryPassiveEffectKind.ReviveOnce);
            LevelsCleared = 0;

            int rerolls = LegendaryPassiveEffects.Level(LegendaryPassiveEffectKind.PermanentRerolls) switch
            {
                >= 2 => tuning.rerollsLevel2,
                1 => tuning.rerollsLevel1,
                _ => 0,
            };
            RunRerolls.Set(rerolls);

            RunStateChanged?.Invoke(true);
        }

        private void OnRunEnded(bool completed)
        {
            _inRun = false;
            ReviveAvailable = false;
            LevelsCleared = 0;
            RunRerolls.Set(0);
            RunStateChanged?.Invoke(false);
        }

        // ------------------------------------------------------------------ niveles despejados

        private void OnSectionCleared()
        {
            if (_run == null || !_run.RunInProgress) return;

            var tuning = LegendaryPassiveTuning.Current;
            Vector3 at = _tracker != null ? _tracker.LastDeathPosition : Vector3.zero;
            bool boss = _run.Phase == RunPhase.Boss;
            LevelsCleared++;

            // La recompensa del jefe (el altar) cae justo en 'at': los items gratis, a los lados.
            int codex = LegendaryPassiveEffects.Level(LegendaryPassiveEffectKind.FreeItemAfterBoss);
            if (boss && codex > 0)
            {
                int count = codex >= 2 ? tuning.bossItemsLevel2 : tuning.bossItemsLevel1;
                ItemPickup.Drop(RandomItems(count, ItemRarity.Common), at + Vector3.right * 2.5f);
            }

            int manuscript = LegendaryPassiveEffects.Level(LegendaryPassiveEffectKind.PeriodicFreeItem);
            if (manuscript > 0 && LevelsCleared % tuning.levelsPerFreeItem == 0)
            {
                var minRarity = manuscript >= 2 ? ItemRarity.Epic : ItemRarity.Common;
                ItemPickup.Drop(RandomItems(1, minRarity), at + (boss ? Vector3.left * 2.5f : Vector3.zero));
            }
        }

        private static List<ItemDefinition> RandomItems(int count, ItemRarity minRarity)
        {
            var items = new List<ItemDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                var item = ItemPickup.RandomItem(minRarity);
                if (item != null) items.Add(item);
            }
            return items;
        }

        // ------------------------------------------------------------------ Anales del Vacío

        private static void OnWaveEnemySpawned(WaveManager manager, int waveIndex, Health health)
        {
            if (waveIndex != 0 || !LegendaryPassiveEffects.Has(LegendaryPassiveEffectKind.WeakenedEnemies)) return;

            var tuning = LegendaryPassiveTuning.Current;
            var synergy = SynergyConfig.CurrentTuning;
            SlowStatus.Apply(health, tuning.firstWaveSlowStrength, tuning.firstWaveSlowDuration, 0f,
                             synergy.slowTint, synergy.fullTintAtSlow);
        }

        private void OnHealthStarted(Health health)
        {
            if (!_inRun || LegendaryPassiveEffects.Level(LegendaryPassiveEffectKind.WeakenedEnemies) < 2) return;
            if (health == null || Teams.Of(health) != Team.Enemy) return;
            if (health.GetComponent<TrainingDummy>() != null) return;

            float keep = 1f - LegendaryPassiveTuning.Current.enemyHealthReductionLevel2;
            health.SetMaxHealth(health.MaxHealth * keep, healToFull: true);
        }
    }
}
