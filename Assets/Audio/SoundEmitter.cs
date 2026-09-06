using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Audio
{
    /// <summary>Cuándo se dispara un <see cref="SoundEvent"/>.</summary>
    public enum SoundTrigger
    {
        OnHit,
        OnDeath,
        OnSpawn,

        /// <summary>Nombre libre, definido en <see cref="SoundEvent.customEventName"/>.</summary>
        Custom
    }

    /// <summary>
    /// Una entrada de sonido del <see cref="SoundEmitter"/>: qué la dispara y qué clip suena.
    /// El clip es un AudioClip normal — se arrastra el .wav/.mp3 directamente, sin ScriptableObjects.
    /// </summary>
    [Serializable]
    public class SoundEvent
    {
        [Tooltip("Qué dispara este sonido.")]
        public SoundTrigger trigger = SoundTrigger.OnHit;

        [Tooltip("Nombre del evento cuando el trigger es Custom. Es el string que se pasa a Play().")]
        public string customEventName = "";

        [Tooltip("Arrastra aquí el .wav / .mp3.")]
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("Variar el tono en cada reproducción para que no suene repetitivo.")]
        public bool randomizePitch;

        [Tooltip("Pitch mínimo (1 = tono original).")]
        public float minPitch = 0.95f;

        [Tooltip("Pitch máximo (1 = tono original).")]
        public float maxPitch = 1.05f;

        /// <summary>Nombre por el que responde esta entrada a <see cref="SoundEmitter.Play(string)"/>.</summary>
        public string EventName =>
            trigger == SoundTrigger.Custom ? customEventName : trigger.ToString();

        public float ResolvePitch() =>
            randomizePitch ? UnityEngine.Random.Range(minPitch, maxPitch) : 1f;
    }

    /// <summary>
    /// Configuración de sonidos por objeto. Se pone en cualquier GameObject que emita sonido
    /// (enemigos, proyectiles, trampas, puertas…) y se dispara con <see cref="Play(string)"/>.
    ///
    /// SoundEmitter es sólo la capa de configuración/disparo: la reproducción real la sigue
    /// haciendo <see cref="AudioManager"/> a través de su mixer, así que el volumen y el mute
    /// globales de Master/SFX siguen aplicando igual.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RedMagic/Audio/Sound Emitter")]
    public class SoundEmitter : MonoBehaviour
    {
        [Tooltip("Sonidos de este objeto. Puede haber varias entradas con el mismo trigger: sonarán todas.")]
        [SerializeField] private List<SoundEvent> soundEvents = new List<SoundEvent>();

        [Tooltip("Disparar automáticamente las entradas OnSpawn al activarse el objeto " +
                 "(compatible con pooling: reactivar cuenta como spawn).")]
        [SerializeField] private bool playOnSpawnAutomatically = true;

        public IReadOnlyList<SoundEvent> SoundEvents => soundEvents;

        private void OnEnable()
        {
            if (playOnSpawnAutomatically) Play(SoundTrigger.OnSpawn);
        }

        /// <summary>Dispara todas las entradas cuyo trigger coincida (versión con enum, sin erratas).</summary>
        public void Play(SoundTrigger trigger)
        {
            if (trigger == SoundTrigger.Custom)
            {
                Debug.LogWarning("[SoundEmitter] Para triggers Custom usa Play(\"nombreDelEvento\").", this);
                return;
            }

            PlayMatching(trigger.ToString());
        }

        /// <summary>
        /// Dispara todas las entradas que coincidan con <paramref name="triggerType"/>.
        /// Acepta los nombres del enum ("OnHit", "OnDeath", "OnSpawn") o un nombre Custom.
        /// </summary>
        public void Play(string triggerType)
        {
            if (string.IsNullOrWhiteSpace(triggerType)) return;
            PlayMatching(triggerType);
        }

        /// <summary>True si hay alguna entrada configurada con ese trigger y un clip asignado.</summary>
        public bool Has(string triggerType)
        {
            foreach (var soundEvent in soundEvents)
                if (Matches(soundEvent, triggerType) && soundEvent.clip != null)
                    return true;

            return false;
        }

        private void PlayMatching(string triggerType)
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;

            foreach (var soundEvent in soundEvents)
            {
                if (soundEvent == null || soundEvent.clip == null) continue;
                if (!Matches(soundEvent, triggerType)) continue;

                audio.PlayClip(soundEvent.clip, soundEvent.volume, soundEvent.ResolvePitch());
            }
        }

        private static bool Matches(SoundEvent soundEvent, string triggerType) =>
            soundEvent != null &&
            string.Equals(soundEvent.EventName, triggerType, StringComparison.OrdinalIgnoreCase);
    }
}
