using System;
using RedMagic.Bosses;
using RedMagic.Pipeline;
using RedMagic.Rigging;
using UnityEngine;

namespace RedMagic.Enemies
{
    /// <summary>
    /// La capa de animación del enemigo: traduce estados a clips y, sobre todo, <b>avisa del frame
    /// exacto en el que el golpe sale</b>.
    ///
    /// Esa es la razón de ser del componente. Un ataque que hace daño cuando le apetece a un
    /// temporizador no se lee: el proyectil aparece antes de que el bicho levante el brazo, o
    /// después de bajarlo. Aquí el clip de ataque lleva dos <c>AnimationEvent</c> puestos por el
    /// pipeline — <see cref="OnAttackRelease"/> en el frame en que suelta y
    /// <see cref="OnAttackFinished"/> en el último —, así que el golpe cae clavado con el dibujo
    /// pase lo que pase con la velocidad de reproducción.
    ///
    /// El <see cref="Animator"/> va en la <b>raíz</b> del enemigo a propósito, no en el hijo del
    /// sprite: los AnimationEvent sólo llegan a componentes del mismo GameObject, y el cerebro y
    /// los ataques viven en la raíz. Los clips animan al hijo por ruta ("Sprite").
    ///
    /// Sin Animator (un enemigo por pool, que anima con <see cref="SpriteStateMachine"/>) se cae a
    /// tiempos: el evento no existe, así que el aviso sale a
    /// <see cref="EnemyTuning.attackReleaseFallback"/> segundos.
    ///
    /// Con un <see cref="RigDriver"/> (cuerpo hecho de sprites sueltos, ver <c>ProceduralRig</c>)
    /// manda el rig: cada orden se le reenvía y sus eventos hacen de <c>AnimationEvent</c>.
    ///
    /// También es el <see cref="IAttackBody"/> del enemigo: los ataques del catálogo (la baraja de
    /// <c>EnemyTuning.attacks</c>) piden aquí su <b>gesto</b> por nombre — un <c>RigGesture</c> del
    /// rig o un estado del Animator — y mientras dura, la suelta del gesto no dispara el ataque
    /// propio.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyAnimation : MonoBehaviour, IAttackBody
    {
        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string Attack = "Attack";
        public const string Hurt = "Hurt";
        public const string Death = "Death";
        public const string Wake = "Wake";

        [Tooltip("Vacío = se busca en este objeto y en sus hijos.")]
        [SerializeField] private Animator animator;

        [Tooltip("Alternativa sin Animator, para lo que pase por pool.")]
        [SerializeField] private SpriteStateMachine flipbook;

        [Tooltip("Sprite al que se le da la vuelta según hacia dónde mira. Vacío = el del hijo.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Cuerpo por partes. Vacío = se busca en este objeto. Si existe, manda sobre el " +
                 "Animator y el flipbook.")]
        [SerializeField] private RigDriver rig;

        private static readonly int MovingKey = Animator.StringToHash("Moving");
        private static readonly int AttackKey = Animator.StringToHash("Attack");
        private static readonly int HurtKey = Animator.StringToHash("Hurt");
        private static readonly int DeadKey = Animator.StringToHash("Dead");
        private static readonly int WakeKey = Animator.StringToHash("Wake");

        private static readonly int IdleSpeedKey = Animator.StringToHash("IdleSpeed");
        private static readonly int WalkSpeedKey = Animator.StringToHash("WalkSpeed");
        private static readonly int AttackSpeedKey = Animator.StringToHash("AttackSpeed");
        private static readonly int HurtSpeedKey = Animator.StringToHash("HurtSpeed");
        private static readonly int DeathSpeedKey = Animator.StringToHash("DeathSpeed");
        private static readonly int WakeSpeedKey = Animator.StringToHash("WakeSpeed");

        private EnemyStats _stats;
        private EnemyAttack _attack;
        private float _fallbackRelease = -1f;
        private float _fallbackFinish = -1f;
        private float _wakeFinish = -1f;
        private bool _released;
        private bool _hasWakeTrigger;

        // --- gesto de un ataque del catálogo (IAttackBody)
        private static readonly int IdleHash = Animator.StringToHash(Idle);

        /// <summary>Hay un gesto de la baraja en marcha: las sueltas son suyas, no del ataque propio.</summary>
        private bool _inGesture;

        /// <summary>El rig está con un gesto con nombre: sus avisos de suelta/fin no son los de PlayAttack.</summary>
        private bool _rigGesture;

        private int _gestureHash;
        private float _gestureReleaseAt = -1f;

        /// <summary>True desde que el gesto en curso ha soltado su golpe.</summary>
        public bool Released { get; private set; }

        /// <summary>Segundos que tardará en soltar el último gesto lanzado.</summary>
        public float SecondsToRelease { get; private set; }

        /// <summary>Sueltas que lleva / que tiene el gesto de la baraja en curso (un rig puede soltar varias veces).</summary>
        public int ReleaseCount => HasRig && _rigGesture ? rig.ReleasesFired : (Released ? 1 : 0);
        public int ReleaseTotal => HasRig && _rigGesture ? Mathf.Max(1, rig.ReleaseTotal) : 1;

        /// <summary>
        /// Qué parámetros de velocidad existen <b>de verdad</b> en este controller. No todos los
        /// estados los tienen: un Static no genera clip de "Walk", así que su controller nunca
        /// tuvo "WalkSpeed" — y <c>Animator.SetFloat</c> con un hash que no existe no falla en
        /// silencio, avisa por consola. Avisar una vez por segundo sería ruido; avisar en CADA
        /// Update de CADA enemigo es lo que se comió los fps: un warning con stack trace es caro,
        /// y aquí salían decenas por segundo. Se comprueba una vez en Awake y punto.
        /// </summary>
        private bool _hasIdleSpeed, _hasWalkSpeed, _hasAttackSpeed, _hasHurtSpeed, _hasDeathSpeed,
                     _hasWakeSpeed;

        /// <summary>Último valor aplicado de cada uno, para no repetir la llamada nativa si no cambia.</summary>
        private float _lastIdle = float.NaN, _lastWalk = float.NaN, _lastAttack = float.NaN,
                      _lastHurt = float.NaN, _lastDeath = float.NaN, _lastWake = float.NaN;

        /// <summary>El frame en el que el golpe sale. Lo escucha el ataque.</summary>
        public event Action AttackReleased;

        /// <summary>El clip de ataque ha terminado. Lo escucha el cerebro para volver al reposo.</summary>
        public event Action AttackFinished;

        /// <summary>El clip de despertar ha terminado. Lo escucha el cerebro para echar a moverse.</summary>
        public event Action WakeFinished;

        /// <summary>True si hay un Animator de verdad (y por tanto eventos de animación).</summary>
        public bool HasAnimator => animator != null && animator.runtimeAnimatorController != null;

        /// <summary>True si el cuerpo es un rig por partes (y es él quien avisa del golpe).</summary>
        public bool HasRig => rig != null && rig.enabled;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (flipbook == null) flipbook = GetComponentInChildren<SpriteStateMachine>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (rig == null) rig = GetComponent<RigDriver>();
            _stats = GetComponent<EnemyStats>();
            _attack = GetComponent<EnemyAttack>();

            CacheParameters();
            ApplySpeeds();
        }

        /// <summary>Qué parámetros de velocidad tiene de verdad el controller. Sólo hace falta una vez.</summary>
        private void CacheParameters()
        {
            if (!HasAnimator) return;

            foreach (var p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.nameHash == WakeKey)
                    _hasWakeTrigger = true;

                if (p.type != AnimatorControllerParameterType.Float) continue;

                if (p.nameHash == IdleSpeedKey) _hasIdleSpeed = true;
                else if (p.nameHash == WalkSpeedKey) _hasWalkSpeed = true;
                else if (p.nameHash == AttackSpeedKey) _hasAttackSpeed = true;
                else if (p.nameHash == HurtSpeedKey) _hasHurtSpeed = true;
                else if (p.nameHash == DeathSpeedKey) _hasDeathSpeed = true;
                else if (p.nameHash == WakeSpeedKey) _hasWakeSpeed = true;
            }
        }

        private void OnEnable()
        {
            _fallbackRelease = -1f;
            _fallbackFinish = -1f;
            _wakeFinish = -1f;
            _released = false;
            _inGesture = false;
            _rigGesture = false;
            _gestureReleaseAt = -1f;
            Released = false;

            _dead = false;
            if (_frozen && animator != null) animator.speed = 1f;
            _frozen = false;

            if (rig == null) return;
            rig.AttackReleased += OnRigReleased;
            rig.AttackFinished += OnRigFinished;
            rig.WakeFinished += OnRigWakeFinished;
        }

        private void OnDisable()
        {
            if (rig == null) return;
            rig.AttackReleased -= OnRigReleased;
            rig.AttackFinished -= OnRigFinished;
            rig.WakeFinished -= OnRigWakeFinished;
        }

        // El rig avisa con los mismos dos eventos de un gesto con nombre y de PlayAttack: los de un
        // gesto de la baraja se quedan aquí (sólo marcan la suelta) y no llegan al ataque propio.
        private void OnRigReleased()
        {
            if (_rigGesture) { Released = true; return; }

            if (_inGesture) return;
            _released = true;
            _fallbackRelease = -1f;
            AttackReleased?.Invoke();
        }

        private void OnRigFinished()
        {
            if (_rigGesture) _rigGesture = false;
            else Finish();
        }

        private void OnRigWakeFinished()
        {
            _wakeFinish = -1f;
            WakeFinished?.Invoke();
        }

        // Muerto: el clip de muerte se reproduce UNA vez y el cadáver se queda en su último dibujo.
        private bool _dead;
        private bool _frozen;

        /// <summary>
        /// Red de seguridad de la muerte con Animator: si el clip de muerte viene en bucle (los
        /// importados del exportador web lo traían), al pasar de su final se clava en el último
        /// frame y se para el Animator. Va en LateUpdate y fuerza la evaluación con Update(0): así el
        /// primer dibujo que el bucle acaba de poner nunca llega a pintarse. Con un clip sin bucle no
        /// hace nada (ya se queda quieto solo). Ver <c>Pipeline.AnimStates</c>.
        /// </summary>
        private void LateUpdate()
        {
            if (!_dead || _frozen || !HasAnimator || animator.IsInTransition(0)) return;

            var info = animator.GetCurrentAnimatorStateInfo(0);
            // Sólo en el estado de muerte: justo tras morir aún puede estar en Walk (en bucle, con
            // normalizedTime > 1) hasta que arranque la transición.
            if (!info.IsName(Death) || !info.loop || info.normalizedTime < 1f) return;

            animator.Play(info.fullPathHash, 0, 0.9999f);
            animator.speed = 0f;
            animator.Update(0f);
            _frozen = true;
        }

        /// <summary>
        /// Vuelca las velocidades de cada estado. Van como parámetros del Animator (uno por estado)
        /// en lugar de tocar <c>animator.speed</c>, porque así el reposo puede ir lento y el ataque
        /// rápido a la vez, que es justo lo que se quiere poder afinar.
        /// </summary>
        public void ApplySpeeds()
        {
            if (!HasAnimator || _stats == null) return;

            var t = _stats.Tuning;
            SetSpeed(_hasIdleSpeed, IdleSpeedKey, t.idleAnimSpeed, ref _lastIdle);
            SetSpeed(_hasWalkSpeed, WalkSpeedKey, t.moveAnimSpeed, ref _lastWalk);
            SetSpeed(_hasAttackSpeed, AttackSpeedKey, t.attackAnimSpeed, ref _lastAttack);
            SetSpeed(_hasHurtSpeed, HurtSpeedKey, t.hurtAnimSpeed, ref _lastHurt);
            SetSpeed(_hasDeathSpeed, DeathSpeedKey, t.deathAnimSpeed, ref _lastDeath);
            SetSpeed(_hasWakeSpeed, WakeSpeedKey, t.wakeAnimSpeed, ref _lastWake);
        }

        private void SetSpeed(bool exists, int key, float value, ref float last)
        {
            // Ni siquiera se intenta si el estado no existe en este controller (evita el warning
            // de parámetro inexistente) ni si el valor no ha cambiado (evita la llamada nativa de
            // sobra 60 veces por segundo por cada enemigo, aunque el parámetro sí exista).
            if (!exists || value == last) return;
            last = value;
            animator.SetFloat(key, value);
        }

        private void Update()
        {
            ApplySpeeds();
            TickFallback();
        }

        // ============================================================ órdenes del cerebro

        /// <summary>Andar o estar quieto. Sin clip de andar se queda en reposo, que es lo correcto.</summary>
        /// <summary>
        /// Enganchado a un objetivo (o ya no). Sólo lo usa un cuerpo por partes: sus sprites de "alerta"
        /// (<c>RigNamedState.Alert</c>). Con Animator no hay nada que cambiar.
        /// </summary>
        public void SetAlert(bool alert)
        {
            if (HasRig) rig.SetAlert(alert);
        }

        /// <summary>
        /// Entre ataque y ataque (quedan <paramref name="seconds"/> de enfriamiento): un cuerpo por partes
        /// puede hacer uno de sus gestos de transición. Con Animator no hay nada que hacer.
        /// </summary>
        public void PlayTransition(float seconds)
        {
            if (HasRig) rig.PlayTransition(seconds);
        }

        public void SetMoving(bool moving)
        {
            if (HasRig)
            {
                rig.SetMoving(moving);
                return;
            }

            if (HasAnimator) animator.SetBool(MovingKey, moving);

            if (flipbook == null) return;
            flipbook.Play(moving && flipbook.Has(Walk) ? Walk : Idle);
        }

        /// <summary>Lanza el ataque propio. El golpe sale luego, cuando lo diga el clip (o el rig).</summary>
        public void PlayAttack()
        {
            _released = false;

            if (HasRig)
            {
                // La cola de un gesto de la baraja (su vuelta al reposo) no puede acabar avisando
                // como si fuera este ataque: se suelta antes de empezar.
                if (_rigGesture)
                {
                    _rigGesture = false;
                    rig.ClearHold();
                }

                float speed = _stats != null ? _stats.Tuning.attackAnimSpeed : 1f;
                rig.AimTarget = _attack != null ? _attack.AimPoint : null;
                rig.PlayAttack(_attack != null ? _attack.AimPoint : null, speed);

                // Sólo red de seguridad (un rig que se apaga a mitad del gesto): el rig avisa él
                // mismo en el instante exacto, y esto llegaría bastante después.
                var data = rig.GetComponent<ProceduralRig>();
                float total = (data != null ? data.AttackDuration(speed) : 1f) + 1f;
                _fallbackRelease = Time.time + total;
                _fallbackFinish = Time.time + total;
                return;
            }

            if (HasAnimator)
            {
                animator.SetTrigger(AttackKey);
                ArmFallback(ClipLength(Attack, _stats != null ? _stats.Tuning.attackAnimSpeed : 1f));
                return;
            }

            if (flipbook != null && flipbook.Has(Attack))
            {
                flipbook.Play(Attack, true);
                ArmFallback(flipbook.DurationOf(Attack));
                return;
            }

            // Sin animación de ataque no se deja al enemigo mudo: se avisa igual, en el acto, para
            // que el golpe exista aunque el arte todavía no.
            Release();
            Finish();
        }

        /// <summary>
        /// Volver a dormir (o nacer dormido, con <paramref name="instant"/>). Sólo lo usa un rig:
        /// con Animator el reposo de un dormilón ya ES el clip de dormir.
        /// </summary>
        public void PlaySleep(bool instant)
        {
            if (HasRig) rig.PlaySleep(instant);
        }

        /// <summary>
        /// Despertar. Avisa con <see cref="WakeFinished"/> al acabar el clip; sin clip de
        /// despertar avisa en el acto, para que el enemigo no se quede dormido para siempre.
        /// </summary>
        public void PlayWake()
        {
            if (HasRig)
            {
                rig.PlayWake(_stats != null ? _stats.Tuning.wakeAnimSpeed : 1f);
                return;
            }

            if (HasAnimator && _hasWakeTrigger)
            {
                animator.SetTrigger(WakeKey);
                _wakeFinish = Time.time + ClipLength(Wake, _stats != null ? _stats.Tuning.wakeAnimSpeed : 1f);
                return;
            }

            if (flipbook != null && flipbook.Has(Wake))
            {
                flipbook.Play(Wake, true);
                _wakeFinish = Time.time + flipbook.DurationOf(Wake);
                return;
            }

            WakeFinished?.Invoke();
        }

        public void PlayHurt()
        {
            if (HasRig) rig.PlayHurt();
            else if (HasAnimator) animator.SetTrigger(HurtKey);
            else if (flipbook != null && flipbook.Has(Hurt)) flipbook.Play(Hurt, true);
        }

        public void PlayDeath()
        {
            _dead = true;
            _fallbackRelease = _fallbackFinish = -1f;

            if (HasRig)
            {
                rig.PlayDeath();
                return;
            }

            if (HasAnimator) animator.SetBool(DeadKey, true);

            if (flipbook == null) return;
            flipbook.PlayOnce(Death);   // una pasada: el cadáver se queda en el último frame
        }

        /// <summary>-1 mira a la izquierda, 1 a la derecha.</summary>
        public void SetFacing(int facing)
        {
            if (HasRig) rig.SetFacing(facing);
            else if (spriteRenderer != null) spriteRenderer.flipX = facing < 0;
        }

        // ============================================================ gestos de la baraja (IAttackBody)

        /// <summary>
        /// Gesto de un ataque del catálogo: un <c>RigGesture</c> con ese nombre (se ajusta para
        /// soltar en <paramref name="secondsToRelease"/>) o un estado del Animator (suelta en su
        /// evento <c>OnAttackRelease</c>, a su velocidad). False si el cuerpo no lo tiene: quien
        /// llama cae al aviso por tiempo.
        /// </summary>
        /// <summary>Hacia dónde va el ataque del catálogo que se va a lanzar: orienta los efectos de disparo del rig.</summary>
        public void AimAt(Vector2 point)
        {
            if (HasRig) rig.AimTarget = point;
        }

        public bool PlayGesture(string state, float secondsToRelease)
        {
            if (_dead || string.IsNullOrWhiteSpace(state)) return false;

            if (HasRig)
            {
                if (!rig.PlayGesture(state, secondsToRelease)) return false;

                // Un gesto con tiempos propios suelta cuando digan sus pasos, no a 'secondsToRelease'.
                _rigGesture = true;
                StartGesture(rig.SecondsToRelease(state, secondsToRelease), 0.3f);
                return true;
            }

            if (!HasAnimator) return false;

            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) return false;

            _gestureHash = hash;
            animator.Play(hash, 0, 0f);
            StartGesture(ReleaseTime(state, secondsToRelease), 0.1f);
            return true;
        }

        private void StartGesture(float secondsToRelease, float slack)
        {
            _inGesture = true;
            Released = false;
            SecondsToRelease = secondsToRelease;
            _gestureReleaseAt = Time.time + secondsToRelease + slack;   // sólo red de seguridad
            _fallbackRelease = _fallbackFinish = -1f;
        }

        /// <summary>Mantiene una pose (el viaje de una embestida) hasta <see cref="ReturnToRest"/>.</summary>
        public bool Hold(string state)
        {
            if (_dead || string.IsNullOrWhiteSpace(state)) return false;

            if (HasRig)
            {
                _rigGesture = false;   // una pose mantenida no avisa de nada
                return rig.HoldGesture(state);
            }

            if (!HasAnimator) return false;

            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) return false;

            _gestureHash = hash;
            animator.Play(hash, 0, 0f);
            return true;
        }

        public void ClearHold() => ReturnToRest();

        /// <summary>
        /// Un movimiento (una esquiva) que dura <paramref name="seconds"/>: un cuerpo por partes reproduce el gesto
        /// entero, ajustado a ese tiempo, y vuelve al reposo solo; un Animator mantiene el estado (<see cref="Hold"/>)
        /// hasta <see cref="EndMotion"/>.
        /// </summary>
        public bool PlayMotion(string state, float seconds)
        {
            if (_dead || string.IsNullOrWhiteSpace(state)) return false;
            if (!HasRig) return Hold(state);

            _rigGesture = false;   // no avisa de nada
            return rig.PlayMotion(state, seconds);
        }

        /// <summary>El movimiento ha terminado. El gesto de un rig acaba su vuelta al reposo él solo.</summary>
        public void EndMotion()
        {
            if (!HasRig) ReturnToRest();
        }

        /// <summary>Suelta el gesto o la pose que hubiera y vuelve al reposo.</summary>
        public void ReturnToRest()
        {
            if (_dead) return;

            if (HasRig)
            {
                _rigGesture = false;
                rig.ClearHold();
                return;
            }

            if (!HasAnimator || _gestureHash == 0) return;

            // Sólo si sigue en el estado del gesto: un Hurt o la muerte que lo cortaron mandan.
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _gestureHash && animator.HasState(0, IdleHash))
                animator.Play(IdleHash, 0, 0f);
            _gestureHash = 0;
        }

        /// <summary>
        /// El ataque de la baraja ha terminado (o lo cortaron): las sueltas vuelven a ser del
        /// ataque propio. El gesto de un rig acaba su vuelta al reposo él solo.
        /// </summary>
        public void EndGesture()
        {
            if (!_inGesture) return;

            _inGesture = false;
            _gestureReleaseAt = -1f;
            if (!HasRig) ReturnToRest();
        }

        /// <summary>Cuándo suelta el clip de <paramref name="state"/> (su evento de suelta), a su velocidad.</summary>
        private float ReleaseTime(string state, float fallback)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null || !clip.name.EndsWith(state, StringComparison.Ordinal)) continue;

                float release = clip.length;
                foreach (var e in clip.events)
                {
                    if (e.functionName != nameof(OnAttackRelease)) continue;
                    release = e.time;
                    break;
                }

                float speed = state == Attack && _stats != null ? _stats.Tuning.attackAnimSpeed : 1f;
                return release / Mathf.Max(0.01f, speed);
            }

            return fallback;
        }

        // ============================================================ eventos del clip

        /// <summary>Lo llama el <c>AnimationEvent</c> del frame en que suelta el golpe.</summary>
        public void OnAttackRelease() => Release();

        /// <summary>Lo llama el <c>AnimationEvent</c> del último frame del ataque.</summary>
        public void OnAttackFinished() => Finish();

        private void Release()
        {
            // En un gesto de la baraja la suelta es la de ese ataque (la espera EnemyAttackHost).
            if (_inGesture)
            {
                Released = true;
                return;
            }

            if (_released) return;
            _released = true;
            _fallbackRelease = -1f;
            AttackReleased?.Invoke();
        }

        private void Finish()
        {
            if (_inGesture) return;   // el fin lo marca EnemyAttackHost (EndGesture)

            _fallbackFinish = -1f;
            // Un ataque cuya animación acaba sin haber soltado nada (evento mal puesto) suelta
            // aquí: mejor un golpe a destiempo que un enemigo que no ataca nunca.
            Release();
            AttackFinished?.Invoke();
        }

        // ============================================================ respaldo por tiempo

        private void ArmFallback(float clipLength)
        {
            float release = _stats != null ? _stats.Tuning.attackReleaseFallback : 0.25f;
            float total = clipLength > 0.01f ? clipLength : Mathf.Max(release * 2f, 0.4f);

            // Con eventos en el clip esto no llega a disparar: el evento los desarma antes. Está
            // por si el clip viene sin eventos (arte importado a mano, por ejemplo).
            _fallbackRelease = Time.time + Mathf.Min(release, total);
            _fallbackFinish = Time.time + total;
        }

        private void TickFallback()
        {
            if (_inGesture && !Released && _gestureReleaseAt > 0f && Time.time >= _gestureReleaseAt) Released = true;

            if (_fallbackRelease > 0f && Time.time >= _fallbackRelease) Release();
            if (_fallbackFinish > 0f && Time.time >= _fallbackFinish) Finish();

            if (_wakeFinish > 0f && Time.time >= _wakeFinish)
            {
                _wakeFinish = -1f;
                WakeFinished?.Invoke();
            }
        }

        /// <summary>Duración real del clip del estado, ya dividida por su multiplicador de velocidad.</summary>
        private float ClipLength(string state, float speedMultiplier)
        {
            if (!HasAnimator) return 0f;

            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null || !clip.name.EndsWith(state, StringComparison.Ordinal)) continue;
                return clip.length / Mathf.Max(0.01f, speedMultiplier);
            }

            return 0f;
        }
    }
}
