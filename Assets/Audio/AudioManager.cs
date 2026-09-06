using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace RedMagic.Audio
{
    /// <summary>
    /// Gestor de audio global. Singleton persistente (DontDestroyOnLoad).
    /// Enruta música y SFX a través de un AudioMixer con los grupos Master / Music / SFX
    /// y persiste volumen y mute por grupo en PlayerPrefs.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        // Identificadores de grupo (coinciden con los parámetros expuestos del mixer, ver ParamFor).
        public const string Master = "Master";
        public const string Music = "Music";
        public const string Sfx = "SFX";

        [Header("Mixer")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("Biblioteca de sonidos")]
        [Tooltip("Assets SoundData disponibles para PlaySFX / PlayMusic (se buscan por su campo id).")]
        [SerializeField] private List<SoundData> sounds = new List<SoundData>();

        [Header("Placeholders — asigna los AudioClip en el Inspector")]
        [Tooltip("id: \"Music_Menu\"")]
        [SerializeField] private AudioClip Music_Menu;
        [Tooltip("id: \"SFX_ButtonHover\"")]
        [SerializeField] private AudioClip SFX_ButtonHover;
        [Tooltip("id: \"SFX_ButtonClick\"")]
        [SerializeField] private AudioClip SFX_ButtonClick;

        [Header("Valores por defecto (usados si no hay nada guardado en PlayerPrefs)")]
        [Range(0f, 1f)] [SerializeField] private float defaultMasterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float defaultMusicVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float defaultSfxVolume = 1f;

        private readonly Dictionary<string, SoundData> _library = new Dictionary<string, SoundData>();
        private readonly Dictionary<string, AudioClip> _placeholderClips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _volume = new Dictionary<string, float>();
        private readonly Dictionary<string, bool> _muted = new Dictionary<string, bool>();

        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private string _currentMusicId;
        private Coroutine _musicRoutine;

        private const string PrefVolume = "audio.volume.";
        private const string PrefMuted = "audio.muted.";
        private const float MinLinear = 0.0001f; // ~ -80 dB

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            BuildLibrary();
            SetupSources();
            ReadSettings();
        }

        private void Start()
        {
            // Empujar los valores al mixer un frame después de Awake: SetFloot sobre parámetros
            // expuestos puede ignorarse silenciosamente si se llama demasiado pronto tras la carga.
            ApplyAllToMixer();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ setup

        private void BuildLibrary()
        {
            _library.Clear();
            _placeholderClips.Clear();

            foreach (var sound in sounds)
            {
                if (sound == null || string.IsNullOrEmpty(sound.id)) continue;
                _library[sound.id] = sound;
            }

            RegisterPlaceholder("Music_Menu", Music_Menu, musicGroup, loop: true);
            RegisterPlaceholder("SFX_ButtonHover", SFX_ButtonHover, sfxGroup, loop: false);
            RegisterPlaceholder("SFX_ButtonClick", SFX_ButtonClick, sfxGroup, loop: false);
        }

        private void RegisterPlaceholder(string id, AudioClip clip, AudioMixerGroup group, bool loop)
        {
            // El clip del campo del Inspector se usa como fallback en tiempo de ejecución
            // (nunca se escribe en el SoundData asset, para no ensuciarlo).
            if (clip != null) _placeholderClips[id] = clip;

            if (_library.ContainsKey(id)) return; // ya hay un SoundData real con ese id

            var data = ScriptableObject.CreateInstance<SoundData>();
            data.id = id;
            data.clip = clip;
            data.volume = 1f;
            data.loop = loop;
            data.mixerGroup = group;
            data.hideFlags = HideFlags.HideAndDontSave;
            _library[id] = data;
        }

        // Clip efectivo: el del SoundData, o el placeholder del Inspector si el SoundData no tiene ninguno.
        private AudioClip ClipFor(SoundData data)
        {
            if (data.clip != null) return data.clip;
            return _placeholderClips.TryGetValue(data.id, out var c) ? c : null;
        }

        private void SetupSources()
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.outputAudioMixerGroup = musicGroup;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
            _sfxSource.outputAudioMixerGroup = sfxGroup;
        }

        // ------------------------------------------------------------------ playback

        public void PlayMusic(string id)
        {
            if (!TryGet(id, out var data)) return;
            if (_currentMusicId == id && _musicSource.isPlaying) return;

            _currentMusicId = id;

            if (_musicRoutine != null) StopCoroutine(_musicRoutine);
            _musicRoutine = StartCoroutine(PlayMusicRoutine(data));
        }

        // El clip puede tener "Preload Audio Data" desactivado: en ese caso Play() se llamaría
        // sobre un clip aún sin cargar y a veces no suena. Forzamos la carga y esperamos.
        // Con Time.timeScale = 0 esta corrutina sigue avanzando (yield return null no usa tiempo escalado).
        private IEnumerator PlayMusicRoutine(SoundData data)
        {
            var clip = ClipFor(data);

            _musicSource.Stop();
            _musicSource.clip = clip;
            _musicSource.loop = data.loop;
            _musicSource.volume = data.volume;
            _musicSource.outputAudioMixerGroup = data.mixerGroup != null ? data.mixerGroup : musicGroup;

            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] '{data.id}' no tiene AudioClip asignado.", this);
                _musicRoutine = null;
                yield break;
            }

            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();

            while (clip.loadState == AudioDataLoadState.Loading)
                yield return null;

            if (clip.loadState == AudioDataLoadState.Loaded)
                _musicSource.Play();
            else
                Debug.LogWarning($"[AudioManager] No se pudo cargar el clip de música '{data.id}'.", this);

            _musicRoutine = null;
        }

        public void StopMusic()
        {
            if (_musicRoutine != null)
            {
                StopCoroutine(_musicRoutine);
                _musicRoutine = null;
            }

            _musicSource.Stop();
            _musicSource.clip = null;
            _currentMusicId = null;
        }

        public void PlaySFX(string id)
        {
            if (!TryGet(id, out var data)) return;

            var clip = ClipFor(data);
            if (clip == null) return;

            var group = data.mixerGroup != null ? data.mixerGroup : sfxGroup;
            if (_sfxSource.outputAudioMixerGroup != group) _sfxSource.outputAudioMixerGroup = group;

            _sfxSource.PlayOneShot(clip, data.volume);
        }

        private bool TryGet(string id, out SoundData data)
        {
            if (!string.IsNullOrEmpty(id) && _library.TryGetValue(id, out data)) return true;

            Debug.LogWarning($"[AudioManager] Sonido no registrado: '{id}'.", this);
            data = null;
            return false;
        }

        // ------------------------------------------------------------------ mixer / settings

        public float GetVolume(string group) => _volume.TryGetValue(group, out var v) ? v : 1f;

        public bool IsMuted(string group) => _muted.TryGetValue(group, out var m) && m;

        public void SetVolume(string group, float value)
        {
            value = Mathf.Clamp01(value);
            _volume[group] = value;
            ApplyToMixer(group);

            PlayerPrefs.SetFloat(PrefVolume + group, value);
            PlayerPrefs.Save();
        }

        public void ToggleMute(string group) => SetMute(group, !IsMuted(group));

        public void SetMute(string group, bool muted)
        {
            _muted[group] = muted;
            ApplyToMixer(group);

            PlayerPrefs.SetInt(PrefMuted + group, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplyAllToMixer()
        {
            ApplyToMixer(Master);
            ApplyToMixer(Music);
            ApplyToMixer(Sfx);
        }

        private void ApplyToMixer(string group)
        {
            if (mixer == null) return;

            var param = ParamFor(group);
            if (param == null) return;

            float linear = IsMuted(group) ? MinLinear : Mathf.Max(GetVolume(group), MinLinear);
            mixer.SetFloat(param, Mathf.Log10(linear) * 20f);
        }

        private static string ParamFor(string group)
        {
            switch (group)
            {
                case Master: return "MasterVolume";
                case Music: return "MusicVolume";
                case Sfx: return "SFXVolume";
                default: return null;
            }
        }

        private void ReadSettings()
        {
            ReadGroup(Master, defaultMasterVolume);
            ReadGroup(Music, defaultMusicVolume);
            ReadGroup(Sfx, defaultSfxVolume);
        }

        private void ReadGroup(string group, float fallbackVolume)
        {
            _volume[group] = PlayerPrefs.GetFloat(PrefVolume + group, fallbackVolume);
            _muted[group] = PlayerPrefs.GetInt(PrefMuted + group, 0) == 1;
        }
    }
}
