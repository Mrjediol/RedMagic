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

        private SpriteRenderer _renderer;
        private float _timer;
        private int _index;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (randomStart && frames != null && frames.Length > 1)
                _index = Random.Range(0, frames.Length);
        }

        private void Update()
        {
            if (frames == null || frames.Length < 2 || _renderer == null) return;

            _timer += Time.deltaTime;
            float step = 1f / framesPerSecond;
            if (_timer < step) return;

            _timer -= step;
            _index++;

            int span = pingPong && frames.Length > 2 ? (frames.Length - 1) * 2 : frames.Length;
            int frame = _index % span;
            if (frame >= frames.Length) frame = span - frame;

            _renderer.sprite = frames[frame];
        }
    }
}
