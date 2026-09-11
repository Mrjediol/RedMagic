using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Core.EditorTools
{
    /// <summary>
    /// Dibujo de <see cref="SubclassPickerAttribute"/>: a la derecha de la cabecera, un desplegable con
    /// las subclases concretas del tipo del campo ("(ninguno)" vacía la entrada); debajo, los campos
    /// de la instancia elegida, como cualquier otro valor. Cambiar de tipo crea una instancia nueva
    /// con sus valores por defecto.
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassPickerAttribute))]
    public class SubclassPickerDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, (Type[] types, string[] names)> Cache = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUI.GetPropertyHeight(property, label, true);
            return property.managedReferenceValue == null
                ? EditorGUIUtility.singleLineHeight
                : EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var (types, names) = Options(BaseType());
            var current = property.managedReferenceValue?.GetType();
            int index = current == null ? 0 : Array.IndexOf(types, current) + 1;

            float line = EditorGUIUtility.singleLineHeight;
            var popupRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                                     position.width - EditorGUIUtility.labelWidth, line);

            // El desplegable va primero: en IMGUI el primer control que consume el clic gana, y la
            // cabecera plegable de debajo ocupa toda la fila.
            int chosen = EditorGUI.Popup(popupRect, index, names);
            if (chosen != index)
            {
                property.managedReferenceValue = chosen == 0 ? null : Activator.CreateInstance(types[chosen - 1]);
                property.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }

            if (property.managedReferenceValue == null)
            {
                EditorGUI.LabelField(new Rect(position.x, position.y, EditorGUIUtility.labelWidth, line), label);
                return;
            }

            EditorGUI.PropertyField(position, property, new GUIContent(label.text), true);
        }

        /// <summary>El tipo base: el del campo, o el de los elementos si es una lista/array.</summary>
        private Type BaseType()
        {
            var type = fieldInfo.FieldType;
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return type.GetGenericArguments()[0];
            return type;
        }

        private static (Type[] types, string[] names) Options(Type baseType)
        {
            if (Cache.TryGetValue(baseType, out var cached)) return cached;

            var types = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null &&
                            !typeof(UnityEngine.Object).IsAssignableFrom(t))
                .OrderBy(DisplayName)
                .ToArray();

            var names = new[] { "(ninguno)" }.Concat(types.Select(DisplayName)).ToArray();
            return Cache[baseType] = (types, names);
        }

        private static string DisplayName(Type type)
        {
            var attr = (DisplayNameAttribute)Attribute.GetCustomAttribute(type, typeof(DisplayNameAttribute));
            return attr != null ? attr.DisplayName : ObjectNames.NicifyVariableName(type.Name);
        }
    }
}
