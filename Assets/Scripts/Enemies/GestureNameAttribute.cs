using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// Un texto que nombra un gesto del cuerpo. En el Inspector se elige de la lista de gestos que
    /// tiene el <c>ProceduralRig</c> del mismo objeto, en vez de escribirlo; sin rig (un enemigo de
    /// hoja de sprites, cuyo "gesto" es un estado del Animator) queda el campo de texto.
    /// </summary>
    public class GestureNameAttribute : PropertyAttribute
    {
    }
}
