using RedMagic.Audio;
using UnityEngine;

namespace RedMagic.Abilities
{
    /// <summary>
    /// Familia de la habilidad. Sólo sirve para agrupar y colorear la lista del menú de pruebas;
    /// no cambia nada del comportamiento.
    /// </summary>
    public enum AbilityCategory
    {
        Melee,
        Ranged,
        Area,
        Utility
    }

    /// <summary>
    /// Base de todas las habilidades. Cada habilidad concreta es un <b>asset</b>
    /// (ScriptableObject) en <c>Assets/Resources/Abilities/</c>: crear una nueva es duplicar un
    /// asset y tocar números, sin escribir código ni enganchar nada en el Inspector — el menú y el
    /// jugador las descubren solas (<see cref="AbilityLibrary"/>).
    ///
    /// Escribir una clase nueva sólo hace falta para un <i>arquetipo</i> que no exista todavía
    /// (una forma de atacar distinta), no para una habilidad nueva. Los arquetipos actuales son
    /// <see cref="MeleeArcAbility"/>, <see cref="ProjectileAbility"/>, <see cref="NovaAbility"/>,
    /// <see cref="BeamAbility"/>, <see cref="DashStrikeAbility"/>, <see cref="ZoneAbility"/>,
    /// <see cref="OrbitAbility"/>, <see cref="TurretAbility"/> y <see cref="BuffAbility"/>.
    ///
    /// Los assets no guardan estado de lanzamiento (los comparten todos los que la usen): el
    /// estado va en <see cref="AbilityUser"/> y en los objetos que la habilidad instancia.
    /// </summary>
    public abstract class AbilityDefinition : ScriptableObject
    {
        /// <summary>
        /// Lo que cambia en un nivel respecto al nivel 1. Los valores son <b>absolutos respecto al
        /// nivel 1</b>, no acumulativos: el nivel 3 no multiplica al 2. Así se lee de un vistazo
        /// cuánto mejor es cada versión del arma sin tener que multiplicar de cabeza.
        /// </summary>
        [System.Serializable]
        public class LevelTier
        {
            [Tooltip("Multiplica el daño.")]
            [Min(0f)]
            public float damageMultiplier = 1f;

            [Tooltip("Multiplica el tiempo entre usos. Menor que 1 = dispara más rápido.")]
            [Min(0.05f)]
            public float cooldownMultiplier = 1f;

            [Tooltip("Multiplica el tamaño: alcance del haz, caja del golpe, radio de la onda, " +
                     "tamaño del proyectil… cada arquetipo lo aplica a su geometría.")]
            [Min(0.05f)]
            public float sizeMultiplier = 1f;

            [Tooltip("Fracción del daño hecho que cura al lanzador. 0.1 = 10%.")]
            [Range(0f, 1f)]
            public float lifesteal;
        }

        /// <summary>Nivel máximo de cualquier arma.</summary>
        public const int MaxLevel = 3;

        [Header("Ficha")]
        [Tooltip("Nombre que se ve en el menú. Vacío = el nombre del asset.")]
        [SerializeField] private string displayName;
        [TextArea(2, 4)]
        [SerializeField] private string description;
        [SerializeField] private AbilityCategory category = AbilityCategory.Melee;
        [Tooltip("Icono opcional para el menú.")]
        [SerializeField] private Sprite icon;
        [Tooltip("Color del efecto y de la carta en el menú.")]
        [SerializeField] private Color accent = new Color(0.95f, 0.55f, 0.25f);

        [Header("Ritmo")]
        [Tooltip("Segundos entre usos.")]
        [Min(0f)]
        [SerializeField] private float cooldown = 0.5f;
        [Tooltip("Retardo entre pulsar y que el efecto salga, para que cuadre con la animación.")]
        [Min(0f)]
        [SerializeField] private float windup;

        [Header("Daño")]
        [Min(0f)]
        [SerializeField] private float damage = 20f;
        [Tooltip("Cuánto empuja: multiplica el retroceso configurado en el Knockback del objetivo.")]
        [Min(0f)]
        [SerializeField] private float knockbackMultiplier = 1f;

        [Header("Niveles (2 y 3)")]
        [Tooltip("Qué mejora al subir a nivel 2 respecto al nivel 1.")]
        [SerializeField] private LevelTier level2 = new LevelTier
        {
            damageMultiplier = 1.15f,
            cooldownMultiplier = 0.85f,
            sizeMultiplier = 1.25f
        };

        [Tooltip("Qué mejora al subir a nivel 3 respecto al nivel 1 (no se acumula con el 2).")]
        [SerializeField] private LevelTier level3 = new LevelTier
        {
            damageMultiplier = 1.3f,
            cooldownMultiplier = 0.7f,
            sizeMultiplier = 1.5f,
            lifesteal = 0.1f
        };

        [Header("Presentación")]
        [Tooltip("Sonido al lanzar.")]
        [SerializeField] private SoundCue castSound = new SoundCue();
        [Tooltip("Sprite del efecto. Vacío = un cuadrado blanco generado en tiempo de ejecución, " +
                 "que ya se ve y se puede sustituir por arte después.")]
        [SerializeField] private Sprite fxSprite;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public AbilityCategory Category => category;
        public Sprite Icon => icon;
        public Color Accent => accent;
        public float Cooldown => cooldown;
        public float Windup => windup;
        public float Damage => damage;
        public float KnockbackMultiplier => knockbackMultiplier;
        public Sprite FxSprite => fxSprite;

        /// <summary>Mejoras de un nivel concreto. El nivel 1 (y cualquier valor raro) no cambia nada.</summary>
        public LevelTier TierFor(int level) => level switch
        {
            2 => level2,
            3 => level3,
            _ => null
        };

        public float DamageAt(int level) => damage * (TierFor(level)?.damageMultiplier ?? 1f);

        public float CooldownAt(int level) => cooldown * (TierFor(level)?.cooldownMultiplier ?? 1f);

        /// <summary>Multiplicador de tamaño/alcance que cada arquetipo aplica a su geometría.</summary>
        public float SizeScaleAt(int level) => TierFor(level)?.sizeMultiplier ?? 1f;

        /// <summary>Fracción del daño que cura al lanzador en ese nivel.</summary>
        public float LifestealAt(int level) => TierFor(level)?.lifesteal ?? 0f;

        /// <summary>
        /// Resumen de las mejoras de un nivel, para la carta del altar de mejora. Se compone sólo
        /// con lo que de verdad cambia, así que un nivel que sólo da alcance no miente diciendo
        /// "+0% daño".
        /// </summary>
        public string TierSummary(int level)
        {
            var tier = TierFor(level);
            if (tier == null) return "";

            var parts = new System.Collections.Generic.List<string>();
            if (!Mathf.Approximately(tier.damageMultiplier, 1f))
                parts.Add($"{(tier.damageMultiplier - 1f) * 100f:+0;-0}% daño");
            if (!Mathf.Approximately(tier.cooldownMultiplier, 1f))
                parts.Add($"{(1f - tier.cooldownMultiplier) * 100f:0}% más rápido");
            if (!Mathf.Approximately(tier.sizeMultiplier, 1f))
                parts.Add($"{(tier.sizeMultiplier - 1f) * 100f:+0;-0}% alcance y tamaño");
            if (tier.lifesteal > 0f)
                parts.Add($"cura {tier.lifesteal * 100f:0}% del daño");

            return parts.Count > 0 ? string.Join(" · ", parts) : "sin cambios";
        }

        /// <summary>
        /// Resumen de una línea para la carta del menú: lo genera cada arquetipo con sus propios
        /// números (cadencia, proyectiles, radio…), que es lo que de verdad distingue una
        /// habilidad de otra al probarlas.
        /// </summary>
        public virtual string ShortStats() => $"{damage:0} dmg · {cooldown:0.00}s";

        /// <summary>
        /// Se llama justo al pulsar, ya pasados el cooldown y el windup. La habilidad hace su
        /// efecto aquí; si necesita durar en el tiempo, arranca una corrutina en
        /// <see cref="AbilityContext.Runner"/>.
        /// </summary>
        public abstract void Execute(AbilityContext ctx);

        /// <summary>
        /// Condición extra para poder lanzarla (por ejemplo, sólo en el suelo). Por defecto siempre
        /// se puede.
        /// </summary>
        public virtual bool CanCast(in AbilityContext ctx) => true;

        /// <summary>Sonido de lanzamiento. Lo llama <see cref="AbilityUser"/>, no cada arquetipo.</summary>
        public void PlayCastSfx()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(castSound);
        }

        protected float ScaledDamage(in AbilityContext ctx) => damage * ctx.DamageScale;
    }
}
