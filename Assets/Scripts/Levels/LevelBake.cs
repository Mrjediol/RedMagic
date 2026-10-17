using UnityEngine;

namespace RedMagic.Levels
{
    /// <summary>
    /// El aspecto ya generado de un <see cref="Level"/>, guardado para no recalcularlo al cargar: las mismas mallas
    /// (roca, borde, brillo), sus materiales y la textura del brillo interior (un PNG al lado). Lo escribe el editor al
    /// guardar la escena (<c>LevelBaker</c>, en <c>Assets/Levels/Horneado/</c>); <see cref="signature"/> dice con qué
    /// tiles, tamaño y bioma se hizo, y si ya no cuadra el nivel se vuelve a generar como siempre.
    /// </summary>
    public class LevelBake : ScriptableObject
    {
        [Tooltip("Huella de lo que se horneó (tiles, tamaño, bioma, versión del generador).")]
        public string signature;

        public Mesh rock, rim, glow;
        public Material rockMaterial, rimMaterial, glowMaterial;
        public Texture2D glowTexture;

        public bool IsComplete => rock != null && rim != null && glow != null &&
                                  rockMaterial != null && rimMaterial != null && glowMaterial != null;
    }
}
