using System.Collections.Generic;
using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Bosses
{
    /// <summary>
    /// La capa de animación de un jefe con <see cref="Animator"/>: reproduce en su propio cuerpo el
    /// <b>gesto</b> de cada ataque (<see cref="BossAttack.Gesture"/>) y avisa del frame exacto en el
    /// que el golpe sale. Es el equivalente de <c>EnemyAnimation</c> para jefes, con los mismos dos
    /// <c>AnimationEvent</c> que planta el pipeline — <see cref="OnAttackRelease"/> en el
    /// <c>releaseFrame</c> de la fila y <see cref="OnAttackFinished"/> al final —, así que un jefe no
    /// necesita un sistema de disparo propio: el orbe, la onda o la púa nacen en el dibujo.
    ///
    /// <see cref="BossController"/> lo usa si está; es opcional, y los jefes de un solo sprite siguen
    /// igual. El ritmo lo sigue llevando el controlador: pide el gesto con los segundos de aviso que
    /// tocan en la fase, y aquí se ajusta la velocidad del clip para que la suelta caiga justo ahí.
    /// Por eso la fase 2, que acorta los avisos, acelera los gestos sin tocar nada más.
    ///
    /// Va en la <b>raíz</b> del jefe, junto al Animator: los AnimationEvent sólo llegan a
    /// componentes del mismo GameObject. Los clips animan al hijo "Sprite" por ruta.
    ///
    /// Los gestos son estados sueltos del controller (el generador no les pone transiciones); al
    /// acabar, este componente devuelve el Animator al reposo. Tambaleo y muerte usan lo que el
    /// pipeline ya cablea: la salida de Hurt a Idle y el bool Dead.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossAnimator : MonoBehaviour
    {
        /// <summary>Evento que el pipeline clava en el frame de suelta (ver AnimClipBuilder).</summary>
        private const string ReleaseEvent = "OnAttackRelease";

        [Tooltip("Vacío = el de este objeto. Tiene que estar en la RAÍZ del jefe, o los eventos de " +
                 "los clips no llegarían aquí.")]
        [SerializeField] private Animator animator;

        [Header("Estados")]
        [SerializeField] private string idleState = "Idle";

        [Tooltip("Tambaleo: al cambiar de fase y, con 'flinchOnHit', al recibir golpes.")]
        [SerializeField] private string staggerState = "Hurt";

        [Tooltip("Sólo si el controller no tiene el bool 'Dead' (el que cablea el pipeline).")]
        [SerializeField] private string deathState = "Death";

        [Header("Velocidad")]
        [Tooltip("Velocidad del reposo con ritmo 1. Se multiplica por el ritmo de la fase " +
                 "(speedScale, frenesí incluido): en la fase 2 el jefe respira más deprisa.")]
        [Min(0.05f)]
        [SerializeField] private float idleSpeed = 1f;

        [Tooltip("Velocidad mínima y máxima de un gesto. El gesto se ajusta para que su frame de " +
                 "suelta caiga al acabar el aviso; con un aviso extremo el clip iría a ×20 o ×0.1, " +
                 "así que se recorta — y entonces manda el dibujo: el ataque espera a la suelta.")]
        [SerializeField] private Vector2 gestureSpeedRange = new Vector2(0.35f, 3f);

        [Header("Golpes")]
        [Tooltip("Tambalearse al recibir daño. Sólo en reposo (nunca corta un gesto, que es un " +
                 "ataque en marcha) y como mucho una vez cada 'flinchCooldown' segundos: un jefe " +
                 "acribillado no puede pasarse la pelea encogido.")]
        [SerializeField] private bool flinchOnHit = true;

        [Min(0f)]
        [SerializeField] private float flinchCooldown = 3f;

        [Tooltip("Daño mínimo de un golpe para que se tambalee. 0 = cualquiera.")]
        [Min(0f)]
        [SerializeField] private float flinchMinDamage;

        private struct ClipTiming
        {
            public float release;
            public float length;
        }

        private static readonly int DeadKey = Animator.StringToHash("Dead");

        private readonly Dictionary<int, ClipTiming> _timings = new Dictionary<int, ClipTiming>();
        private readonly HashSet<int> _parameters = new HashSet<int>();
        private readonly HashSet<int> _warnedMissing = new HashSet<int>();

        private Health _health;
        private int _idleHash, _staggerHash, _deathHash, _gestureHash;
        private bool _inGesture;
        private bool _dead;
        private float _releaseAt = -1f;
        private float _finishAt = -1f;
        private float _lastFlinch = float.NegativeInfinity;

        public bool HasAnimator => animator != null && animator.runtimeAnimatorController != null;

        /// <summary>True desde que el gesto en curso ha soltado su golpe.</summary>
        public bool Released { get; private set; }

        /// <summary>Segundos que tardará en soltar el último gesto lanzado, ya con su velocidad.</summary>
        public float SecondsToRelease { get; private set; }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            _health = GetComponent<Health>();

            _idleHash = Animator.StringToHash(idleState);
            _staggerHash = Animator.StringToHash(staggerState);
            _deathHash = Animator.StringToHash(deathState);

            CacheController();
        }

        private void OnEnable()
        {
            if (_health == null) return;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void Update()
        {
            if (!_inGesture) return;

            // Respaldo por tiempo: con los eventos en el clip no llega a disparar. Está por si el
            // clip viene sin eventos, para que el jefe no se quede con el ataque a medias.
            if (_releaseAt > 0f && Time.time >= _releaseAt) Release();
            if (_finishAt > 0f && Time.time >= _finishAt) FinishGesture();
        }

        /// <summary>
        /// Qué parámetros existen y cuándo suelta cada clip. El frame de suelta se lee del propio
        /// evento del clip, así que retocar <c>releaseFrame</c> en la receta y regenerar basta.
        /// </summary>
        private void CacheController()
        {
            _timings.Clear();
            _parameters.Clear();
            if (!HasAnimator) return;

            foreach (var parameter in animator.parameters) _parameters.Add(parameter.nameHash);

            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;

                // Convención del pipeline: el clip del estado X se llama <Personaje>_X.
                int cut = clip.name.LastIndexOf('_');
                string state = cut >= 0 ? clip.name.Substring(cut + 1) : clip.name;

                float release = clip.length;
                foreach (var e in clip.events)
                {
                    if (e.functionName != ReleaseEvent) continue;
                    release = e.time;
                    break;
                }

                _timings[Animator.StringToHash(state)] = new ClipTiming { release = release, length = clip.length };
            }
        }

        // ============================================================ órdenes del controlador

        /// <summary>Ritmo de la fase. Acelera el reposo; los gestos ya se ajustan solos a su aviso.</summary>
        public void SetPace(float pace) => SetSpeed(idleState, idleSpeed * Mathf.Max(0.05f, pace));

        /// <summary>
        /// Lanza el gesto <paramref name="state"/> ajustado para soltar dentro de
        /// <paramref name="secondsToRelease"/> segundos. Devuelve false si no hay Animator o el
        /// estado no existe: el controlador cae entonces al aviso por tiempo de siempre.
        /// </summary>
        public bool PlayGesture(string state, float secondsToRelease)
        {
            if (_dead || !HasAnimator || string.IsNullOrWhiteSpace(state)) return false;

            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash))
            {
                if (_warnedMissing.Add(hash))
                    Debug.LogWarning($"[BossAnimator] '{name}' no tiene el estado '{state}'; el ataque " +
                                     "sale sin gesto.", this);
                return false;
            }

            // Sin clip reconocible se reproduce a velocidad 1 y se suelta por tiempo, al acabar el aviso.
            if (!_timings.TryGetValue(hash, out var timing))
                timing = new ClipTiming { release = secondsToRelease, length = secondsToRelease };

            float min = Mathf.Max(0.05f, gestureSpeedRange.x);
            float max = Mathf.Max(min, gestureSpeedRange.y);
            float speed = timing.release > 0.001f && secondsToRelease > 0.001f
                ? timing.release / secondsToRelease
                : max;
            speed = Mathf.Clamp(speed, min, max);

            SetSpeed(state, speed);

            _gestureHash = hash;
            _inGesture = true;
            Released = false;
            SecondsToRelease = timing.release / speed;

            _releaseAt = Time.time + SecondsToRelease + 0.1f;
            _finishAt = Time.time + Mathf.Max(timing.length, timing.release) / speed + 0.1f;

            animator.Play(hash, 0, 0f);
            return true;
        }

        /// <summary>Tambaleo: corta el gesto que hubiera (sin soltarlo) y se encoge.</summary>
        public void PlayStagger()
        {
            if (_dead || !HasAnimator) return;

            CancelGesture();
            _lastFlinch = Time.time;

            if (animator.HasState(0, _staggerHash)) animator.Play(_staggerHash, 0, 0f);
        }

        // ============================================================ eventos del clip

        /// <summary>Lo llama el <c>AnimationEvent</c> del frame de suelta.</summary>
        public void OnAttackRelease()
        {
            if (_inGesture) Release();
        }

        /// <summary>Lo llama el <c>AnimationEvent</c> del último frame.</summary>
        public void OnAttackFinished()
        {
            if (_inGesture) FinishGesture();
        }

        private void Release()
        {
            if (Released) return;
            Released = true;
            _releaseAt = -1f;
        }

        private void FinishGesture()
        {
            // Un gesto que acaba sin haber soltado (evento mal puesto) suelta aquí: mejor un golpe a
            // destiempo que un jefe que se queda mirando.
            Release();
            _inGesture = false;
            _finishAt = -1f;

            if (HasAnimator && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _gestureHash)
                animator.Play(_idleHash, 0, 0f);
        }

        private void CancelGesture()
        {
            _inGesture = false;
            _releaseAt = -1f;
            _finishAt = -1f;
        }

        // ============================================================ vida

        private void OnDamaged(float amount)
        {
            if (!flinchOnHit || _dead || _inGesture || !HasAnimator) return;
            if (amount < flinchMinDamage || Time.time - _lastFlinch < flinchCooldown) return;
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != _idleHash) return;

            PlayStagger();
        }

        private void OnDied()
        {
            _dead = true;
            CancelGesture();
            if (!HasAnimator) return;

            if (_parameters.Contains(DeadKey)) animator.SetBool(DeadKey, true);
            else if (animator.HasState(0, _deathHash)) animator.Play(_deathHash, 0, 0f);
        }

        private void SetSpeed(string state, float value)
        {
            if (!HasAnimator) return;

            int key = Animator.StringToHash(state + "Speed");
            if (_parameters.Contains(key)) animator.SetFloat(key, value);
        }
    }
}
