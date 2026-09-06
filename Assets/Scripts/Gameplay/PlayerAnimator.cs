using RedMagic.Combat;
using UnityEngine;

namespace RedMagic.Gameplay
{
    /// <summary>
    /// Traduce el estado de <see cref="PlayerMovement"/> y <see cref="Health"/> a los parámetros
    /// del Animator del dragón. No decide nada de gameplay: sólo observa y escribe parámetros.
    ///
    /// Parámetros del controller DragonWarrior:
    ///  - Speed (Float)     velocidad horizontal absoluta
    ///  - VSpeed (Float)    velocidad vertical con signo (separa Jump de Fall)
    ///  - Grounded (Bool)   tocando suelo
    ///  - Crouching (Bool)  agachado
    ///  - Attack (Trigger)  lo dispara <see cref="PlayerAttack"/>
    ///  - Hurt (Trigger)    al recibir daño
    ///  - Dead (Bool)       al morir (estado terminal, sin salida)
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAnimator : MonoBehaviour
    {
        [Tooltip("Animator del dragón. Si se deja vacío se busca en los hijos.")]
        [SerializeField] private Animator animator;

        private PlayerMovement _movement;
        private Health _health;

        private static readonly int SpeedKey = Animator.StringToHash("Speed");
        private static readonly int VSpeedKey = Animator.StringToHash("VSpeed");
        private static readonly int GroundedKey = Animator.StringToHash("Grounded");
        private static readonly int CrouchingKey = Animator.StringToHash("Crouching");
        private static readonly int AttackKey = Animator.StringToHash("Attack");
        private static readonly int HurtKey = Animator.StringToHash("Hurt");
        private static readonly int DeadKey = Animator.StringToHash("Dead");

        /// <summary>Animator en uso, por si otro script necesita consultarlo.</summary>
        public Animator Animator => animator;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _movement = GetComponent<PlayerMovement>();
            _health = GetComponent<Health>();

            if (animator == null)
                Debug.LogWarning("[PlayerAnimator] No hay Animator ni en este objeto ni en sus hijos.", this);
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
            if (animator == null || _movement == null) return;

            // Durante el arranque el controlador aún no ha corrido sus sensores, así que
            // IsGrounded es false y se vería la pose de caída al aparecer. Se fuerza Idle.
            if (!_movement.IsActive)
            {
                animator.SetFloat(SpeedKey, 0f);
                animator.SetFloat(VSpeedKey, 0f);
                animator.SetBool(GroundedKey, true);
                animator.SetBool(CrouchingKey, false);
                return;
            }

            var raw = _movement.RawMovement;
            animator.SetFloat(SpeedKey, Mathf.Abs(raw.x));
            animator.SetFloat(VSpeedKey, raw.y);
            animator.SetBool(GroundedKey, _movement.IsGrounded);
            animator.SetBool(CrouchingKey, _movement.IsCrouching);
        }

        /// <summary>Lo llama <see cref="PlayerAttack"/> al iniciar un ataque.</summary>
        public void TriggerAttack()
        {
            if (animator != null) animator.SetTrigger(AttackKey);
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
    }
}
