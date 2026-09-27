using System;
using System.Collections;
using System.Collections.Generic;
using RedMagic.Abilities;
using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Fx;
using RedMagic.Gameplay;
using RedMagic.Gameplay.Movement;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// El cerebro de un jefe. Sustituye a <see cref="EnemyController"/>: un jefe no patrulla ni
    /// persigue, se planta en el centro de su arena y encadena patrones.
    ///
    /// Lo único que hace este componente es <b>llevar el combate</b>; qué ataca y con qué números
    /// vive todo en el <see cref="BossDefinition"/>, así que otro jefe es otro asset y otro prefab,
    /// sin tocar este archivo.
    ///
    /// El bucle es el clásico de jefe de acción 2D:
    /// <list type="number">
    ///   <item>espera a que el jugador entre en la arena;</item>
    ///   <item>presentación: invulnerable, rugido, barra de vida y música;</item>
    ///   <item>por ataque — <b>aviso</b> (nada hace daño) → <b>ataque</b> → <b>recuperación</b>
    ///         (ventana de daño del jugador) → pausa;</item>
    ///   <item>al cruzar el umbral de vida de la siguiente fase, corta lo que esté haciendo, se
    ///         vuelve invulnerable un momento y cambia de baraja;</item>
    ///   <item>al morir, se lleva por delante a los esbirros que hubiera invocado.</item>
    /// </list>
    ///
    /// El jefe no lleva <see cref="EnemyController"/> ni barra de vida flotante: su barra es la de
    /// pantalla (<see cref="BossHealthBar"/>), que se pide sola al empezar el combate.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class BossController : MonoBehaviour, ISoundEventSource
    {
        /// <summary>OnActivate al empezar la presentación (rugido), OnVulnerable al abrir una ventana de castigo. Ver <see cref="SoundEmitter"/>.</summary>
        public event Action<SoundTrigger> SoundTriggered;

        public void DeclareSoundTriggers(List<SoundTrigger> into)
        {
            into.Add(SoundTrigger.OnActivate);
            if (HasPunishWindows()) into.Add(SoundTrigger.OnVulnerable);
        }

        // Algún ataque de la baraja (o de apertura) deja al jefe expuesto al acabar.
        private bool HasPunishWindows()
        {
            if (definition == null) return false;
            foreach (var phase in definition.Phases)
            {
                if (phase == null) continue;
                if (phase.openingAttack != null && phase.openingAttack.VulnerableSeconds > 0f) return true;
                foreach (var attack in phase.attacks)
                    if (attack != null && attack.VulnerableSeconds > 0f) return true;
            }
            return false;
        }

        [Header("Definición")]
        [Tooltip("Asset con el nombre, las fases y las barajas de ataques de este jefe.")]
        [SerializeField] private BossDefinition definition;

        [Header("Arena")]
        [Tooltip("Media anchura de la arena respecto al jefe. Marca hasta dónde barren las ondas y " +
                 "en qué franja cae la lluvia de proyectiles.")]
        [Min(1f)]
        [SerializeField] private float arenaHalfWidth = 16f;

        [Tooltip("Altura útil sobre el suelo. De aquí caen los proyectiles de lluvia.")]
        [Min(1f)]
        [SerializeField] private float arenaHeight = 12f;

        [Tooltip("Capas contra las que impactan los ataques del jefe. El filtrado real lo hace la " +
                 "etiqueta amiga, así que ~0 (todo) es lo normal.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Tooltip("Capas que cuentan como suelo al medir la altura de la arena.")]
        [SerializeField] private LayerMask groundLayers = 1 << 6;

        [Tooltip("Planta al jefe sobre el suelo medido al empezar. Su origen es la base del sprite, " +
                 "así que basta con dejarlo caído a ojo en la escena y él se coloca solo — igual " +
                 "que hace GroundSnap con la tienda y la recompensa.")]
        [SerializeField] private bool snapToGround = true;

        [Header("Objetivo")]
        [Tooltip("Etiqueta del objetivo. También es a quién daña al tocarlo.")]
        [SerializeField] private string targetTag = "Player";

        [Tooltip("Distancia a la que despierta y empieza el combate.")]
        [Min(1f)]
        [SerializeField] private float activationRadius = 20f;

        [Tooltip("Gira el sprite hacia el jugador.")]
        [SerializeField] private bool faceTarget = true;

        [Header("Presentación")]
        [Min(0f)]
        [SerializeField] private float introSeconds = 2.2f;

        [Tooltip("Música del combate, arranca con la presentación. Vacío = no cambia la música " +
                 "(la de la fase, BossBattle{n} en Resources/Music, sigue sonando). El rugido de " +
                 "entrada es el momento OnActivate del SoundEmitter.")]
        [SerializeField] private AudioClip music;

        [Min(0f)]
        [SerializeField] private float introShake = 0.6f;

        [Header("Daño por contacto")]
        [Tooltip("Daño al tocar al jugador. 0 = el cuerpo del jefe no hace daño.")]
        [Min(0f)]
        [SerializeField] private float contactDamage = 12f;

        [Min(0.05f)]
        [SerializeField] private float contactDamageCooldown = 0.8f;

        [Min(0f)]
        [SerializeField] private float contactKnockbackMultiplier = 1.4f;

        [Header("Efectos")]
        [Tooltip("Sprite base de los efectos del jefe (avisos, ondas, proyectiles). Vacío = el " +
                 "cuadrado generado por AbilityFx.")]
        [SerializeField] private Sprite fxSprite;

        [Header("Placeholders de FX (prefab → arte real)")]
        [Tooltip("Prefab del aviso/telegrafiado. Vacío = el cuadrado de AbilityFx.Flash. Compartido " +
                 "por todos los avisos del jefe; para arte de verdad, edita el prefab.")]
        [SerializeField] private GameObject warnPrefab;

        [Tooltip("Prefab del aviso de ÁREA en el suelo (círculo/elipse). Lo usan los ataques que " +
                 "marcan dónde va a caer algo (la roca lanzada). Vacío = franja plana de color.")]
        [SerializeField] private GameObject warnCirclePrefab;

        [Tooltip("Prefab del aviso de TRAYECTORIA (flecha, arte mirando +X). Lo usan las " +
                 "embestidas. Vacío = barra girada de color.")]
        [SerializeField] private GameObject warnArrowPrefab;

        [Tooltip("Prefab de la onda de choque (ShockwaveAttack). Vacío = construida en código.")]
        [SerializeField] private GameObject shockwavePrefab;

        [Tooltip("Prefab del filo giratorio (SweepBeamAttack). Vacío = construido en código.")]
        [SerializeField] private GameObject sweepBeamPrefab;

        [Tooltip("Prefab del parche de suelo peligroso (HazardFieldAttack, rubble, marea). Vacío = código.")]
        [SerializeField] private GameObject hazardPrefab;

        [Tooltip("Prefab de la plataforma temporal (PlatformFloodAttack). Debe llevar un " +
                 "BoxCollider2D sólido en la capa Ground. Vacío = construida en código.")]
        [SerializeField] private GameObject platformPrefab;

        [Tooltip("Prefab del ancla del ritual (AnchorRitualAttack). Debe llevar BoxCollider2D " +
                 "trigger + Health + HitFlash. Vacío = construida en código.")]
        [SerializeField] private GameObject anchorPrefab;

        public GameObject WarnPrefab => warnPrefab;
        public GameObject WarnCirclePrefab => warnCirclePrefab;
        public GameObject WarnArrowPrefab => warnArrowPrefab;
        public GameObject ShockwavePrefab => shockwavePrefab;
        public GameObject SweepBeamPrefab => sweepBeamPrefab;
        public GameObject HazardPrefab => hazardPrefab;
        public GameObject PlatformPrefab => platformPrefab;
        public GameObject AnchorPrefab => anchorPrefab;

        [Tooltip("Silueta del jefe que se enciende detrás de él durante el aviso de cada ataque. Se " +
                 "construye en código; hace legible que 'algo viene' sin pelearse con el HitFlash " +
                 "del cuerpo, que también escribe el color del sprite.")]
        [SerializeField] private bool showTelegraphAura = true;

        [Tooltip("Cuánto se agranda la silueta del aura respecto al cuerpo.")]
        [Min(1f)]
        [SerializeField] private float auraScale = 1.06f;

        [Tooltip("Color del aura mientras el jefe está expuesto. A propósito distinto del de " +
                 "cualquier fase: mientras brilla así, pegarle renta.")]
        [SerializeField] private Color vulnerableAura = new Color(1f, 0.86f, 0.35f, 1f);

        [Tooltip("Color del aura mientras el jefe se cubre. Tiene que ser lo más distinto posible " +
                 "del de exposición: significan cosas opuestas.")]
        [SerializeField] private Color guardAura = new Color(0.45f, 0.72f, 1f, 1f);

        [Tooltip("Balanceo lento del cuerpo mientras espera, en grados. 0 = quieto.")]
        [Min(0f)]
        [SerializeField] private float swayDegrees = 1.8f;

        [Min(0.01f)]
        [SerializeField] private float swaySpeed = 1.1f;

        [Header("Depuración")]
        [Tooltip("Dibuja en Play (vista Scene, y en Game con Gizmos activados) las cajas y círculos " +
                 "de daño que usan los ataques en el momento en que golpean.")]
        [SerializeField] private bool debugHitboxes;

        [Header("Movimiento (opcional: componente Mover)")]
        [Tooltip("Con un Mover en el jefe, se mueve SOLO en la pausa entre ataques (nunca durante el " +
                 "aviso, el ataque o la recuperación: la recuperación es la ventana del jugador). " +
                 "Distancia a los bordes de la arena que no cruza al moverse.")]
        [Min(0f)]
        [SerializeField] private float moveArenaMargin = 2f;

        // ------------------------------------------------------------------ estado

        private Health _health;
        private Knockback _knockback;
        private SpriteRenderer _body;
        private Transform _visual;
        private SpriteRenderer _aura;
        private Collider2D[] _colliders;
        private Rigidbody2D _rigidbody;
        private BossAnimator _animator;
        private Mover _mover;
        private float _arenaCenterX;
        private bool _arenaAnchored;

        private Transform _target;
        private float _retargetTimer;
        private float _contactTimer;
        private float _groundY;
        private Quaternion _visualBaseRotation;

        private int _phaseIndex;
        private bool _fighting;
        private bool _dead;
        private bool _attacksSuspended;
        private Coroutine _fightRoutine;
        private Coroutine _attackRoutine;
        private Coroutine _vulnerableRoutine;
        private Coroutine _guardRoutine;
        private float _reflectDamage;
        private float _reflectKnockback;
        private float _auraTarget;

        /// <summary>Esbirros invocados por los ataques. Se limpian al morir el jefe.</summary>
        private readonly List<GameObject> _adds = new List<GameObject>();

        /// <summary>Cuántos ataques han pasado desde que se usó cada patrón (para no repetir).</summary>
        private readonly Dictionary<BossAttack, int> _lastUsedAt = new Dictionary<BossAttack, int>();

        /// <summary>Time.time del último lanzamiento de cada patrón (para su cooldownSeconds).</summary>
        private readonly Dictionary<BossAttack, float> _lastUsedTime = new Dictionary<BossAttack, float>();

        private int _attacksLaunched;

        private const float RetargetInterval = 0.4f;

        // ------------------------------------------------------------------ API pública

        public BossDefinition Definition => definition;
        public Health BossHealth => _health;
        public int PhaseIndex => _phaseIndex;
        public BossPhase CurrentPhase => definition != null ? definition.GetPhase(_phaseIndex) : null;
        public bool IsFighting => _fighting && !_dead;
        public float GroundY => _groundY;
        public float ArenaHalfWidth => arenaHalfWidth;
        public float ArenaHeight => arenaHeight;

        /// <summary>Capa de animación del cuerpo, si la tiene (para ataques con más de un gesto).</summary>
        public BossAnimator BodyAnimator => _animator;

        /// <summary>
        /// Deja de girarse hacia el jugador mientras está a true: una embestida que le pasa por
        /// encima no debe darse la vuelta a mitad del recorrido.
        /// </summary>
        public bool FacingLocked { get; set; }

        /// <summary>El movimiento del jefe, si lleva un <see cref="Mover"/>.</summary>
        public Mover Movement => _mover;

        /// <summary>
        /// Desplaza el cuerpo del jefe (una embestida). Por el Rigidbody cinemático si lo tiene, para
        /// que la física lo vea moverse y el contacto siga funcionando.
        /// </summary>
        public void MoveBody(Vector2 position)
        {
            // Un jefe que se ha movido deja de llevarse la arena consigo: desde aquí se mide desde
            // donde empezó. Los jefes que nunca llaman a esto siguen exactamente como antes.
            _arenaAnchored = true;
            if (_rigidbody != null && _rigidbody.simulated) _rigidbody.MovePosition(position);
            else transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        /// <summary>
        /// Corta la baraja: <see cref="FightLoop"/> deja de sortear ataques y el que estuviera en
        /// curso se detiene. Lo usa un jefe con locomoción propia (la Reina Escarabajo mientras
        /// excava) para no atacar desde donde el jugador no puede verla. Idempotente.
        /// </summary>
        public void SuspendAttacks()
        {
            _attacksSuspended = true;
            FacingLocked = false;
            if (_animator != null) _animator.ClearHold();
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }
        }

        /// <summary>Reanuda la baraja tras un <see cref="SuspendAttacks"/>.</summary>
        public void ResumeAttacks() => _attacksSuspended = false;

        /// <summary>
        /// Enseña u oculta el cuerpo del jefe y su aura sin desactivar el GameObject (los
        /// colliders y las corrutinas siguen vivos). Para un jefe que desaparece bajo tierra.
        /// </summary>
        public void SetBodyVisible(bool visible)
        {
            if (_body != null) _body.enabled = visible;
            if (_aura != null) _aura.enabled = visible;
        }

        /// <summary>
        /// Lanza un ataque puntual fuera de la baraja — aviso y ejecución, con el contexto y el
        /// ritmo de la fase actual. Para picos que dispara otro componente: el temblor que la
        /// Reina Escarabajo suelta mientras túnela, invisible, entre una superficie y la otra.
        /// </summary>
        public void RunScriptedAttack(BossAttack attack)
        {
            if (attack == null || _dead) return;
            StartCoroutine(ScriptedAttack(attack));
        }

        private IEnumerator ScriptedAttack(BossAttack attack)
        {
            var phase = CurrentPhase;
            float pace = PaceOf(phase);

            yield return Telegraph(attack, BuildContext(phase), pace);
            if (_dead) yield break;

            yield return attack.Run(BuildContext(phase));
        }

        /// <summary>
        /// El aviso de un ataque. Sin gesto, un tiempo fijo. Con gesto (<see cref="BossAttack.Gesture"/>
        /// y un <see cref="BossAnimator"/> en el jefe) el aviso lo hace el cuerpo: el clip se ajusta
        /// para soltar al acabar el telegrafiado y el ataque arranca en el frame de suelta, no
        /// cuando diga un reloj — el orbe nace en el dibujo en el que el jefe abre los brazos.
        /// </summary>
        private IEnumerator Telegraph(BossAttack attack, BossContext ctx, float pace)
        {
            float seconds = attack.Telegraph / pace;
            attack.OnTelegraph(ctx);

            if (_animator != null && _animator.PlayGesture(attack.Gesture, seconds))
            {
                // Seguro por si el Animator no llega a soltar (clip sin evento, estado cortado):
                // el ataque sale igual, un pelo después de lo previsto.
                float timeout = Mathf.Max(seconds, _animator.SecondsToRelease) + 0.5f;
                while (!_animator.Released && timeout > 0f && !_dead)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }

                yield break;
            }

            yield return new WaitForSeconds(seconds);
        }

        /// <summary>Se dispara al empezar el combate (tras la presentación).</summary>
        public event Action FightStarted;

        /// <summary>Nuevo índice de fase. Lo escucha la barra de vida para marcar el corte.</summary>
        public event Action<int> PhaseChanged;

        /// <summary>Se dispara al morir el jefe.</summary>
        public event Action Defeated;

        /// <summary>True mientras dura una ventana de castigo (el jefe recibe daño multiplicado).</summary>
        public bool IsVulnerable { get; private set; }

        /// <summary>Abre y cierra la ventana de castigo. Lo escucha la barra de vida.</summary>
        public event Action<bool> VulnerabilityChanged;

        /// <summary>True mientras el jefe está cubierto: apenas recibe daño y lo devuelve.</summary>
        public bool IsGuarding { get; private set; }

        /// <summary>Abre y cierra la guardia. Lo escucha la barra de vida.</summary>
        public event Action<bool> GuardChanged;

        /// <summary>
        /// Apunta un esbirro invocado para que muera con el jefe. Sin esto, matar al jefe dejaría
        /// bichos sueltos dando vueltas mientras el jugador recoge la recompensa.
        /// </summary>
        public void RegisterAdd(GameObject add)
        {
            if (add == null) return;
            _adds.Add(add);
        }

        /// <summary>Esbirros vivos ahora mismo (los ataques de invocación lo usan como tope).</summary>
        public int LiveAdds
        {
            get
            {
                int alive = 0;
                for (int i = _adds.Count - 1; i >= 0; i--)
                {
                    var add = _adds[i];
                    var health = add != null ? add.GetComponent<Health>() : null;

                    if (add == null || (health != null && health.IsDead)) _adds.RemoveAt(i);
                    else alive++;
                }
                return alive;
            }
        }

        // ------------------------------------------------------------------ ciclo de vida

        private void Awake()
        {
            _health = GetComponent<Health>();
            _knockback = GetComponent<Knockback>();
            _body = GetComponentInChildren<SpriteRenderer>();
            _colliders = GetComponentsInChildren<Collider2D>();
            _rigidbody = GetComponent<Rigidbody2D>();
            _animator = GetComponent<BossAnimator>();
            _mover = GetComponent<Mover>();

            _visual = _body != null ? _body.transform : transform;
            _visualBaseRotation = _visual.localRotation;

            // Un jefe es inamovible: ni el retroceso de los golpes ni la física lo desplazan de su
            // arena, que es lo que hacen legibles sus patrones.
            if (_knockback != null) _knockback.Immune = true;
            if (_rigidbody != null)
            {
                _rigidbody.bodyType = RigidbodyType2D.Kinematic;
                _rigidbody.useFullKinematicContacts = true;
                _rigidbody.freezeRotation = true;
            }

            if (showTelegraphAura) BuildAura();
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
            _health.HealthChanged += OnHealthChanged;
            if (_mover != null) _mover.MovingChanged += OnMovingChanged;
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
            _health.HealthChanged -= OnHealthChanged;
            if (_mover != null) _mover.MovingChanged -= OnMovingChanged;
        }

        private void Start()
        {
            _groundY = MeasureGroundY();

            // El origen del prefab es la base del jefe, así que plantarlo es igualar la Y.
            if (snapToGround)
                transform.position = new Vector3(transform.position.x, _groundY, transform.position.z);

            // La arena se mide desde donde empieza el jefe. Con Mover se queda ahí aunque él se
            // mueva; sin Mover sigue siendo su posición (el jefe no se mueve), como siempre.
            _arenaCenterX = transform.position.x;
            if (_mover != null)
            {
                _arenaAnchored = true;
                _mover.Paused = true;
                float half = Mathf.Max(0.5f, arenaHalfWidth - moveArenaMargin);
                _mover.SetBounds(_arenaCenterX - half, _arenaCenterX + half);
            }

            if (definition == null || definition.PhaseCount == 0)
            {
                Debug.LogWarning($"[Boss] '{name}' no tiene BossDefinition (o no tiene fases): se queda quieto.", this);
                return;
            }

            ApplyPhaseVisuals(definition.GetPhase(0));
            if (_animator != null) _animator.SetPace(PaceOf(definition.GetPhase(0)));
        }

        private void Update()
        {
            if (_contactTimer > 0f) _contactTimer -= Time.deltaTime;

            UpdateAura();

            if (_dead) return;

            UpdateFacing();
            UpdateSway();

            if (!_fighting && ShouldWakeUp()) BeginFight();
        }

        // ------------------------------------------------------------------ despertar / presentación

        private bool ShouldWakeUp()
        {
            if (definition == null || definition.PhaseCount == 0) return false;
            if (!GameStateManager.CanPlayerAct) return false;

            var target = ResolveTarget();
            if (target == null) return false;

            return Vector2.Distance(target.position, transform.position) <= activationRadius;
        }

        private void BeginFight()
        {
            _fighting = true;
            _fightRoutine = StartCoroutine(FightLoop(playIntro: true));
        }

        /// <summary>
        /// Bucle del combate. Se relanza tal cual tras cada transición de fase, por eso la
        /// presentación es un parámetro: sólo la primera vuelta la reproduce.
        /// </summary>
        private IEnumerator FightLoop(bool playIntro, BossAttack opening = null)
        {
            if (playIntro) yield return Intro();

            // Ataque de entrada de la fase (BossPhase.openingAttack): una sola vez, antes de la
            // baraja, con su ciclo completo de aviso → ataque → recuperación → pausa.
            if (opening != null && !_dead)
            {
                var phase = CurrentPhase;
                yield return RunAttack(opening, phase);
                if (_dead) yield break;

                if (phase != null)
                {
                    float pause = UnityEngine.Random.Range(phase.pauseBetweenAttacks.x, phase.pauseBetweenAttacks.y);
                    yield return PauseBetweenAttacks(pause / PaceOf(phase));
                }
            }

            while (!_dead)
            {
                var phase = CurrentPhase;
                if (phase == null || phase.attacks == null || phase.attacks.Length == 0)
                {
                    yield return null;
                    continue;
                }

                // Un jefe con locomoción propia puede pedir que se pare la baraja mientras está
                // fuera de la arena (la Reina Escarabajo, bajo tierra).
                if (_attacksSuspended)
                {
                    yield return null;
                    continue;
                }

                var attack = PickAttack(phase);
                if (attack == null)
                {
                    yield return null;
                    continue;
                }

                yield return RunAttack(attack, phase);

                if (_dead) yield break;

                float pause = UnityEngine.Random.Range(phase.pauseBetweenAttacks.x, phase.pauseBetweenAttacks.y);
                yield return PauseBetweenAttacks(pause / PaceOf(phase));
            }
        }

        /// <summary>
        /// La pausa entre ataques. Es el único momento en que un jefe con <see cref="Mover"/> se
        /// mueve: se recoloca y vuelve a plantarse antes del siguiente aviso.
        /// </summary>
        private IEnumerator PauseBetweenAttacks(float seconds)
        {
            if (_mover == null)
            {
                yield return new WaitForSeconds(seconds);
                yield break;
            }

            _mover.Target = ResolveTarget();
            _mover.Paused = false;
            yield return new WaitForSeconds(seconds);
            _mover.Paused = true;
        }

        private void OnMovingChanged(bool moving)
        {
            if (_animator != null) _animator.SetMoving(moving && !_dead);
        }

        private IEnumerator Intro()
        {
            // Invulnerable durante la presentación: nadie debería poder abrir el combate
            // descargándole el arma entera antes de que el jefe pueda reaccionar.
            _health.Invulnerable = true;

            BossHealthBar.Show(this);

            if (music != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayMusic(music);

            SoundTriggered?.Invoke(SoundTrigger.OnActivate);

            if (introShake > 0f) CameraFollow.ShakeAll(introShake, Mathf.Max(0.3f, introSeconds * 0.5f));

            _auraTarget = 0.7f;
            yield return new WaitForSeconds(introSeconds);
            _auraTarget = 0f;

            _health.Invulnerable = false;
            FightStarted?.Invoke();
        }

        // ------------------------------------------------------------------ ataques

        /// <summary>
        /// Sorteo por peso dentro de la baraja de la fase, descartando los que todavía están en
        /// enfriamiento (<see cref="BossAttack.CooldownInAttacks"/>). Si el enfriamiento dejara la
        /// baraja vacía se ignora, para que el jefe nunca se quede parado por una mala
        /// configuración del asset. Lo que NO se ignora nunca es <see cref="BossAttack.MinPhase"/>:
        /// un ataque de fase 2 no sale en la fase 1 ni como último recurso.
        /// </summary>
        private BossAttack PickAttack(BossPhase phase)
        {
            BossAttack fallback = null;
            float total = 0f;

            for (int i = 0; i < phase.attacks.Length; i++)
            {
                var candidate = phase.attacks[i];
                if (candidate == null || !candidate.AvailableInPhase(_phaseIndex)) continue;

                fallback ??= candidate;
                if (!IsReady(candidate)) continue;

                total += Mathf.Max(0.01f, candidate.Weight);
            }

            if (total <= 0f) return fallback;

            float roll = UnityEngine.Random.value * total;

            for (int i = 0; i < phase.attacks.Length; i++)
            {
                var candidate = phase.attacks[i];
                if (candidate == null || !IsReady(candidate)) continue;

                roll -= Mathf.Max(0.01f, candidate.Weight);
                if (roll <= 0f) return candidate;
            }

            return fallback;
        }

        private bool IsReady(BossAttack attack) =>
            attack.AvailableInPhase(_phaseIndex) &&
            (!_lastUsedAt.TryGetValue(attack, out int last) || _attacksLaunched - last > attack.CooldownInAttacks) &&
            (attack.CooldownSeconds <= 0f || !_lastUsedTime.TryGetValue(attack, out float at) ||
             Time.time - at >= attack.CooldownSeconds);

        /// <summary>Aviso → ataque → recuperación. Es el ciclo que hace legible a un jefe.</summary>
        private IEnumerator RunAttack(BossAttack attack, BossPhase phase)
        {
            // Nunca se mueve mientras ataca: el aviso, el golpe y la recuperación son en el sitio.
            if (_mover != null) _mover.Paused = true;

            // Lo que un ataque anterior cortado a medias pudiera dejar puesto.
            FacingLocked = false;
            if (_animator != null) _animator.ClearHold();

            _lastUsedAt[attack] = _attacksLaunched;
            _lastUsedTime[attack] = Time.time;
            _attacksLaunched++;

            var ctx = BuildContext(phase);
            float pace = PaceOf(phase);

            // El ritmo cambia también dentro de una fase (frenesí), así que se reenvía en cada ataque.
            if (_animator != null) _animator.SetPace(pace);

            // --- aviso: el aura se enciende, el jefe hace su gesto (si lo tiene) y el ataque pinta
            // sus propias marcas. Nada daña aún.
            _auraTarget = 0.6f;
            yield return Telegraph(attack, ctx, pace);
            _auraTarget = 0f;

            if (_dead) yield break;

            // --- ataque: se guarda la corrutina para poder cortarla en seco al cambiar de fase.
            _attackRoutine = StartCoroutine(attack.Run(BuildContext(phase)));
            yield return _attackRoutine;
            _attackRoutine = null;

            if (_dead) yield break;

            // --- castigo: si el ataque lo declara, el jefe queda expuesto durante su recuperación.
            // La ventana empieza aquí y no antes a propósito: mientras el ataque está haciendo daño
            // no puede ser también la oportunidad de pegarle.
            if (attack.VulnerableSeconds > 0f)
                EnterVulnerable(attack.VulnerableSeconds / pace, attack.VulnerableMultiplier);

            // --- recuperación: el jefe se queda quieto. Ésta es la ventana de daño del jugador.
            yield return new WaitForSeconds(attack.Recovery / pace);
        }

        // ------------------------------------------------------------------ ventana de castigo

        /// <summary>
        /// Deja al jefe expuesto <paramref name="seconds"/> segundos: recibe el daño multiplicado
        /// y su aura cambia a <see cref="vulnerableAura"/> para que se vea desde la otra punta de
        /// la arena. Es público para que también pueda abrirla un ataque a mitad de su ejecución
        /// (por ejemplo, uno que se quede con el puño clavado en el suelo).
        /// </summary>
        public void EnterVulnerable(float seconds, float multiplier)
        {
            if (_dead || seconds <= 0f) return;

            if (_vulnerableRoutine != null) StopCoroutine(_vulnerableRoutine);
            _vulnerableRoutine = StartCoroutine(VulnerableWindow(seconds, Mathf.Max(1f, multiplier)));
            SoundTriggered?.Invoke(SoundTrigger.OnVulnerable);
        }

        private IEnumerator VulnerableWindow(float seconds, float multiplier)
        {
            // Simétrico a la guardia: los dos estados escriben el mismo multiplicador, así que el
            // que entra tiene que cerrar al otro o el que salga después restauraría la armadura
            // por debajo y se comería la ventana.
            EndGuard();

            IsVulnerable = true;
            VulnerabilityChanged?.Invoke(true);

            _health.DamageMultiplier = PhaseArmor() * multiplier;

            SetAuraColor(vulnerableAura);
            _auraTarget = 0.8f;

            yield return new WaitForSeconds(seconds);

            _vulnerableRoutine = null;
            EndVulnerable();
        }

        /// <summary>Cierra la ventana y devuelve la armadura de la fase. Es idempotente.</summary>
        private void EndVulnerable()
        {
            if (_vulnerableRoutine != null)
            {
                StopCoroutine(_vulnerableRoutine);
                _vulnerableRoutine = null;
            }

            _health.DamageMultiplier = PhaseArmor();

            if (!IsVulnerable) return;

            IsVulnerable = false;
            _auraTarget = 0f;
            ApplyPhaseVisuals(CurrentPhase);
            VulnerabilityChanged?.Invoke(false);
        }

        /// <summary>Armadura de la fase actual: cuánto daño recibe el jefe fuera de las ventanas.</summary>
        private float PhaseArmor()
        {
            var phase = CurrentPhase;
            return phase != null ? Mathf.Max(0f, phase.damageTakenMultiplier) : 1f;
        }

        // ------------------------------------------------------------------ guardia

        /// <summary>
        /// El jefe se cubre: durante <paramref name="seconds"/> apenas recibe daño y <b>devuelve</b>
        /// una fracción de lo que le entra a quien se lo hizo.
        ///
        /// Es la ventana de castigo del revés, y por eso existe: la de castigo enseña "pégale
        /// ahora", ésta enseña "ahora no". Un jefe que sólo tenga la primera se juega pulsando el
        /// botón sin mirar; teniendo las dos, hay que leerlo.
        ///
        /// La armadura de la guardia no se pone a 0 a propósito: el golpe tiene que <i>entrar</i>
        /// para poder devolverse, y ver un número ridículo salir del jefe es justo la señal de que
        /// estás perdiendo el tiempo.
        /// </summary>
        public void EnterGuard(float seconds, float damageTaken, float reflectDamage, float reflectKnockback)
        {
            if (_dead || seconds <= 0f) return;

            if (_guardRoutine != null) StopCoroutine(_guardRoutine);
            _guardRoutine = StartCoroutine(GuardWindow(seconds, Mathf.Max(0.01f, damageTaken),
                                                       Mathf.Max(0f, reflectDamage),
                                                       Mathf.Max(0f, reflectKnockback)));
        }

        private IEnumerator GuardWindow(float seconds, float damageTaken, float reflectDamage,
                                        float reflectKnockback)
        {
            // Una guardia cancela cualquier ventana de castigo abierta: no puede estar expuesto y
            // cubierto a la vez.
            EndVulnerable();

            IsGuarding = true;
            _reflectDamage = reflectDamage;
            _reflectKnockback = reflectKnockback;
            GuardChanged?.Invoke(true);

            _health.DamageMultiplier = damageTaken;
            _health.Damaged += OnGuardedDamage;

            SetAuraColor(guardAura);
            _auraTarget = 0.7f;

            yield return new WaitForSeconds(seconds);

            _guardRoutine = null;
            EndGuard();
        }

        private void EndGuard()
        {
            if (_guardRoutine != null)
            {
                StopCoroutine(_guardRoutine);
                _guardRoutine = null;
            }

            if (!IsGuarding) return;

            _health.Damaged -= OnGuardedDamage;
            _health.DamageMultiplier = PhaseArmor();

            IsGuarding = false;
            _auraTarget = 0f;
            ApplyPhaseVisuals(CurrentPhase);
            GuardChanged?.Invoke(false);
        }

        /// <summary>
        /// Devuelve parte del golpe a quien esté pegando. Se busca al objetivo en vez de guardar
        /// quién golpeó porque <c>Health</c> no dice de dónde vino el daño — y para lo que hace
        /// falta aquí (castigar a quien está encima del jefe) el objetivo es exactamente ése.
        /// </summary>
        private void OnGuardedDamage(float amount)
        {
            if (!IsGuarding || _reflectDamage <= 0f) return;

            var target = ResolveTarget();
            if (target == null) return;

            var health = target.GetComponentInParent<Health>();
            if (health == null || health.IsDead) return;

            health.TakeDamage(_reflectDamage, transform.position, _reflectKnockback);
        }

        private BossContext BuildContext(BossPhase phase)
        {
            var ability = new AbilityContext(
                caster: gameObject,
                runner: this,
                casterHealth: _health,
                hitLayers: hitLayers,
                facing: FacingSign(),
                aim: AimAtTarget(),
                friendlyTag: gameObject.CompareTag("Untagged") ? null : tag,
                damageScale: phase != null ? phase.damageScale : 1f);

            return new BossContext(this, ResolveTarget(), ability, _groundY, arenaHalfWidth,
                                   arenaHeight, PaceOf(phase), fxSprite,
                                   phase != null ? phase.accent : Color.white,
                                   _arenaAnchored ? _arenaCenterX : float.NaN);
        }

        /// <summary>Ritmo efectivo de la fase, incluido el frenesí de vida baja.</summary>
        private float PaceOf(BossPhase phase)
        {
            if (phase == null) return 1f;

            float pace = phase.speedScale;
            if (phase.frenzyBelowHealth > 0f && _health.Normalized <= phase.frenzyBelowHealth)
                pace *= phase.frenzySpeedScale;

            return Mathf.Max(0.1f, pace);
        }

        // ------------------------------------------------------------------ fases

        private void OnHealthChanged(float current, float max)
        {
            if (_dead || definition == null || definition.PhaseCount == 0) return;

            int next = definition.PhaseIndexFor(max <= 0f ? 0f : current / max);

            // Las fases nunca van hacia atrás: curar al jefe (o el robo de vida de un arma) no
            // debería devolverlo a una fase anterior a mitad de combate.
            if (next <= _phaseIndex) return;

            _phaseIndex = next;
            StartCoroutine(EnterPhase(definition.GetPhase(next)));
        }

        /// <summary>
        /// Transición de fase: corta el ataque en curso, se vuelve invulnerable un momento y
        /// cambia de baraja. La invulnerabilidad evita el caso feo de saltarse la fase 2 entera
        /// con una ráfaga bien puesta justo en el umbral.
        /// </summary>
        private IEnumerator EnterPhase(BossPhase phase)
        {
            if (phase == null) yield break;

            // La pausa entre ataques puede quedar cortada a medias: el movimiento se para aquí.
            if (_mover != null) _mover.Paused = true;
            FacingLocked = false;

            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            if (_fightRoutine != null)
            {
                StopCoroutine(_fightRoutine);
                _fightRoutine = null;
            }

            // Ni una ventana de castigo ni una guardia deben sobrevivir al cambio de fase: la
            // armadura que les tocaría restaurar sería la de la fase anterior.
            EndGuard();
            EndVulnerable();

            _health.Invulnerable = true;
            ApplyPhaseVisuals(phase);
            PhaseChanged?.Invoke(_phaseIndex);

            // El golpe que cruza el umbral se lee: el jefe se tambalea (corta el gesto que tuviera
            // a medias) y, si la fase lo trae, algo le estalla a los pies.
            if (_animator != null)
            {
                _animator.PlayStagger();
                _animator.SetPace(PaceOf(phase));
            }

            if (phase.transitionFx != null)
                VfxOneShot.Spawn(phase.transitionFx, new Vector3(transform.position.x, _groundY, 0f));

            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(phase.transitionSound, transform.position);

            if (phase.transitionShake > 0f)
                CameraFollow.ShakeAll(phase.transitionShake, Mathf.Max(0.3f, phase.transitionSeconds * 0.6f));

            _auraTarget = 0.85f;
            yield return new WaitForSeconds(phase.transitionSeconds);
            _auraTarget = 0f;

            if (_dead) yield break;

            _health.Invulnerable = false;
            _fightRoutine = StartCoroutine(FightLoop(playIntro: false, opening: phase.openingAttack));
        }

        private void ApplyPhaseVisuals(BossPhase phase)
        {
            if (phase == null) return;

            // La armadura es de la fase, así que se aplica con el resto de su identidad: una fase
            // puede empezar acorazada y la siguiente agrietarse sin tocar nada más.
            if (!IsVulnerable) _health.DamageMultiplier = Mathf.Max(0f, phase.damageTakenMultiplier);

            SetAuraColor(phase.accent);
        }

        private void SetAuraColor(Color color)
        {
            if (_aura == null) return;

            color.a = _aura.color.a;
            _aura.color = color;
        }

        // ------------------------------------------------------------------ muerte

        private void OnDied()
        {
            if (_dead) return;
            _dead = true;
            _fighting = false;

            if (_mover != null) _mover.Paused = true;

            StopAllCoroutines();
            _attackRoutine = null;
            _fightRoutine = null;
            _vulnerableRoutine = null;
            _guardRoutine = null;

            if (IsVulnerable)
            {
                IsVulnerable = false;
                VulnerabilityChanged?.Invoke(false);
            }

            if (IsGuarding)
            {
                // La suscripción al daño tiene que soltarse a mano: StopAllCoroutines corta la
                // corrutina de la guardia, pero no deshace lo que ya había enganchado.
                _health.Damaged -= OnGuardedDamage;
                IsGuarding = false;
                GuardChanged?.Invoke(false);
            }

            // Los esbirros mueren con su invocador: si no, el jugador se queda peleando contra los
            // restos mientras recoge la recompensa del jefe.
            KillAdds();

            // Lo que dejó en el suelo (el fuego verde, el escombro) se va con él: un hazard que siga
            // quemando mientras se recoge la recompensa ya no es parte de ningún combate.
            BossHazard.ClearFrom(this);

            if (_aura != null) _aura.gameObject.SetActive(false);

            CameraFollow.ShakeAll(0.9f, 0.8f);
            AbilityFx.Flash(fxSprite, transform.position + Vector3.up, Vector3.one * 5f,
                            new Color(1f, 0.95f, 0.7f, 0.85f), 0.6f, 0f, 2.4f, gameObject);

            // El aspecto del cadáver y su limpieza son cosa de Corpse; aquí sólo se apaga la
            // física, igual que hace EnemyController.
            foreach (var col in _colliders)
                if (col != null) col.enabled = false;

            if (_rigidbody != null) _rigidbody.simulated = false;

            BossHealthBar.Hide();
            Defeated?.Invoke();
        }

        private void KillAdds()
        {
            for (int i = 0; i < _adds.Count; i++)
            {
                var add = _adds[i];
                if (add == null) continue;

                var health = add.GetComponent<Health>();
                if (health != null && !health.IsDead) health.Die();
                else Destroy(add);
            }

            _adds.Clear();
        }

        // ------------------------------------------------------------------ contacto

        private void OnCollisionStay2D(Collision2D collision) => TryContactDamage(collision.collider);

        private void OnTriggerStay2D(Collider2D other) => TryContactDamage(other);

        private void TryContactDamage(Collider2D other)
        {
            if (_dead || contactDamage <= 0f || _contactTimer > 0f || other == null) return;
            if (!GameStateManager.CanPlayerAct) return;

            var otherHealth = other.GetComponentInParent<Health>();
            if (otherHealth == null || otherHealth == _health) return;
            if (!string.IsNullOrEmpty(targetTag) && !otherHealth.CompareTag(targetTag)) return;

            if (otherHealth.TakeDamage(contactDamage, transform.position, contactKnockbackMultiplier))
                _contactTimer = contactDamageCooldown;
        }

        // ------------------------------------------------------------------ objetivo y presencia

        private Transform ResolveTarget()
        {
            if (_target != null) return _target;

            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer > 0f) return null;

            _retargetTimer = RetargetInterval;

            if (string.IsNullOrEmpty(targetTag)) return null;

            var found = GameObject.FindGameObjectWithTag(targetTag);
            _target = found != null ? found.transform : null;
            return _target;
        }

        private int FacingSign()
        {
            var target = _target;
            if (target == null) return 1;
            return target.position.x < transform.position.x ? -1 : 1;
        }

        private Vector2 AimAtTarget()
        {
            var target = _target;
            if (target == null) return new Vector2(FacingSign(), 0f);

            Vector2 delta = (Vector2)target.position - (Vector2)transform.position;
            return delta.sqrMagnitude < 0.0001f ? new Vector2(FacingSign(), 0f) : delta.normalized;
        }

        private void UpdateFacing()
        {
            if (!faceTarget || _body == null || FacingLocked) return;

            var target = ResolveTarget();
            if (target == null) return;

            _body.flipX = target.position.x < transform.position.x;
        }

        /// <summary>Balanceo lento del cuerpo: un jefe totalmente inmóvil parece un decorado.</summary>
        private void UpdateSway()
        {
            // Sólo si hay un hijo visual: girar la raíz movería también los colliders del jefe.
            if (swayDegrees <= 0f || _visual == null || _visual == transform) return;

            float angle = Mathf.Sin(Time.time * swaySpeed) * swayDegrees;
            _visual.localRotation = _visualBaseRotation * Quaternion.Euler(0f, 0f, angle);
        }

        // ------------------------------------------------------------------ aura de aviso

        /// <summary>
        /// El aura es una <b>copia del propio sprite del jefe</b> un pelín más grande y detrás de
        /// él, no un rectángulo de color: así se lee como un halo que rodea su silueta y no como
        /// una caja translúcida pegada encima. Cuelga del transform del cuerpo, de modo que hereda
        /// gratis su posición, su escala y el balanceo.
        /// </summary>
        private void BuildAura()
        {
            var parent = _body != null ? _body.transform : transform;

            var go = new GameObject("Boss Aura");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * auraScale;

            _aura = go.AddComponent<SpriteRenderer>();
            _aura.color = new Color(1f, 1f, 1f, 0f);

            if (_body != null)
            {
                _aura.sprite = _body.sprite;
                _aura.sortingLayerID = _body.sortingLayerID;
                _aura.sortingOrder = _body.sortingOrder - 1;   // detrás del cuerpo
            }
            else
            {
                // Sin sprite de cuerpo no hay silueta que copiar: se cae al cuadrado genérico.
                _aura.sprite = fxSprite != null ? fxSprite : AbilityFx.DefaultSprite;
                _aura.sortingOrder = 49;
                AbilityFx.Resize(go.transform, _aura, Vector2.one * 4f);
            }
        }

        private void UpdateAura()
        {
            if (_aura == null) return;

            // El halo sigue al cuerpo si éste cambia de fotograma o se voltea.
            if (_body != null)
            {
                if (_aura.sprite != _body.sprite) _aura.sprite = _body.sprite;
                _aura.flipX = _body.flipX;
            }

            var color = _aura.color;
            // Durante el aviso late en vez de quedarse fijo: un aura constante se lee como decorado.
            float pulse = _auraTarget > 0.01f ? 0.75f + 0.25f * Mathf.Sin(Time.time * 14f) : 0f;
            float target = _auraTarget * pulse;

            color.a = Mathf.MoveTowards(color.a, target, Time.deltaTime * 4f);
            _aura.color = color;
        }

        // ------------------------------------------------------------------ arena

        /// <summary>
        /// Mide el suelo bajo el jefe una sola vez, al empezar. Los ataques rasantes y la lluvia
        /// necesitan una altura de referencia, y sacarla de la escena evita tener que ajustarla a
        /// mano en cada arena.
        /// </summary>
        private float MeasureGroundY()
        {
            // El rayo sale muy por encima del jefe a propósito: si sale desde su base y lo han
            // dejado un pelo hundido en el terreno, empezaría dentro del collider y no habría
            // suelo que medir.
            var origin = (Vector2)transform.position + Vector2.up * (arenaHeight * 0.5f);
            var hit = Physics2D.Raycast(origin, Vector2.down, arenaHeight * 0.5f + arenaHeight + 10f, groundLayers);

            if (hit.collider != null) return hit.point.y;

            // Sin suelo debajo (arena flotante o capa mal puesta): la base del sprite es la mejor
            // aproximación disponible, y es mejor que dejar los ataques a la altura del centro.
            return _body != null ? _body.bounds.min.y : transform.position.y;
        }

        private void OnDrawGizmos()
        {
            if (!debugHitboxes || !Application.isPlaying) return;

            BossHitboxDebug.Prune();
            foreach (var e in BossHitboxDebug.Current)
            {
                Gizmos.color = e.Color;
                if (e.Radius > 0f) Gizmos.DrawWireSphere(e.Center, e.Radius);
                else Gizmos.DrawWireCube(e.Center, e.Size);
            }
        }

        private void OnDrawGizmosSelected()
        {
            float groundY = Application.isPlaying ? _groundY : transform.position.y;

            // Arena: hasta dónde barren los ataques y de dónde cae la lluvia.
            Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(transform.position.x, groundY + arenaHeight * 0.5f, 0f),
                                new Vector3(arenaHalfWidth * 2f, arenaHeight, 0f));

            // Línea del suelo medido.
            Gizmos.color = new Color(0.9f, 0.8f, 0.3f, 0.9f);
            Gizmos.DrawLine(new Vector3(transform.position.x - arenaHalfWidth, groundY, 0f),
                            new Vector3(transform.position.x + arenaHalfWidth, groundY, 0f));

            // Radio de despertar.
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);
        }
    }
}
