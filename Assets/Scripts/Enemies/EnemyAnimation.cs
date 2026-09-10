using System;
using RedMagic.Pipeline;
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
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyAnimation : MonoBehaviour
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
        private float _fallbackRelease = -1f;
        private float _fallbackFinish = -1f;
        private float _wakeFinish = -1f;
        private bool _released;
        private bool _hasWakeTrigger;

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

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (flipbook == null) flipbook = GetComponentInChildren<SpriteStateMachine>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _stats = GetComponent<EnemyStats>();

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
        public void SetMoving(bool moving)
        {
            if (HasAnimator) animator.SetBool(MovingKey, moving);

            if (flipbook == null) return;
            flipbook.Play(moving && flipbook.Has(Walk) ? Walk : Idle);
        }

        /// <summary>Lanza el ataque. El golpe sale luego, cuando lo diga el clip.</summary>
        public void PlayAttack()
        {
            _released = false;

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
        /// Despertar. Avisa con <see cref="WakeFinished"/> al acabar el clip; sin clip de
        /// despertar avisa en el acto, para que el enemigo no se quede dormido para siempre.
        /// </summary>
        public void PlayWake()
        {
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
            if (HasAnimator) animator.SetTrigger(HurtKey);
            else if (flipbook != null && flipbook.Has(Hurt)) flipbook.Play(Hurt, true);
        }

        public void PlayDeath()
        {
            if (HasAnimator) animator.SetBool(DeadKey, true);

            if (flipbook == null) return;
            flipbook.Locked = false;
            flipbook.Play(Death, true);
            flipbook.Locked = true;   // el cadáver se queda en el último frame
        }

        /// <summary>-1 mira a la izquierda, 1 a la derecha.</summary>
        public void SetFacing(int facing)
        {
            if (spriteRenderer != null) spriteRenderer.flipX = facing < 0;
        }

        // ============================================================ eventos del clip

        /// <summary>Lo llama el <c>AnimationEvent</c> del frame en que suelta el golpe.</summary>
        public void OnAttackRelease() => Release();

        /// <summary>Lo llama el <c>AnimationEvent</c> del último frame del ataque.</summary>
        public void OnAttackFinished() => Finish();

        private void Release()
        {
            if (_released) return;
            _released = true;
            _fallbackRelease = -1f;
            AttackReleased?.Invoke();
        }

        private void Finish()
        {
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
