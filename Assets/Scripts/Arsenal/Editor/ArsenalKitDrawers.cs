using UnityEditor;
using UnityEngine;

namespace RedMagic.Arsenal.Editor
{
    /// <summary>Utilidades: el kit que contiene una propiedad (el objeto de su [SerializeReference] padre).</summary>
    internal static class KitOf
    {
        public static ArsenalKit Find(SerializedProperty property)
        {
            string path = property.propertyPath;
            int dot = path.LastIndexOf('.');
            if (dot < 0) return null;
            var parent = property.serializedObject.FindProperty(path.Substring(0, dot));
            return parent != null && parent.propertyType == SerializedPropertyType.ManagedReference ? parent.managedReferenceValue as ArsenalKit : null;
        }
    }

    /// <summary>Evolución: desplegable con los nombres del árbol del kit (Base, Erizo, Lanza…).</summary>
    [CustomPropertyDrawer(typeof(ArsenalEvolutionAttribute))]
    public sealed class ArsenalEvolutionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var kit = KitOf.Find(property);
            if (kit == null || property.propertyType != SerializedPropertyType.Integer) { EditorGUI.PropertyField(position, property, label); return; }
            var evos = kit.Tree.evolutions;
            var names = new GUIContent[evos.Count];
            for (int i = 0; i < evos.Count; i++) names[i] = new GUIContent(evos[i].name, evos[i].desc);
            EditorGUI.BeginProperty(position, label, property);
            int v = EditorGUI.Popup(position, new GUIContent("Evolución", label.tooltip), Mathf.Clamp(property.intValue, 0, evos.Count - 1), names);
            if (v != property.intValue) property.intValue = v;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>Mejora: la casilla lleva el nombre de la mejora de la evolución elegida (gris sin evolución).</summary>
    [CustomPropertyDrawer(typeof(ArsenalUpgradeAttribute))]
    public sealed class ArsenalUpgradeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var kit = KitOf.Find(property);
            int idx = ((ArsenalUpgradeAttribute)attribute).index;
            if (kit == null || property.propertyType != SerializedPropertyType.Boolean) { EditorGUI.PropertyField(position, property, label); return; }
            var evos = kit.Tree.evolutions;
            int e = Mathf.Clamp(kit.evolution, 0, evos.Count - 1);
            string name = e > 0 ? $"Mejora {idx + 1}: {evos[e].ups[idx]}" : $"Mejora {idx + 1} (elige evolución)";
            using (new EditorGUI.DisabledScope(e == 0))
            {
                EditorGUI.BeginProperty(position, label, property);
                property.boolValue = EditorGUI.Toggle(position, new GUIContent(name, e > 0 ? evos[e].upDescs[idx] : label.tooltip), property.boolValue);
                EditorGUI.EndProperty();
            }
        }
    }
}
