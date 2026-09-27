using System;
using System.Collections.Generic;
using UnityEditor;

namespace RedMagic.Audio.EditorTools
{
    /// <summary>
    /// Avisa de que el registro de sonidos puede estar desactualizado: cualquier cambio en un asset que
    /// puede tener huecos (prefab, ScriptableObject, escena, script) o en la música de
    /// <c>Resources/Music</c> lo marca, y la pestaña Sounds enseña el aviso con el botón de regenerar.
    /// Escanear en cada cambio sería lento (abre escenas); esto sólo cuenta.
    ///
    /// Lo que escribe la propia pestaña (asignar un clip, quitar un sonido) no cuenta: ya deja el
    /// registro al día en el momento (<see cref="IgnoreNext"/>).
    /// </summary>
    public sealed class SoundRegistryWatcher : AssetPostprocessor
    {
        private const string Key = "RedMagic.SoundRegistry.StaleCount";
        private static readonly HashSet<string> s_ignored = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Cambios pendientes de regenerar (0 = registro al día).</summary>
        public static int StaleCount => EditorPrefs.GetInt(Key, 0);

        public static void MarkFresh() => EditorPrefs.SetInt(Key, 0);

        /// <summary>El próximo cambio de <paramref name="assetPath"/> lo ha hecho la pestaña: no cuenta.</summary>
        public static void IgnoreNext(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath)) s_ignored.Add(assetPath);
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            int count = 0;
            foreach (var path in imported) if (Relevant(path)) count++;
            foreach (var path in deleted) if (Relevant(path)) count++;
            foreach (var path in moved) if (Relevant(path)) count++;
            if (count > 0) EditorPrefs.SetInt(Key, StaleCount + count);
        }

        private static bool Relevant(string path)
        {
            if (s_ignored.Remove(path)) return false;
            if (path.StartsWith(SoundRegistry.Folder + "/", StringComparison.Ordinal)) return false;
            if (path.StartsWith("Assets/Brackeys/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Cainos/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Dragon Warrior Files/", StringComparison.Ordinal)) return false;

            if (path.StartsWith(SoundRegistry.MusicResourceFolder + "/", StringComparison.Ordinal)) return true;
            return path.EndsWith(".prefab", StringComparison.Ordinal) || path.EndsWith(".asset", StringComparison.Ordinal) ||
                   path.EndsWith(".unity", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal);
        }
    }
}
