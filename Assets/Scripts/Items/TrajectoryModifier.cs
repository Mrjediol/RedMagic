using RedMagic.Localization;
using UnityEngine;

namespace RedMagic.Items
{
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
    /// Slot Trayectoria. Lleva 2 tags universales. Define cómo se mueve el proyectil/hitbox tras
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

        public override void Apply(WeaponShot shot)
        {
            shot.trajectory = kind;
            switch (kind)
            {
                case TrajectoryKind.HomingCurve:
                    shot.homingTurnRate = homingTurnRate;
                    shot.homingRange = homingRange;
                    break;
                case TrajectoryKind.Bounce:
                    // El runtime de proyectil mínimo aún no rebota; se resuelve en un paso posterior.
                    break;
            }
        }

        public override string EffectSummary() => kind switch
        {
            TrajectoryKind.HomingCurve => Loc.Get("modifier.trajectory.homing"),
            TrajectoryKind.Bounce => Loc.Get("modifier.trajectory.bounce", bounces),
            _ => Loc.Get("modifier.trajectory.straight"),
        };
    }
}
