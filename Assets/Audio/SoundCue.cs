using System;
using UnityEngine;

namespace RedMagic.Audio
{
    /// <summary>Orden de robo de voces cuando están todas ocupadas: nunca se roba a una prioridad mayor.</summary>
    public enum SoundPriority
    {
        Low = 0,        // pasos, ambiente
        Normal = 1,     // golpes, disparos
        High = 2,       // rugidos de jefe, muertes
        Critical = 3    // UI: nunca se le roba la voz
    }

    /// <summary>
    /// Un sonido configurable en el Inspector: variantes, volumen, pitch aleatorio, prioridad y
    /// atenuación por distancia. Clase serializable normal (no ScriptableObject): se declara como
    /// campo en cualquier componente o asset y se reproduce con <see cref="AudioManager.Play(SoundCue)"/>.
    ///
    /// Los elementos de una lista que Unity crea con "+" llegan con todo a 0 (no ejecuta los
    /// inicializadores de C#); <see cref="initialized"/> lo detecta y en ese caso se usan los valores
    /// por defecto, y <c>SoundCueDrawer</c> los escribe en cuanto se ve en el Inspector.
    /// </summary>
    [Serializable]
    public class SoundCue
    {
        public const float DefaultRolloffStart = 8f;
        public const float DefaultRolloffEnd = 24f;

        [Tooltip("Variantes. Se elige una al azar en cada reproducción.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();

        [Range(0f, 1f)] public float volume = 1f;

        [Range(0.1f, 3f)] public float pitchMin = 1f;
        [Range(0.1f, 3f)] public float pitchMax = 1f;

        [Tooltip("No repetir la misma variante dos veces seguidas.")]
        public bool noRepeatLast = true;

        [Tooltip("Qué voces puede robar cuando están todas ocupadas (sólo iguales o menores).")]
        public SoundPriority priority = SoundPriority.Normal;

        [Tooltip("Atenuar y panear según la distancia al AudioListener. Para sonidos del mundo.")]
        public bool positional;

        [Tooltip("Distancia (unidades de mundo) hasta la que suena a volumen completo.")]
        [Min(0f)] public float rolloffStart = DefaultRolloffStart;

        [Tooltip("Distancia a la que ya no suena (ni ocupa voz).")]
        [Min(0f)] public float rolloffEnd = DefaultRolloffEnd;

        [SerializeField, HideInInspector] private bool initialized = true;

        [NonSerialized] private int _lastPickPlusOne;

        public bool IsInitialized => initialized;
        public bool HasClips => clips != null && clips.Length > 0 && AnyClip();

        public float Volume => initialized ? volume : 1f;
        public SoundPriority Priority => initialized ? priority : SoundPriority.Normal;
        public float RolloffStart => initialized ? rolloffStart : DefaultRolloffStart;
        public float RolloffEnd => initialized ? Mathf.Max(rolloffEnd, RolloffStart + 0.01f) : DefaultRolloffEnd;

        public float PickPitch()
        {
            if (!initialized) return 1f;
            float lo = Mathf.Clamp(Mathf.Min(pitchMin, pitchMax), 0.1f, 3f);
            float hi = Mathf.Clamp(Mathf.Max(pitchMin, pitchMax), 0.1f, 3f);
            return Mathf.Approximately(lo, hi) ? lo : UnityEngine.Random.Range(lo, hi);
        }

        /// <summary>Variante a reproducir (salta huecos vacíos y, si toca, la última usada). Null si no hay ninguna.</summary>
        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];

            int last = _lastPickPlusOne - 1;
            bool avoidLast = (noRepeatLast || !initialized) && last >= 0;

            // Pocas variantes: elegir entre las válidas sin reservar memoria.
            int candidates = 0;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && !(avoidLast && i == last)) candidates++;

            if (candidates == 0)
            {
                // Sólo queda la última (o nada): repetirla antes que callar.
                if (last >= 0 && last < clips.Length && clips[last] != null) return clips[last];
                return null;
            }

            int pick = UnityEngine.Random.Range(0, candidates);
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null || (avoidLast && i == last)) continue;
                if (pick-- == 0)
                {
                    _lastPickPlusOne = i + 1;
                    return clips[i];
                }
            }

            return null;
        }

        private bool AnyClip()
        {
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null) return true;
            return false;
        }
    }
}
