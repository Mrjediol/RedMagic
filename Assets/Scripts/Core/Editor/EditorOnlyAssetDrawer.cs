using UnityEditor;
using UnityEngine;

namespace RedMagic.Core.EditorTools
{
    /// <summary>
    /// Dibujo de <see cref="EditorOnlyAssetAttribute"/>: un campo de objeto del tipo pedido; arrastrar un asset guarda su
    /// GUID en el string, vaciarlo lo borra. Un GUID que ya no existe se ve como campo vacío con aviso.
    /// </summary>
    [CustomPropertyDrawer(typeof(EditorOnlyAssetAttribute))]
    public class EditorOnlyAssetDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var type = ((EditorOnlyAssetAttribute)attribute).Type;
            string guid = property.stringValue;
            string path = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
            var current = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, type);
            if (current == null && !string.IsNullOrEmpty(guid))
                label = new GUIContent(label.text + " (falta)", "El asset guardado ya no existe: " + guid);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUI.ObjectField(position, label, current, type, false);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = picked == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(picked));
            EditorGUI.EndProperty();
        }
    }
}
