using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// IA de enemigo de cuerpo a cuerpo. El ciclo es el clásico de plataformas: deambula por su
    /// zona (o espera quieto), y en cuanto el jugador entra en su radio de detección deja lo que
    /// estaba haciendo y va a por él; al tocarlo le hace daño por contacto.
    ///
    /// La detección y la patrulla son independientes: <see cref="behaviour"/> decide qué hace
    /// mientras NO ve al jugador (quieto = emboscada, patrulla = ronda), y
    /// <see cref="chasePlayer"/> decide si lo persigue cuando lo ve. Así un mismo script cubre
    /// desde un bicho que patrulla y embiste hasta una torreta que sólo espera.
    ///
    /// Para no entrar y salir del estado de persecución en el borde del radio hay histéresis:
    /// engancha a <see cref="detectionRadius"/> y no suelta hasta <see cref="loseSightRadius"/>.
    /// Y con <see cref="stopAtLedges"/> ni la patrulla ni la persecución se tiran por un
    /// precipicio: sondean el suelo un paso por delante antes de avanzar.
    ///
    /// <see cref="stoppingDistance"/> es lo que distingue a un enemigo a distancia de uno cuerpo a
    /// cuerpo en el movimiento: sin ella la IA persigue siempre hasta el contacto, y un disparo
    /// con proyectil nunca se ve porque para cuando dispara ya está encima empujando.
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
        [Tooltip("Si vuela: se mueve libre por el aire (sin gravedad, ignora precipicios) y " +
                 "persigue al objetivo también en vertical. Si no: sólo camina por el suelo.")]
        [SerializeField] private bool canFly;
        [Tooltip("Qué hace mientras NO ve al jugador: quedarse quieto (emboscada) o patrullar.")]
        [SerializeField] private Behaviour behaviour = Behaviour.Patrol;
        [SerializeField] private float patrolSpeed = 2f;
        [Tooltip("Distancia a cada lado del punto de inicio.")]
        [SerializeField] private float patrolDistance = 3f;
        [Tooltip("Segundos parado en cada extremo antes de dar la vuelta.")]
        [SerializeField] private float waitAtEnds = 0.5f;
        [SerializeField] private bool startMovingRight = true;

        [Header("Persecución")]
        [Tooltip("Si está activo, abandona la patrulla y va a por el objetivo cuando lo detecta.")]
        [SerializeField] private bool chasePlayer = true;
        [Tooltip("Etiqueta del objetivo. Es también a quién le hace daño por contacto.")]
        [SerializeField] private string targetTag = "Player";
        [Tooltip("Distancia horizontal a la que detecta al objetivo y empieza a perseguirlo.")]
        [SerializeField] private float detectionRadius = 6f;
        [Tooltip("Distancia a la que se rinde. Conviene que sea mayor que el radio de detección: " +
                 "esa diferencia es la histéresis que evita el parpadeo perseguir/patrullar.")]
        [SerializeField] private float loseSightRadius = 9f;
        [SerializeField] private float chaseSpeed = 3.5f;
        [Tooltip("Diferencia de altura máxima para 'ver' al objetivo. Evita que persiga a alguien " +
                 "que está dos plataformas más arriba y no puede alcanzar.")]
        [SerializeField] private float verticalTolerance = 3f;
        [Tooltip("A esta distancia deja de avanzar y se queda quieto en vez de seguir hasta tocar. " +
                 "0 = siempre cierra hasta el contacto (lo normal en un cuerpo a cuerpo). Es para un " +
                 "enemigo con ataque a distancia: sin esto, la IA lo lleva a pegarse al objetivo " +
                 "igual que uno sin proyectil, y el disparo nunca se lee porque para cuando dispara " +
                 "ya está encima. Sigue re-evaluándose cada frame, así que si el objetivo se aleja " +
                 "más de aquí, vuelve a acercarse.")]
        [SerializeField] private float stoppingDistance;

        [Header("Bordes")]
        [Tooltip("Sondea el suelo un paso por delante y no avanza si no hay: ni patrullando ni " +
                 "persiguiendo se tira por un precipicio.")]
        [SerializeField] private bool stopAtLedges = true;
        [Tooltip("Capas que cuentan como suelo para ese sondeo.")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [Tooltip("Cuánto baja el rayo del sondeo antes de dar el borde por precipicio.")]
        [SerializeField] private float ledgeProbeDepth = 0.6f;

        [Header("Daño por contacto")]
        [Tooltip("Daño que hace al objetivo al tocarlo. 0 = no hace daño.")]
        [SerializeField] private float contactDamage;
        [SerializeField] private float contactDamageCooldown = 1f;
        [Tooltip("Cuánto empuja el golpe por contacto: multiplica el retroceso configurado en el " +
                 "Knockback del objetivo. 1 = el suyo tal cual, 0 = no empuja.")]
        [Min(0f)]
        [SerializeField] private float contactKnockbackMultiplier = 1f;

        private Rigidbody2D _body;
        private SpriteRenderer _sprite;
        private Health _health;
        private SoundEmitter _sound;
        private Collider2D _collider;
        private Knockback _knockback;

        private Vector2 _origin;
        private int _direction = 1;
        private float _waitTimer;
        private float _contactTimer;
        private bool _active = true;

        private Transform _target;
        private float _retargetTimer;
        private bool _chasing;

        /// <summary>True mientras está persiguiendo al objetivo (lo usan animadores y gizmos).</summary>
        public bool IsChasing => _chasing;

        /// <summary>True si el enemigo vuela (se mueve libre por el aire, sin gravedad).</summary>
        public bool CanFly => canFly;

        /// <summary>Segundos entre reintentos de buscar al objetivo cuando no se ha encontrado.</summary>
        private const float RetargetInterval = 0.5f;

        /// <summary>Cuánto se separa del borde del collider el rayo que sondea el suelo.</summary>
        private const float LedgeProbeInset = 0.05f;

        /// <summary>Buffer reutilizable para el sondeo de suelo (sin allocs por frame).</summary>
        private static readonly RaycastHit2D[] GroundHits = new RaycastHit2D[8];

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            _health = GetComponent<Health>();
            _sound = GetComponent<SoundEmitter>();
            _collider = GetComponent<Collider2D>();
            _knockback = GetComponent<Knockback>();

            _body.freezeRotation = true;
            // Un enemigo volador flota: sin gravedad, y sostiene su altura él mismo.
            if (canFly) _body.gravityScale = 0f;
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

            // Mientras sale despedido, la IA no toca la velocidad: si siguiera escribiéndola cada
            // FixedUpdate el empujón se borraría en el mismo frame y el golpe no se notaría.
            if (_knockback != null && _knockback.IsActive) return;

            // La persecución tiene prioridad sobre lo que estuviera haciendo.
            if (TryChase()) return;

            if (behaviour == Behaviour.Idle)
            {
                Stop();
                return;
            }

            Patrol();
        }

        // ------------------------------------------------------------------ persecución

        /// <summary>
        /// Decide si toca perseguir y, si es que sí, mueve al enemigo hacia el objetivo.
        /// Devuelve false cuando no hay a quién perseguir, para que el llamante siga con la
        /// patrulla.
        /// </summary>
        private bool TryChase()
        {
            if (!chasePlayer)
            {
                _chasing = false;
                return false;
            }

            var target = ResolveTarget();
            if (target == null)
            {
                _chasing = false;
                return false;
            }

            Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;

            // Un enemigo de suelo que ve al objetivo fuera de su alcance vertical no le "ve": está
            // en otra plataforma y perseguirlo sólo lo pegaría a una pared debajo de él. Un volador
            // sí puede subir, así que para él la tolerancia vertical no aplica.
            if (!canFly && Mathf.Abs(toTarget.y) > verticalTolerance)
            {
                _chasing = false;
                return false;
            }

            // Histéresis: engancha con detectionRadius y no suelta hasta loseSightRadius. El de
            // suelo mide sólo la distancia horizontal; el volador, la distancia real.
            float distance = canFly ? toTarget.magnitude : Mathf.Abs(toTarget.x);
            float threshold = _chasing ? Mathf.Max(loseSightRadius, detectionRadius) : detectionRadius;
            _chasing = distance <= threshold;

            if (!_chasing) return false;

            _direction = toTarget.x >= 0f ? 1 : -1;
            if (_sprite != null) _sprite.flipX = _direction < 0;

            // Se queda a distancia en vez de cerrar hasta tocar: es lo que le da a un ataque a
            // distancia una ventana real para dispararse antes de que el cuerpo a cuerpo lo tape.
            if (stoppingDistance > 0f && distance <= stoppingDistance)
            {
                Stop();
                return true;
            }

            float reach = (_collider != null ? _collider.bounds.extents.x : 0.3f) + 0.6f;
            bool withinReach = distance <= reach;

            if (canFly)
            {
                // Vuela directo hacia el objetivo en línea recta; al alcance del morro, se queda
                // flotando encima en vez de empujarlo sin parar.
                if (withinReach) Stop();
                else MoveTowards(toTarget.normalized, chaseSpeed);
                return true;
            }

            // Con el objetivo ya al alcance del morro, el sondeo de borde se ignora: el jugador
            // está justo delante, así que hay suelo donde pisar, y un empujón entre cuerpos no
            // debe leerse como precipicio y dejar al enemigo clavado a medio combate.
            if (!withinReach && !CanAdvance(_direction)) Stop();
            else Move(chaseSpeed);

            return true;
        }

        /// <summary>
        /// Busca al objetivo por etiqueta y lo cachea. Se reintenta cada pocos segundos en vez de
        /// cada frame porque el jugador de la run es un objeto persistente que puede no existir
        /// todavía cuando la sección acaba de cargarse.
        /// </summary>
        private Transform ResolveTarget()
        {
            if (_target != null) return _target;

            _retargetTimer -= Time.fixedDeltaTime;
            if (_retargetTimer > 0f) return null;

            _retargetTimer = RetargetInterval;

            if (string.IsNullOrEmpty(targetTag)) return null;

            var found = GameObject.FindGameObjectWithTag(targetTag);
            _target = found != null ? found.transform : null;
            return _target;
        }

        // ------------------------------------------------------------------ patrulla

        private void Patrol()
        {
            if (_waitTimer > 0f)
            {
                _waitTimer -= Time.fixedDeltaTime;
                Stop();
                return;
            }

            float offsetFromOrigin = transform.position.x - _origin.x;
            bool reachedEnd = (_direction > 0 && offsetFromOrigin >= patrolDistance) ||
                              (_direction < 0 && offsetFromOrigin <= -patrolDistance);

            // Dar la vuelta también en un precipicio, no sólo al final del tramo: así se puede
            // soltar el mismo enemigo en una plataforma corta sin ajustarle la distancia a mano.
            if (reachedEnd || !CanAdvance(_direction))
            {
                _direction = -_direction;
                _waitTimer = waitAtEnds;
                Stop();
                return;
            }

            Move(patrolSpeed);

            if (_sprite != null) _sprite.flipX = _direction < 0;
        }

        // ------------------------------------------------------------------ movimiento

        private void Move(float speed)
        {
            var velocity = _body.linearVelocity;
            velocity.x = _direction * speed;
            // Un volador no tiene gravedad que lo baje: mantiene su altura anulando la deriva.
            if (canFly) velocity.y = 0f;
            _body.linearVelocity = velocity;
        }

        /// <summary>Mueve en cualquier dirección (sólo lo usa el enemigo volador al perseguir).</summary>
        private void MoveTowards(Vector2 direction, float speed)
        {
            _body.linearVelocity = direction * speed;
        }

        /// <summary>
        /// True si hay suelo un paso por delante en la dirección dada.
        ///
        /// El rayo sale por delante del morro pero desde la altura del CENTRO del cuerpo (no de
        /// los pies) y es largo: <c>media altura + ledgeProbeDepth</c>. Así, un empujón que
        /// levante al bicho unos centímetros —lo normal al chocar con el jugador— no se lee como
        /// precipicio y lo deja parado a medio combate.
        ///
        /// Los impactos contra uno mismo y contra el objetivo se descartan: que el jugador esté
        /// delante (comparte capa con el suelo) no significa que haya plataforma.
        /// </summary>
        private bool CanAdvance(int direction)
        {
            // Un volador no se cae por un precipicio; el sondeo de suelo no le aplica.
            if (canFly || !stopAtLedges || _collider == null) return true;

            var bounds = _collider.bounds;
            var origin = new Vector2(
                direction > 0 ? bounds.max.x + LedgeProbeInset : bounds.min.x - LedgeProbeInset,
                bounds.center.y);
            float length = bounds.extents.y + Mathf.Max(0.15f, ledgeProbeDepth);

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayers, useTriggers = false };
            int count = Physics2D.Raycast(origin, Vector2.down, filter, GroundHits, length);

            for (int i = 0; i < count; i++)
            {
                var col = GroundHits[i].collider;
                if (col == null || col == _collider || col.transform.IsChildOf(transform)) continue;
                if (_target != null && col.transform.IsChildOf(_target.root)) continue;
                return true;
            }

            return false;
        }

        private void Stop()
        {
            var velocity = _body.linearVelocity;
            velocity.x = 0f;
            // El volador se queda flotando quieto; el de suelo conserva su caída/gravedad.
            if (canFly) velocity.y = 0f;
            _body.linearVelocity = velocity;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (contactDamage <= 0f || _contactTimer > 0f || !_active) return;

            var otherHealth = collision.collider.GetComponentInParent<Health>();
            if (otherHealth == null || otherHealth == _health) return;

            // Sólo daña al objetivo. Se comprueba sobre el GameObject del Health (la raíz del
            // jugador, que es la que lleva la etiqueta) y no sobre el collider tocado, que puede
            // ser un hijo sin etiquetar. Sin esto, dos enemigos que se rozan se matan entre ellos.
            if (!string.IsNullOrEmpty(targetTag) && !otherHealth.CompareTag(targetTag)) return;

            // El empujón aleja al objetivo de este cuerpo, y sólo si el golpe entra de verdad
            // (los i-frames del jugador se comen los contactos repetidos, y con ellos el empujón).
            otherHealth.TakeDamage(contactDamage, transform.position, contactKnockbackMultiplier);
            _contactTimer = contactDamageCooldown;
        }

        private void OnDied()
        {
            _active = false;
            Stop();

            _sound?.Play("OnDeath");

            // El aspecto del cadáver y su desaparición son cosa de Corpse. Sin ese componente se
            // mantiene el tinte de siempre, para que un enemigo suelto siga viéndose muerto.
            if (_sprite != null && GetComponent<Corpse>() == null)
                _sprite.color = new Color(0.35f, 0.1f, 0.12f, 0.5f);

            foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;
            _body.simulated = false;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = Application.isPlaying ? (Vector3)_origin : transform.position;

            // Tramo de patrulla.
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin + Vector3.left * patrolDistance, origin + Vector3.right * patrolDistance);

            if (!chasePlayer) return;

            // Radios de detección y de abandono, como cajas: la detección es horizontal pero está
            // limitada en vertical por verticalTolerance, y una esfera mentiría sobre su forma.
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(transform.position,
                                new Vector3(detectionRadius * 2f, verticalTolerance * 2f, 0f));

            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.3f);
            Gizmos.DrawWireCube(transform.position,
                                new Vector3(Mathf.Max(loseSightRadius, detectionRadius) * 2f,
                                            verticalTolerance * 2f, 0f));
        }
    }
}
