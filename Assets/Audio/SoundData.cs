using System;
using UnityEngine;
using UnityEngine.Audio;

namespace RedMagic.Audio
{
    /// <summary>
    /// TEMPORAL (audio fase 4): entrada de la tabla de ids antigua del AudioManager. Ya no se
    /// reproduce nada por id; sólo existe para que la herramienta de migración lea los datos que
    /// hay en las escenas. Se borra en la fase 4.
    /// </summary>
    [Serializable]
    public class SoundData
    {
                public string id = "New Sound";

        [Tooltip("Clip de audio. Puede dejarse vacío como placeholder y asignarse más tarde.")]
        public AudioClip clip;

        [Range(0f, 1f)]
        [Tooltip("Volumen relativo de este sonido (se multiplica por el volumen del grupo en el mixer).")]
        public float volume = 1f;

        [Range(0.1f, 3f)]
        [Tooltip("Tono de reproducción. 1 = tono original.")]
        public float pitch = 1f;

        [Tooltip("Reproducir en bucle (relevante sobre todo para música).")]
        public bool loop;

        [Tooltip("Grupo del AudioMixer al que se enruta este sonido. Si se deja vacío, AudioManager " +
                 "usa el grupo Music o SFX según cómo se reproduzca.")]
        public AudioMixerGroup mixerGroup;
    }
}
