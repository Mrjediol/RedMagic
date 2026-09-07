namespace RedMagic.Items
{
    /// <summary>
    /// Todas las tags de sinergia del sistema de builds. La familia (elemental / universal) se
    /// deduce del valor numérico: elementales &lt; 100, universales &gt;= 100. Así el
    /// synergy tracker puede llevar un solo diccionario <c>BuildTag → puntos</c> sin ramas por tipo.
    ///
    /// Arcano y Eléctrico quedan reservados (valores 2 y 3) para una fase posterior; no están en el
    /// set mínimo v1.
    /// </summary>
    public enum BuildTag
    {
        // Elementales
        Ice = 0,
        Fire = 1,
        // Arcane = 2,   reservado
        // Electric = 3, reservado

        // Universales
        Tank = 100,
        Haste = 101,
        Lifesteal = 102,
        Reset = 103,
    }

    /// <summary>Familia de una <see cref="BuildTag"/>.</summary>
    public enum TagKind
    {
        Elemental,
        Universal,
    }

    /// <summary>
    /// Identidad elemental de un arma o de un modificador de Elemento. Es un dominio aparte de
    /// <see cref="BuildTag"/> porque aquí "ninguno" (arma física) es un valor válido, mientras que
    /// en el conteo de sinergias simplemente no se suma nada.
    /// </summary>
    public enum ElementId
    {
        None = 0,
        Ice = 1,
        Fire = 2,
        // Arcane, Electric -> fase posterior
    }

    /// <summary>
    /// Helpers de sólo lectura sobre las tags. No guarda estado: el conteo vivo es cosa del
    /// synergy tracker (paso posterior).
    /// </summary>
    public static class BuildTags
    {
        public static readonly BuildTag[] Elementals = { BuildTag.Ice, BuildTag.Fire };

        public static readonly BuildTag[] Universals =
        {
            BuildTag.Tank, BuildTag.Haste, BuildTag.Lifesteal, BuildTag.Reset,
        };

        /// <summary>
        /// Umbrales de sinergia v1: tier 1 a los 2 puntos, tier 2 a los 4, tier 3 a los 6.
        /// Los universales podrían necesitar su propia escala más adelante (reciben aportes de más
        /// fuentes); en v1 comparten esta. Cuando eso pase, esto se muda a un <c>SynergyConfig</c>
        /// por-tag junto con los efectos de umbral.
        /// </summary>
        public static readonly int[] Thresholds = { 2, 4, 6 };

        /// <summary>
        /// Tope de sinergia: a partir de 6 puntos en una tag no se gana nada extra. Permite
        /// desviarse una vez del set puro sin perder el bonus de tier 3.
        /// </summary>
        public const int SynergyCap = 6;

        public static TagKind KindOf(BuildTag tag) =>
            (int)tag < 100 ? TagKind.Elemental : TagKind.Universal;

        public static bool IsElemental(BuildTag tag) => KindOf(tag) == TagKind.Elemental;

        public static bool IsUniversal(BuildTag tag) => KindOf(tag) == TagKind.Universal;

        /// <summary>Nombre para la UI (columna de sinergias, chips de tags del panel de descripción).</summary>
        public static string DisplayName(BuildTag tag) => tag switch
        {
            BuildTag.Ice => "Hielo",
            BuildTag.Fire => "Fuego",
            BuildTag.Tank => "Tanque",
            BuildTag.Haste => "Rapidez",
            BuildTag.Lifesteal => "Vampirismo",
            BuildTag.Reset => "Reset",
            _ => tag.ToString(),
        };

        public static string DisplayName(ElementId element) => element switch
        {
            ElementId.Ice => "Hielo",
            ElementId.Fire => "Fuego",
            _ => "físico",
        };

        /// <summary>
        /// La <see cref="BuildTag"/> que aporta un elemento al conteo, o null si es
        /// <see cref="ElementId.None"/> (un arma física no suma a ninguna sinergia elemental).
        /// </summary>
        public static BuildTag? TagFor(ElementId element) => element switch
        {
            ElementId.Ice => BuildTag.Ice,
            ElementId.Fire => BuildTag.Fire,
            _ => null,
        };
    }
}
