namespace RedMagic.UI
{
    /// <summary>
    /// Pantalla de menú que se puede mostrar u ocultar. La implementan MainMenuController,
    /// OptionsMenuController y PauseMenuController para poder navegar entre ellas sin acoplarlas
    /// a un tipo concreto (p. ej. el botón Volver de Opciones vuelve a la pantalla que la abrió).
    /// </summary>
    public interface IMenuScreen
    {
        void SetVisible(bool visible);
    }
}
