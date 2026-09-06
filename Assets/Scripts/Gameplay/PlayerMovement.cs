using System.Collections.Generic;
using System.Linq;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Controlador de plataformas 2D. La lógica de movimiento (física, sensación del salto y
    /// detección de suelo) es un port del controlador de Tarodev: <b>no usa la física del
    /// Rigidbody2D</b>, sino que integra su propia velocidad, mueve el <c>transform</c> y resuelve
    /// las colisiones con raycasts contra la capa de suelo.
    ///
    /// Características de la sensación del salto que se conservan tal cual:
    ///  - aceleración / deceleración hacia una velocidad máxima con tope por frame,
    ///  - bono de velocidad horizontal en el ápex del salto,
    ///  - velocidad de caída modulada por el ápex (cae más despacio cerca del punto más alto),
    ///  - altura de salto variable (soltar el botón antes = más gravedad),
    ///  - coyote time y jump buffer.
    ///
    /// Sobre el original se ha adaptado:
    ///  - <b>Entrada</b>: en vez de <c>UnityEngine.Input</c>, lee el asset de Input Actions
    ///    (teclado + mando por las mismas acciones) y además los botones táctiles vía
    ///    <see cref="TouchInput"/>. Las tres fuentes conviven; gana la de mayor magnitud.
    ///  - <b>Pausa / muerte</b>: respeta <see cref="GameStateManager.CanPlayerAct"/> y el evento
    ///    <see cref="Health.Died"/>.
    ///  - <b>Audio</b>: los SFX salen por <see cref="AudioManager"/> por id (salto, pasos, pisotón).
    ///
    /// El Rigidbody2D se mantiene (forzado a Kinematic) sólo para que sigan llegando los eventos
    /// de colisión / trigger a los enemigos, proyectiles y zonas de daño.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class PlayerMovement : MonoBehaviour
    {
        // ---------------------------------------------------------------- tipos internos

        private struct FrameInput
        {
            public float X;
            public bool JumpDown;
            public bool JumpUp;
        }

        private struct RayRange
        {
            public RayRange(float x1, float y1, float x2, float y2, Vector2 dir)
            {
                Start = new Vector2(x1, y1);
                End = new Vector2(x2, y2);
                Dir = dir;
            }

            public readonly Vector2 Start, End, Dir;
        }

        // ---------------------------------------------------------------- estado público (hooks externos)

        public Vector3 Velocity { get; private set; }
        public Vector3 RawMovement { get; private set; }
        public bool JumpingThisFrame { get; private set; }
        public bool LandingThisFrame { get; private set; }
        public bool IsGrounded => _colDown;

        // ---------------------------------------------------------------- Input Actions + táctil

        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";

        // ---------------------------------------------------------------- SFX

        [Header("SFX — ids de sonido del AudioManager")]
        [SerializeField] private string jumpSfxId = "SFX_PlayerJump";
        [Tooltip("Pasos al correr por el suelo. Déjalo vacío para desactivarlos.")]
        [SerializeField] private string footstepSfxId = "SFX_PlayerFootstep";
        [SerializeField] private float footstepInterval = 0.32f;
        [Tooltip("Sonido al pisar a un enemigo. Déjalo vacío para no sonar.")]
        [SerializeField] private string stompSfxId = "SFX_EnemyStomp";

        // ---------------------------------------------------------------- pisotón

        [Header("Pisotón a enemigos")]
        [Tooltip("Si está activo, caer sobre algo con Health por encima lo mata y rebota.")]
        [SerializeField] private bool enableStomp = true;
        [Tooltip("Daño aplicado al enemigo pisado (0 o menos = matar directamente).")]
        [SerializeField] private float stompDamage = 0f;

        // ---------------------------------------------------------------- colisión

        [Header("COLLISION")]
        [Tooltip("Caja del personaje para los sensores. Si se deja a cero se toma del Collider2D.")]
        [SerializeField] private Bounds _characterBounds = new Bounds(Vector3.zero, new Vector3(0.7f, 1.5f, 0f));
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private int _detectorCount = 3;
        [SerializeField] private float _detectionRayLength = 0.1f;
        [SerializeField] [Range(0.1f, 0.3f)] private float _rayBuffer = 0.1f;

        // ---------------------------------------------------------------- caminar

        [Header("WALKING")]
        [SerializeField] private float _acceleration = 90f;
        [SerializeField] private float _moveClamp = 13f;
        [SerializeField] private float _deAcceleration = 60f;
        [SerializeField] private float _apexBonus = 2f;

        // ---------------------------------------------------------------- gravedad

        [Header("GRAVITY")]
        [SerializeField] private float _fallClamp = -40f;
        [SerializeField] private float _minFallSpeed = 80f;
        [SerializeField] private float _maxFallSpeed = 120f;

        // ---------------------------------------------------------------- salto

        [Header("JUMPING")]
        [SerializeField] private float _jumpHeight = 30f;
        [SerializeField] private float _jumpApexThreshold = 10f;
        [SerializeField] private float _coyoteTimeThreshold = 0.1f;
        [SerializeField] private float _jumpBuffer = 0.1f;
        [SerializeField] private float _jumpEndEarlyGravityModifier = 3f;

        // ---------------------------------------------------------------- movimiento

        [Header("MOVE")]
        [SerializeField]
        [Tooltip("Sube la precisión de colisión a costa de rendimiento.")]
        private int _freeColliderIterations = 10;

        // ---------------------------------------------------------------- privados

        private Rigidbody2D _body;
        private Collider2D _collider;
        private SpriteRenderer _sprite;
        private Health _health;

        private InputAction _moveAction;
        private InputAction _jumpAction;

        private FrameInput _input;
        private Vector3 _lastPosition;
        private float _currentHorizontalSpeed, _currentVerticalSpeed;
        private bool _controlEnabled = true;
        private float _footstepTimer;

        // Hack del original: los colliders no están del todo listos al arrancar.
        private bool _active;

        private RayRange _raysUp, _raysRight, _raysDown, _raysLeft;
        private bool _colUp, _colRight, _colDown, _colLeft;
        private float _timeLeftGrounded;

        // salto
        private bool _coyoteUsable;
        private bool _endedJumpEarly = true;
        private float _apexPoint;   // 1 en el ápex del salto
        private float _fallSpeed;
        private float _lastJumpPressed = float.MinValue;

        private bool CanUseCoyote => _coyoteUsable && !_colDown && _timeLeftGrounded + _coyoteTimeThreshold > Time.time;
        private bool HasBufferedJump => _colDown && _lastJumpPressed + _jumpBuffer > Time.time;

        // ================================================================ ciclo de vida

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            _health = GetComponent<Health>();

            // El controlador mueve el transform: el Rigidbody2D sólo se mantiene para que sigan
            // llegando los callbacks de colisión / trigger.
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.useFullKinematicContacts = true;
            _body.freezeRotation = true;

            if (_characterBounds.size == Vector3.zero && _collider != null)
                _characterBounds = new Bounds(_collider.offset, _collider.bounds.size);

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                if (map != null)
                {
                    _moveAction = map.FindAction(moveActionName, throwIfNotFound: false);
                    _jumpAction = map.FindAction(jumpActionName, throwIfNotFound: false);
                }
                else
                {
                    Debug.LogWarning($"[PlayerMovement] No existe el action map '{actionMapName}' en {inputActions.name}.", this);
                }
            }

            _lastPosition = transform.position;
            Invoke(nameof(Activate), 0.5f);
        }

        private void Activate() => _active = true;

        private void OnEnable()
        {
            _moveAction?.Enable();
            _jumpAction?.Enable();

            if (_health != null) _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _jumpAction?.Disable();

            if (_health != null) _health.Died -= OnDied;
        }

        private void Update()
        {
            if (!_active) return;

            // Velocidad real a partir del desplazamiento del frame anterior (la usa el cálculo del ápex).
            Velocity = (transform.position - _lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastPosition = transform.position;

            // Con un menú abierto (Time.timeScale == 0) o tras morir no se actúa.
            if (!_controlEnabled || !GameStateManager.CanPlayerAct || Time.timeScale == 0f)
            {
                TouchInput.ConsumeJump();
                _input = default;
                _lastJumpPressed = float.MinValue;
                _currentHorizontalSpeed = 0f;
                return;
            }

            GatherInput();
            RunCollisionChecks();

            CalculateWalk();      // horizontal
            CalculateJumpApex();  // afecta a la caída: antes de la gravedad
            CalculateGravity();   // vertical
            CalculateJump();      // puede sobreescribir la vertical

            UpdateFootsteps();
            MoveCharacter();      // aplica el movimiento
        }

        // ================================================================ entrada

        private void GatherInput()
        {
            // Teclado + mando por el asset de acciones…
            float axis = _moveAction != null ? _moveAction.ReadValue<Vector2>().x : 0f;

            // …más los botones táctiles. Se queda con el de mayor magnitud.
            float touch = TouchInput.Horizontal;
            float x = Mathf.Abs(touch) > Mathf.Abs(axis) ? touch : axis;

            bool jumpDown = (_jumpAction != null && _jumpAction.WasPressedThisFrame()) || TouchInput.ConsumeJump();
            bool jumpUp = _jumpAction != null && _jumpAction.WasReleasedThisFrame();

            _input = new FrameInput
            {
                X = Mathf.Clamp(x, -1f, 1f),
                JumpDown = jumpDown,
                JumpUp = jumpUp
            };

            if (_input.JumpDown) _lastJumpPressed = Time.time;

            if (_sprite != null && Mathf.Abs(_input.X) > 0.01f)
                _sprite.flipX = _input.X < 0f;
        }

        // ================================================================ colisiones (sensores)

        private void RunCollisionChecks()
        {
            CalculateRayRanged();

            LandingThisFrame = false;
            var groundedCheck = RunDetection(_raysDown);

            if (_colDown && !groundedCheck) _timeLeftGrounded = Time.time; // acaba de despegar
            else if (!_colDown && groundedCheck)
            {
                _coyoteUsable = true;   // acaba de tocar suelo
                LandingThisFrame = true;
            }

            _colDown = groundedCheck;

            _colUp = RunDetection(_raysUp);
            _colLeft = RunDetection(_raysLeft);
            _colRight = RunDetection(_raysRight);

            bool RunDetection(RayRange range) =>
                EvaluateRayPositions(range).Any(point =>
                    Physics2D.Raycast(point, range.Dir, _detectionRayLength, _groundLayer));
        }

        private void CalculateRayRanged()
        {
            var b = new Bounds(transform.position, _characterBounds.size);

            _raysDown = new RayRange(b.min.x + _rayBuffer, b.min.y, b.max.x - _rayBuffer, b.min.y, Vector2.down);
            _raysUp = new RayRange(b.min.x + _rayBuffer, b.max.y, b.max.x - _rayBuffer, b.max.y, Vector2.up);
            _raysLeft = new RayRange(b.min.x, b.min.y + _rayBuffer, b.min.x, b.max.y - _rayBuffer, Vector2.left);
            _raysRight = new RayRange(b.max.x, b.min.y + _rayBuffer, b.max.x, b.max.y - _rayBuffer, Vector2.right);
        }

        private IEnumerable<Vector2> EvaluateRayPositions(RayRange range)
        {
            for (var i = 0; i < _detectorCount; i++)
            {
                var t = (float)i / (_detectorCount - 1);
                yield return Vector2.Lerp(range.Start, range.End, t);
            }
        }

        // ================================================================ caminar

        private void CalculateWalk()
        {
            if (_input.X != 0)
            {
                _currentHorizontalSpeed += _input.X * _acceleration * Time.deltaTime;
                _currentHorizontalSpeed = Mathf.Clamp(_currentHorizontalSpeed, -_moveClamp, _moveClamp);

                // Bono en el ápex del salto.
                var apexBonus = Mathf.Sign(_input.X) * _apexBonus * _apexPoint;
                _currentHorizontalSpeed += apexBonus * Time.deltaTime;
            }
            else
            {
                _currentHorizontalSpeed = Mathf.MoveTowards(_currentHorizontalSpeed, 0, _deAcceleration * Time.deltaTime);
            }

            if ((_currentHorizontalSpeed > 0 && _colRight) || (_currentHorizontalSpeed < 0 && _colLeft))
                _currentHorizontalSpeed = 0;
        }

        // ================================================================ gravedad

        private void CalculateGravity()
        {
            if (_colDown)
            {
                if (_currentVerticalSpeed < 0) _currentVerticalSpeed = 0;
            }
            else
            {
                var fallSpeed = _endedJumpEarly && _currentVerticalSpeed > 0
                    ? _fallSpeed * _jumpEndEarlyGravityModifier
                    : _fallSpeed;

                _currentVerticalSpeed -= fallSpeed * Time.deltaTime;

                if (_currentVerticalSpeed < _fallClamp) _currentVerticalSpeed = _fallClamp;
            }
        }

        // ================================================================ salto

        private void CalculateJumpApex()
        {
            if (!_colDown)
            {
                _apexPoint = Mathf.InverseLerp(_jumpApexThreshold, 0, Mathf.Abs(Velocity.y));
                _fallSpeed = Mathf.Lerp(_minFallSpeed, _maxFallSpeed, _apexPoint);
            }
            else
            {
                _apexPoint = 0;
            }
        }

        private void CalculateJump()
        {
            if ((_input.JumpDown && CanUseCoyote) || HasBufferedJump)
                Jump();
            else
                JumpingThisFrame = false;

            if (!_colDown && _input.JumpUp && !_endedJumpEarly && Velocity.y > 0)
                _endedJumpEarly = true;

            if (_colUp && _currentVerticalSpeed > 0) _currentVerticalSpeed = 0;
        }

        private void Jump()
        {
            _currentVerticalSpeed = _jumpHeight;
            _endedJumpEarly = false;
            _coyoteUsable = false;
            _timeLeftGrounded = float.MinValue;
            _lastJumpPressed = float.MinValue;
            JumpingThisFrame = true;

            PlaySfx(jumpSfxId);
        }

        // ================================================================ pasos

        private void UpdateFootsteps()
        {
            if (string.IsNullOrWhiteSpace(footstepSfxId)) return;

            if (!_colDown || Mathf.Abs(_currentHorizontalSpeed) < 0.5f)
            {
                _footstepTimer = 0f;
                return;
            }

            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer > 0f) return;

            _footstepTimer = footstepInterval;
            PlaySfx(footstepSfxId);
        }

        // ================================================================ mover

        private void MoveCharacter()
        {
            var pos = transform.position;
            RawMovement = new Vector3(_currentHorizontalSpeed, _currentVerticalSpeed);
            var move = RawMovement * Time.deltaTime;
            var furthestPoint = pos + move;

            var hit = Physics2D.OverlapBox(furthestPoint, _characterBounds.size, 0, _groundLayer);
            if (!hit)
            {
                transform.position += move;
                return;
            }

            var positionToMoveTo = transform.position;
            for (int i = 1; i < _freeColliderIterations; i++)
            {
                var t = (float)i / _freeColliderIterations;
                var posToTry = Vector2.Lerp(pos, furthestPoint, t);

                if (Physics2D.OverlapBox(posToTry, _characterBounds.size, 0, _groundLayer))
                {
                    transform.position = positionToMoveTo;

                    if (i == 1)
                    {
                        if (_currentVerticalSpeed < 0) _currentVerticalSpeed = 0;
                        var dir = transform.position - hit.transform.position;
                        transform.position += dir.normalized * move.magnitude;
                    }

                    return;
                }

                positionToMoveTo = posToTry;
            }
        }

        // ================================================================ pisotón / contacto

        private void OnTriggerEnter2D(Collider2D other) => HandleContact(other);

        private void OnCollisionEnter2D(Collision2D collision) => HandleContact(collision.collider);

        private void HandleContact(Collider2D other)
        {
            if (!enableStomp || other == null || !_active) return;

            var otherHealth = other.GetComponentInParent<Health>();
            if (otherHealth == null || otherHealth == _health || otherHealth.IsDead) return;

            // Sólo cuenta como pisotón si vamos cayendo y por encima del objetivo.
            bool fromAbove = _currentVerticalSpeed < 0f &&
                             transform.position.y + _characterBounds.center.y > other.bounds.center.y;
            if (!fromAbove) return;

            if (stompDamage > 0f) otherHealth.TakeDamage(stompDamage);
            else otherHealth.Die();

            Jump();                 // rebote
            PlaySfx(stompSfxId);
        }

        // ================================================================ varios

        private void OnDied()
        {
            _controlEnabled = false;
            _input = default;
            _currentHorizontalSpeed = 0f;
            _currentVerticalSpeed = 0f;
        }

        private static void PlaySfx(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(id);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + _characterBounds.center, _characterBounds.size);

            if (!Application.isPlaying)
            {
                CalculateRayRanged();
                Gizmos.color = Color.blue;
                foreach (var range in new List<RayRange> { _raysUp, _raysRight, _raysDown, _raysLeft })
                    foreach (var point in EvaluateRayPositions(range))
                        Gizmos.DrawRay(point, range.Dir * _detectionRayLength);
                return;
            }

            Gizmos.color = Color.red;
            var futureMove = new Vector3(_currentHorizontalSpeed, _currentVerticalSpeed) * Time.deltaTime;
            Gizmos.DrawWireCube(transform.position + _characterBounds.center + futureMove, _characterBounds.size);
        }
    }
}
