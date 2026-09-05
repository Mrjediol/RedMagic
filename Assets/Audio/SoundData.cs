using UnityEngine;
using UnityEngine.Audio;

namespace RedMagic.Audio
{
    /// <summary>
    /// Definición de un sonido reutilizable. AudioManager lo localiza por <see cref="id"/>.
    /// Editable en el Inspector: volumen, loop y grupo de mezcla por sonido.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundData", menuName = "RedMagic/Audio/Sound Data")]
    public class SoundData : ScriptableObject
    {
        [Tooltip("Identificador usado por AudioManager.PlaySFX(id) / PlayMusic(id). Debe ser único.")]
        public string id = "New Sound";

        [Tooltip("Clip de audio. Puede dejarse vacío como placeholder y asignarse más tarde.")]
        public AudioClip clip;

        [Range(0f, 1f)]
        [Tooltip("Volumen relativo de este sonido (se multiplica por el volumen del grupo en el mixer).")]
        public float volume = 1f;

        [Tooltip("Reproducir en bucle (relevante sobre todo para música).")]
        public bool loop;

        [Tooltip("Grupo del AudioMixer al que se enruta este sonido. Normalmente Music o SFX.")]
        public AudioMixerGroup mixerGroup;
    }
}
