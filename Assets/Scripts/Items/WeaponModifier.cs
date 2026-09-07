using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Base de los tres items de slot dedicado. Cada uno transforma el "shot base" del arma
    /// (<see cref="WeaponDefinition.BaseShot"/>) en un punto fijo de la cascada de resolución:
    ///
    /// <code>
    /// 1. TRAYECTORIA  → cómo viaja el proyectil/hitbox
    /// 2. FORMA        → cuántas veces / dónde aplica el daño
    /// 3. ELEMENTO     → qué tipo de daño/status "pinta" el resultado
    /// </code>
    ///
    /// <b>La transformación en sí (el paso de composición) es de un paso posterior.</b> Aquí sólo
    /// están los datos que la describen y <see cref="PipelineOrder"/>, que fija el orden.
    /// </summary>
    public abstract class WeaponModifier : ItemDefinition
    {
        /// <summary>Posición en la cascada. Menor = se aplica antes. Trayectoria 1, Forma 2, Elemento 3.</summary>
        public abstract int PipelineOrder { get; }
    }

    // ---------------------------------------------------------------------------------------------

    public enum TrajectoryKind
    {
        /// <summary>Recto en la dirección de disparo (comportamiento base, sin modificador).</summary>
        Straight,
        /// <summary>Curva hacia el enemigo más cercano (auto-aim).</summary>
        HomingCurve,
        /// <summary>Rebota en geometría/enemigos un número de veces.</summary>
        Bounce,
    }

    /// <summary>
    /// Slot Trayectoria. Sólo tags universales (2). Define cómo se mueve el proyectil/hitbox tras
    /// generarse; no toca cantidad de daño ni elemento.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Items/Modifier - Trajectory", fileName = "Trajectory_")]
    public class TrajectoryModifier : WeaponModifier
    {
        [Header("Trayectoria")]
        [SerializeField] private TrajectoryKind kind = TrajectoryKind.Straight;

        [Tooltip("HomingCurve: grados/segundo de giro hacia el objetivo.")]
        [Min(0f)]
        [SerializeField] private float homingTurnRate = 540f;

        [Tooltip("HomingCurve: radio de búsqueda de objetivo, en unidades.")]
        [Min(0f)]
        [SerializeField] private float homingRange = 9f;

        [Tooltip("Bounce: número de rebotes antes de morir.")]
        [Min(0)]
        [SerializeField] private int bounces = 3;

        public override ItemSlot Slot => ItemSlot.DedicatedTrajectory;
        public override int PipelineOrder => 1;

        public TrajectoryKind Kind => kind;
        public float HomingTurnRate => homingTurnRate;
        public float HomingRange => homingRange;
        public int Bounces => bounces;
    }

    // ---------------------------------------------------------------------------------------------

    public enum ShapeKind
    {
        /// <summary>Un solo golpe donde llega el shot base (sin modificador).</summary>
        Single,
        /// <summary>Al impactar, se divide en N proyectiles nuevos.</summary>
        SplitOnImpact,
        /// <summary>Salen N proyectiles a la vez desde el cañón, en abanico.</summary>
        MultiShot,
        /// <summary>El proyectil se repite en el aire cada cierto intervalo.</summary>
        AirRepeat,
    }

    /// <summary>
    /// Slot Forma. Sólo tags universales (2). Define cuántas veces / dónde se aplica el daño.
    /// Cualquier proyectil que genere hereda automáticamente el elemento actual del arma (paso 3),
    /// así que esta clase no sabe nada de elementos.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Items/Modifier - Shape", fileName = "Shape_")]
    public class ShapeModifier : WeaponModifier
    {
        [Header("Forma")]
        [SerializeField] private ShapeKind kind = ShapeKind.Single;

        [Tooltip("SplitOnImpact / MultiShot: cuántos proyectiles.")]
        [Min(1)]
        [SerializeField] private int count = 5;

        [Tooltip("MultiShot: abanico total en grados repartido entre los proyectiles.")]
        [Min(0f)]
        [SerializeField] private float spreadAngle = 40f;

        [Tooltip("AirRepeat: repeticiones y segundos entre cada una.")]
        [Min(0)]
        [SerializeField] private int repeats = 3;

        [Min(0.02f)]
        [SerializeField] private float repeatInterval = 0.15f;

        [Tooltip("Fracción del daño base que conserva cada proyectil derivado (0..1).")]
        [Range(0f, 1f)]
        [SerializeField] private float derivedDamageFraction = 0.6f;

        public override ItemSlot Slot => ItemSlot.DedicatedShape;
        public override int PipelineOrder => 2;

        public ShapeKind Kind => kind;
        public int Count => count;
        public float SpreadAngle => spreadAngle;
        public int Repeats => repeats;
        public float RepeatInterval => repeatInterval;
        public float DerivedDamageFraction => derivedDamageFraction;
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Slot Elemento. Lleva 2 tags: 1 elemental (que debe coincidir con <see cref="Element"/>) +
    /// 1 universal. "Pinta" el resultado de los pasos anteriores con un tipo de daño / status;
    /// se resuelve al final del pipeline y lo heredan todos los proyectiles generados por la Forma.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Items/Modifier - Element", fileName = "Element_")]
    public class ElementModifier : WeaponModifier
    {
        [Header("Elemento")]
        [Tooltip("Qué elemento aplica. Debe estar también en la lista de tags de sinergia.")]
        [SerializeField] private ElementId element = ElementId.Ice;

        [Tooltip("Probabilidad de aplicar el status en cada golpe (0..1). Placeholder v1.")]
        [Range(0f, 1f)]
        [SerializeField] private float statusChance = 1f;

        [Tooltip("Duración base del status en segundos. Placeholder v1.")]
        [Min(0f)]
        [SerializeField] private float statusDuration = 3f;

        [Tooltip("Magnitud base del status (ralentización %, daño/tick…). Placeholder v1.")]
        [Min(0f)]
        [SerializeField] private float statusMagnitude = 0.1f;

        public override ItemSlot Slot => ItemSlot.DedicatedElement;
        public override int PipelineOrder => 3;

        public ElementId Element => element;
        public float StatusChance => statusChance;
        public float StatusDuration => statusDuration;
        public float StatusMagnitude => statusMagnitude;

        protected override void OnValidate()
        {
            base.OnValidate();

            var expected = BuildTags.TagFor(element);
            if (expected.HasValue && PointsFor(expected.Value) == 0)
                Debug.LogWarning($"[Items] '{name}' aplica {element} pero su lista de tags no incluye " +
                                 $"{expected.Value}: la sinergia elemental no contará este item.", this);
        }
    }
}
