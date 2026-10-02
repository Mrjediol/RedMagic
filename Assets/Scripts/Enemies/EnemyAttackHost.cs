using System;
using System.Collections;
using RedMagic.Abilities;
using RedMagic.Bosses;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// Lo que hace que un enemigo normal pueda lanzar <b>ataques del catálogo</b> (los
    /// <see cref="BossAttack"/> de los jefes): es su <see cref="IAttackHost"/>. Monta el
    /// <see cref="BossContext"/> de cada lanzamiento y lleva el ciclo <b>aviso → ataque →
    /// recuperación</b> de un ataque, igual que <see cref="BossController"/> con los suyos.
    ///
    /// No decide nada: <b>cuándo</b> se ataca lo dice el cerebro y <b>cuál</b> la baraja de
    /// <see cref="EnemyTuning.attacks"/> (ver <see cref="EnemyAttack"/>). Aquí sólo viven los
    /// visuales compartidos por los ataques (avisos, ondas…) y el tamaño de su "arena".
    ///
    /// Si un enemigo con baraja no lo lleva, <see cref="EnemyAttack"/> lo añade al arrancar con los
    /// placeholders en código; ponerlo en el prefab sirve para darle arte de verdad a los avisos.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyAttackHost : MonoBehaviour, IAttackHost
    {
        [Header("Alcance de los ataques")]
        [Tooltip("Media anchura de la zona que cubren sus ataques, centrada en el enemigo. Marca " +
                 "hasta dónde llegan las ondas, los barridos y la lluvia de proyectiles.")]
        [Min(1f)]
        [SerializeField] private float arenaHalfWidth = 10f;

        [Tooltip("Altura útil sobre el suelo (de dónde cae la lluvia de proyectiles).")]
        [Min(1f)]
        [SerializeField] private float arenaHeight = 8f;

        [Tooltip("Los pies: un vacío hijo de la raíz, en la parte baja del cuerpo. Los ataques miden " +
                 "sus alturas desde ahí. Vacío = la base del collider (la raíz va en el centro del cuerpo).")]
        [SerializeField] private Transform feet;

        [Header("Placeholders de FX (prefab → arte real)")]
        [Tooltip("Sprite base de los efectos. Vacío = el cuadrado de AbilityFx.")]
        [SerializeField] private Sprite fxSprite;

        [Tooltip("Prefab del aviso/telegrafiado. Vacío = el cuadrado de AbilityFx.Flash.")]
        [SerializeField] private GameObject warnPrefab;

        [Tooltip("Prefab del aviso de ÁREA en el suelo (círculo). Vacío = franja plana de color.")]
        [SerializeField] private GameObject warnCirclePrefab;

        [Tooltip("Prefab del aviso de TRAYECTORIA (flecha, arte mirando +X). Vacío = barra de color.")]
        [SerializeField] private GameObject warnArrowPrefab;

        [Tooltip("Prefab de la onda de choque (ShockwaveAttack). Vacío = construida en código.")]
        [SerializeField] private GameObject shockwavePrefab;

        [Tooltip("Prefab del filo giratorio (SweepBeamAttack). Vacío = construido en código.")]
        [SerializeField] private GameObject sweepBeamPrefab;

        [Tooltip("Prefab del parche de suelo peligroso. Vacío = código.")]
        [SerializeField] private GameObject hazardPrefab;

        [Tooltip("Prefab de la plataforma temporal (PlatformFloodAttack). Vacío = construida en código.")]
        [SerializeField] private GameObject platformPrefab;

        private EnemyStats _stats;
        private EnemyAnimation _animation;
        private Health _health;
        private Collider2D _collider;
        private Rigidbody2D _rigidbody;
        private EnemyAttack _attack;
        private Coroutine _routine;
        private Vector2 _aimPoint;

        // ============================================================ IAttackHost

        /// <summary>El punto que congeló el cerebro al decidir el ataque en curso.</summary>
        public Vector2? AimPoint => _routine != null ? _aimPoint : (Vector2?)null;

        /// <summary>Un golpe del ataque en curso: suena el "OnAttack" del enemigo, como con su ataque propio.</summary>
        public void OnShotFired()
        {
            if (_attack != null) _attack.RaiseAttackSound();
        }

        public GameObject WarnPrefab => warnPrefab;
        public GameObject WarnCirclePrefab => warnCirclePrefab;
        public GameObject WarnArrowPrefab => warnArrowPrefab;
        public GameObject ShockwavePrefab => shockwavePrefab;
        public GameObject SweepBeamPrefab => sweepBeamPrefab;
        public GameObject HazardPrefab => hazardPrefab;
        public GameObject PlatformPrefab => platformPrefab;

        /// <summary>Las anclas son del ritual de un jefe: un enemigo no las usa.</summary>
        public GameObject AnchorPrefab => null;

        public IAttackBody Body => _animation != null ? _animation : null;

        /// <summary>El cerebro no gira al enemigo mientras ataca, así que aquí no hay nada que bloquear.</summary>
        public bool FacingLocked { get; set; }

        public float FeetHeight
        {
            get
            {
                if (feet != null) return feet.position.y - transform.position.y;
                return _collider != null ? _collider.bounds.min.y - transform.position.y : 0f;
            }
        }

        public Vector2 FeetPosition => new Vector2(transform.position.x, transform.position.y + FeetHeight);

        /// <summary>
        /// De dónde sale el ataque en un cuerpo por partes: el punto de la suelta del gesto en curso
        /// (<c>RigRelease.point</c>) o, si no dice nada, la boca del rig. Sin rig, los pies.
        /// </summary>
        public Vector2 CastPoint
        {
            get
            {
                if (!_rigLooked) { _rig = GetComponent<RedMagic.Rigging.ProceduralRig>(); _rigLooked = true; }
                return _rig != null ? _rig.CastPosition : FeetPosition;
            }
        }

        private RedMagic.Rigging.ProceduralRig _rig;
        private bool _rigLooked;

        public void MoveBody(Vector2 feetPosition)
        {
            feetPosition.y -= FeetHeight;

            // Por posición y no por MovePosition: el cerebro escribe la velocidad del cuerpo cada
            // paso de física (lo frena mientras ataca) y se comería el desplazamiento.
            if (_rigidbody != null && _rigidbody.simulated) _rigidbody.position = feetPosition;
            else transform.position = new Vector3(feetPosition.x, feetPosition.y, transform.position.z);
        }

        // ============================================================ ciclo de un ataque

        /// <summary>True mientras hay un ataque del catálogo en marcha (aviso, golpe o recuperación).</summary>
        public bool Running => _routine != null;

        private void Awake()
        {
            _stats = GetComponent<EnemyStats>();
            _animation = GetComponent<EnemyAnimation>();
            _attack = GetComponent<EnemyAttack>();
            _health = GetComponent<Health>();
            _collider = GetComponent<Collider2D>();
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            _routine = null;
            FacingLocked = false;
            if (_health != null) _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;
            Cancel();
        }

        private void OnDied()
        {
            Cancel();

            // Lo que dejó en el suelo se va con él, como con un jefe.
            BossHazard.ClearFrom(this);
        }

        /// <summary>
        /// Lanza <paramref name="attack"/> contra <paramref name="target"/>: aviso (con su gesto, si el
        /// cuerpo lo tiene) → ataque → recuperación, y avisa con <paramref name="finished"/>.
        /// <paramref name="aimPoint"/> es el punto que congeló el cerebro al decidir atacar.
        /// <paramref name="lead"/> = segundos de aviso en vez del <c>telegraph</c> del ataque (un ataque
        /// lanzado desde una defensa: lo que dura apartarse); negativo = el suyo.
        /// </summary>
        public void Run(BossAttack attack, Transform target, Vector2 aimPoint, Action finished, float lead = -1f)
        {
            Cancel();

            if (attack == null || !isActiveAndEnabled)
            {
                finished?.Invoke();
                return;
            }

            _aimPoint = aimPoint;
            _routine = StartCoroutine(Routine(attack, target, aimPoint, finished, lead));
        }

        private IEnumerator Routine(BossAttack attack, Transform target, Vector2 aimPoint, Action finished, float lead)
        {
            float seconds = lead >= 0f ? Mathf.Max(0.01f, lead) : attack.Telegraph;
            attack.OnTelegraph(BuildContext(attack, target, aimPoint));

            // --- aviso: el gesto del cuerpo si existe (el ataque arranca en su frame de suelta),
            // o el tiempo de 'telegraph' a secas.
            var body = Body;
            if (body != null) body.AimAt(aimPoint);
            if (body != null && body.PlayGesture(attack.Gesture, seconds))
            {
                float timeout = Mathf.Max(seconds, body.SecondsToRelease) + 0.5f;
                while (!body.Released && timeout > 0f)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(seconds);
            }

            // --- ataque, con el contexto del momento (el enemigo pudo salir despedido en el aviso).
            yield return attack.Run(BuildContext(attack, target, aimPoint));

            // --- recuperación: quieto. La pausa hasta el siguiente la pone 'attackCooldown'.
            yield return new WaitForSeconds(attack.Recovery);

            _routine = null;
            Settle();
            finished?.Invoke();
        }

        private void Cancel()
        {
            if (_routine == null) return;

            StopCoroutine(_routine);
            _routine = null;
            Settle();
        }

        /// <summary>Lo que un ataque (acabado o cortado) pudiera dejar puesto en el cuerpo.</summary>
        private void Settle()
        {
            FacingLocked = false;
            if (_animation != null) _animation.EndGesture();
        }

        private BossContext BuildContext(BossAttack attack, Transform target, Vector2 aimPoint)
        {
            var tuning = _stats.Tuning;
            Vector2 origin = transform.position;

            int facing = aimPoint.x >= origin.x ? 1 : -1;
            Vector2 delta = aimPoint - origin;
            Vector2 aim = delta.sqrMagnitude < 0.0001f ? new Vector2(facing, 0f) : delta.normalized;

            var ability = new AbilityContext(gameObject, this, _health, tuning.hitLayers, facing, aim,
                                             CompareTag("Untagged") ? null : tag);

            // Sin fases: el ritmo es 1 y el color de los avisos es el del propio ataque.
            return new BossContext(this, target, ability, MeasureGroundY(tuning), arenaHalfWidth,
                                   arenaHeight, 1f, fxSprite, attack.Accent);
        }

        /// <summary>El terreno bajo los pies ahora (un enemigo se mueve: no vale medirlo una vez).</summary>
        private float MeasureGroundY(EnemyTuning tuning)
        {
            Vector2 feetNow = FeetPosition;
            var hit = Physics2D.Raycast(feetNow + Vector2.up * 0.35f, Vector2.down, arenaHeight + 30f,
                                        tuning.obstacleLayers);
            return hit.collider != null ? hit.point.y : feetNow.y;
        }

        private void OnDrawGizmosSelected()
        {
            var stats = GetComponent<EnemyStats>();
            if (stats == null || !stats.Tuning.HasDeck) return;

            Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.6f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * (arenaHeight * 0.5f - 1f),
                                new Vector3(arenaHalfWidth * 2f, arenaHeight, 0f));
        }
    }
}
