using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>
    /// TEMPORAL — trucos de prueba que se encienden desde Opciones ▸ General (sección "Pruebas"). No se guardan: vuelven a
    /// apagarse al empezar cada sesión de juego. Los aplica <see cref="Health.TakeDamage(float)"/>:
    /// <see cref="GodMode"/> = el jugador no recibe daño; <see cref="OneHitKill"/> = cualquier daño a un enemigo lo mata (sólo
    /// el jugador daña a los enemigos, así que vale para cuerpo a cuerpo, armas y lo que sea). Quitar antes de publicar.
    /// </summary>
    public static class DebugCheats
    {
        public static bool GodMode { get; set; }
        public static bool OneHitKill { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GodMode = false;
            OneHitKill = false;
        }
    }
}
