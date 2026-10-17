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
    ///  - <b>Terreno plano</b>: el juego no tiene rampas ni escalones (los niveles son celdas con esquinas
    ///    cuadradas), así que no hay seguimiento de rampas ni subida automática de bordes. Una superficie más
    ///    alta, aunque sea un poco, se salta; nunca se sube sola.
    ///
    /// El Rigidbody2D se mantiene (forzado a Kinematic) sólo para que sigan llegando los eventos
    /// de colisión / trigger a los enemigos, proyectiles y zonas de daño.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public class PlayerMovement : MonoBehaviour, IKnockbackReceiver, ISoundEventSource, ICooldownSource
    {
        /// <summary>OnJump / OnAirJump / OnDash / Footstep / OnLand. Ver <see cref="SoundEmitter"/>.</summary>
        public event System.Action<SoundTrigger> SoundTriggered;

        public void DeclareSoundTriggers(List<SoundTrigger> into)
        {
            into.Add(SoundTrigger.Footstep);
            into.Add(SoundTrigger.OnJump);
            if (maxAirJumps > 0) into.Add(SoundTrigger.OnAirJump);
            if (allowDash) into.Add(SoundTrigger.OnDash);
            into.Add(SoundTrigger.OnLand);
        }

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

        /// <summary>True mientras se desliza agarrado a una pared (para la animación).</summary>
        public bool IsWallSliding => _wallSide != 0;

        /// <summary>-1 pared a la izquierda, 1 a la derecha, 0 sin pared. Mientras desliza se mira al lado contrario.</summary>
        public int WallSide => _wallSide;

        /// <summary>True mientras está clavado a la pared tras chocar con un dash (no cae).</summary>
        public bool IsWallStuck => _wallSide != 0 && _wallStickTimer > 0f;

        /// <summary>Se agarra a una pared (lado de la pared). Para la animación / el efecto de agarre.</summary>
        public event System.Action<int> WallGrabbed;

        /// <summary>Un dash se ha clavado en la pared (lado de la pared).</summary>
        public event System.Action<int> WallStuck;

        /// <summary>Salto de pared (dirección de salida: -1 izquierda, 1 derecha).</summary>
        public event System.Action<int> WallJumped;

        /// <summary>True mientras dura un dash.</summary>
        public bool IsDashing => _dashTimer > 0f;

        /// <summary>True mientras el jugador sale despedido por un golpe (no controla).</summary>
        public bool IsKnockedBack => _knockbackTimer > 0f;

        /// <summary>True mientras dura un picado (<see cref="TryStartDive"/>): la velocidad la fija el picado hasta tocar suelo.</summary>
        public bool IsDiving => _diving;

        /// <summary>Entrada abajo mantenida (stick, teclas o táctil).</summary>
        public bool DownHeld => _input.DownHeld || _input.Y < -0.5f;

        /// <summary>El picado ha tocado suelo (posición de los pies). Lo usa el ataque en picado para el impacto.</summary>
        public event System.Action<Vector2> DiveLanded;

        /// <summary>Se dispara al saltar desde el suelo (o con coyote / salto guardado). Lo usa el VFX de los pies.</summary>
        public event System.Action Jumped;

        /// <summary>Se dispara al usar un salto en el aire (doble salto). Lo usa el VFX de los pies.</summary>
        public event System.Action AirJumped;

        /// <summary>Se dispara al iniciar un dash.</summary>
        public event System.Action<int> Dashed;

        /// <summary>Lo que dura un dash ahora mismo (el de base por el multiplicador de distancia de los items).</summary>
        public float DashDuration => dashDuration * PlayerStats.Multiplier(PlayerStat.DashDistance);

        // ---------------------------------------------------------------- HUD (ICooldownSource: el dash)

        public CooldownSlot Slot => CooldownSlot.Dash;

        public bool CooldownActive => allowDash;

        /// <summary>Bloqueado = en el aire sin dashes aéreos que gastar (se recarga al tocar suelo).</summary>
        public CooldownState Cooldown => new CooldownState
        {
            Remaining = Mathf.Max(0f, _dashCooldownTimer),
            Duration = _dashCooldownDuration,
            Blocked = !_colDown && _airDashesUsed >= maxAirDashes,
        };

        public Sprite CooldownIcon => null;

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
        [Tooltip("Segundos mínimos en el aire para que al tocar suelo suene OnLand (los bordes de un escalón no cuentan).")]
        [SerializeField] private float landMinAirTime = 0.15f;

        // ---------------------------------------------------------------- colisión

        // La caja del personaje es UNA: su BoxCollider2D (offset + tamaño). Con ella se mueve, se apoya, choca y
        // recibe golpes. Se ajusta con el botón "Edit Collider" del BoxCollider2D, no aquí.
        private BoxCollider2D _box;

        private Bounds Box
        {
            get
            {
                if (_box == null) _box = GetComponent<BoxCollider2D>();
                return _box != null ? new Bounds(_box.offset, _box.size) : new Bounds(Vector3.zero, new Vector3(0.6f, 1.6f));
            }
        }

        /// <summary>La caja del personaje en mundo si estuviera en <paramref name="position"/>.</summary>
        private Bounds BodyAt(Vector3 position) => new Bounds(position + Box.center, Box.size);

        [Header("COLLISION")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private int _detectorCount = 3;
        [SerializeField] private float _detectionRayLength = 0.1f;
        [SerializeField] [Range(0.1f, 0.3f)] private float _rayBuffer = 0.1f;

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

        // ---------------------------------------------------------------- suelo sólido

        [Header("SUELO SÓLIDO — la capa Ground es infranqueable")]
        [Tooltip("Contención de la capa Ground: barrido real del trayecto (no sólo del punto de " +
                 "destino) antes de mover, y corrección en LateUpdate si aun así se ha penetrado.\n\n" +
                 "Afecta ÚNICAMENTE a la capa 'Ground Layer' de arriba. Las plataformas y todo lo " +
                 "demás se mueven exactamente igual con esto encendido o apagado.")]
        [SerializeField] private bool groundIsImpassable = true;

        [Tooltip("Cuánto se encoge la caja con la que la red de seguridad comprueba si el personaje " +
                 "está dentro del suelo sólido. Debe ser mayor que el 'Default Contact Offset' de " +
                 "Physics 2D (0.01) para que estar apoyado no cuente como penetración.")]
        [Range(0.02f, 0.2f)]
        [SerializeField] private float groundClampInset = 0.04f;

        [Tooltip("Desnivel máximo (unidades) que se sube andando sin saltar: el escalón mínimo entre una " +
                 "plataforma y el suelo contiguo (el collider de la plataforma va unos píxeles más bajo). " +
                 "0 = desactivado.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float maxStepHeight = 0.12f;

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
        [Tooltip("Segundos que se recuerda un dash pulsado antes de poder hacerlo. Cuenta DESDE QUE ACABA el dash en curso: pulsar durante un dash dispara el siguiente justo al terminar, siempre en el mismo frame (doble dash consistente).")]
        [SerializeField, Range(0f, 0.3f)] private float dashBuffer = 0.12f;
        [Tooltip("Congela la caída durante el dash (dash horizontal puro).")]
        [SerializeField] private bool dashIgnoresGravity = true;

        // Como la garra de mantis de Hollow Knight: agarrarse a una pared del aire la desliza despacio; saltar sale en
        // diagonal hacia el lado contrario con el control bloqueado un instante; el dash sale recto hacia fuera; y
        // agarrarse recarga el doble salto y el dash. Sólo las paredes de la capa Ground (las plataformas no son pared).
        [Header("WALL — deslizar por la pared y salto de pared")]
        [SerializeField] private bool allowWallSlide = true;
        [Tooltip("Velocidad máxima de caída deslizando (unidades/s). La caída libre llega a 40.")]
        [Min(0f)] [SerializeField] private float wallSlideSpeed = 7f;
        [Tooltip("Velocidad horizontal con la que el salto de pared despega hacia el lado contrario.")]
        [Min(0f)] [SerializeField] private float wallJumpHorizontalSpeed = 15f;
        [Tooltip("Impulso vertical del salto de pared. 0 o menos = el del salto normal.")]
        [SerializeField] private float wallJumpHeight = 27f;
        [Tooltip("Segundos tras el salto de pared en que la dirección no cuenta (sale despegado aunque se apunte a la pared). " +
                 "Corto: con la dirección hacia la pared se vuelve a ella y se trepa a saltos.")]
        [Range(0f, 0.4f)] [SerializeField] private float wallJumpInputLock = 0.12f;
        [Tooltip("Segundos tras soltarse de la pared en que aún vale el salto de pared (como el coyote time).")]
        [Range(0f, 0.3f)] [SerializeField] private float wallCoyoteTime = 0.1f;
        [Tooltip("Segundos que se queda clavado (sin caer, aunque no se apunte) al chocar un dash contra la pared en el aire. 0 = nada.")]
        [Range(0f, 1.5f)] [SerializeField] private float wallDashStickTime = 0.5f;
        [Tooltip("Sacudida de cámara al clavarse con el dash: amplitud (unidades) y duración (s). 0 = sin sacudida.")]
        [Min(0f)] [SerializeField] private float wallStickShakeAmplitude = 0.15f;
        [Min(0f)] [SerializeField] private float wallStickShakeDuration = 0.12f;
        [Tooltip("Sacudida de cámara al salir de la pared con un dash. 0 = sin sacudida.")]
        [Min(0f)] [SerializeField] private float wallDashOutShakeAmplitude = 0.08f;
        [Min(0f)] [SerializeField] private float wallDashOutShakeDuration = 0.1f;

        // ---------------------------------------------------------------- movimiento

        [Header("ENEMIGOS — empujar con el cuerpo")]
        [Tooltip("Velocidad máxima (unidades/s) al andar contra un enemigo: a la que lo empuja. Andando normal se va a 13. " +
                 "0 = no lo mueve nada (se queda pegado a él).")]
        [Min(0f)] [SerializeField] private float enemyPushSpeed = 1f;

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
        private bool _diving;
        private Vector2 _diveVelocity;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private float _dashCooldownDuration;
        private int _dashDirection = 1;

        // pared
        private int _wallSide;                 // -1 pared a la izquierda, 1 a la derecha, 0 sin agarre
        private int _lastWallSide;
        private float _wallLeftTime = float.MinValue;
        private float _wallJumpLockTimer;
        private int _wallJumpDirection;
        private int _pendingStick;            // pared contra la que acaba de chocar un dash en el aire
        private int _wallStickSide;
        private float _wallStickTimer;

        private ContactFilter2D _enemyFilter;
        private readonly List<RaycastHit2D> _enemyHits = new List<RaycastHit2D>(8);

        private FrameInput _input;
        private Vector3 _lastPosition;
        private float _currentHorizontalSpeed, _currentVerticalSpeed;
        private bool _controlEnabled = true;
        private float _footstepTimer;
        private float _airTime;

        // Hack del original: los colliders no están del todo listos al arrancar.
        private bool _active;

        /// <summary>Desde cuánto por encima de los pies sale el rayo de suelo (ver <see cref="ProbeGround"/>).</summary>
        private const float FootProbeRise = 0.1f;

        private RayRange _raysUp, _raysRight, _raysDown, _raysLeft;
        private bool _colUp, _colRight, _colDown, _colLeft;
        private float _timeLeftGrounded;

        // suelo / plataformas
        private float _groundDistance = float.MaxValue;
        private bool _onPlatform;
        private float _dropTimer;
        private Collider2D _dropCollider;    // la plataforma concreta que se está atravesando
        private Collider2D _groundCollider;  // la que se pisa ahora mismo

        // suelo sólido (Ground): barrido y red de seguridad
        private ContactFilter2D _groundFilter;
        private readonly List<RaycastHit2D> _groundCasts = new List<RaycastHit2D>(8);
        private readonly Collider2D[] _groundOverlaps = new Collider2D[4];
        private Vector3 _lastSafePosition;                  // última posición validada fuera del suelo sólido
        private bool _hasSafePosition;
        private MoveSource _moveSource;                     // qué sistema conduce este frame
        private MoveSource _lastWrite;                      // qué escritura de posición fue la última
        private Vector2 _lastClampPush, _lastClampCenter, _lastClampSize;
        private int _lastClampFrame = int.MinValue;

        /// <summary>
        /// Quién ha movido al personaje. Sólo sirve para que el aviso de <c>debugGroundClamp</c>
        /// pueda decir qué sistema dejó al jugador dentro del suelo, en vez de limitarse a decir
        /// que pasó.
        /// </summary>
        private enum MoveSource { None, Walk, Dash, Knockback, GroundSnap, Solver }

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

            // Al dejarse caer se ignora LA PLATAFORMA CONCRETA que se pisaba, no una altura: cualquier OTRA
            // plataforma sigue frenando la caída, incluida la de justo debajo.
            if (_dropCollider != null && hit.collider == _dropCollider) return false;

            return true;
        }

        // salto
        private bool _coyoteUsable;
        private bool _endedJumpEarly = true;
        private float _apexPoint;   // 1 en el ápex del salto
        private float _fallSpeed;
        private float _lastJumpPressed = float.MinValue;
        private float _dashBufferUntil = float.MinValue;   // hasta cuándo vale un dash pulsado y todavía sin gastar

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

            // Un cuerpo dormido deja de reportar contactos y triggers: quieto, un enemigo o una zona de daño
            // que se le acerque no llegaría a notarlo.
            _body.sleepMode = RigidbodySleepMode2D.NeverSleep;

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
                TouchInput.ConsumeJumpRelease();
                TouchInput.ConsumeDash();
                _input = default;
                _lastJumpPressed = float.MinValue;
                _dashBufferUntil = float.MinValue;
                _currentHorizontalSpeed = 0f;
                _dashTimer = 0f;
                _knockbackTimer = 0f;
                _diving = false;
                _wallSide = 0;
                _wallStickTimer = 0f;
                _wallJumpLockTimer = 0f;
                _moveSource = MoveSource.None;
                ClearDropThrough();
                return;
            }

            // Durante el retroceso no se lee la entrada: el golpe manda. La cola táctil se vacía
            // igualmente para que no se dispare un salto acumulado al recuperar el control.
            if (IsKnockedBack)
            {
                TouchInput.ConsumeJump();
                TouchInput.ConsumeJumpRelease();
                TouchInput.ConsumeDash();
                _input = default;
                _dashBufferUntil = float.MinValue;   // un golpe anula lo que se había pulsado
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

            UpdateWall();

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
            else if (_diving)
            {
                _moveSource = MoveSource.Dash;
                UpdateDive();     // el picado manda hasta tocar suelo
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
            UpdateLanding();
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
            bool jumpUp = (_jumpAction != null && _jumpAction.WasReleasedThisFrame()) || TouchInput.ConsumeJumpRelease();
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
            // El dash se recuerda hasta "dashBuffer" segundos DESPUÉS de que acabe el que está en curso: si no, lo pulsado
            // durante el dash se perdía y el siguiente salía en el frame exacto en que el dedo cayera (caída variable).
            if (_input.DashDown) _dashBufferUntil = Time.time + dashBuffer + Mathf.Max(0f, _dashTimer);

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

            _colUp = RunDetection(_raysUp);
            _colLeft = RunDetection(_raysLeft);
            _colRight = RunDetection(_raysRight);

            // Ojo: arriba / izquierda / derecha miran SÓLO la capa sólida. Es lo que hace que una
            // plataforma se pueda atravesar de lado y de abajo arriba sin ningún caso especial.
            bool RunDetection(RayRange range) =>
                EvaluateRayPositions(range).Any(point => Physics2D.Raycast(point, range.Dir, _detectionRayLength, _groundLayer));
        }

        /// <summary>
        /// Sensor de suelo: si hay apoyo a menos de <c>_detectionRayLength</c> bajo los pies, la distancia exacta
        /// (para <see cref="SnapToGround"/>) y si ese apoyo es una plataforma de un solo sentido. Sólo cuentan caras
        /// que miran hacia arriba: el terreno es plano, sin rampas.
        /// </summary>
        private bool ProbeGround()
        {
            _groundDistance = float.MaxValue;
            _onPlatform = false;
            _groundCollider = null;

            float range = _detectionRayLength;
            bool platformsActive = PlatformsActive;
            float bestDistance = float.MaxValue;
            bool bestIsPlatform = false;
            Collider2D bestCollider = null;

            foreach (var point in EvaluateRayPositions(_raysDown))
            {
                // El rayo de Ground nace FootProbeRise por encima de los pies: el barrido de caída deja los pies
                // ~0.02 dentro del suelo (su caja va encogida 'solverSkin'), y un rayo nacido bajo la línea de un
                // EdgeCollider no la ve — se quedaba "cayendo" sobre suelo firme sin poder saltar. La distancia
                // se devuelve medida desde los pies: negativa = hundido, y SnapToGround lo saca.
                var groundHit = Physics2D.Raycast(point + Vector2.up * FootProbeRise, Vector2.down,
                                                  range + FootProbeRise, _groundLayer);
                if (groundHit) groundHit.distance -= FootProbeRise;
                Consider(groundHit, false);

                if (platformsActive)
                {
                    var plat = Physics2D.Raycast(point, Vector2.down, range, _platformLayer);
                    if (AcceptPlatformHit(plat)) Consider(plat, true);
                }
            }

            bool grounded = bestDistance <= range;
            if (grounded)
            {
                _groundDistance = bestDistance;
                _onPlatform = bestIsPlatform;
                _groundCollider = bestCollider;
            }
            return grounded;

            void Consider(RaycastHit2D hit, bool isPlatform)
            {
                if (!hit || hit.normal.y <= 0.5f) return;   // sólo caras de suelo, nunca un canto o una pared
                if (hit.distance >= bestDistance) return;
                bestDistance = hit.distance;
                bestIsPlatform = isPlatform;
                bestCollider = hit.collider;
            }
        }

        private void CalculateRayRanged()
        {
            var b = BodyAt(transform.position);

            // Los rayos de suelo cubren el MISMO ancho que la caja del barrido (encogida 'solverSkin'), no
            // '_rayBuffer' hacia dentro: si no, en el borde de una esquina cuadrada la caja se apoya en el
            // canto pero ningún rayo ve suelo, y el personaje se queda colgado "cayendo" sin caer ni saltar.
            float footInset = solverSkin * 0.5f;
            _raysDown = new RayRange(b.min.x + footInset, b.min.y, b.max.x - footInset, b.min.y, Vector2.down);
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
            // Recién saltado de una pared: sale despegado hacia el otro lado aunque se apunte a la pared.
            if (IsWallStuck)
            {
                _currentHorizontalSpeed = 0f;   // clavado por un dash: sólo se sale saltando o con otro dash
            }
            else if (_wallJumpLockTimer > 0f)
            {
                _wallJumpLockTimer -= Time.deltaTime;
                _currentHorizontalSpeed = _wallJumpDirection * wallJumpHorizontalSpeed;
            }
            else if (_input.X != 0)
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

            // Contra una pared se frena. No hay subida automática: un borde, por bajo que sea, se salta.
            if ((_currentHorizontalSpeed > 0 && _colRight) || (_currentHorizontalSpeed < 0 && _colLeft))
                _currentHorizontalSpeed = 0;

            // Contra un enemigo apenas avanza: así casi no lo empuja (ver enemyPushSpeed).
            if (Mathf.Abs(_currentHorizontalSpeed) > enemyPushSpeed && EnemyAhead(_currentHorizontalSpeed > 0f ? 1 : -1))
                _currentHorizontalSpeed = Mathf.Sign(_currentHorizontalSpeed) * enemyPushSpeed;
        }

        /// <summary>
        /// ¿Hay un enemigo vivo pegado a la caja del jugador en esa dirección? El jugador es cinemático y mueve su
        /// transform: contra un cuerpo dinámico la física lo aparta como si el jugador tuviera masa infinita, así que andar
        /// contra un enemigo lo arrastraba. Frenando aquí, el choque sigue existiendo (el daño por contacto lo necesita)
        /// pero el enemigo apenas se mueve. El dash no pasa por aquí (lo atraviesa, <see cref="PlayerDashCombat"/>).
        /// </summary>
        private bool EnemyAhead(int dir)
        {
            var b = BodyAt(transform.position);
            var size = new Vector2(b.size.x * 0.9f, b.size.y * 0.8f);
            float distance = 0.06f + Mathf.Abs(_currentHorizontalSpeed) * Time.deltaTime;
            _enemyFilter.SetLayerMask(~(_groundLayer.value | _platformLayer.value));
            _enemyFilter.useTriggers = false;
            int n = Physics2D.BoxCast(b.center, size, 0f, new Vector2(dir, 0f), _enemyFilter, _enemyHits, distance);
            for (int i = 0; i < n; i++)
            {
                var c = _enemyHits[i].collider;
                if (c == null || c.attachedRigidbody == _body) continue;
                if ((c.bounds.center.x - b.center.x) * dir <= 0f) continue;   // está detrás: alejarse no se frena
                var h = c.GetComponentInParent<Health>();
                if (h != null && !h.IsDead && Teams.Of(h.gameObject) != Team.Player) return true;
            }
            return false;
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

                // Agarrado a la pared se desliza despacio (subiendo no se toca: el impulso de un salto sigue); clavado por un
                // dash, no cae.
                if (_wallSide != 0 && _currentVerticalSpeed < -wallSlideSpeed) _currentVerticalSpeed = -wallSlideSpeed;
                if (IsWallStuck && _currentVerticalSpeed < 0f) _currentVerticalSpeed = 0f;
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
            else if (!_colDown && CanWallJump)
            {
                WallJump();
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

        /// <summary>
        /// Salto de pared: agarrado (o recién soltado, <see cref="wallCoyoteTime"/>) y pulsando salto; agarrado también vale
        /// un salto pulsado justo antes de tocarla (jump buffer). No gasta el doble salto.
        /// </summary>
        private bool CanWallJump
        {
            get
            {
                if (!allowWallSlide) return false;
                if (_wallSide != 0) return _input.JumpDown || _lastJumpPressed + _jumpBuffer > Time.time;
                return _input.JumpDown && _lastWallSide != 0 && Time.time - _wallLeftTime <= wallCoyoteTime;
            }
        }

        private void WallJump()
        {
            int away = -(_wallSide != 0 ? _wallSide : _lastWallSide);
            _wallSide = 0;
            _wallStickTimer = 0f;
            _wallLeftTime = float.MinValue;   // un salto de pared por agarre
            _dashTimer = 0f;
            Jump(wallJumpHeight > 0f ? wallJumpHeight : _jumpHeight);
            _wallJumpDirection = away;
            _wallJumpLockTimer = wallJumpInputLock;
            _currentHorizontalSpeed = away * wallJumpHorizontalSpeed;
            Facing = away;
            if (_sprite != null) _sprite.flipX = away < 0;
            SoundTriggered?.Invoke(SoundTrigger.OnJump);
            WallJumped?.Invoke(away);
        }

        // ================================================================ pared

        /// <summary>
        /// Agarre a la pared (cada frame, tras los sensores). Se agarra en el aire tocando una pared de la capa Ground y
        /// <b>sólo mientras se apunta hacia ella</b>: al soltar la dirección se cae con normalidad. Agarrado: no avanza en horizontal, mira hacia fuera, cae a <see cref="wallSlideSpeed"/> como mucho y
        /// recupera el doble salto y el dash (y su cooldown).
        /// </summary>
        private void UpdateWall()
        {
            int touching = _colLeft && _colRight
                ? (_input.X < -0.01f ? -1 : _input.X > 0.01f ? 1 : _wallSide)
                : _colLeft ? -1 : _colRight ? 1 : 0;
            int was = _wallSide;
            bool can = allowWallSlide && touching != 0 && !_colDown && !IsKnockedBack && !_diving && _dashTimer <= 0f && _wallJumpLockTimer <= 0f;

            // Un dash que acaba de chocar con la pared en el aire la clava un momento, se apunte o no.
            if (_pendingStick != 0 && can && touching == _pendingStick && wallDashStickTime > 0f)
            {
                _wallStickSide = _pendingStick;
                _wallStickTimer = wallDashStickTime;
                CameraFollow.ShakeAll(wallStickShakeAmplitude, wallStickShakeDuration);
                WallStuck?.Invoke(_wallStickSide);
            }
            _pendingStick = 0;
            if (_wallStickTimer > 0f) _wallStickTimer -= Time.deltaTime;
            bool stuck = _wallStickTimer > 0f && can && touching == _wallStickSide;
            if (!stuck) _wallStickTimer = 0f;

            if (!can) _wallSide = 0;
            else if (stuck) _wallSide = _wallStickSide;
            else _wallSide = _input.X * touching > 0.01f ? touching : 0; // soltar la dirección (o apuntar fuera) suelta

            if (_wallSide != 0)
            {
                if (was == 0)
                {
                    _dashCooldownTimer = 0f;   // agarrarse devuelve el dash al momento
                    WallGrabbed?.Invoke(_wallSide);
                }
                _airJumpsUsed = 0;
                _airDashesUsed = 0;
                _lastWallSide = _wallSide;
                _currentHorizontalSpeed = 0f;
                Facing = -_wallSide;
                if (_sprite != null) _sprite.flipX = Facing < 0;
            }
            else if (was != 0)
            {
                _wallLeftTime = Time.time;
            }
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
            _coyoteUsable = false;
            _timeLeftGrounded = float.MinValue;
            _lastJumpPressed = float.MinValue;   // que el buffer no dispare un salto al frame siguiente

            if (_currentVerticalSpeed > 0f) _currentVerticalSpeed = 0f;
            return true;
        }

        /// <summary>
        /// Suelta la plataforma que se estaba atravesando en cuanto se ha dejado atrás: cuando la
        /// cabeza del personaje queda por debajo de ella. Se mide contra los límites del collider,
        /// así que hay que haber bajado del todo, no unos centímetros.
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

            float headY = transform.position.y + Box.center.y + Box.extents.y;
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
            bool wantsDash = _input.DashDown || Time.time <= _dashBufferUntil;
            if (!allowDash || !wantsDash || _dashTimer > 0f || _dashCooldownTimer > 0f) return;
            // Recién salido de un borde (coyote) cuenta como dash de suelo: si no, pulsar justo al despegar gastaba el dash aéreo
            // y el doble dash dependía de qué frame detectaba el suelo perdido.
            bool fromGround = _colDown || CanUseCoyote;
            if (!fromGround && _airDashesUsed >= maxAirDashes) return;
            _dashBufferUntil = float.MinValue;

            if (!fromGround) _airDashesUsed++;

            // Se lanza hacia donde se está apuntando; si no hay input, hacia donde se mira. Desde la pared, recto hacia fuera.
            _dashDirection = _wallSide != 0 ? -_wallSide : Mathf.Abs(_input.X) > 0.01f ? (int)Mathf.Sign(_input.X) : Facing;
            if (_wallSide != 0) CameraFollow.ShakeAll(wallDashOutShakeAmplitude, wallDashOutShakeDuration);
            _wallSide = 0;
            _wallStickTimer = 0f;
            _wallJumpLockTimer = 0f;
            Facing = _dashDirection;
            if (_sprite != null) _sprite.flipX = _dashDirection < 0;

            // Más distancia = más duración a la misma velocidad (el dash se sigue viendo igual de rápido).
            float duration = DashDuration;
            _dashTimer = duration;
            _dashCooldownTimer = dashCooldown + duration;
            _dashCooldownDuration = _dashCooldownTimer;
            // Dash de suelo ya despegado (coyote): es lo mismo que salir del borde en pleno dash, que ya devuelve el cooldown.
            if (!_colDown) _dashCooldownTimer = 0f;

            SoundTriggered?.Invoke(SoundTrigger.OnDash);
            Dashed?.Invoke(_dashDirection);
        }

        private void UpdateDash()
        {
            _dashTimer -= Time.deltaTime;

            _currentHorizontalSpeed = dashSpeed * _dashDirection;
            if (dashIgnoresGravity) _currentVerticalSpeed = 0f;

            // No atravesar paredes durante el dash.
            if (_currentHorizontalSpeed == 0f ||
                (_currentHorizontalSpeed > 0 && _colRight) || (_currentHorizontalSpeed < 0 && _colLeft))
            {
                if (_currentHorizontalSpeed != 0f && !_colDown) _pendingStick = _currentHorizontalSpeed > 0f ? 1 : -1; // se clava
                _currentHorizontalSpeed = 0f;
                _dashTimer = 0f;
            }

            if (_dashTimer <= 0f)
            {
                // Se sale del dash sin conservar toda la velocidad, para que no patine.
                _currentHorizontalSpeed = Mathf.Clamp(_currentHorizontalSpeed, -MoveClamp, MoveClamp);
            }
        }

        // ================================================================ picado

        /// <summary>
        /// Empieza un picado: sólo en el aire. Hasta tocar suelo la velocidad es
        /// <paramref name="velocity"/> (sin gravedad ni control); al aterrizar se lanza
        /// <see cref="DiveLanded"/>. Corta el dash en curso. Un golpe recibido lo cancela.
        /// </summary>
        public bool TryStartDive(Vector2 velocity)
        {
            if (_diving || _colDown || IsKnockedBack || velocity.y >= 0f) return false;
            _diving = true;
            _diveVelocity = velocity;
            _dashTimer = 0f;
            _endedJumpEarly = true;
            _coyoteUsable = false;
            if (Mathf.Abs(velocity.x) > 0.01f) Facing = velocity.x < 0f ? -1 : 1;
            return true;
        }

        private void UpdateDive()
        {
            _currentHorizontalSpeed = _diveVelocity.x;
            _currentVerticalSpeed = _diveVelocity.y;
            if ((_currentHorizontalSpeed > 0 && _colRight) || (_currentHorizontalSpeed < 0 && _colLeft))
                _currentHorizontalSpeed = 0f;

            if (!_colDown) return;
            _diving = false;
            _currentVerticalSpeed = 0f;
            _currentHorizontalSpeed = Mathf.Clamp(_currentHorizontalSpeed * 0.2f, -MoveClamp, MoveClamp);
            var feet = _collider != null ? new Vector2(transform.position.x, _collider.bounds.min.y) : (Vector2)transform.position;
            DiveLanded?.Invoke(feet);
        }

        // ================================================================ retroceso

        /// <summary>
        /// Corta el dash en curso (recibir daño en pleno dash). Conserva algo de inercia, como al
        /// terminar un dash normal. No hace nada con "dash imparable" (item).
        /// </summary>
        public void CancelDash()
        {
            if (_dashTimer <= 0f || PlayerStats.DashUnstoppable) return;
            _dashTimer = 0f;
            _currentHorizontalSpeed = Mathf.Clamp(_currentHorizontalSpeed, -MoveClamp, MoveClamp);
        }

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
            // Con "dash imparable" (item) un golpe en pleno dash no lo corta ni empuja.
            if (_dashTimer > 0f && PlayerStats.DashUnstoppable) return;

            _knockbackTimer = Mathf.Max(_knockbackTimer, duration);

            _currentHorizontalSpeed = velocity.x;
            _currentVerticalSpeed = velocity.y;

            // Un golpe corta el dash y el salto en curso, y devuelve el control aéreo: si no,
            // salir despedido en pleno dash dejaría al jugador cayendo sin recursos.
            _dashTimer = 0f;
            _endedJumpEarly = true;
            _diving = false;   // un golpe corta también el picado
            _wallSide = 0;     // …y suelta la pared
            _wallStickTimer = 0f;
            _wallJumpLockTimer = 0f;

            if (velocity.y > 0f) _coyoteUsable = false;
        }

        /// <summary>
        /// Devuelve saltos y dashes aéreos ya gastados (recompensa del parry en el aire). Con
        /// <paramref name="allowBeyondMax"/> se pueden acumular por encima del máximo normal hasta
        /// tocar suelo; si no, como mucho se vuelve a tenerlos todos.
        /// </summary>
        public void RefundAirMoves(int jumps, int dashes, bool resetDashCooldown, bool allowBeyondMax)
        {
            int jumpFloor = allowBeyondMax ? -jumps : 0;
            int dashFloor = allowBeyondMax ? -dashes : 0;
            if (jumps > 0) _airJumpsUsed = Mathf.Max(jumpFloor, Mathf.Min(_airJumpsUsed, maxAirJumps) - jumps);
            if (dashes > 0) _airDashesUsed = Mathf.Max(dashFloor, Mathf.Min(_airDashesUsed, maxAirDashes) - dashes);
            if (resetDashCooldown && _dashTimer <= 0f) _dashCooldownTimer = 0f;
        }

        /// <summary>
        /// Impulso vertical instantáneo (el rebote del parry en el aire). Sin la gravedad extra de
        /// "salto soltado pronto" (<c>_endedJumpEarly</c>), que lo acortaría a la mitad.
        /// </summary>
        public void Bounce(float verticalSpeed)
        {
            if (verticalSpeed <= 0f) return;
            _currentVerticalSpeed = Mathf.Max(_currentVerticalSpeed, verticalSpeed);
            _endedJumpEarly = false;
            _coyoteUsable = false;
        }

        /// <summary>Corta el empujón y devuelve el control (muerte, respawn, cambio de sección).</summary>
        public void CancelKnockback()
        {
            if (_knockbackTimer <= 0f) return;

            _knockbackTimer = 0f;
            _diving = false;
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
        // ProbeGround ya midió la distancia exacta al suelo (o a la plataforma). Si es negativa (pies
        // hundidos tras aterrizar), se sube.
        private void SnapToGround()
        {
            if (!snapToGround || !_colDown || _currentVerticalSpeed > 0f) return;
            if (_groundDistance >= float.MaxValue) return;

            float drop = _groundDistance - groundSkin;
            if (Mathf.Abs(drop) <= 0.0001f) return;

            // Hundido (el barrido de caída deja los pies unos centímetros dentro): se sube hasta la superficie,
            // salvo que eso lo meta en un techo.
            if (drop < 0f && WouldOverlapGround(transform.position - Vector3.up * drop)) return;

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

        // Cuenta el tiempo en el aire y avisa OnLand al volver al suelo tras un salto o una caída de
        // verdad. Con el juego en pausa Update no llega aquí, así que el reloj se congela.
        private void UpdateLanding()
        {
            if (!_colDown)
            {
                _airTime += Time.deltaTime;
                return;
            }

            if (_airTime >= landMinAirTime) SoundTriggered?.Invoke(SoundTrigger.OnLand);
            _airTime = 0f;
        }

        // ================================================================ mover

        private void MoveCharacter()
        {
            var pos = transform.position;
            RawMovement = new Vector3(_currentHorizontalSpeed, _currentVerticalSpeed);

            var move = RawMovement * Time.deltaTime;

            // Aterrizar sobre las plataformas, que el barrido de colisión no ve.
            move = LandOnPlatforms(move);

            // La caja del barrido va encogida: apoyado en el suelo la caja a tamaño real entra
            // dentro del margen de contacto de Physics2D y daría un choque falso cada frame.
            var solverSize = (Vector2)Box.size - Vector2.one * solverSkin;
            var solverOffset = Box.center;

            // Barrido REAL del trayecto contra la capa Ground, antes de aplicar nada. El resolvedor
            // de abajo sólo mira el punto de destino, así que por sí solo deja pasar cualquier paso
            // grande (dash, retroceso, caída rápida, un frame largo) por encima de geometría fina.
            move = SweepAgainstGround(pos + solverOffset, move, solverSize);

            var furthestPoint = pos + move + solverOffset;
            pos += solverOffset;

            _lastWrite = MoveSource.Solver;

            // Con el suelo sólido activo, el barrido de arriba es la ÚNICA autoridad sobre Ground. Lo que sigue
            // (OverlapBox del destino + muestreo) es el resolvedor original, sólo con 'groundIsImpassable' apagado.
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
        /// venga el movimiento de donde venga: andar, dash, retroceso o cualquier sistema
        /// futuro, porque todos terminan aquí. Paredes, techos y suelos recortan el avance hasta el
        /// punto de contacto. La capa de plataformas no se consulta: se atraviesan igual que antes.
        /// </summary>
        private Vector3 SweepAgainstGround(Vector3 origin, Vector3 move, Vector2 solverSize)
        {
            if (!groundIsImpassable || _groundLayer.value == 0) return move;
            if (solverSize.x <= 0f || solverSize.y <= 0f) return move;

            // --- horizontal: paredes (un escalón mínimo se sube en vez de frenar).
            float stepLift = 0f;
            if (Mathf.Abs(move.x) > 0.0001f)
            {
                float dirX = Mathf.Sign(move.x);
                float wanted = Mathf.Abs(move.x);
                float allowed = SweepAxisAgainstGround(origin, solverSize, new Vector2(dirX, 0f), wanted);
                if (allowed < wanted && !TryStepUp(origin, solverSize, dirX, wanted, out stepLift))
                {
                    move.x = allowed * dirX;
                    if (Mathf.Abs(_currentHorizontalSpeed) > 0f &&
                        Mathf.Sign(_currentHorizontalSpeed) == dirX) _currentHorizontalSpeed = 0f;
                }
            }

            // --- vertical: techos y el aterrizaje (impide atravesar un suelo fino en una caída rápida).
            if (Mathf.Abs(move.y) > 0.0001f)
            {
                float dirY = Mathf.Sign(move.y);
                var from = origin + new Vector3(move.x, 0f);
                float wanted = Mathf.Abs(move.y);
                float allowed = SweepAxisAgainstGround(from, solverSize, new Vector2(0f, dirY), wanted);
                if (allowed < wanted)
                {
                    move.y = allowed * dirY;
                    if (Mathf.Abs(_currentVerticalSpeed) > 0f &&
                        Mathf.Sign(_currentVerticalSpeed) == dirY) _currentVerticalSpeed = 0f;
                }
            }

            if (stepLift > 0f && move.y <= 0f) move.y = stepLift;
            return move;
        }

        /// <summary>
        /// ¿Lo que frena el avance es un escalón de <see cref="maxStepHeight"/> o menos? Sólo andando por el suelo (no
        /// subiendo). Con la caja subida esa altura el avance debe quedar libre y debe haber suelo debajo; devuelve cuánto
        /// hay que subir para quedar apoyado encima.
        /// </summary>
        private bool TryStepUp(Vector3 origin, Vector2 size, float dirX, float wanted, out float lift)
        {
            lift = 0f;
            if (maxStepHeight <= 0f || !_colDown || _currentVerticalSpeed > 0.01f) return false;

            var raised = origin + Vector3.up * maxStepHeight;
            if (SweepAxisAgainstGround(origin, size, Vector2.up, maxStepHeight) < maxStepHeight) return false;   // techo
            if (SweepAxisAgainstGround(raised, size, new Vector2(dirX, 0f), wanted) < wanted) return false;      // muro alto

            var landing = raised + new Vector3(dirX * wanted, 0f);
            int count = Physics2D.BoxCast(landing, size, 0f, Vector2.down, _groundFilter, _groundCasts, maxStepHeight);
            float fall = -1f;
            for (int i = 0; i < count; i++)
            {
                var c = _groundCasts[i];
                if (c.collider == null || c.distance <= 0f) continue;
                if (fall < 0f || c.distance < fall) fall = c.distance;
            }
            if (fall < 0f) return false;

            lift = Mathf.Clamp(maxStepHeight - fall + groundSkin, 0f, maxStepHeight);
            return lift > 0f;
        }

        /// <summary>
        /// Un eje del barrido: cuánto se puede avanzar en <paramref name="dir"/> antes de tocar
        /// Ground. Un impacto a distancia 0 (la caja ya nacía dentro) se ignora aquí — no dice nada
        /// útil sobre el trayecto y, si se tomara, congelaría al personaje; ese caso es justo el que
        /// arregla <see cref="ResolveGroundPenetration"/>.
        /// </summary>
        private float SweepAxisAgainstGround(Vector3 origin, Vector2 size, Vector2 dir, float distance)
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
                best = candidate.distance;
            }

            return best >= distance ? distance : Mathf.Max(0f, best - groundSkin);
        }

        /// <summary>Caja de sondeo del suelo sólido para una posición dada: el cuerpo encogido <c>groundClampInset</c>.</summary>
        private void GroundProbeBox(Vector3 position, out Vector2 center, out Vector2 size)
        {
            size = (Vector2)Box.size - Vector2.one * groundClampInset;
            center = (Vector2)(position + Box.center);
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
            float teleport = Mathf.Max(4f, Box.size.y * 3f);
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
                                     $"dt={Time.deltaTime * 1000f:F1}ms", this);
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
            var a = (Vector2)(from + Box.center);
            var b = (Vector2)(to + Box.center);
            if ((b - a).sqrMagnitude <= 0.000001f) return false;

            if (Physics2D.Linecast(a, b, _groundFilter, _groundCasts) == 0) return false;

            // ¿Sigue habiendo superficie pisable justo debajo? Entonces se ha pasado por encima de
            // ella (cresta, lomo), que es movimiento legítimo.
            float reach = Box.extents.y + _detectionRayLength + groundSkin;
            if (Physics2D.Raycast(b, Vector2.down, _groundFilter, _groundCasts, reach) > 0)
            {
                for (int i = 0; i < _groundCasts.Count; i++)
                {
                    var below = _groundCasts[i];
                    if (below.collider == null || below.distance <= 0f) continue;
                    if (below.normal.y > 0.5f) return false;   // hay suelo debajo
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
        /// <c>Collider2D.Distance</c> porque mide contra el collider REAL del personaje, que apoyado está
        /// dentro del margen de contacto; la caja de sondeo, encogida, no.
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
                                 $"vy={_currentVerticalSpeed:F2} dt={Time.deltaTime * 1000f:F1}ms", this);
            }

            return true;
        }

        /// <summary>
        /// Frena la caída sobre la cara superior de una plataforma (el sensor de suelo, de 0.1, no alcanza a
        /// un frame de caída a 40 u/s; en el suelo sólido eso ya lo hace el barrido). Los rayos nacen en los
        /// pies (+<c>groundSkin</c>) y sólo miran hacia ABAJO: una plataforma más alta que los pies, por
        /// poco que sea, no se sube nunca — se salta.
        /// </summary>
        private Vector3 LandOnPlatforms(Vector3 move)
        {
            if (!PlatformsActive) return move;

            float feetY = _raysDown.Start.y;
            float destFeetY = feetY + move.y;
            float length = Mathf.Max(0f, -move.y) + groundSkin * 2f;
            float bestSurfaceY = float.MinValue;

            foreach (var point in EvaluateRayPositions(_raysDown))
            {
                var hit = Physics2D.Raycast(new Vector2(point.x + move.x, feetY + groundSkin), Vector2.down, length, _platformLayer);
                if (AcceptPlatformHit(hit)) bestSurfaceY = Mathf.Max(bestSurfaceY, hit.point.y);
            }

            if (bestSurfaceY == float.MinValue) return move;
            float targetFeetY = bestSurfaceY + groundSkin;
            if (targetFeetY <= destFeetY) return move;   // el paso no llega a la superficie

            move.y += targetFeetY - destFeetY;
            if (_currentVerticalSpeed < 0f) _currentVerticalSpeed = 0f;
            return move;
        }

        // ================================================================ varios

        private void OnDied()
        {
            _controlEnabled = false;
            _input = default;
            _currentHorizontalSpeed = 0f;
            _currentVerticalSpeed = 0f;
            _dashTimer = 0f;
            _knockbackTimer = 0f;
            _diving = false;
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

        /// <summary>
        /// Sólo con el jugador seleccionado. La caja ya la dibuja Unity (contorno verde del BoxCollider2D); aquí
        /// sólo los sensores: rayos de pies (verdes si pisa suelo, azules si no) y laterales/cabeza (azules).
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) CalculateRayRanged();
            foreach (var range in new[] { _raysUp, _raysRight, _raysDown, _raysLeft })
            {
                bool feet = range.Dir == Vector2.down;
                Gizmos.color = feet && _colDown ? Color.green : Color.blue;
                foreach (var point in EvaluateRayPositions(range))
                    Gizmos.DrawRay(point, range.Dir * _detectionRayLength);
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
        }
    }
}
