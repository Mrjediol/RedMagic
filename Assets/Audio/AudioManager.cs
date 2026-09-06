using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace RedMagic.Audio
{
    /// <summary>
    /// Gestor de audio global. Singleton persistente (DontDestroyOnLoad).
    ///
    /// Los sonidos se dan de alta en una única lista en el Inspector (<see cref="sounds"/>): cada
    /// entrada es un <see cref="SoundData"/> con id, clip, volumen, pitch, loop y grupo de mezcla.
    /// Para añadir un sonido nuevo se hace crecer la lista y se rellenan los campos — igual que el
    /// viejo array de "Sounds", sin ScriptableObjects.
    ///
    /// Internamente sigue enrutando música y SFX por un AudioMixer con los grupos Master / Music /
    /// SFX y persiste volumen y mute por grupo en PlayerPrefs (lo que consume el menú de Opciones).
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

        [Header("Sonidos")]
        [Tooltip("Alta de sonidos: id + clip + volumen + pitch + loop + grupo. " +
                 "El id es lo que se pasa a PlaySFX(id) / PlayMusic(id).")]
        [SerializeField] private List<SoundData> sounds = new List<SoundData>();

        [Header("Valores por defecto (usados si no hay nada guardado en PlayerPrefs)")]
        [Range(0f, 1f)] [SerializeField] private float defaultMasterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float defaultMusicVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float defaultSfxVolume = 1f;

        [Header("Música")]
        [Tooltip("Segundos de fundido al cambiar o parar la música. 0 = corte seco.")]
        [Range(0f, 5f)] [SerializeField] private float musicFadeDuration = 0.75f;

        [Tooltip("Carpeta dentro de un 'Resources' donde PlaySceneMusic(id) busca el clip por " +
                 "nombre (MainHub, World1-1, BossBattle1...). Añadir ahí un clip con el nombre " +
                 "correcto le pone música a esa escena/fase sin tocar código ni el Inspector.")]
        [SerializeField] private string musicResourceFolder = "Music";

        [Header("Voces de SFX")]
        [Tooltip("Nº de AudioSource reservados para SFX. Cada sonido con pitch propio necesita su " +
                 "propia voz porque el pitch es del AudioSource, no del PlayOneShot.")]
        [Range(1, 32)] [SerializeField] private int sfxVoiceCount = 8;

        private readonly Dictionary<string, SoundData> _library = new Dictionary<string, SoundData>();
        private readonly Dictionary<string, float> _volume = new Dictionary<string, float>();
        private readonly Dictionary<string, bool> _muted = new Dictionary<string, bool>();
        private readonly HashSet<string> _missingSceneMusic = new HashSet<string>();

        private AudioSource _musicSource;
        private AudioSource[] _sfxVoices;
        private int _nextVoice;
        private string _currentMusicId;
        private Coroutine _musicRoutine;
        private Coroutine _sceneMusicRoutine;

        /// <summary>Grupo del mixer al que van los SFX. Lo usa <see cref="SoundEmitter"/>.</summary>
        public AudioMixerGroup SfxGroup => sfxGroup;

        /// <summary>Grupo del mixer al que va la música.</summary>
        public AudioMixerGroup MusicGroup => musicGroup;

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
            // Empujar los valores al mixer un frame después de Awake: SetFloat sobre parámetros
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

            foreach (var sound in sounds)
            {
                if (sound == null || string.IsNullOrEmpty(sound.id)) continue;
                if (_library.ContainsKey(sound.id))
                {
                    Debug.LogWarning($"[AudioManager] id de sonido duplicado: '{sound.id}'. Se usa el primero.", this);
                    continue;
                }

                _library[sound.id] = sound;
            }
        }

        private void SetupSources()
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.outputAudioMixerGroup = musicGroup;

            // Voces de SFX. PlayOneShot comparte el pitch del AudioSource, así que cada sonido que
            // quiera pitch propio necesita su propia voz.
            var voicesRoot = new GameObject("SfxVoices");
            voicesRoot.transform.SetParent(transform, false);

            _sfxVoices = new AudioSource[Mathf.Max(1, sfxVoiceCount)];
            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                var voice = voicesRoot.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;
                voice.outputAudioMixerGroup = sfxGroup;
                _sfxVoices[i] = voice;
            }
        }

        // ------------------------------------------------------------------ playback

        /// <summary>Reproduce el sonido <paramref name="id"/> como música (loop, grupo Music, con fundido).</summary>
        public void PlayMusic(string id)
        {
            if (!TryGet(id, out var data)) return;
            if (_currentMusicId == id && _musicSource.isPlaying) return;

            _currentMusicId = id;

            if (_musicRoutine != null) StopCoroutine(_musicRoutine);
            _musicRoutine = StartCoroutine(PlayMusicRoutine(data));
        }

        /// <summary>
        /// Música por convención de escena/fase. Busca <paramref name="id"/> primero en la lista
        /// del Inspector y, si no está, como AudioClip en <c>Resources/{musicResourceFolder}/{id}</c>.
        /// Si no existe en ningún sitio corta la música actual y no suena nada — <b>sin warnings</b>,
        /// para que una escena a la que aún no se le ha puesto tema simplemente esté en silencio.
        /// Lo usa <c>RunManager</c> para el hub (<c>MainHub</c>), las secciones (<c>World{n}-{puesto}</c>)
        /// y los jefes (<c>BossBattle{n}</c>): basta con dejar un clip con ese nombre en Resources/Music.
        /// </summary>
        public void PlaySceneMusic(string id)
        {
            if (_sceneMusicRoutine != null)
            {
                StopCoroutine(_sceneMusicRoutine);
                _sceneMusicRoutine = null;
            }

            if (string.IsNullOrEmpty(id))
            {
                StopMusic();
                return;
            }

            // Ya resuelto antes (en la lista del Inspector o en una carga previa): reproducir ya.
            if (_library.ContainsKey(id))
            {
                PlayMusic(id);
                return;
            }

            // Se sabe que no existe: silencio, sin volver a tocar disco.
            if (_missingSceneMusic.Contains(id))
            {
                StopMusic();
                return;
            }

            // Primera vez con este id: cargar el clip de Resources en asíncrono. Hacerlo síncrono
            // (Resources.Load) es lo que provocaba el tirón al cambiar de escena, porque bloquea el
            // hilo principal mientras se abre el asset. La música anterior sigue sonando hasta que
            // el clip nuevo está listo, así que no hay silencio intermedio.
            _sceneMusicRoutine = StartCoroutine(LoadSceneMusicRoutine(id));
        }

        // Carga un clip de música de Resources por nombre, en asíncrono. Cachea el acierto en
        // _library (como SoundData sintético en loop por el grupo Music) y el fallo en
        // _missingSceneMusic para no reintentar cada transición. El caché de fallos es de esta
        // sesión: un clip añadido más tarde se detecta al reentrar en Play o en un build.
        private IEnumerator LoadSceneMusicRoutine(string id)
        {
            string path = string.IsNullOrEmpty(musicResourceFolder) ? id : musicResourceFolder + "/" + id;

            var request = Resources.LoadAsync<AudioClip>(path);
            yield return request;

            _sceneMusicRoutine = null;

            var clip = request.asset as AudioClip;   // null si no existe: no registra nada en consola
            if (clip == null)
            {
                _missingSceneMusic.Add(id);
                StopMusic();
                yield break;
            }

            _library[id] = new SoundData
            {
                id = id,
                clip = clip,
                volume = 1f,
                pitch = 1f,
                loop = true,
                mixerGroup = musicGroup
            };

            PlayMusic(id);
        }

        // Espera a que el clip esté cargado (puede tener "Preload Audio Data" desactivado) y luego
        // hace un fundido de entrada. Con Time.timeScale = 0 la corrutina sigue avanzando porque
        // se mide con Time.unscaledDeltaTime.
        private IEnumerator PlayMusicRoutine(SoundData data)
        {
            var clip = data.clip;
            float targetVolume = Mathf.Clamp01(data.volume);

            // Fundido de salida de lo que estuviera sonando.
            yield return FadeMusicOut();

            _musicSource.clip = clip;
            _musicSource.loop = data.loop;
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

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogWarning($"[AudioManager] No se pudo cargar el clip de música '{data.id}'.", this);
                _musicRoutine = null;
                yield break;
            }

            _musicSource.volume = musicFadeDuration > 0f ? 0f : targetVolume;
            _musicSource.Play();

            // Fundido de entrada.
            while (_musicSource.volume < targetVolume)
            {
                _musicSource.volume = musicFadeDuration > 0f
                    ? Mathf.MoveTowards(_musicSource.volume, targetVolume, targetVolume * Time.unscaledDeltaTime / musicFadeDuration)
                    : targetVolume;
                yield return null;
            }

            _musicSource.volume = targetVolume;
            _musicRoutine = null;
        }

        /// <summary>Para la música con un fundido de salida.</summary>
        public void StopMusic()
        {
            if (_musicRoutine != null)
            {
                StopCoroutine(_musicRoutine);
                _musicRoutine = null;
            }

            _currentMusicId = null;
            _musicRoutine = StartCoroutine(StopMusicRoutine());
        }

        private IEnumerator StopMusicRoutine()
        {
            yield return FadeMusicOut();
            _musicSource.Stop();
            _musicSource.clip = null;
            _musicRoutine = null;
        }

        private IEnumerator FadeMusicOut()
        {
            if (!_musicSource.isPlaying) yield break;

            if (musicFadeDuration <= 0f)
            {
                _musicSource.Stop();
                yield break;
            }

            float start = _musicSource.volume;
            while (_musicSource.volume > 0f)
            {
                _musicSource.volume = Mathf.MoveTowards(_musicSource.volume, 0f, start * Time.unscaledDeltaTime / musicFadeDuration);
                yield return null;
            }

            _musicSource.Stop();
        }

        /// <summary>Reproduce el sonido <paramref name="id"/> como efecto puntual por el grupo SFX.</summary>
        public void PlaySFX(string id)
        {
            if (!TryGet(id, out var data)) return;
            if (data.clip == null) return;

            PlayClip(data.clip, data.volume, data.pitch, data.mixerGroup);
        }

        /// <summary>
        /// Reproduce un AudioClip suelto por el grupo SFX del mixer, con su propio volumen y pitch.
        /// Es la vía que usa <see cref="SoundEmitter"/>: el clip lo aporta el objeto, pero el
        /// enrutado (y por tanto el volumen/mute global de Master y SFX) sigue siendo de AudioManager.
        /// </summary>
        public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f, AudioMixerGroup group = null)
        {
            if (clip == null || _sfxVoices == null || _sfxVoices.Length == 0) return;

            var voice = TakeVoice();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume);
            voice.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            voice.outputAudioMixerGroup = group != null ? group : sfxGroup;
            voice.Play();
        }

        // Coge una voz libre; si están todas ocupadas roba la más antigua (round-robin).
        private AudioSource TakeVoice()
        {
            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                var candidate = _sfxVoices[(_nextVoice + i) % _sfxVoices.Length];
                if (candidate != null && !candidate.isPlaying)
                {
                    _nextVoice = (_nextVoice + i + 1) % _sfxVoices.Length;
                    return candidate;
                }
            }

            var stolen = _sfxVoices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _sfxVoices.Length;
            stolen.Stop();
            return stolen;
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
