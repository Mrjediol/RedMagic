using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Pasa una lista de sprites a ritmo fijo sobre el <see cref="SpriteRenderer"/> del objeto.
    ///
    /// Es el animador más tonto posible, y ésa es la gracia: para un bicho que sólo respira no
    /// hace falta un <c>AnimatorController</c> con sus estados, sus transiciones y su asset que
    /// mantener — hacen falta tres sprites y una cadencia. Los personajes que sí tienen máquina de
    /// estados (el jugador) siguen usando el Animator de Unity.
    ///
    /// El aura de aviso de <c>BossController</c> copia el sprite del cuerpo cada frame, así que un
    /// jefe animado con esto conserva su halo sin ningún trabajo extra.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class SpriteFlipbook : MonoBehaviour
    {
        [Tooltip("Fotogramas, en orden. Con menos de dos, este componente no hace nada.")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("Fotogramas por segundo.")]
        [Min(0.1f)]
        [SerializeField] private float framesPerSecond = 6f;

        [Tooltip("Ida y vuelta (1-2-3-2-1) en vez de reiniciar. Para un ciclo de respiración o de " +
                 "patas queda mucho mejor que el corte del bucle simple.")]
        [SerializeField] private bool pingPong = true;

        [Tooltip("Desfase aleatorio al empezar. Evita que dos copias del mismo bicho parpadeen " +
                 "sincronizadas como un solo objeto raro.")]
        [SerializeField] private bool randomStart = true;

        [Tooltip("Reproduce la tira UNA vez y se queda clavado en el último fotograma, en vez de " +
                 "repetir. Para una explosión o un impacto, que no deben volver a empezar. " +
                 "Apagado (por defecto) = bucle de siempre.")]
        [SerializeField] private bool oneShot;

        private SpriteRenderer _renderer;
        private float _timer;
        private int _index;

        /// <summary>true cuando una tira <see cref="oneShot"/> ya ha llegado al final.</summary>
        public bool Finished { get; private set; }

        /// <summary>Segundos que dura la tira entera a la cadencia actual.</summary>
        public float Duration => frames == null || frames.Length == 0
            ? 0f
            : frames.Length / Mathf.Max(0.1f, framesPerSecond);

        private void Awake() => _renderer = GetComponent<SpriteRenderer>();

        /// <summary>
        /// Rebobina. Es imprescindible por el pooling: <c>PrefabPool</c> no tiene hook por
        /// instancia, así que sin esto un objeto reutilizado seguiría la animación por donde la
        /// dejó el anterior — y una tira de un solo uso ya habría terminado antes de empezar.
        /// </summary>
        private void OnEnable()
        {
            _timer = 0f;
            _index = 0;
            Finished = false;

            if (frames == null || frames.Length == 0) return;

            // El desfase aleatorio sólo tiene sentido en un bucle: en una explosión se saltaría el
            // principio, que es justo lo que hay que ver.
            if (randomStart && !oneShot && frames.Length > 1) _index = Random.Range(0, frames.Length);

            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null) _renderer.sprite = frames[Mathf.Min(_index, frames.Length - 1)];
        }

        private void Update()
        {
            if (Finished || frames == null || frames.Length < 2 || _renderer == null) return;

            _timer += Time.deltaTime;
            float step = 1f / framesPerSecond;
            if (_timer < step) return;

            _timer -= step;
            _index++;

            if (oneShot)
            {
                if (_index >= frames.Length)
                {
                    _index = frames.Length - 1;
                    Finished = true;
                }
                _renderer.sprite = frames[_index];
                return;
            }

            int span = pingPong && frames.Length > 2 ? (frames.Length - 1) * 2 : frames.Length;
            int frame = _index % span;
            if (frame >= frames.Length) frame = span - frame;

            _renderer.sprite = frames[frame];
        }
    }
}
