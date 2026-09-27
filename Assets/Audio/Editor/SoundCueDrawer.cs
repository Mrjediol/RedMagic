using RedMagic.Audio;
using UnityEditor;
using UnityEngine;

namespace RedMagic.AudioEditor
{
    /// <summary>
    /// <see cref="SoundCue"/> plegado en una línea ("3 clips · vol 0.8 · pitch 0.95-1.05"), con el
    /// rolloff sólo visible si es posicional. Si la cue llega con todo a 0 (elemento de lista creado
    /// con "+", Unity no ejecuta los inicializadores) le escribe los valores por defecto.
    /// </summary>
    [CustomPropertyDrawer(typeof(SoundCue))]
    public class SoundCueDrawer : PropertyDrawer
    {
        private const float Pad = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight + Pad;
            if (!property.isExpanded) return line;

            float h = line;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("clips"), true) + Pad;
            h += line * 5; // volume, pitchMin, pitchMax, noRepeatLast, priority
            h += line;     // positional
            if (property.FindPropertyRelative("positional").boolValue) h += line * 2;
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureInitialized(property);

            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            var row = new Rect(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded,
                new GUIContent(label.text + "   " + Summary(property), label.tooltip), true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                row.y += line + Pad;

                var clips = property.FindPropertyRelative("clips");
                row.height = EditorGUI.GetPropertyHeight(clips, true);
                EditorGUI.PropertyField(row, clips, true);
                row.y += row.height + Pad;
                row.height = line;

                Field(ref row, property, "volume");
                Field(ref row, property, "pitchMin");
                Field(ref row, property, "pitchMax");
                Field(ref row, property, "noRepeatLast");
                Field(ref row, property, "priority");
                Field(ref row, property, "positional");
                if (property.FindPropertyRelative("positional").boolValue)
                {
                    Field(ref row, property, "rolloffStart");
                    Field(ref row, property, "rolloffEnd");
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static void Field(ref Rect row, SerializedProperty parent, string name)
        {
            EditorGUI.PropertyField(row, parent.FindPropertyRelative(name));
            row.y += EditorGUIUtility.singleLineHeight + Pad;
        }

        private static string Summary(SerializedProperty p)
        {
            var clips = p.FindPropertyRelative("clips");
            int count = 0;
            for (int i = 0; i < clips.arraySize; i++)
                if (clips.GetArrayElementAtIndex(i).objectReferenceValue != null) count++;

            if (count == 0) return "(sin clips)";

            float lo = p.FindPropertyRelative("pitchMin").floatValue;
            float hi = p.FindPropertyRelative("pitchMax").floatValue;
            string pitch = Mathf.Approximately(lo, hi) ? $"pitch {lo:0.##}" : $"pitch {lo:0.##}-{hi:0.##}";
            string pos = p.FindPropertyRelative("positional").boolValue ? " · posicional" : "";
            return $"{count} clip{(count == 1 ? "" : "s")} · vol {p.FindPropertyRelative("volume").floatValue:0.##} · {pitch}{pos}";
        }

        private static void EnsureInitialized(SerializedProperty p)
        {
            var initialized = p.FindPropertyRelative("initialized");
            if (initialized == null || initialized.boolValue) return;

            p.FindPropertyRelative("volume").floatValue = 1f;
            p.FindPropertyRelative("pitchMin").floatValue = 1f;
            p.FindPropertyRelative("pitchMax").floatValue = 1f;
            p.FindPropertyRelative("noRepeatLast").boolValue = true;
            p.FindPropertyRelative("priority").enumValueIndex = (int)SoundPriority.Normal;
            p.FindPropertyRelative("rolloffStart").floatValue = SoundCue.DefaultRolloffStart;
            p.FindPropertyRelative("rolloffEnd").floatValue = SoundCue.DefaultRolloffEnd;
            initialized.boolValue = true;
            p.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
