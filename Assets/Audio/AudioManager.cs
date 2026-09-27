using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using RedMagic.Core;

namespace RedMagic.Audio
{
    /// <summary>Asa de un sonido en bucle arrancado con <see cref="AudioManager.StartLoop"/>. default = ninguno.</summary>
    public readonly struct SoundLoop
    {
        internal readonly int Id;
        internal SoundLoop(int id) { Id = id; }
        public bool IsValid => Id != 0;
    }

    /// <summary>
    /// Gestor de audio global. Singleton persistente (DontDestroyOnLoad).
    ///
    /// Reproducción: <see cref="Play(SoundCue)"/> / <see cref="Play(SoundCue, Vector3)"/> para efectos
    /// puntuales y <see cref="StartLoop"/> / <see cref="StopLoop"/> para bucles. Los efectos puntuales
    /// salen de un pool de AudioSources que crece bajo demanda hasta <see cref="sfxVoicesMax"/>; con
    /// todas ocupadas se roba la de menor prioridad (nunca una mayor). El mismo clip no se apila:
    /// dos arranques dentro de <see cref="sameClipWindow"/> suenan como uno y hay un máximo de
    /// <see cref="maxSameClip"/> copias a la vez. Los bucles se pausan con el juego; los efectos
    /// puntuales no (son cortos, y así la UI sigue sonando en pausa).
    ///
    /// Posicional: sin audio 3D (cámara ortográfica). Volumen y paneo se calculan en 2D contra la
    /// cámara principal, con el rolloff de cada <see cref="SoundCue"/>.
    ///
    /// Es dueño del único AudioListener: lo lleva él (persistente) y apaga el de cada cámara de
    /// escena al cargarse, así nunca hay cero listeners entre descargar y cargar una sección.
    ///
    /// Música y volúmenes por grupo del mixer (Master / Music / SFX, persistidos en PlayerPrefs)
    /// igual que antes.
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

        [Header("Sonidos de UI (UiSounds los usa en todos los menús)")]
        [SerializeField] private SoundCue uiHover = new SoundCue { priority = SoundPriority.Critical };
        [SerializeField] private SoundCue uiClick = new SoundCue { priority = SoundPriority.Critical };
        [SerializeField] private SoundCue uiBack = new SoundCue { priority = SoundPriority.Critical };
        [SerializeField] private SoundCue uiDeny = new SoundCue { priority = SoundPriority.Critical };

        // TEMPORAL (audio fase 4): la tabla de ids antigua. Nada la lee en runtime; sólo la
        // herramienta de migración, para resolver cada id a su clip. Se borra en la fase 4.
        [SerializeField, HideInInspector] private List<SoundData> sounds = new List<SoundData>();

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

        [Header("Voces de SFX (móvil: vigilar el total frente a Project Settings ▸ Audio ▸ Max Real Voices)")]
        [Tooltip("AudioSources de efectos creados al arrancar.")]
        [FormerlySerializedAs("sfxVoiceCount")]
        [Range(1, 32)] [SerializeField] private int sfxVoicesInitial = 8;

        [Tooltip("Tope de AudioSources de efectos. El pool crece hasta aquí; a partir de ahí se roba voz por prioridad.")]
        [Range(1, 32)] [SerializeField] private int sfxVoicesMax = 20;

        [Tooltip("Tope de bucles simultáneos (cargas, rayos, zonas…).")]
        [Range(1, 16)] [SerializeField] private int loopVoicesMax = 6;

        [Header("Solapamiento")]
        [Tooltip("Segundos: el mismo clip arrancado otra vez dentro de esta ventana no suena (5 perdigones " +
                 "que impactan a la vez = un solo golpe, no uno 5 veces más fuerte).")]
        [Range(0f, 0.2f)] [SerializeField] private float sameClipWindow = 0.04f;

        [Tooltip("Copias simultáneas máximas del mismo clip. Al pasarse, se reinicia la más antigua.")]
        [Range(1, 8)] [SerializeField] private int maxSameClip = 3;

        [Header("Posicional (2D)")]
        [Tooltip("Distancia horizontal (unidades) a la que el paneo llega a su máximo.")]
        [Min(0.1f)] [SerializeField] private float panDistance = 12f;

        [Tooltip("Paneo máximo (0 = mono, 1 = todo a un lado).")]
        [Range(0f, 1f)] [SerializeField] private float maxPan = 0.6f;

        [Header("Listener")]
        [Tooltip("AudioManager lleva el único AudioListener y apaga el de las cámaras de escena.")]
        [SerializeField] private bool ownAudioListener = true;

        [Header("Ajustes guardados")]
        [Tooltip("Segundos sin cambios antes de escribir el volumen a disco (arrastrar un slider no escribe cada frame).")]
        [Range(0.1f, 3f)] [SerializeField] private float prefsSaveDelay = 0.5f;

        private readonly Dictionary<string, AudioClip> _sceneMusic = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _volume = new Dictionary<string, float>();
        private readonly Dictionary<string, bool> _muted = new Dictionary<string, bool>();
        private readonly HashSet<string> _missingSceneMusic = new HashSet<string>();

        // Una voz = un AudioSource y lo que hace falta para robarla, atenuarla o pausarla.
        private sealed class Voice
        {
            public AudioSource Source;
            public AudioClip Clip;
            public SoundPriority Priority;
            public float StartedAt;          // tiempo sin escalar

            // Posicional
            public bool Positional;
            public float RolloffStart, RolloffEnd;
            public Vector3 Position;
            public Transform Follow;
            public float BaseVolume;

            // Sólo bucles
            public int LoopId;
            public Component Owner;
            public bool HasOwner;
            public bool PausesWithGame;
            public bool PausedByGame;
            public float Fade = 1f;
            public float FadeSpeed;          // >0 entrando, <0 saliendo

            public bool Busy => Source.isPlaying || PausedByGame;
        }

        private readonly List<Voice> _oneShots = new List<Voice>();
        private readonly List<Voice> _loops = new List<Voice>();
        private GameObject _voicesRoot;
        private int _nextLoopId;

        private AudioListener _listener;
        private bool _prefsDirty;
        private float _prefsDirtySince;

        private AudioSource _musicSource;
        private AudioClip _currentMusic;
        private Coroutine _musicRoutine;
        private Coroutine _sceneMusicRoutine;

        /// <summary>Grupo del mixer al que van los SFX. Lo usa <see cref="SoundEmitter"/>.</summary>
        public AudioMixerGroup SfxGroup => sfxGroup;

        /// <summary>Grupo del mixer al que va la música.</summary>
        public AudioMixerGroup MusicGroup => musicGroup;

        private const string PrefVolume = "audio.volume.";
        private const string PrefMuted = "audio.muted.";
        private const float MinLinear = 0.0001f; // ~ -80 dB

        /// <summary>Ruta en Resources del prefab que se instancia al arrancar (ver <see cref="Bootstrap"/>).</summary>
        public const string PrefabResourcePath = "AudioManager";

        // Un único AudioManager para todo el juego: el prefab de Resources se instancia antes de
        // cargar la primera escena, así gana siempre. Las copias que aún quedan en escenas se
        // destruyen solas en su Awake (Instance ya existe). Sin prefab, se usa la de la escena.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Instance = null;   // Domain Reload desactivado: no arrastrar la de la sesión anterior
            var prefab = Resources.Load<AudioManager>(PrefabResourcePath);
            if (prefab != null) Instantiate(prefab).name = prefab.name;
        }

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

            SetupSources();
            ReadSettings();
            SetupListener();
        }

        private void Start()
        {
            // Empujar los valores al mixer un frame después de Awake: SetFloat sobre parámetros
            // expuestos puede ignorarse silenciosamente si se llama demasiado pronto tras la carga.
            ApplyAllToMixer();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SavePrefsIfDirty();
            Instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SavePrefsIfDirty();
        }

        private void OnApplicationQuit() => SavePrefsIfDirty();

        private void Update()
        {
            UpdateLoops();

            if (_prefsDirty && Time.unscaledTime - _prefsDirtySince >= prefsSaveDelay)
                SavePrefsIfDirty();
        }

        private void LateUpdate()
        {
            // OnSpawn de los SoundEmitter activados este frame (ver SoundEmitter: evita el doble
            // sonido de un objeto de pool que se crea activo, se apaga y se vuelve a encender).
            SoundEmitter.FlushPendingSpawns();

            if (_listener != null)
            {
                var cam = Camera.main;
                if (cam != null) transform.position = cam.transform.position;
            }
        }

        // ------------------------------------------------------------------ setup

        private void SetupSources()
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.outputAudioMixerGroup = musicGroup;

            // Voces de SFX. PlayOneShot comparte el pitch del AudioSource, así que cada sonido que
            // quiera pitch propio necesita su propia voz. El pool crece en AcquireOneShot.
            _voicesRoot = new GameObject("SfxVoices");
            _voicesRoot.transform.SetParent(transform, false);

            _oneShots.Clear();
            _loops.Clear();
            int initial = Mathf.Clamp(sfxVoicesInitial, 1, Mathf.Max(1, sfxVoicesMax));
            for (int i = 0; i < initial; i++)
                _oneShots.Add(CreateVoice(loop: false));
        }

        private Voice CreateVoice(bool loop)
        {
            var source = _voicesRoot.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;              // 2D: la posición la resolvemos nosotros (Spatialize)
            source.outputAudioMixerGroup = sfxGroup;
            return new Voice { Source = source };
        }

        // Un solo AudioListener persistente: el de AudioManager. Los de las cámaras de escena se
        // apagan al cargarse cada escena, así al descargar la sección anterior nunca queda ninguno
        // y al cargar la nueva nunca hay dos.
        private void SetupListener()
        {
            if (!ownAudioListener) return;

            _listener = GetComponent<AudioListener>();
            if (_listener == null) _listener = gameObject.AddComponent<AudioListener>();
            _listener.enabled = true;

            DisableOtherListeners();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => DisableOtherListeners();

        private void DisableOtherListeners()
        {
            if (_listener == null) return;

            foreach (var other in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (other != _listener && other.enabled)
                    other.enabled = false;
        }

        // ------------------------------------------------------------------ música

        /// <summary>
        /// Reproduce <paramref name="clip"/> como música (loop, grupo Music, con fundido). Si ese clip
        /// ya está sonando no hace nada, así volver a un menú no reinicia su tema.
        /// </summary>
        public void PlayMusic(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (_currentMusic == clip && _musicSource.isPlaying) return;

            _currentMusic = clip;

            if (_musicRoutine != null) StopCoroutine(_musicRoutine);
            _musicRoutine = StartCoroutine(PlayMusicRoutine(clip, volume));
        }

        /// <summary>
        /// Música por convención de escena/fase: el AudioClip <c>Resources/{musicResourceFolder}/{nombre}</c>.
        /// Si no existe corta la música actual y no suena nada — <b>sin warnings</b>, para que una
        /// escena a la que aún no se le ha puesto tema simplemente esté en silencio.
        /// Lo usa <c>RunManager</c> para el hub (<c>MainHub</c>), las secciones (<c>World{n}-{puesto}</c>)
        /// y los jefes (<c>BossBattle{n}</c>): basta con dejar un clip con ese nombre en Resources/Music.
        /// </summary>
        public void PlaySceneMusic(string resourceName)
        {
            if (_sceneMusicRoutine != null)
            {
                StopCoroutine(_sceneMusicRoutine);
                _sceneMusicRoutine = null;
            }

            if (string.IsNullOrEmpty(resourceName))
            {
                StopMusic();
                return;
            }

            // Ya cargado en una transición anterior: reproducir ya.
            if (_sceneMusic.TryGetValue(resourceName, out var cached))
            {
                PlayMusic(cached);
                return;
            }

            // Se sabe que no existe: silencio, sin volver a tocar disco.
            if (_missingSceneMusic.Contains(resourceName))
            {
                StopMusic();
                return;
            }

            // Primera vez con este nombre: cargar el clip de Resources en asíncrono. Hacerlo síncrono
            // (Resources.Load) es lo que provocaba el tirón al cambiar de escena, porque bloquea el
            // hilo principal mientras se abre el asset. La música anterior sigue sonando hasta que
            // el clip nuevo está listo, así que no hay silencio intermedio.
            _sceneMusicRoutine = StartCoroutine(LoadSceneMusicRoutine(resourceName));
        }

        // Carga un clip de música de Resources por nombre, en asíncrono. Cachea el acierto en
        // _sceneMusic y el fallo en _missingSceneMusic para no reintentar cada transición. El caché
        // de fallos es de esta sesión: un clip añadido más tarde se detecta al reentrar en Play o en un build.
        private IEnumerator LoadSceneMusicRoutine(string resourceName)
        {
            string path = string.IsNullOrEmpty(musicResourceFolder) ? resourceName : musicResourceFolder + "/" + resourceName;

            var request = Resources.LoadAsync<AudioClip>(path);
            yield return request;

            _sceneMusicRoutine = null;

            var clip = request.asset as AudioClip;   // null si no existe: no registra nada en consola
            if (clip == null)
            {
                _missingSceneMusic.Add(resourceName);
                StopMusic();
                yield break;
            }

            _sceneMusic[resourceName] = clip;
            PlayMusic(clip);
        }

        // Espera a que el clip esté cargado (puede tener "Preload Audio Data" desactivado) y luego
        // hace un fundido de entrada. Con Time.timeScale = 0 la corrutina sigue avanzando porque
        // se mide con Time.unscaledDeltaTime.
        private IEnumerator PlayMusicRoutine(AudioClip clip, float volume)
        {
            float targetVolume = Mathf.Clamp01(volume);

            // Fundido de salida de lo que estuviera sonando.
            yield return FadeMusicOut();

            _musicSource.clip = clip;
            _musicSource.loop = true;
            _musicSource.outputAudioMixerGroup = musicGroup;

            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();

            while (clip.loadState == AudioDataLoadState.Loading)
                yield return null;

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogWarning($"[AudioManager] No se pudo cargar el clip de música '{clip.name}'.", this);
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

            _currentMusic = null;
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

        // ------------------------------------------------------------------ SoundCue: efectos puntuales

        /// <summary>Efecto puntual sin posición (UI, avisos globales). Ignora <see cref="SoundCue.positional"/>.</summary>
        public void Play(SoundCue cue) => PlayCue(cue, false, default);

        /// <summary>
        /// Efecto puntual en un punto del mundo. Si la cue es <see cref="SoundCue.positional"/> se
        /// atenúa y panea respecto a la cámara (y lejos del todo no ocupa voz); si no, suena 2D.
        /// </summary>
        public void Play(SoundCue cue, Vector3 worldPosition) => PlayCue(cue, true, worldPosition);

        private void PlayCue(SoundCue cue, bool hasPosition, Vector3 position)
        {
            if (cue == null || _voicesRoot == null) return;

            float attenuation = 1f, pan = 0f;
            bool positional = hasPosition && cue.positional;
            if (positional && !Spatialize(position, cue.RolloffStart, cue.RolloffEnd, out attenuation, out pan))
                return;     // fuera de alcance: ni se elige variante ni se gasta voz

            var clip = cue.PickClip();
            if (clip == null) return;

            float volume = cue.Volume * attenuation;
            if (volume <= 0.0001f) return;

            StartOneShot(clip, volume, cue.PickPitch(), pan, cue.Priority, sfxGroup);
        }

        private void StartOneShot(AudioClip clip, float volume, float pitch, float pan,
                                  SoundPriority priority, AudioMixerGroup group)
        {
            float now = Time.unscaledTime;

            // Anti-apilado del mismo clip: un arranque casi simultáneo no suena, y pasado el
            // máximo de copias se reinicia la más antigua en vez de quitarle la voz a otro sonido.
            int sameCount = 0;
            Voice oldestSame = null;
            for (int i = 0; i < _oneShots.Count; i++)
            {
                var v = _oneShots[i];
                if (v.Clip != clip || !v.Source.isPlaying) continue;
                if (now - v.StartedAt < sameClipWindow) return;

                sameCount++;
                if (oldestSame == null || v.StartedAt < oldestSame.StartedAt) oldestSame = v;
            }

            var voice = sameCount >= maxSameClip ? oldestSame : AcquireOneShot(priority);
            if (voice == null) return;      // todo ocupado por sonidos más importantes

            EnsureLoaded(clip);

            var source = voice.Source;
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            source.panStereo = pan;
            source.outputAudioMixerGroup = group != null ? group : sfxGroup;

            voice.Clip = clip;
            voice.Priority = priority;
            voice.StartedAt = now;
            source.Play();
        }

        // Voz libre → crecer el pool si queda cupo → robar la de menor prioridad (y más antigua)
        // siempre que no sea más importante que la que llega. Null = no se toca nada.
        private Voice AcquireOneShot(SoundPriority priority)
        {
            for (int i = 0; i < _oneShots.Count; i++)
                if (!_oneShots[i].Source.isPlaying) return _oneShots[i];

            if (_oneShots.Count < sfxVoicesMax)
            {
                var grown = CreateVoice(loop: false);
                _oneShots.Add(grown);
                return grown;
            }

            return FindVictim(_oneShots, priority);
        }

        private static Voice FindVictim(List<Voice> voices, SoundPriority priority)
        {
            Voice victim = null;
            for (int i = 0; i < voices.Count; i++)
            {
                var v = voices[i];
                if (v.Priority > priority) continue;
                if (victim == null || v.Priority < victim.Priority ||
                    (v.Priority == victim.Priority && v.StartedAt < victim.StartedAt))
                    victim = v;
            }
            return victim;
        }

        // Con "Preload Audio Data" (lo pone SfxImportPostprocessor) el clip ya está en memoria al
        // cargar la escena; esto sólo cubre un clip importado a mano sin él.
        private static void EnsureLoaded(AudioClip clip)
        {
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        // Atenuación lineal entre rolloffStart (volumen completo) y rolloffEnd (silencio) medida en
        // 2D contra la cámara principal, y paneo según la distancia horizontal. False = inaudible.
        private bool Spatialize(Vector3 position, float rolloffStart, float rolloffEnd,
                                out float attenuation, out float pan)
        {
            var cam = Camera.main;
            Vector3 listener = cam != null ? cam.transform.position : transform.position;

            float dx = position.x - listener.x;
            float dy = position.y - listener.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);

            attenuation = 1f - Mathf.InverseLerp(rolloffStart, rolloffEnd, distance);
            pan = Mathf.Clamp(dx / panDistance, -1f, 1f) * maxPan;
            return attenuation > 0.001f;
        }

        // ------------------------------------------------------------------ SoundCue: bucles

        /// <summary>
        /// Arranca <paramref name="cue"/> en bucle. Se para con <see cref="StopLoop"/> o solo, cuando
        /// <paramref name="owner"/> se destruye o se desactiva (seguro con objetos de pool). Si
        /// <paramref name="pausesWithGame"/>, se pausa mientras el juego está en pausa. Si la cue es
        /// posicional, sigue a <paramref name="follow"/> (o a <paramref name="owner"/>).
        /// </summary>
        public SoundLoop StartLoop(SoundCue cue, Component owner, Transform follow = null,
                                   bool pausesWithGame = true, float fadeIn = 0f)
        {
            if (cue == null || _voicesRoot == null) return default;

            var clip = cue.PickClip();
            if (clip == null) return default;

            var priority = cue.Priority;
            var voice = AcquireLoop(priority);
            if (voice == null) return default;

            if (++_nextLoopId <= 0) _nextLoopId = 1;
            EnsureLoaded(clip);

            voice.Clip = clip;
            voice.Priority = priority;
            voice.StartedAt = Time.unscaledTime;
            voice.LoopId = _nextLoopId;
            voice.Owner = owner;
            voice.HasOwner = owner != null;
            voice.PausesWithGame = pausesWithGame;
            voice.PausedByGame = false;
            voice.BaseVolume = cue.Volume;
            voice.Positional = cue.positional;
            voice.RolloffStart = cue.RolloffStart;
            voice.RolloffEnd = cue.RolloffEnd;
            voice.Follow = follow != null ? follow : owner != null ? owner.transform : null;
            voice.Position = voice.Follow != null ? voice.Follow.position : transform.position;
            voice.Fade = fadeIn > 0f ? 0f : 1f;
            voice.FadeSpeed = fadeIn > 0f ? 1f / fadeIn : 0f;

            var source = voice.Source;
            source.Stop();
            source.clip = clip;
            source.loop = true;
            source.pitch = cue.PickPitch();
            source.outputAudioMixerGroup = sfxGroup;
            ApplyLoopVolume(voice);
            source.Play();

            return new SoundLoop(voice.LoopId);
        }

        /// <summary>Para un bucle con un fundido corto. Llamarlo con un asa ya parada no hace nada.</summary>
        public void StopLoop(SoundLoop loop, float fadeOut = 0.08f)
        {
            var voice = FindLoop(loop);
            if (voice == null) return;

            if (fadeOut <= 0f || voice.PausedByGame) ReleaseLoop(voice);
            else voice.FadeSpeed = -1f / fadeOut;
        }

        public bool IsLoopPlaying(SoundLoop loop) => FindLoop(loop) != null;

        private Voice FindLoop(SoundLoop loop)
        {
            if (!loop.IsValid) return null;
            for (int i = 0; i < _loops.Count; i++)
                if (_loops[i].LoopId == loop.Id) return _loops[i];
            return null;
        }

        private Voice AcquireLoop(SoundPriority priority)
        {
            for (int i = 0; i < _loops.Count; i++)
                if (_loops[i].LoopId == 0) return _loops[i];

            if (_loops.Count < loopVoicesMax)
            {
                var grown = CreateVoice(loop: true);
                _loops.Add(grown);
                return grown;
            }

            var victim = FindVictim(_loops, priority);
            if (victim != null) ReleaseLoop(victim);
            return victim;
        }

        private static void ReleaseLoop(Voice voice)
        {
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.LoopId = 0;
            voice.Owner = null;
            voice.HasOwner = false;
            voice.Follow = null;
            voice.PausedByGame = false;
            voice.Clip = null;
        }

        private void UpdateLoops()
        {
            if (_loops.Count == 0) return;

            bool gamePaused = IsGamePaused();
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < _loops.Count; i++)
            {
                var v = _loops[i];
                if (v.LoopId == 0) continue;

                // Dueño destruido o apagado (proyectil devuelto al pool, enemigo muerto…).
                if (v.HasOwner && (v.Owner == null || !v.Owner.gameObject.activeInHierarchy ||
                                   (v.Owner is Behaviour b && !b.enabled)))
                {
                    ReleaseLoop(v);
                    continue;
                }

                if (v.PausesWithGame)
                {
                    if (gamePaused && !v.PausedByGame) { v.Source.Pause(); v.PausedByGame = true; }
                    else if (!gamePaused && v.PausedByGame) { v.Source.UnPause(); v.PausedByGame = false; }
                }
                if (v.PausedByGame) continue;

                if (v.FadeSpeed != 0f)
                {
                    v.Fade += v.FadeSpeed * dt;
                    if (v.Fade >= 1f) { v.Fade = 1f; v.FadeSpeed = 0f; }
                    else if (v.Fade <= 0f) { ReleaseLoop(v); continue; }
                }

                if (v.Follow != null) v.Position = v.Follow.position;
                ApplyLoopVolume(v);
            }
        }

        private void ApplyLoopVolume(Voice v)
        {
            float attenuation = 1f, pan = 0f;
            if (v.Positional) Spatialize(v.Position, v.RolloffStart, v.RolloffEnd, out attenuation, out pan);

            v.Source.volume = Mathf.Clamp01(v.BaseVolume * v.Fade * attenuation);
            v.Source.panStereo = pan;
        }

        // En pausa = algún menú bloqueante abierto, o el tiempo congelado por otra vía
        // (transiciones de RunManager, cinemática de pasiva).
        private static bool IsGamePaused() =>
            (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) || Time.timeScale == 0f;

        // ------------------------------------------------------------------ UI

        public SoundCue UiHover => uiHover;
        public SoundCue UiClick => uiClick;
        public SoundCue UiBack => uiBack;
        public SoundCue UiDeny => uiDeny;

        // ------------------------------------------------------------------ mixer / settings

        public float GetVolume(string group) => _volume.TryGetValue(group, out var v) ? v : 1f;

        public bool IsMuted(string group) => _muted.TryGetValue(group, out var m) && m;

        public void SetVolume(string group, float value)
        {
            value = Mathf.Clamp01(value);
            _volume[group] = value;
            ApplyToMixer(group);

            PlayerPrefs.SetFloat(PrefVolume + group, value);
            MarkPrefsDirty();
        }

        public void ToggleMute(string group) => SetMute(group, !IsMuted(group));

        // PlayerPrefs.Set* sólo toca memoria; lo caro es Save (disco). Se escribe cuando el valor
        // lleva prefsSaveDelay quieto, al salir de la app o al pasar a segundo plano.
        private void MarkPrefsDirty()
        {
            _prefsDirty = true;
            _prefsDirtySince = Time.unscaledTime;
        }

        private void SavePrefsIfDirty()
        {
            if (!_prefsDirty) return;
            _prefsDirty = false;
            PlayerPrefs.Save();
        }

        public void SetMute(string group, bool muted)
        {
            _muted[group] = muted;
            ApplyToMixer(group);

            PlayerPrefs.SetInt(PrefMuted + group, muted ? 1 : 0);
            MarkPrefsDirty();
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
