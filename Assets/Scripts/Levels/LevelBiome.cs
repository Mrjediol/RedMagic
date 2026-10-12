using UnityEngine;

namespace RedMagic.Levels
{
    public enum RimInUse
    {
        [InspectorName("Actual")] Actual,
        [InspectorName("Variante A · Grumos")] VarianteA,
        [InspectorName("Variante B · Goteo")] VarianteB,
    }

    /// <summary>Valores de una nieve del borde. Las bandas van de fuera (la superficie) hacia dentro de la roca.</summary>
    [System.Serializable]
    public class RimLook
    {
        [Tooltip("Grosor en suelos (cara hacia arriba), paredes y techos.")]
        public float floorWidth = 0.625f;
        public float wallWidth = 0.4f;
        public float ceilingWidth = 0.35f;
        [Range(0f, 1f)] public float widthVariation = 0.5f;
        [Tooltip("4 colores en suelos: [0] el más hondo … [3] la superficie.")]
        public Color[] floorBands =
        {
            new Color32(63, 149, 208, 255), new Color32(134, 205, 242, 255),
            new Color32(205, 238, 252, 255), new Color32(255, 255, 255, 255),
        };
        [Tooltip("Los mismos 4 en techos; las paredes mezclan.")]
        public Color[] ceilingBands =
        {
            new Color32(40, 104, 170, 255), new Color32(84, 160, 218, 255),
            new Color32(150, 210, 244, 255), new Color32(210, 240, 255, 255),
        };
        public Color outline = new Color32(4, 17, 42, 255);
        [Min(0f)] public float outlineWidth = 0.11f;

        public void CopyFrom(RimLook o)
        {
            floorWidth = o.floorWidth; wallWidth = o.wallWidth; ceilingWidth = o.ceilingWidth; widthVariation = o.widthVariation;
            floorBands = (Color[])o.floorBands.Clone(); ceilingBands = (Color[])o.ceilingBands.Clone();
            outline = o.outline; outlineWidth = o.outlineWidth;
        }
    }

    [System.Serializable]
    public class RimLookGrumos : RimLook
    {
        [Tooltip("Ancho de cada grumo, en unidades.")]
        [Min(0.1f)] public float lumpSize = 0.45f;
        [Tooltip("Lo que se meten los grumos (0 = recto, 1 = grumos muy marcados).")]
        [Range(0f, 1f)] public float lumpDepth = 0.35f;
    }

    [System.Serializable]
    public class RimLookGoteo : RimLook
    {
        [Tooltip("Largo máximo de los goterones, en unidades. 0 = sin goterones.")]
        [Min(0f)] public float dripLength = 0.45f;
        [Tooltip("Separación media entre goterones, en unidades.")]
        [Min(0.3f)] public float dripSpacing = 1.1f;
        [Tooltip("Parte de la banda que va en sombra (el tono oscuro, hacia dentro).")]
        [Range(0f, 0.9f)] public float shadowFraction = 0.35f;
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

        [Header("Nieve del borde en uso")]
        [Tooltip("Qué nieve del borde se dibuja. Cada variante tiene sus propios valores (abajo): probarlas no toca " +
                 "la actual (los valores de 'Borde' de arriba).")]
        public RimInUse rimInUse = RimInUse.Actual;
        [Tooltip("Variante A · Grumos: la banda acaba hacia dentro en grumos redondos, con degradado suave.")]
        public RimLookGrumos varianteA = new RimLookGrumos();
        [Tooltip("Variante B · Goteo: dos tonos planos (luz y sombra) y goterones que bajan desde los suelos.")]
        public RimLookGoteo varianteB = new RimLookGoteo();

        /// <summary>La nieve actual (los campos de 'Borde') con la misma forma que las variantes.</summary>
        public RimLook CurrentRim() => new RimLook
        {
            floorWidth = floorWidth, wallWidth = wallWidth, ceilingWidth = ceilingWidth, widthVariation = widthVariation,
            floorBands = floorBands, ceilingBands = ceilingBands, outline = outline, outlineWidth = outlineWidth,
        };

        public RimLook ActiveRim() => rimInUse == RimInUse.VarianteA ? varianteA : rimInUse == RimInUse.VarianteB ? varianteB : CurrentRim();

        [ContextMenu("Copiar la nieve actual a las dos variantes")]
        public void CopyCurrentToVariants()
        {
            varianteA.CopyFrom(CurrentRim());
            varianteB.CopyFrom(CurrentRim());
            Changed?.Invoke(this);
        }

        [Header("Ambiente")]
        [Tooltip("Fondo de parallax del bioma (prefab hecho con 'Importar fondo de parallax').")]
        public GameObject background;
        [Tooltip("Partículas del bioma (nieve, hojas, ceniza...).")]
        public GameObject particles;
    }
}
