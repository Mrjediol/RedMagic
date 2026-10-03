using UnityEngine;

namespace RedMagic.Core
{
    /// <summary>
    /// Fija el límite de FPS lo antes posible, antes de que cargue la primera escena.
    ///
    /// Sin esto el juego corría a 30 FPS en móvil: <see cref="Application.targetFrameRate"/> nunca
    /// se tocaba en ningún sitio, y su valor por defecto en Android/iOS es 30 (a diferencia de
    /// PC, donde -1 deja correr tan rápido como pueda). Además, el nivel de calidad que usan
    /// Android/iPhone por defecto (Medium, ver <c>ProjectSettings/QualitySettings.asset</c>) trae
    /// <c>vSyncCount: 1</c>, que por sí solo ignora <c>targetFrameRate</c> y sincroniza al
    /// refresco de la pantalla — hace falta tocar los dos a la vez para que el límite cambie de
    /// verdad en el dispositivo.
    /// </summary>
    public static class PerformanceBootstrap
    {
        /// <summary>
        /// FPS objetivo en dispositivos móviles. 60 porque es el refresco nativo de la inmensa
        /// mayoría de móviles; en un panel de 90/120 Hz el juego se queda corto a propósito antes
        /// que arriesgar caídas de frame en gama baja. Súbelo aquí si se quiere aprovechar pantallas
        /// de más refresco.
        /// </summary>
        private const int MobileTargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            // vSyncCount a 0 primero: mientras esté a 1 o más, Unity ignora por completo
            // targetFrameRate y sincroniza al refresco de pantalla — asignar uno sin el otro no
            // tiene efecto.
            QualitySettings.vSyncCount = 0;

            // En PC se deja correr libre (-1). !UNITY_EDITOR es obligatorio: con el build target en
            // Android, UNITY_ANDROID también está definido en el Editor y lo dejaba clavado a 60 (con
            // tirones, porque sin vSync el límite se hace durmiendo).
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Application.targetFrameRate = MobileTargetFrameRate;
#else
            Application.targetFrameRate = -1;
#endif
#if UNITY_EDITOR
            // En el Editor, al refresco del monitor (vSync): por encima no se ve nada más y la GPU
            // trabaja al 100% para nada. Con vSync, no durmiendo: no da tirones.
            QualitySettings.vSyncCount = 1;
#endif
        }
    }
}
