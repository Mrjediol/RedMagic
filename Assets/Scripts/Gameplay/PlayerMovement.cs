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
    ///  - <b>Audio</b>: avisa de salto, doble salto, dash y pasos como
    ///    <see cref="ISoundEventSource"/>; qué suena lo decide el <see cref="SoundEmitter"/>.
    ///
    /// El Rigidbody2D se mantiene (forzado a Kinematic) sólo para que sigan llegando los eventos
    /// de colisión / trigger a los enemigos, proyectiles y zonas de daño.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class PlayerMovement : MonoBehaviour, IKnockbackReceiver, ISoundEventSource
    {
        /// <summary>OnJump / OnAirJump / OnDash / Footstep. Ver <see cref="SoundEmitter"/>.</summary>
        public event System.Action<SoundTrigger> SoundTriggered;

        // ---------------------------------------------------------------- tipos internos

        private struct FrameInput
        {
            public float X;
            public float Y;
            public bool JumpDown;
            public bool JumpUp;
            public bool DownHeld;
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

        /// <summary>Se dispara al saltar desde el suelo (o con coyote / salto guardado). Lo usa el VFX de los pies.</summary>
        public event System.Action Jumped;

        /// <summary>Se dispara al usar un salto en el aire (doble salto). Lo usa el VFX de los pies.</summary>
        public event System.Action AirJumped;

        /// <summary>Se dispara al iniciar un dash.</summary>
        public event System.Action<int> Dashed;

        /// <summary>Lo que dura un dash ahora mismo (el de base por el multiplicador de distancia de los items).</summary>
        public float DashDuration => dashDuration * PlayerStats.Multiplier(PlayerStat.DashDistance);

        // ---------------------------------------------------------------- Input Actions + táctil

        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string dashActionName = "Dash";

        // ---------------------------------------------------------------- SFX

        [Header("Sonido")]
        [Tooltip("Segundos entre pasos (evento Footstep del SoundEmitter) al correr por el suelo.")]
        [SerializeField] private float footstepInterval = 0.32f;

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
        [SerializeField] private float maxSlopeAngle = 80f;

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

        // ---------------------------------------------------------------- escalones

        [Header("STEP UP — subida automática de escalones")]
        [Tooltip("Al chocar caminando contra un borde bajo, el personaje se sube solo en vez de " +
                 "bloquearse contra él. Independiente del salto: no consume salto en el aire ni " +
                 "pasa por Jump.")]
        [SerializeField] private bool enableStepUp = true;

        [Tooltip("EL ÚNICO valor de la subida de escalones: hasta qué altura, medida desde los PIES " +
                 "del personaje, un obstáculo cuenta como escalón.\n\n" +
                 "Si el punto en que el obstáculo toca al personaje queda por DEBAJO de esa altura, " +
                 "es un escalón y se sube encima sin frenar. Si queda por ENCIMA, es una pared: " +
                 "bloquea igual que siempre y hay que saltar.\n\n" +
                 "Subirlo = se trepan bordes más altos. Bajarlo = más cosas se comportan como pared.")]
        [Range(0f, 1f)]
        [SerializeField] private float stepThresholdHeight = 0.4f;

        [Tooltip("Depuración: dibuja en el Scene view la línea de los pies, la línea del umbral y " +
                 "los puntos de contacto detectados (verde = escalón, rojo = pared), y registra en " +
                 "consola cada subida. Dejar apagado normalmente.")]
        [SerializeField] private bool debugStepUp = false;

        // ---------------------------------------------------------------- suelo sólido

        [Header("SUELO SÓLIDO — la capa Ground es infranqueable")]
        [Tooltip("Contención de la capa Ground: barrido real del trayecto (no sólo del punto de " +
                 "destino) antes de mover, y corrección en LateUpdate si aun así se ha penetrado.\n\n" +
                 "Afecta ÚNICAMENTE a la capa 'Ground Layer' de arriba. Las plataformas y todo lo " +
                 "demás se mueven exactamente igual con esto encendido o apagado.")]
        [SerializeField] private bool groundIsImpassable = true;

        [Tooltip("SÓLO PARA GROUND. Inclinación mínima que debe tener una cara de suelo sólido " +
                 "para que el step-up la considere un ESCALÓN al que subirse.\n\n" +
                 "Existe porque el step-up sólo miraba 'más inclinada que maxSlopeAngle', y con eso " +
                 "una rampa demasiado empinada (p. ej. 82° con el máximo en 80°) se confundía con un " +
                 "escalón: se trepaba y, de paso, se saltaba el bloqueo de pared. Una cara de Ground " +
                 "entre 'maxSlopeAngle' y este valor es una rampa no trepable y FRENA como un muro.\n\n" +
                 "Los escalones reales de un tilemap son caras verticales (90°), así que el valor por " +
                 "defecto no cambia ningún escalón existente. No afecta a la capa de plataformas.")]
        [Range(45f, 90f)]
        [SerializeField] private float groundStepMinFaceAngle = 85f;

        [Tooltip("Cuánto se encoge la caja con la que la red de seguridad comprueba si el personaje " +
                 "está dentro del suelo sólido. Debe ser mayor que el 'Default Contact Offset' de " +
                 "Physics 2D (0.01) para que estar apoyado no cuente como penetración.")]
        [Range(0.02f, 0.2f)]
        [SerializeField] private float groundClampInset = 0.04f;

        [Tooltip("Desplazamiento máximo que puede aplicar la red de seguridad de una sola vez. " +
                 "Corre cada frame, así que una penetración real nunca es más profunda que un " +
                 "frame de movimiento: este tope sólo evita un tirón visible si algo va muy mal.")]
        [Min(0.1f)]
        [SerializeField] private float groundClampMaxPush = 1.5f;

        [Tooltip("Depuración: registra en consola y dibuja en el Scene view cada vez que la red de " +
                 "seguridad ha tenido que sacar al personaje del suelo sólido, diciendo qué sistema " +
                 "movió al jugador ese frame (andar / dash / retroceso / escalón / …).\n\n" +
                 "Con el barrido funcionando esto NO debería saltar nunca: si salta durante una " +
                 "partida de prueba, queda un camino que se salta el barrido. Dejar apagado.")]
        [SerializeField] private bool debugGroundClamp = false;

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

        // escalones: lo que los contactos de la última colisión dejaron anotado
        private readonly ContactPoint2D[] _stepContacts = new ContactPoint2D[16];
        private float _stepSurfaceY;                        // altura absoluta del borde a pisar
        private int _stepSide;                              // -1 / 1: de qué lado estaba
        private float _stepContactTime = float.MinValue;    // cuándo se vio (para descartar lo viejo)
        private int _stepLogOutcome;                        // sólo depuración: último veredicto registrado

        // suelo sólido (Ground): barrido y red de seguridad
        private ContactFilter2D _groundFilter;
        private readonly List<RaycastHit2D> _groundCasts = new List<RaycastHit2D>(8);
        private readonly Collider2D[] _groundOverlaps = new Collider2D[4];
        private float _sweepLift;                           // el de ResolveSurface, para reusar su forma de caja
        private bool _surfaceResolvedGround;                // ResolveSurface encontró apoyo de Ground en el destino
        private Vector3 _lastSafePosition;                  // última posición validada fuera del suelo sólido
        private bool _hasSafePosition;
        private int _groundSteepDir;                        // -1 / 1: rampa de Ground no trepable vista por contactos
        private float _groundSteepTime = float.MinValue;
        private MoveSource _moveSource;                     // qué sistema conduce este frame
        private MoveSource _lastWrite;                      // qué escritura de posición fue la última
        private Vector2 _lastClampPush, _lastClampCenter, _lastClampSize;
        private int _lastClampFrame = int.MinValue;

        /// <summary>
        /// Quién ha movido al personaje. Sólo sirve para que el aviso de <c>debugGroundClamp</c>
        /// pueda decir qué sistema dejó al jugador dentro del suelo, en vez de limitarse a decir
        /// que pasó.
        /// </summary>
        private enum MoveSource { None, Walk, Dash, Knockback, StepUp, GroundSnap, Solver }

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

            // El step-up se alimenta de los contactos de colisión (ver EvaluateStepContacts). Un
            // cuerpo dormido deja de reportarlos, y estar quieto apoyado contra un escalón es
            // exactamente el caso en el que hacen falta.
            _body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            if (_characterBounds.size == Vector3.zero && _collider != null)
                _characterBounds = new Bounds(_collider.offset, _collider.bounds.size);

            RebuildGroundFilter();

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

        private void Activate()
        {
            _active = true;
            _hasSafePosition = false;   // se reancla en el primer LateUpdate
        }

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
                _dashTimer = 0f;
                _knockbackTimer = 0f;
                _moveSource = MoveSource.None;
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

            // Saltar durante un dash lo cancela en el acto: si no, UpdateDash seguiría pisando la
            // velocidad vertical que Jump() acaba de poner (dashIgnoresGravity la fuerza a 0 cada
            // frame) y el salto se anularía él solo al frame siguiente. La velocidad horizontal del
            // dash no se toca aquí, así que el impulso se conserva y sólo se amortigua como cualquier
            // otra salida de dash (el mismo clamp a MoveClamp que aplica UpdateDash al terminar).
            if (!IsKnockedBack && _dashTimer > 0f && _input.JumpDown)
                _dashTimer = 0f;

            if (IsKnockedBack)
            {
                _moveSource = MoveSource.Knockback;
                UpdateKnockback();   // el empujón manda sobre todo lo demás
            }
            else if (_dashTimer > 0f)
            {
                _moveSource = MoveSource.Dash;
                UpdateDash();     // el dash manda: sustituye a andar y a la gravedad
                CalculateJump();  // …pero saltar puede cancelarlo
            }
            else
            {
                _moveSource = MoveSource.Walk;
                CalculateWalk();      // horizontal
                CalculateJumpApex();  // afecta a la caída: antes de la gravedad
                CalculateGravity();   // vertical
                CalculateJump();      // puede sobreescribir la vertical
            }

            SnapToGround();       // pega los pies al suelo (corrige el flotar del sensor)
            UpdateFootsteps();
            MoveCharacter();      // aplica el movimiento
        }

        /// <summary>
        /// Red de seguridad. Después de que TODO lo que mueve al jugador este frame haya escrito
        /// ya su posición, se comprueba una última vez que no ha quedado dentro de la capa Ground y,
        /// si lo está, se le saca. Ninguna otra capa entra aquí.
        ///
        /// Es el último recurso, no el mecanismo: el barrido de <see cref="MoveCharacter"/> debería
        /// hacer que esto no salte nunca. Está precisamente para que un camino que nadie previó —
        /// un sistema nuevo que empuje al jugador, un frame largo, un teletransporte — se corrija
        /// solo en el mismo frame en vez de terminar en una caída fuera del mapa.
        /// </summary>
        private void LateUpdate()
        {
            if (!_active || !groundIsImpassable) return;
            ValidateGroundContainment();
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
                DownHeld = TouchInput.Down,
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

            if (_colDown && !groundedCheck)
            {
                _timeLeftGrounded = Time.time; // acaba de despegar
                _dashCooldownTimer = 0f;       // suelo→aire resetea el cooldown del dash (dash→salto→dash)
            }
            else if (!_colDown && groundedCheck)
            {
                _coyoteUsable = true;   // acaba de tocar suelo
                LandingThisFrame = true;
                _dashCooldownTimer = 0f; // aire→suelo también resetea el cooldown del dash
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

            // Si el obstáculo de delante resultó ser un escalón (lo decidieron los contactos de la
            // colisión, ver EvaluateStepContacts), se sube y NO se bloquea el avance. Si no, el
            // bloqueo de pared de siempre, igual que antes de existir el step-up.
            if (!ConsumeStepUp() && ((_currentHorizontalSpeed > 0 && _colRight) || (_currentHorizontalSpeed < 0 && _colLeft)))
            {
                _currentHorizontalSpeed = 0;
            }

            BlockAgainstSteepSlope();
        }

        /// <summary>
        /// Decide si el obstáculo con el que se acaba de chocar es un <b>escalón</b> (se sube) o una
        /// <b>pared</b> (bloquea), usando los puntos de contacto reales de la colisión. El motor ya
        /// dice exactamente dónde está tocando el obstáculo, así que no hay que adivinar geometría
        /// con rayos ni hacer que varios sensores con offsets distintos coincidan entre sí.
        ///
        /// La regla, con un único número — <see cref="stepThresholdHeight"/>, medido desde la base de
        /// la caja del personaje (sus pies):
        ///  - Todo lo que bloquea el avance toca <b>por debajo</b> de esa línea → es un escalón: se
        ///    anota su borde superior y <see cref="ConsumeStepUp"/> sube al personaje encima.
        ///  - Algo que bloquea el avance toca <b>por encima</b> → el obstáculo sigue existiendo más
        ///    arriba de lo que se puede subir: es pared. No se sube, y el bloqueo horizontal normal
        ///    hace su trabajo de siempre (hay que saltar).
        ///
        /// "Bloquea el avance" son dos condiciones, ambas leídas del propio manifold: la cara tocada
        /// es más inclinada que <c>maxSlopeAngle</c> (la cara vertical de un escalón lo es; el suelo
        /// llano y las rampas transitables no, de ésas se encarga el sistema de rampas, así que los
        /// dos sistemas no se pisan), y el contacto está del lado hacia el que se camina.
        ///
        /// El borde superior del escalón es el <b>contacto más alto</b> de ese conjunto: el manifold
        /// de una cara vertical se reporta en sus dos extremos, así que el más alto ES la superficie
        /// sobre la que hay que quedarse. Decidir y medir salen del mismo dato, luego no pueden
        /// discrepar — que es justo lo que pasaba con tres rayos ajustados por separado.
        ///
        /// Corre en los callbacks de colisión (un paso de física), no por frame, y no mira cuánto se
        /// avanzó este frame: por eso el resultado no depende de los FPS. Mientras se siga apoyado
        /// contra el escalón, <c>OnCollisionStay2D</c> lo vuelve a reportar cada paso de física, así
        /// que el mismo escalón se sube siempre, en cualquier momento en que se llegue a tocarlo.
        /// </summary>
        private void EvaluateStepContacts(Collision2D collision, bool entering)
        {
            if (!enableStepUp || !_active || collision.collider == null) return;

            int layerBit = 1 << collision.collider.gameObject.layer;
            int terrain = _groundLayer.value | _platformLayer.value;
            if ((terrain & layerBit) == 0) return;

            // El suelo sólido tiene una regla de más (ver 'groundStepMinFaceAngle'). Las plataformas
            // pasan por exactamente el mismo camino de siempre.
            bool isGround = groundIsImpassable && (_groundLayer.value & layerBit) != 0;

            // La plataforma que se está atravesando hacia abajo no es un escalón al que subirse.
            if (_dropCollider != null && collision.collider == _dropCollider) return;

            // Sentido en el que se intenta avanzar. Se lee de la ENTRADA y no de la velocidad porque
            // al chocar el bloqueo horizontal ya la ha puesto a 0, y el lado quedaría indefinido
            // precisamente en el instante que importa.
            int dir = Mathf.Abs(_input.X) > 0.01f
                ? (int)Mathf.Sign(_input.X)
                : (_currentHorizontalSpeed != 0f ? (int)Mathf.Sign(_currentHorizontalSpeed) : 0);
            if (dir == 0) return;

            var b = new Bounds(transform.position, _characterBounds.size);
            float thresholdY = b.min.y + stepThresholdHeight;

            bool blockedAboveThreshold = false;
            bool steepGroundRamp = false;
            float topOfStep = float.MinValue;
            string detail = null;

            // GetContacts sobre un buffer reutilizado: 'collision.contacts' asigna un array nuevo en
            // cada callback, y esto se llama cada paso de física mientras se toque algo.
            int count = collision.GetContacts(_stepContacts);
            for (int i = 0; i < count; i++)
            {
                var contact = _stepContacts[i];
                float angle = Vector2.Angle(contact.normal, Vector2.up);
                bool walkableFace = angle <= maxSlopeAngle;
                bool wrongSide = (contact.point.x - b.center.x) * dir <= 0f;

                if (debugStepUp)
                {
                    string why = walkableFace ? $"DESCARTADO (cara de {angle:F1}° <= maxSlopeAngle {maxSlopeAngle:F0}°)"
                               : wrongSide ? "DESCARTADO (otro lado)"
                               : isGround && angle < groundStepMinFaceAngle
                                   ? $"RAMPA DE GROUND NO TREPABLE ({angle:F1}° < {groundStepMinFaceAngle:F0}°) → PARED"
                               : contact.point.y > thresholdY ? "sobre el umbral → PARED"
                               : "bajo el umbral → escalón";
                    detail += $"    c{i}: n={contact.normal} ang={angle:F1}° punto.y={contact.point.y:F3} " +
                              $"(pies{(contact.point.y - b.min.y >= 0 ? "+" : "")}{contact.point.y - b.min.y:F3}) " +
                              $"sep={contact.separation:F4} → {why}\n";
                }

                // Suelo llano o rampa transitable: no frena el avance, no es un escalón.
                if (walkableFace) continue;

                // Sólo el lado hacia el que se camina.
                if (wrongSide) continue;

                // SÓLO GROUND: una cara entre 'maxSlopeAngle' y 'groundStepMinFaceAngle' es una
                // rampa demasiado inclinada, no un escalón. Antes caía del lado de "escalón" (su
                // base toca por debajo del umbral), así que el personaje la trepaba de un salto
                // por paso de física Y, de paso, ConsumeStepUp corto-circuitaba el bloqueo de
                // pared, con lo que además seguía avanzando: se atravesaba la cuesta.
                if (isGround && angle < groundStepMinFaceAngle)
                {
                    steepGroundRamp = true;
                    if (debugStepUp) DebugDrawContact(contact.point, Color.red);
                    continue;
                }

                if (contact.point.y > thresholdY)
                {
                    blockedAboveThreshold = true;
                    if (debugStepUp) DebugDrawContact(contact.point, Color.red);
                }
                else
                {
                    topOfStep = Mathf.Max(topOfStep, contact.point.y);
                    if (debugStepUp) DebugDrawContact(contact.point, Color.green);
                }
            }

            // Diagnóstico: un contacto que se descarta no deja rastro, y era imposible ver por qué.
            // Se registra al entrar en contacto y cada vez que el veredicto cambia, no en cada paso
            // de física, para que apoyarse contra algo no llene la consola.
            if (debugStepUp)
            {
                int outcome = steepGroundRamp ? 3 : blockedAboveThreshold ? 2 : (topOfStep > float.MinValue ? 1 : 0);
                if (entering || outcome != _stepLogOutcome)
                {
                    string verdict = outcome == 3 ? "RAMPA DE GROUND NO TREPABLE (bloquea)"
                                   : outcome == 2 ? "PARED" : outcome == 1 ? "ESCALON" : "NINGUN CONTACTO UTIL";
                    Debug.Log($"[StepUp/Contacto] {Time.frameCount} '{collision.collider.name}' " +
                              $"({LayerMask.LayerToName(collision.collider.gameObject.layer)}) → {verdict} | " +
                              $"pies={b.min.y:F3} umbral={thresholdY:F3} (+{stepThresholdHeight:F2}) " +
                              $"apoyado={_colDown} vy={_currentVerticalSpeed:F2} dir={dir}\n{detail}", this);
                }
                _stepLogOutcome = outcome;
            }

            // Una rampa de Ground no trepable arma el bloqueo horizontal (lo consume
            // BlockAgainstSteepSlope) y nunca deja subir un escalón hacia ese mismo lado.
            if (steepGroundRamp)
            {
                _groundSteepDir = dir;
                _groundSteepTime = Time.time;
            }

            if (blockedAboveThreshold || steepGroundRamp || topOfStep == float.MinValue) return;

            // Se guarda la altura ABSOLUTA de la superficie, no cuánto habría que subir: al aplicarla
            // se recalcula contra los pies de ese momento, así que un contacto de un paso de física
            // anterior nunca puede subir de más.
            _stepSurfaceY = topOfStep;
            _stepSide = dir;
            _stepContactTime = Time.time;
        }

        /// <summary>
        /// Aplica la subida que anotó <see cref="EvaluateStepContacts"/>, si sigue valiendo ahora
        /// mismo. Se hace aquí, en el Update, y no dentro del callback de colisión, porque este
        /// controlador mueve el <c>transform</c> él mismo: toda la escritura de posición vive en un
        /// solo sitio y en un solo orden, así que no hay dos sistemas peleándose por la posición del
        /// mismo frame.
        ///
        /// Sólo apoyado en el suelo: en el aire un "escalón" no es más que el borde de una plataforma
        /// y hay que saltar, como siempre. Y sólo hacia el lado en que se detectó, para que soltar y
        /// volver en sentido contrario no arrastre una subida vieja.
        /// </summary>
        private bool ConsumeStepUp()
        {
            if (!enableStepUp) return false;

            // El contacto tiene que ser de este paso de física o del anterior; más viejo es
            // información caduca. El margen se saca del propio reloj de física, así que vale igual a
            // 20 que a 300 FPS.
            bool fresh = Time.time - _stepContactTime <= Mathf.Max(Time.fixedDeltaTime, Time.deltaTime) * 2f;

            var b = new Bounds(transform.position, _characterBounds.size);
            float lift = _stepSurfaceY + groundSkin - b.min.y;

            // Se acumula el motivo del rechazo en vez de salir en silencio: cuando esto falla, lo
            // único que se veía era que el personaje no subía, sin saber cuál de las condiciones fue.
            string reject =
                  !fresh ? "sin contacto reciente"
                : !_colDown ? $"EN EL AIRE (vy={_currentVerticalSpeed:F2}, sin suelo desde hace " +
                              $"{(Time.time - _timeLeftGrounded) * 1000f:F0}ms)"
                : _currentHorizontalSpeed == 0f ? "sin velocidad horizontal"
                : _stepSide != (int)Mathf.Sign(_currentHorizontalSpeed)
                              ? $"lado distinto (contacto={_stepSide}, avance={Mathf.Sign(_currentHorizontalSpeed)})"
                : lift <= 0f ? $"los pies ya están encima (lift={lift:F3})"
                : lift > stepThresholdHeight + groundSkin
                              ? $"demasiado alto (lift={lift:F3} > umbral {stepThresholdHeight:F2})"
                : null;

            if (reject != null)
            {
                if (debugStepUp && fresh)
                    Debug.Log($"[StepUp/Subida] {Time.frameCount} NO se sube: {reject}.", this);
                return false;
            }

            // Esta subida NO pasa por el barrido de MoveCharacter, así que se valida aquí: un
            // escalón bajo un techo bajo, o un saliente, dejaría al personaje dentro del suelo.
            // Sólo mira la capa Ground; sobre plataformas la subida es exactamente la de antes.
            if (WouldOverlapGround(transform.position + Vector3.up * lift))
            {
                if (debugStepUp || debugGroundClamp)
                    Debug.Log($"[StepUp/Subida] {Time.frameCount} NO se sube: subir {lift:F3}u " +
                              $"dejaría al personaje dentro de la capa Ground.", this);
                return false;
            }

            _lastWrite = MoveSource.StepUp;
            transform.position += Vector3.up * lift;
            CalculateRayRanged();                // los sensores se quedaron bajos tras subir
            _stepContactTime = float.MinValue;   // consumido: no se vuelve a aplicar
            if (_currentVerticalSpeed < 0f) _currentVerticalSpeed = 0f;

            if (debugStepUp)
                Debug.Log($"[StepUp] {Time.frameCount} subido {lift:F3}u (superficie y={_stepSurfaceY:F3}).", this);
            return true;
        }

        /// <summary>Cruz en el punto de contacto: verde = cuenta como escalón, roja = lo descarta
        /// por estar sobre el umbral. Se mantiene unos frames para poder verla al caminar.</summary>
        private static void DebugDrawContact(Vector2 point, Color color)
        {
            const float size = 0.06f;
            const float hold = 0.15f;
            Debug.DrawLine(point + Vector2.left * size, point + Vector2.right * size, color, hold, false);
            Debug.DrawLine(point + Vector2.down * size, point + Vector2.up * size, color, hold, false);
        }

        /// <summary>
        /// Una rampa de más de <c>maxSlopeAngle</c> se comporta como pared: no se trepa. Los rayos
        /// laterales ya frenan contra un muro alto; esto cubre las cuñas bajas, que sólo tocan los
        /// rayos de abajo.
        /// </summary>
        private void BlockAgainstSteepSlope()
        {
            if (_currentHorizontalSpeed == 0f) return;
            int dir = (int)Mathf.Sign(_currentHorizontalSpeed);

            // Bloqueo de siempre, de los rayos de suelo (Ground y plataformas por igual): sólo ve
            // la cuña que ya está BAJO los pies. Se deja tal cual para no cambiar las plataformas.
            if (_steepBlockDir != 0 && dir == _steepBlockDir)
            {
                _currentHorizontalSpeed = 0f;
                return;
            }

            // SÓLO GROUND, y el que de verdad cierra el caso: la rampa no trepable que se tiene
            // DELANTE, vista por los contactos reales de la colisión. Corre después de
            // ConsumeStepUp, así que también anula el avance cuando el step-up se ha saltado el
            // bloqueo de pared.
            if (_groundSteepDir == 0 || dir != _groundSteepDir) return;

            bool fresh = Time.time - _groundSteepTime <= Mathf.Max(Time.fixedDeltaTime, Time.deltaTime) * 2f;
            if (fresh) _currentHorizontalSpeed = 0f;
            else _groundSteepDir = 0;
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
                Jumped?.Invoke();
                SoundTriggered?.Invoke(SoundTrigger.OnJump);
            }
            else if (_input.JumpDown && !_colDown && _airJumpsUsed < maxAirJumps)
            {
                // Doble salto: cancela el dash y avisa para el efecto en los pies.
                _airJumpsUsed++;
                _dashTimer = 0f;
                Jump(airJumpHeight > 0f ? airJumpHeight : _jumpHeight);
                AirJumped?.Invoke();
                SoundTriggered?.Invoke(SoundTrigger.OnAirJump);
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
        }

        /// <summary>
        /// Atravesar hacia abajo la plataforma que se pisa. El patrón es <b>abajo + salto</b>: es el
        /// más común en plataformeros 2D de Unity y el más robusto — "sólo abajo" provocaría caídas
        /// accidentales cada vez que se pulsa abajo para otra cosa.
        /// </summary>
        private bool TryDropThroughPlatform()
        {
            if (!allowDropThrough || !_input.JumpDown || !_colDown || !_onPlatform) return false;
            if (_input.Y > -0.5f && !_input.DownHeld) return false;
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
            float duration = DashDuration;
            _dashTimer = duration;
            _dashCooldownTimer = dashCooldown + duration;

            SoundTriggered?.Invoke(SoundTrigger.OnDash);
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

            if (velocity.y > 0f) _coyoteUsable = false;
        }

        /// <summary>Corta el empujón y devuelve el control (muerte, respawn, cambio de sección).</summary>
        public void CancelKnockback()
        {
            if (_knockbackTimer <= 0f) return;

            _knockbackTimer = 0f;
            _currentHorizontalSpeed = 0f;
            _currentVerticalSpeed = 0f;

            // Esto se llama también al teletransportar (cambio de sección) y al morir: la posición
            // buena anterior ya no vale como referencia de continuidad.
            _hasSafePosition = false;
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

            _lastWrite = MoveSource.GroundSnap;
            transform.position += Vector3.down * drop;
            CalculateRayRanged();   // los sensores se quedaron altos tras bajar
        }

        // ================================================================ pasos

        private void UpdateFootsteps()
        {
            // Ojo: el temporizador NO se pone a cero al dejar de andar. Si se reiniciara, cualquier
            // parpadeo de _colDown haría sonar un paso en cada frame en que vuelve a tocar suelo.
            // Así el intervalo se respeta siempre, pase lo que pase con el sensor de suelo.
            _footstepTimer -= Time.deltaTime;

            // Sólo andando por el suelo: nada en el aire, al saltar (este frame aún toca suelo), en el
            // dash ni al salir despedido. En pausa Update ni llega aquí, así que el reloj se congela.
            if (!_colDown || JumpingThisFrame || IsDashing || IsKnockedBack) return;
            if (Mathf.Abs(_currentHorizontalSpeed) < 0.5f) return;
            if (_footstepTimer > 0f) return;

            _footstepTimer = footstepInterval;
            SoundTriggered?.Invoke(SoundTrigger.Footstep);
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
            _sweepLift = sweepLift;

            // La caja del barrido va encogida: apoyado en el suelo la caja a tamaño real entra
            // dentro del margen de contacto de Physics2D y daría un choque falso cada frame.
            // En rampa además se le sube la base: la caja es plana y la cuesta no, así que su
            // esquina del lado de subida se mete en el suelo y el barrido lo leía como un muro.
            // Los pies ya los ha colocado ResolveSurface; al barrido sólo le toca ver paredes.
            var solverSize = (Vector2)_characterBounds.size - Vector2.one * solverSkin;
            solverSize.y -= sweepLift;
            var solverOffset = new Vector3(0f, sweepLift * 0.5f);

            // Barrido REAL del trayecto contra la capa Ground, antes de aplicar nada. El resolvedor
            // de abajo sólo mira el punto de destino, así que por sí solo deja pasar cualquier paso
            // grande (dash, retroceso, caída rápida, un frame largo) por encima de geometría fina.
            move = SweepAgainstGround(pos + solverOffset, move, solverSize, sweepLift);

            var furthestPoint = pos + move + solverOffset;
            pos += solverOffset;

            _lastWrite = MoveSource.Solver;

            // Con el suelo sólido activo, el barrido de arriba es la ÚNICA autoridad sobre Ground:
            // ya ha medido el trayecto real y ha recortado 'move' contra paredes, techos y cuestas
            // no trepables, perdonando la rampa que se pisa. Lo que sigue — el OverlapBox del punto
            // de destino más el muestreo — es el resolvedor original, y tenerlos a la vez era el
            // "mini frenado" al entrar en una cuesta: ahí los rayos de destino todavía no alcanzan
            // la rampa, así que 'groundAngle' es 0, 'sweepLift' es 0, y sin ese margen la esquina
            // delantera de su caja se mete en la inclinación. El OverlapBox daba positivo y el
            // muestreo bloqueaba ya en la primera iteración: un frame entero sin avanzar.
            //
            // Dos resolvedores con cajas distintas no pueden estar de acuerdo. Se queda el que mide
            // el trayecto; el viejo sigue disponible apagando 'groundIsImpassable'.
            if (groundIsImpassable && _groundLayer.value != 0)
            {
                transform.position += move;
                return;
            }

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

                        // Antes aquí había un empujón a ciegas:
                        //     transform.position += (transform.position - hit.transform.position).normalized
                        //                           * move.magnitude;
                        // 'hit.transform.position' es el ORIGEN DEL TRANSFORM del collider, no el
                        // punto de contacto — para el suelo pintado con Tilemap, el origen del
                        // Tilemap, que puede estar a decenas de unidades y en cualquier dirección.
                        // Era un teletransporte arbitrario, de magnitud proporcional a la velocidad
                        // (o sea: peor cuanto más rápido se iba, justo en dash y retroceso) y sin
                        // volver a comprobarse. Se sustituye por la despenetración real, que mide
                        // la salida más corta y sólo contra Ground.
                        ResolveGroundPenetration();
                    }

                    return;
                }

                positionToMoveTo = posToTry;
            }
        }

        // ================================================================ suelo sólido (Ground)

        /// <summary>
        /// Recorta <paramref name="move"/> para que el TRAYECTO del frame no atraviese la capa
        /// Ground, eje por eje. Es lo único que garantiza que el suelo sólido sea infranqueable
        /// venga el movimiento de donde venga: andar, rampa, dash, retroceso o cualquier sistema
        /// futuro, porque todos terminan aquí.
        ///
        /// Lo que NO frena, a propósito, es una cara de Ground <b>transitable</b> (inclinación
        /// dentro de <c>maxSlopeAngle</c>) a la que los pies puedan llegar este frame: de esas se
        /// encarga <see cref="ResolveSurface"/> subiendo el pie encima, y frenarlas aquí dejaría al
        /// personaje clavado al pie de cada cuesta. Todo lo demás — muros, techos y las rampas
        /// demasiado inclinadas — recorta el avance hasta el punto de contacto.
        ///
        /// La capa de plataformas no se consulta en ningún momento: se atraviesan igual que antes.
        /// </summary>
        private Vector3 SweepAgainstGround(Vector3 origin, Vector3 move, Vector2 solverSize, float lift)
        {
            if (!groundIsImpassable || _groundLayer.value == 0) return move;
            if (solverSize.x <= 0f || solverSize.y <= 0f) return move;

            // Una cara transitable sólo se perdona si ResolveSurface acaba de resolver el apoyo de
            // Ground en la columna de DESTINO, es decir, si el pie va a terminar encima de ella
            // este frame. Si no lo ha resuelto, esa cuesta no es alcanzable y frena como un muro.
            //
            // El criterio anterior ("el contacto queda por debajo de pies + alcance") era el
            // agujero: con 'maxSlopeAngle' alto, una pared casi vertical cuenta como transitable y
            // su contacto está justo a la altura del pie, así que se perdonaba sola — el barrido la
            // dejaba pasar y ResolveSurface no llegaba a subir el pie. El personaje la atravesaba.
            // La bandera es TODO el criterio. No se le añade ningún umbral de altura: la base de la
            // caja del barrido va por encima de los pies (medio 'solverSkin' más 'sweepLift'), así
            // que en una rampa el contacto cae siempre por encima de la línea del pie. Compararlo
            // con la altura del pie frenaba la cuesta a poca velocidad, ponía la horizontal a 0, y
            // al frame siguiente se arrancaba otra vez desde 0: subir se volvía un arrastre.
            bool forgiveWalkable = _surfaceResolvedGround;

            // --- horizontal: el eje en el que frenan las paredes y las rampas no trepables.
            if (Mathf.Abs(move.x) > 0.0001f)
            {
                float dirX = Mathf.Sign(move.x);
                var dir = new Vector2(dirX, 0f);
                float wanted = Mathf.Abs(move.x);
                float allowed = SweepAxisAgainstGround(origin, solverSize, dir, wanted, forgiveWalkable);

                // La franja inferior que 'sweepLift' saca de la caja del barrido. En una rampa de
                // 80° son ~0.58u — el 40% inferior del cuerpo — y ahí dentro cabe entera una pared
                // baja que el barrido no vería. Se barre aparte: la propia rampa se descarta por su
                // normal, así que en esa franja sólo puede frenar algo demasiado inclinado.
                if (lift > 0.0001f)
                {
                    var stripSize = new Vector2(solverSize.x, lift);
                    var stripOrigin = new Vector3(origin.x, origin.y - solverSize.y * 0.5f - lift * 0.5f);
                    allowed = Mathf.Min(allowed,
                        SweepAxisAgainstGround(stripOrigin, stripSize, dir, wanted, forgiveWalkable));
                }

                if (allowed < wanted)
                {
                    move.x = allowed * dirX;
                    if (Mathf.Abs(_currentHorizontalSpeed) > 0f &&
                        Mathf.Sign(_currentHorizontalSpeed) == dirX) _currentHorizontalSpeed = 0f;
                }
            }

            // --- vertical: techos y muros. Apoyado en una cuesta, una cara transitable NUNCA frena
            // aquí: bajando, el desplazamiento es casi todo vertical y contra la propia rampa, y
            // recortarlo convertiría cada descenso en un goteo de centímetros. En el aire, en
            // cambio, sí frena — es el aterrizaje, y es lo que impide atravesar un suelo fino en
            // una caída rápida o en un frame largo.
            if (Mathf.Abs(move.y) > 0.0001f)
            {
                float dirY = Mathf.Sign(move.y);
                var from = origin + new Vector3(move.x, 0f);
                float wanted = Mathf.Abs(move.y);
                float allowed = SweepAxisAgainstGround(from, solverSize, new Vector2(0f, dirY),
                                                       wanted, forgiveWalkable);

                if (allowed < wanted)
                {
                    move.y = allowed * dirY;
                    if (Mathf.Abs(_currentVerticalSpeed) > 0f &&
                        Mathf.Sign(_currentVerticalSpeed) == dirY) _currentVerticalSpeed = 0f;
                }
            }

            return move;
        }

        /// <summary>
        /// Un eje del barrido: cuánto se puede avanzar en <paramref name="dir"/> antes de tocar
        /// Ground. Un impacto a distancia 0 (la caja ya nacía dentro) se ignora aquí — no dice nada
        /// útil sobre el trayecto y, si se tomara, congelaría al personaje; ese caso es justo el que
        /// arregla <see cref="ResolveGroundPenetration"/>.
        /// </summary>
        private float SweepAxisAgainstGround(Vector3 origin, Vector2 size, Vector2 dir,
                                             float distance, bool forgiveWalkable)
        {
            if (distance <= 0.0001f || size.x <= 0f || size.y <= 0f) return distance;

            int count = Physics2D.BoxCast(origin, size, 0f, dir, _groundFilter, _groundCasts, distance);
            float best = distance;

            for (int i = 0; i < count; i++)
            {
                var candidate = _groundCasts[i];
                if (candidate.collider == null) continue;
                if (candidate.distance <= 0f) continue;
                if (candidate.distance >= best) continue;

                // Cuesta transitable con el apoyo ya resuelto: la sube ResolveSurface, no es muro.
                if (forgiveWalkable && Vector2.Angle(candidate.normal, Vector2.up) <= maxSlopeAngle)
                    continue;

                best = candidate.distance;
            }

            return best >= distance ? distance : Mathf.Max(0f, best - groundSkin);
        }

        /// <summary>
        /// Caja de sondeo del suelo sólido para una posición dada: el cuerpo encogido
        /// <c>groundClampInset</c> y con la base subida <c>_sweepLift</c>.
        ///
        /// Lo de la base no es opcional: apoyado en una rampa, la esquina baja de una caja plana
        /// penetra de verdad en la cuesta (0.1 × tan(ángulo): más de medio cuerpo a 80°). Sin subir
        /// la base, la red de seguridad leería cada rampa como una penetración y escupiría al
        /// jugador fuera de la cuesta en cada frame. Es la misma forma que usa el barrido, así que
        /// las dos mitades ven exactamente la misma geometría.
        /// </summary>
        private void GroundProbeBox(Vector3 position, out Vector2 center, out Vector2 size)
        {
            float lift = Mathf.Max(0f, _sweepLift);

            size = (Vector2)_characterBounds.size - Vector2.one * groundClampInset;
            size.y = Mathf.Max(0.05f, size.y - lift);
            center = (Vector2)(position + _characterBounds.center) + Vector2.up * (lift * 0.5f);
        }

        /// <summary>
        /// Red de seguridad completa, en dos mitades, porque el terreno de este juego viene en dos
        /// formas de collider muy distintas:
        ///
        ///  1. <b>Geometría rellena</b> (los tilemaps y las cajas del hub): se detecta estando
        ///     dentro, con <see cref="ResolveGroundPenetration"/>.
        ///  2. <b>Geometría de línea abierta</b> — el terreno de los mundos son SpriteShape, que
        ///     generan un <c>EdgeCollider2D</c> de polilínea ABIERTA y grosor 0
        ///     (<c>m_IsOpenEnded: 1</c>, <c>m_EdgeRadius: 0</c>). Una línea <b>no tiene dentro</b>:
        ///     en cuanto el personaje pasa al otro lado, no solapa absolutamente nada y ninguna
        ///     prueba de solape puede verlo. Ahí la única señal es el <b>cruce</b>: si el segmento
        ///     entre la posición buena anterior y la actual atraviesa la línea, se ha colado.
        ///
        /// Sin la mitad 2 la red era decorativa en todas las secciones de mundo — exactamente donde
        /// hacía falta.
        /// </summary>
        private void ValidateGroundContainment()
        {
            var current = transform.position;

            if (!_hasSafePosition)
            {
                _lastSafePosition = current;
                _hasSafePosition = true;
                return;
            }

            // Un teletransporte (cambio de sección, respawn) no es un cruce: se reancla y ya.
            // Cualquier frame legítimo se mueve muchísimo menos que esto.
            float teleport = Mathf.Max(4f, _characterBounds.size.y * 3f);
            if ((current - _lastSafePosition).sqrMagnitude > teleport * teleport)
            {
                _lastSafePosition = current;
                return;
            }

            if (ResolveGroundPenetration()) current = transform.position;

            if (CrossedGround(_lastSafePosition, current))
            {
                var recovered = _lastSafePosition;

                if (debugGroundClamp)
                {
                    _lastClampPush = recovered - current;
                    GroundProbeBox(recovered, out _lastClampCenter, out _lastClampSize);
                    _lastClampFrame = Time.frameCount;

                    Debug.LogWarning($"[GroundClamp] {Time.frameCount} el jugador ATRAVESÓ la línea " +
                                     $"de la capa Ground: devuelto {(recovered - current).magnitude:F3}u " +
                                     $"a la última posición buena | sistema={_moveSource} " +
                                     $"última escritura={_lastWrite} | apoyado={_colDown} " +
                                     $"vx={_currentHorizontalSpeed:F2} vy={_currentVerticalSpeed:F2} " +
                                     $"rampa={_slopeAngle:F1}° dt={Time.deltaTime * 1000f:F1}ms", this);
                }

                transform.position = recovered;
                _lastPosition = recovered;      // la corrección no es movimiento del personaje
                CalculateRayRanged();
                _currentHorizontalSpeed = 0f;
                if (_currentVerticalSpeed < 0f) _currentVerticalSpeed = 0f;
                return;                          // _lastSafePosition sigue siendo la buena
            }

            _lastSafePosition = transform.position;
        }

        /// <summary>
        /// ¿El segmento <paramref name="from"/> → <paramref name="to"/> atraviesa la capa Ground?
        ///
        /// Se mide entre CENTROS del personaje, no entre pies: caminando, el centro va media altura
        /// por encima de la superficie, así que un recorrido normal nunca roza la línea. Aun así,
        /// pasar por encima de una cresta afilada puede rozarla, y eso no es colarse: por eso un
        /// cruce sólo cuenta si además, en el destino, NO hay suelo transitable bajo los pies.
        /// </summary>
        private bool CrossedGround(Vector3 from, Vector3 to)
        {
            var a = (Vector2)(from + _characterBounds.center);
            var b = (Vector2)(to + _characterBounds.center);
            if ((b - a).sqrMagnitude <= 0.000001f) return false;

            if (Physics2D.Linecast(a, b, _groundFilter, _groundCasts) == 0) return false;

            // ¿Sigue habiendo superficie pisable justo debajo? Entonces se ha pasado por encima de
            // ella (cresta, lomo), que es movimiento legítimo.
            float reach = _characterBounds.extents.y + _detectionRayLength + groundSkin;
            if (Physics2D.Raycast(b, Vector2.down, _groundFilter, _groundCasts, reach) > 0)
            {
                for (int i = 0; i < _groundCasts.Count; i++)
                {
                    var below = _groundCasts[i];
                    if (below.collider == null || below.distance <= 0f) continue;
                    if (Vector2.Angle(below.normal, Vector2.up) <= maxSlopeAngle) return false;
                }
            }

            return true;
        }

        /// <summary>True si el personaje, colocado en <paramref name="position"/>, quedaría dentro
        /// de la capa Ground. Para validar una escritura de posición que no pasa por el barrido.</summary>
        private bool WouldOverlapGround(Vector3 position)
        {
            if (!groundIsImpassable || _groundLayer.value == 0) return false;

            GroundProbeBox(position, out var center, out var size);
            return Physics2D.OverlapBox(center, size, 0f, _groundFilter, _groundOverlaps) > 0;
        }

        /// <summary>
        /// Si el personaje está dentro de la capa Ground, lo saca por el camino más corto.
        ///
        /// Busca la salida probando la caja de sondeo desplazada en las cuatro direcciones
        /// cardinales y quedándose con la distancia más pequeña que la deja libre. No se usa
        /// <c>Collider2D.Distance</c> porque mide contra el collider REAL del personaje, que en una
        /// rampa está legítimamente penetrado, y la separación que devolvería sería la de expulsarlo
        /// de la cuesta. La caja de sondeo, en cambio, ya descuenta esa penetración legítima.
        ///
        /// Corre cada frame, así que lo que tiene que corregir nunca es más profundo que un frame
        /// de movimiento y el reajuste no se ve. <c>groundClampMaxPush</c> es sólo un tope de
        /// seguridad para que un caso extremo no se convierta en un tirón a media pantalla.
        /// </summary>
        private bool ResolveGroundPenetration()
        {
            if (!groundIsImpassable || _groundLayer.value == 0) return false;

            GroundProbeBox(transform.position, out var center, out var size);
            if (Physics2D.OverlapBox(center, size, 0f, _groundFilter, _groundOverlaps) == 0) return false;

            float step = Mathf.Max(0.02f, groundClampInset * 0.5f);
            int steps = Mathf.CeilToInt(groundClampMaxPush / step);

            Vector2 push = Vector2.zero;
            float shortest = float.MaxValue;

            for (int d = 0; d < 4; d++)
            {
                // Arriba primero: con empate, sacar al jugador hacia arriba es lo que menos se nota.
                var dir = d == 0 ? Vector2.up : d == 1 ? Vector2.left : d == 2 ? Vector2.right : Vector2.down;

                for (int i = 1; i <= steps; i++)
                {
                    float distance = i * step;
                    if (distance >= shortest) break;
                    if (Physics2D.OverlapBox(center + dir * distance, size, 0f, _groundFilter, _groundOverlaps) > 0)
                        continue;

                    shortest = distance;
                    push = dir * distance;
                    break;
                }
            }

            if (shortest == float.MaxValue)
            {
                if (debugGroundClamp)
                    Debug.LogWarning($"[GroundClamp] {Time.frameCount} DENTRO del suelo sólido y sin " +
                                     $"salida a menos de {groundClampMaxPush:F2}u (sistema: {_moveSource}, " +
                                     $"última escritura: {_lastWrite}). No se corrige.", this);
                return false;
            }

            transform.position += (Vector3)push;

            // El desplazamiento correctivo no es movimiento del personaje: si contara, 'Velocity'
            // daría un pico que falsearía el cálculo del ápex del salto.
            _lastPosition += (Vector3)push;
            CalculateRayRanged();

            // La velocidad que empujaba contra la superficie se anula; la perpendicular se conserva.
            if (push.y > 0f && _currentVerticalSpeed < 0f) _currentVerticalSpeed = 0f;
            if (push.y < 0f && _currentVerticalSpeed > 0f) _currentVerticalSpeed = 0f;
            if (push.x > 0f && _currentHorizontalSpeed < 0f) _currentHorizontalSpeed = 0f;
            if (push.x < 0f && _currentHorizontalSpeed > 0f) _currentHorizontalSpeed = 0f;

            if (debugGroundClamp)
            {
                _lastClampPush = push;
                _lastClampCenter = center + push;
                _lastClampSize = size;
                _lastClampFrame = Time.frameCount;

                Debug.LogWarning($"[GroundClamp] {Time.frameCount} el jugador estaba DENTRO de la capa " +
                                 $"Ground: sacado {push.magnitude:F3}u hacia {push.normalized} | " +
                                 $"sistema={_moveSource} última escritura={_lastWrite} | " +
                                 $"apoyado={_colDown} vx={_currentHorizontalSpeed:F2} " +
                                 $"vy={_currentVerticalSpeed:F2} rampa={_slopeAngle:F1}° " +
                                 $"sweepLift={_sweepLift:F3} dt={Time.deltaTime * 1000f:F1}ms", this);
            }

            return true;
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
            _surfaceResolvedGround = false;

            bool platforms = PlatformsActive;
            bool ground = enableSlopes && _colDown && _currentVerticalSpeed <= 0f;
            if (!platforms && !ground) return move;

            // Cuánto puede subir el pie en un frame: lo que gana la rampa más inclinada admitida a
            // lo largo del avance horizontal. Sólo apoyado; en el aire esto es sólo el aterrizaje.
            // Antes esto llevaba un tope fijo de 3 (≈71.57°) que en la práctica bajaba el límite
            // real muy por debajo de lo que 'maxSlopeAngle' decía permitir en el Inspector — a 80°
            // el pie se quedaba corto en cada paso y la rampa se sentía como pegajosa/resbaladiza.
            // El propio 'maxSlopeAngle' (con su Range de hasta 80°) ya es el único tope necesario.
            float rise = _colDown
                ? Mathf.Abs(move.x) * Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad) + groundSkin
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

                    // Hay apoyo de Ground alcanzable en la columna de destino: el pie va a acabar
                    // encima. Es lo que autoriza al barrido a NO frenar contra esta misma cuesta.
                    _surfaceResolvedGround = true;
                }
            }

            // La esquina de la caja va '_rayBuffer' más afuera que el último rayo de pies: en una
            // cuesta queda como mucho esa distancia × pendiente por debajo de la superficie.
            if (groundAngle > 0.5f)
            {
                sweepLift = _rayBuffer * Mathf.Tan(groundAngle * Mathf.Deg2Rad) + groundSkin;

                // A partir de ~70° 'sweepLift' crece muy rápido (tan diverge) y sin este tope podía
                // comerse toda la altura de 'solverSize' en MoveCharacter y dejarla en 0 o negativa,
                // lo que rompe el OverlapBox del barrido. Se deja siempre medio cuerpo de margen.
                sweepLift = Mathf.Min(sweepLift, _characterBounds.size.y * 0.5f - solverSkin);
            }

            if (debugStepUp) DebugReportSurfaceLift(move, feetY, rise, bestSurfaceY);

            if (bestSurfaceY == float.MinValue) return move;

            float targetFeetY = bestSurfaceY + groundSkin;
            if (targetFeetY <= destFeetY) return move;   // el paso no llega a la superficie: nada que corregir

            move.y += targetFeetY - destFeetY;
            if (_currentVerticalSpeed < 0f) _currentVerticalSpeed = 0f;
            return move;
        }

        /// <summary>
        /// Diagnóstico del OTRO camino por el que se sube a una superficie más alta, y el único que
        /// funciona con plataformas: el <c>rise</c> de <see cref="ResolveSurface"/>. Ese alcance vale
        /// <c>|move.x| × tan(maxSlopeAngle)</c>, o sea que <b>depende de cuánto se avanza en el frame</b>
        /// — de los FPS y de la velocidad del momento — y si el escalón es más alto que ese alcance,
        /// el rayo nace por debajo de la superficie y no la ve.
        ///
        /// Lanza un rayo extra, sólo con la depuración encendida, mirando bastante más arriba de lo
        /// que <c>rise</c> permite, para poder decir exactamente "había superficie a +X sobre los
        /// pies, el alcance de este frame era +Y, y por eso (no) se subió".
        /// </summary>
        private void DebugReportSurfaceLift(Vector3 move, float feetY, float rise, float bestSurfaceY)
        {
            if (Mathf.Abs(move.x) <= 0.0001f) return;

            int dir = move.x > 0f ? 1 : -1;
            float probeX = (dir > 0 ? _raysDown.End.x : _raysDown.Start.x) + move.x;
            float scan = stepThresholdHeight + Mathf.Abs(move.x) + groundSkin;

            var ahead = Physics2D.Raycast(new Vector2(probeX, feetY + scan), Vector2.down,
                                          scan + groundSkin, _groundLayer | _platformLayer);
            if (!ahead) return;

            float aheadRise = ahead.point.y - feetY;
            if (aheadRise <= groundSkin) return;   // está al nivel del pie o por debajo: no es un escalón

            bool taken = bestSurfaceY > float.MinValue && bestSurfaceY >= ahead.point.y - groundSkin;
            Debug.Log($"[StepUp/Superficie] {Time.frameCount} " +
                      $"superficie delante a +{aheadRise:F3} sobre los pies → {(taken ? "SUBIDO" : "NO SUBIDO")} | " +
                      $"alcance rise={rise:F3} (moveX={move.x:F3}, dt={Time.deltaTime * 1000f:F1}ms, " +
                      $"maxSlopeAngle={maxSlopeAngle:F0}°) | apoyado={_colDown} vy={_currentVerticalSpeed:F2} | " +
                      $"'{ahead.collider.name}' ({LayerMask.LayerToName(ahead.collider.gameObject.layer)})", this);
        }

        // ================================================================ contacto

        private void OnCollisionEnter2D(Collision2D collision) => EvaluateStepContacts(collision, entering: true);

        /// <summary>
        /// Los contactos se vuelven a leer en cada paso de física mientras se siga tocando el
        /// obstáculo. Eso es lo que hace que apoyarse contra un escalón termine subiéndolo SIEMPRE:
        /// no hace falta acertar el frame exacto del choque, como sí hacía falta con los rayos.
        /// </summary>
        private void OnCollisionStay2D(Collision2D collision) => EvaluateStepContacts(collision, entering: false);

        // ================================================================ varios

        private void OnDied()
        {
            _controlEnabled = false;
            _input = default;
            _currentHorizontalSpeed = 0f;
            _currentVerticalSpeed = 0f;
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

            if (groundStepMinFaceAngle < maxSlopeAngle) groundStepMinFaceAngle = maxSlopeAngle;

            RebuildGroundFilter();
        }

        /// <summary>
        /// Filtro compartido por TODO lo que consulta el suelo sólido: sólo la capa Ground y sin
        /// disparadores. Los disparadores importan porque el proyecto tiene
        /// <c>Queries Hit Triggers</c> activado, y una zona de disparo que alguien ponga en la capa
        /// Ground no debe frenar ni expulsar al jugador.
        /// </summary>
        private void RebuildGroundFilter()
        {
            _groundFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = _groundLayer
            };
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + _characterBounds.center, _characterBounds.size);

            if (debugStepUp)
            {
                // Línea de los pies y línea del umbral: todo contacto que caiga entre las dos es
                // escalón, todo el que caiga por encima de la amarilla es pared.
                var stepBounds = new Bounds(transform.position, _characterBounds.size);
                float margin = 0.25f;
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(new Vector3(stepBounds.min.x - margin, stepBounds.min.y),
                                new Vector3(stepBounds.max.x + margin, stepBounds.min.y));
                Gizmos.color = Color.yellow;
                float thresholdY = stepBounds.min.y + stepThresholdHeight;
                Gizmos.DrawLine(new Vector3(stepBounds.min.x - margin, thresholdY),
                                new Vector3(stepBounds.max.x + margin, thresholdY));
            }

            // Última corrección de la red de seguridad de Ground: caja magenta donde quedó el
            // personaje tras sacarlo y flecha con el empujón que hizo falta. Si esto se ve alguna
            // vez durante una partida, queda un camino que se salta el barrido.
            if (debugGroundClamp && _lastClampFrame != int.MinValue && Time.frameCount - _lastClampFrame < 120)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(_lastClampCenter, _lastClampSize);
                Gizmos.DrawRay((Vector3)(_lastClampCenter - _lastClampPush), _lastClampPush);
            }

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
