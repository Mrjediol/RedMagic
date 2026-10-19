using System;
using UnityEngine;

namespace RedMagic.Core
{
    /// <summary>
    /// Para un campo <c>string</c> que guarda el GUID de un asset que <b>sólo usa el editor</b> (crear escenas, exportar a la
    /// web…). El Inspector lo dibuja como un campo de objeto normal, pero al guardar sólo un texto, el juego no carga ese asset
    /// ni sus texturas al cargar lo que lo referencia (una referencia directa arrastraría todo a la memoria y a la build).
    /// Leerlo desde código de editor: <c>AssetDatabase.LoadAssetAtPath&lt;T&gt;(AssetDatabase.GUIDToAssetPath(guid))</c>.
    /// </summary>
    public class EditorOnlyAssetAttribute : PropertyAttribute
    {
        public readonly Type Type;

        public EditorOnlyAssetAttribute(Type type) => Type = type;
    }
}
