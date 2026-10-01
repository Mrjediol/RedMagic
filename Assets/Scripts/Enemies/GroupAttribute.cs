using System;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// Como <c>[Header]</c>, pero además <b>agrupa</b>: los inspectores de enemigos y jefes
    /// (<c>FoldoutInspector</c>) dibujan un desplegable por cada grupo. Fuera de esos inspectores se
    /// ve exactamente como un Header (un título en negrita), así que un asset con el inspector por
    /// defecto no pierde sus secciones. Dentro de un desplegable no dibuja nada: el desplegable ya
    /// es el título.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class GroupAttribute : PropertyAttribute
    {
        public readonly string Title;

        public GroupAttribute(string title) => Title = title;
    }
}
