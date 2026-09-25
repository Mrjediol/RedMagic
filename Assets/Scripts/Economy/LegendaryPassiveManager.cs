using System;
using System.Collections.Generic;
using RedMagic.Combat;
using RedMagic.UI;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Estado de partida de las pasivas legendarias del espejo: qué casillas están desbloqueadas y
    /// a qué nivel, la compra de mejoras, y el sistema de <b>drop por bajas</b> que las concede.
    /// Singleton persistente que se auto-crea, mismo patrón que <see cref="UpgradeManager"/>.
    ///
    /// Desbloqueo y nivel se guardan en <c>PlayerPrefs</c> por id de casilla (meta-progresión:
    /// sobreviven a la muerte y al cierre del juego). La primera lectura de cada id cae al valor de
    /// diseño (<see cref="LegendaryPassive.isUnlocked"/>/<see cref="LegendaryPassive.currentLevel"/>)
    /// si todavía no hay nada guardado.
    ///
    /// <b>Drop</b>: se suscribe una sola vez a <see cref="Health.AnyDied"/> (igual que
    /// <see cref="RedMagic.Fx.DamagePopups"/> hace con <c>AnyDamaged</c>) y cuenta cada baja de
    /// bando <see cref="Team.Enemy"/> que no sea el muñeco de entrenamiento (nunca muere de verdad,
    /// pero por si acaso). Cada <see cref="KillsPerDrop"/> bajas concede, al azar, una pasiva de las
    /// que sigan bloqueadas; si ya están las 9 desbloqueadas no pasa nada. El aviso del espejo
    /// (<see cref="HasNewPassiveNotification"/>) es la señal persistente que
    /// <c>MirrorInteractable</c> lee para mostrar el "!" y que <c>MirrorMenuController.Open</c>
    /// apaga al entrar.
    /// </summary>
    [DisallowMultipleComponent]
    public class LegendaryPassiveManager : MonoBehaviour
    {
        public static LegendaryPassiveManager Instance { get; private set; }

        /// <summary>Moneda con la que se pagan las mejoras del espejo.</summary>
        public const Currency Cost = Currency.Skull;

        [Header("Drop por bajas")]
        [Tooltip("Bajas de enemigo necesarias para que caiga una pasiva legendaria al azar. Editable " +
                 "en el Inspector — también en Play — para probar el drop sin matar 100 enemigos de " +
                 "verdad: selecciona '[LegendaryPassiveManager]' en la Hierarchy mientras juegas.")]
        [Min(1)]
        [SerializeField] private int killsPerDrop = 100;

        public int KillsPerDrop => killsPerDrop;

        private const string UnlockedPrefPrefix = "legendaryPassive.unlocked.";
        private const string LevelPrefPrefix = "legendaryPassive.level.";
        private const string TotalKillsPrefKey = "legendaryPassive.totalKills";
        private const string NewPassivePrefKey = "legendaryPassive.newPassiveUnlocked";

        /// <summary>(id, nuevoNivel). Lo escucha el menú del espejo para repintarse.</summary>
        public event Action<int, int> Changed;

        /// <summary>Se dispara al cambiar <see cref="HasNewPassiveNotification"/>.</summary>
        public event Action<bool> NotificationChanged;

        private readonly Dictionary<int, bool> _unlocked = new();
        private readonly Dictionary<int, int> _levels = new();

        private int _totalKills = -1;
        private bool? _hasNotification;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("[LegendaryPassiveManager]").AddComponent<LegendaryPassiveManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            Health.AnyDied += OnAnyDied;
            LegendaryPassiveEffects.Recompute();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Health.AnyDied -= OnAnyDied;
            Instance = null;
        }

        // ------------------------------------------------------------------ consulta

        public bool IsUnlocked(LegendaryPassive passive)
        {
            if (passive == null) return false;
            if (_unlocked.TryGetValue(passive.id, out var value)) return value;

            value = PlayerPrefs.GetInt(UnlockedPrefPrefix + passive.id, passive.isUnlocked ? 1 : 0) != 0;
            _unlocked[passive.id] = value;
            return value;
        }

        public int GetLevel(LegendaryPassive passive)
        {
            if (passive == null) return 0;
            if (_levels.TryGetValue(passive.id, out var lvl)) return lvl;

            lvl = PlayerPrefs.GetInt(LevelPrefPrefix + passive.id, passive.currentLevel);
            _levels[passive.id] = lvl;
            return lvl;
        }

        public bool IsMaxed(LegendaryPassive passive) => passive != null && GetLevel(passive) >= passive.maxLevel;

        public bool CanUpgrade(LegendaryPassive passive)
        {
            if (passive == null || !IsUnlocked(passive) || IsMaxed(passive)) return false;

            var wallet = CurrencyManager.Instance;
            return wallet != null && wallet.Get(Cost) >= passive.upgradeCost;
        }

        public int TotalKills
        {
            get
            {
                if (_totalKills < 0) _totalKills = PlayerPrefs.GetInt(TotalKillsPrefKey, 0);
                return _totalKills;
            }
        }

        /// <summary>True hasta que el jugador abre el espejo tras un drop nuevo (<see cref="ClearNewPassiveNotification"/>).</summary>
        public bool HasNewPassiveNotification
        {
            get
            {
                _hasNotification ??= PlayerPrefs.GetInt(NewPassivePrefKey, 0) != 0;
                return _hasNotification.Value;
            }
            private set
            {
                if (_hasNotification == value) return;
                _hasNotification = value;
                PlayerPrefs.SetInt(NewPassivePrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                NotificationChanged?.Invoke(value);
            }
        }

        /// <summary>Lo llama <c>MirrorMenuController.Open</c>: ya se ha visto el aviso.</summary>
        public void ClearNewPassiveNotification() => HasNewPassiveNotification = false;

        /// <summary>
        /// Borra TODO el progreso de pasivas legendarias (desbloqueo, nivel, bajas acumuladas y el
        /// aviso del espejo) — vuelve a 0 desbloqueadas, como una partida nueva. Sólo para probar:
        /// selecciona "[LegendaryPassiveManager]" en la Hierarchy en Play y usa este mismo menú
        /// (⋮ del Inspector) o el context-menu del componente.
        /// </summary>
        [ContextMenu("Borrar progreso de pasivas legendarias (test)")]
        public void ResetAllProgress()
        {
            foreach (var passive in LegendaryPassiveLibrary.BySlot)
            {
                if (passive == null) continue;
                PlayerPrefs.DeleteKey(UnlockedPrefPrefix + passive.id);
                PlayerPrefs.DeleteKey(LevelPrefPrefix + passive.id);
            }
            PlayerPrefs.DeleteKey(TotalKillsPrefKey);
            PlayerPrefs.DeleteKey(NewPassivePrefKey);
            PlayerPrefs.Save();

            _unlocked.Clear();
            _levels.Clear();
            _totalKills = 0;
            _hasNotification = false;

            LegendaryPassiveEffects.Recompute();
            NotificationChanged?.Invoke(false);
            Changed?.Invoke(-1, 0); // -1: ningún id concreto, refresca la rejilla entera del espejo.

            Debug.Log("[LegendaryPassiveManager] Progreso de pasivas legendarias borrado (0 desbloqueadas).", this);
        }

        // ------------------------------------------------------------------ mutación

        /// <summary>
        /// Desbloquea una pasiva sin cobrar nada — gancho para lo que en el futuro las conceda
        /// además del drop por bajas (un jefe, una compra). No hace nada si ya estaba desbloqueada.
        /// </summary>
        public void Unlock(LegendaryPassive passive)
        {
            if (passive == null || IsUnlocked(passive)) return;

            _unlocked[passive.id] = true;
            PlayerPrefs.SetInt(UnlockedPrefPrefix + passive.id, 1);

            // Desbloquear concede el nivel 1 (el efecto base de la descripción); el único upgrade
            // posible la lleva a nivel 2. Sin esto se quedaba en el nivel 0 de diseño y el primer
            // "upgrade" salía gratis (0 → 1 sin pasar por CanUpgrade con el nivel real).
            if (GetLevel(passive) < 1)
            {
                _levels[passive.id] = 1;
                PlayerPrefs.SetInt(LevelPrefPrefix + passive.id, 1);
            }

            PlayerPrefs.Save();

            LegendaryPassiveEffects.Recompute();
            Changed?.Invoke(passive.id, GetLevel(passive));
        }

        /// <summary>
        /// Sube un nivel la pasiva cobrando <see cref="Currency.Skull"/>. Devuelve false sin tocar
        /// nada si no se cumplen las condiciones (<see cref="CanUpgrade"/>).
        /// </summary>
        public bool TryUpgrade(LegendaryPassive passive)
        {
            if (!CanUpgrade(passive)) return false;
            if (!CurrencyManager.Instance.TrySpend(Cost, passive.upgradeCost)) return false;

            int newLevel = GetLevel(passive) + 1;
            _levels[passive.id] = newLevel;
            PlayerPrefs.SetInt(LevelPrefPrefix + passive.id, newLevel);
            PlayerPrefs.Save();

            LegendaryPassiveEffects.Recompute();
            Changed?.Invoke(passive.id, newLevel);
            return true;
        }

        // ------------------------------------------------------------------ drop por bajas

        /// <summary>
        /// Filtra qué bajas cuentan: sólo bando enemigo (nunca el jugador) y nunca el muñeco de
        /// entrenamiento — mismo criterio que <c>SectionClearTracker.CountsAsEnemy</c>, aunque en
        /// la práctica el muñeco jamás dispara <c>Died</c> porque se cura solo en cada golpe.
        /// </summary>
        private void OnAnyDied(Health health)
        {
            if (health == null) return;
            if (Teams.Of(health) != Team.Enemy) return;
            if (health.GetComponent<TrainingDummy>() != null) return;

            RegisterKill();
        }

        private void RegisterKill()
        {
            int total = TotalKills + 1;
            _totalKills = total;
            PlayerPrefs.SetInt(TotalKillsPrefKey, total);
            PlayerPrefs.Save();

            if (total % KillsPerDrop == 0) TryDropRandomPassive();
        }

        /// <summary>
        /// Desbloquea una pasiva al azar de entre las que siguen bloqueadas. Si las 9 ya están
        /// desbloqueadas no hace nada — "no more drops", tal cual.
        /// </summary>
        private void TryDropRandomPassive()
        {
            var locked = new List<LegendaryPassive>(LegendaryPassiveLibrary.SlotCount);
            foreach (var passive in LegendaryPassiveLibrary.BySlot)
                if (passive != null && !IsUnlocked(passive)) locked.Add(passive);

            if (locked.Count == 0) return;

            var chosen = locked[UnityEngine.Random.Range(0, locked.Count)];
            Unlock(chosen);
            HasNewPassiveNotification = true;
            PassiveDropCinematic.Show(chosen);
        }
    }
}
