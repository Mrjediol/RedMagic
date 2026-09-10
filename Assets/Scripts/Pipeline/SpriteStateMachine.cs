using System;
using UnityEngine;

namespace RedMagic.Pipeline
{
    /// <summary>
    /// Reproductor de estados por sprites, sin Animator.
    ///
    /// Es el equivalente de un AnimatorController para todo lo que pasa por pool: un array de
    /// estados (nombre + frames + fps + loop) y un estado activo. Rebobina en <c>OnEnable</c>,
    /// que es el único gancho de reinicio que <c>PrefabPool</c> ofrece — por eso un Animator no
    /// sirve ahí: volvería del pool a mitad de la animación de muerte.
    ///
    /// La lógica de juego no habla con esto directamente: <c>EnemyAnimation</c> traduce el
    /// estado del enemigo tanto a parámetros de Animator como a llamadas a <see cref="Play"/>,
    /// así que el mismo enemigo funciona con cualquiera de los dos motores.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteStateMachine : MonoBehaviour
    {
        [Serializable]
        public class State
        {
            public string name = "Idle";
            public Sprite[] frames;
            [Min(0.1f)] public float fps = 10f;
            public bool loop = true;

            /// <summary>Segundos que dura una pasada completa.</summary>
            public float Duration => frames == null || frames.Length == 0 ? 0f : frames.Length / Mathf.Max(0.1f, fps);
        }

        [SerializeField] private State[] states;

        [Tooltip("Estado con el que arranca y al que se vuelve cuando termina uno que no repite.")]
        [SerializeField] private string defaultState = "Idle";

        private SpriteRenderer _renderer;
        private State _current;
        private State _fallback;
        private float _timer;
        private int _frame;

        /// <summary>Nombre del estado en curso, o vacío.</summary>
        public string CurrentState => _current != null ? _current.name : string.Empty;

        /// <summary>True cuando un estado que no repite ha llegado al último frame.</summary>
        public bool Finished { get; private set; }

        /// <summary>Cuando está activo, <see cref="Play"/> no cambia de estado. Lo usa la muerte.</summary>
        public bool Locked { get; set; }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _fallback = Find(defaultState);
        }

        private void OnEnable()
        {
            // Reinicio obligatorio para pooling: una instancia reutilizada tiene que volver a
            // empezar, no continuar donde la dejó su vida anterior.
            Locked = false;
            _current = null;
            Play(defaultState, true);
        }

        /// <summary>Duración de un estado, para cuadrar tiempos de juego con la animación.</summary>
        public float DurationOf(string stateName)
        {
            var s = Find(stateName);
            return s != null ? s.Duration : 0f;
        }

        public bool Has(string stateName) => Find(stateName) != null;

        /// <summary>
        /// Cambia de estado. Si ya es el activo no reinicia, salvo <paramref name="restart"/>:
        /// así se puede llamar cada frame desde el bucle de un enemigo sin congelar la animación.
        /// </summary>
        public void Play(string stateName, bool restart = false)
        {
            if (Locked && !restart) return;

            var next = Find(stateName);
            if (next == null || (next.frames?.Length ?? 0) == 0) return;
            if (next == _current && !restart) return;

            _current = next;
            _timer = 0f;
            _frame = 0;
            Finished = false;
            Apply();
        }

        private void Update()
        {
            if (_current == null || _current.frames == null || _current.frames.Length == 0) return;
            if (Finished) return;

            _timer += Time.deltaTime;
            float step = 1f / Mathf.Max(0.1f, _current.fps);
            if (_timer < step) return;

            int advance = Mathf.FloorToInt(_timer / step);
            _timer -= advance * step;
            _frame += advance;

            if (_frame < _current.frames.Length)
            {
                Apply();
                return;
            }

            if (_current.loop)
            {
                _frame %= _current.frames.Length;
                Apply();
                return;
            }

            // Sin repetición: se congela en el último frame y, si hay estado por defecto y no
            // está bloqueado (muerte), se vuelve solo.
            _frame = _current.frames.Length - 1;
            Apply();
            Finished = true;

            if (!Locked && _fallback != null && _current != _fallback) Play(_fallback.name);
        }

        private void Apply()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = _current.frames[Mathf.Clamp(_frame, 0, _current.frames.Length - 1)];
        }

        private State Find(string stateName)
        {
            if (states == null || string.IsNullOrEmpty(stateName)) return null;
            for (int i = 0; i < states.Length; i++)
                if (states[i] != null && states[i].name == stateName) return states[i];
            return null;
        }

#if UNITY_EDITOR
        /// <summary>Lo usa el generador para rellenar los estados sin exponerlos en runtime.</summary>
        public void EditorSetStates(State[] value, string fallbackName)
        {
            states = value;
            defaultState = fallbackName;
        }
#endif
    }
}
