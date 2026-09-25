using RedMagic.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Gameplay
{
    /// <summary>Qué lado del sprite es la "punta" (el que apunta hacia donde vuela).</summary>
    public enum ProjectileFacingAxis
    {
        Right,
        Left,
        Up,
        Down,
    }

    /// <summary>Hacia dónde sale el proyectil al dispararse.</summary>
    public enum ProjectileAimMode
    {
        /// <summary>La dirección que da quien dispara (comportamiento de siempre).</summary>
        Fixed,
        /// <summary>Sólo jugador: hacia el cursor del ratón en el instante del disparo.</summary>
        MouseDirection,
        /// <summary>Sólo jugador: hacia el enemigo vivo más cercano al salir. No persigue.</summary>
        NearestEnemy,
    }

    /// <summary>
    /// <b>La lógica única de apuntado y orientación de TODOS los proyectiles</b>: los del arma del
    /// jugador (<c>BaseShot</c> → <c>ShotResolver</c> → <c>ShotProjectile</c>) y los de enemigos,
    /// jefes y habilidades (<c>ProjectileSpec</c> → <c>ProjectileFactory</c> → <c>Projectile</c>).
    /// Las dos definiciones llevan los mismos tres campos (<c>faceDirection</c>,
    /// <c>facingAxis</c>, <c>aimMode</c>) y los dos caminos llaman aquí; no hay otra implementación.
    /// </summary>
    public static class ProjectileAim
    {
        /// <summary>Radio en el que <see cref="ProjectileAimMode.NearestEnemy"/> busca objetivo.</summary>
        public const float NearestEnemySearchRadius = 25f;

        private static readonly Collider2D[] Buffer = new Collider2D[128];

        /// <summary>
        /// Dirección final de salida. <see cref="ProjectileAimMode.MouseDirection"/> y
        /// <see cref="ProjectileAimMode.NearestEnemy"/> sólo valen para el jugador: con otro
        /// lanzador, sin ratón o sin enemigo cerca devuelven <paramref name="fallback"/> (Fixed).
        /// </summary>
        public static Vector2 Resolve(ProjectileAimMode mode, Vector2 origin, Vector2 fallback, GameObject caster)
        {
            if (mode == ProjectileAimMode.Fixed || Teams.Of(caster) != Team.Player) return fallback;

            Vector2? target = mode switch
            {
                ProjectileAimMode.MouseDirection => MouseWorld(),
                ProjectileAimMode.NearestEnemy => NearestEnemyPoint(origin, caster),
                _ => null,
            };

            if (target == null) return fallback;

            Vector2 dir = target.Value - origin;
            return dir.sqrMagnitude < 0.0001f ? fallback : dir.normalized;
        }

        /// <summary>Rotación que deja el lado <paramref name="axis"/> del sprite apuntando a <paramref name="direction"/>.</summary>
        public static Quaternion Rotation(Vector2 direction, ProjectileFacingAxis axis)
        {
            float travel = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, travel - AxisAngle(axis));
        }

        /// <summary>Orienta <paramref name="transform"/> hacia <paramref name="direction"/> con la punta <paramref name="axis"/>.</summary>
        public static void Face(Transform transform, Vector2 direction, ProjectileFacingAxis axis)
        {
            if (transform == null || direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Rotation(direction, axis);
        }

        private static float AxisAngle(ProjectileFacingAxis axis) => axis switch
        {
            ProjectileFacingAxis.Up => 90f,
            ProjectileFacingAxis.Left => 180f,
            ProjectileFacingAxis.Down => 270f,
            _ => 0f,
        };

        /// <summary>Posición del cursor en el mundo, o null sin ratón / sin cámara.</summary>
        public static Vector2? MouseWorld()
        {
            var mouse = Mouse.current;
            var camera = Camera.main;
            if (mouse == null || camera == null) return null;

            Vector3 screen = mouse.position.ReadValue();
            screen.z = Mathf.Abs(camera.transform.position.z);
            return camera.ScreenToWorldPoint(screen);
        }

        /// <summary>Centro del cuerpo del enemigo vivo más cercano a <paramref name="origin"/>, o null.</summary>
        public static Vector2? NearestEnemyPoint(Vector2 origin, GameObject caster)
        {
            var filter = new ContactFilter2D { useLayerMask = false, useTriggers = true };
            int count = Physics2D.OverlapCircle(origin, NearestEnemySearchRadius, filter, Buffer);

            Vector2? best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var collider = Buffer[i];
                if (collider == null || !collider.enabled) continue;

                var health = collider.GetComponentInParent<Health>();
                if (health == null || health.IsDead || !health.isActiveAndEnabled) continue;
                if (Teams.Allied(caster, health)) continue;

                Vector2 point = collider.bounds.center;
                float distance = (point - origin).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = point;
            }

            return best;
        }
    }
}
