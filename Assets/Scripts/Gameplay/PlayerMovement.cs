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
    public class PlayerMovement : MonoBehaviour, IKnockbackReceiver
    {
        // ---------------------------------------------------------------- tipos internos

        private struct FrameInput
        {
            public float X;
            public float Y;
            public bool JumpDown;
            public bool JumpUp;
            public bool CrouchHeld;
            public bool DashDown;
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

        /// <summary>Normal de la superficie bajo los pies. (0,1) en plano.</summary>
        public Vector2 GroundNormal => _groundNormal;

        /// <summary>Inclinación de esa superficie en grados. 0 = plano.</summary>
        public float SlopeAngle => _slopeAngle;

        /// <summary>True apoyado en una rampa transitable (0 &lt; ángulo ≤ <c>maxSlopeAngle</c>).</summary>
        public bool IsOnSlope => _onWalkableSlope;

        /// <summary>True si el suelo bajo los pies es una plataforma de un solo sentido.</summary>
        public bool IsOnPlatform => _onPlatform;

        /// <summary>True mientras se está atravesando una plataforma hacia abajo.</summary>
        public bool IsDroppingThrough => _dropCollider != null;

        /// <summary>True mientras el jugador mantiene abajo estando en el suelo.</summary>
        public bool IsCrouching { get; private set; }

        /// <summary>-1 si mira a la izquierda, 1 si mira a la derecha. Lo usa el ataque.</summary>
        public int Facing { get; private set; } = 1;

        /// <summary>
        /// False durante el medio segundo inicial en que el controlador aún no actúa (los
        /// colliders no están listos al arrancar). Hasta entonces los sensores no son fiables.
        /// </summary>
        public bool IsActive => _active;

        /// <summary>True mientras dura un dash.</summary>
        public bool IsDashing => _dashTimer > 0f;

        /// <summary>True mientras el jugador sale despedido por un golpe (no controla).</summary>
        public bool IsKnockedBack => _knockbackTimer > 0f;

        /// <summary>Se dispara al usar un salto en el aire (doble salto). Lo usa el VFX de los pies.</summary>
        public event System.Action AirJumped;

        /// <summary>Se dispara al iniciar un dash.</summary>
        public event System.Action<int> Dashed;

        // ---------------------------------------------------------------- Input Actions + táctil

        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string dashActionName = "Dash";

        // ---------------------------------------------------------------- SFX

        [Header("SFX — ids de sonido del AudioManager")]
        [SerializeField] private string jumpSfxId = "SFX_PlayerJump";
        [Tooltip("Pasos al correr por el suelo. Déjalo vacío para desactivarlos.")]
        [SerializeField] private string footstepSfxId = "SFX_PlayerFootstep";
        [SerializeField] private float footstepInterval = 0.32f;
        [Tooltip("Sonido al pisar a un enemigo. Déjalo vacío para no sonar.")]
        [SerializeField] private string stompSfxId = "SFX_EnemyStomp";
        [Tooltip("Sonido del dash. Déjalo vacío para no sonar.")]
        [SerializeField] private string dashSfxId = "SFX_PlayerDash";

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

        // ---------------------------------------------------------------- rampas

        [Header("SLOPES — rampas")]
        [Tooltip("Permite caminar por terreno irregular siguiendo la superficie. Apagado, el " +
                 "controlador se comporta exactamente como antes (sólo suelo plano).")]
        [SerializeField] private bool enableSlopes = true;

        [Tooltip("Inclinación máxima que se puede subir. Por encima, la rampa actúa como pared.")]
        [Range(0f, 80f)]
        [SerializeField] private float maxSlopeAngle = 45f;

        [Tooltip("Longitud del rayo que lee la normal del suelo. Debe ser mayor que " +
                 "'Detection Ray Length' para ver la rampa antes de pisarla.")]
        [SerializeField] private float slopeCheckDistance = 0.35f;

        [Tooltip("Cuánto se sigue considerando 'en el suelo' al bajar una rampa. Es lo que evita " +
                 "el rebote: al descender los pies se despegan un instante cada frame.")]
        [SerializeField] private float slopeSnapDistance = 0.35f;

        // ---------------------------------------------------------------- plataformas

        [Header("PLATFORMS — plataformas de un solo sentido")]
        [Tooltip("Capa del suelo atravesable: se puede saltar a través desde abajo y se pisa por " +
                 "arriba. Debe ser una capa distinta de 'Ground Layer'.")]
        [SerializeField] private LayerMask _platformLayer;

        [Tooltip("Abajo + salto deja caer al jugador a través de la plataforma que pisa.")]
        [SerializeField] private bool allowDropThrough = true;

        [Tooltip("Tope de seguridad, en segundos, de la caída a través de una plataforma. " +
                 "Normalmente NO es lo que la termina: la plataforma se suelta en cuanto el " +
                 "personaje queda por debajo de ella. Esto sólo evita quedarse ignorándola para " +
                 "siempre si eso no llega a pasar.")]
        [SerializeField] private float dropThroughDuration = 1.5f;

        // ---------------------------------------------------------------- caminar

        [Header("WALKING")]
        [SerializeField] private float _acceleration = 90f;
        [SerializeField] private float _moveClamp = 13f;
        [SerializeField] private float _deAcceleration = 60f;

        // Con los multiplicadores de items (PlayerStats). Sin items valen 1 y esto es _moveClamp tal cual.
        private float MoveClamp => _moveClamp * PlayerStats.Multiplier(PlayerStat.MoveSpeed);
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
        [Tooltip("Saltos extra disponibles en el aire. 1 = doble salto.")]
        [SerializeField] private int maxAirJumps = 1;
        [Tooltip("Altura del salto en el aire. 0 o menos = usa la misma que el salto normal.")]
        [SerializeField] private float airJumpHeight = 0f;

        [Header("DASH")]
        [SerializeField] private bool allowDash = true;
        [SerializeField] private float dashSpeed = 26f;
        [SerializeField] private float dashDuration = 0.18f;
        [SerializeField] private float dashCooldown = 0.55f;
        [Tooltip("Dashes disponibles en el aire antes de tocar suelo. 0 = sólo en el suelo.")]
        [SerializeField] private int maxAirDashes = 1;
        [Tooltip("Congela la caída durante el dash (dash horizontal puro).")]
        [SerializeField] private bool dashIgnoresGravity = true;

        // ---------------------------------------------------------------- movimiento

        [Header("MOVE")]
        [SerializeField]
        [Tooltip("Sube la precisión de colisión a costa de rendimiento.")]
        private int _freeColliderIterations = 10;

        [Header("AGACHARSE")]
        [Tooltip("Permite agacharse manteniendo abajo en el suelo (frena el movimiento horizontal).")]
        [SerializeField] private bool allowCrouch = true;

        [Header("Ajuste visual")]
        [Tooltip("Pega el personaje al suelo al aterrizar. Sin esto flota hasta " +
                 "'_detectionRayLength' porque el sensor lo da por apoyado antes de tocar.")]
        [SerializeField] private bool snapToGround = true;

        [Tooltip("Holgura que se deja al pegar al suelo. Si es 0 la caja de colisión queda " +
                 "tocando exactamente el suelo y el resolvedor de MoveCharacter da tirones.")]
        [Range(0f, 0.05f)]
        [SerializeField] private float groundSkin = 0.01f;

        [Tooltip("Cuánto se encoge la caja que usa MoveCharacter para barrer colisiones. Debe ser " +
                 "mayor que el 'Default Contact Offset' de Physics 2D (0.01), o al estar apoyado en " +
                 "el suelo se detectaría un choque falso cada frame y el personaje saldría despedido.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float solverSkin = 0.06f;

        // ---------------------------------------------------------------- privados

        private Rigidbody2D _body;
        private Collider2D _collider;
        private SpriteRenderer _sprite;
        private Health _health;

        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _dashAction;

        private int _airJumpsUsed;
        private int _airDashesUsed;
        private float _knockbackTimer;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private int _dashDirection = 1;

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

        // rampas / plataformas
        private Vector2 _groundNormal = Vector2.up;
        private float _slopeAngle;
        private float _groundDistance = float.MaxValue;
        private bool _onWalkableSlope;
        private bool _onPlatform;
        private int _steepBlockDir;   // -1 / 1: sentido cuesta arriba de una rampa no trepable
        private float _dropTimer;
        private Collider2D _dropCollider;    // la plataforma concreta que se está atravesando
        private Collider2D _groundCollider;  // la que se pisa ahora mismo

        /// <summary>
        /// Una plataforma sólo existe para el sensor de suelo cuando el personaje no sube y no
        /// está atravesándola. Fuera de eso es aire en todas las direcciones.
        /// </summary>
        private bool PlatformsActive => _platformLayer.value != 0 && _currentVerticalSpeed <= 0f;

        /// <summary>
        /// Filtro común de los impactos contra plataformas, para el sensor de suelo y para el
        /// aterrizaje. Decide qué cara cuenta como pisable y cuál se está atravesando ahora mismo.
        /// </summary>
        private bool AcceptPlatformHit(RaycastHit2D hit)
        {
            if (!hit) return false;

            // Nacer DENTRO de la plataforma (subiendo a través de ella) devuelve un impacto a
            // distancia 0: eso no es un suelo sobre el que estar.
            if (hit.distance <= 0.0001f) return false;

            // Sólo se pisa por arriba: fuera caras laterales e inferior.
            if (hit.normal.y <= 0.5f) return false;

            // Al dejarse caer se ignora LA PLATAFORMA CONCRETA que se pisaba, no una altura. En una
            // rampa la misma plataforma sigue estando más abajo, así que filtrar por altura sólo
            // hacía bajar unos centímetros y volver a aterrizar en ella. Cualquier OTRA plataforma
            // sigue frenando la caída, incluida la de justo debajo.
            if (_dropCollider != null && hit.collider == _dropCollider) return false;

            return true;
        }

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
                    _dashAction = map.FindAction(dashActionName, throwIfNotFound: false);
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
            _dashAction?.Enable();

            if (_health != null)
            {
                _health.Died += OnDied;
                _health.Revived += OnRevived;
            }
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _jumpAction?.Disable();
            _dashAction?.Disable();

            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
            }
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
                TouchInput.ConsumeDash();
                _input = default;
                _lastJumpPressed = float.MinValue;
                _currentHorizontalSpeed = 0f;
                IsCrouching = false;
                _dashTimer = 0f;
                _knockbackTimer = 0f;
                ClearDropThrough();
                return;
            }

            // Durante el retroceso no se lee la entrada: el golpe manda. La cola táctil se vacía
            // igualmente para que no se dispare un salto acumulado al recuperar el control.
            if (IsKnockedBack)
            {
                TouchInput.ConsumeJump();
                TouchInput.ConsumeDash();
                _input = default;
                IsCrouching = false;
            }
            else
            {
                GatherInput();
            }

            UpdateDropThrough();

            RunCollisionChecks();

            // Al tocar suelo se recargan los saltos y dashes aéreos.
            if (_colDown)
            {
                _airJumpsUsed = 0;
                _airDashesUsed = 0;
                ClearDropThrough();   // se ha vuelto a pisar algo: la caída a través ha terminado
            }

            if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;
            if (!IsKnockedBack) TryStartDash();

            if (IsKnockedBack)
            {
                UpdateKnockback();   // el empujón manda sobre todo lo demás
            }
            else if (_dashTimer > 0f)
            {
                UpdateDash();     // el dash manda: sustituye a andar y a la gravedad
                CalculateJump();  // …pero saltar puede cancelarlo
            }
            else
            {
                CalculateWalk();      // horizontal
                CalculateJumpApex();  // afecta a la caída: antes de la gravedad
                CalculateGravity();   // vertical
                CalculateJump();      // puede sobreescribir la vertical
            }

            SnapToGround();       // pega los pies al suelo (corrige el flotar del sensor)
            UpdateFootsteps();
            MoveCharacter();      // aplica el movimiento
        }

        // ================================================================ entrada

        private void GatherInput()
        {
            // Teclado + mando por el asset de acciones…
            Vector2 stick = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;

            // …más los botones táctiles. Se queda con el de mayor magnitud.
            float touch = TouchInput.Horizontal;
            float x = Mathf.Abs(touch) > Mathf.Abs(stick.x) ? touch : stick.x;

            bool jumpDown = (_jumpAction != null && _jumpAction.WasPressedThisFrame()) || TouchInput.ConsumeJump();
            bool jumpUp = _jumpAction != null && _jumpAction.WasReleasedThisFrame();
            bool dashDown = (_dashAction != null && _dashAction.WasPressedThisFrame()) || TouchInput.ConsumeDash();

            _input = new FrameInput
            {
                X = Mathf.Clamp(x, -1f, 1f),
                Y = Mathf.Clamp(stick.y, -1f, 1f),
                JumpDown = jumpDown,
                JumpUp = jumpUp,
                CrouchHeld = TouchInput.Crouch,
                DashDown = dashDown
            };

            if (_input.JumpDown) _lastJumpPressed = Time.time;

            if (Mathf.Abs(_input.X) > 0.01f)
            {
                Facing = _input.X < 0f ? -1 : 1;
                if (_sprite != null) _sprite.flipX = _input.X < 0f;
            }
        }

        // ================================================================ colisiones (sensores)

        private void RunCollisionChecks()
        {
            CalculateRayRanged();

            LandingThisFrame = false;
            var groundedCheck = ProbeGround();

            if (_colDown && !groundedCheck) _timeLeftGrounded = Time.time; // acaba de despegar
            else if (!_colDown && groundedCheck)
            {
                _coyoteUsable = true;   // acaba de tocar suelo
                LandingThisFrame = true;
            }

            _colDown = groundedCheck;

            _colUp = RunDetection(_raysUp, false);
            _colLeft = RunDetection(_raysLeft, true);
            _colRight = RunDetection(_raysRight, true);

            // Ojo: arriba / izquierda / derecha miran SÓLO la capa sólida. Es lo que hace que una
            // plataforma se pueda atravesar de lado y de abajo arriba sin ningún caso especial.
            //
            // De lado, una rampa transitable NO es pared: el rayo lateral más bajo la toca en cuanto
            // se empieza a subir (a 45º, a 1 cm del borde) y frenaba en seco al pie de la cuesta.
            // Por eso las rampas sólo funcionaban en la capa de plataformas, que estos rayos no ven.
            bool RunDetection(RayRange range, bool ignoreWalkable) =>
                EvaluateRayPositions(range).Any(point =>
                {
                    var hit = Physics2D.Raycast(point, range.Dir, _detectionRayLength, _groundLayer);
                    return hit && !(ignoreWalkable && IsWalkableSurface(hit));
                });
        }

        /// <summary>
        /// Cara que se puede pisar: inclinación dentro de <c>maxSlopeAngle</c>. Un impacto a distancia
        /// 0 (el rayo nació dentro del collider) no dice nada de la cara, así que cuenta como pared.
        /// </summary>
        private bool IsWalkableSurface(RaycastHit2D hit) =>
            enableSlopes && hit.distance > 0f && Vector2.Angle(hit.normal, Vector2.up) <= maxSlopeAngle;

        /// <summary>
        /// Sensor de suelo. Además de decir si hay apoyo, deja anotada la normal de la superficie
        /// (para el movimiento en rampa), la distancia exacta (para <see cref="SnapToGround"/>),
        /// si el apoyo es una plataforma de un solo sentido y el sentido bloqueado por una rampa
        /// demasiado inclinada.
        /// </summary>
        private bool ProbeGround()
        {
            _groundNormal = Vector2.up;
            _slopeAngle = 0f;
            _groundDistance = float.MaxValue;
            _onWalkableSlope = false;
            _onPlatform = false;
            _groundCollider = null;
            _steepBlockDir = 0;

            // Al bajar una rampa los pies se despegan un instante en cada frame. Mientras se venía
            // apoyado y no se sube, el sensor mira más abajo para no perder el suelo; SnapToGround
            // vuelve a pegarlo. Sin esto el descenso son microsaltos.
            bool descending = enableSlopes && _colDown && _currentVerticalSpeed <= 0f;
            float groundedRange = descending ? Mathf.Max(_detectionRayLength, slopeSnapDistance) : _detectionRayLength;
            float probeRange = Mathf.Max(groundedRange, enableSlopes ? slopeCheckDistance : 0f);

            bool platformsActive = PlatformsActive;
            float bestDistance = float.MaxValue;
            Vector2 bestNormal = Vector2.up;
            bool bestIsPlatform = false;
            Collider2D bestCollider = null;
            float steepestAngle = 0f;
            Vector2 steepestNormal = Vector2.zero;

            foreach (var point in EvaluateRayPositions(_raysDown))
            {
                Consider(Physics2D.Raycast(point, Vector2.down, probeRange, _groundLayer), false);

                if (platformsActive)
                {
                    var plat = Physics2D.Raycast(point, Vector2.down, probeRange, _platformLayer);
                    if (AcceptPlatformHit(plat)) Consider(plat, true);
                }
            }

            bool grounded = bestDistance <= groundedRange;
            if (grounded)
            {
                _groundDistance = bestDistance;
                _groundNormal = bestNormal;
                _slopeAngle = Vector2.Angle(bestNormal, Vector2.up);
                _onPlatform = bestIsPlatform;
                _groundCollider = bestCollider;
                _onWalkableSlope = enableSlopes && _slopeAngle > 0.5f;
            }

            if (enableSlopes && steepestNormal != Vector2.zero)
                _steepBlockDir = steepestNormal.x > 0f ? -1 : 1;   // cuesta arriba = contra la normal

            return grounded;

            void Consider(RaycastHit2D hit, bool isPlatform)
            {
                if (!hit) return;

                float angle = Vector2.Angle(hit.normal, Vector2.up);

                if (enableSlopes && angle > maxSlopeAngle)
                {
                    // Rampa no trepable: no es suelo. Sólo se anota para bloquear el avance, y sólo
                    // si está de verdad bajo los pies (si no, una pared a media pantalla frenaría).
                    if (hit.distance <= groundedRange && angle > steepestAngle)
                    {
                        steepestAngle = angle;
                        steepestNormal = hit.normal;
                    }
                    return;
                }

                if (hit.distance >= bestDistance) return;

                bestDistance = hit.distance;
                bestNormal = hit.normal;
                bestIsPlatform = isPlatform;
                bestCollider = hit.collider;
            }
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
            // Agacharse: sólo en el suelo, y frena el avance horizontal.
            IsCrouching = allowCrouch && _colDown && (_input.Y < -0.5f || _input.CrouchHeld);
            if (IsCrouching)
            {
                _currentHorizontalSpeed = Mathf.MoveTowards(_currentHorizontalSpeed, 0, _deAcceleration * Time.deltaTime);
                return;
            }

            if (_input.X != 0)
            {
                _currentHorizontalSpeed += _input.X * _acceleration * PlayerStats.Multiplier(PlayerStat.MoveSpeed) * Time.deltaTime;
                _currentHorizontalSpeed = Mathf.Clamp(_currentHorizontalSpeed, -MoveClamp, MoveClamp);

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

            BlockAgainstSteepSlope();
        }

        /// <summary>
        /// Una rampa de más de <c>maxSlopeAngle</c> se comporta como pared: no se trepa. Los rayos
        /// laterales ya frenan contra un muro alto; esto cubre las cuñas bajas, que sólo tocan los
        /// rayos de abajo.
        /// </summary>
        private void BlockAgainstSteepSlope()
        {
            if (_steepBlockDir == 0 || _currentHorizontalSpeed == 0f) return;
            if ((int)Mathf.Sign(_currentHorizontalSpeed) == _steepBlockDir) _currentHorizontalSpeed = 0f;
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
            // Abajo + salto sobre una plataforma = dejarse caer, no saltar.
            if (TryDropThroughPlatform())
            {
                JumpingThisFrame = false;
                return;
            }

            if ((_input.JumpDown && CanUseCoyote) || HasBufferedJump)
            {
                Jump(_jumpHeight);
            }
            else if (_input.JumpDown && !_colDown && _airJumpsUsed < maxAirJumps)
            {
                // Doble salto: cancela el dash y avisa para el efecto en los pies.
                _airJumpsUsed++;
                _dashTimer = 0f;
                Jump(airJumpHeight > 0f ? airJumpHeight : _jumpHeight);
                AirJumped?.Invoke();
            }
            else
            {
                JumpingThisFrame = false;
            }

            if (!_colDown && _input.JumpUp && !_endedJumpEarly && Velocity.y > 0)
                _endedJumpEarly = true;

            if (_colUp && _currentVerticalSpeed > 0) _currentVerticalSpeed = 0;
        }

        private void Jump(float height)
        {
            // La altura va con el cuadrado de la velocidad: ×N de altura = ×√N de velocidad.
            _currentVerticalSpeed = height * Mathf.Sqrt(PlayerStats.Multiplier(PlayerStat.JumpHeight));
            _endedJumpEarly = false;
            _coyoteUsable = false;
            _timeLeftGrounded = float.MinValue;
            _lastJumpPressed = float.MinValue;
            JumpingThisFrame = true;

            PlaySfx(jumpSfxId);
        }

        /// <summary>
        /// Atravesar hacia abajo la plataforma que se pisa. El patrón es <b>abajo + salto</b>: es el
        /// más común en plataformeros 2D de Unity y el más robusto de los dos, porque "sólo abajo"
        /// provoca caídas accidentales cada vez que se agacha — y este controlador ya usa abajo
        /// para agacharse.
        /// </summary>
        private bool TryDropThroughPlatform()
        {
            if (!allowDropThrough || !_input.JumpDown || !_colDown || !_onPlatform) return false;
            if (_input.Y > -0.5f && !_input.CrouchHeld) return false;
            if (_groundCollider == null) return false;

            // Se ignora ESTA plataforma, la que se está pisando, hasta haberla dejado atrás. El
            // temporizador es sólo un tope de seguridad por si nunca se sale de ella.
            _dropCollider = _groundCollider;
            _dropTimer = dropThroughDuration;

            // Se deja de estar apoyado en el acto: si no, SnapToGround volvería a pegar los pies a
            // la plataforma este mismo frame y el jugador no llegaría a caer.
            _colDown = false;
            _onPlatform = false;
            _onWalkableSlope = false;
            _coyoteUsable = false;
            _timeLeftGrounded = float.MinValue;
            _lastJumpPressed = float.MinValue;   // que el buffer no dispare un salto al frame siguiente
            IsCrouching = false;

            if (_currentVerticalSpeed > 0f) _currentVerticalSpeed = 0f;
            return true;
        }

        /// <summary>
        /// Suelta la plataforma que se estaba atravesando en cuanto se ha dejado atrás: cuando la
        /// cabeza del personaje queda por debajo de ella. Se mide contra los límites del collider,
        /// así que en una rampa vale igual — hay que haber bajado del todo, no unos centímetros.
        /// El temporizador sólo es un tope por si algo sale mal.
        /// </summary>
        private void UpdateDropThrough()
        {
            if (_dropCollider == null) return;

            _dropTimer -= Time.deltaTime;
            if (_dropTimer <= 0f)
            {
                ClearDropThrough();
                return;
            }

            float headY = transform.position.y + _characterBounds.center.y + _characterBounds.extents.y;
            if (headY < _dropCollider.bounds.min.y) ClearDropThrough();
        }

        private void ClearDropThrough()
        {
            _dropCollider = null;
            _dropTimer = 0f;
        }

        // ================================================================ dash

        private void TryStartDash()
        {
            if (!allowDash || !_input.DashDown || _dashTimer > 0f || _dashCooldownTimer > 0f) return;
            if (!_colDown && _airDashesUsed >= maxAirDashes) return;

            if (!_colDown) _airDashesUsed++;

            // Se lanza hacia donde se está apuntando; si no hay input, hacia donde se mira.
            _dashDirection = Mathf.Abs(_input.X) > 0.01f ? (int)Mathf.Sign(_input.X) : Facing;
            Facing = _dashDirection;
            if (_sprite != null) _sprite.flipX = _dashDirection < 0;

            // Más distancia = más duración a la misma velocidad (el dash se sigue viendo igual de rápido).
            float duration = dashDuration * PlayerStats.Multiplier(PlayerStat.DashDistance);
            _dashTimer = duration;
            _dashCooldownTimer = dashCooldown + duration;

            PlaySfx(dashSfxId);
            Dashed?.Invoke(_dashDirection);
        }

        private void UpdateDash()
        {
            _dashTimer -= Time.deltaTime;

            _currentHorizontalSpeed = dashSpeed * _dashDirection;
            if (dashIgnoresGravity) _currentVerticalSpeed = 0f;

            // No atravesar paredes durante el dash (una rampa no trepable cuenta como pared).
            BlockAgainstSteepSlope();
            if (_currentHorizontalSpeed == 0f ||
                (_currentHorizontalSpeed > 0 && _colRight) || (_currentHorizontalSpeed < 0 && _colLeft))
            {
                _currentHorizontalSpeed = 0f;
                _dashTimer = 0f;
            }

            if (_dashTimer <= 0f)
            {
                // Se sale del dash sin conservar toda la velocidad, para que no patine.
                _currentHorizontalSpeed = Mathf.Clamp(_currentHorizontalSpeed, -MoveClamp, MoveClamp);
            }
        }

        // ================================================================ retroceso

        /// <summary>
        /// Recibe el empujón de <see cref="Knockback"/>. Como este controlador integra su propia
        /// velocidad y sólo usa un Rigidbody2D cinemático, el empujón no puede llegar como una
        /// fuerza: se escribe directamente en la velocidad interna.
        ///
        /// Durante <paramref name="duration"/> segundos la entrada se ignora — si no, mantener la
        /// dirección contraria anularía el empujón en el mismo frame y el golpe no se notaría.
        /// </summary>
        public void ApplyKnockback(Vector2 velocity, float duration)
        {
            if (duration <= 0f) return;

            _knockbackTimer = Mathf.Max(_knockbackTimer, duration);

            _currentHorizontalSpeed = velocity.x;
            _currentVerticalSpeed = velocity.y;

            // Un golpe corta el dash y el salto en curso, y devuelve el control aéreo: si no,
            // salir despedido en pleno dash dejaría al jugador cayendo sin recursos.
            _dashTimer = 0f;
            _endedJumpEarly = true;
            IsCrouching = false;

            if (velocity.y > 0f) _coyoteUsable = false;
        }

        /// <summary>Corta el empujón y devuelve el control (muerte, respawn, cambio de sección).</summary>
        public void CancelKnockback()
        {
            if (_knockbackTimer <= 0f) return;

            _knockbackTimer = 0f;
            _currentHorizontalSpeed = 0f;
            _currentVerticalSpeed = 0f;
        }

        private void UpdateKnockback()
        {
            _knockbackTimer -= Time.deltaTime;

            // Contra una pared el empujón se corta en seco en vez de empotrar al jugador en ella.
            if ((_currentHorizontalSpeed > 0 && _colRight) || (_currentHorizontalSpeed < 0 && _colLeft))
                _currentHorizontalSpeed = 0f;

            // La gravedad sigue actuando: el empujón es un arco, no un desplazamiento plano.
            CalculateJumpApex();
            CalculateGravity();

            if (_knockbackTimer <= 0f)
            {
                _knockbackTimer = 0f;
                // Se devuelve el control sin arrastrar velocidad de más.
                _currentHorizontalSpeed = Mathf.Clamp(_currentHorizontalSpeed, -MoveClamp, MoveClamp);
            }
        }

        // ================================================================ pegado al suelo

        // El sensor da por apoyado al personaje cuando el suelo está a menos de
        // _detectionRayLength por debajo, así que puede quedarse flotando hasta esa distancia.
        // Aquí se baja lo justo para que los pies toquen de verdad.
        // Al bajar una rampa el hueco puede llegar a 'slopeSnapDistance': ProbeGround ya midió la
        // distancia exacta al suelo (rampa o plataforma incluidas), así que aquí sólo se baja.
        private void SnapToGround()
        {
            if (!snapToGround || !_colDown || _currentVerticalSpeed > 0f) return;
            if (_groundDistance >= float.MaxValue) return;

            float drop = _groundDistance - groundSkin;
            if (drop <= 0.0001f) return;

            transform.position += Vector3.down * drop;
            CalculateRayRanged();   // los sensores se quedaron altos tras bajar
        }

        // ================================================================ pasos

        private void UpdateFootsteps()
        {
            if (string.IsNullOrWhiteSpace(footstepSfxId)) return;

            // Ojo: el temporizador NO se pone a cero al dejar de andar. Si se reiniciara, cualquier
            // parpadeo de _colDown haría sonar un paso en cada frame en que vuelve a tocar suelo.
            // Así el intervalo se respeta siempre, pase lo que pase con el sensor de suelo.
            _footstepTimer -= Time.deltaTime;

            if (!_colDown || Mathf.Abs(_currentHorizontalSpeed) < 0.5f) return;
            if (_footstepTimer > 0f) return;

            _footstepTimer = footstepInterval;
            PlaySfx(footstepSfxId);
        }

        // ================================================================ mover

        private void MoveCharacter()
        {
            var pos = transform.position;
            RawMovement = new Vector3(_currentHorizontalSpeed, _currentVerticalSpeed);

            // En rampa el desplazamiento sigue la superficie en vez de ser horizontal puro.
            var move = (Vector3)ProjectOnSlope(RawMovement) * Time.deltaTime;

            // Subir el pie por una rampa (de plataforma o de suelo sólido) y aterrizar sobre las
            // plataformas, que el barrido de colisión no ve, se resuelve aquí.
            move = ResolveSurface(move, out float sweepLift);

            var furthestPoint = pos + move;

            // La caja del barrido va encogida: apoyado en el suelo la caja a tamaño real entra
            // dentro del margen de contacto de Physics2D y daría un choque falso cada frame.
            // En rampa además se le sube la base: la caja es plana y la cuesta no, así que su
            // esquina del lado de subida se mete en el suelo y el barrido lo leía como un muro.
            // Los pies ya los ha colocado ResolveSurface; al barrido sólo le toca ver paredes.
            var solverSize = (Vector2)_characterBounds.size - Vector2.one * solverSkin;
            solverSize.y -= sweepLift;
            var solverOffset = new Vector3(0f, sweepLift * 0.5f);
            furthestPoint += solverOffset;
            pos += solverOffset;

            var hit = Physics2D.OverlapBox(furthestPoint, solverSize, 0, _groundLayer);
            if (!hit)
            {
                transform.position += move;
                return;
            }

            var positionToMoveTo = transform.position + solverOffset;
            for (int i = 1; i < _freeColliderIterations; i++)
            {
                var t = (float)i / _freeColliderIterations;
                var posToTry = Vector2.Lerp(pos, furthestPoint, t);

                if (Physics2D.OverlapBox(posToTry, solverSize, 0, _groundLayer))
                {
                    transform.position = positionToMoveTo - solverOffset;

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

        /// <summary>
        /// Reparte la velocidad horizontal sobre la tangente de la rampa. La tangente es unitaria,
        /// así que el módulo de la velocidad en pendiente es el mismo que en plano: subir y bajar
        /// se sienten igual que andar. Al subir esto evita quedarse clavado contra la cuesta; al
        /// bajar evita despegarse y rebotar.
        /// </summary>
        private Vector2 ProjectOnSlope(Vector2 raw)
        {
            if (!_onWalkableSlope || raw.y > 0f) return raw;   // saltando: movimiento normal

            var tangent = new Vector2(_groundNormal.y, -_groundNormal.x);   // orientada a +X
            return tangent * raw.x;
        }

        /// <summary>
        /// Coloca los pies sobre la superficie en la columna de <b>destino</b> del paso:
        ///  - <b>subir el pie</b> hasta la superficie al recorrer una rampa, sea de plataforma o
        ///    de suelo sólido. Sin esto, un paso grande (ir rápido) mete los pies dentro del
        ///    collider; el rayo del sensor nace ya dentro, Physics2D devuelve un impacto a
        ///    distancia 0 que no cuenta como suelo, y el personaje se cae a través de la rampa.
        ///  - <b>frenar la caída</b> sobre la cara superior de una plataforma (el sensor de suelo,
        ///    de 0.1, no alcanza a un frame de caída a 40 u/s). En el suelo sólido eso ya lo hace
        ///    el barrido de <see cref="MoveCharacter"/>, así que ahí sólo se atiende apoyado.
        ///
        /// <paramref name="sweepLift"/> es cuánto debe subir la base de la caja del barrido para
        /// no leer como muro la rampa sólida que pisa: 0 en llano, así que ahí nada cambia.
        /// </summary>
        private Vector3 ResolveSurface(Vector3 move, out float sweepLift)
        {
            sweepLift = 0f;

            bool platforms = PlatformsActive;
            bool ground = enableSlopes && _colDown && _currentVerticalSpeed <= 0f;
            if (!platforms && !ground) return move;

            // Cuánto puede subir el pie en un frame: lo que gana la rampa más inclinada admitida a
            // lo largo del avance horizontal. Sólo apoyado; en el aire esto es sólo el aterrizaje.
            float rise = _colDown
                ? Mathf.Abs(move.x) * Mathf.Min(3f, Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad)) + groundSkin
                : 0f;
            float fall = Mathf.Max(0f, -move.y) + groundSkin;

            float feetY = _raysDown.Start.y;
            float destFeetY = feetY + move.y;
            float bestSurfaceY = float.MinValue;

            // La rampa sólida más inclinada bajo los pies, ahora o en el destino del paso.
            float groundAngle = _onWalkableSlope && !_onPlatform ? _slopeAngle : 0f;

            foreach (var point in EvaluateRayPositions(_raysDown))
            {
                var origin = new Vector2(point.x + move.x, feetY + rise);

                if (platforms)
                {
                    var hit = Physics2D.Raycast(origin, Vector2.down, rise + fall, _platformLayer);
                    if (AcceptPlatformHit(hit) && Vector2.Angle(hit.normal, Vector2.up) <= maxSlopeAngle)
                        bestSurfaceY = Mathf.Max(bestSurfaceY, hit.point.y);
                }

                if (ground)
                {
                    // Pared o escalón (rayo nacido dentro): no es rampa, lo frena el barrido.
                    var hit = Physics2D.Raycast(origin, Vector2.down, rise + fall, _groundLayer);
                    if (!IsWalkableSurface(hit)) continue;

                    bestSurfaceY = Mathf.Max(bestSurfaceY, hit.point.y);
                    groundAngle = Mathf.Max(groundAngle, Vector2.Angle(hit.normal, Vector2.up));
                }
            }

            // La esquina de la caja va '_rayBuffer' más afuera que el último rayo de pies: en una
            // cuesta queda como mucho esa distancia × pendiente por debajo de la superficie.
            if (groundAngle > 0.5f)
                sweepLift = _rayBuffer * Mathf.Tan(groundAngle * Mathf.Deg2Rad) + groundSkin;

            if (bestSurfaceY == float.MinValue) return move;

            float targetFeetY = bestSurfaceY + groundSkin;
            if (targetFeetY <= destFeetY) return move;   // el paso no llega a la superficie: nada que corregir

            move.y += targetFeetY - destFeetY;
            if (_currentVerticalSpeed < 0f) _currentVerticalSpeed = 0f;
            return move;
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

            if (stompDamage > 0f) otherHealth.TakeDamage(stompDamage, transform.position);
            else otherHealth.Die();

            Jump(_jumpHeight);      // rebote
            PlaySfx(stompSfxId);
        }

        // ================================================================ varios

        private void OnDied()
        {
            _controlEnabled = false;
            _input = default;
            _currentHorizontalSpeed = 0f;
            _currentVerticalSpeed = 0f;
            IsCrouching = false;
            _dashTimer = 0f;
            _knockbackTimer = 0f;
            ClearDropThrough();
        }

        /// <summary>Complemento de <see cref="OnDied"/>: devuelve el control tras un respawn.</summary>
        private void OnRevived() => _controlEnabled = true;

        // La capa de plataformas NO puede estar también en la máscara sólida: el barrido de
        // colisión la trataría como muro y no se podría atravesar por abajo.
        private void OnValidate()
        {
            if ((_groundLayer.value & _platformLayer.value) != 0)
                _groundLayer = _groundLayer.value & ~_platformLayer.value;
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
            var futureMove = (Vector3)ProjectOnSlope(new Vector2(_currentHorizontalSpeed, _currentVerticalSpeed)) * Time.deltaTime;
            Gizmos.DrawWireCube(transform.position + _characterBounds.center + futureMove, _characterBounds.size);

            if (!_colDown) return;

            // Normal del suelo: verde si la rampa es transitable, roja si actúa como pared.
            Gizmos.color = _slopeAngle <= maxSlopeAngle ? Color.green : Color.red;
            Gizmos.DrawRay(new Vector3(transform.position.x, _raysDown.Start.y - _groundDistance), _groundNormal);
        }
    }
}
