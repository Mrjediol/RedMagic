using System;
using UnityEngine;

namespace RedMagic.Core
{
    /// <summary>
    /// Preferencias de rendimiento del jugador: límite de FPS y contador en pantalla. Se guardan en PlayerPrefs y se
    /// aplican al arrancar (<see cref="PerformanceBootstrap"/>) y al cambiarlas en Opciones.
    /// </summary>
    public static class PerformanceSettings
    {
        private const string FpsLimitKey = "settings.perf.fpsLimit";
        private const string ShowFpsKey = "settings.perf.showFps";

        /// <summary>Opciones del selector. 0 = lo que dé la pantalla (60, 120, 144…).</summary>
        public static readonly int[] FpsLimits = { 30, 60, 90, 120, 144, 0 };

        /// <summary>Se dispara al cambiar el límite o el contador.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Changed = null;

        /// <summary>Límite elegido (uno de <see cref="FpsLimits"/>). Por defecto 0: la frecuencia de la pantalla.</summary>
        public static int FpsLimit
        {
            get
            {
                int v = PlayerPrefs.GetInt(FpsLimitKey, 0);
                return Array.IndexOf(FpsLimits, v) >= 0 ? v : 0;
            }
            set
            {
                PlayerPrefs.SetInt(FpsLimitKey, value);
                PlayerPrefs.Save();
                Apply();
                Changed?.Invoke();
            }
        }

        /// <summary>Contador de FPS en pantalla. Por defecto, sólo en editor y Development Build.</summary>
        public static bool ShowFps
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || REDMAGIC_FPS
                const int fallback = 1;
#else
                const int fallback = 0;
#endif
                return PlayerPrefs.GetInt(ShowFpsKey, fallback) == 1;
            }
            set
            {
                PlayerPrefs.SetInt(ShowFpsKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>La frecuencia más alta que admite la pantalla (la actual o la mayor de sus modos).</summary>
        public static int ScreenRefreshRate()
        {
            double best = Screen.currentResolution.refreshRateRatio.value;
            foreach (var r in Screen.resolutions) best = Math.Max(best, r.refreshRateRatio.value);
            return Mathf.Max(30, Mathf.RoundToInt((float)best));
        }

        /// <summary>FPS objetivo real para un límite (0 = el de la pantalla).</summary>
        public static int TargetFor(int limit) => limit > 0 ? limit : ScreenRefreshRate();

        /// <summary>Aplica el límite: sin vSync (si no, Unity ignora targetFrameRate) y con el objetivo elegido.</summary>
        public static void Apply()
        {
            QualitySettings.vSyncCount = 0;
#if UNITY_ANDROID || UNITY_IOS
#if !UNITY_EDITOR
            Application.targetFrameRate = TargetFor(FpsLimit);
            return;
#endif
#endif
            // PC: libre (-1); en el Editor se sincroniza al monitor (ver PerformanceBootstrap).
            Application.targetFrameRate = FpsLimit > 0 && !Application.isEditor ? FpsLimit : -1;
        }
    }
}
