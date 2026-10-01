using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RedMagic.Enemies.EditorTools
{
    /// <summary>
    /// Dibuja las propiedades de un objeto serializado en <b>grupos plegables</b>, un grupo por cada
    /// <c>[Group]</c> de la clase (lo que va antes del primer Group es el grupo "General"). Nada
    /// desaparece: se baja el ruido visual dejando abiertos sólo los grupos que se tocan a diario.
    /// El estado abierto/cerrado se recuerda por tipo y grupo en <see cref="SessionState"/>.
    /// Compartido por el inspector de enemigos (<c>EnemyStats</c>) y el de jefes (<c>BossController</c>).
    /// </summary>
    public static class FoldoutInspector
    {
        private static readonly Dictionary<Type, Dictionary<string, string>> HeaderCache =
            new Dictionary<Type, Dictionary<string, string>>();

        public const string General = "General";

        /// <summary>True mientras se dibuja un desplegable: el título del grupo ya está, el decorador calla.</summary>
        public static bool Drawing { get; private set; }

        /// <summary>Campo → cabecera de su grupo (el último [Group] visto en el orden de declaración).</summary>
        private static Dictionary<string, string> HeadersOf(Type type)
        {
            if (HeaderCache.TryGetValue(type, out var map)) return map;

            map = new Dictionary<string, string>();
            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(object); t = t.BaseType)
                chain.Insert(0, t);

            string current = General;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var t in chain)
            {
                var fields = new List<FieldInfo>(t.GetFields(flags));
                fields.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
                foreach (var f in fields)
                {
                    var group = (GroupAttribute)Attribute.GetCustomAttribute(f, typeof(GroupAttribute), false);
                    if (group != null) current = group.Title;
                    map[f.Name] = current;
                }
            }

            HeaderCache[type] = map;
            return map;
        }

        /// <summary>
        /// Dibuja <paramref name="properties"/> (ya filtradas por quien llama) en grupos plegables.
        /// <paramref name="openByDefault"/> = cabeceras que arrancan abiertas.
        /// </summary>
        public static void Draw(Type owner, IEnumerable<SerializedProperty> properties, ICollection<string> openByDefault)
        {
            var map = HeadersOf(owner);
            var order = new List<string>();
            var groups = new Dictionary<string, List<SerializedProperty>>();

            foreach (var p in properties)
            {
                string header = map.TryGetValue(p.name, out var h) ? h : General;
                if (!groups.TryGetValue(header, out var list))
                {
                    groups[header] = list = new List<SerializedProperty>();
                    order.Add(header);
                }

                list.Add(p);
            }

            foreach (string header in order)
            {
                string key = $"FoldoutInspector.{owner.Name}.{header}";
                bool open = SessionState.GetBool(key, openByDefault != null && openByDefault.Contains(header));
                // Foldout normal (no 'header group'): un campo de lista lleva su propio desplegable dentro.
                bool now = EditorGUILayout.Foldout(open, $"{header}  ({groups[header].Count})", true, EditorStyles.foldoutHeader);
                if (now != open) SessionState.SetBool(key, now);

                if (!now) continue;

                Drawing = true;
                try
                {
                    EditorGUI.indentLevel++;
                    foreach (var p in groups[header]) EditorGUILayout.PropertyField(p, true);
                    EditorGUI.indentLevel--;
                }
                finally
                {
                    Drawing = false;
                }
            }
        }

        /// <summary>Las propiedades de primer nivel de <paramref name="so"/> sin <c>m_Script</c>.</summary>
        public static List<SerializedProperty> TopLevel(SerializedObject so, Func<string, bool> hidden = null)
        {
            var list = new List<SerializedProperty>();
            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script" || (hidden != null && hidden(it.name))) continue;
                list.Add(it.Copy());
            }

            return list;
        }
    }
}
