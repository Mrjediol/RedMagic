using RedMagic.Audio;
using UnityEditor;
using UnityEngine;

namespace RedMagic.AudioEditor
{
    /// <summary>
    /// Dibuja cada entrada de <see cref="SoundEvent"/> plegada, con el trigger y el clip en el
    /// título, y ocultando los campos que no aplican (nombre custom, rango de pitch).
    /// </summary>
    [CustomPropertyDrawer(typeof(SoundEvent))]
    public class SoundEventDrawer : PropertyDrawer
    {
        private const float Pad = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight + Pad;
            if (!property.isExpanded) return line;

            var trigger = property.FindPropertyRelative("trigger");
            var randomize = property.FindPropertyRelative("randomizePitch");

            // plegado + trigger + clip + volumen + randomizePitch
            int lines = 5;
            if (trigger.enumValueIndex == (int)SoundTrigger.Custom) lines += 1;
            if (randomize.boolValue) lines += 2;

            return lines * line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var trigger = property.FindPropertyRelative("trigger");
            var custom = property.FindPropertyRelative("customEventName");
            var clip = property.FindPropertyRelative("clip");
            var volume = property.FindPropertyRelative("volume");
            var randomize = property.FindPropertyRelative("randomizePitch");
            var minPitch = property.FindPropertyRelative("minPitch");
            var maxPitch = property.FindPropertyRelative("maxPitch");

            float line = EditorGUIUtility.singleLineHeight;
            float step = line + Pad;
            var row = new Rect(position.x, position.y, position.width, line);

            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, BuildTitle(trigger, custom, clip), true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;

            row.y += step;
            EditorGUI.PropertyField(row, trigger, new GUIContent("Trigger"));

            if (trigger.enumValueIndex == (int)SoundTrigger.Custom)
            {
                row.y += step;
                EditorGUI.PropertyField(row, custom, new GUIContent("Event Name"));
            }

            row.y += step;
            EditorGUI.PropertyField(row, clip, new GUIContent("Audio Clip"));

            row.y += step;
            EditorGUI.PropertyField(row, volume, new GUIContent("Volume"));

            row.y += step;
            EditorGUI.PropertyField(row, randomize, new GUIContent("Random Pitch"));

            if (randomize.boolValue)
            {
                row.y += step;
                EditorGUI.PropertyField(row, minPitch, new GUIContent("Pitch Min"));
                row.y += step;
                EditorGUI.PropertyField(row, maxPitch, new GUIContent("Pitch Max"));
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private static string BuildTitle(SerializedProperty trigger, SerializedProperty custom, SerializedProperty clip)
        {
            string name = trigger.enumValueIndex == (int)SoundTrigger.Custom
                ? (string.IsNullOrWhiteSpace(custom.stringValue) ? "Custom (sin nombre)" : "Custom: " + custom.stringValue)
                : ((SoundTrigger)trigger.enumValueIndex).ToString();

            var clipObject = clip.objectReferenceValue;
            return name + "  —  " + (clipObject != null ? clipObject.name : "(sin clip)");
        }
    }
}
