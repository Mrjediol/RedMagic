using UnityEngine;

namespace RedMagic.Survival
{
    /// <summary>
    /// Lo elegido en el menú antes de entrar al modo (hoy, la dificultad). Lo lee <see cref="SurvivalManager"/>; si la
    /// escena se abre suelta desde el editor no hay nada elegido y usa su dificultad de pruebas.
    /// </summary>
    public static class SurvivalSession
    {
        public static SurvivalDifficulty? Difficulty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Difficulty = null;
    }
}
