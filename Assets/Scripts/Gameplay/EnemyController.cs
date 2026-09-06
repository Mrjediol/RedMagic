using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// IA placeholder de enemigo: se queda quieto o patrulla de un lado a otro alrededor de su
    /// posición inicial. Sin persecución ni ataques todavía; es sólo para probar la escena.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class EnemyController : MonoBehaviour
    {
        public enum Behaviour
        {
            Idle,
            Patrol
        }

        [Header("Comportamiento")]
        [SerializeField] private Behaviour behaviour = Behaviour.Patrol;
        [SerializeField] private float patrolSpeed = 2f;
        [Tooltip("Distancia a cada lado del punto de inicio.")]
        [SerializeField] private float patrolDistance = 3f;
        [Tooltip("Segundos parado en cada extremo antes de dar la vuelta.")]
        [SerializeField] private float waitAtEnds = 0.5f;
        [SerializeField] private bool startMovingRight = true;

        [Header("Daño por contacto (placeholder)")]
        [Tooltip("Daño que hace al jugador al tocarlo. 0 = no hace daño.")]
        [SerializeField] private float contactDamage;
        [SerializeField] private float contactDamageCooldown = 1f;

        private Rigidbody2D _body;
        private SpriteRenderer _sprite;
        private Health _health;
        private SoundEmitter _sound;

        private Vector2 _origin;
        private int _direction = 1;
        private float _waitTimer;
        private float _contactTimer;
        private bool _active = true;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            _health = GetComponent<Health>();
            _sound = GetComponent<SoundEmitter>();

            _body.freezeRotation = true;
            _origin = transform.position;
            _direction = startMovingRight ? 1 : -1;
        }

        private void OnEnable()
        {
            if (_health == null) return;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void OnDamaged(float amount)
        {
            // Sonido de impacto configurado por objeto en su SoundEmitter.
            _sound?.Play("OnHit");
        }

        private void Update()
        {
            if (_contactTimer > 0f) _contactTimer -= Time.deltaTime;
        }

        private void FixedUpdate()
        {
            if (!_active || !GameStateManager.CanPlayerAct)
            {
                Stop();
                return;
            }

            if (behaviour == Behaviour.Idle)
            {
                Stop();
                return;
            }

            Patrol();
        }

        private void Patrol()
        {
            if (_waitTimer > 0f)
            {
                _waitTimer -= Time.fixedDeltaTime;
                Stop();
                return;
            }

            float offsetFromOrigin = transform.position.x - _origin.x;
            if ((_direction > 0 && offsetFromOrigin >= patrolDistance) ||
                (_direction < 0 && offsetFromOrigin <= -patrolDistance))
            {
                _direction = -_direction;
                _waitTimer = waitAtEnds;
                Stop();
                return;
            }

            var velocity = _body.linearVelocity;
            velocity.x = _direction * patrolSpeed;
            _body.linearVelocity = velocity;

            if (_sprite != null) _sprite.flipX = _direction < 0;
        }

        private void Stop()
        {
            var velocity = _body.linearVelocity;
            velocity.x = 0f;
            _body.linearVelocity = velocity;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (contactDamage <= 0f || _contactTimer > 0f || !_active) return;

            var otherHealth = collision.collider.GetComponentInParent<Health>();
            if (otherHealth == null || otherHealth == _health) return;

            otherHealth.TakeDamage(contactDamage);
            _contactTimer = contactDamageCooldown;
        }

        private void OnDied()
        {
            _active = false;
            Stop();

            _sound?.Play("OnDeath");

            // Placeholder: se apaga el enemigo. Sustitúyelo por animación de muerte / drop / etc.
            if (_sprite != null) _sprite.color = new Color(0.35f, 0.1f, 0.12f, 0.5f);
            foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;
            _body.simulated = false;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = Application.isPlaying ? (Vector3)_origin : transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin + Vector3.left * patrolDistance, origin + Vector3.right * patrolDistance);
        }
    }
}
