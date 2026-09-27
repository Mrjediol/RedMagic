using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
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
    /// Slot Forma. Lleva 2 tags universales. Define cuántas veces / dónde se aplica el daño.
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

        public override void Apply(WeaponShot shot)
        {
            switch (kind)
            {
                case ShapeKind.MultiShot:
                    shot.projectileCount = Mathf.Max(1, count);
                    shot.spreadAngle = spreadAngle;
                    break;
                case ShapeKind.SplitOnImpact:
                    shot.splitCount = Mathf.Max(1, count);
                    shot.splitDamageFraction = derivedDamageFraction;
                    shot.splitGenerationsLeft = 1;
                    break;
                case ShapeKind.AirRepeat:
                    // El runtime mínimo aún no repite en el aire; se resuelve en un paso posterior.
                    break;
            }
        }

        public override string EffectSummary() => kind switch
        {
            ShapeKind.SplitOnImpact => Loc.Get("modifier.shape.split", count, derivedDamageFraction * 100f),
            ShapeKind.MultiShot => Loc.Get("modifier.shape.multishot", count, spreadAngle),
            ShapeKind.AirRepeat => Loc.Get("modifier.shape.air_repeat", repeats),
            _ => Loc.Get("modifier.shape.single"),
        };
    }
}
