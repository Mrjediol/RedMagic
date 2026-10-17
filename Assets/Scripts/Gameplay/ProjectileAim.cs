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

        /// <summary>
        /// Posición del cursor en el mundo, o null sin ratón / sin cámara. En modo táctil es el último
        /// punto tocado para disparar (<see cref="RedMagic.Core.TouchInput.AimScreen"/>), o la dirección del joystick de
        /// apuntado (<see cref="RedMagic.Core.TouchInput.AimDirection"/>) si la hay.
        /// </summary>
        public static Vector2? MouseWorld()
        {
            var camera = Camera.main;
            if (camera == null) return null;

            // Táctil: la dirección del joystick de apuntado o el punto tocado; sin ninguno, null (sale recto hacia donde se
            // mira). Nunca el ratón: en el simulador del editor también hay uno y daría direcciones sin sentido.
            if (RedMagic.Core.InputDeviceManager.TouchActive)
            {
                var touchDir = RedMagic.Core.TouchInput.AimDirection;
                if (touchDir.HasValue) return FromPlayer(camera, touchDir.Value);
                if (!RedMagic.Core.TouchInput.AimScreen.HasValue) return null;
            }

            // Mando: el stick derecho. Quieto = null (recto hacia donde se mira); inclinado = hacia ahí.
            if (RedMagic.Core.InputDeviceManager.GamepadActive)
            {
                var pad = Gamepad.current;
                Vector2 stick = pad != null ? pad.rightStick.ReadValue() : Vector2.zero;
                return stick.magnitude >= GamepadAimDeadzone ? FromPlayer(camera, stick) : (Vector2?)null;
            }

            Vector3 screen;
            var touchAim = RedMagic.Core.TouchInput.AimScreen;
            if (touchAim.HasValue && RedMagic.Core.InputDeviceManager.TouchActive) screen = touchAim.Value;
            else
            {
                var mouse = Mouse.current;
                if (mouse == null) return null;
                screen = mouse.position.ReadValue();
            }
            // El Input System puede dar NaN (ventana sin foco, cambio de tamaño): convertirlo saca
            // "Screen position out of view frustum" en la consola.
            if (float.IsNaN(screen.x) || float.IsNaN(screen.y) || float.IsInfinity(screen.x) || float.IsInfinity(screen.y)) return null;
            screen.z = Mathf.Abs(camera.transform.position.z);
            return camera.ScreenToWorldPoint(screen);
        }

        /// <summary>Inclinación mínima del stick derecho del mando para apuntar; por debajo el disparo sale recto.</summary>
        public const float GamepadAimDeadzone = 0.35f;

        /// <summary>Un punto lejano desde el jugador en <paramref name="direction"/> (lo que apuntan el joystick táctil y el mando).</summary>
        private static Vector2 FromPlayer(Camera camera, Vector2 direction)
        {
            var shooter = RedMagic.Items.WeaponUser.Current;
            Vector2 from = shooter != null ? (Vector2)shooter.transform.position : (Vector2)camera.transform.position;
            return from + direction.normalized * 20f;
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
