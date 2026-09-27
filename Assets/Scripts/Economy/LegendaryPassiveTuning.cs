using RedMagic.Audio;
using UnityEngine;

namespace RedMagic.Economy
{
    /// <summary>
    /// Números de los efectos de las pasivas legendarias, en un solo asset:
    /// <c>Assets/Resources/LegendaryPassiveTuning.asset</c> (<c>Resources.Load</c>, mismo patrón que
    /// <c>SynergyConfig</c>). Sin asset se usan estos valores por defecto. Se lee en cada uso, así
    /// que se puede afinar en Play.
    /// </summary>
    [CreateAssetMenu(fileName = "LegendaryPassiveTuning", menuName = "RedMagic/Economy/Legendary Passive Tuning")]
    public class LegendaryPassiveTuning : ScriptableObject
    {
        public const string ResourcePath = "LegendaryPassiveTuning";

        [Header("Codex Aurum — objetos al matar al jefe")]
        [Min(0)] public int bossItemsLevel1 = 1;
        [Min(0)] public int bossItemsLevel2 = 2;

        [Header("Grimorio del Umbral — elegir arma del cofre")]
        [Range(2, 5)] public int chestWeaponChoices = 3;
        [Tooltip("Mejoras con las que sale el arma elegida en el nivel 2.")]
        [Min(0)] public int chestChoiceUpgradesLevel2 = 1;

        [Header("Páginas del Eco — rerolls por run")]
        [Min(0)] public int rerollsLevel1 = 2;
        [Min(0)] public int rerollsLevel2 = 4;

        [Header("El Libro Sin Nombre — revivir")]
        [Range(0.05f, 1f)] public float reviveHealthFraction = 0.3f;
        [Tooltip("Segundos de invulnerabilidad tras revivir.")]
        [Min(0f)] public float reviveInvulnerability = 2f;
        [Tooltip("Golpes que absorbe el escudo del nivel 2.")]
        [Min(0)] public int reviveShieldHits = 3;
        public Color shieldColor = new(0.3f, 1f, 0.9f, 0.35f);

        [Header("Volumen Carmesí — daño del jugador")]
        [Min(0f)] public float damageBonusLevel1 = 0.10f;
        [Min(0f)] public float damageBonusLevel2 = 0.20f;

        [Header("Anales del Vacío")]
        [Tooltip("Ralentización de la 1ª oleada de cada nivel (0.4 = -40% de velocidad).")]
        [Range(0f, 0.95f)] public float firstWaveSlowStrength = 0.4f;
        [Min(0f)] public float firstWaveSlowDuration = 8f;
        [Tooltip("Vida máxima que pierde cada enemigo de la run en el nivel 2 (0.2 = -20%).")]
        [Range(0f, 0.9f)] public float enemyHealthReductionLevel2 = 0.2f;

        [Header("Manuscrito Eterno — objeto cada N niveles")]
        [Min(1)] public int levelsPerFreeItem = 3;

        [Header("El Tomo Roto — arma del cofre")]
        [Min(0)] public int chestWeaponUpgrades = 2;

        [Header("Sonidos de las pasivas")]
        [Tooltip("Codex Aurum: caen los objetos gratis al matar al jefe.")]
        public SoundCue codexAurumDropSound = new SoundCue { positional = true };
        [Tooltip("Manuscrito Eterno: cae el objeto de cada N secciones.")]
        public SoundCue manuscritoDropSound = new SoundCue { positional = true };
        [Tooltip("Grimorio del Umbral: el cofre ofrece varias armas a elegir.")]
        public SoundCue grimorioChoiceSound = new SoundCue();
        [Tooltip("El Tomo Roto: el arma del cofre sale mejorada.")]
        public SoundCue tomoRotoUpgradeSound = new SoundCue();
        [Tooltip("El Libro Sin Nombre: revive.")]
        public SoundCue libroReviveSound = new SoundCue { priority = SoundPriority.High };
        [Tooltip("El Libro Sin Nombre (nivel 2): el escudo absorbe un golpe.")]
        public SoundCue libroShieldSound = new SoundCue();
        [Tooltip("Anales del Vacío: la primera oleada llega ralentizada (una vez por oleada).")]
        public SoundCue analesSlowSound = new SoundCue();

        private static LegendaryPassiveTuning _cached;

        /// <summary>El asset de Resources, o uno con los valores por defecto si no existe.</summary>
        public static LegendaryPassiveTuning Current
        {
            get
            {
                if (_cached != null) return _cached;
                _cached = Resources.Load<LegendaryPassiveTuning>(ResourcePath);
                if (_cached == null) _cached = CreateInstance<LegendaryPassiveTuning>();
                return _cached;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cached = null;
    }
}
