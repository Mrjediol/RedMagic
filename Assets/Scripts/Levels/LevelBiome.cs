using UnityEngine;

namespace RedMagic.Levels
{
    public enum RimStyle
    {
        [InspectorName("Bandas (actual)")] Bandas,
        [InspectorName("A · Nieve acumulada")] NieveAcumulada,
        [InspectorName("B · Hielo cristalino")] HieloCristal,
    }

    /// <summary>
    /// Todo lo que cambia de un bioma a otro (hielo, bosque, volcán) para el mismo sistema de niveles: la
    /// textura de la roca, los colores y grosores del borde, el fondo de parallax y las partículas. Un nivel
    /// sólo apunta a su bioma; cambiar un valor aquí cambia todos los niveles de ese bioma.
    /// Valores de partida del hielo = los del boceto web validado (40 px de boceto = 1 unidad).
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Niveles/Bioma", fileName = "Bioma")]
    public class LevelBiome : ScriptableObject
    {
        /// <summary>Un valor del bioma ha cambiado en el Inspector: los <see cref="Level"/> que lo usan se redibujan.</summary>
        public static event System.Action<LevelBiome> Changed;

        private void OnValidate() => Changed?.Invoke(this);

        [Header("Roca (relleno del terreno)")]
        [Tooltip("Textura repetible de la roca. Se repite en coordenadas de mundo.")]
        public Texture2D rockTexture;
        [Tooltip("Cuántas unidades ocupa una repetición de la textura.")]
        [Min(0.5f)] public float rockTileUnits = 5.4f;
        [Tooltip("Oscurecimiento de toda la roca (el borde lo vuelve a aclarar cerca del aire).")]
        public Color rockShadow = new Color(0.016f, 0.055f, 0.133f, 0.45f);
        [Tooltip("Shader del relleno (iluminado: reacciona a las luces 2D).")]
        public Shader rockShader;

        [Header("Borde")]
        [Tooltip("Radio de todas las esquinas, en unidades. 0.4 = 16 px del boceto. SÓLO visual: el collider " +
                 "tiene siempre esquinas cuadradas. 0 = esquinas en ángulo recto.")]
        [Range(0f, 0.5f)] public float cornerRadius = 0.4f;
        [Tooltip("Grosor del borde en suelos (cara hacia arriba).")]
        public float floorWidth = 0.625f;
        [Tooltip("Grosor del borde en paredes.")]
        public float wallWidth = 0.4f;
        [Tooltip("Grosor del borde en techos (cara hacia abajo).")]
        public float ceilingWidth = 0.35f;
        [Tooltip("Cuánto varía el grosor a lo largo del borde (0 = recto).")]
        [Range(0f, 1f)] public float widthVariation = 0.5f;
        [Tooltip("Las 4 bandas del borde de fuera hacia dentro, en suelos.")]
        public Color[] floorBands =
        {
            new Color32(63, 149, 208, 255), new Color32(134, 205, 242, 255),
            new Color32(205, 238, 252, 255), new Color32(255, 255, 255, 255),
        };
        [Tooltip("Las 4 bandas en techos; las paredes mezclan las dos.")]
        public Color[] ceilingBands =
        {
            new Color32(40, 104, 170, 255), new Color32(84, 160, 218, 255),
            new Color32(150, 210, 244, 255), new Color32(210, 240, 255, 255),
        };
        [Tooltip("Proporción del grosor que ocupa cada banda (la 1ª es la más ancha).")]
        public float[] bandFractions = { 1f, 0.68f, 0.4f, 0.17f };
        public Color outline = new Color32(4, 17, 42, 255);
        [Min(0f)] public float outlineWidth = 0.11f;
        [Tooltip("Brillo hacia el aire (se desvanece).")]
        public Color outerGlow = new Color(0.75f, 0.91f, 0.98f, 0.35f);
        [Min(0f)] public float outerGlowWidth = 0f;
        [Tooltip("Brillo hacia dentro de la roca (se desvanece): da profundidad.")]
        public Color innerGlow = new Color(0.31f, 0.76f, 1f, 0.28f);
        [Min(0f)] public float innerGlowWidth = 1.6f;
        [Tooltip("Shader del borde (color por vértice, sin luz).")]
        public Shader rimShader;

        [Header("Estilo del borde")]
        [Tooltip("Bandas = el borde de siempre. Las variantes usan los mismos colores, grosores y contorno de arriba.")]
        public RimStyle rimStyle = RimStyle.Bandas;
        [Tooltip("A · Nieve acumulada: alto de los montones de nieve sobre los suelos, en unidades.")]
        [Min(0f)] public float snowPileHeight = 0.28f;
        [Tooltip("B · Hielo cristalino: largo máximo de los carámbanos de los techos, en unidades. 0 = sin carámbanos.")]
        [Min(0f)] public float icicleLength = 0.5f;

        [Header("Ambiente")]
        [Tooltip("Fondo de parallax del bioma (prefab hecho con 'Importar fondo de parallax').")]
        public GameObject background;
        [Tooltip("Partículas del bioma (nieve, hojas, ceniza...).")]
        public GameObject particles;
    }
}
