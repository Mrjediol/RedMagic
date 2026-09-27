using System;
using System.Reflection;
using UnityEditor;

namespace RedMagic.Audio.EditorTools
{
    /// <summary>
    /// Evalúa <see cref="SoundSlotIfAttribute"/>: si un campo <see cref="SoundCue"/> existe como hueco
    /// de sonido para el objeto concreto que lo lleva. Lo comparten el Inspector (oculta el hueco) y el
    /// escáner del registro (no lo lista), así nunca discrepan.
    /// </summary>
    public static class SoundSlotRules
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>True si el hueco en <paramref name="cue"/> aplica. Sin atributo, siempre.</summary>
        public static bool IsActive(SerializedProperty cue, FieldInfo field)
        {
            var rule = field?.GetCustomAttribute<SoundSlotIfAttribute>();
            if (rule == null) return true;

            object owner = OwnerOf(cue);
            return owner == null || Evaluate(owner, rule.Member);
        }

        /// <summary>El campo C# del que sale <paramref name="cue"/> (último tramo de su ruta).</summary>
        public static FieldInfo FieldOf(SerializedProperty cue)
        {
            object owner = OwnerOf(cue);
            if (owner == null) return null;
            string name = cue.name;
            for (var t = owner.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        // Objeto que declara el campo: el asset/componente, o el valor del elemento padre (una fase…).
        private static object OwnerOf(SerializedProperty cue)
        {
            string path = cue.propertyPath;
            int dot = path.LastIndexOf('.');
            if (dot < 0) return cue.serializedObject.targetObject;

            var parent = cue.serializedObject.FindProperty(path.Substring(0, dot));
            if (parent == null) return null;
            try { return parent.boxedValue; }
            catch (Exception) { return null; }
        }

        private static bool Evaluate(object owner, string member)
        {
            for (var t = owner.GetType(); t != null; t = t.BaseType)
            {
                var p = t.GetProperty(member, Any | BindingFlags.DeclaredOnly);
                if (p != null && p.PropertyType == typeof(bool)) return (bool)p.GetValue(owner);

                var f = t.GetField(member, Any | BindingFlags.DeclaredOnly);
                if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(owner);

                var m = t.GetMethod(member, Any | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(bool)) return (bool)m.Invoke(owner, null);
            }
            return true;   // miembro mal escrito: mejor enseñar el hueco que esconderlo sin avisar
        }
    }
}
