using UnityEngine;

namespace RedMagic.Items
{
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

        public override void Apply(WeaponShot shot)
        {
            // El elemento se resuelve el último: "pinta" el resultado de trayectoria y forma. Todo
            // proyectil generado en el paso 3 hereda esto porque comparte el mismo WeaponShot.
            shot.element = element;
            shot.tint = Accent;
            shot.statusChance = statusChance;
            shot.statusDuration = statusDuration;
            shot.statusMagnitude = statusMagnitude;
        }

        public override string EffectSummary() =>
            $"El disparo pasa a ser de {BuildTags.DisplayName(element)}.";

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
