using RedMagic.Audio;
using RedMagic.Combat;
using RedMagic.Core;
using RedMagic.Gameplay;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// La IA de un enemigo, como una máquina de estados explícita en vez de una maraña de ifs.
    ///
    /// El ciclo es siempre el mismo, y es el que el jugador tiene que poder leer:
    /// <b>reposo → (detecta) → acercarse → (a tiro) → atacar → enfriamiento → reposo</b>.
    /// Lo que cambia entre los cinco arquetipos es sólo cómo se recorre:
    /// <list type="bullet">
    /// <item>Un <see cref="EnemyArchetype.Static"/> no tiene "acercarse": espera en reposo y ataca
    /// en cuanto el objetivo entra en su rango de ataque.</item>
    /// <item>Un volador se acerca por el aire esquivando; uno de suelo camina y no se tira por un
    /// precipicio.</item>
    /// <item>Uno a distancia además <b>retrocede</b> si le invaden el espacio propio, en vez de
    /// quedarse a que le peguen de cerca.</item>
    /// </list>
    ///
    /// <b>No hay números aquí.</b> Todos salen de <see cref="EnemyStats"/>, que es el único
    /// componente que se toca a mano. Y no decide en qué momento del ataque cae el daño: eso lo
    /// avisa la animación (ver <see cref="EnemyAnimation"/>), que es lo que hace que el golpe
    /// coincida con el dibujo.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyStats))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyBrain : MonoBehaviour, ISoundEventSource, IEnemyCooldownSource
    {
        // ------------------------------------------------------------ HUD

        /// <summary>
        /// Peleando: persigue, ataca, se recarga, se retira o esquiva. Lo usan las barras de vida de
        /// enemigo del HUD para saber con quién está peleando el jugador.
        /// </summary>
        public bool InCombat => Current is State.Approach or State.Attacking or State.Cooldown or State.Retreat or State.Evading;

        public EnemyCooldownKind Kind => EnemyCooldownKind.Attack;

        /// <summary>Recarga entre ataques; Charge = 1 mientras ataca (el HUD lo marca como aviso).</summary>
        public CooldownState Cooldown => new CooldownState
        {
            Remaining = Mathf.Max(0f, _cooldownTimer),
            Duration = _stats != null ? T.attackCooldown : 0f,
            Charge = Current == State.Attacking ? 1f : 0f,
        };

        /// <summary>OnMove cada paso mientras se desplaza, OnWake al despertar. Ver <see cref="SoundEmitter"/>.</summary>
        public event System.Action<SoundTrigger> SoundTriggered;

        public void DeclareSoundTriggers(System.Collections.Generic.List<SoundTrigger> into)
        {
            var stats = _stats != null ? _stats : GetComponent<EnemyStats>();
            if (stats == null) return;
            if (stats.Tuning.Moves) into.Add(SoundTrigger.OnMove);
            if (stats.Tuning.Sleeps) into.Add(SoundTrigger.OnWake);
            if (stats.Tuning.Moves && stats.Tuning.defences != null && stats.Tuning.defences.Count > 0) into.Add(SoundTrigger.OnEvade);
        }

        private float _moveSoundTimer;

        public enum State
        {
            Idle,
            Approach,
            Retreat,
            Attacking,
            Cooldown,
            Waking,
            Returning,
            Dead,

            /// <summary>Un movimiento defensivo en curso (<see cref="EnemyDefence"/>): aviso y desplazamiento.</summary>
            Evading,
        }

        private EnemyStats _stats;
        private EnemyAnimation _animation;
        private EnemyAttack _attack;
        private Rigidbody2D _body;
        private Collider2D _collider;
        private Health _health;
        private Knockback _knockback;


        private Transform _target;
        private float _retargetTimer;
        private float _cooldownTimer;
        private float _contactTimer;
        private bool _seenTarget;
        private float _loseInterestTimer;
        private bool _retreating;
        private bool _retreatBlocked;

        // Retirada de un volador que no avanza (ver RetreatStalled): dónde estaba el paso anterior y
        // cuánto lleva sin moverse.
        private Vector2 _retreatFrom;
        private bool _retreatTracked;
        private float _retreatStall;

        // Movimiento defensivo en curso (EnemyTuning.defences): cuál, por dónde va (negativo = aún en
        // el aviso), cuánto dura, hacia dónde, y el enfriamiento de cada uno.
        private EnemyDefence _evade;
        private int _evadeIndex;
        private float _evadeTime, _evadeDuration;
        private Vector2 _evadeAway;
        private int _evadeSide;
        private bool _evadeHopped;
        private float[] _defenceCooldowns = System.Array.Empty<float>();

        /// <summary>La esquiva en curso lanzó un ataque (<see cref="EnemyDefence.attack"/>).</summary>
        private bool _evadeAttack;
        private bool _evadeShown;   // el gesto de la esquiva ya arrancó (en su primer paso de física, no al decidirla)
        private bool[] _lowHealthFired = System.Array.Empty<bool>();

        /// <summary>Segundos hasta volver a tirar el dado tras un intento fallido (para no tirar cada frame).</summary>
        private const float DefenceRetry = 1f;

        /// <summary>Dormido: quieto en reposo hasta detectar (ver <see cref="EnemyTuning.sleepsUntilDetected"/>).</summary>
        private bool _asleep;

        /// <summary>Falta poner la pose de dormido del primer frame (ver Update).</summary>
        private bool _pendingSleepPose;

        /// <summary>Donde empezó. Un dormilón vuelve aquí a dormirse cuando se rinde.</summary>
        private Vector2 _home;

        /// <summary>Distancia a <see cref="_home"/> a la que se da por llegado.</summary>
        private const float HomeArrival = 0.15f;

        /// <summary>Un volador en retirada que avanza menos de esta fracción de su paso está atascado…</summary>
        private const float RetreatStallFraction = 0.25f;

        /// <summary>…y tras este tiempo seguido así deja de retroceder y pelea.</summary>
        private const float RetreatStallTime = 0.2f;

        /// <summary>El suelo bajo los pies, medido una vez por paso de física. Vacío en un volador.</summary>

        /// <summary>Estado actual. Lo leen gizmos y depuración; nadie lo escribe desde fuera.</summary>
        public State Current { get; private set; } = State.Idle;

        /// <summary>Cada cuánto se reintenta localizar al objetivo si aún no existe.</summary>
        private const float RetargetInterval = 0.5f;

        /// <summary>Separación del morro para el rayo que sondea el suelo.</summary>
        private const float LedgeProbeInset = 0.05f;

        /// <summary>
        /// Cuánto por encima de los pies arranca el rayo del sondeo de bordes. Da holgura para que
        /// ir un instante despegado del suelo (un rebote, un empujón) no se lea como precipicio.
        /// </summary>
        private const float LedgeProbeRise = 0.35f;

        /// <summary>
        /// Desnivel que el enemigo acepta bajar andando sin considerarlo precipicio. Un escalón del
        /// tilemap no debe pararlo: si hay suelo dentro de esta caída, sigue avanzando y baja.
        /// </summary>
        private const float WalkableDrop = 1.5f;

        private static readonly RaycastHit2D[] Probe = new RaycastHit2D[8];

        private EnemyTuning T => _stats.Tuning;

        private void Awake()
        {
            _stats = GetComponent<EnemyStats>();
            _animation = GetComponent<EnemyAnimation>();
            _attack = GetComponent<EnemyAttack>();
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _health = GetComponent<Health>();
            _knockback = GetComponent<Knockback>();

            _body.freezeRotation = true;
        }

        private void OnEnable()
        {
            Current = State.Idle;
            _cooldownTimer = 0f;
            _seenTarget = false;
            _loseInterestTimer = 0f;
            _retreating = false;
            _retreatBlocked = false;
            _asleep = T.Sleeps;
            _evade = null;
            int defences = T.defences != null ? T.defences.Count : 0;
            _defenceCooldowns = new float[defences];
            _lowHealthFired = new bool[defences];
            _home = transform.position;
            _pendingSleepPose = _asleep;

            if (_animation != null)
            {
                _animation.AttackFinished += OnAttackFinished;
                _animation.WakeFinished += OnWakeFinished;
            }

            if (_attack != null) _attack.DeckAttackFinished += OnAttackFinished;

            if (_health == null) return;

            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_animation != null)
            {
                _animation.AttackFinished -= OnAttackFinished;
                _animation.WakeFinished -= OnWakeFinished;
            }

            if (_attack != null) _attack.DeckAttackFinished -= OnAttackFinished;

            if (_health == null) return;

            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        // ============================================================ decisión

        private void Update()
        {
            // Pose de dormido al nacer: aquí y no en OnEnable, para no depender del orden de
            // activación con el rig (que en su OnEnable vuelve a la pose del prefab).
            if (_pendingSleepPose)
            {
                _pendingSleepPose = false;
                if (_animation != null) _animation.PlaySleep(true);
            }

            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
            if (_contactTimer > 0f) _contactTimer -= Time.deltaTime;
            for (int i = 0; i < _defenceCooldowns.Length; i++)
                if (_defenceCooldowns[i] > 0f) _defenceCooldowns[i] -= Time.deltaTime;

            if (Current == State.Dead || !GameStateManager.CanPlayerAct) return;

            // Congelado o aturdido (armas del arsenal): ni decide ni ataca; el empujón quieto ya lo para en FixedUpdate.
            if (Arsenal.ElementStatus.Disables(this)) return;

            // Un ataque en curso no se interrumpe: manda la animación hasta que avise de que acabó.
            // Despertarse tampoco: es el aviso de que viene, y cortarlo lo hace ilegible.
            // Apartarse tampoco: se acaba solo (ver EvadeStep).
            if (Current is State.Attacking or State.Waking or State.Evading) return;

            var target = ResolveTarget();
            if (target == null)
            {
                Disengage();
                return;
            }

            Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
            float distance = Distance(toTarget);

            // Un enemigo de suelo no persigue a quien está dos plataformas más arriba: perseguirlo
            // sólo lo pegaría a una pared. El volador sí puede subir. Fuera de ese margen el
            // objetivo cuenta como "lejos" para la conciencia de abajo, pero no se olvida de golpe.
            bool reachable = T.Flies || Mathf.Abs(toTarget.y) <= T.verticalTolerance;

            if (T.Detects) UpdateAwareness(distance, reachable);

            // Dormido no hace nada más que esperar: el mismo enganche de siempre, pero lo que
            // dispara es el despertar, no la persecución.
            if (_asleep)
            {
                if (_seenTarget) BeginWake(toTarget);
                else Enter(State.Idle);
                return;
            }

            // Un salto o un escalón cruzan 'verticalTolerance' un instante: eso NO es motivo para
            // que un enemigo que persigue se plante en seco. Para los que se mueven manda sólo el
            // enganche (_seenTarget), que se suelta al salir del radio de detección durante
            // 'loseInterestGrace' segundos. Un Static no persigue: para él sigue mandando el
            // alcance vertical directo.
            bool disengaged = T.Detects ? !_seenTarget : !reachable;
            if (disengaged)
            {
                Disengage();
                return;
            }

            // Movimiento defensivo por cercanía: antes que retroceder o atacar — es justo la respuesta a
            // que el objetivo se le eche encima.
            if (TryDefend(DefenceTrigger.TargetNear, distance)) return;

            // Retroceder gana al ataque a propósito: un enemigo a distancia con el jugador encima
            // tiene que apartarse, no disparar a bocajarro. Es lo que lo hace sentir "a distancia".
            // Sólo lo hacen los que disparan (ver EnemyTuning.Retreats): un melé persigue y pega.
            // Con histéresis: entra al invadirle 'personalSpace', pero no vuelve a disparar hasta
            // recuperar 'personalSpace * retreatReleaseFactor' — sin esa banda muerta tiembla entre
            // disparar y retroceder justo en el borde. Y si tiene una pared detrás y no logra
            // alejarse (_retreatBlocked), deja de retroceder y pelea en vez de quedarse clavado.
            if (T.Retreats)
            {
                if (distance >= T.personalSpace) _retreatBlocked = false;

                float releaseAt = T.personalSpace * Mathf.Max(1f, T.retreatReleaseFactor);
                bool wantRetreat = _retreating ? distance < releaseAt : distance < T.personalSpace;
                _retreating = wantRetreat && !_retreatBlocked;

                if (_retreating)
                {
                    Enter(State.Retreat);
                    return;
                }
            }
            else
            {
                _retreating = false;
            }

            // Atacar sólo si además está a alcance vertical: si no, un objetivo que salta pegado
            // haría que el enemigo diera espadazos al aire. Con baraja (EnemyTuning.attacks) decide
            // EnemyAttack qué hay disponible: una carta puede llegar más lejos que 'attackRange' o
            // pedir justo que el objetivo esté por encima.
            if (_cooldownTimer <= 0f && CanAttack(distance, toTarget.y, reachable))
            {
                BeginAttack(toTarget);
                return;
            }

            // Sólo los que se mueven se acercan: un Static dormilón también "ficha" al objetivo
            // (para despertar y rendirse), pero se queda en su sitio esperando a tenerlo a tiro.
            if (T.Moves && _seenTarget && distance > T.attackRange)
            {
                Enter(State.Approach);
                return;
            }

            // A tiro pero enfriando: se queda en reposo mirando al objetivo, que es lo que pide un
            // estático entre disparo y disparo.
            Enter(_cooldownTimer > 0f && distance <= T.attackRange ? State.Cooldown : State.Idle);
            FaceTowards(toTarget);
        }

        /// <summary>
        /// Decide si el enemigo tiene "fichado" al objetivo (<see cref="_seenTarget"/>).
        ///
        /// <b>Engancha</b> al entrar el objetivo en <see cref="EnemyTuning.detectionRange"/>
        /// estando a la vista (alcance vertical).
        ///
        /// <b>Suelta</b> cuando el objetivo lleva <see cref="EnemyTuning.loseInterestGrace"/>
        /// segundos <b>seguidos</b> fuera de ese mismo <see cref="EnemyTuning.detectionRange"/>.
        /// El radio de detección es el límite en los dos sentidos, que es como se lee al dibujarlo
        /// con el gizmo; el margen de tiempo es la histéresis, y evita tener que mantener un
        /// segundo radio sólo para que no parpadee justo en el borde.
        ///
        /// Sólo cuenta la <b>distancia</b>: un salto o un escalón no sueltan al objetivo, porque
        /// el alcance vertical ya decide aparte si es alcanzable.
        /// </summary>
        private void UpdateAwareness(float distance, bool reachable)
        {
            // Engancha: hace falta verlo (alcance vertical) y que entre en el radio de detección.
            if (!_seenTarget)
            {
                _seenTarget = reachable && distance <= T.detectionRange;
                _loseInterestTimer = 0f;
                return;
            }

            // Dentro del radio: sigue fichado y el cronómetro de rendirse vuelve a cero.
            if (distance <= T.detectionRange)
            {
                _loseInterestTimer = 0f;
                return;
            }

            _loseInterestTimer += Time.deltaTime;
            if (_loseInterestTimer >= Mathf.Max(0f, T.loseInterestGrace))
            {
                _seenTarget = false;
                _loseInterestTimer = 0f;
            }
        }

        // ============================================================ movimiento

        private void FixedUpdate()
        {
            if (Current == State.Dead || !GameStateManager.CanPlayerAct)
            {
                Stop();
                return;
            }

            // Mientras sale despedido la IA no toca la velocidad: si la reescribiera cada
            // FixedUpdate el empujón se borraría en el mismo frame y el golpe no se notaría.
            if (_knockback != null && _knockback.IsActive) return;

            switch (Current)
            {
                case State.Approach:
                    MoveToward(Target(), T.moveSpeed);
                    break;

                case State.Retreat:
                    // Si no consigue moverse (borde o pared detrás) lo marca para que el cerebro
                    // pase a atacar en vez de dejarlo temblando contra el obstáculo.
                    _retreatBlocked = !MoveToward(Target(), -T.retreatSpeed) || RetreatStalled();
                    break;

                case State.Returning:
                    // Al llegar (o si un borde le corta el camino) se duerme donde esté.
                    if (!T.Moves || !ReturnHome()) FallAsleep();   // un Static se pliega donde está
                    break;

                // Sin anclar sigue persiguiendo durante el gesto: es lo que pide un kamikaze, que
                // no debe dejarse esquivar con un paso atrás mientras arde la mecha.
                case State.Attacking:
                    if (T.rootedWhileAttacking) Stop();
                    else MoveToward(Target(), T.moveSpeed);
                    break;

                case State.Evading:
                    EvadeStep();
                    break;

                case State.Waking:
                case State.Idle:
                case State.Cooldown:
                    Stop();
                    break;
            }

            UpdateMoveSound();
        }

        /// <summary>
        /// Un paso hacia (o desde, con velocidad negativa) el objetivo. El volador va recto en los
        /// dos ejes atravesando lo que haga falta; el de suelo camina siguiendo la pendiente y sin
        /// tirarse por un borde.
        /// </summary>
        /// <returns>True si dio el paso; false si tuvo que pararse (sin objetivo, borde o pared).</returns>
        private bool MoveToward(Transform target, float speed)
        {
            if (target == null) { Stop(); return false; }

            return MoveAlong((Vector2)target.position - (Vector2)transform.position, speed, T.hoverOffset);
        }

        /// <summary>
        /// ¿Un volador en retirada no consigue alejarse? Va recto y no sondea nada (ver
        /// <see cref="MoveAlong"/>): contra el suelo o una pared lo para la física y el paso "sale"
        /// igual, así que se quedaría empujando contra lo macizo sin disparar mientras el objetivo
        /// siga dentro de <see cref="EnemyTuning.personalSpace"/> — justo lo que pasa con el objetivo
        /// encima o debajo. Se mide lo que se movió de verdad desde el paso anterior.
        /// </summary>
        private bool RetreatStalled()
        {
            if (!T.Flies) return false;

            Vector2 now = _body.position;
            float expected = T.retreatSpeed * SlowStatus.SpeedScale(this) * Time.fixedDeltaTime * RetreatStallFraction;
            bool stuck = _retreatTracked && (now - _retreatFrom).sqrMagnitude < expected * expected;

            _retreatFrom = now;
            _retreatTracked = true;
            _retreatStall = stuck ? _retreatStall + Time.fixedDeltaTime : 0f;
            return _retreatStall >= RetreatStallTime;
        }

        /// <summary>
        /// Un paso de vuelta a <see cref="_home"/>. False al llegar, o si un borde o una pared le
        /// cortan el camino.
        /// </summary>
        private bool ReturnHome()
        {
            Vector2 toHome = _home - _body.position;
            float remaining = T.Flies ? toHome.magnitude : Mathf.Abs(toHome.x);

            if (remaining <= HomeArrival)
            {
                if (T.Flies) _body.position = _home;
                Stop();
                return false;
            }

            // Sin 'hoverOffset': vuelve a SU sitio exacto, no a flotar por encima de él.
            return MoveAlong(toHome, T.moveSpeed, 0f);
        }

        /// <summary>Un paso en la dirección de <paramref name="toGoal"/>. Ver <see cref="MoveToward"/>.</summary>
        private bool MoveAlong(Vector2 toGoal, float speed, float hover)
        {
            // Garantía única del arquetipo: un Static no se desplaza nunca, venga de donde venga
            // la orden.
            if (!T.Moves) { Stop(); return false; }

            // Ralentización (hielo): escala la velocidad, sea de avance, retirada o vuelta a casa.
            speed *= SlowStatus.SpeedScale(this);

            Vector2 toTarget = toGoal;
            if (speed >= 0f) FaceTowards(toTarget);

            if (T.Flies)
            {
                // Va recto al objetivo: las plataformas no existen para él (EnemyStats se las
                // excluye) y contra lo macizo lo para la física, sin esquive por rayos.
                Vector2 desired = toTarget + Vector2.up * hover;
                if (desired.sqrMagnitude < 0.0001f) { Stop(); return false; }

                SetVelocity(desired.normalized * speed);
                return true;
            }

            int direction = (toTarget.x >= 0f ? 1 : -1) * (speed >= 0f ? 1 : -1);

            // Sondear el borde sólo cuando el objetivo no está pegado: con el jugador delante hay
            // suelo de sobra, y un empujón entre cuerpos no debe leerse como precipicio y dejar al
            // enemigo clavado a media pelea.
            bool adjacent = Mathf.Abs(toTarget.x) <= Reach();
            if (!adjacent && !CanAdvance(direction)) { Stop(); return false; }

            SetVelocity(GroundMotion.Walk(direction * Mathf.Abs(speed), _body.linearVelocity));
            return true;
        }

        /// <summary>
        /// True si hay suelo un paso por delante, es decir: si se puede seguir andando.
        ///
        /// El rayo sale <b>desde los pies</b> (un poco por encima) y baja bastante a propósito. La
        /// versión anterior salía del <i>centro</i> del collider con un alcance de
        /// <c>extents.y + ledgeProbeDepth</c>, lo que en la práctica sólo miraba
        /// <see cref="EnemyTuning.ledgeProbeDepth"/> por debajo de los pies: con el enemigo medido
        /// en escena eso dejaba <b>0,13 unidades</b> de margen sobre el suelo. Cualquier bache del
        /// tilemap, o estar un instante despegado del suelo (rebote, caída, empujón), hacía que el
        /// rayo no llegase, se leyera "precipicio" y el enemigo se plantase en seco <b>en plena
        /// persecución</b>, en terreno que a ojo es completamente plano.
        ///
        /// Ahora sólo cuenta como precipicio un hueco de verdad: un desnivel mayor que
        /// <see cref="WalkableDrop"/> + <see cref="EnemyTuning.ledgeProbeDepth"/> por debajo de los
        /// pies. Un escalón se baja andando — para eso tiene gravedad.
        /// </summary>
        private bool CanAdvance(int direction)
        {
            if (T.Flies || !T.stopAtLedges || _collider == null) return true;

            var bounds = _collider.bounds;
            var origin = new Vector2(
                direction > 0 ? bounds.max.x + LedgeProbeInset : bounds.min.x - LedgeProbeInset,
                bounds.min.y + LedgeProbeRise);
            float length = LedgeProbeRise + WalkableDrop + Mathf.Max(0f, T.ledgeProbeDepth);

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = T.obstacleLayers, useTriggers = false };
            int count = Physics2D.Raycast(origin, Vector2.down, filter, Probe, length);

            for (int i = 0; i < count; i++)
            {
                var hit = Probe[i].collider;
                if (hit == null || hit == _collider || hit.transform.IsChildOf(transform)) continue;
                if (_target != null && hit.transform.IsChildOf(_target.root)) continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Quedarse quieto. En un volador es flotar (no se desploma); en uno de suelo, X a cero y la
        /// caída se respeta.
        /// </summary>
        private void Stop()
        {
            if (T.Flies)
            {
                SetVelocity(Vector2.zero);
                return;
            }

            SetVelocity(GroundMotion.Halt(_body.linearVelocity));
        }

        /// <summary>Un cuerpo Static (torreta en pared o techo) no admite velocidad: se queda donde está.</summary>
        private void SetVelocity(Vector2 velocity)
        {
            if (_body.bodyType == RigidbodyType2D.Static) return;
            _body.linearVelocity = velocity;
        }

        private float Reach() => (_collider != null ? _collider.bounds.extents.x : 0.3f) + 0.4f;

        private float Distance(Vector2 toTarget) => T.Flies ? toTarget.magnitude : Mathf.Abs(toTarget.x);

        // ============================================================ estados

        // Un OnMove cada moveSoundInterval mientras se desplaza él solo (no al salir despedido ni al caer).
        private void UpdateMoveSound()
        {
            _moveSoundTimer -= Time.fixedDeltaTime;
            if (_moveSoundTimer > 0f || _body == null) return;

            bool walking = Current is State.Approach or State.Retreat or State.Returning ||
                           (Current == State.Attacking && !T.rootedWhileAttacking);
            if (!walking || _body.linearVelocity.sqrMagnitude < 0.04f) return;

            _moveSoundTimer = T.moveSoundInterval;
            SoundTriggered?.Invoke(SoundTrigger.OnMove);
        }

        private void Enter(State next)
        {
            if (Current == next) return;
            Current = next;
            _retreatTracked = false;
            _retreatStall = 0f;

            if (_animation != null)
            {
                _animation.SetMoveBackwards(next is State.Retreat);
                _animation.SetMoving(next is State.Approach or State.Retreat or State.Returning);
                // Con objetivo: lo persigue, se retira, ataca, enfría o se aparta. Sin él: reposo, volver a su sitio, muerto.
                _animation.SetAlert(next is State.Approach or State.Retreat or State.Attacking or State.Cooldown
                                        or State.Waking or State.Evading);
            }
        }

        /// <summary>
        /// Sin objetivo que perseguir. El normal se queda en reposo donde esté; el dormilón vuelve
        /// a su sitio a dormirse — su reposo es el clip de dormir, y dormido en mitad de la
        /// nada se leería como un fallo.
        /// </summary>
        private void Disengage()
        {
            if (T.Sleeps && !_asleep) Enter(State.Returning);
            else Enter(State.Idle);
        }

        private void FallAsleep()
        {
            _asleep = true;
            _seenTarget = false;
            _loseInterestTimer = 0f;
            Enter(State.Idle);
            if (_animation != null) _animation.PlaySleep(false);
        }

        /// <summary>Se despierta: queda enganchado y reproduce 'Wake' quieto antes de moverse.</summary>
        private void BeginWake(Vector2 toTarget)
        {
            _asleep = false;
            _seenTarget = true;
            _loseInterestTimer = 0f;

            Enter(State.Waking);
            Stop();
            FaceTowards(toTarget);
            SoundTriggered?.Invoke(SoundTrigger.OnWake);

            if (_animation != null) _animation.PlayWake();
            else OnWakeFinished();
        }

        private void OnWakeFinished()
        {
            if (Current != State.Waking) return;

            // Directo a moverse, sin pasar por el reposo: el reposo de un dormilón es el clip de
            // dormir, y un frame de él entre despertar y volar se ve como un parpadeo.
            // Un Static (torreta) no se acerca: despierta y se queda en guardia en su sitio.
            if (_target != null)
            {
                Enter(T.Moves ? State.Approach : State.Idle);
                return;
            }

            _seenTarget = false;
            Enter(State.Returning);
        }

        /// <summary>
        /// ¿Hay ataque que lanzar ahora? Sin <see cref="EnemyAttack"/> (o sin baraja) es la regla de
        /// siempre: a tiro y a alcance vertical.
        /// </summary>
        private bool CanAttack(float distance, float heightDelta, bool reachable) =>
            _attack != null ? _attack.Choose(distance, heightDelta, reachable)
                            : reachable && distance <= T.attackRange;

        private void BeginAttack(Vector2 toTarget)
        {
            Enter(State.Attacking);
            Stop();
            FaceTowards(toTarget);

            // Apuntar antes de lanzar: el golpe saldrá luego, cuando lo diga el clip, pero el punto
            // se congela ahora para que no persiga al jugador a mitad del gesto.
            if (_attack != null) _attack.AimAt(AimPoint());

            // Si salió una carta de la baraja, el ataque lo lleva EnemyAttackHost entero (aviso,
            // golpe y recuperación) y avisa al acabar; si no, el ataque propio por la animación.
            if (_attack != null && _attack.BeginChosen(_target)) return;

            if (_animation != null) _animation.PlayAttack();
            else OnAttackFinished();
        }

        // ============================================================ movimientos defensivos

        /// <summary>
        /// ¿Toca apartarse por <paramref name="trigger"/>? Recorre <see cref="EnemyTuning.defences"/> y
        /// lanza el primero que cumpla su condición, esté fuera de enfriamiento y saque su probabilidad.
        /// Un intento fallido espera <see cref="DefenceRetry"/> antes de volver a tirar. Nunca corta un
        /// ataque, un despertar ni otra esquiva; dormido o sin objetivo no hay de qué apartarse.
        /// </summary>
        private bool TryDefend(DefenceTrigger trigger, float distance = float.MaxValue)
        {
            var list = T.defences;
            if (list == null || list.Count == 0 || !T.Moves || _asleep || _target == null) return false;
            if (Current is State.Dead or State.Attacking or State.Waking or State.Evading) return false;
            if (_defenceCooldowns.Length != list.Count) return false;   // la lista cambió en caliente: al reactivarse

            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (d == null || d.trigger != trigger || _defenceCooldowns[i] > 0f) continue;

                switch (trigger)
                {
                    case DefenceTrigger.TargetNear:
                        if (distance >= d.distance) continue;
                        break;

                    case DefenceTrigger.LowHealth:
                        if (_lowHealthFired[i] || _health == null || _health.Normalized > d.healthFraction) continue;
                        _lowHealthFired[i] = true;   // una vez por vida, salga o no el dado
                        break;
                }

                if (Random.value > d.chance)
                {
                    _defenceCooldowns[i] = Mathf.Min(DefenceRetry, Mathf.Max(0.1f, d.cooldown));
                    continue;
                }

                if (BeginEvade(d, i)) return true;
            }

            return false;
        }

        /// <summary>¿Este movimiento va hacia el objetivo? <see cref="DefenceDirection.ByRange"/>: si está más cerca de <c>attackRange</c> que de <c>personalSpace</c> (lejos: avanza; cerca: huye).</summary>
        private bool GoesToward(EnemyDefence d, float distance)
        {
            switch (d.direction)
            {
                case DefenceDirection.Toward: return true;
                case DefenceDirection.ByRange: return Mathf.Abs(distance - T.attackRange) < Mathf.Abs(distance - T.personalSpace);
                default: return false;
            }
        }

        /// <summary>
        /// Diagonal de una propulsión: la X sale de <paramref name="horizontal"/> (hacia / lejos del objetivo) y la Y
        /// del terreno — más cerca del techo baja, más cerca del suelo sube — con un ángulo de <c>angle</c> ± <c>angleVariance</c>.
        /// </summary>
        private Vector2 TerrainDiagonal(EnemyDefence d, float horizontal)
        {
            float x = horizontal >= 0f ? 1f : -1f;
            float y = transform.position.y >= (d.floorY + d.ceilingY) * 0.5f ? -1f : 1f;
            float angle = (d.angle + Random.Range(-d.angleVariance, d.angleVariance)) * Mathf.Deg2Rad;
            return new Vector2(x * Mathf.Cos(angle), y * Mathf.Sin(angle));
        }

        private bool BeginEvade(EnemyDefence d, int index)
        {
            Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
            Vector2 away = T.Flies ? -toTarget : new Vector2(-toTarget.x, 0f);
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.left;
            if (GoesToward(d, Distance(toTarget))) away = -away;   // ofensivo: el mismo impulso, hacia el objetivo
            if (T.Flies && d.diagonalByTerrain) away = TerrainDiagonal(d, away.x);

            // Uno de suelo no se tira por un borde: si detrás no hay suelo, no se aparta.
            if (!T.Flies && !CanAdvance(away.x >= 0f ? 1 : -1))
            {
                _defenceCooldowns[index] = DefenceRetry;
                return false;
            }

            _evade = d;
            _evadeIndex = index;
            _evadeAway = away;
            _evadeSide = Random.value < 0.5f ? -1 : 1;
            _evadeHopped = false;
            _evadeTime = -Mathf.Max(0f, d.anticipation);
            _evadeDuration = DefenceMotion.Duration(d, T.Flies, Mathf.Abs(Physics2D.gravity.y) * _body.gravityScale);
            _defenceCooldowns[index] = d.cooldown + d.anticipation + _evadeDuration;

            Enter(State.Evading);
            Stop();
            FaceTowards(toTarget);   // se aparta sin darle la espalda

            // Con ataque: su gesto se reproduce entero mientras se aparta y sale en sus sueltas (la
            // primera, al acabar de apartarse). Sin él, el gesto también se reproduce entero (sus pasos
            // "dura el aviso" llenan aviso + movimiento), en silencio: no avisa de ningún ataque.
            _evadeAttack = false;
            if (d.attack != null && _attack != null)
            {
                _attack.AimAt(AimPoint());
                _evadeAttack = _attack.RunDefenceAttack(d.attack, _target, Mathf.Max(0f, d.anticipation) + _evadeDuration);
            }

            // Sin ataque, el gesto arranca con el primer paso de la esquiva (EvadeStep), no aquí: tras un golpe el
            // empujón la retrasa, y un gesto lanzado ya habría acabado cuando el cuerpo por fin se aparta.
            _evadeShown = _evadeAttack;
            SoundTriggered?.Invoke(SoundTrigger.OnEvade);
            return true;
        }

        /// <summary>Un paso de física de la esquiva: el aviso quieto, luego el desplazamiento de <see cref="DefenceMotion"/>.</summary>
        private void EvadeStep()
        {
            if (_evade == null) { EndEvade(); return; }

            if (!_evadeShown)
            {
                _evadeShown = true;
                if (_animation != null && !string.IsNullOrWhiteSpace(_evade.gesture))
                    _animation.PlayMotion(_evade.gesture, Mathf.Max(0f, -_evadeTime) + _evadeDuration);
            }

            if (_evadeTime < 0f)
            {
                Stop();
                _evadeTime += Time.fixedDeltaTime;
                return;
            }

            float k = _evadeTime / _evadeDuration;
            if (k >= 1f) { EndEvade(); return; }

            Vector2 velocity = DefenceMotion.Velocity(_evade, k, _evadeDuration, _evadeAway, T.Flies, _evadeSide)
                               * SlowStatus.SpeedScale(this);

            if (T.Flies)
            {
                SetVelocity(velocity);
            }
            else if (_evade.move == DefenceMove.HopBack)
            {
                // El impulso una vez; después la X la pone la esquiva y la Y la física (cae solo).
                float y = _body.linearVelocity.y;
                if (!_evadeHopped)
                {
                    _evadeHopped = true;
                    y = DefenceMotion.HopImpulse(_evade.hopHeight, Mathf.Abs(Physics2D.gravity.y) * _body.gravityScale);
                }

                SetVelocity(new Vector2(velocity.x, y));
            }
            else
            {
                int direction = velocity.x >= 0f ? 1 : -1;
                if (!CanAdvance(direction)) { EndEvade(); return; }   // se acaba el suelo: hasta aquí
                SetVelocity(GroundMotion.Walk(velocity.x, _body.linearVelocity));
            }

            _evadeTime += Time.fixedDeltaTime;
        }

        private void EndEvade()
        {
            bool hadGesture = _evade != null && !string.IsNullOrWhiteSpace(_evade.gesture);
            bool attacking = _evadeAttack;
            _evade = null;
            _evadeAttack = false;
            if (Current != State.Evading) return;

            // El ataque que lanzó al apartarse sigue en marcha (le quedan sueltas, o su recuperación):
            // se queda quieto atacando hasta que acabe, como con cualquier ataque de la baraja.
            if (attacking && _attack != null && _attack.HostRunning)
            {
                Stop();
                Enter(State.Attacking);
                return;
            }

            if (_animation != null && hadGesture && !attacking) _animation.EndMotion();
            Stop();
            Enter(State.Idle);   // el siguiente Update decide: perseguir, atacar, retirarse
        }

        private void OnAttackFinished()
        {
            if (Current is State.Dead or State.Evading) return;

            _cooldownTimer = T.attackCooldown;
            Enter(State.Cooldown);
            if (_animation != null) _animation.PlayTransition(_cooldownTimer);

            // Movimiento "al acabar un ataque" (propulsión de reposicionado): ya en Cooldown, que no lo bloquea.
            TryDefend(DefenceTrigger.AfterAttack);
        }

        /// <summary>
        /// Dónde apuntar: el centro del collider del objetivo, no su pivote. El pivote de un
        /// personaje suele estar en los pies, así que apuntar ahí manda el tiro al suelo — y con
        /// una boca de disparo a la altura de la mano, el resultado es pasar por encima o por
        /// debajo pero casi nunca acertar.
        /// </summary>
        private Vector2 AimPoint()
        {
            if (_target == null) return transform.position;

            var targetCollider = _target.GetComponentInParent<Collider2D>();
            return targetCollider != null ? (Vector2)targetCollider.bounds.center
                                          : (Vector2)_target.position;
        }

        private void FaceTowards(Vector2 toTarget)
        {
            // En su propio espacio: una torreta montada en una pared o en el techo está girada, y su "derecha" ya no es la
            // del mundo. Sin giro es lo mismo que mirar la X del mundo.
            Vector2 local = transform.InverseTransformDirection(toTarget);
            if (_animation == null || Mathf.Abs(local.x) < 0.01f) return;
            _animation.SetFacing(local.x >= 0f ? 1 : -1);
        }

        // ============================================================ objetivo

        private Transform Target() => _target;

        /// <summary>
        /// Localiza al objetivo por etiqueta y lo cachea, reintentando cada poco si aún no existe:
        /// el jugador de una run es un objeto persistente que puede no estar todavía cuando la
        /// sección acaba de cargar.
        ///
        /// Distancia contra una referencia cacheada, no un collider de trigger: para el puñado de
        /// enemigos que hay en pantalla sale más barato que meter cuerpos y capas en el broadphase,
        /// y evita tener que configurar la matriz de colisiones para que los rangos funcionen.
        /// </summary>
        private Transform ResolveTarget()
        {
            if (_target != null) return _target;

            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer > 0f) return null;
            _retargetTimer = RetargetInterval;

            if (string.IsNullOrEmpty(T.targetTag)) return null;

            var found = GameObject.FindGameObjectWithTag(T.targetTag);
            _target = found != null ? found.transform : null;
            return _target;
        }

        // ============================================================ daño y muerte

        private void OnDamaged(float amount)
        {
            if (_animation != null) _animation.PlayHurt();

            // Un golpe despierta a un dormilón aunque quien dispara esté fuera de su detección.
            if (_asleep && _health != null && !_health.IsDead)
                BeginWake(_target != null ? (Vector2)(_target.position - transform.position) : Vector2.zero);
            else if (_health != null && !_health.IsDead)
            {
                // Reacciones defensivas al golpe: primero la de poca vida (una sola vez), luego la de "al recibir".
                if (!TryDefend(DefenceTrigger.LowHealth)) TryDefend(DefenceTrigger.OnHit);
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            var tuning = T;
            if (tuning.contactDamage <= 0f || _contactTimer > 0f || Current == State.Dead) return;

            var other = collision.collider.GetComponentInParent<Health>();
            if (other == null || other == _health) return;
            if (!string.IsNullOrEmpty(tuning.targetTag) && !other.CompareTag(tuning.targetTag)) return;

            other.TakeDamage(tuning.contactDamage * _stats.OutgoingDamageScale, transform.position, tuning.contactKnockbackMultiplier);
            _contactTimer = tuning.contactDamageCooldown;
        }

        private void OnDied()
        {
            _evade = null;
            Current = State.Dead;
            Stop();
            if (_animation != null) _animation.PlayDeath();

            foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;
            _body.simulated = false;
        }
    }
}
