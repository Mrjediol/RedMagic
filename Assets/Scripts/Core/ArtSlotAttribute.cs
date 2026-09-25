using System;

namespace RedMagic.Core
{
    /// <summary>Qué clase de arte va en un hueco. Mismo vocabulario que los "kinds" de la web.</summary>
    public enum ArtSlotKind
    {
        /// <summary>Algo que vuela y choca (bala, roca lanzada). Prefab con <c>Projectile</c>.</summary>
        Projectile,
        /// <summary>Efecto de un solo uso (impacto, explosión, destello). Prefab con <c>VfxOneShot</c>.</summary>
        Fx,
        /// <summary>Aviso de telegrafiado (círculo, flecha). Prefab con <c>FxTelegraph</c>.</summary>
        Warning,
        /// <summary>Objeto que el personaje sujeta (la roca sobre la cabeza). Prefab con sprite.</summary>
        Prop,
    }

    /// <summary>
    /// Marca un campo (GameObject o <c>ProjectileSpec</c>) como <b>hueco de arte</b> de un ataque:
    /// qué pide, cómo se llama para el usuario y qué sale si se deja vacío. El editor de jefes y el
    /// catálogo que se exporta a la web lo leen para decir "este ataque necesita un proyectil y un
    /// efecto de impacto" sin mantener ninguna lista a mano.
    ///
    /// Regla de todos los huecos: <b>vacío siempre funciona</b> (placeholder de código), así que
    /// un ataque se puede probar antes de tener su arte.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ArtSlotAttribute : Attribute
    {
        public readonly string Label;
        public readonly ArtSlotKind Kind;
        public readonly string Placeholder;

        public ArtSlotAttribute(string label, ArtSlotKind kind, string placeholder = "forma de color de la fase")
        {
            Label = label;
            Kind = kind;
            Placeholder = placeholder;
        }
    }
}
