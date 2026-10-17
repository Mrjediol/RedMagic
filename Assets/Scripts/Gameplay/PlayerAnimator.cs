using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Traduce el estado de <see cref="PlayerMovement"/> y <see cref="Health"/> a los parámetros
    /// del Animator del dragón. No decide nada de gameplay: sólo observa y escribe parámetros.
    ///
    /// Parámetros del controller del jugador:
    ///  - Speed (Float)     velocidad horizontal absoluta
    ///  - VSpeed (Float)    velocidad vertical con signo (separa Jump de Fall)
    ///  - Grounded (Bool)   tocando suelo
    ///  - Attack (Trigger)  lo dispara <see cref="PlayerAttack"/>
    ///  - Hurt (Trigger)    al recibir daño
    ///  - Dead (Bool)       al morir; vuelve a false si Health dispara Revived (respawn)
    ///  - Dashing (Bool)    mientras dura el dash
    ///  - DoubleJump (Trigger) al gastar un salto en el aire
    ///  - Interact (Trigger) lo dispara el interactuable que se está usando (ver <see cref="TriggerInteract"/>)
    ///
    /// También voltea el dibujo alrededor del <b>centro del jugador</b> (su origen = centro de la caja), no del pivote
    /// del sprite: <c>flipX</c> espeja sobre el pivote, y como el cuerpo no está centrado en él (cola a un lado,
    /// mochila al otro) al darse la vuelta saltaba de sitio y dejaba de coincidir con la caja. Se coloca el hijo
    /// <c>Sprite</c> mirando a la derecha con el cuerpo sobre el origen, y al voltear se refleja también su X.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAnimator : MonoBehaviour
    {
        [Tooltip("Animator del dragón. Si se deja vacío se busca en los hijos.")]
        [SerializeField] private Animator animator;

        private PlayerMovement _movement;
        private Health _health;
        private SpriteRenderer _sprite;
        private float _spriteRightX;   // X local del hijo Sprite mirando a la derecha (la del prefab)

        private static readonly int SpeedKey = Animator.StringToHash("Speed");
        private static readonly int VSpeedKey = Animator.StringToHash("VSpeed");
        private static readonly int GroundedKey = Animator.StringToHash("Grounded");
        private static readonly int AttackKey = Animator.StringToHash("Attack");
        private static readonly int HurtKey = Animator.StringToHash("Hurt");
        private static readonly int DeadKey = Animator.StringToHash("Dead");
        private static readonly int DashingKey = Animator.StringToHash("Dashing");
        private static readonly int DoubleJumpKey = Animator.StringToHash("DoubleJump");
        private static readonly int InteractKey = Animator.StringToHash("Interact");
        private static readonly int IdleStateHash = Animator.StringToHash("Idle");

        /// <summary>Animator en uso, por si otro script necesita consultarlo.</summary>
        public Animator Animator => animator;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _movement = GetComponent<PlayerMovement>();
            _health = GetComponent<Health>();

            _sprite = animator != null ? animator.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
            if (_sprite != null && _sprite.transform != transform) _spriteRightX = _sprite.transform.localPosition.x;
            else _sprite = null;   // el sprite en la raíz no se puede reflejar moviéndolo

            if (animator == null)
                Debug.LogWarning("[PlayerAnimator] No hay Animator ni en este objeto ni en sus hijos.", this);
        }

        private void OnEnable()
        {
            if (_movement != null) _movement.AirJumped += OnAirJumped;

            if (_health == null) return;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            _health.Revived += OnRevived;
        }

        private void OnDisable()
        {
            if (_movement != null) _movement.AirJumped -= OnAirJumped;

            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            _health.Revived -= OnRevived;
        }

        private void Update()
        {
            if (animator == null || _movement == null) return;

            // Durante el arranque el controlador aún no ha corrido sus sensores, así que
            // IsGrounded es false y se vería la pose de caída al aparecer. Se fuerza Idle.
            if (!_movement.IsActive)
            {
                animator.SetFloat(SpeedKey, 0f);
                animator.SetFloat(VSpeedKey, 0f);
                animator.SetBool(GroundedKey, true);
                animator.SetBool(DashingKey, false);
                return;
            }

            var raw = _movement.RawMovement;
            animator.SetFloat(SpeedKey, Mathf.Abs(raw.x));
            animator.SetFloat(VSpeedKey, raw.y);
            animator.SetBool(GroundedKey, _movement.IsGrounded);
            animator.SetBool(DashingKey, _movement.IsDashing);
        }

        // LateUpdate: después de todo lo que escribe flipX este frame (movimiento, dash, disparo…).
        private void LateUpdate()
        {
            if (_sprite == null) return;
            var p = _sprite.transform.localPosition;
            p.x = _sprite.flipX ? -_spriteRightX : _spriteRightX;
            _sprite.transform.localPosition = p;
        }

        private void OnAirJumped()
        {
            if (animator != null) animator.SetTrigger(DoubleJumpKey);
        }

        /// <summary>Lo llama <see cref="PlayerAttack"/> al iniciar un ataque.</summary>
        public void TriggerAttack()
        {
            if (animator != null) animator.SetTrigger(AttackKey);
        }

        /// <summary>Lo llama el interactuable en uso (cofre, armario, yunque, tienda…) al pulsar interactuar.</summary>
        public void TriggerInteract()
        {
            if (animator != null) animator.SetTrigger(InteractKey);
        }

        private void OnDamaged(float amount)
        {
            // Si el golpe es mortal deja que mande la animación de muerte.
            if (animator == null || _health == null || _health.IsDead) return;
            animator.SetTrigger(HurtKey);
        }

        private void OnDied()
        {
            if (animator == null) return;
            animator.ResetTrigger(HurtKey);
            animator.ResetTrigger(AttackKey);
            animator.SetBool(DeadKey, true);
        }

        /// <summary>
        /// Al revivir (vuelta al hub, respawn) se sale de la muerte en el acto. El estado Die del controlador no tiene
        /// transición de salida (sólo se entra por Any State), así que bajar Dead no bastaba: se quedaba en la pose de
        /// muerte hasta que otro trigger (atacar, saltar…) lo sacaba.
        /// </summary>
        private void OnRevived()
        {
            if (animator == null) return;
            animator.SetBool(DeadKey, false);
            animator.ResetTrigger(HurtKey);
            animator.ResetTrigger(AttackKey);
            if (!animator.HasState(0, IdleStateHash)) return;
            animator.Play(IdleStateHash, 0, 0f);
            animator.Update(0f);
        }
    }
}
