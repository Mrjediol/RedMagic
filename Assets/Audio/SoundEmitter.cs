using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedMagic.Audio
{
    /// <summary>
    /// Momentos que un objeto puede hacer sonar. Los valores son explícitos y no se renumeran: van
    /// serializados en cada <see cref="SoundEvent"/>. Quién los lanza está en la tabla de
    /// <see cref="SoundEmitter"/>.
    /// </summary>
    public enum SoundTrigger
    {
        OnSpawn = 0,     // SoundEmitter al activarse (también al salir de un pool)
        OnHit = 1,       // Health: recibe daño · Projectile: impacta
        OnDeath = 2,     // Health: muere · Projectile: vuelve al pool
        OnAttack = 3,    // EnemyAttack (frame de release) · PlayerAttack · RangedAttack
        OnJump = 4,      // PlayerMovement: salto desde el suelo
        OnAirJump = 5,   // PlayerMovement: doble salto
        OnDash = 6,      // PlayerMovement
        Footstep = 7,    // PlayerMovement: cada paso en suelo
        // 8 = OnStomp, retirado con la mecánica de pisotón: no reutilizar el valor.
        OnActivate = 9,  // BossController: empieza el combate (rugido)
        OnInteract = 10, // props del hub: abrir / usar
        OnLoot = 11      // props del hub: recoger el contenido
    }

    /// <summary>
    /// Lo implementa cualquier componente que tenga momentos sonoros (Health, PlayerMovement,
    /// EnemyAttack, BossController, Projectile, props…). <see cref="SoundEmitter"/> se suscribe a
    /// todos los que haya en su GameObject: el componente sólo avisa, no sabe si algo suena.
    /// </summary>
    public interface ISoundEventSource
    {
        event Action<SoundTrigger> SoundTriggered;
    }

    /// <summary>Una entrada del <see cref="SoundEmitter"/>: qué momento y qué suena.</summary>
    [Serializable]
    public class SoundEvent
    {
        public SoundTrigger trigger = SoundTrigger.OnHit;
        public SoundCue cue = new SoundCue();
    }

    /// <summary>
    /// Sonidos de un objeto (jugador, enemigo, jefe, proyectil, prop). Va en la raíz, junto a los
    /// componentes que lanzan los momentos: en OnEnable busca todos los <see cref="ISoundEventSource"/>
    /// de su GameObject y se suscribe. Añadir un sonido = añadir entrada, elegir momento, arrastrar
    /// clips. Varias entradas con el mismo momento suenan todas.
    ///
    /// Suena en la posición del objeto: con la cue en "positional", se atenúa con la distancia.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RedMagic/Audio/Sound Emitter")]
    public class SoundEmitter : MonoBehaviour
    {
        [Tooltip("Sonidos de este objeto. Varias entradas con el mismo momento suenan todas.")]
        [SerializeField] private List<SoundEvent> soundEvents = new List<SoundEvent>();

        private static readonly List<ISoundEventSource> s_sourceBuffer = new List<ISoundEventSource>();
        private readonly List<ISoundEventSource> _sources = new List<ISoundEventSource>();
        private Action<SoundTrigger> _handler;

        // OnSpawn no suena en OnEnable sino al final del frame (AudioManager.LateUpdate →
        // FlushPendingSpawns), y sólo si el objeto sigue activo. PrefabPool instancia el prefab
        // activo y lo apaga en el mismo frame antes de entregarlo: sonar en OnEnable daba dos
        // OnSpawn en el primer uso de cada instancia. Además así el spawner ya lo ha colocado.
        private static readonly List<SoundEmitter> s_pendingSpawns = new List<SoundEmitter>();
        private bool _spawnQueued;

        public IReadOnlyList<SoundEvent> SoundEvents => soundEvents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => s_pendingSpawns.Clear();

        private void OnEnable()
        {
            _handler ??= Play;

            GetComponents(s_sourceBuffer);
            for (int i = 0; i < s_sourceBuffer.Count; i++)
            {
                s_sourceBuffer[i].SoundTriggered += _handler;
                _sources.Add(s_sourceBuffer[i]);
            }
            s_sourceBuffer.Clear();

            if (!_spawnQueued && AudioManager.Instance != null && Has(SoundTrigger.OnSpawn))
            {
                _spawnQueued = true;
                s_pendingSpawns.Add(this);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _sources.Count; i++)
                if (_sources[i] != null) _sources[i].SoundTriggered -= _handler;
            _sources.Clear();
        }

        internal static void FlushPendingSpawns()
        {
            for (int i = 0; i < s_pendingSpawns.Count; i++)
            {
                var emitter = s_pendingSpawns[i];
                if (emitter == null) continue;
                emitter._spawnQueued = false;
                if (emitter.isActiveAndEnabled) emitter.Play(SoundTrigger.OnSpawn);
            }
            s_pendingSpawns.Clear();
        }

        /// <summary>Hace sonar todas las entradas de ese momento en la posición del objeto.</summary>
        public void Play(SoundTrigger trigger)
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;

            Vector3 position = transform.position;
            for (int i = 0; i < soundEvents.Count; i++)
            {
                var e = soundEvents[i];
                if (e != null && e.trigger == trigger) audio.Play(e.cue, position);
            }
        }

        /// <summary>True si hay alguna entrada de ese momento con clips.</summary>
        public bool Has(SoundTrigger trigger)
        {
            for (int i = 0; i < soundEvents.Count; i++)
            {
                var e = soundEvents[i];
                if (e != null && e.trigger == trigger && e.cue != null && e.cue.HasClips) return true;
            }
            return false;
        }
    }
}
