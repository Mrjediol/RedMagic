using UnityEngine;

namespace RedMagic.Core
{
    /// <summary>
    /// Para campos (o listas) <c>[SerializeReference]</c> de un tipo base abstracto: el Inspector
    /// dibuja un desplegable con todas sus subclases concretas y, debajo, los valores de la elegida.
    /// Es lo que permite que un item elija sus efectos y los afine sin crear un asset por efecto.
    /// El nombre del desplegable sale de <c>[DisplayName]</c> en la clase, si lo tiene.
    /// </summary>
    public class SubclassPickerAttribute : PropertyAttribute
    {
    }
}
