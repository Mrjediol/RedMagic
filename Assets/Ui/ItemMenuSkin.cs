using UnityEngine;

namespace RedMagic.UI
{
    /// <summary>
    /// El arte de la pantalla de items (<see cref="ItemMenuController"/>), en
    /// <c>Resources/ItemMenuSkin.asset</c>. Lo rellena <c>Tools ▸ RedMagic ▸ UI ▸ Items · Procesar
    /// kit de arte</c> (<c>ItemsUiPack</c>): los sprites se reescriben en cada pasada, los números de
    /// abajo sólo la primera vez, así que se pueden ajustar aquí a mano.
    ///
    /// Cualquier pieza vacía cae al aspecto plano de <see cref="MenuStyle"/>: sin skin el menú
    /// funciona igual, sólo que sin arte.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/UI/Item Menu Skin", fileName = "ItemMenuSkin")]
    public class ItemMenuSkin : ScriptableObject
    {
        public const string ResourcePath = "ItemMenuSkin";

        [Header("Marcos (9-slice por cuadrantes)")]
        [Tooltip("Marco exterior de toda la ventana.")]
        public UiFrame window = new();

        [Tooltip("Columnas laterales: Sinergias y Descripción.")]
        public UiFrame sidePanel = new();

        [Tooltip("Columna central: Equipo.")]
        public UiFrame middlePanel = new();

        [Tooltip("Barra detrás de cada título (ITEMS, SINERGIAS, EQUIPO, DESCRIPCIÓN).")]
        public UiFrame titleBar = new();

        [Tooltip("Cada fila de sinergia.")]
        public UiFrame synergyRow = new();

        [Header("Slots (normal / hover)")]
        [Tooltip("Slot de item vacío.")]
        public UiStateSprites emptySlot = new();

        [Tooltip("Slot con un item equipado (sin icono).")]
        public UiStateSprites filledSlot = new();

        [Tooltip("Slot hueco: se usa cuando el item o el arma tiene icono, que se ve por el hueco.")]
        public UiStateSprites iconSlot = new();

        [Tooltip("Slot del arma (sin icono).")]
        public UiStateSprites weaponSlot = new();

        public UiStateSprites closeButton = new();

        [Header("Medidas (px de la resolución de referencia 1600×900)")]
        [Tooltip("Ancho de las dos columnas laterales (Sinergias y Descripción). Es UNO para las dos a " +
                 "propósito: la central ocupa el resto, así que sólo queda centrada si los lados miden igual.")]
        public float sideColumnWidth = 320f;

        [Tooltip("Lado del marco de slot. Cuatro filas de slots + textos tienen que caber en la columna " +
                 "también en un móvil 19.5:9 (≈810 px de alto virtual): 64 es el máximo que cabe.")]
        public float slotFrameSize = 64f;
        public float slotWidth = 150f;
        public float closeButtonSize = 64f;
        public float columnTitleHeight = 46f;
        public float columnTitleMaxWidth = 290f;
        public float synergyRowHeight = 50f;

        [Tooltip("Relleno del texto de la descripción dentro de su columna.")]
        public UiInsets descriptionPadding = new(16, 12, 0, 0);

        [Tooltip("Separación entre la línea de ayuda de abajo y el borde inferior del marco.")]
        public float hintBottomMargin = 12f;

        [Header("Colores")]
        [Tooltip("Fondo del hueco de un slot con icono.")]
        public Color iconWellColor = new(0.05f, 0.11f, 0.12f, 1f);

        [Tooltip("Tinte del marco de una sinergia a la que aporta el item bajo el ratón.")]
        public Color synergyHighlightTint = new(1f, 0.8f, 0.42f, 1f);

        [Tooltip("Tinte del marco de una sinergia al tope (6).")]
        public Color synergyCappedTint = new(1f, 0.92f, 0.7f, 1f);

        [Tooltip("Anillo de selección (ratón / foco de mando) de las filas de sinergia.")]
        public Color hoverRing = new(0.45f, 1f, 0.95f, 0.9f);

        public static ItemMenuSkin Load() => Resources.Load<ItemMenuSkin>(ResourcePath);
    }
}
