using System.Collections.Generic;
using RedMagic.Rigging;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Enemies.EditorTools
{
    /// <summary>
    /// Dibuja un <see cref="GestureNameAttribute"/> como una lista con los gestos del rig del objeto
    /// inspeccionado: "(ninguno)" + cada gesto. Un nombre que ya no existe se conserva y se marca,
    /// para que no se pierda en silencio al renombrar un gesto.
    /// </summary>
    [CustomPropertyDrawer(typeof(GestureNameAttribute))]
    public class GestureNameDrawer : PropertyDrawer
    {
        private const string None = "(ninguno)";
        private const string Missing = " (no existe)";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var rig = property.serializedObject.targetObject is Component c ? c.GetComponent<ProceduralRig>() : null;
            if (property.propertyType != SerializedPropertyType.String || rig == null || rig.gestures == null)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            string current = property.stringValue ?? string.Empty;
            var options = new List<string> { None };
            foreach (var g in rig.gestures)
                if (g != null && !string.IsNullOrWhiteSpace(g.name) && !options.Contains(g.name)) options.Add(g.name);

            int index = string.IsNullOrEmpty(current) ? 0 : options.IndexOf(current);
            if (index < 0) { options.Add(current + Missing); index = options.Count - 1; }

            EditorGUI.BeginProperty(position, label, property);
            int chosen = EditorGUI.Popup(position, label.text, index, options.ToArray());
            if (chosen != index)
                property.stringValue = chosen == 0 ? string.Empty : options[chosen].Replace(Missing, string.Empty);
            EditorGUI.EndProperty();
        }
    }
}
