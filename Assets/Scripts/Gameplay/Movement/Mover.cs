using System;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Gameplay.Movement
{
    /// <summary>
    /// Componente genérico de movimiento: lleva un <see cref="MovementBehaviour"/> elegido de un
    /// desplegable y lo aplica al cuerpo. No sabe quién lo usa — un jefe, un enemigo o lo que sea —:
    /// el dueño sólo le dice a quién mirar (<see cref="Target"/>), si puede moverse
    /// (<see cref="Paused"/>) y, opcionalmente, entre qué X (<see cref="SetBounds"/>).
    ///
    /// Cambiar el tipo de movimiento de algo es cambiar el desplegable de este componente. Quitar el
    /// componente = no se mueve (el comportamiento de siempre de los jefes).
    ///
    /// Con Rigidbody2D cinemático mueve por <c>MovePosition</c>; con uno dinámico escribe la
    /// velocidad (respetando la vertical si tiene gravedad); sin cuerpo, el transform.
    /// </summary>
    [DisallowMultipleComponent]
    public class Mover : MonoBehaviour
    {
        [Tooltip("Tipo de movimiento. Cambia el desplegable para darle otro comportamiento.")]
        [SerializeReference, SubclassPicker]
        private MovementBehaviour behaviour = new HoverKeepDistance();

        [Tooltip("Velocidad (u/s) por encima de la cual cuenta como 'moviéndose' (anima el Walk).")]
        [Min(0.01f)]
        [SerializeField] private float movingThreshold = 0.2f;

        [Tooltip("Arranca en pausa. El dueño (p.ej. BossController) decide cuándo moverse.")]
        [SerializeField] private bool startPaused = true;

        private Rigidbody2D _body;
        private Vector2 _velocity;
        private bool _paused;
        private bool _bounded;
        private float _minX, _maxX;

        /// <summary>A quién se refiere el comportamiento (del que se aparta, al que sigue…).</summary>
        public Transform Target { get; set; }

        public MovementBehaviour Behaviour
        {
            get => behaviour;
            set => behaviour = value;
        }

        public Vector2 Velocity => _velocity;

        /// <summary>True mientras va por encima de <see cref="movingThreshold"/>.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>Cambia al empezar/dejar de moverse. El dueño lo usa para animar (Walk/Idle).</summary>
        public event Action<bool> MovingChanged;

        /// <summary>En pausa se para en seco y no pide velocidad al comportamiento.</summary>
        public bool Paused
        {
            get => _paused;
            set
            {
                if (_paused == value) return;
                _paused = value;
                if (_paused) Stop();
                else behaviour?.OnResume();
            }
        }

        /// <summary>Límites horizontales del movimiento (la arena, una zona de patrulla…).</summary>
        public void SetBounds(float minX, float maxX)
        {
            _bounded = true;
            _minX = Mathf.Min(minX, maxX);
            _maxX = Mathf.Max(minX, maxX);
        }

        public void ClearBounds() => _bounded = false;

        /// <summary>Para en seco (velocidad 0) sin cambiar el estado de pausa.</summary>
        public void Stop()
        {
            _velocity = Vector2.zero;
            if (_body != null && _body.bodyType == RigidbodyType2D.Dynamic)
                _body.linearVelocity = new Vector2(0f, _body.gravityScale > 0f ? _body.linearVelocity.y : 0f);
            SetMoving(false);
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _paused = startPaused;
        }

        private void OnDisable() => Stop();

        private void FixedUpdate()
        {
            if (_paused || behaviour == null) return;

            float dt = Time.fixedDeltaTime;
            Vector2 position = _body != null ? _body.position : (Vector2)transform.position;

            var frame = new MovementFrame(position, _velocity, Target != null,
                                          Target != null ? (Vector2)Target.position : position,
                                          _bounded, _minX, _maxX, dt);

            _velocity = behaviour.DesiredVelocity(frame);

            // Los límites también se respetan aquí, no sólo en el comportamiento: un tipo de
            // movimiento mal escrito no debe poder sacar a un jefe de su arena.
            Vector2 next = position + _velocity * dt;
            if (_bounded)
            {
                // Si ya está fuera (una embestida lo dejó más cerca de la pared), sólo se le impide
                // alejarse más; nunca se le teletransporta dentro.
                float min = Mathf.Min(_minX, position.x);
                float max = Mathf.Max(_maxX, position.x);
                float clamped = Mathf.Clamp(next.x, min, max);
                if (!Mathf.Approximately(clamped, next.x)) _velocity.x = 0f;
                next.x = clamped;
            }

            Apply(position, next);
            SetMoving(_velocity.magnitude > movingThreshold);
        }

        private void Apply(Vector2 position, Vector2 next)
        {
            if (_body == null)
            {
                transform.position = new Vector3(next.x, next.y, transform.position.z);
                return;
            }

            if (_body.bodyType == RigidbodyType2D.Dynamic)
            {
                float vy = _body.gravityScale > 0f ? _body.linearVelocity.y : _velocity.y;
                _body.linearVelocity = new Vector2(_velocity.x, vy);
                return;
            }

            _body.MovePosition(next);
        }

        private void SetMoving(bool moving)
        {
            if (IsMoving == moving) return;
            IsMoving = moving;
            MovingChanged?.Invoke(moving);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_bounded) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            float y = transform.position.y;
            Gizmos.DrawLine(new Vector3(_minX, y - 1f, 0f), new Vector3(_minX, y + 3f, 0f));
            Gizmos.DrawLine(new Vector3(_maxX, y - 1f, 0f), new Vector3(_maxX, y + 3f, 0f));
        }
    }
}
