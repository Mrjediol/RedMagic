using UnityEngine;

namespace RedMagic.Arsenal
{
    /// <summary>
    /// Assets que las armas del arsenal cargan en tiempo de ejecución (sin cablear nada en escena): los materiales de los
    /// lienzos (normal y aditivo). Vive en <c>Resources/Arsenal/ArsenalAssets.asset</c>; lo crea
    /// <b>Tools ▸ RedMagic ▸ Arsenal ▸ Preparar</b> (también mete los shaders en la build al estar referenciados).
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Arsenal/Assets", fileName = "ArsenalAssets")]
    public sealed class ArsenalAssets : ScriptableObject
    {
        public const string ResourcePath = "Arsenal/ArsenalAssets";

        [Tooltip("Material de los lienzos normales (shader RedMagic/Fx Vertex Color).")]
        public Material solidMaterial;

        [Tooltip("Material de los lienzos de brillos (shader RedMagic/Fx Vertex Color Additive).")]
        public Material glowMaterial;

        [Header("Reglas comunes: estados y reacciones (G.tuning.react de la web)")]
        [Tooltip("Segundos que dura congelado al llenarse la barra de congelación.")]
        [Min(0.1f)] public float frozenTime = 2.5f;
        [Tooltip("Lo que se vacía la barra de congelación por segundo si no se sigue enfriando.")]
        [Min(0f)] public float freezeDecay = 0.18f;
        [Tooltip("Daño de cada tick de quemadura.")]
        [Min(0f)] public float burnTickDamage = 4f;
        [Tooltip("Segundos entre ticks de quemadura.")]
        [Min(0.05f)] public float burnTickEvery = 0.35f;
        [Tooltip("Añicos (congelado + golpe fuerte): daño extra al congelado.")]
        [Min(0f)] public float shatterDamage = 45f;
        [Tooltip("Añicos: radio de la metralla de hielo alrededor (unidades).")]
        [Min(0f)] public float shatterRadius = 2f;
        [Tooltip("Añicos: daño de la metralla a los de alrededor.")]
        [Min(0f)] public float shatterSplash = 15f;
        [Tooltip("Vapor (quemado + hielo): daño en área.")]
        [Min(0f)] public float vaporDamage = 25f;
        [Tooltip("Vapor: radio (unidades).")]
        [Min(0f)] public float vaporRadius = 1.75f;
        [Tooltip("Vapor: segundos que aturde.")]
        [Min(0f)] public float vaporStun = 0.6f;

        [Header("Sonidos (por id, los SND.* de la web)")]
        [Tooltip("Tabla id → sonido. Un id sin clips no suena. Los WAV salen del mismo código de síntesis de la web.")]
        public System.Collections.Generic.List<ArsenalSoundEntry> sounds = new System.Collections.Generic.List<ArsenalSoundEntry>();

        private static ArsenalAssets _cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cached = null;

        public static ArsenalAssets Load()
        {
            if (_cached == null) _cached = Resources.Load<ArsenalAssets>(ResourcePath);
            return _cached;
        }
    }
}
