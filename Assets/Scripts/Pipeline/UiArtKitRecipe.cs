using System;
using UnityEngine;

namespace RedMagic.Pipeline
{
    /// <summary>Cómo se va a estirar una pieza de UI al pintarla.</summary>
    public enum UiSliceMode
    {
        /// <summary>Sin cortes: se pinta entera, a su proporción (slots, botones).</summary>
        None,

        /// <summary>
        /// Marco que se estira sin deformar sus adornos: la pieza se parte en 4 cuadrantes, cada uno
        /// un 9-slice cuyos bordes fijos cubren esquina, runas y la mitad del adorno central de cada
        /// lado. Sólo se estira una tira de 2 px elegida sola donde el arte es liso. Ver
        /// <c>RedMagic.UI.UiFrame</c>.
        /// </summary>
        Quad,
    }

    /// <summary>Una imagen del kit: una pieza sola o una rejilla de estados (normal / hover…).</summary>
    [Serializable]
    public class UiArtPiece
    {
        [Tooltip("Nombre base del PNG de salida (sin prefijo). En una rejilla es el prefijo de cada celda " +
                 "si 'cellNames' no está completo.")]
        public string name = "Piece";

        [Tooltip("Imagen tal como llega del artista. No se modifica nunca.")]
        public Texture2D source;

        [Min(1)] public int columns = 1;
        [Min(1)] public int rows = 1;

        [Tooltip("Nombre de cada celda, por filas empezando arriba a la izquierda. Vacío = <name>_<i>.")]
        public string[] cellNames;

        [Tooltip("Piezas con el mismo grupo (≠ 0) comparten recorte, para que normal y hover de dos " +
                 "archivos distintos queden alineados al intercambiarse. Las celdas de una rejilla ya lo " +
                 "comparten siempre.")]
        public int trimGroup;

        [Tooltip("Franja (px) junto al fondo donde el color se des-mezcla del fondo en vez de cortarse: " +
                 "3-4 para un contorno limpio, 48-64 para halos de brillo (hover).")]
        [Min(0)] public int softEdge = 4;

        [Tooltip("Quitar también el fondo encerrado grande (el hueco de un marco con 'espacio para icono').")]
        public bool keyEnclosed = true;

        public UiSliceMode slice = UiSliceMode.None;

        [Tooltip("Quad: franja (fracción del ancho/alto desde cada borde) donde buscar la columna/fila lisa " +
                 "que se estira. x = desde, y = hasta.")]
        public Vector2 stretchSearch = new(0.12f, 0.42f);

        [Tooltip("Quad: el panel liso del interior, en px de la imagen FUENTE tal como se ve (x, y = esquina " +
                 "superior izquierda; ancho, alto), justo dentro del bisel interior. Se pinta encima de los " +
                 "cuadrantes en una sola pieza: sin él, el interior sale de estirar tiras y se ve partido en " +
                 "rectángulos. Se mide una vez sobre la imagen. Ancho 0 = sin relleno.")]
        public RectInt interior;
    }

    /// <summary>
    /// Receta de un kit de arte de UI (<c>&lt;Kit&gt;.uikit.asset</c>): qué imágenes, cómo se cortan y
    /// cómo se estiran. La procesa <c>UiArtKitProcessor</c>: quita el fondo liso (mismo criterio que
    /// <see cref="SpriteSheetRecipe.keyBackground"/>), recorta, separa estados y deja sprites con sus
    /// bordes de 9-slice puestos. Re-ejecutable: sobrescribe los PNG generados, nunca las fuentes.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/Pipeline/UI Art Kit Recipe", fileName = "Kit.uikit")]
    public class UiArtKitRecipe : ScriptableObject
    {
        [Tooltip("Carpeta de salida de los PNG limpios.")]
        public string outputFolder = "Assets/Art/UI/Kit";

        [Tooltip("Prefijo de cada PNG generado.")]
        public string prefix = "Kit_";

        [Tooltip("Distancia al color de fondo (0-1) por debajo de la cual un pixel es fondo. JPG blanco: ~0.12.")]
        [Range(0.01f, 0.5f)] public float backgroundTolerance = 0.12f;

        [Tooltip("En la franja suave, opacidad 'color a alfa' a partir de la cual el pixel es opaco del todo. " +
                 "Sin esto la piedra desaturada junto a un halo saldría translúcida; más bajo = halos más densos.")]
        [Range(0.2f, 1f)] public float softOpaqueAt = 0.55f;

        [Tooltip("Área mínima (fracción de la imagen) de un hueco encerrado para quitarlo con 'keyEnclosed'. " +
                 "Deja en paz los brillos pequeños del arte.")]
        [Range(0.001f, 0.2f)] public float minEnclosedArea = 0.01f;

        [Tooltip("Margen transparente alrededor del recorte, en px.")]
        [Min(0)] public int margin = 2;

        public UiArtPiece[] pieces = Array.Empty<UiArtPiece>();
    }
}
