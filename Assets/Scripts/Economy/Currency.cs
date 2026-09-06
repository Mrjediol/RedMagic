namespace RedMagic.Economy
{
    /// <summary>
    /// Las cuatro monedas del juego. Se dividen en dos grupos según qué pasa con ellas al morir:
    ///
    ///  - <see cref="Gold"/> y <see cref="Diamond"/> son moneda <b>de run</b>: se gastan en las
    ///    tiendas que salen a mitad de run y se <b>pierden</b> al volver al hub (mueras o completes).
    ///  - <see cref="SoulFragment"/> y <see cref="Skull"/> son moneda <b>de meta-progresión</b>:
    ///    se gastan en el hub en mejoras permanentes y se <b>conservan</b> siempre.
    ///
    /// <see cref="CurrencyManager.IsRunCurrency"/> es la única fuente de esa clasificación.
    /// </summary>
    public enum Currency
    {
        Gold,
        Diamond,
        SoulFragment,
        Skull
    }

    /// <summary>
    /// Categoría de enemigo a efectos de botín. Determina qué fila de la tabla de drops
    /// (<see cref="CurrencyConfig"/>) se usa al morir. La pone <see cref="CurrencyDropper"/> por
    /// enemigo en el Inspector.
    /// </summary>
    public enum EnemyTier
    {
        /// <summary>Enemigo común: suelta oro y fragmentos de alma.</summary>
        Basic,

        /// <summary>Enemigo de élite: suelta de todo, pero menos que un jefe.</summary>
        Elite,

        /// <summary>Jefe: suelta de todo, en las cantidades más altas.</summary>
        Boss
    }
}
