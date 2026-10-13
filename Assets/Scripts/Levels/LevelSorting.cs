namespace RedMagic.Levels
{
    /// <summary>
    /// Orden de dibujo de todo lo del nivel, en un solo sitio. Va en la capa <c>Default</c> con órdenes NEGATIVOS
    /// (convención del proyecto para fondos): enemigos, proyectiles y FX están en <c>Default</c> con orden 0..20, y
    /// <c>Default</c> se pinta ANTES que <c>BackGround</c>/<c>Terrain</c>, así que si el nivel usara esas capas los
    /// taparía. El jugador está en <c>Characters</c> (orden 10); lo que va delante de todo, también ahí.
    /// </summary>
    public static class LevelSorting
    {
        public const string Layer = "Default";
        public const string FrontLayer = "Characters";

        public const int Bounds = -2000;           // BG invisible (límites de cámara)
        public const int Background = -1000;       // + índice de la capa de parallax
        public const int BackgroundSnow = -600;    // nieve de fondo: delante del parallax, detrás del terreno
        public const int Rock = -300;              // relleno de roca
        public const int Glow = -299;              // sombra + brillo interior
        public const int Rim = -298;               // bandas y contorno del borde
        public const int OverTerrain = -200;       // + índice: puertas y objetos apoyados en el suelo
        public const int Front = 50;               // + índice, en FrontLayer: delante del jugador
        public const int GameplaySnow = 20;        // en FrontLayer: nieve del plano del juego
    }
}
