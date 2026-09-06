using UnityEngine;

namespace RedMagic.Core
{
    /// <summary>
    /// Puente entre los controles táctiles en pantalla (UI Toolkit) y los scripts de gameplay.
    /// Los botones de pantalla escriben aquí y <c>PlayerMovement</c> lo suma a lo que llega
    /// del Input System (teclado y mando), de modo que las tres fuentes conviven.
    /// </summary>
    public static class TouchInput
    {
        /// <summary>-1 (izquierda) … 1 (derecha). Lo escribe la UI táctil.</summary>
        public static float Horizontal { get; set; }

        private static bool _jumpQueued;

        /// <summary>La UI táctil lo llama al pulsar el botón de salto.</summary>
        public static void QueueJump() => _jumpQueued = true;

        /// <summary>Devuelve true una sola vez por pulsación de salto.</summary>
        public static bool ConsumeJump()
        {
            if (!_jumpQueued) return false;
            _jumpQueued = false;
            return true;
        }

        public static void Clear()
        {
            Horizontal = 0f;
            _jumpQueued = false;
        }

        // Con "Reload Domain" desactivado los estáticos sobreviven entre sesiones de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
