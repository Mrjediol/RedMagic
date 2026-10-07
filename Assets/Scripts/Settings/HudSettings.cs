using System;
using UnityEngine;

namespace RedMagic.Settings
{
    /// <summary>
    /// Preferencias del HUD que el jugador cambia en Opciones. Se guardan en <c>PlayerPrefs</c> y
    /// avisan con <see cref="Changed"/> para que el HUD se repinte en el acto.
    /// </summary>
    public static class HudSettings
    {
        private const string ShowControlsKey = "settings.hud.showControls";

        /// <summary>Se dispara al cambiar cualquier preferencia del HUD.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Changed = null;

        /// <summary>Enseñar la chuleta de controles en la esquina (sólo con teclado y ratón). Por defecto sí.</summary>
        public static bool ShowControls
        {
            get => PlayerPrefs.GetInt(ShowControlsKey, 1) != 0;
            set
            {
                if (value == ShowControls) return;
                PlayerPrefs.SetInt(ShowControlsKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }
    }
}
