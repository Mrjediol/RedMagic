using UnityEngine;

namespace RedMagic.Core
{
    /// <summary>
    /// Puente entre los controles táctiles en pantalla (UI Toolkit) y los scripts de gameplay.
    /// Los botones de pantalla escriben aquí y <c>PlayerMovement</c> / <c>PlayerAttack</c> lo suman
    /// a lo que llega del Input System (teclado y mando), de modo que las tres fuentes conviven.
    /// </summary>
    public static class TouchInput
    {
        /// <summary>-1 (izquierda) … 1 (derecha). Lo escribe la UI táctil.</summary>
        public static float Horizontal { get; set; }

        /// <summary>True mientras se mantiene pulsado el botón de abajo (atravesar plataformas).</summary>
        public static bool Down { get; set; }

        private static bool _jumpQueued;
        private static bool _attackQueued;
        private static bool _dashQueued;
        private static bool _weaponQueued;
        private static bool _interactQueued;
        private static bool _parryQueued;

        /// <summary>La UI táctil lo llama al pulsar el botón de parry.</summary>
        public static void QueueParry() => _parryQueued = true;

        /// <summary>Devuelve true una sola vez por pulsación de parry.</summary>
        public static bool ConsumeParry()
        {
            if (!_parryQueued) return false;
            _parryQueued = false;
            return true;
        }

        /// <summary>La UI táctil lo llama al pulsar el botón de salto.</summary>
        public static void QueueJump() => _jumpQueued = true;

        /// <summary>Devuelve true una sola vez por pulsación de salto.</summary>
        public static bool ConsumeJump()
        {
            if (!_jumpQueued) return false;
            _jumpQueued = false;
            return true;
        }

        private static bool _jumpReleaseQueued;

        /// <summary>La UI táctil lo llama al soltar el botón de salto (salto corto, como soltar en teclado o mando).</summary>
        public static void QueueJumpRelease() => _jumpReleaseQueued = true;

        /// <summary>Devuelve true una sola vez por cada vez que se suelta el botón de salto.</summary>
        public static bool ConsumeJumpRelease()
        {
            if (!_jumpReleaseQueued) return false;
            _jumpReleaseQueued = false;
            return true;
        }

        /// <summary>La UI táctil lo llama al pulsar el botón de ataque.</summary>
        public static void QueueAttack() => _attackQueued = true;

        /// <summary>Devuelve true una sola vez por pulsación de ataque.</summary>
        public static bool ConsumeAttack()
        {
            if (!_attackQueued) return false;
            _attackQueued = false;
            return true;
        }

        /// <summary>La UI táctil lo llama al pulsar el botón de dash.</summary>
        public static void QueueDash() => _dashQueued = true;

        /// <summary>Devuelve true una sola vez por pulsación de dash.</summary>
        public static bool ConsumeDash()
        {
            if (!_dashQueued) return false;
            _dashQueued = false;
            return true;
        }

        /// <summary>La UI táctil lo llama al pulsar el botón del arma.</summary>
        public static void QueueWeapon() => _weaponQueued = true;

        /// <summary>Devuelve true una sola vez por pulsación del botón del arma.</summary>
        public static bool ConsumeWeapon()
        {
            if (!_weaponQueued) return false;
            _weaponQueued = false;
            return true;
        }

        /// <summary>
        /// True mientras un dedo mantiene el arma (controles por gestos: tocar fuera de la zona de
        /// movimiento). Con arma de carga, mantener carga y soltar dispara; sin carga, dispara en ráfaga.
        /// </summary>
        public static bool WeaponHeld { get; set; }

        /// <summary>
        /// Último punto de pantalla (píxeles, origen abajo a la izquierda) donde apuntó un dedo, o null.
        /// <c>ProjectileAim.MouseWorld</c> lo usa en vez del ratón en modo táctil: el disparo con
        /// "apuntar al ratón" sale hacia donde se tocó.
        /// </summary>
        public static Vector2? AimScreen { get; set; }

        /// <summary>
        /// Dirección elegida con el joystick de apuntado de los controles por gestos (armas que apuntan al cursor), o null.
        /// En modo táctil manda sobre <see cref="AimScreen"/>: <c>ProjectileAim.MouseWorld</c> apunta desde el jugador hacia ella.
        /// </summary>
        public static Vector2? AimDirection { get; set; }

        /// <summary>True mientras se mantiene pulsado el botón táctil de interactuar (reciclar un item del suelo).</summary>
        public static bool InteractHeld { get; set; }

        private static int _interactWantedFrame = -10;

        /// <summary>Algo interactuable está a mano este frame: el botón táctil de interactuar se muestra.</summary>
        public static void RequestInteractButton() => _interactWantedFrame = Time.frameCount;

        /// <summary>¿Lo ha pedido alguien en este frame o el anterior?</summary>
        public static bool InteractButtonWanted => Time.frameCount - _interactWantedFrame <= 1;

        /// <summary>La UI táctil lo llama al pulsar el botón de interactuar (tumbas del hub).</summary>
        public static void QueueInteract() => _interactQueued = true;

        /// <summary>Devuelve true una sola vez por pulsación de interactuar.</summary>
        public static bool ConsumeInteract()
        {
            if (!_interactQueued) return false;
            _interactQueued = false;
            return true;
        }

        public static void Clear()
        {
            Horizontal = 0f;
            Down = false;
            _jumpQueued = false;
            _jumpReleaseQueued = false;
            _attackQueued = false;
            _dashQueued = false;
            _weaponQueued = false;
            _interactQueued = false;
            _parryQueued = false;
            InteractHeld = false;
            WeaponHeld = false;
            AimScreen = null;
            AimDirection = null;
        }

        // Con "Reload Domain" desactivado los estáticos sobreviven entre sesiones de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
