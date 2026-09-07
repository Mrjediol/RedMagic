namespace RedMagic.Items
{
    /// <summary>
    /// Base de los tres items de slot dedicado (<see cref="ElementModifier"/>,
    /// <see cref="TrajectoryModifier"/>, <see cref="ShapeModifier"/> — cada uno en su propio
    /// archivo, como los arquetipos de habilidad). Cada uno transforma el "shot base" del arma
    /// (<see cref="WeaponDefinition.BaseShot"/>) en un punto fijo de la cascada de resolución:
    ///
    /// <code>
    /// 1. TRAYECTORIA  → cómo viaja el proyectil/hitbox
    /// 2. FORMA        → cuántas veces / dónde aplica el daño
    /// 3. ELEMENTO     → qué tipo de daño/status "pinta" el resultado
    /// </code>
    /// </summary>
    public abstract class WeaponModifier : ItemDefinition
    {
        /// <summary>Posición en la cascada. Menor = se aplica antes. Trayectoria 1, Forma 2, Elemento 3.</summary>
        public abstract int PipelineOrder { get; }

        /// <summary>
        /// Muta el <see cref="WeaponShot"/> en su sitio con lo que este modificador cambia. Se
        /// llama desde <see cref="ShotResolver"/> en el orden de <see cref="PipelineOrder"/>.
        /// El asset sigue sin estado: aquí sólo lee sus propios campos y escribe en el shot.
        /// </summary>
        public abstract void Apply(WeaponShot shot);

        /// <summary>
        /// Frase corta de lo que hace mecánicamente, para el panel de descripción de la UI (igual
        /// que <c>AbilityDefinition.ShortStats</c>). La descripción autora sigue en
        /// <see cref="ItemDefinition.Description"/>.
        /// </summary>
        public abstract string EffectSummary();
    }
}
