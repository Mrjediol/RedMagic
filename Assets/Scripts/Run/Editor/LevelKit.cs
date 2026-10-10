using UnityEngine;

namespace RedMagic.RunEditor
{
    /// <summary>
    /// Kit de arte de un mundo para <see cref="LevelSketchImporter"/>: una textura de relleno y
    /// tres tiras de borde. Todo se cambia en el Inspector sin tocar código, y el importador no
    /// necesita nada más para vestir cualquier boceto.
    ///
    /// Las tiras se dibujan HORIZONTALES con el lado exterior ARRIBA (la hierba, la escarcha…):
    /// el importador las gira para suelo, techo y paredes. El grosor del borde es la altura del
    /// propio sprite, así que el arte manda; si la tira no encaja en sus extremos se verá la
    /// junta al repetirla.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelKit", menuName = "RedMagic/Level Kit")]
    public class LevelKit : ScriptableObject
    {
        [Tooltip("Textura repetible del interior de la roca. Wrap Mode = Repeat.")]
        public Texture2D fill;

        [Min(0.5f), Tooltip("Cuántas unidades (celdas) cubre una repetición de la textura de relleno.")]
        public float fillTileUnits = 4f;

        [System.Serializable]
        public class ParallaxLayerDef
        {
            [Tooltip("Imagen repetible en todas las direcciones (se genera en espejo desde una imagen cualquiera).")]
            public Sprite sprite;
            [Tooltip("Fracción del movimiento de la cámara. Cerca de 0 = muy lejana.")]
            public Vector2 factor = new Vector2(0.15f, 0.08f);
            public Color tint = Color.white;
        }

        [Tooltip("Capas de fondo con parallax, de la más lejana a la más cercana. Detrás de todo el terreno.")]
        public ParallaxLayerDef[] layers;

        [Tooltip("Borde de los suelos (aire encima del terreno). Tira horizontal, exterior arriba.")]
        public Sprite floorEdge;

        [Tooltip("Borde de los techos (aire debajo). Si se deja vacío se reutiliza el del suelo.")]
        public Sprite ceilingEdge;

        [Tooltip("Borde de las paredes. Si se deja vacío se reutiliza el del suelo.")]
        public Sprite wallEdge;

        [Min(0f), Tooltip("Cuánto sobresale la tira hacia el aire, en unidades. Tapa la junta con el relleno.")]
        public float edgeOverhang = 0.1f;
    }
}
