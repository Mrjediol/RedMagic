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

        /// <summary>True mientras se mantiene pulsado el botón de agacharse.</summary>
        public static bool Crouch { get; set; }

        private static bool _jumpQueued;
        private static bool _attackQueued;
        private static bool _dashQueued;
        private static bool _fireballQueued;
        private static bool _interactQueued;

        /// <summary>La UI táctil lo llama al pulsar el botón de salto.</summary>
        public static void QueueJump() => _jumpQueued = true;

        /// <summary>Devuelve true una sola vez por pulsación de salto.</summary>
        public static bool ConsumeJump()
        {
            if (!_jumpQueued) return false;
            _jumpQueued = false;
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

        /// <summary>La UI táctil lo llama al pulsar el botón de bola de fuego.</summary>
        public static void QueueFireball() => _fireballQueued = true;

        /// <summary>Devuelve true una sola vez por pulsación de bola de fuego.</summary>
        public static bool ConsumeFireball()
        {
            if (!_fireballQueued) return false;
            _fireballQueued = false;
            return true;
        }

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
            Crouch = false;
            _jumpQueued = false;
            _attackQueued = false;
            _dashQueued = false;
            _fireballQueued = false;
            _interactQueued = false;
        }

        // Con "Reload Domain" desactivado los estáticos sobreviven entre sesiones de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
