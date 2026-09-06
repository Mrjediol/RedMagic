using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Fx;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Proyectil placeholder pensado para pooling: al terminar (impacto o fin de vida) se
    /// desactiva en lugar de destruirse.
    ///
    /// Antes de desactivarse dispara <c>OnDeath</c> en su <see cref="SoundEmitter"/> si lo tiene.
    /// El sonido no se corta al desactivar el objeto porque lo reproduce el AudioManager en sus
    /// propias voces, no un AudioSource local.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class Projectile : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float speed = 14f;
        [Tooltip("Segundos antes de auto-despawn si no impacta con nada.")]
        [SerializeField] private float lifetime = 3f;

        [Header("Daño")]
        [SerializeField] private float damage = 10f;
        [Tooltip("Capas contra las que impacta.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Ciclo de vida")]
        [Tooltip("Destruir al terminar en vez de sólo desactivar. Actívalo cuando el proyectil se " +
                 "instancia en caliente (como hace RangedAttack); déjalo apagado si viene de un pool.")]
        [SerializeField] private bool destroyWhenDone;

        [Header("Impacto")]
        [Tooltip("Efecto que aparece al terminar el proyectil (explosión). Opcional.")]
        [SerializeField] private GameObject impactEffect;
        [Tooltip("Desplazamiento del efecto respecto al punto final del proyectil.")]
        [SerializeField] private Vector2 impactEffectOffset;

        private Rigidbody2D _body;
        private SoundEmitter _sound;
        private Collider2D[] _ownColliders;

        private Vector2 _direction = Vector2.right;
        private GameObject _owner;
        private float _lifeTimer;
        private bool _despawning;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _sound = GetComponent<SoundEmitter>();
            _ownColliders = GetComponentsInChildren<Collider2D>();

            _body.gravityScale = 0f;
            _body.freezeRotation = true;
        }

        private void OnEnable()
        {
            // Reset para reutilización desde el pool.
            _despawning = false;
            _lifeTimer = lifetime;
        }

        /// <summary>
        /// Ajusta daño, velocidad y capas antes de lanzar. Lo usa <c>RangedAttack</c> para que un
        /// mismo prefab de proyectil sirva a distintos atacantes (jugador o enemigo).
        /// Cualquier valor negativo deja el del prefab.
        /// </summary>
        public void Configure(float newDamage, float newSpeed, LayerMask newHitLayers)
        {
            if (newDamage >= 0f) damage = newDamage;
            if (newSpeed > 0f) speed = newSpeed;
            hitLayers = newHitLayers;
        }

        /// <summary>Lanza el proyectil. <paramref name="owner"/> se ignora en las colisiones.</summary>
        public void Launch(Vector2 direction, GameObject owner = null)
        {
            _direction = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
            _owner = owner;
            _lifeTimer = lifetime;
            _despawning = false;

            if (_body != null) _body.linearVelocity = _direction * speed;

            transform.right = _direction;
        }

        private void Update()
        {
            if (_despawning) return;

            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f) Despawn();
        }

        private void FixedUpdate()
        {
            if (_despawning || _body == null) return;
            _body.linearVelocity = _direction * speed;
        }

        private void OnTriggerEnter2D(Collider2D other) => HandleHit(other);

        private void OnCollisionEnter2D(Collision2D collision) => HandleHit(collision.collider);

        private void HandleHit(Collider2D other)
        {
            if (_despawning || other == null) return;
            if (((1 << other.gameObject.layer) & hitLayers) == 0) return;

            // Ignorar a quien lo disparó y a los colliders propios.
            if (_owner != null && (other.gameObject == _owner || other.transform.IsChildOf(_owner.transform))) return;
            foreach (var own in _ownColliders)
                if (other == own) return;

            var health = other.GetComponentInParent<Health>();
            if (health != null && damage > 0f)
            {
                health.TakeDamage(damage);
                _sound?.Play("OnHit");
            }

            Despawn();
        }

        /// <summary>Apaga el proyectil para devolverlo al pool, sonando antes su OnDeath.</summary>
        public void Despawn()
        {
            if (_despawning) return;
            _despawning = true;

            // El sonido se dispara ANTES de desactivar el GameObject.
            _sound?.Play("OnDeath");

            // La explosión es un objeto aparte, así que sobrevive al despawn del proyectil.
            int facing = _direction.x < 0f ? -1 : 1;
            VfxOneShot.Spawn(impactEffect,
                             transform.position + new Vector3(impactEffectOffset.x * facing, impactEffectOffset.y, 0f),
                             facing);

            if (_body != null) _body.linearVelocity = Vector2.zero;

            ReturnToPool();
        }

        /// <summary>
        /// Devolución al pool. Por defecto sólo desactiva el objeto (o lo destruye si
        /// <see cref="destroyWhenDone"/> está activo); sustitúyelo por la llamada a tu pool
        /// real cuando lo tengas.
        /// </summary>
        protected virtual void ReturnToPool()
        {
            if (destroyWhenDone) Destroy(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
