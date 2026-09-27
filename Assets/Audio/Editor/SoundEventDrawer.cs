using RedMagic.Audio;
using UnityEditor;
using UnityEngine;

namespace RedMagic.AudioEditor
{
    /// <summary>
    /// Una entrada del <see cref="SoundEmitter"/> en dos filas: el momento, y debajo su
    /// <see cref="SoundCue"/> (con su propio plegado, ver <see cref="SoundCueDrawer"/>).
    /// </summary>
    [CustomPropertyDrawer(typeof(SoundEvent))]
    public class SoundEventDrawer : PropertyDrawer
    {
        private const float Pad = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + Pad +
                   EditorGUI.GetPropertyHeight(property.FindPropertyRelative("cue"), true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var trigger = property.FindPropertyRelative("trigger");
            var cue = property.FindPropertyRelative("cue");

            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(row, trigger, new GUIContent("Momento"));

            row.y += row.height + Pad;
            row.height = EditorGUI.GetPropertyHeight(cue, true);
            EditorGUI.PropertyField(row, cue, new GUIContent("Sonido"), true);

            EditorGUI.EndProperty();
        }
    }
}
