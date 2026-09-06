using System;
using UnityEngine;
using UnityEngine.Audio;

namespace RedMagic.Audio
{
    /// <summary>
    /// Una entrada de sonido de la lista del <see cref="AudioManager"/>.
    ///
    /// Es una clase serializable normal (no un ScriptableObject): se edita en línea en el
    /// Inspector del AudioManager, igual que el viejo array de "Sounds". Para añadir un sonido
    /// nuevo basta con crecer la lista y rellenar estos campos — no hay assets que crear.
    ///
    /// <see cref="AudioManager"/> localiza cada entrada por su <see cref="id"/>, que es el string
    /// que se pasa a <c>PlaySFX(id)</c> / <c>PlayMusic(id)</c>.
    /// </summary>
    [Serializable]
    public class SoundData
    {
        [Tooltip("Identificador único. Es el string que se pasa a AudioManager.PlaySFX(id) / PlayMusic(id).")]
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
