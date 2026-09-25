using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// Declara en un tipo de ataque una <b>caja</b> relativa al jefe (centro + tamaño, X hacia el
    /// jugador) que el editor dibuja en la escena y deja mover y redimensionar con el ratón.
    /// Las rutas son nombres de campo serializado (se admite "a.b" para campos anidados).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class AttackBoxAttribute : Attribute
    {
        public readonly string OffsetPath;
        public readonly string SizePath;
        public readonly string Label;

        public AttackBoxAttribute(string offsetPath, string sizePath, string label)
        {
            OffsetPath = offsetPath;
            SizePath = sizePath;
            Label = label;
        }
    }

    /// <summary>
    /// Declara un <b>punto</b> relativo al jefe (X hacia el jugador): de dónde sale un proyectil,
    /// dónde sujeta algo. El editor lo dibuja y deja arrastrarlo.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class AttackPointAttribute : Attribute
    {
        public readonly string OffsetPath;
        public readonly string Label;

        public AttackPointAttribute(string offsetPath, string label)
        {
            OffsetPath = offsetPath;
            Label = label;
        }
    }

    /// <summary>
    /// Campos que este tipo de ataque <b>no usa</b> (p. ej. la velocidad de un proyectil que el
    /// ataque calcula solo). El inspector de ataques no los dibuja, para que no parezca que sirven.
    /// Rutas de campo serializado, "a.b" para anidados.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class AttackHidesAttribute : Attribute
    {
        public readonly string[] Paths;

        public AttackHidesAttribute(params string[] paths) => Paths = paths ?? Array.Empty<string>();
    }

    /// <summary>
    /// Registro de las áreas de daño que los ataques acaban de usar, para verlas en Play
    /// (<see cref="BossController"/> ▸ "Ver hitboxes en Play"). Sólo en el editor; en un build no
    /// guarda nada.
    /// </summary>
    public static class BossHitboxDebug
    {
        public struct Entry
        {
            public Vector2 Center;
            public Vector2 Size;
            public float Radius;
            public Color Color;
            public float Until;
        }

        private static readonly List<Entry> Entries = new List<Entry>();

        public static IReadOnlyList<Entry> Current => Entries;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Entries.Clear();

        public static void Box(Vector2 center, Vector2 size, Color color, float seconds = 0.35f)
        {
            if (!Application.isEditor) return;
            Entries.Add(new Entry { Center = center, Size = size, Color = color, Until = Time.time + seconds });
        }

        public static void Circle(Vector2 center, float radius, Color color, float seconds = 0.35f)
        {
            if (!Application.isEditor) return;
            Entries.Add(new Entry { Center = center, Radius = radius, Color = color, Until = Time.time + seconds });
        }

        /// <summary>Quita las caducadas. Lo llama quien las dibuja.</summary>
        public static void Prune()
        {
            float now = Time.time;
            Entries.RemoveAll(e => e.Until < now);
        }
    }
}
