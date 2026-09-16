using UnityEngine;

namespace RedMagic.UI
{
    /// <summary>
    /// El arte de los menús de <b>pantalla completa</b> — principal, pausa y opciones — en
    /// <c>Resources/MenuSkin.asset</c>. Lo rellena <c>Tools ▸ RedMagic ▸ UI ▸ Menús · Procesar kit
    /// de arte</c> (<c>MenusUiPack</c>), mismo trato que <see cref="ItemMenuSkin"/>: los sprites se
    /// reescriben en cada pasada, los números sólo al crearlo, así que lo ajustado a mano se queda.
    ///
    /// Esos tres menús son UXML+USS (no construidos en código como el de items), pero eso da
    /// igual para vestirlos: <see cref="UiFrame.Dress"/> trabaja sobre cualquier
    /// <c>VisualElement</c>, venga de donde venga. Cualquier pieza vacía cae al aspecto plano del
    /// USS de siempre: <b>sin este asset los menús funcionan y se ven exactamente como antes</b>.
    /// </summary>
    [CreateAssetMenu(menuName = "RedMagic/UI/Menu Skin", fileName = "MenuSkin")]
    public class MenuSkin : ScriptableObject
    {
        public const string ResourcePath = "MenuSkin";

        [Header("Marcos (9-slice por cuadrantes)")]
        [Tooltip("Marco cuadrado (Sample). Libre: hoy no lo usa ninguna pantalla.")]
        public UiFrame window = new();

        [Tooltip("Celda de la rejilla de mejoras permanentes. Usa el mismo dibujo que la placa de " +
                 "título, pero con su propia escala y relleno: una celda es mucho más pequeña y " +
                 "necesita la piedra más fina para que quepan las cuatro líneas de texto.")]
        public UiFrame card = new();

        [Tooltip("Panel apaisado: el cuerpo del menú de opciones.")]
        public UiFrame panel = new();

        [Tooltip("Panel vertical: el cuerpo del menú de pausa y la columna de botones del principal.")]
        public UiFrame panelTall = new();

        [Tooltip("Placa detrás de cada título (RED MAGIC, PAUSA, OPCIONES).")]
        public UiFrame titleBar = new();

        [Header("Botón (los tres estados son el mismo dibujo)")]
        public UiFrame button = new();
        public UiFrame buttonHover = new();
        public UiFrame buttonPressed = new();

        [Header("Piezas sueltas (normal / hover)")]
        [Tooltip("Botón redondo de cerrar/volver.")]
        public UiStateSprites closeButton = new();

        [Tooltip("Casilla sin marcar.")]
        public UiStateSprites checkOff = new();

        [Tooltip("Casilla marcada (interior encendido).")]
        public UiStateSprites checkOn = new();

        [Header("Medidas (px de la resolución de referencia del panel)")]
        [Tooltip("Alto de la placa detrás de los títulos de pausa y opciones. Tiene que dar para " +
                 "la letra del título MÁS la piedra de arriba y abajo (~22 px cada una) más aire, " +
                 "o el texto se sale por arriba.")]
        public float titleBarHeight = 132f;

        [Tooltip("Ancho máximo de esa placa. La placa se ajusta al texto, esto sólo la limita.")]
        public float titleBarMaxWidth = 720f;

        [Tooltip("Alto de la placa del título del menú principal, que lleva una letra mucho mayor.")]
        public float mainTitleHeight = 240f;

        [Tooltip("Ancho máximo de la placa del título del menú principal.")]
        public float mainTitleMaxWidth = 1000f;

        [Tooltip("Lado de la casilla de silenciar en opciones.")]
        public float checkBoxSize = 52f;

        [Tooltip("Lado del botón redondo de cerrar.")]
        public float closeButtonSize = 56f;

        // El tamaño de los botones lo sigue mandando el USS (.menu-button): es la maqueta de cada
        // pantalla, no parte del arte, y el marco se adapta a lo que mida el botón.

        [Header("Colores")]
        [Tooltip("Tinte del marco del botón deshabilitado.")]
        public Color disabledTint = new(0.55f, 0.58f, 0.6f, 1f);

        [Tooltip("Tinte del botón de salir / volver al menú principal.")]
        public Color quitTint = new(1f, 0.72f, 0.68f, 1f);

        [Header("Tintes de la celda de mejora")]
        [Tooltip("Nodo bloqueado: hay que comprar antes el de su izquierda.")]
        public Color cardLockedTint = new(0.5f, 0.53f, 0.55f, 1f);

        [Tooltip("Nodo comprable ahora mismo (desbloqueado y con fragmentos de sobra).")]
        public Color cardBuyableTint = new(1f, 0.93f, 0.74f, 1f);

        [Tooltip("Nodo ya al nivel máximo.")]
        public Color cardMaxedTint = new(1f, 0.84f, 0.45f, 1f);

        public static MenuSkin Load() => Resources.Load<MenuSkin>(ResourcePath);
    }
}
