using UnityEngine;

namespace RedMagic.Combat
{
    /// <summary>
    /// Contrato para los personajes que NO se mueven con la física del Rigidbody2D y por tanto
    /// no pueden recibir el retroceso como un impulso.
    ///
    /// El jugador es exactamente ese caso: <c>PlayerMovement</c> integra su propia velocidad y
    /// mueve el <c>transform</c> con un Rigidbody2D cinemático, así que un <c>AddForce</c> sobre
    /// él no haría absolutamente nada. Implementando esta interfaz, el controlador recibe la
    /// velocidad del empujón y la mete en su propia integración.
    ///
    /// Existe además para que <see cref="Knockback"/> (en Combat) no tenga que conocer a
    /// <c>PlayerMovement</c> (en Gameplay).
    /// </summary>
    public interface IKnockbackReceiver
    {
        /// <summary>
        /// Empuja al personaje con esa velocidad (unidades/segundo) durante
        /// <paramref name="duration"/> segundos, ignorando la entrada mientras dure.
        /// </summary>
        void ApplyKnockback(Vector2 velocity, float duration);

        /// <summary>
        /// Corta un empujón en curso y devuelve el control de inmediato. Se llama al morir y al
        /// teletransportar al personaje (cambio de sección), donde arrastrar la velocidad y el
        /// bloqueo de la entrada del golpe anterior sería un error visible.
        /// </summary>
        void CancelKnockback();
    }

    /// <summary>
    /// Retroceso al recibir un golpe. Se pone junto al <see cref="Health"/> de cualquier cosa que
    /// deba salir despedida al ser golpeada (jugador y enemigos usan el mismo componente).
    ///
    /// Quien decide la fuerza es <b>la víctima</b>, no el atacante: cada prefab afina aquí cuánto
    /// se le mueve, y el atacante sólo aporta un multiplicador (un golpe pesado empuja más que
    /// uno rápido) y la posición desde la que viene el golpe. Así un jefe puede ser prácticamente
    /// inamovible (<see cref="resistance"/> a 1) sin tocar el código de ningún ataque.
    ///
    /// No hace falta suscribirse a nada: <see cref="Health.TakeDamage(float, Vector2, float)"/>
    /// llama a <see cref="ApplyFrom"/> sólo si el golpe ha entrado de verdad, de modo que un
    /// impacto comido por los i-frames tampoco empuja.
    ///
    /// El empujón se aplica de dos maneras según el personaje:
    ///  - si tiene un <see cref="IKnockbackReceiver"/> (el jugador), se le pasa la velocidad para
    ///    que la integre su propio controlador;
    ///  - si no, se escribe en el Rigidbody2D dinámico (los enemigos).
    /// </summary>
    [DisallowMultipleComponent]
    public class Knockback : MonoBehaviour
    {
        [Header("Fuerza")]
        [Tooltip("Velocidad horizontal del empujón, en unidades/segundo, en sentido contrario al " +
                 "atacante. 0 = no se mueve en horizontal.")]
        [Min(0f)]
        [SerializeField] private float horizontalForce = 8f;

        [Tooltip("Componente vertical del empujón (siempre hacia arriba). Un poco de altura hace " +
                 "que el golpe se lea mucho mejor; demasiada convierte cada impacto en un salto.")]
        [Min(0f)]
        [SerializeField] private float verticalForce = 4f;

        [Tooltip("Segundos que dura el empujón. Mientras corre, el personaje no controla su " +
                 "movimiento (ni la IA del enemigo ni la entrada del jugador).")]
        [Min(0f)]
        [SerializeField] private float duration = 0.18f;

        [Header("Resistencia")]
        [Tooltip("Cuánto se reduce el empujón recibido: 0 = lo recibe entero, 1 = inmune. " +
                 "Súbelo en enemigos pesados o jefes.")]
        [Range(0f, 1f)]
        [SerializeField] private float resistance;

        [Tooltip("Si está activo no recibe retroceso nunca (equivale a resistencia 1).")]
        [SerializeField] private bool immune;

        private Rigidbody2D _body;
        private IKnockbackReceiver _receiver;
        private Health _health;

        private float _timer;

        /// <summary>True mientras dura el empujón: la IA y la entrada deben ceder el control.</summary>
        public bool IsActive => _timer > 0f;

        public bool Immune
        {
            get => immune;
            set => immune = value;
        }

        /// <summary>Dirección horizontal del último empujón (-1 / 1), para animaciones y VFX.</summary>
        public int LastDirection { get; private set; } = 1;

        /// <summary>Se dispara al empezar un empujón, con su velocidad ya calculada.</summary>
        public event System.Action<Vector2> Knocked;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _receiver = GetComponent<IKnockbackReceiver>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (_health != null) _health.Died += Cancel;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= Cancel;
        }

        private void Update()
        {
            if (_timer <= 0f) return;
            _timer = Mathf.Max(0f, _timer - Time.deltaTime);
        }

        /// <summary>
        /// Empuja alejándose de <paramref name="sourcePosition"/> (la posición del atacante, del
        /// proyectil o del cuerpo con el que se ha chocado).
        /// <paramref name="multiplier"/> lo escala: 0 o menos cancela el empujón.
        /// </summary>
        public void ApplyFrom(Vector2 sourcePosition, float multiplier = 1f)
        {
            float dx = transform.position.x - sourcePosition.x;

            // Golpe perfectamente vertical (o exactamente encima): se conserva la dirección
            // anterior en vez de dejar el empujón en cero, que se vería como un golpe sin reacción.
            int direction = Mathf.Abs(dx) < 0.0001f ? LastDirection : (dx < 0f ? -1 : 1);
            Apply(direction, multiplier);
        }

        /// <summary>Empuja en una dirección horizontal concreta (-1 izquierda, 1 derecha).</summary>
        public void Apply(int direction, float multiplier = 1f)
        {
            if (immune || multiplier <= 0f || duration <= 0f) return;

            float scale = multiplier * (1f - Mathf.Clamp01(resistance));
            if (scale <= 0f) return;

            LastDirection = direction < 0 ? -1 : 1;

            var velocity = new Vector2(LastDirection * horizontalForce * scale, verticalForce * scale);
            if (velocity.sqrMagnitude <= 0f) return;

            ApplyVelocity(velocity, duration);
        }

        /// <summary>Empujón directo, sin pasar por la configuración del componente.</summary>
        public void ApplyVelocity(Vector2 velocity, float knockbackDuration)
        {
            _timer = Mathf.Max(_timer, knockbackDuration);

            if (_receiver != null)
            {
                _receiver.ApplyKnockback(velocity, knockbackDuration);
            }
            else if (_body != null && _body.bodyType == RigidbodyType2D.Dynamic)
            {
                // Asignación, no AddForce: el empujón debe sentirse igual con cualquier masa y
                // debe borrar la velocidad que la IA hubiese fijado ese mismo frame.
                _body.linearVelocity = velocity;
            }

            Knocked?.Invoke(velocity);
        }

        /// <summary>Corta el empujón en curso (al morir, o al cargar otra sección).</summary>
        public void Cancel()
        {
            _timer = 0f;
            _receiver?.CancelKnockback();
        }
    }
}
