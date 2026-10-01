using UnityEditor;
using UnityEngine;

namespace RedMagic.Enemies.EditorTools
{
    /// <summary>
    /// Dibuja <see cref="GroupAttribute"/> como un Header normal, salvo dentro de
    /// <see cref="FoldoutInspector"/>, donde el desplegable ya hace de título.
    /// </summary>
    [CustomPropertyDrawer(typeof(GroupAttribute))]
    public class GroupDecorator : DecoratorDrawer
    {
        public override float GetHeight() => FoldoutInspector.Drawing ? 0f : EditorGUIUtility.singleLineHeight + 8f;

        public override void OnGUI(Rect position)
        {
            if (FoldoutInspector.Drawing) return;

            position.yMin += 8f;
            GUI.Label(position, ((GroupAttribute)attribute).Title, EditorStyles.boldLabel);
        }
    }
}
