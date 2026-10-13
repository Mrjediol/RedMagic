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
        // El límite de FPS lo elige el jugador (Opciones ▸ Límite de FPS, ver PerformanceSettings). Por defecto, la
        // frecuencia de la pantalla: 60 en un móvil normal, 120/144 en uno gaming. Antes estaba fijo en 60.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            // vSyncCount a 0 primero: mientras esté a 1 o más, Unity ignora por completo
            // targetFrameRate y sincroniza al refresco de pantalla — asignar uno sin el otro no
            // tiene efecto. En PC se deja correr libre (-1) salvo límite elegido; en móvil, el elegido
            // o la frecuencia de la pantalla. (!UNITY_EDITOR dentro de Apply: con el build target en Android,
            // UNITY_ANDROID también está definido en el Editor.)
            PerformanceSettings.Apply();
#if UNITY_EDITOR
            // En el Editor, al refresco del monitor (vSync): por encima no se ve nada más y la GPU
            // trabaja al 100% para nada. Con vSync, no durmiendo: no da tirones.
            QualitySettings.vSyncCount = 1;
#endif
        }
    }
}
