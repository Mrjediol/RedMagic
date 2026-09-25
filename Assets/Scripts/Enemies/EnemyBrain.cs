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
    public class EnemyBrain : MonoBehaviour
    {
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
        }

        private EnemyStats _stats;
        private EnemyAnimation _animation;
        private EnemyAttack _attack;
        private Rigidbody2D _body;
        private Collider2D _collider;
        private Health _health;
        private Knockback _knockback;

        private SoundEmitter _sound;

        private Transform _target;
        private float _retargetTimer;
        private float _cooldownTimer;
        private float _contactTimer;
        private bool _seenTarget;
        private float _loseInterestTimer;
        private bool _retreating;
        private bool _retreatBlocked;

        /// <summary>Dormido: quieto en reposo hasta detectar (ver <see cref="EnemyTuning.sleepsUntilDetected"/>).</summary>
        private bool _asleep;

        /// <summary>Donde empezó. Un dormilón vuelve aquí a dormirse cuando se rinde.</summary>
        private Vector2 _home;

        /// <summary>Distancia a <see cref="_home"/> a la que se da por llegado.</summary>
        private const float HomeArrival = 0.15f;

        /// <summary>El suelo bajo los pies, medido una vez por paso de física. Vacío en un volador.</summary>
        private GroundContact _ground;

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
            _sound = GetComponent<SoundEmitter>();

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
            _home = transform.position;

            if (_animation != null)
            {
                _animation.AttackFinished += OnAttackFinished;
                _animation.WakeFinished += OnWakeFinished;
            }

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

            if (_health == null) return;

            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        // ============================================================ decisión

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
            if (_contactTimer > 0f) _contactTimer -= Time.deltaTime;

            if (Current == State.Dead || !GameStateManager.CanPlayerAct) return;

            // Un ataque en curso no se interrumpe: manda la animación hasta que avise de que acabó.
            // Despertarse tampoco: es el aviso de que viene, y cortarlo lo hace ilegible.
            if (Current is State.Attacking or State.Waking) return;

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

            if (T.Moves) UpdateAwareness(distance, reachable);

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
            bool disengaged = T.Moves ? !_seenTarget : !reachable;
            if (disengaged)
            {
                Disengage();
                return;
            }

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
            // haría que el enemigo diera espadazos al aire.
            if (reachable && distance <= T.attackRange && _cooldownTimer <= 0f)
            {
                BeginAttack(toTarget);
                return;
            }

            if (_seenTarget && distance > T.attackRange)
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

            // El suelo bajo los pies, una vez por paso de física: de ahí salen tanto seguir la
            // rampa al andar como no resbalar al pararse. Un volador no lo necesita — el terreno
            // ni siquiera le estorba (ver EnemyStats.Apply).
            _ground = T.Flies ? default : GroundMotion.Probe(_collider, T.obstacleLayers);

            switch (Current)
            {
                case State.Approach:
                    MoveToward(Target(), T.moveSpeed);
                    break;

                case State.Retreat:
                    // Si no consigue moverse (borde o pared detrás) lo marca para que el cerebro
                    // pase a atacar en vez de dejarlo temblando contra el obstáculo.
                    _retreatBlocked = !MoveToward(Target(), -T.retreatSpeed);
                    break;

                case State.Returning:
                    // Al llegar (o si un borde le corta el camino) se duerme donde esté.
                    if (!ReturnHome()) FallAsleep();
                    break;

                // Sin anclar sigue persiguiendo durante el gesto: es lo que pide un kamikaze, que
                // no debe dejarse esquivar con un paso atrás mientras arde la mecha.
                case State.Attacking:
                    if (T.rootedWhileAttacking) Stop();
                    else MoveToward(Target(), T.moveSpeed);
                    break;

                case State.Waking:
                case State.Idle:
                case State.Cooldown:
                    Stop();
                    break;
            }
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
            // Ralentización (hielo): escala la velocidad, sea de avance, retirada o vuelta a casa.
            speed *= SlowStatus.SpeedScale(this);

            Vector2 toTarget = toGoal;
            if (speed >= 0f) FaceTowards(toTarget);

            if (T.Flies)
            {
                // Para un volador el terreno no existe: va recto al objetivo. Ya no esquiva nada
                // porque ya no choca con nada (EnemyStats le excluye la capa de terreno), y el
                // esquive por rayos que había aquí sólo servía para bordear plataformas.
                Vector2 desired = toTarget + Vector2.up * hover;
                if (desired.sqrMagnitude < 0.0001f) { Stop(); return false; }

                _body.linearVelocity = desired.normalized * speed;
                return true;
            }

            int direction = (toTarget.x >= 0f ? 1 : -1) * (speed >= 0f ? 1 : -1);

            // Sondear el borde sólo cuando el objetivo no está pegado: con el jugador delante hay
            // suelo de sobra, y un empujón entre cuerpos no debe leerse como precipicio y dejar al
            // enemigo clavado a media pelea.
            bool adjacent = Mathf.Abs(toTarget.x) <= Reach();
            if (!adjacent && !CanAdvance(direction)) { Stop(); return false; }

            // Seguir la pendiente en vez de empujar contra ella: en una plataforma inclinada,
            // escribir sólo la X deja al enemigo temblando al pie de la cuesta, y al pararse lo
            // deja resbalando. Mismo criterio de rampa transitable que usa el jugador.
            _body.linearVelocity = GroundMotion.AlongSlope(_ground, direction * Mathf.Abs(speed),
                                                           _body.linearVelocity);
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
        /// Quedarse quieto. En un volador es flotar (no se desploma); en uno de suelo es pararse
        /// <b>sin resbalar</b>: en una rampa, poner sólo la X a cero deja que la gravedad lo baje
        /// deslizando, así que apoyado se frena del todo.
        /// </summary>
        private void Stop()
        {
            if (T.Flies)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }

            _body.linearVelocity = GroundMotion.Halt(_ground, _body.linearVelocity);
        }

        private float Reach() => (_collider != null ? _collider.bounds.extents.x : 0.3f) + 0.4f;

        private float Distance(Vector2 toTarget) => T.Flies ? toTarget.magnitude : Mathf.Abs(toTarget.x);

        // ============================================================ estados

        private void Enter(State next)
        {
            if (Current == next) return;
            Current = next;

            if (_animation != null)
                _animation.SetMoving(next is State.Approach or State.Retreat or State.Returning);
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

            if (_animation != null) _animation.PlayWake();
            else OnWakeFinished();
        }

        private void OnWakeFinished()
        {
            if (Current != State.Waking) return;

            // Directo a moverse, sin pasar por el reposo: el reposo de un dormilón es el clip de
            // dormir, y un frame de él entre despertar y volar se ve como un parpadeo.
            if (_target != null)
            {
                Enter(State.Approach);
                return;
            }

            _seenTarget = false;
            Enter(State.Returning);
        }

        private void BeginAttack(Vector2 toTarget)
        {
            Enter(State.Attacking);
            Stop();
            FaceTowards(toTarget);

            // Apuntar antes de lanzar: el golpe saldrá luego, cuando lo diga el clip, pero el punto
            // se congela ahora para que no persiga al jugador a mitad del gesto.
            if (_attack != null) _attack.AimAt(AimPoint());

            if (_animation != null) _animation.PlayAttack();
            else OnAttackFinished();
        }

        private void OnAttackFinished()
        {
            if (Current == State.Dead) return;

            _cooldownTimer = T.attackCooldown;
            Enter(State.Cooldown);
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
            if (_animation == null || Mathf.Abs(toTarget.x) < 0.01f) return;
            _animation.SetFacing(toTarget.x >= 0f ? 1 : -1);
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
            _sound?.Play("OnHit");
            if (_animation != null) _animation.PlayHurt();

            // Un golpe despierta a un dormilón aunque quien dispara esté fuera de su detección.
            if (_asleep && _health != null && !_health.IsDead)
                BeginWake(_target != null ? (Vector2)(_target.position - transform.position) : Vector2.zero);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            var tuning = T;
            if (tuning.contactDamage <= 0f || _contactTimer > 0f || Current == State.Dead) return;

            var other = collision.collider.GetComponentInParent<Health>();
            if (other == null || other == _health) return;
            if (!string.IsNullOrEmpty(tuning.targetTag) && !other.CompareTag(tuning.targetTag)) return;

            other.TakeDamage(tuning.contactDamage, transform.position, tuning.contactKnockbackMultiplier);
            _contactTimer = tuning.contactDamageCooldown;
        }

        private void OnDied()
        {
            Current = State.Dead;
            Stop();

            _sound?.Play("OnDeath");
            if (_animation != null) _animation.PlayDeath();

            foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;
            _body.simulated = false;
        }
    }
}
