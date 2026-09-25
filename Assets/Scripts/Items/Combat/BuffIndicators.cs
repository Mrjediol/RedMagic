using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Items
{
    /// <summary>
    /// Chapas del HUD de efectos de item (contador del Yelmo, "×3" del Bastón…). Un efecto pone la
    /// suya con su propia clave y la quita cuando ya no aplica; <c>UI.ItemBuffHud</c> las pinta todas.
    /// Así un efecto nuevo que necesita indicador sólo llama a <see cref="Set"/>, sin tocar la UI.
    /// </summary>
    public static class BuffIndicators
    {
        public sealed class Indicator
        {
            public Sprite Icon;
            public string Text;
            /// <summary>Encendido: la chapa brilla (listo, activo).</summary>
            public bool Highlight;
            public Color Accent = new Color(0.55f, 0.85f, 0.95f);
            /// <summary>Orden de izquierda a derecha.</summary>
            public int Order;
        }

        private static readonly Dictionary<object, Indicator> Entries = new();

        /// <summary>Sube cada vez que se añade o se quita una chapa (la UI reconstruye sólo entonces).</summary>
        public static int Version { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Entries.Clear();
            Version = 0;
        }

        public static IReadOnlyDictionary<object, Indicator> All => Entries;

        /// <summary>Pone o actualiza la chapa de <paramref name="source"/>.</summary>
        public static void Set(object source, Sprite icon, string text, bool highlight, Color accent, int order = 0)
        {
            if (!Entries.TryGetValue(source, out var entry))
            {
                entry = new Indicator();
                Entries[source] = entry;
                Version++;
            }

            entry.Icon = icon;
            entry.Text = text;
            entry.Highlight = highlight;
            entry.Accent = accent;
            entry.Order = order;
        }

        public static void Remove(object source)
        {
            if (Entries.Remove(source)) Version++;
        }
    }
}
